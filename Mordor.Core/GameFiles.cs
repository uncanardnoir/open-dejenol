using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Mordor.Core;

/// <summary>What <see cref="GameFiles.Check"/> made of a folder.</summary>
public enum GameFilesVerdict
{
    /// <summary>A full (commercial) Mordor: every file the port reads, and all fifteen levels.</summary>
    Full,
    /// <summary>
    /// Mordor's shareware ("PUBLIC Limited Version") data: played, with the limited build's
    /// behaviour switched on (<see cref="LimitedVersion"/>).
    /// </summary>
    Limited,
    /// <summary>No such folder.</summary>
    NotFound,
    /// <summary>The folder is there, but files the port needs are not.</summary>
    Missing,
    /// <summary>The files are there, but they are not Mordor's.</summary>
    NotMordor,
    /// <summary>Mordor's files, but one of them does not read.</summary>
    Damaged,
}

/// <summary>The answer for one folder, and why.</summary>
public sealed record GameFilesCheck(string Dir, GameFilesVerdict Verdict, string Reason, string? Recognised = null)
{
    public bool IsFull => Verdict == GameFilesVerdict.Full;

    /// <summary>The shareware's data: played as the limited build plays.</summary>
    public bool IsLimited => Verdict == GameFilesVerdict.Limited;

    /// <summary>Something to play: the full game, or the shareware as the limited build.</summary>
    public bool IsPlayable => IsFull || IsLimited;
}

/// <summary>
/// Is this folder a Mordor the port can play, and which release? The port plays the player's own
/// copy of the game: it reads the data files of an installed Mordor and ships none of its own, so
/// without them there is nothing to play - that, not any check here, is what keeps it to players
/// who have the game. This only finds out whether the files the port reads are there and read,
/// and whether they are the full game's or the shareware's. docs/GAME_FILES.md has the evidence;
/// in short:
///
/// <para>The original tells its two releases apart at compile time, not at run time. The
/// shareware build is the same source with its "limited" constants switched on: m009C (the
/// "(Limited Version)" caption, FRMMAIN.BAS:148-152, and the goodbye box, 240-248), the level
/// clamp in fn0A0B (MAZEMOD.BAS:409-422, "LIMITED VERSION: Request to go to Level n is Denied"),
/// the dead chutes (MAZEMOD.BAS:1795, 1841; MAINMOD2.BAS:3998), the "PUBLIC version" box
/// (MAINMOD2.BAS:6339-6345). In the full MORDOR.EXE every one of those is a constant False and
/// the clamp is 15, the deepest level - but all their strings are still in the file, so there is
/// no string to look for. What differs is the data: MORDOR.WRI says "the only difference ... is
/// that the Shareware version is limited to 3 dungeon levels", and FRMORDER.BAS that the full
/// version has "so many more Monsters, Items and Dungeon Levels than the PUBLIC version".</para>
///
/// <para>So the port goes by the data, and by what it holds rather than its exact bytes (another
/// full release - a 1.0, a CD against a download - need not be byte for byte the same): MDATA11
/// holding the fifteen levels fn0A0B allows, a bestiary and treasure that reach the fifteenth, is
/// the full game; less is the shareware, which is played as the limited build plays
/// (<see cref="LimitedVersion"/>) - a change of behaviour, as between the original's two builds,
/// never a refusal. A known MORDOR.EXE is only named, never required.</para>
/// </summary>
public static class GameFiles
{
    /// <summary>The deepest level: fn0A0B's mc015C and, in the full build, its clamp mc015E (MAZEMOD.BAS:409-422).</summary>
    public const int FullLevels = 15;

    /// <summary>"the Shareware version is limited to 3 dungeon levels" (MORDOR.WRI, Mordor 1.1 Information).</summary>
    public const int SharewareLevels = 3;

    /// <summary>
    /// What the port reads out of the game folder: the program (its artwork came out of it and
    /// MG11.DLL; it is how a Mordor folder is known), the guild and race tables, the spells, the
    /// items, the monsters and the dungeon. Sounds and music are played when they are there and
    /// are not required.
    /// </summary>
    public static readonly string[] Required =
        { "MORDOR.EXE", "MDATA1.MDR", "MDATA2.MDR", "MDATA3.MDR", "MDATA5.MDR", "MDATA11.MDR" };

    /// <summary>
    /// MORDOR.EXEs that are known, by SHA-256: a name for the log and the first-run screen, never
    /// a requirement. The 1.1 release is the copy installed from MordorSetup.exe (Tarma InstallMate
    /// 9, "Installer for Mordor v1.1 The Depths of Dejenol"), whose decompile gives m009C = 0.
    /// </summary>
    private static readonly Dictionary<string, string> KnownExe = new(StringComparer.OrdinalIgnoreCase)
    {
        ["5284A6A71A10259C97534FD38C3D22F61A3D792065C2999333FC63E80BA7D197"] = "Mordor 1.1 (full release)",
    };

    public static GameFilesCheck Check(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return new GameFilesCheck(dir ?? "", GameFilesVerdict.NotFound, "No folder was given.");
        string full;
        try { full = Path.GetFullPath(dir); }
        catch { return new GameFilesCheck(dir, GameFilesVerdict.NotFound, "That is not a folder name."); }
        if (!Directory.Exists(full))
            return new GameFilesCheck(full, GameFilesVerdict.NotFound, "There is no such folder.");

        string P(string f) => Path.Combine(full, f);
        var missing = Required.Where(f => !File.Exists(P(f))).ToList();
        if (missing.Count == Required.Length)
            return new GameFilesCheck(full, GameFilesVerdict.Missing, "This folder has none of Mordor's files in it (MORDOR.EXE, MDATA1.MDR ...).");
        if (missing.Count > 0)
            return new GameFilesCheck(full, GameFilesVerdict.Missing, "Mordor's files are not all here: " + string.Join(", ", missing) + " missing.");

        // Mordor 1.x is a Visual Basic 3 program for Windows 3.1: an MZ file with a New Executable
        // header behind it
        if (!IsWin16Program(P("MORDOR.EXE")))
            return new GameFilesCheck(full, GameFilesVerdict.NotMordor, "MORDOR.EXE here is not the Windows 3.1 program Mordor 1.x is.");

        // MDATA1, 2, 3 and 5 open with a version string ("1.171..." in 1.1); its number is not
        // checked, only that it is one, so another release reads too
        string? version = null;
        foreach (string f in new[] { "MDATA1.MDR", "MDATA2.MDR", "MDATA3.MDR", "MDATA5.MDR" })
        {
            string? v = DataVersion(P(f));
            if (v == null)
                return new GameFilesCheck(full, GameFilesVerdict.NotMordor, f + " here is not a Mordor data file.");
            version ??= v;
        }

        int levels, deepestMonster, deepestItem;
        try
        {
            levels = MdrLoader.LoadDungeon(P("MDATA11.MDR")).Count;
            deepestMonster = MdrLoader.LoadMonsters(P("MDATA5.MDR")).Select(m => (int)m.LevelFound).DefaultIfEmpty(0).Max();
            deepestItem = MdrLoader.LoadItems(P("MDATA3.MDR")).Select(i => (int)i.Floor).DefaultIfEmpty(0).Max();
            MdrLoader.LoadSpells(P("MDATA2.MDR"));
            if (MdrLoader.LoadRaces(P("MDATA1.MDR")).Count == 0 || MdrLoader.LoadGuilds(P("MDATA1.MDR")).Count == 0)
                throw new InvalidDataException("no races or guilds");
        }
        catch (Exception ex)
        {
            return new GameFilesCheck(full, GameFilesVerdict.Damaged, "Mordor's data files here do not read (" + ex.Message + ").");
        }

        // the shareware's data: played, as the limited build (LimitedVersion)
        string? known = Recognise(P("MORDOR.EXE"));
        string share = "Mordor, shareware (Limited Version) data " + version;
        if (levels < FullLevels)
            return new GameFilesCheck(full, GameFilesVerdict.Limited,
                $"The shareware (Limited) version of Mordor: its dungeon has {levels} level{(levels == 1 ? "" : "s")}, the full game's has {FullLevels}.", known ?? share);
        if (deepestMonster < FullLevels)
            return new GameFilesCheck(full, GameFilesVerdict.Limited,
                $"The shareware (Limited) version of Mordor: no monster in it is found below level {deepestMonster}.", known ?? share);
        if (deepestItem < FullLevels)
            return new GameFilesCheck(full, GameFilesVerdict.Limited,
                $"The shareware (Limited) version of Mordor: no item in it is found below level {deepestItem}.", known ?? share);

        return new GameFilesCheck(full, GameFilesVerdict.Full,
            $"The full version of Mordor (data {version}, {levels} levels).", known ?? "Mordor, full version (data " + version + ")");
    }

    /// <summary>"MZ", then "NE" where the MZ header's e_lfanew points.</summary>
    private static bool IsWin16Program(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            var head = new byte[64];
            if (f.Read(head, 0, 64) < 64 || head[0] != 'M' || head[1] != 'Z') return false;
            int ne = BitConverter.ToInt32(head, 0x3C);
            if (ne <= 0 || ne > f.Length - 2) return false;
            f.Seek(ne, SeekOrigin.Begin);
            return f.ReadByte() == 'N' && f.ReadByte() == 'E';
        }
        catch { return false; }
    }

    /// <summary>The version string a data file opens with ("1.171bb..." in 1.1): a digit, a dot, digits.</summary>
    private static string? DataVersion(string path)
    {
        try
        {
            var b = new byte[12];
            using (var f = File.OpenRead(path)) if (f.Read(b, 0, b.Length) < b.Length) return null;
            if (!char.IsAsciiDigit((char)b[2]) || b[3] != '.' || !char.IsAsciiDigit((char)b[4])) return null;
            int end = 4;
            while (end < b.Length && (char.IsAsciiDigit((char)b[end]) || b[end] == '.')) end++;
            return System.Text.Encoding.ASCII.GetString(b, 2, end - 2);
        }
        catch { return null; }
    }

    /// <summary>The name of a known MORDOR.EXE, or null.</summary>
    public static string? Recognise(string exe)
    {
        try
        {
            using var f = File.OpenRead(exe);
            return KnownExe.TryGetValue(Convert.ToHexString(SHA256.HashData(f)), out var name) ? name : null;
        }
        catch { return null; }
    }
}

/// <summary>Where the game folder was found, and how; or the near misses when it was not.</summary>
public sealed record GameFolderResolution(GameFilesCheck? Found, string How, bool Explicit, IReadOnlyList<GameFilesCheck> Tried)
{
    /// <summary>A full Mordor to play.</summary>
    public bool IsFull => Found?.IsFull == true;

    /// <summary>A Mordor to play: the full game, or the shareware as the limited build.</summary>
    public bool IsPlayable => Found?.IsPlayable == true;

    /// <summary>The check to tell the player about when there is nothing to play: the explicit choice, or the nearest miss.</summary>
    public GameFilesCheck? Nearest =>
        Found ?? Tried.FirstOrDefault(t => t.Verdict is GameFilesVerdict.Damaged or GameFilesVerdict.NotMordor)
              ?? Tried.FirstOrDefault(t => t.Verdict == GameFilesVerdict.Missing && !t.Reason.StartsWith("This folder has none", StringComparison.Ordinal));
}

/// <summary>
/// Where the player's Mordor is. In order:
/// <list type="number">
/// <item><c>MORDOR_GAMEDIR</c>, for the test hooks and anyone who wants to say so outright;</item>
/// <item><c>[Loading Information] GameDir=</c> in the port's own launch settings
///   (<see cref="LaunchIniPath"/>), which the first-run screen writes when a folder is picked;</item>
/// <item>found: a <c>game_files</c> folder beside the port or beside any folder above it (as
///   Desktop\mordor\game_files sits beside Desktop\mordor\port), or the port's own folder; the
///   BasePath the original itself reads from MORDOR.INI in the Windows folder; an installed
///   Mordor's uninstall entry; and the usual install folders.</item>
/// </list>
/// The first two are the player's word: a folder named there that cannot be played is reported,
/// not passed over for another. Found folders are tried in order and the first full one is taken;
/// with none, the first shareware one (played as the limited build).
///
/// The launch settings cannot live in the saves folder's settings.ini, where the port keeps its
/// other settings: the saves folder is found from the game folder (<see cref="Saves.DirFor"/>).
/// </summary>
public static class GameFolder
{
    public const string EnvDir = "MORDOR_GAMEDIR";
    public const string EnvIni = "MORDOR_LAUNCH_INI";
    public const string Section = "Loading Information";
    public const string Key = "GameDir";

    /// <summary>
    /// <c>%APPDATA%\OpenDejenol\settings.ini</c>, or <c>MORDOR_LAUNCH_INI</c>. A player's settings
    /// from before the rename are read from <c>%APPDATA%\Mordor Port</c> until that folder is
    /// renamed (<see cref="UserFolders"/>), so the chosen game folder is never forgotten.
    /// </summary>
    public static string LaunchIniPath
    {
        get
        {
            string? own = Environment.GetEnvironmentVariable(EnvIni);
            if (!string.IsNullOrWhiteSpace(own)) return own;
            return Path.Combine(UserFolders.Roaming, "settings.ini");
        }
    }

    /// <summary>The folder the launch settings name, or null.</summary>
    public static string? Configured(string? iniPath = null)
    {
        string v = Ini.Read(iniPath ?? LaunchIniPath).Text(Section, Key);
        return v == "" ? null : v;
    }

    /// <summary>Keep this folder as the player's choice. False when it could not be written; never throws.</summary>
    public static bool Remember(string dir, string? iniPath = null)
    {
        string path = iniPath ?? LaunchIniPath;
        try { if (Path.GetDirectoryName(path) is { Length: > 0 } parent) Directory.CreateDirectory(parent); }
        catch { return false; }
        var ini = Ini.Read(path);
        ini.Set(Section, Key, dir);
        return ini.Write(path);
    }

    public static GameFolderResolution Resolve(string? iniPath = null, string? baseDir = null)
    {
        string? env = Environment.GetEnvironmentVariable(EnvDir);
        if (!string.IsNullOrWhiteSpace(env))
            return new GameFolderResolution(GameFiles.Check(env), EnvDir, true, Array.Empty<GameFilesCheck>());
        string? configured = Configured(iniPath);
        if (configured != null)
            return new GameFolderResolution(GameFiles.Check(configured), "settings (" + (iniPath ?? LaunchIniPath) + ")", true, Array.Empty<GameFilesCheck>());

        var tried = new List<GameFilesCheck>();
        (GameFilesCheck Check, string How)? shareware = null;
        foreach (var (dir, how) in Candidates(baseDir ?? AppContext.BaseDirectory))
        {
            var check = GameFiles.Check(dir);
            if (check.IsFull) return new GameFolderResolution(check, how, false, tried);
            if (check.IsLimited) shareware ??= (check, how);
            else if (check.Verdict != GameFilesVerdict.NotFound) tried.Add(check);
        }
        if (shareware is var (share, shareHow)) return new GameFolderResolution(share, shareHow, false, tried);
        return new GameFolderResolution(null, "not found", false, tried);
    }

    /// <summary>The folders auto-detection looks in, in order, each once.</summary>
    public static IEnumerable<(string Dir, string How)> Candidates(string baseDir)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Raw(baseDir))
        {
            string full;
            try { full = Path.GetFullPath(c.Dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { continue; }
            if (full != "" && seen.Add(full)) yield return (full, c.How);
        }
    }

    private static IEnumerable<(string Dir, string How)> Raw(string baseDir)
    {
        // beside the port, or beside any folder above it; and the port's own folder, for a copy
        // put into the Mordor folder itself
        for (var d = SafeDir(baseDir); d != null; d = d.Parent)
        {
            yield return (Path.Combine(d.FullName, "game_files"), "beside the port");
            yield return (d.FullName, "the port's folder");
        }
        // what the original reads: [File Information] BasePath in MORDOR.INI in the Windows folder
        string win = Environment.GetEnvironmentVariable("WINDIR") ?? "";
        if (win != "")
        {
            string basePath = Ini.Read(Path.Combine(win, "MORDOR.INI")).Text("File Information", "BasePath");
            if (basePath != "") yield return (basePath, "MORDOR.INI's BasePath");
        }
        if (OperatingSystem.IsWindows())
            foreach (var dir in UninstallEntries()) yield return (dir, "its uninstall entry");
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is { Length: > 0 } local ? Path.Combine(local, "Programs") : "",
                 })
        {
            if (root == "") continue;
            foreach (string name in new[] { "Mordor", "Mordor v1.1", "Mordor v1.1 The Depths of Dejenol", "Mordor The Depths of Dejenol", "MakeItSo\\Mordor" })
                yield return (Path.Combine(root, name), "an install folder");
        }
        string sys = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        yield return (Path.Combine(sys, "MORDOR"), "an install folder");
        yield return (Path.Combine(sys, "GAMES", "MORDOR"), "an install folder");
    }

    private static DirectoryInfo? SafeDir(string path)
    {
        try { return new DirectoryInfo(path); } catch { return null; }
    }

    /// <summary>Installed programs called Mordor: their InstallLocation, else the folder of their icon or uninstaller.</summary>
    [SupportedOSPlatform("windows")]
    private static List<string> UninstallEntries()
    {
        var found = new List<string>();
        try
        {
            const string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            foreach (var (hive, sub) in new[]
                     {
                         (Microsoft.Win32.Registry.LocalMachine, key),
                         (Microsoft.Win32.Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                         (Microsoft.Win32.Registry.CurrentUser, key),
                     })
            {
                using var root = hive.OpenSubKey(sub);
                if (root == null) continue;
                foreach (string name in root.GetSubKeyNames())
                {
                    using var app = root.OpenSubKey(name);
                    if (app?.GetValue("DisplayName") is not string shown || !shown.Contains("Mordor", StringComparison.OrdinalIgnoreCase)) continue;
                    if (app.GetValue("InstallLocation") is string loc && loc.Trim() != "") found.Add(loc.Trim().Trim('"'));
                    foreach (string v in new[] { "DisplayIcon", "UninstallString" })
                        if (app.GetValue(v) is string s && ExeFolder(s) is string folder) found.Add(folder);
                }
            }
        }
        catch { }
        return found;
    }

    /// <summary>The folder of the program a registry command line names ("C:\x\y.exe",0 or "C:\x\y.exe" /U).</summary>
    private static string? ExeFolder(string command)
    {
        string s = command.Trim();
        if (s.StartsWith('"')) { int q = s.IndexOf('"', 1); if (q > 0) s = s[1..q]; }
        else
        {
            int exe = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) s = s[..(exe + 4)];
        }
        int comma = s.LastIndexOf(',');
        if (comma > 0 && !s[comma..].Contains('\\')) s = s[..comma];
        try { return Path.GetDirectoryName(s); } catch { return null; }
    }
}
