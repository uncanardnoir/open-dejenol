namespace Mordor.Core;

/// <summary>
/// <c>mdrcharGetCarry</c> (MAINMOD2.BAS:2027-2084): run whenever a companion's hits reach zero,
/// it decides which fallen companion the owner picks up and lets the rest go.
///
/// **Choosing** happens only while the owner carries nothing or a companion (<c>M5943 &lt; 4</c>;
/// four and up is a body from the morgue list). Slots 0 to 3 are walked; every occupied one at or
/// below zero hits is floored at zero and is a candidate. The first candidate is taken; a later
/// one replaces it only with strictly more MaxHits, and each time that happens the game says
/// <c>"Name" dropped &lt;the carried slot&gt; to carry &lt;the new one&gt;.</c> - naming the slot
/// in <c>M5943</c>, not the candidate it is replacing.
///
/// **Picking up**, when the winner is not already the carried slot: the monster's size less the
/// owner's race size must be at most one. Then it is carried - "You are now carrying X." to the
/// character in command, "Name, you are now carrying X." to anybody else. Otherwise the winner is
/// let go on the spot - "X was too big to carry.", prefixed by the owner's name when the owner is
/// not the one on screen.
///
/// **The sweep** always runs last: every occupied slot at exactly zero hits that is not the
/// carried one is let go, silently.
///
/// The original's faults, kept:
///
///   - With nothing carried (<c>M5943 = -1</c>) and a second candidate outweighing the first, the
///     "dropped" line asks for companion slot -1, which is out of range. The error handler resumes
///     at the End Sub (MAINMOD2.BAS:2079-2082), so nothing is picked up and the sweep never runs:
///     both bodies stay in their slots, uncarried, and the same thing happens at the next call.
///     The handler's message goes through <c>sub04FC</c>, which shows it only under /DEBUG.
///   - The "dropped" line is said before the size test, so it is said even when the new body
///     then proves too big and the old one is kept.
///   - While a morgue body is carried the choice is skipped, and the sweep lets every fallen
///     companion go at once.
///   - A companion stoned by a monster (MONSMOD.BAS:3239) or by a companion (1261) has its slot
///     emptied first; if it was the one carried, the carry is left pointing at an empty slot.
/// </summary>
public static class CompanionCarry
{
    /// <summary>
    /// Run it for <paramref name="who"/>. <paramref name="inCommand"/> is <c>pv0360 = Me</c>; the
    /// port keeps one index where the original keeps <c>Me</c> and <c>gv1A82</c>, so it also
    /// answers the too-big line's <c>pv0360 = gv1A82</c>. Returns the lines for the message bar
    /// (<c>sub085B</c>), in order.
    /// </summary>
    public static List<string> Run(Character who, bool inCommand, Func<Companion, Monster?> monsterOf)
    {
        var said = new List<string>();
        var slots = who.Companions;
        const int four = CompanionSlots.Slots;   // mc00E0

        if (who.CarryingCompanion < four && who.CarryingCharacter == "")
        {
            int best = -1;   // l0364
            for (int i = 0; i < four; i++)
            {
                if (slots[i] is not Companion c || c.Hits > 0) continue;
                c.Hits = 0;
                if (best > -1)
                {
                    if (c.MaxHits > slots[best]!.MaxHits)
                    {
                        // fn128B(pv0360, M5943) with M5943 = -1 reads Companions(-1): the error
                        // handler resumes at the End Sub (mdrcharGetCarry, MAINMOD2.BAS:2079-2082)
                        if (who.CarryingCompanion < 0) return said;
                        said.Add($"\"{who.Name.TrimEnd()}\" dropped {who.CompanionName(who.CarryingCompanion)} to carry {who.CompanionName(i)}.");
                        best = i;
                    }
                }
                else best = i;
            }

            if (best > -1 && who.CarryingCompanion != best)
            {
                // Monsters(Ref).M5565 - Races(RaceIndex).M5565 <= 1 (MAINMOD2.BAS:2050); a monster
                // the data cannot find would be Monsters() out of range, the same handler again
                if (monsterOf(slots[best]!) is not Monster def) return said;
                if (def.Size - (int)RaceData.ByName(who.Race).Size <= 1)
                {
                    who.CarryingCompanion = best;
                    said.Add(inCommand
                        ? $"You are now carrying {who.CompanionName(best)}."
                        : $"{who.Name.TrimEnd()}, you are now carrying {who.CompanionName(best)}.");
                }
                else
                {
                    string name = who.CompanionName(best);
                    slots.Release(best);
                    said.Add(inCommand
                        ? $"{name} was too big to carry."
                        : $"{who.Name.TrimEnd()}, {name} was too big to carry.");
                }
                // mdrcharShowCompanions pv0360 (MAINMOD2.BAS:2067), which blanks the empty slots' names
                // when the owner is the one on screen - before the sweep below lets any more go
                if (inCommand) slots.BlankEmptyNames();
            }
        }

        // the sweep (MAINMOD2.BAS:2071-2077): Hits = 0 and not the one carried. A morgue body is
        // four and up, so no slot number matches it; the port keeps it apart as CarryingCharacter
        for (int i = 0; i < four; i++)
            if (slots[i] is Companion c && c.Hits == 0
                && (who.CarryingCharacter != "" || who.CarryingCompanion != i))
                slots.Release(i);
        return said;
    }
}
