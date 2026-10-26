using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// The Hall of Records: twelve town-wide bests, each remembering a value, who set it and when
/// (FRMHALL.BAS for the display, MODULE11.BAS <c>sub1043</c> - "mdrhalCheckRecord" by its own
/// error handler - for the checking, and <c>fn1BF0</c> for the three skill measures and the
/// monster-power one).
/// </summary>
public sealed class HallRecords
{
    /// <summary>The twelve, in the order the file stores them.</summary>
    public enum Kind
    {
        Strength = 0, Intelligence = 1, Wisdom = 2, Constitution = 3, Charisma = 4, Dexterity = 5,
        DeadliestCreature = 6, MostExperience = 7, Wealthiest = 8,
        Fighting = 9, Magic = 10, Thieving = 11,
    }

    /// <summary>What the guild log calls each record (MODULE11.BAS:473-500).</summary>
    public static string LogName(Kind k) => k switch
    {
        Kind.Strength => "Strongest Character",
        Kind.Intelligence => "most Intelligent Character",
        Kind.Wisdom => "Wisest Character",
        Kind.Constitution => "Healthiest Character",
        Kind.Charisma => "most Attractive Character",
        Kind.Dexterity => "Quickest Character",
        Kind.DeadliestCreature => "most powerful creature defeated",
        Kind.MostExperience => "most experience",
        Kind.Wealthiest => "wealthiest character",
        Kind.Fighting => "highest fighting ability",
        Kind.Magic => "highest spell knowledge & power level",
        _ => "highest thieving ability",
    };

    /// <summary>The heading the Hall shows over each record (FRMHALL.BAS sub32F6).</summary>
    public static string Heading(Kind k) => k switch
    {
        Kind.Strength => "Strongest",
        Kind.Intelligence => "Smartest",
        Kind.Wisdom => "Wisest",
        Kind.Constitution => "Healthiest",
        Kind.Charisma => "Most Attractive",
        Kind.Dexterity => "Quickest",
        Kind.DeadliestCreature => "Deadliest Creature Defeated",
        Kind.MostExperience => "Most Experienced Explorer",
        Kind.Wealthiest => "Wealthiest Explorer",
        Kind.Fighting => "Master of Fighting",
        Kind.Magic => "Master of Magic",
        _ => "Master of Thieving",
    };

    public sealed class Record : System.Text.Json.Serialization.IJsonOnDeserialized
    {
        /// <summary>
        /// <c>M73F5</c>, a Currency (MAIN.TXT:846) for every record. Only the Wealthiest can carry a
        /// fraction - a bank balance does, once an amount with one is typed at the Bank - and
        /// <c>pv0104 > M73F5</c> compares it exactly, so 1,000.5 takes a record of 1,000.
        /// On disk as <see cref="Character.GoldOnHand"/> is: the whole number where it always
        /// was, the exact figure beside it only when there is a fraction.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public VbCurrency Value { get; set; }
        [System.Text.Json.Serialization.JsonConverter(typeof(SavedWholeJson))]
        [System.Text.Json.Serialization.JsonPropertyName("Value")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public long ValueWhole { get => Value.Whole; set => Value = VbCurrency.Saturate(value); }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Text.Json.Serialization.JsonConverter(typeof(SavedExactJson))]
        public decimal? ValueExact { get => Value.ExactForSave; set => _exact = value; }
        private decimal? _exact;
        void System.Text.Json.Serialization.IJsonOnDeserialized.OnDeserialized() { if (_exact != null) Value = VbCurrency.FromSave(Value.Whole, _exact); _exact = null; }
        public string Holder { get; set; } = "";
        /// <summary>The day it was set, VB's day serial. Zero means nobody has taken it yet.</summary>
        public long Day { get; set; }
    }

    public Dictionary<int, Record> Records { get; set; } = new();

    public Record For(Kind k) => Records.TryGetValue((int)k, out var r) ? r : new Record();

    /// <summary>
    /// The line under each heading. With no date the original credits the "Original RecordHolder",
    /// which is how a record nobody has beaten yet reads.
    /// </summary>
    public string Describe(Kind k, Func<VbCurrency, string> valueText)
    {
        var r = For(k);
        string who = r.Day != 0 ? $"Set on {GuildLog.FromDaySerial(r.Day):d} by " : "Set by Original RecordHolder ";
        // RTrim(M73FE), sub32F6 (FRMHALL.BAS:130)
        return who + "\"" + r.Holder.TrimEnd() + "\"\n" + valueText(r.Value);
    }

    /// <summary>
    /// <c>sub1043</c>. Offers a value for a record and returns the line to announce, or null if
    /// nothing was announced - which covers three cases: the value did not beat the record, the
    /// same character already held it, or it is the creature record, which the original stores
    /// silently because it sets the same flag the "you already hold it" case does.
    /// </summary>
    public string? Check(Kind k, Character c, VbCurrency value, Func<int, int>? monsterPower = null, bool alone = false)
    {
        var r = For(k);

        if (k == Kind.DeadliestCreature)
        {
            // the value is a monster id; they are compared by power, not by id. The original
            // zeroes the stored value first so the plain comparison below always passes.
            int power = monsterPower?.Invoke((int)value.RoundToWhole()) ?? 0;
            if (power <= (monsterPower?.Invoke((int)r.Value.RoundToWhole()) ?? 0)) return null;
            r = new Record { Value = -1, Holder = r.Holder, Day = r.Day };
        }

        if (value <= r.Value) return null;

        bool alreadyTheirs = r.Holder.TrimEnd() == c.Name.TrimEnd();
        Records[(int)k] = new Record { Value = value, Holder = c.Name, Day = GuildLog.Today() };

        // improving your own record is silent, and so is the creature record - both because the
        // original sets one flag for the two cases and exits on it before announcing
        if (alreadyTheirs || k == Kind.DeadliestCreature) return null;
        // MODULE11.BAS:505-509: "You have just set..." for a lone adventurer, the name otherwise
        return alone ? $"You have just set the record for {LogName(k)}!"
                     : $"\"{c.Name}\" just set the record for {LogName(k)}!";
    }

    /// <summary>
    /// The same check, also writing the guild-log line the original writes alongside the
    /// announcement. Returns what to show on screen, or null.
    /// </summary>
    public string? CheckAndLog(Kind k, Character c, VbCurrency value, string savesDir, Func<int, int>? monsterPower = null, bool alone = false)
    {
        string? said = Check(k, c, value, monsterPower, alone);
        if (said == null) return null;
        GuildLog.Append(savesDir, GuildData.IndexOf(c.Guild.Guild),
                        GuildLog.Wrote.SetRecord(c.Name, LogName(k), CharStatFile.FormatNumber(For(k).Value)));
        return said;
    }

    // ---- the measures fn1BF0 computes ----

    /// <summary>
    /// How formidable a monster was, <c>fn1BF0</c>'s creature case (MODULE11.BAS:546):
    /// <c>Att*2 + STR*10 + Hits/10 + DEX*2 + guildLevel</c>, plus 75 for each of its fourteen
    /// special attacks and 100 for each of its eighteen spells. Spells count for more, which is
    /// the game's own view of what makes a monster dangerous.
    /// </summary>
    public static int MonsterPower(Monster m)
    {
        // MODULE11.BAS:546 assigns Att*2 + STR*10 + Hits/10 (float division) to an Integer, so
        // that sum is rounded before the rest is added in integer arithmetic
        int first = (int)Math.Round(m.Att * 2 + m.Stats.ElementAtOrDefault(0) * 10 + m.Hits / 10.0, MidpointRounding.ToEven);
        return first
            + m.Stats.ElementAtOrDefault(5) * 2
            + m.GuildLevel
            + 75 * CountBits(m.SpecialAttackFlags, 14)
            + 100 * CountBits(m.SpellFlags, 18);
    }

    private static int CountBits(long mask, int width)
    {
        int n = 0;
        for (int i = 0; i < width; i++) if ((mask & (1L << i)) != 0) n++;
        return n;
    }

    /// <summary>
    /// Combined fighting skill: the fighting rating at full weight plus a fifth each of
    /// backstabbing, critical hit and multiple swings, as a percentage of the 160 that would be
    /// (MODULE11.BAS:561).
    /// </summary>
    public static int FightingSkill(Character c, Func<short, Item?>? gear = null)
    {
        // l012E is an Integer, so every += rounds (banker's), and so does the Integer return
        int total = Rules.AbilityRatingExact(c, 4, gear);
        foreach (int a in new[] { 2, 1, 3 })
            total = (int)Math.Round(total + Rules.AbilityRatingExact(c, a, gear) * 0.2, MidpointRounding.ToEven);
        return (int)Math.Round(total / (100 + 100 * 0.2 + 100 * 0.2 + 100 * 0.2) * 100, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Total thieving skill: the thieving rating times dexterity, over 3500, as a percentage
    /// (MODULE11.BAS:590).
    /// </summary>
    public static int ThievingSkill(Character c, Func<short, Item?> item)
    {
        int v = Rules.AbilityRatingExact(c, 0, item) * c.EffectiveStat((int)Stat.Dex, item);
        if (v < 1) v = 1;
        return (int)Math.Round(v / (100.0 * 35) * 100, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Spell knowledge and power: for every spell in the game, the cheapest guild you belong to
    /// that has reached the level to cast it contributes its spell level and half your guild
    /// level; the total is a percentage of what knowing every spell at a guild level of 255 would
    /// give (MODULE11.BAS:573).
    /// </summary>
    public static int MagicSkill(Character c, IReadOnlyList<Spell> spells)
    {
        var all = spells.ToList();   // every record, id 0 included - MODULE11.BAS:568 loops 0 To gv1BF8-1
        // both accumulators are Integers in the original, rounding at every step
        int have = 0, possible = 0;
        foreach (var s in all) possible += s.Level * 10;
        possible += 255 * all.Count;
        if (possible <= 0) return 0;

        foreach (var s in all)
            foreach (var g0 in GuildData.All)                // fn122A-style table order for the tie
            {
                var m = c.Guilds.FirstOrDefault(x => x.Guild == g0.Name);
                if (m == null) continue;
                if (m.Level <= 0) continue;
                var g = GuildData.ByName(m.Guild);
                double factor = s.Category >= 0 && s.Category < g.SpellRate.Length ? g.SpellRate[s.Category] : 0;
                if (factor <= 0) continue;
                if (s.Level * factor > m.Level / 2.0 + 0.5) continue;
                have += s.Level * 10;
                have = (int)Math.Round(have + (m.Level / 2.0 + 0.5), MidpointRounding.ToEven);
                break;                      // the first guild that can is the one that counts
            }
        return (int)Math.Round(have / (double)possible * 100, MidpointRounding.ToEven);
    }

    /// <summary>Total experience across every guild the character belongs to (MODULE11.BAS:449).</summary>
    public static long TotalExperience(Character c) => c.Guilds.Sum(g => g.Experience);

    // ---- storage ----

    private static string PathFor(string savesDir) => Path.Combine(savesDir, "records.json");

    /// <summary>
    /// The twelve records a new game ships with, <c>sub0595</c> (MAINMOD.BAS:662-711, under the
    /// loading caption "Creating Hall File..."). They are not blank: the Hall opens with named
    /// holders and real figures, and their date is 0, which is what makes it say
    /// "Original RecordHolder" rather than a day.
    ///
    /// This matters more than it looks. With the board empty every record is takeable by a
    /// level-one character the moment they are made; with it seeded, the stat floors are 14 to 18
    /// and the gold and experience run to millions.
    /// </summary>
    public static HallRecords Fresh()
    {
        var h = new HallRecords();
        void Set(Kind k, long value, string holder) =>
            h.Records[(int)k] = new Record { Value = value, Holder = holder, Day = 0 };

        Set(Kind.Strength, 14, "Crashland");
        Set(Kind.Intelligence, 15, "Mager");
        Set(Kind.Wisdom, 18, "Theshal");
        Set(Kind.Constitution, 14, "Orgal");
        Set(Kind.Charisma, 15, "Alaya");
        Set(Kind.Dexterity, 17, "Nimblefingers");
        Set(Kind.DeadliestCreature, 1, "Crashland");
        Set(Kind.MostExperience, 1322451, "Crashland");
        Set(Kind.Wealthiest, 5466453, "Nimblefingers");
        Set(Kind.Fighting, 17, "Morgul");
        Set(Kind.Magic, 6, "Ge'nal");
        Set(Kind.Thieving, 15, "Nimblefingers");
        return h;
    }


    public static HallRecords Load(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p)) return JsonSerializer.Deserialize<HallRecords>(File.ReadAllText(p)) ?? new HallRecords();
        }
        catch { }
        // no file yet means a new game, and a new game's Hall is not empty
        return Fresh();
    }

    public void Save(string savesDir)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            File.WriteAllText(PathFor(savesDir), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
