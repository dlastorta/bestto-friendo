using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class ByeEndpoints
{
    public static IEndpointRouteBuilder MapByeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tournaments/{id:guid}/byes").WithTags("Byes")
            .RequireAuthorization("OrganizeTournaments")
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        group.MapGet("", async (Guid id, TournamentByesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ListAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("", async (Guid id, AssignBye command, TournamentByesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.AssignAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}
