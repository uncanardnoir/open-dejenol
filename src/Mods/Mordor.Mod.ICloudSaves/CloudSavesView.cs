using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Mordor.Mod.ICloudSaves;

/// <summary>
/// The iCloud Saves window, a game child window (<c>ModUI.AddWindow</c>) opened from its toolbar
/// button: where the saves stand against iCloud, the last sync (when, what, and which Mac's world),
/// the newest snapshot in iCloud, the last thing the mod did, and the buttons - Upload now, Restore at
/// next start (or Cancel the restore), Refresh. In a conflict the two choices are named as such: Keep
/// this Mac's (an upload; iCloud's copy stays as an earlier version) and Use iCloud's at next start.
///
/// The text takes the window's font, which the game gives every mod window (its list rows' face and
/// size). The status is read off the window's thread - it reads about a megabyte and lists a folder -
/// and drawn back on it. The game does not guard the handlers of a mod's own controls, so each
/// catches its own failure and reports it to the mod's log.
///
/// Tagged controls (Tag), for a headless check: <c>Head</c>, <c>LastSync</c>, <c>Newest</c>,
/// <c>Message</c>, <c>Upload</c>, <c>Restore</c>, <c>Refresh</c>.
/// </summary>
public static class CloudSavesView
{
    private static readonly IBrush Plain = Brushes.WhiteSmoke;
    private static readonly IBrush Caption = Brushes.CornflowerBlue;
    private static readonly IBrush Warn = Brushes.Khaki;

    public static Control Build(CloudSaves saves, Action<string, Exception?> report)
    {
        TextBlock Line(string tag, IBrush brush) => new() { Tag = tag, Foreground = brush, TextWrapping = TextWrapping.Wrap };
        var head = Line("Head", Plain);
        head.FontWeight = FontWeight.Bold;
        var last = Line("LastSync", Plain);
        var newest = Line("Newest", Plain);
        var message = Line("Message", Caption);
        var upload = new Button { Tag = "Upload", Content = "Upload now" };
        var restore = new Button { Tag = "Restore", Content = "Restore at next start" };
        var refresh = new Button { Tag = "Refresh", Content = "Refresh" };
        foreach (var b in new[] { upload, restore, refresh }) { b.Focusable = false; b.Padding = new Thickness(8, 2); }

        StatusLines? shown = null;
        bool reading = false, again = false;

        void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception ex) { report(what, ex); }
        }

        void Draw(StatusLines s)
        {
            shown = s;
            head.Text = s.Head;
            head.Foreground = s.Conflict ? Warn : Plain;
            last.Text = s.LastSync;
            newest.Text = s.Newest;
            message.Text = saves.LastMessage;
            upload.Content = s.Conflict ? "Keep this Mac's" : "Upload now";
            restore.Content = s.Pending != null ? "Cancel the restore" : s.Conflict ? "Use iCloud's at next start" : "Restore at next start";
            restore.IsEnabled = s.Pending != null || s.Restorable != null;
            upload.IsEnabled = true;
        }

        // the status is read off the window's thread and drawn on it; one reading at a time
        void Read()
        {
            // a reading asked for while one runs is done straight after it, so the last change is shown
            if (reading) { again = true; return; }
            reading = true;
            upload.IsEnabled = restore.IsEnabled = false;
            Task.Run(saves.Describe).ContinueWith(t =>
                Dispatcher.UIThread.Post(() => Safe("drawing the status", () =>
                {
                    reading = false;
                    Draw(t.IsCompletedSuccessfully ? t.Result : new StatusLines("The status could not be read.", "", "", null, null, false));
                    if (again) { again = false; Read(); }
                })));
        }

        // on the window's thread, as the game's own saves are, so the world is never read while the game
        // is part-way through writing it; it is about a megabyte, a moment's work
        upload.Click += (_, _) => Safe("Upload now", () =>
        {
            saves.UploadNow();
            Read();
        });
        restore.Click += (_, _) => Safe("Restore", () =>
        {
            if (shown is not { } s) return;
            saves.RestoreAtNextStart(s.Pending != null ? null : s.Restorable);
            Read();
        });
        refresh.Click += (_, _) => Safe("Refresh", Read);

        // a sync made elsewhere (a save point) is shown when it happens
        void OnChanged() => Dispatcher.UIThread.Post(() => Safe("after a sync", Read));
        var root = new StackPanel
        {
            Margin = new Thickness(8), Spacing = 6,
            Children =
            {
                head, last, newest,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { upload, restore, refresh } },
                message,
            },
        };
        root.AttachedToVisualTree += (_, _) => { saves.Changed += OnChanged; Read(); };
        root.DetachedFromVisualTree += (_, _) => saves.Changed -= OnChanged;
        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }
}
