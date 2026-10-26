using System.Collections.Generic;

namespace Mordor.Core;

/// <summary>
/// The seven state labels down the right of the character window's Stats page.
///
/// <c>FRMCHAR1.BAS:176-182</c> loads one label per entry and captions it from <c>gv07E2$</c>;
/// <c>mdrcharStats</c> page 12 (MAINMOD2.BAS:5633-5641) shows each one whose matching
/// <c>Ailments(i)</c> is set and hides the rest. Each has its own place, one under the other from
/// the first stat's row, whether the ones above it show or not - bm13.png has Invisible, See
/// Invisible and Protected, the fifth to the seventh, beside Charisma, Dexterity and the row below
/// (<see cref="CharStatForm.SpecialTop"/>).
///
/// <code>
/// gv07E2$(0) = "Poisoned"        gv07E2$(4) = "Invisible"
/// gv07E2$(1) = "Diseased"        gv07E2$(5) = "See Invisible"
/// gv07E2$(2) = "Paralyzed"       gv07E2$(6) = "Protected"
/// gv07E2$(3) = "Levitating"
/// </code>
///
/// Seven, in that order, and nothing else. <see cref="Character.AilmentText"/> is a different
/// thing and is left alone: it shouts POISONED and DISEASED, carries a STONE that is not one of
/// these seven, and is what the dungeon's status line and other callers expect.
/// </summary>
public static class CharacterStates
{
    /// <summary>The captions, in <c>gv07E2$</c> order.</summary>
    public static readonly string[] Names =
    {
        "Poisoned", "Diseased", "Paralyzed", "Levitating", "Invisible", "See Invisible", "Protected",
    };

    /// <summary>
    /// The ones showing, in that order. "Paralyzed" is <c>Ailments(2) &lt;&gt; 0</c>, read off the
    /// character like the rest (MAINMOD2.BAS:5635).
    /// </summary>
    public static List<string> Showing(Character c)
    {
        var on = new List<string>();
        if (c.Poisoned) on.Add(Names[0]);
        if (c.Diseased) on.Add(Names[1]);
        if (c.Paralysed) on.Add(Names[2]);
        if (c.Levitating) on.Add(Names[3]);
        if (c.InvisibleNow) on.Add(Names[4]);
        if (c.SeeingInvisible) on.Add(Names[5]);
        // the label is just "Protected" - the strength of it is not painted here
        if (c.Protection > 0) on.Add(Names[6]);
        return on;
    }
}
