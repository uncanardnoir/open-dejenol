using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

/// <summary>
/// CI's picture tools, beside the readability pass (<see cref="ContrastCheck"/>) and the playthrough
/// (<see cref="PlaythroughCheck"/>) that draw the pictures. Neither opens a window nor needs a game folder:
/// they read and write PNGs with SkiaSharp, the library Avalonia draws with.
///
///   Mordor.UiCheck --compare &lt;baseline dir&gt; &lt;current dir&gt; &lt;out dir&gt;
///       the same-named pictures of two runs on one machine, pixel by pixel, each found unchanged,
///       minor (a few scattered pixels: the noise of <see cref="Measure"/>; or text moved by a pixel or
///       two, <see cref="FindShifts"/>) or changed. Into &lt;out dir&gt;:
///       for each changed picture, before.png, after.png and an enlarged crop of each changed area
///       (zoom-1-before.png, zoom-1-after.png, ...) under changed/&lt;name&gt;/, the same for each minor one
///       under minor/&lt;name&gt;/; compare.json, every picture's counts; and summary.md, a short table for
///       the GitHub run summary. An empty or missing baseline is a first run. Always exit 0: it
///       reports, it never fails the job (2 for a wrong command line).
///   Mordor.UiCheck --gallery &lt;artifacts root&gt; &lt;out dir&gt;
///       every machine's pictures side by side: &lt;out dir&gt;/index.html with the pictures and their
///       thumbnails copied beside it, one folder that opens from disk. A changed picture opens on its
///       change: the zoomed crops before and after, and the whole picture with the areas ringed, flipped
///       between before and after by a click or F. Exit 0 (2 for a wrong command line).
///   Mordor.UiCheck --selftest-pictures
///       the checks of the two tools alone (<see cref="SelfTest"/>), with no game folder.
///
/// What is compared: the readability pictures (the PNGs at the folder's top, Light_* and Dark_*), and
/// of the playthrough only <see cref="SteadyPlaythrough"/>. The rest of the playthrough differs from
/// run to run by design - the fights' blows and damage, and the clock and play time on the character
/// window in town - so it is shown in the gallery and never compared.
/// </summary>
static class PictureTools
{
    /// <summary>The readability pass's list of pictures and their failures, beside the pictures.</summary>
    public const string ChecksFile = "checks.json";

    /// <summary>Where a machine's comparison is put in its pictures folder, for the gallery to find.</summary>
    public const string CompareFolder = "compare";

    /// <summary>
    /// The playthrough pictures that come out the same every run: the main menu it ends on. Two runs
    /// of the playthrough on one machine (2026-11-27) differed in every other picture from the
    /// Sparring Room on (the fights' dice), and in 10-town (the clock on the character window).
    /// </summary>
    public static readonly string[] SteadyPlaythrough = { "playthrough/10-main-menu.png" };

    /// <summary>One failure under a picture in checks.json. A known one (LayoutCheck.KnownFindings) carries its reason and does not fail.</summary>
    public sealed record Failure(string Kind, string Message, bool Known, string? Reason);

    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>checks.json: { "pictures": [ { "file", "failures": [ { "kind", "message", "known", "reason"? } ] } ] }, in the order taken.</summary>
    public static string ChecksJson(IEnumerable<(string Picture, List<Failure> Failures)> pictures)
    {
        var list = new JsonArray();
        foreach (var (picture, failures) in pictures)
        {
            var f = new JsonArray();
            foreach (var x in failures)
            {
                var o = new JsonObject { ["kind"] = x.Kind, ["message"] = x.Message, ["known"] = x.Known };
                if (x.Reason != null) o["reason"] = x.Reason;
                f.Add(o);
            }
            list.Add(new JsonObject { ["file"] = picture, ["failures"] = f });
        }
        return new JsonObject { ["pictures"] = list }.ToJsonString(Indented);
    }

    // ================================================================ --compare

    // The noise threshold. Four of CI's five machines draw every picture the same from run to run;
    // GitHub's windows-latest machines do not quite: two runs a day apart (2026-11-27) differed by 3 to
    // 9 scattered pixels on six pictures (the Exchange, Light and Dark; the Hall of Records, Light and
    // Dark; Ordering Info; the town's Guild) - anti-aliasing at the edges of text and lines, as the
    // runner pool hands out different processors. A pixel is counted only when, as pixelmatch
    // (github.com/mapbox/pixelmatch) measures it, its colour moved more than Tolerance and it is not
    // anti-aliasing; a picture whose counted pixels are few and scattered is minor, not changed.

    /// <summary>
    /// How far a pixel's colour may move and not be counted, as pixelmatch's threshold: the YIQ
    /// distance (0 to 1 of the largest there is). Its default, 0.1.
    /// </summary>
    public const double Tolerance = 0.1;

    /// <summary>A picture with no more counted pixels than this, none of them in a clump bigger than <see cref="MinorLargestClump"/>, is minor.</summary>
    public const int MinorMostPixels = 16;

    /// <summary>The biggest clump of counted pixels (touching at a side or a corner) that a minor picture may have.</summary>
    public const int MinorLargestClump = 3;

    /// <summary>Counted pixels this close to each other (in pixels, in any direction) are one area of a change: one box ringed, one zoomed crop.</summary>
    const int AreaGap = 8;

    /// <summary>How far round an area its zoomed crop reaches, and the most of the picture one crop shows (before it is enlarged).</summary>
    const int CropMargin = 8, CropMostWidth = 240, CropMostHeight = 160;

    /// <summary>How many of a picture's areas, the biggest first, get a zoomed crop; and how many are ringed at most.</summary>
    const int MostZooms = 3, MostAreas = 60;

    public enum Verdict { Unchanged, Minor, Changed }

    /// <summary>
    /// One area of a change: its box and how many of its pixels count; and, for the first
    /// <see cref="MostZooms"/>, the crop round it (CropX..) enlarged <see cref="Zoom"/> times into
    /// zoom-n-before.png and zoom-n-after.png (Zoom 0: none).
    /// </summary>
    public sealed record Area(int X, int Y, int Width, int Height, int Pixels, int Zoom = 0, int CropX = 0, int CropY = 0, int CropWidth = 0, int CropHeight = 0);

    /// <summary>
    /// One picture against its baseline. Pixels: every pixel that differs at all; Faint: those whose
    /// colour moved no more than <see cref="Tolerance"/>; AntiAliased: those that are anti-aliasing;
    /// Shifted: those in places found moved a pixel or two (<see cref="FindShifts"/>), each way they
    /// moved one of Moves; Counted: the rest, in Clumps, the biggest LargestClump pixels. The box
    /// (X..) is round the counted pixels of a changed picture, round every differing pixel of a minor one.
    /// </summary>
    public sealed record Change(string Name, Verdict Verdict, int Pixels, int Faint, int AntiAliased, int Counted, int Clumps, int LargestClump,
                                int X, int Y, int Width, int Height, string SizeBefore, string SizeAfter, string Folder, List<Area> Areas,
                                int Shifted, List<Move> Moves);

    /// <summary>One way places moved: Dx right and Dy down (negative: left, up), in how many places, how many counted pixels.</summary>
    public sealed record Move(int Dx, int Dy, int Places, int Pixels);

    /// <summary>The most a picture's places moved, in pixels either way.</summary>
    static int MostMoved(IEnumerable<Move> moves) => moves.Select(v => Math.Max(Math.Abs(v.Dx), Math.Abs(v.Dy))).DefaultIfEmpty(0).Max();

    /// <summary>Moves in words: "text moved 1 px left in 3 places, 2 px right and 1 px up in 1 place".</summary>
    static string MovedWords(IEnumerable<Move> moves)
    {
        string Way(int dx, int dy)
        {
            var parts = new List<string>();
            if (dx != 0) parts.Add($"{Math.Abs(dx)} px {(dx < 0 ? "left" : "right")}");
            if (dy != 0) parts.Add($"{Math.Abs(dy)} px {(dy < 0 ? "up" : "down")}");
            return string.Join(" and ", parts);
        }
        var list = moves.Select(v => $"{Way(v.Dx, v.Dy)} in {v.Places} place{(v.Places == 1 ? "" : "s")}").ToList();
        return list.Count == 0 ? "" : "text moved " + string.Join(", ", list);
    }

    public sealed record CompareResult(bool FirstRun, List<Change> Changed, List<Change> Minor, List<string> Added, List<string> Removed, int Unchanged);

    /// <summary>The pictures a folder holds that are compared, by their path in it with '/' (Light_x.png, playthrough/10-main-menu.png).</summary>
    static SortedSet<string> Comparable(string dir)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(dir)) return names;
        foreach (var f in Directory.GetFiles(dir, "*.png")) names.Add(Path.GetFileName(f));
        foreach (var p in SteadyPlaythrough)
            if (File.Exists(Path.Combine(dir, p))) names.Add(p);
        return names;
    }

    /// <summary>
    /// A PNG as 32-bit pixels, BGRA, row by row; or such a picture moved <see cref="Dx"/>, <see cref="Dy"/>
    /// (<see cref="Moved"/>), its pixel (x, y) at (x + Dx, y + Dy), what it covers from Left to Right and Top to Bottom.
    /// </summary>
    sealed class Pixels
    {
        public int Width, Height, Dx, Dy;
        public uint[] Data = Array.Empty<uint>();
        public int Left => Dx;
        public int Top => Dy;
        public int Right => Dx + Width - 1;
        public int Bottom => Dy + Height - 1;
        public bool Has(int x, int y) => x >= Left && y >= Top && x <= Right && y <= Bottom;
        public uint At(int x, int y) => Has(x, y) ? Data[(y - Dy) * Width + x - Dx] : 0;

        /// <summary>The same pixels moved <paramref name="dx"/> right and <paramref name="dy"/> down (not copied).</summary>
        public Pixels Moved(int dx, int dy) => new() { Width = Width, Height = Height, Data = Data, Dx = Dx + dx, Dy = Dy + dy };

        public static Pixels Load(string path)
        {
            using var decoded = SKBitmap.Decode(path) ?? throw new InvalidDataException($"{path} is not a picture SkiaSharp can read");
            using var bgra = decoded.Copy(SKColorType.Bgra8888) ?? throw new InvalidDataException($"{path} could not be read as 32-bit pixels");
            var p = new Pixels { Width = bgra.Width, Height = bgra.Height };
            p.Data = MemoryMarshal.Cast<byte, uint>(bgra.GetPixelSpan()).ToArray();
            return p;
        }
    }

    static void SavePng(SKBitmap bmp, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    static void SavePng(uint[] px, int w, int h, string path)
    {
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        Marshal.Copy(MemoryMarshal.Cast<uint, int>(px).ToArray(), 0, bmp.GetPixels(), px.Length);
        SavePng(bmp, path);
    }

    // ---------------------------------------------------------------- the measure (pixelmatch's)

    static double Blend(uint c, double a) => 255 + (c - 255.0) * a;

    /// <summary>
    /// pixelmatch's colorDelta: the YIQ distance of two colours (alpha blended over white), its sign
    /// whether the first is the darker; or, <paramref name="yOnly"/>, the difference of their brightness.
    /// </summary>
    static double Delta(uint c1, uint c2, bool yOnly)
    {
        if (c1 == c2) return 0;
        double a1 = (c1 >> 24) / 255.0, a2 = (c2 >> 24) / 255.0;
        double r1 = Blend((c1 >> 16) & 0xFF, a1), g1 = Blend((c1 >> 8) & 0xFF, a1), b1 = Blend(c1 & 0xFF, a1);
        double r2 = Blend((c2 >> 16) & 0xFF, a2), g2 = Blend((c2 >> 8) & 0xFF, a2), b2 = Blend(c2 & 0xFF, a2);
        double y1 = r1 * 0.29889531 + g1 * 0.58662247 + b1 * 0.11448223;
        double y2 = r2 * 0.29889531 + g2 * 0.58662247 + b2 * 0.11448223;
        double y = y1 - y2;
        if (yOnly) return y;
        double i = (r1 * 0.59597799 - g1 * 0.27417610 - b1 * 0.32180189) - (r2 * 0.59597799 - g2 * 0.27417610 - b2 * 0.32180189);
        double q = (r1 * 0.21147017 - g1 * 0.52261711 + b1 * 0.31114694) - (r2 * 0.21147017 - g2 * 0.52261711 + b2 * 0.31114694);
        double delta = 0.5053 * y * y + 0.299 * i * i + 0.1957 * q * q;
        return y1 > y2 ? -delta : delta;
    }

    /// <summary>The largest YIQ distance there is (black to white), pixelmatch's 35215.</summary>
    const double LargestDelta = 35215;

    /// <summary>
    /// pixelmatch's antialiased: is the pixel at (x1, y1) of <paramref name="img"/> anti-aliasing? It is
    /// when it has at most two neighbours of its own colour, both a darker and a brighter neighbour, and
    /// the darkest or the brightest of them sits in a flat area (three or more neighbours of its own
    /// colour) in both pictures: the pixel is a blend on the edge between them.
    /// </summary>
    static bool AntiAliased(Pixels img, int x1, int y1, Pixels other)
    {
        int x0 = Math.Max(x1 - 1, img.Left), y0 = Math.Max(y1 - 1, img.Top), x2 = Math.Min(x1 + 1, img.Right), y2 = Math.Min(y1 + 1, img.Bottom);
        uint c = img.At(x1, y1);
        int zeroes = x1 == x0 || x1 == x2 || y1 == y0 || y1 == y2 ? 1 : 0;
        double min = 0, max = 0;
        int minX = 0, minY = 0, maxX = 0, maxY = 0;
        for (int x = x0; x <= x2; x++)
            for (int y = y0; y <= y2; y++)
            {
                if (x == x1 && y == y1) continue;
                double delta = Delta(c, img.At(x, y), true);
                if (delta == 0) { if (++zeroes > 2) return false; }
                else if (delta < min) { min = delta; minX = x; minY = y; }
                else if (delta > max) { max = delta; maxX = x; maxY = y; }
            }
        if (min == 0 || max == 0) return false;
        return (ManySiblings(img, minX, minY) && ManySiblings(other, minX, minY))
            || (ManySiblings(img, maxX, maxY) && ManySiblings(other, maxX, maxY));
    }

    /// <summary>pixelmatch's hasManySiblings: three or more of the pixel's neighbours (the picture's edge counting as one) are its very colour.</summary>
    static bool ManySiblings(Pixels img, int x1, int y1)
    {
        if (!img.Has(x1, y1)) return false;
        int x0 = Math.Max(x1 - 1, img.Left), y0 = Math.Max(y1 - 1, img.Top), x2 = Math.Min(x1 + 1, img.Right), y2 = Math.Min(y1 + 1, img.Bottom);
        uint c = img.At(x1, y1);
        int zeroes = x1 == x0 || x1 == x2 || y1 == y0 || y1 == y2 ? 1 : 0;
        for (int x = x0; x <= x2; x++)
            for (int y = y0; y <= y2; y++)
            {
                if (x == x1 && y == y1) continue;
                if (img.At(x, y) == c && ++zeroes > 2) return true;
            }
        return false;
    }

    /// <summary>The groups of set pixels, each pixel within <paramref name="reach"/> of another of its group (1: touching at a side or a corner): each one's box and size, the biggest first.</summary>
    static List<(int X0, int Y0, int X1, int Y1, int Pixels)> Groups(bool[] mask, int w, int h, int reach)
        => Groups(mask, w, h, reach, null);

    /// <summary><see cref="Groups(bool[], int, int, int)"/>, and into <paramref name="members"/> (when given) each group's pixels by their index, in the same order.</summary>
    static List<(int X0, int Y0, int X1, int Y1, int Pixels)> Groups(bool[] mask, int w, int h, int reach, List<List<int>>? members)
    {
        var list = new List<(int, int, int, int, int)>();
        var pixels = new List<List<int>>();
        var seen = new bool[mask.Length];
        var stack = new Stack<int>();
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start]) continue;
            seen[start] = true;
            stack.Push(start);
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, n = 0;
            var own = members != null ? new List<int>() : null;
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                n++;
                own?.Add(i);
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
                for (int yy = Math.Max(0, y - reach); yy <= Math.Min(h - 1, y + reach); yy++)
                    for (int xx = Math.Max(0, x - reach); xx <= Math.Min(w - 1, x + reach); xx++)
                    {
                        int j = yy * w + xx;
                        if (mask[j] && !seen[j]) { seen[j] = true; stack.Push(j); }
                    }
            }
            list.Add((x0, y0, x1, y1, n));
            if (own != null) pixels.Add(own);
        }
        var order = Enumerable.Range(0, list.Count).OrderByDescending(k => list[k].Item5).ThenBy(k => list[k].Item2).ThenBy(k => list[k].Item1).ToList();
        if (members != null) members.AddRange(order.Select(k => pixels[k]));
        return order.Select(k => list[k]).ToList();
    }

    /// <summary>What two pictures' pixels say, before anything is written.</summary>
    sealed class Measured
    {
        public int Width, Height;   // the larger of the two each way
        public bool SizeChanged;
        public int Differing, Faint, AntiAliased, Counted, Clumps, LargestClump;
        public bool[] Differs = Array.Empty<bool>(), Counts = Array.Empty<bool>();
        /// <summary>The counted pixels found moved (<see cref="FindShifts"/>), no longer counted; and each moved place's offset and pixels.</summary>
        public int Shifted;
        public List<(int Dx, int Dy, int Pixels)> Moves = new();
        public Verdict Verdict;
    }

    /// <summary>
    /// The noise threshold, on two pictures. Every pixel that differs at all is kept count of; of
    /// those, one whose colour moved no more than <see cref="Tolerance"/> is faint, and one that is
    /// anti-aliasing in either picture (<see cref="AntiAliased"/>) is not counted either. Where the
    /// sizes differ, every pixel outside the smaller counts. Where they do not, the counted pixels
    /// found moved a pixel or two (<see cref="FindShifts"/>) are taken out of the count. The verdict:
    /// unchanged when no pixel differs; changed when the size changed, more than
    /// <see cref="MinorMostPixels"/> pixels count, or any clump of them is bigger than
    /// <see cref="MinorLargestClump"/>; else minor.
    /// </summary>
    static Measured Measure(Pixels a, Pixels b)
    {
        var m = new Measured { Width = Math.Max(a.Width, b.Width), Height = Math.Max(a.Height, b.Height), SizeChanged = a.Width != b.Width || a.Height != b.Height };
        int w = m.Width, h = m.Height;
        m.Differs = new bool[w * h];
        m.Counts = new bool[w * h];
        double most = LargestDelta * Tolerance * Tolerance;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool inBoth = a.Has(x, y) && b.Has(x, y);
                if (inBoth)
                {
                    uint ca = a.At(x, y), cb = b.At(x, y);
                    if (ca == cb) continue;
                    m.Differs[i] = true; m.Differing++;
                    if (Math.Abs(Delta(ca, cb, false)) <= most) { m.Faint++; continue; }
                    if (AntiAliased(a, x, y, b) || AntiAliased(b, x, y, a)) { m.AntiAliased++; continue; }
                }
                else { m.Differs[i] = true; m.Differing++; }
                m.Counts[i] = true; m.Counted++;
            }
        if (!m.SizeChanged && m.Counted > 0) FindShifts(a, b, m);
        var clumps = m.Counted == 0 ? new() : Groups(m.Counts, w, h, 1);
        m.Clumps = clumps.Count;
        m.LargestClump = clumps.Count == 0 ? 0 : clumps[0].Pixels;
        m.Verdict = m.Differing == 0 ? Verdict.Unchanged
            : m.SizeChanged || m.Counted > MinorMostPixels || m.LargestClump > MinorLargestClump ? Verdict.Changed
            : Verdict.Minor;
        return m;
    }

    // The shift test. A font's metrics changed, or a control was laid out a pixel narrower, and text
    // that moved by a pixel or two counted at every edge of every glyph: the Avalonia 12 trial
    // (2026-11-27) had 14 pictures changed by that, little else in them. So the
    // counted pixels, in places (those within ShiftReach of each other), are each tried against the
    // baseline moved by up to ShiftMost pixels each way: a place whose box, and a pixel round it,
    // matches the baseline so moved, within the same Tolerance and anti-aliasing allowance, has
    // moved. And what moved left its old place: a stroke grown thicker, or a box's edge where the
    // rest of the box did not follow, matches a move too, but only goes darker (or lighter); the
    // moved places, weighed with the others moved the same way near them along the move, must have
    // gone as much lighter as darker. What a move cannot explain - a glyph of another shape, a colour
    // changed, an edge moved by itself - stays counted.

    /// <summary>How far, in pixels each way, a place may have moved and be found shifted.</summary>
    public const int ShiftMost = 2;

    /// <summary>Counted pixels this close to each other (in pixels, in any direction) are one place, tried for a move as one.</summary>
    const int ShiftReach = 2;

    /// <summary>How far round a place's counted pixels the moved baseline must match as well.</summary>
    const int ShiftMargin = 1;

    /// <summary>
    /// How lopsided the moved places weighed together may be: how much of their change in
    /// brightness may be one way, darker or lighter, beyond what goes the other (0 to 1). On the
    /// Avalonia 12 trial text moved weighed 0.16 at most; a lone edge or a thickened stroke weighs 1.
    /// </summary>
    const double ShiftMostLopsided = 0.5;

    /// <summary>How far apart along a move, in pixels, the places moved the same way are weighed together (the leading and trailing edges of a thick stroke, a title's serifs).</summary>
    const int ShiftPair = 32;

    /// <summary>The offsets tried, the nearest first: every dx, dy from -<see cref="ShiftMost"/> to <see cref="ShiftMost"/> but 0, 0.</summary>
    static readonly (int Dx, int Dy)[] Offsets =
        (from dy in Enumerable.Range(-ShiftMost, 2 * ShiftMost + 1)
         from dx in Enumerable.Range(-ShiftMost, 2 * ShiftMost + 1)
         where dx != 0 || dy != 0
         orderby Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dx) + Math.Abs(dy), dy, dx
         select (dx, dy)).ToArray();

    /// <summary>
    /// The shift test on <paramref name="m"/>'s counted pixels (two pictures of one size): each place
    /// found moved is taken out of Counts and Counted, into Shifted and Moves.
    /// </summary>
    static void FindShifts(Pixels a, Pixels b, Measured m)
    {
        int w = m.Width, h = m.Height;
        double most = LargestDelta * Tolerance * Tolerance;
        var members = new List<List<int>>();
        var places = Groups(m.Counts, w, h, ShiftReach, members);
        var moved = Offsets.Select(o => a.Moved(o.Dx, o.Dy)).ToArray();
        // each place that a move explains: its offset, and how much brighter it went and how much it changed at all
        var found = new List<(int Place, int Offset, double Brighter, double Change)>();
        for (int k = 0; k < places.Count; k++)
        {
            var p = places[k];
            int x0 = Math.Max(0, p.X0 - ShiftMargin), y0 = Math.Max(0, p.Y0 - ShiftMargin), x1 = Math.Min(w - 1, p.X1 + ShiftMargin), y1 = Math.Min(h - 1, p.Y1 + ShiftMargin);
            int o = Array.FindIndex(moved, s => Matches(s, b, x0, y0, x1, y1, most));
            if (o < 0) continue;
            double brighter = 0, change = 0;
            foreach (int i in members[k]) { double d = Delta(b.Data[i], a.Data[i], true); brighter += d; change += Math.Abs(d); }
            found.Add((k, o, brighter, change));
        }
        // the places moved the same way, ahead of each other or behind along the move within ShiftPair
        // (and no further than ShiftMost across it), weighed together
        var set = Enumerable.Range(0, found.Count).ToArray();
        int Root(int i) { while (set[i] != i) i = set[i] = set[set[i]]; return i; }
        for (int i = 0; i < found.Count; i++)
            for (int j = i + 1; j < found.Count; j++)
            {
                if (found[i].Offset != found[j].Offset) continue;
                var (dx, dy) = Offsets[found[i].Offset];
                int ex = dx != 0 ? ShiftPair : ShiftMost, ey = dy != 0 ? ShiftPair : ShiftMost;
                var p = places[found[i].Place];
                var q = places[found[j].Place];
                if (p.X0 - ex <= q.X1 && q.X0 <= p.X1 + ex && p.Y0 - ey <= q.Y1 && q.Y0 <= p.Y1 + ey) set[Root(i)] = Root(j);
            }
        foreach (var together in Enumerable.Range(0, found.Count).GroupBy(Root))
        {
            double brighter = together.Sum(i => found[i].Brighter), change = together.Sum(i => found[i].Change);
            if (change > 0 && Math.Abs(brighter) / change > ShiftMostLopsided) continue;
            foreach (int i in together)
            {
                var (k, o, _, _) = found[i];
                foreach (int px in members[k]) m.Counts[px] = false;
                m.Counted -= places[k].Pixels;
                m.Shifted += places[k].Pixels;
                m.Moves.Add((Offsets[o].Dx, Offsets[o].Dy, places[k].Pixels));
            }
        }
    }

    /// <summary>Does <paramref name="b"/> match <paramref name="moved"/> at every pixel of the box, within the tolerance and anti-aliasing allowance of <see cref="Measure"/>?</summary>
    static bool Matches(Pixels moved, Pixels b, int x0, int y0, int x1, int y1, double most)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!moved.Has(x, y)) return false;
                uint cs = moved.At(x, y), cb = b.At(x, y);
                if (cs == cb || Math.Abs(Delta(cs, cb, false)) <= most) continue;
                if (AntiAliased(moved, x, y, b) || AntiAliased(b, x, y, moved)) continue;
                return false;
            }
        return true;
    }

    // ---------------------------------------------------------------- the report

    /// <summary>Compare <paramref name="baseline"/> with <paramref name="current"/>, writing the report into <paramref name="outDir"/>.</summary>
    public static CompareResult Compare(string baseline, string current, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var before = Comparable(baseline);
        var after = Comparable(current);
        bool firstRun = before.Count == 0;
        var changed = new List<Change>();
        var minor = new List<Change>();
        var added = new List<string>();
        var removed = new List<string>();
        var every = new JsonArray();
        int unchanged = 0;
        if (!firstRun)
        {
            foreach (var name in after)
            {
                if (!before.Contains(name)) { added.Add(name); continue; }
                var c = CompareOne(name, Path.Combine(baseline, name), Path.Combine(current, name), outDir);
                every.Add(new JsonObject
                {
                    ["name"] = c.Name, ["class"] = Class(c.Verdict), ["pixels"] = c.Pixels, ["faint"] = c.Faint, ["antiAliased"] = c.AntiAliased,
                    ["counted"] = c.Counted, ["shifted"] = c.Shifted, ["clumps"] = c.Clumps, ["largestClump"] = c.LargestClump, ["sizeBefore"] = c.SizeBefore, ["sizeAfter"] = c.SizeAfter,
                });
                if (c.Verdict == Verdict.Changed) changed.Add(c);
                else if (c.Verdict == Verdict.Minor) minor.Add(c);
                else unchanged++;
            }
            removed.AddRange(before.Where(n => !after.Contains(n)));
        }

        JsonNode Entry(Change c) => new JsonObject
        {
            ["name"] = c.Name,
            ["class"] = Class(c.Verdict),
            ["pixels"] = c.Pixels,
            ["faint"] = c.Faint,
            ["antiAliased"] = c.AntiAliased,
            ["counted"] = c.Counted,
            ["clumps"] = c.Clumps,
            ["largestClump"] = c.LargestClump,
            ["shifted"] = c.Shifted,
            ["shiftedBy"] = MostMoved(c.Moves),
            ["moves"] = new JsonArray(c.Moves.Select(v => (JsonNode)new JsonObject { ["dx"] = v.Dx, ["dy"] = v.Dy, ["places"] = v.Places, ["pixels"] = v.Pixels }).ToArray()),
            ["box"] = new JsonObject { ["x"] = c.X, ["y"] = c.Y, ["width"] = c.Width, ["height"] = c.Height },
            ["sizeBefore"] = c.SizeBefore,
            ["sizeAfter"] = c.SizeAfter,
            ["before"] = c.Folder + "/before.png",
            ["after"] = c.Folder + "/after.png",
            ["areas"] = new JsonArray(c.Areas.Select((r, k) =>
            {
                var o = new JsonObject { ["x"] = r.X, ["y"] = r.Y, ["width"] = r.Width, ["height"] = r.Height, ["pixels"] = r.Pixels };
                if (r.Zoom > 0)
                    o["zoom"] = new JsonObject
                    {
                        ["factor"] = r.Zoom, ["x"] = r.CropX, ["y"] = r.CropY, ["width"] = r.CropWidth, ["height"] = r.CropHeight,
                        ["before"] = $"{c.Folder}/zoom-{k + 1}-before.png", ["after"] = $"{c.Folder}/zoom-{k + 1}-after.png",
                    };
                return (JsonNode)o;
            }).ToArray()),
        };
        var json = new JsonObject
        {
            ["baseline"] = Path.GetFullPath(baseline),
            ["current"] = Path.GetFullPath(current),
            ["firstRun"] = firstRun,
            ["rule"] = new JsonObject { ["tolerance"] = Tolerance, ["minorMostPixels"] = MinorMostPixels, ["minorLargestClump"] = MinorLargestClump, ["shiftMost"] = ShiftMost },
            ["compared"] = firstRun ? 0 : changed.Count + minor.Count + unchanged,
            ["unchanged"] = unchanged,
            ["changed"] = new JsonArray(changed.Select(Entry).ToArray()),
            ["minor"] = new JsonArray(minor.Select(Entry).ToArray()),
            ["added"] = new JsonArray(added.Select(a => (JsonNode)JsonValue.Create(a)!).ToArray()),
            ["removed"] = new JsonArray(removed.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
            ["pictures"] = every,
        };
        File.WriteAllText(Path.Combine(outDir, "compare.json"), json.ToJsonString(Indented));

        var md = new StringBuilder();
        md.AppendLine("### Pictures against the baseline");
        md.AppendLine();
        if (firstRun)
        {
            md.AppendLine($"First run: no baseline. {after.Count} picture{(after.Count == 1 ? "" : "s")} taken, nothing compared.");
        }
        else
        {
            md.AppendLine($"{changed.Count} visibly changed, {minor.Count} minor, {added.Count} new, {removed.Count} removed ({unchanged} unchanged).");
            if (changed.Count + added.Count + removed.Count > 0)
            {
                md.AppendLine();
                md.AppendLine("| picture | what | pixels | where |");
                md.AppendLine("|---|---|---|---|");
                foreach (var c in changed)
                    md.AppendLine($"| `{c.Name}` | changed | {c.Counted}{(c.Counted != c.Pixels ? $" (of {c.Pixels} differing)" : "")} | {c.Width}x{c.Height} at {c.X},{c.Y}{(c.SizeBefore != c.SizeAfter ? $" (size {c.SizeBefore} to {c.SizeAfter})" : "")} |");
                foreach (var a in added) md.AppendLine($"| `{a}` | new | | |");
                foreach (var r in removed) md.AppendLine($"| `{r}` | removed | | |");
            }
            if (minor.Count > 0)
            {
                md.AppendLine();
                md.AppendLine($"Minor, a few scattered pixels each (anti-aliasing noise) or text moved by a pixel or two, not counted as changed: "
                    + $"{string.Join(", ", minor.Select(c => $"`{c.Name}` {c.Pixels} px{(c.Shifted > 0 ? $", shifted by up to {MostMoved(c.Moves)} px" : "")}"))}.");
            }
        }
        File.WriteAllText(Path.Combine(outDir, "summary.md"), md.ToString());
        return new CompareResult(firstRun, changed, minor, added, removed, unchanged);
    }

    static string Class(Verdict v) => v switch { Verdict.Changed => "changed", Verdict.Minor => "minor", _ => "unchanged" };

    /// <summary>
    /// One picture against its baseline, measured (<see cref="Measure"/>). For a minor or changed one,
    /// before.png and after.png are written under minor/ or changed/, its areas found (the counted
    /// pixels of a changed picture, every differing pixel of a minor one, within <see cref="AreaGap"/>
    /// of each other), and the biggest areas' crops enlarged.
    /// </summary>
    static Change CompareOne(string name, string beforePath, string afterPath, string outDir)
    {
        var a = Pixels.Load(beforePath);
        var b = Pixels.Load(afterPath);
        var m = Measure(a, b);
        string sizeA = $"{a.Width}x{a.Height}", sizeB = $"{b.Width}x{b.Height}";
        if (m.Verdict == Verdict.Unchanged)
            return new Change(name, m.Verdict, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, sizeA, sizeB, "", new List<Area>(), 0, new List<Move>());
        // each way places moved, the most places first
        var moves = m.Moves.GroupBy(v => (v.Dx, v.Dy)).Select(g => new Move(g.Key.Dx, g.Key.Dy, g.Count(), g.Sum(v => v.Pixels)))
            .OrderByDescending(v => v.Places).ThenByDescending(v => v.Pixels).ToList();

        int w = m.Width, h = m.Height;
        var groups = Groups(m.Verdict == Verdict.Changed ? m.Counts : m.Differs, w, h, AreaGap);
        int x0 = groups.Min(g => g.X0), y0 = groups.Min(g => g.Y0), x1 = groups.Max(g => g.X1), y1 = groups.Max(g => g.Y1);
        string folder = (m.Verdict == Verdict.Changed ? "changed/" : "minor/") + Folder(name);
        string at = Path.Combine(outDir, folder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(at);
        File.Copy(beforePath, Path.Combine(at, "before.png"), true);
        File.Copy(afterPath, Path.Combine(at, "after.png"), true);
        var areas = new List<Area>();
        foreach (var g in groups.Take(MostAreas))
        {
            var area = new Area(g.X0, g.Y0, g.X1 - g.X0 + 1, g.Y1 - g.Y0 + 1, g.Pixels);
            if (areas.Count < MostZooms)
            {
                // the crop: the area and a margin round it, inside the larger of the two pictures, at most
                // CropMostWidth x CropMostHeight from its top left; enlarged 4 times, or 3 when it is big
                int cx = Math.Max(0, g.X0 - CropMargin), cy = Math.Max(0, g.Y0 - CropMargin);
                int cw = Math.Min(Math.Min(w, g.X1 + 1 + CropMargin) - cx, CropMostWidth);
                int ch = Math.Min(Math.Min(h, g.Y1 + 1 + CropMargin) - cy, CropMostHeight);
                int zoom = cw <= 160 && ch <= 120 ? 4 : 3;
                Zoom(a, cx, cy, cw, ch, zoom, Path.Combine(at, $"zoom-{areas.Count + 1}-before.png"));
                Zoom(b, cx, cy, cw, ch, zoom, Path.Combine(at, $"zoom-{areas.Count + 1}-after.png"));
                area = area with { Zoom = zoom, CropX = cx, CropY = cy, CropWidth = cw, CropHeight = ch };
            }
            areas.Add(area);
        }
        return new Change(name, m.Verdict, m.Differing, m.Faint, m.AntiAliased, m.Counted, m.Clumps, m.LargestClump,
                          x0, y0, x1 - x0 + 1, y1 - y0 + 1, sizeA, sizeB, folder, areas, m.Shifted, moves);
    }

    /// <summary>
    /// A crop of a picture enlarged <paramref name="zoom"/> times, each pixel a solid square (no
    /// smoothing). What lies outside the picture (the other one is bigger) is a grey checker.
    /// </summary>
    static void Zoom(Pixels p, int cx, int cy, int cw, int ch, int zoom, string path)
    {
        int w = cw * zoom, h = ch * zoom;
        var px = new uint[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = cx + x / zoom, sy = cy + y / zoom;
                px[y * w + x] = p.Has(sx, sy) ? p.At(sx, sy) : ((x / 8 + y / 8) % 2 == 0 ? 0xFFBDBDBDu : 0xFFE6E6E6u);
            }
        SavePng(px, w, h, path);
    }

    /// <summary>A picture's own folder under changed/ or minor/: its path with the folder separator and the .png gone.</summary>
    static string Folder(string name) => Path.ChangeExtension(name, null)!.Replace('/', '_').Replace('\\', '_');

    public static int CompareMain(string[] args)
    {
        if (args.Length != 4) { Console.WriteLine("usage: Mordor.UiCheck --compare <baseline dir> <current dir> <out dir>"); return 2; }
        try
        {
            var r = Compare(args[1], args[2], args[3]);
            Console.WriteLine(r.FirstRun
                ? $"first run: no baseline at {args[1]}; nothing compared"
                : $"{r.Changed.Count} visibly changed, {r.Minor.Count} minor, {r.Added.Count} new, {r.Removed.Count} removed, {r.Unchanged} unchanged");
            string Moved(Change c) => c.Shifted > 0 ? $"; {c.Shifted} px shifted, {MovedWords(c.Moves)}" : "";
            foreach (var c in r.Changed) Console.WriteLine($"  changed  {c.Name}: {c.Counted} px counted of {c.Pixels} differing, in {c.Width}x{c.Height} at {c.X},{c.Y}{Moved(c)}");
            foreach (var c in r.Minor) Console.WriteLine($"  minor    {c.Name}: {c.Pixels} px differing, {c.Counted} counted, largest clump {c.LargestClump}{Moved(c)}");
            foreach (var a in r.Added) Console.WriteLine($"  new      {a}");
            foreach (var x in r.Removed) Console.WriteLine($"  removed  {x}");
            Console.WriteLine($"the report is in {Path.GetFullPath(args[3])} (compare.json, summary.md)");
        }
        catch (Exception ex)
        {
            // a comparison that cannot be made is said, not failed: the job's pass or fail is the checks'
            Console.WriteLine($"the comparison could not be made: {ex.GetType().Name}: {ex.Message}");
            try
            {
                Directory.CreateDirectory(args[3]);
                File.WriteAllText(Path.Combine(args[3], "summary.md"), $"### Pictures against the baseline\n\nThe comparison could not be made: {ex.Message}\n");
            }
            catch { }
        }
        return 0;
    }

    // ================================================================ --gallery

    /// <summary>The machines in the order of the gallery's columns: the artifact's folder name, and the column's heading.</summary>
    static readonly (string Folder, string Label)[] Machines =
    {
        ("screenshots-Windows-X64", "Windows x64"),
        ("screenshots-Windows-ARM64", "Windows ARM64"),
        ("screenshots-Linux-X64", "Linux x64"),
        ("screenshots-Linux-ARM64", "Linux ARM64"),
        ("screenshots-macOS-ARM64", "macOS ARM64"),
    };

    /// <summary>How wide a thumbnail is drawn, in pixels; the column fits it.</summary>
    const int ThumbWidth = 320;

    /// <summary>The most areas ringed one by one on a thumbnail; more are ringed as one.</summary>
    const int ThumbRings = 6;

    /// <summary>A minor or changed picture as compare.json has it, its files' paths under compare/.</summary>
    sealed class Diff
    {
        public Verdict Verdict;
        public int Pixels, Faint, AntiAliased, Counted, LargestClump, Shifted;
        public List<Move> Moves = new();
        public string Before = "", After = "", Box = "", SizeBefore = "", SizeAfter = "";
        public (int X, int Y, int W, int H) Whole;
        public List<(int X, int Y, int W, int H)> Areas = new();
        public List<(int Factor, int X, int Y, int W, int H, string Before, string After)> Zooms = new();
    }

    sealed class Machine
    {
        public string Folder = "", Label = "", Dir = "";
        public bool Present, FirstRun, HasCompare;
        public HashSet<string> Pictures = new(StringComparer.Ordinal);
        public Dictionary<string, List<Failure>> Failures = new(StringComparer.Ordinal);
        public List<string> Order = new();
        public Dictionary<string, Diff> Diffs = new(StringComparer.Ordinal);
        public int ChangedCells, MinorCells, FailingCells;
    }

    static int Int(JsonNode? n) => n?.GetValue<int>() ?? 0;
    static string Str(JsonNode? n) => n?.GetValue<string>() ?? "";

    static Diff ReadDiff(JsonNode c, Verdict verdict)
    {
        var box = c["box"];
        var d = new Diff
        {
            Verdict = verdict,
            Pixels = Int(c["pixels"]), Faint = Int(c["faint"]), AntiAliased = Int(c["antiAliased"]),
            // a report from before the noise threshold counted every differing pixel
            Counted = c["counted"] == null ? Int(c["pixels"]) : Int(c["counted"]), LargestClump = Int(c["largestClump"]),
            // and one from before the shift test, none shifted
            Shifted = Int(c["shifted"]),
            Moves = (c["moves"]?.AsArray() ?? new JsonArray()).Where(v => v != null).Select(v => new Move(Int(v!["dx"]), Int(v["dy"]), Int(v["places"]), Int(v["pixels"]))).ToList(),
            Before = Str(c["before"]), After = Str(c["after"]), SizeBefore = Str(c["sizeBefore"]), SizeAfter = Str(c["sizeAfter"]),
            Box = box == null ? "" : $"{box["width"]}x{box["height"]} at {box["x"]},{box["y"]}",
            Whole = box == null ? default : (Int(box["x"]), Int(box["y"]), Int(box["width"]), Int(box["height"])),
        };
        foreach (var r in c["areas"]?.AsArray() ?? new JsonArray())
        {
            if (r == null) continue;
            d.Areas.Add((Int(r["x"]), Int(r["y"]), Int(r["width"]), Int(r["height"])));
            if (r["zoom"] is JsonNode z)
                d.Zooms.Add((Int(z["factor"]), Int(z["x"]), Int(z["y"]), Int(z["width"]), Int(z["height"]), Str(z["before"]), Str(z["after"])));
        }
        if (d.Areas.Count == 0 && box != null) d.Areas.Add((Int(box["x"]), Int(box["y"]), Int(box["width"]), Int(box["height"])));
        return d;
    }

    static Machine Read(string root, string folder, string label)
    {
        var m = new Machine { Folder = folder, Label = label, Dir = Path.Combine(root, folder) };
        m.Present = Directory.Exists(m.Dir);
        if (!m.Present) return m;
        foreach (var f in Directory.GetFiles(m.Dir, "*.png")) m.Pictures.Add(Path.GetFileName(f));
        string play = Path.Combine(m.Dir, "playthrough");
        if (Directory.Exists(play))
            foreach (var f in Directory.GetFiles(play, "*.png")) m.Pictures.Add("playthrough/" + Path.GetFileName(f));
        string checks = Path.Combine(m.Dir, ChecksFile);
        if (File.Exists(checks))
        {
            try
            {
                var doc = JsonNode.Parse(File.ReadAllText(checks));
                foreach (var p in doc?["pictures"]?.AsArray() ?? new JsonArray())
                {
                    string file = p?["file"]?.GetValue<string>() ?? "";
                    if (file.Length == 0) continue;
                    m.Order.Add(file);
                    var list = new List<Failure>();
                    foreach (var f in p!["failures"]?.AsArray() ?? new JsonArray())
                        list.Add(new Failure(f?["kind"]?.GetValue<string>() ?? "", f?["message"]?.GetValue<string>() ?? "",
                                             f?["known"]?.GetValue<bool>() ?? false, f?["reason"]?.GetValue<string>()));
                    m.Failures[file] = list;
                }
            }
            catch (Exception ex) { Console.WriteLine($"  note  {checks} could not be read: {ex.Message}"); }
        }
        string compare = Path.Combine(m.Dir, CompareFolder, "compare.json");
        if (File.Exists(compare))
        {
            try
            {
                var doc = JsonNode.Parse(File.ReadAllText(compare));
                m.HasCompare = true;
                m.FirstRun = doc?["firstRun"]?.GetValue<bool>() ?? false;
                foreach (var (key, verdict) in new[] { ("changed", Verdict.Changed), ("minor", Verdict.Minor) })
                    foreach (var c in doc?[key]?.AsArray() ?? new JsonArray())
                        if (c != null) m.Diffs[Str(c["name"])] = ReadDiff(c, verdict);
            }
            catch (Exception ex) { Console.WriteLine($"  note  {compare} could not be read: {ex.Message}"); }
        }
        return m;
    }

    /// <summary>The rows: each readability picture, its Light and Dark beside each other in the order the pass took them, then the playthrough's steps.</summary>
    static List<string> Rows(List<Machine> machines)
    {
        var all = new HashSet<string>(machines.SelectMany(m => m.Pictures.Concat(m.Failures.Keys)), StringComparer.Ordinal);
        string Screen(string n) => n.StartsWith("Light_", StringComparison.Ordinal) ? n[6..] : n.StartsWith("Dark_", StringComparison.Ordinal) ? n[5..] : n;
        int Theme(string n) => n.StartsWith("Light_", StringComparison.Ordinal) ? 0 : n.StartsWith("Dark_", StringComparison.Ordinal) ? 1 : 2;
        // the screens in the order first taken, by the first machine's checks.json that lists each
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var m in machines)
            foreach (var n in m.Order)
                if (!order.ContainsKey(Screen(n))) order[Screen(n)] = order.Count;
        var readability = all.Where(n => !n.StartsWith("playthrough/", StringComparison.Ordinal))
            .OrderBy(n => order.TryGetValue(Screen(n), out int i) ? i : int.MaxValue)
            .ThenBy(n => Screen(n), StringComparer.Ordinal).ThenBy(Theme).ToList();
        var play = all.Where(n => n.StartsWith("playthrough/", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal);
        return readability.Concat(play).ToList();
    }

    /// <summary>A row's heading: the theme, and the window's name as the file has it, spaced again.</summary>
    static string Title(string name)
    {
        string n = Path.ChangeExtension(name, null)!;
        if (n.StartsWith("playthrough/", StringComparison.Ordinal)) return "playthrough: " + n[12..].Replace('-', ' ');
        string theme = n.StartsWith("Light_", StringComparison.Ordinal) ? "Light" : n.StartsWith("Dark_", StringComparison.Ordinal) ? "Dark" : "";
        if (theme.Length > 0) n = n[(theme.Length + 1)..];
        n = System.Text.RegularExpressions.Regex.Replace(n.Replace("__", ", ").Replace("_s_", "'s ").Replace('_', ' '), " {2,}", " ").Trim();
        return theme.Length > 0 ? $"{theme}: {n}" : n;
    }

    static string Url(params string[] parts) => string.Join("/", parts.SelectMany(p => p.Split('/', '\\')).Where(p => p.Length > 0).Select(Uri.EscapeDataString));
    static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
    static string Pct(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "%";

    static (int W, int H) Size(string s)
    {
        var p = s.Split('x');
        return p.Length == 2 && int.TryParse(p[0], out int w) && int.TryParse(p[1], out int h) ? (w, h) : (0, 0);
    }

    /// <summary>A thumbnail <see cref="ThumbWidth"/> wide (or the picture itself where it is narrower).</summary>
    static void Thumb(string from, string to)
    {
        using var src = SKBitmap.Decode(from);
        if (src == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (src.Width <= ThumbWidth) { File.Copy(from, to, true); return; }
        int h = Math.Max(1, (int)Math.Round(src.Height * (double)ThumbWidth / src.Width));
        using var small = src.Resize(new SKImageInfo(ThumbWidth, h), new SKSamplingOptions(SKCubicResampler.Mitchell));   // SKFilterQuality.High, as SkiaSharp 3 spells it
        if (small != null) SavePng(small, to); else File.Copy(from, to, true);
    }

    /// <summary>What a minor or changed picture's counts say, in a sentence.</summary>
    static string Says(Diff d)
    {
        var parts = new List<string>();
        if (d.AntiAliased > 0) parts.Add($"{d.AntiAliased} anti-aliasing");
        if (d.Faint > 0) parts.Add($"{d.Faint} too faint to see");
        if (d.Shifted > 0) parts.Add($"{d.Shifted} moved");
        string size = d.SizeBefore != d.SizeAfter ? $" The picture was {d.SizeBefore} and is {d.SizeAfter}." : "";
        string notCounted = parts.Count > 0 ? $" ({string.Join(", ", parts)}: not counted)" : "";
        return d.Verdict == Verdict.Changed
            ? $"Changed: {d.Counted} pixels count, of {d.Pixels} that differ{notCounted}, in {d.Box}; the largest clump {d.LargestClump}.{size}"
            : d.Shifted > 0
            ? $"Minor, shifted by up to {MostMoved(d.Moves)} px: {d.Pixels} pixels differ{notCounted}, {d.Counted} counted, the largest clump {d.LargestClump}: text moved a pixel or two, the rest scattered, so not marked changed."
            : $"Minor: {d.Pixels} pixels differ{notCounted}, {d.Counted} counted, the largest clump {d.LargestClump}: scattered, below the noise threshold, so not marked changed.";
    }

    /// <summary>The line under <see cref="Says"/> for a picture with places moved: "Text moved 1 px left in 3 places." (empty for none).</summary>
    static string SaysMoved(Diff d)
    {
        string w = MovedWords(d.Moves);
        return w.Length == 0 ? "" : char.ToUpperInvariant(w[0]) + w[1..] + $" ({d.Shifted} pixels, not counted).";
    }

    public static (int Rows, int Columns) Gallery(string root, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var machines = Machines.Select(m => Read(root, m.Folder, m.Label)).ToList();
        // any other machine's folder, after the five
        if (Directory.Exists(root))
            foreach (var d in Directory.GetDirectories(root, "screenshots-*").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal))
                if (!Machines.Any(m => m.Folder == d)) machines.Add(Read(root, d!, d!["screenshots-".Length..].Replace('-', ' ')));
        var rows = Rows(machines);

        var sb = new StringBuilder();
        var body = new StringBuilder();
        var views = new JsonObject();
        var changedViews = new JsonArray();
        var minorViews = new JsonArray();
        var minorRows = new StringBuilder();
        int r = 0, minorShifted = 0;
        foreach (var name in rows)
        {
            var cells = new StringBuilder();
            bool rowChanged = false, rowFailing = false;
            foreach (var m in machines)
            {
                cells.Append("<td>");
                bool has = m.Pictures.Contains(name);
                var failures = m.Failures.TryGetValue(name, out var f) ? f : new List<Failure>();
                bool failing = failures.Any(x => !x.Known);
                var diff = m.Diffs.TryGetValue(name, out var dd) ? dd : null;
                bool changed = diff?.Verdict == Verdict.Changed;
                string? view = null;
                string t = m.Label + " - " + Title(name);
                if (diff != null)
                {
                    // the view of the change: its files copied beside the page, its data for the script
                    view = "v" + views.Count;
                    string Cmp(string rel) => Url("pictures", m.Folder, CompareFolder, rel);
                    foreach (var rel in new[] { diff.Before, diff.After }.Concat(diff.Zooms.SelectMany(z => new[] { z.Before, z.After })))
                        if (rel.Length > 0) Copy(Path.Combine(m.Dir, CompareFolder, rel), Path.Combine(outDir, "pictures", m.Folder, CompareFolder, rel));
                    var (bw, bh) = Size(diff.SizeBefore);
                    var (aw, ah) = Size(diff.SizeAfter);
                    views[view] = new JsonObject
                    {
                        ["t"] = t,
                        ["v"] = changed ? "changed" : "minor",
                        ["d"] = Says(diff),
                        ["m"] = SaysMoved(diff),
                        ["b"] = Cmp(diff.Before), ["a"] = Cmp(diff.After),
                        ["sb"] = new JsonArray(bw, bh), ["sa"] = new JsonArray(aw, ah),
                        ["r"] = new JsonArray(diff.Areas.Select(x => (JsonNode)new JsonArray(x.X, x.Y, x.W, x.H)).ToArray()),
                        ["z"] = new JsonArray(diff.Zooms.Select((z, k) => (JsonNode)new JsonObject
                        {
                            ["b"] = Cmp(z.Before), ["a"] = Cmp(z.After),
                            ["c"] = $"Area {k + 1}: {z.W}x{z.H} at {z.X},{z.Y}, enlarged {z.Factor} times",
                        }).ToArray()),
                    };
                    (changed ? changedViews : minorViews).Add(view);
                }
                var cls = new List<string> { "cell" };
                if (changed) cls.Add("changed");
                if (failing) cls.Add("failing");
                cells.Append($"<div class=\"{string.Join(" ", cls)}\">");
                if (has)
                {
                    string full = Url("pictures", m.Folder, name), thumb = Url("thumbs", m.Folder, name);
                    Copy(Path.Combine(m.Dir, name), Path.Combine(outDir, "pictures", m.Folder, name));
                    Thumb(Path.Combine(m.Dir, name), Path.Combine(outDir, "thumbs", m.Folder, name));
                    string open = view != null ? $" data-view=\"{view}\"" : $" data-full=\"{full}\" data-title=\"{H(t)}\"";
                    cells.Append($"<a class=\"shot\" href=\"{full}\"{open}><span class=\"frame\"><img loading=\"lazy\" src=\"{thumb}\" alt=\"{H(Title(name))} on {H(m.Label)}\">");
                    // the changed areas ringed on the thumbnail, where they are in the picture now; past
                    // ThumbRings of them (a whole window redrawn), one ring round them all
                    var (aw, ah) = diff == null ? (0, 0) : Size(diff.SizeAfter);
                    if (changed && aw > 0 && ah > 0)
                        foreach (var x in (diff!.Areas.Count > ThumbRings && diff.Whole.W > 0 ? new() { diff.Whole } : diff.Areas).Where(x => x.X < aw && x.Y < ah))
                            cells.Append($"<span class=\"mark\" style=\"left:{Pct(100.0 * x.X / aw)};top:{Pct(100.0 * x.Y / ah)};width:{Pct(100.0 * Math.Min(x.W, aw - x.X) / aw)};height:{Pct(100.0 * Math.Min(x.H, ah - x.Y) / ah)}\"></span>");
                    cells.Append("</span></a>");
                }
                else cells.Append("<div class=\"none\">no picture</div>");
                string link = diff == null ? "" : Url("pictures", m.Folder, CompareFolder, diff.After);
                if (changed)
                {
                    rowChanged = true; m.ChangedCells++;
                    cells.Append($"<div class=\"tag\"><span class=\"badge\">changed</span> {diff!.Counted} px, {H(diff.Box)}{(diff.SizeBefore != diff.SizeAfter ? $", size {H(diff.SizeBefore)} to {H(diff.SizeAfter)}" : "")}: "
                        + $"<a href=\"{link}\" data-view=\"{view}\">see the change</a></div>");
                }
                else if (diff != null)
                {
                    m.MinorCells++;
                    if (diff.Shifted > 0) minorShifted++;
                    string what = diff.Shifted > 0 ? $"shifted by up to {MostMoved(diff.Moves)} px" : "scattered";
                    cells.Append($"<div class=\"tag minor\">minor: {diff.Pixels} px, {what} (<a href=\"{link}\" data-view=\"{view}\">look</a>)</div>");
                    minorRows.Append($"<tr><td>{H(m.Label)}</td><td>{H(Title(name))}</td><td>{diff.Pixels} px differ, {diff.Counted} counted, largest clump {diff.LargestClump}"
                        + $"{(diff.Shifted > 0 ? $"; {H(MovedWords(diff.Moves))}" : "")}</td><td><a href=\"{link}\" data-view=\"{view}\">look</a></td></tr>\n");
                }
                if (failing) { rowFailing = true; m.FailingCells++; }
                if (failures.Count > 0)
                {
                    cells.Append("<ul class=\"failures\">");
                    foreach (var x in failures)
                        cells.Append(x.Known
                            ? $"<li class=\"known\"><b>known {H(x.Kind)}</b>: {H(x.Message)}{(x.Reason != null ? $" <i>({H(x.Reason)})</i>" : "")}</li>"
                            : $"<li><b>{H(x.Kind)}</b>: {H(x.Message)}</li>");
                    cells.Append("</ul>");
                }
                cells.Append("</div></td>");
            }
            string flags = (rowChanged ? " changed" : "") + (rowFailing ? " failing" : "");
            body.Append($"<tr data-row=\"{r++}\" class=\"row{flags}\"><th scope=\"row\">{H(Title(name))}<span class=\"file\">{H(name)}</span></th>{cells}</tr>\n");
        }

        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>OpenDejenol pictures</title>\n<style>\n").Append(Css).Append("</style>\n</head>\n<body>\n");
        sb.Append("<header>\n<h1>OpenDejenol pictures</h1>\n<p class=\"lede\">Every window of the readability pass, Light and Dark, and the playthrough's steps, as each machine drew them. ");
        sb.Append("A magenta border is a picture visibly changed since the baseline run, its changed areas ringed: open it to see the change enlarged and flip between before and after. ");
        sb.Append($"A few scattered pixels of anti-aliasing, or text moved by up to {ShiftMost} pixels and drawn the same, are minor, listed apart and not marked. A red border has a check failing under it.</p>\n<ul class=\"counts\">\n");
        foreach (var m in machines)
        {
            string state = !m.Present ? "no pictures"
                : $"{m.ChangedCells} visibly changed, {m.MinorCells} minor, {m.FailingCells} failing" + (m.HasCompare ? (m.FirstRun ? " (first run: no baseline)" : "") : " (not compared)");
            sb.Append($"<li data-machine=\"{H(m.Folder)}\"><b>{H(m.Label)}</b>: {H(state)}</li>\n");
        }
        sb.Append("</ul>\n");
        if (minorViews.Count > 0)
            sb.Append($"<details class=\"minor-list\"><summary>Minor differences: {minorViews.Count} picture{(minorViews.Count == 1 ? "" : "s")} with a few scattered pixels (anti-aliasing noise)"
                      + $"{(minorShifted > 0 ? $" or text moved by a pixel or two ({minorShifted} of them)" : "")}, not marked changed</summary>\n")
              .Append("<div class=\"scroll\"><table><thead><tr><th scope=\"col\">machine</th><th scope=\"col\">picture</th><th scope=\"col\">pixels</th><th scope=\"col\"></th></tr></thead><tbody>\n")
              .Append(minorRows).Append("</tbody></table></div></details>\n");
        sb.Append("<label class=\"filter\"><input type=\"checkbox\" id=\"only\"> Only rows visibly changed or failing</label>\n</header>\n");
        sb.Append("<div class=\"grid\">\n<table>\n<thead><tr><th scope=\"col\" class=\"corner\">screen</th>");
        foreach (var m in machines) sb.Append($"<th scope=\"col\" class=\"machine\" data-machine=\"{H(m.Folder)}\">{H(m.Label)}</th>");
        sb.Append("</tr></thead>\n<tbody>\n").Append(body).Append("</tbody>\n</table>\n</div>\n");
        sb.Append("<p class=\"empty\" id=\"nothing\" hidden>No row visibly changed or fails.</p>\n");
        sb.Append("<div id=\"overlay\" hidden role=\"dialog\" aria-modal=\"true\" aria-labelledby=\"overlay-title\"><div class=\"bar\"><span id=\"overlay-title\"></span>")
          .Append("<span class=\"nav\"><button type=\"button\" id=\"prev\" title=\"Previous (left arrow)\">Previous</button><span id=\"overlay-pos\"></span>")
          .Append("<button type=\"button\" id=\"next\" title=\"Next (right arrow)\">Next</button><button type=\"button\" id=\"close\" title=\"Close (Esc)\">Close</button></span></div>")
          .Append("<div class=\"view\" id=\"overlay-view\"></div></div>\n");
        // the views' data as JSON, which writes < > and & escaped, so nothing in it can end the script element
        var data = new JsonObject { ["views"] = views, ["changed"] = changedViews, ["minor"] = minorViews };
        sb.Append("<script type=\"application/json\" id=\"views\">").Append(data.ToJsonString()).Append("</script>\n");
        sb.Append("<script>\n").Append(Script).Append("</script>\n</body>\n</html>\n");
        File.WriteAllText(Path.Combine(outDir, "index.html"), sb.ToString());
        return (rows.Count, machines.Count);
    }

    static void Copy(string from, string to)
    {
        if (!File.Exists(from)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to, true);
    }

    const string Css = """
:root { color-scheme: light dark; --bg: #f6f6f4; --fg: #1d1d1f; --muted: #5d5d66; --panel: #ffffff; --line: #d4d4d8;
        --changed: #b0129c; --failing: #c62828; --known: #6b6b73; --badge-fg: #ffffff; --link: #1f5fbf; --sunk: #ececea; }
@media (prefers-color-scheme: dark) {
  :root { --bg: #161618; --fg: #ececef; --muted: #a3a3ad; --panel: #222226; --line: #3a3a40;
          --changed: #e45bd2; --failing: #ff6b6b; --known: #9a9aa3; --badge-fg: #161618; --link: #8ab4ff; --sunk: #2c2c31; }
}
* { box-sizing: border-box; }
html { -webkit-text-size-adjust: 100%; }
body { margin: 0; padding: 16px; background: var(--bg); color: var(--fg); font: 14px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif; }
body.viewing { overflow: hidden; }
h1 { font-size: 1.35rem; margin: 0 0 4px; }
.lede { margin: 0 0 8px; color: var(--muted); max-width: 70ch; }
.counts { list-style: none; padding: 0; margin: 0 0 8px; display: flex; flex-wrap: wrap; gap: 4px 16px; }
.filter { display: inline-flex; gap: 6px; align-items: center; margin: 4px 0 12px; cursor: pointer; }
a { color: var(--link); }
.minor-list { margin: 0 0 8px; color: var(--muted); }
.minor-list summary { cursor: pointer; }
.minor-list .scroll { overflow-x: auto; margin-top: 4px; }
.minor-list table { border-collapse: collapse; font-size: 12px; }
.minor-list th, .minor-list td { padding: 2px 12px 2px 0; border-bottom: 1px solid var(--line); text-align: left; vertical-align: top; }
.grid { overflow-x: auto; border: 1px solid var(--line); border-radius: 6px; background: var(--panel); }
.grid table { border-collapse: collapse; }
.grid th, .grid td { border-bottom: 1px solid var(--line); vertical-align: top; text-align: left; padding: 8px; }
.grid thead th { position: sticky; top: 0; background: var(--panel); z-index: 2; }
.grid th[scope=row] { min-width: 160px; max-width: 220px; font-weight: 600; position: sticky; left: 0; background: var(--panel); z-index: 2; }
.file { display: block; font-weight: 400; color: var(--muted); font-size: 12px; word-break: break-all; }
.grid td { min-width: 236px; width: 236px; }
.cell { border: 3px solid transparent; border-radius: 4px; padding: 2px; }
.cell.changed { border-color: var(--changed); }
.cell.failing { border-color: var(--failing); }
.shot { display: block; }
.frame { position: relative; display: block; }
.frame img { display: block; width: 100%; height: auto; border: 1px solid var(--line); }
/* a ring two pixels outside an area, magenta edged in black to show on any picture */
.mark { position: absolute; pointer-events: none; box-sizing: content-box; margin: -4px 0 0 -4px; padding: 2px;
        border: 2px solid #ff00ff; box-shadow: 0 0 0 1px #000, inset 0 0 0 1px #000; }
.none { color: var(--muted); font-style: italic; padding: 24px 0; text-align: center; }
.tag { margin-top: 4px; font-size: 12px; }
.tag.minor { color: var(--muted); }
.badge { background: var(--changed); color: var(--badge-fg); border-radius: 3px; padding: 0 5px; font-weight: 600; }
.failures { margin: 4px 0 0; padding-left: 16px; font-size: 12px; color: var(--failing); word-break: break-word; }
.failures .known { color: var(--known); }
.only .row:not(.changed):not(.failing) { display: none; }
#overlay { position: fixed; inset: 0; background: rgba(0,0,0,.88); z-index: 10; display: flex; flex-direction: column; }
#overlay[hidden] { display: none; }
#overlay .bar { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 8px; padding: 8px 16px; color: #fff; background: #000; }
#overlay .nav { display: inline-flex; flex-wrap: wrap; align-items: center; gap: 6px; }
#overlay button { font: inherit; padding: 4px 12px; cursor: pointer; }
#overlay button[hidden] { display: none; }
#overlay .view { flex: 1; overflow: auto; padding: 0 16px 16px; }
#overlay img.plain { display: block; max-width: none; margin: 0 auto; background: #fff; }
.cv { background: var(--panel); color: var(--fg); border-radius: 6px; padding: 12px 16px; max-width: 1100px; margin: 0 auto; }
.cv p { margin: 0 0 10px; }
.cv .what { color: var(--muted); }
.zoom { margin: 0 0 16px; }
.zoom figcaption { color: var(--muted); font-size: 12px; margin-top: 4px; }
.pair { display: flex; flex-wrap: wrap; gap: 12px; }
.side { display: flex; flex-direction: column; gap: 2px; min-width: 0; max-width: 100%; }
.label { font-size: 12px; font-weight: 600; color: var(--muted); }
/* the crops are enlarged pixel by pixel already; kept square when a narrow screen shrinks them */
img.px { display: block; max-width: 100%; height: auto; image-rendering: crisp-edges; image-rendering: pixelated; border: 1px solid var(--line); }
.flip-cap b { color: var(--changed); }
.stage { position: relative; width: 100%; cursor: pointer; background: var(--sunk); outline: 1px solid var(--line); }
.stage img { position: absolute; left: 0; top: 0; height: auto; display: block; }
.stage.bare .mark { display: none; }
.rings { display: inline-flex; gap: 6px; align-items: center; margin: 0 0 8px; cursor: pointer; font-size: 12px; color: var(--muted); }
.empty { color: var(--muted); }
""";

    const string Script = """
(function () {
  var data = { views: {}, changed: [], minor: [] };
  try { data = JSON.parse(document.getElementById('views').textContent); } catch (e) { }
  var overlay = document.getElementById('overlay'), view = document.getElementById('overlay-view'),
      title = document.getElementById('overlay-title'), pos = document.getElementById('overlay-pos'),
      prev = document.getElementById('prev'), next = document.getElementById('next');
  var flip = null, ring = null, list = [], at = -1;
  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }
  function pct(v) { return (Math.round(v * 1000) / 1000) + '%'; }
  function nav() {
    prev.hidden = next.hidden = list.length < 2;
    pos.textContent = at >= 0 ? (at + 1) + ' of ' + list.length + ' ' + data.views[list[at]].v : '';
  }
  function show() { overlay.hidden = false; document.body.classList.add('viewing'); }
  function close() { overlay.hidden = true; view.textContent = ''; flip = ring = null; document.body.classList.remove('viewing'); }
  function picture(src, t) {
    view.textContent = ''; flip = ring = null; list = []; at = -1; nav();
    var img = el('img', 'plain'); img.src = src; img.alt = t;
    view.appendChild(img);
    title.textContent = t;
    show();
  }
  // a change: the enlarged crops before and after side by side, then the whole picture with its
  // areas ringed, flipped between before and after in place by a click or F
  function change(id) {
    var c = data.views[id];
    if (!c) return;
    list = data[c.v] || [id]; at = list.indexOf(id); nav();
    view.textContent = '';
    title.textContent = c.t;
    var box = el('div', 'cv');
    box.appendChild(el('p', 'what', c.d));
    if (c.m) box.appendChild(el('p', 'what moved', c.m));
    c.z.forEach(function (z) {
      var fig = el('figure', 'zoom'), pair = el('div', 'pair');
      [['before', z.b], ['after', z.a]].forEach(function (p) {
        var side = el('div', 'side');
        side.appendChild(el('span', 'label', p[0]));
        var img = el('img', 'px'); img.src = p[1]; img.alt = c.t + ', ' + p[0] + ', enlarged';
        side.appendChild(img);
        pair.appendChild(side);
      });
      fig.appendChild(pair);
      fig.appendChild(el('figcaption', null, z.c));
      box.appendChild(fig);
    });
    var W = Math.max(c.sb[0], c.sa[0]) || 1, H = Math.max(c.sb[1], c.sa[1]) || 1;
    var cap = el('p', 'flip-cap'), which = el('b', null, 'after');
    cap.appendChild(document.createTextNode('The whole picture, showing '));
    cap.appendChild(which);
    cap.appendChild(document.createTextNode(': click it or press F to flip between before and after.'));
    var stage = el('div', 'stage');
    stage.style.maxWidth = W + 'px';
    stage.style.aspectRatio = W + ' / ' + H;
    stage.title = 'Flip between before and after (F)';
    function layer(src, s, alt) {
      var img = el('img', null); img.src = src; img.alt = c.t + ', ' + alt;
      img.style.width = pct(100 * s[0] / W);
      stage.appendChild(img);
      return img;
    }
    var before = layer(c.b, c.sb, 'before'), after = layer(c.a, c.sa, 'after');
    before.style.visibility = 'hidden';
    c.r.forEach(function (r) {
      var m = el('span', 'mark');
      m.style.left = pct(100 * r[0] / W); m.style.top = pct(100 * r[1] / H);
      m.style.width = pct(100 * r[2] / W); m.style.height = pct(100 * r[3] / H);
      stage.appendChild(m);
    });
    var showingBefore = false;
    flip = function () {
      showingBefore = !showingBefore;
      before.style.visibility = showingBefore ? 'visible' : 'hidden';
      after.style.visibility = showingBefore ? 'hidden' : 'visible';
      which.textContent = showingBefore ? 'before' : 'after';
    };
    stage.addEventListener('click', flip);
    var rings = el('label', 'rings'), tick = el('input', null);
    tick.type = 'checkbox'; tick.checked = true;
    tick.addEventListener('change', function () { stage.classList.toggle('bare', !tick.checked); });
    rings.appendChild(tick);
    rings.appendChild(document.createTextNode(' Ring the changed areas (R)'));
    ring = function () { tick.checked = !tick.checked; stage.classList.toggle('bare', !tick.checked); };
    box.appendChild(cap);
    box.appendChild(rings);
    box.appendChild(stage);
    view.appendChild(box);
    view.scrollTop = 0;
    show();
  }
  function step(d) { if (list.length > 1) change(list[(at + d + list.length) % list.length]); }
  document.addEventListener('click', function (e) {
    var a = e.target.closest && e.target.closest('[data-view], a[data-full]');
    if (!a || overlay.contains(a)) return;
    e.preventDefault();
    if (a.hasAttribute('data-view')) change(a.getAttribute('data-view'));
    else picture(a.getAttribute('data-full'), a.getAttribute('data-title') || '');
  });
  document.getElementById('close').addEventListener('click', close);
  prev.addEventListener('click', function () { step(-1); });
  next.addEventListener('click', function () { step(1); });
  overlay.addEventListener('click', function (e) { if (e.target === overlay || e.target === view) close(); });
  document.addEventListener('keydown', function (e) {
    if (overlay.hidden || e.ctrlKey || e.metaKey || e.altKey) return;
    if (e.key === 'Escape') close();
    else if ((e.key === 'f' || e.key === 'F') && flip) { e.preventDefault(); flip(); }
    else if ((e.key === 'r' || e.key === 'R') && ring) { e.preventDefault(); ring(); }
    else if (e.key === 'ArrowRight') { e.preventDefault(); step(1); }
    else if (e.key === 'ArrowLeft') { e.preventDefault(); step(-1); }
  });
  var only = document.getElementById('only'), nothing = document.getElementById('nothing');
  only.addEventListener('change', function () {
    document.body.classList.toggle('only', only.checked);
    nothing.hidden = !(only.checked && !document.querySelector('.row.changed, .row.failing'));
  });
})();
""";

    public static int GalleryMain(string[] args)
    {
        if (args.Length != 3) { Console.WriteLine("usage: Mordor.UiCheck --gallery <artifacts root> <out dir>"); return 2; }
        try
        {
            var (rows, cols) = Gallery(args[1], args[2]);
            Console.WriteLine($"the gallery: {rows} rows, {cols} machines, at {Path.GetFullPath(Path.Combine(args[2], "index.html"))}");
        }
        catch (Exception ex) { Console.WriteLine($"the gallery could not be made: {ex.GetType().Name}: {ex.Message}"); }
        return 0;
    }

    // ================================================================ the checks of the tools

    /// <summary>
    /// The noise threshold on crops of CI's own pictures (test-data/compare): the six that differed
    /// only by windows-latest's anti-aliasing from one run to the next, and real changes; the shift
    /// test on crops of the Avalonia 12 trial's and on words drawn here, moved 1, 2 and 3 px. Then
    /// --compare and --gallery on pictures made here: a folder against itself (nothing changed),
    /// against a copy with a block changed, a pixel changed, one picture added and one taken away,
    /// and against no baseline; then a gallery of two machines. Returns how many failed.
    /// </summary>
    public static int SelfTest()
    {
        Console.WriteLine("\nthe CI picture tools: the noise threshold on CI's own pictures, --compare and --gallery on pictures made here");
        int failed = 0;
        void Check(string what, object? got, object? want)
        {
            bool ok = Equals(got, want);
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what}{(ok ? "" : $": got {got}, want {want}")}");
            if (!ok) failed++;
        }
        string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-pictures-" + Guid.NewGuid().ToString("N"));
        try
        {
            // ---- the noise threshold on CI's own pictures (2026-11-27). Each is cropped to the area
            // that differed and 4 pixels round it, as far as the anti-aliasing test looks, so its
            // counts are the whole picture's
            string data = Path.Combine(AppContext.BaseDirectory, "test-data", "compare");
            Measured Of(string c) => Measure(Pixels.Load(Path.Combine(data, c, "before.png")), Pixels.Load(Path.Combine(data, c, "after.png")));
            string Counts(Measured m) => $"{m.Differing} px differ, {m.AntiAliased} anti-aliasing, {m.Faint} faint, {m.Shifted} shifted, {m.Counted} counted, largest clump {m.LargestClump}";
            int Most(Measured m) => m.Moves.Select(v => Math.Max(Math.Abs(v.Dx), Math.Abs(v.Dy))).DefaultIfEmpty(0).Max();
            foreach (var (c, what, px) in new[]
            {
                ("noise-dark-exchange", "Dark: the Exchange", 8),
                ("noise-light-exchange", "Light: the Exchange", 8),
                ("noise-dark-hall-of-records", "Dark: the Hall of Records", 6),
                ("noise-light-hall-of-records", "Light: the Hall of Records", 3),
                ("noise-light-ordering-info", "Light: Ordering Info", 9),
                ("noise-light-town-guild", "Light: the town, Guild", 6),
            })
            {
                var m = Of(c);
                Check($"the noise threshold, windows-latest run to run, {what} ({Counts(m)}): minor", (m.Verdict, m.Differing), (Verdict.Minor, px));
            }
            foreach (var (c, what) in new[]
            {
                ("real-char-tab-clock", "the Char tab's clock stopped (Windows ARM64)"),
                ("real-ordering-info-grew", "Ordering Info 15 pixels taller (Windows ARM64)"),
                ("real-font-switch", "the Character List in another face (Linux x64)"),
                ("real-misc-tab-box", "an 11x11 box drawn otherwise on the Misc tab (Windows ARM64)"),
            })
            {
                var m = Of(c);
                Check($"the noise threshold, a real change, {what} ({Counts(m)}): changed", m.Verdict, Verdict.Changed);
            }
            // ---- the shift test on the Avalonia 12 trial's pictures (2026-11-27, Windows), cropped the same
            // way with 7 pixels round the area: text moved, the Stats buttons a pixel narrower (their
            // right edges moved 2 px, their left 1), and the Help's placeholder drawn a lighter grey
            foreach (var (c, what, most) in new[]
            {
                ("shift-dark-about-ok", "Dark: the About box's OK, 1 px right", 1),
                ("shift-light-stats-buttons", "Light: Create a Character's Race Stats and Guild Stats, edges and text 1 and 2 px left", 2),
            })
            {
                var m = Of(c);
                Check($"the shift test, Avalonia 12, {what} ({Counts(m)}): minor, shifted by up to {most} px", (m.Verdict, Most(m), m.Shifted > 0), (Verdict.Minor, most, true));
            }
            var help = Of("real-light-help-placeholder");
            Check($"the shift test, Avalonia 12, Light: the Help's placeholder a lighter grey ({Counts(help)}): changed, nothing shifted", (help.Verdict, help.Shifted), (Verdict.Changed, 0));

            // ---- the noise threshold on pictures made here
            Pixels Make(int w, int h, uint colour, Action<uint[], int>? draw = null)
            {
                var p = new Pixels { Width = w, Height = h, Data = Enumerable.Repeat(colour, w * h).ToArray() };
                draw?.Invoke(p.Data, w);
                return p;
            }
            const uint Grey = 0xFFC0C0C0, Black = 0xFF000000, White = 0xFFFFFFFF;
            var plain = Make(40, 30, Grey);
            Check("the noise threshold: the same picture is unchanged", Measure(plain, plain).Verdict, Verdict.Unchanged);
            var faint = Measure(plain, Make(40, 30, 0xFFC1C1C1));
            Check("the noise threshold: every pixel a shade off, too faint to see, is minor and none counted", (faint.Verdict, faint.Differing, faint.Faint, faint.Counted), (Verdict.Minor, 1200, 1200, 0));
            // an edge: black, a column of grey between, white; the grey column redrawn lighter is anti-aliasing
            Action<uint[], int> Edge(uint middle) => (d, w) => { for (int i = 0; i < d.Length; i++) d[i] = i % w < 10 ? Black : i % w == 10 ? middle : White; };
            var edge = Measure(Make(20, 20, 0, Edge(0xFF808080)), Make(20, 20, 0, Edge(0xFFB0B0B0)));
            Check("the noise threshold: an edge's blended pixels redrawn are anti-aliasing, not counted", (edge.Verdict, edge.Differing, edge.AntiAliased, edge.Counted), (Verdict.Minor, 20, 20, 0));
            var dots = Measure(plain, Make(40, 30, Grey, (d, w) => { foreach (var (x, y) in new[] { (3, 3), (20, 4), (30, 20), (8, 25) }) d[y * w + x] = Black; }));
            Check("the noise threshold: four lone pixels are minor", (dots.Verdict, dots.Counted, dots.LargestClump), (Verdict.Minor, 4, 1));
            var many = Measure(plain, Make(40, 30, Grey, (d, w) => { for (int k = 0; k <= MinorMostPixels; k++) d[(1 + (k / 9) * 3) * w + 1 + (k % 9) * 4] = Black; }));
            Check($"the noise threshold: {MinorMostPixels + 1} lone pixels are too many, changed", (many.Verdict, many.Counted, many.LargestClump), (Verdict.Changed, MinorMostPixels + 1, 1));
            var clump = Measure(plain, Make(40, 30, Grey, (d, w) => { d[5 * w + 5] = d[5 * w + 6] = d[6 * w + 6] = d[7 * w + 7] = Black; }));
            Check($"the noise threshold: a clump of {MinorLargestClump + 1} touching pixels is changed", (clump.Verdict, clump.LargestClump), (Verdict.Changed, MinorLargestClump + 1));
            Check("the noise threshold: another size is changed", Measure(plain, Make(40, 31, Grey)).Verdict, Verdict.Changed);

            // ---- the shift test on pictures made here: words in a small pixel font, its curves' corners half ink
            var font = new Dictionary<char, string[]>
            {
                ['M'] = new[] { "#...#", "##.##", "#+#+#", "#.#.#", "#...#", "#...#", "#...#" },
                ['O'] = new[] { "+###+", "#...#", "#...#", "#...#", "#...#", "#...#", "+###+" },
                ['R'] = new[] { "####+", "#...#", "#...#", "####+", "#.#..", "#..#.", "#...#" },
                ['D'] = new[] { "###+.", "#..+#", "#...#", "#...#", "#...#", "#..+#", "###+." },
            };
            const uint Navy = 0xFF000080, MidGrey = 0xFF808080;
            uint Half(uint c1, uint c2) => 0xFF000000 | (((c1 >> 16 & 0xFF) + (c2 >> 16 & 0xFF)) / 2 << 16) | (((c1 >> 8 & 0xFF) + (c2 >> 8 & 0xFF)) / 2 << 8) | ((c1 & 0xFF) + (c2 & 0xFF)) / 2;
            Pixels Words(params (string Text, int X, int Y, uint Ink)[] words) => Make(60, 34, Grey, (d, w) =>
            {
                foreach (var (text, x, y, ink) in words)
                    for (int k = 0; k < text.Length; k++)
                        for (int r = 0; r < 7; r++)
                            for (int c = 0; c < 5; c++)
                                if (font[text[k]][r][c] is char g && g != '.') d[(y + r) * w + x + k * 6 + c] = g == '#' ? ink : Half(ink, Grey);
            });
            var word = Words(("MORDOR", 6, 6, Navy));
            foreach (var (dx, dy, what) in new[] { (1, 0, "1 px right"), (-2, 1, "2 px left and 1 down") })
            {
                var m = Measure(word, Words(("MORDOR", 6 + dx, 6 + dy, Navy)));
                Check($"the shift test: a word moved {what} ({Counts(m)}): minor, shifted by up to {Math.Max(Math.Abs(dx), Math.Abs(dy))} px, the move found",
                      (m.Verdict, Most(m), m.Moves.Select(v => (v.Dx, v.Dy)).Distinct().SingleOrDefault()), (Verdict.Minor, Math.Max(Math.Abs(dx), Math.Abs(dy)), (dx, dy)));
            }
            var far = Measure(word, Words(("MORDOR", 9, 6, Navy)));
            Check($"the shift test: a word moved 3 px, past {ShiftMost} ({Counts(far)}): changed", (far.Verdict, far.Shifted), (Verdict.Changed, 0));
            var both = Measure(Words(("MORDOR", 6, 6, Navy), ("DOOR", 6, 22, Navy)), Words(("MORDOR", 7, 6, Navy), ("DOOR", 6, 22, MidGrey)));
            Check($"the shift test: a word moved 1 px and another drawn grey ({Counts(both)}): changed, the moved one shifted and the grey one counted",
                  (both.Verdict, both.Shifted > 0, both.Counted > MinorMostPixels), (Verdict.Changed, true, true));
            // a stroke grown a pixel thicker matches a move, but only goes darker; and so do a wide box's
            // edges, moved without the rest of the box between them (more than ShiftPair apart)
            Action<uint[], int> Bar(int x0, int width) => (d, w) => { for (int y = 5; y < 20; y++) for (int x = x0; x < x0 + width; x++) d[y * w + x] = Navy; };
            var thick = Measure(Make(40, 30, Grey, Bar(10, 3)), Make(40, 30, Grey, Bar(10, 4)));
            Check($"the shift test: a stroke 3 px wide drawn 4 ({Counts(thick)}): changed", (thick.Verdict, thick.Shifted), (Verdict.Changed, 0));
            var box = Measure(Make(80, 30, Grey, Bar(5, 60)), Make(80, 30, Grey, Bar(6, 60)));
            Check($"the shift test: a box 60 px wide moved 1 px, its edges alone ({Counts(box)}): changed", (box.Verdict, box.Shifted), (Verdict.Changed, 0));

            // ---- --compare
            void Png(string path, int w, int h, uint colour, (int X, int Y, int W, int H)? block = null)
            {
                using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                bmp.Erase(new SKColor(colour));
                if (block is { } b)
                    for (int y = b.Y; y < b.Y + b.H; y++)
                        for (int x = b.X; x < b.X + b.W; x++) bmp.SetPixel(x, y, SKColors.Black);
                SavePng(bmp, path);
            }
            string a = Path.Combine(scratch, "a"), bDir = Path.Combine(scratch, "b");
            Png(Path.Combine(a, "Light_one.png"), 40, 30, 0xFFC0C0C0);
            Png(Path.Combine(a, "Dark_one.png"), 40, 30, 0xFF202020);
            Png(Path.Combine(a, "Light_gone.png"), 10, 10, 0xFFFFFFFF);
            Png(Path.Combine(a, "playthrough", "05-sparring-room.png"), 10, 10, 0xFF00FF00);
            Png(Path.Combine(a, "playthrough", "10-main-menu.png"), 10, 10, 0xFF0000FF);

            var same = Compare(a, a, Path.Combine(scratch, "same"));
            Check("--compare, a folder with itself: nothing changed, minor, new or removed", (same.FirstRun, same.Changed.Count, same.Minor.Count, same.Added.Count, same.Removed.Count, same.Unchanged), (false, 0, 0, 0, 0, 4));

            Png(Path.Combine(bDir, "Light_one.png"), 40, 30, 0xFFC0C0C0, (7, 5, 6, 4));
            Png(Path.Combine(bDir, "Dark_one.png"), 40, 30, 0xFF202020, (30, 20, 1, 1));
            Png(Path.Combine(bDir, "Light_new.png"), 10, 10, 0xFFFFFFFF);
            Png(Path.Combine(bDir, "playthrough", "05-sparring-room.png"), 10, 10, 0xFFFF0000);
            Png(Path.Combine(bDir, "playthrough", "10-main-menu.png"), 10, 10, 0xFF0000FF);
            string outB = Path.Combine(scratch, "b-compare");
            var one = Compare(a, bDir, outB);
            Check("--compare, a block of 6x4 drawn: changed, its box", one.Changed.Select(c => $"{c.Name} {c.Counted} px at {c.X},{c.Y} {c.Width}x{c.Height}").FirstOrDefault(), "Light_one.png 24 px at 7,5 6x4");
            Check("--compare: and only the one changed", one.Changed.Count, 1);
            Check("--compare, one pixel changed: minor, not changed", one.Minor.Select(c => $"{c.Name} {c.Pixels} px").FirstOrDefault(), "Dark_one.png 1 px");
            Check("--compare: one new, one removed", (string.Join(",", one.Added), string.Join(",", one.Removed)), ("Light_new.png", "Light_gone.png"));
            Check("--compare: the playthrough's fights are not compared", one.Changed.Concat(one.Minor).Any(c => c.Name.Contains("sparring")), false);
            string at = Path.Combine(outB, "changed", "Light_one");
            Check("--compare: before, after and the zoomed crops written",
                  new[] { "before", "after", "zoom-1-before", "zoom-1-after" }.All(n => File.Exists(Path.Combine(at, n + ".png"))) && File.Exists(Path.Combine(outB, "minor", "Dark_one", "after.png")), true);
            // the crop: the block and 8 pixels round it, inside the picture (0,0 to 21,17), 4 times over
            var zoomed = Pixels.Load(Path.Combine(at, "zoom-1-after.png"));
            Check("--compare: the crop is the area and a margin, enlarged 4 times", (zoomed.Width, zoomed.Height), (21 * 4, 17 * 4));
            Check("--compare: enlarged with square pixels, no smoothing: the block's corner and the pixel before it",
                  (zoomed.At(7 * 4, 5 * 4), zoomed.At(7 * 4 - 1, 5 * 4), Pixels.Load(Path.Combine(at, "zoom-1-before.png")).At(7 * 4, 5 * 4)), (0xFF000000u, 0xFFC0C0C0u, 0xFFC0C0C0u));
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(outB, "compare.json")))!;
            Check("--compare: compare.json says it", (json["changed"]!.AsArray().Count, json["minor"]!.AsArray().Count, json["added"]!.AsArray().Count, json["removed"]!.AsArray().Count, json["firstRun"]!.GetValue<bool>()), (1, 1, 1, 1, false));
            Check("--compare: compare.json keeps every compared picture's counts",
                  string.Join(",", json["pictures"]!.AsArray().Select(p => $"{p!["name"]} {p["class"]} {p["pixels"]}/{p["counted"]}")),
                  "Dark_one.png minor 1/1,Light_one.png changed 24/24,playthrough/10-main-menu.png unchanged 0/0");
            Check("--compare: compare.json gives the crop", json["changed"]![0]!["areas"]![0]!["zoom"]?["after"]?.GetValue<string>(), "changed/Light_one/zoom-1-after.png");
            string summary = File.ReadAllText(Path.Combine(outB, "summary.md"));
            Check("--compare: summary.md counts it, the changed listed and the minor in a note after",
                  (summary.Contains("1 visibly changed, 1 minor, 1 new, 1 removed"), summary.IndexOf("| `Light_one.png` | changed", StringComparison.Ordinal) is int ci && ci > 0
                   && summary.IndexOf("Minor, ", StringComparison.Ordinal) > ci, summary.Contains("`Dark_one.png` 1 px"), summary.Contains("| `Dark_one.png`")), (true, true, true, false));
            var first = Compare(Path.Combine(scratch, "nowhere"), bDir, Path.Combine(scratch, "first"));
            Check("--compare, no baseline: a first run, not a failure", (first.FirstRun, first.Changed.Count, File.ReadAllText(Path.Combine(scratch, "first", "summary.md")).Contains("First run")), (true, 0, true));
            // the command itself, its report kept off this run's output
            var said = Console.Out;
            int code;
            try { Console.SetOut(TextWriter.Null); code = CompareMain(new[] { "--compare", a, bDir, Path.Combine(scratch, "cmd") }); }
            finally { Console.SetOut(said); }
            Check("--compare: the command exits 0 whatever it finds, and writes its report", (code, File.Exists(Path.Combine(scratch, "cmd", "compare.json"))), (0, true));

            // ---- the gallery: two machines, one with a check failing, a picture changed and one minor
            string arts = Path.Combine(scratch, "artifacts");
            string win = Path.Combine(arts, "screenshots-Windows-X64"), lin = Path.Combine(arts, "screenshots-Linux-ARM64");
            foreach (var f in Directory.GetFiles(bDir, "*.png", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(bDir, f);
                Copy(f, Path.Combine(win, rel));
                if (!rel.Contains("new")) Copy(f, Path.Combine(lin, rel));
            }
            File.WriteAllText(Path.Combine(win, ChecksFile), ChecksJson(new[]
            {
                ("Light_one.png", new List<Failure> { new("clipped", "TextBlock \"A caption\": needs 64 px wide, has 60", false, null) }),
                ("Dark_one.png", new List<Failure> { new("overflow", "Border: 3 px past the right", true, "the original's form is as wide") }),
                ("Light_new.png", new List<Failure>()),
            }));
            Compare(a, win, Path.Combine(win, CompareFolder));
            string gal = Path.Combine(scratch, "gallery");
            var (rows, cols) = Gallery(arts, gal);
            string html = File.ReadAllText(Path.Combine(gal, "index.html"));
            int Count(string s) { int n = 0, i = 0; while ((i = html.IndexOf(s, i, StringComparison.Ordinal)) >= 0) { n++; i += s.Length; } return n; }
            string RowClass(string file) => System.Text.RegularExpressions.Regex.Match(html, "<tr data-row=\"\\d+\" class=\"([^\"]*)\"><th scope=\"row\">[^<]*<span class=\"file\">" + System.Text.RegularExpressions.Regex.Escape(file) + "<").Groups[1].Value;
            Check("--gallery: a row for each picture (3 readability, 2 playthrough) and a column for each machine", (rows, cols, Count("<tr data-row="), Count("class=\"machine\"")), (5, 5, 5, 5));
            Check("--gallery: Light and Dark of a screen side by side, the playthrough after",
                  string.Join(",", System.Text.RegularExpressions.Regex.Matches(html, "<span class=\"file\">([^<]*)</span>").Select(m => m.Groups[1].Value)),
                  "Light_one.png,Dark_one.png,Light_new.png,playthrough/05-sparring-room.png,playthrough/10-main-menu.png");
            Check("--gallery: the changed cell is marked, its area ringed on the thumbnail, and opens on the change",
                  (Count("<span class=\"badge\">changed</span>"), Count("<span class=\"mark\""), html.Contains("compare/changed/Light_one/zoom-1-after.png"), html.Contains("data-view=\"v")), (1, 1, true, true));
            Check("--gallery: the minor cell is not marked changed, its row not either, and the minor list has it",
                  (Count("<div class=\"tag minor\">"), RowClass("Dark_one.png"), html.Contains("<details class=\"minor-list\">"), Count("<td>Windows x64</td><td>Dark: one</td>")), (1, "row", true, 1));
            Check("--gallery: the filter keeps the visibly changed or failing rows", (RowClass("Light_one.png"), html.Contains(".only .row:not(.changed):not(.failing)")), ("row changed failing", true));
            Check("--gallery: the header counts visibly changed and minor apart",
                  html.Contains("<b>Windows x64</b>: 1 visibly changed, 1 minor, 1 failing</li>") && html.Contains("<b>Linux ARM64</b>: 0 visibly changed, 0 minor, 0 failing (not compared)</li>"), true);
            Check("--gallery: the failing cell is marked red, the known finding is not", (Count("class=\"cell failing\"") + Count("class=\"cell changed failing\""), Count("class=\"known\"")), (1, 1));
            Check("--gallery: missing machines and pictures are empty cells", Count("<div class=\"none\">no picture</div>"), 5 * 5 - 5 - 4);
            Check("--gallery: the pictures, thumbnails and crops copied beside it",
                  File.Exists(Path.Combine(gal, "pictures", "screenshots-Linux-ARM64", "playthrough", "10-main-menu.png")) && File.Exists(Path.Combine(gal, "thumbs", "screenshots-Windows-X64", "Light_one.png"))
                  && File.Exists(Path.Combine(gal, "pictures", "screenshots-Windows-X64", "compare", "changed", "Light_one", "zoom-1-before.png"))
                  && File.Exists(Path.Combine(gal, "pictures", "screenshots-Windows-X64", "compare", "minor", "Dark_one", "before.png")), true);
            Check("--gallery: the flip, by a click and the F key", (html.Contains("stage.addEventListener('click', flip)"), html.Contains("e.key === 'f'")), (true, true));
            Check("--gallery: nothing loaded from elsewhere", System.Text.RegularExpressions.Regex.IsMatch(html, "(src|href)=\"(https?:)?//|https?://"), false);

            // ---- --compare and --gallery on a word moved 1 px right: minor, and what moved said
            string shiftBase = Path.Combine(scratch, "shift-base"), shiftMachine = Path.Combine(scratch, "shift-artifacts", "screenshots-Windows-X64");
            SavePng(word.Data, word.Width, word.Height, Path.Combine(shiftBase, "Light_word.png"));
            var right = Words(("MORDOR", 7, 6, Navy));
            SavePng(right.Data, right.Width, right.Height, Path.Combine(shiftMachine, "Light_word.png"));
            string shiftOut = Path.Combine(shiftMachine, CompareFolder);
            var sr = Compare(shiftBase, shiftMachine, shiftOut);
            Check("--compare, a word moved 1 px: minor, its pixels shifted and none counted",
                  (sr.Changed.Count, sr.Minor.Count, sr.Minor.Select(c => (c.Shifted > 0, c.Counted, MovedWords(c.Moves).StartsWith("text moved 1 px right in ", StringComparison.Ordinal))).FirstOrDefault()), (0, 1, (true, 0, true)));
            var sj = JsonNode.Parse(File.ReadAllText(Path.Combine(shiftOut, "compare.json")))!;
            var sm = sj["minor"]![0]!;
            Check("--compare: compare.json gives what moved, and the rule its reach",
                  (sm["shiftedBy"]!.GetValue<int>(), sm["moves"]![0]!["dx"]!.GetValue<int>(), sm["moves"]![0]!["dy"]!.GetValue<int>(), sm["shifted"]!.GetValue<int>() > 0, sj["rule"]!["shiftMost"]!.GetValue<int>(), sj["pictures"]![0]!["shifted"]!.GetValue<int>() > 0),
                  (1, 1, 0, true, ShiftMost, true));
            Check("--compare: summary.md lists it with the minor, shifted", File.ReadAllText(Path.Combine(shiftOut, "summary.md")).Contains(", shifted by up to 1 px"), true);
            string shiftGal = Path.Combine(scratch, "shift-gallery");
            Gallery(Path.Combine(scratch, "shift-artifacts"), shiftGal);
            string sh = File.ReadAllText(Path.Combine(shiftGal, "index.html"));
            Check("--gallery: a picture of text moved is minor, shifted, in the minor list with what moved, and its view says so",
                  (System.Text.RegularExpressions.Regex.IsMatch(sh, "<div class=\"tag minor\">minor: \\d+ px, shifted by up to 1 px "), sh.Contains("<span class=\"badge\">changed</span>"),
                   sh.Contains("or text moved by a pixel or two (1 of them)"), sh.Contains("; text moved 1 px right in "), sh.Contains("\"m\":\"Text moved 1 px right in "), sh.Contains("if (c.m)")),
                  (true, false, true, true, true, true));
        }
        catch (Exception ex) { Check($"the picture tools ran: {ex.GetType().Name}: {ex.Message}", false, true); }
        finally
        {
            if (new DirectoryInfo(scratch).Parent?.FullName == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                try { Directory.Delete(scratch, true); } catch { }
        }
        return failed;
    }
}
