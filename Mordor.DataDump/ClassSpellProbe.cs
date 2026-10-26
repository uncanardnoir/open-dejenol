using Mordor.Core;

/// <summary>
/// A spell with effect 75..99 only harms one class of creature. Cast each of them at a monster of
/// the right class and at one of the wrong class, and see which lands.
/// </summary>
public static class ClassSpellProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);

        Console.WriteLine($"{"spell",-22} {"effect",6} {"wants class",-14} {"on that class",13} {"on another",11}");
        foreach (var s in spells.Where(x => SpellEffect.Of(x) is >= 75 and <= 99 && x.Damage2 > 0)
                                .OrderBy(x => x.Level))
        {
            int wantClass = SpellEffect.Of(s) - 75;
            var right = monsters.FirstOrDefault(m => m.Id != 0 && m.Hits > 0
                                                  && m.Type < Mdata1.MonsterClasses.Count
                                                  && Mdata1.MonsterClasses[m.Type].Index == wantClass);
            var wrong = monsters.FirstOrDefault(m => m.Id != 0 && m.Hits > 0
                                                  && m.Type < Mdata1.MonsterClasses.Count
                                                  && Mdata1.MonsterClasses[m.Type].Index != wantClass);
            if (right == null || wrong == null) { Console.WriteLine($"{s.Name.Trim(),-22} {SpellEffect.Of(s),6}  no pair to test with"); continue; }

            Console.WriteLine($"{s.Name.Trim(),-22} {SpellEffect.Of(s),6} {Mdata1.MonsterClassName(right.Type),-14}"
                            + $" {Hits(s, right, spells, Look),13} {Hits(s, wrong, spells, Look),11}");
        }
        Console.WriteLine("\n(hits dealt over 40 casts at spell level 30; the wrong class should take nothing at all)");
    }

    private static int Hits(Spell s, Monster target, IReadOnlyList<Spell> spells, Func<short, Item?> look)
    {
        int total = 0;
        for (int i = 0; i < 40; i++)
        {
            var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 25, 25, 25, 25, 25, 25 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = "Mage", Level = 60 });
            var foe = new CombatMonster { Def = target, Hits = 100000 };
            var fight = new CombatEngine(c, new[] { foe }, look, new Random(1000 + i));
            fight.UseSpellTable(spells);
            fight.CastSpell(s, 0, free: true, itemLevel: 30);
            total += 100000 - foe.Hits;
        }
        return total;
    }
}
