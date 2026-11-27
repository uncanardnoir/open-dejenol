using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;
using Mordor.Modding;

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
    // the game's navy role (ThemeKeys): the one brush the theme recolours; the Dark blue when no game publishes it
    private static IBrush Text => Application.Current?.FindResource(ThemeKeys.Navy) as IBrush ?? Brushes.CornflowerBlue;

    // The experience bar and the text over it take the game's bar roles (ThemeKeys, API version 18):
    // the brushes the theme recolours in place, so a change of theme recolours the bar and its figure
    // with nothing of the mod's own. Each falls back to Dark's colour where no game publishes the
    // roles (the probes): the fills at half strength (UnderTextAlpha), the figure white or pink.
    private static IBrush Role(string key, IBrush otherwise) => Application.Current?.FindResource(key) as IBrush ?? otherwise;
    private static IBrush ToNextFill => Role(ThemeKeys.XpToNext, new SolidColorBrush(Rgb(Bars.ToNextColour, BarColours.UnderTextAlpha)));
    private static IBrush ToPinFill => Role(ThemeKeys.XpToPin, new SolidColorBrush(Rgb(Bars.ToPinColour, BarColours.UnderTextAlpha)));
    private static IBrush PinnedFill => Role(ThemeKeys.XpPinned, new SolidColorBrush(Rgb(Bars.ToPinColour, BarColours.UnderTextAlpha)));
    private static IBrush XpTrack => Role(ThemeKeys.XpTrack, Track);
    private static IBrush FigureInk => Role(ThemeKeys.BarInk, new SolidColorBrush(Rgb(BarColours.TextOver)));
    private static IBrush PastInk => Role(ThemeKeys.BarInkPast, new SolidColorBrush(Color.FromRgb(220, 140, 215)));

    private static Color Rgb((byte R, byte G, byte B) c, byte alpha = 255) => Color.FromArgb(alpha, c.R, c.G, c.B);

    /// <summary>A bar: a track (dark unless <paramref name="track"/> is given) with <paramref name="fraction"/> of it filled from the left.</summary>
    private static Control Bar(double? fraction, IBrush fill, double height, IBrush? track = null)
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
        if (f > 0) inside.Children.Add(new Border { Background = fill, CornerRadius = new CornerRadius(1) });
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
                Bar(hp, new SolidColorBrush(Rgb(Bars.Colour(hp ?? 0))), 4),
                Bar(sp, new SolidColorBrush(Rgb(Bars.SpColour(sp ?? 0))), 4),
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
    /// threshold toward the next's over the track; once the level is earned the bar stays full green
    /// and purple fills over it from the left, from the next level's threshold toward the pin;
    /// pinned, the whole bar purple. In the game's bar roles (ThemeKeys.XpToNext, XpToPin, XpPinned,
    /// XpTrack): half strength in Dark, solid in Light.
    /// </summary>
    private static Control ExperienceBar(Character c, double height)
    {
        var (toNext, toPin, stage2, pinned, _) = Bars.Experience(c);
        if (!stage2) return Bar(toNext, ToNextFill, height, XpTrack);
        return Bar(toPin, pinned ? PinnedFill : ToPinFill, height, ToNextFill);
    }

    private static string ExperienceTip(Character c, Mark? mark) =>
        mark == null ? XpColumn.Tip(c) : XpColumn.Tip(c) + "\n" + mark.Value.Tip;

    /// <summary>
    /// The quest mark as drawn: bold, in the row's own face and size so the row keeps its 13 pixels,
    /// in the game's quest roles (ThemeKeys.QuestMark, QuestDone: the gold and the green in Dark,
    /// darker in Light, to read over the solid bar), its own colour where no game publishes them.
    /// The tick is a shape (ModGlyphs, 1.14.1), not the character, so it is the same on every system:
    /// the faces the game bundles away from Windows have no U+2713. The ! stays a character.
    /// </summary>
    private static TextBlock MarkBlock(Mark m)
    {
        bool done = m.Glyph == QuestMark.DoneMark;
        var block = done ? ModGlyphs.Block(ModSymbol.Check) : new TextBlock { Text = m.Glyph };
        block.Foreground = Role(done ? ThemeKeys.QuestDone : ThemeKeys.QuestMark, new SolidColorBrush(Rgb(m.Colour)));
        block.FontWeight = FontWeight.Bold;
        block.VerticalAlignment = VerticalAlignment.Center;
        block.Margin = new Thickness(0, 0, 1, 0);
        return block;
    }

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
            var bar = ExperienceBar(c, double.NaN);
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
            Foreground = stage2 ? PastInk : FigureInk,
            FontWeight = pinned ? FontWeight.Bold : FontWeight.Normal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0),
        }, mark));
        ToolTip.SetTip(grid, ExperienceTip(c, mark));
        return grid;
    }
}
