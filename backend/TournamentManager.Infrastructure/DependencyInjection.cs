using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Abstractions;
using TournamentManager.Application.Features.Games;
using TournamentManager.Infrastructure.Games;
using TournamentManager.Infrastructure.Health;
using TournamentManager.Infrastructure.Persistence;

namespace TournamentManager.Infrastructure;

// Composition/registrations de infraestructura. EF Core no se referencia desde Application o Domain.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TournamentDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta ConnectionStrings:TournamentDb. Configurá ConnectionStrings__TournamentDb.");
        }

        services.AddDbContext<TournamentDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IDatabaseReadinessProbe, PostgresReadinessProbe>();
        services.AddScoped<IGameCatalog, EfGameCatalog>();

        return services;
    }
}
