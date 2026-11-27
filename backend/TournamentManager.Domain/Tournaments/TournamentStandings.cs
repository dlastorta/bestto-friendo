namespace TournamentManager.Domain.Tournaments;

public sealed record Standing(Guid PlayerId, int Position, decimal Points);

public static class TournamentStandings
{
    public static IReadOnlyList<Standing> Calculate(IEnumerable<Guid> participants, IEnumerable<TournamentMatch> matches,
        FrozenTournamentRules rules)
    {
        var totals = participants.Distinct().ToDictionary(id => id, _ => 0m);
        foreach (var match in matches.Where(m => m.TournamentId == rules.TournamentId))
        {
            if (match.CurrentResult is not { } result) continue;
            var points = result.Points(rules);
            totals[match.PlayerOneId] += points.One;
            totals[match.PlayerTwoId] += points.Two;
        }
        var sorted = totals.OrderByDescending(p => p.Value).ThenBy(p => p.Key).ToArray();
        var rows = new List<Standing>();
        var position = 0;
        for (var i = 0; i < sorted.Length; i++)
        {
            if (i == 0 || sorted[i].Value != sorted[i - 1].Value) position = i + 1;
            rows.Add(new(sorted[i].Key, position, sorted[i].Value));
        }
        return rows;
    }
}
