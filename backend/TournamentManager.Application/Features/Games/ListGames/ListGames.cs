namespace TournamentManager.Application.Features.Games.ListGames;

public sealed class ListGamesHandler(IGameCatalog catalog)
{
    public async Task<IReadOnlyList<GameDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var games = await catalog.ListAsync(cancellationToken);
        return games.Select(GameDto.From).ToArray();
    }
}
