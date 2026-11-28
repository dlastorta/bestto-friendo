using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record SetTournamentPublication(bool? Published, int? ExpectedPublicationVersion, int? ExpectedLifecycleVersion);
public sealed record SetDeckVisibility(bool? Visible, int? SnapshotNumber, int? ExpectedVersion);
public sealed record PublicDeck(string Name, string ListText);
public sealed record PublicParticipant(Guid PlayerId, string DisplayName, PublicDeck? Deck);
public sealed record PublicMatch(int Round, Guid PlayerOneId, Guid PlayerTwoId, string Outcome, string Kind, decimal PlayerOnePoints, decimal PlayerTwoPoints);
public sealed record PublicBye(int Round, Guid PlayerId, decimal Points);
public sealed record PublicTournament(Guid Id, string Name, string GameName, DateTimeOffset StartedAt, DateTimeOffset FinishedAt,
    DateTimeOffset PublishedAt, IReadOnlyList<StandingDto> Standings, IReadOnlyList<PublicParticipant> Participants,
    IReadOnlyList<PublicMatch> Matches, IReadOnlyList<PublicBye> Byes);

// A consistent read of the publication and its permitted data; no credentials or private deck sources.
public sealed record PublishedTournamentData(Tournament Tournament, string GameName, IReadOnlyList<PublicParticipant> Participants,
    IReadOnlyList<TournamentMatch> Matches, IReadOnlyList<TournamentBye> Byes);
public interface IPublishedTournaments
{
    Task<PublishedTournamentData?> ReadAsync(Guid id, CancellationToken ct);
}

public sealed class TournamentPublicationHandler(ICurrentAccount current, IPlayerProfiles profiles, ITournamentDrafts drafts)
{
    public async Task<TournamentDto?> SetAsync(Guid id, SetTournamentPublication command, CancellationToken ct)
    {
        var account = current.AccountId ?? throw new UnauthorizedAccessException();
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        var player = await profiles.FindByAccountAsync(account, ct) ?? throw new UnauthorizedAccessException();
        var tournament = await drafts.FindOwnedAsync(id, player.Id, ct);
        if (tournament is null) return null;
        if (command.Published is null || command.ExpectedPublicationVersion is null || command.ExpectedLifecycleVersion is null)
            throw new TournamentRuleViolationException("Indicá la acción y las versiones de publicación y estado.");
        tournament.SetPublication(command.Published.Value, command.ExpectedPublicationVersion.Value,
            command.ExpectedLifecycleVersion.Value, DateTimeOffset.UtcNow);
        await drafts.SaveAsync(tournament, ct);
        return TournamentDraftsHandler.ToDto(tournament);
    }
}

public sealed class PublicTournamentHandler(IPublishedTournaments published)
{
    public async Task<PublicTournament?> GetAsync(Guid id, CancellationToken ct)
    {
        var data = await published.ReadAsync(id, ct);
        if (data is null) return null;
        var t = data.Tournament;
        if (t.Status != TournamentStatus.Finished || t.PublishedAt is null || t.StartedRules is null) return null;
        var names = data.Participants.ToDictionary(p => p.PlayerId, p => p.DisplayName);
        var table = TournamentStandings.Calculate(names.Keys, data.Matches, t.StartedRules, data.Byes)
            .Select(r => new StandingDto(r.PlayerId, names[r.PlayerId], r.Position, r.Points, t.StartedRules.TieBreakerFormulaVersion,
                r.TieBreakers.Select((v, i) => new StandingTieBreaker(t.StartedRules.TieBreakers[i], v.Rounded(6),
                    v.Numerator.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    v.Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray())).ToArray();
        return new(t.Id, t.Name, data.GameName, t.StartedAt!.Value, t.FinishedAt!.Value, t.PublishedAt.Value, table, data.Participants,
            data.Matches.Where(m => !m.IsSuperseded).Select(m =>
            {
                var result = m.CurrentResult!; var points = result.Points(t.StartedRules);
                return new PublicMatch(m.Round, m.PlayerOneId, m.PlayerTwoId, result.Outcome, result.Kind, points.One, points.Two);
            }).ToArray(), data.Byes.Where(b => !b.IsSuperseded).Select(b => new PublicBye(b.Round, b.PlayerId, t.StartedRules.ByePoints)).ToArray());
    }
}
