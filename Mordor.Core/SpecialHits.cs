namespace Mordor.Core;

/// <summary>
/// <c>monGetSpecialHits</c> (MONSMOD.BAS:2391-2460): how much a breath, an acid spit or an
/// electrocution takes off, whoever is throwing it and whoever it lands on.
///
/// The attacker is always a creature record - a monster, or a charmed one fighting for you. The
/// target may be either, and the original says which by the sign of the argument: a negative
/// number is a character (index <c>Abs(n + 1)</c>) and a positive one is a monster. What the sign
/// changes is where the two figures come from - the target's level, and the resistance that cuts
/// the damage down.
///
/// All three kinds share one range:
///
///     range = Int( (Log(attackerLevel + 1) + (attackerLevel + 1) / 15) / Log(1.08)
///                  - targetLevel / 10 + 1 )
///
/// and differ only in what is added to the roll and which resistance applies:
///
/// | kind | damage | resisted by |
/// |---|---|---|
/// | breath | <c>(Int((Rnd * range + 1) / 2) + range / 4) * 0.7</c> | Fire if the attacker breathes fire, else Cold |
/// | acid spit | the same with <c>range / 2</c> | Acid |
/// | electrocute | the same with <c>range / 2</c> | Electrical |
///
/// **This is not the formula a monster's own breath uses.** `sub0E85` has its own, inlined
/// (MONSMOD.BAS:2702-2704), and the two differ in three places: it starts from
/// <c>level * Log(level + 1)</c> rather than <c>level + 1</c>, it divides the target's level by 20
/// rather than 10, and it halves the result at the end. A monster breathing on you and a companion
/// breathing on you do not do the same arithmetic. Both are the original's and neither should be
/// tidied into the other.
/// </summary>
public static class SpecialHits
{
    /// <summary>Which special is being thrown.</summary>
    public enum Kind { Breath, AcidSpit, Electrocute }

    /// <summary>The eleven resistance kinds, in the order MDATA stores them.</summary>
    public const int Fire = 0, Cold = 1, Electrical = 2, Acid = 10;

    /// <summary>VB's <c>Int()</c>: the floor.</summary>
    private static int Floor(double v) => (int)Math.Floor(v);

    /// <summary>Assigning a Double to a VB Integer: round half to even.</summary>
    private static int VbInt(double v) => (int)Math.Round(v, MidpointRounding.ToEven);

    /// <summary>
    /// The range the roll is taken from, shared by all three kinds. <paramref name="targetLevel"/>
    /// is the character's top guild level or the monster's guild level, whichever the target is.
    /// </summary>
    public static int Range(int attackerGuildLevel, int targetLevel)
    {
        double a = attackerGuildLevel + 1;
        return Floor((Math.Log(a) + a / 15.0) / Math.Log(1.08) - targetLevel / 10.0 + 1);
    }

    /// <summary>
    /// The damage before resistance. <paramref name="rng"/> stands in for <c>Rnd</c>, and the result
    /// is rounded half to even because the original lands it in an Integer.
    /// </summary>
    public static int Raw(Random rng, Kind kind, int attackerGuildLevel, int targetLevel)
    {
        int range = Range(attackerGuildLevel, targetLevel);
        double share = kind == Kind.Breath ? range / 4.0 : range / 2.0;
        return VbInt((Floor((rng.NextDouble() * range + 1) / 2) + share) * 0.7);
    }

    /// <summary>
    /// What the resistance leaves, <c>dmg - dmg * ((resistance / 2) * .01)</c> rounded the same
    /// way. Half the resistance, as everywhere else a special attack is resisted.
    /// </summary>
    public static int AfterResistance(int damage, int resistancePercent) =>
        resistancePercent <= 0 ? damage : VbInt(damage - damage * (resistancePercent / 2.0 * 0.01));

    /// <summary>
    /// Which resistance cuts this attack down. Breath asks the **attacker** whether it breathes
    /// fire or cold - the bits are the attacker's, not the target's - so a cold-breathing creature
    /// is resisted by Cold even when the victim's Fire resistance is the higher of the two.
    /// </summary>
    public static int ResistedBy(Kind kind, bool attackerBreathesFire) => kind switch
    {
        Kind.Breath => attackerBreathesFire ? Fire : Cold,
        Kind.AcidSpit => Acid,
        _ => Electrical,
    };
}
