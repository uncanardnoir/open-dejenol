using Mordor.Core;

/// <summary>
/// Lists every teleporter in the dungeon and says where it puts you. The square routine takes a
/// fixed destination as it is, with no rock test and no level-0 test (MAZEMOD.BAS:1788-1830), so a
/// destination in rock or off the map would leave a live party standing there; those are marked.
/// A random one is drawn for each member until it is neither rock nor a teleporter (1798-1808).
/// </summary>
public static class TeleportProbe
{
    public static void Run(string dir)
    {
        var floors = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));

        foreach (var fl in floors)
        {
            foreach (var t in fl.Teleporters)
            {
                int to = t.Z2 > 0 ? t.Z2 : fl.LevelNum;
                string what;
                if (t.X2 == 0 || t.Y2 == 0) what = to == fl.LevelNum ? "random on this level" : $"random on level {to}";
                else
                {
                    var dest = floors.FirstOrDefault(f => f.LevelNum == to);
                    bool outside = dest == null || t.X2 < 1 || t.Y2 < 1
                                || t.X2 > dest.Width || t.Y2 > dest.Height;
                    bool rock = !outside && dest!.Has(t.X2 - 1, t.Y2 - 1, Floor.ROCK);
                    what = outside ? "OFF THE MAP" : rock ? "IN ROCK" : $"{t.X2},{t.Y2},{to}";
                }
                Console.WriteLine($"  level {fl.LevelNum,2}  at {t.X,2},{t.Y,2}  ->  {what}");
            }
        }
    }
}
