using Mordor.Core;

/// <summary>
/// Finds an item that one guild may use and another may not, so the guild-join gear check
/// (<see cref="GearCheck"/>, from <c>mdrcharCheckItems</c>) can be watched happening: equip that
/// item as the first guild, join the second, and it should come off with
/// "GUILD: One or more items were unequipped when you joined..."
/// </summary>
public static class GearCheckProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));

        // fn11B6 refuses an item whose guild mask is empty to everyone, so count them
        Console.WriteLine($"  items with no guild bit set (usable by nobody): "
                        + items.Count(i => i.Id != 0 && i.Guilds == 0));

        // a character stands in for the guild: one joined guild, so "any joined guild" is that one
        Character AsGuild(string g)
        {
            var c = new Character { Name = "probe" };
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = g, Level = 1 });
            return c;
        }

        var guilds = GuildData.All.Select(g => g.Name).ToList();
        int shown = 0;
        foreach (var it in items.Where(i => i.Id != 0 && i.Guilds != 0 && i.Floor <= 2))
        {
            var allowed = guilds.Where(g => GuildData.CanUse(it, AsGuild(g))).ToList();
            var refused = guilds.Where(g => !GuildData.CanUse(it, AsGuild(g))).ToList();
            if (allowed.Count == 0 || refused.Count == 0) continue;
            Console.WriteLine($"  #{it.Id,-4} {it.Name,-24} floor {it.Floor}"
                            + $"\n        usable by: {string.Join(", ", allowed)}"
                            + $"\n        refused  : {string.Join(", ", refused)}");
            if (++shown >= 6) break;
        }
        if (shown == 0) Console.WriteLine("  nothing on the first two floors is guild-restricted");

        // and the rule itself, end to end: a Nomad wearing a Bronze Sword joins the Healers,
        // who will not have it
        var sword = items.FirstOrDefault(i => i.Name.Trim() == "Bronze Sword");
        if (sword == null) { Console.WriteLine("  no Bronze Sword in the data"); return; }

        var c = AsGuild("Nomad");
        c.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
        int slot = Enumerable.Range(1, Character.PackSize - 1).First(i => c.Pack[i]?.ItemId == sword.Id);
        Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);

        Console.WriteLine($"  as a Nomad, the Bronze Sword in slot {slot + 1} is equipped: {c.Pack[slot]!.Equipped}");
        Console.WriteLine($"  GearCheck.Any says anything came off: {GearCheck.Any(c, Look)}  (nothing should)");

        c.Guilds.Insert(0, new GuildMembership { Guild = "Healer", Level = 1 });
        bool shed = GearCheck.Any(c, Look);
        // the Nomad membership stays joined, so an item not tied to one guild stays usable
        bool tied = sword.ClassRestricted != 0;
        Console.WriteLine($"  after joining the Healers: anything came off: {shed}");
        Console.WriteLine($"  the sword is now equipped: {c.Pack[slot]!.Equipped}  (it should be {(!tied).ToString().ToLowerInvariant()}: "
                        + (tied ? "tied to one guild, the current one decides" : "any joined guild will do, and Nomad still is") + ")");
        if (shed) Console.WriteLine($"  and the Guild Hall would say: {GearCheck.UnequippedOnJoining}");
    }
}
