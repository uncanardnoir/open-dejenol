using Mordor.Core;

/// <summary>
/// The Rarity field (M56DB) against the community's Rare_Monsters page, which groups every monster
/// of rarity 8 or below by that number and gives its level. Their "8% Spawn chance" is an index
/// rather than a probability - the page says so - but the grouping is the field itself.
/// </summary>
public static class RarityProbe
{
    public static void Run(string dir)
    {
        var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"))
                          .Where(m => m.Id != 0 && m.Hits > 0).ToList();
        Console.WriteLine("monsters by Rarity, for every value the Rare_Monsters page covers:");
        for (int r = 8; r >= 1; r--)
        {
            var at = ms.Where(m => m.Chance == r).OrderBy(m => m.LevelFound).ThenBy(m => m.Name, StringComparer.Ordinal).ToList();
            Console.WriteLine($"\n  rarity {r}: {at.Count}");
            foreach (var m in at) Console.WriteLine($"    {m.Name.Trim(),-26} level {m.LevelFound}");
        }
        Console.WriteLine($"\nrarity values present across all monsters: "
                        + string.Join(", ", ms.Select(m => (int)m.Chance).Distinct().OrderBy(v => v)));
    }
}
