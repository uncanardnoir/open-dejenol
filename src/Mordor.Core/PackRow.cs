namespace Mordor.Core;

/// <summary>
/// How a line of the pack is drawn on the Objects &amp; Spells window's Items page.
///
/// <c>mdrcharShowItem</c> (MAINMOD2.BAS:5370-5480) paints one row and sets two font flags on it.
/// Both are inside <c>If Pack(n).IdLevel = m0372</c> - the fully identified level - so an item you
/// have not worked out yet is always drawn plain, whatever it turns out to be:
///
/// <code>
/// If Items(ref).Charges > 0 Or Pack(n).Charges > 0 Then  FontBold = True   Else FontBold = False
/// If Items(ref).Cursed Then                              FontItalic = True Else FontItalic = False
/// </code>
///
/// The help's screenshot of the window (game_files/bm14.png) shows it: the four bold rows are the
/// ones carrying a charge count in brackets, and the rest are plain.
///
/// The equipped mark is not part of the text. It is <c>lblItemStatus(n)</c>, a label of its own
/// beside the row, whose caption is a space or a mark (MAINMOD2.BAS:5421-5431).
/// </summary>
public static class PackRow
{
    /// <summary>Identified, and the item or this instance of it carries charges.</summary>
    public static bool Bold(Item? it, InventorySlot slot) =>
        it != null && slot.Known == IdLevel.All && (it.Charges > 0 || slot.Charges > 0);

    /// <summary>Identified and cursed. The name itself also gains " (cursed)" at that level.</summary>
    public static bool Italic(Item? it, InventorySlot slot) =>
        it != null && slot.Known == IdLevel.All && it.Cursed != 0;
}
