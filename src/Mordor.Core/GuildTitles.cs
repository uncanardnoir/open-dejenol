namespace Mordor.Core;

/// <summary>
/// The rank a character holds in a guild, shown by the original as "Vagrant (1)" on the Stats tab.
/// This is <c>fn12C4</c> in MAINMOD2.BAS, a Select Case per guild on the level in the *current*
/// guild. Titles do not advance at a fixed rate - the bands vary by guild and get wider as they go,
/// so a Nomad changes title at 10, 20, 30, 40, 50, 60, 80, 100, 130, 150, 190 and 220.
///
/// The bands are reproduced exactly, including one fault. The Ninja list has three consecutive
/// <c>Case Is &lt; 120</c> branches, and VB takes the first that matches, so "Ninja" and
/// "Master Ninja" are unreachable: a Ninja goes Instructor, Assassin, then Shogun and never holds
/// the titles named after the guild. Matching on the first band that fits keeps that behaviour.
/// </summary>
public static class GuildTitles
{
    /// <summary>Title and the level below which it applies; the last entry catches everything above.</summary>
    private static readonly Dictionary<string, (int Below, string Title)[]> Bands = new()
    {
        ["Nomad"] = new[]
        {
            (10, "Vagrant"), (20, "Drifter"), (30, "Vagabond"), (40, "Wanderer"), (50, "Traveller"),
            (60, "Wayfarer"), (80, "Rover"), (100, "Pioneer"), (130, "Pilgrim"), (150, "Journeyor"),
            (190, "Voyager"), (220, "Nomad"), (int.MaxValue, "Master Nomad"),
        },
        ["Warrior"] = new[]
        {
            (10, "Bravo"), (20, "Soldier"), (30, "Fighter"), (50, "Mercenary"), (70, "Swordsman"),
            (90, "Duelist"), (120, "Gladiator"), (140, "Combatant"), (160, "Hero"), (180, "Swashbuckler"),
            (200, "Master Hero"), (230, "Master Swordsman"), (int.MaxValue, "Warlord"),
        },
        ["Paladin"] = new[]
        {
            (10, "Guard"), (20, "Sentry"), (50, "Defender"), (80, "Protector"), (100, "Sentinel"),
            (120, "Guardian"), (150, "Knight"), (180, "Champion"), (210, "Paladin"),
            (int.MaxValue, "Master of the Guard"),
        },
        // the three 120s are in the original; "Ninja" and "Master Ninja" can never be reached
        ["Ninja"] = new[]
        {
            (10, "Apprentice"), (20, "Student"), (40, "Mentor"), (70, "Teacher"), (90, "Instructor"),
            (120, "Assassin"), (120, "Ninja"), (120, "Master Ninja"), (150, "Shogun"),
            (220, "Weapons Master"), (int.MaxValue, "Death Master"),
        },
        ["Villain"] = new[]
        {
            (10, "Rascal"), (20, "Knave"), (30, "Trickster"), (40, "Scoundrel"), (50, "Miscreant"),
            (70, "Criminal"), (90, "Ruffian"), (120, "Convict"), (150, "Murderer"), (180, "Villain"),
            (210, "Evil Being"), (int.MaxValue, "Master Villain"),
        },
        ["Seeker"] = new[]
        {
            (10, "Apprentice"), (20, "Journeyman"), (30, "Guide"), (40, "Searcher"), (60, "Tracer"),
            (80, "Hunter"), (100, "Navigator"), (120, "Explorer"), (140, "Infiltrator"), (160, "Adventurer"),
            (180, "Delver"), (200, "Questor"), (230, "Seeker"), (int.MaxValue, "Master Seeker"),
        },
        ["Thief"] = new[]
        {
            (10, "Footpad"), (20, "Sneak"), (30, "Pickpocket"), (40, "Trickster"), (50, "Cutpurse"),
            (60, "Robber"), (80, "Swindler"), (100, "Shyster"), (120, "Convict"), (140, "Criminal"),
            (160, "Thief"), (180, "Racketeer"), (200, "Kingpin"), (240, "Master Thief"),
            (int.MaxValue, "Master of Shadows"),
        },
        ["Scavenger"] = new[]
        {
            (10, "Rogue"), (20, "Scrounger"), (30, "Forager"), (40, "Acquirer"), (70, "Plunderer"),
            (100, "Pillager"), (140, "Ransacker"), (180, "Marauder"), (220, "Mugger"), (285, "Scavenger"),
            (int.MaxValue, "Master Scavenger"),
        },
        ["Mage"] = new[]
        {
            (10, "Apprentice"), (30, "Enchanter"), (50, "Charmer"), (80, "Conjuror"), (120, "Illusionist"),
            (150, "Magician"), (180, "Mage"), (230, "Spellbinder"), (260, "Arch-Mage"),
            (int.MaxValue, "Master of Magic"),
        },
        ["Sorcerer"] = new[]
        {
            (10, "Alchemist"), (30, "Warlock"), (50, "Sorcerer"), (70, "Necromancer"), (90, "Element Mage"),
            (120, "Fire Master"), (150, "Master of Electricity"), (170, "Earth Shaker"), (240, "Master Sorcerer"),
            (int.MaxValue, "Master of Elements"),
        },
        ["Wizard"] = new[]
        {
            (10, "Prophet"), (40, "Diviner"), (70, "SoothSayer"), (100, "Theurgist"), (130, "Thaumaturgist"),
            (160, "Wizard"), (190, "Adept"), (230, "Master Wizard"), (int.MaxValue, "Master Adept"),
        },
        ["Healer"] = new[]
        {
            (10, "Apprentice"), (30, "Medic"), (60, "Benefactor"), (90, "Altruist"), (120, "Healer"),
            (150, "Shaman"), (200, "Magus"), (250, "Raiser of the Dead"), (int.MaxValue, "Master Healer"),
        },
    };

    /// <summary>The title for a level in a guild: the first band it falls below, as VB's Select Case does.</summary>
    public static string Of(string guild, int level)
    {
        if (!Bands.TryGetValue(guild, out var bands) || bands.Length == 0) return "Adventurer";
        foreach (var (below, title) in bands)
            if (level < below) return title;
        return bands[^1].Title;
    }
}
