using System.Text.Json;
using Mordor.Modding;

namespace Mordor.Mod.Cartographer;

/// <summary>
/// The four colours a square can be marked with: Tableau 10's first three and a neon purple (in place
/// of Tableau's teal, which read too close to the blue; a file's "Teal" loads as Purple).
/// </summary>
public enum MarkColour { Blue, Orange, Red, Purple }

/// <summary>The mod's colours (red, green, blue).</summary>
public static class Palette
{
    /// <summary>Tableau 10's blue, #4E79A7.</summary>
    public static readonly (byte R, byte G, byte B) Blue = (0x4E, 0x79, 0xA7);
    /// <summary>Tableau 10's orange, #F28E2B.</summary>
    public static readonly (byte R, byte G, byte B) Orange = (0xF2, 0x8E, 0x2B);
    /// <summary>Tableau 10's red, #E15759.</summary>
    public static readonly (byte R, byte G, byte B) Red = (0xE1, 0x57, 0x59);
    /// <summary>A neon purple, #BC13FE: apart from the blue, the red and the waypoint yellow at the 60% tint, on grey and on black.</summary>
    public static readonly (byte R, byte G, byte B) Purple = (0xBC, 0x13, 0xFE);
    /// <summary>A waypoint's source square: Tableau 10's yellow, #EDC948, apart from the four.</summary>
    public static readonly (byte R, byte G, byte B) Waypoint = (0xED, 0xC9, 0x48);
    /// <summary>A highlight: the waypoint under the pointer, the destination just gone to. White, framed dark by the game.</summary>
    public static readonly (byte R, byte G, byte B) Highlight = (0xFF, 0xFF, 0xFF);

    public static (byte R, byte G, byte B) Of(MarkColour c) => c switch
    {
        MarkColour.Blue => Blue,
        MarkColour.Orange => Orange,
        MarkColour.Red => Red,
        _ => Purple,
    };
}

/// <summary>
/// What the player has written on the map, kept per world in <c>cartographer.json</c> in the saves
/// folder (beside settings.ini and automap.json - the shared automap is the saves folder's, so the
/// notes are too): each square's colour, and each waypoint, a source square linked to one destination
/// square on any level.
///
/// <code>
/// {
///   "Version": 1,
///   "Colours": [ { "Level": 1, "X": 5, "Y": 7, "Colour": "Blue" } ],
///   "Waypoints": [ { "Level": 1, "X": 5, "Y": 7, "ToLevel": 3, "ToX": 12, "ToY": 2 } ]
/// }
/// </code>
///
/// X and Y in the file count from one, as the Automap's own readout does ("You are at 5,7,1"); in
/// memory they count from nought, as <see cref="AutomapSquare"/> does. Read once when the mod loads;
/// written whole (beside, then moved over) each time something changes. A file that cannot be read is
/// moved aside at once to <c>cartographer.json.bad-yyyyMMdd-HHmmss</c>, the notes start empty and the
/// mod's log says so; one that cannot even be moved is never written over that session. An entry that
/// makes no sense (a level below one, a square off the map, an unknown colour) is left out. No Avalonia
/// here, so the probe can check it.
/// </summary>
public sealed class MapNotes
{
    public const string FileName = "cartographer.json";

    private readonly Dictionary<AutomapSquare, MarkColour> _colours = new();
    private readonly Dictionary<AutomapSquare, AutomapSquare> _waypoints = new();
    private readonly Action<string, Exception>? _error;

    /// <param name="path">The file; null keeps the notes in memory only.</param>
    /// <param name="error">Where a failure to read or write the file is reported.</param>
    public MapNotes(string? path, Action<string, Exception>? error = null)
    {
        Path = path;
        _error = error;
        Load();
    }

    public string? Path { get; }

    /// <summary>Something was marked or unmarked.</summary>
    public event Action? Changed;

    public int ColourCount => _colours.Count;
    public int WaypointCount => _waypoints.Count;

    private static bool Valid(AutomapSquare s) => s.Level >= 1 && s.X >= 0 && s.Y >= 0;

    /// <summary>A square's colour, or null.</summary>
    public MarkColour? ColourAt(AutomapSquare s) => _colours.TryGetValue(s, out var c) ? c : null;

    /// <summary>Colour a square, or clear it (null), and write the file.</summary>
    public void SetColour(AutomapSquare s, MarkColour? colour)
    {
        if (!Valid(s)) return;
        if (colour is { } c) { if (ColourAt(s) == c) return; _colours[s] = c; }
        else if (!_colours.Remove(s)) return;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Where a waypoint from this square leads, or null.</summary>
    public AutomapSquare? WaypointFrom(AutomapSquare s) => _waypoints.TryGetValue(s, out var to) ? to : null;

    /// <summary>
    /// Link a source square to its one destination (replacing any it had), and write the file. A square
    /// is not linked to itself.
    /// </summary>
    public void SetWaypoint(AutomapSquare from, AutomapSquare to)
    {
        if (!Valid(from) || !Valid(to) || from == to || WaypointFrom(from) == to) return;
        _waypoints[from] = to;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Take a square's waypoint away, and write the file.</summary>
    public void RemoveWaypoint(AutomapSquare from)
    {
        if (!_waypoints.Remove(from)) return;
        Save();
        Changed?.Invoke();
    }

    /// <summary>The coloured squares of a level, south to north, then west to east.</summary>
    public IEnumerable<(AutomapSquare Square, MarkColour Colour)> ColoursOn(int level) =>
        _colours.Where(p => p.Key.Level == level).OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X).Select(p => (p.Key, p.Value)).ToList();

    /// <summary>The waypoints whose source is on a level, in the same order.</summary>
    public IEnumerable<(AutomapSquare From, AutomapSquare To)> WaypointsOn(int level) =>
        _waypoints.Where(p => p.Key.Level == level).OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X).Select(p => (p.Key, p.Value)).ToList();

    // ---------------------------------------------------------------------------------- the file

    private sealed class FileShape
    {
        public int Version { get; set; } = 1;
        public List<ColourShape> Colours { get; set; } = new();
        public List<WaypointShape> Waypoints { get; set; } = new();
    }

    private sealed class ColourShape
    {
        public int Level { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Colour { get; set; } = "";
    }

    private sealed class WaypointShape
    {
        public int Level { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int ToLevel { get; set; }
        public int ToX { get; set; }
        public int ToY { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private void Load()
    {
        _colours.Clear();
        _waypoints.Clear();
        if (Path == null || !File.Exists(Path)) return;
        try
        {
            var shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(Path), Options)
                        ?? throw new JsonException("the file holds no notes (null)");
            foreach (var c in shape.Colours ?? new())
            {
                if (c == null || !Enum.TryParse<MarkColour>(Renamed((c.Colour ?? "").Trim()), ignoreCase: true, out var colour)
                    || !Enum.IsDefined(colour) || int.TryParse(c.Colour, out _)) continue;
                var s = new AutomapSquare(c.Level, c.X - 1, c.Y - 1);
                if (Valid(s)) _colours[s] = colour;
            }
            foreach (var w in shape.Waypoints ?? new())
            {
                if (w == null) continue;
                var from = new AutomapSquare(w.Level, w.X - 1, w.Y - 1);
                var to = new AutomapSquare(w.ToLevel, w.ToX - 1, w.ToY - 1);
                if (Valid(from) && Valid(to) && from != to) _waypoints[from] = to;
            }
        }
        catch (Exception ex)
        {
            _colours.Clear();
            _waypoints.Clear();
            // moved aside before anything can write over it: the next change writes a new file
            string aside = Path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                File.Move(Path, aside);
                MovedAside = aside;
                _error?.Invoke($"{Path} could not be read; it was moved aside to {aside} and the map notes start empty", ex);
            }
            catch (Exception moveEx)
            {
                // it cannot be moved: nothing is written over it this session
                _readOnly = true;
                _error?.Invoke($"{Path} could not be read, nor moved aside ({moveEx.Message}); the map notes start empty and nothing is written to it this session", ex);
            }
        }
    }

    /// <summary>A colour's old name in the file: the fourth was "Teal" before it became Purple.</summary>
    private static string Renamed(string name) =>
        string.Equals(name, "Teal", StringComparison.OrdinalIgnoreCase) ? nameof(MarkColour.Purple) : name;

    /// <summary>Where a file that could not be read was moved to, or null.</summary>
    public string? MovedAside { get; private set; }

    /// <summary>A file that could not be read nor moved aside is never written over.</summary>
    private bool _readOnly;

    private void Save()
    {
        if (Path == null || _readOnly) return;
        var shape = new FileShape
        {
            Colours = _colours.OrderBy(p => p.Key.Level).ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                .Select(p => new ColourShape { Level = p.Key.Level, X = p.Key.X + 1, Y = p.Key.Y + 1, Colour = p.Value.ToString() }).ToList(),
            Waypoints = _waypoints.OrderBy(p => p.Key.Level).ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
                .Select(p => new WaypointShape
                {
                    Level = p.Key.Level, X = p.Key.X + 1, Y = p.Key.Y + 1,
                    ToLevel = p.Value.Level, ToX = p.Value.X + 1, ToY = p.Value.Y + 1,
                }).ToList(),
        };
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // written beside and moved over, so a failure half way leaves the last good file
            string temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(shape, Options));
            File.Move(temp, Path, overwrite: true);
        }
        catch (Exception ex) { _error?.Invoke($"{Path} could not be written", ex); }
    }
}
