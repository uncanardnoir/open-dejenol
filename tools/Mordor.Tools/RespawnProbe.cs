using Mordor.Core;

/// <summary>
/// When a cleared room fills up again - sub0A91 (MONSMOD.BAS:698-708) - and the four
/// oddities that come of the original comparing <c>Abs(Timer - reference)</c> where <c>Timer</c> is
/// seconds since midnight and carries no date at all.
/// </summary>
public static class RespawnProbe
{
    public static void Run(string dir)
    {
        var floors = MdrLoader.LoadDungeon(Path.Combine(dir, "MDATA11.MDR"));

        Console.WriteLine("how long a room takes to repopulate, by floor:");
        Console.WriteLine($"  {"floor",5} {"areas",6} {"respawn",10}  {"still occupied (2x)",20}");
        foreach (var fl in floors)
        {
            var d = DungeonState.RespawnDelay(fl.LevelNum, fl.HeaderAreaCount, fl.HeaderAreaCount);
            Console.WriteLine($"  {fl.LevelNum,5} {fl.HeaderAreaCount,6} {Show(d),10}  {Show(d + d),20}");
        }
        Console.WriteLine("  sub0A91: 1200s + 150s x floor, less 5s for every room emptied and not yet refilled, never under 120s.");
        Console.WriteLine("  A room you emptied uses that; a room that still has its monsters uses twice it,");
        Console.WriteLine("  counted from when they appeared - which is how ground you keep walking past changes.");

        var floor1 = floors[0];
        int areas = floor1.HeaderAreaCount;
        double delay = DungeonState.RespawnDelay(1, areas, areas - 1).TotalMinutes;   // one room emptied

        // ---- the ordinary case ----
        Console.WriteLine($"\nclearing a room on floor 1 at 10:00 (respawn {delay:F1} min):");
        foreach (int mins in new[] { 1, 10, 20, 21, 60 })
        {
            var s = new DungeonState();
            var cleared = new DateTime(2026, 9, 21, 10, 0, 0);
            s.MarkCleared(1, 1, cleared, areas);
            bool back = s.IsOccupied(1, 1, areas, cleared.AddMinutes(mins));
            Console.WriteLine($"  come back {mins,3} min later: {(back ? "monsters again" : "still empty")}");
        }

        // ---- quirk 1: no idea what day it is ----
        Console.WriteLine("\nquirk 1 - the clock carries no date:");
        foreach (double hours in new[] { 23.0, 24.0, 48.0 })
        {
            var s = new DungeonState();
            var cleared = new DateTime(2026, 9, 21, 10, 0, 0);
            s.MarkCleared(1, 1, cleared, areas);
            bool back = s.IsOccupied(1, 1, areas, cleared.AddHours(hours));
            Console.WriteLine($"  {hours,4} hours later ({cleared.AddHours(hours):HH:mm}): {(back ? "monsters again" : "STILL EMPTY")}");
        }
        Console.WriteLine("  a whole day away and the room is exactly as you left it.");

        // ---- quirk 2: the comparison is an absolute value ----
        Console.WriteLine("\nquirk 2 - Abs() means coming back later can read as earlier:");
        var at10 = new DateTime(2026, 9, 21, 10, 0, 0);
        foreach (var (h, m) in new[] { (23, 0), (23, 40), (23, 50) })
        {
            var s = new DungeonState();
            s.MarkCleared(1, 1, at10, areas);
            var later = at10.AddHours(h).AddMinutes(m);
            double diff = Math.Abs(DungeonState.SecondsSinceMidnight(later) - DungeonState.SecondsSinceMidnight(at10)) / 60;
            bool back = s.IsOccupied(1, 1, areas, later);
            Console.WriteLine($"  away {h}h{m:00}m, the game reads it as {diff,5:F0} min: {(back ? "monsters again" : "STILL EMPTY")}");
        }

        // ---- quirk 3: rooms emptied after 12:20am come back at midnight ----
        Console.WriteLine("\nquirk 3 - once the clock wraps past midnight, older rooms all come back at once:");
        foreach (var t in new[] { new TimeSpan(0, 25, 0), new TimeSpan(0, 40, 0), new TimeSpan(3, 0, 0), new TimeSpan(11, 0, 0) })
        {
            var s = new DungeonState();
            var cleared = new DateTime(2026, 9, 21) + t;
            s.MarkCleared(1, 1, cleared, areas);
            var midnight = new DateTime(2026, 9, 22, 0, 0, 1);
            bool back = s.IsOccupied(1, 1, areas, midnight);
            Console.WriteLine($"  emptied at {cleared:HH:mm}, looked at again at 00:00: {(back ? "monsters again" : "still empty")}");
        }
        Console.WriteLine($"  Checking just after midnight, the difference *is* the time the room was emptied, so any");
        Console.WriteLine($"  room emptied more than {delay:F0} minutes past midnight is back. The community page says");
        Console.WriteLine("  \"after 12:20am\", which is that cutoff for the 20-minute base; it moves with the floor.");

        // ---- quirk 4: midnight exactly reads as "never set" ----
        Console.WriteLine("\nquirk 4 - emptied at midnight to the second, so the stamp is zero:");
        {
            var s = new DungeonState();
            var midnight = new DateTime(2026, 9, 21, 0, 0, 0);
            s.MarkCleared(1, 1, midnight, areas);
            Console.WriteLine($"  stored stamp: {s.LoadRoom(1, 1).Foes[0].Stamp}");
            bool back = s.IsOccupied(1, 1, areas, midnight.AddSeconds(1));
            Console.WriteLine($"  one second later: {(back ? "monsters again - the zero read as never-set" : "still empty")}");
        }

        // ---- a room that was never cleared ----
        Console.WriteLine("\na room nobody has emptied keeps its group until twice the respawn time:");
        {
            var s = new DungeonState();
            var start = new DateTime(2026, 9, 21, 10, 0, 0);
            // a group standing since 10:00, as fn0D4D would have stamped it
            var rec = new RoomRecord();
            rec.Foes[0].Ref = 1; rec.Foes[0].GroupCount = 2; rec.Foes[0].Stamp = DungeonState.SecondsSinceMidnight(start);
            s.StoreRoom(1, 1, rec);
            foreach (int mins in new[] { 10, 30, 45, 60, 90 })
            {
                bool replaced = s.RefillDue(1, 1, areas, start.AddMinutes(mins));
                Console.WriteLine($"  after {mins,3} min: {(replaced ? "a different group" : "the same monsters, as they were left")}");
            }
        }

        static string Show(TimeSpan t) => $"{t.TotalMinutes:F1} min";
    }
}
