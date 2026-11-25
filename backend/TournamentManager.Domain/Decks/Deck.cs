using TournamentManager.Domain.Games;

namespace TournamentManager.Domain.Decks;

public sealed class Deck
{
    private readonly List<DeckRevision> _revisions = [];
    private Deck() { }

    public Guid Id { get; private set; }
    public Guid OwnerPlayerId { get; private set; }
    public Guid GameId { get; private set; }
    public bool IsArchived { get; private set; }
    public int Version { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public IReadOnlyCollection<DeckRevision> Revisions => _revisions.AsReadOnly();
    public DeckRevision CurrentRevision => _revisions.Single(item => item.Number == CurrentRevisionNumber);

    public static Deck Create(Guid ownerPlayerId, Game game, string? name, string? listText, IEnumerable<Guid>? factionIds)
    {
        if (ownerPlayerId == Guid.Empty) throw new DeckRuleViolationException("El mazo requiere un propietario.");
        var deck = new Deck { Id = Guid.NewGuid(), OwnerPlayerId = ownerPlayerId, GameId = game.Id };
        deck.AddRevision(game, name, listText, factionIds);
        return deck;
    }

    public void Edit(Game game, string? name, string? listText, IEnumerable<Guid>? factionIds, int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (IsArchived) throw new DeckConflictException("El mazo está archivado y no se puede editar.");
        if (game.Id != GameId) throw new DeckRuleViolationException("No se puede cambiar el juego de un mazo existente.");
        AddRevision(game, name, listText, factionIds);
    }

    public void Archive(int expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (IsArchived) return;
        IsArchived = true;
        Version++;
    }

    private void EnsureVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
            throw new DeckConflictException("El mazo cambió. Recargá su versión antes de guardar.");
    }

    private void AddRevision(Game game, string? name, string? listText, IEnumerable<Guid>? factionIds)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length is < 2 or > 120)
            throw new DeckRuleViolationException("El nombre del mazo debe tener entre 2 y 120 caracteres.");
        var factions = factionIds?.ToArray();
        game.EnsureValidFactionSelection(factions);
        // La lista se conserva literalmente: no se parsea, normaliza ni valida por cartas.
        _revisions.Add(new DeckRevision(Id, CurrentRevisionNumber + 1, normalizedName, listText ?? string.Empty, factions!));
        CurrentRevisionNumber++;
        Version++;
    }
}

public sealed class DeckRevision
{
    private Guid[] _factionIds = [];
    private DeckRevision() { }
    internal DeckRevision(Guid deckId, int number, string name, string listText, Guid[] factionIds)
    {
        DeckId = deckId; Number = number; Name = name; ListText = listText;
        _factionIds = factionIds.ToArray();
    }
    public Guid DeckId { get; private set; }
    public int Number { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ListText { get; private set; } = string.Empty;
    public IReadOnlyList<Guid> FactionIds => Array.AsReadOnly(_factionIds);
}

public sealed class DeckRuleViolationException(string message) : Exception(message);
public sealed class DeckConflictException(string message) : Exception(message);
