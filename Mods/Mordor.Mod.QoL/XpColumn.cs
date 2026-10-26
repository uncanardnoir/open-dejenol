using Mordor.Core;

namespace Mordor.Mod.QoL;

/// <summary>
/// The "Next" column in the Party Information window: the experience a member still needs for the
/// next level in their current guild, and once that is earned what it still needs to the pin. The
/// figures are the toolbar's own (Progress.ToolbarNeed, the rule behind "You need N more experience
/// for level L." and "... to pin"), never worked out again here.
/// </summary>
public static class XpColumn
{
    /// <summary>
    /// What a member's cell says, in the bar's two stages (Bars.Experience): short of the next
    /// level, the amount still needed for it; once the level is earned, the amount still needed to
    /// the pin (both the toolbar's own figures, in <see cref="Short"/>'s form); "Pinned" at or past
    /// the pin (Character.IsPinned - experience left above a lowered pin is pinned by the game's own
    /// test, where the toolbar's figure would be negative).
    /// </summary>
    public static string Cell(Character c)
    {
        var (state, amount) = Progress.ToolbarNeed(c);
        return state switch
        {
            Progress.NeedState.Short => Short(amount),
            Progress.NeedState.Ready when amount > 0 => Short(amount),
            _ => "Pinned",
        };
    }

    /// <summary>
    /// The experience bar's tooltip: short of the next level, how far along it is and both amounts, "N to next level, M to pin".
    /// Ready is the toolbar's state for experience at or past the next level and not equal to the
    /// pin; that includes experience left above the pin (a level drain lowers the pin under it),
    /// where the toolbar's own figure is negative. Such a total is pinned by the game's own test
    /// (Character.IsPinned, at or past the cap), and the next award takes it back down to the pin
    /// (sub0F11, Combat.GiveExperience), so it is not called an amount "to pin".
    /// </summary>
    public static string Tip(Character c)
    {
        var (state, amount) = Progress.ToolbarNeed(c);
        int next = c.Guild.Level + 1;
        long xp = c.Guild.Experience, from = c.ExpForThisLevel(), to = c.ExpForNextLevel();
        long toPin = c.ExperienceCap() - xp;
        double way = to > from ? Math.Clamp((double)(xp - from) / (to - from), 0, 1) : 0;
        return state switch
        {
            // two lines, the progress and then the two amounts: one line of it wrapped in a larger
            // Options font and left "pin" alone on the second
            Progress.NeedState.Short => $"Level {next}: {Bars.Percent(way)}% of the way\n{amount:N0} to next level, {toPin:N0} to pin",
            Progress.NeedState.Pinned => "Pinned: make the level, or experience is lost",
            _ when amount > 0 => $"Ready to level {next} - {amount:N0} more to the pin",
            _ => $"Ready to level {next} - {-amount:N0} past the pin: make the level, or the next experience earned takes it back to the pin",
        };
    }

    private static readonly (long Unit, string Suffix)[] Units =
    {
        (1_000_000_000_000L, "T"), (1_000_000_000L, "B"), (1_000_000L, "M"), (1_000L, "K"),
    };

    /// <summary>
    /// Two significant figures and a suffix. Below 1,000 the plain number (950). From 1,000 up the
    /// number is first rounded to two significant figures, half away from zero, in whole numbers
    /// (so no floating-point doubt: 9,950 is exactly half-way and goes up), and then written in the
    /// largest unit it reaches: one decimal under ten units (3,512 -> 3.5K, 1,000,000 -> 1.0M,
    /// 2,480,000 -> 2.5M), none from ten (12,345 -> 12K). A rounding that carries into a third digit
    /// shows it (99,960 -> 100K; 9,950 -> 10K), and one that reaches the next unit moves to it
    /// (999,600 -> 1.0M).
    /// </summary>
    public static string Short(long n)
    {
        if (n < 0) return "-" + Short(-n);
        if (n < 1000) return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int digits = n.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        long scale = 1;
        for (int i = 0; i < digits - 2; i++) scale *= 10;
        long r = (n + scale / 2) / scale * scale;   // half away from zero, n being positive
        foreach (var (unit, suffix) in Units)
        {
            if (r < unit) continue;
            long whole = r / unit;
            if (whole >= 10) return whole + suffix;
            long tenth = r % unit / (unit / 10);
            return whole + "." + tenth + suffix;
        }
        return r.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
