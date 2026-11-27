using Microsoft.EntityFrameworkCore;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Tournaments;

internal sealed class EfTournamentMatches(TournamentDbContext db) : ITournamentMatches
{
    public async Task<IReadOnlyList<TournamentMatch>> ListAsync(Guid tournamentId, CancellationToken ct) =>
        await db.Matches.Include(m => m.Results).Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.Round).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);
    public Task<TournamentMatch?> FindAsync(Guid tournamentId, Guid matchId, CancellationToken ct) =>
        db.Matches.IgnoreQueryFilters().Include(m => m.Results).SingleOrDefaultAsync(m => m.TournamentId == tournamentId && m.Id == matchId, ct);
    public async Task AddAsync(TournamentMatch match, CancellationToken ct)
    { db.Matches.Add(match); await SaveAsync(ct); }
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new TournamentConflictException("Las partidas cambiaron. Recargá antes de continuar."); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_match_result_revisions" })
        { throw new TournamentConflictException("El resultado cambió. Recargá antes de continuar."); }
    }
}
