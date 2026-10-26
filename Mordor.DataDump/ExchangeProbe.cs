using Mordor.Core;

/// <summary>
/// Checks the Exchange screen's rules (FRMEXCHA.BAS): what a swap refuses, how gold moves above
/// and below ground, and what happens to an item's identification when it changes hands.
/// </summary>
public static class ExchangeProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        Character Make(string name, int guildLevel, int intel)
        {
            var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, intel, intel, 12, 12, 12 });
            c.Guilds[0].Level = guildLevel;
            c.ClearPack();
            return c;
        }

        // ---- gold, above and below ground ----
        Console.WriteLine("gold transfer:");
        foreach (int level in new[] { 0, 3 })
        {
            var a = Make("Giver", 5, 12); var b = Make("Taker", 5, 12);
            a.DungeonLevel = b.DungeonLevel = level;
            a.GoldOnHand = 100; a.GoldInBank = 900;
            string? why = Party.TransferGold(a, b, 500);
            Console.WriteLine($"  {(level == 0 ? "in the city" : "below ground"),-13} give 500 of 100+900: " +
                              (why ?? $"ok - giver now {a.GoldOnHand:N0} on hand, {a.GoldInBank:N0} banked; taker {b.GoldOnHand:N0}"));
        }

        // ---- what a swap refuses ----
        Console.WriteLine("\nswap refusals:");
        var quest = items.FirstOrDefault(i => !ChestTraps.Destructible(i));
        var plain = items.First(i => i.Id != 0 && ChestTraps.Destructible(i));
        var other = items.Last(i => i.Id != 0 && ChestTraps.Destructible(i));

        void Try(string what, Action<Character, Character> setup)
        {
            var a = Make("A", 5, 12); var b = Make("B", 5, 12);
            setup(a, b);
            // PutInPack drops into the first free slot, which is 1 - slot 0 is the Hands, and
            // SwapItems refuses that end the way pnlItem_DragDrop does
            string? why = Party.SwapItems(a, 1, b, 1, Lookup);
            Console.WriteLine($"  {what,-34} {why ?? "allowed"}");
        }

        Try("plain items, two characters", (a, b) =>
        {
            a.PutInPack(new InventorySlot { ItemId = plain.Id });
            b.PutInPack(new InventorySlot { ItemId = other.Id });
        });
        Try("source is worn", (a, b) =>
        {
            a.PutInPack(new InventorySlot { ItemId = plain.Id, Equipped = true });
            b.PutInPack(new InventorySlot { ItemId = other.Id });
        });
        Try("destination is worn", (a, b) =>
        {
            a.PutInPack(new InventorySlot { ItemId = plain.Id });
            b.PutInPack(new InventorySlot { ItemId = other.Id, Equipped = true });
        });
        if (quest != null)
            Try("source is guild-issued", (a, b) =>
            {
                a.PutInPack(new InventorySlot { ItemId = quest.Id });
                b.PutInPack(new InventorySlot { ItemId = other.Id });
            });

        // ---- identification on receipt ----
        var novice = Make("Novice", 1, 10);
        var sage = Make("Sage", 60, 25);
        Console.WriteLine($"\nreading items (fn12F0). Novice skill " +
                          $"{Rules.IdentifySkill(novice.TopGuildLevel, 10, 10):0.0}, " +
                          $"sage skill {Rules.IdentifySkill(sage.TopGuildLevel, 25, 25):0.0}:");
        Console.WriteLine($"  {"item",-26} {"floor",5} {"difficulty",10}  {"novice",-8} {"sage",-8}");
        foreach (int floor in new[] { 1, 3, 5, 6, 8, 12, 16 })
        {
            var it = items.FirstOrDefault(i => i.Id != 0 && i.Floor == floor);
            if (it == null) continue;
            Console.WriteLine($"  {it.Name,-26} {it.Floor,5} {Loot.IdentifyDifficulty(it),10:0.0}  " +
                              $"{Loot.ReadableLevel(novice, it),-8} {Loot.ReadableLevel(sage, it),-8}");
        }

        // a shallow item the sage reads better than the novice who is handing it over
        var shallow = items.First(i => i.Id != 0 && i.Floor is >= 1 and <= 5 && ChestTraps.Destructible(i));
        var slot = new InventorySlot { ItemId = shallow.Id, Known = IdLevel.Barely };
        novice.ClearPack(); novice.PutInPack(slot);
        sage.ClearPack(); sage.PutInPack(new InventorySlot { ItemId = plain.Id });
        Party.SwapItems(novice, 0, sage, 0, Lookup);
        Console.WriteLine($"\n  the novice hands the sage a {shallow.Name} known only {IdLevel.Barely}; " +
                          $"in the sage's pack it reads {slot.Known}");

        var alreadyKnown = new InventorySlot { ItemId = shallow.Id, Known = IdLevel.All };
        sage.ClearPack(); sage.PutInPack(alreadyKnown);
        novice.ClearPack(); novice.PutInPack(new InventorySlot { ItemId = plain.Id });
        Party.SwapItems(sage, 0, novice, 0, Lookup);
        Console.WriteLine($"  a fully identified item handed on stays {alreadyKnown.Known} - knowledge is never lost");

        // the drag runs on the panel dropped upon, so only that side looks over what it got
        var goingToTheNovice = new InventorySlot { ItemId = shallow.Id, Known = IdLevel.Barely };
        var goingToTheSage = new InventorySlot { ItemId = shallow.Id, Known = IdLevel.Barely };
        sage.ClearPack(); sage.PutInPack(goingToTheNovice);
        novice.ClearPack(); novice.PutInPack(goingToTheSage);
        Party.SwapItems(novice, 0, sage, 0, Lookup);
        Console.WriteLine($"  dragging novice -> sage: the sage's new item reads {goingToTheSage.Known}, " +
                          $"the novice's stays {goingToTheNovice.Known} (only the side dropped on re-reads)");
    }
}
