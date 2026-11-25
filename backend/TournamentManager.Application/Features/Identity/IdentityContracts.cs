using TournamentManager.Domain.Players;

namespace TournamentManager.Application.Features.Identity;

public sealed record PlayerProfile(Guid Id, string DisplayName, bool CanManageCatalog, bool CanOrganizeTournaments = false);
public sealed record AuthenticatedAccount(Guid Id, bool CanManageCatalog, bool CanOrganizeTournaments = false);
public sealed record AccountResult(Guid? AccountId, IReadOnlyDictionary<string, string[]> Errors, bool Conflict = false)
{
    public bool Succeeded => AccountId.HasValue;
}

public interface IAccountAccess
{
    Task<AccountResult> RegisterAsync(string email, string password, Player player, CancellationToken ct);
    Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct);
    Task<AuthenticatedAccount?> FindAccountAsync(Guid accountId, CancellationToken ct);
}

public interface ICurrentAccount
{
    Guid? AccountId { get; }
    bool CanManageCatalog { get; }
    bool CanOrganizeTournaments { get; }
}

public interface IPlayerProfiles
{
    Task<Player?> FindByAccountAsync(Guid accountId, CancellationToken ct);
    Task SaveAsync(Player player, CancellationToken ct);
}

public sealed record RegisterAccount(string? Email, string? Password, string? DisplayName);
public sealed record LoginAccount(string? Email, string? Password);
public sealed record RenamePlayer(string? DisplayName);

public sealed class RegisterAccountHandler(IAccountAccess accounts)
{
    public Task<AccountResult> HandleAsync(RegisterAccount command, CancellationToken ct)
    {
        var player = Player.Create(Guid.NewGuid(), command.DisplayName);
        var email = command.Email?.Trim() ?? string.Empty;
        if (email.Length is < 3 or > 254 || command.Password is null || command.Password.Length is < 12 or > 128)
            return Task.FromResult(new AccountResult(null, new Dictionary<string, string[]>
            {
                ["account"] = ["Ingresá un email válido y una contraseña de entre 12 y 128 caracteres."]
            }));
        return accounts.RegisterAsync(email, command.Password, player, ct);
    }
}

public sealed class PlayerProfileHandler(ICurrentAccount currentAccount, IPlayerProfiles profiles)
{
    private Guid AccountId => currentAccount.AccountId ?? throw new UnauthorizedAccessException();

    public async Task<PlayerProfile?> GetAsync(CancellationToken ct)
    {
        var player = await profiles.FindByAccountAsync(AccountId, ct);
        return player is null ? null : new(player.Id, player.DisplayName, currentAccount.CanManageCatalog, currentAccount.CanOrganizeTournaments);
    }

    public async Task<PlayerProfile?> RenameAsync(RenamePlayer command, CancellationToken ct)
    {
        var player = await profiles.FindByAccountAsync(AccountId, ct);
        if (player is null) return null;
        player.Rename(command.DisplayName);
        await profiles.SaveAsync(player, ct);
        return new(player.Id, player.DisplayName, currentAccount.CanManageCatalog, currentAccount.CanOrganizeTournaments);
    }
}
