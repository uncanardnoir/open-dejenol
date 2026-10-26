using Mordor.Core;

/// <summary>
/// Checks being lost (MODULE3.BAS): how likely a character is to lose their bearings on a
/// rotator square, and how many steps it then takes them to work out where they are.
/// </summary>
public static class LostProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var rng = new Random(1234);

        Character Make(string guild, int level, int wis)
        {
            var c = Character.Create("Walker", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, wis, 12, 12, 12 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = level });
            return c;
        }

        // Seeker is the perception guild; Warrior is the character who never looks up.
        var cases = new[]
        {
            ("Warrior", 1, 10), ("Warrior", 20, 12),
            ("Seeker", 1, 14), ("Seeker", 10, 18), ("Seeker", 30, 21), ("Seeker", 60, 25),
        };

        Console.WriteLine("perception, and the chance a rotator square takes your bearings (level 5):");
        Console.WriteLine($"  {"who",-14} {"perception",10} {"loses way",10}");
        foreach (var (guild, lvl, wis) in cases)
        {
            var c = Make(guild, lvl, wis); c.DungeonLevel = 5;
            double p = Rules.BestAbilityChance(c, Lost.Perception, c.DungeonLevel);
            int lost = 0;
            for (int i = 0; i < 20000; i++)
            {
                c.FoundYourself();
                if (Lost.LoseDirection(c, rng)) lost++;
            }
            Console.WriteLine($"  {guild + " " + lvl,-14} {p,10:0.0} {lost / 200.0,9:0.0}%");
        }

        Console.WriteLine("\nonce completely lost, steps taken to work it out again (level 5, no mapped ground):");
        Console.WriteLine($"  {"who",-14} {"perception",10} {"median",7} {"90th",7} {"worst",7}");
        foreach (var (guild, lvl, wis) in cases)
        {
            var c = Make(guild, lvl, wis); c.DungeonLevel = 5;
            double p = Rules.BestAbilityChance(c, Lost.Perception, c.DungeonLevel);
            var steps = new List<int>();
            for (int trial = 0; trial < 3000; trial++)
            {
                c.BecomeLost();
                int n = 0;
                while (c.IsLost && n < 5000) { Lost.TryToWorkItOut(c, rng); n++; }
                steps.Add(n);
            }
            steps.Sort();
            Console.WriteLine($"  {guild + " " + lvl,-14} {p,10:0.0} {steps[steps.Count / 2],7} " +
                              $"{steps[(int)(steps.Count * 0.9)],7} {steps[^1],7}");
        }

        Console.WriteLine("\nwhat you recover first (3000 trials, Seeker 30):");
        var seeker = Make("Seeker", 30, 21); seeker.DungeonLevel = 5;
        int levelFirst = 0, facingFirst = 0, allAtOnce = 0;
        for (int trial = 0; trial < 3000; trial++)
        {
            seeker.BecomeLost();
            while (seeker.IsLost)
            {
                var said = Lost.TryToWorkItOut(seeker, rng);
                if (said.Count == 0) continue;
                if (said[0].Contains("where you are")) allAtOnce++;
                else if (said[0].Contains("level")) levelFirst++;
                else facingFirst++;
                break;
            }
        }
        Console.WriteLine($"  the floor first: {levelFirst}   the facing first: {facingFirst}   " +
                          $"the whole thing at once: {allAtOnce}");

        Console.WriteLine("\nmessages:");
        var who = Make("Seeker", 10, 18); who.BecomeLost();
        Console.WriteLine("  landing unmapped, all three gone: " + Lost.Realisation(who));
        who.FoundYourself(); who.LostPosition = true;
        Console.WriteLine("  position only:                    " + Lost.Realisation(who));
        Console.WriteLine("  spun round, alone:                " + Lost.DirectionLostText(false));
        Console.WriteLine("  spun round, leading a party:      " + Lost.DirectionLostText(true));
        Console.WriteLine("  stepping onto mapped ground:      " + Lost.OnKnownGround(who));
    }
}
