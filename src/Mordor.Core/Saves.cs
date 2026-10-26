namespace Mordor.Core;

/// <summary>
/// Where saved characters, the shared automap, the dungeon state and the store's shelves live.
///
/// In order (docs/GAME_FILES.md, "Saves"):
/// <list type="number">
/// <item><c>MORDOR_SAVES</c>: the automated smoke tests create throwaway characters, and would
///   otherwise leave them in the player's saves, so this redirects the lot somewhere disposable.</item>
/// <item>The port's first home for them, <c>saves\</c> beside the game folder
///   (<c>&lt;game folder&gt;\..\saves</c>, as Desktop\mordor\saves sits beside
///   Desktop\mordor\game_files), when that folder is there and has anything in it: an existing
///   setup goes on exactly as before. Nothing is ever moved or copied out of it.</item>
/// <item>Otherwise a folder of the player's own, <c>%APPDATA%\OpenDejenol\saves</c>
///   (<see cref="PerUserDir"/>), beside the port's launch settings: a Mordor installed under
///   Program Files has no writable folder beside it.</item>
/// </list>
/// The per-user folder is not made here - only named; whatever writes there first makes it, as
/// every save already does for a saves folder that is not there yet.
/// </summary>
public static class Saves
{
    public const string EnvDir = "MORDOR_SAVES";

    /// <summary>
    /// The port's own: which mods are running, as "id@version", for <see cref="Character.Save"/> to
    /// record (docs/MODDING.md, Safeguards). The mod host sets it; with no mods it answers null and
    /// a save carries no tag at all.
    /// </summary>
    public static Func<List<string>?>? ModTag { get; set; }

    /// <summary>
    /// <c>%APPDATA%\OpenDejenol\saves</c>: the player's own, for an install with no writable folder
    /// beside it - in the old <c>%APPDATA%\Mordor Port</c> until that is renamed (<see cref="UserFolders"/>).
    /// </summary>
    public static string PerUserDir =>
        Path.Combine(UserFolders.Roaming, "saves");

    /// <summary><c>&lt;game folder&gt;\..\saves</c>, where the port has always kept them.</summary>
    public static string LegacyDirFor(string gameDir) => Path.Combine(gameDir, "..", "saves");

    public static string DirFor(string gameDir) =>
        Resolve(gameDir, Environment.GetEnvironmentVariable(EnvDir), PerUserDir);

    /// <summary>
    /// <see cref="DirFor"/> with its inputs given: the override (MORDOR_SAVES), else the legacy
    /// folder beside the game folder if it holds anything, else <paramref name="perUser"/>.
    /// </summary>
    public static string Resolve(string gameDir, string? overridden, string perUser)
    {
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            try { Directory.CreateDirectory(overridden); } catch { }
            return overridden;
        }
        string legacy = LegacyDirFor(gameDir);
        if (HasAnything(legacy)) return legacy;
        return perUser;
    }

    /// <summary>The folder is there and has a file or folder in it.</summary>
    private static bool HasAnything(string dir)
    {
        try { return Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any(); }
        catch { return false; }
    }
}
