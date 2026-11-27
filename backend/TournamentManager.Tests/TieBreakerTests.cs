using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TieBreakerTests
{
    [Fact]
    public void AppliesNextCriterionOnlyWithinRemainingPointAndBuchholzTies()
    {
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Cadena");
        t.ConfigureRules(3, 1, 0, true, 3, ["Buchholz", "OMWPercentage"], 0);
        t.Start(4, 4, 1, 0, DateTimeOffset.UtcNow);
        Guid[] p = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var matches = new List<TournamentMatch>();
        foreach (var (round, one, two) in new[] { (1, 0, 1), (1, 2, 3), (2, 1, 2) })
        {
            var m = TournamentMatch.Create(t, round, p[one], p[two], p, matches, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
            m.RecordResult(t, "PlayerOneWin", "Played", null, 0, t.OrganizerPlayerId, DateTimeOffset.UtcNow); matches.Add(m);
        }
        var rows = TournamentStandings.Calculate(p, matches, t.StartedRules!);
        Assert.Equal(new[] { p[1], p[0], p[2], p[3] }, rows.Select(r => r.PlayerId));
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(r => r.Position));
        Assert.Equal(new TieBreakerValue(3, 1), rows[1].TieBreakers[0]);
        Assert.Equal(rows[1].TieBreakers[0], rows[2].TieBreakers[0]);
        Assert.True(rows[1].TieBreakers[1].CompareTo(rows[2].TieBreakers[1]) > 0);
    }
    [Fact]
    public void UsesPlayedOpponentsWithMultiplicityFloorAndOneMinimumDiscard()
    {
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Desempates");
        t.ConfigureRules(3, 1, 0, true, 3, ["Buchholz", "OMWPercentage", "BuchholzCut"], 0);
        t.Start(4, 4, 1, 0, DateTimeOffset.UtcNow);
        Guid[] p = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var matches = new List<TournamentMatch>();
        TournamentMatch Add(int round, int one, int two, string outcome, string kind)
        {
            var m = TournamentMatch.Create(t, round, p[one], p[two], p, matches, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
            m.RecordResult(t, outcome, kind, null, 0, t.OrganizerPlayerId, DateTimeOffset.UtcNow); matches.Add(m); return m;
        }
        Add(1, 0, 1, "PlayerOneWin", "Played");
        Add(2, 0, 1, "PlayerOneWin", "Played");
        var forfeit = Add(3, 1, 2, "PlayerOneWin", "Forfeit");
        var rows = TournamentStandings.Calculate(p, matches, t.StartedRules!);
        var a = rows.Single(r => r.PlayerId == p[0]);
        Assert.Equal(new TieBreakerValue(6, 1), a.TieBreakers[0]); // rival repeated, total includes forfeit points
        Assert.Equal(new TieBreakerValue(1, 3), a.TieBreakers[1]); // rival's forfeit is not a win in MWP
        Assert.Equal(new TieBreakerValue(3, 1), a.TieBreakers[2]);
        Assert.All(rows.Single(r => r.PlayerId == p[2]).TieBreakers, v => Assert.Equal(TieBreakerValue.Zero, v));
        Assert.All(rows.Single(r => r.PlayerId == p[3]).TieBreakers, v => Assert.Equal(TieBreakerValue.Zero, v));
        forfeit.RecordResult(t, "Draw", "Played", "Se jugó", 1, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        rows = TournamentStandings.Calculate(p, matches, t.StartedRules!);
        Assert.Equal(new TieBreakerValue(2, 1), rows.Single(r => r.PlayerId == p[0]).TieBreakers[0]);
        Assert.Equal(TieBreakerValue.Zero, rows.Single(r => r.PlayerId == p[2]).TieBreakers[2]);
    }

    [Fact]
    public void ExactFractionsDoNotBreakMathematicalTiesOrLoseSmallDifferences()
    {
        var a = new TieBreakerValue(1, 3);
        Assert.Equal(0, (a + a + a).CompareTo(new TieBreakerValue(1, 1)));
        Assert.True(new TieBreakerValue(10000001, 30000000).CompareTo(a) > 0);
        Assert.Equal(a.Rounded(6), new TieBreakerValue(10000001, 30000000).Rounded(6));
    }
}
