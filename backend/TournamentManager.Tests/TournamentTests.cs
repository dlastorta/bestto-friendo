using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentTests
{
    [Fact]
    public void CreationProducesDistinctDraftsForTheChosenGameAndOrganizer()
    {
        var organizer = Guid.NewGuid();
        var game = Guid.NewGuid();
        var first = Tournament.CreateDraft(organizer, game, "  Copa local  ");
        var second = Tournament.CreateDraft(organizer, game, "Copa local");
        Assert.Equal("Copa local", first.Name);
        Assert.Equal(organizer, first.OrganizerPlayerId);
        Assert.Equal(game, first.GameId);
        Assert.Equal(TournamentStatus.Draft, first.Status);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" A ")]
    public void InvalidNamesAreRejected(string? name) =>
        Assert.Throws<TournamentRuleViolationException>(() => Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), name));

    [Fact]
    public void NameLengthAndRequiredReferencesAreValidated()
    {
        Assert.Throws<TournamentRuleViolationException>(() => Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new string('a', 121)));
        Assert.Throws<TournamentRuleViolationException>(() => Tournament.CreateDraft(Guid.Empty, Guid.NewGuid(), "Copa"));
        Assert.Throws<TournamentRuleViolationException>(() => Tournament.CreateDraft(Guid.NewGuid(), Guid.Empty, "Copa"));
        Assert.Equal(120, Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new string('a', 120)).Name.Length);
    }
}
