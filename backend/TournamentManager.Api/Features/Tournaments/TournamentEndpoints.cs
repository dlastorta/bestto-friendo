using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Api.Features.Tournaments;

public static class TournamentEndpoints
{
    public static IEndpointRouteBuilder MapTournamentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tournaments = endpoints.MapGroup("/api/tournaments").WithTags("Tournaments")
            .RequireAuthorization("OrganizeTournaments")
            .AddEndpointFilter<TournamentErrorsFilter>().AddEndpointFilter<CsrfFilter>();
        tournaments.MapPost("", async (CreateTournamentDraft command, TournamentDraftsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.CreateAsync(command, ct);
            return Results.Created($"/api/tournaments/{result.Id}", result);
        });
        tournaments.MapGet("", async (Guid? gameId, TournamentDraftsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.ListAsync(gameId, ct)));
        tournaments.MapGet("/{id:guid}", async (Guid id, TournamentDraftsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        tournaments.MapPut("/{id:guid}/rules", async (Guid id, ConfigureTournamentRules command, TournamentDraftsHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ConfigureRulesAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}

public sealed class TournamentErrorsFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (TournamentAccessDeniedException) { return Results.Forbid(); }
        catch (TournamentConflictException ex) { return Results.Conflict(new { message = ex.Message }); }
        catch (TournamentRuleViolationException ex)
        { return Results.ValidationProblem(new Dictionary<string, string[]> { ["tournament"] = [ex.Message] }); }
    }
}
