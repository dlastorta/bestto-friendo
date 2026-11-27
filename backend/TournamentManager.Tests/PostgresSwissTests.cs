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

public sealed class PostgresSwissTests
{
    [Fact]
    public async Task HttpRoundsEnforceOwnerCsrfCompletionAndHistoricalPolicyWithoutLeakingDecks()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var anon = factory.CreateClient();
        var (id, p) = await PostgresByeTests.Seed(factory, owner, player, false);
        var path = $"/api/tournaments/{id}/rounds";
        var command = new GenerateSwissRound(1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync(path, command)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, command)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        var response = await owner.PostAsJsonAsync(path, command); response.EnsureSuccessStatusCode();
        var first = (await response.Content.ReadFromJsonAsync<SwissRoundDto>())!;
        Assert.Equal(p.Order(), first.ParticipantOrder.Order()); Assert.Equal(p[0], first.ActorPlayerId);
        Assert.Equal(1, first.AlgorithmVersion); Assert.Single(first.Matches); Assert.NotNull(first.ByePlayerId);
        Assert.Equal(first.ParticipantOrder[0], first.ByePlayerId);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, command)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, new GenerateSwissRound(2))).StatusCode);
        var matchId = first.Matches[0].MatchId;
        (await owner.PostAsJsonAsync($"/api/tournaments/{id}/matches/{matchId}/result", new RecordMatchResult("Draw", "Played", null, 0))).EnsureSuccessStatusCode();
        var secondResponse = await owner.PostAsJsonAsync(path, new GenerateSwissRound(2)); secondResponse.EnsureSuccessStatusCode();
        var second = (await secondResponse.Content.ReadFromJsonAsync<SwissRoundDto>())!;
        Assert.NotEqual(first.ByePlayerId, second.ByePlayerId);
        var before = await owner.GetStringAsync(path);
        Assert.DoesNotContain("Privado", before); Assert.DoesNotContain("deck", before, StringComparison.OrdinalIgnoreCase);
        (await owner.PostAsJsonAsync($"/api/tournaments/{id}/matches/{matchId}/result", new RecordMatchResult("PlayerOneWin", "Forfeit", "Corrección posterior", 1))).EnsureSuccessStatusCode();
        var after = await owner.GetStringAsync(path);
        using var beforeJson = System.Text.Json.JsonDocument.Parse(before);
        using var afterJson = System.Text.Json.JsonDocument.Parse(after);
        Assert.Equal(beforeJson.RootElement.GetProperty("rounds").GetRawText(), afterJson.RootElement.GetProperty("rounds").GetRawText());
        Assert.Equal(beforeJson.RootElement.GetProperty("resultsVersion").GetInt32() + 1,
            afterJson.RootElement.GetProperty("resultsVersion").GetInt32());
        Assert.True((await owner.GetAsync(path)).Headers.CacheControl!.NoStore);
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

        using var legacyOwner = factory.CreateClient(); using var legacyPlayer = factory.CreateClient();
        var legacy = await PostgresByeTests.Seed(factory, legacyOwner, legacyPlayer);
        var legacyPath = $"/api/tournaments/{legacy.Id}/rounds";
        Assert.False((await legacyOwner.GetFromJsonAsync<SwissRoundsDto>(legacyPath))!.Enabled);
        Assert.Equal(HttpStatusCode.Conflict, (await legacyOwner.PostAsJsonAsync(legacyPath, command)).StatusCode);
    }

    [Theory]
    [InlineData("generation", true)] [InlineData("generation", false)]
    [InlineData("match", true)] [InlineData("match", false)]
    [InlineData("bye", true)] [InlineData("bye", false)]
    [InlineData("correction", true)] [InlineData("correction", false)]
    public async Task WholeRoundConflictsAtomicallyWithGenerationManualAssignmentsAndCorrections(string operation, bool generationFirst)
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        var (id, p) = await PostgresByeTests.Seed(factory, owner, player, false);
        using (var seed = factory.Services.CreateScope())
        {
            var sp = seed.ServiceProvider;
            var t = (await sp.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
            var first = SwissRound.Generate(t, 1, p, [], [], p, p[0], DateTimeOffset.UtcNow);
            await sp.GetRequiredService<ISwissRounds>().AddAsync(first, default);
            first.Matches[0].RecordResult(t, "Draw", "Played", null, 0, p[0], DateTimeOffset.UtcNow);
            await sp.GetRequiredService<ITournamentMatches>().SaveAsync(default);
        }
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var ta = (await a.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        var tb = (await b.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        var ma = (await a.ServiceProvider.GetRequiredService<ITournamentMatches>().ListAsync(id, default)).ToArray();
        var mb = (await b.ServiceProvider.GetRequiredService<ITournamentMatches>().ListAsync(id, default)).ToArray();
        var ba = (await a.ServiceProvider.GetRequiredService<ITournamentByes>().ListAsync(id, default)).ToArray();
        var bb = (await b.ServiceProvider.GetRequiredService<ITournamentByes>().ListAsync(id, default)).ToArray();
        var generated = SwissRound.Generate(ta, 2, p, ma, ba, [], p[0], DateTimeOffset.UtcNow);
        Func<Task> other;
        if (operation == "generation")
        {
            var second = SwissRound.Generate(tb, 2, p, mb, bb, [], p[0], DateTimeOffset.UtcNow);
            other = () => b.ServiceProvider.GetRequiredService<ISwissRounds>().AddAsync(second, default);
        }
        else if (operation == "match")
        {
            var manual = TournamentMatch.Create(tb, 2, p[0], p[1], p, mb, p[0], DateTimeOffset.UtcNow, bb);
            other = () => b.ServiceProvider.GetRequiredService<ITournamentMatches>().AddAsync(manual, default);
        }
        else if (operation == "bye")
        {
            var manual = TournamentBye.Assign(tb, 2, p[1], p, mb, bb, p[0], DateTimeOffset.UtcNow);
            other = () => b.ServiceProvider.GetRequiredService<ITournamentByes>().AddAsync(manual, default);
        }
        else
        {
            mb[0].RecordResult(tb, "PlayerOneWin", "Played", "Corrección concurrente", 1, p[0], DateTimeOffset.UtcNow);
            other = () => b.ServiceProvider.GetRequiredService<ITournamentMatches>().SaveAsync(default);
        }
        Func<Task> commit = () => a.ServiceProvider.GetRequiredService<ISwissRounds>().AddAsync(generated, default);
        if (generationFirst) { await commit(); await Assert.ThrowsAsync<TournamentConflictException>(other); }
        else { await other(); await Assert.ThrowsAsync<TournamentConflictException>(commit); }
        using var check = factory.Services.CreateScope();
        var cs = check.ServiceProvider;
        var games = await cs.GetRequiredService<ITournamentMatches>().ListAsync(id, default);
        var byes = await cs.GetRequiredService<ITournamentByes>().ListAsync(id, default);
        var rounds = await cs.GetRequiredService<ISwissRounds>().ListAsync(id, default);
        var roundCommitted = generationFirst || operation == "generation";
        Assert.Equal(roundCommitted ? 2 : 1, rounds.Count);
        Assert.Equal(roundCommitted || operation == "match" ? 2 : 1, games.Count);
        Assert.Equal(roundCommitted || operation == "bye" ? 2 : 1, byes.Count);
        Assert.Equal(!generationFirst && operation == "correction" ? 2 : 1, games.Single(m => m.Round == 1).Results.Count);
    }
}
