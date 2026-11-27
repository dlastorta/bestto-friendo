using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record AssignBye(int Round, Guid PlayerId);
public sealed record ByeDto(Guid Id, int Round, Guid PlayerId, decimal Points, Guid ActorPlayerId, DateTimeOffset AssignedAt);
public interface ITournamentByes
{
    Task<IReadOnlyList<TournamentBye>> ListAsync(Guid tournamentId, CancellationToken ct);
    Task AddAsync(TournamentBye bye, CancellationToken ct);
}

public sealed class TournamentByesHandler(ICurrentAccount current, IPlayerProfiles profiles,
    ITournamentDrafts tournaments, ITournamentRegistrations registrations, ITournamentMatches matches, ITournamentByes byes)
{
    private async Task<Tournament?> OwnedAsync(Guid id, CancellationToken ct)
    {
        var account = current.AccountId ?? throw new UnauthorizedAccessException();
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        var player = await profiles.FindByAccountAsync(account, ct) ?? throw new UnauthorizedAccessException();
        return await tournaments.FindOwnedAsync(id, player.Id, ct);
    }

    public async Task<IReadOnlyList<ByeDto>?> ListAsync(Guid id, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        return tournament is null ? null : (await byes.ListAsync(id, ct)).Select(b => ToDto(b, tournament)).ToArray();
    }

    public async Task<ByeDto?> AssignAsync(Guid id, AssignBye command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var participants = await registrations.ListParticipantsAsync(id, ct);
        var bye = TournamentBye.Assign(tournament, command.Round, command.PlayerId,
            participants.Select(p => p.PlayerId).ToArray(), (await matches.ListAsync(id, ct)).ToArray(),
            (await byes.ListAsync(id, ct)).ToArray(), tournament.OrganizerPlayerId, DateTimeOffset.UtcNow);
        await byes.AddAsync(bye, ct);
        return ToDto(bye, tournament);
    }

    private static ByeDto ToDto(TournamentBye bye, Tournament tournament) => new(bye.Id, bye.Round, bye.PlayerId,
        tournament.StartedRules!.ByePoints, bye.ActorPlayerId, bye.AssignedAt);
}
