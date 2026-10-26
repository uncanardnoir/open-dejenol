using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;

namespace Mordor.App;

/// <summary>
/// A child window inside <see cref="MdiHost"/>: a title bar you can drag, a corner you can pull,
/// and the minimise / maximise / close buttons the original's windows carry.
///
/// Mordor is an MDI application - every part of it (the character, the dungeon, the automap,
/// Objects &amp; Spells, Party Information, Messages) is a window of its own inside one frame, and
/// the player arranges them. Avalonia has no MDI, so this is it: a Canvas plus these.
///
/// Nothing in here takes keyboard focus. The game is driven from the keyboard, and a focused button
/// swallows space and enter - which is how a stray space once closed the dungeon.
/// </summary>
public sealed class MdiChild : Border
{
    /// <summary>
    /// The caption. Two of the game's windows re-title as you play: the character window carries
    /// the character's own name in quotes, and the dungeon carries whoever currently has the focus
    /// - "Dungeon (Misty)" in a screenshot means the player's character Misty is selected, not that
    /// the word is fixed. Switching with Alt+1..4 or 'n' changes it.
    /// </summary>
    public string Title
    {
        get => _title;
        set { _title = value; if (_caption != null) _caption.Text = value; }
    }
    private string _title;

    /// <summary>Raised when the close box is used, so the host can forget it.</summary>
    public event Action<MdiChild>? Closed;

    private readonly TextBlock _caption;
    private readonly Border _titleBar;
    private readonly ContentControl _body;
    private readonly Button _minBtn, _maxBtn, _closeBtn;
    private readonly Border _grip;

    /// <summary>
    /// The form's own border and title-bar buttons, from its .FRM: BorderStyle (a Fixed Double
    /// form cannot be dragged to a new size), MinButton, MaxButton and ControlBox. A form that
    /// has none of the three buttons shows none. The close box stands for the Control-menu's
    /// Close, which the port gives the children that the game shows and hides itself.
    /// </summary>
    public void Chrome(bool sizable, bool minButton, bool maxButton, bool closeBox)
    {
        _grip.IsVisible = sizable;
        _minBtn.IsVisible = minButton;
        _maxBtn.IsVisible = maxButton;
        _closeBtn.IsVisible = closeBox;
    }

    /// <summary>
    /// The height a width asks for, for a form whose Form_Resize keeps its proportions
    /// (<see cref="Mordor.Core.WindowLayout.KeepsAspect"/>); null for one that does not.
    /// </summary>
    public Func<double, double>? AspectHeight { get; set; }

    /// <summary>
    /// A window this one is kept above (the town for its service windows): whenever that one is
    /// brought to the front, this one, if shown, is brought up over it again
    /// (<see cref="MdiHost.BringToFront"/>). A GUI choice of the port's.
    /// </summary>
    public MdiChild? StaysAbove { get; set; }

    /// <summary>Whether the window has a button to roll it up, or to maximise it.</summary>
    public bool CanMinimise => _minBtn.IsVisible;
    public bool CanMaximise => _maxBtn.IsVisible;

    /// <summary>Whether the title bar can be dragged to move the window.</summary>
    public bool Movable { get; set; } = true;

    /// <summary>The space between the frame and what is in it.</summary>
    public Thickness BodyMargin
    {
        get => _body.Margin;
        set => _body.Margin = value;
    }

    private Point _grabbedAt;
    private bool _dragging, _resizing;
    private Rect _restore;
    private bool _maximised, _minimised;

    /// <summary>The height a minimised child collapses to - just its title bar.</summary>
    private const double BarHeight = 22;

    public MdiChild(string title, Control content, double width, double height)
    {
        _title = title;
        Background = Brush.Parse("#12121a");
        BorderBrush = Brush.Parse("#3a3a4e");
        BorderThickness = new Thickness(1);
        Width = width;
        Height = height;
        MinWidth = 0;
        MinHeight = BarHeight;

        _caption = new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        _minBtn = BarButton("_", () => { Minimised = !Minimised; });
        _maxBtn = BarButton("□", () => { Maximised = !Maximised; });
        _closeBtn = BarButton("✕", () => { IsVisible = false; Closed?.Invoke(this); });

        _titleBar = new Border
        {
            Background = Brush.Parse("#2b2b52"),
            Height = BarHeight,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    _caption,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 1,
                        [Grid.ColumnProperty] = 1,
                        Children = { _minBtn, _maxBtn, _closeBtn },
                    },
                },
            },
        };

        _body = new ContentControl { Content = content, Margin = new Thickness(4) };

        // the pull-tab in the corner, which is how the original is resized as well
        var grip = _grip = new Border
        {
            Width = 14, Height = 14,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = new Cursor(StandardCursorType.BottomRightCorner),
            Child = new Path
            {
                Data = Geometry.Parse("M 13,3 L 13,13 L 3,13 Z"),
                Fill = Brush.Parse("#55556e"),
            },
        };
        grip.PointerPressed += (_, e) =>
        {
            if (_maximised || _minimised) return;
            _resizing = true;
            // the scroll range holds still while the grip is held, as it does for a title drag:
            // otherwise shrinking the window that sets the range moves every child under the pointer
            if (Host is MdiHost h) h.Dragging = true;
            _grabbedAt = e.GetPosition(this);
            e.Pointer.Capture(grip);
            e.Handled = true;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (!_resizing) return;
            var p = e.GetPosition(this);
            Width = Math.Max(MinWidth, p.X + (Width - _grabbedAt.X));
            // a form that keeps its proportions takes its height from the width it was given
            Height = AspectHeight != null
                ? Math.Max(MinHeight, AspectHeight(Width))
                : Math.Max(MinHeight, p.Y + (Height - _grabbedAt.Y));
            _grabbedAt = p;
        };
        grip.PointerReleased += (_, e) =>
        {
            _resizing = false;
            e.Pointer.Capture(null);
            if (Host is MdiHost h) { h.Dragging = false; h.InvalidateArrange(); }
        };
        grip.PointerCaptureLost += (_, _) =>
        {
            if (!_resizing) return;
            _resizing = false;
            if (Host is MdiHost h) { h.Dragging = false; h.InvalidateArrange(); }
        };

        Child = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                _titleBar,
                new Panel { [Grid.RowProperty] = 1, Children = { _body, grip } },
            },
        };

        _titleBar.PointerPressed += OnTitlePressed;
        _titleBar.PointerMoved += OnTitleMoved;
        _titleBar.PointerReleased += OnTitleReleased;
        _titleBar.PointerCaptureLost += (_, _) =>
        {
            if (!_dragging) return;
            _dragging = false;
            if (Host is MdiHost h) { h.Dragging = false; h.InvalidateArrange(); }
        };
        // double-clicking the bar rolls it up, as a Windows 3.1 window does
        _titleBar.DoubleTapped += (_, e) => { if (_minBtn.IsVisible) Minimised = !Minimised; e.Handled = true; };

        // clicking anywhere in the window brings it forward
        // and makes it the active one, the form a key goes to first
        AddHandler(PointerPressedEvent, (_, _) => { if (Host is MdiHost h) { h.BringToFront(this); if (Activates) h.Active = this; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Whether a click makes this the active child (<see cref="MdiHost.Active"/>), the form the
    /// game's keys go to first. A mod's window (<see cref="ModWindows"/>) is brought forward by a
    /// click but is no form of the game's: the town's Alt keys and a service's keys stay with the
    /// child that had them.
    /// </summary>
    public bool Activates { get; set; } = true;

    private static Button BarButton(string glyph, Action click)
    {
        var b = new Button
        {
            Content = glyph,
            Focusable = false,            // the keyboard belongs to the game, never to a title bar
            Width = 20,
            Height = BarHeight - 4,
            Padding = new Thickness(0),
            FontSize = 11,
            Background = Brush.Parse("#3c3c66"),
            Foreground = Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 2, 2),
        };
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>
    /// What is inside the frame. Settable because the Automap is redrawn from scratch every time
    /// it is asked for, rather than kept in step square by square.
    /// </summary>
    public Control? Body
    {
        get => _body.Content as Control;
        set => _body.Content = value;
    }

    private MdiHost? Host => this.FindAncestorOfType<MdiHost>();

    /// <summary>
    /// Whether a pointer event came from the sizing grip - the window's border, in Windows terms,
    /// and not part of the form, so none of the form's own click handlers should see it.
    /// </summary>
    public bool OnGrip(object? source)
    {
        for (var v = source as Visual; v != null && v != this; v = v.GetVisualParent())
            if (v == _grip) return true;
        return false;
    }

    // ---------------------------------------------------------------- dragging

    private void OnTitlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_maximised || !Movable) return;
        _dragging = true;
        if (Host is MdiHost h) h.Dragging = true;
        _grabbedAt = e.GetPosition(this);
        e.Pointer.Capture(_titleBar);
    }

    private void OnTitleMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || Host is not MdiHost host) return;
        var p = e.GetPosition(host);
        // keep at least the title bar reachable, so a window cannot be lost off an edge - of the
        // part of the area in view, which the scroll bars may have moved
        double x = Math.Clamp(p.X - _grabbedAt.X, 20 - Width, host.Bounds.Width - 40);
        double y = Math.Clamp(p.Y - _grabbedAt.Y, 0, Math.Max(0, host.Bounds.Height - BarHeight));
        Canvas.SetLeft(this, x + host.ScrollOffset.X);
        Canvas.SetTop(this, y + host.ScrollOffset.Y);
    }

    private void OnTitleReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
        if (Host is MdiHost h) { h.Dragging = false; h.InvalidateArrange(); }
        e.Pointer.Capture(null);
    }

    // ---------------------------------------------------------------- the three states

    public bool Minimised
    {
        get => _minimised;
        set
        {
            if (_minimised == value) return;
            _minimised = value;
            if (value)
            {
                // A minimised MDI child is an icon along the bottom of the frame's client area, not
                // a window of its own size with nothing in it. The game's windows are children of
                // frmMain, an MDI form (MDIForm_Load, FRMMAIN.BAS:130), and each keeps a WindowState
                // that may be 1, minimised - Messages sets its own from the save in Form_Load
                // (FRMCHAR4.BAS:55) - and Form_Resize leaves a minimised one's size alone
                // (FRMCHAR4.BAS:63-70). The port draws it as its
                // bare title bar, icon-wide, parked in the bottom row. The minimum sizes the frame
                // gave the child for its contents are set aside while it is down - they are what
                // had held it at full size, covering whatever lay beneath.
                if (!_maximised) _restore = CurrentRect();
                _openMinW = MinWidth; _openMinH = MinHeight;
                _body.IsVisible = false;
                MinWidth = IconWidth; MinHeight = BarHeight;
                Width = IconWidth;
                Height = BarHeight + 2;
                Host?.ParkIcon(this);
            }
            else
            {
                Reopened();
                if (_maximised) FillHost();
                else
                {
                    Canvas.SetLeft(this, _restore.X);
                    Canvas.SetTop(this, _restore.Y);
                    Width = _restore.Width;
                    Height = _restore.Height;
                }
            }
            _minBtn.Content = value ? "▴" : "_";
        }
    }

    /// <summary>The width a minimised child keeps: room for a short caption and its three boxes.</summary>
    public const double IconWidth = 160;

    private double _openMinW = 0, _openMinH = BarHeight;

    /// <summary>The contents and their minimum size back, as the child comes up from an icon.</summary>
    private void Reopened()
    {
        MinWidth = _openMinW; MinHeight = _openMinH;
        _body.IsVisible = true;
        Restored?.Invoke(this);
    }

    /// <summary>
    /// Raised as the contents come back from an icon: anything that scrolled a list while it was
    /// hidden - the Messages window keeps its newest line in view - has to do it again.
    /// </summary>
    public event Action<MdiChild>? Restored;

    public bool Maximised
    {
        get => _maximised;
        set
        {
            if (_maximised == value) return;
            if (value)
            {
                if (!_minimised) _restore = CurrentRect();
                else { _minimised = false; Reopened(); _minBtn.Content = "_"; }
                _maximised = true;
                FillHost();
            }
            else
            {
                _maximised = false;
                if (_minimised) { _maxBtn.Content = "□"; return; }   // an icon stays an icon
                Canvas.SetLeft(this, _restore.X);
                Canvas.SetTop(this, _restore.Y);
                Width = _restore.Width;
                Height = _restore.Height;
            }
            _maxBtn.Content = value ? "❐" : "□";
        }
    }

    private void FillHost()
    {
        if (Host is not MdiHost host) return;
        Canvas.SetLeft(this, 0);
        Canvas.SetTop(this, 0);
        Width = Math.Max(MinWidth, host.Bounds.Width);
        Height = Math.Max(MinHeight, host.Bounds.Height);
    }

    private Rect CurrentRect() => new(Canvas.GetLeft(this), Canvas.GetTop(this),
                                      double.IsNaN(Width) ? Bounds.Width : Width,
                                      double.IsNaN(Height) ? Bounds.Height : Height);

    /// <summary>Where this window is, for saving with the character.</summary>
    public Rect Placement() => _maximised || _minimised ? _restore : CurrentRect();

    /// <summary>Put it back where it was found.</summary>
    public void PlaceAt(Rect r)
    {
        // an icon keeps its place in the bottom row; the placement is where it comes back to
        if (_minimised) { _restore = r; return; }
        Canvas.SetLeft(this, r.X);
        Canvas.SetTop(this, r.Y);
        if (r.Width > 0) Width = r.Width;
        if (r.Height > 0) Height = r.Height;
        _restore = r;
    }

    /// <summary>Show it again after the close box was used.</summary>
    public void Reopen()
    {
        IsVisible = true;
        Host?.BringToFront(this);
    }

    /// <summary>
    /// Brought forward and made the active child (<see cref="MdiHost.Active"/>): what a
    /// <c>SetFocus</c> on one of a form's controls does to that form, as every Info's
    /// <c>frmCharStat1!picTabPicture.SetFocus</c> does to the character window (FRMCHAR3.BAS:209).
    /// </summary>
    public void Activate()
    {
        Reopen();
        if (Activates && Host is MdiHost h) h.Active = this;
    }

    /// <summary>
    /// <c>If frmCharStat1.WindowState &lt;&gt; 0 Then frmCharStat1.WindowState = 0</c>, the first line
    /// of mdrcharSetInfo (MAINMOD2.BAS:4598): a minimised window or a maximised one comes back to
    /// its normal size, at the front. One brought back from an icon is made the active child as
    /// well - measured in the original (2026-10-25): Ctrl+L on a minimised character window
    /// restored it as the active, topmost child, while Ctrl+G on a maximised one brought it back to
    /// its normal size. One already at its normal size is left where it is in the stack - the line
    /// does not raise it. A window closed with its close box is loaded again by the reference, and
    /// shown.
    /// </summary>
    public void SetNormal()
    {
        if (!IsVisible) Reopen();
        if (!Minimised && !Maximised) return;
        bool fromIcon = Minimised;
        Minimised = false;
        Maximised = false;
        if (fromIcon) Activate(); else Reopen();
    }
}

/// <summary>
/// The frame the child windows live in. Avalonia has no MDI of its own, so this is a Canvas that
/// knows how to stack, arrange and remember its children.
/// </summary>
public sealed class MdiHost : Canvas
{
    private readonly List<MdiChild> _children = new();

    public IReadOnlyList<MdiChild> Windows => _children;

    public MdiHost()
    {
        Background = Brush.Parse("#0a0a10");
        ClipToBounds = true;
        // the icons keep to the bottom row whatever the frame's size, and one parked before the
        // frame had a size at all is put there once it has one
        SizeChanged += (_, _) => RepackIcons();
        _hBar = NewBar(Avalonia.Layout.Orientation.Horizontal);
        _vBar = NewBar(Avalonia.Layout.Orientation.Vertical);
    }

    // ---------------------------------------------------------------- scroll bars
    //
    // An MDIForm's ScrollBars property is True unless the form sets it otherwise, and then the
    // frame shows a scroll bar along an edge whenever a child lies beyond that edge of its client
    // area (the VB3 default; FRMMAIN.FRM, where frmMain's own would be set, is not among the
    // decompiled files). sub0524 only pulls back a window that lies past the whole display
    // (MAINMOD.BAS:487-494), so a window saved low in a taller frame - Messages at Top 850 in the
    // 760-tall one - stays there, and in the original it is scrolled to. Here the bars scroll the
    // view only: a child keeps its own Left and Top, which are what is saved, and nothing is moved.
    // They lie over the edges rather than taking room from the area, so the area the window sizes
    // are scaled against (MdiFrame.Size) is the same with them or without. A minimised child's
    // icon keeps to the bottom row and a maximised child fills the area, as in an MDI frame, and
    // while one is maximised there is nothing to scroll.

    private readonly Avalonia.Controls.Primitives.ScrollBar _hBar, _vBar;

    /// <summary>How far the view is scrolled: the point of the area at its top-left corner.</summary>
    public Vector ScrollOffset => _offset;
    private Vector _offset;

    /// <summary>A child is being dragged: the scrolled range does not shrink under it.</summary>
    internal bool Dragging { get; set; }

    /// <summary>The thickness of a scroll bar, a Windows scroll bar's.</summary>
    private const double BarThickness = 16;

    private bool _arranging;

    /// <summary>Whether a scroll bar is showing, for the self-check.</summary>
    public bool HorizontalBarShown => _hBar.IsVisible;
    public bool VerticalBarShown => _vBar.IsVisible;

    private Avalonia.Controls.Primitives.ScrollBar NewBar(Avalonia.Layout.Orientation o)
    {
        var bar = new Avalonia.Controls.Primitives.ScrollBar
        {
            Orientation = o,
            Focusable = false,          // the keyboard belongs to the game
            AllowAutoHide = false,
            IsVisible = false,
            SmallChange = 20,
            ZIndex = int.MaxValue,
        };
        bar.ValueChanged += (_, _) =>
        {
            if (_arranging) return;
            _offset = o == Avalonia.Layout.Orientation.Horizontal ? new Vector(bar.Value, _offset.Y) : new Vector(_offset.X, bar.Value);
            InvalidateArrange();
        };
        Children.Add(bar);
        return bar;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _arranging = true;
        try
        {
            bool needH = false, needV = false;
            if (finalSize.Width > 1 && finalSize.Height > 1)
            {
                var b = Mordor.Core.MdiScroll.Bars(
                    _children.Select(c => new Mordor.Core.MdiScroll.Child(GetLeft(c), GetTop(c),
                        double.IsNaN(c.Width) ? c.DesiredSize.Width : c.Width,
                        double.IsNaN(c.Height) ? c.DesiredSize.Height : c.Height,
                        c.IsVisible, c.Minimised, c.Maximised)),
                    finalSize.Width, finalSize.Height, BarThickness,
                    Dragging ? (_offset.X, _offset.Y) : null);
                (needH, needV) = (b.H, b.V);
                _offset = new Vector(Math.Clamp(_offset.X, b.MinX, b.MaxX), Math.Clamp(_offset.Y, b.MinY, b.MaxY));
                SetBar(_hBar, needH, b.MinX, b.MaxX, finalSize.Width - (needV ? BarThickness : 0), _offset.X);
                SetBar(_vBar, needV, b.MinY, b.MaxY, finalSize.Height - (needH ? BarThickness : 0), _offset.Y);
            }
            else
            {
                _offset = default;
                _hBar.IsVisible = _vBar.IsVisible = false;
            }

            foreach (var c in Children)
            {
                if (c == _hBar)
                    c.Arrange(needH ? new Rect(0, finalSize.Height - BarThickness, Math.Max(0, finalSize.Width - (needV ? BarThickness : 0)), BarThickness) : default);
                else if (c == _vBar)
                    c.Arrange(needV ? new Rect(finalSize.Width - BarThickness, 0, BarThickness, Math.Max(0, finalSize.Height - (needH ? BarThickness : 0))) : default);
                else
                {
                    double x = GetLeft(c), y = GetTop(c);
                    if (double.IsNaN(x)) x = 0;
                    if (double.IsNaN(y)) y = 0;
                    // an icon keeps to the bottom row of the area, above a horizontal bar, and a
                    // maximised child fills it
                    bool pinned = c is MdiChild m && (m.Minimised || m.Maximised);
                    if (!pinned) { x -= _offset.X; y -= _offset.Y; }
                    else if (needH && c is MdiChild { Minimised: true }) y -= BarThickness;
                    c.Arrange(new Rect(new Point(x, y), c.DesiredSize));
                }
            }
        }
        finally { _arranging = false; }
        return finalSize;
    }

    private static void SetBar(Avalonia.Controls.Primitives.ScrollBar bar, bool shown, double min, double max, double viewport, double value)
    {
        bar.IsVisible = shown;
        if (!shown) return;
        bar.Minimum = min;
        bar.Maximum = Math.Max(min, max);
        bar.ViewportSize = Math.Max(1, viewport);
        bar.LargeChange = Math.Max(1, viewport);
        bar.Value = value;
    }

    public MdiChild Add(string title, Control content, double x, double y, double w, double h)
    {
        var child = new MdiChild(title, content, w, h);
        SetLeft(child, x);
        SetTop(child, y);
        Children.Add(child);
        _children.Add(child);
        BringToFront(child);
        return child;
    }

    /// <summary>
    /// A child taken out of another frame (<see cref="Remove"/>) put into this one at the top of
    /// the stack, where it was in that frame: a mod's window going down to the dungeon with the
    /// party, or coming back up (<see cref="ModWindows.Sync"/>).
    /// </summary>
    public void Adopt(MdiChild child)
    {
        if (_children.Contains(child)) return;
        Children.Add(child);
        _children.Add(child);
        BringToFront(child);
        if (child.Minimised) ParkIcon(child);
    }

    /// <summary>Take a child out of this frame for good, or to go into another (<see cref="Adopt"/>).</summary>
    public bool Remove(MdiChild child)
    {
        if (!_children.Remove(child)) return false;
        Children.Remove(child);
        if (Active == child) Active = null;
        return true;
    }

    /// <summary>
    /// The child last clicked, which stands for the active MDI child - the form whose KeyDown sees
    /// a key first. Null until a screen names one or a child is clicked.
    /// </summary>
    public MdiChild? Active
    {
        get => _active;
        set
        {
            if (ReferenceEquals(_active, value)) return;
            _active = value;
            if (value != null) Activated?.Invoke(value);
        }
    }

    private MdiChild? _active;

    /// <summary>
    /// A child became the active one - its form's Form_Activate: by a click, a restore from an icon
    /// (<see cref="MdiChild.SetNormal"/>) or a SetFocus (<see cref="MdiChild.Activate"/>).
    /// </summary>
    public event Action<MdiChild>? Activated;

    public void BringToFront(MdiChild child)
    {
        int top = _children.Count == 0 ? 0 : _children.Max(c => c.ZIndex);
        if (child.ZIndex > top) return;
        child.ZIndex = top + 1;
        // the windows kept above this one go back over it, in the order they stood
        foreach (var above in _children.Where(c => c != child && c.StaysAbove == child && c.IsVisible).OrderBy(c => c.ZIndex).ToList())
            above.ZIndex = ++top + 1;
    }

    /// <summary>
    /// Where a minimised child goes: the bottom row of the frame, left to right, in the first
    /// place no other icon holds - as Windows lays out a frame's minimised children.
    /// </summary>
    public void ParkIcon(MdiChild icon)
    {
        if (Bounds.Height <= 0) return;   // no size yet: SizeChanged parks it
        double h = icon.Height, w = MdiChild.IconWidth + 2;
        double y = Math.Max(0, Bounds.Height - h);
        var taken = _children.Where(c => c != icon && c.IsVisible && c.Minimised).Select(c => GetLeft(c)).ToList();
        double x = 0;
        while (taken.Any(t => Math.Abs(t - x) < w / 2) && x + w <= Math.Max(w, Bounds.Width)) x += w;
        SetLeft(icon, x);
        SetTop(icon, y);
    }

    /// <summary>Every icon laid out again along the bottom row, left to right in their present order.</summary>
    private void RepackIcons()
    {
        if (Bounds.Height <= 0) return;
        double w = MdiChild.IconWidth + 2, x = 0;
        foreach (var c in _children.Where(c => c.IsVisible && c.Minimised).OrderBy(c => GetLeft(c)).ToList())
        {
            SetLeft(c, x);
            SetTop(c, Math.Max(0, Bounds.Height - c.Height));
            if (x + 2 * w <= Bounds.Width) x += w;
        }
    }

    public MdiChild? ByTitle(string title) =>
        _children.FirstOrDefault(c => string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The Control Panel's "Show": bring a window back whether it was closed, rolled up or buried.
    /// </summary>
    public void Show(string title)
    {
        if (ByTitle(title) is not MdiChild c) return;
        c.Minimised = false;
        c.Reopen();
    }

    /// <summary>
    /// "Adjust to Display": shrink anything that no longer fits, and pull back anything that has
    /// been dragged off an edge. The original writes the result onto the character (<c>M5958</c>).
    /// </summary>
    public void AdjustToDisplay()
    {
        foreach (var c in _children)
        {
            if (c.Maximised) continue;
            double w = Math.Min(c.Bounds.Width, Bounds.Width);
            double h = Math.Min(c.Bounds.Height, Bounds.Height);
            double x = Math.Clamp(GetLeft(c), 0, Math.Max(0, Bounds.Width - w));
            double y = Math.Clamp(GetTop(c), 0, Math.Max(0, Bounds.Height - h));
            c.PlaceAt(new Rect(x, y, w, h));
        }
    }
}
