using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// A mod's own settings window (mod.json <c>settingsWindow</c>, <see cref="ModSettingsWindowSpec"/>,
/// API version 11), opened from the Settings... button on the Mods page - the main menu's and the
/// in-game one alike, also while the mod is off, since these are settings and not the mod switched on
/// or off. Laid out as the Mods page is (the Options window's style).
///
/// Each group is a bordered box: its heading, then its settings, in the order the manifest names
/// them - a toggle as a check box, a choice as radio buttons - each with its description as the
/// tooltip. A group holding toggles has a check box on its heading that shows them all (ticked when
/// all are on, empty when all are off, mixed otherwise); a click on it switches them all on, or all
/// off when they are all on already. Every change is written to settings.ini at once and a mod that
/// reads its settings as it goes (as the Quality of Life mod does) follows at once; a change made
/// elsewhere while the window is up moves the boxes.
/// </summary>
public sealed class ModSettingsWindow : Window
{
    /// <summary>The group headings' boxes, by group label, for the checks.</summary>
    public Dictionary<string, CheckBox> GroupBoxes { get; } = new();

    public ModSettingsWindow(ModHost host, DiscoveredMod mod)
    {
        var spec = mod.Manifest.SettingsWindow ?? new ModSettingsWindowSpec();
        Title = spec.Title == "" ? mod.Manifest.Name + " settings" : spec.Title;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brush.Parse("#101018");
        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var loaded = host.Loaded.FirstOrDefault(l => l.Id == mod.Id);
        // a mod that is off has not brought settings.ini up to date since it was last written (it does
        // so as it loads): its own migration runs first - for one running it finds nothing to do -
        // so the boxes show what the mod will use
        host.MigrateSettings(mod);
        IReadOnlyList<ModToggleSpec> toggles = loaded?.Settings.Toggles ?? (IReadOnlyList<ModToggleSpec>)mod.Manifest.Settings;
        var body = new StackPanel { Spacing = 10, Width = 480 };

        foreach (var g in spec.Groups)
        {
            var list = new StackPanel { Spacing = 4, Margin = new Thickness(18, 0, 0, 0) };
            var boxes = new List<(CheckBox Box, ModToggleSpec Spec)>();
            foreach (var key in g.Keys)
            {
                if (toggles.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) is { } t)
                {
                    var box = ModsWindow.ToggleBox(this, host, mod.Id, t);
                    boxes.Add((box, t));
                    list.Children.Add(box);
                }
                else if (mod.Manifest.Choices.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)) is { Options.Count: > 0 } c)
                    list.Children.Add(ModsWindow.ChoiceEditor(this, host, mod.Id, c, new Thickness(0)));
            }

            Control heading;
            if (boxes.Count > 0)
            {
                // the group's own box: all, none, or mixed (null); a click switches them all
                var all = new CheckBox
                {
                    Content = g.Label,
                    IsThreeState = true,
                    Foreground = ModsWindow.HeadingBrush,
                    FontWeight = FontWeight.Bold,
                    FontSize = 12,
                };
                if (g.Description != "") ToolTip.SetTip(all, g.Description);
                bool? State()
                {
                    int on = boxes.Count(b => host.Settings.ReadFlag(mod.Id, b.Spec.Key, b.Spec.Default));
                    return on == boxes.Count ? true : on == 0 ? false : null;
                }
                void Show() => all.IsChecked = State();
                Show();
                // the click has toggled the box already (a three-state box would cycle through mixed):
                // the group's rule decides instead - all on, or all off when all are on
                all.Click += (_, _) =>
                {
                    bool turnOn = State() != true;
                    foreach (var (_, s) in boxes)
                        if (host.Settings.ReadFlag(mod.Id, s.Key, s.Default) != turnOn) host.Settings.WriteFlag(mod.Id, s.Key, turnOn);
                    foreach (var (b, _) in boxes) b.IsChecked = turnOn;
                    Show();
                };
                foreach (var (b, _) in boxes) b.IsCheckedChanged += (_, _) => Show();
                Action<string, string> follow = (m, k) =>
                {
                    if (string.Equals(m, mod.Id, StringComparison.OrdinalIgnoreCase) && boxes.Any(b => string.Equals(b.Spec.Key, k, StringComparison.OrdinalIgnoreCase)))
                        Avalonia.Threading.Dispatcher.UIThread.Post(Show);
                };
                host.Settings.Changed += follow;
                Closed += (_, _) => host.Settings.Changed -= follow;
                GroupBoxes[g.Label] = all;
                heading = all;
            }
            else
            {
                var text = new TextBlock { Text = g.Label, Foreground = ModsWindow.HeadingBrush, FontWeight = FontWeight.Bold, FontSize = 12, Background = Brushes.Transparent };
                if (g.Description != "") ToolTip.SetTip(text, g.Description);
                heading = text;
            }
            body.Children.Add(ModsWindow.Group(heading, list));
        }

        var close = new Button { Content = "_Close", IsDefault = true, IsCancel = true, Width = 120, HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        close.Click += (_, _) => Close();
        body.Margin = new Thickness(0, 0, 18, 0);
        Content = new Border
        {
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new ScrollViewer { MaxHeight = 620, Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
                    close,
                },
            },
        };
    }
}
