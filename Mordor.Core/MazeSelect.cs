namespace Mordor.Core;

/// <summary>
/// Works out what the dungeon view should show: which background column each half uses and which
/// of the eight wall slabs are lit, on which side.
///
/// The body of this lives in MazeSelect.g.cs and MazeSelectPrune.g.cs, translated mechanically
/// from <c>sub0980</c> in MODULE3.BAS (MAZEMOD) by <c>tools/VbToCs</c> rather than transcribed by
/// hand - it is nearly five hundred conditionals and copying it out would have been an invitation
/// to a silent error. Regenerate those files rather than editing them; see tools/gen-mazeselect.sh.
///
/// The slice translated is MODULE3.BAS lines 846-1288 and 1315-1351, with the <c>GoSub L14530</c>
/// at line 1061 becoming a call to <see cref="Prune"/>, which is lines 1291-1312. Line 845 and
/// earlier is set-up the caller does; line 1352 onwards is the blitting, which MazeView does.
/// </summary>
public static partial class MazeSelect
{
    /// <summary>The map is a fixed 30x30 grid; <c>gv1864.M420C</c> is its width (MODULE3.BAS:73).</summary>
    public const int MapWidth = 30;

    // ---- constants the translated slice refers to by their original names --------------------

    // The four facings, MODULE3.BAS:9-12.
    private const int mc00D2 = 1, mc00D4 = 2, mc00D6 = 3, mc00D8 = 4;

    // Which side of the view an overlay slot is drawn on, MODULE3.BAS:33-34. These are bit
    // numbers, so they end up as values 2 and 4 in a slot word, not 1 and 2.
    private const int mc022C = 1, mc022E = 2;

    // Wall kinds, MAIN.TXT:626-632. gv1D16 is declared as a variable and never assigned, so it is
    // the zero of this run - the decompiler could not tell a zero-valued Const from a Global.
    private const int gv1D16 = 0, gc1D18 = 1, gc1D1A = 2, gc1D1C = 3, gc1D1E = 4, gc1D20 = 5;

    /// <summary>Slot word bit for the left-hand variant of an overlay (<c>mc022C</c>).</summary>
    public const int SlotLeft = 1 << mc022C;

    /// <summary>Slot word bit for the right-hand variant of an overlay (<c>mc022E</c>).</summary>
    public const int SlotRight = 1 << mc022E;

    /// <summary>
    /// The state the original keeps in locals while it decides: the view frustum, the two
    /// background halves, and the eight slot words.
    /// </summary>
    public sealed class View
    {
        /// <summary>The frustum, 1-based as the original indexes it; holds wall kind + 10.</summary>
        public int[] V = new int[26];

        /// <summary>Slot words, 1-based over the eight slots (<c>mc0204</c> = 8); entry 0 unused.</summary>
        public int[] Slot = new int[9];

        /// <summary>Background column for the left half (index 0) and right half (index 1).</summary>
        public int[] Bg = new int[2];

        public int X, Y, Facing, Level;

        /// <summary>The wall word of a map square, looked up by the caller (the original's fn0AD3).</summary>
        public Func<int, int> Walls = _ => 0;

        /// <summary>The party's linear square index, the original's fn0AE8 (MODULE3.BAS:73).</summary>
        public int Square => X + MapWidth * (Y - 1);
    }

    /// <summary>Bit test, the original's fn0380 (MODULE1.BAS:211).</summary>
    private static int Bit(int bit, int word) => (word & (1 << bit)) != 0 ? 1 : 0;

    /// <summary>
    /// Bit set, the original's fn0418 (MODULE1.BAS:1017). With a non-zero third argument it sets
    /// the bit; with zero it clears it. Only the setting form appears in this slice.
    /// </summary>
    private static int SetBit(int bit, int word, int set) =>
        set != 0 ? word | (1 << bit)
                 : Bit(bit, word) == 1 ? word ^ (1 << bit) : word;

    /// <summary>The wall word of one square of the map, by linear index.</summary>
    private static int WallsAt(View v, int square) => v.Walls(square);

    /// <summary>
    /// Decide the view for a position. <paramref name="walls"/> is asked for the wall word of a
    /// square given its linear index on a 30-wide map, which is how the original addresses it.
    /// </summary>
    public static View For(int x, int y, int facing, int level, Func<int, int> walls)
    {
        var v = new View { X = x, Y = y, Facing = facing, Level = level, Walls = walls };
        Select(v);
        return v;
    }
}
