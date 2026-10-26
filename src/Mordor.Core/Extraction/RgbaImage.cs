using System.IO.Compression;

namespace Mordor.Core.Extraction;

/// <summary>A picture as 32-bit ARGB pixels (0xAARRGGBB), top row first.</summary>
public sealed class RgbaImage
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public RgbaImage(int width, int height, uint[]? pixels = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "an image has to have some size");
        Width = width;
        Height = height;
        Pixels = pixels ?? new uint[width * height];
        if (Pixels.Length != width * height) throw new ArgumentException("pixel count does not match the size", nameof(pixels));
    }

    public uint this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    /// <summary>A rectangle of this image as an image of its own.</summary>
    public RgbaImage Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x + width > Width || y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(x), $"{width}x{height} at ({x},{y}) is outside a {Width}x{Height} image");
        var o = new RgbaImage(width, height);
        for (int r = 0; r < height; r++) Array.Copy(Pixels, (y + r) * Width + x, o.Pixels, r * width, width);
        return o;
    }

    /// <summary>
    /// Scaled to a new size by taking the nearest pixel, as GDI's StretchBlt does in its
    /// COLORONCOLOR mode: no colour is made that the picture did not have.
    /// </summary>
    public RgbaImage ScaleNearest(int width, int height)
    {
        var o = new RgbaImage(width, height);
        for (int y = 0; y < height; y++)
        {
            int sy = (int)((long)y * Height / height);
            for (int x = 0; x < width; x++) o.Pixels[y * width + x] = Pixels[sy * Width + (int)((long)x * Width / width)];
        }
        return o;
    }

    /// <summary>
    /// Same size, and the same colour at every pixel; a pixel both images leave fully transparent
    /// matches whatever colour it nominally has, since nothing of it is drawn.
    /// </summary>
    public bool SamePixels(RgbaImage other, out string difference)
    {
        difference = "";
        if (other.Width != Width || other.Height != Height)
        {
            difference = $"{Width}x{Height} against {other.Width}x{other.Height}";
            return false;
        }
        int bad = 0, first = -1;
        for (int i = 0; i < Pixels.Length; i++)
        {
            uint a = Pixels[i], b = other.Pixels[i];
            if (a == b || ((a >> 24) == 0 && (b >> 24) == 0)) continue;
            if (bad++ == 0) first = i;
        }
        if (bad == 0) return true;
        difference = $"{bad} pixel(s) differ, the first at ({first % Width},{first / Width}): {Pixels[first]:X8} against {other.Pixels[first]:X8}";
        return false;
    }
}

/// <summary>
/// Device-independent bitmaps as Windows 3.1 keeps them: a BITMAPINFOHEADER (or the older
/// BITMAPCOREHEADER), a palette, and rows bottom first, each padded to four bytes - uncompressed at
/// 1, 4, 8 or 24 bits a pixel, or run-length encoded at 4 or 8 (BI_RLE4, BI_RLE8; MG11.DLL's
/// "MONS" is RLE8).
/// </summary>
public static class Dib
{
    /// <summary>A whole .BMP file: the BITMAPFILEHEADER, then the DIB.</summary>
    public static RgbaImage DecodeBmpFile(byte[] b, int offset = 0)
    {
        if (offset + 14 > b.Length || b[offset] != 'B' || b[offset + 1] != 'M')
            throw new InvalidDataException("not a bitmap file (no BM signature)");
        int bits = BitConverter.ToInt32(b, offset + 10);
        return Decode(b, offset + 14, offset + bits);
    }

    /// <summary>
    /// A packed DIB at <paramref name="offset"/>. The pixels follow the palette unless
    /// <paramref name="pixelOffset"/> says where they are (a .BMP file's bfOffBits).
    /// </summary>
    public static RgbaImage Decode(byte[] b, int offset, int pixelOffset = -1)
    {
        if (offset + 12 > b.Length) throw new InvalidDataException("the bitmap header is cut short");
        int hs = BitConverter.ToInt32(b, offset);
        bool core = hs == 12;
        if (!core && hs < 40) throw new InvalidDataException($"unknown bitmap header size {hs}");
        int w = core ? BitConverter.ToUInt16(b, offset + 4) : BitConverter.ToInt32(b, offset + 4);
        int h = core ? BitConverter.ToUInt16(b, offset + 6) : BitConverter.ToInt32(b, offset + 8);
        int bpp = BitConverter.ToUInt16(b, offset + (core ? 10 : 14));
        int comp = core ? 0 : BitConverter.ToInt32(b, offset + 16);
        int used = core ? 0 : BitConverter.ToInt32(b, offset + 32);
        if (w <= 0 || w > 8192 || h == 0 || Math.Abs(h) > 8192) throw new InvalidDataException($"implausible bitmap size {w}x{h}");
        int colours = bpp <= 8 ? (used > 0 ? used : 1 << bpp) : 0;
        int entry = core ? 3 : 4;
        var pal = new uint[Math.Max(colours, 1 << Math.Min(bpp, 8))];
        for (int i = 0; i < colours; i++)
        {
            int q = offset + hs + i * entry;
            if (q + 3 > b.Length) throw new InvalidDataException("the bitmap palette is cut short");
            pal[i] = 0xFF000000u | ((uint)b[q + 2] << 16) | ((uint)b[q + 1] << 8) | b[q];
        }
        for (int i = colours; i < pal.Length; i++) pal[i] = 0xFF000000u;
        int px = pixelOffset >= 0 ? pixelOffset : offset + hs + colours * entry;
        bool bottomUp = h > 0;
        int height = Math.Abs(h);
        var img = new RgbaImage(w, height);

        if (comp == 1 || comp == 2)
        {
            DecodeRle(b, px, comp == 1 ? 8 : 4, w, height, pal, img, bottomUp);
            return img;
        }
        if (comp != 0) throw new InvalidDataException($"bitmap compression {comp} is not read");
        int stride = ((w * bpp + 31) / 32) * 4;
        for (int y = 0; y < height; y++)
        {
            int row = px + (bottomUp ? height - 1 - y : y) * stride;
            if (row + (w * bpp + 7) / 8 > b.Length) throw new InvalidDataException("the bitmap's pixels are cut short");
            for (int x = 0; x < w; x++)
            {
                img.Pixels[y * w + x] = bpp switch
                {
                    1 => pal[(b[row + x / 8] >> (7 - x % 8)) & 1],
                    4 => pal[(b[row + x / 2] >> (x % 2 == 0 ? 4 : 0)) & 15],
                    8 => pal[b[row + x]],
                    24 => 0xFF000000u | ((uint)b[row + x * 3 + 2] << 16) | ((uint)b[row + x * 3 + 1] << 8) | b[row + x * 3],
                    32 => 0xFF000000u | ((uint)b[row + x * 4 + 2] << 16) | ((uint)b[row + x * 4 + 1] << 8) | b[row + x * 4],
                    _ => throw new InvalidDataException($"{bpp} bits a pixel is not read"),
                };
            }
        }
        return img;
    }

    /// <summary>
    /// BI_RLE8 / BI_RLE4: runs of one index, literal stretches, and escapes for the end of a line,
    /// the end of the picture and a jump. Pixels a jump passes over keep palette entry 0, as GDI
    /// leaves them on a freshly made bitmap.
    /// </summary>
    private static void DecodeRle(byte[] b, int p, int bits, int w, int h, uint[] pal, RgbaImage img, bool bottomUp)
    {
        var idx = new byte[w * h];
        int x = 0, y = 0;
        void Put(int v) { if (x < w && y < h) idx[y * w + x] = (byte)v; x++; }
        while (p + 1 < b.Length)
        {
            int n = b[p++], c = b[p++];
            if (n > 0)
            {
                for (int k = 0; k < n; k++) Put(bits == 8 ? c : (k % 2 == 0 ? c >> 4 : c & 15));
                continue;
            }
            if (c == 0) { x = 0; y++; continue; }
            if (c == 1) break;
            if (c == 2) { x += b[p]; y += b[p + 1]; p += 2; continue; }
            if (bits == 8)
            {
                for (int k = 0; k < c; k++) Put(b[p + k]);
                p += c + (c & 1);
            }
            else
            {
                int bytes = (c + 1) / 2;
                for (int k = 0; k < c; k++) Put(k % 2 == 0 ? b[p + k / 2] >> 4 : b[p + k / 2] & 15);
                p += bytes + (bytes & 1);
            }
        }
        for (int yy = 0; yy < h; yy++)
        {
            int src = bottomUp ? h - 1 - yy : yy;
            for (int xx = 0; xx < w; xx++) img.Pixels[yy * w + xx] = pal[idx[src * w + xx]];
        }
    }

    /// <summary>
    /// An RT_ICON resource: a DIB twice the icon's height, the colour (XOR) picture and then the
    /// one-bit AND mask. A pixel the mask sets is transparent.
    /// </summary>
    public static RgbaImage DecodeIcon(byte[] b, int offset)
    {
        int hs = BitConverter.ToInt32(b, offset);
        if (hs != 40) throw new InvalidDataException($"unknown icon header size {hs}");
        int w = BitConverter.ToInt32(b, offset + 4);
        int h2 = BitConverter.ToInt32(b, offset + 8);
        int bpp = BitConverter.ToUInt16(b, offset + 14);
        int h = h2 / 2;
        int used = BitConverter.ToInt32(b, offset + 32);
        int colours = bpp <= 8 ? (used > 0 ? used : 1 << bpp) : 0;
        int xorAt = offset + hs + colours * 4;
        int xorStride = ((w * bpp + 31) / 32) * 4;
        int andAt = xorAt + xorStride * h;
        int andStride = ((w + 31) / 32) * 4;

        // the colour half alone, read as a DIB of the icon's own height
        var header = new byte[hs + colours * 4 + xorStride * h];
        Array.Copy(b, offset, header, 0, header.Length);
        BitConverter.GetBytes(h).CopyTo(header, 8);
        var img = Decode(header, 0);
        for (int y = 0; y < h; y++)
        {
            int row = andAt + (h - 1 - y) * andStride;
            for (int x = 0; x < w; x++)
                if (((b[row + x / 8] >> (7 - x % 8)) & 1) != 0) img.Pixels[y * w + x] = 0;
        }
        return img;
    }
}

/// <summary>PNG files: written as 8-bit RGBA, and read back in the common forms.</summary>
public static class Png
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static byte[] Encode(RgbaImage img)
    {
        var raw = new MemoryStream(img.Height * (img.Width * 4 + 1));
        for (int y = 0; y < img.Height; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < img.Width; x++)
            {
                uint p = img.Pixels[y * img.Width + x];
                raw.WriteByte((byte)(p >> 16)); raw.WriteByte((byte)(p >> 8)); raw.WriteByte((byte)p); raw.WriteByte((byte)(p >> 24));
            }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) { raw.Position = 0; raw.CopyTo(zs); }
        var o = new MemoryStream();
        o.Write(Signature);
        var ihdr = new byte[13];
        BigEndian(ihdr, 0, img.Width); BigEndian(ihdr, 4, img.Height);
        ihdr[8] = 8; ihdr[9] = 6;                    // 8 bits a channel, RGBA
        Chunk(o, "IHDR", ihdr);
        Chunk(o, "IDAT", z.ToArray());
        Chunk(o, "IEND", Array.Empty<byte>());
        return o.ToArray();
    }

    /// <summary>A non-interlaced PNG of any colour type at 1-8 bits a channel.</summary>
    public static RgbaImage Decode(byte[] f)
    {
        if (f.Length < 8 || !f.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("not a PNG file");
        int p = 8, w = 0, h = 0, depth = 0, ct = 0;
        var idat = new MemoryStream();
        uint[]? pal = null;
        byte[]? trns = null;
        while (p + 8 <= f.Length)
        {
            int len = ReadBigEndian(f, p);
            string type = System.Text.Encoding.ASCII.GetString(f, p + 4, 4);
            int d = p + 8;
            switch (type)
            {
                case "IHDR":
                    w = ReadBigEndian(f, d); h = ReadBigEndian(f, d + 4); depth = f[d + 8]; ct = f[d + 9];
                    if (f[d + 12] != 0) throw new InvalidDataException("interlaced PNGs are not read");
                    if (depth > 8) throw new InvalidDataException("16-bit PNGs are not read");
                    break;
                case "PLTE":
                    pal = new uint[len / 3];
                    for (int i = 0; i < pal.Length; i++) pal[i] = 0xFF000000u | ((uint)f[d + i * 3] << 16) | ((uint)f[d + i * 3 + 1] << 8) | f[d + i * 3 + 2];
                    break;
                case "tRNS": trns = f.AsSpan(d, len).ToArray(); break;
                case "IDAT": idat.Write(f, d, len); break;
            }
            if (type == "IEND") break;
            p += 12 + len;
        }
        idat.Position = 0;
        var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        byte[] r = raw.ToArray();
        int channels = ct switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"PNG colour type {ct}") };
        int bpp = Math.Max(1, channels * depth / 8);
        int stride = (w * channels * depth + 7) / 8;
        var cur = new byte[stride];
        var prev = new byte[stride];
        var img = new RgbaImage(w, h);
        int q = 0;
        for (int y = 0; y < h; y++)
        {
            int filter = r[q++];
            Array.Copy(r, q, cur, 0, stride);
            q += stride;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
                int v = cur[i];
                v += filter switch
                {
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => 0,
                };
                cur[i] = (byte)v;
            }
            for (int x = 0; x < w; x++)
            {
                int Sample(int index)
                {
                    int bit = index * depth;
                    int v = (cur[bit / 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1);
                    return depth == 8 ? v : v * 255 / ((1 << depth) - 1);
                }
                uint px;
                if (ct == 3)
                {
                    int bit = x * depth;
                    int i = (cur[bit / 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1);
                    px = pal![i];
                    if (trns != null && i < trns.Length) px = (px & 0xFFFFFF) | ((uint)trns[i] << 24);
                }
                else if (ct == 2) px = 0xFF000000u | ((uint)cur[x * 3] << 16) | ((uint)cur[x * 3 + 1] << 8) | cur[x * 3 + 2];
                else if (ct == 6) px = ((uint)cur[x * 4 + 3] << 24) | ((uint)cur[x * 4] << 16) | ((uint)cur[x * 4 + 1] << 8) | cur[x * 4 + 2];
                else if (ct == 4) { uint g = cur[x * 2]; px = ((uint)cur[x * 2 + 1] << 24) | (g * 0x010101u); }
                else { uint g = (uint)Sample(x); px = 0xFF000000u | (g * 0x010101u); }
                img.Pixels[y * w + x] = px;
            }
            (cur, prev) = (prev, cur);
        }
        return img;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static int ReadBigEndian(byte[] f, int p) => (f[p] << 24) | (f[p + 1] << 16) | (f[p + 2] << 8) | f[p + 3];

    private static void BigEndian(byte[] b, int p, int v)
    {
        b[p] = (byte)(v >> 24); b[p + 1] = (byte)(v >> 16); b[p + 2] = (byte)(v >> 8); b[p + 3] = (byte)v;
    }

    private static void Chunk(Stream o, string type, byte[] data)
    {
        var len = new byte[4];
        BigEndian(len, 0, data.Length);
        o.Write(len);
        var typed = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(typed, 0);
        data.CopyTo(typed, 4);
        o.Write(typed);
        BigEndian(len, 0, (int)Crc32(typed));
        o.Write(len);
    }

    private static uint[]? _crcTable;

    private static uint Crc32(byte[] b)
    {
        var t = _crcTable ??= Enumerable.Range(0, 256).Select(n =>
        {
            uint c = (uint)n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            return c;
        }).ToArray();
        uint crc = 0xFFFFFFFF;
        foreach (byte x in b) crc = t[(crc ^ x) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
