using System.Text;

/// <summary>
/// Reads the resource table of a 16-bit Windows NE executable (MORDOR.EXE, MG11.DLL) and writes out
/// every RT_BITMAP as a .bmp file. NE layout: the MZ header at 0x3C points at the NE header; the NE
/// header's offset 0x24 gives the resource table position, which lists type blocks, each with a
/// shift count, then name-info records holding a sector offset and length.
/// </summary>
public static class NeResources
{
    private const int RT_BITMAP = 2, RT_ICON = 3, RT_MENU = 4, RT_DIALOG = 5, RT_STRING = 6, RT_GROUP_ICON = 14;

    public static void Run(string dir, string? outDir = null)
    {
        outDir ??= System.IO.Path.Combine(dir, "..", "extracted-art");
        Directory.CreateDirectory(outDir);
        foreach (var file in new[] { "MORDOR.EXE", "MG11.DLL" })
        {
            string path = System.IO.Path.Combine(dir, file);
            if (!File.Exists(path)) { Console.WriteLine($"{file}: missing"); continue; }
            Console.WriteLine($"=== {file}");
            Dump(path, outDir, System.IO.Path.GetFileNameWithoutExtension(file));
        }
    }

    /// <summary>Wrap a bare DIB in a BITMAPFILEHEADER and write it as a .bmp.</summary>
    private static void WriteDib(byte[] b, int offset, int length, string outPath)
    {
        int headerSize = BitConverter.ToInt32(b, offset);
        int bpp = headerSize == 40 ? BitConverter.ToInt16(b, offset + 14) : BitConverter.ToInt16(b, offset + 10);
        int paletteEntries = bpp <= 8 ? (headerSize == 40 && BitConverter.ToInt32(b, offset + 32) != 0 ? BitConverter.ToInt32(b, offset + 32) : 1 << bpp) : 0;
        int pixelOffset = 14 + headerSize + paletteEntries * (headerSize == 40 ? 4 : 3);
        using var fs = File.Create(outPath);
        using var w = new BinaryWriter(fs);
        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(14 + length); w.Write(0); w.Write(0); w.Write(pixelOffset);
        w.Write(b, offset, length);
    }

    private static void Dump(string path, string outDir, string tag)
    {
        byte[] b = File.ReadAllBytes(path);
        ushort U16(int p) => (ushort)(b[p] | (b[p + 1] << 8));

        int ne = (int)(b[0x3C] | (b[0x3D] << 8) | (b[0x3E] << 16) | (b[0x3F] << 24));
        if (ne <= 0 || ne + 0x40 > b.Length || b[ne] != (byte)'N' || b[ne + 1] != (byte)'E')
        {
            Console.WriteLine($"  not an NE image (signature {(char)b[ne]}{(char)b[ne + 1]})");
            return;
        }

        int resTable = ne + U16(ne + 0x24);
        int p = resTable;
        int align = U16(p); p += 2;               // shift count: sizes are in 1 << align byte units
        Console.WriteLine($"  resource table at 0x{resTable:X}, alignment shift {align}");

        int saved = 0;
        while (p + 8 <= b.Length)
        {
            ushort typeId = U16(p);
            if (typeId == 0) break;               // end of the type table
            ushort count = U16(p + 2);
            p += 8;                               // typeId, count, 4 reserved bytes
            string typeName = (typeId & 0x8000) != 0 ? ((typeId & 0x7FFF) switch
            {
                RT_BITMAP => "BITMAP", RT_ICON => "ICON", RT_MENU => "MENU", RT_DIALOG => "DIALOG",
                RT_STRING => "STRING", RT_GROUP_ICON => "GROUP_ICON", _ => "type" + (typeId & 0x7FFF),
            }) : "named";
            Console.WriteLine($"  {typeName,-10} x{count}");

            for (int i = 0; i < count && p + 12 <= b.Length; i++, p += 12)
            {
                int offset = U16(p) << align;
                int length = U16(p + 2) << align;
                ushort nameId = U16(p + 6);
                if (offset <= 0 || offset + length > b.Length || length <= 0) continue;

                // VB3 keeps its embedded pictures in RCDATA (type 10); report what each blob starts with
                if ((typeId & 0x7FFF) == 10 || (typeId & 0x8000) == 0)
                {
                    string head = string.Join(" ", Enumerable.Range(0, Math.Min(8, length)).Select(k => b[offset + k].ToString("X2")));
                    string ascii = new string(Enumerable.Range(0, Math.Min(8, length)).Select(k => b[offset + k] >= 32 && b[offset + k] < 127 ? (char)b[offset + k] : '.').ToArray());
                    bool isDib = length > 40 && (BitConverter.ToInt32(b, offset) == 40 || BitConverter.ToInt32(b, offset) == 12);
                    bool bmp = b[offset] == 'B' && b[offset + 1] == 'M';
                    if (isDib || bmp || length > 2000)
                        Console.WriteLine($"    blob id {nameId & 0x7FFF,5} len {length,7}  {head}  |{ascii}|{(isDib ? "  <-- DIB" : bmp ? "  <-- BMP" : "")}");
                    string raw = System.IO.Path.Combine(outDir, $"{tag}_raw_{nameId & 0x7FFF}.bin");
                    if (length > 2000 || isDib || bmp) File.WriteAllBytes(raw, b.Skip(offset).Take(length).ToArray());
                    if (isDib)
                    {
                        WriteDib(b, offset, length, System.IO.Path.Combine(outDir, $"{tag}_pic_{nameId & 0x7FFF}.bmp"));
                        saved++;
                    }
                    continue;
                }
                if ((typeId & 0x7FFF) != RT_BITMAP || (typeId & 0x8000) == 0) continue;

                // an RT_BITMAP resource is a DIB (BITMAPINFOHEADER + palette + pixels); prepend a BITMAPFILEHEADER
                var dib = new byte[length];
                Array.Copy(b, offset, dib, 0, length);
                int headerSize = BitConverter.ToInt32(dib, 0);
                if (headerSize != 40 && headerSize != 12) continue;
                int width = headerSize == 40 ? BitConverter.ToInt32(dib, 4) : BitConverter.ToInt16(dib, 4);
                int height = headerSize == 40 ? BitConverter.ToInt32(dib, 8) : BitConverter.ToInt16(dib, 6);
                int bpp = headerSize == 40 ? BitConverter.ToInt16(dib, 14) : BitConverter.ToInt16(dib, 10);
                if (width <= 0 || Math.Abs(height) <= 0 || width > 4000 || Math.Abs(height) > 4000) continue;

                int paletteEntries = bpp <= 8 ? (headerSize == 40 && BitConverter.ToInt32(dib, 32) != 0 ? BitConverter.ToInt32(dib, 32) : 1 << bpp) : 0;
                int paletteBytes = paletteEntries * (headerSize == 40 ? 4 : 3);
                int pixelOffset = 14 + headerSize + paletteBytes;

                string idText = (nameId & 0x8000) != 0 ? (nameId & 0x7FFF).ToString() : "n" + nameId;
                string name = $"{tag}_bitmap_{idText}_{width}x{Math.Abs(height)}.bmp";
                using var fs = File.Create(System.IO.Path.Combine(outDir, name));
                using var w = new BinaryWriter(fs);
                w.Write((byte)'B'); w.Write((byte)'M');
                w.Write(14 + length);
                w.Write(0); w.Write(0);
                w.Write(pixelOffset);
                w.Write(dib);
                saved++;
                Console.WriteLine($"    {name}  ({bpp}bpp, {length} bytes)");
            }
        }
        Console.WriteLine($"  saved {saved} bitmaps");
    }
}
