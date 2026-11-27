using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record StandingDto(Guid PlayerId, string DisplayName, int Position, decimal Points);

public sealed class StandingsHandler(ICurrentAccount current, IPlayerProfiles profiles, ITournamentDrafts tournaments,
    ITournamentRegistrations registrations, ITournamentMatches matches)
{
    public async Task<IReadOnlyList<StandingDto>?> GetAsync(Guid id, CancellationToken ct)
    {
        var player = await profiles.FindByAccountAsync(current.AccountId ?? throw new UnauthorizedAccessException(), ct)
            ?? throw new UnauthorizedAccessException();
        var tournament = current.CanOrganizeTournaments ? await tournaments.FindOwnedAsync(id, player.Id, ct) : null;
        tournament ??= await registrations.FindForPlayerAsync(id, player.Id, ct);
        if (tournament is null) return null;
        if (tournament.Status != TournamentStatus.InProgress || tournament.StartedRules is null)
            throw new TournamentConflictException("La tabla está disponible cuando el torneo está en curso.");
        var participants = await registrations.ListParticipantsAsync(id, ct);
        var names = participants.ToDictionary(p => p.PlayerId, p => p.DisplayName);
        return TournamentStandings.Calculate(names.Keys, await matches.ListAsync(id, ct), tournament.StartedRules)
            .Select(r => new StandingDto(r.PlayerId, names[r.PlayerId], r.Position, r.Points)).ToArray();
    }
}
