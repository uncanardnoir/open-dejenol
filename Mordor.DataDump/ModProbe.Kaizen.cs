using System.Reflection;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The Kaizen mod and the groundwork under it (docs/MODDING.md): the two ModActions moments and
/// their DungeonContext, the list settings (mod.json tables), Rules.SpellHealAverage, the heal-out's
/// greedy choice and its end, the per-character buff lists (kaizen.json, what the Kaizen window
/// offers, the run in party order and its skipping), and ctx.Game. The casts here go to a stand-in for the
/// dungeon's cast path, which records them; the path itself is the dungeon window's, unchanged for a
/// cast by hand, and is not reachable headless.
/// </summary>
public static partial class ModProbe
{
    /// <summary>The dungeon as a hook sees it, with the casts recorded instead of thrown.</summary>
    private sealed class FakeDungeon
    {
        public readonly List<string> Said = new();
        public readonly List<string> Calls = new();
        public bool Monsters;
        public Func<Character, Spell, Character?, bool>? OnCast;
        /// <summary>What the gate said to the last command (the stand-in's OnCast may set it).</summary>
        public RefusalKind? Refusal;
        /// <summary>A cast that has not finished yet, for an awaited hook: the casts wait on it.</summary>
        public Task<bool>? Pending;
        public DungeonContext? Last;

        public DungeonContext Make(Party p, List<Spell> spells, Func<short, Item?> item) => Last = new DungeonContext(
            p, spells, item, () => Monsters,
            (c, s, t) =>
            {
                Refusal = null;
                Calls.Add($"cast {c.Name} {s.Name.Trim()} {t?.Name ?? "-"}");
                return Pending ?? Task.FromResult(OnCast?.Invoke(c, s, t) ?? true);
            },
            (c, slot, t) => { Refusal = null; Calls.Add($"use {c.Name} {item(slot.ItemId)?.Name.Trim()} {t?.Name ?? "-"}"); return Task.FromResult(true); },
            Said.Add, () => Refusal);
    }

    private static Spell Clone(Spell s, string name, int damage2, int effect, int u4) => new()
    {
        Name = name, Id = s.Id, Category = s.Category, Level = s.Level, U4 = (short)u4, Damage1 = s.Damage1,
        Damage2 = (short)damage2, SpecialEffect = s.SpecialEffect, Effect = (short)effect,
        Required = s.Required.ToArray(), ResistIndex = s.ResistIndex, AffectGroup = s.AffectGroup, AffectMonster = s.AffectMonster,
    };

    private static Character Member(string name, IEnumerable<GuildDef> guilds, int level)
    {
        var c = MakeChar(name);
        c.Guilds = guilds.Select(g => new GuildMembership { Guild = g.Name, Level = level }).ToList();
        if (c.Guilds.Count == 0) c.Guilds.Add(new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 });
        c.Stats = new[] { 25, 25, 25, 25, 25, 25 };
        c.MaxHits = c.CurHits = 100;
        c.MaxSpellPoints = c.CurSpellPoints = 5000;
        return c;
    }

    private static void KaizenChecks(string dir, string scratch, string bundled, List<Item> items, Func<short, Item?> lookup)
    {
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));

        // ---- the manifest, and nothing with the mod off ----
        Console.WriteLine("Kaizen: the manifest, and mods off:");
        var log = new List<string>();
        var off = Host(Path.Combine(scratch, "kaizen-off.ini"), log, bundled);
        var kz = off.Discovered.FirstOrDefault(d => d.Id == "Kaizen");
        Check("Kaizen manifest found, no problem", kz != null && kz.Problem == null, true);
        Check("its two toggles, both off by default", string.Join(",", kz?.Manifest.Settings.Select(t => $"{t.Key}={t.Default}") ?? Array.Empty<string>()), "BuffOnEntry=False,HealBeforeTown=False");
        Check("version 1.2.0 (the lists edited by drag and drop)", kz?.Manifest.Version, "1.2.0");
        Check("no table for the Mods page: the buff lists are in the Kaizen window", kz?.Manifest.Tables.Count, 0);
        Check("QoL has no table", off.Discovered.First(d => d.Id == "QoL").Manifest.Tables.Count, 0);
        Check("not enabled by default", off.IsEnabled("Kaizen"), false);
        int made = 0;
        var t0 = off.EnteredDungeonFromTown(() => { made++; return null!; });
        var t1 = off.LeavingByStairs(() => { made++; return null!; });
        Check("mods off: both moments done at once, no context made", $"{t0.IsCompleted} {t1.IsCompleted} {made}", "True True 0");
        var empty = ModHost.Empty();
        Check("empty host: the same", $"{empty.EnteredDungeonFromTown(() => null!).IsCompleted} {empty.LeavingByStairs(() => null!).IsCompleted}", "True True");

        // ---- list settings ----
        Console.WriteLine("list settings (mod.json tables):");
        var rows = new List<string[]> { new[] { "Ann", "Heal|All; x", "Bo\\b" }, new[] { "", "", "" }, new[] { "Cy", "Protection", "" } };
        string line = ModSettings.EncodeRows(rows);
        Check("one line", line.Contains('\n') || line.Contains('\r'), false);
        Check("round trip, with | ; and \\ in cells", string.Join(" / ", ModSettings.DecodeRows(line).Select(r => string.Join(",", r))), "Ann,Heal|All; x,Bo\\b / ,, / Cy,Protection,");
        Check("nothing: no rows", ModSettings.DecodeRows("").Count, 0);

        // ---- Rules.SpellHealAverage ----
        Console.WriteLine("Rules.SpellHealAverage (the expected value of Rules.SpellHeal):");
        // base 10, power 1, SL 1: mod1 = 10, h = 5, Int(U * 5) is 0..4 each a fifth: heals 9, 8, 7, 6, 5
        Check("base 10 at SL 1, by hand", Rules.SpellHealAverage(10, 1, 1).ToString("0.######"), "7");
        foreach (var (hb, hp, hsl) in new[] { (10, 1, 1), (8, 3, 12), (25, 2, 40), (3, 1, 7), (60, 5, 150) })
        {
            var rng = new Random(1234 + hb + hsl);
            const int n = 200_000;
            double sum = 0;
            for (int i = 0; i < n; i++) sum += Rules.SpellHeal(rng, hb, hp, hsl);
            double sampled = sum / n, exact = Rules.SpellHealAverage(hb, hp, hsl);
            Check($"base {hb} power {hp} SL {hsl}: exact {exact:0.00} against {n:N0} rolls {sampled:0.00}", Math.Abs(sampled - exact) < 0.01 * exact + 0.05, true);
        }

        // ---- Kaizen on ----
        Console.WriteLine("Kaizen enabled:");
        string ini = Path.Combine(scratch, "kaizen.ini");
        new ModSettingsStore(ini).SetEnabled("Kaizen", true);
        var on = Host(ini, log, bundled);
        var lm = on.Loaded.FirstOrDefault(l => l.Id == "Kaizen");
        Check("Kaizen loaded, not faulted", lm is { Faulted: false }, true);
        var asm = lm!.Instance!.GetType().Assembly;
        var healOut = asm.GetType("Mordor.Mod.Kaizen.HealOut")!;
        var buffs = asm.GetType("Mordor.Mod.Kaizen.Buffs")!;
        var listsType = asm.GetType("Mordor.Mod.Kaizen.BuffLists")!;
        var rowType = asm.GetType("Mordor.Mod.Kaizen.BuffRow")!;
        var kindType = asm.GetType("Mordor.Mod.Kaizen.BuffKind")!;
        // the mod's own lists (kaizen.json beside this run's settings.ini), and lists of the probe's own
        object modLists = lm.Instance.GetType().GetProperty("Lists")!.GetValue(lm.Instance)!;
        object NewLists(string? path) => Activator.CreateInstance(listsType, new object?[] { path, null })!;
        void SetList(object lists, string who, params (string Kind, string Name, string Target)[] rs)
        {
            var arr = Array.CreateInstance(rowType, rs.Length);
            for (int i = 0; i < rs.Length; i++)
                arr.SetValue(Activator.CreateInstance(rowType, Enum.Parse(kindType, rs[i].Kind), rs[i].Name, rs[i].Target), i);
            listsType.GetMethod("Set")!.Invoke(lists, new object[] { who, arr });
        }
        string ListOf(object lists, string who) => string.Join(" / ", ((System.Collections.IEnumerable)listsType.GetMethod("For")!.Invoke(lists, new object[] { who })!)
            .Cast<object>().Select(r => $"{rowType.GetProperty("Kind")!.GetValue(r)}:{rowType.GetProperty("Name")!.GetValue(r)}>{rowType.GetProperty("Target")!.GetValue(r)}"));
        string AsksOf((string Kind, string Name, string Target) r, Character c) =>
            buffs.GetMethod("AsksTarget")!.Invoke(null, new object[] { Activator.CreateInstance(rowType, Enum.Parse(kindType, r.Kind), r.Name, r.Target)!, c, (IReadOnlyList<Spell>)spells, lookup })?.ToString() ?? "";
        void RunLists(FakeDungeon f, Party p, object lists) =>
            ((Task)buffs.GetMethod("Run", new[] { typeof(DungeonContext), listsType })!.Invoke(null, new object[] { f.Make(p, spells, lookup), lists })!).GetAwaiter().GetResult();

        var healGuilds = GuildData.All.Where(g => g.SpellRate.Length > Spellbook.Healing && g.SpellRate[Spellbook.Healing] > 0)
                                      .OrderByDescending(g => g.SpellMax[Spellbook.Healing]).ToList();
        var noHealGuild = GuildData.All.First(g => g.SpellRate.Length <= Spellbook.Healing || g.SpellRate[Spellbook.Healing] <= 0);
        Check("a guild that heals, and one that does not", healGuilds.Count > 0 && noHealGuild != null, true);

        // the hooks with their toggles off: nothing is cast
        var fake = new FakeDungeon();
        var fighter = Member("Fia", new[] { noHealGuild! }, 50);
        var healer = Member("Hal", healGuilds, 400);
        var party = new Party(fighter, healer);
        fighter.CurHits = 40;
        SetList(modLists, "Hal", ("Spell", "Nothing At All", ""));
        on.EnteredDungeonFromTown(() => fake.Make(party, spells, lookup)).GetAwaiter().GetResult();
        on.LeavingByStairs(() => fake.Make(party, spells, lookup)).GetAwaiter().GetResult();
        Check("toggles off: nothing cast, nothing said", $"{fake.Calls.Count} {fake.Said.Count}", "0 0");
        Check("... and the context closed after the hook", fake.Last?.IsOpen, false);
        Check("... a closed context casts nothing", fake.Last!.Cast(healer, spells[0], fighter).Result, false);

        // ---- the heal choice ----
        Console.WriteLine("Kaizen heal-out: the greedy choice:");
        object? Choose(IReadOnlyList<Character> m, Func<Character, IReadOnlyList<Spell>> known, Func<Character, Spell, bool>? excluded = null) =>
            healOut.GetMethod("Choose")!.Invoke(null, new object[] { m, known, lookup, excluded ?? ((_, _) => false) });
        string Said(object? h)
        {
            if (h == null) return "none";
            var t = h.GetType();
            return $"{((Character)t.GetProperty("Caster")!.GetValue(h)!).Name} {((Spell)t.GetProperty("Spell")!.GetValue(h)!).Name.Trim()} on {((Character)t.GetProperty("Target")!.GetValue(h)!).Name}";
        }
        bool IsHeal(Spell s) => (bool)healOut.GetMethod("IsHeal")!.Invoke(null, new object[] { s })!;
        var knownHeals = Spellbook.Known(healer, spells).Where(IsHeal).ToList();
        Console.WriteLine("  (the heals " + healer.Guilds[0].Guild + " and the rest at 400 know: " + string.Join(", ", knownHeals.Select(s => $"{s.Name.Trim()} {s.Damage2}/{s.SpecialEffect} cost {Spellbook.Cost(s, healer)}{(SpellEffect.IsParty(s) ? " party" : "")}")) + ")");
        var single = knownHeals.First(s => !SpellEffect.IsParty(s) && CastGate.Unfit(healer, s, 0, true, lookup) == null);
        int baseU4 = Math.Max(1, (int)single.U4);
        var small = Clone(single, "Small", 5, single.Effect, baseU4);
        var big = Clone(single, "Big", 20, single.Effect, baseU4);
        var dear = Clone(single, "Dear", 22, single.Effect, 30000);
        var mass = Clone(single, "Mass", 10, SpellEffect.Of(single) + SpellEffect.PartyOffset, baseU4);
        Check("Big and Dear cost what they should", Spellbook.Cost(dear, healer) > Spellbook.Cost(big, healer), true);
        IReadOnlyList<Spell> Book(Character c) => c == healer ? new List<Spell> { small, dear, big, mass } : Spellbook.Known(c, spells);

        var a = Member("Ann", new[] { noHealGuild! }, 10);
        var b = Member("Bob", new[] { noHealGuild! }, 10);
        var dead = Member("Dee", new[] { noHealGuild! }, 10);
        var poor = Member("Pip", healGuilds, 400);
        var four = new List<Character> { a, b, dead, poor, healer };
        void Hurt(int ann, int bob, int pip, int hal) { a.CurHits = 100 - ann; b.CurHits = 100 - bob; poor.CurHits = 100 - pip; healer.CurHits = 100 - hal; }
        dead.IsDead = true; dead.CurHits = 0;
        poor.CurSpellPoints = 0;
        Hurt(0, 0, 0, 0);
        Check("nobody missing hits: nothing", Said(Choose(four, Book)), "none");
        Check("... the dead member, missing everything, is not a target", healOut.GetMethod("Target")!.Invoke(null, new object[] { four }), null);
        Hurt(0, 30, 0, 0);
        Check("one hurt: the target; no heals, no points, dead: passed over; Big, not Mass for one",
              Said(Choose(four, Book)), "Hal Big on Bob");
        Hurt(10, 30, 0, 0);
        Check("two hurt: the one missing the most is the target", Said(Choose(four, Book)).EndsWith("on Bob"), true);
        Hurt(30, 30, 0, 0);
        Check("a tie: the first in party order", Said(Choose(four, Book)).EndsWith("on Ann"), true);
        Hurt(30, 30, 20, 0);
        Check("three hurt: Mass, counted for each of them", Said(Choose(four, Book)), "Hal Mass on Ann");
        Check("... Big's average against Mass's x3", (double)healOut.GetMethod("Average")!.Invoke(null, new object[] { healer, mass, 3 })! > (double)healOut.GetMethod("Average")!.Invoke(null, new object[] { healer, big, 3 })!, true);
        Hurt(0, 30, 0, 0);
        Check("Big and Mass excluded (refused once): the better per point of Small and Dear",
              Said(Choose(four, Book, (c, s) => s == big || s == mass)), Rules.SpellHealAverage(22, dear.SpecialEffect + 1, Rules.SpellLevel(healer.Guild.Level)) / Spellbook.Cost(dear, healer)
                  > Rules.SpellHealAverage(5, small.SpecialEffect + 1, Rules.SpellLevel(healer.Guild.Level)) / Spellbook.Cost(small, healer)
                  ? "Hal Dear on Bob" : "Hal Small on Bob");
        poor.CurSpellPoints = 5000;
        Check("the first member who can afford a heal casts it", Said(Choose(four, Book)).StartsWith("Pip "), true);
        poor.CurSpellPoints = 0;
        healer.CurSpellPoints = Spellbook.Cost(big, healer) - 1;
        Check("what the caster cannot afford is passed over", Said(Choose(four, Book)), Spellbook.Cost(small, healer) <= healer.CurSpellPoints ? "Hal Small on Bob" : "none");
        healer.CurSpellPoints = 5000;
        healer.Stats = new[] { 1, 1, 1, 1, 1, 1 };
        bool unfit = CastGate.Unfit(healer, big, 0, true, lookup) != null;
        Check("a heal the caster's stats cannot cast (CastGate.Unfit) is passed over", Said(Choose(four, Book)), unfit ? "none" : "Hal Big on Bob");
        healer.Stats = new[] { 25, 25, 25, 25, 25, 25 };

        // ---- the loop, and its end ----
        Console.WriteLine("Kaizen heal-out: the loop ends:");
        string RunHeal(FakeDungeon f, Party p)
        {
            f.Calls.Clear(); f.Said.Clear();
            var task = (Task)healOut.GetMethod("Run")!.Invoke(null, new object[] { f.Make(p, spells, lookup) })!;
            task.GetAwaiter().GetResult();
            return $"{f.Calls.Count}";
        }
        int cap = (int)healOut.GetField("MaxCasts")!.GetValue(null)!;
        // the stand-in heals as the game would: the points, then the target (or all) by the average
        var real = new FakeDungeon
        {
            OnCast = (c, s, t) =>
            {
                int cost = Spellbook.Cost(s, c);
                if (cost > c.CurSpellPoints) return false;
                c.CurSpellPoints -= cost;
                int heal = Math.Max(1, (int)Rules.SpellHealAverage(s.Damage2, s.SpecialEffect + 1, Rules.SpellLevel(c.Guild.Level)));
                foreach (var m in SpellEffect.IsParty(s) ? new List<Character>(new Party(a, b, poor, healer).Members) : new List<Character> { t! })
                    m.CurHits = Math.Min(m.MaxHits, m.CurHits + heal);
                return true;
            },
        };
        var hurtParty = new Party(a, b, poor, healer);
        foreach (var m in hurtParty.Members) { m.MaxHits = 400; m.CurHits = 37; }
        poor.CurSpellPoints = 0; healer.CurSpellPoints = 5000;
        string casts = RunHeal(real, hurtParty);
        Check("plenty of points: everybody healed to full", hurtParty.Members.All(m => m.CurHits == m.MaxHits), true);
        Check($"... in some casts ({casts}), fewer than the cap", int.Parse(casts) is > 0 && int.Parse(casts) < cap, true);
        Check("... every cast through the context, the target named for a single heal", real.Calls.All(c => c.StartsWith("cast Hal ")), true);
        foreach (var m in hurtParty.Members) m.CurHits = 37;
        healer.CurSpellPoints = Spellbook.Known(healer, spells).Where(IsHeal).Min(s => Spellbook.Cost(s, healer)) * 2;
        RunHeal(real, hurtParty);
        Check("few points: stops when nobody can afford a heal", Spellbook.Known(healer, spells).Where(IsHeal).All(s => Spellbook.Cost(s, healer) > healer.CurSpellPoints), true);
        Check("... with hits still missing", hurtParty.Members.Any(m => m.CurHits < m.MaxHits), true);

        foreach (var m in hurtParty.Members) m.CurHits = 37;
        healer.CurSpellPoints = 5000;
        int pairs = Spellbook.Known(healer, spells).Count(s => IsHeal(s) && CastGate.Unfit(healer, s, 0, true, lookup) == null
                                                               && Spellbook.Cost(s, healer) <= healer.CurSpellPoints);
        var refusing = new FakeDungeon { OnCast = (_, _, _) => false };
        Check($"every cast refused: each heal tried once ({pairs}), then it stops", RunHeal(refusing, hurtParty), pairs.ToString());
        var free = new FakeDungeon { OnCast = (_, _, _) => true };
        Check("casts that say yes but spend nothing: each tried once, then it stops", RunHeal(free, hurtParty), pairs.ToString());
        var never = new FakeDungeon { OnCast = (c, _, _) => { c.CurSpellPoints--; return true; } };
        healer.CurSpellPoints = 1_000_000;
        Check("casts that spend and heal nothing: the cap ends it", RunHeal(never, hurtParty), cap.ToString());
        healer.CurSpellPoints = 5000;
        var crowded = new FakeDungeon { Monsters = true };
        Check("monsters present: nothing cast", RunHeal(crowded, hurtParty), "0");

        // ---- the buff lists ----
        Console.WriteLine("Kaizen buff lists: each member's own, in party order, each row done or skipped:");
        var caster = Member("Cas", GuildData.All, 400);
        var other = Member("Oto", new[] { noHealGuild! }, 10);
        var gone = Member("Gon", new[] { noHealGuild! }, 10);
        var known = Spellbook.Known(caster, spells);
        var asks = known.First(s => SpellThrown.Handles(s) && SpellThrown.WhoToAsk(s) == SpellThrown.Ask.People);
        var whole = known.FirstOrDefault(s => SpellThrown.Handles(s) && SpellEffect.IsParty(s) && SpellThrown.WhoToAsk(s) == SpellThrown.Ask.None);
        var mapSpell = known.FirstOrDefault(s => !SpellThrown.Handles(s));
        var unknown = spells.FirstOrDefault(s => !Spellbook.Known(other, spells).Contains(s) && SpellThrown.Handles(s));
        var wand = items.First(i => i.Id != 0 && ItemUse.SpellOf(i, spells) is Spell sp && SpellThrown.Handles(sp) && SpellThrown.WhoToAsk(sp) == SpellThrown.Ask.None
                                  && !string.Equals(i.Name.Trim(), Loot.NameFor(i, IdLevel.Barely).Trim(), StringComparison.OrdinalIgnoreCase));
        var wandSlot = new InventorySlot { ItemId = wand.Id, Known = IdLevel.All, Charges = 3 };
        caster.PutInPack(wandSlot);
        var hidden = new InventorySlot { ItemId = wand.Id, Known = IdLevel.Barely, Charges = 3 };
        other.PutInPack(hidden);
        var buffParty = new Party(caster, other, gone);
        gone.IsDead = true; gone.CurHits = 0;   // after joining: a party takes no dead member in
        string q = asks.Name.Trim();
        var bl = NewLists(null);
        // set in another order than the party's: the run goes by the party
        SetList(bl, "Gon", ("Spell", q, "Cas"), ("Spell", q, "Oto"));                 // dead: one line for the list
        var otoRows = new List<(string, string, string)> { ("Item", wand.Name, "") };   // known only Barely: not found by its real name
        if (unknown != null) otoRows.Add(("Spell", unknown.Name, "Cas"));               // not known: sent, the game refuses
        SetList(bl, "Oto", otoRows.ToArray());
        var casRows = new List<(string, string, string)>
        {
            ("Spell", q, "Zed"),                        // the target not in the party: passed over, not a word
            ("Spell", q.ToLowerInvariant(), " oto "),   // done: names matched loosely
            ("Spell", q, ""),                           // asks Cast on Who? and no target
            ("Spell", "No Such Thing", ""),             // no such spell
            ("Item", wand.Name, "Oto"),                 // an item: Use, the target ignored by the game
            ("Item", "No Such Thing", ""),              // not in the pack
            ("Spell", "", "Oto"),                       // nothing chosen yet: left out
        };
        if (whole != null) casRows.Add(("Spell", whole.Name, ""));
        if (mapSpell != null) casRows.Add(("Spell", mapSpell.Name, ""));
        SetList(bl, "Cas", casRows.ToArray());
        SetList(bl, "Zed", ("Spell", q, "Cas"));       // a character not in the party: their list is not looked at
        Check("a list read back as set, by the name without regard to case", ListOf(bl, " gon "), $"Spell:{q}>Cas / Spell:{q}>Oto");
        var bf = new FakeDungeon { OnCast = (c, s, _) => s != unknown };
        RunLists(bf, buffParty, bl);
        // the wand casts a party-wide spell: its row's target is not passed
        var wantCalls = new List<string> { $"cast Cas {q} Oto", $"use Cas {wand.Name.Trim()} -" };
        if (whole != null) wantCalls.Add($"cast Cas {whole.Name.Trim()} -");
        if (unknown != null) wantCalls.Add($"cast Oto {unknown.Name.Trim()} Cas");
        Check("what was sent to the game: Cas's list, then Oto's (party order), each in order", string.Join(" | ", bf.Calls), string.Join(" | ", wantCalls));
        var wantSaid = new List<string>
        {
            $"Kaizen: \"{q}\" needs a target.",
            "Kaizen: There is no spell called \"No Such Thing\".",
            "Kaizen: \"Cas\" has no item called \"No Such Thing\".",
        };
        if (mapSpell != null) wantSaid.Add($"Kaizen: \"{mapSpell.Name.Trim()}\" asks for more than a target; cast it by hand.");
        wantSaid.Add($"Kaizen: \"Oto\" has no item called \"{wand.Name.Trim()}\".");
        wantSaid.Add("Kaizen: \"Gon\" is dead.");
        Check("what the mod said for the skips (the game's refusals are the game's); the dead member once", string.Join(" | ", bf.Said), string.Join(" | ", wantSaid));
        Check("the absent target and the absent character: nothing said of either", bf.Said.Any(l => l.Contains("Zed")), false);
        var turned = new FakeDungeon();
        RunLists(turned, new Party(other, caster), bl);
        Check("the party the other way round: Oto's list first", turned.Calls.FirstOrDefault()?.StartsWith("cast Oto ") ?? unknown == null, true);
        var alone = new FakeDungeon();
        RunLists(alone, new Party(caster), bl);
        Check("Cas alone: every row whose spell asks for Oto passed over without a word; the party-wide ones (the wand naming Oto too) done",
              $"{string.Join(" | ", alone.Calls)} # {alone.Said.Count(l => l.Contains("Oto"))}",
              string.Join(" | ", new[] { $"use Cas {wand.Name.Trim()} -", whole != null ? $"cast Cas {whole.Name.Trim()} -" : null }.Where(x => x != null)) + " # 0");

        // a party-wide row asks nobody: its target field is not looked at, whoever it names
        var wideList = NewLists(null);
        var wideRows = new List<(string, string, string)> { ("Spell", q, "Zed"), ("Item", wand.Name, "Zed") };
        if (whole != null) wideRows.Insert(0, ("Spell", whole.Name, "Zed"));
        SetList(wideList, "Cas", wideRows.ToArray());
        var wide = new FakeDungeon();
        RunLists(wide, buffParty, wideList);
        var wantWide = new List<string>();
        if (whole != null) wantWide.Add($"cast Cas {whole.Name.Trim()} -");
        wantWide.Add($"use Cas {wand.Name.Trim()} -");
        Check("party-wide spell and item naming a target not in the party: done all the same, the target not passed; the spell that asks for it passed over",
              $"{string.Join(" | ", wide.Calls)} # {wide.Said.Count}", $"{string.Join(" | ", wantWide)} # 0");
        Check("whether a row asks Cast on Who? (the window's Target box): the spell that asks, the party-wide item",
              $"{AsksOf(("Spell", q, ""), caster)} {AsksOf(("Item", wand.Name, ""), caster)} {AsksOf(("Item", wand.Name, ""), other)}", "True False ");
        if (whole != null) Check("... and the party-wide spell", AsksOf(("Spell", whole.Name, ""), caster), "False");

        var monstersHere = new FakeDungeon { Monsters = true };
        RunLists(monstersHere, buffParty, bl);
        Check("monsters present: nothing done, nothing said", $"{monstersHere.Calls.Count} {monstersHere.Said.Count}", "0 0");

        // ---- an item not fully identified tells nothing ----
        Console.WriteLine("Kaizen: an item below All is named only as the pack names it, and not used:");
        var uni = Member("Uni", new[] { noHealGuild! }, 10);
        var uniParty = new Party(uni, Member("Val", new[] { noHealGuild! }, 10));
        var asksItem = items.First(i => i.Id != 0 && ItemUse.SpellOf(i, spells) is Spell sp && SpellThrown.Handles(sp)
                                     && SpellThrown.WhoToAsk(sp) != SpellThrown.Ask.None
                                     && spells.All(s => !string.Equals(s.Name.Trim(), i.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
        var mapItem = items.FirstOrDefault(i => i.Id != 0 && ItemUse.SpellOf(i, spells) is Spell sp && !SpellThrown.Handles(sp)
                                     && spells.All(s => !string.Equals(s.Name.Trim(), i.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
        var plain = items.First(i => i.Id != 0 && ItemUse.SpellOf(i, spells) == null && i.Name.Trim() != ""
                                     && spells.All(s => !string.Equals(s.Name.Trim(), i.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
        string Shown(Item it, IdLevel k) => k >= IdLevel.Mostly ? it.Name.Trim() : Loot.NameFor(it, k).Trim();
        var cases = new List<(Item It, IdLevel Known)> { (asksItem, IdLevel.Mostly), (asksItem, IdLevel.Partial), (asksItem, IdLevel.Barely), (plain, IdLevel.Mostly) };
        if (mapItem != null) cases.Add((mapItem, IdLevel.Mostly));
        foreach (var (it, k) in cases)
        {
            uni.ClearPack();
            uni.PutInPack(new InventorySlot { ItemId = it.Id, Known = k, Charges = 3 });
            var uf = new FakeDungeon();
            var one = NewLists(null);
            SetList(one, "Uni", ("Item", Shown(it, k), ""));
            RunLists(uf, uniParty, one);
            Check($"{(it == asksItem ? "an item whose spell asks a target" : it == plain ? "an item holding no spell" : "an item holding a map spell")}, known {k}: one line, the shown name only, nothing used",
                  $"{uf.Calls.Count} | {string.Join(" | ", uf.Said)}", $"0 | Kaizen: \"{Shown(it, k)}\" is not fully identified; use it by hand.");
        }
        uni.ClearPack();
        uni.PutInPack(new InventorySlot { ItemId = asksItem.Id, Known = IdLevel.Mostly, Charges = 3 });
        uni.PutInPack(new InventorySlot { ItemId = asksItem.Id, Known = IdLevel.All, Charges = 3 });
        var known1 = new FakeDungeon();
        var allOne = NewLists(null);
        SetList(allOne, "Uni", ("Item", asksItem.Name.Trim(), ""));
        RunLists(known1, uniParty, allOne);
        Check("the same item known at All (after a copy known Mostly): that one is taken, and its spell's needs may be said (the game shows it)",
              string.Join(" | ", known1.Said), $"Kaizen: \"{ItemUse.SpellOf(asksItem, spells)!.Name.Trim()}\" needs a target.");
        Check("the dungeon's Use refuses an item below All before looking at it",
              PortSource("Mordor.App", "DungeonWindow.Mods.cs").Contains("if (slot.Known != IdLevel.All) return false;"), true);

        // ---- the Kaizen window: what a drop adds, and what it refuses ----
        Console.WriteLine("Kaizen window: a drop from the Spells list or the pack takes only known spells and items at All:");
        var verdictType = buffs.GetNestedType("DropVerdict")!;
        string RowText(object? r) => r == null ? "-" : $"{rowType.GetProperty("Kind")!.GetValue(r)}:{rowType.GetProperty("Name")!.GetValue(r)}>{rowType.GetProperty("Target")!.GetValue(r)}";
        string Judged(ModDragged d, Character owner)
        {
            var v = buffs.GetMethod("Judge")!.Invoke(null, new object[] { d, owner, (IReadOnlyList<Spell>)Spellbook.Known(owner, spells), (IReadOnlyList<Spell>)spells, lookup })!;
            var row = verdictType.GetProperty("Row")!.GetValue(v);
            var why = (string?)verdictType.GetProperty("Refusal")!.GetValue(v);
            var asksFor = (Spell?)verdictType.GetProperty("AsksFor")!.GetValue(v);
            return row == null ? (why == null ? "silent" : "no: " + why) : RowText(row) + (asksFor != null ? " ask " + asksFor.Name.Trim() : "");
        }
        ModDragged SpellDrag(Character owner, Spell s) => new(ModDragKind.Spell, owner, spells.IndexOf(s));
        ModDragged PackDrag(Character owner, int slot) => new(ModDragKind.PackItem, owner, slot);
        Check("a known spell that asks \"Cast on Who?\": a row, its target to be asked", Judged(SpellDrag(caster, asks), caster), $"Spell:{q}> ask {q}");
        if (whole != null) Check("a known party-wide spell: a row, nobody asked", Judged(SpellDrag(caster, whole), caster), $"Spell:{whole.Name.Trim()}>");
        if (mapSpell != null)
            Check("a known spell Kaizen cannot send (asks more than a target): refused, in the run's words", Judged(SpellDrag(caster, mapSpell), caster),
                  $"no: \"{mapSpell.Name.Trim()}\" asks for more than a target; cast it by hand.");
        if (unknown != null)
            Check("a spell the owner does not know: refused", Judged(SpellDrag(other, unknown), other), $"no: \"{unknown.Name.Trim()}\" is not a spell \"Oto\" knows.");
        Check("a spell line of another character's list: refused, the list is the owner's", Judged(SpellDrag(other, asks), caster), "no: Only \"Cas\"'s own spells and items go in this list.");
        Check("no such place in the spell table: nothing, silently", Judged(new ModDragged(ModDragKind.Spell, caster, spells.Count + 5), caster), "silent");
        Check("an item known at All whose spell Kaizen can send (party-wide): a row, nobody asked", Judged(PackDrag(caster, caster.SlotOf(wandSlot)), caster), $"Item:{wand.Name.Trim()}>");
        Check("the Hands, and an empty slot: nothing, silently", $"{Judged(PackDrag(caster, 0), caster)} {Judged(PackDrag(caster, caster.FirstFreeSlot()), caster)}", "silent silent");
        // below All: the line names the item as the pack does, and says nothing of its spell - the
        // same line for one holding a spell that asks, one holding none, and one holding a map spell
        foreach (var (it, k) in cases)
        {
            uni.ClearPack();
            var s1 = new InventorySlot { ItemId = it.Id, Known = k, Charges = 3 };
            uni.PutInPack(s1);
            Check($"dropped, {(it == asksItem ? "an item whose spell asks a target" : it == plain ? "an item holding no spell" : "an item holding a map spell")} known {k}: refused with the pack's name only",
                  Judged(PackDrag(uni, uni.SlotOf(s1)), uni), $"no: \"{Shown(it, k)}\" is not fully identified; use it by hand.");
        }
        uni.ClearPack();
        var plainAll = new InventorySlot { ItemId = plain.Id, Known = IdLevel.All, Charges = 3 };
        var asksAll = new InventorySlot { ItemId = asksItem.Id, Known = IdLevel.All, Charges = 3 };
        uni.PutInPack(plainAll);
        uni.PutInPack(asksAll);
        Check("an item at All holding no spell: refused (its Info says so)", Judged(PackDrag(uni, uni.SlotOf(plainAll)), uni), $"no: \"{plain.Name.Trim()}\" holds no spell.");
        var asksItemSpell = ItemUse.SpellOf(asksItem, spells)!;
        Check("an item at All whose spell asks: a row, its spell's target to be asked", Judged(PackDrag(uni, uni.SlotOf(asksAll)), uni), $"Item:{asksItem.Name.Trim()}> ask {asksItemSpell.Name.Trim()}");
        if (mapItem != null)
        {
            var mapAll = new InventorySlot { ItemId = mapItem.Id, Known = IdLevel.All, Charges = 3 };
            uni.PutInPack(mapAll);
            Check("an item at All whose spell asks more than a target: refused", Judged(PackDrag(uni, uni.SlotOf(mapAll)), uni), $"no: \"{mapItem.Name.Trim()}\" asks for more than a target; use it by hand.");
        }

        // Take: the target asked only when the spell asks, through the box handed in
        string Taken(ModDragged d, Character owner, Character? answer, out int askedTimes)
        {
            int n = 0;
            Func<Spell, Task<Character?>> ask = _ => { n++; return Task.FromResult(answer); };
            var task = (Task)buffs.GetMethod("Take")!.Invoke(null, new object[] { d, owner, (IReadOnlyList<Spell>)Spellbook.Known(owner, spells), (IReadOnlyList<Spell>)spells, lookup, ask })!;
            task.GetAwaiter().GetResult();
            object pair = task.GetType().GetProperty("Result")!.GetValue(task)!;
            askedTimes = n;
            var why = (string?)pair.GetType().GetField("Item2")!.GetValue(pair);
            return RowText(pair.GetType().GetField("Item1")!.GetValue(pair)) + (why == null ? "" : " | " + why);
        }
        Check("a spell that asks: \"Cast on Who?\" once, the row's target the member picked", $"{Taken(SpellDrag(caster, asks), caster, other, out int n1)} {n1}", $"Spell:{q}>Oto 1");
        Check("... the box cancelled: no row, nothing said", $"{Taken(SpellDrag(caster, asks), caster, null, out int n2)} {n2}", "- 1");
        if (whole != null) Check("a party-wide spell: nobody asked", $"{Taken(SpellDrag(caster, whole), caster, other, out int n3)} {n3}", $"Spell:{whole.Name.Trim()}> 0");
        Check("a party-wide item: nobody asked", $"{Taken(PackDrag(caster, caster.SlotOf(wandSlot)), caster, other, out int n4)} {n4}", $"Item:{wand.Name.Trim()}> 0");
        Check("an item's spell that asks: asked", $"{Taken(PackDrag(uni, uni.SlotOf(asksAll)), uni, other, out int n5)} {n5}", $"Item:{asksItem.Name.Trim()}>Oto 1");
        uni.ClearPack();
        var asksMostly = new InventorySlot { ItemId = asksItem.Id, Known = IdLevel.Mostly, Charges = 3 };
        uni.PutInPack(asksMostly);
        Check("an item below All: refused before anything is asked", $"{Taken(PackDrag(uni, uni.SlotOf(asksMostly)), uni, other, out int n6)} {n6}",
              $"- | \"{Shown(asksItem, IdLevel.Mostly)}\" is not fully identified; use it by hand. 0");
        if (mapSpell != null) Check("a spell Kaizen cannot send: refused, nobody asked", $"{Taken(SpellDrag(caster, mapSpell), caster, other, out int n7)} {n7}",
              $"- | \"{mapSpell.Name.Trim()}\" asks for more than a target; cast it by hand. 0");
        uni.ClearPack();

        // the row's spell, for a double-click's "Cast on Who?" again
        object? RowSpellOf((string Kind, string Name, string Target) r, Character c) =>
            buffs.GetMethod("RowSpell")!.Invoke(null, new object[] { Activator.CreateInstance(rowType, Enum.Parse(kindType, r.Kind), r.Name, r.Target)!, c, (IReadOnlyList<Spell>)spells, lookup });
        Check("a row's spell: the spell by name, the item's at All, nothing for an item only known Barely",
              $"{(RowSpellOf(("Spell", q, ""), caster) as Spell)?.Name.Trim()} {(RowSpellOf(("Item", wand.Name, ""), caster) as Spell)?.Name.Trim()} {RowSpellOf(("Item", wand.Name, ""), other) == null}",
              $"{q} {ItemUse.SpellOf(wand, spells)!.Name.Trim()} True");

        // the window's edits, and that they are what is kept
        Console.WriteLine("Kaizen window: a row moved, removed, put in and retargeted, and kept so:");
        var editsType = asm.GetType("Mordor.Mod.Kaizen.BuffEdits")!;
        object Rows(params string[] names)
        {
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType))!;
            foreach (var nm in names) list.Add(Activator.CreateInstance(rowType, Enum.Parse(kindType, "Spell"), nm, ""));
            return list;
        }
        string Names(object rs) => string.Join(",", ((System.Collections.IEnumerable)rs).Cast<object>().Select(r =>
            rowType.GetProperty("Name")!.GetValue(r) + ((string)rowType.GetProperty("Target")!.GetValue(r)! == "" ? "" : ">" + rowType.GetProperty("Target")!.GetValue(r))));
        object Edit(string how, object rs, params object[] args) => editsType.GetMethod(how)!.Invoke(null, new[] { rs }.Concat(args).ToArray())!;
        object D = Activator.CreateInstance(rowType, Enum.Parse(kindType, "Spell"), "D", "")!;
        Check("moved down to the end, up to the top, onto its own gaps (nothing)",
              $"{Names(Edit("Move", Rows("A", "B", "C"), 0, 3))} {Names(Edit("Move", Rows("A", "B", "C"), 2, 0))} {Names(Edit("Move", Rows("A", "B", "C"), 1, 1))} {Names(Edit("Move", Rows("A", "B", "C"), 1, 2))} {Names(Edit("Move", Rows("A", "B", "C"), 0, 2))}",
              "B,C,A C,A,B A,B,C A,B,C B,A,C");
        Check("a drop put in at its gap, or at the end", $"{Names(Edit("Insert", Rows("A", "B", "C"), 1, D))} {Names(Edit("Insert", Rows("A", "B", "C"), 99, D))} {Names(Edit("Insert", Rows(), 0, D))}",
              "A,D,B,C A,B,C,D D");
        Check("removed; out of range nothing", $"{Names(Edit("Remove", Rows("A", "B", "C"), 1))} {Names(Edit("Remove", Rows("A", "B", "C"), 3))}", "A,C A,B,C");
        Check("retargeted", Names(Edit("Retarget", Rows("A", "B", "C"), 2, " Oto ")), "A,B,C>Oto");
        int GapAfter(object before, object now, int gap) => (int)editsType.GetMethod("GapAfter")!.Invoke(null, new[] { before, now, gap })!;
        Check("a drop's gap after its box: kept while the list is unchanged, the end once it changed or out of range",
              $"{GapAfter(Rows("A", "B", "C"), Rows("A", "B", "C"), 1)} {GapAfter(Rows("A", "B", "C"), Rows("A", "C"), 1)} {GapAfter(Rows("A", "B"), Rows("A", "B"), 5)} {GapAfter(Rows("A"), Rows("A"), -1)}",
              "1 2 2 1");
        string editsJson = Path.Combine(scratch, "kaizen-edits.json");
        if (File.Exists(editsJson)) File.Delete(editsJson);
        var kept = NewLists(editsJson);
        listsType.GetMethod("Set")!.Invoke(kept, new object[] { "Cas", Rows("A", "B", "C") });
        object Now() => listsType.GetMethod("For")!.Invoke(kept, new object[] { "Cas" })!;
        void Put(object rs) => listsType.GetMethod("Set")!.Invoke(kept, new object[] { "Cas", rs });
        Put(Edit("Move", Now(), 0, 3));
        Put(Edit("Insert", Now(), 1, D));
        Put(Edit("Retarget", Now(), 0, "Oto"));
        Put(Edit("Remove", Now(), 3));
        Check("each edit written at once: a fresh load reads the list as left", Names(listsType.GetMethod("For")!.Invoke(NewLists(editsJson), new object[] { "cas" })!), "B>Oto,D,C");
        Put(Edit("Remove", Edit("Remove", Edit("Remove", Now(), 0), 0), 0));
        Check("... the last row removed: the character leaves the file", File.ReadAllText(editsJson).Contains("\"Cas\""), false);

        // ---- an anti-magic square ends the moment ----
        Console.WriteLine("Kaizen: \"Anti-Magic area!\" ends the run at once:");
        var am = new FakeDungeon();
        am.OnCast = (_, _, _) => { am.Refusal = RefusalKind.AntiMagic; return false; };
        var amList = NewLists(null);
        SetList(amList, "Cas", ("Spell", q, "Oto"), ("Spell", q, "Cas"));
        SetList(amList, "Oto", ("Spell", q, "Cas"));
        RunLists(am, buffParty, amList);
        Check("buff lists: the first cast swallowed, nothing more tried, not even the next member's list", string.Join(" | ", am.Calls), $"cast Cas {q} Oto");
        var spOut = new FakeDungeon();
        spOut.OnCast = (_, _, _) => { spOut.Refusal = RefusalKind.SpellPoints; return false; };
        RunLists(spOut, buffParty, amList);
        Check("... another refusal (the points): the lists go on", spOut.Calls.Count, 3);
        foreach (var m in hurtParty.Members) m.CurHits = 37;
        healer.CurSpellPoints = 5000;
        var amHeal = new FakeDungeon();
        amHeal.OnCast = (_, _, _) => { amHeal.Refusal = RefusalKind.AntiMagic; return false; };
        Check("heal-out: one cast swallowed, then it stops (not every caster and spell)", RunHeal(amHeal, hurtParty), "1");

        // ---- one moment at a time ----
        Console.WriteLine("mod actions: one at a time:");
        lm.Settings.Set("HealBeforeTown", true);
        var slow = new FakeDungeon();
        var gate = new TaskCompletionSource<bool>();
        slow.Pending = gate.Task;
        var first = on.LeavingByStairs(() => slow.Make(hurtParty, spells, lookup));
        Check("a hook awaiting a cast: still running", $"{first.IsCompleted} {on.ActionRunning}", "False True");
        int again = 0;
        var second = on.LeavingByStairs(() => { again++; return slow.Make(hurtParty, spells, lookup); });
        var third = on.EnteredDungeonFromTown(() => { again++; return slow.Make(hurtParty, spells, lookup); });
        Check("asked again meanwhile: nothing run, no context made", $"{second.IsCompleted} {third.IsCompleted} {again} {slow.Calls.Count}", "True True 0 1");
        slow.Pending = null;
        slow.OnCast = (_, _, _) => false;
        gate.SetResult(false);
        first.GetAwaiter().GetResult();
        Check("the first finishes; then a moment runs again", $"{first.IsCompleted} {on.ActionRunning}", "True False");
        lm.Settings.Set("HealBeforeTown", false);
        Check("the dungeon ignores the stairs while a moment is awaited",
              PortSource("Mordor.App", "DungeonWindow.Mods.cs").Contains("if (_modMomentRunning) return;"), true);

        // ---- a Kaizen cast reads as the caster casting in command ----
        Console.WriteLine("Kaizen: a cast for another member reads as if they were in command:");
        var lead = Member("Lea", new[] { noHealGuild! }, 10);
        var hal2 = Member("Hal", healGuilds, 400);
        var bob2 = Member("Bob", new[] { noHealGuild! }, 10);
        var three = new Party(lead, hal2, bob2);   // Lea leads and is in command
        var heal = knownHeals.First(s => !SpellEffect.IsParty(s));
        var massHeal = knownHeals.FirstOrDefault(SpellEffect.IsParty);
        string hq = "\"" + heal.Name.Trim() + "\"";
        Check("by hand (no commander): the one in command, as before",
              SpellThrown.Line(three, heal, new SpellThrown.Target.Person(bob2)), $"\"Lea\" threw {hq} on \"Bob\".");
        Check("Hal's cast: Hal named as the thrower, not the leader",
              SpellThrown.Line(three, heal, new SpellThrown.Target.Person(bob2), hal2), $"\"Hal\" threw {hq} on \"Bob\".");
        hal2.Sex = Sex.Male;
        Check("Hal on himself: himself, as in command",
              SpellThrown.Line(three, heal, new SpellThrown.Target.Person(hal2), hal2), $"\"Hal\" threw {hq} on himself.");
        bob2.CurHits = 50;
        Check("the effect's own line through Apply, with the caster as commander",
              SpellThrown.Apply(three, hal2, heal, new SpellThrown.Target.Person(bob2), 20, new Random(5), lookup, null, commander: hal2),
              $"\"Hal\" threw {hq} on \"Bob\".");
        Check("... and the heal landed", bob2.CurHits > 50, true);
        if (massHeal != null)
            Check("a party-wide heal: the party line, whoever casts",
                  SpellThrown.Line(three, massHeal, new SpellThrown.Target.Party(), hal2), $"\"{massHeal.Name.Trim()}\" thrown on the party.");
        hal2.CurSpellPoints = 0;
        Check("a refusal in the \"you\" wording (CastGate, isYou), never the other member's",
              CastGate.Refuse(hal2, heal, Spellbook.Cost(heal, hal2), true, false, false, false)?.Text, $"Not enough spell points to cast {hq}!");
        string dwin = PortSource("Mordor.App", "DungeonWindow.axaml.cs");
        Check("the dungeon's cast asks the gate as the one in command",
              dwin.Contains("if (CheckCast(spell, itemLevel, who, asCommander: true) is { } refused)"), true);
        Check("... and has the closing line name the caster as commander",
              dwin.Contains("SpellThrown.Apply(_party, who, spell, target, healLevel, _rng, Lookup, _combat, commander: who);"), true);
        Check("... where who is the one in command for a cast by hand",
              dwin.Contains("var who = caster ?? _ch;"), true);

        // ---- through the host, toggles on ----
        Console.WriteLine("Kaizen through the host, toggles on:");
        on.Settings.WriteFlag("Kaizen", "BuffOnEntry", true);
        SetList(modLists, "Cas", ("Spell", q, "Oto"), ("Spell", q, "Zed"));
        string kaizenJson = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ini))!, "kaizen.json");
        Check("the lists kept by the game in kaizen.json beside settings.ini", File.Exists(kaizenJson), true);
        Check("... keyed by the character's name, the kind spelt out", File.Exists(kaizenJson) && File.ReadAllText(kaizenJson).Contains("\"Cas\"") && File.ReadAllText(kaizenJson).Contains("\"Kind\": \"Spell\""), true);
        Check("... read back by a fresh load", ListOf(NewLists(kaizenJson), "CAS"), $"Spell:{q}>Oto / Spell:{q}>Zed");
        Check("... and nothing of them in settings.ini (the old BuffList setting is gone)", new ModSettingsStore(ini).Read("Kaizen", "BuffList"), null);
        SetList(modLists, "Hal");
        Check("an emptied list leaves the file", File.ReadAllText(kaizenJson).Contains("\"Hal\""), false);
        string garbled = Path.Combine(Path.GetDirectoryName(kaizenJson)!, "garbled-kaizen.json");
        File.WriteAllText(garbled, "{ not json");
        var garbledLists = NewLists(garbled);
        string? aside = (string?)listsType.GetProperty("MovedAside")!.GetValue(garbledLists);
        Check("a file that cannot be read: empty lists, and the file moved aside before anything can write over it",
              $"{ListOf(garbledLists, "Cas")}|{File.Exists(garbled)}|{aside != null && Path.GetFileName(aside).StartsWith("garbled-kaizen.json.bad-") && Path.GetDirectoryName(aside) == Path.GetDirectoryName(garbled)}|{(aside != null && File.Exists(aside) ? File.ReadAllText(aside) : "")}",
              "|False|True|{ not json");
        SetList(garbledLists, "Cas", ("Spell", q, ""));
        Check("... a change then writes a new file, the bad one kept", $"{File.Exists(garbled)} {aside != null && File.ReadAllText(aside) == "{ not json"}", "True True");
        var hf = new FakeDungeon();
        var entered = on.EnteredDungeonFromTown(() => hf.Make(buffParty, spells, lookup));
        Check("entering: done at once (the casts await nothing)", entered.IsCompleted, true);
        Check("... Cas's list cast", string.Join(" | ", hf.Calls), $"cast Cas {q} Oto");
        Check("... the row for Zed, not in the party, passed over without a word", hf.Said.Count, 0);
        on.CharacterDeleted("cas");
        Check("a character deleted on the main menu: their list dropped, and from the file",
              $"{ListOf(modLists, "Cas") == ""} {File.ReadAllText(kaizenJson).Contains("\"Cas\"")}", "True False");
        Check("... the main menu tells the mods after the delete",
              PortSource("Mordor.App", "TitleWindow.axaml.cs").Contains("Mordor.Modding.ModHost.Active.CharacterDeleted(ch.Name.TrimEnd());"), true);
        string? setting = null;
        lm.Settings.Changed += k => setting = k;
        lm.Settings.Changed += _ => throw new InvalidOperationException("a window's handler failing");
        on.Settings.WriteFlag("Kaizen", "BuffOnEntry", false);
        Check("a setting changed: the mod's own Settings.Changed says which (a throwing handler kept to itself)", $"{setting} {lm.Faulted}", "BuffOnEntry False");
        on.Settings.WriteFlag("Kaizen", "BuffOnEntry", true);

        // ---- ctx.Game: the game in play, for a mod's window ----
        Console.WriteLine("ctx.Game: the party in play and the one in command, told when they change:");
        var game = (ModGame)typeof(LoadedMod).GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(lm)!
            .GetType().GetProperty("Game")!.GetValue(typeof(LoadedMod).GetProperty("Context", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(lm))!;
        Check("no game yet: no party, nobody in command, no spells", $"{game.Party == null} {game.Selected == null} {game.Spells.Count}", "True True 0");
        int told = 0;
        game.Changed += () => told++;
        on.SetGame(buffParty, spells, lookup);
        Check("a game begun: the party, the one in command, the tables", $"{game.Party == buffParty} {game.Selected?.Name} {game.Spells.Count == spells.Count} {game.Item(wand.Id) == wand} {told}", "True Cas True True 1");
        Check("... what a member knows, as the spell page lists it", game.KnownSpells(caster).Count, Spellbook.Known(caster, spells).Count);
        on.CharacterSwitched(other, inDungeon: false);
        on.GameChanged();
        Check("a switch and a redraw: told each time", told, 3);
        // AskCastOnWho: the game's own box, for a mod's window (API version 4)
        var someSpell = spells.First(s => s.Name.Trim() != "");
        Check("API version 12 (7: IModLifecycle, mods switched on and off on the main menu; 8: ExperienceGained; 9: MessageLineColour; 10: choice settings; 11: settings windows; 12: PartyGoldTo gone, TownIncomeBank, the arrival Party Pool, ShortfallAnswer.From, option tooltips)", ModApi.ApiVersion, 12);
        Check("no box (headless): Cancel", game.AskCastOnWho(someSpell).GetAwaiter().GetResult() == null, true);
        string asked = "";
        int answer = 1, startedOn = -1;
        on.CastOnWhoBox = (t, p, o, over, sel) => { asked = $"{t} | {p} | {string.Join(",", o)} | {over}"; startedOn = sel; return Task.FromResult(answer); };
        var picked = game.AskCastOnWho(someSpell, "the window").GetAwaiter().GetResult();
        Check("the box asked as the dungeon asks it: the spell in the title, \"Cast on Who?\", the party in order, over the mod's control",
              asked, $"Cast \"{someSpell.Name.TrimEnd()}\" | Cast on Who? | Cas,Oto,Gon | the window");
        Check("... the member picked", picked?.Name, "Oto");
        Check("... the box starts on the first member when no target is given", startedOn, 0);
        game.AskCastOnWho(someSpell, "the window", "Gon").GetAwaiter().GetResult();
        Check("... and on the row's target when one is (a double-click changing it)", startedOn, 2);
        game.AskCastOnWho(someSpell, "the window", "Nobody").GetAwaiter().GetResult();
        Check("... and on the first when the target is not in the party", startedOn, 0);
        Check("the Kaizen window asks with the row's target", PortSource("Mods", "Mordor.Mod.Kaizen", "KaizenView.cs").Contains("game.AskCastOnWho(spell, rootRef, row.Target)"), true);
        // the help line had read "uncanard"'s Spells list: a closing quote and an apostrophe together
        Check("the Kaizen window's text puts no apostrophe straight after a quoted name",
              PortSource("Mods", "Mordor.Mod.Kaizen", "KaizenView.cs").Contains("}\\\"'s"), false);
        answer = -1;
        Check("... Cancel: nobody", game.AskCastOnWho(someSpell).GetAwaiter().GetResult() == null, true);
        answer = 7;
        Check("... an answer out of range: nobody", game.AskCastOnWho(someSpell).GetAwaiter().GetResult() == null, true);
        on.CastOnWhoBox = null;
        Check("the game sets the box at start-up, the dungeon's own list (PickList)",
              PortSource("Mordor.App", "ModStartup.cs").Contains("host.CastOnWhoBox = PickList.ForMods;")
              && PortSource("Mordor.App", "DungeonWindow.axaml.cs").Contains("PickList.Ask(DialogOwner, title, prompt, options);"), true);
        // ModDragged: what the game's drags carry for a mod
        Check("a dragged pack line: the Hands and an empty slot carry no slot, a filled one its own",
              $"{new ModDragged(ModDragKind.PackItem, caster, 0).Slot == null} {new ModDragged(ModDragKind.PackItem, caster, caster.FirstFreeSlot()).Slot == null} {new ModDragged(ModDragKind.PackItem, caster, caster.SlotOf(wandSlot)).Slot == wandSlot}",
              "True True True");
        Check("a dragged spell line: the spell at its place in the table, nothing out of range or for a pack line",
              $"{new ModDragged(ModDragKind.Spell, caster, spells.IndexOf(asks)).SpellIn(spells) == asks} {new ModDragged(ModDragKind.Spell, caster, -1).SpellIn(spells) == null} {new ModDragged(ModDragKind.PackItem, caster, spells.IndexOf(asks)).SpellIn(spells) == null}",
              "True True True");
        string payload = PortSource("Mordor.App", "DragPayload.cs");
        Check("the game's drags carry it beside their own, for spell and pack lines with an owner",
              payload.Contains("d.Set(Mordor.Modding.ModDragged.Format, new Mordor.Modding.ModDragged(") && payload.Contains("if (Owner != null && Kind is DragKind.Item or DragKind.Spell)"), true);
        foreach (var (file, spellLine) in new[] { ("CityWindow.Mdi.cs", "DragPayload.Spell(_known[i].SpellIndex) with { Owner = _ch }"), ("DungeonWindow.Mdi.cs", "DragPayload.Spell(SpellIndexOf(_sheetSpells[i].Spell)) with { Owner = _ch }") })
            Check($"... {file}: the Spells list and the pack name the one in command as their owner",
                  PortSource("Mordor.App", file).Contains(spellLine) && PortSource("Mordor.App", file).Contains("DragPayload.Item(i) with { Owner = _ch }"), true);
        on.SetGame(null);
        Check("the game over: nothing again", $"{game.Party == null} {game.Selected == null} {game.Spells.Count} {game.Item(wand.Id) == null} {told}", "True True 0 True 4");
        off.SetGame(buffParty, spells, lookup);
        off.GameChanged();
        Check("mods off: setting the game asks nothing of anyone", off.Loaded.Count, 0);
        off.SetGame(null);
        Check("the city sets it as it opens and clears it as it closes",
              PortSource("Mordor.App", "CityWindow.axaml.cs").Contains("Mordor.Modding.ModHost.Active.SetGame(_party, _spells, Lookup);")
              && PortSource("Mordor.App", "CityWindow.axaml.cs").Contains("Mordor.Modding.ModHost.Active.SetGame(null);"), true);
        Check("the Kaizen window is offered (the toolbar's button)", lm.UI.Windows.Select(w => w.Title).FirstOrDefault(), "Kaizen");

        // ---- the Mods page: on the main menu, not in Options ----
        Console.WriteLine("the Mods page: a button on the main menu, none in Options:");
        string title = PortSource("Mordor.App", "TitleWindow.axaml");
        Check("the main menu has a Mods button", title.Contains("x:Name=\"ModsBtn\"") && title.Contains("Click=\"OnMods\""), true);
        Check("... in the menu's column, a row above HelpLesson and Options",
              title.Contains("x:Name=\"ModsBtn\" Canvas.Left=\"175\" Canvas.Top=\"293\" Width=\"250\" Height=\"27\""), true);
        Check("Options has none", PortSource("Mordor.App", "OptionsWindow.cs").Contains("ModsWindow"), false);
        Check("the in-game Control Panel has its own Mods button, to the same page with the Enabled boxes greyed",
              PortSource("Mordor.App", "ControlPanelWindow.cs").Contains("await new ModsWindow(atMainMenu: false).ShowDialog(this);"), true);
        Check("the main menu's Mods button opens it with the Enabled boxes live",
              PortSource("Mordor.App", "TitleWindow.axaml.cs").Contains("await new ModsWindow(atMainMenu: true).ShowDialog(this);"), true);
        string view = PortSource("Mods", "Mordor.Mod.Kaizen", "KaizenView.cs");
        Check("the Kaizen window: no drop-downs, nothing that takes the keyboard; a drop read as the game's ModDragged, the box asked after the drop",
              !view.Contains("ComboBox") && !view.Contains("Focus()") && view.Contains("e.Data.Get(ModDragged.Format) is not ModDragged d")
              && view.Contains("Dispatcher.UIThread.Post(() => SafeAsync(\"taking a drop\", () => TakeDrop(d, gap)));"), true);
        Check("the Kaizen window: one \"Cast on Who?\" box at a time; a drop off the rows' visible area goes to the end; the game's drag distance",
              view.Contains("if (shown is not { } owner || asking || at >= rows.Count) return;") && view.Contains("if (shown is not { } owner || asking) return;")
              && view.Contains("bool takes = shown != null && !asking &&")
              && view.Contains("if (scrollRef == null || !new Rect(scrollRef.Bounds.Size).Contains(at(scrollRef))) return rowViews.Count;")
              && view.Contains("Save(BuffEdits.Insert(now, BuffEdits.GapAfter(before, now, gap), row));")
              && view.Contains("PlatformSettings?.GetTapSize(PointerType.Mouse)"), true);
        Check("the dungeon's held-key replay waits for its dialogs; a mod's window is a child in the frame, not a window it owns",
              PortSource("Mordor.App", "DungeonWindow.axaml.cs").Contains("while (!_ended && DialogOwner.OwnedWindows.Any(w => w.IsVisible))")
              && PortSource("Mordor.App", "ModWindows.cs").Contains("var child = host.Add(w.Title, body, 0, 0, DefaultWidth, DefaultHeight);"), true);
        Check("... and no other wait on a frame's owned windows", new[] { "CityWindow.axaml.cs", "CityWindow.Mdi.cs", "CityWindow.Store.cs", "DungeonWindow.Mdi.cs", "DungeonWindow.Mods.cs" }
              .Any(f => PortSource("Mordor.App", f).Contains("OwnedWindows")), false);
        Check("a mod's box for a control in no window is Cancel, not the main window",
              PortSource("Mordor.App", "PickList.cs").Contains(": over is Visual v && TopLevel.GetTopLevel(v) is Window w ? ModWindows.FrameOf(w) : null;"), true);
        on.Settings.WriteFlag("Kaizen", "HealBeforeTown", true);
        foreach (var m in hurtParty.Members) m.CurHits = 37;
        healer.CurSpellPoints = 5000;
        var leaving = on.LeavingByStairs(() => real.Make(hurtParty, spells, lookup));
        Check("the stairs: done at once, everybody healed", $"{leaving.IsCompleted} {hurtParty.Members.All(m => m.CurHits == m.MaxHits)}", "True True");
        Check("Kaizen still running", lm.Faulted, false);
        on.Settings.WriteFlag("Kaizen", "BuffOnEntry", false);
        on.Settings.WriteFlag("Kaizen", "HealBeforeTown", false);
    }
}
