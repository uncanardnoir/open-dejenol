using Mordor.Core;

/// <summary>
/// What the automap is told, against what is really there - <c>fn2EB5</c> and <c>medPlotMaze</c>
/// (FRMAUTOM.BAS), and the recording at MAZEMOD.BAS:1579-1687.
/// </summary>
public static class MapProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var floors = MdrLoader.LoadDungeon(Path.Combine(dir, "MDATA11.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        // ---- how much of each level the map is allowed to show ----
        Console.WriteLine("what the automap records, and what it never does:");
        Console.WriteLine("  recorded edges:   " + string.Join(", ", MapRecord.OwnEdges.Select(Name)));
        Console.WriteLine("  recorded as walls until found: " + string.Join(", ", MapRecord.SecretEdges.Select(Name)));
        Console.WriteLine("  recorded features: " + string.Join(", ", MapRecord.Features.Select(Name)));
        Console.WriteLine("  never recorded:    Teleporter, Rotator, Anti-magic, Rock, Stud, Extinguisher,");
        Console.WriteLine("                     and the four facing squares - Perception is what tells you about those");

        Console.WriteLine("\nhow many secret doors the old map was giving away, level by level:");
        Console.WriteLine($"  {"level",5} {"squares",8} {"secret doors",13} {"teleporters",12}");
        foreach (var fl in floors)
        {
            int secret = 0, tele = 0;
            for (int y = 0; y < fl.Height; y++)
            for (int x = 0; x < fl.Width; x++)
            {
                if (fl.Has(x, y, Floor.SECRET_DOOR_EAST)) secret++;
                if (fl.Has(x, y, Floor.SECRET_DOOR_NORTH)) secret++;
                if (fl.Has(x, y, Floor.TELEPORTER)) tele++;
            }
            Console.WriteLine($"  {fl.LevelNum,5} {fl.Width * fl.Height,8} {secret,13} {tele,12}");
        }

        // ---- the perception roll ----
        Console.WriteLine("\nthe chance of noticing a secret door you are standing beside");
        Console.WriteLine("(Int(Rnd * 100) < fn122A(char, 5, False) / 5 - a fifth of your perception):");
        Console.WriteLine($"  {"guild",-10} {"level",5} {"perception",11} {"chance",7}  {"steps for an even chance",-24}");
        foreach (string g in new[] { "Nomad", "Warrior", "Thief", "Seeker", "Ninja" })
            foreach (int lvl in new[] { 1, 10, 40, 100 })
            {
                var c = Character.Create("X", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                         new[] { 18, 18, 18, 18, 18, 18 });
                c.Guilds.Clear();
                c.Guilds.Add(new GuildMembership { Guild = g, Level = lvl });
                double per = Rules.BestAbilityValue(c, 5);
                double chance = Math.Min(100, per / 5) / 100.0;
                string steps = chance <= 0 ? "never" : $"{Math.Ceiling(Math.Log(0.5) / Math.Log(1 - chance)):N0}";
                Console.WriteLine($"  {g,-10} {lvl,5} {per,11:F1} {chance * 100,6:F1}%  {steps,-24}");
            }

        // ---- a walk past a secret door ----
        var floor = floors.FirstOrDefault(f => Any(f, Floor.SECRET_DOOR_EAST) || Any(f, Floor.SECRET_DOOR_NORTH));
        if (floor == null) { Console.WriteLine("\n(no secret doors in the data?)"); return; }

        (int x, int y, int bit) spot = default;
        for (int y = 0; y < floor.Height && spot.bit == 0; y++)
        for (int x = 0; x < floor.Width; x++)
        {
            if (floor.Has(x, y, Floor.SECRET_DOOR_EAST)) { spot = (x, y, Floor.SECRET_DOOR_EAST); break; }
            if (floor.Has(x, y, Floor.SECRET_DOOR_NORTH)) { spot = (x, y, Floor.SECRET_DOOR_NORTH); break; }
        }

        Console.WriteLine($"\nstanding on level {floor.LevelNum} square ({spot.x + 1},{spot.y + 1}), which has a " +
                          $"{Name(spot.bit)}:");
        foreach (string g in new[] { "Nomad", "Seeker" })
            foreach (int lvl in new[] { 1, 40 })
            {
                var c = Character.Create("Walker", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                         new[] { 18, 18, 18, 18, 18, 18 });
                c.Guilds.Clear();
                c.Guilds.Add(new GuildMembership { Guild = g, Level = lvl });

                int found = 0, trials = 2000;
                var tally = new List<int>();
                for (int t = 0; t < trials; t++)
                {
                    var map = new SharedMap();
                    var rng = new Random(t);
                    int steps = 0;
                    while (steps < 200 && !map.Recorded(floor.LevelNum, spot.x, spot.y, floor.Width, spot.bit))
                    {
                        // each visit clears the record of the roll so the next step rolls again,
                        // exactly as stepping away and back does
                        map.Unrecord(floor.LevelNum, spot.x, spot.y, floor.Width, MapRecord.WallBehind(spot.bit));
                        MapRecord.OnEntry(map, floor, c, spot.x, spot.y, rng, Lookup);
                        steps++;
                        if (map.Recorded(floor.LevelNum, spot.x, spot.y, floor.Width, spot.bit)) { found++; tally.Add(steps); break; }
                    }
                }
                Console.WriteLine($"  a {g} {lvl,-3} found it in {found} of {trials} walks of up to 200 steps" +
                                  (tally.Count > 0 ? $", median {Median(tally)} steps" : ""));
            }

        // ---- what the map holds after one visit ----
        Console.WriteLine($"\nwhat one visit writes down for that square, for a Nomad 1:");
        var nomad = Character.Create("Walker", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male,
                                     new[] { 18, 18, 18, 18, 18, 18 });
        nomad.Guilds.Clear();
        nomad.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 1 });
        var m2 = new SharedMap();
        MapRecord.OnEntry(m2, floor, nomad, spot.x, spot.y, new Random(1), Lookup);
        long seen = m2.SeenFlags(floor.LevelNum, spot.x, spot.y, floor.Width);
        long real = floor.At(spot.x, spot.y).Flags;
        Console.WriteLine($"  really there: {string.Join(", ", Bits(real))}");
        Console.WriteLine($"  on the map:   {string.Join(", ", Bits(seen))}");
        Console.WriteLine("  the secret door is down as a plain wall, and that is what the map draws.");

        bool Any(Floor f, int bit)
        {
            for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) if (f.Has(x, y, bit)) return true;
            return false;
        }
        int Median(List<int> xs) { xs.Sort(); return xs[xs.Count / 2]; }
    }

    private static readonly string[] BitNames =
    {
        "WallEast", "WallNorth", "DoorEast", "DoorNorth", "SecretDoorEast", "SecretDoorNorth",
        "FaceNorth", "FaceEast", "FaceSouth", "FaceWest", "Extinguisher", "Pit", "StairsUp",
        "StairsDown", "Teleporter", "Water", "Quicksand", "Rotator", "AntiMagic", "Rock", "Fog",
        "Chute", "Stud", "Rescue",
    };
    private static string Name(int bit) => bit >= 0 && bit < BitNames.Length ? BitNames[bit] : $"bit{bit}";
    private static IEnumerable<string> Bits(long flags) =>
        Enumerable.Range(0, BitNames.Length).Where(b => (flags & (1L << b)) != 0).Select(Name)
                  .DefaultIfEmpty("(nothing)");
}
