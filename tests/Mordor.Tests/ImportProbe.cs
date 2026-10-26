using Mordor.Core;
using Mordor.Core.Import;

/// <summary>
/// What an original DATA folder holds, read through the importer (Mordor.Core.Import) and printed:
/// the characters field by field as the port will have them, every file, every note. Reads only.
///
///   Mordor.Tools.exe &lt;gamedir&gt; import [datadir [charstat-folder]]
///
/// The DATA folder is the argument, else MORDOR_ORIGINAL_DATA; with neither it says so and stops.
/// </summary>
public static class ImportProbe
{
    public static string? DataDir(string? given)
    {
        string? d = given ?? Environment.GetEnvironmentVariable("MORDOR_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(d)) return null;
        return OriginalSaves.FindDataDir(d);
    }

    public static void Run(string gameDir, string? given, string? charstatDir = null)
    {
        string? data = DataDir(given);
        if (data == null)
        {
            Console.WriteLine("skipped: no original DATA folder (pass one, or set MORDOR_ORIGINAL_DATA to an original game's DATA folder)");
            return;
        }
        Mdata1.LoadFrom(gameDir); RaceData.LoadFrom(gameDir); GuildData.LoadFrom(gameDir);
        var items = MdrLoader.LoadItems(Path.Combine(gameDir, "MDATA3.MDR"));
        var monsters = MdrLoader.LoadMonsters(Path.Combine(gameDir, "MDATA5.MDR"));
        var src = OriginalSaves.Read(data, items, monsters);
        Console.WriteLine($"original DATA folder: {data}");
        foreach (string line in SaveImport.Summary(src)) Console.WriteLine(line);

        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var ic in src.Characters)
        {
            if (ic.Character is not Character c) continue;
            Console.WriteLine($"\n== {c.Name} (record {ic.Record})");
            Console.WriteLine($"  {c.Sex} {c.Race}, {c.Alignment}, age {c.AgeYears} ({c.Age} days)");
            Console.WriteLine($"  stats {string.Join(" ", c.Stats)}  hits {c.CurHits}/{c.MaxHits}  spell points {c.CurSpellPoints}/{c.MaxSpellPoints}");
            Console.WriteLine($"  gold {c.GoldOnHand} on hand, {c.GoldInBank} in the bank");
            foreach (var g in c.Guilds)
                Console.WriteLine($"  guild {g.Guild} level {g.Level}, {g.Experience} experience, quest {g.QuestType}/{g.QuestTarget}{(g.QuestDone ? " done" : "")}");
            Console.WriteLine($"  at level {c.DungeonLevel}, {c.X + 1},{c.Y + 1} facing {c.Facing}, area {c.Area}; lost {c.LostLevel}/{c.LostPosition}/{c.LostFacing}; sanctuary {c.SanctuaryX},{c.SanctuaryY},{c.SanctuaryLevel}");
            Console.WriteLine($"  dead {c.IsDead} ({DeathStates.Name(c.CauseOfDeath)}), poison {c.PoisonLevel}, disease {c.DiseaseLevel}, paralysis {c.ParalysisLevel}, ADShift {c.ADShift}, swings {c.Swings}");
            Console.WriteLine($"  kills {c.Kills}, deaths {c.Deaths}, played {c.PlaySeconds / 3600:0.00} h, created {DateTime.FromOADate(c.CreatedOn):yyyy-MM-dd}");
            for (int i = 1; i < Character.PackSize; i++)
                if (c.Pack[i] is InventorySlot s && s.ItemId > 0)
                    Console.WriteLine($"  pack {i + 1,2}: {(byId.TryGetValue(s.ItemId, out var it) ? it.Name : "?")} (Id {s.ItemId}) {s.Alignment} {s.Known}{(s.Equipped ? " equipped" : "")}{(s.SlotCursed ? " cursed" : "")}, {s.Charges} charges");
            for (int i = 0; i < c.Bank.Count; i++)
                if (c.Bank[i].ItemId > 0)
                    Console.WriteLine($"  bank {i + 1,2}: {(byId.TryGetValue(c.Bank[i].ItemId, out var it) ? it.Name : "?")} (Id {c.Bank[i].ItemId}) {c.Bank[i].Known}");
            foreach (var (slot, comp) in c.Companions.Occupied)
                Console.WriteLine($"  companion {slot + 1}: {(comp.Ref >= 0 && comp.Ref < monsters.Count ? monsters[comp.Ref].Name : "?")} \"{comp.Name.Trim()}\" {comp.Hits}/{comp.MaxHits}, bound {comp.BindingLevel}, ID {comp.IdLevel}, {comp.Alignment}");
            Console.WriteLine($"  buffers {string.Join(" ", c.Buffers)}; combat setting {c.CombatSetting}; options {c.Options.AutoChangeLevel}/{c.Options.Hide}/{c.Options.PreloadCharacter}/{c.Options.NoPerceptionMessages}");
        }

        if (src.Store != null) Console.WriteLine($"\nstore: {src.Store.Shelf.Count} shelves stocked, {src.Store.Shelf.Values.Sum()} items");
        if (src.Pens != null) Console.WriteLine($"pens: {src.Pens.Pens.Count} pens, {src.Pens.Pens.Values.Sum()} creatures");
        if (src.Guildmasters != null) Console.WriteLine("guildmasters: " + string.Join(", ", src.Guildmasters.Held.OrderBy(k => k.Key).Select(k => $"{GuildData.All[k.Key].Name} {k.Value.Name} ({k.Value.Level})")));
        if (src.Automap != null) Console.WriteLine($"automap: squares stood on per level {string.Join(" ", src.Automap.Explored.OrderBy(k => k.Key).Select(k => $"L{k.Key}:{k.Value.Count}"))}, {src.Automap.Seen.Count} squares recorded; deepest reached {src.DeepestReached}");
        if (src.Dungeon != null) Console.WriteLine($"rooms: {src.Dungeon.Rooms.Count} with records; tallies {string.Join(" ", src.Dungeon.Tally.OrderBy(k => k.Key).Select(k => k.Value))}");
        if (src.Library != null) Console.WriteLine($"library: {src.Library.Entries.Count(e => e.Key.StartsWith("mr:"))} creature records, {src.Library.Entries.Count(e => e.Key.StartsWith("i:"))} item records");
        if (src.Hall != null) Console.WriteLine("hall: " + string.Join("; ", src.Hall.Records.OrderBy(k => k.Key).Select(k => $"{HallRecords.Heading((HallRecords.Kind)k.Key)} {k.Value.Value} {k.Value.Holder}")));
        if (src.GuildLog != null) Console.WriteLine($"guild log: {src.GuildLog.Count} entries");

        // the Char page as Shift-F6 writes it (CharStatFile), one file per character, to set beside
        // the CHARSTAT.TXT the original writes for the same character (docs/IMPORT-GROUND-TRUTH.md)
        if (charstatDir != null)
        {
            Directory.CreateDirectory(charstatDir);
            foreach (var ic in src.Characters.Where(c => c.Character != null))
            {
                string to = Path.Combine(charstatDir, "CHARSTAT-" + Character.SafeFileName(ic.Name) + ".TXT");
                File.WriteAllText(to, CharStatFile.Compose(CharStatFile.Paint(ic.Character!, id => byId.TryGetValue(id, out var it) ? it : null, toFile: true, DateTime.Now)));
                Console.WriteLine($"wrote {to}");
            }
        }

        Console.WriteLine($"\nnotes ({src.Notes.Count}):");
        foreach (var n in src.Notes) Console.WriteLine("  " + n);
    }
}
