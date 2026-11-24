namespace TournamentManager.Application.Features.Games.GetGame;

public sealed class GetGameHandler(IGameCatalog catalog)
{
    public async Task<GameDto?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var game = await catalog.FindAsync(id, cancellationToken);
        return game is null ? null : GameDto.From(game);
    }
}
