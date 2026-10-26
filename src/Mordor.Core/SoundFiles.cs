namespace Mordor.Core;

/// <summary>
/// <c>SoundFiles(0 To 71)</c>: the game's sound numbers and the WAV each one names, as MODULE1.BAS
/// fills the table (308-379) from the constants declared just above it (227-297). Index is the sound
/// number <c>sub09DE</c> and <c>sub086D</c> are handed; the name is the file's, without ".WAV".
/// </summary>
public static class SoundFiles
{
    public static readonly string[] Names =
    {
        "FIGHT", "DIE", "HIT", "KILL", "MAGIC", "OPEN", "FALL", "BLACKOUT",            //  0-7
        "SCURRY", "MOAN", "DSTSLAM", "RAISE", "STEAL", "BUG", "SLIME", "GOBLIN",        //  8-15
        "DOG", "DEVIL", "SNAKE", "UNDEAD", "WATERD", "GIANT", "MYTHIC", "LYCAN",        // 16-23
        "MAGE", "WARRIOR", "WATER", "WATER1", "FIRE", "TELEPORT", "DI-DEMON", "DI-HUMAN", // 24-31
        "DI-ANIM", "SHOCK", "BYE", "OUCH", "SWING", "PARAL", "POISON", "DISEASE",       // 32-39
        "BETTER", "FOUND", "COLD", "EXPLOS", "MAGIC2", "WADE", "SLICE", "DRAIN",        // 40-47
        "FEAR", "SLIMETR", "SPIT", "STONE", "WITHER", "MAKELEV", "BUGDIE", "RIP",       // 48-55
        "HELP", "DEMON", "DRAGON", "BIRD", "BIRDDIE", "EXTING", "HELLO", "YEECH",       // 56-63
        "DI-UNDEA", "FOG", "MESSAGE", "DROWN", "ASMO", "PLANTATT", "BSTAB", "CHIT",     // 64-71
    };

    /// <summary>The WAV sound number <paramref name="n"/> plays.</summary>
    public static string Name(int n) => Names[n];
}

/// <summary>
/// <c>sub1005</c> (MUSIC.BAS:171-196), the sound of a spell. It goes by the spell's
/// <c>SpellDefs.Kind</c> - MDATA2's Category - and nothing else but its level:
///
/// <code>
/// Select Case SpellDefs(p).Kind
///     Case 0: sub09DE 28     FIRE
///     Case 2: sub09DE 33     SHOCK
///     Case 1: sub09DE 42     COLD
///     Case 9: sub09DE 44     MAGIC2
///     Case Else: sub09DE 4   MAGIC
/// End Select
/// Kind 0, 2 or 1 and SpellDefs(p).Level >= 7: sub09DE 43   EXPLOS as well
/// </code>
///
/// The first case is <c>m0148</c>, a Static the routine never assigns, so it is 0. The numbers are
/// gc203C, gc2046, gc2058, gc205C, gc200C and gc205A (MAIN.TXT:766-788). The port had a school table
/// of its own - POISON for Poison, DRAIN for Death, BETTER for Heal, TELEPORT for Movement - which
/// no line of the original has. The "sounds off" test at its top is the App's Audio's,
/// the same test sub09DE makes.
/// </summary>
public static class SpellSounds
{
    public const int Fire = 28, Shock = 33, Cold = 42, Magic2 = 44, Magic = 4, Explos = 43;

    /// <summary>The sound numbers sub1005 plays for a spell, in order.</summary>
    public static int[] Numbers(int kind, int level)
    {
        int first = kind switch { 0 => Fire, 2 => Shock, 1 => Cold, 9 => Magic2, _ => Magic };
        return kind is 0 or 2 or 1 && level >= 7 ? new[] { first, Explos } : new[] { first };
    }

    /// <summary>The WAVs sub1005 plays for a spell, in order.</summary>
    public static string[] For(Spell s) => Numbers(s.Category, s.Level).Select(SoundFiles.Name).ToArray();
}
