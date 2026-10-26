using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The original's "Locate Who?" / "Fetch Who?" / "Cast on Who?" list: a prompt, a list of lines and
/// OK and Cancel, in frmInteract's font. Returns the chosen index, or -1. The dungeon asks it for
/// its casts; a mod asks it through <c>ModGame.AskCastOnWho</c> (<see cref="ForMods"/>).
/// </summary>
internal static class PickList
{
    public static async System.Threading.Tasks.Task<int> Ask(Window owner, string title, string prompt, IReadOnlyList<string> options, int selected = 0)
    {
        int chosen = -1;
        var list = new ListBox { Height = 180, ItemsSource = options.ToList() };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // in frmInteract's font, the form it stands for, through the Options fonts (UiFonts)
        var win = UiFonts.Set(new Window
        {
            Title = title,
            Width = 420, Height = 300,
            Background = Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = prompt, Foreground = Brushes.LightGray },
                        list,
                        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
                    },
                },
            },
        }, DesignFonts.Interact, "lblQuestion");
        ok.Click += (_, _) => { chosen = list.SelectedIndex; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => { if (options.Count > 0) list.SelectedIndex = selected >= 0 && selected < options.Count ? selected : 0; list.Focus(); };

        await win.ShowDialog(owner);
        return chosen;
    }

    /// <summary>
    /// The box for a mod (<c>ModHost.CastOnWhoBox</c>): shown over the game's frame that the mod's
    /// window belongs to (<see cref="ModWindows.FrameOf"/>), never over the mod's window itself, so
    /// the keyboard goes back to the game when it closes. With no control given it is shown over the
    /// application's main window. A control in no window (detached, or not a control at all), or a
    /// frame that is closed or hidden, has nothing to show it over: Cancel.
    /// </summary>
    public static System.Threading.Tasks.Task<int> ForMods(string title, string prompt, IReadOnlyList<string> options, object? over, int selected = 0)
    {
        Window? frame = over == null
            ? (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
            : over is Visual v && TopLevel.GetTopLevel(v) is Window w ? ModWindows.FrameOf(w) : null;
        if (frame == null || !frame.IsVisible) return System.Threading.Tasks.Task.FromResult(-1);
        return Ask(frame, title, prompt, options, selected);
    }
}
