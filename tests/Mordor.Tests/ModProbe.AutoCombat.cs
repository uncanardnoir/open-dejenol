using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The Auto-combat mod and the hook under it (docs/MODDING.md): ModActions.CombatRound and its
/// CombatRoundContext, nothing made or asked with no mod hooked (the default), and the rules - kept
/// as a list setting in settings.ini - evaluated through the host against a stand-in for the
/// dungeon's order commands, which records them: first rule that fits wins per member, at most one
/// decision each per round, the conditions (party and own hits, spell points, groups, a name as the
/// list shows it), the aim (names, *first/*last/*most/*fewest), what is never sent (a heal or cure,
/// a spell not known or not affordable, an item below All, a refused cast again in the same fight),
/// orders already standing not sent again, the dead and the held fast left alone, the master toggle,
/// "Leave the one in command to me", a closed context, and a mod throwing in the round disabled.
/// The commands themselves are the dungeon window's (DungeonWindow.Mods.cs), not reachable headless.
/// </summary>
public static partial class ModProbe
{
    /// <summary>The dungeon between two ticks, as a round hook sees it, with the orders recorded instead of given.</summary>
    private sealed class FakeRound
    {
        public readonly List<string> Calls = new();
        public readonly Dictionary<Character, (OrderKind Kind, short Spell, InventorySlot? Item)> Orders = new();
        public readonly Dictionary<Character, int> Aims = new();
        public Func<Character, Spell, bool> CastWorks = (_, _) => true;
        public Func<Character, InventorySlot, bool> UseWorks = (_, _) => true;
        public readonly HashSet<Character> Opening = new();
        public CombatRoundContext? Last;

        public CombatRoundContext Make(Party p, IReadOnlyList<Spell> spells, Func<short, Item?> item, object fight, params MonsterGroupView[] groups) =>
            Last = new CombatRoundContext(p, spells, item, fight, groups,
                m => Orders.TryGetValue(m, out var o) ? new CombatOrderView(o.Kind, o.Spell, o.Item, Opening.Contains(m))
                                                      : new CombatOrderView(OrderKind.None, 0, null, Opening.Contains(m)),
                m => Aims.TryGetValue(m, out int a) ? a : null,
                (m, k) => { Calls.Add($"{m.Name} {k}"); Orders[m] = (k, 0, null); return true; },
                (m, s) =>
                {
                    Calls.Add($"{m.Name} cast {s.Name.Trim()}");
                    if (!CastWorks(m, s)) { Orders[m] = (OrderKind.None, 0, null); return false; }
                    Orders[m] = (OrderKind.Spell, s.Id, null);
                    return true;
                },
                (m, slot) =>
                {
                    Calls.Add($"{m.Name} use {item(slot.ItemId)?.Name.Trim()}");
                    if (!UseWorks(m, slot)) { Orders[m] = (OrderKind.None, 0, null); return false; }
                    Orders[m] = (OrderKind.Spell, 1, slot);
                    return true;
                },
                (m, g) => { Calls.Add($"{m.Name} aim {g + 1}"); Aims[m] = g; return true; });

        public string Take() { string s = string.Join(" / ", Calls); Calls.Clear(); return s; }
    }

    private static void AutoCombatChecks(string dir, string scratch, string bundled, List<Item> items, Func<short, Item?> lookup)
    {
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        string acDir = Path.Combine(scratch, "autocombat");
        Directory.CreateDirectory(acDir);

        // ---- the manifest, and nothing with the mod off ----
        Console.WriteLine("Auto-combat: the manifest, and mods off:");
        var log = new List<string>();
        var off = Host(Path.Combine(acDir, "off.ini"), log, bundled);
        var ac = off.Discovered.FirstOrDefault(d => d.Id == "AutoCombat");
        Check("AutoCombat manifest found, no problem", ac != null && ac.Problem == null, true);
        Check("its name and version", $"{ac?.Manifest.Name} {ac?.Manifest.Version}", "Auto-combat 1.0.0");
        Check("its two toggles, both off by default", string.Join(",", ac?.Manifest.Settings.Select(t => $"{t.Key}={t.Default}") ?? Array.Empty<string>()),
              "FightByRules=False,LeaveInCommand=False");
        Check("no table on the Mods page: the rules are in the Auto-combat window", ac?.Manifest.Tables.Count, 0);
        Check("not enabled by default", off.IsEnabled("AutoCombat"), false);
        int made = 0;
        off.CombatRound(() => { made++; return null!; });
        ModHost.Empty().CombatRound(() => { made++; return null!; });
        Check("mods off, and no host: a round makes no context, asks nothing", made, 0);
        var kzOnly = new ModSettingsStore(Path.Combine(acDir, "kaizen-only.ini"));
        kzOnly.SetEnabled("Kaizen", true);
        var kzHost = Host(kzOnly.Path, log, bundled);
        kzHost.CombatRound(() => { made++; return null!; });
        Check("a mod running with no round hook (Kaizen): no context made", made, 0);
        Check("API version 14 (CombatRound)", ModApi.ApiVersion, 14);

        // ---- the party, the spells, the item ----
        var guilds = GuildData.All.ToList();
        var brak = Member("Brak", guilds.Take(1), 1);
        brak.MaxSpellPoints = brak.CurSpellPoints = 0;
        var cas = Member("Cas", guilds, 60);
        var hal = Member("Hal", guilds, 60);
        var party = new Party(brak, cas, hal);
        var known = Spellbook.Known(cas, spells);
        var bolt = known.First(s => CastGate.UseOf(s) == SpellUse.Combat && Spellbook.Cost(s, cas) > 0);
        var heal = known.FirstOrDefault(s => CastGate.UseOf(s) == SpellUse.NonCombat);
        var unknown = spells.FirstOrDefault(s => CastGate.UseOf(s) == SpellUse.Combat && !Spellbook.Known(brak, spells).Contains(s));
        Check("the probe's party: a combat spell Cas knows, a non-combat one, one Brak does not know", bolt != null && heal != null && unknown != null, true);
        string Bolt = bolt!.Name.Trim();

        var on = new ModSettingsStore(Path.Combine(acDir, "on.ini"));
        on.SetEnabled("AutoCombat", true);
        var host = Host(on.Path, log, bundled);
        var lm = host.Loaded.FirstOrDefault(l => l.Id == "AutoCombat");
        Check("loaded through the host and running", lm?.Running, true);
        var st = lm!.Settings;
        void Rules(params string[][] rows) => st.SetRows("Rules", rows);
        //                          Who    PB    PA     SB    SP    G>=  G<=  Shown       Do        What   Targets
        string[] R(string who = "", string pb = "", string pa = "", string sb = "", string sp = "", string g1 = "", string g2 = "",
                   string shown = "", string act = "Fight", string what = "", string targets = "") =>
            new[] { who, pb, pa, sb, sp, g1, g2, shown, act, what, targets };

        var groups = new[] { new MonsterGroupView(0, "Humanoid", null, "Humanoid"), new MonsterGroupView(1, "Kobolds", 3, "Kobold"),
                             new MonsterGroupView(2, "Giant Rats", 5, "Giant Rat") };
        var f = new FakeRound();
        object fight = new();
        string Round(object? fightKey = null, MonsterGroupView[]? gs = null)
        {
            host.CombatRound(() => f.Make(party, spells, lookup, fightKey ?? fight, gs ?? groups));
            return f.Take();
        }

        // ---- the master toggle ----
        Console.WriteLine("Auto-combat: the rules in a round:");
        Rules(R(targets: "*first"));
        Check("Fight by the rules off: the context is made, nothing given", Round(), "");
        st.Set("FightByRules", true);

        // ---- the user's examples, as the game has them ----
        // 1. hurt -> Defend; 2. Cas, nobody hurt, spell points to spare -> the spell at the Kobolds, else the
        // biggest group; 3. anyone -> Fight, the smallest group first
        Rules(R(sb: "30", act: "Defend"),
              R(who: "cas", pa: "100", sp: "10", act: "Cast", what: Bolt, targets: "Kobold,*most"),
              R(targets: "*fewest"));
        Check("first rule that fits, per member, in party order; aim before order",
              Round(), $"Brak aim 1 / Brak Fight / Cas aim 2 / Cas cast {Bolt} / Hal aim 1 / Hal Fight");
        Check("the same round again: every order already stands, nothing sent", Round(), "");
        hal.CurHits = 20;
        Check("Hal hurt: Hal defends, and Cas's 'nobody hurt' rule no longer fits - Cas fights the smallest group",
              Round(), "Cas aim 1 / Cas Fight / Hal Defend");
        hal.CurHits = hal.MaxHits;
        cas.CurSpellPoints = Spellbook.Cost(bolt, cas) - 1;
        Check("Cas short of the spell's cost (the spell page's figure): the rule is passed over", Round(), "Hal Fight");
        cas.CurSpellPoints = cas.MaxSpellPoints;

        // a refusal is not asked again in the same fight; a new fight asks again
        f.CastWorks = (_, _) => false;
        Check("the game refuses Cas's cast: asked once, the order is the refusal's", Round(), $"Cas aim 2 / Cas cast {Bolt}");
        Check("... next round the rule is passed over in this fight: Cas fights", Round(), "Cas aim 1 / Cas Fight");
        f.CastWorks = (_, _) => true;
        Check("... a new fight asks the spell again", Round(new object()), $"Cas aim 2 / Cas cast {Bolt}");

        // ---- what is never sent ----
        Rules(R(act: "Cast", what: heal!.Name.Trim()), R(act: "Defend"));
        Check("a heal or cure (refused in an engaged fight by the game): never sent, the next rule wins",
              Round(), "Brak Defend / Cas Defend / Hal Defend");
        Rules(R(who: "Brak", act: "Cast", what: unknown!.Name.Trim()), R(act: "Nothing"));
        Check("a spell the member does not know: passed over; Nothing is space", Round(), "Brak None / Cas None / Hal None");

        hal.CurHits = 0; hal.IsDead = true;
        cas.ParalysisLevel = 1;
        Rules(R(act: "Defend"));
        Check("the dead and the held fast are left alone", Round(), "Brak Defend");
        hal.IsDead = false; hal.CurHits = hal.MaxHits; cas.ParalysisLevel = 0;
        st.Set("LeaveInCommand", true);
        party.Select(0);
        Check("Leave the one in command to me: Brak (in command) is skipped", Round(), "Cas Defend / Hal Defend");
        st.Set("LeaveInCommand", false);

        // ---- the conditions on the list as shown ----
        f.Orders.Clear(); f.Aims.Clear();
        Rules(R(shown: "humanoid", act: "Defend"));
        Check("a group shown as 'Humanoid' (an unidentified group's class): fits", Round(), "Brak Defend / Cas Defend / Hal Defend");
        f.Orders.Clear();
        Rules(R(shown: "Orc", act: "Defend"));
        Check("no group shown as 'Orc': nothing", Round(), "");
        var odd = new[] { new MonsterGroupView(0, "Sorcerers", 2, "Sorcerer"), new MonsterGroupView(1, "Werewolves", 3, "Werewolf") };
        Rules(R(shown: "Orc", act: "Defend"), R(targets: "Werewolf"));
        Check("whole words: 'Orc' is not a Sorcerer; 'Werewolf' finds the Werewolves by the name for one",
              Round(gs: odd), "Brak aim 2 / Brak Fight / Cas aim 2 / Cas Fight / Hal aim 2 / Hal Fight");
        Check("the names the list showed are kept for the window, as shown for one",
              string.Join(",", st.GetRows("SeenNames").Select(r => r[0])), "Giant Rat,Humanoid,Kobold,Sorcerer,Werewolf");
        f.Orders.Clear(); f.Aims.Clear();
        f.Opening.Add(cas);
        Rules(R(act: "Defend"));
        Check("a member opening a chest (the Option column's Open) is left to it", Round(), "Brak Defend / Hal Defend");
        f.Opening.Clear();
        f.Orders.Clear();
        Rules(R(g1: "3", act: "Defend"), R(g2: "1", targets: "Rat,*last"));
        Check("three groups: 'at least 3 groups' fits", Round(), "Brak Defend / Cas Defend / Hal Defend");
        f.Orders.Clear();
        var one = new[] { new MonsterGroupView(3, "Giant Rats", 2, "Giant Rat") };
        Check("one group (slot 4): 'at most 1 group' fits, aimed by name", Round(gs: one), "Brak aim 4 / Brak Fight / Cas aim 4 / Cas Fight / Hal aim 4 / Hal Fight");
        Check("no groups standing: nothing at all", Round(gs: Array.Empty<MonsterGroupView>()), "");
        f.Orders.Clear(); f.Aims.Clear();
        Rules(R(pb: "50", act: "Defend"), R(targets: "Dragon"));
        Check("nobody below 50%: the Defend rule does not fit; a target nobody shows leaves the aim", Round(), "Brak Fight / Cas Fight / Hal Fight");
        brak.CurHits = 49;
        Check("Brak below 50%: 'someone below 50%' fits for everyone", Round(), "Brak Defend / Cas Defend / Hal Defend");
        brak.CurHits = brak.MaxHits;

        // ---- an item made the order ----
        var wand = items.FirstOrDefault(i => i.Id != 0 && ItemUse.SpellOf(i, spells) is Spell s && CastGate.UseOf(s) == SpellUse.Combat);
        if (wand != null)
        {
            f.Orders.Clear();
            var low = new InventorySlot { ItemId = wand.Id, Known = IdLevel.Mostly, Charges = 3 };
            hal.PutInPack(low);
            Rules(R(who: "Hal", act: "Use", what: wand.Name.Trim()), R(act: "Defend"));
            Check("an item known only Mostly: not looked at, the next rule wins", Round(), "Brak Defend / Cas Defend / Hal Defend");
            var full = new InventorySlot { ItemId = wand.Id, Known = IdLevel.All, Charges = 3 };
            hal.PutInPack(full);
            Check("known at All: made the order", Round(), $"Hal use {wand.Name.Trim()}");
            Check("... and not sent again while it stands", Round(), "");
            f.Orders.Clear();
            f.UseWorks = (_, _) => false;
            Check("the game refuses the Use: asked once", Round(), $"Brak Defend / Cas Defend / Hal use {wand.Name.Trim()}");
            Check("... and that slot not again in this fight: the next rule wins", Round(), "Hal Defend");
            f.UseWorks = (_, _) => true;
            var itemSpell = ItemUse.SpellOf(wand, spells)!;
            if (Spellbook.Known(cas, spells).Contains(itemSpell) && Spellbook.Cost(itemSpell, cas) <= cas.CurSpellPoints)
            {
                f.Orders[cas] = (OrderKind.Spell, itemSpell.Id, full);
                Rules(R(who: "Cas", act: "Cast", what: itemSpell.Name.Trim()));
                Check("the same spell standing from an item: sent again as a Cast of one's own", Round(), $"Cas cast {itemSpell.Name.Trim()}");
            }
            hal.RemoveFromPack(low); hal.RemoveFromPack(full);
        }
        else Check("an item holding a combat spell (for the Use rule)", "none in MDATA3", "one");

        // ---- rows that make no sense, the closed context ----
        f.Orders.Clear();
        Rules(new[] { "", "", "", "", "", "", "", "", "Teleport", "", "" }, new[] { "", "-5", "", "", "", "", "", "", "Fight", "", "" }, R(act: "Defend"));
        Check("an unknown action or a negative number: the row is left out", Round(), "Brak Defend / Cas Defend / Hal Defend");
        var late = f.Last!;
        Check("once the hook has returned the context is closed: commands refuse, nothing given",
              $"{late.IsOpen} {late.Order(brak, OrderKind.Fight)} {late.Aim(brak, 0)} {f.Take()}", "False False False ");
        Check("Order takes only None, Fight and Defend", new CombatRoundContext(party, spells, lookup, fight, groups,
              _ => new CombatOrderView(OrderKind.None, 0, null), _ => null, (_, _) => true, (_, _) => true, (_, _) => true, (_, _) => true)
              .Order(brak, OrderKind.Spell), false);

        // ---- the dungeon side, by its source: the window's commands are not reachable headless ----
        string tick = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
        string mods = PortSource("src/Mordor.App", "DungeonWindow.Mods.cs");
        string Body(string src, string head)
        {
            int at = src.IndexOf(head, StringComparison.Ordinal);
            if (at < 0) return "";
            int end = src.IndexOf("\n    }\n", at, StringComparison.Ordinal);
            return end < 0 ? src[at..] : src[at..end];
        }
        Check("the round hook is asked right before the round is played, inside the engaged branch",
              System.Text.RegularExpressions.Regex.IsMatch(tick, @"BeforeCombatRound\(\);\s*PlayRound\(\);"), true);
        Check("the context hands a token for the fight, never the engine (which holds the monsters' hits)",
              Body(mods, "private CombatRoundContext RoundContext()").Contains("_fightTokens.GetValue(fight"), true);
        Check("the list's names: MonsterRow's fn0E23 and its two-to-twelve count rule",
              Body(mods, "private IReadOnlyList<MonsterGroupView> ShownGroups").Contains("seen.InGroup > 1 && seen.InGroup <= 12 ? alive : null"), true);
        string order = Body(mods, "private bool ModOrder("), cast = Body(mods, "private bool ModCastOrder("), use = Body(mods, "private bool ModItemOrder(");
        Check("f/d/space and a spell order call off a chest count, as the keys do, and let an item slot go",
              $"{order.Contains("CallOffChestCountOf(fight, m)")} {order.Contains("KeepItemSlot(false, m)")} {cast.Contains("CallOffChestCountOf(fight, m)")} {cast.Contains("KeepItemSlot(false, m)")}",
              "True True True True");
        Check("d refused for one held fast; the gate's refusal in the you wording; Use below All refused first, then sub0BDE",
              $"{order.Contains("kind == OrderKind.Defend && m.Paralysed")} {cast.Contains("asCommander: true")} {use.Contains("if (slot.Known != IdLevel.All) return false;")} {use.Contains("Sub0BDE();")}",
              "True True True True");
        Check("every command asks first that the fight is the one on and the member a living one of the party",
              Body(mods, "private bool RoundOpenFor(").Contains("_combat == fight && !fight.IsOver") && new[] { order, cast, use, Body(mods, "private bool ModAim(") }.All(b => b.Contains("RoundOpenFor(fight, m)")), true);

        // ---- a mod that throws in the round ----
        string bad = Path.Combine(acDir, "mods");
        MakeTestMod(bad, "RoundBoom", nameof(RoundBoomMod));
        var bst = new ModSettingsStore(Path.Combine(acDir, "boom.ini"));
        bst.SetEnabled("RoundBoom", true);
        var bh = Host(bst.Path, log, bad);
        int asked = 0;
        bh.CombatRound(() => { asked++; return f.Make(party, spells, lookup, fight, groups); });
        bh.CombatRound(() => { asked++; return f.Make(party, spells, lookup, fight, groups); });
        var boom = bh.Loaded.Single(l => l.Id == "RoundBoom");
        Check("a mod throwing in its round hook is disabled; the next round asks it nothing", $"{boom.Faulted} {asked}", "True 1");
    }
}

/// <summary>Throws in the combat round: it must be disabled, and the round carry on without it.</summary>
public sealed class RoundBoomMod : IMod
{
    public void Load(IModContext ctx) => ctx.Actions.CombatRound = _ => throw new InvalidOperationException("round boom");
}
