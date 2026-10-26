using Mordor.Core;

/// <summary>
/// Which monsters can actually be drawn, and where from. A monster is only eligible where its
/// LevelFound falls inside the encounter window, and the window's target is the floor you are on -
/// or one deeper on a Stud square (MODULE4.BAS:1426-1495). The dungeon is fifteen floors, so the
/// deepest monsters in MDATA5 are only reachable from a Stud on the last one, and only if the
/// area's spawn mask carries their class.
/// </summary>
public static class ShadowProbe
{
    public static void Run(string dir)
    {
        var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
        Console.WriteLine($"the dungeon is {dungeon.Count} floors deep");
        Console.WriteLine($"monsters with a LevelFound past that: "
            + string.Join(", ", ms.Where(m => m.Id != 0 && m.LevelFound > dungeon.Count)
                                  .Select(m => $"{m.Name.Trim()} ({m.LevelFound})")));

        Console.WriteLine($"\n{"monster",-18} {"floor",5} {"kind",-10} {"plain squares",13} {"stud squares",12}");
        foreach (string name in new[] { "Shadow Ninja", "Asmodeus", "Reaper of Souls", "Minion of Death", "Vampire", "Kobold" })
        {
            var m = ms.FirstOrDefault(x => x.Name.Trim() == name);
            if (m == null) continue;
            int plain = 0, stud = 0;
            for (int lv = 1; lv <= dungeon.Count; lv++)
            {
                var f = dungeon[lv - 1];
                for (int y = 0; y < f.Height; y++)
                    for (int x = 0; x < f.Width; x++)
                    {
                        var a = f.AreaAt(x, y);
                        if (a is not Floor.Area ar || ar.SpawnMask == 0) continue;
                        if ((ar.SpawnMask & (1 << (int)m.Kind)) == 0) continue;
                        bool onStud = f.Has(x, y, Floor.STUD);
                        int target = Rules.EncounterFloor(lv, onStud);
                        if (m.LevelFound > target) continue;      // never inside the window
                        if (onStud) stud++; else plain++;
                    }
            }
            Console.WriteLine($"{m.Name.Trim(),-18} {m.LevelFound,5} {m.Kind,-10} {plain,13} {stud,12}");
        }
    }
}
