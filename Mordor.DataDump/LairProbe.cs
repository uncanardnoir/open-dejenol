using Mordor.Core;

/// <summary>
/// Where every lair is, against the community's Laired_Monster_List. A lair is not a property of a
/// square: MDATA11 gives each square an area id and each area a LairId, and the encounter generator
/// leans on it - <c>fn0D4D</c> takes the area's own monster three times in four (MODULE4.BAS:1449).
/// So a coordinate in that published table has to travel square -> area -> lair monster, and the
/// whole chain has to be right for the name to come out.
/// </summary>
public static class LairProbe
{
    public static void Run(string dir)
    {
        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));

        Console.WriteLine("every lair in the dungeon, by the square the area is entered at");
        Console.WriteLine($"  {"x,y,level",-12} {"area",5}  monster");
        int n = 0;
        for (int lv = 1; lv <= dungeon.Count; lv++)
        {
            var f = dungeon[lv - 1];
            var seen = new HashSet<int>();
            for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                {
                    int id = f.AreaIdAt(x, y);
                    if (id <= 0 || !seen.Add(id)) continue;
                    var area = f.Areas[id];
                    if (area.LairId <= 0) continue;
                    var m = monsters.FirstOrDefault(mm => mm.Id == area.LairId);
                    // the game's own coordinates are 1-based; the arrays here are not
                    Console.WriteLine($"  {x + 1},{y + 1},{lv,-8} {id,5}  {m?.Name.Trim() ?? "?"}");
                    n++;
                }
        }
        Console.WriteLine($"\n{n} lairs");

        // Against the community's Laired_Monster_List, which names 81 distinct monsters: 80 are
        // these, and the two that differ both have an explanation in the data.
        //
        // Destroyer of the Deep is on their list at 13,28,15 and is not a lair monster here. It is
        // the Lamurian High Priestess's companion - her CompanionId is its id - and she lairs at
        // that square, so the page is recording where you meet it rather than what the area holds.
        // Their own uncharmable-monsters page says so outright: it "will only spawn as a companion
        // to the Lamurian High Priestess".
        //
        // Vomitis Goo lairs here and is not on their list at all, with nothing in the data to
        // explain it away. That one looks like an omission on the page.
        Console.WriteLine("\nthe two the community's list does not agree with:");
        foreach (string name in new[] { "Lamurian High Priestess", "Destroyer of the Deep", "Vomitis Goo" })
        {
            var m = monsters.FirstOrDefault(x => x.Name.Trim() == name);
            if (m == null) continue;
            var comp = monsters.FirstOrDefault(x => x.Id == m.CompanionId);
            Console.WriteLine($"  {m.Name.Trim(),-26} id {m.Id,3}  companion {(m.CompanionId > 0 ? comp?.Name.Trim() ?? "?" : "none")}");
        }
    }
}
