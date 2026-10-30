using System;
using System.IO;
using System.Runtime.InteropServices;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// Plays the original WAV effects and MIDI music through the system's own audio API, so no
/// third-party audio library is needed: the Windows multimedia API (winmm.dll) on Windows, and
/// AudioToolbox on macOS (<see cref="AudioQueueStream"/>, <see cref="MacMidi"/>). On other platforms
/// every call is a silent no-op. Toggle with <see cref="Enabled"/>.
///
/// The effects go through the port's WaveMix (<see cref="WaveMixSession"/> over
/// <see cref="WaveMixChannels"/>, out through <see cref="WaveOutStream"/> or
/// <see cref="AudioQueueStream"/>): eight channels mixed
/// together, as the original's WAVMIX16 mixes them, where winmm's PlaySound had one sound at a
/// time and each new one cut off the last.
/// </summary>
public static class Audio
{
    public static bool Enabled { get; set; } = true;

    /// <summary>Where there is a system audio API the port speaks: Windows and macOS.</summary>
    private static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
    public static bool MusicEnabled { get; set; } = true;
    private static string _dir = "";
    private static string _currentMusic = "";
    private static bool _muted;

    /// <summary>
    /// The track to start again when the game comes back from minimised, or "" - see
    /// <see cref="Pause"/>.
    /// </summary>
    private static string _resumeMusic = "";

    /// <summary>
    /// The game was minimised (FRMMAIN.BAS:206-213): <c>mciMusic.Command = "Stop"</c>, then
    /// <c>"Close"</c>, and with Sound FX on <c>WaveMixActivate(gv20C8, False)</c>
    /// (MODULE13.BAS:8), which gives up the wave device, so no effect sounds until it is given back.
    /// </summary>
    public static void Pause()
    {
        if (_currentMusic != "") _resumeMusic = _currentMusic;
        StopMusic();
        Deactivate();
    }

    /// <summary>
    /// The game came back (FRMMAIN.BAS:214-226): <c>sub1A50</c> starts the music again and the
    /// wave device is taken back. sub1A50 (MUSIC.BAS:79-97) chooses a track afresh with Rnd - a
    /// quarter of the time <c>Val(mciMusic.Tag)</c>, which the minimise has just cleared, else the
    /// town's tune or a random dungeon one. The port plays one tune per place and has no such
    /// choice (a difference of its own, not of this pause), so the tune of the place you are in
    /// starts again from its beginning.
    /// </summary>
    public static void Resume()
    {
        string track = _resumeMusic;
        _resumeMusic = "";
        if (track != "") Music(Path.GetFileNameWithoutExtension(track));
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
    private static IDisposable? _out;
    /// <summary>The system refused the device: not asked again until the game comes back from minimised.</summary>
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
            _out = OpenOutput(_channels);
            _outFailed = _out == null;
        }
        return _session;
    }

    /// <summary>The wave device: winmm on Windows, an AudioQueue on macOS; null where there is none.</summary>
    private static IDisposable? OpenOutput(WaveMixChannels channels) =>
        OperatingSystem.IsMacOS() ? AudioQueueStream.Open(channels) : WaveOutStream.Open(channels);

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
        if (!Supported) return;
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
        if (!Supported) return true;
        EnsureSession();
        _channels!.OpenSession();
        _outFailed = false;
        _out ??= OpenOutput(_channels);
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
        if (!Enabled || !Supported) return;
        if (FramePause.Paused) return;   // WaveMixActivate(..., False) while minimised (FRMMAIN.BAS:210-212)
        if (_options.NoDing && string.Equals(name, "MESSAGE", StringComparison.OrdinalIgnoreCase)) return;
        if (_options.NoHelpSound && string.Equals(name, "HELP", StringComparison.OrdinalIgnoreCase)) return;
        int n = Array.FindIndex(SoundFiles.Names, s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
        if (n < 0) return;
        try { Session()?.Play(n, routine); } catch { }
    }

    // ---- MIDI music via MCI (Windows) or AudioToolbox's MusicPlayer (macOS, MacMidi) ----
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, System.Text.StringBuilder? ret, int retLen, IntPtr callback);

    private static void Mci(string cmd)
    {
        try { mciSendString(cmd, null, 0, IntPtr.Zero); } catch { }
    }

    /// <summary>Loop a MIDI by base name ("CITY", "DUNGEON", "THEME"...). Re-requesting the current track is a no-op.</summary>
    public static void Music(string name)
    {
        if (!Supported) return;
        if (!MusicEnabled) { StopMusic(); return; }
        string path = Path.Combine(_dir, name.ToUpperInvariant() + ".MID");
        if (!File.Exists(path)) return;
        // minimised, the player is closed (FRMMAIN.BAS:207-208): the tune asked for waits for Resume
        if (FramePause.Paused) { _resumeMusic = path; return; }
        if (_currentMusic == path) return;
        StopMusic();
        if (OperatingSystem.IsMacOS()) { if (!MacMidi.Play(path)) return; }
        else
        {
            Mci($"open \"{path}\" type sequencer alias bgm");
            Mci("play bgm");
        }
        _currentMusic = path;
    }

    public static void StopMusic()
    {
        if (_currentMusic == "") return;
        if (OperatingSystem.IsMacOS()) MacMidi.Stop();
        else { Mci("stop bgm"); Mci("close bgm"); }
        _currentMusic = "";
    }

    /// <summary>Music tracks loop: call from a timer to restart a finished track.</summary>
    public static void TickMusic()
    {
        if (_currentMusic == "" || !Supported) return;
        if (FramePause.Paused) return;   // timMusic_Timer: "If Settings.Paused = False Then" (FRMMAIN.BAS:296)
        if (OperatingSystem.IsMacOS()) { if (MacMidi.Finished) MacMidi.Restart(); return; }
        var sb = new System.Text.StringBuilder(64);
        try { mciSendString("status bgm mode", sb, sb.Capacity, IntPtr.Zero); } catch { return; }
        if (sb.ToString().Trim() == "stopped") { Mci("seek bgm to start"); Mci("play bgm"); }
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
        if (OperatingSystem.IsMacOS()) MacMidi.Beep();
        else try { MessageBeep(0); } catch { }   // MB_OK: the system default sound
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
