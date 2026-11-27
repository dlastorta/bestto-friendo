using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class TournamentLifecycleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-27T12:00:00Z");
    private static (Tournament Tournament, Guid[] Players, GeneratedRound Round) Setup()
    {
        var players = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Cierre");
        t.ConfigureRules(3, 1, 0, true, 3, [], 0);
        t.Start(3, 3, 1, 0, Now);
        return (t, players, SwissRound.Generate(t, 1, players, [], [], players, t.OrganizerPlayerId, Now));
    }
    private static void Finish(Tournament t, Guid[] players, GeneratedRound round) =>
        t.Finish(players, round.Matches, [round.Bye!], t.LifecycleVersion, t.MatchesVersion, t.ResultsVersion, t.OrganizerPlayerId, Now);

    [Fact]
    public void ClosingRequiresCompletedResultsAndReopeningPreservesEveryClosure()
    {
        var (t, players, round) = Setup(); var match = Assert.Single(round.Matches);
        Assert.Throws<TournamentConflictException>(() => Finish(t, players, round));
        match.RecordResult(t, "PlayerOneWin", "Played", null, 0, t.OrganizerPlayerId, Now);
        Finish(t, players, round);
        Assert.Equal(TournamentStatus.Finished, t.Status);
        Assert.Equal(new[] { 1 }, Assert.Single(t.Lifecycle).ResultNumbers);
        Assert.Throws<TournamentConflictException>(() => match.RecordResult(t, "Draw", "Played", "Error", 1, t.OrganizerPlayerId, Now));
        Assert.Throws<TournamentRuleViolationException>(() => t.Reopen(" ", 1, t.OrganizerPlayerId, Now));
        t.Reopen("Error de carga", 1, t.OrganizerPlayerId, Now);
        match.RecordResult(t, "Draw", "Played", "Resultado correcto", 1, t.OrganizerPlayerId, Now);
        Assert.Throws<TournamentConflictException>(() => SwissRound.Generate(t, 2, players, round.Matches, [round.Bye!], players, t.OrganizerPlayerId, Now));
        Assert.Throws<TournamentConflictException>(() => SwissRound.Regenerate(t, 1, t.MatchesVersion, t.ResultsVersion, t.LifecycleVersion,
            "Corregir", players, round.Matches, [round.Bye!], [round.Audit], players, t.OrganizerPlayerId, Now));
        Finish(t, players, round);
        Assert.Equal(new[] { 1 }, t.Lifecycle.First().ResultNumbers);
        Assert.Equal(new[] { 2 }, t.Lifecycle.Last().ResultNumbers);
        Assert.Equal(3, t.Lifecycle.Count);
    }

    [Fact]
    public void RegenerationAnnulsAssignmentsButPreservesResultsAndRemovesTheirPoints()
    {
        var (t, players, old) = Setup(); var match = Assert.Single(old.Matches);
        match.RecordResult(t, "PlayerOneWin", "Played", null, 0, t.OrganizerPlayerId, Now);
        var replacement = SwissRound.Regenerate(t, 1, t.MatchesVersion, t.ResultsVersion, 0, "Sorteo corregido",
            players, old.Matches, [old.Bye!], [old.Audit], players.Reverse().ToArray(), t.OrganizerPlayerId, Now);
        Assert.True(old.Audit.IsSuperseded); Assert.True(match.IsSuperseded); Assert.True(old.Bye!.IsSuperseded);
        Assert.Single(match.Results); Assert.Equal(2, replacement.Audit.Revision);
        Assert.Null(Assert.Single(replacement.Matches).CurrentResult);
        Assert.Throws<TournamentConflictException>(() => match.RecordResult(t, "Draw", "Played", "Vieja", 1, t.OrganizerPlayerId, Now));
        var table = TournamentStandings.Calculate(players, old.Matches.Concat(replacement.Matches).ToArray(), t.StartedRules!, [old.Bye, replacement.Bye!]);
        Assert.Equal(3m, table.Sum(row => row.Points));
    }

    [Fact]
    public void StaleClosureCannotChangeState()
    {
        var (t, players, round) = Setup();
        Assert.Single(round.Matches).RecordResult(t, "Draw", "Played", null, 0, t.OrganizerPlayerId, Now);
        Assert.Throws<TournamentConflictException>(() => t.Finish(players, round.Matches, [round.Bye!], 0, t.MatchesVersion, 0, t.OrganizerPlayerId, Now));
        Assert.Equal(TournamentStatus.InProgress, t.Status); Assert.Empty(t.Lifecycle);
    }
}
