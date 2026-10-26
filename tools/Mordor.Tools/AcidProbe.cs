using Mordor.Core;

/// <summary>
/// Who an acid attack actually lands on. The draw is Int(Rnd * 5) - 0 to 4, where 0 means the
/// character and 1 to 4 are read back as companion slots (MONSMOD.BAS:2769-2778). Two faults fall
/// out: companion slot 0 can never be hit, because the value that would pick it already means the
/// character; and slot 4 is a slot no loop in the game ever fills, so a fifth of the attacks land
/// on a companion who is not there.
/// </summary>
public static class AcidProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));

        var spitter = mons.FirstOrDefault(m => m.Id != 0 && (m.SpecialAttackFlags & (1 << 5)) != 0);
        if (spitter == null) { Console.WriteLine("no monster in MDATA5 spits acid"); return; }
        var filler = mons.First(m => m.Id != 0 && m.Hits > 0 && m.LevelFound == 1);
        Console.WriteLine($"acid from a {spitter.Name.Trim()}, against a party of one with four companions\n");

        var hits = new int[5];      // index 0 the character, 1..4 the companion slots
        int nobody = 0;
        const int rounds = 4000;
        for (int i = 0; i < rounds; i++)
        {
            var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 20, 20, 20, 20, 20, 20 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 1 });
            c.MaxHits = 100000; c.CurHits = 100000;
            c.Companions.ReleaseAll();
            for (int s = 0; s < 4; s++)
                c.Companions.Add(new Companion { MonsterId = filler.Id, Hits = 100000, MaxHits = 100000, Name = $"C{s}" });

            var foe = new CombatMonster { Def = spitter, Hits = 100000 };
            var fight = new CombatEngine(c, new[] { foe }, id => items.FirstOrDefault(x => x.Id == id), new Random(9000 + i));
            fight.UseMonsterTable(mons);
            foreach (var _ in fight.MonsterAttacks()) { }

            bool any = false;
            if (c.CurHits < 100000) { hits[0]++; any = true; }
            for (int s = 0; s < 4; s++)
                if (c.Companions[s]!.Hits < 100000) { hits[s + 1]++; any = true; }
            if (!any) nobody++;
        }

        Console.WriteLine($"  the character          {hits[0],5}");
        for (int s = 0; s < 4; s++)
            Console.WriteLine($"  companion slot {s}       {hits[s + 1],5}{(s == 0 ? "   <- never, the draw that would pick it means the character" : "")}");
        Console.WriteLine($"  nobody at all          {nobody,5}   <- includes the phantom fifth slot, and rounds where the roll missed");
        Console.WriteLine($"\n  (over {rounds} rounds)");
    }
}
