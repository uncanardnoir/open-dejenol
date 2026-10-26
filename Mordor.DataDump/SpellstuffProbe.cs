using Mordor.Core;
/// <summary>Print each guild's spellstuff[19][2] table from MDATA1 to see which spell schools it grants.</summary>
public static class SpellstuffProbe { public static void Run(string dir) {
  string path = System.IO.Path.Combine(dir, "MDATA1.MDR");
  using var r = new RecordReader(path, 260);
  string[] guilds = { "Nomad","Warrior","Paladin","Ninja","Villain","Seeker","Thief","Scavenger","Mage","Sorcerer","Wizard","Healer" };
  for (int i = 14; i <= 32; i++) {
    r.Seek(i); r.Read(); int len = r.GetUWord(); if (len < 2 || len > 16) continue;
    r.Seek(i); r.Read(); string name = r.GetString(); if (!guilds.Contains(name)) continue;
    r.GetWord(); r.GetWord(); r.GetWord(); r.GetFloat(); r.GetWord(); r.GetWords(7); r.GetDword();
    for (int k = 0; k < 7; k++) r.GetFloat(); r.GetWord(); r.GetFloat(); r.GetWord();
    var a = new float[19]; var b = new float[19];
    for (int k = 0; k < 19; k++) { a[k] = r.GetFloat(); b[k] = r.GetFloat(); }
    System.Console.WriteLine($"{name,-9} col0: {string.Join(" ", a.Select(v => v.ToString("0.#").PadLeft(5)))}");
    System.Console.WriteLine($"{"",-9} col1: {string.Join(" ", b.Select(v => v.ToString("0.#").PadLeft(5)))}");
  }
} }
