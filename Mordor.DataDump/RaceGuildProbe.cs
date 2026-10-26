using Mordor.Core;
public static class RaceGuildProbe { public static void Run(string dir) {
  Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
  System.Console.WriteLine($"races loaded from game data: {RaceData.All.Count}");
  foreach (var r in RaceData.All) System.Console.WriteLine($"  {r.Name,-8} min {string.Join(",",r.Min)} max {string.Join(",",r.Max)} bonus {r.BonusPoints} align {string.Join("/",r.Alignments)} size {r.Size}");
  System.Console.WriteLine($"guilds loaded from game data: {GuildData.All.Count}");
  foreach (var g in GuildData.All) System.Console.WriteLine($"  {g.Name,-10} max {g.MaxLevel,3} hits/lvl {g.AvgHits} to {g.HitsCapLevel} then {g.LateHits} expF {g.ExpFactor,5:F1} req {string.Join("/",g.ReqStats)} align 0x{g.AlignMask:X} races 0x{g.RaceMask:X}");
  var c = Character.Create("T", RaceData.ByName("Human"), Alignment.Good, Sex.Male, new[]{14,9,9,10,16,15});
  System.Console.WriteLine($"test Human(G) 14/9/9/10/16/15 can join: " + string.Join(", ", GuildData.All.Where(g => g.Unmet(c).Count==0).Select(g=>g.Name)));
  System.Console.WriteLine($"exp to Human Nomad L2 = {Rules.Fn12A1(1, GuildData.ByName("Nomad").ExpFactor, RaceData.ByName("Human").ExpFactor)}, Wizard L2 = {Rules.Fn12A1(1, GuildData.ByName("Wizard").ExpFactor, RaceData.ByName("Human").ExpFactor)}");
} }
