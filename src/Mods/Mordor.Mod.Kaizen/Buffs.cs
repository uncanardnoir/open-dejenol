using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.Kaizen;

/// <summary>
/// The buff lists, run once on going down from town: every party member in party order, each one's
/// own list in its order (<see cref="BuffLists"/>), the member as the caster. Each row is done as
/// the player would do it by hand - the spell typed into Objects &amp; Spells, or the item's Use -
/// with the "Cast on Who?" answer taken from the row. Kaizen chooses nothing: not the spell, not
/// single or mass, not the caster. A row that cannot be done is skipped (with the game's own
/// refusal where the game has one, otherwise a line of the mod's own) and the next is tried; a row
/// whose spell asks "Cast on Who?" and whose target is not in the party now is skipped without a
/// word (lists are kept per character, and parties change); a party-wide row's target is not
/// looked at, as the cast by hand asks nobody.
/// </summary>
public static class Buffs
{
    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>A party member by name, or null.</summary>
    public static Character? Member(IReadOnlyList<Character> members, string name) =>
        members.FirstOrDefault(m => Same(m.Name, name));

    /// <summary>
    /// What the pack calls an item at the level it is known, without the charges or alignment
    /// tags: the item's own name once Mostly known, the type's name at Partial, the class's at
    /// Barely (<c>Loot.NameFor</c>). So a row can only find an item by what the player has been
    /// told it is.
    /// </summary>
    public static string KnownName(Item it, IdLevel known) =>
        known >= IdLevel.Mostly ? it.Name.Trim() : Loot.NameFor(it, known).Trim();

    /// <summary>
    /// The pack slot a row names: the first whose item is known at All by that name, else the
    /// first the pack names so at a lower level (which is then skipped as not fully identified),
    /// or null.
    /// </summary>
    public static InventorySlot? ItemNamed(Character c, string name, Func<short, Item?> item)
    {
        var named = c.Inventory.Where(s => item(s.ItemId) is Item it && Same(KnownName(it, s.Known), name)).ToList();
        return named.FirstOrDefault(s => s.Known == IdLevel.All) ?? named.FirstOrDefault();
    }

    // ------------------------------------------------------------------------- the window's drops

    /// <summary>
    /// What a drop on the Kaizen window comes to (<see cref="Judge"/>): the row it adds (its target
    /// still to be asked when <see cref="AsksFor"/> is set), or a refusal to show (<see cref="Refusal"/>),
    /// or neither - a drop refused without a word (the Hands, an empty slot, another list's line).
    /// </summary>
    public sealed record DropVerdict(BuffRow? Row, Spell? AsksFor, string? Refusal)
    {
        public static readonly DropVerdict Silent = new(null, null, null);
        public static DropVerdict No(string why) => new(null, null, why);
    }

    /// <summary>
    /// Whether a line dragged from the game's own lists may become a row of <paramref name="owner"/>'s
    /// buff list: what the spell buffers take from that character's own Spells list and pack, and only
    /// what the player has been shown. A spell must be one they know (<paramref name="known"/>, the spell
    /// page's list) and one Kaizen can send (<see cref="SpellThrown.Handles"/>: a map spell, a raise or a
    /// bind asks more than "Cast on Who?" and is refused with the run's own words). An item must be in
    /// their pack known at <see cref="IdLevel.All"/>: below All nothing about it is looked at - not even
    /// whether it holds a spell - and the refusal names it only as the pack does. At All its spell must be
    /// one Kaizen can send. A line of anyone else's lists is refused: the list is the owner's.
    /// </summary>
    public static DropVerdict Judge(ModDragged d, Character owner, IReadOnlyList<Spell> known, IReadOnlyList<Spell> spells, Func<short, Item?> item)
    {
        if (!ReferenceEquals(d.Owner, owner))
            return DropVerdict.No($"Only \"{owner.Name.TrimEnd()}\"'s own spells and items go in this list.");
        if (d.Kind == ModDragKind.Spell)
        {
            if (d.SpellIn(spells) is not Spell s) return DropVerdict.Silent;
            string name = s.Name.Trim();
            if (!known.Any(k => Same(k.Name, name))) return DropVerdict.No($"\"{name}\" is not a spell \"{owner.Name.TrimEnd()}\" knows.");
            if (!SpellThrown.Handles(s)) return DropVerdict.No($"\"{name}\" asks for more than a target; cast it by hand.");
            return new DropVerdict(new BuffRow(BuffKind.Spell, name, ""), SpellThrown.WhoToAsk(s) != SpellThrown.Ask.None ? s : null, null);
        }
        if (d.Slot is not InventorySlot slot || item(slot.ItemId) is not Item it) return DropVerdict.Silent;
        // below All the game tells nothing of what the item casts, and neither may Kaizen
        if (slot.Known != IdLevel.All) return DropVerdict.No($"\"{KnownName(it, slot.Known)}\" is not fully identified; use it by hand.");
        string itemName = it.Name.Trim();
        if (itemName == "") return DropVerdict.Silent;
        if (ItemUse.SpellOf(it, spells) is not Spell held) return DropVerdict.No($"\"{itemName}\" holds no spell.");
        if (!SpellThrown.Handles(held)) return DropVerdict.No($"\"{itemName}\" asks for more than a target; use it by hand.");
        return new DropVerdict(new BuffRow(BuffKind.Item, itemName, ""), SpellThrown.WhoToAsk(held) != SpellThrown.Ask.None ? held : null, null);
    }

    /// <summary>
    /// A drop taken: <see cref="Judge"/>, then for a spell (or an item's spell) that asks "Cast on Who?"
    /// the game's own box (<paramref name="ask"/>, <see cref="ModGame.AskCastOnWho"/>), the row's target
    /// the member picked. A party-wide one asks nobody. The row, or null with the refusal to show (null
    /// for none: a silent refusal, or the box cancelled - which cancels the drop).
    /// </summary>
    public static async Task<(BuffRow? Row, string? Refusal)> Take(ModDragged d, Character owner, IReadOnlyList<Spell> known,
        IReadOnlyList<Spell> spells, Func<short, Item?> item, Func<Spell, Task<Character?>> ask)
    {
        var v = Judge(d, owner, known, spells, item);
        if (v.Row == null) return (null, v.Refusal);
        if (v.AsksFor == null) return (v.Row, null);
        var who = await ask(v.AsksFor);
        return who == null ? (null, null) : (v.Row with { Target = who.Name.Trim() }, null);
    }

    /// <summary>
    /// The spell a row casts, for the window: the spell by name, or the item's spell when the item is in
    /// the pack known at All now; null when it cannot be told (nothing about an item below All is looked at).
    /// </summary>
    public static Spell? RowSpell(BuffRow row, Character c, IReadOnlyList<Spell> spells, Func<short, Item?> item)
    {
        if (row.Kind == BuffKind.Spell) return spells.FirstOrDefault(x => Same(x.Name, row.Name));
        return ItemNamed(c, row.Name, item) is { Known: IdLevel.All } slot && item(slot.ItemId) is Item it ? ItemUse.SpellOf(it, spells) : null;
    }

    // ------------------------------------------------------------------------------------ the run

    /// <summary>Every member's list from <paramref name="lists"/>, by the member's name.</summary>
    public static Task Run(DungeonContext d, BuffLists lists) => Run(d, m => lists.For(m.Name));

    /// <summary>Every member's list, in party order, each in its own order.</summary>
    public static async Task Run(DungeonContext d, Func<Character, IReadOnlyList<BuffRow>> listOf)
    {
        foreach (var member in d.Members.ToList())
        {
            var rows = listOf(member).Where(r => r.Name.Trim() != "").ToList();
            if (rows.Count == 0) continue;
            // never a combat order: with monsters here nothing at all is done
            if (d.MonstersPresent || !d.IsOpen) return;
            // the sheet's Cast does nothing for the dead, and says nothing: one line for the list
            if (member.IsDead) { d.Say(KaizenMod.Line($"\"{member.Name.TrimEnd()}\" is dead.")); continue; }
            foreach (var row in rows)
            {
                if (d.MonstersPresent || !d.IsOpen) return;
                await Do(d, member, row);
                // "Anti-Magic area!": an anti-magic or extinguish square swallows every cast, and a
                // player would stop after the first one - so does the run, every list
                if (d.LastRefusal == RefusalKind.AntiMagic) return;
            }
        }
    }

    /// <summary>One row of <paramref name="caster"/>'s list: a spell or an item, then its target, each through the game.</summary>
    public static async Task Do(DungeonContext d, Character caster, BuffRow row)
    {
        if (row.Kind == BuffKind.Spell)
        {
            var spell = d.Spells.FirstOrDefault(s => Same(s.Name, row.Name));
            if (spell == null) { d.Say(KaizenMod.Line($"There is no spell called \"{row.Name.Trim()}\".")); return; }
            // a spell the caster does not know (now) is still sent, and the game says why it is refused
            if (!TargetFor(d, spell, row, out var target)) return;
            await d.Cast(caster, spell, target);
            return;
        }

        var slot = ItemNamed(caster, row.Name, d.Item);
        if (slot == null || d.Item(slot.ItemId) is not Item it)
        {
            d.Say(KaizenMod.Line($"\"{caster.Name.TrimEnd()}\" has no item called \"{row.Name.Trim()}\"."));
            return;
        }
        // below All the game tells nothing of what the item casts, and neither may Kaizen: the
        // line names it only as the pack does, and nothing about the item is looked at
        if (slot.Known != IdLevel.All)
        {
            d.Say(KaizenMod.Line($"\"{KnownName(it, slot.Known)}\" is not fully identified; use it by hand."));
            return;
        }
        var held = ItemUse.SpellOf(it, d.Spells);
        if (held == null) { d.Say(KaizenMod.Line($"\"{row.Name.Trim()}\" holds no spell.")); return; }
        if (!TargetFor(d, held, row, out var itemTarget)) return;
        await d.UseItem(caster, slot, itemTarget);
    }

    /// <summary>
    /// The row's answer to "Cast on Who?", for a spell Kaizen will send. A spell whose case asks for
    /// more than that (a map spell, a raise, a bind) is not sent, and said why. A spell that asks
    /// nobody (a party-wide one) ignores the row's target, as the cast by hand asks nobody. One that
    /// asks needs a target in the row (said if there is none); a target not in the party now passes
    /// the row over without a word - the list is the caster's, kept across parties.
    /// </summary>
    private static bool TargetFor(DungeonContext d, Spell s, BuffRow row, out Character? target)
    {
        target = null;
        string q = "\"" + s.Name.TrimEnd() + "\"";
        if (!SpellThrown.Handles(s)) { d.Say(KaizenMod.Line($"{q} asks for more than a target; cast it by hand.")); return false; }
        if (SpellThrown.WhoToAsk(s) == SpellThrown.Ask.None) return true;
        if (row.Target.Trim() == "") { d.Say(KaizenMod.Line($"{q} needs a target.")); return false; }
        target = Member(d.Members, row.Target);
        return target != null;
    }

    /// <summary>
    /// Whether the row's spell (or its item's spell) asks "Cast on Who?", for the Kaizen window's
    /// rows (a target shown, or "(party)") and its double-click: true or false when it is known, null when it cannot be told (an item not in the
    /// pack known at All now - nothing about it is looked at below All).
    /// </summary>
    public static bool? AsksTarget(BuffRow row, Character c, IReadOnlyList<Spell> spells, Func<short, Item?> item) =>
        RowSpell(row, c, spells, item) is Spell s ? SpellThrown.WhoToAsk(s) != SpellThrown.Ask.None : null;
}
