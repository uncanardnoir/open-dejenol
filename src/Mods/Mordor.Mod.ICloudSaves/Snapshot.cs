using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mordor.Mod.ICloudSaves;

/// <summary>One file of a world as a snapshot records it: its name in the saves folder, its size and its SHA-256 (hex, lower case).</summary>
public sealed record SnapshotFile(string Name, long Size, string Sha256);

/// <summary>
/// What a snapshot says about itself (<c>manifest.json</c>, the first entry of the archive): the
/// format, when and on which Mac it was made, every file with its size and hash, and the hash of the
/// whole world (<see cref="Snapshot.WorldHash"/>) - so the receiving Mac can prove that what it is
/// about to restore is complete and unchanged before it touches anything.
/// </summary>
public sealed class SnapshotManifest
{
    /// <summary>The layout of the archive. A Mac that does not know a newer format refuses it rather than guess.</summary>
    public int Format { get; set; } = Snapshot.CurrentFormat;
    public string App { get; set; } = "OpenDejenol";
    /// <summary>When the snapshot was made, by the uploading Mac's clock, in UTC.</summary>
    public DateTime CreatedUtc { get; set; }
    /// <summary>The uploading Mac's name (<see cref="Environment.MachineName"/>).</summary>
    public string Device { get; set; } = "";
    /// <summary>The mod's version that wrote it, for a later reader's information only.</summary>
    public string ModVersion { get; set; } = "";
    public string WorldHash { get; set; } = "";
    /// <summary>
    /// The world this one was made from: the uploading Mac's world hash at its last sync, or null for
    /// a Mac that had never synced. A receiving Mac takes a snapshot as plainly newer only when it grew
    /// from the world that Mac itself last synced; one that grew from another world is a fork, and a
    /// fork is a conflict, never an upgrade.
    /// </summary>
    public string? ParentWorldHash { get; set; }
    public List<SnapshotFile> Files { get; set; } = new();
}

/// <summary>A snapshot read back and proven whole: its manifest and every file's bytes.</summary>
public sealed record VerifiedSnapshot(SnapshotManifest Manifest, IReadOnlyDictionary<string, byte[]> Files);

/// <summary>A snapshot that is not whole or not what its manifest says; nothing is restored from it.</summary>
public sealed class SnapshotInvalidException : Exception
{
    public SnapshotInvalidException(string reason, Exception? inner = null) : base(reason, inner) { }
}

/// <summary>
/// The snapshot file: one zip archive per upload, <c>yyyyMMddTHHmmssZ_Device.odsave</c>, holding
/// <c>manifest.json</c> and the world's files under <c>world/</c>.
///
/// Why one archive and not a folder of files: iCloud Drive syncs file by file, so a folder written on
/// one Mac can turn up on another a few files at a time, and a reader could take a half-arrived world
/// for a whole one. A single file arrives whole or not at all (it shows as a placeholder until then),
/// and the manifest's sizes and hashes catch anything else. The whole world is about 0.9 MB, 100-150 KB
/// compressed (docs/MOBILE_AND_CLOUD.md, "What a save is"), so a full copy per upload is cheap.
/// </summary>
public static class Snapshot
{
    public const int CurrentFormat = 1;
    public const string Extension = ".odsave";
    public const string ManifestEntry = "manifest.json";
    public const string WorldPrefix = "world/";

    /// <summary>
    /// A ceiling on what is read out of one archive, against a damaged or hostile file that claims to
    /// unpack to far more than a world ever is (the real one is under a megabyte).
    /// </summary>
    public const long MaxWorldBytes = 64L * 1024 * 1024;

    /// <summary>
    /// A ceiling on the archive file itself and on its entries, checked before it is opened: opening a
    /// zip reads its whole central directory, and a planted file with millions of entries would stall
    /// the game at a save point. A real snapshot is a few hundred kilobytes and a few dozen entries.
    /// </summary>
    public const long MaxArchiveBytes = 16L * 1024 * 1024;
    public const int MaxEntries = 1000;

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>
    /// The hash of a whole world: SHA-256 over each file's name, size and hash, one line each, in
    /// ordinal name order (case ignored, as the Mac's file system ignores it). Two worlds with the
    /// same files byte for byte have the same hash, whichever Mac or order they were read in.
    /// </summary>
    public static string WorldHash(IEnumerable<SnapshotFile> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(f => f.Name.ToLowerInvariant(), StringComparer.Ordinal))
            sb.Append(f.Name).Append('\n').Append(f.Size.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(f.Sha256).Append('\n');
        return Sha256(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// A name that may be written into the saves folder on a restore: a plain file name - no folder
    /// part, nothing that climbs out (<c>..</c>), not hidden - and one that is synced at all
    /// (<see cref="CloudSync.IsSynced"/>), so a snapshot can never carry another Mac's settings.ini
    /// or mods.log over this one's.
    /// </summary>
    public static bool IsSafeName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) < 0
        && name != "." && name != ".."
        && name == Path.GetFileName(name)
        && CloudSync.IsSynced(name);

    /// <summary>The archive's bytes for a world and its manifest. The manifest is built by the caller from the same bytes.</summary>
    public static void Write(Stream to, SnapshotManifest manifest, IReadOnlyDictionary<string, byte[]> files)
    {
        using var zip = new ZipArchive(to, ZipArchiveMode.Create, leaveOpen: true);
        var m = zip.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
        using (var s = m.Open()) JsonSerializer.Serialize(s, manifest, Json);
        foreach (var f in manifest.Files)
        {
            var e = zip.CreateEntry(WorldPrefix + f.Name, CompressionLevel.Optimal);
            using var s = e.Open();
            s.Write(files[f.Name]);
        }
    }

    /// <summary>Only the manifest, for listing: the archive is opened and its first entry read, nothing else.</summary>
    public static SnapshotManifest ReadManifest(string path)
    {
        try
        {
            using var zip = Open(path);
            return ManifestOf(zip);
        }
        catch (SnapshotInvalidException) { throw; }
        catch (Exception ex) { throw new SnapshotInvalidException("the file is not a readable snapshot (" + ex.Message + ")", ex); }
    }

    private static SnapshotManifest ManifestOf(ZipArchive zip)
    {
        var entry = zip.GetEntry(ManifestEntry) ?? throw new SnapshotInvalidException("it has no manifest");
        if (entry.Length > 4 * 1024 * 1024) throw new SnapshotInvalidException("its manifest is too large to be one");
        using var s = entry.Open();
        var m = JsonSerializer.Deserialize<SnapshotManifest>(s, Json) ?? throw new SnapshotInvalidException("its manifest is empty");
        if (m.Format != CurrentFormat)
            throw new SnapshotInvalidException($"it is in format {m.Format}, which this version of the mod does not know (it knows {CurrentFormat})");
        // the file comes from another Mac, or from anyone who can write to the iCloud Drive: a field
        // left out or nulled is a snapshot refused, never a crash in whatever reads it next
        if (string.IsNullOrWhiteSpace(m.Device) || !IsHash(m.WorldHash) || m.Files == null
            || m.Files.Any(f => f == null || f.Name == null || !IsHash(f.Sha256)))
            throw new SnapshotInvalidException("its manifest is incomplete");
        return m;
    }

    private static bool IsHash(string? h) => h is { Length: 64 } && h.All(Uri.IsHexDigit);

    /// <summary>The archive opened for reading, after the size and entry-count ceilings.</summary>
    private static ZipArchive Open(string path)
    {
        if (new FileInfo(path).Length > MaxArchiveBytes) throw new SnapshotInvalidException("the file is far larger than any snapshot");
        var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > MaxEntries) { zip.Dispose(); throw new SnapshotInvalidException("it holds far more entries than any snapshot"); }
        return zip;
    }

    /// <summary>
    /// The whole snapshot read and proven before anything is restored from it: the manifest in a
    /// known format; every name a plain, synced file name, none twice; exactly the entries the
    /// manifest lists, no more; each file's size and SHA-256 as listed; and the world hash
    /// recomputed from the list. Any failure throws <see cref="SnapshotInvalidException"/> with the
    /// reason, and nothing is returned to restore from.
    /// </summary>
    public static VerifiedSnapshot Verify(string path)
    {
        try
        {
            using var zip = Open(path);
            var m = ManifestOf(zip);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in m.Files)
            {
                if (!IsSafeName(f.Name)) throw new SnapshotInvalidException($"it names a file that may not be restored: \"{f.Name}\"");
                if (!names.Add(f.Name)) throw new SnapshotInvalidException($"it lists \"{f.Name}\" twice");
                if (f.Size < 0) throw new SnapshotInvalidException($"it gives \"{f.Name}\" a negative size");
            }
            if (m.Files.Sum(f => f.Size) > MaxWorldBytes) throw new SnapshotInvalidException("it claims to be far larger than any saved world");
            foreach (var e in zip.Entries)
            {
                if (e.FullName == ManifestEntry) continue;
                if (!e.FullName.StartsWith(WorldPrefix, StringComparison.Ordinal) || !names.Contains(e.FullName[WorldPrefix.Length..]))
                    throw new SnapshotInvalidException($"it holds \"{e.FullName}\", which its manifest does not list");
            }
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in m.Files)
            {
                var e = zip.GetEntry(WorldPrefix + f.Name) ?? throw new SnapshotInvalidException($"\"{f.Name}\" is missing from it");
                byte[] data = ReadExactly(e, f.Size, f.Name);
                if (Sha256(data) != f.Sha256.ToLowerInvariant())
                    throw new SnapshotInvalidException($"\"{f.Name}\" does not match its hash: the copy is damaged or incomplete");
                files[f.Name] = data;
            }
            if (WorldHash(m.Files) != m.WorldHash.ToLowerInvariant())
                throw new SnapshotInvalidException("its world hash does not match its list of files");
            return new VerifiedSnapshot(m, files);
        }
        catch (SnapshotInvalidException) { throw; }
        catch (Exception ex) { throw new SnapshotInvalidException("the file could not be read whole (" + ex.Message + ")", ex); }
    }

    /// <summary>An entry's bytes, refusing one longer or shorter than the manifest says (read with a cap, never trusting the entry's own length).</summary>
    private static byte[] ReadExactly(ZipArchiveEntry e, long size, string name)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        var buf = new byte[81920];
        int n;
        while ((n = s.Read(buf, 0, buf.Length)) > 0)
        {
            ms.Write(buf, 0, n);
            if (ms.Length > size) throw new SnapshotInvalidException($"\"{name}\" is longer than its manifest says");
        }
        if (ms.Length != size) throw new SnapshotInvalidException($"\"{name}\" is shorter than its manifest says: the copy is incomplete");
        return ms.ToArray();
    }

    // ---- names ----

    private static readonly Regex NameRx = new(@"^(?<t>\d{8}T\d{6}Z)_(?<d>[^_]+?)(?:_(?<n>\d+))?\.odsave$", RegexOptions.CultureInvariant);

    /// <summary>A Mac's name as it goes into a file name: letters, digits and hyphens, nothing else.</summary>
    public static string DeviceTag(string device)
    {
        var sb = new StringBuilder();
        foreach (char c in device) sb.Append(char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-');
        string s = sb.ToString().Trim('-');
        return s.Length == 0 ? "Mac" : s.Length > 40 ? s[..40] : s;
    }

    /// <summary>The file name for a snapshot made at <paramref name="utc"/> on <paramref name="device"/>; <paramref name="n"/> above 1 tells apart two in the same second.</summary>
    public static string FileName(DateTime utc, string device, int n = 1) =>
        utc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + "_" + DeviceTag(device)
        + (n > 1 ? "_" + n.ToString(CultureInfo.InvariantCulture) : "") + Extension;

    /// <summary>When and where a snapshot was made, from its file name alone (for one not yet downloaded); null for a name not of that form.</summary>
    public static (DateTime Utc, string DeviceTag)? ParseName(string fileName)
    {
        var m = NameRx.Match(fileName);
        if (!m.Success) return null;
        if (!DateTime.TryParseExact(m.Groups["t"].Value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
                                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)) return null;
        return (DateTime.SpecifyKind(t, DateTimeKind.Utc), m.Groups["d"].Value);
    }
}
