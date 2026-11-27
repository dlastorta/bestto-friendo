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

public sealed class PostgresMatchTests
{
    private static async Task Start(HttpClient owner, Guid id)
    {
        var tournament = (await owner.GetFromJsonAsync<TournamentDto>($"/api/tournaments/{id}"))!;
        (await owner.PostAsJsonAsync($"/api/tournaments/{id}/start",
            new StartTournament(tournament.RulesVersion, tournament.ParticipationVersion))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HttpMatchesEnforcePermissionsAndKeepAuditedCorrectionsWithHistoricalPoints()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var anonymous = factory.CreateClient();
        var seed = await PostgresTournamentStartTests.Seed(factory, owner, player);
        var path = $"/api/tournaments/{seed.TournamentId}/matches";
        var create = new CreateMatch(1, seed.OwnerId, seed.PlayerId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync(path, create)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, create)).StatusCode);
        await Start(owner, seed.TournamentId);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, create)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, create with { PlayerTwoId = Guid.NewGuid() })).StatusCode);
        var response = await owner.PostAsJsonAsync(path, create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var match = (await response.Content.ReadFromJsonAsync<MatchDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, create with { PlayerOneId = seed.PlayerId, PlayerTwoId = seed.OwnerId })).StatusCode);
        var resultPath = $"{path}/{match.Id}/result";
        var result = new RecordMatchResult("PlayerOneWin", "Played", null, 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(resultPath, result with { ExpectedVersion = null })).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(resultPath, result)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        (await owner.PostAsJsonAsync(resultPath, result)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(resultPath, result)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(resultPath, result with { ExpectedVersion = 1, Reason = " " })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var tournament = (await scope.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            db.Entry(tournament.Rules!).Property(r => r.WinPoints).CurrentValue = 999;
            await db.SaveChangesAsync();
        }
        var correctedResponse = await owner.PostAsJsonAsync(resultPath, new RecordMatchResult("PlayerTwoWin", "Forfeit", "Ganador invertido", 1));
        correctedResponse.EnsureSuccessStatusCode();
        var corrected = (await correctedResponse.Content.ReadFromJsonAsync<MatchDto>())!;
        Assert.Equal(0, corrected.Result!.PlayerOnePoints); Assert.Equal(3, corrected.Result.PlayerTwoPoints);
        Assert.Equal(2, corrected.History!.Count); Assert.Equal(3, corrected.History[1].PlayerOnePoints);
        Assert.All(corrected.History, r => Assert.Equal(seed.OwnerId, r.ActorPlayerId));
        var listed = Assert.Single((await owner.GetFromJsonAsync<MatchDto[]>(path))!);
        Assert.Equal(2, listed.Version); Assert.Null(listed.History);
        var json = await owner.GetStringAsync($"{path}/{match.Id}");
        Assert.DoesNotContain("Lista original", json); Assert.DoesNotContain("sourceDeckId", json);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync($"{path}/{match.Id}")).StatusCode);
        // Otro organizador tampoco puede leer ni corregir; revocación se aplica a la cookie existente.
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            var playerAccount = db.Players.Single(p => p.Id == seed.PlayerId).AccountId;
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(playerAccount.ToString()))!, "TournamentOrganizer")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await player.PostAsJsonAsync(resultPath, result)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var account = db.Players.Single(p => p.Id == seed.OwnerId).AccountId;
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByIdAsync(account.ToString()))!, "TournamentOrganizer")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConcurrentCreationOrCorrectionCommitsOnlyOneOperation(bool correction)
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        var seed = await PostgresTournamentStartTests.Seed(factory, owner, player);
        await Start(owner, seed.TournamentId);
        var path = $"/api/tournaments/{seed.TournamentId}/matches";
        MatchDto? initial = null;
        if (correction)
        {
            var response = await owner.PostAsJsonAsync(path, new CreateMatch(1, seed.OwnerId, seed.PlayerId));
            response.EnsureSuccessStatusCode(); initial = (await response.Content.ReadFromJsonAsync<MatchDto>())!;
            (await owner.PostAsJsonAsync($"{path}/{initial.Id}/result", new RecordMatchResult("PlayerOneWin", "Played", null, 0))).EnsureSuccessStatusCode();
        }
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var ta = (await a.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
        var tb = (await b.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
        var ma = a.ServiceProvider.GetRequiredService<ITournamentMatches>(); var mb = b.ServiceProvider.GetRequiredService<ITournamentMatches>();
        if (correction)
        {
            var first = (await ma.FindAsync(seed.TournamentId, initial!.Id, default))!;
            var second = (await mb.FindAsync(seed.TournamentId, initial.Id, default))!;
            first.RecordResult(ta, "Draw", "Played", "Corrección A", 1, seed.OwnerId, DateTimeOffset.UtcNow);
            second.RecordResult(tb, "PlayerTwoWin", "Played", "Corrección B", 1, seed.OwnerId, DateTimeOffset.UtcNow);
            await ma.SaveAsync(default);
            await Assert.ThrowsAsync<TournamentConflictException>(() => mb.SaveAsync(default));
            var persisted = (await owner.GetFromJsonAsync<MatchDto>($"{path}/{initial.Id}"))!;
            Assert.Equal(2, persisted.History!.Count); Assert.Equal("Draw", persisted.Result!.Outcome);
            Assert.Equal("PlayerOneWin", persisted.History[1].Outcome);
        }
        else
        {
            var first = TournamentMatch.Create(ta, 1, seed.OwnerId, seed.PlayerId, [seed.OwnerId, seed.PlayerId], [], seed.OwnerId, DateTimeOffset.UtcNow, []);
            var second = TournamentMatch.Create(tb, 1, seed.PlayerId, seed.OwnerId, [seed.OwnerId, seed.PlayerId], [], seed.OwnerId, DateTimeOffset.UtcNow, []);
            await ma.AddAsync(first, default);
            await Assert.ThrowsAsync<TournamentConflictException>(() => mb.AddAsync(second, default));
            Assert.Single((await owner.GetFromJsonAsync<MatchDto[]>(path))!);
        }
    }
}
