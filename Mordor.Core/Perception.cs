namespace Mordor.Core;

/// <summary>
/// Noticing what kind of square you are on, <c>sub087C</c> / mdrmazPerceiveSquare
/// (MAZEMOD.BAS:605-712). The help describes it: "Characters with a high perception ability are
/// usually able to detect a difference in the maze when walking around. If the character is high
/// enough level and has a good enough perception ability, they may even be able to completely
/// identify the square they are standing on."
///
/// It only ever runs for the square types you cannot simply see - anti-magic, stud, extinguisher,
/// the four facing squares, the rotator and the teleporter. A pit or water announces itself by
/// what it does to you, and those are never guessed at.
/// </summary>
public static class Perception
{
    /// <summary>The square types the original will speak up about (its <c>sub087C</c> call sites).</summary>
    public static readonly int[] Noticeable =
    {
        Floor.FACE_NORTH, Floor.FACE_EAST, Floor.FACE_SOUTH, Floor.FACE_WEST,
        Floor.EXTINGUISHER, Floor.ROTATOR, Floor.ANTIMAGIC, Floor.STUD,
    };

    /// <summary>
    /// The names the game prints, <c>gv194E</c> (MODULE1.BAS:553-575), in flag-bit order. These
    /// are its own words, down to "Right SDoor" and "Extinguish".
    /// </summary>
    public static readonly string[] Names =
    {
        "Right Wall", "Upper Wall", "Right Door", "Upper Door", "Right SDoor", "Upper SDoor",
        "Face North", "Face East", "Face South", "Face West", "Extinguish", "Pit",
        "Stairs Up", "Stairs Down", "Teleporter", "Water", "Quicksand", "Rotator",
        "Anti-Magic", "Rock", "Fog", "Chute", "Stud",
    };

    /// <summary>
    /// How sure you sound, from the perception value (MAZEMOD.BAS:667-684). Having worked it out
    /// outright - which is what mapping it means - you are "completely" certain instead.
    /// </summary>
    public static string Certainty(double perception, bool mappedIt) => mappedIt ? "completely" : (int)perception switch
    {
        <= 39 => "kind of",
        <= 49 => "fairly",
        <= 59 => "strongly",
        <= 69 => "almost very",
        <= 85 => "very",
        <= 95 => "almost absolutely",
        _ => "absolutely",
    };

    /// <summary>
    /// Whether you notice anything at all: <c>Int(Rnd * 100) + 1 &gt; perception</c> and you walk
    /// on none the wiser. A teleporter always registers, because you have just been thrown by it.
    /// </summary>
    public static bool NoticesAnything(Random rng, double perception, int bit) =>
        bit == Floor.TELEPORTER || rng.Next(100) + 1 <= perception;

    /// <summary>
    /// Whether you work out what it really is: a second roll against *half* your perception. A
    /// teleporter is again exempt. Getting this right is also what puts the square on your automap.
    /// </summary>
    public static bool IdentifiesIt(Random rng, double perception, int bit) =>
        bit == Floor.TELEPORTER || rng.Next(100) + 1 <= perception / 2;

    /// <summary>
    /// What you name it when you have got it wrong (MAZEMOD.BAS:653-661). The original draws from
    /// the twenty-three flag bits but rejects the walls and doors, the pit, both staircases, water,
    /// quicksand, rock, fog, the chute, the teleporter and the real answer - everything, in other
    /// words, that you would have noticed for yourself. What is left is the invisible kinds. The
    /// loop has no limit, as the original's has none: eight bits (6-10, 17, 18, 22) always pass,
    /// and at most one of them is the real answer, so it always ends.
    /// </summary>
    public static int WrongGuess(Random rng, int realBit)
    {
        while (true)
        {
            int bit = rng.Next(23);
            switch (bit)
            {
                case >= 0 and <= 5:
                case Floor.PIT:
                case Floor.STAIRS_UP:
                case Floor.STAIRS_DOWN:
                case Floor.WATER:
                case Floor.QUICKSAND:
                case Floor.ROCK:
                case Floor.FOG:
                case Floor.CHUTE:
                case Floor.TELEPORTER:
                    continue;
                default:
                    if (bit == realBit) continue;
                    return bit;
            }
        }
    }

    /// <summary>
    /// What it says. Below a perception of 35 you get no more than a feeling; above it you are
    /// told a square type, with however much certainty you have earned - and that type may be the
    /// wrong one (MAZEMOD.BAS:687-708).
    /// </summary>
    public static string Say(double perception, bool inParty, string who, int namedBit, string certainty)
    {
        if (perception <= 35)
            return inParty ? $"\"{who}\" senses something different here..."
                           : "You sense something different here...";

        string name = namedBit >= 0 && namedBit < Names.Length ? Names[namedBit] : "square";
        if (namedBit == Floor.TELEPORTER)
            return inParty ? $"\"{who}\" is {certainty} certain the party just hit a {name}."
                           : $"You are {certainty} certain you just hit a {name}.";

        return inParty ? $"\"{who}\" is {certainty} certain the party is standing on a {name} square."
                       : $"You are {certainty} certain you're standing on a {name} square.";
    }

    /// <summary>"You just hit a teleporter!", in place of any of the above (MAZEMOD.BAS:664-670).</summary>
    public static string HitATeleporter(bool inParty) =>
        inParty ? "The party just hit a teleporter!" : "You just hit a teleporter!";

    /// <summary>What one call of mdrmazPerceiveSquare comes to: whether the bit goes on the automap, and the line.</summary>
    public readonly record struct Outcome(bool MapIt, string? Line);

    /// <summary>
    /// One call of mdrmazPerceiveSquare for one bit, as sub0835 makes it just before that bit's
    /// effect (MAZEMOD.BAS:1708-1741, 1790), p018A False (MAZEMOD.BAS:610-712):
    /// <list type="bullet">
    /// <item>the "no perception messages" option leaves at once, teleporter or not (620);</item>
    /// <item>a failed roll against the party's best perception leaves, except for a teleporter (628-630);</item>
    /// <item>a bit already on the automap leaves, except for a teleporter (632-635);</item>
    /// <item>a roll against half of it - a teleporter always passes - names the bit, and writes it
    ///   to the automap when the leader is not lost at that moment (636-652); otherwise a wrong
    ///   guess is drawn (653-661);</item>
    /// <item>a teleporter then says only that you hit one (664-670), anything else the certainty line.</item>
    /// </list>
    /// "Completely" certain is for a bit that went on the map, not merely one named right (638-639).
    /// </summary>
    public static Outcome PerceiveSquare(Random rng, int bit, double best, string bestName, bool inParty,
                                         bool noMessages, bool alreadyMapped, bool lost)
    {
        if (noMessages) return new(false, null);
        if (!NoticesAnything(rng, best, bit)) return new(false, null);
        if (alreadyMapped && bit != Floor.TELEPORTER) return new(false, null);
        bool got = IdentifiesIt(rng, best, bit);
        bool mapIt = got && !lost;
        int named = got ? bit : WrongGuess(rng, bit);
        if (bit == Floor.TELEPORTER) return new(mapIt, HitATeleporter(inParty));
        return new(mapIt, Say(best, inParty, bestName, named, Certainty(best, mapIt)));
    }
}
