namespace TournamentManager.Domain.Games;

public sealed class GameFaction
{
    // Constructor privado requerido para materialización por EF Core (solo en infraestructura).
    private GameFaction() { }

    internal GameFaction(Guid gameId, string name)
    {
        Id = Guid.NewGuid();
        GameId = gameId;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
    }

    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}
