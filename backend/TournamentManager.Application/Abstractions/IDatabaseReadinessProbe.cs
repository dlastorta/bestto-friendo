namespace TournamentManager.Application.Abstractions;

// Puerto usado por la API de salud. No expone EF, DbContext ni detalles de PostgreSQL.
public interface IDatabaseReadinessProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
