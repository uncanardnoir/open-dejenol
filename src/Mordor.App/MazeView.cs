using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The dungeon view, composed the way the original composes it rather than projected.
///
/// See docs/DUNGEON-VIEW.md. A 100x100 buffer is built from two 50-wide background columns, then
/// up to eight fixed-size wall pieces are stamped on top, and only at the very end is the whole
/// buffer stretched to fill the panel. Nothing is scaled before that, which is why the artwork is
/// cut into these particular slabs.
///
/// This is MODULE3.BAS lines 1354-1460, the tail of sub0980 that MazeSelect stops at. Which
/// pieces to draw is <see cref="MazeSelect"/>'s answer; this only places them.
/// </summary>
public static class MazeView
{
    /// <summary>The offscreen buffer the original builds, in pixels.</summary>
    public const int Size = 100;

    /// <summary>The original's mc0232 (MODULE3.BAS:35): half the buffer, and one tile.</summary>
    private const int Tile = 50;

    // Square flags the view itself reads, MAIN.TXT:641-646.
    private const int BitWater = 15;      // gc1D34, drawn from the WATR resource
    private const int BitQuicksand = 16;  // gc1D36, drawn from the QSND resource
    private const int BitDark = 20;       // gc1D3E, the whole view becomes FOG

    /// <summary>
    /// The eight overlay slots, MODULE1.BAS:577-600: width, height, and where each begins in the
    /// 200-wide DOOR strip. The right-hand variants start at slot 7's end, which is 100.
    /// </summary>
    private static readonly (int W, int H, int SrcX)[] Slots =
    {
        (11, 79, 0), (5, 53, 11), (40, 53, 16), (10, 28, 56),
        (7, 28, 66), (18, 28, 73), (9, 52, 91), (18, 28, 100),
    };

    /// <summary>Where the right-hand variants begin: gv1BB6(6).M425F + gv1BB6(6).M4216.</summary>
    private static readonly int RightSrcBase = Slots[6].SrcX + Slots[6].W;

    /// <summary>
    /// VB computes these placements in floating point and rounds on assignment to an Integer, so
    /// a half lands on the even neighbour. Slot 7 is the one where this actually differs from
    /// truncation: 25 - 9/2 is 20 here, not 21.
    /// </summary>
    private static int CInt(double x) => (int)Math.Round(x, MidpointRounding.ToEven);

    /// <summary>
    /// The left-hand variant's position and source column, MODULE3.BAS:1377-1408. Slot 8 draws
    /// slot 6's artwork - the original passes gv1BB6(5).M425F rather than its own.
    /// </summary>
    private static (int X, int Y, int SrcX)? LeftPlace(int i)
    {
        var (w, h, srcX) = Slots[i];
        return i switch
        {
            0 => (0, Size - h, srcX),
            1 => (0, Size - h - 16, srcX),
            2 => (CInt(Size / 2.0 - w / 2.0), Size - h - 16, srcX),
            3 => (16 - w, Size - h - 32, srcX),
            4 => (17, Size - h - 32, srcX),
            5 => (CInt(w / 3.0), Size - h - 32, srcX),
            6 => (CInt(25 - w / 2.0), Size - h - 20, srcX),
            7 => (CInt(Tile - w / 2.0), Size - h - 32, Slots[5].SrcX),
            _ => null,
        };
    }

    /// <summary>
    /// The right-hand variant, MODULE3.BAS:1412-1437. Slots 3 and 8 have no case there, so they
    /// are never drawn on the right however the selection logic flags them.
    /// </summary>
    private static (int X, int Y, int SrcX)? RightPlace(int i)
    {
        var (w, h, srcX) = Slots[i];
        int src = RightSrcBase + srcX;
        return i switch
        {
            0 => (Size - w, Size - h, src),
            1 => (Size - w, Size - h - 16, src),
            3 => (Size - (16 - w), Size - h - 32, src),
            4 => (Size - 18 - w, Size - h - 32, src),
            5 => (CInt(Size - w / 3.0 - w), Size - h - 32, src),
            6 => (CInt(Size - 24 - w / 2.0), Size - h - 19, src),
            _ => null,
        };
    }

    private static Bitmap? _maze, _door, _blnk, _fog, _watr, _qsnd;

    private static Bitmap Load(string name) => Art.Bitmap(name);

    /// <summary>The six MG11.DLL resources the view uses, bound in MODULE12.BAS:77-103.</summary>
    public static void EnsureLoaded()
    {
        _maze ??= Load("maze_corridor.png");    // MAZE, 25 columns x 4 rows of 50x50
        _door ??= Load("maze_overlays.png");    // DOOR, 200 x 79, left variants then right
        _blnk ??= Load("maze_backdrop.png");    // BLNK, 100 x 100 plain corridor
        _fog ??= Load("maze_dark.png");         // FOG,  50 x 50, tiled when the square is unlit
        _watr ??= Load("maze_water.png");       // WATR, 50 x 20
        _qsnd ??= Load("maze_quicksand.png");   // QSND, 50 x 18
    }

    /// <summary>
    /// Draw one frame of <paramref name="view"/>. <paramref name="squareWalls"/> is the wall word
    /// of the square the party is standing on, which carries the dark, water and quicksand flags.
    /// </summary>
    public static void Render(DrawingContext ctx, Rect into, MazeSelect.View view, int squareWalls)
    {
        EnsureLoaded();
        double sx = into.Width / Size, sy = into.Height / Size;

        void Blit(Bitmap src, Rect from, int dx, int dy, int dw, int dh) =>
            ctx.DrawImage(src, from, new Rect(into.X + dx * sx, into.Y + dy * sy, dw * sx, dh * sy));

        bool Flag(int bit) => (squareWalls & (1 << bit)) != 0;

        if (Flag(BitDark))
        {
            // MODULE3.BAS:1355-1358 - four quadrants of FOG, and nothing else from the maze
            for (int qx = 0; qx < 2; qx++)
                for (int qy = 0; qy < 2; qy++)
                    Blit(_fog!, new Rect(0, 0, Tile, Tile), qx * Tile, qy * Tile, Tile, Tile);
        }
        else
        {
            // the two background halves: the left takes MAZE rows 2 and 3, the right rows 0 and 1
            int left = view.Bg[0], right = view.Bg[1];

            if (left > 0)
            {
                Blit(_maze!, new Rect((left - 1) * Tile, Tile * 2, Tile, Tile), 0, 0, Tile, Tile);
                Blit(_maze!, new Rect((left - 1) * Tile, Tile * 3, Tile, Tile), 0, Tile, Tile, Tile);
            }
            else Blit(_blnk!, new Rect(0, 0, Tile, Size), 0, 0, Tile, Size);

            if (right > 0)
            {
                Blit(_maze!, new Rect((right - 1) * Tile, 0, Tile, Tile), Tile, 0, Tile, Tile);
                Blit(_maze!, new Rect((right - 1) * Tile, Tile, Tile, Tile), Tile, Tile, Tile, Tile);
            }
            else Blit(_blnk!, new Rect(Tile, 0, Tile, Size), Tile, 0, Tile, Size);

            // then the wall pieces, slot 1 first as the original walks them
            for (int i = 0; i < Slots.Length; i++)
            {
                int word = view.Slot[i + 1];
                if (word <= 0) continue;
                var (w, h, _) = Slots[i];

                if ((word & MazeSelect.SlotLeft) != 0 && LeftPlace(i) is var (lx, ly, lsrc))
                    Blit(_door!, new Rect(lsrc, 0, w, h), lx, ly, w, h);

                if ((word & MazeSelect.SlotRight) != 0 && RightPlace(i) is var (rx, ry, rsrc))
                    Blit(_door!, new Rect(rsrc, 0, w, h), rx, ry, w, h);
            }
        }

        // MODULE3.BAS:1449-1460 - a strip across the bottom of both halves, drawn over the fog as
        // well, so you can still tell you are standing in water when the square is dark.
        const int StripH = 18, StripW = 50;
        if (Flag(BitWater))
            for (int half = 0; half < 2; half++)
                Blit(_watr!, new Rect(0, 0, StripW, StripH), half * Tile, Size - StripH, StripW, StripH);

        if (Flag(BitQuicksand))
            for (int half = 0; half < 2; half++)
                Blit(_qsnd!, new Rect(0, 0, StripW, StripH), half * Tile, Size - StripH, StripW, StripH);

        // The original has a second FOG pass at MODULE3.BAS:1443, guarded by m01C6 - which line
        // 1352 has just set to False and nothing in between touches. It is unreachable, so there
        // is nothing here for it.
    }
}
