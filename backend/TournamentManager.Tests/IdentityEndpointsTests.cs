using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Players;
using Xunit;

namespace TournamentManager.Tests;

public sealed class IdentityEndpointsTests
{
    private static WebApplicationFactory<Program> CreateFactory(FakeAccounts accounts) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountAccess>();
            services.RemoveAll<IPlayerProfiles>();
            services.AddSingleton<IAccountAccess>(accounts);
            services.AddSingleton<IPlayerProfiles>(accounts);
        }));

    [Fact]
    public async Task AnonymousCannotReadProfileOrCreateGames()
    {
        using var factory = CreateFactory(new());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/games", new { })).StatusCode);
    }

    [Fact]
    public async Task RegistrationAndLoginRequireCsrf_AndInvalidPasswordDoesNotCreateSession()
    {
        using var factory = CreateFactory(new());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register",
            new RegisterAccount("ana@example.test", "a long password", "Ana"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginAccount("ana@example.test", "a long password"))).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginAccount("ana@example.test", "wrong password"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task CookiesKeepProfilesIsolated_AndLogoutEndsSession()
    {
        var accounts = new FakeAccounts();
        using var factory = CreateFactory(accounts);
        using var ana = factory.CreateClient();
        using var bob = factory.CreateClient();
        await RegisterAndLogin(ana, "ana@example.test", "Ana");
        await RegisterAndLogin(bob, "bob@example.test", "Bob");
        var anaProfile = await ana.GetFromJsonAsync<PlayerProfile>("/api/me");
        var bobProfile = await bob.GetFromJsonAsync<PlayerProfile>("/api/me");
        Assert.NotEqual(anaProfile!.Id, bobProfile!.Id);
        Assert.False(anaProfile.CanManageCatalog);

        // Un ID ajeno enviado por el cliente no determina qué perfil se modifica.
        var update = await ana.PutAsJsonAsync("/api/me", new { displayName = "Ana Nueva", id = bobProfile.Id });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Bob", (await bob.GetFromJsonAsync<PlayerProfile>("/api/me"))!.DisplayName);
        Assert.Equal("Ana Nueva", (await ana.GetFromJsonAsync<PlayerProfile>("/api/me"))!.DisplayName);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsJsonAsync("/api/games", new { })).StatusCode);

        ana.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PutAsJsonAsync("/api/me", new RenamePlayer("Otro"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(ana);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ana.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task CatalogRoleIsRevalidatedForExistingCookie()
    {
        var accounts = new FakeAccounts();
        using var factory = CreateFactory(accounts);
        using var client = factory.CreateClient();
        await RegisterAndLogin(client, "admin@example.test", "Admin");
        Assert.False((await client.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanManageCatalog);
        accounts.CatalogAdmin = true;
        Assert.True((await client.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanManageCatalog);
        accounts.CatalogAdmin = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/games", new { })).StatusCode);
    }

    internal static async Task RegisterAndLogin(HttpClient client, string email, string name)
    {
        await IdentityTestSupport.SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register",
            new RegisterAccount(email, "a long password", name))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginAccount(email, "a long password"));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        await IdentityTestSupport.SetCsrfAsync(client);
    }

    private sealed class FakeAccounts : IAccountAccess, IPlayerProfiles
    {
        private readonly Dictionary<string, Player> players = new(StringComparer.OrdinalIgnoreCase);
        public bool CatalogAdmin { get; set; }
        public Task<AccountResult> RegisterAsync(string email, string password, Player player, CancellationToken ct)
        {
            if (!players.TryAdd(email, player))
                return Task.FromResult(new AccountResult(null, new Dictionary<string, string[]>(), true));
            return Task.FromResult(new AccountResult(player.AccountId, new Dictionary<string, string[]>()));
        }
        public Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct) =>
            Task.FromResult<AuthenticatedAccount?>(players.TryGetValue(email, out var player) && password == "a long password"
                ? new(player.AccountId, CatalogAdmin) : null);
        public Task<AuthenticatedAccount?> FindAccountAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult<AuthenticatedAccount?>(players.Values.Any(player => player.AccountId == accountId)
                ? new(accountId, CatalogAdmin) : null);
        public Task<Player?> FindByAccountAsync(Guid accountId, CancellationToken ct) =>
            Task.FromResult(players.Values.SingleOrDefault(player => player.AccountId == accountId));
        public Task SaveAsync(Player player, CancellationToken ct) => Task.CompletedTask;
    }
}
