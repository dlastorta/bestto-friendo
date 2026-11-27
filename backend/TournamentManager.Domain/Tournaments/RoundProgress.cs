namespace TournamentManager.Domain.Tournaments;

public static class RoundProgress
{
    public static int NextRound(IEnumerable<TournamentMatch> matches, IEnumerable<TournamentBye> byes)
    {
        var last = matches.Select(m => m.Round).Concat(byes.Select(b => b.Round)).DefaultIfEmpty(0).Max();
        if (last == int.MaxValue) throw new TournamentConflictException("Se alcanzó el límite de numeración de rondas.");
        return last + 1;
    }

    public static void EnsureCanAssign(int round, IReadOnlyCollection<Guid> participants,
        IReadOnlyCollection<TournamentMatch> matches, IReadOnlyCollection<TournamentBye> byes)
    {
        if (round < 1) throw new TournamentRuleViolationException("Indicá una ronda positiva.");
        var rounds = matches.Select(m => m.Round).Concat(byes.Select(b => b.Round)).Distinct().Order().ToArray();
        if (rounds.Any(r => r > round) || rounds.Count(r => r < round) != round - 1)
            throw new TournamentConflictException("Completá las rondas anteriores sin saltar números.");
        EnsureComplete(participants, matches.Where(m => m.Round < round).ToArray(),
            byes.Where(b => b.Round < round).ToArray());
    }

    public static void EnsureComplete(IReadOnlyCollection<Guid> participants,
        IReadOnlyCollection<TournamentMatch> matches, IReadOnlyCollection<TournamentBye> byes)
    {
        var rounds = matches.Select(m => m.Round).Concat(byes.Select(b => b.Round)).Distinct();
        foreach (var previous in rounds)
        {
            var games = matches.Where(m => m.Round == previous).ToArray();
            var occupied = games.SelectMany(m => new[] { m.PlayerOneId, m.PlayerTwoId })
                .Concat(byes.Where(b => b.Round == previous).Select(b => b.PlayerId)).ToArray();
            if (games.Any(m => m.CurrentResult is null) || occupied.Length != participants.Count ||
                occupied.Distinct().Count() != participants.Count || !occupied.ToHashSet().SetEquals(participants))
                throw new TournamentConflictException($"La ronda {previous} debe tener a todos los jugadores asignados y todos los resultados cargados.");
        }
    }
}
