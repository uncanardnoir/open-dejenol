using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;

namespace Mordor.App;

/// <summary>
/// The face the port draws MS Sans Serif in, the forms' own font and the default "NonPro" one.
/// MS Sans Serif is a bitmap font Avalonia cannot draw; Windows' TrueType version of it, Microsoft
/// Sans Serif, has no bold, and Avalonia's made-up bold of it thickens each letter both ways without
/// widening its advance, so bold captions came out smeared and their letters ran together ("Mon
/// Quests:", "Guild A/D:", the Store's usable rows). Windows made the original's bold by drawing the
/// bitmap letters twice a pixel apart and widening each by that pixel.
///
/// So this family takes its regular letters from Microsoft Sans Serif and its bold ones from Tahoma
/// Bold, a real bold drawn for the same small sizes - Tahoma is Windows' own stand-in for MS Sans
/// Serif in dialogs ("MS Shell Dlg 2"), and its bold is about as wide as the original's widened bold.
/// Italics lean either face as Avalonia leans a face with no italic. Where Tahoma Bold is not
/// installed the bold is Microsoft Sans Serif's made-up one, as before, and where Microsoft Sans Serif
/// is not, whatever the system gives for it.
///
/// It is its own font collection ("fonts:MordorSans#MS Sans Serif") because a FontFamily names
/// faces, not weights: nothing else would send a bold to another face. It is named alone, not at the
/// head of a fallback list - Avalonia draws a list's first face that the system has, and would pass
/// over this one. See docs/FIDELITY.md, "Bold MS Sans Serif is drawn in Tahoma Bold".
/// </summary>
internal sealed class SansStandIn : IFontCollection
{
    /// <summary>The family's name within the collection.</summary>
    public const string Name = "MS Sans Serif";

    public static readonly Uri CollectionKey = new("fonts:MordorSans");

    /// <summary>The family to name in a FontFamily.</summary>
    public const string Family = "fonts:MordorSans#" + Name;

    /// <summary>Whether a FontFamily is this one.</summary>
    public static bool IsFamily(FontFamily family) => family.Key?.Source == CollectionKey
        || family.ToString().StartsWith(Family, StringComparison.OrdinalIgnoreCase);

    private static bool _added;

    /// <summary>Add the collection to the font manager, once. False when it could not be.</summary>
    public static bool EnsureAdded()
    {
        if (_added) return _ok;
        _added = true;
        try { FontManager.Current.AddFontCollection(new SansStandIn()); _ok = true; }
        catch { _ok = false; }
        return _ok;
    }

    private static bool _ok;

    private readonly Dictionary<(bool Bold, bool Italic), IGlyphTypeface?> _faces = new();
    private readonly FontFamily[] _families = { new(Name) };

    public Uri Key => CollectionKey;

    public void Initialize(IFontManagerImpl fontManager) { }

    private IGlyphTypeface? Face(bool bold, bool italic)
    {
        if (_faces.TryGetValue((bold, italic), out var f)) return f;
        var style = italic ? FontStyle.Italic : FontStyle.Normal;
        var weight = bold ? FontWeight.Bold : FontWeight.Normal;
        f = null;
        try
        {
            // the installed faces, as the system collection gives them: Tahoma's own bold, not one
            // made up again from its regular
            if (bold && FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily("Tahoma"), style, weight), out var tahoma)
                && tahoma.FamilyName.Equals("Tahoma", StringComparison.OrdinalIgnoreCase)
                && (tahoma.FontSimulations & FontSimulations.Bold) == 0)
                f = tahoma;
            else if (FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily("Microsoft Sans Serif, Segoe UI"), style, weight), out var sans))
                f = sans;
        }
        catch { f = null; }
        _faces[(bold, italic)] = f;
        return f;
    }

    public bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, out IGlyphTypeface glyphTypeface)
    {
        glyphTypeface = null!;
        if (!string.Equals(familyName, Name, StringComparison.OrdinalIgnoreCase)) return false;
        var f = Face(weight >= FontWeight.SemiBold, style != FontStyle.Normal);
        if (f == null) return false;
        glyphTypeface = f;
        return true;
    }

    public bool TryMatchCharacter(int codepoint, FontStyle style, FontWeight weight, FontStretch stretch,
                                  string? familyName, CultureInfo? culture, out Typeface typeface)
    {
        typeface = default;
        return false;
    }

    public int Count => _families.Length;
    public FontFamily this[int index] => _families[index];
    public IEnumerator<FontFamily> GetEnumerator() => ((IEnumerable<FontFamily>)_families).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _families.GetEnumerator();

    /// <summary>The faces belong to the system collection, which disposes of them.</summary>
    public void Dispose() => _faces.Clear();
}
