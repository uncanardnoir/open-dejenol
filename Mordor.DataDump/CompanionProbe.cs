using Mordor.Core;

/// <summary>How do monster companions link up? (CompanionId / CompanionType / spawn mode)</summary>
public static class CompanionProbe
{
    public static void Run(string dir)
    {
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        var byId = monsters.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        Console.WriteLine("Spawn modes: " + string.Join(" ", monsters.GroupBy(m => m.CompanionSpawnMode).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("Companion types: " + string.Join(" ", monsters.GroupBy(m => m.CompanionType).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("NumGroups: " + string.Join(" ", monsters.GroupBy(m => m.NumGroups).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("InGroup: " + string.Join(" ", monsters.GroupBy(m => m.InGroup).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("\nMonsters with a companion:");
        foreach (var m in monsters.Where(m => m.CompanionId != 0).Take(30))
        {
            string comp = byId.TryGetValue(m.CompanionId, out var c) ? c.Name : (m.CompanionId < monsters.Count ? "idx:" + monsters[m.CompanionId].Name : "?");
            Console.WriteLine($"  {m.Name,-24} L{m.LevelFound,2} kind {m.Kind,-10} -> compId {m.CompanionId,3} ({comp}) type {m.CompanionType} sub {m.CompanionSubtype} mode {m.CompanionSpawnMode} groups {m.NumGroups} inGroup {m.InGroup}");
        }
    }
}
