using System.Globalization;
using System.Text.Json;

namespace Mordor.Mod.ICloudSaves;

/// <summary>One file of the saves folder as read for a snapshot: the bytes read once, and the hash of those same bytes.</summary>
public sealed record LocalFile(string Name, byte[] Data, string Sha256, DateTime WrittenUtc);

/// <summary>The world in the saves folder now: every synced file, and the world's hash (<see cref="Snapshot.WorldHash"/>).</summary>
public sealed record LocalWorld(IReadOnlyList<LocalFile> Files, string WorldHash)
{
    public bool HasWorld => Files.Count > 0;
    /// <summary>The newest write among the files, for "this Mac's saves last changed ..." (local time is the caller's).</summary>
    public DateTime? LastWrittenUtc => Files.Count == 0 ? null : Files.Max(f => f.WrittenUtc);
    public IEnumerable<SnapshotFile> Listed => Files.Select(f => new SnapshotFile(f.Name, f.Data.LongLength, f.Sha256));
}

/// <summary>Whether a snapshot in iCloud Drive can be read here.</summary>
public enum EntryState
{
    /// <summary>On this Mac, and its manifest reads.</summary>
    Ready,
    /// <summary>Only a placeholder is here (a <c>.name.icloud</c> stub, or a dataless file): its contents are still in iCloud.</summary>
    NotDownloaded,
    /// <summary>Here but not a readable snapshot (damaged, cut short, or a format this version does not know).</summary>
    Unreadable,
}

/// <summary>
/// A snapshot in iCloud Drive. <see cref="FileName"/> is its real name even for a placeholder, so a
/// snapshot is the same entry whether or not it is downloaded at the moment. For one not downloaded
/// the time and device come from the name alone; <see cref="WorldHash"/> is known only when Ready.
/// </summary>
public sealed record CloudEntry(string FileName, string Path, EntryState State, DateTime CreatedUtc, string Device,
                                string? WorldHash, string? Problem, string? ParentWorldHash = null);

/// <summary>Where the saves stand against iCloud (<see cref="CloudSync.Status"/>).</summary>
public enum SyncKind
{
    /// <summary>No world here and none in iCloud: nothing to do.</summary>
    Empty,
    /// <summary>Nothing has changed on either side since the last sync (or both hold the same world).</summary>
    InSync,
    /// <summary>This Mac's saves changed and iCloud's did not: an upload is due.</summary>
    LocalNewer,
    /// <summary>iCloud has a world from another Mac (or an earlier install) and this Mac's did not change: a restore can be offered.</summary>
    CloudNewer,
    /// <summary>As <see cref="CloudNewer"/>, but that snapshot is not downloaded to this Mac yet: nothing can be restored now.</summary>
    CloudNotDownloaded,
    /// <summary>Both changed since the last sync. Nothing is chosen for the player: both are kept, and they are asked.</summary>
    Conflict,
}

/// <summary>A reading of where things stand: the kind, the world here, what is in iCloud, and the snapshot that differs (<see cref="Candidate"/>), if any.</summary>
public sealed record SyncStatus(SyncKind Kind, LocalWorld Local, IReadOnlyList<CloudEntry> Cloud, CloudEntry? Candidate, SyncState? State)
{
    /// <summary>
    /// A snapshot this Mac has not accounted for and cannot read (damaged, or a newer format from a
    /// newer version of the mod on another Mac): the save-point upload holds back, since it may be
    /// another Mac's newer world; only the player's own Upload now goes past it.
    /// </summary>
    public bool UnreadableNews => Cloud.Any(e => e.State == EntryState.Unreadable && !(State?.Seen.Contains(e.FileName, StringComparer.OrdinalIgnoreCase) ?? false));

    /// <summary>The newest snapshot in iCloud that can be read here.</summary>
    public CloudEntry? NewestReady => Cloud.Where(e => e.State == EntryState.Ready).OrderByDescending(e => e.CreatedUtc).FirstOrDefault();
}

/// <summary>What an upload or a restore did: whether it happened, a line for the player, and (for a restore) the backup made first.</summary>
public sealed record SyncResult(bool Done, string Message, string? Snapshot = null, string? BackupDir = null);

/// <summary>
/// This Mac's record of the last sync, kept in the saves folder as <c>icloudsaves.json</c> and never
/// synced or restored (<see cref="CloudSync.IsSynced"/>): it is about this Mac.
/// </summary>
public sealed class SyncState
{
    /// <summary>The snapshot last uploaded from here or restored here.</summary>
    public string? Snapshot { get; set; }
    /// <summary>The world's hash here just after that sync: anything else now means this Mac's saves changed.</summary>
    public string? WorldHash { get; set; }
    public DateTime? SyncedUtc { get; set; }
    /// <summary>The Mac that made <see cref="Snapshot"/>.</summary>
    public string? SyncedDevice { get; set; }
    /// <summary>"Uploaded", "Restored" or "Matched" (iCloud already held this world).</summary>
    public string? LastAction { get; set; }
    /// <summary>
    /// Every snapshot this Mac has accounted for - its own uploads, and every other one that was there
    /// at a sync. A snapshot not in it is news from another Mac. Names are not dropped when a snapshot
    /// vanishes for a while (iCloud evicting it to a placeholder keeps its name), only when this Mac
    /// deletes one of its own old ones.
    /// </summary>
    public List<string> Seen { get; set; } = new();
    /// <summary>
    /// The snapshots this Mac uploaded itself, by file name. Pruning deletes only these: a manifest's
    /// device name proves nothing (two Macs may share a name, and anyone who can write to the iCloud
    /// Drive can write any name), so it is never trusted to say what is this Mac's to delete.
    /// </summary>
    public List<string> Own { get; set; } = new();
    /// <summary>
    /// Set to the backup folder just before a restore starts writing, cleared when it has finished or
    /// been put back. Still set at a start means the program stopped part-way through a restore: the
    /// saves may be half one world and half the other, so nothing is uploaded until the player says.
    /// </summary>
    public string? RestoreInProgress { get; set; }
    /// <summary>A restore the player asked for during a game, done (after asking again) at the next start's main menu.</summary>
    public string? PendingRestore { get; set; }
}

/// <summary>
/// The sync itself, with no UI and no knowledge of the game's windows: read the world here, list the
/// snapshots in iCloud Drive, say where things stand, upload a snapshot, restore one. Every path is
/// given, so the checks run it on temporary folders.
///
/// The design is Phase M3's (docs/MOBILE_AND_CLOUD.md, "The hard part: two devices, one world"): the
/// saves folder is one world, so the unit of sync is the whole world, never a file - nothing is
/// merged. Every upload is a new, separately named snapshot (versioned; a Mac keeps its last
/// <see cref="KeepPerDevice"/> and never deletes another Mac's), written under a name iCloud does
/// not sync and renamed into place when whole, so a half-written one is never seen as complete.
/// This Mac's settings.ini and mods.log are its own and never go up or come down.
///
/// Safety rules, which every path here keeps:
/// <list type="bullet">
/// <item>Nothing here is ever deleted or overwritten without a timestamped copy of the whole saves
///   folder first (<c>saves-backup-yyyyMMdd-HHmmss</c> beside it, the name .gitignore already
///   expects), each file of the copy checked against the original.</item>
/// <item>A snapshot is read whole and proven against its manifest's hashes (<see cref="Snapshot.Verify"/>)
///   before the backup is made, and the bytes proven are the bytes written.</item>
/// <item>A placeholder, a dataless file or an unreadable snapshot is never restored from.</item>
/// <item>When both sides changed since the last sync, no side wins on its own (<see cref="SyncKind.Conflict"/>);
///   the automatic upload holds back and the player chooses.</item>
/// <item>A restore that fails part-way puts the backup back.</item>
/// </list>
/// </summary>
public sealed class CloudSync
{
    /// <summary>This Mac's record of the last sync (<see cref="SyncState"/>), in the saves folder.</summary>
    public const string StateFileName = "icloudsaves.json";
    /// <summary>The folder of snapshots inside the mod's iCloud Drive folder.</summary>
    public const string SnapshotsFolder = "Snapshots";
    /// <summary>How many of its own snapshots a Mac keeps; older ones of its own are deleted after an upload.</summary>
    public const int KeepPerDevice = 10;
    /// <summary>The suffix iCloud Drive does not sync: a snapshot is written under it, then renamed.</summary>
    public const string NoSync = ".nosync";

    private readonly Func<DateTime> _utcNow;

    /// <param name="savesDir">The game's saves folder (the world).</param>
    /// <param name="cloudDir">The mod's folder in iCloud Drive (<see cref="ICloudDrive.Locate"/>), or a test's.</param>
    /// <param name="device">This Mac's name.</param>
    /// <param name="utcNow">The clock (the checks give their own).</param>
    /// <param name="isDataless">Whether a file's contents are still in iCloud (<see cref="ICloudDrive.IsDataless"/>; the checks give their own).</param>
    public CloudSync(string savesDir, string cloudDir, string device, Func<DateTime>? utcNow = null, Func<string, bool>? isDataless = null)
    {
        SavesDir = savesDir; CloudDir = cloudDir; Device = device;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _isDataless = isDataless ?? ICloudDrive.IsDataless;
    }

    private readonly Func<string, bool> _isDataless;

    public string SavesDir { get; }
    public string CloudDir { get; }
    public string Device { get; }
    public string SnapshotsDir => Path.Combine(CloudDir, SnapshotsFolder);
    public string StatePath => Path.Combine(SavesDir, StateFileName);

    // ------------------------------------------------------------------ what is synced

    /// <summary>
    /// Whether a file at the top of the saves folder is part of the world. Everything is, but:
    /// settings.ini (this Mac's settings, docs/MOBILE_AND_CLOUD.md - including which mods are on and
    /// this mod's own toggles), mods.log (this run's log, never synced per M3), this mod's own record
    /// <see cref="StateFileName"/>, crash.log, hidden files (.DS_Store), and the half-written files
    /// the game and the mods write before a rename (<c>*.tmp</c>) or this mod's own (<c>*.nosync</c>).
    /// Folders inside the saves folder are not looked at.
    /// </summary>
    public static bool IsSynced(string name) =>
        !string.IsNullOrEmpty(name)
        && !name.StartsWith('.')
        && !Is(name, Mordor.Core.PortSettings.FileName)
        && !Is(name, "mods.log")
        && !Is(name, StateFileName)
        && !Is(name, Mordor.Core.PortFiles.CrashLogName)
        && !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
        && !name.EndsWith(NoSync, StringComparison.OrdinalIgnoreCase);

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The world here now: each synced file read once, hashed from those bytes.</summary>
    public LocalWorld ReadLocal()
    {
        var files = new List<LocalFile>();
        if (Directory.Exists(SavesDir))
            foreach (string p in Directory.GetFiles(SavesDir).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(p);
                if (!IsSynced(name)) continue;
                byte[] data = File.ReadAllBytes(p);
                files.Add(new LocalFile(name, data, Snapshot.Sha256(data), File.GetLastWriteTimeUtc(p)));
            }
        return new LocalWorld(files, Snapshot.WorldHash(files.Select(f => new SnapshotFile(f.Name, f.Data.LongLength, f.Sha256))));
    }

    // ------------------------------------------------------------------ what is in iCloud

    /// <summary>
    /// Every snapshot in iCloud Drive, newest first. A placeholder or a dataless file is listed as
    /// <see cref="EntryState.NotDownloaded"/> and not opened; a file whose manifest does not read is
    /// <see cref="EntryState.Unreadable"/>; a snapshot still being written (<c>*.nosync</c>) is not listed.
    /// </summary>
    public List<CloudEntry> ListCloud()
    {
        var list = new List<CloudEntry>();
        if (!Directory.Exists(SnapshotsDir)) return list;
        foreach (string p in Directory.GetFiles(SnapshotsDir))
        {
            string name = Path.GetFileName(p);
            if (ICloudDrive.PlaceholderFor(name) is { } real)
            {
                if (real.EndsWith(Snapshot.Extension, StringComparison.OrdinalIgnoreCase)) list.Add(FromName(real, p, EntryState.NotDownloaded, "not downloaded to this Mac yet"));
                continue;
            }
            if (name.StartsWith('.') || !name.EndsWith(Snapshot.Extension, StringComparison.OrdinalIgnoreCase)) continue;
            if (_isDataless(p)) { list.Add(FromName(name, p, EntryState.NotDownloaded, "not downloaded to this Mac yet")); continue; }
            try
            {
                var m = Snapshot.ReadManifest(p);
                var made = DateTime.SpecifyKind(m.CreatedUtc, DateTimeKind.Utc);
                // a snapshot dated well ahead of this Mac's clock would stay "the newest" for ever
                if (made > _utcNow().AddDays(1)) { list.Add(FromName(name, p, EntryState.Unreadable, "it is dated in the future")); continue; }
                list.Add(new CloudEntry(name, p, EntryState.Ready, made, m.Device, m.WorldHash.ToLowerInvariant(), null, m.ParentWorldHash?.ToLowerInvariant()));
            }
            // any failure to read one snapshot makes that one unreadable, never the listing fail
            catch (Exception ex) { list.Add(FromName(name, p, EntryState.Unreadable, ex.Message)); }
        }
        return list.OrderByDescending(e => e.CreatedUtc).ThenByDescending(e => e.FileName, StringComparer.Ordinal).ToList();
    }

    private static CloudEntry FromName(string name, string path, EntryState state, string problem)
    {
        var parsed = Snapshot.ParseName(name);
        return new CloudEntry(name, path, state, parsed?.Utc ?? DateTime.MinValue, parsed?.DeviceTag ?? "another Mac", null, problem);
    }

    // ------------------------------------------------------------------ this Mac's record

    /// <summary>This Mac's record, or null when there is none or it does not read (a garbled one counts as none, which only makes the next sync ask).</summary>
    public SyncState? LoadState()
    {
        try
        {
            return File.Exists(StatePath) ? JsonSerializer.Deserialize<SyncState>(File.ReadAllText(StatePath), Snapshot.Json) : null;
        }
        catch { return null; }
    }

    /// <summary>Written whole under a temporary name and renamed over the old one, so a crash leaves the old record or the new, never half of one.</summary>
    public void SaveState(SyncState s)
    {
        Directory.CreateDirectory(SavesDir);
        // the oldest names first out when the list grows long: a few hundred is years of play
        if (s.Seen.Count > 500) s.Seen = s.Seen.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.Ordinal).TakeLast(500).ToList();
        string temp = StatePath + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(s, Snapshot.Json));
        File.Move(temp, StatePath, overwrite: true);
    }

    // ------------------------------------------------------------------ where things stand

    /// <summary>
    /// Where the saves stand against iCloud, changing nothing.
    ///
    /// "Changed here": the world's hash is not the one recorded at the last sync (with no record,
    /// any world here counts as changed). "Changed in iCloud": there is a snapshot this Mac has not
    /// accounted for (<see cref="SyncState.Seen"/>); the newest such one, by its manifest's time, is
    /// the <see cref="SyncStatus.Candidate"/>. The newest unaccounted one decides even if it is not
    /// downloaded yet, so an older snapshot is never offered while a newer one is still on its way.
    /// A candidate holding exactly this Mac's world is no change at all.
    ///
    /// With no world here (a fresh saves folder) there is nothing to lose, and the newest snapshot
    /// in iCloud is offered whether or not it was seen before.
    /// </summary>
    public SyncStatus Status()
    {
        var local = ReadLocal();
        var cloud = ListCloud();
        var state = LoadState();
        var seen = new HashSet<string>(state?.Seen ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var usable = cloud.Where(e => e.State != EntryState.Unreadable).ToList();

        if (!local.HasWorld)
        {
            var newest = usable.FirstOrDefault();
            if (newest == null) return new SyncStatus(SyncKind.Empty, local, cloud, null, state);
            return new SyncStatus(newest.State == EntryState.Ready ? SyncKind.CloudNewer : SyncKind.CloudNotDownloaded, local, cloud, newest, state);
        }

        var candidate = usable.FirstOrDefault(e => !seen.Contains(e.FileName));
        bool localChanged = state?.WorldHash is not { } was || was != local.WorldHash;
        if (candidate is { State: EntryState.Ready } c && c.WorldHash == local.WorldHash)
            return new SyncStatus(SyncKind.InSync, local, cloud, candidate, state);
        if (candidate == null)
            return new SyncStatus(localChanged ? SyncKind.LocalNewer : SyncKind.InSync, local, cloud, null, state);
        // a snapshot that did not grow from the world this Mac last synced is a fork - another Mac
        // that never saw this one's upload - and so a conflict even with nothing changed here
        bool fork = candidate.State == EntryState.Ready && state != null && candidate.ParentWorldHash != state.WorldHash;
        if (!localChanged && !fork)
            return new SyncStatus(candidate.State == EntryState.Ready ? SyncKind.CloudNewer : SyncKind.CloudNotDownloaded, local, cloud, candidate, state);
        return new SyncStatus(SyncKind.Conflict, local, cloud, candidate, state);
    }

    /// <summary>
    /// iCloud already holds this Mac's world (<see cref="SyncKind.InSync"/> with a candidate): record
    /// that snapshot as the last sync, so it is not news next time. Nothing is copied.
    /// </summary>
    public void Adopt(SyncStatus st)
    {
        if (st.Kind != SyncKind.InSync || st.Candidate is not { State: EntryState.Ready } c) return;
        var s = st.State ?? new SyncState();
        s.Snapshot = c.FileName; s.WorldHash = st.Local.WorldHash; s.SyncedUtc = _utcNow(); s.SyncedDevice = c.Device; s.LastAction = "Matched";
        s.Seen = s.Seen.Union(Readable(st.Cloud), StringComparer.OrdinalIgnoreCase).ToList();
        SaveState(s);
    }

    // ------------------------------------------------------------------ upload

    /// <summary>
    /// The upload at a save point: done only when this Mac's world changed and iCloud's did not
    /// (<see cref="SyncKind.LocalNewer"/>). In a conflict, or with a newer world in iCloud, or a
    /// restore waiting, it holds back and says why - the player chooses. In sync with a snapshot it
    /// had not recorded, that one is recorded.
    /// </summary>
    public SyncResult AutoUpload()
    {
        var st = Status();
        if (st.State?.RestoreInProgress != null)
            return new SyncResult(false, "Not uploaded: a restore was interrupted, and the saves may be part one world, part another. Start OpenDejenol again to sort it out.");
        if (st.State?.PendingRestore is { } pending)
            return new SyncResult(false, $"Not uploaded: a restore of {pending} is waiting for the next start.");
        switch (st.Kind)
        {
            case SyncKind.LocalNewer when st.UnreadableNews:
                return new SyncResult(false, "Not uploaded: iCloud holds a snapshot this Mac cannot read (damaged, or from a newer version of the mod), "
                                           + "which may be another Mac's newer world. Upload now in the iCloud Saves window uploads anyway.");
            case SyncKind.LocalNewer: return Upload(st, acknowledgeAll: false);
            case SyncKind.InSync: Adopt(st); return new SyncResult(false, "Already in iCloud.");
            case SyncKind.Empty: return new SyncResult(false, "No saved world on this Mac yet.");
            case SyncKind.Conflict: return new SyncResult(false, "Not uploaded: this Mac's saves and iCloud's have both changed. Choose which to keep in the iCloud Saves window.");
            default: return new SyncResult(false, "Not uploaded: iCloud has a newer world from " + Describe(st.Candidate) + ".");
        }
    }

    /// <summary>
    /// The player's own Upload (or "Keep this Mac's" in a conflict): this Mac's world goes up as a new
    /// snapshot whatever iCloud holds, and every snapshot there now is counted as accounted for. Nothing
    /// in iCloud is deleted but this Mac's own oldest beyond <see cref="KeepPerDevice"/>, so the other
    /// Mac's copy stays there as an earlier version: both are kept.
    /// </summary>
    /// <remarks>
    /// Refused while iCloud simply has a newer world and this Mac's has not changed
    /// (<see cref="SyncKind.CloudNewer"/>, <see cref="SyncKind.CloudNotDownloaded"/>): an upload then
    /// would put an older world on top and mark the newer one seen, so it would never be offered again.
    /// </remarks>
    public SyncResult UploadNow()
    {
        var st = Status();
        if (st.State?.RestoreInProgress != null)
            return new SyncResult(false, "Not uploaded: a restore was interrupted. Start OpenDejenol again to sort it out first.");
        if (st.Kind is SyncKind.CloudNewer or SyncKind.CloudNotDownloaded)
            return new SyncResult(false, $"Not uploaded: iCloud has a newer world, from {Describe(st.Candidate)}, and this Mac's has not changed since. "
                                       + "Restore it at the next start instead.");
        if (st.Kind == SyncKind.InSync && !st.UnreadableNews) return new SyncResult(false, "Already in iCloud.");
        return Upload(st, acknowledgeAll: true);
    }

    /// <summary>The names to count as accounted for: every snapshot that can be read or is on its way, never an unreadable one.</summary>
    private static IEnumerable<string> Readable(IEnumerable<CloudEntry> cloud) =>
        cloud.Where(e => e.State != EntryState.Unreadable).Select(e => e.FileName);

    /// <param name="acknowledgeAll">The player's own upload: every snapshot there now counts as seen, an unreadable one too.</param>
    private SyncResult Upload(SyncStatus st, bool acknowledgeAll)
    {
        var local = st.Local;
        if (!local.HasWorld) return new SyncResult(false, "Nothing to upload: there is no saved world on this Mac yet.");
        Directory.CreateDirectory(SnapshotsDir);
        DateTime now = _utcNow();
        var manifest = new SnapshotManifest
        {
            CreatedUtc = now, Device = Device, ModVersion = typeof(CloudSync).Assembly.GetName().Version?.ToString() ?? "",
            Files = local.Listed.ToList(), WorldHash = local.WorldHash, ParentWorldHash = st.State?.WorldHash,
        };
        string name = Snapshot.FileName(now, Device);
        for (int n = 2; File.Exists(Path.Combine(SnapshotsDir, name)) || File.Exists(Path.Combine(SnapshotsDir, "." + name + ".icloud")); n++)
            name = Snapshot.FileName(now, Device, n);
        string final = Path.Combine(SnapshotsDir, name);
        // written under a hidden .nosync name, which iCloud Drive leaves alone, then renamed into place
        // in the same folder - an atomic rename - so no Mac ever sees a half-written snapshot
        string temp = Path.Combine(SnapshotsDir, "." + name + ".partial" + NoSync);
        try
        {
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            {
                Snapshot.Write(fs, manifest, local.Files.ToDictionary(f => f.Name, f => f.Data, StringComparer.OrdinalIgnoreCase));
                fs.Flush(flushToDisk: true);
            }
            // read back and proven before it is given its real name
            var back = Snapshot.Verify(temp);
            if (back.Manifest.WorldHash != local.WorldHash) throw new SnapshotInvalidException("the snapshot read back is not the world written");
            File.Move(temp, final, overwrite: false);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }

        var s = st.State ?? new SyncState();
        s.Snapshot = name; s.WorldHash = local.WorldHash; s.SyncedUtc = now; s.SyncedDevice = Device; s.LastAction = "Uploaded";
        s.Seen = s.Seen.Union((acknowledgeAll ? st.Cloud.Select(e => e.FileName) : Readable(st.Cloud)).Append(name), StringComparer.OrdinalIgnoreCase).ToList();
        s.Own = s.Own.Append(name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var pruned = PruneOwn(name, s.Own);
        s.Own.RemoveAll(n => pruned.Contains(n, StringComparer.OrdinalIgnoreCase));
        s.Seen.RemoveAll(n => pruned.Contains(n, StringComparer.OrdinalIgnoreCase));
        SaveState(s);
        return new SyncResult(true, $"Uploaded to iCloud: {local.Files.Count} files.", name);
    }

    /// <summary>
    /// This Mac's own snapshots beyond the newest <see cref="KeepPerDevice"/> are deleted - only those
    /// this Mac recorded uploading (<see cref="SyncState.Own"/>; the manifest's device name is not
    /// trusted), only readable ones, never the one just written and never another Mac's. Returns the
    /// names deleted. A failure to delete is no failure of the upload.
    /// </summary>
    private List<string> PruneOwn(string justWritten, List<string> ownNames)
    {
        var gone = new List<string>();
        try
        {
            var mine = new HashSet<string>(ownNames, StringComparer.OrdinalIgnoreCase);
            var own = ListCloud().Where(e => e.State == EntryState.Ready && mine.Contains(e.FileName)).ToList();
            foreach (var e in own.Skip(KeepPerDevice))
            {
                if (string.Equals(e.FileName, justWritten, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(e.Path); gone.Add(e.FileName); } catch { }
            }
        }
        catch { }
        return gone;
    }

    // ------------------------------------------------------------------ restore

    /// <summary>
    /// Replace the world here with snapshot <paramref name="fileName"/>, the safe way round:
    /// <list type="number">
    /// <item>refuse anything but a Ready snapshot (never a placeholder, a dataless file or an unreadable one);</item>
    /// <item>read it whole and prove it against its manifest (<see cref="Snapshot.Verify"/>) - nothing has changed yet;</item>
    /// <item>copy the whole saves folder to <c>saves-backup-yyyyMMdd-HHmmss</c> beside it, each copy checked - nothing has changed yet;</item>
    /// <item>write each file under a temporary name and rename it over the old one, then remove the
    ///   world files the snapshot does not have (a character deleted on the other Mac), leaving this
    ///   Mac's own files (settings.ini, mods.log, this record) alone;</item>
    /// <item>read the world back and check it hashes to the snapshot's; if it does not, or anything
    ///   above failed part-way, put the backup back.</item>
    /// </list>
    /// The game must not be holding the saves (the main menu at start, nothing loaded): the caller's to ensure.
    /// </summary>
    public SyncResult Restore(string fileName)
    {
        var entry = ListCloud().FirstOrDefault(e => string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return new SyncResult(false, $"{fileName} is not in iCloud Drive any more. Nothing was changed.");
        if (entry.State == EntryState.NotDownloaded)
            return new SyncResult(false, $"{fileName} is not downloaded to this Mac yet, so it cannot be checked or restored. Nothing was changed. "
                                       + "In Finder, open iCloud Drive > OpenDejenol > Snapshots and choose Download Now, then try again.");
        if (entry.State == EntryState.Unreadable)
            return new SyncResult(false, $"{fileName} cannot be restored: {entry.Problem}. Nothing was changed.");

        VerifiedSnapshot snap;
        try { snap = Snapshot.Verify(entry.Path); }
        catch (SnapshotInvalidException ex) { return new SyncResult(false, $"{fileName} cannot be restored: {ex.Message}. Nothing was changed."); }

        string backup;
        try { backup = Backup(); }
        catch (Exception ex) { return new SyncResult(false, $"The saves could not be backed up first ({ex.Message}), so nothing was restored or changed."); }

        // marked before the first write, so a program stopped part-way is caught at the next start
        var marked = LoadState() ?? new SyncState();
        marked.RestoreInProgress = backup;
        SaveState(marked);
        try
        {
            Apply(snap);
            var after = ReadLocal();
            if (after.WorldHash != snap.Manifest.WorldHash) throw new IOException("the saves read back are not the snapshot's");
        }
        catch (Exception ex)
        {
            bool back = RollBack(backup);
            if (back) { var undo = LoadState() ?? new SyncState(); undo.RestoreInProgress = null; SaveState(undo); }
            string rolled = back ? "The saves were put back as they were." : $"Putting the saves back failed too: copy them back by hand from {backup}.";
            return new SyncResult(false, $"Restoring {fileName} failed ({ex.Message}). {rolled}", fileName, backup);
        }

        var s = LoadState() ?? new SyncState();
        s.RestoreInProgress = null;
        s.Snapshot = entry.FileName; s.WorldHash = snap.Manifest.WorldHash; s.SyncedUtc = _utcNow(); s.SyncedDevice = snap.Manifest.Device; s.LastAction = "Restored";
        s.Seen = s.Seen.Union(Readable(ListCloud()), StringComparer.OrdinalIgnoreCase).ToList();
        s.PendingRestore = null;
        SaveState(s);
        return new SyncResult(true, $"Restored the world saved on {snap.Manifest.Device} at {Local(snap.Manifest.CreatedUtc)}. "
                                  + $"This Mac's saves as they were are in {backup}.", entry.FileName, backup);
    }

    /// <summary>The verified files written in, each under a temporary name and renamed over the old; then the world files the snapshot lacks removed (they are in the backup).</summary>
    private void Apply(VerifiedSnapshot snap)
    {
        Directory.CreateDirectory(SavesDir);
        foreach (var (name, data) in snap.Files)
        {
            // a fresh name each time, created new: never written through whatever might already be there
            string dest = Path.Combine(SavesDir, name), temp = dest + "." + Guid.NewGuid().ToString("N")[..8] + ".icloud-restore.tmp";
            BeforeRestoreWrite?.Invoke(name);
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write)) { fs.Write(data); fs.Flush(flushToDisk: true); }
            File.Move(temp, dest, overwrite: true);
        }
        foreach (string p in Directory.GetFiles(SavesDir))
        {
            string name = Path.GetFileName(p);
            if (IsSynced(name) && !snap.Files.ContainsKey(name)) File.Delete(p);
        }
    }

    /// <summary>
    /// A copy of every file at the top of the saves folder, synced or not, in a new
    /// <c>&lt;saves&gt;-backup-yyyyMMdd-HHmmss</c> folder beside it (<c>saves-backup-*</c>, the name
    /// .gitignore already keeps out of the repository). Each copy is read back and compared with its
    /// original's hash; any difference throws, and the restore does not go on. Returns the folder.
    /// Backups are never deleted by the mod: they are the player's to keep or throw away.
    /// </summary>
    public string Backup()
    {
        string full = Path.GetFullPath(SavesDir).TrimEnd(Path.DirectorySeparatorChar);
        string parent = Path.GetDirectoryName(full) ?? throw new IOException("the saves folder has no folder above it");
        string stem = Path.GetFileName(full) + "-backup-" + _utcNow().ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string dir = Path.Combine(parent, stem);
        for (int n = 2; Directory.Exists(dir) || File.Exists(dir); n++) dir = Path.Combine(parent, stem + "-" + n.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        if (Directory.Exists(SavesDir))
            foreach (string p in Directory.GetFiles(SavesDir))
            {
                string dest = Path.Combine(dir, Path.GetFileName(p));
                byte[] data = File.ReadAllBytes(p);
                File.WriteAllBytes(dest, data);
                if (Snapshot.Sha256(File.ReadAllBytes(dest)) != Snapshot.Sha256(data))
                    throw new IOException($"the backup's copy of {Path.GetFileName(p)} does not match the original");
            }
        return dir;
    }

    /// <summary>
    /// Called with each file's name just before a restore writes it. For the checks only, which throw
    /// from it to prove a restore that fails part-way puts the backup back; null in the game.
    /// </summary>
    public Action<string>? BeforeRestoreWrite { get; set; }

    /// <summary>
    /// After a restore was interrupted (<see cref="SyncState.RestoreInProgress"/>): put the backup's
    /// world back, or (<paramref name="putBack"/> false, the player's choice) keep the saves as they
    /// are. Either way the mark is cleared. True when that was done.
    /// </summary>
    public bool SettleInterrupted(bool putBack)
    {
        var s = LoadState();
        if (s?.RestoreInProgress is not { } backup) return true;
        if (putBack && (!Directory.Exists(backup) || !RollBack(backup))) return false;
        s.RestoreInProgress = null;
        SaveState(s);
        return true;
    }

    /// <summary>
    /// The backup's world files written back and any world file it does not have removed, then the
    /// world read back and compared with the backup's; true only when it matches.
    /// </summary>
    private bool RollBack(string backup)
    {
        bool ok = true;
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string p in Directory.GetFiles(backup))
        {
            string name = Path.GetFileName(p);
            kept.Add(name);
            if (!IsSynced(name)) continue;
            try { File.Copy(p, Path.Combine(SavesDir, name), overwrite: true); } catch { ok = false; }
        }
        foreach (string p in Directory.GetFiles(SavesDir))
        {
            string name = Path.GetFileName(p);
            if (!IsSynced(name) || kept.Contains(name)) continue;
            try { File.Delete(p); } catch { ok = false; }
        }
        if (!ok) return false;
        try
        {
            var expected = Directory.GetFiles(backup).Where(p => IsSynced(Path.GetFileName(p)))
                .Select(p => { var d = File.ReadAllBytes(p); return new SnapshotFile(Path.GetFileName(p), d.LongLength, Snapshot.Sha256(d)); });
            return Snapshot.WorldHash(expected) == ReadLocal().WorldHash;
        }
        catch { return false; }
    }

    /// <summary>Mark <paramref name="fileName"/> to be offered for restoring at the next start (asked during a game, when the saves are in use).</summary>
    public void RequestRestore(string? fileName)
    {
        var s = LoadState() ?? new SyncState();
        s.PendingRestore = fileName;
        SaveState(s);
    }

    // ------------------------------------------------------------------ words

    /// <summary>A time for the player: this Mac's local time, day month year and minutes.</summary>
    public static string Local(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"Daves-MacBook at 30 Oct 2026 14:30", or "unknown" for nothing.</summary>
    public static string Describe(CloudEntry? e) =>
        e == null ? "unknown" : e.CreatedUtc == DateTime.MinValue ? e.Device : $"{e.Device} at {Local(e.CreatedUtc)}";
}
