namespace TournamentManager.Domain.Games;

/// <summary>
/// Juego con su conjunto de afiliaciones permitidas y cardinalidad de selección.
/// No depende de HTTP, Entity Framework ni mecanismos de persistencia.
/// </summary>
public sealed class Game
{
    private readonly List<GameFaction> _factions = [];

    private Game() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int MinimumFactions { get; private set; }
    public int MaximumFactions { get; private set; }
    public IReadOnlyCollection<GameFaction> Factions => _factions.AsReadOnly();

    public static Game Create(string? name, IEnumerable<string?>? factionNames, int minimumFactions, int maximumFactions)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length is < 2 or > 120)
            throw new GameRuleViolationException("El nombre del juego debe tener entre 2 y 120 caracteres.");

        if (factionNames is null)
            throw new GameRuleViolationException("Debés definir al menos una facción.");

        // Materializar y validar antes de construir el agregado.
        var names = factionNames.Select(value => (value ?? string.Empty).Trim()).ToArray();
        if (names.Length is < 1 or > 50)
            throw new GameRuleViolationException("Un juego debe tener entre 1 y 50 facciones.");

        if (names.Any(value => value.Length is < 1 or > 80))
            throw new GameRuleViolationException("Cada facción debe tener entre 1 y 80 caracteres.");

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new GameRuleViolationException("No puede haber facciones repetidas en el mismo juego.");

        if (minimumFactions < 1 || maximumFactions < minimumFactions || maximumFactions > names.Length)
            throw new GameRuleViolationException("La selección mínima/máxima debe estar entre 1 y la cantidad de facciones definidas.");

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            NormalizedName = trimmedName.ToUpperInvariant(),
            MinimumFactions = minimumFactions,
            MaximumFactions = maximumFactions
        };

        foreach (var factionName in names)
            game._factions.Add(new GameFaction(game.Id, factionName));

        return game;
    }

    /// <summary>Valida que todas las facciones elegidas pertenecen al mismo juego.</summary>
    public void EnsureValidFactionSelection(IEnumerable<Guid>? factionIds)
    {
        if (factionIds is null)
            throw new GameRuleViolationException("Debés elegir las facciones del mazo.");

        var ids = factionIds.ToArray();
        if (ids.Length < MinimumFactions || ids.Length > MaximumFactions)
            throw new GameRuleViolationException($"Debés seleccionar entre {MinimumFactions} y {MaximumFactions} facciones.");

        if (ids.Distinct().Count() != ids.Length)
            throw new GameRuleViolationException("No podés seleccionar una misma facción dos veces.");

        var allowed = _factions.Select(faction => faction.Id).ToHashSet();
        if (ids.Any(id => !allowed.Contains(id)))
            throw new GameRuleViolationException("Hay facciones que no pertenecen a este juego.");
    }
}
