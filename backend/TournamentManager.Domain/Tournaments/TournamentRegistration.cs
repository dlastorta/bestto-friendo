using TournamentManager.Domain.Decks;

namespace TournamentManager.Domain.Tournaments;

public sealed class TournamentRegistration
{
    private TournamentRegistration() { }
    private readonly List<TournamentDeckSnapshot> _snapshots = [];
    public Guid Id { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid PlayerId { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public IReadOnlyCollection<TournamentDeckSnapshot> Snapshots => _snapshots.AsReadOnly();
    public TournamentDeckSnapshot? Snapshot => _snapshots.OrderByDescending(s => s.Number).FirstOrDefault();

    public static TournamentRegistration Create(Tournament tournament, Guid playerId, DateTimeOffset now)
    {
        if (playerId == Guid.Empty) throw new TournamentRuleViolationException("La inscripción requiere un jugador.");
        tournament.RecordParticipationChange();
        return new() { Id = Guid.NewGuid(), TournamentId = tournament.Id, PlayerId = playerId, RegisteredAt = now };
    }

    public void Present(Tournament tournament, Deck deck, int expectedDeckVersion, DateTimeOffset now)
    {
        if (tournament.Id != TournamentId || deck.GameId != tournament.GameId || deck.OwnerPlayerId != PlayerId)
            throw new TournamentRuleViolationException("Debés presentar un mazo propio del juego del torneo.");
        if (Snapshot is not null) throw new TournamentConflictException("Ya presentaste un mazo. Usá la acción de reemplazo antes del inicio.");
        if (deck.IsArchived || deck.Version != expectedDeckVersion)
            throw new TournamentConflictException("El mazo cambió o está archivado. Recargá tu biblioteca.");
        tournament.RecordParticipationChange();
        _snapshots.Add(new TournamentDeckSnapshot(Id, deck, 1, PlayerId, "Presentación inicial", now));
    }

    public void Replace(Tournament tournament, Deck deck, int expectedDeckVersion, int expectedPresentationVersion, string? reason, DateTimeOffset now)
    {
        if (tournament.Id != TournamentId || deck.GameId != tournament.GameId || deck.OwnerPlayerId != PlayerId)
            throw new TournamentRuleViolationException("Debés presentar un mazo propio del juego del torneo.");
        if (Snapshot is null || Snapshot.Number != expectedPresentationVersion)
            throw new TournamentConflictException("La presentación cambió o no existe. Recargá antes de reemplazar.");
        if (deck.IsArchived || deck.Version != expectedDeckVersion)
            throw new TournamentConflictException("El mazo cambió o está archivado. Recargá tu biblioteca.");
        var normalizedReason = (reason ?? "").Trim();
        if (normalizedReason.Length is < 1 or > 500)
            throw new TournamentRuleViolationException("Indicá un motivo de entre 1 y 500 caracteres.");
        tournament.RecordParticipationChange();
        _snapshots.Add(new TournamentDeckSnapshot(Id, deck, Snapshot.Number + 1, PlayerId, normalizedReason, now));
    }
}

public sealed class TournamentDeckSnapshot
{
    private Guid[] _factionIds = [];
    private TournamentDeckSnapshot() { }
    internal TournamentDeckSnapshot(Guid registrationId, Deck deck, int number, Guid actorPlayerId, string reason, DateTimeOffset now)
    {
        RegistrationId = registrationId; SourceDeckId = deck.Id; SourceRevision = deck.CurrentRevisionNumber;
        GameId = deck.GameId; Name = deck.CurrentRevision.Name; ListText = deck.CurrentRevision.ListText;
        _factionIds = deck.CurrentRevision.FactionIds.ToArray(); PresentedAt = now;
        Number = number; ActorPlayerId = actorPlayerId; Reason = reason;
    }
    public Guid RegistrationId { get; private set; }
    public Guid SourceDeckId { get; private set; }
    public int SourceRevision { get; private set; }
    public Guid GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ListText { get; private set; } = string.Empty;
    public IReadOnlyList<Guid> FactionIds => Array.AsReadOnly(_factionIds);
    public DateTimeOffset PresentedAt { get; private set; }
    public int Number { get; private set; }
    public Guid ActorPlayerId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
}
