using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class PublicationEndpoints
{
    public static IEndpointRouteBuilder MapPublicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/public/tournaments/{id:guid}", async (Guid id, PublicTournamentHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).AllowAnonymous().AddEndpointFilter<TournamentErrorsFilter>();
        endpoints.MapPost("/api/tournaments/{id:guid}/publication", async (Guid id, SetTournamentPublication command, TournamentPublicationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.SetAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("OrganizeTournaments").AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        endpoints.MapPost("/api/registrations/{id:guid}/deck/visibility", async (Guid id, SetDeckVisibility command, TournamentRegistrationsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.SetVisibilityAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization().AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        return endpoints;
    }
}
