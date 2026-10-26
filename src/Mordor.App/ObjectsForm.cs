using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// Objects &amp; Spells, frmCharStat3, as its form data in MORDOR.EXE (RCDATA 10) lays it out. The
/// layout itself is in CityWindow.axaml and DungeonWindow.axaml, one copy each; what both build
/// in code is here. Every size is the form's, in twips over 15.
/// </summary>
public static class ObjectsForm
{
    /// <summary>
    /// The form's client, 4065x3105 twips. Form_Resize ties the height to the width, sizes the
    /// tab control to the client (sub07D4) and scales the fonts by the width (sub07C5,
    /// FRMCHAR3.BAS:408-444), so the whole form zooms with the window.
    /// </summary>
    public static readonly Size Client = new(4065 / 15, 3105 / 15);

    /// <summary>
    /// A pack row's pitch. sub2263 puts each lblItemStatus at the one above's Top + Height and
    /// lblItem at the same top (FRMCHAR3.BAS:666-678); lblItemStatus(0) is 235 twips tall.
    /// </summary>
    public static readonly double PackRowHeight = 235.0 / 15;

    /// <summary>
    /// The port's pack row: the Options font's line and <see cref="UiFonts.RowPad"/>, as every list's
    /// rows are (16 pixels with the default fonts), never less than the form's
    /// <see cref="PackRowHeight"/>. A deviation (docs/FIDELITY.md): a larger font makes the rows
    /// taller rather than cutting them.
    /// </summary>
    public static double PackRowPitch => UiFonts.Cached(nameof(PackRowPitch), () => UiFonts.RowHeight(DesignFonts.CharStat3, PackRowHeight,
        UiFonts.Design(DesignFonts.CharStat3, "lblItem(0)"), UiFonts.Design(DesignFonts.CharStat3, "lblItemStatus(0)")));

    /// <summary>The port's spell row, as <see cref="PackRowPitch"/>, never less than the form's <see cref="SpellRowHeight"/>.</summary>
    public static double SpellRowPitch => UiFonts.Cached(nameof(SpellRowPitch), () => UiFonts.RowHeight(DesignFonts.CharStat3, SpellRowHeight,
        UiFonts.Design(DesignFonts.CharStat3, "lblName(0)"), DesignFont.Default));

    /// <summary>
    /// One row of the Items page: lblItemStatus (30,-15 210x235, centred, in the form's bold face)
    /// with the equipped mark, and lblItem at 285 (MAINMOD2.BAS:5429-5433). <paramref name="line"/>
    /// is the item's line, with whatever a mod draws after it.
    /// </summary>
    public static Control PackRow(string status, Control line)
    {
        var row = new Panel { Height = PackRowPitch };
        row.Children.Add(new TextBlock
        {
            Text = status, Width = 14, Margin = new Thickness(2, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center, FontWeight = FontWeight.Bold,
        });
        line.Margin = new Thickness(19, 0, 0, 0);
        line.HorizontalAlignment = HorizontalAlignment.Left;
        line.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(line);
        return row;
    }

    /// <summary>
    /// The Spells page's bottom line in a larger Options font: lblLabel(0) "Spell" (105,2070
    /// 510x195) runs 30 twips into txtSpell (585,2025 1890x285), which the default fonts' caption
    /// never reaches, and a larger caption did - it touched the box. txtSpell then begins
    /// <see cref="LabelGap"/> after the caption and keeps its right edge, and is as tall as its
    /// font's line and its frame need (the theme had put a scroll bar's arrow in the box). With
    /// the default fonts nothing moves (a deviation in a larger font only, docs/FIDELITY.md).
    /// </summary>
    public static void FitSpellRow(TextBlock label, TextBox box)
    {
        var form = DesignFonts.CharStat3;
        double caption = UiFonts.TextWidth(label.Text ?? "", form.Pass, UiFonts.Design(form, "lblLabel(0)"));
        double left = Canvas.GetLeft(label) + caption + LabelGap, boxLeft = Canvas.GetLeft(box);
        if (left > boxLeft)
        {
            double right = boxLeft + box.Width;
            label.Width = caption;
            Canvas.SetLeft(box, left);
            box.Width = Math.Max(0, right - left);
        }
        // the text's line, the padding's nought and the frame's pixel above and below, and one to spare
        double tall = UiFonts.LineHeight(form, DesignFont.Default) + 3;
        if (tall > box.Height) box.Height = tall;
    }

    /// <summary>The room between a caption and the box it names (<see cref="FitSpellRow"/>).</summary>
    public const double LabelGap = 4;

    /// <summary>
    /// The Companions page (<see cref="CompanionsPage"/>) in a larger Options font: laid out
    /// <see cref="UiFonts.FormGrowth"/> wider and taller - the pictures, the names and hits, the
    /// carry line and the buttons, every left, top and size - and zoomed back into the page's own
    /// room, as a service page drawn to its form's data is (<see cref="FormPage"/>), so a longer
    /// name keeps to its label. With the default fonts nothing changes.
    /// </summary>
    public static void FitCompanionPage(Canvas page)
    {
        double g = UiFonts.FormGrowth(DesignFonts.CharStat3.Pass).X;
        if (g > 1 && page.Parent is ContentControl tab)
        {
            foreach (var c in page.Children)
            {
                Canvas.SetLeft(c, Canvas.GetLeft(c) * g);
                Canvas.SetTop(c, Canvas.GetTop(c) * g);
                if (!double.IsNaN(c.Width)) c.Width *= g;
                if (!double.IsNaN(c.Height)) c.Height *= g;
            }
            double w = page.Width, h = page.Height;
            var margin = page.Margin;
            page.Width = w * g;
            page.Height = h * g;
            page.Margin = default;
            tab.Content = null;
            tab.Content = new Viewbox
            {
                Stretch = Stretch.Uniform, Width = w, Height = h, Margin = margin,
                HorizontalAlignment = page.HorizontalAlignment, VerticalAlignment = page.VerticalAlignment,
                Child = page,
            };
        }
    }

    /// <summary>
    /// The tab control's room at the form's own client: <see cref="Client"/> less its margin
    /// (30 twips, 2 pixels, at the left, top and right and 1 below).
    /// </summary>
    public static readonly Size TabsAtClient = new(Client.Width - 4, Client.Height - 3);

    /// <summary>
    /// The room the Items page needs before its buttons can stand beside the list rather than over
    /// it: cmdCommand's 58 pixels and a pixel either side of the scroll bar.
    /// </summary>
    public const double BesideRoom = 60;

    /// <summary>
    /// The window made larger than the form's client: the original zooms the whole form, every
    /// control and font by the width (Form_Resize, FRMCHAR3.BAS:408-444), and so did the port, so
    /// a big Objects &amp; Spells drew its text far larger than every other window's and still showed
    /// only the form's ten rows, Equip to Info over the lower right of them. Instead the drawing stays
    /// at its own size, in the Options fonts as every other window (UiFonts), and the room goes to the
    /// lists - a GUI deviation (docs/FIDELITY.md): the pack list and its scroll bar take the extra
    /// height and width, and once there is <see cref="BesideRoom"/> to spare the buttons stand to the
    /// right of the scroll bar, so no row runs under them; the Spells list shows as many whole rows as
    /// fit, and the Companions page's right-hand and lower slots move out with it. At the form's own size nothing moves: the
    /// buttons stand over the list's lower right as bm14.png shows.
    ///
    /// The Spells list also keeps its five rows where the Options fonts make a row taller than the
    /// form's 14 pixels (a 16-pixel row cut the fifth in half): it takes the room the form leaves
    /// under Cast and Info, and the bottom line moves down with it.
    /// </summary>
    public static void Reflow(Control tabs, Canvas items, ListBox pack, ScrollBar packScroll, Control modButtons, Button[] commands,
                              Canvas spells, ListBox spellList, ScrollBar spellScroll, Canvas companions, CompanionsPage companionPage)
    {
        var at = new System.Collections.Generic.Dictionary<Control, Rect>();
        void Keep(Control c) => at[c] = new Rect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height);
        foreach (var page in new[] { items, spells, companions })
        {
            Keep(page);
            foreach (var c in page.Children) Keep(c);
        }
        var host = companions.Parent as Viewbox;
        Rect hostAt = host != null ? new Rect(0, 0, host.Width, host.Height) : default;
        double growth = host != null && host.Width > 0 ? companions.Width / host.Width : 1;

        void Place(double dw, double dh)
        {
            // Items: the page, the list and its bar the room; the buttons over the list's lower
            // right as the form has them, or beside the bar once there is room
            var page = at[items];
            items.Width = page.Width + dw;
            items.Height = page.Height + dh;
            var list = at[pack];
            bool beside = dw >= BesideRoom;
            pack.Width = list.Width + dw - (beside ? BesideRoom : 0);
            pack.Height = list.Height + dh;
            var bar = at[packScroll];
            Canvas.SetLeft(packScroll, bar.X + (pack.Width - list.Width));
            packScroll.Height = bar.Height + dh;
            double left = beside ? Canvas.GetLeft(packScroll) + bar.Width + 2 : at[commands[0]].X + dw;
            foreach (var b in commands)
            {
                Canvas.SetLeft(b, left);
                Canvas.SetTop(b, at[b].Y + dh);
            }
            Canvas.SetLeft(modButtons, left);   // Canvas.Bottom keeps them over Equip

            // Spells: whole rows, five at the least where the form's room under the buttons allows
            var sp = at[spells];
            spells.Height = sp.Height + dh;
            var sl = at[spellList];
            double pitch = SpellRowPitch, inset = 2, under = sp.Height - BottomOf(spells, at, sl.Bottom);
            int rows = Math.Max((int)Math.Floor((sl.Height - inset + dh) / pitch),
                                Math.Min(5, (int)Math.Floor((sl.Height - inset + under + dh) / pitch)));
            double tall = Math.Max(sl.Height, rows * pitch + inset);
            spellList.Height = tall;
            double down = tall - sl.Height;
            spellScroll.Height = at[spellScroll].Height + down;
            foreach (var c in spells.Children)
            {
                if (!at.TryGetValue(c, out var r)) continue;
                if (r.Y >= sl.Bottom) Canvas.SetTop(c, r.Y + down);
                // across, the room goes to the spell names: what stands right of lblName's column
                // (the Guild, Lev and Points headings, the bar, Cast and Info) moves over, and the
                // class box, the list and txtSpell widen
                if (c == spellList || c is ComboBox || c is TextBox) c.Width = r.Width + dw;
                else if (r.X >= NameColumnEnd) Canvas.SetLeft(c, r.X + dw);
            }
            spells.Width = sp.Width + dw;
            _nameWidth.AddOrUpdate(spellList, new System.Runtime.CompilerServices.StrongBox<double>(SpellNameWidth + dw));
            foreach (var row in spellList.Items.OfType<Grid>()) WidenName(row, spellList);

            // Companions: the right-hand and lower slots and the buttons move into the room
            // (CompanionsPage.Grow); drawn larger and zoomed back in a larger Options font
            // (FitCompanionPage), so the room is that much larger
            double cw = dw * growth, ch = dh * growth;
            var cp = at[companions];
            companions.Width = cp.Width + cw;
            companions.Height = cp.Height + ch;
            if (host != null) { host.Width = hostAt.Width + dw; host.Height = hostAt.Height + dh; }
            companionPage.Grow(cw, ch);
        }

        spellList.ContainerPrepared += (_, e) => { if (e.Container is ContentControl { Content: Grid row }) WidenName(row, spellList); };
        tabs.SizeChanged += (_, e) =>
        {
            double dw = Math.Max(0, Math.Floor(e.NewSize.Width - TabsAtClient.Width));
            double dh = Math.Max(0, Math.Floor(e.NewSize.Height - TabsAtClient.Height));
            Place(dw, dh);
        };
    }

    /// <summary>lblName's column in a spell row: from lblName (30 twips) to lblGuild (1815) (<see cref="SpellPage"/>).</summary>
    public const double SpellNameWidth = (1815 - 30) / 15;

    /// <summary>
    /// The page's left past which a control stands right of the names: the "Guild" heading's left
    /// (125), with "Spell Name", "Spell Type" and the "Spell" caption short of it.
    /// </summary>
    private const double NameColumnEnd = 120;

    /// <summary>The spell-name column each Spells list's rows are given (<see cref="Reflow"/>).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListBox, System.Runtime.CompilerServices.StrongBox<double>> _nameWidth = new();

    private static void WidenName(Grid row, ListBox list)
    {
        if (row.ColumnDefinitions.Count == 4) row.ColumnDefinitions[0].Width = new GridLength(_nameWidth.TryGetValue(list, out var w) ? w.Value : SpellNameWidth);
    }

    /// <summary>The lowest bottom on a page of the controls below a list (the Spells page's bottom line).</summary>
    private static double BottomOf(Canvas page, System.Collections.Generic.Dictionary<Control, Rect> at, double below)
    {
        double bottom = below;
        foreach (var c in page.Children)
            if (at.TryGetValue(c, out var r) && r.Y >= below && !double.IsNaN(r.Height)) bottom = Math.Max(bottom, r.Bottom);
        return bottom;
    }

    /// <summary>The spell rows' pitch: lblName(0) is 210 twips tall and Form_Load stacks five (FRMCHAR3.BAS:358-369).</summary>
    public static readonly double SpellRowHeight = 210.0 / 15;

    /// <summary>
    /// vsbItems or vsbSpells beside its list: the bar scrolls the rows a row a step (vsbItems_Change
    /// moves picItems, FRMCHAR3.BAS:872-879; vsbSpells_Change moves the spells, 881-912), and it
    /// follows the list however that was scrolled - the wheel, the keys, a selection brought into view.
    /// </summary>
    /// <remarks>
    /// The list's own ScrollViewer is the one place the position lives; the bar only shows it and
    /// asks to move it. So the link is one-way each way, and each way has its own trigger:
    /// <list type="bullet">
    /// <item>the list to the bar on the viewer's ScrollChanged, the bar's range and position written
    /// with SetCurrentValue, range first, so a Value is never coerced against a stale Maximum;</item>
    /// <item>the bar to the list only on the bar's <see cref="ScrollBar.Scroll"/> event, which
    /// ScrollBar raises for a person's use of it alone - an arrow, a click in the track (a page),
    /// the thumb dragged - never for a Value it was handed or had coerced.</item>
    /// </list>
    /// It had written the viewer's Offset, as a local value, on every change of the bar's Value that
    /// came outside its own update. Then any Value the bar took that was not the list's - a coercion
    /// as its range moved, its Track's two-way binding writing back, a value kept from a range the
    /// list no longer has - would have gone straight back into the list and moved it to wherever the
    /// bar thought it was. Only what a person does to the bar reaches the list now.
    /// </remarks>
    /// <param name="wholeRows">
    /// The list stops only on a whole row, as a VB scroll bar's Value is a whole row (the Spells
    /// page, vsbSpells_Change, FRMCHAR3.BAS:881-912): the Spells list, brought down to its last
    /// spell, had left the lower half of a row showing at its top under the Spell Name heading. The
    /// rows' panel is given the room below its last row that makes the list's bottom a whole row
    /// from its top (<see cref="WholeRows.Tail"/>), and a scroll that stops between rows is taken to
    /// the nearest.
    /// </param>
    public static void LinkScrollBar(ListBox list, ScrollBar bar, double rowHeight, bool wholeRows = false)
    {
        ScrollViewer? viewer = null;
        bool busy = false;
        bool snapPosted = false;
        void Snap()
        {
            snapPosted = false;
            if (viewer == null || rowHeight <= 0) return;
            if (list.ItemsPanelRoot is Panel rows)
            {
                double tail = WholeRows.Tail(viewer.Viewport.Height, rows.Margin.Top, rowHeight);
                if (Math.Abs(rows.Margin.Bottom - tail) > 0.01)
                {
                    rows.Margin = new Thickness(rows.Margin.Left, rows.Margin.Top, rows.Margin.Right, tail);
                    return;   // the extent changes, and ScrollChanged brings this back
                }
            }
            double max = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
            double at = viewer.Offset.Y;
            double whole = WholeRows.Snap(at, rowHeight, max);
            if (Math.Abs(whole - at) > 0.01)
                viewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(viewer.Offset.X, whole));
        }
        void FromViewer()
        {
            if (wholeRows && !snapPosted)
            {
                snapPosted = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(Snap, Avalonia.Threading.DispatcherPriority.Background);
            }
            if (viewer == null || busy) return;
            busy = true;
            try
            {
                double max = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
                bar.SetCurrentValue(RangeBase.MaximumProperty, max);
                bar.SetCurrentValue(ScrollBar.ViewportSizeProperty, viewer.Viewport.Height);
                bar.SetCurrentValue(RangeBase.SmallChangeProperty, rowHeight);
                bar.SetCurrentValue(RangeBase.LargeChangeProperty, Math.Max(rowHeight, viewer.Viewport.Height));
                bar.SetCurrentValue(RangeBase.ValueProperty, Math.Clamp(viewer.Offset.Y, 0, max));
            }
            finally { busy = false; }
        }
        list.TemplateApplied += (_, e) =>
        {
            viewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            if (viewer == null) return;
            var mine = viewer;
            // only this viewer's own scrolling: ScrollChanged bubbles, and a list re-templated
            // leaves its old viewer behind
            mine.ScrollChanged += (_, ev) => { if (ev.Source == mine && viewer == mine) FromViewer(); };
            FromViewer();
        };
        bar.Scroll += (_, e) =>
        {
            if (busy || viewer == null) return;
            busy = true;
            try { viewer.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(viewer.Offset.X, e.NewValue)); }
            finally { busy = false; }
        };
    }
}
