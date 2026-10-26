namespace Mordor.Core;

/// <summary>
/// <c>mdrmazPerceiveAhead</c> (MAZEMOD.BAS:450-600), which the port did not have at all.
///
/// <c>mdrmazShowMaze</c> runs it at the end of every redraw - so on every step and every turn -
/// and it is the other half of perception. <see cref="Perception"/> tells you about the square you
/// are standing on; this one looks at the square you are **facing** and warns you what is in it
/// before you walk in. Eight things: quicksand, both staircases, a teleporter, a chute, a pit, a
/// rotator and an extinguish square.
///
/// Three rules govern it.
///
/// - it uses the **best perception in the party**, capped at 95, and names whoever has it. The
///   loop runs over everybody, the dead included, exactly as <c>mdrmazPerceiveSquare</c>'s does
/// - it cannot see through anything. A wall, a door or a secret door on the side you are facing
///   stops it dead, and so does the edge of the map. Note that a **door** blocks it: a closed door
///   is as good as a wall for this
/// - every feature it finds is written into the automap, which is one of the two ways the rotator
///   and extinguisher icons can ever appear there (the other is <c>mdrmazPerceiveSquare</c>)
///
/// It rolls separately for each feature, every time, and never asks whether you have been told
/// before - see the note in <see cref="Look"/>.
/// </summary>
public static class PerceiveAhead
{
    /// <summary>The eight bits it tests, in the order the routine tests them.</summary>
    public static readonly int[] Features =
    {
        Floor.QUICKSAND, Floor.STAIRS_UP, Floor.STAIRS_DOWN, Floor.TELEPORTER,
        Floor.CHUTE, Floor.PIT, Floor.ROTATOR, Floor.EXTINGUISHER,
    };

    /// <summary>
    /// What it calls each thing when you are alone. The case is the original's: a Pit and a Chute
    /// and a Teleporter are capitalised and a rotator is not, and the square is an "Extinguish
    /// square" here and an "Extinguish Square" in the party form below. Both spellings are in the
    /// source, eight lines apart.
    /// </summary>
    public static string Alone(int bit) => "You detect " + bit switch
    {
        Floor.QUICKSAND => "Quicksand",
        Floor.STAIRS_UP => "Stairs Up",
        Floor.STAIRS_DOWN => "Stairs Down",
        Floor.TELEPORTER => "a Teleporter",
        Floor.CHUTE => "a Chute",
        Floor.PIT => "a Pit",
        Floor.ROTATOR => "a rotator",
        _ => "an Extinguish square",
    } + " ahead of you.";

    /// <summary>What it calls each thing when somebody else in the party spots it.</summary>
    public static string InParty(string who, int bit) =>
        "\"" + who.TrimEnd() + "\" detects " + bit switch
        {
            Floor.QUICKSAND => "Quicksand",
            Floor.STAIRS_UP => "Stairs Up",
            Floor.STAIRS_DOWN => "Stairs Down",
            Floor.TELEPORTER => "a Teleporter",
            Floor.CHUTE => "a Chute",
            Floor.PIT => "a Pit",
            Floor.ROTATOR => "a Rotator",
            _ => "an Extinguish Square",
        } + " ahead of the party.";

    /// <summary>
    /// The square you are looking into, or null when nothing can be seen that way.
    ///
    /// The four cases are written out one by one in the original (MAZEMOD.BAS:479-511) and they
    /// all do the same thing from a different side: test the edge between here and there, then
    /// step. North and east read this square's own north and east sides; south and west read the
    /// neighbour's, because a square only carries two of its four sides. Any of wall, door or
    /// secret door blocks it, and at the edge of the map there is nothing to look at.
    ///
    /// Coordinates are the port's, counting from zero.
    /// </summary>
    public static (int x, int y)? SquareAhead(Floor fl, int x, int y, Facing facing)
    {
        bool Blocked(int cx, int cy, bool northSide)
        {
            if (cx < 0 || cy < 0 || cx >= fl.Width || cy >= fl.Height) return true;
            long f = fl.At(cx, cy).Flags;
            int wall = northSide ? Floor.WALL_NORTH : Floor.WALL_EAST;
            int door = northSide ? Floor.DOOR_NORTH : Floor.DOOR_EAST;
            int secret = northSide ? Floor.SECRET_DOOR_NORTH : Floor.SECRET_DOOR_EAST;
            return (f & ((1L << wall) | (1L << door) | (1L << secret))) != 0;
        }

        int tx = x, ty = y;
        switch (facing)
        {
            case Facing.North:
                if (Blocked(x, y, northSide: true)) return null;
                ty++; break;
            case Facing.East:
                if (Blocked(x, y, northSide: false)) return null;
                tx++; break;
            case Facing.South:
                if (y <= 0 || Blocked(x, y - 1, northSide: true)) return null;
                ty--; break;
            default:
                if (x <= 0 || Blocked(x - 1, y, northSide: false)) return null;
                tx--; break;
        }
        if (tx < 0 || ty < 0 || tx >= fl.Width || ty >= fl.Height) return null;
        return (tx, ty);
    }

    /// <summary>
    /// One look ahead. Returns the lines to say, and writes whatever was found into
    /// <paramref name="map"/>.
    ///
    /// <paramref name="bestPerception"/> is the party's best, already capped at 95 by
    /// <see cref="Best"/>. Each feature gets its own <c>Rnd * 100 &lt; perception</c>, so a square
    /// with two things in it can give up one of them and keep the other.
    ///
    /// **It never asks whether you already knew.** There is no test against the automap here, the
    /// way there is in <c>mdrmazPerceiveSquare</c> - so a pit you are standing in front of is
    /// announced again on every turn that keeps you facing it, for as long as the rolls keep
    /// coming up. The one exception is quicksand, and it is not a fix: the line is held back only
    /// while you are *standing in* quicksand yourself, and the bit is still written to the map.
    /// </summary>
    public static List<string> Look(SharedMap map, Floor fl, int x, int y, Facing facing,
                                    Random rng, double bestPerception, string who, bool inParty)
    {
        var said = new List<string>();
        if (SquareAhead(fl, x, y, facing) is not (int ax, int ay)) return said;

        bool standingInQuicksand = fl.Has(x, y, Floor.QUICKSAND);

        foreach (int bit in Features)
        {
            if (!fl.Has(ax, ay, bit)) continue;
            if (!(rng.NextDouble() * 100 < bestPerception)) continue;

            // fn0ABD writes it into the automap file, found or not said
            map.Record(fl.LevelNum, ax, ay, fl.Width, bit);
            map.Note(fl.LevelNum, ax, ay, fl.Width, bit);

            if (bit == Floor.QUICKSAND && standingInQuicksand) continue;
            said.Add(inParty ? InParty(who, bit) : Alone(bit));
        }
        return said;
    }

    /// <summary>
    /// The party's best perception and whose it is, capped at 95 (MAZEMOD.BAS:466-475). The loop
    /// runs over every member, including the dead and the stoned - the original does not ask.
    /// With a gear lookup the perception case reads WIS + StatMods / 4 (MAINMOD2.BAS:1962).
    /// </summary>
    public static (double best, Character who) Best(Party party, int level, Func<short, Item?>? gear = null)
    {
        double best = 0;
        Character who = party.Members[0];
        foreach (var m in party.Members)
        {
            double p = Rules.BestAbilityChance(m, 5, level, 0, gear);
            if (p > best) { best = p; who = m; }
        }
        return (Math.Min(best, 95), who);
    }
}
