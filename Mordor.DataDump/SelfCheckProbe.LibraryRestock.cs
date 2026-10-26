using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// mdrReplenishStore files each item it puts on a shelf in the Library:
    /// <c>sub0730 m0070, -l02C4, m029E, -2, True</c> (MAINMOD.BAS:1582-1586; sub0730 is
    /// MAINMOD2.BAS:4273-4328 and draws no Rnd).
    /// </summary>
    private static void LibraryRestockChecks(string dir)
    {
        Console.WriteLine("\nthe store restock files what it shelves in the Library (MAINMOD.BAS:1586, MAINMOD2.BAS:4273-4328)");
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        int day = Librarian.Serial(new DateTime(2026, 10, 18));

        var it = items.First(i => i.Id != 0 && i.Floor > 0 && i.Cursed == 0);
        var lib = new Librarian.Store();
        Librarian.FileRestock(lib, it.Id, day);
        var rec = lib.ForItem(it.Id);
        Check("shelved: fully known, 4 (m029E = m0372 = 0; 4285-4286)", rec.Known, 4);
        Check("no finder: no name (negated index, 4280-4283, 4295-4301)", rec.Name, "");
        Check("an unknown monster, -2, and today (4309-4312)", (rec.MonId, rec.LastSeen), ((short)-2, day));
        Check("level \"?\" and one found (4314-4323)", (rec.Location, rec.NSeen), ("?", 1L));
        Check("the page: \"unknown monster on level ? just Today.\" (HALL.BAS:385-388)",
              Librarian.DescribeItem(it, rec, day).Contains("1 of these items have been found, the last one from a unknown monster on level ? just Today.", StringComparison.Ordinal), true);

        var taker = Character.Create("Taker", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        var named = new Librarian.Store();
        Librarian.FileItem(named, taker, it.Id, IdLevel.Mostly);
        Librarian.FileRestock(named, it.Id, day);
        Check("over a graded record: the first recorder kept, the grade raised to 4", (named.ForItem(it.Id).Name, named.ForItem(it.Id).Known), ("Taker", (short)4));
        var low = new Librarian.Store();
        Librarian.FileItem(low, taker, it.Id, IdLevel.Partial);
        Librarian.FileRestock(low, it.Id, day);
        Check("over a grade-0 record: its name goes, as for any record with no grade (4295-4301)", (low.ForItem(it.Id).Name, low.ForItem(it.Id).Known), ("", (short)4));

        // the restock itself: a filing for each shelf raised, and not one draw more
        int seed = 0, filedCount = 0;
        StoreStock with = null!, without = null!;
        var shelvedIds = new List<short>();
        for (seed = 1; seed < 200 && filedCount == 0; seed++)
        {
            with = StoreStock.CreateInitial(items);
            without = StoreStock.CreateInitial(items);
            shelvedIds.Clear();
            var library = new Librarian.Store();
            with.Replenish(items, 50, new Random(seed), StoreStock.DeepestStocked, x => { shelvedIds.Add(x.Id); Librarian.FileRestock(library, x.Id, day); });
            without.Replenish(items, 50, new Random(seed), StoreStock.DeepestStocked);
            filedCount = shelvedIds.Count;
            if (filedCount > 0)
            {
                Check("a restock that shelved something filed it", library.Entries.Values.Sum(e => e.NSeen), (long)filedCount);
                Check("each such record fully known, no finder, \"?\"",
                      library.Entries.Values.All(e => e.Known == 4 && e.Name == "" && e.MonId == -2 && e.Location == "?"), true);
                Check("only what was shelved: no cursed item, no permanent stock (1575)",
                      shelvedIds.All(id => items.First(i => i.Id == id) is Item x && x.Cursed == 0 && x.Floor > 0), true);
            }
        }
        Check("the restock found something to shelve within the seeds tried", filedCount > 0, true);
        Check("filing draws no Rnd: the shelves come out the same with and without it",
              string.Join(";", with.Shelf.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value)),
              string.Join(";", without.Shelf.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value)));

        string city = PortSource("Mordor.App", "CityWindow.axaml.cs");
        Check("the city files the restock's shelvings in the Library",
              city.Contains("Librarian.FileRestock(library, it.Id, today)", StringComparison.Ordinal), true);
    }
}
