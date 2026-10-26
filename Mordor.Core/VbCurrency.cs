using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// VB's <c>Currency</c>: a 64-bit integer counting ten-thousandths, so four decimal places and
/// nothing lost to binary fractions, from -922,337,203,685,477.5808 to 922,337,203,685,477.5807.
/// The character record keeps <c>Gold</c> and <c>BankGold</c> in it (MAIN.TXT:332, 335), as it
/// does a chest's <c>Fight.Gold</c> (MAIN.TXT:105) and a Hall of Records value (MAIN.TXT:846).
/// <para>
/// What VB does with one, and this type with it:
/// </para>
/// <list type="bullet">
/// <item>Currency with Currency, Integer or Long (<c>+</c>, <c>-</c>, comparisons) is exact: the
/// whole number is scaled and nothing rounds. Here a <c>long</c> converts implicitly.</item>
/// <item>Currency with a Single or Double is worked in Double, and storing the answer back in a
/// Currency rounds it to four places, half to even (<see cref="FromDouble"/>). So
/// <c>Fight.Gold = Fight.Gold * 1.5#</c> is <c>FromDouble(gold.ToDouble() * 1.5)</c>.</item>
/// <item>Storing a Currency in an Integer or Long - an assignment, a <c>Long</c> parameter, or
/// <c>CLng</c> - rounds half to even to a whole number (<see cref="RoundToWhole"/>).</item>
/// <item>Leaving the range is VB's run-time error 6, Overflow: an <see cref="OverflowException"/>.</item>
/// </list>
/// How it is shown is not here but where the original shows it: <c>formatNumber</c>
/// (<see cref="CharStatFile.FormatNumber(VbCurrency)"/>) for every gold figure on screen, and
/// <c>Str$</c> (<see cref="Str"/>) for the one place the original writes the bare value, the Bank's
/// "All" buttons.
/// </summary>
public readonly struct VbCurrency : IEquatable<VbCurrency>, IComparable<VbCurrency>, IFormattable
{
    /// <summary>Ten-thousandths in one.</summary>
    public const long Scale = 10_000;

    /// <summary>The scaled integer: the value times 10,000.</summary>
    public long Raw { get; }

    private VbCurrency(long raw) { Raw = raw; }

    public static readonly VbCurrency Zero = default;
    public static readonly VbCurrency MaxValue = new(long.MaxValue);
    public static readonly VbCurrency MinValue = new(long.MinValue);

    /// <summary>A value straight from its ten-thousandths.</summary>
    public static VbCurrency FromRaw(long raw) => new(raw);

    /// <summary>An Integer or Long into a Currency: exact, unless it is past the range.</summary>
    public static implicit operator VbCurrency(long whole) => new(checked(whole * Scale));

    /// <summary>
    /// A Single or Double into a Currency (<c>CCur</c>, or an assignment): rounded to four places,
    /// half to even, and an Overflow past the range.
    /// </summary>
    public static VbCurrency FromDouble(double d)
    {
        double scaled = Math.Round(d * Scale, MidpointRounding.ToEven);
        // 2^63 is the first double past the range; anything at or beyond it, or NaN, overflows
        if (double.IsNaN(scaled) || scaled >= 9223372036854775808.0 || scaled < -9223372036854775808.0)
            throw new OverflowException("Currency overflow");
        return new((long)scaled);
    }

    /// <summary>A decimal into a Currency, rounded to four places half to even (how a typed amount lands).</summary>
    public static VbCurrency FromDecimal(decimal m)
    {
        decimal scaled = Math.Round(m * Scale, MidpointRounding.ToEven);
        if (scaled > long.MaxValue || scaled < long.MinValue) throw new OverflowException("Currency overflow");
        return new((long)scaled);
    }

    public double ToDouble() => (double)Raw / Scale;
    public decimal ToDecimal() => (decimal)Raw / Scale;

    /// <summary>
    /// Into a whole number as VB stores a Currency in an Integer or Long: half to even, so 2.5 is 2
    /// and 3.5 is 4. (VB's Long stops at 2,147,483,647; the port's whole-gold figures are 64-bit,
    /// as they were before this type, so a fortune past that does not overflow here.)
    /// </summary>
    public long RoundToWhole()
    {
        long q = Raw / Scale, r = Raw % Scale;          // C# truncates toward zero
        long half = Scale / 2;
        long ar = Math.Abs(r);
        if (ar > half || (ar == half && (q & 1) != 0)) q += Math.Sign(r);
        return q;
    }

    /// <summary>The whole part, the fraction dropped toward zero. What an older save holds.</summary>
    public long Whole => Raw / Scale;

    /// <summary>True when there is no fraction.</summary>
    public bool IsWhole => Raw % Scale == 0;

    public static VbCurrency operator +(VbCurrency a, VbCurrency b) => new(checked(a.Raw + b.Raw));
    public static VbCurrency operator -(VbCurrency a, VbCurrency b) => new(checked(a.Raw - b.Raw));
    public static VbCurrency operator -(VbCurrency a) => new(checked(-a.Raw));

    /// <summary>Currency times an Integer or Long stays Currency and is exact.</summary>
    public static VbCurrency operator *(VbCurrency a, long b) => new(checked(a.Raw * b));

    // ---- without throwing: what a call site with an error trap, or none, is given ----
    //
    // VB raises Overflow (error 6) at the statement, and what happens next is the routine's own
    // error handling: On Error Resume Next skips that one statement, a trap leaves the routine
    // where it got to, and no handler at all ends the program. The port must never die, so each
    // call site asks with these and does what its original does (docs/FIDELITY.md, "Gold is VB
    // Currency", Overflow).

    /// <summary><c>a + b</c>, or false where VB would raise Overflow.</summary>
    public static bool TryAdd(VbCurrency a, VbCurrency b, out VbCurrency sum)
    {
        long r = unchecked(a.Raw + b.Raw);
        // overflow happened iff both operands have the same sign and the result's sign differs
        bool ok = ((a.Raw ^ r) & (b.Raw ^ r)) >= 0;
        sum = ok ? new(r) : Zero;
        return ok;
    }

    /// <summary><c>a - b</c>, or false where VB would raise Overflow.</summary>
    public static bool TrySubtract(VbCurrency a, VbCurrency b, out VbCurrency difference)
    {
        long r = unchecked(a.Raw - b.Raw);
        bool ok = ((a.Raw ^ b.Raw) & (a.Raw ^ r)) >= 0;
        difference = ok ? new(r) : Zero;
        return ok;
    }

    /// <summary><c>a + b</c> held to the range. For a figure that is only compared or shown.</summary>
    public static VbCurrency AddSaturating(VbCurrency a, VbCurrency b) =>
        TryAdd(a, b, out var s) ? s : b.Raw > 0 ? MaxValue : MinValue;

    /// <summary><c>a - b</c> held to the range. For a figure that is only compared or shown.</summary>
    public static VbCurrency SubtractSaturating(VbCurrency a, VbCurrency b) =>
        TrySubtract(a, b, out var d) ? d : b.Raw < 0 ? MaxValue : MinValue;

    /// <summary><see cref="FromDouble"/>, or false where it would overflow.</summary>
    public static bool TryFromDouble(double d, out VbCurrency value)
    {
        double scaled = Math.Round(d * Scale, MidpointRounding.ToEven);
        bool ok = !double.IsNaN(scaled) && scaled < 9223372036854775808.0 && scaled >= -9223372036854775808.0;
        value = ok ? new((long)scaled) : Zero;
        return ok;
    }

    /// <summary>A whole number held to the range: what a save that holds more than a Currency can is read as.</summary>
    public static VbCurrency Saturate(long whole) =>
        whole > MaxValue.Whole ? MaxValue : whole < MinValue.Whole ? MinValue : whole;

    public static bool operator ==(VbCurrency a, VbCurrency b) => a.Raw == b.Raw;
    public static bool operator !=(VbCurrency a, VbCurrency b) => a.Raw != b.Raw;
    public static bool operator <(VbCurrency a, VbCurrency b) => a.Raw < b.Raw;
    public static bool operator >(VbCurrency a, VbCurrency b) => a.Raw > b.Raw;
    public static bool operator <=(VbCurrency a, VbCurrency b) => a.Raw <= b.Raw;
    public static bool operator >=(VbCurrency a, VbCurrency b) => a.Raw >= b.Raw;

    public static VbCurrency Min(VbCurrency a, VbCurrency b) => a <= b ? a : b;
    public static VbCurrency Max(VbCurrency a, VbCurrency b) => a >= b ? a : b;

    public bool Equals(VbCurrency other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is VbCurrency c && Equals(c);
    public override int GetHashCode() => Raw.GetHashCode();
    public int CompareTo(VbCurrency other) => Raw.CompareTo(other.Raw);

    /// <summary>The plain value, invariant, with no trailing zeros: "1500", "10.5", "-0.25". For logs and probes.</summary>
    public override string ToString() => ToDecimal().ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>A .NET format string, as the decimal value takes it. Never how the game shows gold: that is FormatNumber.</summary>
    public string ToString(string? format, IFormatProvider? provider) =>
        format == null ? ToString() : ToDecimal().ToString(format, provider);

    /// <summary>
    /// VB's <c>Str$</c> of a Currency: a leading blank for a value that is not negative, the shortest
    /// digits, a "." whatever the locale, and no zero before the point - <c>Str$(10.5)</c> is
    /// " 10.5", <c>Str$(0.25)</c> is " .25", <c>Str$(-0.25)</c> is "-.25", <c>Str$(1500)</c> is " 1500".
    /// </summary>
    public string Str()
    {
        string digits = Math.Abs(ToDecimal()).ToString("0.####", CultureInfo.InvariantCulture);
        if (digits.StartsWith("0.", StringComparison.Ordinal)) digits = digits.Substring(1);
        return (Raw < 0 ? "-" : " ") + digits;
    }

    /// <summary>
    /// VB's <c>CStr</c> of a Currency: no leading blank, the locale's decimal point, a zero before
    /// it, no trailing zeros - "14", "10.5", "0.25". The Hall shows its stat and skill records so.
    /// </summary>
    public string CStr() => ToDecimal().ToString("0.####", CultureInfo.CurrentCulture);

    /// <summary>
    /// How a Currency goes into the port's JSON without breaking a build from before it: the whole
    /// number stays where it always was, as an integer (<see cref="Whole"/>), and only a value
    /// with a fraction writes this exact figure beside it - null, and so not written, otherwise.
    /// An older build skips the field it does not know and reads the whole part; a save it then
    /// writes has no exact figure, so nothing stale is left to be believed. See
    /// <see cref="FromSave"/>.
    /// </summary>
    public decimal? ExactForSave => IsWhole ? null : ToDecimal();

    /// <summary>
    /// The value back from the two fields: the exact figure when there is one and it agrees with
    /// the whole part (a file edited by hand may not), else the whole part alone - which is every
    /// save written before gold kept its fraction, read unchanged.
    /// Never throws: a whole part past the range is held to it (<see cref="Saturate"/>), and an
    /// exact figure that cannot be a Currency is passed over, so a damaged figure costs the
    /// figure and never the file.
    /// </summary>
    public static VbCurrency FromSave(long whole, decimal? exact)
    {
        if (exact is decimal m)
        {
            try
            {
                var e = FromDecimal(m);
                if (e.Whole == whole) return e;
            }
            catch (OverflowException) { }
        }
        return Saturate(whole);
    }

    /// <summary>
    /// <c>CCur</c> of a string, as the Bank's text boxes take it (FRMCITYB.BAS:308): what
    /// <see cref="VbConvert.TryCLng"/> reads - blanks, a sign, the locale's separators and currency
    /// symbol, an E or D exponent, <c>&amp;H</c>/<c>&amp;O</c> - but kept to four places, half to
    /// even, rather than to a whole number. False for anything that is not a number or is past the
    /// range: the original's <c>On Error Resume Next</c> leaves the amount at nought.
    /// </summary>
    public static bool TryParse(string text, out VbCurrency value)
    {
        value = Zero;
        string s = text.Trim(' ', '\t');
        if (s.Length == 0) return false;
        if (s[0] == '&')
        {
            if (!VbConvert.TryCLng(s, out int n)) return false;
            value = n;
            return true;
        }
        string e = s.Replace('D', 'E').Replace('d', 'E');
        const NumberStyles Styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint
            | NumberStyles.AllowExponent | NumberStyles.AllowCurrencySymbol;
        if (!decimal.TryParse(e, Styles, CultureInfo.CurrentCulture, out decimal m)) return false;
        try { value = FromDecimal(m); return true; }
        catch (OverflowException) { return false; }
    }
}

/// <summary>
/// The whole part of a saved Currency, read so that no number can fail the file: an integer past
/// a long (or a number with a fraction, from a hand edit) is held to the range rather than
/// thrown, as <see cref="VbCurrency.Saturate"/> then holds it to Currency's. Written as before.
/// </summary>
public sealed class SavedWholeJson : System.Text.Json.Serialization.JsonConverter<long>
{
    public override long Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.Number) { reader.Skip(); return 0; }
        if (reader.TryGetInt64(out long n)) return n;
        double d = reader.GetDouble();   // a JSON number always reads as a double, if a rough one
        return double.IsNaN(d) ? 0 : d >= 9.2e18 ? long.MaxValue : d <= -9.2e18 ? long.MinValue : (long)Math.Truncate(d);
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, long value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// The exact figure of a saved Currency: anything that is not a number a decimal can hold reads
/// as absent, so the whole part is used and the file still loads. Written as before.
/// </summary>
public sealed class SavedExactJson : System.Text.Json.Serialization.JsonConverter<decimal?>
{
    public override bool HandleNull => true;

    public override decimal? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Number && reader.TryGetDecimal(out decimal m)) return m;
        reader.Skip();
        return null;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, decimal? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value is decimal m) writer.WriteNumberValue(m); else writer.WriteNullValue();
    }
}
