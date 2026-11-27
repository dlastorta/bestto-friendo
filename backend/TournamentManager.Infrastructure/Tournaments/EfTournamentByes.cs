using Microsoft.EntityFrameworkCore;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Tournaments;

internal sealed class EfTournamentByes(TournamentDbContext db) : ITournamentByes
{
    public async Task<IReadOnlyList<TournamentBye>> ListAsync(Guid tournamentId, CancellationToken ct) =>
        await db.Byes.Where(b => b.TournamentId == tournamentId).OrderBy(b => b.Round).ToListAsync(ct);
    public async Task AddAsync(TournamentBye bye, CancellationToken ct)
    {
        db.Byes.Add(bye);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new TournamentConflictException("Las asignaciones cambiaron. Recargá antes de continuar."); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        { throw new TournamentConflictException("La ronda o el jugador ya tiene un bye. Recargá antes de continuar."); }
    }
}
