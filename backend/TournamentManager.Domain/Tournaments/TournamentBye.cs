namespace TournamentManager.Domain.Tournaments;

public sealed class TournamentBye
{
    private TournamentBye() { }
    public Guid Id { get; private set; }
    public Guid TournamentId { get; private set; }
    public int Round { get; private set; }
    public Guid PlayerId { get; private set; }
    public Guid ActorPlayerId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public int RoundRevision { get; private set; } = 1;
    public bool IsSuperseded { get; private set; }
    internal void Supersede() => IsSuperseded = true;
    internal void SetRoundRevision(int revision) => RoundRevision = revision;

    public static TournamentBye Assign(Tournament tournament, int round, Guid player,
        IReadOnlyCollection<Guid> participants, IReadOnlyCollection<TournamentMatch> matches,
        IReadOnlyCollection<TournamentBye> byes, Guid actor, DateTimeOffset now)
    {
        if (tournament.Status != TournamentStatus.InProgress || tournament.StartedRules is not { } rules)
            throw new TournamentConflictException("El bye requiere un torneo en curso.");
        if (rules.ByePolicyVersion != 1)
            throw new TournamentConflictException("Este torneo conserva reglas históricas sin asignación de byes.");
        if (!rules.ByesEnabled)
            throw new TournamentRuleViolationException("Los byes están deshabilitados en las reglas históricas.");
        if (round < 1 || !participants.Contains(player))
            throw new TournamentRuleViolationException("Indicá una ronda positiva y un jugador inscripto.");
        if (rules.PairingAlgorithmVersion == 1)
            RoundProgress.EnsureCanAssign(round, participants, matches, byes);
        if (participants.Distinct().Count() % 2 == 0)
            throw new TournamentRuleViolationException("El bye requiere una cantidad impar de inscriptos.");
        var existing = byes.Where(b => b.TournamentId == tournament.Id).ToArray();
        if (existing.Any(b => b.Round == round) || existing.Count(b => b.PlayerId == player) >= rules.MaximumByesPerParticipant)
            throw new TournamentConflictException("La ronda o el jugador ya tiene un bye.");
        if (matches.Any(m => m.TournamentId == tournament.Id && m.Round == round &&
            (m.PlayerOneId == player || m.PlayerTwoId == player)))
            throw new TournamentConflictException("El jugador ya tiene una partida en esa ronda.");
        tournament.RecordMatchCreation();
        return new() { Id = Guid.NewGuid(), TournamentId = tournament.Id, Round = round,
            PlayerId = player, ActorPlayerId = actor, AssignedAt = now };
    }
}
