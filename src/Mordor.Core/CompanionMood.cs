namespace Mordor.Core;

/// <summary>
/// <c>sub0C00</c> (MODULE4.BAS:604-660), which runs every combat round for every character's
/// companions: a companion that is not bound for life drifts towards leaving, says so as it goes,
/// and eventually goes - or turns on you on the way out.
///
/// The help puts it this way: "Discontent companions (companions who have joined free of will) are
/// likely to leave if they become too discontent while bound companions are likely to turn on their
/// master."
///
/// A companion's binding is signed. Positive is <b>bound</b> - charmed or paid for - and negative is
/// <b>content</b>, one that came along of its own accord. Either way the number walks towards zero,
/// and zero is when it goes.
/// </summary>
public static class CompanionMood
{
    /// <summary>The result of one round for one companion.</summary>
    public sealed record Step
    {
        /// <summary>The binding moved a notch, and this is what the game said about it.</summary>
        public string? Drifted { get; init; }
        /// <summary>It is leaving. <see cref="Left"/> is what the game says if it does not turn.</summary>
        public bool Leaving { get; init; }
        /// <summary>The line for a companion that simply goes.</summary>
        public string? Left { get; init; }
    }

    /// <summary>
    /// One round for one companion. The caller runs <see cref="CompanionTurn.Check"/> when
    /// <see cref="Step.Leaving"/> comes back set: if that turns the companion into a monster, this
    /// line is not said, because the original only says "Left!" on the branch where it did not.
    /// </summary>
    public static Step Round(Character who, int index, Monster def, Random rng, bool isLeader)
    {
        // Ref > -1 And Hits > 0 (sub0C00, MONSMOD.BAS:617): an empty slot or a fallen companion is passed over
        if (who.Companions[index] is not Companion c || c.Hits <= 0) return new Step();

        // gc1D78: bound for life, and nothing below moves it
        if (c.BindingLevel >= Binding.ForLife) return new Step();

        string? drifted = null;
        if (rng.NextDouble() * 200 < Drift(who, c, def))
        {
            // towards zero from either side
            c.BindingLevel += c.BindingLevel > 0 ? -1 : 1;
            drifted = c.BindingLevel >= 0
                ? $"\"{who.Name.TrimEnd()}\" - {who.CompanionName(index)} has broken a level of binding!"
                : $"\"{who.Name.TrimEnd()}\" - {who.CompanionName(index)} is becoming restless!";
        }

        bool leaving = rng.NextDouble() * 200 < Binding.ChanceToLeave(
                           c.BindingLevel, def.GuildLevel, who.TopGuildLevel,
                           who.Stats.Length > (int)Stat.Cha ? who.Stats[(int)Stat.Cha] : 10,
                           c.Hits, c.MaxHits)
                       || c.BindingLevel == 0;

        return new Step
        {
            Drifted = drifted,
            Leaving = leaving,
            Left = leaving
                ? isLeader ? $"{who.CompanionName(index)} Left!"
                           : $"\"{who.Name.TrimEnd()}\": {who.CompanionName(index)} Left!"
                : null,
        };
    }

    /// <summary>
    /// How fast the binding walks towards zero, rolled against <c>Rnd * 200</c>.
    ///
    /// A <b>content</b> companion drifts on how hurt it is and nothing else: all the way down at
    /// full health, fifteen at death's door. A <b>bound</b> one drifts on how strong the creature is
    /// against how strong you are and how tightly it is held, so a powerful companion on a weak
    /// master slips fast and a well-paid binding on a high-level character barely moves.
    ///
    /// Each has its own floor - half a point for a content one, a whole point for a bound one - so
    /// no companion is ever held completely still.
    /// </summary>
    public static double Drift(Character who, Companion c, Monster def)
    {
        double drift;
        if (c.BindingLevel < 0)
        {
            drift = c.MaxHits <= 0 ? 0 : (double)(c.MaxHits - c.Hits) / c.MaxHits * 15;
            return Math.Max(0.5, drift);
        }
        double strength = def.GuildLevel / Math.Log(3) * 2;
        double hold = who.TopGuildLevel / Math.Log(12) * Math.Abs(c.BindingLevel / 2.0);
        drift = strength - hold;
        return Math.Max(1, drift);
    }
}
