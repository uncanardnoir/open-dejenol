namespace Mordor.Core;

public enum Facing { North = 0, East = 1, South = 2, West = 3 }

public enum Edge { Open, Wall, Door, SecretDoor }

/// <summary>
/// Player position/facing within a floor, plus wall queries and movement.
/// Coordinates: x increases east, y increases NORTH (row 0 is the south edge),
/// matching the game's native layout. Each cell stores its own NORTH and EAST edges;
/// a cell's south edge is its southern neighbour's north edge, west edge is the
/// western neighbour's east edge.
/// </summary>
public sealed class DungeonNav
{
    public Floor Floor { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public Facing Facing { get; private set; }
    /// <summary>Squares the character has walked onto. The original maps only where you actually go.</summary>
    public HashSet<(int x, int y)> Explored { get; } = new();

    // Which squares are drawn small - "recently mapped" - is not the walker's business and never
    // was: medPlotMaze fills every square it plots the full size and the incremental draw fills
    // the small one, so the difference is between what the map window has already drawn and what
    // it has not. AutomapWindow keeps that; this used to keep a second, wrong copy of it.

    public DungeonNav(Floor floor, int x, int y, Facing facing = Facing.North)
    {
        Floor = floor; X = x; Y = y; Facing = facing;
        MarkExplored();
    }

    public static (int dx, int dy) Delta(Facing f) => f switch
    {
        Facing.North => (0, 1),
        Facing.East => (1, 0),
        Facing.South => (0, -1),
        _ => (-1, 0),
    };

    public static Facing TurnRight(Facing f) => (Facing)(((int)f + 1) & 3);
    public static Facing TurnLeft(Facing f) => (Facing)(((int)f + 3) & 3);
    public static Facing Opposite(Facing f) => (Facing)(((int)f + 2) & 3);

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Floor.Width && y < Floor.Height;

    /// <summary>What is on the edge leaving cell (x,y) in direction f.</summary>
    public Edge EdgeAt(int x, int y, Facing f)
    {
        if (!InBounds(x, y)) return Edge.Wall;
        switch (f)
        {
            case Facing.North:
                return Classify(Floor.At(x, y).Flags, Floor.WALL_NORTH, Floor.DOOR_NORTH, Floor.SECRET_DOOR_NORTH);
            case Facing.East:
                return Classify(Floor.At(x, y).Flags, Floor.WALL_EAST, Floor.DOOR_EAST, Floor.SECRET_DOOR_EAST);
            case Facing.South:
                return InBounds(x, y - 1)
                    ? Classify(Floor.At(x, y - 1).Flags, Floor.WALL_NORTH, Floor.DOOR_NORTH, Floor.SECRET_DOOR_NORTH)
                    : Edge.Wall;
            default: // West
                return InBounds(x - 1, y)
                    ? Classify(Floor.At(x - 1, y).Flags, Floor.WALL_EAST, Floor.DOOR_EAST, Floor.SECRET_DOOR_EAST)
                    : Edge.Wall;
        }
    }

    private static Edge Classify(long flags, int wallBit, int doorBit, int secretBit)
    {
        if ((flags & (1L << wallBit)) != 0) return Edge.Wall;
        if ((flags & (1L << doorBit)) != 0) return Edge.Door;
        if ((flags & (1L << secretBit)) != 0) return Edge.SecretDoor;
        return Edge.Open;
    }

    public bool IsRock(int x, int y) => !InBounds(x, y) || Floor.Has(x, y, Floor.ROCK);

    public bool CanMove(Facing f)
    {
        var e = EdgeAt(X, Y, f);
        if (e == Edge.Wall) return false;
        var (dx, dy) = Delta(f);
        return !IsRock(X + dx, Y + dy);
    }

    public bool MoveForward()
    {
        if (!CanMove(Facing)) return false;
        var (dx, dy) = Delta(Facing);
        X += dx; Y += dy;
        MarkExplored();
        return true;
    }

    public bool MoveBackward()
    {
        var back = Opposite(Facing);
        if (!CanMove(back)) return false;
        var (dx, dy) = Delta(back);
        X += dx; Y += dy;
        MarkExplored();
        return true;
    }

    public void TurnLeft() => Facing = TurnLeft(Facing);
    public void TurnRight() => Facing = TurnRight(Facing);
    public void TurnAround() => Facing = Opposite(Facing);

    /// <summary>
    /// Place the player (stairs, chutes, teleporters, spells, a loaded save) exactly where asked.
    /// The original writes Chars().X and Y and nothing moves you off rock: a Teleport or an Ethereal
    /// Portal into rock leaves the party dead on that square (MAINMOD2.BAS:5860-5896, 1180-1206),
    /// and a body saved there is loaded there. Neither of those rock branches sets the "been here"
    /// bit - only the rock bit (MAINMOD2.BAS:5870-5872, 1195-1197) - so a rock square is not
    /// marked explored.
    /// </summary>
    public void SetPosition(int x, int y)
    {
        X = x; Y = y;
        if (!IsRock(x, y)) MarkExplored();
    }

    /// <summary>
    /// Ethereal Portal's step (MAINMOD2.BAS:1170-1184): one square the way you face, whatever is in
    /// the way, wrapping round at the map edge (1180-1183). The caster is moved first and the square
    /// tested after, so true - rock, and the party dies - leaves you standing on the rock square.
    /// </summary>
    public bool StepThrough()
    {
        var (dx, dy) = Delta(Facing);
        int nx = X + dx, ny = Y + dy;
        if (nx >= Floor.Width) nx = 0;
        if (nx < 0) nx = Floor.Width - 1;
        if (ny >= Floor.Height) ny = 0;
        if (ny < 0) ny = Floor.Height - 1;
        SetPosition(nx, ny);
        return IsRock(nx, ny);
    }

    /// <summary>
    /// Map the square you are standing on, and nothing else: "The map for a level is automatically
    /// updated as a character walks into unmapped areas" (the original help's AutoMap page).
    /// Whether its features are recorded too is decided by the caller's perception check.
    /// </summary>
    private void MarkExplored()
    {
        if (!Mapping) return;              // you cannot map ground you do not know you are on
        Explored.Add((X, Y));
    }

    /// <summary>
    /// False while the walker is lost. MODULE3.BAS:1574 only sets the flag that writes the square
    /// into the map when neither the position nor the level flag is set.
    /// </summary>
    public bool Mapping { get; set; } = true;

    /// <summary>
    /// Face a given way outright. The four facing squares set <c>M5899</c> to a fixed value and
    /// the rotator sets it to <c>Int(Rnd * 4) + 1</c>, so both are absolute, not a count of turns.
    /// </summary>
    public void FaceTowards(Facing f) { while (Facing != f) TurnRight(); }

    /// <summary>Has this square been mapped before? <c>fn0A34</c> reads exactly this.</summary>
    public bool HasMapped(int x, int y) => Explored.Contains((x, y));

    /// <summary>
    /// Map the square underfoot now. The original works out whether you are still lost *before* it
    /// writes the square into the map (MODULE3.BAS:1541 then 1574), so the step on which you
    /// realise where you are is the step that gets recorded.
    /// </summary>
    public void MapHere() => MarkExplored();

    /// <summary>
    /// What the square announces, which is <c>lblSquareInfo</c> - see <see cref="SquareInfo"/>
    /// for the whole of what that label can say and why water is not in it. This used to build a
    /// sentence of the port's own, "You see stairs up, water.", out of five flags the original
    /// never lists together.
    /// </summary>
    public string Describe() => SquareInfo.Of(Floor, X, Y);

}
