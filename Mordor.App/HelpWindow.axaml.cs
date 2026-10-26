using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// The game's own Help Lesson, shown inside the port. The text is the original's, read out of the
/// player's MORDOR.HLP at start (Mordor.Core.Extraction.HelpBundle, kept in the art cache - see Art).
/// Nothing here is rewritten; the WinHelp topics become plain text with their tabs and jumps.
///
/// The original handed F1 and the Help buttons to WinHelp on MORDOR.HLP (MAIN.TXT:596, the
/// WinHelp declaration at MODULE1.BAS:42), so it had no help window of its own. The title and the
/// header are the help's own heading, "Mordor 1.1 (M1 Depths of Dejenol) Help Lesson" (its Welcome
/// page). The topic list and the search box are port chrome standing in for WinHelp's Contents and
/// Search, and are kept to a bare list and a box.
///
/// What that chrome says is the port's, and is kept on purpose because it has no original to copy:
/// the original's help button asks WinHelp for its contents, <c>WinHelp(hWnd, App.HelpFile, 3, 0)</c>
/// - 3 is HELP_CONTENTS (FRMMAIN.BAS:75-76) - and every word after that is WinHelp's, not Mordor's.
/// So the box's "Search" watermark, the "No match" heading when a search finds nothing, and the
/// "Help unavailable" page when the extracted help cannot be read are the stand-in's own labels.
/// The search itself is a plain case-insensitive match on a topic's title or text; WinHelp's Search
/// looked up the help file's keywords instead (MORDOR.HLP's |KWBTREE, which the extraction does
/// not read).
///
/// The help's jumps - the 103 cross-references such as the Welcome page's list of topics - are
/// drawn as WinHelp drew a jump, green (RGB 0,128,0) with a solid underline, and a click on one
/// opens its topic. Tab and Shift+Tab in the page step through them and Enter follows one, as in
/// WinHelp; the reversed look of the jump the keyboard is on is the port's. WinHelp's Back button
/// has no counterpart, as none of its button bar does.
/// </summary>
public partial class HelpWindow : Window
{
    /// <summary>A tabbed line's first-cell start and tab stops, in points; -1 is a default stop.</summary>
    internal sealed record TabRow(double Start, double[] Stops);

    /// <summary>A WinHelp jump: Length characters of the topic's text from Start, leading to topic To.</summary>
    internal sealed record Link(int Start, int Length, int To);

    private sealed record Topic(string Title, string Text, List<TabRow>? Tabs = null, List<Link>? Links = null);

    private List<Topic> _topics = new();
    private List<Topic> _shown = new();
    /// <summary>The search the topic list is filtered by.</summary>
    private string? _query;

    /// <summary>The jumps of the topic on show, as spans of the text on screen.</summary>
    private readonly List<(int Start, int End, int To)> _jumps = new();
    /// <summary>The jump the keyboard is on (Tab and Shift+Tab in the page), or -1.</summary>
    private int _jumpAt = -1;
    /// <summary>The jump a left press went down on, followed if the release comes up on it too.</summary>
    private int _pressed = -1;

    /// <summary>WinHelp draws a jump in green, RGB 0,128,0, with a solid underline.</summary>
    private static readonly IBrush JumpBrush = new SolidColorBrush(Color.FromRgb(0, 128, 0));

    /// <summary>Every help window that is up.</summary>
    private static readonly List<HelpWindow> OpenWindows = new();

    /// <summary>
    /// <c>WinHelp(frmMain.hWnd, App.HelpFile, HELP_QUIT, 0)</c> in mdrEndProgram (MAINMOD.BAS:938,
    /// <c>mc01E6 = 2</c>; extfn012C is WinHelp): as the game ends, the help it opened closes with
    /// it. Without this a help window left open kept the program running after its last game
    /// window had gone.
    /// </summary>
    public static void CloseAll()
    {
        foreach (var w in OpenWindows.ToArray()) w.Close();
    }

    public HelpWindow(string? openAt = null)
    {
        InitializeComponent();
        Opened += (_, _) => OpenWindows.Add(this);
        Closed += (_, _) => OpenWindows.Remove(this);
        // a session end closes every window at once, the help among them; but the help goes only
        // when mdrEndProgram runs (MAINMOD.BAS:938), after a Yes to the game's own question
        // (FRMMAIN.BAS:176-192). While that question is still to be answered the help stays, and a
        // Yes closes it through CloseAll; with no game screen to ask, it goes with the session.
        Closing += (_, e) =>
        {
            if (e.CloseReason == WindowCloseReason.OSShutdown && !e.IsProgrammatic && OriginalBox.SessionEndWillAsk)
                e.Cancel = true;
        };
        // a change of text that leaves the query as it is (a jump clearing the box after clearing the
        // filter itself) keeps the topic on show
        SearchBox.TextChanged += (_, _) => { if ((SearchBox.Text ?? "").Trim() != _query) Filter(SearchBox.Text ?? ""); };
        TopicList.SelectionChanged += (_, _) => Show(TopicList.SelectedIndex);
        TopicBody.PointerMoved += (_, e) => TopicBody.Cursor = JumpAt(e.GetPosition(TopicBody)) >= 0 ? HandCursor : null;
        TopicBody.AddHandler(PointerPressedEvent, OnBodyPressed, RoutingStrategies.Tunnel, true);
        TopicBody.AddHandler(PointerReleasedEvent, OnBodyReleased, RoutingStrategies.Bubble, true);
        // on the scroller, which takes the focus when the page is tabbed to or clicked beside the text
        BodyScroll.AddHandler(KeyDownEvent, OnBodyKey, RoutingStrategies.Tunnel);
        Load();
        Filter("");

        int start = openAt == null ? 0 : _shown.FindIndex(t => t.Title.Contains(openAt, StringComparison.OrdinalIgnoreCase));
        TopicList.SelectedIndex = start < 0 ? 0 : start;
        // Filter() already selected row 0, so the assignment above may not raise SelectionChanged
        Show(TopicList.SelectedIndex);
    }

    private void Load()
    {
        try
        {
            // read out of the player's MORDOR.HLP at start (Art, Mordor.Core.Extraction.HelpBundle)
            string path = Art.PathOf(Mordor.Core.Extraction.GameArt.HelpFile);
            if (!System.IO.File.Exists(path))
                throw new System.IO.FileNotFoundException("There is no MORDOR.HLP in the game folder to read the help from.");
            using var s = System.IO.File.OpenRead(path);
            _topics = JsonSerializer.Deserialize<List<Topic>>(s) ?? new List<Topic>();
        }
        catch (Exception ex)
        {
            _topics = new List<Topic> { new("Help unavailable", "The help bundle could not be loaded.\n\n" + ex.Message) };
        }
    }

    private void Filter(string query)
    {
        query = query.Trim();
        _query = query;
        _shown = query.Length == 0
            ? _topics
            : _topics.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                              || t.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        TopicList.ItemsSource = _shown.Select(t => t.Title).ToList();
        if (_shown.Count > 0) TopicList.SelectedIndex = 0; else { TopicTitle.Text = "No match"; SetBody("", null); }
    }

    private void Show(int index)
    {
        if (index < 0 || index >= _shown.Count) return;
        var t = _shown[index];
        TopicTitle.Text = t.Title;
        SetBody(AlignTabs(t.Text, t.Tabs, ArialPx, CharPx(), out var map), t.Links?.Select(l => (map[l.Start], map[l.Start + l.Length], l.To)));
        BodyScroll.ScrollToHome();
    }

    /// <summary>Puts the page up, its jumps drawn as WinHelp drew them.</summary>
    private void SetBody(string text, IEnumerable<(int Start, int End, int To)>? jumps)
    {
        _jumps.Clear();
        _jumps.AddRange((jumps ?? Enumerable.Empty<(int, int, int)>()).Where(j => j.End > j.Start).OrderBy(j => j.Start));
        _jumpAt = _pressed = -1;
        _text = text;
        TopicBody.SelectionStart = TopicBody.SelectionEnd = 0;
        DrawBody();
    }

    private string _text = "";

    /// <summary>
    /// The page's text as runs, each jump green and underlined, and the one the keyboard is on
    /// drawn reversed (green behind, the page's colour in front) so it can be seen.
    /// </summary>
    private void DrawBody()
    {
        var inlines = new InlineCollection();
        int at = 0;
        for (int k = 0; k < _jumps.Count; k++)
        {
            var (start, end, _) = _jumps[k];
            if (start < at) continue;
            if (start > at) inlines.Add(new Run(_text[at..start]));
            var run = new Run(_text[start..end]) { Foreground = JumpBrush, TextDecorations = TextDecorations.Underline };
            if (k == _jumpAt) { run.Background = JumpBrush; run.Foreground = Background; }
            inlines.Add(run);
            at = end;
        }
        if (at < _text.Length) inlines.Add(new Run(_text[at..]));
        TopicBody.Inlines = inlines;
    }

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    /// <summary>
    /// The jump under a point of the page, or -1. The character nearest the point must be in a
    /// jump, and the point on that jump's words, not merely level with them past a line's end.
    /// </summary>
    private int JumpAt(Point p)
    {
        if (_jumps.Count == 0) return -1;
        var layout = TopicBody.TextLayout;
        int pos = layout.HitTestPoint(p).TextPosition;
        int j = _jumps.FindIndex(j => pos >= j.Start && pos < j.End);
        if (j < 0) return -1;
        return layout.HitTestTextRange(_jumps[j].Start, _jumps[j].End - _jumps[j].Start).Any(r => r.Contains(p)) ? j : -1;
    }

    private Point _pressedAt;

    // Decided as the press comes down, before the page's own selection handling redoes its layout;
    // the release only checks that it came up where the press went down, in the scroller's
    // coordinates so the page scrolling under the pointer is a move too.
    private void OnBodyPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedAt = e.GetPosition(BodyScroll);
        _pressed = e.GetCurrentPoint(TopicBody).Properties.IsLeftButtonPressed ? JumpAt(e.GetPosition(TopicBody)) : -1;
    }

    private void OnBodyReleased(object? sender, PointerReleasedEventArgs e)
    {
        int j = _pressed;
        _pressed = -1;
        var at = e.GetPosition(BodyScroll);
        if (e.InitialPressMouseButton != MouseButton.Left || j < 0 || j >= _jumps.Count) return;
        // a drag to select the jump's words is a selection, not a click
        if (Math.Abs(at.X - _pressedAt.X) > 4 || Math.Abs(at.Y - _pressedAt.Y) > 4) return;
        Follow(_jumps[j].To);
    }

    /// <summary>
    /// Tab and Shift+Tab in the page step through its jumps, WinHelp's own keys for them, and Enter
    /// follows the one they are on. Past the last (or first) jump Tab leaves the page as usual.
    /// </summary>
    private void OnBodyKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && _jumps.Count > 0)
        {
            bool back = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            int next = _jumpAt < 0 ? (back ? _jumps.Count - 1 : 0) : _jumpAt + (back ? -1 : 1);
            if (next < 0 || next >= _jumps.Count) { _jumpAt = -1; DrawBody(); return; }
            _jumpAt = next;
            DrawBody();
            // once the page is laid out again, scroll the jump into view if it is off screen
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_jumpAt != next || next >= _jumps.Count) return;
                var rects = TopicBody.TextLayout.HitTestTextRange(_jumps[next].Start, _jumps[next].End - _jumps[next].Start).ToList();
                if (rects.Count == 0) return;
                double y = rects.Min(r => r.Top), bottom = rects.Max(r => r.Bottom);
                double top = BodyScroll.Offset.Y, view = BodyScroll.Viewport.Height;
                if (y < top || bottom > top + view)
                    BodyScroll.Offset = new Vector(BodyScroll.Offset.X, Math.Max(0, y - view / 3));
            }, Avalonia.Threading.DispatcherPriority.Background);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _jumpAt >= 0 && _jumpAt < _jumps.Count)
        {
            Follow(_jumps[_jumpAt].To);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Opens the topic a jump leads to, as WinHelp did: it is selected in the topic list, and a
    /// search that hides it is cleared first.
    /// </summary>
    private void Follow(int to)
    {
        if (to < 0 || to >= _topics.Count) return;
        var target = _topics[to];
        if (!_shown.Any(t => ReferenceEquals(t, target))) { Filter(""); SearchBox.Text = ""; }
        int index = _shown.FindIndex(t => ReferenceEquals(t, target));
        if (index < 0) return;
        if (TopicList.SelectedIndex == index) Show(index);
        else TopicList.SelectedIndex = index;
        TopicList.ScrollIntoView(index);
        TopicBody.Focus();
    }

    // The help was written for WinHelp's Arial 10pt, which is 13 pixels at 96 dpi; its tab stops
    // are measured against that text, so that is what decides where a tab lands.
    private static readonly Typeface Arial = new("Arial");
    private static double ArialPx(string s) => s.Length == 0 ? 0 :
        new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Arial, 13, null).WidthIncludingTrailingWhitespace;

    private double CharPx() =>
        new FormattedText("0000000000", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(TopicBody.FontFamily), TopicBody.FontSize, null).WidthIncludingTrailingWhitespace / 10;

    /// <summary>
    /// Lines up the help's tabbed paragraphs as WinHelp's tab stops did. The bundler keeps each
    /// WinHelp tab as a '\t' and, per tabbed line, where its first cell starts and where each tab's
    /// stop is, in points (see HelpBundler). A tab goes to its own stop, or, when the text before it
    /// already runs past that stop, to the next stop beyond the text; a tab with no stop of its own,
    /// or one past the line's last stop, goes to WinHelp's default grid, every half inch. The text is
    /// measured in Arial 10pt for this, as WinHelp drew it.
    ///
    /// The page itself is drawn in a monospaced font, so a stop becomes a character column: its
    /// distance from the table's left edge at the font's character width, pushed right where
    /// needed so a cell in the wider font still ends at least two spaces before the next cell of its
    /// row. A run of consecutive tabbed lines is one table and shares its columns. Lines without a
    /// tab are left exactly as they are.
    /// </summary>
    internal static string AlignTabs(string text, IReadOnlyList<TabRow>? tabs, Func<string, double> textPx, double charPx) =>
        AlignTabs(text, tabs, textPx, charPx, out _);

    /// <summary>
    /// As above, and <paramref name="map"/> says where each character of the text (and its end, at
    /// text.Length) lands in what comes back, so a jump's span can follow its words into a table.
    /// A tab maps to the end of the cell before it.
    /// </summary>
    internal static string AlignTabs(string text, IReadOnlyList<TabRow>? tabs, Func<string, double> textPx, double charPx, out int[] map)
    {
        const double ptPx = 96.0 / 72, grid = 36 * ptPx;
        var lines = text.Split('\n');
        var output = new List<string>(lines.Length);
        var lineAt = new int[lines.Length];
        for (int k = 1; k < lines.Length; k++) lineAt[k] = lineAt[k - 1] + lines[k - 1].Length + 1;
        var srcMap = new int[text.Length + 1];
        int dst = 0;   // where the next output line starts
        int next = 0;
        for (int i = 0; i < lines.Length;)
        {
            if (!lines[i].Contains('\t'))
            {
                for (int c = 0; c <= lines[i].Length; c++) srcMap[lineAt[i] + c] = dst + c;
                dst += lines[i].Length + 1;
                output.Add(lines[i++]);
                continue;
            }
            int end = i;
            while (end < lines.Length && lines[end].Contains('\t')) end++;

            // where WinHelp puts each cell, in pixels from the page edge
            var rows = new List<(string[] Cells, double[] At)>();
            for (int k = i; k < end; k++, next++)
            {
                var cells = lines[k].Split('\t');
                var row = tabs != null && next < tabs.Count ? tabs[next] : null;
                var stops = (row?.Stops ?? Array.Empty<double>()).Select(s => s < 0 ? -1 : s * ptPx).ToArray();
                var own = stops.Where(s => s >= 0).OrderBy(s => s).ToArray();
                var at = new double[cells.Length];
                at[0] = (row?.Start ?? 0) * ptPx;
                for (int c = 1; c < cells.Length; c++)
                {
                    double x = at[c - 1] + textPx(cells[c - 1]);
                    double stop = c - 1 < stops.Length ? stops[c - 1] : -1;
                    if (stop < 0 || stop <= x)
                    {
                        stop = stop < 0 ? -1 : own.FirstOrDefault(s => s > x, -1);
                        if (stop < 0) stop = (Math.Floor(x / grid) + 1) * grid;
                    }
                    at[c] = stop;
                }
                rows.Add((cells, at));
            }

            // each landing place becomes a character column; stops a point or two apart (71pt
            // beside 72pt in Commands) are one column on screen
            double left = rows.Min(r => r.At[0]);
            var snap = new Dictionary<double, double>();
            double head = double.NegativeInfinity;
            foreach (var p in rows.SelectMany(r => r.At).Distinct().OrderBy(p => p))
            {
                if (p - head >= charPx / 2) head = p;
                snap[p] = head;
            }
            foreach (var (_, at) in rows)
                for (int k = 0; k < at.Length; k++) at[k] = snap[at[k]];
            var places = rows.SelectMany(r => r.At).Distinct().OrderBy(p => p).ToList();
            var col = new Dictionary<double, int>();
            int prev = -1;
            foreach (var p in places)
            {
                int c = Math.Max((int)Math.Round((p - left) / charPx), prev + 1);
                foreach (var (cells, at) in rows)
                    for (int k = 1; k < cells.Length; k++)
                        if (at[k] == p && at[k - 1] < p && cells[k - 1].Length > 0)
                            c = Math.Max(c, col[at[k - 1]] + cells[k - 1].Length + 2);
                col[p] = prev = c;
            }
            int shift = col[places[0]];
            for (int r = 0; r < rows.Count; r++)
            {
                var (cells, at) = rows[r];
                var sb = new System.Text.StringBuilder();
                int src = lineAt[i + r];
                for (int k = 0; k < cells.Length; k++)
                {
                    int want = col[at[k]] - shift;
                    if (sb.Length < want) sb.Append(' ', want - sb.Length);
                    // each character of the cell, and the tab (or line end) after it
                    for (int c = 0; c <= cells[k].Length; c++) srcMap[src + c] = dst + sb.Length + c;
                    src += cells[k].Length + 1;
                    sb.Append(cells[k]);
                }
                string line = sb.ToString().TrimEnd();
                for (int c = lineAt[i + r]; c < src; c++) srcMap[c] = Math.Min(srcMap[c], dst + line.Length);
                dst += line.Length + 1;
                output.Add(line);
            }
            i = end;
        }
        map = srcMap;
        return string.Join("\n", output);
    }
}
