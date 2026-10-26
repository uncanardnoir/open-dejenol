using Mordor.Core;

/// <summary>
/// Cross-checks the MDATA3 item record against Type T567A in the game's own MAIN.TXT.
/// The decompiled Type has two Integers (M56DB, M4ED4) where our reader took one 32-bit
/// Charges, so this reads the record both ways and reports where they disagree.
/// </summary>
public static class ItemLayoutProbe { public static void Run(string dir) {
  using var r = new RecordReader(System.IO.Path.Combine(dir, "MDATA3.MDR"), 125);
  r.Read(); r.GetString(); r.Read(); r.GetUWord(); r.Read(); int n = r.GetUWord();
  int split = 0, hiSet = 0;
  var odd = new List<string>();
  for (int i = 0; i < n; i++)
  {
    r.Read();
    string name = r.GetString();
    r.GetWord(); r.GetWord(); r.GetWord();          // id, att, def
    r.GetDword();                                    // price
    r.GetWord(); r.GetWord();                        // floor, rarity
    r.GetDword();                                    // abilities
    r.GetWord(); r.GetWord(); r.GetWord(); r.GetWord();  // swings, specialType, spellIndex, spellId
    int lo = (ushort)r.GetWord();                    // M56DB - charges
    int hi = (ushort)r.GetWord();                    // M4ED4 - the field we were swallowing
    long merged = (uint)(lo | (hi << 16));
    if (hi != 0) { hiSet++; if (odd.Count < 12) odd.Add($"  {name,-26} charges={lo,-6} M4ED4={hi,-6} (our merged value was {merged:N0})"); }
    split++;
    // skip the rest of the record
    r.GetDword(); r.GetWord(); r.GetFloat(); r.GetDword(); r.GetWord(); r.GetWord(); r.GetDword();
    r.GetWords(7); r.GetWords(7); r.GetWord(); r.GetWord(); r.GetWord();
  }
  Console.WriteLine($"items read: {split}");
  Console.WriteLine($"items where the extra field M4ED4 is non-zero: {hiSet}");
  if (odd.Count > 0) { Console.WriteLine("these are the ones our reader got wrong:"); foreach (var s in odd) Console.WriteLine(s); }
  else Console.WriteLine("M4ED4 is always 0, so the merged read happened to give the right Charges.");
} }
