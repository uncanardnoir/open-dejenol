using System.Reflection;
using Avalonia.Media;
using Mordor.App;

/// <summary>
/// The inks declared on their grounds (Palette.Pairs), read by arithmetic alone, no window and no
/// picture: in every theme the palette has - every theme any role names, and every theme the player
/// can choose (ThemeSelection's choices but "same as the system") - each pair's ink on its ground,
/// the see-through grounds mixed over the solid one under them, at least 3:1 (WCAG's figure for
/// large text and the parts of a user interface). A role a theme leaves undefined fails, so a theme
/// added later is held to every pair the day it is added. The pairs let stand under 3:1 are in
/// <see cref="Known"/>, each with its reason: they print as known and do not fail.
/// </summary>
static class ThemePairsCheck
{
    const double Least = 3.0;

    /// <summary>A pair let stand: the ink, the ground as the pair names it (its roles, topmost first, joined by " over "), the theme, and why.</summary>
    record KnownPair(string Ink, string Ground, string Theme, string Reason);

    static readonly KnownPair[] Known =
    {
        new(Palette.StateKey, "Mordor.Ground.Window", "Light",
            "the original's own ForeColor (&H000000FF& on &H00C0C0C0&, frmCharStat1), kept for fidelity"),
        // Dark's experience bar is drawn as it always was, by the owner's wish: its pink figure past the
        // level on the half-strength purple over the half-strength green is 2.7:1
        new("Mordor.Bar.InkPast", "Mordor.Bar.ToPin over Mordor.Bar.ToNext over Mordor.Ground.List", "Dark",
            "Dark's experience bar kept as Dark has always drawn it (the owner's call, 2026-11-27): the pink figure past the level on the purple"),
        new("Mordor.Bar.InkPast", "Mordor.Bar.Pinned over Mordor.Bar.ToNext over Mordor.Ground.List", "Dark",
            "Dark's experience bar kept as Dark has always drawn it (the owner's call, 2026-11-27): Pinned in pink on the purple"),
    };

    public static int Run()
    {
        Console.WriteLine($"\nthe declared inks on their grounds (Palette.Pairs), every theme, each at least {Least}:1:");
        int failed = 0;
        var choices = typeof(App).Assembly.GetType("Mordor.App.ThemeSelection")!.GetNestedType("Choice", BindingFlags.Public | BindingFlags.NonPublic)!;
        var themes = Palette.Themes.Union(Enum.GetNames(choices).Where(n => n != "System")).ToList();
        Console.WriteLine($"  themes: {string.Join(", ", themes)}; {Palette.Pairs.Count} pairs");
        bool enough = themes.Contains("Light") && themes.Contains("Dark") && Palette.Pairs.Count > 0;
        Console.WriteLine($"  {(enough ? "ok  " : "FAIL")}  Light and Dark both there, and pairs declared");
        if (!enough) failed++;

        // every role defined in every theme
        foreach (string theme in themes)
        {
            var missing = Palette.Keys.Where(k => Palette.ColourOf(k, theme) == null).ToList();
            bool ok = missing.Count == 0;
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {theme}: every role has a colour" + (ok ? "" : $": none for {string.Join(", ", missing)}"));
            if (!ok) failed++;
        }

        foreach (string theme in themes)
            foreach (var pair in Palette.Pairs)
            {
                string ground = string.Join(" over ", pair.Ground);
                string name = $"{theme}: {pair.Ink} on {pair.What}";
                var ink = Palette.ColourOf(pair.Ink, theme);
                var layers = pair.Ground.Select(k => Palette.ColourOf(k, theme)).ToList();
                if (ink == null || layers.Any(l => l == null))
                {
                    Console.WriteLine($"  FAIL  {name}: a role undefined in {theme}");
                    failed++;
                    continue;
                }
                if (layers[^1]!.Value.A != 255)
                {
                    Console.WriteLine($"  FAIL  {name}: the ground has nothing solid at the bottom ({ground})");
                    failed++;
                    continue;
                }
                var under = layers[^1]!.Value;
                for (int i = layers.Count - 2; i >= 0; i--) under = Over(layers[i]!.Value, under);
                var fg = Over(ink.Value, under);
                double ratio = Contrast(fg, under);
                string numbers = $"{Hex(fg)} on {Hex(under)}, {ratio:0.00}:1";
                var known = Known.FirstOrDefault(k => k.Ink == pair.Ink && k.Ground == ground && k.Theme == theme);
                if (ratio >= Least) Console.WriteLine($"  ok    {name}: {numbers}");
                else if (known != null) Console.WriteLine($"  known {name}: {numbers} - {known.Reason}");
                else
                {
                    Console.WriteLine($"  FAIL  {name}: {numbers} ({ground})");
                    failed++;
                }
            }

        // a known entry no pair needs any more goes, so the list does not hide a later regression
        foreach (var k in Known)
        {
            var pair = Palette.Pairs.FirstOrDefault(p => p.Ink == k.Ink && string.Join(" over ", p.Ground) == k.Ground);
            bool used = pair != null && themes.Contains(k.Theme);
            if (used)
            {
                var layers = pair!.Ground.Select(g => Palette.ColourOf(g, k.Theme)!.Value).ToList();
                var under = layers[^1];
                for (int i = layers.Count - 2; i >= 0; i--) under = Over(layers[i], under);
                used = Contrast(Over(Palette.ColourOf(k.Ink, k.Theme)!.Value, under), under) < Least;
            }
            if (!used)
            {
                Console.WriteLine($"  FAIL  the known pair {k.Ink} on {k.Ground} in {k.Theme} is no longer under {Least}:1 or no longer declared: take it off the list");
                failed++;
            }
        }
        return failed;
    }

    static Color Over(Color top, Color under)
    {
        double a = top.A / 255.0;
        byte Mix(byte t, byte u) => (byte)Math.Round(t * a + u * (1 - a));
        return Color.FromRgb(Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

    /// <summary>WCAG 2's contrast ratio of two colours, 1 to 21.</summary>
    static double Contrast(Color a, Color b)
    {
        static double Channel(byte c) { double s = c / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
