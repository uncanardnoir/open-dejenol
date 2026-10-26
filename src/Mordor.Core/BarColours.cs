namespace Mordor.Core;

/// <summary>
/// The port's bar colours, kept in one place for every window that draws a bar: the Quality of
/// Life mod's Party Information bars (hits, spell points, experience; its <c>Bars</c>) and the DPS
/// Meter mod's damage bars. None of them is the original's - the game drew no bars - so they are the
/// port's own, and each window takes them from here rather than a shade of its own. Red, green and
/// blue, 0 to 255; no Avalonia here, so a mod's arithmetic can read them where there is no window.
/// </summary>
public static class BarColours
{
    /// <summary>A bar's dark track, under its fill.</summary>
    public static readonly (byte R, byte G, byte B) Track = (0x2a, 0x2a, 0x38);

    // the hits bar's three stops: full is green, half is yellow, empty is red
    public static readonly (byte R, byte G, byte B) HitsFull = (60, 200, 70);
    public static readonly (byte R, byte G, byte B) HitsHalf = (230, 210, 50);
    public static readonly (byte R, byte G, byte B) HitsEmpty = (220, 50, 40);

    // the spell points bar's: full is a bright blue, half a violet, empty a dim purple - none of
    // them near the hits bar's, and the dimmest still clear of the dark track
    public static readonly (byte R, byte G, byte B) SpFull = (70, 150, 255);
    public static readonly (byte R, byte G, byte B) SpHalf = (150, 95, 235);
    public static readonly (byte R, byte G, byte B) SpEmpty = (120, 60, 150);

    /// <summary>
    /// The experience bar's first colour, toward the next level (the Next column's green): a shade
    /// apart from the hits bar's full green, which has a column of its own.
    /// </summary>
    public static readonly (byte R, byte G, byte B) ToNext = (50, 175, 95);

    /// <summary>
    /// The experience bar's second colour, toward the pin (the Next column's purple): a magenta
    /// purple. Every stop of the spell points bar has more blue than red; this has more red than
    /// blue, so it is not mistaken for any point on that ramp.
    /// </summary>
    public static readonly (byte R, byte G, byte B) ToPin = (205, 70, 185);

    /// <summary>
    /// How strong a bar is drawn when text is written over it, as the Next column's is: half, over
    /// the dark track, so plain <see cref="TextOver"/> reads on every colour above without a shadow.
    /// </summary>
    public const byte UnderTextAlpha = 0x80;

    /// <summary>The text written over such a bar: the Party window's own plain white (WhiteSmoke).</summary>
    public static readonly (byte R, byte G, byte B) TextOver = (0xf5, 0xf5, 0xf5);
}
