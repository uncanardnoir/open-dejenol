using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// The character window's Char page (<c>mdrcharShowInfo</c> case <c>mc0598</c>, MAINMOD2.BAS:5224-5334)
/// and Shift-F6: "Outputs current character stats, statistics and information to CHARSTAT.TXT (in
/// the Root Mordor directory)" - the help's Miscellaneous Keys.
///
/// The original paints the page one piece at a time with <c>sub09B8</c>: a row, a column, the text
/// and whether it is bold. Shift-F6 does not write a report of its own - it raises a module flag and
/// paints the same page, and <c>sub09B8</c> (MAINMOD2.BAS:3246-3279) appends every piece it is handed
/// to the file while that flag is up, a Tab in front of anything not bold. Pieces given row and
/// column -1 are never drawn, so they reach the file alone: the header, the item lists, "Total A/D",
/// the guild list and the abilities. <see cref="Paint"/> is that sequence; the window draws the
/// pieces that have a place and <see cref="Compose"/> is what the file receives. The file is
/// appended to, so asking twice gives two copies.
///
/// A fault kept: the ability loop stores each chance in <c>p06E4</c>, the parameter that said
/// "write the file", and the footer is guarded by <c>If p06E4 = True</c> - so the blank line,
/// "***** END OF CHARACTER *****" and "BETA: Finished writing Character Statistics..." appear only
/// if the last ability chance happens to be -1, which is to say never. What the player sees is the
/// opening "BETA: Writing Character Statistics to File..." (5231).
/// </summary>
public static class CharStatFile
{
    /// <summary>One <c>sub09B8</c> call: row and column as passed (-1 for the file only), text, and its style - bold (<c>mc052A</c>) or italic (<c>mc052C</c>).</summary>
    public readonly record struct Piece(int Row, int Col, string Text, bool Bold, bool Italic = false)
    {
        public bool OnScreen => Row != -1 && Col != -1;
    }

    /// <summary><c>gc0186</c>, the version the data files and the help title carry.</summary>
    public const string Version = "1.1";

    /// <summary>The file the original writes, in the game's own directory rather than the saves.</summary>
    public const string FileName = "CHARSTAT.TXT";

    /// <summary>
    /// Appends this character's page to the first writable of <see cref="PortFiles.CharStatPlaces(string)"/>:
    /// beside the saves folder for a setup that keeps them there (where the port always put it),
    /// else the game folder as the original, else the per-user folder. Returns the file written,
    /// or null if none could be.
    /// </summary>
    public static string? Write(Character c, string gameDir, Func<short, Item?> item) =>
        Write(c, PortFiles.CharStatPlaces(gameDir), item);

    /// <summary><see cref="Write(Character, string, Func{short, Item?})"/> with its places given.</summary>
    public static string? Write(Character c, IEnumerable<string> places, Func<short, Item?> item)
    {
        try { return PortFiles.Append(places, Compose(Paint(c, item, toFile: true, DateTime.Now))); }
        catch { return null; }
    }

    /// <summary>What <c>sub09B8</c> appends: <c>Print #</c> of each piece, Tab first unless bold.</summary>
    public static string Compose(IEnumerable<Piece> pieces)
    {
        var s = new System.Text.StringBuilder();
        foreach (var p in pieces) s.Append(p.Bold ? "" : "\t").Append(p.Text).Append("\r\n");
        return s.ToString();
    }

    /// <summary>VB's <c>Str$</c>: a leading space for anything not negative.</summary>
    private static string Str(long n) => n < 0 ? n.ToString(CultureInfo.InvariantCulture) : " " + n.ToString(CultureInfo.InvariantCulture);

    /// <summary><c>formatNumber(x, 0)</c> (MODULE1.BAS:194-213): "###,###,###,###,###", and "0" for nought.</summary>
    public static string FormatNumber(long n) => n == 0 ? "0" : n.ToString("###,###,###,###,###", CultureInfo.CurrentCulture);

    /// <summary>
    /// <c>formatNumber</c> of a Currency with a fraction, which is how every gold figure in the
    /// original is shown (its parameter is Currency, and the callers wrap the figure in
    /// <c>CCur</c>). The format has no decimal places, so no fraction is ever seen: Format$ rounds
    /// to a whole number, and unlike <c>CLng</c> it rounds a half away from zero - 10.5 shows as
    /// "11", 2.5 as "3". A value that is not nought but rounds to nothing gives "" for a positive
    /// one, which the function turns into "0", and keeps the sign for a negative one: Format$
    /// writes -0.4 as "-", as it writes <c>Format$(-.001, "0.00")</c> as "-0.00".
    /// </summary>
    public static string FormatNumber(VbCurrency v)
    {
        if (v.IsWhole) return FormatNumber(v.Whole);
        decimal m = Math.Round(Math.Abs(v.ToDecimal()), 0, MidpointRounding.AwayFromZero);
        string s = (v < 0 ? "-" : "") + (m == 0 ? "" : m.ToString("###,###,###,###,###", CultureInfo.CurrentCulture));
        return s.Length == 0 ? "0" : s;
    }

    /// <summary>
    /// <c>CStr</c> of a Single: seven significant digits, which is how a Warrior's 42.99999 attack
    /// reads as 43 in the guild list while "Guild A/D" (<c>Int</c>) says 52.
    /// </summary>
    private static string CStrSingle(float v) => v.ToString("G7", CultureInfo.CurrentCulture);

    /// <summary>
    /// The page, piece by piece, in the order the original paints it. <paramref name="toFile"/> is
    /// <c>p06E4</c>: the header and the two item lists are painted only then.
    /// </summary>
    public static List<Piece> Paint(Character c, Func<short, Item?> item, bool toFile, DateTime now)
    {
        var p = new List<Piece>();
        void Put(int row, int col, string text, bool bold) => p.Add(new Piece(row, col, text, bold));

        if (toFile)
        {
            Put(-1, -1, "  VERSION: " + Version, true);
            Put(-1, -1, "CHARACTER: \"" + c.Name.TrimEnd() + "\" (" + Librarian.Alignments[(int)c.Alignment] + " " + c.Race + ")", true);
            Put(-1, -1, "", true);
            Put(-1, -1, "ITEM INFO", true);
            for (int i = 0; i < Character.PackSize; i++)
            {
                var slot = c.Pack[i];
                if (slot == null || slot.ItemId <= 0 || item(slot.ItemId) is not Item it) continue;
                Put(-1, -1, (slot.Equipped ? "* " : "  ") + Str(i) + ". " + Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges), false);
            }
            Put(-1, -1, "", true);
            Put(-1, -1, "BANK ITEM INFO", true);
            for (int i = 0; i < c.Bank.Count; i++)
            {
                var slot = c.Bank[i];
                if (slot.ItemId <= 0 || item(slot.ItemId) is not Item it) continue;
                Put(-1, -1, Str(i) + ". " + Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges), false);
            }
            Put(-1, -1, "", true);
        }

        Put(2, 1, "Kills:", true);
        Put(2, 6, FormatNumber(c.Kills), false);
        Put(3, 1, "Died:", true);
        Put(3, 6, Str(c.Deaths), false);
        Put(4, 1, "Comp:", true);
        Put(4, 6, Str(c.Complications), false);
        Put(7, 1, "Mon Quests:", true);
        Put(7, 8, Str(c.MonsterQuestsDone), false);
        Put(8, 1, "Item Quests:", true);
        Put(8, 8, Str(c.ItemQuestsDone), false);

        // mdrcharSetAD mode 0 takes the stat part out of Att/Def, leaving base + guild + gear; the
        // best guild's Att + 10 (mc0782) is the guild part and what is left is the gear. Mode 1 puts
        // the stats back, and the stats are whatever the three parts do not account for.
        var (ga, gd) = Rules.BestGuildAD(c);
        int guildAtt = Rules.BaseAD + ga, guildDef = Rules.BaseAD + gd;
        int[] withMods = Enumerable.Range(0, 6).Select(i => c.StatWithMods(i, item)).ToArray();
        int statsAD = Rules.StatsAD(withMods);
        int itemAtt = CombatEngine.WornSum(c, item, i => i.Att), itemDef = CombatEngine.WornSum(c, item, i => i.Def);
        // the stored Att/Def carry ADShift - the stat part sub0D1D took (MONSMOD.BAS:3884-3906),
        // while held or for good - and mode 0 strips the stats a second time, so "Item A/D" comes
        // out off by the shift and "Stats A/D" still reads them in full: the report's arithmetic, kept
        int held = -c.StoredADShift(item);
        int att = guildAtt + statsAD - held + itemAtt, def = guildDef + statsAD - held + itemDef;
        itemAtt = att - statsAD - guildAtt; itemDef = def - statsAD - guildDef;
        Put(12, 1, " Item A/D:", true);
        Put(12, 8, Str(itemAtt) + "/" + itemDef.ToString(CultureInfo.InvariantCulture), false);
        Put(10, 1, "Guild A/D:", true);
        Put(10, 8, Str(guildAtt) + "/" + guildDef.ToString(CultureInfo.InvariantCulture), false);
        Put(11, 1, "Stats A/D:", true);
        Put(11, 8, Str(att - itemAtt - guildAtt) + "/" + (def - itemDef - guildDef).ToString(CultureInfo.InvariantCulture), false);
        Put(-1, -1, "Total A/D:", true);
        Put(-1, -1, Str(att) + "/" + Str(def), false);

        Put(14, 1, "Time:", true);
        Put(14, 8, now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), false);   // Time$
        Put(15, 1, "Date:", true);
        Put(15, 8, now.ToString("d", CultureInfo.CurrentCulture), false);             // Date, the short date
        Put(16, 1, "Play Time:", true);
        Put(16, 8, (c.PlaySeconds / 60 / 60).ToString("###,###.00", CultureInfo.CurrentCulture) + " Hrs", false);
        Put(17, 1, "Created on:", true);
        Put(17, 8, DateTime.FromOADate(c.CreatedOn).ToString("d", CultureInfo.CurrentCulture), false);   // CVDate
        Put(-1, -1, "", true);
        Put(-1, -1, "", true);

        Put(2, 13, "Stats Nat(Mod)", true);
        var max = c.RaceDef.Max;
        for (int i = 0; i < 6; i++)
        {
            int mods = withMods[i] - c.Stats[i];
            // bold when the natural stat is at least the race maximum (M553C) plus five
            Put(3 + i, 14, RaceData.StatNamesLong[i][..3] + " " + Str(c.Stats[i]) + " (" + mods.ToString(CultureInfo.InvariantCulture) + ")",
                c.Stats[i] >= max[i] + 5);
        }

        Put(-1, -1, "", true);
        Put(-1, -1, "GUILD INFO", true);
        foreach (var g in GuildData.All)
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            var (fa, fd) = Rules.GuildADSingle(g.Name, m.Level);
            Put(-1, -1, g.Name + " (" + m.Level.ToString(CultureInfo.InvariantCulture) + ") " + CStrSingle(fa) + "/" + CStrSingle(fd), false);
        }
        Put(-1, -1, "", true);
        Put(-1, -1, "Abilities (Lead Guild)", true);
        for (int a = 0; a < Rules.AbilityNames.Length; a++)
        {
            var lead = Rules.AbilityLead(c, a, item);
            if (lead.Modifier > 0) Put(-1, -1, Rules.AbilityNames[a] + " (" + lead.Guild + ")", false);
        }
        Put(-1, -1, "", true);

        Put(10, 13, "Total Exp.", true);
        Put(11, 14, FormatNumber(c.Guilds.Sum(g => g.Experience)), false);
        Put(13, 13, "Total Gold", true);
        Put(14, 14, FormatNumber(c.Purse), false);
        // the footer, If p06E4 = True, is never reached: see the summary
        return p;
    }
}
