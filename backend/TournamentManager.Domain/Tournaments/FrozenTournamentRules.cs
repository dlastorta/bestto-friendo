namespace TournamentManager.Domain.Tournaments;

// Copia histórica: los cálculos futuros deben usar estas reglas, nunca una configuración viva.
public sealed class FrozenTournamentRules
{
    private string[] _tieBreakers = [];
    private FrozenTournamentRules() { }
    public Guid TournamentId { get; private set; }
    public int SourceVersion { get; private set; }
    public int TieBreakerFormulaVersion { get; private set; }
    public int ByePolicyVersion { get; private set; }
    public int PairingAlgorithmVersion { get; private set; }
    public decimal WinPoints { get; private set; }
    public decimal DrawPoints { get; private set; }
    public decimal LossPoints { get; private set; }
    public decimal ByePoints { get; private set; }
    public bool ByesEnabled { get; private set; }
    public int MaximumByesPerParticipant { get; private set; }
    public IReadOnlyList<string> TieBreakers => Array.AsReadOnly(_tieBreakers);

    internal static FrozenTournamentRules Capture(Guid id, TournamentRules rules, int version) => new()
    {
        TournamentId = id, SourceVersion = version, TieBreakerFormulaVersion = 1, ByePolicyVersion = 1, PairingAlgorithmVersion = 1, WinPoints = rules.WinPoints, DrawPoints = rules.DrawPoints,
        LossPoints = rules.LossPoints, ByePoints = rules.ByePoints, ByesEnabled = rules.ByesEnabled,
        MaximumByesPerParticipant = TournamentRules.MaximumByesPerParticipant, _tieBreakers = rules.TieBreakers.ToArray()
    };
}
