using Mordor.Core;

/// <summary>Checks the spell record's last field, M5B5B: which resistance the target rolls against.</summary>
public static class ResistProbe
{
    public static void Run(string dir)
    {
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        Console.WriteLine($"{spells.Count} spells; ResistIndex values in use:");
        foreach (var g in spells.GroupBy(s => s.ResistIndex).OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key,3}: {g.Count(),3} spell(s)   e.g. {string.Join(", ", g.Take(4).Select(s => s.Name))}");
        Console.WriteLine("\na few in full:");
        foreach (var s in spells.Where(s => s.Damage2 > 0).Take(6))
            Console.WriteLine($"  {s.Name,-20} school {s.Category,2} level {s.Level,2} dmg {s.Damage2,3}/{s.SpecialEffect,2} effect {s.Effect,3} resist {s.ResistIndex,3}");
    }
}
