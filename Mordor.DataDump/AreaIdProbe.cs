using Mordor.Core;
/// <summary>Are spawn-area ids 0-based or 1-based? Check what kind of cells carry id 0.</summary>
public static class AreaIdProbe { public static void Run(string dir) {
  var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
  for (int lv = 1; lv <= 3; lv++) {
    var f = dungeon[lv - 1];
    int zeroRock = 0, zeroOpen = 0, nonZeroRock = 0, nonZeroOpen = 0;
    for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) {
      bool rock = f.Has(x, y, Floor.ROCK);
      if (f.At(x, y).AreaId == 0) { if (rock) zeroRock++; else zeroOpen++; }
      else { if (rock) nonZeroRock++; else nonZeroOpen++; }
    }
    int maxId = 0; for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) maxId = System.Math.Max(maxId, f.At(x, y).AreaId);
    int nonEmptyAreas = f.Areas.Count(a => a.SpawnMask != 0 || a.LairId != 0);
    Console.WriteLine($"L{lv}: areaId 0 -> rock {zeroRock,3}, open {zeroOpen,3} | areaId>0 -> rock {nonZeroRock,3}, open {nonZeroOpen,3} | max id {maxId}, areas with data {nonEmptyAreas}");
    // last area record with data: if ids are 1-based, area[maxId] should have data and area[0] may be junk
    Console.WriteLine($"     area[0] mask 0x{f.Areas[0].SpawnMask:X} lair {f.Areas[0].LairId} | area[{maxId}] mask 0x{f.Areas[maxId].SpawnMask:X} lair {f.Areas[maxId].LairId} | area[{maxId + 1}] mask 0x{f.Areas[System.Math.Min(maxId + 1, f.Areas.Count - 1)].SpawnMask:X}");
  }
} }
