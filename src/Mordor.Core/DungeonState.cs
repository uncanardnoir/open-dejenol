using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// Which rooms of the dungeon still hold monsters. The original does not roll dice as you walk: every
/// spawn area either holds a group or it does not, you fight it when you walk in, and once you have
/// killed it the room stays empty until it repopulates. From the help's tutorial: "when you kill the
/// monsters that are in a room and go back, it's empty. That's because they're dead! However, after a
/// period of time, the dungeon will repopulate with new monsters."
///
/// Shared by every character, like the automap, and stored beside the saves as dungeon.json.
/// </summary>
public sealed class DungeonState
{
    /// <summary>
    /// Every room's records, keyed "level:areaId": what the original keeps in the level's own file,
    /// four Foes() records and a Fight record per area (fn0F88, MONSMOD.BAS:3921-3924). A room
    /// missing here reads as the file's zeroed records - nothing standing, a stamp of 0.
    /// </summary>
    public Dictionary<string, RoomRecord> Rooms { get; set; } = new();

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
        Rooms[Key(level, areaId)] = r.Copy();
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

    private static string PathFor(string savesDir) => Path.Combine(savesDir, "dungeon.json");

    public static DungeonState Load(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p))
            {
                var state = JsonSerializer.Deserialize<DungeonState>(File.ReadAllText(p)) ?? new DungeonState();
                state.TakeLegacyStamps();
                return state;
            }
        }
        catch { }
        return new DungeonState();
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
    /// Every fn0F88 is a Put to the level's file, so this is called at each of them. Written beside
    /// the file and moved over it, so a game killed half way through keeps the last good file.
    /// </summary>
    public void Save(string savesDir)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            string p = PathFor(savesDir), temp = p + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, p, overwrite: true);
        }
        catch { }
    }
}
