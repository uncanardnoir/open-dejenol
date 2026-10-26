using Mordor.Core;

/// <summary>
/// Reinterpret each guild's 38 floats as two arrays of 19 (not 19 pairs): the second array
/// looks like a per-spell-category level cap. Print it against the spell categories.
/// </summary>
public static class SpellCapProbe
{
    public static void Run(string dir)
    {
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        Console.WriteLine("Spell level range per category:");
        foreach (var g in spells.Where(s => s.Id != 0).GroupBy(s => s.Category).OrderBy(g => g.Key))
            Console.WriteLine($"  cat {g.Key,2}: n={g.Count(),2} levels {g.Min(s => s.Level)}-{g.Max(s => s.Level)}  e.g. {string.Join(", ", g.OrderBy(s => s.Level).Take(4).Select(s => $"{s.Name}(L{s.Level})"))}");

        string path = System.IO.Path.Combine(dir, "MDATA1.MDR");
        using var r = new RecordReader(path, 260);
        string[] guilds = { "Nomad", "Warrior", "Paladin", "Ninja", "Villain", "Seeker", "Thief", "Scavenger", "Mage", "Sorcerer", "Wizard", "Healer" };
        Console.WriteLine("\nPer-guild arrays (first 19 floats | second 19 floats):");
        for (int i = 14; i <= 32; i++)
        {
            r.Seek(i); r.Read(); int len = r.GetUWord(); if (len < 2 || len > 16) continue;
            r.Seek(i); r.Read(); string name = r.GetString(); if (!guilds.Contains(name)) continue;
            r.GetWord(); r.GetWord(); r.GetWord(); r.GetFloat(); r.GetWord(); r.GetWords(7); r.GetDword();
            for (int k = 0; k < 7; k++) r.GetFloat(); r.GetWord(); r.GetFloat(); r.GetWord();
            var all = new float[38]; for (int k = 0; k < 38; k++) all[k] = r.GetFloat();
            Console.WriteLine($"{name,-9} A: {string.Join(" ", all.Take(19).Select(v => v.ToString("0.#").PadLeft(4)))}");
            Console.WriteLine($"{"",-9} B: {string.Join(" ", all.Skip(19).Select(v => v.ToString("0.#").PadLeft(4)))}");
        }
    }
}
