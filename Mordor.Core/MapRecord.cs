namespace Mordor.Core;

/// <summary>
/// What the automap has actually been told about each square, as against what is really there.
///
/// The original keeps a 30x30 grid of flag words per level in its own file and draws the map from
/// that and nothing else - <c>fn2EB5</c> (FRMAUTOM.BAS:520) reads the automap file, and every cell
/// <c>medPlotMaze</c> puts on screen comes from it. The port drew straight from the loaded maze
/// instead, so an explored square showed its true edges. That gave away **every secret door on any
/// square you had walked past**, which is most of what the maps in this game are for.
///
/// The recording happens on the step that takes you onto a square (MAZEMOD.BAS:1579-1687). Three
/// rules govern it:
///
/// - only some of the flags are written down at all. The walls and doors on the square's own east
///   and north sides, and seven features: pit, stairs up, stairs down, water, quicksand, fog and
///   chute. A teleporter, a rotator, an anti-magic square, rock, a stud or an extinguisher is
///   never recorded, which is why <see cref="Perception"/> exists to tell you about those instead
/// - **a secret door is written down as a plain wall** unless you notice it, and you get one roll
///   each time you step beside one: <c>Int(Rnd * 100) &lt; fn122A(char, 5, False) / 5</c>, a fifth
///   of your perception. Notice it and the wall bit is cleared and the secret door written in its
///   place
/// - in fog, none of the edges are recorded at all - only the features
///
/// Stepping onto a square also records the east side of the square to your west and the north side
/// of the square to your south, because you can see those from where you stand.
/// </summary>
public static class MapRecord
{
    /// <summary>The square's own edges, recorded as seen - MAZEMOD.BAS:1583.</summary>
    public static readonly int[] OwnEdges =
        { Floor.WALL_EAST, Floor.WALL_NORTH, Floor.DOOR_EAST, Floor.DOOR_NORTH };

    /// <summary>The two secret doors, which are recorded as walls until found - MAZEMOD.BAS:1589.</summary>
    public static readonly int[] SecretEdges = { Floor.SECRET_DOOR_EAST, Floor.SECRET_DOOR_NORTH };

    /// <summary>
    /// The features the automap writes down - MAZEMOD.BAS:1609. The list is short and the omissions
    /// are the point: a teleporter, rotator, anti-magic square, rock, stud or extinguisher is not
    /// here, so the map never shows one however often you cross it.
    /// </summary>
    public static readonly int[] Features =
        { Floor.PIT, Floor.STAIRS_UP, Floor.STAIRS_DOWN, Floor.WATER, Floor.QUICKSAND, Floor.FOG, Floor.CHUTE };

    /// <summary>The wall that stands in for an unnoticed secret door on the same side.</summary>
    public static int WallBehind(int secretBit) =>
        secretBit == Floor.SECRET_DOOR_EAST ? Floor.WALL_EAST : Floor.WALL_NORTH;

    /// <summary>
    /// One roll at noticing a secret door: <c>Int(Rnd * 100) &lt; (fn122A(char, 5, False) / 5)</c>
    /// (MAZEMOD.BAS:1593). Perception is ability 5, and only a fifth of it counts here.
    /// </summary>
    public static bool NoticesSecretDoor(Character c, Random rng, Func<short, Item?>? gear = null) =>
        rng.Next(100) < Rules.BestAbilityValue(c, 5, gear) / 5;

    /// <summary>
    /// Write down what the walker can see from the square they have just stepped onto, and from
    /// there onto the two squares whose sides they are looking at. Does nothing while they are
    /// lost, because the original only records when neither lost flag is set (MAZEMOD.BAS:1580).
    /// </summary>
    public static void OnEntry(SharedMap map, Floor fl, Character c, int x, int y,
                               Random rng, Func<short, Item?>? gear = null)
    {
        if (c.LostPosition || c.LostLevel) return;
        int level = fl.LevelNum, w = fl.Width;

        // The fog that matters is the fog on the square you are standing on, for your own square
        // and for both neighbours alike: l027E is read once, from squareWalls(squareIndex(char))
        // (MAZEMOD.BAS:1570), and the two neighbour writes test that same l027E (MAZEMOD.BAS:1646,
        // 1671), never the neighbour's own word in l027A. The port had tested the neighbour's fog,
        // so standing in the clear beside a foggy square lost your west or south wall, and
        // standing in fog beside a clear one recorded it.
        bool fogHere = fl.Has(x, y, Floor.FOG);

        Square(map, fl, c, level, w, x, y, rng, gear, fogHere, isOwn: true);

        // the east side of the square to the west, and the north side of the one to the south
        if (x > 0) Square(map, fl, c, level, w, x - 1, y, rng, gear, fogHere, isOwn: false,
                          only: new[] { Floor.WALL_EAST, Floor.DOOR_EAST }, secret: Floor.SECRET_DOOR_EAST);
        if (y > 0) Square(map, fl, c, level, w, x, y - 1, rng, gear, fogHere, isOwn: false,
                          only: new[] { Floor.WALL_NORTH, Floor.DOOR_NORTH }, secret: Floor.SECRET_DOOR_NORTH);
    }

    /// <summary>
    /// Walking into a wall while standing in fog - the one time a blocked step writes to the map.
    /// The blocked branch of <c>fn1521</c> tests <c>testBit(mc0616, squareWalls(squareIndex(char)))</c>
    /// with mc0616 = 20, the fog bit (MAINMOD2.BAS:3864, 219), and only then sets the wall on the
    /// edge you walked into: the north bit (mc0602 = 1) facing north or south, the east bit
    /// (m0612 = 0) facing east or west, on your own square or on the one south or west of you
    /// (MAINMOD2.BAS:3865-3891). The square has to be on the map, 1-based X and Y above zero
    /// (MAINMOD2.BAS:3880), so bumping the west or south edge of the level writes nothing - those two edges are
    /// drawn from the square you stand on instead (FRMAUTOM.BAS:430-439). Out of fog nothing is
    /// written: stepping onto the square wrote every wall already (MAZEMOD.BAS:1588-1592).
    ///
    /// The original makes no lost test here, unlike the step itself, so neither does this.
    /// </summary>
    public static void OnBumpInFog(SharedMap map, Floor fl, int x, int y, Facing facing)
    {
        if (x < 0 || y < 0 || x >= fl.Width || y >= fl.Height) return;
        if (!fl.Has(x, y, Floor.FOG)) return;
        (int cx, int cy, int bit) = facing switch
        {
            Facing.North => (x, y, Floor.WALL_NORTH),
            Facing.East  => (x, y, Floor.WALL_EAST),
            Facing.South => (x, y - 1, Floor.WALL_NORTH),
            _            => (x - 1, y, Floor.WALL_EAST),
        };
        if (cx < 0 || cy < 0 || cx >= fl.Width || cy >= fl.Height) return;
        map.Record(fl.LevelNum, cx, cy, fl.Width, bit);
    }

    /// <summary>
    /// Fill in the record for a map that was drawn before there was one. Saves made while the port
    /// read the maze directly hold explored squares but no recorded flags, so without this an
    /// established map would come back blank.
    ///
    /// It writes down what the old map was entitled to show and no more: the walls, doors and seven
    /// features of every explored square, and **not** the secret doors. A player who had already
    /// found one loses that knowledge and has to find it again - which is the right way round.
    /// Giving them away to save a little re-walking would be the bug this whole change is about.
    /// </summary>
    public static void BackfillExplored(SharedMap map, Floor fl, IEnumerable<(int x, int y)> explored)
    {
        int level = fl.LevelNum, w = fl.Width;
        foreach (var (x, y) in explored)
        {
            if (x < 0 || y < 0 || x >= fl.Width || y >= fl.Height) continue;
            long truth = fl.At(x, y).Flags;

            foreach (int bit in Features)
                if ((truth & (1L << bit)) != 0) map.Record(level, x, y, w, bit);

            if ((truth & (1L << Floor.FOG)) != 0) continue;

            foreach (int bit in OwnEdges)
                if ((truth & (1L << bit)) != 0) map.Record(level, x, y, w, bit);

            // a secret door the old map was drawing is written back as the wall it should have been
            foreach (int bit in SecretEdges)
                if ((truth & (1L << bit)) != 0) map.Record(level, x, y, w, WallBehind(bit));

            if (x == fl.Width - 1) map.Record(level, x, y, w, Floor.WALL_EAST);
            if (y == fl.Height - 1) map.Record(level, x, y, w, Floor.WALL_NORTH);
        }
    }


    /// <summary>
    /// You walked through it, so now you know it is there - MAINMOD2.BAS:3765-3800. Going through a
    /// door or a secret door records that edge and clears the wall the map had in its place. This
    /// is the second way to find a secret door, and the reliable one: the perception roll on
    /// <see cref="OnEntry"/> may never come up, but a door you have opened is a door you have seen.
    /// </summary>
    public static void OnPassingThrough(SharedMap map, Floor fl, int x, int y, Facing dir)
    {
        int w = fl.Width, level = fl.LevelNum;
        (int cx, int cy, int wallBit, int doorBit, int secretBit) = dir switch
        {
            Facing.East  => (x, y, Floor.WALL_EAST, Floor.DOOR_EAST, Floor.SECRET_DOOR_EAST),
            Facing.North => (x, y, Floor.WALL_NORTH, Floor.DOOR_NORTH, Floor.SECRET_DOOR_NORTH),
            Facing.West  => (x - 1, y, Floor.WALL_EAST, Floor.DOOR_EAST, Floor.SECRET_DOOR_EAST),
            _            => (x, y - 1, Floor.WALL_NORTH, Floor.DOOR_NORTH, Floor.SECRET_DOOR_NORTH),
        };
        if (cx < 0 || cy < 0 || cx >= fl.Width || cy >= fl.Height) return;

        long truth = fl.At(cx, cy).Flags;
        bool secret = (truth & (1L << secretBit)) != 0;
        bool door = (truth & (1L << doorBit)) != 0;
        if (!secret && !door) return;

        map.Record(level, cx, cy, w, secret ? secretBit : doorBit);
        map.Unrecord(level, cx, cy, w, wallBit);
    }


    private static void Square(SharedMap map, Floor fl, Character c, int level, int w, int x, int y,
                               Random rng, Func<short, Item?>? gear, bool foggy, bool isOwn,
                               int[]? only = null, int secret = -1)
    {
        long truth = fl.At(x, y).Flags;

        if (isOwn)
        {
            // the features go down whatever the weather
            foreach (int bit in Features)
                if ((truth & (1L << bit)) != 0) map.Record(level, x, y, w, bit);

            // MAZEMOD.BAS:1618-1623 - the far edges of the grid are walls, and you can see that
            if (!foggy)
            {
                if (x == fl.Width - 1) map.Record(level, x, y, w, Floor.WALL_EAST);
                if (y == fl.Height - 1) map.Record(level, x, y, w, Floor.WALL_NORTH);
            }
        }

        if (foggy) return;   // in fog the edges are not written down at all

        foreach (int bit in only ?? OwnEdges)
            if ((truth & (1L << bit)) != 0) map.Record(level, x, y, w, bit);

        foreach (int bit in secret >= 0 ? new[] { secret } : SecretEdges)
        {
            if ((truth & (1L << bit)) == 0) continue;
            if (map.Recorded(level, x, y, w, bit)) continue;    // already found, nothing to roll for

            int wall = WallBehind(bit);
            if (NoticesSecretDoor(c, rng, gear))
            {
                map.Record(level, x, y, w, bit);
                map.Unrecord(level, x, y, w, wall);
            }
            else map.Record(level, x, y, w, wall);
        }
    }
}
