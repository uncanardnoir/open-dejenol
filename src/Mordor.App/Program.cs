using Avalonia;
using System;

namespace Mordor.App;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack's install, update and uninstall hooks (docs/RELEASING.md): first of all, as it
        // asks. Started by the installer or the updater with one of its own arguments, it does what
        // that asks and ends the process here; started any other way - by the player, or from a
        // build folder, where it is not installed - it does nothing and the game starts.
        Exception? velopack = null;
        try { Velopack.VelopackApp.Build().Run(); }
        catch (Exception ex) { velopack = ex; }
        // %APPDATA%\Mordor Port and %LOCALAPPDATA%\Mordor Port, from before the program had its
        // name, become OpenDejenol (Mordor.Core.UserFolders), before anything reads or writes there.
        // Not on a test run (MORDOR_TEST), which keeps to scratch folders and leaves the player's alone.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MORDOR_TEST"))) Mordor.Core.UserFolders.MigrateAll();
        CrashLog.Install();
        // a save that could not be written, or a character file that would not read, is said in crash.log
        Mordor.Core.SafeFile.Log = CrashLog.Note;
        Mordor.Core.SaveRecovery.Log = CrashLog.Note;
        if (velopack != null) CrashLog.Write("the installer's start-up hooks", velopack);
        // Since .NET 10 the runtime no longer ends a program gracefully on SIGTERM (a Linux or macOS
        // logout, a kill): it would stop dead, without ProcessExit. This puts back what .NET 8 did,
        // an exit through ProcessExit, so what waits there is still done (the DPS Meter's last
        // tally written, the sound closed). Windows is left exactly as it was.
        using var sigterm = OperatingSystem.IsWindows() ? null
            : System.Runtime.InteropServices.PosixSignalRegistration.Create(
                System.Runtime.InteropServices.PosixSignal.SIGTERM, _ => Environment.Exit(0));
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { CrashLog.Write("fatal", ex); throw; }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
        // the faces where the system's are missing or Avalonia's fallback would differ (PortFonts.ManagerOptions):
        // Liberation Sans away from Windows, Segoe UI for a missing geometric shape on Windows
        if (PortFonts.ManagerOptions() is { } fonts) builder = builder.With(fonts);
        return builder;
    }
}
