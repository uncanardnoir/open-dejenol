using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// VB's conversion of a string to a number, as <c>CLng</c> does it - which is not <c>Val</c>.
/// <c>Val</c> reads as far as it can and ignores the rest; <c>CLng</c> takes the whole string or
/// raises an error. What it takes:
/// <list type="bullet">
/// <item>blanks either side, a leading sign, the locale's thousands separator, decimal point and
/// currency symbol ("$5", "1,000", "-12.5");</item>
/// <item>an exponent, written E or D ("1E3", "2.5D2");</item>
/// <item>hexadecimal and octal in VB's own notation, <c>&amp;H</c> and <c>&amp;O</c> (or a bare
/// <c>&amp;</c> for octal). Up to 16 bits they are an Integer, so <c>&amp;HFFFF</c> is -1;
/// past that a Long, so <c>&amp;HFFFFFFFF</c> is -1 too.</item>
/// </list>
/// A fraction is rounded half to even, and anything outside a Long is an overflow error. Both
/// failures come back as false, for callers whose On Error simply gives up.
/// </summary>
public static class VbConvert
{
    public static bool TryCLng(string text, out int value)
    {
        value = 0;
        string s = text.Trim(' ', '\t');
        if (s.Length == 0) return false;

        if (s[0] == '&')
        {
            int radix = 8, at = 1;
            if (s.Length > 1 && (s[1] == 'H' || s[1] == 'h')) { radix = 16; at = 2; }
            else if (s.Length > 1 && (s[1] == 'O' || s[1] == 'o')) { at = 2; }
            if (at >= s.Length) return false;
            ulong n = 0;
            for (int i = at; i < s.Length; i++)
            {
                int d = HexDigit(s[i]);
                if (d < 0 || d >= radix) return false;
                n = n * (ulong)radix + (ulong)d;
                if (n > 0xFFFFFFFF) return false;
            }
            value = n <= 0xFFFF ? (short)(ushort)n : (int)(uint)n;
            return true;
        }

        // the D exponent is VB's double-precision spelling of E
        string e = s.Replace('D', 'E').Replace('d', 'E');
        const NumberStyles Styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint
            | NumberStyles.AllowExponent | NumberStyles.AllowCurrencySymbol;
        if (!double.TryParse(e, Styles, CultureInfo.CurrentCulture, out double v)) return false;
        v = Math.Round(v, MidpointRounding.ToEven);
        if (double.IsNaN(v) || v < int.MinValue || v > int.MaxValue) return false;
        value = (int)v;
        return true;
    }

    private static int HexDigit(char c) =>
        c >= '0' && c <= '9' ? c - '0'
        : c >= 'A' && c <= 'F' ? c - 'A' + 10
        : c >= 'a' && c <= 'f' ? c - 'a' + 10
        : -1;
}
