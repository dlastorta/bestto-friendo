using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record CreateTournamentDraft(string? Name, Guid GameId);
public sealed record TournamentDto(Guid Id, string Name, Guid GameId, string Status);

public interface ITournamentDrafts
{
    Task AddAsync(Tournament tournament, CancellationToken ct);
    Task<IReadOnlyList<Tournament>> ListOwnedAsync(Guid organizerPlayerId, Guid? gameId, CancellationToken ct);
    Task<Tournament?> FindOwnedAsync(Guid id, Guid organizerPlayerId, CancellationToken ct);
}

public sealed class TournamentAccessDeniedException : Exception;

public sealed class TournamentDraftsHandler(ICurrentAccount current, IPlayerProfiles profiles, IGameCatalog games, ITournamentDrafts drafts)
{
    private async Task<Guid> OrganizerAsync(CancellationToken ct)
    {
        var accountId = current.AccountId ?? throw new UnauthorizedAccessException();
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        var player = await profiles.FindByAccountAsync(accountId, ct);
        return player?.Id ?? throw new UnauthorizedAccessException();
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentDraft command, CancellationToken ct)
    {
        var organizer = await OrganizerAsync(ct);
        var game = await games.FindAsync(command.GameId, ct)
            ?? throw new TournamentRuleViolationException("El juego no existe.");
        var tournament = Tournament.CreateDraft(organizer, game.Id, command.Name);
        await drafts.AddAsync(tournament, ct);
        return ToDto(tournament);
    }

    public async Task<IReadOnlyList<TournamentDto>> ListAsync(Guid? gameId, CancellationToken ct)
    {
        var tournaments = await drafts.ListOwnedAsync(await OrganizerAsync(ct), gameId, ct);
        return tournaments.Select(ToDto).ToArray();
    }

    public async Task<TournamentDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var tournament = await drafts.FindOwnedAsync(id, await OrganizerAsync(ct), ct);
        return tournament is null ? null : ToDto(tournament);
    }

    private static TournamentDto ToDto(Tournament tournament) =>
        new(tournament.Id, tournament.Name, tournament.GameId, tournament.Status.ToString());
}
