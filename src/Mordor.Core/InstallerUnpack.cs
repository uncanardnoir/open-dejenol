using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Mordor.Core.Extraction;

namespace Mordor.Core;

/// <summary>
/// What <see cref="InstallerUnpack.Unpack"/> made of an installer: the game folder, or why there is
/// none. <see cref="Message"/> is for the player; <see cref="Detail"/>, when there is one, is the
/// technical reason behind it, for the crash log.
/// </summary>
public sealed record UnpackResult(string? Dir, string Message, GameFilesCheck? Check, IReadOnlyList<string> Files, IReadOnlyList<string> Skipped, string? Detail = null)
{
    public bool Ok => Dir != null;
}

/// <summary>
/// Mordor from Decklin's Domain's installers, MordorSetup.exe (the full game, 1.1) and
/// MordorDemoSetup.exe (the shareware), without running them. Both are Tarma InstallMate 9 setups
/// of 2016 that refuse Windows 10 and 11 ("Windows 10 (x64) ... not supported"; compatibility modes
/// do not help), so a player who buys Mordor today could not install it. The port reads the
/// installer's payload itself, as docs/GAME_FILES.md ("The installers") sets out:
///
/// <list type="number">
/// <item>The setup program is a Windows PE file whose section <c>.tsuarch</c> holds the archive:
///   16 bytes, then a <c>tiz3</c> header - its length at +4 (0x3C), its total length from the
///   section's start at +0x10, and at +0x38 the five property bytes of an LZMA stream that follows
///   at +0x3D and ends with LZMA's end marker.</item>
/// <item>That stream unpacks to a run of <c>tzf3</c> records, back to back to the last byte: a 0x40
///   byte header (the record's 8-byte key at +8, its length at +0x10) and the record's bytes. The
///   first record, key 2, is the InstallMate project (<c>tin9</c>); a few more with small keys are
///   the setup's own runtime; the rest are the installed files, one each, stored whole.</item>
/// <item>The project names them. A <c>file</c> entry has the record's key at +8, the folder's key
///   at +0x2C, the size at +0x3C and, at +0x54, the name (a 32-bit length, the characters and a
///   32-bit zero) followed the same way by the name it was stored under (the name, "_", sixteen
///   hex digits). A <c>fldr</c> entry has its own key at +8, then its parent's key and its two names
///   (an internal one - INSTALLDIR for the install folder, System16Folder for Windows' 16-bit
///   System folder - and the folder's name).</item>
/// </list>
///
/// <para>What is unpacked: everything the installer puts in the install folder, and the Visual
/// Basic 3 runtime it puts in Windows' System folder, as an installed Mordor would have them. The
/// one other file, WININIT.INI for the Windows folder, is the installer's own business and is
/// skipped. The install folder's layout is the original's - MORDOR.EXE and the help at the top,
/// the data in DATA\ (gc1C74), the sounds and music in MUSIC\ (gc1C78), and SSAVER\, html\ and
/// images\ beside them - but the port reads all its files from one folder (<see cref="GameFiles"/>),
/// so DATA\, MUSIC\ and images\ (cityw1.png, <see cref="GameArt"/>) are unpacked into the top
/// folder too; html\ and SSAVER\, which the port does not read, keep their folders.</para>
///
/// <para>The installer is a file the player chose, so it is read as untrusted: the PE headers, the
/// archive's lengths and the records are checked before use, sizes are capped, every name is a
/// plain file or folder name (no "..", no separator, no drive, no character Windows refuses), and
/// the files are written to a fresh folder that is moved into place only when the whole game is
/// there and <see cref="GameFiles.Check"/> plays it. Anything wrong is a message, never a throw.</para>
/// </summary>
public static class InstallerUnpack
{
    /// <summary>
    /// The message for a file that is not one of the installers this reads, then what to choose
    /// instead (<see cref="NotAnInstallerHint"/>). What was wrong with the file is the result's
    /// <see cref="UnpackResult.Detail"/>, for the crash log, not the player.
    /// </summary>
    public const string NotAnInstaller = "This file isn't a Mordor installer OpenDejenol can read.";
    public const string NotAnInstallerHint = "Choose MordorSetup.exe or MordorDemoSetup.exe, as downloaded from Decklin's Domain.";

    /// <summary>The installers' names on Decklin's Domain.</summary>
    public static readonly string[] InstallerNames = { "MordorSetup.exe", "MordorDemoSetup.exe" };

    /// <summary>Moves <see cref="Root"/>, for the checks.</summary>
    public const string EnvRoot = "MORDOR_UNPACK_DIR";

    // caps on a user-chosen file: the full installer is 6 MB and unpacks to 14 MB in 310 records
    private const long MaxInstaller = 64L << 20;
    private const int MaxUnpacked = 128 << 20;
    private const int MaxRecords = 10000;
    private const int MaxNameLength = 128;
    private const int MaxDepth = 16;

    /// <summary>
    /// Where unpacked games go: <c>%LOCALAPPDATA%\OpenDejenol\game</c> (<c>MORDOR_UNPACK_DIR</c>
    /// replaces it), beside the art cache - the port's own, re-made from the installer if lost.
    /// </summary>
    public static string Root
    {
        get
        {
            string? env = Environment.GetEnvironmentVariable(EnvRoot);
            if (!string.IsNullOrWhiteSpace(env)) return env;
            return Path.Combine(UserFolders.Local, "game");
        }
    }

    /// <summary>
    /// The folder in <paramref name="root"/> for the full game, <c>full</c>, or the shareware,
    /// <c>shareware</c>: one each, so unpacking the demo never replaces the full game or the other
    /// way round, and unpacking the same release again replaces it with the same files.
    /// </summary>
    public static string EditionDir(string root, bool full) => Path.Combine(root, full ? "full" : "shareware");

    /// <summary>One file of the game: where it goes, relative to the game folder, and its bytes.</summary>
    public sealed record Entry(string RelativePath, byte[] Data);

    /// <summary>What an installer holds, as <see cref="Read"/> found it.</summary>
    public sealed record Contents(IReadOnlyList<Entry> Files, IReadOnlyList<string> Skipped);

    /// <summary>
    /// The game's files out of an installer, in the layout the port reads (see the class). Throws
    /// <see cref="InvalidDataException"/>, with what was wrong, for anything that is not such an
    /// installer or does not read whole.
    /// </summary>
    public static Contents Read(string installer)
    {
        var fi = new FileInfo(installer);
        if (!fi.Exists) throw new InvalidDataException("there is no such file");
        if (fi.Length > MaxInstaller) throw new InvalidDataException("it is far larger than Mordor's installers");
        return Parse(File.ReadAllBytes(installer));
    }

    /// <summary><see cref="Read"/>, from the installer's bytes.</summary>
    public static Contents Parse(byte[] exe) => ReadArchive(Unwrap(exe));

    /// <summary>The installer's archive, unpacked: its tzf3 records, back to back (steps 1 and 2 of the class).</summary>
    public static byte[] Unwrap(byte[] exe)
    {
        var (archive, archiveLength) = Archive(exe);
        return Payload(exe, archive, archiveLength);
    }

    /// <summary>The game's files out of an unpacked archive (<see cref="Unwrap"/>): step 3 of the class, and the layout.</summary>
    public static Contents ReadArchive(byte[] arc)
    {
        var records = Records(arc);
        if (!records.TryGetValue(ProjectKey, out var project) || !Tag(arc, project.Offset, "tin9"))
            throw new InvalidDataException("the archive has no InstallMate project");
        var (folders, files) = Project(arc, project.Offset, project.Length);
        return Layout(arc, records, folders, files);
    }

    // ---- the PE file and its .tsuarch section ----

    /// <summary>Where the archive starts in the file, and how long it says it is.</summary>
    private static (int Start, int Length) Archive(byte[] f)
    {
        if (f.Length < 0x40 || f[0] != 'M' || f[1] != 'Z') throw new InvalidDataException("it is not a Windows program");
        int pe = I32(f, 0x3C);
        if (pe < 0x40 || pe > f.Length - 24 || !Tag(f, pe, "PE\0\0")) throw new InvalidDataException("it is not a Windows program");
        int sections = U16(f, pe + 6), optional = U16(f, pe + 20);
        int table = pe + 24 + optional;
        if (sections > 96 || table < 0 || (long)table + sections * 40L > f.Length) throw new InvalidDataException("its section table runs off the end of the file");
        for (int i = 0; i < sections; i++)
        {
            int s = table + i * 40;
            if (Encoding.ASCII.GetString(f, s, 8).TrimEnd('\0') != ".tsuarch") continue;
            long raw = (uint)I32(f, s + 16), ptr = (uint)I32(f, s + 20);
            if (ptr + raw > f.Length || raw < 0x60) throw new InvalidDataException("its archive runs off the end of the file");
            int start = (int)ptr;
            if (!Tag(f, start + 0x10, "tiz3") || U16(f, start + 0x14) != 0x3C) throw new InvalidDataException("its archive is not an InstallMate tiz3 archive");
            long length = (uint)I32(f, start + 0x20);
            if (length < 0x4D || length > raw) throw new InvalidDataException("its archive's length is wrong");
            return (start, (int)length);
        }
        throw new InvalidDataException("it has no InstallMate archive (.tsuarch)");
    }

    /// <summary>The archive's LZMA stream, unpacked; it must end with its end marker exactly where the archive does.</summary>
    private static byte[] Payload(byte[] f, int start, int length)
    {
        int props = start + 0x10 + 0x38, data = start + 0x10 + 0x3D;
        byte[] arc;
        int used;
        try { arc = Lzma.DecodeToEnd(f.AsSpan(props, 5), f, data, start + length - data, MaxUnpacked, out used); }
        catch (InvalidDataException ex) { throw new InvalidDataException("its archive does not unpack (" + ex.Message + ")"); }
        if (data + used != start + length) throw new InvalidDataException("its archive does not end where it says");
        return arc;
    }

    /// <summary>The project's key: 2 in its low word, the rest ones (the setup's own records are numbered so, the files keyed by hash).</summary>
    private const ulong ProjectKey = 0xFFFFFFFFFFFF0002;

    private readonly record struct Record(int Offset, int Length);

    /// <summary>The tzf3 records, by key: back to back, the last ending at the archive's last byte.</summary>
    private static Dictionary<ulong, Record> Records(byte[] arc)
    {
        var map = new Dictionary<ulong, Record>();
        long at = 0;
        while (at < arc.Length)
        {
            if (at + 0x40 > arc.Length || !Tag(arc, (int)at, "tzf3")) throw new InvalidDataException("its archive's records do not read");
            long len = BitConverter.ToInt64(arc, (int)at + 0x10);
            if (len < 0 || len > arc.Length - at - 0x40) throw new InvalidDataException("a record runs off the end of the archive");
            ulong key = BitConverter.ToUInt64(arc, (int)at + 8);
            if (!map.TryAdd(key, new Record((int)at + 0x40, (int)len))) throw new InvalidDataException("two records have the same key");
            if (map.Count > MaxRecords) throw new InvalidDataException("the archive has too many records");
            at += 0x40 + len;
        }
        return map;
    }

    // ---- the project: folders and files ----

    private sealed record Folder(ulong Key, ulong Parent, string Internal, string Name);
    private sealed record FileEntry(ulong Key, ulong Folder, long Size, string Name);

    /// <summary>
    /// The project's <c>fldr</c> and <c>file</c> entries. They are found by their tags and taken
    /// only when every field reads as it should - a tag that turns up inside other data does not.
    /// </summary>
    private static (Dictionary<ulong, Folder>, List<FileEntry>) Project(byte[] a, int start, int length)
    {
        var folders = new Dictionary<ulong, Folder>();
        var files = new List<FileEntry>();
        int end = start + length;
        for (int i = start; i + 0x60 <= end; i++)
        {
            if (a[i] == 'f' && a[i + 1] == 'i' && Tag(a, i, "file") && I32(a, i + 4) == 0)
            {
                int at = i + 0x54;
                if (!ReadName(a, ref at, end, out string? name) || !ReadName(a, ref at, end, out string? stored)) continue;
                if (!IsStoredName(stored!, name!)) continue;
                files.Add(new FileEntry(U64(a, i + 8), U64(a, i + 0x2C), BitConverter.ToInt64(a, i + 0x3C), name!));
            }
            else if (a[i] == 'f' && a[i + 1] == 'l' && Tag(a, i, "fldr") && I32(a, i + 4) == 0)
            {
                // the parent's key and the names follow a run of fields whose length varies with the
                // kind of folder (0x2C for the install's own, 0x28 and 0x24 for Windows' and
                // InstallMate's): the one at which both names read is the one
                foreach (int p in new[] { 0x2C, 0x28, 0x24 })
                {
                    int at = i + p + 8;
                    if (!ReadName(a, ref at, end, out string? inner) || !ReadName(a, ref at, end, out string? name)) continue;
                    folders.TryAdd(U64(a, i + 8), new Folder(U64(a, i + 8), U64(a, i + p), inner!, name!));
                    break;
                }
            }
        }
        return (folders, files);
    }

    /// <summary>A 32-bit length, that many printable characters, and a 32-bit zero.</summary>
    private static bool ReadName(byte[] a, ref int at, int end, out string? s)
    {
        s = null;
        if (at + 4 > end) return false;
        int n = I32(a, at);
        if (n < 1 || n > 260 || at + 4 + n + 4 > end) return false;
        for (int k = 0; k < n; k++) if (a[at + 4 + k] < 0x20 || a[at + 4 + k] > 0x7E) return false;
        if (I32(a, at + 4 + n) != 0) return false;
        s = Encoding.ASCII.GetString(a, at + 4, n);
        at += 8 + n;
        return true;
    }

    /// <summary>
    /// "MORDOR.EXE_C295531D7C6889B4": the name, "_", sixteen hex digits - with some characters of
    /// the name stored as "_" ("DI_ANIM.WAV_25C94EF79CE94451" for DI-ANIM.WAV).
    /// </summary>
    private static bool IsStoredName(string stored, string name)
    {
        if (stored.Length != name.Length + 17 || stored[name.Length] != '_' || !stored[(name.Length + 1)..].All(Uri.IsHexDigit)) return false;
        for (int k = 0; k < name.Length; k++)
            if (stored[k] != name[k] && stored[k] != '_') return false;
        return true;
    }

    // ---- where each file goes ----

    private const string InstallDir = "INSTALLDIR", System16 = "System16Folder";

    /// <summary>The install folder's subfolders whose files the port reads from its one game folder.</summary>
    private static readonly HashSet<string> Flattened = new(StringComparer.OrdinalIgnoreCase) { "DATA", "MUSIC", "images" };

    private static Contents Layout(byte[] arc, Dictionary<ulong, Record> records, Dictionary<ulong, Folder> folders, List<FileEntry> files)
    {
        var install = folders.Values.Where(f => f.Internal == InstallDir).ToList();
        if (install.Count != 1) throw new InvalidDataException("its project has no install folder");
        var taken = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<string>();
        foreach (var file in files)
        {
            var path = PathOf(file, folders, install[0].Key);
            if (path == null) { skipped.Add(Describe(file, folders)); continue; }
            if (!records.TryGetValue(file.Key, out var rec)) throw new InvalidDataException(file.Name + " is named but not in the archive");
            if (rec.Length != file.Size) throw new InvalidDataException(file.Name + " is not the size the project says");
            string rel = string.Join(Path.DirectorySeparatorChar, path);
            var data = arc.AsSpan(rec.Offset, rec.Length).ToArray();
            if (taken.TryGetValue(rel, out var already))
            {
                // the same file put in two places that the port's layout makes one: harmless if the same
                if (already.Data.AsSpan().SequenceEqual(data)) continue;
                throw new InvalidDataException("two different files would both be " + rel);
            }
            taken[rel] = new Entry(rel, data);
        }
        if (taken.Count == 0) throw new InvalidDataException("it installs no files");
        // a record the project names no file for, other than the setup's own: said, so a file the
        // project names in some way this does not read is not lost without a word
        var named = files.Select(f => f.Key).ToHashSet();
        int unnamed = records.Keys.Count(k => (k >> 16) != 0xFFFFFFFFFFFF && !named.Contains(k));
        if (unnamed > 0) skipped.Add(unnamed + " record" + (unnamed == 1 ? "" : "s") + " the project names no file for");
        return new Contents(taken.Values.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase).ToList(), skipped);
    }

    /// <summary>The file's path in the game folder, as names; null for a file that goes elsewhere (skipped).</summary>
    private static List<string>? PathOf(FileEntry file, Dictionary<ulong, Folder> folders, ulong install)
    {
        CheckName(file.Name);
        var parts = new List<string> { file.Name };
        ulong at = file.Folder;
        for (int depth = 0; ; depth++)
        {
            if (depth > MaxDepth) throw new InvalidDataException("its folders go round in a circle");
            if (at == install) break;
            if (!folders.TryGetValue(at, out var folder)) return null;
            if (folder.Internal == System16 && depth == 0) return parts; // the VB3 runtime, beside MORDOR.EXE
            CheckName(folder.Name);
            parts.Insert(0, folder.Name);
            at = folder.Parent;
        }
        // DATA\, MUSIC\ and images\ straight under the install folder: into the top folder
        if (parts.Count == 2 && Flattened.Contains(parts[0])) parts.RemoveAt(0);
        return parts;
    }

    private static string Describe(FileEntry file, Dictionary<ulong, Folder> folders) =>
        file.Name + (folders.TryGetValue(file.Folder, out var f) ? " (for " + f.Name + ")" : "");

    /// <summary>A plain file or folder name: nothing that could reach outside the folder it is put in.</summary>
    private static void CheckName(string name)
    {
        if (!IsPlainName(name)) throw new InvalidDataException("it names a file \"" + name + "\" that cannot be written safely");
    }

    /// <summary>
    /// A name with no separator, drive, "." or "..", character Windows refuses, or trailing dot or
    /// space, and no device name (CON, NUL ...).
    /// </summary>
    public static bool IsPlainName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength) return false;
        if (name == "." || name == ".." || name.EndsWith('.') || name.EndsWith(' ')) return false;
        foreach (char c in name)
            if (c < 0x20 || "<>:\"/\\|?*".Contains(c)) return false;
        string stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsAsciiDigit(stem[3])))
            return false;
        return !Path.IsPathRooted(name);
    }

    // ---- unpacking into place ----

    /// <summary>
    /// Unpack an installer into <paramref name="root"/> (<see cref="Root"/> when null): read it,
    /// write its files into a new folder there, check the folder is a Mordor to play, and move it to
    /// its edition's folder (<see cref="EditionDir"/>), replacing an earlier unpacking of that
    /// edition. Nothing is written outside <paramref name="root"/>, and a failure leaves what was
    /// there before as it was. Never throws.
    /// </summary>
    public static UnpackResult Unpack(string installer, string? root = null)
    {
        var none = Array.Empty<string>();
        Contents contents;
        try { contents = Read(installer); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new UnpackResult(null, NotAnInstaller + " " + NotAnInstallerHint, null, none, none, Path.GetFileName(installer) + ": " + ex.Message);
        }

        string top, work;
        try
        {
            top = Path.GetFullPath(root ?? Root);
            Directory.CreateDirectory(top);
            work = Path.Combine(top, ".unpacking-" + Guid.NewGuid().ToString("N")[..12]);
            Directory.CreateDirectory(work);
        }
        catch (Exception ex)
        {
            return new UnpackResult(null, "The game could not be unpacked: there is no folder for it (" + ex.Message + ").", null, none, none);
        }

        try
        {
            string within = Path.TrimEndingDirectorySeparator(work) + Path.DirectorySeparatorChar;
            foreach (var e in contents.Files)
            {
                string dest = Path.GetFullPath(Path.Combine(work, e.RelativePath));
                // the names were checked one by one; this is the belt to those braces
                if (!dest.StartsWith(within, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("a file would go outside the game folder");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.WriteAllBytes(dest, e.Data);
            }
            var check = GameFiles.Check(work);
            if (!check.IsPlayable)
            {
                TryDelete(work);
                return new UnpackResult(null, NotAnInstaller + " " + NotAnInstallerHint, check, none, none,
                                        Path.GetFileName(installer) + ": it unpacks, but not to a Mordor to play - " + check.Reason);
            }
            string dir = EditionDir(top, check.IsFull);
            string? old = null;
            if (Directory.Exists(dir))
            {
                old = Path.Combine(top, ".replaced-" + Guid.NewGuid().ToString("N")[..12]);
                Directory.Move(dir, old);
            }
            try { Directory.Move(work, dir); }
            catch
            {
                if (old != null) try { Directory.Move(old, dir); } catch { }
                throw;
            }
            if (old != null) TryDelete(old);
            var placed = GameFiles.Check(dir);
            return new UnpackResult(dir, "Unpacked " + (placed.Recognised ?? (placed.IsFull ? "the full game" : "the shareware")) + " into " + dir + ".",
                                    placed, contents.Files.Select(f => f.RelativePath).ToList(), contents.Skipped);
        }
        catch (Exception ex)
        {
            TryDelete(work);
            return new UnpackResult(null, "The game could not be unpacked (" + ex.Message + ").", null, none, none);
        }
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
    }

    /// <summary>The unpacked games in <see cref="Root"/>, for <see cref="GameFolder"/> to find: the full game's first.</summary>
    public static IEnumerable<string> Unpacked(string? root = null)
    {
        string top = root ?? Root;
        yield return EditionDir(top, full: true);
        yield return EditionDir(top, full: false);
    }

    // ---- the installers in the player's Downloads ----

    /// <summary>
    /// MordorSetup.exe and MordorDemoSetup.exe in the player's Downloads folder (the known folder,
    /// wherever it has been moved to), the full game's first; for the first-run window to offer.
    /// </summary>
    public static IReadOnlyList<string> InDownloads(string? downloads = null)
    {
        string? dir = downloads ?? DownloadsFolder();
        var found = new List<string>();
        if (string.IsNullOrEmpty(dir)) return found;
        foreach (string name in InstallerNames)
        {
            try
            {
                string p = Path.Combine(dir, name);
                if (File.Exists(p)) found.Add(p);
            }
            catch { }
        }
        return found;
    }

    /// <summary>Replaces the Downloads folder, for the checks.</summary>
    public const string EnvDownloads = "MORDOR_DOWNLOADS";

    /// <summary>
    /// The Downloads known folder (FOLDERID_Downloads), else the profile's Downloads; null when
    /// there is neither. <c>MORDOR_DOWNLOADS</c> replaces it.
    /// </summary>
    public static string? DownloadsFolder()
    {
        string? env = Environment.GetEnvironmentVariable(EnvDownloads);
        if (!string.IsNullOrWhiteSpace(env)) return env;
        if (OperatingSystem.IsWindows())
        {
            string? known = KnownDownloads();
            if (!string.IsNullOrEmpty(known)) return known;
        }
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return profile == "" ? null : Path.Combine(profile, "Downloads");
    }

    [SupportedOSPlatform("windows")]
    private static string? KnownDownloads()
    {
        try
        {
            var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            if (SHGetKnownFolderPath(ref downloads, 0, IntPtr.Zero, out IntPtr p) != 0) return null;
            try { return Marshal.PtrToStringUni(p); }
            finally { Marshal.FreeCoTaskMem(p); }
        }
        catch { return null; }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);

    // ---- bytes ----

    private static bool Tag(byte[] a, int at, string tag)
    {
        if (at < 0 || at + tag.Length > a.Length) return false;
        for (int k = 0; k < tag.Length; k++) if (a[at + k] != tag[k]) return false;
        return true;
    }

    private static int U16(byte[] a, int at) => at >= 0 && at + 2 <= a.Length ? a[at] | (a[at + 1] << 8) : throw new InvalidDataException("it ends too soon");
    private static int I32(byte[] a, int at) => at >= 0 && at + 4 <= a.Length ? BitConverter.ToInt32(a, at) : throw new InvalidDataException("it ends too soon");
    private static ulong U64(byte[] a, int at) => at >= 0 && at + 8 <= a.Length ? BitConverter.ToUInt64(a, at) : throw new InvalidDataException("it ends too soon");
}
