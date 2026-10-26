using Mordor.Core;

/// <summary>
/// Checks mis-identification (MONSMOD.BAS:1880-1942 and FRMDUNM.BAS:475-489): how often a party
/// is given the wrong name for a creature, what it gets called instead, and how long the mistake
/// survives.
/// </summary>
public static class MisidProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var rng = new Random(53);

        Character Make(string guild, int level, int wis, int inte)
        {
            var c = Character.Create("Squinter", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 12, inte, wis, 12, 12, 12 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = level });
            return c;
        }

        Console.WriteLine("how often the name you are given is wrong:");
        Console.WriteLine($"  {"guild",-10} {"lvl",4} {"INT/WIS",8} {"skill",6} | {"vs a Kobold",13} {"vs a lvl-8 foe",15}");
        var shallow = monsters.First(m => m.Id != 0 && m.Name == "Kobold");
        var deep = monsters.First(m => m.Id != 0 && m.LevelFound == 8 && m.GuildLevel > 0);
        foreach (var (guild, level, wis, inte) in new[]
        {
            ("Nomad", 1, 10, 10), ("Nomad", 20, 15, 15), ("Seeker", 1, 18, 18),
            ("Seeker", 20, 18, 18), ("Wizard", 40, 21, 21), ("Wizard", 200, 25, 25),
        })
        {
            var c = Make(guild, level, wis, inte);
            double skill = Rules.IdentifySkill(c.TopGuildLevel, c.Stats[(int)Stat.Int], c.Stats[(int)Stat.Wis]);
            int wrongShallow = 0, wrongDeep = 0;
            for (int i = 0; i < 3000; i++)
            {
                if (EncounterGenerator.BelievedToBe(monsters, shallow, c, rng) != shallow) wrongShallow++;
                if (EncounterGenerator.BelievedToBe(monsters, deep, c, rng) != deep) wrongDeep++;
            }
            Console.WriteLine($"  {guild,-10} {level,4} {inte,3}/{wis,-4} {skill,6:0.0} | {wrongShallow / 30.0,12:0.0}% {wrongDeep / 30.0,14:0.0}%");
        }

        Console.WriteLine($"\n(the deep one is a {deep.Name}, guild level {deep.GuildLevel}, found on floor {deep.LevelFound})");

        Console.WriteLine("\nwhat a weak party actually gets told it is facing:");
        var novice = Make("Nomad", 1, 10, 10);
        foreach (string name in new[] { "Kobold", "Goblin", "Giant Owl" })
        {
            var real = monsters.FirstOrDefault(m => m.Name == name);
            if (real == null) continue;
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < 600; i++)
            {
                string called = EncounterGenerator.BelievedToBe(monsters, real, novice, rng).Name;
                seen[called] = seen.GetValueOrDefault(called) + 1;
            }
            Console.WriteLine($"  a {real.Name} is taken for: "
                + string.Join(", ", seen.OrderByDescending(k => k.Value).Take(6).Select(k => k.Key)));
        }

        Console.WriteLine("\nand how likely you are to put it right, each four-second tick:");
        Console.WriteLine($"  {"skill",6} {"in the open",12} {"in fog",8}");
        foreach (double skill in new double[] { 5, 20, 40, 60, 80 })
            Console.WriteLine($"  {skill,6:0} {Rules.RenameChance(skill, false),11:0.0}% {Rules.RenameChance(skill, true),7:0.0}%");
    }
}
