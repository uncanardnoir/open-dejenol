using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Mordor.App;

/// <summary>
/// The two pictures that flank the monster in the Dungeon window, out of MG11.DLL's <c>"MISC"</c>
/// resource - 270x45, six 45x45 cells, loaded by GRAPHICS.BAS:91 into <c>gv2BBC</c> and drawn by
/// exactly two routines. Like <see cref="AutomapPics"/> it had never been extracted; unlike it,
/// neither picture existed here in any form.
///
/// <c>sub0B9B</c> (MONSMOD.BAS:4434-4471) draws one at <c>x = 3</c>, the left-hand end of
/// picMonsters, and it says whether the group has engaged you:
///
/// <code>
/// 0  skull and crossbones   Foes(0).Engaged = 1 - they are hostile
/// 1  peace sign             anything else - they are on your square and not coming
/// 5  a demon's head in fire  the same, for Asmodeus, monster id 277
/// </code>
///
/// <c>monShowChest</c> (MONSMOD.BAS:4064-4123) draws the other against the right-hand end, and it
/// is the chest the encounter rolled: <c>(2 * 45) + ((TreasureClass - 2) * 45)</c>, so class 2 is
/// cell 2 and class 3 is cell 3 - the only two <see cref="Mordor.Core.Rules.ChestClass"/> returns -
/// with cell 4 kept for Asmodeus. There is no cell for class 0, because a fight that left nothing
/// paints that corner back to the window's own colour instead.
///
/// So the pictures are not decoration: the left one is the only thing on screen that distinguishes
/// a group standing on your square from a group attacking you, and the right one tells you a chest
/// is there before you read a word.
/// </summary>
public static class MiscPics
{
    public const int Cell = 45;    // mc0622, MONSMOD.BAS:4074

    /// <summary>They have engaged you.</summary>
    public const int Hostile = 0;
    /// <summary>They are here and are not coming.</summary>
    public const int AtPeace = 1;
    /// <summary>Asmodeus, engaged.</summary>
    public const int AsmodeusHostile = 5;
    /// <summary>Asmodeus's chest, whatever class the encounter rolled.</summary>
    public const int AsmodeusChest = 4;

    /// <summary>The chest for a treasure class: cell 2 for class 2, cell 3 for class 3.</summary>
    public static int Chest(int treasureClass) => treasureClass;

    private static Bitmap? _sheet;

    private static Bitmap Sheet =>
        _sheet ??= Art.Bitmap("combat-misc.png");

    private static readonly CroppedBitmap?[] _cells = new CroppedBitmap?[6];

    /// <summary>One 45x45 cell, or null if the number is not one of the six.</summary>
    public static CroppedBitmap? Get(int cell)
    {
        if (cell < 0 || cell >= _cells.Length) return null;
        return _cells[cell] ??= new CroppedBitmap(Sheet, new PixelRect(cell * Cell, 0, Cell, Cell));
    }
}
