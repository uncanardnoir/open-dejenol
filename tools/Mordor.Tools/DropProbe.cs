using Mordor.Core;
/// <summary>Sanity-check the two fields the item-drop rules hang on.</summary>
public static class DropProbe { public static void Run(string dir) {
  var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
  Console.WriteLine("ItemChance (M5AEE) distribution: " + string.Join(", ",
    ms.GroupBy(m => m.ItemChance).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}")));
  // Deep_Item_Dropping_Monsters lists fifteen of these. The data holds sixteen: Mind Twister
  // (spawn 15, drops 16) is not on the page. The page excludes "monsters programmed to drop
  // specific items from deep levels" and monsters laired shallower than they spawn, so it may
  // belong to one of those or may simply have been missed - the port follows MDATA5 either way.
  Console.WriteLine("monsters whose items come from deeper than they spawn (the page lists 15 of these):");
  foreach (var m in ms.Where(x => x.Id != 0 && x.ItemDropLevel > x.LevelFound).OrderBy(x => x.LevelFound).ThenBy(x => x.Name))
    Console.WriteLine($"  {m.Name,-22} spawn {m.LevelFound,2}  drops {m.ItemDropLevel,2}");
  Console.WriteLine();
  Console.WriteLine("ItemDropLevel (M560D) range: " + ms.Min(m => m.ItemDropLevel) + ".." + ms.Max(m => m.ItemDropLevel));
  Console.WriteLine();
  foreach (var m in ms.Where(m => m.LevelFound <= 2).Take(6))
    Console.WriteLine($"  {m.Name,-20} floor {m.LevelFound}  itemChance {m.ItemChance,3}  dropLevel {m.ItemDropLevel,2}  kinds {string.Join(",", m.Items)}");
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
  Console.WriteLine($"\nitem Kind values present: {string.Join(",", items.Select(i => i.Type).Distinct().OrderBy(t => t))}");
} }
