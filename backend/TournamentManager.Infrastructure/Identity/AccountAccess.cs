using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Players;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Identity;

internal sealed class AccountAccess(
    UserManager<IdentityUser<Guid>> users,
    SignInManager<IdentityUser<Guid>> signIn,
    TournamentDbContext context) : IAccountAccess
{
    public async Task<AccountResult> RegisterAsync(string email, string password, Player player, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var account = new IdentityUser<Guid> { Id = player.AccountId, UserName = email, Email = email };
        try
        {
            var result = await users.CreateAsync(account, password);
            if (!result.Succeeded)
                return new(null, result.Errors.GroupBy(error => error.Code)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()),
                    result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"));

            context.Players.Add(player);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(account.Id, new Dictionary<string, string[]>());
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(null, new Dictionary<string, string[]> { ["email"] = ["Ya existe una cuenta con ese email."] }, true);
        }
    }

    public async Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var account = await users.FindByEmailAsync(email.Trim());
        if (account is null) return null;
        var result = await signIn.CheckPasswordSignInAsync(account, password, lockoutOnFailure: true);
        return result.Succeeded ? new(account.Id, await users.IsInRoleAsync(account, "CatalogAdmin"),
            await users.IsInRoleAsync(account, "TournamentOrganizer")) : null;
    }

    public async Task<AuthenticatedAccount?> FindAccountAsync(Guid accountId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var account = await users.FindByIdAsync(accountId.ToString());
        if (account is null || await users.IsLockedOutAsync(account)) return null;
        return new(account.Id, await users.IsInRoleAsync(account, "CatalogAdmin"),
            await users.IsInRoleAsync(account, "TournamentOrganizer"));
    }
}

internal sealed class PlayerProfiles(TournamentDbContext context) : IPlayerProfiles
{
    public Task<Player?> FindByAccountAsync(Guid accountId, CancellationToken ct) =>
        context.Players.SingleOrDefaultAsync(player => player.AccountId == accountId, ct);

    public async Task SaveAsync(Player player, CancellationToken ct) => await context.SaveChangesAsync(ct);
}
