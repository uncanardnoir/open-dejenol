namespace Mordor.Core;

/// <summary>
/// The Options window's settings, <c>gv1A66</c> and the two volumes. Unlike
/// <see cref="CharacterOptions"/> these are the game's, not the character's: they live in
/// MORDOR.INI under <c>[Loading Information]</c>, and <c>FRMOPTIO</c>'s OK button writes every one
/// of them there by name (FRMOPTIO.BAS:152-247). That MORDOR.INI was the WINDOWS directory's
/// (gv19E4, MODULE1.BAS:920-926); the port writes the same keys to its own settings file in the
/// saves directory (<see cref="PortSettings"/>) and only reads the game directory's MORDOR.INI.
///
/// The twelve checkboxes, in the order the form declares them, with the field each one sets and the
/// key it is written under:
///
/// <code>
///  0  M5EE9  No3d            No 3-D
///  1  M5EF1  NoMusic         No Music
///  2  M5EFC  NoFX            No Sound FX
///  3  M5F04  NoStretch       No StretchBlt
///  4  M5F39  SoundCanvas     Use Sound Canvas MIDI
///  5  M5F11  NoDing          No Msg. Ding
///  6  M5F1B  NoLoadAutomap   No Load Automap
///  7  M5F2C  NoToolbar       No Toolbar
///  8  M5F48  NoPartyFX       No Party Sound
///  9  M5F71  NoHelpSound     No HELP Sound
/// 10  M5F80  NoAutoSave      No Autosave
/// 11  M5F8E  NoBlinkCursor   No Automap Cursor Blink
/// </code>
///
/// The captions are the help's screenshot of the window, game_files/bm3.png, and every one of the
/// pairings above is fixed by the INI key the OK button writes beside the field.
///
/// One slip is kept: the True branch writes <c>NoToolBar</c> with a capital B and the False branch
/// writes <c>NoToolbar</c>. Windows INI keys are case-insensitive so nothing came of it, and
/// <see cref="Ini"/> matches case-insensitively for the same reason.
/// </summary>
public sealed class GameOptions
{
    /// <summary>M5EE9. Turns Ctl3D off - the Windows 3.1 sunken-control look, not the maze.</summary>
    public bool No3d { get; set; }
    /// <summary>M5EF1.</summary>
    public bool NoMusic { get; set; }
    /// <summary>M5EFC.</summary>
    public bool NoFX { get; set; }
    /// <summary>
    /// M5F04. Pictures are drawn at their own size instead of stretched to the space
    /// (MODULE3.BAS:1476 and MODULE6.BAS:4954 both pick StretchBlt or BitBlt on this).
    /// </summary>
    public bool NoStretch { get; set; }
    /// <summary>M5F39. Not a "No" - the only box on the form that turns something on.</summary>
    public bool SoundCanvas { get; set; }
    /// <summary>M5F11. The sound the Messages window makes for each line it is given.</summary>
    public bool NoDing { get; set; }
    /// <summary>M5F1B. Stops the Automap opening itself (MODULE6.BAS:3159).</summary>
    public bool NoLoadAutomap { get; set; }
    /// <summary>M5F2C.</summary>
    public bool NoToolbar { get; set; }
    /// <summary>
    /// M5F48. "Selecting this will disable all background combat sounds (from other characters and
    /// companions in the party)" - the help's Control Panel page lists it among the Character
    /// Options, but the control is on this window.
    /// </summary>
    public bool NoPartyFX { get; set; }
    /// <summary>M5F71.</summary>
    public bool NoHelpSound { get; set; }
    /// <summary>M5F80.</summary>
    public bool NoAutoSave { get; set; }
    /// <summary>M5F8E (FRMAUTOM.BAS:317).</summary>
    public bool NoBlinkCursor { get; set; }

    /// <summary>
    /// <c>[Loading Information] LastBackup</c>, the only thing in that section that is not a flag
    /// or a number. <c>sub2C76</c> (FRMOPTIO.BAS:416-420) reads it and writes it under the two
    /// backup buttons:
    ///
    /// <code>
    /// m0072 = fn03F1(fn0360(gv19E4) + "MORDOR.INI", "Loading Information", "LastBackup")
    /// If m0072 = "" Then m0072 = "(none)"
    /// lblBackup = "Last Backup was on " + m0072
    /// </code>
    ///
    /// fn0662 writes the day's <c>Date</c> there after a backup or a restore (MAINMOD.BAS:1032).
    /// The port keeps it in the saves directory's <see cref="PortSettings"/>, not in MORDOR.INI,
    /// because the backup it dates is of that directory.
    /// </summary>
    public string LastBackup { get; set; } = "";

    /// <summary>The label under the Backup and Restore buttons, fallback and all.</summary>
    public string LastBackupLine => "Last Backup was on " + (LastBackup == "" ? "(none)" : LastBackup);

    /// <summary>gv2196 and gv219A, written as plain numbers.</summary>
    public int MusicVolume { get; set; }
    /// <summary>
    /// gv219A in the original's own form, <c>waveOutSetVolume</c>'s Long with both words the same -
    /// see <see cref="WaveVolume"/>. 0 leaves the wave volume alone. Before 2026-10-21 the port kept
    /// a 0 to 100 slider position here that nothing read.
    /// </summary>
    public int FXVolume { get; set; } = WaveVolume.Default;
    /// <summary>gv2A22.</summary>
    public int Brightness { get; set; }

    /// <summary>
    /// [Fonts] Pro / ProSize (<c>gv1ADC.M41D4</c> / <c>M41E5</c>) and NonPro / NonProSize
    /// (<c>M41DB</c> / <c>M41F0</c>).
    ///
    /// The names are the original's and they are the wrong way round, which is kept: "Pro" holds
    /// Courier New 9 and "NonPro" MS Sans Serif 8 - the defaults written when the key is missing
    /// (MAINMOD.BAS:1256-1265) and what the Default button puts back (FRMOPTIO.BAS:253-257), with
    /// gc0562 = "Courier New" and gc0566 = "MS Sans Serif" (MAIN.TXT:124-125). sub0784 gives every
    /// Courier New control the "Pro" font (MAINMOD.BAS:1866-1880). bm3.png shows "Courier New (9)"
    /// under the "Non-Proportional Font" caption, so the label under that caption is M41D4's.
    /// </summary>
    public string ProportionalFont { get; set; } = DefaultProportionalFont;
    public int ProportionalSize { get; set; } = DefaultProportionalSize;
    public string NonProportionalFont { get; set; } = DefaultNonProportionalFont;
    public int NonProportionalSize { get; set; } = DefaultNonProportionalSize;

    /// <summary>What the Default button puts back (FRMOPTIO.BAS:253-257).</summary>
    public const string DefaultProportionalFont = "Courier New";
    public const int DefaultProportionalSize = 9;
    public const string DefaultNonProportionalFont = "MS Sans Serif";
    public const int DefaultNonProportionalSize = 8;

    private const string Section = "Loading Information";
    private const string Fonts = "Fonts";

    /// <summary>
    /// The MORDOR.INI in the game directory. The original never used that one: gv19E4 is filled by
    /// GetWindowsDirectory (sub03E3, MODULE1.BAS:920-926), so its MORDOR.INI is in the WINDOWS
    /// directory. The port only reads this file, as a fallback for values it holds; it never writes it.
    /// </summary>
    public static string PathFor(string gameDir) => Path.Combine(gameDir, "MORDOR.INI");

    /// <summary>
    /// Each key from the port's own settings file (<see cref="PortSettings"/>, in the saves
    /// directory) when it is there, otherwise from the game directory's MORDOR.INI.
    /// </summary>
    public static GameOptions Load(string gameDir)
    {
        var ini = new IniLayers(Ini.Read(PortSettings.PathFor(Saves.DirFor(gameDir))), Ini.Read(PathFor(gameDir)));
        var o = new GameOptions
        {
            No3d = ini.Flag(Section, "No3d"),
            NoMusic = ini.Flag(Section, "NoMusic"),
            NoFX = ini.Flag(Section, "NoFX"),
            NoStretch = ini.Flag(Section, "NoStretch"),
            SoundCanvas = ini.Flag(Section, "SoundCanvas"),
            NoDing = ini.Flag(Section, "NoDing"),
            NoLoadAutomap = ini.Flag(Section, "NoLoadAutomap"),
            LastBackup = PortSettings.Load(Saves.DirFor(gameDir)).LastBackup,
            NoToolbar = ini.Flag(Section, "NoToolbar"),
            NoPartyFX = ini.Flag(Section, "NoPartyFX"),
            NoHelpSound = ini.Flag(Section, "NoHelpSound"),
            NoAutoSave = ini.Flag(Section, "NoAutoSave"),
            NoBlinkCursor = ini.Flag(Section, "NoBlinkCursor"),
            MusicVolume = ini.Number(Section, "MusicVolume", 0),
            // no key, "-65538" (MODULE1.BAS:983-987)
            FXVolume = ini.Number(Section, "FXVolume", WaveVolume.Default),
            Brightness = ini.Number(Section, "Brightness", 0),
        };
        o.ReadFonts(ini);
        return o;
    }

    /// <summary>
    /// [Fonts], as MAINMOD.BAS:1252-1265 reads it: the four are read, and a face that comes back
    /// empty - the key missing, or there with nothing after the "=" - is written back as Courier
    /// New / MS Sans Serif with its size as 9 / 8, and the face read again. The size is not read
    /// again, so for that run it stays what was read (the key's value, or the default when it is
    /// missing). The port does not write the keys back: it never writes a MORDOR.INI, and a Load
    /// writes nothing.
    /// </summary>
    public void ReadFonts(IniLayers ini)
    {
        ProportionalFont = ini.Text(Fonts, "Pro");
        ProportionalSize = ini.Number(Fonts, "ProSize", DefaultProportionalSize);
        NonProportionalFont = ini.Text(Fonts, "NonPro");
        NonProportionalSize = ini.Number(Fonts, "NonProSize", DefaultNonProportionalSize);
        if (ProportionalFont == "") ProportionalFont = DefaultProportionalFont;
        if (NonProportionalFont == "") NonProportionalFont = DefaultNonProportionalFont;
    }

    /// <summary>
    /// The OK button's writes (FRMOPTIO.BAS:152-247), into the port's own settings file in the saves
    /// directory, leaving every other key there alone. Never into a MORDOR.INI: the original wrote
    /// its own in the WINDOWS directory, not the game's.
    /// </summary>
    public void Save(string gameDir)
    {
        string saves = Saves.DirFor(gameDir);
        try { Directory.CreateDirectory(saves); } catch { }
        var ini = Ini.Read(PortSettings.PathFor(saves));
        void Flag(string key, bool on) => ini.Set(Section, key, on ? "True" : "False");
        Flag("No3d", No3d);
        Flag("NoMusic", NoMusic);
        Flag("NoFX", NoFX);
        Flag("NoStretch", NoStretch);
        Flag("SoundCanvas", SoundCanvas);
        Flag("NoDing", NoDing);
        Flag("NoLoadAutomap", NoLoadAutomap);
        Flag("NoToolbar", NoToolbar);
        Flag("NoPartyFX", NoPartyFX);
        Flag("NoHelpSound", NoHelpSound);
        Flag("NoAutoSave", NoAutoSave);
        Flag("NoBlinkCursor", NoBlinkCursor);
        ini.Set(Section, "MusicVolume", MusicVolume.ToString());
        ini.Set(Section, "FXVolume", FXVolume.ToString());
        ini.Set(Section, "Brightness", Brightness.ToString());
        if (ProportionalFont != "")
        {
            ini.Set(Fonts, "Pro", ProportionalFont);
            ini.Set(Fonts, "ProSize", ProportionalSize.ToString());
        }
        if (NonProportionalFont != "")
        {
            ini.Set(Fonts, "NonPro", NonProportionalFont);
            ini.Set(Fonts, "NonProSize", NonProportionalSize.ToString());
        }
        ini.Write(PortSettings.PathFor(saves));
    }

    /// <summary>The Default button: "Pro" back to Courier New 9 and "NonPro" to MS Sans Serif 8.</summary>
    public void DefaultFonts()
    {
        ProportionalFont = DefaultProportionalFont;
        ProportionalSize = DefaultProportionalSize;
        NonProportionalFont = DefaultNonProportionalFont;
        NonProportionalSize = DefaultNonProportionalSize;
    }
}

/// <summary>
/// The port's stand-in for MORDOR.INI, <c>settings.ini</c> in <see cref="Saves.DirFor"/>. Every
/// MORDOR.INI write of the original lands here - the Options window's keys
/// (<see cref="GameOptions.Save"/>) and these two:
///
/// <code>
/// [Loading Information]
/// NotFirstTime   frmMenu's Form_Load shows "Welcome to Mordor 1.1!" until it is set (FRMMENU.BAS:146-160)
/// LastBackup     fn0662's Date after a backup or a restore (MAINMOD.BAS:1032)
/// Character      the leader to preload, written by sub16CC (MAINMOD2.BAS:5948-5953), read at start (MAINMOD.BAS:1266)
/// Frame          the port's own: where the frame window was left (see <see cref="FramePlacement"/>)
/// </code>
///
/// Same section and key names as the original, read the same way: the welcome test is
/// <c>InStr(..., "T") = False</c>, so any value with a capital T in it counts as set.
/// </summary>
public sealed class PortSettings
{
    public const string FileName = "settings.ini";
    private const string Section = "Loading Information";

    public static string PathFor(string savesDir) => Path.Combine(savesDir, FileName);

    public string NotFirstTime { get; set; } = "";
    public string LastBackup { get; set; } = "";
    public string Character { get; set; } = "";

    /// <summary>Whether the file has a Character entry at all, empty or not.</summary>
    public bool HasCharacter { get; set; }

    /// <summary><c>InStr(value, "T")</c> - case-sensitive, as VB's default text compare is binary.</summary>
    public bool Welcomed => NotFirstTime.Contains('T', StringComparison.Ordinal);

    public static PortSettings Load(string savesDir)
    {
        var ini = Ini.Read(PathFor(savesDir));
        return new PortSettings
        {
            NotFirstTime = ini.Text(Section, "NotFirstTime"),
            LastBackup = ini.Text(Section, "LastBackup"),
            Character = ini.Text(Section, "Character"),
            HasCharacter = ini.Has(Section, "Character"),
            Frame = FramePlacement.Parse(ini.Text(Section, "Frame")),
        };
    }

    /// <summary>The frame window's last place, or null when none was saved or it does not read.</summary>
    public FramePlacement? Frame { get; set; }

    /// <summary>
    /// One key, leaving the rest of the file alone, as WritePrivateProfileString does. False when it
    /// could not be written; it never throws.
    /// </summary>
    public static bool Write(string savesDir, string key, string value)
    {
        try { Directory.CreateDirectory(savesDir); } catch { }
        var ini = Ini.Read(PathFor(savesDir));
        ini.Set(Section, key, value);
        return ini.Write(PathFor(savesDir));
    }
}

/// <summary>
/// Where the frame window - the port's frmMain - was left: its outer top-left in screen pixels,
/// its client size, and whether it was maximised. The size and place are the ones it had while
/// normal, so a maximised frame comes back to them when it is restored.
///
/// DEVIATION, a port necessity: FRMMAIN.FRM is not among the decompiled files, so how frmMain
/// itself was sized and placed is not known - MDIForm_Load (FRMMAIN.BAS:130) does not do it in
/// code. The port's frame is an ordinary window opening at 1180x760, and the children's saved
/// places (Character.Windows) are only right inside a frame of the size they were saved in; so
/// the port keeps the frame's own placement in settings.ini, as Windows keeps any window's.
/// Written as "left,top,width,height,maximised".
/// </summary>
public sealed record FramePlacement(int Left, int Top, double Width, double Height, bool Maximised)
{
    public override string ToString() =>
        string.Join(",", Left, Top,
            Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Maximised ? "1" : "0");

    /// <summary>The strip along the frame's top a player takes hold of to move it, in DIPs.</summary>
    public const double TitleStrip = 30;

    /// <summary>
    /// Whether a display's working area (in physical pixels, at <paramref name="scale"/>) shows
    /// part of the frame's title strip - the part a player can take hold of to drag it back. Any
    /// overlap at all is not enough: a sliver of the frame's bottom edge peeping onto a display
    /// could never be moved. A port necessity, as the placement itself is.
    /// </summary>
    public bool TitleShownOn(int areaX, int areaY, int areaWidth, int areaHeight, double scale)
    {
        if (scale <= 0) scale = 1;
        long w = (long)Math.Round(Width * scale), h = (long)Math.Round(TitleStrip * scale);
        return Left < (long)areaX + areaWidth && (long)Left + w > areaX
            && Top < (long)areaY + areaHeight && (long)Top + h > areaY;
    }

    public static FramePlacement? Parse(string text)
    {
        var p = (text ?? "").Split(',');
        if (p.Length != 5) return null;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (!int.TryParse(p[0].Trim(), System.Globalization.NumberStyles.Integer, inv, out int l)) return null;
        if (!int.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Integer, inv, out int t)) return null;
        if (!double.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float, inv, out double w) || w < 100) return null;
        if (!double.TryParse(p[3].Trim(), System.Globalization.NumberStyles.Float, inv, out double h) || h < 100) return null;
        return new FramePlacement(l, t, w, h, p[4].Trim() == "1");
    }
}

/// <summary>Two INI files read as one: a key in the first wins, a key only in the second falls through.</summary>
public sealed class IniLayers
{
    private readonly Ini _first, _second;
    public IniLayers(Ini first, Ini second) { _first = first; _second = second; }
    private Ini For(string section, string key) => _first.Has(section, key) ? _first : _second;
    public string Text(string section, string key, string fallback = "") => For(section, key).Text(section, key, fallback);
    public bool Flag(string section, string key) => For(section, key).Flag(section, key);
    public int Number(string section, string key, int fallback) => For(section, key).Number(section, key, fallback);
}

/// <summary>
/// Just enough of a Windows 3.1 INI to read and write MORDOR.INI without losing anything that was
/// already in it. Sections and keys are matched without regard to case, as the API they were
/// written against does.
/// </summary>
public sealed class Ini
{
    private readonly List<string> _lines = new();

    /// <summary>An INI of these lines, as if read from a file.</summary>
    public static Ini Parse(IEnumerable<string> lines)
    {
        var ini = new Ini();
        ini._lines.AddRange(lines);
        return ini;
    }

    public static Ini Read(string path)
    {
        var ini = new Ini();
        try { if (File.Exists(path)) ini._lines.AddRange(File.ReadAllLines(path)); }
        catch { }
        return ini;
    }

    /// <summary>False when the file could not be written (a read-only folder, say); never throws.</summary>
    public bool Write(string path)
    {
        try { File.WriteAllLines(path, _lines); return true; }
        catch { return false; }
    }

    public string Text(string section, string key, string fallback = "")
    {
        int i = Find(section, key);
        if (i < 0) return fallback;
        int eq = _lines[i].IndexOf('=');
        return eq < 0 ? fallback : _lines[i][(eq + 1)..].Trim();
    }

    public bool Flag(string section, string key) =>
        string.Equals(Text(section, key, "False"), "True", StringComparison.OrdinalIgnoreCase);

    public bool Has(string section, string key) => Find(section, key) >= 0;

    /// <summary>Take a key out of its section, if it is there (the section itself stays).</summary>
    public void Remove(string section, string key)
    {
        int i = Find(section, key);
        if (i >= 0) _lines.RemoveAt(i);
    }

    public int Number(string section, string key, int fallback) =>
        int.TryParse(Text(section, key, "").Trim(), out int v) ? v : fallback;

    public void Set(string section, string key, string value)
    {
        int i = Find(section, key);
        if (i >= 0) { _lines[i] = $"{key}={value}"; return; }

        int s = FindSection(section);
        if (s < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Trim() != "") _lines.Add("");
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }
        // after the last line of that section
        int at = s + 1;
        while (at < _lines.Count && !_lines[at].TrimStart().StartsWith('[')) at++;
        while (at - 1 > s && _lines[at - 1].Trim() == "") at--;
        _lines.Insert(at, $"{key}={value}");
    }

    private int FindSection(string section)
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            string t = _lines[i].Trim();
            if (t.StartsWith('[') && t.EndsWith(']') &&
                string.Equals(t[1..^1].Trim(), section, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private int Find(string section, string key)
    {
        int s = FindSection(section);
        if (s < 0) return -1;
        for (int i = s + 1; i < _lines.Count; i++)
        {
            string t = _lines[i].Trim();
            if (t.StartsWith('[')) return -1;
            int eq = t.IndexOf('=');
            if (eq > 0 && string.Equals(t[..eq].Trim(), key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
