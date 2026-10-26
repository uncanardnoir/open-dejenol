using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mordor.Mod.Kaizen;

/// <summary>What a buff row does: casts one of the owner's spells, or uses an item in their pack.</summary>
public enum BuffKind { Spell, Item }

/// <summary>
/// One row of a character's buff list. The caster is the character the list belongs to.
/// <paramref name="Name"/> is the spell's name, or the item's (as the pack names it known at All);
/// <paramref name="Target"/> a party member's name for a spell that asks "Cast on Who?", or empty
/// for a party-wide one.
/// </summary>
public sealed record BuffRow(BuffKind Kind, string Name, string Target);

/// <summary>
/// Every character's buff list, kept by the game in <c>kaizen.json</c> in the saves folder (beside
/// settings.ini), keyed by the character's name:
///
/// <code>
/// {
///   "Version": 1,
///   "Characters": {
///     "Cas": [
///       { "Kind": "Spell", "Name": "Protection", "Target": "Oto" },
///       { "Kind": "Item", "Name": "Rod of Light", "Target": "" }
///     ]
///   }
/// }
/// </code>
///
/// Read once when the mod loads; written whole each time a list changes (the Kaizen window), and a
/// character's list dropped when the character is deleted. A file that cannot be read is moved
/// aside at once, to <c>kaizen.json.bad-yyyyMMdd-HHmmss</c> beside it, and the lists start empty
/// (the mod's log says so); one that cannot even be moved is never written over that session. No
/// Avalonia here, so the probe can check it.
/// </summary>
public sealed class BuffLists
{
    public const string FileName = "kaizen.json";

    private readonly Dictionary<string, List<BuffRow>> _lists = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string, Exception>? _error;

    /// <param name="path">The file; null keeps the lists in memory only.</param>
    /// <param name="error">Where a failure to read or write the file is reported.</param>
    public BuffLists(string? path, Action<string, Exception>? error = null)
    {
        Path = path;
        _error = error;
        Load();
    }

    public string? Path { get; }

    /// <summary>A list was changed (by <see cref="Set"/>).</summary>
    public event Action? Changed;

    private static string Key(string character) => (character ?? "").Trim();

    /// <summary>The characters that have a list, as the file names them.</summary>
    public IEnumerable<string> Characters => _lists.Keys;

    /// <summary>A character's list, in order (empty when they have none). Names are matched without regard to case or surrounding spaces.</summary>
    public IReadOnlyList<BuffRow> For(string character) =>
        _lists.TryGetValue(Key(character), out var l) ? l.ToList() : new List<BuffRow>();

    /// <summary>Replace a character's list and write the file. An empty list takes the character out.</summary>
    public void Set(string character, IEnumerable<BuffRow> rows)
    {
        string key = Key(character);
        if (key == "") return;
        var list = rows.Select(r => new BuffRow(r.Kind, (r.Name ?? "").Trim(), (r.Target ?? "").Trim())).ToList();
        if (list.Count == 0) _lists.Remove(key);
        else _lists[key] = list;
        Save();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------------------------- the file

    private sealed class FileShape
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, List<RowShape>> Characters { get; set; } = new();
    }

    private sealed class RowShape
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BuffKind Kind { get; set; }
        public string Name { get; set; } = "";
        public string Target { get; set; } = "";
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
        _lists.Clear();
        if (Path == null || !File.Exists(Path)) return;
        try
        {
            var shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(Path), Options);
            foreach (var (name, rows) in shape?.Characters ?? new())
            {
                string key = Key(name);
                if (key == "" || rows == null) continue;
                _lists[key] = rows.Where(r => r != null)
                                  .Select(r => new BuffRow(r.Kind, (r.Name ?? "").Trim(), (r.Target ?? "").Trim())).ToList();
            }
        }
        catch (Exception ex)
        {
            _lists.Clear();
            // moved aside before anything can write over it: the next change writes a new file
            string aside = Path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                File.Move(Path, aside);
                MovedAside = aside;
                _error?.Invoke($"{Path} could not be read; it was moved aside to {aside} and the buff lists start empty", ex);
            }
            catch (Exception moveEx)
            {
                // it cannot be moved: nothing is written over it this session
                _readOnly = true;
                _error?.Invoke($"{Path} could not be read, nor moved aside ({moveEx.Message}); the buff lists start empty and nothing is written to it this session", ex);
            }
        }
    }

    /// <summary>Where a file that could not be read was moved to, or null.</summary>
    public string? MovedAside { get; private set; }

    /// <summary>A file that could not be read nor moved aside is never written over.</summary>
    private bool _readOnly;

    private void Save()
    {
        if (Path == null || _readOnly) return;
        var shape = new FileShape
        {
            Characters = _lists.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => p.Value.Select(r => new RowShape { Kind = r.Kind, Name = r.Name, Target = r.Target }).ToList()),
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

/// <summary>
/// The Kaizen window's edits of one list, by row index (each a new list, the given one untouched; an
/// index out of range changes nothing). Kept apart from the window, without Avalonia, so the probe can
/// check them; the window writes the result with <see cref="BuffLists.Set"/>.
/// </summary>
public static class BuffEdits
{
    /// <summary>A dropped row, at <paramref name="at"/> (0 the top; past the end, or below 0, at the end).</summary>
    public static List<BuffRow> Insert(IReadOnlyList<BuffRow> rows, int at, BuffRow row)
    {
        var list = rows.ToList();
        list.Insert(at < 0 || at > list.Count ? list.Count : at, row);
        return list;
    }

    /// <summary>
    /// A row dragged within the window: row <paramref name="from"/> let go at the gap
    /// <paramref name="gap"/> (0 above the first row, the row count below the last). The gaps either
    /// side of the row itself leave it where it is.
    /// </summary>
    public static List<BuffRow> Move(IReadOnlyList<BuffRow> rows, int from, int gap)
    {
        var list = rows.ToList();
        if (from < 0 || from >= list.Count || gap < 0 || gap > list.Count || gap == from || gap == from + 1) return list;
        var row = list[from];
        list.RemoveAt(from);
        list.Insert(gap > from ? gap - 1 : gap, row);
        return list;
    }

    /// <summary>
    /// The gap a drop goes in once its "Cast on Who?" box has closed: the gap it was let go at, while
    /// the list is still the one it was measured on (<paramref name="before"/>); the end when the list
    /// changed meanwhile (another character in command and back, a row removed by hand), or when the
    /// gap is out of range.
    /// </summary>
    public static int GapAfter(IReadOnlyList<BuffRow> before, IReadOnlyList<BuffRow> now, int gap) =>
        before.SequenceEqual(now) && gap >= 0 && gap <= now.Count ? gap : now.Count;

    /// <summary>A row dragged out of the window, or removed from its right-click menu.</summary>
    public static List<BuffRow> Remove(IReadOnlyList<BuffRow> rows, int at)
    {
        var list = rows.ToList();
        if (at >= 0 && at < list.Count) list.RemoveAt(at);
        return list;
    }

    /// <summary>A row's target answered again ("Cast on Who?" from a double-click).</summary>
    public static List<BuffRow> Retarget(IReadOnlyList<BuffRow> rows, int at, string target)
    {
        var list = rows.ToList();
        if (at >= 0 && at < list.Count) list[at] = list[at] with { Target = (target ?? "").Trim() };
        return list;
    }
}
