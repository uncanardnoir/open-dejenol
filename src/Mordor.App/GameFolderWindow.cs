using System;
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
/// found, and lets the player point it at their Mordor folder. A folder with the files, full game
/// or shareware, is remembered in the launch settings and the game goes on to the menu - the
/// shareware's as the limited build plays (<see cref="LimitedVersion"/>).
///
/// The port's own, with no counterpart in the original, which found its data through BasePath in
/// MORDOR.INI and said only "Is BasePath in MORDOR.INI correct?" when it could not
/// (MAINMOD.BAS:1193-1196).
/// </summary>
public sealed class GameFolderWindow : Window
{
    /// <summary>Asks the player for a folder; null when they cancel. Replaceable, for the headless check.</summary>
    public Func<Window, Task<string?>> PickFolder { get; set; } = AskForFolder;

    /// <summary>Called with a playable Mordor's folder once one has been picked (and remembered).</summary>
    public Action<string>? Accepted { get; set; }

    /// <summary>The line that says what is wrong with the folder found or picked.</summary>
    public TextBlock Problem { get; }

    public Button LocateButton { get; }
    public Button ExitButton { get; }

    public GameFolderWindow(GameFolderResolution found)
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
                 + "the game. It contains none of the game's data, so it needs the folder of an installed Mordor - "
                 + "the folder with MORDOR.EXE and the MDATA files in it.\n\n"
                 + "The shareware (\"PUBLIC Limited Version\") plays as the shareware did, with its "
                 + GameFiles.SharewareLevels + " dungeon levels; the full version has all " + GameFiles.FullLevels + ".\n\n"
                 + "Install Mordor (from the Mordor 1.1 installer, MordorSetup.exe, the original CD-ROM or disks, or "
                 + "the shareware), then choose the folder it was installed to. The choice is remembered.",
        };
        Problem = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Salmon, FontSize = 13,
            Text = Describe(found.Nearest, found.Explicit),
        };
        LocateButton = new Button { Content = "_Locate Mordor...", MinWidth = 130, HorizontalContentAlignment = HorizontalAlignment.Center };
        ExitButton = new Button { Content = "E_xit", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        LocateButton.Click += async (_, _) => await Locate();
        ExitButton.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 12,
            Children =
            {
                heading, explain, Problem,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { LocateButton, ExitButton },
                },
            },
        };
        Opened += (_, _) => LocateButton.Focus();
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
        bool kept = GameFolder.Remember(check.Dir);
        LimitedVersion.Apply(check);
        Accepted?.Invoke(check.Dir);
        // after the hand-over, so the note goes to the crash log beside the folder now in use
        CrashLog.Note("game folder chosen: " + check.Dir + " (" + check.Recognised + ")"
                      + (kept ? "" : ", but it could not be kept in " + GameFolder.LaunchIniPath));
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

    /// <summary>
    /// The start: <paramref name="start"/>'s window straight away when a Mordor to play was found
    /// (the limited build's behaviour switched on for the shareware's data), otherwise this window,
    /// which hands over to <paramref name="start"/>'s window (and <paramref name="becomeMain"/>)
    /// once one is picked. <paramref name="start"/> is never called without a playable folder.
    /// </summary>
    public static Window FirstWindow(GameFolderResolution found, Func<string, Window> start, Action<Window>? becomeMain = null)
    {
        if (found.IsPlayable)
        {
            LimitedVersion.Apply(found.Found);
            if (found.Found!.IsLimited) CrashLog.Note("the shareware's data: playing as the limited build - " + found.Found.Dir);
            return start(found.Found.Dir);
        }
        CrashLog.Note("no Mordor to play: " + (found.Nearest?.Dir ?? "nothing found") + " - " + (found.Nearest?.Reason ?? ""));
        var gate = new GameFolderWindow(found);
        gate.Accepted = dir =>
        {
            var next = start(dir);
            becomeMain?.Invoke(next);
            next.Show();
            gate.Close();
        };
        return gate;
    }

    /// <summary>The desktop lifetime's main window becomes <paramref name="w"/>.</summary>
    public static void MakeMain(Window w)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life) life.MainWindow = w;
    }
}
