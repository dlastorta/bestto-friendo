using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Decks;

namespace TournamentManager.Application.Features.Decks;

public sealed record DeckSummary(Guid Id, Guid GameId, string Name, int Revision, int Version, bool IsArchived);
public sealed record DeckRevisionDto(int Number, string Name, string ListText, IReadOnlyList<Guid> FactionIds);
public sealed record DeckDto(Guid Id, Guid GameId, bool IsArchived, int Version, DeckRevisionDto Current);
public sealed record CreateDeck(Guid GameId, string? Name, string? ListText, Guid[]? FactionIds);
public sealed record EditDeck(string? Name, string? ListText, Guid[]? FactionIds, int ExpectedVersion);
public sealed record ArchiveDeck(int ExpectedVersion);

public interface IDeckLibrary
{
    Task<IReadOnlyList<DeckSummary>> ListAsync(Guid ownerPlayerId, Guid? gameId, bool archived, CancellationToken ct);
    Task<Deck?> FindOwnedAsync(Guid deckId, Guid ownerPlayerId, CancellationToken ct);
    Task AddAsync(Deck deck, CancellationToken ct);
    Task SaveAsync(Deck deck, CancellationToken ct);
}

public sealed class DeckLibraryHandler(ICurrentAccount current, IPlayerProfiles profiles, IGameCatalog games, IDeckLibrary library)
{
    private async Task<Guid> OwnerAsync(CancellationToken ct)
    {
        var accountId = current.AccountId ?? throw new UnauthorizedAccessException();
        var player = await profiles.FindByAccountAsync(accountId, ct);
        return player?.Id ?? throw new UnauthorizedAccessException();
    }

    public async Task<IReadOnlyList<DeckSummary>> ListAsync(Guid? gameId, bool archived, CancellationToken ct) =>
        await library.ListAsync(await OwnerAsync(ct), gameId, archived, ct);

    public async Task<DeckDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var deck = await library.FindOwnedAsync(id, await OwnerAsync(ct), ct);
        return deck is null ? null : ToDto(deck);
    }

    public async Task<IReadOnlyList<DeckRevisionDto>?> HistoryAsync(Guid id, CancellationToken ct)
    {
        var deck = await library.FindOwnedAsync(id, await OwnerAsync(ct), ct);
        return deck?.Revisions.OrderByDescending(item => item.Number).Select(ToDto).ToArray();
    }

    public async Task<DeckDto> CreateAsync(CreateDeck command, CancellationToken ct)
    {
        var ownerId = await OwnerAsync(ct);
        var game = await games.FindAsync(command.GameId, ct)
            ?? throw new DeckRuleViolationException("El juego no existe.");
        var deck = Deck.Create(ownerId, game, command.Name, command.ListText, command.FactionIds);
        await library.AddAsync(deck, ct);
        return ToDto(deck);
    }

    public async Task<DeckDto?> EditAsync(Guid id, EditDeck command, CancellationToken ct)
    {
        var deck = await library.FindOwnedAsync(id, await OwnerAsync(ct), ct);
        if (deck is null) return null;
        var game = await games.FindAsync(deck.GameId, ct)
            ?? throw new DeckRuleViolationException("El juego no existe.");
        deck.Edit(game, command.Name, command.ListText, command.FactionIds, command.ExpectedVersion);
        await library.SaveAsync(deck, ct);
        return ToDto(deck);
    }

    public async Task<DeckDto?> ArchiveAsync(Guid id, ArchiveDeck command, CancellationToken ct)
    {
        var deck = await library.FindOwnedAsync(id, await OwnerAsync(ct), ct);
        if (deck is null) return null;
        deck.Archive(command.ExpectedVersion);
        await library.SaveAsync(deck, ct);
        return ToDto(deck);
    }

    private static DeckRevisionDto ToDto(DeckRevision revision) =>
        new(revision.Number, revision.Name, revision.ListText, revision.FactionIds.ToArray());
    private static DeckDto ToDto(Deck deck) => new(deck.Id, deck.GameId, deck.IsArchived, deck.Version, ToDto(deck.CurrentRevision));
}
