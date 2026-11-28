using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresPublicationTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ConsentAndSnapshotReplacementCannotAuthorizeAnotherVersion(bool consentFirst)
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>(); using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        var seed = await PostgresTournamentStartTests.Seed(factory, owner, player);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var ra = a.ServiceProvider.GetRequiredService<ITournamentRegistrations>();
        var rb = b.ServiceProvider.GetRequiredService<ITournamentRegistrations>();
        var consent = (await ra.FindAsync(seed.TournamentId, seed.PlayerId, default))!;
        var replacement = (await rb.FindAsync(seed.TournamentId, seed.PlayerId, default))!;
        var tournament = (await b.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
        var deck = (await b.ServiceProvider.GetRequiredService<TournamentManager.Application.Features.Decks.IDeckLibrary>()
            .FindOwnedAsync(seed.DeckId, seed.PlayerId, default))!;
        consent.SetDeckVisibility(true, 1, 0, DateTimeOffset.UtcNow);
        replacement.Replace(tournament, deck, deck.Version, 1, "Reemplazo concurrente", DateTimeOffset.UtcNow);
        if (consentFirst) { await ra.SaveAsync(default); await Assert.ThrowsAsync<TournamentConflictException>(() => rb.SaveAsync(default)); }
        else { await rb.SaveAsync(default); await Assert.ThrowsAsync<TournamentConflictException>(() => ra.SaveAsync(default)); }
        using var check = factory.Services.CreateScope();
        var stored = (await check.ServiceProvider.GetRequiredService<ITournamentRegistrations>().FindAsync(seed.TournamentId, seed.PlayerId, default))!;
        Assert.Equal(consentFirst ? 1 : 2, stored.Snapshot!.Number);
        Assert.Equal(consentFirst ? 1 : (int?)null, stored.PublicSnapshotNumber);
        Assert.Equal(consentFirst ? 1 : 2, stored.Snapshots.Count);
    }

    private static async Task<(Guid Id, Guid[] Players)> Closed(WebApplicationFactory<Program> factory, HttpClient owner, HttpClient player)
    {
        var seed = await PostgresByeTests.Seed(factory, owner, player, false);
        using var scope = factory.Services.CreateScope(); var sp = scope.ServiceProvider;
        var drafts = sp.GetRequiredService<ITournamentDrafts>();
        var t = (await drafts.FindOwnedAsync(seed.Id, seed.Players[0], default))!;
        var round = SwissRound.Generate(t, 1, seed.Players, [], [], seed.Players, seed.Players[0], DateTimeOffset.UtcNow);
        await sp.GetRequiredService<ISwissRounds>().AddAsync(round, default);
        round.Matches[0].RecordResult(t, "Draw", "Played", "Nota privada", 0, seed.Players[0], DateTimeOffset.UtcNow);
        await sp.GetRequiredService<ITournamentMatches>().SaveAsync(default);
        t.Finish(seed.Players, round.Matches, [round.Bye!], 0, t.MatchesVersion, t.ResultsVersion, seed.Players[0], DateTimeOffset.UtcNow);
        await drafts.SaveAsync(t, default);
        return seed;
    }

    [Fact]
    public async Task AnonymousPublicationRequiresOwnerAndIndividualConsentAndDisappearsOnReopening()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var anonymous = factory.CreateClient();
        var (id, p) = await Closed(factory, owner, player);
        var path = $"/api/tournaments/{id}"; var page = $"/api/public/tournaments/{id}"; var mine = $"/api/registrations/{id}/deck/visibility";
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(page)).StatusCode);
        var command = new SetTournamentPublication(true, 0, 1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path + "/publication", command)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync(path + "/publication", command)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/publication", command)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/publication", new { })).StatusCode);
        (await owner.PostAsJsonAsync(path + "/publication", command)).EnsureSuccessStatusCode();
        var publicData = (await anonymous.GetFromJsonAsync<PublicTournament>(page))!;
        Assert.Equal(3, publicData.Standings.Count); Assert.Single(publicData.Matches); Assert.Single(publicData.Byes);
        Assert.All(publicData.Participants, participant => Assert.Null(participant.Deck));
        var raw = await anonymous.GetStringAsync(page);
        foreach (var forbidden in new[] { "Lista original", "Privado", "Nota privada", "sourceDeckId", "registrationToken", "accountId", "actorPlayerId", "reason", "@example.test" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        Assert.True((await anonymous.GetAsync(page)).Headers.CacheControl!.NoStore);
        player.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync(mine, new SetDeckVisibility(true, 1, 0))).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(player);
        (await player.PostAsJsonAsync(mine, new SetDeckVisibility(true, 1, 0))).EnsureSuccessStatusCode();
        publicData = (await anonymous.GetFromJsonAsync<PublicTournament>(page))!;
        Assert.Equal("Lista original", publicData.Participants.Single(x => x.PlayerId == p[1]).Deck!.ListText);
        Assert.Null(publicData.Participants.Single(x => x.PlayerId == p[0]).Deck);
        (await player.PostAsJsonAsync(mine, new SetDeckVisibility(false, 1, 1))).EnsureSuccessStatusCode();
        Assert.DoesNotContain("Lista original", await anonymous.GetStringAsync(page));
        Assert.Equal(HttpStatusCode.Conflict, (await player.PostAsJsonAsync(mine, new SetDeckVisibility(true, 1, 1))).StatusCode);
        (await owner.PostAsJsonAsync(path + "/publication", new SetTournamentPublication(false, 1, 1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(page)).StatusCode);
        (await owner.PostAsJsonAsync(path + "/publication", new SetTournamentPublication(true, 2, 1))).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync(path + "/reopen", new ReopenTournament(1, "Corrección privada"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(page)).StatusCode);
        var state = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        (await owner.PostAsJsonAsync(path + "/finish", new FinishTournament(state.LifecycleVersion, state.MatchesVersion, state.ResultsVersion))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(page)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        var account = db.Players.Single(x => x.Id == p[0]).AccountId;
        Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(account.ToString()))!, "TournamentOrganizer")).Succeeded);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path + "/publication", new SetTournamentPublication(true, 4, 3))).StatusCode);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PublicationAndReopeningConflictInBothCommitOrders(bool publishFirst)
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>(); using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        var (id, p) = await Closed(factory, owner, player);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var da = a.ServiceProvider.GetRequiredService<ITournamentDrafts>(); var db = b.ServiceProvider.GetRequiredService<ITournamentDrafts>();
        var ta = (await da.FindOwnedAsync(id, p[0], default))!; var tb = (await db.FindOwnedAsync(id, p[0], default))!;
        ta.SetPublication(true, 0, 1, DateTimeOffset.UtcNow); tb.Reopen("Corregir", 1, p[0], DateTimeOffset.UtcNow);
        if (publishFirst) { await da.SaveAsync(ta, default); await Assert.ThrowsAsync<TournamentConflictException>(() => db.SaveAsync(tb, default)); }
        else { await db.SaveAsync(tb, default); await Assert.ThrowsAsync<TournamentConflictException>(() => da.SaveAsync(ta, default)); }
        using var check = factory.Services.CreateScope();
        var result = await check.ServiceProvider.GetRequiredService<IPublishedTournaments>().ReadAsync(id, default);
        Assert.Equal(publishFirst, result is not null);
    }
}
