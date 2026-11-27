using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class SwissRoundEndpoints
{
    public static IEndpointRouteBuilder MapSwissRoundEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tournaments/{id:guid}/rounds").WithTags("Rounds")
            .RequireAuthorization("OrganizeTournaments")
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        group.MapGet("", async (Guid id, SwissRoundsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ListAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("", async (Guid id, GenerateSwissRound command, SwissRoundsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GenerateAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}
