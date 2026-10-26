using Mordor.Core;

/// <summary>What do Item.SpellId / SpellIndex / Charges / Guilds / AlignmentFlags mean?</summary>
public static class ItemSpellProbe
{
    public static void Run(string dir)
    {
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var byId = spells.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        Console.WriteLine("Items with SpellId > 0 (SpellId -> spell by Id / by index):");
        foreach (var it in items.Where(i => i.SpellId > 0).Take(40))
        {
            string byIdName = byId.TryGetValue(it.SpellId, out var s1) ? s1.Name : "?";
            string byIdx = it.SpellId < spells.Count ? spells[it.SpellId].Name : "?";
            Console.WriteLine($"  {it.Name,-26} type {it.Type,2} spellId {it.SpellId,3} idx {it.SpellIndex,3} lvl {it.SpellLvl,2} charges {it.Charges,4} | byId={byIdName} byIdx={byIdx}");
        }
        Console.WriteLine("\nGuild masks (12 bits) for a few items:");
        string[] guilds = { "Nom", "War", "Pal", "Nin", "Vil", "See", "Thi", "Sca", "Mag", "Sor", "Wiz", "Hea" };
        foreach (var it in items.Where(i => i.Id != 0).Take(400).Where((_, k) => k % 25 == 0))
            Console.WriteLine($"  {it.Name,-26} guilds 0x{it.Guilds:X4} = {string.Join(",", Enumerable.Range(0, 12).Where(b => (it.Guilds >> b & 1) != 0).Select(b => guilds[b]))}   align 0x{it.AlignmentFlags:X} cursed {it.Cursed} hands {it.NHands} classRestr {it.ClassRestricted}");
        Console.WriteLine("\nCharges distribution: " + string.Join(" ", items.GroupBy(i => i.Charges).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("SpellLvl distribution: " + string.Join(" ", items.GroupBy(i => i.SpellLvl).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine("Abilities bits used: " + string.Join(" ", Enumerable.Range(0, 32).Where(b => items.Any(i => (i.Abilities >> b & 1) != 0)).Select(b => $"{b}:{items.Count(i => (i.Abilities >> b & 1) != 0)}")));
    }
}
