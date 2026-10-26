namespace Mordor.Core;

/// <summary>
/// <c>s</c> - "Swap Items", one of the help's Game Commands, so it works "in both the City and the
/// Dungeon (any time)". <c>fn1521</c> handles it (MAINMOD2.BAS:3420-3432): one box takes both slot
/// numbers, comma separated, and the guards are its own.
///
/// <code>
/// l0536 = fn170D("Swap Items", "Enter the Item Slots to Swap" + Chr$(13) + "(i.e '12,17')", ...)
/// If l0536 = "" Or InStr(l0536, ",") = False Or Len(l0536) &gt; 5 Then Exit Function
/// ...
/// If l054C &lt;= 1 Or l054C &gt; mc0190 Then Exit Function
/// </code>
///
/// Every refusal is a silent <c>Exit Function</c> - nothing is said, the box simply closes. Slot 1
/// is your hands and cannot be moved. Empty slots swap perfectly well; that is how a pack is
/// tidied. The five-character limit means "40,39" is the longest thing it will take.
/// </summary>
public static class SwapCommand
{
    /// <summary>
    /// The two one-based slot numbers typed into the box, or null for anything the original would
    /// have walked away from. <see cref="Character.SwapSlots"/> applies the range checks, which are
    /// the same two the original makes.
    /// </summary>
    public static (int A, int B)? Parse(string? typed)
    {
        string s = (typed ?? "").Trim();
        if (s == "" || !s.Contains(',') || s.Length > 5) return null;
        var parts = s.Split(',');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0].Trim(), out int a)) return null;
        if (!int.TryParse(parts[1].Trim(), out int b)) return null;
        return (a, b);
    }

    /// <summary>sub085B's line when it works (MAINMOD2.BAS:3434).</summary>
    public const string Done = "Item swap completed!";
}
