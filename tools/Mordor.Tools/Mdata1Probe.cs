using Mordor.Core;

/// <summary>
/// Walks MDATA1 the way the game walks it, instead of guessing at record ranges.
///
/// fn03D0 in MODULE1.BAS:678-716 opens it with a record length of 260 and then reads straight
/// through with record-less Gets, so the layout is completely determined:
///
///   record 1        version string
///   records 2..7    six counts: races, guilds, and the four name tables
///   then            that many Races, GuildDefs, gv1592, gv15C6, gv1662, gv1696
///
/// The last four are all the same shape - a name and one integer. gv1662 is the monster class
/// table and gv1696 the broader group table, which is what fn0E23 names a monster from before
/// you have identified it.
/// </summary>
public static class Mdata1Probe
{
    public static void Run(string dir)
    {
        string path = System.IO.Path.Combine(dir, "MDATA1.MDR");
        using var r = new RecordReader(path, 260);

        r.Seek(1); r.Read();
        string version = r.GetString();

        var counts = new int[6];
        for (int i = 0; i < 6; i++) { r.Seek(2 + i); r.Read(); counts[i] = r.GetUWord(); }

        string[] names = { "races", "guilds", "gv1592", "gv15C6", "gv1662 (monster class)", "gv1696 (monster group)" };
        Console.WriteLine($"MDATA1 version record: \"{version}\"");
        Console.WriteLine($"file is {new FileInfo(path).Length / 260} records of 260 bytes\n");
        for (int i = 0; i < 6; i++) Console.WriteLine($"  {names[i],-24} {counts[i]}");

        // records run on from 8, each table straight after the last
        int rec = 8;
        Console.WriteLine($"\n  races at record {rec}..{rec + counts[0] - 1}");
        rec += counts[0];
        Console.WriteLine($"  guilds at record {rec}..{rec + counts[1] - 1}");
        rec += counts[1];

        for (int t = 2; t < 6; t++)
        {
            Console.WriteLine($"\n{names[t]} - records {rec}..{rec + counts[t] - 1}");
            for (int i = 0; i < counts[t]; i++, rec++)
            {
                r.Seek(rec); r.Read();
                string name = r.GetString();
                int extra = r.GetWord();
                Console.WriteLine($"  {i,3}  {name,-24} -> {extra}");
            }
        }

        // and what the ladders actually produce, now that the tables are loaded
        Mdata1.LoadFrom(dir);
        RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        Console.WriteLine("\nwhat an item is called as you learn it (fn1345):");
        Console.WriteLine($"  {"item",-22} {"barely",-22} {"partial",-20} {"mostly/complete"}");
        foreach (var it in items.Where(i => i.Id != 0).Take(8))
            Console.WriteLine($"  {it.Name,-22} {Loot.NameFor(it, IdLevel.Barely),-22} " +
                              $"{Loot.NameFor(it, IdLevel.Partial),-20} {Loot.NameFor(it, IdLevel.Mostly)}");

        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        var viewer = Character.Create("Probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        viewer.DungeonLevel = 1;
        Console.WriteLine("\nwhat a monster is called before you identify it (fn0E23):");
        foreach (var m in mons.Take(8))
            Console.WriteLine($"  {m.Name,-22} -> {EncounterGenerator.NameFor(m, viewer, 1, identified: false)}");
        var inv = mons.FirstOrDefault(m => (m.SpecialPropertyFlags & (1 << Effects.Invisible)) != 0);
        if (inv != null)
            Console.WriteLine($"  invisible: {inv.Name} -> {EncounterGenerator.NameFor(inv, viewer, 2, identified: true)}");

        // Teleport offsets are relative to facing (mdrcharTeleport). From (15,15) 0-based,
        // "+3 right, +2 forwards" should go a different way for each facing, and the grid wraps.
        Console.WriteLine("\nteleport from (16,16) 1-based, right +3 forwards +2:");
        foreach (Facing f in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
        {
            var to = SpellEffect.TeleportTo(15, 15, f, 5, 3, 2);
            Console.WriteLine($"  facing {f,-6} -> ({to.X + 1},{to.Y + 1}) level {to.Level}");
        }
        Console.WriteLine("wrapping and depth:");
        foreach (var (x, y, rt, fwd, d, what) in new[]
                 {
                     (0, 15, -3, 0, 0, "off the west edge"),
                     (29, 15, 3, 0, 0, "off the east edge"),
                     (15, 29, 0, 4, 0, "off the north edge"),
                     (15, 15, 0, 0, 9, "down 9, clamped to 3"),
                     (15, 15, 0, 0, -9, "up 9, clamped to 3"),
                 })
        {
            var to = SpellEffect.TeleportTo(x, y, Facing.North, 5, rt, fwd, d);
            Console.WriteLine($"  {what,-22} ({x + 1},{y + 1}) -> ({to.X + 1},{to.Y + 1}) level {to.Level}");
        }

        // Soul Search reports where someone is in the same terms Teleport takes, so the two must
        // be exact inverses. Teleport by (r, f, d) then locate, and you should read back (r, f, d).
        Console.WriteLine("\nSoul Search is the inverse of Teleport:");
        int bad = 0;
        foreach (Facing f2 in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
            for (int rr = -4; rr <= 4; rr++)
                for (int ff = -4; ff <= 4; ff++)
                    for (int dd = -2; dd <= 2; dd++)
                    {
                        var t = SpellEffect.TeleportTo(14, 14, f2, 6, rr, ff, dd);
                        var back = SpellEffect.RelativeDirection(14, 14, 6, f2, t.X, t.Y, t.Level);
                        if (back.Right != rr || back.Forward != ff || back.Down != dd)
                        {
                            if (bad++ < 4)
                                Console.WriteLine($"  MISMATCH {f2} ({rr},{ff},{dd}) -> read back ({back.Right},{back.Forward},{back.Down})");
                        }
                    }
        Console.WriteLine(bad == 0 ? "  all 4 x 9 x 9 x 5 = 1620 round trips agree" : $"  {bad} mismatches");

        // spell buffers: ten slots, each an item in the pack or a spell, M59B0 in the original
        var buf = Character.Create("Probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        buf.ClearPack();
        foreach (var it in items.Where(i => i.Id != 0 && i.Floor == 1).Take(3))
            buf.PutInPack(new InventorySlot { ItemId = it.Id, Charges = (short)it.Charges });
        buf.Buffers[0] = SpellBuffers.ForItem(0);
        buf.Buffers[1] = SpellBuffers.ForSpell(spells.FindIndex(sp => sp.Name.Trim() == "Firebolt"));
        buf.Buffers[2] = SpellBuffers.ForItem(9);          // a pack slot that does not exist
        buf.Buffers[9] = SpellBuffers.ForSpell(9999);      // a spell that does not exist

        Console.WriteLine("\nspell buffers (slot 10 is the 0 key):");
        for (int i = 0; i < SpellBuffers.Count; i++)
            Console.WriteLine($"  {(i == 9 ? 0 : i + 1),2}. {SpellBuffers.Describe(buf, i, spells, id => byId.TryGetValue(id, out var x) ? x : null)}");
        Console.WriteLine($"  the two dangling buffers reset themselves: slot 3 = {buf.Buffers[2]}, slot 10 = {buf.Buffers[9]}");
    }
}
