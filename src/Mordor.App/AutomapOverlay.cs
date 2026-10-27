using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// The mods' marks over the Automap (<see cref="ModUI.AutomapMarks"/>, API version 13): laid exactly over
/// picMaze (MapImage) and drawn on the map's own squares at the size the map is drawn now. Nothing here
/// is the original's: with no mod marking anything it has no marks, is hidden and draws nothing, so the
/// Automap is the original's.
///
/// Every edge is a whole device pixel and the same one the map's bitmap has: a square's place comes from
/// <see cref="AutomapLayout.DeviceEdges"/> for the same scale, so a tint sits exactly inside the square's
/// edge lines at any zoom and on any display scaling, crisp as the map itself is. The edge lines - a
/// square's walls and doors, map pixel 0 of each square - are never covered.
/// </summary>
public sealed class AutomapOverlay : Control
{
    public AutomapOverlay() { IsHitTestVisible = false; }

    /// <summary>The marks to draw, on the level shown.</summary>
    public IReadOnlyList<AutomapMark> Marks { get; private set; } = Array.Empty<AutomapMark>();

    /// <summary>The square a waiting pick would take, framed by the game itself, or null.</summary>
    public (int X, int Y)? PickAt { get; private set; }

    private int _levelH, _mazeW, _mazeH;
    private double _zoom = 1;

    /// <summary>What to draw and on what map: the level's height in squares, picMaze's size in map pixels, the zoom.</summary>
    public void Set(IReadOnlyList<AutomapMark> marks, (int X, int Y)? pickAt, int levelHeight, int mazeW, int mazeH, double zoom)
    {
        Marks = marks;
        PickAt = pickAt;
        _levelH = levelHeight; _mazeW = mazeW; _mazeH = mazeH; _zoom = zoom > 0 ? zoom : 1;
        IsVisible = marks.Count > 0 || pickAt != null;
        InvalidateVisual();
    }

    /// <summary>The map's zoom changed (a resize): the same marks at the new size.</summary>
    public void Rescale(double zoom)
    {
        if (Math.Abs(zoom - _zoom) < 1e-12) return;
        _zoom = zoom > 0 ? zoom : 1;
        InvalidateVisual();
    }

    /// <summary>The square framed while a pick waits: white, bold.</summary>
    public static readonly (byte R, byte G, byte B) PickColour = (255, 255, 255);

    public override void Render(DrawingContext dc)
    {
        if (!IsVisible || _mazeW <= 0 || _mazeH <= 0 || (Marks.Count == 0 && PickAt == null)) return;
        double rs = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var cols = AutomapLayout.DeviceEdges(_mazeW, _zoom * rs);
        var rows = AutomapLayout.DeviceEdges(_mazeH, _zoom * rs);
        // a resize being dragged stretches the map's bitmap for a moment (AutomapWindow.PlaceImage): the
        // marks are stretched with it, then drawn afresh when it is drawn again
        double fx = cols[_mazeW] > 0 && Bounds.Width > 0 ? Bounds.Width * rs / cols[_mazeW] : 1;
        double fy = rows[_mazeH] > 0 && Bounds.Height > 0 ? Bounds.Height * rs / rows[_mazeH] : 1;
        bool stretched = Math.Abs(fx - 1) > 1e-6 || Math.Abs(fy - 1) > 1e-6;
        using (stretched ? dc.PushTransform(Matrix.CreateScale(fx, fy)) : (DrawingContext.PushedState?)null)
        {
            var marks = new List<AutomapMark>(Marks);
            if (PickAt is { } p) marks.Add(AutomapMark.Frame(p.X, p.Y, PickColour, bold: true));
            Paint(dc, marks, _levelH, cols, rows, _zoom * rs, rs);
        }
    }

    /// <summary>
    /// A square's inside in device pixels: from map pixel 1 to map pixel 13 of it, its edge lines (map
    /// pixel 0 and the next square's 0, the shared edge) left alone. Squares are laid
    /// <see cref="AutomapLayout.Pitch"/> apart, north up: the level's last row at the top.
    /// </summary>
    public static (int L, int T, int R, int B) Inside(int x, int y, int levelHeight, int[] cols, int[] rows, int inset = 1)
    {
        int px = x * AutomapLayout.Pitch, py = (levelHeight - 1 - y) * AutomapLayout.Pitch;
        int a = px + inset, b = px + AutomapLayout.Pitch - inset + 1;
        int c = py + inset, d = py + AutomapLayout.Pitch - inset + 1;
        if (a < 0 || c < 0 || b >= cols.Length || d >= rows.Length || b <= a || d <= c) return (0, 0, 0, 0);
        return (cols[a], rows[c], cols[b], rows[d]);
    }

    /// <summary>A square's middle in device pixels.</summary>
    public static Point Middle(int x, int y, int levelHeight, int[] cols, int[] rows)
    {
        var (l, t, r, b) = Inside(x, y, levelHeight, cols, rows);
        return new Point((l + r) / 2.0, (t + b) / 2.0);
    }

    /// <summary>The tint's strength: the map shows through it.</summary>
    public const byte TintAlpha = 153;

    /// <summary>
    /// The marks drawn on <paramref name="dc"/>, whose units are device pixels over
    /// <paramref name="rs"/> (the window's own), the map's device edges given: tints first, then frames,
    /// then arrows. <paramref name="scale"/> is device pixels per map pixel.
    /// </summary>
    public static void Paint(DrawingContext dc, IReadOnlyList<AutomapMark> marks, int levelHeight, int[] cols, int[] rows, double scale, double rs)
    {
        rs = rs > 0 ? rs : 1;
        Rect R(int l, int t, int r, int b) => new(l / rs, t / rs, (r - l) / rs, (b - t) / rs);
        IBrush Solid((byte R, byte G, byte B) c, byte a = 255) => new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
        var dark = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));

        foreach (var m in marks)
        {
            if (m.Kind != AutomapMarkKind.Tint) continue;
            var (l, t, r, b) = Inside(m.X, m.Y, levelHeight, cols, rows);
            if (r > l && b > t) dc.FillRectangle(Solid(m.Colour, TintAlpha), R(l, t, r, b));
        }
        foreach (var m in marks)
        {
            if (m.Kind != AutomapMarkKind.Frame) continue;
            var (l, t, r, b) = Inside(m.X, m.Y, levelHeight, cols, rows);
            // the ring: two map pixels thick (three bold), on the square's own device edges
            var (il, it, ir, ib) = Inside(m.X, m.Y, levelHeight, cols, rows, m.Bold ? 4 : 3);
            if (r <= l || b <= t || ir <= il || ib <= it) continue;
            var c = Solid(m.Colour);
            Ring(dc, c, R(l, t, r, b), R(il, it, ir, ib));
            // a dark line one device pixel wide inside it: the ring reads on the grey of unmapped
            // ground as well as on the black of explored
            if (ir - il > 2 && ib - it > 2) Ring(dc, dark, R(il, it, ir, ib), R(il + 1, it + 1, ir - 1, ib - 1));
        }
        foreach (var m in marks)
        {
            if (m.Kind != AutomapMarkKind.Arrow || (m.X == m.ToX && m.Y == m.ToY)) continue;
            var from = Middle(m.X, m.Y, levelHeight, cols, rows);
            var to = Middle(m.ToX, m.ToY, levelHeight, cols, rows);
            if (from == to) continue;
            double w = Math.Max(2, Math.Round(2 * scale));          // device pixels
            double head = Math.Max(8, 6 * scale);
            var v = to - from;
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            var u = new Vector(v.X / len, v.Y / len);
            var n = new Vector(-u.Y, u.X);
            var neck = to - u * Math.Min(head, len * 0.6);
            Point P(Point d) => new(d.X / rs, d.Y / rs);
            var shaft = new LineGeometry(P(from), P(neck));
            dc.DrawGeometry(null, new Pen(dark, (w + 2) / rs, lineCap: PenLineCap.Round), shaft);
            var tip = new StreamGeometry();
            using (var g = tip.Open())
            {
                g.BeginFigure(P(to), true);
                g.LineTo(P(neck + n * (head * 0.55)));
                g.LineTo(P(neck - n * (head * 0.55)));
                g.EndFigure(true);
            }
            dc.DrawGeometry(Solid(m.Colour), new Pen(dark, 1.5 / rs, lineJoin: PenLineJoin.Round), tip);
            dc.DrawGeometry(null, new Pen(Solid(m.Colour), w / rs, lineCap: PenLineCap.Round), shaft);
        }
    }

    private static void Ring(DrawingContext dc, IBrush brush, Rect outer, Rect inner)
    {
        dc.FillRectangle(brush, new Rect(outer.Left, outer.Top, outer.Width, inner.Top - outer.Top));
        dc.FillRectangle(brush, new Rect(outer.Left, inner.Bottom, outer.Width, outer.Bottom - inner.Bottom));
        dc.FillRectangle(brush, new Rect(outer.Left, inner.Top, inner.Left - outer.Left, inner.Height));
        dc.FillRectangle(brush, new Rect(inner.Right, inner.Top, outer.Right - inner.Right, inner.Height));
    }
}
