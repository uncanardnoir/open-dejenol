using Mordor.Core;

/// <summary>
/// FRMEXCHA.BAS:199-201. Handing an item to another character re-reads how well they know it, and
/// the test and the assignment do not pass the same thing:
///
/// <code>
/// If  fn12F0(char, Pack(slot).ItemRef) &lt; Pack(slot).IdLevel Then
///     Pack(slot).IdLevel = fn12F0(char, Items(Pack(slot).ItemRef).LevelScale)
/// </code>
///
/// The test passes an item index. The assignment passes that item's <c>LevelScale</c> - a
/// difficulty number - where an index belongs, so what gets stored describes some unrelated item.
///
/// docs/FIDELITY.md says the port does not reproduce this because "there is no single behaviour to
/// be faithful to". That is worth testing rather than asserting: the expression is deterministic,
/// so the question is only whether the indices it produces are in range.
/// </summary>
public static class HandoverProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var real = items.Where(i => i.Id != 0 && i.Name.Trim().Length > 0).ToList();

        // fn12F0(char, p) indexes gv1426 by p, so p is an item *reference* - what the port calls
        // Id, not the record position. The two are not the same here: 356 of the 366 records
        // carry an Id different from their position, so which one the slip lands on matters.
        Item? ById(int id) => items.FirstOrDefault(i => i.Id == id);
        Console.WriteLine($"MDATA3 holds {items.Count} records; ids run "
                        + $"{items.Min(i => i.Id)}..{items.Max(i => i.Id)}.\n");

        var scales = real.Select(i => (int)i.LevelScale).ToList();
        Console.WriteLine($"LevelScale across {real.Count} real items: "
                        + $"low {scales.Min()}, high {scales.Max()}, distinct {scales.Distinct().Count()}");
        int inRange = scales.Count(s => ById(s) != null);
        Console.WriteLine($"  of those, matching a real item id: {inRange} of {scales.Count}");
        Console.WriteLine($"  out of range, so the original's error handler catches them: {scales.Count - inRange}");

        Console.WriteLine("\nwhat the stray index lands on, for the first few items:");
        foreach (var it in real.Take(10))
        {
            var landed = ById(it.LevelScale);
            string lands = landed != null ? "\"" + landed.Name.Trim() + "\"" : "NO SUCH ITEM";
            Console.WriteLine($"  {it.Name.Trim(),-26} LevelScale {it.LevelScale,3}  ->  {lands}");
        }

        // how often would the substitution actually change the answer? fn12F0 is the identify
        // skill, so it only matters when the two items give different levels.
        var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                 new[] { 18, 18, 18, 18, 18, 18 });
        c.Guilds.Clear();
        c.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 12 });

        int differs = 0, same = 0, oob = 0;
        foreach (var it in real)
        {
            var right = Loot.ReadableLevel(c, it, id => items.FirstOrDefault(i => i.Id == id));
            var other = ById(it.LevelScale);
            if (other == null) { oob++; continue; }
            var wrong = Loot.ReadableLevel(c, other, id => items.FirstOrDefault(i => i.Id == id));
            if (right == wrong) same++; else differs++;
        }
        Console.WriteLine($"\nfor a level 12 Nomad, reading the wrong item gives:");
        Console.WriteLine($"  the same identification level: {same}");
        Console.WriteLine($"  a different one:               {differs}");
        Console.WriteLine($"  no item with that id at all:   {oob}");
    }
}
