namespace Mordor.Core;

/// <summary>
/// The Seer of Mordor, <c>FRMSEEK.BAS</c>. The help says what he is for: "The Seer of Mordor has
/// the ability to locate other characters, monsters and items that are in the depths of the
/// dungeon." He does not identify anything and he does not lift curses - those are the General
/// Store's work - and the port had him doing both, with an invented fee, until 2026-09-20.
///
/// Every string below is the original's, including its spelling of "presense".
/// </summary>
public static class Seer
{
    /// <summary>Nothing below this is within his reach (FRMSEEK.BAS:90, 237).</summary>
    public const int DeepestHeCanReach = 12;

    /// <summary>What one search costs: <c>132567.52 * Log(level + 1) * level</c>, plus up to a
    /// quarter of itself again, halved (FRMSEEK.BAS:94-95).</summary>
    public static long Fee(Random rng, int level)
    {
        double fee = 132567.52 * Math.Log(level + 1) * level;
        return (long)((fee + rng.NextDouble() * fee / 4) / 2);
    }

    /// <summary>What a name-matching attempt came to.</summary>
    public enum Found { None, Several, One }

    /// <summary>
    /// How the original matches a typed name (FRMSEEK.BAS:66-86, 224-233): case-insensitively,
    /// against the start of the name, so "gob" finds the Goblin. An exact match wins outright; two
    /// or more partial matches and he refuses to guess.
    ///
    /// A monster whose own name the typing does not begin is tried once more on
    /// <paramref name="alsoNames"/>, the same index: <c>monsterNameFor(Me, 2, l0040)</c> with Ref and
    /// KnownAs the record and IdLevel nought (FRMSEEK.BAS:74-84) - its name as the plural is said,
    /// so "Kobolds" finds the Kobold (<see cref="EncounterGenerator.NameFor(Monster, Character?, int, int)"/>).
    /// Items have no second try.
    /// </summary>
    public static (Found found, int index) Match(string typed, IReadOnlyList<string> names, IReadOnlyList<string>? alsoNames = null)
    {
        if (typed.Length == 0) return (Found.None, -1);
        int count = 0, last = -1;
        for (int i = 0; i < names.Count; i++)
        {
            // UCase(typed) = Left(UCase(name), Len(typed)): a name shorter than the typing never matches
            string name = names[i];
            if (name.Length < typed.Length || !name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            {
                if (alsoNames == null || i >= alsoNames.Count) continue;
                name = alsoNames[i];
                if (name.Length < typed.Length || !name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) continue;
            }
            last = i;
            count++;
            if (name.Equals(typed, StringComparison.OrdinalIgnoreCase)) return (Found.One, i);
        }
        return count switch { 0 => (Found.None, -1), 1 => (Found.One, last), _ => (Found.Several, -1) };
    }

    // ---- what he says ----

    public static string NoSuchMonster(string typed) => $"I am unable to locate a creature called '{typed}'.";
    public static string NoSuchItem(string typed) => $"I am unable to locate an item by the name of '{typed}'.";
    public const string SeveralMonsters = "The name you provided is shared by more than one creature.";
    public const string SeveralItems = "The name you provided is shared by more than one item.";
    public const string MonsterTooDeep = "Unfortunately, this creature dwells beyond the reach of my powers.";
    public const string ItemTooDeep = "Unfortunately, this item is found on a level beyond the reach of my powers.";
    public const string NotEnoughGold = "Not enough gold!";

    public static string OfferMonster(string name, string fee) =>
        $"I can sense the general presense of a creature known as '{name}' somewhere in the depths below.  "
        + $"A small fee of {fee} will be required to do the search.\nDo you accept?";

    public static string OfferItem(string name, string fee) =>
        $"I can sense the general presense of a creature who may carry the item known as '{name}' somewhere in the depths below.  "
        + $"A small fee of {fee} will be required to do the search.\nDo you accept?";

    public static string MonsterLevelOnly(int level) =>
        $"I can detect the presense of this creature starting somewhere on level {level}, but that is all.\n"
        + "Another try may reveal more information.";

    public static string ItemLevelOnly(int level) =>
        $"I can detect this item starting to be found somewhere on level {level}, but that is all.\n"
        + "Another try may reveal more information.";

    public const string NoPlaceForMonster = "I cannot locate a general place for this creature.  Another try may work, though.";
    public const string NoCarrier = "I cannot locate a creature who may have this item.  Another try may work, though.";
    public const string CannotDetect = "I am unable to detect the creature in the depths below. ";
    public const string NoSpecificCarrier = "I was unable to sense the presense of a creature who specifically carries such an item.";

    /// <summary>
    /// The search can fail before it begins: <c>If Int(Rnd * 100) + 1 &lt; level * 5</c> and he
    /// gets no further, which makes the deeper creatures the harder ones to pin down even when he
    /// can reach them at all. He then either names the level or admits he has nothing
    /// (FRMSEEK.BAS:104-112).
    /// </summary>
    public static bool SearchFailsEarly(Random rng, int level) => rng.Next(100) + 1 < level * 5;

    /// <summary>Which of the two failures it is (FRMSEEK.BAS:106).</summary>
    public static bool FailureNamesTheLevel(Random rng, int level) => rng.NextDouble() * 100 >= level * 5;

    /// <summary>
    /// How common this general type is in the area he found, by how many monster classes the
    /// area's spawn mask admits - the fewer it admits, the more of it is this one
    /// (FRMSEEK.BAS:175-186). A lair is its own answer.
    /// </summary>
    // ---------------------------------------------------------------- locating a character

    /// <summary>
    /// What the Seer charges to find a person, rather than a creature or a thing:
    /// <c>132567.52# * Log(level + 1) * level</c> (FRMSEEK.BAS:336). No random element at all,
    /// unlike <see cref="Fee"/> - it is the depth they are at and nothing else.
    /// </summary>
    public static long LocateCharacterFee(int level) =>
        (long)(132567.52 * Math.Log(level + 1) * level);

    /// <summary>They are standing in the city, so there is nothing to find.</summary>
    public const string InTheCity = "Do you mock my abilities?!\nThey are here in the city!";

    public static string OfferCharacter(string name, string fee) =>
        $"I can feel their presense.  I will need {fee} to attempt to locate {name}.\nIs this acceptable?";

    /// <summary>
    /// Whether the Seer finds them, and what he says. The roll is <c>Int(Rnd * 100) + 1</c> against
    /// three times the level they are on, so the deeper they are the likelier he fails - and on a
    /// near miss, within ten, he gets the level but loses them again (FRMSEEK.BAS:340-352).
    /// </summary>
    public static string LocateCharacter(Random rng, string name, int level, int x, int y, bool dead)
    {
        int need = level * 3;
        int roll = rng.Next(100) + 1;
        if (roll < need)
            return roll + 10 >= need
                ? $"I can detect them somewhere on level {level}, but I've lost connection."
                : $"I cannot locate \"{name}\".  Another try may work, though.";

        string said = $"I have detected \"{name}\" at: {x},{y},{level}.";
        if (dead) said += "\nI also feel that they are among the dead.";
        return said;
    }

    public static string Commonality(int classesInArea) => classesInArea switch
    {
        <= 2 => "very common",
        <= 4 => "fairly common",
        <= 7 => "uncommon",
        <= 11 => "rare",
        _ => "almost unheard of",
    };

    /// <summary>The lair case: he found the creature's own den (FRMSEEK.BAS:188).</summary>
    public const string AlwaysSeen = "almost always seen";

    /// <summary>How often this creature turns up among its type, from its Chance (FRMSEEK.BAS:192-206).</summary>
    public static string Frequency(int chance) => chance switch
    {
        <= 2 => "almost never",
        <= 8 => "very rarely",
        <= 15 => "rarely",
        <= 35 => "uncommonly",
        <= 50 => "commonly",
        _ => "very commonly",
    };

    public static string Sighting(string name, int x, int y, int level) =>
        $"I detect the possible presense of the creature known as '{name}' in the area of {x},{y},{level}.";

    public static string TypeIsCommon(string howCommon) =>
        $"I also sense that creatures of this general type are {howCommon} in this area.";

    public static string SeenAmongItsType(string howOften) =>
        $"I can also detect that this creature is {howOften} seen among its type.";

    /// <summary>
    /// Where on the floor he places the creature (FRMSEEK.BAS:120-160). He walks the level a cell
    /// at a time. A cell whose spawn area is this creature's own lair ends the search three times
    /// in four and is the strongest answer he can give. Otherwise he collects every distinct area
    /// whose mask admits the creature's class, and picks one of them at random.
    /// </summary>
    /// <summary>sub321C (FRMSEEK.BAS:413-428): each of x and y moved by Int(Rnd * spread) one way or the other, kept in 1..size.</summary>
    public static (int x, int y) Blur(Random rng, int x, int y, int spread, int width, int height)
    {
        x = rng.Next(2) == 1 ? x - (int)(rng.NextDouble() * spread) : x + (int)(rng.NextDouble() * spread);
        y = rng.Next(2) == 1 ? y - (int)(rng.NextDouble() * spread) : y + (int)(rng.NextDouble() * spread);
        return (Math.Clamp(x, 1, width), Math.Clamp(y, 1, height));
    }

    public static (int x, int y, int classesInArea, bool lair)? Locate(Floor floor, Monster def, Random rng)
    {
        var seen = new HashSet<int>();
        var candidates = new List<(int x, int y, int classes)>();
        for (int x = 1; x <= floor.Width; x++)
        {
            for (int y = 1; y <= floor.Height; y++)
            {
                int id = floor.AreaIdAt(x - 1, y - 1);
                if (!seen.Add(id)) continue;
                if (floor.AreaAt(x - 1, y - 1) is not Floor.Area area) continue;
                if (area.LairId > 0 && area.LairId == def.Id && rng.NextDouble() * 100 < 75)
                    return (x, y, 0, true);
                if ((area.SpawnMask & (1u << def.Type)) == 0) continue;
                int classes = 0;
                for (int c = 0; c < Mdata1.MonsterClasses.Count; c++)
                    if ((area.SpawnMask & (1u << c)) != 0) classes++;
                candidates.Add((x, y, classes));
            }
        }
        if (candidates.Count == 0) return null;
        var pick = candidates[rng.Next(candidates.Count)];
        return (pick.x, pick.y, pick.classes, false);
    }

    /// <summary>
    /// Which creatures might be carrying an item (FRMSEEK.BAS:262-290). A monster qualifies if one
    /// of its eleven treasure entries is this item's *type* and its drop level sits between the
    /// item's own floor and up to two deeper, or if an entry names this exact item - the entries
    /// below -1 are specific items, stored as <c>-(id + 2)</c>. A named carrier is "very strong".
    /// </summary>
    public static List<(Monster def, bool certain)> Carriers(IReadOnlyList<Monster> monsters, Item it, Random rng)
    {
        int band = rng.Next(3);
        var found = new List<(Monster, bool)>();
        foreach (var m in monsters)
        {
            if (m.Id == 0) continue;
            foreach (short entry in m.Items)
            {
                bool hit = false, certain = false;
                if (entry == it.Type && m.ItemDropLevel >= it.Floor && m.ItemDropLevel <= it.Floor + band) hit = true;
                if (entry < -1 && Math.Abs(entry) - 2 == it.Id) { hit = true; certain = true; }
                if (hit) found.Add((m, certain));
            }
        }
        return found;
    }

    /// <summary>
    /// How many of them he names, and the sentence he names them in (FRMSEEK.BAS:292-313). He
    /// takes a random share of what he found, never more than three, and never fewer than one.
    /// </summary>
    public static string Carriers(List<(Monster def, bool certain)> found, Random rng)
    {
        if (found.Count == 0) return NoSpecificCarrier;
        // l0070 is an Integer (FRMSEEK.BAS:44), so the Single it is given rounds, half to even
        int take = (int)Math.Round(found.Count - (double)(float)rng.NextDouble() * found.Count, MidpointRounding.ToEven);
        if (take > 3) take = 3;
        if (take > found.Count) take = found.Count;
        if (take < 1) take = 1;

        // FRMSEEK.BAS:304-317: each draw not named yet adds "Name and ", then the last five characters are cut
        // off whatever the sentence holds. A draw that repeats a carrier adds nothing, so had every
        // draw repeated, the cut would take "n as " and leave "The creature(s) know" (316). The
        // first draw always finds a carrier unnamed, so that never shows; it is kept as written.
        var used = new HashSet<int>();
        string text = "The creature(s) known as ";
        for (int i = 0; i < take; i++)
        {
            int pick = rng.Next(found.Count);
            if (!used.Add(pick)) continue;
            text += found[pick].def.Name + (found[pick].certain ? " (very strong) and " : " and ");
        }
        text = text[..^5];
        return text + " come to me as possible carriers for the item you seek.";
    }
}
