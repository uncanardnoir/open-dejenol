namespace Mordor.Core;

/// <summary>
/// A character save that will not read is never just passed over any more. The menu's lists
/// (<see cref="Roster.Saved"/>) skipped it in silence while the name stayed taken - the file was
/// there - so a character damaged by a crash simply vanished. A port necessity, as
/// <see cref="SafeFile"/> is: the original has no such file to lose.
///
/// Each character that reads keeps a copy of itself in the saves folder's <c>last-good</c> folder
/// (<see cref="Keep"/>, at most every few minutes). One that does not read is reported in crash.log
/// (<see cref="Log"/>) with why, and, when there is a copy, put back from it - the damaged file is
/// kept in the <c>damaged</c> folder for a bug report - and the player is told
/// (<see cref="TakeNotices"/>, shown by the menu). Both folders sit inside the saves folder, where
/// nothing that walks its top level for saves or backups looks.
/// </summary>
public static class SaveRecovery
{
    public const string GoodDir = "last-good";
    public const string DamagedDir = "damaged";

    /// <summary>How stale the last good copy may grow while a character is played and saved.</summary>
    public static readonly TimeSpan KeepEvery = TimeSpan.FromMinutes(5);

    /// <summary>crash.log, set by the program.</summary>
    public static Action<string>? Log { get; set; }

    /// <summary>The saves folder's files that are not characters, and are not read as one.</summary>
    private static readonly HashSet<string> WorldFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "roster.json", "automap.json", "dungeon.json", "store.json", "library.json", "records.json", SaveFormat.FileName,
        "confinement.json", "guildmasters.json",
    };

    private static readonly object Gate = new();
    private static readonly List<string> Notices = new();
    /// <summary>A damaged file is reported once a run for each state of it, not at every list.</summary>
    private static readonly HashSet<string> Reported = new(StringComparer.OrdinalIgnoreCase);

    public static string GoodPathFor(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "", GoodDir, Path.GetFileName(path));

    /// <summary>Whether a file that will not read as a character may be one (not a world file).</summary>
    public static bool MayBeCharacter(string path)
    {
        string name = Path.GetFileName(path);
        return name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("party-", StringComparison.OrdinalIgnoreCase)
            && !WorldFiles.Contains(name)
            && !DungeonState.IsLevelFile(name);   // dungeon-01.json and the rest: the levels, not characters
    }

    /// <summary>A character file that has just read: copied to last-good, unless that copy is recent.</summary>
    public static void Keep(string path)
    {
        try
        {
            var file = new FileInfo(path);
            var good = new FileInfo(GoodPathFor(path));
            if (good.Exists && (good.LastWriteTimeUtc >= file.LastWriteTimeUtc
                                || DateTime.UtcNow - good.LastWriteTimeUtc < KeepEvery)) return;
            SafeFile.Copy(path, good.FullName);
        }
        catch { }
    }

    /// <summary>
    /// A file that would not read (<paramref name="why"/>): reported, and put back from its last good
    /// copy when there is one that reads. The character as put back, or null when there is none.
    /// </summary>
    public static Character? Damaged(string path, Exception why)
    {
        string file = Path.GetFileName(path);
        bool first;
        lock (Gate)
        {
            string state = path + "|" + SafeStamp(path);
            first = Reported.Add(state);
        }
        if (first) Log?.Invoke($"save file {path} could not be read: {why.GetType().Name}: {why.Message}");

        string good = GoodPathFor(path);
        if (File.Exists(good))
        {
            try
            {
                var copy = Character.Load(good);
                if (!string.IsNullOrWhiteSpace(copy.Name))
                {
                    string damaged = Path.Combine(Path.GetDirectoryName(path) ?? "", DamagedDir,
                        $"{Path.GetFileNameWithoutExtension(file)}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(damaged)!);
                    SafeFile.Copy(path, damaged);
                    DateTime when = File.GetLastWriteTime(good);
                    SafeFile.Copy(good, path);
                    var restored = Character.Load(path);
                    Log?.Invoke($"save file {path} put back from its last good copy of {when:yyyy-MM-dd HH:mm}; the damaged file is {damaged}");
                    Notice($"{restored.Name}'s save file could not be read, so it has been put back as it was on "
                         + $"{when:d MMMM yyyy} at {when:HH:mm}.\n\nThe damaged file is kept in the saves folder's "
                         + $"\"{DamagedDir}\" folder, and what went wrong is in crash.log.");
                    return restored;
                }
            }
            catch (Exception e) { Log?.Invoke($"the last good copy {good} could not be put back: {e.GetType().Name}: {e.Message}"); }
        }
        if (first)
            Notice($"The save file \"{file}\" could not be read, and there is no earlier copy of it to put back.\n\n"
                 + "It has been left as it is. What went wrong is in crash.log - please attach both files to a bug report.");
        return null;
    }

    private static string SafeStamp(string path)
    {
        try { var f = new FileInfo(path); return f.Length + "@" + f.LastWriteTimeUtc.Ticks; }
        catch { return ""; }
    }

    private static void Notice(string text)
    {
        lock (Gate) Notices.Add(text);
    }

    /// <summary>What the player has not been told yet, taken: the menu shows each in a box.</summary>
    public static List<string> TakeNotices()
    {
        lock (Gate)
        {
            var all = new List<string>(Notices);
            Notices.Clear();
            return all;
        }
    }
}
