using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// The dungeon's side of the mods' <see cref="ModActions"/> (docs/MODDING.md): the two moments a
/// mod may act - going down from town, and the stairs up out of level 1 - and the cast and Use
/// commands it acts through (<see cref="DungeonContext"/>), which run this window's own cast path
/// (<see cref="CastSpellHere"/>, <see cref="CastAndThrow"/>, <see cref="UseItemSpell"/>) for a
/// named party member with the "Cast on Who?" answer given beforehand. With no mod hooked nothing
/// here runs, and every cast by hand goes exactly as it did.
/// </summary>
public partial class DungeonWindow
{
    /// <summary>Set by the city for a fresh walk down from town (see <see cref="EnteredFromTown"/>).</summary>
    private bool _freshFromTown;

    /// <summary>
    /// The city's Dungeon picture (or d) took the party down from town - not a character loaded
    /// below ground, not a raise below. The mods' <see cref="ModActions.EnteredDungeonFromTown"/>
    /// runs once the dungeon is up, before its clocks start.
    /// </summary>
    public void EnteredFromTown() => _freshFromTown = true;

    /// <summary>A context for one mod's hook: this party, these tables, these two commands.</summary>
    private DungeonContext ModContext() =>
        new(_party, _spells, Lookup, () => MonstersHere, ModCast, ModUseItem, Say, () => _lastRefusal);

    /// <summary>
    /// What the gate said to the last mod command, if it refused it (<see cref="DungeonContext.LastRefusal"/>):
    /// set by <see cref="CastSpellHere"/>, cleared as each mod command starts. Nothing else reads it.
    /// </summary>
    private RefusalKind? _lastRefusal;

    /// <summary>
    /// A mod's moment (either one) is still being awaited. While it is, the other cannot start:
    /// the stairs taken again do nothing (see <see cref="BeforeStairsToTown"/>).
    /// </summary>
    private bool _modMomentRunning;

    /// <summary>
    /// The end of the dungeon's start (the Begun handler): the clocks are started by
    /// <paramref name="startClocks"/>, after the mods' entry hook on a fresh walk down. With no mod
    /// hooked, or a hook that awaits nothing, all of it runs at once, as before.
    /// </summary>
    private void AfterBegun(System.Action startClocks)
    {
        bool fresh = _freshFromTown;
        _freshFromTown = false;
        if (!fresh || _modMomentRunning) { startClocks(); return; }
        Task hook;
        _modMomentRunning = true;
        try { hook = ModHost.Active.EnteredDungeonFromTown(ModContext); }
        catch (System.Exception ex) { CrashLog.Note($"dungeon: the entry hook failed - {ex.GetType().Name}: {ex.Message}"); hook = Task.CompletedTask; }
        if (hook.IsCompleted) { _modMomentRunning = false; startClocks(); return; }
        _ = StartAfter(hook, startClocks);
    }

    private async Task StartAfter(Task hook, System.Action startClocks)
    {
        try { await hook; }
        catch (System.Exception ex) { CrashLog.Note($"dungeon: the entry hook failed - {ex.GetType().Name}: {ex.Message}"); }
        finally { _modMomentRunning = false; }
        if (!_ended) startClocks();
    }

    /// <summary>
    /// The stairs up out of level 1, after a won round is finished: the mods'
    /// <see cref="ModActions.LeavingByStairs"/>, then <paramref name="leave"/> (the climb itself).
    /// With no mod hooked, or a hook that awaits nothing, the climb follows at once, as before. A
    /// hook still running after this returns gets the climb when it finishes - if the party is still
    /// on these stairs and the dungeon still up. The stairs taken again while a mod's moment is
    /// being awaited do nothing: that climb is already waiting.
    /// </summary>
    private void BeforeStairsToTown(System.Action leave)
    {
        if (_modMomentRunning) return;
        Task hook;
        _modMomentRunning = true;
        try { hook = ModHost.Active.LeavingByStairs(ModContext); }
        catch (System.Exception ex) { CrashLog.Note($"dungeon: the stairs hook failed - {ex.GetType().Name}: {ex.Message}"); hook = Task.CompletedTask; }
        if (hook.IsCompleted) { _modMomentRunning = false; leave(); return; }
        _ = LeaveAfter(hook, leave, _level, _nav.X, _nav.Y);
    }

    private async Task LeaveAfter(Task hook, System.Action leave, int level, int x, int y)
    {
        try { await hook; }
        catch (System.Exception ex) { CrashLog.Note($"dungeon: the stairs hook failed - {ex.GetType().Name}: {ex.Message}"); }
        finally { _modMomentRunning = false; }
        if (_ended || _level != level || _nav.X != x || _nav.Y != y || _ch.IsDead || _ch.Paralysed) return;
        FinishWonFight();
        leave();
    }

    // ------------------------------------------------------------ the fight's round (API version 14)

    /// <summary>
    /// The timer's tick in an engaged fight, before <see cref="PlayRound"/>: the mods'
    /// <see cref="ModActions.CombatRound"/>. With none hooked the host makes no context and returns,
    /// so nothing here is read or changed and the round is the original's. A failure in the host
    /// itself is noted and the round goes on.
    /// </summary>
    private void BeforeCombatRound()
    {
        if (_combat == null || _combat.IsOver) return;
        try { ModHost.Active.CombatRound(RoundContext); }
        catch (System.Exception ex) { CrashLog.Note($"dungeon: the combat round hook failed - {ex.GetType().Name}: {ex.Message}"); }
    }

    /// <summary>
    /// The fight as a mod sees it: an object of its own per fight, never the engine itself - the
    /// engine holds the monsters' hits and what each really is, which the game does not show.
    /// </summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<CombatEngine, object> _fightTokens = new();

    /// <summary>A context for one mod's round hook: this fight, the list as shown, the order commands.</summary>
    private CombatRoundContext RoundContext()
    {
        var fight = _combat!;
        // not the method group Lookup, whose Target would be this window: a closure over the table only
        var items = _itemsById;
        return new CombatRoundContext(_party, _spells, id => items.TryGetValue(id, out var it) ? it : null, _fightTokens.GetValue(fight, _ => new object()), ShownGroups(fight),
            m => RoundOrderOf(fight, m), m => AimedSlot(fight, m),
            (m, kind) => ModOrder(fight, m, kind), (m, s) => ModCastOrder(fight, m, s), (m, slot) => ModItemOrder(fight, m, slot),
            (m, slot) => ModAim(fight, m, slot));
    }

    /// <summary>
    /// The monster list's rows as <see cref="RefreshCombat"/> writes them (monShowMonsters,
    /// MONSMOD.BAS:4400-4404): the groups with someone standing, each named by <see cref="MonsterRow"/>'s
    /// own fn0E23 for the character in command, the count only where the row shows one.
    /// </summary>
    private IReadOnlyList<MonsterGroupView> ShownGroups(CombatEngine fight) =>
        CombatMonster.Runs(fight.Monsters)
            .Select((g, n) => (g, n))
            .Where(t => t.g.Any(m => m.Alive))
            .Select(t =>
            {
                var seen = t.g[0].Seen;
                int alive = t.g.Count(m => m.Alive);
                return new MonsterGroupView(t.n, EncounterGenerator.NameFor(seen, _ch, alive, t.g[0].Identified),
                                            seen.InGroup > 1 && seen.InGroup <= 12 ? alive : null,
                                            EncounterGenerator.NameFor(seen, _ch, 1, t.g[0].Identified));
            })
            .ToList();

    /// <summary>The member's order as the Option column shows it (with "Open" for a chest count); read without making an order for them.</summary>
    private CombatOrderView RoundOrderOf(CombatEngine fight, Character m)
    {
        // a chest count running is the Option column's "Open" (OptionOf, FRMGROUP.BAS:453-466)
        bool opening = fight.ChestRoundsOf(m) > 0;
        if (!_party.Orders.TryGetValue(m, out var o)) return new CombatOrderView(OrderKind.None, 0, null, opening);
        var io = fight.ItemOrders.TryGetValue(m, out var x) && x.ItemCast ? x.Slot : null;
        return new CombatOrderView(o.Kind, o.Kind == OrderKind.Spell ? o.SpellId : (short)0, io, opening);
    }

    /// <summary>
    /// What the keys f, d (for one who can act), space and Shift and a number do before their order:
    /// <c>If gv1E7C(Me) &gt; 0 Then gv1E7C(Me) = 0</c> and the caption cleared (MAINMOD2.BAS:3594-3600,
    /// 4011-4016, 4040-4044, 4137-4145; <see cref="CallOffChestCount"/>), for <paramref name="m"/>.
    /// </summary>
    private void CallOffChestCountOf(CombatEngine fight, Character m)
    {
        if (fight.ChestRoundsOf(m) <= 0) return;
        fight.ChestRounds[m] = 0;
        if (m == _ch) CombatHit.Text = "";
    }

    /// <summary>The slot the member aims at: the run holding <c>M547A</c>'s monster, if it still has someone standing.</summary>
    private int? AimedSlot(CombatEngine fight, Character m)
    {
        if (!_party.Orders.TryGetValue(m, out var o) || o.Target < 0 || o.Target >= fight.Monsters.Count) return null;
        var runs = CombatMonster.Runs(fight.Monsters);
        int at = runs.FindIndex(g => g.Contains(fight.Monsters[o.Target]));
        return at >= 0 && runs[at].Any(x => x.Alive) ? at : null;
    }

    /// <summary>The fight the context was made for is the one on, and the member is a living one of the party.</summary>
    private bool RoundOpenFor(CombatEngine fight, Character m) =>
        // the UI thread only: a command from a task the hook left running would change the orders
        // while the round reads them, and redraw from the wrong thread
        Avalonia.Threading.Dispatcher.UIThread.CheckAccess()
        && !_ended && _combat == fight && !fight.IsOver && m != null && _party.Members.Contains(m) && !m.IsDead;

    /// <summary>
    /// f, d or space for <paramref name="m"/>, as <see cref="SetOrder"/> gives them to the one in
    /// command (MAINMOD2.BAS:3594-3604, 4011-4054): the order set, the item slot let go (M5766 = -1),
    /// nothing said. d does nothing for one held fast (fn1521 case 68, MAINMOD2.BAS:3596).
    /// </summary>
    private bool ModOrder(CombatEngine fight, Character m, OrderKind kind)
    {
        if (!RoundOpenFor(fight, m)) return false;
        if (kind == OrderKind.Defend && m.Paralysed) return false;
        CallOffChestCountOf(fight, m);
        var o = _party.OrderFor(m);
        o.Kind = kind;
        o.SpellId = 0;
        KeepItemSlot(false, m);
        RefreshCombat();
        RefreshParty();
        return true;
    }

    /// <summary>
    /// A spell made <paramref name="m"/>'s order with monsters engaged, as a spell buffer fires it
    /// (MAINMOD2.BAS:4151-4158 into cmdCast_Click's engaged branch, FRMCHAR3.BAS:174-184; see
    /// <see cref="CastInCombat"/>): the spell becomes the order with M5766 set to -1 (4155), then
    /// mdrcharCheckCast, whose refusal - said in the "you" wording, as if <paramref name="m"/> were in
    /// command - leaves what it leaves (<see cref="RefusedOrder"/>). Nothing is thrown here: the round
    /// casts it.
    /// </summary>
    private bool ModCastOrder(CombatEngine fight, Character m, Spell spell)
    {
        if (!RoundOpenFor(fight, m) || spell == null) return false;
        CallOffChestCountOf(fight, m);   // the buffer key's (4137-4145)
        var o = _party.OrderFor(m);
        o.Kind = OrderKind.Spell;
        o.SpellId = spell.Id;
        KeepItemSlot(false, m);
        if (CheckCast(spell, 0, m, asCommander: true) is { } said) { RefusedOrder(said, m); return false; }
        RefreshCombat();
        RefreshParty();
        return true;
    }

    /// <summary>
    /// Use on <paramref name="m"/>'s pack slot in an engaged fight: sub15CD's guards
    /// (<c>ItemUse.Problem</c>, said as the Use button says them), then, for an item holding a spell,
    /// mdrcharCheckCast at the item's level and the item made the order - "Att = False : M56C4 =
    /// spell + 1000 : M5766 = slot" (MAINMOD2.BAS:6129-6137) - with nothing thrown and no charge
    /// spent; the round casts it (<see cref="CastInCombat"/>'s item branch). As <see cref="ModUseItem"/>,
    /// only an item known at All, and nothing for one held fast.
    /// </summary>
    private bool ModItemOrder(CombatEngine fight, Character m, InventorySlot slot)
    {
        if (!RoundOpenFor(fight, m) || slot == null || m.Paralysed) return false;
        // below All the game shows nothing of the item's spell: refused before it is looked at
        if (slot.Known != IdLevel.All) return false;
        if (!m.Inventory.Contains(slot) || Lookup(slot.ItemId) is not Item it) return false;
        if (ItemUse.SpellOf(it, _spells) is not Spell spell) return false;
        if (ItemUse.Problem(m, slot, it, Lookup) is string why)
        {
            if (why != "") Say(why);
            return false;
        }
        if (CheckCast(spell, it.SpellLvl, m, asCommander: true) is { } refused) { RefusedOrder(refused, m); return false; }
        // "Foes(0).Engaged = mc0138 : sub0BDE -1" (MAINMOD2.BAS:6131-6132): engaged already, sub0BDE runs
        // all the same - every member with no order drops a chest count and takes up their Combat Setting
        Sub0BDE();
        if (_combat != fight || fight.IsOver) return false;
        var o = _party.OrderFor(m);
        o.Kind = OrderKind.Spell;
        o.SpellId = spell.Id;
        fight.ItemOrders[m] = new CombatEngine.ItemOrder(slot, it, ItemCast: true);
        RefreshCombat();
        RefreshParty();
        return true;
    }

    /// <summary>
    /// A number key for <paramref name="m"/>, as <see cref="TargetGroup"/> for the one in command:
    /// "CombatOrders(Me).M547A = n" when that slot still has someone standing (MAINMOD2.BAS:4131-4136).
    /// </summary>
    private bool ModAim(CombatEngine fight, Character m, int slot)
    {
        if (!RoundOpenFor(fight, m)) return false;
        var runs = CombatMonster.Runs(fight.Monsters);
        if (slot < 0 || slot >= runs.Count) return false;
        var first = runs[slot].FirstOrDefault(x => x.Alive);
        if (first == null) return false;
        int index = fight.Monsters.IndexOf(first);
        if (index < 0) return false;
        _party.OrderFor(m).Target = index;
        ShowCursor();
        return true;
    }

    /// <summary>
    /// The target a mod gave, as the "Cast on Who?" box would have answered: null for a spell that
    /// asks nobody (it is ignored), a party member for one that asks; false when the spell asks and
    /// no party member was given.
    /// </summary>
    private bool PresetFor(Spell spell, Character? target, out SpellThrown.Target? preset)
    {
        preset = null;
        if (SpellThrown.WhoToAsk(spell) == SpellThrown.Ask.None) return true;
        if (target == null || !_party.Members.Contains(target)) return false;
        preset = new SpellThrown.Target.Person(target);
        return true;
    }

    /// <summary>
    /// <see cref="DungeonContext.Cast"/>: <see cref="CastKnownSpell"/> for <paramref name="caster"/>
    /// with the target given. Never while monsters are here (it would be a combat order), never for
    /// a dead caster (the sheet's Cast leaves for one), and only for <see cref="SpellThrown"/>'s
    /// cases, which ask nothing but "Cast on Who?".
    /// </summary>
    private async Task<bool> ModCast(Character caster, Spell spell, Character? target)
    {
        _lastRefusal = null;
        if (_ended || MonstersHere || caster == null || spell == null) return false;
        if (!_party.Members.Contains(caster) || caster.IsDead) return false;
        if (!SpellThrown.Handles(spell) || !PresetFor(spell, target, out var preset)) return false;
        var before = Spot;
        var (cast, msg) = await CastSpellHere(spell, itemLevel: 0, caster, preset);
        if (cast && SpellEffect.Of(spell) != SpellEffect.Passwall) Audio.PlaySpell(spell);
        SaveChar();
        ShowCastLine(msg);
        Redraw(sub0956: Spot != before);
        return cast;
    }

    /// <summary>
    /// <see cref="DungeonContext.UseItem"/>: the Use button (<see cref="OnSheetUse"/>,
    /// <see cref="UseSelectedItem"/>) for <paramref name="caster"/>'s slot - held fast, nothing;
    /// <c>ItemUse.Problem</c>'s refusals said as there - then <see cref="UseItemSpell"/> with the
    /// target given. Only an item that holds one of <see cref="SpellThrown"/>'s spells is used.
    /// </summary>
    private async Task<bool> ModUseItem(Character caster, InventorySlot slot, Character? target)
    {
        _lastRefusal = null;
        if (_ended || MonstersHere || caster == null || slot == null) return false;
        if (!_party.Members.Contains(caster) || caster.IsDead || caster.Paralysed) return false;
        // below All the game shows nothing of the item's spell (ItemInfoPage), so a mod learns
        // nothing about one either: refused before the item is looked at
        if (slot.Known != IdLevel.All) return false;
        if (!caster.Inventory.Contains(slot) || Lookup(slot.ItemId) is not Item it) return false;
        // the item a mod may use at all, before the game is asked anything about it
        if (ItemUse.SpellOf(it, _spells) is not Spell spell || !SpellThrown.Handles(spell)) return false;
        if (!PresetFor(spell, target, out var preset)) return false;
        // then sub15CD's own refusals, said as the Use button says them
        if (ItemUse.Problem(caster, slot, it, Lookup) is string why)
        {
            if (why != "") Say(why);
            return false;
        }
        return await UseItemSpell(slot, it, spell, caster, preset);
    }
}
