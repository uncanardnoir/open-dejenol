namespace Mordor.Core;

/// <summary>
/// Where the port's loose files go - the crash log and CHARSTAT.TXT - so that each lands somewhere
/// it can be written (docs/GAME_FILES.md, "Files the port writes"). Everything else the port writes
/// already follows the saves folder (<see cref="Saves.DirFor"/>, which falls back to the per-user
/// folder) or the per-user art cache.
///
/// Each file has a list of places, best first, and is appended to the first that takes it. The
/// first place is always where the file went before this list existed, for a setup that could
/// write there, so nothing moves for an existing setup:
/// <list type="bullet">
/// <item><c>crash.log</c>: beside the saves folder in use (for <c>&lt;game&gt;\..\saves</c> that is
///   <c>&lt;game&gt;\..\crash.log</c>, where it always was), then the per-user folder
///   <c>%APPDATA%\OpenDejenol</c>, then the temporary folder.</item>
/// <item><c>CHARSTAT.TXT</c>: beside the saves folder when the saves are kept outside the per-user
///   folder (again <c>&lt;game&gt;\..\CHARSTAT.TXT</c>, where the port has always put it), then the
///   game folder - the original's "Root Mordor directory" - then the per-user folder. DEVIATION, a
///   port necessity: the original wrote only into its own directory, which under Program Files a
///   player cannot write to.</item>
/// </list>
/// </summary>
public static class PortFiles
{
    public const string CrashLogName = "crash.log";

    /// <summary><c>%APPDATA%\OpenDejenol</c>: the per-user folder, holding the launch settings and the per-user saves.</summary>
    public static string PerUserRoot => Path.GetDirectoryName(Saves.PerUserDir) ?? Saves.PerUserDir;

    /// <summary>The folder that holds this saves folder, or null when it cannot be told.</summary>
    public static string? BesideSaves(string savesDir)
    {
        try { return Path.GetDirectoryName(Path.GetFullPath(savesDir)); }
        catch { return null; }
    }

    /// <summary>
    /// The crash log's places, best first. <paramref name="savesDir"/> is null before the game
    /// folder is known (very early in start-up), when only the per-user and temporary ones remain.
    /// </summary>
    public static List<string> CrashLogPlaces(string? savesDir, string perUserRoot)
    {
        var l = new List<string>();
        if (savesDir != null && BesideSaves(savesDir) is { Length: > 0 } b) l.Add(Path.Combine(b, CrashLogName));
        l.Add(Path.Combine(perUserRoot, CrashLogName));
        l.Add(Path.Combine(Path.GetTempPath(), "mordor-crash.log"));
        return Distinct(l);
    }

    /// <summary>The crash log's places for this game folder, from the saves folder in use.</summary>
    public static List<string> CrashLogPlaces(string gameDir) =>
        CrashLogPlaces(string.IsNullOrWhiteSpace(gameDir) ? null : Saves.DirFor(gameDir), PerUserRoot);

    /// <summary>CHARSTAT.TXT's places, best first; <paramref name="perUserSaves"/> is <see cref="Saves.PerUserDir"/> outside the checks.</summary>
    public static List<string> CharStatPlaces(string gameDir, string savesDir, string perUserSaves, string perUserRoot)
    {
        var l = new List<string>();
        if (!SamePath(savesDir, perUserSaves) && BesideSaves(savesDir) is { Length: > 0 } b) l.Add(Path.Combine(b, CharStatFile.FileName));
        if (!string.IsNullOrWhiteSpace(gameDir)) l.Add(Path.Combine(gameDir, CharStatFile.FileName));
        l.Add(Path.Combine(perUserRoot, CharStatFile.FileName));
        return Distinct(l);
    }

    /// <summary>CHARSTAT.TXT's places for this game folder.</summary>
    public static List<string> CharStatPlaces(string gameDir) =>
        CharStatPlaces(gameDir, Saves.DirFor(gameDir), Saves.PerUserDir, PerUserRoot);

    /// <summary>
    /// Appends the text to the first place that takes it, making its folder if need be, and
    /// returns that place; null when none would. Never throws.
    /// </summary>
    public static string? Append(IEnumerable<string> places, string text)
    {
        foreach (string p in places)
        {
            try
            {
                if (Path.GetDirectoryName(p) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
                File.AppendAllText(p, text);
                return p;
            }
            catch { /* not writable here: the next place */ }
        }
        return null;
    }

    private static bool SamePath(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static List<string> Distinct(List<string> l)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var r = new List<string>();
        foreach (string p in l)
        {
            string key;
            try { key = Path.GetFullPath(p); } catch { key = p; }
            if (seen.Add(key)) r.Add(p);
        }
        return r;
    }
}
