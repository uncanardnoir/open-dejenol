using System.Text.Json;

namespace Mordor.Core.Import;

/// <summary>
/// Writes what <see cref="OriginalSaves"/> read into the port's saves folder. The safe way round:
/// <list type="bullet">
/// <item>everything the folder holds is copied first into <c>BACKUP\before-import-&lt;time&gt;</c>
/// inside it - a folder the Options window's Restore can bring back;</item>
/// <item>a character is written only if asked for by name, and the caller asks only after the
/// player has said so for each character whose name the port already has (<see cref="Plan"/>
/// says which); the rest are left alone;</item>
/// <item>the shared world - automap, rooms, store, Library, Hall of Records, guildmasters, pens,
/// guild log - is written only when asked for, and then replaces the port's, which the backup
/// keeps.</item>
/// </list>
/// The original's DATA folder is only ever read.
/// </summary>
public static class SaveImport
{
    /// <summary>One character the import can write, and whether the port already has one of that name.</summary>
    public sealed record CharacterChoice(ImportedCharacter Imported, bool Exists, string File);

    /// <summary>One port file the world import would write, and whether the saves folder has it already.</summary>
    public sealed record WorldChoice(string File, string Holds, bool Exists);

    public sealed record ImportPlan(List<CharacterChoice> Characters, List<WorldChoice> World);

    public sealed record ImportResult(string? BackupDir, List<string> Written, List<string> Skipped);

    /// <summary>What an import into <paramref name="savesDir"/> would do: which characters are new and which the port has.</summary>
    public static ImportPlan Plan(OriginalSaves src, string savesDir)
    {
        var existing = ExistingNames(savesDir);
        var chars = new List<CharacterChoice>();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ic in src.Characters)
        {
            if (ic.Character == null) continue;
            string file = Character.SafeFileName(ic.Name) + ".json";
            if (!files.Add(file)) continue;   // a second record with the same file name: the first wins (noted when read)
            bool exists = File.Exists(Path.Combine(savesDir, file)) || existing.Contains(ic.Name.TrimEnd());
            chars.Add(new CharacterChoice(ic, exists, file));
        }
        var world = new List<WorldChoice>();
        void W(bool have, string file, string holds)
        {
            if (have) world.Add(new WorldChoice(file, holds, File.Exists(Path.Combine(savesDir, file))));
        }
        W(src.Automap != null, "automap.json", "the automap (MDATA8)");
        W(src.Dungeon != null || src.DeepestReached != null, "dungeon.json", "the rooms, respawn tallies and deepest level (MDATA10, MDATA8)");
        W(src.Store != null, "store.json", "the General Store's shelves (MDATA6)");
        W(src.Library != null, "library.json", "the Library (MDATA13)");
        W(src.Hall != null, "records.json", "the Hall of Records (MDATA14)");
        W(src.Guildmasters != null, "guildmasters.json", "the guildmasters (MDATA7)");
        W(src.Pens != null, "confinement.json", "the Confinement's pens (MDATA15)");
        W(src.GuildLog != null, GuildLog.FileName, "the guild log (MDATA9)");
        return new ImportPlan(chars, world);
    }

    /// <summary>The names of the characters the saves folder has now, compared as the menu compares them.</summary>
    private static HashSet<string> ExistingNames(string savesDir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!Directory.Exists(savesDir)) return names;
            foreach (string f in Directory.GetFiles(savesDir, "*.json"))
            {
                if (!Backups.IsCharacterFile(f)) continue;
                try { names.Add(Character.ReadRaw(f).Name.TrimEnd()); } catch { }
            }
        }
        catch { }
        return names;
    }

    /// <summary>
    /// Copy every file at the top of the saves folder into a new <c>BACKUP\before-import-yyyyMMdd-HHmmss</c>
    /// inside it. Null when there was nothing to keep. Throws when a copy fails, so nothing is written
    /// over a folder that could not be kept.
    /// </summary>
    public static string? Backup(string savesDir, DateTime now)
    {
        if (!Directory.Exists(savesDir)) return null;
        var files = Directory.GetFiles(savesDir);
        if (files.Length == 0) return null;
        string first = Path.Combine(savesDir, "BACKUP", "before-import-" + now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        string dir = first;
        for (int n = 2; Directory.Exists(dir); n++) dir = first + "-" + n;
        Directory.CreateDirectory(dir);
        foreach (string f in files) File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), overwrite: false);
        return dir;
    }

    /// <summary>
    /// Write the chosen characters (by name, as <see cref="ImportedCharacter.Name"/>) and, with
    /// <paramref name="world"/>, the shared files, after <see cref="Backup"/>. Characters go into
    /// the roster in the original's record order; a party is written for a leader written now.
    /// </summary>
    public static ImportResult Apply(OriginalSaves src, string savesDir, IEnumerable<string> characters, bool world, DateTime now)
    {
        var chosen = new HashSet<string>(characters, StringComparer.Ordinal);
        var plan = Plan(src, savesDir);
        string? backup = Backup(savesDir, now);
        Directory.CreateDirectory(savesDir);
        var written = new List<string>();
        var skipped = new List<string>();

        var existing = Roster.Saved(savesDir).Select(p => p.Ch.Name).ToList();
        var writtenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in plan.Characters)
        {
            string name = choice.Imported.Name;
            if (!chosen.Contains(name)) { skipped.Add(name); continue; }
            choice.Imported.Character!.Save(Path.Combine(savesDir, choice.File));
            Roster.Added(savesDir, name, existing);
            if (!existing.Contains(name)) existing.Add(name);
            writtenNames.Add(name);
            written.Add(choice.File);
        }
        foreach (var ic in src.Characters.Where(c => c.Character == null)) skipped.Add(ic.Name);

        // a remembered party comes with its leader: party-<leader>.json, as Party.SaveParty writes it
        foreach (var p in src.Parties)
        {
            if (!writtenNames.Contains(p.Leader)) continue;
            string file = "party-" + Character.SafeFileName(p.Leader) + ".json";
            File.WriteAllText(Path.Combine(savesDir, file),
                JsonSerializer.Serialize(new { Leader = p.Leader, Members = p.Members }, new JsonSerializerOptions { WriteIndented = true }));
            written.Add(file);
        }

        if (world)
        {
            if (src.Automap != null) { src.Automap.Save(savesDir); written.Add("automap.json"); }
            if (src.Dungeon != null || src.DeepestReached != null)
            {
                var d = src.Dungeon ?? DungeonState.Load(savesDir);
                if (src.DeepestReached is int deepest) d.DeepestReached = deepest;
                d.Save(savesDir);
                written.Add("dungeon.json");
            }
            if (src.Store != null) { src.Store.Save(savesDir); written.Add("store.json"); }
            if (src.Library != null) { src.Library.Save(savesDir); written.Add("library.json"); }
            if (src.Hall != null) { src.Hall.Save(savesDir); written.Add("records.json"); }
            if (src.Guildmasters != null) { src.Guildmasters.Save(savesDir); written.Add("guildmasters.json"); }
            if (src.Pens != null) { src.Pens.Save(savesDir); written.Add("confinement.json"); }
            if (src.GuildLog != null) { GuildLog.Rewrite(savesDir, src.GuildLog); written.Add(GuildLog.FileName); }
        }
        return new ImportResult(backup, written, skipped);
    }

    /// <summary>A few lines on what the DATA folder holds, for the preview and the probes.</summary>
    public static List<string> Summary(OriginalSaves src)
    {
        var lines = new List<string> { $"Characters ({src.Characters.Count(c => c.Character != null)} of {src.CharacterCount} in MDATA4):" };
        foreach (var ic in src.Characters)
        {
            if (ic.Character is not Character c) { lines.Add($"  {ic.Name}: not importable - {ic.Refusal}"); continue; }
            string state = c.IsDead ? " - " + DeathStates.Name(c.CauseOfDeath) : "";
            string where = c.DungeonLevel > 0 ? $"level {c.DungeonLevel} at {c.X + 1},{c.Y + 1}" : "in town";
            lines.Add($"  {c.Name}: {c.Race} {c.Guild.Guild} {c.Guild.Level}, {c.CurHits}/{c.MaxHits} hits, "
                      + $"{CharStatFile.FormatNumber(c.GoldOnHand)} gold, {where}{state}");
        }
        if (src.Parties.Count > 0)
            lines.Add("Parties: " + string.Join("; ", src.Parties.Select(p => string.Join(", ", p.Members))));
        lines.Add("Files:");
        foreach (var f in src.Files) lines.Add($"  {f.Name} - {f.Holds}: {f.Status}");
        return lines;
    }
}
