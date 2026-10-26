using Mordor.Core;

/// <summary>
/// Rules.GuildAD against every value in the community's A-D_For_all_Guild_Levels tables, split by
/// whether the level is below the guild's HitsCapLevel (where the award interval is the flat
/// Int(AdEvery)) or above it (where fn13D4 stretches the interval).
/// </summary>
public static class ADCheck { public static void Run(string dir) {
  GuildData.LoadFrom(dir);
  Console.WriteLine($"{"guild",-9} {"cap",4} {"every",6}   below the cap      above the cap");
  int lo = 0, loT = 0, hi = 0, hiT = 0;
  var misses = new List<string>();
  foreach (var (guild, table) in Rules.GuildTables)
  {
    var g = GuildData.All.First(x => x.Name == guild);
    int a1 = 0, a1t = 0, a2 = 0, a2t = 0;
    foreach (string entry in table.Split(' '))
    {
      var p = entry.Split(':'); var ad = p[0].Split('/');
      int lvl = int.Parse(p[1]), wantA = int.Parse(ad[0]), wantD = int.Parse(ad[1]);
      var (a, d) = Rules.GuildAD(guild, lvl);
      bool okv = a + Rules.BaseAD == wantA && d + Rules.BaseAD == wantD;
      if (lvl <= g.HitsCapLevel) { a1t++; if (okv) a1++; } else { a2t++; if (okv) a2++; }
      if (!okv) misses.Add($"  {guild,-9} L{lvl,4}  published {wantA,3}/{wantD,-3}  port {a + Rules.BaseAD,3}/{d + Rules.BaseAD,-3}"
                         + $"  off by {a + Rules.BaseAD - wantA,3}/{d + Rules.BaseAD - wantD,-3}");
    }
    lo += a1; loT += a1t; hi += a2; hiT += a2t;
    Console.WriteLine($"{guild,-9} {g.HitsCapLevel,4} {g.AdEvery,6:0.##}   {a1,3} of {a1t,3} exact   {a2,3} of {a2t,3} exact");
  }
  Console.WriteLine($"\nbelow the cap: {lo} of {loT} exact ({100.0 * lo / Math.Max(1, loT):0.#}%)");
  Console.WriteLine($"above the cap: {hi} of {hiT} exact ({100.0 * hi / Math.Max(1, hiT):0.#}%)");
  if (misses.Count > 0)
  {
    Console.WriteLine("\nwhere the port and the published table part company:");
    foreach (string m in misses) Console.WriteLine(m);
  }
} }
