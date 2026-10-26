using Mordor.Core;

/// <summary>Dump the dungeon's spawn areas (kinds allowed, lair monster) and the monsters each level can actually spawn.</summary>
public static class AreaProbe
{
    public static void Run(string dir)
    {
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
        string[] kinds = Enum.GetNames(typeof(MonsterKind));

        for (int lv = 1; lv <= 3; lv++)
        {
            var f = dungeon[lv - 1];
            var cellCount = new Dictionary<int, int>();
            for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                    cellCount[f.At(x, y).AreaId] = cellCount.GetValueOrDefault(f.At(x, y).AreaId) + 1;

            var kindsSeen = new HashSet<int>();
            int lairs = 0;
            foreach (var id in cellCount.Keys.Where(k => k >= 1).OrderBy(k => k))
            {
                if (id - 1 >= f.Areas.Count) continue;
                var a = f.Areas[id];
                foreach (int b in Enumerable.Range(0, 18)) if ((a.SpawnMask & (1 << b)) != 0) kindsSeen.Add(b);
                if (a.LairId > 0) lairs++;
            }
            Console.WriteLine($"=== dungeon level {lv}: {cellCount.Count(c => c.Key >= 1)} areas in use, {lairs} lairs");
            Console.WriteLine($"    kinds that can spawn anywhere on this level: {string.Join(", ", kindsSeen.OrderBy(b => b).Select(b => kinds[b]))}");
            foreach (var id in cellCount.Keys.Where(k => k >= 1).OrderBy(k => k))
            {
                if (id - 1 >= f.Areas.Count) continue;
                var a = f.Areas[id];
                if (a.LairId == 0) continue;
                string lair = monsters.FirstOrDefault(m => m.Id == a.LairId)?.Name ?? $"#{a.LairId}";
                var names = Enumerable.Range(0, 18).Where(b => (a.SpawnMask & (1 << b)) != 0).Select(b => kinds[b]);
                Console.WriteLine($"    lair area {id,3} ({cellCount[id],3} cells): {lair,-22} kinds: {string.Join(",", names)}");
            }
        }

        Console.WriteLine("\nLevel-1 monsters in the data (Chance = spawn %):");
        foreach (var m in monsters.Where(m => m.Id != 0 && m.LevelFound == 1).OrderByDescending(m => m.Chance))
            Console.WriteLine($"  {m.Name,-22} kind {m.Kind,-10} att {m.Att,4} hits {m.Hits,4} chance {m.Chance,4} inGroup {m.InGroup}");
    }
}
