using Microsoft.EntityFrameworkCore;
using TournamentManager.Application.Abstractions;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure.Health;

internal sealed class PostgresReadinessProbe(TournamentDbContext context) : IDatabaseReadinessProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        context.Database.CanConnectAsync(cancellationToken);
}
