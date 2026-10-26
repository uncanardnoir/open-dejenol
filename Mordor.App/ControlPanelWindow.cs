using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The Control Panel, <c>FRMCONTR</c>. "A character can access the Control Panel by Pressing F10 in
/// either the City or Dungeon" (help, Control Panel), and the help's own screenshot of it is
/// game_files/bm0.png: Character Options across the top, Window Control beneath, and Reset,
/// Adjust to Display and Show down the right of the list.
///
/// Two different things are being edited here and they are kept in two different places, which is
/// the original's arrangement and not a tidy one:
///
/// - the four Character Options are written onto the character with the focus (<c>gv1A86</c>)
/// - the window sizes are written onto the party leader, slot 0 of the character array, whichever
///   character happens to have the focus
///
/// The list is captioned with each window's own title, which is why the screenshot shows the
/// character window as <c>"Crashland"</c> - that is the name of the character whose game it was.
/// </summary>
public sealed class ControlPanelWindow : Window
{
    /// <summary>Raised when the Game Options window was accepted, so the frame can catch up.</summary>
    public event Action? OptionsChanged;

    /// <summary>The first row of the list, which means every window at once.</summary>
    public const string All = "<ALL>";

    private readonly Character _options;      // gv1A86, the character with the focus
    private readonly Character _leader;       // slot 0, which carries M5958
    private readonly List<(MdiChild Child, GameWindow Which)> _windows;

    /// <summary>
    /// The original's <c>Screen</c>. It fills the display there because the MDI parent does; here
    /// the parent is a window, so this is the frame the children have to fit into.
    /// </summary>
    private readonly double _frameWidth, _frameHeight;

    /// <summary>The game-wide settings the Game Options button edits, and where they are kept.</summary>
    private readonly GameOptions _game;
    private readonly string _gameDir;

    private readonly CheckBox _hide, _preload, _noPerception, _autoChange;
    private readonly ListBox _list;

    public ControlPanelWindow(Character options, Character leader,
                              IEnumerable<(MdiChild Child, GameWindow Which)> windows,
                              double frameWidth, double frameHeight,
                              GameOptions game, string gameDir)
    {
        _options = options;
        _leader = leader;
        _windows = windows.ToList();
        _frameWidth = frameWidth;
        _frameHeight = frameHeight;
        _game = game;
        _gameDir = gameDir;

        Title = "Control Panel";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        Background = Brush.Parse("#101018");
        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // the labels are the form's own, down to the abbreviations
        _hide = Check("_Hide", _options.Options.Hide);
        _preload = Check("_Preload Char.", _options.Options.PreloadCharacter);
        _noPerception = Check("_No Perception Msgs.", _options.Options.NoPerceptionMessages);
        _autoChange = Check("_Auto Change Lev", _options.Options.AutoChangeLevel);

        _list = new ListBox
        {
            Height = 132,
            Background = Brush.Parse("#0a0a10"),
            FontSize = 12,
            ItemsSource = new[] { All }.Concat(_windows.Select(w => w.Child.Title)).ToList(),
            SelectedIndex = 0,
        };

        var reset = Bar("_Reset", OnReset);
        var adjust = Bar("Adjust to _Display", async () => await OnAdjust());
        var show = Bar("_Show", OnShow);

        // "This will display the Game Options screen, allowing the user to set volume, etc."
        // FRMCONTR.BAS:67-73 shows it modally and then puts the toolbar where the answer says.
        var gameOptions = new Button
        {
            Content = "Game Options...",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        gameOptions.Click += async (_, _) =>
        {
            var win = new OptionsWindow(_game, _gameDir, inGame: true);   // gv1B52: a character is in play
            await win.ShowDialog(this);
            if (win.Accepted) OptionsChanged?.Invoke();
        };

        // the port's own: the Mods page (docs/MODDING.md), so a mod's toggles can be changed in a
        // game, where the main menu's Mods button cannot be reached. Not on the form of bm0.png. A
        // mod's own settings take effect at once; switching a mod on or off, at the next start
        var mods = new Button
        {
            Content = "_Mods...",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        mods.Click += async (_, _) =>
        {
            try { await new ModsWindow(atMainMenu: false).ShowDialog(this); }
            catch (Exception ex) { CrashLog.Write("the Mods page", ex); }
        };

        var ok = new Button { Content = "_OK", IsDefault = true, Width = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => { Accept(); Close(); };
        var cancel = new Button { Content = "_Cancel", IsCancel = true, Width = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close();

        Content = new Border
        {
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Group("Character Options", new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                        Children =
                        {
                            At(_hide, 0, 0), At(_autoChange, 0, 1),
                            At(_preload, 1, 0),
                            At(_noPerception, 2, 0),
                        },
                    }),
                    Group("Window Control", new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,12,Auto"),
                        Children =
                        {
                            _list,
                            new StackPanel
                            {
                                [Grid.ColumnProperty] = 2,
                                Spacing = 8,
                                Width = 160,
                                Children = { reset, adjust, show },
                            },
                        },
                    }),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,10,*"),
                        Children = { gameOptions, At(mods, 0, 2) },
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = { ok, cancel },
                    },
                },
            },
        };
    }

    private static Control At(Control c, int row, int col)
    {
        c[Grid.RowProperty] = row;
        c[Grid.ColumnProperty] = col;
        return c;
    }

    private static CheckBox Check(string text, bool value) =>
        new() { Content = text, IsChecked = value, Foreground = Brushes.Gainsboro, Margin = new Thickness(0, 2) };

    private static Button Bar(string text, Action click)
    {
        var b = new Button
        {
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Group(string caption, Control body) => new()
    {
        BorderBrush = Brush.Parse("#3a3a4e"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 8),
        Child = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = caption, Foreground = Brush.Parse("#8fb0ff"), FontWeight = FontWeight.Bold },
                body,
            },
        },
    };

    /// <summary>
    /// Which window the list is pointing at, or null for &lt;ALL&gt;. A window with no record in
    /// the table - the city is the one - comes back as its own negative index, which Reset and
    /// Adjust to Display then match nothing against, exactly as <c>sub074E</c> fails to find it.
    /// </summary>
    private int? Picked()
    {
        int i = _list.SelectedIndex;
        if (i <= 0) return null;
        return (int)_windows[i - 1].Which;
    }

    /// <summary>
    /// OK: the four checkboxes go onto the character (FRMCONTR.BAS:152-158). The window sizes have
    /// already been written by the buttons that changed them, which is how the original works too -
    /// Cancel does not put a resized window back.
    /// </summary>
    private void Accept()
    {
        _options.Options.Hide = _hide.IsChecked == true;
        _options.Options.PreloadCharacter = _preload.IsChecked == true;
        _options.Options.NoPerceptionMessages = _noPerception.IsChecked == true;
        _options.Options.AutoChangeLevel = _autoChange.IsChecked == true;
    }

    /// <summary>
    /// "With this section, a player can reset the size of one or all windows back to the original
    /// size" (help). Back to the size the form was drawn at, with no scaling for the screen -
    /// <c>sub074E</c> (MAINMOD.BAS:1685-1740).
    ///
    /// It writes to a character nothing reads back, and that is reproduced. Every *reader* of this
    /// table uses character 0 - all fourteen forms. The writers disagree: twelve forms save through
    /// <c>sub1614 -1, ...</c>, which writes to every character in the party; the automap and
    /// Adjust to Display write to character 0; and Reset writes <c>Chars(gv1A82)</c>
    /// (MODULE2.BAS:1718).
    ///
    /// <c>gv1A82</c> is the last character *fully* switched to, not the one on screen - that is
    /// <c>gv1A86</c>. Double clicking a party name calls <c>sub1536</c>, which moves both; single
    /// clicking calls <c>sub178B</c>, which leaves <c>gv1A82</c> behind.
    ///
    /// Alone, or with the leader in focus, the two are the same and nothing is wrong. Otherwise
    /// Reset resizes the windows on screen and files the new sizes under a character nobody reads
    /// them from, so they are gone on the next load. See docs/FIDELITY.md.
    /// </summary>
    private void OnReset()
    {
        WindowLayout.Reset(_options.Windows, Picked());
        // the resize happens either way: sub074E sets the form's own Width and Height directly,
        // whoever it just saved them to
        ApplyToChildren();
    }

    /// <summary>
    /// "One can also resize (adjust) windows to match the current resolution by clicking on the
    /// Adjust to Display button" (help). The original asks about Large Fonts first and takes Cancel
    /// as "do nothing" (FRMCONTR.BAS:73-77).
    /// </summary>
    private async Task OnAdjust()
    {
        var answer = await LargeFonts();
        if (answer == null) return;
        WindowLayout.AdjustToDisplay(_leader.NormaliseWindows(),_frameWidth, _frameHeight, answer.Value, Picked());
        ApplyToChildren();
    }

    /// <summary>
    /// Show: bring that window to the front, and the Control Panel back in front of it
    /// (FRMCONTR.BAS:186-195). &lt;ALL&gt; is not a window, so it does nothing.
    /// </summary>
    private void OnShow()
    {
        int i = _list.SelectedIndex;
        if (i <= 0) return;
        var child = _windows[i - 1].Child;
        child.Minimised = false;
        child.Reopen();
    }

    /// <summary>Puts the new sizes onto the windows that are actually on the frame.</summary>
    private void ApplyToChildren()
    {
        foreach (var (child, which) in _windows)
        {
            var r = _leader.WindowFor(which);
            if (!r.HasSize) continue;
            var at = child.Placement();
            child.Maximised = false;
            child.Minimised = false;
            child.PlaceAt(new Rect(at.X, at.Y,
                                   Math.Max(child.MinWidth, r.Width),
                                   Math.Max(child.MinHeight, r.Height)));
        }
    }

    /// <summary>
    /// <c>MsgBox "Are you using Large Fonts?", vbYesNoCancel</c>. Yes divides the scale by 1.25;
    /// Cancel abandons the whole thing.
    /// </summary>
    private async Task<bool?> LargeFonts()
    {
        bool? answer = null;
        var yes = new Button { Content = "Yes", Width = 90 };
        var no = new Button { Content = "No", Width = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        var win = new Window
        {
            Title = "Adjust Windows to Display",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            Background = Brush.Parse("#101018"),
            FontFamily = FontFamily,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Are you using Large Fonts?",
                            Foreground = Brushes.Gainsboro,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children = { yes, no, cancel },
                        },
                    },
                },
            },
        };
        yes.Click += (_, _) => { answer = true; win.Close(); };
        no.Click += (_, _) => { answer = false; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        await win.ShowDialog(this);
        return answer;
    }
}
