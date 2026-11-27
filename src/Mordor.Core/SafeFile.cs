using System.Text;

namespace Mordor.Core;

/// <summary>
/// How every save file is written, so that a game that dies part way through a save - a crash, the
/// process killed, the power cut - keeps the file it had, whole, instead of an empty or half-written
/// one. A port necessity: the original Puts records into its MDATA files in place.
///
/// The text goes to <c>&lt;file&gt;.tmp</c> beside the file, is forced to disk (so a power cut cannot
/// leave the rename done and the contents not), and is then renamed over the file. A rename on one
/// volume is all or nothing on NTFS and on Linux file systems: the file is the old one or the new one,
/// never a mixture. Something else holding the file for a moment - a virus scanner, a sync client -
/// gets a few retries before the save gives up; a save that gives up says so in crash.log
/// (<see cref="Log"/>) and leaves the old file and the .tmp as they are.
/// </summary>
public static class SafeFile
{
    /// <summary>Where a save that could not be written is reported: crash.log, set by the program.</summary>
    public static Action<string>? Log { get; set; }

    public const string TempSuffix = ".tmp";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>File.WriteAllText's job - UTF-8, no byte-order mark - done safely.</summary>
    public static void WriteAllText(string path, string text) => WriteAllBytes(path, Utf8.GetBytes(text));

    /// <summary>A copy of <paramref name="from"/> at <paramref name="to"/>, written safely.</summary>
    public static void Copy(string from, string to) => WriteAllBytes(to, File.ReadAllBytes(from));

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
            string temp = path + TempSuffix;
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite: true);
                    SaveJournal.Wrote(path, bytes);   // the port's save journal: never throws
                    return;
                }
                catch (Exception e) when ((e is IOException or UnauthorizedAccessException) && attempt < 5)
                {
                    Thread.Sleep(40 * attempt);
                }
            }
        }
        catch (Exception e)
        {
            Log?.Invoke($"{path} could not be saved: {e.GetType().Name}: {e.Message}");
            SaveJournal.WriteFailed(path, e);
            throw;
        }
    }
}
