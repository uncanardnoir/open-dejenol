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
