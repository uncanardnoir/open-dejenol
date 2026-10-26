using Mordor.Core;

namespace Mordor.Mod.QoL;

/// <summary>
/// The arithmetic of the Party Information bars (HP and SP, and experience), kept apart from the
/// controls that draw them so it can be checked without a window (the <c>mods</c> probe).
/// </summary>
public static class Bars
{
    /// <summary>
    /// How full a bar is: <paramref name="cur"/> over <paramref name="max"/>, held to 0..1. Null
    /// when there is no maximum (a character with no spell points), and the bar is drawn empty.
    /// The numbers are the ones the Party window shows (CurHits/MaxHits, CurSpellPoints/MaxSpellPoints).
    /// </summary>
    public static double? Fraction(long cur, long max)
    {
        if (max <= 0) return null;
        if (cur <= 0) return 0;
        if (cur >= max) return 1;
        return (double)cur / max;
    }

    /// <summary>The percentage for a tooltip, rounded down: 100% only when full, 0% only when empty.</summary>
    public static int Percent(double fraction)
    {
        if (fraction >= 1) return 100;
        if (fraction <= 0) return 0;
        int p = (int)Math.Floor(fraction * 100);
        return Math.Clamp(p, fraction > 0 ? 1 : 0, 99);
    }

    // the hits bar's three stops (BarColours, shared with the DPS Meter): full is green, half is yellow, empty is red
    public static readonly (byte R, byte G, byte B) Green = BarColours.HitsFull;
    public static readonly (byte R, byte G, byte B) Yellow = BarColours.HitsHalf;
    public static readonly (byte R, byte G, byte B) Red = BarColours.HitsEmpty;

    // the spell points bar's: full is a bright blue, half a violet, empty a dim purple - none of
    // them near the hits bar's, and the dimmest still clear of the dark track (#2a2a38)
    public static readonly (byte R, byte G, byte B) SpFull = BarColours.SpFull;
    public static readonly (byte R, byte G, byte B) SpHalf = BarColours.SpHalf;
    public static readonly (byte R, byte G, byte B) SpEmpty = BarColours.SpEmpty;

    /// <summary>
    /// Between three stops by a fraction: <paramref name="empty"/> at 0, <paramref name="half"/> at
    /// a half, <paramref name="full"/> at 1, each channel drawn straight between the two stops either
    /// side and rounded half up.
    /// </summary>
    private static (byte R, byte G, byte B) Ramp(double fraction, (byte R, byte G, byte B) empty, (byte R, byte G, byte B) half, (byte R, byte G, byte B) full)
    {
        double f = Math.Clamp(fraction, 0, 1);
        var (a, b, t) = f >= 0.5 ? (half, full, (f - 0.5) * 2) : (empty, half, f * 2);
        static byte Mix(byte x, byte y, double t) => (byte)Math.Floor(x + (y - x) * t + 0.5);
        return (Mix(a.R, b.R, t), Mix(a.G, b.G, t), Mix(a.B, b.B, t));
    }

    /// <summary>The hits bar's colour: green when full, through yellow at half, to red when empty.</summary>
    public static (byte R, byte G, byte B) Colour(double fraction) => Ramp(fraction, Red, Yellow, Green);

    /// <summary>The spell points bar's colour: bright blue when full, through violet at half, to a dim purple when empty.</summary>
    public static (byte R, byte G, byte B) SpColour(double fraction) => Ramp(fraction, SpEmpty, SpHalf, SpFull);

    /// <summary>
    /// The experience bar, in two stages, in the current guild.
    /// <list type="bullet">
    /// <item><c>ToNext</c>: stage one's fill, from this level's threshold (0) to the next level's
    /// (1). Whole once the level is earned.</item>
    /// <item><c>ToPin</c>: stage two's fill, from the next level's threshold (0) to the pin (1).
    /// Nought while short of the level; whole once pinned.</item>
    /// <item><c>Stage2</c>: the level is earned, so the bar shows <c>ToPin</c> in the second colour
    /// instead of <c>ToNext</c> in the first: purple fills from the left over a full green bar.</item>
    /// <item><c>Pinned</c>: at or past the pin (Character.IsPinned).</item>
    /// <item><c>State</c>: the toolbar's (Progress.ToolbarNeed), for the text.</item>
    /// </list>
    /// The three thresholds are the game's own: this level's (Character.ExpForThisLevel, fn12A1 at
    /// the level below, nought at level 1), the next's (ExpForNextLevel, fn12A1) and the pin
    /// (ExperienceCap, fn13ED: one short of the level after next), which always lies past the next
    /// level. Experience below this level's threshold (a drain) is an empty bar. Neither stage is
    /// whole short of its end: one short of the next level, or of the pin, is held to 0.999.
    /// </summary>
    public static (double ToNext, double ToPin, bool Stage2, bool Pinned, Progress.NeedState State) Experience(Character c)
    {
        var (state, _) = Progress.ToolbarNeed(c);
        long xp = c.Guild.Experience;
        long from = c.ExpForThisLevel(), next = c.ExpForNextLevel(), cap = c.ExperienceCap();
        if (xp < next) return (Math.Min(Along(xp, from, next), 0.999), 0, false, false, state);
        if (c.IsPinned) return (1, 1, true, true, state);
        return (1, Math.Min(Along(xp, next, cap), 0.999), true, false, state);
    }

    private static double Along(long v, long from, long to) =>
        to <= from ? 0 : Math.Clamp((double)(v - from) / (to - from), 0, 1);

    /// <summary>
    /// The experience bar's first colour, toward the next level: a green, a shade apart from the
    /// hits bar's full green (60, 200, 70), which has a column of its own.
    /// </summary>
    public static readonly (byte R, byte G, byte B) ToNextColour = BarColours.ToNext;

    /// <summary>
    /// The experience bar's second colour, toward the pin, and the whole bar once pinned: a magenta
    /// purple. Every stop of the spell points bar (SpFull, SpHalf, SpEmpty) has more blue than red;
    /// this has more red than blue, so it is not mistaken for any point on that ramp.
    /// </summary>
    public static readonly (byte R, byte G, byte B) ToPinColour = BarColours.ToPin;
}
