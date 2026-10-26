using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Mordor.Core.Extraction;

namespace Mordor.App;

/// <summary>
/// The game's pictures and help, read from the player's own Mordor at start
/// (<see cref="GameArt"/>): the port ships none of them. <see cref="Prepare"/> is called with the
/// game folder before any window that shows them is made; it extracts into the per-user cache the
/// first time, and after that only finds it. A picture asked for before then, or one that is not
/// in the cache, is an error that names the file - nothing falls back to a copy inside the program.
/// </summary>
public static class Art
{
    private static readonly Dictionary<string, Bitmap> Loaded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folder the art was extracted to, once <see cref="Prepare"/> has succeeded.</summary>
    public static string? Dir { get; private set; }

    /// <summary>What the extraction had to say: pictures it could not read and left blank, and the like.</summary>
    public static IReadOnlyList<string> Notes { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Get the art for <paramref name="gameDir"/> ready: null when it is, else what went wrong, in
    /// words for the first-run window.
    /// </summary>
    public static string? Prepare(string gameDir)
    {
        var result = GameArt.Prepare(gameDir);
        if (!result.Ok)
        {
            CrashLog.Note("game art: " + result.Error);
            return result.Error;
        }
        if (!string.Equals(Dir, result.Dir, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var b in Loaded.Values) b.Dispose();
            Loaded.Clear();
        }
        Dir = result.Dir;
        Notes = result.Notes;
        foreach (var n in result.Notes) CrashLog.Note("game art: " + n);
        return null;
    }

    /// <summary>The path of an extracted file (a picture, or <see cref="GameArt.HelpFile"/>).</summary>
    public static string PathOf(string name)
    {
        if (Dir == null) throw new InvalidOperationException($"the game's art has not been read yet (wanted {name})");
        return Path.Combine(Dir, name);
    }

    /// <summary>A picture, loaded once and shared; do not dispose it.</summary>
    public static Bitmap Bitmap(string name)
    {
        if (Loaded.TryGetValue(name, out var b)) return b;
        string path = PathOf(name);
        if (!File.Exists(path)) throw new FileNotFoundException($"{name} is not among the game's extracted pictures in {Dir}", path);
        b = new Bitmap(path);
        Loaded[name] = b;
        return b;
    }
}

/// <summary>
/// <c>{app:Art town_store.png}</c> in a form: the extracted picture of that name, as
/// <see cref="Art.Bitmap"/> gives it.
/// </summary>
public sealed class ArtExtension : MarkupExtension
{
    public ArtExtension() { }
    public ArtExtension(string name) { Name = name; }

    public string Name { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Art.Bitmap(Name);
}
