using Mordor.Core;

namespace Mordor.Mod.QoL;

/// <summary>
/// Bank sort: the vault put in the order the pack's Sort uses (<see cref="PackSort"/>) - worn
/// classes, then consumables, then Miscellaneous Object; by class, then the type where it is known,
/// then the name as the character knows it, then the old order. The items fill the vault from its
/// first slot and the empty slots (holes a withdrawal left) go last, so the Bank's list, which
/// draws the slots in order and skips the holes, shows them sorted.
///
/// Each record moves whole (charges, alignment, how well it is known, the cursed bit). Nothing
/// points at a vault slot once a drag is over - the buffers name pack slots only - so nothing else
/// has to follow.
/// </summary>
public static class BankSort
{
    /// <summary>The new order: <c>order[newSlot] = oldSlot</c> for every vault slot there is.</summary>
    public static int[] Order(Character c, Func<short, Item?> item)
    {
        var full = new List<(int Slot, (int, int, string, int) Key)>();
        var empty = new List<int>();
        for (int i = 0; i < c.Bank.Count; i++)
        {
            var s = c.Bank[i];
            if (s == null || s.ItemId <= 0) { empty.Add(i); continue; }
            var it = item(s.ItemId);
            full.Add((i, it == null ? (int.MaxValue, int.MaxValue, "", i) : PackSort.KeyFor(s, it, i)));
        }
        return full.OrderBy(f => f.Key, PackSort.KeyOrder).Select(f => f.Slot).Concat(empty).ToArray();
    }

    /// <summary>Put the vault in <paramref name="order"/> (<c>order[newSlot] = oldSlot</c>).</summary>
    public static void Apply(Character c, int[] order)
    {
        var old = c.Bank.ToArray();
        for (int n = 0; n < order.Length; n++) c.Bank[n] = old[order[n]];
    }

    /// <summary>The Bank's Sort button. Says so as the pack's does; true when anything moved (the host then saves), false for a vault already in order.</summary>
    public static bool Run(Mordor.Modding.BankCommandContext ctx)
    {
        var c = ctx.Character;
        var order = Order(c, ctx.Item);
        bool moved = order.Where((old, n) => old != n).Any();
        if (moved) Apply(c, order);
        ctx.Say("Bank sorted.");
        return moved;
    }
}
