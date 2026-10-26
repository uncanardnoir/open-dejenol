using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// The order the characters are listed in. The original keeps every character as a record of
/// MDATA4.MDR and lists them by walking the records, skipping the blank ones
/// (<c>mdrmazGetChars</c>, MAZEMOD.BAS:203-224), so the Character List, the Load picker and the
/// Exchange picker all show creation order. A deleted character's record is only blanked
/// (<c>sub2D18</c>, FRMMENU.BAS:191-192), and the next new character goes into the first blank
/// record it finds, or on the end when there is none (<c>fn04ED</c>, MAINMOD.BAS:384-402).
///
/// The port keeps one file per character, which has no order of its own, so this file stands in
/// for the record order: one name per record, and "" for a blanked one. Characters it does not
/// know - saves from before it existed - follow in the order the caller found them.
/// </summary>
public static class Roster
{
    private static string PathFor(string savesDir) => Path.Combine(savesDir, "roster.json");

    /// <summary>The records as stored, "" for a blank one. Missing or unreadable is empty.</summary>
    public static List<string> Records(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p)) return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(p)) ?? new();
        }
        catch { }
        return new();
    }

    private static void Write(string savesDir, List<string> records)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            File.WriteAllText(PathFor(savesDir), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.TrimEnd(), b.TrimEnd(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="found"/> put in record order: the ones the roster names first, in its
    /// order, and then the rest as they came.
    /// </summary>
    public static List<T> Order<T>(string savesDir, IEnumerable<T> found, Func<T, string> nameOf)
    {
        var records = Records(savesDir);
        var left = found.ToList();
        var ordered = new List<T>();
        foreach (string r in records)
        {
            if (r.Trim() == "") continue;
            int i = left.FindIndex(x => Same(nameOf(x), r));
            if (i < 0) continue;
            ordered.Add(left[i]);
            left.RemoveAt(i);
        }
        ordered.AddRange(left);
        return ordered;
    }

    /// <summary>
    /// A new character's record: the first blank one, or a new one on the end (fn04ED). The
    /// characters already on disk that the roster does not know are written in first, in the
    /// order given, so the order they were being shown in does not change under them.
    /// </summary>
    public static void Added(string savesDir, string name, IEnumerable<string> existing)
    {
        var records = Records(savesDir);
        foreach (string e in existing)
            if (!Same(e, name) && e.Trim() != "" && !records.Any(r => Same(r, e))) records.Add(e.TrimEnd());
        if (records.Any(r => Same(r, name))) { Write(savesDir, records); return; }
        int hole = records.FindIndex(r => r.Trim() == "");
        if (hole >= 0) records[hole] = name.TrimEnd();
        else records.Add(name.TrimEnd());
        Write(savesDir, records);
    }

    /// <summary>
    /// Every saved character with its file, in record order - the port's MorgueList. Files the
    /// roster does not know follow in file-name order, which is how the menu listed them before.
    /// </summary>
    public static List<(string File, Character Ch)> Saved(string savesDir)
    {
        var found = new List<(string, Character)>();
        if (!Directory.Exists(savesDir)) return found;
        foreach (string f in Directory.GetFiles(savesDir, "*.json").OrderBy(f => f))
        {
            if (Path.GetFileName(f).StartsWith("party-")) continue;
            try
            {
                var c = Character.Load(f);
                if (!string.IsNullOrWhiteSpace(c.Name)) found.Add((f, c));
            }
            catch { }
        }
        return Order(savesDir, found, p => p.Item2.Name);
    }

    /// <summary>A deleted character's record is blanked, not removed (sub2D18).</summary>
    public static void Removed(string savesDir, string name, IEnumerable<string> existing)
    {
        var records = Records(savesDir);
        foreach (string e in existing)
            if (e.Trim() != "" && !records.Any(r => Same(r, e))) records.Add(e.TrimEnd());
        for (int i = 0; i < records.Count; i++)
            if (Same(records[i], name)) records[i] = "";
        Write(savesDir, records);
    }
}
