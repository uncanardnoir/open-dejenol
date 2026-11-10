using System.Diagnostics;
using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;

static class LegacyInstallerCheck
{
    public static int Run(string game)
    {
        Console.WriteLine("\nM1FULL.zip through first-run discovery and installer selection:");
        int failed = 0;
        void Check(string name, bool ok) { Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {name}"); if (!ok) failed++; }
        void Pump() { for (int i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); } }
        string scratch = Path.Combine(Path.GetTempPath(), "mordor-ui-zip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var env = new[] { GameFolder.EnvIni, GameFolder.EnvDir, InstallerUnpack.EnvRoot, "MORDOR_CRASHLOG" }
            .ToDictionary(k => k, Environment.GetEnvironmentVariable);
        bool limited = LimitedVersion.On;
        GameFolderWindow? gate = null;
        Window? menu = null;
        try
        {
            string? supplied = Environment.GetEnvironmentVariable("MORDOR_LEGACY_INSTALLER");
            bool useProvided = !string.IsNullOrWhiteSpace(supplied);
            string package = useProvided ? supplied! : Path.Combine(scratch, "M1FULL.zip");
            if (useProvided && !File.Exists(package)) throw new FileNotFoundException("The supplied legacy installer was not found.", package);
            if (!useProvided)
            {
                using var payload = new MemoryStream();
                using (var inner = new ZipArchive(payload, ZipArchiveMode.Create, leaveOpen: true))
                    foreach (string name in GameFiles.Required.Append("MG11.DLL").Where(n => File.Exists(Path.Combine(game, n))))
                    { using var file = inner.CreateEntry(name).Open(); file.Write(File.ReadAllBytes(Path.Combine(game, name))); }
                using var outer = ZipFile.Open(package, ZipArchiveMode.Create);
                using var entry = outer.CreateEntry("M1FULL/MORDOR.WIP").Open(); entry.Write(payload.ToArray());
            }
            foreach (bool offer in new[] { false, true })
            {
                string ini = Path.Combine(scratch, offer ? "offer.ini" : "picker.ini");
                Environment.SetEnvironmentVariable(GameFolder.EnvIni, ini);
                Environment.SetEnvironmentVariable(GameFolder.EnvDir, Path.Combine(scratch, "not-installed"));
                Environment.SetEnvironmentVariable(InstallerUnpack.EnvRoot, Path.Combine(scratch, offer ? "offer" : "picker"));
                Environment.SetEnvironmentVariable("MORDOR_CRASHLOG", Path.Combine(scratch, "crash.log"));
                string? accepted = null;
                gate = (GameFolderWindow)GameFolderWindow.FirstWindow(GameFolder.Resolve(), path =>
                { accepted = path; return menu = new Window { Width = 200, Height = 100 }; }, null,
                    offer ? new[] { package } : Array.Empty<string>());
                gate.PickInstaller = _ => Task.FromResult<string?>(package);
                gate.Show(); Pump();
                Check("first-run instructions name M1FULL.zip", gate.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("M1FULL.zip") == true));
                Check("nothing unpacked before the player chooses it", accepted == null && !Directory.Exists(InstallerUnpack.Root));
                (offer ? gate.Offers.Single().Button : gate.InstallerButton).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check("ZIP selection shows unpacking progress and disables duplicate requests", gate.Status.IsVisible && !gate.InstallerButton.IsEnabled);
                var watch = Stopwatch.StartNew();
                while (gate.Unpacking is { IsCompleted: false } && watch.ElapsedMilliseconds < 15000) { Pump(); Thread.Sleep(5); }
                if (gate.Unpacking is { IsCompleted: false }) throw new TimeoutException("ZIP first-run import did not finish.");
                gate.Unpacking?.GetAwaiter().GetResult(); Pump();
                Check((offer ? "Downloads offer" : "installer picker") + " opens the game with a validated full edition",
                    accepted != null && GameFiles.Check(accepted).IsFull && !LimitedVersion.On && menu?.IsVisible == true);
                Check("ZIP location is remembered and first-run window closes", !gate.IsVisible && GameFolder.Configured(ini) == accepted);
                menu?.Close(); gate.Close(); Pump();
            }
        }
        catch (Exception ex) { Check("ZIP first-run UI can be driven: " + ex, false); }
        finally
        {
            gate?.Close(); menu?.Close(); Pump();
            foreach (var (key, value) in env) Environment.SetEnvironmentVariable(key, value);
            LimitedVersion.On = limited;
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new InvalidOperationException("Unexpected ZIP UI cleanup path.");
            Directory.Delete(scratch, true);
        }
        return failed;
    }
}
