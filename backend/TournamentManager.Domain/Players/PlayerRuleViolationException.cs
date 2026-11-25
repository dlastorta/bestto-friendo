namespace TournamentManager.Domain.Players;

public sealed class PlayerRuleViolationException(string message) : Exception(message);
