/// <summary>
/// <c>icon &lt;out.ico&gt; &lt;png&gt;...</c>: packs square PNG pictures into one Windows icon file, each PNG
/// whole as one of its images (an icon image may be a PNG since Windows Vista). It is how the
/// program's icon, src/Mordor.App/Assets/Icon/opendejenol.ico, is made from the PNGs beside it
/// (docs/TOOLS.md, "The program's icon"). The game folder is not read.
/// </summary>
public static class IconPack
{
    static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("usage: Mordor.Tools <gamedir> icon <out.ico> <png> [png...]   (the game folder is not read)");
            return 2;
        }
        var images = new List<(int Size, byte[] Data, string From)>();
        foreach (var path in args.Skip(1))
        {
            var data = File.ReadAllBytes(path);
            // the signature, then IHDR first: width, height (big-endian), bit depth, colour type
            if (data.Length < 33 || !data.AsSpan(0, 8).SequenceEqual(PngSignature) || data[12] != 'I' || data[13] != 'H' || data[14] != 'D' || data[15] != 'R')
            {
                Console.WriteLine($"not a PNG: {path}");
                return 1;
            }
            int w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            if (w != h || w < 1 || w > 256)
            {
                Console.WriteLine($"{path} is {w}x{h}: an icon image is square, 256 at most");
                return 1;
            }
            if (data[24] != 8 || data[25] != 6)
            {
                Console.WriteLine($"{path} is not 8-bit RGBA (bit depth {data[24]}, colour type {data[25]})");
                return 1;
            }
            if (images.Any(i => i.Size == w))
            {
                Console.WriteLine($"two pictures {w}x{w}: {path}");
                return 1;
            }
            images.Add((w, data, path));
        }
        images.Sort((a, b) => a.Size.CompareTo(b.Size));

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            // ICONDIR: reserved, type 1 (icon), count
            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)images.Count);
            int offset = 6 + 16 * images.Count;
            foreach (var (size, data, _) in images)
            {
                // ICONDIRENTRY: width and height (0 is 256), colours, reserved, planes, bits per pixel, bytes, offset
                bw.Write((byte)(size == 256 ? 0 : size));
                bw.Write((byte)(size == 256 ? 0 : size));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((ushort)1);
                bw.Write((ushort)32);
                bw.Write(data.Length);
                bw.Write(offset);
                offset += data.Length;
            }
            foreach (var (_, data, _) in images) bw.Write(data);
        }
        File.WriteAllBytes(args[0], ms.ToArray());
        Console.WriteLine($"wrote {Path.GetFullPath(args[0])}: {ms.Length} bytes, {images.Count} images");
        foreach (var (size, data, from) in images) Console.WriteLine($"  {size,3}x{size,-3} {data.Length,7} bytes  {from}");
        return 0;
    }
}
