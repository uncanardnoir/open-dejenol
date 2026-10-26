using Mordor.Core;

/// <summary>Checks the Seer (FRMSEEK.BAS): name matching, his fee, his refusals, and what he finds.</summary>
public static class SeerProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var dungeon = MdrLoader.LoadDungeon(Path.Combine(dir, "MDATA11.MDR"));
        var monNames = monsters.Where(m => m.Id != 0).Select(m => m.Name).ToList();
        var itemNames = items.Where(i => i.Id != 0).Select(i => i.Name).ToList();
        var rng = new Random(7);

        Console.WriteLine("how he reads a name you type:");
        foreach (string typed in new[] { "Kobold", "kobold", "Gob", "Z", "", "Bronze Sword" })
        {
            var (found, idx) = Seer.Match(typed, monNames);
            string answer = found switch
            {
                Seer.Found.One => monNames[idx],
                Seer.Found.Several => Seer.SeveralMonsters,
                _ => Seer.NoSuchMonster(typed),
            };
            Console.WriteLine($"  \"{typed,-14}\" -> {answer}");
        }

        Console.WriteLine("\nwhat a search costs, by the depth of what you are after:");
        Console.Write("  ");
        foreach (int lvl in new[] { 1, 2, 4, 6, 8, 10, 12 })
            Console.Write($"L{lvl}: {Seer.Fee(new Random(lvl), lvl),9:N0}   ");
        Console.WriteLine();

        Console.WriteLine("\nwhat he refuses:");
        var deep = monsters.FirstOrDefault(m => m.Id != 0 && m.LevelFound > Seer.DeepestHeCanReach);
        Console.WriteLine($"  a creature from level {deep?.LevelFound}: {Seer.MonsterTooDeep}");
        Console.WriteLine($"  a name he cannot place:     {Seer.NoSuchMonster("Grue")}");
        Console.WriteLine($"  a name shared by several:   {Seer.SeveralMonsters}");

        Console.WriteLine("\nhow often the search itself comes to nothing (1,000 tries):");
        Console.WriteLine($"  {"level",5} {"fails early",12} {"of those, names the level",26}");
        foreach (int lvl in new[] { 1, 4, 8, 12 })
        {
            int early = 0, named = 0;
            for (int i = 0; i < 1000; i++)
                if (Seer.SearchFailsEarly(rng, lvl)) { early++; if (Seer.FailureNamesTheLevel(rng, lvl)) named++; }
            Console.WriteLine($"  {lvl,5} {early / 10.0,11:0.0}% {(early == 0 ? 0 : named * 100.0 / early),25:0.0}%");
        }

        Console.WriteLine("\nwhat he says when he does find something:");
        foreach (string want in new[] { "Kobold", "Giant Owl", "Clean-Up" })
        {
            var (found, idx) = Seer.Match(want, monNames);
            if (found != Seer.Found.One) continue;
            var def = monsters.Where(m => m.Id != 0).ElementAt(idx);
            if (def.LevelFound < 1 || def.LevelFound > dungeon.Count) continue;
            var spot = Seer.Locate(dungeon[def.LevelFound - 1], def, rng);
            if (spot == null) { Console.WriteLine($"  {def.Name}: {Seer.CannotDetect}"); continue; }
            var (x, y, classes, lair) = spot.Value;
            Console.WriteLine($"  {Seer.Sighting(def.Name, x, y, def.LevelFound)}");
            Console.WriteLine($"    {Seer.TypeIsCommon(lair ? Seer.AlwaysSeen : Seer.Commonality(classes))}");
            Console.WriteLine($"    {Seer.SeenAmongItsType(Seer.Frequency(def.Chance))}");
        }

        Console.WriteLine("\nwho might be carrying a thing:");
        foreach (string want in new[] { "Bronze Sword", "Elven Ring", "Vorpal Blade" })
        {
            var (found, idx) = Seer.Match(want, itemNames);
            if (found != Seer.Found.One) { Console.WriteLine($"  {want}: not a single match"); continue; }
            var it = items.Where(i => i.Id != 0).ElementAt(idx);
            if (it.Floor > Seer.DeepestHeCanReach) { Console.WriteLine($"  {it.Name}: {Seer.ItemTooDeep}"); continue; }
            var carriers = Seer.Carriers(monsters, it, rng);
            Console.WriteLine($"  {it.Name} (floor {it.Floor}, {carriers.Count} possible carriers)");
            Console.WriteLine($"    {Seer.Carriers(carriers, rng)}");
        }
    }
}
