using TournamentManager.Application.Features.Decks;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Application.Features.Tournaments;

public sealed record RegistrationInvitation(Guid TournamentId, string Name, Guid GameId, string Status);
public sealed record ParticipantStatus(Guid PlayerId, string DisplayName, bool HasPresentedDeck);
public sealed record MyRegistration(Guid TournamentId, string Name, Guid GameId, bool HasPresentedDeck, string Status = "Draft");
public sealed record PresentedDeck(Guid SourceDeckId, int SourceRevision, Guid GameId, string Name,
    string ListText, IReadOnlyList<Guid> FactionIds, DateTimeOffset PresentedAt, int Number, Guid ActorPlayerId, string Reason);
public sealed record RegistrationDetail(Guid TournamentId, bool HasPresentedDeck, PresentedDeck? Deck, string Status = "Draft");
public sealed record PresentDeck(Guid DeckId, int? ExpectedDeckVersion);
public sealed record ReplacePresentedDeck(Guid DeckId, int? ExpectedDeckVersion, int? ExpectedPresentationVersion, string? Reason);

public interface ITournamentRegistrations
{
    Task<Tournament?> FindByTokenAsync(Guid token, CancellationToken ct);
    Task<Tournament?> FindForPlayerAsync(Guid tournamentId, Guid playerId, CancellationToken ct);
    Task<TournamentRegistration?> FindAsync(Guid tournamentId, Guid playerId, CancellationToken ct);
    Task<IReadOnlyList<MyRegistration>> ListMineAsync(Guid playerId, CancellationToken ct);
    Task<IReadOnlyList<ParticipantStatus>> ListParticipantsAsync(Guid tournamentId, CancellationToken ct);
    Task AddAsync(TournamentRegistration registration, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class TournamentRegistrationsHandler(ICurrentAccount current, IPlayerProfiles profiles,
    ITournamentDrafts drafts, ITournamentRegistrations registrations, IDeckLibrary decks)
{
    private async Task<Guid> PlayerAsync(CancellationToken ct) =>
        (await profiles.FindByAccountAsync(current.AccountId ?? throw new UnauthorizedAccessException(), ct))?.Id
        ?? throw new UnauthorizedAccessException();

    private async Task<Tournament?> OwnedAsync(Guid id, CancellationToken ct)
    {
        if (!current.CanOrganizeTournaments) throw new TournamentAccessDeniedException();
        return await drafts.FindOwnedAsync(id, await PlayerAsync(ct), ct);
    }

    public async Task<Guid?> EnableLinkAsync(Guid id, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return null;
        var token = tournament.EnableRegistrationLink();
        await drafts.SaveAsync(tournament, ct);
        return token;
    }

    public async Task<bool> StartAsync(Guid id, StartTournament command, CancellationToken ct)
    {
        var tournament = await OwnedAsync(id, ct);
        if (tournament is null) return false;
        if (command.ExpectedRulesVersion is null || command.ExpectedParticipationVersion is null)
            throw new TournamentRuleViolationException("Recargá el torneo para indicar las versiones esperadas.");
        var participants = await registrations.ListParticipantsAsync(id, ct);
        tournament.Start(participants.Count, participants.Count(p => p.HasPresentedDeck), command.ExpectedRulesVersion.Value,
            command.ExpectedParticipationVersion.Value, DateTimeOffset.UtcNow);
        await drafts.SaveAsync(tournament, ct);
        return true;
    }

    public async Task<IReadOnlyList<ParticipantStatus>?> ParticipantsAsync(Guid id, CancellationToken ct) =>
        await OwnedAsync(id, ct) is null ? null : await registrations.ListParticipantsAsync(id, ct);

    public async Task<RegistrationInvitation?> InvitationAsync(Guid token, CancellationToken ct)
    {
        await PlayerAsync(ct);
        var tournament = await registrations.FindByTokenAsync(token, ct);
        return tournament is null ? null : new(tournament.Id, tournament.Name, tournament.GameId, tournament.Status.ToString());
    }

    public async Task<RegistrationDetail?> JoinAsync(Guid token, CancellationToken ct)
    {
        var player = await PlayerAsync(ct);
        var tournament = await registrations.FindByTokenAsync(token, ct);
        if (tournament is null) return null;
        var existing = await registrations.FindAsync(tournament.Id, player, ct);
        if (existing is not null) return Detail(existing, tournament);
        var registration = TournamentRegistration.Create(tournament, player, DateTimeOffset.UtcNow);
        await registrations.AddAsync(registration, ct);
        return Detail(registration, tournament);
    }

    public async Task<IReadOnlyList<MyRegistration>> ListMineAsync(CancellationToken ct) =>
        await registrations.ListMineAsync(await PlayerAsync(ct), ct);

    public async Task<RegistrationDetail?> GetMineAsync(Guid id, CancellationToken ct)
    {
        var player = await PlayerAsync(ct);
        var tournament = await registrations.FindForPlayerAsync(id, player, ct);
        var registration = await registrations.FindAsync(id, player, ct);
        return registration is null || tournament is null ? null : Detail(registration, tournament);
    }

    public async Task<RegistrationDetail?> PresentAsync(Guid id, PresentDeck command, CancellationToken ct)
    {
        var player = await PlayerAsync(ct);
        var registration = await registrations.FindAsync(id, player, ct);
        if (registration is null) return null;
        var tournament = await registrations.FindForPlayerAsync(id, player, ct);
        var deck = await decks.FindOwnedAsync(command.DeckId, player, ct);
        if (deck is null || tournament is null) return null;
        if (command.ExpectedDeckVersion is null) throw new TournamentRuleViolationException("Debés indicar la versión del mazo.");
        registration.Present(tournament, deck, command.ExpectedDeckVersion.Value, DateTimeOffset.UtcNow);
        await registrations.SaveAsync(ct);
        return Detail(registration, tournament);
    }

    public async Task<IReadOnlyList<PresentedDeck>?> HistoryAsync(Guid id, CancellationToken ct)
    {
        var registration = await registrations.FindAsync(id, await PlayerAsync(ct), ct);
        return registration?.Snapshots.OrderByDescending(s => s.Number).Select(SnapshotDto).ToArray();
    }

    public async Task<RegistrationDetail?> ReplaceAsync(Guid id, ReplacePresentedDeck command, CancellationToken ct)
    {
        var player = await PlayerAsync(ct);
        var tournament = await registrations.FindForPlayerAsync(id, player, ct);
        var registration = await registrations.FindAsync(id, player, ct);
        if (registration is null || tournament is null) return null;
        var deck = await decks.FindOwnedAsync(command.DeckId, player, ct);
        if (deck is null) return null;
        if (command.ExpectedDeckVersion is null || command.ExpectedPresentationVersion is null)
            throw new TournamentRuleViolationException("Debés indicar las versiones del mazo y de la presentación.");
        registration.Replace(tournament, deck, command.ExpectedDeckVersion.Value, command.ExpectedPresentationVersion.Value, command.Reason, DateTimeOffset.UtcNow);
        await registrations.SaveAsync(ct);
        return Detail(registration, tournament);
    }

    private static PresentedDeck SnapshotDto(TournamentDeckSnapshot s) =>
        new(s.SourceDeckId, s.SourceRevision, s.GameId, s.Name, s.ListText, s.FactionIds.ToArray(), s.PresentedAt, s.Number, s.ActorPlayerId, s.Reason);
    private static RegistrationDetail Detail(TournamentRegistration registration, Tournament tournament) => new(registration.TournamentId,
        registration.Snapshot is not null, registration.Snapshot is { } s ? SnapshotDto(s) : null, tournament.Status.ToString());
}
