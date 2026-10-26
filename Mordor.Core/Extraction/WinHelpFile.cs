using System.Text;

namespace Mordor.Core.Extraction;

/// <summary>
/// A Windows 3.1 help file (MORDOR.HLP), read far enough to give back its topics: their titles,
/// paragraphs, tabs, tables and jumps. The layout is the one documented by the help decompilers
/// (Pete Davis and Mike Wallace's "Windows Help File Format", Manfred Winterhoff's helpfile.txt and
/// helpdeco):
///
/// <list type="bullet">
/// <item>a 16-byte header (magic 0x00035F3F, the directory's offset), then internal files, each
/// opening with a 9-byte header (reserved size, used size, flags);</item>
/// <item>the directory, a B+ tree of file name to offset, and the same B+ tree layout again for
/// <c>|CONTEXT</c> (context-string hash to topic offset) and <c>|TTLBTREE</c> (topic offset to
/// title);</item>
/// <item><c>|SYSTEM</c>, whose version and flags say how <c>|TOPIC</c> is stored: 4 KB blocks, each
/// a 12-byte header and then, when flag 4 or 8 is set, LZ77-compressed data;</item>
/// <item><c>|TOPIC</c>, a chain of TOPICLINK records - a topic header (type 2) carrying the title,
/// and display records (0x20, and 0x23 for a table row) whose LinkData1 holds the paragraph format
/// and formatting commands and whose LinkData2 holds the text, one string per command.</item>
/// </list>
///
/// Phrase compression (<c>|Phrases</c>, <c>|PhrIndex</c>) is read as well, though MORDOR.HLP (1.1)
/// uses neither; pictures inside a paragraph are recognised and stepped over.
/// </summary>
public sealed class WinHelpFile
{
    private const int Magic = 0x00035F3F;

    private readonly byte[] _f;
    private readonly Dictionary<string, int> _files = new(StringComparer.Ordinal);

    public int Minor { get; }
    public int Flags { get; }
    public string Title { get; } = "";

    public IReadOnlyCollection<string> InternalFiles => _files.Keys;

    private WinHelpFile(byte[] f)
    {
        _f = f;
        if (f.Length < 16 || BitConverter.ToInt32(f, 0) != Magic) throw new InvalidDataException("not a Windows help file");
        int dir = BitConverter.ToInt32(f, 4);
        foreach (var (key, value) in BTree(f, dir + 9, CString, 4))
            _files[(string)key] = BitConverter.ToInt32(f, value);
        var sys = File_("|SYSTEM");
        if (BitConverter.ToUInt16(sys, 0) != 0x036C) throw new InvalidDataException("the help file's |SYSTEM does not read");
        Minor = BitConverter.ToUInt16(sys, 2);
        if (Minor > 16)
        {
            Flags = BitConverter.ToUInt16(sys, 10);
            for (int p = 12; p + 4 <= sys.Length;)
            {
                int type = BitConverter.ToUInt16(sys, p), len = BitConverter.ToUInt16(sys, p + 2);
                if (type == 1 && p + 4 + len <= sys.Length) Title = Latin(sys, p + 4, len).TrimEnd('\0');
                p += 4 + len;
            }
        }
        else Title = Latin(sys, 12, Math.Max(0, sys.Length - 12)).TrimEnd('\0');
    }

    public static WinHelpFile Read(string path) => new(File.ReadAllBytes(path));

    public static WinHelpFile Parse(byte[] bytes) => new(bytes);

    public bool Has(string name) => _files.ContainsKey(name);

    /// <summary>An internal file's bytes: <c>|TOPIC</c>, or baggage such as <c>CITYW1.BMP</c>.</summary>
    public byte[] File_(string name)
    {
        if (!_files.TryGetValue(name, out int off)) throw new InvalidDataException($"the help file has no {name}");
        int used = BitConverter.ToInt32(_f, off + 4);
        if (off + 9 + used > _f.Length) throw new InvalidDataException($"the help file's {name} is cut short");
        return _f.AsSpan(off + 9, used).ToArray();
    }

    private static string Latin(byte[] b, int at, int len) => Encoding.Latin1.GetString(b, at, len);

    private static (object, int) CString(byte[] b, int p)
    {
        int e = p;
        while (e < b.Length && b[e] != 0) e++;
        return (Latin(b, p, e - p), e + 1);
    }

    /// <summary>
    /// The leaves of a B+ tree in key order: (key, offset of the value). <paramref name="key"/> reads a
    /// key at a position and says where it ends; <paramref name="valueSize"/> is skipped after it
    /// (0 when the key reader consumes the value too).
    /// </summary>
    private static List<(object Key, int Value)> BTree(byte[] b, int at, Func<byte[], int, (object, int)> key, int valueSize)
    {
        if (BitConverter.ToUInt16(b, at) != 0x293B) throw new InvalidDataException("a help file B+ tree does not read");
        int pageSize = BitConverter.ToUInt16(b, at + 4);
        int root = BitConverter.ToInt16(b, at + 26);
        int levels = BitConverter.ToInt16(b, at + 32);
        int pages = at + 38;
        int page = root;
        for (int l = 1; l < levels; l++) page = BitConverter.ToInt16(b, pages + page * pageSize + 4);
        var list = new List<(object, int)>();
        var seen = new HashSet<int>();
        while (page != -1 && seen.Add(page))
        {
            int p = pages + page * pageSize;
            int n = BitConverter.ToInt16(b, p + 2);
            int next = BitConverter.ToInt16(b, p + 6);
            p += 8;
            for (int i = 0; i < n; i++)
            {
                var (k, q) = key(b, p);
                list.Add((k, q));
                p = q + valueSize;
            }
            page = next;
        }
        return list;
    }

    /// <summary>The context-string hashes (as WinHelp hashes a jump's target) to topic offsets.</summary>
    public Dictionary<uint, int> Contexts()
    {
        var map = new Dictionary<uint, int>();
        if (!Has("|CONTEXT")) return map;
        var c = File_("|CONTEXT");
        foreach (var (k, v) in BTree(c, 0, (b, p) => (BitConverter.ToUInt32(b, p), p + 4), 4))
            map[(uint)k] = BitConverter.ToInt32(c, v);
        return map;
    }

    /// <summary>Each topic's offset and title, in topic order.</summary>
    public List<(int Offset, string Title)> Titles()
    {
        var list = new List<(int, string)>();
        if (!Has("|TTLBTREE")) return list;
        var t = File_("|TTLBTREE");
        foreach (var (k, v) in BTree(t, 0, (b, p) => { int e = p + 4; while (e < b.Length && b[e] != 0) e++; return (BitConverter.ToInt32(b, p), e + 1); }, 0))
        {
            int s = v - 1;
            while (s > 0 && t[s - 1] != 0) s--;
            list.Add(((int)k, Latin(t, s, v - 1 - s)));
        }
        return list;
    }

    // ---- |TOPIC ----

    public static byte[] Lz77(byte[] b, int start, int end)
    {
        var o = new List<byte>(16384);
        int p = start;
        while (p < end)
        {
            int flags = b[p++];
            for (int bit = 0; bit < 8 && p < end; bit++, flags >>= 1)
            {
                if ((flags & 1) != 0)
                {
                    if (p + 1 >= end) return o.ToArray();
                    int w = b[p] | (b[p + 1] << 8);
                    p += 2;
                    int len = (w >> 12) + 3, from = o.Count - ((w & 0xFFF) + 1);
                    for (int k = 0; k < len; k++) o.Add(from + k >= 0 ? o[from + k] : (byte)0);
                }
                else o.Add(b[p++]);
            }
        }
        return o.ToArray();
    }

    /// <summary>
    /// The topics in file order. A topic offset (what <see cref="Contexts"/> and
    /// <see cref="Titles"/> hold) is reproduced for each: in 3.1 files the block number times 0x8000
    /// plus a count of characters since that block's first record.
    /// </summary>
    public List<HelpTopicData> Topics()
    {
        var topic = File_("|TOPIC");
        bool compressed = Minor > 16 && (Flags & 0xC) != 0;
        int blockSize = Minor <= 16 ? 2048 : (Flags & 8) != 0 ? 2048 : 4096;
        // the decompressed blocks, end to end; a TOPICPOS is a block number and an offset in it
        int blocks = (topic.Length + blockSize - 1) / blockSize;
        var data = new List<byte[]>(blocks);
        for (int b = 0; b < blocks; b++)
        {
            int s = b * blockSize + 12, e = Math.Min(topic.Length, (b + 1) * blockSize);
            data.Add(s >= e ? Array.Empty<byte>() : compressed ? Lz77(topic, s, e) : topic.AsSpan(s, e - s).ToArray());
        }
        int posShift = Minor <= 16 ? 0 : 14;              // 3.0 positions are plain file offsets
        var stream = new List<byte>(blocks * 16384);
        var blockStart = new int[blocks];
        for (int b = 0; b < blocks; b++) { blockStart[b] = stream.Count; stream.AddRange(data[b]); }
        var S = stream.ToArray();
        int StreamPos(int topicPos)
        {
            if (posShift == 0) { int blk = topicPos / blockSize; return blk < blocks ? blockStart[blk] + topicPos % blockSize - 12 : -1; }
            int block = topicPos >> posShift, off = (topicPos & ((1 << posShift) - 1)) - 12;
            return block < blocks && off >= 0 ? blockStart[block] + off : -1;
        }
        int BlockOf(int streamPos)
        {
            int b = Array.BinarySearch(blockStart, streamPos);
            if (b < 0) b = ~b - 1;
            while (b + 1 < blocks && blockStart[b + 1] == blockStart[b] && blockStart[b] <= streamPos) b++;
            return b;
        }

        var phrases = Phrases();
        var topics = new List<HelpTopicData>();
        HelpTopicData? cur = null;
        int pos = StreamPos(BitConverter.ToInt32(topic, 4));
        int charBlock = -1, chars = 0;
        var visited = new HashSet<int>();
        while (pos >= 0 && pos + 21 <= S.Length && visited.Add(pos))
        {
            int recSize = BitConverter.ToInt32(S, pos);
            int dataLen2 = BitConverter.ToInt32(S, pos + 4);
            int next = BitConverter.ToInt32(S, pos + 12);
            int dataLen1 = BitConverter.ToInt32(S, pos + 16);
            int type = S[pos + 20];
            if (recSize < 21 || dataLen1 < 21 || pos + recSize > S.Length) throw new InvalidDataException("a help topic record does not read");
            var d1 = S.AsSpan(pos + 21, dataLen1 - 21).ToArray();
            var stored = S.AsSpan(pos + dataLen1, recSize - dataLen1).ToArray();
            var d2 = dataLen2 > stored.Length ? Unphrase(stored, dataLen2, phrases) : stored;

            int block = BlockOf(pos);
            if (block != charBlock) { charBlock = block; chars = 0; }
            int offset = (block << 15) + chars;
            if (type == 2)
            {
                int e = 0;
                while (e < d2.Length && d2[e] != 0) e++;
                cur = new HelpTopicData(offset, Win1252(d2, 0, e));
                topics.Add(cur);
            }
            else if (type == 0x20 || type == 0x23)
            {
                if (cur == null) { cur = new HelpTopicData(offset, ""); topics.Add(cur); }
                chars += Display(d1, d2, type == 0x23, cur);
            }
            if (next == -1) break;
            pos = StreamPos(next);
        }
        return topics;
    }

    private string[]? Phrases()
    {
        if (!Has("|Phrases")) return null;
        var p = File_("|Phrases");
        int n = BitConverter.ToUInt16(p, 0);
        bool packed = Minor > 16;
        int offsets = packed ? 8 : 4;
        var o = new int[n + 1];
        for (int i = 0; i <= n; i++) o[i] = BitConverter.ToUInt16(p, offsets + i * 2);
        byte[] text;
        int baseAt;
        if (packed)
        {
            int start = offsets + (n + 1) * 2;
            text = Lz77(p, start, p.Length);
            baseAt = o[0];
        }
        else { text = p; baseAt = 0; }
        var list = new string[n];
        for (int i = 0; i < n; i++)
        {
            int s = o[i] - baseAt, e = o[i + 1] - baseAt;
            list[i] = s >= 0 && e <= text.Length && e >= s ? Win1252(text, s, e - s) : "";
        }
        return list;
    }

    private static byte[] Unphrase(byte[] b, int size, string[]? phrases)
    {
        if (phrases == null) return b;
        var o = new List<byte>(size);
        for (int i = 0; i < b.Length && o.Count < size; i++)
        {
            int c = b[i];
            if (c == 0 || c > 0x10 || i + 1 >= b.Length) { o.Add((byte)c); continue; }
            int n = 256 * (c - 1) + b[++i];
            int phrase = n / 2;
            if (phrase < phrases.Length) o.AddRange(Encoding.Latin1.GetBytes(phrases[phrase]));
            if ((n & 1) != 0) o.Add((byte)' ');
        }
        return o.ToArray();
    }

    private static int ScanWord(byte[] b, ref int p)
    {
        if ((b[p] & 1) != 0) { int v = BitConverter.ToUInt16(b, p) >> 1; p += 2; return v; }
        return b[p++] >> 1;
    }

    private static int ScanInt(byte[] b, ref int p)
    {
        if ((b[p] & 1) != 0) { int v = (BitConverter.ToUInt16(b, p) >> 1) - 0x4000; p += 2; return v; }
        return (b[p++] >> 1) - 0x40;
    }

    private static long ScanLong(byte[] b, ref int p)
    {
        if ((b[p] & 1) != 0) { long v = (BitConverter.ToUInt32(b, p) >> 1) - 0x40000000L; p += 4; return v; }
        long w = (BitConverter.ToUInt16(b, p) >> 1) - 0x4000;
        p += 2;
        return w;
    }

    /// <summary>
    /// One display record: a paragraph format, then its text with the formatting commands threaded
    /// through it - each string of LinkData2 is followed by one command from LinkData1. 0x82 ends a
    /// paragraph (the next keeps the format); 0xFF ends the record. Returns the record's character
    /// count, which topic offsets count in.
    /// </summary>
    private static int Display(byte[] d1, byte[] d2, bool table, HelpTopicData topic)
    {
        int p = 0;
        ScanLong(d1, ref p);                           // topic size
        int length = ScanWord(d1, ref p);              // characters in the record
        HelpTableData? row = null;
        if (table)
        {
            int cols = d1[p++];
            int tableType = d1[p++];
            if (tableType == 0 || tableType == 2) p += 2;   // minimum table width
            var columns = new (int Gap, int Width)[cols];
            // each column a pair of words; the first column's first word is the row's left edge
            for (int c = 0; c < cols; c++) columns[c] = (BitConverter.ToInt16(d1, p + c * 4 + 2), BitConverter.ToInt16(d1, p + c * 4));
            p += cols * 4;
            row = new HelpTableData(columns);
            topic.Items.Add(row);
        }
        int tp = 0;
        while (p < d1.Length)
        {
            int column = -1;
            if (table)
            {
                column = BitConverter.ToInt16(d1, p);
                p += 2;
                if (column == -1) break;
                p += 3;
            }
            p += 2;                                     // two bytes of nothing known
            p += 2;                                     // a paragraph id
            int bits = BitConverter.ToUInt16(d1, p);
            p += 2;
            var fmt = new HelpParagraphFormat();
            if ((bits & 0x0001) != 0) ScanLong(d1, ref p);
            if ((bits & 0x0002) != 0) fmt.SpaceAbove = ScanInt(d1, ref p);
            if ((bits & 0x0004) != 0) fmt.SpaceBelow = ScanInt(d1, ref p);
            if ((bits & 0x0008) != 0) fmt.LineSpacing = ScanInt(d1, ref p);
            if ((bits & 0x0010) != 0) fmt.LeftIndent = ScanInt(d1, ref p);
            if ((bits & 0x0020) != 0) fmt.RightIndent = ScanInt(d1, ref p);
            if ((bits & 0x0040) != 0) fmt.FirstIndent = ScanInt(d1, ref p);
            if ((bits & 0x0100) != 0) p += 3;           // border: flags and width
            if ((bits & 0x0200) != 0)
            {
                int n = ScanInt(d1, ref p);
                for (int i = 0; i < n; i++)
                {
                    int stop = ScanWord(d1, ref p);
                    int kind = (stop & 0x4000) != 0 ? ScanWord(d1, ref p) : 0;
                    fmt.TabStops.Add((stop & 0x3FFF, kind));
                }
            }
            fmt.Alignment = (bits & 0x0400) != 0 ? 1 : (bits & 0x0800) != 0 ? 2 : 0;

            var para = new HelpParagraphData(fmt, column);
            AddParagraph(topic, row, para);
            bool done = false;
            while (!done)
            {
                int s = tp;
                while (tp < d2.Length && d2[tp] != 0) tp++;
                if (tp > s) para.Runs.Add(new HelpRun(HelpRunKind.Text, Win1252(d2, s, tp - s)));
                tp++;
                if (p >= d1.Length) return length;
                int c = d1[p++];
                switch (c)
                {
                    case 0xFF: done = true; break;
                    case 0x20: p += 4; break;           // vfld
                    case 0x21: p += 2; break;           // dtype
                    case 0x80: p += 2; break;           // font change
                    case 0x81: para.Runs.Add(new HelpRun(HelpRunKind.LineBreak)); break;
                    case 0x82:
                        para.Ended = true;
                        para = new HelpParagraphData(fmt, column);
                        AddParagraph(topic, row, para);
                        break;
                    case 0x83: para.Runs.Add(new HelpRun(HelpRunKind.Tab)); break;
                    case 0x86: case 0x87: case 0x88:
                        {
                            int kind = d1[p++];
                            long size = ScanLong(d1, ref p);
                            if (kind == 0x22) ScanWord(d1, ref p);   // hotspot count
                            p += (int)size;
                            para.Runs.Add(new HelpRun(HelpRunKind.Picture));
                            break;
                        }
                    case 0x89: para.Runs.Add(new HelpRun(HelpRunKind.LinkEnd)); break;
                    case 0x8B: para.Runs.Add(new HelpRun(HelpRunKind.Text, " ")); break;
                    case 0x8C: para.Runs.Add(new HelpRun(HelpRunKind.Text, "-")); break;
                    case 0xC8: case 0xCC:
                        p += 2 + BitConverter.ToInt16(d1, p);
                        para.Runs.Add(new HelpRun(HelpRunKind.LinkStart, Macro: true));
                        break;
                    case 0xE0: case 0xE1:
                        para.Runs.Add(new HelpRun(HelpRunKind.LinkStart, Offset: BitConverter.ToInt32(d1, p), Popup: c == 0xE0));
                        p += 4;
                        break;
                    case 0xE2: case 0xE3: case 0xE6: case 0xE7:
                        para.Runs.Add(new HelpRun(HelpRunKind.LinkStart, Hash: BitConverter.ToUInt32(d1, p), Popup: c is 0xE2 or 0xE6));
                        p += 4;
                        break;
                    case 0xEA: case 0xEB: case 0xEE: case 0xEF:
                        {
                            int len = BitConverter.ToInt16(d1, p);
                            int kind = d1[p + 2];
                            uint hash = BitConverter.ToUInt32(d1, p + 3);
                            // kinds 4 and 6 name another help file: not a jump inside this one
                            para.Runs.Add(new HelpRun(HelpRunKind.LinkStart, Hash: kind is 4 or 6 ? null : hash, Popup: c is 0xEA or 0xEE, External: kind is 4 or 6));
                            p += 2 + len;
                            break;
                        }
                    default:
                        throw new InvalidDataException($"unknown help formatting command 0x{c:X2}");
                }
            }
            if (!table) break;
        }
        return length;
    }

    private static void AddParagraph(HelpTopicData topic, HelpTableData? row, HelpParagraphData para)
    {
        if (row != null) row.Cells.Add(para);
        else topic.Items.Add(para);
    }

    private static readonly char[] Cp1252High =
        "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ".ToCharArray();

    /// <summary>Text as Windows 3.1 wrote it, code page 1252.</summary>
    public static string Win1252(byte[] b, int at, int len)
    {
        var c = new char[len];
        for (int i = 0; i < len; i++)
        {
            byte x = b[at + i];
            c[i] = x is >= 0x80 and < 0xA0 ? Cp1252High[x - 0x80] : (char)x;
        }
        return new string(c);
    }
}

/// <summary>What a paragraph says about its layout, in the help file's own units.</summary>
public sealed class HelpParagraphFormat
{
    public int LeftIndent, RightIndent, FirstIndent, SpaceAbove, SpaceBelow, LineSpacing;
    /// <summary>0 left, 1 right, 2 centred.</summary>
    public int Alignment;
    public List<(int Position, int Kind)> TabStops { get; } = new();
}

public enum HelpRunKind { Text, Tab, LineBreak, LinkStart, LinkEnd, Picture }

/// <summary>
/// A piece of a paragraph. A link start names its target by context hash, or by topic offset in
/// 3.0 files; a macro hotspot or a jump into another help file has neither.
/// </summary>
public sealed record HelpRun(HelpRunKind Kind, string Text = "", uint? Hash = null, int? Offset = null, bool Popup = false, bool Macro = false, bool External = false);

public sealed class HelpParagraphData
{
    public HelpParagraphFormat Format { get; }
    /// <summary>The table column the paragraph is in, or -1.</summary>
    public int Column { get; }
    public List<HelpRun> Runs { get; } = new();
    /// <summary>Closed by an end-of-paragraph command; the record's last, unclosed one is empty.</summary>
    public bool Ended { get; set; }

    public HelpParagraphData(HelpParagraphFormat format, int column) { Format = format; Column = column; }
}

/// <summary>A table row (a 0x23 record): its columns, and the paragraphs in its cells.</summary>
public sealed class HelpTableData
{
    public (int Gap, int Width)[] Columns { get; }
    public List<HelpParagraphData> Cells { get; } = new();
    public HelpTableData((int Gap, int Width)[] columns) { Columns = columns; }
}

/// <summary>A topic: where it starts, its title, and its paragraphs and table rows in order.</summary>
public sealed class HelpTopicData
{
    public int Offset { get; }
    public string Title { get; }
    public List<object> Items { get; } = new();
    public HelpTopicData(int offset, string title) { Offset = offset; Title = title; }
}
