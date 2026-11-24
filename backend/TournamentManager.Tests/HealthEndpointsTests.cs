using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentManager.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using TournamentManager.Api.Features.System;
using Xunit;

namespace TournamentManager.Tests;

public sealed class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Liveness_ReturnsOk_WithoutRequiringPostgres()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("healthy", body?.Status);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task Readiness_UsesApplicationPort_WithoutConnectingToPostgres(
        bool databaseIsReady,
        HttpStatusCode expectedStatus)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDatabaseReadinessProbe>();
                services.AddScoped<IDatabaseReadinessProbe>(
                    _ => new StubReadinessProbe(databaseIsReady));
            });
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private sealed class StubReadinessProbe(bool canConnect) : IDatabaseReadinessProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
            Task.FromResult(canConnect);
    }

    [Fact]
    public async Task Readiness_ReturnsServiceUnavailable_WhenPostgresCannotBeReached()
    {
        // Puerto 1 como destino deliberadamente inválido para mantener el test aislado.
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TournamentDb"] =
                        "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1"
                });
            });
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
