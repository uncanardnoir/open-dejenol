namespace Mordor.Core;

/// <summary>
/// 'g' Give, <c>fn1521</c> case 71 (MAINMOD2.BAS:3705-3730): three <c>fn170D</c> questions and then
/// <c>sub14B5</c>.
/// <code>
///   If UBound(Chars) = False Then sub085B "Nobody in party to give to!" : Exit Function
///   l0540 = Val(fn170D("Give to Character", "Character's Name?", "", "GIVE"))         ' -1: Exit
///   l05DE = CLng(fn170D("Give to " + Q + Name + Q, "How much Gold?", "", "GIVEGOLD")) ' -1: Exit
///   l05E0 = Val(fn170D("Give to " + Q + Name + Q, "Which Item #?", "", "GIVEITEM"))   ' -1: Exit
///   If l05E0 > 1 Then l05E0 = l05E0 - 1 Else l05E0 = False
///   sub14B5 Me, l0540, l05E0, l05DE
/// </code>
/// Gold and an item can go in the one command, gold first. frmInteract's text-box answers are
/// checked as they are typed in (FRMINTER.BAS:116-151, 282-285); this class holds those checks and
/// the conversions, the windows put the questions.
/// </summary>
public static class GiveCommand
{
    public const string NameTitle = "Give to Character";
    public const string NameLabel = "Character's Name?";
    public const string GoldLabel = "How much Gold?";
    public const string ItemLabel = "Which Item #?";
    public const string NotEnoughGold = "Not enough gold!";
    public const string NoItem = "No Item to give!";
    public const string ItemEquipped = "Item is Equipped!";
    /// <summary>The title of the three refusal boxes, <c>MsgBox(..., 48, "SORRY!")</c>.</summary>
    public const string Sorry = "SORRY!";
    /// <summary><c>txtQuestion.MaxLength</c> for the two questions (FRMINTER.BAS:468-471).</summary>
    public const int GoldMaxLength = 10, ItemMaxLength = 3;

    /// <summary>The caption of the gold and item questions: <c>"Give to " + Chr$(34) + RTrim(Name) + Chr$(34)</c>.</summary>
    public static string ToTitle(Character to) => "Give to \"" + to.Name.TrimEnd() + "\"";

    /// <summary>
    /// Who the name question offers: everyone in the party but the one giving (FRMINTER.BAS:331-338,
    /// which leaves out <c>gv1A84</c> - the character being acted on, moved by both switches).
    /// </summary>
    public static List<Character> Offered(IReadOnlyList<Character> party, Character me) =>
        party.Where(c => c != me).ToList();

    /// <summary>
    /// The gold question's OK (FRMINTER.BAS:121-132; an empty box is 284-285). Returns the answer
    /// handed back, or null with the refusal for the "SORRY!" box, after which the box is cleared
    /// and stays open.
    /// </summary>
    public static (string? Tag, string? Refusal) GoldAnswer(string text, VbCurrency goldOnHand)
    {
        if (text == "") return ("0", null);
        double v = Val(text);
        if (v < 1) return ("0", null);
        if (v > goldOnHand.ToDouble()) return (null, NotEnoughGold);   // Val is a Double: the Currency is compared as one
        return (text, null);                    // the text as typed, not its Val
    }

    /// <summary>
    /// The item question's OK (FRMINTER.BAS:133-151, 284-285): an empty box or "0" is no item;
    /// otherwise <see cref="Fn131A"/> must name a slot past the Hands that is not worn.
    /// </summary>
    public static (string? Tag, string? Refusal) ItemAnswer(string text, Character me, Func<short, Item?> lookup)
    {
        if (text == "" || text == "0") return ("0", null);
        int slot = Fn131A(text, me, lookup);
        if (slot < 1) return (null, NoItem);    // item #1, the Hands, is slot 0 and so refused too
        if (me.Pack[slot] is InventorySlot s && s.Equipped) return (null, ItemEquipped);
        return (text, null);
    }

    /// <summary>
    /// <c>fn131A</c> (MAINMOD2.BAS:2523-2551): "" is 0; <c>Val</c> into an Integer (rounding half to
    /// even; an overflow is its error trap's -1); 1 to 40 whose slot holds something, or is slot 1,
    /// is that slot less one; anything else -1.
    /// </summary>
    public static int Fn131A(string text, Character me, Func<short, Item?> lookup)
    {
        if (text == "") return 0;
        double v = Val(text);
        if (v > short.MaxValue || v < short.MinValue) return -1;
        int n = (int)Math.Round(v, MidpointRounding.ToEven);
        if (n < 1 || n > Character.PackSize) return -1;
        var sl = me.Pack[n - 1];
        if ((sl != null && sl.ItemId != 0) || n == 1)
            if (lookup(sl?.ItemId ?? Character.HandsItemId) is Item named && named.Name != "") return n - 1;
        return -1;
    }

    /// <summary>
    /// <c>l05DE = CLng(answer)</c> (MAINMOD2.BAS:3716). The answer is "-1" (Cancel), "0", or the text
    /// as typed, whose <c>Val</c> passed; <c>CLng</c> reads the whole string, so typed text that is
    /// not a number throughout ("12 gold") is a Type mismatch. That lands in fn1521's error trap
    /// (4169-4171), whose <c>sub04FC</c> writes only to the error file, and the command ends with
    /// nothing given and no item asked for: null here.
    ///
    /// The gold question tests <c>Val</c> of the text and the transfer uses <c>CLng</c> of it, and the
    /// two do not read text the same way. <c>Val</c> reads up to the first thing it cannot use and
    /// knows only "." for a point; <c>CLng</c> reads the whole string with the Control Panel's number
    /// settings, thousands separators included. So "1,000" is 1 to the test and 1,000 to the
    /// transfer: with 500 gold on hand it passes "Not enough gold!" and hands over a thousand,
    /// leaving the giver at -500. That is the original's own hole, kept (docs/FIDELITY.md).
    /// </summary>
    public static long? GoldFromTag(string tag) => CLng(tag);

    /// <summary>
    /// VB's <c>CLng</c> of a string: blanks round it allowed, <c>&amp;H</c>/<c>&amp;O</c> as <see cref="Val"/>
    /// reads them, otherwise a sign, digits with the locale's thousands separator, the locale's
    /// decimal point and an E or D exponent - the whole string or a Type mismatch (null) - rounded
    /// half to even into a Long, past whose range is an Overflow (null).
    /// </summary>
    public static long? CLng(string s)
    {
        string t = s.Trim(' ', '\t');
        double v;
        if (t.StartsWith("&H", StringComparison.OrdinalIgnoreCase) || t.StartsWith("&O", StringComparison.OrdinalIgnoreCase))
        {
            int end = t.StartsWith("&H", StringComparison.OrdinalIgnoreCase) ? RadixEnd(t, 2, 16) : RadixEnd(t, 2, 8);
            if (end != t.Length || end == 2) return null;
            v = Radix(t, 2, end);
        }
        else
        {
            // VB takes D for a Double's exponent as well as E; .NET only E
            string e = t.Replace('D', 'E').Replace('d', 'E');
            if (!double.TryParse(e, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                                 System.Globalization.CultureInfo.CurrentCulture, out v) || double.IsNaN(v) || double.IsInfinity(v))
                return null;                   // not a number, or "Infinity"/"NaN" (not numbers to VB), or past a Double
        }
        if (double.IsInfinity(v)) return null;
        double r = Math.Round(v, MidpointRounding.ToEven);
        if (r > int.MaxValue || r < int.MinValue) return null;      // Overflow, the same trap
        return (long)r;
    }

    private static int RadixEnd(string t, int from, int radix)
    {
        int i = from;
        while (i < t.Length && DigitOf(t[i]) is int d && d < radix) i++;
        return i;
    }

    private static int? DigitOf(char c) =>
        c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : null;

    /// <summary>
    /// A hex or octal literal's value as VB gives it: up to &amp;HFFFF it is an Integer, so
    /// &amp;HFFFF is -1 and &amp;H8000 is -32768; past that a Long, the same way; past &amp;HFFFFFFFF
    /// an Overflow (infinity here).
    /// </summary>
    private static double Radix(string t, int from, int end)
    {
        int radix = char.ToUpperInvariant(t[from - 1]) == 'H' ? 16 : 8;
        ulong u = 0;
        for (int i = from; i < end; i++)
        {
            u = u * (ulong)radix + (ulong)DigitOf(t[i])!.Value;
            if (u > 0xFFFFFFFF) return double.PositiveInfinity;
        }
        return u <= 0xFFFF ? (short)(ushort)u : (int)(uint)u;
    }

    /// <summary>
    /// <c>l05E0 = Val(answer)</c> into an Integer, then <c>If l05E0 > 1 Then l05E0 - 1 Else 0</c>
    /// (MAINMOD2.BAS:3720-3728): the pack slot to give, 0 for none.
    /// </summary>
    public static int SlotFromTag(string tag)
    {
        double v = Val(tag);
        if (v > short.MaxValue || v < short.MinValue) return 0;
        int n = (int)Math.Round(v, MidpointRounding.ToEven);
        return n > 1 ? n - 1 : 0;
    }

    /// <summary>What <c>sub14B5</c> said, and how: a Messages line or a "SORRY!" box.</summary>
    public readonly record struct Said(string Text, bool Box);

    /// <summary>
    /// <c>sub14B5 Me, l0540, l05E0, l05DE</c> (MAINMOD2.BAS:2924-3007), the pack branch. The gold
    /// goes first - the answer was checked against the gold on hand, so it is not checked again -
    /// and says "Give of N gold to "X" Completed!" (2930-2935). Then an item in that slot: guild
    /// issue and worn are refused on Messages, a full pack in a box, each ending the routine
    /// (2936-2955); one that goes across is read again by the receiver (2957-2964), and the
    /// routine ends "Give of Item to "X" Completed!" (3004-3006). An empty slot gives nothing.
    /// </summary>
    public static List<Said> Give(Character from, Character to, int slot, long gold, Func<short, Item?> lookup,
                                  Action<int, IdLevel>? filed = null)
    {
        var said = new List<Said>();
        if (gold > 0)
        {
            // sub14B5 has no handler, so an Overflow goes to fn1521's trap (4169-4171), which writes
            // to the error file only and ends the command where it got to: no line, no item
            if (!VbCurrency.TryAdd(to.GoldOnHand, gold, out var got)) return said;
            to.GoldOnHand = got;                                                       // 2931
            if (!VbCurrency.TrySubtract(from.GoldOnHand, gold, out var left)) return said;
            from.GoldOnHand = left;                                                    // 2932
            said.Add(new Said("Give of " + CharStatFile.FormatNumber(gold) + " gold to \"" + to.Name.TrimEnd() + "\" Completed!", false));
        }
        if (slot > 0 && slot < Character.PackSize && from.Pack[slot] is InventorySlot s && s.ItemId > 0)
        {
            string? no = Party.GiveItem(from, to, s, lookup(s.ItemId), lookup, line => said.Add(new Said(line, false)), filed);
            if (no != null) { said.Add(new Said(no, no == Party.NoFreeSlots(to))); return said; }
            said.Add(new Said(Party.GaveItem(to), false));
        }
        return said;
    }

    /// <summary>
    /// VB's <c>Val</c>. Blanks, tabs and linefeeds are stripped wherever they are; then
    /// <c>&amp;H</c> or <c>&amp;O</c> and their digits (see <see cref="Radix"/>), or a sign, digits, a
    /// "." (never the locale's), more digits and an E or D exponent - read up to the first character
    /// that does not fit, so "7. Potion" is 7, "1e3" is 1000, "1D2" is 100, "&amp;H10" is 16 and
    /// "1,000" is 1. A number past a Double is VB's Overflow, and that error has no handler in
    /// frmInteract's cmdOK_Click; the port answers infinity, which no amount of gold passes.
    /// </summary>
    public static double Val(string s)
    {
        var t = new System.Text.StringBuilder();
        foreach (char ch in s) if (ch != ' ' && ch != '\t' && ch != '\n') t.Append(ch);
        string x = t.ToString();
        if (x.StartsWith("&H", StringComparison.OrdinalIgnoreCase) || x.StartsWith("&O", StringComparison.OrdinalIgnoreCase))
        {
            int end = x.StartsWith("&H", StringComparison.OrdinalIgnoreCase) ? RadixEnd(x, 2, 16) : RadixEnd(x, 2, 8);
            return end == 2 ? 0 : Radix(x, 2, end);
        }
        int i = 0;
        if (i < x.Length && (x[i] == '-' || x[i] == '+')) i++;
        int digits = 0;
        while (i < x.Length && char.IsAsciiDigit(x[i])) { i++; digits++; }
        if (i < x.Length && x[i] == '.') { i++; while (i < x.Length && char.IsAsciiDigit(x[i])) { i++; digits++; } }
        if (digits == 0) return 0;
        int mantissa = i;
        if (i < x.Length && (x[i] is 'e' or 'E' or 'd' or 'D'))
        {
            int j = i + 1;
            if (j < x.Length && (x[j] == '-' || x[j] == '+')) j++;
            int k = j;
            while (k < x.Length && char.IsAsciiDigit(x[k])) k++;
            if (k > j) i = k;                  // an exponent with no digits is not read
        }
        string num = x[..mantissa] + (i > mantissa ? "E" + x[(mantissa + 1)..i] : "");
        return double.Parse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }
}
