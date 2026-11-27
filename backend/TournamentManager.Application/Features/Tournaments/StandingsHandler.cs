using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record StandingTieBreaker(string Code, decimal Value, string Numerator, string Denominator);
public sealed record StandingDto(Guid PlayerId, string DisplayName, int Position, decimal Points,
    int FormulaVersion, IReadOnlyList<StandingTieBreaker> TieBreakers);

public sealed class StandingsHandler(ICurrentAccount current, IPlayerProfiles profiles, ITournamentDrafts tournaments,
    ITournamentRegistrations registrations, ITournamentMatches matches, ITournamentByes byes)
{
    public async Task<IReadOnlyList<StandingDto>?> GetAsync(Guid id, CancellationToken ct)
    {
        var player = await profiles.FindByAccountAsync(current.AccountId ?? throw new UnauthorizedAccessException(), ct)
            ?? throw new UnauthorizedAccessException();
        var tournament = current.CanOrganizeTournaments ? await tournaments.FindOwnedAsync(id, player.Id, ct) : null;
        tournament ??= await registrations.FindForPlayerAsync(id, player.Id, ct);
        if (tournament is null) return null;
        if (tournament.Status == TournamentStatus.Draft || tournament.StartedRules is null)
            throw new TournamentConflictException("La tabla está disponible cuando el torneo está en curso.");
        var participants = await registrations.ListParticipantsAsync(id, ct);
        var names = participants.ToDictionary(p => p.PlayerId, p => p.DisplayName);
        return TournamentStandings.Calculate(names.Keys, await matches.ListAsync(id, ct), tournament.StartedRules,
                await byes.ListAsync(id, ct))
            .Select(r => new StandingDto(r.PlayerId, names[r.PlayerId], r.Position, r.Points,
                tournament.StartedRules.TieBreakerFormulaVersion,
                r.TieBreakers.Select((v, i) => new StandingTieBreaker(tournament.StartedRules.TieBreakers[i], v.Rounded(6),
                    v.Numerator.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    v.Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray())).ToArray();
    }
}
