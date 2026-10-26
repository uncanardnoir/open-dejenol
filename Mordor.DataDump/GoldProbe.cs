using Mordor.Core;
public static class GoldProbe { public static void Run(string dir) {
  var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
  foreach (var g in monsters.GroupBy(m => m.LevelFound).OrderBy(g => g.Key))
    Console.WriteLine($"  L{g.Key,2}: n={g.Count(),3} goldFactor min {g.Min(m => m.GoldFactor),8} avg {g.Average(m => m.GoldFactor),10:0} max {g.Max(m => m.GoldFactor),9}   hits avg {g.Average(m => m.Hits),5:0}  e.g. {string.Join(", ", g.Take(3).Select(m => $"{m.Name}={m.GoldFactor}"))}");
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0 && i.Floor >= 1).ToList();
  foreach (var g in items.GroupBy(i => i.Floor).OrderBy(g => g.Key))
    Console.WriteLine($"  items fl{g.Key,2}: price median {g.OrderBy(i => i.Price).ElementAt(g.Count() / 2).Price,10:N0}  max {g.Max(i => i.Price),12:N0}");
} }
