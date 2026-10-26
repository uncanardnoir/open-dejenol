namespace Mordor.Core;

/// <summary>
/// Loads Mordor's .MDR data files. Faithful port of libDejenol's MLoader.cpp.
/// File map: MDATA2=spells, 3=items, 5=monsters, 8=automap, 9=guildlogs,
/// 11=dungeon, 13=library, 14=hall of records.
/// </summary>
public static class MdrLoader
{
    public static List<Spell> LoadSpells(string path)
    {
        using var r = new RecordReader(path, 75);
        var spells = new List<Spell>();
        r.Read(); r.GetString();            // version
        r.Read(); int n = r.GetUWord();     // count
        for (int i = 0; i < n; i++)
        {
            r.Read();
            var s = new Spell
            {
                Name = r.GetString(),
                Id = r.GetWord(),
                Category = r.GetWord(),
                Level = r.GetWord(),
                U4 = r.GetWord(),
                KillEffect = r.GetWord(),
                AffectMonster = r.GetWord(),
                AffectGroup = r.GetWord(),
                Damage1 = r.GetWord(),
                Damage2 = r.GetWord(),
                SpecialEffect = r.GetWord(),
            };
            // The record is Name, Id, Kind, Level, M556D, M5B0A, M5B12, M5B1D, M5B28, M5B33,
            // M5B41, Ailments, M5B4F(6), M5B5B (MAIN.TXT:403-417). Ailments is the effect id and
            // comes before the stat requirements, not after them.
            s.Effect = r.GetWord();
            s.Required = r.GetWords(7);
            s.ResistIndex = r.GetWord();
            spells.Add(s);
        }
        return spells;
    }

    public static List<Item> LoadItems(string path)
    {
        using var r = new RecordReader(path, 125);
        var items = new List<Item>();
        r.Read(); r.GetString();            // version
        r.Read(); r.GetUWord();             // store code
        r.Read(); int n = r.GetUWord();     // count
        for (int i = 0; i < n; i++)
        {
            r.Read();
            var it = new Item
            {
                Name = r.GetString(),
                Id = r.GetWord(),
                Att = r.GetWord(),
                Def = r.GetWord(),
                Price = r.GetDword(),
                Floor = r.GetWord(),
                Rarity = r.GetWord(),
                Abilities = r.GetDword(),
                Swings = r.GetWord(),
                SpecialType = r.GetWord(),
                SpellIndex = r.GetWord(),
                SpellId = r.GetWord(),
                Charges = r.GetDword(),
                Guilds = r.GetDword(),
                LevelScale = r.GetWord(),
                DamageMod = r.GetFloat(),
                AlignmentFlags = r.GetDword(),
                NHands = r.GetWord(),
                Type = r.GetWord(),
                ResistanceFlags = r.GetDword(),
            };
            it.StatsRequired = r.GetWords(7);
            it.StatsMod = r.GetWords(7);
            it.Cursed = r.GetWord();
            it.SpellLvl = r.GetWord();
            it.ClassRestricted = r.GetWord();
            items.Add(it);
        }
        return items;
    }

    public static List<Monster> LoadMonsters(string path)
    {
        using var r = new RecordReader(path, 160);
        var monsters = new List<Monster>();
        r.Read(); r.GetString();            // version
        r.Read();                           // (unused record)
        r.Read(); int n = r.GetUWord();     // count
        for (int i = 0; i < n; i++)
        {
            r.Read();
            var m = new Monster
            {
                Name = r.GetString(),
                Att = r.GetWord(),
                Def = r.GetWord(),
                Id = r.GetWord(),
                Hits = r.GetWord(),
                NumGroups = r.GetWord(),
                PicId = r.GetWord(),
                LockedChance = r.GetWord(),
                LevelFound = r.GetWord(),
            };
            m.Resistances = r.GetWords(12);
            m.SpecialPropertyFlags = r.GetDword();
            m.SpecialAttackFlags = r.GetDword();
            m.SpellFlags = r.GetDword();
            m.Chance = r.GetWord();
            m.BoxChance = r.GetWords(4);
            m.AlignmentValue = r.GetWord();
            m.InGroup = r.GetWord();
            m.GoldFactor = r.GetDword();
            m.TrapFlags = r.GetDword();
            m.GuildLevel = r.GetWord();
            m.Stats = r.GetWords(7);
            m.Type = r.GetWord();
            m.DamageMod = r.GetFloat();
            m.CompanionType = r.GetWord();
            m.CompanionSpawnMode = r.GetWord();
            m.CompanionId = r.GetWord();
            m.Items = r.GetWords(11);
            m.Subtype = r.GetWord();
            m.CompanionSubtype = r.GetWord();
            m.Size = r.GetWord();
            m.ItemDropLevel = r.GetWord();
            m.ItemChance = r.GetWord();
            m.Ref = i;                      // Monsters(i): table index i is record i + 4 of the file
            monsters.Add(m);
        }
        return monsters;
    }

    public static List<Floor> LoadDungeon(string path)
    {
        using var r = new RecordReader(path, 20);
        r.Read(); int numLevels = r.GetUWord();
        var offsets = new int[numLevels];
        for (int i = 0; i < numLevels; i++) { r.Read(); offsets[i] = r.GetUWord(); }

        var dungeon = new List<Floor>(numLevels);
        for (int i = 0; i < numLevels; i++)
        {
            r.Seek(offsets[i]);
            dungeon.Add(LoadFloor(r));
        }
        return dungeon;
    }

    private static Floor LoadFloor(RecordReader r)
    {
        var f = new Floor();
        r.Read();
        f.Width = r.GetWord();
        f.Height = r.GetWord();
        f.LevelNum = r.GetWord();
        int numAreas = r.GetUWord();
        int numChutes = r.GetUWord();
        int numTeleporters = r.GetUWord();
        f.HeaderAreaCount = numAreas;

        int cells = f.Width * f.Height;
        f.Fields = new Floor.Field[cells];
        for (int i = 0; i < cells; i++)
        {
            r.Read();
            f.Fields[i] = new Floor.Field { AreaId = r.GetWord(), Flags = r.GetIntCurrency() };
        }

        r.Read(); r.GetWord(); // area count (== numAreas)
        for (int i = 0; i < 201; i++)
        {
            r.Read();
            f.Areas.Add(new Floor.Area { SpawnMask = r.GetDword(), LairId = r.GetWord() });
        }

        r.Read(); r.GetWord(); // teleporter count
        for (int i = 0; i < numTeleporters; i++)
        {
            r.Read();
            var t = new Floor.Teleporter { X = r.GetWord(), Y = r.GetWord(), X2 = r.GetWord(), Y2 = r.GetWord(), Z2 = r.GetWord() };
            if (t.X > 0 && t.Y > 0) f.Teleporters.Add(t);
        }

        r.Read(); r.GetWord(); // chute count
        for (int i = 0; i < numChutes; i++)
        {
            r.Read();
            var c = new Floor.Chute { X = r.GetWord(), Y = r.GetWord(), Depth = r.GetWord() };
            if (c.X > 0 && c.Y > 0) f.Chutes.Add(c);
        }
        return f;
    }

    public static Library LoadLibrary(string path)
    {
        using var r = new RecordReader(path, 100);
        var lib = new Library();
        r.Read(); r.GetString();                 // version
        r.Read(); int numMonsters = r.GetWord();
        r.Read(); int numItems = r.GetWord();
        r.Read(); lib.MonstersFound = r.GetWord();
        r.Read(); lib.ItemsFound = r.GetWord();
        for (int i = 0; i < numMonsters; i++) { r.Read(); lib.Monsters.Add(ReadLibRecord(r)); }
        for (int i = 0; i < numItems; i++) { r.Read(); lib.Items.Add(ReadLibRecord(r)); }
        return lib;
    }

    private static LibraryRecord ReadLibRecord(RecordReader r) => new()
    {
        Id = r.GetWord(),
        Known = r.GetWord(),
        NSeen = r.GetCurrency(),
        Name = r.GetString(30).TrimEnd('\0', ' '),
        MonId = r.GetWord(),
        LastSeen = r.GetDword(),
        Location = r.GetString(8).TrimEnd('\0', ' '),
        NDeaths = r.GetWord(),
    };

    // ---- MDATA1: game rules (races, guilds). 260-byte records; races come first, then guilds. ----

    // Every record in the two ranges is kept, as fn03D0 keeps them: it Gets NumRaces records into
    // Races(0..) and NumGuilds into GuildDefs(0..) with no test on any of them (MODULE1.BAS:693-700).
    // Dropping one would shift every index after it, and a guild's RaceMask bits are race indexes.

    public static List<Race> LoadRaces(string path)
    {
        var races = new List<Race>();
        using var r = new RecordReader(path, Mdata1.RecordLength);
        var at = Mdata1.Read(r).Races;      // exact, from fn03D0's own read order
        for (int i = at.First; i < at.First + at.Count; i++)
        {
            r.Seek(i); r.Read();
            string name = r.GetString();
            var min = r.GetWords(7); var max = r.GetWords(7); var res = r.GetWords(12);
            int align = r.GetDword(); short size = r.GetWord(), bonus = r.GetWord(), maxAge = r.GetWord(); float exp = r.GetFloat();
            races.Add(new Race
            {
                Name = name, Min = min.Take(6).Select(v => (int)v).ToArray(), Max = max.Take(6).Select(v => (int)v).ToArray(),
                Size = (RaceSize)size,   // as the record has it

                AlignMask = align, BonusPoints = bonus, MaxAge = maxAge, ExpFactor = exp,
                Resistances = res.Take(Effects.ResistanceKinds).Select(v => (int)v).ToArray(),
            });
        }
        return races;
    }

    public static List<GuildDef> LoadGuilds(string path)
    {
        var guilds = new List<GuildDef>();
        using var r = new RecordReader(path, Mdata1.RecordLength);
        var at = Mdata1.Read(r).Guilds;     // exact, from fn03D0's own read order
        for (int i = at.First; i < at.First + at.Count; i++)
        {
            r.Seek(i); r.Read();
            string name = r.GetString();
            // AH (hits per level), ML (level after which only MH hits are given), MH, EP (experience parameter)
            short avgHits = r.GetWord(), maxLevel = r.GetWord(), lateHits = r.GetWord(); float expF = r.GetFloat(); r.GetWord();
            var req = r.GetWords(7); int align = r.GetDword();
            // ability rates, in the help's order: thieving, backstab, critical hit, extra swings, fighting (/10), perception, (unused)
            var abilities = new float[7];
            for (int k = 0; k < 7; k++) abilities[k] = r.GetFloat();
            r.GetWord(); r.GetFloat(); short quest = r.GetWord();
            // spell table: 19 categories × (guild levels per spell level), then 19 × (max spell level)
            var rate = new float[19]; var cap = new int[19];
            for (int k = 0; k < 19; k++) rate[k] = r.GetFloat();
            for (int k = 0; k < 19; k++) cap[k] = (int)r.GetFloat();
            int raceMask = r.GetDword();
            // T5584 continues past the race mask: Gold (the guild factor), an unknown Single,
            // then Att and Def - the fractional A/D a level in this guild is worth.
            short goldFactor = r.GetWord(); float u16 = r.GetFloat();
            float attPerLevel = r.GetFloat(), defPerLevel = r.GetFloat();
            // fields 19 and 20: the level past which no more A/D is granted, and the
            // interval - FRMCTYG only awards A/D when level Mod Int(interval) is zero.
            short adMaxLevel = r.GetWord(); float adEvery = r.GetFloat();
            // AH, MH and EP go in as the record has them: GuildDefs is filled by a plain Get
            // (MODULE1.BAS:697-700) and nothing on the way in bounds them
            guilds.Add(new GuildDef
            {
                Name = name, MaxLevel = 999, HitsCapLevel = maxLevel, LateHits = lateHits,
                ReqStats = req.Take(6).Select(v => (int)v).ToArray(), AlignMask = align,
                RaceMask = raceMask, AvgHits = avgHits, ExpFactor = expF, QuestPercent = quest,
                SpellRate = rate, SpellMax = cap, Abilities = abilities,
                GoldFactor = goldFactor, AttPerLevel = attPerLevel, DefPerLevel = defPerLevel, ItemUseFactor = u16,
                AdMaxLevel = adMaxLevel, AdEvery = adEvery,
            });
        }
        return guilds;
    }

    public static List<HallRecord> LoadHallRecords(string path)
    {
        using var r = new RecordReader(path, 45);
        var records = new List<HallRecord>();
        for (int i = 0; i < 13; i++)
        {
            r.Read();
            records.Add(new HallRecord
            {
                Value = r.GetCurrency(),
                Name = r.GetString(30).TrimEnd('\0', ' '),
                Date = r.GetDword(),
            });
        }
        return records;
    }
}
