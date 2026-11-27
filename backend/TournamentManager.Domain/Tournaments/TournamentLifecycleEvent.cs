namespace TournamentManager.Domain.Tournaments;

public sealed class TournamentLifecycleEvent
{
    private Guid[] _matchIds = [];
    private int[] _resultNumbers = [];
    private Guid[] _byeIds = [];
    private TournamentLifecycleEvent() { }
    internal TournamentLifecycleEvent(Guid tournamentId, int number, string kind, string reason, Guid actor,
        DateTimeOffset now, IEnumerable<TournamentMatch> matches, IEnumerable<TournamentBye> byes)
    {
        TournamentId = tournamentId; Number = number; Kind = kind; Reason = reason;
        ActorPlayerId = actor; RecordedAt = now;
        var games = matches.OrderBy(m => m.Id).ToArray();
        _matchIds = games.Select(m => m.Id).ToArray();
        _resultNumbers = games.Select(m => m.CurrentResult!.Number).ToArray();
        _byeIds = byes.Select(b => b.Id).ToArray();
    }
    public Guid TournamentId { get; private set; }
    public int Number { get; private set; }
    public string Kind { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public Guid ActorPlayerId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public IReadOnlyList<Guid> MatchIds => Array.AsReadOnly(_matchIds);
    public IReadOnlyList<int> ResultNumbers => Array.AsReadOnly(_resultNumbers);
    public IReadOnlyList<Guid> ByeIds => Array.AsReadOnly(_byeIds);
}
