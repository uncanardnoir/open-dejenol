using Mordor.Core;

/// <summary>
/// The Misc tab's spell page, <c>sub1572</c> case 9. Prints a handful of spells of different
/// classes so the Damage/Heal wording, the resistance line, the mass-spell line and the Special
/// line can all be read at once.
/// </summary>
public static class SpellInfoProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR")).Where(s => s.Name.Trim() != "").ToList();

        var c = new Character { Name = "Prober", Race = "Human" };
        for (int i = 0; i < 6; i++) { c.Stats[i] = 18; c.StatMax[i] = 18; }
        foreach (var g in new[] { "Sorcerer", "Healer", "Warrior" })
            if (GuildData.IndexOf(g) >= 0) c.Guilds.Add(new GuildMembership { Guild = g, Level = 12 });
        if (c.Guilds.Count == 0) c.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 12 });
        c.RecalcSpellPoints(true);

        Console.WriteLine("effects seen in MDATA2, and what the Special line calls each:\n");
        foreach (var g in spells.GroupBy(s => s.Effect).OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key,4}  {SpellInfo.Special(g.First()),-45}  ({g.Count()} spells, e.g. {g.First().Name.Trim()})");

        Console.WriteLine();
        foreach (var s in Picks(spells))
        {
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(CharStatFile.Compose(SpellInfo.Paint(s, c)));
            Console.WriteLine();
        }
    }

    /// <summary>One of each interesting shape: a damage spell, a heal, a mass spell, a resist.</summary>
    private static IEnumerable<Spell> Picks(List<Spell> all)
    {
        Spell? First(Func<Spell, bool> f) => all.FirstOrDefault(f);
        var picks = new List<Spell?>
        {
            First(s => s.Damage2 > 0 && s.Category != 9),
            First(s => s.Category == 9 && s.Damage2 > 0),
            First(s => s.Effect >= SpellInfo.MassOffset),
            First(s => s.Effect is >= SpellInfo.ResistBase and < SpellInfo.ResistBase + 11),
            First(s => s.ResistIndex > 0),
            First(s => s.Effect is >= SpellInfo.OnlyAffectsBase and <= 99),
        };
        return picks.Where(s => s != null)!.Distinct()!;
    }
}
