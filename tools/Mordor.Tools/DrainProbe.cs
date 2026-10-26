using Mordor.Core;

/// <summary>
/// Which stats a drain attack can take. sub0E85 draws with <c>Int(Rnd * 6 + 1)</c> - the +1 inside
/// the Int, so 1 to 6 and never 0 - and the Select Case reads 0..5 as stats and 6 as "two off max
/// hits". Strength is index 0, so a wraith can drain everything about you except the one stat that
/// makes you hit harder.
///
/// Unlike the acid draw this is visible from outside: nothing else in a monster's turn moves a
/// stat, so whatever drops was drained.
/// </summary>
public static class DrainProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));

        var drainer = mons.FirstOrDefault(m => m.Id != 0 && (m.SpecialAttackFlags & (1 << 7)) != 0 && m.LevelFound >= 8);
        if (drainer == null) { Console.WriteLine("no drainer found"); return; }
        Console.WriteLine($"drained by a {drainer.Name.Trim()} (floor {drainer.LevelFound})\n");

        var lost = new int[6];
        int hitsLost = 0;
        const int rounds = 3000;
        for (int i = 0; i < rounds; i++)
        {
            var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 17, 18, 18, 17, 18, 18 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 1 });
            c.MaxHits = 100000; c.CurHits = 100000;
            var before = c.Stats.ToArray();
            int maxBefore = c.MaxHits;

            var foe = new CombatMonster { Def = drainer, Hits = 100000 };
            var fight = new CombatEngine(c, new[] { foe }, id => items.FirstOrDefault(x => x.Id == id), new Random(7000 + i));
            fight.UseMonsterTable(mons);
            foreach (var _ in fight.MonsterAttacks()) { }

            for (int s = 0; s < 6; s++) if (c.Stats[s] < before[s]) lost[s]++;
            if (c.MaxHits < maxBefore) hitsLost++;
        }

        for (int s = 0; s < 6; s++)
            Console.WriteLine($"  {RaceData.StatNamesLong[s],-14} {lost[s],5}{(s == 0 ? "   <- never: the draw is 1 to 6, and 0 is Strength" : "")}");
        Console.WriteLine($"  {"max hits",-14} {hitsLost,5}   <- the Case 6 the draw can reach");
        Console.WriteLine($"\n  (over {rounds} rounds)");
    }
}
