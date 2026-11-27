namespace TournamentManager.Domain.Tournaments;

public sealed class TournamentMatch
{
    private readonly List<MatchResultRevision> _results = [];
    private TournamentMatch() { }
    public Guid Id { get; private set; }
    public Guid TournamentId { get; private set; }
    public int Round { get; private set; }
    public Guid PlayerOneId { get; private set; }
    public Guid PlayerTwoId { get; private set; }
    public Guid CreatedByPlayerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<MatchResultRevision> Results => _results.AsReadOnly();
    public MatchResultRevision? CurrentResult => _results.MaxBy(r => r.Number);

    public static TournamentMatch Create(Tournament tournament, int round, Guid one, Guid two,
        IReadOnlyCollection<Guid> participants, IReadOnlyCollection<TournamentMatch> existing, Guid actor, DateTimeOffset now,
        IReadOnlyCollection<TournamentBye> byes)
    {
        EnsureInProgress(tournament);
        if (tournament.StartedRules!.PairingAlgorithmVersion == 1)
        {
            RoundProgress.EnsureCanAssign(round, participants, existing, byes);
            if (existing.Any(m => m.TournamentId == tournament.Id &&
                ((m.PlayerOneId == one && m.PlayerTwoId == two) || (m.PlayerOneId == two && m.PlayerTwoId == one))))
                throw new TournamentConflictException("Estos jugadores ya se enfrentaron en el torneo.");
        }
        if (byes.Any(b => b.TournamentId == tournament.Id && b.Round == round && (b.PlayerId == one || b.PlayerId == two)))
            throw new TournamentConflictException("Un jugador ya tiene un bye en esa ronda.");
        if (round < 1) throw new TournamentRuleViolationException("Indicá una ronda mayor que cero.");
        if (one == two || !participants.Contains(one) || !participants.Contains(two))
            throw new TournamentRuleViolationException("Elegí dos jugadores distintos inscriptos en este torneo.");
        if (existing.Any(m => m.TournamentId == tournament.Id && m.Round == round &&
            (m.PlayerOneId == one || m.PlayerTwoId == one || m.PlayerOneId == two || m.PlayerTwoId == two)))
            throw new TournamentConflictException("Un jugador ya tiene una partida en esa ronda.");
        tournament.RecordMatchCreation();
        return new() { Id = Guid.NewGuid(), TournamentId = tournament.Id, Round = round,
            PlayerOneId = one, PlayerTwoId = two, CreatedByPlayerId = actor, CreatedAt = now };
    }

    public void RecordResult(Tournament tournament, string? outcome, string? kind, string? reason,
        int expectedVersion, Guid actor, DateTimeOffset now)
    {
        EnsureInProgress(tournament);
        if (tournament.Id != TournamentId) throw new TournamentRuleViolationException("La partida no pertenece al torneo.");
        if (Version != expectedVersion) throw new TournamentConflictException("El resultado cambió. Recargá antes de guardar.");
        if (outcome is not ("PlayerOneWin" or "PlayerTwoWin" or "Draw") || kind is not ("Played" or "Forfeit"))
            throw new TournamentRuleViolationException("Indicá un resultado y motivo de partida válidos.");
        if (kind == "Forfeit" && outcome == "Draw")
            throw new TournamentRuleViolationException("Un forfeit requiere un ganador.");
        var note = (reason ?? "").Trim();
        if (note.Length > 500 || (Version > 0 && note.Length == 0))
            throw new TournamentRuleViolationException("Para corregir indicá un motivo de entre 1 y 500 caracteres.");
        Version++;
        tournament.RecordResultChange();
        _results.Add(new(Id, Version, outcome, kind, note.Length == 0 ? "Carga inicial" : note, actor, now));
    }

    private static void EnsureInProgress(Tournament tournament)
    {
        if (tournament.Status != TournamentStatus.InProgress || tournament.StartedRules is null)
            throw new TournamentConflictException("Las partidas requieren un torneo en curso con reglas históricas.");
    }
}

public sealed class MatchResultRevision
{
    private MatchResultRevision() { }
    internal MatchResultRevision(Guid matchId, int number, string outcome, string kind, string reason, Guid actor, DateTimeOffset now)
    { MatchId = matchId; Number = number; Outcome = outcome; Kind = kind; Reason = reason; ActorPlayerId = actor; RecordedAt = now; }
    public Guid MatchId { get; private set; }
    public int Number { get; private set; }
    public string Outcome { get; private set; } = "";
    public string Kind { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public Guid ActorPlayerId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    // Semántica v1: forfeit otorga victoria/derrota normales. No usa reglas vivas ni totales mutables.
    public (decimal One, decimal Two) Points(FrozenTournamentRules rules) => Outcome switch
    {
        "PlayerOneWin" => (rules.WinPoints, rules.LossPoints),
        "PlayerTwoWin" => (rules.LossPoints, rules.WinPoints),
        "Draw" => (rules.DrawPoints, rules.DrawPoints),
        _ => throw new TournamentRuleViolationException("Resultado histórico desconocido.")
    };
}
