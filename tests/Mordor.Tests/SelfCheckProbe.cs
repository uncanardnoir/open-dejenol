using Mordor.Core;

/// <summary>
/// The figures this port can be held to, asserted rather than printed.
///
/// Most probes print a computed number beside the one their source quotes and leave a human to
/// compare them. That is fine when you are working on the rule and useless afterwards: running the
/// whole suite tells you only that nothing threw. This one fails, with an exit code, when a number
/// stops matching - so a regression in the arithmetic is caught by running it rather than by
/// somebody rereading eighty pages of output.
///
/// Every expectation here is a figure quoted by a source the port is allowed to use: the
/// dejenol.com pages, the original's help, or the MG11.DLL resources themselves. Nothing here is a
/// number this port produced and then enshrined - that would only lock in whatever it does today.
/// </summary>
public static partial class SelfCheckProbe
{
    private static int _failed;

    private static void Check(string what, object? got, object? want)
    {
        bool ok = got?.ToString() == want?.ToString();
        if (!ok) _failed++;
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what,-52} got {got}, want {want}");
    }

    private static void CheckNear(string what, double got, double low, double high)
    {
        bool ok = got >= low && got <= high;
        if (!ok) _failed++;
        // Whole numbers read better without decimals, but a fractional bound printed as N0 states
        // something the check is not testing: "got 20, want 20..22" for 20.46 in 19.5..21.5.
        string f = got == Math.Floor(got) && low == Math.Floor(low) && high == Math.Floor(high) ? "N0" : "N2";
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what,-52} got {got.ToString(f)}, want {low.ToString(f)}..{high.ToString(f)}");
    }

    private static void CheckAtLeast(string what, int got, int least)
    {
        bool ok = got >= least;
        if (!ok) _failed++;
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what,-52} got {got}, want {least} or more");
    }

    /// <summary>
    /// The game folder checked is the shareware's (<see cref="GameFiles.Check"/>: Limited). Its
    /// MDATA1, 2, 3 and 5 and MG11.DLL are the full 1.1's byte for byte, but its dungeon has three
    /// levels and its help and MORDOR.EXE are other files (docs/GAME_FILES.md), so a figure counted
    /// across the full game's fifteen levels or its help cannot be held to it.
    /// </summary>
    private static bool _shareware;

    /// <summary>
    /// True, after a line saying so, when the folder is the shareware and the check that follows
    /// holds a figure of the full game's; such a check is skipped, not counted.
    /// </summary>
    private static bool FullGameOnly(string what)
    {
        if (!_shareware) return false;
        Console.WriteLine($"  skip  {what}: full game only (this folder is the shareware)");
        return true;
    }

    /// <summary><see cref="Check"/>, for a figure of the full game's (<see cref="FullGameOnly"/>).</summary>
    private static void FullCheck(string what, object? got, object? want)
    {
        if (!FullGameOnly(what)) Check(what, got, want);
    }

    /// <summary><see cref="CheckAtLeast"/>, for a figure of the full game's (<see cref="FullGameOnly"/>).</summary>
    private static void FullCheckAtLeast(string what, int got, int least)
    {
        if (!FullGameOnly(what)) CheckAtLeast(what, got, least);
    }

    /// <summary>
    /// Runs a section of checks. In a tree without the decompiled source (<see cref="PortTree"/>),
    /// a section that reads it stops at the read (<see cref="DecompiledMissing"/>) and says it was
    /// skipped; what it checked before the read still counts. With the source here, it is the
    /// section run as it stands.
    /// </summary>
    private static void Section(Action checks, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(checks))] string what = "") =>
        Section(what.Replace("() => ", "").Trim(), checks);

    /// <summary><see cref="Section(Action, string)"/>, named.</summary>
    private static void Section(string name, Action checks)
    {
        try { checks(); }
        catch (DecompiledMissing)
        {
            Console.WriteLine($"  {PortTree.SkipLine} - {name}: the rest of it not run");
        }
    }

    /// <summary>
    /// A file of the decompiled original under the tree's root (<c>decompiled</c>/... or
    /// <c>decompiled-named</c>/...), read; <see cref="DecompiledMissing"/> in a tree without it.
    /// </summary>
    private static string Decompiled(params string[] parts)
    {
        string p = PortTree.At(parts);
        if (PortTree.PrivateMissing(parts)) throw new DecompiledMissing(p);
        return System.IO.File.ReadAllText(p);
    }

    /// <summary>A folder of the decompiled original under the tree's root; <see cref="DecompiledMissing"/> in a tree without it.</summary>
    private static string DecompiledDir(string folder)
    {
        if (PortTree.PrivateMissing(folder)) throw new DecompiledMissing(PortTree.At(folder));
        return PortTree.At(folder);
    }

    /// <summary>
    /// A file of the decompiled original that a check needs is missing: a failure when the source
    /// is here, and the skip line when it is not.
    /// </summary>
    private static void SourceMissing(string what)
    {
        if (!PortTree.HasDecompiled) { Console.WriteLine($"  {PortTree.SkipLine} - {what}"); return; }
        Console.WriteLine($"  FAIL  {what} missing");
        _failed++;
    }

    /// <summary>Extra swings a character of this guild has at this level, from nothing else.</summary>
    private static int SwingsAt(string guild, int level)
    {
        var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                 new[] { 18, 18, 18, 18, 18, 18 });
        c.Guilds.Clear();
        c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
        c.Swings = 0;
        c.RecalcSwings();
        return c.Swings;
    }

    public static int Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        _failed = 0;
        _shareware = GameFiles.Check(dir).IsLimited;
        if (_shareware)
            Console.WriteLine("the game folder is the shareware: the checks of the full game's dungeon and help say \"full game only\" and are skipped\n");

        // ---- the dejenol.com rules pages ----
        Console.WriteLine("experience required (dejenol.com: Human Nomad L100 = 872,326, L80 = 556,907)");
        // fn12A1 (MAINMOD2.BAS:2118-2138), at the level below: Human is race Exp 3
        Check("fn12A1, Human Nomad at L99", Rules.Fn12A1(99, 8, 3), 872_326);
        Check("fn12A1, Human Nomad at L79", Rules.Fn12A1(79, 8, 3), 556_907);
        Check("fn12A1 leaves l038C / 25 unnarrowed, as the x87 does: Human Nomad at L65", Rules.Fn12A1(65, 8, 3), 378_045L);
        Check("fn13ED is fn12A1 a level up, less one", Rules.Fn13ED(79, 8, 3), Rules.Fn12A1(80, 8, 3) - 1);
        {
            // the page's closed form against the routine, every guild and race Exp to level 200. The
            // closed form lives only here: nothing in the game uses it (it was Rules.ExpRequired).
            static double RaceMult(int raceExp) => raceExp switch
            {
                <= 3 => 1.0, 4 => 1.05819132268, 5 => 1.10551994263, 6 => 1.14714602440, 7 => 1.18544741085, 8 => 1.22165868435, _ => 1.25647909518,
            };
            static long ClosedForm(int level, float gef, double raceMult)
            {
                if (level <= 1) return 0;
                double l = level - 1;
                double xp = 88.09020776 * l * l * (gef / 8.0) + 45.45454 * ((l * 2) - 1) - 0.264;
                return (long)Math.Round(xp * raceMult);
            }
            int differ = 0, far = 0, total = 0;
            foreach (var g in GuildData.All)
                foreach (float race in RaceData.All.Select(r => r.ExpFactor).Distinct())
                    for (int lv = 1; lv <= 200; lv++)
                    {
                        long a = Rules.Fn12A1(lv, g.ExpFactor, race);
                        long b = ClosedForm(lv + 1, g.ExpFactor, RaceMult((int)race));
                        total++;
                        if (a != b) differ++;
                        if (Math.Abs(a - b) > 1) far++;
                    }
            Console.WriteLine($"  fn12A1 against the closed form: {differ} of {total} differ");
            Check("and none by more than one", far, 0);
        }

        Console.WriteLine("\nguild join cost (dejenol.com: Warrior 5,009,672  Ninja 10,886,091  Thief 7,465,060)");
        foreach (var (name, want) in new[] { ("Warrior", 5_009_672L), ("Ninja", 10_886_091L), ("Thief", 7_465_060L) })
            Check(name, Rules.GuildJoinCost(GuildData.ByName(name)), want);

        // Gold_Required_for_Level_Formula: the guild gold factors as Wabbit's editor shows them, a
        // L755 Thief paying "almost 33 million" a level, "more than 8 billion" spent getting there
        // and "nearly 19 billion" in all to reach 999. fn12B2 (MAINMOD2.BAS:2140-2155) with MDATA1's
        // Gold field is held to all of them.
        Console.WriteLine("\ngold to make level (dejenol.com: GGF per guild; a L755 Thief pays almost 33 million)");
        foreach (var (name, ggf) in new[] { ("Warrior", 1), ("Paladin", 2), ("Nomad", 3), ("Seeker", 3), ("Mage", 3), ("Sorcerer", 3),
                                            ("Wizard", 4), ("Healer", 4), ("Ninja", 5), ("Villain", 5), ("Scavenger", 7), ("Thief", 9) })
            Check($"{name} gold factor", GuildData.ByName(name).GoldFactor, ggf);
        {
            int thiefGold = GuildData.ByName("Thief").GoldFactor;
            CheckNear("fn12B2, Thief from L755", Rules.Fn12B2(755, thiefGold), 32_000_000, 33_000_000);
            double spent = 0, toTop = 0;
            for (int lv = 1; lv <= 998; lv++) { long f = Rules.Fn12B2(lv, thiefGold); toTop += f; if (lv < 755) spent += f; }
            CheckNear("fn12B2, Thief L1 to L755, billions", spent / 1e9, 8, 9);
            CheckNear("fn12B2, Thief L1 to L999, billions", toTop / 1e9, 18, 19);
            // The page's own loop is JavaScript, whose Math.round takes every x.5 up. On the x87 the
            // terms that are a whole number and a half are a shade over it (60.2#), so fn12B2 takes
            // them up too, and the two part only where the last line's Gold / 10 lands - by one
            // gold at most (1,130 of 11,976 levels) - printed for information, not asserted.
            int differ = 0, total = 0; long gap = 0;
            foreach (var g in GuildData.All)
            {
                long levelCost = 0;
                for (int lv = 1; lv <= 998; lv++)
                {
                    levelCost += (long)Math.Round(((lv * 2) - 1) * 30.1, MidpointRounding.AwayFromZero);
                    long page = (long)Math.Round(levelCost * (1 + g.GoldFactor / 10.0), MidpointRounding.AwayFromZero);
                    long b = Rules.Fn12B2(lv, g.GoldFactor);
                    total++;
                    if (b != page) differ++;
                    gap = Math.Max(gap, Math.Abs(b - page));
                }
            }
            Console.WriteLine($"  fn12B2 against the page's JavaScript loop: {differ} of {total} levels differ, by at most {gap:N0}");
            // the x87 reading (Rules.Fn12B2): 30.1 * 5 = 150.5 at i = 3 is a shade over the half there
            Check("fn12B2 on the x87, Nomad from L3 (150.5 rounds up, not to even)", Rules.Fn12B2(3, GuildData.ByName("Nomad").GoldFactor), 352L);
            Check("fn12B2 on the x87, Warrior from L1", Rules.Fn12B2(1, GuildData.ByName("Warrior").GoldFactor), 33L);
        }

        // The page gives A/D = 2*(Str+Dex) + Int + Wis - 76, and says it holds when Str and Dex are
        // at least 12 and Int and Wis at least 14 - so only cases meeting that are checked here.
        Console.WriteLine("\nstats A/D (dejenol.com: 2*(Str+Dex) + Int + Wis - 76, above the thresholds)");
        foreach (var s in new[] { new[] { 12, 14, 14, 10, 10, 12 }, new[] { 18, 18, 18, 10, 10, 18 }, new[] { 30, 20, 20, 10, 10, 30 } })
            Check($"[{string.Join(",", s)}]", Rules.StatsAD(s), 2 * (s[0] + s[5]) + s[1] + s[2] - 76);

        // ---- the original's help ----
        // "Every character has a certain number of Spell Points ... based on the character's
        // Intelligence and Wisdom", and its tutorial Nomad with INT 12 / WIS 12 has 120.
        Console.WriteLine("\nspell points (help: the tutorial Nomad, INT 12 WIS 12, has 120)");
        var tutorial = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                        new[] { 12, 12, 12, 12, 12, 12 });
        Check("INT 12 WIS 12", Spellbook.MaxSpellPoints(tutorial), 120);

        // ---- the help against the data ----
        // Every spell page in the help carries a COMBAT / NON-COMBAT / "COMBAT & NON-COMBAT" label
        // written by hand, and fn149E (MAINMOD2.BAS:2908) works the same thing out from MDATA2.
        // Two independent sources, so a disagreement means one of them is being read wrongly.
        // The count is floored as well as the disagreement checked: a matcher that quietly stopped
        // finding labels would otherwise pass this by comparing nothing at all.
        Console.WriteLine("\ncast gate (the help's own labels vs fn149E, over every spell the help labels)");
        var gate = CastGateProbe.Compare(dir);
        Check("spells where the two disagree", gate.Disagree, 0);
        CheckAtLeast("spells compared", gate.Agree, 91);

        // The help works one example of the guild rate table by hand: "A good example is the Charm
        // of Opening spell, which is Spell Level 3 for the Thief's guild, but Spell Level 12 for
        // the Nomad's guild." Charm of Opening is base level 1 in MDATA2, so this is a direct test
        // of both the MDATA1 rate read and Int(Level * rate) (FRMCHAR3.BAS:110).
        Console.WriteLine("\nthe help's worked example (Charm of Opening: Thief 3, Nomad 12)");
        var open = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"))
                            .First(sp => sp.Name.Trim() == "Charm of Opening");
        Check("base level in MDATA2", open.Level, 1);
        Check("Thief learns it at spell level",
              GuildData.ByName("Thief").ShownSpellLevel(open.Category, open.Level), 3);
        Check("Nomad learns it at spell level",
              GuildData.ByName("Nomad").ShownSpellLevel(open.Category, open.Level), 12);

        // Every spell page also states a base level, a stat requirement and the guilds that get it.
        // The guild list is the strongest of the three: it is not stored per spell anywhere, only
        // implied by each guild's MDATA1 row, and the help still names exactly the same set every
        // time. The other two are asserted at the counts they actually hold, mismatches included,
        // because those mismatches are the original's own and are listed in FIDELITY.md - pinning
        // them here means a change in either direction shows up.
        Console.WriteLine("\nthe help's spell headers (base level, stats, guilds) vs MDATA1 and MDATA2");
        var sh = SpellHelpProbe.Compare(dir, verbose: false);
        Check("guild lists that disagree", sh.GuildBad, 0);
        CheckAtLeast("spell entries with a guild list compared", sh.GuildOk, 92);
        Check("spell levels in brackets that disagree", sh.WhenBad, 1);
        CheckAtLeast("guild/spell pairs compared", sh.WhenOk, 190);
        FullCheck("base levels where the help differs", sh.LevelBad, 4);
        Check("stat lines where the help differs", sh.StatsBad, 3);

        // When a spell actually arrives. FRMCHAR3.BAS:97 gates the list on
        //
        //     (SpellDefs(s).Level * rate) <= ((GuildsJoined(g).Level / 2) + .5#)
        //
        // so the spell's rated level is compared against the caster's spell level, not against the
        // raw guild level. Teleport is a level 12 Movement spell and the Wizard's Movement rate is
        // 1, which puts the boundary at guild level 23: 12 <= 23/2 + .5 holds and 12 <= 22/2 + .5
        // does not. Testing both sides is the point - it is the one place the corrected rule and
        // the uncited Ceiling(Level * rate) it replaced give different answers, and a check that
        // only ever asked the "yes" side would pass under either.
        Console.WriteLine("\nwhen a spell arrives (FRMCHAR3.BAS:97; Teleport L12, Wizard Movement rate 1)");
        var wiz = GuildData.ByName("Wizard");
        var move = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"))
                            .First(sp => sp.Name.Trim() == "Teleport");
        Check("Teleport is a level 12 spell", move.Level, 12);
        Check("Wizard Movement rate", wiz.SpellRate[move.Category], 1);
        Check("a Wizard 22 cannot cast it", wiz.CanCast(move.Category, move.Level, 22), false);
        Check("a Wizard 23 can", wiz.CanCast(move.Category, move.Level, 23), true);

        // The Spell_Damage_Formula page names four spells with their Base/Power ratings -
        // "FB = Firebolt(10/1)", "BF = Blue Flame(15/11)", "CB = Cold Blast(15/2)",
        // "AS = Acidic Spray(30/11)" - and the help explains the notation: "a power & damage (or
        // heal) rating (e.g. '15/2') ... a Base number and a LevelModifier number". Base is MDATA2's
        // Damage2 and Power is SpecialEffect + 1, which is how Rules.SpellDamage is fed. Four
        // quoted pairs is a cheap guard on two field offsets that would otherwise rot in silence.
        Console.WriteLine("\ndamage ratings (Spell_Damage_Formula: Firebolt 10/1, Blue Flame 15/11, Cold Blast 15/2, Acidic Spray 30/11)");
        var dmg = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        foreach (var (name, bas, power) in new[]
                 { ("Firebolt", 10, 1), ("Blue Flame", 15, 11), ("Cold Blast", 15, 2), ("Acidic Spray", 30, 11) })
        {
            var sp = dmg.First(x => x.Name.Trim() == name);
            Check($"{name} rating", $"{sp.Damage2}/{sp.SpecialEffect + 1}", $"{bas}/{power}");
        }

        // The same page then tabulates maximum damage for those four spells at four spell levels:
        //
        //             FB      BF       CB       AS
        //     SL  15   25/26   53/ 54   39/40    92/ 95
        //     SL  65   35/35  117/118   59/59   170/172
        //     SL 115   38/38  172/173   66/69   230/232
        //     SL 255   43/43  313/320   89/90   386/387  <--- Max SL
        //
        // "X is observed Max Damage, Y is calculated Max Damage". The checks below use Y, because
        // Y is what the published formula produces and that formula is what Rules.SpellDamage
        // implements; X came off a forum thread and is a sample maximum, which can only fall short.
        //
        // Maximum damage is the Rnd = 0 case: Modifier2 becomes 1 and Damage = Int(Modifier1 - 1).
        // Feeding a Random that always returns zero makes all sixteen deterministic, so this pins
        // the whole formula - both logarithms, the Int() on the power term and the final Int() -
        // at four points two orders of magnitude apart.
        Console.WriteLine("\nmaximum spell damage (Spell_Damage_Formula's calculated column, Rnd = 0)");
        var zero = new ZeroRandom();
        foreach (var (name, bas, power, want) in new[]
                 {
                     ("Firebolt",     10,  1, new[] { 26,  35,  38,  43 }),
                     ("Blue Flame",   15, 11, new[] { 54, 118, 173, 320 }),
                     ("Cold Blast",   15,  2, new[] { 40,  59,  69,  90 }),
                     ("Acidic Spray", 30, 11, new[] { 95, 172, 232, 387 }),
                 })
        {
            var sls = new[] { 15, 65, 115, 255 };
            var got = sls.Select(sl => Rules.SpellDamage(zero, bas, power, sl, 0)).ToArray();
            Check($"{name} at SL {string.Join("/", sls)}", string.Join(",", got), string.Join(",", want));
        }

        // Protection, which the community page marks "Buggy" and the source bears out. The value a
        // cast writes (MAINMOD2.BAS:1046) is Int(casterSpellLevel / 2) + 1 and that case never
        // calls recomputeEffects; recomputeEffects (2805, 2827) writes Int(currentGuild.Level / 2)
        // + 1 instead, and overwrites it the next time anything triggers one. The page states both
        // halves: "3 * LN(Int(SL / 2) + 1)^2" for a guild spell against "3 * LN(SL)^2" for an item.
        // A Wizard at guild level 26 has spell level Int(26/2 + .5) = 13, so the cast is worth
        // Int(13/2) + 1 = 7 and the recompute 26/2 + 1 = 14 - which is the doubling, asserted so it
        // stays a reproduced fault rather than quietly becoming one value or the other.
        Console.WriteLine("\nprotection (MAINMOD2.BAS:1046 against 2805/2827 - the page's \"Buggy\")");
        var all = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        var prot = all.First(sp => SpellEffect.Of(sp) == SpellEffect.Protect);
        var pc = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                  new[] { 18, 18, 18, 18, 18, 18 });
        pc.Guilds.Insert(0, new GuildMembership { Guild = "Wizard", Level = 26 });
        Check("a Wizard 26 has spell level", Rules.SpellLevel(26), 13);
        SpellEffect.ApplyToCharacter(SpellEffect.Protect, pc, prot, new ZeroRandom(), _ => null);
        Check("what the cast leaves", pc.Protection, 7);
        Effects.Recompute(pc, _ => null);
        Check("what the next recompute leaves", pc.Protection, 14);

        // Spell cost. The Spell_Cost_Formula page gives an identity worth checking directly: at the
        // level where a guild first grants the spell, SL = StartSpellLevel * BaseMod, and the cost
        // collapses to "Initial Spell Cost = (SpellPointRatio * BaseMod * 1.9) - 0.0001, rounded
        // down". Firebolt is level 1 with a ratio of 14, the Sorcerer's Fire rate is 1, and a
        // Sorcerer at guild level 1 has SL = Int(1/2 + .5) = 1 - so it should cost
        // Int(14 * 1 * 1.9 - 0.0001) = 26.
        Console.WriteLine("\nspell cost (Spell_Cost_Formula's initial-cost identity, and fn1485's cheapest guild)");
        var fire = all.First(sp => sp.Name.Trim() == "Firebolt");
        var sorc = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
        sorc.Guilds.Insert(0, new GuildMembership { Guild = "Sorcerer", Level = 1 });
        Check("Firebolt for a Sorcerer 1", Spellbook.Cost(fire, sorc), (int)(14 * 1 * 1.9 - 0.0001));

        // And the rule that picks the guild. fn1485 prices the spell in every guild and keeps the
        // cheapest; the help says it too - "if the Wizard's guild can cast the spell for less points
        // than the Mage's guild, the lowest points will be used". Cost goes as rate^2 / SL, so the
        // guild with the highest spell level is not always the cheap one, which is what makes this
        // case discriminating. Harm is a level 6 Wounds spell with a ratio of 40; the Wizard's
        // Wounds rate is 1.25 and the Healer's is 1.
        //
        //   Wizard 40: SL = Int(40/2 + .5) = 20, rated = round(6 * 1.25) = 8 (half to even)
        //              Int(8/20 * 40 * 1.25 * 1.9 - .0001) = 37
        //   Healer 28: SL = Int(28/2 + .5) = 14, rated = 6
        //              Int(6/14 * 40 * 1 * 1.9 - .0001) = 32
        //
        // The Wizard has the higher spell level and the Healer is cheaper, so 32 is the answer and
        // the guild that used to be chosen would have given something else entirely.
        var harm = all.First(sp => sp.Name.Trim() == "Harm");
        var two = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
        two.Guilds.Insert(0, new GuildMembership { Guild = "Wizard", Level = 40 });
        two.Guilds.Add(new GuildMembership { Guild = "Healer", Level = 28 });
        Check("Harm for a Wizard 40 who is also a Healer 28", Spellbook.Cost(harm, two), 32);

        // fn1485's own arithmetic (MAINMOD2.BAS:2868-2893) in place of the page's "- 0.0001": Int()
        // floors the x87's product, and 1.9# is a shade under 1.9. The page's own example - an exact
        // 209 comes out 208 - is a ratio of 110 at the first spell level (rated / SL = 1):
        Check("fn1485: an exact 209 floors to 208", Rules.Fn1485Price(1, 110, 1f, 1, out _), 208);
        // and where the two part: a true cost a hair over a whole number. A rate of 1.1 is the
        // Single 1.10000002384..., so ratio 100 at SL 1 is 209.0000045 - 209 to the source, 208
        // to the page's epsilon. No shipped guild has such a rate: on the shipped tables the two
        // agree everywhere but the ratio-0 spells, where the page went to -1 and fn1485 gives 0.
        Check("fn1485: 209.0000045 floors to 209", Rules.Fn1485Price(1, 100, 1.1f, 1, out _), 209);
        Check("fn1485: a ratio of 0 prices at 0", Rules.Fn1485Price(1, 0, 1f, 1, out _), 0);
        // Measured in the running original (2026-10-25, under otvdm from a scratch copy): the Points
        // column of Objects & Spells, which is fn1485. A new Human Sorcerer (Int 18, Wis 16 as
        // created; fn1485 reads no stat) at SL 1, and a Nomad 37 who is also a Sorcerer 37 (SL 19
        // in both). Every price matched Fn1485Price, and nine discriminate: the reading with every
        // step rounded to its VB type (Single or Double) gives one more for each of Shock at SL 1
        // (19) and, at SL 19, Charm of Opening (144), Cold Blast, Sight Veil, See Invisible (3),
        // Mass Sight Veil (36), Sphere of Flames (75), Flesh to Stone (96) and Paralyzing Death (90).
        // The rest are controls that both readings give.
        {
            var newSorc = Character.Create("probe", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male, new[] { 15, 18, 16, 15, 15, 15 });
            newSorc.Guilds.Clear();
            newSorc.Guilds.Add(new GuildMembership { Guild = "Sorcerer", Level = 1 });
            foreach (var (name, want) in new[] { ("Shock", 18), ("Firebolt", 26) })
                Check($"measured: {name} for a new Sorcerer (SL 1)", all.FirstOrDefault(sp => sp.Name.Trim() == name) is Spell sp1 ? Spellbook.Cost(sp1, newSorc) : -1, want);
            var sl19 = Character.Create("probe", RaceData.ByName("Human"), Alignment.Neutral, Sex.Male, new[] { 15, 18, 16, 15, 15, 15 });
            sl19.Guilds.Clear();
            sl19.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 37 });
            sl19.Guilds.Add(new GuildMembership { Guild = "Sorcerer", Level = 37 });
            foreach (var (name, want) in new[]
                     {
                         // the nine that discriminate
                         ("Charm of Opening", 143), ("Cold Blast", 2), ("Sight Veil", 2), ("See Invisible", 2), ("Mass Sight Veil", 35),
                         ("Sphere of Flames", 74), ("Flesh to Stone", 95), ("Paralyzing Death", 89),
                         // controls
                         ("Poison", 2), ("Leprosy", 9), ("Acidic Spray", 23), ("Ice Spray", 15), ("Hail Storm", 52), ("Arctic Storm", 119),
                         ("Firebolt", 1), ("Blue Flame", 3), ("Flamesheet", 10), ("Pillar of Fire", 31), ("Resist Fire", 4), ("Resist Draining", 4),
                         ("Resist Cold", 4), ("Resist Poison", 7), ("Resist Disease", 7),
                     })
                Check($"measured: {name} for a Nomad 37 + Sorcerer 37 (SL 19)", all.FirstOrDefault(sp => sp.Name.Trim() == name) is Spell sp19 ? Spellbook.Cost(sp19, sl19) : -1, want);
        }
        // mdrcharCheckCast's first line, CombatOrders(pv00FC).Att = False (MAINMOD2.BAS:433), which the
        // spell page runs for each spell it lists (FRMCHAR3.BAS:112, 699): Fight and Defend go to
        // None, a queued spell and the aimed-at group stay
        Console.WriteLine("\nlisting spells clears Att (MAINMOD2.BAS:433; FRMCHAR3.BAS:112, 699)");
        {
            var fightOrder = new CombatOrder { Kind = OrderKind.Fight, Target = 2 };
            Check("a Fight order is cleared", fightOrder.CheckCastClearsAtt() && fightOrder.Kind == OrderKind.None ? 1 : 0, 1);
            Check("the group aimed at stays", fightOrder.Target, 2);
            var defendOrder = new CombatOrder { Kind = OrderKind.Defend };
            Check("a Defend order is cleared", defendOrder.CheckCastClearsAtt() && defendOrder.Kind == OrderKind.None ? 1 : 0, 1);
            var spellOrder = new CombatOrder { Kind = OrderKind.Spell, SpellId = 7 };
            Check("a queued spell stays", !spellOrder.CheckCastClearsAtt() && spellOrder.Kind == OrderKind.Spell && spellOrder.SpellId == 7 ? 1 : 0, 1);
            // and an item's stat change re-lists them (l085E, MAINMOD2.BAS:6156-6200, sub1658 at 6230)
            var user = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
            var plain = new InventorySlot { ItemId = 1 };
            Check("special 1 re-lists", ItemUse.ChangesStats(user, plain, new Item { SpecialType = 1 }) ? 1 : 0, 1);
            Check("special 14 re-lists", ItemUse.ChangesStats(user, plain, new Item { SpecialType = 14 }) ? 1 : 0, 1);
            Check("special 11 (spell points) does not", ItemUse.ChangesStats(user, plain, new Item { SpecialType = 11 }) ? 1 : 0, 0);
        }
        // A heal in an engaged fight is refused, not aimed: fn149E makes the Heal class non-combat
        // (MAINMOD2.BAS:2908-2921), mdrcharCheckCast refuses that while engaged (512-519), and the
        // round's mdrcharCastSpell checks first (831) and then "sub102F caster, 1" (832) - Fight.
        Console.WriteLine("\na heal ordered in a fight (MAINMOD2.BAS:512-519, 831-832, 2908-2921)");
        {
            var healSpell = all.First(sp => sp.Name.Trim() == "Heal");
            var foeDef = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).First(m => m.Id != 0 && m.Hits > 0);
            var healer = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
            healer.Guilds.Insert(0, new GuildMembership { Guild = "Healer", Level = 40 });
            healer.CurSpellPoints = 5000;
            healer.MaxHits = 5000; healer.CurHits = 10;
            var healParty = new Party();
            healParty.Members.Add(healer);
            var order = healParty.OrderFor(healer);
            order.Kind = OrderKind.Spell; order.SpellId = healSpell.Id; order.Target = 0;
            var healFight = new CombatEngine(healParty, new List<CombatMonster> { new() { Def = foeDef, Hits = 100000 } }, _ => null, new Random(7));
            healFight.UseSpellTable(all);
            int healerHits = healer.CurHits;
            var said = healFight.RoundPaced(all).Select(l => l.Text).ToList();
            Check("the round says the original's refusal", said.Contains("\"Heal\" is a Non-Combat Spell!") ? 1 : 0, 1);
            Check("and the order becomes Fight", order.Kind, OrderKind.Fight);
            Check("the group aimed at stays", order.Target, 0);
            Check("no spell points spent", healer.CurSpellPoints, 5000);
            Check("nobody healed", healer.CurHits <= healerHits ? 1 : 0, 1);
            var direct = new CombatEngine(healer, new[] { new CombatMonster { Def = foeDef, Hits = 100000 } }, _ => null, new Random(7));
            direct.UseSpellTable(all);
            Check("a direct cast is refused the same way", direct.CastSpell(healSpell), "\"Heal\" is a Non-Combat Spell!");

            // the round re-asks the whole of mdrcharCheckCast on every cast (MAINMOD2.BAS:831): not
            // knowing the spell (437-455) and an anti-magic square (520-526) included, each refused
            // before the draw at 840, then sub102F caster, 1 - Fight
            var fireSpell = all.First(sp => sp.Name.Trim() == "Firebolt");
            var monsterDefs = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var biter = monsterDefs.First(m => m.Id != 0 && m.Hits > 0 && m.Att > 0);
            (List<string> Lines, CombatOrder Order, CombatEngine Fight, Character Who, double After) RoundWith(string guild, bool noSpells, bool order, Action<CombatEngine>? setup = null)
            {
                var who = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
                who.Guilds.Clear();
                who.Guilds.Add(new GuildMembership { Guild = guild, Level = 40 });
                who.CurSpellPoints = 5000; who.MaxHits = 90000; who.CurHits = 90000;
                var p = new Party();
                p.Members.Add(who);
                var o = p.OrderFor(who);
                if (order) { o.Kind = OrderKind.Spell; o.SpellId = fireSpell.Id; o.Target = 0; }
                var rng11 = new Random(11);
                var f = new CombatEngine(p, new List<CombatMonster> { new() { Def = biter, Hits = 100000 } }, _ => null, rng11);
                f.UseSpellTable(all);
                f.NoSpellsHere = noSpells;
                setup?.Invoke(f);
                var said11 = f.RoundPaced(all).Select(l => l.Text).ToList(); return (said11, o, f, who, rng11.NextDouble());
            }
            var unknown = RoundWith("Warrior", false, true);
            Check("a spell not known: refused every round", unknown.Lines.FirstOrDefault(l => l.Contains("know")), "You do not know \"Firebolt\".");
            Check("and the member fights", unknown.Order.Kind, OrderKind.Fight);
            var still = RoundWith("Warrior", false, false);
            Check("refused before the draw: the monsters' turn draws as with no order at all",
                  string.Join("|", unknown.Lines.Where(l => !l.Contains("know"))) + $" {unknown.After:R}", string.Join("|", still.Lines) + $" {still.After:R}");
            string? perceived = null;
            var dampened = RoundWith("Sorcerer", true, false);
            var anti = RoundWith("Sorcerer", true, true);
            Check("an anti-magic square: \"Anti-Magic area!\" every round", anti.Lines.Contains("Anti-Magic area!") ? 1 : 0, 1);
            Check("then Fight, and no points spent", $"{anti.Order.Kind} {anti.Who.CurSpellPoints}", "Fight 5000");
            Check("and no draw either", string.Join("|", anti.Lines.Where(l => l != "Anti-Magic area!")) + $" {anti.After:R}",
                  string.Join("|", dampened.Lines) + $" {dampened.After:R}");
            // and a cast that passes does draw: the same Sorcerer off the anti-magic square
            var passes = RoundWith("Sorcerer", false, true);
            var idle = RoundWith("Sorcerer", false, false);
            Check("a cast that passes the check draws (the RNG moves on)", passes.After != idle.After ? 1 : 0, 1);
            RoundWith("Sorcerer", true, true, f => f.AntiMagicRefused = w => perceived = w.Name);
            Check("the refusal is handed to the window for the map (523)", perceived, "probe");
        }
        Check("fn1485: rated = Level * rate into an Integer, half to even", Rules.Fn1485Price(6, 40, 1.25f, 20, out int ratedHarm) >= 0 ? ratedHarm : -1, 8);

        // The Combat_Initiative_Formula page writes its ceiling as "[maximum 98?]", with the
        // question mark, and the source says why: the cap is on one call path and not the other.
        // MULTIMOD.BAS:337 (Moved) floors at 5 and never caps; :355 (ChangedLevel) caps at 98 as
        // well. So a monster 10 or more points of Dex ahead of you attacks 98% of the time when you
        // take the stairs and 100% of the time when you walk. Both numbers are the original's, and
        // asserting the pair is what keeps a later tidy-up from making them agree.
        Console.WriteLine("\ninitiative clamps (MULTIMOD.BAS:337 against :355 - the page's \"[maximum 98?]\")");
        foreach (var (kind, monDex, charDex, want) in new[]
                 {
                     ("Moved        +20 Dex", 30, 10, 150),   // uncapped
                     ("ChangedLevel +20 Dex", 30, 10,  98),   // capped
                     ("Moved        -20 Dex", 10, 30,   5),   // floored
                     ("ChangedLevel -20 Dex", 10, 30,   5),
                 })
        {
            var k = kind.StartsWith("ChangedLevel", StringComparison.Ordinal)
                  ? AttackKind.ChangedLevel : AttackKind.Moved;
            Check(kind, CombatEngine.AttackChance(k, monDex, charDex), want);
        }

        // Chance to hit. The port computes fn0DEE's threshold (MODULE4.BAS:2221-2236) and misses on
        // Int(Rnd * 100) + 1 >= threshold, so a threshold of 98 is a 97% hit rate - which is what
        // the Character_Attack_Formulas page means by "range [1 to 97]" and by "Since Monster
        // Defense passed to this formula is 0, you should always have 97% chance to hit". The two
        // sources look like they disagree by one and do not; the page counts hits and the code
        // holds a threshold. Asserting both ends of the clamp keeps that distinction honest.
        Console.WriteLine("\nchance to hit (fn0DEE's threshold; the page counts the hits, one lower)");
        Check("any attack against defence 0", Rules.HitThreshold(10, 0), 98);      // the page's 97%
        Check("attack and defence both 0", Rules.HitThreshold(0, 0), 50);
        Check("hopelessly outmatched", Rules.HitThreshold(1, 1000), 2);            // the page's 1%

        // Experience scaling. The Experience_Awarded_Formula page quotes both of its factors as
        // decimals, which makes them assertable without re-deriving anything: GuildLevelFactor
        // "equates to 0.997 for a Level 1 character, 0.5 for a Level 333 character, all the way
        // down to 0.25 for a Level 999 character", and PartySizeFactor "equates to 1.00, 0.96,
        // 0.92, or 0.88 for a 1-, 2-, 3-, or 4-character party".
        //
        // ChestExp is linear in both, so feeding it a disarm chance of 1000 against a monster of
        // guild level 2 makes the base exactly 1000 and the answer reads off as the factor in
        // thousandths.
        Console.WriteLine("\nexperience factors (Experience_Awarded_Formula's own quoted decimals)");
        foreach (var (level, want) in new[] { (1, 997L), (333, 500L), (999, 250L) })
            Check($"guild level factor at {level}", Rules.ChestExp(1000, 2, level), want);
        foreach (var (size, want) in new[] { (1, 1000L), (2, 960L), (3, 920L), (4, 880L) })
            Check($"party size factor at {size}", Rules.ChestExp(1000, 2, 0, size), want);

        // The Extra_Swings page tabulates the guild level at which each extra swing arrives:
        //
        //     Ninja      56  122  267  585
        //     Warrior    91  324   -    -
        //     Villain   105  434   -    -
        //     Paladin   111  484   -    -
        //     Scavenger 116  531   -    -
        //
        // Both sides of each are checked. The threshold in FRMCTYG.BAS:184 is declared As Integer,
        // so every assignment rounds half to even, and dropping that rounding moves every one of
        // these a level or two early - Ninja at 55, Warrior at 90. A check that only asked whether
        // a Ninja 56 had the swing would pass either way; asking whether a Ninja 55 does not is
        // what catches it.
        Console.WriteLine("\nextra swings (Extra_Swings: the guild level each one arrives at)");
        foreach (var (guild, at) in new[]
                 {
                     ("Ninja",     new[] {  56, 122, 267, 585 }),
                     ("Warrior",   new[] {  91, 324 }),
                     ("Villain",   new[] { 105, 434 }),
                     ("Paladin",   new[] { 111, 484 }),
                     ("Scavenger", new[] { 116, 531 }),
                 })
        {
            var got = new List<string>();
            for (int i = 0; i < at.Length; i++)
            {
                got.Add($"{SwingsAt(guild, at[i])}/{SwingsAt(guild, at[i] - 1)}");
            }
            // at the quoted level the count is i+1, one level earlier it is still i
            string want = string.Join(" ", Enumerable.Range(0, at.Length).Select(i => $"{i + 1}/{i}"));
            Check($"{guild} at {string.Join(", ", at)}", string.Join(" ", got), want);
        }

        // The whole of the community's A/D tables, every level of the five guilds they completed.
        // This is the largest published body of numbers the port can be held to - 334 values - and
        // it is sensitive to two details that are easy to lose: the per-guild accumulator is a
        // Single, and the figure shown is its whole part, not a rounding of it. Getting either one
        // wrong costs values here, and getting the pair wrong in opposite directions can still
        // score 89%, which is how it sat before.
        Console.WriteLine("\nguild A/D (A-D_For_all_Guild_Levels, every published level)");
        int adOk = 0, adAll = 0;
        foreach (var (guild, table) in Rules.GuildTables)
            foreach (string entry in table.Split(' '))
            {
                var p = entry.Split(':'); var ad = p[0].Split('/');
                var (a, d) = Rules.GuildAD(guild, int.Parse(p[1]));
                adAll++;
                if (a + Rules.BaseAD == int.Parse(ad[0]) && d + Rules.BaseAD == int.Parse(ad[1])) adOk++;
            }
        Check("published A/D values reproduced", $"{adOk} of {adAll}", $"{adAll} of {adAll}");
        CheckAtLeast("published A/D values compared", adAll, 334);

        // The Max_A-D page gives the highest A/D each guild ever grants and the level it arrives
        // at. That is the accumulation at full stretch - hundreds of awards of a Single - so it is
        // the sharpest test there is of the precision the A/D tables turned out to depend on.
        Console.WriteLine("\nmaximum guild A/D (Max_A-D: the most each guild grants, and where)");
        foreach (var (guild, att, def, level) in new[]
                 {
                     ("Nomad",     175, 175, 197), ("Warrior",   254, 276, 450),
                     ("Paladin",   223, 258, 430), ("Ninja",     254, 254, 457),
                     ("Villain",   243, 272, 433), ("Seeker",    208, 251, 428),
                     ("Thief",     168, 204, 394), ("Scavenger", 196, 222, 522),
                     ("Mage",      102, 194, 475), ("Sorcerer",   98, 195, 467),
                     ("Wizard",    106, 232, 622), ("Healer",    124, 200, 470),
                 })
        {
            var (a, d) = Rules.GuildAD(guild, level);
            Check($"{guild} at {level}", $"{a + Rules.BaseAD}/{d + Rules.BaseAD}", $"{att}/{def}");
        }

        // The same page works an example of how two guilds combine: "The game will use whichever
        // Attack or Defense score is highest from each of your guilds, so if you have a Thief
        // 394/Nomad 197, your Guild A/D will be 175/204." The attack comes from the Nomad and the
        // defence from the Thief, which is the rule worth checking - they are taken independently,
        // not as a package from whichever guild is better overall.
        Console.WriteLine("\ntwo guilds at once (Max_A-D: a Thief 394 with a Nomad 197 is 175/204)");
        var both = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                    new[] { 10, 10, 10, 10, 10, 10 });
        both.Guilds.Clear();
        both.Guilds.Add(new GuildMembership { Guild = "Thief", Level = 394 });
        both.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 197 });
        var (ba, bd) = Rules.BestGuildAD(both);
        Check("Thief 394 + Nomad 197", $"{ba + Rules.BaseAD}/{bd + Rules.BaseAD}", "175/204");

        // Complications on a failed raise. The page states the formula and then works three cases:
        // "a 16-year-old Human will have a 32% chance of complications on a failed raise... A
        // 16-year-old Elf has only an 8% chance of complications on a failed raise; a Dwarf, 11.6%."
        // Those also test the race table's lifespans - 100, 400 and 275 - which nothing else does.
        Console.WriteLine("\nraise complications (Chance_of_Complications_Formula, at 16 years old)");
        foreach (var (race, want) in new[] { ("Human", 32.0), ("Elf", 8.0), ("Dwarf", 11.6) })
            Check($"{race} at 16", Math.Round(Raising.ComplicationChance(16 * 365, RaceData.ByName(race).MaxAge), 1), want);

        // And the Constitution it can cost: "There's a (50 - (Con * 2)) percent chance of losing a
        // point of Constitution after a successful raise... Max Con 24 always have at least a 2%
        // chance; Humans and Gnomes, 4%; Elves, 8%. Only Ogres (Max Con 27) have a 0% chance."
        // The page's own worked values, read back through the rule.
        Console.WriteLine("\nconstitution lost on a raise (the page's quoted percentages)");
        foreach (var (con, want) in new[] { (24, 2), (23, 4), (21, 8), (27, 0), (25, 0) })
            Check($"CON {con}", Rules.RaiseConLossChance(con), want);

        // Questing_Formula is a list of parameters rather than a formula, and most of them are
        // checkable directly. The tier a target is drawn from, the squeeze an item quest puts on
        // it, and the floor of level 5 below which nobody is ever quested.
        Console.WriteLine("\nquesting (Questing_Formula: the tier, the item band, and the level 5 floor)");
        foreach (var (level, want) in new[] { (5, 1), (12, 1), (13, 1), (25, 1), (100, 4), (250, 10), (999, 10) })
            Check($"tier at guild level {level}", Rules.QuestTier(level), want);
        // "you will never be quested for items from below Level 7" - Int(10 / 1.5) + 1
        Check("deepest item anyone is sent for", Rules.QuestItemBand(10), 7);
        Check("item band at tier 1", Rules.QuestItemBand(1), 1);

        // "Characters below guild level 5 will never be quested" - FRMCTYG.BAS:461 forces the roll
        // to 100, which no guild's questing percentage can beat. Checked against the most generous
        // guild there is, so it is the floor being tested and not a stingy table.
        var keenest = GuildData.All.OrderByDescending(g => g.QuestPercent).First();
        int quested = 0;
        var qrng = new Random(1);
        for (int i = 0; i < 2000; i++) if (Rules.RollQuest(keenest, 4, qrng).Type != 0) quested++;
        Check($"quests for a {keenest.Name} 4 in 2000 tries", quested, 0);

        // `Rnd * n` straight into a VB Integer rounds, and the store and the pens both draw that
        // way (MAINMOD.BAS:1337 and :1598). Index 0 owns only the values below 0.5 and index n is
        // reachable though it matches nothing, so over a fair number of draws index 0 should come
        // up about half as often as its neighbours and about the same share should fall off the
        // end. Reaching for a plain random index evens both of those out, which is the mistake this
        // guards. Wide bounds: the point is the shape, not a particular sample.
        Console.WriteLine("\nrounded index draws (MAINMOD.BAS:1337, 1598 - not a uniform 0..n-1)");
        {
            const int n = 24, draws = 200_000;
            var r = new Random(7);
            int zeroes = 0, past = 0, one = 0;
            for (int i = 0; i < draws; i++)
            {
                int k = Rules.RoundedIndex(r, n);
                if (k == 0) zeroes++; else if (k >= n) past++; else if (k == 1) one++;
            }
            CheckNear("index 0, per 200k draws", zeroes, draws / n / 2 * 8 / 10, draws / n / 2 * 12 / 10);
            CheckNear("index 1, per 200k draws", one, draws / n * 8 / 10, draws / n * 12 / 10);
            // The halving only matters if index 0 has anything in it. It does, in both places the
            // draw is used, so the distribution above is a statement about real items rather than
            // an empty slot: item type 0 is the Hands, of which six can reach a shelf (the seventh,
            // Gnarled Hands, is cursed), and monster kind 0 is the Humanoids.
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            int shelvable = wares.Count(i => i.Id != 0 && (int)i.Type == 0 && i.Floor >= 1
                                          && i.Cursed == 0);
            Check("items of type 0 the store can shelve", shelvable, 6);
            var creatures = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            CheckAtLeast("monsters of kind 0 the pens can hold",
                         creatures.Count(m => m.Id != 0 && (int)m.Type == 0 && m.LevelFound >= 1), 40);

            CheckNear("past the end, per 200k draws", past, draws / n / 2 * 8 / 10, draws / n / 2 * 12 / 10);
        }

        // Resistance comes off that damage as `l0432 = l0432 * (1 - (Resistances(i) / 100))` with
        // `l0432 As Integer` (MONSMOD.BAS:2469, 2520), so the step rounds rather than truncating -
        // half to even, as every VB Integer assignment does. Firebolt at SL 15 lands on 26 with the
        // roll at zero, so a 25% resistance puts it exactly on 19.5, which is the midpoint case and
        // goes to 20; a 35% resistance gives 16.9, which truncation would report as 16.
        Console.WriteLine("\nresistance against spell damage (MONSMOD.BAS:2520 rounds, it does not truncate)");
        Check("Firebolt 26 against 25% resistance", Rules.SpellDamage(new ZeroRandom(), 10, 1, 15, 25), 20);
        Check("Firebolt 26 against 35% resistance", Rules.SpellDamage(new ZeroRandom(), 10, 1, 15, 35), 17);
        Check("Firebolt 26 against no resistance", Rules.SpellDamage(new ZeroRandom(), 10, 1, 15, 0), 26);

        // Deep_Item_Dropping_Monsters tabulates every monster whose items come from deeper than it
        // spawns, with both levels. That is two MDATA5 fields per row, read back against a list
        // somebody compiled by playing, and it is the only check the port has on ItemDropLevel at
        // all - the drop probes all sample from it rather than testing it.
        Console.WriteLine("\ndeep item drops (Deep_Item_Dropping_Monsters: spawn level / drop level)");
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        foreach (var (name, spawn, drop) in new[]
                 {
                     ("Aboleth", 1, 2), ("Pseudo-Dragon", 1, 2), ("Slave Masher", 1, 2), ("Tengu", 1, 2),
                     ("Slave Driver", 2, 3), ("Cockatrice", 5, 6), ("Twisted Elf", 5, 6),
                     ("Minotaur", 6, 8), ("Butcher", 7, 8), ("Demonist", 7, 8), ("Hooded Thief", 7, 8),
                     ("Asteryex", 10, 14), ("Giant King", 10, 12), ("Mother of Serpents", 12, 14),
                     ("Dark Shape", 13, 14),
                 })
        {
            var m = mons.FirstOrDefault(x => x.Name.Trim() == name);
            if (m == null) { Console.WriteLine($"  FAIL  {name,-52} not in MDATA5"); _failed++; continue; }
            Check(name, $"{m.LevelFound}/{m.ItemDropLevel}", $"{spawn}/{drop}");
        }

        // List_of_Uncharmable_Monsters_That_Can_Join names the monsters that cannot be charmed but
        // may still offer to join, and states the criterion: "either Charm Resistant, Resistant to
        // All Magic, or have 100% Magic Resistance ... They are, however, either Good or Neutral".
        //
        // Every term of that is a separate reading of MDATA5, so deriving the set tests four at
        // once: SpecialPropertyFlags bit 3 is Charm Resistant and bit 2 Resistant to All Magic
        // (gv08DC$'s own names), resistance index 6 is Magic (gv0620$), and AlignmentValue 2 is
        // Evil. Get any one wrong and the set moves.
        //
        // The page lists 45 and this derives 44, with nothing spurious and nothing else missing.
        //
        // Where the expected list came from, because it matters: it was taken from this port's own
        // output and then diffed against the page, which agreed on all 44. So what follows is a
        // regression check against a verified snapshot, not an independent reading of the page -
        // it will catch the derivation changing, and it would not catch the page having been
        // transcribed wrongly in the first place. The diff is the evidence; this holds it.
        // The odd one out is Aboleth, which the page carries with its own note - "normally
        // charmable, but never appears outside of its anti-magic lair" - so it is on their list for
        // a reason other than the stated criterion, and it is right that this does not derive it.
        Console.WriteLine("\nuncharmable but joinable (List_of_Uncharmable_Monsters_That_Can_Join)");
        {
            var everyMonster = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var derived = everyMonster
                .Where(m => m.Id != 0 && m.Hits > 0
                         && ((m.SpecialPropertyFlags & (1 << 3)) != 0
                          || (m.SpecialPropertyFlags & (1 << 2)) != 0
                          || (m.Resistances.Length > 6 && m.Resistances[6] >= 100))
                         && m.AlignmentValue != 2)
                .Select(m => m.Name.Trim())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            var listed = new[]
            {
                "Asteryex", "Black Gremlin", "Blue Gremlin", "Destroyer of the Deep", 
                "Dragon King", "Dragon Queen", "Dragon Turtle", "Drey Dragon", "Dryad", 
                "Dwarven Lord", "Earth Golemn", "Elven Lord", "Giant King", "Great White", 
                "Green Gremlin", "Griffin", "Guardsman", "Gythyek Lizard", "Hooded Thief", 
                "Indigo Gremlin", "Jubilex", "Lamurian High Priestess", "Lamurian Lord", 
                "Lamurian Mage", "Morey Bilk", "Mother of Serpents", "Numanog", "Obsidian Golemn", 
                "Orange Gremlin", "Pariah", "Pegan Lizard", "Phoenix", "Purple Gremlin", 
                "Red Gremlin", "Sentinal", "Slippery Mire", "Spell Binder", "T'Lanea", "Tazen", 
                "Twisted Elf", "Violet Fungus", "Warlord", "Wil 'O Wisp", "Yellow Gremlin",
            };
            Check("monsters the criterion picks out", derived.Count, listed.Length);
            Check("names the data and the page disagree on",
                  string.Join(", ", derived.Except(listed).Concat(listed.Except(derived))), "");
        }

        // Laired_Monster_List tabulates where every lair is. A lair is not a flag on a square:
        // MDATA11 gives each square an area id and each area a LairId, so a name on that page has
        // to survive square -> area -> monster, and all three readings have to be right.
        //
        // The expected list below is this port's own output, diffed against the page when it was
        // written: 80 of the 81 names matched, and the two that did not are explained underneath.
        // That makes this a regression check against a verified snapshot rather than a reading of
        // the page itself - and it is the port's list, so it contains Vomitis Goo and not
        // Destroyer of the Deep.
        //
        // The page names 81 distinct monsters and the data holds 81. Eighty are the same, and both
        // differences are accounted for:
        //
        //   Destroyer of the Deep is on their list at 13,28,15 and lairs nowhere. It is the
        //   Lamurian High Priestess's companion - her CompanionId is its id, asserted below - and
        //   she lairs at that square, so the page recorded where you meet it. Their own
        //   uncharmable-monsters page says it "will only spawn as a companion to the Lamurian High
        //   Priestess".
        //
        //   Vomitis Goo lairs on level 2 and is not on their list, with nothing in the data to
        //   explain it. That one looks like an omission on the page.
        Console.WriteLine("\nlairs (Laired_Monster_List, by way of MDATA11's areas)");
        {
            var floors = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var lairs = new HashSet<string>();
            foreach (var fl in floors)
                foreach (var a in fl.Areas)
                    if (a.LairId > 0 && beasts.FirstOrDefault(m => m.Id == a.LairId) is Monster lm)
                        lairs.Add(lm.Name.Trim());
            var listed = new[]
            {
                "Aboleth", "Arch-Fiend", "Arthropod", "Assassin", "Black Pelagon", "Black Yundrun", 
                "Bushido", "Butcher", "Clean-Up", "Cockatrice", "Defender", "Demonist", 
                "Diseased Muck", "Displacer", "Drakul", "Dwarven Guard", "Dwarven Lord", 
                "Earth Golemn", "Eidolan Phantasm", "Element Mage", "Filcher", "Fire Lord", 
                "Flame Dragon", "Genie", "Giant Ant", "Giant King", "Giant Wasp", "Gladiator", 
                "Goblin", "Goblin Guard", "Goblin Lord", "Goblin Shaman", "Gold Pelagon", 
                "Golden Centaur", "Gredlan Footpad", "Harpy", "Holy Warrior", "Hooded Thief", 
                "Ice Demon", "Illusionist", "Kei-Shuma", "Lamurian High Priestess", 
                "Lamurian Lord", "Lamurian Mage", "Large Orc", "Lava Giant", "Mage", "Medusa", 
                "Meeshi", "Mengu-Ogre", "Mercenary", "Minotaur", "Mother of Serpents", 
                "Night Dragon", "Obsidian Golemn", "Ogre", "Piranha", "Poltergeist", "Que'ssus", 
                "Robber", "Scylla", "Shadow Serpent", "Shadow of Death", "Sharper", 
                "Shralar Thief", "Silent Eagle", "Slave Driver", "Slave Masher", "Slaver", 
                "Snake Servant", "Sun Dragon", "Twisted Dwarf", "Twisted Elf", "Vampire", "Veyasu", 
                "Vomitis Goo", "Walking Corpse", "Warlock", "White Dragon", "Wretch", "ZBrat",
            };
            FullCheck("distinct monsters that lair", lairs.Count, listed.Length);
            FullCheck("lair names that differ",
                  string.Join(", ", lairs.Except(listed).Concat(listed.Except(lairs))), "");

            // the linkage that explains the Destroyer
            var priestess = beasts.First(m => m.Name.Trim() == "Lamurian High Priestess");
            var destroyer = beasts.First(m => m.Name.Trim() == "Destroyer of the Deep");
            Check("the Priestess's companion", priestess.CompanionId, destroyer.Id);
            Check("the Destroyer lairs anywhere", lairs.Contains("Destroyer of the Deep"), false);
        }

        // Rare_Monsters groups every monster of rarity 8 or below by that number and gives its
        // level. The page calls the groups "8% Spawn chance" and so on, and is careful to say the
        // figure is an index rather than a probability - "the exact chance being a complicated
        // function, but this serves as a nice index" - so what is being checked here is the Rarity
        // field itself (M56DB) and LevelFound, over 140 rows.
        //
        // Worth doing in C# rather than by eye: one of these names carries a byte above ASCII
        // (Gra\u00E8l Mosquito, 0xE8) and piping it through a console to compare it by hand turns
        // it into something else entirely. The data is fine; the terminal is not.
        Console.WriteLine("\nrarity (Rare_Monsters: every monster of rarity 8 or below, with its level)");
        {
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"))
                                  .Where(m => m.Id != 0 && m.Hits > 0).ToList();
            var listed = new (int Rarity, string Name, int Level)[]
            {
                (1, "Black Gremlin", 16),
                (1, "Dark Shape", 13),
                (1, "Goblin Guard", 3),
                (1, "Goblin Shaman", 3),
                (1, "Griffin", 11),
                (1, "Medusa", 6),
                (1, "Poltergeist", 6),
                (1, "Shadow Devil", 13),
                (1, "Tengu", 1),
                (1, "ZBrat", 6),
                (2, "Asteryex", 10),
                (2, "Daemon", 3),
                (2, "Dryad", 5),
                (2, "Gibbering Yethwa", 7),
                (2, "Lamurian Lord", 15),
                (2, "Pariah", 13),
                (2, "Rabid Snook", 6),
                (2, "Shadow Ogre", 12),
                (2, "Slaver", 2),
                (2, "Slither", 15),
                (2, "Troll Demon", 5),
                (2, "Twisted Dwarf", 1),
                (3, "Angel of Death", 14),
                (3, "Arch-Fiend", 12),
                (3, "Black Pelagon", 6),
                (3, "Champion", 6),
                (3, "Charon", 15),
                (3, "Cockatrice", 5),
                (3, "Cutthroat", 2),
                (3, "Demon Prince", 16),
                (3, "Dragon King", 15),
                (3, "Dragon Turtle", 14),
                (3, "Drey Dragon", 8),
                (3, "Element Mage", 4),
                (3, "Elven Lord", 13),
                (3, "Evil Spirit", 6),
                (3, "Giant Leech", 4),
                (3, "Giant Silverfish", 10),
                (3, "Giant Slug", 3),
                (3, "Golden Eagle", 14),
                (3, "Gorguni", 11),
                (3, "Gra\u00E8l Mosquito", 3),
                (3, "Guardian", 11),
                (3, "Hydra", 4),
                (3, "Jubilex", 7),
                (3, "Lynch Demon", 4),
                (3, "Mind Twister", 15),
                (3, "Minion of Death", 12),
                (3, "Morey Bilk", 7),
                (3, "Morgul", 4),
                (3, "Pegan Lizard", 5),
                (3, "Reaper of Souls", 16),
                (3, "Red-Cloaked Thief", 12),
                (3, "Rot Grub", 9),
                (3, "Shadow of Death", 14),
                (3, "Silent Eagle", 11),
                (3, "Slave Masher", 1),
                (3, "Slave", 1),
                (3, "Slippery Mire", 7),
                (3, "Snake Servant", 6),
                (3, "Stalker", 8),
                (3, "Stone Golemn", 8),
                (3, "Swashbuckler", 3),
                (3, "Vampire Lord", 15),
                (3, "Walking Corpse", 4),
                (3, "Water Watcher", 10),
                (3, "Wil 'O Wisp", 8),
                (4, "Dungeonweed", 2),
                (4, "Shadow Demon", 11),
                (4, "Shadow Golem", 14),
                (4, "Shadow Serpent", 11),
                (4, "Strangleweed", 5),
                (5, "Cyclops", 6),
                (5, "Disciple", 2),
                (5, "NaLanea", 8),
                (5, "Piranha", 11),
                (5, "Pit Viper", 4),
                (5, "Shadow Dragon", 12),
                (5, "T'Lanea", 16),
                (6, "Master Ninja", 11),
                (6, "Vampire", 11),
                (7, "Astral Demon", 7),
                (7, "Blue Gremlin", 10),
                (7, "Green Gremlin", 8),
                (7, "Indigo Gremlin", 14),
                (7, "Magi Lord", 15),
                (7, "Orange Gremlin", 4),
                (7, "Purple Gremlin", 12),
                (7, "Red Cloak Assassin", 15),
                (7, "Red Gremlin", 2),
                (7, "Yellow Gremlin", 6),
                (8, "Air Elemental", 4),
                (8, "Black Mist", 14),
                (8, "Bushart", 5),
                (8, "Carrion Crawler", 4),
                (8, "Cave Bear", 2),
                (8, "Coltpixie", 12),
                (8, "Corpser", 9),
                (8, "Coutal", 13),
                (8, "Diseased Muck", 3),
                (8, "Doppleganger", 6),
                (8, "Dragon Queen", 15),
                (8, "Drakul", 7),
                (8, "Eidolan Phantasm", 8),
                (8, "Emerald Golemn", 13),
                (8, "Fire Ant", 4),
                (8, "Fire Beetle", 6),
                (8, "Fire Centipede", 5),
                (8, "Gargantuan", 14),
                (8, "Giant Beetle", 3),
                (8, "Giant Scorpion", 4),
                (8, "Giant Wolverine", 6),
                (8, "Great White", 15),
                (8, "Gythyek Lizard", 8),
                (8, "Harpy", 12),
                (8, "Hobgoblin", 6),
                (8, "Horned Demon", 3),
                (8, "Large Orc", 2),
                (8, "Lost Soul", 6),
                (8, "Mammon", 10),
                (8, "Master of Shadows", 15),
                (8, "Mengu-Ogre", 13),
                (8, "Mummy", 2),
                (8, "Night Dragon", 3),
                (8, "Ninja", 8),
                (8, "Phoenix", 16),
                (8, "Pickpocket", 3),
                (8, "Pixie", 8),
                (8, "Pseudo-Dragon", 1),
                (8, "Razor Boar", 2),
                (8, "Rust Monster", 15),
                (8, "Scylla", 3),
                (8, "Sentinal", 8),
                (8, "Shadow Ninja", 16),
                (8, "Silver Bear", 7),
                (8, "Silver Dragon", 5),
                (8, "Siren", 9),
                (8, "Violet Fungus", 9),
                (8, "Vomitis Goo", 2),
                (8, "Warlock", 7),
            };
            int matched = 0;
            var wrong = new List<string>();
            foreach (var row in listed)
            {
                var m = beasts.FirstOrDefault(x => x.Name.Trim() == row.Name);
                if (m == null) { wrong.Add($"{row.Name} (absent)"); continue; }
                if (m.Chance == row.Rarity && m.LevelFound == row.Level) { matched++; continue; }
                wrong.Add($"{row.Name} rarity {m.Chance}/{row.Rarity} level {m.LevelFound}/{row.Level}");
            }
            Check("rows that match the page", matched, listed.Length);
            Check("rows that do not", string.Join("; ", wrong.Take(4)), "");

            // and the grouping is exhaustive: nothing else in the data carries a rarity of 8 or less
            int lowRarity = beasts.Count(m => m.Chance >= 1 && m.Chance <= 8);
            Check("monsters of rarity 1..8 in the data", lowRarity, listed.Length);
        }

        // Monster_Trap_Table gives every monster a YES or NO for each of the eleven chest traps,
        // with its id and floor. The page's column order - UNTR PSON XPLD DISE FIRE FEAR TPRT WTHR
        // BLKT SLME FATE - is *not* gv08AA$'s index order, so the table is an independent reading
        // of which TrapFlags bit means which trap. Taking the page's columns as the bit order
        // instead fits only 151 of the 400 rows; the mapping below fits all of them.
        //
        // Each entry is id:floor:flags, the flags being what the page's row implies. Joining on
        // floor as well as id matters for exactly one pair - see the duplicate below.
        Console.WriteLine("\nchest traps by monster (Monster_Trap_Table, 400 rows)");
        {
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            string packed =
                "1:1:31 2:1:31 3:1:15 4:1:1 5:2:31 6:3:543 7:3:14 8:4:527 9:4:1163 10:5:1163 11:5:1163 12:5:699 " +
                "13:6:1807 14:6:1152 15:6:535 16:7:1551 17:7:1551 18:8:1983 19:8:671 20:9:1023 21:9:1951 22:1:1 " +
                "23:1:2 24:2:1 25:2:1 26:4:1 27:6:1 28:7:1 29:7:1 30:8:1 31:9:1 32:2:15 33:3:527 34:4:143 " +
                "35:4:1983 36:5:531 37:6:1679 38:7:1979 39:8:1967 40:9:927 41:1:31 42:2:27 43:3:31 44:4:159 " +
                "45:5:671 46:6:1951 47:6:1851 48:7:827 49:8:827 50:2:1 51:4:1 52:5:1 53:6:1 54:7:1 55:8:256 " +
                "56:9:519 57:1:1 58:2:1 59:3:1 60:4:1 61:5:1 62:6:1 63:6:1 64:6:1727 65:7:1567 66:7:1567 " +
                "67:8:1567 68:8:1567 69:1:31 70:3:31 71:4:671 72:4:31 73:5:671 74:6:927 75:6:703 76:7:827 " +
                "77:8:2047 78:9:2047 79:1:1 80:1:1 81:1:31 82:2:31 83:2:31 84:2:31 85:2:31 86:3:31 87:3:31 " +
                "88:4:31 89:4:31 90:4:31 91:4:31 92:5:31 93:5:31 94:5:31 95:6:31 96:6:31 97:7:31 98:7:31 99:8:31 " +
                "100:8:31 101:9:31 102:1:1 103:1:1 104:2:1 105:3:1 106:3:1 107:4:1 108:4:1 109:4:1 110:5:1 " +
                "111:6:1 112:6:1 113:7:1 114:8:1 115:8:1 116:9:1 117:1:31 118:1:31 119:1:15 120:2:15 121:2:15 " +
                "122:3:15 123:3:527 124:4:911 125:4:527 126:5:911 127:6:1551 128:7:1935 129:7:1551 130:8:951 " +
                "131:1:1 132:1:31 133:1:31 134:2:31 135:3:31 136:4:31 137:5:415 138:6:31 139:7:319 140:8:319 " +
                "141:1:29 142:2:31 143:3:799 144:5:671 145:5:543 146:7:1695 147:8:1695 148:8:1695 149:9:1727 " +
                "150:3:31 151:4:31 152:5:31 153:5:31 154:7:31 155:7:31 156:8:31 157:4:31 158:5:159 159:5:159 " +
                "160:6:1567 161:7:959 162:9:959 163:1:11 164:1:31 165:2:31 166:2:31 167:3:31 168:4:543 169:5:671 " +
                "170:6:1951 171:7:959 172:7:683 173:8:959 174:1:31 175:3:31 176:5:671 177:7:1855 178:7:927 " +
                "179:8:2033 180:9:2033 181:2:31 182:2:31 183:3:31 184:4:31 185:4:31 186:5:159 187:5:1567 " +
                "188:6:415 189:6:415 190:6:1051 191:7:443 192:8:443 193:8:443 194:9:651 195:10:31 196:10:1851 " +
                "197:10:1053 198:10:415 199:10:955 200:10:31 201:10:1567 202:10:31 203:10:31 204:10:1 205:10:1 " +
                "206:10:757 207:10:1 208:10:415 209:10:319 210:10:1727 211:10:31 212:10:959 213:10:959 214:10:927 " +
                "215:10:651 216:11:982 217:11:1375 218:11:670 219:11:21 220:11:539 221:12:659 222:11:1983 " +
                "223:12:991 224:11:2047 225:11:31 226:11:1 227:11:2033 228:12:827 229:12:447 230:13:651 " +
                "231:12:927 232:12:651 233:12:31 234:12:959 235:14:951 236:12:415 237:13:1727 238:13:1 " +
                "239:13:2047 240:13:2033 241:13:651 242:13:25 243:13:1727 244:14:2047 245:13:1 246:14:2047 " +
                "247:14:2047 248:14:951 249:14:27 250:14:1 251:14:31 252:14:1 253:14:2033 254:14:959 255:14:25 " +
                "256:14:1 257:15:31 258:15:1951 259:15:1567 260:15:927 261:15:2047 262:15:2047 263:15:651 " +
                "264:15:2047 265:15:351 266:15:2033 267:15:2047 268:15:1 269:15:1 270:15:1951 271:15:1 272:15:959 " +
                "273:15:2047 274:15:651 276:16:2047 277:16:2046 278:13:1567 279:11:1567 280:15:1567 281:9:543 " +
                "282:13:2015 283:12:31 284:13:959 285:9:959 286:13:959 287:16:1983 288:1:2 289:2:31 290:2:1 " +
                "291:2:1 292:3:1 293:3:1 294:3:31 295:3:1 296:3:537 297:3:1 298:3:31 299:4:1793 300:5:31 301:4:31 " +
                "302:4:1 303:4:1 304:3:1 305:5:769 306:6:1551 307:4:799 307:16:2047 308:4:31 309:6:1 310:13:703 " +
                "311:16:2046 312:2:31 313:3:542 314:9:31 315:1:31 316:1:31 317:3:15 318:6:15 319:8:15 320:11:15 " +
                "321:13:15 322:15:47 323:16:1 324:2:31 325:1:31 326:3:1 327:6:31 328:11:31 329:5:15 330:1:31 " +
                "331:2:31 332:2:543 333:2:31 334:3:15 335:3:15 336:3:15 337:2:31 338:4:543 339:4:543 340:5:542 " +
                "341:5:670 342:11:319 343:3:1 344:3:31 345:8:31 346:16:64 347:6:31 348:7:683 349:1:15 350:7:955 " +
                "351:7:927 352:8:959 353:11:923 354:11:31 355:11:857 356:15:1883 357:10:2047 358:12:2047 " +
                "359:9:319 360:15:1023 361:13:927 362:2:25 363:4:153 364:6:153 365:8:665 366:10:921 367:12:953 " +
                "368:14:1017 369:5:415 370:2:31 371:2:31 372:1:31 373:3:31 374:11:959 375:7:31 376:8:959 377:3:15 " +
                "378:2:31 379:13:65 380:14:2015 381:14:2015 382:15:2015 383:15:2015 384:16:2015 385:15:2015 " +
                "386:9:319 387:16:1023 388:16:1017 389:11:1983 390:13:827 391:12:447 392:11:1567 393:12:2047 " +
                "394:15:2047 395:14:1727 396:7:15 397:13:2015 398:11:33 399:6:543 400:11:2047"
            ;
            int exact = 0, untrappedOnly = 0, differ = 0, absent = 0;
            foreach (string entry in packed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = entry.Split(':');
                int id = int.Parse(bits[0]), floor = int.Parse(bits[1]), want = int.Parse(bits[2]);
                var m = beasts.FirstOrDefault(x => x.Id == id && x.LevelFound == floor);
                if (m == null) { absent++; continue; }
                if (m.TrapFlags == want) exact++;
                // A monster with no trap bits at all can only ever hand over an untrapped chest,
                // which is what the page's UNTR column records for it.
                else if (m.TrapFlags == 0 && want == 1) untrappedOnly++;
                else differ++;
            }
            Check("rows with no matching monster", absent, 0);
            Check("rows where the flags differ", differ, 0);
            Check("rows that match bit for bit", exact, 341);
            Check("rows that are untrapped-only", untrappedOnly, 59);
        }

        // MDATA5 ships one duplicated monster id, and it is worth knowing about: 307 belongs to
        // both Morgul on floor 4 and Shadow Ninja on floor 16. Anything that resolves a monster by
        // id - a lair, a companion, a quest target - finds Morgul, because it comes first.
        //
        // Nothing in the shipped data points a LairId or a CompanionId at 307, so this has no
        // effect anybody can observe. It is asserted because it is a real oddity and because a
        // join on monster id is wrong without it - which is how it was found.
        //
        // It does *not* explain the community's "you will never meet Shadow Ninjas": fn0D4D builds
        // its pool from monster records rather than ids, so a shared id cannot hide anything from
        // the spawn path. Shadow Ninja is reachable from thirty Stud squares on floor 15, by the
        // same route that reaches Asmodeus. See Mordor.Tools shadow, and docs/FIDELITY.md.
        Console.WriteLine("\nthe duplicated monster id (MDATA5)");
        {
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"))
                                  .Where(m => m.Id != 0).ToList();
            var dupes = beasts.GroupBy(m => m.Id).Where(g => g.Count() > 1)
                              .OrderBy(g => g.Key).ToList();
            Check("ids used by more than one monster", dupes.Count, 1);
            Check("which id", dupes.Count > 0 ? dupes[0].Key : -1, 307);
            Check("the monsters sharing it",
                  string.Join(", ", dupes.SelectMany(g => g).Select(m => $"{m.Name.Trim()} ({m.LevelFound})")),
                  "Morgul (4), Shadow Ninja (16)");
            Check("which one a lookup by id finds",
                  beasts.First(m => m.Id == 307).Name.Trim(), "Morgul");
        }

        // What actually keeps the deepest monsters rare. The dungeon is fifteen floors and ten
        // monsters carry a LevelFound of 16, so they fall outside every encounter window except on
        // a Stud square, where the target is one floor deeper (MODULE4.BAS:1426-1495). Asmodeus is
        // among them, which is the point - that Stud on floor 15 is how the game's last fight is
        // reached, and the same door lets the other nine through.
        Console.WriteLine("\nthe deepest monsters (a 15-floor dungeon with LevelFound 16 in it)");
        {
            var floors = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"))
                                  .Where(m => m.Id != 0).ToList();
            FullCheck("floors in the dungeon", floors.Count, 15);
            var past = beasts.Where(m => m.LevelFound > floors.Count).Select(m => m.Name.Trim()).ToList();
            FullCheck("monsters deeper than the dungeon goes", past.Count, 10);
            Check("Asmodeus is one of them", past.Contains("Asmodeus"), true);
            Check("so is Shadow Ninja", past.Contains("Shadow Ninja"), true);

            // and they really are reachable, from a Stud on the deepest floor and nowhere else
            int Reachable(string name, bool studs)
            {
                var m = beasts.First(x => x.Name.Trim() == name);
                int n = 0;
                for (int lv = 1; lv <= floors.Count; lv++)
                {
                    var f = floors[lv - 1];
                    for (int y = 0; y < f.Height; y++)
                        for (int x = 0; x < f.Width; x++)
                        {
                            if (f.AreaAt(x, y) is not Floor.Area a || a.SpawnMask == 0) continue;
                            if ((a.SpawnMask & (1 << (int)m.Kind)) == 0) continue;
                            bool stud = f.Has(x, y, Floor.STUD);
                            if (stud != studs) continue;
                            if (m.LevelFound > Rules.EncounterFloor(lv, stud)) continue;
                            n++;
                        }
                }
                return n;
            }
            Check("squares that can draw Asmodeus without a Stud", Reachable("Asmodeus", false), 0);
            FullCheck("Stud squares that can draw Asmodeus", Reachable("Asmodeus", true), 18);
            Check("squares that can draw a Shadow Ninja without a Stud", Reachable("Shadow Ninja", false), 0);
            FullCheck("Stud squares that can draw a Shadow Ninja", Reachable("Shadow Ninja", true), 30);
        }

        // The community's Races page carries three tables - statistics, resistances and sizes -
        // covering every race. Between them that is the whole of what MDATA7 says about a race:
        // lifespan, the experience factor, six stat ranges, which alignments it allows, its size
        // and eleven resistances. The resistance columns are in gv0620$'s order, so index 6 there
        // is Magic exactly as it is everywhere else in the port.
        //
        // 261 values, and the only check on most of these fields anywhere - the complications page
        // tests three lifespans and nothing tests the rest.
        Console.WriteLine("\nthe race tables (Races: statistics, resistances and sizes)");
        {
            var listed = new (string Name, int Age, int Xp, string Bounds, string Align, string Size, string Res)[]
            {
                ("Human", 100, 3, "4,17,4,18,4,18,6,17,5,18,6,18", "XXX", "Normal", "0,0,0,0,0,0,0,0,0,0,0"),
                ("Elf", 400, 7, "3,15,7,20,7,20,3,16,3,18,3,18", "XXX", "Small", "0,0,0,50,0,0,80,0,0,0,0"),
                ("Giant", 225, 7, "12,25,4,17,3,17,9,19,2,16,3,18", "-X-", "VeryBig", "50,50,50,0,50,50,0,25,50,50,0"),
                ("Gnome", 300, 7, "4,17,7,19,5,19,3,17,9,22,6,18", "XXX", "Normal", "0,0,0,40,0,0,0,0,0,0,0"),
                ("Dwarf", 275, 5, "3,18,3,18,7,19,3,19,3,17,5,18", "-X-", "Small", "45,80,0,0,50,50,0,40,50,90,0"),
                ("Ogre", 265, 6, "9,20,3,16,3,16,9,21,3,18,5,17", "XXX", "Big", "75,75,0,0,50,0,0,50,75,75,15"),
                ("Morloch", 175, 4, "6,20,3,17,5,19,3,15,2,14,5,20", "X-X", "Normal", "35,35,35,50,0,0,40,0,95,65,50"),
                ("Osiri", 325, 8, "5,17,3,18,3,17,7,19,3,18,10,22", "-X-", "Normal", "0,0,90,90,0,0,0,90,0,0,90"),
                ("Troll", 285, 9, "6,20,3,18,3,18,6,19,3,17,6,20", "XXX", "Normal", "0,0,0,0,45,45,0,0,45,65,25"),
            };
            int ok = 0;
            var wrong = new List<string>();
            foreach (var row in listed)
            {
                var r = RaceData.All.FirstOrDefault(x => x.Name == row.Name);
                if (r == null) { wrong.Add($"{row.Name} absent"); continue; }
                string bounds = string.Join(",", Enumerable.Range(0, 6).SelectMany(i => new[] { r.Min[i], r.Max[i] }));
                string align = string.Join("", Enumerable.Range(0, 3).Select(b => (r.AlignMask & (1 << b)) != 0 ? "X" : "-"));
                string res = string.Join(",", r.Resistances);
                if (r.MaxAge != row.Age) wrong.Add($"{row.Name} age {r.MaxAge}/{row.Age}");
                else if (r.ExpFactor != row.Xp) wrong.Add($"{row.Name} xp {r.ExpFactor}/{row.Xp}");
                else if (bounds != row.Bounds) wrong.Add($"{row.Name} bounds {bounds}");
                else if (align != row.Align) wrong.Add($"{row.Name} align {align}/{row.Align}");
                else if (r.Size.ToString() != row.Size) wrong.Add($"{row.Name} size {r.Size}/{row.Size}");
                else if (res != row.Res) wrong.Add($"{row.Name} resistances {res}");
                else ok++;
            }
            Check("races that match the page in every field", ok, listed.Length);
            Check("races that do not", string.Join("; ", wrong.Take(3)), "");

            // "through the life of a character, you can only improve a stat by 5 points above the
            // listed maximum" - MAINMOD2.BAS:6207 clamps to Races(r).M553C(stat) + 5, and ItemUse
            // is where the port raises a stat.
            Check("the ceiling a stat can be raised to", RaceData.ByName("Human").Max[0] + 5, 22);
        }

        // The same page's Race / Guild Chart: which of the twelve guilds each race may enter, as a
        // grid of 108 marks. That is GuildDef.RaceMask read back through AllowsRace, and it is what
        // decides whether a character can be made at all - nothing else in the suite touches it.
        //
        // Like the alignment column, the marks are positional with blanks, so the expected side
        // here was parsed from the page by column offset rather than by splitting on whitespace.
        Console.WriteLine("\nwhich guilds each race may enter (Races: the Race / Guild Chart)");
        {
            var chart = new (string Race, string Guilds)[]
            {
                ("Human", "Nomad,Warrior,Paladin,Ninja,Villain,Seeker,Thief,Scavenger,Mage,Sorcerer,Wizard,Healer"),
                ("Elf", "Nomad,Paladin,Seeker,Thief,Scavenger,Mage,Sorcerer,Wizard,Healer"),
                ("Giant", "Nomad,Warrior,Seeker"),
                ("Gnome", "Nomad,Paladin,Villain,Seeker,Thief,Scavenger,Mage,Sorcerer,Healer"),
                ("Dwarf", "Nomad,Warrior,Seeker,Thief,Scavenger,Sorcerer,Wizard,Healer"),
                ("Ogre", "Nomad,Warrior,Scavenger"),
                ("Morloch", "Nomad,Warrior,Villain,Seeker,Scavenger,Mage,Sorcerer"),
                ("Osiri", "Nomad,Seeker,Thief,Scavenger,Sorcerer,Wizard,Healer"),
                ("Troll", "Nomad,Ninja,Villain,Thief,Scavenger"),
            };
            int ok = 0;
            var wrong = new List<string>();
            foreach (var row in chart)
            {
                string got = string.Join(",", GuildData.All.Where(g => g.AllowsRace(row.Race)).Select(g => g.Name));
                if (got == row.Guilds) ok++; else wrong.Add($"{row.Race}: {got}");
            }
            Check("races whose guild list matches", ok, chart.Length);
            Check("races whose does not", string.Join("; ", wrong.Take(2)), "");
        }

        // Guild_Crests ends with a table of guildmasters: "Nomad / Crashland (65)", and so on for
        // all twelve. The port has Crashland at 64, and every one of the twelve is exactly one
        // less than the page's figure - which the page's own prose explains: "the Nomads'
        // guildmaster is Crashland (level 64). This means that as soon as you reach level 65, you
        // will become the guildmaster." The column is the level you must reach to take over, not
        // the master's own level, and the two sources agree once it is read that way.
        //
        // The names come from MAINMOD.BAS:621-632. The page writes the Healer's as "Hands On
        // Henry"; the source is "Hands on Henry", and the port follows the source.
        Console.WriteLine("\nguildmasters (Guild_Crests: the level at which you take over)");
        {
            var takeover = new (string Guild, string Master, int Level)[]
            {
                ("Nomad", "Crashland", 65), ("Warrior", "Garth SliceMonger", 57),
                ("Paladin", "Eli", 63), ("Ninja", "Shogun", 49),
                ("Villain", "Maxx", 68), ("Seeker", "Martin", 78),
                ("Thief", "Nimblefingers", 57), ("Scavenger", "Kane the Wise Axe", 55),
                ("Mage", "Sparrowhawk", 53), ("Sorcerer", "Requnix", 62),
                ("Wizard", "Merlin", 49), ("Healer", "Hands on Henry", 66),
            };
            int ok = 0;
            var wrong = new List<string>();
            foreach (var row in takeover)
            {
                int i = GuildData.IndexOf(row.Guild);
                var (name, level) = Guildmasters.Seeded[i];
                if (name == row.Master && level + 1 == row.Level) ok++;
                else wrong.Add($"{row.Guild}: {name} {level} against {row.Master} {row.Level - 1}");
            }
            Check("guildmasters that match", ok, takeover.Length);
            Check("guildmasters that do not", string.Join("; ", wrong.Take(2)), "");
        }

        // Guild_Crests tabulates each crest's stat mods, its Att/Def and its abilities. The
        // abilities column mixes two different fields - "Protect" and "Invisible" are item
        // abilities (gv0684$, bits 0..5) while "Electrical Resistance" and the rest are the item's
        // resistance mask - so only the ability bits are compared here, which is why the Ninja's
        // Electrical Resistance shows as no abilities at all rather than as a mismatch.
        Console.WriteLine("\nguild crests (Guild_Crests: stat mods, Att/Def, abilities)");
        {
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var crests = new (string Guild, string Mods, int Att, int Def, int Abilities)[]
            {
                ("Nomad",     "1,0,0,0,0,1",  6,  6, 0x0),
                ("Warrior",   "2,0,0,2,0,2", 12,  6, 0x0),
                ("Paladin",   "2,0,0,1,2,1",  6, 12, 0x4),
                ("Ninja",     "2,0,0,0,0,3",  6, 18, 0x0),
                ("Villain",   "1,1,1,1,0,2",  9,  9, 0x0),
                ("Seeker",    "2,2,2,0,0,0",  6,  9, 0x1),
                ("Thief",     "0,0,0,0,0,4",  3,  6, 0x0),
                ("Scavenger", "2,0,0,1,0,3",  6,  9, 0x0),
                ("Mage",      "0,2,2,0,2,0",  0, 12, 0xA),
                ("Sorcerer",  "0,3,2,1,0,0",  3, 12, 0x0),
                ("Wizard",    "0,4,2,0,0,0",  3, 12, 0xE),
                ("Healer",    "1,2,2,0,0,1",  3,  9, 0x0),
            };
            int ok = 0;
            var wrong = new List<string>();
            foreach (var row in crests)
            {
                var it = wares.FirstOrDefault(i => i.Name.Trim() == $"Crest of the {row.Guild}");
                if (it == null) { wrong.Add($"{row.Guild} absent"); continue; }
                string mods = string.Join(",", Enumerable.Range(0, 6).Select(k => it.StatsMod[k]));
                if (mods != row.Mods) wrong.Add($"{row.Guild} mods {mods}");
                else if (it.Att != row.Att || it.Def != row.Def) wrong.Add($"{row.Guild} A/D {it.Att}/{it.Def}");
                else if (it.Abilities != row.Abilities) wrong.Add($"{row.Guild} abilities 0x{it.Abilities:X}");
                else ok++;
            }
            Check("crests that match the page", ok, crests.Length);
            Check("crests that do not", string.Join("; ", wrong.Take(3)), "");
        }

        // Guild_Crests' second table says what each crest casts and how many charges it holds.
        // The spell an item casts is M56C4, which is one-based and is a *record position* rather
        // than a spell id: sub15CD tests `> 0` and then uses `M56C4 - 1` (MAINMOD2.BAS:6112, 6127).
        //
        // That distinction is the whole of a bug this table found. Charm of Opening is the record
        // at index 0, so any item casting it has M56C4 = 1 and a spell *id* of 0 - and the port's
        // Castables gated on `SpellId > 0`, which drops exactly those. Seven items, every one of
        // them a lock-opener, could not be used at all.
        Console.WriteLine("\nwhat items cast (Guild_Crests, and the seven Charm of Opening items)");
        {
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var spellList = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            string Casts(string itemName)
            {
                var it = wares.FirstOrDefault(i => i.Name.Trim() == itemName);
                if (it == null) return "absent";
                if (it.SpellIndex <= 0) return "none";
                return $"{spellList[it.SpellIndex - 1].Name.Trim()} ({it.Charges})";
            }
            foreach (var (crest, want) in new[]
                     {
                         ("Crest of the Nomad", "none"), ("Crest of the Warrior", "none"),
                         ("Crest of the Paladin", "Minor Heal (12)"), ("Crest of the Ninja", "none"),
                         ("Crest of the Villain", "Poison (6)"), ("Crest of the Seeker", "Displacement (6)"),
                         ("Crest of the Thief", "Charm of Opening (12)"),
                         ("Crest of the Scavenger", "Charm of Opening (6)"),
                         ("Crest of the Mage", "Soul Entrapment (6)"), ("Crest of the Sorcerer", "Pillar of Fire (6)"),
                         ("Crest of the Wizard", "Power (6)"), ("Crest of the Healer", "Restoration (6)"),
                     })
                Check(crest.Replace("Crest of the ", "") + "'s crest", Casts(crest), want);

            // every item whose M56C4 says it casts, and how many of those a SpellId test would lose
            int casters = wares.Count(i => i.Id != 0 && i.SpellIndex > 0);
            var dropped = wares.Where(i => i.Id != 0 && i.SpellIndex > 0 && i.SpellId <= 0)
                               .Select(i => i.Name.Trim()).OrderBy(x => x, StringComparer.Ordinal).ToList();
            CheckAtLeast("items that cast something", casters, 132);
            Check("the ones a SpellId gate would silently drop",
                  string.Join(", ", dropped),
                  "Crest of the Scavenger, Crest of the Thief, Dust of Opening, Key of Opening, "
                  + "Ring of Opening, Skeleton Key, Thief's Cap");
        }

        // MDATA2's record 0 is a real spell. MDATA3's and MDATA5's record 0 are templates - the
        // item and monster loaders skip them for good reason - but FRMCHAR3.BAS:83 runs
        // `For l0076 = 0 To gv1BF8 - 1` over the spells and skips nothing, and all 105 records
        // carry a name. Known() used to drop `s.Id == 0`, which is Charm of Opening and nothing
        // else, so no character could learn the spell the help uses as its worked example.
        //
        // The boundaries below join this to the rate table and to CanCast: the help says the Thief
        // learns it at Spell Level 3 and the Nomad at 12, which by FRMCHAR3.BAS:97 is guild level 5
        // and guild level 23. Both sides of both are checked.
        Console.WriteLine("\nspell record 0 (Charm of Opening, the one the help works through)");
        {
            var everySpell = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            Check("spell records with no name at all", everySpell.Count(s => s.Name.Trim().Length == 0), 0);
            Check("record 0 is", everySpell[0].Name.Trim(), "Charm of Opening");
            Check("records carrying id 0", everySpell.Count(s => s.Id == 0), 1);

            bool Knows(string guild, int level)
            {
                var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 18, 18, 18, 18, 18, 18 });
                c.Guilds.Clear();
                c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
                return Spellbook.Known(c, everySpell).Any(s => s.Name.Trim() == "Charm of Opening");
            }
            Check("a Thief 4 knows it", Knows("Thief", 4), false);
            Check("a Thief 5 knows it", Knows("Thief", 5), true);
            Check("a Nomad 22 knows it", Knows("Nomad", 22), false);
            Check("a Nomad 23 knows it", Knows("Nomad", 23), true);
        }

        // Record 0 means something different in each of the three tables, which is exactly why
        // "skip record 0" is not a rule that can be applied uniformly - and applying it uniformly
        // is what hid Charm of Opening.
        //
        //   MDATA2  a real spell, Charm of Opening. FRMCHAR3.BAS:83 iterates from 0 and skips
        //           nothing, and every one of the 105 records carries a name.
        //   MDATA3  a real item, "Hands" - what you attack with bare-handed. It is pinned in pack
        //           slot 0 and never moves, which is why the Objects & Spells window opens with
        //           "1. Hands". Keeping it out of the store and quest draws is right; treating it
        //           as absent would not be.
        //   MDATA5  a genuine duplicate. "GOBLIE" carries the same floor, hits, attack, defence,
        //           guild level, kind, rarity and trap flags as Outcast Goblin, to the value.
        //           Skipping it is right.
        Console.WriteLine("\nrecord 0 in each table (three tables, three different answers)");
        {
            var sp0 = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var it0 = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var mo0 = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));

            Check("MDATA2 record 0", sp0[0].Name.Trim(), "Charm of Opening");
            Check("MDATA3 record 0", it0[0].Name.Trim(), "Hands");
            Check("MDATA5 record 0", mo0[0].Name.Trim(), "GOBLIE");
            Check("the unarmed item the pack pins in slot 0", Character.HandsItemId, it0[0].Id);

            // GOBLIE is a duplicate in every field that matters, which is what makes it skippable
            var og = mo0.First(m => m.Name.Trim() == "Outcast Goblin");
            var z = mo0[0];
            Check("GOBLIE against Outcast Goblin",
                  $"{z.LevelFound},{z.Hits},{z.Att},{z.Def},{z.GuildLevel},{(int)z.Kind},{z.Chance},{z.TrapFlags}",
                  $"{og.LevelFound},{og.Hits},{og.Att},{og.Def},{og.GuildLevel},{(int)og.Kind},{og.Chance},{og.TrapFlags}");

            // and none of the three tables pads with nameless rows, so a blank name is never the
            // thing to filter on either
            Check("nameless records across the three tables",
                  sp0.Count(s => s.Name.Trim().Length == 0) + it0.Count(i => i.Name.Trim().Length == 0)
                    + mo0.Count(m => m.Name.Trim().Length == 0), 0);
        }

        // The feature, not the fields. Charm of Opening is the only spell in the shipped data with
        // a lock-opening effect - the Opening school holds two spells and the other is Fate - so
        // when it was unreachable there was no magical way to open a locked chest at all. Both
        // routes are checked here rather than the spell list alone, because the earlier assertion
        // on this spell's level tested the arithmetic and passed while the spell was unreachable.
        Console.WriteLine("\nopening a locked chest (the one spell that does it, and the seven items)");
        {
            var everySpell = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var everyItem = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? Look(short id) => everyItem.FirstOrDefault(i => i.Id == id);

            var openers = everySpell.Where(s => Spellbook.OpensLockUpTo(s) > 0).Select(s => s.Name.Trim()).ToList();
            Check("spells that open a lock", string.Join(", ", openers), "Charm of Opening");

            var thief = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 18, 18, 18, 18, 18, 18 });
            thief.Guilds.Clear();
            thief.Guilds.Add(new GuildMembership { Guild = "Thief", Level = 10 });
            Check("a Thief 10 can cast it", Spellbook.Castables(thief, everySpell, Look)
                      .Any(c => !c.FromItem && c.Spell.Name.Trim() == "Charm of Opening"), true);

            int offering = 0;
            foreach (string name in new[] { "Thief's Cap", "Ring of Opening", "Dust of Opening", "Key of Opening",
                                            "Skeleton Key", "Crest of the Thief", "Crest of the Scavenger" })
            {
                var it = everyItem.FirstOrDefault(i => i.Name.Trim() == name);
                if (it == null) continue;
                var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 25, 25, 25, 25, 25, 25 });
                c.Guilds.Clear();
                c.Guilds.Add(new GuildMembership { Guild = "Thief", Level = 60 });
                c.PutInPack(new InventorySlot
                {
                    ItemId = it.Id, Charges = (short)Math.Max(1, it.Charges),
                    Known = IdLevel.All, Equipped = ItemUse.MustBeEquippedToUse(it),
                });
                if (Spellbook.Castables(c, everySpell, Look)
                        .Any(x => x.FromItem && x.Item!.Id == it.Id && x.Spell.Name.Trim() == "Charm of Opening"))
                    offering++;
            }
            Check("items that offer it when held", offering, 7);
        }

        // A spell whose effect is 75 to 99 only harms one class of creature: monGetSpellHits
        // (MONSMOD.BAS:2561-2565) zeroes the damage unless the ordered target's class index equals
        // `effect - 75`. That routine serves both directions, and the port applied the rule only
        // when a monster cast - so a character's Abolish Undead hit anything at all, for full.
        //
        // Five spells carry damage and a class: Dispell Undead, Dispell Elemental, Banish Devil,
        // Banish Demon, Abolish Undead. The charm and control spells in the same range take the
        // charm path rather than the damage path, so they are not part of this.
        Console.WriteLine("\nspells that only bite one class (MONSMOD.BAS:2561)");
        {
            var everySpell = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? Look(short id) => wares.FirstOrDefault(i => i.Id == id);

            int Dealt(Spell s, Monster target)
            {
                int total = 0;
                for (int i = 0; i < 12; i++)
                {
                    var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 25, 25, 25, 25, 25, 25 });
                    c.Guilds.Clear();
                    c.Guilds.Add(new GuildMembership { Guild = "Mage", Level = 60 });
                    var foe = new CombatMonster { Def = target, Hits = 100000 };
                    var fight = new CombatEngine(c, new[] { foe }, Look, new Random(500 + i));
                    fight.UseSpellTable(everySpell);
                    fight.CastSpell(s, 0, free: true, itemLevel: 30);
                    total += 100000 - foe.Hits;
                }
                return total;
            }

            int landedOnOwn = 0, leakedToOthers = 0, tested = 0;
            foreach (var s in everySpell.Where(x => SpellEffect.Of(x) is >= 75 and <= 99 && x.Damage2 > 0
                                                 && !Spellbook.IsCharm(x)))
            {
                int want = SpellEffect.Of(s) - 75;
                var right = beasts.FirstOrDefault(m => m.Id != 0 && m.Hits > 0 && m.Type < Mdata1.MonsterClasses.Count
                                                    && Mdata1.MonsterClasses[m.Type].Index == want);
                var wrong = beasts.FirstOrDefault(m => m.Id != 0 && m.Hits > 0 && m.Type < Mdata1.MonsterClasses.Count
                                                    && Mdata1.MonsterClasses[m.Type].Index != want);
                if (right == null || wrong == null) continue;
                tested++;
                if (Dealt(s, right) > 0) landedOnOwn++;
                if (Dealt(s, wrong) > 0) leakedToOthers++;
            }
            CheckAtLeast("class-targeted damage spells tested", tested, 5);
            Check("of those, the ones that hurt their own class", landedOnOwn, tested);
            Check("of those, the ones that leak onto other classes", leakedToOthers, 0);
        }

        // Four spells sit at level 50, above the 15 that is the highest ceiling any guild reaches
        // in any school, so no character can ever learn them. That is not an oversight: every one
        // of the four is carried by an item, and the items are ordinary obtainable gear on floors
        // 3 to 13. Level 50 is how the data says "this one comes from an item".
        //
        // The pairing is what makes it a convention rather than a coincidence - a spell nobody can
        // learn and nothing can cast would be a dead record, and there are none of those.
        Console.WriteLine("\nspells no guild can reach (level 50 means item-only)");
        {
            var everySpell = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));

            var beyond = new List<string>();
            var orphaned = new List<string>();
            for (int rec = 0; rec < everySpell.Count; rec++)
            {
                var s = everySpell[rec];
                if (s.Name.Trim().Length == 0) continue;
                int ceiling = GuildData.All.Where(g => g.HasSpellTable)
                                           .Select(g => g.SpellMax[s.Category]).DefaultIfEmpty(0).Max();
                if (s.Level <= ceiling) continue;
                beyond.Add(s.Name.Trim());
                if (!wares.Any(i => i.Id != 0 && i.SpellIndex - 1 == rec)) orphaned.Add(s.Name.Trim());
            }
            Check("spells past every guild ceiling",
                  string.Join(", ", beyond.OrderBy(x => x, StringComparer.Ordinal)),
                  "Fate, Life Domination, She'tal, Shock Lance");
            Check("of those, the ones no item casts either", string.Join(", ", orphaned), "");
            Check("the highest ceiling any guild reaches",
                  GuildData.All.Where(g => g.HasSpellTable).SelectMany(g => g.SpellMax).Max(), 15);

            // and the carriers work: held and identified, each offers its spell
            int offered = 0;
            foreach (string nm in new[] { "Elixr of Fate", "Magical Dust", "Dalyn's Groan",
                                          "Scroll of She'tal", "Orb of Life Domination", "Lamurian Shock Lance" })
            {
                var it = wares.FirstOrDefault(i => i.Name.Trim() == nm);
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
                if (Spellbook.Castables(c, everySpell, id => wares.FirstOrDefault(i => i.Id == id))
                        .Any(x => x.FromItem && x.Item!.Id == it.Id)) offered++;
            }
            Check("carriers that offer their spell when held", offered, 6);
        }

        // The help's Commands page is a flat list of about fifty keys - the one place the interface
        // can be checked against a written source rather than against a screenshot. Every one was
        // traced by hand to its handler; this keeps that from rotting.
        //
        // It is a scan of the key handler's source, not a test of behaviour, because the bindings
        // live in switch statements inside an Avalonia window and driving them needs the window.
        // It catches a binding being deleted. It would not catch one wired to the wrong thing, and
        // saying so is the point - an interface check that looks stronger than it is would be
        // worse than none.
        Console.WriteLine("\nthe help's Commands page against the key handler");
        {
            string portDir = PortTree.Root;
            var (found, total, missing) = CommandProbe.Scan(portDir);
            CheckAtLeast("keys the page lists", CommandProbe.Listed.Length, 49);
            Check("keys still bound in the source", $"{found} of {total}", $"{total} of {total}");
            Check("keys that went missing", string.Join(", ", missing.Take(4)), "");

            // and the scan reads real source: pointed somewhere with none, it finds nothing
            var (none, everyToken, _) = CommandProbe.Scan(System.IO.Path.Combine(dir, "..", "no-such-tree"));
            Check("the same scan with no source to read", none, 0);
            Check("which is all of them", everyToken, total);
        }

        // The Options window, against game_files/bm3.png and the help's own page. Twelve tick
        // boxes, two volume sliders, Font Settings and File Backups - and no Screen Saver button,
        // although the help describes one: "If you purchased the CD-ROM/Enhanced version of Mordor
        // and installed the Screen Saver". FRMOPTIO.BAS has no such control, and neither does the
        // screenshot, so the port has none either.
        //
        // The captions come from the screenshot rather than the help's prose, which differs on two
        // of them: the picture reads "No HELP Sound" and "No Autosave", the prose "No Help Sound"
        // and "No AutoSave". For a caption the picture wins.
        Console.WriteLine("\nthe Options window (bm3.png, and FRMOPTIO's INI keys)");
        {
            // FRMOPTIO.BAS:152-247 writes one key per box, and one of them twice over: the ticked
            // branch writes NoToolBar and the unticked branch NoToolbar. Windows matches INI keys
            // without regard to case so nothing came of it, and the port keeps the slip - which
            // only stays harmless because its own reader matches the same way. That is the thing
            // worth asserting here: not the slip, but the property that makes it survivable.
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-selfcheck-ini");
            System.IO.Directory.CreateDirectory(tmp);
            string path = GameOptions.PathFor(tmp);
            System.IO.File.WriteAllText(path, "[Loading Information]\r\nNoToolBar=True\r\n");
            Check("a key written with the other casing still reads back",
                  GameOptions.Load(tmp).NoToolbar, true);
            System.IO.File.WriteAllText(path, "[loading information]\r\nnotoolbar=True\r\n");
            Check("and so does a section in the wrong case", GameOptions.Load(tmp).NoToolbar, true);
            try { System.IO.File.Delete(path); System.IO.Directory.Delete(tmp); } catch { }
        }

        // Character creation seeds window sizes from a table that does not line up with the list
        // it seeds (CHARCREA.BAS:315-342 against sub03A2, MODULE1.BAS:606-648). Index 1 is seeded
        // and has no default; index 14 has a default and is never seeded. So a new character
        // carries a zero size for one window and a meaningless one for another.
        //
        // Both are harmless, and each for a different reason - which is the part worth holding on
        // to. A reproduced fault stays safe only while whatever neutralised it is reproduced too,
        // and neither of these neutralisers was being checked.
        Console.WriteLine("\nthe window sizes a new character is given (CHARCREA.BAS:315-342)");
        {
            // the fault itself
            Check("index 1 is seeded", WindowLayout.SeededByCharacterCreation.Contains(1), true);
            Check("and has no default to seed from", WindowLayout.Default(1), (0d, 0d));
            Check("index 14 is not seeded", WindowLayout.SeededByCharacterCreation.Contains(14), false);
            Check("though it does have a default", WindowLayout.Default(14).W > 0, true);

            // what keeps index 14's zero from ever being read: the Control Panel leaves it out of
            // the Window Control list and out of Adjust to Display (FRMCONTR.BAS:97, 216)
            Check("14 is not in the Window Control list", WindowLayout.ListedInWindowControl(14), false);
            Check("14 is not touched by Adjust to Display", WindowLayout.AdjustedByControlPanel(14), false);

            // and what keeps index 1's meaningless size from being read: there is no window there.
            // The frame's list walks real children, and nothing in GameWindow carries the value 1.
            Check("no window is numbered 1",
                  Enum.GetValues<GameWindow>().Cast<int>().Contains(1), false);
        }

        // A saved window table is read back as it was. The original writes the table only at
        // character creation (CHARCREA.BAS:302-343), in the Control Panel (FRMCONTR.BAS:115-116,
        // 149-150, 185-186) and as each form unloads, and sub0524 moves a
        // window only when it lies past the whole display (MAINMOD.BAS:487-494). The port had
        // wiped every table saved before its layout revision 1, and snapped a window back to 0
        // whenever it lay below the frame's area - so Messages, saved at Top 834 in a tall frame,
        // jumped to the top of a 725-tall one before the frame reached its size.
        Console.WriteLine("\na saved window table comes back unchanged (CHARCREA.BAS:302-343, MAINMOD.BAS:487-494)");
        {
            var saved = new Character { WindowsRevision = 0 };
            var msgs = saved.WindowFor(GameWindow.Messages);
            msgs.Left = 300; msgs.Top = 834; msgs.Width = 700; msgs.Height = 150;
            var ch = saved.WindowFor(GameWindow.Character);
            ch.Left = 1; ch.Top = 1; ch.Width = 400; ch.Height = 540;
            // the 800x600-shaped part of a 1180x725 frame, as MdiFrame.ScaleArea gives it
            bool arranged = WindowLayout.Prepare(saved, 725 * 800.0 / 600, 725);
            Check("a placed table is not laid out again", arranged, false);
            Check("an old revision's table is not wiped", (msgs.Top, msgs.Width, ch.Width), (834d, 700d, 400d));
            Check("Top 834 is kept in a 725-tall frame on a 1080-tall display",
                  WindowLayout.KeptOnScreen(msgs.Left, msgs.Top, 1920, 1080), (300d, 834d));
            Check("a window past the whole display still comes back to its edge",
                  WindowLayout.KeptOnScreen(2000, 1200, 1920, 1080), (0d, 0d));
            var fresh = new Character();
            fresh.Windows = WindowLayout.Fresh();
            Check("a blank table is still seeded and laid out, as CHARCREA does",
                  (WindowLayout.Prepare(fresh, 800, 600), fresh.WindowFor(GameWindow.Character).HasSize), (true, true));
        }

        // A window kept below the frame's area has to be reachable: an MDIForm's ScrollBars is
        // True by default and scrolls to a child past its client edge (a VB3 default; the port's
        // MdiHost had clipped it out of sight). The bars move the view, never the window.
        Console.WriteLine("\nthe frame scrolls to a window past its edge (VB3 MDIForm ScrollBars, MAINMOD.BAS:487-494)");
        {
            MdiScroll.Child Win(double x, double y, double w, double h, bool min = false, bool max = false, bool shown = true) =>
                new(x, y, w, h, shown, min, max);
            var low = MdiScroll.Bars(new[] { Win(10, 10, 400, 300), Win(100, 850, 574, 126) }, 1180, 700, 16);
            Check("Messages at Top 850 in a 700-tall area: a vertical bar", (low.V, low.H), (true, false));
            Check("which scrolls until its bottom shows", (low.MinY, low.MaxY), (0d, 850 + 126 - 700d));
            Check("everything inside: no bars",
                  MdiScroll.Bars(new[] { Win(10, 10, 400, 300) }, 1180, 700, 16) is { H: false, V: false }, true);
            Check("an icon or a closed window does not count",
                  MdiScroll.Bars(new[] { Win(0, 900, 160, 24, min: true), Win(0, 900, 300, 300, shown: false) }, 1180, 700, 16) is { H: false, V: false }, true);
            Check("with one maximised nothing scrolls",
                  MdiScroll.Bars(new[] { Win(0, 0, 1180, 700, max: true), Win(100, 850, 574, 126) }, 1180, 700, 16) is { H: false, V: false }, true);
            var wide = MdiScroll.Bars(new[] { Win(1412, 424, 558, 468) }, 1180, 700, 16);
            Check("past the right and the bottom: both bars, each range allowing for the other bar",
                  (wide.H, wide.V, wide.MaxX, wide.MaxY), (true, true, 1970 - 1180 + 16d, 892 - 700 + 16d));
            var left = MdiScroll.Bars(new[] { Win(-200, 10, 400, 300) }, 1180, 700, 16);
            Check("off the left: the range runs from there to the area's own corner", (left.H, left.V, left.MinX, left.MaxX), (true, false, -200d, 0d));
            var held = MdiScroll.Bars(new[] { Win(10, 10, 400, 300) }, 1180, 700, 16, keep: (0, 150));
            Check("while a window is dragged the view is kept, and no other bar comes up",
                  (held.V, held.MaxY, held.H), (true, 150d, false));
        }

        // The frame's own saved place is let go when no display shows its title strip, where it
        // is taken hold of - a sliver of its bottom edge on a display is not enough. A port
        // necessity, as the saved place is (FramePlacement).
        Console.WriteLine("\na saved frame is kept only where its title can be taken hold of");
        {
            var onScreen = new FramePlacement(100, 100, 1180, 760, false);
            Check("inside a 1920x1040 working area", onScreen.TitleShownOn(0, 0, 1920, 1040, 1), true);
            var above = new FramePlacement(100, -740, 1180, 760, false);
            Check("only its bottom edge showing, title above the display", above.TitleShownOn(0, 0, 1920, 1040, 1), false);
            var offRight = new FramePlacement(1900, 100, 1180, 760, false);
            Check("its title's left end on the display", offRight.TitleShownOn(0, 0, 1920, 1040, 1), true);
            var gone = new FramePlacement(3000, 100, 1180, 760, true);
            Check("on a display no longer there", gone.TitleShownOn(0, 0, 1920, 1040, 1.25), false);
        }

        // modAdjustMap and sub2E18 test the automap FORM against the table's 458x493, each axis on
        // its own (FRMAUTOM.BAS:461, 478, 508, 515). The port had tested a child's Bounds read
        // mid-arrange, which were 0x0 the first time: every form came out narrow and shallow with a
        // one-pixel room, and the square you stood on sat at the picture's top left. The user's
        // 475x553 window is at least full size both ways, so the whole map from the top left.
        Console.WriteLine("\nwhere the automap's picture goes (FRMAUTOM.BAS:455-522)");
        {
            Check("the user's 475x553 form is not narrow", AutomapLayout.Narrow(475.6), false);
            Check("nor shallow", AutomapLayout.Shallow(553.5), false);
            Check("so the map starts at the left", AutomapLayout.Across(475.6, 455, 30, 8), 0d);
            Check("and at the top", AutomapLayout.Down(553.5, 480, 30, 10), 0d);
            Check("exactly the default is full size across", AutomapLayout.Narrow(458), false);
            Check("and down", AutomapLayout.Shallow(493), false);
            Check("a pixel less is narrow", AutomapLayout.Narrow(457), true);
            Check("and shallow", AutomapLayout.Shallow(492), true);
            // the seeded size, the default over 2.25 (CHARCREA.BAS), with a 193x199 picture: the
            // square 9,11 is (X-1)*14 + 15/4 from picMaze's left and (30-11)*14 + 15/4 from its top
            double across = AutomapLayout.Across(458 / 2.25, 193, 30, 8);
            double down = AutomapLayout.Down(493 / 2.25, 199, 30, 10);
            CheckNear("a small form puts your square in the middle across", across + 8 * 14 + 15 / 4.0, 96, 97);
            CheckNear("and down, to the pixel", down + 19 * 14 + 15 / 4.0, 99, 100);
            // lost: the level's middle, Width / 2 and Height / 2 in 1-based squares (467, 484)
            Check("lost, the middle column", AutomapLayout.Across(200, 200, 30, null), Math.Round(100 - (14 * 14 + 3.75)));
            Check("lost, the middle row", AutomapLayout.Down(200, 200, 30, null), Math.Round(100 - (15 * 14 + 3.75)));
            // sub2E18 prints the columns only at full width and the rows only at full height, and
            // a picture grown to fill the other axis covers what was printed
            Check("numbers at full size", AutomapLayout.ColumnNumbers(458, 493) && AutomapLayout.RowNumbers(458, 493), true);
            Check("no column numbers on a narrow form", AutomapLayout.ColumnNumbers(457, 600), false);
            Check("no row numbers on a short form", AutomapLayout.RowNumbers(600, 492), false);
            Check("nor either kind when one axis is short", AutomapLayout.ColumnNumbers(600, 492) || AutomapLayout.RowNumbers(457, 600), false);

            // picMaze is never scaled, however big the form (see AutomapLayout's summary): the form
            // blits with BitBlt alone, never StretchBlt, never asks "No StretchBlt", and never runs
            // the routines that scale a form's controls with it
            Section("the automap blits, never stretches (FRMAUTOM.BAS)", () =>
            {
                string automSrc = PortSource("decompiled-named", "FRMAUTOM.BAS");
                Check("extfn182D is GDI's BitBlt",
                      PortSource("decompiled-named", "MODULE8.BAS").Contains("Declare Function extfn182D Lib \"GDI\" Alias \"BitBlt\""), true);
                Check("the automap blits with it", System.Text.RegularExpressions.Regex.Matches(automSrc, @"extfn182D\(").Count > 0, true);
                Check("and never with StretchBlt (extfn01C4) or StretchDIBits (extfn1844)",
                      automSrc.Contains("extfn01C4") || automSrc.Contains("extfn1844"), false);
                Check("nor reads No StretchBlt (gv1A66.M5F04)", automSrc.Contains("M5F04"), false);
                Check("nor calls sub0760 or sub07C5, which scale controls with the form",
                      automSrc.Contains("sub0760") || automSrc.Contains("sub07C5"), false);
            });
            // so the port's enlargement is its own, a GUI deviation the user asked for (2026-10-14,
            // docs/FIDELITY.md): the map grows only through Zoom
            string automPort = PortSource("src/Mordor.App", "AutomapWindow.axaml.cs");
            Check("the port enlarges the map in one place, through AutomapLayout.Zoom",
                  System.Text.RegularExpressions.Regex.Matches(automPort, @"AutomapLayout\.Zoom\(").Count, 1);
            Check("and never with a transform of its own", automPort.Contains("ScaleTransform"), false);
        }

        // The zoom: the largest factor that fits both ways, less four device pixels for the
        // snapping, never below 1; the map drawn for the device by the square, so every wall is
        // one thickness; centred on whole device pixels; and every pointer test on the map goes
        // back through it (AutomapLayout.ToMap)
        Console.WriteLine("\nthe port's enlarged automap (a GUI deviation, docs/FIDELITY.md)");
        {
            static double Z(double w, double h, double rs) => Math.Round(AutomapLayout.Zoom(w, h, 438, 434, rs), 9);
            // picMaze 421x421 and its numbers: 438x434 with the default fonts
            Check("the default form's picture is its own size", Z(428, 440, 1), 1d);
            Check("any room more is a bigger map: no whole steps", Z(438 * 1.1 + 4, 434 * 1.1 + 4, 1), 1.1);
            Check("half as big again is 1.5x", Z(657 + 4, 651 + 4, 1), 1.5);
            Check("twice the room is 2x", Z(876 + 4, 868 + 4, 1), 2d);
            Check("the shorter way decides: wide and short", Z(2000, 651 + 4, 1), 1.5);
            Check("and tall and narrow", Z(657 + 4, 2000, 1), 1.5);
            Check("short of a whole step is still more than 1x", Z(870, 860, 1) > 1.9 && Z(870, 860, 1) < 2, true);
            // on a 125% display the spare four device pixels are 3.2 of the window's
            Check("125%: the same rule, four device pixels spare", Z(438 * 1.3 + 3.2, 434 * 1.3 + 3.2, 1.25), 1.3);
            Check("125%: no step needed to start enlarging", Z(438 * 1.02 + 3.2, 434 * 1.02 + 3.2, 1.25), 1.02);
            Check("never smaller than the map's own size", Z(200, 200, 1), 1d);
            Check("no room yet, no zoom", Z(0, 0, 1), 1d);
            Check("centred", AutomapLayout.Centred(1000, 876, 1), 62d);
            Check("on a whole device pixel at 125%", Math.Abs(AutomapLayout.Centred(1001, 700.8, 1.25) * 1.25 - Math.Round(AutomapLayout.Centred(1001, 700.8, 1.25) * 1.25)) < 1e-9, true);
            Check("never left of the picture", AutomapLayout.Centred(400, 438, 1), 0d);
            Check("the map itself centred, its 20-pixel gutter of numbers at its left", AutomapLayout.Centred(1000, 876, 20, 0, 1), 52d);
            Check("its numbers never pushed out of the picture, left", AutomapLayout.Centred(900, 890, 30, 0, 1), 0d);
            Check("or right", AutomapLayout.Centred(900, 890, 0, 30, 1), 10d);

            // DeviceEdges: a 30-square level, 421 map pixels, at factors whole and not, 100% and 125%
            Check("1:1 is the map as it is", string.Join(",", AutomapLayout.DeviceEdges(421, 1).Take(5)) + ".." + AutomapLayout.DeviceEdges(421, 1)[421], "0,1,2,3,4..421");
            Check("2x is two device pixels a pixel", AutomapLayout.DeviceEdges(421, 2).Select((e, i) => e == 2 * i).All(b => b), true);
            foreach (double s in new[] { 1.25, 1.5, 1.6, 1.7, 2.0 * 1.25, 1.37 * 1.25, 2.913, 3.654 * 1.25 })
            {
                var e = AutomapLayout.DeviceEdges(421, s);
                int W(int i) => e[i + 1] - e[i];
                const int pitch = AutomapLayout.Pitch;
                int floor = (int)Math.Floor(pitch * s + 1e-9);
                bool squares = true, walls = true, every = true, oneOff = true;
                int offsetsVarying = 0;
                for (int c = 0; c <= 30; c++)
                {
                    squares &= e[c * pitch] == (int)Math.Floor(c * pitch * s + 0.5);
                    walls &= W(c * pitch) == W(0);
                    if (c < 30) oneOff &= e[(c + 1) * pitch] - e[c * pitch] is var sq && (sq == floor || sq == floor + 1);
                }
                for (int i = 0; i < 421; i++) every &= W(i) >= 1;
                for (int o = 0; o < pitch; o++)
                {
                    var widths = Enumerable.Range(0, 30).Select(c => W(c * pitch + o)).Distinct().ToList();
                    if (widths.Count > 1) offsetsVarying++;
                    if (widths.Count > 2 || widths.Max() - widths.Min() > 1) offsetsVarying += 100;
                }
                Check($"{s:0.###} device pixels a pixel: every square starts on the device pixel nearest its place", squares, true);
                Check($"{s:0.###}: every square {floor} or {floor + 1} device pixels", oneOff, true);
                Check($"{s:0.###}: every wall line (a square's edge) one thickness, {W(0)}", walls && W(0) == (int)Math.Floor(Math.Floor(pitch * s + 1e-9) / pitch + 0.5), true);
                Check($"{s:0.###}: no pixel dropped", every, true);
                Check($"{s:0.###}: inside a square at most one offset differs from square to square, by one", offsetsVarying <= 1, true);
                Check($"{s:0.###}: and the whole no more than a device pixel past the factor's", e[421] <= 421 * s + 1 + 1e-9 && e[421] >= 421 * s - 1 - 1e-9, true);
            }
            // a click at picPicture (100 + 2 * 117, 40 + 2 * 50) on a 2x map drawn from (100, 40)
            // is map pixel (117, 50): picMaze at 17 in, square 8 across and 4 down from the top
            var (mx, my) = AutomapLayout.ToMap(334, 140, 100, 40, 2);
            Check("a click on the enlarged map is the same map pixel", (mx, my), (117d, 50d));
            Check("which is on picMaze", AutomapLayout.OnMaze(mx, my, 17, 421, 421), true);
            Check("the same pixel at 1x", AutomapLayout.ToMap(217, 90, 100, 40, 1), (117d, 50d));
            Check("the row numbers are not picMaze", AutomapLayout.OnMaze(10, 50, 17, 421, 421), false);
            Check("nor past its far edge", AutomapLayout.OnMaze(17 + 421, 50, 17, 421, 421), false);
            Check("nor the column numbers below it", AutomapLayout.OnMaze(117, 425, 17, 421, 421), false);
            Check("a map slid left of the picture still maps", AutomapLayout.ToMap(50, 60, -200, -30, 1), (250d, 90d));
            var (fx, fy) = AutomapLayout.ToMap(100 + 1.37 * 117, 40 + 1.37 * 50, 100, 40, 1.37);
            Check("and at a fractional factor, 1.37x", (Math.Round(fx, 9), Math.Round(fy, 9)), (117d, 50d));
        }

        // timAilment_Timer posts nothing to frmAutoMap; the port's ailment clock ended in a Redraw
        // that did, and with "Auto Change Lev." that turned the map back to your level every four
        // seconds - what the user saw as "resizing the automap resets it to level 1"
        Console.WriteLine("\nthe automap is not told anything by the ailment clock (FRMDUNM.BAS:395-614)");
        {
            Section("timAilment_Timer and frmAutoMap (FRMDUNM.BAS)", () =>
            {
                string dunm = PortSource("decompiled-named", "FRMDUNM.BAS");
                int a = dunm.IndexOf("Sub timAilment_Timer", StringComparison.Ordinal);
                int b = a < 0 ? -1 : dunm.IndexOf("End Sub", a, StringComparison.Ordinal);
                Check("timAilment_Timer is found", a >= 0 && b > a, true);
                Check("and never names frmAutoMap", a >= 0 && b > a && dunm[a..b].Contains("frmAutoMap"), false);
            });
            string dw = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
            int p = dw.IndexOf("private void PushToAutomap()", StringComparison.Ordinal);
            int q = p < 0 ? -1 : dw.IndexOf("_automap.Walked(", p, StringComparison.Ordinal);
            Check("the port's push to the map stops while the ailment clock runs",
                  p >= 0 && q > p && dw[p..q].Contains("if (_inAilment) return;"), true);
        }

        // Form_Load's level (FRMAUTOM.BAS:94-98) asks M59F4(0) and DungeonLevel, nothing else; the
        // map is moved only by Form_Resize and medDrawCursor's arrow branch (164, 320-322); the dead
        // get frmDungeonMain, not the city (MAINMOD2.BAS:3170-3180); q loads no city (6316-6331)
        Console.WriteLine("\nwhen the automap opens, moves and is minimised (FRMAUTOM.BAS:94-98, 320-342)");
        {
            Check("opens on your level", AutomapLayout.OpeningLevel(4, lostLevel: false), 4);
            Check("on level 1 with the level lost", AutomapLayout.OpeningLevel(4, lostLevel: true), 1);
            Check("and on level 1 in town", AutomapLayout.OpeningLevel(0, lostLevel: false), 1);
            Check("drawing the arrow re-centres", AutomapLayout.CursorRecentres(3, lost: false, shownLevel: 3), true);
            Check("another level shown does not", AutomapLayout.CursorRecentres(3, lost: false, shownLevel: 2), false);
            Check("lost does not", AutomapLayout.CursorRecentres(3, lost: true, shownLevel: 3), false);
            Check("town does not", AutomapLayout.CursorRecentres(0, lost: false, shownLevel: 1), false);
            Check("the living in town get the city", AutomapLayout.CityShownAtStart(0, dead: false), true);
            Check("the dead do not, even at level 0", AutomapLayout.CityShownAtStart(0, dead: true), false);
            Check("walking out loads the city", AutomapLayout.CityLoadsOnReturn(dead: false, exitedToMainMenu: false), true);
            Check("coming up dead does not", AutomapLayout.CityLoadsOnReturn(dead: true, exitedToMainMenu: false), false);
            Check("nor does q", AutomapLayout.CityLoadsOnReturn(dead: false, exitedToMainMenu: true), false);
        }

        // A class maximum of 0 stops sub0F74 dead but reads to fn11B6 as "no cap at all"
        // (MAINMOD2.BAS:1724 against :576). For the consumable classes that is harmless - the
        // toggle refuses first - but Miscellaneous Object carries **-1**, which the toggle lets
        // through and fn11B6 then declines to cap. So Miscellaneous Items are the only things in
        // the game you may wear any number of at once.
        //
        // The port reproduces it by testing `!= 0` where `< 1` would read more tidily, and that
        // choice is the whole of it: `< 1` would treat -1 as unwearable and quietly delete the
        // behaviour. Nothing was holding the distinction in place, so here it is - the three values
        // MDATA1 actually uses, and what each one means to each routine.
        Console.WriteLine("\nhow many of a kind you may wear (MAINMOD2.BAS:576 against :1724)");
        {
            var kinds = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"))
                                 .Where(i => i.Id != 0)
                                 .Select(i => Character.WearClassOf(i)).Distinct().Where(k => k >= 0).ToList();
            var caps = kinds.Select(Character.MaxWornOfClass).Distinct().OrderBy(v => v).ToList();
            Check("the caps MDATA1 uses", string.Join(",", caps), "-1,0,1");

            // -1 is wearable and uncapped; 0 is not wearable at all; 1 is wearable and capped
            var misc = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"))
                                .FirstOrDefault(i => i.Id != 0 && Character.MaxWornOfClass(Character.WearClassOf(i)) == -1);
            Check("something carries the -1 cap", misc != null, true);
            if (misc != null)
            {
                Check("and it is wearable", Character.IsWearable(misc), true);
                Check("Medusa's Head is one of them",
                      Character.MaxWornOfClass(Character.WearClassOf(
                          MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"))
                                   .First(i => i.Name.Trim() == "Medusa's Head"))), -1);
            }
            var consumable = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"))
                                      .FirstOrDefault(i => i.Id != 0 && Character.MaxWornOfClass(Character.WearClassOf(i)) == 0);
            Check("something carries the 0 cap", consumable != null, true);
            if (consumable != null) Check("and it is not wearable", Character.IsWearable(consumable), false);
        }

        // CLng as Transfer Gold reads its InputBox: VB's numeric forms, halves to even, and a
        // silent refusal for anything else or anything past a Long
        Console.WriteLine("\nCLng for the gold transfer");
        {
            int CL(string s) => Mordor.Core.VbConvert.TryCLng(s, out int v) ? v : int.MinValue;
            Check("CLng(\" 250 \")", CL(" 250 "), 250);
            Check("CLng(\"&H10\")", CL("&H10"), 16);
            Check("CLng(\"&O17\")", CL("&O17"), 15);
            Check("CLng(\"&HFFFF\") reads as an Integer", CL("&HFFFF"), -1);
            Check("CLng(\"1E3\")", CL("1E3"), 1000);
            Check("CLng(\"2.5D2\")", CL("2.5D2"), 250);
            Check("CLng(\"2.5\") rounds half to even", CL("2.5"), 2);
            Check("CLng(\"3.5\") rounds half to even", CL("3.5"), 4);
            Check("CLng(\"-7\")", CL("-7"), -7);
            Check("CLng(\"abc\") is refused", CL("abc"), int.MinValue);
            Check("CLng(\"3000000000\") is past a Long", CL("3000000000"), int.MinValue);
        }

        // The pens overflow. mdrReplenishCStore tests the cap against pen `l0288` - Good (index 0,
        // gv077E$(0 + 1) = "(g)") until a take-away pass moves it - and then increments the
        // creature's *own* alignment (MAINMOD.BAS:1372-1373). So the pen that is counted is seldom
        // the pen that is filled, and a creature can be penned past nine indefinitely.
        //
        // This one has no neutraliser: it is live, and the port reproduces it.
        Console.WriteLine("\nthe pens overflow (MAINMOD.BAS:1372-1373)");
        {
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"))
                                  .Where(m => m.Id != 0 && m.LevelFound > 0).ToList();
            var pens = new Confinement();
            // a long restock with nothing ever bought: whatever it pens, it pens past the cap
            var penRng = new Random(4);
            for (int visit = 0; visit < 4000; visit++) pens.Replenish(beasts, days: 50, rng: penRng, deepestReached: 14);
            int worst = 0;
            foreach (var kv in pens.Pens) worst = Math.Max(worst, kv.Value);
            CheckAtLeast("the most of one creature and alignment the pens end up holding",
                         worst, Confinement.ShelfCap + 1);
        }

        // Who an acid attack lands on: Int(Rnd * 5), where 0 is the character and 1 to 4 are read
        // back as companion slots (MONSMOD.BAS:2769-2778). Two faults in the one line, both the
        // original's, and both live - no neutraliser to look for.
        //
        // Measuring this through a whole combat round does not work: the monster also swings, and
        // melee reaches every companion, so slot 0 takes damage and the draw looks innocent. The
        // draw had to be pulled out to be seen at all, which is the general lesson - a rule buried
        // inside a bigger routine can only be asserted through whatever else that routine does.
        Console.WriteLine("\nwho acid lands on (MONSMOD.BAS:2769-2778)");
        {
            var seen = new int[6];
            var rng = new Random(11);
            // four occupied slots, as the draw now reads the slots themselves
            var four = new CompanionSlots();
            for (int k = 0; k < 4; k++) four.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 5 });
            const int draws = 60000;
            for (int i = 0; i < draws; i++) seen[CombatEngine.AcidTarget(rng, four)]++;

            // The draw is uniform over 0..4 and nothing else comes back.
            Check("draws outside 0..4", seen[5], 0);
            CheckNear("draw 0 - the character", seen[0], draws / 5 * 9 / 10, draws / 5 * 11 / 10);
            for (int t = 1; t <= 3; t++)
                CheckNear($"draw {t} - companion slot {t}", seen[t], draws / 5 * 9 / 10, draws / 5 * 11 / 10);
            CheckNear("draw 4 - the slot nobody occupies", seen[4], draws / 5 * 9 / 10, draws / 5 * 11 / 10);

            // The first fault, derived rather than declared: take every draw, apply the same rule
            // the acid code applies - `t != 0` and `t < comps.Count` selects comps[t] - and collect
            // which companion slots can actually be reached. Slot 0 is not among them, because the
            // value that would index it is spent on meaning "the character".
            var reachable = new SortedSet<int>();
            var mapRng = new Random(13);
            for (int i = 0; i < draws; i++)
            {
                int t = CombatEngine.AcidTarget(mapRng, four);
                if (t != 0 && t < 4) reachable.Add(t);
            }
            Check("companion slots acid can reach, out of four", string.Join(",", reachable), "1,2,3");
            // The second: a fifth of the draws are slot 4, which no loop in the game ever fills.
            CheckNear("the share of acid that lands on nobody, per 60000",
                      seen[4], draws / 5 * 9 / 10, draws / 5 * 11 / 10);

            // and with no companions at all the draw never leaves the character
            var lone = new Random(12);
            bool always0 = true;
            for (int i = 0; i < 500; i++) if (CombatEngine.AcidTarget(lone, new CompanionSlots()) != 0) always0 = false;
            Check("with no companions it is always the character", always0, true);
        }

        // A drain attack picks its stat with Int(Rnd * 6 + 1) - the +1 inside the Int, so 1 to 6
        // and never 0 (MONSMOD.BAS:3142-3168). The Select Case reads 0..5 as stats and 6 as "two
        // off max hits", so Strength, index 0, cannot be drained at all: a wraith takes everything
        // about you except the stat that makes you hit harder. The +1 was plainly meant to be
        // outside the Int, which would have reached Strength and made Case 6 dead instead.
        //
        // This one is visible from outside, unlike the acid draw: nothing else in a monster's turn
        // moves a stat, so whatever dropped was drained. Run end to end for that reason.
        Console.WriteLine("\nwhat a drain can take (MONSMOD.BAS:3142-3168)");
        {
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var drainer = beasts.First(m => m.Id != 0 && (m.SpecialAttackFlags & (1 << 7)) != 0 && m.LevelFound >= 8);

            var lost = new int[6];
            int hitsLost = 0;
            const int rounds = 1500;
            for (int i = 0; i < rounds; i++)
            {
                var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 17, 18, 18, 17, 18, 18 });
                c.Guilds.Clear();
                c.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 1 });
                c.MaxHits = 100000; c.CurHits = 100000;
                var before = c.Stats.ToArray();
                int maxBefore = c.MaxHits;
                var foe = new CombatMonster { Def = drainer, Hits = 100000 };
                var fight = new CombatEngine(c, new[] { foe }, id => wares.FirstOrDefault(x => x.Id == id),
                                             new Random(7000 + i));
                fight.UseMonsterTable(beasts);
                foreach (var _ in fight.MonsterAttacks()) { }
                for (int s = 0; s < 6; s++) if (c.Stats[s] < before[s]) lost[s]++;
                if (c.MaxHits < maxBefore) hitsLost++;
            }

            Check("Strength drained, ever", lost[0], 0);
            for (int s = 1; s < 6; s++)
                CheckAtLeast($"{RaceData.StatNamesLong[s]} drained", lost[s], 1);
            CheckAtLeast("max hits taken instead", hitsLost, 1);
            // the six reachable outcomes share the draw evenly, which is what says the range is
            // 1..6 rather than something narrower that merely happens to miss Strength
            int lo = Math.Min(hitsLost, Enumerable.Range(1, 5).Min(s => lost[s]));
            int hi = Math.Max(hitsLost, Enumerable.Range(1, 5).Max(s => lost[s]));
            Check("the five stats and the hits case land within a fifth of each other",
                  hi - lo < lo / 5, true);
        }

        // ---- one spell leaks a second state, and only one ----
        // recomputeEffects (MODULE6.BAS:2785) runs a single loop over bits 0..5 of the gear mask
        // and the spell mask together and assigns with the *gear* meanings, then re-tests the
        // spell mask with the spell meanings. Spells write their states at bits 3..6, so three of
        // the four fall inside the loop's range under the wrong name:
        //
        //     Levitate      bit 3  ->  case 3, which sets See Invisible     <- the leak
        //     Invisible     bit 4  ->  case 4, which is empty
        //     See Invisible bit 5  ->  no case 5 exists
        //     Protect       bit 6  ->  past the end of the loop
        //
        // So exactly one of the four leaks, and the explicit tests still set the right state, so
        // the loop can only ever add. Both halves are asserted: the leak, and the three that do
        // not leak - a fix that broke the vocabulary the other way would pass a one-line check.
        Console.WriteLine("\nspell states (MODULE6.BAS:2785 reads the spell mask with the gear names)");
        {
            var spellRecs = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var itemRecs = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? Look(short id) => itemRecs.FirstOrDefault(i => i.Id == id);

            // "Lev/Inv/See/Pro", a dash where the state is off, so one string shows all four
            string States(Character c) =>
                (c.Levitating ? "Lev" : "---") + "/" + (c.InvisibleNow ? "Inv" : "---") + "/"
                + (c.SeeingInvisible ? "See" : "---") + "/" + (c.Protection > 0 ? "Pro" : "---");

            foreach (var (label, effect, want) in new[]
                     {
                         ("Levitate",      SpellEffect.Levitate,     "Lev/---/See/---"),
                         ("Invisible",     SpellEffect.Invisible,    "---/Inv/---/---"),
                         ("See Invisible", SpellEffect.SeeInvisible, "---/---/See/---"),
                         ("Protect",       SpellEffect.Protect,      "---/---/---/Pro"),
                     })
            {
                var sp = spellRecs.FirstOrDefault(x => x.Effect == effect && x.Name.Trim().Length > 0);
                if (sp == null) { Console.WriteLine($"  FAIL  no spell has effect {effect}"); _failed++; continue; }
                var c = StateLeakProbe.Fresh();
                SpellEffect.ApplyToCharacter(effect, c, sp, new Random(1), Look);
                Check($"casting {label} (\"{sp.Name.Trim()}\") leaves", States(c), want);
            }

            // The same loop is correct for gear - that is what makes this a clash of two bit
            // vocabularies rather than a plain wrong table, and it is the half that must not move.
            foreach (var (bit, label, want) in new[]
                     {
                         (Effects.Levitate,     "Levitate",      "Lev/---/---/---"),
                         (Effects.Invisible,    "Invisible",     "---/Inv/---/---"),
                         (Effects.Protect,      "Protect",       "---/---/---/Pro"),
                         (Effects.SeeInvisible, "See Invisible", "---/---/See/---"),
                     })
            {
                // an item carrying that bit and no other state bit, so the reading is unambiguous
                int stateBits = (1 << Effects.Levitate) | (1 << Effects.Invisible)
                              | (1 << Effects.Protect) | (1 << Effects.SeeInvisible);
                var it = itemRecs.FirstOrDefault(i => i.Id != 0 && (i.Abilities & stateBits) == (1 << bit));
                if (it == null) { Console.WriteLine($"  FAIL  no item carries only {label}"); _failed++; continue; }
                var c = StateLeakProbe.Fresh();
                c.PutInPack(new InventorySlot { ItemId = it.Id, Charges = 1, Known = IdLevel.All, Equipped = true });
                Effects.Recompute(c, Look);
                Check($"wearing {label} (\"{it.Name.Trim()}\") leaves", States(c), want);
            }
        }

        // ---- Fate has a floor, and it is the reason the trap is survivable ----
        // sub0F29 (MODULE6.BAS:1906) writes both pools with Int(Rnd * max) + 1. The + 1 is inside
        // nothing - it is added after the Int - so the draw is 1..max and never 0. A trap that
        // rerolls your hits therefore cannot kill you, however unlucky the roll. Reached two ways,
        // chest trap 6 (MODULE4.BAS:3553) and spell effect 19, and they must stay identical
        // because they are the same routine in the original.
        //
        // The bounds and the mean are read off that line, not measured and enshrined: a uniform
        // draw over 1..40 has mean 20.5 by arithmetic. The window is 20.0..21.0 rather than anything
        // wider because dropping the + 1 gives a mean of exactly 19.5 - a bound placed there would
        // be a coin flip. Over 8,000 draws the standard error is about 0.13, so 0.5 is four of
        // them either way: wide enough not to flake, narrow enough to fail the missing floor.
        Console.WriteLine("\nFate (MODULE6.BAS:1906, Int(Rnd * max) + 1 into both pools)");
        {
            var fateSpells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var fateItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? Look(short id) => fateItems.FirstOrDefault(i => i.Id == id);
            var fateSpell = fateSpells.First(x => x.Effect == SpellEffect.Fate);
            var rng = new Random(20260922);

            foreach (var (route, apply) in new (string, Action<Character, Random>)[]
                     {
                         ("cast", (c, r) => SpellEffect.ApplyToCharacter(SpellEffect.Fate, c, fateSpell, r, Look)),
                         ("trap 6", (c, r) => ChestTraps.Spring(6, new List<Character> { c }, c, 1, r, Look, (x, y) => 0)),
                     })
            {
                var hits = new List<int>(); var sp = new List<int>();
                for (int i = 0; i < 8000; i++)
                {
                    var c = FateProbe.Subject();           // 40 max hits, 40 max spell points
                    apply(c, rng);
                    hits.Add(c.CurHits); sp.Add(c.CurSpellPoints);
                }
                Check($"{route}: hits never reach 0", hits.Min(), 1);
                Check($"{route}: hits reach the maximum", hits.Max(), 40);
                CheckNear($"{route}: mean hits, uniform 1..40 gives 20.5", hits.Average(), 20.0, 21.0);
                Check($"{route}: spell points never reach 0", sp.Min(), 1);
                Check($"{route}: spell points reach the maximum", sp.Max(), 40);
                CheckNear($"{route}: mean spell points", sp.Average(), 20.0, 21.0);
            }

            // it rerolls rather than subtracts, so on someone nearly dead it is overwhelmingly a
            // rescue - 39 of the 40 outcomes are better than 1
            int helped = 0;
            for (int i = 0; i < 2000; i++)
            {
                var c = FateProbe.Subject(); c.CurHits = 1;
                SpellEffect.ApplyToCharacter(SpellEffect.Fate, c, fateSpell, rng, Look);
                if (c.CurHits > 1) helped++;
            }
            CheckNear("on 1 hit, Fate heals (39 of 40 draws beat 1)", helped, 1900, 1990);

            // and no guild ceiling admits a level 50 spell, so the only way to cast it is the item
            var elixr = fateItems.First(i => i.Name.Trim() == "Elixr of Fate");
            var holder = FateProbe.Subject();
            holder.PutInPack(new InventorySlot
            {
                ItemId = elixr.Id, Charges = (short)Math.Max(1, elixr.Charges),
                Known = IdLevel.All, Equipped = ItemUse.MustBeEquippedToUse(elixr),
            });
            var offer = Spellbook.Castables(holder, fateSpells, Look)
                                 .FirstOrDefault(x => x.FromItem && x.Item!.Id == elixr.Id);
            Check("Elixr of Fate offers", offer?.Spell.Name.Trim() ?? "NOTHING", "Fate");
        }

        // ---- the store's uncurse: both odd answers, and one of them was called unreachable ----
        // cmdUncurse_Click (FRMCTYS.BAS:292-302) tests the *slot's* curse flag and then the slot's
        // equipped bit. Three routines set that flag and only one of them equips: sub0F01 copies it
        // on when chest treasure lands in a pack slot (MODULE4.BAS:3825) and modAwardCrest does the
        // same for a Crest (FRMCTYG.BAS:745). So the two can come apart, and this is the only place
        // in the game that reads one without the other.
        //
        // FIDELITY recorded "Item isn't equipped!" as unreachable on the grounds that the flag is
        // only ever set while the item is on. That is true of sub0F74 and of nothing else.
        Console.WriteLine("\nthe store's uncurse (FRMCTYS.BAS:292-302, against the slot flag)");
        {
            var uItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? Look(short id) => uItems.FirstOrDefault(i => i.Id == id);

            Check("flag clear, not worn", Uncursing.Refusal(new InventorySlot()) ?? "goes ahead", Uncursing.NotCursed);
            Check("flag set, not worn", Uncursing.Refusal(new InventorySlot { SlotCursed = true }) ?? "goes ahead", Uncursing.NotEquipped);
            Check("flag set, worn", Uncursing.Refusal(new InventorySlot { SlotCursed = true, Equipped = true }) ?? "goes ahead", "goes ahead");
            // the fourth combination cannot be produced by any route, and is asserted as the store
            // would answer it rather than left to be discovered
            Check("flag clear, worn", Uncursing.Refusal(new InventorySlot { Equipped = true }) ?? "goes ahead", Uncursing.NotCursed);

            // and the state that gets you there: a cursed item out of a chest that the equip guards
            // turn down. 38 of the 43 findable cursed items do that to a fresh character, so this is
            // the ordinary case rather than a corner.
            var fresh = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 10, 10, 10, 10, 10, 10 });
            var findable = uItems.Where(i => i.Id != 0 && i.Cursed != 0 && i.Floor >= 1).ToList();
            int turnedDown = findable.Count(it =>
            {
                var sl = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)Math.Max(1, it.Charges) };
                return !Character.IsWearable(it) || fresh.WearRefusal(sl, it, Look) != null;
            });
            Check("findable cursed items", findable.Count, 43);
            // a chest copies Items.Cursed into the slot whatever it is (MONSMOD.BAS:3834) but only
            // "If Items(l04FA).Cursed = gc1DD0" - 2 (MAIN.TXT:694) - puts it on (3850). The port
            // had tested Cursed != 0 for both, so a chest's Bone Dagger (Cursed 1) put itself on
            Check("cursed items of the kind 1", uItems.Count(i => i.Id != 0 && i.Cursed == 1), 38);
            Check("and of the kind 2", string.Join(", ", uItems.Where(i => i.Cursed == 2).Select(i => i.Name.Trim())),
                  "Ball and Chain, Tim's Lead Weights, Dalyn's Evil Laugh, Derf's Toe Lint, Curse of Morash");
            Check("items with any other Cursed", uItems.Count(i => i.Cursed != 0 && i.Cursed != 1 && i.Cursed != 2), 0);
            Check("a chest's kind-1 item stays in the pack (MONSMOD.BAS:3850)",
                  Loot.AttachesItself(uItems.First(i => i.Cursed == 1)), false);
            Check("a kind-2 item puts itself on", Loot.AttachesItself(uItems.First(i => i.Cursed == 2)), true);
            CheckAtLeast("of those, refused to a fresh character", turnedDown, 30);

            // end to end on one of them: the flag goes on at pickup, the equip is refused, and the
            // store gives the answer that was supposed to be impossible
            // not a Hands-class item: record 0 of that type is the pinned unarmed weapon and the
            // draw reads index 0 as "not found", so whether Hands can drop at all is a separate
            // question and this claim should not rest on it
            var stuck = findable.First(it =>
            {
                if (it.Type == 0) return false;
                var sl = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)Math.Max(1, it.Charges) };
                return Character.IsWearable(it) && fresh.WearRefusal(sl, it, Look) != null;
            });
            var found = new InventorySlot
            {
                ItemId = stuck.Id, Known = IdLevel.All, Charges = (short)Math.Max(1, stuck.Charges),
                SlotCursed = stuck.Cursed != 0,           // MODULE4.BAS:3825, before anything equips
            };
            Check($"\"{stuck.Name.Trim()}\" out of a chest, equip refused", Uncursing.Refusal(found), Uncursing.NotEquipped);

            // bought instead of found: FRMCTYS writes no curse flag at all on a purchase, so the
            // same item answers the other way round, and untruthfully
            var bought = new InventorySlot { ItemId = stuck.Id, Known = IdLevel.All };
            Check("the same item bought over the counter", Uncursing.Refusal(bought), Uncursing.NotCursed);

            // the flag's own upkeep: on when it goes on, off when it comes off (MODULE6.BAS:1786/1745)
            var wearer = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                          new[] { 25, 25, 25, 25, 25, 25 });
            var worn = new InventorySlot { ItemId = stuck.Id, Known = IdLevel.All };
            wearer.ApplyEquipCurse(worn, stuck);
            Check("equipping sets the slot flag", worn.SlotCursed, true);
            Check("and it will not come off", Character.CanUnequip(worn, stuck, wearer.Alignment), false);
            worn.SlotCursed = false;                       // what the unequip path does
            Check("cleared, it comes off again", Character.CanUnequip(worn, stuck, wearer.Alignment), true);

            // an unaligned, uncursed item equipped by someone it does not oppose leaves it clear
            var plain = uItems.First(i => i.Id != 0 && i.Cursed == 0 && Character.IsWearable(i));
            var plainSlot = new InventorySlot { ItemId = plain.Id, Known = IdLevel.All };
            wearer.ApplyEquipCurse(plainSlot, plain);
            Check($"equipping \"{plain.Name.Trim()}\" leaves it clear", plainSlot.SlotCursed, false);
        }

        // Who a window's placement is saved to. Twelve of the thirteen forms call
        // "sub1614 -1, index, Me, True" and the -1 makes that routine loop over every character
        // (MODULE6.BAS:4370-4392), so the layout belongs to the party. The automap never calls it
        // and assigns Chars(0) by hand (FRMAUTOM.BAS:173-176, 571-574).
        //
        // This entry in FIDELITY used to say the opposite - that every writer uses slot 0, citing
        // the automap as though it were the pattern. It is the one exception.
        Console.WriteLine("\nwho a window's placement is saved to (sub1614's -1 against FRMAUTOM)");
        {
            Check("the automap reaches character 0 only",
                  WindowLayout.PlacementSavedToWholeParty(GameWindow.Automap), false);
            foreach (var w in new[] { GameWindow.Character, GameWindow.ObjectsAndSpells, GameWindow.Messages,
                                      GameWindow.Group, GameWindow.Dungeon, GameWindow.GeneralStore,
                                      GameWindow.Bank, GameWindow.Morgue, GameWindow.Guild,
                                      GameWindow.Seer, GameWindow.CompanionStore, GameWindow.City })
                Check($"{w} goes to the whole party", WindowLayout.PlacementSavedToWholeParty(w), true);
            // The twelve above are exactly the twelve sub1614 call sites, and the automap is the
            // thirteenth form. Fourteen table entries carry a default, so one has a size and no
            // form at all: index 14, the Unnamed window, which nothing ever saves or reads. Naming
            // the leftover is the point - it is what says the twelve-plus-one is the whole
            // population rather than as far as the list happened to go.
            var sized = Enum.GetValues<GameWindow>().Where(w => w >= 0 && WindowLayout.Default(w).W > 0).ToList();
            Check("table entries with a default size", sized.Count, 14);
            Check("of those, forms that save their own placement", sized.Count(w => w != GameWindow.Unnamed), 13);
            Check("the leftover is the formless one", string.Join(",", sized.Where(w => w == GameWindow.Unnamed)), "Unnamed");
        }

        // ---- neither shelf ever reaches the fifteenth floor ----
        // mdrReplenishStore and mdrReplenishCStore both read the deepest level anyone has reached
        // and cap it with "If x >= mc0118 - 1 Then x = mc0118 - 1" (MAINMOD.BAS:1506 and 1308),
        // where mc0118 is 15. Three other uses of that constant in the same file are loop bounds
        // running 1 to 15, so 15 is the floor count and both caps are one short.
        //
        // Asserting the constant alone would be worth little - it is the draw that matters, and the
        // draw takes items with Floor <= the chosen floor, so the cap has to bite all the way
        // through. Both replenishers are run with the deepest level set to the true bottom, which
        // is what a caller would pass once someone has been to 15.
        Console.WriteLine("\nthe fifteenth floor against the two shelves (MAINMOD.BAS:1506, 1308)");
        {
            var dItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var dMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));

            Check("the cap", StoreStock.DeepestStocked, 14);
            Check("items whose only floor is 15", dItems.Count(i => i.Id != 0 && i.Floor == 15), 22);
            Check("creatures found no shallower than 15", dMons.Count(m => m.Id != 0 && m.LevelFound >= 15), 36);

            var shelf = new StoreStock();
            var srng = new Random(20260922);
            for (int i = 0; i < 6000; i++) shelf.Replenish(dItems, 50, srng, deepestReached: 15);
            var floors = shelf.Shelf.Where(kv => kv.Value > 0)
                                    .Select(kv => short.Parse(kv.Key.Split(":")[0]))
                                    .Distinct()
                                    .Select(id => (int)(dItems.FirstOrDefault(i => i.Id == id)?.Floor ?? 0))
                                    .ToList();
            CheckAtLeast("store: distinct items stocked", floors.Count, 100);
            Check("store: any from floor 15", floors.Count(f => f >= 15), 0);
            // the boundary has to be exercised or the check would also pass with the cap at 5
            Check("store: the deepest floor that did appear", floors.DefaultIfEmpty(0).Max(), 14);

            var pens = new Confinement();
            var prng = new Random(20260922);
            for (int i = 0; i < 6000; i++) pens.Replenish(dMons, 50, prng, deepestReached: 15);
            var levels = pens.Entries.Where(e => e.Count > 0)
                                  .Select(e => e.Ref)
                                  .Distinct()
                                  .Select(r => (int)(dMons.FirstOrDefault(m => m.Ref == r)?.LevelFound ?? 0))
                                  .ToList();
            CheckAtLeast("pens: distinct creatures penned", levels.Count, 100);
            Check("pens: any found at 15 or deeper", levels.Count(l => l >= 15), 0);
            Check("pens: the deepest that did appear", levels.DefaultIfEmpty(0).Max(), 14);

            // Two items sit at floor 16, past the bottom of a fifteen-floor dungeon, and so do ten
            // monsters. They are not orphans: the drop band comes from the monster's own
            // ItemDropLevel rather than from the floor, and seven monsters carry 16.
            Check("items past the bottom floor", dItems.Count(i => i.Id != 0 && i.Floor > 15), 2);
            Check("monsters that can drop that deep",
                  dMons.Count(m => m.Id != 0 && m.ItemDropLevel >= 16), 7);
        }

        // ---- the quote that is opened and never closed ----
        // Chr$(34) is a double quote. Three lines in the whole decompiled source use an odd number
        // of them, and all three have the same shape - RTrim(Name) + " - " + Chr$(34) + speech:
        //
        //   FRMDUNM.BAS:596   "Help! My hits are ...      timAilment_Timer, while you walk about
        //   MODULE4.BAS:3404  "Help! My hits are ...      sub0E85, after a group attacks
        //   MODULE4.BAS:3290  "I've been Paralyzed!       the same, on a paralysis
        //
        // Every other Chr$(34) in the source comes in pairs, wrapping a name or a spell title. So
        // this is not the house style of one routine, as this file used to say - it is what the
        // game does every time a character speaks, and it does that in exactly two messages.
        //
        // The scan is the evidence for "three", so it is done here rather than asserted in prose:
        // a fourth speech line appearing later would be a fourth reproduction to make.
        Console.WriteLine("\nunbalanced quotes in the decompiled source (Chr$(34) counted per line)");
        {
            string decompiled = PortTree.At("decompiled");
            var odd = new List<string>();
            if (System.IO.Directory.Exists(decompiled))
                foreach (string f in System.IO.Directory.GetFiles(decompiled, "*.BAS"))
                {
                    var lines = System.IO.File.ReadAllLines(f);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int n = System.Text.RegularExpressions.Regex.Matches(lines[i], @"Chr\$\(34\)").Count;
                        if (n % 2 == 1) odd.Add($"{System.IO.Path.GetFileName(f)}:{i + 1}");
                    }
                }
            if (!System.IO.Directory.Exists(decompiled)) SourceMissing("decompiled/");
            else
            {
                Check("lines with an odd number of quote characters", odd.Count, 3);
                Check("and which they are", string.Join(" ", odd.OrderBy(x => x)),
                      "FRMDUNM.BAS:596 MODULE4.BAS:3290 MODULE4.BAS:3404");
            }

            // the port's two, built from the same shape
            var hurt = Character.Create("Brannoc", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                        new[] { 10, 10, 10, 10, 10, 10 });
            hurt.MaxHits = 15; hurt.CurHits = 5;
            Check("the cry", HelpCry.Line(hurt), "Brannoc - \"Help! My hits are 5/15!");
            Check("one quote in it", HelpCry.Line(hurt).Count(ch => ch == '"'), 1);
            Check("the paralysis line", HelpCry.Paralysis(hurt), "Brannoc - \"I've been Paralyzed!");
            Check("one quote in that too", HelpCry.Paralysis(hurt).Count(ch => ch == '"'), 1);
            // the watched character gets a caption instead, and a caption is not speech
            Check("what the watched character sees", HelpCry.ParalysisWatched, "You have been Paralyzed!");
            Check("with no quote at all", HelpCry.ParalysisWatched.Count(ch => ch == '"'), 0);

            // and the threshold both callers test: under ten, or under a tenth of the maximum
            hurt.MaxHits = 500; hurt.CurHits = 50;
            Check("fifty of five hundred is a tenth, so still a cry", HelpCry.Needed(hurt), true);
            hurt.CurHits = 51;
            Check("fifty-one is not", HelpCry.Needed(hurt), false);
            hurt.MaxHits = 20; hurt.CurHits = 9;
            Check("nine hits always cries, whatever the maximum", HelpCry.Needed(hurt), true);
            hurt.CurHits = 10;
            Check("ten of twenty does not", HelpCry.Needed(hurt), false);
        }

        // ---- two of monGetGold's four wordings cannot be printed ----
        // fn0DBF (MONSMOD.BAS:2121-2178) folds everybody's recovered gold into the found gold
        // first, then wraps the whole announcement in "If l039C > 0" and tests l039C again inside
        // it. Both else-branches need it falsy inside a block that already requires it positive.
        //
        // The second is dead twice over: reaching it needs the selected character's recovered gold
        // to be nonzero, and that figure is already part of l039C.
        Console.WriteLine("\nthe gold announcement (MONSMOD.BAS:2154-2167)");
        {
            var seen = new HashSet<string>();
            for (long found = 0; found <= 3; found++)
                for (long mine = 0; mine <= 3; mine++)
                    for (int party = 1; party <= 4; party++)
                    {
                        // mine is part of the pot, exactly as l039C = l039C + l03A4 makes it
                        long pot = found + mine;
                        // the numbers differ per combination; it is the wordings being counted
                        string shape = GoldShare.Line(pot, mine, party) is string s
                            ? System.Text.RegularExpressions.Regex.Replace(s, "[0-9,]+", "N")
                            : "(nothing printed)";
                        seen.Add(shape);
                    }

            Check("distinct wordings produced", seen.Count, 3);   // two lines, plus saying nothing
            Check("of those, actual sentences", seen.Count(s => s.StartsWith("Your share")), 2);
            Check("the no-gold branch", seen.Any(s => s == GoldShare.DeadNoGold), false);
            Check("the only-recovered branch",
                  seen.Any(s => s.StartsWith("You didn't find any gold, but")), false);
            Check("nothing at all is printed when the pot is empty",
                  GoldShare.Line(0, 0, 4) ?? "(nothing printed)", "(nothing printed)");

            // the two that are reachable, with the figures they quote
            Check("found only", GoldShare.Line(100, 0, 4), "Your share is 25 Gold!");
            Check("found and recovered", GoldShare.Line(140, 40, 4),
                  "Your share is 35 and you recovered 40 Gold!");

            // The share is the whole pot over the party, but the sentence is chosen by the
            // *selected* character's own recovered gold. Somebody else being robbed and getting it
            // back is in the pot and not in the wording - the port used to use a party-wide total
            // for both, which said "you recovered" to a character who had not been touched.
            Check("another member's recovery is in the share", GoldShare.Line(140, 0, 4),
                  "Your share is 35 Gold!");

            // CLng rounds half to even and everybody gets the same figure, so the remainder is
            // lost rather than handed to the first in line
            Check("ten between four, rounded half to even", GoldShare.Share(10, 4), 2);
            Check("fourteen between four", GoldShare.Share(14, 4), 4);
            Check("four members at 2 each from a pot of 10 loses 2", GoldShare.Share(10, 4) * 4, 8);
        }

        // ---- the automap sheet: cell number is flag bit number, and one half of the blitter is dead ----
        // medDrawSquare (sub2E7F, FRMAUTOM.BAS:352-392) walks "For l0104 = 0 To 22" over the
        // square's flag word and blits from source x = l0104 * 15, so cell number = flag bit
        // number. Cells 23-26 are outside that loop: the magenta "you are here" arrow, blitted as
        // Facing + 22 with the original's one-based Facing.
        //
        // The routine has a second path. When a bit is *not* set it tests "If l0104 = pv00FE" and
        // blits that cell with a different raster op - 70, whose low word matches SRCINVERT
        // (0x00660046), against 134 for SRCPAINT (0x00EE0086) on the set-bit path. Nothing ever
        // asks for it: all nine call sites pass -1, which no bit index can equal.
        Console.WriteLine("\nthe automap sheet (FRMAUTOM.BAS:352-392)");
        {
            string autom = PortTree.At("decompiled", "FRMAUTOM.BAS");
            if (!System.IO.File.Exists(autom)) SourceMissing("FRMAUTOM.BAS");
            else
            {
                var calls = System.IO.File.ReadAllLines(autom)
                    .Where(l => l.Contains("sub2E7F") && !l.TrimStart().StartsWith("Sub "))
                    .ToList();
                Check("calls to medDrawSquare", calls.Count, 9);
                Check("of those, passing -1 for the bit to invert", calls.Count(l => l.Contains(", -1, ")), 9);
            }

            // Cell = bit, so the sheet is indexed by the Floor flag numbering itself. The loop
            // stops at 22, and that is a neutraliser rather than a coincidence: bit 23 is a real
            // flag - RESCUE - and cell 23 is the first of the four magenta "you are here" arrows.
            // A loop running to 23 would put a north arrow on every rescue square.
            Check("the last bit the loop draws", Floor.STUD, 22);
            Check("the first bit it does not", Floor.RESCUE, 23);
            Check("27 cells of 15 is the sheet the artwork check measures", 27 * 15, 405);
            Check("23 icons plus four arrows fills it", 23 + 4, 27);

            // The four FACE bits are forced-facing squares, not a drawing of where you are
            // standing, and perception both notices them and writes them to the map - four
            // consecutive sub087C call sites at MAZEMOD.BAS:1709-1723, each setting the
            // character's facing afterwards. This file used to say they had no writer at all.
            foreach (int b in new[] { Floor.FACE_NORTH, Floor.FACE_EAST, Floor.FACE_SOUTH, Floor.FACE_WEST })
                Check($"perception notices bit {b}", Perception.Noticeable.Contains(b), true);
            Check("and it notices this many things in all", Perception.Noticeable.Length, 8);
        }

        // ---- one call of mdrmazPerceiveSquare (MAZEMOD.BAS:610-712) ----
        // sub0835 calls it for the teleporter at 1790 as for the others, and for the teleporter it
        // skips the perception roll and the already-mapped test, always names it, writes it to the
        // automap unless the leader is lost at that moment (638), and says only "You just hit a
        // teleporter!" (664-670). The "no perception messages" option stops even that (620).
        Console.WriteLine("\nperceiving the square you stand on (MAZEMOD.BAS:610-712)");
        {
            var prng = new Random(3);
            var tp = Perception.PerceiveSquare(prng, Floor.TELEPORTER, 0, "A", false, false, true, false);
            Check("a teleporter with no perception at all, already mapped", tp.Line ?? "(nothing)", "You just hit a teleporter!");
            Check("and it goes on the automap", tp.MapIt, true);
            var tpParty = Perception.PerceiveSquare(prng, Floor.TELEPORTER, 0, "A", true, false, false, true);
            Check("in a party", tpParty.Line ?? "(nothing)", "The party just hit a teleporter!");
            Check("lost, it is not mapped", tpParty.MapIt, false);
            var quiet = Perception.PerceiveSquare(prng, Floor.TELEPORTER, 100, "A", false, true, false, false);
            Check("with perception messages off, nothing", (quiet.Line ?? "(nothing)", quiet.MapIt), ("(nothing)", false));
            var blind = Perception.PerceiveSquare(prng, Floor.ROTATOR, 0, "A", false, false, false, false);
            Check("a rotator with no perception, nothing", blind.Line ?? "(nothing)", "(nothing)");
            var known = Perception.PerceiveSquare(prng, Floor.ROTATOR, 100, "A", false, false, true, false);
            Check("a rotator already on the map, nothing", known.Line ?? "(nothing)", "(nothing)");
            // perception 200 passes both rolls, so the rotator is named and mapped, "completely"
            var seen = Perception.PerceiveSquare(prng, Floor.ROTATOR, 200, "A", false, false, false, false);
            Check("a rotator seen clearly", seen.Line ?? "(nothing)", "You are completely certain you're standing on a Rotator square.");
            var seenLost = Perception.PerceiveSquare(prng, Floor.ROTATOR, 200, "A", false, false, false, true);
            Check("the same when lost: named, not mapped, not completely",
                  (seenLost.MapIt, seenLost.Line ?? ""), (false, "You are absolutely certain you're standing on a Rotator square."));
        }

        // ---- the perception wordings, against the source text rather than a copy of it ----
        // mdrmazPerceiveAhead writes its eight features' lines out twice, once for a lone
        // character and once for a party (MAZEMOD.BAS:515-588). Two of the eight pairs disagree
        // with themselves - "a rotator" against "a Rotator", "an Extinguish square" against "an
        // Extinguish Square" - and both spellings are the original's, eight lines apart.
        //
        // These are pulled out of MODULE3.BAS itself and compared with what the port would say, so
        // a typo in either cannot survive. Nothing here is a string this port produced and kept.
        Console.WriteLine("\nperception wordings (MAZEMOD.BAS:515-588, read out of the file)");
        {
            string maze = PortTree.At("decompiled", "MODULE3.BAS");
            if (!System.IO.File.Exists(maze)) SourceMissing("MODULE3.BAS");
            else
            {
                string src = System.IO.File.ReadAllText(maze);
                var alone = System.Text.RegularExpressions.Regex.Matches(src, "\"(You detect [^\"]*)\"")
                             .Select(m => m.Groups[1].Value).ToList();
                var party = System.Text.RegularExpressions.Regex.Matches(src, "\"( detects [^\"]*)\"")
                             .Select(m => m.Groups[1].Value).ToList();
                Check("lone-character lines in the source", alone.Count, 8);
                Check("party lines in the source", party.Count, 8);

                // the port's, in the order the routine tests the bits
                // compared line by line and reported by name: the whole set concatenated makes an
                // unreadable row and says no more than the count of what did not match
                var mine = PerceiveAhead.Features.Select(PerceiveAhead.Alone).ToList();
                var ours = PerceiveAhead.Features
                    .Select(b => PerceiveAhead.InParty("X", b).Substring(3)).ToList();
                var wrong = new List<string>();
                for (int i = 0; i < Math.Min(8, alone.Count); i++)
                    if (mine[i] != alone[i]) wrong.Add("lone " + i);
                for (int i = 0; i < Math.Min(8, party.Count); i++)
                    if (ours[i] != party[i]) wrong.Add("party " + i);
                Check("lines that do not match the source", string.Join(" ", wrong), "");

                // and the two pairs that disagree, named so the disagreement cannot be tidied away
                Check("rotator, alone", alone[6], "You detect a rotator ahead of you.");
                Check("Rotator, in a party", party[6], " detects a Rotator ahead of the party.");
                Check("Extinguish square, alone", alone[7], "You detect an Extinguish square ahead of you.");
                Check("Extinguish Square, in a party", party[7], " detects an Extinguish Square ahead of the party.");
                Check("pairs that disagree on case", Enumerable.Range(0, 8).Count(i =>
                    alone[i].Replace("You detect ", "").Replace(" ahead of you.", "")
                    != party[i].Replace(" detects ", "").Replace(" ahead of the party.", "")), 2);
            }

            // The quicksand branch is the only one of the eight with a suppression test, and it is
            // not a fix for the repetition: MAZEMOD.BAS:517 writes the bit to the map *before*
            // MAZEMOD.BAS:518 decides whether to speak, and the test asks whether you are standing
            // in quicksand yourself, not whether you have been told before.
            {
                var fl = new Floor { Width = 4, Height = 4, LevelNum = 1 };
                fl.Fields = new Floor.Field[16];
                void Set(int x, int y, int bit) => fl.Fields[y * 4 + x].Flags |= 1L << bit;

                // standing at (1,1) and facing north, which is y + 1 in this port
                Set(1, 2, Floor.QUICKSAND);
                var map = new SharedMap();
                var rng = new Random(1);

                // on firm ground: it is announced
                var heard = PerceiveAhead.Look(map, fl, 1, 1, Facing.North, rng, 100, "X", false);
                Check("quicksand ahead, on firm ground", string.Join("", heard),
                      "You detect Quicksand ahead of you.");
                Check("and it is on the map", map.Knows(1, 1, 2, 4, Floor.QUICKSAND), true);

                // standing in quicksand: nothing is said, and the bit is written anyway
                Set(1, 1, Floor.QUICKSAND);
                var map2 = new SharedMap();
                var quiet = PerceiveAhead.Look(map2, fl, 1, 1, Facing.North, rng, 100, "X", false);
                Check("standing in it, nothing is said", quiet.Count, 0);
                Check("but the map is written regardless", map2.Knows(1, 1, 2, 4, Floor.QUICKSAND), true);

                // and the suppression is quicksand's alone - a pit ahead still speaks while you
                // are stuck, which is what says this was never a rule about repetition
                var fl2 = new Floor { Width = 4, Height = 4, LevelNum = 1, Fields = new Floor.Field[16] };
                fl2.Fields[2 * 4 + 1].Flags |= 1L << Floor.PIT;         // ahead
                fl2.Fields[1 * 4 + 1].Flags |= 1L << Floor.QUICKSAND;   // underfoot
                var said2 = PerceiveAhead.Look(new SharedMap(), fl2, 1, 1, Facing.North, rng, 100, "X", false);
                Check("a pit ahead speaks even from inside quicksand", string.Join("", said2),
                      "You detect a Pit ahead of you.");
            }

            // Detect Rock's two answers, twenty lines apart and neither a slip for the other
            string mm2 = PortTree.At("decompiled", "MODULE6.BAS");
            if (System.IO.File.Exists(mm2))
            {
                string src = System.IO.File.ReadAllText(mm2);
                Check("the square ahead is", src.Contains("\"The square ahead is Rock.\""), true);
                Check("and when it is not", src.Contains("\"The ahead square is not Rock.\""), true);
            }
        }

        // ---- the theft roll is wrong at both ends ----
        // MONSMOD.BAS:3351 is Int(Rnd * (mc04D0 - 1)) + 2 with mc04D0 = 40, so the draw is 2 to 40
        // against a pack numbered 0 to 39. Forty reads off the end; one is never drawn at all.
        // FIDELITY recorded only the overrun, and the other half is the one that shows in play.
        Console.WriteLine("\nwhich pack slot a thief reaches into (MONSMOD.BAS:3351)");
        {
            var rng = new Random(20260922);
            var hit = new int[42];
            for (int i = 0; i < 120_000; i++) hit[CombatEngine.TheftSlot(rng)]++;

            Check("slot 0, the Hands, is never drawn", hit[0], 0);
            Check("slot 1 is never drawn either", hit[1], 0);
            Check("slots 2 to 39 that were never drawn",
                  string.Join(" ", Enumerable.Range(2, 38).Where(s => hit[s] == 0)), "");
            CheckAtLeast("slot 40, one past the end, is drawn", hit[40], 1);
            Check("and nothing beyond it", hit[41], 0);

            // the shape of the draw: 39 outcomes, evenly, so the overrun is one roll in 39 and
            // slot 1 is not merely rare
            Check("distinct outcomes", Enumerable.Range(0, 42).Count(s => hit[s] > 0), 39);
            int lo = Enumerable.Range(2, 39).Min(s => hit[s]);
            int hi = Enumerable.Range(2, 39).Max(s => hit[s]);
            Check("and they share the draw evenly", hi - lo < lo / 5, true);

            // Character.PackSize is what makes slot 40 harmless here: the port tests it before
            // indexing where the original relies on its error handler
            Check("the pack the draw is read against", Character.PackSize, 40);
        }

        // ---- a Teleport or an Ethereal Portal into rock ----
        // Nothing moves you off rock. mdrcharTeleport writes X, Y and DungeonLevel and only then
        // tests them (MAINMOD2.BAS:5786-5833, 5839, 5860); Ethereal Portal steps and then tests
        // (1170-1184). Either way a rock square is a death and the bodies lie on it. The port's
        // SetPosition used to snap to the nearest open square, which let a Teleport into rock
        // through alive and saved a Passwall's bodies beside the rock instead of in it. The window
        // asks Teleporting.ForSpell before it moves anybody, and DungeonNav.StepThrough moves and
        // then answers; both are what is tested here.
        Console.WriteLine("\nteleport and passwall into rock (MAINMOD2.BAS:5839-5896, 1170-1206)");
        {
            var floors = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            var fl = floors[0];
            // a rock square with an open square just south of it, both off the edge
            int rx = -1, ry = -1;
            for (int yy = 1; yy < fl.Height - 1 && rx < 0; yy++)
                for (int xx = 1; xx < fl.Width - 1; xx++)
                    if (fl.Has(xx, yy, Floor.ROCK) && !fl.Has(xx, yy - 1, Floor.ROCK)) { rx = xx; ry = yy; break; }
            Check("a rock square on level 1 with open ground south of it", rx >= 0, true);
            if (rx >= 0)
            {
                // Passwall, facing north from the open square: moved, then tested
                var nav = new DungeonNav(fl, rx, ry - 1, Facing.North);
                Check("passwall into rock is a death", nav.StepThrough(), true);
                Check("and leaves the caster on the rock square", (nav.X, nav.Y), (rx, ry));
                Check("which is not marked as walked on", nav.Explored.Contains((rx, ry)), false);

                // Teleport "+0,+1" from the same square: decided before any move
                nav = new DungeonNav(fl, rx, ry - 1, Facing.North);
                var to = SpellEffect.TeleportTo(nav.X, nav.Y, nav.Facing, 1, 0, 1, 0, fl.Width, fl.Height);
                Check("teleport into rock is a death",
                      Teleporting.ForSpell(floors, to.Level, to.X, to.Y), Teleporting.Outcome.IntoRock);
                Check("and names the rock square for the bodies", (to.X, to.Y, to.Level), (rx, ry, 1));
                nav.SetPosition(to.X, to.Y);   // TeleportedIntoRock's placement
                Check("placing them there leaves them there", (nav.X, nav.Y), (rx, ry));
                Check("not marked as walked on", nav.Explored.Contains((rx, ry)), false);
                Check("the open square arrives",
                      Teleporting.ForSpell(floors, 1, rx, ry - 1), Teleporting.Outcome.Arrives);
            }
            Check("up out of level 1 is the fall (5839)",
                  Teleporting.ForSpell(floors, 0, 0, 0), Teleporting.Outcome.FallsFromTheSky);
            Check("past the deepest level is rock (5860)",
                  Teleporting.ForSpell(floors, SpellEffect.MaxDungeonLevel + 1, 0, 0), Teleporting.Outcome.IntoRock);
            Check("the deepest level (mc0276)", SpellEffect.MaxDungeonLevel, 15);
            FullCheck("levels in the shipped dungeon", floors.Count, SpellEffect.MaxDungeonLevel);
        }

        // ---- teleporter squares ----
        // A teleporter square is sub0835's own code (MAZEMOD.BAS:1788-1830), not mdrcharTeleport,
        // which only the spells call. It writes X, Y and DungeonLevel for each member and tests
        // neither rock nor level 0, so a fixed destination is taken as it is. A random one is drawn
        // for each member separately until it is neither rock nor a teleporter (1798-1808), and
        // sub0908 then drops whoever is not on the leader's square (1878). The data is checked for
        // what the missing tests would let through.
        Console.WriteLine("\nevery teleporter square in the dungeon (MDATA11 against MAZEMOD.BAS:1788-1830)");
        {
            var floors = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            int total = 0, random = 0, fixedDest = 0, offLevel = 0, randomOffLevel = 0;
            var bad = new List<string>();
            var pastBottom = new List<string>();

            foreach (var fl in floors)
                foreach (var t in fl.Teleporters)
                {
                    total++;
                    if (t.X2 == 0 || t.Y2 == 0)
                    {
                        random++;
                        if (t.Z2 > 0 && t.Z2 != fl.LevelNum) randomOffLevel++;
                        continue;
                    }
                    fixedDest++;
                    int to = t.Z2 > 0 ? t.Z2 : fl.LevelNum;
                    if (to != fl.LevelNum) offLevel++;
                    var dest = floors.FirstOrDefault(f => f.LevelNum == to);
                    // with no test in the square routine, any of these would put a live party there
                    bool outside = dest == null || t.X2 < 1 || t.Y2 < 1
                                || t.X2 > dest.Width || t.Y2 > dest.Height
                                || dest.Has(t.X2 - 1, t.Y2 - 1, Floor.ROCK);
                    if (outside) bad.Add($"L{fl.LevelNum} {t.X},{t.Y}");
                    if (outside && to > floors.Count) pastBottom.Add($"L{fl.LevelNum} {t.X},{t.Y}");
                }

            // the shareware's: a teleporter to a level its three do not reach "doesn't go anywhere
            // in the limited version" (MAZEMOD.BAS:1794-1796); none of the others may lead off the map
            if (_shareware)
                Check("shareware: fixed destinations off the map or in rock, but for those past its last level (dead in the limited build)",
                      string.Join(" ", bad.Except(pastBottom)), "");

            FullCheck("teleporters in the dungeon", total, 86);
            FullCheck("fixed destinations off the map or in rock", string.Join(" ", bad), "");
            // the two populations, so a loader that dropped one kind would not pass unnoticed
            FullCheck("with a fixed destination", fixedDest, 48);
            FullCheck("drawn at random", random, 38);
            CheckAtLeast("fixed ones that change level", offLevel, 1);
            Check("random ones that change level", randomOffLevel, 0);

            // After a teleporter the whole square routine runs again on the new square (l0286,
            // MAZEMOD.BAS:1830, 1891-1893), with no guard, so a fixed destination that is itself a
            // teleporter fires it in turn. A random one never lands on a teleporter (1798-1808), so
            // only a ring of fixed ones could go round for ever. Followed from every teleporter.
            int ontoTeleporter = 0, loops = 0, longest = 0;
            foreach (var fl in floors)
                foreach (var start in fl.Teleporters)
                {
                    var seen = new HashSet<(int, int, int)>();
                    int level = fl.LevelNum; var t = start; int hops = 0;
                    while (true)
                    {
                        if (!seen.Add((level, t.X, t.Y))) { loops++; break; }
                        if (t.X2 == 0 || t.Y2 == 0) break;
                        int nl = t.Z2 > 0 ? t.Z2 : level;
                        var nf = floors.FirstOrDefault(f => f.LevelNum == nl);
                        if (nf == null) break;
                        var next = nf.Teleporters.Where(n => n.X == t.X2 && n.Y == t.Y2).ToList();
                        if (next.Count == 0) break;
                        if (hops == 0) ontoTeleporter++;
                        level = nl; t = next[0]; hops++;
                    }
                    longest = Math.Max(longest, hops);
                }
            // and by the square's bit, which is what sub0835 tests first (1788)
            int ontoBit = 0, ontoOther = 0;
            foreach (var fl in floors)
                foreach (var t in fl.Teleporters.Where(t => t.X2 != 0 && t.Y2 != 0))
                {
                    var nf = floors.FirstOrDefault(f => f.LevelNum == (t.Z2 > 0 ? t.Z2 : fl.LevelNum));
                    if (nf == null) continue;
                    int dx = t.X2 - 1, dy = t.Y2 - 1;
                    if (nf.Has(dx, dy, Floor.TELEPORTER)) ontoBit++;
                    if (nf.Has(dx, dy, Floor.CHUTE) || nf.Has(dx, dy, Floor.PIT) || nf.Has(dx, dy, Floor.WATER)
                        || nf.Has(dx, dy, Floor.QUICKSAND) || nf.Has(dx, dy, Floor.ROTATOR)
                        || nf.Has(dx, dy, Floor.EXTINGUISHER)) ontoOther++;
                }
            Console.WriteLine($"  fixed teleporters landing on another teleporter: {ontoTeleporter} by record, {ontoBit} by bit;"
                            + $" longest chain {longest}; on a chute, pit, water, quicksand, rotator or extinguisher: {ontoOther}");
            Check("fixed destinations that are teleporter squares", ontoBit, ontoTeleporter);
            Check("teleporter chains that go round for ever", loops, 0);

            // sub0835 takes a square's effects in one pass and in a fixed order: water and
            // quicksand, the extinguisher's sound, the four facing bits, the rotator, then for each
            // member the extinguisher and the pit, and the stairs captions (MAZEMOD.BAS:1703-1786);
            // then the teleporter (1788) and the chute (1839). So a teleporter square that carries
            // any of the others has them done first. How many shipped squares that touches:
            int[] before = { Floor.WATER, Floor.QUICKSAND, Floor.EXTINGUISHER, Floor.FACE_NORTH, Floor.FACE_EAST,
                             Floor.FACE_SOUTH, Floor.FACE_WEST, Floor.ROTATOR, Floor.PIT, Floor.CHUTE };
            int tpSquares = 0, tpShared = 0;
            var sharedBits = new SortedDictionary<int, int>();
            foreach (var fl in floors)
                for (int yy = 0; yy < fl.Height; yy++)
                    for (int xx = 0; xx < fl.Width; xx++)
                    {
                        if (!fl.Has(xx, yy, Floor.TELEPORTER)) continue;
                        tpSquares++;
                        var with = before.Where(b => fl.Has(xx, yy, b)).ToList();
                        if (with.Count > 0) tpShared++;
                        foreach (int b in with) sharedBits[b] = sharedBits.GetValueOrDefault(b) + 1;
                    }
            Console.WriteLine($"  teleporter squares: {tpSquares}; also carrying water, quicksand, extinguisher, a facing bit,"
                            + $" rotator, pit or chute: {tpShared}"
                            + (sharedBits.Count == 0 ? "" : " (" + string.Join(", ", sharedBits.Select(kv => $"bit {kv.Key} x{kv.Value}")) + ")"));
            FullCheck("teleporter squares that share a square effect", tpShared, 5);
            FullCheck("and what they share (water, quicksand)",
                  $"{sharedBits.GetValueOrDefault(Floor.WATER)} {sharedBits.GetValueOrDefault(Floor.QUICKSAND)}", "4 1");
            // the same for the chute, which comes after all of them and after the teleporter too
            int chSquares = 0, chShared = 0;
            var chBits = new SortedDictionary<int, int>();
            foreach (var fl in floors)
                for (int yy = 0; yy < fl.Height; yy++)
                    for (int xx = 0; xx < fl.Width; xx++)
                    {
                        if (!fl.Has(xx, yy, Floor.CHUTE)) continue;
                        chSquares++;
                        var with = before.Where(b => b != Floor.CHUTE && fl.Has(xx, yy, b)).ToList();
                        if (fl.Has(xx, yy, Floor.TELEPORTER)) with.Add(Floor.TELEPORTER);
                        if (with.Count > 0) chShared++;
                        foreach (int b in with) chBits[b] = chBits.GetValueOrDefault(b) + 1;
                    }
            Console.WriteLine($"  chute squares: {chSquares}; also carrying one of those or a teleporter: {chShared}"
                            + (chBits.Count == 0 ? "" : " (" + string.Join(", ", chBits.Select(kv => $"bit {kv.Key} x{kv.Value}")) + ")"));
            Check("chute squares that share a square effect", chShared, 0);

            // How much is "none of them is rock" worth? Rock is 7.8% of the dungeon, so 48
            // destinations placed at random would all miss it about one time in fifty. That is
            // evidence the destinations were chosen rather than proof of it, and the figure is
            // checked here so the strength of the claim is visible instead of assumed.
            long rock = 0, squares = 0;
            foreach (var fl in floors)
                for (int yy = 0; yy < fl.Height; yy++)
                    for (int xx = 0; xx < fl.Width; xx++)
                    { squares++; if (fl.Has(xx, yy, Floor.ROCK)) rock++; }
            FullCheck("squares in the dungeon", squares, 15 * 30 * 30);
            CheckNear("of those, rock, as a percentage", rock * 100.0 / squares, 7, 9);

            // what Teleporting.ForSquare does with them, for a party of four
            var trng = new Random(11);
            var home = floors.First(f => f.Teleporters.Any(t => t.X2 == 0 || t.Y2 == 0));
            var rt = home.Teleporters.First(t => t.X2 == 0 || t.Y2 == 0);
            int landedBad = 0, scattered = 0;
            for (int n = 0; n < 200; n++)
            {
                var d = Teleporting.ForSquare(floors, home.LevelNum, rt, 4, trng);
                foreach (var (l, x, y) in d)
                    if (floors[l - 1].Has(x, y, Floor.ROCK) || floors[l - 1].Has(x, y, Floor.TELEPORTER)) landedBad++;
                if (d.Skip(1).Any(p => p != d[0])) scattered++;
            }
            Check("random draws on rock or a teleporter", landedBad, 0);
            CheckAtLeast("of 200 random jumps, the party split up in", scattered, 195);
            var ft = floors.SelectMany(f => f.Teleporters.Where(t => t.X2 != 0 && t.Y2 != 0 && t.Z2 > 0 && t.Z2 != f.LevelNum)
                                                          .Select(t => (f, t))).First();
            var fd = Teleporting.ForSquare(floors, ft.f.LevelNum, ft.t, 4, trng);
            Check("a fixed one puts all four on its square",
                  fd.All(p => p == (ft.t.Z2, ft.t.X2 - 1, ft.t.Y2 - 1)), true);
        }

        // ---- zero and minus one mean opposite things to the two equip routines ----
        // A class maximum of 0 stops sub0F74 dead ("If gv15C6(l02F8).M54F1 = 0 Then Exit Sub",
        // MODULE6.BAS:1724) but reads to fn11B6 as "no cap at all" ("If gv15C6(l0162).M54F1 >
        // False", MODULE6.BAS:576). For the consumable classes that is harmless - the toggle
        // refuses first and fn11B6 is never reached. Miscellaneous Object carries -1, which is not
        // 0: the toggle lets it through and fn11B6 then declines to cap it.
        Console.WriteLine("\nthe class maximum (MODULE6.BAS:1724 against :576)");
        {
            var moItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? MoLook(short id) => moItems.FirstOrDefault(i => i.Id == id);

            // the three values MDATA1 holds, and how many classes carry each
            var byMax = Enumerable.Range(0, Mdata1.ItemDescriptions.Count)
                                  .GroupBy(Character.MaxWornOfClass)
                                  .ToDictionary(g => g.Key, g => g.Count());
            Check("distinct class maxima in MDATA1", string.Join(",", byMax.Keys.OrderBy(k => k)), "-1,0,1");
            Check("classes that cap at one", byMax[1], 15);
            Check("classes that refuse the toggle outright", byMax[0], 8);
            Check("classes that carry minus one", byMax[-1], 1);

            // -1 gets past IsWearable, which is the whole fault: the port tests != 0 and not < 1
            int uncapped = Enumerable.Range(0, Mdata1.ItemDescriptions.Count)
                                     .Single(c => Character.MaxWornOfClass(c) == -1);
            var wearableUncapped = moItems.Where(i => i.Id != 0 && i.Name.Trim().Length > 0
                                                   && Character.WearClassOf(i) == uncapped).ToList();
            Check("items in that class", wearableUncapped.Count, 7);
            Check("and the game will wear them", wearableUncapped.All(Character.IsWearable), true);
            Check("Medusa's Head among them",
                  wearableUncapped.Any(i => i.Name.Trim() == "Medusa's Head"), true);

            // end to end: wear every one of the seven at once, which nothing else in the game allows
            var hoarder = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                           new[] { 25, 25, 25, 25, 25, 25 });
            hoarder.Guilds.Clear();
            hoarder.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 30 });
            int worn = 0;
            foreach (var it in wearableUncapped)
            {
                var sl = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Charges = (short)Math.Max(1, it.Charges) };
                hoarder.PutInPack(sl);
                if (Character.IsWearable(it) && hoarder.WearRefusal(sl, it, MoLook) == null)
                { sl.Equipped = true; worn++; }
            }
            CheckAtLeast("worn at once, of a class that caps at nothing", worn, 2);
            Check("the class maximum that let them", Character.MaxWornOfClass(uncapped), -1);

            // and the contrast: a class that caps at one refuses the second
            var helmets = moItems.Where(i => i.Id != 0 && Character.MaxWornOfClass(Character.WearClassOf(i)) == 1)
                                 .GroupBy(Character.WearClassOf).First().Take(2).ToList();
            if (helmets.Count == 2)
            {
                var one = Character.Create("probe2", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                           new[] { 25, 25, 25, 25, 25, 25 });
                one.Guilds.Clear();
                one.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 30 });
                var a = new InventorySlot { ItemId = helmets[0].Id, Known = IdLevel.All, Equipped = true };
                one.PutInPack(a);
                var b = new InventorySlot { ItemId = helmets[1].Id, Known = IdLevel.All };
                one.PutInPack(b);
                Check("a second of a capped class is refused",
                      one.WearRefusal(b, helmets[1], MoLook) != null, true);
            }
        }

        // ---- the guild log, against the eighteen places that write it ----
        // sub0578 is called from eighteen places and between them they write sixteen distinct
        // lines: two pairs are variants rather than separate messages - "his"/"her" on the demotion
        // (FRMCTYG.BAS:955 and 957) and alone/in-a-party on Asmodeus (MODULE4.BAS:4271 and 4274).
        //
        // Both the call-site list and the wordings are read out of the .BAS files rather than kept
        // here, so a line reworded in the port, or a nineteenth call site appearing, is caught.
        Console.WriteLine("\nthe guild log (sub0578's eighteen call sites)");
        {
            string dec = PortTree.At("decompiled");
            if (!System.IO.Directory.Exists(dec)) SourceMissing("decompiled/");
            else
            {
                var sites = new List<string>();
                var basText = new System.Text.StringBuilder();
                foreach (string f in System.IO.Directory.GetFiles(dec, "*.BAS").OrderBy(x => x))
                {
                    var lines = System.IO.File.ReadAllLines(f);
                    basText.Append(string.Join("\n", lines)).Append('\n');
                    for (int i = 0; i < lines.Length; i++)
                        if (lines[i].Contains("sub0578") && !lines[i].TrimStart().StartsWith("Sub "))
                            sites.Add($"{System.IO.Path.GetFileName(f)}:{i + 1}");
                }
                string src = basText.ToString();

                Check("call sites", sites.Count, 18);
                // the list GuildLog.Wrote names in its own doc comment, compared as a set so a
                // moved or added call site is reported by name rather than as two long rows
                var documented = new[]
                {
                    "CHARCREA.BAS:258", "FRMCTYG.BAS:148", "FRMCTYG.BAS:323", "FRMCTYG.BAS:439",
                    "FRMCTYG.BAS:563", "FRMCTYG.BAS:565", "FRMCTYG.BAS:726", "FRMCTYG.BAS:955",
                    "FRMCTYG.BAS:957", "MODULE11.BAS:511", "MODULE2.BAS:638", "MODULE3.BAS:1631",
                    "MODULE4.BAS:4271", "MODULE4.BAS:4274", "MODULE6.BAS:368", "MODULE6.BAS:3105",
                    "MODULE6.BAS:4575", "MODULE6.BAS:4577",
                };
                Check("call sites the doc comment misses",
                      string.Join(" ", sites.Except(documented)), "");
                Check("call sites it names that are not there",
                      string.Join(" ", documented.Except(sites)), "");

                // every wording the port writes, and the piece of it the source must contain
                var wordings = new (string What, string Fragment)[]
                {
                    ("joined a guild",        " joined our guild."),
                    ("given a title",         " was appointed the title of "),
                    ("...for reaching",       " for reaching Level "),
                    ("became guildmaster",    " became the new GuildMaster."),
                    ("appointed guildmaster", " was appointed GuildMaster."),
                    ("quest, fetch",          " was assigned a quest to return with "),
                    ("quest, kill",           " was assigned a quest to kill one "),
                    ("awarded a crest",       " was awarded the '"),
                    ("...for the rank",       "' for achieving the rank of new Guildmaster."),
                    ("demoted, his",          " levels for not completing his quest."),
                    ("demoted, her",          " levels for not completing her quest."),
                    ("set a record",          " set the record for "),
                    ("...with a value",       " with a value of "),
                    ("first to a level",      " was the first to go to level "),
                    ("Asmodeus, in a party",  " was part of the party that defeated Asmodeus."),
                    ("Asmodeus, alone",       " single handedly defeated Asmodeus."),
                    ("raised by somebody",    " was resurrected by "),
                    ("raised at the morgue",  " was resurrected in the morgue."),
                    ("died on a level",       " died on level "),
                    ("died, place unknown",   " died somewhere in the dungeon."),
                };
                var missingFromSource = wordings.Where(w => !src.Contains("\"" + w.Fragment + "\"")).ToList();
                Check("fragments the source does not carry",
                      string.Join(" ", missingFromSource.Select(w => w.What)), "");

                // and the port says each of them, in those words
                string rendered = string.Join("\n", new[]
                {
                    GuildLog.Wrote.JoinedGuild("N"), GuildLog.Wrote.Titled("N", "T", 3),
                    GuildLog.Wrote.BecameGuildmaster("N"), GuildLog.Wrote.AppointedGuildmaster("N"),
                    GuildLog.Wrote.QuestForItem("N", "I"), GuildLog.Wrote.QuestForMonster("N", "M"),
                    GuildLog.Wrote.GuildmasterAward("N", "C"),
                    GuildLog.Wrote.Demoted("N", 2, Sex.Male), GuildLog.Wrote.Demoted("N", 2, Sex.Female),
                    GuildLog.Wrote.SetRecord("N", "R", "V"), GuildLog.Wrote.FirstToLevel("N", 4),
                    GuildLog.Wrote.PartyBeatAsmodeus("N"), GuildLog.Wrote.AloneBeatAsmodeus("N"),
                    GuildLog.Wrote.Resurrected("N", "B"), GuildLog.Wrote.ResurrectedInMorgue("N"),
                    GuildLog.Wrote.DiedOnLevel("N", 5), GuildLog.Wrote.DiedSomewhere("N"),
                });
                var missingFromPort = wordings.Where(w => !rendered.Contains(w.Fragment)).ToList();
                Check("fragments the port does not write",
                      string.Join(" ", missingFromPort.Select(w => w.What)), "");
            }
        }

        // ---- the counts this file states, counted ----
        // A FIDELITY entry saying "the sixteen classes you wear" turned out to be fifteen: the
        // number had been read off a table rather than added up. These are the other counts in the
        // document that the data or the source can settle.
        Console.WriteLine("\ncounts asserted in docs/FIDELITY.md");
        {
            // "twelve guilds", "only five of them have Multiple Swings"
            Check("guilds", GuildData.All.Count, 12);
            // every record fn03D0 reads is kept (MODULE1.BAS:693-696), so the guilds' RaceMask bits
            // index the same races the original's do
            Check("races", RaceData.All.Count, 9);
            Check("of those, with Multiple Swings",
                  GuildData.All.Count(g => g.ExtraSwings > 0), 5);

            // "the twelve named masters a new game installs"
            Check("guildmasters a new game installs", Guildmasters.Seeded.Length, 12);

            // "the guild crest is the item whose id is 299 plus the guild index"
            var crestItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            int crests = 0;
            for (int g = 0; g < GuildData.All.Count; g++)
                if (crestItems.Any(i => i.Id == 299 + g)) crests++;
            Check("guilds with a crest at id 299 + index", crests, 12);

            // "ten slots per character" for the buffers, "40 or more is the spell at that index"
            Check("spell buffers per character", SpellBuffers.Count, 10);

            // "twelve town-wide bests in two columns"
            Check("records in the Hall", Enum.GetValues<HallRecords.Kind>().Length, 12);

            // "the four identification levels"
            Check("identification levels", Enum.GetValues<IdLevel>().Length, 4);

            // "six stats at creation, the three skills after a level"
            Check("stats", RaceData.StatNamesLong.Length, 6);
            Check("resistance kinds", Effects.ResistanceKinds, 11);

            // "all ten chest traps"
            Check("chest traps, counting Not Trapped", ChestTraps.Names.Length, 11);
        }

        // ---- an item handed over is re-read against the wrong item ----
        // FRMEXCHA.BAS:199-201 tests with the item's reference and then assigns with that item's
        // LevelScale used as a reference. fn12F0's second argument indexes gv1426, so what gets
        // stored is how well the receiver reads whichever item carries that number.
        //
        // This was recorded as not reproduced because "there is no single behaviour to be faithful
        // to". There is: every LevelScale in MDATA3 is 0..80 and every one matches a real item id,
        // so the stray read is always in range and always lands somewhere definite.
        Console.WriteLine("\nhanding an item over (FRMEXCHA.BAS:199-201)");
        {
            var hoItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? HoLook(short id) => hoItems.FirstOrDefault(i => i.Id == id);
            var hoReal = hoItems.Where(i => i.Id != 0 && i.Name.Trim().Length > 0).ToList();

            Check("LevelScale values that name no item",
                  hoReal.Count(i => HoLook((short)i.LevelScale) == null), 0);
            Check("the range they occupy",
                  $"{hoReal.Min(i => i.LevelScale)}..{hoReal.Max(i => i.LevelScale)}", "0..80");
            // ids are a shuffle of the record positions, which is why it matters that fn12F0 takes
            // a reference and not an index
            Check("records whose id is not their position",
                  Enumerable.Range(0, hoItems.Count).Count(p => hoItems[p].Id != p), 356);

            var taker = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 18, 18, 18, 18, 18, 18 });
            taker.Guilds.Clear();
            taker.Guilds.Add(new GuildMembership { Guild = "Nomad", Level = 12 });

            // how many items the slip gives a different answer for than the honest reading would
            int differs = 0;
            foreach (var it in hoReal)
            {
                var stray = HoLook((short)it.LevelScale);
                if (stray == null) continue;
                if (Loot.ReadableLevel(taker, it, HoLook) != Loot.ReadableLevel(taker, stray, HoLook)) differs++;
            }
            Check("items the slip reads differently", differs, 132);

            // and end to end on one of them: the item is judged by the other one's difficulty
            var subject = hoReal.First(i =>
            {
                var s = HoLook((short)i.LevelScale);
                return s != null && Loot.ReadableLevel(taker, i, HoLook) != Loot.ReadableLevel(taker, s, HoLook)
                                 && Loot.ReadableLevel(taker, i, HoLook) > IdLevel.Barely;
            });
            var other = HoLook((short)subject.LevelScale)!;
            var sl = new InventorySlot { ItemId = subject.Id, Known = IdLevel.Barely };
            Loot.OnReceived(taker, sl, subject, HoLook);
            Check($"\"{subject.Name.Trim()}\" handed over reads as", sl.Known,
                  Loot.ReadableLevel(taker, other, HoLook));
            Check("which is not what it would read as on its own",
                  sl.Known != Loot.ReadableLevel(taker, subject, HoLook), true);
        }

        // ---- two chest traps read resistance backwards, and one beside them does not ----
        // monOpenChest afflicts you with poison gas or disease spores when a roll falls *under*
        // half your resistance (MONSMOD.BAS:3516 and 3523), so a higher resistance makes the trap
        // more likely to catch you and a character with none can never be caught at all.
        //
        // The fire trap in the same Select Case reads the same array the right way round -
        // "damage = damage - damage * (Resistances(0) / 2 * .01)" at MONSMOD.BAS:3536 - and all
        // three name the correct kind: 0 fire, 4 disease, 5 poison. So the array is resistance and
        // the constants are right; it is the direction of the two tests that is wrong. This is
        // what settles the question the FIDELITY entry left open.
        Console.WriteLine("\nchest traps against resistance (MONSMOD.BAS:3516, 3523, 3536)");
        {
            var ctItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? CtLook(short id) => ctItems.FirstOrDefault(i => i.Id == id);
            int Walls(int x, int y) => 0;

            // a race with no poison or disease resistance at all
            var bare = RaceData.All.FirstOrDefault(r =>
                r.Resistances.Length > 5 && r.Resistances[5] == 0 && r.Resistances[4] == 0);
            Check("a race with neither resistance exists", bare != null, true);
            if (bare != null)
            {
                int poisoned = 0, diseased = 0;
                var rng = new Random(20260922);
                for (int i = 0; i < 4000; i++)
                {
                    var c = Character.Create("probe", bare, Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    var party = new List<Character> { c };
                    ChestTraps.Spring(1, party, c, 5, rng, CtLook, Walls);
                    if (c.Poisoned) poisoned++;
                    ChestTraps.Spring(2, party, c, 5, rng, CtLook, Walls);
                    if (c.Diseased) diseased++;
                }
                Check($"{bare.Name.Trim()}, no resistance: poisoned by poison gas", poisoned, 0);
                Check("and diseased by disease spores", diseased, 0);
            }

            // and a character who *is* resistant catches it, which is the fault the other way up
            var tough = RaceData.All.OrderByDescending(r => r.Resistances.Length > 5 ? r.Resistances[5] : 0).First();
            Check("some race resists poison", tough.Resistances[5] > 0, true);
            {
                int caught = 0;
                var rng = new Random(20260922);
                for (int i = 0; i < 4000; i++)
                {
                    var c = Character.Create("probe", tough, Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    ChestTraps.Spring(1, new List<Character> { c }, c, 5, rng, CtLook, Walls);
                    if (c.Poisoned) caught++;
                }
                CheckAtLeast($"{tough.Name.Trim()}, {tough.Resistances[5]}% resistant: poisoned", caught, 1);
                // half the resistance is the chance, so it lands near that
                CheckNear("and about half its resistance of the time",
                          caught * 100.0 / 4000, tough.Resistances[5] / 2.0 - 3, tough.Resistances[5] / 2.0 + 3);
            }

            // the fire trap next to them uses the same array to reduce damage, which is the
            // contrast that says the array was never the problem
            {
                var rng = new Random(7);
                long bareTotal = 0, toughTotal = 0;
                for (int i = 0; i < 3000; i++)
                {
                    var a = Character.Create("a", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                    a.MaxHits = 100000; a.CurHits = 100000;
                    ChestTraps.Spring(4, new List<Character> { a }, a, 10, rng, CtLook, Walls);
                    bareTotal += 100000 - a.CurHits;

                    var b = Character.Create("b", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                    b.MaxHits = 100000; b.CurHits = 100000;
                    b.SpellResist[0] = 90;                      // warded against fire
                    ChestTraps.Spring(4, new List<Character> { b }, b, 10, rng, CtLook, Walls);
                    toughTotal += 100000 - b.CurHits;
                }
                Check("fire hurts a warded character less", toughTotal < bareTotal, true);
                // 90% resistance halved is a 45% cut
                CheckNear("by about the half of its resistance", 100.0 - toughTotal * 100.0 / bareTotal, 40, 50);
            }
        }

        // ---- the three current-character indices, and which switch moves which ----
        // The original holds three, not two: gv1A82, gv1A84 and gv1A86. Two routines change
        // character and they do not change the same ones.
        //
        //   sub1536 (MODULE7.BAS:651) sets all three - the full switch, and a double click
        //   sub178B (MODULE7.BAS:600) sets gv1A84 and gv1A86 only - a single click
        //
        // So gv1A82 lags behind after a single click, which is what the window-size entry and the
        // gold announcement both turn on. Two places test for exactly that and resynchronise.
        Console.WriteLine("\nthe current-character indices (MODULE7.BAS:600 against :651)");
        {
            string dec = PortTree.At("decompiled");
            if (!System.IO.Directory.Exists(dec)) SourceMissing("decompiled/");
            else
            {
                string src = string.Concat(System.IO.Directory.GetFiles(dec, "*.BAS")
                                                    .OrderBy(x => x).Select(System.IO.File.ReadAllText));
                int Reads(string g) => System.Text.RegularExpressions.Regex.Matches(src, g).Count;

                // three of them, and the one everything else reads is gv1A86
                Check("gv1A86 is the one in general use", Reads("gv1A86") > Reads("gv1A84") * 5, true);
                Check("and gv1A82 is the rarest", Reads("gv1A82") < Reads("gv1A84"), true);

                // the two switches, by what they assign
                string full = Between(src, "Sub sub1536", "End Sub");
                string light = Between(src, "Sub sub178B", "End Sub");
                Check("the full switch sets gv1A82", full.Contains("gv1A82 ="), true);
                Check("and gv1A84", full.Contains("gv1A84 ="), true);
                Check("and gv1A86", full.Contains("gv1A86 ="), true);
                Check("the light switch leaves gv1A82 alone", light.Contains("gv1A82 ="), false);
                Check("but sets gv1A84", light.Contains("gv1A84 ="), true);
                Check("and gv1A86", light.Contains("gv1A86 ="), true);

                // and the resynchronisation that exists because of the lag
                Check("places that catch gv1A82 lagging",
                      System.Text.RegularExpressions.Regex.Matches(
                          src, @"If\s+gv1A82 <> gv1A86 Then sub1536 gv1A86").Count, 2);
            }
        }

        // ---- the Ninja titles, reproduced as written ----
        // fn12C4's Ninja branch has "Case Is < 120" three times running, so VB takes the first and
        // "Ninja" and "Master Ninja" - the two named after the guild - are unreachable. The port
        // ships the original's ladder and nothing else (the house rule that once filled in the two
        // thresholds was removed on 2026-10-25), so a Ninja goes Assassin straight to Shogun.
        Console.WriteLine("\nthe Ninja titles (fn12C4's three Case Is < 120)");
        {
            Check("a Ninja at 125", GuildTitles.Of("Ninja", 125), "Shogun");
            Check("at 135", GuildTitles.Of("Ninja", 135), "Shogun");
            Check("at 119", GuildTitles.Of("Ninja", 119), "Assassin");
            // the two guild-named titles are unreachable at every level, which is the fault
            Check("levels that ever read Ninja or Master Ninja",
                  Enumerable.Range(1, 400).Count(l =>
                      GuildTitles.Of("Ninja", l) is "Ninja" or "Master Ninja"), 0);

            // every other guild's ladder ascends strictly, which is what says three bands were
            // meant here rather than a deliberate triple
            var notAscending = GuildData.All.Select(g => g.Name.Trim())
                .Where(g => g != "Ninja")
                .Where(g =>
                {
                    var seen = new List<string>();
                    foreach (int l in Enumerable.Range(1, 400))
                    {
                        string t = GuildTitles.Of(g, l);
                        if (seen.Count == 0 || seen[^1] != t) seen.Add(t);
                    }
                    return seen.Count != seen.Distinct().Count();
                }).ToList();
            Check("other guilds whose ladder repeats a title",
                  string.Join(" ", notAscending), "");
        }

        // ---- the twips conversion, against the help's own screenshot ----
        // The defaults table is in twips, fifteen to the pixel at the 96dpi the game was drawn for.
        // The Dungeon's 7320x3450 is therefore 488x230, and the help's screenshot of that window is
        // 487x229 - the client area inside a one-pixel border. That is the only place in the
        // project where the conversion is checked against something the original shipped, so the
        // file is measured rather than the number being taken on trust.
        Console.WriteLine("\nthe twips conversion (game_files/dmainw1.png)");
        {
            Check("twips to the pixel", WindowLayout.TwipsPerPixel, 15);
            Check("the Dungeon default, converted", WindowLayout.Default(GameWindow.Dungeon), (488d, 230d));
            string shot = System.IO.Path.Combine(dir, "dmainw1.png");
            if (!System.IO.File.Exists(shot)) { if (!FullGameOnly("the help's screenshot dmainw1.png, an extra of the 1.1 install")) { Console.WriteLine("  FAIL  dmainw1.png missing"); _failed++; } }
            else
            {
                var (shotW, shotH) = PngSize(shot);
                Check("the help's screenshot of it", $"{shotW}x{shotH}", "487x229");
                // one pixel of border on each axis, which is what makes the two agree
                Check("and the difference is a border", $"{488 - shotW}x{230 - shotH}", "1x1");
            }
        }

        // ---- the Messages window keeps twenty lines ----
        // sub085B (MAINMOD2.BAS:3189-3205) adds a line and drops the oldest once the count passes
        // mc04F2 = 20. Both of this port's screens keep their own list and both wrote the 20 out by
        // hand until 2026-09-22; it is one constant now, so they cannot drift apart.
        Console.WriteLine("\nthe Messages window (sub085B, mc04F2)");
        {
            Check("lines kept", MessageList.Kept, 20);

            var log = new List<string>();
            for (int i = 1; i <= 50; i++) MessageList.Add(log, $"line {i}");
            Check("after fifty lines, held", log.Count, 20);
            Check("the oldest is", log[0], "line 31");
            Check("the newest is", log[^1], "line 50");

            // a blank is the port's "nothing to say"; the same line twice running is added twice,
            // as sub085B adds it (MAINMOD2.BAS:3195-3208)
            Check("a blank line is refused", MessageList.Add(log, "   "), false);
            Check("the same line twice running is kept", MessageList.Add(log, "line 50"), true);
            Check("and shows twice", log[^1] + "|" + log[^2], "line 50|line 50");
            Check("still twenty", log.Count, 20);
        }
        // ---- every routine citation, in the document and in the code ----
        // The logic is in Citations.cs, which also runs as a probe. It resolves both naming
        // schemes and both line-number conventions - see the notes there, which record the three
        // patterns that had to be thrown away before this one worked.
        //
        // The resolved count is asserted as well as the problem list, and that is the important
        // half: the first version of this check ended up matching nothing at all, so every real
        // error read as clean. A count near zero means the patterns have rotted.
        Console.WriteLine("\nroutine citations, in the document and in the code");
        {
            string portRoot = PortTree.Root;
            var files = Citations.FilesToCheck(portRoot);
            CheckAtLeast("files scanned", files.Count, 80);
            if (!PortTree.HasDecompiled) SourceMissing("the routines the citations name (decompiled/, decompiled-named/)");
            else
            {
                var (problems, resolved) = Citations.Check(files, portRoot);
                CheckAtLeast("citations resolved", resolved, 200);
                Check("citations that do not land where they say", string.Join("  ", problems), "");
            }
        }

        // ---- the Companions page, and the gate that makes it go stale ----
        // sub0B82 (MAINMOD2.BAS:4704-4745) writes each row as the slot number, the creature's name
        // in brackets when the companion has none of its own or its own name in quotes, and the
        // hits separated by a **backslash**. Its first line is the interesting one:
        //
        //     If pv06CE <> gv1A82 Then Exit Sub
        //
        // so the page redraws for the lagging index alone. That is the concrete mechanism behind
        // the staleness the help describes - a single click moves gv1A84 and gv1A86 and this page
        // is not repainted at all.
        Console.WriteLine("\nthe Companions page (MAINMOD2.BAS:4704)");
        {
            string mm2 = PortTree.At("decompiled", "MODULE6.BAS");
            if (!System.IO.File.Exists(mm2)) SourceMissing("MODULE6.BAS");
            else
            {
                string body = Between(System.IO.File.ReadAllText(mm2), "Sub sub0B82", "End Sub");
                Check("the page is gated on gv1A82", body.Contains("If  pv06CE <> gv1A82 Then Exit Sub"), true);
                // the hits really are joined with a backslash, which reads like a typo and is not
                Check("hits joined with a backslash", body.Contains("+ \"\\\" +"), true);
                Check("and not with a slash", body.Contains("+ \"/\" +"), false);
                // four slots walked, of the five the array declares - the same mc00E0 the acid
                // draw reads past
                Check("slots the page walks", body.Contains("For l06D0 = 0 To mc00E0 - 1"), true);
            }
        }

        // ---- a chest trap says almost nothing, and the port had it saying a great deal ----
        // Across all ten arms monOpenChest calls sub085B exactly twice: "SLIME: <item> Destroyed!"
        // and "Your companions were scared away!". The Withering arm adds a third through
        // mdrcharAddAge True, 28 (MONSMOD.BAS:3633): "You are now 4 week(s) older." What tells you which trap fired is its *name*,
        // written into lblTrap.Caption - not a sentence describing it.
        //
        // The port had six lines of its own invention here: gas catching you, fire catching you,
        // fate turning, the floor dropping away, everything going dark, and withering. All removed.
        // This asserts the whole message set rather than their absence, so a seventh cannot appear
        // without a check noticing.
        Console.WriteLine("\nwhat a chest trap says (monOpenChest's two sub085B calls)");
        {
            var tItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? TLook(short id) => tItems.FirstOrDefault(i => i.Id == id);
            int NoWalls(int x, int y) => 0;

            var said = new List<string>();
            var rng = new Random(20260922);
            for (int trap = 1; trap <= 10; trap++)
                for (int go = 0; go < 400; go++)
                {
                    var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    c.MaxHits = 500; c.CurHits = 500;
                    // something to destroy, and something to frighten away
                    var destroyable = tItems.First(i => i.Id != 0 && ChestTraps.Destructible(i));
                    c.PutInPack(new InventorySlot { ItemId = destroyable.Id, Known = IdLevel.All });
                    c.Companions.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 5, BindingLevel = 3 });
                    said.AddRange(ChestTraps.Spring(trap, new List<Character> { c }, c, 5, rng, TLook, NoWalls).Lines);
                }

            // the two wordings, with the item name normalised out of the SLIME one
            var shapes = said.Select(s => s.StartsWith("SLIME: ") ? "SLIME: <item> Destroyed!" : s)
                             .Distinct().OrderBy(s => s).ToList();
            Check("distinct things a trap says", string.Join(" | ", shapes),
                  "SLIME: <item> Destroyed! | You are now 4 week(s) older. | Your companions were scared away!");
            CheckAtLeast("and all three were actually reached", shapes.Count, 3);

            // both are the original's, read out of the file rather than kept here
            string monsPath = PortTree.At("decompiled", "MODULE4.BAS");
            if (System.IO.File.Exists(monsPath))
            {
                string src = System.IO.File.ReadAllText(monsPath);
                Check("the slime line is the original's", src.Contains("\"SLIME: \""), true);
                Check("and the fear line", src.Contains("\"Your companions were scared away!\""), true);
            }
        }

        // ---- a companion's ordinary blow says nothing ----
        // monCompanionAttack writes two captions across its whole length, both for special attacks
        // - fn128B(companion) + " Stoned one!" and the same with " Electrocuted one!". An ordinary
        // hit, miss or kill produces no text at all. The port had it announcing every blow until
        // 2026-09-22.
        //
        // The character's own melee is a different routine and was always right: monAttack's nine
        // captions, shown only for the watched character. Both are asserted here, because the
        // mistake last time was assuming which routine a string belonged to.
        Console.WriteLine("\nwhat a swing says (monAttack's nine, monCompanionAttack's none)");
        {
            string mons4 = PortTree.At("decompiled", "MODULE4.BAS");
            string monsNamed = PortTree.At("decompiled-named", "MONSMOD.BAS");
            if (!System.IO.File.Exists(mons4) || !System.IO.File.Exists(monsNamed))
            SourceMissing("MONSMOD");
            else
            {
                string raw = System.IO.File.ReadAllText(mons4);
                // the nine the character's own swing can produce, all of them the original's
                foreach (string cap in new[]
                         {
                             "You missed!", "You hit for", "You Severed one for", "You Backstabbed for",
                             "You Critically hit for", "You Killed One!", "You Severed one!",
                             "You Backstabbed one!", "You Critically hit one!",
                             "Using no weapon!", "Weapon has no effect!",
                         })
                    Check($"monAttack says \"{cap}\"", raw.Contains("\"" + cap), true);

                // and the companion routine says only these two, both for special attacks
                string comp = Between(System.IO.File.ReadAllText(monsNamed),
                                      "Sub monCompanionAttack", "\nEnd Sub");
                var caps = System.Text.RegularExpressions.Regex.Matches(
                        comp, @"lblCharacterHit\.Caption = [^\r\n]*")
                    .Select(m => m.Value.Substring("lblCharacterHit.Caption = ".Length).Trim())
                    .Distinct().OrderBy(s => s).ToList();
                Check("captions monCompanionAttack writes", caps.Count, 2);
                Check("and they are the two specials",
                      string.Join(" | ", caps),
                      "fn128B(Me, l0204) + \" Electrocuted one!\" | fn128B(Me, l0204) + \" Stoned one!\"");

                // Nothing in the port narrates an ordinary companion blow any more. Only lines
                // that actually emit a message are looked at: the comment recording what was
                // removed quotes both strings, so a whole-file search finds its own tombstone.
                var emitting = System.IO.File.ReadAllLines(
                        PortTree.At("src/Mordor.Core", "Combat.cs"))
                    .Where(l => l.Contains("Say(") || l.Contains("Lines.Add(")).ToList();
                CheckAtLeast("lines in Combat.cs that emit a message", emitting.Count, 5);   // 9 on 2026-09-25, after the invented cast lines went
                foreach (string gone in new[] { "lunges at the", "killing it!" })
                    Check($"no message line says \"{gone}\"", emitting.Any(l => l.Contains(gone)), false);
            }
        }

        // ---- a companion's special attack can land on your own party ----
        // fn0D64 (MONSMOD.BAS:2020-2052) rolls before a breath, spit or spell fires. The chance is
        // (999 / the companion's guild level) * (bodies on your side / 20) * a factor per kind,
        // clamped per kind, and the victim is then drawn from the party - except that a draw
        // landing on the companion's own owner means no mishap at all.
        //
        // The port had none of this. The rule is here and asserted; the attacks that would call it
        // are not built yet - see docs/FIDELITY.md.
        Console.WriteLine("\na companion's special going astray (fn0D64)");
        {
            // the three kinds, their factors and their clamps
            Check("breath, weak companion, small party",
                  CompanionMishap.Chance(CompanionMishap.Kind.Breath, 1, 4), 40.0);   // clamped high
            Check("breath, strong companion, small party",
                  CompanionMishap.Chance(CompanionMishap.Kind.Breath, 999, 4), 2.0);  // clamped low
            Check("spit is clamped lower than breath",
                  CompanionMishap.Chance(CompanionMishap.Kind.Spit, 1, 4), 20.0);
            Check("and its floor is half a point",
                  CompanionMishap.Chance(CompanionMishap.Kind.Spit, 9990, 4), 0.5);
            Check("a spell sits between them",
                  CompanionMishap.Chance(CompanionMishap.Kind.Spell, 999, 4), 1.0);

            // the shape between the clamps: level in the denominator, bodies on top
            // worked out by hand rather than restated from the code: 999/100 is 9.99, times
            // 5/20 is 2.4975, times 2 is 4.995. A row comparing the function to its own formula
            // would have passed while the number in this comment was wrong, which it was.
            CheckNear("999/100 * 5/20 * 2 comes to 4.995",
                      CompanionMishap.Chance(CompanionMishap.Kind.Breath, 100, 5), 4.99, 5.00);
            Check("a weaker companion is more dangerous",
                  CompanionMishap.Chance(CompanionMishap.Kind.Breath, 50, 5)
                  > CompanionMishap.Chance(CompanionMishap.Kind.Breath, 200, 5), true);
            Check("and a bigger party is too",
                  CompanionMishap.Chance(CompanionMishap.Kind.Breath, 100, 10)
                  > CompanionMishap.Chance(CompanionMishap.Kind.Breath, 100, 5), true);

            // fn125B counts characters plus living companions, and stops at four companions each
            var party = new Party();
            for (int i = 0; i < 3; i++)
            {
                var c = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 12, 12, 12, 12, 12, 12 });
                party.Members.Add(c);
            }
            Check("three characters alone", CompanionMishap.BodiesOnYourSide(party), 3);
            // four slots and no more: a fifth Add finds no hole (fn1163's loop, MAINMOD2.BAS:386-387)
            for (int s = 0; s < 4; s++)
                party.Members[0].Companions.Add(new Companion { MonsterId = 1, Hits = 4, MaxHits = 4 });
            Check("a fifth companion finds no slot",
                  party.Members[0].Companions.Add(new Companion { MonsterId = 1, Hits = 4, MaxHits = 4 }), -1);
            Check("four companions on one of them count as four",
                  CompanionMishap.BodiesOnYourSide(party), 7);
            party.Members[0].Companions[1]!.Hits = 0;
            Check("and a dead one does not count", CompanionMishap.BodiesOnYourSide(party), 6);

            // the owner is spared and everybody else is not, over a long run
            var rng = new Random(20260922);
            var hit = new int[3];
            int spared = 0;
            for (int i = 0; i < 40000; i++)
            {
                var v = CompanionMishap.Victim(rng, CompanionMishap.Kind.Breath, 1, party, owner: 1);
                if (v is int who) hit[who]++; else spared++;
            }
            Check("the owner is never the victim", hit[1], 0);
            CheckAtLeast("but the first member is", hit[0], 1);
            CheckAtLeast("and the third", hit[2], 1);
            // a draw that picks the owner is turned into no-mishap, so the two reachable victims
            // share the mishaps and the owner's third of the draws is given back to the monsters
            Check("the two reachable victims share it evenly",
                  Math.Abs(hit[0] - hit[2]) < hit[0] / 5, true);
            CheckAtLeast("and most attacks still go at the monsters", spared, hit[0] + hit[2]);
        }

        // ---- monGetSpecialHits, and the other breath formula that is not it ----
        // The original has two different breath calculations and they are not the same arithmetic.
        // sub0E85 inlines its own (MONSMOD.BAS:2702-2704) for a monster breathing on you;
        // monGetSpecialHits (2391-2460) is what a *companion's* breath, acid spit and electrocution
        // use. Asserting the difference is the point - unifying them would look like tidying.
        Console.WriteLine("\nmonGetSpecialHits (MONSMOD.BAS:2391) against sub0E85's own");
        {
            // the shared range: Int((Log(L+1) + (L+1)/15) / Log(1.08) - target/10 + 1)
            static int SourceRange(int atk, int tgt)
            {
                double a = atk + 1;
                return (int)Math.Floor((Math.Log(a) + a / 15.0) / Math.Log(1.08) - tgt / 10.0 + 1);
            }
            foreach (var (atk, tgt) in new[] { (10, 5), (100, 30), (1, 1), (500, 200) })
                Check($"range at attacker {atk}, target {tgt}",
                      SpecialHits.Range(atk, tgt), SourceRange(atk, tgt));

            // a stronger target takes less, and a stronger attacker gives more
            Check("a higher-level target shortens the range",
                  SpecialHits.Range(100, 200) < SpecialHits.Range(100, 10), true);
            Check("a higher-level attacker lengthens it",
                  SpecialHits.Range(300, 50) > SpecialHits.Range(30, 50), true);

            // breath adds a quarter of the range, the other two a half, so spit and electrocution
            // hit harder than a breath from the same creature
            var rng = new Random(20260922);
            long breath = 0, spit = 0, shock = 0;
            for (int i = 0; i < 20000; i++)
            {
                breath += SpecialHits.Raw(rng, SpecialHits.Kind.Breath, 120, 40);
                spit += SpecialHits.Raw(rng, SpecialHits.Kind.AcidSpit, 120, 40);
                shock += SpecialHits.Raw(rng, SpecialHits.Kind.Electrocute, 120, 40);
            }
            Check("acid spit beats breath from the same creature", spit > breath, true);
            Check("and electrocution matches the spit", Math.Abs(spit - shock) < spit / 20, true);

            // resistance takes half its percentage off, as every special attack does
            Check("90% resistance leaves 55 of 100", SpecialHits.AfterResistance(100, 90), 55);
            Check("and none leaves it alone", SpecialHits.AfterResistance(100, 0), 100);
            Check("40% leaves 80", SpecialHits.AfterResistance(100, 40), 80);

            // which resistance applies is read off the *attacker*: a cold breather is resisted by
            // Cold however good the victim's Fire resistance is
            Check("a fire breather is resisted by Fire",
                  SpecialHits.ResistedBy(SpecialHits.Kind.Breath, true), SpecialHits.Fire);
            Check("a cold breather by Cold",
                  SpecialHits.ResistedBy(SpecialHits.Kind.Breath, false), SpecialHits.Cold);
            Check("a spit by Acid", SpecialHits.ResistedBy(SpecialHits.Kind.AcidSpit, true), SpecialHits.Acid);
            Check("a shock by Electrical", SpecialHits.ResistedBy(SpecialHits.Kind.Electrocute, true), SpecialHits.Electrical);

            // and the two formulas really do differ - sub0E85 starts from
            // level * Log(level + 1), divides the target's level by 20, and halves at the end
            static int MonsterBreathRange(int monLvl, int charGuildLevel)
            {
                int seed = (int)Math.Round(monLvl * Math.Log(monLvl + 1), MidpointRounding.ToEven);
                return (int)Math.Floor((Math.Log(Math.Max(1, seed)) + seed / 15.0) / Math.Log(1.08)
                                       - charGuildLevel / 20.0 + 1);
            }
            Check("the two breath ranges are not the same number",
                  SpecialHits.Range(120, 40) == MonsterBreathRange(120, 40), false);
            CheckAtLeast("and the monster's own is the larger",
                         MonsterBreathRange(120, 40) - SpecialHits.Range(120, 40), 1);
        }

        // ---- a companion's breath, end to end ----
        // MONSMOD.BAS:875-959. It fires on 50, or 80 when the monsters are much stronger than the
        // companion and 20 when they are much weaker. Then the mishap roll decides whether it
        // lands on the party. The once-a-round guard, l0208, only stops a cold breath: the test is
        // "bit3 Or bit4 And l0208 = False" and And binds tighter than Or.
        Console.WriteLine("\na companion's breath (MONSMOD.BAS:875-959)");
        {
            var bMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var bItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Monster? MonById(short id) => bMons.FirstOrDefault(m => m.Id == id);
            Item? ItById(short id) => bItems.FirstOrDefault(i => i.Id == id);

            // a real breather and a real victim out of the data
            var breather = bMons.First(m => m.Id != 0 && (m.SpecialAttackFlags & (1 << 3)) != 0);
            var prey = bMons.First(m => m.Id != 0 && m.Hits > 20 && m.GuildLevel < breather.GuildLevel);
            Check("a fire-breathing creature exists", breather.Name.Trim().Length > 0, true);

            int breathed = 0, mishaps = 0, monstersHurt = 0;
            for (int run = 0; run < 3000; run++)
            {
                var party = new Party();
                for (int i = 0; i < 3; i++)
                {
                    var m = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    m.MaxHits = 4000; m.CurHits = 4000;
                    party.Members.Add(m);
                }
                // the middle member holds the breather, so the owner-spared rule is visible
                party.Members[1].Companions.Add(new Companion
                { MonsterId = breather.Id, Hits = 40, MaxHits = 40, BindingLevel = 5 });

                var foes = new List<CombatMonster>
                {
                    new() { Def = prey, Hits = 4000 }, new() { Def = prey, Hits = 4000 },
                };
                var fight = new CombatEngine(party, foes, ItById, new Random(1000 + run));
                fight.UseCompanionDefs(c => MonById(c.MonsterId));
                int before = foes.Sum(f => f.Hits);
                var lines = fight.RoundPaced(new List<Spell>()).Select(l => l.Text).ToList();

                bool said = lines.Any(l => l.Contains("accidently breathed on"));
                if (said) { mishaps++; breathed++; }
                else if (foes.Sum(f => f.Hits) < before) { monstersHurt++; breathed++; }
            }

            CheckAtLeast("rounds where the breath fired", breathed, 1200);
            CheckAtLeast("of those, some went at the monsters", monstersHurt, 100);
            CheckAtLeast("and some went astray", mishaps, 1);
            Check("every firing is one or the other", mishaps + monstersHurt, breathed);
        }

        // ---- the acid spit picks one victim where the breath splashes everything ----
        // fn0DAB (MONSMOD.BAS:2100-2119): with N living companions the spit lands on one of them
        // N/(N+1) of the time and on the character otherwise. One companion is a coin flip, two is
        // two in three, three is three in four.
        Console.WriteLine("\nwhere an acid spit lands (fn0DAB)");
        {
            var rng = new Random(20260922);
            foreach (int have in new[] { 0, 1, 2, 3 })
            {
                var who = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                           new[] { 12, 12, 12, 12, 12, 12 });
                for (int i = 0; i < have; i++)
                    who.Companions.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 5 });

                int onCompanion = 0;
                const int runs = 20000;
                for (int i = 0; i < runs; i++)
                    if (CompanionSpit.Slot(rng, who) >= 0) onCompanion++;

                double want = have == 0 ? 0 : have / (double)(have + 1) * 100;
                CheckNear($"with {have} companions it hits one {want:F0}% of the time",
                          onCompanion * 100.0 / runs, Math.Max(0, want - 1.5), want + 1.5);
            }

            // a dead companion is not a target and does not count towards the odds
            var one = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                       new[] { 12, 12, 12, 12, 12, 12 });
            one.Companions.Add(new Companion { MonsterId = 1, Hits = 0, MaxHits = 5 });
            Check("a dead companion is never the target", CompanionSpit.Slot(rng, one), -1);

            // four companions fill the four slots the draw walks, and every one of them is reachable;
            // the fifth is refused a slot, so there is none beyond them to be safe from it
            var five = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                        new[] { 12, 12, 12, 12, 12, 12 });
            for (int i = 0; i < 4; i++)
                five.Companions.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 5 });
            Check("and a fifth is refused", five.Companions.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 5 }), -1);
            var landed = new HashSet<int>();
            for (int i = 0; i < 20000; i++)
            {
                int s = CompanionSpit.Slot(rng, five);
                if (s >= 0) landed.Add(s);
            }
            Check("slots a spit can land on, with all four full", string.Join(",", landed.OrderBy(x => x)), "0,1,2,3");
        }

        // ---- picking up the fallen (mdrcharGetCarry, MAINMOD2.BAS:2027-2084) ----
        // The heaviest fallen companion is carried, by MaxHits strictly greater, so a tie keeps the
        // earlier slot. With nothing carried, a later heavier one makes the "dropped" line read
        // Companions(-1) and the error handler leaves before anything is done. A creature more
        // than one size above the owner's race is let go. And while a morgue body is carried
        // nothing is chosen at all: the sweep lets every fallen companion go.
        Console.WriteLine("\npicking up a fallen companion (mdrcharGetCarry)");
        {
            var smallDef = new Monster { Name = "small", Id = 30950, Size = 1, Stats = new short[7] };
            var hugeDef = new Monster { Name = "huge", Id = 30951, Size = 4, Stats = new short[7] };
            Monster? Sized(short id) => id == smallDef.Id ? smallDef : id == hugeDef.Id ? hugeDef : null;
            Character Owner() => Character.Create("Ann", RaceData.ByName("Human"), Alignment.Neutral, Sex.Female,
                                                  new[] { 12, 12, 12, 12, 12, 12 });
            string Held(Character c) => string.Join(",", Enumerable.Range(0, 4)
                .Select(i => c.Companions[i] is Companion x ? x.Hits.ToString() : "-"));

            var tie = Owner();
            tie.Companions[0] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 20 };
            tie.Companions[2] = new Companion { MonsterId = smallDef.Id, Hits = -4, MaxHits = 20 };
            var said = CompanionCarry.Run(tie, true, c => Sized(c.MonsterId));
            Check("a tie: the earlier slot is carried", tie.CarryingCompanion, 0);
            Check("and says so, with no \"dropped\" line", string.Join("|", said), "You are now carrying Companion #1.");
            Check("the other is swept out of its slot", Held(tie), "0,-,-,-");

            var abort = Owner();
            abort.Companions[0] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 5 };
            abort.Companions[2] = new Companion { MonsterId = smallDef.Id, Hits = -1, MaxHits = 50 };
            said = CompanionCarry.Run(abort, true, c => Sized(c.MonsterId));
            Check("fn128B(-1): nothing is said", said.Count, 0);
            Check("nothing is carried", abort.CarryingCompanion, -1);
            Check("and the sweep never runs: both stay, floored at 0", Held(abort), "0,-,0,-");

            var big = Owner();
            big.Companions[3] = new Companion { MonsterId = hugeDef.Id, Hits = 0, MaxHits = 50 };
            said = CompanionCarry.Run(big, false, c => Sized(c.MonsterId));
            Check("too big, for somebody not on screen", string.Join("|", said), "Ann, Companion #4 was too big to carry.");
            Check("and it is let go", Held(big), "-,-,-,-");
            Check("with nothing carried", big.CarryingCompanion, -1);

            var body = Owner();
            body.CarryingCharacter = "Bob";
            body.Companions[0] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 5 };
            body.Companions[1] = new Companion { MonsterId = smallDef.Id, Hits = 3, MaxHits = 5 };
            body.Companions[3] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 50 };
            said = CompanionCarry.Run(body, true, c => Sized(c.MonsterId));
            Check("carrying a morgue body: nothing is said", said.Count, 0);
            Check("every fallen one is swept, the living stay", Held(body), "-,3,-,-");
            Check("and the body is still carried", body.CarryingCharacter, "Bob");

            // "dropped" with a companion carried (MAINMOD2.BAS:2039): M5943 names the one carried,
            // the heavier one is picked up (2050-2054) and the old one swept (2071-2077)
            var swap = Owner();
            swap.Companions[0] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 5 };
            swap.CarryingCompanion = 0;
            swap.Companions[2] = new Companion { MonsterId = smallDef.Id, Hits = -3, MaxHits = 50 };
            said = CompanionCarry.Run(swap, true, c => Sized(c.MonsterId));
            Check("carrying #1, a heavier #3 falls", string.Join("|", said),
                  "\"Ann\" dropped Companion #1 to carry Companion #3.|You are now carrying Companion #3.");
            Check("#3 is carried", swap.CarryingCompanion, 2);
            Check("and #1 is let go", Held(swap), "-,-,0,-");

            // "dropped" is said before the size test (2039 then 2050), so a heavier body too big
            // to lift is announced as the carry, then let go, and the old carry stays
            var tooBig = Owner();
            tooBig.Companions[0] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 5 };
            tooBig.CarryingCompanion = 0;
            tooBig.Companions[2] = new Companion { MonsterId = hugeDef.Id, Hits = 0, MaxHits = 50 };
            said = CompanionCarry.Run(tooBig, true, c => Sized(c.MonsterId));
            Check("\"dropped\", then too big", string.Join("|", said),
                  "\"Ann\" dropped Companion #1 to carry Companion #3.|Companion #3 was too big to carry.");
            Check("the old carry is kept", tooBig.CarryingCompanion, 0);
            Check("#3 is gone, #1 stays", Held(tooBig), "0,-,-,-");

            // somebody not on screen (pv0360 <> Me) is told by name (MAINMOD2.BAS:2056)
            var other = Owner();
            other.Companions[1] = new Companion { MonsterId = smallDef.Id, Hits = 0, MaxHits = 5 };
            said = CompanionCarry.Run(other, false, c => Sized(c.MonsterId));
            Check("picked up by somebody not on screen", string.Join("|", said), "Ann, you are now carrying Companion #2.");
            Check("and carried", other.CarryingCompanion, 1);

            // an empty slot keeps its last name until the page is painted for its owner
            // (MAINMOD2.BAS:4745), and fn1163 fills a slot without writing a name (383-400)
            var stale = Owner();
            stale.Companions[0] = new Companion { MonsterId = smallDef.Id, Name = "Fido", Hits = 5, MaxHits = 5 };
            stale.Companions.Release(0);
            Check("an emptied slot still answers to its name", stale.CompanionName(0), "\"Fido\"");
            stale.Companions.Add(new Companion { MonsterId = smallDef.Id, Hits = 5, MaxHits = 5 }, keepSlotName: true);
            Check("and a creature charmed into it takes the name", stale.CompanionName(0), "\"Fido\"");
            stale.Companions.Release(0);
            stale.Companions.BlankEmptyNames();
            stale.Companions.Add(new Companion { MonsterId = smallDef.Id, Hits = 5, MaxHits = 5 }, keepSlotName: true);
            Check("once the page has blanked it, the newcomer is unnamed", stale.CompanionName(0), "Companion #1");
        }

        // ---- the spit, end to end ----
        // Unlike the breath it has no once-a-round guard, and on a mishap it names either the
        // character or one companion rather than splashing the lot.
        Console.WriteLine("\na companion's acid spit (MONSMOD.BAS:968-1027)");
        {
            var sMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var sItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Monster? MonBy(short id) => sMons.FirstOrDefault(m => m.Id == id);
            Item? ItBy(short id) => sItems.FirstOrDefault(i => i.Id == id);

            // a spitter that does not also breathe, so what is seen is the spit
            // every spitter in MDATA5 also breathes, so the two are told apart by their wording
            var spitter = sMons.Where(m => m.Id != 0 && (m.SpecialAttackFlags & (1 << 5)) != 0)
                              .OrderByDescending(m => m.GuildLevel).First();
            var prey = sMons.First(m => m.Id != 0 && m.Hits > 20 && m.GuildLevel < spitter.GuildLevel);
            Check("a spitter exists", spitter.Name.Trim().Length > 0, true);

            int onChar = 0, onComp = 0, onMonsters = 0;
            for (int run = 0; run < 4000; run++)
            {
                var party = new Party();
                for (int i = 0; i < 3; i++)
                {
                    var m = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    m.MaxHits = 9000; m.CurHits = 9000;
                    party.Members.Add(m);
                }
                party.Members[1].Companions.Add(new Companion
                { MonsterId = spitter.Id, Hits = 900, MaxHits = 900, BindingLevel = 5 });
                // the other two hold something for the spit to land on
                foreach (int who in new[] { 0, 2 })
                    party.Members[who].Companions.Add(new Companion
                    { MonsterId = prey.Id, Hits = 900, MaxHits = 900, BindingLevel = 5 });

                var foes = new List<CombatMonster>
                {
                    new() { Def = prey, Hits = 9000 }, new() { Def = prey, Hits = 9000 },
                };
                var fight = new CombatEngine(party, foes, ItBy, new Random(7000 + run));
                fight.UseCompanionDefs(c => MonBy(c.MonsterId));
                int before = foes.Sum(f => f.Hits);
                var lines = fight.RoundPaced(new List<Spell>()).Select(l => l.Text).ToList();

                if (lines.Any(l => l.Contains("accidently spit on") && l.Contains("belonging to"))) onComp++;
                else if (lines.Any(l => l.Contains("accidently spit on"))) onChar++;
                else if (foes.Sum(f => f.Hits) < before) onMonsters++;
            }

            CheckAtLeast("rounds where the spit went at the monsters", onMonsters, 1000);
            CheckAtLeast("mishaps that landed on a character", onChar, 1);
            CheckAtLeast("mishaps that landed on a companion", onComp, 1);
        }

        // ---- the companion's blow can land on a friend, and so can what rides on it ----
        // monCompanionAttack draws the victim at MONSMOD.BAS:961-967, on the spit's odds, *before*
        // it asks whether the companion can spit at all. The blow then aims at that victim. These
        // use made-up companions so that one rule is visible at a time: a striker with no special
        // attacks, a stoner and an electrocuter.
        Console.WriteLine("\na companion's blow, stone and electrocution (MONSMOD.BAS:961-1319)");
        {
            var real = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var cItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? ItOf(short id) => cItems.FirstOrDefault(i => i.Id == id);

            // guild level 1 puts the mishap at its 20% ceiling; no breath, no spit, no spells
            Monster Made(short id, int attacks, short level, short con) => new Monster
            {
                Name = $"probe{id}", Id = id, Att = 400, Def = 0, Hits = 900, GuildLevel = level,
                LevelFound = 0, SpecialAttackFlags = attacks, DamageMod = 1,
                Stats = new short[] { 30, 10, 10, con, 10, 10, 10 },
            };
            // a foe that hits nobody, so every change to the party is the companion's doing
            Monster Foe(short id, short con) => new Monster
            {
                Name = $"foe{id}", Id = id, Att = 0, Def = 0, Hits = 30000, GuildLevel = 1,
                Stats = new short[] { 1, 1, 1, con, 1, 1, 1 },
            };

            List<CombatEngine.CombatLine> Round(Party party, Monster foe, IEnumerable<Monster> table, int seed)
            {
                var fight = new CombatEngine(party, new[] { new CombatMonster { Def = foe, Hits = 30000 } },
                                             ItOf, new Random(seed));
                fight.UseMonsterTable(table.ToList());
                return fight.RoundPaced(new List<Spell>()).ToList();
            }
            Party Three(Monster companion, bool othersHoldOne, Monster held)
            {
                var party = new Party();
                for (int i = 0; i < 3; i++)
                {
                    var m = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    m.MaxHits = 90000; m.CurHits = 90000;
                    party.Members.Add(m);
                }
                party.Members[1].Companions.Add(new Companion { MonsterId = companion.Id, Hits = 900, MaxHits = 900 });
                if (othersHoldOne)
                    foreach (int who in new[] { 0, 2 })
                        party.Members[who].Companions.Add(new Companion { MonsterId = held.Id, Hits = 90000, MaxHits = 90000 });
                return party;
            }

            // -- a plain striker: no spit bit, and still it hits friends --
            var striker = Made(30001, 0, 1, 10);
            var pet = Made(30002, 0, 1, 10);
            var dummy = Foe(30003, 10);
            int onM0 = 0, onM2 = 0, onComp = 0, namedOwner = 0, captionRight = 0, captionWrong = 0, rounds = 3000;
            for (int run = 0; run < rounds; run++)
            {
                var party = Three(striker, true, pet);
                var lines = Round(party, dummy, new[] { striker, pet }, 9100 + run);
                bool m0 = lines.Any(l => l.Text.Contains("accidently hit \"m0\""));
                bool caption = lines.Any(l => l.Text.StartsWith("You were accidently hit for"));
                if (m0) onM0++;
                if (lines.Any(l => l.Text.Contains("accidently hit \"m2\""))) onM2++;
                if (lines.Any(l => l.Text.Contains("accidently hit") && l.Text.Contains("belonging to"))) onComp++;
                if (lines.Any(l => Enumerable.Range(0, 3).Any(i => l.Text.StartsWith($"m{i}:") && l.Text.Contains($"\"m{i}\"")))) namedOwner++;
                // the caption is for the character on screen, m0, and for nobody else
                if (m0 && caption) captionRight++;
                if (!m0 && caption) captionWrong++;
            }
            CheckAtLeast("a companion with no spit still hits m0 by accident", onM0, 100);
            CheckAtLeast("and m2", onM2, 100);
            CheckAtLeast("and a companion of theirs", onComp, 100);
            Check("but never its own owner", namedOwner, 0);
            Check("\"You were accidently hit\" whenever m0 is hit", captionRight, onM0);
            Check("and never when somebody else is", captionWrong, 0);

            // -- the stone reads the companion's own CON when the target is a monster --
            // MONSMOD.BAS:1226. A stoner with CON 10 and floor 0 gives
            //     10 * (Log(10) / Log(2.5)) - 0 = 10 * 2.5129 = 25.129
            // and the foe is stoned when Rnd * 100 exceeds that: 74.87% of the blows that land.
            // Were the foe's own CON read, a CON 3 foe would go 96.4% of the time and a CON 40
            // one 5% (capped at 95). Alone, so nothing can go astray.
            var stoner = Made(30004, 1 << 8, 1, 10);
            foreach (short foeCon in new short[] { 3, 40 })
            {
                var target = Foe((short)(30010 + foeCon), foeCon);
                int landed = 0, stoned = 0, told = 0;
                for (int run = 0; run < 6000; run++)
                {
                    var party = new Party();
                    var lone = Character.Create("solo", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                                new[] { 15, 15, 15, 15, 15, 15 });
                    lone.MaxHits = 90000; lone.CurHits = 90000;
                    lone.Companions.Add(new Companion { MonsterId = stoner.Id, Hits = 900, MaxHits = 900 });
                    party.Members.Add(lone);
                    var fight = new CombatEngine(party, new[] { new CombatMonster { Def = target, Hits = 30000 } },
                                                 ItOf, new Random(40000 + run));
                    fight.UseCompanionDefs(c => c.MonsterId == stoner.Id ? stoner : null);
                    var lines = fight.RoundPaced(new List<Spell>()).ToList();
                    int left = fight.Monsters[0].Hits;
                    if (left < 30000) landed++;
                    if (left == 0) stoned++;
                    // fn128B names an unnamed companion by its slot, and the owner is on screen
                    if (left == 0 && lines.Any(l => l.Text == "Companion #1 Stoned one!" && l.Channel == LineChannel.CharacterHit)) told++;
                }
                CheckNear($"a CON {foeCon} foe is stoned at the companion's rate, 74.87%",
                          stoned * 100.0 / Math.Max(1, landed), 72.5, 77.5);
                Check($"and every stoning of the CON {foeCon} foe says \"Companion #1 Stoned one!\"", told, stoned);
            }

            // -- a stone on the last of a group, then the electrocution on its record --
            // The stone leaves the record's hits (MONSMOD.BAS:1241-1249) and sub0D08 reloads nothing
            // with nobody left, so a stoner that also electrocutes strikes that record (1268-1296):
            // SHOCK after "Stoned one!". The port had ended the attack at the stone.
            var stoneZap = Made(30008, (1 << 8) | (1 << 6), 1, 10);
            var lonely = Foe(30013, 3);
            int lastStoned = 0, shockedAfter = 0, stillDead = 0;
            for (int run = 0; run < 3000; run++)
            {
                var party = new Party();
                var lone = Character.Create("solo", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                            new[] { 15, 15, 15, 15, 15, 15 });
                lone.MaxHits = 90000; lone.CurHits = 90000;
                // companions are looked up by table index now (CompanionRef): a one-row table
                stoneZap.Ref = 0;
                lone.Companions.Add(new Companion { Ref = 0, MonsterId = stoneZap.Id, Hits = 900, MaxHits = 900 });
                party.Members.Add(lone);
                var fight = new CombatEngine(party, new[] { new CombatMonster { Def = lonely, Hits = 30000 } },
                                             ItOf, new Random(70000 + run));
                fight.UseMonsterTable(new[] { stoneZap });
                var lines = fight.RoundPaced(new List<Spell>()).ToList();
                int at = lines.FindIndex(l => l.Text == "Companion #1 Stoned one!");
                if (at < 0) continue;
                lastStoned++;
                if (lines.Skip(at + 1).Any(l => l.HitSounds != null && l.HitSounds.Contains("SHOCK"))) shockedAfter++;
                if (fight.Monsters[0].Hits == 0) stillDead++;
            }
            CheckAtLeast("rounds where the last of a group was stoned", lastStoned, 100);
            CheckAtLeast("and the electrocution followed on its record, SHOCK", shockedAfter, lastStoned * 9 / 10);
            Check("the stoned one stays dead", stillDead, lastStoned);

            // -- the electrocution caption has no "is it you" test --
            // MONSMOD.BAS:1300: whoever the companion electrocuted, the character on screen is
            // told "You were accidently electrocuted". m0 is on screen; m2 is the one hit.
            var zapper = Made(30005, 1 << 6, 1, 10);
            int m2Zapped = 0, m0Told = 0;
            for (int run = 0; run < 3000; run++)
            {
                var party = Three(zapper, false, zapper);
                var lines = Round(party, dummy, new[] { zapper }, 60000 + run);
                if (!lines.Any(l => l.Text.Contains("accidently electrocuted \"m2\""))) continue;
                m2Zapped++;
                if (lines.Any(l => l.Text.StartsWith("You were accidently electrocuted for")
                                && l.Actor == party.Members[0] && l.Channel == LineChannel.MonsterHit)) m0Told++;
            }
            CheckAtLeast("rounds where m2 was electrocuted by accident", m2Zapped, 50);
            Check("and in every one, m0 on screen is told it was them", m0Told, m2Zapped);

            // -- the mishaps, heard (MONSMOD.BAS:1175-1213, 1256-1258, 1276-1278, 1314-1316) --
            // m1's striker is a dragon (sub0D35: DRAGON); m0 holds a one-hit undead (sub0CB7:
            // DI-UNDEA). A blow that kills the undead plays DI-UNDEA once, after the line, under
            // the owner m1's No Party Sound test - and no DRAGON, the Exit For at 1207 skipping
            // the attack cry. A blow on m0 that m0 survives plays DRAGON once, m1's to hear.
            var dragon = Made(30006, 0, 1, 10); dragon.Type = (short)MonsterKind.Dragon;
            var frail = Made(30007, 0, 1, 10); frail.Type = (short)MonsterKind.Undead;
            int killed = 0, cryOnce = 0, cryAfter = 0, ownerHears = 0, victimHushed = 0, noDragon = 0;
            int struck = 0, dragonOnce = 0, dragonOwner = 0, dragonHushed = 0;
            for (int run = 0; run < 4000; run++)
            {
                var party = Three(dragon, false, dragon);
                party.Members[0].Companions.Add(new Companion { MonsterId = frail.Id, Hits = 1, MaxHits = 1 });
                var lines = Round(party, dummy, new[] { dragon, frail }, 70000 + run);
                var m0 = party.Members[0]; var m1 = party.Members[1];
                int said = lines.FindIndex(l => l.Text.StartsWith("m1: ") && l.Text.Contains("accidently hit") && l.Text.Contains("belonging to \"m0\""));
                if (said >= 0)
                {
                    killed++;
                    var heardAll = CombatSounds.OfLines(lines, m0, false);
                    if (heardAll.Count(s => s == "DI-UNDEA") == 1) cryOnce++;
                    if (!heardAll.Contains("DRAGON")) noDragon++;
                    int cry = lines.FindIndex(l => l.HitSounds != null && l.HitSounds.Contains("DI-UNDEA"));
                    if (cry > said && lines[cry].Text == "" && lines[cry].Actor == m1) cryAfter++;
                    if (CombatSounds.OfLines(lines, m1, true).Contains("DI-UNDEA")) ownerHears++;
                    if (!CombatSounds.OfLines(lines, m0, true).Contains("DI-UNDEA")) victimHushed++;
                }
                else if (lines.Any(l => l.Text.StartsWith("m1: ") && l.Text.Contains("accidently hit \"m0\"")))
                {
                    struck++;
                    if (CombatSounds.OfLines(lines, m0, false).Count(s => s == "DRAGON") == 1) dragonOnce++;
                    if (CombatSounds.OfLines(lines, m1, true).Contains("DRAGON")) dragonOwner++;
                    if (!CombatSounds.OfLines(lines, m0, true).Contains("DRAGON")) dragonHushed++;
                }
            }
            CheckAtLeast("blows that killed m0's companion by accident", killed, 30);
            Check("each plays its death cry DI-UNDEA once", cryOnce, killed);
            Check("on a line of its own after the \"accidently hit\" line, m1's", cryAfter, killed);
            Check("and no attack cry after that blow", noDragon, killed);
            Check("No Party Sound: m1, the owner, hears the death cry", ownerHears, killed);
            Check("and m0, whose companion it was, does not", victimHushed, killed);
            CheckAtLeast("blows on m0 by accident", struck, 30);
            Check("each plays the striker's attack cry DRAGON once", dragonOnce, struck);
            Check("No Party Sound: the owner hears it", dragonOwner, struck);
            Check("and m0, who was hit, does not", dragonHushed, struck);
        }

        // ---- the gold is in the chest ----
        // sub0C53 (MONSMOD.BAS:783-789) pays monGetGold at victory only when the fight left no
        // chest; with one, monOpenChest pays it (3660). The port paid it at victory either way.
        Console.WriteLine("\ngold waits in the chest (MONSMOD.BAS:783-789, 3660)");
        {
            var gMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var gItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var boxer = gMons.First(m => m.Id != 0 && m.BoxChance.Any(b => b > 0) && m.GoldFactor > 0);
            int withChest = 0, withoutChest = 0, paidAtVictoryWithChest = 0, unpaidWithout = 0, chestPaid = 0, paidTwice = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                var lone = Character.Create("g", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                            new[] { 15, 15, 15, 15, 15, 15 });
                lone.GoldOnHand = 0;
                var fight = new CombatEngine(lone, new[] { new CombatMonster { Def = boxer, Hits = 0 } },
                                             id => gItems.FirstOrDefault(i => i.Id == id), new Random(seed));
                long owed = fight.GoldEarned;
                if (owed <= 0) continue;
                fight.AwardSpoils();
                if (fight.ChestClass > 0)
                {
                    withChest++;
                    if (lone.GoldOnHand != 0) paidAtVictoryWithChest++;
                    fight.PayGold();                         // the chest is opened
                    if (lone.GoldOnHand == owed) chestPaid++;
                    fight.PayGold();                         // Fight.Gold was emptied
                    if (lone.GoldOnHand != owed) paidTwice++;
                }
                else
                {
                    withoutChest++;
                    if (lone.GoldOnHand != owed) unpaidWithout++;
                }
            }
            CheckAtLeast("fights that left a chest", withChest, 20);
            CheckAtLeast("fights that did not", withoutChest, 20);
            Check("a chest's gold is not paid at victory", paidAtVictoryWithChest, 0);
            Check("it is paid in full when the chest is opened", chestPaid, withChest);
            Check("and only once", paidTwice, 0);
            Check("with no chest, victory pays it all", unpaidWithout, 0);
        }

        // ---- each character whole, then the next ----
        // mdrmulInitiateCombatRound (MULTIMOD.BAS:511-539): a character's action, then their
        // companions, then the next character - and the loop leaves when nothing is standing. So
        // when m0's companion kills the last monster, m1 never acts. m1 holds a Fight order with
        // no weapon, which says "Using no weapon!" whenever m1 does get a turn.
        Console.WriteLine("\na round is each character whole (MULTIMOD.BAS:511-539)");
        {
            var oItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var killer = new Monster
            {
                Name = "probe", Id = 30100, Att = 900, Def = 0, Hits = 900, GuildLevel = 999,
                SpecialAttackFlags = 0, DamageMod = 1, Stats = new short[] { 90, 10, 10, 10, 10, 10, 10 },
            };
            var victim = new Monster { Name = "one-hit", Id = 30101, Hits = 1, GuildLevel = 1, Stats = new short[7] };
            int killedByCompanion = 0, m1ActedAfter = 0;
            for (int run = 0; run < 200; run++)
            {
                var party = new Party();
                for (int i = 0; i < 2; i++)
                {
                    var m = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    m.MaxHits = 9000; m.CurHits = 9000;
                    party.Members.Add(m);
                }
                party.Members[0].Companions.Add(new Companion { MonsterId = killer.Id, Hits = 900, MaxHits = 900 });
                party.OrderFor(party.Members[1]).Kind = OrderKind.Fight;
                var foe = new CombatMonster { Def = victim, Hits = 1 };
                var fight = new CombatEngine(party, new[] { foe }, id => oItems.FirstOrDefault(i => i.Id == id), new Random(500 + run));
                fight.UseCompanionDefs(c => c.MonsterId == killer.Id ? killer : null);
                var lines = fight.RoundPaced(new List<Spell>()).ToList();
                if (foe.Hits > 0) continue;
                killedByCompanion++;
                if (lines.Any(l => l.Actor == party.Members[1] && l.Text.Contains("Using no weapon!"))) m1ActedAfter++;
            }
            CheckAtLeast("rounds where m0's companion killed the only monster", killedByCompanion, 150);
            Check("and m1, next in line, never got to act", m1ActedAfter, 0);
        }

        // ---- opening a chest in the middle of a fight ----
        // The count is CInt(10 - (Log(disarm - 10) * 2)), never under 2 (MAINMOD2.BAS:4080-4083):
        //   disarm 11 -> 10 - 0          = 10
        //   disarm 12 -> 10 - 2(0.6931)  = 8.614 -> 9
        //   disarm 20 -> 10 - 2(2.3026)  = 5.395 -> 5
        //   disarm 60 -> 10 - 2(3.9120)  = 2.176 -> 2
        //   disarm 99 -> 10 - 2(4.4886)  = 1.023 -> 1, raised to 2
        //   disarm 10 -> Log(0), an error: no count at all
        Console.WriteLine("\nopening a chest mid-fight (MAINMOD2.BAS:4063-4090, MULTIMOD.BAS:517-531)");
        {
            Check("rounds at disarm 11", CombatEngine.OpeningRounds(11), (int?)10);
            Check("rounds at disarm 12", CombatEngine.OpeningRounds(12), (int?)9);
            Check("rounds at disarm 20", CombatEngine.OpeningRounds(20), (int?)5);
            Check("rounds at disarm 60", CombatEngine.OpeningRounds(60), (int?)2);
            Check("rounds at disarm 99", CombatEngine.OpeningRounds(99), (int?)2);
            Check("disarm 10 is a Log error and sets nothing", CombatEngine.OpeningRounds(10)?.ToString() ?? "none", "none");

            var cItems2 = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var wall = new Monster { Name = "wall", Id = 30200, Att = 0, Hits = 30000, GuildLevel = 1, Stats = new short[7] };
            var hitter = new Monster
            {
                Name = "hitter", Id = 30201, Att = 200, Hits = 30000, GuildLevel = 20, DamageMod = 1,
                Stats = new short[] { 40, 10, 10, 10, 10, 10, 10 },
            };
            (Party, CombatEngine) Fight(Monster foe, int seed)
            {
                var party = new Party();
                for (int i = 0; i < 2; i++)
                {
                    var m = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                             new[] { 15, 15, 15, 15, 15, 15 });
                    m.MaxHits = 900000; m.CurHits = 900000;
                    party.Members.Add(m);
                }
                var fight = new CombatEngine(party, new[] { new CombatMonster { Def = foe, Hits = 30000 } },
                                             id => cItems2.FirstOrDefault(i => i.Id == id), new Random(seed));
                return (party, fight);
            }

            // m1 (not on screen) counts down from 2; the chest is opened on the second round, by m1
            var (p1, f1) = Fight(wall, 1);
            bool hadGold = f1.GoldEarned > 0;
            f1.ChestRounds[p1.Members[1]] = 2;
            var round1 = f1.RoundPaced(new List<Spell>()).ToList();
            Check("encounter gold is there to be opened for", hadGold, true);
            Check("after one round m1 has one to go", f1.ChestRoundsOf(p1.Members[1]), 1);
            Check("and nothing is opened yet", round1.Count(l => l.OpensChest), 0);
            var round2 = f1.RoundPaced(new List<Spell>()).ToList();
            Check("after the second the count is back to -1", f1.ChestRoundsOf(p1.Members[1]), -1);
            Check("and the chest is opened, once, by m1",
                  string.Join(",", round2.Where(l => l.OpensChest).Select(l => l.Actor?.Name)), "m1");

            // the caption is the one on screen's: m0 counting shows its own count
            var (p2, f2) = Fight(wall, 2);
            f2.ChestRounds[p2.Members[0]] = 4;
            var r = f2.RoundPaced(new List<Spell>()).ToList();
            Check("m0 on screen sees its count", r.Any(l => l.Text == "Opening Chest (3 round(s) to go)" && l.Actor == p2.Members[0]), true);

            // with no gold in the fight the count is dropped without opening anything
            var (p3, f3) = Fight(wall, 3);
            f3.ChestEmptied();
            f3.ChestRounds[p3.Members[0]] = 2;
            var r3 = f3.RoundPaced(new List<Spell>()).ToList();
            Check("no gold: the count goes to -1", f3.ChestRoundsOf(p3.Members[0]), -1);
            Check("and nothing is opened", r3.Count(l => l.OpensChest), 0);

            // MONSMOD.BAS:2907-2909: counting adds .75 to the monster's damage modifier. Same seeds,
            // with and without a count: the blows can only get heavier, and over many they do.
            long plain = 0, busy = 0, plainLast = 0;
            int heavier = 0, lighter = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                foreach (bool counting in new[] { false, true })
                {
                    var (pp, ff) = Fight(hitter, 9000 + seed);
                    pp.Members.RemoveAt(1);
                    if (counting) ff.ChestRounds[pp.Members[0]] = 50;
                    int before = pp.Members[0].CurHits;
                    ff.RoundPaced(new List<Spell>()).ToList();
                    int took = before - pp.Members[0].CurHits;
                    if (counting) busy += took; else plain += took;
                    if (counting && took > (int)(plainLast)) heavier++;
                    if (counting && took < (int)(plainLast)) lighter++;
                    if (!counting) plainLast = took;
                }
            }
            Check("never lighter on the same dice", lighter, 0);
            CheckAtLeast("and heavier on many", heavier, 50);
        }

        // ---- lines this port wrote that the original never does ----
        // Each was checked against the routine it stands in for and found to say nothing there.
        // Comment lines are skipped, because the notes that record the removals quote them.
        Console.WriteLine("\nlines the original never writes (removed 2026-09-25)");
        {
            string root = PortTree.Root;
            var code = new List<string>();
            foreach (string proj in new[] { "src/Mordor.Core", "src/Mordor.App" })
                foreach (string p in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, proj), "*.cs"))
                    code.AddRange(System.IO.File.ReadAllLines(p).Where(l => !l.TrimStart().StartsWith("//"))
                        .Select(l => l.IndexOf("   // ") is int k && k >= 0 ? l[..k] : l));   // and trailing notes
            Check("source files read", code.Count > 10000, true);
            foreach (string gone in new[]
            {
                "takes aim at", "is watching now", "steps up.", "Switched to ", "Nobody is in that slot",
                "You are alone.", "travelling alone", "leaves the party.", "The trap kills you.",
                "You use the ", "is used up.", "Lost in the fight", "paralyzed and cannot act",
                "is empty.\"", "cannot keep casting", "Pick a companion first", "Choose a spell first", "Pick something in the pack first", "Pick a spell first", "You gain 50 spell points", "You gain 200 spell points", "Nobody else is here", "You cannot make it out at all", "You can tell nothing more", "You must bring back one", "lost track of where you are", "Visit the Morgue to be revived", "The Morgue can help", "Accept the guild", "Enter a name.", "Choose an alignment.", "Only one may hold a name", "Save failed: ", "Keep Intelligence and Wisdom at 10", "Welcome to MORDOR!", "nothing to clean", "old entries removed", "You take back the", "You take it back", "The keepers take the body", "No bodies of yours", "Your wounds are tended", "The poison passes", "The disease passes", "sickness finishes", "descend to level", "ascend to level", "There are no stairs here", "stairs lead nowhere", "You fall into a pit", "You cast {", "The lock holds", "springs open", "step through the wall", "back at your sanctuary", "does nothing here", "no companion to bind", "settle the bill", "between purse and bank", "The keepers cannot help", "is yours for {", "They take the {", "is bound to you at level", "is now known completely", "Select a creature", "Select a companion", "You already know the {", "between your purse and the bank", ", completely identified.", "You learn a little more", "is already {", "Sold the {", "squints at it", "You still hold", "Too little of it was left", "no more of those", "Showing everything", "opposed to your alignment", "feel a chill", "You unequip the", "not something you wear", "It is gone.", "You leave them behind", "change places.", "is in the vault", "You enter the dungeon. ", "The fight is over. ", "Could not write CHARSTAT", "Written to {", "Select an item", "The Seer in town can identify it", " join you.", " joins you.", "is warded.", "already that well protected", "Fate turns:", "This place is your sanctuary", "The poison leaves your body", "An unseen force", "You walk into a wall", "not come for you", "do not come.", "not moving yet", "or walk on)", "\"Not enough spell points.\"",
            })
                Check($"no \"{gone}\"", code.Count(l => l.Contains(gone)), 0);
        }

        // ---- a standing spell order that runs out of points ----
        // mdrcharCheckCast (MAINMOD2.BAS:502-510): "Not enough spell points to cast "X"!" for the
        // character the windows show, "Name doesn't have enough spell points to cast "X"!" for
        // anybody else; then sub102F caster, 1 (831-834) - the order becomes Fight, target kept.
        Console.WriteLine("\nout of spell points mid-fight (MAINMOD2.BAS:502-510, 831-834)");
        {
            var sp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var sItems2 = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            // one a Nomad can price: a spell no guild of theirs reaches costs nought (fn1485), and nought
            // is always enough
            var nomad = Character.Create("n", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var spell = sp.First(s => s.Id != 0 && s.Name.Trim().Length > 0 && Spellbook.Cost(s, nomad) > 0);
            var wall2 = new Monster { Name = "wall", Id = 30300, Att = 0, Hits = 30000, GuildLevel = 1, Stats = new short[7] };
            var party = new Party();
            for (int i = 0; i < 2; i++)
            {
                var m = Character.Create($"m{i}", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 15, 15, 15, 15, 15, 15 });
                m.MaxHits = 9000; m.CurHits = 9000; m.CurSpellPoints = 0;
                party.Members.Add(m);
                var o = party.OrderFor(m);
                o.Kind = OrderKind.Spell; o.SpellId = spell.Id; o.Target = 0;
            }
            var fight = new CombatEngine(party, new[] { new CombatMonster { Def = wall2, Hits = 30000 } },
                                         id => sItems2.FirstOrDefault(i => i.Id == id), new Random(1));
            var lines = fight.RoundPaced(sp).Select(l => (l.Text, l.Channel)).ToList();
            string q = "\"" + spell.Name.TrimEnd() + "\"";
            Check("the watched character: the plain wording",
                  lines.Contains(($"Not enough spell points to cast {q}!", LineChannel.Message)), true);
            Check("anybody else: named, without quotes",
                  lines.Contains(($"m1 doesn't have enough spell points to cast {q}!", LineChannel.Message)), true);
            Check("and both now fight", string.Join(",", party.Members.Select(m => party.OrderFor(m).Kind)), "Fight,Fight");
            Check("at the group they had", party.OrderFor(party.Members[1]).Target, 0);
        }

        // ---- a damage spell's captions (mdrcharCastSpell, MAINMOD2.BAS:1403-1601) ----
        // Firebolt reaches four monsters of one group (M5B1D 4) in two groups (M5B28 2) and its verb
        // is gv0972$(1), "Fried". It works from the front and stops at the first survivor.
        Console.WriteLine("\na damage spell's captions (MAINMOD2.BAS:1403-1601)");
        {
            var dsp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var firebolt = dsp.First(s => s.Name.Trim() == "Firebolt");
            Check("Firebolt: four a group, two groups, verb 1", $"{firebolt.AffectGroup},{firebolt.Damage1},{firebolt.AffectMonster}", "4,2,1");
            var dItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Monster Mon(short id, short hits) => new Monster { Name = $"m{id}", Id = id, Hits = hits, GuildLevel = 1, Stats = new short[7] };
            CombatEngine Fight(params CombatMonster[] ms)
            {
                var lone = Character.Create("caster", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                return new CombatEngine(lone, ms, id => dItems.FirstOrDefault(i => i.Id == id), new Random(3));
            }

            // three one-hit monsters in one group: all three go, one caption
            var weak = Mon(30400, 1);
            var f1 = Fight(new() { Def = weak, Hits = 1 }, new() { Def = weak, Hits = 1 }, new() { Def = weak, Hits = 1 });
            f1.CastSpell(firebolt, 0, free: true);
            Check("three killed in one group", string.Join("|", f1.LastCaptions), "You Fried Three!");

            // a tough one in front: it survives, and the two behind it are never touched
            var f2 = Fight(new() { Def = weak, Hits = 30000 }, new() { Def = weak, Hits = 1 }, new() { Def = weak, Hits = 1 });
            f2.CastSpell(firebolt, 0, free: true);
            int dealt = 30000 - f2.Monsters[0].Hits;
            Check("the front survivor stops the spell", string.Join("|", f2.LastCaptions), $"You damaged one for {dealt}!");

            // a room read back with slot 1 emptied: only slots 0, 2 and 3 are members (Finish), and
            // the loop spends a step on slot 1 as it passes (l028E, MAINMOD2.BAS:1426-1448): a
            // three-group Flamesheet from slot 0 burns slots 0 and 2, and its steps are used up
            // before slot 3. The port had closed the gap and reached slot 3.
            var flamesheet = dsp.First(s => s.Name.Trim() == "Flamesheet");
            var slot2Kind = Mon(30403, 1);
            var slot3Kind = Mon(30404, 1);
            var gap = Fight(new() { Def = weak, Hits = 1, Group = 0 }, new() { Def = slot2Kind, Hits = 1, Group = 2 },
                            new() { Def = slot3Kind, Hits = 1, Group = 3 });
            Check("BySlot keeps the emptied slot", string.Join(",", CombatMonster.BySlot(gap.Monsters).Select(s => s.Count)), "1,0,1,1");
            gap.CastSpell(flamesheet, 0, free: true);
            Check("Flamesheet over slots 0, 2, 3 with 1 emptied: two groups, slot 3 untouched",
                  $"{flamesheet.Damage1}: {gap.LastCaptions.Count} / {gap.Monsters[1].Hits} / {gap.Monsters[2].Hits}", "3: 2 / 0 / 1");
            Check("and the two behind it are untouched", f2.Monsters.Count(m => m.Alive), 3);
            // MAINMOD2.BAS:1456 and 1582-1586: the target follows the loop and comes back to the group
            // aimed at while it stands
            Check("the mark sat on the group hit", string.Join(",", f2.LastCaptionAims), "0");
            Check("and the target is back on it", f2.LastCastAim, 0);

            // two groups: the second is reached too, with its own caption
            var other = Mon(30401, 1);
            var f3 = Fight(new() { Def = weak, Hits = 1 }, new() { Def = other, Hits = 1 }, new() { Def = other, Hits = 1 });
            f3.CastSpell(firebolt, 0, free: true);
            Check("one caption per group", string.Join("|", f3.LastCaptions), "You Fried One!|You Fried Two!");
            Check("the mark walked group to group", string.Join(",", f3.LastCaptionAims), "0,1");
            Check("nothing left standing: no target (fn0DCD(-1))", f3.LastCastAim, -1);

            // the first group already dead: sub1019 (MONSMOD.BAS:297-305) moves the target to the first
            // group standing before the cast, so no step is spent skipping the dead one and both
            // live groups are reached - with no target set, and with the target left on the dead group
            var fallen = Mon(30403, 1);
            var third = Mon(30404, 1);
            var f5 = Fight(new() { Def = fallen, Hits = 0 }, new() { Def = other, Hits = 1 }, new() { Def = third, Hits = 1 });
            f5.CastSpell(firebolt, null, free: true);
            Check("no target, first group dead: both live groups reached", string.Join("|", f5.LastCaptions), "You Fried One!|You Fried One!");
            Check("and the mark walked the two live groups", string.Join(",", f5.LastCaptionAims), "1,2");
            var f6 = Fight(new() { Def = fallen, Hits = 0 }, new() { Def = other, Hits = 1 }, new() { Def = third, Hits = 1 });
            f6.CastSpell(firebolt, 0, free: true);
            Check("target on the dead group: both live groups reached", string.Join("|", f6.LastCaptions), "You Fried One!|You Fried One!");

            // a magic-immune monster (property bit 2): nothing, "Failed", and the order becomes Fight
            var immune = new Monster { Name = "immune", Id = 30402, Hits = 1, GuildLevel = 1, SpecialPropertyFlags = 1 << 2, Stats = new short[7] };
            var f4 = Fight(new CombatMonster { Def = immune, Hits = 1 });
            f4.CastSpell(firebolt, 0, free: true);
            Check("a magic-immune monster: Failed", string.Join("|", f4.LastCaptions), "Failed!");
            Check("and the caster is put on Fight", f4.Party.OrderFor(f4.Party.Members[0]).Kind, OrderKind.Fight);
        }

        // ---- a companion's spell (MONSMOD.BAS:1038-1083) ----
        Console.WriteLine("\na companion's spell (MONSMOD.BAS:1038-1083)");
        {
            var csp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var cItems3 = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            // fn0FF5 gives up half the time for anything that is not of a Mage class
            short mage = (short)Enumerable.Range(0, Mdata1.MonsterClasses.Count)
                .First(i => Mdata1.MonsterClassName((short)i).Contains("Mage"));
            Monster Caster(short id, int school, short level, short floor) => new Monster
            {
                Name = $"caster{id}", Id = id, Att = 900, Hits = 900, GuildLevel = level, LevelFound = floor,
                SpellFlags = 1 << school, Type = mage, DamageMod = 1, Stats = new short[] { 90, 10, 10, 10, 10, 10, 10 },
            };
            var sheep = new Monster { Name = "sheep", Id = 30500, Hits = 1, GuildLevel = 1, Stats = new short[7] };
            Character Member(string name)
            {
                var m = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                m.MaxHits = 90000; m.CurHits = 90000;
                return m;
            }
            (Party, CombatEngine) Fight(Party party, Monster[] table, int seed, int foes = 3)
            {
                var ms = Enumerable.Range(0, foes).Select(_ => new CombatMonster { Def = sheep, Hits = 1 }).ToArray();
                var f = new CombatEngine(party, ms, id => cItems3.FirstOrDefault(i => i.Id == id), new Random(seed));
                f.UseMonsterTable(table);
                f.UseSpellTable(csp);
                return (party, f);
            }

            // a lone owner, a level 999 fire caster from floor 3 (Blue Flame: fn0FF5 wants M5B41 > 0,
            // which rules Firebolt out): no mishap is
            // possible alone, so every cast is "Comp. #1 ..." and the companion never also swings
            var fireCaster = Caster(30501, 0, 999, 3);
            int captioned = 0, foreign = 0;
            for (int run = 0; run < 200; run++)
            {
                var party = new Party();
                party.Members.Add(Member("solo"));
                party.Members[0].Companions.Add(new Companion { MonsterId = fireCaster.Id, Hits = 900, MaxHits = 900 });
                var (_, f) = Fight(party, new[] { fireCaster }, 100 + run);
                var lines = f.RoundPaced(csp).Where(l => l.Channel == LineChannel.CharacterHit && l.Text != "").Select(l => l.Text).ToList();
                if (lines.Any(t => t.StartsWith("Comp. #1 "))) captioned++;
                if (lines.Any(t => !t.StartsWith("Comp. #1 ") && !t.StartsWith("Opening"))) foreign++;
            }
            CheckAtLeast("rounds with a \"Comp. #1\" caption", captioned, 40);
            Check("and no caption of any other kind", foreign, 0);

            // two casters: l01EA lets only one of them cast in a round
            int twoCast = 0;
            for (int run = 0; run < 200; run++)
            {
                var party = new Party();
                party.Members.Add(Member("solo"));
                for (int k = 0; k < 2; k++)
                    party.Members[0].Companions.Add(new Companion { MonsterId = fireCaster.Id, Hits = 900, MaxHits = 900 });
                var (_, f) = Fight(party, new[] { fireCaster }, 300 + run, foes: 30);
                var caps = f.RoundPaced(csp).Where(l => l.Text.StartsWith("Comp. #")).Select(l => l.Text[..8]).Distinct().ToList();
                if (caps.Count > 1) twoCast++;
            }
            Check("never two companions casting for one owner in a round", twoCast, 0);

            // the owner's item pays for the companion's cast: mdrcharCastSpell takes a charge off
            // the owner's M5766 whoever casts (MAINMOD2.BAS:841-853). The owner is paralysed, so
            // only the companion acts (MONSMOD.BAS:291); its owner holds an item order.
            int charged = 0, casts = 0, ranOut = 0, castAfterRunOut = 0;
            var wand = cItems3.First(i => i.Id > 0);
            for (int run = 0; run < 200; run++)
            {
                var party = new Party();
                var owner = Member("solo");
                owner.ParalysisLevel = 5;
                party.Members.Add(owner);
                owner.Companions.Add(new Companion { MonsterId = fireCaster.Id, Hits = 900, MaxHits = 900 });
                var (_, f) = Fight(party, new[] { fireCaster }, 100 + run);
                var slot = new InventorySlot { ItemId = wand.Id, Charges = (short)(run % 2 == 0 ? 5 : 0) };
                var order = party.OrderFor(owner);
                order.Kind = OrderKind.Spell; order.SpellId = csp.First(sp => sp.Name.Trim() == "Firebolt").Id;
                f.ItemOrders[owner] = new CombatEngine.ItemOrder(slot, wand, ItemCast: true);
                var lines = f.RoundPaced(csp).Select(l => l.Text).ToList();
                bool cast = lines.Any(t => t.StartsWith("Comp. #1 "));
                if (run % 2 == 0)
                {
                    if (cast) { casts++; if (slot.Charges == 4) charged++; }
                }
                else if (lines.Contains("Your item has run out of charges!"))
                {
                    ranOut++;
                    if (cast || order.Kind != OrderKind.Fight || f.ItemOrders.ContainsKey(owner)) castAfterRunOut++;
                }
            }
            // and the run-out's sub102F owner, -1 (MAINMOD2.BAS:850) takes its first branch when
            // the owner has no spell queued and Att is 0 or 1 - Defend (MULTIMOD.BAS:470-474) - and
            // its second for Defend (Att 2): Fight. A refused cast can leave the slot so
            // (RefusedOrder keeps M5766).
            var toDefend = new Dictionary<OrderKind, int>();
            foreach (var before in new[] { OrderKind.None, OrderKind.Fight, OrderKind.Defend })
            {
                for (int run = 0; run < 200; run++)
                {
                    var party = new Party();
                    var owner = Member("solo");
                    owner.ParalysisLevel = 5;
                    party.Members.Add(owner);
                    owner.Companions.Add(new Companion { MonsterId = fireCaster.Id, Hits = 900, MaxHits = 900 });
                    var (_, f) = Fight(party, new[] { fireCaster }, 900 + run);
                    party.OrderFor(owner).Kind = before;
                    f.ItemOrders[owner] = new CombatEngine.ItemOrder(new InventorySlot { ItemId = wand.Id, Charges = 0 }, wand, ItemCast: false);
                    if (!f.RoundPaced(csp).Any(l => l.Text == "Your item has run out of charges!")) continue;
                    toDefend[before] = (int)party.OrderFor(owner).Kind;
                    break;
                }
            }
            Check("item run dry with no spell queued: None and Fight go to Defend, Defend to Fight",
                  string.Join(",", new[] { OrderKind.None, OrderKind.Fight, OrderKind.Defend }.Select(k => toDefend.TryGetValue(k, out int v) ? ((OrderKind)v).ToString() : "?")),
                  "Defend,Defend,Fight");

            CheckAtLeast("rounds where the companion cast on the owner's item", casts, 20);
            Check("each took one of the item's charges", charged, casts);
            CheckAtLeast("with no charges left, the run-out line", ranOut, 20);
            Check("and then no cast, the owner on Fight and the slot let go (sub102F, MULTIMOD.BAS:476-479)", castAfterRunOut, 0);

            // a healer companion: with a slot passed, mdrcharCastSpell never takes its non-combat
            // branch: the heal is thrown at the monsters as damage, and with M5B1D 0 it reaches none
            var healer = Caster(30502, 9, 999, 2);
            int melted = 0;
            for (int run = 0; run < 300; run++)
            {
                var party = new Party();
                party.Members.Add(Member("solo"));
                party.Members[0].Companions.Add(new Companion { MonsterId = healer.Id, Hits = 900, MaxHits = 900 });
                var (_, f) = Fight(party, new[] { healer }, 600 + run);
                if (f.RoundPaced(csp).Any(l => l.Text == "Comp. #1 failed in casting \"Minor Heal\"!")) melted++;
            }
            CheckAtLeast("a companion's heal goes at the monsters, and with M5B1D 0 fails", melted, 20);

            // a mishap: a level 1 caster (40% ceiling) hits a party member, and the line's
            // " and his companions" depends only on the last slot the loop visits. m0's one
            // companion sits in slot 0 and Blue Flame's loop runs on to slot 2, which is empty - so
            // the line names m0 alone although the companion was hit.
            var clumsy = Caster(30503, 0, 1, 3);
            var pet = new Monster { Name = "pet", Id = 30504, Hits = 30000, GuildLevel = 1, Stats = new short[7] };
            int onM0 = 0, alone = 0, petHit = 0;
            for (int run = 0; run < 600; run++)
            {
                var party = new Party();
                foreach (string n in new[] { "m0", "m1", "m2" }) party.Members.Add(Member(n));
                party.Members[0].Companions.Add(new Companion { MonsterId = pet.Id, Hits = 90000, MaxHits = 90000 });
                party.Members[1].Companions.Add(new Companion { MonsterId = clumsy.Id, Hits = 900, MaxHits = 900 });
                var (_, f) = Fight(party, new[] { clumsy, pet }, 900 + run, foes: 30);
                var lines = f.RoundPaced(csp).Select(l => l.Text).ToList();
                var said = lines.FirstOrDefault(t => t.StartsWith("m1: Companion #1 accidently cast \"Blue Flame\" on \"m0\""));
                if (said == null) continue;
                onM0++;
                if (said == "m1: Companion #1 accidently cast \"Blue Flame\" on \"m0\"!") alone++;
                if (party.Members[0].Companions[0]!.Hits < 90000) petHit++;
            }
            CheckAtLeast("Blue Flames that went astray onto m0", onM0, 10);
            Check("the line names m0 alone every time", alone, onM0);
            CheckAtLeast("though m0's companion was hit in some", petHit, 1);

            // the same mishap killing m0's one-hit undead: its death cry, DI-UNDEA, once, before the
            // line - and under the *victim's* No Party Sound test (l0214 = Me, MONSMOD.BAS:1068-1070),
            // where every other mishap asks the owner's
            var frail = new Monster { Name = "frail", Id = 30505, Hits = 1, GuildLevel = 1, Type = (short)MonsterKind.Undead, Stats = new short[7] };
            int spellKills = 0, spellOnce = 0, spellBefore = 0, victimHears = 0, ownerHushed = 0;
            for (int run = 0; run < 1500; run++)
            {
                var party = new Party();
                foreach (string n in new[] { "m0", "m1", "m2" }) party.Members.Add(Member(n));
                party.Members[0].Companions.Add(new Companion { MonsterId = frail.Id, Hits = 1, MaxHits = 1 });
                party.Members[1].Companions.Add(new Companion { MonsterId = clumsy.Id, Hits = 900, MaxHits = 900 });
                var (_, f) = Fight(party, new[] { clumsy, frail }, 5000 + run, foes: 30);
                var lines = f.RoundPaced(csp).ToList();
                int said = lines.FindIndex(l => l.Text.StartsWith("m1: Companion #1 accidently cast \"Blue Flame\" on \"m0\""));
                if (said < 0 || party.Members[0].Companions[0]!.Hits > 0) continue;
                spellKills++;
                if (CombatSounds.OfLines(lines, party.Members[0], false).Count(s => s == "DI-UNDEA") == 1) spellOnce++;
                int cry = lines.FindIndex(l => l.HitSounds != null && l.HitSounds.Contains("DI-UNDEA"));
                if (cry >= 0 && cry < said && lines[cry].Text == "") spellBefore++;
                if (CombatSounds.OfLines(lines, party.Members[0], true).Contains("DI-UNDEA")) victimHears++;
                if (!CombatSounds.OfLines(lines, party.Members[1], true).Contains("DI-UNDEA")) ownerHushed++;
            }
            CheckAtLeast("Blue Flames that went astray and killed m0's companion", spellKills, 5);
            Check("each plays its death cry DI-UNDEA once", spellOnce, spellKills);
            Check("on a line of its own before the \"accidently cast\" line", spellBefore, spellKills);
            Check("No Party Sound: m0, the victim, hears it", victimHears, spellKills);
            Check("and m1, the owner, does not", ownerHushed, spellKills);
        }

        // ---- where a non-combat spell went (MAINMOD2.BAS:1358-1391) ----
        Console.WriteLine("\nwhere a non-combat spell went (MAINMOD2.BAS:1358-1391)");
        {
            var tsp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var tItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? TIt(short id) => tItems.FirstOrDefault(i => i.Id == id);
            Spell Named(string n) => tsp.First(s => s.Name.Trim() == n);
            Character Make(string name, Sex sex)
            {
                var m = Character.Create(name, RaceData.All[0], Alignment.Neutral, sex, new[] { 15, 15, 15, 15, 15, 15 });
                m.MaxHits = 100; m.CurHits = 10;
                return m;
            }
            var heal = Named("Heal");

            var lone = new Party(); lone.Members.Add(Make("Ann", Sex.Female));
            Check("alone", SpellThrown.Line(lone, heal, new SpellThrown.Target.Person(lone.Members[0])), "\"Heal\" thrown on yourself.");

            var duo = new Party(); duo.Members.Add(Make("Ann", Sex.Female)); duo.Members.Add(Make("Bob", Sex.Male));
            Check("on herself", SpellThrown.Line(duo, heal, new SpellThrown.Target.Person(duo.Members[0])), "\"Ann\" threw \"Heal\" on herself.");
            Check("on somebody else", SpellThrown.Line(duo, heal, new SpellThrown.Target.Person(duo.Members[1])), "\"Ann\" threw \"Heal\" on \"Bob\".");
            Check("on nobody", SpellThrown.Line(duo, heal, new SpellThrown.Target.Nobody()), "\"Heal\" thrown.");
            Check("on the party", SpellThrown.Line(duo, heal, new SpellThrown.Target.Party()), "\"Heal\" thrown on the party.");
            duo.Members[0].Companions.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 50 });
            Check("on a companion", SpellThrown.Line(duo, heal, new SpellThrown.Target.Companion(0)), "\"Heal\" thrown on Companion #1.");

            // Fate never sets l01B6, so it is 0: the line names the party's first character. With
            // Bob in command and casting, it still says Ann.
            var fate = tsp.First(s => SpellEffect.Of(s) == SpellEffect.Fate);
            var p3 = new Party(); p3.Members.Add(Make("Ann", Sex.Female)); p3.Members.Add(Make("Bob", Sex.Male));
            p3.Select(1);
            string fateLine = SpellThrown.Apply(p3, p3.Members[1], fate, new SpellThrown.Target.Nobody(), 1, new Random(1), TIt, null);
            Check("Fate cast by Bob names Ann", fateLine, $"\"Bob\" threw \"{fate.Name.TrimEnd()}\" on \"Ann\".");

            // Cure Paralysis frees the caster, whoever was picked
            var cureP = tsp.First(s => SpellEffect.Of(s) == SpellEffect.CureParalysis);
            var p4 = new Party(); p4.Members.Add(Make("Ann", Sex.Female)); p4.Members.Add(Make("Bob", Sex.Male));
            var fight = new CombatEngine(p4, new[] { new CombatMonster { Def = new Monster { Name = "x", Id = 30600, Hits = 9, Stats = new short[7] }, Hits = 9 } }, TIt, new Random(1));
            fight.Paralyzed[p4.Members[0]] = 3;
            fight.Paralyzed[p4.Members[1]] = 3;
            string cureLine = SpellThrown.Apply(p4, p4.Members[0], cureP, new SpellThrown.Target.Person(p4.Members[1]), 1, new Random(1), TIt, fight);
            Check("Cure Paralysis on Bob frees Ann, the caster", $"{fight.ParalyzedRounds(p4.Members[0])},{fight.ParalyzedRounds(p4.Members[1])}", "0,3");
            Check("and the line still names Bob", cureLine, $"\"Ann\" threw \"{cureP.Name.TrimEnd()}\" on \"Bob\".");

            // a resistance already at 95 is left alone, without a word of refusal
            var resist = tsp.First(s => SpellEffect.ResistKind(SpellEffect.Of(s)) >= 0 && !SpellEffect.IsParty(s));
            var p5 = new Party(); p5.Members.Add(Make("Ann", Sex.Female));
            int kind = SpellEffect.ResistKind(SpellEffect.Of(resist));
            p5.Members[0].SpellResist[kind] = 95;
            Check("a resist already at 95: just the line",
                  SpellThrown.Apply(p5, p5.Members[0], resist, new SpellThrown.Target.Person(p5.Members[0]), 1, new Random(1), TIt, null),
                  $"\"{resist.Name.TrimEnd()}\" thrown on yourself.");

            // who each asks
            Check("Heal asks party and companions", SpellThrown.WhoToAsk(heal), SpellThrown.Ask.PeopleAndCompanions);
            Check("Cure Paralysis too (SPELLHEAL)", SpellThrown.WhoToAsk(cureP), SpellThrown.Ask.PeopleAndCompanions);
            Check("Fate asks nobody", SpellThrown.WhoToAsk(fate), SpellThrown.Ask.None);
        }

        // ---- a charm is a damage spell that keeps what it would have killed ----
        // MAINMOD2.BAS:1410-1416, 1473-1477, 1511-1517, 1527-1528 and fn1163 (383-405).
        Console.WriteLine("\ncharming (MAINMOD2.BAS:1410-1528, fn1163)");
        {
            var chs = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var chItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var charmH = chs.First(s => s.Name.Trim() == "Charm Humanoid");
            // effects 75..99 bite on one class: class index Effect - 75
            short right = (short)Enumerable.Range(0, Mdata1.MonsterClasses.Count).First(i => Mdata1.MonsterClasses[i].Index == charmH.Effect - 75);
            short wrong = (short)Enumerable.Range(0, Mdata1.MonsterClasses.Count).First(i => Mdata1.MonsterClasses[i].Index != charmH.Effect - 75);
            Monster Kind(short id, short type) => new Monster { Name = $"k{id}", Id = id, Hits = 50, GuildLevel = 1, Type = type, Stats = new short[7] };
            (Character, CombatEngine) Cast(Monster def, int hits, int companionsAlready = 0)
            {
                var c = Character.Create("charmer", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                for (int i = 0; i < companionsAlready; i++) c.Companions.Add(new Companion { MonsterId = 1, Hits = 5, MaxHits = 5 });
                c.CurSpellPoints = 500;
                var f = new CombatEngine(c, new[] { new CombatMonster { Def = def, Hits = hits } },
                                         id => chItems.FirstOrDefault(i => i.Id == id), new Random(5));
                f.CastSpell(charmH, 0, free: true);
                return (c, f);
            }

            var (c1, f1) = Cast(Kind(30700, right), 1);
            Check("a one-hit humanoid is charmed", string.Join("|", f1.LastCaptions), "You charmed One!");
            Check("it joins with the hits it had, its rolled maximum, bound at the spell's level",
                  c1.Companions.OccupiedCount == 1 ? $"{c1.Companions[0]!.Hits}/{c1.Companions[0]!.MaxHits} bind {c1.Companions[0]!.BindingLevel}" : "none",
                  $"1/1 bind {charmH.Level}");
            Check("and is out of the fight, not killed", f1.Monsters[0].Gone, true);

            var (c2, f2) = Cast(Kind(30701, right), 30000);
            Check("one it cannot kill: Failed", string.Join("|", f2.LastCaptions), "Failed!");
            Check("untouched", f2.Monsters[0].Hits, 30000);
            Check("nobody joins", c2.Companions.OccupiedCount, 0);

            var (_, f3) = Cast(Kind(30702, wrong), 1);
            Check("the wrong class: Failed", string.Join("|", f3.LastCaptions), "Failed!");

            var (c4, f4) = Cast(Kind(30703, right), 1, companionsAlready: 4);
            Check("four companions already: refused", f4.Log.LastOrDefault(), "\"Charm Humanoid\" - No room for more Companions!");
            Check("and the monster is untouched", f4.Monsters[0].Hits, 1);
        }

        // ---- monsters that join (MAINMOD2.BAS:4095-4125) ----
        // fn1163(Me, Foes(0), Foes(0).Hits, -10) in a loop that never loads the next member, so
        // every joiner is made from the front one: its hits, its rolled maximum.
        Console.WriteLine("\nmonsters that join (MAINMOD2.BAS:4095-4125)");
        {
            var kin = new Monster { Name = "kin", Id = 30800, Hits = 40, GuildLevel = 1, Stats = new short[7] };
            var grp = new List<CombatMonster>
            {
                new() { Def = kin, Hits = 7, MaxHits = 12, JoinOffer = 2 },
                new() { Def = kin, Hits = 20, MaxHits = 20 },
                new() { Def = kin, Hits = 30, MaxHits = 30 },
            };
            var who = Character.Create("j", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            string said = EncounterGenerator.AcceptJoin(who, grp, _ => null);
            Check("nothing is said", said, "");
            Check("two came, both with the front one's hits and maximum",
                  string.Join(",", who.Companions.All.Select(c => $"{c.Hits}/{c.MaxHits}")), "7/12,7/12");
            Check("bound at -10", string.Join(",", who.Companions.All.Select(c => c.BindingLevel)), "-10,-10");
            Check("one is left standing", grp.Count, 1);

            var full = Character.Create("f", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            for (int i = 0; i < 4; i++) full.Companions.Add(new Companion { MonsterId = 1, Hits = 1, MaxHits = 1 });
            var g2 = new List<CombatMonster> { new() { Def = kin, Hits = 7, JoinOffer = 1 } };
            Check("no room: the original's line", EncounterGenerator.AcceptJoin(full, g2, _ => null), "Join - No room for more Companions!");
            Check("and the offer stands", g2[0].JoinOffer, 1);
            // a second group behind the first is never taken, whatever the offer says
            var other = new Monster { Name = "other", Id = 30801, Hits = 40, GuildLevel = 1, Stats = new short[7] };
            var mixed = new List<CombatMonster> { new() { Def = kin, Hits = 7, JoinOffer = 2 }, new() { Def = other, Hits = 9 } };
            var w2 = Character.Create("w", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            EncounterGenerator.AcceptJoin(w2, mixed, _ => null);
            Check("only the first group joins", $"{w2.Companions.OccupiedCount} joined, {mixed.Count} left, {mixed[0].Def.Name}", "1 joined, 1 left, other");
        }

        // ---- the realign fee is rounded into a Long ----
        // l011A = ((.72 * Price)) * n, n = 2 across the spectrum (FRMCTYS.BAS:204-208); l011A is
        // a Long, so VB rounds. A price of 4: 2.88 -> 3, a step of two: 5.76 -> 6.
        Console.WriteLine("\nthe realign fee (FRMCTYS.BAS:204-208)");
        {
            var cheap = new Item { Name = "cheap", Id = 30900, Price = 4 };
            Check("price 4, one step: 2.88 rounds to 3", StoreStock.RealignCost(cheap, ItemAlignment.Good, ItemAlignment.Neutral), 3L);
            Check("price 4, Good to Evil: 5.76 rounds to 6", StoreStock.RealignCost(cheap, ItemAlignment.Good, ItemAlignment.Evil), 6L);
            Check("and to the same alignment it still costs (no test for it)", StoreStock.RealignCost(cheap, ItemAlignment.Good, ItemAlignment.Good), 3L);
        }

        // ---- your own body in the morgue (MAINMOD2.BAS:3087-3118) against fn16B5 (330-380) ----
        Console.WriteLine("\nthe morgue, own body against the counter (MAINMOD2.BAS:3087-3118, 330-380)");
        {
            var race = RaceData.All[0];
            int min = race.Min[(int)Stat.Con];
            string? ownLine = null, otherLine = null; int ownCon = -99, otherCon = -99;
            for (int seed = 0; seed < 400 && (ownLine == null || otherLine == null); seed++)
            {
                foreach (bool own in new[] { true, false })
                {
                    var c = Character.Create("Ann", race, Alignment.Neutral, Sex.Female, new[] { 15, 15, 15, 15, 15, 15 });
                    c.Stats[(int)Stat.Con] = min;
                    c.IsDead = true; c.CauseOfDeath = Rules.DeathOrdinary;
                    var o = Raising.Resolve(new Random(seed), c, race, Raising.ByMorgue, 100, _ => null, ownBody: own);
                    string? lost = o.Lines.FirstOrDefault(l => l.StartsWith("MORGUE:"));
                    if (lost == null) continue;
                    if (own && ownLine == null) { ownLine = lost; ownCon = c.Stats[(int)Stat.Con]; }
                    if (!own && otherLine == null) { otherLine = lost; otherCon = c.Stats[(int)Stat.Con]; }
                }
            }
            Check("own body: the routine's own line", ownLine, "MORGUE: You lost a point of Constitution during the raise.");
            Check("and Constitution is lifted back to the race minimum", ownCon, min);
            Check("raised at the counter: fn16B5's line, misspelling and all", otherLine, "MORGUE: \"Ann\" lost a point of Constution during the raise.");
            Check("and no floor", otherCon, min - 1);
        }

        // ---- the binding spells' lines (MAINMOD2.BAS:878, 896, 899) ----
        Console.WriteLine("\nbinding spells (MAINMOD2.BAS:870-914)");
        {
            var bsp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var control = bsp.First(s => SpellEffect.Of(s) == SpellEffect.BindFirst);
            var life = bsp.First(s => SpellEffect.Of(s) == SpellEffect.BindForLife);
            var loose = new Companion { MonsterId = 1, Name = "Rex", Hits = 5, MaxHits = 5, BindingLevel = 1 };
            string raised = SpellEffect.BindCompanion(loose, 2, control, 10);
            Check("by slot, lower case, not by name", raised.StartsWith($"\"{control.Name}\" raised the Bind Level of companion #3 to "), true);
            Check("and again: had no effect", SpellEffect.BindCompanion(loose, 2, control, 10), $"\"{control.Name}\" had no effect!");
            var nameless = new Companion { MonsterId = 1, Name = "", Hits = 5, MaxHits = 5, BindingLevel = 1 };
            Check("for life, named as fn128B does", SpellEffect.BindCompanion(nameless, 0, life, 10), "Companion #1 is now bound to you for life!");
        }

        // ---- the Char page, piece by piece (MAINMOD2.BAS:5224-5334) ----
        Console.WriteLine("\nthe Char page and CHARSTAT.TXT (MAINMOD2.BAS:5224-5334)");
        {
            var saved = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            try
            {
                var race = RaceData.All[0];
                var sheet = Character.Create("Ann ", race, Alignment.Good, Sex.Female, new[] { 15, 15, 15, 15, 15, 15 });
                sheet.Kills = 1234; sheet.Deaths = 3; sheet.Complications = 1; sheet.MonsterQuestsDone = 2; sheet.ItemQuestsDone = 0;
                sheet.PlaySeconds = 5400; sheet.CreatedOn = new DateTime(1996, 3, 1).ToOADate();
                var when = new DateTime(2026, 9, 25, 13, 5, 9);
                var screen = CharStatFile.Paint(sheet, _ => null, toFile: false, when);
                string At(int row, int col) => screen.First(p => p.Row == row && p.Col == col).Text;
                Check("nothing file-only is painted without the flag but the A/D total and spacers",
                      string.Join("|", screen.Where(p => !p.OnScreen).Select(p => p.Text)), "Total A/D:| 24/ 24||||GUILD INFO|Nomad (1) 0/0||Abilities (Lead Guild)|Thieving (Nomad)|Fighting (Nomad)|Perception (Nomad)|");
                Check("Kills, formatNumber", At(2, 6), "1,234");
                Check("Died, Str$", At(3, 6), " 3");
                Check("Comp", At(4, 6), " 1");
                Check("Mon Quests at column 8", At(7, 8), " 2");
                Check("Item Quests", At(8, 8), " 0");
                // all six at 15: STR and DEX 3 over 12 at 2 each, INT and WIS 1 over 14 at 1 each
                Check("Guild A/D, the best guild plus 10", At(10, 8), " 10/10");
                Check("Stats A/D, what the rest leave", At(11, 8), " 14/14");
                Check("Item A/D, nothing worn", At(12, 8), " 0/0");
                Check("Time$", At(14, 8), "13:05:09");
                Check("Play Time, hours to two places", At(16, 8), "1.50 Hrs");
                Check("Created on, CVDate", At(17, 8), "3/1/1996");
                Check("a stat row, Left$ and LTrim$", At(4, 14), "Int  15 (0)");
                sheet.Stats[0] = race.Max[0] + 5;
                Check("bold at the race maximum plus five", CharStatFile.Paint(sheet, _ => null, false, when).First(p => p.Row == 3 && p.Col == 14).Bold, true);
                sheet.Stats[0] = race.Max[0] + 4;
                Check("and not one short of it", CharStatFile.Paint(sheet, _ => null, false, when).First(p => p.Row == 3 && p.Col == 14).Bold, false);
                sheet.Stats[0] = 15;
                Check("and not below it", screen.First(p => p.Row == 4 && p.Col == 14).Bold, false);
                Check("Total Gold, hand and bank", At(14, 14), "1,500");
                sheet.PlaySeconds = 0;
                Check("no hours: Format's # gives no leading nought", CharStatFile.Paint(sheet, _ => null, false, when).First(p => p.Row == 16 && p.Col == 8).Text, ".00 Hrs");

                var file = CharStatFile.Compose(CharStatFile.Paint(sheet, _ => null, toFile: true, when));
                Check("the file opens with the version, bold so no Tab", file.StartsWith("  VERSION: 1.1\r\nCHARACTER: \"Ann\" (Good " + race.Name + ")\r\n\r\nITEM INFO\r\n\r\nBANK ITEM INFO\r\n\r\nKills:\r\n\t1,234\r\n"), true);
                Check("and never reaches its footer", file.Contains("END OF CHARACTER"), false);
                Check("it ends on Total Gold's value", file.EndsWith("Total Gold\r\n\t1,500\r\n"), true);
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = saved; }
        }

        // ---- kills go to the killer, quests are settled at the kill (MONSMOD.BAS:475, MAINMOD2.BAS:1621-1639) ----
        Console.WriteLine("\nkills and monster quests (MONSMOD.BAS:475, MAINMOD2.BAS:1621-1639)");
        {
            var kMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var kItems = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            Item? KItem(short id) => kItems.FirstOrDefault(i => i.Id == id);
            var blade = kItems.First(i => i.Id != 0 && i.Type <= 7 && i.Swings >= 1);   // something to swing
            var prey = kMons.First(m => m.Id != 0 && m.Hits > 20 && !MonsterProperty.Has(m, MonsterProperty.ImmuneToWeapons)
                                          && !MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon));
            string? told = null; int k0 = -1, k1 = -1, q1 = -1; bool done = false; long xpOnBlow = 0;
            for (int run = 0; run < 200 && told == null; run++)
            {
                var party = new Party();
                for (int i = 0; i < 2; i++)
                {
                    var m = Character.Create($"m{i}", RaceData.All[0], i == 0 ? Alignment.Neutral : Alignment.Good,
                                             i == 0 ? Sex.Male : Sex.Female, new[] { 18, 15, 15, 15, 15, 18 });
                    m.MaxHits = 4000; m.CurHits = 4000;
                    party.Members.Add(m);
                }
                party.Members[1].Guild.QuestType = 1; party.Members[1].Guild.QuestTarget = prey.Id;
                party.Members[0].PutInPack(new InventorySlot { ItemId = blade.Id, Known = IdLevel.All, Equipped = true });
                var fight = new CombatEngine(party, new[] { new CombatMonster { Def = prey, Hits = 1 } }, KItem, new Random(3100 + run));
                foreach (var _ in fight.PlayerSwings(party.Members[0])) { }
                if (party.Members[0].Kills == 0) continue;
                told = string.Join("|", fight.TakeSaid().Select(l => l.Text));
                k0 = party.Members[0].Kills; k1 = party.Members[1].Kills;
                xpOnBlow = party.Members[0].Guild.Experience;
                q1 = party.Members[1].MonsterQuestsDone; done = party.Members[1].Guild.QuestDone;
            }
            Check("experience is paid on the blow, not at the end (sub0CDF -> sub0F11)", xpOnBlow > 0, true);
            Check("the killer's count", k0, 1);
            Check("and nobody else's", k1, 0);
            Check("the quest is someone else's, and it is done", $"{q1} {done}", "1 True");
            Check("in a party it is said by name", told, "\"m1\" completed her quest!");

            string? alone = null;
            for (int run = 0; run < 200 && alone == null; run++)
            {
                var solo = Character.Create("s", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 15, 15, 15, 15, 18 });
                solo.MaxHits = 4000; solo.CurHits = 4000;
                solo.Guild.QuestType = 1; solo.Guild.QuestTarget = prey.Id;
                solo.PutInPack(new InventorySlot { ItemId = blade.Id, Known = IdLevel.All, Equipped = true });
                var fight = new CombatEngine(solo, new[] { new CombatMonster { Def = prey, Hits = 1 } }, KItem, new Random(3300 + run));
                fight.Party.ApplyDefaultOrders();   // Combat Setting 1, Fight
                var said = fight.RoundPaced(new List<Spell>()).Where(l => l.Channel == LineChannel.Message).Select(l => l.Text).ToList();
                if (solo.Kills > 0) alone = string.Join("|", said.Where(l => l.Contains("quest")));
            }
            Check("alone, the round passes it on as a message", alone, "You have completed your quest!");
        }

        // ---- the play clock (MAINMOD2.BAS:6004-6023) ----
        Console.WriteLine("\nthe play clock, sub16F8 (MAINMOD2.BAS:6004-6023)");
        {
            var a = Character.Create("a", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var b = Character.Create("b", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var clock = new PlayClock();
            var pair = new List<Character> { a, b };
            clock.Tick(pair, 100f);
            clock.Tick(pair, 101f);
            Check("the first call starts the first and leaves: the second waits a call", $"{a.PlaySeconds} {b.PlaySeconds}", "1 0");
            clock.Tick(pair, 102.5f);
            Check("then both count", $"{a.PlaySeconds} {b.PlaySeconds}", "2.5 1.5");
            clock.Tick(pair, 86300f);
            clock.Tick(pair, 10f);
            Check("past midnight the day is taken as 86340 seconds", a.PlaySeconds, 2.5 + (86300 - 102.5) + 50);
        }

        // ---- the Guild page, piece by piece (MAINMOD2.BAS:5335-5359) ----
        Console.WriteLine("\nthe Guild page (MAINMOD2.BAS:5335-5359)");
        {
            var gsp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var gc = Character.Create("g", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var page = GuildTabPage.Paint(gc, _ => null, gsp);
            string Row(int r, int col) => page.First(x => x.Row == r && x.Col == col).Text;
            Check("heading, then the guild and its level a column in", $"{Row(2, 1)}|{Row(3, 2)}", "Guilds|Nomad (1)");
            Check("Total Skill two rows below the last guild", Row(5, 1), "Total Skill");
            Check("fighting from the Hall's own index", Row(6, 2), HallRecords.FightingSkill(gc, _ => null) + " Fighting");
            Check("Current Guild", $"{Row(10, 1)}|{Row(11, 2)}", "Current Guild|Nomad");
            Check("every ability, fn123F never giving less than 1", page.Count(x => x.Col == 12), 6);
            Check("names every third row, bold", string.Join(",", page.Where(x => x.Col == 12).Select(x => x.Row + (x.Bold ? "b" : ""))), "3b,6b,9b,12b,15b,18b");
            Check("the rating two rows under, in italics", page.First(x => x.Row == 5 && x.Col == 13).Italic, true);

            // a stale guild: dual guilds with no modifier for ability a, only the later one rating a-1
            string? stale = null, want = null;
            var table = GuildData.All;
            for (int a = 2; a <= 3 && stale == null; a++)
                for (int i = 0; i < table.Count && stale == null; i++)
                    for (int j = i + 1; j < table.Count && stale == null; j++)
                    {
                        double M(int g, int ab) => table[g].Abilities.Length > ab ? table[g].Abilities[ab] : 0;
                        if (M(i, a) != 0 || M(j, a) != 0 || M(i, a - 1) != 0 || M(j, a - 1) <= 0) continue;
                        var dual = Character.Create("t", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                        dual.Guilds.Clear();
                        dual.Guilds.Add(new GuildMembership { Guild = table[j].Name, Level = 5 });
                        dual.Guilds.Add(new GuildMembership { Guild = table[i].Name, Level = 5 });
                        var pg = GuildTabPage.Paint(dual, _ => null, gsp);
                        stale = pg.First(x => x.Row == 3 * (a + 1) + 2 && x.Col == 13).Text;
                        want = "1 from " + table[j].Name + " (not " + table[i].Name + ")";
                        stale += " (not " + table[i].Name + ")";
                    }
            Check("nobody rates it: the guild of the call before", stale, want);
        }

        // ---- the Resist. page (MAINMOD2.BAS:5209-5219) ----
        Console.WriteLine("\nthe Resist. page (MAINMOD2.BAS:5209-5219)");
        {
            var rc = Character.Create("r", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            int baseFire = Effects.ResistancePercent(rc, 0, _ => null);
            rc.SpellResist[0] = Math.Max(baseFire, 0) + 40;
            var rp = ResistTabPage.Paint(rc, _ => null);
            string R(int row, int col) => rp.First(x => x.Row == row && x.Col == col).Text;
            Check("heading bold at row 2", $"{R(2, 1)} {rp[0].Bold}", "Current Resistances True");
            Check("Fire at row 4, its percentage at column 12", $"{R(4, 2)}|{R(4, 12)}", $"Fire|{Math.Max(baseFire, 0) + 40}%");
            Check("Acid the eleventh, at row 14", R(14, 2), "Acid");
            int noneRow = Enumerable.Range(0, 11).First(i => Effects.ResistancePercent(rc, i, _ => null) <= 0);
            Check("none is \" -\", space and all", R(4 + noneRow, 12), " -");
        }

        // ---- who is in the room, and the readout (MAZEMOD.BAS:255-346) ----
        Console.WriteLine("\nmdrmazLookAround and lblRoom (MAZEMOD.BAS:255-346)");
        {
            Character Mk(string n) => Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var lead = Mk("Lead"); var ghost = Mk("Ghost"); var far = Mk("Far"); var below = Mk("Below"); var hid = Mk("Hid");
            ghost.IsDead = true; ghost.CurHits = 0; hid.Options.Hide = true;
            var place = new Dictionary<Character, (int, int)>
            { [lead] = (3, 7), [ghost] = (3, 7), [far] = (3, 8), [below] = (4, 7), [hid] = (3, 7) };
            var (hereList, living, deadN) = LookAround.Walk(lead, new[] { ghost, far, lead, below, hid }, c => place[c]);
            Check("the leader's floor and area, in roster order, Hide or no Hide", string.Join(",", hereList.Select(e => e.Name)), "Ghost,Lead,Hid");
            Check("the dead counted apart", $"{living}+{deadN}", "2+1");
            lead.IsDead = true;
            var (_, l2, d2) = LookAround.Walk(lead, new[] { lead }, c => place[c]);
            Check("the leader dead is still counted among the living", $"{l2}+{d2}", "1+0");
            lead.IsDead = false;
            var every = LookAround.Walk(lead, new[] { ghost, far, lead, below }, c => place[c], everywhere: true).Here;
            Check("mode 3 takes everyone", every.Count, 4);

            Check("the readout", LookAround.Readout(lead, lead, 26, 6, 6, Facing.West, 1, 0), "26,6,6 West [ 1 ]");
            Check("with the dead", LookAround.Readout(lead, lead, 26, 6, 6, Facing.West, 2, 1), "26,6,6 West [ 2+1 ]");
            lead.LostLevel = true;
            Check("lost on the level alone still shows it", LookAround.Readout(lead, lead, 26, 6, 6, Facing.West, 1, 0), "26,6,6 West [ 1 ]");
            lead.LostPosition = true;
            Check("lost in both", LookAround.Readout(lead, lead, 26, 6, 6, Facing.West, 1, 0), "(Lost) West [ 1 ]");
            lead.LostLevel = false; lead.LostFacing = true;
            Check("lost in position only, and facing", LookAround.Readout(lead, lead, 26, 6, 6, Facing.West, 1, 0), "(Lost on 6) ? [ 1 ]");
            var deadShown = Mk("D"); deadShown.IsDead = true;
            Check("the one on screen dead", LookAround.Readout(deadShown, lead, 26, 6, 6, Facing.West, 1, 0), "(You are Dead)");

            var look = LookAround.Paint(hereList);
            Check("headings bold at columns 1, 9, 15, 19", string.Join("|", look.Take(4).Select(x => $"{x.Col}{x.Text}{(x.Bold ? "b" : "")}")), "1Nameb|9Guildb|15Levelb|19Hitsb");
            Check("a row: name, current guild, level, hits", string.Join("|", look.Where(x => x.Row == 3).Select(x => x.Text)), "Ghost|Nomad|1|0");
        }

        // ---- sub09CC: bodies and strangers in the area, not the square (MAZEMOD.BAS:286-287, 348-380) ----
        Console.WriteLine("\nsub09CC counts by area (MAZEMOD.BAS:286-287, 348-380)");
        {
            var rf = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            var f1 = rf[0];
            // two different squares of one area on level 1, and a square of another area
            var sq = Enumerable.Range(0, f1.Width * f1.Height).Select(i => (X: i % f1.Width, Y: i / f1.Width))
                .Where(p => f1.AreaIdAt(p.X, p.Y) > 0).ToList();
            var a = sq.GroupBy(p => f1.AreaIdAt(p.X, p.Y)).First(g => g.Count() > 1).ToList();
            var other = sq.First(p => f1.AreaIdAt(p.X, p.Y) != f1.AreaIdAt(a[0].X, a[0].Y));
            Character Mk(string n) => Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            // the port's placement: the record's M59BB, or for an old save the area of its square
            (int, int) Place(Character c) => (c.DungeonLevel, c.AreaOr(k => k.DungeonLevel >= 1 ? rf[k.DungeonLevel - 1].AreaIdAt(k.X, k.Y) : -1));
            List<string> Said(Character leader, Character[] party, params Character[] others)
            {
                var (_, living, dead) = LookAround.Walk(leader, LookAround.Roster(party, others), Place);
                return RoomRoster.Lines(living, dead, party.Count(m => m.CarryingCharacter != ""), party.Length);
            }
            var lead = Mk("Lead"); (lead.DungeonLevel, lead.X, lead.Y) = (1, a[0].X, a[0].Y); lead.Area = f1.AreaIdAt(a[0].X, a[0].Y);
            var misty = Mk("Misty"); misty.IsDead = true; (misty.DungeonLevel, misty.X, misty.Y, misty.Area) = (1, a[1].X, a[1].Y, null);
            Check("a body in the area, on another square (an old save, its square's area)", string.Join("|", Said(lead, new[] { lead }, misty)), "There is a dead body here!");
            misty.Area = lead.Area;
            Check("a body in the area by its M59BB", string.Join("|", Said(lead, new[] { lead }, misty)), "There is a dead body here!");
            var far = Mk("Far"); far.IsDead = true; (far.DungeonLevel, far.X, far.Y, far.Area) = (1, other.X, other.Y, f1.AreaIdAt(other.X, other.Y));
            Check("a body in another area of the floor: nothing", Said(lead, new[] { lead }, far).Count, 0);
            var below = Mk("Below"); below.IsDead = true; (below.DungeonLevel, below.X, below.Y, below.Area) = (2, a[0].X, a[0].Y, lead.Area);
            Check("the same area number on another floor: nothing", Said(lead, new[] { lead }, below).Count, 0);
            var ghost = Mk("Ghost"); ghost.IsDead = true; (ghost.DungeonLevel, ghost.X, ghost.Y, ghost.Area) = (1, a[0].X, a[0].Y, lead.Area);
            Check("two bodies", string.Join("|", Said(lead, new[] { lead }, misty, ghost)), "There are dead bodies here!");
            lead.CarryingCharacter = "Misty";
            Check("a carried body is taken off the count", Said(lead, new[] { lead }, misty).Count, 0);
            Check("carrying one of two", string.Join("|", Said(lead, new[] { lead }, misty, ghost)), "There is a dead body here!");
            lead.CarryingCharacter = "";
            var walker = Mk("Walker"); (walker.DungeonLevel, walker.X, walker.Y, walker.Area) = (1, a[1].X, a[1].Y, lead.Area);
            Check("a stranger elsewhere in the area", string.Join("|", Said(lead, new[] { lead }, walker)), "Somebody else is in the room!");
            var mate = Mk("Mate"); (mate.DungeonLevel, mate.X, mate.Y, mate.Area) = (1, a[0].X, a[0].Y, lead.Area);
            Check("the party is not strangers", Said(lead, new[] { lead, mate }).Count, 0);
            mate.IsDead = true;
            Check("a dead member is a body, and still taken off the living", string.Join("|", Said(lead, new[] { lead, mate }, walker)), "There is a dead body here!");
            lead.IsDead = true;
            Check("the leader's own body is not one", Said(lead, new[] { lead }).Count, 0);
        }

        // ---- the buffers page (MAINMOD2.BAS:4633-4665) ----
        Console.WriteLine("\nthe buffers (mdcharShowBuffer, MAINMOD2.BAS:4633-4665)");
        {
            var bsp2 = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var bc = Character.Create("b", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            bc.Buffers[0] = Character.PackSize + bsp2.Count + 5;   // past the end of the table
            Check("a spell past the table shows nothing", SpellBuffers.Describe(bc, 0, bsp2, _ => null), "(Nothing)");
            Check("and is not cleared", bc.Buffers[0], Character.PackSize + bsp2.Count + 5);
            Check("no cost beside it", SpellBuffers.CostText(bc, 0, bsp2), "");
            var known = bsp2.First(s => Spellbook.Cost(s, bc) > 0);
            bc.Buffers[1] = Character.PackSize + bsp2.IndexOf(known);
            Check("a spell: quoted, cost beside", $"{SpellBuffers.Describe(bc, 1, bsp2, _ => null)}|{SpellBuffers.CostText(bc, 1, bsp2)}", $"\"{known.Name}\"|{Spellbook.Cost(known, bc)}");
            var untaught = bsp2.FirstOrDefault(s => Spellbook.Cost(s, bc) == 0);
            if (untaught != null)
            {
                bc.Buffers[2] = Character.PackSize + bsp2.IndexOf(untaught);
                Check("one no guild of yours prices: ?", SpellBuffers.CostText(bc, 2, bsp2), "?");
            }
        }

        // ---- the item on the Misc page (MAINMOD2.BAS:5040-5168) ----
        Console.WriteLine("\nitem info on the Misc page (MAINMOD2.BAS:5040-5168)");
        {
            var iSp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var probe = new Item { Name = "Blade", Id = 31000, Att = 7, Def = 2, Type = 0, NHands = 1, Swings = 0, SpecialType = 1,
                                   Cursed = 1, ClassRestricted = 1, Guilds = 0b11, Abilities = 0b10, ResistanceFlags = 0b1,
                                   StatsRequired = new short[] { 10, 0, 0, 0, 0, 12, 0 }, StatsMod = new short[] { 2, 0, 0, 0, 0, 0, 0 } };
            var full = ItemInfoPage.Paint(probe, ItemAlignment.Good, IdLevel.All, iSp);
            string At(List<CharStatFile.Piece> pg, int r, int col) => pg.FirstOrDefault(x => x.Row == r && x.Col == col).Text ?? "(none)";
            Check("name, alignment tag, cursed", At(full, 2, 6), "Blade (g) (cursed)");
            Check("class restricted only for -1 exactly", At(full, 4, 6).EndsWith(" (Not Class Restricted)"), true);
            Check("no swings: row 10's text runs on", At(full, 11, 1), "One-handed ItemModifies Stat(s)");
            Check("Req and Mod on their columns", $"{At(full, 6, 3)}|{At(full, 7, 4)}|{At(full, 7, 14)}|{At(full, 8, 4)}", "Str|10|12|2");
            Check("guild level with Str$'s space", At(full, 11, 19).StartsWith(" "), true);
            Check("abilities then resistances, italic", string.Join(",", full.Where(x => x.Col == 2 && x.Row >= 14).Select(x => x.Text + (x.Italic ? "/i" : ""))),
                  $"{Librarian.ItemAbilities[1]}/i,Fire Res/i");
            Check("ID Lev", $"{At(full, 20, 1)}|{At(full, 21, 1)}", "ID Lev:|Completely Identified");
            probe.Swings = 2;
            Check("with swings", At(ItemInfoPage.Paint(probe, ItemAlignment.Good, IdLevel.All, iSp), 11, 1), "One-handed Item, 2 swings");
            var mostly = ItemInfoPage.Paint(probe, ItemAlignment.Good, IdLevel.Mostly, iSp);
            Check("Mostly: name without the tag, no Mod row, guild levels as ?", $"{At(mostly, 2, 6)}|{At(mostly, 8, 1)}|{At(mostly, 11, 19)}", "Blade|(none)|?");
            var barely = ItemInfoPage.Paint(probe, ItemAlignment.Good, IdLevel.Barely, iSp);
            Check("Barely: <Unknown>, no Att/Def", $"{At(barely, 2, 6)}|{At(barely, 3, 1)}", "<Unknown>|(none)");
            Check("Str$ of a Single drops the nought", $"{ItemInfoPage.VbSingle(0.5f)} {ItemInfoPage.VbSingle(-0.5f)} {ItemInfoPage.VbSingle(1.5f)}", ".5 -.5 1.5");
            // the character window spreads an item page's columns to its text (CharacterPanel.Draw)
            Check("an item's page is told from the others, at every id level", $"{ItemInfoPage.IsItemPage(full)} {ItemInfoPage.IsItemPage(barely)}", "True True");
            Check("the stat rows the page centres its figures on", $"{At(full, ItemInfoPage.StatNameRow, 3)}|{At(full, ItemInfoPage.ReqRow, 1)}|{At(full, ItemInfoPage.ModRow, 1)}", "Str|Req|Mod");
        }

        // ---- the spell on the Misc page (MAINMOD2.BAS:4813-4933) ----
        Console.WriteLine("\nspell info on the Misc page (MAINMOD2.BAS:4813-4933)");
        {
            var pSp = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
            var fb = pSp.First(s => s.Name.Trim().Equals("Firebolt", StringComparison.OrdinalIgnoreCase));
            var caster = Character.Create("s", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            caster.Guilds.Clear(); caster.Guilds.Add(new GuildMembership { Guild = "Sorcerer", Level = 9 });
            var pg = SpellInfo.Paint(fb, caster);
            string At(int r, int col) => pg.FirstOrDefault(x => x.Row == r && x.Col == col).Text ?? "(none)";
            Check("Damage is M5B33 / M5B41 + 1", $"{At(4, 1)} {At(4, 9)}", $"Damage: {fb.Damage2}/{fb.SpecialEffect + 1}");
            Check("a spell's page is not taken for an item's (its columns are drawn as the original's)", ItemInfoPage.IsItemPage(pg), false);
            Check("groups from M5B28, monsters from M5B1D", $"{At(14, 1)}|{At(15, 1)}", $"This spell will affect {fb.Damage1} groups,|{fb.AffectGroup} monsters max in each group.");
            Check("the guild teaching it, and the level it casts at", $"{At(5, 9)}|{At(18, 1)}", "Sorcerer|level of 5 by your Sorcerer training.");
            // a spell no guild of this Nomad reaches: guild 0, no points, level 0, and gv1D08 left as the Sorcerer set it
            var nomad = Character.Create("n", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var unreached = pSp.First(s => s.Id != 0 && Spellbook.Cost(s, nomad) == 0 && s.Name.Trim().Length > 0);
            var np = SpellInfo.Paint(unreached, nomad);
            Check("unreached: guild 0, 0 points, the stale training guild",
                  $"{np.First(x => x.Row == 5 && x.Col == 9).Text}|{np.First(x => x.Row == 6 && x.Col == 9).Text}|{np.First(x => x.Row == 18).Text}",
                  $"{GuildData.All[0].Name}|0|level of 0 by your Sorcerer training.");
            var withStats = pSp.First(s => s.Required.Take(6).Any(v => v > 0));
            int first = Enumerable.Range(0, 6).First(i => withStats.Required[i] > 0);
            Check("stats required, mixed-case three letters", SpellInfo.Paint(withStats, caster).First(x => x.Row == 5 && x.Col == 16).Text,
                  $"{RaceData.StatNamesLong[first][..3]} ({withStats.Required[first]})");
            var mass = pSp.FirstOrDefault(s => s.Effect >= SpellInfo.MassOffset);
            if (mass != null)
                Check("a mass spell says so, in italics", SpellInfo.Paint(mass, caster).First(x => x.Row == 12 && x.Col == 1).Italic, true);
        }

        // ---- a companion on the Misc page (MAINMOD2.BAS:4934-5039) ----
        Console.WriteLine("\ncompanion info on the Misc page (MAINMOD2.BAS:4934-5039)");
        {
            var cMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var breed = cMons.First(m => m.Id != 0 && m.SpecialAttackFlags != 0);
            var owner = Character.Create("o", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var pet = new Companion { MonsterId = breed.Id, Name = "Rex", Hits = 10, MaxHits = 10, BindingLevel = 6, IdLevel = 0, Alignment = ItemAlignment.Good };
            var pg = CompanionInfo.Paint(pet, 1, breed, owner, _ => null);
            string At(List<CharStatFile.Piece> page, int r, int col) => page.FirstOrDefault(x => x.Row == r && x.Col == col).Text ?? "(none)";
            Check("heading, bold", $"{At(pg, 2, 1)} {pg[0].Bold}", $"Comp. #2 \"Rex\" ({breed.Name.TrimEnd()}) True");
            Check("A/D is the breed's", At(pg, 7, 8), $"{breed.Att}/{breed.Def}");
            Check("stats joined with /", At(pg, 14, 1), string.Join("/", breed.Stats.Take(6)));
            Check("specials from row 14, column 9, italic", pg.First(x => x.Row == 14 && x.Col == 9).Italic, true);
            Check("bind level with its words", At(pg, 10, 1), "Bind Level:");
            pet.IdLevel = 2;
            var vague = CompanionInfo.Paint(pet, 1, breed, owner, _ => null);
            Check("id 2: Type but no Align, no Special", $"{At(vague, 5, 1)}|{At(vague, 6, 1)}|{At(vague, 13, 9)}|{At(vague, 17, 1)}", "Type:|(none)|(none)|Lots more to learn");
            var store = CompanionInfo.Paint(null, -1, breed, owner, _ => null, Alignment.Evil);
            Check("the pens: STORE:, fully known, no binding lines", $"{At(store, 2, 1)}|{At(store, 6, 8)}|{At(store, 10, 1)}|{At(store, 17, 1)}",
                  $"STORE: {breed.Name.TrimEnd()}|Evil|(none)|You know All");
        }

        // ---- chutes (MAZEMOD.BAS:98-139, 1839-1893) ----
        Console.WriteLine("\nchutes (MAZEMOD.BAS:98-139, 1839-1893)");
        {
            Character Mk(string n) => Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var me = Mk("Me"); me.MaxHits = me.CurHits = 500;
            var r1 = new Random(77); var r2 = new Random(77);
            string? said = Chute.Land(me, onScreen: true, onLeadersFloor: true, landedLevel: 3, r1);
            int want = Rules.ChuteDamage(r2, 3, (int)RaceData.ByName(me.Race).Size);
            Check("the one on screen: fallen into a chute for N hits", said, $"You have fallen into a chute for {want} hits.");
            Check("and the hits come off", me.CurHits, 500 - want);
            var other = Mk("Pal"); other.MaxHits = other.CurHits = 500;
            Check("another, on the leader's floor: named", Chute.Land(other, false, true, 3, new Random(5))?.StartsWith("\"Pal\" has fallen into a chute for "), true);
            Check("another, elsewhere: nothing said", Chute.Land(Mk("Far"), false, false, 3, new Random(5)) ?? "null", "null");
            var floater = Mk("Up"); floater.MaxHits = floater.CurHits = 500; floater.Levitating = true;
            Check("levitating: no damage and nothing said", $"{Chute.Land(floater, true, true, 3, new Random(5)) ?? "null"} {floater.CurHits}", "null 500");
            Check("only the levitating roll to float", Chute.Floats(me, new Random(1), 3), false);
            int up = 0;
            for (int s = 0; s < 1000; s++) if (Chute.Floats(floater, new Random(s), 8)) up++;
            CheckAtLeast("levitating on level 8 floats unless Rnd*100 <= 7 (so most of the time)", up, 880);
            Check("left elsewhere", Chute.Disappeared(other), "\"Pal\" has disappeared from view!");
            // l00B6 is an Integer: rounded, not cut - count the seeds where the two differ
            int differ = 0, rightAll = 0;
            for (int s = 0; s < 300; s++)
            {
                double raw = Math.Floor(new Random(s).NextDouble() * (7 * 10.5)) * Math.Log(2 + 2) + 5;
                int got = Rules.ChuteDamage(new Random(s), 7, 2);
                if ((int)raw != (int)Math.Round(raw, MidpointRounding.ToEven)) { differ++; if (got == (int)Math.Round(raw, MidpointRounding.ToEven)) rightAll++; }
            }
            Check("chute damage rounds where cutting would differ", $"{rightAll}/{differ}", $"{differ}/{differ}");
            CheckAtLeast("and such cases exist", differ, 1);
            var chFloors = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            int past = 0, pastFromAbove = 0;
            for (int lv = 0; lv < chFloors.Count; lv++)
                foreach (var ch in chFloors[lv].Chutes)
                    if (lv + 1 + ch.Depth > chFloors.Count) { past++; if (lv + 1 < chFloors.Count) pastFromAbove++; }
            FullCheck("no chute in the data drops past the bottom", past, 0);
            // the shareware's: the chutes that would drop past its last level are on that level, where
            // the limited build makes them do nothing (MAZEMOD.BAS:1840-1841, MAINMOD2.BAS:3997-3998)
            if (_shareware)
                Check("shareware: chutes that drop past its last level and are not on it (dead there in the limited build)", pastFromAbove, 0);

            // A levitating party splits on a chute and follows later (MAZEMOD.BAS:111-118, MULTIMOD.BAS:52-71,
            // MAINMOD2.BAS:3996-4009, FRMINTER.BAS:352-387): the one who fell is written down there with the
            // M59BB of the square they landed on, so the party taking the same chute finds them in its JOIN
            // list. Run on a shipped chute whose square lies in a different area on the two floors - the
            // case the port got wrong, keeping the area of the floor above.
            {
                int sl = -1; Floor.Chute sc = default;
                for (int lv = 0; lv < chFloors.Count && sl < 0; lv++)
                    foreach (var ch in chFloors[lv].Chutes)
                    {
                        int dl = lv + 1 + ch.Depth;
                        if (ch.Depth <= 0 || dl > chFloors.Count) continue;
                        if (chFloors[lv].AreaIdAt(ch.X - 1, ch.Y - 1) == chFloors[dl - 1].AreaIdAt(ch.X - 1, ch.Y - 1)) continue;
                        sl = lv + 1; sc = ch; break;
                    }
                CheckAtLeast("a shipped chute lands in another area id than its own square's", sl, 1);
                if (sl >= 1)
                {
                    int cx = sc.X - 1, cy = sc.Y - 1, cBelow = sl + sc.Depth;
                    int CArea(Character k) => k.AreaOr(q => q.DungeonLevel >= 1 && q.DungeonLevel <= chFloors.Count
                                                                 ? chFloors[q.DungeonLevel - 1].AreaIdAt(q.X, q.Y) : -1);
                    var cLead = Mk("Lead"); var cMid = Mk("Mid"); var cFell = Mk("Fell");
                    var cSplit = new Party();
                    foreach (var k in new[] { cLead, cMid, cFell })
                    { k.Levitating = true; k.DungeonLevel = sl; k.X = cx; k.Y = cy; k.Area = chFloors[sl - 1].AreaIdAt(cx, cy); cSplit.Members.Add(k); }
                    // the leader and Mid float, Fell does not: sub08F5 for Fell alone, then sub0908 drops them
                    Chute.MoveDown(cFell, cBelow, cx, cy, chFloors[cBelow - 1]);
                    cSplit.Remove(cFell);
                    Check("the one who fell: on the floor below, same square, that floor's area",
                          $"{cFell.DungeonLevel} {cFell.X} {cFell.Y} {cFell.Area}",
                          $"{cBelow} {cx} {cy} {chFloors[cBelow - 1].AreaIdAt(cx, cy)}");
                    Check("and is no longer in the party", cSplit.Members.Select(k => k.Name).Aggregate((a, b) => a + "," + b), "Lead,Mid");
                    var cAll = new[] { cLead, cMid, cFell };
                    Check("from the floor above, they are not in the JOIN list",
                          cSplit.HereToJoin(cAll, sl, chFloors[sl - 1].AreaIdAt(cx, cy), chFloors.Count, CArea).Count, 0);
                    // TAKE: every member goes down with no roll (MAINMOD2.BAS:4000-4002), onto the same square
                    foreach (var k in cSplit.Members) Chute.MoveDown(k, cBelow, cx, cy, chFloors[cBelow - 1]);
                    var cJoin = cSplit.HereToJoin(cAll, cBelow, chFloors[cBelow - 1].AreaIdAt(cx, cy), chFloors.Count, CArea);
                    Check("the party follows down the chute: the one who fell is there to join",
                          string.Join(",", cJoin.Select(k => k.Name)), "Fell");
                    // and the join itself goes through (fn1521 puts them on the leader's square, MAINMOD2.BAS:3507-3509)
                    Check("and may join", cSplit.WhyCannotJoin(cFell) ?? "joins", "joins");
                    // a record written by the old port, with the floor above's area, is what hid them
                    var cStale = Mk("Stale"); cStale.DungeonLevel = cBelow; cStale.X = cx; cStale.Y = cy; cStale.Area = chFloors[sl - 1].AreaIdAt(cx, cy);
                    Check("a record left with the floor above's area is not listed (the reported bug)",
                          cSplit.HereToJoin(new[] { cStale }, cBelow, chFloors[cBelow - 1].AreaIdAt(cx, cy), chFloors.Count, CArea).Count, 0);
                }
            }
            int pDiffer = 0, pRight = 0;
            for (int s = 0; s < 300; s++)
            {
                double raw = Math.Floor(new Random(s).NextDouble() * (((5 + 1) / 2.0) * 15.5)) * Math.Log(2 + 2) + 1;
                int rounded = (int)Math.Round(raw, MidpointRounding.ToEven);
                if ((int)raw != rounded) { pDiffer++; if (Rules.PitDamage(new Random(s), 5, 2) == rounded) pRight++; }
            }
            Check("pit damage rounds as well (MAZEMOD.BAS:1512, 1764)", $"{pRight}/{pDiffer}", $"{pDiffer}/{pDiffer}");
            // modUpdateCharacterList: a member at nought is marked dead before being dropped
            var pp = new Party(); var lead = Mk("Lead"); var hurt = Mk("Hurt"); lead.MaxHits = lead.CurHits = 10; hurt.CurHits = -4;
            pp.Members.Add(lead); pp.Members.Add(hurt);
            var marked = new List<string>();
            var told = pp.RemoveTheDead(null, c => { c.IsDead = true; marked.Add(c.Name); });
            Check("the party UPDATE: has died!, set dead, dropped", $"{string.Join("|", told)} {string.Join(",", marked)} {pp.Size} {hurt.CurHits}", "\"Hurt\" has died! Hurt 1 0");
        }

        // ---- the morgue's coordinates (FRMMORG.BAS:118-135) and VB's Val ----
        Console.WriteLine("\nthe morgue's coordinate loop and Val");
        {
            Check("15,5,3 is three", string.Join("|", Rescue.SplitCoords("15,5,3")), "15|5|3");
            Check("a trailing comma rides with the last piece", string.Join("|", Rescue.SplitCoords("15,5,3,")), "15|5|3");
            Check("1,2, is two", Rescue.SplitCoords("1,2,").Count, 2);
            Check("Val: spaces skipped, rounding, stop at a letter", $"{Rescue.Val("1 5")} {Rescue.Val("3.7")} {Rescue.Val("1-2")} {Rescue.Val("x")}", "15 4 1 0");
        }

        Section(() => GameArtChecks(dir));
        Section(() => HelpLinkChecks(dir));
        Section(() => EncounterChecks(dir));
        Section(() => DungeonFormChecks(dir));
        Section(() => ServiceFormChecks(dir));
        Section(() => AlignmentChoiceChecks(dir));
        Section(() => CurrencyChecks(dir));
        Section(() => CompanionDragChecks(dir));
        Section(() => CompanionRefChecks(dir));
        Section(() => CommandDropChecks(dir));
        Section(() => FontChecks(dir));
        Section(() => DungeonReviewChecks(dir));
        Section(() => ObjectsFormChecks(dir));
        Section(() => AccentChecks());
        Section(() => StatsPageChecks(dir));
        Section(() => LibrarySightingChecks(dir));
        Section(() => LibraryFindChecks(dir));
        Section(() => LibraryRefChecks(dir));
        Section(() => LibraryRestockChecks(dir));
        Section(() => ObjectsFocusChecks());
        Section(() => UiBehaviourChecks());
        Section(() => QueueChecks(dir));
        Section(() => SlotKnownChecks());
        Section(() => BankButtonChecks());
        Section(() => ReviewFixChecks());
        Section(() => TownBatchChecks(dir));
        Section(() => Town2Checks(dir));
        Section(() => Fid2Checks(dir));
        Section(() => PartyUpdateChecks());
        Section(() => WaveMixChecks(dir));
        Section(() => GameFilesChecks(dir));
        Section(() => ImportChecks(dir));

        Console.WriteLine(_failed == 0 ? "\nall checks passed" : $"\n{_failed} CHECK(S) FAILED");
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>A made-up monster for the generator's own tables.</summary>
    private static Monster Beast(string name, int type, int floor, int chance, int groups = 1, int inGroup = 2, int hits = 10,
                                 int guildLevel = 1, int compType = 0, int mode = 2, int align = 2, int subtype = 0) => new()
    {
        Name = name, Id = (short)(1000 + subtype), Type = (short)type, LevelFound = (short)floor, Chance = (short)chance,
        NumGroups = (short)groups, InGroup = (short)inGroup, Hits = (short)hits, GuildLevel = (short)guildLevel,
        CompanionType = (short)compType, CompanionSpawnMode = (short)mode, CompanionSubtype = 0,
        AlignmentValue = (short)align, Subtype = (short)subtype, GoldFactor = 2, BoxChance = new short[4],
    };

    /// <summary>A one-square floor whose square is in <paramref name="areaId"/>, every area admitting <paramref name="mask"/>.</summary>
    private static Floor OneSquare(int areaId, int mask)
    {
        var f = new Floor { Width = 1, Height = 1, LevelNum = 1 };
        f.Fields = new[] { new Floor.Field { AreaId = (short)areaId } };
        for (int i = 0; i < 30; i++) f.Areas.Add(new Floor.Area { SpawnMask = i == 0 ? 0 : mask });
        f.HeaderAreaCount = 29;
        return f;
    }

    /// <summary>
    /// fn0D4D (MONSMOD.BAS:1361-1989) and sub0A91's respawn time, against tables made up so that
    /// each rule shows by itself. The four faults asserted here are the original's, kept.
    /// </summary>
    private static void EncounterChecks(string dir)
    {
        Console.WriteLine("\nthe encounter generator (fn0D4D, MONSMOD.BAS:1361-1989)");

        // ---- the class pick's running total starts at v(0) and adds the class it has passed ----
        // three classes weighing 10, 80 and 10. Done right that is 10/80/10; the source's loop
        // (1542-1552) tests class k against 2*v(0) + v(1) + .. + v(k-1), so the 80 wins only the
        // slice 10..20 of Rnd * 101 and the last class takes nearly all of it.
        {
            var t = new List<Monster>
            {
                Beast("record 0", 0, 99, 0), Beast("A", 0, 5, 10, subtype: 1), Beast("B", 1, 5, 80, subtype: 2),
                Beast("C", 2, 5, 10, subtype: 3), Beast("end", 17, 99, 0, subtype: 4),
            };
            var f = OneSquare(20, 0b111);
            var by = new int[3]; int tries = 3000;
            for (int seed = 0; seed < tries; seed++)
            {
                var e = EncounterGenerator.Generate(t, 5, new Random(seed), f, 0, 0);
                if (e.Lead is >= 1 and <= 3) by[e.Lead - 1]++;
            }
            CheckNear("class weighing 80 drawn, percent (fault: ~10)", by[1] * 100.0 / tries, 5, 15);
            CheckNear("class weighing 10 after it, percent (fault: ~80)", by[2] * 100.0 / tries, 72, 88);
        }

        // ---- the newbie guard: level 1, area below 18, guild level over 2, not table index 4 ----
        {
            var t = new List<Monster>
            {
                Beast("record 0", 0, 99, 0), Beast("strong", 0, 1, 50, guildLevel: 5, hits: 1, subtype: 1),
                Beast("weak", 0, 1, 50, guildLevel: 1, hits: 1, subtype: 2), Beast("never", 0, 1, 0, guildLevel: 5, hits: 1, subtype: 3),
                Beast("index 4", 0, 1, 50, guildLevel: 30, hits: 1, subtype: 4), Beast("end", 17, 99, 0, subtype: 5),
            };
            int strongNewbie = 0, fourNewbie = 0, strongLater = 0;
            var hitsSeen = new SortedSet<int>();
            for (int seed = 0; seed < 2000; seed++)
            {
                var e = EncounterGenerator.Generate(t, 1, new Random(seed), OneSquare(5, 1), 0, 0);
                if (e.Lead == 1) strongNewbie++;
                if (e.Lead == 4) fourNewbie++;
                hitsSeen.Add(e.Foes[0].Hits);
                if (EncounterGenerator.Generate(t, 1, new Random(seed), OneSquare(18, 1), 0, 0).Lead == 1) strongLater++;
            }
            Check("newbie room (area 5): a guild-level-5 lead", strongNewbie, 0);
            CheckAtLeast("newbie room: table index 4 is let through", fourNewbie, 100);
            CheckAtLeast("area 18 on level 1: the guild-level-5 lead", strongLater, 100);
            // ---- one Hits per group, with no floor: a 1-hit monster comes out at 0 or 2 ----
            Check("a 1-hit monster's group hits (1869-1879)", string.Join(",", hitsSeen), "0,2");
        }

        // ---- the companion chain: each group is sought from the previous PICK's fields ----
        {
            var t = new List<Monster>
            {
                Beast("record 0", 0, 99, 0),
                Beast("L", 0, 3, 100, groups: 3, compType: 1, mode: 2, subtype: 1, align: 1),
                Beast("P", 1, 3, 100, compType: 2, mode: 2, subtype: 2, align: 1),
                Beast("Q", 2, 3, 100, compType: 0, mode: 2, subtype: 3, align: 1),
                Beast("end", 17, 99, 0, subtype: 4),
            };
            var who = Character.Create("c", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 18, 15 });
            int three = 0, chained = 0, singles = 0, singleOffers = 0, multiOffers = 0;
            for (int seed = 0; seed < 3000; seed++)
            {
                var e = EncounterGenerator.Generate(t, 3, new Random(seed), OneSquare(20, 1), 0, 0, who);
                int slots = e.Foes.Count(s => s.GroupCount > 0);
                if (slots == 3) { three++; if (e.Foes[1].Ref == 2 && e.Foes[2].Ref == 3) chained++; }
                if (slots == 1) { singles++; if (e.Count > 0 && e[0].JoinOffer > 0) singleOffers++; }
                else if (e.Count > 0 && e[0].JoinOffer > 0) multiOffers++;
            }
            CheckAtLeast("three-group encounters", three, 500);
            Check("of them, L then P then P's companion Q", chained, three);
            // the offer is wiped on every group's pass and only Foes(0)'s is read (1948-1967)
            CheckAtLeast("single-group encounters that offer to join", singleOffers, 1);
            Check("multi-group encounters that offer to join", multiOffers, 0);
        }

        // ---- the shipped data: one roll of hits per group, carried by its front member ----
        {
            var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
            int groups = 0, wrongFront = 0, outOfSwing = 0, engineMismatch = 0;
            var lone = Character.Create("h", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            for (int lv = 1; lv <= dungeon.Count; lv++)
            {
                var f = dungeon[lv - 1];
                for (int cell = 0; cell < f.Width * f.Height; cell += 7)
                {
                    int x = cell % f.Width, y = cell / f.Width;
                    if (f.AreaIdAt(x, y) <= 0) continue;
                    var e = EncounterGenerator.Generate(all, lv, new Random(cell * 31 + lv), f, x, y, lone);
                    for (int g = 0; g < 4; g++)
                    {
                        var s = e.Foes[g];
                        if (s.GroupCount <= 0) continue;
                        groups++;
                        var front = e.FirstOrDefault(m => m.Group == g);
                        if (front == null || front.Hits != s.Hits || front.MaxHits != s.MaxHits || e.Count(m => m.Group == g) != s.GroupCount) wrongFront++;
                        int h = all[s.Ref].Hits, most = (int)(h * .5 / 2) + 1;
                        if (Math.Abs(s.Hits - h) > most || s.Hits == h) outOfSwing++;
                    }
                    var fight = new CombatEngine(lone, e, _ => null, new Random(1));
                    if (fight.ChestClass != e.TreasureClass || fight.GoldEarned != CombatEngine.GoldAfterClass(e.Gold, e.TreasureClass)) engineMismatch++;
                }
            }
            FullCheckAtLeast("groups generated across the dungeon", groups, 1000);
            Check("groups whose front member is not the slot's roll", wrongFront, 0);
            Check("slot hits outside Hits +- (Int(Hits/4) + 1), or unchanged", outOfSwing, 0);
            Check("fights that re-roll the generator's chest or gold", engineMismatch, 0);

            // area id 0 is the only room gate (sub0A91, MONSMOD.BAS:703-708). The room you arrive
            // in on floor 1 is empty because its own spawn mask is 0 in MDATA11, not by a rule;
            // the first stairs-up room of floor 2, which the port's old rule also spared, is not.
            Check("floor 1's arrival room (8,10): its spawn mask", dungeon[0].Areas[dungeon[0].AreaIdAt(8, 10)].SpawnMask, 0);
            var f2 = dungeon[1];
            int held = 0;
            for (int k = 0; k < 20; k++)
                if (EncounterGenerator.InRoom(all, f2, 2, 6, 5, new DungeonState(), new DateTime(2026, 9, 21, 10, k, 0), new Random(k), lone) != null) held++;
            Check("floor 2's first stairs-up room (6,5) holding monsters, of 20", held, 20);
        }

        // ---- sub0A91's respawn time (MONSMOD.BAS:698-700) ----
        Check("respawn, floor 1, no room emptied (s)", DungeonState.RespawnDelay(1, 50, 50).TotalSeconds, 1350);
        Check("respawn, floor 1, ten rooms emptied (s)", DungeonState.RespawnDelay(1, 50, 40).TotalSeconds, 1300);
        Check("respawn, floor 15, none emptied (s)", DungeonState.RespawnDelay(15, 50, 50).TotalSeconds, 3450);
        Check("respawn never under 120 s", DungeonState.RespawnDelay(1, 400, 0).TotalSeconds, 120);
        {
            var s = new DungeonState();
            var at = new DateTime(2026, 9, 21, 10, 0, 0);
            s.MarkCleared(1, 3, at, 50);
            Check("a won fight takes one off the tally (779)", s.TallyOf(1, 50), 49);
            Check("still empty at exactly the respawn time (strict >)", s.IsOccupied(1, 3, 50, at.AddSeconds(1345)), false);
            Check("back a second after it", s.IsOccupied(1, 3, 50, at.AddSeconds(1346)), true);
            Check("the test alone leaves the tally be (711-716)", s.TallyOf(1, 50), 49);
            // "If Foes(0).Ref = -1 Then Exit Sub" (MONSMOD.BAS:769): emptied once is emptied
            double stamped = s.LoadRoom(1, 3).Foes[0].Stamp;
            s.MarkCleared(1, 3, at.AddMinutes(3), 50);
            Check("a second sub0C53 on an emptied room: tally", s.TallyOf(1, 50), 49);
            Check("a second sub0C53 on an emptied room: stamp", s.LoadRoom(1, 3).Foes[0].Stamp, stamped);
            // and the dead do not put back the offer sub0C53 cleared (777)
            var dead = new CombatMonster { Def = new Monster { Name = "x" }, Hits = -3, MaxHits = 5, Group = 0, JoinOffer = 2 };
            var after = EncounterGenerator.Capture(s.LoadRoom(1, 3), new[] { dead }, null, new List<Monster>());
            Check("an emptied room's offer to join stays cleared", after.Foes[0].JoinOffer, 0);
        }
        {
            // the tally goes back up only once fn0D4D has returned True for the refilled room
            var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var f2 = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"))[1];
            int area = f2.AreaIdAt(6, 5), header = f2.HeaderAreaCount;
            var s = new DungeonState();
            var at = new DateTime(2026, 9, 21, 10, 0, 0);
            s.MarkCleared(2, area, at, header);
            var back = EncounterGenerator.InRoom(all, f2, 2, 6, 5, s, at.AddHours(2), new Random(1), out bool wroteRefill);
            Check("a refilled room: generated", back != null && !back.Failed, true);
            Check("and the tally has its one back", s.TallyOf(2, header), header);

            // fn0D4D Puts the room as it makes it (sub0C79 at MONSMOD.BAS:1425, fn0F88 at 1979), so
            // the caller writes dungeon.json then; sub0A91 reading a room back writes nothing (687)
            Check("a refilled room: the level's file written (fn0F88, 1979)", wroteRefill, true);
            EncounterGenerator.InRoom(all, f2, 2, 6, 5, s, at.AddHours(2).AddMinutes(1), new Random(2), out bool wroteRevisit);
            Check("walked back into before it is due: nothing written", wroteRevisit, false);
            {
                // a fresh room with a chest, saved as the dungeon window saves it on arrival and read
                // back as a game killed straight after would find it (a temporary folder, never the saves)
                Encounter? withChest = null; DungeonState? sc = null;
                for (int seed = 0; seed < 500 && withChest == null; seed++)
                {
                    sc = new DungeonState();
                    var g = EncounterGenerator.InRoom(all, f2, 2, 6, 5, sc, at, new Random(seed), out bool w);
                    if (w && g != null && !g.Failed && g.TreasureClass > 0) withChest = g;
                }
                Check("a fresh room with a chest found", withChest != null, true);
                if (withChest != null && sc != null)
                {
                    string tmpArrive = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-selfcheck-arrive");
                    sc.Save(tmpArrive);
                    var onDisk = DungeonState.Load(tmpArrive).LoadRoom(2, area);
                    bool leftTemp = System.IO.File.Exists(System.IO.Path.Combine(tmpArrive, "dungeon.json.tmp"));
                    System.IO.Directory.Delete(tmpArrive, true);
                    Check("dungeon.json holds the room made on arrival, chest and all",
                          onDisk.TreasureClass == withChest.TreasureClass && onDisk.Gold == withChest.Gold
                          && onDisk.Foes[0].Ref == withChest.Foes[0].Ref && onDisk.Foes[0].GroupCount == withChest.Foes[0].GroupCount, true);
                    Check("saved beside and moved over: no temporary file left", leftTemp, false);
                }
            }

            // ---- the room keeps its records (fn0F88 / fn0E38): walk away from a wounded group ----
            {
                var st = new DungeonState();
                var who = Character.Create("w", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                var first = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at, new Random(7), who)!;
                var front = first[0];
                int wounded = Math.Max(1, front.Hits - 3);
                front.Hits = wounded;
                int count0 = first.Foes[0].GroupCount;
                if (count0 > 1) first[1].Hits = 0;                          // one behind it killed
                int stillThere = count0 > 1 ? count0 - 1 : count0;
                st.StoreRoom(2, area, EncounterGenerator.Capture(st.LoadRoom(2, area), first, null, all));
                int tally = st.TallyOf(2, header);
                var again = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at.AddMinutes(5), new Random(99), who)!;
                Check("come back: the same monster in slot 0", again.Foes[0].Ref, first.Foes[0].Ref);
                Check("come back: the front one's wounds kept", again.Foes[0].Hits, wounded);
                Check("come back: its rolled maximum kept", again.Foes[0].MaxHits, first.Foes[0].MaxHits);
                Check("come back: the group less the one killed", again.Foes[0].GroupCount, stillThere);
                Check("come back: what it is taken for kept", again.Foes[0].KnownAs, first.Foes[0].KnownAs);
                Check("come back: nothing generated (no fn0D4D lines)", again.Messages.Count == 0 && again.Lead == first.Lead, true);
                Check("come back: the tally untouched", st.TallyOf(2, header), tally);
                // the front member standing is the stored record itself
                Check("come back: the front member carries the slot's hits", again[0].Hits, wounded);
                double t = st.RespawnSeconds(2, header);
                Check("a group more than twice the respawn time old is replaced",
                      st.RefillDue(2, area, header, at.AddSeconds(2 * t + 1)) && !st.RefillDue(2, area, header, at.AddSeconds(2 * t)), true);

                // a chest left in an emptied room is still there (monShowChest p01AA, MONSMOD.BAS:728)
                var chestRoom = new RoomRecord { TreasureClass = 3, Lead = first.Lead, Gold = 100 };
                chestRoom.Foes[0].Ref = -1; chestRoom.Foes[0].Stamp = DungeonState.SecondsSinceMidnight(at);
                st.StoreRoom(2, area, chestRoom);
                var left = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at.AddMinutes(1), new Random(3), who);
                Check("an unopened chest in an emptied room, on return", left != null && left.Count == 0 && left.TreasureClass == 3, true);

                // and it all survives dungeon.json (a temporary folder, never the saves)
                string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-selfcheck-rooms");
                st.StoreRoom(2, area, EncounterGenerator.Capture(first.ToRecord(), first, null, all));
                st.Save(tmp);
                var read = DungeonState.Load(tmp).LoadRoom(2, area);
                System.IO.Directory.Delete(tmp, true);
                Check("dungeon.json keeps a room's records", read.Foes[0].Hits == wounded && read.Foes[0].Stamp == first.Foes[0].Stamp
                                                            && read.Gold == first.Gold && read.TreasureClass == first.TreasureClass, true);
            }

            // everyone joins and no group is left: sub0C53 all the same (MAINMOD2.BAS:4110-4113)
            var lone = Character.Create("j", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            int noChest = 0, paidRight = 0, chests = 0, heldInChest = 0, stillEmpty = 0, tallyDown = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var st = new DungeonState();
                var enc = EncounterGenerator.Generate(all, 2, new Random(seed), f2, 6, 5, lone);
                if (enc.Count == 0) continue;
                long owed = CombatEngine.GoldAfterClass(enc.Gold, enc.TreasureClass);
                var who = Character.Create("j", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                who.GoldOnHand = 0;
                st.StoreRoom(2, area, enc.ToRecord());
                enc.Clear();                                   // every member went off with you
                var fight = EncounterGenerator.ClearedByJoining(new Party(who), enc, st, 2, area, header, at, _ => null, new Random(1), all);
                if (!st.IsOccupied(2, area, header, at.AddSeconds(60))) stillEmpty++;
                if (st.TallyOf(2, header) == header - 1) tallyDown++;
                if (enc.TreasureClass == 0) { noChest++; if (who.GoldOnHand == owed) paidRight++; }
                else { chests++; if (who.GoldOnHand == 0 && fight.GoldEarned == owed && fight.ChestClass == enc.TreasureClass) heldInChest++; }
            }
            Check("all joined: the room reads emptied, of 200", stillEmpty, 200);
            Check("all joined: the level's tally loses one, of 200", tallyDown, 200);
            CheckAtLeast("all joined with no chest", noChest, 20);
            Check("and monGetGold pays the gold at once", paidRight, noChest);
            CheckAtLeast("all joined with a chest", chests, 5);
            Check("and the chest holds the gold instead", heldInChest, chests);
        }

        // ---- paralysis moves the stored Att/Def, and some of it never comes back ----
        // sub0D1D (MONSMOD.BAS:3884-3906): mode 0 takes the stat part off, mode 1 puts back the stat
        // part as it is then. Str/Int/Wis/Dex 18 is 2*6 + 4 + 4 + 2*6 = 32 of stat part.
        {
            Item? NoItem(short id) => null;
            Character Held() => Character.Create("held", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                                 new[] { 18, 18, 18, 18, 18, 18 });
            int Part(Character c) => c.StatsADNow(NoItem) + c.StoredADShift(NoItem);
            var a = Held();
            Check("stat part of Att/Def, Str/Int/Wis/Dex 18", Part(a), 32);
            a.Paralyse(3, NoItem);
            Check("held: the stat part is gone (3897-3898)", Part(a), 0);
            a.Unparalyse(NoItem);
            Check("cured: it is back, no shift left", $"{Part(a)},{a.ADShift}", "32,0");

            var b = Held();
            b.Paralyse(3, NoItem);
            b.RestInCity();   // FRMCTYM.BAS:115 zeroes Ailments(2) before sub048C could undo it
            Check("freed at the city gate: stats lost for good", $"{b.Paralysed},{Part(b)},{b.ADShift}", "False,0,-32");

            var z = Held();
            z.Paralyse(0, NoItem);   // a guild-level-0 monster: level 0 stored (3899), stats taken
            Check("level-0 paralysis: not held, stats gone", $"{z.Paralysed},{Part(z)}", "False,0");
            z.Paralyse(0, NoItem);
            Check("and a second one takes them again", Part(z), -32);

            var s = Held();
            s.Paralyse(3, NoItem);
            s.Stats[(int)Stat.Str] = 20;   // gear or a stat item while held: bracketed by mdrcharSetAD
            s.Unparalyse(NoItem);          // gives back 36, having taken 32
            Check("stats up while held: cure gives back more", $"{Part(s)},{s.ADShift}", "40,4");

            // a save from before ADShift, held: saved again before its A/D is ever read, reloaded
            // and cured, it must come out where it went in
            {
                string tmp = System.IO.Path.GetTempFileName();
                try
                {
                    var old = Held();
                    old.ParalysisLevel = 3;   // the old port kept only the level
                    old.Save(tmp);
                    var lines = System.IO.File.ReadAllLines(tmp).Where(l => !l.Contains("\"ADShift")).ToList();
                    System.IO.File.WriteAllText(tmp, string.Join("\n", lines));
                    var once = Character.Load(tmp);
                    once.Save(tmp);                       // written before anything reads its A/D
                    var twice = Character.Load(tmp);
                    Check("old held save, resaved: still without its stats", Part(twice), 0);
                    twice.RestInCity();                   // the gate clears the level, the loss stays
                    twice.Save(tmp);
                    var thrice = Character.Load(tmp);
                    Check("and after the city gate and another save", Part(thrice), 0);
                    var cured = Held();
                    cured.ParalysisLevel = 3;
                    cured.Save(tmp);
                    System.IO.File.WriteAllText(tmp, string.Join("\n",
                        System.IO.File.ReadAllLines(tmp).Where(l => !l.Contains("\"ADShift"))));
                    var c1 = Character.Load(tmp); c1.Save(tmp);
                    var c2 = Character.Load(tmp);
                    c2.Unparalyse(NoItem);
                    Check("old held save, resaved then cured: A/D as before", $"{Part(c2)},{c2.ADShift}", "32,0");
                }
                finally { try { System.IO.File.Delete(tmp); } catch { } }
            }

            // sub15CD on slot 0 (MAINMOD2.BAS:6096 lets it through): Items(0), the Hands
            {
                var all = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
                var hands = all.First(i => i.Id == Character.HandsItemId);
                var who = Held();
                string said = ItemUse.Problem(who, who.Pack[0]!, hands, id => all.FirstOrDefault(i => i.Id == id)) ?? "(none)";
                Console.WriteLine($"  Items(0) {hands.Name.Trim()}: SpecialType {hands.SpecialType}, Charges {hands.Charges} -> {said}");
                // Items(0).Charges is 0: "the Item doesn't appear to do much of anything." (MAINMOD2.BAS:6098-6100)
                Check("sub15CD on the Hands", said, "\"held\", the Item doesn't appear to do much of anything.");
            }

            // M59BB: -1 at creation (CHARCREA.BAS:242), nought for a death in rock below ground
            // whatever the square (MAINMOD2.BAS:4569-4574), -1 again at the city gate (FRMCTYM.BAS:112)
            var r = Held();
            Check("a new character's area", r.Area ?? -99, -1);
            r.DungeonLevel = 3; r.Area = 17;
            r.AreaForRockDeath();
            Check("dead in rock on level 3: area nought", r.Area ?? -99, 0);
            var carrier = Held(); carrier.DungeonLevel = 2; carrier.Area = 9;
            PendingRaise.Initiate(r, carrier, 0, 0);   // sub0A7C copies the mover's (MAINMOD2.BAS:5992)
            Check("a body put down takes the carrier's area", r.Area ?? -99, 9);
            r.RestInCity();
            Check("the city gate: -1", r.Area ?? -99, -1);
        }

        // ---- the loops with no cap end, for this data ----
        var (hangs, deadEnds, badCompanions) = EncounterLoopsProbe.Analyse(dir);
        Check("monsters whose companion class or direct index is out of range", badCompanions, 0);
        Check("like-types states that can never pick (L2003C)", hangs, 0);
        Check("restart states with no way through (L1EB02)", deadEnds, 0);
    }

    /// <summary>
    /// picTrap's two labels as frmDungeonMain's form data in MORDOR.EXE has them, and the port's
    /// chest panel against that. VB3 keeps each form as an RCDATA resource (frmDungeonMain is 22):
    /// per control a record whose properties are a byte id and a value - 03 BackColor, 04 ForeColor
    /// (4 bytes), 05 Left, Top, Width, Height (four Integers, twips), 0A Visible (1), 0C the font
    /// (name, Single size, a flags byte), 12 TabIndex (2), 13 (1), 14 Alignment (1: 0 left,
    /// 1 right, 2 centre) - ended by FF. The records are found by their captions.
    /// </summary>
    /// <summary>
    /// A lone character's death in a monster's round, the chest's self-attaching curse, and
    /// frmCharStat3's buttons - each against the original's own source or form data.
    /// </summary>
    private static void DungeonReviewChecks(string dir)
    {
        string root = PortTree.Root;
        // a missing file is a failure, not an empty text for "does not contain" to pass on
        string Port(string rel) => PortSource(rel);

        // ---- a lone character killed by a blow ----
        // sub0E85 takes the hits off, puts up lblMonsterHit, and then says "You were killed by a X."
        // (MONSMOD.BAS:3030-3040); sub0A64 then runs sub0930 (MULTIMOD.BAS:362-363). The engine
        // yields the caption with the hits already gone, so the window must draw it though the party
        // is wiped by then - it had stopped on PartyWiped and lost the caption and the line.
        Console.WriteLine("\na lone character killed by a monster's blow (MONSMOD.BAS:3030-3040)");
        {
            var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
            var brute = beasts.Where(m => m.Id != 0 && m.SpecialAttackFlags == 0 && m.LevelFound >= 10)
                              .OrderByDescending(m => m.LevelFound).First();
            bool died = false, captionAfterWipe = false, killedLine = false, lineAfterCaption = false;
            bool dead = false, putDown = false, unparalysed = false;
            for (int i = 0; i < 400 && !died; i++)
            {
                var c = Character.Create("probe", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                         new[] { 17, 18, 18, 17, 18, 18 });
                c.MaxHits = 1; c.CurHits = 1;
                c.CarryingCharacter = "Somebody";
                c.ParalysisLevel = 1;
                var fight = new CombatEngine(c, new[] { new CombatMonster { Def = brute, Hits = 1000 } },
                                             id => wares.FirstOrDefault(x => x.Id == id), new Random(9100 + i));
                fight.UseMonsterTable(beasts);
                bool sawCaption = false;
                foreach (var line in fight.MonsterAttacks())
                {
                    if (line.Channel == LineChannel.MonsterHit && line.Text.Contains(" hit for "))
                    {
                        sawCaption = true;
                        if (fight.PartyWiped) captionAfterWipe = true;
                    }
                    if (line.Channel == LineChannel.Message && line.Text.StartsWith("You were killed by a "))
                    {
                        killedLine = true;
                        lineAfterCaption = sawCaption;
                    }
                }
                if (c.CurHits > 0) continue;
                died = true;
                dead = c.IsDead;
                putDown = c.CarryingCharacter == "";
                unparalysed = !c.Paralysed;
            }
            Check("a lone character died of a blow", died, true);
            Check("the killing caption is yielded with the party already wiped", captionAfterWipe, true);
            Check("\"You were killed by a X.\" is said, after the caption", killedLine && lineAfterCaption, true);
            Check("the body is dead, the carried body put down, paralysis undone (sub0930, sub048C)",
                  dead && putDown && unparalysed, true);

            string win = Port(System.IO.Path.Combine("src/Mordor.App", "DungeonWindow.axaml.cs"));
            Check("PlayRound draws a line though the party is wiped", win.Contains("fight.PartyWiped) break"), false);
            Check("EndCombat's lone wiped path is sub0930 whole", win.Contains("if (_party.Size == 1) Sub0930(_ch);"), true);
        }

        // ---- the chest's self-attaching curse ----
        // "sub086D mc05A6 : sub09DE mc05A8" (MONSMOD.BAS:3857-3858), after the line and whatever
        // sub0F74 did. The numbers are SoundFiles indices, named in MODULE1.BAS.
        Console.WriteLine("\na chest item that attaches itself (MONSMOD.BAS:3850-3858)");
        {
            string named = DecompiledDir("decompiled-named");
            string mons = System.IO.File.ReadAllText(System.IO.Path.Combine(named, "MONSMOD.BAS"));
            string mod1 = System.IO.File.ReadAllText(System.IO.Path.Combine(named, "MODULE1.BAS"));
            int ConstIn(string src, string name)
            {
                var m = System.Text.RegularExpressions.Regex.Match(src, "Const " + name + @" = (\d+)");
                return m.Success ? int.Parse(m.Groups[1].Value) : -1;
            }
            string SoundNamed(int n)
            {
                var c = System.Text.RegularExpressions.Regex.Match(mod1, @"^Const (\w+) = " + n + " ", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (!c.Success) return "?";
                var f = System.Text.RegularExpressions.Regex.Match(mod1, @"SoundFiles\(" + c.Groups[1].Value + @"\) = ""([A-Z0-9-]+)\.WAV""");
                return f.Success ? f.Groups[1].Value : "?";
            }
            string body = Between(mons, "If  Items(l04FA).Cursed = gc1DD0 Then", "Exit Do");
            var calls = System.Text.RegularExpressions.Regex.Matches(body, @"(sub086D|sub09DE) (\w+)");
            var names = calls.Select(m => SoundNamed(ConstIn(mons, m.Groups[2].Value))).ToList();
            Check("the attach plays SoundFiles(61) then (63)", string.Join(",", names), "EXTING,YEECH");
            string win = Port(System.IO.Path.Combine("src/Mordor.App", "DungeonWindow.axaml.cs"));
            string port = Between(win, "private IEnumerable<string> TreasureFrom", "private List<string> OpenChest");
            Check("the port plays both, EXTING through sub086D", port.Contains("Audio.PlayOnNextChannel(\"EXTING\")") && port.Contains("Audio.Play(\"YEECH\")"), true);
            Check("and not BLACKOUT, which is SoundFiles(7)", port.Contains("Audio.Play(\"BLACKOUT\")") || port.Contains("Audio.PlayOnNextChannel(\"BLACKOUT\")"), false);
            Check("sub0F74's refusal is said, not swallowed", port.Contains("WearRefusal(slot, drop, Lookup) is string why) yield return why"), true);
        }

        // ---- a spell's sound: sub1005 ----
        // MUSIC.BAS:171-196 plays by SpellDefs.Kind and Level alone; the numbers resolve through
        // MAIN.TXT's Global Consts and MODULE1.BAS's SoundFiles table. The port had a school table
        // of its own (POISON, DRAIN, BETTER, TELEPORT...).
        Console.WriteLine("\na spell's sound (sub1005, MUSIC.BAS:171-196)");
        {
            string named = DecompiledDir("decompiled-named");
            string mod1 = System.IO.File.ReadAllText(System.IO.Path.Combine(named, "MODULE1.BAS"));
            string music = System.IO.File.ReadAllText(System.IO.Path.Combine(named, "MUSIC.BAS"));
            string main = System.IO.File.ReadAllText(System.IO.Path.Combine(named, "MAIN.TXT"));
            static System.Text.RegularExpressions.MatchCollection rx(string s, string p, System.Text.RegularExpressions.RegexOptions o = default) => System.Text.RegularExpressions.Regex.Matches(s, p, o);

            // SoundFiles(n): MODULE1's local constants (commented "' Const" in the listing), and
            // m0624, a Static nothing assigns, for 0
            var local = new Dictionary<string, int> { ["m0624"] = 0 };
            foreach (System.Text.RegularExpressions.Match m in rx(mod1, @"Const (m\w+) = (\d+)"))
                local[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            var table = new Dictionary<int, string>();
            foreach (System.Text.RegularExpressions.Match m in rx(mod1, @"SoundFiles\((\w+)\) = ""([A-Z0-9-]+)\.WAV"""))
                if (local.TryGetValue(m.Groups[1].Value, out int n)) table[n] = m.Groups[2].Value;
            Check("MODULE1 names all 72 sounds, 0 to 71", table.Count == 72 && table.Keys.Min() == 0 && table.Keys.Max() == 71, true);
            Check("SoundFiles.Names is MODULE1's table, number for number",
                  SoundFiles.Names.Length == 72 && Enumerable.Range(0, 72).All(i => table.GetValueOrDefault(i) == SoundFiles.Names[i]), true);

            // sub1005's body: its cases, and the sounds they name
            string sub = Between(music, "Sub sub1005 (", "End Sub");
            int Global(string gc)
            {
                var m = System.Text.RegularExpressions.Regex.Match(main, "Global Const " + gc + @" = (\d+)");
                return m.Success ? int.Parse(m.Groups[1].Value) : -1;
            }
            int Label(string l)
            {
                if (l == "m0148") return 0;   // Static m0148 As Integer, never assigned
                var m = System.Text.RegularExpressions.Regex.Match(sub, "Const " + l + @" = (\d+)");
                return m.Success ? int.Parse(m.Groups[1].Value) : -99;
            }
            var byKind = new Dictionary<int, int>();
            int otherwise = -1;
            string first = Between(sub, "Select Case SpellDefs(p013E).Kind", "End Select");
            foreach (System.Text.RegularExpressions.Match m in rx(first, @"Case (\w+)\s+sub09DE (\w+)"))
                if (m.Groups[1].Value == "Else") otherwise = Global(m.Groups[2].Value);
                else byKind[Label(m.Groups[1].Value)] = Global(m.Groups[2].Value);
            Check("sub1005's first Select: Kind -> sound",
                  string.Join(",", byKind.OrderBy(k => k.Key).Select(k => k.Key + "=" + SoundFiles.Name(k.Value))) + ",else=" + SoundFiles.Name(otherwise),
                  "0=FIRE,1=COLD,2=SHOCK,9=MAGIC2,else=MAGIC");
            var extra = System.Text.RegularExpressions.Regex.Match(sub,
                @"Case (\w+), (\w+), (\w+)\s+If  SpellDefs\(p013E\)\.Level >= (\d+) Then\s+sub09DE (\w+)");
            var extraKinds = extra.Success ? new[] { 1, 2, 3 }.Select(i => Label(extra.Groups[i].Value)).OrderBy(k => k).ToList() : new List<int>();
            int extraLevel = extra.Success ? int.Parse(extra.Groups[4].Value) : -1;
            int extraSound = extra.Success ? Global(extra.Groups[5].Value) : -1;
            Check("and the second: Kinds 0, 1, 2 at Level >= 7 add EXPLOS",
                  string.Join(",", extraKinds) + " >=" + extraLevel + " " + (extraSound >= 0 ? SoundFiles.Name(extraSound) : "?"), "0,1,2 >=7 EXPLOS");

            // the port's rule against the source's, every kind and level
            bool all = true;
            for (int kind = 0; kind < Spellbook.ClassCount; kind++)
                for (int level = 0; level <= 20; level++)
                {
                    var want = new List<int> { byKind.TryGetValue(kind, out int f) ? f : otherwise };
                    if (extraKinds.Contains(kind) && level >= extraLevel) want.Add(extraSound);
                    var got = SpellSounds.For(new Spell { Category = (short)kind, Level = (short)level });
                    if (!got.SequenceEqual(want.Select(SoundFiles.Name))) all = false;
                }
            Check("SpellSounds.For gives sub1005's sounds for every kind and level 0-20", all, true);
            Check("a level 7 Fire spell: FIRE then EXPLOS",
                  string.Join(",", SpellSounds.For(new Spell { Category = Spellbook.Fire, Level = 7 })), "FIRE,EXPLOS");
            Check("a level 6 Cold spell: COLD alone",
                  string.Join(",", SpellSounds.For(new Spell { Category = Spellbook.Cold, Level = 6 })), "COLD");
            Check("a Heal (kind 9) spell at level 9: MAGIC2 and no EXPLOS",
                  string.Join(",", SpellSounds.For(new Spell { Category = Spellbook.Healing, Level = 9 })), "MAGIC2");
            Check("a Poison or Movement spell: MAGIC",
                  string.Join(",", SpellSounds.For(new Spell { Category = Spellbook.Poison, Level = 8 })) + "|"
                  + string.Join(",", SpellSounds.For(new Spell { Category = Spellbook.Movement, Level = 3 })), "MAGIC|MAGIC");

            // no invented table left, and every caller the original has
            string audio = Port(System.IO.Path.Combine("src/Mordor.App", "Audio.cs"));
            string dwin = Port(System.IO.Path.Combine("src/Mordor.App", "DungeonWindow.axaml.cs"));
            string combat = Port(System.IO.Path.Combine("src/Mordor.Core", "Combat.cs"));
            string mattack = Port(System.IO.Path.Combine("src/Mordor.Core", "MonsterAttack.cs"));
            Check("the school table is gone", audio.Contains("SpellSound(") || audio.Contains("Spellbook.Poison =>")
                  || dwin.Contains("Audio.SpellSound"), false);
            int callers = 0;
            foreach (string f in System.IO.Directory.GetFiles(named, "*.BAS"))
                callers += rx(System.IO.File.ReadAllText(f), @"^\s+sub1005 ", System.Text.RegularExpressions.RegexOptions.Multiline).Count;
            Check("the original calls sub1005 six times (MAINMOD2 x5, MONSMOD x1)", callers, 6);
            Check("out of combat: Passwall's, and the one after the Select, for a spell and an item",
                  System.Text.RegularExpressions.Regex.Matches(dwin, @"Audio\.PlaySpell\(spell\)").Count, 3);
            // the three places a damage spell's captions are said all go through CaptionLines since
            // 95c5ebf, which gives each caption the spell's sounds
            Check("in combat: every damage caption carries the spell's sounds",
                  System.Text.RegularExpressions.Regex.Matches(combat, @"foreach \(var line in CaptionLines\(\w+\)\) yield return line;").Count == 3
                  && Between(combat, "private IEnumerable<CombatLine> CaptionLines(", "if (caps.Count > 0)").Contains("CastSounds: LastCaptionSounds"), true);
            // the non-combat branch's sub1005 (MAINMOD2.BAS:1397-1403) is not reached in an engaged
            // fight: mdrcharCheckCast refuses a non-combat spell there first (512-519, 831). Out of
            // a fight it is the dungeon's Audio.PlaySpell, counted above.
            Check("and no non-combat cast in an engaged fight to sound: it is refused (MAINMOD2.BAS:512-519)",
                  combat.Contains("private string? NonCombatInFight(")
                  && !combat.Contains("LastSaidSounds = SpellSounds.For(s);"), true);
            Check("fn0FF5's sound is SpellDefs(l00CC), the pool index (MONSMOD.BAS:245)",
                  mons0FF5(named).Contains("sub1005 l00CC") && mattack.Contains("SpellSounds.For(_spells[l00CC])"), true);
            Check("the pick sounds come before the No Party Sound test",
                  Between(Port(System.IO.Path.Combine("src/Mordor.Core", "CombatSounds.cs")), "public static IEnumerable<string> Of(",
                          "if (noPartySound").Contains("line.PickSounds"), true);
            static string mons0FF5(string dir) =>
                Between(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "MONSMOD.BAS")), "Function fn0FF5 (", "End Function");
        }

        // ---- a monsters' turn, heard: sub0E85's sounds on both of the window's paths ----
        // The timed round (PlayRound over RoundPaced) had skipped every line on the log channel
        // before its sounds, so a companion's death cry, a monster spell, DRAIN, STONE, STEAL and
        // BYE were silent there and heard only on the engage/move tick (MonstersRound). Both now
        // play every line through CombatSounds.Of; these rounds go through CombatSounds.OfLines,
        // which is that rule line by line.
        Console.WriteLine("\na monsters' turn, heard (sub0E85, MONSMOD.BAS:2593-3417)");
        {
            Monster Mon(short id, MonsterKind kind, int flags = 0, int spellFlags = 0, short found = 1) => new Monster
            {
                Name = $"snd{id}", Id = id, Type = (short)kind, Att = 400, Def = 0, Hits = 900, GuildLevel = 5,
                LevelFound = found, SpecialAttackFlags = flags, SpellFlags = spellFlags, DamageMod = 1,
                Stats = new short[] { 30, 10, 10, 10, 10, 10, 10 },
            };
            Character Member(string n)
            {
                var m = Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                m.MaxHits = 90000; m.CurHits = 90000;
                return m;
            }
            CombatEngine Fight(Party p, Monster foe, int count, int seed, Monster? table = null, IReadOnlyList<Spell>? spells = null)
            {
                var f = new CombatEngine(p, Enumerable.Range(0, count).Select(_ => new CombatMonster { Def = foe, Hits = 900 }).ToList(),
                                         _ => null, new Random(seed));
                f.UseCompanionDefs(c => table != null && table.Id == c.MonsterId ? table : null);
                if (spells != null) f.UseSpellTable(spells);
                return f;
            }
            string Heard(IEnumerable<CombatEngine.CombatLine> lines, Character watched, bool nps) =>
                string.Join(",", CombatSounds.OfLines(lines, watched, nps));
            var paths = new (string name, Func<CombatEngine, IEnumerable<CombatEngine.CombatLine>> run)[]
            {
                ("the timed round", f => f.RoundPaced(new List<Spell>()).ToList()),
                ("MonstersRound", f => f.MonsterAttacks(AttackKind.Round).ToList()),
            };

            foreach (var (path, run) in paths)
            {
                // a melee hit on the one on screen: sub0D35's attack cry then sub0E99's hurt
                // (MONSMOD.BAS:3009-3027) - GOBLIN, OUCH for a humanoid; an animal adds RIP
                foreach (var (kind, want) in new[] { (MonsterKind.Humanoid, "GOBLIN,OUCH"), (MonsterKind.Animal, "DOG,RIP,OUCH") })
                {
                    int hits = 0, right = 0;
                    for (int seed = 1; seed <= 30; seed++)
                    {
                        var p = new Party(); p.Members.Add(Member("solo"));
                        var lines = run(Fight(p, Mon(9001, kind), 1, seed)).ToList();
                        if (!lines.Any(l => l.Text.Contains(" hit for "))) continue;
                        hits++;
                        if (Heard(lines, p.Members[0], false) == want) right++;
                    }
                    Check($"{path}: a {kind}'s blow on you plays {want}", hits > 0 && right == hits, true);
                }

                // a companion killed by the blows: sub0CB7 for its kind (MONSMOD.BAS:2966-2968),
                // once, on a line with no caption
                {
                    var undead = Mon(9002, MonsterKind.Undead);
                    int deaths = 0, right = 0;
                    for (int seed = 1; seed <= 40; seed++)
                    {
                        var p = new Party(); var me = Member("owner"); p.Members.Add(me);
                        me.Companions.Add(new Companion { MonsterId = undead.Id, Hits = 1, MaxHits = 1 });
                        var comp = me.Companions[0]!;
                        var lines = run(Fight(p, Mon(9003, MonsterKind.Humanoid), 5, seed, undead)).ToList();
                        if (comp.Hits > 0 && me.Companions[0] != null) continue;
                        deaths++;
                        var sounds = CombatSounds.OfLines(lines, me, false);
                        if (sounds.Count(s => s == "DI-UNDEA") == 1
                            && lines.Any(l => l.Text == "" && l.HitSounds != null && l.HitSounds.Contains("DI-UNDEA"))) right++;
                    }
                    Check($"{path}: a companion's death plays DI-UNDEA once", deaths > 0 && right == deaths, true);
                }

                // No Party Sound, the second of two on screen: a blow on the first is silent, a blow
                // on you is heard (the M5F48 test at 3009 and 3026)
                {
                    int silent = 0, silentOk = 0, mine = 0, mineOk = 0;
                    for (int seed = 1; seed <= 40; seed++)
                    {
                        var p = new Party(); p.Members.Add(Member("front")); p.Members.Add(Member("back"));
                        p.Select(1);
                        var lines = run(Fight(p, Mon(9004, MonsterKind.Humanoid), 1, seed)).ToList();
                        bool frontHit = p.Members[0].CurHits < 90000, backHit = p.Members[1].CurHits < 90000;
                        string heard = Heard(lines, p.Members[1], true);
                        if (frontHit && !backHit) { silent++; if (heard == "") silentOk++; }
                        if (backHit && !frontHit) { mine++; if (heard == "GOBLIN,OUCH") mineOk++; }
                    }
                    Check($"{path}: No Party Sound - another's blow is silent", silent > 0 && silentOk == silent, true);
                    Check($"{path}: No Party Sound - your own is heard", mine > 0 && mineOk == mine, true);
                }

                // a monster spell at the other member, No Party Sound on: fn0FF5's sub1005 and
                // sub0E99 have no test (MONSMOD.BAS:245, 2850-2852), sub0D35 has (2847-2849)
                {
                    var spells = new List<Spell> { new Spell { Name = "probe bolt", Category = 0, Level = 1, Damage2 = 6, SpecialEffect = 1 } };
                    int casts = 0, npsOk = 0, allOk = 0;
                    for (int seed = 1; seed <= 200; seed++)
                    {
                        var p = new Party(); p.Members.Add(Member("front")); p.Members.Add(Member("back"));
                        p.Select(1);
                        var lines = run(Fight(p, Mon(9005, MonsterKind.Mage, spellFlags: 1), 1, seed, null, spells)).ToList();
                        if (!lines.Any(l => l.PickSounds != null) || p.Members[0].CurHits == 90000 || p.Members[1].CurHits != 90000) continue;
                        casts++;
                        if (Heard(lines, p.Members[1], true) == "FIRE,OUCH") npsOk++;
                        if (Heard(lines, p.Members[1], false) == "FIRE,MAGE,OUCH,MAGE,OUCH") allOk++;
                    }
                    Check($"{path}: another's spell hit, No Party Sound: FIRE,OUCH", casts > 0 && npsOk == casts, true);
                    Check($"{path}: and without it: FIRE,MAGE,OUCH,MAGE,OUCH", casts > 0 && allOk == casts, true);
                }

                // a drain on the member not on screen: DRAIN on a line with no caption (3183-3185)
                {
                    int drained = 0, heard = 0, hushed = 0;
                    for (int seed = 1; seed <= 60; seed++)
                    {
                        var p = new Party(); p.Members.Add(Member("front")); p.Members.Add(Member("back"));
                        p.Select(1);
                        var lines = run(Fight(p, Mon(9006, MonsterKind.Undead, flags: 1 << 7, found: 60), 1, seed)).ToList();
                        if (!lines.Any(l => l.Text == "\"front\" has been drained!")) continue;
                        drained++;
                        if (CombatSounds.OfLines(lines, p.Members[1], false).Contains("DRAIN")) heard++;
                        if (!CombatSounds.OfLines(lines, p.Members[1], true).Contains("DRAIN")) hushed++;
                    }
                    Check($"{path}: another's drain plays DRAIN", drained > 0 && heard == drained, true);
                    Check($"{path}: and No Party Sound hushes it", drained > 0 && hushed == drained, true);
                }
            }

            // the window: every line of the timed round, the log-only ones included, goes through
            // LineSounds, and LineSounds is CombatSounds.Of
            string dwin = Port(System.IO.Path.Combine("src/Mordor.App", "DungeonWindow.axaml.cs"));
            string logBranch = Between(Between(dwin, "private async void PlayRound()", "private void LineSounds("),
                                       "if (line.Channel == LineChannel.Log)", "continue;");
            Check("PlayRound's log-only lines play their sounds", logBranch.Contains("LineSounds(line);"), true);
            Check("LineSounds is CombatSounds.Of",
                  Between(dwin, "private void LineSounds(", "private void MonstersRound(").Contains("CombatSounds.Of(line, _ch, _options.NoPartyFX"), true);
            Check("no STONE after the round (the blow's own, MONSMOD.BAS:3213)",
                  Between(dwin, "private void AfterRoundSounds()", "private void StartCombat(").Contains("\"STONE\""), false);
        }

        // ---- frmCharStat3's buttons ----
        // cmdCommand(0..3) are Equip, Use, Drop and Info in the form data (MORDOR.EXE), each with the
        // same properties - none hidden - and cmdCommand_Click asks DungeonLevel only for Use
        // (FRMCHAR3.BAS:222). So the city's Objects & Spells has a Drop too.
        Console.WriteLine("\nObjects & Spells' buttons (frmCharStat3 in MORDOR.EXE)");
        {
            string exe = System.IO.Path.Combine(dir, "MORDOR.EXE");
            byte[] b = System.IO.File.Exists(exe) ? System.IO.File.ReadAllBytes(exe) : Array.Empty<byte>();
            bool Button(string caption, int index)
            {
                var pat = new List<byte> { (byte)caption.Length };
                pat.AddRange(System.Text.Encoding.ASCII.GetBytes(caption));
                pat.AddRange(new byte[] { 0x02, (byte)index, 0x00 });
                int at = b.AsSpan().IndexOf(pat.ToArray());
                if (at < 0) return false;
                int p = at + pat.Count - 3;
                // Index (02), a Long (03), the rectangle (04), then the font (0B): Use's layout, with
                // no Visible = False among them
                return b[p] == 0x02 && b[p + 3] == 0x03 && b[p + 8] == 0x04 && b[p + 17] == 0x0B;
            }
            Check("cmdCommand(0) Equip", Button("Equip", 0), true);
            Check("cmdCommand(1) Use", Button("Use", 1), true);
            Check("cmdCommand(2) Drop, shown as the others are", Button("Drop", 2), true);
            Check("cmdCommand(3) Info", Button("Info", 3), true);
            string city = Port(System.IO.Path.Combine("src/Mordor.App", "CityWindow.axaml"));
            Check("the city's Objects & Spells has Drop", city.Contains("Focusable=\"False\" Content=\"Drop\" Click=\"OnPackDrop\""), true);
            string cityMdi = Port(System.IO.Path.Combine("src/Mordor.App", "CityWindow.Mdi.cs"));
            string equip = Between(cityMdi, "private void OnPackEquip", "EquipSelected();");
            Check("the city's Equip does nothing while paralysed (FRMCHAR3.BAS:203)", equip.Contains("if (_ch.Paralysed) return;"), true);
        }
    }

    private static void DungeonFormChecks(string dir)
    {
        Console.WriteLine("\nthe chest panel's labels (frmDungeonMain in MORDOR.EXE)");
        string exe = System.IO.Path.Combine(dir, "MORDOR.EXE");
        if (!System.IO.File.Exists(exe)) { Console.WriteLine("  FAIL  MORDOR.EXE missing"); _failed++; return; }
        byte[] b = System.IO.File.ReadAllBytes(exe);
        int U16(int p) => b[p] | (b[p + 1] << 8);

        // the RCDATA resource numbered 22
        int ne = U16(0x3C), table = ne + U16(ne + 0x24), shift = U16(table), p = table + 2;
        int formAt = -1, formLen = 0;
        while (U16(p) != 0)
        {
            int type = U16(p), count = U16(p + 2);
            p += 8;
            for (int i = 0; i < count; i++, p += 12)
                if (type == 0x800A && U16(p + 6) == 0x8016) { formAt = U16(p) << shift; formLen = U16(p + 2) << shift; }
        }
        if (formAt < 0) { Console.WriteLine("  FAIL  RCDATA 22 not found"); _failed++; return; }
        var form = b.AsSpan(formAt, formLen).ToArray();

        // the properties after a caption, up to the record's FF
        Dictionary<int, object>? PropsAfter(byte[] caption)
        {
            int at = form.AsSpan().IndexOf(caption);
            if (at < 0) return null;
            var props = new Dictionary<int, object>();
            int q = at + caption.Length;
            while (q < form.Length && form[q] != 0xFF)
            {
                int id = form[q++];
                switch (id)
                {
                    case 0x03: case 0x04: props[id] = BitConverter.ToInt32(form, q); q += 4; break;
                    case 0x05: props[id] = $"{(short)U16F(q)},{(short)U16F(q + 2)} {U16F(q + 4)}x{U16F(q + 6)}"; q += 8; break;
                    case 0x0A: case 0x13: case 0x14: props[id] = (int)form[q]; q += 1; break;
                    case 0x12: props[id] = U16F(q); q += 2; break;
                    case 0x0C:
                        int n = form[q];
                        string name = System.Text.Encoding.ASCII.GetString(form, q + 1, n);
                        float size = BitConverter.ToSingle(form, q + 1 + n);
                        props[id] = $"{name} {size.ToString(System.Globalization.CultureInfo.InvariantCulture)} bold={(form[q + 5 + n] & 1) != 0}";
                        q += 1 + n + 4 + 1;
                        break;
                    default: return props;   // an id this reader does not know: stop, what came before stands
                }
            }
            return props;
        }
        int U16F(int at) => form[at] | (form[at + 1] << 8);

        var trap = PropsAfter(System.Text.Encoding.ASCII.GetBytes("\x10Magically Locked"));
        var disarm = PropsAfter(new byte[] { 0x07, 0x2B, 0x2B, 0x2B, 0x20, 0x7F, 0x7F, 0x7F });
        if (trap == null || disarm == null) { Console.WriteLine("  FAIL  lblTrap or lblDisarm not found"); _failed++; return; }
        Check("lblTrap Left,Top WidthxHeight", trap[0x05], "-195,0 1950x210");
        Check("lblTrap Alignment (1 = Right Justify)", trap.GetValueOrDefault(0x14, 0), 1);
        Check("lblTrap has no font of its own", trap.ContainsKey(0x0C), false);
        Check("lblDisarm Left,Top WidthxHeight", disarm[0x05], "120,195 1650x210");
        Check("lblDisarm Alignment (1 = Right Justify)", disarm.GetValueOrDefault(0x14, 0), 1);
        Check("lblDisarm font", disarm.GetValueOrDefault(0x0C, "none"), "Courier New 7.5 bold=True");

        // and the port's panel, read out of its own axaml beside this build
        string axaml = PortTree.At("src/Mordor.App", "DungeonWindow.axaml");
        if (!System.IO.File.Exists(axaml)) { Console.WriteLine($"  FAIL  {axaml} missing"); _failed++; return; }
        string src = System.IO.File.ReadAllText(axaml);
        string Element(string name)
        {
            int a = src.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
            int b2 = a < 0 ? -1 : src.IndexOf("/>", a, StringComparison.Ordinal);
            if (b2 < 0)
            {
                // an empty text would pass the "not in Courier New" check below on nothing
                Console.WriteLine($"  FAIL  no self-closed element x:Name=\"{name}\" in {axaml}");
                _failed++;
                return "";
            }
            return src.Substring(a, b2 - a);
        }
        string chestText = Element("ChestText"), chestGauge = Element("ChestGauge");
        Check("the port's lblTrap is right-justified", chestText.Contains("TextAlignment=\"Right\""), true);
        Check("and in the form's font, not Courier New", chestText.Contains("Courier"), false);
        Check("the port's lblDisarm is right-justified", chestGauge.Contains("TextAlignment=\"Right\""), true);
        // Courier New 7.5 bold, through the Options fonts: 7.5 points, 10 pixels, only while nothing
        // resizes the form; sub0760 then makes it ProSize (UiFonts, DesignFonts.Disarm)
        Check("in its own font, Courier New 7.5, through the Options fonts",
              chestGauge.Contains("app:UiFonts.Font=\"frmDungeonMain.lblDisarm\"") && !chestGauge.Contains("FontSize="), true);
        Check("picTrap clips what runs past it",
              src.Contains("<Canvas Width=\"119\" Height=\"26\" ClipToBounds=\"True\">"), true);
        // FRMCHAR3 has no message label (lblItem, lblItemStatus, lblGuild, lblName, lblLevel,
        // lblPoints, lblCompName, lblCharacterHit, lblCall): the port's SheetMsg is gone
        Check("Objects & Spells has no message line of the port's", src.Contains("x:Name=\"SheetMsg\""), false);
    }

    /// <summary>
    /// Objects &amp; Spells against frmCharStat3's form data in MORDOR.EXE (RCDATA 10), and the
    /// character window's tab strip against frmCharStat1's (RCDATA 8). Each rectangle is read out
    /// of the exe and the port's layout is held to it, twips over 15: the Items page's list,
    /// scroll bar and buttons, the Spells page, the pages' places in the tab control, the tab
    /// control itself and the form's client.
    /// </summary>
    /// <summary>
    /// One accent for every window: the Fluent theme's palettes pinned to the port's colour, so a ticked
    /// box or a selected row does not take the Windows accent, which Windows may change with the
    /// wallpaper (the Mods page had been gold from the title screen and blue-grey from the Control Panel).
    /// </summary>
    private static void AccentChecks()
    {
        Console.WriteLine("\none accent colour, the port's own, in every window");
        string app = PortSource("src/Mordor.App", "App.axaml");
        Check("the dark and light palettes both pin the accent",
              System.Text.RegularExpressions.Regex.Matches(app, "<ColorPaletteResources x:Key=\"(Dark|Light)\" Accent=\"#2f5597\"/>").Count, 2);
    }

    private static void ObjectsFormChecks(string dir)
    {
        Console.WriteLine("\nObjects & Spells is frmCharStat3's form data (MORDOR.EXE RCDATA 10)");
        // the Spells list stops on whole rows, as vsbSpells does (FRMCHAR3.BAS:881-912). Its viewport
        // is 69 pixels under a 2-pixel top margin, its rows 16 with the default fonts
        Check("whole rows: the room under the last row makes the furthest scroll a whole row",
              string.Join("|", WholeRows.Tail(69, 2, 16), WholeRows.Tail(64, 0, 16), WholeRows.Tail(69, 2, 21), WholeRows.Tail(0, 2, 16)), "3|0|4|0");
        {
            // 12 spells, 16 each, from 2 down, with the tail: scrolled right down it is 8 rows on
            double extent = 2 + 12 * 16 + WholeRows.Tail(69, 2, 16), max = extent - 69;
            // Teleport, the last, brought into view at the bottom: offset 2 + 12*16 - 69 = 125, and
            // with the tail the list's own end is 128 = 8 rows, which shows the last four whole
            Check("whole rows: the furthest scroll is a whole row", max, 128.0);
            Check("whole rows: a row brought in at the bottom shows whole, and the one above it too",
                  string.Join("|", WholeRows.Snap(2 + 12 * 16 - 69, 16, max), WholeRows.Snap(2 + 9 * 16 - 69, 16, max)), "128|80");
            Check("whole rows: a row brought in at the top is that row, not the margin past it",
                  string.Join("|", WholeRows.Snap(2 + 5 * 16, 16, max), WholeRows.Snap(0, 16, max), WholeRows.Snap(500, 16, max)), "80|0|128");
            string objectsSource = PortSource("src/Mordor.App", "ObjectsForm.cs");
            Check("whole rows: the Spells lists, in town and in the dungeon, snap; the pack list is as it was",
                  PortSource("src/Mordor.App", "CityWindow.Mdi.cs").Contains("ObjectsForm.LinkScrollBar(SpellList, SpellScroll, ObjectsForm.SpellRowPitch, wholeRows: true);")
                  && PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs").Contains("ObjectsForm.LinkScrollBar(SheetSpellBox, SheetSpellScroll, ObjectsForm.SpellRowPitch, wholeRows: true);")
                  && PortSource("src/Mordor.App", "CityWindow.Mdi.cs").Contains("ObjectsForm.LinkScrollBar(PackList, PackScroll, ObjectsForm.PackRowPitch);")
                  && objectsSource.Contains("double tail = WholeRows.Tail(viewer.Viewport.Height, rows.Margin.Top, rowHeight);")
                  && objectsSource.Contains("double whole = WholeRows.Snap(at, rowHeight, max);"), true);
        }
        string exe = System.IO.Path.Combine(dir, "MORDOR.EXE");
        byte[] b = System.IO.File.Exists(exe) ? System.IO.File.ReadAllBytes(exe) : Array.Empty<byte>();
        int U16(int p) => b[p] | (b[p + 1] << 8);
        byte[] Resource(int id)
        {
            if (b.Length < 0x40) return Array.Empty<byte>();
            int ne = U16(0x3C), table = ne + U16(ne + 0x24), shift = U16(table), p = table + 2;
            byte[] found = Array.Empty<byte>();
            while (U16(p) != 0)
            {
                int type = U16(p), count = U16(p + 2);
                p += 8;
                for (int i = 0; i < count; i++, p += 12)
                    if (type == 0x800A && U16(p + 6) == (0x8000 | id)) found = b.AsSpan(U16(p) << shift, U16(p + 2) << shift).ToArray();
            }
            return found;
        }
        byte[] form = Resource(10), stat1 = Resource(8);
        if (form.Length == 0 || stat1.Length == 0) { Console.WriteLine("  FAIL  RCDATA 10 or 8 not found"); _failed++; return; }
        byte[] Ascii(string s) => System.Text.Encoding.Latin1.GetBytes(s);
        byte[] Counted(string s) => new[] { (byte)s.Length }.Concat(Ascii(s)).ToArray();
        // four Integers: Left, Top, Width, Height in twips
        int[]? Four(byte[] f, int k) => k >= 0 && k + 8 <= f.Length
            ? new int[] { (short)(f[k] | (f[k + 1] << 8)), (short)(f[k + 2] | (f[k + 3] << 8)), (short)(f[k + 4] | (f[k + 5] << 8)), (short)(f[k + 6] | (f[k + 7] << 8)) }
            : null;
        string Show(int[]? r) => r == null ? "missing" : $"{r[0]},{r[1]} {r[2]}x{r[3]}";
        int After(byte[] f, byte[] pat, int from = 0)
        {
            int at = from < f.Length ? f.AsSpan(from).IndexOf(pat) : -1;
            return at < 0 ? -1 : from + at + pat.Length;
        }
        // a captioned control: its caption, its Index (02), then on a Label any BackColor (03) and
        // ForeColor (04) and the rectangle (05); on a CommandButton a Long (03) and the rectangle (04)
        int[]? Captioned(string caption, int index, byte rectId)
        {
            var pat = Counted(caption).ToList();
            if (index >= 0) pat.AddRange(new byte[] { 0x02, (byte)index, 0x00 });
            int k = After(form, pat.ToArray());
            if (k < 0) return null;
            while (k < form.Length && (form[k] == 0x03 || (rectId == 0x05 && form[k] == 0x04))) k += 5;
            return k < form.Length && form[k] == rectId ? Four(form, k + 1) : null;
        }
        // the Elastics in the order the form holds them; each is its Align (03, an Integer) and
        // BackColor (01, a Long) where it has them, then the rectangle (04)
        var elastics = new List<int[]?>();
        for (int k = After(form, Ascii("VideoSoftElastic\xff")); k >= 0; k = After(form, Ascii("VideoSoftElastic\xff"), k))
        {
            int q = k;
            if (form[q] == 0x03) q += 3;
            if (form[q] == 0x01) q += 5;
            elastics.Add(form[q] == 0x04 ? Four(form, q + 1) : null);
        }
        int[]? Elastic(int n) => n < elastics.Count ? elastics[n] : null;

        // the form's client: its caption, BackColor (03), then 05 with four Longs
        int client = After(form, Counted("Objects & Spells"));
        string clientSize = client >= 0 && form[client] == 0x03 && form[client + 5] == 0x05
            ? $"{BitConverter.ToInt32(form, client + 14)}x{BitConverter.ToInt32(form, client + 18)}" : "missing";
        Check("the form's client", clientSize, "4065x3105");
        int tab = After(form, Ascii("VideoSoftIndexTab\xff\x02"));
        var tabRect = Four(form, tab);
        Check("tabItemTab, a VideoSoft IndexTab", Show(tabRect), "30,30 4005x3060");
        Check("... its tabs", tab >= 0 && After(form, Counted("Items|Spells|Companions"), tab) == tab + 8 + 1 + 1 + 23, true);
        int perRow = After(form, new byte[] { 0x2A, 0x00, 0x2C }, Math.Max(tab, 0));
        Check("... three to the row (property 2C)", perRow >= 0 ? (int)form[perRow] : -1, 3);
        var items = Elastic(4); var spells = Elastic(0); var comps = Elastic(2);
        Check("elaTabFrame(0), the Items page", Show(items), "75,360 3810x2550");
        Check("elaTabFrame(1), the Spells page", Show(spells), "75,375 3810x2580");
        Check("elaTabFrame(2), the Companions page", Show(comps), "135,390 3810x2580");
        Check("elaItemFrame, sized to the page by Form_Load (FRMCHAR3.BAS:352-357)", Show(Elastic(5)), "0,15 3495x495");
        Check("elaSpells", Show(Elastic(1)), "165,825 3330x1065");
        Check("elaElastic, the companions' pictures", Show(Elastic(3)), "60,270 3705x2205");
        var status = Captioned("X", 0, 0x05);
        var item = Captioned("1. Boots of the Elven Lords (n) [125]", 0, 0x05);
        Check("lblItemStatus(0)", Show(status), "30,-15 210x235");
        Check("lblItem(0)", Show(item), "285,-15 5000x240");
        var vsbItems = Four(form, After(form, new byte[] { 0x14, 0x00, 0x0A, 0xFF, 0x02 }));
        Check("vsbItems", Show(vsbItems), "3510,0 270x2520");
        string[] names = { "Equip", "Use", "Drop", "Info" };
        var buttons = names.Select((n, i) => Captioned(n, i, 0x04)).ToArray();
        for (int i = 0; i < 4; i++)
            Check($"cmdCommand({i}) {names[i]}", Show(buttons[i]), $"2655,{1365 + 285 * i} 870x300");
        var labels = new (string Caption, int Index, string Want)[]
        {
            ("Spell Type", 4, "105,210 1020x240"), ("Spell Name", 1, "135,600 1080x210"), ("Guild", 5, "1875,600 510x195"),
            ("Lev", 2, "2295,600 510x195"), ("Points", 3, "2895,600 555x195"), ("Spell", 0, "105,2070 510x195"),
        };
        foreach (var (caption, index, want) in labels)
            Check($"lblLabel({index}) {caption}", Show(Captioned(caption, index, 0x05)), want);
        var cmbClass = Four(form, After(form, new byte[] { 0x1F, 0x02, 0xFF, 0x04 }));
        var txtSpell = Four(form, After(form, new byte[] { 0xFF, 0x02, 0xFF, 0xFF, 0xE0, 0x00, 0x04 }));
        var vsbSpells = Four(form, After(form, new byte[] { 0x09, 0x00, 0x0A, 0xFF, 0x02 }));
        var cast = Captioned("&Cast", -1, 0x04);
        var info = Captioned("&Info", -1, 0x04);
        Check("cmbClass", Show(cmbClass), "1275,180 2190x300");
        Check("txtSpell", Show(txtSpell), "585,2025 1890x285");
        Check("vsbSpells", Show(vsbSpells), "3495,825 285x1080");
        Check("cmdCast", Show(cast), "2580,2010 600x315");
        Check("cmdInfo", Show(info), "3165,2010 600x315");

        // the port, in pixels of 15 twips, from what was just read
        int Px(int twips) => (int)Math.Round(twips / 15.0);
        string At(int[]? r) => r == null ? "missing" : $"Canvas.Left=\"{Px(r[0])}\" Canvas.Top=\"{Px(r[1])}\" Width=\"{Px(r[2])}\" Height=\"{Px(r[3])}\"";
        if (new[] { tabRect, items, spells, comps, Elastic(5), vsbItems, cmbClass, txtSpell, vsbSpells, cast, info, status, item }.Any(r => r == null) || buttons.Any(r => r == null))
        {
            Console.WriteLine("  FAIL  a rectangle is missing: the port is not checked against it");
            _failed++;
            return;
        }
        // the tab strip: the page starts 360 twips down the control, 24 pixels, two of them the page's edge
        int strip = Px(items![1]) - 2;
        string Page(int[] r) => $"Margin=\"{Px(r[0])},{Px(r[1]) - strip},0,0\"";
        string mdiCity = PortSource("src/Mordor.App", "CityWindow.Mdi.cs"), mdiDungeon = PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs");
        foreach (var (file, mdi, spellList, spellScroll, classBox, spellText) in new[]
                 {
                     ("CityWindow", mdiCity, "SpellList", "SpellScroll", "ClassBox", "SpellText"),
                     ("DungeonWindow", mdiDungeon, "SheetSpellBox", "SheetSpellScroll", "SheetClassBox", "SheetSpellText"),
                 })
        {
            string x = PortSource("src/Mordor.App", file + ".axaml");
            Check($"{file}: the tab control 2 pixels in", x.Contains($"<TabControl x:Name=\"SheetTabs\" Padding=\"0\" Margin=\"{Px(tabRect![0])},{Px(tabRect[1])},{Px(4065 - tabRect[0] - tabRect[2])},{Px(3105 - tabRect[1] - tabRect[3])}\""), true);
            Check($"{file}: its three tabs in a {strip}-pixel strip", mdi.Contains($"IndexTab.Apply(SheetTabs, perRow: 3, strip: {strip}, DesignFonts.CharStat3, \"tabItemTab\")"), true);
            Check($"{file}: drawn at the form's client, scaled down but not zoomed up; a larger window gives the lists the room (ObjectsForm.Reflow)",
                  mdi.Contains(", ObjectsForm.Client);") && !mdi.Contains(", ObjectsForm.Client, grow: true)") && mdi.Contains("ObjectsForm.Reflow(SheetTabs, (Canvas)PackList.Parent!, PackList, PackScroll, PackModButtons,") && mdi.Contains("_sheetChild.BodyMargin = new"), true);
            Check($"{file}: the Items page where elaTabFrame(0) is",
                  x.Contains($"<Canvas Width=\"{Px(items[2])}\" Height=\"{Px(items[3])}\" {Page(items)}"), true);
            // elaItemFrame keeps its width and takes the page's height (Form_Load, FRMCHAR3.BAS:352-357)
            Check($"{file}: the list is elaItemFrame, the page's height",
                  x.Contains($"<ListBox x:Name=\"PackList\" Canvas.Left=\"0\" Canvas.Top=\"0\" Width=\"{Px(Elastic(5)![2])}\" Height=\"{Px(items[3])}\""), true);
            Check($"{file}: vsbItems", x.Contains($"<ScrollBar x:Name=\"PackScroll\" Orientation=\"Vertical\" {At(vsbItems)}"), true);
            for (int i = 0; i < 4; i++)
                Check($"{file}: {names[i]}", x.Contains($"<Button {At(buttons[i])} Focusable=\"False\" Content=\"{names[i]}\""), true);
            Check($"{file}: the mods' buttons stack up from Equip", x.Contains($"<StackPanel x:Name=\"PackModButtons\" Canvas.Left=\"{Px(buttons[0]![0])}\" Canvas.Bottom=\"{Px(items[3]) - Px(buttons[0]![1])}\""), true);
            Check($"{file}: the Spells page where elaTabFrame(1) is",
                  x.Contains($"<Canvas Width=\"{Px(spells![2])}\" Height=\"{Px(spells[3])}\" {Page(spells)}"), true);
            Check($"{file}: cmbClass", x.Contains($"<ComboBox x:Name=\"{classBox}\" {At(cmbClass)}"), true);
            Check($"{file}: the spell rows are elaSpells, five rows tall", x.Contains($"<ListBox x:Name=\"{spellList}\" {At(Elastic(1))}"), true);
            Check($"{file}: vsbSpells", x.Contains($"<ScrollBar x:Name=\"{spellScroll}\" Orientation=\"Vertical\" {At(vsbSpells)}"), true);
            Check($"{file}: txtSpell", x.Contains($"<TextBox x:Name=\"{spellText}\" {At(txtSpell)}"), true);
            Check($"{file}: cmdCast and cmdInfo", x.Contains($"<Button Classes=\"frame\" {At(cast)} Focusable=\"False\" Content=\"_Cast\"") && x.Contains($"<Button Classes=\"frame\" {At(info)} Focusable=\"False\" Content=\"_Info\""), true);
            Check($"{file}: the Companions page where elaTabFrame(2) is",
                  x.Contains($"<Canvas Width=\"{Px(comps![2])}\" Height=\"{Px(comps[3])}\" {Page(comps)}"), true);
            Check($"{file}: rows start where lblItem(0) does, {-item![1]} twips up", x.Contains("<StackPanel Margin=\"0,-1,0,0\"/>"), true);
            Check($"{file}: the pack rows are ObjectsForm.PackRow", PortSource("src/Mordor.App", file + (file == "CityWindow" ? ".Mdi.cs" : ".axaml.cs")).Contains("rows.Add(ObjectsForm.PackRow(mark, ModSlots.PackLine("), true);
        }
        string objects = PortSource("src/Mordor.App", "ObjectsForm.cs");
        Check("the client in the port", objects.Contains("Client = new(4065 / 15, 3105 / 15)"), true);
        Check($"a pack row is lblItemStatus's {status![3]} twips (sub2263, FRMCHAR3.BAS:666-678)", objects.Contains($"PackRowHeight = {status[3]}.0 / 15"), true);
        Check("lblItemStatus and lblItem where the form has them",
              objects.Contains($"Width = {Px(status[2])}, Margin = new Thickness({Px(status[0])}, 0, 0, 0)") && objects.Contains($"line.Margin = new Thickness({Px(item![0])}, 0, 0, 0)"), true);
        Check("a spell row is 210 twips, its columns lblName, lblGuild, lblLevel and lblPoints (1815, 2265, 2775, 525 wide)",
              objects.Contains("SpellRowHeight = 210.0 / 15")
              && PortSource("src/Mordor.App", "SpellPage.cs").Contains($"new ColumnDefinitions(\"{Px(1815 - 30)},{Px(2265 - 1815)},{Px(2775 - 2265)},{Px(525)}\"), Height = ObjectsForm.SpellRowPitch"), true);
        Check("the port's pack and spell rows are the Options font's line and RowPad, never under the form's (UiFonts)",
              objects.Contains("var row = new Panel { Height = PackRowPitch };")
              && objects.Contains("PackRowPitch => UiFonts.Cached(nameof(PackRowPitch), () => UiFonts.RowHeight(DesignFonts.CharStat3, PackRowHeight,")
              && objects.Contains("SpellRowPitch => UiFonts.Cached(nameof(SpellRowPitch), () => UiFonts.RowHeight(DesignFonts.CharStat3, SpellRowHeight,"), true);
        Check("Objects & Spells keeps its proportions (FRMCHAR3.BAS:424-430)", WindowLayout.KeepsAspect(GameWindow.ObjectsAndSpells), true);

        Console.WriteLine("\nthe character window's tabs are frmCharStat1's tabStatTab (MORDOR.EXE RCDATA 8)");
        {
            int t = After(stat1, Ascii("VideoSoftIndexTab\xff\x02"));
            Check("tabStatTab", Show(Four(stat1, t)), "30,30 3915x5055");
            Check("... its tabs", t >= 0 && After(stat1, Counted("Stats|Resist.|Char|Guild|Look|Buffers|Misc"), t) > 0, true);
            int row = After(stat1, new byte[] { 0x2A, 0x00, 0x2C }, Math.Max(t, 0));
            Check("... four to the row (property 2C), so the seven wrap onto two", row >= 0 ? (int)stat1[row] : -1, 4);
            // picTabPicture, the page, a BackColor of &H8000000F& then its rectangle
            var page = Four(stat1, After(stat1, new byte[] { 0x01, 0x0F, 0x00, 0x00, 0x80, 0x05 }, Math.Max(t, 0)));
            Check("picTabPicture, below the two rows", Show(page), "180,510 3555x4335");
            int strip1 = page == null ? -1 : Px(page[1]) - 2;
            Check($"the port's strip, {strip1} pixels for the two rows",
                  PortSource("src/Mordor.App", "CharacterPanel.cs").Contains($"IndexTab.Apply(_tabs, perRow: 4, strip: {strip1}, DesignFonts.CharStat1, \"tabStatTab\")"), true);
        }
    }

    /// <summary>A file of the port's own source, beside this build (the worktree it was built in).</summary>
    private static string PortSource(params string[] parts)
    {
        string p = PortTree.At(parts);
        if (System.IO.File.Exists(p)) return System.IO.File.ReadAllText(p);
        // the decompiled source in a tree without it (the public one): the section is skipped
        if (PortTree.PrivateMissing(parts)) throw new DecompiledMissing(p);
        // an empty text would let every "does not contain" check pass on nothing: say so and fail
        Console.WriteLine($"  FAIL  port source not found: {p}");
        _failed++;
        return "";
    }

    /// <summary>The 2026-09-27 queue: item tags, joins, the portrait strip, the Store, the gold line.</summary>
    /// <summary>
    /// The Bank's Deposit and Withdraw buttons, double-click and multi-selection (a deviation the
    /// user asked for): the vault's own rule, and the buttons going through the drag paths' code
    /// rather than a copy of it, placed in the form's empty space.
    /// </summary>
    private static void BankButtonChecks()
    {
        Console.WriteLine("\nthe Bank's vault rule (lstItems_DragDrop, FRMCITYB.BAS:172-206)");
        var c = new Character { Name = "V" };
        c.PutInSlot(5, new InventorySlot { ItemId = 7 });
        Check("deposit: into the first slot", c.DepositToVault(5), true);
        Check("... the vault holds it and the pack slot is empty", $"{c.Bank.Count} {c.Bank[0].ItemId} {c.SlotEmpty(5)}", "1 7 True");
        c.Bank = new List<InventorySlot> { new() { ItemId = 1 }, new(), new() { ItemId = 2 } };
        c.PutInSlot(6, new InventorySlot { ItemId = 9 });
        Check("deposit: a hole a withdrawal left is taken first", c.DepositToVault(6) && c.Bank[1].ItemId == 9 && c.Bank.Count == 3, true);
        Check("the Hands never go in", c.DepositToVault(0), false);
        c.Bank = Enumerable.Range(1, Character.BankSlots).Select(i => new InventorySlot { ItemId = (short)i }).ToList();
        c.PutInSlot(7, new InventorySlot { ItemId = 11 });
        Check("forty full: refused", c.DepositToVault(7), false);
        Check("... and the item stays in the pack", c.Pack[7]?.ItemId, 11);
        Check("forty full: an empty slot is refused too (the room test comes first)", c.DepositToVault(8), false);
        c.Bank[3] = new InventorySlot();
        Check("a hole free: an empty slot goes through and changes nothing", c.DepositToVault(8) && c.Bank[3].ItemId <= 0, true);

        Console.WriteLine("\nthe Bank's Deposit and Withdraw buttons use the drag paths' code (FRMCHAR3.BAS:530-561)");
        string src = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
        Check("the drop and Deposit call one routine", src.Contains("Dragging.Target(vault, DragKind.Item, async p => await Deposit(p.Index));")
                                                     && src.Contains("deposit.Click += async (_, _) => await Deposit(PackList.SelectedIndex);"), true);
        Check("Withdraw takes each line with TakeFromVault into the first free slot", src.Contains("int free = _ch.FirstFreeSlot();") && src.Contains("TakeFromVault(v, free);"), true);
        Check("a full pack is the vault branch's box (MAINMOD2.BAS:2985)", src.Contains("await Tell(\"SORRY!\", Party.NoFreeSlotsFromVault(_ch))"), true);
        Check("the equipped refusal written in the two drag paths only",
              System.Text.RegularExpressions.Regex.Matches(src, "Cannot put equipped items in the bank!").Count, 2);
        Check("Withdraw and a double-click share the one routine", src.Contains("withdraw.Click += async (_, _) => await Withdraw(") && src.Contains("await Withdraw(new[] { row });"), true);
        Check("the list takes Shift/Ctrl selections", src.Contains("vault.SelectionMode = SelectionMode.Multiple;"), true);
        Check("TakeFromVault tells an open Store (FRMCHAR3.BAS:583)", src.Contains("_bankRefresh?.Invoke();\r\n        _storeUpdate?.Invoke();") || src.Contains("_bankRefresh?.Invoke();\n        _storeUpdate?.Invoke();"), true);

        // the port's controls in space the form leaves empty
        FormRect Parsed(string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(src, name + @" = new\((\d+), (\d+), (\d+), (\d+)\)");
            return m.Success ? new FormRect(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value)) : default;
        }
        static bool Meet(FormRect a, FormRect b) =>
            a.Left < b.Left + b.Width && b.Left < a.Left + a.Width && a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;
        static bool Inside(FormRect a, int w, int h) => a.Left >= 0 && a.Top >= 0 && a.Left + a.Width <= w && a.Top + a.Height <= h;
        var withdraw = Parsed("BankWithdrawButton");
        var deposit = Parsed("BankDepositButton");
        var mods = Parsed("BankModCommands");
        var B = typeof(ServiceForms.Bank);
        Check("Withdraw: inside elaElastic(3), clear of lstItems and cmdInfo",
              withdraw.Width > 0 && Inside(withdraw, ServiceForms.Bank.ItemsBox.Width, ServiceForms.Bank.ItemsBox.Height)
              && !Meet(withdraw, ServiceForms.Bank.lstItems) && !Meet(withdraw, ServiceForms.Bank.cmdInfo), true);
        // Deposit and the mod commands are in elaElastic(0), with every control of it (the ones in
        // the other Elastics are covered by their Elastic's own rectangle)
        var rootControls = new[] { ServiceForms.Bank.Welcome, ServiceForms.Bank.AccountCaption, ServiceForms.Bank.StatusBox,
            ServiceForms.Bank.DepositCaption, ServiceForms.Bank.DepositAmount, ServiceForms.Bank.DepositAll,
            ServiceForms.Bank.WithdrawCaption, ServiceForms.Bank.WithdrawAmount, ServiceForms.Bank.WithdrawAll,
            ServiceForms.Bank.PartyCaption, ServiceForms.Bank.PartyBox, ServiceForms.Bank.ItemsCaption, ServiceForms.Bank.ItemsBox, ServiceForms.Bank.cmdExit };
        Check("Deposit: inside the form, clear of every control", deposit.Width > 0
              && Inside(deposit, ServiceForms.Bank.ClientW, ServiceForms.Bank.ClientH) && rootControls.All(r => !Meet(deposit, r)), true);
        Check("the mod commands' place: inside the form, clear of every control", mods.Width > 0
              && Inside(mods, ServiceForms.Bank.ClientW, ServiceForms.Bank.ClientH) && rootControls.All(r => !Meet(mods, r)), true);
        Check("the form's own rectangles are unchanged", B.GetField("cmdInfo") != null && ServiceForms.Bank.cmdInfo == new FormRect(45, 1920, 1530, 300)
              && ServiceForms.Bank.lstItems == new FormRect(60, 60, 2940, 1785) && ServiceForms.Bank.cmdExit == new FormRect(4425, 3585, 1530, 330), true);
        var info = ServiceForms.Bank.cmdInfo; var exit = ServiceForms.Bank.cmdExit; var box = ServiceForms.Bank.ItemsBox;
        Check("Deposit: left lined up with Info's, 45 narrower than Info, ending 45 twips short of Exit, Exit's top and height",
              $"{deposit.Left - (box.Left + info.Left)} {info.Width - deposit.Width} {exit.Left - (deposit.Left + deposit.Width)} {deposit.Top - exit.Top} {deposit.Height - exit.Height}",
              "0 45 45 0 0");
    }

    /// <summary>
    /// The 2026-09-28 review: markers that could pass on nothing, a list drag of the line pressed
    /// on, no drag from a double-click's second press, form-sized services in a small frame, and
    /// the smoke test's process and build handling.
    /// </summary>
    private static void ReviewFixChecks()
    {
        Console.WriteLine("\na source marker that is missing is a failure, not a pass on the rest of the file");
        {
            int before = _failed;
            var was = Console.Out;
            Console.SetOut(System.IO.TextWriter.Null);
            string noEnd = Between("Sub a ()\n  x\nEnd Sub", "Sub a ()", "End Function");
            string noStart = Between("Sub a ()\n  x\nEnd Sub", "Sub b ()", "End Sub");
            string both = Between("Sub a ()\n  x\nEnd Sub\nSub c ()\nEnd Sub", "Sub a ()", "End Sub");
            int counted = _failed - before;
            Console.SetOut(was);
            _failed = before;
            Check("Between: a missing end and a missing start each count one failure, and hand back nothing",
                  $"{counted} [{noEnd}] [{noStart}]", "2 [] []");
            Check("... and with both there, the text runs to the first end after the start", both, "Sub a ()\n  x\n");
        }

        Console.WriteLine("\na list drag takes the line pressed on; a double-click's second press drags nothing (FRMCHAR3.BAS:589-597)");
        {
            string drag = PortSource("src/Mordor.App", "DragPayload.cs");
            string original = PortSource("decompiled-named", "FRMCHAR3.BAS").Replace("\r\n", "\n");
            string[] ol = original.Split('\n');
            Check("the original: 589 is lblItem_MouseMove, 590 its early return on an unmoved pointer",
                  ol.Length > 590 ? $"{ol[588].Trim()} | {ol[589].Trim()}" : "",
                  "Sub lblItem_MouseMove (Index%, Button As Integer, Shift As Integer, X As Single, Y As Single) | If  X = m0022 And Y = m0026 Then Exit Sub");
            Check("... and 597 ends it", ol.Length > 597 ? ol[596].Trim() : "", "End Sub");
            Check("the port cites 589-597, not 586-594", drag.Contains("FRMCHAR3.BAS:589-597") && !drag.Contains("586-594"), true);
            Check("a press is a drag start only below a click count of two", drag.Contains("&& e.ClickCount < 2 && pressedOn(e)"), true);
            Check("a DoubleTapped ends the press", drag.Contains("from.AddHandler(InputElement.DoubleTappedEvent, (_, _) => pressedAt = null,"), true);
            var lists = new[] { ("src/Mordor.App", "CityWindow.axaml.cs"), ("src/Mordor.App", "CityWindow.Mdi.cs"), ("src/Mordor.App", "DungeonWindow.Mdi.cs"),
                                ("src/Mordor.App", "DungeonWindow.axaml.cs"), ("src/Mordor.App", "ExchangeWindow.cs") };
            int fromSelection = 0, byLine = 0;
            foreach (var (d, f) in lists)
            {
                string s = PortSource(d, f);
                fromSelection += System.Text.RegularExpressions.Regex.Matches(s, @"Dragging\.Source\([^;]*SelectedIndex").Count;
                byLine += System.Text.RegularExpressions.Regex.Matches(s, @"Dragging\.SourceLines\(").Count;
            }
            Check("no drag source reads a list's SelectedIndex", fromSelection, 0);
            Check("the vault, both packs and both spell lists drag the pressed line", byLine, 5);
            Check("the vault among them",
                  PortSource("src/Mordor.App", "CityWindow.axaml.cs").Contains("Dragging.SourceLines(vault, row => row < rowSlots.Count ? DragPayload.Vault(rowSlots[row]) : null);"), true);
        }

        Console.WriteLine("\na form-drawn service in a smaller frame keeps its size, for the frame's scroll bars");
        {
            string mdi = PortSource("src/Mordor.App", "CityWindow.Mdi.cs").Replace("\r\n", "\n");
            string place = Between(mdi, "private void PlaceService()", "private (double W, double H) ServicePageSize()");
            Check("the frame caps only a page of the port's own layout",
                  place.Contains("bool formSized = _serviceForm != null;")
                  && place.Contains("if (hw > 1 && !formSized) w = Math.Min(w, hw);")
                  && place.Contains("if (hh > 1 && !formSized) h = Math.Min(h, hh);")
                  && !place.Contains("\n        if (hw > 1) w = Math.Min(w, hw);\n"), true);
            Check("and it is placed from the frame's corner at the least", place.Contains("PlaceAt(new Avalonia.Rect(Math.Max(0, x), Math.Max(0, y), w, h))"), true);
            // a window 480x316 in a frame 400x250: both bars, reaching its far edges
            var bars = MdiScroll.Bars(new[] { new MdiScroll.Child(0, 0, 480, 316, true, false, false) }, 400, 250, 16);
            Check("MdiScroll: a 480x316 window at the corner of a 400x250 frame gets both bars", $"{bars.H} {bars.V}", "True True");
            Check("... reaching past its right and bottom edges", bars.MaxX >= 480 - 400 && bars.MaxY >= 316 - 250, true);
        }

        Console.WriteLine("\nthe docs say what the code does");
        {
            string fid = PortSource("docs", "FIDELITY.md");
            Check("no DungeonWindow.FrameSize left (ScaledFrameSize)", fid.Contains("DungeonWindow.FrameSize`"), false);
            Check("the Bank's Store UPDATE is said to reach an open Store", fid.Contains("**The town's services are windows of their own, up together - corrected 2026-10-19.**") && !fid.Contains("and the call finds no Store"), true);
        }

        Console.WriteLine("\nthe smoke test stops only its own copy, and fails on a failed build");
        {
            string sh = PortSource("tools", "check.sh");
            Check("the id file goes once the app is seen gone", sh.Contains("if ($p.HasExited) {\n    Remove-Item -LiteralPath $PidFile")
                                                              || sh.Contains("if ($p.HasExited) {\r\n    Remove-Item -LiteralPath $PidFile"), true);
            Check("the id's executable is checked against the scratch copy", sh.Contains("(Get-Process -Id $w -ErrorAction Stop).Path")
                                                                            && sh.Contains("= \"$SMOKE_EXE_M\" ]; then"), true);
            Check("and taskkill is filtered to OpenDejenol.exe", sh.Contains("taskkill //F //PID \"$w\" //FI \"IMAGENAME eq OpenDejenol.exe\""), true);
            Check("no unfiltered taskkill", System.Text.RegularExpressions.Regex.IsMatch(sh, @"taskkill //F //PID ""\$w"" >"), false);
            Check("the app's build status is kept and a failure fails the smoke", sh.Contains("build_rc=$?") && sh.Contains("if [ \"$build_rc\" -ne 0 ]; then"), true);
            Check("a locked exe is named as such", sh.Contains("most likely because the"), true);
            Check("the harness's build status is kept too", sh.Contains("BUILD_RC=$?"), true);
        }
    }

    /// <summary>A slot's identification level survives a save and a load.</summary>
    private static void SlotKnownChecks()
    {
        Console.WriteLine("\na pack slot's ID level is kept across a save and load");
        static InventorySlot Read(string json) => System.Text.Json.JsonSerializer.Deserialize<InventorySlot>(json)!;
        Check("Known 1 then Identified false stays Partial", Read("{\"Known\":1,\"Identified\":false}").Known, IdLevel.Partial);
        Check("Identified false then Known 2 stays Mostly", Read("{\"Identified\":false,\"Known\":2}").Known, IdLevel.Mostly);
        Check("an old save with Identified false alone is Barely", Read("{\"Identified\":false}").Known, IdLevel.Barely);
        Check("an old save with Identified true alone is All", Read("{\"Identified\":true}").Known, IdLevel.All);
        var slot = new InventorySlot { ItemId = 5, Known = IdLevel.Partial };
        string written = System.Text.Json.JsonSerializer.Serialize(slot);
        Check("Identified is no longer written", written.Contains("Identified"), false);
        Check("and a round trip keeps Partial", Read(written).Known, IdLevel.Partial);

        Console.WriteLine("\na dead member's gold does not cover another's shortfall");
        var buyer = new Character { Name = "B", GoldOnHand = 0 };
        var body = new Character { Name = "D", GoldInBank = 5000, IsDead = true };   // a shortfall is covered from accounts (QoL 1.13.0)
        var alive = new Character { Name = "A", GoldInBank = 100 };
        Check("the dead are passed over", Party.PlanShortfall(new[] { buyer, body }, buyer, 300) == null, true);
        Check("the living still give", Party.PlanShortfall(new[] { buyer, body, alive }, buyer, 100)?.Count, 1);
    }

    private static void QueueChecks(string dir)
    {
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));

        Console.WriteLine("\nitemNameFor's alignment is gv077E$'s tag (MAINMOD2.BAS:2571-2573, MODULE1.BAS:552-555)");
        var dagger = items.FirstOrDefault(i => i.Name.TrimEnd() == "Iron Dagger");
        if (dagger != null)
        {
            Check("a Good Iron Dagger, fully known", Loot.NameFor(dagger, IdLevel.All, ItemAlignment.Good), "Iron Dagger (g)");
            Check("an Evil one", Loot.NameFor(dagger, IdLevel.All, ItemAlignment.Evil), "Iron Dagger (e)");
            Check("an unaligned one has no tag", Loot.NameFor(dagger, IdLevel.All, ItemAlignment.Unaligned), "Iron Dagger");
            Check("mostly known: the name alone", Loot.NameFor(dagger, IdLevel.Mostly, ItemAlignment.Neutral), "Iron Dagger");
        }
        else { Console.WriteLine("  FAIL  no Iron Dagger in MDATA3"); _failed++; }

        Console.WriteLine("\nthe Store never shelves a cursed item (FRMCTYS.BAS:267, MAINMOD.BAS initial stock and restock)");
        foreach (var tome in items.Where(i => i.Name.TrimEnd() == "Tome of Lies"))
            Console.WriteLine($"  Tome of Lies: id {tome.Id}, Cursed {tome.Cursed}, Floor {tome.Floor}, Align flags {tome.AlignmentFlags}, Price {tome.Price}");        var byId = items.ToDictionary(i => i.Id);
        var initial = StoreStock.CreateInitial(items);
        Check("cursed items on a new game's shelf",
              items.Count(i => i.Cursed != 0 && initial.AnyInStock(i.Id)), 0);
        var turned = StoreStock.CreateInitial(items);
        turned.Replenish(items, 50, new Random(7));
        for (int n = 0; n < 200; n++) turned.Replenish(items, 50, new Random(n));
        Check("cursed items on it after 200 restocks",
              items.Count(i => i.Cursed != 0 && turned.AnyInStock(i.Id)), 0);

        Console.WriteLine("\nObjects & Spells in town: every row has lblItemStatus's column (MAINMOD2.BAS:5429-5433)");
        string mdi = PortSource("src/Mordor.App", "CityWindow.Mdi.cs");
        Check("an empty town row is a blank mark and \"n.\"", mdi.Contains("ObjectsForm.PackRow(\"\", PackLine($\"{i + 1}.\", false, false))"), true);
        Check("the drag swap sends the Store its UPDATE (FRMCHAR3.BAS:583)",
              Between(mdi, "Dragging.Target(PackList, DragKind.Item", "DragKind.Vault").Contains("_storeUpdate?.Invoke()"), true);

        Console.WriteLine("\nmonGetGold's line is lblCharacterHit's for half a second, not sub085B (MONSMOD.BAS:2158, 2174-2176)");
        {
            var gMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var payer = gMons.First(m => m.Id != 0 && m.GoldFactor > 0);
            int paid = 0, logged = 0, captioned = 0;
            for (int seed = 0; seed < 200 && paid < 20; seed++)
            {
                var lone = Character.Create("g", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
                var fight = new CombatEngine(lone, new[] { new CombatMonster { Def = payer, Hits = 0 } },
                                             id => items.FirstOrDefault(i => i.Id == id), new Random(seed));
                if (fight.GoldEarned <= 0 || fight.ChestClass != 0) continue;
                fight.AwardSpoils();
                paid++;
                if (fight.Log.Any(l => l.Contains("share", StringComparison.OrdinalIgnoreCase))) logged++;
                if (fight.GoldLine?.StartsWith("Your share is ") == true) captioned++;
            }
            CheckAtLeast("fights paid at victory", paid, 5);
            Check("share lines in the fight's log (the Messages list)", logged, 0);
            Check("share lines left for lblCharacterHit", captioned, paid);
        }
        string dw = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
        Check("and nothing forwards a share line to Messages", dw.Contains("chest.Log.Count"), false);

        Console.WriteLine("\nthe typed Swap Items remaps no buffer; a dragged swap does (MAINMOD2.BAS:3432-3442, FRMCHAR3.BAS:571-577)");
        {
            var c = Character.Create("s", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            c.Buffers[0] = SpellBuffers.ForItem(2);
            c.SwapSlots(3, 5, buffersFollow: false);
            Check("typed: buffer 1 still names slot 3", SpellBuffers.PackSlotOf(c.Buffers[0]) + 1, 3);
            c.SwapSlots(3, 5);
            Check("dragged: buffer 1 follows to slot 5", SpellBuffers.PackSlotOf(c.Buffers[0]) + 1, 5);
            string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
            Check("the city's s passes buffersFollow: false", Between(city, "private async void SwapItems()", "Log(SwapCommand.Done)").Contains("buffersFollow: false"), true);
            Check("the dungeon's s passes buffersFollow: false", Between(dw, "private async void SwapItems()", "Log(SwapCommand.Done)").Contains("buffersFollow: false"), true);
        }

        Console.WriteLine("\nAsk to Join: no silent refusal, and the party's square is brought up to date first (MAINMOD2.BAS:3444-3523)");
        {
            string join = Between(dw, "private async void AskToJoin()", "private async System.Threading.Tasks.Task<bool> OfferPendingRaise");
            int sync = join.IndexOf("SavePos();", StringComparison.Ordinal), check = join.IndexOf("_party.WhyCannotJoin(who)", StringComparison.Ordinal);
            Check("SavePos runs before the join test", sync >= 0 && check > sync, true);
            Check("the refusal is said", join.Contains("is string refused) { Say(refused); who.Save(whoFile); return; }"), true);
            Check("no bare \"if (!_party.Add(who)) return;\"", join.Contains("if (!_party.Add(who)) return;"), false);
        }

        Console.WriteLine("\nmonShowMonsters draws one portrait per group (MONSMOD.BAS:4351-4396)");
        string axaml = PortSource("src/Mordor.App", "DungeonWindow.axaml");
        Check("the single MonsterImage is gone", axaml.Contains("x:Name=\"MonsterImage\""), false);
        Check("RefreshCombat redraws the strip", Between(dw, "private void RefreshCombat()", "// ---").Contains("ShowPortraits(_combat.Monsters)"), true);

        Console.WriteLine("\nthe Store's list: fixed labels that clip, and take no keys with mods off (FRMCTYS.BAS:407-424)");
        string store = PortSource("src/Mordor.App", "CityWindow.Store.cs");
        Check("no sideways scroll bar", store.Contains("HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled"), true);
        Check("the list's focus follows the unlock decision", store.Contains("buyList.Focusable = on;"), true);
        Check("and is looked at again when a mod setting changes", store.Contains("ModHost.Active.Changed += keysSettingChanged;"), true);
        Check("a change of row asks the decision afresh", store.Contains("if (KeysTakeRows()) ClickRow(picked);"), true);
        Check("with mods off, the decision is no", Mordor.Modding.ModHost.Empty().StoreAllowsRowChangeWhileChoosing(), false);
        Check("a press leaves the click to the release", store.Contains("if (pressing) return;"), true);
        Check("only the left button's release ends the press", store.Contains("if (ev.InitialPressMouseButton != MouseButton.Left) return;\n            bool wasPressing")
                                                               || store.Contains("if (ev.InitialPressMouseButton != MouseButton.Left) return;\r\n            bool wasPressing"), true);
        Check("a lost capture ends it too", store.Contains("buyList.PointerCaptureLost +="), true);

        Console.WriteLine("\nthe Store's UPDATE after a give and after any pack drop (FRMGROUP.BAS:205-206, FRMCHAR3.BAS:583)");
        {
            string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
            string give = Between(city, "Dragging.Target(row, DragKind.Item", "Dragging.Target(row, DragKind.Vault");
            Check("a give onto a party member sends it", give.Contains("finally { _storeUpdate?.Invoke(); }"), true);
            string drop = Between(mdi, "Dragging.Target(PackList, DragKind.Item", "DragKind.Vault");
            Check("a drop onto the same slot or the Hands is not an early return",
                  drop.Contains("if (onto == p.Index) return;") || drop.Contains("if (!_ch.SwapSlots(p.Index + 1, onto + 1)) return;"), false);
        }

        Console.WriteLine("\nmonOpenChest: the treasure after picTrap's and monGetGold's half seconds (MONSMOD.BAS:3650, 2175, 3661)");
        {
            string open = Between(dw, "private void OpenChestBy(Character opener)", "private IEnumerable<string> TreasureFrom");
            int hold = open.IndexOf("await ShowGoldLine(goldLine);", StringComparison.Ordinal);
            int loot = open.IndexOf("RunPendingTreasure(treasure);", StringComparison.Ordinal);
            Check("the treasure is gone through after the gold line's wait", hold >= 0 && loot > hold, true);
            Check("and not straight away", open.Contains("if (chest.ChestLead is Monster owner) Out(TreasureFrom(owner"), false);
            Check("a leaving goes through it first", Between(dw, "private void FinishWonFight()", "private bool _holdingKeys").Contains("RunPendingTreasure(_pendingTreasure);"), true);
            Check("keys wait through sub07E2 (sub0456's queue)", dw.Contains("try { if (KeyMustWait(e)) return; }"), true);
            Check("the buffer, Spell, Take and Map buttons queue as their keys do (FRMDUNM.BAS:76-122)",
                  dw.Contains("ButtonMustWait(() => FireBuffer(which))") && dw.Contains("private void OnTakeStairs(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.T);") && dw.Contains("private void Press(Key k, KeyModifiers mods = KeyModifiers.None)")
                  && dw.Contains("ButtonMustWait(() => OnShowSheetSpells(s, e))") && dw.Contains("ButtonMustWait(() => OnOpenAutomap(s, e))"), true);
            Check("a chest holds the keys until its treasure is given", dw.Contains("HoldUntil(AfterChestOpened(goldLine, treasure));"), true);
            Check("a held box waits for its answer before the next held key (a mod's window is a child in the frame, not a window it owns)", dw.Contains("while (!_ended && DialogOwner.OwnedWindows.Any(w => w.IsVisible))"), true);
        }

        Console.WriteLine("\nParty Information: a mod column widens the contents, not squeezes Name");
        {
            string slots = PortSource("src/Mordor.App", "ModSlots.cs");
            Check("the header hands its mod width, and the room after it, to the window's scaling",
                  slots.Contains("WidenWindowContents(header, cols.Count == 0 ? 0 : cols.Sum(c => PartyColumnWidth(c.Column)) + PartyRightRoom * CityWindow.PartyGrowthX);"), true);
            // the heading and every row take the same width, never under the mod's own (grown with
            // the drawing in a larger Options font), and wider only for a heading that font no longer fits
            Check("a mod column is as wide in the heading as in the rows, and holds its heading",
                  slots.Contains("header.ColumnDefinitions.Add(new ColumnDefinition(PartyColumnWidth(cols[i].Column), GridUnitType.Pixel));")
                  && slots.Contains("row.ColumnDefinitions.Add(new ColumnDefinition(width, GridUnitType.Pixel));")
                  && slots.Contains("double width = PartyColumnWidth(cols[i].Column);")
                  && slots.Contains("Math.Max(column.Width * CityWindow.PartyGrowthX, UiFonts.TextWidth(column.Header, DesignFonts.Party.Pass,"), true);
            // a cell exactly its column's width, its text set right in it: a row whose cell outgrew
            // its column had been laid out a few pixels off the others
            Check("a mod's cell is its column's width, whatever it holds",
                  slots.Contains("? new Border { Width = width, HorizontalAlignment = HorizontalAlignment.Left, Child = drawn }")
                  && slots.Contains("Text = cell?.ToString() ?? \"\", Foreground = foreground, Width = width,"), true);
            Check("the Next column is kept off the window's right edge by as much as Leave is", slots.Contains("public const double PartyRightRoom = 4;"), true);
            string frame = PortSource("src/Mordor.App", "MdiFrame.cs");
            Check("ShrinkToFit adds it to the drawn width", frame.Contains("return _natural.Width + _extraWidth;"), true);
        }

        // a local function, so its names are its own (the checks above use the same ones)
        void FontFitChecks()
        {
        Console.WriteLine("\nsub150A's arrangement, and the Automap kept off a mod's Party Information columns");
        {
            // a new character's table at 800x600 and at 1250x937 (the scale the port's smoke runs at)
            foreach (var (sw, sh) in new[] { (800.0, 600.0), (1250.0, 937.5) })
            {
                WindowRect[] Laid(bool widened)
                {
                    var t = WindowLayout.Fresh();
                    WindowLayout.Seed(t, sw, sh, largeFonts: false);
                    for (int i = 0; i < t.Length; i++)
                        if (WindowLayout.KeepsAspect((GameWindow)i)) t[i].Height = WindowLayout.AspectHeight((GameWindow)i, t[i].Width);
                    WindowLayout.Arrange(t, sw, sh, 23, 4, widened);
                    return t;
                }
                var plain = Laid(false);
                var wide = Laid(true);
                var ch = plain[(int)GameWindow.Character]; var gr = plain[(int)GameWindow.Group];
                var am = plain[(int)GameWindow.Automap]; var os = plain[(int)GameWindow.ObjectsAndSpells];
                Check($"{sw}x{sh}: with no mod column, the Automap one pixel right of the character window (MAINMOD2.BAS:3241)",
                      am.Left, ch.Left + ch.Width + 1);
                Check($"{sw}x{sh}: ... which leaves it over Party Information's right edge, as in the original",
                      am.Left < gr.Left + gr.Width, true);
                bool same = true;
                for (int i = 0; i < plain.Length; i++)
                    if (i != (int)GameWindow.Automap && (plain[i].Left != wide[i].Left || plain[i].Top != wide[i].Top)) same = false;
                Check($"{sw}x{sh}: a mod column moves the Automap and nothing else", same, true);
                var amw = wide[(int)GameWindow.Automap];
                Check($"{sw}x{sh}: with one, the Automap clear of Party Information as far as Objects & Spells leaves room",
                      amw.Left, Math.Max(ch.Left + ch.Width + 1, Math.Min(gr.Left + gr.Width + 1, os.Left - amw.Width - 1)));
                Check($"{sw}x{sh}: ... and never further into Objects & Spells than without",
                      amw.Left + amw.Width <= Math.Max(os.Left - 1, am.Left + am.Width) + 1e-9, true);
                if (sw > 1000) Check($"{sw}x{sh}: there is room at this size: past Party Information", amw.Left, gr.Left + gr.Width + 1);
            }
            string frame = PortSource("src/Mordor.App", "MdiFrame.cs");
            Check("the frame asks the mods whether a Party Information column is shown",
                  frame.Contains("bool partyWidened = Mordor.Modding.ModHost.Active.ShownPartyColumns().Count > 0;")
                  && frame.Contains("WindowLayout.Arrange(drawn, fw, fh, SystemMetric(4), SystemMetric(33), partyWidened);"), true);
        }

        Console.WriteLine("\nthe Options fonts: frmDungeonMain's rows and button row, Objects & Spells' bottom line and Companions");
        {
            string mdi = System.Text.RegularExpressions.Regex.Replace(PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs"), @"\s+", " ");
            string xaml = PortSource("src/Mordor.App", "DungeonWindow.axaml");
            // lblMonster's rows and the two captions are the font's line where that is taller; with
            // the defaults' 13-pixel line, 14, 14 and 13 as the form has them, and nothing moves
            Check("the monster rows are the font's line and a pixel, never under the form's 210 twips",
                  mdi.Contains("double pitch = Math.Max(RowPitch, line + 1);") && mdi.Contains("const double RowPitch = 210.0 / 15, CharacterHitHeight = 210.0 / 15, MonsterHitHeight = 195.0 / 15;")
                  && mdi.Contains("ItemHeight = pitch"), true);
            Check("lblCharacterHit and lblMonsterHit are the line's height, never under their own",
                  mdi.Contains("double hit = UiFonts.LabelHeight(form, CharacterHitHeight, DesignFont.Default);")
                  && mdi.Contains("double monsterHit = UiFonts.LabelHeight(form, MonsterHitHeight, DesignFont.Default);")
                  && mdi.Contains("CombatHit.Height = hit;") && mdi.Contains("CombatMonsterHit.Height = monsterHit;"), true);
            Check("with the default fonts the drawing is the form's: nothing grows",
                  mdi.Contains("if (grow > 0) {") && mdi.Contains("if (FontRule.IsDefault(UiFonts.Options)) return;"), true);
            // the window keeps its size: the drawing grows across in the form's proportions
            Check("the drawing grows across by as much as down, so the window keeps its shape",
                  mdi.Contains("double wider = Math.Ceiling(DungeonCanvas.Width * grow / DungeonCanvas.Height);")
                  && mdi.Contains("DungeonCanvas.Height += grow;") && mdi.Contains("DungeonCanvas.Width += wider;"), true);
            Check("the XAML's rows are the form's 14 pixels as they were", xaml.Contains("ItemWidth=\"166\" ItemHeight=\"14\""), true);
            // a VB3 CommandButton's black frame parts the buttons the form overlaps by 15 twips
            int framed = System.Text.RegularExpressions.Regex.Matches(xaml, "<Button Classes=\"frame\" Focusable=\"False\" x:Name=\"(MapBtn|PickupBtn|DropBodyBtn|FightBtn|SpellBtn|TakeBtn|OpenBtn|ExitBtn)\"").Count;
            Check("the eight cmdAction buttons are framed", framed, 8);
            Check("the buffers are framed", PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Contains("Classes = { \"frame\" },"), true);
            // cmdAction's rectangles in frmDungeonMain's form data (MORDOR.EXE RCDATA 22), in
            // picPicture(1) at 45,2715: Map (6) 2490 500 wide, Pickup (4) 3015 600, Drop (5) 3600
            // 600, Fight (0) 4230 500, Spell (1) 4710 500, Take (2) 5235 600, Open (3) 5865 600,
            // Exit (7) 6495 600. Pickup and Fight each run 15 twips under the next: the frame parts them.
            foreach (var (btn, left, width) in new[] { ("MapBtn", 2490, 500), ("PickupBtn", 3015, 600), ("DropBodyBtn", 3600, 600), ("FightBtn", 4230, 500),
                                                        ("SpellBtn", 4710, 500), ("TakeBtn", 5235, 600), ("OpenBtn", 5865, 600), ("ExitBtn", 6495, 600) })
            {
                var at = System.Text.RegularExpressions.Regex.Match(xaml, $"x:Name=\"{btn}\"[^>]*Canvas.Left=\"(\\d+)\" Canvas.Top=\"180\" Width=\"(\\d+)\"");
                Check($"{btn} where its cmdAction is, in pixels", at.Success ? $"{at.Groups[1].Value} {at.Groups[2].Value}" : "missing",
                      $"{(45 + left) / 15} {width / 15}");
            }
            // cmdAction_Click (FRMDUNM.BAS:76-109) hands each button's key to sub0456: Fight (0) f,
            // Spell (1) c, Take (2) t, Open (3) o, Pickup (4) p, Drop (5) l, Map (6) F8, Exit (7) q.
            // Each button does what its key does: it presses the key, or calls the very handler the
            // key's case calls. Take had opened Objects & Spells (found on screen 2026-10-26).
            string dcs = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
            foreach (var (btn, key) in new[] { ("FightBtn", "F"), ("SpellBtn", "C"), ("TakeBtn", "T"), ("OpenBtn", "O"),
                                               ("PickupBtn", "P"), ("DropBodyBtn", "L"), ("MapBtn", "F8"), ("ExitBtn", "Q") })
            {
                var click = System.Text.RegularExpressions.Regex.Match(xaml, $"x:Name=\"{btn}\"[^>]*Click=\"(\\w+)\"");
                string handler = click.Success ? click.Groups[1].Value : "";
                bool presses = dcs.Contains($"private void {handler}(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.{key});");
                bool sameAsKey = handler switch
                {
                    "OnOpenAutomap" => dcs.Contains("case Key.F8: OnOpenAutomap(null, null!); e.Handled = true; return;"),
                    "OnShowSheetSpells" => dcs.Contains("if (!_ch.IsDead) OnShowSheetSpells(null, null!);"),
                    _ => false,
                };
                Check($"{btn} (cmdAction) does what the {key} key does", handler != "" && (presses || sameAsKey), true);
            }
            foreach (var file in new[] { "CityWindow", "DungeonWindow" })
            {
                string x = PortSource("src/Mordor.App", file + ".axaml");
                Check($"{file}: the frame is black, and stays black under the pointer and pressed",
                      x.Contains("<Style Selector=\"Button.frame\">") && x.Contains("<Setter Property=\"BorderBrush\" Value=\"Black\"/>")
                      && x.Contains("<Style Selector=\"Button.frame:pointerover /template/ ContentPresenter#PART_ContentPresenter\">")
                      && x.Contains("<Style Selector=\"Button.frame:pressed /template/ ContentPresenter#PART_ContentPresenter\">"), true);
                Check($"{file}: txtSpell has no scroll bar", x.Contains("<Setter Property=\"ScrollViewer.VerticalScrollBarVisibility\" Value=\"Disabled\"/>"), true);
                string m = PortSource("src/Mordor.App", file + ".Mdi.cs");
                Check($"{file}: the Spells page's bottom line and the Companions page are fitted to the font",
                      m.Contains("ObjectsForm.FitSpellRow(") && m.Contains("ObjectsForm.FitCompanionPage("), true);
            }
            string of = PortSource("src/Mordor.App", "ObjectsForm.cs");
            Check("txtSpell moves only when the caption would reach it, and keeps its right edge",
                  of.Contains("if (left > boxLeft)") && of.Contains("box.Width = Math.Max(0, right - left);"), true);
            Check("the Companions page's buttons are cmdCompanion's, each a button's width left of the next (FRMCHAR3.BAS:388-391)",
                  PortSource("src/Mordor.App", "CompanionsPage.cs").Contains("Canvas.SetLeft(b, (k.Left - (Slots - 1 - i) * k.Width) / 15.0);")
                  && !of.Contains("ButtonRoom"), true);
        }
        }
        FontFitChecks();

        Console.WriteLine("\nthe town's Party Information is frmParty's form data (MORDOR.EXE RCDATA 44)");
        {
            string exe = System.IO.Path.Combine(dir, "MORDOR.EXE");
            byte[] b = System.IO.File.Exists(exe) ? System.IO.File.ReadAllBytes(exe) : Array.Empty<byte>();
            int U16(int p) => b[p] | (b[p + 1] << 8);
            byte[] form = Array.Empty<byte>();
            if (b.Length > 0x40)
            {
                int ne = U16(0x3C), table = ne + U16(ne + 0x24), shift = U16(table), p = table + 2;
                while (U16(p) != 0)
                {
                    int type = U16(p), count = U16(p + 2);
                    p += 8;
                    for (int i = 0; i < count; i++, p += 12)
                        if (type == 0x800A && U16(p + 6) == 0x802C) form = b.AsSpan(U16(p) << shift, U16(p + 2) << shift).ToArray();
                }
            }
            // a control's rectangle, which follows its caption, its Index (02) and, on a Label, any BackColor (03,
            // a Long) and ForeColor (04): property 05 on a Label, 04 on a CommandButton (as frmCharStat3's buttons have it)
            string Rect(string caption, int index, int rectId = 0x05)
            {
                var pat = new List<byte> { (byte)caption.Length };
                pat.AddRange(System.Text.Encoding.ASCII.GetBytes(caption));
                pat.AddRange(new byte[] { 0x02, (byte)index, 0x00 });
                int at = form.AsSpan().IndexOf(pat.ToArray());
                if (at < 0) return "missing";
                int k = at + pat.Count;
                while (rectId == 0x05 && k < form.Length && form[k] is 0x03 or 0x04) k += 5;   // a Label's BackColor, ForeColor
                if (k + 8 >= form.Length || form[k] != rectId) return "no rectangle";
                int F(int o) => (short)(form[o] | (form[o + 1] << 8));
                return $"{F(k + 1)},{F(k + 3)} {F(k + 5)}x{F(k + 7)}";
            }
            Check("frmParty is captioned Party Information", form.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes("\x11Party Information")) >= 0, true);
            Check("lblName(0)", Rect("Crashland", 0), "30,45 1275x195");
            Check("lblHits(0), which cuts the name off at 1095", Rect("999/999", 0), "1095,45 660x195");
            Check("lblSpells(0)", Rect("200", 0), "1830,45 615x195");
            Check("lblStatus(0)", Rect("PoPaDi", 0), "2475,45 600x195");
            Check("lblOption(0)", Rect("Fight", 0), "3255,45 900x195");
            Check("cmdCommand(0) Switch To", Rect("Switch To", 0, 0x04), "30,1125 945x300");
            Check("cmdCommand(1) Options", Rect("Options", 1, 0x04), "1020,1125 945x300");
            Check("cmdCommand(2) Leave", Rect("Leave", 2, 0x04), "3345,1125 945x300");
            // the town's copy and the dungeon's: the same form
            foreach (var (place, file) in new[] { ("town", "CityWindow"), ("dungeon", "DungeonWindow") })
            {
                string xaml = PortSource("src/Mordor.App", file + ".axaml");
                Check($"the {place}'s header has those columns", xaml.Contains("<Grid ColumnDefinitions=\"72,49,42,53,64\" x:Name=\"PartyHeader\""), true);
                Check($"the {place}'s buttons are 945x300 at 1140 twips down",
                      xaml.Contains("<Setter Property=\"Width\" Value=\"63\"/>") && xaml.Contains("<Setter Property=\"Height\" Value=\"20\"/>")
                      && xaml.Contains("Canvas.Top=\"76\" Focusable=\"False\" Content=\"Leave\""), true);
                string rows = System.Text.RegularExpressions.Regex.Replace(PortSource("src/Mordor.App", file + ".axaml.cs"), @"\s+", " ");
                // the one row builder both windows share
                Check($"the {place}'s rows are PartyRowGrid's",
                      rows.Contains("var row = " + (file == "CityWindow" ? "" : "CityWindow.") + "PartyRowGrid(m, selected,"), true);
            }
            {
                string grid = System.Text.RegularExpressions.Regex.Replace(PortSource("src/Mordor.App", "CityWindow.Mdi.cs"), @"\s+", " ");
                // 195 twips, or a larger Options font's line (PartyRowHeight: the drawing grows with it)
                Check("the rows have the header's columns, 195 twips tall",
                      grid.Contains("PartyColumns = { 72, 49, 42, 53, 64 };") && grid.Contains("Height = PartyRowHeight,")
                      && grid.Contains("ColumnDefinitions = new ColumnDefinitions(string.Join(\",\", PartyColumns.Select(c => (c * gx)"), true);
                // every cell its own width, its text aligned in it, so no row's content moves another's cells
                Check("each label a width of its own: lblHits to 5 short of its column, lblSpells 41, lblStatus 12 short, lblOption 60",
                      grid.Contains("double hitsRight = (PartyColumns[0] + PartyColumns[1] - 5) * gx;") && grid.Contains("Width = hitsRight,")
                      && grid.Contains("Width = 41 * gx,") && grid.Contains("Width = (PartyColumns[3] - 12) * gx,") && grid.Contains("Width = 60 * gx,"), true);
                // lblHits whole at five digits, the name cut PartyNameGap before it or its label (FRMGROUP.BAS:371)
                Check("the name is cut short of lblHits or of its figure, whichever begins first",
                      grid.Contains("double nameRoom = Math.Max(0, Math.Min(PartyColumns[0] * gx, hitsLeft) - PartyNameGap * gx - nameLeft);")
                      && grid.Contains("internal const double PartyNameGap = 3;"), true);
                Check("the drawing is as much wider as the Options font is (exactly the form's with the defaults)",
                      grid.Contains("internal static double PartyGrowthX => UiFonts.FormGrowth(DesignFonts.Party.Pass).X;")
                      && grid.Contains("if (g <= 0 && gx <= 1) return PartyDrawnAt;"), true);
            }
            string partyMdi = PortSource("src/Mordor.App", "CityWindow.Mdi.cs");
            string dungeonMdi = PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs");
            Check("both drawn at the form's client and zoomed both ways (FRMGROUP.BAS:117-124)",
                  partyMdi.Contains("PartyDrawnAt = new(4365 / 15, 1485 / 15)") && partyMdi.Contains("PartyRowHeight => 195 / 15 + PartyLineGrowth;")
                  && partyMdi.Contains("var partyDrawn = FitPartyDrawing(PartyBox, PartyHeader);") && partyMdi.Contains("PartyBox, partyDrawn, grow: true")
                  && dungeonMdi.Contains("var partyDrawn = CityWindow.FitPartyDrawing(PartyBox, PartyHeader);") && dungeonMdi.Contains("PartyBox, partyDrawn, grow: true"), true);
            // Form_Resize ties the height to the width, sizes the Elastic to the client and scales
            // the fonts by the width (FRMGROUP.BAS:117-124, sub07B3, sub07C5): zoomed by the width,
            // each control's top moved with the height - no band beside or under the drawing, and
            // Leave where the form has it, 4 pixels in from the drawing's right
            string frame = PortSource("src/Mordor.App", "MdiFrame.cs");
            Check("a form drawn to its design that keeps its proportions zooms by the width",
                  frame.Contains("ByWidth = grow && WindowLayout.KeepsAspect(which) && panel is ElasticCanvas")
                  && frame.Contains("double s = ByWidth && nw > 0 && !double.IsInfinity(room.Width) ? sx : Math.Min(sx, sy);"), true);
            Check("the Elastic moves each top with the height", frame.Contains("if (!double.IsNaN(top)) y = top * k;"), true);
            foreach (var file in new[] { "CityWindow", "DungeonWindow" })
                Check($"{file}'s Party Information is an Elastic 99 pixels tall",
                      PortSource("src/Mordor.App", file + ".axaml").Contains("<app:ElasticCanvas x:Name=\"PartyBox\" DrawnHeight=\"99\""), true);
            Check("Party Information keeps its proportions (FRMGROUP.BAS:117-122)", WindowLayout.KeepsAspect(GameWindow.Group), true);
        }

        Console.WriteLine("\nmdrmulSwitch in town tells the open services UPDATE, in order (MULTIMOD.BAS:633-637)");
        {
            string multi = PortSource("decompiled-named", "MULTIMOD.BAS");
            int Line(string s) => multi.IndexOf(s, StringComparison.Ordinal);
            int[] orig = { Line("If  fn03BD(frmCityStore) Then frmCityStore!lblCall = \"UPDATE\""),
                           Line("If  fn03BD(frmCityCStore) Then frmCityCStore!lblCall = \"UPDATE\""),
                           Line("If  fn03BD(frmCityMorgue) Then frmCityMorgue!lblCall = \"UPDATE\""),
                           Line("If  fn03BD(frmCityGuild) Then frmCityGuild!lblCall = \"UPDATE\""),
                           Line("If  fn03BD(frmCityBank) Then frmCityBank!lblCall = \"UPDATE\"") };
            Check("the original: Store, Confinement, Morgue, Guild, Bank", orig.All(i => i >= 0) && orig.Zip(orig.Skip(1)).All(p => p.First < p.Second), true);
            Check("the original tells the Seer nothing", multi.Contains("frmSeek!lblCall"), false);
            string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
            string sw = Between(city, "private void SwitchedTo()", "Mordor.Modding.ModHost.Active.CharacterSwitched");
            int[] port = { sw.IndexOf("_storeUpdate?.Invoke();"), sw.IndexOf("_confinementUpdate?.Invoke();"), sw.IndexOf("_morgueUpdate?.Invoke();"),
                           sw.IndexOf("_guildUpdate?.Invoke();"), sw.IndexOf("_bankRefresh?.Invoke();") };
            Check("the port: the same five, in the same order", port.All(i => i >= 0) && port.Zip(port.Skip(1)).All(p => p.First < p.Second), true);
            Check("the port tells the Seer nothing", System.Text.RegularExpressions.Regex.IsMatch(sw, @"_seer\w*\?\.Invoke", System.Text.RegularExpressions.RegexOptions.IgnoreCase), false);
            string guild = PortSource("decompiled-named", "FRMCTYG.BAS");
            string change = Between(guild, "Sub lblCall_Change ()", "End Sub");
            Check("lblCall_Change: m001A back to the current guild, then sub294F, sub2974, sub2962",
                  change.Contains("m001A% = Chars(Me).CurrentGuild") && change.IndexOf("sub294F") is int s294F && s294F >= 0 && s294F < change.IndexOf("sub2974") && change.IndexOf("sub2974") < change.IndexOf("sub2962"), true);
            Check("... and Make Level tested on experience and gold", change.Contains("fn12A1(Chars(Me))") && change.Contains("fn12B2(Chars(Me))"), true);
            string upd = Between(city.Replace("\r\n", "\n"), "Action update = () =>\n        {\n            _guildVisited", "_guildUpdate = update;");
            Check("the port's Guild UPDATE: the current guild, Make Level tested, redrawn",
                  upd.Contains("_guildVisited = _ch.Guild.Guild;") && upd.Contains("_makeTested = true;") && upd.Contains("RedrawGuildInPlace();"), true);
            string inPlace = Between(city, "private void RedrawGuildInPlace()", "private void DrawGuild()");
            Check("... in place: no Form_Load, so no OpenService and no placing again (FRMCTYG.BAS:671-686)",
                  inPlace.Contains("InServiceWindow(GameWindow.Guild, DrawGuild);") && !inPlace.Contains("OpenService(") && !inPlace.Contains("PlaceService(") && !inPlace.Contains("ShowGuild("), true);
            Check("the original's 622 asks about the leader, Chars(m0022), m0022 = 0",
                  multi.Contains("If  Chars(m0022).DungeonLevel > False Or Chars(m0022).DeathState <> False Then") && multi.Contains("Const m0022 = 0 ' &H0%"), true);
            Check("the port's switch sends the UPDATEs on the leader's town branch only",
                  sw.Contains("if (leader.DungeonLevel <= 0 && !leader.IsDead)") && sw.IndexOf("if (leader.DungeonLevel <= 0") < sw.IndexOf("_storeUpdate?.Invoke();"), true);
        }

        Console.WriteLine("\nthe Store with rows a mod hides, and a mod's window brought forward");
        {
            string st = PortSource("src/Mordor.App", "CityWindow.Store.cs");
            string shelf = Between(st, "void RefreshShelf()", "void ShowChosen(");
            string gone = Between(shelf, "if (wasLit != null && lit == null && hiddenNow.Contains(wasLit.Item.Id))", "int keep = top;");
            Check("a lit row a mod now hides empties the choice as FRMCTYS.BAS:92-98 does",
                  gone.Contains("buyName.Text = \"\";") && gone.Contains("selected = null;") && gone.Contains("costLabel.Text = \"Cost\";")
                  && gone.Contains("buyCost.Text = \"\";") && gone.Contains("buyBtn.IsEnabled = false;") && gone.Contains("buyInfoBtn.IsEnabled = false;")
                  && gone.Contains("if (choosing) CancelChoice();"), true);
            Check("the top row is kept by item only when the hiding changed the list's shape",
                  shelf.Contains("if (refiltered && topItem >= 0 && rows.FindIndex(r => r.Item.Id == topItem) is int at && at >= 0) keep = at;"), true);
            Check("with no mod, nothing is hidden: the host lists every row", Mordor.Modding.ModHost.Empty().StoreRowVisible(Character.Create("s", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 }), new Item { Id = 1, Name = "x" }, false), true);
            string mw = PortSource("src/Mordor.App", "ModWindows.cs");
            Check("a mod's window is never activated", mw.Contains(".Activate()"), false);
            Check("a mod's window is a child of the game's frame, never a window of its own, and a click does not make it the active child",
                  !mw.Contains("new Window") && mw.Contains("var child = host.Add(w.Title, body, 0, 0, DefaultWidth, DefaultHeight);") && mw.Contains("child.Activates = false;"), true);
        }

        Console.WriteLine("\nthe morgue changes the party's own copy of a member, which the party's save then writes (FRMMORG.BAS:147-176, 86)");
        {
            string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
            Check("Hire Rescuers resolves the pick to the party's copy", city.Contains("var target = PartyCopyOf(everyone[pick]);"), true);
            Check("Raise Character does too, before the raise is written", city.Contains("var body = PartyCopyOf(here[pick]);"), true);
        }

        Console.WriteLine("\nthe party list's UPDATE after the characters' turns, and a party wiped with bodies still listed (MULTIMOD.BAS:542-553, FRMGROUP.BAS:321-346)");
        {
            string multi = PortSource("decompiled-named", "MULTIMOD.BAS").Replace("\r\n", "\n");
            string[] ml = multi.Split('\n');
            Check("the original: 543 notes anyone dead, 553 sends UPDATE",
                  $"{ml[542].Trim()} | {ml[552].Trim()}", "If  Chars(l00E6).DeathState <> False Then l00E4 = True | If  l00E4 Then frmParty!lblCall = \"UPDATE\"");
            var gMons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
            var foe = gMons.First(m => m.Id != 0);
            Character Mk(string n) => Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var a = Mk("Able"); var b = Mk("Body"); var c = Mk("Also");
            var party = new Party(); party.Members.Add(a); party.Members.Add(b); party.Members.Add(c);
            var fight = new CombatEngine(party, new[] { new CombatMonster { Def = foe, Hits = 30000, MaxHits = 30000 } },
                                         id => items.FirstOrDefault(i => i.Id == id), new Random(1));
            fight.NoteWhoIsAlive();
            b.CurHits = 0; b.IsDead = true;   // as a companion's mishap leaves them (mdrcharSetDead alone)
            int updates = 0;
            fight.ListUpdate = () => { updates++; party.RemoveTheDead(); };
            foreach (var _ in fight.RoundPaced(Array.Empty<Spell>())) { }
            Check("a body after the characters' turns: one UPDATE, and it is off the list", $"{updates} {party.Members.Contains(b)}", "1 False");
            var quiet = new Party(); quiet.Members.Add(Mk("One")); quiet.Members.Add(Mk("Two"));
            var calm = new CombatEngine(quiet, new[] { new CombatMonster { Def = foe, Hits = 30000, MaxHits = 30000 } },
                                        id => items.FirstOrDefault(i => i.Id == id), new Random(1));
            int none = 0; calm.ListUpdate = () => none++;
            calm.NoteWhoIsAlive();
            foreach (var _ in calm.RoundPaced(Array.Empty<Spell>())) { }
            Check("nobody dead before the monsters: no UPDATE from the characters' turns", none, 0);

            string dwsrc = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
            Check("the window hands the engine the UPDATE", dwsrc.Contains("_combat.ListUpdate = CountThenPrune;"), true);
            string wiped = Between(dwsrc, "if (wiped)\n", "// MAINMOD2.BAS:3035 turns the combat timer off");
            int prune = wiped.IndexOf("if (_party.Size > 1) PruneTheDead();", StringComparison.Ordinal);
            int dead = wiped.IndexOf("if (_party.Size == 1) Sub0930(_ch);", StringComparison.Ordinal);
            Check("a wiped party is pruned to one, and that one gets sub0930", prune >= 0 && dead > prune, true);
            // a wipe with three bodies on the list: the prune leaves the last one only
            var all = new Party(); var x = Mk("X"); var y = Mk("Y"); var z = Mk("Z");
            foreach (var m in new[] { x, y, z }) { m.CurHits = 0; m.IsDead = true; m.Stoned = true; all.Members.Add(m); }
            all.RemoveTheDead();
            Check("three statues: the UPDATE leaves the last", string.Join(",", all.Members.Select(m => m.Name)), "Z");
        }

        Console.WriteLine("\na stat item's raise is looked at for the Hall even when it meets race max + 5 (MAINMOD2.BAS:6213-6219)");
        {
            string main = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
            string[] lines = main.Split('\n');
            string L(int n) => n - 1 < lines.Length ? lines[n - 1].Trim() : "";
            Check("6213-6218: the raise, clamped or announced", L(6213).StartsWith("Chars(pv0850).Stats(l0854) = Chars(pv0850).Stats(l0854) + Items(l085A).M571E(l0854)")
                  && L(6216) == "Else" && L(6218) == "End If", true);
            Check("6219: mdrhalCheckRecord after that End If", L(6219), "mdrhalCheckRecord l0854, pv0850, CCur(Chars(pv0850).Stats(l0854))");
            Check("6205-6211: the reversed branch checks no record",
                  L(6205).StartsWith("If  l085C = True") && L(6212) == "Else" && !string.Join("\n", lines.Skip(6204).Take(7)).Contains("mdrhalCheckRecord"), true);

            var apply = typeof(ItemUse).GetMethod("ApplyStatMods", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-statrec-" + Guid.NewGuid().ToString("N")[..8]);
            System.IO.Directory.CreateDirectory(tmp);
            try
            {
                var c = Character.Create("Capper", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
                int ceiling = c.RaceDef.Max[0] + 5;
                c.Stats[0] = ceiling - 1;
                var potion = new Item { Name = "Test Potion", StatsMod = new short[] { 2, 0, 0, 0, 0, 0, 0 } };
                var hall = new HallRecords();
                var said = (List<string>)apply.Invoke(null, new object?[] { c, potion, false, hall, tmp, true })!;
                Check("clamped to race max + 5", c.Stats[0], ceiling);
                Check("no raise line when clamped", said.Any(s => s.Contains("Strength", StringComparison.OrdinalIgnoreCase) && !s.Contains("record")), false);
                Check("the Strength record is taken at the ceiling", $"{hall.For(HallRecords.Kind.Strength).Holder} {hall.For(HallRecords.Kind.Strength).Value}", $"Capper {ceiling}");
                var c2 = Character.Create("Lowerer", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
                c2.Stats[1] = 15;
                var hall2 = new HallRecords();
                apply.Invoke(null, new object?[] { c2, new Item { Name = "Opposed", StatsMod = new short[] { 0, 2, 0, 0, 0, 0, 0 } }, true, hall2, tmp, true });
                Check("reversed: lowered, and no record looked at", $"{c2.Stats[1]} {hall2.For(HallRecords.Kind.Intelligence).Holder}", "13 ");
            }
            finally { try { System.IO.Directory.Delete(tmp, true); } catch { } }
        }

        Console.WriteLine("\nthe dead screen: Wait for Rescue below ground, Visit the Morgue at level 0, nothing in the rock (MAINMOD2.BAS:3045-3055, FRMDUNM.BAS:144-196)");
        {
            string main = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
            string[] ml = main.Split('\n');
            string L(int n) => n - 1 < ml.Length ? ml[n - 1].Trim() : "";
            Check("the original: 3045-3048 caption cmdWait by the body's level",
                  $"{L(3045)} | {L(3046)} | {L(3048)}",
                  "If  Chars(pv04B4).DungeonLevel > False Then | frmDungeonMain!cmdWait.Caption = \"&Wait for Rescue\" | frmDungeonMain!cmdWait.Caption = \"&Visit the Morgue\"");
            Check("the original: 3050-3051 show it only out of the rock", $"{L(3050)} | {L(3051)}",
                  "If  Chars(pv04B4).DeathState <> mc00B6 Then | frmDungeonMain!cmdWait.Visible = True");
            string dunm = PortSource("decompiled-named", "FRMDUNM.BAS").Replace("\r\n", "\n");
            string click = Between(dunm, "Sub cmdWait_Click ()", "End Sub");
            Check("the original's cmdWait_Click: the find, the body to town, then sub0930 Me again",
                  click.Contains("\"You have been found after \" + CStr(l007E) + \" day(s)!\", mc0098, \"Wait for Rescue\"")
                  && click.Contains("Chars(Me).DungeonLevel = 0") && click.TrimEnd().EndsWith("sub0930 Me\n    End If", StringComparison.Ordinal), true);

            string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
            string dead = Between(city, "private void ShowDead()", "private void CityArrivalAfterRaise()");
            string rock = Between(dead, "if (_ch.CauseOfDeath == Rules.DeathRock)", "else if (_ch.DungeonLevel > 0)");
            string below = Between(dead, "else if (_ch.DungeonLevel > 0)", "long cost =");
            string atZero = Between(dead, "long cost =", "ServicePage.Content = sp;");
            Check("dead below ground: Wait for Rescue is offered", below.Contains("new Button { Content = \"_Wait for Rescue\" }") && below.Contains("await WaitForRescue(wait)"), true);
            Check("dead below ground: no Hire Rescuers", below.Contains("RescuersButton(") || dead.Contains("RescuersButton(") || dead.Contains("Content = \"Hire Rescuers\""), false);
            Check("the port-only AddRescuers is gone", city.Contains("AddRescuers"), false);
            Check("dead at level 0: Visit the Morgue", atZero.Contains("Content = \"_Visit the Morgue\""), true);
            Check("dead in the rock: no button at all", rock.Contains("new Button"), false);
            string wait = Between(city, "private async System.Threading.Tasks.Task WaitForRescue(Button btn)", "private Character PartyCopyOf(");
            Check("the city's wait uses Rescue's rules, the same as the dungeon's",
                  wait.Contains("Rescue.DaysUntilFound(") && wait.Contains("Rescue.EasyToFind(") && wait.Contains("Rescue.CarryHome(_ch, days)")
                  && wait.Contains("Rescue.TurnOverTown(") && wait.Contains("await Tell(\"Wait for Rescue\", Rescue.Found(days))"), true);
            string arrive = Between(city, "private void ArriveDeadSub0930(Character? whom = null)", "private Character PartyCopyOf(");
            Check("and ends in sub0930 again: its head, then the dead screen with its boxes, now at level 0",
                  wait.IndexOf("ArriveDeadSub0930();", StringComparison.Ordinal) is int ad && ad > wait.IndexOf("Rescue.CarryHome(", StringComparison.Ordinal)
                  && arrive.IndexOf("(whom ?? _ch).Sub0930Head(Lookup);", StringComparison.Ordinal) is int hd && hd >= 0
                  && arrive.IndexOf("_arrivedDead = true;", StringComparison.Ordinal) > hd
                  && arrive.IndexOf("ShowDead();", StringComparison.Ordinal) > hd, true);
            string dunSrc = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
            string dwait = Between(dunSrc, "private async void WaitForRescue()", "Close();");
            Check("the dungeon's wait uses the same shared rules",
                  dwait.Contains("Rescue.EasyToFind(") && dwait.Contains("Rescue.CarryHome(_ch, days)") && dwait.Contains("Rescue.TurnOverTown("), true);

            // the logic: a body on level 3 waited out is in town, aged by the days, M59BB -1
            var body = Character.Create("Waiter", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            body.IsDead = true; body.DungeonLevel = 3; body.X = 5; body.Y = 7; body.Area = 12;
            int age = body.Age;
            int days = Rescue.DaysUntilFound(new Random(7), body.DungeonLevel, body.Area ?? 0, false);
            Rescue.CarryHome(body, days);
            Check("waited out: in town, M59BB -1, aged by the days, still dead",
                  $"{body.DungeonLevel} {body.Area} {body.Age - age == days} {days > 0} {body.IsDead}", "0 -1 True True True");
            Check("level 1, area 18 or less: found within a hundred-to-one ceiling", Rescue.DaysUntilFound(new ZeroRandom(), 1, 5, false), 1);

            // every service window, the dead screen included, moves by its title bar
            string mdiSrc = PortSource("src/Mordor.App", "CityWindow.Mdi.cs");
            Check("the service and dead-screen windows are movable", mdiSrc.Contains("_serviceChild.Movable = true;") && !mdiSrc.Contains("_serviceChild.Movable = _serviceWhich"), true);
        }

        Section(() => DeathCleanupChecks(dir));
    }

    /// <summary>
    /// Every death the original follows with <c>sub0930</c> runs its head - companions, carry and
    /// <c>sub048C</c> (MAINMOD2.BAS:3026-3033, 4359-4373) - and so does every arrival of a dead
    /// character (sub14E0, 3181-3182; cmdWait_Click, FRMDUNM.BAS:194). A body raised afterwards
    /// cannot be poisoned. The raises themselves (fn16B5, raiseComplications, the own-body morgue)
    /// clear nothing, so a body that mdrcharSetDead alone left poisoned and that is raised by a
    /// join (MAINMOD2.BAS:3478-3499, no sub0930) keeps it, as in the original.
    /// </summary>
    private static void DeathCleanupChecks(string dir)
    {
        Console.WriteLine("\nthe death cleanup: sub0930's head and sub048C on every path that has them (MAINMOD2.BAS:3026-3033, 4359-4373)");
        string main = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        string sub048C = Between(main, "Sub sub048C (ByVal pv067E As Integer)", "End Sub");
        Check("the original: sub048C undoes paralysis, then zeroes Ailments 0-6 and the spell resistances, then recomputes",
              sub048C.Contains("If  Chars(pv067E).Ailments(mc01EC) <> False Then sub0D1D pv067E, 1, 0")
              && new[] { "m01FC", "mc01FE", "mc01EC", "mc0206", "mc020C", "mc0210", "mc0214" }.All(k => sub048C.Contains($"Chars(pv067E).Ailments({k}) = 0"))
              && sub048C.Contains("Chars(pv067E).SpellResistances(l0680) = 0") && sub048C.Contains("recomputeEffects pv067E"), true);
        Check("the original: those seven are Ailments 0 to 6",
              new[] { "Const m01FC = 0 ", "Const mc01FE = 1 ", "Const mc01EC = 2 ", "Const mc0206 = 3 ", "Const mc020C = 4 ", "Const mc0210 = 5 ", "Const mc0214 = 6 " }.All(main.Contains), true);
        Check("the original: sub0930 runs it at 3033, before its party exit",
              Between(main, "Sub sub0930 (ByVal pv04B4 As Integer)", "End Sub") is string s0930
              && s0930.IndexOf("sub048C pv04B4", StringComparison.Ordinal) is int at048 && at048 > 0
              && s0930.IndexOf("frmParty!lblCall = \"UPDATE\"", StringComparison.Ordinal) > at048, true);
        Check("the original: a dead character loaded runs sub0930 (sub14E0)",
              Between(main, "Sub sub14E0 ()", "End Sub").Contains("If  Chars(Me).DeathState <> False Then\n        sub0930 Me"), true);
        string fn16B5 = Between(main, "Function fn16B5 (ByVal pv00AA As Integer) As Integer", "End Function");
        string rc = Between(main, "Function raiseComplications (ByVal pv02D4 As Integer) As Integer", "End Function");
        Check("the original: fn16B5 and raiseComplications touch no ailment", fn16B5.Contains("Ailments(") || rc.Contains("Ailments("), false);

        Character Poisoned(string name)
        {
            var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 17, 18, 18, 17, 18, 18 });
            c.Poisoned = true; c.PoisonLevel = 7;
            c.Diseased = true; c.DiseaseLevel = 4;
            c.SpellResist[0] = 30;
            c.CarryingCharacter = "Somebody";
            return c;
        }
        string Ailing(Character c) =>
            $"{c.Poisoned} {c.PoisonLevel} {c.Diseased} {c.DiseaseLevel} {c.Paralysed} {c.SpellResist.Sum()} {c.CarryingCharacter == ""}";
        const string clean = "False 0 False 0 False 0 True";

        // the core routine itself; stone is what the body died of, not an ailment, and stays
        {
            var c = Poisoned("Head");
            c.ParalysisLevel = 3;
            c.Stoned = true;
            c.Sub0930Head(_ => null);
            Check("Sub0930Head: poison, disease, paralysis, resistances, carry all gone", Ailing(c), clean);
            Check("and the stone stays (DeathState 2 is not one of Ailments 0-6)", c.Stoned, true);
        }

        var beasts = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var wares = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var brute = beasts.Where(m => m.Id != 0 && m.SpecialAttackFlags == 0 && m.LevelFound >= 10)
                          .OrderByDescending(m => m.LevelFound).First();

        // a monster's blow: sub0E85's mdrcharSetDead, then sub0A64's sub0930 (MULTIMOD.BAS:362-363)
        {
            string after = "never died";
            for (int i = 0; i < 400; i++)
            {
                var c = Poisoned("Blow");
                c.MaxHits = 1; c.CurHits = 1;
                var fight = new CombatEngine(c, new[] { new CombatMonster { Def = brute, Hits = 1000 } },
                                             id => wares.FirstOrDefault(x => x.Id == id), new Random(9300 + i));
                fight.UseMonsterTable(beasts);
                foreach (var _ in fight.MonsterAttacks()) { }
                if (!c.IsDead) continue;
                after = Ailing(c);
                break;
            }
            Check("a poisoned character killed by a monster's blow is cleaned", after, clean);
        }

        // a body already at nought that a group picks: sub0930 runs for it all the same
        {
            var c = Poisoned("Picked");
            c.CurHits = 0; c.IsDead = true;
            var fight = new CombatEngine(c, new[] { new CombatMonster { Def = brute, Hits = 1000 } },
                                         id => wares.FirstOrDefault(x => x.Id == id), new Random(1));
            fight.UseMonsterTable(beasts);
            foreach (var _ in fight.MonsterAttacks()) { }
            Check("a poisoned body a group picks at nought is cleaned (MULTIMOD.BAS:362-363)", Ailing(c), clean);
        }

        // the window's paths: every Kill with sub0930, Sub0930 itself, and every dead arrival
        string dw = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
        string kill = Between(dw, "private void Kill(Character who, string why, bool sub0930 = true)", "Audio.PlayOnNextChannel(\"DIE\");");
        Check("Kill: sub0930's head whenever sub0930 follows", kill.Contains("if (sub0930) who.Sub0930Head(Lookup);"), true);
        Check("Sub0930: its head before the save",
              Between(dw, "private void Sub0930(Character who)", "private void Kill(") is string s && s.IndexOf("who.Sub0930Head(Lookup);", StringComparison.Ordinal) is int h
              && h >= 0 && s.IndexOf("who.Save(", StringComparison.Ordinal) > h, true);
        Check("only the chest trap's Kill has no sub0930 (MONSMOD.BAS:3654)",
              System.Text.RegularExpressions.Regex.Matches(dw, @"Kill\([^;]*sub0930: false\)").Count, 1);
        string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        Check("every dead arrival in the city is ArriveDeadSub0930 - the load, the city's wait, the dungeon's return",
              System.Text.RegularExpressions.Regex.Matches(city, @"ArriveDeadSub0930\(\);").Count, 3);
        Check("and nothing sets _arrivedDead elsewhere", System.Text.RegularExpressions.Regex.Matches(city, @"_arrivedDead = true;").Count, 1);
        Check("the join raise has no sub0930, as MAINMOD2.BAS:3478-3499",
              Between(city, "private async void AskToJoin()", "private GameOptions _options").Contains("Sub0930Head"), false);

        // a raise after the arrival: never poisoned; a raise alone clears nothing, as fn16B5
        {
            var race = RaceData.All[0];
            var body = Poisoned("Raised");
            body.CurHits = 0; body.IsDead = true; body.DungeonLevel = 0;
            body.Sub0930Head(_ => null);                               // the arrival (sub14E0 / cmdWait)
            var o = Raising.Resolve(new Random(3), body, race, Raising.ByMorgue, 100, _ => null);
            Check("a body arriving dead and then raised is not poisoned", $"{o.Raised} {Ailing(body)}", $"True {clean}");

            var joined = Poisoned("Joined");
            joined.CurHits = 0; joined.IsDead = true;
            var o2 = Raising.Resolve(new Random(3), joined, race, Raising.ByMorgue, 100, _ => null);
            Check("fn16B5 alone (a join) clears nothing: a body left poisoned stays so",
                  $"{o2.Raised} {joined.Poisoned} {joined.PoisonLevel}", "True True 7");
        }
    }

    /// <summary>
    /// The text between a routine's header and its End, for reading one Sub in isolation.
    /// Either marker missing is a failure: a missing start would hand back nothing, and a missing
    /// end the rest of the file, and either lets a check pass on text it was never meant to read
    /// (a renamed end marker once made a "does not contain" check read the whole file after it).
    /// The end is looked for after the start marker, so an end that is part of the start does not
    /// cut the text to nothing.
    /// </summary>
    private static string Between(string src, string from, string to)
    {
        int a = src.IndexOf(from, StringComparison.Ordinal);
        if (a < 0)
        {
            // the routine was not found: an empty text would pass every "does not contain" check
            Console.WriteLine($"  FAIL  not found in the source read: {from}");
            _failed++;
            return "";
        }
        int b = src.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        if (b < 0)
        {
            // the end was not found: the rest of the file would pass most "contains" checks, and
            // an empty text every "does not contain" one, so neither is handed back
            Console.WriteLine($"  FAIL  end marker not found after \"{from}\": {to}");
            _failed++;
            return "";
        }
        return src.Substring(a, b - a);
    }

    /// <summary>Width and height out of a PNG header, without decoding the image.</summary>
    private static (int W, int H) PngSize(string path)
    {
        using var f = System.IO.File.OpenRead(path);
        var head = new byte[24];
        if (f.Read(head, 0, 24) < 24) return (0, 0);
        int Be(int at) => (head[at] << 24) | (head[at + 1] << 16) | (head[at + 2] << 8) | head[at + 3];
        return (Be(16), Be(20));
    }
}

/// <summary>
/// A Random that always rolls zero, so a formula built on Rnd can be checked at its maximum.
/// Rules.SpellDamage takes its randomness through NextDouble, which Random declares virtual.
/// </summary>
internal sealed class ZeroRandom : Random
{
    public override double NextDouble() => 0;
    public override int Next(int max) => 0;
    public override int Next(int min, int max) => min;
}
