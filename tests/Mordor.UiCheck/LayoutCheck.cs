using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using Mordor.App;

/// <summary>
/// Is the text all there, and in its weight? Four checks the readability pass (<see cref="ContrastCheck"/>) runs on
/// every window it opens, Light and Dark, beside the contrast:
/// <list type="bullet">
/// <item><b>glyph</b> - a character no font on this machine can draw. The text is weighed as Avalonia
/// shaped it for the control: each run of it carries the face it was given, the control's own or the
/// one <see cref="FontManager.TryMatchCharacter"/> found as a fallback, and a character that face has
/// no glyph for is drawn as the face's empty box (glyph 0). (The dungeon's arrow buttons, U+2190 and
/// U+2192, came out as dashes on Linux CI: the face had them, too small to read. Away from Windows
/// they and the title bars' symbols are drawn as shapes now, <see cref="Glyphs"/>, checked in
/// <see cref="SelfTest"/>.)</item>
/// <item><b>clipped</b> - a text control whose text needs more room than its bounds give it: text on one
/// line wider than the control, wrapped text taller than it. Text that is trimmed on purpose (an
/// ellipsis) is left out, and so is a text box's, which scrolls.</item>
/// <item><b>overflow</b> - a visible control that reaches past its window's client area: the top-level
/// window's, or an MDI child's (the game's windows inside the town's frame). Only the outermost
/// control of a chain that overflows is named, and what lies inside a scroller is left to the scroller.</item>
/// <item><b>weight</b> - text set bold drawn in a face that is not bold, or set plain and drawn bold
/// (<see cref="WrongWeight"/>): the face each shaped run was given, weighed as the platform made it,
/// against the weight the run asked for. Avalonia 11.3 swapped the two for Microsoft Sans Serif and
/// Cascadia Mono, and nothing here saw it.</item>
/// </list>
/// Each finding names the window, the theme, the control and the numbers. The ones already known when
/// the checks were written are listed in <see cref="KnownFindings"/>, each with its reason: they print
/// as "known" and do not fail; anything else does.
/// </summary>
static class LayoutCheck
{
    /// <summary>A layout finding: its kind (glyph, clipped, overflow), the control as named, and what was measured.</summary>
    public sealed record Finding(string Kind, string Control, string Numbers)
    {
        public override string ToString() => $"{Kind}: {Control}: {Numbers}";
    }

    /// <summary>
    /// A finding seen when the checks were written, kept visible rather than fixed (no game window is
    /// changed to make a check pass). <see cref="Window"/> and <see cref="Control"/> are regular
    /// expressions over the window's name in the pass (without the theme) and the control as a finding
    /// names it; <see cref="Os"/>, when set, limits the entry to one system ("Windows", "Linux", "macOS").
    /// </summary>
    public sealed record Known(string Kind, string Window, string Control, string Reason, string? Os = null)
    {
        public bool Matches(string kind, string window, Finding f) =>
            Kind == kind && Regex.IsMatch(window, Window) && Regex.IsMatch(f.Control, Control)
            && (Os == null || Os == OsName);
    }

    public static string OsName => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";

    /// <summary>
    /// The findings known on Windows when these checks were written (2026-11-27), each with the reason
    /// it is let stand for now. Triage belongs to the owner: an entry goes when its window is put right.
    /// A new finding - another window, another control, another system - fails the pass.
    /// </summary>
    public static readonly Known[] KnownFindings =
    {
        new("clipped", "^the town", @"^TextBlock ""Automap \(Level \d+\)"" \(in ""Automap \(Level \d+\)""\)$",
            "the Automap waits in town rolled up to an icon's narrow bar, its caption cut short as a Windows 3.1 icon's title is"),
    };

    /// <summary>How far, in device-independent pixels, text or a control may run over before it counts: rounding, not layout.</summary>
    const double Slack = 1.0;

    /// <summary>Every finding under <paramref name="root"/>, a window or the town with its children.</summary>
    public static List<Finding> Find(Visual root)
    {
        var found = new List<Finding>();
        found.AddRange(MissingGlyphs(root));
        found.AddRange(ClippedText(root));
        found.AddRange(Overflow(root));
        found.AddRange(WrongWeight(root));
        return found;
    }

    /// <summary>The known entry a finding matches in <paramref name="window"/>, or null for a new one.</summary>
    public static Known? KnownFor(string window, Finding f) => KnownFindings.FirstOrDefault(k => k.Matches(f.Kind, window, f));

    // ---------------------------------------------------------------- the text controls

    /// <summary>A shown text control: what it says, and the layout Avalonia drew it with.</summary>
    record Shown(Control Control, string Text, TextLayout Layout);

    static IEnumerable<Shown> Texts(Visual root)
    {
        foreach (var v in root.GetVisualDescendants())
        {
            if (v is not Control c || !c.IsEffectivelyVisible || c.Bounds.Width <= 0 || c.Bounds.Height <= 0) continue;
            switch (v)
            {
                case TextBlock t:
                {
                    string? text = t.Inlines is { Count: > 0 } i ? i.Text : t.Text;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    yield return new Shown(t, text, t.TextLayout);
                    break;
                }
                case TextPresenter p when !string.IsNullOrWhiteSpace(p.Text):
                    // a password box shows its mask, not the text
                    if (p.PasswordChar != default) continue;
                    yield return new Shown(p, p.Text!, p.TextLayout);
                    break;
                case AccessCaption a when !string.IsNullOrWhiteSpace(a.Text):
                    // drawn with a FormattedText of its own: the same text in the same face, shaped the same way
                    yield return new Shown(a, a.Text, new TextLayout(a.Text, TypefaceOf(a), a.GetValue(TextElement.FontSizeProperty), null));
                    break;
            }
        }
    }

    static Typeface TypefaceOf(Control c) => new(c.GetValue(TextElement.FontFamilyProperty), c.GetValue(TextElement.FontStyleProperty),
                                                 c.GetValue(TextElement.FontWeightProperty), c.GetValue(TextElement.FontStretchProperty));

    // ---------------------------------------------------------------- glyph

    /// <summary>
    /// Characters drawn with no glyph. Each shaped run of the control's layout holds the face Avalonia
    /// chose for it: the control's own face where that has the character, else the face
    /// FontManager.TryMatchCharacter gave as a fallback, else - nothing on the machine having it - the
    /// control's own face again, whose glyph 0 (an empty box, or on some faces a dash) is what is drawn.
    /// A character counts as missing when the run's face has no glyph for it.
    /// </summary>
    internal static IEnumerable<Finding> MissingGlyphs(Visual root)
    {
        foreach (var s in Texts(root))
        {
            var missing = new SortedDictionary<int, string>();
            foreach (var line in s.Layout.TextLines)
                foreach (var run in line.TextRuns)
                {
                    if (run is not ShapedTextRun shaped) continue;
                    var face = shaped.ShapedBuffer.GlyphTypeface;
                    string text = shaped.Text.ToString();
                    for (int i = 0; i < text.Length; i++)
                    {
                        int cp = CodePoint(text, i, out int width);
                        i += width - 1;
                        if (cp < 0x20 || cp is >= 0x7F and <= 0x9F || cp <= 0xFFFF && char.IsWhiteSpace((char)cp)) continue;
                        if (cp is 0xAD or 0x200B or 0x200C or 0x200D or 0xFEFF) continue;   // shown as nothing on purpose
                        // macOS's Last Resort font has a glyph for every code point: the box that says no font
                        // has it. Fallback lands there for anything nothing else draws, so a character it
                        // draws is missing all the same (the first macOS run found the self-test passing it)
                        if (!face.CharacterToGlyphMap.TryGetGlyph(cp, out ushort glyph) || glyph == 0 || IsLastResort(face.FamilyName))
                            missing[cp] = face.FamilyName;
                    }
                }
            if (missing.Count > 0)
                yield return new Finding("glyph", Name(s.Control, s.Text),
                    "no glyph for " + string.Join(", ", missing.Select(m => $"U+{m.Key:X4} '{char.ConvertFromUtf32(m.Key)}' in {m.Value}")));
        }
    }

    /// <summary>macOS's Last Resort face (".LastResort", "LastResort"): only ever the "no font has this" box.</summary>
    static bool IsLastResort(string family) => family.TrimStart('.').StartsWith("LastResort", StringComparison.OrdinalIgnoreCase);

    static int CodePoint(string s, int i, out int width)
    {
        width = 1;
        if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { width = 2; return char.ConvertToUtf32(s[i], s[i + 1]); }
        return s[i];
    }

    // ---------------------------------------------------------------- weight

    /// <summary>
    /// Text drawn in the wrong weight: set bold and drawn in a face that is not bold, or set plain
    /// (anything under SemiBold) and drawn bold. Each shaped run carries the weight it asked for (its
    /// properties' typeface, the control's own or a Run's) and the face it was given; the face is
    /// weighed as the platform made it (<see cref="FaceWeight"/>), not by the weight Avalonia files it
    /// under, since that is where a mix-up hides. Avalonia 11.3 filed the face DirectWrite returns
    /// for a bold Microsoft Sans Serif (no real bold) or Cascadia Mono (a variable face) under the
    /// plain weight the first time bold was asked for: after that plain text was drawn bold, and bold
    /// text in Avalonia's own thickening of a plain (or, for Cascadia, extra-light) face. A bold of
    /// Avalonia's own making counts as bold for plain text (it must not be there) and not for bold
    /// text (it is the faint stand-in).
    /// </summary>
    internal static IEnumerable<Finding> WrongWeight(Visual root)
    {
        foreach (var s in Texts(root))
        {
            var wrong = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var run in WeighedRuns(s))
            {
                bool askedBold = run.Asked >= FontWeight.SemiBold;
                var (family, weight, made) = FaceWeight(run.Face);
                // Avalonia's thickening is the faint stand-in only where a real bold of the family is to be
                // had: Avalonia 11.3 drew it over Windows' bold of MS Sans Serif. A machine with no bold of
                // the family at all - a Mac, whose Microsoft Sans Serif has none - can draw bold no other way
                bool drawnBold = askedBold ? weight >= (int)FontWeight.SemiBold || made && !HasRealBold(family)
                                           : weight >= (int)FontWeight.SemiBold || made;
                if (askedBold == drawnBold) continue;
                wrong.Add($"set {run.Asked}, drawn in {family} {weight}{(made ? " thickened by Avalonia" : "")}");
            }
            if (wrong.Count > 0)
                yield return new Finding("weight", Name(s.Control, s.Text), string.Join("; ", wrong));
        }
    }

    /// <summary>
    /// Whether this machine has a bold face of the family, as the system's font manager answers for a
    /// bold of it (a made-up one included, as DirectWrite makes for a family with none): weighed 600
    /// or more. Asked once per family.
    /// </summary>
    static bool HasRealBold(string family)
    {
        lock (RealBold)
        {
            if (RealBold.TryGetValue(family, out bool has)) return has;
            using var bold = SkiaSharp.SKFontManager.Default.MatchFamily(family, SkiaSharp.SKFontStyle.Bold);
            has = bold != null && bold.FontWeight >= (int)FontWeight.SemiBold
                  && string.Equals(bold.FamilyName, family, StringComparison.OrdinalIgnoreCase);
            RealBold[family] = has;
            return has;
        }
    }

    static readonly Dictionary<string, bool> RealBold = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A shaped run of text with a letter or a figure in it: the weight it asked for and the face it got.</summary>
    record WeighedRun(FontWeight Asked, GlyphTypeface Face);

    static IEnumerable<WeighedRun> WeighedRuns(Shown s)
    {
        foreach (var line in s.Layout.TextLines)
            foreach (var run in line.TextRuns)
            {
                // a weight shows in letters and figures; a run of spaces or symbols alone is left out
                if (run is not ShapedTextRun shaped || run.Properties is null) continue;
                if (!HasLetter(shaped.Text.Span)) continue;
                yield return new WeighedRun(run.Properties.Typeface.Weight, shaped.ShapedBuffer.GlyphTypeface);
            }
    }

    static bool HasLetter(ReadOnlySpan<char> text)
    {
        foreach (char c in text) if (char.IsLetterOrDigit(c)) return true;
        return false;
    }

    /// <summary>
    /// How many runs of text under <paramref name="root"/> the weight check weighed, set bold and set
    /// plain, and the controls they are in: so a pass can say it reached the text it is meant to.
    /// </summary>
    public static (int Bold, int Plain, List<Control> Controls) Weighed(Visual root)
    {
        int bold = 0, plain = 0;
        var controls = new List<Control>();
        foreach (var s in Texts(root))
        {
            bool any = false;
            foreach (var run in WeighedRuns(s))
            {
                if (run.Asked >= FontWeight.SemiBold) bold++; else plain++;
                any = true;
            }
            if (any) controls.Add(s.Control);
        }
        return (bold, plain, controls);
    }

    /// <summary>
    /// A face's weight as drawn: the platform's own face (Skia's typeface, its weight as the system
    /// font manager gave it, DirectWrite's made-up bold included) where it can be reached, else the
    /// weight its platform typeface reports; and whether Avalonia thickens it itself. Avalonia 12 counts a
    /// bold the platform made (DirectWrite's, for a family with none) among the face's FontSimulations
    /// without applying it again, so a bold simulation is Avalonia's own only over a face that is not bold.
    /// </summary>
    internal static (string Family, int Weight, bool Made) FaceWeight(GlyphTypeface face)
    {
        var sk = PlatformFace(face.PlatformTypeface);
        int weight = sk?.FontWeight ?? (int)face.PlatformTypeface.Weight;
        bool made = (face.FontSimulations & FontSimulations.Bold) != 0 && weight < (int)FontWeight.SemiBold;
        return (sk?.FamilyName ?? face.FamilyName, weight, made);
    }

    /// <summary>The Skia typeface behind a platform typeface: a field of it, or of what it holds, a level down.</summary>
    internal static SkiaSharp.SKTypeface? PlatformFace(object face)
    {
        const System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        SkiaSharp.SKTypeface? Search(object o, int depth)
        {
            if (!seen.Add(o)) return null;
            var fields = o.GetType().GetFields(Fields);
            foreach (var f in fields)
                if (f.GetValue(o) is SkiaSharp.SKTypeface t) return t;
            if (depth == 0) return null;
            foreach (var f in fields)
            {
                if (f.FieldType.IsValueType || f.FieldType == typeof(string)) continue;
                if (f.GetValue(o) is { } v && v.GetType().Assembly.GetName().Name?.StartsWith("Avalonia") == true
                    && Search(v, depth - 1) is { } found) return found;
            }
            return null;
        }
        return Search(face, 1);
    }

    // ---------------------------------------------------------------- clipped

    /// <summary>
    /// Text that needs more room than its control has. One line of text (no wrapping) is weighed by
    /// its widest line against the control's width less its padding; wrapped text is laid out again
    /// at that width with no limit on its height, and weighed by that height against the control's.
    /// Trimmed text and text boxes are left out: both are made to hold more text than they show.
    /// </summary>
    internal static IEnumerable<Finding> ClippedText(Visual root)
    {
        foreach (var s in Texts(root))
        {
            if (s.Control is not TextBlock t) continue;   // a text box scrolls; an AccessCaption sizes itself to its text
            if (t.TextTrimming != null && t.TextTrimming != TextTrimming.None) continue;
            if (t.MaxLines > 0) continue;
            var pad = t.Padding;
            double haveW = t.Bounds.Width - pad.Left - pad.Right, haveH = t.Bounds.Height - pad.Top - pad.Bottom;
            double needW = s.Layout.TextLines.Count == 0 ? 0 : s.Layout.TextLines.Max(l => l.Width);
            if (t.TextWrapping == TextWrapping.NoWrap)
            {
                if (needW > haveW + Slack)
                    yield return new Finding("clipped", Name(t, s.Text), $"needs {needW:0.#} px wide, has {haveW:0.#}");
                continue;
            }
            // wrapped: laid out afresh, unlimited in height (the control's own layout stops at its height),
            // and weighed to its last line with anything on it: blank lines after the text hide nothing
            if (t.Inlines is { Count: > 0 }) continue;
            var free = new TextLayout(s.Text, TypefaceOf(t), t.FontSize, null, t.TextAlignment, t.TextWrapping,
                                      textTrimming: null, textDecorations: null, flowDirection: t.FlowDirection,
                                      maxWidth: Math.Max(0, haveW), maxHeight: double.PositiveInfinity,
                                      lineHeight: t.LineHeight, letterSpacing: t.LetterSpacing);
            var lines = free.TextLines;
            int last = lines.Count - 1;
            while (last >= 0 && Blank(s.Text, lines[last])) last--;
            double needH = lines.Take(last + 1).Sum(l => l.Height);
            double wideW = last < 0 ? 0 : lines.Take(last + 1).Max(l => l.Width);
            if (needH > haveH + Slack)
                yield return new Finding("clipped", Name(t, s.Text), $"needs {needH:0.#} px high ({last + 1} lines), has {haveH:0.#}");
            else if (wideW > haveW + Slack)
                yield return new Finding("clipped", Name(t, s.Text), $"a word needs {wideW:0.#} px wide, has {haveW:0.#}");
        }
    }

    static bool Blank(string text, TextLine line)
    {
        int from = Math.Clamp(line.FirstTextSourceIndex, 0, text.Length);
        int to = Math.Clamp(line.FirstTextSourceIndex + line.Length, from, text.Length);
        return string.IsNullOrWhiteSpace(text[from..to]);
    }

    // ---------------------------------------------------------------- overflow

    /// <summary>
    /// Controls reaching past their window's client area. The windows are the top-level window and each
    /// MDI child in it (its client area is below its title bar, inside its frame). A control is weighed
    /// by the part of it that is drawn: cut to every ancestor that clips its children, up to its window.
    /// Inside a scroller only the scroller is weighed, and inside a control that overflows only that
    /// control is named. The MDI children themselves may stand past the town's frame: the frame scrolls.
    /// </summary>
    internal static IEnumerable<Finding> Overflow(Visual root)
    {
        var top = root as TopLevel ?? TopLevel.GetTopLevel(root);
        if (top == null) yield break;
        // each window: what is weighed (the top level's own controls; an MDI child's body), and its client area
        var windows = new List<(Visual Window, Visual From, Rect Client, string Title)> { (top, top, new Rect(top.ClientSize), "the window") };
        foreach (var m in top.GetVisualDescendants().OfType<MdiChild>())
            if (m.IsEffectivelyVisible && m.Bounds.Width > 0 && ClientOf(m) is var (body, client))
                windows.Add((m, body, client, $"\"{m.Title}\""));
        foreach (var (window, from, client, title) in windows)
        {
            var flagged = new HashSet<Visual>();
            foreach (var v in from.GetVisualDescendants())
            {
                if (v is not Control c || !c.IsEffectivelyVisible || c.Bounds.Width <= 0 || c.Bounds.Height <= 0) continue;
                // an MDI child, and all in it, belongs to the child's own turn (and the town's frame scrolls)
                if (window is TopLevel && (c is MdiChild || c.GetVisualAncestors().TakeWhile(a => a != window).OfType<MdiChild>().Any())) continue;
                var parent = v.GetVisualParent();
                // inside a control already named, or inside a scroller: the outer one speaks for it
                if (parent != null && flagged.Contains(parent)) { flagged.Add(v); continue; }
                if (parent != null && InsideScroller(parent, window)) continue;
                if (Drawn(c, window) is not Rect drawn) continue;
                double right = drawn.Right - client.Right, bottom = drawn.Bottom - client.Bottom;
                double left = client.Left - drawn.Left, up = client.Top - drawn.Top;
                if (right <= Slack && bottom <= Slack && left <= Slack && up <= Slack) continue;
                flagged.Add(v);
                var by = new List<string>();
                if (right > Slack) by.Add($"{right:0.#} px past the right");
                if (bottom > Slack) by.Add($"{bottom:0.#} px past the bottom");
                if (left > Slack) by.Add($"{left:0.#} px past the left");
                if (up > Slack) by.Add($"{up:0.#} px past the top");
                string name = Name(c, TextOf(c));
                if (window is TopLevel) name += " in the window";
                yield return new Finding("overflow", name,
                    $"{string.Join(", ", by)} (drawn {drawn.Width:0.#}x{drawn.Height:0.#} at {drawn.X:0.#},{drawn.Y:0.#}; the client area {client.Width:0.#}x{client.Height:0.#} at {client.X:0.#},{client.Y:0.#})");
            }
        }
    }

    /// <summary>
    /// An MDI child's body (the ContentControl that holds the form) and its client area in the child's
    /// coordinates: the panel under the title bar, inside the frame, that the body shares with the grip.
    /// </summary>
    static (Visual Body, Rect Client)? ClientOf(MdiChild m)
    {
        if (m.Body is not Control form) return null;
        var content = form.GetVisualAncestors().OfType<ContentControl>()
            .FirstOrDefault(cc => cc.GetVisualParent() is Panel p && p.GetVisualParent() is Grid g && g.GetVisualParent() == m);
        if (content?.GetVisualParent() is not Panel panel || panel.TranslatePoint(new Point(0, 0), m) is not Point at) return null;
        return (content, new Rect(at, panel.Bounds.Size));
    }

    static bool InsideScroller(Visual v, Visual window)
    {
        for (Visual? p = v; p != null && p != window; p = p.GetVisualParent())
            if (p is ScrollContentPresenter) return true;
        return false;
    }

    /// <summary>The part of <paramref name="c"/> that is drawn, in its window's coordinates: cut to each clipping ancestor below the window.</summary>
    static Rect? Drawn(Control c, Visual window)
    {
        if (c.TransformToVisual(window) is not Matrix m) return null;
        var rect = new Rect(c.Bounds.Size).TransformToAABB(m);
        for (var p = c.GetVisualParent(); p != null && p != window; p = p.GetVisualParent())
        {
            if (!p.ClipToBounds || p.TransformToVisual(window) is not Matrix pm) continue;
            rect = rect.Intersect(new Rect(p.Bounds.Size).TransformToAABB(pm));
            if (rect.Width <= 0 || rect.Height <= 0) return null;
        }
        return rect;
    }

    // ---------------------------------------------------------------- naming

    static string? TextOf(Control c) => c switch
    {
        TextBlock t => t.Inlines is { Count: > 0 } i ? i.Text : t.Text,
        ContentControl { Content: string s } => s,
        Avalonia.Controls.Primitives.HeaderedContentControl { Header: string h } => h,
        _ => null,
    };

    /// <summary>The control as a finding names it: its type, its text, and where it is (its name and its named ancestors).</summary>
    internal static string Name(Control c, string? text)
    {
        var names = new List<string>();
        if (!string.IsNullOrEmpty(c.Name) && !c.Name.StartsWith("PART_")) names.Add(c.Name);
        for (var p = c.GetVisualParent(); p != null && names.Count < 2; p = p.GetVisualParent())
        {
            if (p is StyledElement s && !string.IsNullOrEmpty(s.Name) && !s.Name.StartsWith("PART_")) names.Add(s.Name);
            if (p is MdiChild) break;
        }
        // a template's text: the control it is the text of (a button's caption)
        string owner = c.TemplatedParent is Control tp && tp.GetType() != c.GetType() ? $" of a {tp.GetType().Name}" : "";
        string what = c.GetType().Name + owner;
        if (!string.IsNullOrWhiteSpace(text)) what += $" \"{Short(text)}\"";
        string? child = c.GetVisualAncestors().OfType<MdiChild>().FirstOrDefault()?.Title;
        string where = names.Count > 0 ? string.Join(" < ", names) : "";
        if (child != null) where += (where.Length > 0 ? " " : "") + $"in \"{child}\"";
        return where.Length > 0 ? $"{what} ({where})" : what;
    }

    static string Short(string s)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= 40 ? s : s[..37] + "...";
    }

    // ---------------------------------------------------------------- the checks of the checks

    /// <summary>
    /// Each check is shown a window made to fail it, and one that should pass: a character from the
    /// private-use plane no face has (how the Linux arrows look to the check: no face on the machine
    /// with the glyph), a label narrower than its text, a wrapped one too short for its lines, and a
    /// panel standing past the window's edge. Returns how many failed.
    /// </summary>
    public static int SelfTest(Action pump)
    {
        int failed = 0;
        void Say(string what, bool ok) { Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  layout checks: {what}"); if (!ok) failed++; }
        var canvas = new Canvas { Width = 300, Height = 200, Background = Brushes.White };
        var noGlyph = new TextBlock { Text = "Arrow \U000F0001 here", Name = "NoGlyph" };
        var arrows = new TextBlock { Text = "plain text", Name = "Plain" };
        var narrow = new TextBlock { Text = "A caption much wider than its label", Width = 40, Name = "Narrow" };
        var wrapped = new TextBlock { Text = "Several words that wrap onto many lines in a short box", Width = 60, Height = 20, TextWrapping = TextWrapping.Wrap, Name = "Short" };
        var trimmed = new TextBlock { Text = "A caption trimmed on purpose, with an ellipsis", Width = 40, TextTrimming = TextTrimming.CharacterEllipsis, Name = "Trimmed" };
        var wide = new Border { Width = 120, Height = 30, Background = Brushes.Gray, Name = "Wide" };
        Canvas.SetTop(noGlyph, 0); Canvas.SetTop(arrows, 20); Canvas.SetTop(narrow, 40); Canvas.SetTop(wrapped, 60); Canvas.SetTop(trimmed, 90);
        Canvas.SetLeft(wide, 250); Canvas.SetTop(wide, 120);
        canvas.Children.AddRange(new Control[] { noGlyph, arrows, narrow, wrapped, trimmed, wide });
        var w = new Window { Width = 300, Height = 200, Content = canvas };
        try
        {
            w.Show(); pump();
            var found = Find(w);
            bool Has(string kind, string name) => found.Any(f => f.Kind == kind && f.Control.Contains($"({name}"));
            Say("a character no face can draw is found", Has("glyph", "NoGlyph"));
            Say("text every face draws passes", !found.Any(f => f.Control.Contains("(Plain")));
            Say("text wider than its label is found", Has("clipped", "Narrow"));
            Say("wrapped text taller than its label is found", Has("clipped", "Short"));
            Say("trimmed text passes", !found.Any(f => f.Control.Contains("(Trimmed")));
            Say("a panel past the window's edge is found", Has("overflow", "Wide"));
            Say("and nothing else is", found.Count == 4);
            if (found.Count != 4) foreach (var f in found) Console.WriteLine("          " + f);
        }
        catch (Exception ex) { Say($"could not run: {ex.GetType().Name}: {ex.Message}", false); }
        finally { try { w.Close(); pump(); } catch { } }
        failed += GlyphShapes(pump);
        failed += WeightSelfTest(pump);
        return failed;
    }

    /// <summary>
    /// The button symbols drawn as shapes away from Windows (<see cref="Glyphs"/>), forced on here so
    /// Windows checks them too: each symbol the port's buttons use has a shape, the shape is drawn in
    /// the button's own text colour (and follows it), nothing about it is a character the layout check
    /// could find missing, and with the shapes off the button keeps its character. Returns how many failed.
    /// </summary>
    static int GlyphShapes(Action pump)
    {
        int failed = 0;
        void Say(string what, bool ok) { Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  button symbols: {what}"); if (!ok) failed++; }
        string[] symbols = { "←", "↑", "→", "↓", "✕", "□", "❐", "▴" };
        var was = Glyphs.Override;
        Window? w = null;
        try
        {
            Glyphs.Override = false;
            Say("with the shapes off (Windows) a button keeps its character", symbols.All(s => Glyphs.Content(s) as string == s));
            Glyphs.Override = true;
            Say("every symbol has a shape", symbols.All(Glyphs.Has));
            Say("a symbol with no shape stays a character", Glyphs.Content("▲") as string == "▲");
            var buttons = symbols.Select(s => new Button { Content = Glyphs.Content(s), Width = 20, Height = 20, Foreground = Brushes.DarkRed }).ToList();
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
            panel.Children.AddRange(buttons);
            w = new Window { Width = 300, Height = 60, Content = panel };
            w.Show(); pump();
            var paths = buttons.Select(b => b.Content as Avalonia.Controls.Shapes.Path).ToList();
            Say("each is a shape, shown", paths.All(p => p != null && p.IsEffectivelyVisible && p.Bounds.Width > 0));
            Say("drawn in the button's text colour", paths.All(p => p!.Stroke == Brushes.DarkRed || p.Fill == Brushes.DarkRed));
            buttons[0].Foreground = Brushes.Navy; pump();
            Say("and following it", paths[0]!.Stroke == Brushes.Navy);
            Say("no character in them for a face to miss", !Find(w).Any(f => f.Kind == "glyph"));
        }
        catch (Exception ex) { Say($"could not run: {ex.GetType().Name}: {ex.Message}", false); }
        finally { Glyphs.Override = was; try { w?.Close(); pump(); } catch { } }
        return failed;
    }

    /// <summary>
    /// The weight check shown text in its right weight and text in the wrong one. Right: bold and
    /// plain in the faces the game draws in (Microsoft Sans Serif, Cascadia Mono, MS Sans Serif's
    /// stand-in, Times New Roman), the bold asked for first, as the 11.3 mix-up needed. Wrong: two
    /// families of a font collection made here (<see cref="ForcedFaces"/>) that hand back one face
    /// whatever weight is asked, the plain one for "Forced Plain" and a bold one for "Forced Bold":
    /// bold text in the first and plain text in the second must be found. Returns how many failed.
    /// </summary>
    static int WeightSelfTest(Action pump)
    {
        int failed = 0;
        void Say(string what, bool ok) { Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  weight check: {what}"); if (!ok) failed++; }
        Window? w = null;
        try
        {
            var forced = ForcedFaces.Add();
            var plainFace = forced.Plain;
            var boldFace = forced.Bold;
            Say($"the forced faces are a plain one and a bold one ({Describe(plainFace)}; {Describe(boldFace)})",
                plainFace != null && FaceWeight(plainFace).Weight < 600 && !FaceWeight(plainFace).Made
                && boldFace != null && FaceWeight(boldFace).Weight >= 600);
            var stack = new StackPanel { Background = Brushes.White };
            var right = new List<TextBlock>();
            foreach (string family in new[] { "Microsoft Sans Serif", "Cascadia Mono", StandInFamily(), "Times New Roman" })
            {
                // bold first: the 11.3 mix-up filed the first bold it was asked for as the plain face
                var bold = new TextBlock { Text = "Bold Caption 12", FontFamily = new FontFamily(family), FontWeight = FontWeight.Bold, Name = "RightBold" };
                var plain = new TextBlock { Text = "Plain caption 34", FontFamily = new FontFamily(family), FontWeight = FontWeight.Normal, Name = "RightPlain" };
                right.Add(bold); right.Add(plain);
            }
            var boldInPlain = new TextBlock { Text = "Bold asked", FontFamily = new FontFamily(ForcedFaces.PlainFamily), FontWeight = FontWeight.Bold, Name = "BoldInPlain" };
            var plainInBold = new TextBlock { Text = "Plain asked", FontFamily = new FontFamily(ForcedFaces.BoldFamily), FontWeight = FontWeight.Normal, Name = "PlainInBold" };
            var plainInPlain = new TextBlock { Text = "Plain in plain", FontFamily = new FontFamily(ForcedFaces.PlainFamily), FontWeight = FontWeight.Normal, Name = "PlainInPlain" };
            // a bold Run inside plain text, and plain text inside a bold block: each run weighed by its own weight
            var mixed = new TextBlock { FontFamily = new FontFamily(ForcedFaces.PlainFamily), Name = "MixedRuns" };
            mixed.Inlines!.Add(new Run("plain words "));
            mixed.Inlines.Add(new Run("bold words") { FontWeight = FontWeight.Bold });
            foreach (var t in right.Where(t => t.FontWeight == FontWeight.Bold)) stack.Children.Add(t);
            w = new Window { Width = 300, Height = 400, Content = stack };
            w.Show(); pump();
            // the plain ones after the bold ones are drawn, as a window opened later would be
            foreach (var t in right.Where(t => !stack.Children.Contains(t))) stack.Children.Add(t);
            stack.Children.AddRange(new Control[] { boldInPlain, plainInBold, plainInPlain, mixed });
            pump();
            var found = WrongWeight(w).ToList();
            bool Has(string name) => found.Any(f => f.Control.Contains($"({name}"));
            Say("bold drawn in a plain face is found", Has("BoldInPlain"));
            Say("plain drawn in a bold face is found", Has("PlainInBold"));
            Say("a bold Run drawn in a plain face is found", found.Any(f => f.Control.Contains("(MixedRuns") && f.Numbers.Contains("set Bold") && !f.Numbers.Contains("set Normal")));
            Say("plain in a plain face passes", !Has("PlainInPlain"));
            Say("bold and plain in the game's faces pass, bold asked first", !Has("RightBold") && !Has("RightPlain"));
            var weighed = Weighed(w);
            Say($"every caption weighed ({weighed.Bold} bold runs, {weighed.Plain} plain)", weighed.Controls.Count == stack.Children.Count);
            Say("and nothing else is found", found.Count == 3);
            if (found.Count != 3 || Has("RightBold") || Has("RightPlain")) foreach (var f in found) Console.WriteLine("          " + f);
        }
        catch (Exception ex) { Say($"could not run: {ex.GetType().Name}: {ex.Message}", false); }
        finally { try { w?.Close(); pump(); } catch { } }
        return failed;
    }

    /// <summary>The family the game draws MS Sans Serif in (UiFonts.Family, its regular and bold from two faces).</summary>
    static string StandInFamily() => typeof(App).Assembly.GetType("Mordor.App.UiFonts")!.GetMethod("Family")!
        .Invoke(null, new object[] { Mordor.Core.DesignFont.NonProFace })!.ToString()!;

    static string Describe(GlyphTypeface? face) => face == null ? "none" : FaceWeight(face) is var (family, weight, made) ? $"{family} {weight}{(made ? " thickened" : "")}" : "";

    /// <summary>
    /// A font collection for the weight check's self-test: "Forced Plain" is the system's plain sans
    /// face and "Forced Bold" a real bold one, whatever weight is asked of either.
    /// </summary>
    sealed class ForcedFaces : Avalonia.Media.Fonts.IFontCollection
    {
        public const string PlainName = "Forced Plain", BoldName = "Forced Bold";
        static readonly Uri CollectionKey = new("fonts:WeightSelfTest");
        public const string PlainFamily = "fonts:WeightSelfTest#" + PlainName, BoldFamily = "fonts:WeightSelfTest#" + BoldName;
        static ForcedFaces? _added;

        public GlyphTypeface? Plain { get; private set; }
        public GlyphTypeface? Bold { get; private set; }

        public static ForcedFaces Add()
        {
            if (_added != null) return _added;
            var c = new ForcedFaces();
            // the plain face: the sans the game draws in; the bold one a real bold (Tahoma's on Windows,
            // the default family's elsewhere), whichever the system has with a bold of its own
            c.Plain = Get("Microsoft Sans Serif", FontWeight.Normal) ?? Get(FontFamily.DefaultFontFamilyName, FontWeight.Normal);
            c.Bold = new[] { "Tahoma", "Arial", FontFamily.DefaultFontFamilyName }.Select(f => Get(f, FontWeight.Bold))
                .FirstOrDefault(g => g != null && FaceWeight(g).Weight >= 600 && !FaceWeight(g).Made);
            FontManager.Current.AddFontCollection(c);
            return _added = c;
        }

        static GlyphTypeface? Get(string family, FontWeight weight) =>
            FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(family), FontStyle.Normal, weight), out var g) ? g : null;

        readonly FontFamily[] _families = { new(PlainName), new(BoldName) };
        public Uri Key => CollectionKey;
        public bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GlyphTypeface? glyphTypeface)
        {
            glyphTypeface = familyName == PlainName ? Plain : familyName == BoldName ? Bold : null;
            return glyphTypeface != null;
        }

        public bool TryGetNearestMatch(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
                                       [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GlyphTypeface? glyphTypeface) =>
            TryGetGlyphTypeface(familyName, style, weight, stretch, out glyphTypeface);

        public bool TryGetFamilyTypefaces(string familyName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IReadOnlyList<Typeface>? familyTypefaces)
        {
            familyTypefaces = null;
            return false;
        }

        /// <summary>None: the one face is the point.</summary>
        public bool TryCreateSyntheticGlyphTypeface(GlyphTypeface glyphTypeface, FontStyle style, FontWeight weight, FontStretch stretch,
                                                    [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out GlyphTypeface? syntheticGlyphTypeface)
        {
            syntheticGlyphTypeface = null;
            return false;
        }

        public bool TryMatchCharacter(int codepoint, FontStyle style, FontWeight weight, FontStretch stretch,
                                      string? familyName, CultureInfo? culture, out Typeface typeface)
        {
            typeface = default;
            return false;
        }

        public int Count => _families.Length;
        public FontFamily this[int index] => _families[index];
        public IEnumerator<FontFamily> GetEnumerator() => ((IEnumerable<FontFamily>)_families).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _families.GetEnumerator();
        /// <summary>The faces belong to the system collection, which disposes of them.</summary>
        public void Dispose() { }
    }
}
