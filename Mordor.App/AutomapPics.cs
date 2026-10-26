using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The Automap's icons, which are the original's own.
///
/// <c>GRAPHICS.BAS:110</c> loads a bitmap resource named <c>"PICS"</c> out of MG11.DLL into
/// <c>gv2BC6</c>, and <c>medDrawSquare</c> (FRMAUTOM.BAS:352-392) is the only thing that draws
/// from it. It had never been extracted - the port drew the map with lines and letters of its own
/// invention - so it is read out of the player's MG11.DLL at start, pixel for pixel the resource
/// (<c>automap-pics.png</c> in the art cache, see <see cref="Art"/>), 405x15 in one row.
///
/// The row is twenty-seven 15x15 cells and the numbering is not arbitrary: <c>medDrawSquare</c>
/// walks <c>For l0104 = 0 To 22</c> over the square's flag word and blits cell <c>l0104</c>
/// whenever bit <c>l0104</c> is set, so **cell number = flag bit number**. Cells 0-22 are the
/// twenty-three flag bits in <see cref="Floor"/>'s own order, which is how the whole sheet
/// identifies itself:
///
/// <code>
///  0 WALL_EAST          vertical white line        12 STAIRS_UP     red stair, arrow up
///  1 WALL_NORTH         horizontal white line      13 STAIRS_DOWN   red stair, arrow down
///  2 DOOR_EAST          vertical line, gap         14 TELEPORTER    white pinwheel
///  3 DOOR_NORTH         horizontal line, gap       15 WATER         blue waves
///  4 SECRET_DOOR_EAST   vertical line, box         16 QUICKSAND     scattered dots
///  5 SECRET_DOOR_NORTH  horizontal line, box       17 ROTATOR       four white arrows
///  6 FACE_NORTH         green arrow up             18 ANTIMAGIC     white box in a box
///  7 FACE_EAST          green arrow right          19 ROCK          solid white square
///  8 FACE_SOUTH         green arrow down           20 FOG           blue chequer
///  9 FACE_WEST          green arrow left           21 CHUTE         red chute
/// 10 EXTINGUISHER       hatched box                22 STUD          white dotted box
/// 11 PIT                box with an X
/// </code>
///
/// Cells 23-26 are outside the loop: they are the magenta arrow that marks where you are standing,
/// blitted as <c>(Facing + 22)</c> with the original's one-based Facing, so 23 is north, 24 east,
/// 25 south, 26 west - the same order as the green arrows at 6-9.
///
/// Ten of the twenty-three arrive only by perception: walking onto a square writes six edge bits
/// and seven features (MAZEMOD.BAS:1610), and everything else reaches the file through sub087C,
/// which writes the bit it worked out (MAZEMOD.BAS:634-637). The four FACE arrows are among them -
/// they are forced-facing *squares*, not a drawing of where you are standing, and MAZEMOD.BAS:1709
/// perceives each one and sets your facing afterwards. This comment used to say they had no writer.
///
/// The routine has a third parameter naming a bit to blit with SRCINVERT when it is *not* set. All
/// nine call sites pass -1, so that half never runs, which is why only SRCPAINT and the cursor's
/// SRCCOPY are implemented below.
/// </summary>
public static class AutomapPics
{
    public const int Cell = 15;          // mc001C, FRMAUTOM.BAS:7
    public const int Pitch = Cell - 1;   // squares are laid out (n-1)*(mc001C-1) apart, so they share an edge

    /// <summary>The magenta "you are here" arrow: cell 23 + facing, north first.</summary>
    public const int YouAreHere = 23;

    /// <summary>A blit never leaves a pixel half-transparent; the sheet is an opaque DIB.</summary>
    private const int Opaque = unchecked((int)0xFF000000);

    private static int[]? _px;
    private static int _w;

    private static void Load()
    {
        if (_px != null) return;
        using var bmp = new Bitmap(Art.PathOf("automap-pics.png"));
        _w = bmp.PixelSize.Width;
        int h = bmp.PixelSize.Height;
        var px = new int[_w * h];
        IntPtr buf = Marshal.AllocHGlobal(px.Length * 4);
        try
        {
            bmp.CopyPixels(new PixelRect(0, 0, _w, h), buf, px.Length * 4, _w * 4);
            Marshal.Copy(buf, px, 0, px.Length);
        }
        finally { Marshal.FreeHGlobal(buf); }
        _px = px;
    }

    /// <summary>
    /// One icon onto the map, the way <c>medDrawSquare</c> does it: <c>SRCPAINT</c>, raster op
    /// <c>0x00EE0086</c> (the decompiler prints the constant as <c>134 ' ? &amp;H 0086 00EE</c>).
    /// That is a bitwise OR of source and destination, which is why the icons need no mask - the
    /// black around the glyph leaves whatever is underneath alone, and the black square the map
    /// has already painted for an explored cell shows the glyph in its own colours.
    /// </summary>
    public static void Paint(int[] dst, int dw, int dh, int cell, int x, int y)
    {
        Load();
        Blit(dst, dw, dh, cell, 0, 0, Cell, Cell, x, y, or: true);
    }

    /// <summary>
    /// The arrow for where you are standing. <c>medDrawCursor</c> (FRMAUTOM.BAS:324) takes an
    /// 11x11 crop from (2,2) inside the cell and blits it with <c>SRCCOPY</c>, not OR - it is the
    /// one thing on the map that paints over what is beneath it, so the arrow stays readable on a
    /// square that already carries a feature.
    /// </summary>
    public static void Copy(int[] dst, int dw, int dh, int cell, int x, int y)
    {
        Load();
        Blit(dst, dw, dh, cell, 2, 2, Cell - 4, Cell - 4, x, y, or: false);
    }

    private static void Blit(int[] dst, int dw, int dh, int cell, int sx, int sy, int w, int h,
                             int x, int y, bool or)
    {
        if (cell < 0 || (cell + 1) * Cell > _w) return;
        int src0 = cell * Cell + sx;
        for (int row = 0; row < h; row++)
        {
            int dy = y + row;
            if (dy < 0 || dy >= dh) continue;                 // the map clips at its own edge
            int s = (sy + row) * _w + src0;
            int d = dy * dw + x;
            for (int col = 0; col < w; col++)
            {
                int dx = x + col;
                if (dx < 0 || dx >= dw) continue;
                int v = _px![s + col];
                dst[d + col] = (or ? dst[d + col] | v : v) | Opaque;
            }
        }
    }
}
