using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// The Automap, the original's <c>FRMAUTOM</c>. It is a window of its own there, reached from the
/// Dungeon window's Map button - and from the city, which is why this takes its floors and its map
/// rather than a live walker.
///
/// Two rules come straight from <c>Form_Load</c> (FRMAUTOM.BAS:74-158):
///
/// - the level bar runs from 1 to the **deepest level anyone has reached**, read out of the automap
///   file. You cannot look at ground nobody has stood on
/// - it opens on the level you are standing on, unless you have lost track of the level
///   (M59F4(0)) or are in town, in which case it opens on level 1 (94-98)
///
/// Everything drawn comes from <see cref="SharedMap"/>'s record and never from the maze, which is
/// what <c>fn2EB5</c> does: see <see cref="MapRecord"/> for why that matters.
///
/// What it is drawn *with* is <see cref="AutomapPics"/> - the game's own icon sheet, which had
/// never been pulled out of MG11.DLL. The map used to be lines and letters of this port's own
/// devising; it is now the same twenty-seven 15x15 cells the original blits, laid out by the same
/// arithmetic.
/// </summary>
public partial class AutomapWindow : Window
{
    private readonly IReadOnlyList<Floor> _floors;
    private SharedMap _map;
    private int _deepest;

    /// <summary>Where the party is, so the arrow can be drawn on that level alone. Null in town.</summary>
    private (int level, int x, int y, Facing facing)? _you;
    private bool _lost;
    /// <summary>M59F4 again, the second of the two flags: the level itself is not known either.</summary>
    private bool _lostLevel;

    /// <summary>
    /// The squares that were already on the map when it was last plotted in full.
    ///
    /// This is the whole of the difference between the two ways an explored square can look.
    /// <c>medPlotMaze</c> fills each one with a box a pixel wider than the square on every side
    /// (FRMAUTOM.BAS:421), so neighbours run together into one black field; the incremental draw
    /// that fires as you walk fills a box inset by four instead (FRMAUTOM.BAS:232), which leaves a
    /// grey border. So "a small black square with a little gray around it" (help, AutoMap) does not
    /// mean *newly discovered* - it means *discovered since the map was last drawn from scratch*,
    /// and re-plotting the level turns them all solid.
    /// </summary>
    private HashSet<(int x, int y)> _plotted = new();
    private int _plottedLevel = -1;

    private int _level;

    /// <summary>The Control Panel's "Auto Change Lev.", Chars().M5914 (FRMAUTOM.BAS:310).</summary>
    private bool _autoChangeLevel = true;

    public AutomapWindow() { InitializeComponent(); _floors = Array.Empty<Floor>(); _map = new SharedMap(); }

    /// <summary>
    /// Hands the map itself over to somebody else and leaves this window empty.
    ///
    /// In the original the Automap is an MDI child like every other part of the game, and both the
    /// city and the dungeon are children of that same frame, so it sits beside whichever of them
    /// you are looking at - you are meant to be able to read the map and still walk. This lets one
    /// lot of drawing code serve both: the window is still the controller behind the buttons and
    /// the slider, it just is not the thing on screen.
    /// </summary>
    public Control TakeBody()
    {
        if (_body != null) return _body;
        _body = (Control)Content!;
        Content = null;
        return _body;
    }

    /// <summary>The map as taken out of this window, once it has been.</summary>
    private Control? _body;

    /// <summary>The frame child the map is in now: the town's or the dungeon's.</summary>
    private MdiChild? _child;

    /// <summary>
    /// Puts a freshly loaded map into a frame's Automap child and shows it - <c>Form_Load</c>, which
    /// only runs when the form is not loaded already. <c>FRMAUTOM.BAS:405</c> re-captions the
    /// window - "Automap (Level N)" - whenever the level changes, which the window still does even
    /// though it is not the thing being looked at.
    /// </summary>
    public void PutInto(MdiChild child)
    {
        MoveInto(child);
        child.Minimised = false;
        child.Reopen();
    }

    /// <summary>
    /// The one frmAutoMap passing from one frame's child to the other's. The original has a single
    /// form for the town and the dungeon: nothing between them unloads it - only sub16E0, as the
    /// game ends (MAINMOD2.BAS:5970) - so walking up or down keeps the level it shows, what has
    /// been drawn on it and the squares plotted since it was last drawn in full. The port has a
    /// child per frame, so the same map is lifted out of one and put into the other.
    /// </summary>
    public void MoveInto(MdiChild child)
    {
        var body = TakeBody();
        if (_child != null && _child != child && ReferenceEquals(_child.Body, body)) _child.Body = null;
        _child = child;
        if (!ReferenceEquals(child.Body, body)) child.Body = body;
        child.Title = Title ?? "Automap";
    }

    /// <summary>
    /// Points the map at the holder of the automap file it is in now. The original reads the one
    /// file, gv1B58, from wherever it is; the port's town and dungeon each keep that record in a
    /// <see cref="SharedMap"/> of their own, so the map is told which one to read.
    /// </summary>
    public void Rebind(SharedMap map, Func<bool>? noBlink)
    {
        _map = map;
        _noBlinkOption = noBlink;
    }

    /// <summary>
    /// <c>frmAutoMap!lblCall = "UPDATE"</c> from frmCityMain's Form_Load (FRMCTYM.BAS:149-151): with
    /// DungeonLevel 0, <c>modUpdateInfo</c> says "You are in the city." and reads the deepest level
    /// again (FRMAUTOM.BAS:543-547), and <c>medDrawCursor</c> puts back what was under the arrow and
    /// draws no other (303-305, 340-342). The level shown and the picture stay as they were: there
    /// is no re-plot, and no return to level 1 - that is Form_Load's, and the form is not loaded
    /// again.
    /// </summary>
    public void InTown(int deepestReached)
    {
        _you = null;
        _lost = _lostLevel = false;
        if (deepestReached > 0) SetDeepest(deepestReached);
        Render();
    }

    /// <summary>
    /// The automap's two mouse commands, from the help's Double Click and Right Click lists.
    ///
    /// <c>picMaze_DblClick</c> (FRMAUTOM.BAS:585-598) is a toggle rather than a reset: at the full
    /// default size it drops to that over 2.25, and at anything else it goes back up to the full
    /// default. That divisor is the same one character creation uses, which is why a new
    /// character's automap opens at the small size - double-clicking is how you get the big one.
    /// It resizes and refreshes and nothing else: the resize's Form_Resize places the map again
    /// (modAdjustMap, 164), but there is no re-plot - only the right button does that (601).
    /// Form_DblClick is the same routine (51-53). A single click on picPicture, the frame round
    /// picMaze, is too (<c>picPicture_Click</c>, 604-606); see <see cref="ToggleSize"/>.
    ///
    /// <c>picMaze_MouseDown</c> with the right button calls the level's own redraw.
    ///
    /// <paramref name="current"/> is asked each time because the child outlives any one map: a new
    /// AutomapWindow is built every time the map is opened.
    /// </summary>
    public static void WireMouse(MdiChild child, Func<AutomapWindow?> current)
    {
        child.DoubleTapped += (_, e) =>
        {
            // the grip is the window's border, not the form: no DblClick of the form's sees it
            if (child.OnGrip(e.Source)) return;
            if (current() is not { } map || map.DoubleClickResizes(e.Source)) ToggleSize(child);
            e.Handled = true;
        };
        child.PointerPressed += (_, e) =>
        {
            var button = e.GetCurrentPoint(child).Properties;
            if (button.IsLeftButtonPressed) { current()?.LeftPress(e); return; }
            if (!button.IsRightButtonPressed) return;
            // a mod's pick waiting (API version 13): the right button anywhere on the form gives it up
            if (current() is { Picking: true } picking) { picking.CancelPick(); e.Handled = true; return; }
            // picMaze_MouseDown's (600-602), and only picMaze's: picPicture, the scroll bar and the
            // labels have no MouseDown of their own. Found by where the click is on the map at the
            // size it is drawn, not by which control took it (see OnPicMaze)
            if (current() is not { } map || !map.OnPicMaze(e)) return;
            // with a mod offering items for the square, the re-plot is the first item of a menu;
            // with none, it is done at once, as the original
            if (!map.OfferMenu(e)) map.RedrawLevel();
            e.Handled = true;
        };
        // the pointer over the map, for the mods' hover (nothing at all with none hooked)
        child.PointerMoved += (_, e) => current()?.HoverAt(e);
        child.PointerExited += (_, _) => current()?.HoverOff();
    }

    /// <summary>
    /// Whether a double click on this control is one of the two that resize the form:
    /// <c>picMaze_DblClick</c> or <c>Form_DblClick</c> (FRMAUTOM.BAS:51-53, 585-598) - the map, or
    /// the form round it, where sub2E18 prints the numbers.
    ///
    /// Not hsbLevel, nor its two arrows: a VB3 scroll bar has no DblClick event, and clicking it
    /// pages the level and nothing else (hsbLevel_Change, 194-201). The port's arrows are buttons,
    /// and Avalonia raises a double tap on a button clicked twice quickly; it bubbled up to the
    /// child and toggled the window between its two sizes, so paging through the levels at speed
    /// put the window back to its default size every second click. Not lblLevel or lblInfo either,
    /// labels whose DblClick the form does not handle, and not picPicture: a double click there is
    /// its Click and then a DblClick it has no handler for, and the Click has already toggled the
    /// size (see the constructor).
    /// </summary>
    private bool DoubleClickResizes(object? source)
    {
        if (IsPicPicture(source)) return false;
        for (var v = source as Avalonia.Visual; v != null && v != FormArea; v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
            if (v == LevelSlider || v == DeeperBtn || v == ShallowerBtn || v == LevelLabel || v == InfoLabel
                || v is Avalonia.Controls.Primitives.RangeBase || v is Button)
                return false;
        return true;
    }

    /// <summary>
    /// Whether a pointer event is on picMaze itself, worked out from where it is on picPicture and
    /// how the map is drawn there now - slid, or enlarged and centred (<see cref="Place"/>) - so it
    /// hits the same pixel of the map at any size.
    /// </summary>
    private bool OnPicMaze(Avalonia.Input.PointerEventArgs e)
    {
        var p = e.GetPosition(MapClip);
        if (p.X < 0 || p.Y < 0 || p.X >= MapClip.Bounds.Width || p.Y >= MapClip.Bounds.Height) return false;
        var (mx, my) = AutomapLayout.ToMap(p.X, p.Y, _mazeLeft, _mazeTop, _zoom);
        return AutomapLayout.OnMaze(mx, my, GutterLeft, _mazeW, _mazeH);
    }

    /// <summary>
    /// <c>picMaze_DblClick</c>'s body (FRMAUTOM.BAS:586-597): from exactly the full default size to
    /// that over 2.25, and from any other size to the full default.
    /// </summary>
    private static void ToggleSize(MdiChild child)
    {
        var (full, fullH) = WindowLayout.Default(GameWindow.Automap);
        var at = child.Placement();
        bool atFull = Math.Abs(at.Width - full) < 1 && Math.Abs(at.Height - fullH) < 1;
        double w = atFull ? full / WindowLayout.AutomapSeedDivisor : full;
        double h = atFull ? fullH / WindowLayout.AutomapSeedDivisor : fullH;
        child.PlaceAt(new Avalonia.Rect(at.X, at.Y,
                                        Math.Max(child.MinWidth, w),
                                        Math.Max(child.MinHeight, h)));
    }

    /// <summary>
    /// Whether a click landed on picPicture itself - MapClip's own background, round picMaze and
    /// not on it - rather than on the map or on the form.
    /// </summary>
    private bool IsPicPicture(object? source) => ReferenceEquals(source, MapClip);

    public AutomapWindow(IReadOnlyList<Floor> floors, SharedMap map, int deepestReached,
                         (int level, int x, int y, Facing facing)? you, bool lost,
                         bool autoChangeLevel = true, bool lostLevel = false, Func<bool>? noBlink = null,
                         (int x, int y)? square = null)
    {
        InitializeComponent();
        _floors = floors;
        _map = map;
        _deepest = Math.Clamp(deepestReached <= 0 ? 1 : deepestReached, 1, Math.Max(1, floors.Count));
        _you = you;
        _square = you is { } here ? (here.x, here.y) : square;
        _lost = lost;
        _lostLevel = lostLevel;
        _autoChangeLevel = autoChangeLevel;

        // FRMAUTOM.BAS:94-98 - your level, or 1 when the level flag M59F4(0) is up or you are in town
        // (AutomapLayout.OpeningLevel). "Auto Change Lev." is not asked here: it is medDrawCursor's
        // (310), and only for following you to another level once the map is open
        _level = Math.Clamp(AutomapLayout.OpeningLevel(you?.level ?? 0, lostLevel), 1, _deepest);

        LevelSlider.Maximum = _deepest;
        LevelSlider.Value = _level;
        LevelSlider.IsEnabled = _deepest > 1;
        LevelSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty) return;
            int want = (int)Math.Round(LevelSlider.Value);
            if (want == _level) return;
            _level = Math.Clamp(want, 1, _deepest);
            Plot();
        };

        // gv1A66.M5F8E, the Options window's "No Automap Cursor Blink", is tested where it is used -
        // by medDrawCursor and on every tick (FRMAUTOM.BAS:321, 612) - so it is asked of the owner's
        // options each time rather than read once here
        _noBlinkOption = noBlink;
        _blinker.Tick += (_, _) => Blink();
        // the timer belongs to the picture: a map that has been replaced must stop blinking
        MapImage.DetachedFromVisualTree += (_, _) => _blinker.Stop();
        MapImage.AttachedToVisualTree += (_, _) => ShowCursor();
        // picPicture's room changes only when the form is resized (its margins follow the form's
        // size alone), so a new room is that resize's modAdjustMap arriving a layout pass late
        MapClip.PropertyChanged += (_, e) => { if (e.Property == BoundsProperty) Place(adjust: true, resizing: true); };
        LevelRow.PropertyChanged += (_, e) => { if (e.Property == BoundsProperty) Place(adjust: false, resizing: true); };
        _settle.Tick += (_, _) => Settled();
        MapImage.DetachedFromVisualTree += (_, _) => { _settle.Stop(); _settlePending = false; };
        // picPicture_Click (FRMAUTOM.BAS:604-606): a single click on the picture round the map is
        // picMaze_DblClick
        MapClip.Tapped += (_, e) =>
        {
            if (!IsPicPicture(e.Source) || _watched == null) return;
            ToggleSize(_watched);
            e.Handled = true;
        };
        // the form's own size is what modAdjustMap and sub2E18 test, so a resize of the child
        // places the map again whether or not the picture's room has changed - Form_Resize
        // (FRMAUTOM.BAS:164)
        FormArea.AttachedToVisualTree += (_, _) =>
        {
            WatchScaling(TopLevel.GetTopLevel(FormArea));
            Watch(Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<MdiChild>(FormArea));
        };
        FormArea.DetachedFromVisualTree += (_, _) => { WatchScaling(null); Watch(null); };
        PropertyChanged += (_, ev) =>
        {
            if (ev.Property == TitleProperty && _child != null) _child.Title = Title ?? "Automap";
        };

        _view = new AutomapView(() => _level, () => _deepest,
                                lv => FloorFor(lv) is { } f ? (f.Width, f.Height) : (0, 0),
                                lv => Go(lv), RefreshMarks, StartPick, () => FinishPick(null), () => _pick != null);
        // a mod disabled for throwing, or a setting of one changed: its marks are asked for again
        FormArea.AttachedToVisualTree += (_, _) => Listen(ModHost.Active);
        FormArea.DetachedFromVisualTree += (_, _) => Listen(null);

        Plot();
    }

    // ------------------------------------------------------------------ the mods' Automap (API version 13)
    //
    // Nothing here is the original's, and none of it does anything unless a running mod has an Automap
    // hook (ModHost.AutomapHooked): no menu (the right button re-plots at once), no marks (the overlay
    // is hidden), no hover, no click. See docs/MODDING.md, "The Automap".

    /// <summary>The map as the mods' hooks see it.</summary>
    private readonly AutomapView _view = null!;

    /// <summary>The host listened to while the map is on screen.</summary>
    private ModHost? _host;

    private void Listen(ModHost? host)
    {
        if (ReferenceEquals(host, _host)) return;
        if (_host != null) _host.Changed -= OnModsChanged;
        _host = host;
        if (_host != null) _host.Changed += OnModsChanged;
    }

    private void OnModsChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshMarks);

    /// <summary>A mod's pick waiting for a square: the prompt in lblInfo's place, and whom to answer.</summary>
    private (string Prompt, Action<AutomapSquare?> Done)? _pick;

    /// <summary>Whether a mod's pick is waiting for the player to click a square.</summary>
    public bool Picking => _pick != null;

    /// <summary>The square the pointer is over, as last told to the mods.</summary>
    private AutomapSquare? _hover;

    /// <summary>The right-click menu last opened, for Mordor.UiCheck.</summary>
    private ContextMenu? _menu;

    /// <summary>The first item of the menu: the original's right-click, the help's "Automap - Redraw/Refresh Level".</summary>
    public const string RedrawItem = "Redraw/Refresh Level";

    private void StartPick(string prompt, Action<AutomapSquare?> done)
    {
        if (_pick != null) FinishPick(null);
        _pick = (prompt ?? "", done);
        InfoLabel.Text = _pick.Value.Prompt;
        MapClip.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Cross);
        RefreshMarks();
    }

    /// <summary>A pick answered: with the square clicked, or null (Escape, the right button, or another pick).</summary>
    private void FinishPick(AutomapSquare? square)
    {
        if (_pick is not { } pick) return;
        _pick = null;
        InfoLabel.Text = WhereYouAre();
        MapClip.Cursor = null;
        pick.Done(square);
        RefreshMarks();
    }

    /// <summary>Escape, or the right button: a waiting pick given up.</summary>
    public void CancelPick() => FinishPick(null);

    /// <summary>
    /// The square of the level shown under the pointer, or null off picMaze - where the pointer is on the
    /// map at the size it is drawn, as <see cref="OnPicMaze"/> finds it.
    /// </summary>
    private AutomapSquare? SquareAt(Avalonia.Input.PointerEventArgs e)
    {
        var p = e.GetPosition(MapClip);
        if (p.X < 0 || p.Y < 0 || p.X >= MapClip.Bounds.Width || p.Y >= MapClip.Bounds.Height) return null;
        var (mx, my) = AutomapLayout.ToMap(p.X, p.Y, _mazeLeft, _mazeTop, _zoom);
        if (!AutomapLayout.OnMaze(mx, my, GutterLeft, _mazeW, _mazeH) || FloorFor(_level) is not { } fl) return null;
        var (x, y) = AutomapLayout.SquareAt(mx - GutterLeft, my, fl.Width, fl.Height);
        return new AutomapSquare(_level, x, y);
    }

    /// <summary>
    /// A right-click on picMaze with a mod offering items for the square: the menu, its first item the
    /// original's re-plot. False, and nothing shown, when no running mod offers any.
    /// </summary>
    private bool OfferMenu(Avalonia.Input.PointerEventArgs e)
    {
        var host = ModHost.Active;
        if (!host.AutomapHooked || SquareAt(e) is not { } square) return false;
        var items = host.AutomapMenu(_view, square);
        if (items.Count == 0) return false;
        var menu = new ContextMenu();
        UiFonts.Set(menu, DesignFonts.AutoMap);
        var redraw = new MenuItem { Header = RedrawItem };
        redraw.Click += (_, _) => RedrawLevel();
        menu.Items.Add(redraw);
        menu.Items.Add(new Separator());
        foreach (var (mod, item) in items)
        {
            if (item.IsSeparator) { menu.Items.Add(new Separator()); continue; }
            var mi = new MenuItem { Header = item.Label, IsEnabled = item.IsEnabled };
            if (item.Swatch is { } c)
                mi.Icon = new Border
                {
                    Width = 12, Height = 12, BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
                    Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)),
                };
            mi.Click += (_, _) => { ModHost.Active.RunAutomapItem(mod, item); RefreshMarks(); };
            menu.Items.Add(mi);
        }
        _menu?.Close();
        _menu = menu;
        menu.Open(MapImage);
        return true;
    }

    /// <summary>A left press on the form: a waiting pick takes the square; otherwise the mods are told of the click.</summary>
    private void LeftPress(Avalonia.Input.PointerEventArgs e)
    {
        if (_pick == null && !ModHost.Active.AutomapHooked) return;
        if (SquareAt(e) is not { } square) return;
        if (_pick != null) { FinishPick(square); return; }
        ModHost.Active.AutomapClick(_view, square);
        RefreshMarks();
    }

    /// <summary>The pointer moved over the form: the mods are told when it comes onto another square.</summary>
    private void HoverAt(Avalonia.Input.PointerEventArgs e)
    {
        if (_pick == null && !ModHost.Active.AutomapHooked) return;
        Hover(SquareAt(e));
    }

    private void HoverOff()
    {
        if (_pick == null && !ModHost.Active.AutomapHooked) return;
        Hover(null);
    }

    private void Hover(AutomapSquare? square)
    {
        if (Equals(square, _hover)) return;
        _hover = square;
        ModHost.Active.AutomapHover(_view, square);
        RefreshMarks();
    }

    /// <summary>
    /// The mods' marks for the level shown, asked for again and laid over picMaze; and the square a
    /// waiting pick would take, framed. Hidden, with nothing asked, when no mod is hooked.
    /// </summary>
    private void RefreshMarks()
    {
        var host = ModHost.Active;
        // on screen, the host the game asks now is the one listened to (it is set once, at start-up)
        if (_host != null) Listen(host);
        var fl = FloorFor(_level);
        if (fl == null || _mazeW <= 0 || (!host.AutomapHooked && _pick == null))
        {
            if (MapMarks.IsVisible || MapMarks.Marks.Count > 0) MapMarks.Set(Array.Empty<AutomapMark>(), null, 0, 0, 0, 1);
            return;
        }
        var marks = host.AutomapHooked ? host.AutomapMarks(_view) : new List<AutomapMark>();
        (int, int)? pickAt = _pick != null && _hover is { } h && h.Level == _level ? (h.X, h.Y) : null;
        MapMarks.Set(marks, pickAt, fl.Height, _mazeW, _mazeH, _zoom);
        SyncMarks();
    }

    /// <summary>The overlay laid exactly where picMaze is, at its size.</summary>
    private void SyncMarks()
    {
        Canvas.SetLeft(MapMarks, Canvas.GetLeft(MapImage));
        Canvas.SetTop(MapMarks, Canvas.GetTop(MapImage));
        MapMarks.Width = MapImage.Width;
        MapMarks.Height = MapImage.Height;
        MapMarks.Rescale(_zoom);
    }

    /// <summary>
    /// Chars(0).X and .Y, the square modAdjustMap centres on (FRMAUTOM.BAS:465, 482). They are
    /// not cleared as you walk out, so in town it is still the square you left the dungeon from.
    /// </summary>
    private (int x, int y)? _square;

    /// <summary>The child window whose size is being watched.</summary>
    private MdiChild? _watched;

    private void Watch(MdiChild? form)
    {
        if (_watched == form) return;
        if (_watched != null) _watched.PropertyChanged -= OnFormChanged;
        _watched = form;
        if (_watched != null) _watched.PropertyChanged += OnFormChanged;
        // Form_Load gives the form its size, and that is a Form_Resize
        Place(adjust: true);
    }

    private void OnFormChanged(object? s, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WidthProperty || e.Property == HeightProperty) Place(adjust: true, resizing: true);
    }

    /// <summary>
    /// The window the map is in, for its display scaling: moved to a display of another scaling,
    /// the same size in the window's units is another number of device pixels, and the map is
    /// drawn again for them.
    /// </summary>
    private TopLevel? _scalingOf;

    private void WatchScaling(TopLevel? top)
    {
        if (_scalingOf == top) return;
        if (_scalingOf != null) _scalingOf.ScalingChanged -= OnScalingChanged;
        _scalingOf = top;
        if (_scalingOf != null) _scalingOf.ScalingChanged += OnScalingChanged;
    }

    private void OnScalingChanged(object? s, EventArgs e) => Place(adjust: false);

    // ------------------------------------------------------------------ the blinking cursor

    /// <summary>
    /// <c>timBlinker</c>. Its Interval is not in the code - the program never sets it - but in the
    /// binary form: 500 ms (docs/DECOMPILE.md, "Recovering design-time properties").
    /// </summary>
    private readonly Avalonia.Threading.DispatcherTimer _blinker = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private Func<bool>? _noBlinkOption;

    /// <summary>gv1A66.M5F8E as it stands now, from the options of the frame this map is on.</summary>
    private bool _noBlink => _noBlinkOption?.Invoke() ?? false;

    /// <summary>
    /// m0030, the blinker's phase. <c>timBlinker_Timer</c> (FRMAUTOM.BAS:618-624) blits the arrow
    /// when it is set and clears it, and puts back what was under the arrow when it is not and
    /// sets it. It is a module-level Dim, so it starts False - the first tick hides the arrow - and
    /// nothing resets it when a step redraws the arrow, so the rhythm runs on across steps.
    /// </summary>
    private bool _blinkShow;

    /// <summary>The picture twice: with the arrow, and with what the arrow covers.</summary>
    private WriteableBitmap? _withCursor, _withoutCursor;

    /// <summary>
    /// The end of <c>medDrawCursor</c> (FRMAUTOM.BAS:320-338): on the level you are standing on,
    /// and not lost, the arrow is drawn and the blinker switched on unless "No Automap Cursor
    /// Blink" is ticked; anywhere else the blinker is switched off.
    /// </summary>
    private void ShowCursor()
    {
        MapImage.Source = _withCursor;
        if (_withoutCursor != null && !_noBlink) { if (!_blinker.IsEnabled) _blinker.Start(); }
        else _blinker.Stop();
    }

    /// <summary><c>timBlinker_Timer</c>, FRMAUTOM.BAS:608-632.</summary>
    private void Blink()
    {
        if (_withoutCursor == null || _noBlink) { _blinker.Stop(); MapImage.Source = _withCursor; return; }
        MapImage.Source = _blinkShow ? _withCursor : _withoutCursor;
        _blinkShow = !_blinkShow;
    }

    /// <summary>
    /// One level along, for whoever holds the frame this map is a child of. See
    /// <c>fn1521</c> (MAINMOD2.BAS:3393-3410) - the keys belong to the game, not to this window.
    /// </summary>
    public void Page(int delta) => Go(_level + delta);

    /// <summary>
    /// The step you have just taken, with the map open beside you: the original's
    /// <c>frmAutoMap!lblCall = "DRAW"</c> (MAZEMOD.BAS:1695). It moves the arrow and draws in
    /// whatever has been recorded since, small - it does **not** re-plot, which is what keeps the
    /// two kinds of black square apart. Without it the map stood still while you walked, arrow and
    /// all, until you closed it and opened it again.
    /// </summary>
    public void Walked((int level, int x, int y, Facing facing) you, bool lost, bool lostLevel, int deepestReached = 0, bool? autoChangeLevel = null)
    {
        _you = you;
        _square = (you.x, you.y);
        if (autoChangeLevel is bool auto) _autoChangeLevel = auto;   // read at each draw, as the Control Panel may change it
        _lost = lost;
        _lostLevel = lostLevel;

        // "UPDATE" is modUpdateInfo and then medDrawCursor (FRMAUTOM.BAS:222-224). modUpdateInfo
        // reads the deepest-level record again and makes it the bar's Max (543-544), and
        // medDrawCursor lifts the Max to the level you stand on when you know it and "Auto Change
        // Lev." is set (310-311). The bar
        // had kept the Max it opened with, so a teleport or a staircase to a new level left it at
        // "Level 1 of 1" and the map could not follow you there.
        if (deepestReached > 0) SetDeepest(deepestReached);
        if (!lost && _autoChangeLevel && you.level > _deepest) SetDeepest(you.level);

        // With "Auto Change Lev." set, medDrawCursor moves hsbLevel to the level you are on when
        // you are not on the one being shown (FRMAUTOM.BAS:306-313); with it off the map stays
        // on the level being read.
        if (!lost && _autoChangeLevel && you.level != _level && you.level >= 1 && you.level <= _deepest)
        {
            _level = you.level;
            LevelSlider.Value = _level;
            Plot();
            return;
        }
        Render();
    }

    /// <summary>
    /// The help's Automap Keys: "&lt;  Display Previous Automap Level" and
    /// "&gt;  Display Next Automap Level". Escape backs out, as it does everywhere in the original.
    ///
    /// Not reached while the map is a child of a frame - the body has been taken out of this
    /// window and nothing gives it focus. The frames handle those keys and call
    /// <see cref="Page"/>; this stays for the day the window is shown on its own again.
    /// </summary>
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Avalonia.Input.Key.OemComma:
            case Avalonia.Input.Key.OemMinus:
            case Avalonia.Input.Key.Left:
            case Avalonia.Input.Key.Up:
                Go(_level - 1); e.Handled = true; return;
            case Avalonia.Input.Key.OemPeriod:
            case Avalonia.Input.Key.OemPlus:
            case Avalonia.Input.Key.Right:
            case Avalonia.Input.Key.Down:
                Go(_level + 1); e.Handled = true; return;
            case Avalonia.Input.Key.Escape:
            case Avalonia.Input.Key.F8:
                Close(); e.Handled = true; return;
        }
        base.OnKeyDown(e);
    }


    /// <summary><c>hsbLevel.Max</c> set again, and what reads it.</summary>
    private void SetDeepest(int deepest)
    {
        int d = Math.Clamp(deepest, 1, Math.Max(1, _floors.Count));
        if (d == _deepest) return;
        _deepest = d;
        LevelSlider.Maximum = _deepest;
        LevelSlider.IsEnabled = _deepest > 1;
    }

    private void OnDeeper(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Go(_level + 1);
    private void OnShallower(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Go(_level - 1);

    private void Go(int want)
    {
        int next = Math.Clamp(want, 1, _deepest);
        if (next == _level) return;
        _level = next;
        LevelSlider.Value = _level;
        Plot();
    }

    private Floor? FloorFor(int level) => _floors.FirstOrDefault(f => f.LevelNum == level);

    /// <summary>
    /// <c>lblInfo</c>, written by <c>modUpdateInfo</c> (FRMAUTOM.BAS:541-566). Four lines, in the
    /// order it tests them:
    ///
    /// <code>
    /// "You are in the city."                                   DungeonLevel = 0
    /// "You are at 5,7,1 facing North."                          neither lost flag set
    /// "You are lost on Level 3."                                position lost, level known
    /// "You are completely lost."                                both lost
    /// </code>
    ///
    /// The spacing is the original's: <c>Str$</c> puts a space in front of a positive number and
    /// the Y is wrapped in <c>LTrim$</c> while the X is not, so "You are at" runs straight into a
    /// space and the comma after the X has none. This label was missing from the port entirely -
    /// the window said which level you were looking at and never where you were on it.
    /// </summary>
    private string WhereYouAre()
    {
        if (_you is not { } me) return "You are in the city.";
        if (_lost) return _lostLevel ? "You are completely lost."
                                     : $"You are lost on Level {me.level}.";
        // Chars().X and .Y count from 1 (FRMAUTOM.BAS:550 prints them as they are); the port keeps
        // the square 0-based, as lblRoom's readout does before it adds the 1 back
        return $"You are at {me.x + 1},{me.y + 1},{me.level} facing {Facings[(int)me.facing]}.";
    }

    /// <summary>
    /// gv06E8$, MODULE1.BAS:542-546. The original numbers Facing from one and writes
    /// gv06E8$(Facing - 1); the port's enum is zero-based already, so there is nothing to
    /// take off.
    /// </summary>
    private static readonly string[] Facings = { "North", "East", "South", "West" };

    /// <summary>
    /// Right-clicking the map, <c>picMaze_MouseDown</c> with Button = 2: "Automap - Redraw/Refresh
    /// Level", the help's Right Click Commands. It calls <c>medPlotMaze</c>, the full plot - which
    /// is why the right-click is worth having: it settles the squares you have just walked into the
    /// solid black field with the rest.
    /// </summary>
    public void RedrawLevel() => Plot();

    /// <summary>A full re-plot: everything on the map now counts as already drawn.</summary>
    private void Plot()
    {
        var fl = FloorFor(_level);
        _plotted = fl == null ? new HashSet<(int, int)>() : _map.CellsFor(_level, fl.Width);
        _plottedLevel = _level;
        Render();
    }

    // picMaze.BackColor: the unmapped part of a level is the form's own grey, not a black void.
    // The port had that the other way round, which also left rock indistinguishable from nothing.
    private const int Grey = unchecked((int)0xFFC0C0C0);
    private const int Black = unchecked((int)0xFF000000);

    /// <summary>Room for the two rows of numbers sub2E18 prints outside picMaze.</summary>
    private const int GutterBottom = 13;

    /// <summary>
    /// The room left of picMaze for the row numbers: 17 pixels, or the widest number (the level's
    /// last even row, "30") and <see cref="RowNumberGap"/> when that is wider. Set as a level is
    /// drawn. "30" and "28" had run against the map in a larger Options font (a deviation,
    /// docs/FIDELITY.md).
    /// </summary>
    private int GutterLeft = 17;

    /// <summary>The least room between a row number and the map.</summary>
    private const int RowNumberGap = 2;

    private static int GutterFor(int rows)
    {
        int widest = rows - rows % 2;
        if (widest < 2) return 17;
        double text = UiFonts.TextWidth(widest.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                        Mordor.Core.DesignFonts.AutoMap.Pass, Mordor.Core.DesignFont.Default);
        return Math.Max(17, (int)Math.Ceiling(text) + RowNumberGap);
    }

    private void Render()
    {
        // the original's own caption, FRMAUTOM.BAS:405
        Title = $"Automap (Level {_level})";
        LevelLabel.Text = $"Level {_level} of {_deepest}";
        InfoLabel.Text = _pick is { } waiting ? waiting.Prompt : WhereYouAre();
        DeeperBtn.IsEnabled = _level < _deepest;
        ShallowerBtn.IsEnabled = _level > 1;

        var fl = FloorFor(_level);
        if (fl == null)
        {
            _pxWith = _pxWithout = null;
            _mazeW = _mazeH = 0;
            _drawnAt = double.NaN;
            SetDrawn(null, null);
            RefreshMarks();
            return;
        }

        int w = fl.Width, h = fl.Height;
        const int pitch = AutomapPics.Pitch;
        GutterLeft = GutterFor(h);

        // Form_Load, FRMAUTOM.BAS:113-114: picMaze is (squares * 14) + 1 pixels each way. The +1 is
        // there because the squares overlap by a pixel, so the last one still needs its far edge.
        int bw = w * pitch + 1, bh = h * pitch + 1;
        var px = new int[bw * bh];
        for (int i = 0; i < px.Length; i++) px[i] = Grey;

        var cells = _map.CellsFor(_level, w);
        bool plotted = _plottedLevel == _level;

        // pass one, medPlotMaze's first loop: the black ground under every square the map has.
        foreach (var (x, y) in cells)
        {
            int cx = x * pitch, cy = (h - 1 - y) * pitch;
            if (plotted && !_plotted.Contains((x, y)))
                Fill(px, bw, bh, cx + 4, cy + 4, 7, 7, Black);        // walked since the last plot
            else
                Fill(px, bw, bh, cx - 1, cy - 1, 17, 17, Black);      // and these run together
        }

        // pass two, medDrawSquare. medPlotMaze's second loop runs over **every** square on the
        // level and draws any whose flag word is not zero (FRMAUTOM.BAS:427-430) - not just the
        // ones you have walked. That matters: stepping onto a square records the east side of the
        // square to its west, so the corridor you are walking down has its left-hand wall written
        // into squares you have never been on. Drawing only the explored ones left every corridor
        // in the game with a wall down one side and nothing down the other.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            long flags = _map.SeenFlags(_level, x, y, w);
            if (flags != 0) Square(px, bw, bh, h, x, y, flags);

            // the level's own west and south edges, which no square carries: a phantom square off
            // each end holding nothing but the one wall (FRMAUTOM.BAS:430-439), and only beside a
            // square you have actually been on - the been-here bit, 23 (mc00E6). That bit is in
            // the original's flag word, so a square you have stood on is never zero there; the
            // port keeps it apart, in the explored cells, and had tested the flag word first, so
            // an edge square with no wall or feature of its own lost its boundary wall
            if (!cells.Contains((x, y))) continue;
            if (x == 0) Square(px, bw, bh, h, -1, y, 1L << Floor.WALL_EAST);
            if (y == 0) Square(px, bw, bh, h, x, -1, 1L << Floor.WALL_NORTH);
        }

        // medDrawCursor, FRMAUTOM.BAS:325-326. While you are lost there is no marker at all, and
        // none on a level you are not standing on. The picture without it is kept as well: it is
        // m002A, what the arrow covered, which the blinker puts back every other tick.
        _pxWithout = null;
        if (!_lost && _you is { } me && me.level == _level)
        {
            _pxWithout = (int[])px.Clone();
            AutomapPics.Copy(px, bw, bh, AutomapPics.YouAreHere + (int)me.facing,
                             me.x * pitch + 2, (h - 1 - me.y) * pitch + 2);
        }
        _pxWith = px;
        _mazeW = bw;
        _mazeH = bh;
        // what is on the map has changed: the next Place draws it at the size shown, at once
        _drawnAt = double.NaN;

        MapArea.Width = GutterLeft + bw;
        MapArea.Height = bh + GutterBottom;
        Axes(w, h, bw, bh);
        // medDrawCursor calls modAdjustMap only where it draws the arrow (FRMAUTOM.BAS:320-322): on
        // another level, lost, or in town picMaze stays where it was (328-342). Every redraw had
        // centred it again, so a small map paged to another level jumped back to the middle.
        Place(adjust: AutomapLayout.CursorRecentres(_you?.level ?? 0, _lost, _level));
        RefreshMarks();
    }

    /// <summary>
    /// The form's own size, <c>Me.Width</c> and <c>Me.Height</c>: the child window's outer size, as
    /// the table's 458x493 is. It is read from the size the child was given, never from a
    /// Bounds - a child's Bounds is only brought up to date after everything inside it has been
    /// arranged, so read from inside that arrange it is the size before (or 0x0 the first time),
    /// which made every form "narrow" and "shallow" with a room of one pixel, and left the square
    /// you stand on at the picture's top left with the map beyond it cut off.
    /// </summary>
    private (double W, double H) FormSize()
    {
        if (_watched is { } f)
            return (double.IsNaN(f.Width) ? f.Bounds.Width : f.Width,
                    double.IsNaN(f.Height) ? f.Bounds.Height : f.Height);
        // shown on its own, the window stands in for the form
        return (Bounds.Width > 0 ? Bounds.Width : Width, Bounds.Height > 0 ? Bounds.Height : Height);
    }

    /// <summary>
    /// Where the picture and the map sit on the form: <c>modAdjustMap</c> (FRMAUTOM.BAS:455-501),
    /// with <see cref="AutomapLayout"/> holding its tests and arithmetic. picMaze is never shrunk;
    /// below the size it can be enlarged to it is drawn at its own size and only moved. Each axis
    /// on its own:
    ///
    /// - at full width the picture keeps its margin and the map starts at its left (473-476);
    ///   narrower, the picture runs from the form's left edge to its right (462-463) and the map is
    ///   slid to put your square - or the level's middle, lost - in the middle of it (464-471)
    /// - at full height the picture sits below the level row (490-493); shorter, it goes to the top
    ///   of the form and fills it, over hsbLevel, lblLevel and lblInfo (479-480), and the map is
    ///   centred down it the same way (481-488)
    ///
    /// And sub2E18's numbers, which are there only at full size (see
    /// <see cref="AutomapLayout.ColumnNumbers"/>).
    ///
    /// picMaze's Left and Top on a narrow or short form are worked out again only when
    /// <paramref name="adjust"/> is set - when modAdjustMap would run: from Form_Resize
    /// (FRMAUTOM.BAS:164) and from medDrawCursor as it draws the arrow (320-322). Otherwise the map
    /// keeps the place it was last given. At full size on an axis modAdjustMap always puts it at
    /// 0 (476, 493) and nothing else moves it, so that is not waited for. The picture's margins
    /// and the numbers follow the form's size alone, so they are always brought up to date.
    ///
    /// The port's one addition (a GUI deviation, docs/FIDELITY.md): on a form full size both ways
    /// whose picture is bigger than the map, the map - picMaze and its numbers together - is
    /// enlarged by <see cref="AutomapLayout.Zoom"/> to fill it, the map itself centred with its numbers beside it. The level is not
    /// plotted again for that: the plotted pixels are drawn again at the device's resolution for
    /// the new size (<see cref="PlaceImage"/>). <paramref name="resizing"/> says the call comes
    /// from a resize, which may be one step of a drag.
    /// </summary>
    private void Place(bool adjust, bool resizing = false)
    {
        double nw = MapArea.Width, nh = MapArea.Height;
        if (double.IsNaN(nw) || double.IsNaN(nh) || nw <= 0 || nh <= 0) return;

        double rs = TopLevel.GetTopLevel(MapClip)?.RenderScaling ?? 1;
        var (fw, fh) = FormSize();
        if (fw <= 0 || fh <= 0)
        {
            _zoom = 1;
            MapBox.Width = nw;
            MapBox.Height = nh;
            PlaceImage(rs, resizing);
            return;
        }
        bool narrow = AutomapLayout.Narrow(fw), shallow = AutomapLayout.Shallow(fh);

        // picPicture's own place: below the level row - "lblLevel.Top + lblLevel.Height + 3", here
        // the row's measured height with its margins - or at the form's top. Setting it re-arranges
        // the form, and the new room comes back through MapClip's Bounds to place the map in it.
        double below = LevelRow.DesiredSize.Height;
        var margin = new Thickness(narrow ? 0 : 10, shallow ? 0 : below, narrow ? 0 : 10, shallow ? 0 : 10);
        if (MapClip.Margin != margin) MapClip.Margin = margin;

        bool cols = AutomapLayout.ColumnNumbers(fw, fh), rows = AutomapLayout.RowNumbers(fw, fh);
        foreach (var t in _columnNumbers) t.IsVisible = cols;
        foreach (var t in _rowNumbers) t.IsVisible = rows;

        double aw = MapClip.Bounds.Width, ah = MapClip.Bounds.Height;
        _zoom = !narrow && !shallow && aw > 0 && ah > 0 ? AutomapLayout.Zoom(aw, ah, nw, nh, rs) : 1;
        MapBox.Width = nw * _zoom;
        MapBox.Height = nh * _zoom;
        if (_zoom > 1)
        {
            _mazeLeft = AutomapLayout.Centred(aw, nw * _zoom, GutterLeft * _zoom, 0, rs);
            _mazeTop = AutomapLayout.Centred(ah, nh * _zoom, 0, GutterBottom * _zoom, rs);
        }
        else
        {
            var fl = FloorFor(_level);
            int w = fl?.Width ?? 30, h = fl?.Height ?? 30;
            // Chars(0).X and .Y unless a lost flag is up (FRMAUTOM.BAS:464, 481)
            (int x, int y)? at = !_lost && _square is { } sq ? sq : null;
            // picMaze starts GutterLeft into MapArea, where the row numbers go
            if (!narrow) _mazeLeft = 0;
            else if (adjust && aw > 0) _mazeLeft = AutomapLayout.Across(fw, aw, w, at?.x) - GutterLeft;
            if (!shallow) _mazeTop = 0;
            else if (adjust && ah > 0) _mazeTop = AutomapLayout.Down(fh, ah, h, at?.y);
        }
        Canvas.SetLeft(MapBox, _mazeLeft);
        Canvas.SetTop(MapBox, _mazeTop);
        PlaceImage(rs, resizing);
    }

    /// <summary>picMaze.Left and .Top as modAdjustMap last set them (Form_Load starts both at 0, 111-112).</summary>
    private double _mazeLeft, _mazeTop;

    /// <summary>How much the map is enlarged now: 1, as the original always draws it, unless <see cref="Place"/> grew it.</summary>
    private double _zoom = 1;

    /// <summary>picMaze's own size, in map pixels: the level's squares * 14 + 1 (FRMAUTOM.BAS:113-114).</summary>
    private int _mazeW, _mazeH;

    /// <summary>The numbers sub2E18 prints, each row of them shown or not as a whole.</summary>
    private readonly List<TextBlock> _columnNumbers = new(), _rowNumbers = new();

    /// <summary>
    /// <c>medDrawSquare</c> (FRMAUTOM.BAS:352-392): walk bits 0 to 22 and blit the icon of the same
    /// number for each one set. The three placements are the whole of the routine's body - a wall,
    /// door or secret door on the square's **east** side (bits 0, 2, 4) is drawn seven pixels to
    /// the right, one on its **north** side (1, 3, 5) seven pixels up, and everything else sits in
    /// the square. That offset is half a cell, so the edge icons straddle the line between two
    /// squares, which is what makes one wall serve both of them.
    /// </summary>
    private static void Square(int[] px, int bw, int bh, int h, int x, int y, long flags)
    {
        const int pitch = AutomapPics.Pitch;
        int cx = x * pitch, cy = (h - 1 - y) * pitch;
        for (int bit = 0; bit <= 22; bit++)
        {
            if ((flags & (1L << bit)) == 0) continue;
            switch (bit)
            {
                case Floor.WALL_EAST:
                case Floor.DOOR_EAST:
                case Floor.SECRET_DOOR_EAST:
                    AutomapPics.Paint(px, bw, bh, bit, cx + 7, cy); break;
                case Floor.WALL_NORTH:
                case Floor.DOOR_NORTH:
                case Floor.SECRET_DOOR_NORTH:
                    AutomapPics.Paint(px, bw, bh, bit, cx, cy - 7); break;
                default:
                    AutomapPics.Paint(px, bw, bh, bit, cx, cy); break;
            }
        }
    }

    /// <summary>
    /// <c>sub2E18</c> (FRMAUTOM.BAS:503-522), the numbers printed on the form around picMaze: every
    /// second column along the bottom and every second row up the left side, both counting from
    /// two. They are made here and shown only when the form is full size (<see cref="Place"/>):
    /// the columns need the full width and the rows the full height, and a picture that has
    /// grown to fill a smaller form covers the other set.
    /// </summary>
    private void Axes(int w, int h, int bw, int bh)
    {
        MapArea.Children.Clear();   // the numbers alone: picMaze is MapImage, beside MapBox
        _columnNumbers.Clear();
        _rowNumbers.Clear();
        const int pitch = AutomapPics.Pitch;

        for (int i = 2; i <= w; i += 2)
        {
            var t = Label(i);
            Canvas.SetLeft(t, GutterLeft + (i - 1) * pitch - 4);
            Canvas.SetTop(t, bh + 1);
            MapArea.Children.Add(t);
            _columnNumbers.Add(t);
        }
        for (int i = 2; i <= h; i += 2)
        {
            var t = Label(i);
            Canvas.SetLeft(t, 0);
            Canvas.SetTop(t, bh - i * pitch - 1);
            MapArea.Children.Add(t);
            _rowNumbers.Add(t);
        }
    }

    // Me.Print prints in the form's own font, MS Sans Serif 8.25 bold (frmAutoMap has none of its
    // own), which UiFonts puts on FormArea and these inherit; they had been 9 pixels of the theme's
    private static TextBlock Label(int n) => new()
    {
        Text = n.ToString(),
        FontWeight = FontWeight.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(0x8f, 0xb0, 0xff)),
    };

    private static void Fill(int[] px, int bw, int bh, int x, int y, int w, int h, int colour)
    {
        for (int row = 0; row < h; row++)
        {
            int dy = y + row;
            if (dy < 0 || dy >= bh) continue;
            for (int col = 0; col < w; col++)
            {
                int dx = x + col;
                if (dx < 0 || dx >= bw) continue;
                px[dy * bw + dx] = colour;
            }
        }
    }

    /// <summary>
    /// The map's pixels <paramref name="px"/> (<paramref name="bw"/> by <paramref name="bh"/>) as a
    /// bitmap of device pixels, each map pixel i covering <c>cols[i]</c> to <c>cols[i + 1]</c> across
    /// and the same down from <paramref name="rows"/> (<see cref="AutomapLayout.DeviceEdges"/>).
    /// A device row is built once per map row and copied for the rest of that map row.
    /// </summary>
    private static WriteableBitmap ToBitmap(int[] px, int bw, int bh, int[] cols, int[] rows)
    {
        int dw = Math.Max(1, cols[bw]), dh = Math.Max(1, rows[bh]);
        var bmp = new WriteableBitmap(new PixelSize(dw, dh), new Vector(96, 96),
                                      PixelFormat.Bgra8888, AlphaFormat.Premul);
        var line = new int[dw];
        using var fb = bmp.Lock();
        for (int y = 0; y < bh; y++)
        {
            int from = y * bw;
            for (int x = 0; x < bw; x++)
            {
                int v = px[from + x];
                for (int d = cols[x]; d < cols[x + 1]; d++) line[d] = v;
            }
            for (int d = rows[y]; d < rows[y + 1]; d++)
                Marshal.Copy(line, 0, fb.Address + d * fb.RowBytes, dw);
        }
        return bmp;
    }

    // ------------------------------------------------------------------ drawn at the size shown

    /// <summary>
    /// The level as plotted, one int per map pixel, with the arrow and without it (null when no
    /// arrow is drawn): m002A and picMaze as medPlotMaze and medDrawCursor leave them. Kept so a
    /// resize can draw the same map again at another size without plotting the level again.
    /// </summary>
    private int[]? _pxWith, _pxWithout;

    /// <summary>The device pixels per map pixel the bitmaps shown were drawn at; NaN when what is on the map has changed since.</summary>
    private double _drawnAt = double.NaN;

    /// <summary>The device pixels the bitmaps shown are, across and down.</summary>
    private int _drawnW, _drawnH;

    /// <summary>How many times the map has been drawn at a size, for Mordor.UiCheck to count a drag's.</summary>
    private int _draws;

    /// <summary>
    /// A resize being dragged: once a resize has drawn the map at a new size, more resizes inside
    /// this time stretch the bitmap already drawn - nearest neighbour, for a moment - and the map is
    /// drawn again once they stop. A change to what is on the map never waits.
    /// </summary>
    private readonly Avalonia.Threading.DispatcherTimer _settle = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private bool _settlePending;

    /// <summary>The bitmaps on show change: the arrow put back as medDrawCursor leaves it.</summary>
    private void SetDrawn(WriteableBitmap? with, WriteableBitmap? without)
    {
        var oldWith = _withCursor;
        var oldWithout = _withoutCursor;
        _withCursor = with;
        _withoutCursor = without;
        ShowCursor();
        // the old pair are no longer the Image's source; their pixels go now rather than whenever
        // the collector gets to them - a maximised map's are several megabytes each
        foreach (var b in new[] { oldWith, oldWithout })
            if (b != null && !ReferenceEquals(b, with) && !ReferenceEquals(b, without))
                Avalonia.Threading.Dispatcher.UIThread.Post(b.Dispose, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// picMaze put on picPicture at the size it is shown: <see cref="_zoom"/> times its own, at
    /// <see cref="_mazeLeft"/> plus the row numbers' gutter and <see cref="_mazeTop"/>, on whole
    /// device pixels. The bitmap is drawn again for the device pixels that covers whenever that
    /// is not what it was drawn at - at once, unless a resize is being dragged (see
    /// <see cref="_settle"/>). Never per frame, and never on a resize that leaves the size alone.
    /// </summary>
    private void PlaceImage(double rs, bool resizing)
    {
        PlaceImageCore(rs, resizing);
        SyncMarks();
    }

    private void PlaceImageCore(double rs, bool resizing)
    {
        Canvas.SetLeft(MapImage, AutomapLayout.Snap(_mazeLeft + GutterLeft * _zoom, rs));
        Canvas.SetTop(MapImage, AutomapLayout.Snap(_mazeTop, rs));
        if (_pxWith == null || _mazeW <= 0 || _mazeH <= 0) return;

        double s = _zoom * rs;
        if (!double.IsNaN(_drawnAt) && Math.Abs(s - _drawnAt) < 1e-9)
        {
            // back to the size last drawn, perhaps after being stretched away from it
            MapImage.Width = _drawnW / rs;
            MapImage.Height = _drawnH / rs;
            return;
        }
        if (!resizing || double.IsNaN(_drawnAt)) { Draw(s, rs); return; }

        // a resize: the bitmap there is, stretched to the new size for now (nearest neighbour)
        MapImage.Width = _mazeW * _zoom;
        MapImage.Height = _mazeH * _zoom;
        if (_settle.IsEnabled)
        {
            // another step of a drag: drawn when the dragging stops
            _settlePending = true;
            _settle.Stop();
            _settle.Start();
            return;
        }
        // the first resize in a while - a double click, a maximise, or the start of a drag: drawn
        // once this layout pass is over, so the several size changes one resize makes (width,
        // height, the picture's room) are drawn once, at the size they come to
        _settle.Start();
        if (_drawPosted) return;
        _drawPosted = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { _drawPosted = false; DrawIfStale(); },
                                                    Avalonia.Threading.DispatcherPriority.Background);
    }

    private bool _drawPosted;

    /// <summary>The settled resize: the map drawn at the size it was left at.</summary>
    private void Settled()
    {
        _settle.Stop();
        if (!_settlePending) return;
        _settlePending = false;
        DrawIfStale();
    }

    private void DrawIfStale()
    {
        if (_pxWith == null || _mazeW <= 0 || _mazeH <= 0) return;
        double rs = TopLevel.GetTopLevel(MapClip)?.RenderScaling ?? 1;
        double s = _zoom * rs;
        if (double.IsNaN(_drawnAt) || Math.Abs(s - _drawnAt) >= 1e-9) Draw(s, rs);
    }

    private void Draw(double s, double rs)
    {
        var cols = AutomapLayout.DeviceEdges(_mazeW, s);
        var rows = AutomapLayout.DeviceEdges(_mazeH, s);
        var with = ToBitmap(_pxWith!, _mazeW, _mazeH, cols, rows);
        var without = _pxWithout == null ? null : ToBitmap(_pxWithout, _mazeW, _mazeH, cols, rows);
        _drawnAt = s;
        _draws++;
        _drawnW = cols[_mazeW];
        _drawnH = rows[_mazeH];
        MapImage.Width = _drawnW / rs;
        MapImage.Height = _drawnH / rs;
        SetDrawn(with, without);
        SyncMarks();
    }
}
