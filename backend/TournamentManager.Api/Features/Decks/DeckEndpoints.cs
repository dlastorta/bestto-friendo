using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Domain.Decks;
using TournamentManager.Domain.Games;

namespace TournamentManager.Api.Features.Decks;

public static class DeckEndpoints
{
    public static IEndpointRouteBuilder MapDeckEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var decks = endpoints.MapGroup("/api/decks").WithTags("Decks").RequireAuthorization()
            .AddEndpointFilter<CsrfFilter>().AddEndpointFilter<DeckErrorsFilter>();
        decks.MapGet("", async (Guid? gameId, bool? archived, DeckLibraryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.ListAsync(gameId, archived ?? false, ct)));
        decks.MapGet("/{id:guid}", async (Guid id, DeckLibraryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.GetAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        decks.MapGet("/{id:guid}/revisions", async (Guid id, DeckLibraryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HistoryAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        decks.MapPost("", async (CreateDeck command, DeckLibraryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.CreateAsync(command, ct);
            return Results.Created($"/api/decks/{result.Id}", result);
        });
        decks.MapPut("/{id:guid}", async (Guid id, EditDeck command, DeckLibraryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.EditAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        decks.MapPost("/{id:guid}/archive", async (Guid id, ArchiveDeck command, DeckLibraryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ArchiveAsync(id, command, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        return endpoints;
    }
}

public sealed class DeckErrorsFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        catch (DeckConflictException ex) { return Results.Conflict(new { message = ex.Message }); }
        catch (Exception ex) when (ex is DeckRuleViolationException or GameRuleViolationException)
        { return Results.ValidationProblem(new Dictionary<string, string[]> { ["deck"] = [ex.Message] }); }
    }
}
