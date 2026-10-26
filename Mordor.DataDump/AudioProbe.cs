using System.Runtime.InteropServices;

/// <summary>Checks every WAV/MID asset is loadable by the Windows multimedia API — without playing anything.</summary>
public static class AudioProbe
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, System.Text.StringBuilder? ret, int retLen, IntPtr callback);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciGetErrorString(int err, System.Text.StringBuilder buf, int len);

    public static void Run(string dir)
    {
        int ok = 0, bad = 0;
        foreach (var f in Directory.GetFiles(dir, "*.MID").OrderBy(f => f))
        {
            var sb = new System.Text.StringBuilder(128);
            int e = mciSendString($"open \"{f}\" type sequencer alias probe", null, 0, IntPtr.Zero);
            if (e == 0)
            {
                mciSendString("set probe time format milliseconds", null, 0, IntPtr.Zero);
                mciSendString("status probe length", sb, sb.Capacity, IntPtr.Zero);
                mciSendString("close probe", null, 0, IntPtr.Zero);
                Console.WriteLine($"MID ok   {Path.GetFileName(f),-14} {int.Parse(sb.ToString()) / 1000.0,6:0.0}s");
                ok++;
            }
            else
            {
                var es = new System.Text.StringBuilder(256); mciGetErrorString(e, es, es.Capacity);
                Console.WriteLine($"MID FAIL {Path.GetFileName(f),-14} {es}"); bad++;
            }
        }
        foreach (var f in Directory.GetFiles(dir, "*.WAV").OrderBy(f => f))
        {
            using var s = File.OpenRead(f); var h = new byte[12]; s.Read(h, 0, 12);
            bool riff = System.Text.Encoding.ASCII.GetString(h, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(h, 8, 4) == "WAVE";
            if (riff) ok++; else { bad++; Console.WriteLine($"WAV FAIL {Path.GetFileName(f)}"); }
        }
        Console.WriteLine($"{ok} assets ok, {bad} bad");
    }
}
