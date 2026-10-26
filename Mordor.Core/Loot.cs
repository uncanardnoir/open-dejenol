namespace Mordor.Core;

/// <summary>Item drops and identification. Item type names follow MDATA3's Type field (0 = hands … 34 = guild crest).</summary>
public static class Loot
{
    /// <summary>
    /// The fallback type names, used only if MDATA1 has not been loaded. The real list is
    /// <see cref="Mdata1.ItemTypes"/>; this one was a good guess but wrong in five places
    /// (the game says Chain Armor, Plate Armor, Belt, Miscellaneous Item and Guild Crest).
    /// </summary>
    public static readonly string[] TypeNames =
    {
        "Hands", "Dagger", "Cross", "Sword", "Staff", "Mace", "Axe", "Hammer",
        "Leather Armor", "Chain Armor", "Plate Armor", "Shield", "Cap", "Helmet", "Gloves", "Gauntlets",
        "Cloak", "Bracers", "Sash", "Belt", "Boots", "Ring", "Amulet", "Potion", "Scroll", "Tome",
        "Dust", "Crystal", "Rod", "Stone", "Sphere", "Cube", "Artifact", "Miscellaneous Item", "Guild Crest",
    };

    public static string TypeName(Item it) =>
        Mdata1.Loaded ? Mdata1.ItemTypeName(it.Type)
        : it.Type >= 0 && it.Type < TypeNames.Length ? TypeNames[it.Type] : "Item";

    /// <summary>
    /// How the original describes an identification level, <c>fn132C</c> (MAINMOD2.BAS:2553).
    /// Its own numbering runs the other way - 0 is completely identified - so this maps by name.
    /// </summary>
    public static string IdLevelName(IdLevel known) => known switch
    {
        IdLevel.All => "Completely Identified",
        IdLevel.Mostly => "Mostly identified",
        IdLevel.Partial => "Partially identified",
        _ => "Barely identified",
    };

    /// <summary>
    /// What an item is called at a given level of knowledge, <c>fn1345</c> (MAINMOD2.BAS:2566):
    /// its own name when you know it, then just the name, then the *type* out of gv1592
    /// ("Sword"), then the vaguer description out of gv15C6 ("Weapon", "Round Object").
    ///
    /// Only the fully identified form carries the alignment tag (" " + gv077E$: "(g)", "(n)", "(e)",
    /// MAINMOD2.BAS:2571-2573, MODULE1.BAS:552-555), the charge count and "(cursed)".
    /// </summary>
    public static string NameFor(Item it, IdLevel known, ItemAlignment alignment = ItemAlignment.Unaligned, int charges = 0)
    {
        switch (known)
        {
            case IdLevel.All:
            {
                string s = it.Name;
                if (alignment > ItemAlignment.Unaligned) s += " " + ItemInfoPage.AlignmentTags[Math.Clamp((int)alignment, 0, 3)];
                if (charges > 0 || it.Charges > 0) s += $" [{charges}]";
                if (it.Cursed != 0) s += " (cursed)";
                return s;
            }
            case IdLevel.Mostly: return it.Name;
            case IdLevel.Partial: return TypeName(it);
            default:
                return Mdata1.Loaded ? Mdata1.ItemDescriptionName(it.Type) : "Unidentified Item";
        }
    }

    /// <summary>What the player sees before the item is identified.</summary>
    public static string UnknownName(Item it) => NameFor(it, IdLevel.Barely);



    /// <summary>
    /// How hard an item is to read, the difficulty term inside <c>fn12F0</c>
    /// (MAINMOD2.BAS:2486): <c>25 + ((Log(DamageMod + 1) + 0.3) * Log(LevelScale + 1) * Floor)</c>.
    /// </summary>
    public static double IdentifyDifficulty(Item it) =>
        25 + (Math.Log(it.DamageMod + 1) + 0.3) * Math.Log(it.LevelScale + 1) * it.Floor;

    /// <summary>
    /// The best a given character can make of an item, <c>fn12F0</c> (MAINMOD2.BAS:2480). Their
    /// identify skill against the item's difficulty:
    /// <code>
    ///   result = Partial                          ' the answer unless something below changes it
    ///   If difficulty &lt;= skill Then
    ///       If item.Floor &lt;= 5 Then result = Mostly
    ///   Else
    ///       If (difficulty - skill) >= 10 Then result = Partial   ' no-op: it already is
    ///       If (difficulty - skill) >= 20 Then result = Barely
    ///   End If
    /// </code>
    /// So Partial is the common answer, a shallow item within your skill reads Mostly, and only a
    /// gap of twenty leaves you unable to say more than what kind of thing it is. Reading never
    /// reaches <see cref="IdLevel.All"/> - that takes the Seer.
    /// </summary>
    public static IdLevel ReadableLevel(Character c, Item it, Func<short, Item?>? gear = null)
    {
        // mdrcharGetIDSkill reads Stats + StatMods plainly (MAINMOD2.BAS:2511-2512). StatMods is
        // the running total equipping keeps, each item worth what its alignment allows
        // (MAINMOD2.BAS:1769-1774, 1852-1857) - StatWithMods - with no floor of 1 under it.
        // fn12F0 puts both the skill and the difficulty into Integers before it compares them,
        // so both round
        int intel = gear != null ? c.StatWithMods((int)Stat.Int, gear) : c.Stats[(int)Stat.Int];
        int wis = gear != null ? c.StatWithMods((int)Stat.Wis, gear) : c.Stats[(int)Stat.Wis];
        int skill = (int)Math.Round(Rules.IdentifySkill(c.TopGuildLevel, intel, wis), MidpointRounding.ToEven);
        int difficulty = (int)Math.Round(IdentifyDifficulty(it), MidpointRounding.ToEven);

        if (difficulty <= skill) return it.Floor <= 5 ? IdLevel.Mostly : IdLevel.Partial;
        return difficulty - skill >= 20 ? IdLevel.Barely : IdLevel.Partial;
    }

    /// <summary>
    /// What happens to an item's identification when it changes hands (FRMEXCHA.BAS:201-205): a
    /// receiver who can read it better than its current level improves it. Something already
    /// completely identified is left alone.
    ///
    /// The test and the assignment do not pass the same thing:
    ///
    /// <code>
    /// If  fn12F0(char, Pack(slot).ItemRef) &lt; Pack(slot).IdLevel Then
    ///     Pack(slot).IdLevel = fn12F0(char, Items(Pack(slot).ItemRef).LevelScale)
    /// </code>
    ///
    /// <c>fn12F0</c>'s second argument indexes <c>gv1426</c>, so it is an item reference. The test
    /// passes one. The assignment passes that item's <c>LevelScale</c> - a difficulty number - so
    /// what gets stored is how well the receiver could read *some other item*: whichever one
    /// happens to carry that reference.
    ///
    /// This was recorded as not reproduced, on the grounds that the two readings disagree and
    /// there was no single behaviour to be faithful to. That is wrong. Every LevelScale in MDATA3
    /// is between 0 and 80 and every one of them matches a real item id, so the stray read is
    /// always in range and always lands on a particular item. It is well defined and merely wrong,
    /// which is the ordinary case for everything else in this file. Reproduced: for a level 12
    /// Nomad it changes the answer for 132 of the 365 items.
    /// </summary>
    public static void OnReceived(Character receiver, InventorySlot slot, Item it, Func<short, Item?>? gear = null)
    {
        if (slot.Known == IdLevel.All) return;
        // the test, which asks about the item you are actually holding
        if (ReadableLevel(receiver, it, gear) <= slot.Known) return;
        // and the assignment, which asks about whatever item LevelScale names
        var stray = gear?.Invoke((short)it.LevelScale);
        slot.Known = ReadableLevel(receiver, stray ?? it, gear);
    }

    /// <summary>
    /// Pick the alignment a found copy of this item carries, from the alignments it allows
    /// (MONSMOD.BAS:3807-3811): nought unless Align is above 1, and otherwise
    /// <c>Int(Rnd * 4)</c> drawn again until it names a set bit - an even choice among the set bits
    /// 0 to 3, which is what picking one of <see cref="ItemAlignments.Possible"/> at random is.
    /// Every Align in MDATA3 above 1 has a bit in 1 to 3 set (the aligns probe), so the loop there
    /// always ends.
    /// </summary>
    public static ItemAlignment RollAlignment(Item it, Random rng)
    {
        var options = ItemAlignments.Possible(it);
        return options[rng.Next(options.Count)];
    }

    /// <summary>
    /// Whether a chest's item puts itself on as it lands: "If Items(l04FA).Cursed = gc1DD0 Then
    /// sub0F74 ..." (MONSMOD.BAS:3850-3851), gc1DD0 being 2 (MAIN.TXT:694). The data has two kinds of
    /// cursed item, 1 (38 of them, the Bone Dagger's kind) and 2 (five, Ball and Chain's); only
    /// the second attaches itself. The slot's own curse flag is copied from either (3830).
    /// </summary>
    public static bool AttachesItself(Item it) => it.Cursed == 2;

    /// <summary>A found item, with its alignment and charges already decided.</summary>
    public static InventorySlot NewSlot(Item it, Random rng, bool identified = false) => new()
    {
        ItemId = it.Id,
        Identified = identified,
        Charges = it.Charges > 0 ? (short)it.Charges : (short)0,
        Alignment = RollAlignment(it, rng),
    };

    /// <summary>
    /// Whether the encounter's chest yields an item at all, from MONSMOD.BAS. The chance is the
    /// monster's own <see cref="Monster.ItemChance"/>, and the roll is nudged by the treasure class:
    /// <code>
    ///   l0502 = Int(Rnd * 100) + 1
    ///   Select Case chest class: 1 -> +15, 2 -> nothing, 3 -> -25
    ///   If encounter kind is -1 Then l0502 = l0502 + (Log(dungeonLevel + 10) * 5)
    ///   If l0502 > 90 And monster.ItemChance &lt;&gt; 100 Then l0502 = 90
    ///   If l0502 &lt;= monster.ItemChance Then ... an item is generated
    /// </code>
    /// A better chest makes an item likelier; the cap at 90 means all but a certain-drop monster
    /// keeps some chance however badly the roll goes.
    /// </summary>
    /// <summary>
    /// Whether the chest yields a first item (MONSMOD.BAS:3656-3670). The roll is 1..100, made
    /// worse by 15 for the poorest chest and better by 25 for the richest, and then - a detail the
    /// community page has backwards - made *worse* again by <c>Log(level + 10) * 5</c> if the chest
    /// was magically locked and a Charm of Opening got it open. Forcing a lock costs you.
    ///
    /// The roll is capped at 90 unless the monster's chance is exactly 100, so a monster carrying
    /// 90 or better always hands over something.
    /// </summary>
    public static bool DropsItem(Monster lead, int chestClass, Random rng, int lockState = 0, int dungeonLevel = 1)
    {
        double roll = rng.Next(100) + 1 + chestClass switch { 1 => 15, 3 => -25, _ => 0 };
        if (lockState == -1) roll += Math.Log(dungeonLevel + 10) * 5;
        if (roll > 90 && lead.ItemChance != 100) roll = 90;
        return roll <= lead.ItemChance;
    }

    /// <summary>
    /// Whether it yields *another* one. MONSMOD.BAS wraps the whole generation in a Do loop and
    /// comes back for more at <c>Int(ItemChance / 10)</c> each time, so a monster that reliably
    /// carries something reliably carries several. The port had been taking one and stopping.
    /// </summary>
    public static bool DropsAnother(Monster lead, Random rng) =>
        rng.Next(100) + 1 <= (int)(lead.ItemChance / 10);


    /// <summary>
    /// Which item, <c>monOpenChest</c> (MONSMOD.BAS:3671-3774). The band comes first - half the
    /// monster's drop level up to the whole of it, shifted by the chest class - and then one of the
    /// monster's ten treasure entries is drawn.
    ///
    /// An entry of -1 means any item type at all. An entry below -1 names one exact item, stored as
    /// <c>-(id + 2)</c>. Anything else is an item *type*, and this is where the port had it wrong:
    /// the original does not filter the item table, it walks a **contiguous run** of it. It finds
    /// the first record of that type at or above the floor, then runs forward until the type
    /// changes or the floor passes the top, and picks from that stretch weighted by rarity. Items
    /// sitting past a break in the run are unreachable, which is why the community's notes single
    /// out a few that never drop.
    /// </summary>
    public static Item? RollDrop(IReadOnlyList<Item> all, Monster lead, int chestClass, Random rng)
    {
        for (int restart = 0; restart < 60; restart++)
        {
            // MONSMOD.BAS:3677 lands this float division in an Integer, so it rounds half to even:
            // a drop level of 7 gives 4, not the 3 that truncation would (Item_Generation_Formula's
            // published 1,1,2,2,2,3,4,4,4,5,6,6,6,7,8,8 for levels 1 to 16).
            int top = lead.ItemDropLevel;
            int low = (int)Math.Round(lead.ItemDropLevel / 2.0, MidpointRounding.ToEven);
            if (rng.Next(100) < lead.BoxChance[3]) low = top - 1;
            if (chestClass == 1) { top -= 1; low -= 1; }
            else if (chestClass == 3) low += 1;
            if (top > lead.ItemDropLevel) top = lead.ItemDropLevel;
            if (low < 1) low = 1;
            if (top < 1) top = 1;
            if (low > top) low = top;
            // last, and unclamped, exactly where MODULE4.BAS:3697 puts it
            if (Asmodeus.Is(lead)) (low, top) = Asmodeus.DropBand(lead);

            bool restartBand = false;
            for (int pick = 0; pick < 200 && !restartBand; pick++)
            {
                int entry = lead.Items.Length > 0 ? lead.Items[rng.Next(Math.Min(10, lead.Items.Length))] : -1;
                if (entry == -1) entry = rng.Next(Math.Max(1, Mdata1.ItemTypes.Count));

                if (entry < -1)
                {
                    // a named item rather than a type
                    short want = (short)(Math.Abs(entry) - 2);
                    var exact = all.FirstOrDefault(i => i.Id == want);
                    if (exact != null) return exact;
                    continue;
                }

                // the first record of this type at or above the floor; record 0 is the null item,
                // and the original treats index 0 as "not found"
                int first = 0;
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Type == entry && all[i].Floor >= low) { first = i; break; }

                // sitting exactly on the top of the band: widen downwards and draw again
                if (first > 0 && all[first].Floor == top && low > 1) { low -= 1; continue; }
                // nothing of this type in reach at all: the whole generation starts over
                if (first == 0 || all[first].Floor > top) { restartBand = true; break; }

                int last = first;
                for (int i = first; i < all.Count; i++)
                    if (all[i].Floor > top || all[i].Type != entry) { last = i - 1; break; }
                if (last < first) last = first;

                double total = 0;
                for (int i = first; i <= last; i++) total += all[i].Rarity;
                if (total <= 0) continue;

                double draw = rng.NextDouble() * 100, acc = 0;
                for (int i = first; i <= last; i++)
                {
                    acc += all[i].Rarity / total * 100;
                    if (draw <= acc) return all[i];
                }
            }
        }
        return null;
    }


    /// <summary>
    /// The charges a found item carries. The original sets them to the item's own count and then,
    /// for anything with more than one, shifts them up or down by up to half at random.
    /// </summary>
    public static short RollCharges(Item it, Random rng)
    {
        if (it.Charges <= 0) return 0;
        int n = it.Charges;
        if (it.Charges > 1)
            n += (rng.Next(2) == 0 ? 1 : -1) * (int)(rng.NextDouble() * it.Charges / 2);
        return (short)Math.Max(0, n);
    }
}
