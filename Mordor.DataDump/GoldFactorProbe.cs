using Mordor.Core;
public static class GoldFactorProbe { public static void Run(string dir) {
  var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
  Console.WriteLine("GoldFactor distribution: " + string.Join(", ", ms.GroupBy(m => m.GoldFactor).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}")));
  foreach (var m in ms.Where(m => m.LevelFound <= 2).Take(8))
    Console.WriteLine($"  {m.Name,-22} lvl {m.LevelFound}  goldFactor {m.GoldFactor}  inGroup {m.InGroup}  box {string.Join("/", m.BoxChance)}  locked {m.LockedChance}");
} }
