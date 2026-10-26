using System.Text;
/// <summary>Dump every named BMP in MG11.DLL with its real dimensions, to find the maze art.</summary>
public static class WallArtProbe { public static void Run(string dir) {
  string path = System.IO.Path.Combine(dir, "MG11.DLL");
  byte[] f = File.ReadAllBytes(path);
  string outDir = System.IO.Path.Combine(dir, "..", "extracted-art", "wallart");   // outside the port: the bitmaps are the game's
  Directory.CreateDirectory(outDir);
  int saved = 0;
  for (int i = 0; i + 54 < f.Length; i++)
  {
    if (f[i] != 'B' || f[i + 1] != 'M') continue;
    int size = BitConverter.ToInt32(f, i + 2);
    if (size < 200 || size > 400000 || i + size > f.Length) continue;
    int hdr = BitConverter.ToInt32(f, i + 14);
    if (hdr != 40) continue;
    int w = BitConverter.ToInt32(f, i + 18), h = BitConverter.ToInt32(f, i + 22);
    short bpp = BitConverter.ToInt16(f, i + 28);
    if (w <= 0 || h == 0 || w > 2000 || Math.Abs(h) > 2000) continue;
    string name = $"bmp_{i:X6}_{w}x{Math.Abs(h)}_{bpp}bpp.bmp";
    File.WriteAllBytes(System.IO.Path.Combine(outDir, name), f.Skip(i).Take(size).ToArray());
    Console.WriteLine($"  offset 0x{i:X6}  {w,4} x {Math.Abs(h),4}  {bpp,2}bpp  {size,8} bytes");
    saved++;
    i += size - 1;
  }
  Console.WriteLine($"saved {saved} bitmaps to {outDir}");
} }
