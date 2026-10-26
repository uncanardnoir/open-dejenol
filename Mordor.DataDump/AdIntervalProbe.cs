using Mordor.Core;

/// <summary>
/// Works out exactly when a guild awards Att/Def above its cap, by running the original's own
/// code rather than trying to fit a formula to the published tables.
///
/// The caller (FRMCTYG.BAS:377-392) is the part I had missed: it keeps <c>M59A1</c> on the
/// character between level-ups, and only refreshes it when it falls behind. So the test at level
/// L is not against a freshly computed interval, it is against the value fn13D4 produced at L-1.
///
/// <code>
///   If level &lt;= AdMaxLevel Then
///       If level &lt;= HitsCapLevel Then l009E = level Mod Int(AdEvery)
///       Else
///           If M59A1 &lt; level Then M59A1 = fn13D4(guild, level)
///           l009E = level Mod M59A1
///       End If
///       If l009E = 0 Then award
///       M59A1 = fn13D4(guild, level)
///   End If
/// </code>
/// </summary>
public static class AdIntervalProbe
{
    /// <summary>fn13D4 (MODULE6.BAS:2675), transcribed line for line.</summary>
    private static int Fn13D4(GuildDef g, int guildIndex, int level)
    {
        float acc;
        if (level > g.HitsCapLevel)
        {
            acc = g.HitsCapLevel;
            float bonus = guildIndex == 0 && level > g.HitsCapLevel ? 3f : 0f;
            for (int i = (int)g.HitsCapLevel; i <= level; i++)
            {
                // l0434 is declared Integer, so the division rounds on assignment
                int step = RoundVb((int)g.HitsCapLevel / 2.0 + acc / 18.0, g.AdEvery + bonus);
                acc += step;
                if (acc > level) break;
            }
            if (acc == g.HitsCapLevel) acc = level + 1;
        }
        else acc = 0;

        if (level > g.AdMaxLevel) acc *= 1.1f;
        return (int)Math.Round(acc, MidpointRounding.ToEven);
    }

    /// <summary><c>((Int(cap) / 2) + (acc / 18)) / (AdEvery + bonus)</c>, landing in an Integer.</summary>
    private static int RoundVb(double numerator, double denominator) =>
        (int)Math.Round(numerator / denominator, MidpointRounding.ToEven);

    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);

        Console.WriteLine($"{"guild",-12} {"cap",4} {"every",6} {"adMax",6}   award levels above the cap");
        for (int gi = 0; gi < GuildData.All.Count; gi++)
        {
            var g = GuildData.All[gi];
            var awards = new List<int>();
            int m59a1 = 0;                                  // the character's stored value

            for (int level = 1; level <= 60; level++)
            {
                if (level <= g.AdMaxLevel)
                {
                    int test;
                    if (level <= g.HitsCapLevel)
                        test = (int)g.AdEvery <= 0 ? 1 : level % (int)g.AdEvery;
                    else
                    {
                        if (m59a1 < level) m59a1 = Fn13D4(g, gi, level);
                        test = m59a1 == 0 ? 1 : level % m59a1;
                    }
                    if (test == 0 && level > g.HitsCapLevel) awards.Add(level);
                    m59a1 = Fn13D4(g, gi, level);
                }
            }

            Console.WriteLine($"{g.Name,-12} {g.HitsCapLevel,4} {g.AdEvery,6:0.##} {g.AdMaxLevel,6}   " +
                              string.Join(" ", awards.Take(14)));
        }

        // Compare against every published table. The wiki pages list "att/def  level" pairs, so
        // the level column is the second number of each pair.
        Console.WriteLine();
        string textDir = System.IO.Path.Combine("docs", "dejenol-formulas", "text");
        if (!System.IO.Directory.Exists(textDir)) { Console.WriteLine($"(no {textDir})"); return; }

        // Only five of the wiki's guild pages hold real data. The index page says so itself:
        // "Currently only Nomad, Warrior, Paladin, Thief and Seeker complete -- all other stats
        // from Nomad guild for template purposes."
        var complete = new HashSet<string> { "Nomad", "Warrior", "Paladin", "Thief", "Seeker" };

        int totalChecked = 0, totalWrong = 0;
        foreach (var g in GuildData.All)
        {
            string file = System.IO.Path.Combine(textDir, $"guild_{g.Name}.txt");
            if (!System.IO.File.Exists(file)) continue;
            string text = System.IO.File.ReadAllText(file);

            // the saved pages are already tag-stripped: "att/def |" then the level on its own line
            var published = System.Text.RegularExpressions.Regex.Matches(text, @"\d+/\d+\s*\|\s*(\d+)")
                .Select(m => int.Parse(m.Groups[1].Value))
                // level 1 is the table's starting row, showing the flat 10/10 everyone begins
                // with - not a guild award, so it is not one of the levels being checked
                .Where(v => v > 1 && v <= 1000)
                .Distinct().OrderBy(v => v).ToList();
            if (published.Count < 10) { Console.WriteLine($"{g.Name,-12} no usable table"); continue; }
            if (!complete.Contains(g.Name))
            {
                Console.WriteLine($"{g.Name,-12} placeholder (the wiki says this page is a copy of Nomad)");
                continue;
            }

            int gi = GuildData.All.ToList().FindIndex(x => x.Name == g.Name);
            var mine = Simulate(g, gi, published.Max());
            var missing = published.Except(mine).ToList();
            var extra = mine.Except(published).ToList();
            totalChecked += published.Count;
            totalWrong += missing.Count + extra.Count;

            Console.WriteLine($"{g.Name,-12} {published.Count,3} published levels up to {published.Max(),3}: " +
                (missing.Count == 0 && extra.Count == 0
                    ? "all reproduce exactly"
                    : $"missing {string.Join(",", missing.Take(8))}  extra {string.Join(",", extra.Take(8))}"));
        }
        Console.WriteLine($"\n{totalChecked - totalWrong} of {totalChecked} published award levels reproduce exactly");
    }

    /// <summary>
    /// Every level at which this guild awards A/D - taken from <see cref="Rules.GuildAD"/>, the
    /// code the game actually uses, rather than a second copy of it here. An award is a level
    /// where the running total goes up.
    /// </summary>
    private static List<int> Simulate(GuildDef g, int guildIndex, int maxLevel)
    {
        var awards = new List<int>();
        int last = 0;
        for (int level = 1; level <= maxLevel; level++)
        {
            var (att, _) = Rules.GuildAD(g.Name, level);
            if (level > 1 && att > last) awards.Add(level);
            last = att;
        }
        return awards;
    }
}
