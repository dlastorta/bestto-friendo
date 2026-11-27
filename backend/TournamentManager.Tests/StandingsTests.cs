using TournamentManager.Domain.Tournaments;
using Xunit;

namespace TournamentManager.Tests;

public sealed class StandingsTests
{
    [Fact]
    public void RebuildsFromCurrentResultsWithSharedRanksAndZeroForPendingPlayers()
    {
        var t = Tournament.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Tabla");
        t.ConfigureRules(3.5m, 1.5m, 0, true, 3, [], 0);
        t.Start(4, 4, 1, 0, DateTimeOffset.UtcNow);
        Guid[] players = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        var a = TournamentMatch.Create(t, 1, players[0], players[1], players, [], t.OrganizerPlayerId, DateTimeOffset.UtcNow, []);
        var b = TournamentMatch.Create(t, 1, players[2], players[3], players, [a], t.OrganizerPlayerId, DateTimeOffset.UtcNow, []);
        a.RecordResult(t, "PlayerOneWin", "Forfeit", null, 0, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        var initial = TournamentStandings.Calculate(players, [a, b], t.StartedRules!);
        Assert.Equal(3.5m, initial.Single(r => r.PlayerId == players[0]).Points);
        Assert.Equal(3, initial.Count(r => r.Points == 0 && r.Position == 2));
        a.RecordResult(t, "Draw", "Played", "Corrección", 1, t.OrganizerPlayerId, DateTimeOffset.UtcNow);
        var corrected = TournamentStandings.Calculate(players, [a, b], t.StartedRules!);
        Assert.Equal(2, corrected.Count(r => r.Points == 1.5m && r.Position == 1));
        Assert.Equal(2, corrected.Count(r => r.Points == 0 && r.Position == 3));
        Assert.Equal(3m, corrected.Sum(r => r.Points));
    }
}
