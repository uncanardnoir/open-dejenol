namespace Mordor.Core;

/// <summary>One of MDATA1's four little name tables: a name and one index.</summary>
public readonly record struct NamedIndex(string Name, int Index);

/// <summary>
/// MDATA1's layout, exactly as <c>fn03D0</c> reads it (MODULE1.BAS:678-716). It opens the file
/// with a record length of 260 and then reads straight through with record-less Gets, so nothing
/// here is a guess about where a table starts:
///
/// <code>
///   record 1      version string
///   records 2-7   six counts: races, guilds, item types, item descriptions,
///                 monster classes, monster groups
///   then          that many of each, in that order, one per record
/// </code>
///
/// The port used to find races and guilds by scanning record ranges and keeping whatever looked
/// plausible. That worked, but it could not find the four tables after them - which is where the
/// names for an unidentified item and an unidentified monster live.
/// </summary>
public static class Mdata1
{
    public const int RecordLength = 260;

    /// <summary>Where each table begins (1-based record numbers) and how many entries it has.</summary>
    public sealed class Layout
    {
        public string Version = "";
        public (int First, int Count) Races, Guilds, ItemTypes, ItemDescriptions, MonsterClasses, MonsterGroups;
    }

    public static Layout Read(RecordReader r)
    {
        var l = new Layout();
        r.Seek(1); r.Read();
        l.Version = r.GetString();

        var counts = new int[6];
        for (int i = 0; i < 6; i++) { r.Seek(2 + i); r.Read(); counts[i] = r.GetUWord(); }

        int rec = 8;
        (int, int) Take(int n) { var at = (rec, n); rec += n; return at; }
        l.Races = Take(counts[0]);
        l.Guilds = Take(counts[1]);
        l.ItemTypes = Take(counts[2]);
        l.ItemDescriptions = Take(counts[3]);
        l.MonsterClasses = Take(counts[4]);
        l.MonsterGroups = Take(counts[5]);
        return l;
    }

    private static List<NamedIndex> ReadTable(RecordReader r, (int First, int Count) at)
    {
        var list = new List<NamedIndex>(at.Count);
        for (int i = 0; i < at.Count; i++)
        {
            r.Seek(at.First + i); r.Read();
            string name = r.GetString();
            list.Add(new NamedIndex(name, r.GetWord()));
        }
        return list;
    }

    // ---- the four name tables, loaded once ----

    /// <summary>gv1592: what an item is - Dagger, Sword, Potion, Guild Crest. Points into <see cref="ItemDescriptions"/>.</summary>
    public static IReadOnlyList<NamedIndex> ItemTypes { get; private set; } = Array.Empty<NamedIndex>();

    /// <summary>gv15C6: the vaguer word for it - Weapon, Liquid, Parchment, Round Object.</summary>
    public static IReadOnlyList<NamedIndex> ItemDescriptions { get; private set; } = Array.Empty<NamedIndex>();

    /// <summary>gv1662: a monster's class - Humanoid, Undead, Dragon, Thief. Points into <see cref="MonsterGroups"/>.</summary>
    public static IReadOnlyList<NamedIndex> MonsterClasses { get; private set; } = Array.Empty<NamedIndex>();

    /// <summary>gv1696: the broader group above that.</summary>
    public static IReadOnlyList<NamedIndex> MonsterGroups { get; private set; } = Array.Empty<NamedIndex>();

    public static bool Loaded { get; private set; }

    public static void LoadFrom(string gameDir)
    {
        string path = Path.Combine(gameDir, "MDATA1.MDR");
        if (!File.Exists(path)) return;
        try
        {
            using var r = new RecordReader(path, RecordLength);
            var l = Read(r);
            ItemTypes = ReadTable(r, l.ItemTypes);
            ItemDescriptions = ReadTable(r, l.ItemDescriptions);
            MonsterClasses = ReadTable(r, l.MonsterClasses);
            MonsterGroups = ReadTable(r, l.MonsterGroups);
            Loaded = ItemTypes.Count > 0;
        }
        catch { /* leave the tables empty; callers fall back to the item's own name */ }
    }

    private static string At(IReadOnlyList<NamedIndex> table, int i, string fallback) =>
        i >= 0 && i < table.Count ? table[i].Name : fallback;

    /// <summary>The item type's name, e.g. "Sword" for a Holy Sword.</summary>
    public static string ItemTypeName(int kind, string fallback = "Item") => At(ItemTypes, kind, fallback);

    /// <summary>The vaguer description, e.g. "Weapon" - what you see when barely identified.</summary>
    public static string ItemDescriptionName(int kind, string fallback = "Unidentified Item") =>
        kind >= 0 && kind < ItemTypes.Count ? At(ItemDescriptions, ItemTypes[kind].Index, fallback) : fallback;

    /// <summary>The monster's class, e.g. "Undead".</summary>
    public static string MonsterClassName(int cls, string fallback = "Monster") => At(MonsterClasses, cls, fallback);

    /// <summary>The broader group the class belongs to.</summary>
    public static string MonsterGroupName(int cls, string fallback = "Monster") =>
        cls >= 0 && cls < MonsterClasses.Count ? At(MonsterGroups, MonsterClasses[cls].Index, fallback) : fallback;
}
