using Mordor.Core;

/// <summary>
/// The companion page of the Misc tab, <c>sub1572</c> case 8, and the kick-out roll,
/// <c>fn0B6C</c>. The page is worth printing at each ID grade because the grades run backwards -
/// zero is knowing everything - and the kick-out roll is worth counting because its three chances
/// read off the creature's alignment, not yours.
/// </summary>
public static class CompanionPageProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();

        var owner = new Character { Name = "Prober", Race = "Human", Alignment = Alignment.Good };
        for (int i = 0; i < 6; i++) { owner.Stats[i] = 15; owner.StatMax[i] = 15; }
        owner.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 10 });

        var def = monsters.First(m => m.SpecialAttackFlags != 0 && m.Resistances.Any(r => r > 0));
        Console.WriteLine($"the same companion at each of the four grades - {def.Name.Trim()}:\n");
        foreach (int grade in new[] { 0, 1, 2, 3 })
        {
            var c = new Companion
            {
                MonsterId = def.Id, Name = grade == 0 ? "Scrap" : "", Hits = def.Hits / 2,
                MaxHits = def.Hits, BindingLevel = 6, IdLevel = grade,
                Alignment = ItemAlignment.Neutral,
            };
            Console.WriteLine($"--- ID level {grade}: {CompanionInfo.IdLevelText(grade)}");
            Console.WriteLine(CharStatFile.Compose(CompanionInfo.Paint(c, 0, def, owner, _ => null)));
            Console.WriteLine();
        }

        Console.WriteLine("how often a bound companion turns when you kick it out, by its alignment");
        Console.WriteLine("(the master here is Good, so two steps away is certain):\n");
        var rng = new Random(1);
        foreach (var align in new[] { ItemAlignment.Good, ItemAlignment.Neutral, ItemAlignment.Evil })
        {
            int turned = 0;
            const int tries = 20000;
            for (int n = 0; n < tries; n++)
            {
                var who = new Character { Name = "Prober", Alignment = Alignment.Good };
                who.Companions.Add(new Companion { MonsterId = def.Id, Hits = 5, MaxHits = 10, BindingLevel = 6, Alignment = align });
                var o = CompanionTurn.Check(who, 0, forced: false, rng, _ => def, null, isLeader: true, groupsPresent: 0);
                if (o.Turned) turned++;
            }
            Console.WriteLine($"  {align,-9} {turned * 100.0 / tries,6:0.0}%");
        }

        Console.WriteLine();
        Console.WriteLine("a companion that joined of its own free will (binding at or below zero) never turns");
        Console.WriteLine("unless it is attacked outright:");
        var free = new Character { Name = "Prober", Alignment = Alignment.Good };
        free.Companions.Add(new Companion { MonsterId = def.Id, Hits = 5, MaxHits = 10, BindingLevel = -6, Alignment = ItemAlignment.Evil });
        var kicked = CompanionTurn.Check(free, 0, forced: false, rng, _ => def, null, isLeader: true, groupsPresent: 0);
        var attacked = CompanionTurn.Check(free, 0, forced: true, rng, _ => def, null, isLeader: true, groupsPresent: 0);
        Console.WriteLine($"  kicked out : {(kicked.Turned ? "turns" : "leaves quietly")}");
        Console.WriteLine($"  attacked   : {(attacked.Turned ? "turns - " + attacked.Message : "nothing")}");

        Console.WriteLine();
        Console.WriteLine("with all four group slots already taken, nothing can turn:");
        var full = new Character { Name = "Prober", Alignment = Alignment.Good };
        full.Companions.Add(new Companion { MonsterId = def.Id, Hits = 5, MaxHits = 10, BindingLevel = 6, Alignment = ItemAlignment.Evil });
        var nowhere = CompanionTurn.Check(full, 0, forced: true, rng, _ => def, null, isLeader: true,
                                          groupsPresent: CompanionTurn.GroupSlots);
        Console.WriteLine($"  attacked   : {(nowhere.Turned ? "turns" : "nothing happens")}");
        Console.WriteLine();
        Console.WriteLine("sub0C00: how long a companion stays, in rounds (one a second)");
        Console.WriteLine("a round is a roll against Rnd*200, so these are seconds of game time:\n");
        Console.WriteLine("  binding  drift   rounds to leave (median of 400 runs)");
        foreach (int binding in new[] { -10, -4, -1, 2, 6, 10, 12 })
        {
            var lives = new List<int>();
            for (int run = 0; run < 400; run++)
            {
                var who = new Character { Name = "Prober", Alignment = Alignment.Neutral };
                for (int i = 0; i < 6; i++) { who.Stats[i] = 15; who.StatMax[i] = 15; }
                who.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 10 });
                who.Companions.Add(new Companion
                {
                    MonsterId = def.Id, Hits = def.Hits, MaxHits = def.Hits, BindingLevel = binding,
                });
                int rounds = 0;
                while (who.Companions.OccupiedCount > 0 && rounds < 20000)
                {
                    rounds++;
                    var step = CompanionMood.Round(who, 0, def, rng, isLeader: true);
                    if (step.Leaving) break;
                }
                lives.Add(rounds);
            }
            lives.Sort();
            var sample = new Companion { MonsterId = def.Id, Hits = def.Hits, MaxHits = def.Hits, BindingLevel = binding };
            var master = new Character { Name = "Prober" };
            master.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 10 });
            Console.WriteLine($"  {binding,7}  {CompanionMood.Drift(master, sample, def),5:0.0}   {lives[lives.Count / 2],6}");
        }
        Console.WriteLine();
        Console.WriteLine("a companion bound for life never drifts and never leaves:");
        var forlife = new Character { Name = "Prober" };
        forlife.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 10 });
        forlife.Companions.Add(new Companion
        {
            MonsterId = def.Id, Hits = def.Hits, MaxHits = def.Hits, BindingLevel = Binding.ForLife,
        });
        int stayed = 0;
        for (int i = 0; i < 20000; i++) if (!CompanionMood.Round(forlife, 0, def, rng, true).Leaving) stayed++;
        Console.WriteLine($"  {stayed} of 20000 rounds with no change, binding still {forlife.Companions[0]!.BindingLevel}");
    }
}
