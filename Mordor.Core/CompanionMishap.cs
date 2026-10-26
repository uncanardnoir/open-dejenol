namespace Mordor.Core;

/// <summary>
/// Whether a companion's special attack goes wrong and lands on your own side, <c>fn0D64</c>
/// (MONSMOD.BAS:2020-2052).
///
/// A companion with a breath, spit or spell attack rolls this before it fires. If it comes up, the
/// attack hits a member of your own party instead of the monsters - that character takes the
/// damage, every one of *their* companions takes it too, and the line that follows begins
/// "X: Companion #1 accidently breathed on ...". If it does not come up, the attack goes at the
/// monsters as intended.
///
/// The chance is
///
///     (999 / companion's guild level) * (bodies on your side / 20) * a factor per attack kind
///
/// clamped per kind. Two things fall out of that shape and both are the original's:
///
/// - a **weak** companion is far more dangerous than a strong one. The guild level is in the
///   denominator, so a level 1 creature starts at 999 and is pinned to the ceiling of its clamp
///   whatever the party looks like, while a level 500 one is near the floor.
/// - a **bigger party** misfires more often, because every character and every living companion
///   counts towards <c>fn125B</c>.
///
/// Then the victim is drawn from the party at random - and if the draw lands on the companion's
/// **own owner** the result is zero, which the caller reads as "no mishap" and sends the attack at
/// the monsters. So a companion can breathe on anybody in the party except the person holding its
/// leash.
/// </summary>
public static class CompanionMishap
{
    /// <summary>Which of the three special attacks is being made; each has its own odds.</summary>
    public enum Kind { Breath, Spit, Spell }

    /// <summary>
    /// <c>fn125B</c> (MAINMOD2.BAS:2012-2024): how many bodies are on your side - every character,
    /// plus every companion that is alive.
    ///
    /// The companion loop runs <c>0 To mc00E0 - 1</c>, which is 0 to 3, so a fifth companion would
    /// not be counted. That is the same four-of-five bound the acid draw reads past; see
    /// docs/FIDELITY.md.
    /// </summary>
    public static int BodiesOnYourSide(Party party)
    {
        int n = 0;
        foreach (var c in party.Members)
        {
            n++;
            for (int slot = 0; slot < 4; slot++)
                if (c.Companions[slot] is Companion comp && comp.Hits > 0) n++;
        }
        return n;
    }

    /// <summary>
    /// The percentage chance that this attack goes astray, before the victim is drawn. The factor
    /// and the clamp are per kind: breath 2 and 2..40, spell 1.5 and 1..40, spit 1 and 0.5..20.
    /// </summary>
    public static double Chance(Kind kind, int companionGuildLevel, int bodies)
    {
        double level = companionGuildLevel <= 0 ? 1 : companionGuildLevel;
        var (factor, low, high) = kind switch
        {
            Kind.Breath => (2.0, 2.0, 40.0),
            Kind.Spell => (1.5, 1.0, 40.0),
            _ => (1.0, 0.5, 20.0),
        };
        double c = (999.0 / level) * (bodies / 20.0) * factor;
        return Math.Clamp(c, low, high);
    }

    /// <summary>
    /// Who the attack lands on, or null when it goes at the monsters as intended.
    ///
    /// <paramref name="owner"/> is the party index of whoever holds the companion. The draw is
    /// <c>CInt(Rnd * UBound(Chars)) + 1</c> - a VB Integer conversion, so it rounds half to even
    /// rather than truncating - and a draw that picks the owner is turned into "no mishap" rather
    /// than into somebody else.
    /// </summary>
    public static int? Victim(Random rng, Kind kind, int companionGuildLevel, Party party, int owner)
    {
        if (party.Members.Count == 0) return null;
        if (!(rng.NextDouble() * 100 < Chance(kind, companionGuildLevel, BodiesOnYourSide(party))))
            return null;

        int last = party.Members.Count - 1;
        int drawn = (int)Math.Round(rng.NextDouble() * last, MidpointRounding.ToEven) + 1;
        return drawn == owner + 1 ? null : drawn - 1;
    }
}
