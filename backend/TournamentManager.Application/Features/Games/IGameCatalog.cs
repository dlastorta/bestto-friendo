using TournamentManager.Domain.Games;

namespace TournamentManager.Application.Features.Games;

/// <summary>Puerto orientado al catálogo, no expone IQueryable ni DbContext.</summary>
public interface IGameCatalog
{
    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken);
    Task AddAsync(Game game, CancellationToken cancellationToken);
    Task<Game?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Game>> ListAsync(CancellationToken cancellationToken);
}
