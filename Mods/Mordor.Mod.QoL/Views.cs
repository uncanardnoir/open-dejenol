using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.Mod.QoL;

/// <summary>
/// The Quality of Life mod's controls: the Party Information bars. The
/// arithmetic is in <see cref="Bars"/>; only drawing is here, so the
/// rest of the mod loads and can be checked where there is no Avalonia.
/// </summary>
public static class Views
{
    private static readonly IBrush Track = new SolidColorBrush(Rgb(BarColours.Track));
    /// <summary>The Party window's own blue (PartyRowColours.Fixed): the colour of its Spells and Option text.</summary>
    private static readonly IBrush Text = Brushes.CornflowerBlue;

    private static Color Rgb((byte R, byte G, byte B) c, byte alpha = 255) => Color.FromArgb(alpha, c.R, c.G, c.B);

    /// <summary>A bar: a track (dark unless <paramref name="track"/> is given) with <paramref name="fraction"/> of it filled from the left.</summary>
    private static Control Bar(double? fraction, Color fill, double height, IBrush? track = null)
    {
        double f = fraction ?? 0;
        var inside = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(f, GridUnitType.Star),
                new ColumnDefinition(1 - f, GridUnitType.Star),
            },
        };
        if (f > 0) inside.Children.Add(new Border { Background = new SolidColorBrush(fill), CornerRadius = new CornerRadius(1) });
        return new Border
        {
            Height = height,
            Background = track ?? Track,
            CornerRadius = new CornerRadius(1),
            Opacity = fraction == null ? 0.5 : 1,
            Child = inside,
        };
    }

    /// <summary>
    /// The HP/SP column's cell: hits over spell points, two thin bars - hits green to red, spell points blue to purple. Read from the
    /// character as the Party window reads it, so it is drawn again with the row whenever the hits
    /// or spell points change (a blow, a spell, a rest).
    /// </summary>
    public static Control HpSpCell(Character c)
    {
        double? hp = Bars.Fraction(c.CurHits, c.MaxHits);
        double? sp = Bars.Fraction(c.CurSpellPoints, c.MaxSpellPoints);
        var panel = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 1, 0),
            Children =
            {
                Bar(hp, Rgb(Bars.Colour(hp ?? 0)), 4),
                Bar(sp, Rgb(Bars.SpColour(sp ?? 0)), 4),
            },
        };
        string Say(string what, long cur, long max, double? f) =>
            f == null ? $"{what}: none" : $"{what}: {cur}/{max} ({Bars.Percent(f.Value)}%)";
        ToolTip.SetTip(panel, Say("Hits", c.CurHits, c.MaxHits, hp) + "\n" + Say("Spells", c.CurSpellPoints, c.MaxSpellPoints, sp));
        // the whole cell takes the pointer, so the tooltip shows over the gap between the bars too
        return new Border { Background = Brushes.Transparent, Child = panel };
    }

    /// <summary>
    /// The experience bar (Bars.Experience): short of the next level, green from this level's
    /// threshold toward the next's; once the level is earned the bar stays full green and purple
    /// fills over it from the left, from the next level's threshold toward the pin; pinned, the
    /// whole bar purple.
    /// </summary>
    private static Control ExperienceBar(Character c, byte alpha, double height)
    {
        var (toNext, toPin, stage2, _, _) = Bars.Experience(c);
        if (!stage2) return Bar(toNext, Rgb(Bars.ToNextColour, alpha), height);
        return Bar(toPin, Rgb(Bars.ToPinColour, alpha), height, new SolidColorBrush(Rgb(Bars.ToNextColour, alpha)));
    }

    private static string ExperienceTip(Character c, Mark? mark) =>
        mark == null ? XpColumn.Tip(c) : XpColumn.Tip(c) + "\n" + mark.Value.Tip;

    /// <summary>The quest mark as drawn: bold, in its colour, in the row's own face and size so the row keeps its 13 pixels.</summary>
    private static TextBlock MarkBlock(Mark m) => new()
    {
        Text = m.Glyph,
        Foreground = new SolidColorBrush(Rgb(m.Colour)),
        FontWeight = FontWeight.Bold,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 1, 0),
    };

    /// <summary>The figure with the quest mark before it, set right as the column's text is.</summary>
    private static Control MarkedFigure(TextBlock figure, Mark? mark)
    {
        if (mark == null) return figure;
        figure.HorizontalAlignment = HorizontalAlignment.Left;
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = figure.Margin,
            Children = { MarkBlock(mark.Value) },
        };
        figure.Margin = default;
        row.Children.Add(figure);
        return row;
    }

    /// <summary>
    /// The Next column's cell with the experience bar on: the text it always has, over a bar that
    /// fills behind it, so the two share the column's width and the row keeps its height. A quest
    /// mark, when there is one, goes before the text.
    /// </summary>
    public static Control NextCell(Character c, string text, Mark? mark = null, bool showBar = true)
    {
        var grid = new Grid { Background = Brushes.Transparent, Margin = new Thickness(3, 1, 0, 1) };
        // the XP bar behind the figure, unless switched off on its own (1.11.0)
        if (showBar)
        {
            var bar = ExperienceBar(c, 0x80, double.NaN);
            bar.VerticalAlignment = VerticalAlignment.Stretch;
            grid.Children.Add(bar);
        }
        // once the level is earned the number is the way to the pin: said in the purple's own
        // family, so "only just Ready" and "nearly there" read apart even when
        // the purple is a sliver; Pinned in bold purple
        var (_, _, stage2, pinned, _) = Bars.Experience(c);
        // two pixels kept inside the bar at either end, so a figure as wide as the bar ("!Pinned" in
        // bold, in a larger Options font) does not run to its edges; the column is wider in a
        // larger font too (the host grows a column's width with the drawing's)
        grid.Children.Add(MarkedFigure(new TextBlock
        {
            Text = text,
            Foreground = stage2 ? new SolidColorBrush(Color.FromRgb(220, 140, 215)) : Brushes.WhiteSmoke,
            FontWeight = pinned ? FontWeight.Bold : FontWeight.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0),
        }, mark));
        ToolTip.SetTip(grid, ExperienceTip(c, mark));
        return grid;
    }
}
