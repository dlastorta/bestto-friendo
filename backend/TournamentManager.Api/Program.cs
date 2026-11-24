using TournamentManager.Api.Features.System;
using TournamentManager.Api.Features.Games;
using TournamentManager.Application.Features.Games.CreateGame;
using TournamentManager.Application.Features.Games.GetGame;
using TournamentManager.Application.Features.Games.ListGames;
using TournamentManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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

app.MapSystemEndpoints();
app.MapGameEndpoints();

app.Run();

// Necesario para WebApplicationFactory en las pruebas de integración.
public partial class Program { }
