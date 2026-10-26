using Mordor.Core;

/// <summary>
/// Checks the endgame: where Asmodeus can be found, what his chest yields, and the ending text.
/// </summary>
public static class AsmodeusProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var dungeon = MdrLoader.LoadDungeon(Path.Combine(dir, "MDATA11.MDR"));

        var asmo = monsters.FirstOrDefault(m => Asmodeus.Is(m));
        if (asmo == null) { Console.WriteLine($"No monster with id {Asmodeus.MonsterId} in MDATA5."); return; }

        Console.WriteLine($"{asmo.Name}: id {asmo.Id}, {asmo.Hits} hits, attack {asmo.Att}, " +
                          $"guild level {asmo.GuildLevel}, found from floor {asmo.LevelFound}, " +
                          $"power {HallRecords.MonsterPower(asmo)}");

        // ---- where he lives ----
        Console.WriteLine("\nwhere he can be found:");
        bool anywhere = false;
        for (int lvl = 0; lvl < dungeon.Count; lvl++)
        {
            var f = dungeon[lvl];
            var lairs = f.Areas.Select((a, i) => (a, i)).Where(t => t.a.LairId == asmo.Id).ToList();
            if (lairs.Count > 0)
            {
                anywhere = true;
                Console.WriteLine($"  level {lvl + 1}: {lairs.Count} lair(s), area index(es) " +
                                  string.Join(", ", lairs.Select(t => t.i)));
            }
        }
        if (!anywhere)
            Console.WriteLine("  no area names him as its lair monster.");

        Console.WriteLine("\n  every lair the dungeon defines:");
        for (int lvl = 0; lvl < dungeon.Count; lvl++)
        {
            var named = dungeon[lvl].Areas.Where(a => a.LairId != 0)
                .GroupBy(a => a.LairId)
                .Select(g => $"{monsters.FirstOrDefault(m => m.Id == g.Key)?.Name ?? $"#{g.Key}"} x{g.Count()}")
                .ToList();
            if (named.Count > 0) Console.WriteLine($"    level {lvl + 1,2}: {string.Join(", ", named)}");
        }

        Console.WriteLine($"  by LevelFound he is eligible from floor {asmo.LevelFound} " +
                          $"(the dungeon has {dungeon.Count})");

        // MODULE4.BAS:1432 - a square flag adds one to the floor the encounter is drawn from,
        // which is the only way a floor-16 monster can be rolled at all. Find which bit that is
        // by looking for one that is rare and gets rarer with depth, or appears only deep down.
        Console.WriteLine("\n  how many squares carry each flag bit, by level:");
        Console.Write("    bit ");
        for (int lvl = 1; lvl <= dungeon.Count; lvl++) Console.Write($"{lvl,5}");
        Console.WriteLine("   name");
        for (int bit = 0; bit < 32; bit++)
        {
            var counts = dungeon.Select(f => Enumerable.Range(0, f.Width * f.Height)
                                              .Count(i => (f.Fields[i].Flags & (1L << bit)) != 0)).ToList();
            if (counts.Sum() == 0) continue;
            Console.Write($"    {bit,3} ");
            foreach (int n in counts) Console.Write($"{n,5}");
            Console.WriteLine("   " + BitName(bit));
        }

        // sub06D7 (MODULE4.BAS:4033) builds a contiguous index band per floor over a table that
        // is sorted by floor - and then extends the last band to the end of the table. Anything
        // filed past the deepest floor therefore lands in the deepest floor's pool.
        var real = monsters.Where(m => m.Id != 0).ToList();
        var breaks = real.Zip(real.Skip(1)).Select((p, i) => (i, p.First, p.Second))
                         .Where(t => t.First.LevelFound > t.Second.LevelFound).ToList();
        Console.WriteLine($"\nthe monster table is sorted by floor: {breaks.Count == 0}" +
                          (breaks.Count == 0 ? "" : $" ({breaks.Count} place(s) out of order)"));
        foreach (var (i, a, b) in breaks.Take(6))
            Console.WriteLine($"    at {i}: {a.Name} (floor {a.LevelFound}) then {b.Name} (floor {b.LevelFound})");
        Console.WriteLine($"  floors present: {real.Min(m => m.LevelFound)}..{real.Max(m => m.LevelFound)}, " +
                          $"{real.Count} monsters");
        var past = real.Where(m => m.LevelFound > dungeon.Count).ToList();
        Console.WriteLine($"  filed past the deepest floor ({dungeon.Count}): {past.Count} - " +
                          string.Join(", ", past.Select(m => $"{m.Name} ({m.LevelFound})")));
        Console.WriteLine("  sub06D7's last line extends the deepest band over exactly those, so they");
        Console.WriteLine($"  appear on floor {dungeon.Count} and nowhere else.");

        // ---- is he actually reachable? ----
        Console.WriteLine("\nthe encounter window, ordinary square vs stud square:");
        var wr = new Random(11);
        Console.WriteLine($"  {"level",5}  {"ordinary",-22} {"on a stud square",-22}");
        foreach (int lvl in new[] { 1, 5, 10, 14, 15 })
        {
            string Window(bool stud)
            {
                var seen = new HashSet<(int, int)>();
                for (int i = 0; i < 4000; i++)
                {
                    int t = Rules.EncounterFloor(lvl, stud);
                    seen.Add((Math.Max(1, t - Rules.EncounterSpread(lvl, wr, stud)), t));
                }
                return string.Join(", ", seen.OrderBy(w => w.Item1).Select(w => $"{w.Item1}..{w.Item2}"));
            }
            Console.WriteLine($"  {lvl,5}  {Window(false),-22} {Window(true),-22}");
        }

        var studFloor = dungeon[dungeon.Count - 1];
        var studs = Enumerable.Range(0, studFloor.Width * studFloor.Height)
                              .Where(i => (studFloor.Fields[i].Flags & (1L << Floor.STUD)) != 0).ToList();
        Console.WriteLine($"\n  level {dungeon.Count} has {studs.Count} stud squares. Rolling encounters on them:");
        var grng = new Random(5);
        var met = new Dictionary<string, int>();
        for (int i = 0; i < 20000; i++)
        {
            int cell = studs[grng.Next(studs.Count)];
            var g = EncounterGenerator.Generate(monsters, dungeon.Count, grng, studFloor, cell % studFloor.Width, cell / studFloor.Width);
            if (g.Count > 0) met[g[0].Def.Name] = met.GetValueOrDefault(g[0].Def.Name) + 1;
        }
        Console.WriteLine($"    {met.Count} different leaders in 20,000 encounters; the floor-16 ones:");
        foreach (var m in past.OrderByDescending(HallRecords.MonsterPower))
            Console.WriteLine($"      {met.GetValueOrDefault(m.Name),5}  {m.Name}");

        // ---- his chest ----
        Console.WriteLine($"\nhis drop band is the top three floors of his range rather than the usual half:");
        var (low, top) = Asmodeus.DropBand(asmo);
        Console.WriteLine($"  ItemDropLevel {asmo.ItemDropLevel} -> floors {low}..{top} " +
                          $"(an ordinary monster of his rank would draw from {asmo.ItemDropLevel / 2}..{asmo.ItemDropLevel})");

        var rng = new Random(7);
        var got = new Dictionary<string, int>();
        for (int i = 0; i < 4000; i++)
            if (Loot.RollDrop(items, asmo, 2, rng) is Item it)
                got[it.Name] = got.GetValueOrDefault(it.Name) + 1;
        Console.WriteLine("  what 4,000 of his chests held, most often first:");
        foreach (var (name, n) in got.OrderByDescending(kv => kv.Value).Take(8))
        {
            var it = items.First(x => x.Name == name);
            Console.WriteLine($"    {n,5}  {name,-28} floor {it.Floor,3}");
        }
        Console.WriteLine($"    ...{got.Count} different items in all, " +
                          $"floors {got.Keys.Select(n => items.First(x => x.Name == n).Floor).Min()}" +
                          $"..{got.Keys.Select(n => items.First(x => x.Name == n).Floor).Max()}");

        // ---- the ending ----
        Console.WriteLine("\nthe ending, alone:\n");
        Console.WriteLine(Indent(Asmodeus.Ending(false)));
        Console.WriteLine("\nand the two lines it writes to the guild log:");
        var c = Character.Create("Tester", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        Console.WriteLine("  alone:      " + Asmodeus.LogLine(c, false));
        Console.WriteLine("  in a party: " + Asmodeus.LogLine(c, true));

        Console.WriteLine("\nwhat changes in the party wording:");
        foreach (var line in Diff(Asmodeus.Ending(false), Asmodeus.Ending(true))) Console.WriteLine("  " + line);
    }

    static string Indent(string s) => string.Join("\n", s.Split('\n').Select(l => "    " + l));

    /// <summary>The sentences that differ between the lone and party forms.</summary>
    static IEnumerable<string> Diff(string alone, string party)
    {
        var a = alone.Split("\n\n");
        var p = party.Split("\n\n");
        for (int i = 0; i < Math.Min(a.Length, p.Length); i++)
            if (a[i] != p[i]) yield return $"[{i}] alone: ...{Tail(a[i])}\n      party: ...{Tail(p[i])}";
    }

    static string Tail(string s) => s.Length <= 96 ? s : s[..96] + "...";

    static string BitName(int bit) => bit switch
    {
        Floor.WALL_EAST => "wall east", Floor.WALL_NORTH => "wall north",
        Floor.DOOR_EAST => "door east", Floor.DOOR_NORTH => "door north",
        Floor.SECRET_DOOR_EAST => "secret door east", Floor.SECRET_DOOR_NORTH => "secret door north",
        Floor.FACE_NORTH => "face north", Floor.FACE_EAST => "face east",
        Floor.FACE_SOUTH => "face south", Floor.FACE_WEST => "face west",
        Floor.EXTINGUISHER => "extinguisher", Floor.PIT => "pit",
        Floor.STAIRS_UP => "stairs up", Floor.STAIRS_DOWN => "stairs down",
        Floor.TELEPORTER => "teleporter", Floor.WATER => "water",
        Floor.QUICKSAND => "quicksand", Floor.ROTATOR => "rotator",
        Floor.ANTIMAGIC => "anti-magic", Floor.ROCK => "rock",
        Floor.FOG => "fog", Floor.CHUTE => "chute",
        Floor.STUD => "stud", Floor.RESCUE => "doubles a rescue chance",
        _ => "(unnamed)",
    };
}
