using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The bookkeeping a frame does for its child windows: which game window each one is, where the
/// character left it, and what the Control Panel is allowed to list.
///
/// In the original there is only one frame - <c>frmMain</c> - and the city and the dungeon are
/// children of it alongside the character window, Objects &amp; Spells, Party Information and
/// Messages. The port has one frame window too: the dungeon's children are put into the city's
/// window in place of the town's for as long as the party is below (see
/// <see cref="DungeonWindow.RunInside"/>). Each screen still builds its own set of children, and
/// this does the part that is the same either way.
/// </summary>
public sealed class MdiFrame
{
    private readonly MdiHost _host;
    private readonly Panel? _shed;
    private readonly Dictionary<MdiChild, GameWindow> _which = new();
    private readonly HashSet<MdiChild> _unsaved = new();

    public MdiFrame(MdiHost host, Panel? shed = null)
    {
        _host = host;
        _shed = shed;
    }

    public MdiHost Host => _host;

    /// <summary>
    /// sub150A for a character whose windows have never been placed (see
    /// <see cref="WindowLayout.Arrange"/>). The positions are worked out on the sizes the children
    /// are really drawn at - a window that keeps its proportions takes the height its width asks
    /// for (<see cref="WindowLayout.KeepsAspect"/>), as its Form_Resize gives it the moment it is
    /// shown - so they sit beside one another. Only the positions and states are written to the
    /// character; the sizes stay as they were seeded.
    /// </summary>
    private void ArrangeFresh(Character leader, Screens? screens)
    {
        var drawn = leader.Windows.Select(r => r == null ? new WindowRect()
            : new WindowRect { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height, State = r.State }).ToArray();
        for (int i = 0; i < drawn.Length && i < WindowLayout.Count; i++)
        {
            var r = drawn[i];
            var which = (GameWindow)i;
            var (mw, mh) = WindowLayout.Minimum(which);
            r.Width = Math.Max(mw, r.Width);
            if (WindowLayout.KeepsAspect(which)) r.Height = WindowLayout.AspectHeight(which, r.Width);
            r.Height = Math.Max(mh, r.Height);
        }
        // a window whose drawing a larger Options font made taller (the Dungeon, Party Information)
        // takes its own height for its width, so the windows arranged under it are not overlapped
        foreach (var (child, which) in Children)
            if (child.AspectHeight != null && (int)which < drawn.Length)
                drawn[(int)which].Height = Math.Max(WindowLayout.Minimum(which).H, child.AspectHeight(drawn[(int)which].Width));
        var (fw, fh) = OuterFrameSize();
        // extfn01D2 is GetSystemMetrics (MODULE1.BAS:23): SM_CYCAPTION 4 and SM_CYFRAME 33.
        // A mod's Party Information column shown: the Automap is kept off it (WindowLayout.Arrange)
        bool partyWidened = Mordor.Modding.ModHost.Active.ShownPartyColumns().Count > 0;
        WindowLayout.Arrange(drawn, fw, fh, SystemMetric(4), SystemMetric(33), partyWidened);
        for (int i = 0; i < drawn.Length && i < leader.Windows.Length; i++)
        {
            if (leader.Windows[i] == null) continue;
            leader.Windows[i].Left = drawn[i].Left;
            leader.Windows[i].Top = drawn[i].Top;
            leader.Windows[i].State = drawn[i].State;
        }
    }

    /// <summary>
    /// <c>Settings.M6049</c> and <c>M6039</c> (FRMMAIN.BAS:228-234): the frame's outer size when it
    /// is maximised, the screen's otherwise. A maximised window's borders lie off the edge of the
    /// screen, so what that size covers is the frame's inside: the toolbar and the children's
    /// area. The port hands over the same - its window's client area - whether or not it is
    /// maximised, for the reason the scale is measured against the frame (see <see cref="Size"/>):
    /// the screen would put Messages below the bottom of an ordinary window, and the window's
    /// outer size, with Windows 11's invisible borders in it, past its right edge.
    /// </summary>
    private (double W, double H) OuterFrameSize()
    {
        if (TopLevel.GetTopLevel(_host) is Window top && top.ClientSize.Width > 1 && top.ClientSize.Height > 1)
            return (top.ClientSize.Width, top.ClientSize.Height);
        return (_host.Bounds.Width, _host.Bounds.Height);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>GetSystemMetrics, in pixels; nought where there is no user32 to ask.</summary>
    private static double SystemMetric(int index)
    {
        try { return OperatingSystem.IsWindows() ? GetSystemMetrics(index) : 0; }
        catch { return 0; }
    }

    /// <summary>Every child, with the window it stands for.</summary>
    public IEnumerable<(MdiChild Child, GameWindow Which)> Children =>
        _which.Select(kv => (kv.Key, kv.Value));

    /// <summary>
    /// Takes a panel out of the Shed - the hidden holder the XAML writes them in, so they keep
    /// their x:Names and their Click handlers - and gives it a window of its own, with the form's
    /// own floor (<see cref="WindowLayout.Minimum"/>) and, where the form keeps its proportions,
    /// its height tied to its width.
    ///
    /// <paramref name="drawnAt"/> is the size this port's contents are laid out at, for a window
    /// whose contents are not drawn to the form's own design: made smaller than that, they are
    /// scaled down to fit (<see cref="ShrinkToFit"/>), as the original scales every control on a
    /// resize (sub0760, MAINMOD.BAS:1750-1848), rather than cut off.
    /// </summary>
    /// <param name="grow">Scale the contents up as well as down (<see cref="ShrinkToFit.Grow"/>):
    /// for contents drawn to the form's own design, which the original zooms either way.</param>
    public MdiChild Adopt(GameWindow which, string title, Control? panel, Size? drawnAt = null, bool grow = false)
    {
        if (panel != null) _shed?.Children.Remove(panel);
        // a form drawn to its own design that keeps its proportions zooms as its Form_Resize does:
        // by the width, with the Elastic spreading the controls down the height (ShrinkToFit.ByWidth)
        Control? body = panel != null && drawnAt is Size d
            ? new ShrinkToFit(panel, d) { Grow = grow, ByWidth = grow && WindowLayout.KeepsAspect(which) && panel is ElasticCanvas }
            : panel;
        var (dw, dh) = WindowLayout.Default(which);
        var child = _host.Add(title, body!, 0, 0, dw, dh);
        var (mw, mh) = WindowLayout.Minimum(which);
        child.MinWidth = mw;
        child.MinHeight = Math.Max(mh, 22);
        if (WindowLayout.KeepsAspect(which)) child.AspectHeight = w => WindowLayout.AspectHeight(which, w);
        _which[child] = which;
        return child;
    }

    /// <summary>
    /// A window of the port's own that has no entry in the table and is never saved: the services
    /// of the town, which the original draws as forms of their own (frmCityStore and the rest), as
    /// the port now does too (CityWindow.Services.cs).
    /// </summary>
    public MdiChild AdoptUnsaved(string title, Control panel, double width, double height)
    {
        _shed?.Children.Remove(panel);
        var child = _host.Add(title, panel, 0, 0, width, height);
        _unsaved.Add(child);
        return child;
    }

    /// <summary>
    /// What the original calls <c>Screen</c>. There it is the whole display, because the MDI parent
    /// fills it and the children live inside; here the parent is an ordinary window, so the frame is
    /// what a child has to fit into and scaling against the display would blow every window up by
    /// two or three times the room there is. Same arithmetic, measured from the right thing.
    /// </summary>
    public (double W, double H) Size(Screens? screens)
    {
        if (_host.Bounds.Width > 1 && _host.Bounds.Height > 1) return (_host.Bounds.Width, _host.Bounds.Height);
        var screen = screens?.Primary?.Bounds;
        return (screen?.Width ?? WindowLayout.BaselineWidth, screen?.Height ?? WindowLayout.BaselineHeight);
    }

    /// <summary>
    /// The part of the frame the window sizes are scaled against: the largest 800-by-600-shaped
    /// area inside it. CHARCREA and Adjust to Display divide the width by 800 and the height by
    /// 600 (CHARCREA.BAS:307-308, FRMCONTR.BAS:81-82) - a display of the shape the game was drawn
    /// for, where the two come out equal. Four windows then set their height from their width
    /// (<see cref="WindowLayout.KeepsAspect"/>), so on a frame of any other shape those four grow
    /// past the room sub150A laid out for them and cover the windows below. See docs/FIDELITY.md.
    /// </summary>
    public (double W, double H) ScaleArea(Screens? screens)
    {
        var (w, h) = Size(screens);
        double ratio = WindowLayout.BaselineWidth / WindowLayout.BaselineHeight;
        return (Math.Min(w, h * ratio), Math.Min(h, w / ratio));
    }

    /// <summary>
    /// sub0524's <c>Screen.Width</c> and <c>Screen.Height</c>: the whole primary display, in the
    /// units a child is placed in - its pixels over its scaling. Only where there is no display to
    /// ask does this fall back to the frame's own area.
    /// </summary>
    private (double W, double H) ScreenSize(Screens? screens)
    {
        if (screens?.Primary is Avalonia.Platform.Screen p && p.Bounds.Width > 0 && p.Bounds.Height > 0)
        {
            double s = p.Scaling > 0 ? p.Scaling : 1;
            return (p.Bounds.Width / s, p.Bounds.Height / s);
        }
        return (_host.Bounds.Width, _host.Bounds.Height);
    }

    /// <summary>
    /// Puts every child where the character left it. A character that has never had a layout is
    /// seeded here from <see cref="WindowLayout"/>, as CHARCREA does when one is made - it needs the
    /// frame's size, which is not known until the window is up.
    ///
    /// The original positions a window at <c>M425F</c> / <c>M4258</c>, and a new character's are
    /// laid out by <c>sub150A</c> straight after the sizes are seeded (CHARCREA.BAS:343) - see
    /// <see cref="WindowLayout.Arrange"/>. Each form's Form_Resize then holds it to its floor and,
    /// for four of them, sets its height from its width (<see cref="WindowLayout.Minimum"/>,
    /// <see cref="WindowLayout.KeepsAspect"/>).
    /// </summary>
    public void Apply(Character leader, Screens? screens)
    {
        // a save with a short or holed table (written before the table had its present size, or
        // edited by hand) is made whole first, as WindowFor does; a blank one is seeded as
        // CHARCREA seeds a new character's (302-342), and a saved one is read back untouched, of
        // whatever revision - see WindowLayout.Prepare
        var (sw, sh) = ScaleArea(screens);
        // a character with sizes and no positions at all - new, or seeded before the layout was
        // ported - is laid out by sub150A, as CHARCREA does straight after seeding (343)
        if (WindowLayout.Prepare(leader, sw, sh)) ArrangeFresh(leader, screens);
        var (screenW, screenH) = ScreenSize(screens);

        foreach (var (child, which) in Children)
        {
            var r = leader.WindowFor(which);
            double w = Math.Max(child.MinWidth, r.HasSize ? r.Width : WindowLayout.Default(which).W);
            double h = r.HasSize ? r.Height : WindowLayout.Default(which).H;
            if (child.AspectHeight != null) h = child.AspectHeight(w);
            h = Math.Max(child.MinHeight, h);
            // sub0524 (MAINMOD.BAS:487-494): a window left off the edge of a smaller screen comes
            // back to that edge - the display's, not the frame's
            var (x, y) = WindowLayout.KeptOnScreen(r.Left, r.Top, screenW, screenH);
            child.PlaceAt(new Rect(x, y, w, h));
            // a state the window has no button to leave - a Dungeon or City saved rolled up or
            // maximised before their forms' own buttons were drawn - opens normal instead
            child.Minimised = r.State == 1 && child.CanMinimise;
            child.Maximised = r.State == 2 && child.CanMaximise;
        }
    }

    /// <summary>
    /// Writes every child's placement back onto the party, which is what each form's Form_Unload
    /// does. A maximised or minimised window keeps the size it had before, exactly as the original
    /// does by testing WindowState before it saves.
    ///
    /// Twelve of the thirteen forms save through <c>sub1614 -1, ...</c> (MODULE6.BAS:4370), and the
    /// -1 makes that routine loop from the first character to the last, writing the same placement
    /// onto every one of them. The layout is party-wide, not the leader's: whoever leads next
    /// already has it.
    ///
    /// The automap is the exception and does it by hand, <c>gv14C2(m0078)</c> with
    /// <c>m0078 = 0</c> (FRMAUTOM.BAS:173-176 and 571-574). It never calls <c>sub1614</c> at all,
    /// so its placement reaches the leader and nobody else. Since every reader of the table also
    /// uses character 0 that makes no difference until the party's first slot changes hands.
    /// </summary>
    public void Save(IReadOnlyList<Character> party)
    {
        if (party.Count == 0) return;
        foreach (var (child, which) in Children)
        {
            var p = child.Placement();
            int state = child.Maximised ? 2 : child.Minimised ? 1 : 0;
            foreach (var c in WindowLayout.PlacementSavedToWholeParty(which) ? party : new[] { party[0] })
            {
                c.WindowsRevision = WindowLayout.Revision;
                var r = c.WindowFor(which);
                r.Left = p.X;
                r.Top = p.Y;
                r.Width = p.Width;
                r.Height = p.Height;
                r.State = state;
            }
        }
    }

    /// <summary>
    /// The Control Panel's Window Control list: every child that is on the frame now and that the
    /// original lets you reach (<see cref="WindowLayout.ListedInWindowControl"/>).
    /// </summary>
    public IEnumerable<(MdiChild Child, GameWindow Which)> Listed()
    {
        foreach (var (child, which) in Children)
            if (child.IsVisible && WindowLayout.ListedInWindowControl((int)which))
                yield return (child, which);
    }

    /// <summary>
    /// Ties a child window to the panel inside it, so that code setting <c>panel.IsVisible</c> opens
    /// and closes the window. The close box sets the flag back down, so the next time the game asks
    /// for the panel the change is a real one and the window comes back.
    /// </summary>
    public static void Track(Control panel, MdiChild child)
    {
        child.IsVisible = panel.IsVisible;
        panel.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty) return;
            if (panel.IsVisible) child.Reopen();
            else child.IsVisible = false;
        };
        child.Closed += _ => panel.IsVisible = false;
    }
}

/// <summary>
/// A window's contents drawn at the size this port lays them out at, and scaled down to fit when
/// the window is made smaller than that - never up. The original scales every control on a form
/// with the form (sub0760, MAINMOD.BAS:1750-1848); the port's contents reflow instead, which works
/// until there is less room than they need, and then this takes over rather than cutting them off.
/// </summary>
public sealed class ShrinkToFit : Decorator
{
    private readonly Size _natural;

    /// <param name="natural">The size the contents are laid out at. A zero side is not held to:
    /// contents in a scrolling list can be as tall as they like, and only their width is fitted.</param>
    public ShrinkToFit(Control child, Size natural)
    {
        _natural = natural;
        Child = child;
        ClipToBounds = true;
    }

    private double _extraWidth;

    /// <summary>
    /// Scale above the drawn size too. Off, the contents are never drawn larger than they are laid
    /// out. On, for contents drawn to a form's own design in its own pixels: the original's
    /// Form_Resize sizes the form's Elastic to the client and scales every font by the width over
    /// the default (sub07B3, sub07C5, MAINMOD.BAS:1917-1975), so the form zooms both ways.
    /// </summary>
    public bool Grow { get; init; }

    /// <summary>
    /// Width the contents have been given on top of the size they are drawn at - a mod's column in
    /// Party Information (<see cref="ModSlots.PartyHeader"/>). The contents are then laid out that
    /// much wider and scaled to the window as a whole, rather than the port's own columns being
    /// squeezed to make room. Nought, as it is with no mod, leaves everything as it was. The
    /// window's own size, and so its saved placement, is not touched.
    /// </summary>
    public double ExtraWidth
    {
        get => _extraWidth;
        set
        {
            if (value < 0 || double.IsNaN(value)) value = 0;
            if (value == _extraWidth) return;
            _extraWidth = value;
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    /// <summary>
    /// A natural width of nought is taken from the contents themselves, as wide as they ask to be.
    /// </summary>
    private double NaturalWidth(bool measure)
    {
        if (_natural.Width > 0) return _natural.Width + _extraWidth;
        if (Child == null) return _natural.Width;
        if (measure)
        {
            Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _askedWidth = Child.DesiredSize.Width;
        }
        return _askedWidth;
    }

    private double _askedWidth;

    /// <summary>
    /// Zoom by the width alone, and let the contents take whatever height that leaves: for a form
    /// whose Form_Resize ties its height to its width, sizes an Elastic to the client and scales
    /// the fonts by the width (frmParty, FRMGROUP.BAS:117-124; sub07B3, sub07C5,
    /// MAINMOD.BAS:1917-1975). The Elastic moves each control down in proportion to the client's
    /// height, which <see cref="ElasticCanvas"/> does for the contents; scaling by the smaller of
    /// the two ratios instead left a blank band down the side or along the bottom, and put a
    /// right-anchored control (Leave) out beyond the drawn form.
    /// </summary>
    public bool ByWidth { get; init; }

    private double ScaleFor(Size room, bool measure = false)
    {
        double nw = NaturalWidth(measure);
        double sx = nw > 0 && !double.IsInfinity(room.Width) ? room.Width / nw : 1;
        double sy = _natural.Height > 0 && !double.IsInfinity(room.Height) ? room.Height / _natural.Height : 1;
        double s = ByWidth && nw > 0 && !double.IsInfinity(room.Width) ? sx : Math.Min(sx, sy);
        return Math.Max(0.05, Grow ? s : Math.Min(1, s));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child == null) return default;
        double s = ScaleFor(availableSize, measure: true);
        Child.Measure(new Size(availableSize.Width / s, availableSize.Height / s));
        return new Size(double.IsInfinity(availableSize.Width) ? Child.DesiredSize.Width * s : availableSize.Width,
                        double.IsInfinity(availableSize.Height) ? Child.DesiredSize.Height * s : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child == null) return finalSize;
        double s = ScaleFor(finalSize);
        if (_natural.Height <= 0) s = ScaleFor(new Size(finalSize.Width, double.PositiveInfinity));
        Child.Arrange(new Rect(0, 0, finalSize.Width / s, finalSize.Height / s));
        Child.RenderTransformOrigin = RelativePoint.TopLeft;
        Child.RenderTransform = s != 1 ? new Avalonia.Media.ScaleTransform(s, s) : null;
        return finalSize;
    }
}

/// <summary>
/// A Canvas that places its children as a VB form's Elastic does when the form is resized
/// (sub07B3, MAINMOD.BAS:1917-1923): each control's top moves with the client's height. Across it
/// is an ordinary Canvas - Left, or Right from the edge - since the width is what the whole
/// drawing is zoomed by (<see cref="ShrinkToFit.ByWidth"/>). Laid out at <see cref="DrawnHeight"/>,
/// every top is where the form data puts it, so at the form's own proportions it is a Canvas.
/// </summary>
public sealed class ElasticCanvas : Canvas
{
    /// <summary>The height the tops are given for (the form's client, in its own pixels). Nought: no stretching.</summary>
    public double DrawnHeight { get; set; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double k = DrawnHeight > 0 && finalSize.Height > 0 && !double.IsInfinity(finalSize.Height) ? finalSize.Height / DrawnHeight : 1;
        foreach (var child in Children)
        {
            var want = child.DesiredSize;
            double x = 0, y = 0;
            double left = GetLeft(child), right = GetRight(child), top = GetTop(child), bottom = GetBottom(child);
            if (!double.IsNaN(left)) x = left;
            else if (!double.IsNaN(right)) x = finalSize.Width - right - want.Width;
            if (!double.IsNaN(top)) y = top * k;
            else if (!double.IsNaN(bottom)) y = finalSize.Height - bottom * k - want.Height;
            child.Arrange(new Rect(new Point(x, y), want));
        }
        return finalSize;
    }
}
