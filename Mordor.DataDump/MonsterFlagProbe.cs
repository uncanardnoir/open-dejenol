using Mordor.Core;

/// <summary>Which monsters carry each SpecialAttackFlags / SpecialPropertyFlags / SpellFlags bit?</summary>
public static class MonsterFlagProbe
{
    public static void Run(string dir)
    {
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        void Dump(string title, Func<Monster, int> f)
        {
            Console.WriteLine($"\n{title}:");
            for (int b = 0; b < 32; b++)
            {
                var hits = monsters.Where(m => (f(m) >> b & 1) != 0).ToList();
                if (hits.Count == 0) continue;
                Console.WriteLine($"  bit {b,2} n={hits.Count,3}: {string.Join(", ", hits.Take(7).Select(m => m.Name))}");
            }
        }
        Dump("SpecialAttackFlags", m => m.SpecialAttackFlags);
        Dump("SpecialPropertyFlags", m => m.SpecialPropertyFlags);
        Dump("SpellFlags", m => m.SpellFlags);
        Console.WriteLine("\nResistances columns (avg over monsters): " + string.Join(" ", Enumerable.Range(0, 12).Select(i => $"{i}:{monsters.Average(m => m.Resistances[i]):0}")));
        Console.WriteLine("Attacks column sample: " + string.Join(", ", monsters.Take(8).Select(m => $"{m.Name} att{m.Att} def{m.Def} hits{m.Hits} lvl{m.LevelFound}")));
        // List_of_Uncharmable_Monsters_That_Can_Join: "either Charm Resistant, Resistant to All
        // Magic, or have 100% Magic Resistance ... They are, however, either Good or Neutral and
        // thus may still offer to join your party." Those are SpecialPropertyFlags bit 3 and bit 2
        // by gv08DC$'s own names, and resistance index 6 by gv0620$'s. Deriving the set is a check
        // on all three at once, against a list somebody compiled by playing.
        Console.WriteLine("\nuncharmable but able to join (Charm Resistant, Resistant to All Magic, or 100% Magic):");
        var joinable = monsters.Where(m => m.Hits > 0
                                  && (((m.SpecialPropertyFlags >> 3) & 1) != 0
                                   || ((m.SpecialPropertyFlags >> 2) & 1) != 0
                                   || (m.Resistances.Length > 6 && m.Resistances[6] >= 100))
                                  && m.AlignmentValue != 2)
                          .OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        Console.WriteLine($"  {joinable.Count} of them:");
        foreach (var m in joinable)
            Console.WriteLine($"    {m.Name,-26} align {m.AlignmentValue}  charmRes {((m.SpecialPropertyFlags >> 3) & 1)}"
                            + $"  allMagic {((m.SpecialPropertyFlags >> 2) & 1)}  magicRes {(m.Resistances.Length > 6 ? m.Resistances[6] : 0),3}");

    }
}
