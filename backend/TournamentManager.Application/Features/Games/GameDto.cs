using TournamentManager.Domain.Games;

namespace TournamentManager.Application.Features.Games;

public sealed record FactionDto(Guid Id, string Name);

public sealed record GameDto(
    Guid Id,
    string Name,
    int MinimumFactions,
    int MaximumFactions,
    IReadOnlyList<FactionDto> Factions)
{
    public static GameDto From(Game game) => new(
        game.Id,
        game.Name,
        game.MinimumFactions,
        game.MaximumFactions,
        game.Factions.OrderBy(faction => faction.Name, StringComparer.OrdinalIgnoreCase)
            .Select(faction => new FactionDto(faction.Id, faction.Name)).ToArray());
}
