using Microsoft.EntityFrameworkCore;
using Npgsql;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Tournaments;

internal sealed class EfTournamentRegistrations(TournamentDbContext db) : ITournamentRegistrations
{
    public Task<Tournament?> FindByTokenAsync(Guid token, CancellationToken ct) =>
        db.Tournaments.SingleOrDefaultAsync(t => t.RegistrationToken == token, ct);
    public Task<Tournament?> FindForPlayerAsync(Guid tournamentId, Guid playerId, CancellationToken ct) =>
        db.Tournaments.SingleOrDefaultAsync(t => t.Id == tournamentId && db.Registrations.Any(r => r.TournamentId == t.Id && r.PlayerId == playerId), ct);
    public Task<TournamentRegistration?> FindAsync(Guid tournamentId, Guid playerId, CancellationToken ct) =>
        db.Registrations.Include(r => r.Snapshot).SingleOrDefaultAsync(r => r.TournamentId == tournamentId && r.PlayerId == playerId, ct);
    public async Task<IReadOnlyList<MyRegistration>> ListMineAsync(Guid playerId, CancellationToken ct) =>
        await (from r in db.Registrations.AsNoTracking() join t in db.Tournaments on r.TournamentId equals t.Id
               where r.PlayerId == playerId orderby t.Name, t.Id
               select new MyRegistration(t.Id, t.Name, t.GameId, r.Snapshot != null)).ToListAsync(ct);
    public async Task<IReadOnlyList<ParticipantStatus>> ListParticipantsAsync(Guid tournamentId, CancellationToken ct) =>
        await (from r in db.Registrations.AsNoTracking() join p in db.Players on r.PlayerId equals p.Id
               where r.TournamentId == tournamentId orderby p.DisplayName, p.Id
               select new ParticipantStatus(p.Id, p.DisplayName, r.Snapshot != null)).ToListAsync(ct);
    public async Task AddAsync(TournamentRegistration registration, CancellationToken ct)
    {
        db.Registrations.Add(registration);
        await SaveAsync(ct);
    }
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw Conflict(); }
    }
    private static TournamentConflictException Conflict() => new("La inscripción cambió. Recargá antes de continuar.");
}
