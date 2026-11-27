namespace TournamentManager.Domain.Tournaments;

public sealed class Tournament
{
    private readonly List<TournamentLifecycleEvent> _lifecycle = [];
    private Tournament() { }

    public Guid Id { get; private set; }
    public Guid OrganizerPlayerId { get; private set; }
    public Guid GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TournamentStatus Status { get; private set; }
    public int RulesVersion { get; private set; }
    public TournamentRules? Rules { get; private set; }
    public Guid? RegistrationToken { get; private set; }
    public int ParticipationVersion { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public FrozenTournamentRules? StartedRules { get; private set; }
    public int MatchesVersion { get; private set; }
    public int ResultsVersion { get; private set; }
    public int LifecycleVersion { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public IReadOnlyCollection<TournamentLifecycleEvent> Lifecycle => _lifecycle.AsReadOnly();

    public void Finish(IReadOnlyCollection<Guid> participants, IReadOnlyCollection<TournamentMatch> matches,
        IReadOnlyCollection<TournamentBye> byes, int expectedLifecycle, int expectedMatches, int expectedResults,
        Guid actor, DateTimeOffset now)
    {
        if (Status is not (TournamentStatus.InProgress or TournamentStatus.Correction) || StartedRules is null)
            throw new TournamentConflictException("Solo se puede finalizar un torneo en curso o reabierto para correcciones.");
        EnsureVersions(expectedLifecycle, expectedMatches, expectedResults);
        if (matches.Any(m => m.TournamentId != Id) || byes.Any(b => b.TournamentId != Id))
            throw new TournamentRuleViolationException("Las asignaciones no pertenecen al torneo.");
        var active = matches.Where(m => !m.IsSuperseded).ToArray();
        var activeByes = byes.Where(b => !b.IsSuperseded).ToArray();
        if (active.Length == 0) throw new TournamentConflictException("Se requiere al menos una ronda completa para finalizar.");
        RoundProgress.EnsureComplete(participants, active, activeByes);
        Status = TournamentStatus.Finished;
        FinishedAt = now;
        LifecycleVersion++;
        _lifecycle.Add(new(Id, LifecycleVersion, "Finished", "Cierre confirmado", actor, now, active, activeByes));
    }

    public void Reopen(string? reason, int expectedLifecycle, Guid actor, DateTimeOffset now)
    {
        if (Status != TournamentStatus.Finished || LifecycleVersion != expectedLifecycle)
            throw new TournamentConflictException("El estado del torneo cambió. Recargá antes de reabrir.");
        var note = (reason ?? "").Trim();
        if (note.Length is < 1 or > 500) throw new TournamentRuleViolationException("Indicá un motivo de entre 1 y 500 caracteres.");
        Status = TournamentStatus.Correction;
        LifecycleVersion++;
        _lifecycle.Add(new(Id, LifecycleVersion, "Reopened", note, actor, now, [], []));
    }

    public void EnsureVersions(int lifecycle, int matches, int results)
    {
        if (LifecycleVersion != lifecycle || MatchesVersion != matches || ResultsVersion != results)
            throw new TournamentConflictException("El torneo o sus resultados cambiaron. Recargá antes de continuar.");
    }

    internal void RecordResultChange() => ResultsVersion++;

    public void RecordMatchCreation()
    {
        if (Status != TournamentStatus.InProgress)
            throw new TournamentConflictException("El torneo debe estar en curso.");
        MatchesVersion++;
    }

    public void Start(int participantCount, int presentedCount, int expectedRulesVersion, int expectedParticipationVersion, DateTimeOffset now)
    {
        EnsureDraft();
        if (RulesVersion != expectedRulesVersion || ParticipationVersion != expectedParticipationVersion)
            throw new TournamentConflictException("El torneo cambió. Recargá antes de iniciar.");
        if (Rules is null) throw new TournamentRuleViolationException("Configurá las reglas antes de iniciar.");
        if (participantCount < 2) throw new TournamentRuleViolationException("Se necesitan al menos dos jugadores para iniciar.");
        if (presentedCount != participantCount) throw new TournamentRuleViolationException("Todos los jugadores deben presentar un mazo antes de iniciar.");
        StartedRules = FrozenTournamentRules.Capture(Id, Rules, RulesVersion);
        StartedAt = now;
        Status = TournamentStatus.InProgress;
        ParticipationVersion++;
    }

    public Guid EnableRegistrationLink()
    {
        EnsureDraft();
        RegistrationToken ??= Guid.NewGuid();
        return RegistrationToken.Value;
    }

    public void RecordParticipationChange()
    {
        EnsureDraft();
        ParticipationVersion++;
    }

    private void EnsureDraft()
    {
        if (Status != TournamentStatus.Draft)
            throw new TournamentConflictException("Las inscripciones y presentaciones solo están disponibles antes del inicio.");
    }

    public void ConfigureRules(decimal win, decimal draw, decimal loss, bool byesEnabled, decimal bye,
        IEnumerable<string>? tieBreakers, int expectedVersion)
    {
        if (Status != TournamentStatus.Draft)
            throw new TournamentConflictException("Solo se pueden modificar reglas en borrador.");
        if (RulesVersion != expectedVersion)
            throw new TournamentConflictException("Las reglas cambiaron. Recargá antes de guardar.");
        var rules = TournamentRules.Create(Id, win, draw, loss, byesEnabled, bye, tieBreakers);
        if (Rules is null) Rules = rules;
        else Rules.Apply(rules);
        RulesVersion++;
    }

    public static Tournament CreateDraft(Guid organizerPlayerId, Guid gameId, string? name)
    {
        if (organizerPlayerId == Guid.Empty)
            throw new TournamentRuleViolationException("El torneo requiere un organizador.");
        if (gameId == Guid.Empty)
            throw new TournamentRuleViolationException("Debés elegir un juego.");
        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length is < 2 or > 120)
            throw new TournamentRuleViolationException("El nombre del torneo debe tener entre 2 y 120 caracteres.");

        return new Tournament
        {
            Id = Guid.NewGuid(), OrganizerPlayerId = organizerPlayerId,
            GameId = gameId, Name = normalizedName, Status = TournamentStatus.Draft
        };
    }
}

public enum TournamentStatus { Draft, InProgress, Finished, Correction }

public sealed class TournamentRuleViolationException(string message) : Exception(message);
