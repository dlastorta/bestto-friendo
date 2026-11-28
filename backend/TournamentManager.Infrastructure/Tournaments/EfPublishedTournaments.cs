using System.Data;
using Microsoft.EntityFrameworkCore;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Tournaments;

internal sealed class EfPublishedTournaments(TournamentDbContext db) : IPublishedTournaments
{
    public async Task<PublishedTournamentData?> ReadAsync(Guid id, CancellationToken ct)
    {
        // One snapshot prevents mixing a published closure with later corrections or withdrawn consent.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var tournament = await db.Tournaments.AsNoTracking().Include(t => t.StartedRules)
            .SingleOrDefaultAsync(t => t.Id == id && t.Status == TournamentStatus.Finished && t.PublishedAt != null, ct);
        if (tournament is null) return null;
        var gameName = await db.Games.Where(g => g.Id == tournament.GameId).Select(g => g.Name).SingleAsync(ct);
        var participants = await (from r in db.Registrations.AsNoTracking()
            join p in db.Players on r.PlayerId equals p.Id
            where r.TournamentId == id orderby p.DisplayName, p.Id
            select new { p.Id, p.DisplayName, r.PublicSnapshotNumber, RegistrationId = r.Id }).ToListAsync(ct);
        // Only opted-in snapshots are read; never load private list text for public responses.
        var decks = await (from r in db.Registrations.AsNoTracking()
            from s in r.Snapshots
            where r.TournamentId == id && r.PublicSnapshotNumber == s.Number
            select new { r.Id, s.Name, s.ListText }).ToDictionaryAsync(s => s.Id, s => new PublicDeck(s.Name, s.ListText), ct);
        var matches = await db.Matches.AsNoTracking().Include(m => m.Results).Where(m => m.TournamentId == id)
            .OrderBy(m => m.Round).ThenBy(m => m.Id).ToListAsync(ct);
        var byes = await db.Byes.AsNoTracking().Where(b => b.TournamentId == id).OrderBy(b => b.Round).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(tournament, gameName, participants.Select(p => new PublicParticipant(p.Id, p.DisplayName,
            decks.GetValueOrDefault(p.RegistrationId))).ToArray(), matches, byes);
    }
}
