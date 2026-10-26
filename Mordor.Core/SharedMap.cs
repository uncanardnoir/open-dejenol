using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// The automap, which the original shares between every character in the game: "The nice thing is
/// that it's shared. If one character goes down and completely maps level 1, then all of the other
/// characters in the game have access to the same Map." So it lives next to the saves, not inside
/// a character file.
/// </summary>
public sealed class SharedMap
{
    /// <summary>Dungeon level → explored cell indices (y * width + x).</summary>
    public Dictionary<int, List<int>> Explored { get; set; } = new();

    /// <summary>
    /// What each square has been found to *be*. The original keeps this on the automap itself -
    /// <c>fn0ABD(x, y, flags)</c> at MAZEMOD.BAS:637 writes the discovered flag back - which is
    /// how it knows not to tell you a second time about a rotator you have already worked out.
    /// Keyed "level:index", holding the flag bits noticed there.
    /// </summary>
    public Dictionary<string, List<int>> Noticed { get; set; } = new();

    /// <summary>
    /// What the automap has been told each square holds - the original's per-square flag word,
    /// which is the only thing its map is ever drawn from. Keyed "level:index"; a bit set here
    /// means the square has been *recorded* as carrying that flag, which is not the same as the
    /// maze carrying it. See <see cref="MapRecord"/> for what goes in and what stays out.
    /// </summary>
    public Dictionary<string, long> Seen { get; set; } = new();

    private static string SeenKey(int level, int x, int y, int width) => $"{level}:{y * width + x}";

    /// <summary>Everything the map has been told about this square.</summary>
    public long SeenFlags(int level, int x, int y, int width) =>
        Seen.TryGetValue(SeenKey(level, x, y, width), out long f) ? f : 0L;

    /// <summary>Whether the map has this square down as carrying that flag.</summary>
    public bool Recorded(int level, int x, int y, int width, int bit) =>
        (SeenFlags(level, x, y, width) & (1L << bit)) != 0;

    /// <summary>Write a flag into the map for this square.</summary>
    public void Record(int level, int x, int y, int width, int bit) =>
        Seen[SeenKey(level, x, y, width)] = SeenFlags(level, x, y, width) | (1L << bit);

    /// <summary>
    /// Take a flag back out again. The only thing that does this is finding a secret door, which
    /// clears the wall the map had been showing in its place (MAZEMOD.BAS:1597).
    /// </summary>
    public void Unrecord(int level, int x, int y, int width, int bit)
    {
        string k = SeenKey(level, x, y, width);
        if (Seen.TryGetValue(k, out long f)) Seen[k] = f & ~(1L << bit);
    }


    /// <summary>Whether anything at all has been written down for this level.</summary>
    public bool HasRecordFor(int level)
    {
        string prefix = level + ":";
        foreach (var k in Seen.Keys) if (k.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }


    /// <summary>Whether this square is already known to carry that flag.</summary>
    public bool Knows(int level, int x, int y, int width, int bit) =>
        Noticed.TryGetValue($"{level}:{y * width + x}", out var bits) && bits.Contains(bit);

    /// <summary>Record that the square has been worked out to carry that flag.</summary>
    public void Note(int level, int x, int y, int width, int bit)
    {
        string key = $"{level}:{y * width + x}";
        if (!Noticed.TryGetValue(key, out var bits)) Noticed[key] = bits = new List<int>();
        if (!bits.Contains(bit)) bits.Add(bit);
    }

    private static string PathFor(string savesDir) => Path.Combine(savesDir, "automap.json");

    public static SharedMap Load(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p)) return JsonSerializer.Deserialize<SharedMap>(File.ReadAllText(p)) ?? new SharedMap();
        }
        catch { }
        return new SharedMap();
    }

    public void Save(string savesDir)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            File.WriteAllText(PathFor(savesDir), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public HashSet<(int x, int y)> CellsFor(int level, int width)
    {
        var set = new HashSet<(int, int)>();
        if (Explored.TryGetValue(level, out var cells))
            foreach (int i in cells) set.Add((i % width, i / width));
        return set;
    }

    /// <summary>Merge a level's explored cells in; the map only ever grows.</summary>
    public void Merge(int level, IEnumerable<(int x, int y)> cells, int width)
    {
        var have = Explored.TryGetValue(level, out var list) ? new HashSet<int>(list) : new HashSet<int>();
        foreach (var (x, y) in cells) have.Add(y * width + x);
        Explored[level] = have.OrderBy(v => v).ToList();
    }

    /// <summary>Deepest level anyone has visited; the original will not show you levels nobody has reached.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int DeepestVisited => Explored.Count == 0 ? 0 : Explored.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).DefaultIfEmpty(0).Max();

    /// <summary>Take a character's own map (from older saves) into the shared one, so nothing is lost.</summary>
    public void AbsorbLegacy(Character c, int width)
    {
        foreach (var (level, cells) in c.Explored)
            Merge(level, cells.Select(i => (i % width, i / width)), width);
    }
}
