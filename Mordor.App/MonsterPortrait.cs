using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// A creature's picture, cut from MG11.DLL's <c>"MONS"</c> - 600x540, a 10x9 grid of 60x60 cells
/// indexed by the data's <c>PicId</c>, which is one-based. The column and row are
/// <c>((PicId - 1) Mod 10)</c> and <c>Int((PicId - 1) / 10)</c>, which is written out twice in the
/// original and identically both times (FRMLIBRA.BAS:104-105, MONSMOD.BAS:4322-4323).
///
/// One creature is not in that sheet. <c>picMonster_Paint</c> asks <c>fn0C3B</c> first, and for
/// Asmodeus it draws the 100x100 <c>"ASMO"</c> resource instead (FRMLIBRA.BAS:108, 114) - the same
/// picture that replaces the whole dungeon view when you meet him. Nothing else in the game makes
/// that substitution: the portrait beside him in a fight comes from the sheet like anyone else's.
/// </summary>
public static class MonsterPortrait
{
    public const int Cell = 60;          // mc0090, FRMLIBRA.BAS:14

    private static Bitmap? _sheet, _asmodeus;
    private static readonly Dictionary<int, Bitmap> Cache = new();

    private static Bitmap Load(string name, ref Bitmap? slot) =>
        slot ??= Art.Bitmap(name);

    /// <summary>Asmodeus as the Library draws him: 100x100, out of "ASMO".</summary>
    public static Bitmap Asmodeus100 => Load("asmodeus.png", ref _asmodeus);

    /// <summary>The creature's cell from the sheet, or null if its PicId is off the grid.</summary>
    public static Bitmap? Of(Monster m)
    {
        int index = m.PicId - 1;
        if (index < 0 || index >= 90) return null;
        if (Cache.TryGetValue(index, out var cached)) return cached;
        try
        {
            int col = index % 10, row = index / 10;
            var target = new RenderTargetBitmap(new PixelSize(Cell, Cell));
            using (var ctx = target.CreateDrawingContext())
                ctx.DrawImage(Load("monsters.png", ref _sheet),
                              new Rect(col * Cell, row * Cell, Cell, Cell),
                              new Rect(0, 0, Cell, Cell));
            Cache[index] = target;
            return target;
        }
        catch { return null; }
    }

    /// <summary>
    /// What the Library's picMonster shows: Asmodeus's own picture for him, the sheet cell for
    /// everyone else. The size comes back with it, because the two are not the same size.
    /// </summary>
    public static (Bitmap? picture, int size) ForLibrary(Monster m) =>
        Mordor.Core.Asmodeus.Is(m) ? (Asmodeus100, 100) : (Of(m), Cell);
}
