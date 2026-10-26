namespace Mordor.Core;

/// <summary>
/// The end of <c>mdrcharCastSpell</c>'s non-combat branch (MAINMOD2.BAS:1358-1391): the line
/// written after a spell has done its work, for every effect that does not set <c>l01BA</c> to
/// say something of its own - heals, cures, the four states, resistances, Fate, both Sanctuary
/// spells and the chest charms.
///
///     caster not in the party     "Heal" was thrown...
///     party-wide (effect + 200)   "Heal" thrown on the party.
///     no target (l01B6 = -1)      "Sanctuary" thrown.
///     a companion                 "Heal" thrown on Companion #2.
///     a lone character            "Heal" thrown on yourself.
///     the character in command    "Ann" threw "Heal" on herself.
///     anybody else                "Ann" threw "Heal" on "Bob".
///
/// The name in the last two is <c>Chars(Me)</c>'s - the character in command, not necessarily
/// the caster - and the companion's name is taken from <c>fn128B(Me, slot)</c>, so from the same
/// character's list.
///
/// Heals, cures and the states replaced a set of this port's own lines ("You cast Heal. …",
/// "The poison leaves your body.", "Ann is warded.", "Fate turns: …", "This place is your
/// sanctuary.") and two refusals the original does not make ("You are not poisoned.", "… is
/// already that well protected.").
/// </summary>
public static class SpellThrown
{
    /// <summary>Who the spell landed on, as <c>l01B6</c> and <c>l01C4</c> leave it.</summary>
    public abstract record Target
    {
        public sealed record Party : Target;
        public sealed record Nobody : Target;
        public sealed record Person(Character Who) : Target;
        public sealed record Companion(int Slot) : Target;
    }

    /// <param name="commander">
    /// Who the line takes to be in command: <c>party.Selected</c> when null (every cast by hand). A
    /// mod's cast for another member (the Kaizen mod) stands for switching to that member and
    /// casting by hand, so it passes the caster and the line reads as it would then.
    /// </param>
    public static string Line(Mordor.Core.Party party, Spell s, Target t, Character? commander = null)
    {
        string q = "\"" + s.Name.TrimEnd() + "\"";
        var me = commander ?? party.Selected;
        return t switch
        {
            Target.Party => $"{q} thrown on the party.",
            Target.Nobody => $"{q} thrown.",
            Target.Companion c => $"{q} thrown on {me.CompanionName(c.Slot)}.",
            Target.Person p when party.Members.Count <= 1 => $"{q} thrown on yourself.",
            Target.Person p when p.Who == me =>
                $"\"{me.Name.TrimEnd()}\" threw {q} on {(me.Sex == Sex.Female ? "herself" : "himself")}.",
            Target.Person p => $"\"{me.Name.TrimEnd()}\" threw {q} on \"{p.Who.Name.TrimEnd()}\".",
            _ => $"{q} was thrown...",
        };
    }

    /// <summary>
    /// Whether this is one of the effects handled here: everything the closing line follows that
    /// needs no map - heals, cures, the four states, resistances, Fate and Set Sanctuary.
    /// </summary>
    public static bool Handles(Spell s)
    {
        int e = SpellEffect.Of(s);
        return e is SpellEffect.Damage or SpellEffect.Cure or SpellEffect.CurePoison or SpellEffect.CureDisease
                 or SpellEffect.CureParalysis or SpellEffect.Levitate or SpellEffect.Invisible
                 or SpellEffect.SeeInvisible or SpellEffect.Protect or SpellEffect.Fate or SpellEffect.SetSanctuary
            || SpellEffect.ResistKind(e) >= 0;
    }

    /// <summary>
    /// Do what the spell does to its target, as MAINMOD2.BAS:915-1115 and 1333-1337 do, and say
    /// where it went. <paramref name="healLevel"/> is the level a heal is thrown at.
    ///
    /// Where it lands, case by case:
    /// - **Heal** (0, and 18 which also clears poison and disease): each target rolled on its own,
    ///   capped at the maximum.
    /// - **Cure Poison / Disease**: cleared on the target. No refusal when there is nothing to
    ///   cure.
    /// - **Cure Paralysis** asks who, and then frees **the caster** if the caster is held - the
    ///   pick is only used for the line (925-929: <c>l01C8 = pv01A6 : sub0D1D pv01A6, 1, 0</c>).
    /// - **The four states**: the bit and the state set on each target, and no recompute. Protection is the caster's spell level
    ///   halved, plus one (1046).
    /// - **Resistances**: raised to 95 where lower, and nothing said where not.
    /// - **Fate** rerolls the caster (fateReroll, 1911-1916: an unused <c>Int(Rnd * 3)</c> first).
    ///   It never sets <c>l01B6</c>, which is therefore 0 - so the line names the party's first
    ///   character, whoever cast it.
    /// - **Set Sanctuary** records the caster's square; <c>l01B6 = -1</c>, "thrown."
    /// </summary>
    public static string Apply(Party party, Character caster, Spell s, Target t, int healLevel,
                               Random rng, Func<short, Item?> item, CombatEngine? fight, Character? commander = null)
    {
        int e = SpellEffect.Of(s);
        var people = t switch
        {
            Target.Party => party.Members.ToList(),
            Target.Person p => new List<Character> { p.Who },
            _ => new List<Character>(),
        };

        switch (e)
        {
            case SpellEffect.Damage:
            case SpellEffect.Cure:
                // "If SpellDefs(pv01A8).M5B33 > 0 Then" (MAINMOD2.BAS:931) - M5B33 is Damage2. With
                // none the case does nothing and asks nobody, and l01B6 is left at its nought, so
                // the closing line names the party's first character, as Fate's does
                if (s.Damage2 <= 0)
                {
                    if (t is Target.Nobody) t = new Target.Person(party.Members[0]);
                    break;
                }
                if (t is Target.Companion comp)
                {
                    if (caster.Companions[comp.Slot] is Companion c)
                    {
                        c.Hits = Math.Min(c.MaxHits, c.Hits + Rules.SpellHeal(rng, s.Damage2, s.SpecialEffect + 1, healLevel));
                    }
                    break;
                }
                foreach (var who in people)
                {
                    who.CurHits = Math.Min(who.MaxHits, who.CurHits + Rules.SpellHeal(rng, s.Damage2, s.SpecialEffect + 1, healLevel));
                    // Ailments(0) and (1) = False (MAINMOD2.BAS:974-977): one field each, the level
                    if (e == SpellEffect.Cure) { who.CurePoison(); who.CureDisease(); }
                }
                break;
            case SpellEffect.CurePoison:
                foreach (var who in people) who.CurePoison();    // 989
                break;
            case SpellEffect.CureDisease:
                foreach (var who in people) who.CureDisease();   // 996
                break;
            case SpellEffect.CureParalysis:
                // "If Chars(pv01A6).Ailments(mc01EC) > False Then ... sub0D1D pv01A6, 1, 0"
                // (MAINMOD2.BAS:925-927): the caster, fight or no fight - the pick names the line only
                if (caster.ParalysisLevel > 0) caster.Unparalyse(item);
                break;
            case SpellEffect.Levitate:
            case SpellEffect.Invisible:
            case SpellEffect.SeeInvisible:
            case SpellEffect.Protect:
                // "Chars(l01B6).Ailments(n) = ..." and the spell bit, and nothing else
                // (MAINMOD2.BAS:1008-1009, 1023-1024, 1033-1034, 1046-1047): no recomputeEffects.
                // The port had recomputed here, which handed a Levitate its See Invisible at once
                // (the leak waits for the next recompute in the original) and put Protection back to
                // the guild value before overwriting it.
                foreach (var who in people)
                {
                    who.SpellEffects |= 1 << e;
                    switch (e)
                    {
                        case SpellEffect.Levitate: who.Levitating = true; break;         // Ailments(3) = 1
                        case SpellEffect.Invisible: who.InvisibleNow = true; break;      // Ailments(4) = 1
                        case SpellEffect.SeeInvisible: who.SeeingInvisible = true; break; // Ailments(5) = True
                        // Ailments(6) = Int(casterSpellLevel(pv01A6, pv01A8) / 2) + 1 (1046)
                        default: who.Protection = Spellbook.CasterSpellLevel(caster, s) / 2 + 1; break;
                    }
                }
                break;
            case SpellEffect.Fate:
                rng.Next(3);                                            // l031E, never used
                caster.CurHits = (int)(rng.NextDouble() * caster.MaxHits) + 1;
                caster.CurSpellPoints = (int)(rng.NextDouble() * caster.MaxSpellPoints) + 1;
                t = new Target.Person(party.Members[0]);
                break;
            case SpellEffect.SetSanctuary:
                // stored 1-based, so that a zero means the spell has never been cast
                caster.SanctuaryX = caster.X + 1; caster.SanctuaryY = caster.Y + 1; caster.SanctuaryLevel = caster.DungeonLevel;
                t = new Target.Nobody();
                break;
            default:
                int kind = SpellEffect.ResistKind(e);
                if (kind >= 0)
                    foreach (var who in people)
                        if (who.SpellResist[kind] < SpellEffect.ResistGranted)
                        {
                            who.SpellResist[kind] = SpellEffect.ResistGranted;
                            Effects.Recompute(who, item);
                        }
                break;
        }
        return Line(party, s, t, commander);   // commander: see Line
    }

    /// <summary>What "Cast on Who?" offers for a spell, or nothing when it does not ask.</summary>
    public enum Ask { None, People, PeopleAndCompanions }

    /// <summary>
    /// The cases that call fn170D, and with which tag: SPELLHEAL offers the companions too, SPELL
    /// only the party (MAINMOD2.BAS:917-1115). A party-wide spell (effect 200 and up) asks nobody -
    /// but only the heal, the three states Levitate, Invisible and Protect, and the resistances
    /// have a party-wide form; See Invisible and the cures always ask.
    /// </summary>
    public static Ask WhoToAsk(Spell s)
    {
        int e = SpellEffect.Of(s);
        bool party = SpellEffect.IsParty(s);
        return e switch
        {
            // only "If SpellDefs(pv01A8).M5B33 > 0" (MAINMOD2.BAS:931)
            SpellEffect.Damage or SpellEffect.Cure => party || s.Damage2 <= 0 ? Ask.None : Ask.PeopleAndCompanions,
            SpellEffect.CureParalysis => Ask.PeopleAndCompanions,
            SpellEffect.CurePoison or SpellEffect.CureDisease or SpellEffect.SeeInvisible => Ask.People,
            SpellEffect.Levitate or SpellEffect.Invisible or SpellEffect.Protect => party ? Ask.None : Ask.People,
            _ when SpellEffect.ResistKind(e) >= 0 => party ? Ask.None : Ask.People,
            _ => Ask.None,
        };
    }
}
