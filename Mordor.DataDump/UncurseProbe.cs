using Mordor.Core;

/// <summary>
/// The Seer's uncurse asks two questions in a fixed order (FRMCTYS.BAS:292-302), and which of
/// them a cursed item answers depends on how the item got into the pack. This walks the three
/// routines that write the slot's curse flag and counts what the data can actually deliver.
/// </summary>
public static class UncurseProbe
{
    public static void Run(string dir)
    {
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var real = items.Where(i => i.Id != 0 && i.Name.Trim().Length > 0).ToList();

        Console.WriteLine("writers of the *slot* curse flag M572A, and whether they also equip:\n");
        foreach (var (where, what, equips) in new[]
                 {
                     ("MODULE6.BAS:1786", "equip: copies the item's flag on", true),
                     ("MODULE6.BAS:1821", "equip: an opposed-alignment item is cursed onto you", true),
                     ("MODULE6.BAS:1745", "unequip: clears it", true),
                     ("MODULE4.BAS:3825", "sub0F01, chest treasure into a pack slot", false),
                     ("FRMCTYG.BAS:745",  "modAwardCrest, the Guildmaster's Crest", false),
                     ("FRMEDITC.BAS:224", "the character editor", false),
                     ("FRMCTYS.BAS:311",  "the uncurse itself, clearing it", false),
                 })
            Console.WriteLine($"  {where,-18} {what,-55} {(equips ? "sets M577E too" : "leaves M577E alone")}");

        Console.WriteLine("\n(MODULE4 and FRMCTYG contain no assignment to M577E at all - only tests -");
        Console.WriteLine(" so an item arriving by either route is flagged cursed and not equipped.)");

        var cursed = real.Where(i => i.Cursed != 0).ToList();
        Console.WriteLine($"\ncursed items in MDATA3: {cursed.Count} of {real.Count}");
        Console.WriteLine($"  reachable from a chest (floor 1 and below): {cursed.Count(i => i.Floor >= 1)}");
        Console.WriteLine($"  by floor: " + string.Join(", ",
            cursed.Where(i => i.Floor >= 1).GroupBy(i => i.Floor).OrderBy(g => g.Key)
                  .Select(g => $"{g.Key}:{g.Count()}")));

        Console.WriteLine("\n  the shallowest few, which is where a new character meets one:");
        foreach (var it in cursed.Where(i => i.Floor >= 1).OrderBy(i => i.Floor).ThenBy(i => i.Name.Trim()).Take(8))
            Console.WriteLine($"    floor {it.Floor,2}  cursed {it.Cursed}  {it.Name.Trim()}");

        var crests = real.Where(i => i.Name.Contains("Crest")).ToList();
        Console.WriteLine($"\n  Guildmaster Crests: {crests.Count}, of which cursed: {crests.Count(i => i.Cursed != 0)}");

        Console.WriteLine("\nwhat values does the Cursed field take?");
        foreach (var g in real.GroupBy(i => i.Cursed).OrderBy(g => g.Key))
            Console.WriteLine($"  Cursed = {g.Key}: {g.Count(),3} items   e.g. {string.Join(", ", g.Take(3).Select(i => i.Name.Trim()))}");
        Console.WriteLine("  (MODULE6.BAS:1810 tests = 2 for its own refusal wording, so 2 is a distinct kind.");
        Console.WriteLine("   MODULE4.BAS:3845 tests = gc1DD0, a Global Const the decompiler dropped - 195 such");
        Console.WriteLine("   names survive with no declaration - so the auto-attach threshold cannot be read");
        Console.WriteLine("   off the source, only bounded by what values the data actually holds.)");

        Console.WriteLine("\nso at the General Store's uncurse panel, with the original's slot flag:");
        Console.WriteLine("  bought and never worn   -> flag clear -> \"Item is not cursed!\"   (a lie, but the game's)");
        Console.WriteLine("  worn                    -> flag set, equipped   -> uncursed properly");
        Console.WriteLine("  found in a chest        -> flag set, NOT equipped -> \"Item isn't equipped!\"");

        // which of the 43 would actually be refused by sub0F74's guards for a fresh character?
        // That is the population the third line above can happen to.
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);
        var fresh = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 10, 10, 10, 10, 10, 10 });
        var refused = new List<(string Name, string Why)>();
        foreach (var it in cursed.Where(i => i.Floor >= 1))
        {
            var sl = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)Math.Max(1, it.Charges) };
            if (!Character.IsWearable(it)) { refused.Add((it.Name.Trim(), "not something you wear")); continue; }
            if (fresh.WearRefusal(sl, it, Look) is string why) refused.Add((it.Name.Trim(), why));
        }
        Console.WriteLine($"\nof those {cursed.Count(i => i.Floor >= 1)} findable cursed items, a fresh level 1 character");
        Console.WriteLine($"is refused {refused.Count} by the equip guards - each of those, found in a chest,");
        Console.WriteLine("sits flagged cursed and unworn:");
        foreach (var (name, why) in refused.Take(10)) Console.WriteLine($"    {name,-28} {why}");
        if (refused.Count > 10) Console.WriteLine($"    ... and {refused.Count - 10} more");
    }
}
