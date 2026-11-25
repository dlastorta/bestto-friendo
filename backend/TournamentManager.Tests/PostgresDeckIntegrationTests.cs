using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresDeckIntegrationTests
{
    [Fact]
    public async Task RevisionsPersist_AndConcurrentEditsCannotOverwriteEachOther()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        await IdentityEndpointsTests.RegisterAndLogin(client, $"decks-{Guid.NewGuid():N}@example.test", "Jugador");
        var owner = (await client.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
        var game = Game.Create($"Decks-{Guid.NewGuid():N}", ["Azul", "Rojo"], 1, 2);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IGameCatalog>().AddAsync(game, default);
        var factions = new[] { game.Factions.First().Id };
        const string original = "  4 x Nombre carta\r\nSin formato ni validación  ";
        var response = await client.PostAsJsonAsync("/api/decks", new CreateDeck(game.Id, "Mazo", original, factions));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deck = (await response.Content.ReadFromJsonAsync<DeckDto>())!;
        var path = $"/api/decks/{deck.Id}";
        Assert.Equal(original, (await client.GetFromJsonAsync<DeckDto>(path))!.Current.ListText);
        Assert.Equal(factions, (await client.GetFromJsonAsync<DeckDto>(path))!.Current.FactionIds);
        using var otherClient = factory.CreateClient();
        await IdentityEndpointsTests.RegisterAndLogin(otherClient, $"other-{Guid.NewGuid():N}@example.test", "Otro jugador");
        Assert.Empty((await otherClient.GetFromJsonAsync<DeckSummary[]>("/api/decks"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync(path + "/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PutAsJsonAsync(path, new EditDeck("Ajeno", "", factions, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PostAsJsonAsync(path + "/archive", new ArchiveDeck(1))).StatusCode);
        // Dos contextos cargan la misma versión antes de que alguno guarde.
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IDeckLibrary>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IDeckLibrary>();
        var first = (await firstStore.FindOwnedAsync(deck.Id, owner, default))!;
        var second = (await secondStore.FindOwnedAsync(deck.Id, owner, default))!;
        first.Edit(game, "Ganador", "Nueva lista", factions, 1);
        second.Edit(game, "Perdedor", "No debe persistir", factions, 1);
        await firstStore.SaveAsync(first, default);
        await Assert.ThrowsAsync<DeckConflictException>(() => secondStore.SaveAsync(second, default));
        var history = (await client.GetFromJsonAsync<DeckRevisionDto[]>(path + "/revisions"))!;
        Assert.Equal(2, history.Length);
        Assert.Equal(original, history.Single(r => r.Number == 1).ListText);
        Assert.Equal("Ganador", history.Single(r => r.Number == 2).Name);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, new EditDeck("Viejo", "", factions, 1))).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<DeckSummary[]>($"/api/decks?gameId={game.Id}"))!);
        Assert.Empty((await client.GetFromJsonAsync<DeckSummary[]>($"/api/decks?gameId={Guid.NewGuid()}"))!);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/archive", new ArchiveDeck(2))).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<DeckRevisionDto[]>(path + "/revisions"))!.Length);
        Assert.Empty((await client.GetFromJsonAsync<DeckSummary[]>("/api/decks"))!);
        Assert.Single((await client.GetFromJsonAsync<DeckSummary[]>("/api/decks?archived=true"))!);
    }
}
