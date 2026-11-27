using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Abstractions;
using TournamentManager.Application.Features.Games;
using TournamentManager.Infrastructure.Games;
using TournamentManager.Infrastructure.Health;
using TournamentManager.Infrastructure.Persistence;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Infrastructure.Identity;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Infrastructure.Decks;
using TournamentManager.Application.Features.Tournaments;
using TournamentManager.Infrastructure.Tournaments;

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
        services.AddIdentityCore<IdentityUser<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<TournamentDbContext>().AddSignInManager();
        services.AddScoped<IAccountAccess, AccountAccess>();
        services.AddScoped<IPlayerProfiles, PlayerProfiles>();
        services.AddScoped<IDatabaseReadinessProbe, PostgresReadinessProbe>();
        services.AddScoped<IGameCatalog, EfGameCatalog>();
        services.AddScoped<IDeckLibrary, EfDeckLibrary>();
        services.AddScoped<ITournamentDrafts, EfTournamentDrafts>();
        services.AddScoped<ITournamentRegistrations, EfTournamentRegistrations>();
        services.AddScoped<ITournamentMatches, EfTournamentMatches>();
        services.AddScoped<ITournamentByes, EfTournamentByes>();

        return services;
    }
}
