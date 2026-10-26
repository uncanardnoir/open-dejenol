using Mordor.Core;

/// <summary>
/// Checks hostility and the offer to join (MONSMOD.BAS:1708-1719 and 1943-1962): how often an
/// encounter turns out friendly, how often one of them asks to come along, and what accepting does.
/// </summary>
public static class JoinProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var dungeon = MdrLoader.LoadDungeon(Path.Combine(dir, "MDATA11.MDR"));

        Character Make(Alignment align, int cha, int level)
        {
            var c = Character.Create("Charmer", RaceData.All[0], align, Sex.Female,
                                     new[] { 12, 12, 12, 12, cha, 12 });
            c.Guilds.Insert(0, new GuildMembership { Guild = "Nomad", Level = level });
            return c;
        }

        Console.WriteLine("how an encounter turns out, by your alignment (2,000 level-1 rooms each):");
        Console.WriteLine($"  {"you are",-9} {"friendly",9} {"offered a join",15}");
        foreach (var align in new[] { Alignment.Good, Alignment.Neutral, Alignment.Evil })
        {
            int friendly = 0, offers = 0, total = 0;
            var rng = new Random(11);
            for (int i = 0; i < 2000; i++)
            {
                var who = Make(align, 18, 5);
                var g = EncounterGenerator.Generate(monsters, 1, rng, dungeon[0], 10, 10, who);
                if (g.Count == 0) continue;
                total++;
                if (!g[0].Hostile) friendly++;
                if (g[0].JoinOffer > 0) offers++;
            }
            Console.WriteLine($"  {align,-9} {friendly * 100.0 / Math.Max(1, total),8:0.0}% {offers * 100.0 / Math.Max(1, total),14:0.0}%");
        }

        Console.WriteLine("\nhow much your charisma matters (neutral character, level 5, 2,000 rooms):");
        Console.Write("  ");
        foreach (int cha in new[] { 6, 10, 14, 18, 22, 25 })
        {
            int offers = 0, total = 0;
            var rng = new Random(23);
            for (int i = 0; i < 2000; i++)
            {
                var who = Make(Alignment.Neutral, cha, 5);
                var g = EncounterGenerator.Generate(monsters, 1, rng, dungeon[0], 10, 10, who);
                if (g.Count == 0) continue;
                total++;
                if (g[0].JoinOffer > 0) offers++;
            }
            Console.Write($"CHA {cha}: {offers * 100.0 / Math.Max(1, total),4:0.0}%   ");
        }
        Console.WriteLine();

        Console.WriteLine("\nand what accepting one does:");
        var rng2 = new Random(5);
        for (int i = 0; i < 40000; i++)
        {
            var who = Make(Alignment.Neutral, 25, 40);
            var g = EncounterGenerator.Generate(monsters, 1, rng2, dungeon[0], 10, 10, who);
            if (g.Count == 0 || g[0].JoinOffer == 0) continue;
            var def = g[0].Def;
            Console.WriteLine($"  a group of {g.Count} {def.Name}, {g[0].JoinOffer} of them offering");
            string said = EncounterGenerator.AcceptJoin(who, g, _ => null);
            Console.WriteLine($"  -> {said}");
            Console.WriteLine($"     party now: {string.Join(", ", who.Companions.All.Select(c => $"{c.Name} {c.Hits}/{c.MaxHits} bound {c.BindingLevel}"))}");
            Console.WriteLine($"     {g.Count} left in the room");

            // and with no room for them
            var full = Make(Alignment.Neutral, 25, 40);
            for (int k = 0; k < full.MaxCompanions; k++)
                full.Companions.Add(new Companion { MonsterId = 1, Name = "Filler", Hits = 1, MaxHits = 1 });
            var g2 = new List<CombatMonster> { new() { Def = def, Hits = 10, JoinOffer = 2 } };
            Console.WriteLine($"  with every slot full: {EncounterGenerator.AcceptJoin(full, g2, _ => null)}");
            break;
        }
    }
}
