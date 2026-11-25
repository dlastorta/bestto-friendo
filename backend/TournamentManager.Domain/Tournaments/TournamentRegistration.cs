using TournamentManager.Domain.Decks;

namespace TournamentManager.Domain.Tournaments;

public sealed class TournamentRegistration
{
    private TournamentRegistration() { }
    public Guid Id { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid PlayerId { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public TournamentDeckSnapshot? Snapshot { get; private set; }

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
        if (Snapshot is not null) throw new TournamentConflictException("Ya presentaste un mazo. El reemplazo no está disponible todavía.");
        if (deck.IsArchived || deck.Version != expectedDeckVersion)
            throw new TournamentConflictException("El mazo cambió o está archivado. Recargá tu biblioteca.");
        tournament.RecordParticipationChange();
        Snapshot = new TournamentDeckSnapshot(Id, deck, now);
    }
}

public sealed class TournamentDeckSnapshot
{
    private Guid[] _factionIds = [];
    private TournamentDeckSnapshot() { }
    internal TournamentDeckSnapshot(Guid registrationId, Deck deck, DateTimeOffset now)
    {
        RegistrationId = registrationId; SourceDeckId = deck.Id; SourceRevision = deck.CurrentRevisionNumber;
        GameId = deck.GameId; Name = deck.CurrentRevision.Name; ListText = deck.CurrentRevision.ListText;
        _factionIds = deck.CurrentRevision.FactionIds.ToArray(); PresentedAt = now;
    }
    public Guid RegistrationId { get; private set; }
    public Guid SourceDeckId { get; private set; }
    public int SourceRevision { get; private set; }
    public Guid GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ListText { get; private set; } = string.Empty;
    public IReadOnlyList<Guid> FactionIds => Array.AsReadOnly(_factionIds);
    public DateTimeOffset PresentedAt { get; private set; }
}
