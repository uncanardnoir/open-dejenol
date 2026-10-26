using Mordor.Core;

/// <summary>
/// Why a caster does or does not know a spell: the guild spell table gates every school by a
/// maximum spell level and a rate, and Spellbook.Known then filters by what the port can actually
/// cast. Prints both, so a spell that never shows up in the game can be traced to whichever gate
/// dropped it.
/// </summary>
public static class SpellGateProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));

        string[] schools =
        {
            "Fire", "Cold", "Electric", "Light", "Wounds", "Poison", "Death", "Charm",
            "Control", "Healing", "Movement", "Banish", "Dispel", "Resist", "Sight", "Opening",
        };

        Console.WriteLine("guild spell tables - max spell level per school (rate in brackets):\n");
        foreach (var g in GuildData.All.Where(g => g.SpellMax.Any(v => v > 0)))
        {
            Console.WriteLine($"  {g.Name}");
            for (int c = 0; c < schools.Length && c < g.SpellMax.Length; c++)
                if (g.SpellMax[c] > 0)
                    Console.WriteLine($"      {schools[c],-10} up to level {g.SpellMax[c],2}  (rate {g.SpellRate[c]:0.##})");
        }

        // and what a deep caster of each guild actually ends up knowing
        foreach (string guild in new[] { "Wizard", "Sorcerer", "Mage", "Healer" })
        {
            if (GuildData.All.All(g => g.Name != guild)) continue;
            var c = Character.Create("Probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 21, 21, 21, 21, 21, 21 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = 30 });
            var known = Spellbook.Known(c, spells);
            Console.WriteLine($"\n{guild} at level 30 knows {known.Count} spells; the ones that are not damage or healing:");
            foreach (var s in known.Where(Spellbook.IsUtility))
                Console.WriteLine($"    effect {SpellEffect.Of(s),3}  {s.Name}");
            foreach (string want in new[] { "Teleport", "Displacement", "Raise Dead", "Resurrect", "Sanctuary" })
            {
                var sp = spells.FirstOrDefault(s => s.Name.Trim() == want);
                if (sp == null) continue;
                var def = GuildData.ByName(guild);
                bool gate = def.CanCast(sp.Category, sp.Level, 30);
                bool inList = known.Any(k => k.Id == sp.Id);
                if (!inList)
                    Console.WriteLine($"    MISSING {want}: school {schools[sp.Category]} level {sp.Level}, " +
                                      $"guild allows up to {def.SpellMax[sp.Category]} -> CanCast {gate}");
            }
        }
    }
}
