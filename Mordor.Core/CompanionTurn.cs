namespace Mordor.Core;

/// <summary>
/// What happens when a companion is kicked out, or attacked: <c>fn0B6C</c>
/// (MONSMOD.BAS:524-600, <c>monCheckCompAttack</c>).
///
/// Kicking one out is the help's Companions page - "The buttons allow you to kick out a specific
/// companion whenever you wish and even turn and attack them" - and the Commands page's right-click
/// on a companion picture is the attack. Both go through this: the difference is the forced flag.
/// </summary>
public static class CompanionTurn
{
    /// <summary>mc0186: the original has four group slots and a turned companion needs a free one.</summary>
    public const int GroupSlots = 4;

    /// <summary><c>fn128B</c>. See <see cref="Character.CompanionName"/>, which is the one copy.</summary>
    public static string NameOf(Character who, int index) => who.CompanionName(index);

    /// <summary>What <see cref="Check"/> decided.</summary>
    public sealed record Outcome
    {
        /// <summary>The companion is now a monster and has been taken off the character.</summary>
        public bool Turned { get; init; }
        /// <summary>The monster it became, for the caller to put in the fight.</summary>
        public CombatMonster? Became { get; init; }
        /// <summary>What to say, or null for nothing.</summary>
        public string? Message { get; init; }
        /// <summary>
        /// It would have turned but the monsters already there are too far from it in alignment, so
        /// nothing happened at all - the companion stays with you.
        /// </summary>
        public bool Refused { get; init; }
    }

    /// <summary>
    /// The roll. <paramref name="forced"/> is the attack; without it a companion that joined of its
    /// own free will - binding at or below zero - never turns, however badly it is treated.
    ///
    /// The three chances are the original's, and they read off the **creature's** own alignment,
    /// <c>Monsters(l0178).Alignment</c> (MONSMOD.BAS:539-542) - not the companion's, which the
    /// Confinement can change: two steps away from yours always turns it, an Evil one turns three
    /// times in four, a Neutral one about one time in seven. A Good creature of a Good master is
    /// left with no chance at all.
    ///
    /// VB's <c>And</c> does not short-circuit, so lines 541 and 542 each draw their <c>Rnd</c>
    /// whatever the alignment, forced or not: two draws, in that order, every time past the
    /// free-will test (536-538).
    ///
    /// The refusal reads <c>Foes(0).Alignment</c> against the creature's own (546), and only while
    /// some group stands, <c>fn0DCD(-1) &gt; -1</c> (544, MONSMOD.BAS:2190-2209).
    /// </summary>
    public static Outcome Check(Character who, int index, bool forced, Random rng,
                                Func<Companion, Monster?> monsterDef,
                                IReadOnlyList<CombatMonster>? fight,
                                bool isLeader, int groupsPresent)
    {
        if (who.Companions[index] is not Companion c) return new Outcome();
        if (c.BindingLevel <= 0 && !forced) return new Outcome();

        var def = monsterDef(c);   // Monsters(Companions(p0170).Ref)
        if (def == null) return new Outcome();

        int mine = (int)who.Alignment;                   // 0 Good, 1 Neutral, 2 Evil
        int theirs = def.AlignmentValue;                 // Monsters(l0178).Alignment (540-542)
        bool turns = Math.Abs(theirs - mine) > 1;                                  // 540
        double evilRoll = rng.NextDouble() * 100;                                  // 541, drawn always
        if (theirs == (int)Alignment.Evil && evilRoll < 75) turns = true;
        double neutralRoll = rng.NextDouble() * 100;                               // 542, drawn always
        if (theirs == (int)Alignment.Neutral && neutralRoll < 15) turns = true;
        if (forced) turns = true;                                                  // 543

        // A fight already going: the companion will not join monsters two steps from its own
        // alignment, and the whole thing is called off - it does not leave either.
        if (fight != null && fight.Any(m => m.Alive))
        {
            // Foes(0): the first slot's record, standing or not; a list built by hand has no slots
            var first = fight.FirstOrDefault(m => m.Group == 0) ?? fight.First(m => m.Alive);
            int foes0 = first.Alignment >= 0 ? first.Alignment : first.Def.AlignmentValue;
            if (Math.Abs(foes0 - theirs) > 1)
                return new Outcome
                {
                    Refused = true,
                    Message = isLeader
                        ? "Your companion refuses to join the current monsters!"
                        : $"\"{who.Name.TrimEnd()}\" - Your companion refuses to join the current monsters!",
                };
        }

        // no free slot, no turning: the original looks for an empty one of four and gives up quietly
        if (!turns || groupsPresent >= GroupSlots) return new Outcome();

        string name = who.CompanionName(index);
        return new Outcome
        {
            Turned = true,
            Became = new CombatMonster
            {
                Def = def,
                Hits = c.Hits,
                // Foes(l0176).Alignment = Companions(p0170).Alignment (MONSMOD.BAS:569)
                Alignment = MonsterAlignment(def, c),
                Hostile = true,
                Identified = true,
            },
            Message = isLeader
                ? (forced ? $"You've attacked {name}!" : $"{name} has turned on you!")
                : $"\"{who.Name.TrimEnd()}\": {name} has turned on the party!",
        };
    }

    /// <summary>
    /// A companion carries its own alignment (<c>M4EBF</c>), which the Confinement can change, and a
    /// turned one takes it into the fight as its <c>Foes().Alignment</c> (MONSMOD.BAS:569); the
    /// creature's own stands in for one saved Unaligned. The turning roll itself reads the
    /// creature's own (540-542), not this.
    /// </summary>
    private static int MonsterAlignment(Monster def, Companion? c) =>
        c != null ? (int)c.Alignment switch
        {
            (int)ItemAlignment.Good => (int)Alignment.Good,
            (int)ItemAlignment.Neutral => (int)Alignment.Neutral,
            (int)ItemAlignment.Evil => (int)Alignment.Evil,
            _ => def.AlignmentValue,
        }
        : def.AlignmentValue;

    /// <summary>
    /// The two refusals in front of Kick Out, <c>cmdCompanion_Click</c> (FRMCHAR3.BAS:252-262).
    /// Returns null when it may go ahead.
    /// </summary>
    public static string? KickOutProblem(Character who, int index, bool inDungeon)
    {
        if (who.Companions[index] is not Companion c) return "";   // one button per companion in the original: nothing to pick, and nothing said
        if (c.Hits == 0)
            return "You must drop a dead companion you're carrying - not kick them out!";
        if (!inDungeon) return "You cannot kick out companions in the city!";
        return null;
    }

    /// <summary>The question, with the warning a bound companion earns.</summary>
    public static string KickOutQuestion(Character who, int index)
    {
        string q = $"Kick out {who.CompanionName(index)}?";
        if (who.Companions[index] is Companion c && c.BindingLevel > 0)
            q += "\r\n\r\nWARNING: Since the companion is bound, you will risk them turning on you!";
        return q;
    }
}
