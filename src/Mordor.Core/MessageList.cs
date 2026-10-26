namespace Mordor.Core;

/// <summary>
/// The Messages window's list, <c>sub085B</c> (MAINMOD2.BAS:3190-3203).
///
/// The routine adds a line, drops the oldest as soon as the count passes <c>mc04F2</c>, selects
/// the newest so that it scrolls into view, and plays MESSAGE.WAV. Double-clicking the list empties
/// it (FRMCHAR4.BAS:84-88) - the only way to clear it.
///
/// Both of this port's screens keep their own list and both used to write the 20 out by hand; it
/// lives here so the two cannot drift and so the number can be checked.
/// </summary>
public static class MessageList
{
    /// <summary>mc04F2: how many lines the list keeps.</summary>
    public const int Kept = 20;

    /// <summary>
    /// Add a line and trim, returning whether anything was added.
    ///
    /// A line the same as the one above it is added again, as <c>sub085B</c> adds it
    /// (MAINMOD2.BAS:3195-3208): a second refusal, a second drain, a second "Help!" all show. The
    /// port used to drop such a line. The one thing kept back is an empty line, which the port's
    /// callers pass to mean "nothing to say".
    /// </summary>
    /// <param name="kept">
    /// How many lines stay: <see cref="Kept"/>, the original's, unless a mod says otherwise
    /// (docs/MODDING.md). At least one.
    /// </param>
    public static bool Add(List<string> lines, string line, int kept = Kept)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        lines.Add(line);
        kept = Math.Max(1, kept);
        if (lines.Count > kept) lines.RemoveRange(0, lines.Count - kept);
        return true;
    }

    /// <summary>
    /// <see cref="Add(List{string}, string, int)"/> with each line's <see cref="MessageLook"/> kept
    /// beside it in <paramref name="looks"/>, trimmed with it, so a line's kind and colour stay its
    /// own however the list moves. Should the two lists ever differ in length, every look is
    /// dropped (all drawn plain) rather than one shifted onto another line.
    /// </summary>
    public static bool Add(List<string> lines, List<MessageLook> looks, string line, MessageLook look, int kept = Kept)
    {
        if (looks.Count != lines.Count)
        {
            looks.Clear();
            looks.AddRange(Enumerable.Repeat(MessageLook.Plain, lines.Count));
        }
        if (!Add(lines, line, kept)) return false;
        looks.Add(look);
        if (looks.Count > lines.Count) looks.RemoveRange(0, looks.Count - lines.Count);
        return true;
    }
}

/// <summary>
/// How a Messages line is drawn, decided once as it is added and kept with it: what the game said
/// it is about, and the colour a mod gave it then (null: the window's own). A line keeps its look
/// when the list scrolls, trims or is carried between the town and the dungeon; a change of a
/// mod's setting colours the lines added after it.
/// </summary>
public readonly record struct MessageLook(MessageKind Kind, (byte R, byte G, byte B)? Colour = null)
{
    /// <summary>No kind, the window's own colour.</summary>
    public static readonly MessageLook Plain = default;
}

/// <summary>
/// What a Messages line is about, said where the game makes the line - never worked out from its
/// text and never shown. The original draws every line alike; a mod may colour lines by kind
/// (docs/MODDING.md, MessageLineColour). The categories are those the Quality of Life mod's old
/// Combat Log used.
/// </summary>
public enum MessageKind
{
    /// <summary>Anything else: the window's own colour.</summary>
    None = 0,
    /// <summary>The party's side of a fight: a member's spell or other action in combat.</summary>
    Party,
    /// <summary>What a foe did to the party: blows, breath, drains, thefts, deaths, a companion's mishap.</summary>
    Foes,
    /// <summary>A chest opened: the trap, the gold and what was found.</summary>
    Chest,
    /// <summary>Monsters joining the party.</summary>
    Join,
}
