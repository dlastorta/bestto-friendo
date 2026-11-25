using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentEndpointsTests
{
    [Fact]
    public async Task DraftsRequireOrganizerRole_AndRemainPrivateWithControlledDependencies()
    {
        var accounts = new IdentityEndpointsTests.FakeAccounts();
        var games = new MemoryGames();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountAccess>(); services.RemoveAll<IPlayerProfiles>();
            services.RemoveAll<IGameCatalog>(); services.RemoveAll<ITournamentDrafts>();
            services.AddSingleton<IAccountAccess>(accounts); services.AddSingleton<IPlayerProfiles>(accounts);
            services.AddSingleton<IGameCatalog>(games); services.AddSingleton<ITournamentDrafts, MemoryDrafts>();
        }));
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/tournaments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/tournaments", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync($"/api/tournaments/{Guid.NewGuid()}")).StatusCode);
        await IdentityEndpointsTests.RegisterAndLogin(owner, "organizer@example.test", "Organizador");
        Assert.False((await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanOrganizeTournaments);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/tournaments", new { })).StatusCode);
        accounts.CatalogAdmin = true;
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/tournaments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/tournaments", new { })).StatusCode);
        accounts.CatalogAdmin = false;
        accounts.TournamentOrganizer = true;
        Assert.True((await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanOrganizeTournaments);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/games", new { })).StatusCode);
        await IdentityEndpointsTests.RegisterAndLogin(other, "other-organizer@example.test", "Otro organizador");
        var otherId = (await other.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
        var command = new { name = "  Copa local  ", gameId = games.Game.Id, organizerPlayerId = otherId, status = "Started" };
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/tournaments", command)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentDraft("Copa", Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/tournaments",
            new CreateTournamentDraft(" ", games.Game.Id))).StatusCode);
        var created = await owner.PostAsJsonAsync("/api/tournaments", command);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tournament = (await created.Content.ReadFromJsonAsync<TournamentDto>())!;
        var path = $"/api/tournaments/{tournament.Id}";
        Assert.Equal(path, created.Headers.Location!.ToString());
        Assert.Equal("Copa local", tournament.Name);
        Assert.Equal("Draft", tournament.Status);
        Assert.Equal(games.Game.Id, tournament.GameId);
        Assert.Equal(tournament, await owner.GetFromJsonAsync<TournamentDto>(path));
        Assert.Single((await owner.GetFromJsonAsync<TournamentDto[]>("/api/tournaments"))!);
        Assert.Single((await owner.GetFromJsonAsync<TournamentDto[]>($"/api/tournaments?gameId={games.Game.Id}"))!);
        Assert.Empty((await owner.GetFromJsonAsync<TournamentDto[]>($"/api/tournaments?gameId={Guid.NewGuid()}"))!);
        Assert.Empty((await other.GetFromJsonAsync<TournamentDto[]>("/api/tournaments"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/tournaments/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal("no-store", (await owner.GetAsync(path)).Headers.CacheControl!.ToString());
        accounts.TournamentOrganizer = false;
        Assert.False((await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.CanOrganizeTournaments);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/tournaments", command)).StatusCode);
    }

    private sealed class MemoryGames : IGameCatalog
    {
        public Game Game { get; } = Game.Create("Juego", ["Azul"], 1, 1);
        public Task<bool> NameExistsAsync(string name, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Game game, CancellationToken ct) => Task.CompletedTask;
        public Task<Game?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(id == Game.Id ? Game : null);
        public Task<IReadOnlyList<Game>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Game>>([Game]);
    }

    private sealed class MemoryDrafts : ITournamentDrafts
    {
        private readonly List<Tournament> tournaments = [];
        public Task AddAsync(Tournament tournament, CancellationToken ct) { tournaments.Add(tournament); return Task.CompletedTask; }
        public Task<Tournament?> FindOwnedAsync(Guid id, Guid owner, CancellationToken ct) =>
            Task.FromResult(tournaments.SingleOrDefault(item => item.Id == id && item.OrganizerPlayerId == owner));
        public Task<IReadOnlyList<Tournament>> ListOwnedAsync(Guid owner, Guid? gameId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Tournament>>(tournaments.Where(item => item.OrganizerPlayerId == owner && (!gameId.HasValue || item.GameId == gameId)).ToArray());
    }
}
