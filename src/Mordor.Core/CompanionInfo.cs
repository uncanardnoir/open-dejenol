namespace Mordor.Core;

/// <summary>
/// The companion page of the character window's Misc tab, page 8
/// (MAINMOD2.BAS:4934-5039, <c>mdrcharShowInfo</c> case <c>mc01E2</c>). The help calls it Companion Information: "Depending on the ID Level of
/// the character (based on Intelligence, Wisdom and Guild Level), the information displayed
/// includes ... The Name, Alignment and A/D of the companion. Also showed are the Stats (in the
/// same order as the Item Information stats), the ID Level and Special Abilities (if any)."
///
/// The ID level gates it in three steps, and the original's numbers run the other way round from
/// how they read: <b>0 is knowing everything</b> and larger is knowing less.
///
/// <code>
/// idLevel &lt;= 2   Type
/// idLevel &lt;= 1   Alignment, A/D, Average Hits, Stats
/// idLevel == 0   Special abilities
/// </code>
///
/// and the line at the bottom: 0 "You know All", 1 "More to learn", 2 "Lots more to learn",
/// anything else "You know Nothing".
/// </summary>
public static class CompanionInfo
{
    /// <summary>m0372, mc0378, mc0376: the three grades the page tests against.</summary>
    public const int KnowAll = 0, MoreToLearn = 1, LotsMoreToLearn = 2;

    /// <summary>The bottom line, <c>Select Case l06F0</c> at MAINMOD2.BAS:5029-5038.</summary>
    public static string IdLevelText(int idLevel) => idLevel switch
    {
        KnowAll => "You know All",
        MoreToLearn => "More to learn",
        LotsMoreToLearn => "Lots more to learn",
        _ => "You know Nothing",
    };

    /// <summary>
    /// The page as <c>sub09B8</c> pieces (MAINMOD2.BAS:4934-5039). <paramref name="index"/> is the slot,
    /// or -1 for a creature in the Confinement's pens, which is shown fully known, with no binding lines,
    /// under "STORE:" and at <paramref name="storeAlignment"/> - the alignment button last pressed.
    /// A companion's A/D is its breed's: every place the original makes one copies the monster's.
    /// </summary>
    public static List<CharStatFile.Piece> Paint(Companion? c, int index, Monster def, Character owner,
                                                 Func<short, Item?> item, Alignment storeAlignment = Alignment.Good)
    {
        var p = new List<CharStatFile.Piece>();
        void Put(int row, int col, string text, bool bold = false, bool italic = false) =>
            p.Add(new CharStatFile.Piece(row, col, text, bold, italic));
        string N(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool owned = index > -1 && c != null;
        int id = owned ? c!.IdLevel : KnowAll;

        string head = owned
            ? "Comp. #" + N(index + 1) + " " + (c!.Name.Trim() != "" ? "\"" + c.Name.TrimEnd() + "\" " : "") + "(" + def.Name + ")"
            : "STORE: " + def.Name;   // Monsters().Name, not RTrim'd
        Put(2, 1, head, bold: true);
        Put(4, 1, "Size:", bold: true);
        Put(4, 8, Librarian.Sizes.ElementAtOrDefault(def.Size) ?? "");
        if (id <= LotsMoreToLearn)
        {
            Put(5, 1, "Type:", bold: true);
            Put(5, 8, Mdata1.MonsterGroupName(def.Type, ""));
        }
        if (id <= MoreToLearn)
        {
            int align = owned ? AlignmentOf(c!, def) : (int)storeAlignment;
            Put(6, 1, "Align:", bold: true);
            Put(6, 8, Librarian.Alignments.ElementAtOrDefault(align) ?? "");
            Put(7, 1, "A/D:", bold: true);
            Put(7, 8, N(def.Att) + "/" + N(def.Def));
            Put(8, 1, "Avg. Hits:", bold: true);
            Put(8, 8, N(def.Hits));
        }
        if (owned)
        {
            Put(10, 1, c!.BindingLevel < 0 ? "Content Level:" : "Bind Level:", bold: true);
            Put(11, 1, N(Math.Abs(c.BindingLevel)) + " (" + Binding.Describe(c.BindingLevel, LeaveChance(c, def, owner, item)) + ")");
        }
        if (id <= MoreToLearn)
        {
            Put(13, 1, "Stats", bold: true);
            Put(14, 1, string.Join("/", def.Stats.Take(6).Select(v => N(v))));
        }
        if (id == KnowAll)
        {
            // the heading at row 13, the lines from row 14 at column 9 - over the stats string when that is wide enough, as there
            Put(13, 9, "Special", bold: true);
            int n = 0;
            for (int i = 0; i < Librarian.MonsterProperties.Length; i++)
                if (Bit(def.SpecialPropertyFlags, i)) Put(13 + ++n, 9, Librarian.MonsterProperties[i], italic: true);
            for (int i = 0; i < Librarian.MonsterAttacks.Length; i++)
                if (Bit(def.SpecialAttackFlags, i)) Put(13 + ++n, 9, "Can " + Librarian.MonsterAttacks[i], italic: true);
            for (int i = 0; i < Librarian.SpellSchools.Length; i++)
                if (Bit(def.SpellFlags, i)) Put(13 + ++n, 9, "Can Cast \"" + Librarian.SpellSchools[i] + "\" Spells", italic: true);
            for (int i = 0; i < Librarian.Resistances.Length && i < def.Resistances.Length; i++)
                if (def.Resistances[i] > 0) Put(13 + ++n, 9, "Is " + Librarian.Resistances[i] + " Resistant", italic: true);
            if (n == 0) Put(14, 9, "(None)", italic: true);
        }
        Put(16, 1, "ID Lev:", bold: true);
        Put(17, 1, IdLevelText(id));
        return p;
    }

    /// <summary>
    /// picCompanion_DblClick (FRMCHAR3.BAS:744-759): before the page, the companion's id level is
    /// brought down to what <c>fn1272</c> says the owner can tell now, and kept.
    /// </summary>
    public static void Sharpen(Character owner, Companion c, Monster def, Func<short, Item?> item)
    {
        int now = CombatEngine.CharmIdLevel(owner, def, item);
        if (now < c.IdLevel) c.IdLevel = now;
    }

    private static bool Bit(long mask, int b) => (mask & (1L << b)) != 0;

    /// <summary>The companion's own alignment, which the Confinement can change, not the breed's.</summary>
    private static int AlignmentOf(Companion c, Monster def) => c.Alignment switch
    {
        ItemAlignment.Good => (int)Alignment.Good,
        ItemAlignment.Neutral => (int)Alignment.Neutral,
        ItemAlignment.Evil => (int)Alignment.Evil,
        _ => def.AlignmentValue,
    };

    // Stats + StatMods of Charisma, as companionLeaveChance reads it (MONSMOD.BAS:2061)
    private static double LeaveChance(Companion c, Monster def, Character owner, Func<short, Item?> item) =>
        Binding.ChanceToLeave(c.BindingLevel, def.GuildLevel, owner.TopGuildLevel,
                              owner.StatWithMods((int)Stat.Cha, item), c.Hits, c.MaxHits);
}
