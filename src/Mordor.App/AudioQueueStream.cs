using System;
using System.Runtime.InteropServices;
using System.Threading;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The output end of the port's WaveMix on macOS: an AudioToolbox <c>AudioQueue</c>, 16-bit mono at
/// <see cref="WaveMixChannels.Rate"/>, fed by <see cref="WaveMixChannels.Mix"/> - the same stream
/// <see cref="WaveOutStream"/> gives winmm on Windows, with its buffer sizes, so a sound starts as
/// soon after it is asked for. The queue calls back on a thread of its own each time a buffer has
/// played, and the buffer is mixed and queued again there. AudioToolbox is part of macOS, so no
/// library is added.
/// </summary>
internal sealed class AudioQueueStream : IDisposable
{
    private readonly WaveMixChannels _mixer;
    private readonly IntPtr _queue;
    private readonly IntPtr[] _buffers = new IntPtr[WaveOutStream.Buffers];
    private readonly short[] _mix = new short[WaveOutStream.BufferFrames];
    private GCHandle _self;   // not readonly: Free() must change this field, not a copy of it
    private volatile bool _stop;
    private int _disposed;     // 0 or 1, by Interlocked: Shutdown (ProcessExit) and a minimise may race

    /// <summary>
    /// The queue refused a buffer - the output device went away, or coreaudiod restarted - so no
    /// more callbacks will come and the effects are silent. <see cref="Audio"/> opens a new stream
    /// at the next effect.
    /// </summary>
    public bool Broken { get; private set; }

    /// <summary>The callback, kept alive for as long as any queue may call it.</summary>
    private static readonly OutputCallback Callback = OnBufferDone;
    private static readonly IntPtr CallbackPtr = Marshal.GetFunctionPointerForDelegate(Callback);

    private AudioQueueStream(WaveMixChannels mixer, IntPtr queue, GCHandle self)
    {
        _mixer = mixer;
        _queue = queue;
        _self = self;
    }

    /// <summary>The queue made, primed and started, or null when macOS will not give one.</summary>
    public static AudioQueueStream? Open(WaveMixChannels mixer)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        // the stream's handle is the queue's user data, so it is made before the queue: the callback
        // only reaches the stream through it, and nothing is called back until AudioQueueStart
        var holder = new Holder();
        var self = GCHandle.Alloc(holder);
        try
        {
            var fmt = new StreamDescription
            {
                SampleRate = WaveMixChannels.Rate, FormatID = FormatLinearPCM,
                FormatFlags = FlagSignedInteger | FlagPacked, BytesPerPacket = 2, FramesPerPacket = 1,
                BytesPerFrame = 2, ChannelsPerFrame = 1, BitsPerChannel = 16,
            };
            if (AudioQueueNewOutput(ref fmt, CallbackPtr, GCHandle.ToIntPtr(self), IntPtr.Zero, IntPtr.Zero, 0, out IntPtr queue) != 0)
            {
                self.Free();
                return null;
            }
            var s = new AudioQueueStream(mixer, queue, self);
            holder.Stream = s;
            for (int i = 0; i < s._buffers.Length; i++)
            {
                if (AudioQueueAllocateBuffer(queue, WaveOutStream.BufferFrames * 2, out s._buffers[i]) != 0) { s.Dispose(); return null; }
                s.Fill(s._buffers[i]);
            }
            if (AudioQueueStart(queue, IntPtr.Zero) != 0) { s.Dispose(); return null; }
            return s;
        }
        catch
        {
            if (holder.Stream != null) holder.Stream.Dispose();
            else if (self.IsAllocated) self.Free();
            return null;
        }
    }

    /// <summary>The stream, set once it is made: the queue's user data points here.</summary>
    private sealed class Holder { public AudioQueueStream? Stream; }

    private static void OnBufferDone(IntPtr userData, IntPtr queue, IntPtr buffer)
    {
        try
        {
            if (GCHandle.FromIntPtr(userData).Target is Holder { Stream: { _stop: false } s }) s.Fill(buffer);
        }
        catch { }
    }

    /// <summary>Mix the next <see cref="WaveOutStream.BufferFrames"/> samples into a buffer and queue it.</summary>
    private void Fill(IntPtr buffer)
    {
        _mixer.Mix(_mix);
        // never more than the buffer holds, whatever BufferFrames becomes
        int samples = Math.Min(_mix.Length, Marshal.ReadInt32(buffer, BufferCapacityAt) / 2);
        Marshal.Copy(_mix, 0, Marshal.ReadIntPtr(buffer, BufferAudioDataAt), samples);
        Marshal.WriteInt32(buffer, BufferByteSizeAt, samples * 2);
        if (AudioQueueEnqueueBuffer(_queue, buffer, 0, IntPtr.Zero) != 0) Broken = true;
    }

    /// <summary>
    /// <c>mdrDeInitializeFX</c> (MUSIC.BAS:268-289) gives the wave device back: the queue is stopped
    /// at once and disposed, which frees its buffers.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop = true;
        if (_self.IsAllocated && _self.Target is Holder h) h.Stream = null;   // a late callback finds no stream
        bool gone;
        try
        {
            AudioQueueStop(_queue, true);
            gone = AudioQueueDispose(_queue, true) == 0;
        }
        catch { gone = false; }
        // only once the queue is gone can nothing call back with the handle; if it would not go, the
        // handle is kept (a few bytes) rather than freed under a queue that may still use it
        if (gone && _self.IsAllocated) _self.Free();
    }

    // ---- AudioToolbox ----
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const uint FormatLinearPCM = 0x6C70636D;   // 'lpcm'
    private const uint FlagSignedInteger = 0x4, FlagPacked = 0x8;
    // AudioQueueBuffer: UInt32 mAudioDataBytesCapacity, then (aligned) void *mAudioData, UInt32 mAudioDataByteSize
    private const int BufferCapacityAt = 0, BufferAudioDataAt = 8, BufferByteSizeAt = 16;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OutputCallback(IntPtr userData, IntPtr queue, IntPtr buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct StreamDescription
    {
        public double SampleRate;
        public uint FormatID;
        public uint FormatFlags;
        public uint BytesPerPacket;
        public uint FramesPerPacket;
        public uint BytesPerFrame;
        public uint ChannelsPerFrame;
        public uint BitsPerChannel;
        public uint Reserved;
    }

    [DllImport(AudioToolbox)]
    private static extern int AudioQueueNewOutput(ref StreamDescription format, IntPtr callback, IntPtr userData, IntPtr runLoop, IntPtr runLoopMode, uint flags, out IntPtr queue);
    [DllImport(AudioToolbox)]
    private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint byteSize, out IntPtr buffer);
    [DllImport(AudioToolbox)]
    private static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint packetDescriptions, IntPtr descriptions);
    [DllImport(AudioToolbox)]
    private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);
    [DllImport(AudioToolbox)]
    private static extern int AudioQueueStop(IntPtr queue, [MarshalAs(UnmanagedType.U1)] bool immediate);
    [DllImport(AudioToolbox)]
    private static extern int AudioQueueDispose(IntPtr queue, [MarshalAs(UnmanagedType.U1)] bool immediate);
}
