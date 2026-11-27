using Microsoft.EntityFrameworkCore;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Tournaments;

internal sealed class EfTournamentDrafts(TournamentDbContext db) : ITournamentDrafts
{
    public async Task AddAsync(Tournament tournament, CancellationToken ct)
    {
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Tournament>> ListOwnedAsync(Guid organizerPlayerId, Guid? gameId, CancellationToken ct) =>
        await db.Tournaments.AsNoTracking().Include(item => item.Rules).Include(item => item.StartedRules)
            .Where(item => item.OrganizerPlayerId == organizerPlayerId && (!gameId.HasValue || item.GameId == gameId))
            .OrderBy(item => item.Name).ThenBy(item => item.Id).ToListAsync(ct);

    public Task<Tournament?> FindOwnedAsync(Guid id, Guid organizerPlayerId, CancellationToken ct) =>
        db.Tournaments.Include(item => item.Rules).Include(item => item.StartedRules).Include(item => item.Lifecycle).SingleOrDefaultAsync(item => item.Id == id && item.OrganizerPlayerId == organizerPlayerId, ct);

    public async Task SaveAsync(Tournament tournament, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new TournamentConflictException("El torneo cambió. Recargá antes de guardar."); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_tournament_rules" or "PK_tournament_lifecycle_events" })
        { throw new TournamentConflictException("Las reglas cambiaron. Recargá antes de guardar."); }
    }
}
