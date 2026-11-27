namespace TournamentManager.Domain.Tournaments;

public sealed record Standing(Guid PlayerId, int Position, decimal Points, IReadOnlyList<TieBreakerValue> TieBreakers);

public static class TournamentStandings
{
    public static IReadOnlyList<Standing> Calculate(IEnumerable<Guid> participants, IEnumerable<TournamentMatch> matches,
        FrozenTournamentRules rules, IEnumerable<TournamentBye>? byes = null)
    {
        var totals = participants.Distinct().ToDictionary(id => id, _ => 0m);
        if (rules.TieBreakerFormulaVersion is not (0 or 1))
            throw new TournamentRuleViolationException("Versión de desempates no soportada.");
        var all = matches.Where(m => m.TournamentId == rules.TournamentId).ToArray();
        foreach (var bye in (byes ?? []).Where(b => b.TournamentId == rules.TournamentId))
        {
            if (rules.ByePolicyVersion != 1)
                throw new TournamentRuleViolationException("Política histórica de bye no soportada.");
            totals[bye.PlayerId] += rules.ByePoints;
        }
        foreach (var match in all)
        {
            if (match.CurrentResult is not { } result) continue;
            var points = result.Points(rules);
            totals[match.PlayerOneId] += points.One;
            totals[match.PlayerTwoId] += points.Two;
        }
        var opponents = totals.Keys.ToDictionary(id => id, _ => new List<Guid>());
        var played = totals.Keys.ToDictionary(id => id, _ => 0);
        var halfWins = totals.Keys.ToDictionary(id => id, _ => 0);
        foreach (var match in all.Where(m => m.CurrentResult?.Kind == "Played"))
        {
            var one = match.PlayerOneId; var two = match.PlayerTwoId;
            opponents[one].Add(two); opponents[two].Add(one);
            played[one]++; played[two]++;
            var outcome = match.CurrentResult!.Outcome;
            halfWins[one] += outcome == "Draw" ? 1 : outcome == "PlayerOneWin" ? 2 : 0;
            halfWins[two] += outcome == "Draw" ? 1 : outcome == "PlayerTwoWin" ? 2 : 0;
        }
        TieBreakerValue WinRate(Guid id)
        {
            var value = played[id] == 0 ? TieBreakerValue.Zero : new TieBreakerValue(halfWins[id], 2 * played[id]);
            var floor = new TieBreakerValue(1, 3);
            return value.CompareTo(floor) < 0 ? floor : value;
        }
        TieBreakerValue Value(Guid id, string code)
        {
            var rivals = opponents[id];
            if (rivals.Count == 0) return TieBreakerValue.Zero;
            return code switch
            {
                "Buchholz" => TieBreakerValue.FromPoints(rivals.Sum(r => totals[r])),
                "BuchholzCut" => TieBreakerValue.FromPoints(rivals.Sum(r => totals[r]) - rivals.Min(r => totals[r])),
                "OMWPercentage" => rivals.Select(WinRate).Aggregate(TieBreakerValue.Zero, (a, b) => a + b).Divide(rivals.Count),
                _ => throw new TournamentRuleViolationException("Criterio histórico desconocido.")
            };
        }
        var values = totals.Keys.ToDictionary(id => id, id => rules.TieBreakerFormulaVersion == 0
            ? Array.Empty<TieBreakerValue>() : rules.TieBreakers.Select(c => Value(id, c)).ToArray());
        int Compare(Guid a, Guid b)
        {
            var comparison = totals[b].CompareTo(totals[a]);
            for (var i = 0; comparison == 0 && i < values[a].Length; i++) comparison = values[b][i].CompareTo(values[a][i]);
            return comparison;
        }
        var sorted = totals.Keys.OrderBy(id => id, Comparer<Guid>.Create((a, b) =>
        {
            var comparison = Compare(a, b);
            return comparison != 0 ? comparison : a.CompareTo(b);
        })).ToArray();
        var rows = new List<Standing>();
        var position = 0;
        for (var i = 0; i < sorted.Length; i++)
        {
            if (i == 0 || Compare(sorted[i], sorted[i - 1]) != 0) position = i + 1;
            rows.Add(new(sorted[i], position, totals[sorted[i]], values[sorted[i]]));
        }
        return rows;
    }
}
