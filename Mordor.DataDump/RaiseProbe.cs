using Mordor.Core;

/// <summary>
/// Checks who can be brought back and by what (<c>fn142D</c>, MODULE6.BAS:2733): which raise
/// spell each kind of death yields to, and the chance once it is allowed.
/// </summary>
public static class RaiseProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));

        var raises = spells.Where(s => s.Id != 0 && SpellEffect.IsRaise(s.Effect))
                           .GroupBy(s => s.Effect).OrderBy(g => g.Key).ToList();
        Console.WriteLine("the raise spells, by effect:");
        foreach (var g in raises)
            Console.WriteLine($"  effect {g.Key}: {string.Join(", ", g.Select(s => s.Name).Distinct())}" +
                              (SpellEffect.RaisesToFull(g.Key) ? "   (returns full hits)" : ""));

        var deaths = new[]
        {
            (Rules.DeathOrdinary, "an ordinary death"),
            (Rules.DeathStoned,   "turned to stone"),
            (Rules.DeathRock,     "pulled into solid rock"),
        };

        Console.WriteLine("\nwhat each death yields to (- means refused):");
        Console.WriteLine($"  {"death",-24} {"morgue",-42} " + string.Join(" ", raises.Select(g => $"eff{g.Key,-3}")));
        foreach (var (cause, name) in deaths)
        {
            string morgue = Rules.RaiseRefusal(cause, -1) ?? "allowed";
            Console.Write($"  {name,-24} {morgue,-42} ");
            foreach (var g in raises)
                Console.Write($"{(Rules.RaiseRefusal(cause, g.Key) == null ? "yes" : "-"),-6} ");
            Console.WriteLine();
        }

        Console.WriteLine("\nthe chance, once it is allowed (35 * CON/4.5 + tier*10, clamped 10..98):");
        Console.WriteLine($"  {"CON",4} " + string.Join(" ", raises.Select(g => $"eff{g.Key,-3}")) + "  morgue");
        foreach (int con in new[] { 3, 8, 12, 16, 21, 25 })
        {
            Console.Write($"  {con,4} ");
            foreach (var g in raises)
                Console.Write($"{Rules.SpellRaiseChance(con, g.Key, Rules.DeathOrdinary),-6} ");
            Console.WriteLine($" {Rules.MorgueRaiseChance(con)}");
        }

        Console.WriteLine("\npulled into rock overrides the lot - a flat 10 whatever the CON:");
        Console.Write("  ");
        foreach (int con in new[] { 3, 12, 25 })
            Console.Write($"CON {con}: {Rules.SpellRaiseChance(con, 22, Rules.DeathRock)}   ");
        Console.WriteLine();
    }
}
