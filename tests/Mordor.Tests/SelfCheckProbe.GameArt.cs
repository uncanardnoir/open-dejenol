using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Mordor.Core.Extraction;

public static partial class SelfCheckProbe
{
    /// <summary>The last commit whose tree carried the game's pictures and help inside the port.</summary>
    private const string BundledArtCommit = "498ef89";

    /// <summary>
    /// The pictures the port used to carry in Mordor.App/Assets, hashed (SHA-256 of their ARGB
    /// pixels, fully transparent ones as 0): what the extraction has to give back. Only the
    /// hashes are kept here, none of the pictures.
    /// </summary>
    private static readonly Dictionary<string, string> BundledPixels = new()
    {
        ["maze_corridor.png"] = "B1696BB5C179DACC",
        ["maze_overlays.png"] = "C54C3F2F54802612",
        ["maze_backdrop.png"] = "DCA1F95679EF65FA",
        ["maze_dark.png"] = "6696AF90DE50F252",
        ["maze_water.png"] = "3E0E68867244DA5A",
        ["maze_quicksand.png"] = "BC2AA4B4D4051AAF",
        ["automap-pics.png"] = "F3751634B560A358",
        ["combat-misc.png"] = "0E823384FD85ED0E",
        ["asmodeus.png"] = "4A3FD2B0C398003A",
        ["monsters.png"] = "2C000E4D38BB5A4F",
        ["menu_background.png"] = "45E4FE4D3323AAFF",
        ["credits_david.png"] = "0C1CB1C64E035881",
        ["town_exit.png"] = "4D0EC060381B616D",
        ["tool_page.png"] = "6FC03E309EA5AFB0",
        ["tool_join.png"] = "79D7B060DBC8877A",
        ["tool_automap.png"] = "015D22C0A2E30A13",
        ["tool_controlpanel.png"] = "A8D4061102E25C48",
        ["tool_dropbody.png"] = "92F7CA61E153770E",
        ["tool_pickup.png"] = "6191153579AEAB7C",
        ["tool_give.png"] = "896E559868A1A7FD",
        ["tool_experience.png"] = "FFB7F6A178EA2344",
        ["tool_help.png"] = "E79600F13FCC698D",
        ["about_icon.png"] = "B0C7C0CF4BC4AF82",
        ["town_store.png"] = "24B2A9A21391BDBA",
        ["town_morgue.png"] = "54D46553B643B4F7",
        ["town_guild.png"] = "50D68F5919A57FCC",
        ["town_dungeon.png"] = "CED1BADE91B99028",
        ["town_confinement.png"] = "8E82318B6447D6C8",
        ["town_seer.png"] = "FB2D04D7BB0FF9E8",
        ["town_bank.png"] = "A8339F37F6E646E2",
    };

    /// <summary>The help as this run extracted it, for <see cref="HelpLinkChecks"/>.</summary>
    private static List<HelpBundle.Topic>? _extractedHelp;

    private static string PixelHash(RgbaImage img)
    {
        var px = img.Pixels.Select(p => (p >> 24) == 0 ? 0u : p).ToArray();
        var bytes = new byte[px.Length * 4];
        Buffer.BlockCopy(px, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes))[..16];
    }

    /// <summary><c>git show &lt;commit&gt;:&lt;path&gt;</c> in the port's repository, or null when git or the commit is not to hand.</summary>
    private static byte[]? GitShow(string portDir, string path)
    {
        try
        {
            var psi = new ProcessStartInfo("git", $"-C \"{portDir}\" show {BundledArtCommit}:{path}")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            var ms = new MemoryStream();
            var copy = p.StandardOutput.BaseStream.CopyToAsync(ms);
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return null; }
            copy.Wait(30000);
            return p.ExitCode == 0 ? ms.ToArray() : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// The port carries none of the game's pictures or help (docs/GAME_FILES.md): they are read out
    /// of the player's copy at start. Here: nothing of the game's is left in the build; extracting
    /// from the game folder given gives back, pixel for pixel, every picture the port used to carry
    /// (their hashes are kept here, and the pictures themselves are compared against the last
    /// commit that had them when git can show it); the help made from MORDOR.HLP is the old bundle's
    /// but for the differences docs/GAME_FILES.md lists; and scratch copies with pieces missing get
    /// blank or substitute art, or the first-run window's reason. Only scratch folders are written.
    /// </summary>
    private static void GameArtChecks(string dir)
    {
        Console.WriteLine("\nthe game's art, read out of the player's copy (Mordor.Core.Extraction, docs/GAME_FILES.md)");
        string portDir = PortTree.Root;

        // ---- nothing of the game's inside the port ----
        string assets = Path.Combine(portDir, "src/Mordor.App", "Assets");
        Check("Mordor.App/Assets holds no files", Directory.Exists(assets) ? Directory.GetFiles(assets, "*", SearchOption.AllDirectories).Length : 0, 0);
        Check("Mordor.App.csproj builds no resources in",
              File.ReadAllText(Path.Combine(portDir, "src/Mordor.App", "Mordor.App.csproj")).Contains("AvaloniaResource", StringComparison.Ordinal), false);
        Check("no wallart folder of MG11.DLL's bitmaps", Directory.Exists(Path.Combine(portDir, "wallart")), false);
        Check("no decompiled copy of the help (docs/original-help)", Directory.Exists(Path.Combine(portDir, "docs", "original-help")), false);
        int avares = Directory.GetFiles(Path.Combine(portDir, "src/Mordor.App"), "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".axaml", StringComparison.Ordinal))
            .Sum(f => File.ReadAllText(f).Split("avares://Mordor.App/").Length - 1);
        Check("pictures loaded from inside the program (avares://Mordor.App/)", avares, 0);

        string scratch = Path.Combine(Path.GetTempPath(), "mordor-art-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // ---- the game folder given: everything, as it was ----
            string outDir = Path.Combine(scratch, "full");
            Directory.CreateDirectory(outDir);
            var watch = Stopwatch.StartNew();
            var notes = GameArt.Extract(dir, outDir);
            Console.WriteLine($"  extracted {GameArt.Pictures.Count} pictures and the help in {watch.ElapsedMilliseconds} ms");
            Check("nothing missing or made up from this copy", string.Join("; ", notes), "");
            Check("pictures the port loads", GameArt.Pictures.Count, BundledPixels.Count);
            int same = 0, compared = 0, gitSame = 0;
            var odd = new List<string>();
            foreach (var p in GameArt.Pictures)
            {
                string path = Path.Combine(outDir, p.Name);
                if (!File.Exists(path)) { odd.Add(p.Name + " not written"); continue; }
                var img = Png.Decode(File.ReadAllBytes(path));
                if (img.Width != p.Width || img.Height != p.Height) odd.Add($"{p.Name} is {img.Width}x{img.Height}");
                if (BundledPixels.TryGetValue(p.Name, out var want) && PixelHash(img) == want) same++;
                else odd.Add(p.Name + " is not the bundled picture");
                var old = GitShow(portDir, "Mordor.App/Assets/" + p.Name);
                if (old == null) continue;
                compared++;
                if (img.SamePixels(Png.Decode(old), out string why)) gitSame++;
                else odd.Add($"{p.Name} against {BundledArtCommit}: {why}");
            }
            Check("pictures pixel for pixel the ones the port carried", $"{same}/{GameArt.Pictures.Count}", $"{BundledPixels.Count}/{BundledPixels.Count}");
            if (compared == 0) Console.WriteLine($"  note  git could not show {BundledArtCommit}: the pictures were checked by their hashes only");
            else Check($"and against the files in {BundledArtCommit}, decoded", $"{gitSame}/{compared}", $"{BundledPixels.Count}/{BundledPixels.Count}");
            Check("pictures wrong", string.Join("; ", odd), "");
            // the sheets the sprite arithmetic indexes into, by shape
            foreach (var (file, size) in new[] { ("automap-pics.png", "405x15"), ("combat-misc.png", "270x45"), ("asmodeus.png", "100x100"), ("monsters.png", "600x540") })
            {
                var (w, h) = PngSize(Path.Combine(outDir, file));
                Check($"{file}, the sheet indexed into", $"{w}x{h}", size);
            }

            // ---- the help ----
            string helpPath = Path.Combine(outDir, GameArt.HelpFile);
            Check("the help is made from MORDOR.HLP", File.Exists(helpPath), true);
            var help = File.Exists(helpPath) ? HelpBundle.Deserialize(File.ReadAllText(helpPath)) : new List<HelpBundle.Topic>();
            _extractedHelp = help;
            var oldJson = GitShow(portDir, "Mordor.App/Assets/help.json");
            if (oldJson == null) Console.WriteLine($"  note  git could not show {BundledArtCommit}'s help.json: the help was checked by its hash only");
            else if (!FullGameOnly("the help against the old bundle, made from the full game's help")) HelpAgainstBundle(help, HelpBundle.Deserialize(Encoding.UTF8.GetString(oldJson)));
            string all = string.Join("\0", help.Select(t => t.Title + "\n" + t.Text));
            FullCheck("help titles and text, hashed", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all)))[..16], "5B64A586CBE6F7E3");
            FullCheck("help text, all topics, in characters", help.Sum(t => (long)t.Text.Length), 248404);

            // ---- the help file's own structure ----
            var hlp = WinHelpFile.Read(Path.Combine(dir, "MORDOR.HLP"));
            var topics = hlp.Topics();
            var titles = hlp.Titles();
            Check("MORDOR.HLP is WinHelp 3.1 (|SYSTEM minor 21), uncompressed", $"{hlp.Minor} {hlp.Flags}", "21 0");
            // |SYSTEM's TITLE, WinHelp's caption: the full 1.1's, and the shareware's as its help window
            // was captioned (observed in the original shareware 2026-10-26)
            Check("its title", hlp.Title, _shareware ? "Mordor HelpLesson" : "M1: Depths of Dejenol Help Lesson");
            Check("the manifest keeps that title for the help window's caption (GameArt.HelpTitle)", GameArt.HelpTitle(outDir), hlp.Title);
            Check("topic offsets worked out from |TOPIC match |TTLBTREE's", topics.Zip(titles).Count(z => z.First.Offset != z.Second.Offset), 0);
            FullCheck("|TTLBTREE topics (the last an untitled empty one)", $"{titles.Count} {titles[^1].Title.Length}", "74 0");
            // cityw1.png is an extra of the 1.1 install, not a file the port needs (GameFiles.Required):
            // the shareware has none, and a copy without it is checked below
            string cityShot = Path.Combine(dir, "cityw1.png");
            if (File.Exists(cityShot))
                Check("CITYW1.BMP inside it is the cityw1.png beside it",
                      Dib.DecodeBmpFile(hlp.File_("CITYW1.BMP")).SamePixels(Png.Decode(File.ReadAllBytes(cityShot)), out _), true);
            else
                Console.WriteLine("  skip  CITYW1.BMP inside it against cityw1.png: this copy has no cityw1.png (an extra of the 1.1 install)");

            // ---- a copy without the 1.1 install's extras: the help's screenshot, then the help itself ----
            string noShot = Path.Combine(scratch, "no-screenshot");
            Directory.CreateDirectory(noShot);
            foreach (var f in new[] { "MORDOR.EXE", "MG11.DLL", "MORDOR.HLP" }) File.Copy(Path.Combine(dir, f), Path.Combine(noShot, f));
            string noShotOut = Path.Combine(scratch, "no-screenshot-art");
            Directory.CreateDirectory(noShotOut);
            Check("no cityw1.png: nothing missing (MORDOR.HLP has the screenshot too)", string.Join("; ", GameArt.Extract(noShot, noShotOut)), "");
            Check("and the city's buttons are the same pictures",
                  new[] { "town_store.png", "town_bank.png", "town_dungeon.png" }.Count(n => PixelHash(Png.Decode(File.ReadAllBytes(Path.Combine(noShotOut, n)))) == BundledPixels[n]), 3);

            string bare = Path.Combine(scratch, "bare");
            Directory.CreateDirectory(bare);
            foreach (var f in new[] { "MORDOR.EXE", "MG11.DLL" }) File.Copy(Path.Combine(dir, f), Path.Combine(bare, f));
            string bareOut = Path.Combine(scratch, "bare-art");
            Directory.CreateDirectory(bareOut);
            var bareNotes = GameArt.Extract(bare, bareOut);
            Check("no help file and no screenshot: said so", bareNotes.Count(n => n.Contains("MORDOR.HLP") || n.Contains("cityw1.png")), 2);
            Check("every picture still made", GameArt.Pictures.Count(p => File.Exists(Path.Combine(bareOut, p.Name))), GameArt.Pictures.Count);
            Check("but no help", File.Exists(Path.Combine(bareOut, GameArt.HelpFile)), false);
            var store = Png.Decode(File.ReadAllBytes(Path.Combine(bareOut, "town_store.png")));
            var exe = NeModule.Read(Path.Combine(dir, "MORDOR.EXE"));
            Check("the Store's button is then frmCityMain's own picture, scaled to 103x75",
                  store.SamePixels(exe.FormPictures("frmCityMain")[6].ScaleNearest(103, 75), out _), true);
            Check("which is the Store, the program's 120x87 of it", $"{exe.FormPictures("frmCityMain").Count} {exe.FormPictures("frmCityMain")[6].Width}x{exe.FormPictures("frmCityMain")[6].Height}", "8 120x87");

            // ---- a picture the copy does not have: blank, and the rest go on ----
            string patched = Path.Combine(scratch, "no-mons");
            Directory.CreateDirectory(patched);
            File.Copy(Path.Combine(dir, "MORDOR.EXE"), Path.Combine(patched, "MORDOR.EXE"));
            var mg = File.ReadAllBytes(Path.Combine(dir, "MG11.DLL"));
            int at = IndexOf(mg, new byte[] { 4, (byte)'M', (byte)'O', (byte)'N', (byte)'S' });
            Check("MG11.DLL names its MONS resource", at >= 0, true);
            if (at >= 0) mg[at + 4] = (byte)'X';
            File.WriteAllBytes(Path.Combine(patched, "MG11.DLL"), mg);
            string patchedOut = Path.Combine(scratch, "no-mons-art");
            Directory.CreateDirectory(patchedOut);
            var patchedNotes = GameArt.Extract(patched, patchedOut);
            Check("an MG11.DLL without MONS: one note, for monsters.png", patchedNotes.Count(n => n.StartsWith("monsters.png: blank")), 1);
            var blank = Png.Decode(File.ReadAllBytes(Path.Combine(patchedOut, "monsters.png")));
            Check("and it is a blank 600x540", $"{blank.Width}x{blank.Height} {blank.Pixels.All(p => p == 0)}", "600x540 True");
            Check("the maze still read", PixelHash(Png.Decode(File.ReadAllBytes(Path.Combine(patchedOut, "maze_corridor.png")))), BundledPixels["maze_corridor.png"]);

            // ---- what stops the game: MG11.DLL gone or unreadable, as the original stops ----
            string noDll = Path.Combine(scratch, "no-dll");
            Directory.CreateDirectory(noDll);
            File.Copy(Path.Combine(dir, "MORDOR.EXE"), Path.Combine(noDll, "MORDOR.EXE"));
            var gone = GameArt.Prepare(noDll, Path.Combine(scratch, "cache"));
            Check("no MG11.DLL: no art, and the reason names it", gone.Ok ? "ok" : gone.Error!.StartsWith("MG11.DLL is missing") ? "MG11.DLL" : gone.Error, "MG11.DLL");
            File.WriteAllBytes(Path.Combine(noDll, "MG11.DLL"), mg.AsSpan(0, 2000).ToArray());
            var broken = GameArt.Prepare(noDll, Path.Combine(scratch, "cache"));
            Check("a cut-short MG11.DLL: no art, and a reason", broken.Ok ? "ok" : broken.Error!.StartsWith("The game's pictures could not be read") ? "reason" : broken.Error, "reason");
            Check("and no half-made cache left behind", Directory.Exists(Path.Combine(scratch, "cache")) ? Directory.GetDirectories(Path.Combine(scratch, "cache")).Length : 0, 0);

            // ---- the cache ----
            string cache = Path.Combine(scratch, "cache2");
            var first = GameArt.Prepare(dir, cache);
            Check("the cache for this copy is made", first.Ok, true);
            string manifest = Path.Combine(first.Dir ?? "", GameArt.ManifestFile);
            var stamp = File.Exists(manifest) ? File.GetLastWriteTimeUtc(manifest) : default;
            var again = GameArt.Prepare(dir, cache);
            Check("and found, not made again, the next time", (again.Dir == first.Dir, File.Exists(manifest) && File.GetLastWriteTimeUtc(manifest) == stamp).ToString(), "(True, True)");
            Check("named by a hash of the files it came from", Path.GetFileName(first.Dir ?? ""), GameArt.Key(dir));
            // (a copy without cityw1.png is another copy only when this one has it: the shareware has not)
            Check("which another copy's files do not share", GameArt.Key(bare) != GameArt.Key(dir) && (GameArt.Key(noShot) != GameArt.Key(dir)) == File.Exists(cityShot), true);
            Check("the cache is per user unless moved (MORDOR_ART_CACHE)",
                  Environment.GetEnvironmentVariable("MORDOR_ART_CACHE") != null
                  || GameArt.CacheRoot.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase), true);
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); } catch { }
        }
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= hay.Length; i++)
            if (hay.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }

    /// <summary>
    /// The help made from MORDOR.HLP against the bundle the port carried (made from the CHM that the
    /// 2015 edition's help tool generated beside it). The same but for what docs/GAME_FILES.md lists:
    /// the CHM's undecodable bytes (U+FFFD) - a copyright sign the .HLP has as such, and trap symbols
    /// it does not have at all - and the Spells topic's list of spell classes, which the .HLP lays
    /// out in two tabbed columns where the CHM gave one list.
    /// </summary>
    private static void HelpAgainstBundle(List<HelpBundle.Topic> help, List<HelpBundle.Topic> old)
    {
        Check("help topics, as many as the old bundle's", help.Count, old.Count);
        int n = Math.Min(help.Count, old.Count);
        Check("titles that differ", string.Join(", ", Enumerable.Range(0, n).Where(i => help[i].Title != old[i].Title).Select(i => help[i].Title)), "");
        static string Plain(string s, bool mine) => mine ? new string(s.Where(c => c < 0x80).ToArray()) : s.Replace("�", "");
        var differ = Enumerable.Range(0, n).Where(i => Plain(help[i].Text, true) != Plain(old[i].Text, false)).Select(i => help[i].Title).ToList();
        Check("topics whose text differs (bar the CHM's U+FFFD)", string.Join(", ", differ), "Spells");
        int spells = help.FindIndex(t => t.Title == "Spells");
        static string Words(string s) => string.Join(" ", s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).OrderBy(w => w, StringComparer.Ordinal));
        if (spells >= 0) Check("Spells: the same words, in two columns", Words(help[spells].Text) == Words(old[spells].Text), true);
        static string Rows(List<HelpBundle.TabRow>? t) => t == null ? "" : string.Join(" | ", t.Select(r => r.Start + ":" + string.Join(",", r.Stops)));
        Check("topics whose tab stops differ", string.Join(", ", Enumerable.Range(0, n).Where(i => Rows(help[i].Tabs) != Rows(old[i].Tabs)).Select(i => help[i].Title)), "Spells");
        static IEnumerable<string> Jumps(HelpBundle.Topic t) => (t.Links ?? new()).Select(l => t.Text.Substring(l.Start, l.Length).Replace("�", "") + ">" + l.To);
        Check("topics whose jumps differ (words and target)", string.Join(", ", Enumerable.Range(0, n).Where(i => !Jumps(help[i]).SequenceEqual(Jumps(old[i]))).Select(i => help[i].Title)), "Spells");
        if (spells >= 0)
            Check("Spells: the same jumps, in the columns' order", string.Join(" ", Jumps(help[spells]).OrderBy(j => j, StringComparer.Ordinal)) == string.Join(" ", Jumps(old[spells]).OrderBy(j => j, StringComparer.Ordinal)), true);
        Check("U+FFFD in the old bundle, and in this one", $"{old.Sum(t => t.Text.Count(c => c == '�'))} {help.Sum(t => t.Text.Count(c => c == '�'))}", "12 0");
        Check("the copyright sign the .HLP has", help[0].Text.Contains("MORDOR is © Copyright"), true);
    }

    /// <summary>
    /// The help's jumps. MORDOR.HLP's topics jump to one another - the Welcome page is a list of
    /// them - and the extracted help keeps each beside the text as a span leading to a topic.
    /// </summary>
    private static void HelpLinkChecks(string dir)
    {
        Console.WriteLine("\nthe help's jumps (extracted from MORDOR.HLP)");
        string portDir = PortTree.Root;
        var shipped = _extractedHelp;
        if (shipped == null || shipped.Count == 0) { Console.WriteLine("  FAIL  no help to check"); _failed++; return; }
        var hlp = WinHelpFile.Read(Path.Combine(dir, "MORDOR.HLP"));
        HelpBundle.FromHelpFile(hlp, out var report);
        Check("help topics", shipped.Count, 73);
        Check("help topics sharing a title", string.Join(", ", shipped.GroupBy(t => t.Title).Where(g => g.Count() > 1).Select(g => g.Key)), "");
        var links = shipped.SelectMany((t, i) => (t.Links ?? new()).Select(l => (From: i, l.Start, l.Length, l.To))).ToList();
        Check("jumps in the help file", report.LinksInFile, 103);
        Check("jumps in the help", links.Count, 103);
        Check("jumps to no topic", string.Join("; ", report.Unresolved), "");
        Check("jumps that could not be placed in the text", string.Join("; ", report.Misplaced), "");
        Check("jumps to a topic out of range", links.Count(l => l.To < 0 || l.To >= shipped.Count), 0);
        Check("jumps with white space at an end, or outside the text", links.Count(l =>
            l.Start < 0 || l.Length <= 0 || l.Start + l.Length > shipped[l.From].Text.Length
            || char.IsWhiteSpace(shipped[l.From].Text[l.Start]) || char.IsWhiteSpace(shipped[l.From].Text[l.Start + l.Length - 1])), 0);

        string Jump(string from, string words)
        {
            int i = shipped.FindIndex(t => t.Title == from);
            if (i < 0) return "no topic " + from;
            var l = shipped[i].Links?.FirstOrDefault(l => shipped[i].Text.Substring(l.Start, l.Length) == words);
            return l == null ? "no jump" : shipped[l.To].Title;
        }
        Check("Welcome's \"Commands\" leads to", Jump("Welcome", "Commands"), "Commands");
        // one inside a tabbed row, and the one jump into a secondary window (0xEB)
        Check("Welcome's \"Information\" (in a tabbed row) leads to", Jump("Welcome", "Information"), "Mordor Screen Saver");
        Check("Welcome's \"Tutorial\" (into the tutorial window) leads to", Jump("Welcome", "Tutorial"), "Tutorial");
        Check("Spells' \"Fire\" leads to", Jump("Spells", "Fire"), "Fire Spells");
        Check("Spells' \"Heal\", the second column, leads to", Jump("Spells", "Heal"), "Heal Spells");

        // Where the port opens the help: F1 and the Help buttons open it where WinHelp opened it for
        // HELP_CONTENTS (FRMMAIN.BAS:75-76), the Contents topic |SYSTEM names - Welcome, in the full
        // game's MORDOR.HLP (offset 0, its first topic) and the shareware's (0x26896D, its last topic, the 73rd) - and
        // a few buttons open it at a topic by name, which HelpWindow takes as the first topic whose
        // title contains it. Every such name in the source must land on the topic it names.
        Console.WriteLine($"  |SYSTEM's Contents topic offset: {(hlp.ContentsOffset is int co ? "0x" + co.ToString("X") : "none")}");
        Check("one topic is marked as the Contents topic", shipped.Count(t => t.Contents == true), 1);
        Check("the help opens at (no topic named), the Contents topic", shipped[HelpBundle.ContentsIndex(shipped)].Title, "Welcome");
        var fresh = HelpBundle.FromHelpFile(hlp);
        Check("which is the topic |SYSTEM's Contents offset falls in, or the first without one",
              HelpBundle.ContentsIndex(fresh), hlp.ContentsOffset is int off ? hlp.Titles().FindLastIndex(t => t.Offset <= off) : 0);
        var named = new List<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(portDir, "src/Mordor.App"), "*.cs"))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         File.ReadAllText(f), "new HelpWindow\\(\"(?<t>[^\"]+)\"\\)"))
                named.Add(m.Groups["t"].Value);
        CheckAtLeast("help topics the port opens by name", named.Distinct().Count(), 3);
        Check("of those, ones that land elsewhere", string.Join(", ", named.Distinct().Where(n =>
            shipped.FirstOrDefault(t => t.Title.Contains(n, StringComparison.OrdinalIgnoreCase))?.Title != n)), "");
    }
}
