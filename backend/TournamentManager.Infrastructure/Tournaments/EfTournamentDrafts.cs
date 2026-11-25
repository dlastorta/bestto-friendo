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
        await db.Tournaments.AsNoTracking()
            .Where(item => item.OrganizerPlayerId == organizerPlayerId && (!gameId.HasValue || item.GameId == gameId))
            .OrderBy(item => item.Name).ThenBy(item => item.Id).ToListAsync(ct);

    public Task<Tournament?> FindOwnedAsync(Guid id, Guid organizerPlayerId, CancellationToken ct) =>
        db.Tournaments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.OrganizerPlayerId == organizerPlayerId, ct);
}
