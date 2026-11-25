using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using Xunit;

namespace TournamentManager.Tests;

public sealed class DeckEndpointsTests
{
    [Fact]
    public async Task LibraryIsPrivate_IncludingForCatalogAdmins_AndWritesRequireCsrf()
    {
        var accounts = new IdentityEndpointsTests.FakeAccounts { CatalogAdmin = true };
        var catalog = new MemoryGames();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountAccess>(); services.RemoveAll<IPlayerProfiles>();
            services.RemoveAll<IGameCatalog>(); services.RemoveAll<IDeckLibrary>();
            services.AddSingleton<IAccountAccess>(accounts); services.AddSingleton<IPlayerProfiles>(accounts);
            services.AddSingleton<IGameCatalog>(catalog); services.AddSingleton<IDeckLibrary, MemoryDecks>();
        }));
        using var owner = factory.CreateClient();
        using var stranger = factory.CreateClient();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/decks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/decks", new { })).StatusCode);
        await IdentityEndpointsTests.RegisterAndLogin(owner, "owner@example.test", "Dueño");
        await IdentityEndpointsTests.RegisterAndLogin(stranger, "other@example.test", "Admin ajeno");
        var other = await stranger.GetFromJsonAsync<PlayerProfile>("/api/me");
        var body = new { gameId = catalog.Game.Id, name = "Mi mazo", listText = "  texto libre\n4 x Carta  ",
            factionIds = catalog.Game.Factions.Select(f => f.Id).ToArray(), ownerPlayerId = other!.Id };
        var created = await owner.PostAsJsonAsync("/api/decks", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var deck = (await created.Content.ReadFromJsonAsync<DeckDto>())!;
        var path = $"/api/decks/{deck.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/decks",
            new CreateDeck(Guid.NewGuid(), "Mazo", "", body.factionIds))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path,
            new EditDeck("Mazo", "", [Guid.NewGuid()], 1))).StatusCode);
        Assert.Single((await owner.GetFromJsonAsync<DeckSummary[]>("/api/decks"))!);
        Assert.Empty((await stranger.GetFromJsonAsync<DeckSummary[]>("/api/decks"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(path + "/revisions")).StatusCode);
        var edit = new EditDeck("Editado", "cualquier texto", body.factionIds, 1);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync(path, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync(path + "/archive", new ArchiveDeck(1))).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/decks", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/archive", new ArchiveDeck(1))).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(path, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(path, edit)).StatusCode);
        var history = (await owner.GetFromJsonAsync<DeckRevisionDto[]>(path + "/revisions"))!;
        Assert.Equal(2, history.Length);
        Assert.Equal(body.listText, history.Single(r => r.Number == 1).ListText);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(path + "/archive", new ArchiveDeck(2))).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<DeckSummary[]>("/api/decks"))!);
        Assert.Single((await owner.GetFromJsonAsync<DeckSummary[]>("/api/decks?archived=true"))!);
        Assert.Equal("no-store", (await owner.GetAsync(path)).Headers.CacheControl!.ToString());
    }

    private sealed class MemoryGames : IGameCatalog
    {
        public Task<bool> NameExistsAsync(string name, CancellationToken ct) => Task.FromResult(false);
        public Game Game { get; } = Game.Create("Juego", ["Azul"], 1, 1);
        public Task<Game?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(id == Game.Id ? Game : null);
        public Task AddAsync(Game game, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Game>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Game>>([Game]);
    }

    private sealed class MemoryDecks : IDeckLibrary
    {
        private readonly List<Deck> decks = [];
        public Task AddAsync(Deck deck, CancellationToken ct) { decks.Add(deck); return Task.CompletedTask; }
        public Task SaveAsync(Deck deck, CancellationToken ct) => Task.CompletedTask;
        public Task<Deck?> FindOwnedAsync(Guid id, Guid owner, CancellationToken ct) =>
            Task.FromResult(decks.SingleOrDefault(d => d.Id == id && d.OwnerPlayerId == owner));
        public Task<IReadOnlyList<DeckSummary>> ListAsync(Guid owner, Guid? game, bool archived, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DeckSummary>>(decks.Where(d => d.OwnerPlayerId == owner && d.IsArchived == archived && (game == null || d.GameId == game))
                .Select(d => new DeckSummary(d.Id, d.GameId, d.CurrentRevision.Name, d.CurrentRevisionNumber, d.Version, d.IsArchived)).ToArray());
    }
}
