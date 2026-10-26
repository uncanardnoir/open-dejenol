namespace Mordor.Core;

/// <summary>
/// Being lost. A chute or a teleport that drops you somewhere you have never mapped leaves you
/// not knowing the floor, the square or the way you are facing; some dungeon squares spin you
/// round and take the square and the facing. While any of that is true the automap stops
/// recording and the dungeon window stops telling you where you are.
///
/// Every step you take you get three separate chances to work it out, all off your perception
/// (guild ability 5): the floor is easiest, the facing next, and piecing the whole thing together
/// hardest. Walking onto a square you have already mapped ends it outright.
///
/// All of this is MODULE3.BAS - the losses at lines 117, 1727 and 1815, the recoveries at 1541.
/// </summary>
public static class Lost
{
    /// <summary>mc0180: perception is guild ability 5.</summary>
    public const int Perception = 5;

    /// <summary>What <c>sub08E1</c> (MODULE3.BAS:49) says when you land somewhere unmapped.</summary>
    public static string Realisation(Character c) =>
        c.LostPosition && c.LostLevel ? "You realize that you're completely lost..."
                                      : "You realize that you're lost...";

    /// <summary>
    /// A square that spins you round (MODULE3.BAS:1727). You keep your sense of what floor you are
    /// on, but lose the square and the facing, unless your perception beats the roll:
    /// <c>If fn122A(char, 5, False) &lt; (Rnd * 70) + (dungeonLevel * 2)</c>.
    /// </summary>
    public static bool LoseDirection(Character c, Random rng, Func<short, Item?>? gear = null)
    {
        double perception = Rules.BestAbilityChance(c, Perception, c.DungeonLevel, 2, gear);
        if (perception >= rng.NextDouble() * 70 + c.DungeonLevel * 2) return false;
        c.LostPosition = true;
        c.LostFacing = true;
        return true;
    }

    /// <summary>
    /// The message for a failed direction save, which names the leader when there is a party
    /// behind them (MODULE3.BAS:1731).
    /// </summary>
    public static string DirectionLostText(bool inAParty) =>
        inAParty ? "The party leader lost their sense of direction!" : "You've lost your sense of direction!";

    /// <summary>
    /// One step's worth of working out where you are. Returns whatever you realised, in the
    /// original's words and its order. Nothing happens unless you are lost to begin with.
    ///
    /// The three thresholds share one shape, <c>Log(perception)² / n > Rnd * 100</c>, with n of
    /// 15 for the floor, 20 for the facing and 25 for the lot. A low perception makes the square
    /// of its log small, so the chances stay slim until you are genuinely good at this.
    /// </summary>
    public static List<string> TryToWorkItOut(Character c, Random rng) => TryToWorkItOutWith(c, rng, null);

    private static List<string> TryToWorkItOutWith(Character c, Random rng, Func<short, Item?>? gear)
    {
        var said = new List<string>();
        if (!c.IsLost) return said;

        double perception = Rules.BestAbilityChance(c, Perception, c.DungeonLevel, 2, gear);
        double weight = perception <= 0 ? 0 : Math.Log(perception) * Math.Log(perception);

        if (c.LostLevel && weight / 15 > rng.NextDouble() * 100)
        {
            c.LostLevel = false;
            said.Add("You've figured out what level you're on...");
        }
        if (c.LostFacing && weight / 20 > rng.NextDouble() * 100)
        {
            c.LostFacing = false;
            said.Add("You've figured out what direction you're facing...");
        }
        if (weight / 25 > rng.NextDouble() * 100)
        {
            c.FoundYourself();
            said.Add("You've figured out where you are!");
        }
        return said;
    }

    /// <summary>
    /// Standing on ground you have mapped before settles it (MODULE3.BAS:1565). The original only
    /// says so if you were lost; it clears the flags either way. Returns the line, or null.
    /// </summary>
    public static string? OnKnownGround(Character c)
    {
        bool wasLost = c.IsLost;
        c.FoundYourself();
        return wasLost ? "You've found yourself!" : null;
    }

    /// <summary>The version of <see cref="TryToWorkItOut(Character, Random)"/> that lets perception see your gear.</summary>
    public static List<string> TryToWorkItOut(Character c, Random rng, Func<short, Item?>? gear) =>
        TryToWorkItOutWith(c, rng, gear);
}
