using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// The Library, <c>fn1BBE</c> (MODULE11.BAS:78-406). The port used to show two tables of numbers
/// drawn from what the current character had personally met. The original writes prose, it is
/// shared between every character in the game, and - the part the tables threw away - it tells you
/// steadily more as the world learns more, in four distinct grades.
///
/// At grade 1 all you get is how big a creature is. At 2, its size and class. At 3, its full
/// statistics and an admission that nobody knows what it can do. Only at 4 do you get its special
/// attacks, its spells and its resistances. Items have two grades rather than four: everything
/// except the damage modifier, the curse, the spell it casts, its effects and its guild levels -
/// and then all of it.
/// </summary>
public static class Librarian
{
    public const int VeryLittle = 1, AGoodDealMore = 2, AlmostEverything = 3, Everything = 4;

    // ---- the word lists the prose is built from, all MODULE1.BAS:473-533 ----

    /// <summary><c>gv05BC$</c>: how big a creature is.</summary>
    public static readonly string[] Sizes = { "Very Small", "Small", "Normal", "Big", "Very Big", "Huge" };

    /// <summary><c>gv074C$</c>: the three alignments, as the prose names them.</summary>
    public static readonly string[] Alignments = { "Good", "Neutral", "Evil" };

    /// <summary><c>gv08DC$</c>: a creature's standing properties.</summary>
    public static readonly string[] MonsterProperties =
        { "See Invisible", "Invisible", "Resistant to All Magic", "Charm Resistant",
          "Weapon Resistance", "Complete Weapon Resistance" };

    /// <summary><c>gv090E$</c>: the fourteen things a creature can do to you.</summary>
    public static readonly string[] MonsterAttacks =
        { "Poison", "Disease", "Paralyze", "Breathe Fire", "Breathe Cold", "Acid", "Electrocute",
          "Drain", "Stone", "Age", "Critical Hit", "Backstab", "Destroy Item", "Steal" };

    /// <summary><c>gv07B0$</c> (MODULE1.BAS:454-472): the eighteen schools a creature can cast from.</summary>
    public static readonly string[] SpellSchools =
        { "Fire", "Cold", "Electrical", "Mind", "Damage", "Element", "Kill", "Charm", "Bind",
          "Heal", "Movement", "Banish", "Dispell", "Resistant", "Visual", "Magical", "Location",
          "Protection" };

    /// <summary><c>gv0620$</c>: the eleven resistances.</summary>
    public static readonly string[] Resistances =
        { "Fire", "Cold", "Electrical", "Mind", "Disease", "Poison", "Magic", "Stone",
          "Paralysis", "Drain", "Acid" };

    /// <summary><c>gv0684$</c>: what an item can give you when worn.</summary>
    public static readonly string[] ItemAbilities =
        { "Levitate", "Invisible", "Protect", "See Invisible", "Critical Hit", "Backstab" };

    /// <summary><c>gv0846$</c>: the original spells the hand count out in words.</summary>
    public static readonly string[] HandWords = { "Zero", "One", "Two", "Three", "Four" };

    private static string Size(int i) => i >= 0 && i < Sizes.Length ? Sizes[i] : "oddly";
    private static string Align(int i) => i >= 0 && i < Alignments.Length ? Alignments[i] : "Neutral";
    private static bool Bit(long mask, int b) => (mask & (1L << b)) != 0;

    /// <summary>Joins a list the way the original does: comma-separated, with the last comma cut off.</summary>
    private static string Listed(IEnumerable<string> parts) => string.Join(", ", parts);

    // ---------------------------------------------------------------- creatures

    /// <summary>
    /// The Library's page on a creature - MODULE11.BAS:97-231. The four grades are the whole point:
    /// walk in knowing nothing and it will tell you how big a goblin is, and nothing else.
    /// </summary>
    public static string DescribeMonster(Monster m, LibraryRecord e, int today, string plural)
    {
        var s = new System.Text.StringBuilder();
        s.Append("MONSTER: ").Append(m.Name).Append("\r\n\r\n");

        switch (e.Known)
        {
            case VeryLittle:
                s.Append($"All that is known about {plural} is that they are {Size(m.Size)} in Size.");
                break;
            case AGoodDealMore:
                s.Append($"All that is known about {plural} is that they are {Size(m.Size)} sized "
                       + $"{Mdata1.MonsterClassName(m.Type)}s.");
                break;
            default:
                s.Append($"{plural} are {Size(m.Size)} sized {Mdata1.MonsterClassName(m.Type)}s who are "
                       + $"generally {Align(m.AlignmentValue)} in nature.\r\n");
                s.Append($"With an average A/D of {m.Att}/{m.Def}, and average hits of {m.Hits} and a ");
                for (int i = 0; i < 6 && i < m.Stats.Length; i++)
                    if (m.Stats[i] > 0) s.Append($"{m.Stats[i]} {RaceData.StatNamesLong[i]}, ");
                break;
        }

        if (e.Known == AlmostEverything)
            s.Append("it is not yet known if or what special abilities this creature has.");
        else if (e.Known == Everything)
        {
            s.Append("it is also known that these creatures");
            var told = new List<string>();
            int attacks = 0;
            for (int i = 0; i < MonsterProperties.Length; i++)
                if (Bit(m.SpecialPropertyFlags, i)) told.Add(MonsterProperties[i]);
            for (int i = 0; i < MonsterAttacks.Length; i++)
                if (Bit(m.SpecialAttackFlags, i)) { attacks++; told.Add(MonsterAttacks[i]); }
            for (int i = 0; i < SpellSchools.Length; i++)
                if (Bit(m.SpellFlags, i)) told.Add($"\"{SpellSchools[i]}\" Spells");

            if (told.Count == 0) s.Append(" have no special attacks or abilities.");
            else s.Append(attacks == 0 ? " abilities include: " : " abilities and attacks include: ")
                  .Append(Listed(told)).Append('.');

            s.Append("\r\n").Append(plural).Append(" are ");
            var res = new List<string>();
            for (int i = 0; i < Resistances.Length && i < m.Resistances.Length; i++)
                if (m.Resistances[i] > 0) res.Add($"{m.Resistances[i]}% {Resistances[i]}");
            s.Append(res.Count == 0 ? "not resistant to anything." : Listed(res) + " resistant.");
        }

        s.Append("\r\n\r\n").Append(e.Known switch
        {
            Everything => "It is estimated that all is known about this creature.",
            AlmostEverything => "It is estimated that almost everything is known about this creature.",
            AGoodDealMore => "It is estimated that there is a good amount more to learn about this creature.",
            _ => "It is estimated that very little is known about this creature.",
        });

        s.Append("\r\n\r\n");
        s.Append(e.NDeaths == 0
            ? "There are no known deaths associated with this monster."
            : $"Records have it that Explorers have been killed by this creature {e.NDeaths} "
              + (e.NDeaths == 1 ? "time." : "times."));

        s.Append("\r\n");
        if (e.NSeen > 0) s.Append($"A total of {e.NSeen} of these creatures have been seen.\r\n");

        s.Append(e.Name.TrimEnd() == ""
            ? "It is not known who first recorded this Creature."
            : $"\"{e.Name.TrimEnd()}\" was the first to record this Creature.");

        s.Append("\r\n");
        s.Append(e.Location.TrimEnd() == ""
            ? "This creature was last seen by a lost character, "
            : $"This creature was last seen at {e.Location.TrimEnd()}, ");
        s.Append(DaysAgo(e.LastSeen, today));
        return s.ToString();
    }

    // ---------------------------------------------------------------- items

    /// <summary>
    /// The Library's page on an item - MODULE11.BAS:232-400. Items have two grades rather than
    /// four: at anything below <see cref="Everything"/> the damage modifier, the curse, the spell it
    /// casts, its effects, what it does when used and the guild levels are all withheld.
    /// </summary>
    public static string DescribeItem(Item it, LibraryRecord e, int today, Func<int, string>? monsterName = null)
    {
        bool all = e.Known == Everything;
        var s = new System.Text.StringBuilder();
        s.Append("ITEM: ").Append(it.Name).Append("\r\n\r\n");
        s.Append($"This item is a member of the {Mdata1.ItemTypeName(it.Type)} class.\r\n");
        s.Append($"Offering an A/D of {it.Att}/{it.Def}");

        // the damage modifier is only shown for a weapon - class 0 - and only when all is known
        if (all && it.DamageMod != 0 && Character.WearClassOf(it) == 0)
            s.Append($" and a damage modifier of {it.DamageMod}");
        s.Append(" to a properly aligned character, this item ");

        if (all && it.Cursed != 0) s.Append("is CURSED, ");

        if (it.NHands > 0)
        {
            if (it.Swings == 1) s.Append("offers 1 swing and ");
            else if (it.Swings > 1) s.Append($"offers {it.Swings} swings and ");
            s.Append($"is a {Hand(it.NHands)}-handed Item");
        }
        else s.Append("requires no hands");

        if (all && it.SpellIndex > 0)
            s.Append($" and Casts \"{SpellName(it.SpellIndex)}\" up to {it.Charges} times.");
        else s.Append('.');
        s.Append("\r\n");

        if (all)
        {
            s.Append("This item ");
            var told = new List<string>();
            for (int i = 0; i < ItemAbilities.Length; i++)
                if (Bit(it.Abilities, i)) told.Add($"{ItemAbilities[i]} Ability");
            for (int i = 0; i < Resistances.Length; i++)
                if (Bit(it.ResistanceFlags, i)) told.Add($"{Resistances[i]} Resistance");
            s.Append(told.Count > 0
                ? "also offers the following when equipped: " + Listed(told) + "."
                : "offers no special abilities or resistances.");
        }
        s.Append("\r\n");

        s.Append("Requiring ");
        var needs = new List<string>();
        for (int i = 0; i < 6 && i < it.StatsRequired.Length; i++)
            if (it.StatsRequired[i] != 0) needs.Add($"{it.StatsRequired[i]} {RaceData.StatNamesLong[i]}");
        s.Append(needs.Count > 0 ? Listed(needs) + " to equip or use," : "no stats to equip or use,");

        var mods = new List<string>();
        for (int i = 0; i < 6 && i < it.StatsMod.Length; i++)
            if (it.StatsMod[i] != 0) mods.Add($"{it.StatsMod[i]} {RaceData.StatNamesLong[i]}");

        if (all)
        {
            switch (it.SpecialType)
            {
                case >= 1 and <= 10:
                    if (mods.Count > 0) s.Append($" and adding {Listed(mods)} permanently when used,");
                    break;
                case 11: s.Append(" and adding 50 Spell Points when used,"); break;
                case 12: s.Append(" and adding 200 Spell Points when used,"); break;
                case 13: s.Append(" and subtracting 1 year of age when used,"); break;
                case 14: s.Append(" and subtracting 10 years of age when used,"); break;
                default:
                    if (mods.Count > 0) s.Append($" and modifying {Listed(mods)} when equipped,");
                    break;
            }
            s.Append(it.ClassRestricted != 0
                ? " this is a Class Restricted Item " : " this is a non-Class Restricted Item ");
            s.Append("that is usable by the following Guilds: ");
        }
        else s.Append(" this item is usable by the following Guilds: ");

        var guilds = new List<string>();
        for (int i = 0; i < GuildData.All.Count; i++)
            if (Bit(it.Guilds, i))
                guilds.Add(all
                    ? $"{GuildData.All[i].Name} ({Character.UseLevel(it, GuildData.All[i])})"
                    : GuildData.All[i].Name);
        s.Append(Listed(guilds)).Append(".\r\n\r\n");

        s.Append(all
            ? "It is estimated that all is known about this Item."
            : "It is estimated that almost everything is known about this Item.");
        s.Append("\r\n\r\n");

        if (e.NSeen > 0)
        {
            s.Append($"Reports have it that {e.NSeen} of these items have been found, the last one from a ");
            string from = e.MonId >= 0 && monsterName != null ? monsterName(e.MonId) : "";
            s.Append(from == "" ? "unknown monster on level " : from + " on level ");
            s.Append(e.Location.TrimEnd());
            s.Append(DaysAgoForItem(e.LastSeen, today));
            s.Append("\r\n");
        }

        s.Append(e.Name.TrimEnd() == ""
            ? "It is not known who first recorded this Item."
            : $"\"{e.Name.TrimEnd()}\" was the first to record this Item.");
        return s.ToString();
    }

    private static string Hand(int n) => n >= 0 && n < HandWords.Length ? HandWords[n] : n.ToString();

    /// <summary>Filled in by the app, which is where the spell table is loaded.</summary>
    public static Func<int, string> SpellNameOf { get; set; } = _ => "a spell";
    private static string SpellName(int oneBasedIndex) => SpellNameOf(oneBasedIndex - 1);

    /// <summary>
    /// "just Today", "just 1 day ago", or "N days ago" - MODULE11.BAS:216-229. The stored value is
    /// VB's date serial, a whole number of days, and the original takes <c>Abs()</c> of the
    /// difference exactly as it does for the respawn clock. Only today's date being 0 - a clock that
    /// cannot say - gives "it is not known how many days ago." (HALL.BAS:224-225, and 381-382 for an
    /// item); a record with no date of its own is counted from 0 like any other, as the original
    /// counts it.
    /// </summary>
    private static string DaysAgo(int seen, int today)
    {
        if (today == 0) return "it is not known how many days ago.";
        int days = Math.Abs(seen - today);
        return days == 0 ? "just Today." : days == 1 ? "just 1 day ago." : $"{days} days ago.";
    }

    /// <summary>The item version, which punctuates differently (MODULE11.BAS:381-392).</summary>
    private static string DaysAgoForItem(int seen, int today)
    {
        if (today == 0) return "it is not known how many days ago.";
        int days = Math.Abs(seen - today);
        return days == 0 ? " just Today." : days == 1 ? ", just 1 day ago." : $", {days} days ago.";
    }

    /// <summary>VB's date serial: whole days since 30 December 1899.</summary>
    public static int Serial(DateTime when) => (int)(when.Date - new DateTime(1899, 12, 30)).TotalDays;

    // ---------------------------------------------------------------- the shared file

    /// <summary>
    /// The Library as it stands. The original's file holds one record per monster table index and
    /// one per item table index: <c>sub0F9A Ref, kind, record</c> reads record <c>Ref + 6</c>, items
    /// after the monsters (HALL.BAS:60-80), and every caller passes an index - sub06FE
    /// <c>p05D0.Ref</c> (MONSMOD.BAS:3963, 4005), sub0730 the pack's or the shelf's <c>Ref</c>
    /// (MAINMOD2.BAS:4284, 4327), frmLibrary its loop counter (FRMLIBRA.BAS:64-76).
    ///
    /// Keys:
    /// <list type="bullet">
    /// <item><c>"mr:index"</c> - a creature, by its table index (<see cref="Monster.Ref"/>). MDATA5's
    /// Ids are not unique (307 is the Morgul, index 278, and the Shadow Ninja, index 389), so the
    /// two have records of their own, as in the original.</item>
    /// <item><c>"m:id"</c> - a creature record written before 2026-10-18, by monster Id. Never
    /// written by this build: <see cref="SettleMonsterKeys"/> moves it to its index when the
    /// store is loaded with the monster table. The new prefix is the format marker, and it is
    /// per key on purpose: a build from before reads <c>"m:"</c> keys only, so it neither shows
    /// nor files over the index-keyed records (it keeps them untouched when it saves, as a
    /// dictionary entry it does not understand), and anything it does file is an <c>"m:"</c> key
    /// that the next load here settles again. A file-level marker would be dropped by such a
    /// build's save, which knows no such property.</item>
    /// <item><c>"i:id"</c> - an item, by its item Id. The original keys items by table index too,
    /// but MDATA3's item Ids are unique (selfcheck "the Library is keyed by table index"), and
    /// the port names an item by Id everywhere (pack, shelf, bank), so an item Id is exactly one
    /// table index and its record the one the original reads.</item>
    /// </list>
    /// </summary>
    public sealed class Store
    {
        public Dictionary<string, LibraryRecord> Entries { get; set; } = new();

        /// <summary>A creature's record, by monster table index.</summary>
        public const string MonsterPrefix = "mr:";
        /// <summary>A creature's record as it was written before 2026-10-18, by monster Id.</summary>
        public const string LegacyMonsterPrefix = "m:";
        /// <summary>An item's record, by item Id.</summary>
        public const string ItemPrefix = "i:";

        private static string MonsterKey(int tableIndex) => MonsterPrefix + tableIndex;
        private static string ItemKey(int itemId) => ItemPrefix + itemId;

        /// <summary>
        /// Loaded from a file holding Id-keyed creature records without the monster table to
        /// settle them: its creature records are not to be read or filed (see
        /// <see cref="SettleMonsterKeys"/>). Items are unaffected, and saving keeps the old keys
        /// as they are.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool MonstersUnsettled { get; private set; }

        private void MonstersSettled()
        {
            if (MonstersUnsettled)
                throw new InvalidOperationException("the Library was loaded without the monster table, and its creature records are still keyed by Id");
        }

        private LibraryRecord Get(string k) =>
            Entries.TryGetValue(k, out var e) && e != null ? e : new LibraryRecord();

        private LibraryRecord Make(string k)
        {
            if (!Entries.TryGetValue(k, out var e) || e == null) Entries[k] = e = new LibraryRecord();
            return e;
        }

        /// <summary>The record of the creature at table index <paramref name="tableIndex"/>, or an empty one.</summary>
        public LibraryRecord ForMonster(int tableIndex) { MonstersSettled(); return Get(MonsterKey(tableIndex)); }

        /// <summary>The record of the creature at table index <paramref name="tableIndex"/>, made if there is none.</summary>
        public LibraryRecord EditMonster(int tableIndex) { MonstersSettled(); return Make(MonsterKey(tableIndex)); }

        /// <summary>The record of the item of Id <paramref name="itemId"/>, or an empty one.</summary>
        public LibraryRecord ForItem(int itemId) => Get(ItemKey(itemId));

        /// <summary>The record of the item of Id <paramref name="itemId"/>, made if there is none.</summary>
        public LibraryRecord EditItem(int itemId) => Make(ItemKey(itemId));

        /// <summary>
        /// Whether the Library lists it: <c>If l005A.IdLevel Then lstList.AddItem</c> (FRMLIBRA.BAS:66,
        /// 75) - a record with a grade. sub0730 writes a record at grade 0 for an item read below
        /// Mostly (MAINMOD2.BAS:4285-4304), and that record is kept but not shown, nor its page.
        /// </summary>
        public bool KnowsMonster(int tableIndex) { MonstersSettled(); return Known(MonsterKey(tableIndex)); }

        /// <summary>As <see cref="KnowsMonster"/>, for an item by Id (FRMLIBRA.BAS:75).</summary>
        public bool KnowsItem(int itemId) => Known(ItemKey(itemId));

        private bool Known(string k) => Entries.TryGetValue(k, out var e) && e != null && e.Known > 0;

        /// <summary>
        /// Moves every creature record still keyed by monster Id (<c>"m:id"</c>, written before
        /// 2026-10-18) to its table index, and answers how many moved.
        /// <list type="bullet">
        /// <item>An Id that is one creature moves exactly (every Id but 307 in the shipped data).</item>
        /// <item>An Id two creatures share goes to the first table index with it - the original's own
        /// way from an Id to a creature, <c>fn0E0D</c> (MONSMOD.BAS:2269-2277, <c>Exit For</c>), which the
        /// item page uses for the very field that holds a monster Id in the Library (HALL.BAS:383),
        /// and the COMPANION SORTER's for a record it cannot place (MAINMOD2.BAS:4722-4727). So a
        /// 307 record is the Morgul's (index 278). A creature record holds nothing else that tells
        /// the two apart, and copying it to both would make sightings, deaths and a first recorder
        /// up for the Shadow Ninja.</item>
        /// <item>An Id no creature in this table has is left as it is, under its old key: nothing is
        /// dropped, and nothing reads it.</item>
        /// <item>Where the index already has a record - only when a build from before filed
        /// <c>"m:"</c> records into a file this build had already moved, so those are the newer -
        /// the two are put together as the filings would have gone on the one record: the grade
        /// the higher (only ever raised, MONSMOD.BAS:3986), the first recorder the record's own if
        /// it had a grade (3978-3985), the sightings and deaths added (4002-4003), the place the
        /// newer one's when it has one and the date the later.</item>
        /// </list>
        /// The record's own <c>Id</c> field is the creature's Id either way, as sub06FE writes it
        /// (MONSMOD.BAS:4004).
        /// </summary>
        public int SettleMonsterKeys(IReadOnlyList<Monster> table)
        {
            int moved = 0;
            foreach (var key in Entries.Keys.Where(k => k.StartsWith(LegacyMonsterPrefix, StringComparison.Ordinal)).ToList())
            {
                if (!short.TryParse(key.AsSpan(LegacyMonsterPrefix.Length), System.Globalization.NumberStyles.Integer,
                                    System.Globalization.CultureInfo.InvariantCulture, out short id)) continue;
                int r = CompanionRef.FromId(table, id, ItemAlignment.Unaligned);
                if (r < 0) continue;
                var from = Entries[key];
                Entries.Remove(key);
                string to = MonsterKey(r);
                if (Entries.TryGetValue(to, out var there) && there != null) Merge(there, from);
                else Entries[to] = from;
                moved++;
            }
            MonstersUnsettled = false;
            return moved;
        }

        private static void Merge(LibraryRecord there, LibraryRecord newer)
        {
            if (there.Known == 0) there.Name = newer.Name;
            if (there.Known < newer.Known) there.Known = newer.Known;
            there.NSeen += newer.NSeen;
            there.NDeaths = (short)Math.Min(short.MaxValue, there.NDeaths + newer.NDeaths);
            if (newer.Location != "") there.Location = newer.Location;
            if (newer.LastSeen > there.LastSeen) there.LastSeen = newer.LastSeen;
            if (there.Id == 0) there.Id = newer.Id;
        }

        public static string PathFor(string savesDir) => Path.Combine(savesDir, "library.json");

        /// <summary>
        /// Where a problem with the file is told: the app points it at its crash log. A file that
        /// cannot be read is never written over - see <see cref="Load"/>.
        /// </summary>
        public static Action<string>? Note { get; set; }

        /// <summary>Set on a store whose file could not be read and was left where it is: it is never saved.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool ReadOnly { get; private set; }

        /// <summary>Where a file that could not be read was moved to on this load, or null.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string? MovedAside { get; private set; }

        /// <summary>
        /// The Library file, or an empty Library when there is none. The file is read and written
        /// many times a game now - every room the party walks into - so a bad read must never turn
        /// into an empty file written over the good one:
        /// <list type="bullet">
        /// <item>a file that is there but cannot be opened (held by another program, say) gives a
        /// store that is never saved, and the file is left alone;</item>
        /// <item>a file that opens but is not a Library (garbled, cut short) is moved aside to
        /// <c>library.json.bad-yyyyMMdd-HHmmss</c>, as kaizen.json is, and the Library starts
        /// afresh; if it cannot be moved the store is not saved either.</item>
        /// </list>
        /// With <paramref name="monsters"/>, the whole monster table as the loader gives it, creature
        /// records still keyed by Id are moved to their table indices (<see cref="SettleMonsterKeys"/>);
        /// without it such a store reads and files items only (<see cref="MonstersUnsettled"/>).
        /// </summary>
        public static Store Load(string savesDir, IReadOnlyList<Monster>? monsters = null)
        {
            var s = Read(savesDir);
            if (s.Entries.Keys.Any(k => k.StartsWith(LegacyMonsterPrefix, StringComparison.Ordinal)))
            {
                if (monsters != null) s.SettleMonsterKeys(monsters);
                else s.MonstersUnsettled = true;
            }
            return s;
        }

        private static Store Read(string savesDir)
        {
            string p = PathFor(savesDir);
            if (!File.Exists(p)) return new Store();
            string text;
            try { text = File.ReadAllText(p); }
            catch (Exception ex)
            {
                Note?.Invoke($"{p} could not be read ({ex.Message}); the Library is not written to this time");
                return new Store { ReadOnly = true };
            }
            try
            {
                var s = JsonSerializer.Deserialize<Store>(text);
                if (s != null)
                {
                    s.Entries ??= new();
                    // a record written as null (by hand, or cut short) is no record: dropped, so
                    // nothing reading or filing it trips over it
                    foreach (var k in s.Entries.Where(p => p.Value == null).Select(p => p.Key).ToList()) s.Entries.Remove(k);
                    return s;
                }
            }
            catch (JsonException) { }
            string aside = p + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                File.Move(p, aside);
                Note?.Invoke($"{p} is not a Library file; it was moved aside to {aside} and the Library starts empty");
                return new Store { MovedAside = aside };
            }
            catch (Exception ex)
            {
                Note?.Invoke($"{p} is not a Library file and could not be moved aside ({ex.Message}); it is not written over");
                return new Store { ReadOnly = true };
            }
        }

        /// <summary>
        /// Written beside the file and moved over it, so a failure half way leaves the last good
        /// file; a store from a file that could not be read is not written at all.
        /// </summary>
        public void Save(string savesDir)
        {
            if (ReadOnly) return;
            string p = PathFor(savesDir);
            try
            {
                Directory.CreateDirectory(savesDir);
                string temp = p + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, p, overwrite: true);
            }
            catch (Exception ex) { Note?.Invoke($"{p} could not be written ({ex.Message})"); }
        }
    }

    /// <summary>
    /// <c>sub0730</c> (MAINMOD2.BAS:4273-4328) called as (char, ref, IdLevel, -1, False), which is
    /// how <c>sub14B5</c> files a given item (MAINMOD2.BAS:2962, 2994). The IdLevel passed in
    /// becomes a Library grade: fully known (m0372) is 4, Mostly (mc0378) is 3, anything else 0
    /// (:4285-4291). A record that had no grade takes this character's name (:4295-4301), even
    /// when the new grade is 0 too; the grade is only ever raised (:4302-4304). With -1 as the
    /// monster argument no date or finder is written (:4305-4313), and with False as the last no
    /// place and no sighting count (:4314-4325). The record is written either way (:4327).
    ///
    /// The count of items found that a new graded record bumps (<c>sub0FB0</c>, :4292-4294) has
    /// no counterpart in the port's store, the same as for <c>sub06FE</c>'s creature count.
    /// </summary>
    public static void FileItem(Store store, Character c, int itemId, IdLevel known)
    {
        short grade = known == IdLevel.All ? (short)4 : known == IdLevel.Mostly ? (short)3 : (short)0;
        var rec = store.EditItem(itemId);
        if (rec.Known == 0) rec.Name = c.Name;
        if (rec.Known < grade) rec.Known = grade;
        rec.Id = (short)itemId;
    }

    /// <summary><see cref="FileItem(Store, Character, int, IdLevel)"/> against the Library file itself.</summary>
    public static void FileItem(string savesDir, Character c, int itemId, IdLevel known)
    {
        var store = Store.Load(savesDir);
        FileItem(store, c, itemId, known);
        store.Save(savesDir);
    }

    /// <summary>
    /// <c>sub0730</c> (MAINMOD2.BAS:4273-4328) as the chest calls it, with a monster argument and
    /// True: <c>sub0730 l0526, l04FA, IdLevel, l04FC | -2, True</c> (MONSMOD.BAS:3842-3847). The
    /// grade and the finder's name are as <see cref="FileItem(Store, Character, int, IdLevel)"/>
    /// (4285-4304). Then the monster it came from - <c>Monsters(p065E).Id</c>, or -2 for one not
    /// known (4305-4313), which the page calls an "unknown monster" (HALL.BAS:383-386) - and
    /// today's date either way; then the level found on, <c>CStr(Chars(0).DungeonLevel)</c>, or
    /// "?" when either of Chars(0)'s lost flags is up (4314-4321); and one more found (4323).
    /// <paramref name="fromMonsterId"/> is the monster's Id, or -2: sub0730 writes
    /// <c>Monsters(p065E).Id</c>, not the index (4306), and the page turns it back into a creature
    /// with <c>fn0E0D</c>, the first table index with that Id (HALL.BAS:383, MONSMOD.BAS:2269-2277) -
    /// see <see cref="FinderName"/>. sub0730 draws no Rnd.
    /// </summary>
    public static void FileFind(Store store, Character c, int itemId, IdLevel known, short fromMonsterId,
                                int today, bool lostLevel, bool lostPosition, int level)
    {
        FileItem(store, c, itemId, known);
        var rec = store.EditItem(itemId);
        rec.MonId = fromMonsterId;
        rec.LastSeen = today;
        rec.Location = !lostLevel && !lostPosition ? level.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
        rec.NSeen++;
    }

    /// <summary>
    /// <c>sub0730 m0070, -l02C4, m029E, -2, True</c> (MAINMOD.BAS:1586): the store restock files an
    /// item someone else sold in, each time a shelf is actually raised by one (1582-1586). The item
    /// index comes negated, which is sub0730's mark of a find with no finder (MAINMOD2.BAS:4280-4283):
    /// a record with no grade gets no name, "" (4295-4301). <c>m029E</c> is 0, <c>m0372</c>, so the
    /// grade is 4, fully known (MAINMOD.BAS:75, MAINMOD2.BAS:4285-4286), only ever raised (4302-4304).
    /// The monster is -2, an unknown monster, with today's date (4309-4312); the last argument is
    /// True and the finder is no one, so the level is "?" and one more is found (4314-4323). The
    /// record's Id is the item's (4326). sub0730 draws no Rnd, so the restock's draws are as before.
    /// The count of items found that a new graded record bumps (sub0FB0, 4292-4294) has no
    /// counterpart in the port's store.
    /// </summary>
    public static void FileRestock(Store store, int itemId, int today)
    {
        var rec = store.EditItem(itemId);
        if (rec.Known == 0) rec.Name = "";
        if (rec.Known < Everything) rec.Known = Everything;
        rec.MonId = -2;
        rec.LastSeen = today;
        rec.Location = "?";
        rec.NSeen++;
        rec.Id = (short)itemId;
    }

    /// <summary>
    /// Which monster a chest's find is filed under (MONSMOD.BAS:3840-3848): the lead's own
    /// (<c>l04FC = Fight.M4F24</c>, 3678) when the room's first group is known for what it is
    /// (<c>Fight.M4F24 = Foes(0).KnownAs</c>) and is not an invisible creature the taker cannot
    /// see (M55BE bit 1 without Ailments(5)); -2, an unknown monster, otherwise.
    /// </summary>
    public static short ChestFindFrom(Monster lead, int leadIndex, int foes0KnownAs, Character taker) =>
        leadIndex == foes0KnownAs && !(MonsterProperty.Has(lead, MonsterProperty.Invisible) && !taker.SeeingInvisible)
            ? lead.Id : (short)-2;

    /// <summary>
    /// The creature an item page names as the find's source, from the monster Id the record holds:
    /// <c>Monsters(fn0E0D(M5C14)).Name</c> (HALL.BAS:383). <c>fn0E0D</c> (MONSMOD.BAS:2269-2277) is the
    /// first table index with that Id, <c>Exit For</c> - so a 307 is the Morgul's name - and with
    /// none the function's own 0 stands, the creature at index 0. Only called for an Id of 0 or
    /// more; an empty table names nothing.
    /// </summary>
    public static string FinderName(IReadOnlyList<Monster> table, int monsterId)
    {
        if (table.Count == 0) return "";
        foreach (var m in table) if (m.Id == monsterId) return m.Name;
        return table[0].Name;
    }

    /// <summary>
    /// <c>sub06FE</c> (MONSMOD.BAS:3938-4006) called as (record, False, True, False, character):
    /// not a kill, the ID level given rather than rolled, not a sighting. The Confinement's ID
    /// (FRMCITYC.BAS:194-198) and the Companions page's Info (picCompanion_DblClick,
    /// FRMCHAR3.BAS:757) both file a companion this way. The grade is the ID level turned round -
    /// 0 is 4, 1 is 3, 2 is 2, anything else 1 (MONSMOD.BAS:3969-3977) - and only ever raised; a new
    /// record takes this character's name (3978-3985). No date is written (3992, only when the
    /// level is rolled). The place is <c>Chars(m01BC)</c>'s X,Y,level, and only below ground and
    /// when neither of its two M59F4 flags is set (3987-3991): the caller passes it, or null.
    /// The special-creature exit (3958-3962) needs the level rolled, so it is not reached here.
    /// The count of creatures found that a new record bumps (sub0FB0) has no counterpart here.
    /// The record is the companion's <c>Ref</c>, its table index (FRMCITYC.BAS:194, FRMCHAR3.BAS:757):
    /// <paramref name="def"/> is the creature from the loaded table, and one built by hand (Ref -1)
    /// has no record to file to.
    /// </summary>
    public static void FileCompanion(Store store, Character c, Monster def, int idLevel, string? place = null)
    {
        if (def.Ref < 0) return;
        var rec = store.EditMonster(def.Ref);
        short grade = idLevel <= 0 ? (short)4 : idLevel <= 1 ? (short)3 : idLevel <= 2 ? (short)2 : (short)1;
        if (rec.Known == 0) rec.Name = c.Name;
        if (rec.Known < grade) rec.Known = grade;
        if (place != null) rec.Location = place;
        rec.Id = def.Id;
    }

    /// <summary><see cref="FileCompanion(Store, Character, Monster, int, string?)"/> against the Library file itself,
    /// loaded with the whole monster table <paramref name="table"/> so records from before are settled.</summary>
    public static void FileCompanion(string savesDir, IReadOnlyList<Monster> table, Character c, Monster def, int idLevel, string? place = null)
    {
        var store = Store.Load(savesDir, table);
        FileCompanion(store, c, def, idLevel, place);
        store.Save(savesDir);
    }

    /// <summary>
    /// Where sub06FE says a creature was seen: <c>Chars(m01BC)</c> - m01BC is 0, the party's first
    /// member - as "X,Y,level", and only below ground and when neither of its two M59F4 flags (the
    /// lost level and the lost position, set together at MONSMOD.BAS:3580-3581) is set
    /// (MONSMOD.BAS:3987-3991). The port keeps X and Y from 0, the original from 1. Null when it
    /// writes nothing. The square is the party's square as it stands now: the original moves
    /// Chars(0).X and .Y on every step (MAINMOD2.BAS:3786-3790 and the rest of mdrcharProcessKey),
    /// where the port's record is brought up to date only at a change of area (SavePos), so the
    /// dungeon passes its live square here, not the record's.
    /// </summary>
    public static string? PlaceOf(bool lostLevel, bool lostPosition, int level, int x, int y) =>
        !lostLevel && !lostPosition && level > 0 ? $"{x + 1},{y + 1},{level}" : null;

    /// <summary>
    /// <c>sub06FE</c> (MONSMOD.BAS:3938-4006) with the ID level rolled (p05D4 False) - every call
    /// the dungeon makes: a room's groups as sub0A91 reads them (MONSMOD.BAS:719 just after they
    /// are made, with the sighting flag; 732 every time, without), a group recognised for what it
    /// is (FRMDUNM.BAS:497, with the sighting flag) and a character killed by a group
    /// (MONSMOD.BAS:2756, 2826, 3042, 3095, 3227, with the death flag). This is what writes
    /// "This creature was last seen at X,Y,level, N days ago" (HALL.BAS:219-233).
    /// <list type="bullet">
    /// <item>A negative Ref is a lost character's creature: its own number, no finder's name and
    /// "?" for a place (3950-3952, 3982-3983, 3994-3995). Otherwise a group the party takes for
    /// something else is not filed at all: "If Ref &lt;&gt; KnownAs Then Exit Sub" (3954-3956).</item>
    /// <item>An invisible creature (M55BE bit 1) is not filed by a character without See
    /// Invisible (Ailments(5)) below ground (3958-3962).</item>
    /// <item>The grade is <c>fn1272(reader, GuildLevel, Floor, False)</c> turned round: 0 is 4, 1 is
    /// 3, 2 is 2, anything else 1 (3964-3977), and only ever raised (3986). A record with no grade
    /// takes the reader's name (3978-3985).</item>
    /// <item>The place is <paramref name="place"/>, from <see cref="PlaceOf"/> (3987-3991), with the
    /// first member's level <paramref name="leadLevel"/>; the date is today's, always, since the
    /// level is rolled here (3992). With the sighting flag, a lost character's creature is "?",
    /// and in the City a record with no place or date is "the City" (3993-4001).</item>
    /// <item>The death flag adds one to the deaths (4002); the sighting flag adds the group's count
    /// to the creatures seen (4003).</item>
    /// <item>The record is the group's table index: <c>sub0F9A p05D0.Ref</c> and <c>sub0FCB p05D0.Ref</c>
    /// (3963, 4005), so the Morgul and the Shadow Ninja, one Id between them, are filed apart.</item>
    /// </list>
    /// The count of creatures found that a new record bumps (sub0FB0, 3979) has no counterpart in
    /// the port's store, as for <see cref="FileCompanion(Store, Character, Monster, int, string?)"/>.
    /// </summary>
    public static void FileSighting(Store store, IReadOnlyList<Monster> all, int refIndex, int knownAs, int groupCount,
                                    bool killed, bool sighting, Character reader, string? place, int leadLevel, int today,
                                    Func<short, Item?>? item = null)
    {
        bool lost = false;
        if (refIndex < 0) { refIndex = Math.Abs(refIndex); lost = true; }
        else if (refIndex != knownAs) return;
        if (refIndex >= all.Count) return;
        var def = all[refIndex];
        if (MonsterProperty.Has(def, MonsterProperty.Invisible) && !reader.SeeingInvisible && reader.DungeonLevel > 0)
            return;
        int roll = CombatEngine.IdLevelRoll(reader, def, item, false);
        short grade = roll <= 0 ? (short)4 : roll <= 1 ? (short)3 : roll <= 2 ? (short)2 : (short)1;
        var rec = store.EditMonster(CompanionRef.IndexOf(all, def));
        if (rec.Known == 0) rec.Name = lost ? "" : reader.Name;
        if (rec.Known < grade) rec.Known = grade;
        if (place != null) rec.Location = place;
        rec.LastSeen = today;
        if (sighting)
        {
            if (lost) rec.Location = "?";
            else if (leadLevel == 0 && (rec.Location == "" || rec.LastSeen == 0)) rec.Location = "the City";
        }
        if (killed) rec.NDeaths++;
        if (sighting) rec.NSeen += groupCount;
        rec.Id = def.Id;
    }
}
