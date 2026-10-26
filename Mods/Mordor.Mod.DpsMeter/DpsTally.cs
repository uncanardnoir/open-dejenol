using System.Text.Json;
using Mordor.Core;

namespace Mordor.Mod.DpsMeter;

/// <summary>
/// One character's tally: hits taken off monsters by kind, and kills; and the experience their total
/// went up by, from damage dealt (<see cref="XpDamage"/>: their own and their companions') and from
/// chests' traps disarmed (<see cref="XpChests"/>).
/// </summary>
public sealed record DpsRow(long Physical, long Magical, long Companion, long Kills, long XpDamage = 0, long XpChests = 0)
{
    public static readonly DpsRow Zero = new(0, 0, 0, 0);
    /// <summary>Physical, Magical and Companion together.</summary>
    public long Total => Physical + Magical + Companion;
    /// <summary>The experience from damage and from chests together.</summary>
    public long XpTotal => XpDamage + XpChests;
    public DpsRow Plus(DpsRow o) => new(Physical + o.Physical, Magical + o.Magical, Companion + o.Companion, Kills + o.Kills,
                                        XpDamage + o.XpDamage, XpChests + o.XpChests);
}

/// <summary>
/// Every character's tally, kept by the game in <c>dpsmeter.json</c> in the saves folder (beside
/// settings.ini), keyed by the character's name (matched without regard to case or surrounding
/// spaces):
///
/// <code>
/// {
///   "Version": 1,
///   "Characters": {
///     "Cas": { "Physical": 1520, "Magical": 340, "Companion": 75, "Kills": 12, "XpDamage": 4100, "XpChests": 250 }
///   }
/// }
/// </code>
///
/// A file written before the experience was kept has no XpDamage or XpChests: they read as nought.
///
/// Read once when the mod loads. A blow or a kill only marks the tallies changed: the file is
/// written once things have been quiet for <see cref="SaveDelay"/> (a second by default), on a
/// timer of its own, so a burst - a companion's breath across a group, a round of swings - is one
/// write and the combat round never waits on the disk. Reset and a deleted character are written
/// at once, and so is anything still waiting when the tallies are closed (<see cref="Dispose"/>:
/// the mod does it as the program exits). Each write is whole, beside and then moved over, so a
/// failure half way leaves the last good file; a crash loses at most the last second. A file that
/// cannot be read is moved aside at once, to <c>dpsmeter.json.bad-yyyyMMdd-HHmmss</c>, and the
/// tallies start empty (the mod's log says so); one that cannot even be moved is never written over
/// that session - as the Kaizen mod's kaizen.json. No Avalonia here, so the probe can check it.
///
/// Changed on the game's thread (the events come from the combat round); the timer's write runs on
/// a thread of its own and reads the tallies under the same lock.
/// </summary>
public sealed class DpsTally : IDisposable
{
    public const string FileName = "dpsmeter.json";

    /// <summary>How long after the last change the file is written, unless asked otherwise.</summary>
    public static readonly TimeSpan DefaultSaveDelay = TimeSpan.FromSeconds(1);

    private readonly Dictionary<string, DpsRow> _rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string, Exception>? _error;
    /// <summary>Guards <see cref="_rows"/> and <see cref="_dirty"/>.</summary>
    private readonly object _gate = new();
    /// <summary>One write at a time, each of the tallies as they are when it starts.</summary>
    private readonly object _io = new();
    private readonly Timer _timer;
    private bool _dirty;
    private bool _closed;

    /// <param name="path">The file; null keeps the tallies in memory only.</param>
    /// <param name="error">Where a failure to read or write the file is reported.</param>
    /// <param name="saveDelay">How long after the last change the file is written (<see cref="DefaultSaveDelay"/>).</param>
    public DpsTally(string? path, Action<string, Exception>? error = null, TimeSpan? saveDelay = null)
    {
        Path = path;
        _error = error;
        SaveDelay = saveDelay ?? DefaultSaveDelay;
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        Load();
    }

    public string? Path { get; }
    public TimeSpan SaveDelay { get; }

    /// <summary>How many times the file has been written this session.</summary>
    public int Writes { get; private set; }

    /// <summary>Changes not yet written.</summary>
    public bool Dirty { get { lock (_gate) return _dirty; } }

    /// <summary>A tally changed (a blow, a kill, experience, Reset, a character dropped).</summary>
    public event Action? Changed;

    private static string Key(string? character) => (character ?? "").Trim();

    /// <summary>The characters with a tally, as the file names them.</summary>
    public IReadOnlyList<string> Characters { get { lock (_gate) return _rows.Keys.ToList(); } }

    /// <summary>A character's tally (zero when they have none).</summary>
    public DpsRow For(string character)
    {
        lock (_gate) return _rows.TryGetValue(Key(character), out var r) ? r : DpsRow.Zero;
    }

    /// <summary>
    /// What one <see cref="CombatDamage"/> adds: its amount to the column of its kind, and its
    /// kills to Kills - a companion's kills included, credited to its owner like its damage.
    /// </summary>
    public static DpsRow Of(CombatDamage d) => new(
        d.Kind == DamageKind.Physical ? d.Amount : 0,
        d.Kind == DamageKind.Magical ? d.Amount : 0,
        d.Kind == DamageKind.Companion ? d.Amount : 0,
        d.Kills);

    /// <summary>What one <see cref="CombatExperience"/> adds: its amount, under damage or chests by its source.</summary>
    public static DpsRow Of(CombatExperience e) => new(0, 0, 0, 0,
        e.Source == ExperienceSource.Damage ? e.Amount : 0,
        e.Source == ExperienceSource.Chest ? e.Amount : 0);

    /// <summary>Count one event (<see cref="Of"/>) for its character; the file follows after <see cref="SaveDelay"/>.</summary>
    public void Add(CombatDamage d) => Add(d.Character.Name, Of(d));

    /// <summary>Count one award of experience (<see cref="Of(CombatExperience)"/>) for its character.</summary>
    public void Add(CombatExperience e) => Add(e.Character.Name, Of(e));

    public void Add(string character, DpsRow add)
    {
        string key = Key(character);
        if (key == "" || add == DpsRow.Zero) return;
        lock (_gate)
        {
            _rows[key] = (_rows.TryGetValue(key, out var r) ? r : DpsRow.Zero).Plus(add);
            _dirty = true;
            // each change puts the write off again: it comes once things are quiet
            if (!_closed && Path != null) _timer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke();
    }

    /// <summary>Every tally back to nothing, damage and experience (the window's Reset), and the file written empty at once.</summary>
    public void Reset()
    {
        lock (_gate) { _rows.Clear(); _dirty = true; }
        Flush();
        Changed?.Invoke();
    }

    /// <summary>A character's tally dropped (the character was deleted), and the file written at once.</summary>
    public void Remove(string character)
    {
        lock (_gate)
        {
            if (!_rows.Remove(Key(character))) return;
            _dirty = true;
        }
        Flush();
        Changed?.Invoke();
    }

    /// <summary>Write whatever is waiting now (nothing when nothing changed).</summary>
    public void Flush()
    {
        lock (_io)
        {
            Dictionary<string, DpsRow> snapshot;
            lock (_gate)
            {
                if (!_dirty) return;
                _dirty = false;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                snapshot = new Dictionary<string, DpsRow>(_rows, StringComparer.OrdinalIgnoreCase);
            }
            Save(snapshot);
        }
    }

    /// <summary>Write what is waiting and stop the timer (the program is closing).</summary>
    public void Dispose()
    {
        lock (_gate) _closed = true;
        Flush();
        _timer.Dispose();
    }

    // ---------------------------------------------------------------------------------- the file

    private sealed class FileShape
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, RowShape> Characters { get; set; } = new();
    }

    private sealed class RowShape
    {
        public long Physical { get; set; }
        public long Magical { get; set; }
        public long Companion { get; set; }
        public long Kills { get; set; }
        public long XpDamage { get; set; }
        public long XpChests { get; set; }
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
        _rows.Clear();
        if (Path == null || !File.Exists(Path)) return;
        try
        {
            var shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(Path), Options);
            foreach (var (name, r) in shape?.Characters ?? new())
            {
                string key = Key(name);
                if (key == "" || r == null) continue;
                _rows[key] = new DpsRow(r.Physical, r.Magical, r.Companion, r.Kills, r.XpDamage, r.XpChests);
            }
        }
        catch (Exception ex)
        {
            _rows.Clear();
            // moved aside before anything can write over it: the next change writes a new file
            string aside = Path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                File.Move(Path, aside);
                MovedAside = aside;
                _error?.Invoke($"{Path} could not be read; it was moved aside to {aside} and the tallies start empty", ex);
            }
            catch (Exception moveEx)
            {
                _readOnly = true;
                _error?.Invoke($"{Path} could not be read, nor moved aside ({moveEx.Message}); the tallies start empty and nothing is written to it this session", ex);
            }
        }
    }

    /// <summary>Where a file that could not be read was moved to, or null.</summary>
    public string? MovedAside { get; private set; }

    /// <summary>A file that could not be read nor moved aside is never written over.</summary>
    private bool _readOnly;

    private void Save(Dictionary<string, DpsRow> rows)
    {
        if (Path == null || _readOnly) return;
        var shape = new FileShape
        {
            Characters = rows.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, p => new RowShape { Physical = p.Value.Physical, Magical = p.Value.Magical, Companion = p.Value.Companion, Kills = p.Value.Kills,
                                                      XpDamage = p.Value.XpDamage, XpChests = p.Value.XpChests }),
        };
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // written beside and moved over, so a failure half way leaves the last good file
            string temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(shape, Options));
            File.Move(temp, Path, overwrite: true);
            Writes++;
        }
        catch (Exception ex) { _error?.Invoke($"{Path} could not be written", ex); }
    }
}
