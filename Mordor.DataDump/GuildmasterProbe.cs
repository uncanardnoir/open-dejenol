using Mordor.Core;

/// <summary>
/// Checks the guildmaster mechanic (FRMCTYG.BAS): who holds each guild, when the title changes
/// hands, the crest each guild awards, and the demotion for a quest left undone.
/// </summary>
public static class GuildmasterProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));

        Console.WriteLine($"the crest each guild awards its master (item id {Guildmasters.CrestIdBase} + guild index):");
        for (int g = 0; g < GuildData.All.Count; g++)
        {
            var crest = Guildmasters.CrestFor(items, g);
            Console.WriteLine($"  {g,2} {GuildData.All[g].Name,-10} -> " +
                              (crest == null ? "(none in MDATA3)"
                                             : $"{crest.Name} (id {crest.Id}, type {Loot.TypeName(crest)}, " +
                                               $"att {crest.Att} def {crest.Def}, abilities 0x{crest.Abilities:X})"));
        }

        string saves = Path.Combine(Path.GetTempPath(), "mordor-gm-probe");
        if (Directory.Exists(saves)) Directory.Delete(saves, recursive: true);
        Directory.CreateDirectory(saves);

        Character Make(string name, string guild, int level)
        {
            var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = level });
            c.ClearPack();
            return c;
        }

        Console.WriteLine("\nthe masters the game installs on a new file, and the log it writes:");
        var seeded = Guildmasters.LoadSeeded(saves);
        for (int g = 0; g < GuildData.All.Count; g++)
        {
            var h = seeded.For(g);
            Console.WriteLine($"  {GuildData.All[g].Name,-10} {h.Name,-20} level {h.Level}");
        }
        Console.WriteLine($"  ...and {GuildLog.All(saves).Count} log entries written; " +
                          $"seeding again adds {seeded.Seed(saves)}.");
        Console.WriteLine("  the Nomad log reads: " + string.Join(" | ", GuildLog.For(saves, 0).Select(e => e.Text)));

        Console.WriteLine("\nwho holds the Nomad guild, as characters come and go:");
        var table = new Guildmasters();
        void Claim(string who, int level)
        {
            var c = Make(who, "Nomad", level);
            var (qualified, changed) = Guildmasters.Claim(table, 0, c);
            var held = table.For(0);
            Console.WriteLine($"  {who,-8} reaches level {level,2}: " +
                              (changed ? "takes the title" : qualified ? "keeps it, crest re-offered" : "no change") +
                              $"   held by {(held.Name == "" ? "nobody" : held.Name + " at " + held.Level)}");
        }
        Claim("Brannoc", 3);      // nobody holds it: the first to level takes it
        Claim("Sela", 2);         // lower than the holder
        Claim("Sela", 3);         // equal is not enough - the original wants strictly greater
        Claim("Sela", 4);         // greater takes it
        Claim("Sela", 6);         // already the holder: keeps it, but no fanfare

        Console.WriteLine("\nthe crest lands in the pack:");
        var master = Make("Kord", "Nomad", 9);
        var (awarded, why) = Guildmasters.AwardCrest(master, 0, items);
        Console.WriteLine($"  first time:  {why}");
        (awarded, why) = Guildmasters.AwardCrest(master, 0, items);
        Console.WriteLine($"  second time: {why ?? "(nothing said - they already have it)"}");
        Console.WriteLine($"  pack now: {string.Join(", ", master.Inventory.Select(s => (items.FirstOrDefault(i => i.Id == s.ItemId)?.Name ?? "?") + " (" + s.Known + ")"))}");

        var full = Make("Packed", "Nomad", 9);
        var filler = items.First(i => i.Id != 0 && ChestTraps.Destructible(i));
        for (int i = 0; i < Guildmasters.PackSlots - 1; i++) full.PutInPack(new InventorySlot { ItemId = filler.Id });
        (awarded, why) = Guildmasters.AwardCrest(full, 0, items);
        Console.WriteLine($"  with a full pack: {why}");

        Console.WriteLine("\nforfeiting a quest costs Int(Log(level + 1)) levels:");
        Console.Write("  ");
        foreach (int lvl in new[] { 1, 2, 3, 7, 12, 20, 30, 60, 100 })
        {
            var c = Make("Slacker", "Nomad", lvl);
            Console.Write($"{lvl} -> -{c.ForfeitCost()}   ");
        }
        Console.WriteLine();

        Console.WriteLine("\nwhen the forfeit is offered at all - only once you are pinned:");
        var pinned = Make("Stuck", "Nomad", 5);
        Console.WriteLine($"  Nomad 5 needs {pinned.ExpForNextLevel():N0} for the next level, " +
                          $"and can hold {pinned.ExperienceCap():N0} in all");
        foreach (long exp in new[] { 0L, pinned.ExpForNextLevel(), pinned.ExperienceCap() })
        {
            pinned.Guilds[0].Experience = exp;
            Console.WriteLine($"    with {exp,12:N0}: can level {pinned.CanLevelUp(),-5} pinned {pinned.IsPinned}");
        }

        Console.WriteLine("\nwhat a forfeit takes off you:");
        var demoted = Make("Fallen", "Nomad", 20);
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;
        // deliberately mix gear it can only just use with gear it will still qualify for at 17
        var nomad = GuildData.ByName("Nomad");
        var usable = items.Where(i => i.Id != 0 && demoted.EquipProblem(i) == null).ToList();
        foreach (var it in usable.Where(i => Character.UseLevel(i, nomad) is > 17 and <= 20).Take(3)
                    .Concat(usable.Where(i => Character.UseLevel(i, nomad) <= 3).Take(3)))
            demoted.PutInPack(new InventorySlot { ItemId = it.Id, Equipped = true, Known = IdLevel.All });
        Console.WriteLine("  Nomad 20 wearing: " + string.Join(", ", demoted.Inventory.Select(s =>
            $"{Lookup(s.ItemId)?.Name} (needs {Character.UseLevel(Lookup(s.ItemId)!, nomad)})")));
        demoted.Guilds[0].QuestType = 2; demoted.Guilds[0].QuestTarget = items[1].Id;
        var off = demoted.ForfeitQuest(Lookup, new Random(1));
        Console.WriteLine($"  forfeits: now level {demoted.Guilds[0].Level}, quest cleared ({demoted.Guilds[0].QuestType == 0}), " +
                          $"{off.Count} item(s) came off" +
                          (off.Count == 0 ? "" : ": " + string.Join(", ", off.Select(s => Lookup(s.ItemId)?.Name))));

        Directory.Delete(saves, recursive: true);
    }
}
