using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TournamentManager.Tests;

internal static class IdentityTestSupport
{
    public static async Task SetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response!.Token);
    }

    private sealed record CsrfResponse(string Token);
}

internal sealed class CatalogAdminTestAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "2496c1c9-02b0-41f0-8513-f7c0f2b68485"),
            new Claim(ClaimTypes.Role, "CatalogAdmin")], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
