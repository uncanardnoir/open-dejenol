using Avalonia;
using Avalonia.Controls;

namespace Mordor.App;

/// <summary>
/// Linux (issue #6, Bazzite KDE; reproduced under KWin on X11): the keyboard focus is one for the whole
/// program, and a window being activated there does not take it from a window only hidden. The title is
/// hidden while the town is up, so after Run its "Create a Character" button kept the focus and every key
/// in town went to the hidden title - nothing worked until a control that takes the focus was clicked.
/// A focus left in another window is let go when the game's window is activated, so a key comes to that
/// window (its own key handling, as with nothing focused). A port necessity. Windows moves the focus
/// itself and is left as it was.
/// </summary>
public static class KeyboardHome
{
    /// <summary>Called from the constructors of the windows that take each other's place: the title, the town and the dungeon.</summary>
    public static void Follow(Window w)
    {
        if (System.OperatingSystem.IsWindows()) return;
        w.Activated += (_, _) =>
        {
            if (w.FocusManager?.GetFocusedElement() is Visual v && TopLevel.GetTopLevel(v) != w)
                w.FocusManager.Focus(null);   // Avalonia 11's ClearFocus, which was this call
        };
    }
}
