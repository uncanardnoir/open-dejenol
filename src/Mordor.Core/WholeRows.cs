namespace Mordor.Core;

/// <summary>
/// A list that stops only on a whole row, as a VB scroll bar's Value is a whole row (the Spells
/// page's vsbSpells, FRMCHAR3.BAS:881-912). The port's lists scroll by the pixel, and one brought
/// down to its last row had left the lower part of a row showing at its top, under the page's
/// headings. Mordor.App's ObjectsForm.LinkScrollBar uses these two; selfcheck holds them.
/// </summary>
public static class WholeRows
{
    /// <summary>
    /// The room below a list's last row that puts its bottom a whole number of rows below the top
    /// of its viewport when it is scrolled right down: the viewport's height less the rows' top
    /// margin, modulo a row. Then the furthest the list scrolls is a whole number of rows too.
    /// </summary>
    public static double Tail(double viewport, double topMargin, double rowHeight)
    {
        if (rowHeight <= 0 || viewport <= 0) return 0;
        double tail = (viewport - topMargin) % rowHeight;
        if (tail < 0) tail += rowHeight;
        // a hair short of a row is a whole row, not a row's worth of room
        return rowHeight - tail < 0.01 ? 0 : Math.Round(tail, 2);
    }

    /// <summary>
    /// The whole row nearest a scroll offset, within the list's range. A selection brought into view
    /// at the bottom stops a few pixels short of a row (the top margin and <see cref="Tail"/>), and
    /// one brought in at the top a margin past one, so the nearest is the row that shows it whole.
    /// </summary>
    public static double Snap(double offset, double rowHeight, double max)
    {
        if (rowHeight <= 0) return offset;
        return Math.Clamp(Math.Round(offset / rowHeight) * rowHeight, 0, Math.Max(0, max));
    }
}
