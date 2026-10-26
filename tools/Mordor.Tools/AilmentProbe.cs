using Mordor.Core;
/// <summary>How long poison and disease last, and what they cost, at various CON.</summary>
public static class AilmentProbe { public static void Run(string dir) {
  var rng = new Random(2024);
  Console.WriteLine($"{"CON",4} {"strength",9} | {"poison: steps",14} {"hits",6} | {"disease: steps",15} {"hits",6}");
  foreach (int con in new[] { 6, 10, 14, 18 })
    foreach (int lvl in new[] { 1, 5, 10 })
    {
      double pSteps = 0, pHits = 0, dSteps = 0, dHits = 0;
      for (int t = 0; t < 4000; t++)
      {
        int s = 0, h = 0;
        while (s < 500) { var r = Rules.AilmentStep(rng, con, lvl, 0); s++; h += r.HitsLost; if (r.CuredPoison) break; }
        pSteps += s; pHits += h;
        s = 0; h = 0;
        while (s < 500) { var r = Rules.AilmentStep(rng, con, 0, lvl); s++; h += r.HitsLost; if (r.CuredDisease) break; }
        dSteps += s; dHits += h;
      }
      Console.WriteLine($"{con,4} {lvl,9} | {pSteps / 4000,14:0.0} {pHits / 4000,6:0.0} | {dSteps / 4000,15:0.0} {dHits / 4000,6:0.0}");
    }
} }
