using Mordor.Core;
public static class Bits {
  public static void Run(string dir) {
    var d = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir,"MDATA11.MDR"));
    var f = d[0];
    var hist = new int[64];
    foreach (var c in f.Fields) for (int b=0;b<64;b++) if ((c.Flags & (1L<<b))!=0) hist[b]++;
    System.Console.WriteLine("Level 1 bit histogram (of 900 cells):");
    for (int b=0;b<64;b++) if (hist[b]>0) System.Console.WriteLine($"  bit {b,2}: {hist[b],4} cells");
    System.Console.WriteLine("sample raw flags (first 8 cells): " + string.Join(", ", f.Fields.Take(8).Select(c => c.Flags)));
  }
}
