using Mordor.Core;

/// <summary>
/// Checks the chest panel, <c>monShowChest</c> (MONSMOD.BAS:4124-4176): how often the trap it
/// names is the real one, how sure it claims to be, and what the disarm gauge reads.
/// </summary>
public static class TrapReadoutProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var rng = new Random(17);

        Character Make(string guild, int level, int wis)
        {
            var c = Character.Create("Peeker", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, wis, 12, 12, 12 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = level });
            return c;
        }

        var guard = monsters.First(m => m.Id != 0 && m.LevelFound == 1 && m.GuildLevel > 0);
        Console.WriteLine($"guarded by a {guard.Name} (guild level {guard.GuildLevel}, DEX {(guard.Stats.Length > 5 ? guard.Stats[5] : 0)})\n");

        Console.WriteLine("how often the name on the panel is the real trap, and what it claims:");
        Console.WriteLine($"  {"guild",-10} {"lvl",4} {"WIS",4} {"right",7} {"diamonds",10} {"gauge",7}");
        foreach (var (guild, level, wis) in new[]
        {
            ("Nomad", 1, 10), ("Nomad", 1, 18), ("Nomad", 20, 18),
            ("Thief", 1, 18), ("Thief", 10, 18), ("Thief", 40, 21), ("Thief", 100, 25),
            ("Ninja", 40, 21), ("Warrior", 40, 18),
        })
        {
            int right = 0;
            var diamonds = new List<int>();
            string gauge = "";
            for (int i = 0; i < 4000; i++)
            {
                var c = Make(guild, level, wis);
                var (_, guildLevel) = CombatEngine.WinningGuild(c, 0);
                var (g, d, shown) = Rules.TrapReadout(
                    rng, c.Stats[(int)Stat.Wis], 0, guard.GuildLevel,
                    guard.Stats.Length > 5 ? guard.Stats[5] : 10,
                    Rules.BestAbilityChance(c, 0, 1, guard.GuildLevel), guildLevel, realTrap: 3);
                if (shown == 3) right++;
                diamonds.Add(d);
                gauge = g;
            }
            Console.WriteLine($"  {guild,-10} {level,4} {wis,4} {right / 40.0,6:0.0}% {diamonds.Average(),9:0.0} {gauge,7}");
        }

        Console.WriteLine("\nnote that being sure and being right are not the same thing - the diamonds");
        Console.WriteLine("come from the same figure as the roll, so they move together, but the panel");
        Console.WriteLine("never tells you which of the two happened.");

        Console.WriteLine("\na locked chest shows nothing at all:");
        foreach (int state in new[] { 0, 1, -1 })
            Console.WriteLine($"  lock state {state,2}: " +
                (Rules.MagicallyLocked(state) ? $"\"{Rules.LockedCaption}\"" : "the trap readout"));
    }
}
