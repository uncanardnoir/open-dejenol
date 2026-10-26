using Mordor.Core;
/// <summary>What guild level the monsters on each dungeon floor actually are.</summary>
public static class DepthProbe { public static void Run(string dir) {
  var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0 && m.Hits > 0).ToList();
  Console.WriteLine($"{"floor",5} {"monsters",9} {"median glvl",12} {"median hits",12} {"median att",11}");
  foreach (int lvl in Enumerable.Range(1, 15))
  {
    var pool = ms.Where(m => m.LevelFound == lvl).ToList();
    if (pool.Count == 0) continue;
    var g = pool.Select(m => (int)m.GuildLevel).OrderBy(v => v).ToList();
    var h = pool.Select(m => (int)m.Hits).OrderBy(v => v).ToList();
    var a = pool.Select(m => (int)m.Att).OrderBy(v => v).ToList();
    Console.WriteLine($"{lvl,5} {pool.Count,9} {g[g.Count / 2],12} {h[h.Count / 2],12} {a[a.Count / 2],11}");
  }
} }
