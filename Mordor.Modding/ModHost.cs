using System.Reflection;
using System.Runtime.Loader;
using Mordor.Core;

namespace Mordor.Modding;

/// <summary>A folder under a mods directory with a mod.json in it.</summary>
public sealed class DiscoveredMod
{
    internal DiscoveredMod(string directory, ModManifest manifest, string? problem)
    {
        Directory = directory; Manifest = manifest; Problem = problem;
    }
    public string Directory { get; }
    public ModManifest Manifest { get; }
    /// <summary>Why it cannot be loaded (a bad manifest), or null.</summary>
    public string? Problem { get; }
    public string Id => Manifest.Id;
}

/// <summary>A mod that was enabled and whose <see cref="IMod.Load"/> was called.</summary>
public sealed class LoadedMod
{
    internal LoadedMod(DiscoveredMod source, ModContext context) { Source = source; Context = context; }
    public DiscoveredMod Source { get; }
    internal ModContext Context { get; }
    public string Id => Source.Id;
    public ModManifest Manifest => Source.Manifest;
    public ModSettings Settings => Context.Settings;
    public ModUI UI => Context.UI;
    /// <summary>Disabled for the rest of the session because it threw; see <see cref="FaultReason"/>.</summary>
    public bool Faulted { get; internal set; }
    public string? FaultReason { get; internal set; }
    /// <summary>"Class.Method (prefix)" for each Harmony patch it has in place (made while loading or enabling).</summary>
    public List<string> Patches { get; } = new();
    /// <summary>The mod's entry object, once made.</summary>
    public IMod? Instance { get; internal set; }
    /// <summary>
    /// Switched on. False once an <see cref="IModLifecycle"/> mod is switched off on the main menu: still
    /// loaded (an assembly cannot be unloaded safely), but nothing of it is called.
    /// </summary>
    public bool Active { get; internal set; } = true;
    /// <summary>Whether it can be switched on and off without a restart: its entry implements <see cref="IModLifecycle"/>.</summary>
    public bool SwitchesLive => Instance is IModLifecycle;
    /// <summary>Loaded, switched on and not disabled for throwing: the host calls it.</summary>
    public bool Running => Active && !Faulted;
}

/// <summary>Each mod's own context: its hooks, its settings, its log, its Harmony.</summary>
internal sealed class ModContext : IModContext
{
    private HarmonyLib.Harmony? _harmony;

    public ModContext(DiscoveredMod mod, ModSettingsStore store, Action<string> sink, Action<string> errors, Func<ModHost.PickBox?>? box = null)
    {
        Game = new ModGame(box ?? (() => null));
        Manifest = mod.Manifest;
        Directory = mod.Directory;
        Settings = new ModSettings(mod.Id, store, mod.Manifest.Settings, mod.Manifest.Choices);
        Log = new ModLog(mod.Id, sink, errors);
        DataDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(store.Path)) ?? ".";
    }

    public ModGame Game { get; }
    public string DataDirectory { get; }

    public ModManifest Manifest { get; }
    public string Directory { get; }
    public ModEvents Events { get; } = new();
    public ModDecisions Decisions { get; } = new();
    public ModActions Actions { get; } = new();
    public ModUI UI { get; } = new();
    public ModSettings Settings { get; }
    public ModLog Log { get; }
    public HarmonyLib.Harmony Harmony => _harmony ??= new HarmonyLib.Harmony(Manifest.Id);
    internal bool HarmonyUsed => _harmony != null;
}

/// <summary>
/// Loads a mod's assembly on its own, but hands it the game's copy of anything the game already
/// has - Mordor.Core, Mordor.Modding, Harmony, Avalonia - so the types it uses are the game's.
/// </summary>
internal sealed class ModLoadContext : AssemblyLoadContext
{
    private readonly string _dir;
    private static readonly HashSet<string> Platform = new(
        ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => System.IO.Path.GetFileNameWithoutExtension(p)),
        StringComparer.OrdinalIgnoreCase);

    public ModLoadContext(string name, string dir) : base("mod:" + name) { _dir = dir; }

    protected override Assembly? Load(AssemblyName name)
    {
        string simple = name.Name ?? "";
        if (Platform.Contains(simple)) return null;   // the game's own copy
        if (Default.Assemblies.Any(a => string.Equals(a.GetName().Name, simple, StringComparison.OrdinalIgnoreCase))) return null;
        string candidate = System.IO.Path.Combine(_dir, simple + ".dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}

/// <summary>
/// The mod loader and the one place the game asks its hooks. docs/MODDING.md is the design.
///
/// With no mod enabled - the default - every question here gets the original's answer and every
/// event goes nowhere, so the game is the faithful port. Each call into a mod is guarded: a mod
/// that throws, from Load or from any hook, is disabled for the rest of the session, its Harmony
/// patches are removed, the reason goes to the log, and the game carries on with the original's
/// answer.
/// </summary>
public sealed class ModHost
{
    /// <summary>The host the game asks. Until the game starts one it is empty: no mods, original answers.</summary>
    public static ModHost Active { get; set; } = Empty();

    private readonly Action<string> _log;
    private readonly Action<string> _problems;
    private readonly List<DiscoveredMod> _discovered = new();
    private readonly List<LoadedMod> _loaded = new();

    /// <param name="log">Every line: what loaded, each Harmony patch, a mod's own Info - and the problems.</param>
    /// <param name="problems">
    /// The problems again, and only those: a mod that cannot be read or loaded, one disabled for
    /// throwing, a mod's own Error. The game sends these to crash.log as well; the routine lines
    /// go to the mods log alone. Null: <paramref name="log"/> has them once, and that is all.
    /// </param>
    public ModHost(ModSettingsStore settings, Action<string> log, Action<string>? problems = null)
    {
        Settings = settings;
        _log = log;
        _problems = problems ?? (_ => { });
        Settings.Changed += (_, _) => Changed?.Invoke();
    }

    /// <summary>A line worth keeping beside the crashes: to the log and to the problems sink.</summary>
    private void Problem(string line)
    {
        _log(line);
        try { _problems(line); } catch { }
    }

    /// <summary>A host with nothing in it and a settings store that never writes.</summary>
    public static ModHost Empty() =>
        new(new ModSettingsStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-no-mods.ini")) { Writable = false }, _ => { });

    public ModSettingsStore Settings { get; }

    /// <summary>
    /// A question box: its title, its prompt, the lines to pick from, and a control of a mod's window to
    /// show it over (or null), and the line to start on selected; the index picked, or -1 for Cancel.
    /// </summary>
    public delegate Task<int> PickBox(string title, string prompt, IReadOnlyList<string> options, object? over, int selected);

    /// <summary>
    /// The game's "Cast on Who?" box, behind <see cref="ModGame.AskCastOnWho"/>. Set by the game as it
    /// starts; null (a headless run, the probes) answers every question with Cancel.
    /// </summary>
    public PickBox? CastOnWhoBox { get; set; }

    /// <summary>A mod setting changed, or a mod was disabled: whatever reads them live should look again.</summary>
    public event Action? Changed;

    public IReadOnlyList<DiscoveredMod> Discovered => _discovered;
    public IReadOnlyList<LoadedMod> Loaded => _loaded;
    /// <summary>The mods that are loaded, switched on and have not been disabled for throwing.</summary>
    public IEnumerable<LoadedMod> Live => _loaded.Where(m => m.Running);

    // ------------------------------------------------------------------------------ discovery

    /// <summary>
    /// Where mods are looked for, in order: <c>MORDOR_MODS</c> (one or more folders separated by
    /// ';') when it is set, and otherwise the <c>mods</c> folder beside the game folder - beside
    /// <c>saves</c>, where settings.ini lives - and then the <c>mods</c> folder beside the program,
    /// where the build puts the mods that come with the port. A mod id found twice is taken from
    /// the first folder.
    /// </summary>
    public static List<string> DefaultRoots(string gameDir, string programDir)
    {
        string? env = Environment.GetEnvironmentVariable("MORDOR_MODS");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return new List<string>
        {
            System.IO.Path.GetFullPath(System.IO.Path.Combine(gameDir, "..", "mods")),
            System.IO.Path.Combine(programDir, "mods"),
        };
    }

    /// <summary>Find every <c>mods/*/mod.json</c> under these folders.</summary>
    public void Discover(IEnumerable<string> roots)
    {
        foreach (string root in roots)
        {
            if (!System.IO.Directory.Exists(root)) continue;
            foreach (string dir in System.IO.Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string file = System.IO.Path.Combine(dir, ModApi.ManifestFile);
                if (!File.Exists(file)) continue;
                ModManifest m;
                string? problem;
                try { m = ModManifest.Read(file); problem = m.Problem; }
                catch (Exception ex)
                {
                    m = new ModManifest { Id = System.IO.Path.GetFileName(dir), Name = System.IO.Path.GetFileName(dir) };
                    problem = "mod.json could not be read: " + ex.Message;
                }
                if (_discovered.Any(d => string.Equals(d.Id, m.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Problem($"mods: {file} has the same id as one already found ({m.Id}); skipped");
                    continue;
                }
                _discovered.Add(new DiscoveredMod(dir, m, problem));
                if (problem != null) Problem($"mods: {file}: {problem}");
            }
        }
    }

    public bool IsEnabled(string id) => Settings.IsEnabled(id);

    // --------------------------------------------------------------------------------- loading

    /// <summary>Load and start every enabled mod that was found. Safe to call once.</summary>
    public void LoadEnabled()
    {
        foreach (var d in _discovered)
        {
            if (d.Problem != null || !IsEnabled(d.Id)) continue;
            if (_loaded.Any(l => l.Id == d.Id)) continue;
            Load(d, null, null);
        }
    }

    /// <summary>
    /// Entry objects made for a mod ticked on on the main menu that cannot be switched without a restart (no
    /// <see cref="IModLifecycle"/>): kept, so switching it on again does not load its assembly again.
    /// It runs from the next start.
    /// </summary>
    private readonly Dictionary<string, IMod> _waiting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each mod's entry type, its assembly loaded once in a context of its own (for its entry object, or its settings' migration while it is off).</summary>
    private readonly Dictionary<string, Type> _entryTypes = new(StringComparer.OrdinalIgnoreCase);

    private Type EntryType(DiscoveredMod d)
    {
        if (_entryTypes.TryGetValue(d.Id, out var known)) return known;
        string dll = System.IO.Path.Combine(d.Directory, d.Manifest.Assembly);
        if (!File.Exists(dll)) throw new FileNotFoundException("the mod's assembly is not there", dll);
        var alc = new ModLoadContext(d.Id, d.Directory);
        var asm = alc.LoadFromAssemblyPath(System.IO.Path.GetFullPath(dll));
        var type = asm.GetType(d.Manifest.EntryType, throwOnError: true)!;
        _entryTypes[d.Id] = type;
        return type;
    }

    /// <summary>The mod's assembly loaded in a context of its own, and its entry object made.</summary>
    private IMod Make(DiscoveredMod d)
    {
        var type = EntryType(d);
        if (Activator.CreateInstance(type) is not IMod mod)
            throw new InvalidCastException($"{d.Manifest.EntryType} does not implement IMod");
        return mod;
    }

    /// <summary>
    /// Bring a mod's settings in settings.ini up to its current version without loading it (API
    /// version 11): its entry type's <c>public static void Migrate(ModSettings)</c>, when it has
    /// one, run on its settings - for a settings window opened while the mod is off, so it shows
    /// what the mod will use. Nothing for a mod with a problem or no such method; a failure is
    /// written to the log and changes nothing more. A mod that is loaded has run its own already
    /// (in Load), and running it again finds nothing to do.
    /// </summary>
    public void MigrateSettings(DiscoveredMod d)
    {
        if (d.Problem != null) return;
        try
        {
            var m = EntryType(d).GetMethod("Migrate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, new[] { typeof(ModSettings) });
            m?.Invoke(null, new object[] { new ModSettings(d.Id, Settings, d.Manifest.Settings, d.Manifest.Choices) });
        }
        catch (Exception ex) { _log($"mod {d.Id}: migrating its settings failed - {(ex.InnerException ?? ex).Message}"); }
    }

    /// <summary>
    /// Load one mod: its entry made (or <paramref name="made"/>), handed the game in play, Load
    /// called, then OnEnable for a lifecycle mod. <paramref name="makeFailed"/>: making it already
    /// threw, and the mod is entered as disabled with that reason.
    /// </summary>
    private LoadedMod Load(DiscoveredMod d, IMod? made, Exception? makeFailed)
    {
        var ctx = new ModContext(d, Settings, _log, Problem, () => CastOnWhoBox);
        var lm = new LoadedMod(d, ctx);
        // in the order they were found, as at a start - one loaded on the main menu takes its place
        // among the others, so its buttons and columns stand where they will at the next start
        int order = _discovered.IndexOf(d);
        int at = _loaded.FindIndex(l => _discovered.IndexOf(l.Source) > order);
        if (at < 0) _loaded.Add(lm); else _loaded.Insert(at, lm);
        // loaded after a game has begun (none should be: only the main menu switches): it sees it, as the others do
        if (GameParty != null) ctx.Game.Set(GameParty, _spells, _item);
        try
        {
            if (makeFailed != null) throw makeFailed;
            var mod = made ?? Make(d);
            lm.Instance = mod;
            if (d.Manifest.GameVersion != "" && d.Manifest.GameVersion != ModApi.GameVersion)
                Problem($"mod {d.Id}: built for game version {d.Manifest.GameVersion}, this is {ModApi.GameVersion}; loading anyway");
            mod.Load(ctx);
            _log($"mod {d.Id}: loaded {d.Manifest.Title} from {d.Directory}");
        }
        catch (Exception ex)
        {
            Fault(lm, "Load", ex);
            return lm;
        }
        if (lm.Instance is IModLifecycle life) Guard(lm, "OnEnable", life.OnEnable);
        if (!lm.Faulted) NotePatches(lm);
        return lm;
    }

    // ---------------------------------------------------------- switching on the main menu

    /// <summary>
    /// The main menu's Mods page's Enabled box: kept in settings.ini, and done now where it can be.
    /// Only the main menu calls it, with no game going and so no game window open: a game begun
    /// after it builds its windows from the mods running then, and nothing already built has to
    /// follow (the Control Panel's page greys the box).
    ///
    /// On: a mod not loaded yet is loaded now - its Load, then its OnEnable - if it is an
    /// <see cref="IModLifecycle"/> mod (its assembly is loaded to see; one without the lifecycle
    /// is kept made, not loaded, for the next start); one switched off earlier in the session is
    /// made active again (OnEnable, which puts back its Harmony patches) without a second Load.
    /// Off: a lifecycle mod stops being called at once, its OnDisable runs and its Harmony patches
    /// are removed.
    ///
    /// A mod without the lifecycle, or one disabled for throwing, keeps its present state until the
    /// next start (<see cref="ModSwitchResult.NextStart"/>).
    /// </summary>
    public ModSwitchResult Switch(string id, bool on)
    {
        var d = _discovered.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        var lm = d == null ? null : _loaded.FirstOrDefault(l => l.Id == d.Id);
        ModSwitchResult result;
        if (d == null || d.Problem != null) result = ModSwitchResult.NextStart;
        else if (on)
        {
            if (lm != null)
            {
                if (lm.Faulted) result = ModSwitchResult.NextStart;
                else if (lm.Active) result = ModSwitchResult.Now;
                else
                {
                    lm.Active = true;
                    _log($"mod {lm.Id}: switched on");
                    if (lm.Instance is IModLifecycle life) Guard(lm, "OnEnable", life.OnEnable);
                    if (!lm.Faulted) { lm.Patches.Clear(); NotePatches(lm); }
                    result = lm.Faulted ? ModSwitchResult.Failed : ModSwitchResult.Now;
                }
            }
            else
            {
                IMod? made = null; Exception? failed = null;
                if (!_waiting.TryGetValue(d.Id, out made))
                    try { made = Make(d); } catch (Exception ex) { failed = ex; }
                if (failed == null && made is not IModLifecycle)
                {
                    _waiting[d.Id] = made!;
                    result = ModSwitchResult.NextStart;
                }
                else
                {
                    _waiting.Remove(d.Id);
                    var now = Load(d, made, failed);
                    result = now.Faulted ? ModSwitchResult.Failed : ModSwitchResult.Now;
                }
            }
        }
        else if (lm == null || !lm.Running) result = ModSwitchResult.Now;   // not running already
        else if (!lm.SwitchesLive) result = ModSwitchResult.NextStart;
        else
        {
            // not called again from here on, whatever OnDisable does
            lm.Active = false;
            if (lm.Instance is IModLifecycle life) Guard(lm, "OnDisable", life.OnDisable);
            if (lm.Context.HarmonyUsed && !lm.Faulted)
            {
                try { lm.Context.Harmony.UnpatchAll(lm.Id); }
                catch (Exception un) { Problem($"mod {lm.Id}: removing its Harmony patches failed: {un.Message}"); }
            }
            lm.Patches.Clear();
            _log($"mod {lm.Id}: switched off");
            result = ModSwitchResult.Now;
        }
        if (d != null) Settings.SetEnabled(d.Id, on);   // raises Changed
        return result;
    }

    /// <summary>
    /// What the Mods page says of a mod, given whether its Enabled box is ticked: running or not now,
    /// and whether that is waiting for the next start.
    /// </summary>
    public string StatusOf(string id, bool enabledNow)
    {
        var d = _discovered.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        if (d == null) return "Not found.";
        if (d.Problem != null) return "Cannot be loaded: " + d.Problem;
        var lm = _loaded.FirstOrDefault(l => l.Id == d.Id);
        if (lm is { Faulted: true }) return "Switched off for this session because it went wrong (" + lm.FaultReason + "). See crash.log.";
        bool running = lm is { Running: true };
        if (running == enabledNow) return running ? "Running." : "Off.";
        // a mod that cannot be switched without a restart
        return running ? "Running now; off from the next start." : "Starts the next time Mordor starts.";
    }

    private void NotePatches(LoadedMod lm)
    {
        if (!lm.Context.HarmonyUsed) return;
        foreach (var method in HarmonyLib.Harmony.GetAllPatchedMethods().ToList())
        {
            var info = HarmonyLib.Harmony.GetPatchInfo(method);
            if (info == null) continue;
            void Note(IEnumerable<HarmonyLib.Patch> patches, string kind)
            {
                foreach (var p in patches.Where(p => p.owner == lm.Id))
                {
                    string what = $"{method.DeclaringType?.FullName}.{method.Name} ({kind}: {p.PatchMethod.DeclaringType?.Name}.{p.PatchMethod.Name})";
                    lm.Patches.Add(what);
                    _log($"mod {lm.Id}: Harmony patch {what}");
                }
            }
            Note(info.Prefixes, "prefix");
            Note(info.Postfixes, "postfix");
            Note(info.Transpilers, "transpiler");
            Note(info.Finalizers, "finalizer");
        }
    }

    /// <summary>Disable a mod for the session: log why, take its patches out, tell the game.</summary>
    private void Fault(LoadedMod lm, string where, Exception ex)
    {
        if (lm.Faulted) return;
        lm.Faulted = true;
        lm.FaultReason = $"{where}: {(ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message)}";
        Problem($"mod {lm.Id}: disabled for this session - it threw in {where}{Environment.NewLine}{ex}");
        if (lm.Context.HarmonyUsed)
        {
            try { lm.Context.Harmony.UnpatchAll(lm.Id); Problem($"mod {lm.Id}: its Harmony patches were removed"); }
            catch (Exception un) { Problem($"mod {lm.Id}: removing its Harmony patches failed: {un.Message}"); }
        }
        try { Changed?.Invoke(); } catch { }
    }

    /// <summary>Run one mod's hook; if it throws, the mod is disabled and <paramref name="fallback"/> is the answer.</summary>
    private T Guard<T>(LoadedMod lm, string where, Func<T> call, T fallback)
    {
        try { return call(); }
        catch (Exception ex) { Fault(lm, where, ex); return fallback; }
    }

    private void Guard(LoadedMod lm, string where, Action call)
    {
        try { call(); }
        catch (Exception ex) { Fault(lm, where, ex); }
    }

    // ------------------------------------------------------------------------------ the hooks

    /// <summary>
    /// The Store's Select Alignment frame is up and another row was clicked: may it be taken?
    /// False, the original (FRMCTYS.BAS:332-335), unless a mod says otherwise.
    /// </summary>
    public bool StoreAllowsRowChangeWhileChoosing()
    {
        bool any = false;
        foreach (var lm in Live.ToList())
            if (lm.Context.Decisions.StoreAllowsRowChangeWhileChoosing is { } ask)
                any |= Guard(lm, "StoreAllowsRowChangeWhileChoosing", ask, false);
        return any;
    }

    /// <summary>ALT-p in town: the original's on-hand pool unless a mod answers otherwise.</summary>
    public CityAltPAnswer CityAltP(CityAltPRequest request)
    {
        foreach (var lm in Live.ToList())
            if (lm.Context.Decisions.CityAltP is { } ask
                && Guard(lm, "CityAltP", () => ask(request), null) is { } answer)
                return answer;
        return CityAltPAnswer.Original;
    }

    /// <summary>
    /// How many lines the Messages window keeps: <paramref name="original"/> (mc04F2's twenty)
    /// unless a mod answers otherwise. The first non-null answer decides, at least one.
    /// </summary>
    public int MessagesKept(int original)
    {
        foreach (var lm in Live.ToList())
            if (lm.Context.Decisions.MessagesKept is { } ask
                && Guard<int?>(lm, "MessagesKept", () => ask(), null) is int n)
                return Math.Max(1, n);
        return original;
    }

    /// <summary>
    /// A caption was set in the dungeon. An empty one is not news; with no mod listening this does
    /// nothing at all.
    /// </summary>
    public void CaptionShown(CaptionKind kind, string? text, Character? who)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var live = Live.ToList();
        if (live.Count == 0) return;
        var c = new Caption(kind, text, who);
        foreach (var lm in live) Guard(lm, "CaptionShown", () => lm.Context.Events.RaiseCaptionShown(c));
    }

    /// <summary>
    /// The party is back in town from the dungeon: the first mod with an answer to
    /// <see cref="ModDecisions.TownArrival"/> decides what the town does (a Party Deposit, the
    /// Bank's own rule), and then every mod hears of it (<see cref="ModEvents.ArrivedInTown"/>).
    /// Returns what the deposit moved and the line to write, if any. With no mod loaded nothing
    /// is asked and nothing moves: the original's arrival.
    /// </summary>
    public (VbCurrency Deposited, string? Line) ArrivedInTown(Party party, Character selected, TownArrivalKind how)
    {
        var live = Live.ToList();
        if (live.Count == 0) return (VbCurrency.Zero, null);
        var arrival = new TownArrival(party, selected, how);
        VbCurrency deposited = 0;
        string? line = null;
        foreach (var lm in live)
        {
            if (lm.Context.Decisions.TownArrival is not { } ask
                || Guard(lm, "TownArrival", () => ask(arrival), null) is not { } answer) continue;
            if (answer.Action == TownArrivalAction.PartyDeposit)
                deposited = Party.PartyDeposit(party.Members);
            else if (answer.Action == TownArrivalAction.PartyPool && answer.PoolTo is { } to && party.Members.Contains(to))
                deposited = Party.PoolOnHandToBank(party.Members, to);
            if (deposited > 0 && answer.Message is { } say)
                line = Guard<string?>(lm, "TownArrival message", () => say(deposited), null);
            break;
        }
        foreach (var lm in Live.ToList()) Guard(lm, "ArrivedInTown", () => lm.Context.Events.RaiseArrivedInTown(arrival));
        return (deposited, string.IsNullOrWhiteSpace(line) ? null : line);
    }

    /// <summary>
    /// The party has gone down into the dungeon from town: each live mod's
    /// <see cref="ModActions.EnteredDungeonFromTown"/> in load order, each awaited before the next,
    /// each with a <see cref="DungeonContext"/> of its own made by <paramref name="make"/> and
    /// closed when its hook is done. With no mod hooked the task is already complete and
    /// <paramref name="make"/> is never called; a hook that finishes without awaiting anything
    /// leaves it complete too.
    /// </summary>
    public Task EnteredDungeonFromTown(Func<DungeonContext> make) =>
        RunActions("EnteredDungeonFromTown", a => a.EnteredDungeonFromTown, make);

    /// <summary>
    /// The party is taking the stairs up out of level 1: each live mod's
    /// <see cref="ModActions.LeavingByStairs"/>, as <see cref="EnteredDungeonFromTown"/>.
    /// </summary>
    public Task LeavingByStairs(Func<DungeonContext> make) =>
        RunActions("LeavingByStairs", a => a.LeavingByStairs, make);

    private Task RunActions(string where, Func<ModActions, Func<DungeonContext, Task>?> pick, Func<DungeonContext> make)
    {
        var hooked = Live.Where(lm => pick(lm.Context.Actions) != null).ToList();
        if (hooked.Count == 0) return Task.CompletedTask;
        // one moment at a time: a call made while another's hooks are still running runs none
        // (the dungeon also refuses to start one meanwhile; this is the host's own guard)
        if (_actionRunning) { _log($"mods: {where} asked while a mod action was still running; not run"); return Task.CompletedTask; }
        _actionRunning = true;
        return Run();

        async Task Run()
        {
            try
            {
                foreach (var lm in hooked)
                {
                    if (!lm.Running || pick(lm.Context.Actions) is not { } hook) continue;
                    var ctx = make();
                    // a throw before the hook's first await and a task that faults later are the same
                    // to the game: the mod is disabled and the next one is asked
                    try { await hook(ctx); }
                    catch (Exception ex) { Fault(lm, where, ex); }
                    finally { ctx.Close(); }
                }
            }
            finally { _actionRunning = false; }
        }
    }

    /// <summary>A moment's hooks (<see cref="ModActions"/>) are running now.</summary>
    private bool _actionRunning;

    /// <summary>Whether a moment's hooks are still running (the dungeon waits on them).</summary>
    public bool ActionRunning => _actionRunning;

    public void MessageLogged(string line)
    {
        foreach (var lm in Live.ToList()) Guard(lm, "MessageLogged", () => lm.Context.Events.RaiseMessageLogged(line));
    }

    public void CharacterSwitched(Character now, bool inDungeon)
    {
        var s = new CharacterSwitch(now, inDungeon);
        foreach (var lm in Live.ToList()) Guard(lm, "CharacterSwitched", () => lm.Context.Events.RaiseCharacterSwitched(s));
        GameChanged();
    }

    /// <summary>
    /// Hits taken off a monster, or a monster killed, by a party member - their blow or spell - or
    /// one of their companions, credited to the owner (<see cref="ModEvents.DamageDealt"/>; the
    /// combat engine's <c>DamageDealt</c>): a damage event per monster struck, a kill event per
    /// monster killed. Each live mod's handler is guarded. With no mod loaded, nothing.
    /// </summary>
    public void DamageDealt(CombatDamage d)
    {
        var live = Live.ToList();
        if (live.Count == 0) return;
        foreach (var lm in live) Guard(lm, "DamageDealt", () => lm.Context.Events.RaiseDamageDealt(d));
    }

    /// <summary>
    /// Experience put on a party member's total (<see cref="ModEvents.ExperienceGained"/>; the combat
    /// engine's <c>ExperienceGained</c>): one event per award. Each live mod's handler is guarded.
    /// With no mod loaded, nothing.
    /// </summary>
    public void ExperienceGained(CombatExperience e)
    {
        var live = Live.ToList();
        if (live.Count == 0) return;
        foreach (var lm in live) Guard(lm, "ExperienceGained", () => lm.Context.Events.RaiseExperienceGained(e));
    }

    /// <summary>A character was deleted (<see cref="ModEvents.CharacterDeleted"/>). With no mod loaded, nothing.</summary>
    public void CharacterDeleted(string name)
    {
        // every loaded mod, switched off or not: one that keeps something per character lets the
        // deleted one's go, or a new character of the same name would take it over when it is
        // switched back on (the Kaizen buff lists, the DPS Meter's tallies)
        foreach (var lm in _loaded.Where(m => !m.Faulted).ToList()) Guard(lm, "CharacterDeleted", () => lm.Context.Events.RaiseCharacterDeleted(name));
    }

    /// <summary>The party in play (<see cref="ModGame"/>), or null: none.</summary>
    public Party? GameParty { get; private set; }

    /// <summary>
    /// A game began (the party, the spell and item tables) or, with a null party, ended: every
    /// mod's <see cref="IModContext.Game"/> is set to it and told (<see cref="ModGame.Changed"/>).
    /// </summary>
    public void SetGame(Party? party, IReadOnlyList<Spell>? spells = null, Func<short, Item?>? item = null)
    {
        GameParty = party;
        _spells = party == null ? Array.Empty<Spell>() : spells ?? Array.Empty<Spell>();
        _item = party == null || item == null ? _ => null : item;
        foreach (var lm in _loaded)
            lm.Context.Game.Set(party, _spells, _item);
        GameChanged();
    }

    /// <summary>The game in play's tables (<see cref="SetGame"/>), for a mod loaded after the game began.</summary>
    private IReadOnlyList<Spell> _spells = Array.Empty<Spell>();
    private Func<short, Item?> _item = _ => null;

    /// <summary>
    /// The party was drawn again or another character took command: every live mod's
    /// <see cref="ModGame.Changed"/>. With no mod loaded, nothing.
    /// </summary>
    public void GameChanged()
    {
        foreach (var lm in Live.ToList()) Guard(lm, "Game.Changed", () => lm.Context.Game.RaiseChanged());
    }

    /// <summary>A sale put an item on the Store's shelf. With no mod loaded, nothing.</summary>
    public void StoreShelved(Character ch, Item item, ItemAlignment alignment, Action<string> say)
    {
        var live = Live.ToList();
        if (live.Count == 0) return;
        var s = new StoreShelf(ch, item, alignment, say);
        foreach (var lm in live) Guard(lm, "StoreShelved", () => lm.Context.Events.RaiseStoreShelved(s));
    }

    public void StoreRowClicked(Character ch, Item item, bool whileChoosing, bool taken)
    {
        var c = new StoreRowClick(ch, item, whileChoosing, taken);
        foreach (var lm in Live.ToList()) Guard(lm, "StoreRowClicked", () => lm.Context.Events.RaiseStoreRowClicked(c));
    }

    /// <summary>
    /// Whether a Store Buy row is shown: true (the original) unless a mod answers false.
    /// </summary>
    public bool StoreRowVisible(Character ch, Item item, bool usable)
    {
        StoreRowQuery? q = null;
        foreach (var lm in Live.ToList())
            if (lm.Context.Decisions.StoreRowVisible is { } ask
                && Guard<bool?>(lm, "StoreRowVisible", () => ask(q ??= new StoreRowQuery(ch, item, usable)), null) == false)
                return false;
        return true;
    }

    /// <summary>
    /// The Store Buy list's rows in the order to draw them: <paramref name="rows"/> as given (the
    /// shelf's order, the original's) unless a mod answers <see cref="ModDecisions.StoreRowOrder"/>.
    /// An answer that is not the same rows reordered disables its mod and the next is asked.
    /// </summary>
    public List<Item> StoreRowOrder(Character ch, List<Item> rows, Func<short, Item?> lookup)
    {
        var live = Live.ToList();
        if (live.Count == 0) return rows;
        StoreOrderQuery? q = null;
        foreach (var lm in live)
        {
            if (lm.Context.Decisions.StoreRowOrder is not { } ask) continue;
            var answer = Guard(lm, "StoreRowOrder", () => ask(q ??= new StoreOrderQuery(ch, rows.ToList().AsReadOnly(), lookup)), null);
            if (answer == null) continue;
            if (!SameRows(rows, answer))
            {
                Fault(lm, "StoreRowOrder", new InvalidOperationException("its answer is not the rows it was given, reordered"));
                continue;
            }
            return answer.ToList();
        }
        return rows;
    }

    /// <summary>The same Item objects, each as many times, in any order.</summary>
    private static bool SameRows(List<Item> rows, IReadOnlyList<Item> answer)
    {
        if (answer.Count != rows.Count) return false;
        var count = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance);
        foreach (var r in rows) count[r] = count.TryGetValue(r, out int n) ? n + 1 : 1;
        foreach (var a in answer)
        {
            if (a == null || !count.TryGetValue(a, out int n) || n == 0) return false;
            count[a] = n - 1;
        }
        return true;
    }

    /// <summary>
    /// Whether a Store Buy header is sortable, and its mark: the first mod's answer to
    /// <see cref="ModUI.StoreHeaderSort"/>, with that mod (which a click goes to), or null - the
    /// original's plain header.
    /// </summary>
    public (LoadedMod Mod, StoreSortArrow Arrow)? StoreHeaderSort(string header)
    {
        foreach (var lm in Live.ToList())
            if (lm.Context.UI.StoreHeaderSort is { } ask
                && Guard<StoreSortArrow?>(lm, "StoreHeaderSort", () => ask(header), null) is { } arrow)
                return (lm, arrow);
        return null;
    }

    /// <summary>A sortable Store header was clicked: to the mod that owns it. Nothing for a disabled mod.</summary>
    public void StoreHeaderClicked(LoadedMod lm, string header)
    {
        if (lm.Running && lm.Context.UI.StoreHeaderClicked is { } click) Guard(lm, "StoreHeaderClicked", () => click(header));
    }

    /// <summary>
    /// The first mod's answer to <see cref="ModDecisions.CoverShortfall"/>, or null (the original:
    /// charPay refuses). Not asked with nothing short, in a party of one, with the payer below
    /// ground (the town's charges only), or with the payer not in the party.
    /// </summary>
    private (LoadedMod Mod, ShortfallAnswer Answer)? AskShortfall(Party party, Character payer, long cost)
    {
        if (cost <= payer.Purse || party.Size < 2 || payer.DungeonLevel != 0 || !party.Members.Contains(payer)) return null;
        var live = Live.ToList();
        if (live.Count == 0) return null;
        var q = new ShortfallRequest(party, payer, cost, VbCurrency.SubtractSaturating(cost, payer.Purse));
        foreach (var lm in live)
            if (lm.Context.Decisions.CoverShortfall is { } ask
                && Guard(lm, "CoverShortfall", () => ask(q), null) is { } answer)
                return (lm, answer);
        return null;
    }

    /// <summary>
    /// Whether a charge the payer's purse falls short of would be covered by the party
    /// (<see cref="ModDecisions.CoverShortfall"/> and <c>Party.PlanShortfall</c>). Moves nothing:
    /// for the town's tests of the purse ahead of the charge. False with no mod: the original.
    /// </summary>
    public bool WouldCoverShortfall(Party party, Character payer, long cost) =>
        AskShortfall(party, payer, cost) is { } asked && Party.PlanShortfall(party.Members, payer, cost, asked.Answer.From) != null;

    /// <summary>
    /// Before a town charge: if the payer is short and a mod asks for it, the rest of the party
    /// hands over exactly the shortfall from the givers' banks (<c>Party.CoverShortfall</c>, the
    /// answer's <see cref="ShortfallAnswer.From"/>). Returns the Messages lines, one per giver, when
    /// gold moved; null when nothing did (no mod, nothing short, or the givers together cannot
    /// cover it - the original's refusal follows).
    /// </summary>
    public List<string>? CoverShortfall(Party party, Character payer, long cost)
    {
        if (AskShortfall(party, payer, cost) is not { } asked) return null;
        var gifts = Party.CoverShortfall(party.Members, payer, cost, asked.Answer.From);
        if (gifts == null || gifts.Count == 0) return null;
        var lines = new List<string>();
        if (asked.Answer.Line is { } say)
            foreach (var g in gifts)
                if (Guard<string?>(asked.Mod, "CoverShortfall line", () => say(g, payer), null) is { } line
                    && !string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
        return lines;
    }

    /// <summary>What to draw after a pack line: the first mod's answer, or null (the original: nothing).</summary>
    public PackRowDecoration? PackRowDecoration(Character ch, Item item, InventorySlot slot, int index)
    {
        PackRowInfo? info = null;
        foreach (var lm in Live.ToList())
            if (lm.Context.UI.PackRowDecoration is { } deco
                && Guard(lm, "PackRowDecoration", () => deco(info ??= new PackRowInfo(ch, item, slot, index)), null) is { } d)
                return d;
        return null;
    }

    /// <summary>What to draw after a line of the Bank's list: the first mod's answer, or null (the original: nothing).</summary>
    public PackRowDecoration? BankRowDecoration(Character ch, Item item, InventorySlot slot, int vaultSlot)
    {
        BankRowInfo? info = null;
        foreach (var lm in Live.ToList())
            if (lm.Context.UI.BankRowDecoration is { } deco
                && Guard(lm, "BankRowDecoration", () => deco(info ??= new BankRowInfo(ch, item, slot, vaultSlot)), null) is { } d)
                return d;
        return null;
    }

    /// <summary>
    /// Whose bank account gold the town pays goes into (<see cref="ModDecisions.TownIncomeBank"/>):
    /// the first mod's answer, or null (the original: the seller's hand). An answer not in the
    /// party counts as null.
    /// </summary>
    public Character? TownIncomeBank(Party party, Character ch, VbCurrency amount, TownIncomeKind kind)
    {
        TownIncome? q = null;
        foreach (var lm in Live.ToList())
            if (lm.Context.Decisions.TownIncomeBank is { } ask
                && Guard(lm, "TownIncomeBank", () => ask(q ??= new TownIncome(party, ch, amount, kind)), null) is { } who)
                return party.Members.Contains(who) ? who : null;
        return null;
    }

    /// <summary>A Messages line's colour: the first mod's answer, or null (the original: the window's own).</summary>
    public (byte R, byte G, byte B)? MessageLineColour(string text, MessageKind kind)
    {
        MessageLineInfo? info = null;
        foreach (var lm in Live.ToList())
            if (lm.Context.UI.MessageLineColour is { } ask
                && Guard(lm, "MessageLineColour", () => ask(info ??= new MessageLineInfo(text, kind)), null) is { } c)
                return c;
        return null;
    }

    /// <summary>Every Bank command the live mods have added, in load order.</summary>
    public IEnumerable<(LoadedMod Mod, BankCommand Command)> BankCommands =>
        Live.SelectMany(lm => lm.Context.UI.BankCommands.Select(c => (lm, c))).ToList();

    /// <summary>Whether a Bank command is there now; false for a mod that is disabled or throws.</summary>
    public bool IsShown(LoadedMod lm, BankCommand c) =>
        lm.Running && (c.IsShown == null || Guard(lm, "Bank command " + c.Label + ".IsShown", c.IsShown, false));

    /// <summary>Press a mod's Bank button. True when the vault changed; false for a mod that is disabled or throws.</summary>
    public bool Run(LoadedMod lm, BankCommand c, BankCommandContext ctx) =>
        lm.Running && Guard(lm, "Bank command " + c.Label, () => c.Run(ctx), false);

    /// <summary>A toolbar button's tooltip: the first mod's that gives one, or none (the original).</summary>
    public string? ToolbarTooltip(ToolbarButton button)
    {
        foreach (var lm in Live.ToList())
            if (lm.Context.UI.ToolbarTooltip is { } tip
                && Guard(lm, "ToolbarTooltip", () => tip(button), null) is { } text)
                return text;
        return null;
    }

    /// <summary>Every Objects &amp; Spells command the live mods have added, in load order.</summary>
    public IEnumerable<(LoadedMod Mod, ObjectsCommand Command)> ObjectsCommands =>
        Live.SelectMany(lm => lm.Context.UI.ObjectsCommands.Select(c => (lm, c))).ToList();

    public bool IsShown(LoadedMod lm, ObjectsCommand c) =>
        lm.Running && (c.IsShown == null || Guard(lm, c.Label + ".IsShown", c.IsShown, false));

    /// <summary>Press a mod's Objects &amp; Spells button. True when the pack changed.</summary>
    public bool Run(LoadedMod lm, ObjectsCommand c, ObjectsCommandContext ctx) =>
        lm.Running && Guard(lm, c.Label, () => c.Run(ctx), false);

    /// <summary>The Party Information columns the live mods show now, in load order.</summary>
    public List<(LoadedMod Mod, PartyColumn Column)> ShownPartyColumns() =>
        Live.SelectMany(lm => lm.Context.UI.PartyColumns.Select(c => (lm, c))).ToList()
            .Where(p => p.c.IsShown == null || Guard(p.lm, p.c.Header + ".IsShown", p.c.IsShown, false))
            .Where(p => p.lm.Running).ToList();

    /// <summary>One member's cell in a mod's party column; null if the mod throws (it is then disabled).</summary>
    public object? PartyCell(LoadedMod lm, PartyColumn c, Character member,
                             Func<short, Monster?>? monster = null, Func<short, Item?>? item = null) =>
        !lm.Running ? null : Guard<object?>(lm, c.Header + " column",
            () => c.Query(new PartyCellQuery(member, monster ?? (_ => null), item ?? (_ => null))), null);

    /// <summary>Every Store column the live mods have added, in load order, shown or not.</summary>
    public List<(LoadedMod Mod, StoreColumn Column)> StoreColumns() =>
        Live.SelectMany(lm => lm.Context.UI.StoreColumns.Select(c => (lm, c))).ToList();

    /// <summary>Whether a Store column is there now; false for a mod that is disabled or throws.</summary>
    public bool IsShown(LoadedMod lm, StoreColumn c) =>
        lm.Running && (c.IsShown == null || Guard(lm, "Store column " + c.Header + ".IsShown", c.IsShown, false));

    /// <summary>The Store columns shown now, in load order.</summary>
    public List<(LoadedMod Mod, StoreColumn Column)> ShownStoreColumns() =>
        StoreColumns().Where(p => IsShown(p.Mod, p.Column)).ToList();

    /// <summary>One row's cell in a mod's Store column; null if the mod throws (it is then disabled).</summary>
    public object? StoreCell(LoadedMod lm, StoreColumn c, StoreCellQuery q) =>
        !lm.Running ? null : Guard<object?>(lm, "Store column " + c.Header, () => c.Cell(q), null);

    /// <summary>Every Store check box the live mods have added, in load order.</summary>
    public IEnumerable<(LoadedMod Mod, StoreOption Option)> StoreOptions =>
        Live.SelectMany(lm => lm.Context.UI.StoreOptions.Select(o => (lm, o))).ToList();

    /// <summary>Whether a Store check box is there now; false for a mod that is disabled or throws.</summary>
    public bool IsShown(LoadedMod lm, StoreOption o) =>
        lm.Running && (o.IsShown == null || Guard(lm, "Store option " + o.Label + ".IsShown", o.IsShown, false));

    /// <summary>Whether a Store check box is ticked; false for a mod that is disabled or throws.</summary>
    public bool StoreOptionValue(LoadedMod lm, StoreOption o) =>
        lm.Running && Guard(lm, "Store option " + o.Label, o.Get, false);

    /// <summary>A Store check box was clicked. Nothing for a mod that is disabled; one that throws is disabled.</summary>
    public void SetStoreOption(LoadedMod lm, StoreOption o, bool value)
    {
        if (lm.Running) Guard(lm, "Store option " + o.Label, () => o.Set(value));
    }

    /// <summary>Every window the live mods offer.</summary>
    public IEnumerable<(LoadedMod Mod, ModWindow Window)> Windows =>
        Live.SelectMany(lm => lm.Context.UI.Windows.Select(w => (lm, w))).ToList();

    /// <summary>Build a mod window's content, or null if the mod throws (it is then disabled).</summary>
    public object? BuildWindow(LoadedMod lm, ModWindow w) =>
        !lm.Running ? null : Guard<object?>(lm, "window " + w.Title, () => w.Build(), null);

    /// <summary>Whether a mod window is offered now; false for a mod that is disabled or throws.</summary>
    public bool IsShown(LoadedMod lm, ModWindow w) =>
        lm.Running && (w.IsShown == null || Guard(lm, "window " + w.Title + ".IsShown", w.IsShown, false));

    // ------------------------------------------------------------------------------ save tags

    /// <summary>What a save records: "id@version" for each mod running now. Null with none.</summary>
    public List<string>? SaveTag()
    {
        var tag = Live.Select(m => m.Id + "@" + m.Manifest.Version).ToList();
        return tag.Count == 0 ? null : tag;
    }

    /// <summary>
    /// A save tag's "id@version" for a person to read: the mod's name from its mod.json when it is
    /// still to be found ("Quality of Life 1.0.0"), its id when it is not ("QoL 1.0.0").
    /// </summary>
    public string Describe(string tagEntry)
    {
        int at = tagEntry.IndexOf('@');
        string id = at < 0 ? tagEntry : tagEntry[..at];
        string version = at < 0 ? "" : tagEntry[(at + 1)..];
        var found = _discovered.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        string name = found != null && !string.IsNullOrWhiteSpace(found.Manifest.Name) ? found.Manifest.Name.Trim() : id;
        return version == "" ? name : name + " " + version;
    }

    /// <summary>The entries of a save's tag whose mod is not running now.</summary>
    public List<string> MissingFrom(IEnumerable<string>? tag)
    {
        if (tag == null) return new List<string>();
        var live = Live.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tag.Where(t => !live.Contains(t.Split('@')[0])).ToList();
    }
}
