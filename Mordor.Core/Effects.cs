namespace Mordor.Core;

/// <summary>
/// What equipment and spells do to a character besides damage: six effect bits and eleven
/// resistances. Three procedures in MAINMOD2.BAS between them define the whole thing -
/// <c>sub120F</c> gathers what your gear grants, <c>sub1447</c> resets resistances to your race's,
/// and <c>sub0894</c> combines the two with whatever spells are running.
/// </summary>
public static class Effects
{
    /// <summary>The original's mc0060: how many resistance kinds there are.</summary>
    public const int ResistanceKinds = 11;

    /// <summary>The original's mc0318: how many effect bits an item or spell can carry.</summary>
    public const int EffectBits = 6;

    /// <summary>
    /// The original's mc031A. A matching equipped item sets the resistance to a flat 90 - it does
    /// not add, so a second such item buys you nothing.
    /// </summary>
    public const int GearResistance = 90;

    /// <summary>
    /// The effect bits an item carries in its Abilities word, MODULE1.BAS:518-523. The last two
    /// are combat abilities rather than visible states, and <c>sub0894</c> has an empty case for
    /// Critical Hit and no case at all for Backstab.
    /// </summary>
    public const int Levitate = 0, Invisible = 1, Protect = 2, SeeInvisible = 3,
                     CriticalHit = 4, Backstab = 5;

    /// <summary>
    /// Spells record the same four states at *different* bit positions (MAINMOD2.BAS:59-65:
    /// mc0206 = 3, mc020C = 4, mc0210 = 5, mc0214 = 6), which is what makes the Levitate quirk in
    /// <see cref="Recompute"/> happen.
    /// </summary>
    public const int SpellLevitate = 3, SpellInvisible = 4, SpellSeeInvisible = 5, SpellProtect = 6;

    /// <summary>Does this equipped item count for its wearer? Unaligned, or of their alignment.</summary>
    public static bool Counts(Character c, InventorySlot slot) => slot.Equipped && c.ItemHelps(slot);

    /// <summary>
    /// The effect bits the character's gear grants (the original's M5977), from <c>sub120F</c>.
    /// </summary>
    public static int GearEffects(Character c, Func<short, Item?> item)
    {
        int mask = 0;
        foreach (var slot in c.Inventory)
        {
            if (!Counts(c, slot)) continue;
            var it = item(slot.ItemId);
            if (it == null || it.Abilities <= 0) continue;
            for (int bit = 0; bit < EffectBits; bit++)
                if ((it.Abilities & (1 << bit)) != 0) mask |= 1 << bit;
        }
        return mask;
    }

    /// <summary>
    /// The character's resistance to one kind, as a percentage. Race base, then a flat 90 if any
    /// equipped item resists it, then whatever a spell has raised it to if that is higher.
    /// </summary>
    public static int ResistancePercent(Character c, int kind, Func<short, Item?> item)
    {
        int value = c.RaceDef?.Resistances is { } baseline && kind < baseline.Length ? baseline[kind] : 0;

        foreach (var slot in c.Inventory)
        {
            if (!Counts(c, slot)) continue;
            var it = item(slot.ItemId);
            if (it != null && (it.ResistanceFlags & (1 << kind)) != 0) { value = GearResistance; break; }
        }

        if (kind < c.SpellResist.Length && c.SpellResist[kind] > value) value = c.SpellResist[kind];
        return value;
    }

    /// <summary>
    /// Recompute the four visible states from gear and spells, the original's <c>sub0894</c>.
    ///
    /// The loop there runs over bits 0 to 5 of gear *and* spell masks together, reading both with
    /// the gear meanings, and four explicit tests afterwards read the spell mask again with the
    /// spell meanings. The explicit tests set the right states; the loop is what leaves a quirk -
    /// a spell that sets bit 3 means Levitate, but the loop's `Case 3` reads bit 3 as See
    /// Invisible, so casting Levitate also grants See Invisible. Reproduced.
    /// </summary>
    public static void Recompute(Character c, Func<short, Item?> item)
    {
        c.Levitating = false;
        c.InvisibleNow = false;
        c.SeeingInvisible = false;
        c.Protection = 0;

        int gear = GearEffects(c, item);
        int spell = c.SpellEffects;

        for (int bit = 0; bit < EffectBits; bit++)
        {
            if ((gear & (1 << bit)) == 0 && (spell & (1 << bit)) == 0) continue;
            switch (bit)
            {
                case Levitate: c.Levitating = true; break;
                case Invisible: c.InvisibleNow = true; break;
                case Protect: c.Protection = c.Guild.Level / 2 + 1; break;
                case SeeInvisible: c.SeeingInvisible = true; break;
                case CriticalHit: break;        // an empty case in the original
            }
        }

        if ((spell & (1 << SpellLevitate)) != 0) c.Levitating = true;
        if ((spell & (1 << SpellInvisible)) != 0) c.InvisibleNow = true;
        if ((spell & (1 << SpellProtect)) != 0) c.Protection = c.Guild.Level / 2 + 1;
        if ((spell & (1 << SpellSeeInvisible)) != 0) c.SeeingInvisible = true;
    }

    /// <summary>
    /// Strip everything a spell was holding up - the mask and the spell-granted resistances. This
    /// is what the Blackout chest trap does (MONSMOD.BAS:3605-3609) and what an antimagic square
    /// does (MAZEMOD.BAS:1745-1747); both then recompute.
    /// </summary>
    public static void ClearSpellEffects(Character c, Func<short, Item?> item)
    {
        c.SpellEffects = 0;
        for (int i = 0; i < c.SpellResist.Length; i++) c.SpellResist[i] = 0;
        Recompute(c, item);
    }
}
