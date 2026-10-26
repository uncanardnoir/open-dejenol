using Mordor.Core;

/// <summary>Exercise the spell system and loot rolls without the UI.</summary>
public static class MagicProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        var rng = new Random(7);

        Console.WriteLine($"Guild spell table from MDATA1: {GuildData.ByName("Mage").HasSpellTable}");
        foreach (var guild in new[] { "Mage", "Sorcerer", "Wizard", "Healer", "Paladin", "Villain", "Seeker", "Warrior" })
        foreach (int level in new[] { 6, 20 })
        {
            var race = RaceData.ByName("Human");
            var c = Character.Create("T", race, Alignment.Neutral, Sex.Male, new[] { 14, 18, 18, 14, 12, 14 });
            c.Guilds.Insert(0, new GuildMembership { Guild = guild, Level = level });
            c.RecalcSpellPoints(refill: true);
            var known = Spellbook.Known(c, spells);
            Console.WriteLine($"{guild,-9} L{level,2}: SP {c.MaxSpellPoints,3}, knows {known.Count,2}: " +
                string.Join(", ", known.Take(7).Select(s => $"{s.Name}[{Spellbook.Cost(s, c)}]")));
        }

        // Mage L6 vs a level-2 group: cast until out of points
        {
            var c = Character.Create("M", RaceData.ByName("Elf"), Alignment.Neutral, Sex.Female, new[] { 10, 18, 16, 12, 12, 14 });
            c.Guilds.Insert(0, new GuildMembership { Guild = "Mage", Level = 6 });
            c.MaxHits = 30; c.CurHits = 30; c.RecalcSpellPoints(refill: true);
            var known = Spellbook.Known(c, spells);
            var group = EncounterGenerator.Generate(monsters, 2, rng);
            var eng = new CombatEngine(c, group, id => byId.GetValueOrDefault(id), rng);
            var best = known.Where(Spellbook.IsOffensive).OrderByDescending(s => s.Damage2).First();
            Console.WriteLine($"\nMage L6 SP {c.MaxSpellPoints} vs {group.Count}x {group[0].Def.Name} ({group[0].Def.Hits} hits) using {best.Name} ({best.Damage1}-{best.Damage2}, group={Spellbook.IsGroup(best)})");
            int rounds = 0;
            while (!eng.IsOver && rounds++ < 20)
            {
                if (c.CurSpellPoints >= Spellbook.Cost(best, c)) eng.CastSpell(best); else eng.PlayerAttacks();
                foreach (var _ in eng.MonstersAttack()) { }
            }
            foreach (var l in eng.Log.TakeLast(6)) Console.WriteLine("  " + l);
            Console.WriteLine($"  -> won={eng.Won} hp={c.CurHits} sp={c.CurSpellPoints} rounds={rounds}");
        }

        // Charm: a level-12 Mage tries Charm Humanoid on level-2 humanoids, then fights with the companion
        {
            var c = Character.Create("C", RaceData.ByName("Elf"), Alignment.Neutral, Sex.Female, new[] { 10, 18, 16, 12, 16, 14 });
            c.Guilds.Insert(0, new GuildMembership { Guild = "Mage", Level = 12 });
            c.MaxHits = 60; c.CurHits = 60; c.RecalcSpellPoints(refill: true);
            var charm = Spellbook.Known(c, spells).First(s => s.Name == "Charm Humanoid");
            var humanoid = monsters.First(m => m.Name == "Orc");
            var group = new List<CombatMonster> { new() { Def = humanoid, Hits = humanoid.Hits }, new() { Def = humanoid, Hits = humanoid.Hits } };
            var eng = new CombatEngine(c, group, id => byId.GetValueOrDefault(id), rng);
            eng.UseMonsterTable(monsters);
            Console.WriteLine($"\nMage L12 casts {charm.Name} on 2 Orcs:");
            int rounds = 0;
            while (!eng.IsOver && rounds++ < 12)
            {
                if (c.Companions.OccupiedCount == 0 && c.CurSpellPoints >= Spellbook.Cost(charm, c)) eng.CastSpell(charm); else eng.PlayerAttacks();
                if (eng.LastCastTookTurn) foreach (var _ in eng.MonstersAttack()) { }
            }
            foreach (var l in eng.Log.Take(10)) Console.WriteLine("  " + l);
            Console.WriteLine($"  -> won={eng.Won} companions={string.Join(",", c.Companions.All.Select(x => $"{x.Name} {x.Hits}/{x.MaxHits}"))} killed={eng.Killed} max={c.MaxCompanions}");
        }

    }
}
