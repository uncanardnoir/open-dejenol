using System.Text.Json;

namespace Mordor.Core;

public sealed class GuildMembership
{
    public string Guild { get; set; } = "";
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    /// <summary>0 none, 1 defeat a monster, 2 fetch an item (M55CB in the guild membership).</summary>
    public int QuestType { get; set; }
    /// <summary>The id of the monster to kill or the item to bring (M57A9).</summary>
    public short QuestTarget { get; set; }
    /// <summary>Set when the quest monster has been defeated (M57B2).</summary>
    public bool QuestDone { get; set; }
}

public sealed class InventorySlot
{
    public short ItemId { get; set; }
    public bool Equipped { get; set; }
    public short Charges { get; set; }
    /// <summary>
    /// How much you know about this item. The original has four levels and only the General Store
    /// can take one all the way (Item_Identifying_Formula).
    /// </summary>
    public IdLevel Known
    {
        get => _known;
        set { _known = value; _knownRead = true; }
    }
    private IdLevel _known = IdLevel.All;
    private bool _knownRead;

    /// <summary>
    /// Fully identified, which is what the store's top price and the full description need. Not
    /// saved: it was, after Known, and reading it back turned a Partial or Mostly slot into Barely
    /// on every load.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Identified
    {
        get => Known == IdLevel.All;
        set => Known = value ? IdLevel.All : IdLevel.Barely;
    }

    /// <summary>
    /// A save written before Known existed carries only "Identified": honour it then, and only
    /// then - a file that also has Known keeps Known, whichever order the two are read in.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    [System.Text.Json.Serialization.JsonPropertyName("Identified")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    private bool? LegacyIdentified
    {
        get => null;
        set { if (value is bool b && !_knownRead) _known = b ? IdLevel.All : IdLevel.Barely; }
    }
    /// <summary>
    /// The original's <c>M572A</c> on the *slot*, which is a different thing from the item
    /// definition's cursed field and is what almost everything actually tests.
    ///
    /// Three routines set it and three clear it (see <c>uncurse</c> probe). Two of the setters do
    /// not equip the item: <c>sub0F01</c> drops chest treasure into a pack slot and copies the
    /// flag across (MODULE4.BAS:3825), and <c>modAwardCrest</c> does the same for a Guildmaster's
    /// Crest (FRMCTYG.BAS:745). So "flagged cursed" and "worn" can come apart, and the Seer is the
    /// one place that tests the flag without also testing the equipped bit.
    ///
    /// Not carried by saves written before this field existed; <see cref="Character.SyncSlotCurses"/>
    /// rebuilds it for equipped gear on load.
    /// </summary>
    public bool SlotCursed { get; set; }
    /// <summary>Which alignment this particular copy carries (the original's U, G, N or E).</summary>
    public ItemAlignment Alignment { get; set; } = ItemAlignment.Unaligned;
}

/// <summary>A charmed monster that fights at the character's side.</summary>
public sealed class Companion
{
    /// <summary>
    /// <c>Ref</c>, the creature's index in the monster table (<see cref="Monster.Ref"/>): what the
    /// original looks the companion up by everywhere (<c>Monsters(Companions(i).Ref)</c>) and pens
    /// it under when it is sold (FRMCITYC.BAS:288-291). fn1163 sets it from the foe's Ref
    /// (MAINMOD2.BAS:388), cmdBuy_Click from the row bought (FRMCITYC.BAS:128). -1 here is a record
    /// saved before the port kept it, settled from <see cref="MonsterId"/> on load
    /// (<see cref="CompanionRef.Settle"/>).
    /// </summary>
    public int Ref { get; set; } = -1;

    /// <summary>
    /// <c>M57D4</c>, the creature's Id, kept beside <see cref="Ref"/> (MAINMOD2.BAS:389,
    /// FRMCITYC.BAS:129). The original reads it only to put a Ref right that no longer names a
    /// creature of this Id (the COMPANION SORTER, MAINMOD2.BAS:4720-4727). Ids are not unique.
    /// </summary>
    public short MonsterId { get; set; }
    public string Name { get; set; } = "";
    public int Hits { get; set; }
    public int MaxHits { get; set; }

    /// <summary>The companion's own alignment, M4EBF - what the Confinement charges to change.</summary>
    public ItemAlignment Alignment { get; set; }

    /// <summary>
    /// How well you know this creature, M4EEE: 0 completely, higher the less. The Confinement's
    /// ID button takes one off, and an unidentified companion sells for a fraction.
    /// </summary>
    public int IdLevel { get; set; }

    /// <summary>
    /// How tightly the companion is bound to you (the original's M57E1). It starts at
    /// <c>5 + monster floor</c>, capped at 10 (FRMCITYC.BAS:136-138), and the Confinement's Bind
    /// service raises it 4 at a time to a maximum of <see cref="Binding.Max"/>.
    /// </summary>
    public int BindingLevel { get; set; }

    /// <summary>
    /// Fallen: still in its slot with no hits, waiting to be carried, raised or let go
    /// (<c>mdrcharGetCarry</c> floors it at zero, MAINMOD2.BAS:2034-2035). It does not fight,
    /// follow, or take the monsters' blows.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDead => Hits <= 0;

    /// <summary>
    /// The port's save marker for an empty slot's record: the slot's monster index is -1 and this is
    /// only what the last occupant left in it, name and hits (MAINMOD2.BAS:383-400, 4745). Written
    /// only for such a record (<see cref="CompanionSlotsJson"/>); never set on a companion in play.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Vacant { get; set; }
}

/// <summary>
/// Binding a companion, from FRMCITYC.BAS - the Confinement's Bind service.
/// </summary>
public static class Binding
{
    /// <summary>mc006C: the highest binding the Confinement will sell.</summary>
    public const int Max = 12;

    /// <summary>mc006E: how much one payment buys.</summary>
    public const int Step = 4;

    /// <summary>
    /// gc1D78. A companion bound for life never abandons you (fn0D78 returns a flat zero chance)
    /// and shrugs off the Fear chest trap. The Confinement cannot sell it - it stops at
    /// <see cref="Max"/> - but the spell with effect 150 sets it outright (MAINMOD2.BAS:876).
    /// </summary>
    public const int ForLife = 100;

    /// <summary>What a companion's binding would become after one more payment.</summary>
    public static int NextLevel(int current) =>
        Math.Abs(current) + Step > Max ? Max : Math.Abs(current) + Step;

    /// <summary>
    /// fn33B9, bindCost (FRMCITYC.BAS:606-618), a Currency: <c>(monster guild level * 1237 * f *
    /// current) / 4</c>. The factor f is <b>not</b> the level it binds to: with a = |current| it is
    /// <c>a + 4</c>, or <c>12 - a</c> when that would pass 12 - so the last steps get cheaper, 3 at
    /// a binding of 9 and 1 at 11 (:611-615). The guild level is the companion's monster's (:616).
    /// The current level is a factor too, so binding a companion that is not bound at all is free.
    /// Charged through charPay, which takes a Long - see <see cref="Charge"/>.
    /// </summary>
    public static decimal Cost(int monsterGuildLevel, int current)
    {
        int a = Math.Abs(current);
        int f = a + Step > Max ? Max - a : a + Step;
        return (decimal)monsterGuildLevel * 1237m * f * current / 4m;
    }

    /// <summary>The Currency passed to <c>charPay (p0196 As Long)</c>: rounded half to even.</summary>
    public static long Charge(decimal cost) => (long)Math.Round(cost, MidpointRounding.ToEven);

    /// <summary>What a companion starts at when you charm it: fn at FRMCITYC.BAS:136-138.</summary>
    public static int Initial(int monsterFloor) => Math.Min(5 + monsterFloor, 10);

    /// <summary>
    /// fn0D92, the wording the original shows. The adverb comes from the chance of the companion
    /// leaving, not from the binding level itself, so a strong monster can read badly even when
    /// you have paid for it.
    /// </summary>
    public static string Describe(int level, double chanceToLeave)
    {
        if (level == ForLife) return "Bound for Life";
        string strength = chanceToLeave switch
        {
            0 => "Completely", <= 3 => "Excellently", <= 10 => "Fairly",
            <= 15 => "Marginally", <= 20 => "Weakly", _ => "Very Weakly",
        };
        return level < 0 ? strength + " content" : strength + " bound";
    }

    /// <summary>
    /// fn0D78: the chance, in percent, that the companion abandons you. A binding of
    /// <see cref="ForLife"/> makes it zero; otherwise it falls with how tightly bound it is and
    /// with your own guild level, and rises with the monster's and with how hurt it is.
    /// </summary>
    public static double ChanceToLeave(int bindingLevel, int monsterGuildLevel, int topGuildLevel,
                                       int charisma, int hits, int maxHits)
    {
        if (bindingLevel == ForLife) return 0;
        double bound = Math.Round(Math.Abs(bindingLevel) * 4.5, MidpointRounding.ToEven);   // l0376 is an Integer (MONSMOD.BAS:2058)
        double mon = Math.Log(monsterGuildLevel + 1) * 6;
        double x = mon * 4 - (bound * 2 + Math.Log(topGuildLevel / 10.0 + 1) * 4);
        x -= charisma / 2.0;
        x = x / 200 * 100;
        x += (maxHits - hits) / (double)maxHits * 15;
        x /= 2;
        return Math.Clamp(x, 0, 90);
    }
}

/// <summary>A player character. Persisted as JSON for now (native save format, not MDATA4 yet).</summary>
public sealed class Character : System.Text.Json.Serialization.IJsonOnDeserialized
{
    public string Name { get; set; } = "";
    public string Race { get; set; } = "Human";
    public Alignment Alignment { get; set; } = Alignment.Neutral;
    public Sex Sex { get; set; } = Sex.Male;

    public int[] Stats { get; set; } = new int[6];          // STR,INT,WIS,CON,CHA,DEX (current)
    public int[] StatMax { get; set; } = new int[6];        // per-race caps

    public int MaxHits { get; set; }
    public int CurHits { get; set; }
    public int MaxSpellPoints { get; set; }
    public int CurSpellPoints { get; set; }

    // ---- ailments from monster special attacks ----
    /// <summary>Loses hits every step in the dungeon until cured (Cure Poison, or resting in town).</summary>
    public bool Poisoned { get; set; }
    /// <summary>Healing only half works until cured (Cure Disease, or resting in town).</summary>
    public bool Diseased { get; set; }
    /// <summary>Turned to stone: as good as dead until the Morgue restores you.</summary>
    public bool Stoned { get; set; }
    /// <summary>Poison/disease strength = the spawn level of the monster that caused it; higher takes longer to wear off.</summary>
    public int PoisonLevel { get; set; }
    public int DiseaseLevel { get; set; }
    /// <summary>Steps taken while poisoned or diseased, used to let it wear off.</summary>
    public int AilmentSteps { get; set; }

    /// <summary>
    /// <c>Chars(x).Ailments(2)</c>: paralysis, held as the guild level of the monster that did it
    /// (MONSMOD.BAS:3286) and saved with the character, as the original's record is. Nought is
    /// free. While it is set the stat part of Att/Def is missing - see <see cref="Paralyse"/> - and
    /// the character cannot swing, walk, climb, open a chest or use the pack's buttons.
    /// </summary>
    public int ParalysisLevel { get; set; }

    /// <summary>"Ailments(2) &lt;&gt; False": the display's and most gates' test.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Paralysed => ParalysisLevel != 0;

    /// <summary>
    /// What the original's stored <c>Att</c>/<c>Def</c> hold beyond base + best guild + gear + the
    /// stat part the port works out from the current stats. The original keeps Att and Def as
    /// running totals and moves them by what <c>mdrcharSetAD</c> measures (MAINMOD2.BAS:4455-4547),
    /// which changes Att and Def by the same amounts, so one number covers both. It is nought until
    /// <c>sub0D1D</c> takes the stat part away (MONSMOD.BAS:3897-3898) and stays wrong for good
    /// wherever the original never gives the same amount back:
    /// <list type="bullet">
    /// <item>the city gate zeroes Ailments(2) (FRMCTYM.BAS:115) before sub048C (130) could undo it;</item>
    /// <item>a guild-level-0 monster's paralysis takes the stats off and stores level 0 (3899), so
    ///       nothing ever undoes it, and the next such blow takes them off again;</item>
    /// <item>stats or gear that change while held: the cure gives back the stat part as it is then
    ///       (3901-3902), not what was taken.</item>
    /// </list>
    /// Saved, as the original's record is; a save without it is read by <see cref="StoredADShift"/>.
    /// </summary>
    public int ADShift { get; set; }

    /// <summary>
    /// A save from before <see cref="ADShift"/> holding a paralysed character: its shift is still to be
    /// worked out, from the stats as <see cref="StoredADShift"/> next finds them. Saved itself, so a
    /// save written before that happens - or a city gate that has cleared the paralysis since - keeps it.
    /// </summary>
    public bool ADShiftUnsettled { get; set; }

    /// <summary>
    /// The stat part of Att/Def as <c>mdrcharSetAD</c> measures it (MAINMOD2.BAS:4465-4504): the
    /// Str, Int, Wis and Dex terms of stat plus StatMods - the same inputs combat reads.
    /// </summary>
    public int StatsADNow(Func<short, Item?> item) =>
        Rules.StatsAD(Enumerable.Range(0, 6).Select(i => EffectiveStat(i, item)).ToArray());

    /// <summary>
    /// <see cref="ADShift"/>, first settling a save written before it existed. Those saves had the
    /// stat part missing while held and nothing else, which is a shift of minus the stats now.
    /// </summary>
    public int StoredADShift(Func<short, Item?> item)
    {
        if (ADShiftUnsettled) { ADShiftUnsettled = false; ADShift = -StatsADNow(item); }
        return ADShift;
    }

    /// <summary>
    /// <c>sub0D1D ch, 0, level</c> (MONSMOD.BAS:3884-3906). It leaves at once if the character is
    /// already held (3887), otherwise measures the stat part of Att/Def with mdrcharSetAD mode 0 and
    /// back (3889-3894), takes it off both (3897-3898) and stores the level (3899). A level of
    /// nought (a monster of guild level 0) stores nothing but still takes the stats off - kept.
    /// </summary>
    public void Paralyse(int level, Func<short, Item?> item)
    {
        if (ParalysisLevel != 0) return;   // "If p05B0 = 0 And Ailments(mc0122) <> False Then Exit Sub"
        ADShift = StoredADShift(item) - StatsADNow(item);
        ParalysisLevel = level;
    }

    /// <summary>
    /// <c>sub0D1D ch, 1, 0</c> (MONSMOD.BAS:3884-3906): nothing if not held (3888), otherwise the
    /// stat part as it stands now goes back on (3901-3902) and Ailments(2) = 0 (3903).
    /// </summary>
    public void Unparalyse(Func<short, Item?> item)
    {
        if (ParalysisLevel == 0) return;   // "If p05B0 = 1 And Ailments(mc0122) = False Then Exit Sub"
        ADShift = StoredADShift(item) + StatsADNow(item);
        ParalysisLevel = 0;
    }

    /// <summary>
    /// Cure Poison, and the poison half of the Cure heal: <c>Ailments(0) = False</c>
    /// (MAINMOD2.BAS:975, 989). The original has one field, the level, so the flag and the level
    /// go together - the port had cleared the flag alone, and a left-over level stops a monster
    /// poisoning the character again until the city (MonsterAttack tests PoisonLevel = 0).
    /// </summary>
    public void CurePoison() { Poisoned = false; PoisonLevel = 0; }

    /// <summary>Cure Disease, and the other half of Cure: <c>Ailments(1) = False</c> (976, 996).</summary>
    public void CureDisease() { Diseased = false; DiseaseLevel = 0; }

    /// <summary>
    /// <c>sub048C</c> (MAINMOD2.BAS:4359-4373), bar its paralysis and its recompute, which the
    /// callers do: Ailments(0), (1) and the spell resistances all back to nought. It runs at the
    /// city gate (FRMCTYM.BAS:130) and at every death (sub0930, MAINMOD2.BAS:3033). The four
    /// states it also zeroes (4365-4368) come straight back from <c>recomputeEffects</c> at 4372
    /// while the spell mask holds them - which at a death it still does - so they are left to the
    /// caller's <see cref="Effects.Recompute"/>.
    /// </summary>
    public void EndAilmentsAndSpellResists()
    {
        CurePoison();
        CureDisease();
        for (int i = 0; i < SpellResist.Length; i++) SpellResist[i] = 0;
    }

    /// <summary>
    /// <c>sub048C</c> (MAINMOD2.BAS:4359-4373) in full: paralysis undone through sub0D1D mode 1
    /// when held (4361), Ailments(0) to (6) zeroed - poison, disease, paralysis, and the four
    /// spell-held states (4362-4368), the spell resistances (4369-4371), then recomputeEffects
    /// (4372). The indices are MAINMOD2.BAS:51-65 (m01FC 0, mc01FE 1, mc01EC 2, mc0206 3,
    /// mc020C 4, mc0210 5, mc0214 6), the seven gv07E2$ names (<see cref="CharacterStates"/>).
    /// Stone is not one of them - it is DeathState 2, what the body died of - and stays. States
    /// 3-6 the port derives from the spell mask and the gear in <see cref="Effects.Recompute"/>,
    /// which is where the original's recompute puts them back from too. Idempotent: a second run
    /// finds nothing to undo.
    /// </summary>
    public void EndEveryAilment(Func<short, Item?> item)
    {
        Unparalyse(item);
        EndAilmentsAndSpellResists();
        Effects.Recompute(this, item);
    }

    /// <summary>
    /// The head of <c>sub0930</c> (MAINMOD2.BAS:3026-3033), which every run of it does before
    /// anything else, dead or not, alone or in a party: the companions let go and the carry put
    /// down (3026-3030, <see cref="ReleaseFollowers"/>), then <see cref="EndEveryAilment"/>
    /// (sub048C, 3033). The one death routine: a death the original follows with sub0930 comes
    /// through here, and one it does not (<c>mdrcharSetDead</c> alone - a companion's mishap,
    /// MONSMOD.BAS:901-1305; the chest trap, 3654; the list's UPDATE, FRMGROUP.BAS:326-335;
    /// sub0908, MULTIMOD.BAS:57-61) leaves the body as it lay until the next sub0930 on it - the
    /// load of a dead character (sub14E0, MAINMOD2.BAS:3181-3182) or Wait for Rescue
    /// (FRMDUNM.BAS:194).
    /// </summary>
    public void Sub0930Head(Func<short, Item?> item)
    {
        ReleaseFollowers();
        EndEveryAilment(item);
    }

    public string AilmentText => string.Join(" ", new[] { Poisoned ? "POISONED" : "", Diseased ? "DISEASED" : "", Stoned ? "STONE" : "" }.Where(s => s != ""));

    // ---- what spells are holding up (the original's M591F and M592F) ----
    /// <summary>
    /// Effect bits granted by spells currently running. Spells use their own bit positions, not
    /// the ones items use - see <see cref="Effects.SpellLevitate"/>. A Blackout trap or an
    /// antimagic square clears this.
    /// </summary>
    public int SpellEffects { get; set; }

    /// <summary>Resistance percentages a spell has raised, one per kind; cleared with the effects.</summary>
    public int[] SpellResist { get; set; } = new int[Effects.ResistanceKinds];

    /// <summary>
    /// The Combat Setting from Party Options - what this character falls back to when a fight
    /// starts. The original keeps it on the character rather than with the party, as
    /// <c>M5914(5)</c>, so it is saved and comes back with them (FRMPINFO.BAS:51-59).
    ///
    /// Its encoding: 0 Nothing, 1 Fight, 2 Defend (optOption_Click writes the option's index,
    /// FRMPINFO.BAS:51-53), and 3 or more the buffer picked in cmbBuffer, <c>ListIndex + 3</c>
    /// (FRMPINFO.BAS:15-16) - Buffer n is <c>n + 2</c>, the ten slots bound to the number keys.
    /// Party Options writes all of these; <see cref="CombatOrder"/> reads a buffer's back out.
    /// </summary>
    public int CombatSetting { get; set; }

    /// <summary>
    /// The ten spell buffers, <c>M59B0</c>. See <see cref="SpellBuffers"/> for what the numbers
    /// mean; a fresh character has all ten empty (CHARCREA.BAS:254).
    /// </summary>
    public int[] Buffers { get; set; } = Enumerable.Repeat(SpellBuffers.Empty, SpellBuffers.Count).ToArray();

    // ---- carrying a body out (the original's M5943) --------------------------------------------
    // One field there: -1 nothing, 0 to 3 one of your own companions, 4 or more a character, that
    // many places into the morgue list. Split in two here because the port addresses characters by
    // name rather than by a row in a file.

    /// <summary>Which of your companions you are carrying, or -1.</summary>
    public int CarryingCompanion { get; set; } = -1;

    /// <summary>The fallen character you are carrying out, or "".</summary>
    public string CarryingCharacter { get; set; } = "";

    /// <summary>
    /// The part of <c>sub0930</c> that a death does to what travels with you
    /// (MAINMOD2.BAS:3026-3030): every companion slot let go, and <c>M5943 = -1</c> - which puts
    /// down a carried body from the morgue list as well as a carried companion.
    /// </summary>
    public void ReleaseFollowers()
    {
        Companions.ReleaseAll();
        CarryingCompanion = -1;
        CarryingCharacter = "";
    }

    /// <summary>You can carry one thing at a time, and not while raising the dead.</summary>
    public bool IsCarrying => CarryingCompanion >= 0 || CarryingCharacter != "";

    /// <summary>
    /// <c>fn128B</c> (MODULE6.BAS:2105-2111): what a companion is called anywhere the game names
    /// one. A companion with no name of its own is "Companion #N", counting from one; a named one
    /// is its name in quotes. It is never called by its breed - every message in MONSMOD and
    /// MAINMOD2 goes through this function.
    /// </summary>
    public string CompanionName(int index) =>
        index < 0 || index >= CompanionSlots.Slots ? "" :
        // fn128B reads the record's name without asking whether the slot is occupied: an empty
        // slot answers with what its last occupant left, until mdrcharShowCompanions blanks it
        // for the character on screen (MAINMOD2.BAS:4745)
        Companions.Left(index).Name.Trim() == "" ? $"Companion #{index + 1}"
                                                 : $"\"{Companions.Left(index).Name.Trim()}\"";

    /// <summary>
    /// What the original's Carry line says, <c>mdrcharShowCharry</c> (MAINMOD2.BAS:4681-4686):
    /// "Carrying " and the slot's name, for any slot number below four - it does not ask whether
    /// the slot still holds anybody.
    /// </summary>
    public string CarryText =>
        CarryingCharacter != "" ? $"Carrying \"{CarryingCharacter}\"."
        : CarryingCompanion >= 0 && CarryingCompanion < CompanionSlots.Slots
            ? $"Carrying {CompanionName(CarryingCompanion)}."
            : "";

    // ---- the four visible states, recomputed by Effects.Recompute (the original's sub0894) ----
    /// <summary>gv07E2$(3) "Levitating": you float over pits and quicksand.</summary>
    public bool Levitating { get; set; }
    /// <summary>gv07E2$(4) "Invisible".</summary>
    public bool InvisibleNow { get; set; }
    /// <summary>gv07E2$(5) "See Invisible".</summary>
    public bool SeeingInvisible { get; set; }
    /// <summary>gv07E2$(6) "Protected", carrying its strength rather than just on or off.</summary>
    public int Protection { get; set; }

    // ---- being lost: M59F4, three separate flags (MODULE3.BAS:118) ----

    /// <summary>M59F4(0): you do not know which floor you are on.</summary>
    public bool LostLevel { get; set; }
    /// <summary>M59F4(1): you do not know where you are, so the automap stops recording.</summary>
    public bool LostPosition { get; set; }
    /// <summary>M59F4(2): you do not know which way you are facing.</summary>
    public bool LostFacing { get; set; }

    /// <summary>
    /// How this character died, <c>M5483</c>. It decides what can bring them back: see
    /// <see cref="Rules.RaiseRefusal"/>. Cleared on a successful raise, as MODULE6.BAS:359 does.
    /// </summary>
    public int CauseOfDeath { get; set; }

    /// <summary>
    /// <c>M58E9</c>: the chance of a raise somebody else has cast on this body and is waiting for the
    /// dead character's answer, or 0 for none. Written by <c>sub0A7C</c> (MAINMOD2.BAS:5985) and
    /// offered by <c>fn16B5</c> when the character is next run; see <see cref="PendingRaise"/>.
    /// </summary>
    public int RaiseChance { get; set; }

    /// <summary><c>M58F9</c>: who cast that raise (MAINMOD2.BAS:5986).</summary>
    public string RaisedBy { get; set; } = "";

    /// <summary><c>M58DC</c>: which raise it is - 20 to 22 for the spells, 23 for the morgue (MAINMOD2.BAS:5987).</summary>
    public int RaiseKind { get; set; }

    /// <summary>Whether anything about your whereabouts is unknown. The recovery rolls hang off this.</summary>
    public bool IsLost => LostPosition || LostLevel;

    /// <summary>All three at once - what a chute or a teleport into unmapped ground does.</summary>
    public void BecomeLost() { LostLevel = LostPosition = LostFacing = true; }

    /// <summary>The morgue clears these, and so does standing somewhere you have already mapped.</summary>
    public void FoundYourself() { LostLevel = LostPosition = LostFacing = false; }


    /// <summary>Recompute spell points from caster guild levels; call after creation, level-up, or joining a guild.</summary>
    public void RecalcSpellPoints(bool refill = false)
    {
        MaxSpellPoints = Spellbook.MaxSpellPoints(this);
        if (refill) CurSpellPoints = MaxSpellPoints;
        else CurSpellPoints = Math.Min(CurSpellPoints, MaxSpellPoints);
    }
    /// <summary>
    /// <c>Gold</c> and <c>BankGold</c> (MAIN.TXT:332, 335) are Currency, so they keep four places
    /// of a fraction. Every way the game pays gold in is a whole number - a share is <c>CLng</c>'d,
    /// a sale price is a Long - except the Bank's text boxes, which take <c>CCur</c> of whatever
    /// is typed: deposit "10.5" and both balances carry the half from then on (docs/FIDELITY.md).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public VbCurrency GoldOnHand { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public VbCurrency GoldInBank { get; set; }

    // On disk the two keep the integer fields they always had, so a save from before the fraction
    // loads unchanged and a build from before it still reads a new save (the whole part). Only a
    // balance with a fraction writes its exact figure beside it; see VbCurrency.ExactForSave.
    [System.Text.Json.Serialization.JsonConverter(typeof(SavedWholeJson))]
    [System.Text.Json.Serialization.JsonPropertyName("GoldOnHand")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public long GoldOnHandWhole { get => GoldOnHand.Whole; set => GoldOnHand = VbCurrency.Saturate(value); }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Text.Json.Serialization.JsonConverter(typeof(SavedExactJson))]
    public decimal? GoldOnHandExact { get => GoldOnHand.ExactForSave; set => _handExact = value; }
    [System.Text.Json.Serialization.JsonConverter(typeof(SavedWholeJson))]
    [System.Text.Json.Serialization.JsonPropertyName("GoldInBank")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public long GoldInBankWhole { get => GoldInBank.Whole; set => GoldInBank = VbCurrency.Saturate(value); }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Text.Json.Serialization.JsonConverter(typeof(SavedExactJson))]
    public decimal? GoldInBankExact { get => GoldInBank.ExactForSave; set => _bankExact = value; }
    private decimal? _handExact, _bankExact;

    /// <summary>The exact figures, read in whatever order the file has them, joined to the whole parts.</summary>
    void System.Text.Json.Serialization.IJsonOnDeserialized.OnDeserialized()
    {
        // with no exact figure the whole part (already held to the range) stands as read
        if (_handExact != null) GoldOnHand = VbCurrency.FromSave(GoldOnHand.Whole, _handExact);
        if (_bankExact != null) GoldInBank = VbCurrency.FromSave(GoldInBank.Whole, _bankExact);
        _handExact = _bankExact = null;
    }

    /// <summary>
    /// Everything you could spend right now, purse and bank together: <c>Gold + BankGold</c>.
    /// Held to Currency's range rather than raising Overflow, so a purse that a negative
    /// withdrawal has pushed to the edge can still be compared and shown: against any charge or
    /// amount inside the range the held figure answers as the true sum would.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public VbCurrency Purse => VbCurrency.AddSaturating(GoldOnHand, GoldInBank);

    /// <summary>
    /// <c>charPay</c> (MAINMOD2.BAS:693-708), the one way the town charges. The help, "The City:
    /// Store": "With all options in the city, gold will be removed from your bank account if you
    /// do not have enough on your character's person, but do in your account." Returns false, and
    /// takes nothing, if you cannot afford it.
    /// <para>
    /// The charge is a Long. When the bank makes up the difference, <c>p0196 = p0196 - Gold</c>
    /// puts a Currency back into that Long, so the bank's part is rounded half to even: 100 owed
    /// with 50.5 on hand takes 50 from the bank (49.5 rounds up), and an account of 49.5 is left at
    /// -0.5. With whole balances nothing rounds.
    /// </para>
    /// <para>
    /// charPay has no error handler, and neither have most of its callers, so an Overflow in it
    /// (a balance at the edge of Currency's range) would end the original program before anything
    /// was saved. The port refuses the charge instead and moves nothing.
    /// </para>
    /// </summary>
    public bool Pay(long cost)
    {
        var c = VbCurrency.Saturate(cost);
        if (c > GoldOnHand)
        {
            if (c > Purse) return false;
            if (!VbCurrency.TrySubtract(c, GoldOnHand, out var owed)) return false;
            long rest = owed.RoundToWhole();   // 699, into the Long
            if (!VbCurrency.TrySubtract(GoldInBank, VbCurrency.Saturate(rest), out var bank)) return false;
            GoldOnHand = 0;
            GoldInBank = bank;
            return true;
        }
        if (!VbCurrency.TrySubtract(GoldOnHand, c, out var hand)) return false;
        GoldOnHand = hand;
        return true;
    }

    /// <summary>
    /// Gold the town pays this character (a sale to the Store or the Confinement): into the hand,
    /// the original's, or into the bank when a mod says so (ModDecisions.TownIncomeBank). False,
    /// and nothing moved, when the sum would overflow Currency - the original's Overflow there ends
    /// the program unsaved (FRMCTYS.BAS:240, FRMCITYC.BAS:284), so the port refuses the sale.
    /// </summary>
    public bool Receive(VbCurrency amount, bool toBank)
    {
        if (!VbCurrency.TryAdd(toBank ? GoldInBank : GoldOnHand, amount, out var sum)) return false;
        if (toBank) GoldInBank = sum; else GoldOnHand = sum;
        return true;
    }

    /// <summary>Whether <see cref="Receive"/> would take <paramref name="amount"/> (no overflow).</summary>
    public bool CanReceive(VbCurrency amount, bool toBank) => VbCurrency.TryAdd(toBank ? GoldInBank : GoldOnHand, amount, out _);

    /// <summary>Age in days. CHARCREA.BAS starts a character at <c>16 * 365</c>.</summary>
    public int Age { get; set; } = 16 * 365;
    public bool IsDead { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    /// <summary><c>M5906(2)</c>: raises that went wrong, counted by raiseComplications (MAINMOD2.BAS:1654).</summary>
    public int Complications { get; set; }
    /// <summary><c>M5906(3)</c>: monster quests finished, counted by sub0CF3 (MAINMOD2.BAS:1629).</summary>
    public int MonsterQuestsDone { get; set; }
    /// <summary><c>M5906(4)</c>: quest items handed over at the guild (FRMCTYG.BAS:265).</summary>
    public int ItemQuestsDone { get; set; }
    /// <summary><c>M5906(5)</c>: seconds played, a Currency the play clock (sub16F8) adds to.</summary>
    public double PlaySeconds { get; set; }
    /// <summary><c>M5906(6)</c>: <c>CLng(Date)</c> at creation (CHARCREA.BAS:248), a date serial.</summary>
    public double CreatedOn { get; set; }

    public List<GuildMembership> Guilds { get; set; } = new();

    /// <summary>
    /// The pack, <c>M5857</c>: a fixed array of forty slots. Slot 0 is the character's own
    /// <b>Hands</b> - MDATA3's item 0, no attack, no defence, two hands wide - which is why the
    /// Objects &amp; Spells window opens with "1. Hands" and why Drop and Give both start their scan
    /// at 1. Every other slot is either an item or blank, and a blank slot keeps its number: the
    /// original's own list runs "7. Potion, 8., 9., 10." with the empty ones still numbered.
    ///
    /// That fixedness is not cosmetic. Swap Items takes two slot numbers up to 40 and exchanges
    /// them, spell buffers point at slots by number, and the game's messages name them ("BOX: Slot
    /// #7, Potion"). The port kept a list that closed up when something left, so none of those
    /// could be right.
    /// </summary>
    public InventorySlot?[] Pack
    {
        get => _pack;
        set => _pack = Resize(value);
    }
    private InventorySlot?[] _pack = FreshPack();

    /// <summary>How many slots a pack has, <c>mc0190</c>. Slot 0 of them is your hands.</summary>
    public const int PackSize = 40;

    /// <summary>Item 0 in MDATA3 is called "Hands"; it sits in slot 0 and never moves.</summary>
    public const short HandsItemId = 0;

    private static InventorySlot?[] FreshPack()
    {
        var p = new InventorySlot?[PackSize];
        p[0] = new InventorySlot { ItemId = HandsItemId, Known = IdLevel.All };
        return p;
    }

    private static InventorySlot?[] Resize(InventorySlot?[]? given)
    {
        var p = new InventorySlot?[PackSize];
        if (given != null)
            for (int i = 0; i < PackSize && i < given.Length; i++) p[i] = given[i];
        p[0] ??= new InventorySlot { ItemId = HandsItemId, Known = IdLevel.All };
        return p;
    }

    /// <summary>
    /// Whether that slot is free. Slot 0 never is - it holds your hands, and item id 0 means
    /// "empty" everywhere else (<c>If M5857(p).M4ED4 &lt; 1 And p &gt; 0</c>, MAINMOD2.BAS:1723).
    /// </summary>
    public bool SlotEmpty(int i) => i > 0 && i < PackSize && (_pack[i] == null || _pack[i]!.ItemId == 0);

    /// <summary>The first free slot, or -1 - <c>For l04A4 = 1 To mc0190 - 1</c> (MAINMOD2.BAS:2941).</summary>
    public int FirstFreeSlot()
    {
        for (int i = 1; i < PackSize; i++) if (SlotEmpty(i)) return i;
        return -1;
    }

    /// <summary>What is carried, in slot order, hands and blanks left out. Read-only by design:
    /// everything that changes the pack goes through <see cref="PutInPack"/> or
    /// <see cref="RemoveFromPack(InventorySlot)"/> so the slot numbers stay put.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<InventorySlot> Inventory
    {
        get
        {
            var live = new List<InventorySlot>();
            for (int i = 1; i < PackSize; i++) if (!SlotEmpty(i)) live.Add(_pack[i]!);
            return live;
        }
    }

    /// <summary>Which slot this is in, or -1.</summary>
    public int SlotOf(InventorySlot slot)
    {
        for (int i = 0; i < PackSize; i++) if (ReferenceEquals(_pack[i], slot)) return i;
        return -1;
    }

    /// <summary>Put it in the first free slot; false if the pack is full.</summary>
    public bool PutInPack(InventorySlot slot)
    {
        int i = FirstFreeSlot();
        if (i < 0) return false;
        _pack[i] = slot;
        return true;
    }

    /// <summary>Put it in a named slot, whatever was there.</summary>
    public void PutInSlot(int i, InventorySlot? slot)
    {
        if (i > 0 && i < PackSize) _pack[i] = slot;
    }

    /// <summary>Empty every slot but your hands.</summary>
    public void ClearPack()
    {
        for (int i = 1; i < PackSize; i++) _pack[i] = null;
    }

    /// <summary>
    /// Saves written before the pack had fixed slots held a plain list. Read it into slots 1
    /// upwards, which is where those items would have been anyway.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("Inventory")]
    public List<InventorySlot>? LegacyInventory { get; set; }

    internal void MigrateLegacyPack()
    {
        if (LegacyInventory == null) return;
        if (LegacyInventory.Count > 0 && Inventory.Count == 0)
        {
            int i = 1;
            foreach (var slot in LegacyInventory)
            {
                if (i >= PackSize) break;
                _pack[i++] = slot;
            }
        }
        LegacyInventory = null;
    }

    /// <summary>
    /// What the Bank is holding for you, <c>M5860</c> - forty slots, separate from the pack, and
    /// safe from anything that happens below ground (FRMCITYB.BAS:168-199).
    /// </summary>
    public List<InventorySlot> Bank { get; set; } = new();

    /// <summary>mc00A2: how many items the Bank will hold.</summary>
    public const int BankSlots = 40;

    /// <summary>
    /// lstItems_DragDrop (FRMCITYB.BAS:187-199): the pack slot goes into the first vault slot that holds
    /// nothing - a hole left by a withdrawal counts - and the pack slot empties. False when all forty
    /// are full.
    /// </summary>
    /// <remarks>
    /// An empty pack slot goes through the same steps (the drag starts on any slot,
    /// FRMCHAR3.BAS:590-594): with a hole free, the blank record lands in it and the hole is still
    /// a hole, so nothing changes and this answers true; with all forty full it is refused like
    /// anything else (FRMCITYB.BAS:186-196).
    /// </remarks>
    public bool DepositToVault(int packSlot)
    {
        if (packSlot <= 0 || packSlot >= PackSize) return false;
        int hole = Bank.FindIndex(b => b.ItemId <= 0);
        if (hole < 0 && Bank.Count >= BankSlots) return false;
        if (Pack[packSlot] is not InventorySlot sl || sl.ItemId == 0) return true;
        if (hole >= 0) Bank[hole] = sl; else Bank.Add(sl);
        PutInSlot(packSlot, null);
        return true;
    }

    /// <summary>
    /// The character's password, <c>M594C</c>, kept in the character record exactly as typed. The
    /// original stores it in the clear and so does this - it guards one player's character from
    /// another at the same keyboard, not anything worth encrypting.
    ///
    /// Declining a password stores a **single space** (CHARCREA.BAS:170), which is how "no password"
    /// is represented: every check asks <c>Len(RTrim(...))</c> first, and a lone space trims to
    /// nothing, so no prompt ever appears.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// The four Character Options from the Control Panel, <c>M5914</c>. They live on the
    /// character, so they are saved with them - see <see cref="CharacterOptions"/>.
    /// </summary>
    public CharacterOptions Options { get; set; } = new();

    /// <summary>
    /// M5958: where each of the game's windows was left, kept on the character so that the layout
    /// follows whoever is playing. The original stores them on the party leader alone - FRMAUTOM
    /// and the Control Panel both write slot 0 of the character array, not the character with the
    /// focus - so only the leader's copy is ever read.
    ///
    /// See <see cref="WindowLayout"/> for the table of defaults these are seeded from.
    /// </summary>
    public WindowRect[] Windows { get; set; } = WindowLayout.Fresh();

    /// <summary>
    /// Which way of laying out <see cref="Windows"/> they were written under - see
    /// <see cref="WindowLayout.Revision"/>. A save from before the field existed reads as 0. Nothing
    /// is decided by it any more: it is kept so that the saves that carry it still load.
    /// </summary>
    public int WindowsRevision { get; set; }

    /// <summary>A window's placement, or an empty one if the array has been trimmed by a save.</summary>
    public WindowRect WindowFor(GameWindow which) => NormaliseWindows()[(int)which];

    /// <summary>
    /// The table made whole: a save that wrote fewer entries than <see cref="WindowLayout.Count"/>
    /// (or none) is grown to the full count, and a null entry becomes an empty placement, so
    /// anything that walks it by index - the seeding, the layout - never falls off the end.
    /// </summary>
    public WindowRect[] NormaliseWindows()
    {
        var w = Windows ?? System.Array.Empty<WindowRect>();
        if (w.Length < WindowLayout.Count)
        {
            var grown = WindowLayout.Fresh();
            for (int j = 0; j < w.Length; j++) grown[j] = w[j] ?? new WindowRect();
            w = grown;
        }
        for (int j = 0; j < w.Length; j++) w[j] ??= new WindowRect();
        return Windows = w;
    }

    /// <summary>What the original writes when you answer "no password".</summary>
    public const string NoPassword = " ";

    /// <summary>
    /// Whether this character asks for a password at all - <c>If Len(RTrim(l00DA.M594C))</c>
    /// (FRMMENU.BAS:221).
    /// </summary>
    public bool HasPassword => Password.TrimEnd().Length > 0;

    /// <summary>
    /// Does this match? <c>If p00A6 &lt;&gt; RTrim(gv14C2(pv00A8).M594C)</c> (MAINMOD.BAS:52) - the
    /// stored value is right-trimmed and the comparison is **case sensitive**, unlike the character
    /// name lookup just above it, which upper-cases both sides.
    /// </summary>
    public bool PasswordMatches(string entered) => entered == Password.TrimEnd();


    /// <summary>Charmed monsters travelling with you (kept in the Confinement when in town).</summary>
    public CompanionSlots Companions { get; set; } = new();
    /// <summary>Four, flat. MAINMOD2.BAS keeps a fixed array of four companion slots (mc00E0).</summary>
    public const int MaxCompanionSlots = 4;
    public int MaxCompanions => MaxCompanionSlots;

    // dungeon position (0 = in town)
    public int DungeonLevel { get; set; } = 0;
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>
    /// <c>Facing</c>, part of the character record beside X and Y (MAIN.TXT:329) and so written
    /// with it by every fn04ED: 1 North, 2 East, 3 South, 4 West (gv06E8, MODULE1.BAS:543-546).
    /// A new character faces North (CHARCREA.BAS:227), and so does one that walks down from town
    /// (FRMCTYM.BAS:208-211); sub0956 gives every member the leader's (MULTIMOD.BAS:723). A save
    /// from before the port kept it reads as North.
    /// </summary>
    public int Facing { get; set; } = 1;

    /// <summary>
    /// <c>M59BB</c>, the area the character is in as the record keeps it: -1 in town (CHARCREA.BAS:242,
    /// FRMCTYM.BAS:112, FRMDUNM.BAS:188, 279), the area of the leader's square while in the party
    /// (MAZEMOD.BAS:822-824, MULTIMOD.BAS:722), and nought for a death in rock below ground
    /// (MAINMOD2.BAS:1185, 4571-4573, 5883-5885) whatever the square's area is. A body moved by Fetch
    /// takes the mover area (MAINMOD2.BAS:1323), as does one put down by sub0A7C (5992). Null in a save from
    /// before it was kept; readers then work it out from the square, as the port used to.
    /// </summary>
    public int? Area { get; set; }

    // ---- where Set Sanctuary put your marker (the original's M59E7) ----
    // Kept 1-based, as the original keeps map coordinates, so that zero means "never set".
    public int SanctuaryX { get; set; }
    public int SanctuaryY { get; set; }
    public int SanctuaryLevel { get; set; }

    /// <summary>
    /// Set Sanctuary has been cast somewhere, so Sanctuary has a place to return to. The original
    /// refuses when *either* coordinate is zero (MAINMOD2.BAS:1339).
    /// </summary>
    public bool HasSanctuary => SanctuaryX != 0 && SanctuaryY != 0;

    /// <summary>Automap: explored cell indices (y * width + x) per dungeon level, persisted with the character.</summary>
    public Dictionary<int, List<int>> Explored { get; set; } = new();

    public int AgeYears => Age / 365;   // the original divides by 365 to display it

    /// <summary>The bare title, <c>fn12C4</c> - "Rover", with no level after it.</summary>
    public string GuildTitle => GuildTitles.Of(Guild.Guild, Guild.Level);

    /// <summary>The title as the character sheet shows it, with the level in brackets.</summary>
    public string Title => $"{GuildTitle} ({Guild.Level})";

    /// <summary>
    /// Resting in the city, as the help describes it: "Upon returning to the city from the dungeon,
    /// you will rest and heal yourself (aging in the process). This process will restore your hits,
    /// replenish spell points, and cure any poison or disease. It will also heal any companions."
    /// How much you age is <c>mdrcharAddAge</c>'s rest case, below. Returns the days spent, or 0.
    /// </summary>
    public int RestInCity()
    {
        // FRMCTYM.BAS:111 - whatever spells were holding up end at the city gate, for everyone; the
        // caller recomputes what that leaves (Effects.Recompute). The port had carried them over.
        SpellEffects = 0;
        Area = -1;   // "Chars(gv1A84).M59BB = -1" (FRMCTYM.BAS:112, again at 132)
        // FRMCTYM.BAS:115 zeroes Ailments(2) for everybody, dead or alive, and sub048C (130) then
        // finds nothing to undo - its "If Ailments(mc01EC) <> False Then sub0D1D" (MAINMOD2.BAS:4361)
        // is never true here, so the original's stored Att/Def are left without their stat part -
        // for good. Only the flag goes; ADShift keeps what sub0D1D took (MONSMOD.BAS:3897-3898).
        ParalysisLevel = 0;
        // FRMCTYM.BAS:113-114 and sub048C (130) for everybody, dead or alive: poison and disease,
        // and the resistances a spell raised (MAINMOD2.BAS:4362-4371). The port had kept those.
        EndAilmentsAndSpellResists();
        // the block runs for every member whatever their state (107-134), the dead included:
        // mdrcharAddAge's rest case (MAINMOD2.BAS:6355-6360) never looks at DeathState, so a body
        // at 0 hits ages 31 days, "rested for 4 week(s) and 3 day(s)", and is left dead with its
        // hits full, and line 110 fills its spell points. As member 0 it also turns the store and
        // the pens over (6384-6389). The port had skipped the dead altogether.

        // mdrcharAddAge (MAINMOD2.BAS): resting costs days in proportion to how hurt you were,
        //     l0896 = ((100 - (Hits / MaxHits * 100)) / 3.25#)
        //     Hits = MaxHits
        //     Age = Age + l0896
        // so arriving untouched costs nothing and arriving at death's door costs about 30 days.
        // l0896 is Dim As Integer, so the Single lands in it rounded half to even, not truncated:
        // at 0 hits 100 / 3.25 = 30.77 becomes 31, and at a tenth left 27.69 becomes 28
        int days = MaxHits > 0 && CurHits < MaxHits
            ? (int)Math.Round((100 - ((double)CurHits / MaxHits * 100)) / 3.25, MidpointRounding.ToEven)
            : 0;
        Age += days;
        CurHits = MaxHits;
        RecalcSpellPoints(refill: true);
        AilmentSteps = 0;
        // FRMCTYM.BAS:113 heals only companions still on their feet - a dead one stays dead
        foreach (var c in Companions.All) if (c.Hits > 0) c.Hits = c.MaxHits;
        return days;
    }

    /// <summary>
    /// Format a span of days the way mdrcharAddAge prints it. Note it tests <c>days &gt; 7</c>,
    /// so exactly seven days prints as "7 day(s)" rather than "1 week(s)".
    /// </summary>
    public static string DescribeDays(int days)
    {
        int weeks = days > 7 ? days / 7 : 0;
        int rest = days - weeks * 7;
        if (weeks > 0 && rest > 0) return $"{weeks} week(s) and {rest} day(s)";
        if (weeks > 0) return $"{weeks} week(s)";
        return $"{rest} day(s)";
    }

    /// <summary>
    /// <c>mdrcharAddAge</c> when it is not a rest (MAINMOD2.BAS:6362-6365 and 6400-6405): the days
    /// go on, and the line is "You are now X older." alone or "\"Name\" is now X older." in a party,
    /// X worded as <see cref="DescribeDays"/> words it. Nothing is said when X would be nought
    /// (6375-6377). Returns the line, or "" for none. Its callers are the original's p088C = True
    /// ones: Sanctuary (1352), the raises (363, 1663, 1680, 3109), a Kind 25 item (6228), monster
    /// aging (MONSMOD.BAS:3129) and chest Withering (MONSMOD.BAS:3633).
    /// </summary>
    public string AgeBy(int days, bool inAParty)
    {
        Age += days;
        if (days <= 0) return "";   // l0898 = 0 And l0896 = 0 (a negative l0896 is folded to 0 at 6373)
        return inAParty ? $"\"{Name.TrimEnd()}\" is now {DescribeDays(days)} older."
                        : $"You are now {DescribeDays(days)} older.";
    }

    /// <summary>
    /// Monster id → times killed (0 = met but never killed). The port's own tally, kept in saves;
    /// the Library no longer reads it - its records are written as the original writes them
    /// (<see cref="Librarian.FileSighting"/>).
    /// </summary>
    public Dictionary<int, int> MonstersSeen { get; set; } = new();
    /// <summary>Library: item ids this character has identified or bought.</summary>
    public HashSet<int> ItemsSeen { get; set; } = new();

    public void NoteMonster(int id, bool killed)
    {
        MonstersSeen.TryGetValue(id, out int n);
        MonstersSeen[id] = n + (killed ? 1 : 0);
    }

    public int TotalLevel => Guilds.Sum(g => g.Level);

    /// <summary>The highest level across every guild, which is fn1373 in MAINMOD2.BAS.</summary>
    public int TopGuildLevel => Guilds.Count == 0 ? 0 : Guilds.Max(g => g.Level);
    public string PrimaryGuild => Guilds.Count > 0 ? Guilds[0].Guild : "Nomad";
    public GuildMembership Guild => Guilds[0];

    public int GetStat(Stat s) => Stats[(int)s];

    /// <summary>Stat with equipped gear modifiers applied (Girdle of Strength +2, cursed Lead Axe −4…); never below 1.</summary>
    /// <summary>
    /// A stat as equipped gear leaves it. An item of the wrong alignment does not simply stop
    /// helping: one step away is worth half, two steps away is worth its modifier *against* you
    /// (MAINMOD2.BAS:1846-1853).
    /// </summary>
    public int EffectiveStat(int i, Func<short, Item?> item)
    {
        int v = Stats[i];
        foreach (var s in Inventory.Where(s => s.Equipped))
            if (item(s.ItemId) is Item it && i < it.StatsMod.Length)
                v += ItemAlignments.StatModWorth(it.StatsMod[i], ItemAlignments.FitFor(s.Alignment, Alignment));
        return Math.Max(1, v);
    }


    /// <summary>
    /// Take an item out of the pack, and put the spell buffers right afterwards. Every removal goes
    /// through here rather than touching <see cref="Inventory"/> directly, because a buffer holding
    /// a pack index has to be told - see <see cref="SpellBuffers.PackItemRemoved"/>.
    /// </summary>
    public bool RemoveFromPack(InventorySlot slot)
    {
        int i = SlotOf(slot);
        if (i <= 0) return false;
        _pack[i] = null;
        SpellBuffers.PackSlotEmptied(this, i);
        return true;
    }

    /// <summary>The same, by position in the carried list rather than by slot.</summary>
    public bool RemoveFromPackAt(int index)
    {
        var live = Inventory;
        if (index < 0 || index >= live.Count) return false;
        return RemoveFromPack(live[index]);
    }

    /// <summary>
    /// Swap Items, <c>fn1521</c>'s middle case (MAINMOD2.BAS:3415-3437). It takes two slot
    /// numbers as the player types them - one-based - refuses slot 1 because that is your hands,
    /// refuses anything past forty, and exchanges whatever is in them, empty slots included.
    ///
    /// The buffers follow their items only on a dragged swap: lblItem_DragDrop remaps M59B0
    /// (FRMCHAR3.BAS:571-577). The typed 's' swap exchanges the two Pack records and redraws the
    /// buffers without remapping them (MAINMOD2.BAS:3432-3442), so a buffer that named slot 12
    /// names slot 12 still, whatever is there now - <paramref name="buffersFollow"/> false.
    /// </summary>
    public bool SwapSlots(int oneBasedA, int oneBasedB, bool buffersFollow = true)
    {
        if (oneBasedA <= 1 || oneBasedA > PackSize) return false;
        if (oneBasedB <= 1 || oneBasedB > PackSize) return false;
        int a = oneBasedA - 1, b = oneBasedB - 1;
        (_pack[a], _pack[b]) = (_pack[b], _pack[a]);
        if (buffersFollow) SpellBuffers.PackSlotsSwapped(this, a, b);
        return true;
    }


    /// <summary>
    /// Which of the vaguer item classes this item belongs to - <c>gv1592(type).M54D5</c>, the word
    /// after each item type's name in MDATA1. A Cap and a Helmet are both Head Protection; a Dagger
    /// and an Axe are both Weapons.
    /// </summary>
    public static int WearClassOf(Item it) =>
        it.Type >= 0 && it.Type < Mdata1.ItemTypes.Count ? Mdata1.ItemTypes[it.Type].Index : -1;

    /// <summary>
    /// How many of that class may be worn at once - <c>gv15C6(class).M54F1</c>. Three values turn
    /// up in MDATA1: 1 for the fifteen classes you wear, 0 for the eight consumables, and -1 for
    /// Miscellaneous Object. <c>fn11B6</c> only consults the number when it is above zero
    /// (<c>If gv15C6(l0162).M54F1 &gt; False</c>, MODULE6.BAS:576), so both 0 and -1 mean
    /// "as many as you like" to it.
    /// </summary>
    public static int MaxWornOfClass(int wearClass) =>
        wearClass >= 0 && wearClass < Mdata1.ItemDescriptions.Count ? Mdata1.ItemDescriptions[wearClass].Index : 0;

    /// <summary>Hands already occupied by what is equipped - the original's running <c>M5700</c>.</summary>
    public int HandsUsed(Func<short, Item?> item) =>
        Inventory.Where(s => s.Equipped).Select(s => item(s.ItemId))
                 .Where(i => i != null).Sum(i => (int)i!.NHands);

    /// <summary>How many of that wear class are on already - the original's running <c>M586D()</c>.</summary>
    public int WornOfClass(int wearClass, Func<short, Item?> item) =>
        Inventory.Where(s => s.Equipped).Select(s => item(s.ItemId))
                 .Count(i => i != null && WearClassOf(i) == wearClass);

    /// <summary>
    /// Why this item cannot be equipped, or null if it can - <c>fn11B6</c>, MODULE6.BAS:566-650.
    ///
    /// Pass <paramref name="item"/> to get the two physical checks the original asks *before* it
    /// looks at any guild: are there hands free, and are you already wearing as many of this kind as
    /// you may. They correspond to the function's fourth argument <c>p0160</c>, which every caller
    /// but one sets, turning them off: the store, the guild hall and the character editor only want
    /// to know whether this character could ever use the thing, so they skip them, and only
    /// <c>sub0F74</c>'s equip path (MODULE6.BAS:1776) passes zero and enforces them. Leaving the
    /// argument out here means the same thing.
    /// </summary>
    public string? EquipProblem(Item it, Func<short, Item?>? item = null)
    {
        if (item != null)
        {
            // Two hands, no more. A two-handed sword and a shield will not go on together.
            if (HandsUsed(item) + it.NHands > 2) return "No Free Hands!";

            int cls = WearClassOf(it);
            int max = MaxWornOfClass(cls);
            if (max > 0 && WornOfClass(cls, item) >= max) return "Using too Many!";
        }

        // then the guild, then the guild *level*, then the stats, saying which of the three stopped you
        if (!GuildData.CanUse(it, this))
            return it.ClassRestricted != 0
                ? "Your current guild cannot use this item!"
                : "Your currently joined guilds cannot use this item!";

        if (!MeetsUseLevel(it))
            return it.ClassRestricted != 0
                ? "Your guild level is too low to use item!"
                : "All guild levels too low to use item!";

        // MODULE6.BAS:645 says no more than this - it will not tell you which stat, and the port
        // used to add that out of helpfulness. The original is blunt, so this is blunt.
        for (int i = 0; i < 6 && i < it.StatsRequired.Length; i++)
            if (Stats[i] < it.StatsRequired[i]) return "One or more stats are too low!";
        return null;
    }


    /// <summary>
    /// Whether this is a thing one wears at all. <c>sub0F74</c> looks the item's class maximum up
    /// before anything else and returns when it is exactly zero (MODULE6.BAS:1724-1726), so a potion
    /// or a scroll cannot be equipped and the game says nothing about why.
    ///
    /// The test really is <c>= 0</c> and not <c>&lt; 1</c>, which matters: Miscellaneous Object
    /// carries -1, so the seven items in that class can be worn, and any number of them at once,
    /// because <c>fn11B6</c>'s <c>&gt; False</c> then declines to cap them. Nothing else in MDATA1
    /// is negative, so that one class is the whole of the difference - and it is a difference the
    /// original clearly did not plan for, since 0 and -1 mean opposite things to the two routines.
    /// </summary>
    public static bool IsWearable(Item it) => MaxWornOfClass(WearClassOf(it)) != 0;

    /// <summary>
    /// A stat with everything equipped counted in, before the floor of 1 - the original's
    /// <c>M55B5(stat) + M584B(stat)</c>, a base and a running total of modifiers.
    /// </summary>
    public int StatWithMods(int i, Func<short, Item?> item)
    {
        int v = Stats[i];
        foreach (var s in Inventory.Where(s => s.Equipped))
            if (item(s.ItemId) is Item it && i < it.StatsMod.Length)
                v += ItemAlignments.StatModWorth(it.StatsMod[i], ItemAlignments.FitFor(s.Alignment, Alignment));
        return v;
    }

    /// <summary>
    /// Why putting this particular copy on fails, or null if it goes on. This is the equip branch of
    /// <c>sub0F74</c> (MODULE6.BAS:1774-1816): it runs <c>fn11B6</c> with the physical limits turned
    /// on, then asks one more question that <c>fn11B6</c> never does - would wearing it push a stat
    /// below your race's minimum? A Dwarf whose Charisma is already at the floor cannot put on a
    /// ring that takes another point off it. The modifier counted here is the one the item is
    /// actually worth to you, so a half-strength item is half as likely to be refused.
    ///
    /// Callers should check <see cref="IsWearable"/> first and refuse silently if it is false.
    /// </summary>
    public string? WearRefusal(InventorySlot slot, Item it, Func<short, Item?> item)
    {
        if (EquipProblem(it, item) is string why) return why;

        var fit = ItemAlignments.FitFor(slot.Alignment, Alignment);
        var min = RaceDef.Min;
        for (int i = 0; i < 6 && i < it.StatsMod.Length && i < min.Length; i++)
            if (StatWithMods(i, item) + ItemAlignments.StatModWorth(it.StatsMod[i], fit) < min[i])
                // MODULE6.BAS:1809-1813 - a cursed item gets its own wording, because it is the
                // curse rather than your arm that failed to take hold
                return it.Cursed == 2
                    ? "Your stats are too low for the item to properly attach!"
                    : "You are too weak to equip this item!";
        return null;
    }


    /// <summary>
    /// Whether any guild the character belongs to has reached the level this item needs. The level
    /// is <c>fn030F</c> in MODULE1.BAS - <c>(factor + factor * 2.97) * item.LevelScale</c>, at least
    /// one - where the factor is the guild's own. A class-restricted item only looks at the guild
    /// you are currently in; anything else will take the best of the guilds you have joined.
    /// </summary>
    public bool MeetsUseLevel(Item it)
    {
        if (it.ClassRestricted != 0)
        {
            var cur = Guild;
            return cur.Level >= UseLevel(it, GuildData.ByName(cur.Guild));
        }
        foreach (var m in Guilds)
        {
            if (m.Level <= 0) continue;
            var g = GuildData.ByName(m.Guild);
            int idx = GuildData.IndexOf(m.Guild);
            if (idx >= 0 && (it.Guilds & (1 << idx)) == 0) continue;
            if (m.Level >= UseLevel(it, g)) return true;
        }
        return false;
    }

    /// <summary>The guild level one particular guild needs before it may use this item.</summary>
    public static int UseLevel(Item it, GuildDef g)
    {
        double f = g.ItemUseFactor;
        // fn030F assigns a Single expression to an Integer: VB rounds (banker's), it does not truncate
        return Math.Max(1, (int)Math.Round((f + f * 2.97) * it.LevelScale, MidpointRounding.ToEven));
    }


    /// <summary>Does this worn item actually give its attack, defence and stat bonuses?</summary>
    public bool ItemHelps(InventorySlot slot) => ItemAlignments.FitFor(slot.Alignment, Alignment) == ItemFit.Fine;

    /// <summary>
    /// Can this equipped item be taken off? <c>sub0F74</c> asks one question and it is about the
    /// slot, not the item: <c>If slot.M572A &lt;&gt; False And l0302 = False</c> (MODULE6.BAS:1728),
    /// where <c>l0302</c> is the force flag the Seer sets by passing a negative slot index. An
    /// opposed-alignment item needs no separate test here because equipping one sets the same slot
    /// flag (MODULE6.BAS:1821).
    /// </summary>
    public static bool CanUnequip(InventorySlot slot, Item? it, Alignment who) => !slot.SlotCursed;

    public bool CanUnequip(InventorySlot slot, Item? it) => CanUnequip(slot, it, Alignment);

    /// <summary>
    /// What equipping this item does to the slot's curse flag, <c>sub0F74</c>'s two setters:
    /// MODULE6.BAS:1786 copies the item definition's flag across (and only when it is set, so it
    /// cannot clear one), and MODULE6.BAS:1821 sets it outright when the item's alignment opposes
    /// yours. Call after a successful equip.
    /// </summary>
    public void ApplyEquipCurse(InventorySlot slot, Item? it)
    {
        if (it != null && it.Cursed != 0) slot.SlotCursed = true;
        if (ItemAlignments.FitFor(slot.Alignment, Alignment) == ItemFit.Opposed) slot.SlotCursed = true;
    }

    /// <summary>
    /// Rebuild <see cref="InventorySlot.SlotCursed"/> for gear that is already on, for saves
    /// written before the flag existed. Only equipped slots are touched: an unequipped one is
    /// legitimately clear unless it came from a chest, and a save cannot say which did.
    /// </summary>
    public void SyncSlotCurses(Func<short, Item?> lookup)
    {
        foreach (var s in Inventory.Where(s => s.Equipped)) ApplyEquipCurse(s, lookup(s.ItemId));
    }

    /// <summary>Stats raised by the last LevelUp, for the message.</summary>
    public List<string> LastLevelGains { get; } = new();

    // ---- Progression ----

    /// <summary>This character's race definition, for the things that come from the race table.</summary>
    public Race RaceDef => RaceData.ByName(Race);

    public GuildDef ActiveGuildDef => GuildData.ByName(Guild.Guild);
    /// <summary><c>fn12A1</c> (MAINMOD2.BAS:2118-2138) for the current guild, ported with its Currency roundings.</summary>
    public long ExpForNextLevel() => Rules.Fn12A1(Guild.Level, ActiveGuildDef.ExpFactor, RaceDef.ExpFactor);

    /// <summary>
    /// The experience the level the character holds in the current guild asked for: <c>fn12A1</c>
    /// at the level below, the same rule as <see cref="ExpForNextLevel"/> one step back (nought at
    /// level 1, where the loop does not run). Nothing in the original reads it; a mod drawing
    /// progress from this level to the next does.
    /// </summary>
    public long ExpForThisLevel() => Rules.Fn12A1(Guild.Level - 1, ActiveGuildDef.ExpFactor, RaceDef.ExpFactor);

    /// <summary><c>fn12B2</c> (MAINMOD2.BAS:2140-2155) for the current guild: the gold the guildmaster charges for the next level.</summary>
    public long GoldForNextLevel() => Rules.Fn12B2(Guild.Level, ActiveGuildDef.GoldFactor);

    public bool CanLevelUp() => Guild.Level < ActiveGuildDef.MaxLevel && Guild.Experience >= ExpForNextLevel();

    /// <summary>
    /// Making a level, <c>cmdMake_Click</c> (FRMCTYG.BAS:347-375). The hits depend on whether this
    /// guild is taking you past every other guild you hold: if some other guild already stands at
    /// this level you gain **nothing**, which is what stops a second guild paying twice for ground
    /// you have already covered. Otherwise, below the guild's own ML you get its AH flat, plus - two
    /// times in three - a slice of 1 to AH/5, plus, if your CON is over 16, another 1 to CON-16.
    /// Past ML you get exactly MH, with no roll and no constitution bonus. Maximum hits stop at 999,
    /// and making a level fills you up.
    /// </summary>
    public int LevelUp(Random rng)
    {
        int highestOther = Guilds.Where(g => g != Guild).Select(g => g.Level).DefaultIfEmpty(0).Max();
        Guild.Level++;
        var def = ActiveGuildDef;
        bool aheadOfEveryOtherGuild = Guild.Level > highestOther;
        int before = MaxHits;

        if (Guild.Level > def.HitsCapLevel || !aheadOfEveryOtherGuild)
        {
            // past ML: exactly MH, and nothing at all if another guild has already been here
            if (aheadOfEveryOtherGuild) MaxHits += def.LateHits;
        }
        else
        {
            if (rng.Next(3) + 1 <= 2) MaxHits += (int)(rng.NextDouble() * def.AvgHits / 5) + 1;
            MaxHits += def.AvgHits;
            if (Stats[(int)Stat.Con] > 16 && Guild.Level < def.HitsCapLevel)
                MaxHits += (int)(rng.NextDouble() * (Stats[(int)Stat.Con] - 16)) + 1;
        }
        if (MaxHits > 999) MaxHits = 999;
        CurHits = MaxHits;
        // Stats do NOT rise when you make a level. The only way to raise a natural stat is to find and
        // use an item that does it (a Tome, a Potion of Might and so on), as the help says.
        LastLevelGains.Clear();
        RecalcSpellPoints(refill: true);
        RecalcSwings();
        return MaxHits - before;
    }


    /// <summary>
    /// Extra swings earned from the Multiple Swings guild ability, <c>M56AF</c>
    /// (FRMCTYG.BAS:394-406, run at every level):
    /// <code>
    ///   mod = guild.Abilities(MultipleSwings)
    ///   If mod > 0 Then
    ///       threshold = Log((10 - mod) + 1) * 50
    ///       extra = 0
    ///       For level = 1 To guildLevel
    ///           If level > threshold Then
    ///               extra = extra + 1
    ///               threshold = Log((10 - mod) + 1) * (threshold * 2)
    ///           End If
    ///       Next
    ///       If extra > Swings Then Swings = extra
    ///   End If
    /// </code>
    /// The thresholds double each time, so swings come slowly and never go down - and the best of
    /// your guilds wins, because each one only raises the figure if it beats what you already had.
    /// </summary>
    public void RecalcSwings()
    {
        foreach (var m in Guilds)
        {
            if (m.Level <= 0) continue;
            double mod = GuildData.ByName(m.Guild).Abilities.ElementAtOrDefault(3);
            if (mod <= 0) continue;

            // FRMCTYG.BAS:184 declares the threshold `As Integer`, so each assignment rounds the
            // Single half to even. Keeping it as a real number puts every extra swing one or two
            // levels early: Ninja at 55 instead of 56, Warrior at 90 instead of 91.
            double step = Math.Log(10 - mod + 1);
            double threshold = Math.Round(step * 50, MidpointRounding.ToEven);
            int extra = 0;
            for (int level = 1; level <= m.Level; level++)
                if (level > threshold)
                {
                    extra++;
                    threshold = Math.Round(step * (threshold * 2), MidpointRounding.ToEven);
                }

            if (extra > Swings) Swings = extra;
        }
    }

    /// <summary>M56AF: extra swings a round, on top of whatever the weapon itself offers.</summary>
    public int Swings { get; set; }


    /// <summary>
    /// Roll the guild's quest after a level has been made, and describe it. FRMCTYG.BAS clears any
    /// existing quest first, so making a level always replaces what you were carrying.
    /// </summary>
    public List<string> RollQuestForLevel(IReadOnlyList<Monster> monsters, IReadOnlyList<Item> items, Random rng)
    {
        Guild.QuestType = 0; Guild.QuestTarget = 0; Guild.QuestDone = false;
        var (type, tier) = Rules.RollQuest(ActiveGuildDef, Guild.Level, rng);
        if (type == 0) return new();
        // the Messages lines, FRMCTYG.BAS:470-480 and the quest itself
        var said = new List<string> { "Questmaster is going through his books..." };
        if (type == -1) { said.Add("Questmaster lets you off the hook."); return said; }

        if (type == 1 && Rules.QuestMonster(monsters, tier, rng) is Monster m)
        {
            Guild.QuestType = 1; Guild.QuestTarget = m.Id;
            said.Add($"You must defeat one {m.Name}.");
            return said;
        }
        if (type == 2 && Rules.QuestItem(items, tier, rng) is Item it)
        {
            Guild.QuestType = 2; Guild.QuestTarget = it.Id;
            said.Add($"You must return one {it.Name}.");   // FRMCTYG.BAS:563
            return said;
        }
        return said;
    }

    /// <summary>
    /// Whether the active guild's quest is satisfied. A "defeat one" quest needs the kill recorded;
    /// a "bring one back" quest needs the item in the pack, and the guild takes it.
    /// </summary>
    public bool QuestSatisfied(bool consume)
    {
        if (Guild.QuestType == 0) return true;
        if (Guild.QuestType == 1) return Guild.QuestDone;

        // FRMCTYG.BAS:254-259 scans every pack slot by item id, worn or not - an equipped copy
        // satisfies the guild, and it is handed over all the same
        var slot = Inventory.FirstOrDefault(s => s.ItemId == Guild.QuestTarget);
        if (slot == null) return false;
        if (consume) { RemoveFromPack(slot); ItemQuestsDone++; }   // M5906(4), FRMCTYG.BAS:265
        return true;
    }

    // ---- forfeiting a quest (FRMCTYG.BAS:285-296 and sub29BD) ----

    /// <summary>
    /// The experience you can hold at your current level, <c>fn13ED</c> (MODULE6.BAS:2705): one
    /// short of what the level after next would need. Reach it and you stop gaining.
    /// </summary>
    public long ExperienceCap() => Rules.Fn13ED(Guild.Level, ActiveGuildDef.ExpFactor, RaceDef.ExpFactor);

    /// <summary>
    /// "Pinned": you have all the experience this level can hold and cannot earn another point
    /// until you make the level. Only a pinned character is offered the forfeit.
    /// </summary>
    public bool IsPinned => Guild.Experience >= ExperienceCap();

    /// <summary>
    /// What forfeiting the active guild's quest costs: <c>Int(Log(level + 1))</c> levels. Flat and
    /// deterministic - the original rolls no dice here.
    /// </summary>
    public int ForfeitCost() => (int)Math.Floor(Math.Log(Guild.Level + 1));

    /// <summary>
    /// Give up the quest. You drop <see cref="ForfeitCost"/> levels, the quest is cleared, and
    /// anything the lower level no longer lets you use comes off - <c>sub29BD</c> re-runs
    /// <c>fn11B6</c> over every worn slot with the hands and too-many checks suppressed, so what
    /// it is really re-testing is guild, level and stats. Returns what was unequipped.
    /// </summary>
    /// <summary>
    /// <c>sub29BD</c>'s first act (FRMCTYG.BAS:845-850): a cursed item you are wearing stops the
    /// forfeit outright. Returns the refusal, or null. (The original misspells it "Cnanot".)
    /// </summary>
    public string? ForfeitRefusal(Func<short, Item?> lookup)
    {
        foreach (var s in Inventory.Where(s => s.Equipped))
            if (s.SlotCursed)
                return "Your Aura is Tainted!\nCnanot forfeit quest until cursed items are removed.";
        return null;
    }

    public List<InventorySlot> ForfeitQuest(Func<short, Item?> lookup, Random rng)
    {
        int cost = ForfeitCost();
        int from = Guild.Level;
        var def = ActiveGuildDef;

        // FRMCTYG.BAS:862-885 - each lost level takes back the hits it gave, walking down from the
        // level you had. A level that was your highest at the time (no other guild at or above it)
        // was the one that paid hits: past the cap it paid LateHits (MH), so that comes off; at
        // or below the cap it paid AvgHits (AH), so that comes off plus, two times in three, a
        // random slice of up to a fifth of it. Never below 15.
        for (int lvl = from; lvl >= from - cost + 1; lvl--)
        {
            bool wasHighest = !Guilds.Any(g => g != Guild && g.Level >= lvl);
            if (lvl > def.HitsCapLevel || !wasHighest)
            {
                if (wasHighest) MaxHits -= def.LateHits;
            }
            else
            {
                if (rng.Next(3) + 1 is 1 or 2) MaxHits -= (int)(rng.NextDouble() * def.AvgHits / 5) - 1;
                MaxHits -= def.AvgHits;
            }
            if (MaxHits < 15) MaxHits = 15;
        }
        CurHits = MaxHits;

        Guild.Level = Math.Max(1, from - cost);
        Guild.QuestType = 0; Guild.QuestTarget = 0; Guild.QuestDone = false;

        var removed = new List<InventorySlot>();
        foreach (var slot in Inventory.Where(s => s.Equipped).ToList())
        {
            var it = lookup(slot.ItemId);
            if (it == null) continue;
            if (EquipProblem(it) != null) { slot.Equipped = false; removed.Add(slot); }
        }
        RecalcSpellPoints(refill: false);
        // FRMCTYG.BAS:919 overwrites M56AF with this guild's figure alone - a demotion can lower it
        Swings = SwingsFor(Guild);
        // FRMCTYG.BAS:942-944 - the experience is *assigned*, not adjusted: the level is dropped
        // by one, fn12A1 is asked what the next level costs from there, and that figure is stored.
        // The demoted character is put back on the floor of the level they fell to, so the banked
        // experience that made them pinned is gone and every lost level has to be earned again.
        Guild.Experience = Rules.Fn12A1(Guild.Level - 1, ActiveGuildDef.ExpFactor, RaceDef.ExpFactor);   // fn12A1 a level down (FRMCTYG.BAS:946-948)
        return removed;
    }

    /// <summary>The extra swings one guild membership alone would give (FRMCTYG.BAS:394-406).</summary>
    public int SwingsFor(GuildMembership m)
    {
        double mod = GuildData.ByName(m.Guild).Abilities.ElementAtOrDefault(3);
        if (mod <= 0 || m.Level <= 0) return 0;
        double step = Math.Log(10 - mod + 1);
        double threshold = Math.Round(step * 50, MidpointRounding.ToEven);
        int extra = 0;
        for (int level = 1; level <= m.Level; level++)
            if (level > threshold) { extra++; threshold = Math.Round(step * (threshold * 2), MidpointRounding.ToEven); }

        return extra;
    }

    /// <summary>Join a guild (must meet its requirements); it becomes the active guild.</summary>
    public bool JoinGuild(GuildDef g)
    {
        if (Guilds.Any(m => m.Guild == g.Name)) return SetActiveGuild(g.Name);
        if (g.Unmet(this).Count > 0) return false;
        Guilds.Insert(0, new GuildMembership { Guild = g.Name, Level = 1, Experience = 0 });
        RecalcSpellPoints(refill: true);
        return true;
    }

    /// <summary>Make an already-joined guild the active one (gains experience, levels up).</summary>
    public bool SetActiveGuild(string name)
    {
        int i = Guilds.FindIndex(m => m.Guild == name);
        if (i <= 0) return i == 0;
        var m = Guilds[i]; Guilds.RemoveAt(i); Guilds.Insert(0, m);
        return true;
    }

    // ---- Creation ----

    public static Character Create(string name, Race race, Alignment alignment, Sex sex, int[] stats)
    {
        var c = new Character
        {
            Name = name,
            Race = race.Name,
            Alignment = alignment,
            Sex = sex,
            Stats = (int[])stats.Clone(),
            StatMax = (int[])race.Max.Clone(),
            CreatedOn = DateTime.Today.ToOADate(),   // CHARCREA.BAS:248, CLng(Date)
        };
        c.Guilds.Add(new GuildMembership { Guild = GuildData.StartingGuild, Level = 1, Experience = 0 });
        // 15 hits flat, whatever the constitution: a screenshot of the real game shows 15/15 at CON 17,
        // and the help's tutorial character shows 15/15 at CON 11
        c.MaxHits = 15;
        c.CurHits = c.MaxHits;
        c.RecalcSpellPoints(refill: true);
        c.GoldOnHand = 1500;   // the real game starts you with 1,500 gold
        // CHARCREA.BAS:239 sets the Combat Setting to 1, which is Fight: a fresh character swings
        // on the first round of their first fight rather than standing there
        c.CombatSetting = 1;
        c.Area = -1;   // "Chars(m00C2).M59BB = -1" (CHARCREA.BAS:242)
        return c;
    }

    /// <summary>
    /// <see cref="Area"/> for a reader: the stored M59BB, or for a save from before it was kept the
    /// area of the square it stands on (-1 in town) - except a body in the rock, whose M59BB every
    /// rock death writes as nought below ground and -1 at level 0 (MAINMOD2.BAS:1185, 4573, 5885).
    /// </summary>
    public int AreaOr(Func<Character, int> fromSquare) =>
        Area ?? (IsDead && CauseOfDeath == Rules.DeathRock ? (DungeonLevel > 0 ? 0 : -1) : fromSquare(this));

    /// <summary>
    /// mdrcharSetDead's rock case (MAINMOD2.BAS:4569-4574), and the same lines before it at 1185
    /// and 5883-5885: below ground the area becomes nought, whatever the square's area is.
    /// </summary>
    public void AreaForRockDeath()
    {
        if (DungeonLevel <= 0) { DungeonLevel = 0; Area = -1; }
        else Area = 0;
    }

    // ---- Persistence ----

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>
    /// The port's own: the mods that were running when this was last saved, "id@version" each
    /// (docs/MODDING.md, Safeguards). Null - and so not written - when none were.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Mods { get; set; }

    /// <summary>
    /// <see cref="Mods"/> as it was read from the file, kept apart because the next save rewrites
    /// Mods - the game warns from this one when a save needs mods that are not running.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string>? ModsWhenLoaded { get; private set; }

    public void Save(string path)
    {
        Mods = Saves.ModTag?.Invoke();
        // a per-user saves folder (Saves.DirFor) is only named until something is first written there
        if (Path.GetDirectoryName(path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }

    /// <summary>
    /// The port keeps a character's race and guilds by name where MDATA4 keeps numbers, so a name
    /// that is in no MDATA1 table is a record the game cannot read. It is refused here, at load,
    /// and never later mid-play, where RaceData.ByName and GuildData.ByName would throw. The
    /// original's fn04CD makes any failed read a plain 0 (MAINMOD.BAS:349-351), and every caller of
    /// <see cref="Load"/> already treats an exception the same way, silently.
    /// </summary>
    private void CheckNames(string path)
    {
        if (RaceData.IndexOf(Race) < 0)
            throw new InvalidDataException($"{path}: race \"{Race}\" is not in MDATA1");
        foreach (var m in Guilds)
            if (GuildData.IndexOf(m.Guild) < 0)
                throw new InvalidDataException($"{path}: guild \"{m.Guild}\" is not in MDATA1");
    }

    /// <summary>
    /// The save as it lies on disk, with no check of its race or guild names and nothing derived:
    /// for the file-level tests that only ask whether a file is a character's record (Backups).
    /// Never for play - that is <see cref="Load"/>.
    /// </summary>
    public static Character ReadRaw(string path) =>
        JsonSerializer.Deserialize<Character>(File.ReadAllText(path)) ?? throw new InvalidDataException("Bad save file");

    public static Character Load(string path)
    {
        string json = File.ReadAllText(path);
        var c = JsonSerializer.Deserialize<Character>(json) ?? throw new InvalidDataException("Bad save file");
        c.CheckNames(path);
        c.ModsWhenLoaded = c.Mods;
        // a save from before ADShift, paralysed: its stat part was missing while held
        if (c.ParalysisLevel != 0 && !json.Contains("\"ADShift\"")) c.ADShiftUnsettled = true;
        c.MigrateLegacyPack();   // a save from before the pack had fixed slots held a plain list
        c.RecalcSwings();   // derived from guild levels; a save from before it existed has 0
        return c;
    }

    /// <summary>
    /// Every character saved in a folder. The saves directory also holds the shared automap, the
    /// dungeon state, the store's shelves and the party rosters, and all of those deserialize
    /// quite happily into a blank Character - so a file only counts if it comes back with a name.
    ///
    /// In record order: this is the port's <c>MorgueList</c>, which <c>mdrmazGetChars</c> fills by
    /// walking the character file's records (MAZEMOD.BAS:203-224), and every walk over it - who is in
    /// the room (MAZEMOD.BAS:278), frmInteract's lists (FRMINTER.BAS:352) - goes in that order.
    /// <see cref="Roster.Saved"/> keeps it; the port had taken the files as the folder gave them.
    /// </summary>
    public static List<Character> LoadAllFrom(string savesDir) =>
        Roster.Saved(savesDir).Select(p => p.Ch).ToList();

    public static string SafeFileName(string s) =>
        string.Concat(s.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
}
