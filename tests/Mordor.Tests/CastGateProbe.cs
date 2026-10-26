using Mordor.Core;

/// <summary>
/// Cross-checks <see cref="CastGate.UseOf"/> against the game's own help, which labels every spell
/// page "COMBAT Spell,", "NON-COMBAT Spell," or - for Teleport alone - "COMBAT &amp; NON-COMBAT
/// Spell,". The classification is computed from MDATA2 (<c>fn149E</c>, MAINMOD2.BAS:2908) and the
/// help was written by hand, so agreement between them is two independent sources saying the same
/// thing.
///
/// The help's third label lines up exactly with the routine's "either", including on Teleport,
/// which is the one spell either source spells out that way.
///
/// Cure Paralysis is the one place the two are not identical: <c>fn149E</c> calls it either (by
/// effect id 10) and the help labels it COMBAT. That is not a contradiction - "either" includes
/// combat - so it is counted apart as the help saying less than the routine does, rather than
/// being scored as an agreement it has not earned.
/// </summary>
public static class CastGateProbe
{
    public static void Run(string dir)
    {
        var r = Compare(dir);
        Console.WriteLine($"\nagree {r.Agree}, disagree {r.Disagree}, help says less than the routine {r.Weaker}");
    }

    /// <summary>The tallies, so selfcheck can assert on them instead of a human reading them.</summary>
    public static (int Agree, int Disagree, int Weaker) Compare(string dir)
    {
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));

        var help = new Dictionary<string, SpellUse>();
        // the help as the game folder's MORDOR.HLP has it (HelpText)
        foreach (var topic in HelpText.Topics(dir))
            {
                // Each spell's entry is laid out the same way, and the shape is what makes the
                // match exact:
                //
                //     Mental Resistance
                //     Base Level: 6  Guilds: Sor, Wiz (18)
                //     Required Stats: 18 Int, 14 Wis, 12 Con, 10 Cha
                //     NON-COMBAT Spell, will raise the recipient's Mental Resistance to 95%.
                //
                // So the name is a heading on a line of its own and the label is a few lines below
                // it. Requiring the name to *be* the line is what keeps a spell mentioned inside
                // somebody else's description - "will raise the recipient's Electrical Resistance
                // to 95%" - from stealing the next entry's label, which is how an earlier version
                // of this reported a mismatch that was not one.
                var lines = HelpText.Lines(topic);
                for (int i = 0; i < lines.Length; i++)
                {
                    string heading = lines[i].Trim();
                    if (heading.Length == 0) continue;
                    var s = spells.FirstOrDefault(sp => sp.Name.Trim() == heading);
                    if (s == null) continue;

                    for (int j = i + 1; j < Math.Min(lines.Length, i + 6); j++)
                    {
                        string line = lines[j].TrimStart();
                        // Longest first: Teleport's label begins with the word COMBAT too, and
                        // testing "COMBAT Spell" before it would leave the entry unlabelled.
                        if (line.StartsWith("COMBAT & NON-COMBAT Spell", StringComparison.Ordinal))
                        { help[heading] = SpellUse.Either; break; }
                        if (line.StartsWith("NON-COMBAT Spell", StringComparison.Ordinal))
                        { help[heading] = SpellUse.NonCombat; break; }
                        if (line.StartsWith("COMBAT Spell", StringComparison.Ordinal))
                        { help[heading] = SpellUse.Combat; break; }
                    }
                }
            }

        Console.WriteLine($"help labels found for {help.Count} spells\n");
        int agree = 0, disagree = 0, weaker = 0;
        foreach (var s in spells.Where(s => s.Name.Trim().Length > 0))
        {
            var use = CastGate.UseOf(s);
            if (!help.TryGetValue(s.Name.Trim(), out SpellUse said)) continue;
            if (use == said) { agree++; continue; }

            if (use == SpellUse.Either)
            {
                weaker++;
                Console.WriteLine($"  weaker    {s.Name,-24} effect {s.Effect,3}"
                                + $"   fn149E says either, the help says {said}");
                continue;
            }

            disagree++;
            Console.WriteLine($"  MISMATCH  {s.Name,-24} school {s.Category,2} dmg2 {s.Damage2,3} effect {s.Effect,3}"
                            + $"   we say {use}, the help says {said}");
        }
        return (agree, disagree, weaker);
    }
}
