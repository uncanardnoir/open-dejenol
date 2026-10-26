using Mordor.Core;
/// <summary>How many rooms on each level hold monsters, and how big those groups are.</summary>
public static class RoomProbe { public static void Run(string dir) {
  var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
  var state = new DungeonState();
  var now = DateTime.UtcNow;
  for (int lv = 1; lv <= 3; lv++) {
    var f = dungeon[lv - 1];
    var areaCells = new Dictionary<int, (int x, int y)>();
    for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) {
      int id = f.AreaIdAt(x, y);
      if (id >= 1 && !areaCells.ContainsKey(id)) areaCells[id] = (x, y);
    }
    int occupied = 0; var sizes = new List<int>();
    foreach (var (id, cell) in areaCells) {
      var g = EncounterGenerator.InRoom(monsters, f, lv, cell.x, cell.y, state, now, new Random(id));
      if (g != null) { occupied++; sizes.Add(g.Count); }
    }
    Console.WriteLine($"level {lv}: {areaCells.Count} rooms, {occupied} hold monsters");
    Console.WriteLine($"   group sizes: " + string.Join(" ", sizes.GroupBy(s => s).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
    Console.WriteLine($"   respawn delay: {DungeonState.RespawnDelay(lv, f.HeaderAreaCount, state.TallyOf(lv, f.HeaderAreaCount)).TotalMinutes:0.0} minutes");
    var ex = areaCells.Take(4).Select(kv => EncounterGenerator.InRoom(monsters, f, lv, kv.Value.x, kv.Value.y, state, now, new Random(kv.Key))).Where(g => g != null);
    foreach (var g in ex) Console.WriteLine("   e.g. " + EncounterGenerator.Describe(g!));
  }
} }
