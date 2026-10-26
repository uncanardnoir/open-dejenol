using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// The character window's Misc page for an item, <c>mdrcharShowInfo</c> case <c>mc06BA</c>
/// (MAINMOD2.BAS:5040-5168), as <c>sub09B8</c> pieces. The original's id levels run the other way
/// from the port's: 0 is All, 1 Mostly, 2 Partial, 3 Barely (126-129).
///
/// <list type="bullet">
/// <item>"Item" and the name, from Mostly up; "&lt;Unknown&gt;" below. At All the alignment's tag
/// (<c>gv077E$</c>: "(g)", "(n)", "(e)") follows when the copy is aligned, then " (cursed)" for a
/// cursed item.</item>
/// <item>"Att/Def" from Mostly up, with the damage modifier in brackets at All for an item whose
/// type has no description index.</item>
/// <item>"Class": the type's name from Partial up, its description's name at Barely. The branch
/// that would say "Unknown" can never be reached. At All, " (Class Restricted)" when <c>M5742</c>
/// is True (-1) exactly, " (Not Class Restricted)" otherwise.</item>
/// <item>Stat abbreviations on row 6 and "Req" values on row 7 from Mostly up; "Mod" on row 8 at
/// All.</item>
/// <item>At All, row 10 says what the special does: "Modifies Stat(s)", "Adds 50 Spell Points",
/// "Adds 200 Spell Points" or "Subtracts Age". It is drawn even when blank.</item>
/// <item>Row 11 from Mostly up: "&lt;count&gt;-handed Item, N swings", or "Requires no hands".
/// <b>A fault kept:</b> the swings text shares its variable with row 10, which is not cleared.
/// With no swings, a one-handed stat item reads "One-handed ItemModifies Stat(s)".</item>
/// <item>At All, "Casts "X"" and the spell level it casts at.</item>
/// <item>"Allowed Guilds" at row 10, column 13, from Mostly up. Beside each guild at column 19 is
/// the level it needs (<c>Str$(fn030F)</c>, leading space and all) at All, or "?" below.</item>
/// <item>From row 14, in italics at All: the abilities the item gives, then "X Res" for each
/// resistance. With enough of them they run over "ID Lev:" (rows 20 and 21) - kept.</item>
/// <item>"ID Lev:" and the level's name; "This is a Guild-Issued Item" in bold on row 23 for special
/// types 500 to 500 + guilds - 1.</item>
/// </list>
/// </summary>
public static class ItemInfoPage
{
    /// <summary><c>gv077E$</c> (MODULE1.BAS:552-555), indexed as the port's ItemAlignment.</summary>
    public static readonly string[] AlignmentTags = { "(u)", "(g)", "(n)", "(e)" };

    /// <summary>The rows of the stat names, the Req figures and the Mod figures (MAINMOD2.BAS:5085-5095).</summary>
    public const int StatNameRow = 6, ReqRow = 7, ModRow = 8;

    /// <summary>Whether these pieces are an item's page: its bold "Item" heading at row 2, column 1.</summary>
    public static bool IsItemPage(IEnumerable<CharStatFile.Piece> pieces) =>
        pieces.Any(p => p.Row == 2 && p.Col == 1 && p.Bold && p.Text == "Item");

    /// <summary><c>gv0846$</c> (MODULE1.BAS:429-440).</summary>
    public static readonly string[] HandWords =
        { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "A Lot" };

    /// <summary>
    /// <c>LTrim$(Str$(single))</c>: seven significant digits, and no nought before the point - VB
    /// writes 0.5 as ".5" and -0.5 as "-.5".
    /// </summary>
    public static string VbSingle(float v)
    {
        string s = v.ToString("G7", CultureInfo.InvariantCulture);
        if (s.StartsWith("0.")) return s[1..];
        if (s.StartsWith("-0.")) return "-" + s[2..];
        return s;
    }

    public static List<CharStatFile.Piece> Paint(Item it, ItemAlignment alignment, IdLevel known, IReadOnlyList<Spell> spells)
    {
        var p = new List<CharStatFile.Piece>();
        void Put(int row, int col, string text, bool bold = false, bool italic = false) =>
            p.Add(new CharStatFile.Piece(row, col, text, bold, italic));
        string Str(long n) => n < 0 ? n.ToString(CultureInfo.InvariantCulture) : " " + n.ToString(CultureInfo.InvariantCulture);
        string N(long n) => n.ToString(CultureInfo.InvariantCulture);
        bool all = known == IdLevel.All, mostly = known >= IdLevel.Mostly, partial = known >= IdLevel.Partial;

        Put(2, 1, "Item", bold: true);
        string name;
        if (mostly)
        {
            name = it.Name.TrimEnd();
            if ((int)alignment > 0 && all) name += " " + AlignmentTags[Math.Clamp((int)alignment, 0, 3)];
            if (all && it.Cursed != 0) name += " (cursed)";
        }
        else name = "<Unknown>";
        Put(2, 6, name);

        if (mostly)
        {
            string ad = N(it.Att) + "/" + N(it.Def);
            int typeIndex = Mdata1.Loaded && it.Type >= 0 && it.Type < Mdata1.ItemTypes.Count ? Mdata1.ItemTypes[it.Type].Index : 0;
            if (it.DamageMod != 0 && all && typeIndex == 0)
                ad += " [" + VbSingle(it.DamageMod) + "]";
            Put(3, 1, "Att/Def", bold: true);
            Put(3, 6, ad);
        }

        Put(4, 1, "Class", bold: true);
        string cls = partial ? Mdata1.ItemTypeName(it.Type) : Mdata1.ItemDescriptionName(it.Type);
        if (all) cls += it.ClassRestricted == -1 ? " (Class Restricted)" : " (Not Class Restricted)";
        Put(4, 6, cls);

        if (mostly)
        {
            Put(ReqRow, 1, "Req", bold: true);
            for (int i = 0; i < 6; i++)
            {
                Put(StatNameRow, i * 2 + 3, RaceData.StatNamesLong[i][..3], bold: true);
                Put(ReqRow, i * 2 + 4, N(i < it.StatsRequired.Length ? it.StatsRequired[i] : 0));
            }
        }
        if (all)
        {
            Put(ModRow, 1, "Mod", bold: true);
            for (int i = 0; i < 6; i++) Put(ModRow, i * 2 + 4, N(i < it.StatsMod.Length ? it.StatsMod[i] : 0));
        }

        string l075A = "";   // a plain Dim, "" on each call, shared by rows 10 and 11
        if (all)
        {
            l075A = it.SpecialType switch
            {
                >= 1 and <= 10 => "Modifies Stat(s)",
                11 => "Adds 50 Spell Points",
                12 => "Adds 200 Spell Points",
                13 or 14 => "Subtracts Age",
                _ => l075A,
            };
            Put(10, 1, l075A);
        }
        if (mostly)
        {
            if (it.NHands > 0)
            {
                if (it.Swings == 1) l075A = ", 1 swing";
                else if (it.Swings > 1) l075A = ", " + N(it.Swings) + " swings";
                Put(11, 1, HandWords[Math.Clamp((int)it.NHands, 0, HandWords.Length - 1)] + "-handed Item" + l075A);
            }
            else Put(11, 1, "Requires no hands");
        }
        if (all && it.SpellIndex != 0)
        {
            int s = it.SpellIndex - 1;
            string spell = s >= 0 && s < spells.Count ? spells[s].Name.TrimEnd() : "";
            Put(12, 1, "Casts \"" + spell + "\"");
            Put(13, 1, it.SpellLvl > 0 ? "at a Spell Level of " + N(it.SpellLvl) : "at the casters Spell Level");
        }

        if (mostly)
        {
            int n = 0;
            Put(10, 13, "Allowed Guilds", bold: true);
            for (int g = 0; g < GuildData.All.Count; g++)
            {
                if ((it.Guilds & (1 << g)) == 0) continue;
                n++;
                Put(10 + n, 13, GuildData.All[g].Name);
                Put(10 + n, 19, all ? Str(Character.UseLevel(it, GuildData.All[g])) : "?");
            }
        }
        if (all)
        {
            int n = 0;
            for (int a = 0; a < Librarian.ItemAbilities.Length && a < 6; a++)
                if ((it.Abilities & (1 << a)) != 0) Put(13 + ++n, 2, Librarian.ItemAbilities[a], italic: true);
            for (int r = 0; r < Effects.ResistanceKinds; r++)
                if ((it.ResistanceFlags & (1 << r)) != 0) Put(13 + ++n, 2, Librarian.Resistances[r] + " Res", italic: true);
        }

        Put(20, 1, "ID Lev:", bold: true);
        Put(21, 1, Loot.IdLevelName(known));
        if (it.SpecialType >= 500 && it.SpecialType <= 500 + GuildData.All.Count - 1)
            Put(23, 1, "This is a Guild-Issued Item", bold: true);
        return p;
    }
}
