using Mordor.Core;

/// <summary>
/// Cross-checks MDATA2 and MDATA1 against the header the help prints above every spell:
///
///     Mental Resistance
///     Base Level: 6  Guilds: Sor, Wiz (18)
///     Required Stats: 18 Int, 14 Wis, 12 Con, 10 Cha
///     NON-COMBAT Spell, will raise the recipient's Mental Resistance to 95%.
///
/// Three separate claims sit in those two lines and none is stored where the others are:
///
///   Base Level      MDATA2's Level field.
///   Required Stats  MDATA2's Required[] array, in STR INT WIS CON CHA DEX order.
///   Guilds          not stored per spell at all. A guild reaches a spell when its MDATA1 entry
///                   allows that spell's category up to that spell's level. The number in brackets
///                   is not a guild level: the help says "the level at which the spell is learned
///                   at by a particular guild is listed after the guild name", and that is
///                   Int(Level * rate) from the rate table - FRMCHAR3.BAS:110, and
///                   <see cref="GuildData.ShownSpellLevel"/> here. Where the help prints no
///                   number the spell is learned at its own base level, i.e. rate 1.
///
/// The help was typed by a person and the tables were built by the program, so each agreement is
/// worth something. <see cref="CastGateProbe"/> does the same for the COMBAT / NON-COMBAT line.
/// </summary>
public static class SpellHelpProbe
{
    public static void Run(string dir)
    {
        var r = Compare(dir, verbose: true);
        Console.WriteLine($"\nbase level:                {r.LevelOk,4} agree, {r.LevelBad} disagree");
        Console.WriteLine($"required stats:            {r.StatsOk,4} agree, {r.StatsBad} disagree");
        Console.WriteLine($"guild lists:               {r.GuildOk,4} agree, {r.GuildBad} disagree");
        Console.WriteLine($"spell level in brackets:   {r.WhenOk,4} agree, {r.WhenBad} disagree");
    }

    public sealed record Tally(int LevelOk, int LevelBad, int StatsOk, int StatsBad,
                               int GuildOk, int GuildBad, int WhenOk, int WhenBad);

    public static Tally Compare(string dir, bool verbose)
    {
        Mdata1.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));

        int levelOk = 0, levelBad = 0, statsOk = 0, statsBad = 0;
        int guildOk = 0, guildBad = 0, whenOk = 0, whenBad = 0;

        var topics = HelpText.Topics(dir);   // the game folder's MORDOR.HLP
        if (topics.Count == 0)
        {
            Console.WriteLine("no help (MORDOR.HLP)");
            return new Tally(0, 0, 0, 0, 0, 0, 0, 0);
        }

        // Only guilds that hold a spell table are candidates; a guild whose MDATA1 row never loaded
        // would otherwise look like one that simply casts nothing.
        var casters = GuildData.All.Where(g => g.HasSpellTable).ToList();
        var seen = new HashSet<string>();

        foreach (var topic in topics)
        {
            var lines = HelpText.Lines(topic);

            for (int i = 0; i + 1 < lines.Length; i++)
            {
                string heading = lines[i].Trim();
                if (heading.Length == 0) continue;
                var s = spells.FirstOrDefault(sp => sp.Name.Trim() == heading);
                if (s == null || seen.Contains(heading)) continue;

                // The header must be the very next line. Requiring that is what keeps a spell named
                // inside another entry's prose from picking up that entry's numbers.
                var hdr = System.Text.RegularExpressions.Regex.Match(
                    lines[i + 1].Trim(), @"^Base Level:\s*(\d+)\s+Guilds:\s*(.+?)\s*$");
                if (!hdr.Success) continue;
                seen.Add(heading);

                // ---- Base Level ----
                int baseLevel = int.Parse(hdr.Groups[1].Value);
                if (baseLevel == s.Level) levelOk++;
                else
                {
                    levelBad++;
                    if (verbose) Console.WriteLine($"  LEVEL     {heading,-24} MDATA2 {s.Level}, help {baseLevel}");
                }

                // ---- Required Stats ----
                if (i + 2 < lines.Length && lines[i + 2].TrimStart().StartsWith("Required Stats:", StringComparison.Ordinal))
                {
                    var want = new int[6];
                    foreach (System.Text.RegularExpressions.Match m in
                             System.Text.RegularExpressions.Regex.Matches(lines[i + 2], @"(\d+)\s+([A-Za-z]{3})"))
                    {
                        int at = Array.FindIndex(RaceData.StatNames,
                                                 n => n.Equals(m.Groups[2].Value, StringComparison.OrdinalIgnoreCase));
                        if (at >= 0) want[at] = int.Parse(m.Groups[1].Value);
                    }
                    var got = Enumerable.Range(0, 6).Select(k => (int)s.Required[k]).ToArray();
                    if (got.SequenceEqual(want)) statsOk++;
                    else
                    {
                        statsBad++;
                        if (verbose)
                            Console.WriteLine($"  STATS     {heading,-24} MDATA2 [{string.Join(",", got)}], help [{string.Join(",", want)}]");
                    }
                }

                // ---- Guilds, and the spell level in brackets ----
                // "See (6), Mag (4), Sor, Wiz (8)" - the bracket is optional and belongs to the
                // guild it follows.
                var helpGuilds = new Dictionary<string, int>();   // guild name -> level, 0 = unstated
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(hdr.Groups[2].Value, @"([A-Za-z]{3})\s*(?:\((\d+)\))?"))
                {
                    var g = casters.FirstOrDefault(x => x.Name.StartsWith(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
                    if (g != null) helpGuilds[g.Name] = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                }
                if (helpGuilds.Count == 0) continue;

                var ours = casters.Where(g => g.SpellMax[s.Category] >= s.Level).Select(g => g.Name).ToHashSet();
                if (ours.SetEquals(helpGuilds.Keys)) guildOk++;
                else
                {
                    guildBad++;
                    if (verbose)
                        Console.WriteLine($"  GUILDS    {heading,-24} cat {s.Category,2} lvl {s.Level,2}   "
                                        + $"MDATA1 [{string.Join(",", ours.OrderBy(x => x))}], help [{string.Join(",", helpGuilds.Keys.OrderBy(x => x))}]");
                }

                foreach (var (name, want) in helpGuilds)
                {
                    var g = casters.First(x => x.Name == name);
                    if (g.SpellMax[s.Category] < s.Level) continue;   // already reported above
                    float rate = g.SpellRate[s.Category] <= 0 ? 1 : g.SpellRate[s.Category];
                    int at = g.ShownSpellLevel(s.Category, s.Level);
                    // No bracket in the help means the spell is learned at its own base level.
                    int expect = want == 0 ? s.Level : want;
                    if (at == expect) whenOk++;
                    else
                    {
                        whenBad++;
                        if (verbose)
                            Console.WriteLine($"  WHEN      {heading,-20} {name,-10} rate {rate:0.###}  "
                                            + $"we say spell level {at}, help says {(want == 0 ? "its base level " + s.Level : want.ToString())}");
                    }
                }
            }
        }

        Console.WriteLine($"read the header of {seen.Count} spell entries\n");
        return new Tally(levelOk, levelBad, statsOk, statsBad, guildOk, guildBad, whenOk, whenBad);
    }
}
