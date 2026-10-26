using Mordor.Core;

/// <summary>
/// Checks noticing what square you are on (sub087C, MAZEMOD.BAS:605-712): how often anything
/// registers, how often the name you are given is the right one, and how sure it sounds.
/// </summary>
public static class PerceiveProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var rng = new Random(41);

        Character Make(string guild, int level, int wis)
        {
            var c = Character.Create("Watcher", RaceData.All[0], Alignment.Neutral, Sex.Female,
                                     new[] { 12, 12, wis, 12, 12, 12 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = level });
            return c;
        }

        Console.WriteLine("standing on a rotator, 4,000 times each:");
        Console.WriteLine($"  {"guild",-10} {"lvl",4} {"percep",7} {"notices",8} {"names it right",15} {"says",20}");
        foreach (var (guild, level, wis) in new[]
        {
            ("Nomad", 1, 12), ("Nomad", 20, 18), ("Seeker", 1, 18),
            ("Seeker", 10, 18), ("Seeker", 40, 21), ("Seeker", 200, 25), ("Warrior", 40, 12),
        })
        {
            var c = Make(guild, level, wis);
            double p = Rules.BestAbilityChance(c, 5, 1, 0);
            int noticed = 0, right = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (!Perception.NoticesAnything(rng, p, Floor.ROTATOR)) continue;
                noticed++;
                if (Perception.IdentifiesIt(rng, p, Floor.ROTATOR)) right++;
            }
            string example = Perception.Say(p, false, c.Name, Floor.ROTATOR, Perception.Certainty(p, false));
            Console.WriteLine($"  {guild,-10} {level,4} {p,6:0.0} {noticed / 40.0,7:0.0}% {(noticed == 0 ? 0 : right * 100.0 / noticed),14:0.0}% {Short(example),20}");
        }

        Console.WriteLine("\nthe certainty ladder:");
        foreach (double p in new double[] { 20, 39, 45, 55, 65, 80, 90, 99 })
            Console.WriteLine($"  perception {p,3:0}: \"{Perception.Certainty(p, false)}\"   mapped it: \"{Perception.Certainty(p, true)}\"");

        Console.WriteLine("\nwhat it says at each end:");
        Console.WriteLine($"  alone, low perception:  {Perception.Say(20, false, "Watcher", Floor.ROTATOR, "kind of")}");
        Console.WriteLine($"  alone, high:            {Perception.Say(90, false, "Watcher", Floor.ANTIMAGIC, "almost absolutely")}");
        Console.WriteLine($"  in a party:             {Perception.Say(90, true, "Watcher", Floor.STUD, "very")}");
        Console.WriteLine($"  a teleporter:           {Perception.Say(90, false, "Watcher", Floor.TELEPORTER, "very")}");
        Console.WriteLine($"  hitting one:            {Perception.HitATeleporter(false)}");

        Console.WriteLine("\nwhen it guesses wrong, what can it name? (2,000 draws against a rotator)");
        var wrong = new Dictionary<string, int>();
        for (int i = 0; i < 2000; i++)
        {
            string named = Perception.Names[Perception.WrongGuess(rng, Floor.ROTATOR)];
            wrong[named] = wrong.GetValueOrDefault(named) + 1;
        }
        Console.WriteLine("  " + string.Join(", ", wrong.OrderByDescending(k => k.Value).Select(k => k.Key)));
        Console.WriteLine("  (never a pit, water, stairs, rock, fog, a chute or a teleporter - things you would notice yourself)");
    }

    static string Short(string s) => s.Length <= 20 ? s : s[..17] + "...";
}
