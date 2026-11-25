namespace TournamentManager.Domain.Players;

/// <summary>Perfil de jugador separado de las credenciales de su cuenta.</summary>
public sealed class Player
{
    private Player() { }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    public static Player Create(Guid accountId, string? displayName)
    {
        if (accountId == Guid.Empty)
            throw new PlayerRuleViolationException("El perfil requiere una cuenta válida.");

        var player = new Player { Id = Guid.NewGuid(), AccountId = accountId };
        player.Rename(displayName);
        return player;
    }

    public void Rename(string? displayName)
    {
        var name = (displayName ?? string.Empty).Trim();
        if (name.Length is < 2 or > 80)
            throw new PlayerRuleViolationException("El nombre del jugador debe tener entre 2 y 80 caracteres.");

        DisplayName = name;
    }
}
