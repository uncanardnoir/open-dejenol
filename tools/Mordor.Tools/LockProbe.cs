using Mordor.Core;

/// <summary>
/// End to end: can anything actually open a locked chest? Two routes - a spell a character knows,
/// and an item held with charges - and until this week neither worked, because Charm of Opening is
/// spell record 0 and both routes treated a zero as absence.
/// </summary>
public static class LockProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Item? Lookup(short id) => items.FirstOrDefault(i => i.Id == id);

        var open = spells[0];
        Console.WriteLine($"Charm of Opening: effect {open.Effect}, opens locks up to level "
                        + $"{Spellbook.OpensLockUpTo(open)}, counts as a utility spell: {Spellbook.IsUtility(open)}");

        // route one: a Thief who has learned it
        var thief = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
        thief.Guilds.Clear();
        thief.Guilds.Add(new GuildMembership { Guild = "Thief", Level = 10 });
        var known = Spellbook.Castables(thief, spells, Lookup);
        Console.WriteLine($"\na Thief 10 can cast {known.Count} things; Charm of Opening among them: "
                        + known.Any(c => c.Spell.Name.Trim() == "Charm of Opening" && !c.FromItem));

        // route two: each of the seven items, carried, identified, with charges
        Console.WriteLine("\nheld items that offer it:");
        foreach (string name in new[] { "Thief's Cap", "Ring of Opening", "Dust of Opening", "Key of Opening",
                                        "Skeleton Key", "Crest of the Thief", "Crest of the Scavenger" })
        {
            var it = items.FirstOrDefault(i => i.Name.Trim() == name);
            if (it == null) { Console.WriteLine($"  {name,-22} not in MDATA3"); continue; }
            var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 25, 25, 25, 25, 25, 25 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = "Thief", Level = 60 });
            // an item that must be worn to work is worn; the rest simply carried
            c.PutInPack(new InventorySlot
            {
                ItemId = it.Id, Charges = (short)Math.Max(1, it.Charges),
                Known = IdLevel.All, Equipped = ItemUse.MustBeEquippedToUse(it),
            });
            var castable = Spellbook.Castables(c, spells, Lookup)
                                    .FirstOrDefault(x => x.FromItem && x.Item!.Id == it.Id);
            Console.WriteLine($"  {name,-22} equipped {ItemUse.MustBeEquippedToUse(it),-5}  offers: "
                            + (castable == null ? "NOTHING" : castable.Spell.Name.Trim()));
        }
    }
}
