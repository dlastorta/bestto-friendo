using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Games.CreateGame;
using TournamentManager.Application.Features.Games.GetGame;
using TournamentManager.Application.Features.Games.ListGames;
using TournamentManager.Domain.Games;
using TournamentManager.Api.Features.Identity;

namespace TournamentManager.Api.Features.Games;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var games = endpoints.MapGroup("/api/games").WithTags("Games");

        games.MapGet("", async (ListGamesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(ct)))
            .WithName("ListGames");

        games.MapGet("/{id:guid}", async (Guid id, GetGameHandler handler, CancellationToken ct) =>
        {
            var game = await handler.HandleAsync(id, ct);
            return game is null ? Results.NotFound() : Results.Ok(game);
        }).WithName("GetGame");

        games.MapPost("", async (CreateGameCommand command, CreateGameHandler handler, CancellationToken ct) =>
        {
            try
            {
                var created = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/games/{created.Id}", created);
            }
            catch (GameRuleViolationException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["game"] = [ex.Message]
                });
            }
            catch (GameAlreadyExistsException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        }).WithName("CreateGame").RequireAuthorization("ManageCatalog").AddEndpointFilter<CsrfFilter>();

        return endpoints;
    }
}
