using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record CreateTournamentDraft(string? Name, Guid GameId);
public sealed record TournamentDto(Guid Id, string Name, Guid GameId, string Status, int RulesVersion = 0, TournamentRulesDto? Rules = null,
    int ParticipationVersion = 0, DateTimeOffset? StartedAt = null, TournamentRulesDto? StartedRules = null,
    int MatchesVersion = 0, int ResultsVersion = 0, int LifecycleVersion = 0, DateTimeOffset? FinishedAt = null, DateTimeOffset? PublishedAt = null, int PublicationVersion = 0);
public sealed record StartTournament(int? ExpectedRulesVersion, int? ExpectedParticipationVersion);
public sealed record TournamentRulesDto(decimal WinPoints, decimal DrawPoints, decimal LossPoints,
    bool ByesEnabled, decimal ByePoints, int MaximumByesPerParticipant, IReadOnlyList<string> TieBreakers);
public sealed record ConfigureTournamentRules(decimal? WinPoints, decimal? DrawPoints, decimal? LossPoints,
    bool? ByesEnabled, decimal? ByePoints, string[]? TieBreakers, int? ExpectedVersion);

public interface ITournamentDrafts
{
    Task AddAsync(Tournament tournament, CancellationToken ct);
    Task<IReadOnlyList<Tournament>> ListOwnedAsync(Guid organizerPlayerId, Guid? gameId, CancellationToken ct);
    Task<Tournament?> FindOwnedAsync(Guid id, Guid organizerPlayerId, CancellationToken ct);
    Task SaveAsync(Tournament tournament, CancellationToken ct);
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

    public async Task<TournamentDto?> ConfigureRulesAsync(Guid id, ConfigureTournamentRules command, CancellationToken ct)
    {
        var tournament = await drafts.FindOwnedAsync(id, await OrganizerAsync(ct), ct);
        if (tournament is null) return null;
        if (command.WinPoints is null || command.DrawPoints is null || command.LossPoints is null ||
            command.ByesEnabled is null || command.ByePoints is null || command.ExpectedVersion is null)
            throw new TournamentRuleViolationException("Debés indicar todos los puntajes, la política de bye y la versión esperada.");
        tournament.ConfigureRules(command.WinPoints.Value, command.DrawPoints.Value, command.LossPoints.Value,
            command.ByesEnabled.Value, command.ByePoints.Value, command.TieBreakers, command.ExpectedVersion.Value);
        await drafts.SaveAsync(tournament, ct);
        return ToDto(tournament);
    }

    internal static TournamentDto ToDto(Tournament tournament) =>
        new(tournament.Id, tournament.Name, tournament.GameId, tournament.Status.ToString(), tournament.RulesVersion,
            tournament.Rules is { } rules ? new(rules.WinPoints, rules.DrawPoints, rules.LossPoints,
                rules.ByesEnabled, rules.ByePoints, TournamentRules.MaximumByesPerParticipant, rules.TieBreakers.ToArray()) : null,
            tournament.ParticipationVersion, tournament.StartedAt, tournament.StartedRules is { } frozen
                ? new(frozen.WinPoints, frozen.DrawPoints, frozen.LossPoints, frozen.ByesEnabled, frozen.ByePoints,
                    frozen.MaximumByesPerParticipant, frozen.TieBreakers.ToArray()) : null,
            tournament.MatchesVersion, tournament.ResultsVersion, tournament.LifecycleVersion, tournament.FinishedAt, tournament.PublishedAt, tournament.PublicationVersion);
}
