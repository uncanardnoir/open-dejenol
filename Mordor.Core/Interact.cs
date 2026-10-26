namespace Mordor.Core;

/// <summary>
/// The lines the game says when you name another character, and when you pick one up or put one
/// down.
///
/// Naming happens through <c>frmInteract</c>, the one box that asks "Character's Name?" for every
/// command that needs one - <c>fn170D</c> hands it a tag and the box validates against that tag
/// (FRMINTER.BAS:154-250). That is where the refusals live, and it is why they are the same
/// wording whether you are joining, picking up, giving or casting Locate: one box, one list of
/// checks, switched on the tag.
///
/// The carrying lines are <c>fn1521</c>'s, case 80 for 'p' and case 76 for 'l'
/// (MAINMOD2.BAS:3654-3705).
/// </summary>
public static class Interact
{
    // ---- frmInteract's refusals, in the order it makes them ----

    /// <summary>No character of that name (FRMINTER.BAS:166).</summary>
    /// <summary>
    /// What <c>frmInteract</c> puts in the name box when the list it was going to offer turns
    /// out to be empty (FRMINTER.BAS:435-453). The box is shown anyway, greyed, with OK disabled
    /// - so the refusal reads as the answer to the question rather than an error - and which of
    /// the four sentences it holds depends on what you were asking for.
    /// </summary>
    public static string NobodyFor(string tag) => tag switch
    {
        "PICKUP" or "JOIN" => "No other characters in the area!",
        "SPELLHEAL" or "GIVE" => "No characters in the party!",
        "SPELLRAISE" => "No characters to resurrect!",
        _ => "No characters available for this selection!",
    };

    /// <summary>The companion picker's own version, FRMINTER.BAS:425.</summary>
    public const string NoBoundCompanions = "No bound companions to select!";

    /// <summary>
    /// The two ways <c>mdrcharTeleport</c> can end badly (MAINMOD2.BAS:5839-5893), and they are
    /// both fatal to the whole party.
    ///
    /// Land above the city - a destination level of zero - and you fall: the line goes up in a
    /// box headed "Teleport" and everybody dies an ordinary death. Land inside rock, or below
    /// the deepest level there is, and the square is written onto your automap as rock before
    /// everybody is killed with the cause that only Resurrect can undo.
    /// </summary>
    public static string FellFromTeleport(bool inAParty) =>
        inAParty ? "The party has teleported hundreds of feet above the city and fallen to their deaths!"
                 : "You have teleported hundreds of feet above the city and fallen to your death!";

    public const string FellFromTeleportTitle = "Teleport";

    public static string TeleportedIntoRock(Character who, bool inAParty) =>
        inAParty ? "\"" + who.Name.TrimEnd() + "\" teleported into solid rock!"
                 : "You teleported into solid rock!";

    public const string NotFound = "Not Found!";

    /// <summary>More than one name starts that way (FRMINTER.BAS:172). Lower case, as written.</summary>
    public const string Ambiguous = "ambiguous!";

    /// <summary>
    /// Naming yourself, for PICKUP, LOCATE, GIVE and JOIN (FRMINTER.BAS:192-198). Two spaces.
    /// </summary>
    public const string Yourself = "Yourself?  HA!";

    /// <summary>
    /// Not on your square (FRMINTER.BAS:199-206). The test is
    /// <c>DungeonLevel &lt;&gt; Chars(Me).DungeonLevel Or M547A &lt;&gt; Chars(Me).M59BB</c> - the
    /// level and the square index both - and it is made for every tag except LOCATE. In town both
    /// sides are the city, so everybody in town is "here".
    /// </summary>
    public const string NotHere = "Character is not here!";

    /// <summary>JOIN only (FRMINTER.BAS:208-213).</summary>
    public const string IsDead = "Character is Dead!";

    /// <summary>JOIN only, the first of its two tests for this (FRMINTER.BAS:214-221).</summary>
    public const string AlreadyInParty = "Character is already in Party!";

    /// <summary>PICKUP and SPELLRAISE (FRMINTER.BAS:224-231): you cannot carry the living.</summary>
    public const string NotDead = "Character is not Dead!";

    // ---- picking up and putting down ----

    /// <summary>
    /// "no other character's can pick you up and carry you anywhere" - the Hide option, checked
    /// before the pickup goes through (MAINMOD2.BAS:3664).
    /// </summary>
    public static string Hidden(string name) => $"\"{name.TrimEnd()}\" is Hidden!";

    public static string NowCarrying(Character who, string whom) =>
        $"\"{who.Name.TrimEnd()}\" is now carrying \"{whom.TrimEnd()}\".";

    public static string NoLongerCarrying(Character who, string whom) =>
        $"\"{who.Name.TrimEnd()}\" is no longer carrying \"{whom.TrimEnd()}\".";

    /// <summary>
    /// The same line for a companion, and the original does not put quotes round this one:
    /// <c>" is no longer carrying " + fn128B(Me, n) + "."</c> against
    /// <c>" is no longer carrying " + Chr$(34) + Name + Chr$(34) + "."</c> for a character
    /// (MAINMOD2.BAS:3677 and 3680). Reproduced, quotes and all.
    /// </summary>
    public static string NoLongerCarryingCompanion(Character who, string companion) =>
        $"\"{who.Name.TrimEnd()}\" is no longer carrying {companion.TrimEnd()}.";
}
