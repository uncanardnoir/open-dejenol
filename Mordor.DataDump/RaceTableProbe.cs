using Mordor.Core;
/// <summary>The race table as loaded from the data, laid out like the community's Races page.</summary>
public static class RaceTableProbe { public static void Run(string dir) {
  RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
  Console.WriteLine($"{"Race",-10} {"Age",4} {"XP",3}  " + string.Join(" ", RaceData.StatNames.Select(n => $"{n,-6}")) + " G N E  Size");
  foreach (var r in RaceData.All)
  {
    string stats = string.Join(" ", Enumerable.Range(0, 6).Select(i => $"{r.Min[i],2}/{r.Max[i],-3}"));
    string al = string.Join(" ", Enumerable.Range(0, 3).Select(b => (r.AlignMask & (1 << b)) != 0 ? "X" : "-"));
    Console.WriteLine($"{r.Name,-10} {r.MaxAge,4} {r.ExpFactor,3}  {stats} {al}  {r.Size}");
  }
  Console.WriteLine($"\n{"Race",-10} " + string.Join(" ", Librarian.Resistances.Select(n => $"{n,-5}")));
  foreach (var r in RaceData.All)
    Console.WriteLine($"{r.Name,-10} " + string.Join(" ", r.Resistances.Select(v => $"{(v == 0 ? "-" : v + "%"),-5}")));

  Console.WriteLine();
  Console.WriteLine("which guilds each race may enter:");
  foreach (var r in RaceData.All)
    Console.WriteLine($"{r.Name,-10} " + string.Join(",", GuildData.All.Where(g => g.AllowsRace(r.Name)).Select(g => g.Name)));
} }
