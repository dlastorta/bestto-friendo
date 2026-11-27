using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresByeTests
{
    internal static async Task<(Guid Id, Guid[] Players)> Seed(WebApplicationFactory<Program> factory, HttpClient owner, HttpClient player,
        bool historicalManual = true)
    {
        var seed = await PostgresTournamentStartTests.Seed(factory, owner, player);
        using var third = factory.CreateClient();
        await IdentityEndpointsTests.RegisterAndLogin(third, $"bye-{Guid.NewGuid():N}@example.test", "Tercero");
        var thirdId = (await third.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
        using var scope = factory.Services.CreateScope();
        var s = scope.ServiceProvider;
        var drafts = s.GetRequiredService<ITournamentDrafts>();
        var t = (await drafts.FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
        var game = (await s.GetRequiredService<IGameCatalog>().FindAsync(t.GameId, default))!;
        var deck = Deck.Create(thirdId, game, "Mazo tercero", "Privado", game.Factions.Select(f => f.Id));
        await s.GetRequiredService<IDeckLibrary>().AddAsync(deck, default);
        var reg = TournamentRegistration.Create(t, thirdId, DateTimeOffset.UtcNow);
        reg.Present(t, deck, 1, DateTimeOffset.UtcNow);
        await s.GetRequiredService<ITournamentRegistrations>().AddAsync(reg, default);
        t.Start(3, 3, t.RulesVersion, t.ParticipationVersion, DateTimeOffset.UtcNow);
        if (historicalManual)
            s.GetRequiredService<TournamentDbContext>().Entry(t.StartedRules!).Property(r => r.PairingAlgorithmVersion).CurrentValue = 0;
        await drafts.SaveAsync(t, default);
        return (t.Id, [seed.OwnerId, seed.PlayerId, thirdId]);
    }

    [Fact]
    public async Task HttpByesRequireOwnerRoleCsrfAndPreserveHistoricalPolicyAndPrivacy()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var anon = factory.CreateClient();
        var (id, p) = await Seed(factory, owner, player);
        var path = $"/api/tournaments/{id}/byes";
        var command = new AssignBye(1, p[1]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync(path, command)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, command)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            var t = (await scope.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
            db.Entry(t.Rules!).Property(r => r.ByePoints).CurrentValue = 999;
            db.Entry(t.StartedRules!).Property(r => r.ByePolicyVersion).CurrentValue = 0;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, command)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            var t = (await scope.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
            db.Entry(t.StartedRules!).Property(r => r.ByePolicyVersion).CurrentValue = 1;
            await db.SaveChangesAsync();
        }
        var response = await owner.PostAsJsonAsync(path, command);
        response.EnsureSuccessStatusCode();
        var bye = (await response.Content.ReadFromJsonAsync<ByeDto>())!;
        Assert.Equal(3, bye.Points); Assert.Equal(p[0], bye.ActorPlayerId);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, command with { Round = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, command with { PlayerId = p[2] })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/tournaments/{id}/matches", new CreateMatch(1, p[0], p[1]))).StatusCode);
        var rows = (await player.GetFromJsonAsync<StandingDto[]>($"/api/tournaments/{id}/standings"))!;
        Assert.Equal(3, rows.Single(r => r.PlayerId == p[1]).Points);
        Assert.All(rows.SelectMany(r => r.TieBreakers), v => Assert.Equal(0, v.Value));
        var json = await owner.GetStringAsync(path);
        Assert.DoesNotContain("Privado", json); Assert.DoesNotContain("deck", json, StringComparison.OrdinalIgnoreCase);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var other = db.Players.Single(x => x.Id == p[1]).AccountId;
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(other.ToString()))!, "TournamentOrganizer")).Succeeded);
            var account = db.Players.Single(x => x.Id == p[0]).AccountId;
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(account.ToString()))!, "TournamentOrganizer")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await player.PostAsJsonAsync(path, command)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, command)).StatusCode);
    }

    [Theory]
    [InlineData("match", true)] [InlineData("match", false)]
    [InlineData("round", true)] [InlineData("round", false)]
    [InlineData("player", true)] [InlineData("player", false)]
    public async Task ConcurrentAssignmentsCommitOnlyOneAndRollbackTheOther(string conflict, bool byeFirst)
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        var (id, p) = await Seed(factory, owner, player);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var ta = (await a.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        var tb = (await b.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        var ba = a.ServiceProvider.GetRequiredService<ITournamentByes>();
        var bb = b.ServiceProvider.GetRequiredService<ITournamentByes>();
        var bye = TournamentBye.Assign(ta, 1, p[0], p, [], [], p[0], DateTimeOffset.UtcNow);
        Func<Task> other;
        if (conflict == "match")
        {
            var match = TournamentMatch.Create(tb, 1, p[1], p[0], p, [], p[0], DateTimeOffset.UtcNow, []);
            other = () => b.ServiceProvider.GetRequiredService<ITournamentMatches>().AddAsync(match, default);
        }
        else
        {
            var second = TournamentBye.Assign(tb, conflict == "round" ? 1 : 2, conflict == "round" ? p[1] : p[0], p, [], [], p[0], DateTimeOffset.UtcNow);
            other = () => bb.AddAsync(second, default);
        }
        if (byeFirst)
        {
            await ba.AddAsync(bye, default);
            await Assert.ThrowsAsync<TournamentConflictException>(other);
        }
        else
        {
            await other();
            await Assert.ThrowsAsync<TournamentConflictException>(() => ba.AddAsync(bye, default));
        }
        using var check = factory.Services.CreateScope();
        var byes = await check.ServiceProvider.GetRequiredService<ITournamentByes>().ListAsync(id, default);
        var matches = await check.ServiceProvider.GetRequiredService<ITournamentMatches>().ListAsync(id, default);
        Assert.Equal(1, byes.Count + matches.Count);
        var persisted = (await check.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        Assert.Equal(1, persisted.MatchesVersion);
    }
}
