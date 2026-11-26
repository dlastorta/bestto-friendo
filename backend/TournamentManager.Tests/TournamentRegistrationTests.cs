using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentRegistrationTests
{
    [Fact]
    public void SnapshotPreservesLiteralContentAfterEditingAndArchivingSource()
    {
        var player = Guid.NewGuid();
        var game = Game.Create("Juego", ["Azul", "Rojo"], 1, 1);
        var tournament = Tournament.CreateDraft(Guid.NewGuid(), game.Id, "Torneo");
        var registration = TournamentRegistration.Create(tournament, player, DateTimeOffset.UtcNow);
        Assert.Null(registration.Snapshot);
        const string literal = "  texto libre\r\n<script>literal</script>  ";
        var deck = Deck.Create(player, game, "Original", literal, [game.Factions.First().Id]);
        registration.Present(tournament, deck, 1, DateTimeOffset.UtcNow);
        deck.Edit(game, "Editado", "Otra lista", [game.Factions.Last().Id], 1);
        deck.Archive(2);
        var snapshot = registration.Snapshot!;
        Assert.Equal(literal, snapshot.ListText);
        Assert.Equal("Original", snapshot.Name);
        Assert.Equal(1, snapshot.SourceRevision);
        Assert.Equal(deck.Id, snapshot.SourceDeckId);
        Assert.Equal(game.Factions.First().Id, Assert.Single(snapshot.FactionIds));
        Assert.Throws<TournamentConflictException>(() => registration.Present(tournament, deck, 3, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RejectsForeignIncompatibleArchivedAndStaleDecks()
    {
        var player = Guid.NewGuid();
        var game = Game.Create("Juego", ["Azul"], 1, 1);
        var other = Game.Create("Otro", ["Rojo"], 1, 1);
        var tournament = Tournament.CreateDraft(Guid.NewGuid(), game.Id, "Torneo");
        var registration = TournamentRegistration.Create(tournament, player, DateTimeOffset.UtcNow);
        foreach (var deck in new[] {
            Deck.Create(Guid.NewGuid(), game, "Ajeno", "", game.Factions.Select(f => f.Id)),
            Deck.Create(player, other, "Otro", "", other.Factions.Select(f => f.Id)) })
            Assert.Throws<TournamentRuleViolationException>(() => registration.Present(tournament, deck, 1, DateTimeOffset.UtcNow));
        var own = Deck.Create(player, game, "Propio", "", game.Factions.Select(f => f.Id));
        Assert.Throws<TournamentConflictException>(() => registration.Present(tournament, own, 0, DateTimeOffset.UtcNow));
        own.Archive(1);
        Assert.Throws<TournamentConflictException>(() => registration.Present(tournament, own, 2, DateTimeOffset.UtcNow));
        Assert.Null(registration.Snapshot);
    }

    [Fact]
    public void NonDraftRejectsRegistrationAndPresentation()
    {
        var game = Game.Create("Juego", ["Azul"], 1, 1);
        var player = Guid.NewGuid();
        var tournament = Tournament.CreateDraft(Guid.NewGuid(), game.Id, "Torneo");
        var registration = TournamentRegistration.Create(tournament, player, DateTimeOffset.UtcNow);
        var deck = Deck.Create(player, game, "Propio", "", game.Factions.Select(f => f.Id));
        typeof(Tournament).GetProperty(nameof(Tournament.Status))!.SetValue(tournament, (TournamentStatus)1);
        Assert.Throws<TournamentConflictException>(() => TournamentRegistration.Create(tournament, player, DateTimeOffset.UtcNow));
        Assert.Throws<TournamentConflictException>(() => registration.Present(tournament, deck, 1, DateTimeOffset.UtcNow));
        Assert.Throws<TournamentConflictException>(() => tournament.EnableRegistrationLink());
    }
}
