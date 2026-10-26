using Mordor.Core;
/// <summary>What each guild crest casts, for Guild_Crests' second table.</summary>
public static class CrestSpellProbe { public static void Run(string dir) {
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
  Console.WriteLine($"{"crest",-26} {"spellIdx",8} {"spellId",7} {"spell",-22} {"charges",7} {"spellLvl",8}");
  foreach (var it in items.Where(i => i.Name.Trim().StartsWith("Crest of the")))
  {
    var sp = spells.FirstOrDefault(s => s.Id == it.SpellId);
    Console.WriteLine($"{it.Name.Trim(),-26} {it.SpellIndex,8} {it.SpellId,7} {sp?.Name.Trim() ?? "-",-22} {it.Charges,7} {it.SpellLvl,8}");
  }

  Console.WriteLine();
  var casters = items.Where(i => i.Id != 0 && i.SpellIndex > 0).ToList();
  Console.WriteLine($"items whose M56C4 says they cast: {casters.Count}");
  var lost = casters.Where(i => i.SpellId <= 0).ToList();
  Console.WriteLine($"of those, the ones a SpellId > 0 test would drop: {lost.Count}");
  foreach (var i in lost) Console.WriteLine($"  {i.Name.Trim(),-26} spellIdx {i.SpellIndex,3}  spellId {i.SpellId,3}  charges {i.Charges,3}");

  // Do the two fields ever name different spells? If they always agree, SpellId is redundant and
  // the only harm it did was the gate. If they diverge, more than the gate was wrong.
  int agree = 0;
  var differ = new List<string>();
  foreach (var i in casters)
  {
    if (i.SpellIndex - 1 >= spells.Count) { differ.Add($"{i.Name.Trim()}: index past the table"); continue; }
    var byIndex = spells[i.SpellIndex - 1];
    if (byIndex.Id == i.SpellId) agree++;
    else differ.Add($"{i.Name.Trim(),-24} record {i.SpellIndex - 1} is {byIndex.Name.Trim()} (id {byIndex.Id}), SpellId says {i.SpellId}");
  }
  Console.WriteLine($"\nitems where the record position and SpellId name the same spell: {agree} of {casters.Count}");
  foreach (var d in differ.Take(10)) Console.WriteLine($"  {d}");
} }
