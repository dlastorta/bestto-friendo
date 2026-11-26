using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PostgresRegistrationIntegrationTests
{
    [Fact]
    public async Task LinkAllowsJoiningWithoutDeck_AndOnlyPlayerCanReadPersistentSnapshot()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true") return;
        using var factory = new WebApplicationFactory<Program>();
        using var owner = factory.CreateClient(); using var player = factory.CreateClient(); using var stranger = factory.CreateClient();
        var email = $"registration-owner-{Guid.NewGuid():N}@example.test";
        await IdentityEndpointsTests.RegisterAndLogin(owner, email, "Organizador");
        await IdentityEndpointsTests.RegisterAndLogin(player, $"player-{Guid.NewGuid():N}@example.test", "Participante");
        var game = Game.Create($"Registration-{Guid.NewGuid():N}", ["Azul"], 1, 1);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, "TournamentOrganizer")).Succeeded);
            await scope.ServiceProvider.GetRequiredService<IGameCatalog>().AddAsync(game, default);
        }
        var created = await owner.PostAsJsonAsync("/api/tournaments", new CreateTournamentDraft("Copa privada", game.Id));
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<TournamentDto>())!.Id;
        var ownerPath = $"/api/tournaments/{id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await player.PostAsJsonAsync(ownerPath + "/registration-link", new { })).StatusCode);
        var link = await owner.PostAsJsonAsync(ownerPath + "/registration-link", new { });
        link.EnsureSuccessStatusCode();
        var token = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetGuid();
        var invitePath = $"/api/registrations/invite/{token}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync(invitePath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await player.GetAsync(invitePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync(ownerPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync($"/api/registrations/invite/{Guid.NewGuid()}")).StatusCode);
        player.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await player.PostAsJsonAsync(invitePath, new { })).StatusCode);
        await IdentityTestSupport.SetCsrfAsync(player);
        var joined = await player.PostAsJsonAsync(invitePath, new { });
        joined.EnsureSuccessStatusCode();
        Assert.False((await joined.Content.ReadFromJsonAsync<RegistrationDetail>())!.HasPresentedDeck);
        Assert.Equal(HttpStatusCode.OK, (await player.PostAsJsonAsync(invitePath, new { })).StatusCode);
        Assert.Single((await player.GetFromJsonAsync<MyRegistration[]>("/api/registrations"))!);
        const string literal = "  lista privada\r\n3 x Carta\n<script>texto</script> ";
        var deckResponse = await player.PostAsJsonAsync("/api/decks", new CreateDeck(game.Id, "Mazo secreto", literal, game.Factions.Select(f => f.Id).ToArray()));
        deckResponse.EnsureSuccessStatusCode();
        var deck = (await deckResponse.Content.ReadFromJsonAsync<DeckDto>())!;
        var minePath = $"/api/registrations/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(minePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await player.PostAsJsonAsync(minePath + "/deck", new PresentDeck(deck.Id, 0))).StatusCode);
        var presented = await player.PostAsJsonAsync(minePath + "/deck", new PresentDeck(deck.Id, deck.Version));
        presented.EnsureSuccessStatusCode();
        Assert.Equal(literal, (await presented.Content.ReadFromJsonAsync<RegistrationDetail>())!.Deck!.ListText);
        Assert.Equal(HttpStatusCode.Conflict, (await player.PostAsJsonAsync(minePath + "/deck", new PresentDeck(deck.Id, deck.Version))).StatusCode);
        var edit = await player.PutAsJsonAsync($"/api/decks/{deck.Id}", new EditDeck("Cambiado", "Otra lista", game.Factions.Select(f => f.Id).ToArray(), deck.Version));
        edit.EnsureSuccessStatusCode();
        var updated = (await edit.Content.ReadFromJsonAsync<DeckDto>())!;
        (await player.PostAsJsonAsync($"/api/decks/{deck.Id}/archive", new ArchiveDeck(updated.Version))).EnsureSuccessStatusCode();
        var saved = (await player.GetFromJsonAsync<RegistrationDetail>(minePath))!;
        Assert.Equal(literal, saved.Deck!.ListText);
        Assert.Equal(1, saved.Deck.SourceRevision);
        var roster = (await owner.GetFromJsonAsync<JsonElement[]>(ownerPath + "/participants"))!;
        var participant = Assert.Single(roster);
        Assert.True(participant.GetProperty("hasPresentedDeck").GetBoolean());
        Assert.Equal(new[] { "displayName", "hasPresentedDeck", "playerId" }, participant.EnumerateObject().Select(p => p.Name).Order().ToArray());
        // El organizador inscripto solo puede consultar su propia presentación, nunca la de otro jugador.
        (await owner.PostAsJsonAsync(invitePath, new { })).EnsureSuccessStatusCode();
        Assert.Null((await owner.GetFromJsonAsync<RegistrationDetail>(minePath))!.Deck);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(minePath + "/deck", new PresentDeck(deck.Id, updated.Version))).StatusCode);
        await IdentityEndpointsTests.RegisterAndLogin(stranger, $"stranger-{Guid.NewGuid():N}@example.test", "Otro jugador");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(minePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(ownerPath + "/participants")).StatusCode);

        var playerId = (await stranger.GetFromJsonAsync<PlayerProfile>("/api/me"))!.Id;
        using var scopeA = factory.Services.CreateScope(); using var scopeB = factory.Services.CreateScope();
        var a = scopeA.ServiceProvider.GetRequiredService<ITournamentRegistrations>();
        var b = scopeB.ServiceProvider.GetRequiredService<ITournamentRegistrations>();
        var ta = (await a.FindByTokenAsync(token, default))!; var tb = (await b.FindByTokenAsync(token, default))!;
        var ra = TournamentRegistration.Create(ta, playerId, DateTimeOffset.UtcNow);
        var rb = TournamentRegistration.Create(tb, playerId, DateTimeOffset.UtcNow);
        await a.AddAsync(ra, default);
        await Assert.ThrowsAsync<TournamentConflictException>(() => b.AddAsync(rb, default));
        Assert.Single((await stranger.GetFromJsonAsync<MyRegistration[]>("/api/registrations"))!);
    }
}
