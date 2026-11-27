using System.Security.Cryptography;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record GenerateSwissRound(int ExpectedRound);
public sealed record RoundPair(Guid MatchId, Guid PlayerOneId, Guid PlayerTwoId);
public sealed record SwissRoundDto(int Number, int AlgorithmVersion, Guid ActorPlayerId, DateTimeOffset GeneratedAt,
    IReadOnlyList<Guid> ParticipantOrder, IReadOnlyList<RoundPair> Matches, Guid? ByePlayerId);
public sealed record SwissRoundsDto(int NextRound, bool Enabled, IReadOnlyList<SwissRoundDto> Rounds);
public interface ISwissRounds
{
    Task<IReadOnlyList<SwissRound>> ListAsync(Guid tournamentId, CancellationToken ct);
    Task AddAsync(GeneratedRound round, CancellationToken ct);
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
            (await rounds.ListAsync(id, ct)).Select(r => ToDto(r, games, assigned)).ToArray());
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
            matches.Where(m => m.Round == round.Number).OrderBy(m => Array.IndexOf(round.ParticipantOrder.ToArray(), m.PlayerOneId))
                .Select(m => new RoundPair(m.Id, m.PlayerOneId, m.PlayerTwoId)).ToArray(),
            byes.SingleOrDefault(b => b.Round == round.Number)?.PlayerId);
}
