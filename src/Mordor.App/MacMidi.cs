using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Mordor.App;

/// <summary>
/// The music on macOS: a MIDI file through AudioToolbox's <c>MusicPlayer</c>, which plays a
/// sequence loaded from a file on Apple's built-in General MIDI synthesiser (its DLS sound set) - the
/// part winmm's MCI sequencer plays on Windows (<see cref="Audio.Music"/>). One track at a time, as
/// the MCI alias <c>bgm</c> is. AudioToolbox is part of macOS, so no library or SoundFont is added.
/// </summary>
internal static class MacMidi
{
    private static IntPtr _player, _sequence;
    /// <summary>The sequence's length in beats: the longest of its tracks.</summary>
    private static double _length;

    /// <summary>Load and start a MIDI file; false when AudioToolbox will not play it.</summary>
    public static bool Play(string path)
    {
        Stop();
        try
        {
            if (NewMusicSequence(out _sequence) != 0) { _sequence = IntPtr.Zero; return false; }
            byte[] bytes = Encoding.UTF8.GetBytes(path);
            IntPtr url = CFURLCreateFromFileSystemRepresentation(IntPtr.Zero, bytes, bytes.Length, false);
            if (url == IntPtr.Zero) { Stop(); return false; }
            int loaded;
            try { loaded = MusicSequenceFileLoad(_sequence, url, FileTypeMidi, 0); }
            finally { CFRelease(url); }
            if (loaded != 0) { Stop(); return false; }
            _length = Length(_sequence);
            if (NewMusicPlayer(out _player) != 0) { _player = IntPtr.Zero; Stop(); return false; }
            if (MusicPlayerSetSequence(_player, _sequence) != 0 || MusicPlayerPreroll(_player) != 0 || MusicPlayerStart(_player) != 0)
            {
                Stop();
                return false;
            }
            return true;
        }
        catch { Stop(); return false; }
    }

    /// <summary>
    /// The track has played to its end - MCI's <c>status bgm mode</c> "stopped". A MusicPlayer does
    /// not stop at the end of its sequence, its time just runs on past it.
    /// </summary>
    public static bool Finished
    {
        get
        {
            if (_player == IntPtr.Zero) return false;
            try { return MusicPlayerGetTime(_player, out double beats) == 0 && _length > 0 && beats >= _length; }
            catch { return false; }
        }
    }

    /// <summary>From the start again: MCI's <c>seek bgm to start</c> and <c>play bgm</c>.</summary>
    public static void Restart()
    {
        if (_player == IntPtr.Zero) return;
        try { MusicPlayerSetTime(_player, 0); MusicPlayerStart(_player); } catch { }
    }

    /// <summary>MCI's <c>stop bgm</c> and <c>close bgm</c>: the player and its sequence let go.</summary>
    public static void Stop()
    {
        try
        {
            if (_player != IntPtr.Zero)
            {
                MusicPlayerStop(_player);
                DisposeMusicPlayer(_player);
            }
            if (_sequence != IntPtr.Zero) DisposeMusicSequence(_sequence);
        }
        catch { }
        _player = _sequence = IntPtr.Zero;
        _length = 0;
    }

    private static double Length(IntPtr sequence)
    {
        double longest = 0;
        if (MusicSequenceGetTrackCount(sequence, out uint count) != 0) return 0;
        for (uint i = 0; i < count; i++)
        {
            if (MusicSequenceGetIndTrack(sequence, i, out IntPtr track) != 0) continue;
            double beats = 0;
            uint size = sizeof(double);
            if (MusicTrackGetProperty(track, TrackPropertyLength, ref beats, ref size) == 0)
                longest = Math.Max(longest, beats);
        }
        return longest;
    }

    /// <summary>The system's alert sound, for VB's <c>Beep</c> (<see cref="Audio.Beep"/>).</summary>
    public static void Beep()
    {
        try { AudioServicesPlayAlertSound(UserPreferredAlert); } catch { }
    }

    // ---- AudioToolbox and CoreFoundation ----
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint FileTypeMidi = 0x6D696469;        // kMusicSequenceFile_MIDIType, 'midi'
    private const uint TrackPropertyLength = 5;          // kSequenceTrackProperty_TrackLength
    private const uint UserPreferredAlert = 0x00001000;  // kSystemSoundID_UserPreferredAlert

    [DllImport(AudioToolbox)] private static extern int NewMusicSequence(out IntPtr sequence);
    [DllImport(AudioToolbox)] private static extern int DisposeMusicSequence(IntPtr sequence);
    [DllImport(AudioToolbox)] private static extern int MusicSequenceFileLoad(IntPtr sequence, IntPtr url, uint fileTypeHint, uint flags);
    [DllImport(AudioToolbox)] private static extern int MusicSequenceGetTrackCount(IntPtr sequence, out uint count);
    [DllImport(AudioToolbox)] private static extern int MusicSequenceGetIndTrack(IntPtr sequence, uint index, out IntPtr track);
    [DllImport(AudioToolbox)] private static extern int MusicTrackGetProperty(IntPtr track, uint property, ref double data, ref uint length);
    [DllImport(AudioToolbox)] private static extern int NewMusicPlayer(out IntPtr player);
    [DllImport(AudioToolbox)] private static extern int DisposeMusicPlayer(IntPtr player);
    [DllImport(AudioToolbox)] private static extern int MusicPlayerSetSequence(IntPtr player, IntPtr sequence);
    [DllImport(AudioToolbox)] private static extern int MusicPlayerPreroll(IntPtr player);
    [DllImport(AudioToolbox)] private static extern int MusicPlayerStart(IntPtr player);
    [DllImport(AudioToolbox)] private static extern int MusicPlayerStop(IntPtr player);
    [DllImport(AudioToolbox)] private static extern int MusicPlayerGetTime(IntPtr player, out double beats);
    [DllImport(AudioToolbox)] private static extern int MusicPlayerSetTime(IntPtr player, double beats);
    [DllImport(AudioToolbox)] private static extern void AudioServicesPlayAlertSound(uint soundId);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFURLCreateFromFileSystemRepresentation(IntPtr allocator, byte[] buffer, nint length, [MarshalAs(UnmanagedType.U1)] bool isDirectory);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr cf);
}
