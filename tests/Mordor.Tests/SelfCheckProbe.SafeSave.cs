using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// A short hash of every field written to a save: each saved type's public properties (what
    /// System.Text.Json writes, less [JsonIgnore]), their types, and every Mordor.Core type and enum
    /// reached through them; plus the guild log's binary entry.
    /// </summary>
    private static string SaveLayoutFingerprint()
    {
        var core = typeof(Character).Assembly;
        Type Nested(Type outer, string name) => outer.GetNestedType(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
        var saved = new[]
        {
            typeof(Character), typeof(Confinement), typeof(DungeonState), typeof(Guildmasters), typeof(HallRecords),
            typeof(Librarian.Store), typeof(SharedMap), typeof(StoreStock), Nested(typeof(Party), "Roster"), typeof(GuildLog.Entry),
        };
        var seen = new HashSet<Type>();
        var lines = new List<string>();
        void Walk(Type t)
        {
            if (t.IsArray) { Walk(t.GetElementType()!); return; }
            if (t.IsGenericType) { foreach (var a in t.GetGenericArguments()) Walk(a); }
            if (t.Assembly != core || !seen.Add(t)) return;
            if (t.IsEnum)
            {
                lines.Add($"{t.FullName} = " + string.Join(",", Enum.GetValues(t).Cast<object>().Select(v => $"{v}:{Convert.ToInt64(v)}")));
                return;
            }
            foreach (var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (p.GetMethod == null || p.GetIndexParameters().Length > 0) continue;
                var ignore = p.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true)
                    .Cast<System.Text.Json.Serialization.JsonIgnoreAttribute>().FirstOrDefault();
                if (ignore is { Condition: System.Text.Json.Serialization.JsonIgnoreCondition.Always }) continue;
                lines.Add($"{t.FullName}.{p.Name}: {p.PropertyType}");
                Walk(p.PropertyType);
            }
        }
        foreach (var t in saved) Walk(t);
        lines.Sort(StringComparer.Ordinal);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return $"{lines.Count} fields, " + Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Saves written so a crash part way through keeps the old file (SafeFile), and a character file
    /// that will not read put back from its last good copy - or at least said - instead of vanishing
    /// from the lists while its name stays taken (SaveRecovery, Roster.Saved). Only scratch folders.
    /// </summary>
    private static void SafeSaveChecks(string dir)
    {
        Console.WriteLine("\nsafe saving and damaged character files (SafeFile, SaveRecovery)");
        string scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mordor-safesave-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var log = new List<string>();
        var oldSafeLog = SafeFile.Log;
        var oldRecoveryLog = SaveRecovery.Log;
        SafeFile.Log = log.Add;
        SaveRecovery.Log = log.Add;
        SaveRecovery.TakeNotices();
        try
        {
            // ---- SafeFile ----
            string f = Path.Combine(scratch, "plain", "a.json");
            SafeFile.WriteAllText(f, "first");
            SafeFile.WriteAllText(f, "second");
            Check("SafeFile: the file holds the last text, its folder made", File.ReadAllText(f), "second");
            Check("SafeFile: no .tmp left behind", File.Exists(f + SafeFile.TempSuffix), false);
            Check("SafeFile: UTF-8 with no byte-order mark, as File.WriteAllText", File.ReadAllBytes(f)[0], (byte)'s');
            // something else holding the file for a moment: the swap waits and goes through
            using (var held = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var release = Task.Run(async () => { await Task.Delay(60); held.Dispose(); });
                SafeFile.WriteAllText(f, "third");
                release.Wait();
            }
            Check("SafeFile: a file held for a moment is still written (retries)", File.ReadAllText(f), "third");
            // held for good: the save gives up, says so, and the old file and the .tmp are as they were
            using (new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool threw = false;
                try { SafeFile.WriteAllText(f, "fourth"); } catch { threw = true; }
                Check("SafeFile: a file held for good - the save throws", threw, true);
            }
            Check("SafeFile: ...the old file is untouched", File.ReadAllText(f), "third");
            Check("SafeFile: ...and crash.log is told", log.Any(l => l.Contains(f) && l.Contains("could not be saved")), true);
            Check("backups leave an unfinished save's .tmp behind", Backups.IsGameFile(f + SafeFile.TempSuffix), false);
            File.Delete(f + SafeFile.TempSuffix);

            // ---- a character that reads keeps a last good copy ----
            string saves = Directory.CreateDirectory(Path.Combine(scratch, "saves")).FullName;
            Character Make(string name) => new() { Name = name, Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } } };
            Make("Hob").Save(Path.Combine(saves, "Hob.json"));
            Make("Ann").Save(Path.Combine(saves, "Ann.json"));
            File.WriteAllText(Path.Combine(saves, "store.json"), "{ \"not\": \"a character\" ");   // a damaged world file
            Roster.Added(saves, "Hob", Array.Empty<string>());
            Check("both characters listed", string.Join(",", Roster.Saved(saves).Select(p => p.Ch.Name).OrderBy(n => n)), "Ann,Hob");
            string goodHob = SaveRecovery.GoodPathFor(Path.Combine(saves, "Hob.json"));
            Check("a character that reads gets a last good copy, in last-good inside the saves folder",
                  File.Exists(goodHob) && Path.GetDirectoryName(goodHob)!.EndsWith(SaveRecovery.GoodDir), true);
            Check("a damaged world file is not taken for a character: nothing said", SaveRecovery.TakeNotices().Count, 0);

            // ---- a save cut short by a crash: put back from the last good copy ----
            File.WriteAllText(Path.Combine(saves, "Hob.json"), "{ \"Name\": \"Hob\", \"Gol");
            log.Clear();
            var after = Roster.Saved(saves).Select(p => p.Ch.Name).OrderBy(n => n).ToList();
            Check("a half-written Hob is still listed - put back from his last good copy", string.Join(",", after), "Ann,Hob");
            Check("...his file reads again", Character.Load(Path.Combine(saves, "Hob.json")).Name, "Hob");
            Check("...the damaged file is kept in damaged/ for a bug report",
                  Directory.GetFiles(Path.Combine(saves, SaveRecovery.DamagedDir), "Hob-*.json").Length, 1);
            Check("...crash.log says why and what was done",
                  log.Any(l => l.Contains("could not be read")) && log.Any(l => l.Contains("put back")), true);
            var notices = SaveRecovery.TakeNotices();
            Check("...and the player is told once", notices.Count == 1 && notices[0].StartsWith("Hob's save file could not be read"), true);

            // ---- damaged with no copy: said, once, and left as it is ----
            File.WriteAllText(Path.Combine(saves, "Ann.json"), "");
            File.Delete(SaveRecovery.GoodPathFor(Path.Combine(saves, "Ann.json")));
            log.Clear();
            Roster.Saved(saves);
            Roster.Saved(saves);
            Check("an empty Ann.json with no copy: not listed", Roster.Saved(saves).Any(p => p.Ch.Name == "Ann"), false);
            Check("...left as it is (the name stays taken, as before)", File.Exists(Path.Combine(saves, "Ann.json")), true);
            Check("...crash.log told once, not at every list", log.Count(l => l.Contains("Ann.json") && l.Contains("could not be read")), 1);
            notices = SaveRecovery.TakeNotices();
            Check("...and the player is told once, with where to look",
                  notices.Count == 1 && notices[0].Contains("\"Ann.json\"") && notices[0].Contains("crash.log"), true);

            // ---- a saves folder that moves between Windows, Linux and a browser ----
            if (OperatingSystem.IsWindows())
                Check("the file-name rule is exactly Windows' own forbidden set, so no Windows save changes name",
                      Character.NotInFileNames.SetEquals(Path.GetInvalidFileNameChars()), true);
            Check("\"Who?\" is Who.json on every system", Character.SafeFileName("Who?") + ".json", "Who.json");
            Check("\"A:B*C\" is ABC.json on every system", Character.SafeFileName("A:B*C") + ".json", "ABC.json");
            string moved = Directory.CreateDirectory(Path.Combine(scratch, "moved")).FullName;
            Make("Newbie").Save(Path.Combine(moved, "oldname.json"));                 // a file under another name
            Make("Ann").Save(Path.Combine(moved, "Ann.json"));
            Make("Ann").Save(Path.Combine(moved, "Clash.json"));                      // would be Ann.json, which is taken
            File.WriteAllText(Path.Combine(moved, "party-x.json"), "{ \"Leader\": \"Newbie\", \"Members\": [\"Newbie\"] }");
            log.Clear();
            var names = Roster.Saved(moved).Select(p => Path.GetFileName(p.File)).OrderBy(n => n).ToList();
            Check("a character saved under another name is renamed to the one it is looked for by",
                  File.Exists(Path.Combine(moved, "Newbie.json")) && !File.Exists(Path.Combine(moved, "oldname.json")), true);
            Check("...and listed under it", names.Contains("Newbie.json"), true);
            Check("a party file follows its leader's name", File.Exists(Path.Combine(moved, "party-Newbie.json")), true);
            Check("a rename onto a name taken is left, and said",
                  File.Exists(Path.Combine(moved, "Clash.json")) && log.Any(l => l.Contains("Clash.json") && l.Contains("taken")), true);
            Check("names are taken in any case of their letters", Saves.NameTaken(moved, "nEWBIE") && Saves.NameTaken(moved, "ANN"), true);
            Check("...and a free one is free", Saves.NameTaken(moved, "Zed"), false);

            // ---- the save layout's version (SaveFormat) ----
            string fmt = Directory.CreateDirectory(Path.Combine(scratch, "format")).FullName;
            Check("a folder with no marker is not too new", SaveFormat.TooNew(fmt, out _), false);
            SaveFormat.Stamp(fmt, "0.0.test");
            Check("Stamp marks it with this layout and version", SaveFormat.Read(fmt), (SaveFormat.Current, "0.0.test"));
            File.WriteAllText(SaveFormat.PathFor(fmt), $"{{ \"Format\": {SaveFormat.Current + 1}, \"WrittenBy\": \"9.9.9\" }}");
            Check("a folder from a newer layout is too new, and says who wrote it",
                  SaveFormat.TooNew(fmt, out string by) && by == "9.9.9", true);
            SaveFormat.Stamp(fmt, "0.0.test");
            Check("...and Stamp leaves the newer marker alone", SaveFormat.Read(fmt), (SaveFormat.Current + 1, "9.9.9"));
            Make("Someone").Save(Path.Combine(fmt, "elsewhere.json"));
            Roster.Saved(fmt);
            Check("...and its files are never renamed", File.Exists(Path.Combine(fmt, "elsewhere.json")), true);
            Check("the marker is not taken for a damaged character", SaveRecovery.MayBeCharacter(SaveFormat.PathFor(fmt)), false);

            // ---- the layout's fingerprint: SaveFormat.Current goes up whenever a saved type changes ----
            // Every field of every type written to a save, nested types too. An older game reading a
            // newer save would drop the fields it does not know of the next time it saved, so a change
            // here must come with SaveFormat.Current raised and the fingerprint for the new number added.
            var layouts = new Dictionary<int, string>
            {
                [1] = "221 fields, 2A75F30B4581C0C0",   // 0.1.7
            };
            string layout = SaveLayoutFingerprint();
            Check($"the save layout is the one SaveFormat.Current ({SaveFormat.Current}) stands for - if a saved type changed, "
                  + "raise SaveFormat.Current and add this fingerprint for it",
                  layout, layouts.GetValueOrDefault(SaveFormat.Current, "none for this number"));

            // ---- settings.ini, the guild log and Backup/Restore go through SafeFile too ----
            string ini = Path.Combine(scratch, "ini", "settings.ini");
            var lines = Ini.Read(ini);
            lines.Set("Loading Information", "NotFirstTime", "True");
            lines.Set("Fonts", "Pro", "Courier New");
            Check("settings.ini: written", lines.Write(ini), true);
            File.WriteAllLines(ini + ".plain", File.ReadAllLines(ini));
            Check("settings.ini: byte for byte what File.WriteAllLines wrote", File.ReadAllBytes(ini).SequenceEqual(File.ReadAllBytes(ini + ".plain")), true);
            Check("settings.ini: reads back", Ini.Read(ini).Text("Fonts", "Pro"), "Courier New");
            string logDir = Directory.CreateDirectory(Path.Combine(scratch, "guildlog")).FullName;
            GuildLog.Rewrite(logDir, new List<GuildLog.Entry> { new(2, 7, "Hob joined") });
            Check("the guild log: written whole and read back", string.Join("|", GuildLog.All(logDir).Select(e => $"{e.Guild},{e.Day},{e.Text}")), "2,7,Hob joined");
            Check("the guild log: no .tmp left", File.Exists(GuildLog.PathFor(logDir) + SafeFile.TempSuffix), false);
            string backup = Directory.CreateDirectory(Path.Combine(scratch, "backup")).FullName;
            Backups.Backup(saves, backup);
            Check("Backup: the characters copied whole", Character.Load(Path.Combine(backup, "Hob.json")).Name, "Hob");
            Check("Backup: nothing half-copied left behind", Directory.GetFiles(backup, "*" + SafeFile.TempSuffix).Length, 0);

            // ---- nothing in Mordor.Core writes a file in place any more ----
            // Every write, copy or create must go through SafeFile, but for these, each named with why:
            var allowed = new (string File, string Text, string Why)[]
            {
                ("SaveImport.cs", "File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), overwrite: false)", "copies into a new before-import folder, never over a save"),
                ("GuildLog.cs", "File.AppendAllText(Path.Combine(savesDir, TextName)", "GUILDLOG.TXT, a printout appended to"),
                ("PortFiles.cs", "File.AppendAllText(p, text)", "crash.log and CHARSTAT.TXT, appended to"),
                ("GuildLog.cs", "new BinaryWriter(buffer,", "builds the log in memory for SafeFile.WriteAllBytes"),
                ("SaveJournal.cs", "new FileStream(path, FileMode.Append,", "saves-journal.log, appended to a line at a time"),
                ("SaveJournal.cs", "File.WriteAllBytes(temp, all[from..])", "saves-journal.log's newest half, to a .tmp moved over it; not a save, and never journalled itself"),
            };
            string core = Path.Combine(PortTree.Root, "src", "Mordor.Core");
            var writes = new System.Text.RegularExpressions.Regex(@"(?<![\w.])File\.(WriteAll\w*|AppendAll\w*|Copy|Create|Open\w*)\(|new (BinaryWriter|StreamWriter|FileStream)\(");
            var unsafeWrites = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains("Extraction") && !p.EndsWith("InstallerUnpack.cs") && !p.EndsWith("SafeFile.cs"))
                .SelectMany(p => File.ReadAllLines(p).Select((line, i) => (File: Path.GetFileName(p), Line: i + 1, Text: line.Trim())))
                .Where(l => !l.Text.StartsWith("//") && writes.IsMatch(l.Text))
                // reading is not writing: a read-only open, File.OpenRead
                .Where(l => !System.Text.RegularExpressions.Regex.IsMatch(l.Text, @"FileAccess\.Read\b") && !l.Text.Contains("File.OpenRead(") && !l.Text.Contains("File.OpenText("))
                .Where(l => !allowed.Any(a => a.File == l.File && l.Text.Contains(a.Text)))
                .Select(l => $"{l.File}:{l.Line}").ToList();
            Check("no file in Mordor.Core is written, copied or created in place but through SafeFile (or one of "
                  + allowed.Length + " named appends and copies)", string.Join(", ", unsafeWrites), "");
        }
        finally
        {
            SafeFile.Log = oldSafeLog;
            SaveRecovery.Log = oldRecoveryLog;
            try { Directory.Delete(scratch, true); } catch { }
        }
    }
}
