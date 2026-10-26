namespace Mordor.Core;

public enum MonsterKind
{
    Humanoid, Cleanup, Demon, Devil, Elemental, Reptile, Dragon, Animal, Insect,
    Undead, Aquatic, Giant, Mythical, Lycanthrope, Thief, Mage, Warrior, Indigini
}

public enum Alignment { Good, Neutral, Evil }

/// <summary>
/// The alignment carried by one particular item. MDATA3's AlignmentFlags is a mask of which of these
/// an item can be: bit 0 unaligned, bit 1 good, bit 2 neutral, bit 3 evil. Every item the store
/// stocks permanently is unaligned (0x1); most dungeon loot is 0xE, so it turns up good, neutral or
/// evil; a few are fixed, like the Holy Sword (good only) and the Wicked Blade (evil only).
/// </summary>
public enum ItemAlignment { Unaligned = 0, Good = 1, Neutral = 2, Evil = 3 }

public static class ItemAlignments
{
    public static readonly ItemAlignment[] All = { ItemAlignment.Unaligned, ItemAlignment.Good, ItemAlignment.Neutral, ItemAlignment.Evil };

    /// <summary>The single letter the original prints in its store columns: U, G, N, E.</summary>
    public static char Letter(ItemAlignment a) => a switch
    {
        ItemAlignment.Good => 'G', ItemAlignment.Neutral => 'N', ItemAlignment.Evil => 'E', _ => 'U',
    };

    public static string Name(ItemAlignment a) => a == ItemAlignment.Unaligned ? "Unaligned" : a.ToString();

    /// <summary>
    /// Which alignments this item can come in, read from its flags: bit n of Align allows
    /// alignment n (<c>testBit(l04FE, Items(l04FA).Align)</c>, MONSMOD.BAS:3808-3810). An Align of 0
    /// or 1 gives nought, unaligned, without a draw (MONSMOD.BAS:3807), which the empty list's
    /// Unaligned and bit 0's Unaligned both answer.
    /// </summary>
    public static List<ItemAlignment> Possible(Item it)
    {
        var list = new List<ItemAlignment>();
        foreach (var a in All) if ((it.AlignmentFlags & (1 << (int)a)) != 0) list.Add(a);
        if (list.Count == 0) list.Add(ItemAlignment.Unaligned);
        return list;
    }

    /// <summary>The character alignment an item alignment matches, or null for unaligned.</summary>
    public static Alignment? AsCharacter(ItemAlignment a) => a switch
    {
        ItemAlignment.Good => Alignment.Good, ItemAlignment.Neutral => Alignment.Neutral,
        ItemAlignment.Evil => Alignment.Evil, _ => null,
    };

    /// <summary>
    /// What an item's attack or defence is actually worth to this wearer (MAINMOD2.BAS:1819-1827).
    /// The halving is a float division landing in an <c>Integer</c>, so it rounds half to even: an
    /// attack of 5 gives 2, an attack of 7 gives 4.
    /// </summary>
    public static int AttDefWorth(int raw, ItemFit fit) => fit switch
    {
        ItemFit.Fine => raw,
        ItemFit.Adjacent => (int)Math.Round(raw / 2.0, MidpointRounding.ToEven),
        _ => -raw,
    };

    /// <summary>
    /// What an item's stat modifier is worth (MAINMOD2.BAS:1848-1852). Note this one halves
    /// *inside* an <c>Int()</c>, so it floors where the attack and defence round - the two are
    /// written differently in the original and behave differently for odd numbers.
    /// </summary>
    public static int StatModWorth(int raw, ItemFit fit) => fit switch
    {
        ItemFit.Fine => raw,
        ItemFit.Adjacent => (int)Math.Floor(raw / 2.0),
        _ => -raw,
    };

    /// <summary>
    /// How an item sits with a character: unaligned suits everyone, a match is fine, good against
    /// evil (or the reverse) is an opposed item, and one step either way is an adjacent one.
    /// </summary>
    public static ItemFit FitFor(ItemAlignment item, Alignment who)
    {
        var a = AsCharacter(item);
        if (a == null || a == who) return ItemFit.Fine;
        return Party.Opposed(a.Value, who) ? ItemFit.Opposed : ItemFit.Adjacent;
    }
}

/// <summary>
/// How an item's alignment sits against its wearer's, and what that costs. <c>sub0F74</c>
/// (MAINMOD2.BAS:1817-1828) splits three ways rather than two, and the middle case is the one the
/// port had wrong: an item one step away is not useless, it is worth *half*.
///
/// Fine: unaligned, or the same alignment as you - full attack, defence and stat modifiers.
/// Adjacent: one step away - half of each.
/// Opposed: two steps away - the attack and defence are applied **negative**, the stat modifiers
/// likewise, and the item curses itself onto you so it cannot be taken off.
/// </summary>
public enum ItemFit { Fine, Adjacent, Opposed }


/// <summary>The four levels of identification the original uses, in order.</summary>
public enum IdLevel { Barely, Partial, Mostly, All }

public sealed class Spell
{
    public string Name { get; set; } = "";
    public short Id { get; set; }
    public short Category { get; set; }
    public short Level { get; set; }
    public short U4 { get; set; }
    public short KillEffect { get; set; }
    public short AffectMonster { get; set; }
    public short AffectGroup { get; set; }
    public short Damage1 { get; set; }
    public short Damage2 { get; set; }
    public short SpecialEffect { get; set; }
    /// <summary>
    /// What the spell does: the effect id MAINMOD2.BAS dispatches on (its `Ailments` field).
    /// 200 or more means "cast on the whole party", with the real effect 200 lower.
    /// See <see cref="SpellEffect"/>.
    /// </summary>
    public short Effect { get; set; }
    public short[] Required { get; set; } = new short[7];
    /// <summary>M5B5B, the last field: which resistance (0..10) the target's percentage comes from, or -1 for none.</summary>
    public short ResistIndex { get; set; } = -1;
}

public sealed class Item
{
    public string Name { get; set; } = "";
    public short Id { get; set; }
    public short Att { get; set; }
    public short Def { get; set; }
    public int Price { get; set; }
    public short Floor { get; set; }
    public short Rarity { get; set; }
    public int Abilities { get; set; }
    public short Swings { get; set; }
    public short SpecialType { get; set; }
    public short SpellIndex { get; set; }
    public short SpellId { get; set; }
    public int Charges { get; set; }
    public int Guilds { get; set; }
    public short LevelScale { get; set; }
    public float DamageMod { get; set; }
    public int AlignmentFlags { get; set; }
    public short NHands { get; set; }
    public short Type { get; set; }
    public int ResistanceFlags { get; set; }
    public short[] StatsRequired { get; set; } = new short[7];
    public short[] StatsMod { get; set; } = new short[7];
    public short Cursed { get; set; }
    public short SpellLvl { get; set; }
    public short ClassRestricted { get; set; }
}

public sealed class Monster
{
    /// <summary>
    /// The creature's index in the monster table, <c>Monsters(i)</c>: what the original keeps as a
    /// companion's <c>Ref</c> (fn1163, MAINMOD2.BAS:388) and indexes the Confinement pens by
    /// (<c>gv162E(Ref)</c>, FRMCITYC.BAS:288-291; sub054E, MAINMOD.BAS:542-557). Unlike
    /// <see cref="Id"/> it is one creature: MDATA5 gives Id 307 to both the Morgul (index 278) and
    /// the Shadow Ninja (389). Set by <see cref="MdrLoader.LoadMonsters"/>; -1 for a creature built
    /// by hand, which then answers to its place in whatever list holds it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Ref { get; set; } = -1;

    public string Name { get; set; } = "";
    public short Att { get; set; }
    public short Def { get; set; }
    public short Id { get; set; }
    public short Hits { get; set; }
    public short NumGroups { get; set; }
    public short PicId { get; set; }
    public short LockedChance { get; set; }
    public short LevelFound { get; set; }
    public short[] Resistances { get; set; } = new short[12];
    public int SpecialPropertyFlags { get; set; }
    public int SpecialAttackFlags { get; set; }
    public int SpellFlags { get; set; }
    public short Chance { get; set; }
    public short[] BoxChance { get; set; } = new short[4];
    public short AlignmentValue { get; set; }
    public short InGroup { get; set; }
    public int GoldFactor { get; set; }
    public int TrapFlags { get; set; }
    public short GuildLevel { get; set; }
    public short[] Stats { get; set; } = new short[7];
    public short Type { get; set; }
    public float DamageMod { get; set; }
    public short CompanionType { get; set; }
    public short CompanionSpawnMode { get; set; }
    public short CompanionId { get; set; }
    public short[] Items { get; set; } = new short[11];
    public short Subtype { get; set; }
    public short CompanionSubtype { get; set; }
    public short Size { get; set; }
    public short ItemDropLevel { get; set; }
    public short ItemChance { get; set; }

    public MonsterKind Kind => (MonsterKind)Type;
}

/// <summary>One dungeon level (30x30 grid of fields plus areas, chutes, teleporters).</summary>
public sealed class Floor
{
    // bit positions within Field.Flags
    public const int WALL_EAST = 0, WALL_NORTH = 1, DOOR_EAST = 2, DOOR_NORTH = 3,
        SECRET_DOOR_EAST = 4, SECRET_DOOR_NORTH = 5, FACE_NORTH = 6, FACE_EAST = 7,
        FACE_SOUTH = 8, FACE_WEST = 9, EXTINGUISHER = 10, PIT = 11, STAIRS_UP = 12,
        STAIRS_DOWN = 13, TELEPORTER = 14, WATER = 15, QUICKSAND = 16, ROTATOR = 17,
        ANTIMAGIC = 18, ROCK = 19, FOG = 20, CHUTE = 21, STUD = 22, RESCUE = 23;

    /// <summary>
    /// Bit 23 has no name in the community's MDATA11 notes, which stop at Stud. The only thing the
    /// original ever does with it is double your chance of being found while you wait for rescue
    /// (FRMDUNM.BAS:153-156), so it is named for that and for nothing more. The port had it down
    /// as "explored" and never read it.
    /// </summary>

    public struct Field { public short AreaId; public long Flags; }
    public struct Area { public int SpawnMask; public short LairId; }
    public struct Chute { public short X, Y, Depth; }
    public struct Teleporter { public short X, Y, X2, Y2, Z2; }

    public short Width, Height, LevelNum;
    public Field[] Fields = System.Array.Empty<Field>();
    public List<Area> Areas = new();
    public List<Teleporter> Teleporters = new();
    public List<Chute> Chutes = new();

    public Field At(int x, int y) => Fields[y * Width + x];
    public bool Has(int x, int y, int bit) => (At(x, y).Flags & (1L << bit)) != 0;

    /// <summary>
    /// The spawn area covering a cell, or null if the cell belongs to none. Area.SpawnMask is a
    /// bitmask of MonsterKind (bit 0 Humanoid, 1 Cleanup, 2 Demon …); LairId names a monster that is
    /// far more likely to appear there (MDATA11 file format page).
    ///
    /// An area id indexes the 201 area records directly. MONSMOD.BAS:1435 reads the record at
    /// <c>levelHeader + 2 + (width * height) + areaId</c>, and the area records begin at
    /// <c>levelHeader + 2 + (width * height)</c> - one past the area header, which is one past the
    /// last field record. The teleporter read at MAZEMOD.BAS:1787 confirms the layout from the
    /// other side: it starts at <c>levelHeader + 4 + (width * height) + 200</c>, which is exactly
    /// where 201 area records and a header leave off. So id 0 is the first record, the one that
    /// means "no spawn area" and that the rock cells carry.
    /// </summary>
    public Area? AreaAt(int x, int y)
    {
        int id = At(x, y).AreaId;
        return id >= 1 && id < Areas.Count ? Areas[id] : null;
    }

    public int AreaIdAt(int x, int y) => At(x, y).AreaId;

    /// <summary>How many spawn areas this level actually uses (the respawn formula needs it).</summary>
    public int AreasInUse
    {
        get
        {
            if (_areasInUse >= 0) return _areasInUse;
            var seen = new HashSet<int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (At(x, y).AreaId >= 1) seen.Add(At(x, y).AreaId);
            return _areasInUse = seen.Count;
        }
    }
    private int _areasInUse = -1;

    /// <summary>
    /// The level header's area count, <c>gv1864.M5CF4</c> (T5CE8, the fourth word of the header).
    /// sub04A3 seeds each level's respawn tally with it (MAINMOD.BAS:826-829), and sub0A91's
    /// respawn time subtracts five seconds for every area short of it (MONSMOD.BAS:699).
    /// </summary>
    public int HeaderAreaCount;
}

public sealed class LibraryRecord
{
    public short Id { get; set; }
    public short Known { get; set; }
    public long NSeen { get; set; }
    public string Name { get; set; } = "";
    public short MonId { get; set; }
    public int LastSeen { get; set; }
    public string Location { get; set; } = "";
    public short NDeaths { get; set; }
}

public sealed class Library
{
    public short MonstersFound, ItemsFound;
    public List<LibraryRecord> Monsters = new();
    public List<LibraryRecord> Items = new();
}

public sealed class HallRecord
{
    public long Value { get; set; }
    public string Name { get; set; } = "";
    public int Date { get; set; }
}
