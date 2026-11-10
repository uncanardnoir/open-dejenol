using System.IO.Compression;

namespace Mordor.Core;

public static partial class InstallerUnpack
{
    /// <summary>
    /// Decklin's M1FULL.zip contains MORDOR.WIP and WAVES.WIP, both ordinary ZIPs despite their
    /// extension. Read their flat game files, never execute SETUP.EXE or unpack its helpers.
    /// The same validated staging/edition replacement as the newer installer handles placement.
    /// </summary>
    private static Contents ReadLegacyZip(byte[] bytes)
    {
        if (bytes.Length > MaxInstaller) throw new InvalidDataException("the ZIP is too large");
        using var stream = new MemoryStream(bytes, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        int entriesLeft = MaxRecords, bytesLeft = MaxUnpacked;
        var outer = ZipIndex(zip, ref entriesLeft);
        var games = outer.Where(p => p.Key.Split('/')[^1].Equals("MORDOR.WIP", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (games.Length != 1) throw new InvalidDataException("choose the M1FULL.zip package containing one MORDOR.WIP");
        string prefix = games[0].Key[..^"MORDOR.WIP".Length];
        var files = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var read = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in new[] { "MORDOR.WIP", "WAVES.WIP" })
        {
            string path = prefix + name;
            if (!outer.TryGetValue(path, out var archive)) continue; // audio is optional
            read.Add(path);
            byte[] payload = ZipBytes(archive, ref bytesLeft);
            using var innerStream = new MemoryStream(payload, writable: false);
            using var inner = new ZipArchive(innerStream, ZipArchiveMode.Read);
            foreach (var (relative, entry) in ZipIndex(inner, ref entriesLeft))
            {
                CheckName(relative); // these packages contain flat files, not install paths
                var data = ZipBytes(entry, ref bytesLeft);
                if (!files.TryAdd(relative, new Entry(relative, data)))
                    throw new InvalidDataException("two ZIP entries would both be " + relative);
            }
        }
        return new(files.Values.OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            outer.Keys.Where(p => !read.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static Dictionary<string, ZipArchiveEntry> ZipIndex(ZipArchive zip, ref int entriesLeft)
    {
        if (zip.Entries.Count > entriesLeft) throw new InvalidDataException("the ZIP contains too many entries");
        entriesLeft -= zip.Entries.Count;
        var result = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            bool directory = path.EndsWith('/');
            string[] parts = (directory ? path[..^1] : path).Split('/');
            if (parts.Length > MaxDepth) throw new InvalidDataException("ZIP folders are nested too deeply");
            foreach (string part in parts) CheckName(part);
            if (!directory && !result.TryAdd(path, entry))
                throw new InvalidDataException("the ZIP repeats " + path);
        }
        return result;
    }

    private static byte[] ZipBytes(ZipArchiveEntry entry, ref int bytesLeft)
    {
        if (entry.Length < 0 || entry.Length > bytesLeft)
            throw new InvalidDataException("the ZIP expands beyond the unpacking limit");
        bytesLeft -= (int)entry.Length;
        var result = new byte[(int)entry.Length];
        using var source = entry.Open();
        source.ReadExactly(result);
        if (source.ReadByte() != -1) throw new InvalidDataException("a ZIP entry is longer than declared");
        uint crc = 0xFFFFFFFF;
        foreach (byte b in result) crc = ZipCrcTable[(crc ^ b) & 255] ^ (crc >> 8);
        if (~crc != entry.Crc32) throw new InvalidDataException("a ZIP entry failed its checksum");
        return result;
    }

    private static readonly uint[] ZipCrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint crc = (uint)n;
        for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        return crc;
    }).ToArray();
}
