using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresLifecycleTests
{
    [Fact]
    public async Task HttpReplacementAndClosurePreserveHistoryAndEnforcePermissions()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var anonymous = factory.CreateClient();
        var (id, _) = await PostgresByeTests.Seed(factory, owner, player, false);
        var path = $"/api/tournaments/{id}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + "/lifecycle")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync(path + "/rounds/history")).StatusCode);
        var response = await owner.PostAsJsonAsync(path + "/rounds", new GenerateSwissRound(1)); response.EnsureSuccessStatusCode();
        var original = (await response.Content.ReadFromJsonAsync<SwissRoundDto>())!;
        var state = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        var regenerate = new RegenerateSwissRound(1, state.MatchesVersion, state.ResultsVersion, state.LifecycleVersion, "Nuevo sorteo");
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/rounds/regenerate", regenerate)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        response = await owner.PostAsJsonAsync(path + "/rounds/regenerate", regenerate); response.EnsureSuccessStatusCode();
        var replacement = (await response.Content.ReadFromJsonAsync<SwissRoundDto>())!;
        Assert.Equal(2, replacement.Revision);
        Assert.NotEqual(original.Matches[0].MatchId, replacement.Matches[0].MatchId);
        var archived = Assert.Single((await owner.GetFromJsonAsync<RoundHistoryDto[]>(path + "/rounds/history"))!);
        Assert.Equal(original.Matches[0].MatchId, Assert.Single(archived.Matches).Id);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/rounds/regenerate", regenerate)).StatusCode);
        var matchPath = path + $"/matches/{replacement.Matches[0].MatchId}/result";
        (await owner.PostAsJsonAsync(matchPath, new RecordMatchResult("Draw", "Played", null, 0))).EnsureSuccessStatusCode();
        state = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        var finish = new FinishTournament(state.LifecycleVersion, state.MatchesVersion, state.ResultsVersion);
        (await owner.PostAsJsonAsync(path + "/finish", finish)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(matchPath, new RecordMatchResult("PlayerOneWin", "Played", "Error", 1))).StatusCode);
        (await owner.PostAsJsonAsync(path + "/reopen", new ReopenTournament(1, "Corregir resultado"))).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync(matchPath, new RecordMatchResult("PlayerOneWin", "Played", "Corrección", 1))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/rounds", new GenerateSwissRound(2))).StatusCode);
        state = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        (await owner.PostAsJsonAsync(path + "/finish", new FinishTournament(state.LifecycleVersion, state.MatchesVersion, state.ResultsVersion))).EnsureSuccessStatusCode();
        var events = (await owner.GetFromJsonAsync<LifecycleDto[]>(path + "/lifecycle"))!;
        Assert.Equal(3, events.Length); Assert.Equal(new[] { 1 }, events[0].ResultNumbers); Assert.Equal(new[] { 2 }, events[2].ResultNumbers);
        Assert.True((await owner.GetAsync(path + "/lifecycle")).Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ClosureAndRegenerationExcludeEachOtherAtomically(bool closeFirst)
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
        var sa = a.ServiceProvider; var sb = b.ServiceProvider;
        var ta = (await sa.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        var tb = (await sb.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        var ma = (await sa.GetRequiredService<ITournamentMatches>().ListAsync(id, default)).ToArray();
        var mb = (await sb.GetRequiredService<ITournamentMatches>().ListAsync(id, default)).ToArray();
        var ba = (await sa.GetRequiredService<ITournamentByes>().ListAsync(id, default)).ToArray();
        var bb = (await sb.GetRequiredService<ITournamentByes>().ListAsync(id, default)).ToArray();
        ta.Finish(p, ma, ba, 0, ta.MatchesVersion, ta.ResultsVersion, p[0], DateTimeOffset.UtcNow);
        var regenerated = SwissRound.Regenerate(tb, 1, tb.MatchesVersion, tb.ResultsVersion, 0, "Rehacer", p, mb, bb,
            (await sb.GetRequiredService<ISwissRounds>().ListAllAsync(id, default)).ToArray(), p, p[0], DateTimeOffset.UtcNow);
        Func<Task> close = () => sa.GetRequiredService<ITournamentDrafts>().SaveAsync(ta, default);
        Func<Task> replace = () => sb.GetRequiredService<ISwissRounds>().AddAsync(regenerated, default);
        if (closeFirst) { await close(); await Assert.ThrowsAsync<TournamentConflictException>(replace); }
        else { await replace(); await Assert.ThrowsAsync<TournamentConflictException>(close); }
        using var check = factory.Services.CreateScope();
        var rounds = check.ServiceProvider.GetRequiredService<ISwissRounds>();
        Assert.Equal(closeFirst ? 1 : 2, (await rounds.ListAllAsync(id, default)).Count);
        Assert.Equal(closeFirst ? 1 : 2, Assert.Single(await rounds.ListAsync(id, default)).Revision);
        var stored = (await check.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(id, p[0], default))!;
        Assert.Equal(closeFirst ? TournamentStatus.Finished : TournamentStatus.InProgress, stored.Status);
        Assert.Equal(closeFirst ? 1 : 0, stored.Lifecycle.Count);
    }
}
