using Mordor.Core;

/// <summary>What are the monster PicId values, and do they index a 60x60 grid in the extracted sheet?</summary>
public static class ArtProbe
{
    public static void Run(string dir)
    {
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        var ids = monsters.Select(m => (int)m.PicId).ToList();
        Console.WriteLine($"monsters {monsters.Count}: PicId min {ids.Min()} max {ids.Max()} distinct {ids.Distinct().Count()}");
        Console.WriteLine("counts by PicId (first 40): " + string.Join(" ", ids.GroupBy(v => v).OrderBy(g => g.Key).Take(40).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("\nSome monsters and their picture id:");
        foreach (var m in monsters.Where(m => m.Name is "Kobold" or "Orc" or "Large Rat" or "Giant Spider" or "Skeleton" or "Zombie" or "Pseudo-Dragon" or "Medusa" or "Asmodeus" or "Black Bear" or "Giant Centipede" or "Outcast Goblin"))
            Console.WriteLine($"  {m.Name,-18} picId {m.PicId,4}  kind {m.Kind}");
        Console.WriteLine("\nLowest and highest picIds:");
        foreach (var m in monsters.OrderBy(m => m.PicId).Take(6)) Console.WriteLine($"  {m.PicId,4}  {m.Name}");
        foreach (var m in monsters.OrderByDescending(m => m.PicId).Take(6)) Console.WriteLine($"  {m.PicId,4}  {m.Name}");
    }
}
