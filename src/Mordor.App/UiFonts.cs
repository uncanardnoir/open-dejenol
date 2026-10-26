using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The one place the port's game windows get their fonts: the Options window's "Pro" and "NonPro"
/// fonts (MORDOR.INI [Fonts], <see cref="GameOptions"/>) applied to each control's design font by
/// the original's own rule (<see cref="FontRule"/>: sub0784 at load, sub07C5 / sub0760 as a form is
/// shown and resized). A control drawn from a form takes the font its form data gives it
/// (<see cref="DesignFonts"/>); the port's own text on such a form - a mod's column, a caption the
/// port draws another way - takes the form's own font, MS Sans Serif 8.25, the "NonPro" role.
///
/// The zoom of sub07C5 is the port's ScaleTransform over a drawing (ShrinkToFit, the Viewboxes), so
/// the sizes here are a form's at its own size. The fonts are read when a game window is built: the
/// Options window's font buttons are disabled while a character is in play (FRMOPTIO.BAS:330-338),
/// so they can only change between games, and the next town or dungeon window is built with them.
///
/// A list's rows are the font's line height and <see cref="RowPad"/>, so a larger font makes taller
/// rows rather than clipped ones (docs/FIDELITY.md, "The Options fonts are applied").
///
/// Nothing in the game's windows sets a FontFamily or FontSize of its own: selfcheck holds the
/// port's UI source to that, with the port's own windows (the title, Options, Help, the Control
/// Panel, character creation, the mods page) allowed their fonts and listed there.
/// </summary>
internal static class UiFonts
{
    private static GameOptions? _options;

    /// <summary>The Options window's settings the windows are drawn with.</summary>
    public static GameOptions Options => _options ??= GameOptions.Load(TitleWindow.GameDir);

    /// <summary>Draw what is built from now on with these settings (a game window, as it opens).</summary>
    public static void Use(GameOptions o) => _options = o;

    private static GameOptions? _cachedFor;
    private static readonly Dictionary<string, double> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// A measure that depends only on the settings (a list's row pitch): worked out once for the
    /// <see cref="Options"/> in use, and again only when a game window is built with others.
    /// </summary>
    public static double Cached(string key, Func<double> measure)
    {
        var o = Options;
        if (!ReferenceEquals(o, _cachedFor)) { Cache.Clear(); _cachedFor = o; }
        if (Cache.TryGetValue(key, out double v)) return v;
        v = measure();
        Cache[key] = v;
        return v;
    }

    /// <summary>
    /// A face as Avalonia can draw it. MS Sans Serif is a bitmap font it cannot: it is drawn in
    /// Microsoft Sans Serif, Windows' TrueType MS Sans Serif, and its bold in Tahoma Bold
    /// (<see cref="SansStandIn"/>).
    /// </summary>
    public static FontFamily Family(string face)
    {
        if (Families.TryGetValue(face, out var f)) return f;
        string chain = string.Equals(face, DesignFont.NonProFace, StringComparison.OrdinalIgnoreCase)
            ? StandIn()
            : $"{face}, Microsoft Sans Serif, Segoe UI";
        try { f = new FontFamily(chain); } catch { f = new FontFamily("Microsoft Sans Serif, Segoe UI"); }
        Families[face] = f;
        return f;
    }

    private static readonly Dictionary<string, FontFamily> Families = new(StringComparer.Ordinal);

    private static string StandIn() => SansStandIn.EnsureAdded() ? SansStandIn.Family : "Microsoft Sans Serif, Segoe UI";

    /// <summary>
    /// MsgBox's text is Windows' message font, not a form's: no sub0784 reaches it, so the Options
    /// fonts never did. On the Windows the game was made for that is MS Sans Serif 8 points, 11
    /// pixels, drawn here in the port's MS Sans Serif (<see cref="Family"/>), plain.
    /// </summary>
    public const double MessagePixels = 11;

    /// <summary>
    /// A box standing in for MsgBox (OriginalBox, the town's and the dungeon's Tell and Confirm) in
    /// Windows' message font, its buttons too. They had taken their frame's Cascadia Mono.
    /// </summary>
    public static T MessageBox<T>(T box) where T : Window
    {
        var family = Family(DesignFont.NonProFace);
        box.FontFamily = family;
        box.FontSize = MessagePixels;
        box.Styles.Add(new Style(x => x.Is<TemplatedControl>())
        {
            Setters =
            {
                new Setter(TemplatedControl.FontFamilyProperty, family),
                new Setter(TemplatedControl.FontSizeProperty, MessagePixels),
            },
        });
        return box;
    }

    // ---------------------------------------------------------------- one pass

    /// <summary>A design font through one pass: what a control designed in it is drawn in.</summary>
    public static ResolvedFont Resolve(FontPass pass, DesignFont font) => FontRule.Resolve(Options, font, pass);
    public static FontFamily FamilyOf(FontPass pass, DesignFont font) => Family(Resolve(pass, font).Face);
    /// <summary>The size, in pixels at the form's own size.</summary>
    public static double SizeOf(FontPass pass, DesignFont font) => Resolve(pass, font).Pixels;

    /// <summary>
    /// Give <paramref name="target"/> (a TextBlock, a templated control, or a panel whose text
    /// inherits it) its face and size. The weight stays with the caller.
    /// </summary>
    public static T Set<T>(T target, FontPass pass, DesignFont font) where T : AvaloniaObject
    {
        var r = Resolve(pass, font);
        var family = Family(r.Face);
        target.SetValue(TextElement.FontFamilyProperty, family);
        target.SetValue(TextElement.FontSizeProperty, r.Pixels);
        // A container's buttons, boxes and list items would not inherit it: the theme gives each its
        // own size (ControlContentThemeFontSize), and a theme's setter outranks an inherited value.
        // A style on the container outranks the theme and loses to a control's own local font.
        if (target is Panel or ItemsControl or UserControl or Decorator or Window && target is StyledElement container)
            container.Styles.Add(new Style(x => x.Is<TemplatedControl>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.FontFamilyProperty, family),
                    new Setter(TemplatedControl.FontSizeProperty, r.Pixels),
                },
            });
        return target;
    }

    // ---------------------------------------------------------------- a form of DesignFonts

    public static ResolvedFont Resolve(DesignFonts.Form form, DesignFont font) => Resolve(form.Pass, font);
    public static FontFamily FamilyOf(DesignFonts.Form form, DesignFont font) => FamilyOf(form.Pass, font);
    public static double SizeOf(DesignFonts.Form form, DesignFont font) => SizeOf(form.Pass, font);

    /// <summary>
    /// The font of a control with none of its own (VB3's MS Sans Serif 8.25 bold): the "NonPro" role,
    /// for the port's text on the form. frmCharStat1's header stores Courier New 8.25 bold, but its
    /// unfonted controls are drawn in MS Sans Serif all the same (bm13.png), so this is right there too.
    /// </summary>
    public static FontFamily FamilyOf(DesignFonts.Form form) => FamilyOf(form, DesignFont.Default);
    public static double SizeOf(DesignFonts.Form form) => SizeOf(form, DesignFont.Default);

    /// <summary>The form's own font, or <paramref name="font"/>, on <paramref name="target"/> (and whatever inherits it).</summary>
    public static T Set<T>(T target, DesignFonts.Form form, DesignFont? font = null) where T : AvaloniaObject =>
        Set(target, form.Pass, font ?? DesignFont.Default);

    /// <summary>A control's own font from the table (Name or Name(Index)) on <paramref name="target"/>.</summary>
    public static T Set<T>(T target, DesignFonts.Form form, string control) where T : AvaloniaObject => Set(target, form, Design(form, control));

    /// <summary>The table's font for a control, or the form's default when the control has none of its own.</summary>
    public static DesignFont Design(DesignFonts.Form form, string control)
    {
        foreach (var e in DesignFonts.All)
            if (e.Form == form && e.Control == control) return e.Font;
        return DesignFont.Default;
    }

    // ---------------------------------------------------------------- XAML: app:UiFonts.Font="frmParty"

    /// <summary>
    /// "frmName" or "frmName.control(index)": the form, and the control whose design font this is
    /// (the form's own when left off). Set in XAML on a control or on a panel whose text inherits it.
    /// </summary>
    public static readonly AttachedProperty<string?> FontProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Font", typeof(UiFonts));

    public static string? GetFont(AvaloniaObject o) => o.GetValue(FontProperty);
    public static void SetFont(AvaloniaObject o, string? value) => o.SetValue(FontProperty, value);

    static UiFonts()
    {
        FontProperty.Changed.AddClassHandler<AvaloniaObject>((o, e) =>
        {
            if (e.NewValue is not string spec || spec == "") return;
            int dot = spec.IndexOf('.');
            string formName = dot < 0 ? spec : spec[..dot];
            var form = FormNamed(formName) ?? throw new ArgumentException($"UiFonts.Font: no form {formName} in DesignFonts");
            if (dot < 0) Set(o, form);
            else Set(o, form, spec[(dot + 1)..]);
        });
    }

    /// <summary>A form of <see cref="DesignFonts"/> by its name in MORDOR.EXE.</summary>
    public static DesignFonts.Form? FormNamed(string name)
    {
        foreach (var f in typeof(DesignFonts).GetFields())
            if (f.GetValue(null) is DesignFonts.Form form && form.Name == name) return form;
        return null;
    }

    // ---------------------------------------------------------------- rows

    /// <summary>
    /// The space a list row keeps above and below its text together, in pixels. The Store's rows
    /// had been 16 pixels for 12-pixel text at the user's request, crowded at the form's 14; with
    /// the default 11-pixel face (a 13-pixel line) this makes a list's rows 16 again, and every
    /// list's the same.
    /// </summary>
    public const double RowPad = 3;

    /// <summary>
    /// The height of one line of text in this face and size, as a TextBlock lays it out, to the pixel
    /// above. Measured once for each face, size and weight: the pack, spell and party rows ask for
    /// it for every row they build. A size of nothing or less is drawn at one pixel
    /// (<see cref="FontRule.Pixels"/>): FormattedText throws on it.
    ///
    /// The port's MS Sans Serif keeps its plain line for its bold (<see cref="SansStandIn"/>): the
    /// original's bold was the plain letters drawn twice, on the plain line, and Tahoma Bold's
    /// taller line would make every row and caption that holds one a pixel taller than the form's.
    /// Its letters are drawn <see cref="BoldLift(FontFamily, double)"/> higher, which puts them in that line.
    /// </summary>
    public static double LineHeight(FontFamily family, double size, FontWeight weight = FontWeight.Normal)
    {
        size = Math.Max(1, size);
        if (weight > FontWeight.Normal && SansStandIn.IsFamily(family)) weight = FontWeight.Normal;
        var key = (family.ToString(), size, weight);
        if (Lines.TryGetValue(key, out double h)) return h;
        var ft = new FormattedText("Xgjy", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   new Typeface(family, FontStyle.Normal, weight), size, null);
        h = Math.Ceiling(ft.Height - 0.01);
        Lines[key] = h;
        return h;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string, double, FontWeight), double> Lines = new();

    /// <summary>How wide <paramref name="text"/> is in a design font through one pass, bold as the font is.</summary>
    public static double TextWidth(string text, FontPass pass, DesignFont font)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   new Typeface(FamilyOf(pass, font), FontStyle.Normal, font.Bold ? FontWeight.Bold : FontWeight.Normal),
                                   Math.Max(1, SizeOf(pass, font)), null);
        return Math.Ceiling(ft.WidthIncludingTrailingWhitespace);
    }

    /// <summary>
    /// How much larger than the defaults' the Options fonts draw a form's text, across and down: a
    /// line of sample captions in each of the four faces a form's controls are designed in (MS Sans
    /// Serif and Courier New 8.25, bold and not), through this pass, against the same line in the
    /// default fonts; never below one. Exactly one each way with the default fonts. A page drawn to
    /// its form's data is laid out this much larger (<see cref="FormPage"/>), so a larger chosen font
    /// keeps the room the form gave its captions (a deviation, docs/FIDELITY.md).
    /// </summary>
    public static (double X, double Y) FormGrowth(FontPass pass) => FontRule.IsDefault(Options) ? (1, 1) : (
        UiFonts.Cached($"{nameof(FormGrowth)}.X.{pass}", () => Growth(pass, across: true)),
        UiFonts.Cached($"{nameof(FormGrowth)}.Y.{pass}", () => Growth(pass, across: false)));

    private const string GrowthSample = "Identify, Realign & Sell Items; Search for What? Pool & Deposit 0123456789";
    private static readonly GameOptions DefaultOptions = new();

    private static double Growth(FontPass pass, bool across)
    {
        double most = 1;
        foreach (var font in new[] { DesignFont.Default, DesignFont.Default.Plain,
                                     new DesignFont(DesignFont.ProFace, 8.25), new DesignFont(DesignFont.ProFace, 8.25, Bold: false) })
        {
            double chosen = Measure(Options, pass, font, across), design = Measure(DefaultOptions, pass, font, across);
            if (design > 0) most = Math.Max(most, chosen / design);
        }
        // to the hundredth above, so a page's size does not turn on a rounding error
        return Math.Ceiling(most * 100 - 0.001) / 100;
    }

    private static double Measure(GameOptions o, FontPass pass, DesignFont font, bool across)
    {
        var r = FontRule.Resolve(o, font, pass);
        var weight = font.Bold ? FontWeight.Bold : FontWeight.Normal;
        if (!across) return LineHeight(Family(r.Face), r.Pixels, weight);
        var ft = new FormattedText(GrowthSample, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   new Typeface(Family(r.Face), FontStyle.Normal, weight), Math.Max(1, r.Pixels), null);
        return ft.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// How much lower in its line a bold letter of this face sits than a plain one, to the pixel:
    /// what a bold caption is lifted by, so its letters stand where the plain face's would. Nothing
    /// for a face whose bold and plain share their metrics; about one pixel for the port's MS Sans
    /// Serif, whose bold is Tahoma Bold (<see cref="SansStandIn"/>), which sits lower in the line
    /// than Microsoft Sans Serif and brought a caption's descenders onto the box under it ("Stats
    /// Required" on the Guild's stats) and bold headings a pixel below the plain values beside them.
    /// </summary>
    public static double BoldLift(FontFamily family, double size)
    {
        size = Math.Max(1, size);
        var key = (family.ToString(), size);
        if (Lifts.TryGetValue(key, out double lift)) return lift;
        double Baseline(FontWeight w) => new FormattedText("X", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                                            new Typeface(family, FontStyle.Normal, w), size, null).Baseline;
        lift = Math.Max(0, Math.Round(Baseline(FontWeight.Bold) - Baseline(FontWeight.Normal)));
        Lifts[key] = lift;
        return lift;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string, double), double> Lifts = new();

    /// <summary>The lift of a design font through a pass: nothing when it is not bold (<see cref="BoldLift(FontFamily, double)"/>).</summary>
    public static double BoldLift(FontPass pass, DesignFont font) =>
        font.Bold ? BoldLift(FamilyOf(pass, font), SizeOf(pass, font)) : 0;

    public static double LineHeight(FontPass pass, DesignFont font) =>
        LineHeight(FamilyOf(pass, font), SizeOf(pass, font), font.Bold ? FontWeight.Bold : FontWeight.Normal);
    public static double LineHeight(DesignFonts.Form form, DesignFont font) => LineHeight(form.Pass, font);

    /// <summary>
    /// A list row for text in these fonts (the form's own when none is given): the tallest one's
    /// line and <see cref="RowPad"/>, and never less than <paramref name="atLeast"/>.
    /// </summary>
    public static double RowHeight(FontPass pass, double atLeast, params DesignFont[] fonts) => Math.Max(atLeast, Line(pass, fonts) + RowPad);
    public static double RowHeight(DesignFonts.Form form, double atLeast, params DesignFont[] fonts) => RowHeight(form.Pass, atLeast, fonts);

    /// <summary>A label's height for text in these fonts: the tallest line, and never less than the form's own <paramref name="atLeast"/>.</summary>
    public static double LabelHeight(FontPass pass, double atLeast, params DesignFont[] fonts) => Math.Max(atLeast, Line(pass, fonts));
    public static double LabelHeight(DesignFonts.Form form, double atLeast, params DesignFont[] fonts) => LabelHeight(form.Pass, atLeast, fonts);

    private static double Line(FontPass pass, DesignFont[] fonts)
    {
        double line = 0;
        foreach (var f in fonts.Length == 0 ? new[] { DesignFont.Default } : fonts) line = Math.Max(line, LineHeight(pass, f));
        return line;
    }
}
