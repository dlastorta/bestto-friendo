using TournamentManager.Domain.Tournaments;

namespace TournamentManager.Tests;

internal static class HistoricalTournamentFixture
{
    // Simulates a persisted snapshot from before Slice 12, without adding a production mutation API.
    internal static void UseManualPairing(Tournament tournament) =>
        typeof(FrozenTournamentRules).GetProperty(nameof(FrozenTournamentRules.PairingAlgorithmVersion))!
            .SetValue(tournament.StartedRules, 0);
}
