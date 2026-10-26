using Mordor.Core;

/// <summary>
/// What Fate actually does, measured, down both routes that reach it.
///
/// <c>sub0F29</c> (MODULE6.BAS:1906) is three lines, one of which is dead:
///
///     l031E = Int(Rnd * 3)                                  ' never read again
///     M4EAC = Int(Rnd * M4EB4) + 1                          ' current hits  = 1..max
///     M5830 = Int(Rnd * fn138E(pv031C)) + 1                 ' current spell points = 1..max
///
/// Two callers: chest trap 6 (MODULE4.BAS:3553, in a Select Case over trapNumber - 1) and spell
/// effect 19 (MODULE6.BAS:911). The name suggests a coin flip; the arithmetic says something
/// blunter, and the `+ 1` inside the `Int` gives it a floor the caller cannot see.
/// </summary>
public static class FateProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);
        var rng = new Random(20260922);

        var (spellHits, spellSp) = Measure(10000, rng, (c, r) =>
        {
            var s = spells.First(x => x.Effect == SpellEffect.Fate);
            SpellEffect.ApplyToCharacter(SpellEffect.Fate, c, s, r, Look);
        });
        var (trapHits, trapSp) = Measure(10000, rng, (c, r) =>
            ChestTraps.Spring(6, new List<Character> { c }, c, 1, r, Look, (x, y) => 0));

        foreach (var (route, hits, sp) in new[]
                 { ("the spell", spellHits, spellSp), ("chest trap 6", trapHits, trapSp) })
        {
            Console.WriteLine($"\n{route}, 10,000 casts on a character with 40 max hits and 40 max spell points:");
            Report("hits", hits);
            Report("spell points", sp);
        }

        // the floor is the whole story: a spell that rerolls your hits and cannot reach zero
        Console.WriteLine($"\ntimes Fate left the character on 0 hits: {spellHits.Count(h => h == 0) + trapHits.Count(h => h == 0)} of 20,000");
        Console.WriteLine($"times it reached the maximum, 40:        {spellHits.Count(h => h == 40) + trapHits.Count(h => h == 40)}");

        // and it is a reroll rather than damage, so it can hand hits back
        var hurt = new Random(7);
        int helped = 0;
        for (int i = 0; i < 2000; i++)
        {
            var c = Subject(); c.CurHits = 1;
            var s = spells.First(x => x.Effect == SpellEffect.Fate);
            SpellEffect.ApplyToCharacter(SpellEffect.Fate, c, s, hurt, Look);
            if (c.CurHits > 1) helped++;
        }
        Console.WriteLine($"\ncast on a character down to 1 hit, it healed them {helped} times in 2,000"
                        + " - it is a reroll, not damage");

        // is the spell reachable at all? no guild ceiling admits it, so only an item can carry it
        var elixr = items.FirstOrDefault(i => i.Name.Trim() == "Elixr of Fate");
        if (elixr != null)
        {
            var c = Subject();
            c.PutInPack(new InventorySlot
            {
                ItemId = elixr.Id, Charges = (short)Math.Max(1, elixr.Charges),
                Known = IdLevel.All, Equipped = ItemUse.MustBeEquippedToUse(elixr),
            });
            var offer = Spellbook.Castables(c, spells, Look).FirstOrDefault(x => x.FromItem && x.Item!.Id == elixr.Id);
            Console.WriteLine($"\nElixr of Fate, floor {elixr.Floor}, offers: "
                            + $"{(offer == null ? "NOTHING" : offer.Spell.Name.Trim())}"
                            + $" (effect {offer?.Spell.Effect})");
        }
    }

    private static void Report(string what, List<int> draws)
    {
        Console.WriteLine($"  {what,-13} low {draws.Min(),2}  high {draws.Max(),2}  mean {draws.Average(),5:F2}"
                        + $"  (uniform 1..40 would mean 20.5)");
    }

    private static (List<int> Hits, List<int> Sp) Measure(int n, Random rng, Action<Character, Random> apply)
    {
        var hits = new List<int>(n); var sp = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            var c = Subject();
            apply(c, rng);
            hits.Add(c.CurHits); sp.Add(c.CurSpellPoints);
        }
        return (hits, sp);
    }

    /// <summary>40 max hits and 40 max spell points, so both pools read on the same scale.</summary>
    public static Character Subject()
    {
        var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                 new[] { 18, 18, 18, 18, 18, 18 });
        c.Guilds.Clear();
        c.Guilds.Add(new GuildMembership { Guild = "Mage", Level = 10 });
        c.MaxHits = 40; c.CurHits = 40;
        c.MaxSpellPoints = 40; c.CurSpellPoints = 40;
        return c;
    }
}
