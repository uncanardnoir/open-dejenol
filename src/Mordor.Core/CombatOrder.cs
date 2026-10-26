namespace Mordor.Core;

/// <summary>What a character does when the combat round comes round, if you do not interrupt.</summary>
public enum OrderKind
{
    /// <summary>gv1E66(i).Att = 0: stand there. The original's default.</summary>
    None = 0,
    /// <summary>gv1E66(i).Att = 1 (mc0150).</summary>
    Fight = 1,
    /// <summary>gv1E66(i).Att = 2 (mc0152).</summary>
    Defend = 2,
    /// <summary>gv1E66(i).M56C4 holds a spell rather than .Att holding an action.</summary>
    Spell = 3,
}

/// <summary>
/// One character's standing order in combat - the original's <c>gv1E66(i)</c>, a T68BA of
/// <c>Att</c>, <c>M56C4</c> (the queued spell, stored as id + 1000), <c>M547A</c> (which monster
/// group) and some display state.
///
/// This exists because combat is not turn-based. A timer fires once a second while you are in the
/// dungeon and every character carries out whatever order they are holding; the help calls it "the
/// default setting for the selected character when combat starts... that option will automatically
/// be executed every round unless it has been interrupted by the player".
/// </summary>
public sealed class CombatOrder
{
    public OrderKind Kind { get; set; } = OrderKind.None;

    /// <summary>The spell to cast each round when <see cref="Kind"/> is Spell.</summary>
    public short SpellId { get; set; }

    /// <summary>Which monster group to go for, or -1 for whatever is in front.</summary>
    public int Target { get; set; } = -1;

    /// <summary>The wording the original's party window uses for the Option column.</summary>
    public string Describe(Func<short, Spell?> spell) => Kind switch
    {
        OrderKind.Fight => "Fight",
        OrderKind.Defend => "Defend",
        OrderKind.Spell => spell(SpellId) is { } s ? $"\"{s.Name}\"" : "None",
        _ => "None",
    };

    // ---- the Party Options window's Combat Settings ----

    /// <summary>
    /// Take up the character's Combat Setting, which is what happens the moment monsters engage.
    /// The help: "This is the default setting for the selected character when combat starts. None,
    /// Fight, Defend, or a spell buffer can be selected."
    ///
    /// The setting lives on the character (<see cref="Character.CombatSetting"/>) because that is
    /// where the original keeps it, so it survives a save.
    /// </summary>
    public void ApplyDefault(Character c, IReadOnlyList<Spell>? spells = null)
    {
        SpellId = 0;
        Target = -1;
        switch (c.CombatSetting)
        {
            case 1: Kind = OrderKind.Fight; return;
            case 2: Kind = OrderKind.Defend; return;
            case >= 3:
                // 3 and up name a spell buffer - the help: "None, Fight, Defend, or a spell buffer
                // can be selected". A buffer holding a spell becomes a standing order to cast it
                // every round (MULTIMOD.BAS:176-185). One holding a pack slot is sub15CD on that
                // slot (174-175), the Use command, which needs the fight and the pack - the caller
                // runs it (DungeonWindow.CombatSettingItem).
                Kind = OrderKind.None;
                if (spells == null) return;
                int b = c.Buffers.ElementAtOrDefault(c.CombatSetting - 3);
                if (!SpellBuffers.IsSpell(b)) return;
                int idx = SpellBuffers.SpellIndexOf(b);
                if (idx < 0 || idx >= spells.Count) return;
                Kind = OrderKind.Spell;
                SpellId = spells[idx].Id;
                return;
            default: Kind = OrderKind.None; return;
        }
    }

    /// <summary>
    /// The four names the original puts in the Combat Settings box, plus the ten buffers the fourth
    /// one selects between. <c>gv058A$</c> is loaded with "None", "Fight", "Defend", "Spell"
    /// (MODULE1.BAS:416-419), and the encoding stores a buffer as 3 plus its number.
    /// </summary>
    public static string[] SettingNames() =>
        new[] { "None", "Fight", "Defend" }
        .Concat(Enumerable.Range(1, SpellBuffers.Count).Select(i => $"Spell - buffer {(i == 10 ? 0 : i)}"))
        .ToArray();

    /// <summary>
    /// The original clears everyone's order when a fight ends (FRMGROUP.BAS:419, guarded by
    /// <c>fn0EEF() = False</c> - no monsters left). The default survives; it is a setting, not a
    /// state.
    /// </summary>
    public void Clear() { Kind = OrderKind.None; SpellId = 0; Target = -1; }

    /// <summary>
    /// <c>CombatOrders(pv00FC).Att = False</c>, the first line of mdrcharCheckCast (MAINMOD2.BAS:433),
    /// which runs before any of its tests and whatever its last argument. Fight and Defend live in
    /// <c>Att</c> and always come with <c>M56C4 = -1</c> (MAINMOD2.BAS:3602-3604, 4030-4032,
    /// 4048-4050), so they become None; a queued spell is <c>M56C4</c> with <c>Att = False</c> already
    /// (6133-6134) and stays, and so does the group aimed at (M547A).
    /// Returns whether anything changed, for the party window's Option column.
    /// </summary>
    public bool CheckCastClearsAtt() => ClearAtt();

    /// <summary>
    /// <c>sub102F p, -1</c> (MULTIMOD.BAS:467-479): with Att at 0 or 1 and no spell in M56C4 -
    /// None or Fight here - the member goes on Defend (470-474); otherwise - a spell queued, or
    /// Defend already (Att 2) - on Fight (476-478). M56C4 and M5766 go to -1 either way (the
    /// caller lets the item slot go); the group aimed at stays.
    /// </summary>
    public void Sub102FMinusOne()
    {
        Kind = Kind is OrderKind.None or OrderKind.Fight ? OrderKind.Defend : OrderKind.Fight;
        SpellId = 0;
    }

    private bool ClearAtt()
    {
        if (Kind is not (OrderKind.Fight or OrderKind.Defend)) return false;
        Kind = OrderKind.None;
        return true;
    }
}
