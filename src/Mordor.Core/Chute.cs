namespace Mordor.Core;

/// <summary>
/// Chutes, MAZEMOD.BAS. Walking onto one (sub0835, 1839-1876) takes the party a member at a time,
/// leader first:
/// <list type="bullet">
/// <item>Each member falls unless they are Levitating and float: the same roll a pit uses,
/// <c>5 + DungeonLevel / 4 &lt; Rnd * 100</c>. A member who does not fall and is the one on
/// screen is told "There is a chute here.  Select TAKE to use."</item>
/// <item>When the leader falls in a party of more than one, "Your party leader has fallen down a
/// chute.  Do you wish the party to follow them?" (Follow Leader?, Yes/No). Yes sends every later
/// member down whatever their own roll.</item>
/// <item>Each one who goes down is <see cref="Land"/>ed (sub08F5, 98-139).</item>
/// <item>If anyone fell: FALL, then HIT.</item>
/// </list>
/// TAKE on a chute square (MAINMOD2.BAS:3996-4009) lands every member, with no roll, no question
/// and no sound. Afterwards sub0908 (MULTIMOD.BAS:42-110) drops from the party anyone not on the
/// leader's square and floor: "X has disappeared from view!", dead if their hits are gone, saved
/// where they are. Nothing brings them back but going to them.
/// </summary>
public static class Chute
{
    public const string TakeHint = "There is a chute here.  Select TAKE to use.";
    public const string FollowQuestion = "Your party leader has fallen down a chute.  Do you wish the party to follow them?";
    public const string FollowTitle = "Follow Leader?";

    /// <summary>Stays up: Levitating and the roll goes their way (MAZEMOD.BAS:1846-1851).</summary>
    public static bool Floats(Character m, Random rng, int dungeonLevel) =>
        m.Levitating && Rules.HoversOverPit(rng, dungeonLevel);

    /// <summary>
    /// The damage part of sub08F5 at the floor they land on: nothing for a Levitating member,
    /// otherwise <see cref="Rules.ChuteDamage"/>, and the line - "You have fallen into a chute for N
    /// hits." for the one on screen, "\"Name\" has fallen into a chute for N hits." for another
    /// only when they are now on the leader's floor. Returns the line, or null.
    /// </summary>
    public static string? Land(Character m, bool onScreen, bool onLeadersFloor, int landedLevel, Random rng)
    {
        if (m.Levitating) return null;
        int hits = Rules.ChuteDamage(rng, landedLevel, (int)RaceData.ByName(m.Race).Size);
        m.CurHits -= hits;
        if (onScreen) return $"You have fallen into a chute for {hits} hits.";
        return onLeadersFloor ? $"\"{m.Name.TrimEnd()}\" has fallen into a chute for {hits} hits." : null;
    }

    /// <summary>sub0908's line for a member left elsewhere.</summary>
    public static string Disappeared(Character m) => $"\"{m.Name.TrimEnd()}\" has disappeared from view!";

    /// <summary>
    /// The move half of sub08F5 for a member who went down (MAZEMOD.BAS:111-118): the floor below
    /// by the record's depth (114), X and Y untouched, then fn0A0B loads that floor (115) and
    /// <c>M59BB = fn0992(squareIndex(...))</c> reads the area of the same square on it (116) - so
    /// the record sub0908's mdrcharUnload writes for someone left behind down there
    /// (MULTIMOD.BAS:71) carries the room they landed in. frmInteract's JOIN list compares exactly
    /// that, level and M59BB (FRMINTER.BAS:377-380), so a party that follows them down sees them.
    /// The port had moved the level and square but kept the area of the floor above.
    /// </summary>
    public static void MoveDown(Character m, int level, int x, int y, Floor landed)
    {
        m.DungeonLevel = level; m.X = x; m.Y = y;
        m.Area = landed.AreaIdAt(x, y);
    }
}
