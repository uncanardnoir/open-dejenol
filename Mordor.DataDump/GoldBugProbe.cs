using Mordor.Core;
/// <summary>The gold the depth term actually produces, written vs the base-2 log it looks intended to be.</summary>
public static class GoldBugProbe { public static void Run(string dir) {
  var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0 && m.Hits > 0).ToList();
  Console.WriteLine($"{"floor",5} {"written",10} {"intended",10} {"ratio",7}   median encounter gold, 3000 fights");
  foreach (int lvl in new[] { 1, 2, 3, 5, 8, 12, 15 })
  {
    var pool = all.Where(m => m.LevelFound == lvl).ToList();
    if (pool.Count == 0) continue;
    var a = new List<double>(); var b = new List<double>();
    var rngA = new Random(4242); var rngB = new Random(4242);
    for (int i = 0; i < 3000; i++)
    {
      var m = pool[i % pool.Count];
      int n = 1 + (i % Math.Clamp((int)m.InGroup, 1, 6));
      a.Add(Group(m, n, rngA, written: true));
      b.Add(Group(m, n, rngB, written: false));
    }
    a.Sort(); b.Sort();
    Console.WriteLine($"{lvl,5} {a[1500],10:N0} {b[1500],10:N0} {100 * a[1500] / Math.Max(1, b[1500]),6:0}%");
  }
}

static double Group(Monster m, int count, Random rng, bool written)
{
  double g = m.GoldFactor;
  double x = (g * Math.Pow(10, g - 0.5)) / 4;
  double depth = written
      ? Math.Log((m.LevelFound + 1) / Math.Log(2))     // as the original has it
      : Math.Log(m.LevelFound + 1) / Math.Log(2);      // log base 2, the apparent intent
  x = (x / 4 + rng.NextDouble() * (x / 2) + rng.NextDouble() * (x / 2)) * depth;
  if (x < 500) x = (int)(rng.NextDouble() * 500) + 2;
  double n = Math.Log(count + 1);
  return x * (n * (n / 2));
} }
