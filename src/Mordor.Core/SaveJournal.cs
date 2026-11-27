using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mordor.Core;

/// <summary>
/// The port's own: <c>saves-journal.log</c> in the saves folder, a plain-text line for each thing
/// the game does with a save file - the program opened and closed, a character loaded and unloaded,
/// every save file written or deleted - so that a report of lost progress comes with evidence
/// (docs/GAME_FILES.md, "The save journal"). It is local and never sent anywhere: the player
/// attaches it to an issue when asked.
///
/// Each line is the UTC time and the local offset, the process id in brackets (two copies of the
/// game writing the same folder show as two ids), the event, and its details. File names are
/// relative to the saves folder; any path the system puts in an error message has the saves folder
/// and the home folder taken out. Over <see cref="Cap"/> bytes the oldest half goes.
///
/// It must never change the game: every call here is guarded and returns nothing, a line that cannot
/// be written is dropped, never the save, and with no <see cref="Open"/> (the tools, the tests) it does
/// nothing at all. The folder is not made for it: lines wait in memory until the game's own first
/// write makes the folder.
/// </summary>
public static class SaveJournal
{
    public const string FileName = "saves-journal.log";

    /// <summary>The size past which the oldest half is dropped. Settable for the tests.</summary>
    public static long Cap { get; set; } = 1_000_000;

    /// <summary>
    /// The names in a folder, for the tests: a case-sensitive file system's listing stood in on
    /// Windows, where Bob.json and bob.json cannot both be. Null is the folder's own listing.
    /// </summary>
    public static Func<string, IEnumerable<string>>? ListFolder { get; set; }

    private static readonly object Gate = new();
    private static readonly int Pid = Environment.ProcessId;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly AsyncLocal<string?> Label = new();
    [ThreadStatic] private static Character? _describing;

    private static string? _folder;
    private static string? _path;
    private static bool _closed;
    private static readonly List<string> Waiting = new();
    private static readonly Dictionary<string, List<Written>> Writes = new(StringComparer.Ordinal);

    private readonly record struct Written(string Hash, string Time, int Pid);

    /// <summary>The saves folder the journal is kept in, or null when it is not open.</summary>
    public static string? Folder => _folder;

    /// <summary>The journal file, or null when it is not open.</summary>
    public static string? PathNow => _path;

    // ---------------------------------------------------------------- the program

    /// <summary>
    /// The program has opened on <paramref name="savesDir"/>: the earlier writes are read back for the
    /// verdicts on loads, and the OPEN line says the version, the system, the kind of saves folder,
    /// and whether the run before ended without a closing line.
    /// </summary>
    public static void Open(string savesDir, string kind, string version)
    {
        try
        {
            lock (Gate)
            {
                _folder = Path.GetFullPath(savesDir);
                _path = Path.Combine(_folder, FileName);
                _closed = false;
                Waiting.Clear();
                Writes.Clear();
                string previous = ReadBack();
                string os = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();
                string cpu = $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} on "
                           + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
                Append($"OPEN OpenDejenol {version}; {os}; {cpu}; saves folder: {kind}; {previous}");
            }
        }
        catch { }
    }

    /// <summary>The last line of a run, said once: how the program is ending.</summary>
    public static void Closed(string how)
    {
        try
        {
            lock (Gate)
            {
                if (_folder == null || _closed) return;
                _closed = true;
                Append("CLOSE " + how);
            }
        }
        catch { }
    }

    /// <summary>For the tests: the journal let go, with no closing line, as a killed program leaves it.</summary>
    public static void Detach()
    {
        lock (Gate)
        {
            _folder = _path = null;
            _closed = false;
            Waiting.Clear();
            Writes.Clear();
        }
    }

    // ---------------------------------------------------------------- characters in and out of play

    /// <summary>
    /// A character taken into play: <paramref name="how"/>, the file as it is on disk (its name in
    /// the case it has there, size, short hash), the character as loaded, and the file against the
    /// journal's last write of it. <paramref name="file"/> is the file read; null finds it by name.
    /// </summary>
    public static void Loaded(Character ch, string how, string? file = null)
    {
        if (_folder is not string folder) return;
        try
        {
            string want = file != null ? Path.GetFileName(file) : Character.SafeFileName(ch.Name) + ".json";
            var names = Listing(folder);
            string onDisk = names.FirstOrDefault(n => n == want)
                         ?? names.FirstOrDefault(n => string.Equals(n, want, StringComparison.OrdinalIgnoreCase))
                         ?? want;
            var twins = names.Where(n => n != onDisk && string.Equals(n, onDisk, StringComparison.OrdinalIgnoreCase)).ToList();
            byte[]? bytes = null;
            try { bytes = File.ReadAllBytes(Path.Combine(folder, onDisk)); } catch { }
            var sb = new StringBuilder($"LOAD \"{ch.Name.TrimEnd()}\" how: {how}; ");
            if (bytes == null) sb.Append($"file \"{onDisk}\" could not be read");
            else
            {
                string hash = Short(bytes);
                sb.Append($"file \"{onDisk}\" {bytes.Length} bytes sha {hash}; ");
                lock (Gate) sb.Append(Verdict(onDisk, hash));
            }
            sb.Append("; ").Append(Snapshot(ch));
            foreach (var t in twins)
                sb.Append($"; ALSO \"{t}\" in the folder, the same name but for case");
            lock (Gate) Append(sb.ToString());
        }
        catch { }
    }

    /// <summary>
    /// A character let go of: <paramref name="how"/>, and whether a save was written as it happened.
    /// Null for <paramref name="saved"/>, where the unload itself writes nothing, says instead when this run
    /// last wrote the file, or that it never did.
    /// </summary>
    public static void Unloaded(Character ch, string how, bool? saved)
    {
        if (_folder == null) return;
        try
        {
            string said = saved switch
            {
                true => "saved as it happened",
                false => "NOT saved as it happened",
                null => LastByThisRun(Character.SafeFileName(ch.Name) + ".json"),
            };
            lock (Gate) Append($"UNLOAD \"{ch.Name.TrimEnd()}\" how: {how}; {said}");
        }
        catch { }
    }

    // ---------------------------------------------------------------- the files

    /// <summary>
    /// What the saves that follow are for, until the scope is disposed: "autosave", "arrival in town".
    /// Scopes inside scopes are joined, outermost first. The call stack is written beside it anyway.
    /// </summary>
    public static Scope Because(string why)
    {
        string? was = Label.Value;
        try { Label.Value = was == null ? why : was + " / " + why; } catch { }
        return new Scope(() => Label.Value = was);
    }

    /// <summary>The character whose file the writes inside the scope are, for the WRITE line's snapshot.</summary>
    public static Scope Describing(Character ch)
    {
        var was = _describing;
        _describing = ch;
        return new Scope(() => _describing = was);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Action? _end;
        internal Scope(Action end) => _end = end;
        public void Dispose() { try { _end?.Invoke(); } catch { } }
    }

    /// <summary>A save file written whole (SafeFile): its name, size, short hash, why, and for a character the snapshot.</summary>
    public static void Wrote(string path, byte[] bytes)
    {
        if (_folder == null) return;
        try
        {
            if (InJournal(path) is not string name) return;
            string hash = Short(bytes);
            var sb = new StringBuilder($"WRITE \"{name}\" {bytes.Length} bytes sha {hash}; {Reason()}");
            if (_describing is Character ch) sb.Append("; ").Append(Snapshot(ch));
            lock (Gate)
            {
                string stamp = Stamp(out string time);
                if (!Writes.TryGetValue(name, out var list)) Writes[name] = list = new();
                list.Add(new Written(hash, time, Pid));
                Append(sb.ToString(), stamp);
            }
        }
        catch { }
    }

    /// <summary>A save that could not be written: the file kept what it had.</summary>
    public static void WriteFailed(string path, Exception e)
    {
        if (_folder == null) return;
        try
        {
            if (InJournal(path) is not string name) return;
            lock (Gate) Append($"WRITE-FAILED \"{name}\"; {Reason()}; {e.GetType().Name}: {e.Message}");
        }
        catch { }
    }

    /// <summary>A save file deleted by the game.</summary>
    public static void Deleted(string path)
    {
        if (_folder == null) return;
        try
        {
            if (InJournal(path) is not string name) return;
            lock (Gate) Append($"DELETE \"{name}\"; {Reason()}");
        }
        catch { }
    }

    /// <summary>A save file renamed by the game (Roster: the name it is saved under on every system).</summary>
    public static void Renamed(string from, string to)
    {
        if (_folder == null) return;
        try
        {
            if (InJournal(from) is not string a || InJournal(to) is not string b) return;
            lock (Gate) Append($"RENAME \"{a}\" to \"{b}\"; {Reason()}");
        }
        catch { }
    }

    // ---------------------------------------------------------------- the parts

    /// <summary>
    /// A character in one line: guild and level, that guild's experience, gold on hand and in the
    /// bank, the six statistics, and the dungeon level and square, or town.
    /// </summary>
    public static string Snapshot(Character c)
    {
        try
        {
            var g = c.Guilds.Count > 0 ? c.Guilds[0] : null;
            var s = c.Stats;
            string stats = s.Length >= 6 ? $"Str {s[0]} Int {s[1]} Wis {s[2]} Con {s[3]} Cha {s[4]} Dex {s[5]}" : "no stats";
            string where = c.DungeonLevel > 0 ? $"level {c.DungeonLevel} at {c.X},{c.Y}" : "town";
            return $"{(g == null ? "no guild" : g.Guild + " " + g.Level)}, xp {g?.Experience ?? 0}, "
                 + $"gold {c.GoldOnHandWhole} + {c.GoldInBankWhole} in bank, {stats}, {where}";
        }
        catch { return "(no snapshot)"; }
    }

    /// <summary>The first 12 hex digits of the SHA-256.</summary>
    public static string Short(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant();

    /// <summary>The file's name when it is one the journal follows: in the saves folder itself, not the journal, a .tmp or an .ini.</summary>
    private static string? InJournal(string path)
    {
        if (_folder is not string folder) return null;
        string full = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(full);
        if (dir == null || !string.Equals(Path.TrimEndingDirectorySeparator(dir), Path.TrimEndingDirectorySeparator(folder),
                                          OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            return null;
        string name = Path.GetFileName(full);
        if (name == FileName || name.EndsWith(SafeFile.TempSuffix, StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) return null;
        return name;
    }

    private static List<string> Listing(string folder)
    {
        try
        {
            if (ListFolder != null) return ListFolder(folder).ToList();
            return Directory.EnumerateFiles(folder).Select(f => Path.GetFileName(f)).ToList();
        }
        catch { return new List<string>(); }
    }

    /// <summary>The file read against the journal's writes of it. Under <see cref="Gate"/>.</summary>
    private static string Verdict(string name, string hash)
    {
        if (!Writes.TryGetValue(name, out var list) || list.Count == 0) return "no write recorded";
        var last = list[^1];
        if (last.Hash == hash) return $"same as written {last.Time}";
        for (int i = list.Count - 2; i >= 0; i--)
            if (list[i].Hash == hash)
                return $"OLDER than the last write {last.Time}: it is the file written {list[i].Time}";
        return $"CHANGED since this program wrote it {last.Time}: something else wrote it";
    }

    private static string LastByThisRun(string name)
    {
        lock (Gate)
        {
            if (Writes.TryGetValue(name, out var list))
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].Pid == Pid) return $"last saved by this run {list[i].Time}";
        }
        return "NOT saved by this run";
    }

    /// <summary>
    /// Who asked for the write: the label of any <see cref="Because"/> scope, then the call stack's
    /// first frames in the port - the save routine ("via") and the code that called it ("from").
    /// </summary>
    private static string Reason()
    {
        string? label = Label.Value;
        var via = new List<string>();
        var from = new List<string>();
        try
        {
            var core = typeof(SaveJournal).Assembly;
            foreach (var frame in new StackTrace(1, false).GetFrames())
            {
                if (frame.GetMethod() is not System.Reflection.MethodBase m || m.DeclaringType is not Type t) continue;
                if (t == typeof(SaveJournal) || t == typeof(SafeFile)) continue;
                string? ns = t.Namespace;
                if (ns != null && !ns.StartsWith("Mordor", StringComparison.Ordinal)) continue;
                string name = Pretty(t, m);
                var into = t.Assembly == core && from.Count == 0 ? via : from;
                if (into.Count > 0 && into[^1] == name) continue;
                if (into == via && via.Count >= 2) continue;
                into.Add(name);
                if (from.Count >= 2) break;
            }
        }
        catch { }
        string stack = (via.Count > 0 ? "via " + string.Join(" < ", via) : "")
                     + (from.Count > 0 ? (via.Count > 0 ? " " : "") + "from " + string.Join(" < ", from) : "");
        if (stack == "") stack = "from an unknown caller";
        return label == null ? stack : label + "; " + stack;
    }

    /// <summary>Type.Method, with the compiler's names for async methods and lambdas read back.</summary>
    private static string Pretty(Type t, System.Reflection.MethodBase m)
    {
        string method = m.Name;
        while (t.Name.StartsWith('<') && t.DeclaringType != null)
        {
            if (method == "MoveNext" || method.StartsWith('<')) method = Inner(t.Name) is { Length: > 0 } inner ? inner : method;
            t = t.DeclaringType;
        }
        if (method.StartsWith('<')) method = Inner(method);
        // a constructor, or a handler a constructor wired up: the compiler's ".ctor", "-ctor" in a nested name
        method = method.TrimStart('.', '-');
        string type = t.Name;
        int tick = type.IndexOf('`');
        if (tick > 0) type = type[..tick];
        return type + "." + method;

        static string Inner(string s)
        {
            // "<Name>d__4", "<Name>b__2_0", and an async lambda's "<<Name>b__2_0>d": the first name inside
            string inner = s.TrimStart('<');
            int b = inner.IndexOf('>');
            return b >= 0 ? inner[..b] : s;
        }
    }

    private static string Stamp(out string utc)
    {
        var now = DateTimeOffset.Now;
        utc = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var off = now.Offset;
        return utc + " " + (off < TimeSpan.Zero ? "-" : "+") + off.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>One line, appended and flushed, the folder and home path taken out. Under <see cref="Gate"/>.</summary>
    private static void Append(string body, string? stamp = null)
    {
        if (_folder is not string folder || _path is not string path) return;
        stamp ??= Stamp(out _);
        string line = Scrub($"{stamp} [{Pid}] {body}".Replace('\r', ' ').Replace('\n', ' '), folder);
        Waiting.Add(line);
        if (Waiting.Count > 200) Waiting.RemoveAt(0);
        // not made here: the game's own first write makes the folder, and the waiting lines go then
        if (!Directory.Exists(folder)) return;
        try
        {
            var text = new StringBuilder();
            foreach (string w in Waiting) text.Append(w).Append(Environment.NewLine);
            Waiting.Clear();
            byte[] bytes = Utf8.GetBytes(text.ToString());
            long length;
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
                length = fs.Length;
            }
            if (length > Cap) Trim(path);
        }
        catch { Waiting.Clear(); }   // the line is dropped; the save it describes is not touched
    }

    /// <summary>Over the cap: the newest half kept, from a line's start.</summary>
    private static void Trim(string path)
    {
        try
        {
            byte[] all = File.ReadAllBytes(path);
            long keep = Math.Max(1, Cap / 2);
            int from = (int)Math.Max(0, all.Length - keep);
            while (from < all.Length && from > 0 && all[from - 1] != (byte)'\n') from++;
            string temp = path + SafeFile.TempSuffix;
            File.WriteAllBytes(temp, all[from..]);
            File.Move(temp, path, overwrite: true);
        }
        catch { }
    }

    private static string Scrub(string line, string folder)
    {
        string trimmed = Path.TrimEndingDirectorySeparator(folder);
        line = line.Replace(trimmed + Path.DirectorySeparatorChar, "", StringComparison.OrdinalIgnoreCase)
                   .Replace(trimmed, "<saves>", StringComparison.OrdinalIgnoreCase);
        try
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.Length > 1) line = line.Replace(Path.TrimEndingDirectorySeparator(home), "~", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        return line;
    }

    private static readonly Regex WriteLine = new(@"^(\S+) \S+ \[(\d+)\] WRITE ""([^""]+)"" \d+ bytes sha ([0-9a-f]+)", RegexOptions.CultureInvariant);
    private static readonly Regex RunLine = new(@"^(\S+) \S+ \[(\d+)\] (OPEN|CLOSE)\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// The journal so far, read for its writes, and the run before this one: whether its OPEN has a
    /// CLOSE from the same process after it. Under <see cref="Gate"/>.
    /// </summary>
    private static string ReadBack()
    {
        string? lastOpenTime = null;
        string? lastOpenPid = null;
        bool closed = false;
        try
        {
            if (_path == null || !File.Exists(_path)) return "no earlier run in the journal";
            foreach (string line in File.ReadLines(_path))
            {
                var w = WriteLine.Match(line);
                if (w.Success)
                {
                    string name = w.Groups[3].Value;
                    if (!Writes.TryGetValue(name, out var list)) Writes[name] = list = new();
                    list.Add(new Written(w.Groups[4].Value, w.Groups[1].Value,
                                         int.TryParse(w.Groups[2].Value, out int p) ? p : 0));
                    continue;
                }
                var r = RunLine.Match(line);
                if (!r.Success) continue;
                if (r.Groups[3].Value == "OPEN") { lastOpenTime = r.Groups[1].Value; lastOpenPid = r.Groups[2].Value; closed = false; }
                else if (r.Groups[2].Value == lastOpenPid) closed = true;
            }
        }
        catch { return "the journal could not be read back"; }
        if (lastOpenTime == null) return "no earlier run in the journal";
        return closed
            ? $"the previous run [{lastOpenPid}] closed"
            : $"the previous run [{lastOpenPid}], opened {lastOpenTime}, has NO CLOSING LINE: it crashed, was killed, or is still running";
    }
}
