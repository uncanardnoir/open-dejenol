using Mordor.Core;

/// <summary>
/// The store's trading rules: what buying and selling do to the shelf (FRMCTYS.BAS:37-283), what
/// Combine will and will not pour together (<c>sub2621</c>, FRMCTYS.BAS:612-693), and what the
/// other adventurers do to the shelf while you rest (<c>mdrReplenishStore</c>, MAINMOD.BAS:1479).
/// </summary>
public static class TradeProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        // ---- what the shelf does when you trade ----
        Console.WriteLine("what buying takes off the shelf, and what selling puts back:");
        Console.WriteLine($"  {"item",-26} {"floor",5}  {"bought as",-10} {"depletes",8}   {"sold as",-10} {"shelved",7}");
        var samples = new[]
        {
            items.First(i => i.Floor == 0 && i.Price > 0 && i.AlignmentFlags <= 1),
            items.First(i => i.Floor > 0),
            items.First(i => i.Type == 34),
        };
        foreach (var it in samples)
            foreach (var a in new[] { ItemAlignment.Unaligned, ItemAlignment.Good })
                Console.WriteLine($"  {it.Name,-26} {it.Floor,5}  {ItemAlignments.Name(a),-10} " +
                                  $"{(StoreStock.DepletedByBuying(it, a) ? "yes" : "no"),8}   " +
                                  $"{ItemAlignments.Name(a),-10} {(StoreStock.ShelvedBySelling(it, a) ? "yes" : "no"),7}");
        Console.WriteLine("  the floor-0 unaligned row is the store's own bottomless stock: buying never");
        Console.WriteLine("  lowers it and selling never raises it. An *aligned* copy of that same item does both.");
        Console.WriteLine($"  a guild crest is floor 0 with no price, so it falls in the same row - and Sell");
        Console.WriteLine($"  refuses it outright anyway: \"Cannot sell Guild-Issued Items!\"");

        // ---- the pack limit ----
        Console.WriteLine($"\nthe pack holds {StoreStock.PackSlots} things (slots 1 to 39 of a forty-slot array), and Buy");
        Console.WriteLine("  checks for a free one before it checks your purse: \"No free slots!\"");

        // ---- Combine ----
        Console.WriteLine("\nwhat Combine accepts and refuses:");
        var potion = items.First(i => i.Type == 23 && i.Charges > 0);
        var otherPotion = items.First(i => i.Type == 23 && i.Id != potion.Id && i.Charges > 0);
        var sword = items.First(i => i.Type == 3);
        var scroll = items.First(i => i.Type == 24);

        void Show(string what, params (Item it, IdLevel known, ItemAlignment a, short charges)[] spec)
        {
            var c = Character.Create("Tester", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, 12, 12, 12, 12 });
            foreach (var (it, known, a, ch) in spec)
                c.PutInPack(new InventorySlot { ItemId = it.Id, Known = known, Alignment = a, Charges = ch });
            var chosen = c.Inventory.ToList();
            var numbers = Enumerable.Range(0, chosen.Count).ToList();

            string? no = Combining.Refusal(chosen, Lookup, numbers);
            if (no != null)
            {
                Console.WriteLine($"  {what,-40} {(no == "" ? "(nothing to do)" : no)}");
                return;
            }
            var said = Combining.Combine(c, chosen, Lookup, numbers);
            Console.WriteLine($"  {what,-40} {string.Join("  ", said.Select(r => r.Line))}");
            Console.WriteLine($"  {"",-40} left holding {c.Inventory.Count}, first has {c.Inventory[0].Charges} charges");
        }

        Show("two of the same potion",
             (potion, IdLevel.All, ItemAlignment.Unaligned, 2), (potion, IdLevel.All, ItemAlignment.Unaligned, 3));
        Show("three of the same potion",
             (potion, IdLevel.All, ItemAlignment.Unaligned, 1), (potion, IdLevel.All, ItemAlignment.Unaligned, 1),
             (potion, IdLevel.All, ItemAlignment.Unaligned, 1));
        Show("one of them not identified",
             (potion, IdLevel.All, ItemAlignment.Unaligned, 2), (potion, IdLevel.Mostly, ItemAlignment.Unaligned, 3));
        Show("two swords",
             (sword, IdLevel.All, ItemAlignment.Unaligned, 0), (sword, IdLevel.All, ItemAlignment.Unaligned, 0));
        Show("a potion and a scroll",
             (potion, IdLevel.All, ItemAlignment.Unaligned, 2), (scroll, IdLevel.All, ItemAlignment.Unaligned, 3));
        Show("two different potions",
             (potion, IdLevel.All, ItemAlignment.Unaligned, 2), (otherPotion, IdLevel.All, ItemAlignment.Unaligned, 3));
        Show("same potion, different alignments",
             (potion, IdLevel.All, ItemAlignment.Unaligned, 2), (potion, IdLevel.All, ItemAlignment.Good, 3));
        Show("one potion on its own", (potion, IdLevel.All, ItemAlignment.Unaligned, 2));
        Console.WriteLine("  note the two middle messages read the wrong way round in the original: a mismatched");
        Console.WriteLine("  *type* says \"not compatible class\" and a mismatched *item* says \"not of the same type\".");

        // ---- Replenish ----
        Console.WriteLine("\nwhat the other adventurers do to the shelf while you rest.");
        Console.WriteLine("Each pass moves exactly one item and has a Clamp(days,1,50)% chance of running again,");
        Console.WriteLine("so a single rest rarely changes more than an item or two. Totals over 400 rests:");
        Console.WriteLine($"  {"days",5} {"deepest",8}  {"passes",7} {"added",7} {"removed",8}  {"net",6}  {"deepest floor touched",-21}");
        foreach (int days in new[] { 1, 5, 20, 50 })
            foreach (int deep in new[] { 1, 5, 15 })
            {
                var rng = new Random(20260920 + days * 31 + deep);
                int up = 0, down = 0, deepestTouched = 0;
                for (int rest = 0; rest < 400; rest++)
                {
                    var shelf = StoreStock.CreateInitial(items);
                    var before = items.ToDictionary(i => i.Id, i => Sum(shelf, i));
                    shelf.Replenish(items, days, rng, deep);
                    foreach (var it in items)
                    {
                        int d = Sum(shelf, it) - before[it.Id];
                        if (d == 0) continue;
                        if (d > 0) up += d; else down -= d;
                        deepestTouched = Math.Max(deepestTouched, (int)it.Floor);
                    }
                }
                Console.WriteLine($"  {days,5} {deep,8}  {up + down,7} {up,7} {down,8}  {up - down,6}  {deepestTouched,-21}");
            }
        Console.WriteLine("  60% of passes add and 40% remove, so a shelf that fills can empty again - the port");
        Console.WriteLine("  only ever added, and its shelves could only grow.");
        Console.WriteLine($"  nobody draws from below floor {StoreStock.DeepestStocked}, one short of the fifteen the game has, so a");
        Console.WriteLine("  floor-15 item reaches the shelf only when a player sells one in.");

        int Sum(StoreStock s, Item i) => ItemAlignments.All.Sum(a => s.Count(i.Id, a));

        // ---- the Confinement pens ----
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        Console.WriteLine("\nthe Confinement pens a new game ships with (sub054E, MAINMOD.BAS:539):");
        var pens = Confinement.Fresh(monsters);
        foreach (var m in monsters.Where(m => m.Id != 0).OrderBy(m => m.Type).ThenBy(m => m.Id))
        {
            int n = ItemAlignments.All.Sum(a => pens.Count(m, a));
            if (n == 0) continue;
            Console.WriteLine($"  {n} x {m.Name,-24} kind {Mdata1.MonsterClassName(m.Type),-12} " +
                              $"{ItemAlignments.Name(Confinement.AlignmentOf(m)),-10} floor {m.LevelFound}");
        }
        Console.WriteLine("  the port used to start with the pens empty, so a new character had nothing to buy.");

        Console.WriteLine("\nand what other people do to the pens while you rest (400 rests):");
        Console.WriteLine($"  {"days",5} {"deepest",8}  {"penned",7} {"bought",7}  {"net",6}  {"deepest floor touched",-21}");
        foreach (int days in new[] { 5, 20, 50 })
            foreach (int deep in new[] { 1, 5, 15 })
            {
                var rng = new Random(20260920 + days * 17 + deep);
                int up = 0, down = 0, deepestTouched = 0;
                for (int rest = 0; rest < 400; rest++)
                {
                    var p = Confinement.Fresh(monsters);
                    var before = monsters.Where(m => m.Id != 0)
                                         .ToDictionary(m => m.Ref, m => PenTotal(p, m));
                    p.Replenish(monsters, days, rng, deep);
                    foreach (var m in monsters.Where(m => m.Id != 0))
                    {
                        int d = PenTotal(p, m) - before[m.Ref];
                        if (d == 0) continue;
                        if (d > 0) up += d; else down -= d;
                        deepestTouched = Math.Max(deepestTouched, (int)m.LevelFound);
                    }
                }
                Console.WriteLine($"  {days,5} {deep,8}  {up,7} {down,7}  {up - down,6}  {deepestTouched,-21}");
            }
        Console.WriteLine("  60/40 again, and the same floor cap. The cap test in this copy reads the *unaligned*");
        Console.WriteLine("  count while the increment lands on the creature's own alignment - a slip the store's");
        Console.WriteLine("  copy of the same loop does not have, and one the port reproduces.");

        int PenTotal(Confinement p, Monster m) => ItemAlignments.All.Sum(a => p.Count(m, a));
    }
}
