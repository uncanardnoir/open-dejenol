using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// The windows mods offer (<c>ctx.UI.AddWindow</c>, docs/MODDING.md): one of each at most, opened
/// and closed from a button the toolbar carries after its icons.
/// Each is a child window of the game's frame (<see cref="MdiChild"/> in its <see cref="MdiHost"/>),
/// beside Objects &amp; Spells and Party Information: the same caption bar, borders, sizing grip and
/// minimise / maximise / close boxes, stacked among the game's own children, and gone down to the
/// dungeon and back up with the party as they are. A click brings it forward without making it the
/// active child (<see cref="MdiChild.Activates"/>), and nothing in its frame takes the keyboard:
/// the game's keys stay the game's.
///
/// Its text is drawn in the face and size of the game's list rows - Party Information's names,
/// Objects &amp; Spells' items (frmParty lblName, frmCharStat3 lblItem: MS Sans Serif 8.25 through
/// the Options fonts, <see cref="UiFonts"/>), which is also the Messages list's.
///
/// Whether it was open and where it stood in the frame are kept in settings.ini, in its mod's
/// section (<c>Window.&lt;title&gt;.Open</c> and <c>.Place</c>), and a window left open opens
/// again in the next game. A window whose mod says it is not offered now (a toggle switched off),
/// or whose mod stops, is closed, and comes back when it is offered again, if it was open.
///
/// There is no frame on the main menu, so nothing opens there (the Mods page offers no Open
/// button). With no mod offering a window nothing here adds anything: the toolbar is the original's.
/// </summary>
internal static class ModWindows
{
    /// <summary>An open mod window: its child, the frame area it is in, and the frame's window.</summary>
    private sealed class OpenWindow
    {
        public required MdiChild Child { get; init; }
        public required MdiHost Host { get; set; }
        public required Window Frame { get; set; }
    }

    private static readonly Dictionary<(string Mod, string Title), OpenWindow> Open = new();

    /// <summary>Windows seen to close, so none is ever shown in again.</summary>
    private static readonly ConditionalWeakTable<Window, object> Closed = new();
    private static readonly ConditionalWeakTable<Window, object> Watched = new();

    /// <summary>The child windows that are mods', for the checks.</summary>
    private static readonly ConditionalWeakTable<MdiChild, object> Mine = new();

    /// <summary>Whether a child window is a mod's (<see cref="Show(LoadedMod, ModWindow, MdiHost)"/>).</summary>
    public static bool IsModWindow(MdiChild c) => Mine.TryGetValue(c, out _);

    /// <summary>
    /// A frame's window followed: as it closes, its mod windows' places are written, and they are
    /// forgotten as open - closed with the game, still wanted open for the next one.
    /// </summary>
    private static void Watch(Window w)
    {
        if (Watched.TryGetValue(w, out _)) return;
        Watched.Add(w, new object());
        w.Closing += (_, _) =>
        {
            foreach (var ((mod, title), open) in Open.ToList())
                if (ReferenceEquals(open.Frame, w) && Find(mod, title) is { } found) SavePlace(found.Mod, found.Window, open.Child);
        };
        w.Closed += (_, _) =>
        {
            Closed.AddOrUpdate(w, new object());
            bool any = false;
            foreach (var (key, open) in Open.ToList())
                if (ReferenceEquals(open.Frame, w)) { Open.Remove(key); any = true; }
            if (any) Changed?.Invoke();
        };
    }

    private static bool IsClosed(Window w) => Closed.TryGetValue(w, out _);

    private static (LoadedMod Mod, ModWindow Window)? Find(string mod, string title)
    {
        foreach (var (lm, w) in ModHost.Active.Windows)
            if (lm.Id == mod && w.Title == title) return (lm, w);
        return null;
    }

    /// <summary>
    /// The game's frame for a window: the top of its owner chain. The Mods page is a dialog of the
    /// title (the main menu's Mods button) or of the Control Panel, itself a dialog of the city, so
    /// it is the city that holds what it opens, not the dialog that closes first.
    /// </summary>
    public static Window FrameOf(Window w)
    {
        while (w.Owner is Window o) w = o;
        return w;
    }

    /// <summary>
    /// The area the game's child windows are in, in a frame's window: the town's, or the dungeon's
    /// while the party is below (its contents are put into the city's window in place of the
    /// town's, DungeonWindow.RunInside). None on the main menu, or in a window that has closed.
    /// </summary>
    public static MdiHost? HostIn(Window frame) =>
        IsClosed(frame) ? null : frame.GetVisualDescendants().OfType<MdiHost>().FirstOrDefault();

    private static string Key(ModWindow w, string what) =>
        "Window." + new string(w.Title.Where(char.IsLetterOrDigit).ToArray()) + "." + what;

    private static bool WantedOpen(LoadedMod lm, ModWindow w) =>
        ModHost.Active.Settings.ReadFlag(lm.Id, Key(w, "Open"), false);

    private static void SetWanted(LoadedMod lm, ModWindow w, bool open)
    {
        if (WantedOpen(lm, w) != open) ModHost.Active.Settings.WriteFlag(lm.Id, Key(w, "Open"), open);
    }

    public static bool IsOpen(LoadedMod lm, ModWindow w) => Open.ContainsKey((lm.Id, w.Title));

    /// <summary>The child an open mod window is drawn in, for the checks; null when it is not open.</summary>
    public static MdiChild? ChildOf(LoadedMod lm, ModWindow w) =>
        Open.TryGetValue((lm.Id, w.Title), out var o) ? o.Child : null;

    /// <summary>The default size of a mod window, in the frame's units.</summary>
    public const double DefaultWidth = 520, DefaultHeight = 360;

    /// <summary>
    /// Open it in <paramref name="host"/>, or bring it forward there if it is open already; one open
    /// in another frame area (the town's, the party now below) is moved into this one.
    /// </summary>
    public static void Show(LoadedMod lm, ModWindow w, MdiHost host)
    {
        if (TopLevel.GetTopLevel(host) is not Window frame || IsClosed(frame)) return;
        Watch(frame);
        if (Open.TryGetValue((lm.Id, w.Title), out var already))
        {
            if (!ReferenceEquals(already.Host, host)) Move(already, host, frame);
            if (already.Child.Minimised) already.Child.Minimised = false;
            host.BringToFront(already.Child);
            return;
        }
        if (!ModHost.Active.IsShown(lm, w)) return;
        if (ModHost.Active.BuildWindow(lm, w) is not Control content) return;

        // the game's list rows' face and size (frmParty lblName, through the Options fonts), on a
        // holder of the port's so the mod's own control is not restyled
        var body = UiFonts.Set(new Border { Child = content }, DesignFonts.Party, "lblName(0)");
        var child = host.Add(w.Title, body, 0, 0, DefaultWidth, DefaultHeight);
        Mine.AddOrUpdate(child, new object());
        child.Activates = false;
        child.MinWidth = 200;
        child.MinHeight = 120;
        var (place, state) = ReadPlace(lm, w);
        if (state == 1) child.Minimised = true;
        else if (state == 2) child.Maximised = true;
        PlaceIn(host, child, () => place ?? Centred(host));

        var entry = new OpenWindow { Child = child, Host = host, Frame = frame };
        // the close box: the player's close, so it stays closed in the next game
        child.Closed += _ =>
        {
            if (!Open.TryGetValue((lm.Id, w.Title), out var now) || now != entry) return;
            SavePlace(lm, w, child);
            Forget(lm, w, entry);
            SetWanted(lm, w, false);
            Changed?.Invoke();
        };
        Open[(lm.Id, w.Title)] = entry;
        SetWanted(lm, w, true);
        Changed?.Invoke();
    }

    /// <summary>Out of its frame and off the list of open ones; whether it is still wanted is the caller's.</summary>
    private static void Forget(LoadedMod lm, ModWindow w, OpenWindow entry)
    {
        Open.Remove((lm.Id, w.Title));
        entry.Host.Remove(entry.Child);
    }

    /// <summary>Into another frame area, where it stood in the old one (held to the new one's size).</summary>
    private static void Move(OpenWindow entry, MdiHost host, Window frame)
    {
        var child = entry.Child;
        var place = child.Placement();
        entry.Host.Remove(child);
        host.Adopt(child);
        PlaceIn(host, child, () => place);
        entry.Host = host;
        entry.Frame = frame;
    }

    /// <summary>
    /// Put a child where <paramref name="place"/> says, held to the frame area (<see cref="Fit"/>),
    /// once the area has a size: the dungeon's is not laid out yet when its toolbar first syncs, so
    /// the place waits for its first size. A maximised child is put back to its normal size there
    /// first and maximised again, so it fills this area (a maximised child placed as it stood kept
    /// the old area's size, could not be dragged, and turned the scroll bars off); a minimised one
    /// keeps its icon in the bottom row and comes back to the place.
    /// </summary>
    private static void PlaceIn(MdiHost host, MdiChild child, Func<Rect> place)
    {
        void Now()
        {
            if (!host.Windows.Contains(child)) return;
            if (child.Maximised)
            {
                child.Maximised = false;
                child.PlaceAt(Fit(host, place()));
                child.Maximised = true;
            }
            else child.PlaceAt(Fit(host, place()));
        }
        if (host.Bounds.Width > 1 && host.Bounds.Height > 1) { Now(); return; }
        EventHandler<SizeChangedEventArgs>? once = null;
        once = (_, _) =>
        {
            if (host.Bounds.Width <= 1 || host.Bounds.Height <= 1) return;
            host.SizeChanged -= once;
            Now();
        };
        host.SizeChanged += once;
    }

    /// <summary>Close it as the player would (it stays closed in the next game).</summary>
    public static void Hide(LoadedMod lm, ModWindow w)
    {
        if (!Open.TryGetValue((lm.Id, w.Title), out var open)) return;
        SavePlace(lm, w, open.Child);
        Forget(lm, w, open);
        SetWanted(lm, w, false);
        Changed?.Invoke();
    }

    /// <summary>Close it without the player having asked: it is still wanted open.</summary>
    private static void CloseQuietly((string Mod, string Title) key)
    {
        if (!Open.TryGetValue(key, out var open)) return;
        if (Find(key.Mod, key.Title) is { } found) SavePlace(found.Mod, found.Window, open.Child);
        Open.Remove(key);
        open.Host.Remove(open.Child);
        Changed?.Invoke();
    }

    /// <summary>A window opened or closed: the toolbar's buttons show which.</summary>
    private static event Action? Changed;

    /// <summary>
    /// Every window left open that is offered now opens again in <paramref name="host"/>; every one
    /// open that is no longer offered closes (and is still wanted, for when it is). One open in
    /// another frame area - the town's, with the party now in the dungeon, or the other way about -
    /// moves into this one, where it stood. A frame that has closed is left alone.
    /// </summary>
    public static void Sync(MdiHost host)
    {
        if (TopLevel.GetTopLevel(host) is not Window frame || IsClosed(frame)) return;
        Watch(frame);
        foreach (var (lm, w) in ModHost.Active.Windows)
        {
            bool shown = ModHost.Active.IsShown(lm, w);
            if (Open.TryGetValue((lm.Id, w.Title), out var open))
            {
                if (!shown) CloseQuietly((lm.Id, w.Title));
                else if (!ReferenceEquals(open.Host, host)) Move(open, host, frame);
            }
            else if (shown && WantedOpen(lm, w)) Show(lm, w, host);
        }
        // a mod disabled for throwing takes its windows with it
        CloseStopped();
    }

    /// <summary>
    /// Every open window whose mod is not running now (disabled for throwing, or switched off on
    /// the main menu's Mods page) closes, still wanted open for when it runs again. Nothing is opened.
    /// </summary>
    public static void CloseStopped()
    {
        foreach (var key in Open.Keys.ToList())
            if (!ModHost.Active.Live.Any(l => l.Id == key.Mod)) CloseQuietly(key);
    }

    /// <summary>
    /// The saved place: left, top, width and height in the frame area's units, as a game child's
    /// placement is kept (<see cref="MdiChild.Placement"/>), and its state - 0 normal, 1 minimised,
    /// 2 maximised - as the game keeps its children's. Four numbers (no state) are the screen-pixel
    /// place of the top-level window an earlier version had, and are not used.
    /// </summary>
    private static (Rect? Place, int State) ReadPlace(LoadedMod lm, ModWindow w)
    {
        string? s = ModHost.Active.Settings.Read(lm.Id, Key(w, "Place"));
        var parts = (s ?? "").Split(',');
        // four numbers are the place a top-level window was kept at before (screen pixels): no
        // use in a frame, so the window opens in the middle of it
        if (parts.Length != 5) return (null, 0);
        var n = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i])) return (null, 0);
        if (n[2] < 50 || n[3] < 50) return (null, 0);
        int state = n[4] == 1 || n[4] == 2 ? (int)n[4] : 0;
        return (new Rect(n[0], n[1], n[2], n[3]), state);
    }

    /// <summary>The first place, before one is saved: the middle of the frame area.</summary>
    private static Rect Centred(MdiHost host)
    {
        double hw = host.Bounds.Width, hh = host.Bounds.Height;
        double x = hw > 1 ? Math.Max(0, (hw - DefaultWidth) / 2) : 40, y = hh > 1 ? Math.Max(0, (hh - DefaultHeight) / 2) : 40;
        return new Rect(x + host.ScrollOffset.X, y + host.ScrollOffset.Y, DefaultWidth, DefaultHeight);
    }

    /// <summary>
    /// A place held inside the frame area there is now, the part of it in view: the whole window
    /// in it (a frame made smaller since), and no larger than it. An area not laid out yet holds it
    /// to nothing (<see cref="PlaceIn"/> waits for its size).
    /// </summary>
    private static Rect Fit(MdiHost host, Rect r)
    {
        double hw = host.Bounds.Width, hh = host.Bounds.Height;
        double w = Math.Max(200, r.Width), h = Math.Max(120, r.Height);
        if (hw <= 1 || hh <= 1) return new Rect(r.X, r.Y, w, h);
        w = Math.Min(w, Math.Max(200, hw));
        h = Math.Min(h, Math.Max(120, hh));
        var o = host.ScrollOffset;
        double x = Math.Clamp(r.X, o.X, o.X + Math.Max(0, hw - w));
        double y = Math.Clamp(r.Y, o.Y, o.Y + Math.Max(0, hh - h));
        return new Rect(x, y, w, h);
    }

    private static void SavePlace(LoadedMod lm, ModWindow w, MdiChild child)
    {
        var p = child.Placement();
        if (double.IsNaN(p.X) || double.IsNaN(p.Y) || p.Width < 50 || p.Height < 50) return;
        int state = child.Maximised ? 2 : child.Minimised ? 1 : 0;
        string place = string.Join(",", new[] { p.X, p.Y, p.Width, p.Height, state }
            .Select(v => v.ToString("0", CultureInfo.InvariantCulture)));
        if (ModHost.Active.Settings.Read(lm.Id, Key(w, "Place")) != place)
            ModHost.Active.Settings.Write(lm.Id, Key(w, "Place"), place);
    }

    /// <summary>
    /// The toolbar's buttons for the mods' windows, after its icons: one per window offered now,
    /// which opens it in the frame the toolbar is in or, open, closes it. None when no mod offers a window.
    /// </summary>
    public static void AddButtons(Panel icons)
    {
        var host = ModHost.Active;
        var made = new List<(LoadedMod Mod, ModWindow Window, ToggleButton Button)>();
        foreach (var (lm, w) in host.Windows)
        {
            // in the toolbar's own readouts' face (lblGold's, the "NonPro" role, UiFonts)
            var b = UiFonts.Set(new ToggleButton
            {
                Content = w.Title,
                Focusable = false,            // the keyboard belongs to the game
                Height = 26,
                Padding = new Thickness(6, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brush.Parse("#2a2a38"),
                Margin = new Thickness(4, 0, 0, 0),
            }, DesignFonts.Main, "lblGold");
            b.Click += (_, _) =>
            {
                if (IsOpen(lm, w)) Hide(lm, w);
                else if (Area() is MdiHost area) Show(lm, w, area);
                Refresh();
            };
            icons.Children.Add(b);
            made.Add((lm, w, b));
        }
        if (made.Count == 0) return;

        // the frame area beside this toolbar, while its window is open: the toolbar and the area
        // are the town's, or the dungeon's put into the same window (DungeonWindow.RunInside)
        MdiHost? Area()
        {
            if (TopLevel.GetTopLevel(icons) is not Window f || IsClosed(f)) return null;
            var root = icons.FindAncestorOfType<Toolbar>()?.Parent as Visual;
            return root?.GetVisualDescendants().OfType<MdiHost>().FirstOrDefault() ?? HostIn(f);
        }

        void Refresh()
        {
            foreach (var (lm, w, b) in made)
            {
                b.IsVisible = host.IsShown(lm, w);
                b.IsChecked = IsOpen(lm, w);
            }
        }
        void SyncAndRefresh()
        {
            if (Area() is MdiHost area) Sync(area);
            Refresh();
        }
        Action settings = () => Avalonia.Threading.Dispatcher.UIThread.Post(SyncAndRefresh);
        Action opened = () => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        void Unsubscribe() { host.Changed -= settings; Changed -= opened; }
        icons.AttachedToVisualTree += (_, _) =>
        {
            host.Changed -= settings; host.Changed += settings;
            Changed -= opened; Changed += opened;
            // a frame closed without its toolbar leaving the visual tree first must not keep
            // these: a later settings change would open a mod window in a closed frame
            if (TopLevel.GetTopLevel(icons) is Window f)
            {
                Watch(f);
                f.Closed += (_, _) => Unsubscribe();
            }
            // the windows left open last time open again once the frame is up, and those open in
            // the other frame area (the town's, the dungeon's) come into this one
            Avalonia.Threading.Dispatcher.UIThread.Post(SyncAndRefresh, Avalonia.Threading.DispatcherPriority.Background);
        };
        icons.DetachedFromVisualTree += (_, _) => Unsubscribe();
        Refresh();
    }
}
