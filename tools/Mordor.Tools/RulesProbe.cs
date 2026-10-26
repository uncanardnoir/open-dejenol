using Mordor.Core;

/// <summary>Sanity-check the reimplemented formulas against the numbers quoted on the dejenol.com pages.</summary>
public static class RulesProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var rng = new Random(3);

        Console.WriteLine("Stats A/D (page: A/D = 2*(Str+Dex) + Int + Wis - 76 when Str,Dex>=12 and Int,Wis>=14)");
        foreach (var s in new[] { new[] { 12, 14, 14, 10, 10, 12 }, new[] { 18, 18, 18, 10, 10, 18 }, new[] { 30, 20, 20, 10, 10, 30 }, new[] { 10, 10, 10, 10, 10, 10 } })
            Console.WriteLine($"  [{string.Join(",", s)}] -> {Rules.StatsAD(s)}   (page: {2 * (s[0] + s[5]) + s[1] + s[2] - 76})");

        Console.WriteLine("\nGuild A/D (page tables):  guild level -> att/def");
        foreach (var g in new[] { "Nomad", "Warrior", "Thief", "Mage" })
            Console.WriteLine($"  {g,-8} " + string.Join("  ", new[] { 1, 10, 50, 100, 200, 400 }.Select(l => { var ad = Rules.GuildAD(g, l); return $"L{l}:{ad.att}/{ad.def}"; })));

        // fn12A1 (MAINMOD2.BAS:2118-2138) at the level below is what the next level asks for
        Console.WriteLine("\nExperience required, fn12A1 (page: Human Nomad L100 = 872,326; L80 = 556,907)");
        float human = RaceData.ByName("Human").ExpFactor, troll = RaceData.ByName("Troll").ExpFactor;
        foreach (var l in new[] { 2, 10, 50, 80, 100 })
            Console.WriteLine($"  Nomad L{l,3}: {Rules.Fn12A1(l - 1, 8, human),12:N0}   Warrior: {Rules.Fn12A1(l - 1, 16, human),12:N0}   Troll Nomad: {Rules.Fn12A1(l - 1, 8, troll),12:N0}");

        Console.WriteLine("\nGold to make level (page: a L755 Thief pays almost 33 million)");
        foreach (var l in new[] { 1, 10, 100, 755 })
            Console.WriteLine($"  from L{l,3}: Thief {Rules.Fn12B2(l, GuildData.ByName("Thief").GoldFactor),15:N0}   Warrior {Rules.Fn12B2(l, GuildData.ByName("Warrior").GoldFactor),12:N0}");

        Console.WriteLine("\nGuild join cost (page: Warrior 5,009,672  Ninja 10,886,091  Thief 7,465,060)");
        foreach (var g in GuildData.All)
            Console.WriteLine($"  {g.Name,-10} {Rules.GuildJoinCost(g),15:N0}   abilities [{string.Join(",", g.Abilities.Take(6).Select(a => a.ToString("0.#")))}]");

        Console.WriteLine("\nSpell damage (page, observed max: Firebolt(10/1) SL15 = 25, SL65 = 35, SL115 = 38, SL255 = 43)");
        foreach (int sl in new[] { 15, 65, 115, 255 })
        {
            int max = 0; for (int i = 0; i < 4000; i++) max = Math.Max(max, Rules.SpellDamage(rng, 10, 1, sl, 0));
            int maxBf = 0; for (int i = 0; i < 4000; i++) maxBf = Math.Max(maxBf, Rules.SpellDamage(rng, 15, 11, sl, 0));
            Console.WriteLine($"  SL{sl,3}: Firebolt max {max,3} (page {(sl == 15 ? 25 : sl == 65 ? 35 : sl == 115 ? 38 : 43)})   Blue Flame max {maxBf,3} (page {(sl == 15 ? 53 : sl == 65 ? 117 : sl == 115 ? 172 : 313)})");
        }

        Console.WriteLine("\nExperience awarded (page: damage x monster level, guild level factor)");
        foreach (var (hits, mlvl, glvl) in new[] { (5, 2, 1), (14, 5, 1), (50, 40, 10), (200, 135, 30) })
            Console.WriteLine($"  {hits,3} damage to a L{mlvl,3} monster as a L{glvl,2} character -> {Rules.ExpAwarded(hits, mlvl, glvl, false),8:N0} xp");

        // fn0DEE holds a threshold and misses on Int(Rnd * 100) + 1 >= it, so the hit rate is one
        // lower than the threshold: 98 is the 97% the Character_Attack_Formulas page quotes. One
        // routine serves both sides, so a monster's blow uses the same threshold.
        Console.WriteLine("\nHit chances, fn0DEE (threshold, and the hit rate it gives)");
        foreach (var (a, d) in new[] { (17, 0), (17, 23), (100, 50), (50, 100) })
            Console.WriteLine($"  att {a,3} vs def {d,3}: threshold {Rules.HitThreshold(a, d),5:0.##} -> {Rules.HitThreshold(a, d) - 1,5:0.##}% hits");

        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Console.WriteLine("\nDamage per swing, starting character (Nomad L1, STR 14, bronze sword) vs a Kobold:");
        var sword = items.First(i => i.Name == "Bronze Sword");
        Console.WriteLine($"  bronze sword damage modifier from the data: {sword.DamageMod}");
        var dmgs = Enumerable.Range(0, 2000).Select(_ => Rules.CharacterDamage(rng, 1, 0.7, sword.DamageMod <= 0 ? 1 : sword.DamageMod, 14, 0, false, false, 17)).ToList();
        Console.WriteLine($"  character: min {dmgs.Min()} avg {dmgs.Average():0.0} max {dmgs.Max()}");
        var kobold = monsters.First(m => m.Name == "Kobold");
        var mdmg = Enumerable.Range(0, 2000).Select(_ => Rules.MonsterDamage(rng, kobold, 0, false, false, 17)).ToList();
        Console.WriteLine($"  kobold (glvl {kobold.GuildLevel}, str {kobold.Stats[0]}, dmgmod {kobold.DamageMod}): min {mdmg.Min()} avg {mdmg.Average():0.0} max {mdmg.Max()}  (character has ~17 hits)");
    }
}
