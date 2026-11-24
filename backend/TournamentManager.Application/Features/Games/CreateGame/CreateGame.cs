using TournamentManager.Domain.Games;

namespace TournamentManager.Application.Features.Games.CreateGame;

public sealed record CreateGameCommand(
    string? Name,
    IReadOnlyList<string?>? Factions,
    int MinimumFactions,
    int MaximumFactions);

public sealed class CreateGameHandler(IGameCatalog catalog)
{
    public async Task<GameDto> HandleAsync(CreateGameCommand command, CancellationToken cancellationToken)
    {
        var game = Game.Create(command.Name, command.Factions, command.MinimumFactions, command.MaximumFactions);

        if (await catalog.NameExistsAsync(game.NormalizedName, cancellationToken))
            throw new GameAlreadyExistsException(game.Name);

        await catalog.AddAsync(game, cancellationToken);
        return GameDto.From(game);
    }
}
