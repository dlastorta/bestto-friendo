using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Games;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresTournamentIntegrationTests
{
    [Fact]
    public async Task DraftsPersist_KeepOwnersIsolated_AndRoleChangesApplyToExistingSessions()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        var email = $"organizer-{Guid.NewGuid():N}@example.test";
        var otherEmail = $"organizer-{Guid.NewGuid():N}@example.test";
        await IdentityEndpointsTests.RegisterAndLogin(owner, email, "Organizador");
        await IdentityEndpointsTests.RegisterAndLogin(other, otherEmail, "Otro organizador");
        Assert.False((await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanOrganizeTournaments);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/tournaments")).StatusCode);
        var game = Game.Create($"Tournament-{Guid.NewGuid():N}", ["Azul"], 1, 1);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            foreach (var address in new[] { email, otherEmail })
                Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(address))!, "TournamentOrganizer")).Succeeded);
            await scope.ServiceProvider.GetRequiredService<IGameCatalog>().AddAsync(game, default);
        }
        Assert.True((await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanOrganizeTournaments);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/games", new { })).StatusCode);
        var command = new CreateTournamentDraft("Copa repetible", game.Id);
        var created = await owner.PostAsJsonAsync("/api/tournaments", command);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = (await created.Content.ReadFromJsonAsync<TournamentDto>())!;
        var second = await owner.PostAsJsonAsync("/api/tournaments", command);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(first.Id, (await second.Content.ReadFromJsonAsync<TournamentDto>())!.Id);
        Assert.Equal(first, await owner.GetFromJsonAsync<TournamentDto>($"/api/tournaments/{first.Id}"));
        Assert.Equal("Draft", first.Status);
        Assert.Equal(2, (await owner.GetFromJsonAsync<TournamentDto[]>($"/api/tournaments?gameId={game.Id}"))!.Length);
        Assert.Empty((await owner.GetFromJsonAsync<TournamentDto[]>($"/api/tournaments?gameId={Guid.NewGuid()}"))!);
        Assert.Empty((await other.GetFromJsonAsync<TournamentDto[]>($"/api/tournaments?gameId={game.Id}"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/tournaments/{first.Id}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            Assert.True((await users.RemoveFromRoleAsync((await users.FindByEmailAsync(email))!, "TournamentOrganizer")).Succeeded);
        }
        Assert.False((await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanOrganizeTournaments);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/tournaments/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/tournaments", command)).StatusCode);
    }
}
