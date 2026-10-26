namespace Mordor.Core;

/// <summary>
/// Which companion an acid spit lands on when it goes astray, <c>fn0DAB</c>
/// (MONSMOD.BAS:2100-2119).
///
/// The breath splashes everything the victim owns. The spit picks one thing: either the character
/// or a single one of their companions. This is the draw that decides which.
///
///     count = the character's living companions in slots 0 to 3
///     if count &gt; 0 and Rnd * 100 &lt; count / (count + 1) * 100
///         draw Int(Rnd * 4) until it lands on a living companion
///     else -1, meaning the character
///
/// So with one companion the spit hits it half the time, with two it is two in three, and with
/// three, three in four. The character is never safe and never the likeliest target.
///
/// **An uncertainty that has to be stated.** The single call site is
/// <c>fn0DAB(pv01E4, Companions(l0204).Ref, gc1DC2)</c> at MONSMOD.BAS:966, where
/// <c>pv01E4</c> is the character whose companion is attacking - and the slot it returns is then
/// used to index the **victim's** list, <c>Chars(l0214).Companions(l0216)</c>. The victim is never
/// the attacker, because <see cref="CompanionMishap"/> spares the owner, so the two lists are
/// always different ones.
///
/// Read that way it is a fault: a slot counted from the attacker's companions, applied to
/// somebody else's, can name an empty one. The decompiler recovered this function with a single
/// parameter while noting it is called with three, so the body may be using an argument that was
/// lost, and on that reading it would be the victim's list and correct. Nothing in the file
/// settles it - there is only the one call.
///
/// This port follows the text that exists: the count and the draw come from the attacker, as
/// written, and an empty slot is a miss rather than a crash - which is what the original's own
/// error handler makes of it.
/// </summary>
public static class CompanionSpit
{
    /// <summary>mc01A0 again: slots 0 to 3, the four of five that every loop here walks.</summary>
    public const int SlotsWalked = 4;

    /// <summary>
    /// The slot the spit lands on, or -1 for the character themselves. <paramref name="of"/> is
    /// whose companions are counted and drawn from - the attacker, as the one call site passes.
    /// </summary>
    public static int Slot(Random rng, Character of)
    {
        int count = 0;
        for (int i = 0; i < SlotsWalked; i++)
            if (of.Companions[i] is Companion c && c.Hits > 0) count++;
        if (count == 0) return -1;
        if (!(rng.NextDouble() * 100 < count / (double)(count + 1) * 100)) return -1;

        // Int(Rnd * 4) until it lands on one that is alive. The guard above guarantees there is
        // one, so the loop ends; a cap is kept anyway because this port must not hang on data the
        // original never saw.
        for (int guard = 0; guard < 1000; guard++)
        {
            int slot = (int)Math.Floor(rng.NextDouble() * SlotsWalked);
            if (of.Companions[slot] is Companion c && c.Hits > 0) return slot;
        }
        return -1;
    }
}
