namespace Mordor.Core;

/// <summary>
/// Which creature a companion is. The original keeps the monster table index, <c>Ref</c>, in the
/// companion record and looks it up by that alone: <c>Monsters(Companions(i).Ref)</c> for its name
/// (MAINMOD2.BAS:4735), its size (2050), its turning chances (MONSMOD.BAS:539-542), its Confinement
/// price and pen (FRMCITYC.BAS:288-291, 705), its Library record (FRMCITYC.BAS:194, FRMCHAR3.BAS:747-757).
/// Beside it sits <c>M57D4</c>, the creature's Id (fn1163, MAINMOD2.BAS:388-389).
///
/// The port used to keep only the Id, and Ids are not unique: MDATA5 gives 307 to the Morgul
/// (index 278, Neutral) and the Shadow Ninja (index 389, Evil). Companions now carry the Ref, and
/// a record saved before they did is settled from its Id here, once, on load.
/// </summary>
public static class CompanionRef
{
    /// <summary>
    /// The creature at table index <paramref name="r"/>. A list straight from the loader is the
    /// table itself; a filtered list, or one built by hand (every <see cref="Monster.Ref"/> -1),
    /// is searched or read by place.
    /// </summary>
    public static Monster? At(IReadOnlyList<Monster> table, int r)
    {
        if (r < 0) return null;
        if (r < table.Count && (table[r].Ref == r || table[r].Ref < 0)) return table[r];
        foreach (var m in table) if (m.Ref == r) return m;
        return null;
    }

    /// <summary>The table index of a creature in this list: its own Ref, or its place for one built by hand.</summary>
    public static int IndexOf(IReadOnlyList<Monster> table, Monster m)
    {
        if (m.Ref >= 0) return m.Ref;
        for (int i = 0; i < table.Count; i++) if (ReferenceEquals(table[i], m)) return i;
        return -1;
    }

    /// <summary>The creature this companion is: <c>Monsters(Ref)</c>, Ref settled as <see cref="Resolve"/> would.</summary>
    public static Monster? Def(this Companion c, IReadOnlyList<Monster> table) => At(table, Resolve(c, table));

    /// <summary>
    /// A companion of this creature, filled Ref and Id both as fn1163 fills one (MAINMOD2.BAS:388-389)
    /// and as cmdBuy_Click does (FRMCITYC.BAS:128-129). The rest is the caller's.
    /// </summary>
    public static Companion Of(Monster def) => new() { Ref = def.Ref, MonsterId = def.Id };

    /// <summary>
    /// The Ref this record should have, without changing it.
    ///
    /// A Ref that names a creature of the record's Id stands. One that does not is what the
    /// original's COMPANION SORTER puts right (mdrcharShowCompanions, MAINMOD2.BAS:4720-4727):
    /// the first table index with that Id, <c>Exit For</c>; with none the Ref is left as it was.
    ///
    /// A Ref of -1 on an occupied record is the port's own legacy, a save from before it kept the
    /// Ref (the original's -1 is an empty slot, never a companion). <see cref="FromId"/>.
    /// </summary>
    public static int Resolve(Companion c, IReadOnlyList<Monster> table)
    {
        if (c.Ref >= 0)
        {
            var at = At(table, c.Ref);
            if (at != null && at.Id == c.MonsterId) return c.Ref;
            // a record given its Ref and no Id at all (the port's own, built by hand): the Ref is
            // all it says, so it stands rather than being sent to the creature of Id 0
            if (at != null && c.MonsterId == 0) return c.Ref;
            int first = FirstWithId(table, c.MonsterId);
            return first >= 0 ? first : c.Ref;
        }
        return FromId(table, c.MonsterId, c.Alignment);
    }

    /// <summary>
    /// A legacy record's Ref, from the Id it was saved with. An Id that is one creature is that
    /// creature, exactly. An Id two creatures share (only 307 in the shipped data) is settled by
    /// the one thing the record holds that tells them apart, its alignment: fn1163 gives a charmed
    /// or joined companion the creature's own (MAINMOD2.BAS:392, Foes().Alignment from
    /// MONSMOD.BAS:1880), so a Neutral 307 is the Morgul and an Evil one the Shadow Ninja. When
    /// that does not settle it (Good, which only the Confinement's realign gives, or Unaligned)
    /// the original's own rule for a record whose Ref it cannot trust applies - the first table
    /// index with the Id (MAINMOD2.BAS:4722-4727), the Morgul. No creature with the Id: -1.
    /// </summary>
    public static int FromId(IReadOnlyList<Monster> table, short id, ItemAlignment alignment)
    {
        var withId = new List<int>();
        for (int i = 0; i < table.Count; i++)
            if (table[i].Id == id) withId.Add(IndexOf(table, table[i]));
        if (withId.Count <= 1) return withId.Count == 1 ? withId[0] : -1;
        if (alignment != ItemAlignment.Unaligned)
        {
            var same = withId.Where(r => At(table, r) is Monster m && Confinement.AlignmentOf(m) == alignment).ToList();
            if (same.Count == 1) return same[0];
        }
        return withId[0];
    }

    private static int FirstWithId(IReadOnlyList<Monster> table, short id)
    {
        for (int i = 0; i < table.Count; i++)
            if (table[i].Id == id) return IndexOf(table, table[i]);
        return -1;
    }

    /// <summary>
    /// Settle every companion of a character on load: the Ref written in (<see cref="Resolve"/>),
    /// and a companion saved Unaligned given its creature's own alignment.
    ///
    /// The original never has an Unaligned companion: fn1163 gives a charmed or joined one the
    /// foe's alignment, the creature's own (MAINMOD2.BAS:392, MONSMOD.BAS:1880), and the
    /// Confinement's Buy and Realign set one of G N E (FRMCITYC.BAS:134, 261). Only the port's old
    /// charm and join left one Unaligned - they had not copied it. Now that the Ref says which
    /// creature it is, its own alignment is known exactly. Answers how many records changed.
    /// </summary>
    public static int Settle(CompanionSlots slots, IReadOnlyList<Monster> table)
    {
        int n = 0;
        foreach (var (_, c) in slots.Occupied)
        {
            bool changed = false;
            int r = Resolve(c, table);
            if (r != c.Ref) { c.Ref = r; changed = true; }
            if (c.Alignment == ItemAlignment.Unaligned && At(table, c.Ref) is Monster def)
            {
                c.Alignment = Confinement.AlignmentOf(def);
                changed = true;
            }
            if (changed) n++;
        }
        return n;
    }
}
