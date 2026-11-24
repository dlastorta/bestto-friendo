namespace TournamentManager.Application.Features.Games;

public sealed class GameAlreadyExistsException(string name)
    : Exception($"Ya existe un juego con el nombre '{name}'.") { }
