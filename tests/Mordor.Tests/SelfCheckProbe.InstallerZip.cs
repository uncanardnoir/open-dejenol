using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    private static void LegacyInstallerChecks(string game)
    {
        Console.WriteLine("\nM1FULL.zip: nested WIP archives, validation and edition placement:");
        byte[] Zip(params (string Name, byte[] Bytes)[] files)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var (name, data) in files)
                { using var entry = zip.CreateEntry(name, CompressionLevel.NoCompression).Open(); entry.Write(data); }
            return stream.ToArray();
        }
        byte[] text = Encoding.ASCII.GetBytes("fixture-payload");
        byte[] inner = Zip(("MORDOR.EXE", text));
        byte[] Package(byte[] payload) => Zip(("M1FULL/MORDOR.WIP", payload));
        bool Refused(byte[] bytes)
        {
            try { InstallerUnpack.Parse(bytes); return false; }
            catch (Exception ex) when (ex is InvalidDataException or IOException) { return true; }
        }
        var content = InstallerUnpack.Parse(Zip(("M1FULL/MORDOR.WIP", inner),
            ("M1FULL/WAVES.WIP", Zip(("FALL.WAV", text))), ("M1FULL/SETUP.EXE", text), ("M1FULL/SSAVER.WIP", text)));
        Check("ZIP: game/audio files combined; installer/screensaver skipped",
            (string.Join(",", content.Files.Select(f => f.RelativePath)), content.Skipped.Count), ("FALL.WAV,MORDOR.EXE", 2));
        Check("ZIP: unpacked bytes are exact", content.Files.All(f => f.Data.SequenceEqual(text)), true);
        Check("ZIP: root layout and case variations work without optional audio",
            InstallerUnpack.Parse(Zip(("mordor.wip", inner))).Files.Count, 1);
        Check("ZIP: backslash wrapper paths normalize safely",
            InstallerUnpack.Parse(Zip(("M1FULL\\MORDOR.WIP", inner))).Files.Count, 1);
        Check("ZIP: missing game archive refused", Refused(Zip(("SETUP.EXE", text))), true);
        Check("ZIP: ambiguous game archives refused", Refused(Zip(("A/MORDOR.WIP", inner), ("B/MORDOR.WIP", inner))), true);
        foreach (string path in new[] { "../escape", "C:/escape", "/absolute", "CON", "bad.", "dir/file" })
            Check("ZIP: unsafe/non-flat game name refused: " + path, Refused(Package(Zip((path, text)))), true);
        Check("ZIP: unsafe outer paths refused", Refused(Zip(("MORDOR.WIP", inner), ("../escape", text))), true);
        Check("ZIP: case-insensitive duplicate inner names refused", Refused(Package(Zip(("item", text), ("ITEM", text)))), true);
        Check("ZIP: colliding game/audio files refused", Refused(Zip(("MORDOR.WIP", inner), ("WAVES.WIP", inner))), true);
        byte[] damaged = (byte[])inner.Clone();
        int body = 30 + "MORDOR.EXE".Length;
        damaged[body] ^= 1;
        Check("ZIP: corrupt stored bytes fail CRC validation", Refused(Package(damaged)), true);
        byte[] oversized = (byte[])inner.Clone();
        int central = Array.FindIndex(Enumerable.Range(0, oversized.Length - 3).ToArray(),
            i => oversized.AsSpan(i, 4).SequenceEqual("PK\x01\x02"u8));
        BinaryPrimitives.WriteUInt32LittleEndian(oversized.AsSpan(central + 24, 4), 129u << 20);
        Check("ZIP: oversized declared entry refused before allocation", Refused(Package(oversized)), true);
        Check("ZIP: truncated archive refused", Refused(Package(inner)[..^12]), true);
        Check("ZIP: excessive entry count refused", Refused(Zip(Enumerable.Range(0, 10000)
            .Select(i => ("f" + i, Array.Empty<byte>())).Append(("MORDOR.WIP", inner)).ToArray())), true);

        string scratch = Path.Combine(Path.GetTempPath(), "mordor-zip-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var originals = GameFiles.Required.Append("MG11.DLL").Where(n => File.Exists(Path.Combine(game, n)))
                .Select(n => (n, File.ReadAllBytes(Path.Combine(game, n)))).ToArray();
            string fixture = Path.Combine(scratch, "fixture.zip");
            File.WriteAllBytes(fixture, Package(Zip(originals)));
            var placed = InstallerUnpack.Unpack(fixture, Path.Combine(scratch, "fixture-game"));
            Check("ZIP: normal staged unpack produces a playable full game", placed.Ok && placed.Check is { IsFull: true }, true);
            foreach (string name in InstallerUnpack.InstallerNames) File.WriteAllText(Path.Combine(scratch, name), "");
            Check("ZIP: Downloads discovery offers the ZIP before the demo",
                string.Join(",", InstallerUnpack.InDownloads(scratch).Select(Path.GetFileName)), "MordorSetup.exe,M1FULL.zip,MordorDemoSetup.exe");
            if (Environment.GetEnvironmentVariable("MORDOR_LEGACY_INSTALLER") is { Length: > 0 } real)
            {
                byte[] before = SHA256.HashData(File.ReadAllBytes(real));
                var result = InstallerUnpack.Unpack(real, Path.Combine(scratch, "real-game"));
                Check("ZIP: actual user package is recognized as the full game", result.Ok && result.Check is { IsFull: true, IsPlayable: true }, true);
                Check("ZIP: actual package's core files match the known game", result.Ok && originals.All(f =>
                    File.ReadAllBytes(Path.Combine(result.Dir!, f.n)).SequenceEqual(f.Item2)), true);
                Check("ZIP: original package is unchanged", before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(real))), true);
                File.WriteAllBytes(fixture, Package(damaged));
                var failed = InstallerUnpack.Unpack(fixture, Path.Combine(scratch, "real-game"));
                Check("ZIP: failed replacement preserves the installed edition", !failed.Ok && result.Dir != null
                    && GameFiles.Check(result.Dir).IsPlayable, true);
            }
        }
        finally
        {
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new InvalidOperationException("Unexpected ZIP test cleanup path.");
            Directory.Delete(scratch, true);
        }
    }
}
