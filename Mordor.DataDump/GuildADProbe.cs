using Mordor.Core;

/// <summary>
/// The guild A/D rules read straight out of MDATA1 (guild record fields 17-20), checked against
/// the community's published table for the guilds it covers. Every character starts at 10/10
/// (mc00C6 in CHARCREA.BAS) and a guild adds its award only on levels where
/// <c>level Mod Int(AdEvery)</c> is zero.
/// </summary>
public static class GuildADProbe { public static void Run(string dir) {
  GuildData.LoadFrom(dir);
  Console.WriteLine($"{"guild",-11} {"att",5} {"def",5} {"every",6} {"maxLv",6} {"gold",5}   A/D at level 1 / 2 / 3 / 4 / 6 / 10");
  foreach (var g in GuildData.All)
  {
    string proj = string.Join(" / ", new[] { 1, 2, 3, 4, 6, 10 }.Select(l => $"{Ad(g, l).a}/{Ad(g, l).d}"));
    Console.WriteLine($"{g.Name,-11} {g.AttPerLevel,5:0.##} {g.DefPerLevel,5:0.##} {g.AdEvery,6:0.##} {g.AdMaxLevel,6} {g.GoldFactor,5}   {proj}");
  }
  Console.WriteLine();
  Console.WriteLine("published (A-D_For_all_Guild_Levels):");
  Console.WriteLine("  Nomad   10/10 at 1, 13/13 at 2, 16/16 at 4, 19/19 at 6, 25/25 at 10");
  Console.WriteLine("  Warrior 13/13 at 4, 16/17 at 8, 19/20 at 12");
  Console.WriteLine("  Thief   12/12 at 3, 14/15 at 6, 16/18 at 9");
}


  /// <summary>10/10 plus one award per completed interval, which is what FRMCTYG accumulates.</summary>
  static (int a, int d) Ad(GuildDef g, int level)
  {
    int every = Math.Max(1, (int)g.AdEvery);
    int awards = level / every;
    return (10 + (int)(g.AttPerLevel * awards), 10 + (int)(g.DefPerLevel * awards));
  }
}
