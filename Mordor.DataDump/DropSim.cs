using Mordor.Core;
/// <summary>What the ported item generation actually yields, per dungeon level.</summary>
public static class DropSim { public static void Run(string dir) {
  var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0 && m.Hits > 0).ToList();
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  var rng = new Random(31337);
  foreach (int lvl in new[] { 1, 3, 8, 15 })
  {
    var pool = all.Where(m => m.LevelFound == lvl).ToList();
    if (pool.Count == 0) continue;
    int chests = 0, drops = 0; var names = new Dictionary<string, int>();
    for (int i = 0; i < 6000; i++)
    {
      var m = pool[rng.Next(pool.Count)];
      int cls = Rules.ChestClass(m, rng.NextDouble() * 99, false);
      if (cls <= 0) continue;
      chests++;
      if (!Loot.DropsItem(m, cls, rng)) continue;
      if (Loot.RollDrop(items, m, cls, rng) is not Item it) continue;
      drops++;
      names[it.Name] = names.GetValueOrDefault(it.Name) + 1;
    }
    Console.WriteLine($"dungeon {lvl,2}: {chests,5} chests from 6000 fights, {drops,5} held an item ({100.0 * drops / Math.Max(1, chests):0}%)");
    Console.WriteLine("            commonest: " + string.Join(", ", names.OrderByDescending(k => k.Value).Take(6).Select(k => $"{k.Key} x{k.Value}")));
  }
} }
