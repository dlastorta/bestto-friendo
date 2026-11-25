using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;
using Xunit;

namespace TournamentManager.Tests;

public sealed class DeckTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  4 x Carta\r\ntexto sin formato\n<script>literal</script>  ")]
    public void ListsAreLiteral_AndRevisionsKeepTheirOriginalContent(string text)
    {
        var game = Game.Create("Juego", ["Azul", "Rojo"], 1, 2);
        var ids = new[] { game.Factions.First().Id };
        var deck = Deck.Create(Guid.NewGuid(), game, "Mazo", text, ids);
        ids[0] = Guid.NewGuid();
        deck.Edit(game, "Renombrado", "Otra lista", [game.Factions.Last().Id], 1);
        var original = deck.Revisions.Single(r => r.Number == 1);
        Assert.Equal(text, original.ListText);
        Assert.Equal("Mazo", original.Name);
        Assert.Equal(game.Factions.First().Id, Assert.Single(original.FactionIds));
        Assert.Equal(2, deck.CurrentRevisionNumber);
        Assert.Equal("Otra lista", deck.CurrentRevision.ListText);
    }

    [Fact]
    public void InvalidFactionsAndStaleVersionsDoNotAppendRevisions()
    {
        var game = Game.Create("Juego", ["Azul", "Rojo"], 1, 2);
        var id = game.Factions.First().Id;
        var deck = Deck.Create(Guid.NewGuid(), game, "Mazo", "", [id]);
        foreach (var ids in new Guid[][] { [], [Guid.NewGuid()], [id, id] })
            Assert.Throws<GameRuleViolationException>(() => deck.Edit(game, "Mazo", "", ids, 1));
        Assert.Throws<DeckConflictException>(() => deck.Edit(game, "Mazo", "", [id], 0));
        Assert.Single(deck.Revisions);
        Assert.Equal(1, deck.Version);
    }

    [Fact]
    public void GameCannotChangeAfterCreation()
    {
        var game = Game.Create("Original", ["Azul"], 1, 1);
        var otherGame = Game.Create("Otro juego", ["Rojo"], 1, 1);
        var deck = Deck.Create(Guid.NewGuid(), game, "Mazo", "", game.Factions.Select(f => f.Id));
        Assert.Throws<DeckRuleViolationException>(() => deck.Edit(otherGame, "Mazo", "", otherGame.Factions.Select(f => f.Id), 1));
        Assert.Equal(game.Id, deck.GameId);
        Assert.Single(deck.Revisions);
    }

    [Fact]
    public void ArchivePreservesHistoryAndPreventsEditing()
    {
        var game = Game.Create("Juego", ["Azul"], 1, 1);
        var ids = game.Factions.Select(f => f.Id).ToArray();
        var deck = Deck.Create(Guid.NewGuid(), game, "Mazo", "Lista original", ids);
        deck.Archive(1);
        Assert.True(deck.IsArchived);
        Assert.Equal(2, deck.Version);
        Assert.Equal("Lista original", Assert.Single(deck.Revisions).ListText);
        Assert.Throws<DeckConflictException>(() => deck.Edit(game, "Mazo", "Otra lista", ids, 2));
    }
}
