using TournamentManager.Api.Features.System;
using TournamentManager.Api.Features.Games;
using TournamentManager.Application.Features.Games.CreateGame;
using TournamentManager.Application.Features.Games.GetGame;
using TournamentManager.Application.Features.Games.ListGames;
using TournamentManager.Infrastructure;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using System.Security.Claims;
using TournamentManager.Api.Features.Identity;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Application.Features.Decks;
using TournamentManager.Api.Features.Decks;
using TournamentManager.Api.Features.Tournaments;
using TournamentManager.Application.Features.Tournaments;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAccount, CurrentAccount>();
builder.Services.AddScoped<RegisterAccountHandler>();
builder.Services.AddScoped<PlayerProfileHandler>();
builder.Services.AddScoped<DeckLibraryHandler>();
builder.Services.AddScoped<TournamentDraftsHandler>();
builder.Services.AddScoped<TournamentRegistrationsHandler>();
builder.Services.AddScoped<TournamentMatchesHandler>();
builder.Services.AddScoped<TournamentByesHandler>();
builder.Services.AddScoped<StandingsHandler>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "TournamentManager.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnValidatePrincipal = async context =>
        {
            // Los health checks no usan identidad; una caída de cuentas no debe interceptarlos.
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<HealthEndpointMetadata>() is not null)
            {
                context.RejectPrincipal();
                return;
            }
            var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountAccess>();
            if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                context.RejectPrincipal();
                return;
            }
            var account = await accounts.FindAccountAsync(id, context.HttpContext.RequestAborted);
            if (account is null) { context.RejectPrincipal(); return; }
            var identity = (ClaimsIdentity)context.Principal!.Identity!;
            foreach (var role in identity.FindAll(ClaimTypes.Role).ToArray()) identity.RemoveClaim(role);
            if (account.CanManageCatalog) identity.AddClaim(new Claim(ClaimTypes.Role, "CatalogAdmin"));
            if (account.CanOrganizeTournaments) identity.AddClaim(new Claim(ClaimTypes.Role, "TournamentOrganizer"));
        };
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageCatalog", policy => policy.RequireAuthenticatedUser().RequireRole("CatalogAdmin"));
    options.AddPolicy("OrganizeTournaments", policy => policy.RequireAuthenticatedUser().RequireRole("TournamentOrganizer"));
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddScoped<CreateGameHandler>();
builder.Services.AddScoped<ListGamesHandler>();
builder.Services.AddScoped<GetGameHandler>();
// La API es la composition root: referencia Infraestructura solo aquí para registrar dependencias.
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapIdentityEndpoints();
app.MapSystemEndpoints();
app.MapGameEndpoints();
app.MapDeckEndpoints();
app.MapTournamentEndpoints();
app.MapRegistrationEndpoints();
app.MapMatchEndpoints();
app.MapByeEndpoints();
app.MapGet("/api/tournaments/{id:guid}/standings", async (Guid id, StandingsHandler handler, CancellationToken ct) =>
{
    var result = await handler.GetAsync(id, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization().AddEndpointFilter<TournamentErrorsFilter>();

app.Run();

// Necesario para WebApplicationFactory en las pruebas de integración.
public partial class Program { }
