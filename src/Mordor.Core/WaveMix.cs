namespace Mordor.Core;

/// <summary>
/// The three routines every sound effect in the original goes through, by the value each hands
/// <c>sub1A31</c> as its second argument (MUSIC.BAS:146, 207, 238).
/// </summary>
public enum WaveMixRoutine
{
    /// <summary>
    /// <c>sub08D1</c> (MUSIC.BAS:231-248) passes <c>m00C4</c>, a module-level Dim nothing assigns,
    /// so 0: the sound goes on <c>gv2162</c>, the channel <c>fn19F4</c> handed out last, queued
    /// behind whatever is on it. Its flags are <c>m0166</c>, a Static never assigned - 0 again,
    /// WMIX_QUEUEWAVE. Only MAZEMOD.BAS's fall uses it: <c>sub086D 6: sub08D1 2</c>, FALL then HIT
    /// (1762, 1868), so the HIT waits for the FALL to end.
    /// </summary>
    Sub08D1 = 0,

    /// <summary>
    /// <c>sub086D</c> (MUSIC.BAS:200-229) passes <c>mc00C6</c> = 1: every call takes the next
    /// channel from <c>fn19F4</c>, does not flush it, and queues the wave there (flags
    /// <c>mc0134 And mc0136</c> = 4 And 1 = 0).
    /// </summary>
    Sub086D = 1,

    /// <summary>
    /// <c>sub09DE</c> (MUSIC.BAS:136-169) passes <c>mc00C8</c> = 2: the sound keeps the channel it was
    /// given for five seconds from the giving, and each play flushes that channel
    /// (<c>WaveMixFlushChannel(gv20C8, Id, 2)</c>) before queueing on it. A sound cuts off its own
    /// last play and nothing else.
    /// </summary>
    Sub09DE = 2,
}

/// <summary>
/// The part of WAVMIX16.DLL the original calls (MODULE13.BAS:7-18): open and free a wave, flush a
/// channel, play a wave on a channel behind whatever is queued there. A handle of 0 is a wave that
/// did not open, as <c>WaveMixOpenWave</c> returns.
/// </summary>
public interface IWaveMixDevice
{
    int OpenWave(string path);
    void FreeWave(int wave);
    void FlushChannel(int channel);
    void Play(int channel, int wave);
}

/// <summary>
/// MUSIC.BAS's bookkeeping over WaveMix, line for line: <c>gv20FE</c> (one entry per sound
/// number - its open wave, its channel, when that channel was handed to it), <c>gv2130</c> (the
/// waves open, in the order they were opened) and <c>gv2162</c> (the channel handed out last).
///
/// <list type="bullet">
/// <item><c>sub1A31</c> (MUSIC.BAS:30-77) opens a sound's wave on first use. The list is
/// <c>ReDim gv2130(0)</c> to begin with (MODULE1.BAS:307) and used from 1, and once its top index
/// is 7 <c>sub1A3F</c> makes room, so at most <see cref="CacheSize"/> = 7 waves are open.</item>
/// <item><c>sub1A3F</c> (MUSIC.BAS:250-261) frees <c>gv2130(1)</c>, the wave opened longest ago -
/// whether or not it was played since - and moves the rest down one.</item>
/// <item><c>fn19F4</c> (MUSIC.BAS:348-374) hands out channels 1 to 7 in turn, passing over one
/// that an open sound was given in the last five seconds by <c>sub09DE</c>'s path (only that
/// path stamps the time, <c>M4EE6</c>), at most eight times.</item>
/// </list>
///
/// Channel 0 is opened (<c>WaveMixOpenChannel(gv20C8, 0, 1)</c>, WMIX_OPENALL, MUSIC.BAS:392)
/// but nothing hands it out; a sound reaches it only as a sound whose file is missing, whose
/// channel is set to 0 (MUSIC.BAS:36).
///
/// <c>Timer</c> is VB's seconds since midnight (a Single), so the five seconds run out at
/// midnight as they do in the original.
/// </summary>
public sealed class WaveMixSession
{
    /// <summary>gv2130(1 To 7).</summary>
    public const int CacheSize = 7;
    /// <summary>fn19F4's channels, 1 to 7.</summary>
    public const int LastChannel = 7;
    /// <summary>How long a channel stays a sub09DE sound's: "M4EE6 + 5 &gt; Timer".</summary>
    public const float HoldSeconds = 5;

    private readonly IWaveMixDevice _device;
    private readonly Func<float> _timer;
    private readonly Func<int, string?> _pathOf;

    /// <summary>gv20FE(n).M7208: the sound's open wave, 0 when none.</summary>
    public readonly int[] Wave;
    /// <summary>gv20FE(n).Id: the sound's channel.</summary>
    public readonly int[] Id;
    /// <summary>gv20FE(n).M4EE6: the Timer when sub09DE's path handed it its channel, 0 when not.</summary>
    public readonly float[] Assigned;

    /// <summary>gv2130, index 0 unused as in the original.</summary>
    private readonly List<int> _open = new() { 0 };

    /// <summary>gv2162: the channel fn19F4 handed out last.</summary>
    public int LastHanded { get; private set; }

    /// <param name="device">WaveMix itself.</param>
    /// <param name="timer">VB's Timer.</param>
    /// <param name="pathOf">The sound's file, or null when <c>fn034F</c> finds no such file.</param>
    public WaveMixSession(IWaveMixDevice device, Func<float> timer, Func<int, string?> pathOf, int sounds = 72)
    {
        _device = device; _timer = timer; _pathOf = pathOf;
        Wave = new int[sounds]; Id = new int[sounds]; Assigned = new float[sounds];
    }

    /// <summary>The sounds whose waves are open, opened longest ago first.</summary>
    public IReadOnlyList<int> OpenSounds => _open.Skip(1).ToList();

    /// <summary>VB's Timer: seconds since midnight.</summary>
    public static float VbTimer() => (float)DateTime.Now.TimeOfDay.TotalSeconds;

    /// <summary>
    /// sub09DE, sub086D or sub08D1 with sound <paramref name="sound"/>, after their "No Sound FX"
    /// test (the caller's).
    /// </summary>
    public void Play(int sound, WaveMixRoutine routine)
    {
        if (sound < 0 || sound >= Wave.Length) return;
        Sub1A31(sound, routine);
        switch (routine)
        {
            case WaveMixRoutine.Sub09DE:                                       // MUSIC.BAS:146-167
                if (Wave[sound] == 0) return;
                SetVolume();
                _device.FlushChannel(Id[sound]);
                _device.Play(Id[sound], Wave[sound]);
                break;
            case WaveMixRoutine.Sub086D:                                       // MUSIC.BAS:207-227
                if (Wave[sound] == 0) return;
                SetVolume();
                _device.Play(Id[sound], Wave[sound]);
                break;
            default:                                                           // MUSIC.BAS:238-246, no test of M7208
                _device.Play(Id[sound], Wave[sound]);
                break;
        }
    }

    /// <summary>
    /// <c>gv219A</c>, the FX Vol. setting, in the form <c>waveOutSetVolume</c> takes - see
    /// <see cref="WaveVolume"/>. 0 leaves the wave device's volume as it is.
    /// </summary>
    public int FxVolume { get; set; }

    /// <summary>
    /// <c>waveOutSetVolume</c> on the wave device, or null for none. The session only calls it; what
    /// it acts on is the output's business.
    /// </summary>
    public Action<int>? WaveOutSetVolume { get; set; }

    /// <summary>
    /// MUSIC.BAS:157-164 (sub09DE) and 218-225 (sub086D), after the wave is found open and before it
    /// is played: "If gv219A &lt;&gt; False Then" the device volume is read and, if it differs, set to
    /// gv219A; "Else" it is only read. Nothing puts it back afterwards, so it stays at the FX
    /// setting until the next play. sub08D1 has no such lines: the fall's HIT plays at whatever the
    /// FALL left.
    /// </summary>
    private void SetVolume()
    {
        if (FxVolume != 0) WaveOutSetVolume?.Invoke(FxVolume);
    }

    /// <summary>
    /// The waves' half of <c>mdrDeInitializeFX</c> (MUSIC.BAS:268-289): every sound with a wave open
    /// has it freed, and <c>M7208</c> takes WaveMixFreeWave's answer, 0, so the next play opens it
    /// afresh. Nothing else is reset - not gv2130, not the channels handed out, not gv2162. The
    /// session itself is closed by the caller, on the device.
    /// </summary>
    public void DeInitialize()
    {
        for (int n = 0; n < Wave.Length; n++)
            if (Wave[n] != 0)
            {
                _device.FreeWave(Wave[n]);
                Wave[n] = 0;
            }
    }

    /// <summary><c>sub1A31</c> (MUSIC.BAS:30-77).</summary>
    private void Sub1A31(int p, WaveMixRoutine mode)
    {
        string? path = _pathOf(p);
        if (path == null)
        {
            Wave[p] = 0; Id[p] = 0; Assigned[p] = 0;
            return;
        }
        if (Wave[p] == 0)
        {
            if (_open.Count - 1 == CacheSize) Sub1A3F();
            else _open.Add(0);
            int slot = _open.Count - 1;
            Wave[p] = _device.OpenWave(path);
            _open[slot] = p;
            if (mode == WaveMixRoutine.Sub09DE)
            {
                Assigned[p] = _timer();
                Id[p] = Fn19F4();
            }
            else Assigned[p] = 0;
        }
        bool hand = true;
        if (mode == WaveMixRoutine.Sub08D1)
        {
            Id[p] = LastHanded;
            return;
        }
        if (mode == WaveMixRoutine.Sub09DE && Assigned[p] > 0 && Assigned[p] + HoldSeconds > _timer())
            hand = false;
        if ((mode == WaveMixRoutine.Sub09DE && hand) || mode == WaveMixRoutine.Sub086D)
        {
            Id[p] = Fn19F4();
            if (mode == WaveMixRoutine.Sub09DE) Assigned[p] = _timer();
        }
    }

    /// <summary><c>sub1A3F</c> (MUSIC.BAS:250-261).</summary>
    private void Sub1A3F()
    {
        int first = _open[1];
        if (Wave[first] != 0)
        {
            _device.FreeWave(Wave[first]);
            Wave[first] = 0;
            Assigned[first] = 0;
        }
        for (int i = 1; i <= CacheSize - 1; i++) _open[i] = _open[i + 1];
    }

    /// <summary><c>fn19F4</c> (MUSIC.BAS:348-374).</summary>
    private int Fn19F4()
    {
        int tries = 0;
        bool busy;
        do
        {
            busy = false;
            LastHanded++;
            if (LastHanded > LastChannel) LastHanded = 1;
            if (tries > 7) break;
            for (int i = 1; i < _open.Count; i++)
            {
                int s = _open[i];
                if (Assigned[s] > 0 && Id[s] == LastHanded)
                {
                    if (Assigned[s] + HoldSeconds > _timer()) busy = true;
                    else Assigned[s] = 0;
                    break;
                }
            }
            tries++;
        } while (busy);
        return LastHanded;
    }
}

/// <summary>
/// WaveMix's channels: eight queues of waves (channel 0 to 7) mixed into one stream. A channel plays
/// the wave at the head of its queue and then the next; <see cref="FlushChannel"/> empties it,
/// the wave playing included; <see cref="Play"/> adds to its end (WMIX_QUEUEWAVE, the only flag the
/// original passes). The session is configured mono at 22 kHz (<c>fn1A08</c>, MUSIC.BAS:384-388:
/// WMIX_CONFIG_CHANNELS Or WMIX_CONFIG_SAMPLINGRATE, 1 channel, 22), and every game WAV is 8-bit mono
/// 22,050 Hz already. Channels are summed and clipped, without scaling, as WaveMix sums its 8-bit
/// samples; the output is 16-bit so that nothing is lost on the way out.
///
/// Thread-safe: the game calls in from the UI thread, the output thread calls <see cref="Mix"/>.
/// </summary>
public sealed class WaveMixChannels : IWaveMixDevice
{
    public const int Channels = 8;
    public const int Rate = 22050;

    private readonly object _gate = new();
    private readonly Func<string, short[]?> _load;
    private readonly Dictionary<int, short[]> _waves = new();
    private readonly List<Playing>[] _queues = Enumerable.Range(0, Channels).Select(_ => new List<Playing>()).ToArray();
    private int _nextHandle = 1;
    private bool _active = true;

    private sealed class Playing
    {
        public int Wave;
        public short[] Data = Array.Empty<short>();
        public int Pos;
    }

    /// <param name="load">A file's samples at <see cref="Rate"/>, mono, or null when it will not open.</param>
    public WaveMixChannels(Func<string, short[]?> load) => _load = load;

    public int OpenWave(string path)
    {
        short[]? data;
        try { data = _load(path); } catch { data = null; }
        if (data == null) return 0;
        lock (_gate)
        {
            if (_closed) return 0;
            int h = _nextHandle++;
            _waves[h] = data;
            return h;
        }
    }

    /// <summary>
    /// The wave's memory goes, so it is taken off every channel it is playing or queued on.
    /// </summary>
    public void FreeWave(int wave)
    {
        lock (_gate)
        {
            _waves.Remove(wave);
            foreach (var q in _queues) q.RemoveAll(p => p.Wave == wave);
        }
    }

    public void FlushChannel(int channel)
    {
        if (channel < 0 || channel >= Channels) return;
        lock (_gate) _queues[channel].Clear();
    }

    public void Play(int channel, int wave)
    {
        if (channel < 0 || channel >= Channels) return;   // "An invalid channel (i.e. > 7)" (MODULE13.BAS:24)
        lock (_gate)
        {
            if (!_active || !_waves.TryGetValue(wave, out var data)) return;
            _queues[channel].Add(new Playing { Wave = wave, Data = data });
        }
    }

    /// <summary>
    /// <c>WaveMixActivate</c>: false while the frame is minimised (FRMMAIN.BAS:211), which gives up
    /// the wave device - what was playing stops, and nothing plays until it is true again (223).
    /// </summary>
    public bool Active
    {
        get { lock (_gate) return _active; }
        set
        {
            lock (_gate)
            {
                _active = value;
                if (!value) foreach (var q in _queues) q.Clear();
            }
        }
    }

    private bool _closed;

    /// <summary>
    /// <c>WaveMixCloseSession</c> (MUSIC.BAS:280): the channels closed and emptied, every wave
    /// gone. <c>gv20C8</c> keeps the closed session's handle, so until <c>fn1A08</c> opens another
    /// (<see cref="OpenSession"/>) no wave opens on it and nothing plays.
    /// </summary>
    public void CloseSession()
    {
        lock (_gate)
        {
            _closed = true;
            _waves.Clear();
            foreach (var q in _queues) q.Clear();
        }
    }

    /// <summary><c>fn1A08</c> (MUSIC.BAS:376-399): a session opened, its channels, activated.</summary>
    public void OpenSession()
    {
        lock (_gate) { _closed = false; _active = true; }
    }

    /// <summary>Closed by <see cref="CloseSession"/> and not opened since.</summary>
    public bool Closed
    {
        get { lock (_gate) return _closed; }
    }

    /// <summary>The waves on a channel, the playing one first.</summary>
    public int[] Queued(int channel)
    {
        lock (_gate) return _queues[channel].Select(p => p.Wave).ToArray();
    }

    /// <summary>Whether anything is playing or queued anywhere.</summary>
    public bool Busy
    {
        get { lock (_gate) return _queues.Any(q => q.Count > 0); }
    }

    /// <summary>The next <paramref name="output"/>.Length samples of every channel together.</summary>
    public void Mix(Span<short> output)
    {
        Span<int> sum = output.Length <= 4096 ? stackalloc int[output.Length] : new int[output.Length];
        sum.Clear();
        lock (_gate)
        {
            foreach (var q in _queues)
            {
                int i = 0;
                while (i < output.Length && q.Count > 0)
                {
                    var head = q[0];
                    int n = Math.Min(output.Length - i, head.Data.Length - head.Pos);
                    for (int k = 0; k < n; k++) sum[i + k] += head.Data[head.Pos + k];
                    head.Pos += n; i += n;
                    if (head.Pos >= head.Data.Length) q.RemoveAt(0);
                }
            }
        }
        // WaveMix sums and clips; the wave device then plays the result at its own volume
        int level = DeviceVolume & 0xFFFF;
        for (int i = 0; i < output.Length; i++)
        {
            int clipped = Math.Clamp(sum[i], short.MinValue, short.MaxValue);
            output[i] = (short)(level == 0xFFFF ? clipped : (int)((long)clipped * level / 0xFFFF));
        }
    }

    private volatile int _deviceVolume = -1;

    /// <summary>
    /// The wave device's volume, as <c>waveOutSetVolume</c> takes it: the low word the left channel,
    /// the high word the right, 0 to &amp;HFFFF each (MMSYSTEM). The output is mono, and for a device
    /// with one channel only the low word counts. It scales what <see cref="Mix"/> hands the device,
    /// after the channels are summed - so, like the device's own volume, a change reaches what is
    /// already playing. -1 (&amp;HFFFFFFFF) is full, which is how the port's device opens; the
    /// original's started at whatever Windows' wave volume was.
    /// </summary>
    public int DeviceVolume
    {
        get => _deviceVolume;
        set => _deviceVolume = value;
    }
}

/// <summary>
/// The FX Vol. setting, <c>gv219A</c> - a Long in <c>waveOutSetVolume</c>'s own form, both words the
/// same. MORDOR.INI's FXVolume holds it as that number (<c>Str(gv219A)</c>, FRMOPTIO.BAS:242), read
/// back with <c>CLng</c> and "-65538" when the key is missing (MODULE1.BAS:983-987): &amp;HFFFEFFFE.
/// </summary>
public static class WaveVolume
{
    /// <summary>"-65538" (MODULE1.BAS:985): &amp;HFFFE on each side, the slider at its top.</summary>
    public const int Default = -65538;

    /// <summary>
    /// The slider's top. Its Max is in the form's binary, which the decompile does not give; a VB
    /// scroll bar goes no higher than 32767, and the default of &amp;HFFFE is 32767 times 2.
    /// </summary>
    public const int SliderMax = 32767;

    /// <summary>
    /// hsbVolume_Scroll (FRMOPTIO.BAS:396-412): <c>Hex(Value * 2)</c>, padded to four digits, put
    /// twice after "&amp;H" and read with <c>CLng</c> - so above &amp;H7FFF the Long is negative.
    /// </summary>
    public static int FromSlider(int value)
    {
        string h = (Math.Clamp(value, 0, SliderMax) * 2).ToString("X").PadLeft(4, '0');
        return unchecked((int)Convert.ToUInt32(h + h, 16));
    }

    /// <summary>
    /// Form_Load (FRMOPTIO.BAS:354-356): <c>(gv219A And &amp;HFFFF&amp;) / 2</c> into the slider's
    /// Integer Value, which VB rounds to even. Only when gv219A is not 0; at 0 the slider keeps the
    /// value it was designed with, which the decompile does not give - the port shows 0.
    /// </summary>
    public static int ToSlider(int raw) =>
        raw == 0 ? 0 : (int)Math.Round((raw & 0xFFFF) / 2.0, MidpointRounding.ToEven);
}

/// <summary>
/// A RIFF WAVE file's PCM as 16-bit mono samples at <see cref="WaveMixChannels.Rate"/>: 8-bit samples
/// are centred and scaled by 256, stereo is averaged, another rate is resampled linearly. Every WAV
/// the game ships is 8-bit mono 22,050 Hz, so for those this is the bytes, centred and scaled.
/// </summary>
public static class WavePcm
{
    public static short[]? Decode(byte[] file, int rate = WaveMixChannels.Rate)
    {
        if (file.Length < 12 || Ascii(file, 0) != "RIFF" || Ascii(file, 8) != "WAVE") return null;
        int at = 12;
        int format = 0, channels = 0, sampleRate = 0, bits = 0;
        int dataAt = -1, dataLen = 0;
        while (at + 8 <= file.Length)
        {
            string id = Ascii(file, at);
            int len = BitConverter.ToInt32(file, at + 4);
            if (len < 0) return null;
            int body = at + 8;
            if (id == "fmt " && body + 16 <= file.Length)
            {
                format = BitConverter.ToUInt16(file, body);
                channels = BitConverter.ToUInt16(file, body + 2);
                sampleRate = BitConverter.ToInt32(file, body + 4);
                bits = BitConverter.ToUInt16(file, body + 14);
            }
            else if (id == "data")
            {
                dataAt = body;
                dataLen = Math.Min(len, file.Length - body);
                break;
            }
            at = body + len + (len & 1);
        }
        if (format != 1 || dataAt < 0 || channels < 1 || sampleRate <= 0 || (bits != 8 && bits != 16)) return null;

        int bytesPer = bits / 8;
        int frames = dataLen / (bytesPer * channels);
        var mono = new short[frames];
        for (int f = 0; f < frames; f++)
        {
            int total = 0;
            for (int c = 0; c < channels; c++)
            {
                int o = dataAt + (f * channels + c) * bytesPer;
                total += bits == 8 ? (file[o] - 128) << 8 : BitConverter.ToInt16(file, o);
            }
            mono[f] = (short)(total / channels);
        }
        if (sampleRate == rate) return mono;

        int outFrames = (int)((long)frames * rate / sampleRate);
        var res = new short[outFrames];
        double step = (double)sampleRate / rate;
        for (int i = 0; i < outFrames; i++)
        {
            double pos = i * step;
            int a = (int)pos;
            int b = Math.Min(a + 1, frames - 1);
            double t = pos - a;
            res[i] = (short)Math.Round(mono[a] + (mono[b] - mono[a]) * t);
        }
        return res;
    }

    private static string Ascii(byte[] b, int at) =>
        at + 4 <= b.Length ? System.Text.Encoding.ASCII.GetString(b, at, 4) : "";
}
