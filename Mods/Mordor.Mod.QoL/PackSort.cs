using Mordor.Core;

namespace Mordor.Mod.QoL;

/// <summary>
/// Item sort: the pack put in order of item type, then name.
///
/// <list type="bullet">
///   <item>Slot 1 (Hands, slot 0 here) never moves - the swap refuses it too (MAINMOD2.BAS:3426-3431).</item>
///   <item>The carried items come next, then every empty slot.</item>
///   <item>Each slot moves whole, so what is worn, its charges, alignment, how well it is known and
///   its curse flag all go with the item.</item>
///   <item>A spell buffer holding a pack slot follows the item in it, as a dragged swap moves them
///   (FRMCHAR3.BAS:571-577): a sort is only a string of such swaps.</item>
/// </list>
///
/// How well each item is known comes first (the player's wish, 2026-10-25): fully identified items,
/// then mostly, partly and barely known ones below them, so the unidentified gather at the bottom
/// rather than among their types. Within each level, "type" is the type as the character knows it,
/// so a sort never tells more than the pack's own names do: first the item's class (Weapon, Head
/// Protection... - which even a barely known item's name gives away), then within the class the
/// exact type where it is known (partly identified or better; not-yet-known types first), then the
/// name as shown, then the old order.
/// </summary>
public static class PackSort
{
    public static (int Class, int Type, string Name, int Slot) KeyFor(InventorySlot s, Item it, int slot) =>
        (KnownBand(s.Known) + ClassOf(it), s.Known >= IdLevel.Partial ? it.Type : -1,
         Loot.NameFor(it, s.Known, s.Alignment, s.Charges), slot);

    /// <summary>The identification level as the first part of the key: All 0, Mostly, Partial, Barely after.</summary>
    private static int KnownBand(IdLevel known) => (IdLevel.All - known) * 10_000;

    /// <summary>
    /// The order of two keys: class, then known type, then the name as shown (case ignored), then
    /// the old place. The pack's sort and the Bank's (<see cref="BankSort"/>) both use it.
    /// </summary>
    public static readonly Comparer<(int, int, string, int)> KeyOrder = Comparer<(int, int, string, int)>.Create((a, b) =>
    {
        int r = a.Item1.CompareTo(b.Item1);
        if (r == 0) r = a.Item2.CompareTo(b.Item2);
        if (r == 0) r = string.Compare(a.Item3, b.Item3, StringComparison.OrdinalIgnoreCase);
        if (r == 0) r = a.Item4.CompareTo(b.Item4);
        return r;
    });

    /// <summary>
    /// Worn classes first (a class worn one at a time: MaxWornOfClass above nought), then the
    /// consumables (nought), then Miscellaneous Object (no cap); by class number within each. The
    /// class numbers alone interleave them - Liquid (10) comes before Neck Ornament (15).
    /// </summary>
    private static int ClassOf(Item it)
    {
        int c = Character.WearClassOf(it);
        if (c < 0) return 9_999;           // an item with no wear class: last within its level
        int max = Character.MaxWornOfClass(c);
        int group = max > 0 ? 0 : max == 0 ? 1 : 2;
        return group * 1000 + c;
    }

    /// <summary>
    /// The new order: <c>order[newSlot] = oldSlot</c> for every slot, Hands (0) first. Carried items
    /// sorted, then the empty slots in their old order.
    /// </summary>
    public static int[] Order(Character c, Func<short, Item?> item)
    {
        var full = new List<(int Slot, (int, int, string, int) Key)>();
        var empty = new List<int>();
        for (int i = 1; i < Character.PackSize; i++)
        {
            var s = c.Pack[i];
            if (s == null || s.ItemId == 0) { empty.Add(i); continue; }
            var it = item(s.ItemId);
            // an id the table does not know sorts after everything, keeping its place among its kind
            full.Add((i, it == null ? (int.MaxValue, int.MaxValue, "", i) : KeyFor(s, it, i)));
        }
        var order = new List<int> { 0 };
        order.AddRange(full.OrderBy(f => f.Key, KeyOrder).Select(f => f.Slot));
        order.AddRange(empty);
        return order.ToArray();
    }

    /// <summary>
    /// Put the pack in <paramref name="order"/> (<c>order[newSlot] = oldSlot</c>) and move every
    /// buffer that points at a pack slot along with its slot.
    /// </summary>
    public static void Apply(Character c, int[] order)
    {
        var old = c.Pack.ToArray();
        var newIndexOf = new int[Character.PackSize];
        for (int n = 0; n < order.Length; n++) newIndexOf[order[n]] = n;
        for (int n = 1; n < Character.PackSize; n++) c.PutInSlot(n, old[order[n]]);
        for (int b = 0; b < c.Buffers.Length; b++)
        {
            int buf = c.Buffers[b];
            if (!SpellBuffers.IsItem(buf)) continue;
            int slot = SpellBuffers.PackSlotOf(buf);
            if (slot > 0 && slot < Character.PackSize) c.Buffers[b] = SpellBuffers.ForItem(newIndexOf[slot]);
        }
    }

    /// <summary>
    /// The Sort button. Refused, silently, while paralysed - as Equip, Use and Drop are
    /// (FRMCHAR3.BAS:203) - but not in a fight: Swap Items, the command a sort stands for, has no
    /// such test (MAINMOD2.BAS:3415-3439).
    /// </summary>
    public static bool Run(Mordor.Modding.ObjectsCommandContext ctx)
    {
        var c = ctx.Character;
        if (c.Paralysed) return false;
        Apply(c, Order(c, ctx.Item));
        ctx.Say("Pack sorted.");
        return true;
    }
}
