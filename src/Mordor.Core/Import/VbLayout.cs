using System.Buffers.Binary;
using System.Text;

namespace Mordor.Core.Import;

/// <summary>The VB3 types a record field can have, as a random-access file stores them.</summary>
public enum VbKind { Integer, Long, Single, Double, Currency, FixedString, Udt }

/// <summary>
/// One field of a VB3 user-defined type: its name as the decompiled MAIN.TXT spells it, its type,
/// and its array bound. VB3 arrays start at 0 (the source has no Option Base), so <c>Stats(6)</c>
/// is seven Integers and <c>Pack(40)</c> forty-one records.
/// </summary>
public sealed class VbField
{
    public string Name { get; }
    public VbKind Kind { get; }
    /// <summary>The declared upper bound, or -1 for a field that is not an array.</summary>
    public int Upper { get; }
    /// <summary>The n of <c>String * n</c>.</summary>
    public int StringLength { get; }
    /// <summary>The nested type of a <see cref="VbKind.Udt"/> field.</summary>
    public VbType? Type { get; }
    /// <summary>Where the field starts in its record, in bytes.</summary>
    public int Offset { get; internal set; }

    public VbField(string name, VbKind kind, int upper = -1, int stringLength = 0, VbType? type = null)
    {
        Name = name; Kind = kind; Upper = upper; StringLength = stringLength; Type = type;
    }

    public int Count => Upper < 0 ? 1 : Upper + 1;

    public int ElementSize => Kind switch
    {
        VbKind.Integer => 2,
        VbKind.Long => 4,
        VbKind.Single => 4,
        VbKind.Double => 8,
        VbKind.Currency => 8,
        VbKind.FixedString => StringLength,
        _ => Type!.Size,
    };

    public int Size => Count * ElementSize;

    /// <summary>The declaration as MAIN.TXT writes it, for messages: <c>Pack(40) As T576E</c>.</summary>
    public override string ToString() =>
        Name + (Upper >= 0 ? $"({Upper})" : "") + " As " + Kind switch
        {
            VbKind.FixedString => $"String * {StringLength}",
            VbKind.Udt => Type!.Name,
            _ => Kind.ToString(),
        };
}

/// <summary>
/// A VB3 user-defined type laid out as <c>Put</c> writes it to a random-access file: the fields one
/// after another with nothing between them (VB3 packs a record on disk; there is no alignment),
/// Integers 2 bytes, Long and Single 4, Double and Currency 8, a fixed string its length, all little
/// endian. A record is written at <c>(n - 1) * Len</c> and is as long as the type, so the bytes
/// between the type's end and the next record are whatever was there before - and the last record
/// of a file stops at the type's end, which is how a file's size gives the type's size away.
/// </summary>
public sealed class VbType
{
    public string Name { get; }
    public IReadOnlyList<VbField> Fields { get; }
    public int Size { get; }

    public VbType(string name, params VbField[] fields)
    {
        Name = name;
        Fields = fields;
        int at = 0;
        foreach (var f in fields) { f.Offset = at; at += f.Size; }
        Size = at;
    }

    public VbField Field(string name) =>
        Fields.FirstOrDefault(f => f.Name == name) ?? throw new ArgumentException($"{Name} has no field {name}");

    /// <summary>
    /// The byte offset of a path into the record - <c>"GuildsJoined"</c>, <c>"GuildsJoined(3).Level"</c>,
    /// <c>"Pack(7).Id"</c> - and the field it ends on. An index past the declared bound throws.
    /// </summary>
    public (int Offset, VbField Field) Locate(string path)
    {
        var type = this;
        int offset = 0;
        VbField? last = null;
        foreach (string part in path.Split('.'))
        {
            if (type == null) throw new ArgumentException($"{path}: {last!.Name} is not a record");
            string name = part;
            int index = 0;
            int paren = part.IndexOf('(');
            if (paren >= 0)
            {
                name = part[..paren];
                index = int.Parse(part[(paren + 1)..part.IndexOf(')')], System.Globalization.CultureInfo.InvariantCulture);
            }
            var f = type.Field(name);
            if (index < 0 || index >= f.Count) throw new ArgumentOutOfRangeException(nameof(path), $"{path}: {f.Name} is declared ({f.Upper})");
            offset += f.Offset + index * f.ElementSize;
            last = f;
            type = f.Type;
        }
        return (offset, last!);
    }

    public int OffsetOf(string path) => Locate(path).Offset;
}

/// <summary>
/// One record of a file read through its type. Bytes past the end of the buffer read as zero,
/// which is what the original sees when it <c>Get</c>s the short last record of a file.
/// </summary>
public readonly struct VbRecord
{
    private readonly byte[] _data;
    private readonly int _base;
    public VbType Type { get; }

    public VbRecord(byte[] data, VbType type, int @base = 0) { _data = data; Type = type; _base = @base; }

    private ReadOnlySpan<byte> Bytes(int offset, int length)
    {
        int at = _base + offset;
        if (at + length <= _data.Length) return _data.AsSpan(at, length);
        var padded = new byte[length];
        if (at < _data.Length) _data.AsSpan(at, _data.Length - at).CopyTo(padded);
        return padded;
    }

    private ReadOnlySpan<byte> At(string path, VbKind want)
    {
        var (offset, field) = Type.Locate(path);
        if (field.Kind != want) throw new InvalidOperationException($"{Type.Name}.{path} is {field.Kind}, read as {want}");
        return Bytes(offset, field.ElementSize);
    }

    public short Integer(string path) => BinaryPrimitives.ReadInt16LittleEndian(At(path, VbKind.Integer));
    public int Long(string path) => BinaryPrimitives.ReadInt32LittleEndian(At(path, VbKind.Long));
    public float Single(string path) => BinaryPrimitives.ReadSingleLittleEndian(At(path, VbKind.Single));
    public double Double(string path) => BinaryPrimitives.ReadDoubleLittleEndian(At(path, VbKind.Double));
    /// <summary>A Currency's ten-thousandths, as <see cref="VbCurrency.FromRaw"/> takes them.</summary>
    public VbCurrency Currency(string path) => VbCurrency.FromRaw(BinaryPrimitives.ReadInt64LittleEndian(At(path, VbKind.Currency)));
    /// <summary>A fixed-length string exactly as stored, padding and all (VB pads with spaces).</summary>
    public string FixedString(string path) => VbFile.Text(At(path, VbKind.FixedString));

    /// <summary>A nested record: <c>Sub("Pack(3)")</c>.</summary>
    public VbRecord Sub(string path)
    {
        var (offset, field) = Type.Locate(path);
        if (field.Kind != VbKind.Udt) throw new InvalidOperationException($"{Type.Name}.{path} is not a record");
        return new VbRecord(_data, field.Type!, _base + offset);
    }

    /// <summary>Whether every byte of the record is zero - a record the original never wrote to.</summary>
    public bool IsZero
    {
        get
        {
            foreach (byte b in Bytes(0, Type.Size)) if (b != 0) return false;
            return true;
        }
    }
}

/// <summary>
/// A VB3 random-access file, read whole and never written: record n (from 1) is the <c>Len</c>
/// bytes at <c>(n - 1) * Len</c>, the last one possibly short.
/// </summary>
public sealed class VbFile
{
    public string Path { get; }
    public int RecordLength { get; }
    public byte[] Data { get; }

    private VbFile(string path, int recordLength, byte[] data) { Path = path; RecordLength = recordLength; Data = data; }

    /// <summary>The file, or null when there is none. Opened for reading only.</summary>
    public static VbFile? Open(string path, int recordLength)
    {
        if (!File.Exists(path)) return null;
        byte[] data;
        using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            data = new byte[s.Length];
            int got = 0;
            while (got < data.Length)
            {
                int n = s.Read(data, got, data.Length - got);
                if (n == 0) break;
                got += n;
            }
        }
        return new VbFile(path, recordLength, data);
    }

    public long Length => Data.Length;

    /// <summary>How many records the file holds, the last one counted however short it is.</summary>
    public int RecordCount => (int)((Data.Length + RecordLength - 1) / RecordLength);

    /// <summary>The length of the last record: <c>Len</c> for a file that ends on a boundary, 0 for an empty file.</summary>
    public int LastRecordLength => Data.Length == 0 ? 0 : (int)(Data.Length - (long)(RecordCount - 1) * RecordLength);

    /// <summary>Record n (from 1), <c>Len</c> bytes, zero past the end of the file.</summary>
    public byte[] Record(int n)
    {
        var r = new byte[RecordLength];
        long at = (long)(n - 1) * RecordLength;
        if (n >= 1 && at < Data.Length) Array.Copy(Data, at, r, 0, (int)Math.Min(RecordLength, Data.Length - at));
        return r;
    }

    public VbRecord Record(int n, VbType type) => new(Record(n), type);

    public bool Has(int n) => n >= 1 && (long)(n - 1) * RecordLength < Data.Length;

    public short Integer(int n) => BinaryPrimitives.ReadInt16LittleEndian(Record(n));

    /// <summary>
    /// A variable-length String as <c>Put</c> writes it alone or at the head of a record: a 2-byte
    /// length and the characters (the version stamp in record 1, a party member's name).
    /// </summary>
    public string VarString(int n) => VarString(Record(n), 0, out _);

    public static string VarString(byte[] record, int at, out int next)
    {
        int len = at + 2 <= record.Length ? BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(at)) : 0;
        len = Math.Max(0, Math.Min(len, record.Length - at - 2));
        next = at + 2 + len;
        return Text(record.AsSpan(at + 2, len));
    }

    /// <summary>The 1995 game's strings are the Windows ANSI code page; its names are plain ASCII.</summary>
    public static string Text(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>A fixed string as the game compares it, <c>RTrim</c>: trailing spaces (and NULs, from a record never written) off.</summary>
    public static string RTrim(string s) => s.TrimEnd(' ', '\0');
}

/// <summary>
/// The record types of the original's save files, declared field for field as the decompiled
/// MAIN.TXT declares them (its names, its order, its bounds), and the <c>Len</c> each file is
/// opened with. Selfcheck's "importing the original's saves" section reads MAIN.TXT and the
/// <c>Open ... Len =</c> lines of every module and holds these to them.
/// </summary>
public static class VbLayout
{
    private static VbField I(string n, int upper = -1) => new(n, VbKind.Integer, upper);
    private static VbField L(string n, int upper = -1) => new(n, VbKind.Long, upper);
    private static VbField S(string n, int upper = -1) => new(n, VbKind.Single, upper);
    private static VbField C(string n, int upper = -1) => new(n, VbKind.Currency, upper);
    private static VbField F(string n, int length) => new(n, VbKind.FixedString, -1, length);
    private static VbField U(string n, VbType t, int upper = -1) => new(n, VbKind.Udt, upper, 0, t);

    /// <summary>T576E (MAIN.TXT): a pack or bank slot.</summary>
    public static readonly VbType Slot = new("T576E",
        I("Att"), I("Def"), I("Ref"), I("Id"), I("Alignment"), I("Charges"), I("Equipped"), I("IdLevel"), I("Cursed"));

    /// <summary>T578A: one guild of the sixteen a character can belong to.</summary>
    public static readonly VbType Guild = new("T578A",
        I("Level"), C("M579B"), I("M55CB"), I("M57A9"), I("M57B2"), S("Att"), S("Def"));

    /// <summary>T57BF: a companion slot.</summary>
    public static readonly VbType Companion = new("T57BF",
        F("Name", 15), I("Ref"), I("M57D4"), I("Hits"), I("MaxHits"), I("Alignment"), I("Att"), I("Def"),
        I("BindingLevel"), I("IdLevel"));

    /// <summary>T57EF: a window's placement, in twips.</summary>
    public static readonly VbType Window = new("T57EF",
        L("M425F"), L("M4258"), L("M4216"), L("M420C"), I("M5801"));

    /// <summary>T4E6B: the Set Sanctuary marker.</summary>
    public static readonly VbType Sanctuary = new("T4E6B", I("M4E78"), I("M4E7D"), I("Level"));

    /// <summary>T5810: a character, one record of MDATA4.</summary>
    public static readonly VbType Character = new("T5810",
        F("Name", 30), I("RaceIndex"), I("Alignment"), I("M5829"), S("Age"), I("SpellPoints"), I("DungeonLevel"),
        I("X"), I("Y"), S("Att"), S("Def"), I("Stats", 6), I("StatMods", 6), U("Pack", Slot, 40), U("M5860", Slot, 40),
        I("M586D", 35), I("M56F7"), S("M587C"), S("M588A"), I("Facing"), I("M58A6"), C("M579B"), C("Gold"),
        I("Hits"), I("MaxHits"), C("BankGold"), I("M56AF"), I("CurrentGuild"), U("GuildsJoined", Guild, 15),
        U("Companions", Companion, 4), I("M5700"), I("M58DC"), I("M58E9"), F("M58F9", 30), C("M5906", 8),
        I("M5914", 5), I("Ailments", 7), I("Resistances", 11), L("SpellEffects"), I("SpellResistances", 11),
        I("DeathState"), I("M5943"), F("M594C", 10), U("M5958", Window, 20), I("M5962"), L("M5970"),
        L("ItemEffects"), I("ItemResistances", 11), I("M5997", 1), I("M59A1"), I("M59B0", 10), I("M59BB"),
        I("M59CB"), I("M59D9"), U("Sanctuary", Sanctuary), I("M59F4", 2));

    /// <summary>T5755: a store shelf (MDATA6) or a Confinement pen (MDATA15).</summary>
    public static readonly VbType Shelf = new("T5755", I("M5766"), I("Id"), I("Align", 4));

    /// <summary>T4E8B: one of a room's four Foes() records (MDATA10).</summary>
    public static readonly VbType Foe = new("T4E8B",
        I("Att"), I("Def"), I("Hits"), I("MaxHits"), I("Alignment"), I("Engaged"), I("Ref"), I("GroupCount"),
        S("M4EE6"), I("IdLevel"), I("M4EF9"), I("KnownAs"));

    /// <summary>T4F0B: a room's Fight record, its chest (MDATA10).</summary>
    public static readonly VbType Fight = new("T4F0B",
        I("TreasureClass"), I("M4F24"), C("Gold"), I("ChestTrap"), I("ChestLock"));

    /// <summary>T5BF1: a Library record (MDATA13).</summary>
    public static readonly VbType Library = new("T5BF1",
        I("Id"), I("IdLevel"), C("M5C00"), F("M5C09", 30), I("M5C14"), L("M5C21"), F("M5C2F", 8), I("M5C3D"));

    /// <summary>T73E9: a Hall of Records entry (MDATA14).</summary>
    public static readonly VbType Hall = new("T73E9", C("M73F5"), F("M73FE", 30), L("M7406"));

    /// <summary>Every type above, for the selfcheck against MAIN.TXT.</summary>
    public static readonly VbType[] All = { Slot, Guild, Companion, Window, Sanctuary, Character, Shelf, Foe, Fight, Library, Hall };

    // ---- the files and the Len each is opened with ----

    /// <summary>MDATA4, the characters: <c>Len = mc0064</c>, 2900 (MAINMOD.BAS:153 and every other Open of gc1C10).</summary>
    public const int CharactersLen = 2900;
    /// <summary>MDATA6, the store's shelves: <c>Len = gc1C66</c>, 16 (MODULE1.BAS:1042).</summary>
    public const int StoreLen = 16;
    /// <summary>MDATA7, the guildmasters: <c>Len = gc1C62</c>, 128 (MODULE1.BAS reads it; MAINMOD.BAS:637 writes it).</summary>
    public const int GuildmastersLen = 128;
    /// <summary>MDATA8, the automap: <c>Len = mc004E</c>, 10 (MAINMOD.BAS:508) - one Long per 10-byte record.</summary>
    public const int AutomapLen = 10;
    /// <summary>MDATA10, the rooms ("maze reference file"): <c>Len = mc01B2</c>, 28 (MAINMOD.BAS:818).</summary>
    public const int RoomsLen = 28;
    /// <summary>MDATA12, the remembered parties: <c>Len = mc0084</c>, 35 (MULTIMOD.BAS:213).</summary>
    public const int PartiesLen = 35;
    /// <summary>MDATA13, the Library: <c>Len = mc0190</c>, 100 (MAINMOD.BAS:740).</summary>
    public const int LibraryLen = 100;
    /// <summary>MDATA14, the Hall of Records: <c>Len = mc0162</c>, 45 (MAINMOD.BAS:673).</summary>
    public const int HallLen = 45;
    /// <summary>MDATA15, the Confinement's pens: <c>Len = gc1C72</c>, 16 (MODULE1.BAS:1062).</summary>
    public const int PensLen = 16;

    /// <summary>gc0186, the version every save file is stamped with; fn0326 compares its first four characters.</summary>
    public const string Version = "1.1";

    /// <summary>fn0326 (MODULE1.BAS:134-138): a stamp the game accepts - its first four characters, or none at all.</summary>
    public static bool VersionAccepted(string stamp) =>
        stamp == "" || string.Equals(stamp.Length > 4 ? stamp[..4] : stamp, Version, StringComparison.Ordinal);
}
