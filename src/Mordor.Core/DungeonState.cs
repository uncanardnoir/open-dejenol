using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mordor.Core;

/// <summary>
/// Which rooms of the dungeon still hold monsters. The original does not roll dice as you walk: every
/// spawn area either holds a group or it does not, you fight it when you walk in, and once you have
/// killed it the room stays empty until it repopulates. From the help's tutorial: "when you kill the
/// monsters that are in a room and go back, it's empty. That's because they're dead! However, after a
/// period of time, the dungeon will repopulate with new monsters."
///
/// Shared by every character, like the automap, and stored beside the saves: each level's rooms in
/// a file of its own, dungeon-01.json, dungeon-02.json, ..., as the original keeps them in the level's
/// part of MDATA10, and the dungeon-wide facts in dungeon.json (<see cref="Save"/>).
/// </summary>
public sealed class DungeonState
{
    private Dictionary<string, RoomRecord> _rooms = new();

    /// <summary>
    /// Every room's records, keyed "level:areaId": what the original keeps in the level's own file,
    /// four Foes() records and a Fight record per area (fn0F88, MONSMOD.BAS:3921-3924). A room
    /// missing here reads as the file's zeroed records - nothing standing, a stamp of 0.
    /// A room is changed through <see cref="StoreRoom"/>, which is what marks its level to be written;
    /// a whole new set put here is written whole.
    /// </summary>
    public Dictionary<string, RoomRecord> Rooms { get => _rooms; set { _rooms = value; _whole = true; } }

    /// <summary>
    /// Saves from before the rooms were stored: when each room was emptied, as seconds since
    /// midnight. Read once and turned into records; never written.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, double>? Cleared { get; set; }

    /// <summary>Saves from before the rooms were stored: when each standing group appeared. Dropped on reading.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, double>? Spawned { get; set; }

    /// <summary>
    /// The deepest level anyone has ever reached. The original keeps this in the shared map file
    /// (MODULE3.BAS:1628 reads it, and writes it back when someone goes deeper) purely so the
    /// guild log can record who got there first.
    /// </summary>
    public int DeepestReached { get; set; }

    /// <summary>
    /// True - and the record is updated - if this is the first time anyone has stood this deep.
    /// The original only counts it when the character knows what level they are on.
    /// </summary>
    public bool NoteDepth(int level)
    {
        if (level <= DeepestReached) return false;
        DeepestReached = level;
        return true;
    }

    private static string Key(int level, int areaId) => $"{level}:{areaId}";

    /// <summary>
    /// VB's <c>Timer</c>: seconds since midnight, and nothing else. It carries no date, which is
    /// the root of every oddity below.
    /// </summary>
    public static double SecondsSinceMidnight(DateTime when) => when.TimeOfDay.TotalSeconds;

    /// <summary>
    /// Each level's respawn tally, <c>fn0DDC()</c> (MONSMOD.BAS:2211-2215): a number kept in the
    /// map reference file at record <c>1 + level</c>. sub04A3 seeds it with the level header's area
    /// count (MAINMOD.BAS:826-829); sub0C53 takes one off whenever a fight is won
    /// (MONSMOD.BAS:779), and sub0A91 puts one back when it refills a room that had been emptied
    /// (MONSMOD.BAS:714-716). A level missing here has its seed value.
    /// </summary>
    public Dictionary<int, int> Tally { get; set; } = new();

    public int TallyOf(int level, int headerAreas) => Tally.TryGetValue(level, out int t) ? t : headerAreas;

    /// <summary>
    /// How long a room takes to repopulate, sub0A91 (MONSMOD.BAS:698-700):
    /// <code>
    ///   l01B4 = mc01BA + ((5 * (DungeonLevel / 2)) * 60)        ' 1200 + 150 per level
    ///   l01B4 = l01B4 - ((gv1864.M5CF4 - fn0DDC()) * 5)
    ///   If l01B4 &lt; (mc01BA / 10) Then l01B4 = (mc01BA / 10)   ' 120 seconds at least
    /// </code>
    /// M5CF4 is the level header's area count and fn0DDC() the level's tally, so the time
    /// <b>falls</b> by five seconds for every room emptied and not yet refilled. The community's
    /// formula has it as five seconds <i>added</i> per spawn area; the port had followed it.
    /// </summary>
    public static TimeSpan RespawnDelay(int level, int headerAreas, int tally)
    {
        double t = 1200 + ((5 * (level / 2.0)) * 60);
        t = t - ((headerAreas - tally) * 5);
        if (t < 1200 / 10.0) t = 1200 / 10.0;
        return TimeSpan.FromSeconds(t);
    }

    public double RespawnSeconds(int level, int headerAreas) =>
        RespawnDelay(level, headerAreas, TallyOf(level, headerAreas)).TotalSeconds;

    /// <summary>fn0E38 (MONSMOD.BAS:2367-2389): a copy of the room's records.</summary>
    public RoomRecord LoadRoom(int level, int areaId) =>
        Rooms.TryGetValue(Key(level, areaId), out var r) ? r.Copy() : new RoomRecord();

    /// <summary>fn0F88 (MONSMOD.BAS:3908-3936): the room's records written back whole.</summary>
    public void StoreRoom(int level, int areaId, RoomRecord r)
    {
        if (areaId <= 0) return;   // "If p05BA <= False Then ... Exit Function" (3912)
        string key = Key(level, areaId);
        // the level's file is written at the next save only if this changed what it holds
        if (!Same(r, _rooms.TryGetValue(key, out var was) ? was : Zero)) _dirty.Add(level);
        _rooms[key] = r.Copy();
    }

    /// <summary>
    /// Would walking in now find monsters? sub0A91's test (MONSMOD.BAS:691-708), on the stored
    /// records, changing nothing.
    ///
    /// The timing is not elapsed time, and the difference is visible in play. The original compares
    /// <c>Abs(Timer - Foes(0).M4EE6)</c>, where <c>Timer</c> is **seconds since midnight** on the
    /// machine's own clock, so:
    ///
    /// - it does not know what day it is. Come back at the same time tomorrow and the room has not
    ///   repopulated, however long you were away
    /// - the comparison is an absolute value, so arriving 23 hours 40 minutes later reads as twenty
    ///   minutes *before* and the room stays empty
    /// - a side effect of that: any room emptied more than one respawn time after midnight
    ///   repopulates at midnight
    /// - a stored zero - a room emptied at exactly midnight, to the second - reads as "never set",
    ///   so the room repopulates on the next visit
    ///
    /// All four are faults, all four are the game's behaviour, and the port reproduces them.
    ///
    /// A standing group stays until it is more than twice the respawn time old; an empty room
    /// refills once its stamp is more than one respawn time old. Both tests are strict "&gt;".
    /// </summary>
    public bool IsOccupied(int level, int areaId, int headerAreas, DateTime now) =>
        areaId > 0 && (RefillDue(level, areaId, headerAreas, now) || LoadRoom(level, areaId).AnyStanding);

    /// <summary>
    /// Whether sub0A91 would call fn0D4D for this room now (MONSMOD.BAS:691-708): nothing standing,
    /// or a group more than twice the respawn time old, and then a stamp more than one respawn
    /// time old or never set.
    /// </summary>
    public bool RefillDue(int level, int areaId, int headerAreas, DateTime now)
    {
        if (areaId <= 0) return false;
        var rec = LoadRoom(level, areaId);
        double t = RespawnSeconds(level, headerAreas), nowSec = SecondsSinceMidnight(now), stamp = rec.Foes[0].Stamp;
        bool empty = !rec.AnyStanding;
        if (!empty && (Math.Abs(nowSec - stamp) > t * 2 || stamp == 0)) empty = true;
        return empty && (Math.Abs(nowSec - stamp) > t || stamp == 0);
    }

    /// <summary>sub0C29 (fn0DDC() + 1), MONSMOD.BAS:714-716.</summary>
    public void NoteRefilled(int level, int headerAreas) => Tally[level] = TallyOf(level, headerAreas) + 1;

    /// <summary>
    /// sub0C53 on the room's records (MONSMOD.BAS:775-779): no group is left, so every count is
    /// nought; <c>Foes(0).Ref = -1</c>, <c>Foes(0).M4EE6 = Timer</c>, the offer to join cleared, the
    /// records written, and one taken off the level's tally. The Fight record - the chest - stays.
    /// </summary>
    public void MarkCleared(int level, int areaId, DateTime now, int headerAreas)
    {
        var rec = LoadRoom(level, areaId);
        // "If Foes(0).Ref = -1 Then Exit Sub" (MONSMOD.BAS:769): a room already emptied is not
        // stamped again, nor the tally lowered twice
        if (rec.Foes[0].Ref == -1) return;
        foreach (var f in rec.Foes) f.GroupCount = 0;
        rec.Foes[0].Ref = -1;
        rec.Foes[0].Stamp = SecondsSinceMidnight(now);
        rec.Foes[0].JoinOffer = 0;
        StoreRoom(level, areaId, rec);
        Tally[level] = TallyOf(level, headerAreas) - 1;
    }

    // ------------------------------------------------------------------ the files

    /// <summary>
    /// The layout written: 2 is a file per level beside a small dungeon.json. The single dungeon.json
    /// of 0.1.8 and before has no number (read as 0) and holds the rooms itself.
    /// </summary>
    private const int Layout = 2;

    private static string PathFor(string savesDir) => Path.Combine(savesDir, "dungeon.json");

    /// <summary>A level's own file: dungeon-01.json for level 1, and so on.</summary>
    public static string LevelPath(string savesDir, int level) =>
        Path.Combine(savesDir, "dungeon-" + level.ToString("D2", CultureInfo.InvariantCulture) + ".json");

    private static readonly Regex LevelName = new(@"^dungeon-(-?\d+)\.json$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Whether a file is one of the levels' own files (dungeon-NN.json), not a character.</summary>
    public static bool IsLevelFile(string path) => LevelOfFile(Path.GetFileName(path)) != null;

    private static int? LevelOfFile(string name)
    {
        var m = LevelName.Match(name);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int level)
            ? level : null;
    }

    private static bool Split(string key, out int level, out int area)
    {
        level = area = 0;
        int colon = key.IndexOf(':');
        return colon > 0
            && int.TryParse(key.AsSpan(0, colon), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out level)
            && int.TryParse(key.AsSpan(colon + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out area);
    }

    // What the files on disk hold, so that a save writes only what changed since (Save).

    /// <summary>Levels whose rooms changed since their file was written.</summary>
    private readonly HashSet<int> _dirty = new();
    /// <summary>Each level's tally as its file holds it.</summary>
    private readonly Dictionary<int, int> _writtenTally = new();
    private int _writtenDeepest;
    /// <summary>dungeon.json is missing, unreadable or in the old layout, and is to be written.</summary>
    private bool _topDirty = true;
    /// <summary>
    /// Not read from the files - a new state, or one given a whole new set of rooms: the next save
    /// writes every level, and empties the file of a level it has nothing for, as the single file
    /// was once written whole.
    /// </summary>
    private bool _whole = true;
    /// <summary>The dungeon.json on disk is the old single file, not yet kept as dungeon.json.old.</summary>
    private bool _legacyPending;

    private static readonly RoomRecord Zero = new();
    private static readonly FoeSlot ZeroFoe = new();

    /// <summary>For the tests: called with each dungeon file's path just before it is written; a throw stands for the game dying there.</summary>
    public static Action<string>? BeforeWrite { get; set; }

    /// <summary>No indentation, and a field at its default (zero, or no value) left out.</summary>
    private static readonly JsonSerializerOptions Compact = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    /// <summary>dungeon.json: the dungeon-wide facts, and in the old layout everything.</summary>
    private sealed class TopFile
    {
        public int Format { get; set; }
        public int DeepestReached { get; set; }
        // the old single file's (0.1.8 and before): read, never written
        public Dictionary<string, RoomRecord>? Rooms { get; set; }
        public Dictionary<int, int>? Tally { get; set; }
        public Dictionary<string, double>? Cleared { get; set; }
        public Dictionary<string, double>? Spawned { get; set; }
    }

    /// <summary>A level's file: its tally when it has one, and its rooms by area id.</summary>
    private sealed class LevelFile
    {
        /// <summary>Null when the level has none yet (it has its seed value); 0 is a tally like any other.</summary>
        public int? Tally { get; set; }
        public SortedDictionary<int, RoomFile>? Rooms { get; set; }
    }

    /// <summary>A room as written: its zero fields left out, and its Foes() records only up to the last that is not all zero.</summary>
    private sealed class RoomFile
    {
        public FoeSlot[]? Foes { get; set; }
        public int TreasureClass { get; set; }
        public int Lead { get; set; }
        public decimal Gold { get; set; }
        public int ChestTrap { get; set; }
        public int ChestLock { get; set; }

        public static RoomFile From(RoomRecord r)
        {
            int n = r.Foes.Length;
            while (n > 0 && Same(r.Foes[n - 1], ZeroFoe)) n--;
            return new RoomFile
            {
                Foes = n == 0 ? null : r.Foes[..n],
                TreasureClass = r.TreasureClass, Lead = r.Lead, Gold = r.Gold, ChestTrap = r.ChestTrap, ChestLock = r.ChestLock,
            };
        }

        /// <summary>Back to the four records: what is not there reads as zero, as the level's zeroed records do.</summary>
        public RoomRecord ToRecord()
        {
            var r = new RoomRecord { TreasureClass = TreasureClass, Lead = Lead, Gold = Gold, ChestTrap = ChestTrap, ChestLock = ChestLock };
            if (Foes != null)
            {
                if (Foes.Length > r.Foes.Length) r.Foes = Foes.Select(_ => new FoeSlot()).ToArray();
                for (int i = 0; i < Foes.Length; i++) if (Foes[i] != null) r.Foes[i] = Foes[i].Copy();
            }
            return r;
        }
    }

    /// <summary>Whether two Foes() records hold the same, field for field.</summary>
    public static bool Same(FoeSlot a, FoeSlot b) =>
        a.Ref == b.Ref && a.KnownAs == b.KnownAs && a.GroupCount == b.GroupCount && a.Att == b.Att && a.Def == b.Def
        && a.Hits == b.Hits && a.MaxHits == b.MaxHits && a.Alignment == b.Alignment && a.IdLevel == b.IdLevel
        && a.Engaged == b.Engaged && a.JoinOffer == b.JoinOffer && a.Stamp.Equals(b.Stamp);

    /// <summary>Whether two rooms' records hold the same, field for field; a Foes() record not there is a zeroed one.</summary>
    public static bool Same(RoomRecord a, RoomRecord b)
    {
        if (a.TreasureClass != b.TreasureClass || a.Lead != b.Lead || a.Gold != b.Gold || a.ChestTrap != b.ChestTrap || a.ChestLock != b.ChestLock)
            return false;
        for (int i = 0; i < Math.Max(a.Foes.Length, b.Foes.Length); i++)
            if (!Same(i < a.Foes.Length ? a.Foes[i] : ZeroFoe, i < b.Foes.Length ? b.Foes[i] : ZeroFoe)) return false;
        return true;
    }

    /// <summary>
    /// The dungeon as the files hold it: dungeon.json and every level's file. A missing file, room or
    /// field reads as zero, as the level's zeroed records do; a file that will not read is said in
    /// crash.log and read as empty, as the single file was.
    ///
    /// An old single dungeon.json holding rooms (0.1.8 and before) is split here, once: its levels are
    /// written to their own files, it is kept as dungeon.json.old, and only then is dungeon.json
    /// written anew (<see cref="Save"/> does it in that order). A start that finds both - a split cut
    /// short - takes the old file's rooms only for the levels that have no file of their own yet,
    /// keeps the level files that are there (written from it), and finishes the split.
    /// </summary>
    public static DungeonState Load(string savesDir)
    {
        var state = new DungeonState { _whole = false };
        TopFile? top = null;
        string p = PathFor(savesDir);
        try
        {
            if (File.Exists(p)) top = JsonSerializer.Deserialize<TopFile>(File.ReadAllText(p));
        }
        catch (Exception e) { SafeFile.Log?.Invoke($"{p} could not be read: {e.GetType().Name}: {e.Message}"); }
        if (top != null)
        {
            state.DeepestReached = state._writtenDeepest = top.DeepestReached;
            state._topDirty = top.Format < Layout;
        }

        var onDisk = new HashSet<int>();
        try
        {
            if (Directory.Exists(savesDir))
                foreach (string f in Directory.GetFiles(savesDir, "dungeon-*.json"))
                {
                    if (LevelOfFile(Path.GetFileName(f)) is not int level) continue;
                    try
                    {
                        var file = JsonSerializer.Deserialize<LevelFile>(File.ReadAllText(f));
                        if (file == null) continue;
                        if (file.Tally is int t) state.Tally[level] = state._writtenTally[level] = t;
                        if (file.Rooms != null)
                            foreach (var (area, room) in file.Rooms)
                                if (room != null) state._rooms[Key(level, area)] = room.ToRecord();
                        onDisk.Add(level);
                    }
                    catch (Exception e) { SafeFile.Log?.Invoke($"{f} could not be read: {e.GetType().Name}: {e.Message}"); }
                }
        }
        catch { }

        if (top != null && top.Format < Layout
            && (top.Rooms is { Count: > 0 } || top.Tally is { Count: > 0 } || top.Cleared is { Count: > 0 }))
        {
            // the old single file, read as it always was
            var old = new DungeonState { Rooms = top.Rooms ?? new(), Tally = top.Tally ?? new(), Cleared = top.Cleared, Spawned = top.Spawned };
            old.TakeLegacyStamps();
            foreach (var (key, room) in old.Rooms)
            {
                if (!Split(key, out int level, out _) || onDisk.Contains(level)) continue;
                state._rooms[key] = room;
                state._dirty.Add(level);
            }
            foreach (var (level, t) in old.Tally)
            {
                if (onDisk.Contains(level)) continue;
                state.Tally[level] = t;
                state._dirty.Add(level);
            }
            state._legacyPending = true;
            using (SaveJournal.Because("dungeon.json split into a file per level"))
                state.Save(savesDir);
        }
        return state;
    }

    /// <summary>
    /// Saves from before the rooms were stored. An emptied room becomes a record that says so, with
    /// its stamp; a stamp that cannot be a time of day (older saves held UTC ticks) is dropped and
    /// the room simply repopulates once. The standing groups those saves never held, so their
    /// rooms are generated afresh.
    /// </summary>
    private void TakeLegacyStamps()
    {
        if (Cleared != null)
            foreach (var (key, at) in Cleared)
            {
                if (at < 0 || at > 86400 || Rooms.ContainsKey(key)) continue;
                var rec = new RoomRecord();
                rec.Foes[0].Ref = -1;          // emptied, by sub0C53
                rec.Foes[0].Stamp = at;
                Rooms[key] = rec;
            }
        Cleared = null;
        Spawned = null;
    }

    /// <summary>
    /// Every fn0F88 is a Put to the level's file, so this is called at each of them. What changed since
    /// the last save is written, and nothing else - as the original Puts only the record it changed:
    /// each level whose rooms or tally changed, to its own file, and dungeon.json when the deepest
    /// level did. A save with nothing new writes nothing. Each file is written beside itself and moved
    /// over (SafeFile), so a game killed half way through keeps the last good file; a level that could
    /// not be written stays marked, and is written at the next save.
    /// </summary>
    public void Save(string savesDir)
    {
        try
        {
            var levels = new SortedSet<int>(_dirty);
            foreach (var (level, t) in Tally)
                if (!_writtenTally.TryGetValue(level, out int w) || w != t) levels.Add(level);
            foreach (int level in _writtenTally.Keys)
                if (!Tally.ContainsKey(level)) levels.Add(level);
            if (_whole)
            {
                foreach (string key in _rooms.Keys) if (Split(key, out int level, out _)) levels.Add(level);
                foreach (int level in Tally.Keys) levels.Add(level);
                if (Directory.Exists(savesDir))
                    foreach (string f in Directory.GetFiles(savesDir, "dungeon-*.json"))
                        if (LevelOfFile(Path.GetFileName(f)) is int level) levels.Add(level);
            }
            bool topDue = _topDirty || _whole || _legacyPending || DeepestReached != _writtenDeepest;
            if (levels.Count == 0 && !topDue) return;
            Directory.CreateDirectory(savesDir);

            if (levels.Count > 0)
            {
                var rooms = levels.ToDictionary(l => l, _ => new SortedDictionary<int, RoomFile>());
                foreach (var (key, room) in _rooms)
                    if (Split(key, out int level, out int area) && rooms.TryGetValue(level, out var into)) into[area] = RoomFile.From(room);
                foreach (int level in levels)
                {
                    var file = new LevelFile
                    {
                        Tally = Tally.TryGetValue(level, out int t) ? t : null,
                        Rooms = rooms[level].Count > 0 ? rooms[level] : null,
                    };
                    Write(LevelPath(savesDir, level), JsonSerializer.SerializeToUtf8Bytes(file, Compact));
                    _dirty.Remove(level);
                    if (file.Tally is int written) _writtenTally[level] = written; else _writtenTally.Remove(level);
                }
            }

            // the old single file is kept only once every level is in a file of its own, and replaced last
            if (_legacyPending) KeepLegacy(savesDir);
            if (topDue)
            {
                Write(PathFor(savesDir), JsonSerializer.SerializeToUtf8Bytes(new TopFile { Format = Layout, DeepestReached = DeepestReached }, Compact));
                _writtenDeepest = DeepestReached;
                _topDirty = false;
            }
            _legacyPending = false;
            _whole = false;
        }
        catch { }
    }

    private static void Write(string path, byte[] bytes)
    {
        BeforeWrite?.Invoke(path);
        SafeFile.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// The old single dungeon.json kept, whole, as dungeon.json.old before a new one takes its name -
    /// never deleted. A dungeon.json.old already there with other contents (from an earlier split) is
    /// left as it is, and this one becomes dungeon.json.old2, and so on.
    /// </summary>
    private static void KeepLegacy(string savesDir)
    {
        string top = PathFor(savesDir);
        if (!File.Exists(top)) return;
        byte[] old = File.ReadAllBytes(top);
        string keep = top + ".old";
        for (int n = 2; File.Exists(keep); n++)
        {
            if (File.ReadAllBytes(keep).AsSpan().SequenceEqual(old)) return;   // kept already, by a split cut short
            keep = top + ".old" + n;
        }
        Write(keep, old);
    }
}
