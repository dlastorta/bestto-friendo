using TournamentManager.Application.Abstractions;

namespace TournamentManager.Api.Features.System;

public sealed record HealthResponse(string Status);
public sealed record HealthEndpointMetadata;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var health = endpoints.MapGroup("/api/health").WithTags("System")
            .WithMetadata(new HealthEndpointMetadata()).AllowAnonymous();

        // Liveness: prueba que ASP.NET está ejecutándose; no requiere conexión a DB.
        health.MapGet("/live", () => TypedResults.Ok(new HealthResponse("healthy")))
            .WithName("GetLiveness");

        // Readiness: depende de un puerto abstracto, sin importar EF o DbContext.
        health.MapGet("/ready", async (
            IDatabaseReadinessProbe probe,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var canConnect = await probe.CanConnectAsync(cancellationToken);
                return canConnect
                    ? Results.Ok(new HealthResponse("ready"))
                    : Results.Problem(
                        title: "Database unavailable",
                        detail: "PostgreSQL no está disponible.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Readiness check: PostgreSQL no disponible");
                return Results.Problem(
                    title: "Database unavailable",
                    detail: "PostgreSQL no está disponible.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithName("GetReadiness");

        return endpoints;
    }
}
