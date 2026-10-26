using Mordor.Core;

/// <summary>
/// The wear classes out of MDATA1 and the maximum each allows, <c>gv15C6(class).M54F1</c>. Nothing
/// else prints this table, and two routines read it in ways that disagree - see the class-maximum
/// block in <c>selfcheck</c> and the entry in docs/FIDELITY.md.
///
/// Three values appear: 1 for the fifteen classes you wear one of, 0 for the eight consumables,
/// and -1 for Miscellaneous Object, which is the only class the game will let you pile up.
/// </summary>
public static class ClassDump
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir);
        Console.WriteLine($"wear classes in MDATA1: {Mdata1.ItemDescriptions.Count}\n");
        foreach (var g in Enumerable.Range(0, Mdata1.ItemDescriptions.Count)
                                    .GroupBy(i => Mdata1.ItemDescriptions[i].Index)
                                    .OrderByDescending(g => g.Key))
        {
            string what = g.Key switch
            {
                1 => "one at a time",
                0 => "the equip toggle refuses these outright (MODULE6.BAS:1724)",
                _ => "no cap at all - the toggle lets it through and fn11B6 declines to cap it",
            };
            Console.WriteLine($"  max {g.Key,2}  ({g.Count()} classes)  {what}");
            foreach (int i in g)
                Console.WriteLine($"        {i,2}  {Mdata1.ItemDescriptions[i].Name?.Trim()}");
        }
    }
}
