using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Players;

namespace TournamentManager.Api.Features.Identity;

public sealed class CurrentAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public bool CanManageCatalog => accessor.HttpContext?.User.IsInRole("CatalogAdmin") == true;
    public bool CanOrganizeTournaments => accessor.HttpContext?.User.IsInRole("TournamentOrganizer") == true;
    public Guid? AccountId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : null;
}

public sealed class CsrfFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem(statusCode: 400, title: "Sesión de formulario inválida. Recargá e intentá otra vez.");
            }
        }
        return await next(context);
    }
}

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Identity").AddEndpointFilter<CsrfFilter>();
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        });

        auth.MapPost("/register", async (RegisterAccount command, RegisterAccountHandler handler, CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(command, ct);
                if (result.Conflict) return Results.Conflict(new { message = "Ya existe una cuenta con ese email." });
                if (!result.Succeeded) return Results.ValidationProblem(result.Errors.ToDictionary());
                return Results.StatusCode(StatusCodes.Status201Created);
            }
            catch (PlayerRuleViolationException ex) { return InvalidProfile(ex); }
        }).RequireRateLimiting("accounts");

        auth.MapPost("/login", async (LoginAccount command, IAccountAccess accounts, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(command.Email) || command.Email.Length > 254 ||
                string.IsNullOrEmpty(command.Password) || command.Password.Length > 128)
                return Results.Unauthorized();
            var account = await accounts.AuthenticateAsync(command.Email, command.Password, ct);
            if (account is null) return Results.Unauthorized();
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, account.Id.ToString()) };
            if (account.CanManageCatalog) claims.Add(new(ClaimTypes.Role, "CatalogAdmin"));
            if (account.CanOrganizeTournaments) claims.Add(new(ClaimTypes.Role, "TournamentOrganizer"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
            await context.SignInAsync(IdentityConstants.ApplicationScheme, principal,
                new AuthenticationProperties { IsPersistent = false });
            return Results.NoContent();
        }).RequireRateLimiting("accounts");

        auth.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        var profile = endpoints.MapGroup("/api/me").WithTags("Identity")
            .RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        profile.MapGet("", async (PlayerProfileHandler handler, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = await handler.GetAsync(ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        profile.MapPut("", async (RenamePlayer command, PlayerProfileHandler handler, CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RenameAsync(command, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            }
            catch (PlayerRuleViolationException ex) { return InvalidProfile(ex); }
        });
        return endpoints;
    }

    private static IResult InvalidProfile(PlayerRuleViolationException ex) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = [ex.Message] });
}
