using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Velopack;
using Velopack.Sources;

namespace Mordor.App;

/// <summary>
/// The port's own updates (docs/RELEASING.md), nothing of the original's. A copy installed by the
/// OpenDejenol installer (Velopack) asks the project's GitHub releases once, at start and in the
/// background, whether there is a newer version; if there is, the main menu offers it once that
/// run (<see cref="Offer"/>), and "Update now" downloads it, applies it and starts the new version.
/// Nothing is asked of the network and nothing is shown when:
/// <list type="bullet">
/// <item>the copy was not installed by the installer (a build folder, <c>dotnet run</c>): Velopack's
///   <c>UpdateManager.IsInstalled</c> is false;</item>
/// <item>it is a test run (<c>MORDOR_TEST</c>);</item>
/// <item>the launch settings say <c>[Updates] Check=False</c> (<see cref="Mordor.Core.UpdateSettings"/>,
///   which also says whether test releases are offered).</item>
/// </list>
/// No network, a GitHub that does not answer, or anything else that goes wrong in the check is
/// silent: the game starts and plays exactly as it would have. An update replaces only the
/// installed program; the saves, the settings (<c>%APPDATA%\OpenDejenol</c>), the picture cache
/// (<c>%LOCALAPPDATA%\OpenDejenol\cache</c>), the game folder and the mods beside it are not in the
/// install folder (<c>%LOCALAPPDATA%\OpenDejenol.Game</c>) and are never touched.
/// </summary>
internal static class Updates
{
    /// <summary>Where the releases are published.</summary>
    public const string RepoUrl = "https://github.com/uncanardnoir/open-dejenol";

    /// <summary>This program's version: the Version in Mordor.App.csproj, without any "+commit" the build adds.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        string v = typeof(Updates).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? typeof(Updates).Assembly.GetName().Version?.ToString(3) ?? "";
        int plus = v.IndexOf('+');
        return plus >= 0 ? v[..plus] : v;
    }

    private static UpdateManager? _manager;
    private static UpdateInfo? _found;
    private static bool _offered, _begun;

    /// <summary>Raised on the UI thread when the check has found an update.</summary>
    public static event Action? Found;

    /// <summary>An update was found and has not been offered yet this run.</summary>
    public static bool Pending => _found != null && !_offered;

    /// <summary>The check, started once, in the background. Never throws and never waits.</summary>
    public static void Begin()
    {
        if (_begun) return;
        _begun = true;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MORDOR_TEST"))) return;
        _ = Task.Run(async () =>
        {
            try
            {
                if (!Mordor.Core.UpdateSettings.CheckEnabled()) return;
                var stable = new UpdateManager(new GithubSource(RepoUrl, "", false));
                if (!stable.IsInstalled) return;
                bool pre = Mordor.Core.UpdateSettings.Prereleases(stable.CurrentVersion?.ToString());
                var manager = pre ? new UpdateManager(new GithubSource(RepoUrl, "", true)) : stable;
                var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (info == null || info.IsDowngrade) return;
                Dispatcher.UIThread.Post(() =>
                {
                    _manager = manager;
                    _found = info;
                    Found?.Invoke();
                });
            }
            catch
            {
                // offline, rate-limited, a release without the files: say nothing, play on
            }
        });
    }

    /// <summary>
    /// The offer, over the main menu: "A new version x.y.z is available", Update now or Later. Made
    /// once a run; Later leaves it until the next start. Update now downloads the new version with
    /// its progress in a box, then the program closes, the update is applied and the new version
    /// starts. A download that fails says so and the game goes on as it is.
    /// </summary>
    public static async Task Offer(Window owner)
    {
        if (!Pending || _manager is not UpdateManager manager || _found is not UpdateInfo info) return;
        _offered = true;
        string version = info.TargetFullRelease.Version.ToString();
        bool update = await OriginalBox.Ask(owner, "OpenDejenol",
            $"A new version {version} is available (this is {Version}).\n\n"
            + "Updating closes the game and starts the new version. Your characters, settings and game folder are kept.",
            "Update now", "Later");
        if (!update || !OriginalBox.CanOwn(owner)) return;

        var line = new TextBlock { Text = $"Downloading version {version}...", Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
        var box = new Window
        {
            Title = "OpenDejenol", Width = 420, SizeToContent = SizeToContent.Height, CanResize = false,
            Background = Brush.Parse("#101018"), WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false, Content = new Border { Padding = new Thickness(14), Child = line },
        };
        UiFonts.MessageBox(box);
        bool done = false;
        // the close box does nothing while the download runs; the program or the session ending still closes it
        box.Closing += (_, e) => { if (!done && e.CloseReason == WindowCloseReason.WindowClosing) e.Cancel = true; };
        Task shown = OriginalBox.ShowOver(box, owner);
        try
        {
            await manager.DownloadUpdatesAsync(info, percent =>
                Dispatcher.UIThread.Post(() => line.Text = $"Downloading version {version}... {percent}%"));
        }
        catch (Exception ex)
        {
            done = true;
            box.Close();
            await shown;
            CrashLog.Note("the update to " + version + " could not be downloaded: " + ex.Message);
            if (OriginalBox.CanOwn(owner))
                await OriginalBox.Tell(owner, "OpenDejenol",
                    $"The update to {version} could not be downloaded:\n{ex.Message}\n\nThe game goes on as it is; it will be offered again at the next start.");
            return;
        }
        line.Text = $"Starting version {version}...";
        // what the menu's Exit does on the way out, then Velopack ends this process, applies the
        // update and starts the new version
        HelpWindow.CloseAll();
        Audio.StopMusic();
        manager.ApplyUpdatesAndRestart(info.TargetFullRelease);
    }
}
