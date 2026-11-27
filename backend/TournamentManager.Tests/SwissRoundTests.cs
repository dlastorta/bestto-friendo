using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class SwissRoundTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-27T12:00:00Z");
    private static Tournament Start(int count, bool byes = true)
    {
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Suizo");
        t.ConfigureRules(3, 1, 0, byes, 3, ["Buchholz", "OMWPercentage"], 0);
        t.Start(count, count, 1, 0, Now);
        return t;
    }
    private static Guid[] Players(int count) => Enumerable.Range(1, count).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();
    private static GeneratedRound Generate(Tournament t, Guid[] p, int number, TournamentMatch[] matches, TournamentBye[] byes, Guid[]? draw = null) =>
        SwissRound.Generate(t, number, p, matches, byes, draw ?? p, t.OrganizerPlayerId, Now);
    private static void Complete(Tournament t, IEnumerable<TournamentMatch> matches)
    {
        foreach (var m in matches) m.RecordResult(t, "PlayerOneWin", "Played", null, 0, t.OrganizerPlayerId, Now);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)]
    public void FirstRoundUsesProvidedDrawAndAuditsCompleteAssignment(int count)
    {
        var t = Start(count); var p = Players(count); var draw = p.Reverse().ToArray();
        var generated = Generate(t, p, 1, [], [], draw);
        Assert.Equal(draw, generated.Audit.ParticipantOrder);
        Assert.Equal(t.OrganizerPlayerId, generated.Audit.ActorPlayerId);
        Assert.Equal(Now, generated.Audit.GeneratedAt);
        Assert.Equal(count / 2, generated.Matches.Count);
        Assert.Equal(count % 2 == 1 ? draw[0] : (Guid?)null, generated.Bye?.PlayerId);
        var occupied = generated.Matches.SelectMany(m => new[] { m.PlayerOneId, m.PlayerTwoId }).Concat(generated.Bye is { } b ? [b.PlayerId] : []);
        Assert.Equal(p.Order(), occupied.Order());
        Assert.Equal(draw[count % 2], generated.Matches[0].PlayerOneId);
        Assert.Equal(draw[count % 2 + 1], generated.Matches[0].PlayerTwoId);
        Assert.All(generated.Matches, m => Assert.Null(m.CurrentResult));
        Assert.Equal(0, generated.Audit.SourceMatchesVersion);
    }

    [Fact]
    public void LaterRoundsStartAtBottomAndGiveByeToWorstEligibleWithoutRevenge()
    {
        var t = Start(5); var p = Players(5);
        var first = Generate(t, p, 1, [], []); Complete(t, first.Matches);
        var matches = first.Matches.ToArray(); TournamentBye[] byes = [first.Bye!];
        var expectedOrder = TournamentStandings.Calculate(p, matches, t.StartedRules!, byes).Reverse().Select(r => r.PlayerId).ToArray();
        var second = Generate(t, p, 2, matches, byes);
        Assert.Equal(expectedOrder, second.Audit.ParticipantOrder);
        Assert.Equal(expectedOrder.First(id => id != first.Bye!.PlayerId), second.Bye!.PlayerId);
        Assert.Equal(expectedOrder.First(id => id != second.Bye.PlayerId), second.Matches[0].PlayerOneId);
        Assert.All(second.Matches, m => Assert.DoesNotContain(matches, old =>
            new[] { old.PlayerOneId, old.PlayerTwoId }.ToHashSet().SetEquals([m.PlayerOneId, m.PlayerTwoId])));
    }

    [Fact]
    public void PendingIncompleteSkippedAndStaleRoundsCannotAdvanceOrMutateTournament()
    {
        var t = Start(4); var p = Players(4);
        var first = Generate(t, p, 1, [], []);
        var version = t.MatchesVersion;
        Assert.Throws<TournamentConflictException>(() => Generate(t, p, 2, first.Matches.ToArray(), []));
        Assert.Throws<TournamentConflictException>(() => Generate(t, p, 1, first.Matches.ToArray(), []));
        Complete(t, first.Matches);
        Assert.Throws<TournamentConflictException>(() => Generate(t, p, 2, [first.Matches[0]], []));
        Assert.Throws<TournamentConflictException>(() => Generate(t, p, 3, first.Matches.ToArray(), []));
        Assert.Equal(version, t.MatchesVersion);
        var second = Generate(t, p, 2, first.Matches.ToArray(), []);
        var pairs = second.Matches.Select(m => (m.PlayerOneId, m.PlayerTwoId)).ToArray();
        first.Matches[0].RecordResult(t, "Draw", "Played", "Corregido", 1, t.OrganizerPlayerId, Now);
        Assert.Equal(pairs, second.Matches.Select(m => (m.PlayerOneId, m.PlayerTwoId)));
    }

    [Fact]
    public void ReportsExhaustedOpponentsIncludingForfeitsAndPreservesLegacyManualBehavior()
    {
        var t = Start(2); var p = Players(2);
        var first = Generate(t, p, 1, [], []);
        first.Matches[0].RecordResult(t, "PlayerOneWin", "Forfeit", null, 0, t.OrganizerPlayerId, Now);
        var before = t.MatchesVersion;
        Assert.Contains("No existe", Assert.Throws<TournamentConflictException>(() => Generate(t, p, 2, first.Matches.ToArray(), [])).Message);
        Assert.Equal(before, t.MatchesVersion);
        Assert.Throws<TournamentConflictException>(() => TournamentMatch.Create(t, 2, p[1], p[0], p, first.Matches.ToArray(), t.OrganizerPlayerId, Now, []));
        HistoricalTournamentFixture.UseManualPairing(t);
        Assert.Throws<TournamentConflictException>(() => Generate(t, p, 2, first.Matches.ToArray(), []));
        Assert.Equal(9, TournamentMatch.Create(t, 9, p[0], p[1], p, first.Matches.ToArray(), t.OrganizerPlayerId, Now, []).Round);
    }

    [Fact]
    public void BacktracksWhenNearestAvailableOpponentWouldStrandOtherPlayers()
    {
        var t = Start(6); var p = Players(6); var history = new List<TournamentMatch>();
        foreach (var (a, b) in new[] { (1, 0), (5, 3), (4, 2) })
        {
            var m = TournamentMatch.Create(t, 1, p[a], p[b], p, history, t.OrganizerPlayerId, Now, []);
            m.RecordResult(t, "Draw", "Played", null, 0, t.OrganizerPlayerId, Now);
            history.Add(m);
        }
        var round = Generate(t, p, 2, history.ToArray(), []);
        Assert.Equal(new[] { (p[5], p[4]), (p[3], p[1]), (p[2], p[0]) },
            round.Matches.Select(m => (m.PlayerOneId, m.PlayerTwoId)));
    }

    [Fact]
    public void NoPlayerCanGetSecondByeWhenAllCandidatesAreExhausted()
    {
        var t = Start(3); var p = Players(3);
        var matches = new List<TournamentMatch>(); var byes = new List<TournamentBye>();
        for (var number = 1; number <= 3; number++)
        {
            var round = Generate(t, p, number, matches.ToArray(), byes.ToArray());
            Complete(t, round.Matches); matches.AddRange(round.Matches); byes.Add(round.Bye!);
        }
        Assert.Equal(3, byes.Select(b => b.PlayerId).Distinct().Count());
        Assert.Contains("No quedan", Assert.Throws<TournamentConflictException>(() => Generate(t, p, 4, matches.ToArray(), byes.ToArray())).Message);
    }

    [Fact]
    public void TechnicalLimitsAndCancellationDoNotCreateAssignments()
    {
        var t = Start(513); var p = Players(513);
        Assert.Contains("512", Assert.Throws<TournamentConflictException>(() => Generate(t, p, 1, [], [])).Message);
        Assert.Equal(0, t.MatchesVersion);
        var small = Start(4); var players = Players(4);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => SwissRound.Generate(small, 1, players, [], [], players,
            small.OrganizerPlayerId, Now, cancelled.Token));
        Assert.Equal(0, small.MatchesVersion);
    }

    [Fact]
    public void RejectsOddWithoutByesInvalidDrawAndManualAdvanceWithPendingResults()
    {
        var t = Start(3, false); var p = Players(3);
        Assert.Throws<TournamentConflictException>(() => Generate(t, p, 1, [], []));
        var enabled = Start(3);
        Assert.Throws<TournamentRuleViolationException>(() => Generate(enabled, p, 1, [], [], [p[0], p[0], p[1]]));
        var first = Generate(enabled, p, 1, [], []);
        Assert.Throws<TournamentConflictException>(() => TournamentBye.Assign(enabled, 2, p[1], p, first.Matches.ToArray(), [first.Bye!], enabled.OrganizerPlayerId, Now));
        Assert.Throws<TournamentConflictException>(() => TournamentMatch.Create(enabled, 2, p[0], p[1], p, first.Matches.ToArray(), enabled.OrganizerPlayerId, Now, [first.Bye!]));
        Assert.Throws<TournamentConflictException>(() => TournamentMatch.Create(enabled, 3, p[0], p[1], p, [], enabled.OrganizerPlayerId, Now, []));
    }
}
