namespace Mordor.Core;

/// <summary>
/// The Prince of the Underworld, and what happens when he falls. The game singles him out by id
/// in five places (MODULE4.BAS: the sound when he appears at 1644, his item drop band at 3697,
/// two sprite choices at 4098 and 4444, and the ending at 4234). Only the first, third and last
/// are rules rather than pictures.
/// </summary>
public static class Asmodeus
{
    /// <summary>mc01C8 (MODULE4.BAS:40). MDATA5 has him at this id, with 2000 hits and 1500 attack.</summary>
    public const int MonsterId = 277;

    public static bool Is(Monster m) => m.Id == MonsterId;

    /// <summary>The WAV that announces him. Sound 68 in the original's table; ASMO.WAV on disk.</summary>
    public const string Sound = "ASMO";

    /// <summary>
    /// His chest draws from the top three floors of his own range rather than the usual half-band
    /// (MODULE4.BAS:3697): <c>low = LevelScale - 2, top = LevelScale</c>, applied after the
    /// ordinary banding and overriding it.
    /// </summary>
    public static (int low, int top) DropBand(Monster m) => (m.ItemDropLevel - 2, m.ItemDropLevel);

    /// <summary>
    /// The ending, <c>sub0C61</c> (MODULE4.BAS:4234), word for word. Every sentence has a party
    /// form and a lone form, and the original picks between them five separate times.
    /// </summary>
    public static string Ending(bool inAParty)
    {
        string s = "As the dust settles from the ferocious battle, "
                 + (inAParty ? "the party suddenly realizes " : "you suddenly realize ")
                 + "that the Prince of the Underworld was just defeated!\n\n";

        s += inAParty
            ? "As everybody regains their composure and gives thanks for their life, a member of the "
              + "party notices that the body of Asmodeus has disappeared!\n\n"
              + "It is then that the party looks around at each other with wide eyes and the "
            : "As you regain your composure and give thanks for your life, you notice that the body "
              + "of Asmodeus has disappeared!\n\n"
              + "It is then that you look around with wide eyes and the ";

        s += "realization of only one thing...\n\n"
           + "Something - or Someone - has magically taken the body!\n\n"
           + "Even though today has been victorious and will be recorded in the history books, "
           + (inAParty
                ? "the party continues on with uneasiness, wondering if there's some darker force at work..."
                : "you continue on with uneasiness, wondering if there's some darker force at work...")
           + "\n\n...is this really the last of Asmodeus?";
        return s;
    }

    /// <summary>The title on the box the ending appears in.</summary>
    public const string EndingTitle = "A dark realization...";

    /// <summary>
    /// The guild-log line each survivor gets. A lone adventurer "single handedly defeated
    /// Asmodeus"; everyone in a party "was part of the party that defeated" him.
    /// </summary>
    public static string LogLine(Character c, bool inAParty) =>
        inAParty ? GuildLog.Wrote.PartyBeatAsmodeus(c.Name) : GuildLog.Wrote.AloneBeatAsmodeus(c.Name);
}
