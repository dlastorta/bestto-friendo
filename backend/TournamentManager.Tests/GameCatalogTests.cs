using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Games.CreateGame;
using TournamentManager.Domain.Games;
using Xunit;

namespace TournamentManager.Tests;

public sealed class GameCatalogTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GameCatalogTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CreateAndListGames_AreAvailableWithoutUsingEfInApplicationOrEndpoints()
    {
        var catalog = new FakeCatalog();
        using var factory = WithFakeCatalog(catalog);
        using var client = factory.CreateClient();
        await IdentityTestSupport.SetCsrfAsync(client);
        var create = new CreateGameCommand("Magic", ["Blanco", "Azul", "Negro"], 1, 3);

        var post = await client.PostAsJsonAsync("/api/games", create);

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotNull(created);
        Assert.Equal("Magic", created.Name);
        Assert.Equal(3, created.Factions.Count);
        Assert.Equal($"/api/games/{created.Id}", post.Headers.Location?.ToString());

        var listed = await client.GetFromJsonAsync<GameDto[]>("/api/games");
        Assert.Single(listed!);
        Assert.Equal(created.Id, listed![0].Id);

        var fetched = await client.GetFromJsonAsync<GameDto>($"/api/games/{created.Id}");
        Assert.Equal(created.Id, fetched?.Id);
    }

    [Fact]
    public async Task CreateRejectsInvalidFactionSet_WithBadRequest()
    {
        using var factory = WithFakeCatalog(new FakeCatalog());
        using var client = factory.CreateClient();
        await IdentityTestSupport.SetCsrfAsync(client);

        var result = await client.PostAsJsonAsync("/api/games", new CreateGameCommand(
            "L5R", ["León", "león"], 1, 1));

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task CreateRejectsDuplicateGameName_WithConflict()
    {
        using var factory = WithFakeCatalog(new FakeCatalog());
        using var client = factory.CreateClient();
        await IdentityTestSupport.SetCsrfAsync(client);

        var first = await client.PostAsJsonAsync("/api/games", new CreateGameCommand(
            "Magic", ["Blanco"], 1, 1));
        var second = await client.PostAsJsonAsync("/api/games", new CreateGameCommand(
            " magic ", ["Azul"], 1, 1));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetUnknownGame_ReturnsNotFound()
    {
        using var factory = WithFakeCatalog(new FakeCatalog());
        using var client = factory.CreateClient();

        var result = await client.GetAsync($"/api/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    private WebApplicationFactory<Program> WithFakeCatalog(FakeCatalog catalog) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGameCatalog>();
            services.AddSingleton<IGameCatalog>(catalog);
            services.AddAuthentication("CatalogTest")
                .AddScheme<AuthenticationSchemeOptions, CatalogAdminTestAuthentication>("CatalogTest", _ => { });
        }));

    private sealed class FakeCatalog : IGameCatalog
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
            Task.FromResult(_games.Values.Any(game => game.NormalizedName == normalizedName));

        public Task AddAsync(Game game, CancellationToken cancellationToken)
        {
            _games.Add(game.Id, game);
            return Task.CompletedTask;
        }

        public Task<Game?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_games.GetValueOrDefault(id));

        public Task<IReadOnlyList<Game>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Game>>(_games.Values.OrderBy(game => game.Name).ToArray());
    }
}
