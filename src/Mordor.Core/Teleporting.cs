namespace Mordor.Core;

/// <summary>
/// Where a teleport puts people, decided before anybody is moved. Two different routines in the
/// original, and they do not share their rules:
///
/// - the spells (Teleport, and Displacement's two-number form) go through mdrcharTeleport
///   (MAINMOD2.BAS:1125, 1143), which falls above the city and dies in rock (5839-5896);
/// - a teleporter square is handled inside the square routine sub0835 (MAZEMOD.BAS:1788-1830),
///   which writes X, Y and DungeonLevel and has no rock test and no level-0 test at all.
/// </summary>
public static class Teleporting
{
    /// <summary>What a Teleport spell's destination does to the party.</summary>
    public enum Outcome { Arrives, FallsFromTheSky, IntoRock }

    /// <summary>
    /// mdrcharTeleport's two tests on the square it has already written into X, Y and
    /// DungeonLevel: level 0 or less is the fall (MAINMOD2.BAS:5839), and then rock on that square,
    /// or a level past mc0276, is the rock death (5860). The level is the one the spell asked for;
    /// the bodies lie at <paramref name="level"/>, <paramref name="x"/>, <paramref name="y"/> - past
    /// the deepest level too, where the port has no map to put them on.
    /// </summary>
    public static Outcome ForSpell(IReadOnlyList<Floor> dungeon, int level, int x, int y)
    {
        if (level <= 0) return Outcome.FallsFromTheSky;
        // past mc0276 counts as rock whatever is there (5860); the shipped dungeon has exactly
        // mc0276 levels (selfcheck), so every level up to it has a map to test
        if (level > SpellEffect.MaxDungeonLevel || level > dungeon.Count) return Outcome.IntoRock;
        var fl = dungeon[level - 1];
        if (x < 0 || y < 0 || x >= fl.Width || y >= fl.Height) return Outcome.IntoRock;
        return fl.Has(x, y, Floor.ROCK) ? Outcome.IntoRock : Outcome.Arrives;
    }

    /// <summary>
    /// The teleporter square, for every member in party order (MAZEMOD.BAS:1797-1826). Each one is
    /// given a square of their own:
    ///
    /// - a record with a zero X2 or Y2 draws <c>Int(Rnd * width) + 1</c>, <c>Int(Rnd * height) + 1</c>
    ///   until the square is neither rock nor another teleporter (1798-1808) - drawn afresh for each
    ///   member, so a party is scattered and sub0908 then drops everyone not on the leader's square
    ///   (MAZEMOD.BAS:1878; MULTIMOD.BAS:52-71);
    /// - otherwise X2 and Y2 as the record has them (1810-1811), the same for everybody.
    ///
    /// The level becomes Z2 when Z2 is above nought and differs (1814-1816). The draw reads the
    /// level fn0A0B has loaded, and after each member that is the leader's (1817): the leader's own
    /// draw is made on the level they stood on, everyone after on the level the leader went to.
    /// fn0A0B loads nothing past level 15 (MAZEMOD.BAS:415-417), and nothing here is either.
    /// Coordinates returned are 0-based.
    /// </summary>
    public static List<(int Level, int X, int Y)> ForSquare(IReadOnlyList<Floor> dungeon, int fromLevel,
                                                            Floor.Teleporter t, int members, Random rng)
    {
        var dest = new List<(int Level, int X, int Y)>();
        int loaded = fromLevel;
        for (int i = 0; i < members; i++)
        {
            int x, y;
            if (t.X2 == 0 || t.Y2 == 0)
            {
                var fl = dungeon[loaded - 1];
                do { x = rng.Next(fl.Width); y = rng.Next(fl.Height); }
                while (fl.Has(x, y, Floor.ROCK) || fl.Has(x, y, Floor.TELEPORTER));
            }
            else { x = t.X2 - 1; y = t.Y2 - 1; }
            int level = t.Z2 > 0 && t.Z2 != fromLevel ? t.Z2 : fromLevel;
            dest.Add((level, x, y));
            if (dest[0].Level >= 1 && dest[0].Level <= dungeon.Count) loaded = dest[0].Level;
        }
        return dest;
    }
}
