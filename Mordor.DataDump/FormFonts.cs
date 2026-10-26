using System.Text;

/// <summary>
/// Every control's font in the forms VB3 keeps in MORDOR.EXE: the font a control was designed in,
/// which is what sub0784 and sub07C5 read to give it the Options window's "Pro" or "NonPro" font
/// (MAINMOD.BAS:1862-1898, 1925-1975), and its Tag, whose leading ":" holds a size of its own.
///
/// A form resource (RCDATA) is the form's properties and then one record per control:
/// 01 (the first inside a container) or 03 (the next), a two-byte length that runs to one byte
/// short of the next record, a byte of nought, 80 for a control array, the control's number in the
/// names resource that follows it, its Index when in an array, a nought, and its type (FF for a
/// custom control, whose class name follows), then the properties. A font is stored as a property holding
/// the face's length-prefixed name, the size as a four-byte float (00 00 04 41 is 8.25) and a byte
/// of style bits; a control with no font property has VB3's default, MS Sans Serif 8.25 bold.
/// One form's own header stores a font: frmCharStat1's, Courier New 8.25 bold (RCDATA 8, offset
/// 56). Its controls with none are drawn in MS Sans Serif all the same - bm13.png shows lblStatInfo's
/// and lblRaceInfo's captions in a bold sans serif beside lblStat's Courier New - so a control
/// without a font of its own is VB3's default, never its form's. A Tag is a length-prefixed string; the ones
/// that matter start with ":".
///
/// <c>formfonts</c> prints the lot; <see cref="SelfCheckProbe"/> holds the port's own table of
/// design fonts (<c>Mordor.Core.DesignFonts</c>) to it.
/// </summary>
public static class FormFonts
{
    public sealed record Ctl(string Name, int Index, int Type, string? Class, string? Font, double Points, int Style, string? Tag)
    {
        public string Key => Index >= 0 ? $"{Name}({Index})" : Name;
        /// <summary>The face the control is drawn in: its own, or VB3's default.</summary>
        public string Face => Font ?? "MS Sans Serif";
        public double Size => Font == null ? 8.25 : Points;
    }

    public sealed record Form(int Resource, string Name, List<Ctl> Controls);

    public static Dictionary<int, byte[]> Resources(string exe)
    {
        byte[] b = File.ReadAllBytes(exe);
        int U16(int p) => b[p] | (b[p + 1] << 8);
        var res = new Dictionary<int, byte[]>();
        int ne = U16(0x3C), table = ne + U16(ne + 0x24), shift = U16(table), q = table + 2;
        while (U16(q) != 0)
        {
            int type = U16(q), count = U16(q + 2);
            q += 8;
            for (int i = 0; i < count; i++, q += 12)
                if (type == 0x800A)
                {
                    int at = U16(q) << shift;
                    res[U16(q + 6) & 0x7FFF] = b.AsSpan(at, Math.Min(U16(q + 2) << shift, b.Length - at)).ToArray();
                }
        }
        return res;
    }

    /// <summary>Every form in MORDOR.EXE: each RCDATA whose next resource is a names table starting "frm".</summary>
    public static List<Form> ReadAll(string gameDir)
    {
        var res = Resources(Path.Combine(gameDir, "MORDOR.EXE"));
        var forms = new List<Form>();
        foreach (var id in res.Keys.OrderBy(k => k))
        {
            if (!res.TryGetValue(id + 1, out var namesRes)) continue;
            var names = Names(namesRes);
            if (names.Count == 0 || !names[0].StartsWith("frm")) continue;
            forms.Add(new Form(id, names[0], Controls(res[id], names)));
        }
        return forms;
    }

    public static List<string> Names(byte[] namesRes)
    {
        var names = new List<string>();
        for (int q = 0; q < namesRes.Length && namesRes[q] != 0 && q + 1 + namesRes[q] <= namesRes.Length; q += namesRes[q] + 1)
        {
            string s = Encoding.ASCII.GetString(namesRes, q + 1, namesRes[q]);
            if (s.Any(ch => ch < 0x20 || ch > 0x7E)) break;
            names.Add(s);
        }
        return names;
    }

    private static List<Ctl> Controls(byte[] d, List<string> names)
    {
        int U16(int p) => d[p] | (d[p + 1] << 8);
        var list = new List<Ctl>();
        // Record by record: each runs to one byte short of the next (02s between close a
        // container). A record the header test does not know (a menu, say) is stepped over by
        // looking on for the next header. Most records have an FF straight after the type; a
        // ListBox or TextBox may carry a property or two first (the Library's lstList and
        // txtInfo), and one of those is taken only when a header follows it, or the data ends.
        bool Chains(int next)
        {
            while (next < d.Length && d[next] == 2) next++;
            if (next + 12 >= d.Length || d.Skip(next).All(b => b == 0)) return true;
            return Header(d, next, names, out _, strict: true) || Header(d, next, names, out _, strict: false);
        }
        for (int q = 0; q + 12 < d.Length;)
        {
            if (!Header(d, q, names, out var c, strict: true)
                && !(Header(d, q, names, out c, strict: false) && Chains(q + U16(q + 1) + 1))) { q++; continue; }
            int len = U16(q + 1);
            list.Add(Props(d, q, len, c));
            q += len + 1;
        }
        return list;
    }

    private static bool Header(byte[] d, int q, List<string> names, out Ctl c, bool strict)
    {
        c = new Ctl("", -1, 0, null, null, 0, 0, null);
        if (d[q] != 1 && d[q] != 3) return false;
        int len = d[q + 1] | (d[q + 2] << 8);
        if (len < 8 || q + len > d.Length) return false;
        if (d[q + 3] != 0 || (d[q + 4] != 0 && d[q + 4] != 0x80)) return false;
        int name = d[q + 5];
        if (name <= 0 || name >= names.Count) return false;
        int k = q + 6, index = -1;
        if (d[q + 4] == 0x80) { index = d[k] | (d[k + 1] << 8); k += 2; }
        if (d[k] != 0) return false;
        int type = d[k + 1];
        k += 2;
        string? cls = null;
        if (type == 0xFF)
        {
            int n = d[k];
            if (n == 0 || k + 1 + n >= d.Length) return false;
            cls = Encoding.ASCII.GetString(d, k + 1, n);
            if (cls.Any(ch => ch < 0x20 || ch > 0x7E)) return false;
            k += 1 + n;
        }
        if (strict ? d[k] != 0xFF : type is not (2 or 8)) return false;
        c = new Ctl(names[name], index, type, cls, null, 0, 0, null);
        return true;
    }

    private static Ctl Props(byte[] d, int q, int len, Ctl c)
    {
        int end = Math.Min(d.Length, q + len);
        string? font = null, tag = null;
        double pts = 0;
        int style = 0;
        for (int k = q + 7; k + 2 < end; k++)
        {
            int n = d[k];
            if (n < 2 || k + 1 + n > end) continue;
            // a face: letters and spaces, then a float size between 4 and 72 points
            if (font == null && n >= 4 && k + 1 + n + 5 <= end && char.IsLetter((char)d[k + 1])
                && Enumerable.Range(k + 1, n).All(i => char.IsLetter((char)d[i]) || d[i] == ' '))
            {
                float size = BitConverter.ToSingle(d, k + 1 + n);
                if (size >= 4 && size <= 72 && Math.Abs(size * 4 - Math.Round(size * 4)) < 1e-4)
                {
                    font = Encoding.ASCII.GetString(d, k + 1, n);
                    pts = size;
                    style = d[k + 1 + n + 4];
                    k += n + 5;
                    continue;
                }
            }
            if (tag == null && d[k + 1] == (byte)':' && Enumerable.Range(k + 2, n - 1).All(i => char.IsDigit((char)d[i]) || d[i] == '.'))
                tag = Encoding.ASCII.GetString(d, k + 1, n);
        }
        return c with { Font = font, Points = pts, Style = style, Tag = tag };
    }

    public static void Run(string dir)
    {
        foreach (var f in ReadAll(dir))
        {
            Console.WriteLine($"{f.Name} (RCDATA {f.Resource}): {f.Controls.Count} controls");
            foreach (var c in f.Controls)
                Console.WriteLine($"  {c.Key,-22} {(c.Class ?? c.Type.ToString("X2")),-18} {(c.Font == null ? "(default) MS Sans Serif 8.25" : $"{c.Font} {c.Points} style {c.Style:X2}"),-36} {c.Tag}");
        }
    }
}
