using Mordor.Core.Extraction;

/// <summary>
/// <c>extract [outdir]</c>: the pictures and the help, read out of the game folder exactly as the
/// app does at start (<see cref="GameArt"/>), into <paramref name="outDir"/> - by default
/// <c>../extracted-art/cache</c>, beside the game folder and outside the port, since what is
/// written is the game's.
/// </summary>
public static class GameArtDump
{
    public static void Run(string gameDir, string? outDir)
    {
        outDir ??= Path.Combine(gameDir, "..", "extracted-art", "cache");
        Directory.CreateDirectory(outDir);
        var notes = GameArt.Extract(gameDir, outDir);
        Console.WriteLine($"extracted {GameArt.Pictures.Count} pictures and the help into {Path.GetFullPath(outDir)}");
        foreach (var p in GameArt.Pictures) Console.WriteLine($"  {p.Name,-24} {p.Width,4}x{p.Height,-4} {p.From}");
        foreach (var n in notes) Console.WriteLine("  note: " + n);
        Console.WriteLine("cache key for this folder: " + GameArt.Key(gameDir));
    }
}
