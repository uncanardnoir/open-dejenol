namespace Mordor.Core;

/// <summary>
/// The Use command, <c>sub15CD</c> (MAINMOD2.BAS:6076-6215), and the Drop command that sits beside
/// it on the same four-button row (FRMCHAR3.BAS:199-242).
///
/// Use is not one thing. An item can cast a spell, it can permanently change a stat, it can hand
/// over spell points, it can take years off your age, and it can do a spell *and* one of the others
/// in that order. What decides is the item's <c>SpecialType</c> (<c>M56B9</c>) and whether it
/// carries a spell (<c>SpellIndex</c>, <c>M56C4</c>). Only six special types exist in MDATA3:
///
/// <list type="table">
/// <item><term>0</term><description>334 items - nothing beyond the spell, if it has one</description></item>
/// <item><term>1</term><description>15 potions - apply the item's stat modifiers, permanently</description></item>
/// <item><term>11</term><description>Scroll of Spells - 50 spell points</description></item>
/// <item><term>12</term><description>Tome of Spells - 200 spell points</description></item>
/// <item><term>13</term><description>Potion of Youth - one year younger</description></item>
/// <item><term>14</term><description>Dragon's Blood - ten years younger</description></item>
/// <item><term>500-511</term><description>the twelve guild crests</description></item>
/// </list>
///
/// The original's description window claims types 1 to 10 all "Modify Stat(s)" (MAINMOD2.BAS:5091),
/// but <c>sub15CD</c> only implements <c>Case 1</c>. Nothing in the shipped data uses 2 to 10, so
/// the port does not need to choose between the two - but it is worth knowing the switch is wider
/// than the code behind it.
/// </summary>
public static class ItemUse
{
    /// <summary>Where the guild-crest range starts - <c>500</c> in both fn11CA and sub15CD.</summary>
    public const int GuildIssuedBase = 500;

    /// <summary>mc0866 (MAINMOD2.BAS:275): the days a Kind 25 stat item ages its user.</summary>
    public const int ItemKind25AgeDays = 28;

    /// <summary>
    /// A guild crest: its <c>SpecialType</c> is 500 plus the guild's own index. <c>fn11CA</c>
    /// (MAINMOD2.BAS:652-656) exists only to answer this, and the Drop command is its only caller.
    /// </summary>
    public static bool IsGuildIssued(Item it) =>
        it.SpecialType >= GuildIssuedBase && it.SpecialType < GuildIssuedBase + GuildData.All.Count;

    /// <summary>
    /// Whether the item has to be worn before it will do anything - MAINMOD2.BAS:6113-6119. The list
    /// is written out as item types rather than classes: 1 to 22 is everything from a Dagger to an
    /// Amulet, then Stone, Artifact, Miscellaneous Item and Guild Crest. A wand or a potion works
    /// from the pack; a Holy Sword will only heal you while you are holding it.
    ///
    /// Type 0 is Hands, and it is not in the list. That may be deliberate - item 0 of that type is
    /// the character's own bare hands rather than something they picked up - or it may be an
    /// off-by-one. Either way it never shows, because none of the seven Hands carries a spell.
    /// </summary>
    public static bool MustBeEquippedToUse(Item it) =>
        (it.Type >= 1 && it.Type <= 22) || it.Type == 29 || it.Type == 32 || it.Type == 33 || it.Type == 34;

    /// <summary>
    /// Whether the last charge takes the item with it - <c>sub1149</c> (MAINMOD2.BAS:5684-5711).
    /// Potion, Scroll, Tome, Dust, Crystal, Rod, Sphere and Cube are used up; anything else stays in
    /// the pack at zero charges, which is how a spent guild crest still counts as your crest.
    /// </summary>
    public static bool ConsumedWhenEmpty(Item it) =>
        (it.Type >= 23 && it.Type <= 28) || it.Type == 30 || it.Type == 31;

    /// <summary>
    /// Why this item cannot be used, or null if it can. The order is <c>sub15CD</c>'s own
    /// (MAINMOD2.BAS:6085-6119), and it matters: an item with no charges at all is distinguished
    /// from one that has run out, and both are answered before the guild is consulted.
    ///
    /// The one guard left to the caller is the character being incapacitated
    /// (<c>If gv14C2.M5483 &gt; False Then Exit Sub</c>, MAINMOD2.BAS:6085), which refuses silently.
    /// </summary>
    public static string? Problem(Character c, InventorySlot slot, Item it, Func<short, Item?> lookup)
    {
        // a negative SpecialType means the item is not a usable thing at all, and says nothing
        if (it.SpecialType < 0) return "";

        if (it.Charges <= 0)
            return $"\"{c.Name}\", the Item doesn't appear to do much of anything.";
        if (slot.Charges <= 0)
            return $"\"{c.Name}\", the Item is out of charges!";

        // fn11B6 with its fourth argument set, so the hands and worn-count limits are skipped -
        // you may drink a potion with both hands full
        if (c.EquipProblem(it) is string why) return why;

        if (it.SpellIndex > 0)
        {
            // a spell out of an aligned item only works for its own: not half, nothing
            if (slot.Alignment != ItemAlignment.Unaligned
                && ItemAlignments.AsCharacter(slot.Alignment) != c.Alignment) return "Nothing Happens.";
            if (MustBeEquippedToUse(it) && !slot.Equipped) return "Item must be Equipped to use!";
        }
        return null;
    }

    /// <summary>
    /// The spell an item casts, or null. <c>M56C4</c> is a one-based index into the spell table, so
    /// the original writes <c>gv152A(item.M56C4 - 1)</c> (MAINMOD2.BAS:5119).
    /// </summary>
    public static Spell? SpellOf(Item it, IReadOnlyList<Spell> spells) =>
        it.SpellIndex > 0 && it.SpellIndex - 1 < spells.Count ? spells[it.SpellIndex - 1] : null;

    /// <summary>
    /// The level an item casts at. <c>M5734</c> of zero or less means "at the casters Spell Level"
    /// (MAINMOD2.BAS:5121-5126).
    /// </summary>
    public static int CastLevel(Item it, Character c) =>
        it.SpellLvl > 0 ? it.SpellLvl : Rules.SpellLevel(c.Guild.Level);

    /// <summary>
    /// What the item does beyond its spell, once the charge is spent. Returns the lines to show.
    ///
    /// Two alignment rules apply here and they are not the ones that apply to wearing it
    /// (MAINMOD2.BAS:6145-6152). An item one step from your alignment does nothing - but the charge
    /// has already gone, because <c>sub1149</c> runs first. An item two steps away is turned around:
    /// its positive modifiers are **subtracted**.
    /// </summary>
    public static List<string> ApplySpecial(Character c, InventorySlot slot, Item it,
                                            HallRecords? hall = null, string? savesDir = null,
                                            bool inParty = false, Func<short, Item?>? gear = null)
    {
        var said = new List<string>();
        bool reversed = false;
        if (slot.Alignment != ItemAlignment.Unaligned)
        {
            switch (ItemAlignments.FitFor(slot.Alignment, c.Alignment))
            {
                case ItemFit.Adjacent: said.Add("Nothing Happens."); return said;
                case ItemFit.Opposed: reversed = true; break;
            }
        }

        if (it.SpecialType <= 0) return said;

        // MAINMOD2.BAS:6157-6162 - everything but a guild crest makes a noise about it
        if (!IsGuildIssued(it)) said.Add(Sound("MAGIC"));

        bool applyStats = false;
        switch (it.SpecialType)
        {
            case 1:
                applyStats = true;
                break;
            case 11:
                if (!reversed) c.CurSpellPoints += 50;    // MAINMOD2.BAS:6164-6168, and nothing said
                break;
            case 12:
                if (!reversed) c.CurSpellPoints += 200;   // 6169-6173, likewise silent
                break;
            case 13:
                if (!reversed) said.Add(Younger(c, 1, "one year"));
                applyStats = true;
                break;
            case 14:
                if (!reversed) said.Add(Younger(c, 10, "ten years"));
                applyStats = true;
                break;
        }

        if (applyStats)
        {
            said.AddRange(ApplyStatMods(c, it, reversed, hall, savesDir, inParty));
            // l0860 = mdrcharCheckItems(pv0850, True) (MAINMOD2.BAS:6227): a stat just lowered can
            // leave worn gear out of reach, and each piece that comes off names itself. It needs
            // the item table to look the pack up; a caller that does not pass it skips this.
            if (gear != null) said.AddRange(GearCheck.Run(c, gear, inParty, announce: true));
            // "If Items(l085A).Kind = 25 Then mdrcharAddAge True, pv0850, mc0866, False"
            // (MAINMOD2.BAS:6228): 28 days, said as "You are now 4 week(s) older." - whether or
            // not the item was reversed
            if (it.Type == 25) said.Add(c.AgeBy(ItemKind25AgeDays, inParty));
        }
        said.RemoveAll(string.IsNullOrEmpty);
        return said;
    }

    /// <summary>
    /// <c>l085E</c> of sub15CD (MAINMOD2.BAS:6156-6200): specials 1, 13 and 14 apply the
    /// item's stat changes, and that branch ends in <c>sub1658</c> (6230), which re-clicks the
    /// spell class box and so lists the window character's spells again. The same test as
    /// <see cref="ApplySpecial"/>'s, which leaves first for an item one step from your alignment
    /// (6145-6149).
    /// </summary>
    public static bool ChangesStats(Character c, InventorySlot slot, Item it) =>
        !(slot.Alignment != ItemAlignment.Unaligned && ItemAlignments.FitFor(slot.Alignment, c.Alignment) == ItemFit.Adjacent)
        && it.SpecialType is 1 or 13 or 14;

    /// <summary>A marker the UI turns into a sound; keeps this file free of any audio dependency.</summary>
    public const string SoundPrefix = "sound:";
    private static string Sound(string name) => SoundPrefix + name;

    /// <summary>The sound a line asks for, or null if it is an ordinary line.</summary>
    public static string? SoundIn(string line) =>
        line.StartsWith(SoundPrefix, StringComparison.Ordinal) ? line[SoundPrefix.Length..] : null;

    /// <summary>
    /// MAINMOD2.BAS:6096-6115. Sixteen is the floor because that is the age every character is
    /// created at (CHARCREA.BAS:222), and the original counts age in days.
    /// </summary>
    private static string Younger(Character c, int years, string howMany)
    {
        c.Age -= years * 365;
        if (c.Age < 16 * 365) { c.Age = 16 * 365; return "You cannot get any younger."; }
        return $"You are now {howMany} younger.";
    }

    /// <summary>
    /// The permanent stat change, MAINMOD2.BAS:6203-6225. Three things in here are worth naming.
    ///
    /// The ceiling is your race's maximum **plus five**, not the maximum - potions are how a
    /// character gets past what they were born able to reach. Hitting that ceiling is silent: the
    /// original only announces the change in the branch where it did not clamp, so a potion that
    /// takes you to the cap reads as if nothing happened.
    ///
    /// And a reversed item only subtracts modifiers that were *positive*. A negative modifier on an
    /// opposed item is added exactly as it would have been, so it still hurts you.
    /// </summary>
    private static List<string> ApplyStatMods(Character c, Item it, bool reversed,
                                              HallRecords? hall, string? savesDir, bool inParty)
    {
        var said = new List<string>();
        var race = c.RaceDef;
        for (int i = 0; i < 6 && i < it.StatsMod.Length; i++)
        {
            int mod = it.StatsMod[i];
            if (mod != 0)
            {
                int ceiling = race.Max[i] + 5;
                if (reversed && mod > 0)
                {
                    c.Stats[i] -= mod;
                    if (c.Stats[i] > ceiling) c.Stats[i] = ceiling;
                    else said.Add(StatLine(c, i, -mod, inParty));
                }
                else
                {
                    c.Stats[i] += mod;
                    if (c.Stats[i] > ceiling) c.Stats[i] = ceiling;
                    else said.Add(StatLine(c, i, mod, inParty));
                    // mdrhalCheckRecord (6219) is after the If, not in its Else: a raise that meets the
                    // ceiling is silent but still looked at for the Hall. The reversed branch has none.
                    if (hall != null && savesDir != null)
                    {
                        string? took = hall.CheckAndLog(StatRecord(i), c, c.Stats[i], savesDir);
                        if (took != null) said.Add(took);
                    }
                }
            }
            if (c.Stats[i] < race.Min[i]) c.Stats[i] = race.Min[i];
        }
        c.RecalcSpellPoints();
        c.RecalcSwings();
        return said;
    }

    private static HallRecords.Kind StatRecord(int i) => i switch
    {
        0 => HallRecords.Kind.Strength,
        1 => HallRecords.Kind.Intelligence,
        2 => HallRecords.Kind.Wisdom,
        3 => HallRecords.Kind.Constitution,
        4 => HallRecords.Kind.Charisma,
        _ => HallRecords.Kind.Dexterity,
    };

    /// <summary><c>sub0EBF</c>, MAINMOD2.BAS:5761-5780. Alone you are "you"; in a party you are named.</summary>
    public static string StatLine(Character c, int stat, int delta, bool inParty = false)
    {
        string name = RaceData.StatNamesLong[stat];
        if (inParty)
        {
            string their = c.Sex == Sex.Male ? "his" : "her";
            return delta > 0
                ? $"\"{c.Name}\" had {their} {name} increased by {delta}."
                : $"\"{c.Name}\" had {their} {name} decreased by {Math.Abs(delta)}.";
        }
        return delta > 0
            ? $"Your {name} has increased by {delta}."
            : $"Your {name} has decreased by {Math.Abs(delta)}.";
    }

    // ------------------------------------------------------------------ Drop

    /// <summary>
    /// Why this item cannot be dropped, or null if it can - FRMCHAR3.BAS:227-246.
    ///
    /// Dropping destroys the item. <c>sub0EDA</c> blanks the slot and that is the end of it: nothing
    /// is left on the floor to pick up again, in town or below ground.
    /// </summary>
    /// <remarks>
    /// The original refuses pack slot zero outright (<c>If m001A &gt; 0</c>, FRMCHAR3.BAS:212), and
    /// now that the port has fixed slots with the character's Hands in slot zero, so does this: you
    /// cannot drop your own hands.
    /// </remarks>
    public static string? DropProblem(InventorySlot slot, Item it, int packSlot)
    {
        if (packSlot <= 0) return "";   // your hands, and the original says nothing about it
        if (IsGuildIssued(it)) return "Cannot drop Guild-Issued Items!";
        return null;
    }

    /// <summary>
    /// The cursed refusal, which the original asks *after* the confirmation dialog rather than
    /// before it - so the game asks whether you are sure, and only then tells you that you cannot
    /// (FRMCHAR3.BAS:234-238). A cursed item you are not wearing drops perfectly well. The line
    /// quotes the row's own caption, lblItem(m001A).Caption (237), which mdrcharShowItem builds as
    /// "N. " and the item's name (MAINMOD2.BAS:5434-5436) - the same text the pack list shows.
    /// </summary>
    public static string? CursedRefusal(InventorySlot slot, Item it, int packIndex) =>
        slot.SlotCursed && slot.Equipped
            ? $"You can't drop item {packIndex + 1}. {Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges)}!  It's cursed!"
            : null;
}
