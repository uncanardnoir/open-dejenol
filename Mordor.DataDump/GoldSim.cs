using Mordor.Core;

/// <summary>
/// What the game's own gold formula (MONSMOD.BAS, ported in Rules.EncounterGold) pays out,
/// so the magnitudes can be eyeballed against the cost of making a level.
/// </summary>
public static class GoldSim { public static void Run(string dir) {
  GuildData.LoadFrom(dir);
  var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0 && m.Hits > 0).ToList();
  var rng = new Random(1234);
  Console.WriteLine("dungeon lvl | typical encounter gold (median of 400) | range");
  foreach (int lvl in new[] { 1, 2, 3, 5, 8, 12, 15 })
  {
    var pool = all.Where(m => m.LevelFound == lvl).ToList();
    if (pool.Count == 0) continue;
    var takes = new List<long>();
    for (int i = 0; i < 400; i++)
    {
      var m = pool[rng.Next(pool.Count)];
      int count = rng.Next(1, Math.Clamp((int)m.InGroup, 1, 6) + 1);
      // the caller decides the chest, as the engine does
      int cls = Rules.ChestClass(m, rng.NextDouble() * 99, false);
      bool locked = cls > 1 && rng.Next(100) + 1 <= m.LockedChance;
      takes.Add(Rules.EncounterGold(new[] { m }, new[] { count }, rng, cls, locked));
    }
    takes.Sort();
    Console.WriteLine($"   {lvl,2}       | {takes[takes.Count / 2],14:N0} | {takes[20],10:N0} .. {takes[^20],12:N0}");
  }
  Console.WriteLine();
  Console.WriteLine("gold needed to make a level (Nomad): " + string.Join(", ",
    new[] { 2, 3, 4, 5 }.Select(l => $"L{l}={Rules.Fn12B2(l - 1, GuildData.ByName("Nomad").GoldFactor):N0}")));
} }
