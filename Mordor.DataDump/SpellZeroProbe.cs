using Mordor.Core;
/// <summary>Is spell record 0 a real spell, and can a Thief reach it?</summary>
public static class SpellZeroProbe { public static void Run(string dir) {
  Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
  var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
  Console.WriteLine($"{spells.Count} spell records; blank-named: {spells.Count(s => s.Name.Trim().Length == 0)}");
  Console.WriteLine($"record 0: \"{spells[0].Name.Trim()}\" id {spells[0].Id} level {spells[0].Level} effect {spells[0].Effect}");
  Console.WriteLine($"other records with id 0: {spells.Count(s => s.Id == 0)}");

  foreach (var (guild, level) in new[] { ("Thief", 6), ("Thief", 30), ("Nomad", 30), ("Nomad", 60) })
  {
    var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
    c.Guilds.Clear();
    c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
    var known = Spellbook.Known(c, spells);
    bool has = known.Any(s => s.Name.Trim() == "Charm of Opening");
    Console.WriteLine($"  {guild,-8} {level,3}: knows {known.Count,3} spells, Charm of Opening: {(has ? "yes" : "NO")}");
  }
} }
