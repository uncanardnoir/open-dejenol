namespace Mordor.Core;

/// <summary>
/// Getting a body out of the dungeon. The original is strict about this: the morgue cannot touch
/// you while you are still below ground (<c>sub0930</c>, MAINMOD2.BAS:3040 only offers "Visit the
/// Morgue" when <c>DungeonLevel = 0</c>, and "Wait for Rescue" otherwise). So a corpse on level
/// six has three ways home - lie there until someone stumbles over it, be raised where it lies by
/// another character's spell, or have the morgue send rescuers after it for a great deal of gold.
/// </summary>
public static class Rescue
{
    /// <summary>
    /// Lying there until you are found, <c>cmdWait_Click</c> (FRMDUNM.BAS:140-190). Each day drawn
    /// is a roll of <c>Rnd * ceiling</c> against <c>16 - level</c>, floored at a tenth, and the
    /// ceiling is what makes depth punishing: 10,000 for the first stretch of days, dropping to
    /// 1,000 once you have lain there longer than <c>Log(level)² * 100 + 21</c> days. Dying just
    /// inside the first level is the one merciful case - area 18 or lower on level 1 has a ceiling
    /// of 100, so someone trips over you almost at once.
    ///
    /// A square carrying <see cref="Floor.RESCUE"/> doubles the chance every day.
    ///
    /// The loop has no exit but success: you are always found in the end, and the only question is
    /// how many days of your life it costs. Those days are then added to your age.
    /// </summary>
    public static int DaysUntilFound(Random rng, int dungeonLevel, int areaId, bool easyToFind)
    {
        if (dungeonLevel < 1) return 0;
        int days = 0;
        double threshold = Math.Log(dungeonLevel) * Math.Log(dungeonLevel) * 100 + 21;
        while (true)
        {
            int ceiling = days < threshold ? 10000 : 1000;
            if (dungeonLevel == 1 && areaId <= 18) ceiling = 100;
            days++;

            double chance = 16 - dungeonLevel;
            if (chance < 0.1) chance = 0.1;
            if (easyToFind) chance += 16 - dungeonLevel;

            if (rng.NextDouble() * ceiling <= chance) return days;
            if (days > 2_000_000) return days;   // the original would spin here; nothing else can
        }
    }

    public static string Found(int days) => $"You have been found after {days} day(s)!";

    /// <summary>
    /// Whether the square the body lies on is one rescuers look at first:
    /// <c>testBit(23, fn0A34(X, Y))</c> (FRMDUNM.BAS:158-161), the <see cref="Floor.RESCUE"/> bit.
    /// </summary>
    public static bool EasyToFind(Floor floor, int x, int y) => floor.Has(x, y, Floor.RESCUE);

    /// <summary>
    /// What the wait does to the body once it is found (FRMDUNM.BAS:187-189): carried to town -
    /// <c>DungeonLevel = 0</c>, <c>M59BB = -1</c> - and the days added to the age by
    /// <c>mdrcharAddAge False, Me, days, True</c>, which says nothing (p088C = False). Shared by the
    /// dungeon's dead screen and the city-hosted one, so the two cannot drift apart.
    /// </summary>
    public static void CarryHome(Character body, int days)
    {
        body.Age += days;
        body.DungeonLevel = 0;
        body.Area = -1;
        // X and Y are left as they were: FRMDUNM.BAS:187-188 sets only DungeonLevel and M59BB
    }

    /// <summary>
    /// The rest half of that same <c>mdrcharAddAge</c> call (p0892 = True, days given, so l089C is
    /// never set): for character 0 the store and the pens are turned over with 0 days
    /// (MAINMOD2.BAS:6384-6389), which mdrReplenishStore lifts to a 1% chance (MAINMOD.BAS:1510-1512).
    /// </summary>
    public static void TurnOverTown(string savesDir, IReadOnlyList<Item> items, IReadOnlyList<Monster> monsters,
                                    Random rng, int deepestReached)
    {
        var shelf = StoreStock.Load(savesDir, items);
        shelf.Replenish(items, 0, rng, deepestReached);
        shelf.Save(savesDir);
        var pens = Confinement.Load(savesDir, monsters);
        pens.Replenish(monsters, 0, rng, deepestReached);
        pens.Save(savesDir);
    }

    // ---- the morgue's rescuers, FRMMORG.BAS:104-176 ----

    /// <summary>What it costs to send rescuers to a level: <c>372567.52 * Log(level + 1) * level</c>.</summary>
    public static long SearchCost(int level) => (long)(372567.52 * Math.Log(level + 1) * level);

    public const string BadCoordinates = "We require proper coordinates (X,Y,Level) before we can dispatch rescuers!";
    public const string InvalidCoordinates = "Invalid Coordinates!";
    public const string NotEnoughGold = "Not enough gold!";
    public const string NeverReturned = "Rescuers never returned...  Don't you feel guilty now?";

    public static string Offer(long cost, string name) =>
        $"Cost will be {cost:N0} to search for {name}.\nDo you accept?";

    /// <summary>
    /// Whether the party you paid for comes back at all: <c>Rnd * 100 > level * 4</c>. Below level
    /// twenty-five they always do; past it they sometimes simply do not, and your money with them.
    /// </summary>
    public static bool RescuersReturn(Random rng, int level) => rng.NextDouble() * 100 > level * 4;

    /// <summary>
    /// VB's <c>Val</c> into an Integer: spaces anywhere are skipped, then a sign, digits and one point
    /// are read up to the first thing that is none of those, and the Integer rounds half to even.
    /// </summary>
    public static int Val(string s)
    {
        var t = new System.Text.StringBuilder();
        bool point = false;
        foreach (char ch in s)
        {
            if (ch == ' ' || ch == '\t') continue;
            if ((ch == '-' || ch == '+') && t.Length == 0) { t.Append(ch); continue; }
            if (char.IsDigit(ch)) { t.Append(ch); continue; }
            if (ch == '.' && !point) { point = true; t.Append(ch); continue; }
            break;
        }
        return double.TryParse(t.ToString(), System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? (int)Math.Round(v, MidpointRounding.ToEven) : 0;
    }

    /// <summary>
    /// The morgue's comma loop (FRMMORG.BAS:118-135), one-based as it is written: a piece ends at each
    /// comma or at the last character, and the last character is kept with the piece before it - so
    /// "15,5,3," is three pieces, "3," being read as 3.
    /// </summary>
    public static List<int> SplitCoords(string s)
    {
        var pieces = new List<int>();
        int at = 0, start = 1;
        do
        {
            at++;
            if (s.Substring(at - 1, 1) == "," || at == s.Length)
            {
                if (at == s.Length) at++;
                int len = Math.Min(at - start, s.Length - (start - 1));
                pieces.Add(Val(len > 0 ? s.Substring(start - 1, len) : ""));
                start = at + 1;
            }
        } while (at < s.Length);
        return pieces;
    }

    /// <summary>
    /// How close the coordinates have to be: within eight squares each way, and the level exactly
    /// (FRMMORG.BAS:149). You are telling them where to look, so a wrong guess wastes the fee.
    /// </summary>
    public static bool CloseEnough(int guessX, int guessY, int guessLevel, int x, int y, int level) =>
        Math.Abs(guessX - x) < 8 && Math.Abs(guessY - y) < 8 && guessLevel == level;

    /// <summary>What the rescuers found, and whether the body comes home.</summary>
    public readonly record struct Outcome(string Message, bool BroughtBack);

    /// <summary>
    /// What they report (FRMMORG.BAS:150-171). A living character is only carried home if they are
    /// down to a fifth of their hits; one who is up and about is left where they are. A character
    /// pulled into the rock cannot be recovered by anybody - the rescuers can see them, and that
    /// is all.
    /// </summary>
    public static Outcome Report(Character who, bool foundAtAll)
    {
        if (!foundAtAll)
            return new Outcome($"{who.Name.TrimEnd()} was not found at or around the specified location.", false);

        if (!who.IsDead)
            return who.CurHits <= who.MaxHits * 0.2
                ? new Outcome($"{who.Name.TrimEnd()} was found alive, and in pretty bad shape, so we healed them and brought them back!", true)
                : new Outcome($"{who.Name.TrimEnd()} was found, but was alive and well!", false);

        if (who.CauseOfDeath == Rules.DeathRock)
            return new Outcome("The only thing they saw were feet sticking out of rock.", false);

        return new Outcome($"{who.Name.TrimEnd()} was found at {who.X + 1},{who.Y + 1},{who.DungeonLevel} and brought to the morgue!", true);   // one-based, as typed
    }

    /// <summary>What the dungeon tells you while you lie there (MAINMOD2.BAS:3047-3050).</summary>
    public const string DeadInTheDungeon =
        "You are Dead.  You may be resurrected by the morgue or a character, wait for rescue, or run another character.";

    public const string DeadInRock =
        "You are Dead.  You must be summoned out of the Rock before you can be resurrected.";
}
