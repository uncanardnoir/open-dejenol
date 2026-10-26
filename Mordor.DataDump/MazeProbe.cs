using Mordor.Core;


/// <summary>
/// Sanity-checks the translated view selector against the real level 1 map: walks every open
/// square in all four facings and reports what comes out, so an obviously broken translation
/// (everything blank, everything the same, indices off the sheet) shows up without the GUI.
/// </summary>
public static class MazeProbe
{
    public static void Run(string dataDir)
    {
        var floor = MdrLoader.LoadDungeon(System.IO.Path.Combine(dataDir, "MDATA11.MDR"))[0];
        var fields = floor.Fields;
        int Walls(int square) =>
            square >= 1 && square <= fields.Length ? (int)fields[square - 1].Flags : 0;

        int squares = 0, blankBoth = 0, maxBg = 0, minBg = int.MaxValue, withSlots = 0, offSheet = 0;
        var bgSeen = new SortedSet<int>();
        var slotSeen = new SortedSet<int>();

        for (int y = 1; y <= floor.Height; y++)
            for (int x = 1; x <= floor.Width; x++)
            {
                if (floor.Has(x - 1, y - 1, Floor.ROCK)) continue;
                for (int f = 1; f <= 4; f++)
                {
                    var v = MazeSelect.For(x, y, f, 1, Walls);
                    squares++;
                    foreach (int bg in v.Bg)
                    {
                        bgSeen.Add(bg);
                        if (bg > maxBg) maxBg = bg;
                        if (bg < minBg) minBg = bg;
                        if (bg > 25) offSheet++;
                    }
                    if (v.Bg[0] == 0 && v.Bg[1] == 0) blankBoth++;

                    bool any = false;
                    for (int s = 1; s <= 8; s++)
                    {
                        if (v.Slot[s] == 0) continue;
                        any = true;
                        slotSeen.Add(s);
                        if ((v.Slot[s] & ~(MazeSelect.SlotLeft | MazeSelect.SlotRight)) != 0)
                            Console.WriteLine($"  unexpected slot bits at ({x},{y}) f{f} slot {s}: {v.Slot[s]}");
                    }
                    if (any) withSlots++;
                }
            }

        Console.WriteLine($"level 1: {squares} (square, facing) pairs over open squares");
        Console.WriteLine($"  background columns used : {string.Join(",", bgSeen)}");
        Console.WriteLine($"  range {minBg}..{maxBg}, off the 25-column sheet: {offSheet}");
        Console.WriteLine($"  both halves blank       : {blankBoth}");
        Console.WriteLine($"  at least one slab lit   : {withSlots}");
        Console.WriteLine($"  slots ever used         : {string.Join(",", slotSeen)}");

        // one concrete view, printed in full, so it can be compared against the real game
        for (int f = 1; f <= 4; f++)
        {
            var (sx, sy) = FirstOpen(floor);
            var v = MazeSelect.For(sx, sy, f, 1, Walls);
            Console.WriteLine($"  ({sx},{sy}) facing {f}: bg [{v.Bg[0]},{v.Bg[1]}] " +
                $"slots [{string.Join(" ", Enumerable.Range(1, 8).Select(i => v.Slot[i]))}] " +
                $"V [{string.Join(" ", Enumerable.Range(1, 22).Select(i => v.V[i]))}]");
        }
    }

    private static (int X, int Y) FirstOpen(Floor floor)
    {
        for (int y = 1; y <= floor.Height; y++)
            for (int x = 1; x <= floor.Width; x++)
                if (!floor.Has(x - 1, y - 1, Floor.ROCK)) return (x, y);
        return (1, 1);
    }
}
