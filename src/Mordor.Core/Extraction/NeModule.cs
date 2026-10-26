using System.Text;

namespace Mordor.Core.Extraction;

/// <summary>
/// The resources of a 16-bit Windows "New Executable" - MORDOR.EXE and MG11.DLL. The MZ header's
/// e_lfanew (0x3C) leads to the NE header, whose word at 0x24 is the resource table's offset from
/// it. The table opens with an alignment shift; then a run of type blocks, each a type id, a count,
/// four reserved bytes and that many twelve-byte entries (offset and length in alignment units, flags,
/// a name id, eight reserved bytes); a type id of 0 ends the run. An id with its top bit set is a
/// number; without, it is the offset in the table of a length-prefixed name ("MORDOR", "MAZE").
/// </summary>
public sealed class NeModule
{
    public const int RtIcon = 3, RtBitmap = 2, RtRcData = 10, RtGroupIcon = 14;

    /// <summary>One resource: its type and name (a number as "#n"), and where its bytes are.</summary>
    public sealed record Resource(string Type, string Name, int Offset, int Length);

    public byte[] Bytes { get; }
    public IReadOnlyList<Resource> Resources { get; }

    private NeModule(byte[] bytes, List<Resource> resources) { Bytes = bytes; Resources = resources; }

    public static NeModule Read(string path) => Parse(File.ReadAllBytes(path));

    public static NeModule Parse(byte[] f)
    {
        if (f.Length < 0x40 || f[0] != 'M' || f[1] != 'Z') throw new InvalidDataException("not an MZ program");
        int ne = BitConverter.ToInt32(f, 0x3C);
        if (ne <= 0 || ne + 0x40 > f.Length || f[ne] != 'N' || f[ne + 1] != 'E') throw new InvalidDataException("not a 16-bit Windows (NE) program");
        int U16(int at) => at + 1 < f.Length ? f[at] | (f[at + 1] << 8) : throw new InvalidDataException("the resource table runs off the end of the file");
        int table = ne + U16(ne + 0x24);
        int shift = U16(table);
        string NameOf(int id)
        {
            if ((id & 0x8000) != 0) return "#" + (id & 0x7FFF);
            int at = table + id;
            if (at >= f.Length || at + 1 + f[at] > f.Length) throw new InvalidDataException("a resource name runs off the end of the file");
            return Encoding.ASCII.GetString(f, at + 1, f[at]);
        }
        var list = new List<Resource>();
        int p = table + 2;
        while (U16(p) != 0)
        {
            string type = NameOf(U16(p));
            int count = U16(p + 2);
            p += 8;
            for (int i = 0; i < count; i++, p += 12)
            {
                int off = U16(p) << shift, len = U16(p + 2) << shift;
                if (off < 0 || off >= f.Length) continue;
                list.Add(new Resource(type, NameOf(U16(p + 6)), off, Math.Min(len, f.Length - off)));
            }
        }
        return new NeModule(f, list);
    }

    public static string TypeName(int rt) => "#" + rt;

    public Resource? Find(string type, string name) =>
        Resources.FirstOrDefault(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public byte[] Data(Resource r) => Bytes.AsSpan(r.Offset, r.Length).ToArray();

    /// <summary>
    /// The forms Visual Basic 3 keeps in the program: each is an RCDATA resource followed by one
    /// holding its names table, whose first name is the form's ("frmMenu"). See FormFonts in
    /// Mordor.Tests for the records inside.
    /// </summary>
    public IReadOnlyList<(string Form, Resource Data)> VbForms()
    {
        var rc = Resources.Where(r => r.Type == "#" + RtRcData && r.Name.StartsWith('#'))
                          .ToDictionary(r => int.Parse(r.Name.AsSpan(1)), r => r);
        var forms = new List<(string, Resource)>();
        foreach (var (id, data) in rc.OrderBy(kv => kv.Key))
        {
            if (!rc.TryGetValue(id + 1, out var names) || names.Length == 0) continue;
            int len = Bytes[names.Offset];
            if (len == 0 || len > names.Length - 1) continue;
            string first = Encoding.ASCII.GetString(Bytes, names.Offset + 1, len);
            if (first.StartsWith("frm", StringComparison.Ordinal)) forms.Add((first, data));
        }
        return forms;
    }

    /// <summary>
    /// The pictures a VB3 form holds, in the order they are stored: a picture property is a whole
    /// .BMP file (BITMAPFILEHEADER, BITMAPINFOHEADER, palette, pixels), each found by its "BM" and a
    /// header that reads.
    /// </summary>
    public List<RgbaImage> FormPictures(string form)
    {
        var (_, data) = VbForms().FirstOrDefault(f => f.Form.Equals(form, StringComparison.OrdinalIgnoreCase));
        var found = new List<RgbaImage>();
        if (data == null) return found;
        int end = data.Offset + data.Length;
        for (int i = data.Offset; i + 54 <= end; i++)
        {
            if (Bytes[i] != 'B' || Bytes[i + 1] != 'M' || BitConverter.ToInt32(Bytes, i + 14) != 40) continue;
            int size = BitConverter.ToInt32(Bytes, i + 2);
            if (size < 54 || i + size > end) continue;
            RgbaImage img;
            try { img = Dib.DecodeBmpFile(Bytes, i); }
            catch (InvalidDataException) { continue; }
            found.Add(img);
            i += size - 1;
        }
        return found;
    }
}
