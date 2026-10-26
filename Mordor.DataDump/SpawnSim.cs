using Mordor.Core;
/// <summary>Roll many encounters across every walkable cell of a level and report what actually shows up.</summary>
public static class SpawnSim { public static void Run(string dir) {
  var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
  var rng = new Random(11);
  for (int lv = 1; lv <= 3; lv++) {
    var f = dungeon[lv - 1];
    var cells = new List<(int x, int y)>();
    for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) if (!f.Has(x, y, Floor.ROCK)) cells.Add((x, y));
    var count = new Dictionary<string, int>();
    const int N = 20000;
    int empty = 0;
    for (int i = 0; i < N; i++) {
      var (x, y) = cells[rng.Next(cells.Count)];
      var g = EncounterGenerator.Generate(monsters, lv, rng, f, x, y);
      // An area whose spawn mask is empty holds nothing at all (fn0D4D, MODULE4.BAS:1443-
      // 1446), so Generate returning no monsters is an answer and not a failure. This used
      // to index g[0] regardless and fall over on the first such square.
      if (g.Count == 0) { empty++; continue; }
      count[g[0].Def.Name] = count.GetValueOrDefault(g[0].Def.Name) + 1;
    }
    Console.WriteLine($"=== level {lv}: {count.Count} distinct lead monsters over {N} rolls, "
                    + $"{empty * 100.0 / N:0.0}% of squares holding nothing");
    foreach (var kv in count.OrderByDescending(k => k.Value))
      Console.WriteLine($"   {kv.Key,-24} {kv.Value * 100.0 / N,5:0.00}%");
  }
} }
