using Mordor.Core;
/// <summary>How often chests appear, and which traps, using the ported MONSMOD rules.</summary>
public static class TrapProbe { public static void Run(string dir) {
  var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0 && m.Hits > 0).ToList();
  var rng = new Random(99);
  foreach (int lvl in new[] { 1, 5, 10, 15 })
  {
    var pool = all.Where(m => m.LevelFound == lvl).ToList();
    if (pool.Count == 0) continue;
    var cls = new int[4]; var traps = new int[12];
    for (int i = 0; i < 4000; i++)
    {
      var m = pool[rng.Next(pool.Count)];
      int c = Rules.ChestClass(m, rng.NextDouble() * 99, false);
      cls[c]++;
      traps[Rules.ChestTrap(m, c, rng)]++;
    }
    Console.WriteLine($"dungeon {lvl,2}: chest class 0/1/2/3 = {cls[0]}/{cls[1]}/{cls[2]}/{cls[3]}");
    var named = Enumerable.Range(1, 10).Where(t => traps[t] > 0)
                  .Select(t => $"{Rules.TrapNames[t]} {100.0 * traps[t] / 4000:0.#}%");
    Console.WriteLine($"            traps: {(named.Any() ? string.Join(", ", named) : "none")}");
  }
} }
