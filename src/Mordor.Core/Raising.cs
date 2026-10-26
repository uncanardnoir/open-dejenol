namespace Mordor.Core;

/// <summary>What happened when a raise was attempted, in the order the original says it.</summary>
public sealed class RaiseOutcome
{
    /// <summary>Is the character on their feet again?</summary>
    public bool Raised { get; set; }
    /// <summary>Did the raise fail but drag them back anyway, at the price below?</summary>
    public bool Complications { get; set; }
    /// <summary>Did anything come off because the stats no longer carry it?</summary>
    public bool Unequipped { get; set; }
    /// <summary>The lines to show, in the original's wording.</summary>
    public List<string> Lines { get; } = new();
}

/// <summary>
/// What a raise costs, whether it works or not. <c>fn16B5</c> (MAINMOD2.BAS:330-380) resolves every
/// raise - the three spells and the morgue alike - and <c>raiseComplications</c>
/// (MAINMOD2.BAS:1642-1685) is the branch a failure takes.
///
/// The port had neither. A failed raise simply said "Raise Failed!" and a successful one cost
/// nothing at all, which left the whole price of dying out of the game.
/// </summary>
public static class Raising
{
    /// <summary>M58DC, the kind of raise being attempted: the three spell effects, and the morgue.</summary>
    public const int ByRaiseDead = 20, ByRestoreFlesh = 21, ByResurrect = 22, ByMorgue = 23;

    /// <summary>Days of age a raise costs: mc00B4, mc00C4 for stone, mc00B8 for rock.</summary>
    public const int AgeDaysOrdinary = 54, AgeDaysStoned = 84, AgeDaysRock = 124;

    /// <summary>mc02EA: complications add 5,475 days on top - fifteen years.</summary>
    public const int ComplicationAgeDays = 5475;

    /// <summary>MAINMOD2.BAS:1664 - <c>MaxHits = Int(MaxHits / 1.2#)</c>, the community's "* 0.8333".</summary>
    public const double ComplicationHitsDivisor = 1.2;

    /// <summary>
    /// The percentage chance that a failed raise brings complications rather than simply failing,
    /// MAINMOD2.BAS:1651:
    ///
    ///     Int(Rnd * 100) &lt; ((Chars(c).Age / 365) / (Races(r).Age / 2)) * 100
    ///
    /// which is the community's `(AgeInYears * 2 / RaceMaxAge) * 100`. Half your race's lifespan is
    /// the point where it becomes certain: a 16 year old Human (lifespan 100) is on 32%, an Elf
    /// (400) on 8%, a Dwarf (275) on 11.6%.
    /// </summary>
    public static double ComplicationChance(int ageDays, int raceLifespanYears) =>
        raceLifespanYears <= 0 ? 0 : ageDays / 365.0 / (raceLifespanYears / 2.0) * 100;

    /// <summary>
    /// Whether this failure complicates. Being pulled into rock always does, whatever your age -
    /// the <c>Or Chars(c).DeathState = mc00B6</c> half of the same line, mc00B6 being 8, Rock.
    /// </summary>
    public static bool Complicates(Random rng, Character c, int raceLifespanYears) =>
        c.CauseOfDeath == Rules.DeathRock ||
        rng.Next(100) < ComplicationChance(c.Age, raceLifespanYears);

    /// <summary>Days a raise ages you, by what you died of.</summary>
    public static int AgeDaysFor(int causeOfDeath) => causeOfDeath switch
    {
        Rules.DeathStoned => AgeDaysStoned,
        Rules.DeathRock => AgeDaysRock,
        _ => AgeDaysOrdinary,
    };

    /// <summary>
    /// The price of complications, MAINMOD2.BAS:1654-1677, in that order: five off every stat but
    /// never below the racial minimum, fifteen years of age, max hits divided by 1.2, hits filled
    /// to the new maximum, and anything the stats no longer carry taken off.
    /// </summary>
    public static void ApplyComplications(Character c, Race race, Func<short, Item?> lookup, RaiseOutcome outcome,
                                          bool inAParty = false)
    {
        outcome.Complications = true;
        outcome.Lines.Add("There are complications...");   // the box comes first (MAINMOD2.BAS:1653)
        c.Complications++;   // M5906(2), MAINMOD2.BAS:1654
        for (int i = 0; i < 6; i++)
        {
            c.Stats[i] -= 5;
            if (i < race.Min.Length && c.Stats[i] < race.Min[i]) c.Stats[i] = race.Min[i];
        }

        // mdrcharAddAge True, pv02D4, mc02EA, False (MAINMOD2.BAS:1663) says "... is now 782 week(s)
        // and 1 day(s) older." before the hits drop and the unequipped box
        outcome.Lines.Add(c.AgeBy(ComplicationAgeDays, inAParty));
        c.MaxHits = (int)(c.MaxHits / ComplicationHitsDivisor);
        if (c.MaxHits < 1) c.MaxHits = 1;
        c.CurHits = c.MaxHits;

        // fn11B6 is asked about every equipped slot and anything it refuses comes off. The stats
        // have just dropped by five, so this is the stat arm of that check rather than the guild
        // one GearCheck handles.
        for (int i = 1; i < Character.PackSize; i++)
        {
            if (c.Pack[i] is not InventorySlot slot || !slot.Equipped || slot.ItemId == 0) continue;
            var it = lookup(slot.ItemId);
            if (it == null) continue;
            bool tooWeak = false;
            for (int s = 0; s < 6 && s < it.StatsRequired.Length; s++)
                if (c.Stats[s] < it.StatsRequired[s]) { tooWeak = true; break; }
            if (!tooWeak) continue;
            slot.Equipped = false;
            outcome.Unequipped = true;
        }

        if (outcome.Unequipped) outcome.Lines.Add("One or more items were unequipped due to lack of stats...");
    }

    /// <summary>
    /// Resolve a raise that has already been paid for and allowed. <paramref name="chance"/> is the
    /// percentage the caller computed; <c>fn16B5</c> rolls <c>Int(Rnd * 100) &gt; chance</c> for the
    /// failure, so the chance is a threshold the roll must not exceed.
    ///
    /// Being pulled into rock zeroes the chance outright before the roll (MAINMOD2.BAS:332-334),
    /// which is why that death always ends up in the complications branch.
    /// </summary>
    public static RaiseOutcome Resolve(Random rng, Character c, Race race, int kind, int chance,
                                       Func<short, Item?> lookup, bool ownBody = false, bool inAParty = false)
    {
        var outcome = new RaiseOutcome();
        int roll = chance;
        if (c.CauseOfDeath == Rules.DeathRock) roll = 0;

        if (rng.Next(100) > roll)
        {
            outcome.Lines.Add("Raise Failed!");
            if (!Complicates(rng, c, race.MaxAge))
            {
                // The quiet failure still ages them, and says so: mdrcharAddAge True, pv02D4, mc00B4,
                // False (MAINMOD2.BAS:1680) before the False return.
                outcome.Lines.Add(c.AgeBy(AgeDaysOrdinary, inAParty));
                return outcome;                     // still dead
            }
            ApplyComplications(c, race, lookup, outcome, inAParty);
        }
        else outcome.Lines.Add("Success!");

        // Stone costs a point of Constitution whether the raise went smoothly or not
        // (MAINMOD2.BAS:352-355), and it is charged before the morgue's own roll.
        if (c.CauseOfDeath == Rules.DeathStoned)
        {
            c.Stats[(int)Stat.Con] -= 1;
            outcome.Lines.Add(StatDecreased(c, (int)Stat.Con, 1, inAParty));
        }

        // The own-body morgue ages you before its Constitution roll (MAINMOD2.BAS:3109, 3112); fn16B5
        // after it (363). Both say the line, mdrcharAddAge being called with p088C = True.
        if (ownBody) outcome.Lines.Add(c.AgeBy(AgeDaysFor(c.CauseOfDeath), inAParty));

        // Only the morgue asks for this one. The spells do not: MAINMOD2.BAS:357 tests
        // `Chars(c).M58DC = mc00C6` and mc00C6 is 23, the morgue's own raise kind. The community
        // page states it of "a successful raise" without that qualification.
        if (kind == ByMorgue && rng.NextDouble() * 100 < Rules.RaiseConLossChance(c.Stats[(int)Stat.Con]))
        {
            // Your own body in the morgue is a different routine (MAINMOD2.BAS:3087-3118) with its own
            // line - "You", and Constitution spelled right - and it then lifts Constitution back to
            // the race's minimum, which fn16B5 does not. The roll order is the same.
            outcome.Lines.Add(ownBody ? "MORGUE: You lost a point of Constitution during the raise."
                                      : $"MORGUE: \"{c.Name.Trim()}\" lost a point of Constution during the raise.");
            c.Stats[(int)Stat.Con] -= 1;
        }
        if (ownBody && (int)Stat.Con < race.Min.Length && c.Stats[(int)Stat.Con] < race.Min[(int)Stat.Con])
            c.Stats[(int)Stat.Con] = race.Min[(int)Stat.Con];

        if (!ownBody) outcome.Lines.Add(c.AgeBy(AgeDaysFor(c.CauseOfDeath), inAParty));
        c.IsDead = false;
        c.Stoned = false;
        c.CauseOfDeath = Rules.DeathOrdinary;
        // MAINMOD2.BAS:364-371: Raise Dead and Restore Flesh give you one hit, Resurrect and the
        // morgue give you all of them. A complicated raise has already set hits to the new maximum.
        c.CurHits = kind is ByResurrect or ByMorgue ? c.MaxHits : 1;
        outcome.Raised = true;
        return outcome;
    }

    /// <summary>
    /// <c>mdrcharStatRaiseMessage</c> (MAINMOD2.BAS:5541-5562) with a negative amount. The original
    /// picks the wording on party size and on the character's sex, and names the stat in full.
    /// </summary>
    public static string StatDecreased(Character c, int stat, int by, bool inAParty = false)
    {
        string name = stat >= 0 && stat < RaceData.StatNamesLong.Length ? RaceData.StatNamesLong[stat] : "stat";
        if (!inAParty) return $"Your {name} has decreased by {by}.";
        return $"\"{c.Name.Trim()}\" had {(c.Sex == Sex.Female ? "her" : "his")} {name} decreased by {by}.";
    }
}
