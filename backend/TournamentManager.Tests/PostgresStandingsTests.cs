using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Infrastructure.Persistence;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresStandingsTests
{
    [Fact]
    public async Task ParticipantsAndOwnerSeeRebuiltPrivateTableButOutsidersCannot()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        using var stranger = factory.CreateClient(); using var anonymous = factory.CreateClient();
        var seed = await PostgresTournamentStartTests.Seed(factory, owner, player);
        await IdentityEndpointsTests.RegisterAndLogin(stranger, $"table-{Guid.NewGuid():N}@example.test", "Ajeno");
        var path = $"/api/tournaments/{seed.TournamentId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + "/standings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(path + "/standings")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await player.GetAsync(path + "/standings")).StatusCode);
        var before = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        (await owner.PostAsJsonAsync(path + "/start", new StartTournament(before.RulesVersion, before.ParticipationVersion))).EnsureSuccessStatusCode();
        Assert.All((await player.GetFromJsonAsync<StandingDto[]>(path + "/standings"))!, r => { Assert.Equal(0, r.Points); Assert.Equal(1, r.Position); });
        var created = await owner.PostAsJsonAsync(path + "/matches", new CreateMatch(1, seed.OwnerId, seed.PlayerId));
        created.EnsureSuccessStatusCode();
        var match = (await created.Content.ReadFromJsonAsync<MatchDto>())!;
        var resultPath = $"{path}/matches/{match.Id}/result";
        (await owner.PostAsJsonAsync(resultPath, new RecordMatchResult("PlayerOneWin", "Forfeit", null, 0))).EnsureSuccessStatusCode();
        var first = (await player.GetFromJsonAsync<StandingDto[]>(path + "/standings"))!;
        Assert.Equal(3, first.Single(r => r.PlayerId == seed.OwnerId).Points);
        using (var scope = factory.Services.CreateScope())
        {
            var t = (await scope.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            db.Entry(t.Rules!).Property(r => r.DrawPoints).CurrentValue = 999;
            await db.SaveChangesAsync();
        }
        (await owner.PostAsJsonAsync(resultPath, new RecordMatchResult("Draw", "Played", "Corrección", 1))).EnsureSuccessStatusCode();
        var shared = (await player.GetFromJsonAsync<StandingDto[]>(path + "/standings"))!;
        Assert.All(shared, r => { Assert.Equal(1, r.Points); Assert.Equal(1, r.Position); });
        Assert.Equal(shared, (await owner.GetFromJsonAsync<StandingDto[]>(path + "/standings"))!);
        var response = await player.GetAsync(path + "/standings");
        Assert.True(response.Headers.CacheControl!.NoStore);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Lista original", json); Assert.DoesNotContain("sourceDeckId", json);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync(path + "/matches")).StatusCode);
    }
}
