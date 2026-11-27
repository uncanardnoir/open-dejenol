using System.Text.Json;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The dungeon's rooms on disk (DungeonState): a file per level beside a small dungeon.json, each
    /// written only when it changed; the old single dungeon.json split once, a split cut short
    /// finished at the next start; the new files carried by Backup and Restore and never taken for a
    /// damaged character. Only scratch folders. MORDOR_OLD_DUNGEON names a real old dungeon.json to
    /// split as well (a copy of it; the file itself is only read).
    /// </summary>
    private static void DungeonFilesChecks(string dir)
    {
        Console.WriteLine("\nthe dungeon's files: one per level (DungeonState)");
        string scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mordor-dungeonfiles-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var oldSafeLog = SafeFile.Log;
        var log = new List<string>();
        SafeFile.Log = log.Add;
        var written = new List<string>();
        try
        {
            string Folder(string name) => Directory.CreateDirectory(Path.Combine(scratch, name)).FullName;
            string Names(string folder) => string.Join(" ", Directory.GetFiles(folder).Select(f => Path.GetFileName(f))
                                                                .Where(n => n.StartsWith("dungeon")).OrderBy(n => n, StringComparer.Ordinal));
            // a record in full, every field, to compare rooms by what they hold and not by DungeonState.Same
            string Full(RoomRecord r) => JsonSerializer.Serialize(r);
            string AllRooms(DungeonState s) => string.Join("\n", s.Rooms.Where(kv => !DungeonState.Same(kv.Value, new RoomRecord()))
                                                                         .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                                                         .Select(kv => kv.Key + "=" + Full(kv.Value)));
            string Tallies(DungeonState s) => string.Join(" ", s.Tally.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
            RoomRecord Room(int seed)
            {
                var r = new RoomRecord { TreasureClass = seed % 4, Lead = seed * 3, Gold = 100.25m * seed, ChestTrap = seed % 2 * 8, ChestLock = seed % 2 };
                r.Foes[0].Ref = seed; r.Foes[0].KnownAs = seed + 1; r.Foes[0].GroupCount = 1 + seed % 5; r.Foes[0].Att = 3; r.Foes[0].Def = 4;
                r.Foes[0].Hits = 7 + seed; r.Foes[0].MaxHits = 9 + seed; r.Foes[0].Alignment = 2; r.Foes[0].IdLevel = 1;
                r.Foes[0].Engaged = 1; r.Foes[0].JoinOffer = seed % 3; r.Foes[0].Stamp = 3600.5 + seed;
                if (seed % 3 == 0) { r.Foes[2].Ref = seed + 2; r.Foes[2].GroupCount = 2; r.Foes[2].Hits = 5; }   // a gap before a third group
                return r;
            }
            RoomRecord Emptied(double at) { var r = new RoomRecord(); r.Foes[0].Ref = -1; r.Foes[0].Stamp = at; return r; }

            // ---- a round trip: rooms on several levels, a level with none, the tallies, the deepest level ----
            string trip = Folder("trip");
            var made = new DungeonState { DeepestReached = 12 };
            for (int a = 1; a <= 30; a++) made.StoreRoom(1, a, Room(a));
            made.StoreRoom(5, 7, Room(40));
            made.StoreRoom(5, 8, Emptied(41000.25));
            made.StoreRoom(5, 9, new RoomRecord());                     // a room stored as the zeroed records
            made.StoreRoom(12, 199, Room(50));
            made.MarkCleared(12, 3, new DateTime(2026, 11, 27, 9, 30, 15), 60);
            made.Tally[5] = 0;                                         // every room emptied: a tally of 0 is not "none"
            made.NoteRefilled(7, 40);                                  // a level with a tally and no rooms
            made.Save(trip);
            Check("round trip: a file for each level with anything in it, and dungeon.json", Names(trip),
                  "dungeon-01.json dungeon-05.json dungeon-07.json dungeon-12.json dungeon.json");
            var back = DungeonState.Load(trip);
            Check("round trip: every room's records back as stored", AllRooms(back) == AllRooms(made) && AllRooms(made).Length > 0, true);
            Check("round trip: the zeroed room reads as the zeroed records", Full(back.LoadRoom(5, 9)) == Full(new RoomRecord()), true);
            Check("round trip: the tallies, a 0 among them", Tallies(back), Tallies(made));
            Check("round trip: a level with no tally keeps its seed value", back.TallyOf(2, 50), 50);
            Check("round trip: a level never written has no rooms", back.Rooms.Keys.Any(k => k.StartsWith("2:") || k.StartsWith("3:")), false);
            Check("round trip: the deepest level", back.DeepestReached, 12);
            Check("round trip: no .tmp left", Directory.GetFiles(trip, "*" + SafeFile.TempSuffix).Length, 0);
            string one = File.ReadAllText(DungeonState.LevelPath(trip, 1));
            Check("written compact: no indentation, no zero fields", !one.Contains('\n') && !one.Contains("  ") && !one.Contains(":0,") && !one.Contains(":0}"), true);
            Check("dungeon.json holds the layout number and the deepest level only", File.ReadAllText(Path.Combine(trip, "dungeon.json")),
                  "{\"Format\":2,\"DeepestReached\":12}");
            Check("a level file: dungeon-05.json, the tally, the rooms by area, the emptied one short",
                  File.ReadAllText(DungeonState.LevelPath(trip, 5)).Contains("\"8\":{\"Foes\":[{\"Ref\":-1,\"Stamp\":41000.25}]}"), true);

            // ---- a missing field, record, room or level reads as zero ----
            string sparse = Folder("sparse");
            File.WriteAllText(DungeonState.LevelPath(sparse, 3), "{\"Rooms\":{\"4\":{\"Foes\":[{\"Ref\":-1,\"Stamp\":3600}]},\"9\":{},\"11\":{\"Gold\":12.5}}}");
            File.WriteAllText(DungeonState.LevelPath(sparse, 6), "{}");
            var thin = DungeonState.Load(sparse);
            Check("a missing field reads as zero: the four records there, the missing ones zeroed",
                  Full(thin.LoadRoom(3, 4)) == Full(Emptied(3600)), true);
            Check("a room of no fields reads as the zeroed records", Full(thin.LoadRoom(3, 9)) == Full(new RoomRecord()), true);
            Check("a room with only its gold", Full(thin.LoadRoom(3, 11)) == Full(new RoomRecord { Gold = 12.5m }), true);
            Check("a room not in the file, and a level with an empty file, read as zeroed", Full(thin.LoadRoom(3, 5)) == Full(new RoomRecord())
                  && Full(thin.LoadRoom(6, 1)) == Full(new RoomRecord()) && thin.TallyOf(6, 44) == 44, true);
            Check("no dungeon.json: the deepest level 0", thin.DeepestReached, 0);
            Check("...and read as a room always was: occupied by the never-set stamp's rule",
                  thin.IsOccupied(3, 9, 50, new DateTime(2026, 11, 27, 12, 0, 0)), new DungeonState().IsOccupied(3, 9, 50, new DateTime(2026, 11, 27, 12, 0, 0)));

            // ---- dirty tracking: a save writes only what changed ----
            DungeonState.BeforeWrite = p => written.Add(Path.GetFileName(p));
            string dirty = Folder("dirty");
            foreach (string f in Directory.GetFiles(trip)) File.Copy(f, Path.Combine(dirty, Path.GetFileName(f)));
            var st = DungeonState.Load(dirty);
            written.Clear();
            st.Save(dirty);
            Check("dirty: a save with nothing changed writes nothing", string.Join(" ", written), "");
            st.StoreRoom(1, 4, st.LoadRoom(1, 4));
            st.StoreRoom(9, 2, new RoomRecord());
            st.Save(dirty);
            Check("dirty: a room stored as it was (or zeroed where nothing was) writes nothing", string.Join(" ", written), "");
            var changed = st.LoadRoom(5, 7); changed.Foes[0].Hits--;
            st.StoreRoom(5, 7, changed);
            st.Save(dirty);
            Check("dirty: a room changed on level 5 writes dungeon-05.json and nothing else", string.Join(" ", written), "dungeon-05.json");
            written.Clear();
            st.Save(dirty);
            Check("dirty: ...and the save after it, nothing", string.Join(" ", written), "");
            st.MarkCleared(1, 6, new DateTime(2026, 11, 27, 10, 0, 0), 60);
            st.Save(dirty);
            Check("dirty: a won fight on level 1 (the room and the tally) writes dungeon-01.json", string.Join(" ", written), "dungeon-01.json");
            written.Clear();
            st.NoteRefilled(2, 50);
            st.Save(dirty);
            Check("dirty: a tally alone (a refill of level 2) writes that level's file", string.Join(" ", written), "dungeon-02.json");
            written.Clear();
            st.NoteDepth(13);
            st.Save(dirty);
            Check("dirty: a new deepest level writes dungeon.json alone", string.Join(" ", written), "dungeon.json");
            written.Clear();
            DungeonState.BeforeWrite = p => { written.Add(Path.GetFileName(p)); if (p.EndsWith("dungeon-05.json")) throw new IOException("the disk is full"); };
            var hurt = st.LoadRoom(5, 7); hurt.Foes[0].Hits--;
            st.StoreRoom(5, 7, hurt);
            st.Save(dirty);
            DungeonState.BeforeWrite = p => written.Add(Path.GetFileName(p));
            written.Clear();
            st.Save(dirty);
            Check("dirty: a level that could not be written is written at the next save", string.Join(" ", written), "dungeon-05.json");
            Check("dirty: ...and holds what was stored", Full(DungeonState.Load(dirty).LoadRoom(5, 7)) == Full(hurt), true);
            {
                // every field of a room counts: a change to any one of them is a change to write
                var missed = new List<string>();
                foreach (var p in typeof(FoeSlot).GetProperties().Where(p => p.CanWrite))
                    for (int slot = 0; slot < 4; slot++)
                    {
                        var r = new RoomRecord();
                        p.SetValue(r.Foes[slot], Convert.ChangeType(1, p.PropertyType));
                        if (DungeonState.Same(r, new RoomRecord())) missed.Add($"Foes[{slot}].{p.Name}");
                    }
                foreach (var p in typeof(RoomRecord).GetProperties().Where(p => p.CanWrite && p.Name != nameof(RoomRecord.Foes)))
                {
                    var r = new RoomRecord();
                    p.SetValue(r, Convert.ChangeType(1, p.PropertyType));
                    if (DungeonState.Same(r, new RoomRecord())) missed.Add(p.Name);
                }
                Check("dirty: a change to any field of any record is seen (DungeonState.Same)", string.Join(" ", missed), "");
            }
            {
                // a state made whole (an import) is written whole: a level it has nothing for is emptied
                string whole = Folder("whole");
                foreach (string f in Directory.GetFiles(trip)) File.Copy(f, Path.Combine(whole, Path.GetFileName(f)));
                var fresh = new DungeonState { DeepestReached = 2 };
                fresh.StoreRoom(2, 1, Room(3));
                fresh.Save(whole);
                var w = DungeonState.Load(whole);
                Check("a new state saved over a world replaces it whole, as the single file did",
                      $"{w.Rooms.Count} {w.Tally.Count} {w.DeepestReached} {File.ReadAllText(DungeonState.LevelPath(whole, 1))}", "1 0 2 {}");
            }
            DungeonState.BeforeWrite = null;

            // ---- the old single dungeon.json, split ----
            // as 0.1.8 wrote it: the whole state, indented, rooms keyed "level:area"
            var legacy = new DungeonState { DeepestReached = 9 };
            for (int a = 1; a <= 40; a++) legacy.StoreRoom(1 + a % 4, a, a % 5 == 0 ? Emptied(1000 + a) : Room(a));
            legacy.StoreRoom(9, 17, new RoomRecord());
            legacy.Tally[2] = 31; legacy.Tally[3] = 0;
            string legacyText = JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true });
            // a save from before the rooms were stored: an emptied room as a stamp alone, one a UTC tick count
            string withStamps = legacyText.TrimEnd().TrimEnd('}') + ",\"Cleared\":{\"6:4\":7200.5,\"6:5\":638000000000000000},\"Spawned\":{\"6:6\":3.5}}";
            var expect = JsonSerializer.Deserialize<DungeonState>(legacyText)!;
            expect.StoreRoom(6, 4, Emptied(7200.5));

            string split = Folder("split");
            File.WriteAllText(Path.Combine(split, "dungeon.json"), withStamps);
            byte[] oldBytes = File.ReadAllBytes(Path.Combine(split, "dungeon.json"));
            var migrated = DungeonState.Load(split);
            Check("split: a file per level, dungeon.json anew, the old one kept as dungeon.json.old", Names(split),
                  "dungeon-01.json dungeon-02.json dungeon-03.json dungeon-04.json dungeon-06.json dungeon-09.json dungeon.json dungeon.json.old");
            Check("split: dungeon.json.old is the old file byte for byte", File.ReadAllBytes(Path.Combine(split, "dungeon.json.old")).SequenceEqual(oldBytes), true);
            Check("split: the new dungeon.json", File.ReadAllText(Path.Combine(split, "dungeon.json")), "{\"Format\":2,\"DeepestReached\":9}");
            var reread = DungeonState.Load(split);
            Check("split: the same room records, read back from the new files", AllRooms(reread) == AllRooms(expect) && AllRooms(migrated) == AllRooms(expect), true);
            Check("split: the old save's stamp a room emptied; a tick count dropped, as before", $"{Full(reread.LoadRoom(6, 4)) == Full(Emptied(7200.5))} {reread.Rooms.ContainsKey("6:5")}", "True False");
            Check("split: the tallies, a 0 among them, and the deepest level", $"{Tallies(reread)} {reread.DeepestReached}", "2:31 3:0 9");
            written.Clear();
            DungeonState.BeforeWrite = p => written.Add(Path.GetFileName(p));
            DungeonState.Load(split).Save(split);
            DungeonState.BeforeWrite = null;
            Check("split: once only - the next start and save write nothing", string.Join(" ", written), "");

            // ---- a split cut short, finished at the next start ----
            string cut = Folder("cut");
            File.WriteAllText(Path.Combine(cut, "dungeon.json"), withStamps);
            int levelFiles = 0;
            DungeonState.BeforeWrite = p => { if (Path.GetFileName(p).StartsWith("dungeon-") && ++levelFiles == 3) throw new IOException("killed"); };
            var during = DungeonState.Load(cut);
            DungeonState.BeforeWrite = null;
            Check("cut short after two level files: those two there, the old file untouched, no .old",
                  $"{Names(cut)} {File.ReadAllBytes(Path.Combine(cut, "dungeon.json")).SequenceEqual(oldBytes)}",
                  "dungeon-01.json dungeon-02.json dungeon.json True");
            Check("...and the game that was running still has every room", AllRooms(during) == AllRooms(expect), true);
            DungeonState.Load(cut);
            Check("the next start finishes it: every level, dungeon.json anew, the old kept", Names(cut),
                  "dungeon-01.json dungeon-02.json dungeon-03.json dungeon-04.json dungeon-06.json dungeon-09.json dungeon.json dungeon.json.old");
            Check("...with the same room records", AllRooms(DungeonState.Load(cut)) == AllRooms(expect), true);
            Check("...and the old file kept whole", File.ReadAllBytes(Path.Combine(cut, "dungeon.json.old")).SequenceEqual(oldBytes), true);

            // both forms, and a level file that differs from the old file's level (the game went on
            // after the level was split, then died): the level file is kept, the old file's level is not
            string both = Folder("both");
            File.WriteAllText(Path.Combine(both, "dungeon.json"), withStamps);
            var newer = Room(77);
            var mine = new DungeonState();
            mine.StoreRoom(2, 1, newer);
            mine.Save(Folder("both-level"));
            File.Copy(DungeonState.LevelPath(Path.Combine(scratch, "both-level"), 2), DungeonState.LevelPath(both, 2));
            var won = DungeonState.Load(both);
            Check("both forms: a level with its own file keeps it, the old file's rooms for it are not taken",
                  $"{Full(won.LoadRoom(2, 1)) == Full(newer)} {won.Rooms.Keys.Count(k => k.StartsWith("2:"))}", "True 1");
            Check("both forms: the other levels from the old file", AllRooms(won).Split('\n').Count(l => l.StartsWith("3:")),
                  AllRooms(expect).Split('\n').Count(l => l.StartsWith("3:")));

            // cut short later: every level file and dungeon.json.old written, dungeon.json not yet
            string late = Folder("late");
            File.WriteAllText(Path.Combine(late, "dungeon.json"), withStamps);
            DungeonState.BeforeWrite = p => { if (Path.GetFileName(p) == "dungeon.json") throw new IOException("killed"); };
            DungeonState.Load(late);
            DungeonState.BeforeWrite = null;
            Check("cut short before the new dungeon.json: the old file still in its place, and kept",
                  $"{File.ReadAllBytes(Path.Combine(late, "dungeon.json")).SequenceEqual(oldBytes)} {File.Exists(Path.Combine(late, "dungeon.json.old"))}", "True True");
            var lateBack = DungeonState.Load(late);
            Check("...the next start finishes it, keeping the one copy (no dungeon.json.old2)",
                  $"{File.ReadAllText(Path.Combine(late, "dungeon.json"))} {File.Exists(Path.Combine(late, "dungeon.json.old2"))} {AllRooms(lateBack) == AllRooms(expect)}",
                  "{\"Format\":2,\"DeepestReached\":9} False True");

            // an earlier dungeon.json.old with other contents is never written over
            string again = Folder("again");
            File.WriteAllText(Path.Combine(again, "dungeon.json"), withStamps);
            File.WriteAllText(Path.Combine(again, "dungeon.json.old"), "an older split");
            DungeonState.Load(again);
            Check("an earlier dungeon.json.old is left as it is; this one is dungeon.json.old2",
                  $"{File.ReadAllText(Path.Combine(again, "dungeon.json.old"))} {File.ReadAllBytes(Path.Combine(again, "dungeon.json.old2")).SequenceEqual(oldBytes)}",
                  "an older split True");

            // the owner's kind of world, when one is named (a copy; the file itself is only read)
            if (Environment.GetEnvironmentVariable("MORDOR_OLD_DUNGEON") is { Length: > 0 } real && File.Exists(real))
            {
                string world = Folder("world");
                File.Copy(real, Path.Combine(world, "dungeon.json"));
                var whole = JsonSerializer.Deserialize<DungeonState>(File.ReadAllText(real))!;
                var cleared = whole.Cleared;   // a save from before the rooms were stored would need TakeLegacyStamps too
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var worldNow = DungeonState.Load(world);
                sw.Stop();
                var worldBack = DungeonState.Load(world);
                Console.WriteLine($"  (MORDOR_OLD_DUNGEON: {new FileInfo(real).Length:N0} bytes, {whole.Rooms.Count} rooms; split in {sw.ElapsedMilliseconds} ms into "
                                  + string.Join(", ", Directory.GetFiles(world).Where(f => DungeonState.IsLevelFile(f) || f.EndsWith("dungeon.json"))
                                                       .OrderBy(f => f).Select(f => $"{Path.GetFileName(f)} {new FileInfo(f).Length:N0}")) + ")");
                bool same = whole.Rooms.All(kv => Full(worldBack.LoadRoom(int.Parse(kv.Key.Split(':')[0]), int.Parse(kv.Key.Split(':')[1]))) == Full(kv.Value));
                Check("a real old dungeon.json: every room's records the same after the split", same && cleared == null, true);
                Check("...the tallies and the deepest level the same", $"{Tallies(worldBack)} {worldBack.DeepestReached}", $"{Tallies(whole)} {whole.DeepestReached}");
            }
            else Console.WriteLine("  (no MORDOR_OLD_DUNGEON: a real old dungeon.json not split here)");

            // ---- the save journal says why each file was written ----
            string journaled = Folder("journaled");
            File.WriteAllText(Path.Combine(journaled, "dungeon.json"), withStamps);
            try
            {
                SaveJournal.Open(journaled, "MORDOR_SAVES", "test");
                var js = DungeonState.Load(journaled);
                var jr = js.LoadRoom(4, 3); jr.Foes[0].Hits++;
                js.StoreRoom(4, 3, jr);
                using (SaveJournal.Because("autosave")) js.Save(journaled);
            }
            finally { SaveJournal.Detach(); }
            var lines = File.ReadAllLines(Path.Combine(journaled, SaveJournal.FileName));
            Check("journal: the split, each file a WRITE with its reason",
                  lines.Count(l => l.Contains("WRITE \"dungeon-") && l.Contains("dungeon.json split into a file per level; via DungeonState.")) == 6
                  && lines.Any(l => l.Contains("WRITE \"dungeon.json.old\"") && l.Contains("split"))
                  && lines.Any(l => l.Contains("WRITE \"dungeon.json\"") && l.Contains("split")), true);
            Check("journal: a later room change, its level's file with the save's reason",
                  lines.Count(l => l.Contains("WRITE \"dungeon-04.json\"") && l.Contains("; autosave; via DungeonState.") && l.Contains("from SelfCheckProbe.DungeonFilesChecks")), 1);

            // ---- Backup and Restore carry the level files; none is taken for a damaged character ----
            string saves = Folder("saves");
            foreach (string f in Directory.GetFiles(split)) File.Copy(f, Path.Combine(saves, Path.GetFileName(f)));
            new Character { Name = "Hob", Stats = new[] { 10, 11, 12, 13, 14, 15 }, Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } } }
                .Save(Path.Combine(saves, "Hob.json"));
            string backup = Folder("backup");
            Backups.Backup(saves, backup);
            Check("Backup: the level files and dungeon.json copied", Names(backup), Names(saves));
            string restored = Folder("restored");
            Backups.Restore(backup, restored);
            Check("Restore: the same rooms back", AllRooms(DungeonState.Load(restored)) == AllRooms(expect), true);
            Check("a level file is not a character", $"{SaveRecovery.MayBeCharacter(DungeonState.LevelPath(saves, 1))} {Backups.IsCharacterFile(DungeonState.LevelPath(saves, 1))} {DungeonState.IsLevelFile("Hob.json")}",
                  "False False False");
            SaveRecovery.TakeNotices();
            log.Clear();
            var listed = Roster.Saved(saves);
            Check("the menu's list: the character, no damaged file said, nothing put back",
                  $"{string.Join(",", listed.Select(c => c.Ch.Name))} {SaveRecovery.TakeNotices().Count} {log.Count(l => l.Contains("dungeon"))} {Directory.Exists(Path.Combine(saves, SaveRecovery.DamagedDir))}",
                  "Hob 0 0 False");
        }
        finally
        {
            DungeonState.BeforeWrite = null;
            SafeFile.Log = oldSafeLog;
            try { Directory.Delete(scratch, true); } catch { }
        }
    }
}
