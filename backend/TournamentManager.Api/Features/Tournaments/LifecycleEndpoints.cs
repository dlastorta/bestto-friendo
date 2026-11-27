using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class LifecycleEndpoints
{
    public static IEndpointRouteBuilder MapLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tournaments/{id:guid}").WithTags("Tournament lifecycle")
            .RequireAuthorization("OrganizeTournaments")
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        group.MapPost("/finish", async (Guid id, FinishTournament command, TournamentLifecycleHandler handler, CancellationToken ct) =>
        {
            var result = await handler.FinishAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("/reopen", async (Guid id, ReopenTournament command, TournamentLifecycleHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ReopenAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/lifecycle", async (Guid id, TournamentLifecycleHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HistoryAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPost("/rounds/regenerate", async (Guid id, RegenerateSwissRound command, SwissRoundsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.RegenerateAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapGet("/rounds/history", async (Guid id, SwissRoundsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HistoryAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}
