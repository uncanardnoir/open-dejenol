using Mordor.Core;

/// <summary>
/// Exercises <c>sub0E85</c> as ported - the monsters' whole turn. It runs every monster in
/// MDATA5 against a durable character many times over, so that every special attack, every
/// caption and every sound path is actually reached, and reports what came out.
/// </summary>
public static class MonsterAttackProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Look(short id) => byId.TryGetValue(id, out var x) ? x : null;

        var rng = new Random(20260920);
        var captions = new Dictionary<string, int>();
        var sounds = new Dictionary<string, int>();
        int turns = 0, lines = 0;
        long damage = 0;
        int deaths = 0, stoned = 0, poisoned = 0, diseased = 0, paralysed = 0, drained = 0, aged = 0;
        long goldLost = 0; int itemsLost = 0;

        // a character who can stand there and take it, so a turn is not cut short by dying
        Character Victim()
        {
            var c = Character.Create("Anvil", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 18, 18, 18, 18, 18, 18 });
            c.Guilds.Insert(0, new GuildMembership { Guild = "Warrior", Level = 40 });
            c.MaxHits = 200000; c.CurHits = 200000;
            c.GoldOnHand = 5_000_000;
            foreach (var it in items.Where(i => i.Id > 0 && i.Price > 0).Take(12))
                c.PutInPack(new InventorySlot { ItemId = it.Id, Known = IdLevel.All });
            return c;
        }

        // the interesting monsters are the ones that carry a special; take them all, plus a
        // plain sample, and give each several turns
        var withSpecials = monsters.Where(m => m.Id > 0 && m.Hits > 0 && m.SpecialAttackFlags != 0).ToList();
        var plain = monsters.Where(m => m.Id > 0 && m.Hits > 0 && m.SpecialAttackFlags == 0).Take(40).ToList();

        foreach (var def in withSpecials.Concat(plain))
        {
            for (int round = 0; round < 8; round++)
            {
                var c = Victim();
                VbCurrency goldBefore = c.GoldOnHand;
                int packBefore = c.Inventory.Count, hitsBefore = c.CurHits;
                var group = Enumerable.Range(0, Math.Clamp((int)def.InGroup, 1, 4))
                                      .Select(_ => new CombatMonster { Def = def, Hits = def.Hits })
                                      .ToList();
                var fight = new CombatEngine(new Party(c), group, Look, rng);
                fight.UseMonsterTable(monsters);
                fight.UseSpellTable(spells);

                turns++;
                foreach (var line in fight.MonsterAttacks())
                {
                    lines++;
                    if (line.Text != "") captions[Shape(line.Text)] = captions.GetValueOrDefault(Shape(line.Text)) + 1;
                    foreach (string s in line.HitSounds ?? Array.Empty<string>())
                        sounds[s] = sounds.GetValueOrDefault(s) + 1;
                }
                damage += hitsBefore - c.CurHits;
                if (c.IsDead) deaths++;
                if (c.Stoned) stoned++;
                if (c.Poisoned) poisoned++;
                if (c.Diseased) diseased++;
                if (fight.ParalyzedRounds(c) > 0) paralysed++;
                if (c.Stats.Zip(new[] { 18, 18, 18, 18, 18, 18 }).Any(t => t.First < t.Second) || c.MaxHits < 200000) drained++;
                if (c.Age > 16 * 365) aged++;
                goldLost += (goldBefore - c.GoldOnHand).RoundToWhole();
                itemsLost += packBefore - c.Inventory.Count;
            }
        }

        Console.WriteLine($"{withSpecials.Count} monsters carry a special attack, {plain.Count} plain ones sampled");
        Console.WriteLine($"{turns:N0} group turns, {lines:N0} lines, {damage:N0} damage dealt in all\n");

        Console.WriteLine("what happened to the character, per 1,000 turns:");
        void Rate(string what, int n) => Console.WriteLine($"  {what,-22} {n * 1000.0 / turns,7:0.0}");
        Rate("killed", deaths); Rate("turned to stone", stoned);
        Rate("poisoned", poisoned); Rate("diseased", diseased);
        Rate("paralysed", paralysed); Rate("drained", drained); Rate("aged", aged);
        Console.WriteLine($"  {"gold stolen",-22} {goldLost / (double)turns,7:N0} a turn");
        Console.WriteLine($"  {"items lost",-22} {itemsLost * 1000.0 / turns,7:0.0}");

        Console.WriteLine("\nevery caption the routine can produce (shape, then how often):");
        foreach (var kv in captions.OrderByDescending(k => k.Value))
            Console.WriteLine($"  {kv.Value,6:N0}  {kv.Key}");

        Console.WriteLine("\nsounds played:");
        foreach (var kv in sounds.OrderByDescending(k => k.Value))
            Console.WriteLine($"  {kv.Value,6:N0}  {kv.Key}");
    }

    /// <summary>Replace the numbers and the monster's name so the distinct captions collapse together.</summary>
    static string Shape(string s)
    {
        var sb = new System.Text.StringBuilder();
        bool inNum = false;
        foreach (char ch in s)
        {
            if (char.IsDigit(ch) || (inNum && ch == ',')) { if (!inNum) sb.Append('N'); inNum = true; continue; }
            inNum = false;
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
