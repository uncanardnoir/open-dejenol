using Mordor.Core;
public static class GroupProbe { public static void Run(string dir) {
  var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
  Console.WriteLine("NumGroups (M5A0E) distribution: " + string.Join(", ", ms.GroupBy(m => m.NumGroups).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}")));
  Console.WriteLine("InGroup  (M5A57) distribution: " + string.Join(", ", ms.GroupBy(m => m.InGroup).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}")));
  Console.WriteLine("PicId    distribution (sanity): " + string.Join(", ", ms.GroupBy(m => m.PicId).OrderBy(g => g.Key).Take(8).Select(g => $"{g.Key}x{g.Count()}")) + " ...");
  Console.WriteLine();
  foreach (var m in ms.Where(m => m.LevelFound == 6).Take(6))
    Console.WriteLine($"  {m.Name,-22} numGroups {m.NumGroups,2} inGroup {m.InGroup,2} hits {m.Hits,4} att {m.Att,4} def {m.Def,4} glvl {m.GuildLevel,3}");
} }
