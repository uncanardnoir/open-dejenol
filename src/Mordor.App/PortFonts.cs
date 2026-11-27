using System;
using System.Linq;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// The faces the game is laid out for - Microsoft Sans Serif, Arial, Tahoma, Times New Roman,
/// Courier New - are Windows' own. Where they are not installed (Linux) the system's default face
/// stood in, and DejaVu Sans runs far wider: captions wrapped and ran into the controls under them.
///
/// The Liberation fonts are drawn to Arial's, Times New Roman's and Courier New's widths, glyph for
/// glyph (Arial's are within a hair of Microsoft Sans Serif's), and are built into the program
/// (Assets/Fonts/Liberation, SIL Open Font License, LICENSE.txt beside them and beside the program).
/// They are fonts, not anything of the game's: the self-check that the program carries none of the
/// game names them alongside the icon. Away from Windows, Liberation Sans is the face anything
/// missing falls back to (Program, FontManagerOptions), and <see cref="Chain"/> swaps the serif and
/// fixed-pitch faces for their Liberation twins. On Windows nothing changes: every face is the
/// system's own.
/// </summary>
internal static class PortFonts
{
    private const string Collection = "avares://OpenDejenol/Assets/Fonts/Liberation";
    public const string Sans = Collection + "#Liberation Sans";
    public const string Serif = Collection + "#Liberation Serif";
    public const string Mono = Collection + "#Liberation Mono";

    /// <summary>Whether the Windows faces are missing here and Liberation stands in.</summary>
    public static bool StandIn => !OperatingSystem.IsWindows();

    /// <summary>A comma-separated face list, each Windows face swapped for its twin where <see cref="StandIn"/>.</summary>
    public static string Chain(string chain)
    {
        if (!StandIn) return chain;
        return string.Join(", ", chain.Split(',').Select(n => Twin(n.Trim())).Distinct());
    }

    public static FontFamily Family(string chain) => new(Chain(chain));

    /// <summary>
    /// The font manager's options, or null for Avalonia's own. Away from Windows, a face the game names that
    /// is not installed is drawn in Liberation Sans (<see cref="Sans"/>), drawn to Arial's widths, not the
    /// system's far wider default. On Windows, a geometric shape the face lacks (the spinners' and the
    /// combo boxes' ▲ and ▼ in MS Sans Serif) is drawn in Segoe UI, as Windows' own fallback gave it
    /// with Avalonia 11: Avalonia 12 tries the faces it has already loaded before asking the system, so
    /// the shape came out in whichever face had been used first - Arial in one window, Cascadia Mono in
    /// the next - and smaller than it was.
    /// </summary>
    public static FontManagerOptions? ManagerOptions() =>
        StandIn ? new FontManagerOptions { DefaultFamilyName = Sans }
        : OperatingSystem.IsWindows() ? new FontManagerOptions
        {
            FontFallbacks = new[] { new FontFallback { FontFamily = new FontFamily("Segoe UI"), UnicodeRange = new UnicodeRange(0x25A0, 0x25FF) } },
        }
        : null;

    private static string Twin(string face) => face.ToLowerInvariant() switch
    {
        "microsoft sans serif" or "ms sans serif" or "arial" or "tahoma" or "segoe ui" or "helvetica" or "sans-serif" => Sans,
        "times new roman" or "times" or "serif" => Serif,
        "courier new" or "courier" => Mono,
        _ => face,
    };
}
