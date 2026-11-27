using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Mordor.Modding;

/// <summary>The symbols <see cref="ModGlyphs"/> draws (API version 18).</summary>
public enum ModSymbol
{
    /// <summary>A tick, as U+2713 ✓.</summary>
    Check,
    /// <summary>An arrow pointing right, as U+2192 →.</summary>
    RightArrow,
}

/// <summary>
/// A few symbols drawn as small vector shapes rather than as a font's characters (API version 18),
/// so a mod's tick or arrow looks the same on every system: the faces the game bundles away from
/// Windows lack some symbols, and a face that has one may draw it badly at a list's size (the
/// dungeon's arrows came out as dashes on Linux). Nothing of a font is used to draw them.
///
/// Each is sized to the text it sits in, from the font size and weight it inherits (or is given):
/// as wide as the character would be, its foot on the text's baseline, its line thicker in bold. Its
/// colour is the text's own (the inherited foreground) unless a brush is given; a theme role's brush
/// (<see cref="ThemeKeys"/>) is recoloured in place when the theme changes, and the shape with it.
///
/// <list type="bullet">
/// <item><see cref="Inline"/>: in a TextBlock's inlines, between Runs, on the line's baseline.</item>
/// <item><see cref="Block"/>: on its own, a TextBlock holding only the shape, so it stands beside
/// another TextBlock of the same face (in a horizontal panel) with the same line height and baseline.</item>
/// <item><see cref="Shape"/>: the bare control, for a layout of a mod's own.</item>
/// </list>
/// A shape is not text: a copy of the line, or a tooltip, that needs the symbol takes
/// <see cref="CharacterOf"/>.
/// </summary>
public static class ModGlyphs
{
    /// <summary>The character a symbol stands for: "✓" or "→".</summary>
    public static string CharacterOf(ModSymbol symbol) => symbol switch
    {
        ModSymbol.Check => "✓",
        ModSymbol.RightArrow => "→",
        _ => throw new ArgumentOutOfRangeException(nameof(symbol)),
    };

    /// <summary>The shape as a control, in <paramref name="brush"/> or, with none, the text's own colour.</summary>
    public static ModGlyph Shape(ModSymbol symbol, IBrush? brush = null)
    {
        var g = new ModGlyph { Symbol = symbol };
        if (brush != null) g.Foreground = brush;
        return g;
    }

    /// <summary>The shape for a TextBlock's inlines, on the line's baseline, in <paramref name="brush"/> or the text's colour.</summary>
    public static InlineUIContainer Inline(ModSymbol symbol, IBrush? brush = null) =>
        new(Shape(symbol, brush)) { BaselineAlignment = BaselineAlignment.Baseline };

    /// <summary>The shape on its own: a TextBlock holding only it, in <paramref name="brush"/> or the TextBlock's foreground.</summary>
    public static TextBlock Block(ModSymbol symbol, IBrush? brush = null)
    {
        var t = new TextBlock();
        t.Inlines!.Add(Inline(symbol, brush));
        return t;
    }
}

/// <summary>
/// One of <see cref="ModGlyphs"/>' shapes (API version 18): a control as wide as the symbol's
/// character would be at the inherited font size, standing on its baseline - in a TextBlock's inlines
/// its foot is the line's baseline - and drawn with a line one pixel thick, two in bold, at a list's
/// size, thicker in a larger font.
/// </summary>
public sealed class ModGlyph : Control
{
    /// <summary>Which symbol.</summary>
    public static readonly StyledProperty<ModSymbol> SymbolProperty =
        AvaloniaProperty.Register<ModGlyph, ModSymbol>(nameof(Symbol));

    /// <summary>The colour: the text's own (inherited) unless set.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ModGlyph>();

    /// <summary>The size of the text it stands in (inherited).</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<ModGlyph>();

    /// <summary>The weight of the text it stands in (inherited): SemiBold or heavier draws the thicker line.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<ModGlyph>();

    static ModGlyph()
    {
        AffectsMeasure<ModGlyph>(SymbolProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<ModGlyph>(SymbolProperty, ForegroundProperty, FontSizeProperty, FontWeightProperty);
    }

    public ModSymbol Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    // Each symbol's box in ems: its advance (as wide as the character in a common face, side bearings
    // included) and its height above the baseline (the box's foot is the baseline). Under a capital's
    // height, so the shape never makes its line taller.
    private static (double Advance, double Height) Box(ModSymbol s) => s switch
    {
        ModSymbol.Check => (0.68, 0.70),
        _ => (0.92, 0.58),
    };

    /// <summary>The line's thickness in pixels: 1 at a list's size, 2 in bold, more in a larger font.</summary>
    private double Thickness() =>
        Math.Max(1, Math.Round(FontSize * (FontWeight >= FontWeight.SemiBold ? 0.14 : 0.08)));

    protected override Size MeasureOverride(Size availableSize)
    {
        var (adv, h) = Box(Symbol);
        double em = FontSize > 0 ? FontSize : 12;
        return new Size(Math.Ceiling(adv * em), Math.Ceiling(h * em));
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush) return;
        double em = FontSize > 0 ? FontSize : 12;
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        // whole device pixels thick, so a line one pixel thick is one pixel and not two half-lit ones
        double t = Math.Max(1, Math.Round(Thickness() * scale)) / scale;
        double half = t / 2;
        var pen = new Pen(brush, t, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            switch (Symbol)
            {
                case ModSymbol.Check:
                {
                    // the ink from 0.08 em in to the advance's last 0.1 em, from the top to the baseline,
                    // kept inside by half the line
                    double l = 0.08 * em + half, r = w - 0.10 * em - half, top = half, foot = h - half;
                    g.BeginFigure(new Point(l, top + (foot - top) * 0.55), false);
                    g.LineTo(new Point(l + (r - l) * 0.36, foot));
                    g.LineTo(new Point(r, top));
                    g.EndFigure(false);
                    break;
                }
                default:
                {
                    // the shaft at the arrow's middle, 0.3 em over the baseline, put on whole device
                    // pixels; the head 0.22 em either side of it and 0.26 em long, at least two pixels
                    double y = Snap(h - 0.30 * em, t, scale);
                    double l = 0.08 * em + half, r = w - 0.08 * em - half;
                    double rise = Math.Max(2, 0.22 * em), run = Math.Max(2, 0.26 * em);
                    g.BeginFigure(new Point(l, y), false);
                    g.LineTo(new Point(r, y));
                    g.EndFigure(false);
                    g.BeginFigure(new Point(r - run, y - rise), false);
                    g.LineTo(new Point(r, y));
                    g.LineTo(new Point(r - run, y + rise));
                    g.EndFigure(false);
                    break;
                }
            }
        }
        context.DrawGeometry(null, pen, geo);
    }

    /// <summary>
    /// <paramref name="y"/> moved to where a horizontal line <paramref name="t"/> thick covers whole
    /// device pixels, wherever the control stands in its window.
    /// </summary>
    private double Snap(double y, double t, double scale)
    {
        var root = TopLevel.GetTopLevel(this);
        double top = root != null && this.TranslatePoint(default, root) is { } p ? p.Y : 0;
        double dev = (top + y) * scale;
        long px = (long)Math.Round(t * scale);
        double snapped = px % 2 == 1 ? Math.Floor(dev) + 0.5 : Math.Round(dev);
        return snapped / scale - top;
    }
}
