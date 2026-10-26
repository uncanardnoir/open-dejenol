using Mordor.Core;
public static class ItemTypeProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  foreach (var g in items.Where(i => i.Id != 0).GroupBy(i => i.Type).OrderBy(g => g.Key))
    System.Console.WriteLine($"type {g.Key,3} n={g.Count(),3}: {string.Join(", ", g.Take(6).Select(i => i.Name))}");
  System.Console.WriteLine("floors: " + string.Join(" ", items.GroupBy(i => i.Floor).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
  System.Console.WriteLine("rarity: " + string.Join(" ", items.GroupBy(i => i.Rarity).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
} }
