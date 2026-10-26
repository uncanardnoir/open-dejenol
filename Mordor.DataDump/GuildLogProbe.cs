using Mordor.Core;

/// <summary>
/// Checks the guild log (FRMGLDL.BAS): how entries are dated, that each hall sees only its own,
/// when the Clean button appears, and what a clean keeps, discards and writes out.
/// </summary>
public static class GuildLogProbe
{
    public static void Run(string dir, string? readOnly = null)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);

        // with a directory given, just print what that log holds rather than exercising the rules
        if (readOnly != null)
        {
            long day = GuildLog.Today();
            var all = GuildLog.All(readOnly);
            Console.WriteLine($"{readOnly}: {all.Count} entries");
            foreach (var e in all)
                Console.WriteLine($"  [{GuildData.All[Math.Clamp(e.Guild, 0, GuildData.All.Count - 1)].Name}] {GuildLog.Line(e, day)}");
            return;
        }

        string saves = Path.Combine(Path.GetTempPath(), "mordor-guildlog-probe");
        if (Directory.Exists(saves)) Directory.Delete(saves, recursive: true);
        Directory.CreateDirectory(saves);

        int guilds = GuildData.All.Count;
        Console.WriteLine($"{guilds} guilds, so Clean appears above {GuildLog.KeepOnClean * guilds} entries in all.");

        // ---- dating ----
        long today = GuildLog.Today();
        Console.WriteLine("\ndating:");
        foreach (int ago in new[] { 0, 1, 2, 17 })
            Console.WriteLine($"  {ago,2} days back -> {GuildLog.Prefix(new GuildLog.Entry(0, today - ago, ""), today)}");

        // ---- the wording of every entry the game writes ----
        Console.WriteLine("\nthe lines the game writes:");
        foreach (string line in new[]
        {
            GuildLog.Wrote.JoinedGuild("Brannoc "),
            GuildLog.Wrote.Titled("Brannoc", "Veteran", 4),
            GuildLog.Wrote.QuestForMonster("Brannoc", "Giant Rat"),
            GuildLog.Wrote.QuestForItem("Brannoc", "Steel Dagger"),
            GuildLog.Wrote.BecameGuildmaster("Brannoc"),
            GuildLog.Wrote.AppointedGuildmaster("Brannoc"),
            GuildLog.Wrote.GuildmasterAward("Brannoc", "Guild Crest"),
            GuildLog.Wrote.Demoted("Brannoc", 2, Sex.Male),
            GuildLog.Wrote.Demoted("Sela", 2, Sex.Female),
            GuildLog.Wrote.SetRecord("Brannoc", "Most Kills", "1,204"),
            GuildLog.Wrote.FirstToLevel("Brannoc", 7),
            GuildLog.Wrote.PartyBeatAsmodeus("Brannoc"),
            GuildLog.Wrote.AloneBeatAsmodeus("Brannoc"),
            GuildLog.Wrote.Resurrected("Brannoc", "Sela "),
            GuildLog.Wrote.ResurrectedInMorgue("Brannoc"),
            GuildLog.Wrote.DiedOnLevel("Brannoc", 3),
            GuildLog.Wrote.DiedSomewhere("Brannoc"),
        })
            Console.WriteLine("  " + line);

        // ---- a log spread over three guilds ----
        for (int i = 0; i < 180; i++)
            GuildLog.Append(saves, i % 3, $"entry {i}");

        Console.WriteLine($"\n180 entries written across guilds 0-2; total now {GuildLog.All(saves).Count}.");
        for (int g = 0; g < 3; g++)
        {
            var mine = GuildLog.For(saves, g);
            Console.WriteLine($"  {GuildData.All[g].Name,-10} sees {mine.Count,3} entries, newest first: " +
                              string.Join(", ", mine.Take(3).Select(e => e.Text)));
        }
        Console.WriteLine($"  a guild with nothing in the log ({GuildData.All[3].Name}) sees {GuildLog.For(saves, 3).Count}.");

        // ---- the Clean button's threshold ----
        Console.WriteLine($"\nClean offered at 180 entries? {GuildLog.NeedsCleaning(saves, guilds)} " +
                          $"(needs more than {GuildLog.KeepOnClean * guilds})");
        Console.WriteLine($"Clean offered if there were only {guilds} guilds' worth... " +
                          $"pretending 3 guilds: {GuildLog.NeedsCleaning(saves, 3)}");

        // ---- cleaning ----
        int removed = GuildLog.Clean(saves, "Tester", i => GuildData.All[i].Name, writeText: true);
        var left = GuildLog.All(saves);
        Console.WriteLine($"\ncleaned: {removed} removed, {left.Count} kept " +
                          $"(oldest kept is \"{left[0].Text}\", newest \"{left[^1].Text}\")");
        Console.WriteLine($"  backup written: {File.Exists(Path.Combine(saves, GuildLog.BackupName))}");

        string txt = Path.Combine(saves, GuildLog.TextName);
        Console.WriteLine($"  {GuildLog.TextName}:");
        foreach (string line in File.ReadLines(txt).Take(9)) Console.WriteLine("    " + line);
        Console.WriteLine($"    ... {File.ReadLines(txt).Count()} lines in all");

        Directory.Delete(saves, recursive: true);
    }
}
