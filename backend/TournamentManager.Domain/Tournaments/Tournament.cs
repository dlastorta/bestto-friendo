namespace TournamentManager.Domain.Tournaments;

public sealed class Tournament
{
    private Tournament() { }

    public Guid Id { get; private set; }
    public Guid OrganizerPlayerId { get; private set; }
    public Guid GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TournamentStatus Status { get; private set; }

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

public enum TournamentStatus { Draft }

public sealed class TournamentRuleViolationException(string message) : Exception(message);
