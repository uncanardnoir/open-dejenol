using System.Numerics;
using Mordor.Core;

/// <summary>
/// <c>x87</c>: where the port's 64-bit Doubles and the x87's 80-bit intermediates would give a
/// different whole number, over every shipped value a formula can take.
///
/// The working reading (docs/FIDELITY.md, fn1485) is that a typed VB expression is evaluated on the
/// x87 with a 64-bit mantissa and rounded only when it is stored - to a Single, a Double, a
/// Currency's ten-thousandths or an Integer/Long, half to even. A Variant in the expression goes
/// through the runtime's Variant helpers instead, which round every step to the Variant's type.
/// Each formula below is evaluated three ways: the port's own code, that x87 reading step by step
/// (<see cref="X"/>, every operation rounded to 64 bits), and exactly (rationals, with Log to 320
/// bits). A case is reported when the port and the x87 reading part, and counted as close when
/// the exact value is within a millionth of a rounding boundary, so a reader can see how near the
/// scan came.
///
/// The formulas are the ones whose every input is shipped data: fn12A1's experience (every guild,
/// race and level 1-1000), fn030F's item use level (every item and guild), adInterval's A/D
/// stretch (every guild, levels 1-1000), and fn12B2's guild gold (every guild, levels 1-1000).
/// Anything with a roll, a character's own stats or hits in it is not shipped data and is not here.
/// </summary>
public static class X87Probe
{
    public static int Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var r = Scan(items);
        foreach (var line in r.Lines) Console.WriteLine(line);
        return r.Differ;
    }

    public sealed record Result(int Differ, List<string> Lines);

    public static Result Scan(IReadOnlyList<Item> items)
    {
        var lines = new List<string>();
        int differ = 0;

        // ---- fn12A1 (MAINMOD2.BAS:2118-2138)
        {
            int n = 0, bad = 0, close = 0, badNarrow = 0; string first = "";
            foreach (var g in GuildData.All)
                foreach (var race in RaceData.All)
                    for (int level = 1; level <= 1000; level += 1)
                    {
                        n++;
                        long port = Rules.Fn12A1(level, g.ExpFactor, race.ExpFactor);
                        var (x87, nearest) = Fn12A1X(level, g.ExpFactor, race.ExpFactor);
                        if (nearest < 1e-6) close++;
                        if (port != x87) { bad++; if (first == "") first = $"{g.Name} {race.Name} L{level}: port {port}, x87 {x87}"; }
                        if (port != Fn12A1X(level, g.ExpFactor, race.ExpFactor, narrow: true).Value) badNarrow++;
                    }
            var hn = GuildData.All[0]; var hu = RaceData.All[0];
            lines.Add($"  anchors {hn.Name} {hu.Name}: L79 port {Rules.Fn12A1(79, hn.ExpFactor, hu.ExpFactor)} x87 {Fn12A1X(79, hn.ExpFactor, hu.ExpFactor).Value}; L99 port {Rules.Fn12A1(99, hn.ExpFactor, hu.ExpFactor)} x87 {Fn12A1X(99, hn.ExpFactor, hu.ExpFactor).Value}");
            lines.Add($"fn12A1 experience: {n} cases, {bad} differ, {close} at or within a millionth of a boundary{(first != "" ? " - first: " + first : "")}; with l038C / 25 narrowed to a Single as the port had it before 2026-10-21, {badNarrow} differ");
            differ += bad;
        }

        // ---- fn030F (MODULE1.BAS:123-132): l05E2 = (l05E0 + l05E0 * 2.97#) * Items(p).M560D
        {
            int n = 0, bad = 0, close = 0; string first = "";
            foreach (var it in items)
                foreach (var g in GuildData.All)
                {
                    n++;
                    int port = Character.UseLevel(it, g);
                    var f = X.Of((double)g.ItemUseFactor);
                    var v = X.Mul(X.Add(f, X.Mul(f, X.Of(2.97))), X.Of(it.LevelScale));
                    var exact = (Q.Of((double)g.ItemUseFactor) + Q.Of((double)g.ItemUseFactor) * Q.Of(2.97)) * Q.Of(it.LevelScale);
                    if (Q.FromHalf(exact) < 1e-6) close++;
                    int x87 = (int)BigInteger.Max(1, Q.RoundEven(v));
                    if (port != x87) { bad++; if (first == "") first = $"{it.Name.Trim()} {g.Name}: port {port}, x87 {x87}"; }
                }
            lines.Add($"fn030F item use level: {n} cases, {bad} differ, {close} at or within a millionth of a boundary{(first != "" ? " - first: " + first : "")}");
            differ += bad;
        }

        // ---- adInterval (MAINMOD2.BAS:2680-2707)
        {
            var portAd = typeof(Rules).GetMethod("AdInterval", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            int n = 0, bad = 0, close = 0; string first = "";
            for (int gi = 0; gi < GuildData.All.Count; gi++)
            {
                var g = GuildData.All[gi];
                for (int level = 1; level <= 1000; level++)
                {
                    n++;
                    int port = (int)portAd.Invoke(null, new object[] { g, level, gi == 0 })!;
                    var (x87, nearest) = AdIntervalX(g, gi, level);
                    if (nearest < 1e-6) close++;
                    if (port != x87) { bad++; if (first == "") first = $"{g.Name} L{level}: port {port}, x87 {x87}"; }
                }
            }
            lines.Add($"adInterval A/D stretch: {n} cases, {bad} differ, {close} at or within a millionth of a boundary{(first != "" ? " - first: " + first : "")}");
            differ += bad;
        }

        // ---- fn12B2 (MAINMOD2.BAS:2140-2155), the guild's gold for a level, which Rules.Fn12B2 evaluates on Mordor.Core.X87
        {
            int n = 0, bad = 0, close = 0; string first = "";
            foreach (var g in GuildData.All)
                for (int level = 1; level <= 1000; level++)
                {
                    n++;
                    long port = Rules.Fn12B2(level, g.GoldFactor);
                    var (x87, nearest) = Fn12B2X(level, g.GoldFactor);
                    if (nearest < 1e-6) close++;
                    if (port != x87) { bad++; if (first == "") first = $"{g.Name} L{level}: port {port}, x87 {x87}"; }
                }
            lines.Add($"fn12B2 guild gold: {n} cases, {bad} differ, {close} at or within a millionth of a boundary{(first != "" ? " - first: " + first : "")}");
            differ += bad;
        }
        return new Result(differ, lines);
    }

    /// <summary>
    /// fn12A1 on the x87. The loop's last pass alone counts, and it goes through the Variant
    /// counter l0386, so each of its steps is the Variant helper's - rounded as the port rounds
    /// them; its stores are Currencies. The last line is typed throughout - Currency, Single,
    /// Double, and Log - so it is one x87 expression: <c>l0392@ * (((Log(l038C) / Log(l038C + 1)) + 1)
    /// - .7# + (l038C / 25))</c>, the Single quotient not narrowed to a Single.
    /// </summary>
    private static (long Value, double Nearest) Fn12A1X(int level, float guildExp, float raceExp, bool narrow = false)
    {
        static long Cur(double x) => (long)Math.Round(x * 10000.0, MidpointRounding.ToEven);
        float l038A = guildExp, l038C = raceExp + 1;
        long l0392 = 0;
        if (level >= 1)
        {
            int i = level;
            long l038E = Cur((float)(i + i) * l038A);
            long l0390 = Cur((float)i / 2 * 2 + (i - 1));
            // (l038E@ / 12) * 500 is typed, so the x87 keeps it until the Variant multiply by l0386
            // takes it as a Double; the rest of the line is the Variant helpers' Doubles
            double t = Q.ToDouble(X.Mul(X.Div(X.Div(new Q(l038E, 1), Q.Of(10000)), Q.Of(12)), Q.Of(500)));
            l0392 = Cur((t * i + 344 * l0390 / 10000.0 - 2) / 10);
        }
        var a = X.Of((double)l038C);
        var f = X.Add(X.Sub(X.Add(X.Div(X.Log(a), X.Log(X.Add(a, X.Of(1)))), X.Of(1)), X.Of(.7)), narrow ? Q.Of((double)(float)(l038C / 25)) : X.Div(a, X.Of(25)));
        var prod = X.Mul(X.Div(new Q(l0392, 1), Q.Of(10000)), f);         // a Currency is loaded as its ten-thousandths over 10000
        var cur = Q.RoundEven(X.Mul(prod, Q.Of(10000)));                  // and stored back the same way, FISTP rounding
        var exact = new Q(l0392, 10000) * ((Q.Ln(Q.Of((double)l038C)) / Q.Ln(Q.Of((double)l038C) + Q.Of(1))) + Q.Of(1) - Q.Of(.7) + Q.Of((double)l038C) / Q.Of(25));
        double near = Math.Min(Q.FromHalf(exact * Q.Of(10000)), Q.FromHalf(new Q(cur, 10000)));
        return ((long)Q.RoundEven(new Q(cur, 10000)), near);                // fn12A1 = l0392@, into a Long
    }

    /// <summary>
    /// adInterval on the x87: <c>l0434 = ((Int(HitsCapLevel) / 2) + (l042E! / 18)) / (AdEvery +
    /// l0430!)</c> one expression into an Integer, <c>l042E! = l042E! + l0434</c> into a Single, and
    /// at the end <c>l042E! * 1.1#</c> into the Single and CInt of that.
    /// </summary>
    private static (int Value, double Nearest) AdIntervalX(GuildDef g, int guildIndex, int level)
    {
        double near = 1;
        Q acc;
        if (level > g.HitsCapLevel)
        {
            acc = Q.Of(g.HitsCapLevel);
            var bonus = Q.Of(guildIndex == 0 ? 3.0 : 0.0);
            for (int i = g.HitsCapLevel; i <= level; i++)
            {
                var top = X.Add(X.Div(Q.Of(g.HitsCapLevel), Q.Of(2)), X.Div(acc, Q.Of(18)));
                var q = X.Div(top, X.Add(Q.Of((double)g.AdEvery), bonus));
                var exact = (Q.Of(g.HitsCapLevel) / Q.Of(2) + acc / Q.Of(18)) / (Q.Of((double)g.AdEvery) + bonus);
                near = Math.Min(near, Q.FromHalf(exact));
                var step = Q.RoundEven(q);
                if (step < short.MinValue || step > short.MaxValue) break;   // an Integer overflow; no shipped guild gets here
                acc = Q.RoundBits(acc + new Q(step, 1), 24);
                if (acc.CompareTo(Q.Of(level)) > 0) break;
            }
            if (acc.CompareTo(Q.Of(g.HitsCapLevel)) == 0) acc = Q.Of(level + 1);
        }
        else acc = Q.Of(0);
        if (level > g.AdMaxLevel) acc = Q.RoundBits(X.Mul(acc, Q.Of(1.1)), 24);
        near = Math.Min(near, Q.FromHalf(acc));
        return ((int)Q.RoundEven(acc), near);
    }

    /// <summary>
    /// fn12B2 on the x87: <c>l03A8&amp; = l03A8&amp; + (((l039E / 2) * l03A6&amp;) * 60.2#) / l039E</c> is
    /// typed throughout (the Variant counter is not in it), one expression into the Long; then
    /// <c>l03A8&amp; + (l03A8&amp; * (Gold / 10))</c>, into the Long the function returns.
    /// </summary>
    private static (long Value, double Nearest) Fn12B2X(int level, int guildGold)
    {
        double near = 1;
        var L = Q.Of(level);
        BigInteger acc = 0;
        for (int i = 1; i <= level; i++)
        {
            var l03A6 = Q.RoundEven(Q.Of((double)((float)i / 2 * 2 + (i - 1))));
            var v = X.Add(new Q(acc, 1), X.Div(X.Mul(X.Mul(X.Div(L, Q.Of(2)), new Q(l03A6, 1)), Q.Of(60.2)), L));
            var exact = new Q(acc, 1) + L / Q.Of(2) * new Q(l03A6, 1) * Q.Of(60.2) / L;
            near = Math.Min(near, Q.FromHalf(exact));
            acc = Q.RoundEven(v);
        }
        var a = new Q(acc, 1);
        var total = X.Add(a, X.Mul(a, X.Div(Q.Of((short)guildGold), Q.Of(10))));
        near = Math.Min(near, Q.FromHalf(a + a * (Q.Of((short)guildGold) / Q.Of(10))));
        return ((long)Q.RoundEven(total), near);
    }

    /// <summary>The x87's arithmetic: each operation exact, then rounded to a 64-bit mantissa.</summary>
    private static class X
    {
        public static Q Of(double d) => Q.Of(d);
        public static Q Add(Q a, Q b) => Q.RoundBits(a + b, 64);
        public static Q Sub(Q a, Q b) => Q.RoundBits(a - b, 64);
        public static Q Mul(Q a, Q b) => Q.RoundBits(a * b, 64);
        public static Q Div(Q a, Q b) => Q.RoundBits(a / b, 64);
        /// <summary>FLDLN2 / FYL2X: the natural log, here correctly rounded to 64 bits.</summary>
        public static Q Log(Q a) => Q.RoundBits(Q.Ln(a), 64);
    }

    /// <summary>An exact rational.</summary>
    private readonly struct Q : IComparable<Q>
    {
        public readonly BigInteger N, D;
        public Q(BigInteger n, BigInteger d)
        {
            if (d.Sign < 0) { n = -n; d = -d; }
            var g = BigInteger.GreatestCommonDivisor(n, d);
            if (!g.IsZero && !g.IsOne) { n /= g; d /= g; }
            N = n; D = d.IsZero ? BigInteger.One : d;
        }

        /// <summary>A Double exactly as it is stored.</summary>
        public static Q Of(double v)
        {
            if (v == 0) return new Q(0, 1);
            long bits = BitConverter.DoubleToInt64Bits(v);
            int exp = (int)((bits >> 52) & 0x7FF);
            long mant = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else mant |= 1L << 52;
            exp -= 1075;
            BigInteger m = v < 0 ? -mant : mant;
            return exp >= 0 ? new Q(m << exp, 1) : new Q(m, BigInteger.One << -exp);
        }

        public static Q operator +(Q a, Q b) => new(a.N * b.D + b.N * a.D, a.D * b.D);
        public static Q operator -(Q a, Q b) => new(a.N * b.D - b.N * a.D, a.D * b.D);
        public static Q operator *(Q a, Q b) => new(a.N * b.N, a.D * b.D);
        public static Q operator /(Q a, Q b) => new(a.N * b.D, a.D * b.N);
        public int CompareTo(Q o) => (N * o.D).CompareTo(o.N * D);

        /// <summary>Rounded to <paramref name="bits"/> of mantissa, half to even.</summary>
        public static Q RoundBits(Q x, int bits)
        {
            if (x.N.IsZero) return x;
            var n = BigInteger.Abs(x.N); var d = x.D;
            int shift = bits - (int)(n.GetBitLength() - d.GetBitLength());
            BigInteger q, r, den;
            while (true)
            {
                var num = shift >= 0 ? n << shift : n;
                den = shift >= 0 ? d : d << -shift;
                q = BigInteger.DivRem(num, den, out r);
                if (q >= BigInteger.One << bits) { shift--; continue; }
                if (q < BigInteger.One << (bits - 1)) { shift++; continue; }
                break;
            }
            int c = (r * 2).CompareTo(den);
            if (c > 0 || (c == 0 && !q.IsEven)) q++;
            if (x.N.Sign < 0) q = -q;
            return shift >= 0 ? new Q(q, BigInteger.One << shift) : new Q(q << -shift, 1);
        }

        /// <summary>The Double nearest, half to even.</summary>
        public static double ToDouble(Q x) { var r = RoundBits(x, 53); return (double)r.N / (double)r.D; }

        /// <summary>To the nearest whole number, half to even - VB's CInt, CLng and its Integer stores.</summary>
        public static BigInteger RoundEven(Q x)
        {
            var q = BigInteger.DivRem(x.N, x.D, out var r);
            if (x.N.Sign < 0 && !r.IsZero) { q -= 1; r += x.D; }   // floor
            int c = (r * 2).CompareTo(x.D);
            if (c > 0 || (c == 0 && !q.IsEven)) q += 1;
            return q;
        }

        /// <summary>How far the value is from the nearest half (a rounding boundary).</summary>
        public static double FromHalf(Q x)
        {
            var q = BigInteger.DivRem(x.N, x.D, out var r);
            if (r.Sign < 0) r += x.D;
            // |r/D - 1/2|
            return Math.Abs((double)(r * 2 - x.D) / (double)(x.D * 2));
        }

        /// <summary>The natural log to 320 bits: k ln 2 + 2 atanh((m - 1) / (m + 1)), m in [1, 2).</summary>
        public static Q Ln(Q x)
        {
            const int P = 320;
            int k = (int)(x.N.GetBitLength() - x.D.GetBitLength());
            BigInteger mn = x.N, md = x.D;
            if (k >= 0) md <<= k; else mn <<= -k;
            if (mn < md) { k--; mn <<= 1; }
            BigInteger Atanh2(BigInteger yn, BigInteger yd)   // 2 atanh(yn / yd), fixed point P
            {
                var y = (yn << P) / yd;
                var y2 = (y * y) >> P;
                BigInteger term = y, sum = 0;
                for (int i = 1; !term.IsZero; i += 2)
                {
                    sum += term / i;
                    term = (term * y2) >> P;
                }
                return sum * 2;
            }
            var ln2 = Atanh2(1, 3);
            var lnm = Atanh2(mn - md, mn + md);
            return new Q(lnm + ln2 * k, BigInteger.One << P);
        }
    }
}
