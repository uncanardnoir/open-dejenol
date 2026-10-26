using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// The UI slots of docs/MODDING.md that the game's windows share. With no mod loaded nothing here
/// adds anything, and the windows are the original's.
/// </summary>
internal static class ModSlots
{
    /// <summary>Marks a header cell a mod column put there, so the next redraw can take it away.</summary>
    private const string ModCell = "mod-party-column";

    /// <summary>
    /// The Party Information header's mod columns (the PartyRowExtras slot): whatever the last
    /// redraw added is taken off, and the columns the live mods show now are put after Option.
    /// Returns them, for <see cref="PartyRow"/> to fill each member's cells in the same order.
    /// </summary>
    public static List<(LoadedMod Mod, PartyColumn Column)> PartyHeader(Grid header, int baseColumns)
    {
        while (header.ColumnDefinitions.Count > baseColumns) header.ColumnDefinitions.RemoveAt(header.ColumnDefinitions.Count - 1);
        foreach (var old in header.Children.OfType<Control>().Where(c => Equals(c.Tag, ModCell)).ToList()) header.Children.Remove(old);
        var cols = ModHost.Active.ShownPartyColumns();
        for (int i = 0; i < cols.Count; i++)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition(PartyColumnWidth(cols[i].Column), GridUnitType.Pixel));
            var t = new TextBlock
            {
                // the header's own face, the form's (frmParty's lblLabel), inherited from the drawing
                Text = cols[i].Column.Header, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#888"), HorizontalAlignment = HorizontalAlignment.Right, Tag = ModCell,
            };
            Grid.SetColumn(t, baseColumns + i);
            header.Children.Add(t);
        }
        WidenWindowContents(header, cols.Count == 0 ? 0 : cols.Sum(c => PartyColumnWidth(c.Column)) + PartyRightRoom * CityWindow.PartyGrowthX);
        return cols;
    }

    /// <summary>
    /// The room a mod's Party Information heading keeps clear of the column before it (the heading
    /// is set right in its column, as Option's is in its own).
    /// </summary>
    public const double PartyHeadingGap = 4;

    /// <summary>
    /// The room kept at the drawing's right edge after the last mod column, as Leave is kept 4
    /// pixels in from it: the Next column's figures had run against the window's edge.
    /// </summary>
    public const double PartyRightRoom = 4;

    /// <summary>
    /// A mod column's width in Party Information: the width the mod asks for, which is in the
    /// default fonts' pixels and so is as much wider as the drawing is in a larger Options font
    /// (<see cref="CityWindow.PartyGrowthX"/>: "!Pinned" in bold had filled its bar), or its
    /// heading's width in the drawing's font (frmParty's, lblLabel's, through the Options fonts)
    /// and <see cref="PartyHeadingGap"/> when that is wider - a larger Options font had run "HP/SP"
    /// into "Option". The heading and every row use it, so the cells stay under their heading.
    /// </summary>
    public static double PartyColumnWidth(PartyColumn column) =>
        Math.Max(column.Width * CityWindow.PartyGrowthX, UiFonts.TextWidth(column.Header, DesignFonts.Party.Pass, UiFonts.Design(DesignFonts.Party, "lblLabel(0)")) + PartyHeadingGap);

    /// <summary>
    /// The Party Information contents are drawn at a fixed width (<see cref="CityWindow.PartyDrawnAt"/>)
    /// and scaled down to a narrower window. A mod column taken out of that width left Name, the one
    /// star column, a few pixels. So the contents are drawn wider by the mod columns' own width and
    /// scaled as a whole: every column keeps the room it has with no mod, and the window keeps the
    /// size the character saved for it. With no column shown the extra is nought and nothing changes.
    /// </summary>
    private static void WidenWindowContents(Control header, double extra)
    {
        if (header.FindLogicalAncestorOfType<ShrinkToFit>() is ShrinkToFit fit) { fit.ExtraWidth = extra; return; }
        // not in its window yet: once it is
        void Attached(object? s, Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
        {
            header.AttachedToLogicalTree -= Attached;
            if (header.FindLogicalAncestorOfType<ShrinkToFit>() is ShrinkToFit later) later.ExtraWidth = extra;
        }
        header.AttachedToLogicalTree += Attached;
    }

    /// <summary>One member's cells in the mod columns, after the row's own.</summary>
    /// <param name="monster">The monster table by id, for a cell that names one (a guild quest's).</param>
    /// <param name="item">The item table by id.</param>
    public static void PartyRow(Grid row, Character member, List<(LoadedMod Mod, PartyColumn Column)> cols, IBrush foreground,
                                Func<short, Monster?> monster, Func<short, Item?> item)
    {
        int first = row.ColumnDefinitions.Count;
        for (int i = 0; i < cols.Count; i++)
        {
            double width = PartyColumnWidth(cols[i].Column);
            row.ColumnDefinitions.Add(new ColumnDefinition(width, GridUnitType.Pixel));
            object? cell = ModHost.Active.PartyCell(cols[i].Mod, cols[i].Column, member, monster, item);
            // each cell exactly its column's width, the text set right in it, as the row's own
            // cells are (CityWindow.PartyRowGrid): what one row holds - a bold figure, a cell wider
            // than its column - never moves another's
            Control c = cell is Control drawn
                ? new Border { Width = width, HorizontalAlignment = HorizontalAlignment.Left, Child = drawn }
                : new TextBlock
                {
                    Text = cell?.ToString() ?? "", Foreground = foreground, Width = width,
                    HorizontalAlignment = HorizontalAlignment.Left, TextAlignment = TextAlignment.Right,
                };
            Grid.SetColumn(c, first + i);
            row.Children.Add(c);
        }
    }

    /// <summary>
    /// A pack line on the Items page with whatever a mod draws after it (ctx.UI.PackRowDecoration):
    /// the line itself when no mod answers, as it is with none.
    /// </summary>
    public static Control PackLine(Control line, Character ch, Item? item, InventorySlot slot, int index)
    {
        if (item == null) return line;
        return Decorated(line, ModHost.Active.PackRowDecoration(ch, item, slot, index));
    }

    /// <summary>
    /// A line of the Bank's Deposit/Withdraw Items list with whatever a mod draws after it
    /// (ctx.UI.BankRowDecoration): the plain text, as the list has it with no mod, when none answers.
    /// </summary>
    public static object BankLine(string text, Character ch, Item item, InventorySlot slot, int vaultSlot)
    {
        var d = ModHost.Active.BankRowDecoration(ch, item, slot, vaultSlot);
        return d == null ? text : Decorated(new TextBlock { Text = text }, d);
    }

    /// <summary>A line with a mod's decoration after it; the line itself for none.</summary>
    private static Control Decorated(Control line, PackRowDecoration? d)
    {
        if (d == null) return line;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { line } };
        // the piece, then whatever it says comes after it (Then), each with its own colour and tip
        for (int n = 0; d != null && n < 8; d = d.Then, n++)
        {
            if (string.IsNullOrEmpty(d.Suffix)) continue;
            var suffix = new TextBlock { Text = d.Suffix };
            if (line is TextBlock t) { suffix.FontWeight = t.FontWeight; suffix.FontStyle = t.FontStyle; }
            if (d.Colour is { } c) suffix.Foreground = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            if (!string.IsNullOrEmpty(d.Tooltip)) ToolTip.SetTip(suffix, d.Tooltip);
            panel.Children.Add(suffix);
        }
        return panel.Children.Count == 1 ? line : panel;
    }

    /// <summary>
    /// The room the Item column of the Store's Buy list is given on top of the mods' own columns
    /// while any is shown: the window grows by both, so the names keep more room than they have
    /// with none rather than less. With no Store column shown, nothing.
    /// </summary>
    public const double StoreItemRoom = 80;

    /// <summary>How far a Store column's text is set in from the column's left edge.</summary>
    public const double StoreCellInset = 4;

    /// <summary>
    /// The mods' Store columns as DataGrid columns, inserted at <paramref name="at"/> (after Item).
    /// Each cell asks its mod as it is drawn. Returns them, to be shown or hidden by <see cref="ShowStoreColumns"/>.
    /// </summary>
    /// <param name="line">A row's item and its ink (the Store's bold/grey), from the row object.</param>
    /// <param name="head">A heading control for a caption, in the list's own face; the cells take the list's face too.</param>
    public static List<(DataGridColumn Grid, LoadedMod Mod, StoreColumn Column)> AddStoreColumns(
        DataGrid grid, int at, Func<object?, (Item Item, IBrush Ink, FontWeight Weight)?> line,
        Func<Character> who, Func<short, Item?> lookup, Func<string, Control> head)
    {
        var host = ModHost.Active;
        var made = new List<(DataGridColumn, LoadedMod, StoreColumn)>();
        foreach (var (mod, col) in host.StoreColumns())
        {
            var (m, c) = (mod, col);
            var g = new DataGridTemplateColumn
            {
                Header = head(c.Header),
                Width = new DataGridLength(c.Width),
                CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((r, _) =>
                {
                    var t = new TextBlock
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Avalonia.Thickness(StoreCellInset, 0, 2, 0),
                    };
                    if (line(r) is not { } l) return t;
                    t.Foreground = l.Ink; t.FontWeight = l.Weight;
                    object? cell = host.StoreCell(m, c, new StoreCellQuery(who(), l.Item, lookup));
                    if (cell is ModCell mc)
                    {
                        t.Text = mc.Text;
                        if (mc.Colour is { } k) t.Foreground = new SolidColorBrush(Color.FromRgb(k.R, k.G, k.B));
                        if (!string.IsNullOrEmpty(mc.Tooltip)) ToolTip.SetTip(t, mc.Tooltip);
                    }
                    else t.Text = cell?.ToString() ?? "";
                    // the whole cell takes the pointer, so a tooltip shows past the text's end too
                    return new Border { Background = Brushes.Transparent, Child = t };
                }, supportsRecycling: false),
            };
            grid.Columns.Insert(at + made.Count, g);
            made.Add((g, m, c));
        }
        return made;
    }

    /// <summary>
    /// Each mod Store column shown or hidden as its mod says now. Returns the width the Store's
    /// window is to grow by: the shown columns' widths and <see cref="StoreItemRoom"/>, or nought with none shown.
    /// </summary>
    public static double ShowStoreColumns(List<(DataGridColumn Grid, LoadedMod Mod, StoreColumn Column)> cols)
    {
        double extra = 0;
        foreach (var (g, mod, col) in cols)
        {
            g.IsVisible = ModHost.Active.IsShown(mod, col);
            if (g.IsVisible) extra += col.Width;
        }
        return extra > 0 ? extra + StoreItemRoom : 0;
    }

    /// <summary>
    /// Run <paramref name="redraw"/> whenever a mod setting changes, while the window is open, when
    /// a mod is running at all; with none, nothing is subscribed and nothing is ever redrawn for it.
    /// </summary>
    public static void OnLiveModsChanged(Window window, Action redraw)
    {
        if (ModHost.Active.Live.Any()) OnModsChanged(window, redraw);
    }

    /// <summary>Run <paramref name="redraw"/> whenever a mod setting changes, for as long as the window is open.</summary>
    public static void OnModsChanged(Window window, Action redraw)
    {
        Action again = () => Avalonia.Threading.Dispatcher.UIThread.Post(redraw);
        ModHost.Active.Changed += again;
        window.Closed += (_, _) => ModHost.Active.Changed -= again;
    }

    /// <summary>
    /// The mods' check boxes for the General Store's page (ctx.UI.AddStoreOption), in a row, or null
    /// when no live mod adds one - the Store then builds the page it has with none. Each box shows
    /// its mod's answer, hands a click to the mod, and is looked at again (ticked, shown or hidden)
    /// whenever a mod setting changes, so it agrees with the Mods page.
    /// </summary>
    public static Control? StoreOptions(IBrush foreground, FontPass pass)
    {
        var host = ModHost.Active;
        var options = host.StoreOptions.ToList();
        if (options.Count == 0) return null;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        bool reading = false;
        var made = options.Select(pair =>
        {
            var (mod, opt) = pair;
            // in the Store's own face beside its heading (the "NonPro" role), and drawn as VB draws a
            // check box (FormPage.Compact): the theme's is a 20-pixel square at the top of a 32-pixel
            // row and a 120-pixel minimum width, which the heading's line cut off
            var box = FormPage.Compact(UiFonts.Set(new CheckBox
            {
                Content = opt.Label, Focusable = false, Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
            }, pass, DesignFonts.PlainSans));
            if (!string.IsNullOrEmpty(opt.Tooltip)) ToolTip.SetTip(box, opt.Tooltip);
            box.IsCheckedChanged += (_, _) =>
            {
                if (!reading) host.SetStoreOption(mod, opt, box.IsChecked == true);
            };
            row.Children.Add(box);
            return (mod, opt, box);
        }).ToList();

        void Read()
        {
            reading = true;
            try
            {
                foreach (var (mod, opt, box) in made)
                {
                    box.IsVisible = host.IsShown(mod, opt);
                    box.IsChecked = host.StoreOptionValue(mod, opt);
                }
            }
            finally { reading = false; }
        }
        Read();
        Action again = () => Avalonia.Threading.Dispatcher.UIThread.Post(Read);
        row.AttachedToVisualTree += (_, _) => { host.Changed -= again; host.Changed += again; Read(); };
        row.DetachedFromVisualTree += (_, _) => host.Changed -= again;
        return row;
    }

    /// <summary>
    /// The mods' Objects &amp; Spells commands, as buttons beside Equip, Use, Drop and Info and the
    /// size they are (frmCharStat3's cmdCommand, 870x300 twips). Each is shown or hidden as its
    /// mod's settings say, looked at again whenever a mod setting changes.
    /// </summary>
    /// <param name="panel">The Items page's column for them, above Equip: the form has no room below Info.</param>
    /// <param name="context">What the command is handed: built when the button is pressed.</param>
    /// <param name="changed">Save and redraw, when the command says the pack changed.</param>
    public static void AddObjectsCommands(Panel panel, Func<ObjectsCommandContext> context, Action changed)
    {
        var host = ModHost.Active;
        var made = host.ObjectsCommands.Select(pair =>
        {
            var (mod, cmd) = pair;
            // cmdCommand's size and font (MS Sans Serif 8.25 plain, through the Options fonts)
            var b = UiFonts.Set(new Button { Content = cmd.Label, Focusable = false, Width = 58, Height = 20 },
                                DesignFonts.CharStat3, UiFonts.Design(DesignFonts.CharStat3, "cmdCommand(0)"));
            b.Click += (_, _) =>
            {
                if (host.Run(mod, cmd, context())) changed();
            };
            panel.Children.Add(b);
            return (mod, cmd, b);
        }).ToList();
        if (made.Count == 0) return;

        void Show()
        {
            foreach (var (mod, cmd, b) in made) b.IsVisible = host.IsShown(mod, cmd);
        }
        Show();
        Action again = () => Avalonia.Threading.Dispatcher.UIThread.Post(Show);
        host.Changed += again;
        panel.AttachedToVisualTree += (_, _) => { host.Changed -= again; host.Changed += again; Show(); };
        panel.DetachedFromVisualTree += (_, _) => host.Changed -= again;
    }

    /// <summary>
    /// The mods' Bank commands (ctx.UI.AddBankCommand), as buttons in a row, or null when no live
    /// mod adds one - the Bank then has only the form's own controls. Each is shown or hidden as
    /// its mod's settings say, looked at again whenever a mod setting changes.
    /// </summary>
    /// <param name="context">What the command is handed: built when the button is pressed.</param>
    /// <param name="changed">Save and redraw, when the command says the vault changed.</param>
    /// <param name="pass">The Bank page's font pass (FormPage.Fonts).</param>
    public static StackPanel? BankCommands(Func<BankCommandContext> context, Action changed, FontPass pass)
    {
        var host = ModHost.Active;
        var commands = host.BankCommands.ToList();
        if (commands.Count == 0) return null;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        var made = commands.Select(pair =>
        {
            var (mod, cmd) = pair;
            // in the Bank's own plain face, as its Party Options buttons are
            var b = UiFonts.Set(new Button
            {
                Content = new TextBlock { Text = cmd.Label }, Focusable = false, MinWidth = 0, MinHeight = 0,
                Width = 58, Padding = new Avalonia.Thickness(0), FontWeight = FontWeight.Normal,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            }, pass, DesignFonts.PlainSans);
            b.Click += (_, _) =>
            {
                if (host.Run(mod, cmd, context())) changed();
            };
            row.Children.Add(b);
            return (mod, cmd, b);
        }).ToList();

        void Show()
        {
            foreach (var (mod, cmd, b) in made) b.IsVisible = host.IsShown(mod, cmd);
        }
        Show();
        Action again = () => Avalonia.Threading.Dispatcher.UIThread.Post(Show);
        row.AttachedToVisualTree += (_, _) => { host.Changed -= again; host.Changed += again; Show(); };
        row.DetachedFromVisualTree += (_, _) => host.Changed -= again;
        return row;
    }
}
