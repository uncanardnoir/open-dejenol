using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The Library's "This creature was last seen at X,Y,level, N days ago" (HALL.BAS:219-233) is
    /// written by sub06FE (MONSMOD.BAS:3938-4006) every time the dungeon meets a group - the port
    /// had stamped it once, on the first visit to the Library, with the reader's current level
    /// ("level 1" in the City) and never again. This drives <see cref="Librarian.FileSighting"/>
    /// through each of sub06FE's branches and reads the line back out of the page.
    /// </summary>
    private static void LibrarySightingChecks(string dir)
    {
        Console.WriteLine("\nthe Library's last sighting is sub06FE's, written at every meeting (MONSMOD.BAS:3938-4006, HALL.BAS:219-233)");
        var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        int Find(bool invisible) => Enumerable.Range(0, all.Count)
            .First(i => all[i].Id != 0 && all[i].Hits > 0 && MonsterProperty.Has(all[i], MonsterProperty.Invisible) == invisible);
        int seen = Find(false), other = Enumerable.Range(seen + 1, all.Count - seen - 1).First(i => all[i].Id != 0 && all[i].Id != all[seen].Id
                                                                                                && !MonsterProperty.Has(all[i], MonsterProperty.Invisible));
        int ghost = Find(true);
        Character Who(string name, int stat, int guildLevel)
        {
            var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { stat, stat, stat, stat, stat, stat });
            c.Guilds.Clear();
            if (guildLevel > 0) c.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = guildLevel });
            c.DungeonLevel = 3; c.X = 4; c.Y = 6;
            return c;
        }
        var sage = Who("Sage", 25, 500);
        var lead = Who("Lead", 12, 1);
        int day = Librarian.Serial(new DateTime(2026, 10, 15));
        var store = new Librarian.Store();
        // the place as the dungeon passes it: the leader's lost flags and the square stood on
        string? At(Character l) => Librarian.PlaceOf(l.LostLevel, l.LostPosition, l.DungeonLevel, l.X, l.Y);
        string LastLine(int r, int today)
        {
            var page = Librarian.DescribeMonster(all[r], store.ForMonster(r), today, "them");
            return page[(page.LastIndexOf("\r\n", StringComparison.Ordinal) + 2)..];
        }

        // MONSMOD.BAS:719 - a fresh group, filed as seen: place, date, finder, count, grade
        Librarian.FileSighting(store, all, seen, seen, 3, false, true, sage, At(lead), lead.DungeonLevel, day);
        var rec = store.ForMonster(seen);
        Check("seen: the place is Chars(0)'s X,Y,level, from 1 (3987-3991)", rec.Location, "5,7,3");
        Check("seen: the date is today's (3992)", rec.LastSeen, day);
        Check("seen: the group's count is added (4003)", rec.NSeen, 3);
        Check("seen: a new record takes the reader's name (3978-3985)", rec.Name, "Sage");
        Check("seen: fn1272 False, nought or better, is grade 4 (3965-3977)", rec.Known, 4);
        Check("seen: the page says so", LastLine(seen, day), "This creature was last seen at 5,7,3, just Today.");

        // MONSMOD.BAS:732 - the same room met again four days on, deeper: no count, a new place and date
        lead.DungeonLevel = 9; lead.X = 0; lead.Y = 1;
        Librarian.FileSighting(store, all, seen, seen, 3, false, false, sage, At(lead), lead.DungeonLevel, day + 4);
        rec = store.ForMonster(seen);
        Check("met again: the place moves with the party", rec.Location, "1,2,9");
        Check("met again: the date moves too", rec.LastSeen, day + 4);
        Check("met again: no sighting flag, no count", rec.NSeen, 3);
        Check("two days after that: \"N days ago.\" (HALL.BAS:229-231)", LastLine(seen, day + 6), "This creature was last seen at 1,2,9, 2 days ago.");
        Check("the day after: \"just 1 day ago.\" (232)", LastLine(seen, day + 5), "This creature was last seen at 1,2,9, just 1 day ago.");

        // a lost leader: the date is written, the place is left as it was (3987)
        lead.LostLevel = lead.LostPosition = true; lead.DungeonLevel = 2;
        Librarian.FileSighting(store, all, seen, seen, 1, false, false, sage, At(lead), lead.DungeonLevel, day + 7);
        rec = store.ForMonster(seen);
        Check("lost: the place is kept", rec.Location, "1,2,9");
        Check("lost: the date is still today's", rec.LastSeen, day + 7);
        lead.LostLevel = lead.LostPosition = false;

        // MONSMOD.BAS:3042 - a death: one more, and the date
        Librarian.FileSighting(store, all, seen, seen, 1, true, false, sage, At(lead), lead.DungeonLevel, day + 8);
        Check("killed by it: the deaths go up (4002)", store.ForMonster(seen).NDeaths, 1);

        // taken for something else: not filed (3954-3956)
        Librarian.FileSighting(store, all, other, seen, 2, false, true, sage, At(lead), lead.DungeonLevel, day);
        Check("misidentified: nothing is filed", store.KnowsMonster(other), false);
        // an invisible creature, below ground, without See Invisible: not filed (3958-3962)
        Librarian.FileSighting(store, all, ghost, ghost, 1, false, true, sage, At(lead), lead.DungeonLevel, day);
        Check("invisible, unseen: nothing is filed", store.KnowsMonster(ghost), false);
        sage.SeeingInvisible = true;
        Librarian.FileSighting(store, all, ghost, ghost, 1, false, true, sage, At(lead), lead.DungeonLevel, day);
        Check("invisible, with See Invisible: filed", store.KnowsMonster(ghost), true);

        // a reader with no guild level reads nothing: mdrcharGetIDSkill is 0 (MAINMOD2.BAS:2507-2510),
        // so a creature fifteen or more short is grade 1, "very little"
        var novice = Who("Novice", 3, 0);
        int hard = Enumerable.Range(0, all.Count).First(i => all[i].Id != 0 && i != seen && i != other
            && !MonsterProperty.Has(all[i], MonsterProperty.Invisible) && CombatEngine.IdLevelRoll(novice, all[i], null, false) == 3);
        Librarian.FileSighting(store, all, hard, hard, 1, false, true, novice, At(lead), lead.DungeonLevel, day);
        Check("no skill: grade 1 (3976-3977)", store.ForMonster(hard).Known, 1);

        // the square is where the party stands now, not where its record last put it: Chars(0).X
        // and .Y move with every step (MAINMOD2.BAS:3786-3790), the port's record only at a change
        // of area - so the place is built from the live square, whatever the record says
        lead.DungeonLevel = 3; lead.X = 4; lead.Y = 6;   // the record: the square the room was entered on
        Librarian.FileSighting(store, all, seen, seen, 1, true, false, sage,
                               Librarian.PlaceOf(lead.LostLevel, lead.LostPosition, 3, 10, 11), 3, day + 9);
        Check("a death three steps into the room: the square stood on", store.ForMonster(seen).Location, "11,12,3");
        Check("PlaceOf: below ground, not lost", Librarian.PlaceOf(false, false, 2, 0, 0), "1,1,2");
        Check("PlaceOf: the City writes no place", Librarian.PlaceOf(false, false, 0, 3, 3) ?? "null", "null");
        Check("PlaceOf: the level lost, none", Librarian.PlaceOf(true, false, 2, 3, 3) ?? "null", "null");
        Check("PlaceOf: the square lost, none", Librarian.PlaceOf(false, true, 2, 3, 3) ?? "null", "null");

        // HALL.BAS:224 tests only CLng(Date) = 0: a record with no date is counted from 0 like any other
        var undated = new LibraryRecord { Known = 1, Location = "?" };
        string page0 = Librarian.DescribeMonster(all[seen], undated, day, "them");
        Check("a record dated 0 says how many days, as the original does (224-231)",
              page0[(page0.LastIndexOf("\r\n", StringComparison.Ordinal) + 2)..], $"This creature was last seen at ?, {day} days ago.");
        string pageNoClock = Librarian.DescribeMonster(all[seen], undated, 0, "them");
        Check("only a clock at 0 is \"not known\" (224-225)", pageNoClock.EndsWith("it is not known how many days ago.", StringComparison.Ordinal), true);

        LibraryFileChecks();
    }

    /// <summary>
    /// library.json is read and written at every room now, so a bad read must never become an
    /// empty Library written over the good one. In a scratch folder of the system's temp
    /// directory, never the saves.
    /// </summary>
    private static void LibraryFileChecks()
    {
        Console.WriteLine("\nlibrary.json: written beside and moved over, never written over a file it could not read");
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-librarycheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Librarian.Store.PathFor(dir);
            var notes = new List<string>();
            var before = Librarian.Store.Note;
            Librarian.Store.Note = notes.Add;
            try
            {
                var s = Librarian.Store.Load(dir);
                Check("no file: an empty Library", s.Entries.Count, 0);
                s.EditMonster(7).Location = "1,2,3";
                s.Save(dir);
                Check("saved: read back as written", Librarian.Store.Load(dir).ForMonster(7).Location, "1,2,3");
                Check("saved: no temporary file left beside it", File.Exists(file + ".tmp"), false);

                // held open by something else: not read, and not written over
                string good = File.ReadAllText(file);
                Librarian.Store held;
                using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    held = Librarian.Store.Load(dir);
                    Check("a file that cannot be opened: the store is read-only", held.ReadOnly, true);
                }
                held.EditMonster(8).Location = "x";
                held.Save(dir);
                Check("and saving it leaves the file as it was", File.ReadAllText(file) == good, true);

                // garbled: moved aside, kept whole, and a new Library started
                File.WriteAllText(file, "{ \"Entries\": { \"m:7\": ");
                var fresh = Librarian.Store.Load(dir);
                Check("a garbled file: an empty Library", fresh.Entries.Count, 0);
                Check("moved aside to library.json.bad-...", fresh.MovedAside != null && System.IO.Path.GetFileName(fresh.MovedAside).StartsWith("library.json.bad-", StringComparison.Ordinal), true);
                Check("kept whole there", fresh.MovedAside != null && File.ReadAllText(fresh.MovedAside) == "{ \"Entries\": { \"m:7\": ", true);
                Check("and said so", notes.Any(n => n.Contains("moved aside", StringComparison.Ordinal)), true);
                fresh.EditMonster(9).Location = "4,5,6";
                fresh.Save(dir);
                Check("the new Library is written in its place", Librarian.Store.Load(dir).ForMonster(9).Location, "4,5,6");
            }
            finally { Librarian.Store.Note = before; }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
