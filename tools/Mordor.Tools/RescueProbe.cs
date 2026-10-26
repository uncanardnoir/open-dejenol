using Mordor.Core;

/// <summary>
/// Checks getting a body out of the dungeon: how long waiting takes at each depth, what the
/// morgue charges to send rescuers, and how often they come back (FRMDUNM.BAS:140, FRMMORG.BAS:104).
/// </summary>
public static class RescueProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var rng = new Random(31);

        Console.WriteLine("how long you lie there before somebody finds you (500 deaths a row):");
        Console.WriteLine($"  {"level",5} {"median days",12} {"worst",8} {"as years",10}");
        foreach (int lvl in new[] { 1, 2, 3, 5, 8, 12, 15 })
        {
            var days = new List<int>();
            for (int i = 0; i < 500; i++) days.Add(Rescue.DaysUntilFound(rng, lvl, 50, false));
            days.Sort();
            Console.WriteLine($"  {lvl,5} {days[days.Count / 2],12:N0} {days[^1],8:N0} {days[days.Count / 2] / 365.0,9:0.0}y");
        }

        Console.WriteLine("\nthe one merciful case - level 1, in an area of 18 or lower (near the way in):");
        var near = new List<int>();
        for (int i = 0; i < 500; i++) near.Add(Rescue.DaysUntilFound(rng, 1, 12, false));
        near.Sort();
        Console.WriteLine($"  median {near[near.Count / 2]} day(s), worst {near[^1]}");

        Console.WriteLine("\nand what the unused square flag would have done, had any square carried it:");
        foreach (int lvl in new[] { 5, 12 })
        {
            var plain = new List<int>();
            var bonus = new List<int>();
            for (int i = 0; i < 500; i++) { plain.Add(Rescue.DaysUntilFound(rng, lvl, 50, false)); bonus.Add(Rescue.DaysUntilFound(rng, lvl, 50, true)); }
            plain.Sort(); bonus.Sort();
            Console.WriteLine($"  level {lvl,2}: {plain[plain.Count / 2],7:N0} days -> {bonus[bonus.Count / 2],7:N0} with the flag");
        }

        Console.WriteLine("\nwhat the morgue charges to send rescuers, and how often they return:");
        Console.WriteLine($"  {"level",5} {"cost",14} {"return",8}");
        // the dungeon has fifteen levels and FRMMORG.BAS:137 rejects anything deeper, so those
        // are the only depths that can ever be asked for
        foreach (int lvl in new[] { 1, 3, 6, 10, 12, 15 })
        {
            int back = 0;
            for (int i = 0; i < 2000; i++) if (Rescue.RescuersReturn(rng, lvl)) back++;
            Console.WriteLine($"  {lvl,5} {Rescue.SearchCost(lvl),14:N0} {back / 20.0,7:0.0}%");
        }

        Console.WriteLine("\nwhat they report:");
        Character Body(bool dead, int cause, int hits)
        {
            var c = Character.Create("Fallen", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
            c.MaxHits = 50; c.CurHits = hits; c.IsDead = dead; c.CauseOfDeath = cause;
            c.DungeonLevel = 4; c.X = 10; c.Y = 12;
            return c;
        }
        foreach (var (label, body) in new (string, Character)[]
        {
            ("dead, ordinary", Body(true, Rules.DeathOrdinary, 0)),
            ("dead in rock", Body(true, Rules.DeathRock, 0)),
            ("alive, badly hurt", Body(false, 0, 8)),
            ("alive and well", Body(false, 0, 45)),
        })
        {
            var r = Rescue.Report(body, foundAtAll: true);
            Console.WriteLine($"  {label,-18} -> {r.Message}  (brought back: {r.BroughtBack})");
        }
        Console.WriteLine($"  {"wrong place",-18} -> {Rescue.Report(Body(true, Rules.DeathOrdinary, 0), foundAtAll: false).Message}");

        Console.WriteLine("\nhow near the coordinates have to be (body at 10,12 level 4):");
        foreach (var (gx, gy, gl) in new[] { (10, 12, 4), (17, 19, 4), (18, 12, 4), (10, 12, 5) })
            Console.WriteLine($"  you say {gx},{gy},{gl}: {(Rescue.CloseEnough(gx, gy, gl, 10, 12, 4) ? "found" : "not found")}");
    }
}
