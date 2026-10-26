using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// The port's own Mods page (docs/MODDING.md), opened from the Mods button of the main menu or the in-game Control Panel. Every mod found: its name and version (its
/// description the tooltip), a box to switch it on, a line only for a problem (<see cref="IsProblem"/>), and the
/// settings the mod offers (each description a tooltip), or a Settings... button for a mod with a settings window of its own
/// (<see cref="ModSettingsWindow"/>). A mod's windows open from the toolbar, not from here. Laid out in the Options window's style.
///
/// Mods are switched on or off only on the main menu's page, where no game window is open: there
/// the choice is kept in settings.ini and takes effect at once for a mod that supports it
/// (<see cref="IModLifecycle"/>, as the bundled ones do: <see cref="ModHost.Switch"/>), and at the
/// next start for one that does not - its status line says which. A game's windows build what mods
/// add to them from the mods running when the game begins. On the Control Panel's page the Enabled
/// boxes are greyed. A mod's own settings can be changed on either page, and take effect at once
/// for a mod that reads them as it goes, as the Quality of Life mod does.
/// </summary>
public sealed class ModsWindow : Window
{
    private static readonly IBrush Heading = Brush.Parse("#8fb0ff");
    private static readonly IBrush Faint = Brush.Parse("#9a9aae");

    /// <summary>Each mod's settings window while it is open (one at a time per mod, whichever page opened it).</summary>
    public static readonly System.Collections.Generic.Dictionary<string, ModSettingsWindow> OpenSettings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The note in place of the Enabled boxes on the Control Panel's page.</summary>
    public const string SwitchAtMainMenu = "Mods can be switched on or off at the main menu.";

    /// <param name="atMainMenu">
    /// Opened from the main menu, with no game going: the Enabled boxes switch mods. False (the
    /// Control Panel's page, in a game): they are greyed, with <see cref="SwitchAtMainMenu"/>.
    /// </param>
    public ModsWindow(bool atMainMenu)
    {
        var host = ModHost.Active;
        Title = "Mods";
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brush.Parse("#101018");
        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var body = new StackPanel { Spacing = 10, Width = 520 };
        body.Children.Add(Text(
            "Mods change how the game plays. With none switched on, the game is the original. "
            + (atMainMenu
                ? "Switching a mod on or off takes effect at once (a mod that cannot says so, and "
                  + "changes at the next start); a mod's own settings take effect at once."
                : "A mod's own settings take effect at once."), Brushes.Gainsboro));
        if (!atMainMenu) body.Children.Add(Text(SwitchAtMainMenu, Heading));

        if (host.Discovered.Count == 0)
        {
            body.Children.Add(Text("No mods were found. A mod is a folder holding a mod.json, put in the "
                + "mods folder beside the game folder (next to saves).", Faint));
        }

        foreach (var mod in host.Discovered)
        {
            var loaded = host.Loaded.FirstOrDefault(l => l.Id == mod.Id);
            var group = new StackPanel { Spacing = 4 };

            var enabled = new CheckBox
            {
                Content = "Enabled",
                IsChecked = host.IsEnabled(mod.Id),
                Foreground = Brushes.Gainsboro,
                FontSize = 12,
                // switched only on the main menu's page, with no game window open
                IsEnabled = mod.Problem == null && atMainMenu,
            };
            if (!atMainMenu) ToolTip.SetTip(enabled, SwitchAtMainMenu);
            // a status line only for a problem: the Enabled box already says running or off
            var status = Text("", Faint);
            void ShowStatus(string s) { status.Text = s; status.IsVisible = IsProblem(s); }
            ShowStatus(host.StatusOf(mod.Id, host.IsEnabled(mod.Id)));
            string id = mod.Id;
            enabled.IsCheckedChanged += (_, _) =>
            {
                // loaded, or stopped, now (ModHost.Switch); a game begun after this builds its
                // windows from the mods running then
                try { host.Switch(id, enabled.IsChecked == true); }
                catch (Exception ex) { CrashLog.Note($"mods page: switching {id} failed - {ex.Message}"); }
                // a mod window still open somewhere closes with its mod (none is open on the main menu)
                ModWindows.CloseStopped();
                ShowStatus(host.StatusOf(id, host.IsEnabled(id)));
            };
            // under the heading (its name and version, the mod's description its tooltip): the
            // Enabled box, a problem if there is one, then the mod's own settings - or, for a mod
            // with a settings window of its own, the button that opens it
            group.Children.Add(enabled);
            group.Children.Add(status);

            if (mod.Manifest.SettingsWindow is { } own)
            {
                var open = new Button { Content = "Settings...", HorizontalAlignment = HorizontalAlignment.Left, FontSize = 12, Margin = new Thickness(18, 2, 0, 0) };
                ToolTip.SetTip(open, $"Open the {(own.Title == "" ? mod.Manifest.Name : own.Title)} window");
                var forMod = mod;
                open.Click += (_, _) =>
                {
                    try
                    {
                        // one window per mod: a second click brings the open one forward
                        if (OpenSettings.TryGetValue(forMod.Id, out var already) && already.IsVisible) { already.Activate(); return; }
                        var w = new ModSettingsWindow(host, forMod);
                        OpenSettings[forMod.Id] = w;
                        w.Closed += (_, _) => { if (OpenSettings.TryGetValue(forMod.Id, out var cur) && cur == w) OpenSettings.Remove(forMod.Id); };
                        w.Show(this);
                    }
                    catch (Exception ex) { CrashLog.Note($"mods page: opening {forMod.Id}'s settings failed - {ex.Message}"); }
                };
                group.Children.Add(open);
            }
            else
            {
                // the choice settings (mod.json "choices"): radio buttons each, also while the mod is off
                foreach (var choice in mod.Manifest.Choices)
                    if (choice.Key != "" && choice.Options.Count > 0) group.Children.Add(ChoiceEditor(this, host, mod.Id, choice, new Thickness(18, 4, 0, 0)));

                var toggles = loaded?.Settings.Toggles ?? (System.Collections.Generic.IReadOnlyList<ModToggleSpec>)mod.Manifest.Settings;
                if (toggles.Count > 0)
                {
                    var list = new StackPanel { Spacing = 4, Margin = new Thickness(18, 4, 0, 0) };
                    foreach (var t in toggles) list.Children.Add(ToggleBox(this, host, mod.Id, t));
                    group.Children.Add(list);
                }
            }

            // the list settings (mod.json "tables"): an editor each, also while the mod is off
            foreach (var table in mod.Manifest.Tables)
                if (table.Key != "" && table.Columns.Count > 0) group.Children.Add(TableEditor(host, mod.Id, table));

            body.Children.Add(Group(mod.Manifest.Title, group, mod.Manifest.Description));
        }

        var close = new Button { Content = "_Close", IsDefault = true, IsCancel = true, Width = 120, HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        close.Click += (_, _) => Close();
        // the theme's scroll bar is drawn over the page's right edge: the text keeps clear of it
        body.Margin = new Thickness(0, 0, 18, 0);

        Content = new Border
        {
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new ScrollViewer { MaxHeight = 560, Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
                    close,
                },
            },
        };
    }

    /// <summary>
    /// A toggle (<see cref="ModToggleSpec"/>): a check box with the label, its description the
    /// tooltip. Ticking it writes settings.ini at once; the same setting flipped elsewhere while the
    /// window is up (a mod's own check box, StoreOptions, the other page) moves the box. Setting it
    /// to what it shows raises nothing.
    /// </summary>
    internal static CheckBox ToggleBox(Window owner, ModHost host, string modId, ModToggleSpec t)
    {
        var box = new CheckBox
        {
            Content = t.Label == "" ? t.Key : t.Label,
            IsChecked = host.Settings.ReadFlag(modId, t.Key, t.Default),
            Foreground = Brushes.Gainsboro,
            FontSize = 12,
        };
        if (t.Description != "") ToolTip.SetTip(box, t.Description);
        string key = t.Key;
        bool dflt = t.Default;
        box.IsCheckedChanged += (_, _) => { if (box.IsChecked is bool v && v != host.Settings.ReadFlag(modId, key, dflt)) host.Settings.WriteFlag(modId, key, v); };
        Action<string, string> follow = (m, k) =>
        {
            if (string.Equals(m, modId, StringComparison.OrdinalIgnoreCase) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                Avalonia.Threading.Dispatcher.UIThread.Post(() => box.IsChecked = host.Settings.ReadFlag(modId, key, dflt));
        };
        host.Settings.Changed += follow;
        owner.Closed += (_, _) => host.Settings.Changed -= follow;
        return box;
    }

    /// <summary>
    /// A choice setting (<see cref="ModChoiceSpec"/>): its caption, a radio button per option in a
    /// row; each button's tooltip is its option's own line, else the choice's description (the
    /// caption's). Picking one writes it to settings.ini at once; the same setting changed
    /// elsewhere while the window is up moves the pick.
    /// </summary>
    internal static Control ChoiceEditor(Window owner, ModHost host, string modId, ModChoiceSpec choice, Thickness margin)
    {
        var panel = new StackPanel { Spacing = 2, Margin = margin };
        var caption = new TextBlock { Text = choice.Label == "" ? choice.Key : choice.Label, Foreground = Brushes.Gainsboro, FontSize = 12 };
        if (choice.Description != "") ToolTip.SetTip(caption, choice.Description);
        panel.Children.Add(caption);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(10, 0, 0, 0) };
        string Now() => ModSettings.ChoiceOf(choice, host.Settings.Read(modId, choice.Key));
        var buttons = new System.Collections.Generic.List<RadioButton>();
        foreach (var option in choice.Options)
        {
            var b = new RadioButton
            {
                Content = option,
                GroupName = "choice:" + modId + ":" + choice.Key,
                IsChecked = Now() == option,
                Foreground = Brushes.Gainsboro,
                FontSize = 12,
            };
            if (choice.TooltipFor(option) is { Length: > 0 } tip) ToolTip.SetTip(b, tip);
            string o = option;
            b.IsCheckedChanged += (_, _) => { if (b.IsChecked == true && Now() != o) host.Settings.Write(modId, choice.Key, o); };
            buttons.Add(b);
            row.Children.Add(b);
        }
        panel.Children.Add(row);
        Action<string, string> follow = (m, k) =>
        {
            if (!string.Equals(m, modId, StringComparison.OrdinalIgnoreCase) || !string.Equals(k, choice.Key, StringComparison.OrdinalIgnoreCase)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { foreach (var b in buttons) b.IsChecked = Equals(b.Content, Now()); });
        };
        host.Settings.Changed += follow;
        owner.Closed += (_, _) => host.Settings.Changed -= follow;
        return panel;
    }

    /// <summary>
    /// A list setting's editor (<see cref="ModTableSpec"/>): the caption and description, the
    /// column headings, a row of free-text boxes per entry with ▲ ▼ ✕ (move up, move down, remove),
    /// and "Add row". What the boxes hold is written to settings.ini (<see cref="ModSettings.EncodeRows"/>)
    /// when a box loses the keyboard, when a row is added, moved or removed, and when the page
    /// closes; reading it back on the next opening shows the same rows.
    /// </summary>
    private Control TableEditor(ModHost host, string modId, ModTableSpec table)
    {
        int cols = table.Columns.Count;
        var rows = ModSettings.DecodeRows(host.Settings.Read(modId, table.Key) ?? "")
            .Select(r => Enumerable.Range(0, cols).Select(i => i < r.Length ? r[i] : "").ToArray()).ToList();
        var panel = new StackPanel { Spacing = 3, Margin = new Thickness(18, 6, 0, 0) };
        var tableCaption = new TextBlock
        {
            Text = table.Label == "" ? table.Key : table.Label,
            Foreground = Brushes.Gainsboro, FontSize = 12,
        };
        if (table.Description != "") ToolTip.SetTip(tableCaption, table.Description);
        panel.Children.Add(tableCaption);
        const double buttons = 3 * 26 + 6;
        double cellWidth = Math.Floor((500 - 18 - buttons - 4 * cols) / cols);
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var c in table.Columns)
            heading.Children.Add(new TextBlock { Text = c, Width = cellWidth, Foreground = Faint, FontSize = 11 });
        panel.Children.Add(heading);
        var body = new StackPanel { Spacing = 3 };
        panel.Children.Add(body);

        bool dirty = false;
        void Save()
        {
            if (!dirty) return;
            dirty = false;
            try { host.Settings.Write(modId, table.Key, ModSettings.EncodeRows(rows)); }
            catch (Exception ex) { CrashLog.Note($"mods page: writing {modId}.{table.Key} failed - {ex.Message}"); }
        }

        void Draw()
        {
            body.Children.Clear();
            for (int r = 0; r < rows.Count; r++)
            {
                int at = r;
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                for (int c = 0; c < cols; c++)
                {
                    int col = c;
                    var box = new TextBox { Text = rows[at][col], Width = cellWidth, FontSize = 11, MinHeight = 22, Padding = new Thickness(4, 1) };
                    box.TextChanged += (_, _) =>
                    {
                        if (rows[at][col] == (box.Text ?? "")) return;
                        rows[at][col] = box.Text ?? "";
                        dirty = true;
                    };
                    box.LostFocus += (_, _) => Save();
                    // Enter in a cell keeps the edit and stops there: it must not reach the page's
                    // default Close button (which listens for unhandled Enter on the window)
                    box.KeyDown += (_, k) =>
                    {
                        if (k.Key is not (Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return)) return;
                        k.Handled = true;
                        Save();
                    };
                    line.Children.Add(box);
                }
                Button Small(string text, string tip, Action act, bool enabled = true)
                {
                    var b = new Button { Content = text, Width = 24, MinHeight = 22, Padding = new Thickness(0), FontSize = 10, IsEnabled = enabled,
                                         HorizontalContentAlignment = HorizontalAlignment.Center };
                    ToolTip.SetTip(b, tip);
                    b.Click += (_, _) => { act(); dirty = true; Save(); Draw(); };
                    return b;
                }
                line.Children.Add(Small("▲", "Move up", () => (rows[at - 1], rows[at]) = (rows[at], rows[at - 1]), at > 0));
                line.Children.Add(Small("▼", "Move down", () => (rows[at + 1], rows[at]) = (rows[at], rows[at + 1]), at < rows.Count - 1));
                line.Children.Add(Small("✕", "Remove", () => rows.RemoveAt(at)));
                body.Children.Add(line);
            }
        }

        var add = new Button { Content = "Add row", HorizontalAlignment = HorizontalAlignment.Left, FontSize = 11 };
        add.Click += (_, _) => { rows.Add(Enumerable.Repeat("", cols).ToArray()); dirty = true; Save(); Draw(); };
        panel.Children.Add(add);
        Closed += (_, _) => Save();
        Draw();
        return panel;
    }

    /// <summary>
    /// Whether a status line says something the Enabled box does not: "Running." and "Off." are
    /// what the box shows, so only a problem, or a change waiting for the next start, is written.
    /// </summary>
    public static bool IsProblem(string status) => status is not ("Running." or "Off.");

    private static TextBlock Text(string s, IBrush colour) => new()
    {
        Text = s,
        Foreground = colour,
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>A bordered group: the heading (with <paramref name="tip"/> as its tooltip, when there is one), then the body.</summary>
    internal static Border Group(string caption, Control body, string tip = "") => Group(MakeHeading(caption, tip), body);

    internal static Border Group(Control heading, Control body) => new()
    {
        BorderBrush = Brush.Parse("#3a3a4e"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 8),
        Child = new StackPanel
        {
            Spacing = 6,
            Children = { heading, body },
        },
    };

    private static TextBlock MakeHeading(string caption, string tip)
    {
        var heading = new TextBlock { Text = caption, Foreground = Heading, FontWeight = FontWeight.Bold, FontSize = 11, Background = Brushes.Transparent };
        if (tip != "") ToolTip.SetTip(heading, tip);
        return heading;
    }

    /// <summary>The page's heading colour, for a mod's settings window in the same style.</summary>
    internal static IBrush HeadingBrush => Heading;
}
