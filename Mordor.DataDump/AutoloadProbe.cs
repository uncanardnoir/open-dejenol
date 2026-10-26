using Mordor.Core;

/// <summary>
/// Drives <c>Party.LoadFor</c> against a saves directory and prints what the autoload says, so the
/// three refusals in <c>mdrmulAutoLoad</c> (MULTIMOD.BAS:240-270) can be seen without going through
/// the title screen - the only path in the game that reaches them.
///
/// Usage: <c>Mordor.DataDump &lt;gamedir&gt; autoload &lt;savesdir&gt; &lt;leader&gt;</c>
/// </summary>
public static class AutoloadProbe
{
    public static void Run(string savesDir, string leaderName)
    {
        string f = System.IO.Path.Combine(savesDir, Character.SafeFileName(leaderName) + ".json");
        if (!System.IO.File.Exists(f)) { Console.WriteLine($"no such character: {f}"); return; }
        var leader = Character.Load(f);
        Console.WriteLine($"leader {leader.Name} at {leader.X},{leader.Y},{leader.DungeonLevel}\n");

        var party = Party.LoadFor(leader, savesDir, Console.WriteLine);
        Console.WriteLine($"\nparty: {string.Join(", ", party.Members.Select(m => m.Name))}");
    }
}
