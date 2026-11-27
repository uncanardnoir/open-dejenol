using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The save journal (SaveJournal, docs/GAME_FILES.md): saves-journal.log in the saves folder, a
    /// line for each load, unload and write, so that a report of lost progress comes with evidence.
    /// Only a scratch folder; the journal is let go after, so no other check writes one.
    /// </summary>
    private static void SaveJournalChecks(string dir)
    {
        Console.WriteLine("\nthe save journal (SaveJournal)");
        string scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mordor-journal-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        string saves = Directory.CreateDirectory(Path.Combine(scratch, "saves")).FullName;
        string journal = Path.Combine(saves, SaveJournal.FileName);
        long capWas = SaveJournal.Cap;
        var oldSafeLog = SafeFile.Log;
        SafeFile.Log = _ => { };
        Character Make(string name) => new()
        {
            Name = name, Stats = new[] { 10, 11, 12, 13, 14, 15 }, GoldOnHandWhole = 100,
            Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } },
        };
        List<string> Lines() => File.ReadAllLines(journal).ToList();
        string Last(string evt) => Lines().LastOrDefault(l => l.Contains("] " + evt + " ")) ?? "";
        string Sha(string file) => SaveJournal.Short(File.ReadAllBytes(file));
        try
        {
            SaveJournal.Open(saves, "MORDOR_SAVES", "test");
            Check("journal: opening writes the OPEN line, with the kind of saves folder",
                  Last("OPEN").Contains("saves folder: MORDOR_SAVES") && Last("OPEN").Contains("no earlier run in the journal"), true);

            // ---- a load, a change, a save, an unload, a load ----
            string tess = Path.Combine(saves, "Tess.json");
            using (SaveJournal.Because("Create a Character")) Make("Tess").Save(tess);
            string made = Last("WRITE");
            Check("journal: a character's save is a WRITE line with its name, size and short hash",
                  made.Contains("WRITE \"Tess.json\" " + new FileInfo(tess).Length + " bytes sha " + Sha(tess)), true);
            Check("journal: ...the reason, and the call that saved",
                  made.Contains("; Create a Character; via Character.Save from SelfCheckProbe.SaveJournalChecks"), true);
            Check("journal: ...and what was saved", made.Contains("Tess.json") && made.Contains("Nomad 1, xp 0, gold 100 + 0 in bank, Str 10 Int 11 Wis 12 Con 13 Cha 14 Dex 15, town"), true);
            var ch = Character.Load(tess);
            SaveJournal.Loaded(ch, "Load Character", tess);
            string load1 = Last("LOAD");
            Check("journal: a load names the file, its hash and \"same as written\"",
                  load1.Contains("LOAD \"Tess\" how: Load Character; file \"Tess.json\"") && load1.Contains("sha " + Sha(tess))
                  && load1.Contains("same as written "), true);
            ch.Stats[0]++;   // a Tome read
            ch.Save(tess);
            Check("journal: the change's save has the new hash and the raised stat",
                  Last("WRITE").Contains("sha " + Sha(tess)) && Last("WRITE").Contains("Str 11 Int"), true);
            SaveJournal.Unloaded(ch, "Unload Character", saved: true);
            Check("journal: the unload, and that it was saved",
                  Last("UNLOAD").EndsWith("] UNLOAD \"Tess\" how: Unload Character; saved as it happened"), true);
            SaveJournal.Loaded(Character.Load(tess), "Load Character", tess);
            string writeTime = Last("WRITE").Split(' ')[0];
            Check("journal: loaded again, \"same as written\" at that save's time, the stat as saved",
                  Last("LOAD").Contains("same as written " + writeTime) && Last("LOAD").Contains("Str 11 Int"), true);

            // ---- a second copy: the file rewritten by something else ----
            var other = Character.Load(tess);
            other.GoldOnHandWhole = 5;
            File.WriteAllText(tess, System.Text.Json.JsonSerializer.Serialize(other));   // not through the game's save
            SaveJournal.Loaded(Character.Load(tess), "Load Character", tess);
            Check("journal: a file something else rewrote: CHANGED since this program wrote it",
                  Last("LOAD").Contains("CHANGED since this program wrote it " + writeTime), true);

            // ---- a stale file: an earlier version put back ----
            var stale = Character.Load(tess);
            stale.Save(tess);
            byte[] earlier = File.ReadAllBytes(tess);
            string earlierTime = Last("WRITE").Split(' ')[0];
            stale.Stats[1]++;
            stale.Save(tess);
            string laterTime = Last("WRITE").Split(' ')[0];
            File.WriteAllBytes(tess, earlier);
            SaveJournal.Loaded(Character.Load(tess), "Load Character", tess);
            Check("journal: an earlier version put back: OLDER than the last write, and which write it is",
                  Last("LOAD").Contains($"OLDER than the last write {laterTime}: it is the file written {earlierTime}"), true);

            // ---- names that differ only in case ----
            Make("Pam").Save(Path.Combine(saves, "pam.json"));
            SaveJournal.Loaded(Make("Pam"), "Load Character");
            Check("journal: the file as it is named on disk, whatever case the name asked for",
                  Last("LOAD").Contains("file \"pam.json\"") && !Last("LOAD").Contains("ALSO"), true);
            // a case-sensitive file system's listing, where Tess.json and tess.json are two files
            SaveJournal.ListFolder = _ => new[] { "Tess.json", "tess.json", "store.json" };
            SaveJournal.Loaded(Character.Load(tess), "Load Character", tess);
            Check("journal: another file of the same name but for case is said",
                  Last("LOAD").Contains("file \"Tess.json\"") && Last("LOAD").Contains("ALSO \"tess.json\" in the folder, the same name but for case"), true);
            SaveJournal.ListFolder = null;

            // ---- deletes, failures and world files ----
            Roster.Added(saves, "Tess", Array.Empty<string>());
            Check("journal: a world file's write (roster.json) is a WRITE line with no character",
                  Last("WRITE").Contains("WRITE \"roster.json\"") && !Last("WRITE").Contains("Str "), true);
            string held = Path.Combine(saves, "Held.json");
            Make("Held").Save(held);
            using (new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None))
                try { Make("Held").Save(held); } catch { }
            Check("journal: a save that could not be written is said, with its error", Last("WRITE-FAILED").Contains("\"Held.json\"")
                  && Last("WRITE-FAILED").Contains("Exception"), true);
            Check("journal: ...its error names the file, not the folder's path", Last("WRITE-FAILED").Contains(saves), false);

            // ---- the run with no closing line, and one with ----
            SaveJournal.Detach();
            SaveJournal.Open(saves, "MORDOR_SAVES", "test");
            Check("journal: a run that ended with no closing line is said at the next opening",
                  Last("OPEN").Contains("has NO CLOSING LINE: it crashed, was killed, or is still running"), true);
            SaveJournal.Closed("Exit at the main menu");
            SaveJournal.Closed("the program shut down");
            Check("journal: the closing line, said once", Lines().Count(l => l.Contains("] CLOSE ")), 1);
            Check("journal: ...and it is the last line", Lines()[^1].Contains("] CLOSE Exit at the main menu"), true);
            SaveJournal.Detach();
            SaveJournal.Open(saves, "MORDOR_SAVES", "test");
            Check("journal: a run that closed is said to have closed", Last("OPEN").Contains("the previous run [" + Environment.ProcessId + "] closed"), true);
            SaveJournal.Loaded(Character.Load(tess), "Preload Character");
            Check("journal: the writes are read back from the journal at opening: OLDER still",
                  Last("LOAD").Contains($"OLDER than the last write {laterTime}"), true);

            // ---- the cap ----
            SaveJournal.Cap = 20_000;
            var pad = Make("Pad");
            for (int i = 0; i < 120; i++) { pad.GoldOnHandWhole = i; pad.Save(Path.Combine(saves, "Pad.json")); }
            var capped = Lines();
            long size = new FileInfo(journal).Length;
            Check("journal: over the cap, the newest half is kept", size <= 20_000 && size >= 9_000, true);
            Check("journal: ...from a whole line", capped[0].StartsWith("20") && capped[0].Contains(" [" + Environment.ProcessId + "] "), true);
            Check("journal: ...the newest line kept", capped[^1].Contains("gold 119 + 0"), true);
            SaveJournal.Cap = capWas;

            // ---- no home path in any line ----
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Check("journal: no line holds the home folder or the saves folder's path",
                  Lines().Any(l => l.Contains(home, StringComparison.OrdinalIgnoreCase) || l.Contains(saves, StringComparison.OrdinalIgnoreCase)), false);

            // ---- the journal's own file failing: the saves as ever ----
            string plain = Directory.CreateDirectory(Path.Combine(scratch, "plain")).FullName;
            SaveJournal.Detach();
            var same = Make("Same");
            same.Save(Path.Combine(plain, "Same.json"));
            byte[] without = File.ReadAllBytes(Path.Combine(plain, "Same.json"));
            File.Delete(journal);
            Directory.CreateDirectory(journal);   // a folder where the journal would be
            SaveJournal.Open(saves, "MORDOR_SAVES", "test");
            bool threw = false;
            try
            {
                same.Save(Path.Combine(saves, "Same.json"));
                SaveJournal.Loaded(same, "Load Character");
                SaveJournal.Unloaded(same, "Unload Character", null);
                SaveJournal.Closed("test");
            }
            catch { threw = true; }
            Check("journal: a folder in the journal's place - nothing throws", threw, false);
            Check("journal: ...and the save is written, byte for byte as with no journal",
                  File.ReadAllBytes(Path.Combine(saves, "Same.json")).SequenceEqual(without), true);
            SaveJournal.Detach();
            Directory.Delete(journal);
            File.WriteAllText(journal, "");
            File.SetAttributes(journal, FileAttributes.ReadOnly);
            SaveJournal.Open(saves, "MORDOR_SAVES", "test");
            threw = false;
            try { same.Save(Path.Combine(saves, "Same.json")); SaveJournal.Loaded(same, "Load Character"); }
            catch { threw = true; }
            Check("journal: a read-only journal - nothing throws, the save is written",
                  !threw && File.ReadAllBytes(Path.Combine(saves, "Same.json")).SequenceEqual(without), true);
            File.SetAttributes(journal, FileAttributes.Normal);
            SaveJournal.Detach();

            // ---- not open: nothing written at all ----
            string quiet = Directory.CreateDirectory(Path.Combine(scratch, "quiet")).FullName;
            Make("Quiet").Save(Path.Combine(quiet, "Quiet.json"));
            Check("journal: with no Open (the tools, the other checks), no journal is written",
                  File.Exists(Path.Combine(quiet, SaveJournal.FileName)), false);
        }
        finally
        {
            SaveJournal.Detach();
            SaveJournal.ListFolder = null;
            SaveJournal.Cap = capWas;
            SafeFile.Log = oldSafeLog;
            try { if (File.Exists(journal)) File.SetAttributes(journal, FileAttributes.Normal); } catch { }
            try { Directory.Delete(scratch, true); } catch { }
        }
    }
}
