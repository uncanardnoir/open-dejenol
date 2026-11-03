using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.AutoCombat;

/// <summary>
/// Auto-combat: each round of an engaged fight, every member's order given by the player's own
/// ordered rules (<see cref="Rule"/>, <see cref="RuleEngine"/>) - the orders a player gives between
/// the one-second ticks by hand, and nothing a player could not give: Fight, Defend, None, a combat
/// spell of their own or an item's made the standing order, and the group aimed at. Through
/// <see cref="ModActions.CombatRound"/> (API version 14), so every order goes through the game's own
/// keys and Cast, with its checks, refusals and lines.
///
/// What it is held to, as the Kaizen mod is: it reveals nothing the player was not shown (the rules
/// read the Party window's hits and spell points and the monster list as shown - never a monster's
/// hit points, which the game does not show, nor what an unidentified group really is), it acts
/// only in an engaged fight and only at the round's tick (at most one decision per member per
/// round), and it never starts a fight. Off until ticked on the Mods page, and then "Fight by the
/// rules" is off until ticked; both read live at each round.
///
/// The rules are a list setting (<c>ModSettings.GetRows</c>, key <see cref="RulesKey"/>, in
/// settings.ini), edited in the Auto-combat window (<see cref="AutoCombatView"/>, a toolbar button).
/// The window is reached only through <see cref="Draw"/>, so loading the mod needs no Avalonia.
/// </summary>
public sealed class AutoCombatMod : IMod, IModLifecycle
{
    public const string FightByRules = "FightByRules";
    public const string LeaveInCommand = "LeaveInCommand";
    /// <summary>The list setting the rules are kept under.</summary>
    public const string RulesKey = "Rules";
    /// <summary>The window's title, and its toolbar button's.</summary>
    public const string WindowTitle = "Auto-combat";

    /// <summary>The fight the refusals below belong to; another fight forgets them.</summary>
    private object? _fight;
    /// <summary>
    /// (member, a spell's id or the pack slot itself) the game refused in this fight: not asked again
    /// until the next. A whole fight, so an "Anti-Magic area!" refusal stands even if the party steps
    /// off the square within the area; the next fight asks again.
    /// </summary>
    private readonly HashSet<(Character, object)> _refused = new();

    /// <summary>The list setting the names the monster list has shown are kept under, for the window's drop-downs.</summary>
    public const string SeenKey = "SeenNames";
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nothing to put back: the hook and the window are wired once, and called only while the mod is on.</summary>
    public void OnEnable() { }

    /// <summary>Switched off: forget this fight's refusals.</summary>
    public void OnDisable() { _fight = null; _refused.Clear(); }

    /// <summary>The rules as kept, in order; a row that makes no sense is left out (<see cref="Rule.FromCells"/>).</summary>
    public static List<Rule> Read(ModSettings s) =>
        s.GetRows(RulesKey).Select(Rule.FromCells).OfType<Rule>().ToList();

    /// <summary>The rules written back, whole, in order.</summary>
    public static void Write(ModSettings s, IEnumerable<Rule> rules) =>
        s.SetRows(RulesKey, rules.Select(r => (IReadOnlyList<string>)r.ToCells()));

    public void Load(IModContext ctx)
    {
        var s = ctx.Settings;
        void Report(string what, Exception ex) { try { ctx.Log.Error(what, ex); } catch { } }
        // no try/catch here: a throw in the round disables the mod (the host's guard), which is the
        // safe end for a mod giving combat orders
        ctx.Actions.CombatRound = round =>
        {
            // the names the list showed, for the window: only what the player was shown, kept as shown for one
            if (round.Groups.Any(g => !_seen.Contains(g.Singular.Trim())))
            {
                if (_seen.Count == 0) foreach (var row in s.GetRows(SeenKey)) if (row.Length > 0 && row[0].Trim() != "") _seen.Add(row[0].Trim());
                foreach (var g in round.Groups) _seen.Add(g.Singular.Trim());
                s.SetRows(SeenKey, _seen.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => (IReadOnlyList<string>)new[] { n }));
            }
            if (!s.Get(FightByRules)) return;
            Round(round, Read(s), s.Get(LeaveInCommand));
        };
        ctx.UI.AddWindow(WindowTitle, () => Draw.View(ctx.Game, s, Report));
        ctx.Log.Info("ready");
    }

    /// <summary>One round: decide for every member, then give each decision through the game.</summary>
    public void Round(CombatRoundContext round, IReadOnlyList<Rule> rules, bool leaveInCommand)
    {
        if (!ReferenceEquals(_fight, round.Fight)) { _fight = round.Fight; _refused.Clear(); }
        var selected = round.Party.Selected;
        var decisions = RuleEngine.Decide(rules, round, (m, k) => _refused.Contains((m, k)),
                                          leaveInCommand ? m => ReferenceEquals(m, selected) : null);
        foreach (var d in decisions) RuleEngine.Apply(d, round, (m, k) => _refused.Add((m, k)));
    }

    /// <summary>The way to <see cref="AutoCombatView"/>, typed as object and never inlined (see the class's remarks).</summary>
    private static class Draw
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object View(ModGame game, ModSettings settings, Action<string, Exception> report) =>
            AutoCombatView.Build(game, settings, report);
    }
}
