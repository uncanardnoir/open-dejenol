using Mordor.Core;

namespace Mordor.Mod.QoL;

/// <summary>A quest mark (<see cref="QuestMark.Of"/>): the glyph, its colour, and the tooltip's line.</summary>
public readonly record struct Mark(string Glyph, (byte R, byte G, byte B) Colour, string Tip);

/// <summary>
/// "Mark active quests": the mark before a member's figure in the Party Information window's Next
/// column (or before the XP column's bar), for a guild quest in the member's current guild. The
/// state is the game's own (<see cref="Progress.QuestStateOf"/>: a quest set, and M57B2 for
/// completed) and the tooltip's line is the game's own F5 line for it (<see cref="Progress.Quest"/>,
/// MAINMOD2.BAS:5183-5207), so it tells what the guild told the character and nothing more.
/// </summary>
public static class QuestMark
{
    /// <summary>A quest set and not done: a small gold "!".</summary>
    public const string ActiveMark = "!";
    /// <summary>A monster quest done (the creature slain), not yet cleared by the next Make Level: a green tick.</summary>
    public const string DoneMark = "✓";

    public static readonly (byte R, byte G, byte B) ActiveColour = (240, 190, 50);
    public static readonly (byte R, byte G, byte B) DoneColour = (90, 210, 100);

    /// <summary>The prefix of the tooltip's quest line; what follows is the game's own F5 line.</summary>
    public const string TipPrefix = "Guild quest: ";

    /// <summary>The mark for a member; null with no quest in the current guild.</summary>
    public static Mark? Of(Character c, Func<short, Monster?> monster, Func<short, Item?> item)
    {
        var state = Progress.QuestStateOf(c);
        if (state == Progress.QuestState.None) return null;
        string tip = TipPrefix + (Progress.Quest(c, monster, item) ?? "");
        return state == Progress.QuestState.Done
            ? new Mark(DoneMark, DoneColour, tip)
            : new Mark(ActiveMark, ActiveColour, tip);
    }

    /// <summary>The Next column's text with the mark before it, as drawn: "!4.3K", "✓Pinned"; the figure alone with none.</summary>
    public static string Text(Character c, Func<short, Monster?> monster, Func<short, Item?> item) =>
        (Of(c, monster, item)?.Glyph ?? "") + XpColumn.Cell(c);
}
