using Mordor.Core;

/// <summary>
/// The pack, <c>M5857</c>: forty fixed slots with the character's Hands in the first. Slot numbers
/// are what Swap Items, the spell buffers and the game's own messages all speak in, so they have to
/// stay put when something leaves.
/// </summary>
public static class PackProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).ToList();
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Look(short id) => byId.TryGetValue(id, out var x) ? x : null;

        var c = Character.Create("Tester", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                 new[] { 12, 12, 12, 12, 12, 12 });
        foreach (var it in items.Where(i => i.Id != 0 && i.Floor == 1).Take(6))
            c.PutInPack(new InventorySlot { ItemId = it.Id, Known = IdLevel.All });

        void Show(string when)
        {
            Console.WriteLine($"  {when}");
            for (int i = 0; i < 10; i++)
            {
                var s = c.Pack[i];
                string name = s == null || (i > 0 && s.ItemId == 0) ? "" : Look(s.ItemId)?.Name ?? "?";
                Console.WriteLine($"    {i + 1,2}. {name}");
            }
        }

        Console.WriteLine("slot 1 is your hands, and it is never empty:");
        Show("as packed");

        Console.WriteLine("\nremoving what is in slot 4 leaves a numbered gap - it does not close up:");
        c.RemoveFromPack(c.Pack[3]!);
        Show("after removing slot 4");

        Console.WriteLine("\nthe next thing picked up goes into that gap, as the original's scan does:");
        c.PutInPack(new InventorySlot { ItemId = items.First(i => i.Id != 0 && i.Floor == 2).Id, Known = IdLevel.All });
        Show("after picking something up");

        Console.WriteLine("\nSwap Items, by the numbers the player types:");
        foreach (var (a, b, why) in new[] { (2, 7, "an ordinary swap"), (1, 5, "refuses - slot 1 is your hands"),
                                            (3, 41, "refuses - past the fortieth slot") })
            Console.WriteLine($"    swap {a},{b}: {(c.SwapSlots(a, b) ? "done" : "refused")}   ({why})");
        Show("after swapping 2 and 7");

        Console.WriteLine("\nand a buffer keeps hold of its own item across all of that:");
        var ch2 = Character.Create("Buffers", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                   new[] { 12, 12, 12, 12, 12, 12 });
        foreach (var it in items.Where(i => i.Id != 0 && i.Floor == 1).Take(4))
            ch2.PutInPack(new InventorySlot { ItemId = it.Id, Known = IdLevel.All });
        for (int i = 0; i < 3; i++) ch2.Buffers[i] = SpellBuffers.ForItem(i + 1);

        string Buffers() => string.Join("   ", Enumerable.Range(0, 3).Select(i =>
            $"buf{i + 1}=" + (SpellBuffers.IsEmpty(ch2.Buffers[i])
                ? "-" : "slot " + (SpellBuffers.PackSlotOf(ch2.Buffers[i]) + 1))));

        Console.WriteLine($"    before          {Buffers()}");
        ch2.RemoveFromPack(ch2.Pack[2]!);
        Console.WriteLine($"    slot 3 removed  {Buffers()}   (buffer 2 let go, the others did not move)");
        ch2.SwapSlots(2, 4);
        Console.WriteLine($"    swapped 2,4     {Buffers()}   (buffer 1 followed its item)");
    }
}
