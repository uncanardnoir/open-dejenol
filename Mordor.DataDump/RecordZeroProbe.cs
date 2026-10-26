using Mordor.Core;
/// <summary>What is record 0 in each table, and is it a template or a real thing?</summary>
public static class RecordZeroProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  var mons  = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));

  Console.WriteLine($"MDATA2 spells   {spells.Count,4} records, record 0 = \"{spells[0].Name.Trim()}\" id {spells[0].Id}");
  Console.WriteLine($"                ids equal to 0: {spells.Count(s => s.Id == 0)}, blank names: {spells.Count(s => s.Name.Trim().Length == 0)}");

  Console.WriteLine($"\nMDATA3 items    {items.Count,4} records, record 0 = \"{items[0].Name.Trim()}\" id {items[0].Id}");
  Console.WriteLine($"                ids equal to 0: {items.Count(i => i.Id == 0)}, blank names: {items.Count(i => i.Name.Trim().Length == 0)}");
  var dupOfZero = items.Skip(1).FirstOrDefault(i => i.Name.Trim() == items[0].Name.Trim());
  Console.WriteLine($"                another item with the same name: {(dupOfZero == null ? "none" : $"id {dupOfZero.Id}")}");

  Console.WriteLine($"\nMDATA5 monsters {mons.Count,4} records, record 0 = \"{mons[0].Name.Trim()}\" id {mons[0].Id}");
  Console.WriteLine($"                ids equal to 0: {mons.Count(m => m.Id == 0)}, blank names: {mons.Count(m => m.Name.Trim().Length == 0)}");
  var dupM = mons.Skip(1).FirstOrDefault(m => m.Name.Trim() == mons[0].Name.Trim());
  var og = mons.FirstOrDefault(m => m.Name.Trim() == "Outcast Goblin");
  if (og != null)
  {
    var z = mons[0];
    Console.WriteLine($"\n  GOBLIE vs Outcast Goblin: floor {z.LevelFound}/{og.LevelFound}  hits {z.Hits}/{og.Hits}"
                    + $"  att {z.Att}/{og.Att}  def {z.Def}/{og.Def}  glvl {z.GuildLevel}/{og.GuildLevel}"
                    + $"  kind {z.Kind}/{og.Kind}  chance {z.Chance}/{og.Chance}  trapFlags 0x{z.TrapFlags:X}/0x{og.TrapFlags:X}");
  }
  Console.WriteLine($"                another monster with the same name: {(dupM == null ? "none" : $"id {dupM.Id}")}");
} }
