using Mordor.Core;
/// <summary>Can type 0 - the Hands - actually reach the store's shelves?</summary>
public static class HandsProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  Console.WriteLine($"{"item",-22} {"type",4} {"floor",5} {"price",9} {"cursed",6}");
  foreach (var i in items.Where(x => x.Id != 0 && x.Type == 0))
    Console.WriteLine($"{i.Name.Trim(),-22} {i.Type,4} {i.Floor,5} {i.Price,9:N0} {i.Cursed,6}");
  int stockable = items.Count(i => i.Id != 0 && i.Type == 0 && i.Floor >= 1 && i.Cursed == 0);
  Console.WriteLine($"\nHands the store could ever shelve: {stockable}");

  // The pens draw the same way, so the same question applies to monster kind 0.
  var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var kind0 = mons.Where(m => m.Id != 0 && (int)m.Type == 0 && m.LevelFound >= 1).ToList();
  Console.WriteLine($"\nmonster kind 0 holds {kind0.Count} that could be penned: "
                  + string.Join(", ", kind0.Take(6).Select(m => m.Name.Trim())));
} }
