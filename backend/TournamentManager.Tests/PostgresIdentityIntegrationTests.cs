using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Identity;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresIdentityIntegrationTests
{
    [Fact]
    public async Task AccountsPersist_EnforceUniqueEmail_LockoutAndCatalogRole()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;

        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var email = $"player-{Guid.NewGuid():N}@example.test";
        await IdentityEndpointsTests.RegisterAndLogin(client, email, "Jugador");
        Assert.Equal("Jugador", (await client.GetFromJsonAsync<PlayerProfile>("/api/me"))!.DisplayName);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/games", new { })).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.NotNull(user.PasswordHash);
            Assert.NotEqual("a long password", user.PasswordHash);
            Assert.True((await users.AddToRoleAsync(user, "CatalogAdmin")).Succeeded);
        }

        Assert.True((await client.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanManageCatalog);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/games", new
        {
            name = $"Admin-{Guid.NewGuid():N}", factions = new[] { "Azul" }, minimumFactions = 1, maximumFactions = 1
        })).StatusCode);
        using var other = factory.CreateClient();
        await IdentityTestSupport.SetCsrfAsync(other);
        Assert.Equal(HttpStatusCode.Conflict, (await other.PostAsJsonAsync("/api/auth/register",
            new RegisterAccount(email.ToUpperInvariant(), "a long password", "Duplicado"))).StatusCode);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await other.PostAsJsonAsync("/api/auth/login",
                new LoginAccount(email, "wrong password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.PostAsJsonAsync("/api/auth/login",
            new LoginAccount(email, "a long password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }
}
