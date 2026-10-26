using Mordor.Core;

/// <summary>
/// Everything in the game that destroys or steals one of your things rolls a <b>slot</b> and looks
/// in it - the chest's Slime (MAINMOD.BAS, trap 5), a monster's destroy and steal attacks
/// (MONSMOD.BAS:3295 and 3351), and quicksand. It does not pick one of the items you happen to be
/// carrying. The original is unambiguous: <c>If M5857(slot).M4ED4 &gt; 0</c>.
///
/// The port had been reading the compacted inventory at that number instead. On a pack with no
/// gaps the two agree, which is why it went unnoticed; the moment you drop, sell, bank or swap
/// anything the pack has holes, and then they disagree about <b>which</b> item goes - and about
/// whether anything goes at all.
///
/// This shows the disagreement on a gappy pack.
/// </summary>
public static class SlotRollProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"))
                             .Where(i => i.Id != 0 && ChestTraps.Destructible(i)).ToList();
        Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);

        // three things, in slots 5, 20 and 37 - a pack that has been lived in
        var c = new Character { Name = "Prober" };
        int[] where = { 5, 20, 37 };
        for (int i = 0; i < where.Length; i++)
            c.PutInSlot(where[i], new InventorySlot { ItemId = items[i].Id, Known = IdLevel.All });

        Console.WriteLine("a pack holding three things, in slots 5, 20 and 37:\n");
        for (int i = 0; i < where.Length; i++)
            Console.WriteLine($"  slot {where[i],2}: {Look(c.Pack[where[i]]!.ItemId)?.Name.Trim()}");

        Console.WriteLine();
        Console.WriteLine("what each roll of the Slime's die (1..39) finds:\n");
        Console.WriteLine("  roll   by slot (the original)      by list position (what the port did)");
        foreach (int roll in new[] { 1, 2, 3, 4, 5, 20, 37, 38 })
        {
            var bySlot = c.Pack[roll] is InventorySlot s && s.ItemId > 0 ? Look(s.ItemId)?.Name.Trim() : "(nothing)";
            var live = c.Inventory;
            var byList = roll - 1 < live.Count ? Look(live[roll - 1].ItemId)?.Name.Trim() : "(nothing)";
            Console.WriteLine($"  {roll,4}   {bySlot,-28}  {byList}");
        }

        Console.WriteLine();
        Console.WriteLine("so the two agree only on a pack with no gaps. With gaps, the old reading");
        Console.WriteLine("made low rolls always destructive and high ones never, and took the wrong");
        Console.WriteLine("item when it did fire.");

        Console.WriteLine();
        Console.WriteLine("the theft roll is 2 to 40, and forty is one past the end of the pack:");
        var rng = new Random(7);
        int missed = 0;
        for (int n = 0; n < 100000; n++)
            if ((int)Math.Floor(rng.NextDouble() * 39) + 2 >= Character.PackSize) missed++;
        Console.WriteLine($"  {missed * 100.0 / 100000:0.0}% of theft rolls land past the last slot and find nothing");
    }
}

/// <summary>
/// The Exchange window's swap, <c>pnlItem_DragDrop</c> (FRMEXCHA.BAS:152-205). It reads
/// <c>M5857</c> at the panel's own index - a pack slot - refuses when either end is slot 0, and
/// re-identifies whatever lands on the receiving side.
/// </summary>
public static class ExchangeSwapProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"))
                             .Where(i => i.Id != 0 && ChestTraps.Destructible(i)).ToList();
        Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);

        Character Make(string name)
        {
            var c = new Character { Name = name };
            for (int i = 0; i < 6; i++) { c.Stats[i] = 15; c.StatMax[i] = 15; }
            c.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 12 });
            return c;
        }

        var a = Make("Ann");
        var b = Make("Bob");
        a.PutInSlot(5, new InventorySlot { ItemId = items[0].Id, Known = IdLevel.Barely });
        b.PutInSlot(9, new InventorySlot { ItemId = items[1].Id, Known = IdLevel.Barely });

        void Show(string when)
        {
            Console.WriteLine($"  {when}");
            Console.WriteLine($"    Ann slot 6: {Name(a, 5)}");
            Console.WriteLine($"    Bob slot 10: {Name(b, 9)}");
        }
        string Name(Character c, int slot) =>
            c.Pack[slot] is InventorySlot s && s.ItemId > 0 ? Look(s.ItemId)?.Name.Trim() ?? "?" : "(empty)";

        Console.WriteLine("a swap between two people, by slot:\n");
        Show("before");
        string? no = Party.SwapItems(a, 5, b, 9, Look);
        Console.WriteLine($"    answer: {no ?? "done"}");
        Show("after");

        Console.WriteLine();
        Console.WriteLine("slot 1 is the Hands and cannot be swapped, at either end:");
        Console.WriteLine($"    from slot 1: {Party.SwapItems(a, 0, b, 9, Look) ?? "done"}");
        Console.WriteLine($"    onto slot 1: {Party.SwapItems(a, 5, b, 0, Look) ?? "done"}");
    }
}
