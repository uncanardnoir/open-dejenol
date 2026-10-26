using Mordor.Core;
public static class StatModProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
  foreach (var it in items.Where(i => i.StatsMod.Any(v => v != 0)).Take(25))
    Console.WriteLine($"  {it.Name,-26} mods [{string.Join(",", it.StatsMod)}] req [{string.Join(",", it.StatsRequired)}] cursed {it.Cursed}");
  Console.WriteLine($"cursed items: {items.Count(i => i.Cursed != 0)}; with stat mods: {items.Count(i => i.StatsMod.Any(v => v != 0))}; with stat reqs: {items.Count(i => i.StatsRequired.Any(v => v != 0))}");
} }
