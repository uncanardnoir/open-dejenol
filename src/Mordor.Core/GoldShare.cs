namespace Mordor.Core;

/// <summary>
/// What <c>monGetGold</c> (<c>fn0DBF</c>, MONSMOD.BAS:2126-2181) says when a fight is won, on
/// lblCharacterHit for half a second (2158, 2174-2176) - not on the Messages list.
///
/// The routine writes four different lines and only two of them can ever be printed. It folds the
/// recovered gold into the found gold first:
///
/// <code>
/// For l039E = 0 To UBound(Chars)                  ' every member's recovered gold
///     If Parties(l039E).Recovered > 0 Then l03A4 = l03A4 + Parties(l039E).Recovered
/// Next
/// l039C = l039C + l03A4                           ' found + recovered
/// ...
/// If l039C > 0 Then                               ' the outer test
///     If Parties(Selected).Recovered = False Then
///         If l039C > 0 Then  "Your share is N Gold!"
///         Else               "You didn't find any Gold!"                  ' dead
///     Else
///         If l039C Then      "Your share is N and you recovered M Gold!"
///         Else               "You didn't find any gold, but M was recovered!"   ' dead
/// </code>
///
/// Both else-branches need <c>l039C</c> falsy inside a block that already requires it positive.
/// The second is dead twice over: reaching it needs the selected character's recovered gold to be
/// nonzero, and that figure has already been added into <c>l039C</c>.
///
/// Note which figures the two live lines use. The share is the whole pot - found plus everybody's
/// recovered - divided by the party; but whether the sentence mentions recovery at all, and the
/// number it quotes, come from the selected character's own recovered gold. Someone else being
/// robbed and getting it back does not make it your recovery.
/// </summary>
public static class GoldShare
{
    /// <summary>
    /// The line, or null when the routine prints nothing. Note that printing nothing is not the
    /// same as doing nothing: the original makes the label visible, gives it the empty string and
    /// waits half a second before hiding it again, so a fight that yields no gold still pauses.
    /// The port shows nothing for it: a blank caption for half a second is not seen.
    /// </summary>
    public static string? Line(long foundPlusRecovered, long recoveredByViewer, int partySize)
    {
        if (foundPlusRecovered <= 0) return null;
        // the figure shown is not the figure paid: formatNumber(CCur(l039C / n)) keeps the
        // quotient to four places and Format$ rounds a half away from zero, where the share
        // itself is CLng'd half to even - 5 between two shows "3" and pays 2 each
        string shown = CharStatFile.FormatNumber(Shown(foundPlusRecovered, partySize));
        return recoveredByViewer != 0
            ? $"Your share is {shown} and you recovered {CharStatFile.FormatNumber(recoveredByViewer)} Gold!"
            : $"Your share is {shown} Gold!";
    }

    /// <summary><c>CCur(l039C / (UBound(Chars) + 1))</c>, the share as the line quotes it (MONSMOD.BAS:2161, 2167).</summary>
    public static VbCurrency Shown(long pot, int partySize) => VbCurrency.FromDouble(pot / (double)Math.Max(1, partySize));

    /// <summary>
    /// <c>CLng(l039C / (UBound(Chars) + 1))</c>. VB's CLng rounds half to even, and every member
    /// gets the same figure, so the remainder is lost rather than handed to anybody
    /// (MONSMOD.BAS:2146-2148). The dead are paid too - the loop asks nothing about them.
    /// </summary>
    public static long Share(long pot, int partySize) =>
        (long)Math.Round(pot / (double)Math.Max(1, partySize), MidpointRounding.ToEven);

    /// <summary>The two wordings the routine can never reach, kept so they can be asserted absent.</summary>
    public const string DeadNoGold = "You didn't find any Gold!";
    public const string DeadOnlyRecovered = "You didn't find any gold, but {0} Gold was recovered!";
}
