using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.QoL;

/// <summary>
/// The "Store: sortable columns" toggle: the Store's Buy list sorted by a click on its Item header
/// (the name) or, while the Slot column is shown, its Slot header (the wear-class label, then the
/// name). A click on the header sorted by now reverses it, and a third click goes back to the
/// shelf's own order, the original's (sub260A, FRMCTYS.BAS:512-555, lists by item index). One sort
/// at a time, kept as the text setting <see cref="QolMod.StoreSortOrder"/>: "Item", "-Item", "Slot",
/// "-Slot", or "" for the shelf's order.
/// </summary>
public static class StoreSort
{
    public const string ItemHeader = "Item";
    public const string SlotHeader = "Slot";

    /// <summary>The sort a setting names: the header, and whether it is reversed. ("", false) for none or anything unknown.</summary>
    public static (string Header, bool Descending) Parse(string? setting)
    {
        string s = (setting ?? "").Trim();
        bool desc = s.StartsWith('-');
        if (desc) s = s[1..];
        return s == ItemHeader || s == SlotHeader ? (s, desc) : ("", false);
    }

    public static string Format(string header, bool descending) => header == "" ? "" : (descending ? "-" : "") + header;

    /// <summary>
    /// The setting after a click on <paramref name="header"/>: another header's sort, or none,
    /// becomes this one's first to last; first to last becomes last to first; last to first goes
    /// back to the shelf's order.
    /// </summary>
    public static string Next(string? setting, string header)
    {
        var (now, desc) = Parse(setting);
        if (now != header) return Format(header, false);
        return desc ? "" : Format(header, true);
    }

    /// <summary>The mark <paramref name="header"/> shows under <paramref name="setting"/>.</summary>
    public static StoreSortArrow Arrow(string? setting, string header)
    {
        var (now, desc) = Parse(setting);
        return now != header ? StoreSortArrow.None : desc ? StoreSortArrow.Descending : StoreSortArrow.Ascending;
    }

    private static string Name(Item it) => (it.Name ?? "").Trim();

    /// <summary>
    /// The rows in the setting's order, or null for the shelf's own (no sort, or a Slot sort while
    /// the Slot column is not shown). Names compare ignoring case; ties keep the shelf's order.
    /// </summary>
    public static List<Item>? Order(IReadOnlyList<Item> rows, string? setting, bool slotShown)
    {
        var (header, desc) = Parse(setting);
        var cmp = StringComparer.OrdinalIgnoreCase;
        switch (header)
        {
            case ItemHeader:
                return (desc ? rows.OrderByDescending(Name, cmp) : rows.OrderBy(Name, cmp)).ToList();
            case SlotHeader when slotShown:
                return (desc
                    ? rows.OrderByDescending(WearSlot.LabelOf, cmp).ThenByDescending(Name, cmp)
                    : rows.OrderBy(WearSlot.LabelOf, cmp).ThenBy(Name, cmp)).ToList();
            default:
                return null;
        }
    }
}
