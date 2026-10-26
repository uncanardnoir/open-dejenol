namespace Mordor.Core;

/// <summary>
/// <c>lblSquareInfo</c>, the line in the Dungeon window that says what you are standing on. It
/// holds one thing at a time - MAZEMOD.BAS:1545 blanks it at the start of every square and the
/// later tests overwrite each other, so on a pit with stairs over it you are told about the
/// stairs - and everything it can ever say is here.
///
/// Water and quicksand are not in the per-square code at all. They belong to the ailment clock
/// (FRMDUNM.BAS:504-511), which writes <see cref="Drowning"/> and <see cref="Sinking"/> into the
/// same label once a tick for as long as you stand in them. The port had been saying "You wade
/// through cold water." on the step instead, which is both the wrong words and the wrong moment.
/// </summary>
public static class SquareInfo
{
    /// <summary>MAZEMOD.BAS:1780.</summary>
    public const string StairsUp = "There are Stairs Up here.";
    /// <summary>MAZEMOD.BAS:1783.</summary>
    public const string StairsDown = "There are Stairs Down here.";
    /// <summary>MAZEMOD.BAS:1863. The two spaces after the full stop are the original's.</summary>
    public const string Chute = "There is a chute here.  Select TAKE to use.";
    /// <summary>MAZEMOD.BAS:1774 - levitation carried you over.</summary>
    public const string HoveringOverPit = "You are hovering over a pit.";
    /// <summary>FRMDUNM.BAS:507, once a tick while you are in water and not levitating.</summary>
    public const string Drowning = "You are Drowning!";
    /// <summary>FRMDUNM.BAS:510, the same for quicksand.</summary>
    public const string Sinking = "You are Sinking!";

    /// <summary>
    /// The pit, MAZEMOD.BAS:1766-1769. <c>Str$</c> of a positive number carries its own leading
    /// space, which is why there is none written before it in the original and none here.
    /// </summary>
    public static string FellIntoPit(int hits) => $"You have fallen into a pit for {hits} hits.";

    /// <summary>The same when it happened to somebody else in the party.</summary>
    public static string TheyFellIntoPit(string who, int hits) =>
        "\"" + who.TrimEnd() + "\" has fallen into a pit for " + hits + " hits.";

    /// <summary>
    /// What the square itself announces when you arrive: the stairs, or a chute. Empty when there
    /// is nothing to say, because the label is blanked and left blank.
    ///
    /// The order is the original's own and it matters, because each line overwrites the last.
    /// Stairs up is written at 1780, stairs down at 1783, and the chute later still at 1863,
    /// so a square carrying both staircases reads "There are Stairs Down here." and one with a
    /// chute reads about the chute whatever else is on it.
    /// </summary>
    public static string Of(Floor fl, int x, int y)
    {
        string said = "";
        if (fl.Has(x, y, Floor.STAIRS_UP)) said = StairsUp;
        if (fl.Has(x, y, Floor.STAIRS_DOWN)) said = StairsDown;
        if (fl.Has(x, y, Floor.CHUTE)) said = Chute;
        return said;
    }
}
