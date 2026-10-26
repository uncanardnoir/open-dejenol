using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Mordor.App;

/// <summary>
/// Which focused controls keep their keys from the game's KeyPreview. The original's Form_KeyDown
/// handlers leave every key to a text box, a combo box and (in Objects &amp; Spells and the automap)
/// a scroll bar (FRMCHAR3.BAS:313-318 among them), so the port's previews pass those over.
/// </summary>
public static class KeyOwner
{
    /// <summary>
    /// True for a text box, a combo box or a scroll bar, anything inside one, and anything in a
    /// popup - an open combo box's list above all.
    /// </summary>
    /// <remarks>
    /// An open ComboBox moves the focus to its selected item, which lives in the drop-down's own
    /// PopupRoot. That item has no ComboBox among its visual ancestors - the popup's tree stops at
    /// its root - but its key events still route through the ComboBox to the window (a PopupRoot's
    /// interactive parent is its Popup), so the previews saw a plain control and took the key: the
    /// Down arrow meant for the Spell Type list turned the party round instead. The logical tree
    /// does run from the item to its ComboBox, and a popup's contents are never the game's anyway.
    /// </remarks>
    public static bool KeepsKeys(Visual f) =>
        f is TextBox or ComboBox or ScrollBar
        || f.FindAncestorOfType<TextBox>() != null
        || f.FindAncestorOfType<ComboBox>() != null
        || (f is ILogical l && l.FindLogicalAncestorOfType<ComboBox>() != null)
        || TopLevel.GetTopLevel(f) is PopupRoot;

    /// <summary>A combo box, or anything of one's - its open list included.</summary>
    public static bool InComboBox(Visual f) =>
        f is ComboBox || f.FindAncestorOfType<ComboBox>() != null
        || (f is ILogical l && l.FindLogicalAncestorOfType<ComboBox>() != null);
}
