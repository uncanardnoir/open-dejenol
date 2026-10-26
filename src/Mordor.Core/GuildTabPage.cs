using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// The character window's Guild page, <c>mdrcharShowInfo</c> case <c>mc059C</c> (MAINMOD2.BAS:5335-5359),
/// painted with <c>sub09B8</c> like the Char page (see <see cref="CharStatFile"/>):
///
/// <list type="bullet">
/// <item>"Guilds" at row 2, then each guild joined, in table order, as "Name (level)" a row apiece
/// from row 3, indented one column.</item>
/// <item>Two rows below the last, "Total Skill": the Hall of Records' fighting, magical and
/// thieving indexes (<c>mdrhalGetIndexValue</c> 9, 10 and 11), each followed by its word.</item>
/// <item>Two rows below that, "Current Guild" and its name.</item>
/// <item>At column 11, "Guild Abilities": every ability <c>fn123F</c> rates above nought - which is
/// all six, since it never returns less than 1 - its name bold every third row from row 3, and two
/// rows under it in italics "rating from Guild".</item>
/// </list>
///
/// A fault kept: the guild named is <c>gv141C.M5493</c>, which <c>abilityChance</c> sets only
/// when a guild beats nought (MAINMOD2.BAS:1934-1981). It clears the modifier and the level
/// but not the guild, so an ability no guild of yours gives anything for is said to come "from"
/// whichever guild won the call before - the ability above it, or for the first ones the Hall's
/// own calls made just before (fighting, then critical hit, backstabbing, multiple swings, then
/// thieving).
///
/// What the global held before the page - left by the twenty-odd other abilityChance calls about
/// the game (traps, chests, perception, combat) - never shows. The page's very first call is the
/// fighting index's fn123F for ability 4 (HALL.BAS:567), whose case is
/// <c>mc0080 + ((Log(100 + level) / 1.75 - 2.3) * M55BE^2) / 2</c> (MAINMOD2.BAS:1966): mc0080 is
/// 0.6 and the rest is never negative (Log(101) / 1.75 is 2.64), so the first guild joined beats
/// nought and writes M5493 before anything reads it. Where the port starts it is unobservable,
/// so the global need not be carried between calls (checked 2026-10-21).
/// </summary>
public static class GuildTabPage
{
    public static List<CharStatFile.Piece> Paint(Character c, Func<short, Item?> item, IReadOnlyList<Spell> spells)
    {
        var p = new List<CharStatFile.Piece>();
        void Put(int row, int col, string text, bool bold = false, bool italic = false) =>
            p.Add(new CharStatFile.Piece(row, col, text, bold, italic));
        string N(long v) => v.ToString(CultureInfo.InvariantCulture);

        int n = 0;
        Put(2, 1, "Guilds", bold: true);
        foreach (var g in GuildData.All)
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            n++;
            Put(n + 2, 2, g.Name + " (" + N(m.Level) + ")");
        }

        // gv141C.M5493 as the page's calls leave it, one abilityChance at a time. The first,
        // ability 4, always writes it (see above), so what it starts at never shows
        string lead = GuildData.All.Count > 0 ? GuildData.All[0].Name : "";
        void Chance(int ability)
        {
            var won = Rules.AbilityLead(c, ability, item);
            if (won.Guild != "") lead = won.Guild;
        }

        Put(n + 4, 1, "Total Skill", bold: true);
        int fighting = HallRecords.FightingSkill(c, item);
        foreach (int a in new[] { 4, 2, 1, 3 }) Chance(a);
        Put(n + 5, 2, N(fighting) + " Fighting");
        Put(n + 6, 2, N(HallRecords.MagicSkill(c, spells)) + " Magical");
        int thieving = HallRecords.ThievingSkill(c, item);
        Chance(0);
        Put(n + 7, 2, N(thieving) + " Thieving");
        Put(n + 9, 1, "Current Guild", bold: true);
        Put(n + 10, 2, c.Guild.Guild);

        int row = 0;
        Put(2, 11, "Guild Abilities", bold: true);
        for (int a = 0; a < Rules.AbilityNames.Length; a++)
        {
            int rating = Rules.AbilityRatingExact(c, a, item);
            Chance(a);
            if (rating <= 0) continue;
            row += 3;
            Put(row, 12, Rules.AbilityNames[a], bold: true);
            Put(row + 2, 13, N(rating) + " from " + lead, italic: true);
        }
        return p;
    }
}
