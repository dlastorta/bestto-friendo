using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresTournamentStartTests
{
    private static async Task<(Guid TournamentId, Guid OwnerId, Guid PlayerId, Guid DeckId)> Seed(
        WebApplicationFactory<Program> factory, HttpClient owner, HttpClient player)
    {
        var email = $"start-owner-{Guid.NewGuid():N}@example.test";
        await IdentityEndpointsTests.RegisterAndLogin(owner, email, "Organizador");
        await IdentityEndpointsTests.RegisterAndLogin(player, $"start-player-{Guid.NewGuid():N}@example.test", "Participante");
        var ownerId = (await owner.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
        var playerId = (await player.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, "TournamentOrganizer")).Succeeded);
        var game = Game.Create($"Start-{Guid.NewGuid():N}", ["Azul"], 1, 1);
        await services.GetRequiredService<IGameCatalog>().AddAsync(game, default);
        var tournament = Tournament.CreateDraft(ownerId, game.Id, "Inicio verificable");
        tournament.ConfigureRules(3, 1, 0, true, 3, ["Buchholz", "OMWPercentage"], 0);
        tournament.EnableRegistrationLink();
        await services.GetRequiredService<ITournamentDrafts>().AddAsync(tournament, default);
        Guid playerDeckId = default;
        foreach (var id in new[] { ownerId, playerId })
        {
            var deck = Deck.Create(id, game, "Mazo inicial", "Lista original", game.Factions.Select(f => f.Id));
            await services.GetRequiredService<IDeckLibrary>().AddAsync(deck, default);
            var registration = TournamentRegistration.Create(tournament, id, DateTimeOffset.UtcNow);
            registration.Present(tournament, deck, 1, DateTimeOffset.UtcNow);
            await services.GetRequiredService<ITournamentRegistrations>().AddAsync(registration, default);
            if (id == playerId) playerDeckId = deck.Id;
        }
        return (tournament.Id, ownerId, playerId, playerDeckId);
    }

    [Fact]
    public async Task HttpStartAndReplacementPreservePrivateHistoryAndFrozenRules()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var anonymous = factory.CreateClient();
        var seed = await Seed(factory, owner, player);
        var path = $"/api/tournaments/{seed.TournamentId}";
        var mine = $"/api/registrations/{seed.TournamentId}";
        var before = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        var start = new StartTournament(before.RulesVersion, before.ParticipationVersion);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path + "/start", start)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync(path + "/start", start)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/start", start)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path + "/start", new { })).StatusCode);
        var replace = new ReplacePresentedDeck(seed.DeckId, 1, 1, "Confirmo mi selección");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(mine + "/deck/replace", replace)).StatusCode);
        player.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync(mine + "/deck/replace", replace)).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(player);
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync(mine + "/deck/replace", replace with { Reason = " " })).StatusCode);
        (await player.PostAsJsonAsync(mine + "/deck/replace", replace)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await player.PostAsJsonAsync(mine + "/deck/replace", replace)).StatusCode);
        var history = (await player.GetFromJsonAsync<PresentedDeck[]>(mine + "/deck/history"))!;
        Assert.Equal(new[] { 2, 1 }, history.Select(s => s.Number));
        Assert.All(history, s => Assert.Equal(seed.PlayerId, s.ActorPlayerId));
        Assert.Equal("Confirmo mi selección", history[0].Reason);
        Assert.Single((await owner.GetFromJsonAsync<PresentedDeck[]>(mine + "/deck/history"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/start", start)).StatusCode);
        before = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        var started = await owner.PostAsJsonAsync(path + "/start", new StartTournament(before.RulesVersion, before.ParticipationVersion));
        started.EnsureSuccessStatusCode();
        var result = (await started.Content.ReadFromJsonAsync<TournamentDto>())!;
        Assert.Equal("InProgress", result.Status);
        Assert.NotNull(result.StartedAt);
        Assert.Equal(3, result.StartedRules!.WinPoints);
        Assert.Equal(new[] { "Buchholz", "OMWPercentage" }, result.StartedRules.TieBreakers);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + "/start", start)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(path + "/rules", new ConfigureTournamentRules(9, 1, 0, true, 3, [], 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await player.PostAsJsonAsync(mine + "/deck/replace", replace with { ExpectedPresentationVersion = 2 })).StatusCode);
        Assert.Equal("InProgress", (await player.GetFromJsonAsync<RegistrationDetail>(mine))!.Status);
        Assert.Equal(2, (await player.GetFromJsonAsync<PresentedDeck[]>(mine + "/deck/history"))!.Length);
        // Simula un cambio futuro de la configuración fuente fuera del flujo bloqueado:
        // la copia histórica debe continuar siendo autónoma.
        using (var scope = factory.Services.CreateScope())
        {
            var tournament = (await scope.ServiceProvider.GetRequiredService<ITournamentDrafts>().FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            db.Entry(tournament.Rules!).Property(r => r.WinPoints).CurrentValue = 99;
            await db.SaveChangesAsync();
        }
        var persisted = (await owner.GetFromJsonAsync<TournamentDto>(path))!;
        Assert.Equal(99, persisted.Rules!.WinPoints);
        Assert.Equal(3, persisted.StartedRules!.WinPoints);
    }

    [Theory]
    [InlineData("rules", true)] [InlineData("rules", false)]
    [InlineData("join", true)] [InlineData("join", false)]
    [InlineData("replace", true)] [InlineData("replace", false)]
    public async Task ConcurrentStartAndWritesCannotBothCommit(string operation, bool startFirst)
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient();
        var seed = await Seed(factory, owner, player);
        using var a = factory.Services.CreateScope(); using var b = factory.Services.CreateScope();
        var draftsA = a.ServiceProvider.GetRequiredService<ITournamentDrafts>();
        var draftsB = b.ServiceProvider.GetRequiredService<ITournamentDrafts>();
        var regsA = a.ServiceProvider.GetRequiredService<ITournamentRegistrations>();
        var regsB = b.ServiceProvider.GetRequiredService<ITournamentRegistrations>();
        var start = (await draftsA.FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
        var write = (await draftsB.FindOwnedAsync(seed.TournamentId, seed.OwnerId, default))!;
        var participants = await regsA.ListParticipantsAsync(seed.TournamentId, default);
        start.Start(participants.Count, participants.Count(p => p.HasPresentedDeck), start.RulesVersion, start.ParticipationVersion, DateTimeOffset.UtcNow);
        Func<Task> commit;
        if (operation == "rules")
        {
            write.ConfigureRules(8, 1, 0, true, 3, [], write.RulesVersion);
            commit = () => draftsB.SaveAsync(write, default);
        }
        else if (operation == "join")
        {
            using var newcomer = factory.CreateClient();
            await IdentityEndpointsTests.RegisterAndLogin(newcomer, $"late-{Guid.NewGuid():N}@example.test", "Nuevo jugador");
            var id = (await newcomer.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
            var registration = TournamentRegistration.Create(write, id, DateTimeOffset.UtcNow);
            commit = () => regsB.AddAsync(registration, default);
        }
        else
        {
            var registration = (await regsB.FindAsync(seed.TournamentId, seed.PlayerId, default))!;
            var deck = (await b.ServiceProvider.GetRequiredService<IDeckLibrary>().FindOwnedAsync(seed.DeckId, seed.PlayerId, default))!;
            registration.Replace(write, deck, 1, 1, "Reemplazo concurrente", DateTimeOffset.UtcNow);
            commit = () => regsB.SaveAsync(default);
        }
        if (startFirst)
        {
            await draftsA.SaveAsync(start, default);
            await Assert.ThrowsAsync<TournamentConflictException>(commit);
        }
        else
        {
            await commit();
            await Assert.ThrowsAsync<TournamentConflictException>(() => draftsA.SaveAsync(start, default));
        }
        var state = (await owner.GetFromJsonAsync<TournamentDto>($"/api/tournaments/{seed.TournamentId}"))!;
        Assert.Equal(startFirst ? "InProgress" : "Draft", state.Status);
        Assert.Equal(startFirst, state.StartedRules is not null);
    }
}
