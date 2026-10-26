using System.Numerics;

namespace Mordor.Core;

/// <summary>
/// A value as the x87 holds it inside a typed VB expression: a 64-bit mantissa, every operation
/// exact and then rounded to nearest, half to even. VB3 evaluates a typed expression on the x87
/// and rounds only at the store (the reading fn1485's measured 209 to 208 settles, and the
/// original's own spell prices confirmed, 2026-10-25; see
/// <see cref="Rules.Fn1485Price"/> and docs/FIDELITY.md). Used where a Double would round a step
/// the x87 keeps: <see cref="Rules.Fn12B2"/>. Value = <see cref="M"/> * 2^<see cref="E"/>.
/// </summary>
public readonly struct X87
{
    public readonly BigInteger M;
    public readonly int E;

    private X87(BigInteger m, int e) { M = m; E = e; }

    /// <summary>A Double (or anything narrower) exactly as it is stored.</summary>
    public static X87 Of(double v)
    {
        if (v == 0) return new X87(0, 0);
        long bits = BitConverter.DoubleToInt64Bits(v);
        int exp = (int)((bits >> 52) & 0x7FF);
        long mant = bits & 0xFFFFFFFFFFFFFL;
        if (exp == 0) exp = 1; else mant |= 1L << 52;
        return new X87(v < 0 ? -mant : mant, exp - 1075);
    }

    /// <summary>A whole number, exactly (FILD).</summary>
    public static X87 Of(long v) => Round(v, 0);

    public static X87 operator +(X87 a, X87 b)
    {
        int e = Math.Min(a.E, b.E);
        return Round((a.M << (a.E - e)) + (b.M << (b.E - e)), e);
    }

    public static X87 operator -(X87 a, X87 b) => a + new X87(-b.M, b.E);

    public static X87 operator *(X87 a, X87 b) => Round(a.M * b.M, a.E + b.E);

    public static X87 operator /(X87 a, X87 b)
    {
        if (b.M.IsZero) throw new DivideByZeroException();
        // enough quotient bits that the remainder decides the rounding exactly
        int k = 66 + (int)b.M.GetBitLength() - (int)BigInteger.Abs(a.M).GetBitLength();
        if (k < 0) k = 0;
        var q = BigInteger.DivRem(a.M << k, b.M, out var r);
        // a sticky bit for the remainder: below the 64 kept bits, so it only breaks ties
        q <<= 1;
        if (!r.IsZero) q += (a.M.Sign * b.M.Sign);
        return Round(q, a.E - b.E - k - 1);
    }

    /// <summary>m * 2^e rounded to a 64-bit mantissa, half to even.</summary>
    private static X87 Round(BigInteger m, int e)
    {
        if (m.IsZero) return new X87(0, 0);
        var abs = BigInteger.Abs(m);
        int drop = (int)abs.GetBitLength() - 64;
        if (drop <= 0) return new X87(m, e);
        var kept = abs >> drop;
        var rest = abs - (kept << drop);
        var half = BigInteger.One << (drop - 1);
        int c = rest.CompareTo(half);
        if (c > 0 || (c == 0 && !kept.IsEven)) kept += 1;
        return new X87(m.Sign < 0 ? -kept : kept, e + drop);
    }

    /// <summary>The store into an Integer or a Long (FISTP): nearest, half to even.</summary>
    public long RoundToLong()
    {
        if (E >= 0) return (long)(M << E);
        var abs = BigInteger.Abs(M);
        var whole = abs >> -E;
        var rest = abs - (whole << -E);
        var half = BigInteger.One << (-E - 1);
        int c = rest.CompareTo(half);
        if (c > 0 || (c == 0 && !whole.IsEven)) whole += 1;
        return (long)(M.Sign < 0 ? -whole : whole);
    }
}
