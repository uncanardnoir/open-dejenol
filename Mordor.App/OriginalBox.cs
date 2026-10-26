using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// The original's <c>MsgBox</c>: a titled box with one line of text and OK, or Yes and No. The city
/// and the dungeon windows have their own copies (Tell / Confirm); this one is for the windows
/// that had none.
/// </summary>
public static class OriginalBox
{
    /// <summary><c>MsgBox text, 64, title</c>.</summary>
    public static System.Threading.Tasks.Task Tell(Window owner, string title, string text) =>
        Show(owner, title, text, yesNo: false);

    /// <summary><c>MsgBox(text, 36, title) = 6</c>: true for Yes.</summary>
    public static System.Threading.Tasks.Task<bool> YesNo(Window owner, string title, string text) =>
        Show(owner, title, text, yesNo: true);

    /// <summary>
    /// Yes and No with No as the default button: MB_DEFBUTTON2 (256) is in the style. fn0662's and
    /// the Options window's <c>mc0208 &amp; mc020A</c> are 32 and 4 joined with VB's string
    /// operator, "324", which is 256 + 64 + 4 - so not the question mark meant, but No first.
    /// </summary>
    public static System.Threading.Tasks.Task<bool> YesNoDefaultNo(Window owner, string title, string text) =>
        Show(owner, title, text, yesNo: true, defaultNo: true);

    /// <summary>
    /// MDIForm_QueryUnload's first test (FRMMAIN.BAS:171-175): an UnloadMode of 0 - the frame's
    /// close box, its system menu or Alt+F4 - is refused with "Please use the Mordor EXIT buttons to
    /// leave the game." (MsgBox 64, "Exit Mordor"), and nothing else happens. frmMain is the one
    /// frame the menu, the city and the dungeon all sit in, so the port puts this on each of the
    /// windows that stand in for it. A Close() from the game's own Exit buttons is programmatic and
    /// passes, as <c>Unload frmMain</c> (UnloadMode 1) does.
    ///
    /// Any other UnloadMode - Windows ending the session among them - goes on through the rest of
    /// MDIForm_QueryUnload (FRMMAIN.BAS:176-192): nothing once the program is already ending
    /// (gv1D06); otherwise, with no monsters in the area, or dead, or in town, "Exit Mordor?"
    /// (MsgBox 36, "EXIT"), whose Yes runs sub16CC while a game is on (gv1B52) and then
    /// mdrEndProgram, and whose No cancels; with monsters there, a Beep and "Cannot exit while
    /// monsters are in the area!", and the close is cancelled. <paramref name="queryEnd"/> is that
    /// part for the screen that is up; it answers whether the program may end. The port had let
    /// a session end take the program down without a word.
    /// </summary>
    public static void RefuseCloseBox(Window w, System.Func<System.Threading.Tasks.Task<bool>>? queryEnd = null)
    {
        if (queryEnd != null)
        {
            w.Opened += (_, _) => Frames.Add((w, queryEnd));
            w.Closed += (_, _) => Frames.RemoveAll(f => f.Window == w);
        }
        bool showing = false;
        w.Closing += async (_, e) =>
        {
            if (e.IsProgrammatic || Ending) return;          // "If gv1D06 = True Then Exit Sub" (176)
            if (e.CloseReason == WindowCloseReason.OSShutdown)
            {
                e.Cancel = true;
                await EndSession();
                return;
            }
            if (e.CloseReason != WindowCloseReason.WindowClosing) return;
            e.Cancel = true;
            if (showing) return;
            showing = true;
            try { await Tell(w, "Exit Mordor", "Please use the Mordor EXIT buttons to leave the game."); }
            finally { showing = false; }
        };
    }

    /// <summary><c>gv1D06</c>: set by mdrEndProgram as the program ends (MAINMOD.BAS:929).</summary>
    public static bool Ending { get; private set; }

    /// <summary>
    /// Alt+F4: the system's request to close the frame, which the frame's QueryUnload answers
    /// (FRMMAIN.BAS:171-175). A key handler must let it go by rather than read it as F4.
    /// </summary>
    public static bool IsCloseChord(Avalonia.Input.KeyEventArgs e) =>
        e.Key == Avalonia.Input.Key.F4 && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt);

    /// <summary>
    /// A screen drawn inside another window's frame - the dungeon inside the city's - whose own
    /// question a session end asks while it is there, over that frame. The frame's own close box
    /// is already refused by the frame.
    /// </summary>
    public static void HostFrame(Window frame, System.Func<System.Threading.Tasks.Task<bool>> queryEnd) =>
        Frames.Add((frame, queryEnd));

    /// <summary>
    /// Whether a session end will be put to a game screen's MDIForm_QueryUnload question first
    /// (FRMMAIN.BAS:176-192), which may yet keep the game - and so the help - running.
    /// </summary>
    public static bool SessionEndWillAsk => !Ending && Frames.Count > 0;

    /// <summary>The screen has left the frame; the frame's own question is asked again.</summary>
    public static void UnhostFrame(System.Func<System.Threading.Tasks.Task<bool>> queryEnd) =>
        Frames.RemoveAll(f => f.Query == queryEnd);

    /// <summary>The screens that are up, oldest first, each with its part of MDIForm_QueryUnload.</summary>
    private static readonly System.Collections.Generic.List<(Window Window, System.Func<System.Threading.Tasks.Task<bool>> Query)> Frames = new();

    private static bool _asking;

    /// <summary>
    /// A session end: the screen in front asks, and a Yes ends the program - mdrEndProgram, then
    /// MDIForm_Unload's farewell box (FRMMAIN.BAS:237-248), as the menu's Exit has it.
    /// </summary>
    private static async System.Threading.Tasks.Task EndSession()
    {
        if (_asking || Frames.Count == 0) return;
        _asking = true;
        try
        {
            var (owner, query) = Frames[^1];
            if (!await query()) return;
            Ending = true;
            // mdrEndProgram's WinHelp HELP_QUIT (MAINMOD.BAS:938), only after the Yes: a No or
            // "Cannot exit while monsters are in the area!" leaves the help up with the game
            HelpWindow.CloseAll();
            await ThankYou(owner);
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life)
                life.Shutdown();
        }
        finally { _asking = false; }
    }

    /// <summary>
    /// MDIForm_Unload's box (FRMMAIN.BAS:241-248): MsgBox 64, titled gc0192 + " " + gc0186,
    /// "Mordor 1.1" (MAIN.TXT:33-35). Only "If m009C = False" (240): the limited build, played on
    /// the shareware's data (<see cref="Mordor.Core.LimitedVersion"/>), says nothing on the way out.
    /// </summary>
    public static System.Threading.Tasks.Task ThankYou(Window owner) =>
        !Mordor.Core.LimitedVersion.SaysGoodbye ? System.Threading.Tasks.Task.CompletedTask :
        Tell(owner, "Mordor 1.1",
            "THANK YOU FOR PLAYING MORDOR!\n\n"
            + "If you know someone who would enjoy playing Mordor, please feel free to give them a copy of "
            + "the shareware release of Mordor, which is included on the Mordor CD-ROM.\n"
            + "You can also find the shareware version of Mordor on many FTP sites, Compuserve, the Microsoft Network, and many other locations (see MORDOR.WRI for more information on distributing the Shareware version).\n\n"
            + "Happy Adventuring!\n\n"
            + "Please report any illegal distribution of the commercial version to the SPA at (800) 388-PIR8 or TDA at (800) 624-2101.");

    private static async System.Threading.Tasks.Task<bool> Show(Window owner, string title, string text, bool yesNo, bool defaultNo = false)
    {
        bool yes = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var win = new Window
        {
            Title = title,
            Width = 420, SizeToContent = SizeToContent.Height,
            Background = Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        // Windows' message font, as MsgBox's text is, whatever the Options fonts and the owner's face
        UiFonts.MessageBox(win);
        if (yesNo)
        {
            var y = new Button { Content = "Yes", IsDefault = !defaultNo };
            var n = new Button { Content = "No", IsCancel = true, IsDefault = defaultNo };
            y.Click += (_, _) => { yes = true; win.Close(); };
            n.Click += (_, _) => win.Close();
            buttons.Children.Add(y); buttons.Children.Add(n);
            win.Opened += (_, _) => { if (defaultNo) n.Focus(); else y.Focus(); };
        }
        else
        {
            var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true };
            ok.Click += (_, _) => win.Close();
            buttons.Children.Add(ok);
            win.Opened += (_, _) => ok.Focus();
        }
        win.Content = new Border
        {
            Padding = new Avalonia.Thickness(14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = text, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
                    buttons,
                },
            },
        };
        await win.ShowDialog(owner);
        return yes;
    }
}
