using Mordor.Core;

/// <summary>
/// The Use command, <c>sub15CD</c> (MAINMOD2.BAS:6076-6215), and the Drop command beside it
/// (FRMCHAR3.BAS:212-242): what each special type does, what refuses, and what an item is worth
/// when its alignment is not yours.
/// </summary>
public static class UseProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        // ---- what the data actually holds ----
        Console.WriteLine("every special type in MDATA3, and what sub15CD does with it:");
        foreach (var g in items.GroupBy(i => (int)i.SpecialType).OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key,5}  n={g.Count(),3}  {Explain(g.Key),-34} {string.Join(", ", g.Take(3).Select(i => i.Name))}");
        Console.WriteLine("  the description window also claims 2 to 10 modify stats (MAINMOD2.BAS:5091), but");
        Console.WriteLine("  sub15CD has no branch for them - and no item uses them, so it never showed.");

        // ---- which items have to be worn before they do anything ----
        Console.WriteLine("\nitems that must be equipped before Use will work (types 1-22, 29, 32, 33, 34):");
        var needWorn = items.Where(i => i.SpellIndex > 0 && ItemUse.MustBeEquippedToUse(i)).ToList();
        var fromPack = items.Where(i => i.SpellIndex > 0 && !ItemUse.MustBeEquippedToUse(i)).ToList();
        Console.WriteLine($"  {needWorn.Count} spell-carrying items must be worn: {string.Join(", ", needWorn.Take(5).Select(i => i.Name))}, ...");
        Console.WriteLine($"  {fromPack.Count} work straight from the pack: {string.Join(", ", fromPack.Take(5).Select(i => i.Name))}, ...");

        Console.WriteLine("\nand which vanish on their last charge (sub1149):");
        foreach (var g in items.Where(i => i.Charges > 0).GroupBy(i => ItemUse.ConsumedWhenEmpty(i)))
            Console.WriteLine($"  {(g.Key ? "used up" : "goes inert"),-11} {g.Count(),3}  " +
                              string.Join(", ", g.Select(i => Mdata1.ItemTypeName(i.Type)).Distinct().OrderBy(x => x)));

        // ---- a worked use of each special type ----
        Console.WriteLine("\nusing one of each, as a Neutral Human Nomad:");
        foreach (int st in new[] { 1, 11, 12, 13, 14 })
        {
            var it = items.First(i => i.SpecialType == st);
            var c = Fresh();
            int spBefore = c.CurSpellPoints, ageBefore = c.AgeYears;
            var before = c.Stats.ToArray();
            var slot = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)it.Charges };
            c.PutInPack(slot);

            string? why = ItemUse.Problem(c, slot, it, Lookup);
            if (why != null) { Console.WriteLine($"  {it.Name,-24} refused: {why}"); continue; }
            Spellbook.SpendCharge(c, slot, it);
            var said = ItemUse.ApplySpecial(c, slot, it);
            Console.WriteLine($"  {it.Name,-24} {string.Join("  ", said.Where(l => ItemUse.SoundIn(l) == null))}");
            if (!before.SequenceEqual(c.Stats))
                Console.WriteLine($"      stats {string.Join("/", before)} -> {string.Join("/", c.Stats)}");
            if (c.CurSpellPoints != spBefore) Console.WriteLine($"      spell points {spBefore} -> {c.CurSpellPoints}");
            if (c.AgeYears != ageBefore) Console.WriteLine($"      age {ageBefore} -> {c.AgeYears}");
            Console.WriteLine($"      still in the pack: {(c.Inventory.Count > 0 ? "yes" : "no, used up")}");
        }

        // ---- the ceiling and the floor ----
        Console.WriteLine("\nthe ceiling is your race's maximum plus five, and reaching it is silent:");
        var potion = items.First(i => i.SpecialType == 1 && i.StatsMod.Any(m => m > 0));
        int stat = Enumerable.Range(0, 6).First(i => potion.StatsMod[i] > 0);
        var race = RaceData.ByName("Human");
        Console.WriteLine($"  {potion.Name} gives {RaceData.StatNames[stat]} +{potion.StatsMod[stat]}; " +
                          $"a Human's {RaceData.StatNames[stat]} maximum is {race.Max[stat]}, so the cap is {race.Max[stat] + 5}");
        for (int start = race.Max[stat] + 3; start <= race.Max[stat] + 5; start++)
        {
            var c = Fresh(); c.Stats[stat] = start;
            var slot = new InventorySlot { ItemId = potion.Id, Known = IdLevel.All, Charges = (short)potion.Charges };
            c.PutInPack(slot);
            var said = ItemUse.ApplySpecial(c, slot, potion);
            string spoke = string.Join(" ", said.Where(l => ItemUse.SoundIn(l) == null));
            Console.WriteLine($"    from {start,3} -> {c.Stats[stat],3}   {(spoke == "" ? "(said nothing)" : spoke)}");
        }

        // ---- alignment ----
        Console.WriteLine("\nthe same potion carrying each alignment, drunk by a Neutral character:");
        foreach (var a in ItemAlignments.All)
        {
            var c = Fresh();
            var before = c.Stats.ToArray();
            var slot = new InventorySlot { ItemId = potion.Id, Known = IdLevel.All, Alignment = a, Charges = (short)potion.Charges };
            c.PutInPack(slot);
            Spellbook.SpendCharge(c, slot, potion);
            var said = ItemUse.ApplySpecial(c, slot, potion);
            Console.WriteLine($"  {ItemAlignments.Name(a),-10} {RaceData.StatNames[stat]} {before[stat]} -> {c.Stats[stat],-3} " +
                              $"{string.Join(" ", said.Where(l => ItemUse.SoundIn(l) == null))}");
        }
        Console.WriteLine("  the one-step case costs the charge and does nothing, because sub1149 runs before the check.");

        // ---- refusals ----
        Console.WriteLine("\nwhat Use refuses, in sub15CD's own order:");
        void Refuse(Item it, Action<Character, InventorySlot> setup, string note)
        {
            var c = Fresh();
            var slot = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)it.Charges };
            c.PutInPack(slot);
            setup(c, slot);
            string? why = ItemUse.Problem(c, slot, it, Lookup);
            Console.WriteLine($"  {it.Name,-24} {(why == null ? "goes ahead" : why == "" ? "(refused in silence)" : why),-52} {note}");
        }
        var nomadCrest = items.First(i => i.SpecialType == ItemUse.GuildIssuedBase);
        var paladinCrest = items.First(i => i.SpecialType == ItemUse.GuildIssuedBase + 2);
        Refuse(nomadCrest, (_, _) => { }, "no charges at all");
        Refuse(paladinCrest, (_, _) => { }, "a crest must be worn");
        Refuse(paladinCrest, (_, s) => s.Equipped = true, "worn, but a Nomad may not");
        Refuse(items.First(i => i.SpecialType == 1), (_, s) => s.Charges = 0, "drunk already");
        var wand = fromPack.FirstOrDefault(i => i.Type == 28);
        if (wand != null)
        {
            Refuse(wand, (_, _) => { }, "a rod works from the pack");
            Refuse(wand, (_, s) => s.Alignment = ItemAlignment.Good, "one step from Neutral");
        }

        // ---- Drop ----
        Console.WriteLine("\nwhat Drop refuses (FRMCHAR3.BAS:212-242). Dropping destroys the item:");
        var sword = items.First(i => i.Type == 3 && i.Cursed == 0);
        var cursed = items.First(i => i.Cursed != 0);
        void Try(Item it, int at, bool equipped, string note)
        {
            var slot = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Equipped = equipped };
            string? why = ItemUse.DropProblem(slot, it, at) ?? ItemUse.CursedRefusal(slot, it, at);
            Console.WriteLine($"  slot {at + 1,2}  {it.Name,-24} {(why == null ? "gone" : why == "" ? "(refused in silence)" : why),-46} {note}");
        }
        Try(sword, 3, false, "an ordinary item");
        Try(nomadCrest, 3, false, "guild-issued");
        Try(cursed, 3, true, "cursed and worn");
        Try(cursed, 3, false, "cursed but not worn - it goes");

        Character Fresh()
        {
            var c = Character.Create("Tester", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, 12, 12, 12, 12 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 5 });
            c.RecalcSpellPoints(refill: true);
            return c;
        }
    }

    private static string Explain(int st) => st switch
    {
        0 => "nothing beyond its spell",
        1 => "applies its stat modifiers",
        11 => "50 spell points",
        12 => "200 spell points",
        13 => "one year younger",
        14 => "ten years younger",
        _ => st >= ItemUse.GuildIssuedBase ? "a guild crest - no sound" : "no branch exists",
    };
}
