using Mordor.Core;

/// <summary>
/// The party roster and the spell buffers: who leaves when, what a swap does, what the Combat
/// Setting can hold, and what happens to a buffer when the item it points at leaves the pack.
/// <c>sub2F40</c> and <c>lblName_DragDrop</c> (FRMGROUP.BAS), <c>sub04DC</c> (MAINMOD.BAS:414).
/// </summary>
public static class PartyProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        Character Make(string name, int level = 1)
        {
            var c = Character.Create(name, RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, 12, 12, 12, 12 });
            c.DungeonLevel = level;
            return c;
        }
        Party Four() => new Party(Make("Brannoc"), Make("Sela"), Make("Kord"), Make("Wren"));
        string Roster(Party p) => string.Join(", ", p.Members.Select((m, i) => $"{i + 1}.{m.Name}"));

        // ---- swapping ----
        Console.WriteLine("dragging one name onto another swaps them (FRMGROUP.BAS:218-228):");
        foreach (var (a, b) in new[] { (0, 2), (1, 3), (0, 1) })
        {
            var p = Four();
            string before = Roster(p);
            string? said = p.Swap(a, b);
            Console.WriteLine($"  {a + 1} <-> {b + 1}   {before}  ->  {Roster(p)}   {said ?? ""}");
        }
        Console.WriteLine("  the port used to lift one out and insert it, which rotates everyone in between:");
        Console.WriteLine("  swapping 1 and 3 would have given 3.Kord, 1.Brannoc, 2.Sela, 4.Wren.");

        // ---- leaving ----
        Console.WriteLine("\nanyone may leave, the leader included - sub04DC has no exemption:");
        foreach (int who in new[] { 0, 2 })
        {
            var p = Four();
            var leaving = p.Members[who];
            var next = p.LeaderAfterRemoving(leaving);
            p.Remove(leaving);
            Console.WriteLine($"  {leaving.Name,-8} leaves   {Roster(p)}   {(next != null ? Party.NowLeader(next) : "(leader unchanged)")}");
        }

        // ---- the dead ----
        Console.WriteLine("\nwho sub2F40 drops from the roster, and what it says:");
        void Dead(string what, Action<Party> setup)
        {
            var p = Four();
            setup(p);
            string before = Roster(p);
            var said = p.RemoveTheDead();
            Console.WriteLine($"  {what,-40} {before}  ->  {Roster(p)}");
            Console.WriteLine($"  {"",-40} {(said.Count == 0 ? "(said nothing)" : string.Join("  ", said))}");
        }
        Dead("nobody has died", _ => { });
        Dead("Sela dies on the leader's level", p => { p.Members[1].CurHits = 0; p.Members[1].IsDead = true; });
        Dead("Sela dies two floors up", p =>
        {
            p.Members[1].CurHits = 0; p.Members[1].IsDead = true; p.Members[1].DungeonLevel = 3;
        });
        Dead("three of the four fall", p =>
        {
            foreach (int i in new[] { 0, 1, 3 }) { p.Members[i].CurHits = 0; p.Members[i].IsDead = true; }
        });
        Dead("everyone falls", p =>
        {
            foreach (var m in p.Members) { m.CurHits = 0; m.IsDead = true; }
        });
        Console.WriteLine("  the last one standing is never pruned even when dead - that is what leaves the");
        Console.WriteLine("  Wait for Rescue screen a body to talk about.");

        var alone = new Party(Make("Solo"));
        alone.Members[0].CurHits = 0; alone.Members[0].IsDead = true;
        Console.WriteLine($"  a character adventuring alone: {alone.Size} member(s) before, " +
                          $"{(alone.RemoveTheDead().Count == 0 ? "nothing said" : "something said")}, {alone.Size} after.");

        // ---- Combat Settings ----
        Console.WriteLine("\nwhat the Combat Setting box can hold (gv058A$, MODULE1.BAS:416-419):");
        var names = CombatOrder.SettingNames();
        for (int i = 0; i < names.Length; i++) Console.WriteLine($"  {i,3}  {names[i]}");
        Console.WriteLine("  the port offered only the first three and clamped anything higher into range, so");
        Console.WriteLine("  opening Party Options rewrote a character set to a buffer as Defend.");

        Console.WriteLine("\nand what each setting becomes when a fight starts:");
        var heal = spells.FirstOrDefault(s => s.Name.Trim() == "Minor Heal") ?? spells[0];
        foreach (int setting in new[] { 0, 1, 2, 3, 5 })
        {
            var c = Make("Tester");
            c.CombatSetting = setting;
            c.Buffers[0] = SpellBuffers.ForSpell(spells.IndexOf(heal));
            c.Buffers[2] = SpellBuffers.ForItem(0);
            var order = new CombatOrder();
            order.ApplyDefault(c, spells);
            Console.WriteLine($"  {setting,3} {names[setting],-22} -> {order.Kind}" +
                              (order.SpellId > 0 ? $" \"{spells.First(s => s.Id == order.SpellId).Name.Trim()}\"" : ""));
        }
        Console.WriteLine("  buffer 3 holds an item here, and an item is not a standing order - it has charges");
        Console.WriteLine("  and is used once - so that setting comes out as None.");

        // ---- buffers against a shifting pack ----
        Console.WriteLine("\nwhat happens to the buffers when an item leaves the pack:");
        var pack = items.Where(i => i.Type == 23).Take(4).ToList();
        var ch = Make("Tester");
        foreach (var it in pack)
            ch.PutInPack(new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)Math.Max(1, it.Charges) });
        for (int i = 0; i < 4; i++) ch.Buffers[i] = SpellBuffers.ForItem(i);

        string Show(Character c) => string.Join("   ", Enumerable.Range(0, 4)
            .Select(i => $"buf{i + 1}={(SpellBuffers.IsEmpty(c.Buffers[i]) ? "-" : SpellBuffers.Describe(c, i, spells, Lookup).Replace("Item #", "#"))}"));

        Console.WriteLine($"  before          {Show(ch)}");
        var gone = ch.Inventory[1];
        Console.WriteLine($"  remove slot 2   ({Loot.NameFor(Lookup(gone.ItemId)!, IdLevel.All)})");
        ch.RemoveFromPack(gone);
        Console.WriteLine($"  after           {Show(ch)}");
        Console.WriteLine("  buffer 2 pointed at the item that left, so it empties; buffers 3 and 4 follow their");
        Console.WriteLine("  own items down a slot. Without the shift they would each have come to point at a");
        Console.WriteLine("  different potion, and pressing the key would have used the wrong one.");
    }
}
