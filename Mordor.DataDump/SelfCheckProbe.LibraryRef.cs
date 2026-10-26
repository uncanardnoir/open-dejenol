using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The Library's file holds a record per table index (sub0F9A/sub0FCB, HALL.BAS:60-80, 675-693):
    /// sub06FE files <c>p05D0.Ref</c> (MONSMOD.BAS:3963, 4005), frmLibrary lists index by index
    /// (FRMLIBRA.BAS:64-76). MDATA5 gives Id 307 to the Morgul and the Shadow Ninja, so they keep a
    /// record each. A library.json written by Id is moved across on load.
    /// </summary>
    private static void LibraryRefChecks(string dir)
    {
        Console.WriteLine("\nthe Library is keyed by table index, as sub0F9A reads it (HALL.BAS:60-80, MONSMOD.BAS:3963, FRMLIBRA.BAS:64-76)");
        var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));

        // items: the original keys them by index too (sub0730 pv065A, MAINMOD2.BAS:4284, 4326-4327);
        // the port keys by Id, which is the same record exactly while no two items share an Id
        Check("MDATA3: no two items share an Id, so an item Id is one table index", items.GroupBy(i => i.Id).All(g => g.Count() == 1), true);

        var shared = all.GroupBy(m => m.Id).Where(g => g.Count() > 1).ToList();
        Check("MDATA5: one Id is shared, 307", string.Join(",", shared.Select(g => g.Key)), "307");
        int morgul = all.FindIndex(m => m.Id == 307), ninja = all.FindLastIndex(m => m.Id == 307);
        Check("307 is the Morgul at 278 and the Shadow Ninja at 389",
              (morgul, all[morgul].Name.Trim(), ninja, all[ninja].Name.Trim()), (278, "Morgul", 389, "Shadow Ninja"));

        var sage = Character.Create("Sage", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
        sage.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 500 });
        sage.SeeingInvisible = true;
        int day = Librarian.Serial(new DateTime(2026, 10, 18));

        // filing: a sighting of each, a death by the Ninja, a companion of the Ninja
        var store = new Librarian.Store();
        Librarian.FileSighting(store, all, morgul, morgul, 3, false, true, sage, "1,1,9", 9, day);
        Librarian.FileSighting(store, all, ninja, ninja, 2, false, true, sage, "2,2,9", 9, day);
        Librarian.FileSighting(store, all, ninja, ninja, 1, true, false, sage, "3,3,9", 9, day);
        Check("the Morgul sighted: its own record, 3 seen (4003)", (store.ForMonster(morgul).NSeen, store.ForMonster(morgul).NDeaths), (3L, (short)0));
        Check("the Shadow Ninja: a record of its own, 2 seen and the death (4002-4003)", (store.ForMonster(ninja).NSeen, store.ForMonster(ninja).NDeaths), (2L, (short)1));
        Check("both records say Id 307 in their Id field (4004)", (store.ForMonster(morgul).Id, store.ForMonster(ninja).Id), ((short)307, (short)307));
        Check("their places apart", (store.ForMonster(morgul).Location, store.ForMonster(ninja).Location), ("1,1,9", "3,3,9"));

        var fresh = new Librarian.Store();
        Librarian.FileCompanion(fresh, sage, all[ninja], 1);
        Check("a Shadow Ninja companion's ID files the Ninja, not the Morgul (FRMCITYC.BAS:194)",
              (fresh.KnowsMonster(ninja), fresh.KnowsMonster(morgul)), (true, false));
        var byHand = new Monster { Name = "Hand-made", Id = 307 };
        Librarian.FileCompanion(fresh, sage, byHand, 0);
        Check("a creature built by hand, no table index: nothing filed", fresh.Entries.Count, 1);

        // listing: one row per table index with a graded record (FRMLIBRA.BAS:64-69)
        var rows = all.Where(m => store.KnowsMonster(m.Ref)).Select(m => m.Name.Trim()).ToList();
        Check("the list holds both, each once", string.Join(",", rows), "Morgul,Shadow Ninja");
        Check("and each page reads its own record (HALL.BAS:107-117, by name: the names are unique)",
              (all.Count(m => m.Name.Trim() == "Morgul"), all.Count(m => m.Name.Trim() == "Shadow Ninja"),
               Librarian.DescribeMonster(all[ninja], store.ForMonster(ninja), day, "them").Contains("killed by this creature 1 time.", StringComparison.Ordinal),
               Librarian.DescribeMonster(all[morgul], store.ForMonster(morgul), day, "them").Contains("no known deaths", StringComparison.Ordinal)),
              (1, 1, true, true));

        // the item page names a find's source from the Id it holds, by fn0E0D's first index
        Check("a find credited to 307 reads as the Morgul (fn0E0D, MONSMOD.BAS:2269-2277)", Librarian.FinderName(all, 307).Trim(), "Morgul");
        Check("an Id no creature has: fn0E0D's own 0, the creature at index 0", Librarian.FinderName(all, 9999), all[0].Name);
        Check("the chest still credits a monster Id, not its index (MAINMOD2.BAS:4306)",
              Librarian.ChestFindFrom(all[ninja], ninja, ninja, sage), (short)307);

        int plain = Enumerable.Range(1, all.Count - 1).First(i => all[i].Id != i && all.Count(m => m.Id == all[i].Id) == 1);
        short plainId = all[plain].Id;
        var it = items.First(i => i.Id != 0);
        string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-libraryref-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string file = Librarian.Store.PathFor(scratch);

            // round trip
            store.Save(scratch);
            string text = File.ReadAllText(file);
            Check("saved keyed mr:278 and mr:389, no Id keys", (text.Contains("\"mr:278\"", StringComparison.Ordinal), text.Contains("\"mr:389\"", StringComparison.Ordinal), text.Contains("\"m:", StringComparison.Ordinal)), (true, true, false));
            var back = Librarian.Store.Load(scratch, all);
            Check("read back: both records as they were", (back.ForMonster(morgul).NSeen, back.ForMonster(ninja).NSeen, back.ForMonster(ninja).NDeaths), (3L, 2L, (short)1));

            // a file from before: Id keys
            string Old(string extra) =>
                "{ \"Entries\": { "
                + "\"m:307\": { \"Id\": 307, \"Known\": 3, \"NSeen\": 5, \"Name\": \"Old\", \"LastSeen\": 46000, \"Location\": \"4,4,8\", \"NDeaths\": 2 }, "
                + "\"m:" + plainId + "\": { \"Id\": " + plainId + ", \"Known\": 2, \"NSeen\": 1, \"Name\": \"Plain\" }, "
                + "\"m:9999\": { \"Id\": 9999, \"Known\": 1, \"Name\": \"Nobody\" }, "
                + "\"i:" + it.Id + "\": { \"Id\": " + it.Id + ", \"Known\": 4, \"NSeen\": 7, \"MonId\": 307 }" + extra + " } }";
            File.WriteAllText(file, Old(""));

            // loaded without the table: items only, and the Id keys saved as they are
            var blind = Librarian.Store.Load(scratch);
            Check("loaded without the monster table: its creatures unsettled", blind.MonstersUnsettled, true);
            bool threw = false;
            try { blind.ForMonster(morgul); } catch (InvalidOperationException) { threw = true; }
            Check("and not to be read as indices", threw, true);
            Librarian.FileItem(blind, sage, it.Id, IdLevel.All);
            blind.Save(scratch);
            text = File.ReadAllText(file);
            Check("saved so: the Id keys stay Id keys, never relabelled as indices",
                  (text.Contains("\"m:307\"", StringComparison.Ordinal), text.Contains("\"mr:", StringComparison.Ordinal)), (true, false));

            var moved = Librarian.Store.Load(scratch, all);
            Check("with the table: settled", moved.MonstersUnsettled, false);
            var m307 = moved.ForMonster(morgul);
            Check("m:307 goes to the first index with it, the Morgul (fn0E0D; MAINMOD2.BAS:4722-4727)",
                  (m307.Known, m307.NSeen, m307.NDeaths, m307.Name, m307.Location, m307.LastSeen), ((short)3, 5L, (short)2, "Old", "4,4,8", 46000));
            Check("and is not copied to the Shadow Ninja", moved.Entries.ContainsKey("mr:" + ninja), false);
            Check("an Id that is one creature moves to its index exactly", (moved.ForMonster(plain).Known, moved.ForMonster(plain).Name), ((short)2, "Plain"));
            Check("an Id no creature has stays under its old key, untouched", moved.Entries.ContainsKey("m:9999"), true);
            Check("the item record as it was", (moved.ForItem(it.Id).NSeen, moved.ForItem(it.Id).MonId), (7L, (short)307));
            moved.Save(scratch);
            text = File.ReadAllText(file);
            Check("written back: no m:307, the Morgul at mr:278", (text.Contains("\"m:307\"", StringComparison.Ordinal), text.Contains("\"mr:278\"", StringComparison.Ordinal)), (false, true));

            // an older build files into the moved file: it can only add Id keys, which merge on the next load
            string movedText = File.ReadAllText(file);
            int cut = movedText.IndexOf("\"Entries\": {", StringComparison.Ordinal) + "\"Entries\": {".Length;
            File.WriteAllText(file, movedText[..cut]
                + " \"m:307\": { \"Id\": 307, \"Known\": 4, \"NSeen\": 2, \"Name\": \"Older\", \"LastSeen\": 46100, \"Location\": \"1,1,1\", \"NDeaths\": 1 },"
                + movedText[cut..]);
            var merged = Librarian.Store.Load(scratch, all).ForMonster(morgul);
            Check("an older build's later m:307 joins the Morgul's record: grade raised, counts added, newer place and date, first recorder kept",
                  (merged.Known, merged.NSeen, merged.NDeaths, merged.Name, merged.Location, merged.LastSeen), ((short)4, 7L, (short)3, "Old", "1,1,1", 46100));
        }
        finally { try { Directory.Delete(scratch, true); } catch { } }

        // the windows: the Library page lists and reads by index, every creature filing loads the table
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs"), dun = PortSource("Mordor.App", "DungeonWindow.axaml.cs");
        Check("the Library page: a row per table index, its record by Ref, a find named by fn0E0D",
              (city.Contains("store.KnowsMonster(m.Ref)", StringComparison.Ordinal), city.Contains("store.ForMonster(m.Ref)", StringComparison.Ordinal),
               city.Contains("GroupBy(m => m.Id)", StringComparison.Ordinal), city.Contains("Librarian.FinderName(monsters, id)", StringComparison.Ordinal)),
              (true, true, false, true));
        Check("the dungeon loads the Library with the monster table wherever it files",
              (dun.Contains("Librarian.Store.Load(SavesDir)", StringComparison.Ordinal), dun.Contains("Librarian.Store.Load(SavesDir, _monsters)", StringComparison.Ordinal)),
              (false, true));
    }
}
