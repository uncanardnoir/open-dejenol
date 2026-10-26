using Mordor.Core;

/// <summary>
/// MDATA5's TrapFlags against the community's Monster_Trap_Table, which gives every monster a YES
/// or NO for each of the eleven chest traps. The page's column order is not gv08AA$'s index order,
/// so the table is an independent reading of which bit means which trap.
/// </summary>
public static class TrapTableProbe
{
    /// <summary>The page's columns, as the trap index each one names in gv08AA$ order.</summary>
    public static readonly (string Column, int TrapIndex)[] PageColumns =
    {
        ("UNTR", 0), ("PSON", 1), ("XPLD", 3), ("DISE", 2), ("FIRE", 4), ("FEAR", 9),
        ("TPRT", 7), ("WTHR", 10), ("BLKT", 8), ("SLME", 5), ("FATE", 6),
    };

    public static void Run(string dir)
    {
        var ms = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        Console.WriteLine("TrapFlags, the first twelve monsters, bit by bit:");
        Console.WriteLine($"  {"id",3} {"name",-22} flags       " + string.Join(" ", PageColumns.Select(c => c.Column)));
        foreach (var m in ms.Where(x => x.Id != 0).Take(12))
        {
            string cols = string.Join("    ", PageColumns.Select(c => (m.TrapFlags & (1 << c.TrapIndex)) != 0 ? "Y" : "n"));
            Console.WriteLine($"  {m.Id,3} {m.Name.Trim(),-22} 0x{m.TrapFlags:X4}      {cols}");
        }
        Console.WriteLine("\nany monster id that appears more than once in MDATA5:");
        foreach (var g in ms.Where(x => x.Id != 0).GroupBy(x => x.Id).Where(g => g.Count() > 1))
            foreach (var m in g)
                Console.WriteLine($"  id {m.Id}  {m.Name.Trim(),-22} floor {m.LevelFound,2}  hits {m.Hits,5}  trapFlags 0x{m.TrapFlags:X3}");

        Console.WriteLine("\nid, floor and flags for every monster, for comparing numerically:");
        foreach (var m in ms.Where(x => x.Id != 0)) Console.WriteLine($"ROW {m.Id} {m.LevelFound} {m.TrapFlags}");
        Console.WriteLine("\nbits actually used across every monster: "
            + string.Join(", ", Enumerable.Range(0, 16).Where(b => ms.Any(m => (m.TrapFlags & (1 << b)) != 0))));
    }
}
