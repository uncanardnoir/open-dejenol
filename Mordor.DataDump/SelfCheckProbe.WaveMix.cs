using System.Text.RegularExpressions;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>A WaveMix that writes down what it is asked, for <see cref="WaveMixSession"/>.</summary>
    private sealed class FakeWaveMix : IWaveMixDevice
    {
        public readonly List<string> Log = new();
        public readonly Dictionary<int, string> Opened = new();
        private int _next = 1;
        public int OpenWave(string path) { int h = _next++; Opened[h] = path; Log.Add("open " + path); return h; }
        public void FreeWave(int wave) => Log.Add("free " + Opened.GetValueOrDefault(wave, "?"));
        public void FlushChannel(int channel) => Log.Add("flush " + channel);
        public void Play(int channel, int wave) => Log.Add($"play {channel} {Opened.GetValueOrDefault(wave, "-")}");
        public string Take() { string s = string.Join(", ", Log); Log.Clear(); return s; }
    }

    /// <summary>
    /// The port's WaveMix (Mordor.Core/WaveMix.cs) against MUSIC.BAS: the channel each routine
    /// plays on, the five-second hold, the seven open waves and which one goes, flush against
    /// queue on real samples, and every effect call site in the App routed through the routine
    /// its original call site uses.
    /// </summary>
    private static void WaveMixChecks(string dir)
    {
        Console.WriteLine("\nsound effects mixed as WAVMIX16 mixes them (MUSIC.BAS, MODULE13.BAS)");

        // ---- the source: the numbers WaveMixSession is built on ----
        string music = PortSource("decompiled-named", "MUSIC.BAS");
        string mod1 = PortSource("decompiled-named", "MODULE1.BAS");
        string main = PortSource("decompiled-named", "MAIN.TXT");
        string sub1A31 = Between(music, "Sub sub1A31 (", "End Sub");
        string sub1A3F = Between(music, "Sub sub1A3F (", "End Sub");
        string fn19F4 = Between(music, "Function fn19F4 (", "End Function");
        string sub09DE = Between(music, "Sub sub09DE (", "End Sub");
        string sub086D = Between(music, "Sub sub086D (", "End Sub");
        string sub08D1 = Between(music, "Sub sub08D1 (", "End Sub");
        string fn1A08 = Between(music, "Function fn1A08 (", "End Function");
        Check("gv2130 starts at ReDim (0), used from 1 (MODULE1)", mod1.Contains("ReDim gv2130(0)"), true);
        Check("sub1A31 makes room once UBound(gv2130) = 7", sub1A31.Contains("If  UBound(gv2130) = 7 Then\r\n            sub1A3F") || sub1A31.Contains("If  UBound(gv2130) = 7 Then\n            sub1A3F"), true);
        Check("so at most WaveMixSession.CacheSize = 7 waves are open", WaveMixSession.CacheSize, 7);
        Check("sub1A3F frees gv2130(1) and moves 2..7 down",
              sub1A3F.Contains("extfn1E45(gv20C8, gv20FE(gv2130(1)).M7208)") && sub1A3F.Contains("gv2130(l0168) = gv2130(l0168 + 1)"), true);
        Check("fn19F4 hands out 1..7 in turn",
              fn19F4.Contains("gv2162 = gv2162 + 1") && (fn19F4.Contains("If  gv2162 > 7 Then\r\n            gv2162 = 1") | fn19F4.Contains("If  gv2162 > 7 Then\n            gv2162 = 1")), true);
        Check("fn19F4 passes over a channel held under five seconds, eight tries",
              fn19F4.Contains("(gv20FE(gv2130(l018E)).M4EE6 + 5) > Timer") && fn19F4.Contains("If  l0190 > 7 Then"), true);
        Check("sub09DE flushes the sound's channel, then plays", sub09DE.IndexOf("extfn1E17(gv20C8, gv20FE(p0128).Id, mc0138)") is int f && f > 0
              && sub09DE.IndexOf("extfn1E08(l0130)") > f, true);
        Check("sub086D plays without a flush", sub086D.Contains("extfn1E08(") && !sub086D.Contains("extfn1E17"), true);
        Check("sub08D1 plays without a flush", sub08D1.Contains("extfn1E08(") && !sub08D1.Contains("extfn1E17"), true);
        Check("their flags: mc0134 And mc0136 = 4 And 1 = 0, WMIX_QUEUEWAVE",
              music.Contains("Const mc0134 = 4 ") && music.Contains("Const mc0136 = 1 ") && (4 & 1) == 0
              && sub09DE.Contains("M7E26 = mc0134 And mc0136") && sub086D.Contains("M7E26 = mc0134 And mc0136"), true);
        Check("sub08D1's flags and mode are never-assigned 0s (m0166, m00C4)",
              sub08D1.Contains("Static m0166 As Integer") && sub08D1.Contains("M7E26 = m0166") && sub08D1.Contains("sub1A31 p015C, m00C4")
              && !Regex.IsMatch(music, @"\bm00C4 = "), true);
        Check("the routines' modes: sub086D mc00C6 = 1, sub09DE mc00C8 = 2",
              sub086D.Contains("sub1A31 p0150, mc00C6") && sub09DE.Contains("sub1A31 p0128, mc00C8")
              && (int)WaveMixRoutine.Sub086D == 1 && (int)WaveMixRoutine.Sub09DE == 2 && (int)WaveMixRoutine.Sub08D1 == 0, true);
        Check("fn1A08: mono, 22 kHz, all eight channels opened",
              fn1A08.Contains("M7EC4 = 1") && fn1A08.Contains("M7ED1 = 22") && fn1A08.Contains("extfn1DF2(gv20C8, 0, mc0176)")
              && WaveMixChannels.Channels == 8 && WaveMixChannels.Rate == 22050, true);
        Check("M4EE6 is a Single, so Timer is kept to the fraction", Regex.IsMatch(main, @"Type T71F8\s+M7208 As Long\s+Id As Integer\s+M4EE6 As Single"), true);

        // ---- the session on a fake WaveMix ----
        float now = 100;
        var dev = new FakeWaveMix();
        var missing = new HashSet<int> { 0 };   // FIGHT.WAV is not shipped
        var s = new WaveMixSession(dev, () => now, n => missing.Contains(n) ? null : SoundFiles.Names[n], 72);
        int A = 2, B = 3, C = 4, D = 5, E = 6, F = 7, G = 8, H = 9;   // HIT KILL MAGIC OPEN FALL BLACKOUT SCURRY MOAN

        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("sub09DE, first play: opened, channel 1, flushed, queued", dev.Take(), "open HIT, flush 1, play 1 HIT");
        now = 100.5f;
        s.Play(B, WaveMixRoutine.Sub09DE);
        Check("another sound takes channel 2", dev.Take(), "open KILL, flush 2, play 2 KILL");
        now = 104.9f;
        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("the first again inside five seconds: its own channel, flushed (cuts itself off)", dev.Take(), "flush 1, play 1 HIT");
        now = 105f;
        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("five seconds after the giving (not the last play): a channel afresh", dev.Take(), "flush 3, play 3 HIT");
        Check("and the hold restarts from now", s.Assigned[A], 105f);

        // seven sounds holding all seven channels
        now = 200;
        dev = new FakeWaveMix();
        s = new WaveMixSession(dev, () => now, n => SoundFiles.Names[n], 72);
        foreach (int n in new[] { A, B, C, D, E, F, G }) s.Play(n, WaveMixRoutine.Sub09DE);
        Check("seven sounds on channels 1-7", string.Join(",", new[] { A, B, C, D, E, F, G }.Select(n => s.Id[n])), "1,2,3,4,5,6,7");
        dev.Take();
        now = 201;
        s.Play(B, WaveMixRoutine.Sub086D);
        Check("sub086D with every channel held: eight tries, then the ninth turn's channel", s.Id[B] + " " + s.LastHanded, "2 2");
        Check("queued there without a flush", dev.Take(), "play 2 KILL");
        now = 210;
        s.Play(B, WaveMixRoutine.Sub086D);
        Check("a hold run out: the channel is taken and the stale stamp cleared", s.Id[B] + " " + s.Assigned[C], "3 0");
        dev.Take();
        now = 211;
        s.Play(H, WaveMixRoutine.Sub09DE);
        Check("an eighth wave frees the one opened first (HIT), whatever was played since", dev.Take(), "free HIT, open MOAN, flush 4, play 4 MOAN");
        Check("the open list moves down one", string.Join(",", s.OpenSounds.Select(SoundFiles.Name)), "KILL,MAGIC,OPEN,FALL,BLACKOUT,SCURRY,MOAN");
        Check("the freed sound's wave and stamp are gone, its channel kept", $"{s.Wave[A]} {s.Assigned[A]} {s.Id[A]}", "0 0 1");
        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("and it opens again, freeing the next oldest", dev.Take().StartsWith("free KILL, open HIT"), true);

        // sub086D and sub08D1
        now = 300;
        dev = new FakeWaveMix();
        s = new WaveMixSession(dev, () => now, n => missing.Contains(n) ? null : SoundFiles.Names[n], 72);
        s.Play(E, WaveMixRoutine.Sub086D);
        s.Play(E, WaveMixRoutine.Sub086D);
        Check("sub086D twice: a new channel each time, no flush (the two mix)", dev.Take(), "open FALL, play 1 FALL, play 2 FALL");
        Check("sub086D leaves no hold", s.Assigned[E], 0f);
        s.Play(E, WaveMixRoutine.Sub086D);
        s.Play(A, WaveMixRoutine.Sub08D1);
        Check("sub086D FALL: sub08D1 HIT - the HIT queued behind the FALL on its channel", dev.Take(), "play 3 FALL, open HIT, play 3 HIT");
        s.Play(0, WaveMixRoutine.Sub09DE);
        s.Play(0, WaveMixRoutine.Sub086D);
        Check("a missing file: nothing opened or played by sub09DE or sub086D", dev.Take(), "");
        Check("and its channel is set to 0 (MUSIC.BAS:36)", s.Id[0], 0);
        s.Play(0, WaveMixRoutine.Sub08D1);
        Check("sub08D1 does not test the wave: it asks for wave 0 on channel 0", dev.Take(), "play 0 -");

        // ---- flush against queue, on real samples ----
        static short[] Tone(short v, int n) => Enumerable.Repeat(v, n).ToArray();
        var waves = new Dictionary<string, short[]> { ["A"] = Tone(1000, 10), ["B"] = Tone(2000, 5), ["C"] = Tone(30000, 4) };
        var mix = new WaveMixChannels(p => waves.GetValueOrDefault(p));
        int wa = mix.OpenWave("A"), wb = mix.OpenWave("B"), wc = mix.OpenWave("C");
        Check("a file that will not open is wave 0", mix.OpenWave("none"), 0);
        string Out(int n) { var o = new short[n]; mix.Mix(o); return string.Join(",", o); }
        mix.Play(1, wa); mix.Play(2, wb);
        Check("two channels mix: summed", Out(8), "3000,3000,3000,3000,3000,1000,1000,1000");
        Check("the longer one plays on alone", Out(4), "1000,1000,0,0");
        mix.Play(1, wa); mix.Play(1, wb);
        Check("queued on one channel: one after the other", Out(16), "1000,1000,1000,1000,1000,1000,1000,1000,1000,1000,2000,2000,2000,2000,2000,0");
        mix.Play(1, wa); Out(3); mix.FlushChannel(1); mix.Play(1, wb);
        Check("flushed, then queued: the first is cut off", Out(6), "2000,2000,2000,2000,2000,0");
        mix.Play(1, wc); mix.Play(2, wc);
        Check("clipped at the top, not scaled", Out(2), "32767,32767");
        mix.FlushChannel(1); mix.FlushChannel(2);
        mix.Play(1, wa); mix.Play(2, wa); mix.FreeWave(wa);
        Check("a freed wave leaves every channel", mix.Busy, false);
        mix.Play(3, wb); mix.Active = false;
        Check("WaveMixActivate False: everything stops", mix.Busy, false);
        mix.Play(3, wb);
        Check("and nothing plays until True", mix.Busy, false);
        mix.Active = true; mix.Play(3, wb);
        Check("True again: it plays", mix.Queued(3).Length, 1);
        mix.Play(8, wb);
        Check("channel 8 does not exist", mix.Queued(3).Length, 1);

        // the session over the channels: a sound cuts itself off, two sounds mix
        now = 400;
        var pcm = new Dictionary<string, short[]> { ["HIT"] = Tone(100, 6), ["KILL"] = Tone(10, 6) };
        mix = new WaveMixChannels(p => pcm.GetValueOrDefault(p));
        s = new WaveMixSession(mix, () => now, n => pcm.ContainsKey(SoundFiles.Names[n]) ? SoundFiles.Names[n] : null, 72);
        s.Play(A, WaveMixRoutine.Sub09DE); Out(2); s.Play(A, WaveMixRoutine.Sub09DE);
        Check("sub09DE HIT twice: the second cuts the first", Out(8), "100,100,100,100,100,100,0,0");
        s.Play(A, WaveMixRoutine.Sub09DE); Out(2); s.Play(B, WaveMixRoutine.Sub09DE);
        Check("sub09DE HIT then KILL: mixed", Out(8), "110,110,110,110,10,10,0,0");
        s.Play(A, WaveMixRoutine.Sub086D); Out(2); s.Play(A, WaveMixRoutine.Sub086D);
        Check("sub086D HIT twice: mixed", Out(8), "200,200,200,200,100,100,0,0");

        // ---- the FX volume: gv219A set on the wave device around each play ----
        Check("sub09DE: gv219A not 0, the device read and set to it if it differs; 0, only read (MUSIC.BAS:157-164)",
              sub09DE.Replace("\r\n", "\n").Contains("If  gv219A <> False Then\n        l012C = extfn19A6(0, l0132)\n        If  l0132 <> gv219A Then\n            l012C = extfn1992(0, gv219A)")
              && sub09DE.IndexOf("extfn1992(0, gv219A)") < sub09DE.IndexOf("extfn1E08(l0130)"), true);
        Check("sub086D the same (218-225); sub08D1 never",
              sub086D.Replace("\r\n", "\n").Contains("If  l015A <> gv219A Then\n            l0154 = extfn1992(0, gv219A)")
              && sub086D.IndexOf("extfn1992(0, gv219A)") < sub086D.IndexOf("extfn1E08(l0158)") && !sub08D1.Contains("gv219A"), true);
        Check("extfn1992 is waveOutSetVolume, extfn19A6 waveOutGetVolume",
              music.Contains("extfn19A6 Lib \"MMSYSTEM\" Alias \"waveOutGetVolume\"") && music.Contains("extfn1992 Lib \"MMSYSTEM\" Alias \"waveOutSetVolume\""), true);
        string optio = PortSource("decompiled-named", "FRMOPTIO.BAS");
        Check("FXVolume missing: \"-65538\" (MODULE1.BAS:983-987), WaveVolume.Default",
              Regex.IsMatch(PortSource("decompiled-named", "MODULE1.BAS"), "\"FXVolume\"\\)\\r?\\n    If  l06E2 = \"\" Then\\r?\\n        l06E2 = \"-65538\"") && WaveVolume.Default == -65538
              && new GameOptions().FXVolume == -65538 && PortSource("Mordor.Core", "GameOptions.cs").Contains("ini.Number(Section, \"FXVolume\", WaveVolume.Default)"), true);
        Check("the slider: Hex(Value * 2), four digits, twice, CLng (FRMOPTIO.BAS:401-406)",
              optio.Contains("l0104 = Hex(hsbVolume(Index).Value * 2)") && optio.Contains("l010A = CLng(\"&H\" + l0104 + l0104)"), true);
        Check("slider to gv219A: 32767, 16384, 1, 0",
              $"{WaveVolume.FromSlider(32767)} {WaveVolume.FromSlider(16384)} {WaveVolume.FromSlider(1)} {WaveVolume.FromSlider(0)}",
              $"-65538 {unchecked((int)0x80008000)} {0x00020002} 0");
        Check("gv219A to the slider, (gv219A And &HFFFF&) / 2, at 0 left alone (354-356)",
              $"{WaveVolume.ToSlider(-65538)} {WaveVolume.ToSlider(unchecked((int)0x80008000))} {WaveVolume.ToSlider(0x00030003)} {WaveVolume.ToSlider(0)}",
              "32767 16384 2 0");
        var set = new List<int>();
        var dv = new FakeWaveMix();
        float tv = 500;
        var sv = new WaveMixSession(dv, () => tv, n => n == 9 ? null : SoundFiles.Names[n], 72) { WaveOutSetVolume = v => { set.Add(v); dv.Log.Add("volume " + v); } };
        sv.FxVolume = 0x40004000;
        sv.Play(A, WaveMixRoutine.Sub09DE);
        Check("sub09DE: the volume set, then the flush and the play", dv.Take(), $"open HIT, volume {0x40004000}, flush 1, play 1 HIT");
        sv.Play(B, WaveMixRoutine.Sub086D);
        Check("sub086D: the volume set, then the play", dv.Take(), $"open KILL, volume {0x40004000}, play 2 KILL");
        sv.Play(A, WaveMixRoutine.Sub08D1);
        Check("sub08D1: no volume", dv.Take(), "play 2 HIT");
        sv.Play(9, WaveMixRoutine.Sub09DE);
        Check("a sound with no file: exits before the volume", dv.Take(), "");
        sv.FxVolume = 0; set.Clear();
        sv.Play(A, WaveMixRoutine.Sub09DE); sv.Play(B, WaveMixRoutine.Sub086D);
        Check("gv219A 0: the device left as it was", set.Count, 0);

        mix = new WaveMixChannels(p => pcm.GetValueOrDefault(p));
        s = new WaveMixSession(mix, () => now, n => pcm.ContainsKey(SoundFiles.Names[n]) ? SoundFiles.Names[n] : null, 72)
            { WaveOutSetVolume = v => mix.DeviceVolume = v };
        Check("the port's device opens at full", mix.DeviceVolume, -1);
        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("gv219A 0: HIT at full", Out(2), "100,100");
        s.FxVolume = WaveVolume.FromSlider(16384);
        s.Play(B, WaveMixRoutine.Sub09DE);
        Check("half: the mix after summing, what was playing too", Out(2), "55,55");
        s.FxVolume = 0;
        s.Play(B, WaveMixRoutine.Sub09DE);
        Check("then gv219A 0: the device stays where the last play left it", Out(2), "55,55");
        mix.DeviceVolume = unchecked((int)0xFFFF0000);
        Check("one channel out: the low word alone counts", Out(2), "0,0");
        mix.DeviceVolume = -1; Out(8);
        string app = PortSource("Mordor.App", "Audio.cs"), opts = PortSource("Mordor.App", "OptionsWindow.cs");
        Check("the App: the session sets the port's own output, never Windows' volume",
              app.Contains("_session.WaveOutSetVolume = v => channels.DeviceVolume = v;") && app.Contains("SetFxVolume(o.FXVolume);")
              && !app.Contains("waveOutSetVolume(") && !PortSource("Mordor.App", "WaveOutStream.cs").Contains("waveOutSetVolume"), true);
        Check("the Options slider: live, with sub09DE's HIT, put back unless OK (FRMOPTIO.BAS:148, 396-412)",
              opts.Contains("Audio.SetFxVolume(_fxRaw);") && opts.Contains("Audio.Play(\"HIT\");")
              && opts.Contains("if (!Accepted) { Audio.SetFxVolume(_o.FXVolume);") && opts.Contains("_o.FXVolume = _fxRaw;")
              && optio.Replace("\r\n", "\n").Contains("gv219A = l010A\n                sub09DE mc010E") && optio.Contains("Const mc010E = 2 ") && SoundFiles.Names[2] == "HIT", true);

        // ---- No Sound FX, live in the Options window ----
        string nofx = Between(optio.Replace("\r\n", "\n"), "            Case mc0032\n", "            Case mc0034");
        Check("No Sound FX ticked: M5EFC at once, slider greyed, mdrDeInitializeFX (FRMOPTIO.BAS:89-92)",
              nofx.IndexOf("gv1A66.M5EFC = -Abs(chkCheck(Index).Value)") is int at && at >= 0
              && nofx.IndexOf("hsbVolume(mc0052).Enabled = False\n                    mdrDeInitializeFX") > at, true);
        Check("cleared: slider back, fn1A08, and on failure the box and the slider greyed, else sub09DE 11 (93-98)",
              nofx.Contains("hsbVolume(mc0052).Enabled = True\n                    If  fn1A08() = False Then\n                        MsgBox \"Error initializing Sound FX!\"")
              && nofx.Contains("\"Sound FX\"\n                        hsbVolume(mc0052).Enabled = False\n                    Else\n                        sub09DE mc009E")
              && optio.Contains("Const mc009E = 11 ") && SoundFiles.Names[11] == "RAISE", true);
        string cancel = Between(optio.Replace("\r\n", "\n"), "        Case mc0028\n", "        Case m001A");
        Check("Cancel puts the flag back and nothing else of the session's (138-155)",
              cancel.Contains("gv1A66.M5EFC = m0056") && !cancel.Contains("mdrDeInitializeFX") && !cancel.Contains("fn1A08"), true);
        Check("mdrDeInitializeFX frees each open wave, M7208 = the free's 0, then closes the session (MUSIC.BAS:273-280)",
              music.Contains("gv20FE(l0172).M7208 = extfn1E45(gv20C8, gv20FE(l0172).M7208)") && music.Contains("l0174 = extfn1E58(gv20C8)")
              && music.Contains("Declare Function extfn1E58 Lib \"WAVMIX16.DLL\" Alias \"WaveMixCloseSession\"") | PortSource("decompiled-named", "MODULE13.BAS").Contains("WaveMixCloseSession"), true);
        var dd = new FakeWaveMix();
        var sd = new WaveMixSession(dd, () => 600, n => SoundFiles.Names[n], 72);
        sd.Play(A, WaveMixRoutine.Sub09DE); sd.Play(B, WaveMixRoutine.Sub09DE); dd.Take();
        sd.DeInitialize();
        Check("DeInitialize: every open wave freed", dd.Take(), "free HIT, free KILL");
        sd.Play(A, WaveMixRoutine.Sub09DE);
        Check("and the next play opens it afresh, on a channel handed out afresh", dd.Take(), "open HIT, flush 3, play 3 HIT");
        mix = new WaveMixChannels(p => pcm.GetValueOrDefault(p));
        s = new WaveMixSession(mix, () => now, n => pcm.ContainsKey(SoundFiles.Names[n]) ? SoundFiles.Names[n] : null, 72);
        s.Play(A, WaveMixRoutine.Sub09DE); Out(2);
        s.DeInitialize(); mix.CloseSession();
        Check("the session closed: what was playing stops", Out(2), "0,0");
        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("and nothing opens or plays on it - a tick then Cancel stays silent", Out(2) + " " + s.Wave[A], "0,0 0");
        mix.OpenSession();
        s.Play(A, WaveMixRoutine.Sub09DE);
        Check("fn1A08 again: it plays", Out(2), "100,100");
        Out(8);
        Check("the App: the box live, Cancel restoring the flag only",
              opts.Contains("Audio.NoFxTicked();") && opts.Contains("if (!Audio.NoFxCleared())") && opts.Contains("Audio.RestoreNoFx(_o.NoFX);")
              && opts.Contains("\"Error initializing Sound FX!\\n\\nDo you have a WAVE compatible Sound Card Installed?  Are the drivers current?\"")
              && !Between(app, "public static void RestoreNoFx(", "\n    }").Contains("Session"), true);

        // ---- the WAVs ----
        byte[] Wav(int rate, int bits, int channels, byte[] data)
        {
            var b = new List<byte>();
            void S(string t) => b.AddRange(System.Text.Encoding.ASCII.GetBytes(t));
            void I(int v) => b.AddRange(BitConverter.GetBytes(v));
            void W(short v) => b.AddRange(BitConverter.GetBytes(v));
            S("RIFF"); I(36 + data.Length); S("WAVE");
            S("fmt "); I(16); W(1); W((short)channels); I(rate); I(rate * channels * bits / 8); W((short)(channels * bits / 8)); W((short)bits);
            S("data"); I(data.Length); b.AddRange(data);
            return b.ToArray();
        }
        Check("8-bit mono 22,050: centred and scaled", string.Join(",", WavePcm.Decode(Wav(22050, 8, 1, new byte[] { 128, 255, 0 }))!), "0,32512,-32768");
        Check("11,025: twice as many samples, in between interpolated",
              string.Join(",", WavePcm.Decode(Wav(11025, 8, 1, new byte[] { 128, 192, 128 }))!), "0,8192,16384,8192,0,0");
        Check("16-bit stereo: averaged", string.Join(",", WavePcm.Decode(Wav(22050, 16, 2, new byte[] { 0x10, 0x27, 0x30, 0x75 }))!), "20000");
        Check("not a WAV: null", WavePcm.Decode(new byte[] { 1, 2, 3 }) == null, true);
        var files = Directory.GetFiles(dir, "*.WAV");
        int good = files.Count(p => WavePcm.Decode(File.ReadAllBytes(p)) is short[] d && d.Length > 0);
        Check("every game WAV decodes", $"{good} of {files.Length}", $"{files.Length} of {files.Length}");
        int native = files.Count(p => { var h = File.ReadAllBytes(p); return BitConverter.ToInt32(h, 24) == 22050 && BitConverter.ToInt16(h, 22) == 1 && BitConverter.ToInt16(h, 34) == 8; });
        Check("and every one is 8-bit mono 22,050 Hz, WaveMix's own format", native, files.Length);

        // ---- the routing: each App call site through its original's routine ----
        // the original's call sites in every module, MUSIC.BAS's own included (all sub09DE, in sub0CB7,
        // sub0E99, sub1005 and sub0D35), resolved through each module's Consts and MAIN.TXT's Globals
        var by = new Dictionary<string, HashSet<string>> { ["sub09DE"] = new(), ["sub086D"] = new(), ["sub08D1"] = new() };
        string named = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "decompiled-named", "MUSIC.BAS")))!;
        foreach (string file in Directory.GetFiles(named, "*.BAS"))
        {
            string src = File.ReadAllText(file);
            var consts = new Dictionary<string, int>();
            foreach (Match m in Regex.Matches(src, @"Const (m\w+) = (\d+)")) consts[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            foreach (Match m in Regex.Matches(main, @"Global Const (gc\w+) = (\d+)")) consts[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            foreach (Match m in Regex.Matches(src, @"\b(sub09DE|sub086D|sub08D1) (\w+)(?: \+ Int\(Rnd \* (\d+)\))?"))
            {
                if (!consts.TryGetValue(m.Groups[2].Value, out int n)) continue;   // Int(Rnd * 71), the credits
                int spread = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 1;
                for (int k = 0; k < spread; k++)
                    if (n + k < SoundFiles.Names.Length) by[m.Groups[1].Value].Add(SoundFiles.Names[n + k]);
            }
        }
        var only086D = by["sub086D"].Except(by["sub09DE"]).OrderBy(x => x).ToList();
        Check("sounds the original plays only through sub086D", string.Join(",", only086D),
              "DIE,DROWN,DSTSLAM,EXTING,FALL,FOG,MAKELEV,MOAN,SCURRY,WADE,WATER,WATER1");
        Check("and through sub08D1 only HIT", string.Join(",", by["sub08D1"]), "HIT");

        var calls = new List<(string File, string Routine, List<string> Names, string Arg)>();
        foreach (string file in Directory.GetFiles(Path.GetFullPath(Path.Combine(named, "..", "Mordor.App")), "*.cs"))
        {
            string src = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(src, @"Audio\.(Play|PlayOnNextChannel|PlayAfterLast)\("))
            {
                int depth = 0, i = m.Index + m.Length - 1, end = i;
                for (; i < src.Length; i++)
                {
                    if (src[i] == '(') depth++;
                    else if (src[i] == ')' && --depth == 0) { end = i; break; }
                }
                string arg = src.Substring(m.Index + m.Length, end - m.Index - m.Length);
                calls.Add((Path.GetFileName(file), m.Groups[1].Value,
                           Regex.Matches(arg, @"""([A-Z0-9-]+)""").Select(x => x.Groups[1].Value).ToList(), arg));
            }
        }
        var wrong09DE = calls.Where(c => c.Routine == "Play").SelectMany(c => c.Names).Where(n => by["sub086D"].Contains(n) && !by["sub09DE"].Contains(n)).Distinct().ToList();
        Check("no App sub09DE call plays a sound the original plays only through sub086D", string.Join(",", wrong09DE), "");
        var unknown = calls.SelectMany(c => c.Names).Where(n => !SoundFiles.Names.Contains(n)).Distinct().ToList();
        Check("every sound the App names is in the SoundFiles table", string.Join(",", unknown), "");
        var next = calls.Where(c => c.Routine == "PlayOnNextChannel").SelectMany(c => c.Names).Where(n => !by["sub086D"].Contains(n)).Distinct().ToList();
        Check("every App sub086D call plays a sound the original plays through sub086D", string.Join(",", next), "");
        string dungeon = PortSource("Mordor.App", "DungeonWindow.axaml.cs");
        Check("the fall: sub086D FALL then sub08D1 HIT, both places (MAZEMOD.BAS:1762, 1868)",
              Regex.Matches(dungeon, @"Audio\.PlayOnNextChannel\(""FALL""\); Audio\.PlayAfterLast\(""HIT""\);").Count + " of "
              + calls.Count(c => c.Routine == "PlayAfterLast"), "2 of 2");
        // the sounds the original plays through both: these App sites are its sub086D ones
        foreach (var (what, sound) in new[] { ("TELEPORT, the spell and the square (MAINMOD2.BAS:5815, MAZEMOD.BAS:1789)", "TELEPORT"),
                                              ("RAISE, every raise (MAINMOD2.BAS:349, 3100; FRMMORG.BAS:100)", "RAISE"),
                                              ("POISON, the complications (MAINMOD2.BAS:1652; FRMMORG.BAS:96)", "POISON"),
                                              ("WITHER, the sanctuary trip (MAINMOD2.BAS:1350)", "WITHER") })
            Check(what + " through sub086D",
                  // the one sub09DE RAISE is No Sound FX cleared, Play inside Audio.NoFxCleared (FRMOPTIO.BAS:97), held below
                  calls.Count(c => c.Names.Contains(sound) && c.Routine == "Play") + " sub09DE, "
                  + (calls.Count(c => c.Names.Contains(sound) && c.Routine == "PlayOnNextChannel") > 0 ? "some" : "no") + " sub086D", "0 sub09DE, some sub086D");
        Check("and RAISE through sub09DE only for No Sound FX cleared, in Audio (FRMOPTIO.BAS:97)",
              string.Join(",", calls.Where(c => c.Names.Contains("RAISE") && c.Routine == "Play").Select(c => c.File)) + " "
              + Between(PortSource("Mordor.App", "Audio.cs"), "public static bool NoFxCleared()", "\n    }").Contains("Play(\"RAISE\");"), " True");   // Audio.cs calls its own Play, which the App scan does not count
        // three port sounds with no call in the original, gone
        var fogs = calls.Where(c => c.Names.Contains("FOG")).ToList();
        Check("FOG only on the clock, 25% in fog (FRMDUNM.BAS:589-591); none on stepping in",
              fogs.Count + " " + Regex.Matches(dungeon, @"Floor\.FOG\) && _rng\.NextDouble\(\) \* 100 < 25\) Audio\.PlayOnNextChannel\(""FOG""\)").Count, "1 1");
        var hellos = calls.Where(c => c.Names.Contains("HELLO")).Select(c => c.File).ToList();
        Check("HELLO only for strangers in the dungeon (MAZEMOD.BAS:372); none on arriving in town",
              string.Join(",", hellos), "DungeonWindow.axaml.cs");
        int compAt = dungeon.IndexOf("MAINMOD2.BAS:1072-1087"), compEnd = dungeon.IndexOf("new SpellThrown.Target.Companion(slot)", Math.Max(compAt, 0));
        Check("a companion raised by spell is silent (MAINMOD2.BAS:1072-1087)",
              compAt > 0 && compEnd > compAt && !dungeon[compAt..compEnd].Contains("Audio."), true);
        Check("an item's MAGIC (MAINMOD2.BAS:6160) through sub086D, all three",
              Regex.Matches(dungeon, @"ItemUse\.SoundIn\(line\) is string sound\) Audio\.PlayOnNextChannel\(sound\)").Count
              + " " + calls.Count(c => c.Arg == "sound" && c.Routine == "Play"), "3 0");
        Check("ItemUse's only sound is MAGIC", Regex.Matches(PortSource("Mordor.Core", "ItemUse.cs"), @"Sound\(""(\w+)""\)").Select(m => m.Groups[1].Value).Distinct().Single(), "MAGIC");
        string audio = PortSource("Mordor.App", "Audio.cs");
        Check("a fight's sounds and a spell's go through sub09DE", audio.Contains("foreach (string n in names ?? Array.Empty<string>()) Play(n);")
              && audio.Contains("public static void Play(string name) => Effect(name, WaveMixRoutine.Sub09DE);"), true);
        Check("No Sound FX and MORDOR_MUTE stop every routine before WaveMix", audio.Contains("if (!Enabled || !OperatingSystem.IsWindows()) return;\r\n        if (FramePause.Paused) return;")
              || audio.Contains("if (!Enabled || !OperatingSystem.IsWindows()) return;\n        if (FramePause.Paused) return;"), true);
        Check("winmm's PlaySound is gone", audio.Contains("PlaySound("), false);
    }
}
