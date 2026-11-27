using System.Numerics;

namespace TournamentManager.Domain.Tournaments;

public sealed class SwissRound
{
    private Guid[] _participantOrder = [];
    private SwissRound() { }
    public Guid TournamentId { get; private set; }
    public int Number { get; private set; }
    public int AlgorithmVersion { get; private set; }
    public Guid ActorPlayerId { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }
    public int SourceMatchesVersion { get; private set; }
    public int SourceResultsVersion { get; private set; }
    public IReadOnlyList<Guid> ParticipantOrder => Array.AsReadOnly(_participantOrder);

    public static GeneratedRound Generate(Tournament tournament, int expectedRound, IReadOnlyCollection<Guid> participants,
        IReadOnlyCollection<TournamentMatch> matches, IReadOnlyCollection<TournamentBye> byes,
        IReadOnlyList<Guid> firstRoundDraw, Guid actor, DateTimeOffset now, CancellationToken ct = default)
    {
        if (tournament.Status != TournamentStatus.InProgress || tournament.StartedRules is not { } rules)
            throw new TournamentConflictException("La generación requiere un torneo en curso.");
        if (rules.PairingAlgorithmVersion != 1)
            throw new TournamentConflictException("Este torneo conserva emparejamientos manuales históricos.");
        if (matches.Any(m => m.TournamentId != tournament.Id) || byes.Any(b => b.TournamentId != tournament.Id))
            throw new TournamentRuleViolationException("Las asignaciones deben pertenecer al torneo.");
        var number = RoundProgress.NextRound(matches, byes);
        if (expectedRound != number) throw new TournamentConflictException("Las rondas cambiaron. Recargá antes de generar.");
        if (participants.Count < 2 || participants.Distinct().Count() != participants.Count)
            throw new TournamentRuleViolationException("Se necesitan al menos dos participantes distintos.");
        if (participants.Count > 512)
            throw new TournamentConflictException("El generador actual admite hasta 512 participantes. No se guardó ninguna asignación.");
        RoundProgress.EnsureCanAssign(number, participants, matches, byes);
        Guid[] order;
        if (number == 1)
        {
            if (firstRoundDraw.Count != participants.Count || !firstRoundDraw.ToHashSet().SetEquals(participants))
                throw new TournamentRuleViolationException("El sorteo debe incluir a todos los participantes una sola vez.");
            order = firstRoundDraw.ToArray();
        }
        else
            order = TournamentStandings.Calculate(participants, matches, rules, byes).Reverse().Select(r => r.PlayerId).ToArray();

        var forbidden = matches.SelectMany(m => new[] { (m.PlayerOneId, m.PlayerTwoId), (m.PlayerTwoId, m.PlayerOneId) }).ToHashSet();
        var usedByes = byes.Select(b => b.PlayerId).ToHashSet();
        Guid?[] candidates = participants.Count % 2 == 0 ? [null] : order.Where(p => !usedByes.Contains(p)).Select(p => (Guid?)p).ToArray();
        if (participants.Count % 2 != 0 && (!rules.ByesEnabled || rules.ByePolicyVersion != 1))
            throw new TournamentConflictException("Hay cantidad impar de jugadores y las reglas históricas no permiten asignar un bye.");
        if (candidates.Length == 0) throw new TournamentConflictException("No quedan jugadores elegibles para recibir un bye.");
        var nodes = 0;
        var bits = order.Select((id, index) => (id, bit: BigInteger.One << index)).ToDictionary(x => x.id, x => x.bit);
        var failed = new HashSet<BigInteger>();
        List<(Guid One, Guid Two)>? Pair(Guid[] remaining)
        {
            ct.ThrowIfCancellationRequested();
            if (++nodes > 100000)
                throw new TournamentConflictException("Se alcanzó el límite técnico de búsqueda; no se confirmó que sea imposible. No se guardó ninguna asignación.");
            if (remaining.Length == 0) return [];
            var key = remaining.Aggregate(BigInteger.Zero, (mask, id) => mask | bits[id]);
            if (failed.Contains(key)) return null;
            // Menor puntaje/resistencia primero; intentar vecinos antes de subir en la tabla.
            for (var i = 1; i < remaining.Length; i++)
            {
                if (forbidden.Contains((remaining[0], remaining[i]))) continue;
                var tail = Pair(remaining.Where((_, index) => index != 0 && index != i).ToArray());
                if (tail is not null) { tail.Insert(0, (remaining[0], remaining[i])); return tail; }
            }
            failed.Add(key);
            return null;
        }
        foreach (var candidate in candidates)
        {
            var pairs = Pair(order.Where(p => p != candidate).ToArray());
            if (pairs is null) continue;
            var audit = new SwissRound { TournamentId = tournament.Id, Number = number, AlgorithmVersion = 1,
                ActorPlayerId = actor, GeneratedAt = now, SourceMatchesVersion = tournament.MatchesVersion,
                SourceResultsVersion = tournament.ResultsVersion, _participantOrder = order };
            TournamentBye? bye = candidate is { } player ? TournamentBye.Assign(tournament, number, player,
                participants, matches, byes, actor, now) : null;
            var allByes = bye is null ? byes : byes.Append(bye).ToArray();
            var created = new List<TournamentMatch>();
            foreach (var pair in pairs)
                created.Add(TournamentMatch.Create(tournament, number, pair.One, pair.Two, participants,
                    matches.Concat(created).ToArray(), actor, now, allByes));
            return new(audit, created, bye);
        }
        throw new TournamentConflictException("No existe un emparejamiento completo sin repetir rivales y respetando los byes disponibles.");
    }
}

public sealed record GeneratedRound(SwissRound Audit, IReadOnlyList<TournamentMatch> Matches, TournamentBye? Bye);
