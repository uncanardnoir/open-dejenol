using Mordor.Core;

/// <summary>
/// Every spell effect the shipped data actually uses, against what the port does with it. A spell
/// the dispatcher handles but no record carries is dead code; a record the dispatcher does not
/// handle is a gap. Both are invisible to a check that only reads fields.
/// </summary>
public static class ReachProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));

        Console.WriteLine($"{"effect",6} {"n",3}  {"category",-12} {"example",-24} port");
        foreach (var g in spells.Where(s => s.Name.Trim().Length > 0)
                                .GroupBy(s => SpellEffect.Of(s))
                                .OrderBy(g => g.Key))
        {
            var ex = g.First();
            string handled = Describe(g.Key, ex);
            Console.WriteLine($"{g.Key,6} {g.Count(),3}  {Librarian.SpellSchools[Math.Clamp((int)ex.Category, 0, Librarian.SpellSchools.Length - 1)],-12} {ex.Name.Trim(),-24} {handled}");
        }

        // which effects can a character of some guild and level actually reach?
        Console.WriteLine("\neffects no character can reach at any level, in any guild:");
        var reachable = new HashSet<int>();
        foreach (var g in GuildData.All)
            for (int lvl = 1; lvl <= 999; lvl += 1)
            {
                var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 30, 30, 30, 30, 30, 30 });
                c.Guilds.Clear();
                c.Guilds.Add(new GuildMembership { Guild = g.Name, Level = lvl });
                foreach (var s in Spellbook.Known(c, spells)) reachable.Add(SpellEffect.Of(s));
                if (lvl >= 60) lvl += 39;   // thin out the high end
            }
        int unreachable = 0;
        foreach (var g in spells.Where(s => s.Name.Trim().Length > 0).GroupBy(s => SpellEffect.Of(s)).OrderBy(g => g.Key))
            if (!reachable.Contains(g.Key))
            {
                Console.WriteLine($"  effect {g.Key,3}: {string.Join(", ", g.Select(s => s.Name.Trim()))}");
                unreachable++;
            }
        if (unreachable == 0) Console.WriteLine("  none - every effect in the data is reachable by somebody");
    }

    private static string Describe(int effect, Spell ex)
    {
        if (effect == SpellEffect.Damage) return ex.Damage2 > 0 ? "damage or heal" : "-";
        if (SpellEffect.IsState(effect)) return "a standing state";
        if (SpellEffect.ResistKind(effect) >= 0) return $"resistance {Librarian.Resistances[SpellEffect.ResistKind(effect)]}";
        if (SpellEffect.IsRaise(effect)) return "raise";
        if (SpellEffect.OpensLockUpTo(effect) > 0) return $"opens locks to {SpellEffect.OpensLockUpTo(effect)}";
        if (effect >= SpellEffect.CharmFirst && effect <= SpellEffect.CharmLast) return "charm";
        if (effect >= SpellEffect.BindFirst && effect <= SpellEffect.BindLast) return "bind";
        if (effect == SpellEffect.BindForLife) return "bind for life";
        if (effect >= SpellEffect.Kill) return "kill";
        return effect switch
        {
            SpellEffect.CurePoison => "cure poison", SpellEffect.CureDisease => "cure disease",
            SpellEffect.Teleport => "teleport", SpellEffect.Displacement => "displacement",
            SpellEffect.Passwall => "passwall", SpellEffect.CureParalysis => "cure paralysis",
            SpellEffect.DetectRock => "detect rock", SpellEffect.DepthPerception => "depth",
            SpellEffect.FindDirection => "direction", SpellEffect.SoulSearch => "soul search",
            SpellEffect.RetrieveSoul => "retrieve soul", SpellEffect.SetSanctuary => "set sanctuary",
            SpellEffect.Sanctuary => "sanctuary", SpellEffect.Cure => "cure", SpellEffect.Fate => "fate",
            _ => "NOT HANDLED",
        };
    }
}
