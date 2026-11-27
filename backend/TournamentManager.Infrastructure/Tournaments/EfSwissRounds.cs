using Microsoft.EntityFrameworkCore;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Tournaments;

internal sealed class EfSwissRounds(TournamentDbContext db) : ISwissRounds
{
    public async Task<IReadOnlyList<SwissRound>> ListAsync(Guid tournamentId, CancellationToken ct) =>
        await db.SwissRounds.Where(r => r.TournamentId == tournamentId).OrderBy(r => r.Number).ToListAsync(ct);
    public async Task AddAsync(GeneratedRound round, CancellationToken ct)
    {
        db.SwissRounds.Add(round.Audit);
        db.Matches.AddRange(round.Matches);
        if (round.Bye is not null) db.Byes.Add(round.Bye);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { throw new TournamentConflictException("Las partidas o resultados cambiaron. Recargá antes de generar."); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        { throw new TournamentConflictException("La ronda o el bye ya fue asignado. Recargá antes de generar."); }
    }
}
