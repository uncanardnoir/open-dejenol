namespace Mordor.Core;

/// <summary>
/// The windows the game remembers the size of. These are the original's own indices into
/// <c>gv19C2</c> (the table of window defaults) and <c>M5958</c> (the copy kept on the character),
/// taken from the constant each form declares for itself.
///
/// The numbering has gaps because it is the original's. Index 1 is never used by any form, and 4,
/// 6, 7, 8 and 9 have neither a form nor a default.
/// </summary>
public enum GameWindow
{
    /// <summary>FRMCHAR1, the character window with its seven tabs.</summary>
    Character = 0,
    /// <summary>FRMCHAR3, Objects &amp; Spells.</summary>
    ObjectsAndSpells = 2,
    /// <summary>FRMCHAR4, the Messages list.</summary>
    Messages = 3,
    /// <summary>FRMCITYC, the companion store.</summary>
    CompanionStore = 5,
    /// <summary>
    /// FRMCTYM, frmCityMain - the town itself. Its Form_Load declares <c>mc004C = 10</c> and sizes
    /// the form from <c>M5958(10)</c>, falling back to this default (FRMCTYM.BAS:95-104); it
    /// registers its handle in <c>gv19C2(10).M5E47</c> (FRMCTYM.BAS:145), so the Control Panel's
    /// Reset and Adjust to Display reach it; and Form_Unload saves it to the party
    /// (FRMCTYM.BAS:171). This entry used to be read as a morgue window and the city was kept
    /// out of the table as a window with no size.
    /// </summary>
    City = 10,
    /// <summary>FRMCTYS, the General Store.</summary>
    GeneralStore = 11,
    /// <summary>FRMCTYG, a guild.</summary>
    Guild = 12,
    /// <summary>FRMDUNM, the dungeon itself.</summary>
    Dungeon = 13,
    /// <summary>
    /// The one window with no form of its own in the decompiled source. It is left out of the
    /// Control Panel's list and out of Adjust to Display, so nothing ever resizes it.
    /// </summary>
    Unnamed = 14,
    /// <summary>FRMCITYB, the bank.</summary>
    Bank = 15,
    /// <summary>FRMMORG, the morgue.</summary>
    Morgue = 16,
    /// <summary>FRMGROUP, Party Information.</summary>
    Group = 17,
    /// <summary>FRMAUTOM, the automap.</summary>
    Automap = 18,
    /// <summary>FRMSEEK, the Seer.</summary>
    Seer = 19,
}

/// <summary>One window's placement, the original's M425F / M4258 / M4216 / M420C / M5801.</summary>
public sealed class WindowRect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>M5801, the VB WindowState: 0 normal, 1 minimised, 2 maximised.</summary>
    public int State { get; set; }

    /// <summary>A window that has never been given a size opens at whatever the form was drawn.</summary>
    public bool HasSize => Width > 0 && Height > 0;
}

/// <summary>
/// <c>gv19C2</c>, the table of window defaults (MODULE1.BAS:606-648), and the three things the game
/// does with it: seed a new character's sizes (CHARCREA.BAS:302-342), Adjust to Display and Reset
/// (FRMCONTR.BAS).
///
/// The defaults are written in twips, which is fifteen to the pixel at the 96dpi the game was drawn
/// for, so they are held here in pixels. The Dungeon's 7320x3450 twips is 488x230 pixels, and the
/// help's own screenshot of that window (game_files/dmainw1.png) is 487x229 - the client area
/// inside a one-pixel border. That is the check that the conversion is right.
///
/// The automap is the exception: its entry is already in pixels, which is why <see cref="Seed"/>
/// treats index 18 differently and why the automap's own Form_Resize compares
/// <c>Me.Width / TwipsPerPixelX</c> against it (FRMAUTOM.BAS:152-159).
/// </summary>
public static class WindowLayout
{
    /// <summary>mc0078 / mc02E2: how many entries the table has.</summary>
    public const int Count = 20;

    /// <summary>
    /// The layout the port now writes, stamped on the character as it saves. It is only a stamp,
    /// kept so that saves carrying the field still load: a table written under an earlier one is
    /// read back as it was, as the original reads any saved table (see <see cref="Prepare"/>). The
    /// port used to wipe such a table and lay it out again, which the original never does - it
    /// writes the table only at character creation (CHARCREA.BAS:302-343), in the Control Panel
    /// (FRMCONTR.BAS:115-116, 149-150, 185-186) and as forms unload.
    /// </summary>
    public const int Revision = 1;

    /// <summary>VB's Screen.TwipsPerPixel at the 96dpi the game was drawn for.</summary>
    public const int TwipsPerPixel = 15;

    /// <summary>The screen the sizes above were chosen for. Everything scales up from here.</summary>
    public const double BaselineWidth = 800, BaselineHeight = 600;

    /// <summary>Large Fonts divides the scale by this (CHARCREA.BAS:302-306, FRMCONTR.BAS:74-80).</summary>
    public const double LargeFontDivisor = 1.25;

    /// <summary>
    /// The automap's seeded size is its default over this, and is not scaled to the screen at all
    /// (CHARCREA.BAS:341-342). 458x493 becomes 204x219.
    /// </summary>
    public const double AutomapSeedDivisor = 2.25;

    private static readonly (double W, double H)[] _defaults = BuildDefaults();

    private static (double W, double H)[] BuildDefaults()
    {
        var d = new (double, double)[Count];
        void Twips(GameWindow w, int width, int height) =>
            d[(int)w] = ((double)width / TwipsPerPixel, (double)height / TwipsPerPixel);

        Twips(GameWindow.Character, 4110, 5520);
        Twips(GameWindow.ObjectsAndSpells, 4185, 3510);
        Twips(GameWindow.Messages, 7125, 1560);
        Twips(GameWindow.CompanionStore, 6630, 4485);
        Twips(GameWindow.City, 7320, 3450);
        Twips(GameWindow.GeneralStore, 7290, 4785);
        Twips(GameWindow.Guild, 5340, 3825);
        Twips(GameWindow.Dungeon, 7320, 3450);
        Twips(GameWindow.Unnamed, 3900, 1875);
        Twips(GameWindow.Bank, 6315, 4350);
        Twips(GameWindow.Morgue, 3600, 2310);
        Twips(GameWindow.Group, 4485, 1890);
        Twips(GameWindow.Seer, 5145, 2160);
        // the automap's entry is written in pixels, not twips
        d[(int)GameWindow.Automap] = (458, 493);
        return d;
    }

    /// <summary>The size the window was drawn at, in pixels. Zero for an index with no form.</summary>
    public static (double W, double H) Default(int window) =>
        window >= 0 && window < Count ? _defaults[window] : (0, 0);

    public static (double W, double H) Default(GameWindow window) => Default((int)window);

    /// <summary>
    /// How small a window may be dragged, from its own Form_Resize. Five forms refuse anything
    /// under half their default: the character window (FRMCHAR1.BAS:115-119), Objects &amp; Spells
    /// (FRMCHAR3.BAS:416-420), Messages (FRMCHAR4.BAS:64-68), Party Information
    /// (FRMGROUP.BAS:110-114) and the Dungeon (FRMDUNM.BAS:319-323). The automap refuses a quarter
    /// of its default, which is in pixels (FRMAUTOM.BAS:157-161). The City has a Fixed Double
    /// border (FRMCTYM.FRM, BorderStyle 3) and cannot be dragged to any size, so it has no floor
    /// here. Every other window has none either. The port had its own, larger minimums.
    /// </summary>
    public static (double W, double H) Minimum(GameWindow window)
    {
        var (w, h) = Default(window);
        return window switch
        {
            GameWindow.Character or GameWindow.ObjectsAndSpells or GameWindow.Messages
                or GameWindow.Group or GameWindow.Dungeon => (w / 2, h / 2),
            GameWindow.Automap => (w / 4, h / 4),
            _ => (0, 0),
        };
    }

    /// <summary>
    /// Whether the window's height follows its width. Four forms put the height back to the
    /// default's in proportion to the width on every resize while the window is normal: the
    /// character window (FRMCHAR1.BAS:122, 186-189), Objects &amp; Spells (FRMCHAR3.BAS:425-429), Party
    /// Information (FRMGROUP.BAS:117-121) and the Dungeon (FRMDUNM.BAS:328-332). Messages and the
    /// automap are free in both directions.
    /// </summary>
    public static bool KeepsAspect(GameWindow window) =>
        window is GameWindow.Character or GameWindow.ObjectsAndSpells or GameWindow.Group or GameWindow.Dungeon;

    /// <summary>
    /// The height such a window is given for a width: <c>M5E2D * (Width / M5E20)</c>.
    /// </summary>
    public static double AspectHeight(GameWindow window, double width)
    {
        var (w, h) = Default(window);
        return w > 0 ? h * (width / w) : 0;
    }

    /// <summary>
    /// The scale a screen of this size asks for. Never less than one: the sizes are a floor, so a
    /// screen smaller than 800x600 gets the windows at the size they were drawn and has to live
    /// with it.
    /// </summary>
    public static (double X, double Y) ScaleFor(double screenWidth, double screenHeight, bool largeFonts)
    {
        double f = largeFonts ? LargeFontDivisor : 1.0;
        double x = (screenWidth / BaselineWidth) / f;
        double y = (screenHeight / BaselineHeight) / f;
        return (Math.Max(1, x), Math.Max(1, y));
    }

    /// <summary>
    /// The windows the Control Panel lists, and so the ones Reset and Show can reach. It leaves out
    /// the Control Panel itself and the shops, which have a size of their own that the player is
    /// not meant to be fiddling with (FRMCONTR.BAS Form_Load).
    /// </summary>
    public static bool ListedInWindowControl(int window) => window switch
    {
        (int)GameWindow.Unnamed => false,
        (int)GameWindow.Automap => false,
        (int)GameWindow.GeneralStore => false,
        (int)GameWindow.Morgue => false,
        (int)GameWindow.Guild => false,
        (int)GameWindow.Seer => false,
        (int)GameWindow.Bank => false,
        _ => true,
    };

    /// <summary>
    /// Whose copy of the table a window's placement is written to when the window closes.
    ///
    /// Twelve of the thirteen forms call <c>sub1614 -1, index, Me, True</c>, and the -1 makes that
    /// routine loop <c>For l068C = 0 To UBound(Chars)</c> and write the same placement onto every
    /// character in the party (MODULE6.BAS:4370-4392). The layout belongs to the party, not to the
    /// leader.
    ///
    /// The automap never calls <c>sub1614</c>. It assigns <c>Chars(m0078).M5958(...)</c> by hand
    /// with <c>m0078 = 0</c> (FRMAUTOM.BAS:173-176, and again at 571-574 when it is resized), so
    /// its placement reaches the first character and nobody else.
    ///
    /// Every *reader* of the table uses character 0 - all fourteen forms, each through its own
    /// constant or a never-assigned <c>Static Integer</c>, which VB starts at zero. So the automap's
    /// narrower save is invisible until the party's first slot changes hands.
    /// </summary>
    public static bool PlacementSavedToWholeParty(GameWindow which) => which != GameWindow.Automap;

    /// <summary>
    /// Adjust to Display leaves two windows alone: the unnamed one and the automap
    /// (FRMCONTR.BAS:97-99, and again at 146-149 for the &lt;ALL&gt; pass). The Control Panel's own
    /// window is skipped there too, by handle rather than by index.
    ///
    /// No reason is given for the automap. This file used to assert one - that its table entry is
    /// in pixels, so scaling it would be nonsense - which is a guess, and reads here as if it were
    /// something the source says. It fits, since the entry really is in pixels, but the source
    /// simply lists the three handles and moves on.
    /// </summary>
    public static bool AdjustedByControlPanel(int window) =>
        window != (int)GameWindow.Unnamed && window != (int)GameWindow.Automap;

    /// <summary>
    /// The table as a frame finds it before it places anything: made whole, and seeded as a new
    /// character's (CHARCREA.BAS:302-342) only when it holds no size at all. Answers whether it
    /// also has no position at all, and so wants <see cref="Arrange"/> (CHARCREA.BAS:343).
    ///
    /// Besides a form unloading, the original writes the table only when a character is made and
    /// when the player asks it to in the Control Panel - Adjust to Display (FRMCONTR.BAS:115-116,
    /// 149-150) and Reset (185-186). Loading never does: a table that was saved is read back as it
    /// was, whatever the port's layout was when
    /// it was written. The port had also wiped every table saved before <see cref="Revision"/> 1
    /// and laid it out again, which threw away the player's own arrangement - an invention,
    /// removed 2026-09-27.
    /// </summary>
    public static bool Prepare(Character leader, double scaleWidth, double scaleHeight)
    {
        var table = leader.NormaliseWindows();
        bool blank = true;
        foreach (var r in table) if (r.HasSize) { blank = false; break; }
        if (blank) Seed(table, scaleWidth, scaleHeight, largeFonts: false);
        foreach (var r in table) if (r.Left != 0 || r.Top != 0) return false;
        return true;
    }

    /// <summary>
    /// <c>sub0524</c> (MAINMOD.BAS:487-494): a window whose Left lies past <c>Screen.Width</c>, or
    /// whose Top lies past <c>Screen.Height</c>, has that side put back to nought (<c>False</c>).
    /// <c>Screen</c> is the whole display, not the frame, so a window left low in a frame that is
    /// smaller now than when it was saved keeps its place, and is back in view once the frame is
    /// its size again. The port had compared against the frame's own area, and so moved every
    /// window saved below a frame that had not yet reached its size.
    /// </summary>
    public static (double Left, double Top) KeptOnScreen(double left, double top, double screenWidth, double screenHeight) =>
        (left > screenWidth ? 0 : left, top > screenHeight ? 0 : top);

    /// <summary>
    /// A fresh set of windows, none of them sized yet.
    /// </summary>
    public static WindowRect[] Fresh()
    {
        var w = new WindowRect[Count];
        for (int i = 0; i < Count; i++) w[i] = new WindowRect();
        return w;
    }

    /// <summary>
    /// CHARCREA.BAS:302-342, run once when a character is made: every window gets the default for
    /// the screen it is being made on. The positions are <see cref="Arrange"/>'s, which CHARCREA
    /// calls next (CHARCREA.BAS:343).
    ///
    /// Two things about this routine are the original's and are kept:
    ///
    /// - it seeds index 1, which no form uses and which has no default, and never seeds index 14,
    ///   which has one. A new character therefore carries a zero size for window 14. Nothing reads
    ///   it: 14 is left out of the Control Panel's list and out of Adjust to Display, so the fault
    ///   is invisible in play
    /// - the automap is seeded at its default over 2.25 and is not scaled to the screen
    /// </summary>
    public static void Seed(WindowRect[] windows, double screenWidth, double screenHeight, bool largeFonts)
    {
        var (sx, sy) = ScaleFor(screenWidth, screenHeight, largeFonts);
        foreach (int i in SeededByCharacterCreation)
        {
            var (w, h) = Default(i);
            windows[i].Width = w * sx;
            windows[i].Height = h * sy;
        }
        var (aw, ah) = Default(GameWindow.Automap);
        windows[(int)GameWindow.Automap].Width = aw / AutomapSeedDivisor;
        windows[(int)GameWindow.Automap].Height = ah / AutomapSeedDivisor;
    }

    /// <summary>
    /// <c>sub150A</c> (MAINMOD2.BAS:3210-3244), which CHARCREA calls straight after the sizes are
    /// seeded (CHARCREA.BAS:343): the new character's windows are laid out around the character
    /// window rather than left on top of one another. In pixels, as the table is held here - the
    /// original's <c>Screen.TwipsPerPixel</c> is one pixel, and its 50 twips are 50/15 of one:
    ///
    /// - the character window, 0, at 1,1
    /// - the City, 10, and the Dungeon, 13, beside it, one pixel to its right - the same place,
    ///   since only one of the two is ever up (sub14E0, MAINMOD2.BAS:3170-3180)
    /// - Objects &amp; Spells, 2, under the Dungeon with its right edge on the Dungeon's
    /// - Party Information, 17, under the character window at the left edge
    /// - Messages, 3, in the bottom right corner of the frame: 8 pixels in from the right, and its
    ///   bottom 2.5 times the caption and frame heights (GetSystemMetrics 4 and 33) above the
    ///   frame's
    /// - the Automap, 18, under the Dungeon at the Dungeon's left edge
    ///
    /// Each is put back to a normal window, save the Dungeon's, whose state is not touched.
    /// <paramref name="frameWidth"/> and <paramref name="frameHeight"/> are <c>Settings.M6049</c> and
    /// <c>M6039</c>: the frame's outer size when it is maximised, the screen's
    /// otherwise (FRMMAIN.BAS:228-234).
    ///
    /// Party Information (299 pixels) is wider than the character window (274) above it, so the
    /// Automap, shown after it (sub14E0, MAINMOD2.BAS:3163-3164) and so in front, covers its right
    /// edge - the end of "Option" - in the original as in the port. That is kept. A mod's Party
    /// Information columns are drawn at that right edge, and would all be under the Automap: with
    /// <paramref name="partyWidened"/> (a mod column is shown) the Automap goes one pixel right of
    /// Party Information instead, if that is further right, and as far toward it as Objects &amp;
    /// Spells, beside it, leaves room for (a deviation, docs/FIDELITY.md). With no mod column, the
    /// original's arrangement exactly.
    /// </summary>
    public static void Arrange(WindowRect[] windows, double frameWidth, double frameHeight,
                               double captionHeight, double frameBorderHeight, bool partyWidened = false)
    {
        const double px = 1, fifty = 50.0 / TwipsPerPixel;
        var ch = windows[(int)GameWindow.Character];
        var dn = windows[(int)GameWindow.Dungeon];
        ch.Left = px; ch.Top = px; ch.State = 0;
        var city = windows[(int)GameWindow.City];
        city.Left = ch.Left + ch.Width + px; city.Top = px; city.State = 0;
        dn.Left = ch.Left + ch.Width + px; dn.Top = ch.Top;
        var os = windows[(int)GameWindow.ObjectsAndSpells];
        os.Left = dn.Left + dn.Width - os.Width;
        os.Top = dn.Top + dn.Height + fifty + px; os.State = 0;
        var gr = windows[(int)GameWindow.Group];
        gr.Left = px; gr.Top = ch.Height + px * 2; gr.State = 0;
        double below = (captionHeight + frameBorderHeight) * (px * 2.5);
        var ms = windows[(int)GameWindow.Messages];
        ms.Left = frameWidth - ms.Width - px * 8;
        ms.Top = frameHeight - below - ms.Height; ms.State = 0;
        var am = windows[(int)GameWindow.Automap];
        am.Left = ch.Left + ch.Width + px;
        // ... but never into Objects & Spells beside it, at the same height: on a small screen
        // there is not room for both, and it goes only as far as there is
        if (partyWidened) am.Left = Math.Max(am.Left, Math.Min(gr.Left + gr.Width + px, os.Left - am.Width - px));
        am.Top = dn.Top + dn.Height + fifty + px; am.State = 0;
    }

    /// <summary>The indices CHARCREA writes, in its own order, with its own 1-for-14 slip.</summary>
    public static readonly int[] SeededByCharacterCreation = { 0, 1, 10, 12, 11, 5, 16, 15, 19, 2, 3, 17, 13 };

    /// <summary>
    /// "Adjust to Display" (FRMCONTR.BAS:71-131). One window, or all of them when &lt;ALL&gt; is
    /// picked. A maximised or minimised window is put back to normal first.
    /// </summary>
    public static void AdjustToDisplay(WindowRect[] windows, double screenWidth, double screenHeight,
                                       bool largeFonts, int? only = null)
    {
        var (sx, sy) = ScaleFor(screenWidth, screenHeight, largeFonts);
        for (int i = 0; i < Count; i++)
        {
            if (only.HasValue && only.Value != i) continue;
            if (!AdjustedByControlPanel(i)) continue;
            var (w, h) = Default(i);
            if (w <= 0) continue;
            windows[i].State = 0;
            windows[i].Width = w * sx;
            windows[i].Height = h * sy;
        }
    }

    /// <summary>
    /// "Reset" (FRMCONTR.BAS:167-185 and <c>sub074E</c>, MODULE2.BAS:1680): back to the size the
    /// window was drawn at, with no scaling for the screen at all. On &lt;ALL&gt; it reaches every
    /// entry, the automap included - which Adjust to Display does not.
    /// </summary>
    public static void Reset(WindowRect[] windows, int? only = null)
    {
        for (int i = 0; i < Count; i++)
        {
            if (only.HasValue && only.Value != i) continue;
            var (w, h) = Default(i);
            if (w <= 0) continue;
            windows[i].State = 0;
            windows[i].Width = w;
            windows[i].Height = h;
        }
    }
}

/// <summary>
/// The MDI frame's scroll bars. An MDIForm's ScrollBars property is True unless it is set
/// otherwise (a VB3 default; FRMMAIN.FRM is not among the decompiled files), and the frame then
/// shows a bar along an edge whenever a child lies past that edge of its client area. A port
/// necessity: sub0524 (MAINMOD.BAS:487-494) leaves a window anywhere on the display, so one
/// saved below a smaller frame is reached by scrolling, as it would be in the original.
/// </summary>
public static class MdiScroll
{
    /// <summary>One child, where it is in the area and how it is shown.</summary>
    public readonly record struct Child(double X, double Y, double Width, double Height,
                                        bool Shown, bool Minimised, bool Maximised);

    /// <summary>
    /// What the bars show for an area of <paramref name="areaW"/> by <paramref name="areaH"/>:
    /// whether each is up, and the range of the view's corner along it. The range runs from the
    /// leftmost (topmost) child or the area's own edge to where the view shows the rightmost
    /// (lowest) - a bar lies over the far edge, so the range goes that much further to uncover it.
    /// Icons and hidden windows take no part, and with a child maximised nothing scrolls, as it
    /// fills the area. <paramref name="keep"/>, the view as it is, stays inside the range: a window
    /// being dragged does not have the ground pulled from under it.
    /// </summary>
    public static (bool H, bool V, double MinX, double MaxX, double MinY, double MaxY) Bars(
        IEnumerable<Child> children, double areaW, double areaH, double bar,
        (double X, double Y)? keep = null)
    {
        // the children's own reach, and the range's ends, which take in the area itself
        double minX = 0, minY = 0, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var c in children)
        {
            if (!c.Shown || c.Minimised) continue;
            if (c.Maximised) return (false, false, 0, 0, 0, 0);
            minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y);
            maxX = Math.Max(maxX, c.X + c.Width); maxY = Math.Max(maxY, c.Y + c.Height);
        }
        const double slack = 0.5;
        bool h = minX < -slack || maxX > areaW + slack;
        bool v = minY < -slack || maxY > areaH + slack;
        if (h && !v) v = maxY > areaH - bar + slack;
        if (v && !h) h = maxX > areaW - bar + slack;
        double rx = Math.Max(0, maxX + (v ? bar : 0) - areaW);
        double ry = Math.Max(0, maxY + (h ? bar : 0) - areaH);
        if (!h) minX = rx = 0;
        if (!v) minY = ry = 0;
        if (keep is (double kx, double ky))
        {
            if (Math.Abs(kx) > slack) { h = true; minX = Math.Min(minX, kx); rx = Math.Max(rx, kx); }
            if (Math.Abs(ky) > slack) { v = true; minY = Math.Min(minY, ky); ry = Math.Max(ry, ky); }
        }
        return (h, v, minX, rx, minY, ry);
    }
}
