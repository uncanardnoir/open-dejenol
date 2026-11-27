using Mordor.Modding;

namespace Mordor.Mod.Cartographer;

/// <summary>
/// Cartographer: the player's own marks on the Automap - a colour on a square, and waypoints, each a
/// source square linked to one destination square on any level; and routes between squares. It reveals
/// nothing (any square of the map may be marked, explored or not; a route reads only what the Automap
/// itself draws, never the maze), changes nothing in the game, and with the mod off the Automap is the
/// original's.
///
/// - A right-click on a square opens the Automap's menu (<see cref="ModUI.AutomapMenu"/>): first the
///   original's right-click, "Redraw/Refresh Level" (the game puts it there), then Blue, Orange, Red,
///   Purple, No colour, and Add waypoint... - or Change waypoint... and Remove waypoint for a square that
///   has one. Add and Change ask the player to pick the destination (<see cref="AutomapView.PickSquare"/>):
///   the level bar pages the map, a click on a square takes it, Escape or a right-click gives up.
/// - The marks (<see cref="ModUI.AutomapMarks"/>): a coloured square tinted; a waypoint's source tinted
///   yellow (when it has no colour of its own) and framed yellow. The destination is not marked.
/// - The pointer over a waypoint's source frames it white, and when its destination is on the level
///   shown, an arrow runs from it to the destination (<see cref="ModUI.AutomapHover"/>).
/// - A click on a waypoint's source pages the map to the destination's level and frames the
///   destination white, until the next click on the map (<see cref="ModUI.AutomapClick"/>).
///
/// The notes are kept per world in cartographer.json (<see cref="MapNotes"/>).
///
/// Routes (1.1.0): the menu's "Route to here" asks for the fewest actions from where the party stands,
/// "Route from here..." from the square clicked to a square picked after it (<see cref="RoutePlanner"/>,
/// which reads the Automap's own record and nothing of the maze). The route is drawn in green - an arrow
/// along each straight run, the end framed bold - and the Automap's status line says what it costs. A
/// route to here follows the party as it walks; "Clear route" takes it away. "Route settings..." opens a
/// small window (<see cref="RouteSettingsWindow"/>) choosing whether waypoints are taken, what a cast of
/// Ethereal Portal counts (never cast unless given a number), and what each hazard counts - a whole
/// number, or Avoid.
/// </summary>
public sealed class CartographerMod : IMod, IModLifecycle
{
    /// <summary>Switched on again on the main menu: the hooks were wired once in <see cref="Load"/>.</summary>
    public void OnEnable() { }

    /// <summary>
    /// Switched off on the main menu: no map is open there; the hover, highlight and route are let go, and
    /// the route settings window closed. The notes and settings are written as they change.
    /// </summary>
    public void OnDisable()
    {
        Hovered = null; Highlighted = null; RouteTo = null; RouteFrom = null;
        if (_windowOpened) try { Draw.Close(); } catch { }
    }

    /// <summary>Whether the route settings window has been opened this run (closing it loads Avalonia).</summary>
    private bool _windowOpened;

    /// <summary>The notes (cartographer.json in the saves folder).</summary>
    public MapNotes Notes { get; private set; } = new(null);

    /// <summary>The square under the pointer, as the Automap last said.</summary>
    public AutomapSquare? Hovered { get; private set; }

    /// <summary>The destination a click on a waypoint went to, framed until the next click.</summary>
    public AutomapSquare? Highlighted { get; private set; }

    public const string NoColour = "No colour";
    public const string AddWaypoint = "Add waypoint...";
    public const string ChangeWaypoint = "Change waypoint...";
    public const string RemoveWaypoint = "Remove waypoint";
    /// <summary>The Automap's status line while the destination is picked.</summary>
    public const string PickPrompt = "Click the destination square (any level). Esc cancels.";

    public const string RouteHere = "Route to here";
    public const string RouteFromHere = "Route from here...";
    public const string ClearRoute = "Clear route";
    /// <summary>The Automap's status line while a route's destination is picked.</summary>
    public const string RoutePickPrompt = "Click where the route goes (any level). Esc cancels.";

    public const string RouteSettings = "Route settings...";

    /// <summary>The settings' keys (settings.ini): whether waypoints are taken, and each hazard's extra.</summary>
    public const string UseWaypointsKey = "RouteWaypoints";
    public static string ExtraKey(Hazard h) => h + "Extra";
    /// <summary>A hazard's stored value for "never onto it".</summary>
    public const string Avoid = "Avoid";
    /// <summary>The most extra a hazard can count (the window's box takes three digits).</summary>
    public const int MaxExtra = 999;

    /// <summary>
    /// A hazard's extra from what settings.ini holds: null for Avoid, else a whole number from 0 to
    /// <see cref="MaxExtra"/> ("+2" and "None", as 1.1.0's first settings wrote them, read as 2 and 0);
    /// nothing or anything else is 0.
    /// </summary>
    public static int? ParseExtra(string? stored)
    {
        string s = (stored ?? "").Trim();
        if (string.Equals(s, Avoid, StringComparison.OrdinalIgnoreCase)) return null;
        return int.TryParse(s.TrimStart('+'), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n)
            ? Math.Min(n, MaxExtra) : 0;
    }

    /// <summary>Set a hazard's extra (null: Avoid), written at once.</summary>
    public void SetExtra(Hazard h, int? extra) =>
        _settings?.SetText(ExtraKey(h), extra is { } n ? Math.Clamp(n, 0, MaxExtra).ToString(System.Globalization.CultureInfo.InvariantCulture) : Avoid);

    /// <summary>The key for what an Ethereal Portal counts; <see cref="PortalOff"/> (the default) for never.</summary>
    public const string PortalCostKey = "PortalCost";
    public const string PortalOff = "Off";

    /// <summary>What a cast of Ethereal Portal counts from what settings.ini holds: null (never cast) for nothing, Off or anything not a whole number.</summary>
    public static int? ParsePortalCost(string? stored) =>
        int.TryParse((stored ?? "").Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n)
            ? Math.Min(n, MaxExtra) : null;

    /// <summary>Set what a cast of Ethereal Portal counts (null: never cast), written at once.</summary>
    public void SetPortalCost(int? cost) =>
        _settings?.SetText(PortalCostKey, cost is { } n ? Math.Clamp(n, 0, MaxExtra).ToString(System.Globalization.CultureInfo.InvariantCulture) : PortalOff);

    /// <summary>Set whether routes take waypoints, written at once.</summary>
    public void SetUseWaypoints(bool on) => _settings?.SetText(UseWaypointsKey, on ? "True" : "False");

    /// <summary>
    /// What "Route settings..." does: open the window (<see cref="RouteSettingsWindow"/>). A probe, with
    /// no Avalonia, puts its own here.
    /// </summary>
    public Action OpenRouteSettings { get; set; } = () => { };

    /// <summary>A route's destination, or null for none.</summary>
    public AutomapSquare? RouteTo { get; private set; }

    /// <summary>A route's start; null with a destination: from where the party stands, following it.</summary>
    public AutomapSquare? RouteFrom { get; private set; }

    private ModSettings? _settings;

    public void Load(IModContext ctx)
    {
        void Report(string what, Exception ex) { try { ctx.Log.Error(what, ex); } catch { } }
        Notes = new MapNotes(System.IO.Path.Combine(ctx.DataDirectory, MapNotes.FileName), Report);
        Notes.Changed += () => _notesVersion++;
        _settings = ctx.Settings;
        ctx.UI.AutomapMenu = Menu;
        ctx.UI.AutomapMarks = Marks;
        ctx.UI.AutomapHover = (_, square) => Hovered = square;
        ctx.UI.AutomapClick = Click;
        ctx.UI.AutomapInfo = Info;
        void ReportWindow(string what, Exception ex) { try { ctx.Log.Error(what, ex); } catch { } }
        OpenRouteSettings = () => { _windowOpened = true; Draw.Open(this, ReportWindow); };
        ctx.Log.Info("ready");
    }

    /// <summary>
    /// The way to <see cref="RouteSettingsWindow"/>, never inlined: the probes run the mod with no Avalonia,
    /// and only a method that is called loads it.
    /// </summary>
    private static class Draw
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void Open(CartographerMod mod, Action<string, Exception> report) => RouteSettingsWindow.Open(mod, report);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void Close() => RouteSettingsWindow.Close();
    }

    /// <summary>The rules a route is planned by now: the settings, or the defaults (waypoints taken, no extra).</summary>
    public RouteRules Rules()
    {
        var pen = new Dictionary<Hazard, int?>();
        foreach (var h in Enum.GetValues<Hazard>())
        {
            int? extra = ParseExtra(_settings?.GetText(ExtraKey(h)));
            if (extra is not 0) pen[h] = extra;
        }
        string use = _settings?.GetText(UseWaypointsKey, "True") ?? "True";
        return new RouteRules
        {
            UseWaypoints = !string.Equals(use.Trim(), "False", StringComparison.OrdinalIgnoreCase),
            Penalties = pen,
            PortalCost = ParsePortalCost(_settings?.GetText(PortalCostKey)),
        };
    }

    /// <summary>The menu's items for a square (after the game's own first item).</summary>
    public IReadOnlyList<AutomapMenuItem> Menu(AutomapView view, AutomapSquare square)
    {
        var notes = Notes;
        var items = new List<AutomapMenuItem>();
        foreach (var c in Enum.GetValues<MarkColour>())
            items.Add(new AutomapMenuItem(c.ToString(), () => notes.SetColour(square, c), Palette.Of(c)));
        items.Add(new AutomapMenuItem(NoColour, () => notes.SetColour(square, null), IsEnabled: notes.ColourAt(square) != null));
        items.Add(AutomapMenuItem.Separator);
        void Pick() => view.PickSquare(PickPrompt, to => { if (to != null) notes.SetWaypoint(square, to); });
        if (notes.WaypointFrom(square) == null)
            items.Add(new AutomapMenuItem(AddWaypoint, Pick, Palette.Waypoint));
        else
        {
            items.Add(new AutomapMenuItem(ChangeWaypoint, Pick, Palette.Waypoint));
            items.Add(new AutomapMenuItem(RemoveWaypoint, () => notes.RemoveWaypoint(square)));
        }
        items.Add(AutomapMenuItem.Separator);
        // from where the party stands: only while the map knows that (not lost, not in town)
        // back to the party's own level once it is asked (the owner, 2026-11-27): the target may have been
        // picked on another level, and the route starts where the party stands
        items.Add(new AutomapMenuItem(RouteHere, () => { RouteFrom = null; RouteTo = square; if (view.You is { } here) view.ShowLevel(here.Square.Level); }, Palette.Route,
                                      IsEnabled: view.You is { } you && you.Square != square));
        items.Add(new AutomapMenuItem(RouteFromHere,
            () => view.PickSquare(RoutePickPrompt, to => { if (to != null) { RouteFrom = square; RouteTo = to; } }), Palette.Route));
        if (RouteTo != null) items.Add(new AutomapMenuItem(ClearRoute, () => { RouteTo = null; RouteFrom = null; }));
        items.Add(new AutomapMenuItem(RouteSettings, () => OpenRouteSettings()));
        return items;
    }

    // ------------------------------------------------------------------------------- the route

    private int _notesVersion;

    /// <summary>The route last worked out, what it was worked out for, and its status line.</summary>
    private (string Key, Route? Route, string? Line)? _last;

    /// <summary>"12,5,3": a square as the Automap's readout counts it, from one.</summary>
    public static string Where(AutomapSquare s) => $"{s.X + 1},{s.Y + 1},{s.Level}";

    private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

    /// <summary>
    /// The route as things stand: from the party (as the map shows it) or the chosen square, to the
    /// destination, by the settings now. Worked out afresh for the marks (the map has changed or the party
    /// moved); the status line, asked straight after, has the same.
    /// </summary>
    private (Route? Route, string? Line) Work(AutomapView view, bool fresh)
    {
        if (RouteTo is not { } to) return (null, null);
        var rules = Rules();
        var you = RouteFrom == null ? view.You : null;
        string key = $"{RouteFrom}|{you}|{to}|{rules.UseWaypoints}|{rules.PortalCost}|{string.Join(",", rules.Penalties.Select(p => $"{p.Key}{p.Value}"))}|{_notesVersion}|{view.DeepestLevel}";
        if (!fresh && _last is { } l && l.Key == key) return (l.Route, l.Line);

        Route? route = null;
        string line;
        string toText = Where(to);
        if (RouteFrom == null && you == null)
            line = $"Route to {toText}: where you are is not known.";
        else
        {
            var from = RouteFrom ?? you!.Square;
            route = RoutePlanner.Plan(view.LevelSize, view.DeepestLevel, view.Record, Notes.Waypoints, rules, from, you?.Facing, to);
            string head = RouteFrom == null ? $"Route to {toText}" : $"Route {Where(from)} to {toText}";
            if (route == null) line = RouteFrom == null ? $"No known route to {toText}." : $"No known route from {Where(from)} to {toText}.";
            else if (route.Actions == 0) line = RouteFrom == null ? $"{head}: you are there." : $"{head}: 0 actions.";
            else
            {
                // the owner (2026-11-27): just "Route to x,y,z: w actions" - the breakdown (steps, turns,
                // stairs, portals, waypoints, hazards) and the caveats about fog and unexplored squares were
                // more than the status line wants; the route itself is still drawn on the map
                line = $"{head}: {Count(route.Actions, "action", "actions")}";
            }
        }
        _last = (key, route, line);
        return (route, line);
    }

    /// <summary>The Automap's status line while a route is shown (<see cref="ModUI.AutomapInfo"/>).</summary>
    public string? Info(AutomapView view) { Arrived(view); return Work(view, fresh: false).Line; }

    /// <summary>
    /// A route to here is done once the party stands on its square: it clears itself (the owner,
    /// 2026-11-27). A route from one chosen square to another is a plan, not a walk, and stays.
    /// </summary>
    private void Arrived(AutomapView view)
    {
        if (RouteFrom == null && RouteTo is { } to && view.You is { } you && you.Square == to) { RouteTo = null; _last = null; }
    }

    /// <summary>
    /// A route's marks on the level shown: an arrow along each straight run of steps, the start framed, the
    /// end framed bold, and both ends of a stairs or waypoint move framed.
    /// </summary>
    private static void RouteMarks(Route route, int level, List<AutomapMark> marks)
    {
        var n = route.Nodes;
        (int, int) Delta(int a, int b) => (n[b].Square.X - n[a].Square.X, n[b].Square.Y - n[a].Square.Y);
        bool StepOn(int b) => n[b].How == RouteMove.Step && n[b].Square.Level == level;
        int k = 0;
        while (k + 1 < n.Count)
        {
            if (n[k].Square.Level != level || !StepOn(k + 1)) { k++; continue; }
            var dir = Delta(k, k + 1);
            int j = k + 1;
            while (j + 1 < n.Count && StepOn(j + 1) && Delta(j, j + 1) == dir) j++;
            marks.Add(AutomapMark.Arrow(n[k].Square.X, n[k].Square.Y, n[j].Square.X, n[j].Square.Y, Palette.Route));
            k = j;
        }
        // a portal through the wall beside: its own arrow (one wrapped round the level's edge is its two frames)
        for (int i = 1; i < n.Count; i++)
        {
            var (a, b) = (n[i - 1].Square, n[i].Square);
            if (n[i].How == RouteMove.Portal && a.Level == level && b.Level == level && Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y) == 1)
                marks.Add(AutomapMark.Arrow(a.X, a.Y, b.X, b.Y, Palette.Route));
        }
        for (int i = 0; i < n.Count; i++)
        {
            var s = n[i].Square;
            bool jump = n[i].How is RouteMove.Stairs or RouteMove.Waypoint or RouteMove.Portal;
            bool leaves = i + 1 < n.Count && n[i + 1].How is RouteMove.Stairs or RouteMove.Waypoint or RouteMove.Portal;
            if (s.Level != level || !(i == 0 || i == n.Count - 1 || jump || leaves)) continue;
            marks.Add(AutomapMark.Frame(s.X, s.Y, Palette.Route, bold: i == n.Count - 1));
        }
    }

    /// <summary>What is drawn on the level shown.</summary>
    public IReadOnlyList<AutomapMark> Marks(AutomapView view)
    {
        Arrived(view);
        int level = view.ShownLevel;
        var marks = new List<AutomapMark>();
        foreach (var (s, c) in Notes.ColoursOn(level)) marks.Add(AutomapMark.Tint(s.X, s.Y, Palette.Of(c)));
        foreach (var (from, _) in Notes.WaypointsOn(level))
        {
            if (Notes.ColourAt(from) == null) marks.Add(AutomapMark.Tint(from.X, from.Y, Palette.Waypoint));
            marks.Add(AutomapMark.Frame(from.X, from.Y, Palette.Waypoint));
        }
        if (Hovered is { } h && h.Level == level && Notes.WaypointFrom(h) is { } to)
        {
            marks.Add(AutomapMark.Frame(h.X, h.Y, Palette.Highlight, bold: true));
            if (to.Level == level) marks.Add(AutomapMark.Arrow(h.X, h.Y, to.X, to.Y, Palette.Waypoint));
        }
        if (Highlighted is { } d && d.Level == level) marks.Add(AutomapMark.Frame(d.X, d.Y, Palette.Highlight, bold: true));
        if (Work(view, fresh: true).Route is { } route) RouteMarks(route, level, marks);
        return marks;
    }

    /// <summary>
    /// A click on the map: a highlight goes; on a waypoint's source, the map goes to the destination's
    /// level and the destination is highlighted.
    /// </summary>
    public bool Click(AutomapView view, AutomapSquare square)
    {
        bool had = Highlighted != null;
        Highlighted = null;
        if (Notes.WaypointFrom(square) is not { } to) return had;
        view.ShowLevel(to.Level);
        Highlighted = to;
        return true;
    }
}
