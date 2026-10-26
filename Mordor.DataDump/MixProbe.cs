using Mordor.Core;
/// <summary>Confirm multi-group encounters really do mix monster types.</summary>
public static class MixProbe { public static void Run(string dir) {
  var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var rng = new Random(77);
  foreach (int lvl in new[] { 1, 5, 12 })
  {
    int mixed = 0; var samples = new List<string>();
    for (int i = 0; i < 2000; i++)
    {
      var g = EncounterGenerator.Generate(all, lvl, rng);
      if (g.Select(m => m.Def.Id).Distinct().Count() > 1)
      {
        mixed++;
        if (samples.Count < 3) samples.Add(EncounterGenerator.Describe(g));
      }
    }
    Console.WriteLine($"dungeon {lvl,2}: {100.0 * mixed / 2000:0.#}% of encounters mix types");
    foreach (var s in samples) Console.WriteLine("    " + s);
  }
} }
