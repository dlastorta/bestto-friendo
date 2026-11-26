using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentStartTests
{
    [Fact]
    public void StartRequiresConfiguredRulesTwoPlayersAndEveryDeck()
    {
        var tournament = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Copa");
        Assert.Throws<TournamentRuleViolationException>(() => tournament.Start(2, 2, 0, 0, DateTimeOffset.UtcNow));
        tournament.ConfigureRules(3, 1, 0, true, 3, ["OMWPercentage", "Buchholz"], 0);
        Assert.Throws<TournamentRuleViolationException>(() => tournament.Start(1, 1, 1, 0, DateTimeOffset.UtcNow));
        Assert.Throws<TournamentRuleViolationException>(() => tournament.Start(2, 1, 1, 0, DateTimeOffset.UtcNow));
        Assert.Throws<TournamentConflictException>(() => tournament.Start(2, 2, 0, 0, DateTimeOffset.UtcNow));
        Assert.Throws<TournamentConflictException>(() => tournament.Start(2, 2, 1, 1, DateTimeOffset.UtcNow));
        Assert.Equal(TournamentStatus.Draft, tournament.Status);
        Assert.Null(tournament.StartedRules);
        var now = DateTimeOffset.UtcNow;
        tournament.Start(2, 2, 1, 0, now);
        Assert.Equal(TournamentStatus.InProgress, tournament.Status);
        Assert.Equal(now, tournament.StartedAt);
        Assert.Equal(3, tournament.StartedRules!.WinPoints);
        Assert.Equal(1, tournament.StartedRules.SourceVersion);
        Assert.Equal(new[] { "OMWPercentage", "Buchholz" }, tournament.StartedRules.TieBreakers);
        Assert.Throws<TournamentConflictException>(() => tournament.ConfigureRules(8, 1, 0, false, 0, [], 1));
        Assert.Throws<TournamentConflictException>(() => tournament.RecordParticipationChange());
        Assert.Throws<TournamentConflictException>(() => tournament.Start(2, 2, 1, 1, now));
        Assert.Equal(now, tournament.StartedAt);
    }

    [Fact]
    public void ReplacementRetainsHistoryActorAndReasonAndLocksAtStart()
    {
        var game = Game.Create("Juego", ["Azul"], 1, 1);
        var player = Guid.NewGuid();
        var tournament = Tournament.CreateDraft(Guid.NewGuid(), game.Id, "Copa");
        tournament.ConfigureRules(3, 1, 0, true, 3, [], 0);
        var registration = TournamentRegistration.Create(tournament, player, DateTimeOffset.UtcNow);
        var deck = Deck.Create(player, game, "Original", "Lista original", game.Factions.Select(f => f.Id));
        registration.Present(tournament, deck, 1, DateTimeOffset.UtcNow);
        deck.Edit(game, "Editado", "Lista nueva", game.Factions.Select(f => f.Id), 1);
        Assert.Throws<TournamentRuleViolationException>(() => registration.Replace(tournament, deck, 2, 1, " ", DateTimeOffset.UtcNow));
        Assert.Throws<TournamentConflictException>(() => registration.Replace(tournament, deck, 2, 0, "Cambio", DateTimeOffset.UtcNow));
        registration.Replace(tournament, deck, 2, 1, " Actualización propia ", DateTimeOffset.UtcNow);
        Assert.Equal(2, registration.Snapshots.Count);
        Assert.Equal("Lista original", registration.Snapshots.Single(s => s.Number == 1).ListText);
        Assert.Equal("Lista nueva", registration.Snapshot!.ListText);
        Assert.Equal(player, registration.Snapshot.ActorPlayerId);
        Assert.Equal("Actualización propia", registration.Snapshot.Reason);
        tournament.Start(2, 2, 1, tournament.ParticipationVersion, DateTimeOffset.UtcNow);
        Assert.Throws<TournamentConflictException>(() => registration.Replace(tournament, deck, 2, 2, "Después", DateTimeOffset.UtcNow));
        Assert.Equal(2, registration.Snapshots.Count);
    }
}
