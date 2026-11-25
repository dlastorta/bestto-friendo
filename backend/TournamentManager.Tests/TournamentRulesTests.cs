using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentRulesTests
{
    private static Tournament Draft() => Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Copa");

    [Fact]
    public void RulesAreInitiallyUnset_AndPreserveExplicitPointsAndOrder()
    {
        var tournament = Draft();
        Assert.Null(tournament.Rules);
        var order = new[] { "OMWPercentage", "Buchholz" };
        tournament.ConfigureRules(2.5m, 1m, 0m, true, 2m, order, 0);
        order[0] = "BuchholzCut";
        Assert.Equal(2.5m, tournament.Rules!.WinPoints);
        Assert.Equal(new[] { "OMWPercentage", "Buchholz" }, tournament.Rules.TieBreakers);
        Assert.Equal(1, TournamentRules.MaximumByesPerParticipant);
        tournament.ConfigureRules(3, 0.5m, 0, false, 2, ["Buchholz", "OMWPercentage"], 1);
        Assert.False(tournament.Rules.ByesEnabled);
        Assert.Equal(2, tournament.RulesVersion);
        Assert.Equal(new[] { "Buchholz", "OMWPercentage" }, tournament.Rules.TieBreakers);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1000001)]
    public void InvalidPointsDoNotChangeSavedRules(decimal invalid)
    {
        var tournament = Draft();
        tournament.ConfigureRules(3, 1, 0, true, 3, [], 0);
        Assert.Throws<TournamentRuleViolationException>(() => tournament.ConfigureRules(invalid, 1, 0, true, 3, [], 1));
        Assert.Throws<TournamentRuleViolationException>(() => tournament.ConfigureRules(3, invalid, 0, true, 3, [], 1));
        Assert.Throws<TournamentRuleViolationException>(() => tournament.ConfigureRules(3, 1, invalid, true, 3, [], 1));
        Assert.Throws<TournamentRuleViolationException>(() => tournament.ConfigureRules(3, 1, 0, true, invalid, [], 1));
        Assert.Equal(1, tournament.RulesVersion);
        Assert.Equal(3, tournament.Rules!.WinPoints);
    }

    [Fact]
    public void UnknownDuplicateOrMissingTieBreakersAreRejected_AndEmptyIsAllowed()
    {
        var tournament = Draft();
        foreach (var order in new string[]?[] { null, ["Unknown"], ["Buchholz", "Buchholz"] })
            Assert.Throws<TournamentRuleViolationException>(() => tournament.ConfigureRules(3, 1, 0, true, 3, order, 0));
        Assert.Null(tournament.Rules);
        tournament.ConfigureRules(0, 0, 0, false, 0, [], 0);
        Assert.Empty(tournament.Rules!.TieBreakers);
    }

    [Fact]
    public void StaleVersionAndNonDraftStatusRejectChanges()
    {
        var tournament = Draft();
        tournament.ConfigureRules(3, 1, 0, true, 3, [], 0);
        Assert.Throws<TournamentConflictException>(() => tournament.ConfigureRules(4, 1, 0, true, 3, [], 0));
        // Simula un estado persistido fuera de Draft, sin introducir el caso de uso de inicio del Slice 07.
        typeof(Tournament).GetProperty(nameof(Tournament.Status))!.SetValue(tournament, (TournamentStatus)1);
        Assert.Throws<TournamentConflictException>(() => tournament.ConfigureRules(4, 1, 0, true, 3, [], 1));
        Assert.Equal(3, tournament.Rules!.WinPoints);
        Assert.Equal(1, tournament.RulesVersion);
    }
}
