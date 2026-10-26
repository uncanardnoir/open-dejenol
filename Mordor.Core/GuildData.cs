namespace Mordor.Core;

/// <summary>
/// A guild: join requirements and progression parameters, as loaded from MDATA1.
/// </summary>
public sealed class GuildDef
{
    public required string Name { get; init; }
    public int MaxLevel { get; init; } = 999;
    /// <summary>Minimum STR, INT, WIS, CON, CHA, DEX to join.</summary>
    public int[] ReqStats { get; init; } = new int[6];
    /// <summary>bit0 Good, bit1 Neutral, bit2 Evil</summary>
    public int AlignMask { get; init; } = 7;
    /// <summary>
    /// bit i = race i (in MDATA1 file order) may join. Always the record's own dword, read by
    /// <see cref="MdrLoader.LoadGuilds"/> from the T5584 record GuildDefs is filled with
    /// (MODULE1.BAS:697-700); there is no stand-in value.
    /// </summary>
    public required int RaceMask { get; init; }
    /// <summary>Hits gained per level (the help's AH) until HitsCapLevel (ML); LateHits (MH) after that.</summary>
    public int AvgHits { get; init; }
    public int HitsCapLevel { get; init; }
    public int LateHits { get; init; }

    /// <summary>
    /// The fractional attack and defence a single level in this guild is worth. FRMCTYG.BAS adds
    /// these to the character per-guild A/D on every level gained. Fields 17 and 18 of the guild
    /// record, both Single.
    /// </summary>
    public float AttPerLevel { get; init; }
    public float DefPerLevel { get; init; }
    /// <summary>Field 15, the guild gold factor used by the cost of making a level.</summary>
    public int GoldFactor { get; init; }
    /// <summary>
    /// Field 16: how hard this guild finds it to use powerful gear. fn030F in MODULE1.BAS turns
    /// it into the guild level an item needs:
    /// <c>(factor + factor * 2.97) * item.LevelScale</c>, at least 1. A Nomad is lowest at 0.8
    /// and so gets to use things soonest; a Healer at 1.5 waits nearly twice as long.
    /// </summary>
    public float ItemUseFactor { get; init; }
    /// <summary>Field 19: the guild level past which no further A/D is granted.</summary>
    public int AdMaxLevel { get; init; }
    /// <summary>
    /// Field 20: A/D is only awarded on levels where <c>level Mod Int(AdEvery)</c> is zero, so a
    /// Nomad with 3 per award and an interval of 2 gains at levels 2, 4, 6 - which is exactly the
    /// published table (10/10 at 1, 13/13 at 2, 16/16 at 4, 19/19 at 6, 25/25 at 10).
    /// </summary>
    public float AdEvery { get; init; }

    /// <summary>Guild abilities from MDATA1 in the help's order: thieving, backstab, critical hit, extra swings, fighting (÷10), perception.</summary>
    public float[] Abilities { get; init; } = new float[7];
    public float Thieving => Abilities[0];
    public float Backstab => Abilities[1];
    public float CriticalHit => Abilities[2];
    public float ExtraSwings => Abilities[3];
    public float Perception => Abilities[5];
    /// <summary>Experience multiplier: higher = slower levelling.</summary>
    public float ExpFactor { get; init; }
    public int QuestPercent { get; init; }

    /// <summary>Per spell category (MDATA2 Category 0..17): guild levels needed per spell level. 0 = no access.</summary>
    public float[] SpellRate { get; init; } = new float[19];
    /// <summary>Per spell category: highest spell level this guild ever reaches (15 = everything).</summary>
    public int[] SpellMax { get; init; } = new int[19];

    /// <summary>True once the real MDATA1 spell table is loaded (all-zero means unknown, not "no spells").</summary>
    public bool HasSpellTable => SpellMax.Any(v => v > 0);

    /// <summary>
    /// Can a member at this guild level cast a spell of this category and level?
    ///
    /// FRMCHAR3.BAS:91-97, where the character's spell list is built:
    ///
    ///     If SpellDefs(s).Level &lt;= GuildDefs(g).M55F6(kind, 1) Then
    ///         rate = GuildDefs(g).M55F6(SpellDefs(s).Kind, 0)
    ///         If (SpellDefs(s).Level * rate) &lt;= ((Chars(Me).GuildsJoined(g).Level / 2) + .5#) Then
    ///
    /// so the test is the spell's rated level against <c>guildLevel / 2 + .5</c> - which is the
    /// caster's spell level, the same quantity <see cref="Rules.SpellLevel"/> computes. The bound
    /// is compared unrounded, as the original does; only the level the list *displays* is floored
    /// (FRMCHAR3.BAS:110).
    ///
    /// This used to read <c>guildLevel &gt;= Ceiling(spellLevel * rate)</c>, with no source, which
    /// handed out every spell at roughly half the guild level the original asks for.
    /// </summary>
    public bool CanCast(int category, int spellLevel, int guildLevel)
    {
        if (category < 0 || category >= SpellMax.Length) return false;
        if (SpellMax[category] <= 0 || spellLevel > SpellMax[category]) return false;
        float rate = SpellRate[category] <= 0 ? 1 : SpellRate[category];
        return spellLevel * rate <= guildLevel / 2.0 + 0.5;
    }

    /// <summary>
    /// The spell level this guild's list shows for a spell of this category and base level:
    /// <c>Int(Level * rate)</c>, FRMCHAR3.BAS:110. This is the number the help prints in brackets
    /// after a guild name, per its own explanation - "the level at which the spell is learned at by
    /// a particular guild is listed after the guild name".
    /// </summary>
    public int ShownSpellLevel(int category, int spellLevel)
    {
        float rate = category < 0 || category >= SpellRate.Length || SpellRate[category] <= 0 ? 1 : SpellRate[category];
        return (int)(spellLevel * rate);
    }

    public bool AllowsAlignment(Alignment a) => (AlignMask & (1 << (int)a)) != 0;
    public bool AllowsRace(string race) { int i = RaceData.IndexOf(race); return i < 0 || (RaceMask & (1 << i)) != 0; }
    public bool MeetsStats(int[] stats) => Enumerable.Range(0, 6).All(i => stats[i] >= ReqStats[i]);

    /// <summary>Human-readable list of unmet requirements (empty if joinable).</summary>
    public List<string> Unmet(Character c)
    {
        var why = new List<string>();
        for (int i = 0; i < 6; i++) if (c.Stats[i] < ReqStats[i]) why.Add($"{RaceData.StatNames[i]} {ReqStats[i]}");
        if (!AllowsAlignment(c.Alignment)) why.Add($"not for {c.Alignment}");
        if (!AllowsRace(c.Race)) why.Add($"not for {c.Race}");
        return why;
    }
}

public static class GuildData
{
    /// <summary>Empty until <see cref="LoadFrom"/> has read MDATA1; there is no built-in table to fall back on.</summary>
    public static IReadOnlyList<GuildDef> All { get; private set; } = Array.Empty<GuildDef>();
    public const string StartingGuild = "Nomad";

    /// <summary>
    /// The guild table from MDATA1, as <c>loadDataFile</c> reads it into GuildDefs
    /// (MODULE1.BAS:687-700). The original has no fallback table: a failed read leaves it with
    /// nothing (MODULE1.BAS:909-911). Here a missing or unreadable file throws, and a file with no
    /// guilds in it throws too, for the caller to show - the game cannot run without the table.
    /// </summary>
    public static void LoadFrom(string gameDir)
    {
        string path = Path.Combine(gameDir, "MDATA1.MDR");
        var guilds = MdrLoader.LoadGuilds(path);
        if (guilds.Count == 0) throw new InvalidDataException($"No guilds could be read from {path}.");
        All = guilds;
    }

    /// <summary>
    /// The guild of this name. The original never looks a guild up by name - GuildDefs is indexed
    /// by the membership's guild number (FRMCHAR3.BAS:693) - so a name that matches no guild has
    /// no counterpart there; it is a port bug or a damaged save, and it throws rather than
    /// quietly standing in the first guild.
    /// </summary>
    public static GuildDef ByName(string name) =>
        All.FirstOrDefault(g => g.Name == name)
        ?? throw new KeyNotFoundException($"No guild named \"{name}\" in MDATA1 ({All.Count} guilds loaded).");

    public static int IndexOf(string name) => All.ToList().FindIndex(g => g.Name == name);

    /// <summary>
    /// The guild half of <c>fn11B6</c> (MAINMOD2.BAS:589-610). MDATA3's Guilds field is a 12-bit
    /// mask in guild order (bit 0 = Nomad … bit 11 = Healer), read with <c>testBit</c>.
    ///
    /// An item tied to one guild (<c>M5742</c>, ClassRestricted) asks only about the current guild,
    /// "Your current guild cannot use this item!". Any other item is usable when <i>any</i> guild the
    /// character has joined - level above 0 - has its bit set (MAINMOD2.BAS:591-595), and refused
    /// with "Your currently joined guilds cannot use this item!" otherwise.
    ///
    /// Kept as the original has it: an empty mask sets no bit, so the loop finds nobody and the item
    /// is refused to everyone; there is no "empty means anyone" in fn11B6.
    /// </summary>
    public static bool CanUse(Item it, Character c)
    {
        if (it.ClassRestricted != 0)
        {
            int cur = IndexOf(c.PrimaryGuild);
            return cur >= 0 && cur < 31 && (it.Guilds & (1 << cur)) != 0;
        }
        foreach (var m in c.Guilds)
        {
            if (m.Level <= 0) continue;
            int i = IndexOf(m.Guild);
            if (i >= 0 && i < 31 && (it.Guilds & (1 << i)) != 0) return true;
        }
        return false;
    }
}
