using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// A raise cast on somebody else's body. In the original that never rolls on the spot: sub0A7C
/// (MAINMOD2.BAS:5974-5998) writes the chance, the raiser and the kind of raise into the dead
/// character's file, and the roll is only made when that character is next run and says yes -
/// fn16B5 (MAINMOD2.BAS:314-380), called from sub0930 (MAINMOD2.BAS:3058-3066) when a dead
/// character is loaded, and from the Ask to Join case (MAINMOD2.BAS:3491-3495).
///
/// Its callers with a chance: the morgue's Raise Character (FRMMORG.BAS:86), kind 23, and the three
/// raise spells cast on a body (MAINMOD2.BAS:1090-1091), kind 20, 21 or 22 - the spell's effect.
/// Every other caller passes a chance of 0 and only moves the body to where the carrier stands.
/// </summary>
public static class PendingRaise
{
    /// <summary>The offer's box title: <c>MsgBox(l00B0, 36, "RAISE")</c>.</summary>
    public const string OfferTitle = "RAISE";

    /// <summary>What the morgue counter says when the body already has one (FRMMORG.BAS:78).</summary>
    public const string AlreadyCast = "Raise already cast on Character!";

    /// <summary><c>M58E9 &gt; 0</c> - the test morgueRaiseCost (MAINMOD2.BAS:2730) and sub0930 both make.</summary>
    public static bool IsPending(Character c) => c.RaiseChance > 0;

    /// <summary>
    /// sub0A7C (MAINMOD2.BAS:5974-5998): the body goes to the raiser's square, level and area, and
    /// with a chance above 0 the raise is written on it. The area is the raiser's M59BB, copied into
    /// the record whatever it is (MAINMOD2.BAS:5990-5992).
    /// </summary>
    public static void Initiate(Character body, Character raiser, int chance, int kind)
    {
        body.X = raiser.X;
        body.Y = raiser.Y;
        body.DungeonLevel = raiser.DungeonLevel;
        body.Area = raiser.Area;   // "l0830.M59BB = Chars(Me).M59BB" (MAINMOD2.BAS:5992)
        if (chance > 0)
        {
            body.RaiseChance = chance;
            body.RaisedBy = raiser.Name.TrimEnd();
            body.RaiseKind = kind;
        }
    }

    /// <summary>
    /// fn16B5's opening (MAINMOD2.BAS:332-335): somebody pulled into the rock has the chance
    /// zeroed before the offer is written, so the box says "Chance of success is 0%." and the roll
    /// can only fail - into raiseComplications, which rock always takes.
    /// </summary>
    public static void Prepare(Character c)
    {
        if (c.CauseOfDeath == Rules.DeathRock) c.RaiseChance = 0;
    }

    /// <summary>
    /// The offer (MAINMOD2.BAS:337-341). A character run alone reads "on your character"; one
    /// being loaded into a party is named. <c>Str$</c> leaves a space before the number, which is
    /// the only space between "is" and it.
    /// </summary>
    public static string Offer(Character c, bool alone)
    {
        string by = "\"" + c.RaisedBy.TrimEnd() + "\"";
        string name = "\"" + c.Name.TrimEnd() + "\"";
        string chance = "Chance of success is" + VbStr(c.RaiseChance) + "%.";
        return alone
            ? by + " has initiated a raise on your character.\n" + chance + "\nDo you accept?"
            : by + " has initiated a raise on " + name + ".\n" + chance + "\nDoes " + name + " accept?";
    }

    /// <summary>
    /// Yes to the offer: the rest of fn16B5 (MAINMOD2.BAS:343-378). The roll, the complications,
    /// the stone and morgue Constitution losses, the age and the hits are
    /// <see cref="Raising.Resolve"/>'s, which is the same routine. What is added here is what the
    /// fields do. A quiet failure goes through raiseComplications' False branch, which clears all
    /// three; a raise that takes clears the raiser and the chance and leaves the kind behind.
    /// <paramref name="raiser"/> is who cast it, for the guild's line.
    /// </summary>
    public static RaiseOutcome Accept(Random rng, Character c, Race race, Func<short, Item?> lookup,
                                      bool alone, out string raiser)
    {
        raiser = c.RaisedBy.TrimEnd();
        var o = Raising.Resolve(rng, c, race, c.RaiseKind, c.RaiseChance, lookup, ownBody: false, inAParty: !alone);
        if (!o.Raised)
        {
            c.RaiseKind = 0;
            c.RaiseChance = 0;
            c.RaisedBy = "";
            return o;
        }
        c.RaisedBy = " ";
        c.RaiseChance = 0;
        return o;
    }

    /// <summary>
    /// What happens when fn16B5 comes back False - declined, or failed without complications - in
    /// sub0930 (MAINMOD2.BAS:3062-3065): the raise is gone and the file saved. The Ask to Join case
    /// does not clear it, but mdrcharUnload (MAINMOD.BAS:419-423) saves the character as fn16B5 left
    /// it: an ordinary body keeps its raise for next time, and a rocked one - whose chance fn16B5
    /// zeroed before the box - has it wiped from the file.
    /// </summary>
    public static void Forget(Character c)
    {
        c.RaiseChance = 0;
        c.RaisedBy = " ";
    }

    /// <summary>
    /// Where each of an outcome's lines goes in fn16B5 and raiseComplications: a box and its title,
    /// or null for the Messages window. "Success!" is titled RAISE in fn16B5 (MAINMOD2.BAS:350),
    /// unlike the own-body morgue's "Raise Info".
    /// </summary>
    public static string? BoxTitleFor(string line) => line switch
    {
        "Raise Failed!" => "Raise Info",
        "Success!" => "RAISE",
        "There are complications..." => "Raise Info",
        "One or more items were unequipped due to lack of stats..." => "Notice!",
        _ => null,
    };

    /// <summary>VB's <c>Str$</c> of a whole number: a leading space for anything not negative.</summary>
    public static string VbStr(long n) =>
        n < 0 ? n.ToString(CultureInfo.InvariantCulture) : " " + n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// The morgue counter, frmCityMorgue (FRMMORG.BAS), as a living character sees it - game_files/bm10.png.
/// </summary>
public static class Morgue
{
    public const string Welcome = "Welcome to the Morgue!";
    public const string Rocked = "We are unable to help a rocked character.  You'll need to find somebody who can cast 'resurrect'.  Sorry!";
    public const string NotEnoughGold = "Not enough gold!";
    public const string TakenCareOf = "Character taken care of!";
    public const string RaiseFailed = "Raise Failed!";
    public const string RaiseWorked = "Raise Was Successful!";

    /// <summary>
    /// morgueRaiseCost (MAINMOD2.BAS:2734): <c>l044C = 2912.32# * (MaxHits / 1.5#)</c> into a Long,
    /// so rounded half to even. The own-body offer takes <c>Int</c> of the same sum instead.
    /// </summary>
    public static long RaiseCost(int maxHits) =>
        (long)Math.Round(2912.32 * (maxHits / 1.5), MidpointRounding.ToEven);

    /// <summary>
    /// The counter's chance, morgueRaiseChance called with -1 for the spell (MAINMOD2.BAS:2765-2767):
    /// the tier works out below 1 and is lifted to 1, so it is <c>35 * (CON / 4.5) + 10</c>,
    /// rounded into an Integer and clamped to 10..98.
    /// </summary>
    public static int RaiseChance(int con, int causeOfDeath) => Rules.SpellRaiseChance(con, -1, causeOfDeath);

    /// <summary><c>"Cost to raise " + Name + " is " + formatNumber(cost) + "." + CR + "Do you wish to pay?"</c> (FRMMORG.BAS:81).</summary>
    public static string CostOffer(string name, long cost) =>
        "Cost to raise " + name.TrimEnd() + " is " + FormatNumber(cost) + ".\nDo you wish to pay?";

    /// <summary>
    /// sub2BDB (FRMMORG.BAS:262): <c>m0022 = (MaxHits * 1244.34#) * Monsters(Ref).Floor</c>, a Long.
    /// </summary>
    public static long CompanionCost(int maxHits, int floor) =>
        (long)Math.Round(maxHits * 1244.34 * floor, MidpointRounding.ToEven);

    /// <summary>The Raise Companion button's caption (FRMMORG.BAS:264).</summary>
    public static string CompanionCaption(string companionName, long cost) =>
        "Raise " + companionName + " for " + FormatNumber(cost);

    /// <summary>
    /// The morgue's companion raise (FRMMORG.BAS:93-95): <c>75 + Stats(3)</c>, capped at 98 and no
    /// floor, and it fails on <c>Int(Rnd * 100) &gt; chance</c>.
    /// </summary>
    public static int CompanionChance(int con) => Math.Min(98, 75 + con);

    /// <summary>formatNumber (MODULE1.BAS:194-213): "###,###,###,###,###", and "0" for nought.</summary>
    public static string FormatNumber(long v)
    {
        string t = v.ToString("###,###,###,###,###", CultureInfo.CurrentCulture);
        return t == "" ? "0" : t;
    }
}
