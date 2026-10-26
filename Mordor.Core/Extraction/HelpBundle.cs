using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mordor.Core.Extraction;

/// <summary>
/// The game's help as the port's help window shows it: one topic per WinHelp topic, its title,
/// its text with paragraphs on lines of their own and tabs kept as tabs, the tab stops of each
/// tabbed line, and the jumps as spans of the text. Made from the player's MORDOR.HLP by
/// <see cref="FromHelpFile"/> and kept as JSON in the extracted-art cache.
///
/// <para>Until October 2026 the port shipped this bundle, made from the CHM that the 2015 edition's
/// help tool (HelpScribble 7.8.8) generated beside MORDOR.HLP from the same project, decompiled to
/// HTML and stripped. The rules below reproduce that bundle from the .HLP itself, so the text,
/// titles, jumps and line layout are what the help window has always shown:</para>
/// <list type="bullet">
/// <item>Each paragraph is a line, trimmed; paragraphs are separated by one blank line, except
/// that consecutive tabbed lines stay together, as a table does; runs of blank lines are one.</item>
/// <item>A tabbed line's cells are trimmed and joined with a tab; tabs at its end are dropped.</item>
/// <item>A tab that opens a hanging-indent paragraph, before any text, takes the text to the
/// indent and is not kept (the Welcome page's "Introduction").</item>
/// <item>Measures: the help file holds indents and tab stops in its own units, about ten twips each
/// (help compilers round twips to tens); HelpScribble's export wrote whole points. A value v is
/// taken as 10v - 3 twips and a point count is truncated toward zero, which gives back every
/// indent and stop of MORDOR.HLP 1.1.</item>
/// <item>The export's spans: a tab's span runs from the previous stop to this one less the line's
/// first-line start (an off-by-the-indent the exported pages have, and the bundle with them);
/// a tab with no stop of its own goes to the next whole inch. Default stops are marked -1, as the
/// bundle marked spans that end on a whole inch or repeat the default width.</item>
/// <item>Table rows are one tabbed line each, a cell a column. Column edges are half the help
/// file's running width-plus-gap, the row starts at half the first gap less half the second, and
/// each span is shortened by the row's start as tab spans are.</item>
/// </list>
/// <para>What cannot be had from the .HLP: where the help compiler's rounding to tens of twips
/// lost a half point, a table column edge can land a point from where the export put it. See
/// <c>docs/GAME_FILES.md</c>.</para>
/// </summary>
public static class HelpBundle
{
    /// <summary>
    /// One tabbed line of a topic's text, in the order those lines appear: where its first cell
    /// begins and where the tab after cell i goes, in points from the page's left edge. A stop of
    /// -1 is a tab with no stop of its own, which goes to WinHelp's default grid.
    /// </summary>
    public sealed record TabRow(double Start, double[] Stops);

    /// <summary>A WinHelp jump: <see cref="Length"/> characters of the text from <see cref="Start"/>, to topic <see cref="To"/>.</summary>
    public sealed record Link(int Start, int Length, int To);

    public sealed record Topic(string Title, string Text, List<TabRow>? Tabs, List<Link>? Links = null);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(List<Topic> topics) => JsonSerializer.Serialize(topics, Json);

    public static List<Topic> Deserialize(string json) => JsonSerializer.Deserialize<List<Topic>>(json, Json) ?? new List<Topic>();

    // stand-ins while lines are put together: a tab between cells, and a jump's start, the end of
    // its target and its end (the bundle's own markers, so its trimming is what it always was)
    private const char Cell = '\u0001';
    private const char LinkOpen = '\u0003';
    private const char LinkName = '\u0004';
    private const char LinkClose = '\u0005';

    /// <summary>What <see cref="FromHelpFile"/> could not do: jumps to no topic, jumps it could not place.</summary>
    public sealed record Report(int LinksInFile, List<string> Unresolved, List<string> Misplaced);

    public static List<Topic> FromHelpFile(WinHelpFile hlp) => FromHelpFile(hlp, out _);

    public static List<Topic> FromHelpFile(WinHelpFile hlp, out Report report)
    {
        var data = hlp.Topics();
        // the topics' offsets, which jumps lead to: the title index has them, in topic order
        var titles = hlp.Titles();
        var starts = new List<int>();
        for (int i = 0; i < data.Count; i++) starts.Add(i < titles.Count ? titles[i].Offset : data[i].Offset);
        var contexts = hlp.Contexts();
        int TopicAt(int offset)
        {
            int best = -1;
            for (int i = 0; i < starts.Count; i++) if (starts[i] <= offset && (best < 0 || starts[i] >= starts[best])) best = i;
            return best;
        }
        int Target(HelpRun r)
        {
            if (r.Hash is uint h) return contexts.TryGetValue(h, out int off) ? TopicAt(off) : -1;
            if (r.Offset is int o) return TopicAt(o);
            return -1;
        }

        // a trailing record with no topic text at all (MORDOR.HLP ends with an untitled one) is no topic
        while (data.Count > 0 && data[^1].Title.Length == 0 && !data[^1].Items.OfType<HelpParagraphData>().Any(p => p.Runs.Any(r => r.Kind == HelpRunKind.Text)))
            data.RemoveAt(data.Count - 1);

        var topics = new List<Topic>();
        var unresolved = new List<string>();
        var misplaced = new List<string>();
        int inFile = 0;
        foreach (var t in data)
        {
            var tabs = new List<TabRow>();
            string text = TextOf(t, tabs, null, out int linkCount);
            inFile += linkCount;
            string marked = TextOf(t, new List<TabRow>(), Target, out _);
            var links = new List<Link>();
            foreach (var (start, length, to) in Place(marked, text, t.Title, misplaced))
            {
                if (to >= 0 && to < data.Count) links.Add(new Link(start, length, to));
                else unresolved.Add($"{t.Title}: \"{text.Substring(start, length)}\"");
            }
            topics.Add(new Topic(t.Title, text, tabs.Count > 0 ? tabs : null, links.Count > 0 ? links : null));
        }
        report = new Report(inFile, unresolved, misplaced);
        return topics;
    }

    /// <summary>A measure in the help file's units as whole points, the way the export wrote it.</summary>
    private static int Twips(int v) => v == 0 ? 0 : 10 * v - 3;

    private static int Points(int twips) => twips / 20;   // C#'s division truncates toward zero

    /// <summary>
    /// A topic's text. With <paramref name="target"/>, each jump is marked with its target topic
    /// for <see cref="Place"/> to find; without, the text is the one the topic keeps.
    /// </summary>
    private static string TextOf(HelpTopicData topic, List<TabRow> tabs, Func<HelpRun, int>? target, out int linkCount)
    {
        int count = 0;
        // each paragraph or row: its line(s) as the export put them on the page, and its tab row
        var raw = new List<(string Line, TabRow? Row)>();
        foreach (var item in topic.Items)
        {
            if (item is HelpParagraphData p)
            {
                if (!p.Ended && p.Runs.Count == 0) continue;      // a record's empty tail
                foreach (var line in Paragraph(p, target, ref count)) raw.Add(line);
            }
            else if (item is HelpTableData row) raw.Add(TableRow(row, target, ref count));
            raw.Add(("", null));                                  // the line break between two paragraphs
        }
        linkCount = count;

        var lines = new List<string>();
        var rows = new List<TabRow?>();
        foreach (var (r, row) in raw)
        {
            string l = r.Trim();
            lines.Add(l.Trim().Trim(Cell).Length == 0 ? "" : l.TrimEnd().TrimEnd(Cell).TrimEnd());
            rows.Add(row);
        }
        var outLines = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            string l = lines[i];
            if (l.Length == 0)
            {
                // the break between two paragraphs is not a blank paragraph, so the rows of one
                // table stay together instead of gaining a gap each
                bool separator = !raw[i].Line.Contains(' ') && !raw[i].Line.Contains(Cell);
                if (separator && outLines.Count > 0 && outLines[^1].Contains('\t') && i + 1 < lines.Count && lines[i + 1].Contains(Cell)) continue;
                if (outLines.Count > 0 && outLines[^1].Length > 0) outLines.Add("");
            }
            else
            {
                if (l.Contains(Cell)) tabs.Add(rows[i] ?? new TabRow(0, Array.Empty<double>()));
                outLines.Add(l.Replace(Cell, '\t'));
            }
        }
        return string.Join("\n", outLines).Trim();
    }

    /// <summary>A run's text as the page shows it: a non-breaking space is a space.</summary>
    private static string Shown(string s) => s.Replace(' ', ' ');

    private static string Cells(List<StringBuilder> cells) =>
        string.Join(Cell, cells.Select(c => TrimSpace(c.ToString())));

    private static string TrimSpace(string s) => s.Trim();

    private static IEnumerable<(string, TabRow?)> Paragraph(HelpParagraphData p, Func<HelpRun, int>? target, ref int count)
    {
        var runs = p.Runs;
        var f = p.Format;
        // a tab that opens a hanging-indent line before any text is the indent, not a cell
        int first = runs.FindIndex(r => r.Kind is HelpRunKind.Text or HelpRunKind.Tab);
        bool skipFirstTab = f.FirstIndent < 0 && first >= 0 && runs[first].Kind == HelpRunKind.Tab && f.TabStops.Count == 0
                            && runs.Skip(first + 1).Any(r => r.Kind == HelpRunKind.Text && r.Text.Trim().Length > 0);
        var cells = new List<StringBuilder> { new() };
        var lines = new List<(string, TabRow?)>();
        int tabCount = 0;
        bool hasText = false;
        foreach (var (r, i) in runs.Select((r, i) => (r, i)))
        {
            switch (r.Kind)
            {
                case HelpRunKind.Text: cells[^1].Append(Shown(r.Text)); hasText = true; break;
                case HelpRunKind.Tab:
                    if (skipFirstTab && i == first) break;
                    cells.Add(new StringBuilder());
                    tabCount++;
                    break;
                case HelpRunKind.LinkStart:
                    if (r.Macro || r.External) break;
                    count++;
                    if (target != null) cells[^1].Append(LinkOpen).Append(target(r)).Append(LinkName);
                    break;
                case HelpRunKind.LinkEnd:
                    if (target != null) cells[^1].Append(LinkClose);
                    break;
                case HelpRunKind.LineBreak:
                    lines.Add((cells.Count > 1 ? Cells(cells) : cells[0].ToString(), null));
                    cells = new List<StringBuilder> { new() };
                    break;
            }
        }
        if (tabCount == 0)
        {
            // an empty paragraph was a non-breaking space on the page; a picture alone, nothing
            string line = cells[0].ToString();
            if (!hasText && !runs.Any(r => r.Kind == HelpRunKind.Picture)) line = " ";
            lines.Add((line, null));
            return lines;
        }
        var row = TabRowOf(f, tabCount, out bool anySpan);
        // a line whose every span came out narrower than nothing (a stop behind the indent) was no
        // table to the bundle: its tabs are the two spaces the export wrote for each
        lines.Add(anySpan ? (Cells(cells), row) : (string.Join("  ", cells.Select(c => c.ToString())), null));
        return lines;
    }

    /// <summary>A tabbed paragraph's first-cell start and stops, as the export's spans gave them.</summary>
    private static TabRow TabRowOf(HelpParagraphFormat f, int tabCount, out bool anySpan)
    {
        int ti = Points(Twips(f.FirstIndent));
        int start = Points(Twips(f.LeftIndent) + Twips(f.FirstIndent));
        var widths = new List<int>();
        int prev = 0;
        for (int i = 0; i < tabCount; i++)
        {
            int stop = i < f.TabStops.Count
                ? Points(Twips(f.TabStops[i].Position))
                : ((i == 0 ? start : prev) / 72 + 1) * 72;
            widths.Add(i == 0 ? stop - start : stop - prev - start);
            prev = stop;
        }
        anySpan = widths.Any(w => w >= 0);
        return Stops(start, widths, i => i == 0 && ti < 0);
    }

    /// <summary>
    /// The stops a run of spans gives: each ends where the one before it did plus its width. A
    /// span with a stop of its own (one written with an indent) keeps it; any other ending on a
    /// whole inch, or as wide as the last default, is WinHelp's default grid.
    /// </summary>
    private static TabRow Stops(int start, List<int> widths, Func<int, bool> own)
    {
        double at = start, defaultWidth = -1;
        var stops = new List<double>();
        for (int i = 0; i < widths.Count; i++)
        {
            at += widths[i];
            double inch = at / 72;
            bool isDefault = !own(i) && at > 0 && (Math.Abs(inch - Math.Round(inch)) < 0.5 / 72 || widths[i] == defaultWidth);
            if (isDefault) defaultWidth = widths[i];
            stops.Add(isDefault ? -1 : at);
        }
        return new TabRow(start, stops.ToArray());
    }

    private static (string, TabRow?) TableRow(HelpTableData row, Func<HelpRun, int>? target, ref int count)
    {
        var cols = row.Columns;
        int n = cols.Length;
        var cells = Enumerable.Range(0, n).Select(_ => new StringBuilder()).ToList();
        foreach (var p in row.Cells)
        {
            if (p.Column < 0 || p.Column >= n) continue;
            var sb = cells[p.Column];
            foreach (var r in p.Runs)
            {
                switch (r.Kind)
                {
                    case HelpRunKind.Text: sb.Append(Shown(r.Text)); break;
                    case HelpRunKind.Tab: sb.Append(' '); break;
                    case HelpRunKind.LinkStart:
                        if (r.Macro || r.External) break;
                        count++;
                        if (target != null) sb.Append(LinkOpen).Append(target(r)).Append(LinkName);
                        break;
                    case HelpRunKind.LinkEnd: if (target != null) sb.Append(LinkClose); break;
                }
            }
        }
        int g0 = cols[0].Gap, g1 = n > 1 ? cols[1].Gap : cols[0].Gap;
        int start = g0 / 2 - g1 / 2;
        var widths = new List<int>();
        int sum = 0, edge = 0;
        for (int i = 0; i < n; i++)
        {
            sum += cols[i].Width + cols[i].Gap;
            int next = (10 * sum + g0 + g1) / 20;
            widths.Add(next - (i == 0 ? 0 : edge) - start);
            edge = next;
        }
        // every cell is a span, the last one too, and each was written with its indent
        return (Cells(cells), Stops(start, widths, _ => true));
    }

    /// <summary>
    /// Finds each jump of the marked text in the plain one. The two were made the same way, so they
    /// agree character for character apart from the markers and any white space a trim could not
    /// reach past a marker. A jump's span is where its words fall, with no white space at either end.
    /// </summary>
    private static List<(int Start, int Length, int To)> Place(string marked, string plain, string title, List<string> misplaced)
    {
        var found = new List<(int, int, int)>();
        int j = 0, start = -1, to = -1;
        for (int i = 0; i < marked.Length; i++)
        {
            char c = marked[i];
            if (c == LinkOpen)
            {
                int end = marked.IndexOf(LinkName, i);
                to = int.Parse(marked.AsSpan(i + 1, end - i - 1));
                i = end;
                if (start >= 0) misplaced.Add($"{title}: a link inside another");
                start = j;
                continue;
            }
            if (c == LinkClose)
            {
                if (start < 0) { misplaced.Add($"{title}: a link end with no start"); continue; }
                int s = start, e = j;
                while (s < e && char.IsWhiteSpace(plain[s])) s++;
                while (e > s && char.IsWhiteSpace(plain[e - 1])) e--;
                if (e > s) found.Add((s, e - s, to));
                else misplaced.Add($"{title}: an empty link");
                start = -1;
                continue;
            }
            if (j < plain.Length && plain[j] == c) { j++; continue; }
            if (char.IsWhiteSpace(c)) continue;
            misplaced.Add($"{title}: the marked and plain texts part at {j}");
            return found;
        }
        if (start >= 0) misplaced.Add($"{title}: a link with no end");
        if (j != plain.Length) misplaced.Add($"{title}: the marked text ends early, at {j} of {plain.Length}");
        return found;
    }
}
