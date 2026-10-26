using Mordor.Core;

/// <summary>
/// Checks the Guild tab's ability ratings against the exact <c>fn123F</c> (MODULE6.BAS:1982),
/// including the one number a screenshot of the running game pins down: a level-1 Nomad with
/// DEX 11 and WIS 10 is rated 3 at thieving.
/// </summary>
public static class RatingProbe
{
    static readonly string[] Abilities = { "thieving", "backstab", "critical", "swings", "fighting", "perception" };

    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);

        Console.WriteLine("the floor and ceiling each rating is placed between:");
        for (int a = 0; a < Abilities.Length; a++)
            Console.WriteLine($"  {a} {Abilities[a],-11} {Rules.AbilityFloor(a),10:0.00} .. {Rules.AbilityCeiling(a),12:0.00}");

        Character Make(string guild, int level, int dex, int wis)
        {
            var c = Character.Create("Rated", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, wis, 12, 12, dex });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
            return c;
        }

        Console.WriteLine("\nthe screenshot: a level-1 Nomad with DEX 11 and WIS 10");
        var nomad = Make("Nomad", 1, 11, 10);
        int thieving = Rules.AbilityRatingExact(nomad, 0);
        Console.WriteLine($"  thieving rating {thieving}   (the running game shows 3) -> " +
                          (thieving == 3 ? "MATCH" : "MISMATCH"));
        Console.WriteLine($"  raw value {Rules.BestAbilityValue(nomad, 0):0.000}, " +
                          $"floor {Rules.AbilityFloor(0):0.000}, ceiling {Rules.AbilityCeiling(0):0.000}");

        Console.WriteLine("\nratings across guilds and levels:");
        Console.Write($"  {"guild",-10} {"lvl",4}");
        foreach (var n in Abilities) Console.Write($" {n,10}");
        Console.WriteLine();
        foreach (string g in new[] { "Nomad", "Warrior", "Thief", "Ninja", "Seeker", "Wizard" })
            foreach (int lvl in new[] { 1, 10, 50 })
            {
                var c = Make(g, lvl, 14, 14);
                Console.Write($"  {g,-10} {lvl,4}");
                for (int a = 0; a < Abilities.Length; a++)
                    Console.Write($" {Rules.AbilityRatingExact(c, a),10}");
                Console.WriteLine();
            }

        Console.WriteLine("\nhighest reachable: the help says only the Warrior reaches 100 fighting ability.");
        foreach (string g in GuildData.All.Select(x => x.Name))
        {
            var c = Make(g, 999, 30, 30);
            Console.WriteLine($"  {g,-10} at level 999: fighting {Rules.AbilityRatingExact(c, 4),3}, " +
                              $"thieving {Rules.AbilityRatingExact(c, 0),3}, perception {Rules.AbilityRatingExact(c, 5),3}");
        }
    }
}
