namespace Mordor.Core;

/// <summary>
/// The ten spell buffers each character carries, the original's <c>Chars(c).M59B0(0..9)</c>.
///
/// A buffer is a shortcut to one thing you do often. Each slot holds:
/// <list type="bullet">
///   <item>-1, nothing;</item>
///   <item>a number below <see cref="PackSlots"/>, meaning that slot of your pack - a wand, a
///   potion, whatever is there;</item>
///   <item><see cref="PackSlots"/> or more, meaning the spell at that index in the spell table.</item>
/// </list>
///
/// You fire one with Shift and a number key, 1 to 9 and then 0 for the tenth
/// (MAINMOD2.BAS:4138-4158). Firing a spell buffer while monsters are engaged sets it as your
/// standing order rather than casting once, which is how you keep a spell going every round.
///
/// The plain number keys do something else entirely: 1 to 4 pick which monster group you are
/// aiming at (MAINMOD2.BAS:4131-4136).
/// </summary>
public static class SpellBuffers
{
    /// <summary>mc0574: how many buffers a character has.</summary>
    public const int Count = 10;

    /// <summary>mc0190: the pack size, and the offset that separates an item from a spell.</summary>
    public const int PackSlots = 40;

    public const int Empty = -1;

    public static int ForItem(int packSlot) => packSlot;
    public static int ForSpell(int spellIndex) => PackSlots + spellIndex;

    public static bool IsEmpty(int buffer) => buffer < 0;
    public static bool IsItem(int buffer) => buffer >= 0 && buffer < PackSlots;
    public static bool IsSpell(int buffer) => buffer >= PackSlots;

    public static int PackSlotOf(int buffer) => buffer;
    public static int SpellIndexOf(int buffer) => buffer - PackSlots;

    /// <summary>
    /// A buffer holds the slot an item sits in, and now that slots are fixed that number stays
    /// right on its own - which is how the original works. All this has to do is let go when the
    /// slot it points at is emptied.
    /// </summary>
    public static void PackSlotEmptied(Character c, int slot)
    {
        for (int i = 0; i < Count && i < c.Buffers.Length; i++)
            if (IsItem(c.Buffers[i]) && PackSlotOf(c.Buffers[i]) == slot) c.Buffers[i] = Empty;
    }

    /// <summary>Two slots changed places, so any buffer pointing at either follows it.</summary>
    public static void PackSlotsSwapped(Character c, int a, int b)
    {
        for (int i = 0; i < Count && i < c.Buffers.Length; i++)
        {
            int buf = c.Buffers[i];
            if (!IsItem(buf)) continue;
            int slot = PackSlotOf(buf);
            if (slot == a) c.Buffers[i] = ForItem(b);
            else if (slot == b) c.Buffers[i] = ForItem(a);
        }
    }



    /// <summary>
    /// What the original prints beside each buffer (<c>mdcharShowBuffer</c>, MAINMOD2.BAS:4633).
    /// A buffer pointing at a pack slot that has gone empty resets itself to nothing.
    /// </summary>
    public static string Describe(Character c, int slot, IReadOnlyList<Spell> spells, Func<short, Item?> item)
    {
        if (slot < 0 || slot >= Count) return "(Nothing)";
        int b = c.Buffers[slot];

        // a spell past the end of the table shows as nothing but is not cleared (4639-4641) - only a
        // pack slot gone empty writes the buffer back
        if (IsSpell(b) && (spells == null || SpellIndexOf(b) >= spells.Count)) return "(Nothing)";
        if (IsEmpty(b)) return "(Nothing)";

        if (IsItem(b))
        {
            // A buffer holds a pack *slot*, which is what lblBuffer_DragDrop stores
            // (FRMCHAR1.BAS:220, Source.Index). This used to index the compacted inventory view
            // instead - a leftover from before the pack became forty fixed slots - so an item
            // buffer named whatever happened to be that far down the list rather than the item
            // actually in that slot, and the numbers did not match the ones the game prints.
            if (b <= 0 || b >= Character.PackSize) { c.Buffers[slot] = Empty; return "(Nothing)"; }
            var held = c.Pack[b];
            if (held == null || held.ItemId == 0) { c.Buffers[slot] = Empty; return "(Nothing)"; }
            var it = item(held.ItemId);
            if (it == null) { c.Buffers[slot] = Empty; return "(Nothing)"; }
            return $"Item #{b + 1}. {Loot.NameFor(it, held.Known, held.Alignment, held.Charges)}";
        }

        return $"\"{spells[SpellIndexOf(b)].Name}\"";
    }

    /// <summary>
    /// <c>lblBufferStat</c>, beside the slot (MAINMOD2.BAS:4656-4659): a spell's cost to this caster
    /// (<c>fn1485</c>), or "?" when it comes to nought - which it does when no guild of theirs teaches
    /// it. Nothing for an item or an empty slot.
    /// </summary>
    public static string CostText(Character c, int slot, IReadOnlyList<Spell> spells)
    {
        if (slot < 0 || slot >= Count) return "";
        int b = c.Buffers[slot];
        if (!IsSpell(b) || spells == null || SpellIndexOf(b) >= spells.Count) return "";
        int cost = Spellbook.Cost(spells[SpellIndexOf(b)], c);
        return cost == 0 ? "?" : cost.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
