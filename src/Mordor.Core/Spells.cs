namespace Mordor.Core;

/// <summary>
/// Spell schools (the Category field in MDATA2, <c>SpellDefs.Kind</c>) and who can cast what. Which
/// guild teaches which school comes from MDATA1's per-guild spell table and nothing else: the
/// original has no other (MODULE1.BAS:687-700), and a game without MDATA1 does not start
/// (MAINMOD.BAS:1193-1196). The port once carried a themed guild-to-school table as a fallback.
/// </summary>
public static class Spellbook
{
    public const int Fire = 0, Cold = 1, Electric = 2, Light = 3, Wounds = 4, Poison = 5, Death = 6,
        Charm = 7, Control = 8, Healing = 9, Movement = 10, Banish = 11, Dispel = 12, Resist = 13, Sight = 14, Opening = 15;

    /// <summary><c>mc0758</c>, the number of spell classes (MAINMOD2.BAS:253), one per gv07B0$ name.</summary>
    public const int ClassCount = 18;

    /// <summary>
    /// <c>sub1658</c> (MAINMOD2.BAS:5510-5534): the classes cmbClass offers, in class order - every
    /// class some joined guild has a rate for. None at all and the box holds "NO SPELLS", disabled,
    /// with an ItemData of nought, so the list below is still worked out for class 0.
    /// </summary>
    public static List<int> SpellClasses(Character c)
    {
        var kinds = new List<int>();
        for (int k = 0; k < ClassCount; k++)
        {
            foreach (var m in c.Guilds)
            {
                if (m.Level <= 0) continue;
                var def = GuildData.ByName(m.Guild);
                if (k < def.SpellRate.Length && def.SpellRate[k] > 0) { kinds.Add(k); break; }
            }
        }
        return kinds;
    }

    /// <summary>What cmbClass shows when <see cref="SpellClasses"/> is empty (MAINMOD2.BAS:5530).</summary>
    public const string NoSpellClasses = "NO SPELLS";

    /// <summary>What the first row says when the class has nothing for you yet (FRMCHAR3.BAS:684).</summary>
    public const string NoSpellsYet = "No Spells Yet";

    /// <summary>
    /// One row of the spell list, <c>m0038</c> (type T5B69): the spell, the guild it is listed
    /// under (M5493), the level that guild learns it at (Level) and the points (M556D).
    /// </summary>
    public sealed record SpellRow(Spell Spell, int SpellIndex, int Guild, int Level, int Points)
    {
        /// <summary>lblGuild: the first three letters of the guild's name (FRMCHAR3.BAS:693).</summary>
        public string GuildText => GuildData.All[Guild].Name.Length > 3 ? GuildData.All[Guild].Name[..3] : GuildData.All[Guild].Name;

        /// <summary>lblPoints: "***" past 999 (FRMCHAR3.BAS:694-698).</summary>
        public string PointsText => Points > 999 ? "***" : Points.ToString();
    }

    /// <summary>
    /// <c>cmbClass_Click</c> (FRMCHAR3.BAS:55-140): the spells of one class this character has
    /// reached, in MDATA2 order. For each spell of the class it walks the joined guilds, keeping the
    /// lowest rate seen so far (l007C, a Single, back to nought after every guild until one lists
    /// the spell); a guild lists it when the spell is within its ceiling and
    /// <c>Level * rate &lt;= guildLevel / 2 + .5</c>. The guild shown is the one with the highest
    /// <c>guildLevel / 2 + .5</c> (l0088, an Integer), the level is <c>Int(Level * rate)</c>, and
    /// the points are fn1485's. Nothing is filtered by whether it can be thrown here and now.
    /// </summary>
    public static List<SpellRow> ClassSpells(Character c, IReadOnlyList<Spell> all, int kind)
    {
        var rows = new List<SpellRow>();
        int bestGuild = 0;                                          // l008A, a procedure-level Dim
        for (int s = 0; s < all.Count; s++)                         // 83
        {
            var sp = all[s];
            if (sp.Category != kind) continue;                      // 84
            int points = 0;                                         // l0080
            float rate = 0;                                         // l007C
            int bestSl = 0;                                         // l0088
            bool listed = false;                                    // l0086
            SpellRow? row = null;
            for (int g = 0; g < GuildData.All.Count; g++)           // 89
            {
                var def = GuildData.All[g];
                var m = c.Guilds.FirstOrDefault(x => x.Guild == def.Name);
                if (m == null || m.Level <= 0) continue;            // 90
                if (kind < def.SpellMax.Length && sp.Level <= def.SpellMax[kind])   // 91
                {
                    float r = kind < def.SpellRate.Length ? def.SpellRate[kind] : 0;
                    if (rate == 0) rate = r;                        // 92-94
                    if (r <= rate)                                  // 95
                    {
                        rate = r;
                        double half = m.Level / 2.0 + 0.5;
                        float product = (float)(sp.Level * rate);   // Integer * Single is a Single
                        if (product <= half)                        // 97
                        {
                            if (half > bestSl)                      // 98-101
                            {
                                bestGuild = g;
                                bestSl = (int)Math.Round(half, MidpointRounding.ToEven);
                            }
                            listed = true;                          // 102-107
                            int level = (int)Math.Floor(product);   // 110
                            if (rate == 0) rate = 1;                // 113-115
                            row = new SpellRow(sp, s, bestGuild, level, 0);  // 108-111
                            int cost = Cost(sp, c);                 // fn1485
                            if (cost <= points || points == 0)      // 116-119
                            {
                                points = cost;
                                row = row with { Points = points };
                            }
                        }
                    }
                }
                if (!listed) rate = 0;                              // 122
            }
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// Whether sub2424 sets a row bold: <c>mdrcharCheckCast(Me, spell, False, True)</c>
    /// (FRMCHAR3.BAS:699), whose last argument returns before the points and the combat tests
    /// (MAINMOD2.BAS:498-501) - so bold means the guilds, the spell level and the stats allow it.
    /// "Bold spells indicate that your character has the stats to cast the spells" (help, The Game
    /// Screen). The call's side effect, <c>Att = False</c> (MAINMOD2.BAS:433), is not here: it is
    /// <see cref="CombatOrder.CheckCastClearsAtt"/>, which the dungeon runs where the original lists.
    /// </summary>
    public static bool ShownBold(Character c, Spell s, Func<short, Item?> item) =>
        CastGate.Unfit(c, s, 0, true, item) == null;

    public static bool IsHealing(Spell s) => s.Category == Healing && s.Damage2 > 0;

    // ---- charm spells (category 7): "Charm Insect", "Control Animal", "Dominate Demon", "Soul Entrapment" ----
    public static bool IsCharm(Spell s) => s.Category == Charm;


    // cure spells by MDATA2 id: 19 Cure Poison, 33 Cure Disease, 88 Cure Paralysis, 57 Restore Flesh
    /// <summary>
    /// The cures. These used to be matched by spell id; the id was a guess, the effect is not -
    /// MDATA2 gives Cure Poison effect 1, Cure Disease 2, Cure Paralysis 10 and Restore Flesh 21.
    /// </summary>
    // Restore Flesh (effect 21) is NOT a cure. Stone is a death (sub091E with cause 2), and the
    // only way back is a raise that passes fn142D and its roll - see Rules.RaiseRefusal.
    public static bool IsCure(Spell s) => SpellEffect.Of(s)
        is SpellEffect.CurePoison or SpellEffect.CureDisease or SpellEffect.CureParalysis;

    public static bool IsCureParalysis(Spell s) => SpellEffect.Of(s) == SpellEffect.CureParalysis;

    /// <summary>
    /// A spell that does something other than damage, healing, curing or charming: the four states
    /// it can put up, the resistances, Fate, and the ones that need the dungeon around you.
    /// </summary>
    public static bool IsUtility(Spell s)
    {
        int e = SpellEffect.Of(s);
        return SpellEffect.IsState(e) || SpellEffect.ResistKind(e) >= 0 || IsBinding(s) || OpensLockUpTo(s) > 0
            || SpellEffect.IsRaise(e) || e is SpellEffect.Teleport or SpellEffect.Displacement
            || e is SpellEffect.SoulSearch or SpellEffect.RetrieveSoul
            || e is SpellEffect.Fate or SpellEffect.SetSanctuary or SpellEffect.Sanctuary
                 or SpellEffect.Passwall or SpellEffect.DetectRock
                 or SpellEffect.DepthPerception or SpellEffect.FindDirection;
    }

    /// <summary>Control, Restrain, Domination and Life Domination: they need a companion to aim at.</summary>
    public static bool IsBinding(Spell s)
    {
        int e = SpellEffect.Of(s);
        return (e >= SpellEffect.BindFirst && e <= SpellEffect.BindLast) || e == SpellEffect.BindForLife;
    }

    /// <summary>A spell that picks a locked chest, and the highest lock it can manage.</summary>
    public static int OpensLockUpTo(Spell s) => SpellEffect.OpensLockUpTo(SpellEffect.Of(s));

    public static bool IsOffensive(Spell s) => s.Damage2 > 0 && s.Category != Healing && s.Category != Resist && s.Category != Movement && s.Category != Sight && s.Category != Opening && s.Category != Control && s.Category != Charm;
    /// <summary>Affects every monster in the group (large AffectGroup values) rather than one.</summary>
    public static bool IsGroup(Spell s) => s.AffectGroup >= 6;

    /// <summary>
    /// Cost for a specific caster. <c>fn1485</c>, MAINMOD2.BAS:2868-2903, which prices the spell in
    /// *every* guild the character belongs to and keeps the cheapest:
    ///
    ///     If SpellDefs(s).Level &lt;= GuildDefs(g).M55F6(Kind, 1) And Chars(c).GuildsJoined(g).Level &gt; 0 Then
    ///         l048A = Int((GuildsJoined(g).Level / 2) + .5#)          ' SL, clamped to 1..255
    ///         l0488 = GuildDefs(g).M55F6(Kind, 0)                     ' the rate, 0 treated as 1
    ///         l0484 = SpellDefs(s).Level * l0488                      ' Integer, so this rounds
    ///         If Int(((l0484 / l048A) * SpellDefs(s).M556D) * l0488 * 1.9#) &lt;= l0480 Or l0480 = False Then
    ///             l0480 = Int(...)
    ///             If l0484 &lt; l0486 Or l0486 = -1 Then l0486 = l0484
    ///             If l0480 &lt; l0486 Then l0480 = l0486
    ///
    /// The help says the same thing in words: "if the Wizard's guild can cast the spell for less
    /// points than the Mage's guild, the lowest points will be used". This used to take the guild
    /// with the *highest* spell level instead, which is a different guild whenever the higher-level
    /// one has a worse rate - and the rate is squared in the numerator, so that guild is often the
    /// expensive one. A character was being overcharged for the privilege of a second guild.
    ///
    /// Two smaller points, both from the source rather than the page. The gate is the ceiling alone
    /// (`Level &lt;= SpellMax` and a level in the guild), not <see cref="GuildData.CanCast"/>, exactly
    /// as in <see cref="SpellLevelFor"/>. And `l0484` is an Integer holding `Level * rate`, so a
    /// fractional rate rounds there, VB's half-to-even; the published formula writes
    /// `StartSpellLevel * BaseMod^2` uncollapsed, which is the same thing whenever the rate is a
    /// whole number and differs slightly when it is not.
    ///
    /// The arithmetic is the source's, types and all (<see cref="Rules.Fn1485Price"/>), not the page's
    /// `- 0.0001`. The page explains its epsilon as "internal loss of precision ... 209 is stored as
    /// 208.9999..., causing it to be rounded down to 208", and the source says where that comes
    /// from: `1.9#` is a Double a shade under 1.9, and the product is floored before anything
    /// rounds it, so an exact 209 comes out 208. The epsilon reproduces that case and also floors a
    /// true result up to 0.0001 above a whole number, which the source does not.
    /// </summary>
    public static int Cost(Spell s, Character c)
    {
        int best = 0;      // l0480
        int floor = -1;    // l0486, the smallest rated level of any guild accepted so far
        foreach (var m in c.Guilds)
        {
            var def = GuildData.ByName(m.Guild);
            if (!def.HasSpellTable || m.Level <= 0) continue;
            if (s.Category < 0 || s.Category >= def.SpellMax.Length) continue;
            if (s.Level > def.SpellMax[s.Category]) continue;

            int sl = Rules.SpellLevel(m.Level);          // already clamped to 1..255
            // the spell's own Level and M556D, raw (2880, 2893): no floor of 1 on either
            int cost = Rules.Fn1485Price(s.Level, s.U4, def.SpellRate[s.Category], sl, out int rated);

            if (cost > best && best != 0) continue;
            best = cost;
            if (rated < floor || floor == -1) floor = rated;
            if (best < floor) best = floor;
        }
        // No guild of the character's reaches the spell at all: l0480 stays nought, and fn1485 says
        // so (2901). The buffer page shows that as "?", and a cast at nought costs nothing. The port
        // used to put in a point of its own here.
        return best;
    }

    /// <summary>
    /// <c>gv1D08</c>: the guild <see cref="CasterSpellLevel"/> last found, by table index. It is set
    /// only when some guild qualifies, so it keeps whatever the last successful call left - which is
    /// what the spell page's "by your X training" reads when no guild of yours reaches the spell.
    /// </summary>
    public static int TrainingGuild { get; set; }

    private static IEnumerable<(int Index, GuildDef Def, int Level)> ReachingGuilds(Character c, Spell s)
    {
        for (int g = 0; g < GuildData.All.Count; g++)
        {
            var def = GuildData.All[g];
            if (s.Category < 0 || s.Category >= def.SpellRate.Length) continue;
            if (!(def.SpellRate[s.Category] > 0 && def.SpellMax[s.Category] >= s.Level)) continue;
            var m = c.Guilds.FirstOrDefault(x => x.Guild == def.Name);
            if (m == null || m.Level <= 0) continue;
            yield return (g, def, (int)Math.Floor(m.Level / 2.0 + 0.5));
        }
    }

    /// <summary>
    /// <c>fn1455</c> (MAINMOD2.BAS:2835-2849): the guild, by table index, giving the best spell level
    /// for this spell, the first on a tie - and guild 0 when none reaches it.
    /// </summary>
    public static int TeachingGuild(Character c, Spell s)
    {
        int best = 0, guild = 0;
        foreach (var (g, _, sl) in ReachingGuilds(c, s))
            if (sl > best) { best = sl; guild = g; }
        return guild;
    }

    /// <summary>
    /// <c>casterSpellLevel</c> (MAINMOD2.BAS:2851-2866) exactly: nought when no guild reaches the
    /// spell, and <see cref="TrainingGuild"/> moved only by a guild that wins.
    /// </summary>
    public static int CasterSpellLevel(Character c, Spell s)
    {
        int best = 0;
        foreach (var (g, _, sl) in ReachingGuilds(c, s))
            if (sl > best) { TrainingGuild = g; best = sl; }
        return best;
    }

    /// <summary>
    /// Highest spell level among joined guilds that can cast the spell - the SL the
    /// Spell_Damage_Formula page describes as "Highest Spell Level from all joined guilds that can
    /// cast the spell". <c>casterSpellLevel</c>, MAINMOD2.BAS:2852-2865:
    ///
    ///     If GuildDefs(g).M55F6(Kind, 0) &gt; False And GuildDefs(g).M55F6(Kind, 1) &gt;= SpellDefs(s).Level Then
    ///         If Chars(c).GuildsJoined(g).Level &gt; False Then
    ///             If Int((Chars(c).GuildsJoined(g).Level / 2) + .5#) &gt; best Then ...
    ///
    /// "Can cast" here is weaker than <see cref="GuildData.CanCast"/>: a guild counts if it has a
    /// rate for the school at all and its ceiling reaches the spell's level, and the member's level
    /// in it is above zero. The <c>Level * rate &lt;= guildLevel / 2 + .5</c> gate that decides
    /// whether the spell appears in the list is *not* applied. This routine had been using the full
    /// gate, which is a different answer whenever the character belongs to a guild with a worse
    /// rate but a higher level - a Wizard 30 and Seeker 24 casting a level 4 Sight spell get SL 15
    /// here and did get SL 12 before.
    /// </summary>
    public static int SpellLevelFor(Character c, Spell s)
    {
        CasterSpellLevel(c, s);   // every cast asks casterSpellLevel, and that is what moves gv1D08
        int best = 0;
        foreach (var m in c.Guilds)
        {
            if (m.Level <= 0) continue;
            var def = GuildData.ByName(m.Guild);
            // the table is the only answer: a guild with no rate for the school reaches nothing
            // (MAINMOD2.BAS:2856). There used to be a "spell level up to guild level" guess here
            // for a guild without a table, which let such a guild reach any spell.
            bool reaches = s.Category >= 0 && s.Category < def.SpellRate.Length
                && def.SpellRate[s.Category] > 0 && def.SpellMax[s.Category] >= s.Level;
            if (reaches) best = Math.Max(best, Rules.SpellLevel(m.Level));
        }
        return Math.Max(1, best);
    }

    /// <summary>
    /// <c>fn1725</c> (MAINMOD2.BAS:6266-6293): a typed spell name looked up among every spell, not
    /// only the ones this character knows. Case is ignored; the whole name wins at once, otherwise
    /// the name must be the start of exactly one spell. On a miss it says why through
    /// <paramref name="say"/> (<c>sub085B</c>) and answers -1; otherwise the index into
    /// <paramref name="all"/>.
    /// </summary>
    public static int FindByName(IReadOnlyList<Spell> all, string typed, Action<string> say)
    {
        int found = 0, at = -1;
        for (int i = 0; i < all.Count; i++)
        {
            string name = all[i].Name;
            if (name.Length < typed.Length) continue;
            if (!name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, typed, StringComparison.OrdinalIgnoreCase)) { at = i; found = 1; break; }
            found++;
            at = i;
        }
        if (found == 0) { say("Spell not found!"); return -1; }
        if (found > 1) { say("Spell name is ambiguous!"); return -1; }
        return at;
    }

    /// <summary>
    /// Spells the character can cast, from the MDATA1 guild spell table: each guild has, per spell
    /// category, a level cap and a "guild levels per spell level" rate (so a Paladin reaches Heal,
    /// a level-4 spell, at guild level 12). There is no other table to fall back on.
    /// </summary>
    public static List<Spell> Known(Character c, IReadOnlyList<Spell> all)
    {
        var known = new List<Spell>();
        foreach (var m in c.Guilds)
        {
            var def = GuildData.ByName(m.Guild);
            foreach (var s in all)
            {
                // No `s.Id == 0` guard here. FRMCHAR3.BAS:83 runs `For l0076 = 0 To gv1BF8 - 1`
                // and skips nothing: unlike MDATA3 and MDATA5, whose record 0 is a template,
                // MDATA2's record 0 is a real spell - Charm of Opening, id 0, level 1 - and all
                // 105 records carry a name. Excluding id 0 kept the Thief's and Nomad's own
                // example spell out of every character's spellbook.
                if (known.Contains(s) || !(IsOffensive(s) || IsHealing(s) || IsCure(s) || IsCharm(s) || IsUtility(s))) continue;
                if (def.CanCast(s.Category, s.Level, m.Level)) known.Add(s);
            }
        }
        return known.OrderBy(s => s.Level).ThenBy(s => s.Category).ToList();
    }

    /// <summary>
    /// Something the player can cast: a known spell, or a charged item that holds one. It has no
    /// caption of its own - the spell list shows <see cref="SpellRow"/>s, sub2424's four columns.
    /// </summary>
    public sealed record Castable(Spell Spell, InventorySlot? Slot, Item? Item, int SpCost)
    {
        public bool FromItem => Slot != null;
    }

    /// <summary>Known spells first, then identified items in the pack that still hold charges of a usable spell.</summary>
    public static List<Castable> Castables(Character c, IReadOnlyList<Spell> all, Func<short, Item?> item)
    {
        var list = Known(c, all).Select(s => new Castable(s, null, null, Cost(s, c))).ToList();
        foreach (var slot in c.Inventory)
        {
            if (!slot.Identified || slot.Charges <= 0) continue;
            var it = item(slot.ItemId);
            if (it == null) continue;
            // sub15CD tests `Items(i).M56C4 > 0` and then uses `M56C4 - 1` as the index into
            // SpellDefs (MAINMOD2.BAS:6112, 6127) - the field is one-based and it is a record
            // position, not a spell id. ItemUse and Librarian already read it that way.
            //
            // This used to gate on `SpellId > 0`, which is a different field and, worse, excludes
            // the spell whose id is zero. That is Charm of Opening, and it left seven items unable
            // to do the one thing they exist for: Thief's Cap, Ring of Opening, Dust of Opening,
            // Key of Opening, Skeleton Key, and the Thief's and Scavenger's guild crests.
            if (it.SpellIndex <= 0 || it.SpellIndex - 1 >= all.Count) continue;
            var sp = all[it.SpellIndex - 1];
            // sub15CD asks two things before an item will cast at all (MAINMOD2.BAS:6108-6119): a
            // weapon, a suit of armour or a crest has to be worn, and an aligned item will only
            // work for its own alignment - half is not a thing here, it simply does nothing
            if (ItemUse.MustBeEquippedToUse(it) && !slot.Equipped) continue;
            if (slot.Alignment != ItemAlignment.Unaligned
                && ItemAlignments.AsCharacter(slot.Alignment) != c.Alignment) continue;
            if (IsOffensive(sp) || IsHealing(sp) || IsCure(sp) || IsCharm(sp) || IsUtility(sp)) list.Add(new Castable(sp, slot, it, 0));
        }
        return list;
    }

    /// <summary>
    /// Consumables vanish when their last charge is used; gear just goes inert. The list is
    /// sub1149's (MAINMOD2.BAS:5684-5711) and it is wider than it looks: Potion, Scroll, Tome, Dust,
    /// Crystal, Rod, Sphere and Cube. The port used to name only four of the eight, so a spent Rod
    /// or Sphere stayed in the pack for ever.
    /// </summary>
    public static bool IsConsumable(Item it) => ItemUse.ConsumedWhenEmpty(it);

    /// <summary>Spend one charge; returns true if the item was used up and removed from the pack.</summary>
    public static bool SpendCharge(Character c, InventorySlot slot, Item it)
    {
        slot.Charges--;
        if (slot.Charges <= 0 && IsConsumable(it)) { c.RemoveFromPack(slot); return true; }
        return false;
    }

    /// <summary>The strongest healing spell the character knows and can afford right now, if any.</summary>
    public static Spell? BestHealing(Character c, IReadOnlyList<Spell> all) =>
        Known(c, all).Where(s => IsHealing(s) && Cost(s, c) <= c.CurSpellPoints).OrderByDescending(s => s.Damage2).FirstOrDefault();

    /// <summary>
    /// What the name box asks for a heal or a cure, and what it calls a companion that cannot take
    /// one (MAINMOD2.BAS:933-944; the box itself is FRMINTER.BAS:397-411).
    ///
    /// A heal offers the party **and** the caster's companions, alive or dead - the death is only
    /// noticed after you have picked one, which is why there is a sentence for it. Cure Poison and
    /// Cure Disease go through the same box under a different tag and offer the party alone.
    /// </summary>
    public const string CastOnWho = "Cast on Who?";
    public const string CompanionIsDead = "Companion is Dead!";

    /// <summary>
    /// Maximum spell points, <c>maxSpellPoints</c> (MAINMOD2.BAS:2636-2638):
    /// <c>((Stats(INT) * 10) / 2) + ((Stats(WIS) * 10) / 2)</c> - the natural stats, not the
    /// modified ones - which is (INT + WIS) × 5. The help agrees: "Every character has a certain
    /// number of Spell Points ... based on the character's Intelligence and Wisdom", and its
    /// tutorial Nomad with INT 12 / WIS 12 has 120.
    /// </summary>
    public static int MaxSpellPoints(Character c) => (c.Stats[(int)Stat.Int] + c.Stats[(int)Stat.Wis]) * 5;
}
