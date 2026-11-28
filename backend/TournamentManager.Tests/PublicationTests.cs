using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class PublicationTests
{
    [Fact]
    public void OnlyClosedTournamentsPublishAndReopeningRequiresExplicitRepublication()
    {
        var now = DateTimeOffset.UtcNow;
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Publicación");
        Assert.Throws<TournamentConflictException>(() => t.SetPublication(true, 0, 0, now));
        t.ConfigureRules(3, 1, 0, false, 0, [], 0); t.Start(2, 2, 1, 0, now);
        Guid[] players = [Guid.NewGuid(), Guid.NewGuid()];
        var round = SwissRound.Generate(t, 1, players, [], [], players, t.OrganizerPlayerId, now);
        round.Matches[0].RecordResult(t, "Draw", "Played", null, 0, t.OrganizerPlayerId, now);
        t.Finish(players, round.Matches, [], 0, t.MatchesVersion, t.ResultsVersion, t.OrganizerPlayerId, now);
        Assert.Null(t.PublishedAt);
        t.SetPublication(true, 0, 1, now); Assert.Equal(now, t.PublishedAt);
        Assert.Throws<TournamentConflictException>(() => t.SetPublication(false, 0, 1, now));
        t.Reopen("Corrección", 1, t.OrganizerPlayerId, now);
        Assert.Null(t.PublishedAt); Assert.Equal(2, t.PublicationVersion);
        Assert.Throws<TournamentConflictException>(() => t.SetPublication(true, 2, 2, now));
        t.Finish(players, round.Matches, [], 2, t.MatchesVersion, t.ResultsVersion, t.OrganizerPlayerId, now);
        Assert.Null(t.PublishedAt);
        t.SetPublication(true, 2, 3, now);
        t.SetPublication(false, 3, 3, now);
        Assert.Null(t.PublishedAt); Assert.Equal(TournamentStatus.Finished, t.Status);
    }

    [Fact]
    public void ConsentIsSpecificToTheSnapshotRevocableAndResetByReplacement()
    {
        var now = DateTimeOffset.UtcNow; var player = Guid.NewGuid();
        var game = Game.Create("Cartas", ["Azul"], 1, 1);
        var t = Tournament.CreateDraft(player, game.Id, "Copa");
        var deck = Deck.Create(player, game, "Original", "Lista original", game.Factions.Select(f => f.Id));
        var registration = TournamentRegistration.Create(t, player, now);
        registration.Present(t, deck, 1, now);
        Assert.Null(registration.PublicSnapshotNumber);
        registration.SetDeckVisibility(true, 1, 0, now);
        deck.Edit(game, "Editado", "Lista nueva", game.Factions.Select(f => f.Id), 1);
        Assert.Equal("Lista original", registration.Snapshot!.ListText);
        Assert.Equal(1, registration.PublicSnapshotNumber);
        Assert.Throws<TournamentConflictException>(() => registration.SetDeckVisibility(false, 1, 0, now));
        registration.Replace(t, deck, 2, 1, "Cambio", now);
        Assert.Null(registration.PublicSnapshotNumber);
        Assert.Throws<TournamentConflictException>(() => registration.SetDeckVisibility(true, 1, 2, now));
        registration.SetDeckVisibility(true, 2, 2, now);
        registration.SetDeckVisibility(false, 2, 3, now);
        Assert.Null(registration.PublicSnapshotNumber);
    }
}
