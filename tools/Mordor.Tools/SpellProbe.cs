using Mordor.Core;

/// <summary>Group the spell table by category to infer which guild/school each category is.</summary>
public static class SpellProbe
{
    public static void Run(string dir)
    {
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        Console.WriteLine($"{spells.Count} spells");
        foreach (var grp in spells.GroupBy(s => s.Category).OrderBy(g => g.Key))
        {
            Console.WriteLine($"\nCategory {grp.Key}  ({grp.Count()} spells)");
            foreach (var s in grp.OrderBy(s => s.Level).ThenBy(s => s.Id))
                Console.WriteLine($"  L{s.Level,2} [{s.Id,3}] {s.Name,-24} dmg {s.Damage1,3}-{s.Damage2,-3} kill {s.KillEffect} mon {s.AffectMonster} grp {s.AffectGroup} spec {s.SpecialEffect} u4 {s.U4} effect {s.Effect} req {string.Join(",", s.Required.Take(6))}");
        }
    }
}
