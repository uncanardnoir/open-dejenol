using Mordor.Core;

/// <summary>Decode MDATA1 (races, guilds) as 260-byte VB3 records using libDejenol's struct field order.</summary>
public static class GameDataProbe
{
    public static void Run(string dir)
    {
        string path = System.IO.Path.Combine(dir, "MDATA1.MDR");
        long size = new System.IO.FileInfo(path).Length;
        int recs = (int)(size / 260);
        Console.WriteLine($"MDATA1: {size} bytes = {recs} records of 260");

        // Pass 1: which records start with a plausible name?
        using (var r = new RecordReader(path, 260))
        {
            for (int i = 1; i <= recs; i++)
            {
                r.Seek(i); r.Read();
                int len = r.GetUWord();
                if (len < 2 || len > 20) continue;
                r.Seek(i); r.Read();
                string name = r.GetString();
                if (name.All(c => char.IsLetter(c) || c == ' ' || c == '\''))
                    Console.WriteLine($"  rec {i,3}: '{name}'");
            }
        }

        // Pass 2: decode race records (name, min[7], max[7], res[12], align, size, bonus, maxAge, expFactor)
        string[] races = { "Human", "Elf", "Giant", "Gnome", "Dwarf", "Ogre", "Morloch", "Osiri", "Troll" };
        Console.WriteLine("\nRACES (decoded):");
        using (var r = new RecordReader(path, 260))
        {
            for (int i = 1; i <= recs; i++)
            {
                r.Seek(i); r.Read();
                int len = r.GetUWord(); if (len < 2 || len > 20) continue;
                r.Seek(i); r.Read();
                string name = r.GetString();
                if (!races.Contains(name)) continue;
                var min = r.GetWords(7); var max = r.GetWords(7); var res = r.GetWords(12);
                int align = r.GetDword(); short sz = r.GetWord(), bonus = r.GetWord(), maxAge = r.GetWord(); float exp = r.GetFloat();
                Console.WriteLine($"  {name,-8} min {string.Join(",", min.Take(6))}  max {string.Join(",", max.Take(6))}  align 0x{align:X} size {sz} bonus {bonus} maxAge {maxAge} exp {exp}");
            }
        }

        // Pass 3: decode guild records (name, avgHits, MaxLevel, MH, expFactor, u5, reqStats[7], alignment, ...)
        string[] guilds = { "Nomad", "Warrior", "Paladin", "Ninja", "Villain", "Seeker", "Thief", "Scavenger", "Mage", "Sorcerer", "Wizard", "Healer" };
        Console.WriteLine("\nGUILDS (decoded):");
        using (var r = new RecordReader(path, 260))
        {
            for (int i = 1; i <= recs; i++)
            {
                r.Seek(i); r.Read();
                int len = r.GetUWord(); if (len < 2 || len > 20) continue;
                r.Seek(i); r.Read();
                string name = r.GetString();
                if (!guilds.Contains(name)) continue;
                short avgHits = r.GetWord(), maxLevel = r.GetWord(), mh = r.GetWord(); float expF = r.GetFloat(); short u5 = r.GetWord();
                var req = r.GetWords(7); int align = r.GetDword();
                var abil = new float[7]; for (int k = 0; k < 7; k++) abil[k] = r.GetFloat();
                short u9 = r.GetWord(); float u10 = r.GetFloat(); short quest = r.GetWord();
                for (int k = 0; k < 38; k++) r.GetFloat(); // spellstuff[19][2]
                int raceMask = r.GetDword();
                Console.WriteLine($"  rec {i,2} {name,-9} avgHits {avgHits,2} maxLvl {maxLevel,3} expF {expF,5:F2}  req STR/INT/WIS/CON/CHA/DEX {string.Join("/", req.Take(6))}  align 0x{align:X}  raceMask 0x{raceMask:X}  quest% {quest}");
            }
        }
    }
}
