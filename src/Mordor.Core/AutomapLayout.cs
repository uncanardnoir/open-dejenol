namespace Mordor.Core;

/// <summary>
/// Where the Automap's picture goes on its form, and what is printed round it: <c>modAdjustMap</c>
/// (FRMAUTOM.BAS:455-501) and <c>sub2E18</c> (503-522). Both test the **form's** size against the
/// window table's default for it, 458x493 (<see cref="WindowLayout.Default(GameWindow)"/>), and
/// never the map's, each axis on its own.
///
/// picMaze itself is never scaled. It is drawn at fifteen pixels a square laid fourteen apart and
/// only moved: at full size it sits at the picture's left and top, and on a form smaller than that
/// the picture fills the form on that axis and the map is slid so the square you stand on - or the
/// middle of the level while you are lost - comes to the middle of it.
///
/// Checked again on 2026-09-28 against a screenshot of the original that looked scaled (the map
/// filling a ~590x620 window with squares ~19 pixels apart), and the code leaves no room for it:
/// every blit is <c>extfn182D</c>, which MODULE8.BAS:7 declares as GDI <c>BitBlt</c> - 1:1, no
/// destination size - and StretchBlt (<c>extfn01C4</c>, MODULE1.BAS:37) is never called here; every
/// coordinate is a fixed multiple of <c>mc001C - 1</c> (226-381, 421, 613-622); picMaze's size is set
/// once, in Form_Load (113-114), and modAdjustMap puts picPicture back to that size at or above
/// full size (474, 491); the generic resize routines that scale controls, sub0760
/// (MAINMOD.BAS:1750) and sub07C5 (MAINMOD.BAS:1925), are never called by this form; and the Options "No StretchBlt"
/// (gv1A66.M5F04) is not read here. The help says only that a smaller window centres the map
/// ("When the Automap is smaller than the base size, the map will center itself"). That screenshot
/// is the full-size form seen through Windows' own display scaling: OTVDM's otvdmw.exe declares no
/// DPI awareness, so on the user's 125% display the whole window is bitmap-stretched - 14 pixels
/// become 17.5, and 458x493 about 572x616.
///
/// The port does enlarge it to fill a form full size or bigger, at the user's request - a GUI
/// deviation, see <see cref="Zoom"/>. Everything above still holds for every form smaller than that.
/// </summary>
public static class AutomapLayout
{
    /// <summary>mc001C, FRMAUTOM.BAS:7: a square is fifteen pixels.</summary>
    public const int Cell = 15;

    /// <summary>and they are laid <c>mc001C - 1</c> apart, so neighbours share an edge.</summary>
    public const int Pitch = Cell - 1;

    /// <summary>
    /// <c>(Me.Width / Screen.TwipsPerPixelX) &lt; gv19C2(mc009E).M5E20</c> (FRMAUTOM.BAS:461-463): the
    /// picture moves to Left 0 and takes the form's width, and the map is centred across it.
    /// </summary>
    public static bool Narrow(double formWidth) => formWidth < WindowLayout.Default(GameWindow.Automap).W;

    /// <summary>
    /// <c>(Me.Height / Screen.TwipsPerPixelY) &lt; gv19C2(mc009E).M5E2D</c> (FRMAUTOM.BAS:478-480): the
    /// picture moves to Top 0 and takes the form's height - over hsbLevel, lblLevel and lblInfo,
    /// which sit above it only at full height (490-493) - and the map is centred down it.
    /// </summary>
    public static bool Shallow(double formHeight) => formHeight < WindowLayout.Default(GameWindow.Automap).H;

    /// <summary>
    /// The column numbers along the bottom. sub2E18 prints them only on a form at least full width
    /// (FRMAUTOM.BAS:508-513), and they are printed on the form under the natural-size picture, so
    /// a short form's picture, which reaches down to the form bottom (479-480), covers them.
    /// </summary>
    public static bool ColumnNumbers(double formWidth, double formHeight) => !Narrow(formWidth) && !Shallow(formHeight);

    /// <summary>
    /// The row numbers up the left. sub2E18 prints them only on a form at least full height
    /// (515-521), at CurrentX -2 (517), in the margin left of the picture; a narrow form's picture starts
    /// at Left 0 (462) and covers them.
    /// </summary>
    public static bool RowNumbers(double formWidth, double formHeight) => !Shallow(formHeight) && !Narrow(formWidth);

    /// <summary>
    /// Where picMaze's left edge goes, from the picture's left, on a form of this width with a
    /// picture <paramref name="room"/> wide. At full width it is 0 (476). Narrower, the square is put
    /// <c>(X - 1) * 14 + 15 / 4</c> left of the middle (465, 469), or the level's middle column,
    /// <c>Width / 2</c>, when a lost flag is up (467). <paramref name="x"/> is 0-based, null while
    /// lost; the rounding is the port's, to land the picture on whole pixels.
    /// </summary>
    public static double Across(double formWidth, double room, int levelWidth, int? x)
    {
        if (!Narrow(formWidth)) return 0;
        double at = (x ?? (levelWidth / 2.0 - 1)) * Pitch + Cell / 4.0;
        return Math.Round(room / 2 - at);
    }

    /// <summary>
    /// The same down: 0 at full height (493), and otherwise the square
    /// <c>(Height - Y) * 14 + 15 / 4</c> above the middle (482, 486), counting from the top because the
    /// map is drawn north up, or the level's middle row when lost (484). <paramref name="y"/> is
    /// 0-based, null while lost.
    /// </summary>
    public static double Down(double formHeight, double room, int levelHeight, int? y)
    {
        if (!Shallow(formHeight)) return 0;
        double yy = y ?? (levelHeight / 2.0 - 1);   // VB3 "/" is a float division
        double at = (levelHeight - 1 - yy) * Pitch + Cell / 4.0;
        return Math.Round(room / 2 - at);
    }

    /// <summary>
    /// The level <c>Form_Load</c> opens on: <c>If Chars(0).M59F4(0) = False And Chars(0).DungeonLevel
    /// &lt;&gt; 0 Then m0036 = DungeonLevel Else m0036 = 1</c> (FRMAUTOM.BAS:94-98). M59F4(0) is the
    /// level flag (<c>LostLevel</c>) alone - a party that has lost only its square still opens on its
    /// level - and nothing else is asked: not the Control Panel's "Auto Change Lev.".
    /// </summary>
    public static int OpeningLevel(int dungeonLevel, bool lostLevel) =>
        !lostLevel && dungeonLevel != 0 ? dungeonLevel : 1;

    /// <summary>
    /// Whether <c>medDrawCursor</c> calls <c>modAdjustMap</c>: only on the branch that draws the arrow -
    /// below ground, neither M59F4(1) nor M59F4(0) set, and the level shown the one you are on
    /// (FRMAUTOM.BAS:306-307, 320-322). The other-level branch (328-331) and the lost one (333-339)
    /// leave picMaze where it was, and so does everything in town (340-342). The only other caller
    /// is <c>Form_Resize</c> (164).
    /// </summary>
    public static bool CursorRecentres(int dungeonLevel, bool lost, int shownLevel) =>
        dungeonLevel > 0 && !lost && dungeonLevel == shownLevel;

    /// <summary>
    /// Whether frmCityMain is what <c>sub14E0</c> shows as a character comes into play:
    /// <c>If Chars(Me).DungeonLevel = 0 And Chars(Me).DeathState = False Then frmCityMain.Show</c>,
    /// and frmDungeonMain otherwise (MAINMOD2.BAS:3170-3180). Only the city's Form_Load minimises a
    /// loaded map (FRMCTYM.BAS:154-156); frmDungeonMain's puts it back to a normal window
    /// (FRMDUNM.BAS:297-299), so the dead - who get the dungeon form even at level 0 - get the map
    /// open.
    /// </summary>
    public static bool CityShownAtStart(int dungeonLevel, bool dead) => dungeonLevel == 0 && !dead;

    /// <summary>
    /// Whether leaving the dungeon loads frmCityMain, whose Form_Load says "UPDATE" and minimises the
    /// map (FRMCTYM.BAS:149-156). Not for the dead: the body stays in frmDungeonMain until a raise,
    /// when sub14CC shows the city (MAINMOD2.BAS:3150-3152). Not for q either: "Exit to Main Menu"
    /// ends the game loop, and sub16E0 and frmMenu.Show follow with no city in between
    /// (MAINMOD2.BAS:6316-6331).
    /// </summary>
    public static bool CityLoadsOnReturn(bool dead, bool exitedToMainMenu) => !dead && !exitedToMainMenu;

    // ------------------------------------------------------------------ the port's zoom
    //
    // A GUI deviation, asked for by the user on 2026-10-14 (docs/FIDELITY.md, "the automap grows
    // with its window"). The original never scales picMaze (see the summary above); the port
    // enlarges it smoothly to fill a form that is full size both ways, and leaves every smaller
    // form exactly as modAdjustMap has it.

    /// <summary>
    /// The device pixels <see cref="Zoom"/> leaves spare across the picture, for
    /// <see cref="DeviceEdges"/> and the map's place each rounding to a whole device pixel: the
    /// drawn map can come out up to two device pixels wider than the factor times its size, and
    /// is centred, so four spare keep it inside.
    /// </summary>
    public const double SnapSlack = 4;

    /// <summary>
    /// How much the map is enlarged in a picture <paramref name="roomW"/> by <paramref name="roomH"/>
    /// for a map <paramref name="mapW"/> by <paramref name="mapH"/> (picMaze with its numbers), all in
    /// the window's own units, on a display that puts <paramref name="renderScaling"/> device pixels
    /// to each of them.
    ///
    /// The largest factor that fits both ways, keeping the map's shape - any factor, not whole
    /// steps (asked for by the user on 2026-10-15: whole device-pixel steps needed a window of
    /// about 731x805 at 125% before the first one, so a maximised game never got it). Less
    /// <see cref="SnapSlack"/> device pixels each way. Nothing below 1 is ever returned: a picture
    /// smaller than the map keeps it at its own size, as modAdjustMap has it. The map stays sharp
    /// and its walls even at a fractional factor because it is redrawn at the device resolution
    /// through <see cref="DeviceEdges"/>, not stretched.
    /// </summary>
    public static double Zoom(double roomW, double roomH, double mapW, double mapH, double renderScaling)
    {
        if (!(mapW > 0) || !(mapH > 0) || !(roomW > 0) || !(roomH > 0)) return 1;
        double rs = renderScaling > 0 && !double.IsInfinity(renderScaling) ? renderScaling : 1;
        double fit = Math.Min((roomW - SnapSlack / rs) / mapW, (roomH - SnapSlack / rs) / mapH);
        return fit > 1 ? fit : 1;
    }

    /// <summary>
    /// Where each map pixel goes on the device, one axis, for a map drawn at <paramref name="scale"/>
    /// device pixels per map pixel: <c>edges[i]</c> to <c>edges[i + 1]</c> is pixel i, so the result
    /// has <paramref name="pixels"/> + 1 entries. Nearest neighbour, but laid by the square rather
    /// than by the pixel.
    ///
    /// The map is the original's 15x15 icons laid <see cref="Pitch"/> apart (AutomapPics, from
    /// MG11.DLL) - bitmaps, not lines that could be drawn again at another size - and their walls
    /// are one-pixel lines on the squares' shared edges. A plain fractional stretch gives each map
    /// pixel <c>floor((i + 1) * s) - floor(i * s)</c> device pixels, so the same wall comes out one
    /// pixel wide on one square and two on the next. Here every square starts on the whole device
    /// pixel nearest <c>c * 14 * s</c>, and inside a square the fourteen pixels always get the same
    /// widths, offset by offset: every wall, door and icon is drawn the same in every square. A
    /// square is <c>floor(14 * s)</c> device pixels or one more; that one more goes to a single
    /// interior offset, never to the line on the square's edge (offset 0), so the walls are one
    /// thickness everywhere.
    /// </summary>
    public static int[] DeviceEdges(int pixels, double scale)
    {
        if (pixels < 0) pixels = 0;
        double s = scale > 0 && !double.IsInfinity(scale) ? scale : 1;
        double step = Pitch * s;
        int p = Math.Max(Pitch, (int)Math.Floor(step + 1e-9));
        var off = new int[Pitch + 1];
        for (int o = 0; o <= Pitch; o++) off[o] = (int)Math.Floor(o * (double)p / Pitch + 0.5);
        int wide = WideOffset(off);
        var edges = new int[pixels + 1];
        for (int i = 0; i <= pixels; i++)
        {
            int c = i / Pitch, o = i % Pitch;
            int start = (int)Math.Floor(c * step + 0.5);
            int extra = (int)Math.Floor((c + 1) * step + 0.5) - start - p;
            edges[i] = start + off[o] + (o > wide ? extra : 0);
        }
        return edges;
    }

    /// <summary>
    /// The offset in a square that takes the extra device pixel when the square is one wider: one
    /// of the narrowest, nearest the middle (7), and never 0, the wall line.
    /// </summary>
    private static int WideOffset(int[] off)
    {
        int best = Pitch / 2, bestW = int.MaxValue, bestD = int.MaxValue;
        for (int o = 1; o < Pitch; o++)
        {
            int w = off[o + 1] - off[o], d = Math.Abs(o - Pitch / 2);
            if (w < bestW || (w == bestW && d < bestD)) { best = o; bestW = w; bestD = d; }
        }
        return best;
    }

    /// <summary>A length in the window's units brought to the nearest whole device pixel.</summary>
    public static double Snap(double v, double renderScaling)
    {
        double rs = renderScaling > 0 && !double.IsInfinity(renderScaling) ? renderScaling : 1;
        return Math.Round(v * rs) / rs;
    }

    /// <summary>
    /// Where an enlarged map starts in its picture, on one axis: in the middle of the room, on a
    /// whole device pixel so the enlarged pixels stay square.
    /// </summary>
    public static double Centred(double room, double size, double renderScaling)
    {
        double rs = renderScaling > 0 && !double.IsInfinity(renderScaling) ? renderScaling : 1;
        return Math.Max(0, Math.Round((room - size) / 2 * rs) / rs);
    }

    /// <summary>
    /// <see cref="Centred(double, double, double)"/> for a box with numbers at one side of the map:
    /// the map itself - the box less <paramref name="lead"/> before it and <paramref name="tail"/>
    /// after it - is put in the middle of the room, and the box is kept inside the room. The whole
    /// box centred had left the map a half gutter off the middle, to the right of it across (the row
    /// numbers' gutter is at its left) and up the picture (the column numbers' is below it).
    /// </summary>
    public static double Centred(double room, double size, double lead, double tail, double renderScaling)
    {
        double rs = renderScaling > 0 && !double.IsInfinity(renderScaling) ? renderScaling : 1;
        double at = (room - (size - lead - tail)) / 2 - lead;
        at = Math.Min(at, room - size);
        return Math.Max(0, Math.Round(at * rs) / rs);
    }

    /// <summary>
    /// A point on picPicture, in the window's units, as a point on the unscaled map: the map drawn
    /// from <paramref name="left"/>, <paramref name="top"/> at <paramref name="scale"/>. Every pointer
    /// test on the map goes through this, so a click lands on the same square at any size.
    /// </summary>
    public static (double X, double Y) ToMap(double x, double y, double left, double top, double scale)
    {
        double s = scale > 0 ? scale : 1;
        return ((x - left) / s, (y - top) / s);
    }

    /// <summary>
    /// Whether a point on the unscaled map (from <see cref="ToMap"/>) is on picMaze itself, which
    /// starts <paramref name="gutterLeft"/> in, past the row numbers, and is
    /// <paramref name="mazeW"/> by <paramref name="mazeH"/>: the only place <c>picMaze_MouseDown</c>
    /// sees a click (FRMAUTOM.BAS:600-602).
    /// </summary>
    public static bool OnMaze(double mapX, double mapY, double gutterLeft, int mazeW, int mazeH) =>
        mapX >= gutterLeft && mapX < gutterLeft + mazeW && mapY >= 0 && mapY < mazeH;
}
