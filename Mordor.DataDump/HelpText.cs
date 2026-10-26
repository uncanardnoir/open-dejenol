using Mordor.Core.Extraction;

/// <summary>
/// The game's help, for the probes that hold the port to what it says: read out of the game
/// folder's MORDOR.HLP, as the app reads it (<see cref="HelpBundle"/>). The port keeps no copy.
/// </summary>
public static class HelpText
{
    private static readonly Dictionary<string, List<HelpBundle.Topic>> Read = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The help's topics, or none when the game folder has no MORDOR.HLP.</summary>
    public static List<HelpBundle.Topic> Topics(string gameDir)
    {
        string full = Path.GetFullPath(gameDir);
        if (Read.TryGetValue(full, out var t)) return t;
        string? hlp = GameArt.Find(full, "MORDOR.HLP");
        t = hlp == null ? new List<HelpBundle.Topic>() : HelpBundle.FromHelpFile(WinHelpFile.Read(hlp));
        Read[full] = t;
        return t;
    }

    /// <summary>A topic's paragraphs, one a line, with no blank lines between them.</summary>
    public static string[] Lines(HelpBundle.Topic topic) =>
        topic.Text.Split('\n').Where(l => l.Trim().Length > 0).ToArray();
}
