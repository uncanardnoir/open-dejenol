namespace Mordor.Core;

/// <summary>
/// <c>sub09CC</c> (MAZEMOD.BAS:348-380): who else is in the area you have just come into, and who
/// is lying in it. It runs when the dungeon window opens (FRMDUNM.BAS:282) and when
/// <c>mdrmazShowMaze</c> finds the area or the level changed, or is made to reload by a teleport
/// (MAZEMOD.BAS:798, 832-844) - not on every square.
///
/// Both counts are <c>mdrmazLookAround(False, ...)</c>'s (see <see cref="LookAround"/>): every
/// character on the roster - not just your party - on the leader's floor and in the leader's
/// **area** (M547A against <c>Chars(m00C0).M59BB</c>, MAZEMOD.BAS:286-287), not on the leader's
/// square. A character left dead in the dungeon by somebody else's session is a body in your room.
/// </summary>
public static class RoomRoster
{
    /// <summary>
    /// The bodies: <c>mdrmazLookAround(False, True)</c>, the dead in the area other than the
    /// leader, less one for each member of the party carrying a character (<c>M5943 &gt;= 4</c>,
    /// MAZEMOD.BAS:354-359) - the carried body has just been put in the carrier's area
    /// (MAZEMOD.BAS:839-841), and a body over a shoulder is not a body on the floor.
    /// </summary>
    public static string? DeadBodies(int count) => count switch
    {
        <= 0 => null,
        1 => "There is a dead body here!",
        _ => "There are dead bodies here!",
    };

    /// <summary>
    /// The living: everyone in the area less the dead, less the party size (UBound(Chars) + 1,
    /// MAZEMOD.BAS:369-380) - the party is among those it counted, so what is left is strangers.
    /// A dead member of the party is counted among the dead and still subtracted here, as the
    /// original does.
    /// </summary>
    public static string? Strangers(int count) => count switch
    {
        <= 0 => null,
        1 => "Somebody else is in the room!",
        _ => "Other characters are in the room!",
    };

    /// <summary>
    /// Both lines in <c>sub09CC</c>'s order, the bodies first, from <see cref="LookAround.Walk"/>'s
    /// two counts, the number of party members carrying a character and the size of the party.
    /// </summary>
    public static List<string> Lines(int living, int dead, int carrying, int partySize)
    {
        var said = new List<string>();
        if (DeadBodies(dead - carrying) is string bodies) said.Add(bodies);
        if (Strangers(living - partySize) is string strangers) said.Add(strangers);
        return said;
    }
}
