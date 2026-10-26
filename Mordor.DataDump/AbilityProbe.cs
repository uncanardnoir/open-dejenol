using Mordor.Core;

/// <summary>Print the 7 guild ability floats from MDATA1 (to match the help's A-F table) and a few monster stat rows.</summary>
public static class AbilityProbe
{
    public static void Run(string dir)
    {
        using var r = new RecordReader(System.IO.Path.Combine(dir, "MDATA1.MDR"), 260);
        string[] guilds = { "Nomad", "Warrior", "Paladin", "Ninja", "Villian", "Villain", "Seeker", "Thief", "Scavenger", "Mage", "Sorcerer", "Wizard", "Healer" };
        Console.WriteLine("guild      abilities[7]                                  | word after | float after | word (quest)");
        for (int i = 14; i <= 32; i++)
        {
            r.Seek(i); r.Read(); int len = r.GetUWord(); if (len < 2 || len > 16) continue;
            r.Seek(i); r.Read(); string name = r.GetString(); if (!guilds.Contains(name)) continue;
            r.GetWord(); r.GetWord(); r.GetWord(); r.GetFloat(); r.GetWord(); r.GetWords(7); r.GetDword();
            var ab = new float[7]; for (int k = 0; k < 7; k++) ab[k] = r.GetFloat();
            short w1 = r.GetWord(); float f1 = r.GetFloat(); short w2 = r.GetWord();
            Console.WriteLine($"{name,-10} {string.Join(" ", ab.Select(v => v.ToString("0.##").PadLeft(5)))}   | {w1,5} | {f1,8:0.###} | {w2,4}");
        }
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        Console.WriteLine("\nmonster stats[7], guildLevel, damageMod, size, att/def/hits:");
        foreach (var m in monsters.Where(m => m.Name is "Kobold" or "Orc" or "Giant Rat" or "Large Rat" or "Ogre" or "Asmodeus" or "Medusa" or "Vampire Bat"))
            Console.WriteLine($"  {m.Name,-14} L{m.LevelFound,2} stats [{string.Join(",", m.Stats)}] glvl {m.GuildLevel,3} dmg {m.DamageMod:0.##} size {m.Size} att {m.Att} def {m.Def} hits {m.Hits} kind {m.Kind}");
    }
}
