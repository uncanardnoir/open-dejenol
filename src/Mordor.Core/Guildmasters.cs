using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// Each guild remembers its highest-ranking member, and rewards them with the guild's crest. The
/// original keeps a record per guild in MDATA2-adjacent storage (gv16CA, written to gc1C1C at
/// record guild+2); the port keeps the same two fields - who, and at what level - beside the
/// saves. FRMCTYG.BAS:434 and <c>modAwardCrest</c> (FRMCTYG.BAS:688).
/// </summary>
public sealed class Guildmasters
{
    /// <summary>mc001E: a guild's crest is the item whose id is 299 plus the guild's index.</summary>
    public const int CrestIdBase = 299;

    /// <summary>The pack is forty slots, of which slot 0 is never used.</summary>
    public const int PackSlots = 40;

    public sealed class Holder
    {
        public string Name { get; set; } = "";
        public int Level { get; set; }
    }

    /// <summary>Guild index → who holds it. A guild nobody has claimed is simply absent.</summary>
    public Dictionary<int, Holder> Held { get; set; } = new();

    /// <summary>
    /// The twelve masters the game installs the first time it builds the guildmaster file
    /// (MODULE2.BAS <c>sub0565</c>, lines 571-630), in guild order. They are why a fresh guild is
    /// not an open seat: to take the Seeker's chair you have to pass Martin at level 77.
    /// </summary>
    public static readonly (string Name, int Level)[] Seeded =
    {
        ("Crashland", 64),          // Nomad
        ("Garth SliceMonger", 56),  // Warrior
        ("Eli", 62),                // Paladin
        ("Shogun", 48),             // Ninja
        ("Maxx", 67),               // Villain
        ("Martin", 77),             // Seeker
        ("Nimblefingers", 56),      // Thief
        ("Kane the Wise Axe", 54),  // Scavenger
        ("Sparrowhawk", 52),        // Mage
        ("Requnix", 61),            // Sorcerer
        ("Merlin", 48),             // Wizard
        ("Hands on Henry", 65),     // Healer
    };

    /// <summary>
    /// Install any master the file does not have yet, logging each appointment as the original
    /// does. Returns how many were installed, which is twelve on a brand new game and nought
    /// afterwards. A guild beyond the seeded twelve is left empty rather than given a made-up name.
    /// </summary>
    public int Seed(string savesDir)
    {
        int added = 0;
        for (int g = 0; g < GuildData.All.Count && g < Seeded.Length; g++)
        {
            if (Held.ContainsKey(g) && Held[g].Name != "") continue;
            Held[g] = new Holder { Name = Seeded[g].Name, Level = Seeded[g].Level };
            GuildLog.Append(savesDir, g, GuildLog.Wrote.AppointedGuildmaster(Seeded[g].Name));
            added++;
        }
        if (added > 0) Save(savesDir);
        return added;
    }

    /// <summary>Load the file, installing the original's twelve masters if it is new.</summary>
    public static Guildmasters LoadSeeded(string savesDir)
    {
        var t = Load(savesDir);
        t.Seed(savesDir);
        return t;
    }

    public Holder For(int guild) => Held.TryGetValue(guild, out var h) ? h : new Holder();

    /// <summary>
    /// The line the guild hall shows, <c>sub294F</c> (FRMCTYG.BAS:832) - which has three forms,
    /// and only names a level when the master is somebody else.
    /// </summary>
    public string Describe(int guild, Character viewer)
    {
        var h = For(guild);
        if (h.Name == viewer.Name) return " You are the GuildMaster";
        if (h.Name == "") return " There is no GuildMaster";
        return $" {h.Name} (Level {h.Level})";
    }

    // ---- the title ----

    /// <summary>
    /// Try to take a guild's mastership. FRMCTYG.BAS:434 hands it over when the holder's recorded
    /// level is *below* yours, or when nobody holds it at all - so matching the holder is not
    /// enough, and the sitting master keeps it as they level further.
    ///
    /// The two answers are different things, and the original treats them differently. It fires
    /// the fanfare and the log line only when the name on the record changes, but it re-offers
    /// the crest every time the level test passes, sitting master included - which matters if
    /// they lost it, or had no room for it the first time.
    /// </summary>
    public static (bool qualified, bool changedHands) Claim(Guildmasters table, int guild, Character c)
    {
        int level = c.Guilds.FirstOrDefault(g => GuildData.IndexOf(g.Guild) == guild)?.Level ?? 0;
        if (level <= 0) return (false, false);

        var held = table.For(guild);
        if (held.Level >= level && held.Name != "") return (false, false);

        bool changedHands = held.Name != c.Name;
        table.Held[guild] = new Holder { Name = c.Name, Level = level };
        return (true, changedHands);
    }

    // ---- the crest ----

    /// <summary>The guild's crest, or null if MDATA3 has no such item.</summary>
    public static Item? CrestFor(IReadOnlyList<Item> items, int guild) =>
        items.FirstOrDefault(i => i.Id == CrestIdBase + guild);

    /// <summary>
    /// Hand the new master their guild's crest. The original checks the pack and the item bank
    /// first, so a second term as master awards nothing; then takes the first free pack slot,
    /// drops the crest in completely identified, and says so. A full pack loses the crest
    /// outright - it is not held over.
    ///
    /// <c>modAwardCrest</c> (FRMCTYG.BAS:688-721): a crest (item id 299 + guild) anywhere in pack
    /// slots 1-39 or in any of the forty vault slots means no award at all, and nothing is said.
    ///
    /// The vault pass also runs the "BANK ITEM SORTER: Remapping Item #n" repair
    /// (FRMCTYG.BAS:705-717), which re-points a vault record whose table index (Ref) no longer
    /// matches the item id it was stored with. The port stores the item id itself and has no
    /// separate table index to drift, so there is nothing to remap and that line has no
    /// equivalent here.
    ///
    /// Returns whether the crest was given, and whatever should be shown (null if nothing).
    /// </summary>
    public enum Award { NotApplicable, AlreadyHeld, NoRoom, Given }

    /// <summary>
    /// <c>modAwardCrest</c> writes "was awarded the crest" to the guild log (FRMCTYG.BAS:730) BEFORE it looks
    /// for a free slot - so the log records the award even when the pack was full and the crest
    /// was lost. Callers should log on <see cref="Award.NoRoom"/> as well as <see cref="Award.Given"/>.
    /// </summary>
    public static (Award award, string? message) AwardCrest(Character c, int guild, IReadOnlyList<Item> items)
    {
        var crest = CrestFor(items, guild);
        if (crest == null) return (Award.NotApplicable, null);
        // Items(Pack(l0170).Ref).Id = l0172 (FRMCTYG.BAS:698-702)
        if (c.Inventory.Any(s => s.ItemId == crest.Id)) return (Award.AlreadyHeld, null);
        // M5860(l0170).Ref > 0 and Items(Ref).Id = l0172, then "If l0176 = True Then Exit Sub"
        // (FRMCTYG.BAS:703-721)
        if (c.Bank.Take(Character.BankSlots).Any(s => s.ItemId > 0 && s.ItemId == crest.Id))
            return (Award.AlreadyHeld, null);

        if (c.FirstFreeSlot() < 0)
            return (Award.NoRoom, $"You were awarded the '{crest.Name}', but you had no room on your character for it!");

        c.PutInPack(new InventorySlot
        {
            ItemId = crest.Id,
            Known = IdLevel.All,
            Identified = true,
            Charges = (short)crest.Charges,
            // FRMCTYG.BAS:745 copies the item's curse onto the slot without equipping it, the same
            // as the chest route. No crest in MDATA3 is cursed - 0 of 12 - so this never fires; it
            // is here because the original writes it, not because it can do anything.
            SlotCursed = crest.Cursed != 0,
        });
        return (Award.Given, $"Congratulations!  For achieving the title of Guildmaster, you have been awarded the "
                    + $"'{crest.Name}'!\n\nMay the Crest help you with survival, exploration and bring you luck.");
    }

    // ---- storage ----

    private static string PathFor(string savesDir) => Path.Combine(savesDir, "guildmasters.json");

    public static Guildmasters Load(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p)) return JsonSerializer.Deserialize<Guildmasters>(File.ReadAllText(p)) ?? new Guildmasters();
        }
        catch { }
        return new Guildmasters();
    }

    public void Save(string savesDir)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            File.WriteAllText(PathFor(savesDir), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
