using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.AutoCombat;

/// <summary>What a rule has its member do: the orders a player gives in a fight, and nothing else.</summary>
public enum RuleAction
{
    /// <summary>space: "Stop fighting, opening, etc." - the None order.</summary>
    Nothing,
    /// <summary>f: Fight.</summary>
    Fight,
    /// <summary>d: Defend (raises defence by half, MONSMOD.BAS:2688).</summary>
    Defend,
    /// <summary>A spell of the member's own made the standing order, recast each round.</summary>
    Cast,
    /// <summary>An item in the member's pack, known at All, made the order: cast each round at its level, a charge each.</summary>
    Use,
}

/// <summary>
/// One rule: for whom, when (every condition set must hold), what order, and which monster group to
/// aim at. Every condition reads only what the game shows the player: the Party window's hits and
/// spell points, and the monster list (a group's name as shown and its count where shown). A
/// condition left null (or a blank name) is not asked.
/// </summary>
public sealed class Rule
{
    /// <summary>The member it is for, by name; blank for anyone.</summary>
    public string Who { get; set; } = "";
    /// <summary>Someone living in the party is below this per cent of their hits.</summary>
    public int? PartyBelow { get; set; }
    /// <summary>Everyone living in the party is at or above this per cent of their hits (100: nobody hurt).</summary>
    public int? PartyAtLeast { get; set; }
    /// <summary>The member is below this per cent of their own hits.</summary>
    public int? SelfBelow { get; set; }
    /// <summary>The member has at least this many spell points.</summary>
    public int? SpAtLeast { get; set; }
    /// <summary>At least this many monster groups stand.</summary>
    public int? GroupsAtLeast { get; set; }
    /// <summary>At most this many monster groups stand.</summary>
    public int? GroupsAtMost { get; set; }
    /// <summary>A group the list shows by a name containing this (blank: not asked).</summary>
    public string Shown { get; set; } = "";
    public RuleAction Do { get; set; } = RuleAction.Fight;
    /// <summary>The spell (Cast) or item (Use), by name.</summary>
    public string What { get; set; } = "";
    /// <summary>
    /// The group to aim at, tried in order: a name as the list shows it (contained, case ignored), or
    /// <see cref="First"/>, <see cref="Last"/>, <see cref="Most"/>, <see cref="Fewest"/>. Empty, or
    /// nothing matching: the aim is left as it is.
    /// </summary>
    public List<string> Targets { get; set; } = new();

    public const string First = "*first", Last = "*last", Most = "*most", Fewest = "*fewest";

    public Rule Clone() => new()
    {
        Who = Who, PartyBelow = PartyBelow, PartyAtLeast = PartyAtLeast, SelfBelow = SelfBelow, SpAtLeast = SpAtLeast,
        GroupsAtLeast = GroupsAtLeast, GroupsAtMost = GroupsAtMost, Shown = Shown, Do = Do, What = What, Targets = Targets.ToList(),
    };

    /// <summary>The row's cells, in <see cref="Columns"/>' order, for <c>ModSettings.SetRows</c>.</summary>
    public string[] ToCells() => new[]
    {
        Who, N(PartyBelow), N(PartyAtLeast), N(SelfBelow), N(SpAtLeast), N(GroupsAtLeast), N(GroupsAtMost), Shown,
        Do.ToString(), What, string.Join(",", Targets),
    };

    /// <summary>The cells of a row kept in settings.ini, one per field.</summary>
    public static readonly string[] Columns =
        { "Who", "PartyBelow", "PartyAtLeast", "SelfBelow", "SpAtLeast", "GroupsAtLeast", "GroupsAtMost", "Shown", "Do", "What", "Targets" };

    /// <summary>
    /// A row read back, or null when it makes no sense (an unknown action, a number that is not one):
    /// such a row is left out rather than guessed at, so a hand-edited settings.ini can never make the
    /// mod do something its window does not show.
    /// </summary>
    public static Rule? FromCells(IReadOnlyList<string> c)
    {
        string At(int i) => i < c.Count ? c[i].Trim() : "";
        if (!Enum.TryParse<RuleAction>(At(8), ignoreCase: true, out var act) || !Enum.IsDefined(act)) return null;
        var r = new Rule { Who = At(0), Shown = At(7), Do = act, What = At(9) };
        bool ok = true;
        int? Num(int i)
        {
            string s = At(i);
            if (s == "") return null;
            if (int.TryParse(s, out int n) && n >= 0) return n;
            ok = false;
            return null;
        }
        r.PartyBelow = Num(1); r.PartyAtLeast = Num(2); r.SelfBelow = Num(3); r.SpAtLeast = Num(4);
        r.GroupsAtLeast = Num(5); r.GroupsAtMost = Num(6);
        r.Targets = SplitTargets(At(10));
        return ok ? r : null;
    }

    /// <summary>A target list as typed: comma-separated, blanks dropped.</summary>
    public static List<string> SplitTargets(string text) =>
        text.Split(',').Select(t => t.Trim()).Where(t => t != "").ToList();

    private static string N(int? n) => n?.ToString() ?? "";

    /// <summary>One line for the window: "Anyone: if SP ≥ 10 → Cast "Fire Bolt" at Kobold, *most".</summary>
    public string Describe()
    {
        var when = new List<string>();
        if (PartyBelow is int a) when.Add($"someone below {a}% hits");
        if (PartyAtLeast is int b) when.Add(b >= 100 ? "nobody hurt" : $"everyone at {b}%+ hits");
        if (SelfBelow is int s) when.Add($"own hits below {s}%");
        if (SpAtLeast is int p) when.Add($"{p}+ spell points");
        if (GroupsAtLeast is int g1) when.Add($"{g1}+ groups");
        if (GroupsAtMost is int g2) when.Add($"at most {g2} group{(g2 == 1 ? "" : "s")}");
        if (Shown != "") when.Add($"a group shown as \"{Shown}\"");
        string act = Do switch
        {
            RuleAction.Cast => $"Cast \"{What}\"",
            RuleAction.Use => $"Use \"{What}\"",
            RuleAction.Nothing => "Nothing",
            _ => Do.ToString(),
        };
        string aim = Do is RuleAction.Fight or RuleAction.Cast or RuleAction.Use && Targets.Count > 0 ? " at " + string.Join(", ", Targets) : "";
        return $"{(Who == "" ? "Anyone" : Who)}: {(when.Count == 0 ? "always" : "if " + string.Join(", ", when))} → {act}{aim}";
    }
}

/// <summary>What one rule decided for one member this round.</summary>
/// <param name="Member">Whose order.</param>
/// <param name="Rule">The rule's place in the list, from 0.</param>
/// <param name="Do">The order.</param>
/// <param name="Spell">The spell for Cast (or the item's spell for Use).</param>
/// <param name="Slot">The pack slot for Use.</param>
/// <param name="Aim">The group slot to aim at, or null to leave the aim.</param>
public sealed record Decision(Character Member, int Rule, RuleAction Do, Spell? Spell, InventorySlot? Slot, int? Aim);

/// <summary>
/// The rules applied to a round: per member, the first rule whose conditions hold and whose order
/// the member can give now wins (<see cref="Decide"/>); then that order is given through the game's
/// own commands, and only when it differs from the order standing (<see cref="Apply"/>), so a rule
/// that keeps holding sends nothing more and a refusal is not repeated every second.
/// </summary>
public static class RuleEngine
{
    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    /// <summary>
    /// Whether a group answers to a name: the name's words, whole, in the group's shown name for one
    /// (<see cref="MonsterGroupView.Singular"/>) or as shown now - so "Rat" is a Giant Rat and Giant Rats,
    /// and "Orc" is not a Sorcerer. Case ignored.
    /// </summary>
    public static bool Answers(MonsterGroupView g, string name)
    {
        string n = name.Trim();
        if (n == "") return false;
        var word = new System.Text.RegularExpressions.Regex(@"(?<![\p{L}\p{N}])" + System.Text.RegularExpressions.Regex.Escape(n) + @"(?![\p{L}\p{N}])",
                                                            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return word.IsMatch(g.Singular) || word.IsMatch(g.Name);
    }

    /// <summary>Below <paramref name="pct"/> per cent of their hits, as the Party window's figures read.</summary>
    private static bool Below(Character c, int pct) => c.MaxHits > 0 && (long)c.CurHits * 100 < (long)pct * c.MaxHits;

    /// <summary>Whether every condition the rule sets holds for <paramref name="m"/> now.</summary>
    public static bool Holds(Rule r, Character m, IReadOnlyList<Character> party, IReadOnlyList<MonsterGroupView> groups)
    {
        if (r.Who != "" && !Same(r.Who, m.Name)) return false;
        var living = party.Where(c => !c.IsDead).ToList();
        if (r.PartyBelow is int a && !living.Any(c => Below(c, a))) return false;
        if (r.PartyAtLeast is int b && living.Any(c => Below(c, b))) return false;
        if (r.SelfBelow is int s && !Below(m, s)) return false;
        if (r.SpAtLeast is int p && m.CurSpellPoints < p) return false;
        if (r.GroupsAtLeast is int g1 && groups.Count < g1) return false;
        if (r.GroupsAtMost is int g2 && groups.Count > g2) return false;
        if (r.Shown != "" && !groups.Any(g => Answers(g, r.Shown))) return false;
        return true;
    }

    /// <summary>
    /// The group a rule aims at: its targets tried in order against the list as shown. A name
    /// matches the first group that answers to it (<see cref="Answers"/>) - so an unidentified group answers only
    /// to its class ("Humanoid"), an invisible one only to "Unseen". <see cref="Rule.Most"/> and
    /// <see cref="Rule.Fewest"/> go by the count the list shows, a group shown with none counting
    /// as one, the first on a tie. Null when nothing matches.
    /// </summary>
    public static int? AimFor(Rule r, IReadOnlyList<MonsterGroupView> groups)
    {
        if (groups.Count == 0) return null;
        foreach (string t in r.Targets)
        {
            MonsterGroupView? g = t.ToLowerInvariant() switch
            {
                Rule.First => groups[0],
                Rule.Last => groups[^1],
                Rule.Most => groups.OrderByDescending(x => x.Count ?? 1).First(),
                Rule.Fewest => groups.OrderBy(x => x.Count ?? 1).First(),
                _ => groups.FirstOrDefault(x => Answers(x, t)),
            };
            if (g != null) return g.Slot;
        }
        return null;
    }

    /// <summary>
    /// The spell a Cast rule would make <paramref name="m"/>'s order, or null when it cannot be one
    /// now: not a spell they know (the spell page's list), a heal or a cure (non-combat: the game
    /// refuses it in an engaged fight, MAINMOD2.BAS:512-519), more points than they have (the
    /// spell page's cost), or refused earlier in this fight.
    /// </summary>
    public static Spell? SpellFor(Rule r, Character m, CombatRoundContext ctx, Func<Character, object, bool> refused)
    {
        var s = ctx.KnownSpells(m).FirstOrDefault(x => Same(x.Name, r.What));
        if (s == null || CastGate.UseOf(s) == SpellUse.NonCombat) return null;
        if (Spellbook.Cost(s, m) > m.CurSpellPoints) return null;
        return refused(m, s.Id) ? null : s;
    }

    /// <summary>
    /// The pack slot a Use rule would make the order: the first item in the pack known at All by
    /// that name (below All the game shows nothing of its spell, so it is not looked at), holding a
    /// spell the game casts in a fight, not refused earlier in this fight. Its spell with it.
    /// </summary>
    public static (InventorySlot Slot, Spell Spell)? ItemFor(Rule r, Character m, CombatRoundContext ctx, Func<Character, object, bool> refused)
    {
        for (int i = 1; i < Character.PackSize; i++)
        {
            if (m.SlotEmpty(i) || m.Pack[i] is not InventorySlot slot || slot.Known != IdLevel.All) continue;
            if (ctx.Item(slot.ItemId) is not Item it || !Same(it.Name, r.What)) continue;
            if (ItemUse.SpellOf(it, ctx.Spells) is not Spell s || CastGate.UseOf(s) == SpellUse.NonCombat) continue;
            if (refused(m, slot)) continue;
            return (slot, s);
        }
        return null;
    }

    /// <summary>
    /// Each living, able member's decision this round: the first rule that holds and whose order
    /// can be given. A member no rule fits is left with the order they have. At most one decision
    /// per member. A member opening a chest (o: the Option column's "Open") is left to it - any order
    /// would call the count off, as the keys do. <paramref name="skip"/> leaves a member out (the one
    /// in command, when asked).
    /// </summary>
    public static List<Decision> Decide(IReadOnlyList<Rule> rules, CombatRoundContext ctx, Func<Character, object, bool> refused,
                                        Func<Character, bool>? skip = null)
    {
        var made = new List<Decision>();
        if (ctx.Groups.Count == 0) return made;
        foreach (var m in ctx.Members.ToList())
        {
            // the dead do nothing, and the held fast do nothing in the round (sub1019, MONSMOD.BAS:291)
            if (m.IsDead || m.Paralysed || (skip?.Invoke(m) ?? false) || ctx.OrderOf(m).OpeningChest) continue;
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (!Holds(r, m, ctx.Members, ctx.Groups)) continue;
                Decision? d = r.Do switch
                {
                    RuleAction.Cast => SpellFor(r, m, ctx, refused) is Spell s ? new Decision(m, i, r.Do, s, null, AimFor(r, ctx.Groups)) : null,
                    RuleAction.Use => ItemFor(r, m, ctx, refused) is var (slot, sp) ? new Decision(m, i, r.Do, sp, slot, AimFor(r, ctx.Groups)) : null,
                    RuleAction.Fight => new Decision(m, i, r.Do, null, null, AimFor(r, ctx.Groups)),
                    _ => new Decision(m, i, r.Do, null, null, null),
                };
                if (d == null) continue;
                made.Add(d);
                break;
            }
        }
        return made;
    }

    /// <summary>
    /// Give one decision through the game's commands: the aim first (a number key), then the order,
    /// each only when it differs from what stands. A Cast or Use the game refuses is reported to
    /// <paramref name="refuse"/> so it is not asked again this fight. Returns whether anything was sent.
    /// </summary>
    public static bool Apply(Decision d, CombatRoundContext ctx, Action<Character, object> refuse)
    {
        var m = d.Member;
        bool sent = false;
        if (d.Aim is int aim && ctx.AimedAt(m) != aim) sent |= ctx.Aim(m, aim);
        var now = ctx.OrderOf(m);
        switch (d.Do)
        {
            case RuleAction.Cast when d.Spell is Spell spell:
                if (now.Kind == OrderKind.Spell && now.SpellId == spell.Id && now.ItemSlot == null) break;
                sent = true;
                if (!ctx.Cast(m, spell)) refuse(m, spell.Id);
                break;
            case RuleAction.Cast:
                break;
            case RuleAction.Use when d.Slot is InventorySlot slot:
                if (ReferenceEquals(now.ItemSlot, slot)) break;
                sent = true;
                if (!ctx.UseItem(m, slot)) refuse(m, slot);
                break;
            case RuleAction.Use:
                break;
            default:
                var kind = d.Do switch { RuleAction.Fight => OrderKind.Fight, RuleAction.Defend => OrderKind.Defend, _ => OrderKind.None };
                if (now.Kind == kind) break;
                sent |= ctx.Order(m, kind);
                break;
        }
        return sent;
    }
}
