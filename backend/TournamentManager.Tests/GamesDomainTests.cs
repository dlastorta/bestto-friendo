using TournamentManager.Domain.Games;
using Xunit;

namespace TournamentManager.Tests;

public sealed class GamesDomainTests
{
    [Fact]
    public void Game_WithOneFactionSelection_AcceptsOwnFaction()
    {
        var game = Game.Create(" L5R ", ["León", "Grulla"], 1, 1);

        game.EnsureValidFactionSelection([game.Factions.First().Id]);

        Assert.Equal("L5R", game.Name);
        Assert.Equal(2, game.Factions.Count);
    }

    [Fact]
    public void Game_RejectsFaction_FromAnotherGame()
    {
        var l5r = Game.Create("L5R", ["León", "Dragón"], 1, 1);
        var magic = Game.Create("Magic", ["Blanco", "Azul"], 1, 2);

        var exception = Assert.Throws<GameRuleViolationException>(() =>
            l5r.EnsureValidFactionSelection([magic.Factions.First().Id]));

        Assert.Contains("no pertenecen", exception.Message);
    }

    [Fact]
    public void Game_RejectsTooManyOrRepeatedSelections()
    {
        var game = Game.Create("L5R", ["León", "Grulla"], 1, 1);
        var id = game.Factions.First().Id;

        Assert.Throws<GameRuleViolationException>(() => game.EnsureValidFactionSelection([id, id]));
    }

    [Fact]
    public void Game_AllowsMultipleFactions_WithinConfiguredBounds()
    {
        var game = Game.Create("Magic", ["Blanco", "Azul", "Negro", "Rojo", "Verde"], 1, 5);

        game.EnsureValidFactionSelection(game.Factions.Select(item => item.Id));
        Assert.Throws<GameRuleViolationException>(() => game.EnsureValidFactionSelection([]));
    }

    [Theory]
    [InlineData("León", "león")]
    [InlineData("Grulla", " Grulla ")]
    public void Game_RejectsDuplicateFactionNames_CaseAndWhitespaceInsensitive(string first, string second)
    {
        Assert.Throws<GameRuleViolationException>(() =>
            Game.Create("L5R", [first, second], 1, 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 3)]
    public void Game_RejectsInvalidMinimumAndMaximum(int minimum, int maximum)
    {
        Assert.Throws<GameRuleViolationException>(() =>
            Game.Create("L5R", ["León", "Grulla"], minimum, maximum));
    }
}
