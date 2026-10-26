using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// sub0730 (MAINMOD2.BAS:4273-4328) as the chest calls it (MONSMOD.BAS:3840-3848): the item is
    /// filed under the taker with the monster it came from, today's date, the level found on and
    /// one more found - which is what the Library's "Reports have it that N of these items have
    /// been found, the last one from a X on level L, D days ago." reads (HALL.BAS:378-394).
    /// </summary>
    private static void LibraryFindChecks(string dir)
    {
        Console.WriteLine("\nthe Library files a chest's find as sub0730 does (MAINMOD2.BAS:4273-4328, MONSMOD.BAS:3840-3848)");
        var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var it = items.First(i => i.Id != 0);
        int lead = Enumerable.Range(0, all.Count).First(i => all[i].Id != 0 && !MonsterProperty.Has(all[i], MonsterProperty.Invisible));
        int ghost = Enumerable.Range(0, all.Count).First(i => all[i].Id != 0 && MonsterProperty.Has(all[i], MonsterProperty.Invisible));
        var taker = Character.Create("Taker", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        int day = Librarian.Serial(new DateTime(2026, 10, 15));

        Check("known lead: filed under its own Id (3841-3844)", Librarian.ChestFindFrom(all[lead], lead, lead, taker), all[lead].Id);
        Check("lead taken for something else: an unknown monster, -2 (3846-3847)", Librarian.ChestFindFrom(all[lead], lead, lead + 1, taker), (short)-2);
        Check("invisible lead, taker without See Invisible: -2 (3841-3842)", Librarian.ChestFindFrom(all[ghost], ghost, ghost, taker), (short)-2);
        taker.SeeingInvisible = true;
        Check("invisible lead, taker who sees it: its Id (3844)", Librarian.ChestFindFrom(all[ghost], ghost, ghost, taker), all[ghost].Id);
        taker.SeeingInvisible = false;

        var store = new Librarian.Store();
        Librarian.FileFind(store, taker, it.Id, IdLevel.All, all[lead].Id, day, false, false, 4);
        var rec = store.ForItem(it.Id);
        Check("found: graded 4 for fully known (4285-4286)", rec.Known, 4);
        Check("found: the taker's name on a new record (4295-4298)", rec.Name, "Taker");
        Check("found: the monster's Id (4306)", rec.MonId, all[lead].Id);
        Check("found: today's date (4307)", rec.LastSeen, day);
        Check("found: Chars(0)'s level (4317)", rec.Location, "4");
        Check("found: one found (4323)", rec.NSeen, 1);
        string page = Librarian.DescribeItem(it, rec, day + 3, id => all.First(m => m.Id == id).Name);
        Check("the page says so (HALL.BAS:378-394)", page.Contains($"Reports have it that 1 of these items have been found, the last one from a {all[lead].Name} on level 4, 3 days ago.", StringComparison.Ordinal), true);

        Librarian.FileFind(store, taker, it.Id, IdLevel.Partial, -2, day + 5, true, false, 6);
        rec = store.ForItem(it.Id);
        Check("again, lost: \"?\" for the level (4319)", rec.Location, "?");
        Check("again: an unknown monster, -2 (4310)", rec.MonId, (short)-2);
        Check("again: the date moves (4311)", rec.LastSeen, day + 5);
        Check("again: two found", rec.NSeen, 2);
        Check("again: a lower grade does not lower it (4302-4304)", rec.Known, 4);
        page = Librarian.DescribeItem(it, rec, day + 5, id => all.First(m => m.Id == id).Name);
        Check("the page: \"unknown monster on level ?\" (HALL.BAS:385-386)", page.Contains("the last one from a unknown monster on level ? just Today.", StringComparison.Ordinal), true);

        // FRMCTYG.BAS:434 and FRMCHAR3.BAS:550 pass -1 and False: grade and name only, no find
        var plain = new Librarian.Store();
        Librarian.FileItem(plain, taker, it.Id, IdLevel.Mostly);
        rec = plain.ForItem(it.Id);
        Check("re-read: graded 3 for Mostly (4287-4288)", rec.Known, 3);
        Check("re-read: no find counted, no date (4305-4325)", (rec.NSeen, rec.LastSeen, rec.Location), (0L, 0, ""));

        // FRMLIBRA.BAS:66, 75 list a record only "If l005A.IdLevel": a find read below Mostly is
        // written at grade 0 (4289-4290) and kept, but neither listed nor its page reachable
        var low = new Librarian.Store();
        Librarian.FileFind(low, taker, it.Id, IdLevel.Partial, all[lead].Id, day, false, false, 2);
        Check("a find read below Mostly: the record is written (4327)", low.Entries.ContainsKey("i:" + it.Id), true);
        Check("at grade 0 (4289-4290)", low.ForItem(it.Id).Known, 0);
        Check("and the Library does not list it (FRMLIBRA.BAS:75)", low.KnowsItem(it.Id), false);
        Librarian.FileFind(low, taker, it.Id, IdLevel.Mostly, all[lead].Id, day, false, false, 2);
        Check("read again at Mostly: listed (FRMLIBRA.BAS:75)", low.KnowsItem(it.Id), true);
        Check("a creature with no record is not listed (FRMLIBRA.BAS:66)", low.KnowsMonster(lead), false);

        // a library.json holding a null record: dropped on load, and filing over it works
        string dirN = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-librarynull-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirN);
        try
        {
            File.WriteAllText(Librarian.Store.PathFor(dirN), "{ \"Entries\": { \"i:" + it.Id + "\": null, \"m:" + all[lead].Id + "\": { \"Known\": 2 } } }");
            var loaded = Librarian.Store.Load(dirN, all);
            Check("a null record in the file: dropped on load", loaded.Entries.ContainsKey("i:" + it.Id), false);
            Check("the rest kept", loaded.KnowsMonster(lead), true);
            Librarian.FileFind(loaded, taker, it.Id, IdLevel.All, all[lead].Id, day, false, false, 1);
            Check("and a find filed over it", loaded.ForItem(it.Id).NSeen, 1);
        }
        finally { try { Directory.Delete(dirN, true); } catch { } }

        // fn0F88 keeps Foes(g).KnownAs as the group last stood: recognised (FRMDUNM.BAS:490) and then
        // killed, the room's record still says what it was recognised as, which the chest compares
        // with Fight.M4F24 (MONSMOD.BAS:3840)
        int other = Enumerable.Range(lead + 1, all.Count - lead - 1).First(i => all[i].Id != 0 && all[i].Id != all[lead].Id);
        var stored = new RoomRecord();
        stored.Foes[0].Ref = lead; stored.Foes[0].KnownAs = other; stored.Foes[0].GroupCount = 2;
        var members = new List<CombatMonster>
        {
            new() { Def = all[lead], Group = 0, Hits = 0, MaxHits = 10, Believed = all[lead] },
            new() { Def = all[lead], Group = 0, Hits = -3, MaxHits = 10, Believed = all[lead] },
        };
        var after = EncounterGenerator.Capture(stored, members, null, all);
        Check("recognised, then all killed: none standing", after.Foes[0].GroupCount, 0);
        Check("and KnownAs is what it was recognised as", after.Foes[0].KnownAs, lead);
        Check("so the chest credits the lead (3840-3844)", Librarian.ChestFindFrom(all[lead], lead, after.Foes[0].KnownAs, taker), all[lead].Id);
        stored.Foes[0].KnownAs = other;
        foreach (var m in members) m.Believed = all[other];
        after = EncounterGenerator.Capture(stored, members, null, all);
        Check("never recognised, killed: still taken for the other", after.Foes[0].KnownAs, other);
        Check("so the chest's find is an unknown monster's (3846-3847)", Librarian.ChestFindFrom(all[lead], lead, after.Foes[0].KnownAs, taker), (short)-2);
    }
}
