using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mordor.Core.Extraction;

/// <summary>What <see cref="GameArt.Prepare"/> made of a game folder: the art's folder, or why there is none.</summary>
public sealed record GameArtResult(string? Dir, string? Error, IReadOnlyList<string> Notes)
{
    public bool Ok => Dir != null;
}

/// <summary>
/// The pictures and the help the port shows, read out of the player's own copy of Mordor at
/// start. The port ships none of them: on first run (or when the game files change) they are
/// extracted into a per-user cache, <c>%LOCALAPPDATA%\OpenDejenol\cache\&lt;hash&gt;</c>, named by a
/// hash of the files they came from and of this extractor's version, so a cache is never used for
/// files it did not come from. <c>MORDOR_ART_CACHE</c> moves the cache (the checks use a scratch one).
///
/// <para>Where each comes from (docs/GAME_FILES.md has the table):</para>
/// <list type="bullet">
/// <item>MG11.DLL's "MORDOR" resources - MAZE, DOOR, BLNK, FOG, WATR, QSND, PICS, MISC, ASMO and MONS
/// (MONS is run-length encoded) - the dungeon view, the automap's icons, the monster window's
/// pictures and the monster portraits. The original will not start without MG11.DLL ("Cannot
/// locate graphics file", MODULE12.BAS:71-75), and neither does this.</item>
/// <item>MORDOR.EXE: the pictures Visual Basic keeps in its forms - frmMenu's background,
/// frmCredits' portrait, frmCityMain's Exit sign and its seven building pictures, frmMain's nine
/// toolbar buttons - and the program's icon (About).</item>
/// <item>The city's building buttons as the port draws them, 103x75: cut from the screenshot of the
/// City window the 1.1 install carries (cityw1.png, or CITYW1.BMP inside MORDOR.HLP); a copy with
/// neither gets the program's own 120x87 pictures scaled down.</item>
/// <item>MORDOR.HLP: the help (<see cref="HelpBundle"/>).</item>
/// </list>
/// Anything else that is missing or does not read becomes a blank picture of the right size, and a
/// note says so; the game goes on.
/// </summary>
public static class GameArt
{
    /// <summary>
    /// Bumped whenever what is extracted changes, so an old cache is not reused. 2: help.json marks
    /// the help file's Contents topic (<see cref="HelpBundle.Topic.Contents"/>).
    /// </summary>
    public const int Version = 2;

    public const string HelpFile = "help.json";
    public const string ManifestFile = "extracted.json";

    /// <summary>A file the art comes out of; <see cref="Required"/> ones stop the extraction when absent.</summary>
    public sealed record Source(string Name, bool Required, string Why);

    public static readonly Source[] Sources =
    {
        new("MORDOR.EXE", true, "the menu, credits, toolbar and city pictures"),
        new("MG11.DLL", true, "the dungeon, monster and automap pictures"),
        new("MORDOR.HLP", false, "the help"),
        new("cityw1.png", false, "the city's building buttons, as the 1.1 install shows them"),
    };

    /// <summary>One picture: its file name, its size, and how to make it.</summary>
    public sealed record Picture(string Name, int Width, int Height, string From);

    /// <summary>The town's building buttons in the City window's screenshot, and their place in frmCityMain.</summary>
    private static readonly (string Name, int X, int Y, int FormPicture)[] Town =
    {
        ("town_store.png", 12, 27, 6),
        ("town_morgue.png", 130, 27, 4),
        ("town_guild.png", 249, 27, 2),
        ("town_dungeon.png", 368, 27, 1),
        ("town_confinement.png", 12, 128, 7),
        ("town_seer.png", 130, 128, 3),
        ("town_bank.png", 249, 128, 5),
    };

    /// <summary>frmMain's toolbar buttons, in the order the form holds their pictures.</summary>
    private static readonly string[] Toolbar =
    {
        "tool_page.png", "tool_join.png", "tool_automap.png", "tool_controlpanel.png", "tool_dropbody.png",
        "tool_pickup.png", "tool_give.png", "tool_experience.png", "tool_help.png",
    };

    /// <summary>MG11.DLL's resources of type "MORDOR" and the file each becomes.</summary>
    private static readonly (string Resource, string Name, int Width, int Height)[] Mg11 =
    {
        ("MAZE", "maze_corridor.png", 1250, 200),
        ("DOOR", "maze_overlays.png", 200, 79),
        ("BLNK", "maze_backdrop.png", 100, 100),
        ("FOG", "maze_dark.png", 50, 50),
        ("WATR", "maze_water.png", 50, 20),
        ("QSND", "maze_quicksand.png", 50, 18),
        ("PICS", "automap-pics.png", 405, 15),
        ("MISC", "combat-misc.png", 270, 45),
        ("ASMO", "asmodeus.png", 100, 100),
        ("MONS", "monsters.png", 600, 540),
    };

    /// <summary>Every picture the port loads, with where it comes from.</summary>
    public static IReadOnlyList<Picture> Pictures { get; } = BuildList();

    private static List<Picture> BuildList()
    {
        var l = new List<Picture>();
        foreach (var m in Mg11) l.Add(new Picture(m.Name, m.Width, m.Height, $"MG11.DLL, resource MORDOR/{m.Resource}"));
        l.Add(new Picture("menu_background.png", 600, 400, "MORDOR.EXE, frmMenu's picture"));
        l.Add(new Picture("credits_david.png", 150, 150, "MORDOR.EXE, frmCredits' picture"));
        l.Add(new Picture("town_exit.png", 120, 42, "MORDOR.EXE, frmCityMain's first picture (the Exit sign)"));
        foreach (var (t, i) in Toolbar.Select((t, i) => (t, i)))
            l.Add(new Picture(t, 20, 20, $"MORDOR.EXE, frmMain's 20x20 picture {i + 1} of 9"));
        l.Add(new Picture("about_icon.png", 32, 32, "MORDOR.EXE, the program's icon (RT_ICON)"));
        foreach (var t in Town)
            l.Add(new Picture(t.Name, 103, 75, $"cityw1.png at ({t.X},{t.Y}), else MORDOR.HLP's CITYW1.BMP, else frmCityMain's picture {t.FormPicture + 1} scaled"));
        return l;
    }

    /// <summary>Where the caches live: <c>MORDOR_ART_CACHE</c>, else <c>%LOCALAPPDATA%\OpenDejenol\cache</c>.</summary>
    public static string CacheRoot
    {
        get
        {
            string? env = Environment.GetEnvironmentVariable("MORDOR_ART_CACHE");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            // UserFolders.Local falls back to the temporary folder, as this did, with no local application data
            return Path.Combine(UserFolders.Local, "cache");
        }
    }

    /// <summary>A file of the game folder, whatever the case of its name; null when absent.</summary>
    public static string? Find(string gameDir, string name)
    {
        string direct = Path.Combine(gameDir, name);
        if (File.Exists(direct)) return direct;
        try
        {
            return Directory.EnumerateFiles(gameDir).FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }

    /// <summary>The cache's name: the extractor's version and every source file present, hashed.</summary>
    public static string Key(string gameDir)
    {
        using var sha = SHA256.Create();
        var all = new StringBuilder($"mordor-art v{Version}\n");
        foreach (var s in Sources)
        {
            string? p = Find(gameDir, s.Name);
            if (p == null) { all.Append(s.Name).Append(" absent\n"); continue; }
            using var f = File.OpenRead(p);
            all.Append(s.Name).Append(' ').Append(Convert.ToHexString(SHA256.HashData(f))).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all.ToString())))[..16].ToLowerInvariant();
    }

    /// <summary>
    /// The art for this game folder, extracting it first if the cache does not have it. Never
    /// throws: a failure comes back as <see cref="GameArtResult.Error"/>, in words for the player.
    /// </summary>
    public static GameArtResult Prepare(string gameDir, string? cacheRoot = null)
    {
        try
        {
            foreach (var s in Sources.Where(s => s.Required))
                if (Find(gameDir, s.Name) == null)
                    return new GameArtResult(null, $"{s.Name} is missing from {gameDir}: {s.Why} are in it, and the game cannot start without it.", Array.Empty<string>());
            string root = cacheRoot ?? CacheRoot;
            string dir = Path.Combine(root, Key(gameDir));
            string manifest = Path.Combine(dir, ManifestFile);
            if (Complete(dir))
                return new GameArtResult(dir, null, ReadNotes(manifest));

            Directory.CreateDirectory(root);
            string temp = dir + ".part-" + Guid.NewGuid().ToString("N")[..8];
            Directory.CreateDirectory(temp);
            List<string> notes;
            try { notes = Extract(gameDir, temp); }
            catch { TryDelete(temp); throw; }
            if (Directory.Exists(dir)) TryDelete(dir);    // an earlier, unfinished one
            try { Directory.Move(temp, dir); }
            catch (IOException) when (File.Exists(manifest)) { TryDelete(temp); }  // another start got there first
            return new GameArtResult(dir, null, notes);
        }
        catch (Exception ex)
        {
            return new GameArtResult(null, "The game's pictures could not be read from " + gameDir + ": " + ex.Message, Array.Empty<string>());
        }
    }

    /// <summary>A cache folder with its manifest, every picture, and the help if the manifest says it was made.</summary>
    private static bool Complete(string dir)
    {
        string manifest = Path.Combine(dir, ManifestFile);
        if (!File.Exists(manifest) || !Pictures.All(p => File.Exists(Path.Combine(dir, p.Name)))) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            bool help = doc.RootElement.TryGetProperty("help", out var h) && h.ValueKind == JsonValueKind.True;
            return !help || File.Exists(Path.Combine(dir, HelpFile));
        }
        catch (JsonException) { return false; }
    }

    private static IReadOnlyList<string> ReadNotes(string manifest)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            return doc.RootElement.TryGetProperty("notes", out var n) ? n.EnumerateArray().Select(e => e.GetString() ?? "").ToList() : new List<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    /// <summary>
    /// Extract every picture and the help into <paramref name="outDir"/>; the notes say what was
    /// missing or made up. Throws when a required file does not read at all.
    /// </summary>
    public static List<string> Extract(string gameDir, string outDir)
    {
        var notes = new List<string>();
        var made = new Dictionary<string, RgbaImage>();
        string Need(string name) => Find(gameDir, name) ?? throw new FileNotFoundException($"{name} is missing", name);

        void Put(string name, Func<RgbaImage> make)
        {
            var pic = Pictures.First(p => p.Name == name);
            RgbaImage img;
            try
            {
                img = make();
                if (img.Width != pic.Width || img.Height != pic.Height)
                    throw new InvalidDataException($"it is {img.Width}x{img.Height}, not {pic.Width}x{pic.Height}");
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IndexOutOfRangeException or FileNotFoundException)
            {
                notes.Add($"{name}: blank - {pic.From} did not read ({ex.Message})");
                img = new RgbaImage(pic.Width, pic.Height);
            }
            made[name] = img;
        }

        // MG11.DLL
        var mg11 = NeModule.Read(Need("MG11.DLL"));
        int noted = notes.Count;
        foreach (var (res, name, _, _) in Mg11)
            Put(name, () =>
            {
                var r = mg11.Find("MORDOR", res) ?? throw new InvalidDataException($"no resource {res}");
                return Dib.DecodeBmpFile(mg11.Bytes, r.Offset);
            });
        // a picture or two can be left blank; an MG11.DLL with none of them is not the game's
        if (notes.Count - noted == Mg11.Length) throw new InvalidDataException("MG11.DLL holds none of the game's pictures");

        // MORDOR.EXE
        var exe = NeModule.Read(Need("MORDOR.EXE"));
        RgbaImage FormPicture(string form, int index, int w = 0, int h = 0)
        {
            var pics = exe.FormPictures(form);
            if (w > 0) pics = pics.Where(p => p.Width == w && p.Height == h).ToList();
            if (index >= pics.Count) throw new InvalidDataException($"{form} has no picture {index + 1}");
            return pics[index];
        }
        Put("menu_background.png", () => FormPicture("frmMenu", 0));
        Put("credits_david.png", () => FormPicture("frmCredits", 0));
        Put("town_exit.png", () => FormPicture("frmCityMain", 0));
        for (int i = 0; i < Toolbar.Length; i++)
        {
            int k = i;
            Put(Toolbar[k], () => FormPicture("frmMain", k, 20, 20));
        }
        Put("about_icon.png", () =>
        {
            var icon = exe.Resources.FirstOrDefault(r => r.Type == "#" + NeModule.RtIcon && BitConverter.ToInt32(exe.Bytes, r.Offset + 4) == 32)
                       ?? throw new InvalidDataException("the program has no 32x32 icon");
            return Dib.DecodeIcon(exe.Bytes, icon.Offset);
        });

        // the town's buttons: the 1.1 install's screenshot of the City window, else the help's copy
        // of it, else the program's own pictures
        RgbaImage? city = null;
        string? shot = Find(gameDir, "cityw1.png");
        if (shot != null)
        {
            try { city = Png.Decode(File.ReadAllBytes(shot)); }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException) { notes.Add("cityw1.png does not read: " + ex.Message); }
        }
        WinHelpFile? hlp = null;
        string? hlpPath = Find(gameDir, "MORDOR.HLP");
        if (hlpPath != null)
        {
            try { hlp = WinHelpFile.Read(hlpPath); }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException) { notes.Add("MORDOR.HLP does not read: " + ex.Message); }
        }
        if (city == null && hlp != null && hlp.Has("CITYW1.BMP"))
        {
            try { city = Dib.DecodeBmpFile(hlp.File_("CITYW1.BMP")); }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException) { notes.Add("MORDOR.HLP's CITYW1.BMP does not read: " + ex.Message); }
        }
        if (city != null && (city.Width < 471 || city.Height < 203)) { notes.Add("the City screenshot is not the expected 487x229"); city = null; }
        if (city == null) notes.Add("the city's buttons are the program's own pictures, scaled: no screenshot of the City window (cityw1.png) here");
        foreach (var t in Town)
            Put(t.Name, () => city != null
                ? city.Crop(t.X, t.Y, 103, 75)
                : FormPicture("frmCityMain", t.FormPicture).ScaleNearest(103, 75));

        foreach (var (name, img) in made) File.WriteAllBytes(Path.Combine(outDir, name), Png.Encode(img));

        // the help
        if (hlp != null)
        {
            try
            {
                var topics = HelpBundle.FromHelpFile(hlp, out var report);
                foreach (var u in report.Unresolved) notes.Add("help: a jump to no topic, " + u);
                foreach (var m in report.Misplaced) notes.Add("help: " + m);
                File.WriteAllText(Path.Combine(outDir, HelpFile), HelpBundle.Serialize(topics));
            }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException or FormatException)
            {
                notes.Add("MORDOR.HLP's topics do not read: " + ex.Message);
            }
        }
        else if (hlpPath == null) notes.Add("no help: MORDOR.HLP is not here");

        var manifest = new
        {
            version = Version,
            game = Path.GetFullPath(gameDir),
            made = DateTime.UtcNow.ToString("u"),
            pictures = Pictures.Select(p => new { name = p.Name, from = p.From }),
            help = File.Exists(Path.Combine(outDir, HelpFile)),
            notes,
        };
        File.WriteAllText(Path.Combine(outDir, ManifestFile), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return notes;
    }
}
