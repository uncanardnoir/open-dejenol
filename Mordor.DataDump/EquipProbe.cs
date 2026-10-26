using Mordor.Core;

/// <summary>
/// The rules that decide whether an item goes on, and what it is worth once it is there -
/// <c>fn11B6</c> (MODULE6.BAS:566-650) and the equip branch of <c>sub0F74</c> (MODULE6.BAS:1774-1836).
/// </summary>
public static class EquipProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        // ---- what the data says about each wearable class ----
        Console.WriteLine("the item classes and how many of each may be worn (gv15C6.M54F1):");
        Console.WriteLine($"  {"#",2}  {"class",-22} {"max",4}  {"items",5}  example types");
        for (int cls = 0; cls < Mdata1.ItemDescriptions.Count; cls++)
        {
            int max = Character.MaxWornOfClass(cls);
            var types = Enumerable.Range(0, Mdata1.ItemTypes.Count)
                                  .Where(t => Mdata1.ItemTypes[t].Index == cls)
                                  .Select(t => Mdata1.ItemTypes[t].Name).ToList();
            int n = items.Count(i => Character.WearClassOf(i) == cls);
            Console.WriteLine($"  {cls,2}  {Mdata1.ItemDescriptions[cls].Name,-22} {(max == 0 ? "none" : max < 0 ? "any" : max.ToString()),4}  " +
                              $"{n,5}  {string.Join(", ", types.Take(5))}{(types.Count > 5 ? ", ..." : "")}");
        }
        Console.WriteLine("  a 'none' is a zero in the file: sub0F74 refuses those without a word. An 'any' is");
        Console.WriteLine("  the -1 Miscellaneous Object carries - wearable, and fn11B6 then declines to cap it.");

        // ---- how many hands things take ----
        Console.WriteLine("\nhands an item occupies (gv1426.M5700):");
        foreach (var g in items.GroupBy(i => (int)i.NHands).OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key} hand(s): {g.Count(),4} items   e.g. {string.Join(", ", g.Take(4).Select(i => i.Name))}");

        // ---- what an item is worth at each alignment distance ----
        Console.WriteLine("\nwhat an item is worth by how far its alignment sits from yours");
        Console.WriteLine("(sub0F74 halves attack and defence with Int(x)/2, which rounds half to even, but");
        Console.WriteLine(" halves stat modifiers with Int(x/2), which floors - so odd numbers part company):");
        Console.WriteLine($"  {"raw",4}  {"att/def fine",12} {"adjacent",9} {"opposed",8}   {"stat fine",10} {"adjacent",9} {"opposed",8}");
        foreach (int raw in new[] { 1, 2, 3, 5, 7, 9, 10, 25, 33 })
            Console.WriteLine($"  {raw,4}  {ItemAlignments.AttDefWorth(raw, ItemFit.Fine),12} " +
                              $"{ItemAlignments.AttDefWorth(raw, ItemFit.Adjacent),9} " +
                              $"{ItemAlignments.AttDefWorth(raw, ItemFit.Opposed),8}   " +
                              $"{ItemAlignments.StatModWorth(raw, ItemFit.Fine),10} " +
                              $"{ItemAlignments.StatModWorth(raw, ItemFit.Adjacent),9} " +
                              $"{ItemAlignments.StatModWorth(raw, ItemFit.Opposed),8}");

        Console.WriteLine("\nand who is adjacent to whom:");
        foreach (var who in new[] { Alignment.Good, Alignment.Neutral, Alignment.Evil })
            Console.WriteLine($"  a {who,-8} character: " + string.Join("   ",
                ItemAlignments.All.Select(a => $"{ItemAlignments.Name(a)}={ItemAlignments.FitFor(a, who)}")));

        // ---- worked refusals ----
        Console.WriteLine("\nwhat the two physical limits actually stop:");
        var ch = Character.Create("Tester", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                  new[] { 18, 18, 18, 18, 18, 18 });
        ch.Guilds.Clear();
        foreach (var g in GuildData.All) ch.Guilds.Add(new GuildMembership { Guild = g.Name, Level = 99 });

        void Try(Item it, string note)
        {
            var slot = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Alignment = ItemAlignment.Unaligned };
            ch.PutInPack(slot);
            string verdict = !Character.IsWearable(it)
                ? "(refused in silence - not a worn class)"
                : ch.WearRefusal(slot, it, Lookup) ?? "goes on";
            if (verdict == "goes on") slot.Equipped = true; else ch.RemoveFromPack(slot);
            Console.WriteLine($"  {it.Name,-26} {Mdata1.ItemTypeName(it.Type),-16} " +
                              $"{it.NHands}h  {verdict,-46} {note}");
        }

        // only offer the tester things its guilds allow, so the physical limits are what we see
        bool Legal(Item i) => Character.IsWearable(i) && ch.EquipProblem(i) == null;
        Item? OfClass(string name, int hands) =>
            items.FirstOrDefault(i => Legal(i) && i.NHands == hands
                && Mdata1.ItemDescriptions[Character.WearClassOf(i)].Name == name);

        var twoHander = OfClass("Weapon", 2);
        var oneHander = OfClass("Weapon", 1);
        var shield = items.FirstOrDefault(i => Legal(i)
            && Mdata1.ItemDescriptions[Character.WearClassOf(i)].Name == "Shield");

        Console.WriteLine("  -- a two-hander leaves nothing for a shield --");
        if (twoHander != null) Try(twoHander, "takes both");
        if (shield != null) Try(shield, $"needs {shield.NHands}");
        ch.ClearPack();

        Console.WriteLine("  -- the other way round, the shield goes on first --");
        if (shield != null) Try(shield, "one hand");
        if (twoHander != null) Try(twoHander, "wants two, one left");
        if (oneHander != null) Try(oneHander, "fits");
        ch.ClearPack();

        Console.WriteLine("  -- one weapon is all you get, hands or no hands --");
        var weapons = items.Where(i => Legal(i)
            && Mdata1.ItemDescriptions[Character.WearClassOf(i)].Name == "Weapon" && i.NHands == 1).Take(2).ToList();
        foreach (var w in weapons) Try(w, "class max is 1");
        ch.ClearPack();

        Console.WriteLine("  -- and two of anything else worn --");
        var armour = items.Where(i => Legal(i)
            && Mdata1.ItemDescriptions[Character.WearClassOf(i)].Name == "Armor").Take(2).ToList();
        foreach (var a in armour) Try(a, "class max is 1");
        ch.ClearPack();

        Console.WriteLine("  -- a consumable is not discussed at all --");
        var potion = items.FirstOrDefault(i => !Character.IsWearable(i));
        if (potion != null) Try(potion, "class max is 0");
        var misc = items.FirstOrDefault(i => Character.MaxWornOfClass(Character.WearClassOf(i)) < 0);
        if (misc != null) { Try(misc, "class max is -1, so it goes on"); Try(misc, "and again, uncapped"); }
        ch.ClearPack();

        // ---- the race-minimum floor ----
        Console.WriteLine("\nthe check fn11B6 never makes: wearing it must not push a stat under your race's floor");
        Console.WriteLine("(MODULE6.BAS:1797-1815). The items with a negative modifier are the ones this catches:");
        foreach (var it in items.Where(i => Character.IsWearable(i) && i.StatsMod.Any(m => m < 0)).Take(6))
        {
            var worst = Enumerable.Range(0, Math.Min(6, it.StatsMod.Length)).OrderBy(i => it.StatsMod[i]).First();
            Console.WriteLine($"  {it.Name,-26} {RaceData.StatNames[worst]} {it.StatsMod[worst],+3}");
            foreach (string race in new[] { "Human", "Dwarf", "Gnome" })
            {
                var r = RaceData.ByName(race);
                var c = Character.Create("X", r, Alignment.Neutral, Sex.Male, (int[])r.Min.Clone());
                c.Guilds.Clear();
                foreach (var g in GuildData.All) c.Guilds.Add(new GuildMembership { Guild = g.Name, Level = 99 });
                var slot = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Alignment = ItemAlignment.Unaligned };
                c.PutInPack(slot);
                Console.WriteLine($"      a {race,-6} at the racial floor ({RaceData.StatNames[worst]} {r.Min[worst]}): " +
                                  (c.WearRefusal(slot, it, Lookup) ?? "goes on"));
            }
        }
    }
}
