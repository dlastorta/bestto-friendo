using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TournamentManager.Application.Features.Games;
using TournamentManager.Application.Features.Games.CreateGame;
using Xunit;

namespace TournamentManager.Tests;

/// <summary>
/// Prueba integrada real habilitada al configurar RUN_POSTGRES_INTEGRATION=true.
/// Requiere una base aislada con la migración aplicada.
/// </summary>
public sealed class PostgresGameCatalogIntegrationTests
{
    [Fact]
    public async Task CreateAndGetGame_PersistToPostgres_WhenIntegrationDatabaseIsConfigured()
    {
        if (Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION") != "true")
            return;

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddAuthentication("CatalogTest")
                .AddScheme<AuthenticationSchemeOptions, CatalogAdminTestAuthentication>("CatalogTest", _ => { })));
        using var client = factory.CreateClient();
        await IdentityTestSupport.SetCsrfAsync(client);
        var name = $"Integration-{Guid.NewGuid():N}";
        var command = new CreateGameCommand(name, ["Casa A", "Casa B"], 1, 2);

        var createdResponse = await client.PostAsJsonAsync("/api/games", command);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotNull(created);

        var found = await client.GetFromJsonAsync<GameDto>($"/api/games/{created.Id}");
        Assert.NotNull(found);
        Assert.Equal(name, found.Name);
        Assert.Equal(2, found.Factions.Count);
    }
}
