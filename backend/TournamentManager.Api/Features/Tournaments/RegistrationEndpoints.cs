using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var owner = endpoints.MapGroup("/api/tournaments/{id:guid}").RequireAuthorization("OrganizeTournaments")
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        owner.MapPost("/registration-link", async (Guid id, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var token = await handler.EnableLinkAsync(id, ct);
            return token is null ? Results.NotFound() : Results.Ok(new { token });
        });
        owner.MapGet("/participants", async (Guid id, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ParticipantsAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        var player = endpoints.MapGroup("/api/registrations").RequireAuthorization()
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        player.MapGet("/invite/{token:guid}", async (Guid token, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.InvitationAsync(token, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        player.MapPost("/invite/{token:guid}", async (Guid token, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.JoinAsync(token, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        player.MapGet("", async (TournamentRegistrationsHandler handler, CancellationToken ct) => Results.Ok(await handler.ListMineAsync(ct)));
        player.MapGet("/{id:guid}", async (Guid id, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetMineAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        player.MapPost("/{id:guid}/deck", async (Guid id, PresentDeck command, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.PresentAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}
