namespace Mordor.Core;

/// <summary>
/// F5, "Guild Experience Information", and the toolbar button beside it. <c>sub1572</c> case 2
/// (MODULE6.BAS:5164-5203) is the odd one out among the character window's pages: it paints
/// nothing, it writes one or two lines into the Messages window with <c>sub085B</c> and stops. That
/// is also why <c>sub1560</c> skips page 2 entirely - there is no tab to switch to.
/// </summary>
public static class Progress
{
    /// <summary>Which of the toolbar's three experience readouts applies (<see cref="ToolbarNeed"/>).</summary>
    public enum NeedState
    {
        /// <summary>"You need N more experience for level L."</summary>
        Short,
        /// <summary>Enough for the next level, not yet pinned: "You need N more experience to pin."</summary>
        Ready,
        /// <summary>"You are Pinned."</summary>
        Pinned,
    }

    /// <summary>
    /// The toolbar's experience readout, <c>sub166D</c> case 9, as a state and a number: short of the
    /// next level by <c>Amount</c>; or past it and <c>Amount</c> short of the pin; or pinned (Amount 0).
    /// The toolbar writes its line from this, and a mod that shows the same thing reads it here
    /// rather than working the rule out again.
    /// </summary>
    public static (NeedState State, long Amount) ToolbarNeed(Character c)
    {
        long needed = c.ExpForNextLevel();
        if (c.Guild.Experience < needed) return (NeedState.Short, needed - c.Guild.Experience);
        long cap = c.ExperienceCap();
        return c.Guild.Experience == cap ? (NeedState.Pinned, 0) : (NeedState.Ready, cap - c.Guild.Experience);
    }

    /// <summary>
    /// What is still owed for the next level. Three wordings, and which you get depends on whether
    /// you are short of experience, pinned, or somewhere between.
    /// </summary>
    public static string NextLevel(Character c)
    {
        int next = c.Guild.Level + 1;
        long gold = c.GoldForNextLevel();
        long needed = c.ExpForNextLevel();
        if (c.Guild.Experience < needed)
            return $"You need {needed - c.Guild.Experience:N0} more experience and {gold:N0} gold for level {next}.";

        long cap = c.ExperienceCap();
        if (c.Guild.Experience == cap)
            return $"You are Pinned and need {gold:N0} gold for level {next}.";

        return $"You need {cap - c.Guild.Experience:N0} more experience to pin and {gold:N0} gold for level {next}.";
    }

    /// <summary>
    /// The quest line, if this guild has set one. Note the wording: the guild itself says "You must
    /// return one X" when it hands the quest out (FRMCTYG.BAS:559), but this readout says "return
    /// with one". Both are the original's.
    /// </summary>
    public static string? Quest(Character c, Func<short, Monster?> monster, Func<short, Item?> item)
    {
        var g = c.Guild;
        if (g.QuestType <= 0) return null;
        if (g.QuestDone) return "Your quest has been completed.";
        return g.QuestType switch
        {
            1 => $"You must defeat one {monster(g.QuestTarget)?.Name.Trim() ?? "creature"}.",
            2 => $"You must return with one {item(g.QuestTarget)?.Name.Trim() ?? "item"}.",
            _ => null,
        };
    }

    /// <summary>Where the current guild's quest stands (<see cref="QuestStateOf"/>).</summary>
    public enum QuestState
    {
        /// <summary>No quest in the current guild (M55CB 0).</summary>
        None,
        /// <summary>A quest set and not yet done: "You must defeat one X." / "You must return with one X."</summary>
        Active,
        /// <summary>
        /// "Your quest has been completed." - M57B2 set. Only a monster quest gets there, the moment
        /// the creature dies (sub0CF3, MAINMOD2.BAS:1626-1635); the next Make Level clears it with the
        /// rest of the quest (FRMCTYG.BAS:464-466). An item quest never does: it is done only when
        /// the guild takes the item at Make Level (FRMCTYG.BAS:257-265), which clears it at once,
        /// so carrying the item leaves it Active, as every readout of the game has it.
        /// </summary>
        Done,
    }

    /// <summary>
    /// The current guild's quest as the game's readouts see it - F5's lines (MAINMOD2.BAS:5183-5207)
    /// and the Guild's Info box (FRMCTYG.BAS:57-78): a quest of either kind is set, and M57B2 says it
    /// is completed. Read only; <see cref="Quest"/> is the line the game says for it.
    /// </summary>
    public static QuestState QuestStateOf(Character c)
    {
        var g = c.Guild;
        if (g.QuestType is not (1 or 2)) return QuestState.None;
        return g.QuestDone ? QuestState.Done : QuestState.Active;
    }
}
