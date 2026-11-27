using System.Security.Cryptography;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record GenerateSwissRound(int ExpectedRound);
public sealed record RegenerateSwissRound(int Round, int ExpectedMatchesVersion, int ExpectedResultsVersion, int ExpectedLifecycleVersion, string? Reason);
public sealed record RoundPair(Guid MatchId, Guid PlayerOneId, Guid PlayerTwoId);
public sealed record SwissRoundDto(int Number, int AlgorithmVersion, Guid ActorPlayerId, DateTimeOffset GeneratedAt,
    IReadOnlyList<Guid> ParticipantOrder, IReadOnlyList<RoundPair> Matches, Guid? ByePlayerId, int Revision = 1, string Reason = "Generación inicial");
public sealed record SwissRoundsDto(int NextRound, bool Enabled, IReadOnlyList<SwissRoundDto> Rounds,
    int MatchesVersion = 0, int ResultsVersion = 0, int LifecycleVersion = 0);
public sealed record RoundHistoryDto(int Round, int Revision, IReadOnlyList<MatchDto> Matches, IReadOnlyList<ByeDto> Byes,
    string ReplacementReason, Guid ReplacedByPlayerId, DateTimeOffset ReplacedAt);
public interface ISwissRounds
{
    Task<IReadOnlyList<SwissRound>> ListAsync(Guid tournamentId, CancellationToken ct);
    Task AddAsync(GeneratedRound round, CancellationToken ct);
    Task<IReadOnlyList<SwissRound>> ListAllAsync(Guid tournamentId, CancellationToken ct);
    Task<IReadOnlyList<TournamentMatch>> MatchesHistoryAsync(Guid tournamentId, CancellationToken ct);
    Task<IReadOnlyList<TournamentBye>> ByesHistoryAsync(Guid tournamentId, CancellationToken ct);
}

public sealed class SwissRoundsHandler(ICurrentAccount current, IPlayerProfiles profiles, ITournamentDrafts tournaments,
    ITournamentRegistrations registrations, ITournamentMatches matches, ITournamentByes byes, ISwissRounds rounds)
{
    private async Task<Tournament?> OwnedAsync(Guid id, CancellationToken ct)
    {
        var account = current.AccountId ?? throw new UnauthorizedAccessException();
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        var player = await profiles.FindByAccountAsync(account, ct) ?? throw new UnauthorizedAccessException();
        return await tournaments.FindOwnedAsync(id, player.Id, ct);
    }
    public async Task<SwissRoundsDto?> ListAsync(Guid id, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var games = await matches.ListAsync(id, ct);
        var assigned = await byes.ListAsync(id, ct);
        return new(RoundProgress.NextRound(games, assigned),
            tournament.Status == TournamentStatus.InProgress && tournament.StartedRules?.PairingAlgorithmVersion == 1,
            (await rounds.ListAsync(id, ct)).Select(r => ToDto(r, games, assigned)).ToArray(),
            tournament.MatchesVersion, tournament.ResultsVersion, tournament.LifecycleVersion);
    }
    public async Task<SwissRoundDto?> RegenerateAsync(Guid id, RegenerateSwissRound command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var participants = (await registrations.ListParticipantsAsync(id, ct)).Select(p => p.PlayerId).ToArray();
        var draw = participants.ToArray();
        for (var i = draw.Length - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (draw[i], draw[j]) = (draw[j], draw[i]); }
        var round = SwissRound.Regenerate(tournament, command.Round, command.ExpectedMatchesVersion, command.ExpectedResultsVersion,
            command.ExpectedLifecycleVersion, command.Reason, participants, (await matches.ListAsync(id, ct)).ToArray(),
            (await byes.ListAsync(id, ct)).ToArray(), (await rounds.ListAllAsync(id, ct)).ToArray(), draw,
            tournament.OrganizerPlayerId, DateTimeOffset.UtcNow, ct);
        await rounds.AddAsync(round, ct);
        return ToDto(round.Audit, round.Matches, round.Bye is null ? [] : [round.Bye]);
    }
    public async Task<IReadOnlyList<RoundHistoryDto>?> HistoryAsync(Guid id, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var games = (await rounds.MatchesHistoryAsync(id, ct)).Where(m => m.IsSuperseded).ToArray();
        var assigned = (await rounds.ByesHistoryAsync(id, ct)).Where(b => b.IsSuperseded).ToArray();
        var audits = await rounds.ListAllAsync(id, ct);
        return games.Select(m => (m.Round, m.RoundRevision)).Concat(assigned.Select(b => (b.Round, b.RoundRevision)))
            .Distinct().OrderBy(x => x.Round).ThenBy(x => x.RoundRevision).Select(x =>
            {
                var replacement = audits.Single(r => r.Number == x.Round && r.Revision == x.RoundRevision + 1);
                return new RoundHistoryDto(x.Round, x.RoundRevision,
                games.Where(m => m.Round == x.Round && m.RoundRevision == x.RoundRevision).Select(m => TournamentMatchesHandler.ToDto(m, tournament, true)).ToArray(),
                assigned.Where(b => b.Round == x.Round && b.RoundRevision == x.RoundRevision).Select(b => new ByeDto(b.Id, b.Round,
                    b.PlayerId, tournament.StartedRules!.ByePoints, b.ActorPlayerId, b.AssignedAt)).ToArray(),
                    replacement.Reason, replacement.ActorPlayerId, replacement.GeneratedAt);
            }).ToArray();
    }
    public async Task<SwissRoundDto?> GenerateAsync(Guid id, GenerateSwissRound command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var participants = (await registrations.ListParticipantsAsync(id, ct)).Select(p => p.PlayerId).ToArray();
        var draw = participants.ToArray();
        for (var i = draw.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (draw[i], draw[j]) = (draw[j], draw[i]);
        }
        var round = SwissRound.Generate(tournament, command.ExpectedRound, participants,
            (await matches.ListAsync(id, ct)).ToArray(), (await byes.ListAsync(id, ct)).ToArray(), draw,
            tournament.OrganizerPlayerId, DateTimeOffset.UtcNow, ct);
        await rounds.AddAsync(round, ct);
        return ToDto(round.Audit, round.Matches, round.Bye is null ? [] : [round.Bye]);
    }
    private static SwissRoundDto ToDto(SwissRound round, IEnumerable<TournamentMatch> matches, IEnumerable<TournamentBye> byes) =>
        new(round.Number, round.AlgorithmVersion, round.ActorPlayerId, round.GeneratedAt, round.ParticipantOrder,
            matches.Where(m => m.Round == round.Number && m.RoundRevision == round.Revision).OrderBy(m => Array.IndexOf(round.ParticipantOrder.ToArray(), m.PlayerOneId))
                .Select(m => new RoundPair(m.Id, m.PlayerOneId, m.PlayerTwoId)).ToArray(),
            byes.SingleOrDefault(b => b.Round == round.Number && b.RoundRevision == round.Revision)?.PlayerId, round.Revision, round.Reason);
}
