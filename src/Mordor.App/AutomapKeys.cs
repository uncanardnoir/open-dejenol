using Avalonia.Input;

namespace Mordor.App;

/// <summary>
/// "&lt;  Display Previous Automap Level" and "&gt;  Display Next Automap Level" (help, Commands,
/// Automap Keys) - and the AutoMap page says where they work: "use the '&lt;' and '&gt;' keys from
/// any other window in the game to page through the levels."
///
/// They are handled by <c>fn1521</c> (MAINMOD2.BAS:3393-3410), the routine every form's KeyDown
/// goes through, which is why they work from anywhere:
///
/// <code>
/// Case mc0556                                  ' 188, VK_OEM_COMMA
///     If gv1D64 = mc0558 Then                  ' the automap is open and not minimised
///         If p0534 = mc055A Then               ' Shift held, so the key really is "&lt;"
///             If frmAutoMap!hsbLevel.Value &gt; 1 Then
///                 frmAutoMap!hsbLevel.Value = frmAutoMap!hsbLevel.Value - 1
/// </code>
///
/// <c>gv1D64</c> is set to 1 by the automap's Form_Resize (FRMAUTOM.BAS:155) when it is open and restored, to 2 when
/// it is minimised (166), and put back on Form_Unload (189). So the keys do nothing
/// unless the map is actually up - which is the original's own rule, not a convenience.
///
/// This port had these keys on <c>AutomapWindow.OnKeyDown</c>, which stopped being reached the
/// moment the map became a child of the frame: the child takes no keyboard focus, so the keys
/// went nowhere at all.
/// </summary>
public static class AutomapKeys
{
    /// <summary>
    /// How far along to page for this key, or null if it is not one of them. Shift is required:
    /// the original tests the shift state, and unshifted those keys are "," and ".".
    /// </summary>
    public static int? PageBy(Key key, KeyModifiers mods)
    {
        if (!mods.HasFlag(KeyModifiers.Shift)) return null;
        return key switch
        {
            Key.OemComma => -1,     // 188, "Display Previous Automap Level"
            Key.OemPeriod => 1,     // 190, "Display Next Automap Level"
            _ => null,
        };
    }

    /// <summary>
    /// Pages the frame's automap child, and says whether it did. Nothing happens when the map is
    /// closed or minimised, which is <c>gv1D64</c>'s two states.
    /// </summary>
    /// <summary>
    /// Escape while a mod's pick is waiting on the map (<c>AutomapView.PickSquare</c>, API version 13):
    /// the pick is given up and the key goes no further. Nothing at all otherwise - with no pick waiting,
    /// which is always so with no mod, Escape is the game's as before.
    /// </summary>
    public static bool CancelPick(Key key, AutomapWindow? map)
    {
        if (key != Key.Escape || map is not { Picking: true }) return false;
        map.CancelPick();
        return true;
    }

    public static bool Handle(Key key, KeyModifiers mods, MdiChild? child, AutomapWindow? map)
    {
        if (PageBy(key, mods) is not int delta) return false;
        if (child is not { IsVisible: true, Minimised: false } || map == null) return false;
        map.Page(delta);
        return true;
    }
}
