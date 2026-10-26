using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// The character window's Resist. page, <c>mdrcharShowInfo</c> case <c>mc0590</c> (MAINMOD2.BAS:5209-5219):
/// "Current Resistances" in bold at row 2, then each of the eleven kinds (<c>gv0620$</c>) a row
/// apiece from row 4 at column 2, and at column 12 the percentage - or " -", with its leading space,
/// for anything not above nought. A negative resistance therefore reads as none at all.
/// </summary>
public static class ResistTabPage
{
    public static List<CharStatFile.Piece> Paint(Character c, Func<short, Item?> item)
    {
        var p = new List<CharStatFile.Piece> { new(2, 1, "Current Resistances", true) };
        for (int i = 0; i < Effects.ResistanceKinds; i++)
        {
            int pct = Effects.ResistancePercent(c, i, item);
            p.Add(new(4 + i, 2, Librarian.Resistances[i], false));
            p.Add(new(4 + i, 12, pct > 0 ? pct.ToString(CultureInfo.InvariantCulture) + "%" : " -", false));
        }
        return p;
    }
}
