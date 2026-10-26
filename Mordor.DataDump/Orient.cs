using Mordor.Core;
public static class Orient {
  public static void Run(string dir) {
    var f = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir,"MDATA11.MDR"))[0];
    for (int y=0;y<f.Height;y++) for (int x=0;x<f.Width;x++) {
      if (f.Has(x,y,Floor.STAIRS_UP)) System.Console.WriteLine($"STAIRS_UP (town entrance) at x={x} y={y}");
      if (f.Has(x,y,Floor.STAIRS_DOWN)) System.Console.WriteLine($"STAIRS_DOWN at x={x} y={y}");
    }
    System.Console.WriteLine("teleporters: " + string.Join("; ", f.Teleporters.Select(t => $"({t.X},{t.Y})->({t.X2},{t.Y2},L{t.Z2})")));
    System.Console.WriteLine("chutes: " + string.Join("; ", f.Chutes.Select(c => $"({c.X},{c.Y}) depth {c.Depth}")));
    // rock along which edges? count rock per row 0, row 29, col 0, col 29
    int r0=0,r29=0,c0=0,c29=0; for(int i=0;i<30;i++){ if(f.Has(i,0,Floor.ROCK))r0++; if(f.Has(i,29,Floor.ROCK))r29++; if(f.Has(0,i,Floor.ROCK))c0++; if(f.Has(29,i,Floor.ROCK))c29++; }
    System.Console.WriteLine($"rock count: row0={r0} row29={r29} col0={c0} col29={c29}");
  }
}
