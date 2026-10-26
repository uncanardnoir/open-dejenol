namespace Mordor.Core;

/// <summary>A monster instance in a fight (definition + current hits).</summary>
public sealed class CombatMonster
{
    public required Monster Def { get; init; }

    /// <summary>
    /// The hits on this member's record. Any write is a blow landing (or a record loaded), so it
    /// ends <see cref="AtNought"/>: a member standing on a record at nought falls to the next
    /// thing that writes its hits, as the original's does.
    /// </summary>
    public int Hits { get => _hits; set { _hits = value; AtNought = false; } }
    private int _hits;

    /// <summary>
    /// <c>Foes(g).MaxHits</c>: the hits this one was rolled with (sub0D08 sets MaxHits = Hits as it
    /// loads each member). A charm hands it to the new companion as its maximum (fn1163).
    /// </summary>
    public int MaxHits { get; set; }

    /// <summary>
    /// A member behind its group's front, with no hits yet - not yet rolled. The original holds
    /// one record per group, the front member's; the ones behind it are only a count, and each is
    /// given hits as it steps forward, when sub0D08 loads the record (MONSMOD.BAS:1991-2008), or
    /// takes over the record as it stands when nothing loads it. Until then its <see cref="Hits"/>
    /// are the creature's base figure, standing in so that it counts as alive; nothing reads them.
    /// </summary>
    public bool Unrolled { get; set; }

    /// <summary>
    /// Standing on a record at nought (or below): the group still counts it - GroupCount is above
    /// nought - but its <c>Foes(g).Hits</c> are not. Three ways in: sub0D08 or fn0D4D rolled a
    /// 1-hit creature to 0 (a Gra&#232;l Mosquito, a Piranha), or the member stepped up behind one
    /// that fell where nothing loaded the record afterwards (a companion's breath on a group not
    /// aimed at, MONSMOD.BAS:946-948; a charm with no slot free, MAINMOD2.BAS:1514-1516). It is not
    /// "not yet rolled": its record is loaded, and the next blow that writes its hits kills it,
    /// a spell or breath of nought included (MAINMOD2.BAS:1478-1492, MONSMOD.BAS:936-944); a
    /// weapon's miss writes nothing and leaves it standing (MONSMOD.BAS:440-442).
    /// </summary>
    public bool AtNought { get; set; }

    /// <summary>
    /// <c>sub0D08</c>'s load of the group record (MONSMOD.BAS:1997-2005): the creature's hits, then
    /// <c>Int(Rnd * 2)</c> for the sign and <c>Int(Rnd * (Hits * .5) / 2) + 1</c> for the amount -
    /// two draws, in that order - and MaxHits made the same. A 1-hit creature comes out at 0 or 2,
    /// and at 0 it stands (<see cref="AtNought"/>).
    /// </summary>
    public void LoadRecord(Random rng)
    {
        int hits = Def.Hits;
        int sign = (int)Math.Floor(rng.NextDouble() * 2);
        int swing = (int)Math.Floor(rng.NextDouble() * (Def.Hits * .5) / 2) + 1;
        TakeRecord(sign == 0 ? hits - swing : hits + swing, 0);
        MaxHits = Hits;
    }

    /// <summary>
    /// This member steps up onto the group record as it stands, nothing having loaded it: the hits
    /// and maximum of the one before it, a record at nought standing at nought.
    /// </summary>
    public void TakeRecord(int hits, int maxHits)
    {
        Hits = hits;
        MaxHits = maxHits;
        Unrolled = false;
        AtNought = hits <= 0;
    }

    /// <summary>
    /// <c>Foes(g).Alignment</c>, 0 Good 1 Neutral 2 Evil: the creature's own when sub0D08 makes the
    /// group (<c>Foes(m0286).Alignment = Monsters(m0284).Alignment</c>, MONSMOD.BAS:1880), the
    /// companion's when a companion turns (MONSMOD.BAS:569). -1 for a list built by hand, which
    /// reads as the creature's own.
    /// </summary>
    public int Alignment { get; set; } = -1;

    /// <summary>
    /// The alignment a companion made from this one takes: <c>fn1163</c> copies
    /// <c>p00D6.Alignment</c>, the foe's, into the new companion (MAINMOD2.BAS:392), and the
    /// Confinement pens it under that alignment when it is sold (FRMCITYC.BAS:288-289). The port
    /// had left it out, so every charmed or joined companion was Unaligned - a pen no column shows.
    /// </summary>
    public ItemAlignment CompanionAlignment => Alignment is >= 0 and <= 2
        ? (ItemAlignment)(Alignment + 1)
        : Confinement.AlignmentOf(Def);

    /// <summary>Ran off (after stealing) or was charmed; out of the fight but not killed.</summary>
    public bool Gone { get; set; }

    /// <summary>
    /// What the party believes this is, the original's <c>Foes(g).M4F01</c> - and the field every
    /// name is actually read from, not <c>M4ED4</c>. A party too weak to read a monster is given
    /// the name of something else of the same subtype, and goes on being given it until somebody
    /// works the creature out. The FAQ has the question: "How come some monster's names change
    /// while I'm fighting them? Because your character mis-identified the monster and later
    /// realized what it really was."
    /// </summary>
    public Monster? Believed { get; set; }

    /// <summary>The monster as the party would name it: what they believe, failing that what it is.</summary>
    public Monster Seen => Believed ?? Def;


    /// <summary>
    /// Whether this group means you harm, the original's <c>Foes(g).M4ECC</c>. Not every encounter
    /// does: MONSMOD.BAS:1708-1719 makes a good monster met by a good character friendly outright,
    /// and a neutral one friendly two times in five. A friendly group stands there, and MULTIMOD's
    /// own tick is what may later turn it hostile.
    /// </summary>
    public bool Hostile { get; set; } = true;

    /// <summary>
    /// How many of this group have offered to come with you, <c>Foes(g).M4EF9</c>. The help:
    /// "Sometimes one or more monsters may offer to join you and become your companions. When they
    /// do offer to join, you can press 'j' to allow them to follow you."
    /// </summary>
    public int JoinOffer { get; set; }


    /// <summary>
    /// Whether the party has worked out what this is. The original tracks it per group as
    /// Foes(g).M4EEE, and the ailment clock rolls to improve it every four seconds.
    ///
    /// MODULE4.BAS:1879 creates a group already identified and only clouds it when you meet them
    /// in fog: <c>M4EEE = 0</c>, then <c>If fog Then M4EEE = 1</c>. So you normally know exactly
    /// what you are fighting, and darkness is what takes that away.
    /// </summary>
    public bool Identified { get; set; } = true;
    public bool Alive => !Gone && (Hits > 0 || AtNought);

    /// <summary>
    /// Which of the four <c>Foes()</c> slots this member belongs to, or -1 when the list was built
    /// by hand. fn0D4D can put one monster in two slots running - spawn mode 3 repeats the
    /// previous group's monster (MONSMOD.BAS:1750-1752) - and those are two groups, not one.
    /// </summary>
    public int Group { get; set; } = -1;

    /// <summary>
    /// Whether two members stand in the same group: the same slot, or - for a list built by hand,
    /// with no slots - the same monster, which is how the port read groups before it had slots.
    /// </summary>
    public static bool SameGroup(CombatMonster a, CombatMonster b) =>
        a.Group >= 0 || b.Group >= 0 ? a.Group == b.Group : a.Def == b.Def;

    /// <summary>The monsters as the original's groups: each run of one group, in order.</summary>
    public static List<List<CombatMonster>> Runs(IEnumerable<CombatMonster> monsters)
    {
        var groups = new List<List<CombatMonster>>();
        foreach (var m in monsters)
            if (groups.Count > 0 && SameGroup(groups[^1][0], m)) groups[^1].Add(m);
            else groups.Add(new List<CombatMonster> { m });
        return groups;
    }

    /// <summary>
    /// The runs placed at their <c>Foes()</c> slots, an empty list for a slot with nobody in it. A
    /// room read back with a fight standing has only its standing slots made into members
    /// (<c>Finish</c> skips a record with GroupCount 0), and a slot emptied earlier is still a slot
    /// to the original's group-spell loop, which spends a step on it (l028E, MAINMOD2.BAS:1426-1448).
    /// A run of a list built by hand, or a companion that turned (Group -1), takes the slot after
    /// the run before it, as the runs always did.
    /// </summary>
    public static List<List<CombatMonster>> BySlot(IEnumerable<CombatMonster> monsters)
    {
        var slots = new List<List<CombatMonster>>();
        foreach (var run in Runs(monsters))
        {
            int at = run[0].Group >= slots.Count ? run[0].Group : slots.Count;
            while (slots.Count < at) slots.Add(new List<CombatMonster>());
            slots.Add(run);
        }
        return slots;
    }
}

/// <summary>
/// A monster's <c>M55BE</c> properties. These are *not* the item effect bits in
/// <see cref="Effects"/> even though both words are read the same way - on a monster, bit 1 is
/// invisibility and bits 4 and 5 are the two rules that can stop a weapon working at all
/// (MODULE4.BAS:347-363).
/// </summary>
public static class MonsterProperty
{
    /// <summary>Unseeable without See Invisible.</summary>
    public const int Invisible = 1;

    /// <summary>
    /// Fussy about steel: a weapon from a floor below half this monster's own does nothing to it.
    /// </summary>
    public const int NeedsGoodWeapon = 4;

    /// <summary>Weapons do not touch it at all, however good they are.</summary>
    public const int ImmuneToWeapons = 5;

    public static bool Has(Monster m, int bit) => (m.SpecialPropertyFlags & (1 << bit)) != 0;
}

/// <summary>
/// Monster special attacks (MDATA5 SpecialAttackFlags). Bit meanings were inferred from which
/// monsters carry them and matched to the help's list (poison, disease, stone, age, paralysis,
/// electrocute, drain, breath, acid, spells, critical hit, backstab, destroy item, steal).
///
/// **Nothing uses these.** Every test in this port is a bare <c>Bit(flags, n)</c> with the source
/// line quoted beside it, so these names have never been checked against anything - and one of
/// them was wrong. Bit 5 is the acid spit, which the original names outright as <c>mc0232</c> and
/// tests at MONSMOD.BAS:963; it was called DestroyItem here. The rest are still inference and are
/// marked as such rather than left to read like findings.
/// </summary>
public static class SpecialAttack
{
    /// <summary>Directly evidenced: the original's own constants for these three.</summary>
    public const int Fire = 1 << 3, Cold = 1 << 4, AcidSpit = 1 << 5;

    /// <summary>Inferred, and not confirmed against the source.</summary>
    public const int Poison = 1 << 0, Disease = 1 << 1, Paralyze = 1 << 2,
        Electric = 1 << 6, Drain = 1 << 7, Stone = 1 << 8, DeathTouch = 1 << 9, Critical = 1 << 10,
        StealGold = 1 << 11, Age = 1 << 12, StealItem = 1 << 13;
}

/// <summary>
/// Combat between a party of one to four characters (plus their companions) and a group of monsters,
/// using the original's formulas from <see cref="Rules"/>. Every action names the character taking
/// it, so a lone adventurer is simply a party of one.
/// </summary>
public sealed partial class CombatEngine
{
    private readonly Random _rng;
    private readonly Func<short, Item?> _item;
    private Func<Companion, Monster?>? _monsterDef;

    public Party Party { get; }
    public List<CombatMonster> Monsters { get; } = new();
    public List<string> Log { get; } = new();
    public bool Fled { get; private set; }

    /// <summary>Experience and gold earned by each character this fight.</summary>
    public Dictionary<Character, long> Experience { get; } = new();
    public long GoldEarned { get; private set; }
    /// <summary>The treasure class this encounter rolled: 0 means no chest at all.</summary>
    public int ChestClass { get; private set; }
    /// <summary>Index into <see cref="Rules.TrapNames"/>; 0 means the chest is not trapped.</summary>
    public int ChestTrap { get; private set; }
    /// <summary>The guild level of the monster guarding it, which opposes the disarm roll.</summary>
    public int ChestGuard { get; private set; }

    /// <summary>
    /// The original's Fight.M4F3D: 0 no lock, 1 locked, -1 picked open. A chest is locked on a
    /// roll against the lead monster's LockedChance, and only when the treasure class beat 1
    /// (MONSMOD.BAS:1697-1701). A locked chest carries half again as much gold (line 1976).
    /// </summary>
    public int ChestLocked { get; private set; }

    /// <summary>An opening spell picks the lock; the chest stays, and is worth more.</summary>
    public void UnlockChest() => ChestLocked = -1;
    public long ExpEarned => Experience.Values.Sum();

    /// <summary>
    /// Paralysis used to be held here, per fight, and was lost whenever the fight was. It is
    /// <see cref="Character.ParalysisLevel"/> now - <c>Ailments(2)</c> on the character, as in the
    /// original. This view writes straight through to it, for the probes that set it by hand.
    /// </summary>
    public ParalysisView Paralyzed { get; } = new();

    /// <summary>Reads and writes <see cref="Character.ParalysisLevel"/> as though it were a table.</summary>
    public sealed class ParalysisView
    {
        public int this[Character c] { get => c.ParalysisLevel; set => c.ParalysisLevel = value; }
        public bool Remove(Character c) { bool was = c.ParalysisLevel != 0; c.ParalysisLevel = 0; return was; }
    }


    /// <summary>
    /// Gold a thief took and then failed to escape with, so it is still in its pockets when it
    /// dies. The original keeps this **per character** (<c>gv1E66(victim).M68C8</c>,
    /// MONSMOD.BAS:3390) and that matters at the announcement: the total paid out sums everybody's
    /// (MONSMOD.BAS:2139-2144) but the wording and the number printed come from the *selected*
    /// character's own share alone (<c>gv1E66(gv1A86).M68C8</c>, MONSMOD.BAS:2155 and 2163).
    ///
    /// The port used to keep one party-wide figure, which told you that you had recovered gold
    /// when it was somebody else who was robbed, and printed the party's total as yours.
    /// </summary>
    public Dictionary<Character, long> GoldRecovered { get; } = new();

    /// <summary>Everything anybody got back, which is what goes into the pot.</summary>
    public long GoldStolen => GoldRecovered.Values.Sum();
    public List<short> ItemsStolen { get; } = new();
    public List<string> ItemsDestroyed { get; } = new();
    /// <summary>Whether the last cast actually took a turn (false = no valid target, nothing spent).</summary>
    public bool LastCastTookTurn { get; private set; } = true;

    public CombatEngine(Party party, IEnumerable<CombatMonster> monsters, Func<short, Item?> itemLookup, Random? rng = null, bool isLair = false)
    {
        Party = party; _item = itemLookup; _rng = rng ?? new Random();
        Monsters.AddRange(monsters);
        foreach (var m in Monsters) if (m.MaxHits == 0) m.MaxHits = m.Hits;
        // The original fixes an encounter's treasure when it generates the monsters
        // (MONSMOD.BAS) and only hands it over afterwards, in monGetGold - it is not
        // accumulated kill by kill.
        var kinds = Monsters.GroupBy(m => m.Def).ToList();
        // the chest the encounter rolled, and whether it is trapped and locked (MONSMOD.BAS:1651-1697).
        // All of it is decided here, once, because the gold depends on the same roll.
        var lead = Monsters.Count > 0 ? Monsters[0].Def : null;
        if (monsters is Encounter rolled)
        {
            Source = rolled;
            // fn0D4D has already rolled all of it, in its own order (MONSMOD.BAS:1656-1702 and
            // 1859-1978); what is left is monGetGold's scaling by the chest class
            ChestClass = rolled.TreasureClass;
            ChestTrap = rolled.ChestTrap;
            ChestLocked = rolled.ChestLock;
            ChestGuard = rolled.LeadMonster?.GuildLevel ?? lead?.GuildLevel ?? 0;
            GoldEarned = GoldAfterClass(rolled.Gold, rolled.TreasureClass);
            return;
        }
        if (lead != null)
        {
            ChestClass = Rules.ChestClass(lead, _rng.NextDouble() * 99, isLair);
            ChestTrap = Rules.ChestTrap(lead, ChestClass, _rng);
            ChestGuard = lead.GuildLevel;
            if (_rng.Next(100) + 1 <= lead.LockedChance && ChestClass > 1) ChestLocked = 1;
        }
        GoldEarned = Rules.EncounterGold(kinds.Select(k => k.Key).ToList(),
                                         kinds.Select(k => k.Count()).ToList(), _rng,
                                         ChestClass, ChestLocked == 1);

    }

    /// <summary>The room's records this fight was made from, when it was made from them.</summary>
    public Encounter? Source { get; }

    /// <summary>
    /// <c>Monsters(Fight.M4F24)</c>: the monster whose chest this is - the encounter's lead, which
    /// stays in the Fight record after every group has gone. A hand-built fight has only its first
    /// monster to go by.
    /// </summary>
    public Monster? ChestLead => Source?.LeadMonster ?? (Monsters.Count > 0 ? Monsters[0].Def : null);

    /// <summary>
    /// monGetGold's first lines (MONSMOD.BAS:2134-2143): the Currency <c>Fight.Gold</c> into a
    /// Long, then halved for no chest (or class 1), kept for class 2 and half again for class 3,
    /// each result stored back into the Long.
    /// </summary>
    public static long GoldAfterClass(decimal fightGold, int treasureClass)
    {
        long g = (long)Math.Round(fightGold, MidpointRounding.ToEven);
        double f = treasureClass switch { 0 or 1 => .5, 2 => 1, 3 => 1.5, _ => 1 };
        return treasureClass is >= 0 and <= 3 ? (long)Math.Round(g * f, MidpointRounding.ToEven) : g;
    }

    /// <summary>Convenience for a lone character.</summary>
    public CombatEngine(Character lone, IEnumerable<CombatMonster> monsters, Func<short, Item?> itemLookup, Random? rng = null, bool isLair = false)
        : this(new Party(lone), monsters, itemLookup, rng, isLair) { }

    private HashSet<Character>? _deadAtStart;

    /// <summary>
    /// Everyone who died in this fight, whether or not the party won it. sub091E logs every death
    /// as it happens; the port's engine only sets IsDead, so the window asks this at the end.
    /// </summary>
    public IEnumerable<Character> NewlyDead
    {
        get
        {
            _deadAtStart ??= new HashSet<Character>();
            return Party.Members.Where(c => c.IsDead && !_deadAtStart.Contains(c));
        }
    }

    /// <summary>Remember who was already dead when the fight began, so NewlyDead is exact.</summary>
    public void NoteWhoIsAlive() => _deadAtStart = Party.Members.Where(c => c.IsDead).ToHashSet();

    /// <summary>Lets companions use their monster stats: <c>Monsters(Companions(i).Ref)</c>, the whole table as loaded.</summary>
    public void UseMonsterTable(IReadOnlyList<Monster> table) => _monsterDef = c => c.Def(table);

    /// <summary>For the probes: companions looked up some other way (a creature built by hand).</summary>
    public void UseCompanionDefs(Func<Companion, Monster?> lookup) => _monsterDef = lookup;

    private IReadOnlyList<Spell>? _spells;
    /// <summary>The spell table, for the monsters that cast (fn0FF5 picks from it).</summary>
    public void UseSpellTable(IReadOnlyList<Spell> spells) => _spells = spells;

    /// <summary>The character whose commands you are entering.</summary>
    public Character Actor => Party.Selected;

    public bool IsOver => Fled || !Party.AnyAble || Monsters.All(m => !m.Alive);
    public bool Won => !Fled && Party.AnyAble && Monsters.All(m => !m.Alive);
    /// <summary>True when nobody in the party can act any more.</summary>
    public bool PartyWiped => !Party.AnyAble;
    /// <summary>Monsters actually killed (thieves that ran off and charmed ones do not count).</summary>
    public int Killed => Monsters.Count(m => m.Hits <= 0 && !m.Alive);

    /// <summary><c>Ailments(2)</c>: the paralysis level the character carries, nought when free.</summary>
    public int ParalyzedRounds(Character c) => c.ParalysisLevel;

    /// <summary>
    /// The stat-derived part of Att/Def plus what the original's stored totals have drifted by.
    /// <c>sub0D1D</c> mode 0 (MONSMOD.BAS:3884-3906) measures what <c>mdrcharSetAD</c> mode 0
    /// strips - the Str, Int, Wis and Dex terms, stats plus their mods (MAINMOD2.BAS:4455) - and
    /// takes exactly that off both Att and Def; mode 1 puts back what it measures then. That is
    /// <see cref="Character.ADShift"/>, so a paralysed character fights on base, guild and gear,
    /// and one the city gate freed keeps fighting without the stats.
    /// </summary>
    private int StatsADOf(Character c) => Rules.StatsAD(StatsWithGear(c)) + c.StoredADShift(_item);

    // ---- derived values, per character: stats A/D + best guild A/D + equipped items ----

    /// <summary>
    /// The six stats as A/D sees them. <c>sub047C</c> (MAINMOD2.BAS:4460 onward) reads
    /// <c>M55B5(i) + M584B(i)</c> throughout - the natural stat plus whatever equipped gear adds -
    /// and MAINMOD2.BAS:1761-1773 recomputes A/D on every equip and unequip for exactly that
    /// reason. A Girdle of Strength is worth its +2 in attack and defence, not nothing.
    /// </summary>
    private int[] StatsWithGear(Character c) =>
        Enumerable.Range(0, 6).Select(i => c.EffectiveStat(i, _item)).ToArray();

    public int AttackOf(Character c) => Math.Max(0, Rules.BaseAD + StatsADOf(c) + Rules.BestGuildAD(c).att + EquippedSum(c, i => i.Att));

    /// <summary>
    /// Defence. The Defend order looks as though it should add half again - MONSMOD.BAS:2684 does
    /// compute <c>l048C = l048C + (l048C * .5)</c> for it - but that value never reaches anything:
    /// <c>l048C</c> is unconditionally reassigned at MONSMOD.BAS:2906 (or 2909 for a companion)
    /// before its one and only use at 2946. The bonus is dead code in the original, so the port
    /// does not apply it either. Defending still stops you swinging, still switches on when a
    /// weapon turns out to be useless, and still makes you harder to rob (MONSMOD.BAS:3323).
    /// </summary>
    public int DefenseOf(Character c) =>
        Math.Max(0, Rules.BaseAD + StatsADOf(c) + Rules.BestGuildAD(c).def + EquippedSum(c, i => i.Def));

    /// <summary>
    /// What equipped gear adds to attack or defence. An item of the wrong alignment is worth half
    /// one step away and *minus its value* two steps away, which is what makes wearing an opposed
    /// item actively bad rather than merely pointless (MAINMOD2.BAS:1817-1827).
    /// </summary>
    private int EquippedSum(Character c, Func<Item, int> f) => WornSum(c, _item, f);

    /// <summary><see cref="EquippedSum"/> for callers outside a fight, such as the Char page.</summary>
    public static int WornSum(Character c, Func<short, Item?> item, Func<Item, int> f) =>
        c.Inventory.Where(s => s.Equipped)
         .Select(s => (slot: s, item: item(s.ItemId)))
         .Where(t => t.item != null)
         .Sum(t => ItemAlignments.AttDefWorth(f(t.item!), ItemAlignments.FitFor(t.slot.Alignment, c.Alignment)));


    private Item? WeaponOf(Character c) => c.Inventory.Where(s => s.Equipped).Select(s => _item(s.ItemId))
        .Where(i => i != null && i.Type <= 7 && i.Att > 0).OrderByDescending(i => i!.Att).FirstOrDefault();

    private static int SizeOf(Character c) => (int)RaceData.ByName(c.Race).Size;

    private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

    /// <summary>The guild whose fighting ability is best (the original uses the highest single ability, not a sum).</summary>
    private static (GuildDef def, int level) BestAbilityGuild(Character c, Func<GuildDef, float> pick)
    {
        GuildDef? best = null; int lvl = 1;
        foreach (var m in c.Guilds)
        {
            var d = GuildData.ByName(m.Guild);
            if (best == null || pick(d) > pick(best)) { best = d; lvl = m.Level; }
        }
        return (best ?? GuildData.ByName("Nomad"), lvl);
    }

    /// <summary>
    /// Which guild an ability comes from, and at what level - <c>fn122A</c>'s selection
    /// (MODULE6.BAS:1931-1978). It walks the guilds in table order, computes each one's value,
    /// and keeps it only on a strict improvement over a running best that starts at zero. Two
    /// consequences the simple "largest modifier" pick misses: a guild whose modifier is zero
    /// never wins, so a character with no ability at all gets level 0 back (and therefore no
    /// floor on the chance); and for backstab and critical the value is modifier times level, so a
    /// low-level specialist can lose to a high-level generalist.
    /// </summary>
    public static (double mod, int level) WinningGuild(Character c, int ability)
    {
        double best = 0, bestMod = 0; int bestLevel = 0;
        foreach (var g in GuildData.All)                 // table order, so ties go to the lower index
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            double mod = g.Abilities.ElementAtOrDefault(ability);
            double value = ability == 4
                ? Rules.FightingBase + (((Math.Log(100 + m.Level) / 1.75) - 2.3) * (mod * mod)) / 2
                : mod * m.Level;
            if (value > best) { best = value; bestMod = mod; bestLevel = m.Level; }
        }
        return (bestMod, bestLevel);
    }

    /// <summary>What one blow did.</summary>
    private enum Blow { Miss, Hit, Kill }

    /// <summary>
    /// Every kill this fight, by who landed it - MODULE4.BAS:471-479 offers each to the Hall of
    /// Records from inside the swing, for the killer only, and only when the group is known for
    /// what it is (and, for an invisible monster, only if the killer can see invisible).
    /// </summary>
    public List<(Character killer, Monster def)> RecordableKills { get; } = new();

    /// <summary>Experience for damage dealt, credited to whoever dealt it, scaled by party size.</summary>
    private void Credit(Character who, CombatMonster target, int dmg, bool byCompanion, DamageKind kind, bool removes = true)
    {
        int inflicted = Math.Min(dmg, Math.Max(0, target.Hits));
        // the hits this takes off the monster, for the mods (DamageDealt): every Credit is followed
        // at once by the blow's Hits -= dmg, so this is what it removes, never what it rolled past
        // nought. A record no longer in the fight (the stone's stale one) is nobody's monster.
        // A charm (removes false) takes the creature off alive: experience as the game pays it, no damage.
        if (removes && inflicted > 0 && DamageDealt != null && Monsters.Contains(target))
            Dealt(who, inflicted, byCompanion ? DamageKind.Companion : kind, 0);
        long xp = Rules.ExpAwarded(inflicted, target.Def.GuildLevel, who.Guild.Level, byCompanion, Party.Size);
        Experience[who] = Experience.GetValueOrDefault(who) + xp;   // the fight's record of it
        // and it is paid there and then, blow by blow (sub0CDF -> sub0F11, MONSMOD.BAS:468, 495): a party
        // that flees keeps what it earned
        GiveExperience(who, xp);
    }

    /// <summary>
    /// <c>sub0F11</c> (MAINMOD2.BAS:279-310): put experience on a character's total now, clamp it
    /// at the cap that pins them, and say so the first time either happens. Combat calls it for every blow
    /// as it lands; disarming a chest calls it on the spot,
    /// because <c>monOpenChest</c> awards its experience while the chest is still open and the
    /// fight is already over by then.
    /// </summary>
    public void GiveExperience(Character who, long xp, ExperienceSource source = ExperienceSource.Damage)
    {
        long before = who.Guild.Experience;
        // sub0F11 judges both lines by where the total stood before this award and where it stands
        // after, not by what has been said before: pinned if it was not before, and "enough to make
        // a level" if it had not been enough and did not pin (the pin sets l009C, 291-292)
        long cap = who.ExperienceCap();
        bool wasPinned = who.Guild.Experience >= cap;
        bool hadEnough = who.Guild.Experience >= who.ExpForNextLevel();
        who.Guild.Experience += xp;
        if (who.Guild.Experience >= cap)
        {
            hadEnough = true;
            who.Guild.Experience = cap;
            if (!wasPinned)
                _said.Add(Message(Party.Size > 1 ? $"\"{who.Name.TrimEnd()}\" is now pinned." : "You are now pinned."));
        }
        // fn12A1 alone: the guild's level ceiling is not asked here
        if (who.Guild.Experience >= who.ExpForNextLevel() && !hadEnough)
            _said.Add(Message(Party.Size > 1 ? $"\"{who.Name.TrimEnd()}\" now has enough experience to make a level."
                                             : "You now have enough experience to make a level."));
        // for the mods (ExperienceGained): what the total went up by, after the clamp at the pin
        ExperienceGained?.Invoke(new CombatExperience(who, Math.Max(0, who.Guild.Experience - before), source));
    }

    /// <summary>When the fight is won: the gold (experience went out blow by blow, and the kills as they fell).</summary>
    public void AwardSpoils()
    {
        // sub0C53 (MONSMOD.BAS:783-789) calls monGetGold only when the fight left no chest.
        // Otherwise the gold is in the chest, and monOpenChest pays it (3660) - so a chest nobody
        // opens, or one whose trap kills its opener, pays nothing.
        if (ChestClass == 0) PayGold();
        // the kills were counted as they happened, each to its killer (see KilledBy)
    }

    /// <summary>
    /// sub085B lines said in the middle of a blow - sub0CF3's quest news - waiting to be passed on
    /// after the line that caused them.
    /// </summary>
    private readonly List<CombatLine> _said = new();

    /// <summary>What has been said since last asked, for a caller acting outside <see cref="RoundPaced"/>.</summary>
    public List<CombatLine> TakeSaid()
    {
        var l = new List<CombatLine>(_said);
        _said.Clear();
        return l;
    }

    /// <summary>
    /// A character's own kill, by blow (MONSMOD.BAS:475) or by spell (MAINMOD2.BAS:1507): the killer's
    /// <c>M5906(0)</c> goes up, and nobody else's. Then sub0CF3 for the creature. A companion's kills
    /// run sub0CF3 alone, and a companion's spell neither.
    /// </summary>
    private void KilledBy(Character killer, Monster def, DamageKind how)
    {
        killer.Kills++;
        Dealt(killer, 0, how, 1);
        QuestKill(def);
    }

    /// <summary>
    /// A companion killed <paramref name="victim"/>: told to the mods (DamageDealt) as its owner's
    /// kill, of the Companion kind. The game itself counts nothing for it (no Kills; the owner's
    /// <c>M5906(0)</c> is the character's own). Not for a record no longer in the fight.
    /// </summary>
    private void CompanionKill(Character owner, CombatMonster victim)
    {
        if (DamageDealt != null && Monsters.Contains(victim)) Dealt(owner, 0, DamageKind.Companion, 1);
    }

    /// <summary>
    /// Hits taken off a monster, and kills, by a character or one of their companions
    /// (<see cref="CombatDamage"/>), for the mods: the game sets it to <c>ModHost.DamageDealt</c>.
    /// Null - the default - does nothing; set or not it draws nothing from the dice and changes
    /// nothing in the fight.
    /// </summary>
    public Action<CombatDamage>? DamageDealt { get; set; }

    /// <summary>
    /// Every award of experience (<see cref="GiveExperience"/>: sub0F11, whose only callers are a blow or
    /// spell's damage and a chest's trap disarmed), with what the total went up by
    /// (<see cref="CombatExperience"/>), for the mods: the game sets it to <c>ModHost.ExperienceGained</c>.
    /// Null - the default - does nothing; set or not it draws nothing from the dice and changes nothing.
    /// </summary>
    public Action<CombatExperience>? ExperienceGained { get; set; }

    private void Dealt(Character who, int amount, DamageKind kind, int kills) =>
        DamageDealt?.Invoke(new CombatDamage(who, amount, kind, kills, who == Party.Selected));

    /// <summary>
    /// sub0CF3 (MAINMOD2.BAS:1621-1639), at the moment of the kill: every character in the party whose
    /// current guild's monster quest is open and names this creature has it done, one more on
    /// <c>M5906(3)</c>, and is told - "You have completed your quest!" alone, or the name and "his"
    /// or "her" in a party. The fight need not be won for it to count.
    /// </summary>
    private void QuestKill(Monster def)
    {
        foreach (var c in Party.Members)
        {
            var g = c.Guild;
            if (g.QuestType != 1 || g.QuestDone || g.QuestTarget != def.Id) continue;
            c.MonsterQuestsDone++;
            _said.Add(Message(Party.Members.Count == 1 ? "You have completed your quest!"
                                                       : $"\"{c.Name.TrimEnd()}\" completed {HisHer(c)} quest!"));
            g.QuestDone = true;
        }
    }

    /// <summary>
    /// What the last <see cref="PayGold"/> put on lblCharacterHit for half a second - "" when its
    /// caption was empty (nothing found or recovered, MONSMOD.BAS:2159-2174), for the label is
    /// raised all the same (2158) - or null when monGetGold has not run.
    /// </summary>
    public string? GoldLine { get; private set; }

    /// <summary>
    /// <c>monGetGold</c> (MONSMOD.BAS:2126-2181): the encounter's gold and any recovered from a
    /// thief, split across the party. It empties <c>Fight.Gold</c>, so it pays once.
    /// </summary>
    public void PayGold()
    {
        GoldLine = null;
        // monGetGold (MONSMOD.BAS:2144-2178). The share goes to **everyone**, the dead included -
        // "For l039E = 0 To UBound(Chars)" asks nothing - and each of them gets the same
        // CLng(total / count), so the rounding is lost rather than handed to anybody. The port had
        // been paying only the able and giving the remainder to the first of them.
        long total = GoldEarned + GoldStolen;
        GoldEarned = 0;
        if (total > 0)
        {
            long each = GoldShare.Share(total, Party.Members.Count);
            // a purse at the top of Currency's range overflows here, and monGetGold's trap
            // (2133, 2184-2187) writes to the error file only and leaves the routine: the members
            // before are paid, the rest not, and no line is shown and nothing after it runs
            foreach (var m in Party.Members)
                if (VbCurrency.TryAdd(m.GoldOnHand, each, out var paid)) m.GoldOnHand = paid;
                else return;
        }
        // The wording and the figure it quotes come from the *selected* character's own recovered
        // gold, not the party's (MONSMOD.BAS:2160, 2168). Two of the four wordings there cannot be
        // reached at all - see GoldShare and docs/FIDELITY.md.
        //
        // The line is lblCharacterHit's caption (MONSMOD.BAS:2158, 2174), held half a second by
        // sub07E2 .5 (2175) and hidden again (2176). monGetGold has no sub085B, so it is not a Messages line:
        // it is left in GoldLine for the window to show, and not in the fight's log.
        GoldLine = GoldShare.Line(total, GoldRecovered.GetValueOrDefault(Party.Selected), Party.Members.Count) ?? "";
        // MONSMOD.BAS:2179-2181 clears every member's recovered gold once it has been paid out
        GoldRecovered.Clear();
    }

    /// <summary>
    /// A trap that has gone off is gone: <c>Fight.ChestTrap = False</c> (MONSMOD.BAS:3514). It
    /// matters only when the chest is still there afterwards - see OpenTheChest.
    /// </summary>
    public void TrapSprung() => ChestTrap = 0;

    /// <summary>
    /// <c>Fight = l05AC</c> (MONSMOD.BAS:3868): a chest that has been opened empties the whole
    /// fight record - no chest, no trap, no lock and no gold left in it.
    /// </summary>
    public void ChestEmptied()
    {
        ChestClass = 0; ChestTrap = 0; ChestLocked = 0; GoldEarned = 0;
    }

    /// <summary>
    /// <c>gv1E7C</c>: each character's rounds left to open the chest in a fight. -1 is the state
    /// every encounter starts in (sub0A91, MONSMOD.BAS:679-681) and the state a finished count
    /// goes back to; 0 is a count called off; above 0 is counting. Absent here means -1.
    /// </summary>
    public Dictionary<Character, int> ChestRounds { get; } = new();

    public int ChestRoundsOf(Character c) => ChestRounds.TryGetValue(c, out int n) ? n : -1;

    /// <summary>
    /// How many rounds it takes: <c>CInt(10 - (Log(abilityChance(Me, 0) - 10) * 2))</c>, never
    /// under 2 (MAINMOD2.BAS:4080-4083). A disarm chance of 10 or less is a Log error, and
    /// fn1521's handler leaves the count as it was - null here.
    /// </summary>
    public static int? OpeningRounds(double disarm)
    {
        if (disarm - 10 <= 0) return null;
        int n = (int)Math.Round(10 - Math.Log(disarm - 10) * 2, MidpointRounding.ToEven);
        return n < 2 ? 2 : n;
    }

    /// <summary>
    /// <c>sub0FE1</c> (MONSMOD.BAS:4476-4486): lblCharacterHit for the character in command -
    /// their count if they are opening, otherwise empty. Only while monsters stand.
    /// </summary>
    public CombatLine ChestCaption()
    {
        var me = Party.Selected;
        int n = ChestRoundsOf(me);
        return new CombatLine(n > 0 ? $"Opening Chest ({n} round(s) to go)" : "", me, Channel: LineChannel.CharacterHit);
    }

    /// <summary>
    /// The chest count at the head of each character's turn (MULTIMOD.BAS:517-531). Not
    /// counting: the caption is cleared if it is the character in command. Counting, with gold
    /// still in the chest: one round off, and at nought the count goes back to -1 and the chest is
    /// opened - by *this* character, whoever is on screen. With no gold left the count is simply
    /// dropped.
    /// </summary>
    private IEnumerable<CombatLine> ChestTick(Character c)
    {
        int n = ChestRoundsOf(c);
        if (n <= 0)
        {
            if (c == Party.Selected) yield return new CombatLine("", c, Channel: LineChannel.CharacterHit);
            yield break;
        }
        if (GoldEarned <= 0) { ChestRounds[c] = -1; yield break; }
        n--;
        if (n <= 0)
        {
            ChestRounds[c] = -1;
            yield return new CombatLine("", c, OpensChest: true);
        }
        else ChestRounds[c] = n;
        if (Monsters.Any(m => m.Alive)) yield return ChestCaption();
    }

    /// <summary>
    /// One line of a round: what to show, whose it is, whether the original pauses on it, and the
    /// sounds it makes. <see cref="SwingSound"/> plays before the swing (SWING or SLICE by weapon
    /// type, MODULE4.BAS:340-345) and <see cref="HitSounds"/> after a blow lands: RIP, BSTAB or
    /// CHIT for a special, then HIT (MODULE4.BAS:441-447), then the monster's own death sound
    /// (sub0CB7, MODULE9.BAS:94) if it killed. Both play for every member of the party unless
    /// MORDOR.INI's NoPartyFX is set, in which case only <see cref="Actor"/>'s own are heard -
    /// the frame decides that, because the flag is the game's and not the fight's.
    ///
    /// Two more carry <c>sub1005</c>'s spell sounds (<see cref="SpellSounds"/>), played before
    /// anything else on the line. <see cref="CastSounds"/> are mdrcharCastSpell's - one per group
    /// a damage spell reaches (MAINMOD2.BAS:1452-1455), under the same No Party Sound test, or
    /// the one after a non-combat cast by the character in command (1397-1403).
    /// <see cref="PickSounds"/> are fn0FF5's, when a monster or companion picks its spell
    /// (MONSMOD.BAS:245): no No Party Sound test guards those.
    /// </summary>
    public readonly record struct CombatLine(string Text, Character? Actor = null, bool Paced = false,
                                             string? SwingSound = null, IReadOnlyList<string>? HitSounds = null,
                                             LineChannel Channel = LineChannel.Log, bool OpensChest = false,
                                             IReadOnlyList<string>? CastSounds = null, IReadOnlyList<string>? PickSounds = null,
                                             int? AimAt = null, MessageKind Kind = MessageKind.None);

    /// <summary>
    /// A character's whole attack for the round. MODULE4.BAS:333 loops over the two weapon hands
    /// and, for each one holding a weapon, swings <c>character.Swings + weapon.Swings</c> times -
    /// so a round is not one blow but as many as your guild and your gear have earned you. The
    /// port has no explicit left and right hand, so it takes every weapon you have equipped,
    /// which is the same thing.
    /// </summary>
    public IEnumerable<CombatLine> PlayerSwings(Character actor, int? targetIndex = null)
    {
        var weapons = WeaponsOf(actor);
        // MODULE4.BAS:325-327 - no weapon in either hand, or a swing count that comes to nothing
        // (a new character with a 0-swing weapon can manage that): it says so and puts you on
        // guard. No pause for this line - the original only waits around a real swing.
        if (weapons.Count == 0 || weapons.All(w => actor.Swings + w.Swings < 1))
        {
            Party.OrderFor(actor).Kind = OrderKind.Defend;
            ItemOrders.Remove(actor);   // sub102F sets M5766 = -1 too (MULTIMOD.BAS:476-479)
            yield return new CombatLine(Caption(actor, "Using no weapon!"), actor, Channel: LineChannel.CharacterHit);
            yield break;
        }

        foreach (var w in weapons)
        {
            int swings = actor.Swings + w.Swings;
            for (int i = 0; i < swings; i++)
            {
                if (Fled || Monsters.All(m => !m.Alive)) yield break;   // timCombat.Enabled = False aborts sub1019
                var target = TargetFor(targetIndex);
                if (target == null) yield break;

                // Two ways a weapon can simply not work, checked before the swing is thrown
                // (MODULE4.BAS:347-363). Both `Exit For` - which ends THIS hand and goes on to the
                // other one, so a dual wielder with one useless weapon still swings the other.
                if (MonsterProperty.Has(target.Def, MonsterProperty.NeedsGoodWeapon)
                    && w.Floor < (int)(target.Def.LevelFound / 2))
                {
                    Party.OrderFor(actor).Kind = OrderKind.Defend;   // sub102F ... gc1DB4
                    ItemOrders.Remove(actor);
                    yield return new CombatLine(Caption(actor, "Weapon has no effect!"), actor, Channel: LineChannel.CharacterHit);
                    break;
                }
                if (MonsterProperty.Has(target.Def, MonsterProperty.ImmuneToWeapons))
                {
                    // no order change here - the original only switches you to Defend for the
                    // weak-weapon case and for no weapon at all
                    yield return new CombatLine(Caption(actor, "Weapon has no effect!"), actor, Channel: LineChannel.CharacterHit);
                    break;
                }

                var (blow, said, hitSounds) = SwingOnce(actor, target, w);
                yield return new CombatLine(said, actor, Paced: true, SwingSound: SwingSoundFor(w), HitSounds: hitSounds, Channel: LineChannel.CharacterHit);



                // MODULE4.BAS:436-437 and 462-464: a miss, or a blow the target survives, sets the
                // loop variable to its bound - the hand is finished. Only a kill lets the next
                // swing go, at whatever is still standing. Extra swings are for cutting through a
                // crowd, not for hitting one thing harder.
                // "If Foes(M547A).GroupCount < 1 Then M547A = fn0DCD(-1)" (MONSMOD.BAS:509-511)
                if (blow == Blow.Kill) Retarget(actor);
                if (blow != Blow.Kill) break;
            }
        }
    }

    /// <summary>
    /// <c>Foes(M547A)</c> for the swing: the group aimed at, whose record sub0D08 has loaded with
    /// the next member when the front fell (MONSMOD.BAS:497), and only once that group is gone the
    /// first one standing, "If Foes(M547A).GroupCount &lt; 1 Then M547A = fn0DCD(-1)" (509-511).
    /// The port had gone to the first monster standing as soon as the aimed-at member fell, so a
    /// second swing could land on another group while the aimed one still stood.
    /// </summary>
    private CombatMonster? TargetFor(int? targetIndex) =>
        targetIndex is int ti && ti >= 0 && ti < Monsters.Count && NextInGroup(Monsters[ti]) is CombatMonster aimed
            ? aimed
            : Monsters.FirstOrDefault(m => m.Alive);

    /// <summary>The whole attack as one line - the last blow. Convenience for the headless probes.</summary>
    public string PlayerAttacks(Character actor, int? targetIndex = null)
    {
        string last = "";
        foreach (var line in PlayerSwings(actor, targetIndex)) last = line.Text;
        return last;
    }

    /// <summary>Every weapon the character has equipped, standing in for the original's two hands.</summary>
    private List<Item> WeaponsOf(Character c) => c.Inventory.Where(s => s.Equipped)
        .Select(s => _item(s.ItemId)).Where(i => i != null && i.Type <= 7)   // M5997 holds anything of a weapon type, Att or no Att
        .Select(i => i!).ToList();

    /// <summary>
    /// One blow with one weapon at one target: MODULE4.BAS:373-490. The caption is the original's
    /// <c>lblCharacterHit</c> text, which it only shows for the character you are watching
    /// (<c>If pv00F4 = gv1A86</c>); the other members' blows make a sound and change the monster
    /// count, and nothing else. The log keeps to that.
    /// </summary>
    private (Blow blow, string said, string[] sounds) SwingOnce(Character actor, CombatMonster target, Item weapon)
    {
        // each ability comes from whichever guild fn122A picks for it, at that guild's level;
        // a level of 0 means no guild qualifies, and that is what withholds the floor
        var (fightMod, fightLvl) = WinningGuild(actor, 4);
        var (critMod, critLvl) = WinningGuild(actor, 2);
        var (backMod, backLvl) = WinningGuild(actor, 1);
        int dropLevel = weapon.Floor;
        // whether the weapon itself carries the ability: item effect bits 4 and 5
        bool wCrit = (weapon.Abilities & (1 << Effects.CriticalHit)) != 0;
        bool wBack = (weapon.Abilities & (1 << Effects.Backstab)) != 0;
        // rolled separately, and both can land on the same blow - the original adds +5 and +2 to
        // the damage modifier and never treats them as alternatives
        bool crit = _rng.Next(100) < Rules.CritChance(critLvl, critMod, target.Def.GuildLevel, dropLevel, wCrit);
        bool backstab = _rng.Next(100) < Rules.BackstabChance(backLvl, backMod, target.Def.GuildLevel, dropLevel, wBack);
        // both compare Int(Rnd * 100) against a Single, so the fractional part counts
        // MODULE4.BAS:422-426 - an unseen target takes half from anyone who cannot see it
        bool unseen = MonsterProperty.Has(target.Def, MonsterProperty.Invisible) && !actor.SeeingInvisible;
        // fn0DEE rolls to hit inside the damage function and answers 0 for a miss; the original
        // passes the monster's defence as an unassigned local, which is 0 (MODULE4.BAS:433)
        int dmg = Rules.CharacterDamage(_rng, fightLvl, fightMod, weapon.DamageMod, actor.EffectiveStat((int)Stat.Str, _item),
                                        SizeOf(actor) - target.Def.Size, crit, backstab, AttackOf(actor), unseen);
        if (dmg < 1)
            return (Blow.Miss, Caption(actor, "You missed!"), Array.Empty<string>());

        Credit(actor, target, dmg, byCompanion: false, DamageKind.Physical);
        target.Hits -= dmg;
        // MODULE4.BAS:441-447: RIP for a sever, BSTAB for a backstab, CHIT for a critical, and
        // HIT after any of them
        var sounds = new List<string>();
        if (crit && backstab) sounds.Add("RIP"); else if (backstab) sounds.Add("BSTAB"); else if (crit) sounds.Add("CHIT");
        sounds.Add("HIT");


        // MODULE4.BAS:453-460 for a survivor, 481-489 for a kill - four outcomes each, and landing
        // both at once is its own thing: "Severed". Str$ puts one space before the number.
        if (target.Alive)
        {
            string hit = crit && backstab ? $"You Severed one for {dmg} damage!"
                       : backstab ? $"You Backstabbed for {dmg} damage!"
                       : crit ? $"You Critically hit for {dmg} damage!"
                       : $"You hit for {dmg} damage!";
            return (Blow.Hit, Caption(actor, hit), sounds.ToArray());
        }

        string kill = crit && backstab ? "You Severed one!"
                    : backstab ? "You Backstabbed one!"
                    : crit ? "You Critically hit one!"
                    : "You Killed One!";
        // the creature record is the killer's, and only for a monster they know for what it is
        if (target.Identified && (!MonsterProperty.Has(target.Def, MonsterProperty.Invisible) || actor.SeeingInvisible))
            RecordableKills.Add((actor, target.Def));
        KilledBy(actor, target.Def, DamageKind.Physical);   // MONSMOD.BAS:475 and 496
        Sub0D08(target, target);       // 497: the next one steps forward with its hits
        sounds.Add(DeathSound(target.Def));
        return (Blow.Kill, Caption(actor, kill), sounds.ToArray());
    }

    /// <summary>
    /// What the swing loop puts on <c>lblCharacterHit</c>. It goes into the log only for the
    /// character being watched, because that is the only one the original shows it for; the text
    /// itself comes back for whoever swung, so the probes can see every blow.
    /// </summary>
    private string Caption(Character actor, string text)
    {
        if (actor == Party.Selected) Say(text);
        return text;
    }
    /// <summary>MODULE4.BAS:340-345 - sound 36 (SWING) for weapon types 0, 1, 2, 4, 5 and 7; 46 (SLICE) otherwise.</summary>
    public static string SwingSoundFor(Item weapon) => weapon.Type is 0 or 1 or 2 or 4 or 5 or 7 ? "SWING" : "SLICE";

    /// <summary>
    /// <c>sub0CB7</c> (MODULE9.BAS:94), the sound a monster makes when it dies, by class: humans,
    /// thieves, mages and warriors DI-HUMAN; demons, dragons and giants DI-DEMON; devils, animals,
    /// mythicals and lycanthropes DI-ANIM, except picture 18 which is BIRDDIE; insects BUGDIE;
    /// undead DI-UNDEA; everything else the plain KILL.
    /// </summary>
    public static string DeathSound(Monster def) => def.Kind switch
    {
        MonsterKind.Humanoid or MonsterKind.Thief or MonsterKind.Mage or MonsterKind.Warrior => "DI-HUMAN",
        MonsterKind.Demon or MonsterKind.Dragon or MonsterKind.Giant => "DI-DEMON",
        MonsterKind.Devil or MonsterKind.Animal or MonsterKind.Mythical or MonsterKind.Lycanthrope => def.PicId == 18 ? "BIRDDIE" : "DI-ANIM",
        MonsterKind.Insect => "BUGDIE",
        MonsterKind.Undead => "DI-UNDEA",
        _ => "KILL",
    };


    public string PlayerAttacks(int? targetIndex = null) => PlayerAttacks(Actor, targetIndex);

    /// <summary>
    /// One second of combat, the original's <c>mdrmulInitiateCombatRound</c> (MULTIMOD.BAS:497).
    /// A timer fires this once a second the whole time you are in the dungeon - combat is not
    /// turn-based, and nothing waits for you.
    ///
    /// The order is the source's: each living character in turn carries out whatever standing order
    /// they hold and then their companions attack, and after all of them the monsters go. A paralysed character is skipped
    /// (<c>If Chars(i).Ailments(2) > 0 Then Exit Sub</c> at the top of sub1019), and one holding
    /// None or Defend simply does not attack - Defend pays off in <see cref="DefenseOf"/>.
    /// </summary>
    public IEnumerable<string> Round(IReadOnlyList<Spell> spells)
    {
        foreach (var line in RoundPaced(spells)) yield return line.Text;
    }


    /// <summary>
    /// The round, one action at a time, saying which character each came from and whether the
    /// original would pause on it. It pauses a quarter of a second twice around every swing of the
    /// character you are watching - <c>sub07E2 .25#</c> with the caption cleared (MODULE4.BAS:367)
    /// and again with the result showing (MODULE4.BAS:498), both guarded by
    /// <c>If pv00F4 = gv1A86</c> - and not at all for "Using no weapon!" or "Weapon has no
    /// effect!". A round is not a burst: your blows land one at a time.
    /// </summary>
    public IEnumerable<CombatLine> RoundPaced(IReadOnlyList<Spell> spells)
    {
        _spells ??= spells;
        if (IsOver) yield break;

        // mdrmulInitiateCombatRound (MULTIMOD.BAS:511-539) does each character whole before the
        // next: their own action (sub1019), then their companions (monCompanionAttack), and only
        // when every character has had that do the monsters go (sub0A64, 555). The loop leaves as
        // soon as nothing is standing ("If fn0EEF() = False Then Exit For"), so a companion that
        // kills the last monster stops the characters after its owner from acting at all. This
        // port ran every character, then every companion.
        foreach (var c in Party.Able.ToList())
        {
            if (IsOver || !Monsters.Any(m => m.Alive)) break;
            foreach (var line in ChestTick(c)) yield return line;
            foreach (var line in WithSaid(CharacterActs(c, spells))) yield return line;
            if (IsOver || !Monsters.Any(m => m.Alive)) break;
            foreach (var line in WithSaid(CompanionTurn(c))) yield return line;
        }

        // MULTIMOD.BAS:542-553: after the characters' turns, anyone dead (a companion's mishap is
        // mdrcharSetDead alone, MONSMOD.BAS:901-1305) sends the party list its UPDATE, which
        // unloads everyone at nought while more than one is left (FRMGROUP.BAS:321-346) - before
        // the monsters go. The port had left those bodies on the list to the end of the fight.
        // after the characters, each one aimed at a group that has gone is aimed at the first one
        // standing (MULTIMOD.BAS:542-551) - before the monsters go, and before the list prunes
        foreach (var m in Party.Members.Where(m => !m.IsDead).ToList()) Retarget(m);
        if (Party.Size > 1 && Party.Members.Any(c => c.IsDead)) ListUpdate?.Invoke();

        // "If Foes(0).Engaged = 1 And fn0EEF() > 0 And ... Then sub0A64" (MULTIMOD.BAS:556-557):
        // the monsters go whenever any stand, whether or not anybody is left to fight them. A lone
        // character a companion's mishap killed (mdrcharSetDead alone, MONSMOD.BAS:901-1305) is
        // still on the list, so the first group draws its target (MULTIMOD.BAS:315) and sub0E85
        // leaves at once on the DeathState (MONSMOD.BAS:2598) - no blow - before sub0930 and the
        // Exit For (362-364). The port stopped on IsOver and that draw was never made.
        if (Fled || !Monsters.Any(m => m.Alive) || Party.Members.Count == 0) yield break;
        foreach (var line in MonsterAttacks(AttackKind.Round)) yield return line;
    }

    /// <summary>Each line, then whatever sub0CF3 said while it was being worked out.</summary>
    private IEnumerable<CombatLine> WithSaid(IEnumerable<CombatLine> lines)
    {
        foreach (var l in lines)
        {
            yield return l;
            foreach (var s in TakeSaid()) yield return s;
        }
        foreach (var s in TakeSaid()) yield return s;
    }

    /// <summary>
    /// <c>sub1019</c>: one character's standing order. A paralysed character does nothing
    /// ("If Chars(i).Ailments(2) > 0 Then Exit Sub" at its top) - but their companions, called
    /// separately, still fight.
    /// </summary>
    private IEnumerable<CombatLine> CharacterActs(Character c, IReadOnlyList<Spell> spells)
    {
        // "If Chars(pv00F4).Ailments(mc0122) > 0 Then Exit Sub" (MONSMOD.BAS:291); it wears off
        // on the ailment clock, not the round
        if (ParalyzedRounds(c) > 0) yield break;
        // "If Foes(M547A).GroupCount < 1 Then M547A = fn0DCD(-1)" (MONSMOD.BAS:303-305)
        Retarget(c);

        var order = Party.OrderFor(c);
        if (order.Kind == OrderKind.Fight)
        {
            foreach (var line in PlayerSwings(c, order.Target >= 0 ? order.Target : null))
                yield return line;
            yield break;
        }
        if (order.Kind == OrderKind.Spell)
        {
            var s = spells.FirstOrDefault(sp => sp.Id == order.SpellId);
            if (s == null) { order.Clear(); yield break; }
            // an item's order: M56C4 >= 1000 casts at the item's own M5734 (MONSMOD.BAS:312-313)
            ItemOrders.TryGetValue(c, out var io);
            if (io is { ItemCast: true })
            {
                foreach (var line in ItemOrderActs(c, order, s, io)) yield return line;
                yield break;
            }
            // the whole of the check, every round: sub1019 (MONSMOD.BAS:315) calls mdrcharCastSpell, which asks it (MAINMOD2.BAS:831)
            if (RoundCheckCast(c, order, s, 0) is CombatLine refused) { yield return refused; yield break; }
            // a spell of your own with an item slot still in M5766 - cmdCast_Click sets M56C4 and leaves
            // M5766 (FRMCHAR3.BAS:180) - still takes the item's charge per cast (MAINMOD2.BAS:841-851)
            CastSpellDraw();   // l01D0 (MAINMOD2.BAS:840), after the check and before the charge
            if (io != null && ItemCharge(c, order, io) is CombatLine runOut) { yield return runOut; yield break; }
            string said = CastSpell(c, s, order.Target >= 0 ? order.Target : null);
            // a damage spell's captions go on lblCharacterHit, each held half a second for the
            // character in command (sub07E2 .5#, MAINMOD2.BAS:1566-1568)
            if (LastCaptions.Count > 0)
                foreach (var line in CaptionLines(c)) yield return line;
            else if (said != "") yield return new CombatLine(said, c, Channel: LineChannel.Message, CastSounds: LastSaidSounds, Kind: MessageKind.Party);   // a sub085B line
        }
    }

    /// <summary>
    /// An item used in a fight: sub15CD does not throw it there and then but makes it the order -
    /// <c>Att = False : M56C4 = spell + 1000 : M5766 = slot</c> (MAINMOD2.BAS:6129-6137) - and leaves
    /// by Exit Sub before its own charge is spent. Each round sub1019 then casts it at the item's
    /// level (MONSMOD.BAS:312-313) and mdrcharCastSpell takes a charge per cast (MAINMOD2.BAS:841-852).
    /// </summary>
    /// <param name="ItemCast">M56C4 &gt;= 1000: the order is the item's own spell. False once a spell of
    /// your own has replaced it while M5766 stayed.</param>
    public sealed record ItemOrder(InventorySlot Slot, Item Item, bool ItemCast);

    /// <summary><c>CombatOrders(c).M5766</c>, per character: the pack slot an item order draws on.</summary>
    public Dictionary<Character, ItemOrder> ItemOrders { get; } = new();

    /// <summary>
    /// mdrcharCastSpell's charge (MAINMOD2.BAS:841-851): one off the slot in M5766 when it has any
    /// (mdrcharSubtractItemUse - a consumable goes with its last), else the run-out line and
    /// sub102F caster, -1, which with M56C4 holding a spell is Fight and M5766 = -1 (MULTIMOD.BAS:476-479).
    /// Null when the cast goes on.
    /// </summary>
    /// <summary>
    /// <c>l01D0 = Int(Rnd * 100)</c> (MAINMOD2.BAS:840): mdrcharCastSpell draws it on every call
    /// that passes its check, before the item's charge, and never reads it.
    /// </summary>
    private void CastSpellDraw() => _ = _rng.NextDouble();

    private CombatLine? ItemCharge(Character c, CombatOrder order, ItemOrder io)
    {
        if (io.Slot.Charges > 0) { Spellbook.SpendCharge(c, io.Slot, io.Item); return null; }
        // sub102F pv01A6, -1 (850): Fight when a spell is queued - the caster's own cast always has
        // one - and Defend for an owner whose companion cast on an item the owner holds no spell
        // order for (None or Fight with M5766 still set, as a refusal can leave it)
        order.Sub102FMinusOne();
        ItemOrders.Remove(c);
        return Message(c == Party.Selected
            ? "Your item has run out of charges!"
            : $"The item \"{c.Name.TrimEnd()}\" was using ran out of charges!");
    }

    private IEnumerable<CombatLine> ItemOrderActs(Character c, CombatOrder order, Spell s, ItemOrder io)
    {
        int lvl = io.Item.SpellLvl;
        // the check with p0100 = M5734 - the call at MONSMOD.BAS:313 passes the item level on: a level of its own skips the
        // knowing, level, stat and spell point tests (MAINMOD2.BAS:439-510) but not the Non-Combat
        // or the anti-magic ones (512-526); at nought it is gated like a spell of your own
        if (RoundCheckCast(c, order, s, lvl) is CombatLine refused) { yield return refused; yield break; }
        // "If Chars(pv01A6).Pack(M5766).Charges > 0 Then mdrcharSubtractItemUse ... Else ... sub102F
        // pv01A6, -1 : Exit Function" (MAINMOD2.BAS:841-851). With M56C4 still holding the item's
        // spell, sub102F's -1 takes its second branch (MULTIMOD.BAS:476-479): Att = 1, Fight.
        CastSpellDraw();   // l01D0 (MAINMOD2.BAS:840), after the check and before the charge
        if (ItemCharge(c, order, io) is CombatLine runOut) { yield return runOut; yield break; }
        // points are charged only for an item of level 0 (MAINMOD2.BAS:1355)
        string said = CastSpell(c, s, order.Target >= 0 ? order.Target : null, free: lvl != 0, itemLevel: lvl);
        if (LastCaptions.Count > 0)
            foreach (var line in CaptionLines(c)) yield return line;
        else if (said != "") yield return new CombatLine(said, c, Channel: LineChannel.Message, CastSounds: LastSaidSounds, Kind: MessageKind.Party);
    }

    /// <summary>
    /// What monCompanionAttack keeps between one companion and the next for the same owner. It is
    /// one call per character, and these are its locals.
    /// </summary>
    private sealed class CompanionRound
    {
        /// <summary>
        /// <c>l0208</c> (MONSMOD.BAS:875-957): set when a breath leaves <see cref="Kills"/> above
        /// nought. Only the cold breath asks it - see <see cref="CompanionTurn"/>.
        /// </summary>
        public bool Breathed;

        /// <summary>
        /// <c>l020C</c>: kills in the sweep, zeroed at the start of each group the sweep reaches, so
        /// what is left in it is the *last* group's count. A breath that killed in the first group
        /// and only scorched the second does not set <see cref="Breathed"/>. A breath that went
        /// astray never touches it, and the test after it reads whatever an earlier companion's
        /// sweep left.
        /// </summary>
        public int Kills;

        /// <summary>
        /// <c>l01F8</c>: " and his companions" or " and her companions", written by the breath's
        /// mishap and never cleared for the next companion. Every later "accidently" line for a
        /// character carries it, so a spit or a blow can report companions it never touched.
        /// </summary>
        public string Suffix = "";

        /// <summary>
        /// <c>l01EA</c> (MONSMOD.BAS:1038-1042): one of this owner's companions has picked a spell
        /// this round, so none of the others will.
        /// </summary>
        public bool Cast;

        /// <summary>A character died, and the original leaves the routine with <c>Exit Sub</c>.</summary>
        public bool Over;
    }

    /// <summary>
    /// <c>monCompanionAttack</c> for one owner (MONSMOD.BAS:806 onward). Each living companion in
    /// slots 0 to 3, in turn: its breath, then the friendly-fire draw, then its spit, then its
    /// blow - and the blow's stone and electrocution ride on the blow.
    ///
    /// **The friendly-fire draw is for everybody.** MONSMOD.BAS:961-967 reset the victim to none,
    /// roll <see cref="CompanionMishap.Kind.Spit"/>'s odds and, on a mishap, draw
    /// <see cref="CompanionSpit.Slot"/> - all *before* the spit bit is tested at 968. The blow
    /// then aims at that victim (MONSMOD.BAS:1089-1097). So a companion that cannot spit at all still, now and
    /// then, strikes a party member or one of their companions, and on the spit's odds. The only
    /// thing that could replace the victim, the companion's spell (MONSMOD.BAS:1045), is not built in this
    /// port; a companion with spells swings instead of casting (listed in FIDELITY).
    ///
    /// **The breath guard binds tighter than it reads.** MONSMOD.BAS:875 is
    /// <c>bit3 Or bit4 And l0208 = False</c>, and VB's <c>And</c> takes precedence over its
    /// <c>Or</c>: a fire breather is never stopped by an earlier breath, only a cold one is.
    /// </summary>
    private IEnumerable<CombatLine> CompanionTurn(Character owner)
    {
        var round = new CompanionRound();
        int owned = Party.Members.IndexOf(owner);

        for (int at = 0; at < CompanionSlotsWalked; at++)
        {
            // Ref > 0 And Hits > 0 (MONSMOD.BAS:873): a fallen companion lying in its slot does not fight
            if (owner.Companions[at] is not Companion c || c.Hits <= 0) continue;
            if (!Monsters.Any(m => m.Alive)) yield break;
            if (DefOf(c) is not Monster def) continue;

            if (Bit(def.SpecialAttackFlags, 3) || (Bit(def.SpecialAttackFlags, 4) && !round.Breathed))
            {
                foreach (var line in CompanionBreathes(owner, c, def, round)) yield return line;
                if (round.Over) yield break;
            }

            int victim = -1, slot = -1;
            if (CompanionMishap.Victim(_rng, CompanionMishap.Kind.Spit, def.GuildLevel, Party, owned) is int v
                && v < Party.Members.Count)
            {
                victim = v;
                slot = CompanionSpit.Slot(_rng, owner);
            }

            if (Bit(def.SpecialAttackFlags, 5))
            {
                foreach (var line in CompanionSpits(owner, c, def, victim, slot, round)) yield return line;
                if (round.Over) yield break;
            }

            // fn0EEF (MONSMOD.BAS:1029): nothing left standing ends the routine
            if (!Monsters.Any(m => m.Alive)) { CompanionsCleared(owner); yield break; }

            // the spell (MONSMOD.BAS:1038-1083): a companion that casts does not also strike
            bool cast = false;
            if (def.SpellFlags > 0 && !round.Cast && !AntiMagicHere && ChooseSpell(def) is Spell spell)
            {
                cast = true;
                round.Cast = true;
                foreach (var line in CompanionCasts(owner, c, def, spell, round)) yield return Picked(line);
                PendingPickSounds = null;
                if (round.Over) yield break;
            }
            if (!cast)
                foreach (var line in CompanionStrikes(owner, c, def, victim, slot, round)) yield return line;
            if (round.Over) yield break;
            // after the blow (MONSMOD.BAS:1327-1335) or the spell (MONSMOD.BAS:1340-1346): no foe
            // left frees the owner and leaves
            if (!Monsters.Any(m => m.Alive)) { CompanionsCleared(owner); yield break; }
        }
    }

    /// <summary>
    /// The three places monCompanionAttack finds the square empty (MONSMOD.BAS:1029-1033,
    /// 1328-1333, 1341-1345) all do the same before leaving: "If Ailments(mc0122) &gt; 0 Then
    /// sub0D1D pv01E4, 1, 0". The companions' owner is freed, silently, with no BETTER.
    /// </summary>
    private void CompanionsCleared(Character owner)
    {
        if (owner.ParalysisLevel > 0) owner.Unparalyse(_item);
    }

    /// <summary>
    /// <c>mc01A0</c>: the companion slots monCompanionAttack walks, 0 to 3. The array holds five
    /// and every loop over it stops at four - the same bound the acid draw reads past.
    /// </summary>
    public const int CompanionSlotsWalked = 4;

    /// <summary>
    /// <c>Foes(CombatOrders(owner).M547A)</c>: the group the owner is fighting, which is where
    /// their companions aim too. The original holds one record per group and, when its front
    /// member dies, sub0D08 (MONSMOD.BAS:1991) loads the next member into the same record; the
    /// port's monsters are individuals, so that is the next living one of the same group. Once the
    /// group is gone the original retargets to the first group left (MONSMOD.BAS:1324-1326).
    /// </summary>
    private CombatMonster? FoeOf(Character owner)
    {
        int t = Party.OrderFor(owner).Target;
        if (t >= 0 && t < Monsters.Count)
        {
            if (Monsters[t].Alive) return Monsters[t];
            if (NextInGroup(Monsters[t]) is CombatMonster next) return next;
        }
        return Monsters.FirstOrDefault(m => m.Alive);
    }

    /// <summary>
    /// <c>sub0D08</c> (MONSMOD.BAS:1991-2008): "If Foes(M547A).GroupCount > 0" the record of the
    /// group the character holds as their target is loaded afresh - two draws (<see
    /// cref="CombatMonster.LoadRecord"/>) - and otherwise nothing is drawn (1995, 2009). Every
    /// caller has just taken one off a count. <paramref name="aimed"/> is any member of the target
    /// group (M547A), and the one loaded is whoever now stands at its front; <paramref name="fallen"/>
    /// is the one taken off. When the two are one group, the next member comes up with its hits.
    /// When they are not - a companion's breath on another group (MONSMOD.BAS:948), a thief running
    /// off (3382) - the target's front is loaded all the same, its wounds thrown away, and the
    /// group that lost one keeps its record for whoever is now at its front (<see cref="KeepRecord"/>).
    /// A list built by hand (no slot, nothing unrolled) keeps the hits it was given.
    /// </summary>
    private void Sub0D08(CombatMonster? aimed, CombatMonster fallen)
    {
        if (aimed != null && NextInGroup(aimed) is CombatMonster front && (front.Unrolled || front.Group >= 0))
            front.LoadRecord(_rng);
        KeepRecord(fallen);
    }

    /// <summary>
    /// A group took one off its count and nothing loaded its record: the one now at its front
    /// stands on the record as it was left - at nought after a kill (the hits are clamped there,
    /// MONSMOD.BAS:455-456, 937-939, MAINMOD2.BAS:1479-1481), as it was after a thief left with the
    /// hits it had.
    /// </summary>
    private void KeepRecord(CombatMonster fallen)
    {
        if (NextInGroup(fallen) is CombatMonster next && next.Unrolled)
            next.TakeRecord(Math.Max(0, fallen.Hits), fallen.MaxHits);
    }

    /// <summary>
    /// <c>Foes(CombatOrders(who).M547A)</c> itself, emptied or not: the group of the order's target,
    /// or with no target held the first group standing, fn0DCD(-1), as the original's M547A is
    /// set before anybody acts (MONSMOD.BAS:297-305, 737). Unlike <see cref="FoeOf"/> it does not
    /// move off a group that has gone; the round does that after the characters' turns
    /// (MULTIMOD.BAS:545-551, <see cref="Retarget"/>).
    /// </summary>
    private CombatMonster? AimedRecord(Character who)
    {
        int t = Party.OrderFor(who).Target;
        return t >= 0 && t < Monsters.Count ? Monsters[t] : Monsters.FirstOrDefault(m => m.Alive);
    }

    /// <summary>
    /// "If Foes(CombatOrders(i).M547A).GroupCount &lt;= False Then CombatOrders(i).M547A = fn0DCD(-1)"
    /// for one character: an order aimed at a group that has gone is aimed at the first group
    /// standing, or at nothing.
    /// </summary>
    private void Retarget(Character who)
    {
        var o = Party.OrderFor(who);
        if (o.Target < 0 || o.Target >= Monsters.Count || NextInGroup(Monsters[o.Target]) != null) return;
        o.Target = Monsters.FindIndex(m => m.Alive);
    }

    /// <summary>The first living member of the run of same monsters this one stands in.</summary>
    private CombatMonster? NextInGroup(CombatMonster m)
    {
        int i = Monsters.IndexOf(m);
        if (i < 0) return null;
        while (i > 0 && CombatMonster.SameGroup(Monsters[i - 1], m)) i--;
        for (; i < Monsters.Count && CombatMonster.SameGroup(Monsters[i], m); i++)
            if (Monsters[i].Alive) return Monsters[i];
        return null;
    }

    /// <summary>fn12DC (MAINMOD2.BAS:2472): "his" or "her".</summary>
    private static string HisHer(Character c) => c.Sex == Sex.Male ? "his" : "her";

    /// <summary>The start of every "accidently" line: the owner's name, a colon, the companion.</summary>
    private static string Accidently(Character owner, Companion c) =>
        $"{owner.Name.TrimEnd()}: {owner.CompanionName(owner.Companions.SlotOf(c))} accidently";

    /// <summary>
    /// One sound of monCompanionAttack's, on a line of its own with nothing to show, heard under
    /// <paramref name="guard"/>'s No Party Sound test - the <c>pv01E4 = Me</c> (the owner) or
    /// <c>l0214 = Me</c> (the victim) its call is wrapped in (see <see cref="CombatSounds"/>).
    /// </summary>
    private static CombatLine CompanionCry(Character guard, string sound) =>
        new("", guard, HitSounds: new[] { sound });

    /// <summary>
    /// A companion's breath, MONSMOD.BAS:875-959.
    ///
    /// It fires on a roll against 50, or 80 when the monsters are much stronger than the companion
    /// and 20 when they are much weaker - the comparison is the lead foe's guild level against the
    /// companion's, a quarter either way. Then <see cref="CompanionMishap"/> decides whether it
    /// lands on your own party instead of the monsters.
    ///
    /// On a mishap the victim takes it, every one of *their* companions takes it too, and the line
    /// names both - "and his companions", from fn12DC. If the victim dies of it the routine leaves
    /// at once (MONSMOD.BAS:900-903), before the line is written, so a fatal accident is announced
    /// only by the death.
    ///
    /// On anything else it sweeps the groups, killing up to four in each before a survivor stops
    /// it, and says nothing at all: there is no message call anywhere in the sweep.
    /// </summary>
    private IEnumerable<CombatLine> CompanionBreathes(Character owner, Companion c, Monster def, CompanionRound round)
    {
        int compLvl = def.GuildLevel;
        var lead = Monsters.FirstOrDefault(m => m.Alive);
        if (lead == null) yield break;

        int chance = 50;
        if (lead.Def.GuildLevel > compLvl + compLvl / 4.0) chance = 80;
        if (lead.Def.GuildLevel < compLvl - compLvl / 4.0) chance = 20;
        if (!(Floor(_rng.NextDouble() * 100) < chance)) yield break;

        bool fire = Bit(def.SpecialAttackFlags, 3);
        int owned = Party.Members.IndexOf(owner);

        int? victim = CompanionMishap.Victim(_rng, CompanionMishap.Kind.Breath, compLvl, Party, owned);
        if (victim is int at && at < Party.Members.Count)
        {
            var hurt = Party.Members[at];
            int dmg = SpecialHits.AfterResistance(
                SpecialHits.Raw(_rng, SpecialHits.Kind.Breath, compLvl, hurt.TopGuildLevel),
                Res(hurt, SpecialHits.ResistedBy(SpecialHits.Kind.Breath, fire)));
            if (dmg > 0)
            {
                yield return Caption(hurt, $"You were accidently Breathed on for {dmg}!", new[] { "FIRE" });
                foreach (var line in TakeDamage(hurt, dmg)) yield return line;
                if (hurt.CurHits < 1)
                {
                    // monCompanionAttack (MONSMOD.BAS:901-1305) calls mdrcharSetDead alone: see SetDead -
                    // as at every companion mishap below
                    SetDead(hurt, Rules.DeathOrdinary);
                    round.Over = true;
                    yield break;
                }
            }

            // every companion of the victim's takes it as well, and can die of it
            round.Suffix = "";
            for (int s = 0; s < CompanionSlotsWalked; s++)
            {
                if (hurt.Companions[s] is not Companion mine || mine.Hits <= 0 || DefOf(mine) is not Monster mdef) continue;
                round.Suffix = $" and {HisHer(hurt)} companions";
                int splash = SpecialHits.AfterResistance(
                    SpecialHits.Raw(_rng, SpecialHits.Kind.Breath, compLvl, mdef.GuildLevel),
                    Res(mdef, SpecialHits.ResistedBy(SpecialHits.Kind.Breath, fire)));
                mine.Hits -= splash;
                // its death cry under the owner's No Party Sound test, then Hits = 0 and
                // mdrcharGetCarry (MONSMOD.BAS:913-918)
                if (mine.Hits < 0)
                {
                    yield return CompanionCry(owner, DeathSound(mdef));
                    mine.Hits = 0;
                    foreach (var line in CompanionFalls(hurt)) yield return line;
                }
            }
            if (round.Kills > 0) round.Breathed = true;
            yield return FoeMessage($"{Accidently(owner, c)} breathed on \"{hurt.Name.TrimEnd()}\"{round.Suffix}!");
            yield break;
        }

        // no mishap: it sweeps every group (MONSMOD.BAS:925-953). In each, the front member takes
        // it; a kill brings the next one up to take it too, a survivor ends that group, and the
        // loop gives up after mc0230 + 1 = 5 kills.
        //
        // A kill calls "sub0D08 pv01E4" (948), which loads the record of the owner's target group,
        // Foes(CombatOrders(owner).M547A) - not the group the breath is on. In the target group
        // that brings the next member up with its hits. In any other group the record is left at
        // nought while its count goes down, so every member after the first to fall dies at the
        // next breath, whatever it does (0 - damage is never above 0), with no experience for it -
        // and each of those kills loads the target group's front afresh, its wounds thrown away.
        var record = AimedRecord(owner);   // M547A, which nothing in the sweep moves
        foreach (var group in AliveGroups())
        {
            round.Kills = 0;
            do
            {
                // a member standing on a record left at nought (AtNought) is the front all the same
                var front = group.FirstOrDefault(m => m.Alive);
                if (front == null) break;
                int dmg = SpecialHits.AfterResistance(
                    SpecialHits.Raw(_rng, SpecialHits.Kind.Breath, compLvl, front.Def.GuildLevel),
                    Res(front.Def, SpecialHits.ResistedBy(SpecialHits.Kind.Breath, fire)));
                if (dmg > 0) yield return new CombatLine("", owner, HitSounds: new[] { "FIRE" });
                Credit(owner, front, dmg, byCompanion: true, DamageKind.Companion);
                front.Hits -= dmg;
                if (front.Hits > 0) break;
                front.Hits = 0;
                QuestKill(front.Def); CompanionKill(owner, front);   // MONSMOD.BAS:945
                round.Kills++;
                Sub0D08(record, front); // 948
            } while (group.Any(m => m.Alive) && round.Kills <= 4);
        }
        if (round.Kills > 0) round.Breathed = true;
    }

    /// <summary>
    /// A companion's acid spit, MONSMOD.BAS:968-1027, at the victim <see cref="CompanionTurn"/>
    /// has already drawn. Where the breath splashes everything the victim owns, the spit lands on
    /// one thing - the character, or the companion in the drawn slot - and an empty slot is a
    /// miss.
    ///
    /// On no mishap it goes at the owner's group, and unlike the breath it does not sweep: the
    /// front of that one group takes it, silently.
    /// </summary>
    private IEnumerable<CombatLine> CompanionSpits(Character owner, Companion c, Monster def, int victim, int slot, CompanionRound round)
    {
        int compLvl = def.GuildLevel;
        if (victim >= 0)
        {
            var hurt = Party.Members[victim];
            if (slot < 0)
            {
                int dmg = SpecialHits.AfterResistance(
                    SpecialHits.Raw(_rng, SpecialHits.Kind.AcidSpit, compLvl, hurt.TopGuildLevel),
                    Res(hurt, SpecialHits.Acid));
                if (dmg <= 0) yield break;
                // the line, then the sound, then the caption, then the damage (MONSMOD.BAS:971-987)
                yield return FoeMessage($"{Accidently(owner, c)} spit on \"{hurt.Name.TrimEnd()}\"{round.Suffix}!");
                yield return Caption(hurt, $"You were accidently Spit on for {dmg}!", new[] { "SPIT" });
                foreach (var line in TakeDamage(hurt, dmg)) yield return line;
                if (hurt.CurHits < 1) { SetDead(hurt, Rules.DeathOrdinary); round.Over = true; }
                yield break;
            }

            // the slot was drawn from the attacker's living companions and is read in the victim's
            // list with no test of its own (MONSMOD.BAS:991): a fallen companion lying there is
            // spat on like a living one, and so is an empty slot
            var poor = hurt.Companions.Left(slot);
            int splash;
            if (hurt.Companions[slot] == null)
            {
                // monGetSpecialHits gets the empty slot's Ref, -1, and reads a negative target as a
                // character: Abs(-1 + 1) = 0, so the level and the acid resistance are those of
                // Chars(0), the party's first member (MONSMOD.BAS:2402-2405, 2434-2438)
                var first = Party.Members[0];
                splash = SpecialHits.AfterResistance(
                    SpecialHits.Raw(_rng, SpecialHits.Kind.AcidSpit, compLvl, first.TopGuildLevel),
                    Res(first, SpecialHits.Acid));
            }
            else
            {
                if (DefOf(poor) is not Monster pdef) yield break;
                splash = SpecialHits.AfterResistance(
                    SpecialHits.Raw(_rng, SpecialHits.Kind.AcidSpit, compLvl, pdef.GuildLevel),
                    Res(pdef, SpecialHits.Acid));
            }
            if (splash <= 0) yield break;
            // the line names the slot by the name its record holds (fn128B, MONSMOD.BAS:993)
            yield return FoeMessage($"{Accidently(owner, c)} spit on {hurt.CompanionName(slot)} belonging to \"{hurt.Name.TrimEnd()}\"!");
            poor.Hits -= splash;
            if (poor.Hits < 0)
            {
                // the death sound, sub0CB7 Companions(l0216).Ref (MONSMOD.BAS:996-999), reads
                // Monsters(-1) for an empty slot. sub0CB7 has no handler of its own and leaves at once
                // with sound effects off (MUSIC.BAS:99-102); otherwise the error reaches
                // monCompanionAttack's handler, which resumes at its End Sub (MONSMOD.BAS:1355-1359):
                // no Hits = 0, no GetCarry, and the rest of this owner's companions do nothing
                bool sounds = !SoundFxOff && (!NoPartySound || owner == Party.Selected);
                if (hurt.Companions[slot] == null && sounds) { round.Over = true; yield break; }
                // an empty slot with sounds off falls through silently; a real one cries, under the
                // owner's test (MONSMOD.BAS:996-998)
                if (hurt.Companions[slot] != null && DefOf(poor) is Monster dead) yield return CompanionCry(owner, DeathSound(dead));
                // Hits = 0 then mdrcharGetCarry (MONSMOD.BAS:1000-1001)
                poor.Hits = 0;
                foreach (var line in CompanionFalls(hurt)) yield return line;
            }
            yield break;
        }

        var front = FoeOf(owner);
        if (front == null) yield break;
        int hit = SpecialHits.AfterResistance(
            SpecialHits.Raw(_rng, SpecialHits.Kind.AcidSpit, compLvl, front.Def.GuildLevel),
            Res(front.Def, SpecialHits.Acid));
        if (hit > 0) yield return new CombatLine("", owner, HitSounds: new[] { "SPIT" });
        Credit(owner, front, hit, byCompanion: true, DamageKind.Companion);
        front.Hits -= hit;
        if (front.Hits < 0) front.Hits = 0;
        if (front.Hits <= 0)
        {
            QuestKill(front.Def); CompanionKill(owner, front);   // MONSMOD.BAS:1022
            Sub0D08(front, front);  // 1023-1024
        }
    }

    /// <summary>
    /// A companion's blow, MONSMOD.BAS:1085-1319, with the stone (bit 8) and electrocution (bit 6)
    /// that follow a blow that lands.
    ///
    /// The target is the one <see cref="CompanionTurn"/> drew: the owner's group, or on a mishap
    /// a party member, or one of that member's companions. What it reads from each:
    ///
    ///     defence   the group's Def | the character's Def | the companion's Def
    ///     level     the monster's   | topGuildLevel      | the companion monster's
    ///     invisible property bit 1  | the Invisible state | property bit 1
    ///
    /// and the blow itself is fn0DEE on the companion's Att, its level and its strength, with no
    /// to-hit roll and no size term. The critical and the backstab are bits 10 and 11 of the
    /// companion's **special attacks** (mc023C, mc023E), worth +4 and +1. An invisible target
    /// halves it unless the companion has property bit 0 (m0240 = 0).
    ///
    /// The character's Def is the stored field, which does not include Protection - that is
    /// added only in the monster's own swing - so a friend's blow ignores it.
    ///
    /// **Two slips, both reproduced.** The stone's constitution test reads the *companion's own*
    /// CON when the target is a monster (MONSMOD.BAS:1226 names the attacker, where the other two
    /// branches name the target). And the follow-ups are gated on the owner's group still
    /// standing (<c>Foes(M547A).Hits &gt; 0</c>, MONSMOD.BAS:1215) even when the blow landed on a friend.
    /// </summary>
    private IEnumerable<CombatLine> CompanionStrikes(Character owner, Companion c, Monster def, int victim, int slot, CompanionRound round)
    {
        var foe = FoeOf(owner);
        if (foe == null) { round.Over = true; yield break; }

        Character? hurt = victim >= 0 ? Party.Members[victim] : null;
        Companion? poor = null;
        Monster? pdef = null;
        if (hurt != null && slot >= 0)
        {
            // MONSMOD.BAS:1102 - an empty or dead slot and the blow is not thrown
            if (hurt.Companions[slot] is not Companion p || p.Hits <= 0) yield break;
            poor = p;
            if ((pdef = DefOf(poor)) == null) yield break;
        }

        int targetDef = hurt == null ? foe.Def.Def : pdef == null ? DefenseOf(hurt) : pdef.Def;
        int targetLevel = hurt == null ? foe.Def.GuildLevel : pdef == null ? hurt.TopGuildLevel : pdef.GuildLevel;
        bool unseen = hurt == null ? MonsterProperty.Has(foe.Def, MonsterProperty.Invisible)
                    : pdef == null ? hurt.InvisibleNow
                    : MonsterProperty.Has(pdef, MonsterProperty.Invisible);

        double damMod = def.DamageMod;
        if (Bit(def.SpecialAttackFlags, 10)
            && _rng.Next(100) + 1 <= Rules.MonsterCritChance(def.GuildLevel, def.LevelFound, targetLevel, backstab: false))
            damMod += 4;
        if (Bit(def.SpecialAttackFlags, 11)
            && _rng.Next(100) + 1 <= Rules.MonsterCritChance(def.GuildLevel, def.LevelFound, targetLevel, backstab: true))
            damMod += 1;
        if (unseen && !MonsterProperty.Has(def, 0)) damMod -= damMod / 2;

        int dmg = Rules.Fn0DEE(_rng, def.Att, targetDef, def.GuildLevel, def.Stats.Length > 0 ? def.Stats[0] : 0, damMod);
        // sub0D35, the companion's attack cry under its owner's No Party Sound test, follows the
        // blow whatever it did - a miss included (l01F2 = l01EE at 1161 only ends the loop) - unless
        // the blow killed a companion (Exit For, 1207) or its owner (Exit Sub) first
        // (MONSMOD.BAS:1212-1214)
        var attackCry = CompanionCry(owner, AttackSound(def));
        if (dmg < 1) { yield return attackCry; yield break; }

        string whose = hurt == null ? "" : $"\"{hurt.Name.TrimEnd()}\"";
        if (hurt == null)
        {
            // and nothing is said: an ordinary blow at the monsters writes no line
            Credit(owner, foe, dmg, byCompanion: true, DamageKind.Companion);
            foe.Hits -= dmg;
            if (foe.Hits < 0) foe.Hits = 0;
            if (foe.Hits <= 0)
            {
                // its death cry under the owner's test, then sub0CF3 (MONSMOD.BAS:1175-1178)
                yield return CompanionCry(owner, DeathSound(foe.Def));
                QuestKill(foe.Def); CompanionKill(owner, foe);
                Sub0D08(foe, foe);   // 1179-1180: the next member is up before the stone or the shock
            }
        }
        else if (poor == null)
        {
            yield return Caption(hurt, $"You were accidently hit for {dmg}!", null);
            foreach (var line in TakeDamage(hurt, dmg)) yield return line;
            yield return FoeMessage($"{Accidently(owner, c)} hit {whose}{round.Suffix}!");
            if (hurt.CurHits < 1) { SetDead(hurt, Rules.DeathOrdinary); round.Over = true; yield break; }
        }
        else
        {
            poor.Hits -= dmg;
            yield return FoeMessage($"{Accidently(owner, c)} hit {hurt.CompanionName(slot)} belonging to {whose}!");
            // its death cry under the owner's test, then Hits = 0, mdrcharGetCarry, Exit For
            // (MONSMOD.BAS:1201-1207) - past the attack cry
            if (poor.Hits < 0)
            {
                yield return CompanionCry(owner, DeathSound(pdef!));
                poor.Hits = 0;
                foreach (var line in CompanionFalls(hurt)) yield return line;
                yield break;
            }
        }
        yield return attackCry;

        // the follow-ups ask whether the owner's group still stands, whoever was hit - and a
        // group whose front just died has the next member standing in its record
        var standing = FoeOf(owner);
        if (standing == null || standing.Hits <= 0 || (hurt == null && !CombatMonster.SameGroup(standing, foe))) yield break;
        // (a member standing on a record at nought fails "Foes(M547A).Hits > 0", MONSMOD.BAS:1215)
        var mon = hurt == null ? standing : null;

        if (Bit(def.SpecialAttackFlags, 8))
        {
            int res = mon != null ? Res(mon.Def, 7) : pdef == null ? Res(hurt!, 7) : Res(pdef, 7);
            if (_rng.Next(100) + 1 > res)
            {
                int con = mon != null ? StatOf(def, 3)                      // the companion's own
                        : pdef == null ? hurt!.EffectiveStat((int)Stat.Con, _item)
                        : StatOf(pdef, 3);
                float chance = (float)(Math.Log(Math.Max(1, con)) / Math.Log(2.5));
                chance = con * chance - def.LevelFound;
                if (chance > 95) chance = 95;
                if (_rng.NextDouble() * 100 > chance)
                {
                    if (mon != null)
                    {
                        Credit(owner, mon, mon.Hits, byCompanion: true, DamageKind.Companion);
                        string caption = owner == Party.Selected ? Caption(owner, $"{owner.CompanionName(owner.Companions.SlotOf(c))} Stoned one!") : "";
                        yield return new CombatLine(caption, owner, Paced: caption != "", HitSounds: new[] { "STONE" }, Channel: LineChannel.CharacterHit);
                        int staleHits = mon.Hits;
                        mon.Hits = 0;
                        QuestKill(mon.Def); CompanionKill(owner, mon);   // MONSMOD.BAS:1247
                        // "GroupCount - 1 : sub0D08" (1248-1249): the record is reloaded with the
                        // next member, which the electrocution below then strikes. With nobody
                        // left sub0D08 reloads nothing (1995, 2009-2017) and the stone never zeroed
                        // the record, so the electrocution still runs - the test that let it in,
                        // "Foes(M547A).Hits > 0", was made before the stone (1215) - and strikes
                        // the stoned monster's record at the hits it had: its own draws
                        // (monGetSpecialHits), SHOCK, experience for what it takes, and at nought
                        // "Electrocuted one!" and sub0CF3's quest test once more (1268-1296).
                        // Its GroupCount then goes to -1, which this port has no field for.
                        Sub0D08(mon, mon);
                        mon = NextInGroup(mon) ?? new CombatMonster { Def = mon.Def, Hits = staleHits };
                    }
                    else if (pdef == null)
                    {
                        // STONE is the owner's to hear (MONSMOD.BAS:1236-1237)
                        yield return FoeMessage($"{Accidently(owner, c)} stoned {whose}{round.Suffix}!", new[] { "STONE" }, owner);
                        hurt!.Stoned = true;
                        SetDead(hurt, Rules.DeathStoned);
                        round.Over = true;
                        yield break;
                    }
                    else
                    {
                        yield return FoeMessage($"{Accidently(owner, c)} stoned {hurt!.CompanionName(slot)} belonging to {whose}!", new[] { "STONE" }, owner);
                        // and its death cry, the owner's test again (MONSMOD.BAS:1256-1258)
                        yield return CompanionCry(owner, DeathSound(pdef!));
                        // Hits = 0, Ref = -1, then mdrcharGetCarry (MONSMOD.BAS:1260-1262): the slot
                        // is emptied before the carry is looked at, so a carried companion stoned
                        // here leaves the carry pointing at a hole
                        poor!.Hits = 0;
                        hurt.Companions.Release(slot);
                        foreach (var line in CompanionFalls(hurt)) yield return line;
                        yield break;
                    }
                }
            }
        }

        if (Bit(def.SpecialAttackFlags, 6))
        {
            int level = mon != null ? mon.Def.GuildLevel : pdef == null ? hurt!.TopGuildLevel : pdef.GuildLevel;
            int zap = SpecialHits.AfterResistance(
                SpecialHits.Raw(_rng, SpecialHits.Kind.Electrocute, def.GuildLevel, level),
                mon != null ? Res(mon.Def, SpecialHits.Electrical)
                : pdef == null ? Res(hurt!, SpecialHits.Electrical) : Res(pdef, SpecialHits.Electrical));
            var shock = zap > 0 ? new[] { "SHOCK" } : null;

            if (mon != null)
            {
                Credit(owner, mon, zap, byCompanion: true, DamageKind.Companion);
                mon.Hits -= zap;
                if (mon.Hits < 0) mon.Hits = 0;
                if (mon.Hits <= 0)
                {
                    QuestKill(mon.Def); CompanionKill(owner, mon);   // MONSMOD.BAS:1294
                    Sub0D08(mon, mon);    // 1295-1296; a stale record (the stone's, above) is no group's
                }
                string caption = mon.Hits <= 0 && owner == Party.Selected
                    ? Caption(owner, $"{owner.CompanionName(owner.Companions.SlotOf(c))} Electrocuted one!") : "";
                yield return new CombatLine(caption, owner, Paced: caption != "", HitSounds: shock, Channel: LineChannel.CharacterHit);
            }
            else if (pdef == null)
            {
                // MONSMOD.BAS:1300 has no "l0214 = Me" around this caption, unlike every other
                // one here: whoever was electrocuted, the character on screen is told it was them
                var watching = Party.Selected ?? hurt!;
                string text = $"You were accidently electrocuted for {zap}!";
                // SHOCK comes first and is the owner's to hear (MONSMOD.BAS:1276-1278), not the
                // watched character's the caption goes to
                if (shock != null) yield return CompanionCry(owner, "SHOCK");
                Say(text);
                yield return new CombatLine(text, watching, Paced: true, Channel: LineChannel.MonsterHit);
                foreach (var line in TakeDamage(hurt!, zap)) yield return line;
                if (hurt!.CurHits < 1) { SetDead(hurt, Rules.DeathOrdinary); round.Over = true; yield break; }
                yield return FoeMessage($"{Accidently(owner, c)} electrocuted {whose}{round.Suffix}!");
            }
            else
            {
                poor!.Hits -= zap;
                yield return FoeMessage($"{Accidently(owner, c)} electrocuted {hurt!.CompanionName(slot)} belonging to {whose}!", shock, owner);
                // its death cry under the owner's test, then Hits = 0 and mdrcharGetCarry
                // (MONSMOD.BAS:1314-1319)
                if (poor.Hits < 0)
                {
                    yield return CompanionCry(owner, DeathSound(pdef!));
                    poor.Hits = 0;
                    foreach (var line in CompanionFalls(hurt)) yield return line;
                }
            }
        }
    }

    private static int StatOf(Monster m, int i) => i < m.Stats.Length ? m.Stats[i] : 0;

    /// <summary>
    /// <c>squareWalls</c> bit 18 (mc0236) where the party stands: the only thing that stops a
    /// companion casting (MONSMOD.BAS:1039). A monster's own casting also minds the extinguisher;
    /// a companion's does not.
    /// </summary>
    public bool AntiMagicHere { get; set; }

    /// <summary>
    /// The two sound options, M5EFC "No Sound FX" and M5F48 "No Party Sound", set by the window.
    /// The engine plays nothing, but one death sound reads a monster record that is not there,
    /// and whether it is played decides what happens next (see <see cref="CompanionSpits"/>).
    /// </summary>
    public bool SoundFxOff { get; set; }
    /// <inheritdoc cref="SoundFxOff"/>
    public bool NoPartySound { get; set; }

    /// <summary>
    /// A companion's spell, MONSMOD.BAS:1040-1083, once <see cref="ChooseSpell"/> (fn0FF5) has
    /// picked one.
    ///
    /// The mishap roll is <see cref="CompanionMishap.Kind.Spell"/>. On a mishap the victim takes
    /// it - "You were accidently damaged for N!" if they are on screen - and so do their companions
    /// in slots 0 to the spell's M5B1D, each rolled on its own. The suffix " and his companions" is
    /// **reset on every pass of that loop** (l01F8 = "" at 1063), so what the line says depends only
    /// on the last slot the loop looked at: a victim whose one companion sits in slot 0 is reported
    /// alone. Then "X: C accidently cast "Spell" on "Y"!".
    ///
    /// Otherwise the companion casts through <c>mdrcharCastSpell</c> on its owner's behalf. With a
    /// companion slot passed, that routine never takes its non-combat branch (856), so **every
    /// spell a companion casts is thrown at the monsters as damage** - a heal included. A heal's
    /// M5B1D is 0, so the loop reaches no monster and the caption is "Comp. #1 failed in casting
    /// "Minor Heal"!". Reproduced; see <see cref="CastDamage"/>.
    /// </summary>
    private IEnumerable<CombatLine> CompanionCasts(Character owner, Companion c, Monster def, Spell spell, CompanionRound round)
    {
        int owned = Party.Members.IndexOf(owner);
        int slotOf = owner.Companions.SlotOf(c);
        if (CompanionMishap.Victim(_rng, CompanionMishap.Kind.Spell, def.GuildLevel, Party, owned) is int v
            && v < Party.Members.Count)
        {
            var hurt = Party.Members[v];
            var facing = FoeOf(owner)?.Def;
            int dmg = CompanionSpellOnParty(spell, def, facing, hurt, null);
            if (dmg > 0)
            {
                if (hurt == Party.Selected) yield return Caption(hurt, $"You were accidently damaged for {dmg}!", null);
                foreach (var line in TakeDamage(hurt, dmg)) yield return line;
                if (hurt.CurHits < 1) { SetDead(hurt, Rules.DeathOrdinary); round.Over = true; yield break; }
            }
            for (int i = 0; i <= spell.AffectGroup; i++)
            {
                if (i > CompanionSlotsWalked - 1) break;
                round.Suffix = "";
                if (hurt.Companions[i] is not Companion mine || mine.Hits <= 0 || DefOf(mine) is not Monster mdef) continue;
                round.Suffix = $" and {HisHer(hurt)} companions";
                mine.Hits -= CompanionSpellOnParty(spell, def, facing, hurt, mine);
                // its death cry - under the *victim's* No Party Sound test here (l0214 = Me), where
                // every other mishap asks the owner's (pv01E4 = Me) - then Hits = 0 and
                // mdrcharGetCarry (MONSMOD.BAS:1068-1073)
                if (mine.Hits < 1)
                {
                    yield return CompanionCry(hurt, DeathSound(mdef));
                    mine.Hits = 0;
                    foreach (var line in CompanionFalls(hurt)) yield return line;
                }
            }
            yield return FoeMessage($"{Accidently(owner, c)} cast \"{spell.Name.TrimEnd()}\" on \"{hurt.Name.TrimEnd()}\"{round.Suffix}!");
            yield break;
        }

        // no mishap: "mdrcharCastSpell(pv01E4, l0206, False, l0204)" (MONSMOD.BAS:1080), the damage
        // branch on the owner's behalf, aimed where the owner aims. With a companion slot there is
        // no mdrcharCheckCast (829-835), but the rest of the head runs as for the owner: the draw
        // l01D0 (840), then a charge off the item in the owner's M5766 - the owner's item order, or
        // the slot an own spell left there - and with none left the run-out line and sub102F
        // owner, -1, and the companion's cast is over (841-853).
        CastSpellDraw();
        if (ItemOrders.TryGetValue(owner, out var ownersItem)
            && ItemCharge(owner, Party.OrderFor(owner), ownersItem) is CombatLine runOut)
        {
            yield return runOut;
            yield break;
        }
        string refused = CastDamage(owner, spell, null, 0, slotOf, def);
        if (LastCaptions.Count == 0 && refused != "") { yield return Message(refused); yield break; }
        foreach (var line in CaptionLines(owner)) yield return line;
    }

    /// <summary>
    /// <c>monGetSpellHits</c> with a companion casting at its own party (MONSMOD.BAS:2464-2582,
    /// target -2 for a character, -(3 + slot) for one of their companions): the companion's level
    /// over 6, the victim's resistance, a magic-immune victim companion (bit 2) spared, and the
    /// class-only spells judged against the group the owner is fighting.
    /// </summary>
    private int CompanionSpellOnParty(Spell s, Monster caster, Monster? facing, Character hurt, Companion? victimComp)
    {
        int lvl = (int)Math.Floor(caster.GuildLevel / 6.0);
        if (lvl < 1) lvl = 1;
        if (lvl > 255) lvl = 255;
        var vdef = victimComp == null ? null : DefOf(victimComp);
        int resist = s.ResistIndex < 0 ? 0
                   : vdef == null ? Res(hurt, s.ResistIndex)
                   : Res(vdef, s.ResistIndex);
        int dmg = Rules.SpellDamage(_rng, s.Damage2, s.SpecialEffect + 1, lvl, resist);
        if (vdef != null && MonsterProperty.Has(vdef, 2)) dmg = 0;
        if (s.Effect is >= 75 and <= 99
            && (facing == null || facing.Type >= Mdata1.MonsterClasses.Count || Mdata1.MonsterClasses[facing.Type].Index != s.Effect - 75))
            dmg = 0;
        return dmg;
    }

    /// <summary>Cast a known spell: cure, heal, charm, or damage one monster / the whole group.</summary>
    /// <summary>
    /// Cast a spell. <paramref name="itemLevel"/> is set when the casting comes from an item:
    /// monGetSpellHits takes the level straight from the item rather than from the caster, so a
    /// wand is as strong in a novice hand as in a master one.
    /// </summary>
    public string CastSpell(Character actor, Spell s, int? targetIndex = null, bool free = false, int itemLevel = 0)
    {
        LastCaptions.Clear();
        LastSaidSounds = null;
        LastCastTookTurn = true;
        // a charm with no free slot is refused before anything is spent (MAINMOD2.BAS:1410-1416)
        if (Spellbook.IsCharm(s) && actor.Companions.FreeSlots == 0)
        {
            LastCastTookTurn = false;
            Say($"\"{s.Name.TrimEnd()}\" - No room for more Companions!");
            return Log[^1];
        }
        int cost = free ? 0 : Spellbook.Cost(s, actor);
        if (actor.CurSpellPoints < cost)
        {
            // mdrcharCheckCast's refusal (MAINMOD2.BAS:502-510); "X lacks the spell points for Y
            // (needs N)." was this port's
            LastCastTookTurn = false;
            Say(actor == Party.Selected ? $"Not enough spell points to cast \"{s.Name.TrimEnd()}\"!"
                                        : $"{actor.Name.TrimEnd()} doesn't have enough spell points to cast \"{s.Name.TrimEnd()}\"!");
            return Log[^1];
        }
        // mdrcharCheckCast's next refusal (MAINMOD2.BAS:512-519), before anything is spent: see below
        if (NonCombatInFight(actor, s) is string refused)
        {
            LastCastTookTurn = false;
            Say(refused);
            return Log[^1];
        }
        actor.CurSpellPoints -= cost;
        // monGetSpellHits (MONSMOD.BAS:2485-2492): an item's own level when above nought, the current guild's
        // spell level when below it, and at nought the best guild's (casterSpellLevel)
        int sl = itemLevel > 0 ? itemLevel : itemLevel < 0 ? Rules.SpellLevel(actor.Guild.Level)
               : free ? 1 : Spellbook.SpellLevelFor(actor, s);

        // Cure Paralysis is one of fn149E's two "either" spells, and with monsters present an
        // either spell takes the combat branch, not the cure (MAINMOD2.BAS:856): it goes at the
        // monsters with its nought damage and fails. The port had cured with it mid-fight.
        //
        // A heal or a cure never gets here: see NonCombatInFight. The port had healed the most hurt
        // member or cured whoever needed it, a choice of its own.
        return CastDamage(actor, s, targetIndex, sl);
    }

    /// <summary>gv0972$ (MODULE1.BAS:381-397): the verb a damage spell's caption uses, by M5B12.</summary>
    public static readonly string[] SpellVerbs =
    {
        "Melted", "Fried", "Froze", "Scared", "Drained", "Electrocuted", "Dispelled", "Banished",
        "Dazzled", "Immolated", "Shocked", "Slept", "Choked", "Killed", "Rotted", "Stoned", "Teleported",
    };

    /// <summary>gv0846$ (MODULE1.BAS:429-440): the count, in words, capped at "A Lot".</summary>
    public static readonly string[] CountWords =
    {
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "A Lot",
    };

    /// <summary>
    /// The captions the last damage spell wrote to lblCharacterHit, one per group it reached, each
    /// already ending in "!". Only the watched character's are ever on screen.
    /// </summary>
    public List<string> LastCaptions { get; } = new();

    /// <summary>
    /// For each of <see cref="LastCaptions"/>, a member of the group it was written for: where
    /// the loop put the caster's target as it reached that group - "CombatOrders(pv01A6).M547A =
    /// l0286", and for the character in command lblCurrentGroup moved beside that group's row
    /// (MAINMOD2.BAS:1456-1460) - so the mark walks along with the spell while each caption shows.
    /// </summary>
    public List<int> LastCaptionAims { get; } = new();

    /// <summary>
    /// Where the target is put back once the loop is done (MAINMOD2.BAS:1582-1586): the group
    /// aimed at, or fn0DCD(-1) - the first group standing - if that group is gone; -1 for none.
    /// monShowCursor then redraws the mark there (1590).
    /// </summary>
    public int LastCastAim { get; private set; } = -1;

    /// <summary>
    /// The last damage spell's captions as lines for lblCharacterHit, each carrying the group it
    /// was written for, and after them an empty line that clears the caption (1571) and carries
    /// the target the loop left behind, so the window draws the mark back where the original's
    /// monShowCursor does (1590).
    /// </summary>
    private IEnumerable<CombatLine> CaptionLines(Character caster)
    {
        var caps = LastCaptions.ToList();
        var aims = LastCaptionAims.ToList();
        int back = LastCastAim;
        bool paced = caster == Party.Selected;
        for (int i = 0; i < caps.Count; i++)
            yield return new CombatLine(caps[i], caster, Paced: paced, Channel: LineChannel.CharacterHit, CastSounds: LastCaptionSounds,
                                        AimAt: i < aims.Count ? aims[i] : null);
        if (caps.Count > 0)
            yield return new CombatLine("", caster, Channel: LineChannel.CharacterHit, AimAt: back);
    }

    /// <summary>
    /// sub1005's sounds (<see cref="SpellSounds"/>) for the last damage spell, played once for each
    /// of <see cref="LastCaptions"/>: MAINMOD2.BAS:1452-1455 calls it for every group the spell
    /// reaches, before the damage and the caption.
    /// </summary>
    public IReadOnlyList<string>? LastCaptionSounds { get; private set; }

    /// <summary>sub1005's sounds after a non-combat cast (MAINMOD2.BAS:1397-1403), for the line it says.</summary>
    public IReadOnlyList<string>? LastSaidSounds { get; private set; }

    /// <summary>
    /// The damage branch of <c>mdrcharCastSpell</c> (MAINMOD2.BAS:1403-1601), for a character or -
    /// through the same routine, with <paramref name="compSlot"/> set - for one of their companions.
    ///
    /// It starts at the caster's group and works through <c>M5B28</c> groups (Damage1 here),
    /// wrapping round the four and spending a step on every empty one it passes. In each it takes
    /// up to <c>M5B1D</c> (AffectGroup) monsters **from the front, one at a time, and stops at the
    /// first that survives** - so a spell that fails to kill the front monster never reaches the
    /// second. Each victim is rolled separately (monGetSpellHits), and an all-or-nothing spell
    /// (effect 100) that would not kill does nothing and ends the group.
    ///
    /// Then one caption per group, "!" added, for lblCharacterHit:
    ///
    ///     "You Fried Two"                               kills, verb from gv0972$(M5B12)
    ///     "You Fried Two and damaged one for 12"        and the survivor it stopped at
    ///     "You damaged one for 12"                      no kills
    ///     "Failed"                                      nothing - and the order becomes Fight
    ///     "Comp. #1 failed in casting "Firebolt""       the same for a companion
    ///
    /// "You " is written only when the caster is the character in command; for anybody else the
    /// prefix is empty and the caption is never shown. The port had "X casts Firebolt: 12 to the
    /// Orc, killing it." in the list, which is nobody's wording.
    /// </summary>
    private string CastDamage(Character actor, Spell s, int? targetIndex, int level, int compSlot = -1, Monster? compDef = null)
    {
        LastCaptions.Clear();
        LastCaptionAims.Clear();
        LastCastAim = -1;
        LastCaptionSounds = SpellSounds.For(s);
        // MAINMOD2.BAS:1410-1416: a charm with nowhere to put what it catches is not cast at all -
        // and the slots counted are the owner's, even when a companion is the caster
        if (s.Category == Spellbook.Charm && actor.Companions.FreeSlots == 0)
            return $"\"{s.Name.TrimEnd()}\" - No room for more Companions!";
        const int Slots = 4;   // mc02A6
        // the groups as the original holds them: runs of one kind, in order, the dead included, each
        // at its Foes() slot - a slot emptied before the room was read back is still one the loop
        // steps over (BySlot; the port had closed the gap, so a group spell reached one slot further)
        var groups = CombatMonster.BySlot(Monsters);
        int Alive(int g) => g < groups.Count ? groups[g].Count(m => m.Alive) : 0;

        // sub1019 (MONSMOD.BAS:297-305) runs before the character acts: a target of -1, or one on
        // a group with nobody left, becomes fn0DCD(-1) - the first group standing - so the loop
        // below never opens on an emptied slot. The port had opened on group 0 whatever the target,
        // and the skip over a dead group 0 spent a step (l028E) the spell then lacked: a two-group
        // Firebolt reached one group once the first group had fallen. Corrected 2026-10-01.
        int aim = targetIndex ?? Party.OrderFor(actor).Target;
        int g0 = aim >= 0 && aim < Monsters.Count ? groups.FindIndex(gr => gr.Contains(Monsters[aim])) : -1;
        if (g0 < 0 || Alive(g0) < 1) g0 = groups.FindIndex(gr => gr.Any(m => m.Alive));
        if (g0 < 0) g0 = 0;

        bool me = actor == Party.Selected;
        string prefix = !me ? "" : compSlot < 0 ? "You " : $"Comp. #{compSlot + 1} ";
        int g = g0, done = 0, stepped = 0;   // l0286, l0288, l028E
        do
        {
            if (g > Slots - 1) g = 0;
            if (Alive(g) < 1)
            {
                do
                {
                    stepped++;
                    g++;
                    if (g > Slots - 1) g = 0;
                    if (stepped >= Slots) break;
                    if (Alive(g) > 0) break;
                } while (stepped < Slots);
                if (stepped >= Slots) break;
            }

            var group = groups[g];
            var gdef = group[0].Def;
            // "CombatOrders(pv01A6).M547A = l0286" (1456): the caster's target is this group for as
            // long as the spell works on it - the front member standing, which the window reads
            // back as the group - and the mark goes with it (1457-1460, through CaptionLines)
            int aimNow = Monsters.IndexOf(group.First(m => m.Alive));
            Party.OrderFor(actor).Target = aimNow;
            int reach = Math.Min(s.AffectGroup, Alive(g));
            int kills = 0, survivor = 0, charms = 0;
            bool charm = s.Category == Spellbook.Charm;   // SpellDefs(s).Kind = mc029E (7)
            for (int i = 1; i <= reach; i++)
            {
                var front = group.FirstOrDefault(m => m.Alive);
                if (front == null) break;
                int dmg = SpellHitsOn(s, level, gdef, compDef);
                if (s.Effect == 100 && front.Hits - dmg > 0) { dmg = 0; break; }
                int before = front.Hits;                   // l0296
                // a charm takes only what it would have killed (MAINMOD2.BAS:1473-1477)
                if (charm && before - dmg > 0) dmg = 0;
                Credit(actor, front, dmg, byCompanion: compSlot >= 0, DamageKind.Magical, removes: !charm);
                front.Hits -= dmg;
                if (front.Hits < 0) front.Hits = 0;
                if (front.Hits > 0) { survivor = dmg; break; }
                // 1506-1508, before the charm is settled: a character's kill counts even when the
                // creature is charmed, or charmed with nowhere to go
                if (compSlot < 0) KilledBy(actor, front.Def, DamageKind.Magical);
                else CompanionKill(actor, front);   // nothing of the game's own (no Kills, no sub0CF3), told to the mods only
                if (charm)
                {
                    // l029A, and fn1163 for a character (1511-1517): the creature joins with the hits it
                    // had, bound at the spell's level. With no slot free it is gone all the same and
                    // the count is taken back. For a companion caster the joining is skipped - the
                    // creature simply vanishes and the caption still says it was charmed.
                    charms++;
                    if (compSlot < 0)
                    {
                        // GroupCount - 1 has been taken and the Exit For (MAINMOD2.BAS:1514-1516) skips the load: the next one
                        // up stands on the record at nought
                        if (actor.Companions.FreeSlots == 0) { charms--; KeepRecord(front); break; }
                        // fn1163 writes no name (MAINMOD2.BAS:388-400): the slot keeps what it was called
                        actor.Companions.Add(Charmed(actor, front, before, s), keepSlotName: true);
                    }
                    front.Gone = true;
                    Sub0D08(front, front);   // 1520-1521: M547A is this group (1456), and the next steps up
                    continue;
                }
                kills++;
                if (compSlot < 0 && front.Identified
                    && (!MonsterProperty.Has(front.Def, MonsterProperty.Invisible) || actor.SeeingInvisible))
                    RecordableKills.Add((actor, front.Def));
                Sub0D08(front, front);   // 1520-1521
            }

            string caption = "";
            if (charms > 0) caption = "You charmed " + CountWords[Math.Min(charms, 11)];   // 1527-1528, no other prefix
            else if (kills > 0) caption = prefix + SpellVerbs[Math.Clamp((int)s.AffectMonster, 0, SpellVerbs.Length - 1)]
                                     + " " + CountWords[Math.Min(kills, 11)];
            if (charms > 0) { }
            else if (survivor > 0)
                caption = kills > 0 ? caption + " and damaged one for " + survivor
                                    : prefix + "damaged one for " + survivor;
            else if (kills == 0)
            {
                if (compSlot < 0)
                {
                    // sub102F caster, 1: a character whose spell did nothing is put on Fight
                    var o = Party.OrderFor(actor);
                    o.Kind = OrderKind.Fight; o.SpellId = 0;
                    ItemOrders.Remove(actor);   // and M5766 = -1 (MULTIMOD.BAS:476-479)
                    caption = "Failed";
                }
                else caption = prefix + $"failed in casting \"{s.Name.TrimEnd()}\"";
            }
            LastCaptions.Add(caption + "!");
            LastCaptionAims.Add(aimNow);

            if (!Monsters.Any(m => m.Alive)) break;
            g++; done++; stepped++;
        } while (done < s.Damage1 && stepped < s.Damage1);

        // 1582-1586: "If Foes(l0298).GroupCount < 1 Then M547A = fn0DCD(-1) Else M547A = l0298" -
        // back to the group aimed at, or to the first still standing when that one is gone
        LastCastAim = Alive(g0) > 0 ? Monsters.IndexOf(groups[g0].First(m => m.Alive)) : Monsters.FindIndex(m => m.Alive);
        Party.OrderFor(actor).Target = LastCastAim;
        return string.Join(" ", LastCaptions);
    }

    /// <summary>
    /// <c>monGetSpellHits</c> (MONSMOD.BAS:2464-2582) for a spell cast at a group of monsters -
    /// by a character (<paramref name="compDef"/> null) or by a companion.
    ///
    /// The level is the character's spell level, or the companion's guild level over **6**. Then
    /// the resistance of the group's monster, and three ways to nothing: a class-only spell (75-99)
    /// cast at the wrong class; for a character, a magic-immune monster (property bit 2) or a
    /// kind-7 spell at a monster with bit 3. **For a companion the immunity test reads the
    /// companion itself** - "testBit(mc043C, Monsters(Chars(caster).Companions(slot).Ref).M55BE)"
    /// at 2556 - so a magic-immune companion's spells do nothing, and a magic-immune monster takes
    /// a companion's spells in full. Reproduced.
    /// </summary>
    private int SpellHitsOn(Spell s, int level, Monster target, Monster? compDef)
    {
        int lvl = compDef != null ? (int)Math.Floor(compDef.GuildLevel / 6.0) : level;
        if (lvl < 1) lvl = 1;
        if (lvl > 255) lvl = 255;
        int resist = s.ResistIndex > -1 && s.ResistIndex < target.Resistances.Length ? target.Resistances[s.ResistIndex] : 0;
        int dmg = Rules.SpellDamage(_rng, s.Damage2, s.SpecialEffect + 1, lvl, resist);
        if (compDef == null)
        {
            if (s.Category == 7 && MonsterProperty.Has(target, 3)) dmg = 0;
            if (MonsterProperty.Has(target, 2)) dmg = 0;
        }
        else if (MonsterProperty.Has(compDef, 2)) dmg = 0;
        if (s.Effect is >= 75 and <= 99
            && (target.Type >= Mdata1.MonsterClasses.Count || Mdata1.MonsterClasses[target.Type].Index != s.Effect - 75))
            dmg = 0;
        return dmg;
    }

    public string CastSpell(Spell s, int? targetIndex = null, bool free = false, int itemLevel = 0) => CastSpell(Actor, s, targetIndex, free, itemLevel);

    private string Who(Character c) => Party.Size > 1 ? c.Name : "You";

    /// <summary>
    /// What to call a monster in a line of combat text. Everything the player reads goes through
    /// here, because the original puts every one of those strings through <c>fn0E23</c> - so a
    /// monster you have not identified is named by its class, and one you cannot see at all is
    /// just "Unseen Monster".
    /// </summary>
    private string Named(CombatMonster m, int count = 1) =>
        EncounterGenerator.NameFor(m.Seen, Party.Selected, count, m.Identified ? 0 : 1);

    /// <summary>
    /// mdrcharCheckCast's Non-Combat refusal (MAINMOD2.BAS:512-519): "If fn0EEF() > 0 And
    /// Foes(0).Engaged = 1 And fn149E(spell) = gc1EE2". This engine is the engaged fight, so a
    /// heal, a cure or any spell fn149E calls non-combat (2908-2921: nought damage or the Heal
    /// class, Cure Paralysis and Teleport excepted) is refused - not asked "Cast on Who?", which
    /// is the non-combat branch (917-941) and is reached only with no engaged fight, the dungeon's
    /// CastSpellHere. mdrcharCastSpell makes the check first thing (831), before the item's charge
    /// (841), and is reached from the round (MONSMOD.BAS:313-315) for a spell order and an item's.
    /// The wording goes by gv1A82, the character the windows show (447-517). Null to go on.
    /// </summary>
    private string? NonCombatInFight(Character c, Spell s) =>
        CastGate.UseOf(s) != SpellUse.NonCombat ? null
        : CastGate.Refuse(c, s, 0, c == Party.Selected, monstersPresent: true, engaged: true, antiMagic: false)?.Text;

    /// <summary>
    /// mdrcharCheckCast as the round's mdrcharCastSpell makes it on every cast (MAINMOD2.BAS:831,
    /// from MONSMOD.BAS:313-315), in its own order: the caster does not know the spell (437-455),
    /// the spell level is too low (457-481), a stat is too low (482-497) - those three skipped
    /// for an item with a level of its own, as are the points (502-510) - then Non-Combat in an
    /// engaged fight (512-519) and an anti-magic or extinguisher square (520-526). The wording
    /// goes by gv1A82, the character the windows show. A refusal comes before the draw at 840 and
    /// the item's charge, and mdrcharCastSpell then runs "sub102F pv01A6, mc01CE" (832, mc01CE =
    /// 1): M56C4 = -1, M5766 = -1, Att = 1 - the member fights, keeping the group aimed at
    /// (MULTIMOD.BAS:480-483). The anti-magic refusal's own sub102F pv, -1 (525) is overwritten by
    /// that; its mdrmazPerceiveSquare is the window's (<see cref="AntiMagicRefused"/>).
    /// </summary>
    private CombatLine? RoundCheckCast(Character c, CombatOrder order, Spell s, int itemLevel)
    {
        bool isYou = c == Party.Selected;
        int cost = itemLevel != 0 ? 0 : Spellbook.Cost(s, c);
        var refused = CastGate.Unfit(c, s, itemLevel, isYou, _item)
                   ?? CastGate.Refuse(c, s, cost, isYou, monstersPresent: true, engaged: true, antiMagic: NoSpellsHere);
        if (refused == null) return null;
        order.Kind = OrderKind.Fight; order.SpellId = 0;
        ItemOrders.Remove(c);
        if (refused.Kind == RefusalKind.AntiMagic) AntiMagicRefused?.Invoke(c);
        return Message(refused.Text);
    }

    /// <summary>
    /// The anti-magic refusal's "mdrmazPerceiveSquare mc013A, True" and the automap's DRAWSQ
    /// (MAINMOD2.BAS:521-523), which the window does for the caster.
    /// </summary>
    public Action<Character>? AntiMagicRefused { get; set; }

    /// <summary>
    /// <c>fn1163</c> (MAINMOD2.BAS:383-405): the companion a charm makes. Its hits are what the
    /// creature had when the spell struck, its maximum the hits it was rolled with, its binding the
    /// spell's own level, and how well you know it is <c>fn1272</c>. The port's CastCharm, with its
    /// own success roll from the charm's name, the caster's charisma and the monster's
    /// resistances, is gone: the original charms whatever the spell's damage would have killed.
    /// fn1163 also copies the foe's alignment, <c>p00D6.Alignment</c> (MAINMOD2.BAS:392), which is
    /// <see cref="CombatMonster.CompanionAlignment"/>.
    /// </summary>
    private Companion Charmed(Character caster, CombatMonster m, int hitsBefore, Spell s) => new()
    {
        Ref = m.Def.Ref,                    // p00D6.Ref (MAINMOD2.BAS:388)
        MonsterId = m.Def.Id,               // M57D4 = Monsters(p00D6.Ref).Id (389)
        Name = "",
        Hits = hitsBefore,
        MaxHits = m.MaxHits > 0 ? m.MaxHits : m.Def.Hits,
        Alignment = m.CompanionAlignment,   // fn1163: p00D6.Alignment (MAINMOD2.BAS:392)
        BindingLevel = s.Level,
        IdLevel = CharmIdLevel(caster, m.Def, _item),
    };

    /// <summary>
    /// <c>fn1272</c> (MAINMOD2.BAS:2086-2108) as fn1163 calls it: the creature's difficulty,
    /// <c>(30 + floor) * Log((guild level + 5) / 10 + 1)</c> held as an Integer, taken from
    /// <c>mdrcharGetIDSkill</c>; nought or better is fully known (0), then 1, then 2 from five
    /// short, and 3 from fifteen short. <c>m0088</c>, the "known" threshold, is never assigned, so
    /// it is 0.
    /// </summary>
    public static int CharmIdLevel(Character c, Monster def, Func<short, Item?> item) => IdLevelRoll(c, def, item, true);

    /// <summary>
    /// <c>fn1272</c> (MAINMOD2.BAS:2086-2108) with its last argument: fn1163 and the companion's
    /// Info pass True (MAINMOD2.BAS:400, FRMCHAR3.BAS:752); sub06FE passes False
    /// (MONSMOD.BAS:3965), which makes the creature half as hard again - "If p036E = False Then
    /// l0370 = (l0370 * 1.5#)" (2093), stored back into the Integer.
    /// </summary>
    public static int IdLevelRoll(Character c, Monster def, Func<short, Item?>? item, bool p036E)
    {
        int difficulty = (int)Math.Round((30 + def.LevelFound) * Math.Log((def.GuildLevel + 5) / 10.0 + 1), MidpointRounding.ToEven);
        if (!p036E) difficulty = (int)Math.Round(difficulty * 1.5, MidpointRounding.ToEven);
        double skill = item == null
            ? Rules.IdentifySkill(c.TopGuildLevel, c.Stats[(int)Stat.Int], c.Stats[(int)Stat.Wis])
            : Rules.IdentifySkill(c.TopGuildLevel, c.EffectiveStat((int)Stat.Int, item), c.EffectiveStat((int)Stat.Wis, item));
        int gap = (int)Math.Round(skill - difficulty, MidpointRounding.ToEven);
        return gap >= 0 ? 0 : gap <= -15 ? 3 : gap <= -5 ? 2 : 1;
    }

    /// <summary>The whole party tries to break off; WIS and DEX of the one leading the retreat decide it.</summary>
    /// <summary>
    /// There is no flee command. Grepping the whole of the decompiled source for "flee", "run
    /// away" or "escape" finds nothing, the help lists only four things you can do in a fight -
    /// "You can fight, cast a spell, open a treasure container (if any), or just run" - and the
    /// FAQ says what "run" means: "During Combat, you can use the cursor keys to move around just
    /// as you can when there are no monsters in a room - you can therefore use this ability to
    /// 'run away'." Walking off the square is the whole mechanic, and the port already has it.
    /// This stays only so the headless simulations can still walk out of a fight.
    /// </summary>
    public void WalkAway() => Fled = true;


    private void Say(string s) => Log.Add(s);

}

/// <summary>
/// One of the four <c>Foes()</c> records (T4E8B, MAIN.TXT:87-100) as fn0D4D fills it: a group is a
/// single record - one monster, one count, and one set of hits for whichever member is standing
/// at the front.
/// </summary>
public sealed class FoeSlot
{
    /// <summary>
    /// <c>Ref</c>: the table index of the monster. A record sub0C79 has cleared is all zeroes, so
    /// 0; sub0C53 sets -1 in slot 0 when the room is emptied (MONSMOD.BAS:775).
    /// </summary>
    public int Ref { get; set; }
    /// <summary><c>KnownAs</c>: the table index the party takes it for.</summary>
    public int KnownAs { get; set; }
    public int GroupCount { get; set; }
    public int Att { get; set; }
    public int Def { get; set; }
    /// <summary>The front member's hits and the hits it was rolled with.</summary>
    public int Hits { get; set; }
    public int MaxHits { get; set; }
    public int Alignment { get; set; }
    public int IdLevel { get; set; }
    /// <summary><c>Engaged</c>: 1 hostile, 0 not. Only slot 0 is ever set, or read.</summary>
    public int Engaged { get; set; }
    /// <summary><c>M4EF9</c>: how many offer to join. Only slot 0's is ever read.</summary>
    public int JoinOffer { get; set; }
    /// <summary><c>M4EE6</c>: Timer when the group was made - slot 0's is the room's stamp.</summary>
    public double Stamp { get; set; }

    public FoeSlot Copy() => (FoeSlot)MemberwiseClone();
}

/// <summary>
/// What the level's file holds for one room: its four <c>Foes()</c> records and its <c>Fight</c>
/// record, as fn0F88 writes them (MONSMOD.BAS:3921-3924) and fn0E38 reads them back
/// (MONSMOD.BAS:2379-2382) - whole, every field.
/// </summary>
public sealed class RoomRecord
{
    public FoeSlot[] Foes { get; set; } = { new(), new(), new(), new() };
    public int TreasureClass { get; set; }
    /// <summary><c>Fight.M4F24</c>, the lead's table index; 0 in a cleared record.</summary>
    public int Lead { get; set; }
    public decimal Gold { get; set; }
    public int ChestTrap { get; set; }
    public int ChestLock { get; set; }

    public RoomRecord Copy() => new()
    {
        Foes = Foes.Select(f => f.Copy()).ToArray(),
        TreasureClass = TreasureClass, Lead = Lead, Gold = Gold, ChestTrap = ChestTrap, ChestLock = ChestLock,
    };

    /// <summary>Whether any group still stands: fn0DCD(-1) &gt; -1.</summary>
    public bool AnyStanding => Foes.Any(f => f.GroupCount > 0);
}

/// <summary>
/// What fn0D4D leaves behind: the four <c>Foes()</c> records and the <c>Fight</c> record
/// (T4F0B, MAIN.TXT:102-108), flattened into one <see cref="CombatMonster"/> per member for the
/// rest of the port. A <see cref="CombatEngine"/> given one of these takes its chest, trap, lock
/// and gold as they were rolled here, in the original's order, instead of rolling its own.
/// </summary>
public sealed class Encounter : List<CombatMonster>
{
    public FoeSlot[] Foes { get; } = { new(), new(), new(), new() };
    /// <summary><c>Fight.TreasureClass</c>: 0, or 2 or 3 - the roll never makes 1.</summary>
    public int TreasureClass { get; set; }
    /// <summary><c>Fight.ChestTrap</c>: the trap's bit, 0 untrapped.</summary>
    public int ChestTrap { get; set; }
    /// <summary><c>Fight.ChestLock</c>: 1 locked.</summary>
    public int ChestLock { get; set; }
    /// <summary><c>Fight.Gold</c>, a Currency: before monGetGold applies the chest class.</summary>
    public decimal Gold { get; set; }
    /// <summary><c>Fight.M4F24</c>, the lead's table index.</summary>
    public int Lead { get; set; } = -1;
    /// <summary>The lead monster itself, for whoever needs it after the group has gone.</summary>
    public Monster? LeadMonster { get; set; }
    /// <summary>The room (level, area id) these are the records of, or (-1, -1) for none.</summary>
    public (int Level, int Area) Room { get; set; } = (-1, -1);
    /// <summary>fn0D4D returned False ("TryNumber &gt; 300!"); sub0A91 then says "Error generating monsters!".</summary>
    public bool Failed { get; set; }
    /// <summary>
    /// The groups were made on this visit and fn0D4D succeeded: sub0A91 files each in the Library
    /// as seen (MONSMOD.BAS:717-721) before it files them again as it does on every visit (730-734).
    /// </summary>
    public bool Fresh { get; set; }
    /// <summary>The lines fn0D4D put in the Messages window (sub085B), in order.</summary>
    public List<string> Messages { get; } = new();
    /// <summary>How many times the lead monster 277 was drawn by the class pick (each is <c>sub09DE 68</c>).</summary>
    public int AsmodeusCues { get; set; }
    /// <summary>How many times the whole pick went back to its start (L1EB02). Diagnostic only.</summary>
    public int Restarts { get; set; }

    /// <summary>The records as they stand, for fn0F88.</summary>
    public RoomRecord ToRecord() => new RoomRecord
    {
        Foes = Foes.Select(f => f.Copy()).ToArray(),
        TreasureClass = TreasureClass, Lead = Lead, Gold = Gold, ChestTrap = ChestTrap, ChestLock = ChestLock,
    }.Copy();

    /// <summary>fn0E38's side: the records put back into Foes() and Fight.</summary>
    public void Take(RoomRecord r, IReadOnlyList<Monster> all)
    {
        for (int g = 0; g < Foes.Length; g++)
        {
            var s = g < r.Foes.Length ? r.Foes[g] : new FoeSlot();
            var d = Foes[g];
            d.Ref = s.Ref; d.KnownAs = s.KnownAs; d.GroupCount = s.GroupCount; d.Att = s.Att; d.Def = s.Def;
            d.Hits = s.Hits; d.MaxHits = s.MaxHits; d.Alignment = s.Alignment; d.IdLevel = s.IdLevel;
            d.Engaged = s.Engaged; d.JoinOffer = s.JoinOffer; d.Stamp = s.Stamp;
        }
        TreasureClass = r.TreasureClass; Lead = r.Lead; Gold = r.Gold; ChestTrap = r.ChestTrap; ChestLock = r.ChestLock;
        LeadMonster = r.Lead >= 0 && r.Lead < all.Count ? all[r.Lead] : null;
    }
}

/// <summary>
/// fn0D4D (MONSMOD.BAS:1361-1989), the monster generator, and the room logic of its caller
/// sub0A91 (MONSMOD.BAS:667-745).
/// </summary>
public static class EncounterGenerator
{
    /// <summary>
    /// sub0A91 (MONSMOD.BAS:667-745): what is in this room when the party walks into it. The only
    /// room gate the source has is the area id - "And p01AA &gt; 0" (703, 708); the port's old
    /// "the arrival room never holds anything" had no source and is gone.
    ///
    /// The room's records are read back as fn0E38 leaves them (687; MONSMOD.BAS:2367-2389). A room
    /// with a group standing keeps it - wounds, count, what you took it for and all - until the
    /// group is more than twice the respawn time old; an empty one refills once its stamp is more
    /// than one respawn time old, or was never set. A refill is fn0D4D, whose records are written
    /// back at once (1979), and a refill of a room that had been emptied puts one back on the
    /// level's tally when fn0D4D returns True (711-716).
    ///
    /// Returns what stands there - which may be no group at all but a chest left behind, since the
    /// Fight record is the room's too - or null when there is neither.
    /// </summary>
    public static Encounter? InRoom(IReadOnlyList<Monster> all, Floor floor, int level, int x, int y, DungeonState state, DateTime now,
                                    Random rng, Character? viewer = null, Character? idReader = null, Func<short, Item?>? item = null) =>
        InRoom(all, floor, level, x, y, state, now, rng, out _, viewer, idReader, item);

    /// <summary>
    /// The same, saying whether the level's file was written. <paramref name="written"/> is true when fn0D4D
    /// ran: it Puts the room to the file as it makes it - sub0C79's cleared records first
    /// (MONSMOD.BAS:1425, 794-803) and the new ones at the end (fn0F88, 1979) - so the caller
    /// writes dungeon.json then, not at the next change of area. A room read back as it stands
    /// writes nothing (sub0A91 only reads it, 687).
    /// </summary>
    public static Encounter? InRoom(IReadOnlyList<Monster> all, Floor floor, int level, int x, int y, DungeonState state, DateTime now,
                                    Random rng, out bool written, Character? viewer = null, Character? idReader = null,
                                    Func<short, Item?>? item = null)
    {
        written = false;
        int areaId = floor.AreaIdAt(x, y);
        if (areaId <= 0) return null;
        int header = floor.HeaderAreaCount;
        var rec = state.LoadRoom(level, areaId);                                         // fn0E38 (687)
        double nowSec = DungeonState.SecondsSinceMidnight(now);
        double stamp = rec.Foes[0].Stamp;
        Encounter e;
        if (state.RefillDue(level, areaId, header, now))                                 // 691-708
        {
            bool noneStanding = !rec.AnyStanding;                                        // l01B6, 710
            e = Generate(all, level, rng, floor, x, y, viewer, idReader, item, timer: nowSec);
            state.StoreRoom(level, areaId, e.ToRecord());                                // fn0F88 (1979)
            written = true;
            e.Fresh = !e.Failed;                                                         // 711-721
            if (!e.Failed && stamp != 0 && noneStanding) state.NoteRefilled(level, header); // 714-716
        }
        else
        {
            e = new Encounter();
            e.Take(rec, all);
            Finish(e, all, rng);
        }
        e.Room = (level, areaId);
        return e.Count > 0 || e.TreasureClass > 0 ? e : null;
    }

    /// <summary>
    /// fn0F88 (MONSMOD.BAS:3908-3936) from the port's side: the room's stored records brought up to
    /// date with what the party has done to its groups since they were read. A slot keeps its
    /// monster; its count is the members still standing (and not charmed or fled), and the front
    /// one's hits, maximum and name are the slot's. Slot 0 carries the group's temper and the offer
    /// to join. With a fight record given, the chest is taken from it too.
    /// </summary>
    public static RoomRecord Capture(RoomRecord stored, IEnumerable<CombatMonster> members, CombatEngine? fight, IReadOnlyList<Monster> all)
    {
        var rec = stored.Copy();
        var list = members.ToList();
        // slot 0's temper and offer are only the group's while it stands: once sub0C53 has emptied
        // the room (M4EF9 = False, MONSMOD.BAS:777) nothing of the dead is put back
        bool slot0Standing = rec.Foes[0].GroupCount > 0;
        for (int g = 0; g < rec.Foes.Length; g++)
        {
            var s = rec.Foes[g];
            if (s.GroupCount <= 0) continue;
            // the living members: one standing on a record at nought (AtNought) counts, as its
            // GroupCount does, and the dead and the gone do not
            var standing = list.Where(m => m.Group == g && m.Alive).ToList();
            s.GroupCount = standing.Count;
            if (standing.Count == 0)
            {
                // The original's Foes(g).KnownAs is the record itself, set the moment the group is
                // recognised (FRMDUNM.BAS:490) and left alone when the last of it falls - neither
                // the count running out nor sub0C53 (MONSMOD.BAS:775-777) touches it. The chest's
                // "Fight.M4F24 = Foes(0).KnownAs" reads it after the fight (3840), so a group
                // recognised and then killed keeps what it was recognised as.
                if (list.FirstOrDefault(m => m.Group == g && m.Believed != null) is CombatMonster last)
                    for (int i = 0; i < all.Count; i++) if (ReferenceEquals(all[i], last.Believed)) { s.KnownAs = i; break; }
                continue;
            }
            var front = standing[0];
            s.Hits = front.Hits;
            s.MaxHits = front.MaxHits;
            if (front.Believed != null)
                for (int i = 0; i < all.Count; i++) if (ReferenceEquals(all[i], front.Believed)) { s.KnownAs = i; break; }
            s.IdLevel = front.Identified ? 0 : Math.Max(1, s.IdLevel);
        }
        if (slot0Standing && list.Count > 0)
        {
            rec.Foes[0].Engaged = list.Any(m => m.Hostile) ? 1 : 0;
            rec.Foes[0].JoinOffer = list.FirstOrDefault(m => m.Group == 0)?.JoinOffer ?? 0;
        }
        if (fight != null)
        {
            rec.TreasureClass = fight.ChestClass;
            rec.ChestTrap = fight.ChestTrap;
            rec.ChestLock = fight.ChestLocked;
            // monGetGold empties Fight.Gold (2135), and an opened chest empties the whole record
            if (fight.GoldEarned == 0) rec.Gold = 0;
        }
        return rec;
    }

    // ---- VB's conversions, as fn0D4D uses them ----
    private static int Int(double d) => (int)Math.Floor(d);
    /// <summary>CInt, and any float stored into an Integer: round half to even.</summary>
    private static int CInt(double d) => (int)Math.Round(d, MidpointRounding.ToEven);
    private static long CLng(double d) => (long)Math.Round(d, MidpointRounding.ToEven);
    /// <summary>
    /// <c>Fight.Gold = ...</c> in fn0D4D, a Currency assignment: the Double scaled by 10,000 and
    /// rounded half to even (<see cref="VbCurrency.FromDouble"/>) - not <c>(decimal)d</c>, which
    /// first cuts the Double to fifteen significant digits and can tip a tie the other way. An Overflow there goes to its trap (MONSMOD.BAS:1426,
    /// 1984-1986), which says "Base Error 'Overflow' Generating Monsters!" on Messages and
    /// <c>Resume Next</c>: the assignment is skipped and the gold stays as it was. Not reachable with
    /// the shipped MDATA5 (gold factors 2 to 7), only with edited data.
    /// </summary>
    private static void SetGold(Encounter e, double d)
    {
        if (VbCurrency.TryFromDouble(d, out var v)) e.Gold = v.ToDecimal();
        else e.Messages.Add(GoldOverflowLine);
    }

    /// <summary>fn0D4D's handler line for the Overflow (Error$(6) is "Overflow").</summary>
    public const string GoldOverflowLine = "Base Error 'Overflow' Generating Monsters!";

    /// <summary>
    /// <c>NumMonsterClasses</c>, the size of MDATA1's class table. A probe that has not loaded
    /// MDATA1 falls back on the highest class a monster carries, plus one - the same 18 for the
    /// shipped data. The game always has MDATA1.
    /// </summary>
    public static int ClassCount(IReadOnlyList<Monster> all) =>
        Mdata1.MonsterClasses.Count > 0 ? Mdata1.MonsterClasses.Count : all.Count == 0 ? 0 : all.Max(m => (int)m.Type) + 1;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<Monster>, int[,]> _ranges = new();

    /// <summary>
    /// <c>gv1DD2</c> as sub06D7 builds it (MONSMOD.BAS:4038-4062): for each class, the first table
    /// index of its first run and the index before the next class starts, -1 where there is none.
    /// The walk starts at index 1, so record 0 ("GOBLIE") is never in any class; and the last
    /// class's end is forced to the last record whatever it holds.
    /// </summary>
    public static int[,] ClassRanges(IReadOnlyList<Monster> all)
    {
        if (_ranges.TryGetValue(all, out var cached) && cached.GetLength(0) == ClassCount(all)) return cached;
        int nc = ClassCount(all), n = all.Count;
        var r = new int[Math.Max(nc, 1), 2];
        for (int c = 0; c < nc; c++) { r[c, 0] = -1; r[c, 1] = -1; }
        for (int c = 0; c < nc; c++)
            for (int i = 1; i <= n - 1; i++)
            {
                if (r[c, 0] == -1 && all[i].Type == c) r[c, 0] = i;
                if (all[i].Type != c && r[c, 0] > -1) { r[c, 1] = i - 1; i = n - 1; }
            }
        if (nc > 0) r[nc - 1, 1] = n - 1;
        _ranges.AddOrUpdate(all, r);
        return r;
    }

    /// <summary>
    /// fn0D4D's Static <c>m029E</c> and <c>m029C</c>, the companion window's reach below and above.
    /// They outlive the call, and a spawn mode of exactly 7 matches no Case (MONSMOD.BAS:1739-1765),
    /// so it reuses whatever window the last companion pick set - in this call or an earlier one.
    /// No monster in the shipped MDATA5 has mode 7 (nor 5 or 6); the fault is kept regardless.
    /// </summary>
    private static int _below, _above;

    /// <summary>The window a spawn mode of 7 would read, for the probes.</summary>
    public static (int Below, int Above) StaleWindow { get => (_below, _above); set => (_below, _above) = value; }

    /// <summary>
    /// For the probes only, a square-less call: an area that admits every class, has no lair, and
    /// is numbered 18 so that the newbie rule is out of play. The game always passes its square.
    /// </summary>
    private const int ProbeAreaId = 18;

    /// <summary>
    /// fn0D4D (MONSMOD.BAS:1361-1989), line for line and draw for draw. <paramref name="viewer"/> is
    /// <c>Chars(Me)</c>, whose level, area, alignment, charisma and current guild the routine reads;
    /// <paramref name="idReader"/> is <c>Chars(0)</c>, whose identify skill decides the
    /// misidentification (it passes the constant m01BC = 0, MONSMOD.BAS:1889); it defaults to the
    /// viewer. <paramref name="forceLead"/> is for the test hooks: it enters at L1F688 with that
    /// table index as the lead, the way the lair path does.
    ///
    /// The routine has no retry cap anywhere and neither has this. Its loops end with probability 1
    /// for the shipped data - the encounter termination probe proves that from MDATA5 and MDATA11
    /// and fails if the data ever stops allowing it.
    /// </summary>
    public static Encounter Generate(IReadOnlyList<Monster> all, int level, Random rng, Floor? floor = null, int x = -1, int y = -1,
                                     Character? viewer = null, Character? idReader = null, Func<short, Item?>? item = null, int forceLead = -1, double timer = 0)
    {
        double Rnd() => rng.NextDouble();
        float RndF() => (float)rng.NextDouble();   // Rnd is a Single; this is for the draws kept in m026A

        // sub0C79 clears Foes(0..3) and Fight (MONSMOD.BAS:1425, 794-804)
        var e = new Encounter();
        var foes = e.Foes;
        int n = all.Count, nc = ClassCount(all);
        if (n == 0 || nc == 0) return e;
        var range = ClassRanges(all);
        // v?0306, ReDim'd once per call (MONSMOD.BAS:1428). Its declared type did not survive the
        // decompile; it is held as a double. It is one array for the whole call, so values the
        // class pick writes at indices 0..n-1 stay behind for the later passes, as they do there.
        var v = new double[n];
        double V(int i) => i >= 0 && i < n ? v[i] : 0;   // past the end is an error, and Resume Next skips the add
        // m02EE, ReDim'd to NumMonsterClasses - 1: unfilled slots stay 0, which is class 0
        var list = new int[nc];
        int listN = 0;

        bool onSquare = floor != null && x >= 0 && y >= 0 && x < floor.Width && y < floor.Height;
        bool Square(int bit) => onSquare && floor!.Has(x, y, bit);
        int areaId = onSquare ? floor!.AreaIdAt(x, y) : ProbeAreaId;
        Floor.Area area = onSquare
            ? (areaId >= 0 && areaId < floor!.Areas.Count ? floor.Areas[areaId] : default)
            : new Floor.Area { SpawnMask = nc >= 31 ? -1 : (1 << nc) - 1, LairId = 0 };

        int meLevel = level;
        int meAlign = viewer != null ? (int)viewer.Alignment : (int)Alignment.Neutral;
        var reader = idReader ?? viewer;

        // m0282, the floor the pick aims at (MONSMOD.BAS:1431). It is set here and nowhere before
        // L1EB02, so a restart from inside the group loop keeps whatever floor the loop left in it.
        int m0282 = level;
        // v?031C: a square with this bit gets nothing (1432-1436). The bit number was lost in the
        // decompile. The only other use, the chest teleporter's landing loop at MONSMOD.BAS:3575,
        // is the same loop as the teleporter in sub0835 (MAZEMOD.BAS:1803), which spells it BitRock -
        // so it is ROCK, bit 19.
        if (Square(Floor.ROCK)) return e;
        // v?031E: one floor deeper (1437-1439). STUD, bit 22 - see Rules.EncounterFloor.
        bool stud = Square(Floor.STUD);
        if (stud) m0282++;
        long mask = area.SpawnMask;   // m0266, M5D45 (1440-1441)

        int lead, cls, m0284, spread = 0, m0272 = -1, m0274 = -1;
        int m028C, m028E, m0290, m02A2;
        int groups;
        int tries = 0;                                                                 // m02A0, 1430
        float r, acc, W;                     // m026A, m0288, m027E are Single (MONSMOD.BAS:1367-1378)

    L1EB02:
        lead = -1; cls = -1; m0284 = -1;
        if (mask == 0) return e;                                                         // 1448-1452
        if (forceLead >= 0 && forceLead < n) { lead = forceLead; goto L1F688; }
        // the lair (1454-1463): gc1DC8 = 75, raw Single comparison, a fresh roll on every restart
        if (area.LairId > 0)
        {
            if (RndF() * 100f <= 75)
            {
                for (int i = 0; i <= n - 1; i++) if (all[i].Id == area.LairId) { lead = i; break; }
                // no such monster would leave Monsters(-1) to the error handler; every lair id in
                // the shipped MDATA11 exists in MDATA5, so that path is not followed here
                if (lead < 0) { e.Failed = true; return e; }
                goto L1F688;
            }
        }
        // 1465-1469: mc032C = 18
        bool newbie = meLevel == 1 && areaId < 18;
        // 1470-1485: the roll is always drawn; with it, a class at random, mask or no mask
        if (Int(Rnd() * 100) + 1 < 2 && !newbie)
            cls = Int(Rnd() * nc);
        else
        {
            listN = 0;
            for (int c = 0; c <= nc - 1; c++)
                if (c < 63 && ((mask >> c) & 1) != 0 && range[c, 0] != -1) list[listN++] = c;
            if (listN == 0) cls = Int(Rnd() * nc);
        }
        // 1486-1503: the spread, its unreachable ElseIf ladder and the stud's override
        spread = Rules.EncounterSpread(meLevel, rng, stud);

        // 1504-1557: which class
        if (cls == -1)
        {
            W = 0; int count = 0;
            for (int k = 0; k <= listN - 1; k++)
            {
                int c = list[k];
                m0274 = range[c, 1]; m0272 = -1;
                v[k] = 0;
                for (int m = range[c, 0]; m <= range[c, 1]; m++)
                {
                    if (m0272 == -1 && all[m].LevelFound >= m0282 - spread) m0272 = m;
                    if (all[m].LevelFound > m0282)
                    {
                        m0274 = m - 1;
                        if (m0274 < m0272) m0274 = m0272;
                        break;
                    }
                }
                if (m0272 > -1 && m0274 > -1)
                    for (int m = m0272; m <= m0274; m++)
                    {
                        int f = all[m].LevelFound;
                        if (f < m0282 - spread || f > m0282) continue;
                        W += all[m].Chance; v[k] += all[m].Chance; count++;
                    }
            }
            if (W == 0 || count == 0)
                // UBound(m02EE) - 1 = NumMonsterClasses - 2: the draw lands on any of the first
                // NumMonsterClasses - 1 slots, and the unfilled ones name class 0 (1540)
                cls = list[CInt(Rnd() * (nc - 1 - 1))];
            else
            {
                // Rnd * 101 against percentages, and the running total starts at the first
                // class's weight and then adds the class it has just passed - v(0) twice, and
                // every later class tested against the weight of the one before it (1542-1552)
                r = RndF() * 101f;
                acc = (float)v[0];
                for (int k = 0; k <= listN - 1; k++)
                {
                    if (r <= acc / W * 100) { cls = list[k]; break; }
                    if (r > acc / W * 100) acc = (float)(acc + v[k]);
                }
                if (cls == -1) cls = list[listN - 1];
            }
        }

    L1F1B4:
        if (cls == -1) goto L1EB02;
        // every class holds monsters in the shipped data, so gv1DD2(cls, 0) is never -1 here
        if (range[cls, 0] < 0) goto L1EB02;
        m0274 = range[cls, 1]; m0272 = -1;
        for (int m = range[cls, 0]; m <= range[cls, 1]; m++)
        {
            if (m0272 == -1) m0272 = m;
            if (all[m].LevelFound > m0282)
            {
                m0274 = m - 1;
                if (m0274 < m0272) m0274 = m0272;
                break;
            }
        }

    L1F2A0:
        W = 0;
        if (m0272 == -1 || m0274 == -1) { e.Restarts++; goto L1EB02; }
        for (int m = m0272; m <= m0274; m++)
        {
            int f = all[m].LevelFound;
            if (f < m0282 - spread || f > m0282) continue;
            W += all[m].Chance;
        }
        if (W == 0)
        {
            // 1592-1599: reach one floor further back; past three, start again from the top
            spread++;
            if (spread > 3) { e.Restarts++; goto L1EB02; }
            goto L1F1B4;
        }
        m0274 = range[cls, 1]; m0272 = -1;
        for (int m = range[cls, 0]; m <= range[cls, 1]; m++)
        {
            v[m] = 0;
            if (m0272 == -1) m0272 = m;
            int f = all[m].LevelFound;
            if (f < m0282 - spread) { }
            else if (f > m0282)
            {
                m0274 = m - 1;
                if (m0274 < m0272) m0274 = m0272;
                break;
            }
            else v[m] = all[m].Chance / W * 100;
        }
        if (m0274 < m0272) m0274 = m0272;

    L1F4F8:
        r = RndF() * 100f;
        if (m0272 == -1)
        {
            // reached from the group loop, which has just set m0272 = -1: L1F2A0 then sends it
            // straight back to L1EB02, so a companion that cannot be found re-rolls everything
            spread++;
            goto L1F2A0;
        }
        acc = (float)v[m0272];
        for (int m = m0272 + 1; m <= m0274 + 1; m++)
        {
            if (r <= acc) { lead = m - 1; break; }
            if (m > n - 1) { lead = m - 1; break; }
            if (r > acc) acc = (float)(acc + v[m]);
        }
        if (lead == -1) { e.Restarts++; goto L1EB02; }
        // the newbie guard (1646): table INDEX 4, not id 4 - the Slave
        if (all[lead].GuildLevel > 2 && lead != 4 && meLevel == 1 && areaId < 18) { e.Restarts++; goto L1EB02; }
        if (all[lead].Id == Asmodeus.MonsterId) e.AsmodeusCues++;                          // 1649: sub09DE mc032E

    L1F688:
        m028C = all[lead].CompanionType;
        m028E = all[lead].CompanionSpawnMode;
        m0290 = all[lead].CompanionSubtype;
        m02A2 = lead;
        _ = Rnd() * 99;                                                                  // 1656, never read
        e.Lead = lead;
        e.LeadMonster = all[lead];
        r = RndF() * 99f;                                                                  // 1658
        e.TreasureClass = 0;
        {
            var lm = all[lead];
            // 1661-1665: the lair's own monster, whichever path drew it, adds gc1DCA = 25
            int bonus = 0;
            if (area.LairId > 0 && lm.Id == area.LairId && lm.BoxChance.Length > 2 && lm.BoxChance[2] > 0) bonus += 25;
            for (int k = 1; k <= 2; k++)
                if (r < lm.BoxChance[k] + bonus) e.TreasureClass = k + 1;
            // 1671-1694: the trap. The chest trap is cleared on every pass; the lock is not.
            e.ChestTrap = 0;
            int chance = e.TreasureClass switch { 1 => 25, 2 => 75, 3 => 90, _ => 0 };
            if (e.TreasureClass > 0 && lm.TrapFlags > 0)
            {
                int t = Int(Rnd() * 100) + 1;
                if (t < chance)
                {
                    e.ChestTrap = -1;
                    // every trap mask in MDATA5 has a bit below 12, so this ends
                    do
                    {
                        t = Int(Rnd() * (11 + 1));
                        if ((lm.TrapFlags & (1 << t)) != 0) e.ChestTrap = t;
                    } while (e.ChestTrap == -1);
                }
            }
            // 1697-1702: not on an anti-magic (18) or an extinguisher (10) square. ChestLock is
            // only ever set, so a lock rolled on a pass that is then thrown away still stands.
            if (!Square(Floor.ANTIMAGIC) && !Square(Floor.EXTINGUISHER))
            {
                int t = Int(Rnd() * 100) + 1;
                if (t <= lm.LockedChance && e.TreasureClass > 1) e.ChestLock = 1;
            }
            m0284 = lead;
            // 1704-1712: the number of groups, mc0186 = 4
            groups = lm.NumGroups;
            if (Int(Rnd() * 100) < groups / (4.0 * 2) * 100)
                groups = groups - Int(Rnd() * (lm.NumGroups / 2.0 + 1));
            if (groups < 1) groups = 1;
            else if (groups > 4) groups = 4;
            // 1713-1724: hostility, on Foes(0) alone. m033C and m033E are Statics never assigned: 0.
            foes[0].Engaged = 1;
            if (lm.AlignmentValue == 0 && meAlign == 0) foes[0].Engaged = 0;
            else if (lm.AlignmentValue == 1) foes[0].Engaged = Int(Rnd() * 100) < 40 ? 0 : 1;
            foes[0].GroupCount = 1;
        }

        for (int g = 0; g <= groups - 1; g++)
        {
            if (g == 0)
            {
                m0282 = all[lead].LevelFound;
                goto L2028A;
            }
            // (m0292 > 10000 is tested at 1734, but m0292 is never incremented)
            switch (m028E)
            {
                case 0: _below = 3; _above = -1; break;
                case 1: _below = 3; _above = 0; break;
                case 2: _above = 0; _below = 0; break;                     // + m0292, always 0
                case 3: m0284 = m02A2; goto L2028A;
                case 4: m0284 = 4; goto L2028A;
                case 5: m0284 = 5; goto L2028A;
                case 6: m0284 = 4; goto L2028A;
                case > 7: m0284 = m028E - 7; goto L2028A;
                // 7, or anything below 0: no Case, the Static window stands
            }
            if (m0282 + _above < 1) _above = 0;
            m0272 = -1; m0274 = -1; W = 0;
            // A CompanionType outside the class table would be a subscript error on gv1DD2 and
            // the handler's Resume Next (1984-1986); it is taken as a class with nobody in it, so
            // W stays 0 and the encounter starts again. No monster in MDATA5 has one - the loops
            // probe reports any that do.
            bool classOk = m028C >= 0 && m028C < nc;
            for (int m = classOk ? range[m028C, 0] : -1; classOk && m <= range[m028C, 1] && m >= 0; m++)
            {
                var cm = all[m];
                if (cm.LevelFound < m0282 - _below) { }
                else if (cm.LevelFound > m0282 + _above) break;
                else if (cm.InGroup == 1) { }
                else if (m != m02A2)
                {
                    if ((m0290 > 0 && cm.Subtype == m0290) || m0290 == 0) W += cm.Chance;
                }
            }
            // 1781-1783: nobody to come along - back to L1F4F8, which starts the whole
            // encounter again. The slots already filled are not cleared.
            if (W == 0) goto L1F4F8;
            m0274 = range[m028C, 1];
            for (int m = range[m028C, 0]; m <= range[m028C, 1] && m >= 0; m++)
            {
                v[m] = 0;
                if (m0272 < 0) m0272 = m;
                var cm = all[m];
                if (cm.LevelFound < m0282 - _below) { }
                else if (cm.LevelFound > m0282 + _above) { m0274 = m - 1; break; }
                else if (cm.InGroup == 1) { }
                else if (m != m02A2)
                {
                    if ((m0290 > 0 && cm.Subtype == m0290) || m0290 == 0) v[m] = cm.Chance / W * 100;
                }
            }
            if (m0274 < 0) m0274 = m0272;
            tries = 0;                                                                 // m02A0, 1804

        L2003C:
            m0284 = -1;
            r = RndF() * 99f;
            acc = (float)V(m0274);
            // top down; the first step adds v(m0274) a second time, and a hit on the lead passes
            // the pick to the next one down whatever its weight
            for (int c = m0274 + 1; c >= m0272 + 1; c--)
            {
                if (r > acc) acc = (float)(acc + V(c - 1));
                if (r <= acc && c - 1 != lead)
                {
                    if (m0290 > 0)
                    {
                        if (all[c - 1].Subtype != m0290)
                        {
                            tries++;
                            if (tries > 150) m0290 = 0;
                            if (tries > 300)
                            {
                                e.Messages.Add("TryNumber > 300!");
                                e.Failed = true;
                                return Finish(e, all, rng);
                            }
                            acc = (float)(acc + V(c));
                            m0284 = -1;
                        }
                        else { m0284 = c - 1; break; }
                    }
                    else { m0284 = c - 1; break; }
                }
            }
            // 1838-1840: with nothing picked this reads Monsters(-1) and the handler skips it
            if (m0290 == 0 && m0284 >= 0) m0290 = all[m0284].CompanionSubtype;
            if (m0284 > -1)
            {
                // the chain: the next group is sought from THIS pick's companion fields
                m028C = all[m0284].CompanionType;
                m028E = all[m0284].CompanionSpawnMode;
                m0290 = all[m0284].CompanionSubtype;
                m02A2 = m0284;
                m0282 = all[m0284].LevelFound;
            }

        L2028A:
            if (m0284 == -1)
            {
                // 1850-1853: and round again, the try count not reset
                e.Messages.Add("Error finding like-types for '" + all[lead].Name + "'.");
                goto L2003C;
            }
            // A direct mode naming a table index past the end (mode - 7 >= the monster count) would
            // make every line of L2028A that reads Monsters(m0284) an error the handler skips; the
            // port leaves the slot as it was rather than half-fill it. Not in MDATA5 (the largest
            // mode, 381, names index 374 of 401); the loops probe reports any that would.
            if (m0284 < n) FillGroup(e, g, m0284, all, rng, Square(Floor.FOG), reader, item, meAlign, viewer, range, timer);
        }

        // 1969-1978: the gold scales with every group standing, the stale ones included
        int standing = 0;
        foreach (var s in foes) if (s.GroupCount > 0) standing += s.GroupCount;
        SetGold(e, (double)e.Gold * (standing / 2.0));
        if (e.ChestLock == 1) SetGold(e, (double)e.Gold * 1.5);
        return Finish(e, all, rng);
    }

    /// <summary>One group, from L2028A (MONSMOD.BAS:1849-1967).</summary>
    private static void FillGroup(Encounter e, int g, int m0284, IReadOnlyList<Monster> all, Random rng, bool fog,
                                  Character? reader, Func<short, Item?>? item, int meAlign, Character? me, int[,] range, double timer)
    {
        double Rnd() => rng.NextDouble();
        var s = e.Foes[g];
        var mon = all[m0284];
        // 1854-1858
        s.GroupCount = mon.InGroup;
        s.GroupCount = s.GroupCount - CInt(Rnd() * (mon.InGroup / 2.0));
        if (s.GroupCount < 1) s.GroupCount = 1;
        // 1859-1868: the group's gold, into the Currency Fight.Gold
        if (g == 0) e.Gold = 0;
        int gf = mon.GoldFactor;
        long gold = CLng(gf * Math.Pow(10, gf - .5) / 4);
        gold = CLng((gold / 4.0 + Rnd() * (gold / 2.0) + Rnd() * (gold / 2.0)) * Math.Log((mon.LevelFound + 1) / Math.Log(2)));
        if (gold < 500) gold = Int(Rnd() * 500) + 2;
        double lg = Math.Log(s.GroupCount + 1);
        SetGold(e, (double)e.Gold + gold * (lg * (lg / 2)));
        // 1869-1879: ONE set of hits, for the member at the front, with no floor under it -
        // a monster of 1 hit comes out at 0 or 2
        s.Att = mon.Att; s.Def = mon.Def; s.Hits = mon.Hits;
        int sign = Int(Rnd() * 2);
        int swing = Int(Rnd() * (mon.Hits * .5) / 2) + 1;
        s.Hits = sign == 0 ? s.Hits - swing : s.Hits + swing;
        s.MaxHits = s.Hits;
        s.Alignment = mon.AlignmentValue;
        s.Ref = m0284; s.KnownAs = m0284;
        s.Stamp = timer;                                                              // 1883: M4EE6 = Timer
        // 1884-1887: known, unless met in fog (bit 20)
        s.IdLevel = fog ? 1 : 0;
        s.KnownAs = KnownAsIndex(all, m0284, reader, item, rng, range);
        // 1948-1967: the offer to join. Foes(0)'s is wiped on every pass, and it is the only one
        // anything reads, so an encounter of more than one group never offers. Foes(g).Engaged
        // is only ever set for slot 0; the others are 0, so every companion group is weighed.
        e.Foes[0].JoinOffer = 0;
        if (s.Engaged == 0)
        {
            if (mon.AlignmentValue == 1 || mon.AlignmentValue == meAlign)
            {
                int guildLevel = me != null && me.Guilds.Count > 0 ? me.Guild.Level : 0;
                if (mon.GuildLevel <= guildLevel)
                {
                    int t = Int(Rnd() * 3000) + 1;
                    double bar = (me?.Stats[(int)Stat.Cha] ?? 0) * 10;
                    bar = bar - (mon.LevelFound - 1) * 10;
                    if (bar < 0) bar = 0;
                    if (t <= bar)
                    {
                        int most = s.GroupCount;
                        if (most > 4) most = 4;
                        s.JoinOffer = Int(Rnd() * most) + 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The misidentification, MONSMOD.BAS:1888-1946: what the party takes group's monster for.
    /// The bar is <c>50 + (Chars(0)'s identify skill - Log(guild level) * (floor + 1)) * 10</c>,
    /// held in a Single and clamped 5 to 95. Miss it, and there is a monster of the same subtype
    /// to confuse it with, and the name is stepped up or down the subtype's list by
    /// <c>Int(Rnd * (spread + 1.5))</c>, where the spread is <c>Rnd * (Log(floor) + 2.5)</c> stored
    /// into an Integer. A step that leaves the list, or the real monster's class, is drawn again;
    /// the 101st try takes the real one, after its two draws. There is no guard on Log(0) - no
    /// monster in MDATA5 has a floor or guild level of 0.
    /// </summary>
    private static int KnownAsIndex(IReadOnlyList<Monster> all, int refIndex, Character? reader, Func<short, Item?>? item, Random rng, int[,] range)
    {
        double Rnd() => rng.NextDouble();
        var mon = all[refIndex];
        int tried = 0;                                                                 // m02B2
        double skill = IdSkill(reader, item) - Math.Log(mon.GuildLevel) * (mon.LevelFound + 1);
        double bar = 50 + skill * 10;
        if (bar > 95) bar = 95;
        if (bar < 5) bar = 5;
        int roll = Int(Rnd() * 100) + 1;
        var kin = new List<int>();
        int me = 0;
        for (int i = 0; i <= all.Count - 1; i++)
        {
            if (all[i].Subtype == mon.Subtype) kin.Add(i);
            if (i == refIndex) me = kin.Count - 1;
        }
        if (!(roll > bar && kin.Count > 1)) return refIndex;
        int spread = CInt(Rnd() * (Math.Log(mon.LevelFound) + 2.5));
        int cls = mon.Type;
        int first = cls >= 0 && cls < range.GetLength(0) ? range[cls, 0] : -1;
        int last = cls >= 0 && cls < range.GetLength(0) ? range[cls, 1] : -1;
        int pick;
        do
        {
            bool up = Int(Rnd() * 100) + 1 < bar;
            pick = Int(Rnd() * (spread + 1.5));
            if (up) pick = me + pick <= kin.Count - 1 ? kin[me + pick] : -1;
            else pick = me - pick >= 0 ? kin[me - pick] : -1;
            if (pick < first) pick = -1;
            if (pick > last) pick = -1;
            tried++;
            if (tried > 100) pick = refIndex;
        } while (pick == -1);
        return pick;
    }

    /// <summary>mdrcharGetIDSkill (MAINMOD2.BAS:2502-2515): nought with no guild level at all.</summary>
    private static double IdSkill(Character? c, Func<short, Item?>? item)
    {
        if (c == null) return 0;
        return item != null
            ? Rules.IdentifySkill(c.TopGuildLevel, c.EffectiveStat((int)Stat.Int, item), c.EffectiveStat((int)Stat.Wis, item))
            : Rules.IdentifySkill(c.TopGuildLevel, c.Stats[(int)Stat.Int], c.Stats[(int)Stat.Wis]);
    }

    /// <summary>
    /// What the port needs, which the original never builds: one <see cref="CombatMonster"/> per
    /// member. The front member of each group is the Foes record itself - its hits are the ones
    /// fn0D4D rolled, or the ones the room was left with. The members behind it are only the
    /// record's count: the original gives each its hits as it steps forward, in sub0D08
    /// (MONSMOD.BAS:1991-2008), at the moment the one in front falls, and nothing of theirs is
    /// ever stored - the level's file holds one record per group. So they are made
    /// <see cref="CombatMonster.Unrolled"/> here and draw nothing; the engine loads each where the
    /// original calls sub0D08. The port had rolled every one of them here, after fn0D4D's draws and
    /// again on every visit that read the room back, so the draws came in another order and number.
    /// A group's members carry its slot number, so two groups of one monster stay two groups.
    /// </summary>
    private static Encounter Finish(Encounter e, IReadOnlyList<Monster> all, Random rng)
    {
        bool hostile = e.Foes[0].Engaged != 0;
        for (int g = 0; g < e.Foes.Length; g++)
        {
            var s = e.Foes[g];
            if (s.GroupCount <= 0 || s.Ref < 0 || s.Ref >= all.Count) continue;
            var def = all[s.Ref];
            var seen = s.KnownAs >= 0 && s.KnownAs < all.Count ? all[s.KnownAs] : def;
            for (int i = 0; i < s.GroupCount; i++)
            {
                bool front = i == 0;
                e.Add(new CombatMonster
                {
                    Def = def,
                    Hits = front ? s.Hits : Math.Max(1, (int)def.Hits),
                    MaxHits = front ? s.MaxHits : def.Hits,
                    Unrolled = !front,
                    // a record at nought with its count above it stands (CombatMonster.AtNought)
                    AtNought = front && s.Hits <= 0,
                    Alignment = s.Alignment,
                    Group = g,
                    Identified = s.IdLevel == 0,
                    Believed = seen,
                    Hostile = hostile,
                    JoinOffer = g == 0 && i == 0 ? e.Foes[0].JoinOffer : 0,
                });
            }
        }
        return e;
    }

    /// <summary>
    /// sub0C53's bookkeeping (MONSMOD.BAS:776-789) for the case MAINMOD2.BAS:4110-4113 sends it:
    /// every group has joined you and none is left. The room is stamped emptied and the level's
    /// tally loses one; the gold is paid now when there is no chest (monGetGold), and otherwise
    /// waits in the chest, which the returned fight record carries. Each step is written back as
    /// the original writes it: the emptied groups, then sub0C53's stamp (778), then monGetGold's
    /// emptied gold (2178).
    /// </summary>
    public static CombatEngine ClearedByJoining(Party party, List<CombatMonster> emptied, DungeonState state, int level, int areaId,
                                                int headerAreas, DateTime now, Func<short, Item?> item, Random rng,
                                                IReadOnlyList<Monster> all)
    {
        var fight = new CombatEngine(party, emptied, item, rng);
        if (areaId > 0)
        {
            state.StoreRoom(level, areaId, Capture(state.LoadRoom(level, areaId), emptied, null, all));
            state.MarkCleared(level, areaId, now, headerAreas);
        }
        fight.AwardSpoils();
        if (areaId > 0) state.StoreRoom(level, areaId, Capture(state.LoadRoom(level, areaId), fight.Monsters, fight, all));
        return fight;
    }

    /// <summary>
    /// The misidentification alone, for the probes: what a party led by <paramref name="viewer"/>
    /// takes <paramref name="def"/> for.
    /// </summary>
    public static Monster BelievedToBe(IReadOnlyList<Monster> all, Monster def, Character? viewer, Random rng, Func<short, Item?>? item = null)
    {
        int index = -1;
        for (int i = 0; i < all.Count; i++) if (ReferenceEquals(all[i], def)) { index = i; break; }
        if (index < 0 || viewer == null) return def;
        return all[KnownAsIndex(all, index, viewer, item, rng, ClassRanges(all))];
    }


    /// <summary>
    /// Pressing <c>j</c>, the offer accepted (MAINMOD2.BAS:4095-4125). If every slot is full it
    /// says "Join - No room for more Companions!" and the offer stands. Otherwise the offer is
    /// taken up and **nothing is said** - lblMonsterHit, which carried the offer, is simply hidden.
    /// The port had "N X join you."
    ///
    /// Every one that comes is made by <c>fn1163(Me, Foes(0), Foes(0).Hits, -10)</c> inside the
    /// loop, and the loop never loads the next member into <c>Foes(0)</c> - so **they all arrive
    /// with the front one's hits and its rolled maximum**. Reproduced. They are bound at -10, the
    /// mark of a companion that came of its own free will, and identified by fn1272. The port had
    /// taken each from the back of the group with its own hits, capped at the creature's base.
    /// </summary>
    public static string AcceptJoin(Character who, List<CombatMonster> group, Func<short, Item?> item,
                                    Random? rng = null, int target = -1)
    {
        if (group.Count == 0 || group[0].JoinOffer <= 0) return "";
        int offered = group[0].JoinOffer;
        if (who.Companions.FreeSlots == 0) return "Join - No room for more Companions!";

        group[0].JoinOffer = 0;
        var front = group.FirstOrDefault(m => m.Alive) ?? group[0];
        int hits = front.Hits, max = front.MaxHits > 0 ? front.MaxHits : front.Hits;
        var def = front.Def;
        // CombatOrders(Me).M547A, the group the one in command is aiming at: in a room at peace
        // sub0A91 leaves it on the first group standing, M547A = fn0DCD(-1) (MONSMOD.BAS:737), unless chosen since
        var record = target >= 0 && target < group.Count ? group[target] : group.FirstOrDefault(m => m.Alive);
        for (int i = 0; i < offered; i++)
        {
            // Foes(0).GroupCount > 0: only the first group, however many stand behind it
            if (who.Companions.FreeSlots == 0 || group.Count == 0 || !CombatMonster.SameGroup(group[0], front)) break;
            who.Companions.Add(new Companion
            {
                Ref = def.Ref,                  // p00D6.Ref, the front one (MAINMOD2.BAS:388)
                MonsterId = def.Id,
                Name = "",
                Hits = hits,
                MaxHits = max,
                Alignment = front.CompanionAlignment,   // Foes(0).Alignment (MAINMOD2.BAS:392)
                BindingLevel = FreeWill,
                IdLevel = CombatEngine.CharmIdLevel(who, def, item),
            }, keepSlotName: true);   // fn1163 leaves the slot's name as it was (MAINMOD2.BAS:388-400)
            // GroupCount - 1 (4105): the count goes down and the record stays as it was
            group.RemoveAt(0);
        }
        // "If Foes(0).GroupCount > 0 Then sub0D08 Me" (MAINMOD2.BAS:4110-4111): the record loaded
        // afresh is the aimed-at group's - the first group's own when it is aimed at, so the one now
        // at its front gets new hits. Aimed elsewhere, that group's front is loaded instead, and the
        // first group's record keeps the hits the joiners took with them for whoever is now in front.
        var next = group.FirstOrDefault(m => m.Alive && CombatMonster.SameGroup(m, front));
        if (next != null && rng != null)
        {
            var aimed = record == null ? null : group.FirstOrDefault(m => m.Alive && CombatMonster.SameGroup(m, record));
            if (aimed != null && (aimed.Unrolled || aimed.Group >= 0)) aimed.LoadRecord(rng);
            if (next.Unrolled) next.TakeRecord(front.Hits, front.MaxHits);
        }
        return "";
    }

    /// <summary>The bind level a monster that came of its own free will carries (MAINMOD2.BAS:4099).</summary>
    public const int FreeWill = -10;


    /// <summary>At most four groups make up one encounter (mc0186 in MONSMOD.BAS).</summary>
    public const int MaxGroups = 4;

    /// <summary>
    /// What a monster is called to a given viewer, <c>fn0E23</c> (MODULE4.BAS:2274). An invisible
    /// one you cannot see is "Unseen Monster"; otherwise the name depends on how well the group
    /// has been identified - 0 its own name, 1 its class, 2 its broader group - and is then
    /// pluralised by the original's own table when there is more than one.
    /// </summary>
    public static string NameFor(Monster def, Character? viewer, int count, bool identified = true)
        => NameFor(def, viewer, count, identified ? 0 : 1);

    public static string NameFor(Monster def, Character? viewer, int count, int idLevel)
    {
        bool invisible = (def.SpecialPropertyFlags & (1 << Effects.Invisible)) != 0;
        if (invisible && viewer != null && !viewer.SeeingInvisible && viewer.DungeonLevel > 0)
            return count > 1 ? "Unseen Monsters" : "Unseen Monster";

        string name = !Mdata1.Loaded ? def.Name : idLevel switch
        {
            1 => Mdata1.MonsterClassName(def.Type, def.Name),
            2 => Mdata1.MonsterGroupName(def.Type, def.Name),
            _ => def.Name,
        };
        return Pluralise(name, count);
    }

    /// <summary>
    /// <c>fn0E23</c>'s pluralisation, in its own order. Nine three-letter endings and two
    /// two-letter ones are left alone whatever the count - they are the invented-language names -
    /// and the rest is a ladder of irregulars ending in "add an s unless it already has one".
    /// </summary>
    public static string Pluralise(string name, int count)
    {
        string Tail(int n) => name.Length >= n ? name[^n..] : name;
        string Drop(int n) => name[..^n];

        switch (Tail(3))
        {
            case "sae": case "lex": case "ish": case "asu": case "shi":
            case "toa": case "ngi": case "ubi": case "kul": return name;
        }
        if (Tail(2) == "yr" || Tail(2) == "ee") return name;
        if (count <= 1) return name;

        if (Tail(3) == "man") return Drop(3) + "men";
        if (Tail(3) is "bus" or "gus" or "eet") return Drop(2) + "i";
        if (Tail(2) is "ry" or "py") return Drop(1) + "ies";
        if (Tail(2) == "ch") return Tail(5) == "Leech" ? name + "es" : name + "s";
        if (Tail(3) == "sus") return Drop(1) + "mi";
        if (Tail(3) is "ief" or "arf" or "Elf" or "olf") return Drop(1) + "ves";
        if (Tail(3) == "mmy") return Drop(1) + "ies";
        return Tail(1) == "s" ? name : name + "s";
    }

    /// <summary>
    /// A one-line summary of a group, for the probes to print. It is **not** anything the game
    /// says - the original announces no encounter (see ShowWaiting) - and the game does not use it.
    /// </summary>
    public static string Describe(IEnumerable<CombatMonster> group, Character? viewer = null)
    {
        var list = group.ToList();
        var parts = list.GroupBy(m => m.Seen.Name).Select(g => g.Count() == 1 ? "a " + NameFor(g.First().Seen, viewer, 1, g.First().Identified ? 0 : 1) : $"{g.Count()} " + NameFor(g.First().Seen, viewer, g.Count(), g.First().Identified ? 0 : 1)).ToList();
        string s = parts.Count <= 1 ? parts.FirstOrDefault() ?? "something" : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
        return char.ToUpper(s[0]) + s[1..] + (list.Count == 1 ? " attacks!" : " attack!");
    }

    public static string Plural(string name)
    {
        if (name.EndsWith("s") || name.EndsWith("x") || name.EndsWith("ch") || name.EndsWith("sh")) return name + "es";
        if (name.EndsWith("f")) return name[..^1] + "ves";
        if (name.EndsWith("y") && name.Length > 1 && !"aeiou".Contains(name[^2])) return name[..^1] + "ies";
        return name + "s";
    }
}
