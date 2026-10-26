using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// <c>frmInteract</c> asking for a character (FRMINTER.BAS): a caption, a label, and
/// <c>cmbCharacter</c>, a drop-down combo - a box you may type in, with a button that drops the
/// whole list of the characters on offer. A name, or the start of exactly one, is accepted. A name
/// that matches nothing flashes "Not Found!" in the box for half a second, one that could be
/// several flashes "ambiguous!", and the box stays open. An empty box and OK does nothing. With
/// nobody to offer, the box holds the sentence the caller gives, greyed, and OK is greyed with it.
/// Cancel is -1.
///
/// The name chosen at the start is the one picked last time (<c>m001C</c>, FRMINTER.BAS:473-479),
/// when that is still a line of the list, and otherwise the first. <c>m001C</c> is a form-level
/// variable, written on every accepted OK (111), and every caller shares it: fn170D unloads the
/// form after each question (MAINMOD2.BAS:6256), but only its error path ever sets it to Nothing,
/// so the form's module data outlives the unload.
/// </summary>
public static class NamePicker
{
    /// <summary><c>m001C</c>: the list index accepted last, whoever asked.</summary>
    private static int _lastPicked;

    public static async System.Threading.Tasks.Task<int> Ask(Window owner, string title, string label,
                                                              IReadOnlyList<string> names, string nobody)
    {
        int chosen = -1;
        bool any = names.Count > 0;
        // 473-479: the last pick if it is in range, else the first
        int start = _lastPicked >= 0 && _lastPicked <= names.Count - 1 ? _lastPicked : 0;
        int listIndex = any ? start : -1;

        var box = new TextBox
        {
            Text = any ? names[start] : nobody, IsEnabled = any,
            BorderThickness = new Avalonia.Thickness(0), Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center,
            // the refusal sentences run to 43 characters (Interact.NobodyFor): the box is as wide as
            // the dialog lets it be, with the theme's minimum width and roomy padding taken off, and a
            // sentence that still does not fit wraps rather than being cut off
            MinWidth = 0, Padding = new Avalonia.Thickness(4, 2),
            TextWrapping = any ? TextWrapping.NoWrap : TextWrapping.Wrap,
        };
        // cmbCharacter's list, in the dialog's font (below)
        var list = new ListBox { ItemsSource = names.ToList(), MaxHeight = 160 };
        var drop = new ToggleButton
        {
            Content = "▼", Width = 22, IsEnabled = any, Focusable = false, Padding = new Avalonia.Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            [DockPanel.DockProperty] = Dock.Right,
        };
        var combo = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch, BorderThickness = new Avalonia.Thickness(1), BorderBrush = Brushes.Gray,
            Child = new DockPanel { Children = { drop, box } },
        };
        var popup = new Popup
        {
            PlacementTarget = combo, Placement = PlacementMode.Bottom, IsLightDismissEnabled = true,
            Child = new Border { Background = Brush.Parse("#101018"), BorderBrush = Brushes.Gray, BorderThickness = new Avalonia.Thickness(1), Child = list },
        };
        drop.IsCheckedChanged += (_, _) =>
        {
            popup.IsOpen = drop.IsChecked == true;
            if (popup.IsOpen) { list.Width = combo.Bounds.Width; list.SelectedIndex = listIndex; }
        };
        popup.Closed += (_, _) => drop.IsChecked = false;
        void Pick(int i)
        {
            if (i < 0 || i >= names.Count) return;
            listIndex = i;
            box.Text = names[i];
            box.CaretIndex = box.Text.Length;
        }
        list.AddHandler(InputElement.PointerReleasedEvent, (_, _) =>
        {
            Pick(list.SelectedIndex);
            popup.IsOpen = false;
            box.Focus();
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

        var ok = new Button { Content = "OK", IsDefault = true, IsEnabled = any };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // the combo's own keys: up and down step through the list, F4 and Alt+Down drop it
        box.KeyDown += (_, k) =>
        {
            if (k.Key == Key.F4 || (k.Key == Key.Down && k.KeyModifiers.HasFlag(KeyModifiers.Alt)))
            {
                drop.IsChecked = drop.IsChecked != true;
                k.Handled = true;
            }
            else if (k.Key == Key.Down) { Pick(listIndex + 1); k.Handled = true; }
            else if (k.Key == Key.Up) { Pick(listIndex - 1); k.Handled = true; }
        };

        // frmInteract's own font, MS Sans Serif 8.25 (lblQuestion, cmbCharacter), which its Form_Load
        // puts through sub0784 (FRMINTER.BAS:319): the Options fonts, UiFonts. It had been the frame's
        // Cascadia Mono
        var win = UiFonts.Set(new Window
        {
            Title = title, Width = 420, SizeToContent = SizeToContent.Height,
            Background = Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = label, Foreground = Brushes.LightGray },
                        combo,
                        popup,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
                    },
                },
            },
        }, Mordor.Core.DesignFonts.Interact, "lblQuestion");
        // the drop-down's list is in a popup, out of the window's tree: its own copy of the font
        UiFonts.Set(list, Mordor.Core.DesignFonts.Interact, "cmbCharacter");
        // cmbCharacter.SetFocus (480)
        if (any) win.Opened += (_, _) => { box.Focus(); box.SelectAll(); };

        async System.Threading.Tasks.Task Flash(string what, string after)
        {
            box.Text = what;
            await System.Threading.Tasks.Task.Delay(500);
            box.Text = after;
        }
        ok.Click += async (_, _) =>
        {
            string typed = box.Text ?? "";   // compared as it stands, untrimmed (69-79)
            if (typed.Length == 0) return;   // FRMINTER.BAS:69
            int exact = names.ToList().FindIndex(n => string.Equals(n, typed, System.StringComparison.OrdinalIgnoreCase));
            int pick = exact;
            if (pick < 0)
            {
                var starts = Enumerable.Range(0, names.Count)
                    .Where(i => names[i].StartsWith(typed, System.StringComparison.OrdinalIgnoreCase)).ToList();
                if (starts.Count == 1) pick = starts[0];
                // "Not Found!" puts back what was typed (85-89); "ambiguous!" leaves the last name that matched,
                // which the loop had written into the box (82, 92-97)
                else if (starts.Count == 0) { await Flash("Not Found!", typed); return; }
                else { await Flash("ambiguous!", names[starts[^1]]); return; }
            }
            // 102-111: the list line with the name now in the box is selected, and m001C keeps it
            _lastPicked = pick;
            chosen = pick;
            win.Close();
        };
        cancel.Click += (_, _) => win.Close();
        await win.ShowDialog(owner);
        return chosen;
    }
}
