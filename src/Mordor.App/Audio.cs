using System;
using System.IO;
using System.Runtime.InteropServices;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// Plays the original WAV effects and MIDI music through the Windows multimedia API
/// (winmm.dll), so no third-party audio library is needed. On other platforms every
/// call is a silent no-op. Toggle with <see cref="Enabled"/>.
///
/// The effects go through the port's WaveMix (<see cref="WaveMixSession"/> over
/// <see cref="WaveMixChannels"/>, out through <see cref="WaveOutStream"/>): eight channels mixed
/// together, as the original's WAVMIX16 mixes them, where winmm's PlaySound had one sound at a
/// time and each new one cut off the last.
/// </summary>
public static class Audio
{
    public static bool Enabled { get; set; } = true;
    public static bool MusicEnabled { get; set; } = true;
    private static string _dir = "";
    private static bool _muted;

    /// <summary>
    /// <c>mciMusic.Tag</c>: the <see cref="MusicTrack"/> last opened, which <see cref="Music"/> will
    /// not play again; null once the player is closed. See <see cref="MusicTrack"/>.
    /// </summary>
    private static int? _tag;

    /// <summary>A tune asked for while minimised, started when the game comes back - see <see cref="Pause"/>.</summary>
    private static int? _resumeMusic;

    /// <summary>M5F39, Use Sound Canvas MIDI: which of <c>sub10AB</c>'s two tables names the files.</summary>
    private static bool _soundCanvas;

    /// <summary>
    /// <c>gv1B52</c> and <c>Chars(0).DungeonLevel</c> for <see cref="MusicTrack.Next"/>: the town sets
    /// it while a party is in play, and null means none is.
    /// </summary>
    public static Func<int>? LeaderLevel { get; set; }

    private static readonly Random _rng = new();

    /// <summary>
    /// The game was minimised (FRMMAIN.BAS:206-213): <c>mciMusic.Command = "Stop"</c>, then
    /// <c>"Close"</c> and <c>mciMusic.Tag = ""</c>, and with Sound FX on
    /// <c>WaveMixActivate(gv20C8, False)</c> (MODULE13.BAS:8), which gives up the wave device, so
    /// no effect sounds until it is given back.
    /// </summary>
    public static void Pause()
    {
        StopMusic();
        Deactivate();
    }

    /// <summary>
    /// The game came back (FRMMAIN.BAS:214-226): <c>sub1A50</c> starts the music again and the
    /// wave device is taken back. sub1A50 chooses afresh (<see cref="MusicTrack.Next"/>): with the
    /// tag emptied by the minimise, the menu's THEME, or in play CITY in town and otherwise most
    /// often a random tune. A tune asked for while minimised starts first, as the original would
    /// already be playing it, and sub1A50 may then change it.
    /// </summary>
    public static void Resume()
    {
        int? track = _resumeMusic;
        _resumeMusic = null;
        if (track is int t) Music(t);
        if (MusicEnabled) Music(MusicTrack.Next(_tag, LeaderLevel?.Invoke(), _rng));
        // WaveMixActivate(gv20C8, True) (FRMMAIN.BAS:223): the device is opened again by the next effect
        if (_channels != null) _channels.Active = true;
        _outFailed = false;
    }

    /// <summary>
    /// The Options window's flags, so the two sounds the original guards at their call site can be
    /// guarded here instead. See <see cref="Play"/> for why that is the same thing.
    /// </summary>
    private static GameOptions _options = new();

    /// <summary>
    /// MORDOR_MUTE=1 from the first touch, not only from <see cref="Init"/>: the title screen's
    /// Options window can play an effect (its FX slider, its No Sound FX box) before any Init.
    /// </summary>
    static Audio()
    {
        if (Environment.GetEnvironmentVariable("MORDOR_MUTE") == "1")
        {
            _muted = true;
            Enabled = false; MusicEnabled = false;
        }
    }

    public static void Init(string gameDir)
    {
        _dir = gameDir;
        // MORDOR_MUTE=1 silences everything (used by the automated smoke tests)
        if (Environment.GetEnvironmentVariable("MORDOR_MUTE") == "1")
        {
            _muted = true;
            Enabled = false; MusicEnabled = false;
        }
    }

    /// <summary>
    /// What the Options window's four sound boxes mean. <c>sub09DE</c> - the routine every effect
    /// in the game goes through - begins "If gv1A66.M5EFC &lt;&gt; False Then Exit Sub"
    /// (MUSIC.BAS:143), so No Sound FX silences every effect there is, and No Music does the same
    /// for the MIDI.
    ///
    /// This used to be read once, in the dungeon, and only in the direction that switches sound
    /// off: clearing a box did not bring it back, and the city never read it at all.
    /// </summary>
    public static void Apply(GameOptions o)
    {
        _options = o;
        _soundCanvas = o.SoundCanvas;
        SetFxVolume(o.FXVolume);
        if (_muted) return;
        Enabled = !o.NoFX;
        MusicEnabled = !o.NoMusic;
        if (!MusicEnabled) StopMusic();
    }

    // ---- WAV effects, through the port's WaveMix ----

    /// <summary>gv219A, the FX Vol. setting (<see cref="WaveVolume"/>).</summary>
    private static int _fxVolume = WaveVolume.Default;

    /// <summary>
    /// gv219A changed - by the options, or live by the Options window's slider. It reaches the
    /// sound at the next play, as the original's does: sub09DE and sub086D set the wave device's
    /// volume to it before each sound (MUSIC.BAS:157-164, 218-225). The device here is the port's
    /// own output, <see cref="WaveMixChannels.DeviceVolume"/>, not Windows' volume, and it is the
    /// effects' alone: the music is MIDI, on another device, as the original's was.
    /// </summary>
    public static void SetFxVolume(int raw)
    {
        _fxVolume = raw;
        if (_session != null) _session.FxVolume = raw;
    }

    /// <summary>WaveMix's channels; made on the first effect, so a muted run never has them.</summary>
    private static WaveMixChannels? _channels;
    /// <summary>MUSIC.BAS's bookkeeping over them.</summary>
    private static WaveMixSession? _session;
    /// <summary>The device, open from the first effect until a minimise or the end.</summary>
    private static WaveOutStream? _out;
    /// <summary>Windows refused the device: not asked again until the game comes back from minimised.</summary>
    private static bool _outFailed;

    /// <summary>
    /// <c>fn1A08</c> (MUSIC.BAS:376-399), done when the first effect is asked for: the session, its
    /// eight channels, activated. The files are read once and kept, which changes nothing anyone
    /// hears - which waves count as open is still <see cref="WaveMixSession"/>'s business.
    /// </summary>
    private static WaveMixSession? Session()
    {
        EnsureSession();
        if (_out == null && !_outFailed && _channels != null && !_channels.Closed)
        {
            _out = WaveOutStream.Open(_channels);
            _outFailed = _out == null;
        }
        return _session;
    }

    /// <summary>The session and its channels, made once; no device is opened here.</summary>
    private static void EnsureSession()
    {
        if (_session == null)
        {
            var decoded = new System.Collections.Generic.Dictionary<string, short[]?>(StringComparer.OrdinalIgnoreCase);
            _channels = new WaveMixChannels(path =>
            {
                if (!decoded.TryGetValue(path, out var pcm))
                {
                    try { pcm = WavePcm.Decode(File.ReadAllBytes(path)); } catch { pcm = null; }
                    decoded[path] = pcm;
                }
                return pcm;
            });
            _session = new WaveMixSession(_channels, WaveMixSession.VbTimer, n =>
            {
                string path = Path.Combine(_dir, SoundFiles.Names[n] + ".WAV");
                return File.Exists(path) ? path : null;   // fn034F (MUSIC.BAS:34)
            }, SoundFiles.Names.Length);
            var channels = _channels;
            _session.FxVolume = _fxVolume;
            _session.WaveOutSetVolume = v => channels.DeviceVolume = v;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        }
    }

    /// <summary>
    /// No Sound FX ticked in the Options window: chkCheck_Click, Case 2 (FRMOPTIO.BAS:88-92) sets
    /// gv1A66.M5EFC at once and calls <c>mdrDeInitializeFX</c> (MUSIC.BAS:268-289) - the waves
    /// freed, the session closed and the wave device given back. The original's session was opened
    /// at start-up; the port's is made here if no effect has made it yet, so closing it means the same.
    /// </summary>
    public static void NoFxTicked()
    {
        if (_muted) return;
        Enabled = false;
        if (!OperatingSystem.IsWindows()) return;
        EnsureSession();
        _session!.DeInitialize();
        _channels!.CloseSession();
        _out?.Dispose();
        _out = null;
    }

    /// <summary>
    /// No Sound FX cleared (FRMOPTIO.BAS:93-98): M5EFC False at once, then <c>fn1A08</c> opens a
    /// session again (MUSIC.BAS:376-399) and <c>sub09DE 11</c> plays RAISE. False when the device
    /// would not open - fn1A08's False, for which the form puts up its box.
    /// </summary>
    public static bool NoFxCleared()
    {
        if (_muted) return true;
        Enabled = true;
        if (!OperatingSystem.IsWindows()) return true;
        EnsureSession();
        _channels!.OpenSession();
        _outFailed = false;
        _out ??= WaveOutStream.Open(_channels);
        if (_out == null) { _outFailed = true; return false; }
        Play("RAISE");
        return true;
    }

    /// <summary>
    /// Cancel: "gv1A66.M5EFC = m0056" (FRMOPTIO.BAS:140), the flag as the form found it - and
    /// nothing more. The session is not opened or closed again, so a tick then Cancel leaves the
    /// effects on with their session closed, and silent until No Sound FX is cleared again.
    /// </summary>
    public static void RestoreNoFx(bool noFx)
    {
        if (_muted) return;
        Enabled = !noFx;
    }

    /// <summary>
    /// No Music ticked or cleared in the Options window, at once (chkCheck_Click, Case 1,
    /// FRMOPTIO.BAS:76-87): ticked, <c>fn19DC</c> stops and closes the player and empties the tag;
    /// cleared, <c>sub063F</c> plays THEME.
    /// </summary>
    public static void NoMusicChanged(bool noMusic)
    {
        if (_muted) return;
        MusicEnabled = !noMusic;
        if (noMusic) StopMusic();
        else Music(MusicTrack.Theme);
    }

    /// <summary>
    /// Use Sound Canvas MIDI ticked or cleared, at once (Case 4, FRMOPTIO.BAS:110-114): the player
    /// is stopped, <c>sub10AB</c> fills the table again, and <c>sub063F</c> asks for THEME - which
    /// does nothing if THEME was the tune, so the menu's music then stays stopped.
    /// </summary>
    public static void SoundCanvasChanged(bool soundCanvas)
    {
        _soundCanvas = soundCanvas;
        Mci("stop bgm");
        Music(MusicTrack.Theme);
    }

    /// <summary>
    /// The Options window closing. Cancel first puts both flags back as the form found them, stops
    /// the player and fills the table again (FRMOPTIO.BAS:138-154). Then, either way,
    /// <c>Form_Unload</c> (FRMOPTIO.BAS:376-387): with No Music, fn19DC; otherwise THEME, unless a
    /// tune is still playing.
    /// </summary>
    public static void OptionsClosed(bool cancelled, bool noMusic, bool soundCanvas)
    {
        if (cancelled)
        {
            if (!_muted) MusicEnabled = !noMusic;
            _soundCanvas = soundCanvas;
            Mci("stop bgm");
        }
        if (!MusicEnabled) StopMusic();
        else if (!MusicPlaying()) Music(MusicTrack.Theme);
    }

    /// <summary>The minimise: WaveMixActivate(gv20C8, False) (FRMMAIN.BAS:211).</summary>
    private static void Deactivate()
    {
        if (_channels != null) _channels.Active = false;
        _out?.Dispose();
        _out = null;
    }

    /// <summary><c>mdrDeInitializeFX</c> (MUSIC.BAS:268-289), as the process ends.</summary>
    public static void Shutdown()
    {
        Deactivate();
    }

    /// <summary>
    /// <c>sub09DE</c> (MUSIC.BAS:136-169), the routine nearly every effect goes through: play a WAV
    /// by base name (e.g. "SWING") on the channel that sound holds, cutting off its own last play
    /// there and mixing with every other sound.
    ///
    /// Two of them answer to a box of their own. The original guards those at the call site -
    /// "If gv1A66.M5F11 = False Then sub09DE 66" (MAINMOD2.BAS:3206) and "If Not gv1A66.M5F71 Then
    /// sub09DE 56" (FRMDUNM.BAS:599, MONSMOD.BAS:3406) - and sound 66 is MESSAGE.WAV and sound 56
    /// is HELP.WAV (MODULE1.BAS:305-380). Those are the only two places either sound is asked for
    /// in the whole game, so guarding them by name here is the same rule in one place instead of
    /// three.
    /// </summary>
    public static void Play(string name) => Effect(name, WaveMixRoutine.Sub09DE);

    /// <summary>
    /// <c>sub086D</c> (MUSIC.BAS:200-229): the sound takes the next free channel each time it is
    /// played and is queued there without a flush, so a second play mixes with the first.
    /// </summary>
    public static void PlayOnNextChannel(string name) => Effect(name, WaveMixRoutine.Sub086D);

    /// <summary>
    /// <c>sub08D1</c> (MUSIC.BAS:231-248): queued on the channel handed out last, after what is
    /// playing there - the HIT after <c>sub086D</c>'s FALL (MAZEMOD.BAS:1762, 1868).
    /// </summary>
    public static void PlayAfterLast(string name) => Effect(name, WaveMixRoutine.Sub08D1);

    private static void Effect(string name, WaveMixRoutine routine)
    {
        if (!Enabled || !OperatingSystem.IsWindows()) return;
        if (FramePause.Paused) return;   // WaveMixActivate(..., False) while minimised (FRMMAIN.BAS:210-212)
        if (_options.NoDing && string.Equals(name, "MESSAGE", StringComparison.OrdinalIgnoreCase)) return;
        if (_options.NoHelpSound && string.Equals(name, "HELP", StringComparison.OrdinalIgnoreCase)) return;
        int n = Array.FindIndex(SoundFiles.Names, s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
        if (n < 0) return;
        try { Session()?.Play(n, routine); } catch { }
    }

    // ---- MIDI music via MCI ----
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, System.Text.StringBuilder? ret, int retLen, IntPtr callback);

    private static void Mci(string cmd)
    {
        try { mciSendString(cmd, null, 0, IntPtr.Zero); } catch { }
    }

    /// <summary>
    /// <c>sub063F</c> (MUSIC.BAS:433-469): play a <see cref="MusicTrack"/> once. Nothing happens if
    /// it is the tag, playing or finished. Otherwise the player is closed, and the file, if there
    /// is one, opened and played and its number made the tag. A tune does not loop: when it ends,
    /// <see cref="TickMusic"/> chooses what comes next.
    /// </summary>
    public static void Music(int track)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!MusicEnabled) { StopMusic(); return; }
        // minimised, the player is closed (FRMMAIN.BAS:207-208): the tune asked for waits for Resume
        if (FramePause.Paused) { _resumeMusic = track; return; }
        if (_tag == track) return;
        Mci("stop bgm"); Mci("close bgm");
        string path = Path.Combine(_dir, MusicTrack.File(track, _soundCanvas) + ".MID");
        if (!File.Exists(path)) return;
        Mci($"open \"{Playable(path)}\" type sequencer alias bgm");
        Mci("play bgm");
        _tag = track;
    }

    /// <summary>
    /// The file to hand the sequencer: the game's own, or for THEME.MID a copy in the temp folder
    /// with its Expression set as the sound cards of the time played it (<see cref="MidiExpression"/>).
    /// The game folder is never written.
    /// </summary>
    private static string Playable(string path)
    {
        try
        {
            byte[]? unmuted = MidiExpression.Unmuted(File.ReadAllBytes(path));
            if (unmuted == null) return path;
            string dir = Path.Combine(Path.GetTempPath(), "OpenDejenol");
            Directory.CreateDirectory(dir);
            string copy = Path.Combine(dir, Path.GetFileName(path));
            File.WriteAllBytes(copy, unmuted);
            return copy;
        }
        catch { return path; }
    }

    /// <summary><c>fn19DC</c> (MUSIC.BAS:291-301): the player stopped and closed, and the tag emptied.</summary>
    public static void StopMusic()
    {
        if (!OperatingSystem.IsWindows()) return;
        Mci("stop bgm"); Mci("close bgm");
        _tag = null;
    }

    /// <summary><c>mciMusic.Mode = 526</c>, MCI_MODE_PLAY.</summary>
    private static bool MusicPlaying()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var sb = new System.Text.StringBuilder(64);
        try { mciSendString("status bgm mode", sb, sb.Capacity, IntPtr.Zero); } catch { return false; }
        return sb.ToString().Trim() == "playing";
    }

    /// <summary>
    /// <c>timMusic_Timer</c> (FRMMAIN.BAS:291-303), every ten seconds: with music on, not paused
    /// and no tune playing, <c>sub1A50</c> (<see cref="MusicTrack.Next"/>).
    /// </summary>
    public static void TickMusic()
    {
        if (!OperatingSystem.IsWindows() || !MusicEnabled) return;
        if (FramePause.Paused) return;   // timMusic_Timer: "If Settings.Paused = False Then" (FRMMAIN.BAS:296)
        if (MusicPlaying()) return;
        Music(MusicTrack.Next(_tag, LeaderLevel?.Invoke(), _rng));
    }

    /// <summary>
    /// The sound the Messages window makes for every line put in it: <c>sub085B</c> adds the line,
    /// trims the list to twenty and then plays sound 66, MESSAGE.WAV (MAINMOD2.BAS:3195-3207).
    /// The port kept the twenty-line limit and dropped the sound.
    /// </summary>
    public static void Ding() => Play("MESSAGE");

    /// <summary>VB's <c>Beep</c>: the system's own sound, not one of the game's WAVs, so NoFX does not stop it.</summary>
    public static void Beep()
    {
        // test hook: MORDOR_TRACE_BEEP names a file each Beep appends a line to, so a muted smoke
        // test can still count them
        if (Environment.GetEnvironmentVariable("MORDOR_TRACE_BEEP") is string trace && trace != "")
            try { System.IO.File.AppendAllText(trace, DateTime.Now.ToString("HH:mm:ss.fff") + " beep\n"); } catch { }
        if (_muted) return;
        try { MessageBeep(0); } catch { }   // MB_OK: the system default sound
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    // ---- game-specific sound choices ----

    /// <summary>The sound a monster kind makes when it dies.</summary>
    public static string DeathSound(MonsterKind k) => k switch
    {
        MonsterKind.Animal => "BIRDDIE",
        MonsterKind.Insect => "BUGDIE",
        MonsterKind.Demon or MonsterKind.Devil => "DI-DEMON",
        MonsterKind.Undead => "DI-UNDEA",
        MonsterKind.Humanoid or MonsterKind.Mage or MonsterKind.Warrior or MonsterKind.Thief => "DI-HUMAN",
        _ => "DI-ANIM",
    };

    /// <summary>
    /// <c>sub1005</c> (MUSIC.BAS:171-196): a spell's sound, or two - see <see cref="SpellSounds"/>.
    /// Both go through sub09DE, so No Sound FX silences them as <see cref="Play"/> does, and the
    /// second is mixed over the first: each has a channel of its own.
    /// </summary>
    public static void PlaySpell(Spell s) => PlayAll(SpellSounds.For(s));

    /// <summary>Several sounds in order, as consecutive sub09DE calls.</summary>
    public static void PlayAll(System.Collections.Generic.IEnumerable<string>? names)
    {
        foreach (string n in names ?? Array.Empty<string>()) Play(n);
    }
}
