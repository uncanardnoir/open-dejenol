namespace Mordor.Core;

/// <summary>
/// The Options window's Backup and Restore, <c>fn0662</c> (MAINMOD.BAS:954-1046), without its
/// boxes - the window asks the questions and this does the copying.
///
/// The original copies ten fixed files between <c>BasePath\DATA\</c> and the chosen directory:
/// MDATA4, 6, 7, 8, 9, 10, 12, 13, 14 and 15 (MAINMOD.BAS:996-1005) - the game's own tables,
/// MDATA1, 2, 3, 5 and 11, which ship with it and the port reads from the game directory, are left
/// out, and so is MDATA9B, the guild log's spare copy. The port keeps the files the game changes in
/// its saves directory (<see cref="Saves.DirFor"/>), one per character and one per shared table,
/// so the set is every file at the top of that directory but two: the guild log's spare,
/// <see cref="GuildLog.BackupName"/>, and <see cref="PortSettings"/>, which stands in for
/// MORDOR.INI - not in the original's list either.
///
/// "Backup files found" is <c>fn034F(dir + MDATA4.MDR)</c>, the characters file
/// (FRMDIREC.BAS:63 and MAINMOD.BAS:990, 1008). The port's characters are separate .json files,
/// so a directory counts as holding a backup when it holds at least one character file - not
/// merely any .json, which the automap, the dungeon or the library would also satisfy.
///
/// The characters, being one file in the original, are copied whole: the roster at the far end
/// becomes exactly the roster copied. So a backup removes a character file from the backup
/// directory that the saves directory no longer has, and a restore removes a character file from
/// the saves directory that the backup does not have. The party files, MDATA12 in the original,
/// are matched the same way. Nothing else is removed, and only after every copy has gone through,
/// so a failed copy never costs a file.
///
/// Every other file is copied if it is there and passed over if it is not (<c>Resume Next</c>
/// while l01F6 is set, MAINMOD.BAS:1040-1041): a shared table the backup predates is left as it is.
/// </summary>
public static class Backups
{
    /// <summary>Whether a file at the top of a directory is one of the game files.</summary>
    public static bool IsGameFile(string path)
    {
        string name = Path.GetFileName(path);
        return !string.Equals(name, GuildLog.BackupName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, PortSettings.FileName, StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> GameFilesIn(string dir)
    {
        try { return Directory.GetFiles(dir).Where(IsGameFile).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList(); }
        catch { return new List<string>(); }
    }

    /// <summary>
    /// A saved character, the port's share of MDATA4: a .json that is not a party file and reads as
    /// a character with a name. This is a test of the file, not of the record: a character whose
    /// race or guild is in no MDATA1 table (which <see cref="Character.Load"/> refuses) is still a
    /// character file, so it is copied and matched like any other - the original copies MDATA4
    /// whole (MAINMOD.BAS:996).
    /// </summary>
    public static bool IsCharacterFile(string path) => IsCharacterFile(path, readable: false);

    private static bool IsCharacterFile(string path, bool readable)
    {
        string name = Path.GetFileName(path);
        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.StartsWith("party-", StringComparison.OrdinalIgnoreCase)) return false;
        try { return !string.IsNullOrWhiteSpace((readable ? Character.Load(path) : Character.ReadRaw(path)).Name); }
        catch { return false; }
    }

    public static List<string> CharacterFilesIn(string dir) => GameFilesIn(dir).Where(IsCharacterFile).ToList();

    /// <summary>sub34AE's test (FRMDIREC.BAS:61-69): is there a backup in this directory?</summary>
    public static bool Found(string dir)
    {
        // a backup counts when it holds a character the menu's list would show (Roster.Saved),
        // so this one asks for a readable record
        try { return Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Any(f => IsCharacterFile(f, readable: true)); }
        catch { return false; }
    }

    /// <summary>fn0679 summed over the set (MAINMOD.BAS:975-984): the bytes a backup needs.</summary>
    public static long Size(string savesDir) =>
        GameFilesIn(savesDir).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });

    /// <summary>Free bytes on the drive holding <paramref name="dir"/>, or -1 when it cannot be read.</summary>
    public static long FreeSpace(string dir)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dir))!).AvailableFreeSpace; }
        catch { return -1; }
    }

    /// <summary>The same directory with and without a trailing separator, case ignored as DOS does.</summary>
    public static bool SameDir(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'),
                      StringComparison.OrdinalIgnoreCase);

    /// <summary>The backup: every game file of the saves directory into <paramref name="toDir"/>.</summary>
    public static void Backup(string savesDir, string toDir) => Copy(savesDir, toDir);

    /// <summary>The restore: the backup's game files over the saves directory's.</summary>
    public static void Restore(string fromDir, string savesDir) => Copy(fromDir, savesDir);

    /// <summary>
    /// Copy every game file across, then - only if every copy went through - make the far end's
    /// character and party files the near end's exactly, by removing those it has that the near
    /// end does not. Nothing but a character or a party file is ever removed.
    /// </summary>
    private static void Copy(string fromDir, string toDir)
    {
        var sources = GameFilesIn(fromDir);
        var characters = new HashSet<string>(sources.Where(IsCharacterFile).Select(f => Path.GetFileName(f)),
                                             StringComparer.OrdinalIgnoreCase);
        bool allCopied = true;
        foreach (string f in sources)
        {
            try { File.Copy(f, Path.Combine(toDir, Path.GetFileName(f)), overwrite: true); }
            catch { allCopied = false; /* Resume Next */ }
        }
        if (!allCopied) return;
        // a source with no character at all - an unreadable folder, or an empty one - never clears
        // the far end's characters: there is nothing to make them match
        if (characters.Count == 0) return;
        foreach (string f in CharacterFilesIn(toDir))
        {
            if (characters.Contains(Path.GetFileName(f))) continue;
            try { File.Delete(f); } catch { }
        }
        // The parties are one file in the original too, MDATA12 (gc1C38, MAIN.TXT:590), which
        // mdrmulLoadParty reads (MULTIMOD.BAS:218) and fn0662 copies whole (MAINMOD.BAS:1002, 1021).
        // The far end's parties become the near end's exactly: a party file the source does not
        // have - a party of a character the restore has just taken away - goes with it.
        var parties = new HashSet<string>(sources.Where(IsPartyFile).Select(f => Path.GetFileName(f)),
                                          StringComparer.OrdinalIgnoreCase);
        foreach (string f in GameFilesIn(toDir).Where(IsPartyFile))
        {
            if (parties.Contains(Path.GetFileName(f))) continue;
            try { File.Delete(f); } catch { }
        }
    }

    /// <summary>A party roster, the port's share of MDATA12: <c>party-&lt;leader&gt;.json</c>.</summary>
    public static bool IsPartyFile(string path)
    {
        string name = Path.GetFileName(path);
        return name.StartsWith("party-", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
}
