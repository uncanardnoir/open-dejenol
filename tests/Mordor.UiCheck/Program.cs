using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;

// The Automap child, headless: Avalonia lays the real controls out and routes real pointer input
// through them, and nothing is put on screen. Three things the user reported or asked for
// (2026-10-14), each of which only shows with the controls laid out:
//
//   - resizing the window kept jumping the map back to level 1
//   - paging through the levels put the window back to its default size
//   - a big window enlarges the map (a GUI deviation, docs/FIDELITY.md), and the mouse still
//     lands on the right part of it
//   - and, after it, the Companions page - its portraits, where and how they are drawn, and its drag
//     and drop (CompanionDrag) - and the Confinement's
//     Comp. box, which did nothing for companions (2026-10-18)
//   - and Objects & Spells' Equip, Use, Drop and Info, which a drop of anything presses
//     (cmdCommand_DragDrop, FRMCHAR3.BAS:252-254)
//
//   Mordor.UiCheck <gamedir>        exit code 1 if anything failed

string game = args.Length > 0 ? args[0] : Path.Combine("..", "game_files");
string mdr = Path.Combine(game, "MDATA11.MDR");
if (!File.Exists(mdr)) { Console.WriteLine($"  FAIL  no dungeon file at {mdr}"); return 1; }

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// The game's pictures and help come out of the game folder, as at the app's start (Art): into a
// scratch cache of this run's own, removed at the end, never the player's.
string artRoot = Path.Combine(Path.GetTempPath(), "mordor-uicheck-art-" + Guid.NewGuid().ToString("N")[..8]);
string? artWas = Environment.GetEnvironmentVariable("MORDOR_ART_CACHE");
Environment.SetEnvironmentVariable("MORDOR_ART_CACHE", artRoot);
string? artError = Art.Prepare(game);
if (artError != null) { Console.WriteLine($"  FAIL  the game's art could not be read: {artError}"); return 1; }

int failed = 0;
void Check(string what, object? got, object? want)
{
    bool ok = Equals(got, want);
    Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what}{(ok ? "" : $": got {got}, want {want}")}");
    if (!ok) failed++;
}
void Pump() { for (int i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); } }
// two separate gestures must not read as one double click: the headless platform's double-click
// time is Avalonia's default, 500 ms
void Apart() => Thread.Sleep(600);

var floors = MdrLoader.LoadDungeon(mdr);
int width = floors[0].Width;
// the column numbers' gutter below the map (AutomapWindow.GutterBottom)
int GutterBottom = (int)typeof(AutomapWindow).GetField("GutterBottom", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
var map = new SharedMap();
for (int l = 1; l <= 3; l++) map.Merge(l, new[] { (2, 2), (3, 2), (5, 5) }, width);

var host = new MdiHost();
var top = new Window { Width = 1400, Height = 1100, Content = host };
top.Show();
Pump();
var frame = new MdiFrame(host);
var child = frame.Adopt(GameWindow.Automap, "Automap", null);
child.IsVisible = false;
AutomapWindow? current = null;
AutomapWindow.WireMouse(child, () => current);
var win = new AutomapWindow(floors, map, 3, (1, 5, 5, Facing.North), lost: false, autoChangeLevel: true);
current = win;
win.PutInto(child);
var (fullW, fullH) = WindowLayout.Default(GameWindow.Automap);
child.PlaceAt(new Rect(40, 40, fullW, fullH));
Pump();

T Field<T>(string name) => (T)typeof(AutomapWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(win)!;
int Level() => Field<int>("_level");
Size ChildSize() => new(child.Width, child.Height);
T Named<T>(string name) where T : Control => child.Body!.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
Point Centre(Visual v) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), top)!.Value;
void Click(Point p) { top.MouseDown(p, MouseButton.Left); top.MouseUp(p, MouseButton.Left); }
void DoubleClick(Point p) { Click(p); Click(p); Pump(); }
void Grip(double dx, double dy)
{
    var corner = new Point(Canvas.GetLeft(child) + child.Width - 5, Canvas.GetTop(child) + child.Height - 5);
    top.MouseDown(corner, MouseButton.Left); Pump();
    top.MouseMove(corner + new Point(dx / 2, dy / 2)); Pump();
    top.MouseMove(corner + new Point(dx, dy)); Pump();
    top.MouseUp(corner + new Point(dx, dy), MouseButton.Left); Pump();
}
// The map's two timers run on the clock: _settle (150 ms, a dragged resize drawn once it stops)
// and _blinker (500 ms, the arrow's blink). RunJobs fires a timer that has come due, so under load
// a pause between two steps of one drag could pass for the hand stopping, and draw mid-drag, and a
// blink could take the arrow's picture off show. Both are put out of the clock's reach here, and
// the run says when a drag has stopped: Settle is the tick _settle would give, called by hand.
foreach (var t in new[] { "_settle", "_blinker" })
    ((DispatcherTimer)typeof(AutomapWindow).GetField(t, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(win)!).Interval = TimeSpan.FromDays(1);
// Called before a single resize too: the pause a hand takes between two, without which this
// headless run would take them for one drag
void Settle() { typeof(AutomapWindow).GetMethod("Settled", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(win, null); Pump(); }
// picMaze shown as drawn: a bitmap of exactly the device pixels AutomapLayout.DeviceEdges gives for
// the zoom, put down one bitmap pixel to one device pixel, on a whole device pixel, beside the
// numbers' box at the gutter, and inside the picture
void Drawn(string what)
{
    double rs = top.RenderScaling, zoom = Field<double>("_zoom");
    var image = Named<Image>("MapImage");
    var box = Named<Viewbox>("MapBox");
    var clip = Named<Canvas>("MapClip");
    int mw = Field<int>("_mazeW"), mh = Field<int>("_mazeH");
    var bmp = (Avalonia.Media.Imaging.Bitmap)image.Source!;
    var cols = AutomapLayout.DeviceEdges(mw, zoom * rs);
    var rows = AutomapLayout.DeviceEdges(mh, zoom * rs);
    Check($"{what}: drawn for the size shown", Math.Abs(Field<double>("_drawnAt") - zoom * rs) < 1e-9, true);
    Check($"{what}: a bitmap of the device pixels it covers", (bmp.PixelSize.Width, bmp.PixelSize.Height), (cols[mw], rows[mh]));
    Check($"{what}: shown one to one", (Math.Round(image.Bounds.Width * rs, 6), Math.Round(image.Bounds.Height * rs, 6)), ((double)cols[mw], (double)rows[mh]));
    Check($"{what}: on a whole device pixel",
          Math.Abs(image.Bounds.X * rs - Math.Round(image.Bounds.X * rs)) < 1e-6 && Math.Abs(image.Bounds.Y * rs - Math.Round(image.Bounds.Y * rs)) < 1e-6, true);
    Check($"{what}: beside the row numbers",
          Math.Abs(image.Bounds.X - (Canvas.GetLeft(box) + Field<int>("GutterLeft") * zoom)) <= 0.5 / rs + 1e-9, true);
    // and every device pixel is the map pixel DeviceEdges puts there, nearest neighbour, nothing blended
    if (ReferenceEquals(image.Source, Field<object>("_withCursor")) && bmp.PixelSize.Width == cols[mw] && bmp.PixelSize.Height == rows[mh])
    {
        var px = Field<int[]>("_pxWith");
        int dw = cols[mw], dh = rows[mh];
        var got = new int[dw * dh];
        var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(got.Length * 4);
        try
        {
            bmp.CopyPixels(new PixelRect(0, 0, dw, dh), buf, got.Length * 4, dw * 4);
            System.Runtime.InteropServices.Marshal.Copy(buf, got, 0, got.Length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buf); }
        int wrong = 0;
        for (int y = 0; y < mh; y++)
        for (int x = 0; x < mw; x++)
        for (int dy = rows[y]; dy < rows[y + 1]; dy++)
        for (int dx = cols[x]; dx < cols[x + 1]; dx++)
            if (got[dy * dw + dx] != px[y * mw + x]) wrong++;
        Check($"{what}: every device pixel is its map pixel", wrong, 0);
    }
    else Check($"{what}: the arrow's picture is on show", false, true);
    if (zoom > 1) Check($"{what}: picMaze inside the picture",
                        image.Bounds.Right <= clip.Bounds.Width + 1e-9 && image.Bounds.Bottom <= clip.Bounds.Height + 1e-9, true);
}

Console.WriteLine("\nthe Automap keeps the level it shows across a resize (FRMAUTOM.BAS:151-170)");
{
    win.Page(2); Pump();
    Check("paged to level 3", Level(), 3);
    var drawn = Field<object>("_pxWith");
    child.Width = 620; child.Height = 660; Pump();
    Check("set bigger: still level 3", Level(), 3);
    Apart(); Grip(60, 40);
    Check("pulled bigger by the grip: still level 3", Level(), 3);
    Check("and the grip did resize it", ChildSize(), new Size(680, 700));
    Apart(); Grip(-400, -400);
    Check("pulled down to a narrow, short form: still level 3", Level(), 3);
    child.Width = 1000; child.Height = 1000; Pump();
    Check("enlarged map: still level 3", Level(), 3);
    child.Maximised = true; Pump(); child.Maximised = false; Pump();
    Check("maximised and back: still level 3", Level(), 3);
    child.Minimised = true; Pump(); child.Minimised = false; Pump();
    Check("minimised and back: still level 3", Level(), 3);
    Check("and no resize plotted the level again", ReferenceEquals(Field<object>("_pxWith"), drawn), true);
    Apart(); DoubleClick(new Point(Canvas.GetLeft(child) + child.Width - 5, Canvas.GetTop(child) + child.Height - 5));
    Check("a double click on the grip, the window's border, is not the form's DblClick", ChildSize(), new Size(1000, 1000));
}

Console.WriteLine("\nthe Automap keeps its size across a change of level (hsbLevel_Change, FRMAUTOM.BAS:194-201)");
{
    child.PlaceAt(new Rect(40, 40, fullW, fullH)); Pump();
    var full = ChildSize();
    Apart(); DoubleClick(Centre(Named<Button>("ShallowerBtn")));
    Check("the up arrow clicked twice quickly pages two levels", Level(), 1);
    Check("and leaves the window its size", ChildSize(), full);
    Apart(); DoubleClick(Centre(Named<Button>("DeeperBtn")));
    Check("the down arrow clicked twice quickly", Level(), 3);
    Check("leaves it too", ChildSize(), full);
    var slider = Named<Slider>("LevelSlider");
    var left = slider.TranslatePoint(new Point(3, slider.Bounds.Height / 2), top)!.Value;
    Apart(); DoubleClick(left);
    Check("the scroll bar clicked twice at its left end goes to level 1", Level(), 1);
    Check("and leaves it its size", ChildSize(), full);
    win.Page(1); Pump(); win.Page(1); Pump();
    Check("'>' twice", Level(), 3);
    Check("leaves it its size", ChildSize(), full);
    child.PlaceAt(new Rect(40, 40, 700, 720)); Pump();
    win.Page(-1); Pump();
    Check("a window of the player's own size keeps it too", ChildSize(), new Size(700, 720));
    Check("and its place", (Canvas.GetLeft(child), Canvas.GetTop(child)), (40d, 40d));
}

Console.WriteLine("\nthe double click still resizes from the map itself (picMaze_DblClick, FRMAUTOM.BAS:585-598)");
{
    child.PlaceAt(new Rect(40, 40, fullW, fullH)); Pump();
    var image = Named<Image>("MapImage");
    Apart(); DoubleClick(image.TranslatePoint(new Point(40, 40), top)!.Value);
    Check("full size drops to the size over 2.25",
          ChildSize(), new Size(Math.Max(child.MinWidth, fullW / WindowLayout.AutomapSeedDivisor), Math.Max(child.MinHeight, fullH / WindowLayout.AutomapSeedDivisor)));
    // the small form's picture fills it, the map slid to put your square in the middle
    Apart(); DoubleClick(Centre(Named<Canvas>("MapClip")));
    Check("and back up to full size", ChildSize(), new Size(fullW, fullH));
}

Console.WriteLine("\nthe enlarged map (a GUI deviation, docs/FIDELITY.md)");
{
    var clip = Named<Canvas>("MapClip");
    var box = Named<Viewbox>("MapBox");
    var area = Named<Canvas>("MapArea");
    double nw = area.Width, nh = area.Height;
    Settle(); child.PlaceAt(new Rect(40, 40, fullW, fullH)); Pump();
    Check("at the default size the map is its own size", (box.Width, box.Height), (nw, nh));
    Check("and at the picture's top left", (Canvas.GetLeft(box), Canvas.GetTop(box)), (0d, 0d));
    Drawn("at the default size");
    Settle(); child.PlaceAt(new Rect(40, 40, fullW * 1.5, fullH * 1.5)); Pump();
    double half = Field<double>("_zoom");
    Check("half as big again enlarges it by AutomapLayout.Zoom, no whole step needed",
          half, AutomapLayout.Zoom(clip.Bounds.Width, clip.Bounds.Height, nw, nh, top.RenderScaling));
    Check("which is more than 1x and less than 2x", half > 1.3 && half < 2, true);
    Drawn("half as big again");
    Settle(); child.PlaceAt(new Rect(10, 10, 1300, 1050)); Pump();
    double zoom = Field<double>("_zoom");
    double want = AutomapLayout.Zoom(clip.Bounds.Width, clip.Bounds.Height, nw, nh, top.RenderScaling);
    Check("a big window enlarges it by AutomapLayout.Zoom", zoom, want);
    Check("keeping its shape", (box.Width, box.Height), (nw * zoom, nh * zoom));
    Check("filling the picture the shorter way, but for the snapping's spare",
          Math.Abs(Math.Min(clip.Bounds.Width - box.Width, clip.Bounds.Height - box.Height) - AutomapLayout.SnapSlack / top.RenderScaling) < 1e-6, true);
    Check("centred across", Canvas.GetLeft(box), AutomapLayout.Centred(clip.Bounds.Width, nw * zoom, Field<int>("GutterLeft") * zoom, 0, top.RenderScaling));
    Check("and down", Canvas.GetTop(box), AutomapLayout.Centred(clip.Bounds.Height, nh * zoom, 0, GutterBottom * zoom, top.RenderScaling));
    Check("fitting in the picture", box.Width <= clip.Bounds.Width && box.Height <= clip.Bounds.Height, true);
    {
        // the map itself in the middle across, its row numbers hanging off its left: the whole box
        // centred had left more room at the map's left than at its right (2026-10-21)
        var pic = Named<Image>("MapImage");
        var at = pic.TranslatePoint(default, clip)!.Value;
        double left = at.X, right = clip.Bounds.Width - at.X - pic.Bounds.Width;
        Check($"the map itself centred across (left {left:0.#}, right {right:0.#})", Math.Abs(left - right) <= 1.5, true);
    }
    Drawn("a big window");
    Check("nearest neighbour while a drag stretches it", Avalonia.Media.RenderOptions.GetBitmapInterpolationMode(Named<Image>("MapImage")),
          Avalonia.Media.Imaging.BitmapInterpolationMode.None);
    var label = Named<TextBlock>("LevelLabel");
    Check("the level row is still there", label.IsVisible && label.Bounds.Width > 0, true);

    // a square walked since the last full plot is drawn small; the right button plots in full
    win.Page(-5); Pump();
    map.Merge(1, new[] { (6, 5) }, width);
    win.Walked((1, 6, 5, Facing.North), lost: false, lostLevel: false, deepestReached: 3, autoChangeLevel: true);
    Pump();
    bool Plotted() => Field<HashSet<(int x, int y)>>("_plotted").Contains((6, 5));
    Check("a square walked since the last plot is not yet plotted", Plotted(), false);
    Drawn("a step taken on the enlarged map, drawn at once");
    // the grey round the enlarged map is picPicture, not picMaze
    Apart();
    var grey = clip.TranslatePoint(new Point(Canvas.GetLeft(box) / 2, clip.Bounds.Height / 2), top)!.Value;
    top.MouseDown(grey, MouseButton.Right); top.MouseUp(grey, MouseButton.Right); Pump();
    Check("a right click on the grey round the enlarged map does not re-plot", Plotted(), false);
    // a point on picMaze in map pixels, where it is drawn now
    double gutter = Field<int>("GutterLeft");
    var onMap = clip.TranslatePoint(new Point(Canvas.GetLeft(box) + (gutter + 100) * zoom,
                                              Canvas.GetTop(box) + 100 * zoom), top)!.Value;
    Apart();
    top.MouseDown(onMap, MouseButton.Right); top.MouseUp(onMap, MouseButton.Right); Pump();
    Check("a right click on the enlarged map re-plots the level", Plotted(), true);
    Check("on the level it shows", Level(), 1);
    // past picMaze's own right edge, inside where an unscaled map would end: off the map once enlarged
    map.Merge(1, new[] { (7, 5) }, width);
    win.Walked((1, 7, 5, Facing.North), lost: false, lostLevel: false, deepestReached: 3, autoChangeLevel: true);
    Pump();
    var numbers = clip.TranslatePoint(new Point(Canvas.GetLeft(box) + gutter * zoom / 2, Canvas.GetTop(box) + 100 * zoom), top)!.Value;
    Apart();
    top.MouseDown(numbers, MouseButton.Right); top.MouseUp(numbers, MouseButton.Right); Pump();
    Check("a right click among the row numbers, on the form, does not",
          Field<HashSet<(int x, int y)>>("_plotted").Contains((7, 5)), false);
    var size = ChildSize();
    Apart(); DoubleClick(Centre(Named<Button>("DeeperBtn")));
    Check("paging the enlarged map keeps the window's size", ChildSize(), size);
    Check("and the enlargement", Field<double>("_zoom"), zoom);
    Drawn("paged, enlarged");
}


// The user, 2026-10-15: "automap scaling when the box gets very large still isn't working", on a
// 125% display, the window dragged big by its grip - and then, with whole device-pixel steps found
// to need about 731x805 before the first one, asked for smooth scaling. Each case drags the grip as
// a hand does - a run of moves with the layout catching up only now and then, released at the end -
// or maximises, and the factor is worked out here from the picture's room alone: the largest that
// fits both ways, less the snapping's spare, never under 1.
Console.WriteLine("\nthe enlarged map at large sizes, dragged by the grip, at 100% and 125% (a GUI deviation, docs/FIDELITY.md)");
{
    var impl = top.PlatformImpl!;
    void Scaling(double s)
    {
        // the headless window's RenderScaling has no setter: its backing field, then the event a
        // real window raises when it moves to a display of another scaling
        impl.GetType().GetField("<RenderScaling>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(impl, s);
        ((Action<double>?)impl.GetType().GetProperty("ScalingChanged")!.GetValue(impl))?.Invoke(s);
        Pump();
    }
    var clip = Named<Canvas>("MapClip");
    var box = Named<Viewbox>("MapBox");
    var area = Named<Canvas>("MapArea");
    var image = Named<Image>("MapImage");
    double nw = area.Width, nh = area.Height;
    double Expected(double rs)
    {
        double slack = AutomapLayout.SnapSlack / rs;
        double fit = Math.Min((clip.Bounds.Width - slack) / nw, (clip.Bounds.Height - slack) / nh);
        return Math.Max(1, fit);
    }
    void Drag(double toW, double toH)
    {
        var from = new Point(Canvas.GetLeft(child) + child.Width - 5, Canvas.GetTop(child) + child.Height - 5);
        var to = from + new Point(toW - child.Width, toH - child.Height);
        Apart();
        int before = Field<int>("_draws");
        top.MouseDown(from, MouseButton.Left);
        for (int i = 1; i <= 24; i++)
        {
            top.MouseMove(from + (to - from) * (i / 24.0));
            if (i % 5 == 0) Dispatcher.UIThread.RunJobs();   // a layout pass only now and then
        }
        top.MouseUp(to, MouseButton.Left);
        Pump();
        // the first resize of a drag draws at once, the rest stretch that until it stops
        Check($"dragged to {toW}x{toH}: drawn at most twice while dragging, not once a move", Field<int>("_draws") - before <= 2, true);
        double zoom = Field<double>("_zoom");
        Check($"dragged to {toW}x{toH}: shown at the new size before it settles",
              Math.Abs(image.Width - Field<int>("_mazeW") * zoom) <= 2 / top.RenderScaling + 1e-9, true);
        Settle();
    }
    void Case(string what, double rs, double atLeast)
    {
        double zoom = Field<double>("_zoom");
        Check($"{what}: the largest factor that fits ({Expected(rs):0.###}x)", Math.Abs(zoom - Expected(rs)) < 1e-9, true);
        Check($"{what}: at least {atLeast}x", zoom >= atLeast - 1e-9, true);
        Check($"{what}: the box is the map times the zoom", (box.Width, box.Height), (nw * zoom, nh * zoom));
        Check($"{what}: inside the picture", box.Width <= clip.Bounds.Width + 1e-9 && box.Height <= clip.Bounds.Height + 1e-9, true);
        if (zoom > 1) Check($"{what}: centred across", Canvas.GetLeft(box), AutomapLayout.Centred(clip.Bounds.Width, box.Width, Field<int>("GutterLeft") * zoom, 0, rs));
        if (zoom > 1) Check($"{what}: centred down", Canvas.GetTop(box), AutomapLayout.Centred(clip.Bounds.Height, box.Height, 0, GutterBottom * zoom, rs));
        // a full size form whose picture is smaller than the map: where modAdjustMap puts it (476, 493)
        if (zoom == 1) Check($"{what}: unenlarged, at the picture's top left", (Canvas.GetLeft(box), Canvas.GetTop(box)), (0d, 0d));
        Drawn(what);
    }

    top.Width = 2400; top.Height = 1700; Pump();
    foreach (double rs in new[] { 1.0, 1.25 })
    {
        Scaling(rs);
        Check($"{rs:0%}: the window reports the scaling", top.RenderScaling, rs);
        Settle(); child.PlaceAt(new Rect(10, 10, fullW, fullH)); Pump();
        Check($"{rs:0%}: full size is 1x", Field<double>("_zoom"), 1d);
        Drawn($"{rs:0%}, full size");
        // the user's case: a little bigger than full size is already bigger
        Drag(fullW + 100, fullH + 100);
        Case($"{rs:0%}, dragged a little past full size", rs, 1.05);
        Check($"{rs:0%}, a little past full size: enlarged", Field<double>("_zoom") > 1, true);
        Drag(1300, 1150);
        Case($"{rs:0%}, dragged to 1300x1150", rs, 2.3);
        Drag(1900, 1500);
        Case($"{rs:0%}, dragged to 1900x1500", rs, 3);
        Drag(2000, 800);
        Case($"{rs:0%}, wide and short, 2000x800: the height decides", rs, 1.5);
        Drag(800, 1600);
        Case($"{rs:0%}, tall and narrow, 800x1600: the width decides", rs, 1.5);
        Settle(); child.Maximised = true; Pump();
        Case($"{rs:0%}, maximised", rs, 3);
        Settle(); child.Maximised = false; Pump();
        Case($"{rs:0%}, restored", rs, 1.5);
        Check($"{rs:0%}: restored to the size dragged to", (Math.Round(child.Width), Math.Round(child.Height)), (800d, 1600d));
        Settle(); child.PlaceAt(new Rect(10, 10, fullW, fullH)); Pump();
        Check($"{rs:0%}: back to full size, 1x again", Field<double>("_zoom"), 1d);
        Check($"{rs:0%}: at the picture's top left", (Canvas.GetLeft(box), Canvas.GetTop(box)), (0d, 0d));
        Drawn($"{rs:0%}, back to full size");
        // below full size the map is its own size and slid, as modAdjustMap has it, and still drawn
        // for the device: on a 125% display that is 1.25 device pixels a map pixel, by the square
        Settle(); child.PlaceAt(new Rect(10, 10, fullW / WindowLayout.AutomapSeedDivisor, fullH / WindowLayout.AutomapSeedDivisor)); Pump();
        Check($"{rs:0%}, small: 1x", Field<double>("_zoom"), 1d);
        Drawn($"{rs:0%}, small");
    }
    Scaling(1);
    top.Width = 1400; top.Height = 1100; Pump();
}

// ---- the Cartographer mod on the Automap (src/Mods/Mordor.Mod.Cartographer, API version 13): with
// mods off the right button re-plots at once and nothing is drawn; with the mod on, a right-click on a
// square opens a menu whose first item is that re-plot, then the colours and the waypoint items; a
// waypoint picked on another level (the status line's prompt, the level bar, a click; Escape and the
// right button giving up); the hover's arrow on the same level only; a click going to the destination
// and highlighting it; cartographer.json read back by a new host; the marks drawn on the map's own
// device-pixel squares at 1x, enlarged, and at 125% ----
Console.WriteLine("\nthe Cartographer mod on the Automap (API version 13):");
{
    string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-carto-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(scratch);
    var hostWas = Mordor.Modding.ModHost.Active;
    var impl = top.PlatformImpl!;
    void Scaling(double s)
    {
        impl.GetType().GetField("<RenderScaling>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(impl, s);
        ((Action<double>?)impl.GetType().GetProperty("ScalingChanged")!.GetValue(impl))?.Invoke(s);
        Pump();
    }
    try
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Mordor.sln"))) root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        string? modsDir = root == null ? null : new[] { "Release", "Debug" }
            .Select(c => Path.Combine(root, "src/Mordor.App", "bin", c, "net8.0", "mods"))
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "Cartographer")));
        if (modsDir == null) throw new DirectoryNotFoundException("no build of Mordor.App with the Cartographer mod in its mods folder");

        var overlay = Named<AutomapOverlay>("MapMarks");
        var image = Named<Image>("MapImage");
        var info = Named<TextBlock>("InfoLabel");
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        int H() => floors.First(f => f.LevelNum == Level()).Height;
        Point At(int x, int y)
        {
            double z = Field<double>("_zoom");
            return image.TranslatePoint(new Point((x * AutomapLayout.Pitch + 7.5) * z, ((H() - 1 - y) * AutomapLayout.Pitch + 7.5) * z), top)!.Value;
        }
        ContextMenu? Menu() => (ContextMenu?)typeof(AutomapWindow).GetField("_menu", flags)!.GetValue(win);
        void NoMenu() => typeof(AutomapWindow).GetField("_menu", flags)!.SetValue(win, null);
        void Refresh() => typeof(AutomapWindow).GetMethod("RefreshMarks", flags)!.Invoke(win, null);
        void Press(Point p, MouseButton b) { Apart(); top.MouseDown(p, b); top.MouseUp(p, b); Pump(); }
        string Headers() => Menu() is not { } m ? "no menu" : string.Join(" | ", m.Items.Select(i => i is Separator ? "-" : ((MenuItem)i!).Header as string));
        void Choose(string label)
        {
            var mi = Menu()?.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header as string == label);
            if (mi == null) { Check($"the menu has {label}", Headers(), label); return; }
            mi.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            Menu()?.Close();
            Pump();
        }
        string Hex((byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        string Marks() => !overlay.IsVisible ? "" : string.Join(" ", overlay.Marks.Select(k =>
            $"{k.Kind}({k.X},{k.Y}{(k.Kind == Mordor.Modding.AutomapMarkKind.Arrow ? $"->{k.ToX},{k.ToY}" : "")}){Hex(k.Colour)}{(k.Bold ? "!" : "")}"));
        bool Plotted(int x, int y) => Field<HashSet<(int x, int y)>>("_plotted").Contains((x, y));
        void Step(int x)
        {
            map.Merge(1, new[] { (x, 5) }, width);
            win.Walked((1, x, 5, Facing.North), lost: false, lostLevel: false, deepestReached: 3, autoChangeLevel: false);
            Pump();
        }

        Settle(); child.PlaceAt(new Rect(40, 40, fullW, fullH)); Pump(); Settle();
        win.Page(-5); Pump();
        Check("on level 1, at 1x", (Level(), Field<double>("_zoom")), (1, 1d));

        // ---- mods off: the original's right-click, nothing drawn ----
        Mordor.Modding.ModHost.Active = Mordor.Modding.ModHost.Empty();
        Step(9);
        NoMenu();
        Press(At(2, 3), MouseButton.Right);
        Check("mods off: a right-click on a square re-plots at once (picMaze_MouseDown), no menu", (Plotted(9, 5), Menu() == null), (true, true));
        Apart(); top.MouseMove(At(2, 3)); Pump();
        Press(At(2, 3), MouseButton.Left);
        Check("mods off: hover and click draw nothing (the marks hidden)", (overlay.IsVisible, overlay.Marks.Count, Level()), (false, 0, 1));

        // ---- the mod on ----
        var modHost = new Mordor.Modding.ModHost(new Mordor.Modding.ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
        modHost.Discover(new[] { modsDir });
        modHost.LoadEnabled();
        Mordor.Modding.ModHost.Active = modHost;
        Check("Cartographer switched on (main menu)", modHost.Switch("Cartographer", true), Mordor.Modding.ModSwitchResult.Now);
        Step(10);
        NoMenu();
        Press(At(2, 3), MouseButton.Right);
        Check("mod on: a right-click on a square opens the menu, and does not re-plot by itself", (Menu()?.IsOpen, Plotted(10, 5)), ((bool?)true, false));
        Check("the menu: the original's right-click first, then the colours and Add waypoint...",
              Headers(), $"{AutomapWindow.RedrawItem} | - | Blue | Orange | Red | Purple | No colour | - | Add waypoint...");
        Check("each colour with its swatch, No colour greyed",
              string.Join(" ", Menu()!.Items.OfType<MenuItem>().Skip(1).Take(5).Select(m =>
                  (m.Icon is Border { Background: Avalonia.Media.ISolidColorBrush b } ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : "none") + (m.IsEnabled ? "" : "(greyed)"))),
              "#4E79A7 #F28E2B #E15759 #BC13FE none(greyed)");
        Choose(AutomapWindow.RedrawItem);
        Check("its first item is the original's right-click: the level re-plotted", Plotted(10, 5), true);
        Press(At(2, 3), MouseButton.Right);
        Choose("Blue");
        Check("Blue: the square tinted on the map", Marks(), "Tint(2,3)#4E79A7");
        Check("... kept in cartographer.json in the saves folder (DataDirectory)", File.Exists(Path.Combine(scratch, "cartographer.json")), true);
        Press(At(6, 4), MouseButton.Right);
        Choose("Orange");
        Check("Orange on another", Marks(), "Tint(2,3)#4E79A7 Tint(6,4)#F28E2B");

        // ---- a waypoint to another level ----
        Press(At(2, 3), MouseButton.Right);
        Choose("Add waypoint...");
        string prompt = "Click the destination square (any level). Esc cancels.";
        Check("Add waypoint...: the prompt in the status line (lblInfo)", (win.Picking, info.Text), (true, prompt));
        Press(Centre(Named<Button>("DeeperBtn")), MouseButton.Left);
        Check("the level bar pages the map while it waits", (Level(), win.Picking, info.Text), (2, true, prompt));
        Press(At(5, 6), MouseButton.Left);
        Check("a click on level 2's square takes it: the status line back", (win.Picking, info.Text), (false, "You are at 11,6,1 facing North."));
        Check("level 2 shows nothing of it (the destination is not marked)", Marks(), "");
        win.Page(-1); Pump();
        Check("level 1: the source framed yellow on its blue", Marks(), "Tint(2,3)#4E79A7 Tint(6,4)#F28E2B Frame(2,3)#EDC948");
        Press(At(2, 3), MouseButton.Right);
        Check("its menu now changes or removes it", Headers().EndsWith("Change waypoint... | Remove waypoint", StringComparison.Ordinal), true);
        Menu()?.Close(); Pump();

        // ---- Escape and the right button give a pick up ----
        Press(At(4, 4), MouseButton.Right);
        Choose("Add waypoint...");
        Check("Escape (the frames' keys, AutomapKeys.CancelPick) gives it up", (AutomapKeys.CancelPick(Avalonia.Input.Key.Escape, win), win.Picking, info.Text == prompt), (true, false, false));
        Check("Escape with no pick waiting is left to the game", AutomapKeys.CancelPick(Avalonia.Input.Key.Escape, win), false);
        Press(At(4, 4), MouseButton.Right);
        Choose("Add waypoint...");
        NoMenu();
        Press(At(7, 7), MouseButton.Right);
        Check("a right-click gives it up, and opens no menu", (win.Picking, Menu() == null, Marks().Contains("(4,4)")), (false, true, false));

        // ---- a waypoint on the same level, and the hover ----
        Press(At(5, 5), MouseButton.Right);
        Choose("Add waypoint...");
        Press(At(8, 9), MouseButton.Left);
        Apart(); top.MouseMove(At(5, 5)); Pump();
        Check("the pointer over a source on the same level as its destination: highlighted, an arrow to it",
              Marks().EndsWith("Frame(5,5)#FFFFFF! Arrow(5,5->8,9)#EDC948", StringComparison.Ordinal), true);
        top.MouseMove(At(2, 3)); Pump();
        Check("over a source whose destination is on level 2: highlighted, no arrow",
              (Marks().Contains("Frame(2,3)#FFFFFF!"), Marks().Contains("Arrow")), (true, false));
        top.MouseMove(At(12, 12)); Pump();
        Check("over a plain square: neither", Marks().Contains("#FFFFFF") || Marks().Contains("Arrow"), false);

        // ---- a click on a source goes to its destination ----
        Press(At(2, 3), MouseButton.Left);
        Check("a click on the source: the map on the destination's level", Level(), 2);
        Check("... the destination highlighted", Marks(), "Frame(5,6)#FFFFFF!");
        Check("... the window kept its size", ChildSize(), new Size(fullW, fullH));
        Press(At(1, 20), MouseButton.Left);
        Check("... until the next click", Marks(), "");
        win.Page(-1); Pump();

        // ---- the marks on the map's own squares: 1x, enlarged, 125% ----
        void Rendered(string what)
        {
            Refresh(); Pump();
            double rs = top.RenderScaling, zoom = Field<double>("_zoom");
            Check($"{what}: the marks laid exactly over picMaze", overlay.Bounds, image.Bounds);
            int mw = Field<int>("_mazeW"), mh = Field<int>("_mazeH"), h = H();
            var cols = AutomapLayout.DeviceEdges(mw, zoom * rs);
            var rows = AutomapLayout.DeviceEdges(mh, zoom * rs);
            int dw = cols[mw], dh = rows[mh];
            var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(dw, dh), new Vector(96 * rs, 96 * rs));
            using (var dc = rtb.CreateDrawingContext()) AutomapOverlay.Paint(dc, overlay.Marks, h, cols, rows, zoom * rs, rs);
            var px = new int[dw * dh];
            var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(px.Length * 4);
            try { rtb.CopyPixels(new PixelRect(0, 0, dw, dh), buf, px.Length * 4, dw * 4); System.Runtime.InteropServices.Marshal.Copy(buf, px, 0, px.Length); }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buf); }
            int A(int x, int y) => (px[y * dw + x] >> 24) & 0xFF;
            // the orange square (6,4): its inside exactly the tint, its edge lines untouched
            var (l, t, r, b) = AutomapOverlay.Inside(6, 4, h, cols, rows);
            int inside = 0, wrongInside = 0, edge = 0;
            for (int y = t; y < b; y++) for (int x = l; x < r; x++) { inside++; if (Math.Abs(A(x, y) - AutomapOverlay.TintAlpha) > 1) wrongInside++; }
            for (int y = t - 1; y <= b; y++) { edge += A(l - 1, y) + A(r, y); }
            for (int x = l - 1; x <= r; x++) { edge += A(x, t - 1) + A(x, b); }
            Check($"{what}: the tint fills the square's inside, on whole device pixels ({r - l}x{b - t})", (inside > 0, wrongInside), (true, 0));
            Check($"{what}: inside the map's own square edges (DeviceEdges: map pixels 1 to 13)",
                  (l, t, r, b), (cols[6 * AutomapLayout.Pitch + 1], rows[(h - 1 - 4) * AutomapLayout.Pitch + 1], cols[6 * AutomapLayout.Pitch + AutomapLayout.Pitch], rows[(h - 1 - 4) * AutomapLayout.Pitch + AutomapLayout.Pitch]));
            Check($"{what}: the edge lines round it (its walls) not covered", edge, 0);
            int c = px[((t + b) / 2) * dw + (l + r) / 2];
            int pr = (c >> 16) & 0xFF, pg = (c >> 8) & 0xFF, pb = c & 0xFF;
            Check($"{what}: the tint is the orange (premultiplied at {AutomapOverlay.TintAlpha})",
                  Math.Abs(pr - 0xF2 * AutomapOverlay.TintAlpha / 255) <= 2 && Math.Abs(pg - 0x8E * AutomapOverlay.TintAlpha / 255) <= 2 && Math.Abs(pb - 0x2B * AutomapOverlay.TintAlpha / 255) <= 2, true);
            // the waypoint's frame on (2,3): the yellow ring two map pixels thick inside its edges
            var (fl, ft, fr, fb) = AutomapOverlay.Inside(2, 3, h, cols, rows);
            int ring = px[(ft + 1) * dw + (fl + fr) / 2];
            Check($"{what}: the waypoint's yellow frame inside its square, edge left alone",
                  (((ring >> 16) & 0xFF) == 0xED && ((ring >> 8) & 0xFF) == 0xC9 && (ring & 0xFF) == 0x48, A((fl + fr) / 2, ft - 1)), (true, 0));
        }
        Rendered("1x");
        Settle(); child.PlaceAt(new Rect(10, 10, 1300, 1050)); Pump(); Settle();
        Check("enlarged", Field<double>("_zoom") > 1.5, true);
        Rendered("enlarged");
        Apart(); top.MouseMove(At(5, 5)); Pump();
        Check("enlarged: the pointer still finds the squares (the hover's arrow)", Marks().EndsWith("Arrow(5,5->8,9)#EDC948", StringComparison.Ordinal), true);
        top.Width = 2400; top.Height = 1700; Pump();
        Scaling(1.25);
        Settle(); child.PlaceAt(new Rect(10, 10, 1300, 1050)); Pump(); Settle();
        Rendered("125%, enlarged");
        top.MouseMove(At(2, 3)); Pump();
        Check("125%: the pointer still finds the squares", Marks().Contains("Frame(2,3)#FFFFFF!"), true);
        Scaling(1);
        top.Width = 1400; top.Height = 1100; Pump();
        Settle(); child.PlaceAt(new Rect(40, 40, fullW, fullH)); Pump(); Settle();
        top.MouseMove(new Point(5, 5)); Pump();

        // ---- read back by a new host (the next session), then switched off ----
        string before = Marks();
        var again = new Mordor.Modding.ModHost(new Mordor.Modding.ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
        again.Discover(new[] { modsDir });
        again.LoadEnabled();
        Mordor.Modding.ModHost.Active = again;
        Refresh(); Pump();
        Check("a new session reads cartographer.json back: the same marks", (Marks(), before != ""), (before, true));
        Check("Cartographer switched off", again.Switch("Cartographer", false), Mordor.Modding.ModSwitchResult.Now);
        Pump();
        Check("... its marks gone at once (ModHost.Changed)", overlay.IsVisible, false);
        Step(11);
        NoMenu();
        Press(At(2, 3), MouseButton.Right);
        Check("... and the right button the original's again: a re-plot, no menu", (Plotted(11, 5), Menu() == null), (true, true));
    }
    catch (Exception ex) { Check("Cartographer: the Automap could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { Scaling(1); top.Width = 1400; top.Height = 1100; Pump(); } catch { }
        Mordor.Modding.ModHost.Active = hostWas;
        try { Directory.Delete(scratch, true); } catch { }
    }
}
top.Close();

// ---- the Companions page (2026-10-18: "drag & drop doesn't seem to work for companions"; 2026-10-24:
// "i thought normally when you have a companion you also see its portrait"). The page as both
// windows build it - CompanionsPage on elaTabFrame(2)'s Canvas, the pictures at the form's
// rectangles - wired by CompanionDrag.Wire, and a Comp. box wired as the Confinement's are. The
// headless platform has no drag source of its own, so a stand-in one records what a drag would
// carry and nothing more.
{
    var source = new RecordingDragSource();
    // AvaloniaLocator.CurrentMutable.Bind<IPlatformDragSource>().ToConstant(source), which are internal
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
    var locator = typeof(AvaloniaLocator).GetProperty("CurrentMutable", Any)!.GetValue(null)!;
    var helper = typeof(AvaloniaLocator).GetMethods(Any).First(m => m.Name == "Bind" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
        .MakeGenericMethod(typeof(Avalonia.Input.Platform.IPlatformDragSource)).Invoke(locator, null)!;
    helper.GetType().GetMethods(Any).First(m => m.Name == "ToConstant").MakeGenericMethod(typeof(RecordingDragSource)).Invoke(helper, new object[] { source });

    var monsters = MdrLoader.LoadMonsters(Path.Combine(game, "MDATA5.MDR"));
    // three creatures off the sheet's first row and column, so a wrong row or column shows
    var picked = monsters.Where(m => m.PicId > 10 && m.PicId <= 90 && (m.PicId - 1) % 10 != 0)
                         .GroupBy(m => m.PicId).Select(g => g.First()).Take(3).ToList();
    Companion Of(Monster m, string name, int hits) { var c = CompanionRef.Of(m); c.Name = name; c.Hits = hits; c.MaxHits = Math.Max(1, hits) + 2; return c; }
    var slots = new CompanionSlots();
    slots.Add(Of(picked[0], "A", 5));
    slots.Add(Of(picked[1], "B", 7));
    slots.Add(Of(picked[2], "C", 0));
    Monster? Def(Companion c) => c.Def(monsters);

    var said = new List<string>();
    var named = new List<int>();
    var infos = new List<int>();
    var kicked = new List<int>();
    var attacked = new List<int>();
    bool noStretch = false;
    var canvas = new Canvas { Width = 254, Height = 172, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    var carry = new TextBlock { Width = 222, Height = 14 };
    canvas.Children.Add(carry);
    var comp = new CompanionsPage(canvas, carry, () => noStretch, named.Add, infos.Add, kicked.Add, attacked.Add);
    void Fill() => comp.Show(slots, Def);
    Fill();
    CompanionDrag.Wire(comp.Pictures, () => slots, said.Add, Fill);
    var compBox = new TextBox { Width = 120 };
    string entered = "";
    var refusals = new List<string>();
    Dragging.Target(compBox, DragKind.Companion, p => { compBox.Text = (p.Index + 1).ToString(); entered = compBox.Text; },
                    () => refusals.Add("You may only drag and drop companions to the companion store."));
    var page = new Window { Width = 400, Height = 600, Content = new StackPanel { Children = { canvas, compBox } } };
    page.Show();
    Pump();

    string Rect(Control c) => $"{Canvas.GetLeft(c)},{Canvas.GetTop(c)} {c.Width}x{c.Height}";
    // elaElastic at 60,270 on the page, each picture 750 square in it (MORDOR.EXE RCDATA 10), in pixels
    Check("companions: the pictures where picCompanion(0..3) are, 50 pixels square",
          string.Join(" | ", comp.Pictures.Select(Rect)), "8,21 50x50 | 198,38 50x50 | 8,93 50x50 | 198,111 50x50");
    Check("companions: the names beside them, the right-hand ones right-justified",
          string.Join(" | ", comp.Names.Select(n => $"{Rect(n)} {n.TextAlignment}")),
          "63,22 133x15 Left | 60,58 133x15 Right | 63,94 133x15 Left | 60,130 133x15 Right");
    Check("companions: the hits under the names",
          string.Join(" | ", comp.Hits.Select(Rect)), "64,39 53x15 | 139,75 54x15 | 64,111 54x15 | 141,147 52x15");
    Check("companions: cmdCompanion(3) where (0) was designed, the others a button's width to its left (FRMCHAR3.BAS:388-391)",
          string.Join(" | ", comp.KickButtons.Select(k => $"{k.Content} {Rect(k)}")), "1 179,2 18x17 | 2 197,2 18x17 | 3 215,2 18x17 | 4 233,2 18x17");

    Check("companions: a picture for each filled slot and none for the empty one",
          string.Join(",", comp.Pictures.Select(p => p.IsVisible)), "True,True,True,False");
    Check("companions: each the creature's own cell (MonsterPortrait, picCompanion_Paint)",
          Enumerable.Range(0, 3).All(i => comp.PictureOf(i) != null && ReferenceEquals(comp.PictureOf(i), MonsterPortrait.Of(Def(slots[i]!)!))), true);
    // the cell's pixels are the sheet's at ((PicId - 1) Mod 10, Int((PicId - 1) / 10)) (FRMCHAR3.BAS:812-813)
    {
        var sheet = Art.Bitmap("monsters.png");
        var cell = (Avalonia.Media.Imaging.Bitmap)comp.PictureOf(0)!;
        int pic = picked[0].PicId - 1, col = pic % 10, row = pic / 10;
        byte[] Pixels(Avalonia.Media.Imaging.Bitmap bm, PixelRect r)
        {
            var buf = new byte[r.Width * r.Height * 4];
            var h = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { bm.CopyPixels(r, h.AddrOfPinnedObject(), buf.Length, r.Width * 4); }
            finally { h.Free(); }
            return buf;
        }
        // the sheet is decoded at its own format and the cell drawn into a premultiplied target, so the
        // colours are compared, not the bytes' order: count the pixels whose channels differ by more than 2
        var a = Pixels(sheet, new PixelRect(col * 60, row * 60, 60, 60));
        var b2 = Pixels(cell, new PixelRect(0, 0, 60, 60));
        int Off(byte[] px, bool bgra)
        {
            int n = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                int r1 = bgra ? px[i + 2] : px[i], g1 = px[i + 1], b1 = bgra ? px[i] : px[i + 2];
                if (Math.Abs(r1 - b2[i + 2]) > 2 || Math.Abs(g1 - b2[i + 1]) > 2 || Math.Abs(b1 - b2[i]) > 2) n++;
            }
            return n;
        }
        int off = Math.Min(Off(a, true), Off(a, false));
        Check($"companions: slot 1's picture is the sheet's column {col}, row {row} (PicId {picked[0].PicId})", off, 0);
        var left = Pixels(sheet, new PixelRect((col - 1) * 60, row * 60, 60, 60));
        Check("companions: ... and not the cell beside it", Math.Min(Off(left, true), Off(left, false)) > 0, true);
    }
    Check("companions: stretched, the cell fills the box (StretchBlt, FRMCHAR3.BAS:816)",
          string.Join(" | ", comp.Pictures.Take(3).Select(p => Rect((Control)((Canvas)p.Child!).Children[0]))), "0,0 50x50 | 0,0 50x50 | 0,0 50x50");
    Check("companions: drawn without smoothing", Avalonia.Media.RenderOptions.GetBitmapInterpolationMode((Control)((Canvas)comp.Pictures[0].Child!).Children[0]),
          Avalonia.Media.Imaging.BitmapInterpolationMode.None);
    noStretch = true;
    Fill(); Pump();
    Check("companions: No StretchBlt, the cell at its own size centred - (50 / 2) - (60 / 2) (BitBlt, FRMCHAR3.BAS:818)",
          string.Join(" | ", comp.Pictures.Take(3).Select(p => Rect((Control)((Canvas)p.Child!).Children[0]))), "-5,-5 60x60 | -5,-5 60x60 | -5,-5 60x60");
    Check("companions: and clipped to the box", comp.Pictures[0].ClipToBounds, true);
    noStretch = false;
    Fill(); Pump();

    Check("companions: the names and hits as mdrcharShowCompanions writes them (MAINMOD2.BAS:4730-4735)",
          string.Join(" | ", Enumerable.Range(0, 4).Select(i => comp.Names[i].IsVisible ? $"{comp.Names[i].Text} {comp.Hits[i].Text}" : "-")),
          "1. \"A\" 5\\7 | 2. \"B\" 7\\9 | 3. \"C\" 0\\3 | -");
    Check("companions: a button for each filled slot, the empty one's disabled",
          string.Join(",", comp.KickButtons.Select(k => k.IsEnabled)), "True,True,True,False");

    string Order() => string.Join(",", Enumerable.Range(0, CompanionSlots.Slots).Select(i => slots[i]?.Name ?? "-"));
    Point At(Visual v) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), page)!.Value;
    void Drop(Point p, Avalonia.Input.IDataObject data)
    {
        page.DragDrop(p, Avalonia.Input.Raw.RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
        page.DragDrop(p, Avalonia.Input.Raw.RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);
        page.DragDrop(p, Avalonia.Input.Raw.RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);
        Pump();
    }
    // a press on a picture that moves past the drag rectangle starts the drag (picCompanion_MouseMove)
    DragPayload? Dragged(int slot)
    {
        source.Last = null;
        var from = At(comp.Pictures[slot]);
        page.MouseDown(from, MouseButton.Left); Pump();
        page.MouseMove(from + new Point(3, 3), RawInputModifiers.LeftMouseButton); Pump();
        page.MouseMove(from + new Point(12, 12), RawInputModifiers.LeftMouseButton); Pump();
        page.MouseUp(from + new Point(12, 12), MouseButton.Left); Pump();
        Apart();
        return source.Last;
    }

    var b = Dragged(1);
    Check("companions: a picture dragged carries its companion (\"COMP\", the slot)", b, DragPayload.Companion(1));
    Check("companions: a drag opens nothing", (named.Count, infos.Count).ToString(), "(0, 0)");
    Drop(At(comp.Pictures[0]), b!.Wrapped());
    Check("companions: B dropped on A changes places with it (FRMCHAR3.BAS:775-777)", Order(), "B,A,C,-");
    Check("companions: and the page is repainted with them, pictures and all",
          (comp.Names[0].Text, ReferenceEquals(comp.PictureOf(0), MonsterPortrait.Of(picked[1]))).ToString(), "(1. \"B\", True)");
    Drop(At(comp.Pictures[2]), Dragged(0)!.Wrapped());
    Check("companions: onto the dead one, refused with the original's line", (Order(), said.LastOrDefault()).ToString(), "(B,A,C,-, No use re-arranging a dead companion!)");
    Drop(new Point(At(comp.Pictures[1]).X, At(comp.Pictures[2]).Y + 18), DragPayload.Companion(1).Wrapped());
    Check("companions: let go where the empty slot's hidden picture is, nothing happens", (Order(), said.Count).ToString(), "(B,A,C,-, 1)");
    Drop(At(comp.Pictures[1]), DragPayload.Item(3).Wrapped());
    Check("companions: an item dropped on a companion does nothing", Order(), "B,A,C,-");

    Drop(At(compBox), Dragged(1)!.Wrapped());
    Check("Confinement: a companion dropped on the Comp. box puts its number there and presses Enter (FRMCITYC.BAS:746-748)", (entered, refusals.Count).ToString(), "(2, 0)");
    Drop(At(compBox), DragPayload.Item(3).Wrapped());
    Check("Confinement: anything else is refused with its line (FRMCITYC.BAS:750)", refusals.Count, 1);

    // lblCompName_Click on a plain click; picCompanion_DblClick; the right button; cmdCompanion
    var nameAt = comp.Names[1].TranslatePoint(new Point(comp.Names[1].Bounds.Width - 4, comp.Names[1].Bounds.Height / 2), page)!.Value;
    page.MouseDown(nameAt, MouseButton.Left); page.MouseUp(nameAt, MouseButton.Left); Pump(); Apart();
    Check("companions: a click on a name names that companion (lblCompName_Click)", (string.Join(",", named), comp.Slot).ToString(), "(1, 1)");
    var picAt = At(comp.Pictures[2]);
    page.MouseDown(picAt, MouseButton.Left); page.MouseUp(picAt, MouseButton.Left);
    page.MouseDown(picAt, MouseButton.Left); page.MouseUp(picAt, MouseButton.Left); Pump(); Apart();
    Check("companions: a double-click on a picture is Info (picCompanion_DblClick)", (string.Join(",", infos), comp.Slot).ToString(), "(2, 2)");
    var rightAt = At(comp.Pictures[0]);
    page.MouseDown(rightAt, MouseButton.Right); page.MouseUp(rightAt, MouseButton.Right); Pump(); Apart();
    Check("companions: a right press on a picture is Attack (picCompanion_MouseDown)", string.Join(",", attacked), "0");
    var kickAt = At(comp.KickButtons[1]);
    page.MouseDown(kickAt, MouseButton.Left); page.MouseUp(kickAt, MouseButton.Left); Pump(); Apart();
    Check("companions: button 2 kicks out slot 2's companion (cmdCompanion_Click)", (string.Join(",", kicked), comp.Slot).ToString(), "(1, 1)");

    // a larger window: the right-hand slots and the buttons move right, the lower ones down
    comp.Grow(30, 20);
    Check("companions: a larger page moves the right-hand and lower slots into the room",
          string.Join(" | ", comp.Pictures.Select(p => $"{Canvas.GetLeft(p)},{Canvas.GetTop(p)}")) + $" | {Canvas.GetLeft(comp.KickButtons[0])} | {carry.Width}",
          "8,21 | 228,38 | 8,113 | 228,131 | 209 | 252");
    comp.Grow(0, 0);
    Check("companions: and at the form's size, back where the form has them",
          string.Join(" | ", comp.Pictures.Select(p => $"{Canvas.GetLeft(p)},{Canvas.GetTop(p)}")), "8,21 | 198,38 | 8,93 | 198,111");

    // nobody at all: "No Companions" in lblCompName(0)
    var none = new CompanionSlots();
    comp.Show(none, Def); Pump();
    Check("companions: none at all, \"No Companions\" in the first name and no picture",
          (comp.Names[0].IsVisible, comp.Names[0].Text, comp.Pictures.Any(p => p.IsVisible)).ToString(), "(True, No Companions, False)");
    page.MouseDown(At(comp.Names[0]), MouseButton.Left); page.MouseUp(At(comp.Names[0]), MouseButton.Left); Pump(); Apart();
    Check("companions: and a click on that caption names nobody", named.Count, 1);
    page.Close();
}

// ---- Objects & Spells' four commands take a drop and press themselves: cmdCommand_DragDrop is
// "cmdCommand_Click Index" (FRMCHAR3.BAS:252-254), the only command button with a DragDrop handler
// in the original. Laid out as both windows lay the Items page out - the buttons over the list's
// lower right on a Canvas - and wired by PackCommands.Wire, with the list's own item drop beside it.
{
    var owner = new Character { Name = "o" };
    var other = new Character { Name = "x" };
    var pack = new ListBox { Width = 233, Height = 170, ItemsSource = Enumerable.Range(0, 8).Select(i => new TextBlock { Text = $"slot {i}" }).ToList() };
    var pressed = new List<string>();
    var swaps = new List<string>();
    Dragging.Target(pack, DragKind.Item, p => swaps.Add($"{p.Index}->{pack.SelectedIndex}"));
    var names = new[] { "Equip", "Use", "Drop", "Info" };
    var buttons = names.Select((n, i) => new Button { Content = n, Width = 58, Height = 20, Focusable = false }).ToArray();
    for (int i = 0; i < buttons.Length; i++)
    {
        string n = names[i];
        buttons[i].Click += (_, _) => pressed.Add($"{n}@{pack.SelectedIndex}");
        Canvas.SetLeft(buttons[i], 177); Canvas.SetTop(buttons[i], 91 + 19 * i);
    }
    var canvas = new Canvas { Width = 254, Height = 170, Children = { pack } };
    foreach (var b in buttons) canvas.Children.Add(b);
    PackCommands.Wire(pack, () => owner, buttons);
    var sheet = new Window { Width = 300, Height = 220, Content = canvas };
    sheet.Show();
    Pump();
    pack.SelectedIndex = 2;
    Point Mid(Visual v) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), sheet)!.Value;
    void DropOn(Point p, DragPayload what)
    {
        var data = what.Wrapped();
        sheet.DragDrop(p, Avalonia.Input.Raw.RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
        sheet.DragDrop(p, Avalonia.Input.Raw.RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);
        sheet.DragDrop(p, Avalonia.Input.Raw.RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);
        Pump();
    }
    string Last() => pressed.LastOrDefault() ?? "";

    DropOn(Mid(buttons[1]), DragPayload.Item(5) with { Owner = owner });
    Check("Objects: a pack line dropped on Use presses Use, on that line (lblItem_MouseMove selects it, FRMCHAR3.BAS:592)", Last(), "Use@5");
    DropOn(Mid(buttons[3]), DragPayload.Spell(7) with { Owner = owner });
    Check("Objects: a spell dropped on Info presses Info, on the item selected", Last(), "Info@5");
    DropOn(Mid(buttons[2]), DragPayload.Companion(1));
    Check("Objects: a companion dropped on Drop presses Drop", Last(), "Drop@5");
    DropOn(Mid(buttons[0]), DragPayload.Vault(3));
    Check("Objects: a vault line dropped on Equip presses Equip", Last(), "Equip@5");
    DropOn(Mid(buttons[0]), DragPayload.Member(1));
    Check("Objects: a party name dropped on Equip presses Equip", Last(), "Equip@5");
    DropOn(Mid(buttons[1]), DragPayload.Item(1) with { Owner = other });
    DropOn(Mid(buttons[1]), DragPayload.Item(0));
    Check("Objects: an item from another pack, or the Exchange's, presses it without moving the selection", string.Join(",", pressed.TakeLast(2)), "Use@5,Use@5");
    Check("Objects: one press a drop, and no swap on the list beneath", (pressed.Count, swaps.Count).ToString(), "(7, 0)");
    pack.ScrollIntoView(0); Pump();             // the selection on 5 had scrolled line 1 out of sight
    var line1 = pack.ContainerFromIndex(1)!;   // the line's own row, ListBoxItem
    DropOn(line1.TranslatePoint(new Point(20, line1.Bounds.Height / 2), sheet)!.Value, DragPayload.Item(5) with { Owner = owner });
    Check("Objects: an item let go over a line, clear of the buttons, still changes places with it", (swaps.LastOrDefault(), pressed.Count).ToString(), "(5->1, 7)");
    buttons[2].IsEnabled = false; Pump();
    DropOn(Mid(buttons[2]), DragPayload.Spell(7));
    Check("Objects: a disabled button takes nothing", pressed.Count, 7);
    sheet.Close();
}

// The town's services, each a window of its own as frmCityMain shows them modeless (FRMCTYM.BAS:183-203):
// the Store and the Bank up together, the Store told of a Bank deposit, a picture clicked for a
// service already up bringing it forward without building it again. Saves go to a scratch folder.
{
    string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-town-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    string? savesWas = Environment.GetEnvironmentVariable("MORDOR_SAVES");
    Environment.SetEnvironmentVariable("MORDOR_SAVES", scratch);
    CityWindow? town = null;
    try
    {
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        var hero = new Character
        {
            Name = "Uicheck", Race = RaceData.All[0].Name, CurHits = 10, MaxHits = 10, GoldOnHand = 100,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
        };
        town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
        town.Show();
        Pump();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        void Call(string name, params object?[] a) => typeof(CityWindow).GetMethod(name, flags)!.Invoke(town, a);
        T Get<T>(string name) => (T)typeof(CityWindow).GetField(name, flags)!.GetValue(town)!;
        var services = Get<System.Collections.IList>("_services");
        T Of<T>(object svc, string field) => (T)svc.GetType().GetField(field)!.GetValue(svc)!;
        int OpenCount() => services.Cast<object>().Count(s => Of<Border>(s, "Panel").IsVisible);
        bool OpenServiceShown(GameWindow w) => services.Cast<object>().Any(s => Of<GameWindow?>(s, "Key") == w && Of<Border>(s, "Panel").IsVisible);

        // Objects & Spells made larger than frmCharStat3's client (ObjectsForm.Reflow): the drawing is
        // not zoomed - its text is the Options fonts' size, as every other window's - and the room goes
        // to the lists. Equip to Info stand beside the pack list and its bar, not over the rows; the
        // Spells list shows whole rows, five at the least, with Cast and Info still on the page.
        {
            var sheet = Get<MdiChild>("_sheetChild");
            var tabs = town.FindControl<TabControl>("SheetTabs")!;
            var pack = town.FindControl<ListBox>("PackList")!;
            var bar = town.FindControl<Avalonia.Controls.Primitives.ScrollBar>("PackScroll")!;
            sheet.IsVisible = true;
            Rect In(Control c, Visual to) => new(c.TranslatePoint(default, to)!.Value, c.Bounds.Size);
            foreach (var (w, h, beside) in new[] { (640.0, 520.0, true), (300.0, 240.0, false) })
            {
                sheet.PlaceAt(new Rect(20, 20, w, h));
                tabs.SelectedIndex = 0;
                Pump();
                var page = (Canvas)pack.Parent!;
                var buttons = new[] { "PackEquipBtn", "PackUseBtn", "PackDropBtn", "PackInfoBtn" }.Select(n => town.FindControl<Button>(n)!).ToArray();
                Check($"Objects {w}x{h}: the drawing is not zoomed up", tabs.RenderTransform is not Avalonia.Media.ScaleTransform { ScaleX: > 1 }, true);
                Check($"Objects {w}x{h}: Equip to Info " + (beside ? "beside the list and its bar" : "over the list's lower right, as the form has them"),
                      buttons.All(b => !In(b, page).Intersects(In(pack, page)) && !In(b, page).Intersects(In(bar, page))), beside);
                Check($"Objects {w}x{h}: the buttons inside the page", buttons.All(b => In(b, page).Right <= page.Bounds.Width + 0.5 && In(b, page).Bottom <= page.Bounds.Height + 0.5), true);
                Check($"Objects {w}x{h}: the bar beside the list", In(bar, page).Left >= In(pack, page).Right, true);
                tabs.SelectedIndex = 1;
                Pump();
                var spells = town.FindControl<ListBox>("SpellList")!;
                var spellPage = (Canvas)spells.Parent!;
                var cast = spellPage.Children.OfType<Button>().ToArray();
                double rows = (spells.Bounds.Height - 2) / ObjectsForm.SpellRowPitch;
                Check($"Objects {w}x{h}: the Spells list shows five whole rows at the least", rows >= 5 && Math.Abs(rows - Math.Round(rows)) < 0.01, true);
                Check($"Objects {w}x{h}: Cast and Info below the list and on the page",
                      cast.All(b => In(b, spellPage).Top >= In(spells, spellPage).Bottom && In(b, spellPage).Bottom <= spellPage.Bounds.Height + 0.5), true);
            }
            sheet.IsVisible = false;
            Pump();
        }

        // Ctrl+L, R, C and G set the IndexTab's CurrTab, which raises its Click and so goes through
        // mdrcharSetInfo's WindowState line (MAINMOD2.BAS:4598) - measured in the original
        // (2026-10-25): Ctrl+L on a minimised character window restored it as the active, topmost
        // child; Ctrl+S on a minimised one left it minimised; Ctrl+C on a normal one behind the city
        // changed the tab and left the city active and on top; Ctrl+G on a maximised one restored it
        // to its normal size on the Guild tab
        {
            var mdi = town.FindControl<MdiHost>("Host")!;
            var charWin = Get<MdiChild>("_characterChild");
            var cityWin = Get<MdiChild>("_cityChild");
            var panel = Get<CharacterPanel>("_charPanel");
            void CtrlKey(Key k)
            {
                var e = new KeyEventArgs { Key = k, KeyModifiers = KeyModifiers.Control, RoutedEvent = InputElement.KeyDownEvent };
                typeof(CityWindow).GetMethod("OnKeyDown", flags)!.Invoke(town, new object?[] { e });
                Pump();
            }
            bool OnTop(MdiChild c) => mdi.Windows.Where(w => w != c && w.IsVisible).All(w => w.ZIndex < c.ZIndex);
            void CityInCommand() { mdi.BringToFront(cityWin); mdi.Active = cityWin; Pump(); }
            charWin.IsVisible = true;
            charWin.PlaceAt(new Rect(30, 30, 400, 300));
            panel.Selected = CharTab.Stats;
            charWin.Minimised = true;
            CityInCommand();
            CtrlKey(Key.L);
            Check("Ctrl+L on a minimised character window: restored, on the Look tab",
                  (charWin.Minimised, charWin.Maximised, panel.Selected).ToString(), (false, false, CharTab.Look).ToString());
            Check("Ctrl+L on a minimised character window: the active child, on top", (ReferenceEquals(mdi.Active, charWin), OnTop(charWin)).ToString(), "(True, True)");
            charWin.Minimised = true;
            CityInCommand();
            CtrlKey(Key.S);
            Check("Ctrl+S on a minimised character window: left minimised, the city still active",
                  (charWin.Minimised, panel.Selected, ReferenceEquals(mdi.Active, cityWin)).ToString(), (true, CharTab.Stats, true).ToString());
            charWin.Minimised = false;
            CityInCommand();
            CtrlKey(Key.C);
            Check("Ctrl+C on a normal character window behind the city: the tab changes",
                  (charWin.Minimised, charWin.Maximised, panel.Selected).ToString(), (false, false, CharTab.Char).ToString());
            Check("Ctrl+C on a normal character window behind the city: the city stays active and on top",
                  (ReferenceEquals(mdi.Active, cityWin), cityWin.ZIndex > charWin.ZIndex).ToString(), "(True, True)");
            charWin.Maximised = true;
            mdi.BringToFront(charWin); mdi.Active = charWin; Pump();
            CtrlKey(Key.G);
            Check("Ctrl+G on a maximised character window: back to its normal size, on the Guild tab",
                  (charWin.Minimised, charWin.Maximised, panel.Selected, charWin.Width, charWin.Height).ToString(), (false, false, CharTab.Guild, 400d, 300d).ToString());
            // fn15DF from a service's text box is the same assignment, so the same Click
            charWin.Minimised = true;
            CityInCommand();
            Call("ShowCharacterTab", CharTab.Resist, true);
            Pump();
            Check("Ctrl+R from a service's box on a minimised character window: restored, the active child, on top",
                  (charWin.Minimised, panel.Selected, ReferenceEquals(mdi.Active, charWin), OnTop(charWin)).ToString(), (false, CharTab.Resist, true, true).ToString());
            CityInCommand();
            Call("ShowCharacterTab", CharTab.Misc, true);
            Pump();
            Check("Ctrl+M from a service's box: the tab alone", (panel.Selected, ReferenceEquals(mdi.Active, cityWin), cityWin.ZIndex > charWin.ZIndex).ToString(), (CharTab.Misc, true, true).ToString());

            // the Stats page fills the tab frame across, as elaTabFrame(0) does once Form_Resize has
            // sized tabStatTab to the form (FRMCHAR1.BAS:184-193): the boxes, the divider and the
            // right-set figures reach as far across the page as the form has them, at any width
            var stats = (Canvas)typeof(CharacterPanel).GetField("_stats", flags)!.GetValue(panel)!;
            var viewer = (ScrollViewer)typeof(CharacterPanel).GetField("_statsViewer", flags)!.GetValue(panel)!;
            foreach (var (w, h) in new[] { (465.0, 600.0), (274.0, 368.0) })
            {
                charWin.Minimised = false; charWin.Maximised = false;
                charWin.PlaceAt(new Rect(30, 30, w, h));
                panel.Selected = CharTab.Stats;
                Pump(); Pump();
                double room = viewer.Viewport.Width, frameW = (CharStatForm.FrameLeft + CharStatForm.elaTabFrame0.Width) / 15.0;
                double g = Math.Max(1, room / frameW);
                double Across(int twips) => (CharStatForm.FrameLeft + twips) / 15.0 * g;
                var boxes = stats.Children.OfType<Border>().Select(b => Canvas.GetLeft(b) + b.Width).ToArray();
                Check($"Stats page {w}x{h}: elaTabFrame(0) as wide as the page", room > 0 && Math.Abs(stats.Width - Math.Max(room, frameW)) <= 1, true);
                Check($"Stats page {w}x{h}: elaGroupInfo and elaBorder reach across the frame as the form has them",
                      boxes.Length == 2
                      && Math.Abs(boxes[0] - Across(CharStatForm.elaGroupInfo.Left + CharStatForm.elaGroupInfo.Width)) <= 1.5
                      && Math.Abs(boxes[1] - Across(CharStatForm.elaBorder.Left + CharStatForm.elaBorder.Width)) <= 1.5, true);
                Check($"Stats page {w}x{h}: the figures set right against lblStat's right edge across the frame",
                      stats.Children.OfType<TextBlock>().Count(t => Math.Abs(Canvas.GetLeft(t) + t.Bounds.Width - Across(CharStatForm.lblStat0.Left + CharStatForm.lblStat0.Width)) <= 1.5) >= 7, true);
            }
        }

        Call("ShowStore", town, null);
        Pump();
        Call("ShowBank", town, null);
        Pump();
        Check("Town: the Store and the Bank are up together, each in a window of its own", OpenCount(), 2);
        Check("Town: the Store still hears an UPDATE while the Bank is up (FRMCITYB.BAS:180)",
              typeof(CityWindow).GetField("_storeUpdate", flags)!.GetValue(town) != null, true);
        var storeSvc = services.Cast<object>().First(s => Of<GameWindow?>(s, "Key") == GameWindow.GeneralStore);
        var storePage = Of<ContentControl>(storeSvc, "Page").Content;
        Call("ShowStore", town, null);
        Pump();
        Check("Town: the Store's picture with the Store up brings it forward and builds nothing again",
              ReferenceEquals(Of<ContentControl>(storeSvc, "Page").Content, storePage) && OpenCount() == 2, true);
        Check("Town: and it is the active window", ReferenceEquals(town.FindControl<MdiHost>("Host")!.Active, Of<MdiChild>(storeSvc, "Child")), true);
        {
            // the City clicked (a picture for another service) had covered a service already up
            var city = Get<MdiChild>("_cityChild");
            var mdi = town.FindControl<MdiHost>("Host")!;
            mdi.BringToFront(city);
            Pump();
            Check("Town: the City brought forward stays under the service windows that are up",
                  services.Cast<object>().Where(s => Of<Border>(s, "Panel").IsVisible).All(s => Of<MdiChild>(s, "Child").ZIndex > city.ZIndex), true);
        }
        var bankSvc = services.Cast<object>().First(s => Of<GameWindow?>(s, "Key") == GameWindow.Bank);
        typeof(CityWindow).GetField("_svc", flags)!.SetValue(town, bankSvc);
        Call("CloseService", null, null);
        Pump();
        Check("Town: the Bank shut leaves the Store up", (OpenCount(), Of<Border>(storeSvc, "Panel").IsVisible).ToString(), "(1, True)");
        Check("Town: and the Bank's GOLD and UPDATE go with it",
              typeof(CityWindow).GetField("_bankGold", flags)!.GetValue(town) == null, true);

        // sub14E0 shows frmDungeonMain or frmCityMain, never both (MAINMOD2.BAS:3170-3180); sub0930's
        // screen is for a character alone (3035-3038), and sub14CC brings the town back (3150-3152)
        var cityChild = Get<MdiChild>("_cityChild");
        var party = Get<Party>("_party");
        bool DeadUp() => services.Cast<object>().Any(s => Of<GameWindow?>(s, "Key") == GameWindow.Dungeon && Of<Border>(s, "Panel").IsVisible);
        hero.IsDead = true; hero.CurHits = 0;
        Call("ShowDead");
        Pump();
        Check("Town: dead and alone, the dead screen is up and the town is not shown", (DeadUp(), cityChild.IsVisible).ToString(), "(True, False)");
        var friend = new Character
        {
            Name = "Uicheckb", Race = RaceData.All[0].Name, CurHits = 10, MaxHits = 10,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
        };
        party.Members.Add(friend);
        Call("SwitchedTo");
        Pump();
        Check("Town: company come, the dead screen goes and the town is back", (DeadUp(), cityChild.IsVisible).ToString(), "(False, True)");
        party.Select(1); Call("SwitchedTo"); Pump();
        party.Select(0); Call("SwitchedTo"); Pump();
        Check("Town: switching between the dead and the living leaves the town up", (DeadUp(), cityChild.IsVisible).ToString(), "(False, True)");
        Call("ShowSeer", town, null);
        Pump();
        Check("Town: a dead member in command with company opens a service like anyone (FRMCTYM.BAS:183-190)",
              (DeadUp(), OpenServiceShown(GameWindow.Seer)).ToString(), "(False, True)");
        typeof(CityWindow).GetField("_svc", flags)!.SetValue(town, services.Cast<object>().First(s => Of<GameWindow?>(s, "Key") == GameWindow.Seer));
        Call("CloseService", null, null);
        Pump();
        Check("Town: and closing it puts up no dead screen", (DeadUp(), cityChild.IsVisible).ToString(), "(False, True)");
        party.Members.Remove(friend);
        Call("ShowSeer", town, null);
        Pump();
        Check("Town: alone again, a picture puts the dead screen up", (DeadUp(), cityChild.IsVisible).ToString(), "(True, False)");
        hero.IsDead = false; hero.CurHits = 10;
        Call("RefreshSummary");
        Pump();
        Check("Town: raised, the dead screen goes and the town is back", (DeadUp(), cityChild.IsVisible).ToString(), "(False, True)");

        // frmParty's UPDATE in town (FRMGROUP.BAS:321-346): a member at nought is set dead, saved and unloaded
        var fallen = new Character
        {
            Name = "Uicheckc", Race = RaceData.All[0].Name, CurHits = 0, MaxHits = 10,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
        };
        party.Members.Add(fallen);
        typeof(CityWindow).GetMethod("PruneTheDead", flags)!.Invoke(town, new object?[] { false });
        Pump();
        Check("Town: the party list's UPDATE unloads the one at nought, dead and saved",
              (party.Size, party.Members.Contains(fallen), fallen.IsDead, File.Exists(Path.Combine(scratch, "Uicheckc.json"))).ToString(),
              "(1, False, True, True)");

        // the Control Panel's Form_Unload (FRMCONTR.BAS:245-247): sub0930 for a dead first member - in
        // company, its head and the party list's UPDATE, which unloads them while anyone else stands
        hero.IsDead = true; hero.CurHits = 0;
        party.Members.Add(friend);
        typeof(CityWindow).GetMethod("ArriveDeadSub0930", flags)!.Invoke(town, new object?[] { hero });
        Pump();
        Check("Town: sub0930 for a dead first member in company unloads them and puts up no dead screen",
              (party.Size, party.Members[0] == friend, DeadUp(), cityChild.IsVisible).ToString(), "(1, True, False, True)");
    }
    catch (Exception ex) { Check("Town: the service windows could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { town?.Hide(); Pump(); } catch { }
        Environment.SetEnvironmentVariable("MORDOR_SAVES", savesWas);
        try { Directory.Delete(scratch, true); } catch { }
    }
}

// ---- mods switched on and off on the main menu's Mods page, no restart (ModHost.Switch,
// IModLifecycle, API version 7): a town begun after it carries the running mods' toolbar button,
// Party Information columns, Objects & Spells Sort, Store columns and check box and Bank Sort, and
// none once they are off again; the Control Panel's page greys the Enabled boxes and says where to
// switch, and the mods' own settings stay changeable there ----
Console.WriteLine("\nmods switched on the main menu, and the in-game Mods page:");
{
    string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-mods-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    string? savesWas = Environment.GetEnvironmentVariable("MORDOR_SAVES");
    Environment.SetEnvironmentVariable("MORDOR_SAVES", scratch);
    var hostWas = Mordor.Modding.ModHost.Active;
    CityWindow? town = null;
    try
    {
        // the bundled mods as the build ships them beside the game (src/Mods/ShipMods.targets)
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Mordor.sln"))) root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        string? modsDir = root == null ? null : new[] { "Release", "Debug" }
            .Select(c => Path.Combine(root, "src/Mordor.App", "bin", c, "net8.0", "mods"))
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "QoL")));
        if (modsDir == null) throw new DirectoryNotFoundException("no build of Mordor.App with its mods folder");
        var modHost = new Mordor.Modding.ModHost(new Mordor.Modding.ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
        modHost.Discover(new[] { modsDir });
        modHost.LoadEnabled();   // none is enabled
        Mordor.Modding.ModHost.Active = modHost;

        // the Mods page's boxes, by what they say
        List<CheckBox> Boxes(Window page) => page.GetVisualDescendants().OfType<CheckBox>().ToList();
        CheckBox EnabledOf(Window page, string modTitle) => page.GetVisualDescendants().OfType<Border>()
            .Last(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == modTitle))
            .GetVisualDescendants().OfType<CheckBox>().First(c => Equals(c.Content, "Enabled"));
        string qolTitle = modHost.Discovered.Single(d => d.Id == "QoL").Manifest.Title;

        // the main menu's page: ticking QoL on loads it now
        var menuPage = new ModsWindow(atMainMenu: true);
        menuPage.Show();
        Pump();
        Check("Mods: on the main menu's page the Enabled boxes can be ticked", Boxes(menuPage).Where(c => Equals(c.Content, "Enabled")).All(c => c.IsEnabled), true);
        EnabledOf(menuPage, qolTitle).IsChecked = true;
        Pump();
        Check("Mods: QoL ticked on the main menu runs at once, no restart", modHost.Live.Any(m => m.Id == "QoL"), true);
        Check("Mods: no status line for it: the ticked box says it runs", menuPage.GetVisualDescendants().OfType<TextBlock>().Count(t => t.IsVisible && t.Text is "Running." or "Off."), 0);
        menuPage.Close();
        Pump();

        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        var hero = new Character
        {
            Name = "Uimods", Race = RaceData.All[0].Name, CurHits = 10, MaxHits = 10, GoldOnHand = 100,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
        };
        town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
        town.Show();
        Pump();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        void Call(string name, params object?[] a) => typeof(CityWindow).GetMethod(name, flags)!.Invoke(town, a);
        var services = (System.Collections.IList)typeof(CityWindow).GetField("_services", flags)!.GetValue(town)!;
        T Of<T>(object svc, string field) => (T)svc.GetType().GetField(field)!.GetValue(svc)!;
        Control? PageOf(GameWindow w) => services.Cast<object>()
            .Where(s => Of<GameWindow?>(s, "Key") == w && Of<Border>(s, "Panel").IsVisible)
            .Select(s => Of<ContentControl>(s, "Page").Content as Control).FirstOrDefault();
        var icons = (Panel)typeof(Toolbar).GetField("_icons", flags)!.GetValue(town.FindControl<Toolbar>("Tools"))!;
        var header = town.FindControl<Grid>("PartyHeader")!;
        var packButtons = town.FindControl<StackPanel>("PackModButtons")!;
        static bool Seen(Control c) => c.IsVisible && c.GetVisualAncestors().OfType<Control>().All(a => a.IsVisible || a is Window);
        string ToolbarMods() => string.Join(",", icons.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(b => b.IsVisible).Select(b => b.Content as string));
        string PartyMods() => string.Join(",", header.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "mod-party-column")).Select(t => t.Text));
        string ObjectsMods() => string.Join(",", packButtons.Children.OfType<Button>().Where(b => b.IsVisible).Select(b => b.Content as string));
        string StoreMods()
        {
            var page = PageOf(GameWindow.GeneralStore);
            if (page == null) return "no Store";
            var grid = page.GetVisualDescendants().OfType<DataGrid>().First();
            string heads = string.Join(",", grid.Columns.Where(c => c.IsVisible).Select(c => (c.Header as TextBlock)?.Text?.TrimEnd(' ', '▲', '▼') ?? "")
                .Where(t => t != "" && t != "Item" && !t.Contains("U G N E")));
            bool box = page.GetVisualDescendants().OfType<CheckBox>().Any(b => Equals(b.Content, "Hide unusable") && Seen(b));
            return heads + (box ? " +box" : "");
        }
        string BankMods()
        {
            var page = PageOf(GameWindow.Bank);
            if (page == null) return "no Bank";
            return string.Join(",", page.GetVisualDescendants().OfType<Button>().Where(Seen)
                .Select(b => (b.Content as TextBlock)?.Text ?? b.Content as string).Where(t => t == "Sort"));
        }
        string All() => $"[{ToolbarMods()}] [{PartyMods()}] [{ObjectsMods()}] [{StoreMods()}] [{BankMods()}]";

        Call("ShowStore", town, null);
        Pump();
        Call("ShowBank", town, null);
        Pump();
        Check("Mods: a town begun after carries QoL's toolbar button, party columns, Sort, Store columns and box, and Bank Sort - each once",
              All(), "[] [HP/SP,Next] [Sort] [Slot,Worn +box] [Sort]");

        // the Control Panel's page, in the game: the Enabled boxes greyed, the note, the settings live
        var gamePage = new ModsWindow(atMainMenu: false);
        gamePage.Show(town);
        Pump();
        var enabledBoxes = Boxes(gamePage).Where(c => Equals(c.Content, "Enabled")).ToList();
        Check("Mods: on the in-game page every Enabled box is greyed", enabledBoxes.Count > 0 && enabledBoxes.All(c => !c.IsEnabled), true);
        Check("Mods: and the page says where to switch", gamePage.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == ModsWindow.SwitchAtMainMenu), true);
        var qolMod = modHost.Discovered.Single(d => d.Id == "QoL");
        var qolLabels = qolMod.Manifest.Settings.Select(t => t.Label).ToHashSet();
        Check("Mods: QoL's settings are not listed on the page (1.11.0): no feature box, no Town gold buttons",
              (Boxes(gamePage).Count(c => c.Content is string l && qolLabels.Contains(l)), gamePage.GetVisualDescendants().OfType<RadioButton>().Count()), (0, 0));
        Button SettingsButton(Window page) => page.GetVisualDescendants().OfType<Border>()
            .Last(b => b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == qolTitle))
            .GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Settings..."));
        var settingsButton = SettingsButton(gamePage);
        Check("Mods: a Settings... button in its place, with a tooltip", ToolTip.GetTip(settingsButton) as string, "Open the Quality of Life settings window");
        Check("Mods: no description written on the page (each a tooltip now)",
              gamePage.GetVisualDescendants().OfType<TextBlock>().Count(t => modHost.Discovered.Any(d => t.Text == d.Manifest.Description
                  || d.Manifest.Settings.Any(s => s.Description == t.Text) || d.Manifest.Choices.Any(c => c.Description == t.Text) || d.Manifest.Tables.Any(x => x.Description == t.Text))), 0);
        Check("Mods: QoL's description is its heading's tooltip",
              gamePage.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == qolTitle).Select(t => ToolTip.GetTip(t) as string).FirstOrDefault(), qolMod.Manifest.Description);
        settingsButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        var qw = gamePage.OwnedWindows.OfType<ModSettingsWindow>().SingleOrDefault();
        Check("QoL settings: the button opens its window, in the game too", qw?.Title, "Quality of Life settings");
        if (qw == null) throw new InvalidOperationException("no QoL settings window");
        string Headings() => string.Join(" | ", qw.GetVisualDescendants().OfType<Border>().Where(b => b.BorderThickness.Left == 1 && b.Child is StackPanel sp && sp.Children.Count == 2)
            .Select(b => ((StackPanel)b.Child!).Children[0] switch { CheckBox c => "[" + c.Content + "]", TextBlock t => t.Text, _ => "?" }));
        Check("QoL settings: five groups, General first, Town gold one choice (1.13.0), a box on each heading with features under it",
              Headings(), "[General] | Town gold | [Enhanced character UI] | [Enhanced store UI] | [Color-coded combat messages]");
        var featureBoxes = qw.GetVisualDescendants().OfType<CheckBox>().Where(c => !qw.GroupBoxes.ContainsValue(c)).ToList();
        Check("QoL settings: a box per feature, every one editable in the game",
              (string.Join(" | ", featureBoxes.Select(c => c.Content)), featureBoxes.All(c => c.IsEnabled)),
              ("Unlocked Store alignment choice | Toolbar tooltips | Next column (XP to next level) | HP/SP bars | XP bar | Quest marks | Unidentified-item marks | Slot and Worn columns | Sortable column headings | Note what a sale stocks | Color-coded combat messages", true));
        Check("QoL settings: every feature box's tooltip is its description",
              featureBoxes.All(c => ToolTip.GetTip(c) is string tip && tip == qolMod.Manifest.Settings.Single(s => s.Label == (string)c.Content!).Description), true);
        Check("QoL settings: every group heading's tooltip is the group's line",
              qw.GroupBoxes.All(g => ToolTip.GetTip(g.Value) as string == qolMod.Manifest.SettingsWindow!.Groups.Single(x => x.Label == g.Key).Description), true);
        Check("QoL settings: no description written in it either",
              qw.GetVisualDescendants().OfType<TextBlock>().Count(t => qolMod.Manifest.Settings.Any(s => s.Description == t.Text) || qolMod.Manifest.SettingsWindow!.Groups.Any(g => g.Description == t.Text)), 0);
        var qradios = qw.GetVisualDescendants().OfType<RadioButton>().ToList();
        var townChoice = qolMod.Manifest.Choices.Single(c => c.Key == "TownGold");
        Check("QoL settings: Town gold as three radio buttons, each with its own line as its tooltip, Everyone's own bank picked",
              string.Join(" | ", qradios.Select(r => $"{r.Content}{(r.IsChecked == true ? "*" : "")} ({ToolTip.GetTip(r)})")),
              string.Join(" | ", townChoice.Options.Select((o, i) => $"{o}{(i == 1 ? "*" : "")} ({townChoice.OptionDescriptions[i]})")));
        Check("QoL settings: the three tooltips differ, none the choice's own line", qradios.Select(r => ToolTip.GetTip(r) as string).Distinct().Count(t => t != townChoice.Description), 3);
        Check("QoL settings: the Town gold heading and caption carry the group's and the choice's lines",
              (qw.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == "Town gold").Select(t => ToolTip.GetTip(t) as string).ToList() is var tips
                  && tips.Contains(qolMod.Manifest.SettingsWindow!.Groups.Single(g => g.Label == "Town gold").Description) && tips.Contains(townChoice.Description)), true);
        qradios[2].IsChecked = true;
        Pump();
        Check("QoL settings: picking Leader's bank writes it", (modHost.Settings.Read("QoL", "TownGold"), qradios[1].IsChecked), ("Leader's bank", (bool?)false));
        qradios[0].IsChecked = true;
        Pump();
        Check("QoL settings: picking Off writes it", (modHost.Settings.Read("QoL", "TownGold"), qradios[2].IsChecked), ("Off", (bool?)false));
        modHost.Settings.Write("QoL", "TownGold", "Everyone's own bank");
        Pump();
        Check("QoL settings: changed elsewhere, the pick follows", string.Join(" ", qradios.Select(r => r.IsChecked)), "False True False");

        // a feature on its own, and the group's box: all, mixed, a click for all on, again for all off
        CheckBox Feature(string label) => featureBoxes.Single(c => Equals(c.Content, label));
        var storeGroup = qw.GroupBoxes["Enhanced store UI"];
        void ClickGroup(CheckBox g) { g.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump(); }
        string StoreKeys() => string.Join(" ", new[] { "StoreSlotColumns", "StoreSortable", "SayStoreShelved" }.Select(k => modHost.Settings.ReadFlag("QoL", k, true)));
        Check("QoL settings: all three store features on: the group's box ticked", storeGroup.IsChecked, (bool?)true);
        Feature("Slot and Worn columns").IsChecked = false;
        Pump();
        Check("QoL settings: Slot and Worn columns off alone: the open Store loses the two columns at once, the sort stays",
              (StoreMods(), StoreKeys()), (" +box", "False True True"));
        Check("QoL settings: and the group's box shows mixed", storeGroup.IsChecked, (bool?)null);
        ClickGroup(storeGroup);
        Check("QoL settings: a click on the mixed box switches them all on", (storeGroup.IsChecked, StoreKeys(), StoreMods()), ((bool?)true, "True True True", "Slot,Worn +box"));
        ClickGroup(storeGroup);
        Check("QoL settings: a click with all on switches them all off", (storeGroup.IsChecked, StoreKeys(), Feature("Sortable column headings").IsChecked, StoreMods()),
              ((bool?)false, "False False False", (bool?)false, " +box"));
        ClickGroup(storeGroup);
        Check("QoL settings: and on again", (storeGroup.IsChecked, StoreKeys()), ((bool?)true, "True True True"));
        Feature("HP/SP bars").IsChecked = false;
        Pump();
        Check("QoL settings: the HP/SP bars off alone: the party window keeps Next", (PartyMods(), qw.GroupBoxes["Enhanced character UI"].IsChecked), ("Next", (bool?)null));
        Feature("HP/SP bars").IsChecked = true;
        Pump();
        Check("QoL settings: back: HP/SP and Next", PartyMods(), "HP/SP,Next");
        // General (1.12.0): the unlocked alignment choice (the open Store's list takes the keys) and the toolbar tooltips
        var generalGroup = qw.GroupBoxes["General"];
        bool StoreListTakesKeys() => PageOf(GameWindow.GeneralStore)?.GetVisualDescendants().OfType<DataGrid>().First().IsTabStop ?? false;
        bool AutoMapTip() => town!.FindControl<Toolbar>("Tools")!.GetVisualDescendants().OfType<Button>().Any(b => ToolTip.GetTip(b) as string == "AutoMap (F8)");
        string General() => $"{modHost.StoreAllowsRowChangeWhileChoosing()} {StoreListTakesKeys()} {AutoMapTip()}";
        Check("QoL settings: General's two on by default: the Store's list takes the keys, the toolbar has its tooltips, the group ticked",
              (General(), generalGroup.IsChecked), ("True True True", (bool?)true));
        Feature("Unlocked Store alignment choice").IsChecked = false;
        Pump();
        Check("QoL settings: the unlocked alignment choice off alone: the Store's lock, the tooltips stay, the group mixed",
              (General(), modHost.Settings.ReadFlag("QoL", "UnlockedStoreAlignment", true), generalGroup.IsChecked), ("False False True", false, (bool?)null));
        Feature("Unlocked Store alignment choice").IsChecked = true;
        Feature("Toolbar tooltips").IsChecked = false;
        Pump();
        Check("QoL settings: the toolbar tooltips off alone: none on the toolbar, the Store unlocked again",
              (General(), modHost.Settings.ReadFlag("QoL", "ToolbarTooltips", true), generalGroup.IsChecked), ("True True False", false, (bool?)null));
        Feature("Toolbar tooltips").IsChecked = true;
        Pump();
        Check("QoL settings: the toolbar tooltips on again", (General(), generalGroup.IsChecked), ("True True True", (bool?)true));
        ClickGroup(generalGroup);
        Check("QoL settings: General's box with both on switches both off", (General(), generalGroup.IsChecked), ("False False False", (bool?)false));
        ClickGroup(generalGroup);
        Check("QoL settings: and both on again", (General(), generalGroup.IsChecked), ("True True True", (bool?)true));
        // the XP bar and the quest mark, each its own: drawn in the Next column's cell
        var monsterTable = MdrLoader.LoadMonsters(Path.Combine(game, "MDATA5.MDR"));
        var quester = new Character
        {
            Name = "Quester", Race = RaceData.All[0].Name, CurHits = 10, MaxHits = 10,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1, QuestType = 1, QuestTarget = monsterTable.First(m => m.Id > 0).Id } },
        };
        string NextParts()
        {
            var (lm, col) = modHost.ShownPartyColumns().Single(p => p.Column.Header == "Next");
            var cell = modHost.PartyCell(lm, col, quester, id => monsterTable.FirstOrDefault(m => m.Id == id), _ => null) as Control;
            var all = cell == null ? new List<Avalonia.LogicalTree.ILogical>() : Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(cell).ToList();
            bool bar = cell is Grid g && g.Children.OfType<Border>().Any();
            bool mark = all.OfType<TextBlock>().Any(t => t.Text == "!");
            return $"bar {bar}, mark {mark}";
        }
        Check("QoL settings: the Next cell with both on: the bar and the quest mark", NextParts(), "bar True, mark True");
        Feature("XP bar").IsChecked = false;
        Pump();
        Check("QoL settings: the XP bar off alone: the mark stays", NextParts(), "bar False, mark True");
        Feature("Quest marks").IsChecked = false;
        Pump();
        Check("QoL settings: the quest marks off too: the figure alone", NextParts(), "bar False, mark False");
        ClickGroup(qw.GroupBoxes["Enhanced character UI"]);
        Check("QoL settings: the group's box puts them back", (NextParts(), qw.GroupBoxes["Enhanced character UI"].IsChecked), ("bar True, mark True", (bool?)true));
        modHost.Settings.WriteFlag("QoL", "SayStoreShelved", false);
        Pump();
        Check("QoL settings: a setting changed elsewhere moves its box and the group's", (Feature("Note what a sale stocks").IsChecked, qw.GroupBoxes["Enhanced store UI"].IsChecked), ((bool?)false, (bool?)null));
        modHost.Settings.WriteFlag("QoL", "SayStoreShelved", true);
        Check("QoL settings: no heading box for Town gold (a choice, no toggles)", qw.GroupBoxes.ContainsKey("Town gold"), false);
        Pump();
        qw.Close();
        Pump();
        Check("Mods: QoL still running", modHost.Live.Any(m => m.Id == "QoL"), true);
        gamePage.Close();
        town.Hide();
        Pump();
        town = null;

        // back on the main menu: QoL ticked off, and a town begun after is the original's
        Mordor.Modding.ModHost.Active.SetGame(null);
        menuPage = new ModsWindow(atMainMenu: true);
        menuPage.Show();
        Pump();
        EnabledOf(menuPage, qolTitle).IsChecked = false;
        Pump();
        // its settings are settings, not the mod switched on: the window opens from the main menu's
        // page with the mod off too, an older settings.ini brought up first (the mod's own Migrate,
        // ModHost.MigrateSettings), and its boxes write settings.ini
        foreach (var k in new[] { "StoreSlotColumns", "StoreSortable", "SayStoreShelved" }) modHost.Settings.Remove("QoL", k);
        modHost.Settings.Write("QoL", "EnhancedStoreUI", "False");   // as 1.9.0 to 1.10.0 left it
        SettingsButton(menuPage).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        var offWin = menuPage.OwnedWindows.OfType<ModSettingsWindow>().SingleOrDefault();
        var offBox = offWin?.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => Equals(c.Content, "Note what a sale stocks"));
        Check("QoL settings: from the main menu with QoL off, the window and its boxes", (offWin != null, offBox?.IsEnabled), (true, (bool?)true));
        Check("QoL settings: the old merged key migrated first: the boxes show what the mod will use",
              (offBox?.IsChecked, offWin?.GroupBoxes["Enhanced store UI"].IsChecked, modHost.Settings.Read("QoL", "EnhancedStoreUI") ?? "gone", modHost.Settings.Read("QoL", "SayStoreShelved")),
              ((bool?)false, (bool?)false, "gone", "False"));
        if (offBox != null) { offBox.IsChecked = true; Pump(); }
        Check("QoL settings: and a box there writes the setting", modHost.Settings.ReadFlag("QoL", "SayStoreShelved", false), true);
        SettingsButton(menuPage).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check("QoL settings: a second click brings the open window forward, no second one",
              (menuPage.OwnedWindows.OfType<ModSettingsWindow>().Count(), ModsWindow.OpenSettings.TryGetValue("QoL", out var openOne) && openOne == offWin), (1, true));
        foreach (var k in new[] { "StoreSlotColumns", "StoreSortable" }) modHost.Settings.WriteFlag("QoL", k, true);
        offWin?.Close();
        Pump();
        menuPage.Close();
        Check("Mods: QoL ticked off on the main menu stops at once", modHost.Live.Any(m => m.Id == "QoL"), false);
        town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
        town.Show();
        Pump();
        services = (System.Collections.IList)typeof(CityWindow).GetField("_services", flags)!.GetValue(town)!;
        icons = (Panel)typeof(Toolbar).GetField("_icons", flags)!.GetValue(town.FindControl<Toolbar>("Tools"))!;
        header = town.FindControl<Grid>("PartyHeader")!;
        packButtons = town.FindControl<StackPanel>("PackModButtons")!;
        Call("ShowStore", town, null);
        Pump();
        Call("ShowBank", town, null);
        Pump();
        Check("Mods: then a town begun after is the original's", All(), "[] [] [] [] []");
    }
    catch (Exception ex) { Check("Mods: the Mods pages and the town could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { town?.Hide(); Pump(); } catch { }
        Mordor.Modding.ModHost.Active = hostWas;
        Environment.SetEnvironmentVariable("MORDOR_SAVES", savesWas);
        try { Directory.Delete(scratch, true); } catch { }
    }
}

// ---- the mods' windows (ModWindows, AddWindow): child windows of the game's frame like Objects &
// Spells - the same caption bar, borders, grip and boxes, the game's list-row font, a click that
// does not take the active child, nothing that keeps the game's keys, no window of their own;
// opened from the toolbar only (no Open button on either Mods page); an old screen-pixel place not
// used, a place held inside the frame; closed with the close box for good; taken down to the
// dungeon and back up the town's own way (EnterDungeon, RunInside), a maximised one filling the
// dungeon's area ----
Console.WriteLine("\nthe mods' windows, children of the game's frame:");
{
    string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-modwin-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    string? savesWas = Environment.GetEnvironmentVariable("MORDOR_SAVES");
    Environment.SetEnvironmentVariable("MORDOR_SAVES", scratch);
    var hostWas = Mordor.Modding.ModHost.Active;
    CityWindow? town = null;
    DungeonWindow? down = null;
    try
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Mordor.sln"))) root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        string? modsDir = root == null ? null : new[] { "Release", "Debug" }
            .Select(c => Path.Combine(root, "src/Mordor.App", "bin", c, "net8.0", "mods"))
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "QoL")));
        if (modsDir == null) throw new DirectoryNotFoundException("no build of Mordor.App with its mods folder");
        var modHost = new Mordor.Modding.ModHost(new Mordor.Modding.ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
        modHost.Discover(new[] { modsDir });
        modHost.LoadEnabled();
        Mordor.Modding.ModHost.Active = modHost;
        foreach (var id in new[] { "QoL", "DpsMeter", "Kaizen" }) modHost.Switch(id, true);
        Check("ModWin: the three bundled mods run", string.Join(",", modHost.Live.Select(m => m.Id).OrderBy(s => s)), "DpsMeter,Kaizen,QoL");

        List<Button> OpenButtons(Window page) => page.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Content is string s && s.StartsWith("Open ", StringComparison.Ordinal)).ToList();
        var menuPage = new ModsWindow(atMainMenu: true);
        menuPage.Show();
        Pump();
        Check("ModWin: the Mods page offers no Open button (the windows open from the toolbar)", OpenButtons(menuPage).Count, 0);
        // each mod: its name and version as the heading (its description the tooltip), then the Enabled box
        var dpsGroup = menuPage.GetVisualDescendants().OfType<StackPanel>()
            .First(p => p.Children.OfType<TextBlock>().Any(t => t.Text == "DPS Meter 1.1.0"));
        var dpsLines = ((StackPanel)dpsGroup.Children[1]).Children.Where(c => c.IsVisible).ToList();
        Check("ModWin: a mod's group is its heading, its description the heading's tooltip, then the Enabled box (no inline description)",
              (dpsLines.Count, (dpsLines[0] as CheckBox)?.Content, ToolTip.GetTip(dpsGroup.Children[0]) as string),
              (1, (object?)"Enabled", "Shows damage dealt or XP gained by each character"));
        // Kaizen keeps its few settings on the page, each description a tooltip
        var kaizenSpecs = modHost.Discovered.Single(d => d.Id == "Kaizen").Manifest.Settings;
        var kaizenBoxes = menuPage.GetVisualDescendants().OfType<CheckBox>().Where(c => kaizenSpecs.Any(s => Equals(c.Content, s.Label))).ToList();
        Check("ModWin: Kaizen's settings inline on the page, each with its description as the tooltip",
              (kaizenBoxes.Count, kaizenBoxes.All(c => ToolTip.GetTip(c) as string == kaizenSpecs.Single(s => Equals(c.Content, s.Label)).Description)), (2, true));
        Check("ModWin: no description written on the page",
              menuPage.GetVisualDescendants().OfType<TextBlock>().Count(t => modHost.Discovered.Any(d => t.Text == d.Manifest.Description || d.Manifest.Settings.Any(s => s.Description == t.Text))), 0);
        Check("ModWin: every mod setting has at most one short line of description",
              modHost.Discovered.SelectMany(d => d.Manifest.Settings).Where(t => t.Description.Length > 110 || t.Description.Count(ch => ch == '.') > 1)
                  .Select(t => t.Key).FirstOrDefault(), null);
        menuPage.Close();
        Pump();

        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        Character Hero(string name) => new Character
        {
            Name = name, Race = RaceData.All[0].Name, CurHits = 10, MaxHits = 10, GoldOnHand = 100,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
        };
        town = new CityWindow(new Party(Hero("Uiwin"), Hero("Uiwinb")), game) { Width = 1400, Height = 1000 };
        town.Show();
        Pump();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var frameArea = town.FindControl<MdiHost>("Host")!;
        var icons = (Panel)typeof(Toolbar).GetField("_icons", flags)!.GetValue(town.FindControl<Toolbar>("Tools"))!;
        Avalonia.Controls.Primitives.ToggleButton Tool(string title) =>
            icons.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(b => Equals(b.Content, title));
        void Press(Button b) { b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump(); }
        MdiChild? Child(MdiHost h, string title) => h.Windows.FirstOrDefault(c => c.Title == title && c.IsVisible);
        T ChildField<T>(MdiChild c, string name) => (T)typeof(MdiChild).GetField(name, flags)!.GetValue(c)!;

        var activeBefore = frameArea.Active;
        Press(Tool("DPS Meter"));
        var dps = Child(frameArea, "DPS Meter");
        Check("ModWin: the toolbar's DPS Meter opens a child window in the town's frame", dps != null, true);
        Check("ModWin: and no window of its own", town.OwnedWindows.Count, 0);
        Check("ModWin: the toolbar button shows it open", Tool("DPS Meter").IsChecked, true);
        if (dps == null) throw new InvalidOperationException("no DPS Meter child");

        // the caption bar, border and boxes are a game child's: Objects & Spells'
        var sheet = frameArea.Windows.First(c => c.Title == "Objects & Spells");
        string Look(MdiChild c)
        {
            var cap = ChildField<TextBlock>(c, "_caption");
            var bar = ChildField<Border>(c, "_titleBar");
            return $"{cap.FontFamily} {cap.FontWeight} {cap.FontSize} {cap.Foreground} | {bar.Background} {bar.Height} | {c.Background} {c.BorderBrush} {c.BorderThickness}";
        }
        Check("ModWin: its caption bar, caption font and border are Objects & Spells'", Look(dps), Look(sheet));
        Check("ModWin: with the minimise, maximise and close boxes and the sizing grip",
              (ChildField<Button>(dps, "_minBtn").IsVisible, ChildField<Button>(dps, "_maxBtn").IsVisible, ChildField<Button>(dps, "_closeBtn").IsVisible, ChildField<Border>(dps, "_grip").IsVisible),
              (true, true, true, true));

        // the text: Party Information's row font, through the Options fonts
        var partyRows = town.FindControl<StackPanel>("PartyRows")!;
        string FontOf(Visual v) => $"{v.GetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty)} {v.GetValue(Avalonia.Controls.Documents.TextElement.FontSizeProperty)}";
        Control? Tagged(Control under, string tag) => under.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => Equals(c.Tag, tag));
        var dpsName = dps.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => Equals(t.Tag, "Name"));
        Check("ModWin: the DPS Meter's rows are in Party Information's row font", dpsName != null ? FontOf(dpsName) : "no row", FontOf(partyRows));
        Check("ModWin: its heading bold, as Party Information's header",
              dps.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is "Damage" || Equals(t.Tag, "KillsHeader")).All(t => t.FontWeight == Avalonia.Media.FontWeight.Bold), true);

        // a click on a bar inside the frame: the breakdown, the window forward, the active child kept
        var bar = Tagged(dps, "Bar:Uiwinb")!;
        var at = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, bar.Bounds.Height / 2), town)!.Value;
        town.MouseDown(at, MouseButton.Left); town.MouseUp(at, MouseButton.Left); Pump();
        string breakdown = ((TextBlock)Tagged(dps, "Breakdown")!).Text ?? "";
        Check("ModWin: a click on a DPS bar in the frame shows its breakdown", breakdown.Contains("Physical") && breakdown != Mordor.Mod.DpsMeter.DpsView.NoneSelected, true);
        Check("ModWin: the click brings it forward but leaves the active child", (dps.ZIndex >= frameArea.Windows.Max(c => c.ZIndex), frameArea.Active == activeBefore), (true, true));
        Check("ModWin: nothing in it has the keyboard", town.FocusManager?.GetFocusedElement() is Visual f && dps.IsVisualAncestorOf(f), false);

        // Kaizen and the Combat Log from the toolbar too; the in-game Mods page opens none
        var gamePage = new ModsWindow(atMainMenu: false);
        gamePage.Show(town);
        Pump();
        Check("ModWin: the in-game Mods page has no Open button either", OpenButtons(gamePage).Count, 0);
        gamePage.Close();
        Pump();
        Press(Tool("Kaizen"));
        var kaizen = Child(frameArea, "Kaizen");
        Check("ModWin: the toolbar's Kaizen opens it as a child of the frame", kaizen != null && TopLevel.GetTopLevel(kaizen) == town, true);
        if (kaizen == null) throw new InvalidOperationException("no Kaizen child");
        var kBody = (Control)kaizen.Body!;
        var kTexts = kBody.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible && !string.IsNullOrEmpty(t.Text)).Select(t => t.Text).ToList();
        Check("ModWin: Kaizen's heading and its one line of gestures", string.Join(" | ", kTexts.Take(2)),
              "Buff list of \"Uiwin\" | Drag a spell from the Spells list or an item from their pack to add a row. Drag a row up or down to move it. "
              + "Double-click a row to change its target. Drag a row out of the window or right-click to remove it.");
        Check("ModWin: Kaizen takes drops inside the frame, in the list-row font",
              (kBody.GetVisualDescendants().OfType<DockPanel>().Any(DragDrop.GetAllowDrop), FontOf(kBody.GetVisualDescendants().OfType<TextBlock>().First())),
              (true, FontOf(partyRows)));
        Check("ModWin: QoL has no window or toolbar button any more (the Combat Log is gone)",
              (modHost.Windows.Any(w => w.Mod.Id == "QoL"), icons.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Any(b => Equals(b.Content, "Combat Log"))), (false, false));
        Check("ModWin: still no window of their own", town.OwnedWindows.Count, 0);

        // the boxes: minimised to an icon in the frame's bottom row, then the close box for good
        dps.PlaceAt(new Rect(30, 40, 400, 250));
        ChildField<Button>(dps, "_minBtn").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check("ModWin: minimised like a game child (its bar, in the bottom row)", (dps.Minimised, Canvas.GetTop(dps) > 500), (true, true));
        ChildField<Button>(dps, "_minBtn").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        ChildField<Button>(dps, "_closeBtn").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check("ModWin: the close box closes it for good: out of the frame, the button up, not wanted open",
              (Child(frameArea, "DPS Meter") == null, Tool("DPS Meter").IsChecked, modHost.Settings.ReadFlag("DpsMeter", "Window.DPSMeter.Open", true)), (true, (bool?)false, false));
        Check("ModWin: its place in the frame is kept (left, top, width, height, state)", modHost.Settings.Read("DpsMeter", "Window.DPSMeter.Place"), "30,40,400,250,0");
        Press(Tool("DPS Meter"));
        dps = Child(frameArea, "DPS Meter");
        Check("ModWin: opened again, where it stood", dps?.Placement(), new Rect(30, 40, 400, 250));
        if (dps == null) throw new InvalidOperationException("no DPS Meter child");

        // a place kept by the earlier top-level windows (four numbers, screen pixels) is not used:
        // the window opens in the middle of the frame; a place past the frame's edge is pulled in whole
        bool Inside(MdiChild c, MdiHost h) =>
            Canvas.GetLeft(c) >= h.ScrollOffset.X - 0.5 && Canvas.GetTop(c) >= h.ScrollOffset.Y - 0.5
            && Canvas.GetLeft(c) + c.Width <= h.ScrollOffset.X + h.Bounds.Width + 0.5
            && Canvas.GetTop(c) + c.Height <= h.ScrollOffset.Y + h.Bounds.Height + 0.5;
        Press(Tool("Kaizen"));
        modHost.Settings.Write("Kaizen", "Window.Kaizen.Place", "1718,152,442,234");
        Press(Tool("Kaizen"));
        var log = Child(frameArea, "Kaizen")!;
        Check("ModWin: an old screen-pixel place (four numbers) is not used: opened in the middle of the frame, whole",
              (log.Width != 442, Inside(log, frameArea), Math.Abs(Canvas.GetLeft(log) + log.Width / 2 - frameArea.Bounds.Width / 2) < 2), (true, true, true));
        Press(Tool("Kaizen"));
        modHost.Settings.Write("Kaizen", "Window.Kaizen.Place", "1300,900,520,360,0");
        Press(Tool("Kaizen"));
        log = Child(frameArea, "Kaizen")!;
        Check("ModWin: a place past the frame's edge is pulled in whole", (Inside(log, frameArea), log.Width, log.Height), (true, 520.0, 360.0));
        Press(Tool("Kaizen"));
        modHost.Settings.Write("Kaizen", "Window.Kaizen.Place", "40,60,300,200,0");
        Press(Tool("Kaizen"));
        kaizen = Child(frameArea, "Kaizen")!;

        // the party goes down, the town's own way (EnterDungeon, DungeonWindow.RunInside): the
        // dungeon's toolbar and frame area replace the town's in the same window, and the open mod
        // windows move into it - a maximised one filling the dungeon's area, as a game child does
        ChildField<Button>(dps, "_maxBtn").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check("ModWin: maximised in the town's frame", (dps.Maximised, Math.Abs(dps.Width - frameArea.Bounds.Width) < 1), (true, true));
        typeof(CityWindow).GetMethod("EnterDungeon", flags)!.Invoke(town, new object?[] { null, new Avalonia.Interactivity.RoutedEventArgs() });
        for (int i = 0; i < 10; i++) Pump();
        down = typeof(CityWindow).GetField("_below", flags)!.GetValue(town) as DungeonWindow;
        var downArea = town.GetVisualDescendants().OfType<MdiHost>().FirstOrDefault();
        Check("ModWin: the dungeon is up in the town's window, with a frame area of its own", (down != null, downArea != null && downArea != frameArea), (true, true));
        if (down == null || downArea == null) throw new InvalidOperationException("the dungeon did not come up");
        Check("ModWin: the open windows went down with the party",
              (Child(downArea, "DPS Meter") == dps, Child(downArea, "Kaizen") == kaizen, frameArea.Windows.Any(c => c.Title is "DPS Meter" or "Kaizen")), (true, true, false));
        Check("ModWin: the maximised one fills the dungeon's area and is still maximised",
              (dps.Maximised, Math.Abs(dps.Width - downArea.Bounds.Width) < 1, Math.Abs(dps.Height - downArea.Bounds.Height) < 1), (true, true, true));
        Check("ModWin: the other where it stood, whole in the dungeon's area", Inside(kaizen, downArea), true);
        ChildField<Button>(dps, "_maxBtn").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check("ModWin: restored down there: its place, inside the area, movable again", (dps.Maximised, dps.Placement(), Inside(dps, downArea)), (false, new Rect(30, 40, 400, 250), true));
        Check("ModWin: still no window of their own below", town.OwnedWindows.Count, 0);

        // Color-coded combat messages: the dungeon's own Messages window, a line of the kind the
        // engine gave it in the old Combat Log's colour, the text untouched; off, or QoL off: plain
        var messages = (ListBox)typeof(DungeonWindow).GetField("MessageLog", flags)!.GetValue(down)!;
        var say = typeof(DungeonWindow).GetMethod("Log", flags, new[] { typeof(string), typeof(MessageKind) })!;
        string Said(string line, MessageKind kind)
        {
            say.Invoke(down, new object[] { line, kind });
            for (int i = 0; i < 3; i++) Pump();
            var tb = messages.GetVisualDescendants().OfType<ListBoxItem>().Select(r => r.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == line)).LastOrDefault(t => t != null);
            return tb == null ? "no row" : tb.IsSet(TextBlock.ForegroundProperty) ? tb.Foreground?.ToString() ?? "null" : "plain";
        }
        modHost.Settings.WriteFlag("QoL", "ColorCodedCombatMessages", true);
        Check("ModWin: Messages, a foe's line in indian red", Said("You were killed by a Kobold.", MessageKind.Foes), "IndianRed");
        Check("ModWin: Messages, the party's in light green", Said("\"Uiwin\" heals \"Uiwinb\".", MessageKind.Party), "LightGreen");
        Check("ModWin: Messages, a line of no kind plain", Said("You found a chest.", MessageKind.None), "plain");
        modHost.Settings.WriteFlag("QoL", "ColorCodedCombatMessages", false);
        Check("ModWin: Messages, the toggle off: a foe's line plain", Said("The Kobold hits!", MessageKind.Foes), "plain");
        string Shown(ListBox box, string line)
        {
            var tb = box.GetVisualDescendants().OfType<ListBoxItem>().Select(r => r.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == line)).LastOrDefault(t => t != null);
            return tb == null ? "no row" : tb.IsSet(TextBlock.ForegroundProperty) ? tb.Foreground?.ToString() ?? "null" : "plain";
        }
        Check("ModWin: Messages, a line keeps the look it was added with: the earlier foe's line still indian red", Shown(messages, "You were killed by a Kobold."), "IndianRed");
        modHost.Settings.WriteFlag("QoL", "ColorCodedCombatMessages", true);
        modHost.Switch("QoL", false);
        Check("ModWin: Messages, QoL off: plain", Said("The Kobold hits again!", MessageKind.Foes), "plain");
        modHost.Switch("QoL", true);

        // and back up: the town's toolbar and area come back, and so do the windows
        down.Close();
        down = null;
        for (int i = 0; i < 10; i++) Pump();
        Check("ModWin: back in town, the windows come back into the town's frame, still open",
              (Child(frameArea, "DPS Meter") == dps, Child(frameArea, "Kaizen") == kaizen, Tool("DPS Meter").IsChecked == true, dps.Placement()), (true, true, true, new Rect(30, 40, 400, 250)));
        var townMessages = (ListBox)typeof(CityWindow).GetField("MessageLog", flags)!.GetValue(town)!;
        Check("ModWin: the dungeon's Messages carried up to town, each line with its own look",
              $"{Shown(townMessages, "You were killed by a Kobold.")} {Shown(townMessages, "You found a chest.")} {Shown(townMessages, "The Kobold hits!")} {Shown(townMessages, "\"Uiwin\" heals \"Uiwinb\".")}",
              "IndianRed plain plain LightGreen");
        Check("ModWin: no handler failed", string.Join("; ", modHost.Live.Where(m => !m.Running).Select(m => m.Id)), "");
    }
    catch (Exception ex) { Check("ModWin: the mods' windows could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { down?.Close(); Pump(); town?.Hide(); Pump(); } catch { }
        Mordor.Modding.ModHost.Active = hostWas;
        Environment.SetEnvironmentVariable("MORDOR_SAVES", savesWas);
        try { Directory.Delete(scratch, true); } catch { }
    }
}

// ---- the dungeon's Take button: cmdAction(2), which hands sub0456 the t key, Take Stairs
// (FRMDUNM.BAS:94-95). The port had it open Objects & Spells. Pressed on level 1's stairs up, where
// the town's own way down puts the party, it climbs out to the town, as t does there ----
Console.WriteLine("\nthe dungeon's Take button:");
{
    string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-take-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    string? savesWas = Environment.GetEnvironmentVariable("MORDOR_SAVES");
    Environment.SetEnvironmentVariable("MORDOR_SAVES", scratch);
    CityWindow? town = null;
    DungeonWindow? down = null;
    try
    {
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        var hero = new Character
        {
            Name = "Uitake", Race = RaceData.All[0].Name, CurHits = 10, MaxHits = 10, GoldOnHand = 100,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
        };
        town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
        town.Show();
        Pump();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(CityWindow).GetMethod("EnterDungeon", flags)!.Invoke(town, new object?[] { null, new Avalonia.Interactivity.RoutedEventArgs() });
        for (int i = 0; i < 10; i++) Pump();
        down = typeof(CityWindow).GetField("_below", flags)!.GetValue(town) as DungeonWindow;
        if (down == null) throw new InvalidOperationException("the dungeon did not come up");
        var nav = (DungeonNav)typeof(DungeonWindow).GetField("_nav", flags)!.GetValue(down)!;
        Check("Take: the party stands on level 1's stairs up",
              ((int)typeof(DungeonWindow).GetField("_level", flags)!.GetValue(down)!, nav.Floor.Has(nav.X, nav.Y, Floor.STAIRS_UP)), (1, true));
        var take = (Button)typeof(DungeonWindow).GetField("TakeBtn", flags)!.GetValue(down)!;
        take.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        for (int i = 0; i < 10; i++) Pump();
        Check("Take: the stairs taken - the dungeon gone and the party in town, as t does",
              (typeof(CityWindow).GetField("_below", flags)!.GetValue(town) == null, hero.DungeonLevel), (true, 0));
        if (typeof(CityWindow).GetField("_below", flags)!.GetValue(town) == null) down = null;
    }
    catch (Exception ex) { Check("Take: the dungeon's Take button could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { down?.Close(); Pump(); town?.Hide(); Pump(); } catch { }
        Environment.SetEnvironmentVariable("MORDOR_SAVES", savesWas);
        try { Directory.Delete(scratch, true); } catch { }
    }
}

// ---- a character loaded dead below ground: sub14E0 shows frmDungeonMain and then sub0930 Me
// (MAINMOD2.BAS:3172-3182) - the dead screen in the dungeon window, its readout "(You are Dead)"
// (MAZEMOD.BAS:310) and its buttons kept, as the original shows it (observed 2026-10-26). The port
// had put them on the city's dead screen ----
Console.WriteLine("\na character loaded dead below ground:");
{
    string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-deadload-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    string? savesWas = Environment.GetEnvironmentVariable("MORDOR_SAVES");
    Environment.SetEnvironmentVariable("MORDOR_SAVES", scratch);
    CityWindow? town = null;
    DungeonWindow? down = null;
    try
    {
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        var first = MdrLoader.LoadDungeon(mdr)[0];
        // a square of a room (an area above nought, so sub0A91 reads a record for it), not rock
        var (sx, sy) = Enumerable.Range(0, first.Height).SelectMany(y => Enumerable.Range(0, first.Width).Select(x => (x, y)))
                                 .First(p => first.AreaIdAt(p.x, p.y) > 0 && !first.Has(p.x, p.y, Floor.ROCK));
        // and a hostile group standing in it, as the level's file keeps one (Foes(0).Engaged = 1), stamped
        // now so that it is read back rather than made afresh
        var monsters = MdrLoader.LoadMonsters(Path.Combine(game, "MDATA5.MDR"));
        int foe = monsters.FindIndex(m => m.Id != 0 && m.LevelFound == 1 && m.Hits > 0);
        var room = new RoomRecord();
        room.Foes[0].Ref = foe; room.Foes[0].KnownAs = foe; room.Foes[0].GroupCount = 1;
        room.Foes[0].Hits = room.Foes[0].MaxHits = (int)monsters[foe].Hits;
        room.Foes[0].Engaged = 1; room.Foes[0].Stamp = DungeonState.SecondsSinceMidnight(DateTime.Now);
        var dstate = DungeonState.Load(scratch);
        dstate.StoreRoom(1, first.AreaIdAt(sx, sy), room);
        dstate.Save(scratch);
        var body = new Character
        {
            Name = "Uidead", Race = RaceData.All[0].Name, CurHits = 0, MaxHits = 10, GoldOnHand = 100,
            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
            IsDead = true, DungeonLevel = 1, X = sx, Y = sy, Facing = 1, CombatSetting = 1,
        };
        town = new CityWindow(new Party(body), game) { Width = 1400, Height = 1000 };
        town.Show();
        for (int i = 0; i < 12; i++) Pump();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        down = typeof(CityWindow).GetField("_below", flags)!.GetValue(town) as DungeonWindow;
        Check("loaded dead below ground: the dungeon window comes up, not the city's dead screen", down != null, true);
        if (down == null) throw new InvalidOperationException("the dungeon did not come up");
        T Ctl<T>(string name) => (T)typeof(DungeonWindow).GetField(name, flags)!.GetValue(down)!;
        Check("... its readout says (You are Dead)", Ctl<TextBlock>("PosText").Text, "(You are Dead)");
        Check("... sub0930's line and Wait for Rescue (MAINMOD2.BAS:3045-3052)",
              (Ctl<TextBlock>("MsgText").Text, Ctl<Button>("WaitBtn").IsVisible, Ctl<Button>("WaitBtn").Content),
              (Rescue.DeadInTheDungeon, true, (object?)"_Wait for Rescue"));
        Check("... and the command buttons are there", (Ctl<Button>("TakeBtn").IsVisible, Ctl<Button>("ExitBtn").IsVisible, Ctl<Button>("MapBtn").IsVisible), (true, true, true));
        // the hostile group's room: sub0A91's sub0BDE -1 (MONSMOD.BAS:726) on Form_Load gives the dead one
        // their Combat Setting, Fight (CHARCREA.BAS:243), shown in the Party window's Option column
        // (observed in the original 2026-10-26: "Fight" for a dead character loaded where they fell)
        var rows = Ctl<StackPanel>("PartyRows");
        Check("... with a hostile group in the room, the Party window's Option is the Combat Setting, Fight",
              rows.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => t is "None" or "Fight" or "Defend").FirstOrDefault(), "Fight");
    }
    catch (Exception ex) { Check("loaded dead below ground: could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { down?.Close(); Pump(); town?.Hide(); Pump(); } catch { }
        Environment.SetEnvironmentVariable("MORDOR_SAVES", savesWas);
        try { Directory.Delete(scratch, true); } catch { }
    }
}

// ---- the DPS Meter mod's window (src/Mods/Mordor.Mod.DpsMeter): a bar per party member, split by
// kind in proportion, the longest the top dealer's; the total over it; a click shows the breakdown;
// it follows the tallies; the Damage | XP modes, kept; Reset at once ----
Console.WriteLine("\nthe DPS Meter window:");
{
    Window? dpsWin = null;
    try
    {
        Character Member(string name)
        {
            var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
            c.ClearPack();
            return c;
        }
        var anna = Member("Anna"); var bert = Member("Bert"); var cara = Member("Cara");
        var party = new Party(anna, bert, cara);
        var modGame = new Mordor.Modding.ModGame();
        var iflags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(Mordor.Modding.ModGame).GetMethod("Set", iflags)!.Invoke(modGame, new object?[] { party, Array.Empty<Spell>(), (Func<short, Item?>)(_ => null) });
        var tally = new Mordor.Mod.DpsMeter.DpsTally(null);
        tally.Add("Anna", new Mordor.Mod.DpsMeter.DpsRow(600, 300, 100, 4));
        tally.Add("Bert", new Mordor.Mod.DpsMeter.DpsRow(0, 250, 0, 1));
        var reported = new List<string>();
        string? keptMode = null;
        var view = (Control)Mordor.Mod.DpsMeter.DpsView.Build(modGame, tally, (what, ex) => reported.Add(what + ": " + ex.Message), () => keptMode, m => keptMode = m);
        dpsWin = new Window { Width = 640, Height = 360, Content = view };
        dpsWin.Show();
        Pump();

        Control? Tagged(Control under, string tag) => under.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => Equals(c.Tag, tag));
        Control Row(string name) => Tagged(view, "Bar:" + name) ?? throw new InvalidOperationException("no bar for " + name);
        double Width(string name, string seg) => Tagged(Row(name), seg)!.Bounds.Width;
        // the bar's full width: the segments' grid, the longest dealer's total
        double Full(string name) => Tagged(Row(name), "Physical")!.GetVisualParent<Control>()!.Bounds.Width;
        string TotalText(string name) => ((TextBlock)Tagged(Row(name), "Total")!).Text ?? "";
        bool Near(double got, double want) => Math.Abs(got - want) <= 1.0;
        // the items are joined inside by no-break spaces (so the line wraps only between items); read as plain spaces
        string RawBreakdown() => ((TextBlock)Tagged(view, "Breakdown")!).Text ?? "";
        string Breakdown() => RawBreakdown().Replace(' ', ' ');

        double full = Full("Anna");
        Check("DPS Meter: a bar for each member, in party order",
              string.Join(",", view.GetVisualDescendants().OfType<Control>().Select(c => c.Tag as string).Where(t => t != null && t.StartsWith("Bar:"))),
              "Bar:Anna,Bar:Bert,Bar:Cara");
        Check("DPS Meter: the top dealer's segments fill the bar, 60/30/10",
              (Near(Width("Anna", "Physical"), full * 0.6), Near(Width("Anna", "Magical"), full * 0.3), Near(Width("Anna", "Companion"), full * 0.1)), (true, true, true));
        Check("DPS Meter: the others in proportion to it (Bert's 250 of 1,000: a quarter, all Magical)",
              (Near(Width("Bert", "Physical"), 0), Near(Width("Bert", "Magical"), full * 0.25), Near(Width("Bert", "Companion"), 0)), (true, true, true));
        Check("DPS Meter: nothing dealt, no bar", Near(Width("Cara", "Physical") + Width("Cara", "Magical") + Width("Cara", "Companion"), 0), true);
        Check("DPS Meter: the segments in the order Physical, Magical, Companion",
              Tagged(Row("Anna"), "Physical")!.Bounds.X < Tagged(Row("Anna"), "Magical")!.Bounds.X
              && Tagged(Row("Anna"), "Magical")!.Bounds.X < Tagged(Row("Anna"), "Companion")!.Bounds.X, true);
        var overlay = (TextBlock)Tagged(Row("Anna"), "Total")!;
        Check("DPS Meter: the total over the bar, thousands separated", (TotalText("Anna"), TotalText("Bert"), TotalText("Cara")), ("1,000", "250", "0"));
        static (byte, byte, byte) Of(Avalonia.Media.IBrush? b) =>
            b is Avalonia.Media.ISolidColorBrush s ? (s.Color.R, s.Color.G, s.Color.B) : ((byte)0, (byte)0, (byte)0);
        static byte AlphaOf(Avalonia.Media.IBrush? b) => b is Avalonia.Media.ISolidColorBrush s ? s.Color.A : (byte)0;
        Check("DPS Meter: the total in plain text, the Party window's white, over the segments",
              (Of(overlay.Foreground) == BarColours.TextOver, overlay.FontWeight == Avalonia.Media.FontWeight.Normal,
               overlay.IsVisible && overlay.Bounds.Width > 0), (true, true, true));
        Check("DPS Meter: no shadow or other effect on anything in the window",
              view.GetVisualDescendants().OfType<Visual>().Count(v => v.Effect != null), 0);
        Check("DPS Meter: no colour legend and no note line",
              view.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text is "Physical" or "Magical" or "Companion"
                                                                         || (t.Text ?? "").StartsWith("Damage dealt", StringComparison.Ordinal)), 0);
        var seg = Tagged(Row("Anna"), "Physical")!;
        Check("DPS Meter: the segments in the port's bar colours (BarColours: Next green, SP blue, Next purple), at half over the track",
              (Of(((Border)seg).Background), Of(((Border)Tagged(Row("Anna"), "Magical")!).Background), Of(((Border)Tagged(Row("Anna"), "Companion")!).Background),
               AlphaOf(((Border)seg).Background)),
              (BarColours.ToNext, BarColours.SpFull, BarColours.ToPin, BarColours.UnderTextAlpha));
        Check("DPS Meter: the three kinds' colours are the shared ones",
              (Mordor.Mod.DpsMeter.DpsView.Physical, Mordor.Mod.DpsMeter.DpsView.Magical, Mordor.Mod.DpsMeter.DpsView.Companion),
              (BarColours.ToNext, BarColours.SpFull, BarColours.ToPin));
        string KillsText(string name) => ((TextBlock)Tagged(Row(name), "Kills")!).Text ?? "";
        Check("DPS Meter: a Kills column on every row", (KillsText("Anna"), KillsText("Bert"), KillsText("Cara")), ("4", "1", "0"));
        var killsHeader = (TextBlock)Tagged(view, "KillsHeader")!;
        double KillsRight(string name) { var k = Tagged(Row(name), "Kills")!; return k.TranslatePoint(new Point(k.Bounds.Width, 0), view)!.Value.X; }
        double BarRight(string name) { var b = Tagged(Row(name), "Physical")!.GetVisualParent<Control>()!; return b.TranslatePoint(new Point(b.Bounds.Width, 0), view)!.Value.X; }
        Check("DPS Meter: headed Kills, right of the bars, right-aligned under its heading",
              (killsHeader.Text, BarRight("Anna") < KillsRight("Anna") - 4,
               Near(KillsRight("Anna"), killsHeader.TranslatePoint(new Point(killsHeader.Bounds.Width, 0), view)!.Value.X),
               Near(KillsRight("Anna"), KillsRight("Cara"))),
              ("Kills", true, true, true));
        double line = Math.Ceiling(new Avalonia.Media.FormattedText("Xgjy", System.Globalization.CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight,
                          new Avalonia.Media.Typeface(view.GetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty)),
                          view.GetValue(Avalonia.Controls.Documents.TextElement.FontSizeProperty), null).Height - 0.01);
        Check("DPS Meter: each row the font's line and three pixels, as the port's list rows",
              (Near(Row("Anna").Bounds.Height, line + 3), Near(Row("Cara").Bounds.Height, line + 3)), (true, true));
        Check("DPS Meter: no breakdown until a bar is clicked", Breakdown(), Mordor.Mod.DpsMeter.DpsView.NoneSelected);
        Check("DPS Meter: the hint says a bar gives more", Mordor.Mod.DpsMeter.DpsView.NoneSelected, "Click a bar for more information.");
        string killsHeaderNow() => ((TextBlock)Tagged(view, "KillsHeader")!).Text ?? "";

        Point At(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), dpsWin)!.Value;
        void ClickOn(Control c) { var p = At(c); dpsWin.MouseDown(p, MouseButton.Left); dpsWin.MouseUp(p, MouseButton.Left); Pump(); }
        ClickOn(Row("Bert"));
        Check("DPS Meter: a click on a bar shows its shares, no name, total or kills", Breakdown(),
              "Physical 0 (0%)   Magical 250 (100%)   Companion 0 (0%)");
        TextBlock NameOf(string name) => (TextBlock)Tagged(Row(name), "Name")!;
        Check("DPS Meter: and marks the row as the Party window marks its selected member (green, bold)",
              (NameOf("Bert").Foreground == Avalonia.Media.Brushes.LightGreen, NameOf("Bert").FontWeight == Avalonia.Media.FontWeight.Bold,
               NameOf("Anna").Foreground == Avalonia.Media.Brushes.WhiteSmoke, NameOf("Anna").FontWeight == Avalonia.Media.FontWeight.Normal),
              (true, true, true, true));
        ClickOn(Row("Anna"));
        Check("DPS Meter: another bar, its shares", Breakdown(),
              "Physical 600 (60%)   Magical 300 (30%)   Companion 100 (10%)");
        Check("DPS Meter: the breakdown wraps only between items, never inside one",
              RawBreakdown().Contains("Companion 100 (10%)") && RawBreakdown().Contains("(30%)   Companion"), true);

        // live: a blow lands
        tally.Add(new CombatDamage(anna, 1000, DamageKind.Physical, 1, true));
        Pump();
        full = Full("Anna");
        Check("DPS Meter: follows the tallies at once (the total, the kills and the shares)", (TotalText("Anna"), KillsText("Anna"), Breakdown().Contains("Physical 1,600 (80%)")), ("2,000", "5", true));
        Check("DPS Meter: and the bars rescale (Bert's 250 of 2,000)", Near(Width("Bert", "Magical"), full * 0.125), true);

        // the XP mode: experience from damage and from chests, the mode kept
        tally.Add(new CombatExperience(anna, 300, ExperienceSource.Damage));
        tally.Add(new CombatExperience(anna, 100, ExperienceSource.Chest));
        tally.Add(new CombatExperience(bert, 200, ExperienceSource.Damage));
        Pump();
        var damageTab = (Avalonia.Controls.Primitives.ToggleButton)Tagged(view, "ModeDamage")!;
        var xpTab = (Avalonia.Controls.Primitives.ToggleButton)Tagged(view, "ModeXp")!;
        Check("DPS Meter: two mode buttons, Damage shown first", (damageTab.Content, xpTab.Content, damageTab.IsChecked, xpTab.IsChecked, damageTab.Focusable || xpTab.Focusable),
              ((object?)"Damage", (object?)"XP", (bool?)true, (bool?)false, false));
        ClickOn(xpTab);
        Check("DPS Meter: XP chosen, and kept in the mod's settings", (keptMode, xpTab.IsChecked, damageTab.IsChecked), ((string?)"XP", (bool?)true, (bool?)false));
        full = Tagged(Row("Anna"), "XpDamage")!.GetVisualParent<Control>()!.Bounds.Width;
        Check("DPS Meter: XP bars split Damage | Chests, the top figure the full width (Anna 300 + 100 of 400, Bert 200)",
              (Near(Width("Anna", "XpDamage"), full * 0.75), Near(Width("Anna", "XpChests"), full * 0.25), Near(Width("Bert", "XpDamage"), full * 0.5), Near(Width("Bert", "XpChests"), 0)),
              (true, true, true, true));
        Check("DPS Meter: the XP total over the bar, no kills column", (TotalText("Anna"), TotalText("Bert"), KillsText("Anna"), killsHeaderNow()), ("400", "200", "", ""));
        Check("DPS Meter: the XP segments in two of the port's bar colours (Next green, the hits bar's yellow)",
              (Of(((Border)Tagged(Row("Anna"), "XpDamage")!).Background), Of(((Border)Tagged(Row("Anna"), "XpChests")!).Background)), (BarColours.ToNext, BarColours.HitsHalf));
        Check("DPS Meter: the selected row's experience shares", Breakdown(), "Damage 300 (75%)   Chests 100 (25%)");
        Check("DPS Meter: a damage segment is not drawn in XP mode", Tagged(Row("Anna"), "Physical"), null);
        var again = (Control)Mordor.Mod.DpsMeter.DpsView.Build(modGame, tally, (what, ex) => reported.Add(what + ": " + ex.Message), () => keptMode, m => keptMode = m);
        dpsWin.Content = again;
        Pump();
        Check("DPS Meter: opened again, it is in the mode kept", ((Avalonia.Controls.Primitives.ToggleButton)Tagged(again, "ModeXp")!).IsChecked, (bool?)true);
        dpsWin.Content = view;
        Pump();
        ClickOn((Control)Tagged(view, "ModeDamage")!);
        Check("DPS Meter: back to Damage", (keptMode, TotalText("Anna")), ((string?)"Damage", "2,000"));

        // Reset: at once, damage and experience alike
        var reset = (Button)Tagged(view, "Reset")!;
        ClickOn(reset);
        Check("DPS Meter: Reset clears every tally at once, no question", (TotalText("Anna"), TotalText("Bert"), KillsText("Anna"), tally.Characters.Count(), reset.IsVisible), ("0", "0", "0", 0, true));
        Check("DPS Meter: the experience too", (tally.For("Anna").XpTotal, tally.For("Bert").XpTotal), (0L, 0L));
        Check("DPS Meter: no Yes or No left", (Tagged(view, "ResetYes"), Tagged(view, "ResetNo")), ((Control?)null, (Control?)null));
        Check("DPS Meter: nothing in the window took the keyboard", view.GetVisualDescendants().OfType<Button>().All(b => !b.Focusable), true);
        Check("DPS Meter: no handler failed", string.Join("; ", reported), "");
    }
    catch (Exception ex) { Check("DPS Meter: the window could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally { try { dpsWin?.Close(); Pump(); } catch { } }
}

// ---- the first-run window (GameFolderWindow, docs/GAME_FILES.md): with no Mordor files to play
// it comes up instead of the menu, a folder with files missing keeps it up and says why, the
// shareware is taken (as the limited build) and remembered and hands over; with a Mordor found the
// menu comes straight up, the limited build on for the shareware's data and off for the full game ----
Console.WriteLine("\nthe first-run window:");
{
    string scratchRoot = Path.Combine(Path.GetTempPath(), "mordor-firstrun-" + Guid.NewGuid().ToString("N")[..8]);
    var envKeep = new[] { GameFolder.EnvIni, GameFolder.EnvDir, "MORDOR_CRASHLOG" }
        .ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
    Window? gateWin = null, menu = null;
    bool limitedWas = LimitedVersion.On;
    try
    {
        Directory.CreateDirectory(scratchRoot);
        string ini = Path.Combine(scratchRoot, "launch", "settings.ini");
        Environment.SetEnvironmentVariable(GameFolder.EnvIni, ini);
        Environment.SetEnvironmentVariable("MORDOR_CRASHLOG", Path.Combine(scratchRoot, "crash.log"));
        string notMordor = Directory.CreateDirectory(Path.Combine(scratchRoot, "not-mordor")).FullName;
        // the shareware's dungeon: the full files with MDATA11's level count cut to three
        string share = Directory.CreateDirectory(Path.Combine(scratchRoot, "shareware")).FullName;
        foreach (string f in GameFiles.Required) File.Copy(Path.Combine(game, f), Path.Combine(share, f));
        using (var fs = new FileStream(Path.Combine(share, "MDATA11.MDR"), FileMode.Open, FileAccess.Write))
            fs.Write(BitConverter.GetBytes((ushort)GameFiles.SharewareLevels));
        // a copy without its dungeon: a file the port needs is missing
        string partial = Directory.CreateDirectory(Path.Combine(scratchRoot, "partial")).FullName;
        foreach (string f in GameFiles.Required.Where(f => f != "MDATA11.MDR")) File.Copy(Path.Combine(game, f), Path.Combine(partial, f));
        LimitedVersion.On = false;

        Environment.SetEnvironmentVariable(GameFolder.EnvDir, notMordor);
        int started = 0; string? startedWith = null; Window? mainNow = null;
        Window Start(string d) { started++; startedWith = d; return menu = new Window { Width = 200, Height = 100, Title = "the menu" }; }
        var first = GameFolderWindow.FirstWindow(GameFolder.Resolve(), Start, w => mainNow = w);
        gateWin = first;
        Check("no Mordor files: the first-run window comes up instead of the menu", (first is GameFolderWindow, started), (true, 0));
        var gate = (GameFolderWindow)first;
        gate.Show(); Pump();
        Check("it says what is wrong with the folder named", gate.Problem.Text?.Contains(notMordor) == true && gate.Problem.Text.Contains("none of Mordor's files"), true);
        string shown = string.Join("\n", gate.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Check("it no longer turns the shareware away", (shown.Contains("cannot be used"), shown.Contains("Not the full version")), (false, false));
        void PressLocate()
        {
            var p = gate.LocateButton.TranslatePoint(new Point(gate.LocateButton.Bounds.Width / 2, gate.LocateButton.Bounds.Height / 2), gate)!.Value;
            gate.MouseDown(p, MouseButton.Left); gate.MouseUp(p, MouseButton.Left); Pump();
        }
        gate.PickFolder = _ => Task.FromResult<string?>(null);
        PressLocate();
        Check("Locate, then Cancel: still up, nothing started", (gate.IsVisible, started), (true, 0));
        gate.PickFolder = _ => Task.FromResult<string?>(partial);
        PressLocate();
        Check("a folder without MDATA11 picked: still up, nothing started, nothing kept", (gate.IsVisible, started, File.Exists(ini)), (true, 0, false));
        Check("and it says which file is missing", gate.Problem.Text?.Contains("MDATA11.MDR") == true && gate.Problem.Text.Contains(partial), true);
        gate.PickFolder = _ => Task.FromResult<string?>(share);
        PressLocate();
        Check("the shareware picked: the menu is made, with that folder", (started, startedWith), (1, share));
        Check("and is shown as the main window, the first-run window gone", (mainNow == menu, menu?.IsVisible == true, gate.IsVisible), (true, true, false));
        Check("played as the limited build", LimitedVersion.On, true);
        Check("the choice is kept in the launch settings", GameFolder.Configured(ini), share);
        menu?.Close(); Pump();

        // the next start: the folder kept is the one used, and the menu comes straight up
        Environment.SetEnvironmentVariable(GameFolder.EnvDir, null);
        LimitedVersion.On = false;
        var next = GameFolderWindow.FirstWindow(GameFolder.Resolve(), Start);
        Check("next time, with the folder kept: the menu at once, no first-run window", (next is GameFolderWindow, started, next == menu), (false, 2, true));
        Check("and the limited build again", LimitedVersion.On, true);
        menu?.Close(); Pump();

        // the folder given named: the menu at once, the limited build off for the full game and on
        // for the shareware
        Environment.SetEnvironmentVariable(GameFolder.EnvDir, game);
        var full = GameFolderWindow.FirstWindow(GameFolder.Resolve(), Start);
        if (GameFiles.Check(game).IsFull)
            Check("the full game: the menu at once, as the full build", (full is GameFolderWindow, started, startedWith, LimitedVersion.On), (false, 3, Path.GetFullPath(game), false));
        else
        {
            Console.WriteLine("  skip  the full game: the menu at once, as the full build: full game only (this folder is the shareware)");
            Check("the shareware given: the menu at once, as the limited build", (full is GameFolderWindow, started, startedWith, LimitedVersion.On), (false, 3, Path.GetFullPath(game), true));
        }
        menu?.Close(); Pump();
    }
    catch (Exception ex) { Check("first-run window: could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { gateWin?.Close(); menu?.Close(); Pump(); } catch { }
        foreach (var (k, v) in envKeep) Environment.SetEnvironmentVariable(k, v);
        LimitedVersion.On = limitedWas;
        try { Directory.Delete(scratchRoot, true); } catch { }
    }
}

// ---- the first-run window's installer (GameFolderWindow, InstallerUnpack; docs/GAME_FILES.md): an
// installer in Downloads is offered, not unpacked; Unpack it with a file that is no installer says
// so and starts nothing; Use the installer... with MordorSetup.exe (MORDOR_INSTALLER, else beside the
// game folder) unpacks it into the unpack folder - a scratch one here - off the window's thread, then
// goes on to the menu with that folder, remembered ----
Console.WriteLine("\nthe first-run window, given an installer:");
{
    string scratchRoot = Path.Combine(Path.GetTempPath(), "mordor-firstrun-unpack-" + Guid.NewGuid().ToString("N")[..8]);
    var envKeep = new[] { GameFolder.EnvIni, GameFolder.EnvDir, InstallerUnpack.EnvRoot, "MORDOR_CRASHLOG" }
        .ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
    Window? gateWin = null, menu = null;
    bool limitedWas = LimitedVersion.On;
    try
    {
        Directory.CreateDirectory(scratchRoot);
        string ini = Path.Combine(scratchRoot, "launch", "settings.ini");
        string unpackRoot = Path.Combine(scratchRoot, "game");
        Environment.SetEnvironmentVariable(GameFolder.EnvIni, ini);
        Environment.SetEnvironmentVariable(InstallerUnpack.EnvRoot, unpackRoot);
        Environment.SetEnvironmentVariable("MORDOR_CRASHLOG", Path.Combine(scratchRoot, "crash.log"));
        string notMordor = Directory.CreateDirectory(Path.Combine(scratchRoot, "not-mordor")).FullName;
        Environment.SetEnvironmentVariable(GameFolder.EnvDir, notMordor);
        // a "MordorDemoSetup.exe" in Downloads that is no installer at all
        string downloads = Directory.CreateDirectory(Path.Combine(scratchRoot, "Downloads")).FullName;
        string bogus = Path.Combine(downloads, "MordorDemoSetup.exe");
        File.WriteAllText(bogus, "not an installer");

        int started = 0; string? startedWith = null;
        Window Start(string d) { started++; startedWith = d; return menu = new Window { Width = 200, Height = 100, Title = "the menu" }; }
        var gate = (GameFolderWindow)GameFolderWindow.FirstWindow(GameFolder.Resolve(), Start, null, InstallerUnpack.InDownloads(downloads));
        gateWin = gate;
        gate.Show(); Pump();
        string shown = string.Join("\n", gate.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Check("the installer in Downloads is offered, by name", (gate.Offers.Count, shown.Contains("Found MordorDemoSetup.exe in Downloads.")), (1, true));
        Check("and not unpacked unasked", (started, Directory.Exists(unpackRoot)), (0, false));
        Check("the window says the installers do not run, and that it unpacks them",
              (shown.Contains("do not run on Windows 10 or 11"), shown.Contains("OpenDejenol unpacks the game from it")), (true, true));
        Check("the buttons: Use the installer..., Locate Mordor..., Exit",
              string.Join(" | ", new[] { gate.InstallerButton, gate.LocateButton, gate.ExitButton }.Select(b => b.Content)), "_Use the installer... | _Locate Mordor... | E_xit");

        void ClickGate(Button b)
        {
            var p = b.TranslatePoint(new Point(b.Bounds.Width / 2, b.Bounds.Height / 2), gate)!.Value;
            gate.MouseDown(p, MouseButton.Left); gate.MouseUp(p, MouseButton.Left); Pump();
        }
        void SettleUnpack()
        {
            for (int i = 0; i < 600 && gate.Unpacking is { IsCompleted: false }; i++) { Thread.Sleep(50); Pump(); }
            Pump();
        }
        ClickGate(gate.Offers[0].Button);
        SettleUnpack();
        Check("Unpack it, on a file that is no installer: it says so, nothing started, nothing kept",
              (gate.Problem.Text?.StartsWith(InstallerUnpack.NotAnInstaller, StringComparison.Ordinal) == true, started, File.Exists(ini), gate.IsVisible), (true, 0, false, true));
        // in plain words, with what to choose; the technical reason goes to the crash log
        string log = File.Exists(Path.Combine(scratchRoot, "crash.log")) ? File.ReadAllText(Path.Combine(scratchRoot, "crash.log")) : "";
        Check("the player is told in plain words what to choose, the technical reason is in the crash log",
              (gate.Problem.Text, log.Contains(InstallerUnpack.NotAnInstaller + " " + InstallerUnpack.NotAnInstallerHint + " (MordorDemoSetup.exe: ")),
              (InstallerUnpack.NotAnInstaller + " " + InstallerUnpack.NotAnInstallerHint, true));
        Check("and the buttons are back", (gate.InstallerButton.IsEnabled, gate.LocateButton.IsEnabled, gate.Offers[0].Button.IsEnabled, gate.Status.IsVisible), (true, true, true, false));

        gate.PickInstaller = _ => Task.FromResult<string?>(null);
        ClickGate(gate.InstallerButton);
        SettleUnpack();
        Check("Use the installer..., then Cancel: still up, nothing started", (gate.IsVisible, started), (true, 0));

        string? setup = Environment.GetEnvironmentVariable("MORDOR_INSTALLER");
        if (string.IsNullOrWhiteSpace(setup)) setup = Path.Combine(game, "..", "MordorSetup.exe");
        if (!File.Exists(setup))
            Console.WriteLine("  skip  Use the installer... with MordorSetup.exe: not found (set MORDOR_INSTALLER, or put it beside the game folder)");
        else
        {
            gate.PickInstaller = _ => Task.FromResult<string?>(Path.GetFullPath(setup));
            ClickGate(gate.InstallerButton);
            bool sawStatus = gate.Status.IsVisible && gate.Status.Text?.StartsWith("Unpacking MordorSetup.exe") == true && !gate.InstallerButton.IsEnabled;
            SettleUnpack();
            string full = Path.Combine(unpackRoot, "full");
            Check("while it unpacks: \"Unpacking MordorSetup.exe ...\", the buttons off", sawStatus, true);
            Check("MordorSetup.exe chosen: unpacked, and the menu made with that folder", (started, startedWith), (1, full));
            Check("the first-run window gone, the folder kept in the launch settings", (gate.IsVisible, GameFolder.Configured(ini)), (false, full));
            Check("played as the full build", LimitedVersion.On, false);
            menu?.Close(); Pump();
        }
    }
    catch (Exception ex) { Check("first-run window with an installer: could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { gateWin?.Close(); menu?.Close(); Pump(); } catch { }
        foreach (var (k, v) in envKeep) Environment.SetEnvironmentVariable(k, v);
        LimitedVersion.On = limitedWas;
        try { Directory.Delete(scratchRoot, true); } catch { }
    }
}

// ---- getting the game ready, saying so (GameFolderWindow.Prepare, WhilePreparing; Art.PrepareAsync):
// a folder picked in the first-run window is got ready - the pictures and help read out of it - with
// "Preparing the game files ..." up and the buttons off until the game's window is shown; a failure
// says why and gives the buttons back; a start that must extract first shows a small window that says
// so, then the game's window. The preparing here is a task the check finishes itself ----
Console.WriteLine("\nthe first-run window, preparing the game:");
{
    string scratchRoot = Path.Combine(Path.GetTempPath(), "mordor-firstrun-prepare-" + Guid.NewGuid().ToString("N")[..8]);
    var envKeep = new[] { GameFolder.EnvIni, GameFolder.EnvDir, "MORDOR_CRASHLOG" }
        .ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
    Window? gateWin = null, menu = null, wait = null;
    bool limitedWas = LimitedVersion.On;
    try
    {
        Directory.CreateDirectory(scratchRoot);
        string ini = Path.Combine(scratchRoot, "launch", "settings.ini");
        Environment.SetEnvironmentVariable(GameFolder.EnvIni, ini);
        Environment.SetEnvironmentVariable("MORDOR_CRASHLOG", Path.Combine(scratchRoot, "crash.log"));
        Environment.SetEnvironmentVariable(GameFolder.EnvDir, Directory.CreateDirectory(Path.Combine(scratchRoot, "not-mordor")).FullName);

        int started = 0; Window? mainNow = null;
        Window Start(string d) { started++; return menu = new Window { Width = 200, Height = 100, Title = "the menu" }; }
        var pending = new TaskCompletionSource<string?>();
        string? preparing = null;
        Task<string?> Prep(string d) { preparing = d; return pending.Task; }
        var gate = (GameFolderWindow)GameFolderWindow.FirstWindow(GameFolder.Resolve(), Start, w => mainNow = w, Array.Empty<string>(), Prep);
        gateWin = gate;
        gate.Show(); Pump();
        void PressLocate()
        {
            var p = gate.LocateButton.TranslatePoint(new Point(gate.LocateButton.Bounds.Width / 2, gate.LocateButton.Bounds.Height / 2), gate)!.Value;
            gate.MouseDown(p, MouseButton.Left); gate.MouseUp(p, MouseButton.Left); Pump();
        }
        string picked = GameFiles.Check(game).Dir;
        gate.PickFolder = _ => Task.FromResult<string?>(game);
        PressLocate();
        Check("a Mordor picked: \"Preparing the game files ...\" up, the buttons off, nothing started yet",
              (gate.Status.IsVisible, gate.Status.Text, gate.LocateButton.IsEnabled, gate.InstallerButton.IsEnabled, started, preparing == picked, gate.IsVisible),
              (true, GameFolderWindow.PreparingText, false, false, 0, true, true));
        pending.SetResult("The game's pictures could not be read from " + picked + ": (the check's)");
        Pump();
        Check("not ready: it says why, the buttons back, nothing started, nothing kept",
              (gate.Problem.Text, gate.Status.IsVisible, gate.LocateButton.IsEnabled, started, File.Exists(ini), gate.IsVisible),
              ("The game's pictures could not be read from " + picked + ": (the check's)", false, true, 0, false, true));
        pending = new TaskCompletionSource<string?>();
        PressLocate();
        Check("picked again: preparing again, the reason cleared", (gate.Status.IsVisible, gate.Problem.Text, gate.LocateButton.IsEnabled), (true, "", false));
        pending.SetResult(null);
        Pump();
        Check("ready: the game's window shown as the main window, the first-run window gone, the folder kept",
              (started, menu?.IsVisible == true, mainNow == menu, gate.IsVisible, GameFolder.Configured(ini)), (1, true, true, false, picked));
        menu?.Close(); Pump();

        // the start that must extract first
        var extracting = new TaskCompletionSource<string?>();
        string? outcome = "(none yet)"; Window? next = null;
        wait = GameFolderWindow.WhilePreparing(picked, _ => extracting.Task, e => { outcome = e; return next = new Window { Width = 200, Height = 100, Title = "the menu" }; }, w => mainNow = w);
        wait.Show(); Pump();
        string said = string.Join("\n", wait.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Check("a start that must extract: a small window says \"Preparing the game files ...\", nothing else yet",
              (wait.IsVisible, said.Contains(GameFolderWindow.PreparingText), next == null), (true, true, true));
        extracting.SetResult(null);
        Pump();
        Check("extracted: the game's window shown as the main window, the small one gone",
              (outcome, next?.IsVisible == true, mainNow == next, wait.IsVisible), ((string?)null, true, true, false));
        next?.Close(); Pump();
        // closed before it is done: nothing more is shown
        var slow = new TaskCompletionSource<string?>();
        next = null;
        wait = GameFolderWindow.WhilePreparing(picked, _ => slow.Task, e => next = new Window { Width = 200, Height = 100 }, null);
        wait.Show(); Pump();
        wait.Close(); Pump();
        slow.SetResult(null); Pump();
        Check("closed while it prepares: nothing is shown after", next, (Window?)null);
    }
    catch (Exception ex) { Check("first-run window preparing the game: could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { gateWin?.Close(); menu?.Close(); wait?.Close(); Pump(); } catch { }
        foreach (var (k, v) in envKeep) Environment.SetEnvironmentVariable(k, v);
        LimitedVersion.On = limitedWas;
        try { Directory.Delete(scratchRoot, true); } catch { }
    }
}

// ---- the menu shut down under its boxes: the limited build's Public Version box is up over the
// menu when the program ends (the menu closed, as a shutdown closes every window). The Welcome box
// awaited after it used to be put over the closed menu - "Cannot show a window with a closed
// owner" from an async void handler, which ended the process. Now nothing more is shown, and the
// first-run flag is not written for a Welcome never seen ----
Console.WriteLine("\nthe menu shut down under its Public Version box:");
{
    string scratchSaves = Path.Combine(Path.GetTempPath(), "mordor-shutdown-" + Guid.NewGuid().ToString("N")[..8]);
    var envKeep = new[] { "MORDOR_SAVES", "MORDOR_MUTE", "MORDOR_CRASHLOG" }.ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
    bool limitedWas = LimitedVersion.On;
    string gameDirWas = TitleWindow.GameDir;
    TitleWindow? title = null;
    var thrown = new List<string>();
    void Caught(object? s, DispatcherUnhandledExceptionEventArgs e) { thrown.Add(e.Exception.GetType().Name + ": " + e.Exception.Message); e.Handled = true; }
    Dispatcher.UIThread.UnhandledException += Caught;
    try
    {
        Directory.CreateDirectory(scratchSaves);
        Environment.SetEnvironmentVariable("MORDOR_SAVES", scratchSaves);
        Environment.SetEnvironmentVariable("MORDOR_MUTE", "1");
        Environment.SetEnvironmentVariable("MORDOR_CRASHLOG", Path.Combine(scratchSaves, "crash.log"));
        TitleWindow.GameDir = game;
        LimitedVersion.On = true;
        title = new TitleWindow();
        title.Show(); Pump();
        // first of all the Limitation Notice (sub03E3, before the data and the Public Version box;
        // observed in the original shareware 2026-10-26), and Limited=True once it is answered
        string BoxText(Window w) => string.Join("|", w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t) && t != "OK"));
        var notice = title.OwnedWindows.ToList();
        Check("the limited build: the Limitation Notice comes first, alone, with its text",
              (notice.Count, notice.FirstOrDefault()?.Title, notice.Count > 0 ? BoxText(notice[0]) : ""),
              (1, "Limitation Notice", "NOTICE!\nBecause this is a PUBLIC BETA version of MORDOR, only 3 dungeon levels are currently available.\n\nThis message will not appear again."));
        Check("not marked seen while it is up", PortSettings.Load(scratchSaves).LimitationNoticeSeen, false);
        foreach (var w in notice) w.Close();
        Pump(); Pump();
        Check("its OK writes Limited=True", (PortSettings.Load(scratchSaves).Limited, PortSettings.Load(scratchSaves).LimitationNoticeSeen), ("True", true));
        var boxes = title.OwnedWindows.ToList();
        Check("the limited build: then the Public Version box is up over the menu", (boxes.Count, boxes.FirstOrDefault()?.Title), (1, LimitedVersion.PublicBoxTitle));
        // the program ends: the menu goes, and the box with it
        title.Close(); Pump(); Pump();
        Check("the menu shut down under it: nothing thrown", string.Join("; ", thrown), "");
        Check("and no Welcome box put up after it", string.Join(", ", title.OwnedWindows.Select(w => w.Title + " " + w.IsVisible)), "");
        Check("nor the Welcome marked as seen", PortSettings.Load(scratchSaves).Welcomed, false);
        Check("a box over a closed window is not shown (OriginalBox.CanOwn)", OriginalBox.CanOwn(title), false);
        var late = OriginalBox.Tell(title, "late", "never shown");
        Pump();
        Check("and is done at once, without an exception", (late.IsCompleted, late.IsFaulted), (true, false));

        // and with nothing shutting down: the next menu (the Public Version box is once a run) puts
        // the Welcome box up, and its OK marks it seen
        title = new TitleWindow();
        title.Show(); Pump();
        var welcome = title.OwnedWindows.ToList();
        Check("a menu left up: the Welcome box comes up over it - the Limitation Notice, seen, not again", string.Join(", ", welcome.Select(w => w.Title)), "Welcome!");
        foreach (var w in welcome) w.Close();
        Pump();
        Check("and its OK marks it seen, nothing thrown", (PortSettings.Load(scratchSaves).Welcomed, string.Join("; ", thrown)), (true, ""));
        title.Close(); Pump();
    }
    catch (Exception ex) { Check("the menu's shutdown could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        Dispatcher.UIThread.UnhandledException -= Caught;
        try { title?.Close(); Pump(); } catch { }
        foreach (var (k, v) in envKeep) Environment.SetEnvironmentVariable(k, v);
        LimitedVersion.On = limitedWas;
        TitleWindow.GameDir = gameDirWas;
        try { Directory.Delete(scratchSaves, true); } catch { }
    }
}

// ---- the menu's close box: the full build refuses it with "Please use the Mordor EXIT buttons to
// leave the game." (FRMMAIN.BAS:171-175); the shareware asks "Exit Mordor?" and a Yes quits with no
// goodbye (observed in the original shareware 2026-10-26) ----
Console.WriteLine("\nthe menu's close box:");
{
    string scratchSaves = Path.Combine(Path.GetTempPath(), "mordor-closebox-" + Guid.NewGuid().ToString("N")[..8]);
    var envKeep = new[] { "MORDOR_SAVES", "MORDOR_MUTE", "MORDOR_CRASHLOG" }.ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
    bool limitedWas = LimitedVersion.On;
    string gameDirWas = TitleWindow.GameDir;
    TitleWindow? title = null;
    try
    {
        Directory.CreateDirectory(scratchSaves);
        Environment.SetEnvironmentVariable("MORDOR_SAVES", scratchSaves);
        Environment.SetEnvironmentVariable("MORDOR_MUTE", "1");
        Environment.SetEnvironmentVariable("MORDOR_CRASHLOG", Path.Combine(scratchSaves, "crash.log"));
        TitleWindow.GameDir = game;
        // the first-run boxes already seen, so that the menu stands alone
        PortSettings.Write(scratchSaves, "NotFirstTime", "True");
        PortSettings.Write(scratchSaves, "Limited", "True");
        // the user's close box: the platform's own request, which arrives as a close that is not the program's
        var windowImpl = Type.GetType("Avalonia.Platform.IWindowImpl, Avalonia.Controls")!;
        void CloseBox(Window w)
        {
            var impl = typeof(TopLevel).GetProperty("PlatformImpl")!.GetValue(w)!;
            var closing = (Func<WindowCloseReason, bool>?)windowImpl.GetProperty("Closing")!.GetValue(impl);
            closing?.Invoke(WindowCloseReason.WindowClosing);
            Pump(); Pump();
        }
        string BoxText(Window w) => string.Join("|", w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)));
        void Answer(Window box, string button)
        {
            box.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, button)).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Pump(); Pump();
        }

        LimitedVersion.On = true;
        title = new TitleWindow();
        title.Show(); Pump(); Pump();
        foreach (var w in title.OwnedWindows.ToList()) w.Close();   // a Public Version box, if this run has not had it yet
        Pump();
        // the shareware's About button, "Ordering/&About" (FRMMENU.BAS:162; observed in the original
        // shareware 2026-10-26), opening frmAbout with its Ordering Info
        var aboutBtn = title.FindControl<Button>("AboutBtn")!;
        Check("the shareware: the menu's About button reads Ordering/&About", aboutBtn.Content, "Ordering/_About");
        aboutBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump(); Pump();
        var about = title.OwnedWindows.ToList();
        Check("... and opens About MORDOR, with Ordering Info",
              (about.Count, about.FirstOrDefault()?.Title, about.Count > 0 && about[0].GetVisualDescendants().OfType<Button>().Any(b => Equals(b.Content, "Ordering Info"))),
              (1, "About MORDOR", true));
        foreach (var w in about) w.Close();
        Pump(); Pump();
        CloseBox(title);
        var ask = title.OwnedWindows.ToList();
        Check("the shareware: the close box asks \"Exit Mordor?\", captioned EXIT, Yes and No",
              (ask.Count, ask.FirstOrDefault()?.Title, ask.Count > 0 ? BoxText(ask[0]) : ""), (1, "EXIT", "Exit Mordor?|Yes|No"));
        if (ask.Count > 0) Answer(ask[0], "No");
        Check("... No keeps the menu", (title.IsVisible, title.OwnedWindows.Count), (true, 0));
        Check("the shareware: the menu is captioned as its frame, Mordor 1.1 (Limited Version) (observed 2026-10-26)", title.Title, "Mordor 1.1 (Limited Version)");
        CloseBox(title);
        ask = title.OwnedWindows.ToList();
        if (ask.Count > 0) Answer(ask[0], "Yes");
        Check("... Yes quits, with no goodbye box", (title.IsVisible, title.OwnedWindows.Count(w => w.IsVisible)), (false, 0));

        LimitedVersion.On = false;
        title = new TitleWindow();
        title.Show(); Pump(); Pump();
        Check("the full game: the About button reads About...", title.FindControl<Button>("AboutBtn")!.Content, "_About...");
        CloseBox(title);
        var refused = title.OwnedWindows.ToList();
        Check("the full game: the close box is refused, \"Please use the Mordor EXIT buttons to leave the game.\"",
              (refused.Count, refused.FirstOrDefault()?.Title, refused.Count > 0 ? BoxText(refused[0]) : ""),
              (1, "Exit Mordor", "Please use the Mordor EXIT buttons to leave the game.|OK"));
        if (refused.Count > 0) Answer(refused[0], "OK");
        Check("... and the menu stays", title.IsVisible, true);
        Check("the full game: the menu is captioned as its frame, Mordor 1.1 (FRMMAIN.BAS:148-152)", title.Title, "Mordor 1.1");
        title.Close(); Pump();
    }
    catch (Exception ex) { Check("the menu's close box could be driven", ex.GetType().Name + ": " + ex.Message, ""); }
    finally
    {
        try { title?.Close(); Pump(); } catch { }
        foreach (var (k, v) in envKeep) Environment.SetEnvironmentVariable(k, v);
        LimitedVersion.On = limitedWas;
        TitleWindow.GameDir = gameDirWas;
        try { Directory.Delete(scratchSaves, true); } catch { }
    }
}

// ---- the game's art: read out of the game folder into the cache, and drawn from there ----
// The port carries no pictures of the game's (docs/GAME_FILES.md); what a window shows is what
// Art extracted from the player's copy at start.
Console.WriteLine("\nthe game's art, read out of the game folder and shown from the cache (Art)");
{
    Window? artWin = null;
    try
    {
        Check("the art is in this run's cache", Art.Dir != null && Art.Dir.StartsWith(artRoot, StringComparison.OrdinalIgnoreCase), true);
        Check("named by the game files' hash", Path.GetFileName(Art.Dir ?? ""), Mordor.Core.Extraction.GameArt.Key(game));
        Check("nothing missing from this copy", string.Join("; ", Art.Notes), "");

        // a picture as it is on disk in the cache, decoded by the extractor's own reader
        Mordor.Core.Extraction.RgbaImage OnDisk(string name) =>
            Mordor.Core.Extraction.Png.Decode(File.ReadAllBytes(Path.Combine(Art.Dir!, name)));
        uint[] Pixels(Avalonia.Media.Imaging.Bitmap b)
        {
            var px = new uint[b.PixelSize.Width * b.PixelSize.Height];
            var buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(px.Length * 4);
            try
            {
                b.CopyPixels(new PixelRect(b.PixelSize), buf, px.Length * 4, b.PixelSize.Width * 4);
                var ints = new int[px.Length];
                System.Runtime.InteropServices.Marshal.Copy(buf, ints, 0, ints.Length);
                for (int i = 0; i < px.Length; i++) px[i] = (uint)ints[i];
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buf); }
            return px;
        }

        // the toolbar: nine buttons, each its picture out of the cache
        var bar = new Toolbar();
        artWin = new Window { Width = 400, Height = 60, Content = bar };
        artWin.Show(); Pump();
        var icons = bar.GetVisualDescendants().OfType<Image>().Where(i => i.Source is Avalonia.Media.Imaging.Bitmap).ToList();
        Check("toolbar pictures shown", icons.Count, 9);
        Check("each the cache's own picture", icons.Count(i => i.Source is Avalonia.Media.Imaging.Bitmap b && b.PixelSize == new PixelSize(20, 20)), 9);
        Check("the page button is tool_page.png from the cache", ReferenceEquals(icons[0].Source, Art.Bitmap("tool_page.png")), true);
        var disk = OnDisk("tool_page.png");
        Check("and its pixels are the extracted file's", Pixels(Art.Bitmap("tool_page.png")).SequenceEqual(disk.Pixels), true);

        // the City's buttons, as CityWindow.axaml names them ({app:Art town_store.png})
        var store = (Avalonia.Media.Imaging.Bitmap)new ArtExtension("town_store.png").ProvideValue(null!);
        Check("{app:Art town_store.png} is the cache's picture", (ReferenceEquals(store, Art.Bitmap("town_store.png")), store.PixelSize.Width, store.PixelSize.Height), (true, 103, 75));

        // drawn: a window holding the Store's picture shows its pixels on screen
        var shown = new Image { Source = store, Stretch = Avalonia.Media.Stretch.None, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        artWin.Content = shown;
        artWin.Height = 200;
        Pump();
        var shot = artWin.CaptureRenderedFrame();
        if (shot == null) Check("a frame of the window could be captured", false, true);
        else
        {
            var got = Pixels(shot);
            // the frame is RGBA in memory where a bitmap from a file is BGRA: as 0xAARRGGBB
            if (shot.Format == Avalonia.Platform.PixelFormat.Rgba8888)
                for (int i = 0; i < got.Length; i++) got[i] = (got[i] & 0xFF00FF00u) | ((got[i] & 0xFFu) << 16) | ((got[i] >> 16) & 0xFFu);
            var want = OnDisk("town_store.png");
            int fw = shot.PixelSize.Width, same = 0, tested = 0;
            for (int y = 5; y < 75 && y < shot.PixelSize.Height; y += 7)
                for (int x = 5; x < 103 && x < fw; x += 9)
                {
                    tested++;
                    if ((got[y * fw + x] & 0xFFFFFF) == (want.Pixels[y * 103 + x] & 0xFFFFFF)) same++;
                }
            Check("the window draws the Store's picture, pixel for pixel where sampled", $"{same}/{tested}", $"{tested}/{tested}");
        }

        // the help, out of MORDOR.HLP
        var help = new HelpWindow();
        help.Show(); Pump();
        var topicsShown = help.FindControl<ListBox>("TopicList")?.ItemCount ?? 0;
        Check("the help window lists the help file's topics", topicsShown, 73);
        // opened with no topic named, it is at the help file's Contents topic, as WinHelp was for
        // HELP_CONTENTS: Welcome, in the full game's MORDOR.HLP and the shareware's alike
        Check("and opens at its Contents topic", (help.FindControl<TextBlock>("TopicTitle")?.Text, help.FindControl<ListBox>("TopicList")?.SelectedItem as string), ("Welcome", "Welcome"));
        // captioned, as WinHelp captioned its window, with the help file's own TITLE (|SYSTEM)
        string hlpTitle = Mordor.Core.Extraction.WinHelpFile.Read(Mordor.Core.Extraction.GameArt.Find(game, "MORDOR.HLP")!).Title;
        Check("the help window is captioned with the help file's own title", (help.Title, help.FindControl<TextBlock>("HelpHeading")?.Text), (hlpTitle, hlpTitle));
        help.Close(); Pump();
        var named = new HelpWindow("Tutorial");
        named.Show(); Pump();
        Check("a topic asked for by name is opened instead", named.FindControl<TextBlock>("TopicTitle")?.Text, "Tutorial");
        named.Close(); Pump();
    }
    catch (Exception ex) { Check("the art could be shown", ex.GetType().Name + ": " + ex.Message, ""); }
    finally { try { artWin?.Close(); Pump(); } catch { } }
}
// ---- the Import Original Saves preview (ImportWindow): an MDATA4 of one character, made here
Console.WriteLine("\nthe Import Original Saves preview (ImportWindow, docs/GAME_FILES.md)");
{
    string root = Path.Combine(Path.GetTempPath(), "mordor-uicheck-import-" + Guid.NewGuid().ToString("N")[..8]);
    try
    {
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        string data = Path.Combine(root, "DATA"), saves = Path.Combine(root, "saves");
        Directory.CreateDirectory(data);
        void P16(byte[] b, int at, int v) => System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)v);
        var head = new byte[2900];
        P16(head, 0, 3); System.Text.Encoding.ASCII.GetBytes("1.1").CopyTo(head, 2);
        var count = new byte[2900];
        P16(count, 0, 1);
        var rec = new byte[2887];
        System.Text.Encoding.ASCII.GetBytes("Ui".PadRight(30)).CopyTo(rec, 0);
        BitConverter.GetBytes(16f * 365).CopyTo(rec, 36);
        P16(rec, 44, 9); P16(rec, 46, 11); P16(rec, 1642, 1); P16(rec, 1662, 15); P16(rec, 1664, 15);
        for (int i = 0; i < 6; i++) P16(rec, 56 + 2 * i, 10);
        P16(rec, 1678, 1); P16(rec, 2417, -1);
        for (int s = 0; s < 5; s++) P16(rec, 2062 + s * 33 + 15, -1);
        File.WriteAllBytes(Path.Combine(data, "MDATA4.MDR"), head.Concat(count).Concat(rec).ToArray());
        var items = MdrLoader.LoadItems(Path.Combine(game, "MDATA3.MDR"));
        var monsters = MdrLoader.LoadMonsters(Path.Combine(game, "MDATA5.MDR"));
        var src = Mordor.Core.Import.OriginalSaves.Read(data, items, monsters);
        var form = ImportWindow.BuildPreview(Avalonia.Media.FontFamily.Default, src, Mordor.Core.Import.SaveImport.Plan(src, saves), data, saves);
        form.Window.Show(); Pump();
        Check("the preview lists the character, ticked: the saves folder has none of that name",
              string.Join(",", form.Characters.Select(c => $"{c.Name} {c.Box.IsChecked}")), "Ui True");
        Check("no world box: the folder holds no world file", form.World == null, true);
        Check("laid out with something in it", form.Window.Bounds.Height > 100, true);
        form.Import.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
        Check("Import closes it, with the ticked name", form.Result is { } r ? string.Join(",", r.Names) + " " + r.World : "cancelled", "Ui False");
        Mordor.Core.Import.SaveImport.Apply(src, saves, new[] { "Ui" }, false, DateTime.Now);
        var again = ImportWindow.BuildPreview(Avalonia.Media.FontFamily.Default, src, Mordor.Core.Import.SaveImport.Plan(src, saves), data, saves);
        again.Window.Show(); Pump();
        Check("once imported, the name is taken: unticked to begin with", again.Characters.Single().Box.IsChecked, false);
        again.Cancel.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
        Check("Cancel: nothing to import", again.Result == null, true);
    }
    catch (Exception ex) { Check("the import preview could be shown", ex.GetType().Name + ": " + ex.Message, ""); }
    finally { try { Directory.Delete(root, true); } catch { } }
}

// ---- the program's icon on every window (App.axaml's Window style, src/Mordor.App/Assets/Icon):
// a window class of the game's, and a plain Window as a mod's or a dialog's is, in place of
// Avalonia's logo (no Icon at all) ----
Console.WriteLine("\nthe program's icon on the windows:");
{
    Window? plain = null, helpWin = null;
    try
    {
        plain = new Window { Width = 200, Height = 100 };
        plain.Show(); Pump();
        Check("a plain Window (a mod's, a dialog's) carries the program's icon", plain.Icon != null, true);
        helpWin = new HelpWindow();
        helpWin.Show(); Pump();
        Check("a window class of the game's (HelpWindow) carries it too", helpWin.Icon != null, true);
        Check("the same icon on both", ReferenceEquals(plain.Icon, helpWin.Icon), true);
    }
    catch (Exception ex) { Check("the windows could be shown", ex.GetType().Name + ": " + ex.Message, ""); }
    finally { try { plain?.Close(); helpWin?.Close(); Pump(); } catch { } }
}

Environment.SetEnvironmentVariable("MORDOR_ART_CACHE", artWas);
try { Directory.Delete(artRoot, true); } catch { }

Console.WriteLine(failed == 0 ? "\nall the window checks passed" : $"\n{failed} window check(s) FAILED");
return failed == 0 ? 0 : 1;

/// <summary>A drag source that only notes what a drag would carry; the headless platform has none.</summary>
sealed class RecordingDragSource : Avalonia.Input.Platform.IPlatformDragSource
{
    public DragPayload? Last;
    public Task<DragDropEffects> DoDragDrop(PointerEventArgs triggerEvent, Avalonia.Input.IDataObject data, DragDropEffects allowedEffects)
    {
        Last = data.Get(DragPayload.Format) as DragPayload;
        return Task.FromResult(DragDropEffects.None);
    }
}
