using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.Kaizen;

/// <summary>A heal Kaizen would cast next: who casts what on whom, and its average per spell point.</summary>
public sealed record HealChoice(Character Caster, Spell Spell, Character Target, double Average, int Cost)
{
    public double PerPoint => Average / Math.Max(1, Cost);
}

/// <summary>
/// The heal-out on the stairs up out of level 1, a simple greedy loop the player could run by hand:
///
/// - target: the living member missing the most hits (MaxHits − CurHits), party order on a tie;
///   nobody missing hits ends it. Companions are not looked at.
/// - caster: the first member, in party order, with a healing spell (<c>Spellbook.IsHealing</c>)
///   they can cast (<c>CastGate.Unfit</c> is null) and afford (spell points at least
///   <c>Spellbook.Cost</c>).
/// - spell: of that caster's affordable heals, the one with the most hits per spell point on
///   average - <c>Rules.SpellHealAverage</c> at the caster's spell level (their current guild's,
///   the level the cast heals at), not capped at what is missing; a party-wide heal counts it once
///   for every living member missing hits. The first such spell on a tie, in the spell page's order.
///
/// Each cast goes through the game. One that is refused, or spends nothing, takes that caster and
/// spell out of the loop; it stops after <see cref="MaxCasts"/> in any case, or as soon as monsters
/// are present.
/// </summary>
public static class HealOut
{
    /// <summary>A hard cap on the casts in one heal-out, so it cannot go on for ever.</summary>
    public const int MaxCasts = 250;

    /// <summary>The living member missing the most hits, the first in party order on a tie; null when nobody is.</summary>
    public static Character? Target(IReadOnlyList<Character> members)
    {
        Character? best = null;
        foreach (var m in members)
        {
            if (m.IsDead || m.MaxHits - m.CurHits <= 0) continue;
            if (best == null || m.MaxHits - m.CurHits > best.MaxHits - best.CurHits) best = m;
        }
        return best;
    }

    /// <summary>A heal this choice can cast: a heal, of the kind <see cref="SpellThrown"/> lands (the heal effects).</summary>
    public static bool IsHeal(Spell s) =>
        Spellbook.IsHealing(s) && SpellEffect.Of(s) is SpellEffect.Damage or SpellEffect.Cure;

    /// <summary>
    /// What a heal is worth on average: <c>Rules.SpellHealAverage</c> at the caster's spell level,
    /// times <paramref name="hurt"/> (the living members missing hits) for a party-wide one.
    /// </summary>
    public static double Average(Character caster, Spell s, int hurt)
    {
        double one = Rules.SpellHealAverage(s.Damage2, s.SpecialEffect + 1, Rules.SpellLevel(caster.Guild.Level));
        return SpellEffect.IsParty(s) ? one * hurt : one;
    }

    /// <summary>The next heal, or null: nobody hurt, or nobody able to heal.</summary>
    public static HealChoice? Choose(IReadOnlyList<Character> members, Func<Character, IReadOnlyList<Spell>> known,
                                     Func<short, Item?> item, Func<Character, Spell, bool> excluded)
    {
        var target = Target(members);
        if (target == null) return null;
        int hurt = members.Count(m => !m.IsDead && m.MaxHits - m.CurHits > 0);
        foreach (var c in members)
        {
            if (c.IsDead) continue;
            HealChoice? best = null;
            foreach (var s in known(c))
            {
                if (!IsHeal(s) || excluded(c, s)) continue;
                int cost = Spellbook.Cost(s, c);
                if (cost > c.CurSpellPoints || CastGate.Unfit(c, s, 0, true, item) != null) continue;
                var h = new HealChoice(c, s, target, Average(c, s, hurt), cost);
                if (best == null || h.PerPoint > best.PerPoint) best = h;
            }
            if (best != null) return best;
        }
        return null;
    }

    public static async Task Run(DungeonContext d)
    {
        var excluded = new HashSet<(Character, Spell)>();
        for (int n = 0; n < MaxCasts; n++)
        {
            if (d.MonstersPresent || !d.IsOpen) return;
            var h = Choose(d.Members, d.KnownSpells, d.Item, (c, s) => excluded.Contains((c, s)));
            if (h == null) return;
            int before = h.Caster.CurSpellPoints;
            bool went = await d.Cast(h.Caster, h.Spell, h.Target);
            // "Anti-Magic area!" swallows every cast here: stop at the first, as a player would
            if (d.LastRefusal == RefusalKind.AntiMagic) return;
            if (!went || h.Caster.CurSpellPoints >= before) excluded.Add((h.Caster, h.Spell));
        }
    }
}
