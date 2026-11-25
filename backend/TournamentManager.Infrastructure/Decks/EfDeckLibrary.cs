using Microsoft.EntityFrameworkCore;
using Npgsql;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Domain.Decks;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Decks;

internal sealed class EfDeckLibrary(TournamentDbContext db) : IDeckLibrary
{
    public async Task<IReadOnlyList<DeckSummary>> ListAsync(Guid ownerPlayerId, Guid? gameId, bool archived, CancellationToken ct) =>
        await db.Decks.AsNoTracking()
            .Where(deck => deck.OwnerPlayerId == ownerPlayerId && deck.IsArchived == archived && (!gameId.HasValue || deck.GameId == gameId))
            .OrderBy(deck => deck.Revisions.Where(revision => revision.Number == deck.CurrentRevisionNumber).Select(revision => revision.Name).First())
            .ThenBy(deck => deck.Id)
            .Select(deck => new DeckSummary(deck.Id, deck.GameId,
                deck.Revisions.Where(revision => revision.Number == deck.CurrentRevisionNumber).Select(revision => revision.Name).First(),
                deck.CurrentRevisionNumber, deck.Version, deck.IsArchived))
            .ToListAsync(ct);

    public Task<Deck?> FindOwnedAsync(Guid deckId, Guid ownerPlayerId, CancellationToken ct) =>
        db.Decks.Include(deck => deck.Revisions)
            .SingleOrDefaultAsync(deck => deck.Id == deckId && deck.OwnerPlayerId == ownerPlayerId, ct);

    public async Task AddAsync(Deck deck, CancellationToken ct)
    {
        db.Decks.Add(deck);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(Deck deck, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_deck_revisions" })
        { throw Conflict(); }
    }

    private static DeckConflictException Conflict() => new("El mazo cambió. Recargá su versión antes de guardar.");
}
