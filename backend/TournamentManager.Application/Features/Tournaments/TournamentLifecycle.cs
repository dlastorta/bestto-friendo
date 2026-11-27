using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record FinishTournament(int ExpectedLifecycleVersion, int ExpectedMatchesVersion, int ExpectedResultsVersion);
public sealed record ReopenTournament(int ExpectedLifecycleVersion, string? Reason);
public sealed record LifecycleDto(int Number, string Kind, string Reason, Guid ActorPlayerId, DateTimeOffset RecordedAt,
    IReadOnlyList<Guid> MatchIds, IReadOnlyList<int> ResultNumbers, IReadOnlyList<Guid> ByeIds);

public sealed class TournamentLifecycleHandler(ICurrentAccount current, IPlayerProfiles profiles, ITournamentDrafts tournaments,
    ITournamentRegistrations registrations, ITournamentMatches matches, ITournamentByes byes)
{
    private async Task<Tournament?> OwnedAsync(Guid id, CancellationToken ct)
    {
        var account = current.AccountId ?? throw new UnauthorizedAccessException();
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        var player = await profiles.FindByAccountAsync(account, ct) ?? throw new UnauthorizedAccessException();
        return await tournaments.FindOwnedAsync(id, player.Id, ct);
    }
    public async Task<TournamentDto?> FinishAsync(Guid id, FinishTournament command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        tournament.Finish((await registrations.ListParticipantsAsync(id, ct)).Select(p => p.PlayerId).ToArray(),
            (await matches.ListAsync(id, ct)).ToArray(), (await byes.ListAsync(id, ct)).ToArray(),
            command.ExpectedLifecycleVersion, command.ExpectedMatchesVersion, command.ExpectedResultsVersion,
            tournament.OrganizerPlayerId, DateTimeOffset.UtcNow);
        await tournaments.SaveAsync(tournament, ct);
        return TournamentDraftsHandler.ToDto(tournament);
    }
    public async Task<TournamentDto?> ReopenAsync(Guid id, ReopenTournament command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        tournament.Reopen(command.Reason, command.ExpectedLifecycleVersion, tournament.OrganizerPlayerId, DateTimeOffset.UtcNow);
        await tournaments.SaveAsync(tournament, ct);
        return TournamentDraftsHandler.ToDto(tournament);
    }
    public async Task<IReadOnlyList<LifecycleDto>?> HistoryAsync(Guid id, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        return tournament?.Lifecycle.OrderBy(e => e.Number).Select(e => new LifecycleDto(e.Number, e.Kind, e.Reason,
            e.ActorPlayerId, e.RecordedAt, e.MatchIds, e.ResultNumbers, e.ByeIds)).ToArray();
    }
}
