namespace Mordor.Core;

/// <summary>What dealt a <see cref="CombatDamage"/>.</summary>
public enum DamageKind
{
    /// <summary>A blow of a character's weapon (MODULE4.BAS:373-490): a hit, critical, backstab or sever.</summary>
    Physical,
    /// <summary>A character's damage spell at the monsters (MAINMOD2.BAS:1403-1601), their own or an item's.</summary>
    Magical,
    /// <summary>
    /// One of the character's companions (monCompanionAttack, MONSMOD.BAS:806-1359): its breath,
    /// spit, blow, stone, electrocution or spell. Credited to the owner.
    /// </summary>
    Companion,
}

/// <summary>
/// Hits taken off a monster, or a monster killed, by a party member or one of their companions -
/// raised by <see cref="CombatEngine.DamageDealt"/> (for the mods, <c>ModEvents.DamageDealt</c>).
/// The true figures, whether or not any line shows them:
///
/// - <see cref="Amount"/>: the hits the blow, breath or spell actually took off the monster - what
///   it rolled, capped at the hits the monster had left (an overkill past nought is not counted;
///   that is the same figure experience is paid on, <c>CombatEngine.Credit</c>). Raised once per
///   monster struck, with <see cref="Kills"/> 0, and only when it took something. A creature
///   charmed leaves the fight alive, its hits untouched: no damage (only its kill, below).
/// - <see cref="Kills"/>: 1, with <see cref="Amount"/> 0, raised as the monster dies: for a
///   character, where the game puts it on their Kills (<c>KilledBy</c>: a killing blow, a damage
///   spell's kill, a creature charmed - the game counts that as the caster's kill,
///   MAINMOD2.BAS:1506-1508); for a companion, where its kill runs sub0CF3, or its spell kills.
///
/// So a killing blow raises two: its damage (the hits the monster had left), then the kill.
/// A companion's kills are its owner's here; the game itself puts them on nobody's Kills.
/// </summary>
/// <param name="Character">Who dealt it: the character, or the owner of the companion that did.</param>
/// <param name="Amount">Hits taken off the monster (0 for a kill).</param>
/// <param name="Kind">A blow, a spell, or a companion.</param>
/// <param name="Kills">Monsters killed (0 or 1).</param>
/// <param name="Shown">The character is the one in command, whose captions the game puts on screen.</param>
public readonly record struct CombatDamage(Character Character, int Amount, DamageKind Kind, int Kills, bool Shown);

/// <summary>Where a <see cref="CombatExperience"/> came from: the two callers of sub0F11.</summary>
public enum ExperienceSource
{
    /// <summary>
    /// sub0CDF (MONSMOD.BAS:468, 495; <c>CombatEngine.Credit</c>): hits taken off a monster - the
    /// character's own blow, spell or charm, or a companion's damage credited to its owner (at the
    /// quarter <c>Rules.ExpAwarded</c> gives it).
    /// </summary>
    Damage,
    /// <summary>A chest's trap disarmed (MONSMOD.BAS:3508-3509, <c>Rules.ChestExp</c>).</summary>
    Chest,
}

/// <summary>
/// Experience put on a character's total by sub0F11 (MAINMOD2.BAS:279-310,
/// <c>CombatEngine.GiveExperience</c>), raised once for every award - for the mods,
/// <c>ModEvents.ExperienceGained</c>. <see cref="Amount"/> is what the total actually went up by:
/// the award, or less when the cap that pins the character clamped it (nought for an award to a
/// character already pinned); never below nought.
/// </summary>
/// <param name="Character">Whose total it went on (a companion's damage: its owner's).</param>
/// <param name="Amount">What the total went up by, after the clamp at the pin.</param>
/// <param name="Source">Damage dealt, or a chest's trap disarmed.</param>
public readonly record struct CombatExperience(Character Character, long Amount, ExperienceSource Source);
