using Mordor.Core;

/// <summary>
/// The fifteenth floor against the two shelves that are supposed to stock from it.
///
/// <c>mdrReplenishStore</c> and <c>mdrReplenishCStore</c> both read the deepest level anyone has
/// reached and then cap it with <c>If x >= mc0118 - 1 Then x = mc0118 - 1</c>, where mc0118 is 15
/// (MAINMOD.BAS:1506 and 1308). Three other uses of that constant in the same file are loop bounds
/// running 1 to 15, so 15 is the floor count and the caps are one short.
///
/// This runs both replenishers hard, with the deepest level set to the real bottom, and counts what
/// arrives.
/// </summary>
public static class DeepestProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));

        var deepItems = items.Where(i => i.Id != 0 && i.Floor == 15).ToList();
        var deepMons = mons.Where(m => m.Id != 0 && m.LevelFound >= 15).ToList();
        Console.WriteLine($"items whose only floor is 15:    {deepItems.Count}");
        foreach (var it in deepItems.Take(12))
            Console.WriteLine($"    {it.Name.Trim(),-28} price {it.Price,12:N0}  rarity {it.Rarity,3}  type {it.Type}");
        if (deepItems.Count > 12) Console.WriteLine($"    ... and {deepItems.Count - 12} more");
        foreach (var it in items.Where(i => i.Id != 0 && i.Floor > 15))
            Console.WriteLine($"  past the bottom: floor {it.Floor}  \"{it.Name.Trim()}\"  price {it.Price:N0}  rarity {it.Rarity}");
        Console.WriteLine($"\nmonsters found no shallower than 15: {deepMons.Count}"
                        + $" (of which LevelFound 16: {deepMons.Count(m => m.LevelFound == 16)})");

        Console.WriteLine("\nitems per floor (MDATA3):");
        foreach (var g in items.Where(i => i.Id != 0).GroupBy(i => (int)i.Floor).OrderBy(g => g.Key))
            Console.Write($"  {g.Key}:{g.Count()}");
        Console.WriteLine();
        Console.WriteLine("monsters per LevelFound (MDATA5):");
        foreach (var g in mons.Where(m => m.Id != 0).GroupBy(m => (int)m.LevelFound).OrderBy(g => g.Key))
            Console.Write($"  {g.Key}:{g.Count()}");
        Console.WriteLine();

        // the store, run with the true bottom of the dungeon rather than the capped one
        var shelf = new StoreStock();
        var rng = new Random(20260922);
        for (int i = 0; i < 6000; i++) shelf.Replenish(items, 50, rng, deepestReached: 15);
        var stocked = shelf.Shelf.Where(kv => kv.Value > 0).Select(kv => short.Parse(kv.Key.Split(":")[0])).Distinct().ToList();
        int deepOnShelf = stocked.Count(id => items.FirstOrDefault(i => i.Id == id) is { Floor: 15 });
        int deepestFloorSeen = stocked.Select(id => (int)(items.FirstOrDefault(i => i.Id == id)?.Floor ?? 0)).DefaultIfEmpty(0).Max();
        Console.WriteLine($"\nstore, 6,000 replenishes with deepestReached = 15:");
        Console.WriteLine($"    distinct items stocked:      {stocked.Count}");
        Console.WriteLine($"    of those, floor 15:          {deepOnShelf}");
        Console.WriteLine($"    deepest floor that appeared: {deepestFloorSeen}");

        // and the pens
        var pens = new Confinement();
        var prng = new Random(20260922);
        for (int i = 0; i < 6000; i++) pens.Replenish(mons, 50, prng, deepestReached: 15);
        var penned = pens.Entries.Where(e => e.Count > 0).Select(e => e.Ref).Distinct().ToList();
        int deepPenned = penned.Count(r => mons.FirstOrDefault(m => m.Ref == r) is { LevelFound: >= 15 });
        int deepestPen = penned.Select(r => (int)(mons.FirstOrDefault(m => m.Ref == r)?.LevelFound ?? 0)).DefaultIfEmpty(0).Max();
        Console.WriteLine($"\npens, 6,000 replenishes with deepestReached = 15:");
        Console.WriteLine($"    distinct creatures penned:   {penned.Count}");
        Console.WriteLine($"    of those, found at 15+:      {deepPenned}");
        Console.WriteLine($"    deepest LevelFound penned:   {deepestPen}");

        int maxDrop = mons.Where(m => m.Id != 0).Select(m => (int)m.ItemDropLevel).DefaultIfEmpty(0).Max();
        Console.WriteLine($"\ndeepest ItemDropLevel any monster carries: {maxDrop}"
                        + $"  (monsters reaching it: {mons.Count(m => m.Id != 0 && m.ItemDropLevel == maxDrop)})");
        Console.WriteLine("  - the drop band is taken from the monster, not the floor, so this is the");
        Console.WriteLine("    only route to anything the store will not stock.");

        Console.WriteLine("\nso the only way a floor 15 item reaches the shelf is a player selling one in.");
    }
}
