using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class ByeTests
{
    private static Tournament Start(bool enabled = true)
    {
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Byes");
        t.ConfigureRules(3, 1, 0, enabled, 2.5m, ["Buchholz", "OMWPercentage", "BuchholzCut"], 0);
        t.Start(3, 3, 1, 0, DateTimeOffset.UtcNow);
        return t;
    }

    [Fact]
    public void EnforcesRoundPlayerParityAndMutualExclusionWithMatches()
    {
        var t = Start(); Guid[] p = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        TournamentBye Assign(int round, Guid player, TournamentBye[] byes, TournamentMatch[] matches) =>
            TournamentBye.Assign(t, round, player, p, matches, byes, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        Assert.Throws<TournamentRuleViolationException>(() => Assign(0, p[0], [], []));
        Assert.Throws<TournamentRuleViolationException>(() => Assign(1, Guid.NewGuid(), [], []));
        Assert.Throws<TournamentRuleViolationException>(() => TournamentBye.Assign(t, 1, p[0], p[..2], [], [], t.OrganizerPlayerId, DateTimeOffset.UtcNow));
        var bye = Assign(1, p[0], [], []);
        Assert.Throws<TournamentConflictException>(() => Assign(1, p[1], [bye], []));
        Assert.Throws<TournamentConflictException>(() => Assign(2, p[0], [bye], []));
        Assert.Throws<TournamentConflictException>(() => TournamentMatch.Create(t, 1, p[1], p[0], p, [], t.OrganizerPlayerId, DateTimeOffset.UtcNow, [bye]));
        var match = TournamentMatch.Create(t, 1, p[1], p[2], p, [], t.OrganizerPlayerId, DateTimeOffset.UtcNow, [bye]);
        Assert.Throws<TournamentConflictException>(() => Assign(1, p[2], [], [match]));
        Assert.Equal(t.OrganizerPlayerId, bye.ActorPlayerId);
        Assert.Equal(2, t.MatchesVersion);
        var disabled = Start(false);
        Assert.Throws<TournamentRuleViolationException>(() => TournamentBye.Assign(disabled, 1, p[0], p, [], [], disabled.OrganizerPlayerId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ByeAddsFrozenPointsToOpponentsBuchholzButNeverCountsAsPlayed()
    {
        var t = Start(); Guid[] p = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var match = TournamentMatch.Create(t, 1, p[0], p[1], p, [], t.OrganizerPlayerId, DateTimeOffset.UtcNow, []);
        match.RecordResult(t, "PlayerOneWin", "Played", null, 0, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        var bye = TournamentBye.Assign(t, 2, p[1], p, [match], [], t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        var rows = TournamentStandings.Calculate(p, [match], t.StartedRules!, [bye]);
        var a = rows.Single(r => r.PlayerId == p[0]);
        Assert.Equal(new TieBreakerValue(5, 2), a.TieBreakers[0]);
        Assert.Equal(new TieBreakerValue(1, 3), a.TieBreakers[1]);
        Assert.Equal(TieBreakerValue.Zero, a.TieBreakers[2]);
        Assert.Equal(2.5m, rows.Single(r => r.PlayerId == p[1]).Points);
        Assert.All(rows.Single(r => r.PlayerId == p[2]).TieBreakers, v => Assert.Equal(TieBreakerValue.Zero, v));
        match.RecordResult(t, "Draw", "Played", "Corrección", 1, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        a = TournamentStandings.Calculate(p, [match], t.StartedRules!, [bye]).Single(r => r.PlayerId == p[0]);
        Assert.Equal(new TieBreakerValue(7, 2), a.TieBreakers[0]);
        Assert.Equal(new TieBreakerValue(1, 2), a.TieBreakers[1]);
    }
}
