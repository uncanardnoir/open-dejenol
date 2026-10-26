using Mordor.Core;

/// <summary>
/// Checks the Multiple Swings rule (FRMCTYG.BAS:394 for what you earn, MODULE4.BAS:333 for how
/// it is spent): at what guild level each guild grants another swing, and what a round becomes.
/// </summary>
public static class SwingProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));

        Console.WriteLine("each guild's Multiple Swings modifier, and the levels it pays out at:");
        foreach (var g in GuildData.All)
        {
            double mod = g.Abilities.ElementAtOrDefault(3);
            if (mod <= 0) { Console.WriteLine($"  {g.Name,-10} mod {mod,4:0.0}  never"); continue; }

            var at = new List<int>();
            int had = 0;
            for (int lvl = 1; lvl <= 999; lvl++)
            {
                var c = Make(g.Name, lvl);
                if (c.Swings > had) { had = c.Swings; at.Add(lvl); }
                if (at.Count >= 6) break;
            }
            Console.WriteLine($"  {g.Name,-10} mod {mod,4:0.0}  +1 swing at level " + string.Join(", ", at));
        }

        var wpns = items.Where(i => i.Id != 0 && i.Type <= 7 && i.Att > 0).ToList();
        Console.WriteLine($"\nhow many swings the {wpns.Count} weapons offer (this is what the port was ignoring):");
        foreach (var g in wpns.GroupBy(i => (int)i.Swings).OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key} swing(s): {g.Count(),3} weapons, floors " +
                              $"{g.Min(i => i.Floor)}..{g.Max(i => i.Floor)}   e.g. " +
                              string.Join(", ", g.OrderBy(i => i.Floor).Take(3).Select(i => i.Name)));

        Console.WriteLine("\nswings a round, by guild and level (character + weapon):");
        var sword = items.First(i => i.Id != 0 && i.Type <= 7 && i.Att > 0 && i.Swings > 0);
        Console.WriteLine($"  holding a {sword.Name}, which offers {sword.Swings} swing(s)");
        Console.Write($"  {"guild",-10}");
        foreach (int lvl in new[] { 1, 10, 25, 50, 100, 200 }) Console.Write($"{("L" + lvl),7}");
        Console.WriteLine();
        foreach (string name in new[] { "Nomad", "Warrior", "Ninja", "Villain", "Thief", "Wizard" })
        {
            Console.Write($"  {name,-10}");
            foreach (int lvl in new[] { 1, 10, 25, 50, 100, 200 })
                Console.Write($"{Make(name, lvl).Swings + sword.Swings,7}");
            Console.WriteLine();
        }

        var monsters0 = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        Console.WriteLine("\nthe two ways a weapon can simply not work (MODULE4.BAS:347-363):");
        foreach (var (bit, what) in new[]
        {
            (MonsterProperty.NeedsGoodWeapon, "needs a weapon from at least half its own floor"),
            (MonsterProperty.ImmuneToWeapons, "immune to weapons outright"),
        })
        {
            var hit = monsters0.Where(m => MonsterProperty.Has(m, bit)).ToList();
            Console.WriteLine($"  bit {bit}, {what}: {hit.Count} monsters");
            Console.WriteLine("    " + string.Join(", ", hit.OrderBy(m => m.LevelFound)
                .Take(10).Select(m => $"{m.Name} (floor {m.LevelFound})")));
        }
        var fussy = monsters0.Where(m => MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon))
                             .OrderByDescending(m => m.LevelFound).FirstOrDefault();
        if (fussy != null)
            Console.WriteLine($"  the fussiest is {fussy.Name} on floor {fussy.LevelFound}: " +
                              $"nothing below floor {(int)(fussy.LevelFound / 2)} touches it");

        // ---- the rules in action ----
        var byId0 = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Look(short id) => byId0.TryGetValue(id, out var x) ? x : null;
        var fussyMon = monsters0.First(m => MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon) && m.LevelFound >= 8);
        var immuneMon = monsters0.First(m => MonsterProperty.Has(m, MonsterProperty.ImmuneToWeapons));
        var plainMon = monsters0.First(m => !MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon)
                                         && !MonsterProperty.Has(m, MonsterProperty.ImmuneToWeapons));
        var poor = wpns.OrderBy(i => i.Floor).First(i => i.Swings > 0);
        var good = wpns.OrderByDescending(i => i.Floor).First(i => i.Swings > 0);

        Console.WriteLine($"\nswinging at things, with a {poor.Name} (floor {poor.Floor}) " +
                          $"and a {good.Name} (floor {good.Floor}):");
        foreach (var mon in new[] { plainMon, fussyMon, immuneMon })
            foreach (var w in new[] { poor, good })
            {
                var c = Make("Warrior", 20);
                c.PutInPack(new InventorySlot { ItemId = w.Id, Equipped = true, Identified = true });
                var fight = new CombatEngine(new Party(c), new[] { new CombatMonster { Def = mon, Hits = 99999 } }, Look, new Random(3));
                var lines = fight.PlayerSwings(c).Select(l => l.Text).ToList();
                Console.WriteLine($"  {mon.Name,-18} (floor {mon.LevelFound,2}) with the {w.Name,-18} -> " +
                                  $"{lines.Count} line(s): {lines.FirstOrDefault() ?? "(nothing)"}");
            }

        Console.WriteLine("\ncritical and backstab chances (independent - both can land, which is a 'sever'):");
        Console.WriteLine($"  {"guild",-10} {"lvl",4} {"crit",6} {"backstab",9} {"both",6}  with a critting/backstabbing weapon");
        foreach (string name in new[] { "Ninja", "Thief", "Villain", "Warrior", "Wizard" })
            foreach (int lvl in new[] { 1, 25, 100, 400 })
            {
                var g = GuildData.ByName(name);
                double cm = g.Abilities.ElementAtOrDefault(2), bm = g.Abilities.ElementAtOrDefault(1);
                double c0 = Rules.CritChance(lvl, cm, 100, 10), b0 = Rules.BackstabChance(lvl, bm, 100, 10);
                double c1 = Rules.CritChance(lvl, cm, 100, 10, true), b1 = Rules.BackstabChance(lvl, bm, 100, 10, true);
                Console.WriteLine($"  {name,-10} {lvl,4} {c0,5:0.0}% {b0,8:0.0}% {c0 * b0 / 100.0,5:0.0}%  ->  {c1,5:0.0}% / {b1,5:0.0}%");
            }

        Console.WriteLine("\nwhat that does to a round, against a monster that cannot die:");
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        // the toughest monster a plain weapon can still hurt - Asmodeus himself carries the "needs a good weapon" bit
        var dummy = monsters.Where(m => !MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon) && !MonsterProperty.Has(m, MonsterProperty.ImmuneToWeapons))
                            .OrderByDescending(m => m.Hits).First();
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        Console.WriteLine($"  {"guild",-10} {"lvl",4} {"swings",7} {"damage/round",14}");
        foreach (string name in new[] { "Warrior", "Ninja", "Wizard" })
            foreach (int lvl in new[] { 1, 25, 100 })
            {
                var c = Make(name, lvl);
                c.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
                long total = 0;
                const int rounds = 300;
                for (int r = 0; r < rounds; r++)
                {
                    var mon = new CombatMonster { Def = dummy, Hits = int.MaxValue };
                    var fight = new CombatEngine(new Party(c), new[] { mon }, Lookup, new Random(r));
                    fight.PlayerAttacks(c);
                    total += int.MaxValue - (long)mon.Hits;
                }
                Console.WriteLine($"  {name,-10} {lvl,4} {c.Swings + sword.Swings,7} {total / (double)rounds,14:0.0}");
            }
    }

    static Character Make(string guild, int level)
    {
        var c = Character.Create("Swinger", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                 new[] { 18, 18, 18, 18, 18, 18 });
        c.Guilds.Clear();
        c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
        c.ClearPack();
        c.RecalcSwings();
        return c;
    }
}
