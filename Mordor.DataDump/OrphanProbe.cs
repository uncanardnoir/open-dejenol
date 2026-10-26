using Mordor.Core;

/// <summary>
/// Two spells no guild can reach - Fate and Life Domination, both level 50. Are they cast some
/// other way, or are they records nothing uses?
/// </summary>
public static class OrphanProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); GuildData.LoadFrom(dir); RaceData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));

        foreach (string name in new[] { "Fate", "Life Domination" })
        {
            int rec = -1;
            for (int i = 0; i < spells.Count; i++) if (spells[i].Name.Trim() == name) { rec = i; break; }
            if (rec < 0) { Console.WriteLine($"{name}: not in MDATA2"); continue; }
            var s = spells[rec];
            Console.WriteLine($"\n{name}: record {rec}, id {s.Id}, level {s.Level}, school "
                            + $"{Librarian.SpellSchools[s.Category]}, effect {s.Effect}, ratio {s.U4}");

            var casters = items.Where(i => i.Id != 0 && i.SpellIndex - 1 == rec).ToList();
            Console.WriteLine($"  items that cast it: {(casters.Count == 0 ? "none" : string.Join(", ", casters.Select(i => i.Name.Trim())))}");

            int ceiling = GuildData.All.Where(g => g.HasSpellTable)
                                       .Select(g => g.SpellMax[s.Category]).DefaultIfEmpty(0).Max();
            Console.WriteLine($"  the highest any guild reaches in that school: {ceiling} (the spell is level {s.Level})");

            int monsters = mons.Count(m => m.Id != 0 && (m.SpellFlags & (1 << s.Category)) != 0);
            Console.WriteLine($"  monsters that draw from that school: {monsters}");
        }

        Console.WriteLine("\nare those item-only spells actually obtainable?");
        foreach (string nm in new[] { "Elixr of Fate", "Magical Dust", "Dalyn's Groan", "Scroll of She'tal", "Orb of Life Domination", "Lamurian Shock Lance" })
        {
            var it = items.FirstOrDefault(i => i.Name.Trim() == nm);
            if (it == null) { Console.WriteLine($"  {nm,-24} not in MDATA3"); continue; }
            Console.WriteLine($"  {nm,-24} floor {it.Floor,2}  price {it.Price,12:N0}  cursed {it.Cursed}  charges {it.Charges,3}  rarity {it.Rarity,3}");
        }

        Console.WriteLine("\nheld, do they offer the spell?");
        foreach (string nm in new[] { "Elixr of Fate", "Magical Dust", "Dalyn's Groan",
                                      "Scroll of She'tal", "Orb of Life Domination", "Lamurian Shock Lance" })
        {
            var it = items.FirstOrDefault(i => i.Name.Trim() == nm);
            if (it == null) continue;
            var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 25, 25, 25, 25, 25, 25 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = "Mage", Level = 60 });
            c.PutInPack(new InventorySlot
            {
                ItemId = it.Id, Charges = (short)Math.Max(1, it.Charges),
                Known = IdLevel.All, Equipped = ItemUse.MustBeEquippedToUse(it),
            });
            var offer = Spellbook.Castables(c, spells, id => items.FirstOrDefault(i => i.Id == id))
                                 .FirstOrDefault(x => x.FromItem && x.Item!.Id == it.Id);
            Console.WriteLine($"  {nm,-24} offers: {(offer == null ? "NOTHING" : offer.Spell.Name.Trim())}");
        }

        // and the other way round: which spell records does nothing at all reach?
        Console.WriteLine("\nevery spell no guild ceiling admits:");
        foreach (var s in spells.Where(x => x.Name.Trim().Length > 0))
        {
            int ceiling = GuildData.All.Where(g => g.HasSpellTable)
                                       .Select(g => g.SpellMax[s.Category]).DefaultIfEmpty(0).Max();
            if (s.Level <= ceiling) continue;
            int rec = spells.IndexOf(s);
            var casters = items.Where(i => i.Id != 0 && i.SpellIndex - 1 == rec).Select(i => i.Name.Trim()).ToList();
            Console.WriteLine($"  {s.Name.Trim(),-20} level {s.Level,2} in {Librarian.SpellSchools[s.Category],-10} (ceiling {ceiling,2})"
                            + $"  items: {(casters.Count == 0 ? "none" : string.Join(", ", casters))}");
        }
    }
}
