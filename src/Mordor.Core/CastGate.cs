namespace Mordor.Core;

/// <summary>Whether a spell may be thrown in a fight, out of one, or either (<c>fn149E</c>).</summary>
public enum SpellUse { Combat, NonCombat, Either }

/// <summary>
/// <c>mdrcharCheckCast</c> (MAINMOD2.BAS:502-545), the gate every cast passes through, and the
/// five things it can say.
///
/// The one that matters most is the combat/non-combat split, which the port did not have at all.
/// The help states it plainly: "COMBAT spells can only be cast in combat while NON-COMBAT spells
/// can only be cast when there are no monsters, or there are peaced monsters in the area", and
/// every spell's page in the help is headed one or the other. <c>fn149E</c> (MAINMOD2.BAS:2908)
/// works it out from the data rather than storing it:
///
/// <code>
///   If SpellDefs(s).Damage2 = 0 Or SpellDefs(s).Category = 9 Then non-combat Else combat
///   Select Case SpellDefs(s).Effect: Case 7, 10: either
/// </code>
///
/// Category 9 is the Heal school, so **healing is a non-combat spell** and cannot be thrown once
/// the monsters have engaged - the help says so on every heal page ("NON-COMBAT Spell, will heal
/// hits"). Teleport and Cure Paralysis are the two exceptions, by effect id, and they work in a
/// fight and out of one.
///
/// The <c>Effect</c> test is against the raw field, so the whole-party version of a spell - effect
/// plus 200 - does not match, and a mass Cure Paralysis is not one of the two exceptions.
/// </summary>
public static class CastGate
{
    /// <summary>MDATA2 Category 9, the Heal school (<see cref="Librarian.SpellSchools"/>).</summary>
    public const int HealSchool = 9;

    /// <summary>The two effects that may be thrown either way: Teleport and Cure Paralysis.</summary>
    public static SpellUse UseOf(Spell s)
    {
        if (s.Effect == SpellEffect.Teleport || s.Effect == SpellEffect.CureParalysis) return SpellUse.Either;
        return s.Damage2 == 0 || s.Category == HealSchool ? SpellUse.NonCombat : SpellUse.Combat;
    }

    /// <summary>
    /// The first three of <c>mdrcharCheckCast</c>'s refusals, which come before <see cref="Refuse"/>'s
    /// (MAINMOD2.BAS:437-497): the caster's guilds do not teach the spell at all, teach it at a
    /// higher spell level than the caster has, or a stat is below the spell's requirement.
    /// <paramref name="itemLevel"/> is <c>p0100</c>, an item's own spell level: anything but 0
    /// waves all three through (439, 444, 470, 482), so an item of level 0 is asked everything a
    /// spell of your own is. The sheet only lists spells you can cast, so these are reached from a
    /// buffer, and from an item of level 0.
    ///
    /// The stat line for anybody but you is the original's "RTrim(Name) + "s "" (489): "Anns
    /// Strength", with no apostrophe. Kept.
    /// </summary>
    public static Refusal? Unfit(Character caster, Spell s, int itemLevel, bool isYou, Func<short, Item?> item)
    {
        if (itemLevel != 0) return null;
        string name = caster.Name.TrimEnd();
        string q = "\"" + s.Name.TrimEnd() + "\"";

        // 437-443: a guild of yours with a rate for the school whose ceiling reaches the spell
        bool knows = false, levelOk = false;
        foreach (var m in caster.Guilds)
        {
            if (m.Level <= 0) continue;
            var def = GuildData.ByName(m.Guild);
            if (s.Category < 0 || s.Category >= def.SpellRate.Length) continue;
            float rate = def.SpellRate[s.Category];
            if (rate > 0 && def.SpellMax[s.Category] >= s.Level) knows = true;
            // 457-469: l011E is an Integer, so Level * rate rounds half to even; l0120 = Int(gl / 2 + .5)
            if (rate > 0 && (int)Math.Round(s.Level * (double)rate, MidpointRounding.ToEven)
                            <= (int)Math.Floor(m.Level / 2.0 + 0.5))
                levelOk = true;
        }
        if (!knows)
            return new(RefusalKind.NotKnown, isYou ? $"You do not know {q}." : $"\"{name}\" does not know {q}.");   // 449-451
        if (!levelOk)
            return new(RefusalKind.SpellLevel,
                       isYou ? $"Spell Level too low to cast {q}!"                           // 477
                             : $"\"{name}\", your Spell Level is too low to cast {q}!");     // 475

        // 483-496: Stats + StatMods against M5B4F, the six in order
        for (int i = 0; i < 6 && i < s.Required.Length; i++)
            if (caster.StatWithMods(i, item) < s.Required[i])
                return new(RefusalKind.Stat, isYou ? $"Your {RaceData.StatNamesLong[i]} is too low to cast {q}!"
                             : $"{name}s {RaceData.StatNamesLong[i]} is too low to cast {q}!");
        return null;
    }

    /// <summary>
    /// Why the cast is refused, or null to go ahead. The order is the routine's own, because the
    /// first refusal wins and they overlap: a heal you cannot afford says so about the points, not
    /// about the fight.
    ///
    /// <paramref name="isYou"/> picks between the two wordings of each line - the original writes
    /// a separate sentence for the character you are looking at and for anybody else in the party.
    /// <paramref name="monstersPresent"/> is <c>fn0EEF() &gt; 0</c> and <paramref name="engaged"/>
    /// is <c>Foes(0).Engaged</c>: monsters standing on your square that have not come for you are
    /// "peaced", and that is a third state, not a second.
    /// </summary>
    public static Refusal? Refuse(Character caster, Spell s, int cost, bool isYou,
                                 bool monstersPresent, bool engaged, bool antiMagic)
    {
        string name = caster.Name.TrimEnd();
        string q = "\"" + s.Name.TrimEnd() + "\"";

        if (cost > caster.CurSpellPoints)
            return new(RefusalKind.SpellPoints, isYou ? $"Not enough spell points to cast {q}!"
                         : $"{name} doesn't have enough spell points to cast {q}!");

        var use = UseOf(s);

        if (monstersPresent && engaged && use == SpellUse.NonCombat)
            return new(RefusalKind.NonCombat, isYou ? $"{q} is a Non-Combat Spell!"
                         : $"{q} is a Non-Combat Spell, {name}.");

        // an anti-magic square or an extinguish square: both swallow it, and the original says the
        // same four words for either (MAINMOD2.BAS:520-525)
        if (antiMagic) return new(RefusalKind.AntiMagic, AntiMagic);

        if (use is SpellUse.Combat or SpellUse.Either)
        {
            if (!monstersPresent)
            {
                if (use == SpellUse.Combat) return new(RefusalKind.CombatOnly, $"{q} is a Combat Spell!");
            }
            else if (!engaged && s.Level > PeacedSpellLevel)
            {
                return new(RefusalKind.TooLongPeaced, $"{q} Takes too long to throw when peaced, {name}.");
            }
        }
        return null;
    }

    /// <summary>
    /// What it says on an anti-magic or an extinguish square. The original also **perceives** the
    /// anti-magic bit as it refuses - forced, so it goes onto your map whatever your perception -
    /// and redraws that square (MAINMOD2.BAS:521-523). No spell points are spent: the check
    /// function leaves before anything is cast.
    /// </summary>
    public const string AntiMagic = "Anti-Magic area!";

    /// <summary>
    /// <c>mc014C</c>: throwing a spell above this level at monsters who have not engaged you takes
    /// too long, and is refused. At or below it the cast goes through and **starts the fight** -
    /// <c>Foes(0).Engaged = 1</c> on the next line (MAINMOD2.BAS:544).
    /// </summary>
    public const int PeacedSpellLevel = 7;
}

/// <summary>Which of <c>mdrcharCheckCast</c>'s refusals it was, by the branch that made it.</summary>
public enum RefusalKind
{
    NotKnown,       // MAINMOD2.BAS:449-453
    SpellLevel,     // 475-479
    Stat,           // 487-493
    SpellPoints,    // 506-508
    NonCombat,      // 514-518
    AntiMagic,      // 520-525, through sub102F
    CombatOnly,     // 530-533
    TooLongPeaced,  // 537-540
}

/// <summary>A refusal from <c>mdrcharCheckCast</c>: what it was, and the line it wrote.</summary>
public sealed record Refusal(RefusalKind Kind, string Text)
{
    /// <summary>
    /// The refusals that set M5766 = -1 as well as M56C4: Non-Combat (MAINMOD2.BAS:518), Combat
    /// Spell (533), too long when peaced (540), and anti-magic through sub102F (525; MULTIMOD.BAS:472,
    /// 477). Not knowing it, the spell level, a stat and the points leave it (453, 479, 493, 508).
    /// </summary>
    public bool ClearsItemSlot => Kind is RefusalKind.NonCombat or RefusalKind.AntiMagic
                                         or RefusalKind.CombatOnly or RefusalKind.TooLongPeaced;
}
