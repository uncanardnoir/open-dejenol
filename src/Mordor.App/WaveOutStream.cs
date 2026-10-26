using System;
using System.Runtime.InteropServices;
using System.Threading;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The output end of the port's WaveMix: a winmm <c>waveOut</c> stream, 16-bit mono at
/// <see cref="WaveMixChannels.Rate"/>, fed by <see cref="WaveMixChannels.Mix"/> from a thread of its
/// own. While it is open it plays all the time, silence when nothing is queued, as WaveMix holds the
/// wave device from <c>WaveMixActivate(True)</c> until <c>WaveMixActivate(False)</c>. winmm is what
/// the port already uses for MIDI and what the original's WAVMIX16 itself sat on, so no library is
/// added.
///
/// <see cref="Buffers"/> buffers of <see cref="BufferFrames"/> samples (about 17 ms each) are kept with the
/// device, so a sound asked for starts within about 67 ms plus what Windows itself adds.
/// </summary>
internal sealed class WaveOutStream : IDisposable
{
    public const int BufferFrames = WaveMixChannels.Rate / 60;   // 367 samples, about 17 ms
    public const int Buffers = 4;

    private readonly WaveMixChannels _mixer;
    private readonly IntPtr _device;
    private readonly IntPtr[] _headers = new IntPtr[Buffers];
    private readonly IntPtr[] _data = new IntPtr[Buffers];
    private readonly AutoResetEvent _done;
    private readonly Thread _thread;
    private readonly short[] _mix = new short[BufferFrames];
    private volatile bool _stop;
    private bool _disposed;

    private WaveOutStream(WaveMixChannels mixer, IntPtr device, AutoResetEvent done)
    {
        _mixer = mixer;
        _device = device;
        _done = done;
        int headerSize = Marshal.SizeOf<WaveHeader>();
        for (int i = 0; i < Buffers; i++)
        {
            _data[i] = Marshal.AllocHGlobal(BufferFrames * 2);
            _headers[i] = Marshal.AllocHGlobal(headerSize);
            var h = new WaveHeader { lpData = _data[i], dwBufferLength = BufferFrames * 2 };
            Marshal.StructureToPtr(h, _headers[i], false);
            waveOutPrepareHeader(_device, _headers[i], headerSize);
        }
        _thread = new Thread(Run) { IsBackground = true, Name = "WaveMix output", Priority = ThreadPriority.AboveNormal };
    }

    /// <summary>The device opened and streaming, or null when Windows will not give it.</summary>
    public static WaveOutStream? Open(WaveMixChannels mixer)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var fmt = new WaveFormat
            {
                wFormatTag = 1, nChannels = 1, nSamplesPerSec = WaveMixChannels.Rate,
                nAvgBytesPerSec = WaveMixChannels.Rate * 2, nBlockAlign = 2, wBitsPerSample = 16, cbSize = 0,
            };
            var ev = new AutoResetEvent(false);
            int r = waveOutOpen(out IntPtr dev, WAVE_MAPPER, ref fmt, ev.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
            if (r != 0) { ev.Dispose(); return null; }
            var s = new WaveOutStream(mixer, dev, ev);
            s._thread.Start();
            return s;
        }
        catch { return null; }
    }

    private void Run()
    {
        int flagsAt = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.dwFlags));
        try
        {
            for (int i = 0; i < Buffers && !_stop; i++) Submit(i);
            int next = 0;
            while (!_stop)
            {
                if ((Marshal.ReadInt32(_headers[next], flagsAt) & WHDR_DONE) == 0)
                {
                    _done.WaitOne(10);
                    continue;
                }
                Submit(next);
                next = (next + 1) % Buffers;
            }
        }
        catch { }
    }

    private void Submit(int i)
    {
        _mixer.Mix(_mix);
        Marshal.Copy(_mix, 0, _data[i], BufferFrames);
        waveOutWrite(_device, _headers[i], Marshal.SizeOf<WaveHeader>());
    }

    /// <summary>
    /// <c>mdrDeInitializeFX</c> (MUSIC.BAS:268-289) closes the channels and the session; here the
    /// device is reset, its buffers given back and closed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop = true;
        _done.Set();
        try { _thread.Join(500); } catch { }
        try
        {
            waveOutReset(_device);
            int headerSize = Marshal.SizeOf<WaveHeader>();
            for (int i = 0; i < Buffers; i++) waveOutUnprepareHeader(_device, _headers[i], headerSize);
            waveOutClose(_device);
        }
        catch { }
        for (int i = 0; i < Buffers; i++)
        {
            Marshal.FreeHGlobal(_headers[i]);
            Marshal.FreeHGlobal(_data[i]);
        }
        _done.Dispose();
    }

    // ---- winmm ----
    private const int WAVE_MAPPER = -1;
    private const int CALLBACK_EVENT = 0x00050000;
    private const int WHDR_DONE = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesRecorded;
        public IntPtr dwUser;
        public int dwFlags;
        public int dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WaveFormat lpFormat, IntPtr dwCallback, IntPtr dwInstance, int dwFlags);
    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, int uSize);
    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hWaveOut);
    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);
}
