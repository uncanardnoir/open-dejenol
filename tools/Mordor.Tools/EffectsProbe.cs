using Mordor.Core;

/// <summary>
/// Checks the effect and resistance model against the real data: what each race resists, what a
/// worn item is worth, and what each of the ten chest traps actually does to a character.
/// </summary>
public static class EffectsProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var it) ? it : null;

        Console.WriteLine("race base resistances (11 kinds):");
        foreach (var r in RaceData.All)
            Console.WriteLine($"  {r.Name,-12} {string.Join(" ", r.Resistances.Select(v => $"{v,3}"))}");

        int resistant = items.Count(i => i.ResistanceFlags != 0);
        int effects = items.Count(i => i.Abilities != 0);
        Console.WriteLine($"\nitems: {items.Count}, carrying a resistance {resistant}, carrying an effect bit {effects}");
        Console.WriteLine($"  a matching equipped item sets the resistance to a flat {Effects.GearResistance}");

        // what the character window's Resist. tab now shows, for a race with nothing of its own
        // and one with plenty, before and after putting on a resistant item
        string[] kinds = { "Fire", "Cold", "Electric", "Mind", "Disease", "Poison", "Magic", "Stone", "Paralysis", "Draining", "?" };
        var ring = items.FirstOrDefault(i => i.ResistanceFlags != 0 && i.AlignmentFlags <= 1);
        foreach (string raceName in new[] { "Human", "Dwarf" })
        {
            var race = RaceData.All.FirstOrDefault(r => r.Name == raceName) ?? RaceData.All[0];
            var c = Character.Create("Probe", race, Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
            c.ClearPack();
            Console.WriteLine($"\n{raceName}, nothing worn:");
            Console.WriteLine("  " + string.Join("  ", Enumerable.Range(0, Effects.ResistanceKinds)
                .Select(b => $"{kinds[b]} {Effects.ResistancePercent(c, b, Lookup)}")));
            if (ring == null) continue;
            c.PutInPack(new InventorySlot { ItemId = ring.Id, Equipped = true });
            Console.WriteLine($"  wearing {ring.Name} (resists 0x{ring.ResistanceFlags:X}):");
            Console.WriteLine("  " + string.Join("  ", Enumerable.Range(0, Effects.ResistanceKinds)
                .Select(b => $"{kinds[b]} {Effects.ResistancePercent(c, b, Lookup)}")));
            c.SpellResist[0] = SpellEffect.ResistGranted;
            Console.WriteLine($"  and with a resistance spell up on Fire:");
            Console.WriteLine("  " + string.Join("  ", Enumerable.Range(0, Effects.ResistanceKinds)
                .Select(b => $"{kinds[b]} {Effects.ResistancePercent(c, b, Lookup)}")));
        }

        // monster alignment drives the per-second decision to attack you (MULTIMOD.BAS:559-587)
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        Console.WriteLine("\nmonster alignment values in MDATA5: " +
            string.Join("  ", mons.GroupBy(m => m.AlignmentValue).OrderBy(g => g.Key)
                                  .Select(g => $"{g.Key}={g.Count()}")));
        Console.WriteLine("chance they start on you in one second (CHA 12):");
        Console.WriteLine("  monster \\ you   Good  Neutral  Evil");
        foreach (Alignment ma in new[] { Alignment.Good, Alignment.Neutral, Alignment.Evil })
            Console.WriteLine($"  {ma,-14}" + string.Join("  ", new[] { Alignment.Good, Alignment.Neutral, Alignment.Evil }
                .Select(ca => $"{Rules.EngageChance(ma, ca, 12),5}%")));
        Console.WriteLine("  (charisma subtracts 7.5*Log(CHA): " +
            string.Join(", ", new[] { 3, 12, 18, 25 }.Select(c => $"CHA {c} -> -{(int)(7.5 * Math.Log(c))}")) + ")");

        Console.WriteLine("\nchest traps, one character, dungeon level 10, 200 runs each:");
        for (int trap = 1; trap <= 10; trap++)
        {
            var rng = new Random(trap * 7717);
            int hurt = 0, poisoned = 0, diseased = 0, lostItem = 0, aged = 0, teleported = 0, lostCompanion = 0;
            for (int n = 0; n < 200; n++)
            {
                var c = Fresh(items);
                int hits = c.CurHits, age = c.Age, pack = c.Inventory.Count, comps = c.Companions.OccupiedCount;
                var res = ChestTraps.Spring(trap, new[] { c }, c, 10, rng, Lookup,
                                            (x, y) => x == 1 && y == 1 ? 1 << 19 : 0);
                if (c.CurHits < hits) hurt++;
                if (c.Poisoned) poisoned++;
                if (c.Diseased) diseased++;
                if (c.Inventory.Count < pack) lostItem++;
                if (c.Age > age) aged++;
                if (res.TeleportTo != null) teleported++;
                if (c.Companions.OccupiedCount < comps) lostCompanion++;
            }
            Console.WriteLine($"  {trap,2} {ChestTraps.Names[trap],-16} hurt {hurt,3}  poison {poisoned,3}  disease {diseased,3}  " +
                              $"item lost {lostItem,3}  aged {aged,3}  teleported {teleported,3}  companions fled {lostCompanion,3}");
        }
    }

    private static Character Fresh(List<Item> items)
    {
        var c = Character.Create("Probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        c.MaxHits = 100; c.CurHits = 100;
        c.MaxSpellPoints = 60; c.CurSpellPoints = 60;
        c.ClearPack();
        foreach (var it in items.Where(i => i.Id != 0 && i.Floor == 1).Take(5))
            c.PutInPack(new InventorySlot { ItemId = it.Id });
        c.Companions.Add(new Companion { MonsterId = 1, Name = "Rat", Hits = 5, MaxHits = 5, BindingLevel = 8 });
        c.Companions.Add(new Companion { MonsterId = 2, Name = "Loyal", Hits = 5, MaxHits = 5, BindingLevel = Binding.ForLife });
        return c;
    }
}
