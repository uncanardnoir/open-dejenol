using Mordor.Core;

/// <summary>Headless combat simulator: fights many random encounters per level and reports balance.</summary>
public static class Sim
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
        var rng = new Random(12345);

        Console.WriteLine("level | charLvl | fights | wins | deaths | fled | avgExp | avgGold | avgRounds");
        // the levels the data has: the shareware's dungeon stops at 3 (docs/GAME_FILES.md)
        foreach (var (level, charLevel) in new[] { (1, 1), (1, 3), (2, 3), (2, 6), (3, 6), (4, 9), (6, 14) }.Where(p => p.Item1 <= dungeon.Count))
        {
            int wins = 0, deaths = 0, fled = 0, rounds = 0, ailments = 0, thefts = 0; long exp = 0, gold = 0;
            const int N = 300;
            for (int n = 0; n < N; n++)
            {
                var ch = Character.Create("Sim", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male, new[] { 14, 10, 10, 14, 10, 12 });
                // level up for real so hits, stats and A/D match the game's progression
                for (int l = 1; l < charLevel; l++) ch.LevelUp(rng);
                // level-appropriate gear: best-attack weapon and best-defense armor found on floors <= dungeon level
                var weapon = items.Where(i => i.Id != 0 && i.Att > 0 && i.Floor <= level && i.Def == 0).OrderByDescending(i => i.Att).FirstOrDefault();
                var armor = items.Where(i => i.Id != 0 && i.Def > 0 && i.Floor <= level).OrderByDescending(i => i.Def).FirstOrDefault();
                if (weapon != null) ch.PutInPack(new InventorySlot { ItemId = weapon.Id, Equipped = true });
                if (armor != null) ch.PutInPack(new InventorySlot { ItemId = armor.Id, Equipped = true });

                var group = EncounterGenerator.Generate(monsters, level, rng, dungeon[level - 1], 15, 15);
                var c = new CombatEngine(ch, group, id => byId.TryGetValue(id, out var it) ? it : null, rng);
                c.UseMonsterTable(monsters);
                int r = 0;
                while (!c.IsOver && r < 60)
                {
                    r++;
                    // play like a person: run when you are under 40% hits, as the game's own tutorial advises.
                    // Paralysis costs you the round; it wears off on the ailment clock, not here.
                    if (c.ParalyzedRounds(ch) > 0) { }
                    else if (ch.CurHits * 5 < ch.MaxHits * 2) c.WalkAway();
                    else c.PlayerAttacks();
                    if (!c.IsOver) foreach (var _ in c.MonstersAttack()) { }
                }
                rounds += r;
                if (ch.Poisoned || ch.Diseased || ch.Stoned) ailments++;
                if (c.GoldStolen > 0 || c.ItemsStolen.Count > 0) thefts++;
                if (c.Won) { wins++; exp += c.ExpEarned; gold += c.GoldEarned; }
                else if (c.PartyWiped) deaths++;
                else if (c.Fled) fled++;
            }
            Console.WriteLine($"{level,5} | {charLevel,7} | {N,6} | {wins,4} | {deaths,6} | {fled,4} | {(wins > 0 ? exp / wins : 0),6} | {(wins > 0 ? gold / wins : 0),7} | {rounds / (double)N,9:F1} | ailments {ailments,3} thefts {thefts,3}");
        }

        Console.WriteLine("\nSample encounters:");
        for (int level = 1; level <= Math.Min(6, dungeon.Count); level++)
            for (int k = 0; k < 3; k++)
            {
                var g = EncounterGenerator.Generate(monsters, level, rng, dungeon[level - 1], 15, 15);
                Console.WriteLine($"  L{level}: {EncounterGenerator.Describe(g)}  (lead att {g[0].Def.Att}, def {g[0].Def.Def}, hits {g[0].Def.Hits})");
            }
    }
}
