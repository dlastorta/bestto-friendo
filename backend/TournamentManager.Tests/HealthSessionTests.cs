using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentManager.Application.Features.Identity;
using TournamentManager.Domain.Players;
using Xunit;
namespace TournamentManager.Tests;
public class HealthSessionTests
{
    [Theory]
    [InlineData("/api/health/live", HttpStatusCode.OK)]
    [InlineData("/api/health/ready", HttpStatusCode.ServiceUnavailable)]
    public async Task HealthContractSurvivesAccountDatabaseOutage(string path, HttpStatusCode expected)
    {
        var accounts = new StubAccounts();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountAccess>();
            services.AddSingleton<IAccountAccess>(accounts);
            services.RemoveAll<TournamentManager.Application.Abstractions.IDatabaseReadinessProbe>();
            services.AddSingleton<TournamentManager.Application.Abstractions.IDatabaseReadinessProbe>(new UnavailableProbe());
        }));
        using var client = factory.CreateClient();
        await IdentityTestSupport.SetCsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new LoginAccount("review@example.test", "test password"))).StatusCode);
        accounts.Unavailable = true;
        Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
    }
    private sealed class UnavailableProbe : TournamentManager.Application.Abstractions.IDatabaseReadinessProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class StubAccounts : IAccountAccess
    {
        public bool Unavailable { get; set; }
        private readonly Guid id = Guid.NewGuid();
        public Task<AccountResult> RegisterAsync(string email, string password, Player player, CancellationToken ct) => throw new NotSupportedException();
        public Task<AuthenticatedAccount?> AuthenticateAsync(string email, string password, CancellationToken ct) => Task.FromResult<AuthenticatedAccount?>(new(id, false));
        public Task<AuthenticatedAccount?> FindAccountAsync(Guid accountId, CancellationToken ct) => Unavailable ? throw new InvalidOperationException("Simulated account database outage") : Task.FromResult<AuthenticatedAccount?>(new(id, false));
    }
}

