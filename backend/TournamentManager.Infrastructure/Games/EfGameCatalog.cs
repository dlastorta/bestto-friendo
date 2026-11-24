using Microsoft.EntityFrameworkCore;
using Npgsql;
using TournamentManager.Application.Features.Games;
using TournamentManager.Domain.Games;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Games;

internal sealed class EfGameCatalog(TournamentDbContext db) : IGameCatalog
{
    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
        db.Games.AnyAsync(game => game.NormalizedName == normalizedName, cancellationToken);

    public async Task AddAsync(Game game, CancellationToken cancellationToken)
    {
        db.Games.Add(game);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_games_normalized_name" })
        {
            throw new GameAlreadyExistsException(game.Name);
        }
    }

    public Task<Game?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Games.AsNoTracking().Include(game => game.Factions)
            .SingleOrDefaultAsync(game => game.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Game>> ListAsync(CancellationToken cancellationToken) =>
        await db.Games.AsNoTracking().Include(game => game.Factions)
            .OrderBy(game => game.Name).ToListAsync(cancellationToken);
}
