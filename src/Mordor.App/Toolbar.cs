using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mordor.Core;

namespace Mordor.App;

/// <summary>What a toolbar button asks for. The indices are <c>cmdTool</c>'s own.</summary>
public enum ToolCommand
{
    /// <summary>cmdTool(0): "Selecting this will load the Mordor Helplesson".</summary>
    Help = 0,
    /// <summary>cmdTool(1), F5: "the current characters experience and quest information".</summary>
    Experience = 1,
    /// <summary>cmdTool(2), F8.</summary>
    Automap = 2,
    /// <summary>cmdTool(3), 'a'.</summary>
    AskToJoin = 3,
    /// <summary>cmdTool(4), 'g'.</summary>
    GiveItem = 4,
    /// <summary>cmdTool(5), 'p'.</summary>
    PickupBody = 5,
    /// <summary>cmdTool(6), 'l'.</summary>
    DropBody = 6,
    /// <summary>cmdTool(7), F10.</summary>
    ControlPanel = 7,
    /// <summary>cmdTool(8): the page toggle, the one button the info page keeps.</summary>
    Page = 8,
}

/// <summary>
/// <c>frmMain!picToolbar</c>, the strip across the top of the frame. "Depending on the page mode,
/// the Toolbar is a collection of icons that allow the player to carry out commands at the click of
/// a button, or a display of character information" (help, The Toolbar).
///
/// <c>sub1585</c> (MODULE6.BAS:5713) is the toggle: on the info page it hides cmdTool 0 to 7 and
/// leaves 8, the page button, so there is always a way back. <c>sub08B9</c> then paints the eleven
/// resistances across the strip in two rows - the first three letters above, the percentage or a
/// dash below. <c>sub166D</c> writes the two readouts on the icon page.
///
/// The icons are the game's own, bm17 to bm25 out of the help file, in the order the help lists
/// them: Page, Help, Guild Experience, AutoMap, Ask to Join, Give Item, Pickup Body, Drop Body,
/// Control Panel.
///
/// One piece of dead code is not carried: <c>cmdTool_Click</c> has four more cases, for F2, F4, F6
/// and F7, whose Case labels are uninitialised Variants. They evaluate to zero, which the Help case
/// has already matched, so VB never reaches them.
/// </summary>
public sealed class Toolbar : UserControl
{
    /// <summary>Raised when a button is pressed. The window decides what that means.</summary>
    public event Action<ToolCommand>? Invoked;

    private readonly List<Button> _commands = new();
    private readonly StackPanel _icons;
    private readonly StackPanel _readouts;
    private readonly TextBlock _gold, _experience;
    private readonly Grid _resistances;

    private bool _infoPage;

    public Toolbar()
    {
        Background = Brush.Parse("#1a1a24");

        // No tooltips: VB3 buttons have no such property, and frmMain has no cmdTool_MouseMove to
        // put a hint anywhere either - only Click, KeyDown and KeyPress (FRMMAIN.BAS:48-120). The
        // help's Toolbar page is where the icons are explained. A mod may supply them
        // (ctx.UI.ToolbarTooltip, docs/MODDING.md); with none, there are none.
        var page = IconButton("tool_page.png", ToolCommand.Page);
        _all.Add((page, ToolCommand.Page));
        _icons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (file, cmd) in new (string, ToolCommand)[]
        {
            ("tool_help.png", ToolCommand.Help),
            ("tool_experience.png", ToolCommand.Experience),
            ("tool_automap.png", ToolCommand.Automap),
            ("tool_join.png", ToolCommand.AskToJoin),
            ("tool_give.png", ToolCommand.GiveItem),
            ("tool_pickup.png", ToolCommand.PickupBody),
            ("tool_dropbody.png", ToolCommand.DropBody),
            ("tool_controlpanel.png", ToolCommand.ControlPanel),
        })
        {
            var b = IconButton(file, cmd);
            _commands.Add(b);
            _all.Add((b, cmd));
            _icons.Children.Add(b);
        }
        // a button per window a mod offers (ctx.UI.AddWindow, docs/MODDING.md); none without mods
        ModWindows.AddButtons(_icons);

        // a mod's tooltips are asked for again whenever a mod setting changes
        Action retip = () => Avalonia.Threading.Dispatcher.UIThread.Post(ApplyModTips);
        AttachedToVisualTree += (_, _) => { Mordor.Modding.ModHost.Active.Changed += retip; ApplyModTips(); };
        DetachedFromVisualTree += (_, _) => Mordor.Modding.ModHost.Active.Changed -= retip;

        _gold = Readout("#e8e0c0");
        _experience = Readout("#8fd9d9");
        _readouts = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Children = { _gold, _experience },
        };

        // eleven resistances in two rows, as sub08B9 paints them
        _resistances = new Grid
        {
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };
        for (int i = 0; i < Librarian.Resistances.Length; i++)
        {
            _resistances.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            // sub08B9 prints these on picToolbar, whose font is Courier New 8.25 bold (MORDOR.EXE,
            // frmMain): the "Pro" role, through the Options fonts (UiFonts; frmMain itself is not
            // restyled in the original - a deviation, docs/FIDELITY.md)
            var name = UiFonts.Set(new TextBlock
            {
                Text = Librarian.Resistances[i][..Math.Min(3, Librarian.Resistances[i].Length)],
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#8fb0ff"),
                Margin = new Thickness(0, 0, 8, 0),
                [Grid.ColumnProperty] = i,
            }, DesignFonts.Main, "picToolbar");
            var value = UiFonts.Set(new TextBlock
            {
                Text = " -",
                Foreground = Brushes.Gainsboro,
                Margin = new Thickness(0, 0, 8, 0),
                [Grid.ColumnProperty] = i,
                [Grid.RowProperty] = 1,
            }, DesignFonts.Main, "picToolbar");
            _resistances.Children.Add(name);
            _resistances.Children.Add(value);
        }

        Content = new Border
        {
            BorderBrush = Brush.Parse("#2a2a38"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(6, 3),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Children = { page, _icons, _readouts, _resistances },
            },
        };
    }

    /// <summary>Every button with its command, the page button first.</summary>
    private readonly List<(Button Button, ToolCommand Command)> _all = new();

    /// <summary>
    /// Each button as the help's Toolbar page names it, with the key that page says does the same
    /// ("This functions the same as pressing F8"). Help's key is the Game Commands page's
    /// "F1  HelpLesson"; the Page button has none. This is what a mod's tooltip is built from.
    /// </summary>
    public static readonly IReadOnlyDictionary<ToolCommand, (string HelpName, string? Key)> HelpNames =
        new Dictionary<ToolCommand, (string, string?)>
        {
            [ToolCommand.Page] = ("Page", null),
            [ToolCommand.Help] = ("Help", "F1"),
            [ToolCommand.Experience] = ("Guild Experience Information", "F5"),
            [ToolCommand.Automap] = ("AutoMap", "F8"),
            [ToolCommand.AskToJoin] = ("Ask Character to Join", "a"),
            [ToolCommand.GiveItem] = ("Give Item", "g"),
            [ToolCommand.PickupBody] = ("Pickup Body", "p"),
            [ToolCommand.DropBody] = ("Drop Body", "l"),
            [ToolCommand.ControlPanel] = ("Control Panel", "F10"),
        };

    /// <summary>Put the mods' tooltips on the buttons, or take them off when no mod gives one.</summary>
    private void ApplyModTips()
    {
        foreach (var (b, cmd) in _all)
        {
            var (name, key) = HelpNames[cmd];
            ToolTip.SetTip(b, Mordor.Modding.ModHost.Active.ToolbarTooltip(new Mordor.Modding.ToolbarButton(cmd.ToString(), name, key)));
        }
    }

    private Button IconButton(string file, ToolCommand cmd)
    {
        var b = new Button
        {
            Focusable = false,           // the keyboard belongs to the game
            Padding = new Thickness(2),
            Width = 26,
            Height = 26,
            Background = Brush.Parse("#2a2a38"),
            Content = new Image
            {
                Width = 20,
                Height = 20,
                Source = Art.Bitmap(file),
            },
        };
        b.Click += (_, _) =>
        {
            if (cmd == ToolCommand.Page) InfoPage = !InfoPage;
            else Invoked?.Invoke(cmd);
        };
        return b;
    }

    // lblGold and lblExperience, MS Sans Serif 8.25 (MORDOR.EXE, frmMain): the "NonPro" role
    private static TextBlock Readout(string colour) => UiFonts.Set(new TextBlock
    {
        Foreground = Brush.Parse(colour),
    }, DesignFonts.Main, "lblGold");

    /// <summary>
    /// Which of the two pages is showing. The page button stays whichever it is - the original
    /// hides cmdTool 0 to 7 and no more.
    /// </summary>
    public bool InfoPage
    {
        get => _infoPage;
        set
        {
            _infoPage = value;
            _icons.IsVisible = !value;
            _readouts.IsVisible = !value;
            _resistances.IsVisible = value;
        }
    }

    /// <summary>
    /// <c>sub166D</c> case 10: "You have a total of N Gold." - M58AF + M4F2D, which is what is on
    /// hand plus what is in the bank, not just the coins you are carrying.
    /// </summary>
    public void ShowGold(Character c) =>
        _gold.Text = $"You have a total of {CharStatFile.FormatNumber(c.Purse)} Gold.";

    /// <summary>
    /// <c>sub166D</c> case 9. Shorter than what F5 writes into the Messages window: no gold, and
    /// "You are Pinned." on its own.
    /// </summary>
    public void ShowExperience(Character c)
    {
        var (state, amount) = Progress.ToolbarNeed(c);
        _experience.Text = state switch
        {
            Progress.NeedState.Short => $"You need {amount:N0} more experience for level {c.Guild.Level + 1}.",
            Progress.NeedState.Pinned => "You are Pinned.",
            _ => $"You need {amount:N0} more experience to pin.",
        };
    }

    /// <summary><c>sub08B9</c>: the percentage, or a dash where there is none.</summary>
    public void ShowResistances(Character c, Func<short, Item?> item)
    {
        foreach (var child in _resistances.Children)
        {
            if (child is not TextBlock t || Grid.GetRow(t) != 1) continue;
            int i = Grid.GetColumn(t);
            int pct = Effects.ResistancePercent(c, i, item);
            t.Text = pct > 0 ? pct.ToString() : " -";
        }
    }

    /// <summary>Everything the strip shows, for whoever has the focus now.</summary>
    public void Show(Character c, Func<short, Item?> item)
    {
        ShowGold(c);
        ShowExperience(c);
        ShowResistances(c, item);
    }
}
