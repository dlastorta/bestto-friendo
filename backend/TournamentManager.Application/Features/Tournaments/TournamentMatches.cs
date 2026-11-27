using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record CreateMatch(int Round, Guid PlayerOneId, Guid PlayerTwoId);
public sealed record RecordMatchResult(string? Outcome, string? Kind, string? Reason, int? ExpectedVersion);
public sealed record MatchResultDto(int Number, string Outcome, string Kind, string Reason, Guid ActorPlayerId,
    DateTimeOffset RecordedAt, decimal PlayerOnePoints, decimal PlayerTwoPoints);
public sealed record MatchDto(Guid Id, int Round, Guid PlayerOneId, Guid PlayerTwoId, int Version,
    MatchResultDto? Result, IReadOnlyList<MatchResultDto>? History = null);

public interface ITournamentMatches
{
    Task<IReadOnlyList<TournamentMatch>> ListAsync(Guid tournamentId, CancellationToken ct);
    Task<TournamentMatch?> FindAsync(Guid tournamentId, Guid matchId, CancellationToken ct);
    Task AddAsync(TournamentMatch match, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class TournamentMatchesHandler(ICurrentAccount current, IPlayerProfiles profiles,
    ITournamentDrafts tournaments, ITournamentRegistrations registrations, ITournamentMatches matches, ITournamentByes byes)
{
    private async Task<Tournament?> OwnedAsync(Guid id, CancellationToken ct)
    {
        var account = current.AccountId ?? throw new UnauthorizedAccessException();
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        var player = await profiles.FindByAccountAsync(account, ct) ?? throw new UnauthorizedAccessException();
        return await tournaments.FindOwnedAsync(id, player.Id, ct);
    }

    public async Task<IReadOnlyList<MatchDto>?> ListAsync(Guid id, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        return tournament is null ? null : (await matches.ListAsync(id, ct)).Select(m => ToDto(m, tournament)).ToArray();
    }

    public async Task<MatchDto?> GetAsync(Guid id, Guid matchId, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var match = await matches.FindAsync(id, matchId, ct);
        return match is null ? null : ToDto(match, tournament, true);
    }

    public async Task<MatchDto?> CreateAsync(Guid id, CreateMatch command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var participants = await registrations.ListParticipantsAsync(id, ct);
        var existing = await matches.ListAsync(id, ct);
        var match = TournamentMatch.Create(tournament, command.Round, command.PlayerOneId, command.PlayerTwoId,
            participants.Select(p => p.PlayerId).ToArray(), existing.ToArray(), tournament.OrganizerPlayerId, DateTimeOffset.UtcNow,
            (await byes.ListAsync(id, ct)).ToArray());
        await matches.AddAsync(match, ct);
        return ToDto(match, tournament);
    }

    public async Task<MatchDto?> RecordAsync(Guid id, Guid matchId, RecordMatchResult command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var match = await matches.FindAsync(id, matchId, ct);
        if (match is null) return null;
        if (command.ExpectedVersion is null) throw new TournamentRuleViolationException("Indicá la versión esperada del resultado.");
        match.RecordResult(tournament, command.Outcome, command.Kind, command.Reason, command.ExpectedVersion.Value,
            tournament.OrganizerPlayerId, DateTimeOffset.UtcNow);
        await matches.SaveAsync(ct);
        return ToDto(match, tournament, true);
    }

    private static MatchDto ToDto(TournamentMatch match, Tournament tournament, bool history = false)
    {
        MatchResultDto Result(MatchResultRevision r)
        {
            var points = r.Points(tournament.StartedRules ?? throw new TournamentConflictException("Faltan reglas históricas."));
            return new(r.Number, r.Outcome, r.Kind, r.Reason, r.ActorPlayerId, r.RecordedAt, points.One, points.Two);
        }
        return new(match.Id, match.Round, match.PlayerOneId, match.PlayerTwoId, match.Version,
            match.CurrentResult is { } result ? Result(result) : null,
            history ? match.Results.OrderByDescending(r => r.Number).Select(Result).ToArray() : null);
    }
}
