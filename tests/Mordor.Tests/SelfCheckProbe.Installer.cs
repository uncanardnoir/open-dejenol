using System.Security.Cryptography;
using System.Text;
using Mordor.Core;
using Mordor.Core.Extraction;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Decklin's Domain's installers, unpacked by the port (src/Mordor.Core/InstallerUnpack.cs,
    /// Extraction/Lzma.cs; docs/GAME_FILES.md, "The installers"). The installers are the player's,
    /// never in this tree: MordorSetup.exe is <c>MORDOR_INSTALLER</c>, else beside the game folder
    /// (as Desktop\mordor\MordorSetup.exe sits beside Desktop\mordor\game_files); the free demo,
    /// MordorDemoSetup.exe, is <c>MORDOR_DEMO_INSTALLER</c>, else beside the game folder too. Without
    /// one, its checks print a skip line. Neither installer is ever run: they are only read. Every
    /// file written goes into a scratch folder, which is deleted after.
    /// </summary>
    private static void InstallerChecks(string dir)
    {
        Console.WriteLine("\nthe installers, unpacked (InstallerUnpack, Lzma; docs/GAME_FILES.md)");

        string? Find(string env, string name)
        {
            string? p = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrWhiteSpace(p)) p = Path.Combine(dir, "..", name);
            return File.Exists(p) ? Path.GetFullPath(p) : null;
        }
        string? setup = Find("MORDOR_INSTALLER", "MordorSetup.exe");
        string? demo = Find("MORDOR_DEMO_INSTALLER", "MordorDemoSetup.exe");

        string scratch = Path.Combine(Path.GetTempPath(), "mordor-unpack-" + Guid.NewGuid().ToString("N")[..8]);
        var envKeep = new[] { InstallerUnpack.EnvRoot, InstallerUnpack.EnvDownloads, GameFolder.EnvDir }
            .ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
        try
        {
            Directory.CreateDirectory(scratch);
            string root = Path.Combine(scratch, "game");
            Environment.SetEnvironmentVariable(InstallerUnpack.EnvRoot, root);
            Environment.SetEnvironmentVariable(GameFolder.EnvDir, null);
            Check("MORDOR_UNPACK_DIR moves where unpacked games go", InstallerUnpack.Root, root);
            Check("one folder for each edition, so the demo never replaces the full game",
                  (InstallerUnpack.EditionDir(root, true), InstallerUnpack.EditionDir(root, false)), (Path.Combine(root, "full"), Path.Combine(root, "shareware")));
            var cands = GameFolder.Candidates(Path.Combine(scratch, "port")).ToList();
            Check("the unpacked games are searched for, after the folders beside the port",
                  (cands.FindIndex(c => c.Dir == Path.Combine(root, "full")) > cands.FindIndex(c => c.How == "the port's folder"),
                   cands.Any(c => c.Dir == Path.Combine(root, "shareware") && c.How == "unpacked from the installer")), (true, true));

            // ---- names: nothing that reaches outside the folder it is put in ----
            foreach (string bad in new[] { "..", ".", "a\\b", "a/b", "C:x", "C:\\x", "\\x", "x:", "CON", "nul.txt", "COM1", "x.", "x ", "a*b", "", "  " })
                Check($"\"{bad}\" is not a plain name", InstallerUnpack.IsPlainName(bad), false);
            foreach (string ok in new[] { "MORDOR.EXE", "b6zol..htm", "midi_mapper___troubleshooting.htm", "SSAVER", "CONFIG.SYS", "COMX" })
                Check($"\"{ok}\" is", InstallerUnpack.IsPlainName(ok), true);

            // ---- the LZMA decoder on what is not LZMA ----
            Check("LZMA: bad properties refused", Throws(() => Lzma.DecodeToEnd(new byte[] { 0xFF, 0, 0, 0, 1 }, new byte[64], 0, 64, 1 << 20, out _)), true);
            Check("LZMA: data that ends too soon refused", Throws(() => Lzma.DecodeToEnd(new byte[] { 0x5D, 0, 0, 1, 0 }, new byte[] { 0, 1, 2 }, 0, 3, 1 << 20, out _)), true);
            var noise = new byte[4096];
            new Random(1995).NextBytes(noise);
            noise[0] = 0;
            Check("LZMA: noise refused, within its limit", Throws(() => Lzma.DecodeToEnd(new byte[] { 0x5D, 0, 0, 1, 0 }, noise, 0, noise.Length, 1 << 16, out _)), true);
            Check("LZMA: a size past the bounds given refused", Throws(() => Lzma.Decode(new byte[] { 0x5D, 0, 0, 1, 0 }, noise, 4000, 200, 10, out _)), true);

            // ---- files that are not the installers: refused, nothing written ----
            string bin = Directory.CreateDirectory(Path.Combine(scratch, "bin")).FullName;
            UnpackResult Refused(string label, byte[] bytes)
            {
                string p = Path.Combine(bin, label + ".exe");
                File.WriteAllBytes(p, bytes);
                var r = InstallerUnpack.Unpack(p, root);
                Console.WriteLine($"  {label}: {r.Message} [{r.Detail}]");
                Check($"{label}: refused with the message, nothing unpacked", (r.Ok, r.Message.StartsWith(InstallerUnpack.NotAnInstaller, StringComparison.Ordinal), r.Files.Count), (false, true, 0));
                // the player's message is plain words and what to choose; why is the Detail, for the crash log
                Check($"{label}: in plain words, the reason kept apart",
                      (r.Message, r.Detail?.StartsWith(label + ".exe: ", StringComparison.Ordinal) == true && r.Detail.Length > label.Length + 6),
                      (InstallerUnpack.NotAnInstaller + " " + InstallerUnpack.NotAnInstallerHint, true));
                Check($"{label}: no folder left in the unpack folder", Directory.Exists(root) ? Directory.GetFileSystemEntries(root).Length : 0, 0);
                return r;
            }
            Refused("an empty file", Array.Empty<byte>());
            Refused("text", Encoding.ASCII.GetBytes("MZ but not really a program at all"));
            Refused("MORDOR.EXE itself (a 16-bit program, no PE header)", File.ReadAllBytes(Path.Combine(dir, "MORDOR.EXE")));
            string? self = Environment.ProcessPath;
            if (self != null && File.Exists(self)) Refused("a Windows program that is no installer (this one)", File.ReadAllBytes(self));
            var missing = InstallerUnpack.Unpack(Path.Combine(bin, "nowhere.exe"), root);
            Check("a file that is not there: refused, not thrown", (missing.Ok, missing.Message.StartsWith(InstallerUnpack.NotAnInstaller, StringComparison.Ordinal)), (false, true));

            if (setup == null)
            {
                Console.WriteLine("  skip  MordorSetup.exe: not found (set MORDOR_INSTALLER, or put it beside the game folder)");
            }
            else
            {
                byte[] exe = File.ReadAllBytes(setup);
                Console.WriteLine($"  MordorSetup.exe: {exe.Length:N0} bytes");

                // ---- the format, as the class says ----
                byte[] arc = InstallerUnpack.Unwrap(exe);
                Console.WriteLine($"  its archive unpacks to {arc.Length:N0} bytes");
                Check("the archive unpacks to tzf3 records", Encoding.ASCII.GetString(arc, 0, 4), "tzf3");
                Check("the first is the InstallMate project (tin9)", Encoding.ASCII.GetString(arc, 0x40, 4), "tin9");

                // ---- damaged copies: refused, nothing written ----
                Refused("MordorSetup.exe cut in half", exe[..(exe.Length / 2)]);
                Refused("MordorSetup.exe cut short by a byte", exe[..^1]);
                var spoilt = (byte[])exe.Clone();
                for (int i = 0; i < 64; i++) spoilt[spoilt.Length / 2 + i] ^= 0x5A;
                Refused("MordorSetup.exe with 64 bytes spoilt in its archive", spoilt);

                // a project that names a file "..\README1" (readme.htm renamed, the same length) and a
                // folder "..\..\" (SSAVER renamed): refused before anything is written
                Check("a file named ..\\README1 in the project: refused",
                      Message(() => InstallerUnpack.ReadArchive(Replace(arc, "readme.htm", "..\\README1"))).Contains("cannot be written safely"), true);
                Check("a folder named ..\\..\\ in the project: refused",
                      Message(() => InstallerUnpack.ReadArchive(Replace(arc, "SSAVER", "..\\..\\"))).Contains("cannot be written safely"), true);
                Check("a folder named C:\\Win in the project: refused",
                      Message(() => InstallerUnpack.ReadArchive(Replace(arc, "SSAVER", "C:\\Win"))).Contains("cannot be written safely"), true);
                Check("and the archive itself still reads", InstallerUnpack.ReadArchive(arc).Files.Count > 0, true);

                // ---- unpacked ----
                var r = InstallerUnpack.Unpack(setup, root);
                Console.WriteLine($"  {r.Message}");
                Console.WriteLine($"  {r.Files.Count} files; skipped: {string.Join(", ", r.Skipped)}");
                Check("MordorSetup.exe unpacks: the full game, into game\\full", (r.Ok, r.Check?.Verdict, r.Dir), (true, GameFilesVerdict.Full, Path.Combine(root, "full")));
                Check("named as the 1.1 release", r.Check?.Recognised, "Mordor 1.1 (full release)");
                Check("only WININIT.INI, the installer's own, is left out", string.Join(", ", r.Skipped), "WININIT.INI (for Windows)");
                string full = Path.Combine(root, "full");
                Check("the data, music and VB3 runtime are beside MORDOR.EXE, as the port reads them",
                      new[] { "MDATA11.MDR", "MG11.DLL", "CITY.MID", "HIT.WAV", "VBRUN300.DLL", "THREED.VBX", "cityw1.png", "MORDOR.HLP" }.All(f => File.Exists(Path.Combine(full, f))), true);
                Check("every file the project names is unpacked: 304, 147 at the top, html 73, SSAVER 84",
                      (r.Files.Count, r.Files.Count(f => !f.Contains(Path.DirectorySeparatorChar)), r.Files.Count(f => f.StartsWith("html" + Path.DirectorySeparatorChar)), r.Files.Count(f => f.StartsWith("SSAVER" + Path.DirectorySeparatorChar))),
                      (304, 147, 73, 84));
                Check("html and SSAVER keep their folders", (File.Exists(Path.Combine(full, "html", "orderinfo.htm")), File.Exists(Path.Combine(full, "SSAVER", "MORDOR.BMP"))), (true, true));
                Check("no working folder is left behind", Directory.GetDirectories(root).Select(Path.GetFileName).OrderBy(n => n).Aggregate("", (a, n) => a + n + " ").Trim(), "full");

                // ---- byte for byte, the game folder's files ----
                if (Sha(Path.Combine(dir, "MORDOR.EXE")) != Sha(Path.Combine(full, "MORDOR.EXE")))
                    Console.WriteLine("  skip  the game folder's files against the unpacked ones: the game folder is not the release this installer holds");
                else
                {
                    int same = 0;
                    var differ = new List<string>();
                    foreach (string f in Directory.GetFiles(dir))
                    {
                        string there = Path.Combine(full, Path.GetFileName(f));
                        if (!File.Exists(there)) continue;
                        if (Sha(f) == Sha(there)) same++; else differ.Add(Path.GetFileName(f));
                    }
                    Console.WriteLine($"  {same} files of the game folder are byte for byte the unpacked ones");
                    Check("every file of the game folder that the installer holds is the same, by SHA-256", string.Join(", ", differ), "");
                    var must = new[] { "MORDOR.EXE", "MG11.DLL", "MDATA1.MDR", "MDATA2.MDR", "MDATA3.MDR", "MDATA5.MDR", "MDATA11.MDR", "MORDOR.HLP" }
                        .Concat(Directory.GetFiles(dir, "*.MID").Select(Path.GetFileName)!)
                        .Concat(Directory.GetFiles(dir, "*.WAV").Select(Path.GetFileName).Where(n => File.Exists(Path.Combine(full, n!))))
                        .ToList();
                    Check("the program, the data, the pictures, the help and the music are all there and the same",
                          must.Where(n => !File.Exists(Path.Combine(full, n!)) || Sha(Path.Combine(dir, n!)) != Sha(Path.Combine(full, n!))).Count(), 0);
                    CheckAtLeast("and the sounds", Directory.GetFiles(full, "*.WAV").Length, 71);
                }

                // ---- again: the same edition replaced, in place ----
                File.WriteAllText(Path.Combine(full, "left-over.txt"), "x");
                var again = InstallerUnpack.Unpack(setup, root);
                Check("unpacked again: the full game's folder replaced, nothing else made",
                      (again.Ok, again.Dir, File.Exists(Path.Combine(full, "left-over.txt")), Directory.GetDirectories(root).Length), (true, full, false, 1));

                // ---- found ----
                var found = GameFolder.Resolve(Path.Combine(scratch, "none.ini"), Path.Combine(scratch, "port"));
                Check("with nothing else about, the unpacked full game is found", (found.IsFull, found.Found?.Dir, found.How), (true, full, "unpacked from the installer"));
            }

            if (demo == null)
            {
                Console.WriteLine("  skip  MordorDemoSetup.exe: not found (set MORDOR_DEMO_INSTALLER, or put it beside the game folder)");
            }
            else
            {
                var r = InstallerUnpack.Unpack(demo, root);
                Console.WriteLine($"  {r.Message}");
                Console.WriteLine($"  {r.Files.Count} files; skipped: {string.Join(", ", r.Skipped)}");
                string share = Path.Combine(root, "shareware");
                Check("MordorDemoSetup.exe unpacks: the shareware, into game\\shareware", (r.Ok, r.Check?.Verdict, r.Dir), (true, GameFilesVerdict.Limited, share));
                Check("its dungeon is the shareware's three levels, 68,166 bytes",
                      (MdrLoader.LoadDungeon(Path.Combine(share, "MDATA11.MDR")).Count, new FileInfo(Path.Combine(share, "MDATA11.MDR")).Length), (3, 68166L));
                if (setup != null)
                {
                    Check("and the full game unpacked before is still there", GameFiles.Check(Path.Combine(root, "full")).IsFull, true);
                    // measured 2026-10-27: the demo's files are the full installer's but for four
                    var differ = Directory.GetFiles(share, "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(share, f))
                        .Where(n => Sha(Path.Combine(share, n)) != Sha(Path.Combine(root, "full", n)))
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
                    Check("the demo's files are the full game's but its program, its dungeon, readme.htm and UNINSTAL.TXT",
                          string.Join(", ", differ), "MDATA11.MDR, MORDOR.EXE, readme.htm, UNINSTAL.TXT");
                }
            }

            // ---- Downloads: offered, never unpacked unasked ----
            string downloads = Directory.CreateDirectory(Path.Combine(scratch, "Downloads")).FullName;
            Check("Downloads with no installer: nothing offered", InstallerUnpack.InDownloads(downloads).Count, 0);
            File.WriteAllText(Path.Combine(downloads, "MordorDemoSetup.exe"), "");
            File.WriteAllText(Path.Combine(downloads, "MordorSetup.exe"), "");
            File.WriteAllText(Path.Combine(downloads, "Other.exe"), "");
            Check("both installers found, the full game's first, nothing else",
                  string.Join(", ", InstallerUnpack.InDownloads(downloads).Select(Path.GetFileName)), "MordorSetup.exe, MordorDemoSetup.exe");
            Environment.SetEnvironmentVariable(InstallerUnpack.EnvDownloads, downloads);
            Check("MORDOR_DOWNLOADS names the Downloads folder", (InstallerUnpack.DownloadsFolder(), InstallerUnpack.InDownloads().Count), (downloads, 2));
            Environment.SetEnvironmentVariable(InstallerUnpack.EnvDownloads, null);
            Check("without it, the Downloads known folder (or the profile's Downloads)", InstallerUnpack.DownloadsFolder()?.Length > 0, true);
        }
        finally
        {
            foreach (var (k, v) in envKeep) Environment.SetEnvironmentVariable(k, v);
            try { Directory.Delete(scratch, true); } catch { }
        }

        static bool Throws(Action a)
        {
            try { a(); return false; }
            catch (InvalidDataException) { return true; }
        }
        static string Message(Action a)
        {
            try { a(); return ""; }
            catch (InvalidDataException ex) { return ex.Message; }
        }
        static string Sha(string path)
        {
            if (!File.Exists(path)) return "";
            using var f = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(f));
        }
        // every place the ASCII text is, replaced by another of the same length
        static byte[] Replace(byte[] a, string from, string to)
        {
            var b = (byte[])a.Clone();
            byte[] f = Encoding.ASCII.GetBytes(from), t = Encoding.ASCII.GetBytes(to);
            for (int i = 0; i + f.Length <= b.Length; i++)
                if (b.AsSpan(i, f.Length).SequenceEqual(f)) t.CopyTo(b, i);
            return b;
        }
    }
}
