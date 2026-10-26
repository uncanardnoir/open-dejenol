using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// A town service drawn to its form's own data in MORDOR.EXE: every control at the rectangle the
/// form gives it, in twips, 15 to the pixel, on a client of the form's own size. The services'
/// forms (frmCityGuild, frmCityBank, frmCityMorgue, frmCitySeer, ...) all hold their controls in a
/// VideoSoft Elastic, elaElastic(0), which covers the client a pixel beyond its edge; each form's
/// Elastic is placed where its data puts it and the controls go inside it, or inside the smaller
/// Elastics it holds, at their own offsets - VB places a child relative to its container.
///
/// Each control is drawn in its design font (<see cref="DesignFont"/>: MS Sans Serif 8.25 bold,
/// the form's, unless the form data gives it one of its own - lblWelcome's 12 points bold with
/// its Tag ":12", the Guild's Courier New stats) put through the Options window's fonts as every
/// service form puts it (<see cref="Pass"/>, <see cref="UiFonts"/>). The colours are the port's
/// dark theme's stand-ins for the form's: its blue welcome, its maroon and navy captions.
///
/// See docs/FIDELITY.md, "The town's service windows are drawn to their forms' data".
/// </summary>
internal sealed class FormPage
{
    /// <summary>VB's twips per pixel at the 96dpi the game was drawn for.</summary>
    public const double Twips = 15;

    /// <summary>
    /// The font pass of the page being built, which the static builders below draw in: set by the
    /// constructor to <see cref="FontPass.RestyleAndZoom"/>, as every service form runs sub0784 in
    /// its Form_Load and sub07C5 in its Form_Resize (FRMCTYG.BAS:654/661, FRMCITYB.BAS:133/140,
    /// FRMMORG.BAS:233/240, FRMSEEK.BAS:396/403, FRMCITYC.BAS:423/431, FRMCTYS.BAS:449/457), or by
    /// <see cref="Fonts"/> for a form that goes through another (the Library and the Guild Log,
    /// sub0802 alone: FRMLIBRA.BAS:31, FRMGLDL.BAS:30). Pages are built one at a time, on the UI thread.
    /// It is the pass of the page built last, so it is read only while a page is being built: what a
    /// page draws afterwards (a grid's cell template, a heading redrawn later) takes the page's own
    /// <see cref="Fonts"/>, captured at build time.
    /// </summary>
    public static FontPass Pass { get; private set; } = FontPass.RestyleAndZoom;

    /// <summary>This page's font pass, read by what the page draws after it is built.</summary>
    public FontPass Fonts { get; }

    /// <summary>
    /// How much larger than the form's own the page being built is laid out, across and down: one
    /// with the default fonts, and the Options fonts' growth over them otherwise
    /// (<see cref="UiFonts.FormGrowth"/>). Every rectangle of the form data is put through it
    /// (<see cref="Place"/>, <see cref="X"/>, <see cref="Y"/>), so a larger chosen font finds the
    /// room its captions had in the default one rather than wrapping onto the control below or
    /// running into the next. Like <see cref="Pass"/> it is the page built last's; what a page
    /// draws afterwards reads its own (<see cref="GrowX"/>, <see cref="GrowY"/>).
    /// DEVIATION (docs/FIDELITY.md): the original sizes the fonts by the form's width alone, and a
    /// larger chosen font overflows its controls.
    /// </summary>
    private static double _zoomX = 1, _zoomY = 1;

    /// <summary>This page's growth across and down (see <see cref="_zoomX"/>).</summary>
    public double GrowX { get; }
    public double GrowY { get; }

    /// <summary>A distance across in the form's twips, in the page's pixels: its grown size.</summary>
    public static double X(double twips) => twips / Twips * _zoomX;
    /// <summary>A distance down in the form's twips, in the page's pixels: its grown size.</summary>
    public static double Y(double twips) => twips / Twips * _zoomY;

    /// <summary>This page's <see cref="X"/>, for what it draws after it is built.</summary>
    public double PixelsX(double twips) => twips / Twips * GrowX;
    /// <summary>This page's <see cref="Y"/>, for what it draws after it is built.</summary>
    public double PixelsY(double twips) => twips / Twips * GrowY;

    /// <summary>A plain face's design font: MS Sans Serif 8.25, bold or not.</summary>
    private static DesignFont Sans(bool bold) => bold ? DesignFont.Default : DesignFont.Default.Plain;

    /// <summary>The form's blue (lblWelcome's &amp;HFF0000), in the dark theme.</summary>
    public static readonly IBrush Blue = Brushes.CornflowerBlue;
    /// <summary>The form's maroon captions (&amp;H80: the Guild's GuildMaster, the Bank's lblStatus).</summary>
    public static readonly IBrush Maroon = Brushes.IndianRed;
    /// <summary>The form's navy captions (&amp;H800000: the Bank's lblLabel, the Seer's lblLabel).</summary>
    public static readonly IBrush Navy = new SolidColorBrush(Color.FromRgb(0x8a, 0xa8, 0xe8));
    /// <summary>A plain caption, the form's black.</summary>
    public static readonly IBrush Ink = new SolidColorBrush(Color.FromRgb(0xd8, 0xd8, 0xe0));
    /// <summary>A tan-yellow field you type into (the help's "tan-yellow fields take input").</summary>
    public static readonly IBrush InputField = new SolidColorBrush(Color.FromRgb(0xd8, 0xc8, 0x8a));
    /// <summary>A dark-grey field that only shows something (a label with BorderStyle 1).</summary>
    public static readonly IBrush DisplayField = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3a));
    /// <summary>The form's own ground, the port's dark face (a form's BackColor, &amp;HC0C0C0).</summary>
    public static readonly IBrush Face = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x20));
    /// <summary>An Elastic's bevelled line, in the dark theme (the character window's Stats page draws its two too).</summary>
    public static readonly IBrush Bevel =new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66));

    /// <summary>The client area, in pixels: the size the service window's inside is made.</summary>
    public Size Client { get; }

    /// <summary>What goes into the service window: the whole client.</summary>
    public Canvas Page { get; }

    /// <summary>elaElastic(0), which every control is placed in.</summary>
    public Canvas Root { get; }

    /// <param name="clientW">The form's ScaleWidth, in twips.</param>
    /// <param name="clientH">The form's ScaleHeight, in twips.</param>
    /// <param name="elastic">elaElastic(0)'s rectangle in the client, in twips.</param>
    /// <param name="fonts">The form's font pass: the services' unless it goes through another (see <see cref="Pass"/>).</param>
    public FormPage(int clientW, int clientH, FormRect elastic, FontPass fonts = FontPass.RestyleAndZoom)
    {
        Fonts = Pass = fonts;
        (GrowX, GrowY) = (_zoomX, _zoomY) = UiFonts.FormGrowth(fonts);
        int left = elastic.Left, top = elastic.Top;
        Client = new Size(X(clientW), Y(clientH));
        Page = new Canvas
        {
            Width = Client.Width, Height = Client.Height, ClipToBounds = true,
            Background = Face,
        };
        UiFonts.Set(Page, Pass, DesignFont.Default);
        Root = new Canvas();
        Canvas.SetLeft(Root, X(left));
        Canvas.SetTop(Root, Y(top));
        Page.Children.Add(Root);
    }

    /// <summary>A control put at its form rectangle (twips) inside its container, grown with the page (<see cref="X"/>).</summary>
    public static T Place<T>(Canvas into, T c, FormRect r) where T : Control
    {
        Canvas.SetLeft(c, X(r.Left));
        Canvas.SetTop(c, Y(r.Top));
        c.Width = X(r.Width);
        c.Height = Y(r.Height);
        into.Children.Add(c);
        return c;
    }

    /// <summary>
    /// The <paramref name="index"/>th of a control array loaded across from its first: each at the
    /// one before's Left + Width (the Guild's lblStatName and lblStat, FRMCTYG.BAS:631-640).
    /// </summary>
    public static FormRect Across(FormRect first, int index) =>
        first with { Left = first.Left + index * first.Width };

    /// <summary>
    /// The <paramref name="index"/>th of a control array loaded down from its first: each at the one
    /// above's Top + Height (the Confinement's rows, FRMCITYC.BAS:498-503).
    /// </summary>
    public static FormRect Down(FormRect first, int index) =>
        first with { Top = first.Top + index * first.Height };

    /// <summary>
    /// A VideoSoft Elastic inside another, drawn as the bevelled box the form shows: its controls
    /// go into the returned Canvas at their own offsets from its corner.
    /// </summary>
    public static Canvas Elastic(Canvas into, FormRect r, IBrush? background = null)
    {
        var inside = new Canvas { ClipToBounds = true };
        Place(into, new Border
        {
            BorderBrush = Bevel, BorderThickness = new Thickness(1), Background = background,
            Child = inside,
        }, r);
        // the controls are placed from the Elastic's own corner, which the one-pixel line draws over
        inside.Margin = new Thickness(-1);
        return inside;
    }

    /// <summary>
    /// A Label. <paramref name="bold"/> and <paramref name="points"/> are its font, or the form's
    /// 8.25 bold when it has none; <paramref name="align"/> its Alignment (0 left, 1 right, 2 centre);
    /// <paramref name="boxed"/> its BorderStyle 1, a sunken field. VB wraps a label's caption inside
    /// its box and cuts what does not fit.
    /// </summary>
    public static TextBlock Label(Canvas into, string text, FormRect r,
                                  IBrush? color = null, bool bold = true, double points = 8.25, int align = 0,
                                  bool boxed = false, string? font = null) =>
        Label(into, text, r, new DesignFont(font ?? DesignFont.NonProFace, points, bold), color, align, boxed);

    /// <summary>A Label in its design font from <see cref="DesignFonts"/>.</summary>
    public static TextBlock Label(Canvas into, string text, FormRect r, DesignFont font,
                                  IBrush? color = null, int align = 0, bool boxed = false)
    {
        var t = UiFonts.Set(new TextBlock
        {
            Text = text,
            Foreground = color ?? Ink,
            FontWeight = font.Bold ? FontWeight.Bold : FontWeight.Normal,
            TextAlignment = align switch { 1 => TextAlignment.Right, 2 => TextAlignment.Center, _ => TextAlignment.Left },
            TextWrapping = TextWrapping.Wrap,
            ClipToBounds = true,
        }, Pass, font);
        Lift(t, UiFonts.BoldLift(Pass, font));
        if (!boxed)
        {
            Place(into, t, r);
            LetOut(t, t, font, align, 0);
            return t;
        }
        t.Margin = new Thickness(2, 0);
        t.VerticalAlignment = VerticalAlignment.Center;
        var field = Place(into, new Border
        {
            Background = DisplayField, BorderBrush = Bevel, BorderThickness = new Thickness(1), Child = t,
        }, r);
        LetOut(t, field, font, align, 6);
        return t;
    }

    /// <summary>
    /// A label whose caption a larger Options font no longer fits is let out to hold it, where VB
    /// cuts it off: across to its longest word (away from the side it is aligned to), and down to
    /// its lines, one at the least. The other controls keep their places (a deviation,
    /// docs/FIDELITY.md). With the default fonts every caption fits its box and nothing moves.
    /// </summary>
    /// <param name="box">What is placed: the label, or the sunken field holding it.</param>
    /// <param name="inset">The field's border and the label's margins across, together.</param>
    private static void LetOut(TextBlock t, Control box, DesignFont font, int align, double inset)
    {
        double frame = inset > 0 ? 2 : 0;
        double line = UiFonts.LineHeight(Pass, font) + frame;
        if (box.Height < line) box.Height = line;
        if (string.IsNullOrEmpty(t.Text)) return;
        double longest = 0;
        foreach (string word in t.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            longest = Math.Max(longest, UiFonts.TextWidth(word, Pass, font));
        double wide = longest + inset;
        if (wide > box.Width)
        {
            double more = wide - box.Width;
            box.Width = wide;
            if (align != 0) Canvas.SetLeft(box, Canvas.GetLeft(box) - (align == 1 ? more : more / 2));
        }
        // the caption's own height at that width: measured with the label's own size let go
        double had = t.Height, across = t.Width, room = Math.Max(0, box.Width - inset);
        t.Height = double.NaN;
        t.Width = double.NaN;
        t.Measure(new Size(room, double.PositiveInfinity));
        double tall = t.DesiredSize.Height + frame;
        t.Height = had;
        t.Width = across;
        if (tall > box.Height) box.Height = tall;
    }

    /// <summary>
    /// Text drawn <paramref name="lift"/> pixels higher than it is laid out (<see cref="UiFonts.BoldLift(FontPass, DesignFont)"/>):
    /// a bold caption's letters where the plain face's would stand.
    /// </summary>
    public static void Lift(Control text, double lift)
    {
        if (lift > 0) text.RenderTransform = new TranslateTransform(0, -lift);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button, object> AccessKeys = new();

    /// <summary>
    /// The letter after the "&amp;" in the button's caption in its form's data (MORDOR.EXE): Alt and
    /// that letter press it while its form is the active window and it is visible and enabled, as a
    /// VB CommandButton's access key does. Kept apart from Avalonia's own access keys, which reach
    /// across every window in the frame; the city answers it from the open service's page alone
    /// (<c>CityWindow.ServiceAccessKey</c>).
    /// </summary>
    public static Button Access(Button b, char letter)
    {
        AccessKeys.AddOrUpdate(b, char.ToUpperInvariant(letter));
        return b;
    }

    /// <summary>The access letter <see cref="Access"/> gave a button, upper case, or null.</summary>
    public static char? AccessOf(Button b) => AccessKeys.TryGetValue(b, out var o) ? (char)o : null;

    /// <summary>A CommandButton: its caption centred, in the form's 8.25 bold unless it has a font of its own.</summary>
    public static Button Button(Canvas into, string caption, FormRect r, bool bold = true) =>
        // a TextBlock, so the caption is not read for an access key
        PlaceButton(into, new Button { Content = new TextBlock { Text = caption } }, r, bold);

    /// <summary>A button made elsewhere put at its form rectangle, with the theme's padding taken off.</summary>
    public static Button PlaceButton(Canvas into, Button b, FormRect r, bool bold = true)
    {
        b.Padding = new Thickness(0);
        b.MinHeight = 0;
        b.MinWidth = 0;
        UiFonts.Set(b, Pass, Sans(bold));
        b.FontWeight = bold ? FontWeight.Bold : FontWeight.Normal;
        if (b.Content is TextBlock caption) Lift(caption, UiFonts.BoldLift(Pass, Sans(bold)));
        b.HorizontalContentAlignment = HorizontalAlignment.Center;
        b.VerticalContentAlignment = VerticalAlignment.Center;
        b.Focusable = false;
        return Place(into, b, r);
    }

    /// <summary>
    /// A ListBox, its rows the font's line and <see cref="UiFonts.RowPad"/> (16 pixels with the
    /// default 11-pixel face, where VB's are the line's 13): the theme's roomy item padding is
    /// taken off, and a larger Options font makes taller rows rather than clipped ones.
    /// </summary>
    public static ListBox List(Canvas into, FormRect r, bool bold = false)
    {
        var l = UiFonts.Set(new ListBox
        {
            Padding = new Thickness(0),
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x10)),
            BorderBrush = Bevel, BorderThickness = new Thickness(1),
        }, Pass, Sans(bold));
        TightRows(l, UiFonts.RowHeight(Pass, 0, Sans(bold)));
        return Place(into, l, r);
    }

    /// <summary>
    /// A list's rows made <paramref name="rowHeight"/> tall, the text set <see cref="ListInset"/> in
    /// from the list's one-pixel line, as far as a VB ListBox sets it in from its outer edge (its
    /// two-pixel sunken border and two pixels more). It had been two in, and read as touching the
    /// border (the Guild's list).
    /// </summary>
    public static void TightRows(ListBox l, double rowHeight)
    {
        l.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Thickness(ListInset, 0)),
                new Setter(Layoutable.MinHeightProperty, 0d),
                new Setter(Layoutable.HeightProperty, rowHeight),
                new Setter(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Stretch),
                new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            },
        });
    }

    /// <summary>How far a list's text is set in from its line (<see cref="TightRows"/>).</summary>
    public const double ListInset = 3;

    /// <summary>
    /// An OptionButton at its rectangle: VB's 13-pixel round button with its caption beside it, in
    /// the form's 8.25 bold unless it has a font of its own. The theme's radio button is a 20-pixel
    /// circle on a 32-pixel row, which a form's 18-pixel option cuts in half.
    /// </summary>
    public static RadioButton Option(Canvas into, string caption, FormRect r, string group, bool isChecked, bool bold = true)
    {
        var o = UiFonts.Set(new RadioButton
        {
            Content = caption, GroupName = group, IsChecked = isChecked,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            MinHeight = 0, MinWidth = 0, Padding = new Thickness(0),
        }, Pass, Sans(bold));
        o.Template = new Avalonia.Controls.Templates.FuncControlTemplate<RadioButton>((rb, _) =>
        {
            var dot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 5, Height = 5, Fill = Ink, IsVisible = rb.IsChecked == true,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            rb.IsCheckedChanged += (_, _) => dot.IsVisible = rb.IsChecked == true;
            var presenter = new Avalonia.Controls.Presenters.ContentPresenter
            {
                Name = "PART_ContentPresenter", Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            presenter[!Avalonia.Controls.Presenters.ContentPresenter.ContentProperty] = rb[!ContentControl.ContentProperty];
            var circle = new Panel
            {
                Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = 12, Height = 12, Stroke = Bevel, StrokeThickness = 1,
                        Fill = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x10)),
                    },
                    dot,
                },
            };
            Grid.SetColumn(presenter, 1);
            // a transparent ground, so the caption takes the click as well as the circle
            return new Grid { ColumnDefinitions = new ColumnDefinitions("13,*"), Background = Brushes.Transparent, Children = { circle, presenter } };
        });
        return Place(into, o, r);
    }

    /// <summary>
    /// A CheckBox drawn as VB draws one: a 13-pixel square with its caption beside it, as tall as
    /// its caption's line. The theme's box is a 20-pixel square at the top of a 32-pixel row, which
    /// a one-line place cuts off (the Store's "Hide unusable" had its square cut by the Buy Items panel).
    /// </summary>
    public static CheckBox Compact(CheckBox box)
    {
        box.MinHeight = 0;
        box.MinWidth = 0;
        box.Template = new Avalonia.Controls.Templates.FuncControlTemplate<CheckBox>((cb, _) =>
        {
            var tick = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M 1.5,5 L 4,7.5 L 8.5,2"), Stroke = Ink, StrokeThickness = 1.6,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                IsVisible = cb.IsChecked == true,
            };
            cb.IsCheckedChanged += (_, _) => tick.IsVisible = cb.IsChecked == true;
            var presenter = new Avalonia.Controls.Presenters.ContentPresenter
            {
                Name = "PART_ContentPresenter", Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            presenter[!Avalonia.Controls.Presenters.ContentPresenter.ContentProperty] = cb[!ContentControl.ContentProperty];
            var square = new Border
            {
                Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = Bevel, BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x10)), Child = tick,
            };
            Grid.SetColumn(presenter, 1);
            // a transparent ground, so the caption takes the click as well as the square
            return new Grid { ColumnDefinitions = new ColumnDefinitions("13,*"), Background = Brushes.Transparent, Children = { square, presenter } };
        });
        return box;
    }

    /// <summary>A TextBox at its rectangle, tan-yellow as the forms' input fields are (BackColor &amp;HE0FFFF).</summary>
    public static TextBox Text(Canvas into, FormRect r, bool bold = false)
    {
        var box = UiFonts.Set(new TextBox
        {
            Background = InputField, Foreground = Brushes.Black,
            MinHeight = 0, MinWidth = 0,
            Padding = new Thickness(2, 0),
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            VerticalContentAlignment = VerticalAlignment.Center,
        }, Pass, Sans(bold));
        // keep the tan-yellow while it has focus, instead of the theme's dark focused background
        foreach (string state in new[] { ":focus", ":pointerover", ":focus-within" })
            box.Styles.Add(new Style(x => x.OfType<TextBox>().Class(state).Template().OfType<Border>().Name("PART_BorderElement"))
            {
                Setters = { new Setter(Border.BackgroundProperty, InputField) },
            });
        return Place(into, box, r);
    }
}
