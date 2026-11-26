using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tournaments/{id:guid}/matches").WithTags("Matches")
            .RequireAuthorization("OrganizeTournaments")
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        group.MapGet("", async (Guid id, TournamentMatchesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ListAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/{matchId:guid}", async (Guid id, Guid matchId, TournamentMatchesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetAsync(id, matchId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("", async (Guid id, CreateMatch command, TournamentMatchesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.CreateAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Created($"/api/tournaments/{id}/matches/{result.Id}", result);
        });
        group.MapPost("/{matchId:guid}/result", async (Guid id, Guid matchId, RecordMatchResult command, TournamentMatchesHandler handler, CancellationToken ct) =>
        {
            var result = await handler.RecordAsync(id, matchId, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}
