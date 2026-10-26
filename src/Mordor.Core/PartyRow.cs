namespace Mordor.Core;

/// <summary>
/// The Status and Option columns of the Party Information window, from <c>modUpdateStats</c>
/// (FRMGROUP.BAS:432-472) and the help's own screenshot of the window, game_files/bm5.png - which
/// is 299x126, the size this window is drawn at.
///
/// The five columns are Name, Hits, Spells, Status and Option. Hits is <c>Hits/MaxHits</c>, Spells
/// is the current spell points alone with no maximum, and the other two are here.
/// </summary>
public static class PartyRow
{
    /// <summary>
    /// Two letters per ailment, run together, or "OK" when there are none:
    ///
    /// <code>
    /// If Ailments(poison)    Then l0142 = l0142 + "Po"
    /// If Ailments(disease)   Then l0142 = l0142 + "Di"
    /// If Ailments(paralysis) Then l0142 = l0142 + "Pa"
    /// If l0142 = "" Then l0142 = "OK"
    /// </code>
    ///
    /// So a character who is both poisoned and diseased reads "PoDi". Nothing else appears in this
    /// column - not death, not stoning - because <c>sub2F40</c> takes the dead off the roster
    /// before this ever paints them.
    /// </summary>
    public static string Status(Character c)
    {
        string s = "";
        if (c.Poisoned) s += "Po";
        if (c.Diseased) s += "Di";
        if (c.Paralysed) s += "Pa";   // Ailments(mc014A) <> False (FRMGROUP.BAS:443)
        return s == "" ? "OK" : s;
    }

    /// <summary>Green while it reads "OK", and the warning colour once it does not.</summary>
    public static bool StatusIsWell(Character c) => Status(c) == "OK";
}
