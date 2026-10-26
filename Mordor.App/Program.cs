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
        // %APPDATA%\Mordor Port and %LOCALAPPDATA%\Mordor Port, from before the program had its
        // name, become OpenDejenol (Mordor.Core.UserFolders), before anything reads or writes there.
        // Not on a test run (MORDOR_TEST), which keeps to scratch folders and leaves the player's alone.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MORDOR_TEST"))) Mordor.Core.UserFolders.MigrateAll();
        CrashLog.Install();
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { CrashLog.Write("fatal", ex); throw; }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            
#endif
            .WithInterFont()
            .LogToTrace();
}
