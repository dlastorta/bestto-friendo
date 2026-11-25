namespace TournamentManager.Domain.Tournaments;

public sealed class TournamentRules
{
    private string[] _tieBreakers = [];
    private TournamentRules() { }
    public Guid TournamentId { get; private set; }
    public decimal WinPoints { get; private set; }
    public decimal DrawPoints { get; private set; }
    public decimal LossPoints { get; private set; }
    public bool ByesEnabled { get; private set; }
    public decimal ByePoints { get; private set; }
    public IReadOnlyList<string> TieBreakers => Array.AsReadOnly(_tieBreakers);
    public const int MaximumByesPerParticipant = 1;

    internal static TournamentRules Create(Guid tournamentId, decimal win, decimal draw, decimal loss,
        bool byesEnabled, decimal bye, IEnumerable<string>? tieBreakers)
    {
        foreach (var points in new[] { win, draw, loss, bye })
            if (points < 0 || points > 1_000_000 || decimal.Round(points, 2) != points)
                throw new TournamentRuleViolationException("Los puntos deben estar entre 0 y 1000000 y tener hasta 2 decimales.");
        if (tieBreakers is null)
            throw new TournamentRuleViolationException("Debés indicar la lista de desempates, aunque esté vacía.");
        var ordered = tieBreakers.ToArray();
        if (ordered.Any(value => value is not ("Buchholz" or "OMWPercentage" or "BuchholzCut")))
            throw new TournamentRuleViolationException("Hay criterios de desempate desconocidos.");
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new TournamentRuleViolationException("No se pueden repetir criterios de desempate.");
        return new TournamentRules
        {
            TournamentId = tournamentId, WinPoints = win, DrawPoints = draw, LossPoints = loss,
            ByesEnabled = byesEnabled, ByePoints = bye, _tieBreakers = ordered
        };
    }

    internal void Apply(TournamentRules rules)
    {
        WinPoints = rules.WinPoints; DrawPoints = rules.DrawPoints; LossPoints = rules.LossPoints;
        ByesEnabled = rules.ByesEnabled; ByePoints = rules.ByePoints; _tieBreakers = rules._tieBreakers.ToArray();
    }
}

public sealed class TournamentConflictException(string message) : Exception(message);
