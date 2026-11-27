using System.Numerics;

namespace TournamentManager.Domain.Tournaments;

// Comparación exacta: el redondeo se reserva para la presentación.
public sealed record TieBreakerValue : IComparable<TieBreakerValue>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public TieBreakerValue(BigInteger numerator, BigInteger denominator)
    {
        if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / gcd; Denominator = denominator / gcd;
    }
    public static TieBreakerValue Zero => new(0, 1);
    public int CompareTo(TieBreakerValue? other) => other is null ? 1 :
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
    public static TieBreakerValue operator +(TieBreakerValue a, TieBreakerValue b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    public TieBreakerValue Divide(int count) => new(Numerator, Denominator * count);
    public static TieBreakerValue FromPoints(decimal points) => new(new BigInteger(points * 100), 100);
    public decimal Rounded(int places) => (decimal)((Numerator * BigInteger.Pow(10, places) * 2 + Denominator) / (Denominator * 2)) / (decimal)BigInteger.Pow(10, places);
}
