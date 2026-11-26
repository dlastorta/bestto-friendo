using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentMatchTests
{
    private static Tournament Started()
    {
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Torneo");
        t.ConfigureRules(5.5m, 2, 0.5m, true, 3, [], 0);
        t.Start(2, 2, 1, 0, DateTimeOffset.UtcNow);
        return t;
    }

    [Fact]
    public void CreationRequiresStartedTournamentDistinctMembersAndFreeRound()
    {
        var t = Started(); var one = Guid.NewGuid(); var two = Guid.NewGuid(); Guid[] players = [one, two];
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<TournamentRuleViolationException>(() => TournamentMatch.Create(t, 0, one, two, players, [], t.OrganizerPlayerId, now));
        Assert.Throws<TournamentRuleViolationException>(() => TournamentMatch.Create(t, 1, one, one, players, [], t.OrganizerPlayerId, now));
        Assert.Throws<TournamentRuleViolationException>(() => TournamentMatch.Create(t, 1, one, Guid.NewGuid(), players, [], t.OrganizerPlayerId, now));
        var m = TournamentMatch.Create(t, 1, one, two, players, [], t.OrganizerPlayerId, now);
        Assert.Throws<TournamentConflictException>(() => TournamentMatch.Create(t, 1, two, one, players, [m], t.OrganizerPlayerId, now));
        Assert.Equal(2, TournamentMatch.Create(t, 2, one, two, players, [m], t.OrganizerPlayerId, now).Round);
        var draft = Tournament.CreateDraft(t.OrganizerPlayerId, t.GameId, "Borrador");
        Assert.Throws<TournamentConflictException>(() => TournamentMatch.Create(draft, 1, one, two, players, [], t.OrganizerPlayerId, now));
    }

    [Fact]
    public void CorrectionsKeepHistoryAndRecalculatePointsFromFrozenRules()
    {
        var t = Started(); var one = Guid.NewGuid(); var two = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var m = TournamentMatch.Create(t, 1, one, two, [one, two], [], t.OrganizerPlayerId, now);
        m.RecordResult(t, "PlayerOneWin", "Played", null, 0, t.OrganizerPlayerId, now);
        var original = m.CurrentResult!;
        Assert.Equal((5.5m, 0.5m), original.Points(t.StartedRules!));
        Assert.Throws<TournamentRuleViolationException>(() => m.RecordResult(t, "Draw", "Forfeit", "error", 1, t.OrganizerPlayerId, now));
        Assert.Throws<TournamentRuleViolationException>(() => m.RecordResult(t, "Draw", "Played", " ", 1, t.OrganizerPlayerId, now));
        Assert.Throws<TournamentConflictException>(() => m.RecordResult(t, "Draw", "Played", "corrección", 0, t.OrganizerPlayerId, now));
        m.RecordResult(t, "Draw", "Played", "Resultado cargado por error", 1, t.OrganizerPlayerId, now.AddMinutes(1));
        Assert.Equal((2m, 2m), m.CurrentResult!.Points(t.StartedRules!));
        m.RecordResult(t, "PlayerTwoWin", "Forfeit", "Abandono confirmado", 2, t.OrganizerPlayerId, now.AddMinutes(2));
        Assert.Equal((0.5m, 5.5m), m.CurrentResult!.Points(t.StartedRules!));
        Assert.Equal(3, m.Results.Count);
        Assert.Equal("PlayerOneWin", original.Outcome);
        Assert.All(m.Results, r => Assert.Equal(t.OrganizerPlayerId, r.ActorPlayerId));
    }
}
