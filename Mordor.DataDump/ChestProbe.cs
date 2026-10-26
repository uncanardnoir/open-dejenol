using Mordor.Core;

/// <summary>
/// Checks what a chest gives up (monOpenChest, MONSMOD.BAS:3656-3861): how often it yields
/// anything, how many items it hands over, and which ones the contiguous-run pick can reach.
/// </summary>
public static class ChestProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var rng = new Random(29);

        Console.WriteLine("how many items one chest gives up, by the monster's own item chance:");
        Console.WriteLine($"  {"monster",-22} {"chance",7} {"nothing",8} {"1",6} {"2",6} {"3+",6} {"mean",6}");
        foreach (var lead in monsters.Where(m => m.Id != 0 && m.ItemChance > 0)
                                     .GroupBy(m => m.ItemChance / 20).Select(g => g.First()).OrderBy(m => m.ItemChance))
        {
            var counts = new int[40];
            for (int i = 0; i < 4000; i++)
            {
                int n = 0;
                bool first = true;
                for (int guard = 0; guard < 39; guard++)
                {
                    bool more = first ? Loot.DropsItem(lead, 2, rng) : Loot.DropsAnother(lead, rng);
                    first = false;
                    if (!more) break;
                    n++;
                }
                counts[n]++;
            }
            double mean = 0;
            for (int i = 0; i < counts.Length; i++) mean += i * counts[i] / 4000.0;
            int three = 0; for (int i = 3; i < counts.Length; i++) three += counts[i];
            Console.WriteLine($"  {lead.Name,-22} {lead.ItemChance,6}% {counts[0] / 40.0,7:0.0}% {counts[1] / 40.0,5:0.0}% {counts[2] / 40.0,5:0.0}% {three / 40.0,5:0.0}% {mean,6:0.00}");
        }

        Console.WriteLine("\nforcing a magic lock with a Charm of Opening costs you (chest class 2):");
        foreach (var lead in monsters.Where(m => m.Id != 0 && m.ItemChance is > 30 and < 70).Take(1))
            foreach (int lvl in new[] { 1, 8, 15 })
            {
                int plain = 0, forced = 0;
                for (int i = 0; i < 4000; i++)
                {
                    if (Loot.DropsItem(lead, 2, rng)) plain++;
                    if (Loot.DropsItem(lead, 2, rng, lockState: -1, dungeonLevel: lvl)) forced++;
                }
                Console.WriteLine($"  {lead.Name} on level {lvl,2}: {plain / 40.0,5:0.0}% normally, {forced / 40.0,5:0.0}% after forcing the lock");
            }

        // MONSMOD.BAS:3508-3509. Disarming the trap pays; finding no trap pays nothing. The guild
        // level that discounts the award is Chars(0)'s - the party's first character - even though
        // the chance and the experience are the opener's, so the same disarm is worth less to a
        // second-in-line character simply because whoever stands first has levelled.
        Console.WriteLine("\nexperience for disarming a chest, at a disarm chance of 60%:");
        Console.WriteLine($"  {"monster",-22} {"guild lvl",9} {"first char L1",14} {"first char L100",16} {"first char L333",16}");
        foreach (string name in new[] { "Kobold", "Goblin", "Giant Owl" })
        {
            var lead = monsters.FirstOrDefault(m => m.Name == name);
            if (lead == null) continue;
            Console.WriteLine($"  {lead.Name,-22} {lead.GuildLevel,9} "
                            + $"{Rules.ChestExp(60, lead.GuildLevel, 1),14:N0} "
                            + $"{Rules.ChestExp(60, lead.GuildLevel, 100),16:N0} "
                            + $"{Rules.ChestExp(60, lead.GuildLevel, 333),16:N0}");
        }

        Console.WriteLine("\nwhat a few monsters actually hand over (200 draws each):");
        foreach (string name in new[] { "Kobold", "Goblin", "Giant Owl" })
        {
            var lead = monsters.FirstOrDefault(m => m.Name == name);
            if (lead == null) continue;
            var got = new Dictionary<string, int>();
            for (int i = 0; i < 200; i++)
                if (Loot.RollDrop(items, lead, 2, rng) is Item it) got[it.Name] = got.GetValueOrDefault(it.Name) + 1;
            Console.WriteLine($"  {lead.Name} (drop level {lead.ItemDropLevel}): "
                + string.Join(", ", got.OrderByDescending(k => k.Value).Take(6).Select(k => $"{k.Key} {k.Value}")));
        }
    }
}
