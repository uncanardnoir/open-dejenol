using Mordor.Core;

namespace Mordor.Modding;

/// <summary>
/// The mods' part of the port's settings.ini (in the saves directory, beside the Options window's
/// keys). Each mod has a section of its own:
///
/// <code>
/// [Mod:QoL]
/// Enabled=True
/// UnlockedStoreAlignment=True
/// </code>
///
/// Every write re-reads the file and changes one key, as PortSettings.Write does, so the other
/// sections - the Options window's, "Loading Information" - are left as they were. Reads come from
/// what was read at start plus what has been written since.
/// </summary>
public sealed class ModSettingsStore
{
    private readonly object _gate = new();
    private readonly Ini _ini;

    public ModSettingsStore(string path)
    {
        Path = path;
        _ini = Ini.Read(path);
    }

    /// <summary>The settings.ini this reads and writes.</summary>
    public string Path { get; }

    /// <summary>False: nothing is written (a store for a run that must not touch the player's file).</summary>
    public bool Writable { get; set; } = true;

    /// <summary>Raised after any value changes: the mod's id and the key.</summary>
    public event Action<string, string>? Changed;

    public static string SectionFor(string modId) => "Mod:" + modId;

    public string? Read(string modId, string key)
    {
        lock (_gate) return _ini.Has(SectionFor(modId), key) ? _ini.Text(SectionFor(modId), key) : null;
    }

    public void Write(string modId, string key, string value)
    {
        value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ');   // one line per key
        lock (_gate)
        {
            _ini.Set(SectionFor(modId), key, value);
            if (Writable)
            {
                try
                {
                    string? dir = System.IO.Path.GetDirectoryName(Path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
                catch { }
                var onDisk = Ini.Read(Path);
                onDisk.Set(SectionFor(modId), key, value);
                onDisk.Write(Path);
            }
        }
        Changed?.Invoke(modId, key);
    }

    /// <summary>Take a key out of the mod's section, in memory and on disk; nothing when it is not there.</summary>
    public void Remove(string modId, string key)
    {
        lock (_gate)
        {
            if (!_ini.Has(SectionFor(modId), key)) return;
            _ini.Remove(SectionFor(modId), key);
            if (Writable)
            {
                var onDisk = Ini.Read(Path);
                onDisk.Remove(SectionFor(modId), key);
                onDisk.Write(Path);
            }
        }
        Changed?.Invoke(modId, key);
    }

    public bool ReadFlag(string modId, string key, bool fallback)
    {
        string? v = Read(modId, key);
        if (v == null) return fallback;
        return string.Equals(v.Trim(), "True", StringComparison.OrdinalIgnoreCase);
    }

    public void WriteFlag(string modId, string key, bool value) => Write(modId, key, value ? "True" : "False");

    /// <summary>Whether a mod is switched on. Off until the player ticks it: mods are opt-in.</summary>
    public bool IsEnabled(string modId) => ReadFlag(modId, "Enabled", false);

    public void SetEnabled(string modId, bool on) => WriteFlag(modId, "Enabled", on);
}

/// <summary>
/// One mod's settings: the toggles it declares (in its manifest or from code), read live, and any
/// other boolean or text values it wants kept.
/// </summary>
public sealed class ModSettings
{
    private readonly string _id;
    private readonly ModSettingsStore _store;
    private readonly List<ModToggleSpec> _toggles = new();

    private readonly List<ModChoiceSpec> _choices = new();

    internal ModSettings(string modId, ModSettingsStore store, IEnumerable<ModToggleSpec> declared, IEnumerable<ModChoiceSpec>? choices = null)
    {
        _id = modId;
        _store = store;
        _toggles.AddRange(declared);
        if (choices != null) _choices.AddRange(choices);
        store.Changed += (mod, key) =>
        {
            if (mod != _id) return;
            // a handler's failure is its own: it must not reach whoever changed the setting
            foreach (var h in Changed?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((Action<string>)h)(key); } catch { }
        };
    }

    /// <summary>
    /// One of this mod's settings changed (the Mods page, a mod's own box, the mod itself), with its
    /// key: for a mod's window that shows a setting. A handler that throws is ignored.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>The checkboxes the Mods page shows for this mod.</summary>
    public IReadOnlyList<ModToggleSpec> Toggles => _toggles;

    /// <summary>
    /// Declare a toggle from code (one already in the manifest is left as the manifest has it).
    /// It appears on the Mods page while the mod is loaded.
    /// </summary>
    public ModToggleSpec DeclareToggle(string key, string label, string description, bool defaultValue)
    {
        var have = _toggles.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
        if (have != null) return have;
        var t = new ModToggleSpec { Key = key, Label = label, Description = description, Default = defaultValue };
        _toggles.Add(t);
        return t;
    }

    private bool DefaultFor(string key) =>
        _toggles.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))?.Default ?? false;

    /// <summary>A toggle's value now: what settings.ini holds, or its declared default.</summary>
    public bool Get(string key) => _store.ReadFlag(_id, key, DefaultFor(key));

    public void Set(string key, bool value) => _store.WriteFlag(_id, key, value);

    /// <summary>Whether settings.ini holds a value for this key (API version 8): a setting moved or merged can be told from one never set.</summary>
    public bool Has(string key) => _store.Read(_id, key) != null;

    /// <summary>Forget a key (API version 8): an old setting a newer version of the mod has moved or merged.</summary>
    public void Remove(string key) => _store.Remove(_id, key);

    public string GetText(string key, string fallback = "") => _store.Read(_id, key) ?? fallback;

    /// <summary>The choice settings this mod declares (<see cref="ModChoiceSpec"/>, API version 10).</summary>
    public IReadOnlyList<ModChoiceSpec> Choices => _choices;

    /// <summary>
    /// A choice setting's option now (API version 10): what settings.ini holds when it is one of the
    /// declared options (case ignored, given as declared), else the declared default, else the first
    /// option; for a key not declared, what settings.ini holds or "".
    /// </summary>
    public string GetChoice(string key) => ChoiceOf(_choices.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)), _store.Read(_id, key), key);

    /// <summary>The option a choice setting shows, from what settings.ini holds (null: nothing).</summary>
    public static string ChoiceOf(ModChoiceSpec? spec, string? stored, string key = "")
    {
        if (spec == null || spec.Options.Count == 0) return stored ?? "";
        string? match = spec.Options.FirstOrDefault(o => string.Equals(o, stored?.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? spec.Options.FirstOrDefault(o => string.Equals(o, spec.Default, StringComparison.OrdinalIgnoreCase)) ?? spec.Options[0];
    }

    public void SetChoice(string key, string option) => _store.Write(_id, key, option);

    public void SetText(string key, string value) => _store.Write(_id, key, value);

    /// <summary>
    /// A list setting's rows (<see cref="ModTableSpec"/>): what settings.ini holds under the key,
    /// decoded (<see cref="DecodeRows"/>), or none. Read it in your hooks, as the toggles are.
    /// </summary>
    public List<string[]> GetRows(string key) => DecodeRows(_store.Read(_id, key) ?? "");

    public void SetRows(string key, IEnumerable<IReadOnlyList<string>> rows) => _store.Write(_id, key, EncodeRows(rows));

    /// <summary>
    /// One line for a list of rows: cells joined by <c>|</c>, rows by <c>;</c>, and a backslash
    /// before any <c>\</c>, <c>|</c> or <c>;</c> inside a cell. Line breaks in a cell become spaces
    /// (settings.ini keeps one line per key).
    /// </summary>
    public static string EncodeRows(IEnumerable<IReadOnlyList<string>> rows)
    {
        static string Cell(string? s) => (s ?? "").Replace("\r", " ").Replace("\n", " ")
            .Replace("\\", "\\\\").Replace("|", "\\|").Replace(";", "\\;");
        return string.Join(";", rows.Select(r => string.Join("|", r.Select(Cell))));
    }

    /// <summary>The rows <see cref="EncodeRows"/> wrote. An empty line is no rows.</summary>
    public static List<string[]> DecodeRows(string line)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(line)) return rows;
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\\' && i + 1 < line.Length) { cell.Append(line[++i]); continue; }
            if (c == '|') { cells.Add(cell.ToString()); cell.Clear(); continue; }
            if (c == ';') { cells.Add(cell.ToString()); cell.Clear(); rows.Add(cells.ToArray()); cells.Clear(); continue; }
            cell.Append(c);
        }
        cells.Add(cell.ToString());
        rows.Add(cells.ToArray());
        return rows;
    }
}
