namespace Mordor.Core;

/// <summary>
/// What a spell does. MDATA2 gives every spell an effect id, and MAINMOD2.BAS dispatches on it in
/// one long <c>Select Case</c> (lines 869-1353). The ids below are that case list, and each one is
/// confirmed by the shipped spell table: effect 3 is Feather Essense, 9 is Ethereal Portal, 15 is
/// Retrieve Soul, 150 is Life Domination, and so on.
///
/// An id of <see cref="PartyOffset"/> or more means the same effect cast on the whole party, with
/// the real effect that much lower - which is exactly how Mass Heal (200), Mass Levitate (203),
/// Mass Sight Veil (204) and Mass Protection (206) are built.
/// </summary>
public static class SpellEffect
{
    /// <summary>mc01D2: add this to an effect to cast it on everyone.</summary>
    public const int PartyOffset = 200;

    public const int Damage = 0;            // and healing: the branch keys off Damage2
    public const int CurePoison = 1;
    public const int CureDisease = 2;
    public const int Levitate = 3;          // Feather Essense
    public const int Invisible = 4;         // Sight Veil
    public const int SeeInvisible = 5;
    public const int Protect = 6;           // Protection
    public const int Teleport = 7;          // right, forwards, down (at most 3)
    public const int Displacement = 8;      // right, forwards
    public const int Passwall = 9;          // Ethereal Portal: one square through the wall
    public const int CureParalysis = 10;
    public const int DetectRock = 11;       // is the square ahead solid?
    public const int DepthPerception = 12;  // which level am I on
    public const int FindDirection = 13;    // which way am I facing
    public const int SoulSearch = 14;       // where is another character
    public const int RetrieveSoul = 15;     // bring another character here
    public const int SetSanctuary = 16;
    public const int Sanctuary = 17;        // return there, at a cost in days
    public const int Cure = 18;             // the general cure, handled with Damage
    public const int Fate = 19;             // reroll hits and spell points
    public const int RaiseFirst = 20, RaiseFull = 22, RaiseLast = 25;
    public const int OpenFirst = 30, OpenLast = 32;      // unlock a chest of level (id - 29) or less
    public const int BindFirst = 40, BindLast = 42;      // Control, Restrain, Domination
    public const int ResistFirst = 50;                   // ResistFirst + kind
    public const int CharmFirst = 80, CharmLast = 99;
    public const int Kill = 100;                         // Sleep, Flesh to Stone, Cause Death...
    public const int BindForLife = 150;                  // Life Domination

    /// <summary>mc0226 sets a resistance to this, if it is not already higher.</summary>
    public const int ResistGranted = 95;

    /// <summary>
    /// mc0282 = 365 (MAINMOD2.BAS:803): what a Sanctuary return costs in days of age, a year -
    /// passed to mdrcharAddAge at 1352. The port had 28.
    /// </summary>
    public const int SanctuaryDays = 365;

    /// <summary>The effect the spell really has, with the party offset taken off.</summary>
    public static int Of(Spell s) => s.Effect >= PartyOffset ? s.Effect - PartyOffset : s.Effect;

    /// <summary>Does this spell land on everyone rather than one target?</summary>
    public static bool IsParty(Spell s) => s.Effect >= PartyOffset;

    /// <summary>One of the four states a spell can put up (MAINMOD2.BAS:1008-1048).</summary>
    public static bool IsState(int effect) => effect is Levitate or Invisible or SeeInvisible or Protect;

    /// <summary>The resistance kind a resistance spell grants, or -1.</summary>
    public static int ResistKind(int effect) =>
        effect >= ResistFirst && effect < ResistFirst + Effects.ResistanceKinds ? effect - ResistFirst : -1;

    /// <summary>
    /// Apply an effect that needs nothing but the character: the four states, the resistances,
    /// Fate, and Set Sanctuary. Returns "" when it handled the effect, or null if this effect
    /// is not one of them.
    ///
    /// **The game does not cast through this any more** - every cast goes through
    /// <see cref="SpellThrown.Apply"/>, which sets the four states without a recompute, as
    /// MAINMOD2.BAS:1008-1047 does. This stays for the probes (selfcheck's protection and
    /// spell-state checks, FateProbe, StateLeakProbe), and for the states it is the cast *with*
    /// the next <c>recomputeEffects</c> already run - which is what shows Levitate's See Invisible
    /// leak - except that Protection's cast value is written after it.
    /// </summary>
    public static string? ApplyToCharacter(int effect, Character c, Spell s, Random rng, Func<short, Item?> item)
    {
        if (ResistKind(effect) is var kind && kind >= 0)
        {
            // raised to 95 where lower and nothing said where not (MAINMOD2.BAS:1106-1111); the line
            // that follows is SpellThrown's, and the port's own "X is warded." and "X is already that
            // well protected." are gone
            if (c.SpellResist[kind] < ResistGranted)
            {
                c.SpellResist[kind] = ResistGranted;
                Effects.Recompute(c, item);
            }
            return "";
        }

        switch (effect)
        {
            case Levitate:
            case Invisible:
            case SeeInvisible:
            case Protect:
                // A spell records its state at the bit position that equals its own effect id:
                // MAINMOD2.BAS sets bit mc0206 = 3 for effect mc0204 = 3, and so on up to 6.
                c.SpellEffects |= 1 << effect;
                Effects.Recompute(c, item);
                if (effect == Protect)
                {
                    // Protection is the one of the four that carries a number, and the number the
                    // cast writes is not the one recomputeEffects would. MAINMOD2.BAS:1046 sets
                    //
                    //     Chars(l01B6).Ailments(mc0214) = Int(casterSpellLevel(...) / 2) + 1
                    //
                    // - the caster's *spell* level halved - and that case does not call
                    // recomputeEffects afterwards, so the value stands. recomputeEffects itself
                    // (MAINMOD2.BAS:2805 and 2827) writes Int(currentGuild.Level / 2) + 1, roughly
                    // twice as much, and overwrites this the next time anything triggers it:
                    // equipping something, an antimagic square, a Blackout trap.
                    //
                    // So protection from a spell is worth about half what the same spell is worth
                    // after the character next changes gear. That is the "Buggy" the community's
                    // Spell_Damage_Formula page marks against the guild-spell protection line, and
                    // it is reproduced rather than smoothed over. Recompute is still called first,
                    // because the other three states come from it.
                    c.Protection = Spellbook.SpellLevelFor(c, s) / 2 + 1;
                }
                return "";   // the closing line is SpellThrown's

            case Fate:
                rng.Next(3);   // fateReroll's l031E = Int(Rnd * 3), drawn and never used (MAINMOD2.BAS:1913)
                c.CurHits = (int)(rng.NextDouble() * c.MaxHits) + 1;
                c.CurSpellPoints = (int)(rng.NextDouble() * c.MaxSpellPoints) + 1;
                return "";

            case SetSanctuary:
                // stored 1-based, so that a zero means the spell has never been cast
                c.SanctuaryX = c.X + 1; c.SanctuaryY = c.Y + 1; c.SanctuaryLevel = c.DungeonLevel;
                return "";

            default:
                return null;
        }
    }

    /// <summary>
    /// Where Displacement or Teleport puts you, <c>mdrcharTeleport</c> (MAINMOD2.BAS:5738).
    ///
    /// The numbers you type are *relative to the way you are facing* - right and forwards, not
    /// east and north - which is why the prompt says "Right, Forwards (ie +3,-2)". Stepping off
    /// the 30x30 grid wraps round to the other side rather than failing.
    ///
    /// Teleport takes a third number for depth, clamped to three levels either way. Nothing here
    /// checks what is at the far end: the help warns that if the leader "casts Teleport and lands
    /// in Rock, all members of the party will also land in Rock".
    /// </summary>
    public static (int X, int Y, int Level) TeleportTo(
        int x, int y, Facing facing, int level, int right, int forwards, int down = 0, int width = 30, int height = 30)
    {
        // 1-based while we do this, because the wrap is written around a 1..30 grid
        int nx = x + 1, ny = y + 1;
        switch (facing)
        {
            case Facing.North: nx += right; ny += forwards; break;
            case Facing.East: nx += forwards; ny -= right; break;
            case Facing.South: nx -= right; ny -= forwards; break;
            default: nx -= forwards; ny += right; break;
        }

        nx = Wrap(nx, width);
        ny = Wrap(ny, height);

        if (down != 0) level += Math.Clamp(down, -3, 3);
        if (level < 0) level = 0;
        return (nx - 1, ny - 1, level);
    }

    /// <summary>
    /// Where someone else is, in the terms Soul Search reports them: right, forward and down from
    /// where you are standing and the way you are facing (MAINMOD2.BAS:1292-1302). This is the
    /// exact inverse of <see cref="TeleportTo"/>, so the two can be checked against each other.
    /// </summary>
    public static (int Right, int Forward, int Down) RelativeDirection(
        int myX, int myY, int myLevel, Facing facing, int theirX, int theirY, int theirLevel)
    {
        int dx = theirX - myX, dy = theirY - myY;
        var (right, forward) = facing switch
        {
            Facing.North => (dx, dy),
            Facing.East => (-dy, dx),
            Facing.South => (-dx, -dy),
            _ => (dy, -dx),
        };
        return (right, forward, theirLevel - myLevel);
    }

    /// <summary>Soul Search cannot see further than three levels away (MAINMOD2.BAS:1287).</summary>
    public const int LocateLevelRange = 3;

    /// <summary>
    /// The deepest the dungeon goes, mc0276. Retrieve Soul will fetch someone on your own level
    /// "or deeper than this", which no character can be - so that half of the test is dead.
    /// </summary>
    public const int MaxDungeonLevel = 15;

    /// <summary>
    /// The original's wrap: <c>Abs(Int((v - 1) / 30) * 30)</c> added or subtracted. VB's Int()
    /// rounds toward minus infinity, so 0 wraps to 30 and 31 wraps to 1.
    /// </summary>
    private static int Wrap(int v, int size)
    {
        if (v >= 1 && v <= size) return v;
        int step = Math.Abs((int)Math.Floor((v - 1) / 30.0) * 30);
        return v < 1 ? v + step : v - step;
    }

    /// <summary>
    /// The binding level Control, Restrain or Domination would set (MAINMOD2.BAS:895):
    /// <c>CInt(spell level + Log((10 + caster spell level) / 5))</c>, rounded as VB rounds.
    /// </summary>
    public static int BindLevelFrom(int spellLevel, int casterSpellLevel) =>
        (int)Math.Round(spellLevel + Math.Log((10.0 + casterSpellLevel) / 5), MidpointRounding.ToEven);

    /// <summary>
    /// Bind a companion harder, or for life. Returns what to say. A companion already at least that
    /// tightly bound gets "... had no effect!" - and the points are spent all the same: the case
    /// only sets l01BA, and the charge after the Select (MAINMOD2.BAS:1355-1357) is not skipped.
    /// </summary>
    /// <remarks>
    /// The lines are MAINMOD2.BAS:878, 896 and 899. Life Domination names the companion as fn128B
    /// does ("Companion #N" or its own name in quotes); the other two say "companion #N" by slot,
    /// lower case, whatever the companion is called. The port had written the creature's name.
    /// </remarks>
    public static string BindCompanion(Companion comp, int slot, Spell s, int casterSpellLevel)
    {
        if (Of(s) == BindForLife)
        {
            comp.BindingLevel = Binding.ForLife;
            string called = comp.Name.Trim() == "" ? $"Companion #{slot + 1}" : $"\"{comp.Name.Trim()}\"";
            return $"{called} is now bound to you for life!";
        }
        int level = BindLevelFrom(s.Level, casterSpellLevel);
        if (comp.BindingLevel >= level) return $"\"{s.Name}\" had no effect!";
        comp.BindingLevel = level;
        return $"\"{s.Name}\" raised the Bind Level of companion #{slot + 1} to {level}.";
    }

    /// <summary>
    /// The highest chest lock an opening spell can pick: effects 30, 31 and 32 open locks of 1, 2
    /// and 3 (MAINMOD2.BAS:1056, <c>l01C6 - (mc0216 - 1)</c>).
    /// </summary>
    public static int OpensLockUpTo(int effect) =>
        effect >= OpenFirst && effect <= OpenLast ? effect - (OpenFirst - 1) : 0;

    /// <summary>Raise Dead, Restore Flesh, Resurrect and the rest of the 20-25 band.</summary>
    public static bool IsRaise(int effect) => effect >= RaiseFirst && effect <= RaiseLast;

    /// <summary>
    /// Raising a dead *companion* (MAINMOD2.BAS:1077-1087). The chance is the monster's own
    /// constitution on top of a flat 85, capped at 98, and a failure destroys it outright. Only
    /// Resurrect brings it back at full health; the rest leave it on one hit.
    /// </summary>
    public static int CompanionRaiseChance(int monsterCon) => Math.Min(98, 85 + monsterCon);

    /// <summary>Effect 22, Resurrect, is the one that restores full hits rather than one.</summary>
    public static bool RaisesToFull(int effect) => effect == RaiseFull;
}
