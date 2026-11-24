namespace TournamentManager.Domain.Games;

public sealed class GameRuleViolationException(string message) : Exception(message) { }
