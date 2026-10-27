using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// Shown at start instead of the main menu when the files the port needs were not found, or do not
/// read (see <see cref="GameFolder"/>, docs/GAME_FILES.md). The port is an engine for the player's
/// own copy of the game and has no data of its own: it says so, says what was wrong with what it
/// found, and lets the player give it their Mordor - the installer bought from Decklin's Domain
/// (MordorSetup.exe, or the free demo MordorDemoSetup.exe), which the port unpacks itself
/// (<see cref="InstallerUnpack"/>; the installers do not run on Windows 10 or 11), or the folder of
/// a Mordor installed some other way. An installer found in the player's Downloads folder is
/// offered, never unpacked unasked. A Mordor to play, full game or shareware, is remembered in the
/// launch settings and the game goes on to the menu - the shareware's as the limited build plays
/// (<see cref="LimitedVersion"/>).
///
/// The port's own, with no counterpart in the original, which found its data through BasePath in
/// MORDOR.INI and said only "Is BasePath in MORDOR.INI correct?" when it could not
/// (MAINMOD.BAS:1193-1196).
/// </summary>
public sealed class GameFolderWindow : Window
{
    /// <summary>Asks the player for a folder; null when they cancel. Replaceable, for the headless check.</summary>
    public Func<Window, Task<string?>> PickFolder { get; set; } = AskForFolder;

    /// <summary>Asks the player for an installer; null when they cancel. Replaceable, for the headless check.</summary>
    public Func<Window, Task<string?>> PickInstaller { get; set; } = AskForInstaller;

    /// <summary>Called with a playable Mordor's folder once one has been picked (and remembered).</summary>
    public Action<string>? Accepted { get; set; }

    /// <summary>
    /// Gets a picked folder ready before it is handed over - the game's pictures and help read out
    /// of it (<see cref="Art.PrepareAsync"/>) - while this window says so, its buttons off: null
    /// when ready, else what went wrong, shown here. None: handed over at once.
    /// </summary>
    public Func<string, Task<string?>>? Prepare { get; set; }

    /// <summary>"Preparing the game files ..." while a folder picked is got ready.</summary>
    public const string PreparingText = "Preparing the game files ...";

    /// <summary>The line that says what is wrong with the folder found or picked, or the installer chosen.</summary>
    public TextBlock Problem { get; }

    /// <summary>"Unpacking MordorSetup.exe ..." while an installer is read; empty otherwise.</summary>
    public TextBlock Status { get; }

    public Button InstallerButton { get; }
    public Button LocateButton { get; }
    public Button ExitButton { get; }

    /// <summary>One "Unpack it" for each installer found in Downloads, with the installer it unpacks.</summary>
    public IReadOnlyList<(string Installer, Button Button)> Offers { get; }

    /// <summary>The unpacking under way, if one is; for the headless check to wait on.</summary>
    public Task? Unpacking { get; private set; }

    /// <param name="found">What the search for a game folder made of this machine.</param>
    /// <param name="downloads">The installers in Downloads to offer (<see cref="InstallerUnpack.InDownloads"/> when null).</param>
    public GameFolderWindow(GameFolderResolution found, IReadOnlyList<string>? downloads = null)
    {
        Title = "OpenDejenol " + Updates.Version;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        // the theme's own window and text colours, as every other window: a white background here once hid the
        // theme's buttons, drawn light on light

        var heading = new TextBlock
        {
            Text = "Mordor game files not found",
            FontSize = 18, FontWeight = FontWeight.Bold,
        };
        var explain = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 13,
            Text = "OpenDejenol plays Mordor: The Depths of Dejenol (MakeItSo Software, 1995) from your own copy of "
                 + "the game. It contains none of the game's data, so it needs Mordor's own files - MORDOR.EXE and "
                 + "the MDATA files.\n\n"
                 + "The shareware (\"PUBLIC Limited Version\") plays as the shareware did, with its "
                 + GameFiles.SharewareLevels + " dungeon levels; the full version has all " + GameFiles.FullLevels + ".\n\n"
                 + "Decklin's Domain sells Mordor as MordorSetup.exe, and gives the demo away as MordorDemoSetup.exe. "
                 + "Those installers do not run on Windows 10 or 11, and there is no need to: choose the installer "
                 + "and OpenDejenol unpacks the game from it. A Mordor already installed (from the original CD-ROM "
                 + "or disks, or the shareware) can be chosen by its folder instead. The choice is remembered.",
        };
        Problem = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Salmon, FontSize = 13,
            Text = Describe(found.Nearest, found.Explicit),
        };
        Status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13, IsVisible = false };

        var offers = new List<(string, Button)>();
        var offerRows = new StackPanel { Spacing = 6 };
        foreach (string installer in downloads ?? InstallerUnpack.InDownloads())
        {
            var unpack = new Button { Content = "_Unpack it", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            unpack.Click += async (_, _) => await Unpack(installer);
            offers.Add((installer, unpack));
            offerRows.Children.Add(new DockPanel
            {
                Children =
                {
                    new Border { [DockPanel.DockProperty] = Dock.Right, Margin = new Thickness(10, 0, 0, 0), Child = unpack },
                    new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap, FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                        Text = "Found " + Path.GetFileName(installer) + " in Downloads.",
                    },
                },
            });
        }
        Offers = offers;

        InstallerButton = new Button { Content = "_Use the installer...", MinWidth = 130, HorizontalContentAlignment = HorizontalAlignment.Center };
        LocateButton = new Button { Content = "_Locate Mordor...", MinWidth = 130, HorizontalContentAlignment = HorizontalAlignment.Center };
        ExitButton = new Button { Content = "E_xit", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        InstallerButton.Click += async (_, _) => await UseInstaller();
        LocateButton.Click += async (_, _) => await Locate();
        ExitButton.Click += (_, _) => Close();

        var panel = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 12,
            Children = { heading, explain, Problem },
        };
        if (offers.Count > 0) panel.Children.Add(offerRows);
        panel.Children.Add(Status);
        panel.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
            Children = { InstallerButton, LocateButton, ExitButton },
        });
        Content = panel;
        Opened += (_, _) => (offers.Count > 0 ? offers[0].Item2 : InstallerButton).Focus();
        Closed += (_, _) => _closed = true;
    }

    /// <summary>The window has been closed (Exit, or its close box): nothing is handed over after that.</summary>
    private bool _closed;

    /// <summary>
    /// <paramref name="status"/> shown and every button but Exit off while something is under way;
    /// null puts them back.
    /// </summary>
    private void Busy(string? status)
    {
        foreach (var b in Offers.Select(o => o.Button).Concat(new[] { InstallerButton, LocateButton })) b.IsEnabled = status == null;
        Status.Text = status ?? "";
        Status.IsVisible = status != null;
    }

    /// <summary>What is wrong, in a line, or nothing when nothing was found at all.</summary>
    private static string Describe(GameFilesCheck? check, bool chosen)
    {
        if (check == null) return "No Mordor folder was found on this computer.";
        string where = chosen ? "The folder chosen, " : "Found ";
        return where + check.Dir + ": " + check.Reason;
    }

    /// <summary>Ask for a folder, check it, and go on if it can be played.</summary>
    public async Task Locate()
    {
        string? picked = await PickFolder(this);
        if (string.IsNullOrWhiteSpace(picked)) return;
        var check = GameFiles.Check(picked);
        // the folder above it, picked by mistake: its game_files, as the port's own layout has it
        if (check.Verdict == GameFilesVerdict.Missing)
        {
            var inside = GameFiles.Check(Path.Combine(picked, "game_files"));
            if (inside.IsPlayable) check = inside;
        }
        if (!check.IsPlayable)
        {
            Problem.Text = Describe(check, chosen: true);
            return;
        }
        await Accept(check, "game folder chosen");
    }

    /// <summary>Ask for an installer and unpack it.</summary>
    public async Task UseInstaller()
    {
        string? picked = await PickInstaller(this);
        if (string.IsNullOrWhiteSpace(picked)) return;
        await Unpack(picked);
    }

    /// <summary>
    /// Unpack <paramref name="installer"/> into the port's own folder (<see cref="InstallerUnpack.Root"/>),
    /// off the window's thread, saying so meanwhile; then go on as with a folder picked, or say why not.
    /// </summary>
    public Task Unpack(string installer)
    {
        if (Unpacking is { IsCompleted: false }) return Unpacking;
        return Unpacking = UnpackNow(installer);
    }

    private async Task UnpackNow(string installer)
    {
        Problem.Text = "";
        Busy("Unpacking " + Path.GetFileName(installer) + " ...");
        bool handedOver = false;
        try
        {
            var result = await Task.Run(() => InstallerUnpack.Unpack(installer));
            // the player sees the plain message; what was wrong with the file goes to the crash log
            CrashLog.Note("installer " + installer + ": " + result.Message + (result.Detail is string why ? " (" + why + ")" : ""));
            if (!result.Ok || result.Check is not { IsPlayable: true } check)
            {
                Problem.Text = result.Message;
                return;
            }
            // the buttons stay off and a line stays up until the game's window is shown
            handedOver = await Accept(check, "unpacked from " + installer + " into");
        }
        finally { if (!handedOver) Busy(null); }
    }

    /// <summary>
    /// A Mordor to play: got ready (<see cref="Prepare"/>, saying so meanwhile), remembered, the
    /// limited build set for it, and handed over. False, with the reason shown, when it could not be
    /// got ready, or when this window was closed meanwhile.
    /// </summary>
    private async Task<bool> Accept(GameFilesCheck check, string how)
    {
        if (Prepare != null)
        {
            Problem.Text = "";
            Busy(PreparingText);
            string? error;
            try { error = await Prepare(check.Dir); }
            catch (Exception ex) { CrashLog.Write("preparing " + check.Dir, ex); error = "The game files could not be prepared: " + ex.Message; }
            if (_closed) return false;
            if (error != null)
            {
                Busy(null);
                Problem.Text = error;
                return false;
            }
        }
        bool kept = GameFolder.Remember(check.Dir);
        LimitedVersion.Apply(check);
        Accepted?.Invoke(check.Dir);
        // after the hand-over, so the note goes to the crash log beside the folder now in use
        CrashLog.Note(how + ": " + check.Dir + " (" + check.Recognised + ")"
                      + (kept ? "" : ", but it could not be kept in " + GameFolder.LaunchIniPath));
        return true;
    }

    private static async Task<string?> AskForFolder(Window owner)
    {
        var picked = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Locate Mordor - choose the folder with MORDOR.EXE in it",
            AllowMultiple = false,
        });
        return picked.FirstOrDefault()?.TryGetLocalPath();
    }

    private static async Task<string?> AskForInstaller(Window owner)
    {
        IStorageFolder? start = null;
        try
        {
            if (InstallerUnpack.DownloadsFolder() is string dl && Directory.Exists(dl))
                start = await owner.StorageProvider.TryGetFolderFromPathAsync(dl);
        }
        catch { }
        var picked = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Use the installer - choose MordorSetup.exe or MordorDemoSetup.exe",
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Mordor installer (MordorSetup.exe, MordorDemoSetup.exe)") { Patterns = new[] { "*.exe" } },
            },
        });
        return picked.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>
    /// The start: <paramref name="start"/>'s window straight away when a Mordor to play was found
    /// (the limited build's behaviour switched on for the shareware's data), otherwise this window,
    /// which hands over to <paramref name="start"/>'s window (and <paramref name="becomeMain"/>)
    /// once one is picked. <paramref name="start"/> is never called without a playable folder.
    /// </summary>
    public static Window FirstWindow(GameFolderResolution found, Func<string, Window> start, Action<Window>? becomeMain = null, IReadOnlyList<string>? downloads = null,
                                     Func<string, Task<string?>>? prepare = null)
    {
        if (found.IsPlayable)
        {
            LimitedVersion.Apply(found.Found);
            if (found.Found!.IsLimited)
                CrashLog.Note("the shareware's data: playing as the limited build - " + found.Found.Dir
                              + " (its About date " + LimitedVersion.LastEdited + ", its order price " + LimitedVersion.Price + ")");
            return start(found.Found.Dir);
        }
        CrashLog.Note("no Mordor to play: " + (found.Nearest?.Dir ?? "nothing found") + " - " + (found.Nearest?.Reason ?? ""));
        var gate = new GameFolderWindow(found, downloads) { Prepare = prepare };
        gate.Accepted = dir =>
        {
            var next = start(dir);
            becomeMain?.Invoke(next);
            next.Show();
            gate.Close();
        };
        return gate;
    }

    /// <summary>
    /// A small window that says "Preparing the game files ..." while <paramref name="prepare"/>
    /// gets <paramref name="dir"/> ready off the window's thread - the first start on a Mordor whose
    /// pictures and help are not yet in the cache, which takes a while - then shows the window
    /// <paramref name="then"/> makes of the outcome (null, or what went wrong), makes it the main
    /// window (<paramref name="becomeMain"/>) and goes. Closed meanwhile, it shows nothing more.
    /// </summary>
    public static Window WhilePreparing(string dir, Func<string, Task<string?>> prepare, Func<string?, Window> then, Action<Window>? becomeMain = null)
    {
        var wait = new Window
        {
            Title = "OpenDejenol " + Updates.Version,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Thickness(20), Spacing = 8,
                Children =
                {
                    new TextBlock { Text = PreparingText, FontSize = 13, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = "Reading the pictures and the help from your copy of Mordor.", FontSize = 13, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
        bool closed = false, begun = false;
        wait.Closed += (_, _) => closed = true;
        wait.Opened += async (_, _) =>
        {
            if (begun) return;
            begun = true;
            string? error;
            try { error = await prepare(dir); }
            catch (Exception ex) { CrashLog.Write("preparing " + dir, ex); error = "The game files could not be prepared: " + ex.Message; }
            if (closed) return;
            var next = then(error);
            becomeMain?.Invoke(next);
            next.Show();
            wait.Close();
        };
        return wait;
    }

    /// <summary>The desktop lifetime's main window becomes <paramref name="w"/>.</summary>
    public static void MakeMain(Window w)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life) life.MainWindow = w;
    }
}
