using Mordor.Modding;

namespace Mordor.Mod.Cartographer;

/// <summary>
/// Cartographer: the player's own marks on the Automap - a colour on a square, and waypoints, each a
/// source square linked to one destination square on any level. Purely annotation: it reveals nothing
/// (any square of the map may be marked, explored or not, and nothing about the maze is read), changes
/// nothing in the game, and with the mod off the Automap is the original's.
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
/// </summary>
public sealed class CartographerMod : IMod, IModLifecycle
{
    /// <summary>Switched on again on the main menu: the hooks were wired once in <see cref="Load"/>.</summary>
    public void OnEnable() { }

    /// <summary>Switched off on the main menu: no map is open there; the hover and highlight are let go. The notes are written as they change.</summary>
    public void OnDisable() { Hovered = null; Highlighted = null; }

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

    public void Load(IModContext ctx)
    {
        void Report(string what, Exception ex) { try { ctx.Log.Error(what, ex); } catch { } }
        Notes = new MapNotes(System.IO.Path.Combine(ctx.DataDirectory, MapNotes.FileName), Report);
        ctx.UI.AutomapMenu = Menu;
        ctx.UI.AutomapMarks = Marks;
        ctx.UI.AutomapHover = (_, square) => Hovered = square;
        ctx.UI.AutomapClick = Click;
        ctx.Log.Info("ready");
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
        return items;
    }

    /// <summary>What is drawn on the level shown.</summary>
    public IReadOnlyList<AutomapMark> Marks(AutomapView view)
    {
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
