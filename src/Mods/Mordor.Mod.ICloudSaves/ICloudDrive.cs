using System.Runtime.InteropServices;

namespace Mordor.Mod.ICloudSaves;

/// <summary>
/// Where the snapshots go: the player's iCloud Drive folder, <c>~/Library/Mobile
/// Documents/com~apple~CloudDocs/OpenDejenol</c>, with plain file I/O. macOS syncs that folder
/// itself.
///
/// Why not CloudKit or an app's own iCloud container: both need the iCloud entitlement, and an
/// entitlement needs a provisioning profile from a paid Apple Developer account; OpenDejenol.app is
/// signed ad hoc (tools/pack-macos.sh), so it can have neither. The iCloud Drive folder needs
/// nothing: it is an ordinary folder in the player's home that any program they run may write.
///
/// <c>com~apple~CloudDocs</c> is there only while iCloud Drive is turned on for this Mac (System
/// Settings, Apple Account, iCloud, iCloud Drive). When it is missing the mod says so and does
/// nothing: creating it, or OpenDejenol inside a folder that is not synced, would put the player's
/// copies somewhere that looks right and goes nowhere.
/// </summary>
public static class ICloudDrive
{
    /// <summary>
    /// The iCloud Drive folder to use instead of the real one: the checks point it at a temporary
    /// folder, so nothing is ever read from or written to a player's iCloud Drive in a test.
    /// </summary>
    public const string EnvOverride = "MORDOR_ICLOUD";

    /// <summary>The mod's folder inside iCloud Drive.</summary>
    public const string FolderName = "OpenDejenol";

    /// <summary>iCloud Drive's own folder on this Mac (or <see cref="EnvOverride"/>'s).</summary>
    public static string CloudDocs =>
        Environment.GetEnvironmentVariable(EnvOverride) is { Length: > 0 } o ? o
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                       "Library", "Mobile Documents", "com~apple~CloudDocs");

    /// <summary>
    /// The mod's folder in iCloud Drive and null, or null and why there is none. Only names the folder:
    /// the upload makes it, inside an iCloud Drive that is there.
    /// </summary>
    public static (string? Folder, string? Problem) Locate()
    {
        string docs = CloudDocs;
        try
        {
            if (!Directory.Exists(docs))
                return (null, "iCloud Drive is not turned on on this Mac, so there is nowhere to keep the saves. "
                            + "Turn it on in System Settings > Apple Account > iCloud > iCloud Drive, then start OpenDejenol again. "
                            + $"(Looked for {docs}.)");
        }
        catch (Exception ex) { return (null, $"iCloud Drive could not be looked at ({ex.Message})."); }
        return (Path.Combine(docs, FolderName), null);
    }

    // ---- files iCloud has not downloaded ----

    /// <summary>
    /// SF_DATALESS from &lt;sys/stat.h&gt;: the file's name and size are on this Mac but its contents
    /// are still in iCloud ("Optimise Mac Storage", or not downloaded yet). Opening such a file makes
    /// macOS fetch it there and then, which can stall for as long as the network takes; the mod never
    /// does that on the game's behalf, and never restores from one.
    /// </summary>
    private const uint SfDataless = 0x40000000;

    // stat(2) with 64-bit inodes, the only kind on arm64; on x86_64 that one is exported as stat$INODE64
    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int StatArm64(string path, byte[] buf);

    [DllImport("libc", EntryPoint = "stat$INODE64", SetLastError = true)]
    private static extern int StatX64(string path, byte[] buf);

    // Offsets in Darwin's 64-bit-inode struct stat (sys/stat.h, __DARWIN_STRUCT_STAT64): st_size, an
    // off_t, at 96; st_flags, a uint32, at 116. The struct is 144 bytes; the buffer is roomier.
    private const int SizeOffset = 96, FlagsOffset = 116;

    private static byte[]? Stat(string path)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        try
        {
            var buf = new byte[256];
            int r = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? StatArm64(path, buf) : StatX64(path, buf);
            return r == 0 ? buf : null;
        }
        catch { return null; }
    }

    /// <summary>Whether macOS holds only the file's name here and its contents are in iCloud. False off macOS and when it cannot be told.</summary>
    public static bool IsDataless(string path) =>
        Stat(path) is { } b && (BitConverter.ToUInt32(b, FlagsOffset) & SfDataless) != 0;

    /// <summary>
    /// The file's size as stat(2) gives it, or -1 off macOS: for the checks, which compare it with
    /// .NET's own to prove the offsets above read this Mac's struct stat rightly.
    /// </summary>
    public static long StatSize(string path) => Stat(path) is { } b ? BitConverter.ToInt64(b, SizeOffset) : -1;

    /// <summary>
    /// The placeholder iCloud Drive leaves for a file it has not downloaded on older systems:
    /// <c>.name.icloud</c>, hidden, beside where the file will be. The real name, or null when
    /// <paramref name="fileName"/> is not one.
    /// </summary>
    public static string? PlaceholderFor(string fileName) =>
        fileName.Length > ".x.icloud".Length - 1 && fileName.StartsWith('.') && fileName.EndsWith(".icloud", StringComparison.OrdinalIgnoreCase)
            ? fileName[1..^".icloud".Length]
            : null;
}
