using System;
using System.Collections.Generic;
using System.Text;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// Somewhere for a crash to go. Without this an unhandled exception on the UI thread takes the
/// whole process with it and the window simply vanishes - which from the other side of the screen
/// looks exactly like "the game just closed", with nothing to go on afterwards.
///
/// Every exception that reaches here is written to <c>crash.log</c> beside the saves folder in use
/// (for <c>saves\</c> beside the game folder, that is beside the game folder), or in the per-user
/// folder when that cannot be written (<see cref="PortFiles"/>), with the time and what the player
/// was doing, and appended rather than overwritten so a run of them can be compared.
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();

    /// <summary>What the player was last doing, so the log says more than the stack does.</summary>
    public static string Context { get; set; } = "starting up";

    /// <summary>Where the log may go, best first (<see cref="PortFiles.CrashLogPlaces(string)"/>).</summary>
    private static List<string> Places
    {
        get
        {
            // MORDOR_CRASHLOG: a file of its own, for the start-up smoke test in tools/check.sh,
            // which may run while the game is being played and must neither write to the real
            // log nor read the player's lines in it as its own
            string? own = Environment.GetEnvironmentVariable("MORDOR_CRASHLOG");
            if (!string.IsNullOrWhiteSpace(own)) return new List<string> { own };
            // beside the saves folder in use, never inside it - for saves\ beside the game folder
            // that is beside the game folder, where it always was - else the per-user folder
            try { return PortFiles.CrashLogPlaces(TitleWindow.GameDir); }
            catch { return PortFiles.CrashLogPlaces(null, PortFiles.PerUserRoot); }
        }
    }

    public static void Write(string where, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(new string('-', 78));
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {where}");
            sb.AppendLine($"doing: {Context}");
            sb.AppendLine(ex.ToString());
            lock (Gate) PortFiles.Append(Places, sb.ToString());
        }
        catch { /* a crash logger that throws is worse than none */ }
    }

    /// <summary>A plain line in the same log - for things worth knowing that are not exceptions.</summary>
    public static void Note(string line)
    {
        try
        {
            lock (Gate) PortFiles.Append(Places, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}{Environment.NewLine}");
        }
        catch { }
    }


    /// <summary>Hook everything that can carry an exception out of the program.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Write("unhandled on " + AppDomain.CurrentDomain.FriendlyName, ex);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }
}
