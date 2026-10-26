namespace Mordor.Core;

/// <summary>
/// Why the monsters are attacking, which changes whether they get to at all. <c>sub0A64</c>
/// (MULTIMOD.BAS, mdrmulMonsterAttack) is called with 0 from the combat timer, with 1 when you
/// moved or turned on their square (FRMDUNM.BAS:335, MAZEMOD square change, MONSMOD.BAS:589) and
/// with 2 when you arrived on the level (MAZEMOD.BAS:813). For 1 and 2 each group only attacks
/// if a roll beats 50 + (its DEX - the target's DEX) * 5.
/// </summary>
public enum AttackKind { Round = 0, Moved = 1, ChangedLevel = 2 }

/// <summary>Where a line of combat text belongs on the original's screen.</summary>
public enum LineChannel
{
    /// <summary>The port's scrolling log only.</summary>
    Log,
    /// <summary><c>lblCharacterHit</c>: what your own swing did.</summary>
    CharacterHit,
    /// <summary><c>lblMonsterHit</c>: what the monsters did to you.</summary>
    MonsterHit,
    /// <summary><c>sub085B</c>: the message line, seen by the whole party.</summary>
    Message,
}

public sealed partial class CombatEngine
{
    /// <summary>
    /// Set by the window from the square you are fighting on: monsters do not cast on an
    /// anti-magic square or an extinguisher square (MONSMOD.BAS:2836 tests bits 18 and 10).
    /// </summary>
    public bool NoSpellsHere { get; set; }

    /// <summary>
    /// A character killed by a group's turn, told before mdrcharSetDead: each of sub0E85's five
    /// deaths - breath, acid, a blow, electrocution, stoning - first calls
    /// <c>sub06FE Foes(l0456), True, False, False, pv0442</c> (MONSMOD.BAS:2756, 2826, 3042, 3095,
    /// 3227), which files the killer in the Library. The group is given by its front member.
    /// </summary>
    public Action<Character, CombatMonster>? CharacterKilled { get; set; }

    private static bool Bit(int flags, int bit) => (flags & (1 << bit)) != 0;
    /// <summary>VB's Int(): the floor.</summary>
    private static int Floor(double v) => (int)Math.Floor(v);
    /// <summary>What assigning a Double to a VB Integer does: round half to even.</summary>
    private static int VbInt(double v) => (int)Math.Round(v, MidpointRounding.ToEven);
    private int Res(Character c, int kind) => Effects.ResistancePercent(c, kind, _item);
    private static int Res(Monster m, int kind) => kind < m.Resistances.Length ? m.Resistances[kind] : 0;
    /// <summary><c>fn1373</c>: the highest level the character holds in any guild.</summary>
    private static int MaxGuildLevel(Character c) => c.Guilds.Count == 0 ? 0 : c.Guilds.Max(g => g.Level);
    private Monster? DefOf(Companion comp) => _monsterDef?.Invoke(comp);

    /// <summary>The living monsters as the original's groups: every run of the same kind is one group.</summary>
    private List<List<CombatMonster>> AliveGroups()
    {
        var groups = new List<List<CombatMonster>>();
        foreach (var m in Monsters)
        {
            if (!m.Alive) continue;
            if (groups.Count > 0 && CombatMonster.SameGroup(groups[^1][0], m)) groups[^1].Add(m);
            else groups.Add(new List<CombatMonster> { m });
        }
        return groups;
    }

    /// <summary>
    /// The percentage chance a group gets its free attack in when you move, out of a
    /// <c>Int(Rnd * 100) + 1</c>. <see cref="AttackKind.Round"/> never asks; the other two do, and
    /// they clamp differently:
    ///
    ///     Moved         MULTIMOD.BAS:337-339   floor of 5, and no ceiling at all
    ///     ChangedLevel  MULTIMOD.BAS:355-357   the same floor, and a ceiling of 98
    ///
    /// That difference is the whole of the community page's "[maximum 98?]". A monster 10 points of
    /// Dex ahead of the character it drew attacks 98% of the time when you take the stairs and
    /// every time when you walk, and both are the original.
    /// </summary>
    public static int AttackChance(AttackKind kind, int monDex, int charDex)
    {
        int chance = 50 + (monDex - charDex) * 5;
        if (kind == AttackKind.ChangedLevel && chance > 98) chance = 98;
        if (chance < 5) chance = 5;
        return chance;
    }

    /// <summary>The whole attack, as text only, for the probes and the simulations.</summary>
    public IEnumerable<string> MonstersAttack()
    {
        foreach (var line in MonsterAttacks(AttackKind.Round)) if (line.Text != "") yield return line.Text;
    }

    /// <summary>
    /// <c>sub0A64</c>, mdrmulMonsterAttack (MULTIMOD.BAS). Each group picks one character to
    /// go for - the front of the party draws the fire, with weights 100, 50, 25, 12, 6... down
    /// the line - and then <see cref="GroupAttacks"/> is that group's whole turn against them.
    /// A dead character can still be picked, in which case the group wastes its turn.
    ///
    /// The <see cref="AttackKind"/> values are the argument the original passes, and they decide
    /// whether the group gets its turn at all. This is where the community's
    /// Combat_Initiative_Formula lives, and it is not an initiative roll: nothing here decides who
    /// goes first. It decides whether a group lands a free attack as you move.
    ///
    ///   Round (0, MULTIMOD.BAS:557 and 586) - the monsters' own tick. No roll; every group goes.
    ///   Moved (1, FRMDUNM.BAS:339 and 352, MAZEMOD.BAS:1471) - MULTIMOD.BAS:337:
    ///       l009E = 50 + ((monDex - charDex) * 5)
    ///       If l009E &lt; 5 Then l009E = 5
    ///       If (Int(Rnd * 100) + 1) &lt; l009E Then ...
    ///   ChangedLevel (2, MAZEMOD.BAS:818) - MULTIMOD.BAS:355, the same with a cap as well:
    ///       If l009E &gt; 98 Then l009E = 98
    ///
    /// The cap on one path and not the other is why the page writes its maximum as "[maximum 98?]"
    /// with a question mark: both behaviours are real, and which one you see depends on whether you
    /// walked or took the stairs. The Dex is the drawn character's, not the party's best.
    /// </summary>
    public IEnumerable<CombatLine> MonsterAttacks(AttackKind kind = AttackKind.Round)
    {
        var members = Party.Members;
        if (members.Count == 0) yield break;
        // l0096: the weights summed, halving as an Integer each step (100, 50, 25, 12, 6, 3, 1, 0),
        // once, over the party as it stood when the monsters began (MULTIMOD.BAS:305-308)
        double total = 0; int share = 100;
        for (int i = 0; i < members.Count; i++) { total += share; share /= 2; }

        foreach (var group in AliveGroups())
        {
            // UBound(Chars) is read afresh for every group (MULTIMOD.BAS:320-324): a death's UPDATE
            // below may have shortened the list, while l0096 keeps the old total
            int last = members.Count - 1;
            if (last < 0) yield break;
            var def = group[0].Def;
            int target = 0, roll = _rng.Next(100) + 1, acc = 100, step = 100;
            int guard = 0;
            while (acc / total * 100 < roll && guard++ < 32)
            {
                target++;
                if (target > last) target = last;
                step /= 2;
                acc += step;
            }
            if (target > last) target = last;
            var c = members[target];

            bool attack = true;
            if (kind != AttackKind.Round)
            {
                int charDex = c.EffectiveStat((int)Stat.Dex, _item);
                int monDex = def.Stats.Length > 5 ? def.Stats[5] : 0;
                attack = _rng.Next(100) + 1 < AttackChance(kind, monDex, charDex);
            }
            if (attack)
                foreach (var line in GroupAttacks(c, group)) yield return line;
            // "If Chars(l008E).Hits <= False Then sub0930" (sub0A64, MULTIMOD.BAS:362-363),
            // asked whether or not the group attacked: the dead lose their companions and whatever
            // they were carrying. The rest of sub0930 is the window's (see Kill there)
            if (c.CurHits <= 0)
            {
                // sub0930's head (MAINMOD2.BAS:3026-3033): the companions and the carry go, and
                // sub048C ends paralysis, poison, disease and the spell resistances (4359-4373).
                // Here, not at the blow: sub0930 runs for whoever was picked at nought, so a body
                // a companion's mishap left on the list (mdrcharSetDead alone) is cleaned too
                c.Sub0930Head(_item);
                ShowCompanions(c);   // and sub0930 repaints (MAINMOD2.BAS:3029)
                // in a party sub0930 ends in the list's UPDATE (MAINMOD2.BAS:3035-3037), which prunes
                // everyone at nought there and then (FRMGROUP.BAS:326); the window does that
                if (members.Count > 1) PartyUpdate?.Invoke(c);
                // "If UBound(Chars) = False Then Exit For" (MULTIMOD.BAS:364): the groups stop only
                // once one character is left - so a party of two that loses one leaves the other
                // untouched by the groups still to come, and a lone character's death ends it
                if (members.Count <= 1) yield break;
            }
        }
    }

    /// <summary>
    /// The party list's UPDATE that <c>sub0930</c> sends when more than one is in the party
    /// (MAINMOD2.BAS:3035-3037), set by the window: it saves the body and prunes the dead
    /// (FRMGROUP.BAS:321-346). Without it (the probes and simulations) nobody is pruned.
    /// </summary>
    public Action<Character>? PartyUpdate { get; set; }

    /// <summary>
    /// The party list's UPDATE after the characters' turns when anyone is dead
    /// (MULTIMOD.BAS:542-553), set by the window: the prune (FRMGROUP.BAS:321-346) with no
    /// sub0930 in front, so the bodies keep what they carried. Without it nobody is pruned.
    /// </summary>
    public Action? ListUpdate { get; set; }

    /// <summary>
    /// Which pack slot a thief reaches into, <c>Int(Rnd * (mc04D0 - 1)) + 2</c> at
    /// MONSMOD.BAS:3351 with <c>mc04D0 = 40</c>. The draw is 2 to 40, and the pack is a
    /// forty-slot array numbered 0 to 39, so the one line is wrong at both ends:
    ///
    ///   40 is one past the last slot. One roll in thirty-nine reads off the end, and the
    ///   original's own error handler swallows whatever that does; here it finds nothing.
    ///
    ///   1 is never drawn. Slot 0 holds the Hands and is no loss, but slot 1 is an ordinary
    ///   pack slot, and whatever sits in it can never be stolen.
    ///
    /// Both are the original's and both are reproduced. The second is the one that shows: put
    /// your best item in the second slot of the pack and no thief will ever take it.
    /// </summary>
    public static int TheftSlot(Random rng) => Floor(rng.NextDouble() * 39) + 2;

    /// <summary>
    /// How much a thief takes (MONSMOD.BAS:3337-3340): the roll, or all there is when the roll is
    /// more. <c>l04DE</c> is a Long, so a purse with a fraction goes into it rounded half to even:
    /// 10.5 on hand loses 10 and keeps the half; 11.5 loses 12, and the -0.5 that leaves is put
    /// back to nought (3349-3351). Null when <c>Gold - l04DE</c> itself overflows (a purse near the
    /// bottom of Currency's range): sub0E85's trap then ends the group's attack silently.
    /// </summary>
    public static long? GoldToSteal(VbCurrency onHand, long rolled) =>
        !VbCurrency.TrySubtract(onHand, VbCurrency.Saturate(rolled), out var left) ? null : left < 0 ? onHand.RoundToWhole() : rolled;

    /// <summary>
    /// Who an acid attack lands on, <c>Int(Rnd * 5)</c> at MONSMOD.BAS:2769-2778. The answer is 0
    /// to 4: 0 is the character and 1 to 4 are read straight back as companion slot indices.
    ///
    /// Two faults live in that one line, and both are the original's:
    ///
    ///   Companion slot 0 - the first creature you charmed - can never be hit, because the draw
    ///   that would select it already means the character.
    ///
    ///   <c>M58CB</c> declares five slots, 0 to 4, but every loop that fills or clears them runs
    ///   `0 To mc00E0 - 1`, which is 0 to 3. Slot 4 therefore keeps the zero it was born with
    ///   instead of the -1 that marks a slot empty, the emptiness test accepts it, and the acid is
    ///   taken off a companion who does not exist. A fifth of every spit lands on nobody.
    ///
    /// An empty slot in 1..3 reads -1 and is drawn again, which is the loop. The test is only
    /// <c>Ref = -1</c>, so a fallen companion still in its slot is a fair target.
    /// </summary>
    public static int AcidTarget(Random rng, CompanionSlots slots)
    {
        // fn117A(pv0442) < mc01A0 (MONSMOD.BAS:2775): with every slot empty there is no draw
        if (!slots.Any) return 0;
        for (int guard = 0; guard < 1000; guard++)
        {
            int t = (int)Math.Floor(rng.NextDouble() * 5);
            if (t == 0 || t == 4) return t;      // the character, or the phantom fifth slot
            if (slots[t] != null) return t;      // an occupied slot, alive or not
            // an empty slot reads -1, and the original draws again
        }
        return 0;
    }

    /// <summary>
    /// <c>sub0E85</c> (MONSMOD.BAS:2588): one group's turn against one
    /// character and their companions. Breath first, then acid, then up to five of the group
    /// each cast or swing, then the specials a landed hit allows - electrocution, ageing, drain,
    /// stoning, poison, disease, paralysis, item destruction and theft - in that order. The text
    /// is the original's, caption by caption.
    /// </summary>
    private IEnumerable<CombatLine> GroupAttacks(Character c, List<CombatMonster> group)
    {
        if (c.IsDead || group.Count == 0) yield break;
        var def = group[0].Def;
        bool watched = c == Party.Selected;
        bool party = Party.Size > 1;
        string name = NameFor(def, c, group[0], 1);

        // the caption is cleared, and the game waits a quarter second before anything happens
        if (watched) yield return new CombatLine("", c, Paced: true, Channel: LineChannel.MonsterHit);

        int monAtt = def.Att;
        int charDef = DefenseOf(c);                                   // x1.5 when defending, gc1DB4
        int monStr = def.Stats.Length > 0 ? def.Stats[0] : 0;
        int monLvl = def.GuildLevel;
        int attackers = Math.Min(group.Count, 5);                     // mc0448
        int flags = def.SpecialAttackFlags;
        var comps = c.Companions;
        // l0450: damage landed this turn, slot 0 the character, 1..4 the companions
        var dealt = new int[5];
        int hitsOnChar = 0;                                           // l045E
        bool charTargeted = false, anyHit = false;                    // l0470, l0462
        long critFlags = 0;                                           // l0466, the last attacker's

        // ---- breath (bits 3 fire, 4 cold), MONSMOD.BAS:2692-2757 ----
        bool fire = Bit(flags, 3), cold = Bit(flags, 4);
        if (fire || cold)
        {
            int chance = 50, top = MaxGuildLevel(c);
            if (top > monLvl + monLvl / 4.0) chance = 80;
            if (top < monLvl - monLvl / 4.0) chance = 20;
            if (_rng.Next(100) < chance)
            {
                int l046C = VbInt(monLvl * Math.Log(monLvl + 1));
                // a level-0 monster makes l046C 0 and Log(0) raises an error: the handler (MONSMOD.BAS:3414-3417)
                // resumes at End Sub, so the rest of this group's turn - breath, melee, spells - never happens
                if (l046C <= 0) yield break;
                int range = Floor(((Math.Log(l046C) + l046C / 15.0) / Math.Log(1.08) - c.Guild.Level / 20.0) + 1);
                int dmg = VbInt(((Floor((_rng.NextDouble() * range + 1) / 2) + range / 4.0) * 0.7) / 2);
                var sounds = new List<string>();
                for (int slot = 0; slot < CompanionSlots.Slots; slot++)
                {
                    // Ref > -1 And Hits > 0 (MONSMOD.BAS:2711): a fallen companion is not breathed on
                    if (comps[slot] is not Companion comp || comp.Hits <= 0 || DefOf(comp) is not Monster cdef) continue;
                    // the companion's resistance takes its cut out of the running figure, so
                    // whoever comes after gets less too
                    int res = fire ? Res(cdef, 0) : Res(cdef, 1);
                    if (res > 0) dmg = VbInt(dmg - dmg * ((res / 2.0) * 0.01));
                    comp.Hits -= dmg;
                    if (comp.Hits < 0)
                    {
                        sounds.Add(DeathSound(cdef));
                        comp.Hits = 0;
                        foreach (var line in CompanionFalls(c)) yield return line;
                    }
                }
                if (Res(c, 0) > 0 && fire) dmg = VbInt(dmg - dmg * ((Res(c, 0) / 2.0) * 0.01));
                else if (Res(c, 1) > 0 && cold) dmg = VbInt(dmg - dmg * ((Res(c, 1) / 2.0) * 0.01));
                if (dmg > 0)
                {
                    sounds.Add("FIRE");
                    yield return Caption(c, $"You were Breathed on for {dmg}!", sounds);
                    foreach (var line in TakeDamage(c, dmg)) yield return line;
                    if (c.CurHits < 1)
                    {
                        yield return FoeMessage(party ? $"\"{c.Name}\" was killed by the breath attack of a {name}." : $"You were killed by the breath attack of a {name}.");
                        CharacterKilled?.Invoke(c, group[0]); Dies(c, Rules.DeathOrdinary);
                        yield break;
                    }
                }
                else if (sounds.Count > 0) yield return new CombatLine("", c, HitSounds: sounds);
            }
        }

        // ---- acid (bit 5), MONSMOD.BAS:2759-2831 ----
        if (Bit(flags, 5))
        {
            int chance = 50;
            if (c.Guild.Level > monLvl + monLvl / 4.0) chance = 90;
            if (c.Guild.Level < monLvl - monLvl / 4.0) chance = 10;
            // Who gets it (MONSMOD.BAS:2769-2778): the draw is Int(Rnd * 5), so 0 to 4, where 0
            // is the character and 1 to 4 are companion slots. Two faults fall out of that.
            // The first companion, slot 0, can never be spat on, because 0 already means the
            // character. And M58CB is declared with five slots, 0 to 4, while every loop in the
            // game fills only 0 to 3 - so slot 4's monster index keeps its initial 0 rather than
            // the -1 that marks a slot empty, the draw accepts it, and the acid is taken off a
            // companion who is not there. One acid attack in five lands on nobody.
            int t = AcidTarget(_rng, comps);
            if (_rng.Next(100) < chance)

            {
                int l046C = monLvl + 1;
                int range = Floor(((Math.Log(l046C) + l046C / 15.0) / Math.Log(1.08) - c.Guild.Level / 10.0) + 1);
                int dmg = VbInt((Floor((_rng.NextDouble() * range + 1) / 2) + range / 2.0) * 0.7);
                if (t == 0)
                {
                    if (Res(c, 10) > 0) dmg = VbInt(dmg - dmg * ((Res(c, 10) / 2.0) * 0.01));
                    if (dmg > 0)
                    {
                        yield return Caption(c, $"You were Spit on for {dmg}!", new[] { "SPIT" });
                        foreach (var line in TakeDamage(c, dmg)) yield return line;
                        if (c.CurHits < 1)
                        {
                            yield return FoeMessage(party ? $"\"{c.Name}\" was killed by the acidic attack of a {name}." : $"You were killed by the acidic attack of a {name}.");
                            CharacterKilled?.Invoke(c, group[0]); Dies(c, Rules.DeathOrdinary);
                            yield break;
                        }
                    }
                }
                // the draw only asks that the slot be occupied (MONSMOD.BAS:2778), so a fallen
                // companion still lying in its slot can be spat on, and die again - GetCarry and all
                else if (comps[t] is Companion spat && DefOf(spat) is Monster cdef)
                {
                    if (Res(cdef, 10) > 0) dmg = VbInt(dmg - dmg * ((Res(cdef, 10) / 2.0) * 0.01));
                    spat.Hits -= dmg;
                    if (spat.Hits < 0)
                    {
                        yield return new CombatLine("", c, HitSounds: new[] { DeathSound(cdef) });
                        spat.Hits = 0;
                        // the one death site whose carry is not followed by a repaint of the panel (MONSMOD.BAS:2800-2806)
                        foreach (var line in CompanionFalls(c, repaint: false)) yield return line;
                    }
                }
                // t == 4 is the slot nobody occupies: the damage goes nowhere
            }
        }

        // ---- the attackers, MONSMOD.BAS:2832-2970 ----
        Spell? cast = null;
        for (int n = 1; n <= attackers; n++)
        {
            double damMod = def.DamageMod <= 0 ? 1 : def.DamageMod;   // l0492
            if (def.SpellFlags > 0 && !NoSpellsHere)
            {
                var spell = ChooseSpell(def);
                if (spell != null)
                {
                    cast = spell;
                    int dmg = MonsterSpellHits(c, spell, def, null);
                    // MONSMOD.BAS:2839 assigns rather than adds, so a melee blow landed earlier in
                    // this same turn is discarded and never reaches the character - the damage is
                    // only applied once, after the loop. Reproduced.
                    dealt[0] = dmg;
                    charTargeted = true;
                    hitsOnChar++;
                    // fn0FF5's sub1005 (the picks), then sub0D35 under the No Party Sound test,
                    // then sub0E99 with none (MONSMOD.BAS:2847-2852) - so its RIP and OUCH are
                    // heard with the box ticked, on the line with no actor
                    yield return Picked(new CombatLine("", c, HitSounds: new[] { AttackSound(def) }));
                    if (dmg > 0) yield return new CombatLine("", null, HitSounds: HurtSounds(def).ToList());
                    // the spell reaches the first AffectGroup + 1 companions as well, each death
                    // cry (sub0CB7, guarded, 2861-2863) before its GetCarry
                    for (int i = 0; i <= spell.AffectGroup && i < 4; i++)
                    {
                        if (comps[i] is not Companion ci || ci.Hits <= 0 || DefOf(ci) is not Monster cdef) continue;
                        ci.Hits -= MonsterSpellHits(c, spell, def, ci);
                        if (ci.Hits < 1)
                        {
                            yield return new CombatLine("", c, HitSounds: new[] { DeathSound(cdef) });
                            ci.Hits = 0;
                            foreach (var line in CompanionFalls(c)) yield return line;
                        }
                    }
                    break;   // Exit For - one spell is the group's whole attack
                }
            }

            // who this one swings at: the character, or one of the living companions
            int alive = comps.All.Count(x => x.Hits > 0);
            int t = 0;
            if (alive > 0)
            {
                if (_rng.Next(100) + 1 < 100.0 / (alive + 1)) t = 0;
                else
                {
                    for (int tries = 0; tries < 100 && t == 0; tries++)
                    {
                        int pick = _rng.Next(4) + 1;
                        if (comps[pick - 1] is Companion p && p.Hits > 0) t = pick;
                    }
                }
            }
            Companion? comp = t == 0 ? null : comps[t - 1];
            Monster? compDef = comp == null ? null : DefOf(comp);

            // an invisible target is harder to hurt, unless the attacker sees invisible (bit 0)
            if (t == 0)
            {
                if (c.InvisibleNow && !Bit(def.SpecialPropertyFlags, 0)) damMod -= 0.5;
            }
            else if (compDef != null && Bit(compDef.SpecialPropertyFlags, MonsterProperty.Invisible) && !Bit(def.SpecialPropertyFlags, 0))
                damMod -= 0.5;
            // MONSMOD.BAS:2907-2909: a character busy opening the chest is easier to hurt, and so
            // is whichever of their companions the blow goes to - the test is the character's
            // count, outside the choice of target. The help: "it usually takes a number of rounds
            // and makes your character much easier to hit."
            if (ChestRoundsOf(c) > 0) damMod += .75;

            int targetDef;
            if (t == 0) { targetDef = charDef + ProtectionBonus(c); charTargeted = true; }
            else targetDef = compDef?.Def ?? 0;

            critFlags = 0;
            int charLvl = c.Guild.Level;
            if (Bit(flags, 10) && _rng.Next(100) + 1 <= Rules.MonsterCritChance(monLvl, def.LevelFound, charLvl, backstab: false))
            { damMod += 2; critFlags |= 1 << 10; }
            if (Bit(flags, 11) && _rng.Next(100) + 1 <= Rules.MonsterCritChance(monLvl, def.LevelFound, charLvl, backstab: true))
            { damMod += 1; critFlags |= 1 << 11; }
            int targetSize = t == 0 ? SizeOf(c) : compDef?.Size ?? 0;
            int sizeDiff = Math.Abs(targetSize - def.Size);
            if (targetSize < def.Size) damMod += sizeDiff / 10.0;
            else if (targetSize > def.Size) damMod -= sizeDiff / 10.0;

            int blow = Rules.MonsterBlow(_rng, monAtt, targetDef, monLvl, monStr, damMod);
            if (t == 0 && blow > 0) hitsOnChar++;
            if (blow > 0)
            {
                anyHit = true;
                dealt[t] += blow;
                if (comp != null)
                {
                    comp.Hits -= blow;
                    if (comp.Hits < 1)
                    {
                        if (compDef != null) yield return new CombatLine("", c, HitSounds: new[] { DeathSound(compDef) });
                        comp.Hits = 0;
                        foreach (var line in CompanionFalls(c)) yield return line;
                    }
                }
            }
        }

        // ---- what it came to, MONSMOD.BAS:2971-3030 ----
        string caption;
        var afterSounds = new List<string>();
        if (dealt[0] > 0)
        {
            if (cast != null) caption = $"Monster cast \"{cast.Name}\" for {dealt[0]} damage!";
            else
            {
                caption = $"{hitsOnChar} {NameFor(def, c, group[0], hitsOnChar)}";
                bool cr = Bit((int)critFlags, 10), bs = Bit((int)critFlags, 11);
                if (cr && bs) caption += " delivered a Lethal";
                else
                {
                    if (cr) caption += " Critically";
                    if (bs) caption += " Backstabbed and";
                }
                caption += $" hit for {dealt[0]}!";
            }
        }
        else if (cast != null) caption = $"Monster's attempt to cast \"{cast.Name}\" failed!";
        else caption = group.Count > 1 ? "Monsters missed." : "Monster missed.";

        if (hitsOnChar > 0 || charTargeted)
        {
            afterSounds.Add(AttackSound(def));
            if (dealt[0] > 0)
            {
                bool cr = Bit((int)critFlags, 10), bs = Bit((int)critFlags, 11);
                if (cr && bs) afterSounds.Add("RIP"); else if (cr) afterSounds.Add("CHIT"); else if (bs) afterSounds.Add("BSTAB");
            }
        }
        if (!charTargeted) caption = "";
        if (dealt[0] > 0) afterSounds.AddRange(HurtSounds(def));
        if (dealt[0] > 0) foreach (var line in TakeDamage(c, dealt[0], silent: true)) yield return line;
        yield return Caption(c, caption, afterSounds);
        if (dealt[0] > 0) foreach (var line in OuchLine(c, dealt[0])) yield return line;
        if (c.CurHits < 1)
        {
            yield return FoeMessage(party ? $"\"{c.Name}\" was killed by a {name}." : $"You were killed by a {name}.");
            CharacterKilled?.Invoke(c, group[0]); Dies(c, Rules.DeathOrdinary);
            yield break;
        }

        // ---- electrocution (bit 6), MONSMOD.BAS:3032-3105, every target it touched ----
        if (Bit(flags, 6))
        {
            for (int t = 4; t >= 0; t--)
            {
                if (dealt[t] <= 0) continue;
                Companion? comp = t == 0 ? null : comps[t - 1];
                // a companion the blows killed and GetCarry let go has Ref -1, and
                // Monsters(-1).GuildLevel (MONSMOD.BAS:3055) is out of range: the handler resumes
                // at End Sub (sub0E85, MONSMOD.BAS:3414-3417) and the rest of the turn is lost
                if (t > 0 && comp == null) yield break;
                Monster? compDef = comp == null ? null : DefOf(comp);
                if (t > 0 && compDef == null) continue;
                int tLvl = t == 0 ? c.Guild.Level : compDef!.GuildLevel;
                int chance = 50;
                if (tLvl > monLvl + monLvl / 4.0) chance = 90;
                if (tLvl < monLvl - monLvl / 4.0) chance = 10;
                if (_rng.Next(100) >= chance) continue;
                int l046C = monLvl + 1;
                int range = Floor(((Math.Log(l046C) + l046C / 15.0) / Math.Log(1.08) - tLvl / 10.0) + 1);
                int dmg = VbInt((Floor((_rng.NextDouble() * range + 1) / 2) + range / 2.0) * 0.7);
                int res = t == 0 ? Res(c, 2) : Res(compDef!, 2);
                if (res > 0) dmg = VbInt(dmg - dmg * ((res / 2.0) * 0.01));
                if (t == 0) yield return Caption(c, $"You were Electrocuted for {dmg}!", new[] { "SHOCK" });
                if (dmg <= 0) continue;
                if (t == 0)
                {
                    foreach (var line in TakeDamage(c, dmg)) yield return line;
                    if (c.CurHits < 1)
                    {
                        yield return FoeMessage(party ? $"\"{c.Name}\" was electrocuted to death by a {name}." : $"You were electrocuted to death by a {name}.");
                        CharacterKilled?.Invoke(c, group[0]); Dies(c, Rules.DeathOrdinary);
                        yield break;
                    }
                }
                else
                {
                    comp!.Hits -= dmg;
                    if (comp.Hits < 1)
                    {
                        yield return new CombatLine("", c, HitSounds: new[] { DeathSound(compDef!) });
                        comp.Hits = 0;
                        foreach (var line in CompanionFalls(c)) yield return line;
                    }
                }
            }
        }

        int con = c.EffectiveStat((int)Stat.Con, _item);
        // ---- ageing (bit 9), MONSMOD.BAS:3106-3130 ----
        if (Bit(flags, 9) && dealt[0] > 0 && anyHit)
        {
            if (_rng.Next(100) + 1 > con * 2.5 - def.LevelFound * 2)
            {
                int years = VbInt(monLvl / 50.0);
                if (years < 1) years = 1;
                if (years > 5) years = 5;
                int days = Floor(_rng.NextDouble() * (365 * 5)) + 1;
                if (days > years * 365) days = years * 365;
                // mdrcharAddAge True, pv0442, l049E, False (MONSMOD.BAS:3129) says its own line
                // first - "You are now X older." or "\"Name\" is now X older." (MAINMOD2.BAS:6400-6405)
                // - and then this routine says "is now older!" as well
                string older = c.AgeBy(days, party);
                if (older != "") yield return FoeMessage(older);
                if (watched) yield return Caption(c, "You are now older!", new[] { "WITHER" });
                // WITHER is under the No Party Sound test (MONSMOD.BAS:3130-3132): the line is c's
                else yield return FoeMessage($"\"{c.Name}\" is now older!", new[] { "WITHER" }, c);
            }
        }

        // ---- drain (bit 7), MONSMOD.BAS:3131-3186 ----
        if (Bit(flags, 7) && dealt[0] > 0 && anyHit)
        {
            bool drained = false;
            int tries = Floor(_rng.NextDouble() * VbInt(def.LevelFound / 2.0 + 0.5)) + 1;
            var race = RaceData.ByName(c.Race);
            for (int i = 0; i < tries; i++)
            {
                if (_rng.Next(100) + 1 <= Res(c, 9)) continue;
                if (_rng.Next(100) + 1 <= con * 2.5 - def.LevelFound * 2) continue;
                int pick = Floor(_rng.NextDouble() * 6 + 1);   // 1..6: five stats, then hits
                drained = true;
                if (pick <= 5)
                {
                    // mdrcharSetAD brackets the loss only "If Ailments(mc0122) = False"
                    // (MONSMOD.BAS:3152-3162): held, the stored Att/Def do not move with the stat,
                    // so ADShift takes back whatever the port's recomputed stat part would change by
                    int shift = c.StoredADShift(_item), heldBefore = c.StatsADNow(_item);
                    c.Stats[pick] -= 1;
                    if (c.Stats[pick] < race.Min[pick]) c.Stats[pick] = race.Min[pick];
                    if (c.Paralysed) c.ADShift = shift - (c.StatsADNow(_item) - heldBefore);
                    yield return FoeMessage(party
                        ? $"\"{c.Name}\" had {(c.Sex == Sex.Male ? "his" : "her")} {RaceData.StatNamesLong[pick]} decreased by 1."
                        : $"Your {RaceData.StatNamesLong[pick]} has decreased by 1.");
                }
                else
                {
                    c.MaxHits -= 2;
                    if (c.MaxHits < MaxGuildLevel(c)) c.MaxHits = MaxGuildLevel(c);
                    if (c.CurHits > c.MaxHits) c.CurHits = c.MaxHits;
                }
            }
            if (drained)
            {
                yield return FoeMessage(watched ? "You have been drained!" : $"\"{c.Name}\" has been drained!");
                if (watched) yield return Caption(c, "You have been drained!", new[] { "DRAIN" });
                else yield return new CombatLine("", c, HitSounds: new[] { "DRAIN" });

                // MONSMOD.BAS:3190 - the drain is followed by mdrcharCheckItems with the
                // announcement on, so anything the lost point has made unusable comes off
                // here and names itself. See GearCheck.
                foreach (string line in GearCheck.Run(c, _item, party, announce: true))
                    yield return Message(line);
            }
        }

        // ---- stoning (bit 8), MONSMOD.BAS:3187-3234, every target it touched ----
        if (Bit(flags, 8))
        {
            for (int t = 4; t >= 0; t--)
            {
                if (dealt[t] <= 0) continue;
                Companion? comp = t == 0 ? null : comps[t - 1];
                // a slot already let go: Monsters(-1).Resistances (MONSMOD.BAS:3199) is out of
                // range, and sub0E85's handler (MONSMOD.BAS:3414-3417) ends the turn there
                if (t > 0 && comp == null) yield break;
                Monster? compDef = comp == null ? null : DefOf(comp);
                if (t > 0 && compDef == null) continue;
                int res = t == 0 ? Res(c, 7) : Res(compDef!, 7);
                if (_rng.Next(100) + 1 <= res) continue;
                int tCon = t == 0 ? con : (compDef!.Stats.Length > 3 ? compDef.Stats[3] : 0);
                double chance = tCon * (Math.Log(Math.Max(1, tCon)) / Math.Log(2.5)) - def.LevelFound;
                if (chance > 95) chance = 95;
                if (_rng.Next(100) + 1 <= chance) continue;
                if (t == 0)
                {
                    if (watched) yield return Caption(c, "You are now a statue.", new[] { "STONE" });
                    else yield return new CombatLine("", c, HitSounds: new[] { "STONE" });
                    yield return FoeMessage(party ? $"\"{c.Name}\" was turned to stone by a {name}." : $"You were turned to stone by a {name}.");
                    c.Stoned = true;
                    CharacterKilled?.Invoke(c, group[0]); Dies(c, Rules.DeathStoned);
                    yield break;
                }
                var sounds = new List<string> { "STONE", DeathSound(compDef!) };
                if (watched) yield return Caption(c, $"{CompanionName(comp!, t - 1)} is now a statue.", sounds);
                else yield return new CombatLine("", c, HitSounds: sounds);
                // Ref = -1 and nothing else (MONSMOD.BAS:3239): no Hits = 0 and no GetCarry, so a
                // carried companion stoned here leaves the carry pointing at an empty slot
                comps.Release(t - 1);
                ShowCompanions(c);   // MONSMOD.BAS:3240
            }
        }

        // ---- poison (bit 0) and disease (bit 1), MONSMOD.BAS:3235-3270 ----
        if (Bit(flags, 0) && dealt[0] > 0 && anyHit && _rng.Next(100) + 1 > Res(c, 5) && c.PoisonLevel == 0)
        {
            c.Poisoned = true; c.PoisonLevel = def.LevelFound;
            yield return Caption(c, "You have been Poisoned!", new[] { "POISON" });
        }
        if (Bit(flags, 1) && dealt[0] > 0 && anyHit && _rng.Next(100) + 1 > Res(c, 4) && c.DiseaseLevel == 0)
        {
            c.Diseased = true; c.DiseaseLevel = def.LevelFound;
            yield return Caption(c, "You have been Diseased!", new[] { "DISEASE" });
        }

        // ---- paralysis (bit 2), MONSMOD.BAS:3271-3287 ----
        if (Bit(flags, 2) && dealt[0] > 0 && anyHit)
        {
            int chance = Res(c, 8) + con;
            if (chance > 95) chance = 95;
            if (_rng.Next(100) + 1 > chance && ParalyzedRounds(c) == 0)
            {
                // sub0D1D pv0442, 0, Monsters(..).GuildLevel (MONSMOD.BAS:3286): the level is the
                // monster's guild level as it stands - the port's floor of 1 is gone
                c.Paralyse(monLvl, _item);
                // MONSMOD.BAS:3285-3291. The two wordings live next to the cry for help because
                // they are built the same way - see HelpCry - quote opened and never closed.
                if (watched) yield return Caption(c, HelpCry.ParalysisWatched, new[] { "PARAL" });
                // PARAL under the No Party Sound test (MONSMOD.BAS:3287-3289): the line is c's
                else yield return FoeMessage(HelpCry.Paralysis(c), new[] { "PARAL" }, c);
            }
        }

        // ---- item destruction (bit 12), MONSMOD.BAS:3288-3315 ----
        if (Bit(flags, 12))
        {
            // MONSMOD.BAS:3295-3296 rolls a slot and looks in it - If M5857(slot).M4ED4 > 0 -
            // rather than picking one of the items you happen to have. A light pack is most of the
            // protection there is.
            int slot = Floor(_rng.NextDouble() * 39) + 1;    // pack slots 1..39
            if (c.Pack[slot] is InventorySlot inSlot && inSlot.ItemId > 0 && _item(inSlot.ItemId) is Item it)
            {
                int dex = c.Stats[(int)Stat.Dex], monDex = def.Stats.Length > 5 ? def.Stats[5] : 0;
                int chance = ParalyzedRounds(c) == 0
                    ? (45 - def.LevelFound) + (dex + dex) * 2 - monDex * 2
                    : VbInt((45 - def.LevelFound) + (dex + dex) / 2.0 - monDex * 2);
                if (chance > 90) chance = 90;
                if (chance < 25) chance = 25;
                if (_rng.Next(100) + 1 > chance && it.Floor - 2 <= def.LevelFound && ChestTraps.Destructible(it))
                {
                    yield return FoeMessage(watched ? $"{name}: {it.Name} destroyed!." : $"{c.Name}, {name}: {it.Name} destroyed!.", new[] { "SLIMETR" }, c);   // guarded, 3318-3320
                    RemoveItem(c, inSlot);
                    ItemsDestroyed.Add(it.Name);
                }
            }
        }

        // ---- theft (bit 13), MONSMOD.BAS:3316-3392 ----
        if (Bit(flags, 13))
        {
            int score = _rng.Next(100);
            // the thieving check is made against the character being watched, whoever was robbed
            var (thiefMod, thiefLvl) = WinningGuild(Party.Selected, 0);
            score += Floor(_rng.NextDouble() * (thiefLvl * thiefMod) + thiefLvl / 2.0 * 1.5);
            if (Party.OrderFor(Party.Selected).Kind == OrderKind.Defend) score += score / 2;
            score -= VbInt((double)dealt[0] / Math.Max(1, c.MaxHits) * 100);
            if (score - monLvl * 2 < 50)
            {
                long gold = (long)Math.Floor(_rng.NextDouble() * (((c.DungeonLevel + monAtt) * (3353.452 * monLvl)) / Math.Max(1, c.Guild.Level)));
                if (GoldToSteal(c.GoldOnHand, gold) is not long take) yield break;   // sub0E85's trap (MONSMOD.BAS:2669, 3414-3417)
                gold = take;
                if (gold > 0)
                {
                    if (watched) yield return Caption(c, $"{CharStatFile.FormatNumber(gold)} Gold Stolen!", null);
                    else yield return FoeMessage($"\"{c.Name}\" had {CharStatFile.FormatNumber(gold)} Gold Stolen!");
                    c.GoldOnHand -= gold;
                    if (c.GoldOnHand < 0) c.GoldOnHand = 0;
                }
                InventorySlot? taken = null; Item? takenItem = null;
                if (score - monLvl * 2 <= 10)
                {
                    // MONSMOD.BAS:3351, wrong at both ends - see TheftSlot and docs/FIDELITY.md
                    int slot = TheftSlot(_rng);
                    if (slot < Character.PackSize && c.Pack[slot] is InventorySlot s && s.ItemId > 0)
                    {
                        var it = _item(s.ItemId);
                        if (it != null && ChestTraps.Destructible(it) && !s.Equipped)
                        {
                            yield return FoeMessage($"{name}: \"Thanks for the {it.Name}, {c.Name}!\"");
                            taken = s; takenItem = it;
                            RemoveItem(c, s);
                            ItemsStolen.Add(s.ItemId);
                        }
                    }
                }
                if (gold > 0 || taken != null) yield return new CombatLine("", c, HitSounds: new[] { "STEAL" });
                if ((_rng.Next(100) + 1 > 30 && gold > 0) || taken != null)
                {
                    // the thief makes off with it: one of the group leaves
                    yield return new CombatLine("", c, HitSounds: new[] { "BYE" });
                    var left = group[^1];
                    left.Gone = true;
                    group.RemoveAt(group.Count - 1);
                    // "If Foes(l0456).GroupCount > 0 Then sub0D08 pv0442" (MONSMOD.BAS:3380-3382): the
                    // thief's own record keeps its hits for whoever stands there now, and the record
                    // loaded afresh is the one the robbed character is aiming at, M547A itself - when
                    // that group has gone, sub0D08 finds its count at nought and draws nothing
                    // (1995, 2009). With the thief's group emptied instead, the robbed character's
                    // aim moves off a group that has gone (3384-3386).
                    if (group.Count > 0) Sub0D08(AimedRecord(c), left);
                    else Retarget(c);
                }
                else GoldRecovered[c] = GoldRecovered.GetValueOrDefault(c) + gold;   // still in its pockets when it dies
            }
        }

        // ---- "Help!", MONSMOD.BAS:3404-3409. The ailment clock says the same thing on its own
        // schedule, so the threshold and the words live on HelpCry.
        if (party && HelpCry.Needed(c))
            yield return Message(HelpCry.Line(c), new[] { HelpCry.Sound });
    }

    /// <summary>
    /// <c>fn0FF5</c> (MONSMOD.BAS:180): which spell a monster casts, if any. A school is drawn
    /// from its SpellFlags, then a spell of that school whose level lies between two thirds of
    /// the monster's floor and one above it; a tenth of the time nothing comes, and anything
    /// that is not of the Mage class gives up half the time on top of that.
    /// </summary>
    private Spell? ChooseSpell(Monster def)
    {
        if (_spells == null) return null;
        int school = -1;
        for (int tries = 0; tries <= 1000 && school < 0; tries++)
        {
            int pick = _rng.Next(18);
            if (Bit(def.SpellFlags, pick)) school = pick;
        }
        if (school < 0) return null;
        int top = def.LevelFound + 1;
        int low = VbInt(def.LevelFound - def.LevelFound / 3.0);
        if (low < 1) low = 1;
        var pool = _spells.Where(s => s.Category == school && s.Level >= low && s.Level <= top).ToList();
        if (pool.Count == 0) return null;
        if (_rng.Next(100) < 10) return null;
        if (!Mdata1.MonsterClassName(def.Type).Contains("Mage") && _rng.NextDouble() * 100 < 50) return null;
        int l00CC = _rng.Next(pool.Count);
        var chosen = pool[l00CC];
        if (!(chosen.Damage2 > 0 && chosen.SpecialEffect > 0)) return null;
        // "fn0FF5 = l00D4(l00CC) : sub1005 l00CC" (MONSMOD.BAS:244-245): the sound is asked for
        // with the place in the pool, not the spell - l00D4(l00CC) is the spell, l00CC only its
        // index among the candidates - so it is SpellDefs(l00CC)'s sound, some spell near the top
        // of the table, whatever was picked. Reproduced.
        PendingPickSounds = l00CC < _spells.Count ? SpellSounds.For(_spells[l00CC]) : null;
        return chosen;
    }

    /// <summary>
    /// fn0FF5's sub1005 sounds from the last pick, waiting for the next line out (see
    /// <see cref="Picked"/>).
    /// </summary>
    private IReadOnlyList<string>? PendingPickSounds;

    /// <summary>The line with fn0FF5's pick sounds put on it, if any are waiting; they go out once.</summary>
    private CombatLine Picked(CombatLine line)
    {
        if (PendingPickSounds == null) return line;
        var l = line with { PickSounds = PendingPickSounds };
        PendingPickSounds = null;
        return l;
    }

    /// <summary>
    /// <c>fn0E5F</c>, monGetSpellHits (MONSMOD.BAS:2459), for a monster casting at a character
    /// or at one of their companions. The caster's level is a quarter of its guild level.
    /// </summary>
    private int MonsterSpellHits(Character c, Spell spell, Monster caster, Companion? comp)
    {
        int lvl = Floor(caster.GuildLevel / 4.0);
        if (lvl < 1) lvl = 1;
        if (lvl > 255) lvl = 255;
        double l042C = Math.Log(lvl + 1) / Math.Log(5);
        double power = spell.Damage2 + spell.Damage2 * l042C;
        power += Floor(lvl / 10.0 * spell.SpecialEffect);
        int cut = Floor(_rng.NextDouble() * (power - (lvl - 1) / Math.Pow(lvl, 2)) / 2) + 1;
        int dmg = Floor(power - cut);
        var compDef = comp == null ? null : DefOf(comp);
        if (spell.ResistIndex > -1)
        {
            int res = comp == null ? Res(c, spell.ResistIndex) : compDef == null ? 0 : Res(compDef, spell.ResistIndex);
            dmg = VbInt(dmg * (1 - res / 100.0));
        }
        if (dmg < 1) dmg = 0;
        if (comp != null && compDef != null)
        {
            // a companion that is immune to magic (bit 2) takes nothing, and a spell aimed at
            // one class of creature (effects 75..99) is checked - against the group the owner is
            // fighting, Foes(CombatOrders(owner).M547A), not the companion (MONSMOD.BAS:2561-2564)
            if (Bit(compDef.SpecialPropertyFlags, 2)) dmg = 0;
            if (spell.Effect is >= 75 and <= 99)
            {
                var facing = FoeOf(c)?.Def;
                if (facing != null && facing.Type < Mdata1.MonsterClasses.Count && Mdata1.MonsterClasses[facing.Type].Index != spell.Effect - 75) dmg = 0;
            }
        }
        if (spell.Effect == 100)
        {
            // all or nothing: it only counts if it would kill
            int hits = comp?.Hits ?? c.CurHits;
            if (hits - dmg > 0) dmg = 0;
        }
        return dmg;
    }

    /// <summary><c>fn13FE</c>: a Protection spell adds Log(p) * Log(p) * 3 to defence.</summary>
    private static int ProtectionBonus(Character c) =>
        c.Protection > 0 ? VbInt(Math.Log(c.Protection) * (Math.Log(c.Protection) * 3)) : 0;

    /// <summary>
    /// <c>sub0D35</c> (MUSIC, MODULE9.BAS:466): the sound a monster makes when it attacks, by
    /// class - Asmodeus has his own.
    /// </summary>
    public static string AttackSound(Monster def)
    {
        if (Asmodeus.Is(def)) return Asmodeus.Sound;
        return def.Kind switch
        {
            MonsterKind.Humanoid or MonsterKind.Thief => "GOBLIN",
            MonsterKind.Cleanup or MonsterKind.Elemental => "SLIME",
            MonsterKind.Demon => "DEMON",
            MonsterKind.Devil => "DEVIL",
            MonsterKind.Reptile => "SNAKE",
            MonsterKind.Dragon => "DRAGON",
            MonsterKind.Animal => def.PicId == 18 ? "BIRD" : "DOG",
            MonsterKind.Insect => "BUG",
            MonsterKind.Undead => "UNDEAD",
            MonsterKind.Aquatic => "WATERD",
            MonsterKind.Giant => "GIANT",
            MonsterKind.Mythical => "MYTHIC",
            MonsterKind.Lycanthrope => "LYCAN",
            MonsterKind.Mage => "MAGE",
            MonsterKind.Warrior => "WARRIOR",
            MonsterKind.Indigini => "PLANTATT",
            _ => "",
        };
    }

    /// <summary><c>sub0E99</c> (MODULE9.BAS:119-129): RIP for demons, devils, elementals and animals, then OUCH for everyone.</summary>
    private static IEnumerable<string> HurtSounds(Monster def)
    {
        if (def.Kind is MonsterKind.Demon or MonsterKind.Devil or MonsterKind.Elemental or MonsterKind.Animal) yield return "RIP";
        yield return "OUCH";
    }

    /// <summary><c>fn128B</c>: a companion by name, or "Companion #n".</summary>
    private static string CompanionName(Companion comp, int index) =>
        comp.Name.Trim() == "" ? $"Companion #{index + 1}" : $"\"{comp.Name.Trim()}\"";

    private string NameFor(Monster def, Character viewer, CombatMonster m, int count) =>
        EncounterGenerator.NameFor(def, viewer, count, m.Identified ? 0 : 1);

    /// <summary>A caption for <c>lblMonsterHit</c>: shown, and waited on, only for the character being watched.</summary>
    private CombatLine Caption(Character c, string text, IReadOnlyList<string>? sounds)
    {
        bool watched = c == Party.Selected;
        if (watched && text != "") Say(text);
        return new CombatLine(watched ? text : "", c, Paced: watched, HitSounds: sounds, Channel: LineChannel.MonsterHit);
    }

    /// <summary>
    /// A line for the message bar, <c>sub085B</c>. <paramref name="soundOf"/> is the character
    /// whose No Party Sound test guards <paramref name="sounds"/> (see <see cref="CombatSounds"/>);
    /// without one they are heard whoever is on screen, as the cry for help is.
    /// </summary>
    private CombatLine Message(string text, IReadOnlyList<string>? sounds = null, Character? soundOf = null)
    {
        Say(text);
        return new CombatLine(text, soundOf, HitSounds: sounds, Channel: LineChannel.Message);
    }

    /// <summary>
    /// <see cref="Message"/> for a line telling what a foe did to the party (a blow, a breath, a
    /// drain, a theft, a companion's mishap on a member): the same line, marked
    /// <see cref="MessageKind.Foes"/> for a mod that colours the Messages window. The text, the
    /// sounds and everything else are unchanged.
    /// </summary>
    private CombatLine FoeMessage(string text, IReadOnlyList<string>? sounds = null, Character? soundOf = null) =>
        Message(text, sounds, soundOf) with { Kind = MessageKind.Foes };

    /// <summary>
    /// <c>sub0CA0</c>: take the damage off, floored at zero; in a party, a member who is not being
    /// watched calls out when it hurts - "Aigh!" at zero, "Ouch!  Hit for N!" from a third of their
    /// hits up.
    /// </summary>
    private IEnumerable<CombatLine> TakeDamage(Character c, int dmg, bool silent = false)
    {
        c.CurHits -= dmg;
        if (c.CurHits < 0) c.CurHits = 0;
        if (silent) yield break;
        foreach (var line in OuchLine(c, dmg)) yield return line;
    }

    private IEnumerable<CombatLine> OuchLine(Character c, int dmg)
    {
        if (Party.Size <= 1 || c == Party.Selected) yield break;
        if (c.CurHits == 0) yield return FoeMessage($"{c.Name}, \"Aigh!\"");
        else if (dmg >= c.MaxHits / 3.0) yield return FoeMessage($"{c.Name}, \"Ouch!  Hit for {dmg}!\"");
    }

    /// <summary><c>sub091E</c>: the character is dead, and this is why.</summary>
    private static void Dies(Character c, int cause)
    {
        // mdrcharSetDead inside sub0E85 (MONSMOD.BAS:2757-3228). The "If Hits <= False Then sub0930"
        // that follows (MULTIMOD.BAS:362-363) - its sub048C and all - is MonsterAttacks', once
        // the group's turn is over: every Dies is followed by the end of GroupAttacks
        SetDead(c, cause);
    }

    /// <summary>
    /// <c>mdrcharSetDead</c> alone, with no sub0930 after it: what a companion's mishap does to its
    /// owner. monCompanionAttack (MONSMOD.BAS:901-1305) calls only mdrcharSetDead, and the party
    /// list's UPDATE then unloads the body without sub0930 - so no sub048C: paralysis, poison,
    /// disease and the spell resistances go with the body as they were.
    /// </summary>
    private static void SetDead(Character c, int cause)
    {
        c.CurHits = 0;
        c.IsDead = true;
        c.CauseOfDeath = cause;
    }

    /// <summary>
    /// A companion whose hits reached zero: it stays in its slot, and every such place in the
    /// original follows <c>Hits = 0</c> with <c>mdrcharGetCarry</c> (MONSMOD.BAS:2728, 2804,
    /// 2866, 2971, 3108 and in the companions' own mishaps). The caller has already set the hits;
    /// this runs the carry and says what it says, then repaints the page as each of those sites
    /// does with <c>mdrcharShowCompanions</c> - which, for the one on screen, blanks the names of
    /// the slots the sweep emptied.
    ///
    /// It runs with or without a monster table. Without one only the size test cannot be made:
    /// an unknown creature takes the path the original has for an unreadable <c>Monsters()</c>
    /// entry, the error handler, which leaves before the sweep.
    /// </summary>
    private IEnumerable<CombatLine> CompanionFalls(Character c, bool repaint = true)
    {
        foreach (string line in CompanionCarry.Run(c, c == Party.Selected, _monsterDef ?? (_ => null)))
            yield return Message(line);
        if (repaint) ShowCompanions(c);
    }

    /// <summary>
    /// <c>mdrcharShowCompanions</c> as far as the engine is concerned: it leaves at once for
    /// anybody but gv1A82 (MAINMOD2.BAS:4716), then writes " " into every empty slot's name (4745).
    /// The port keeps one index for gv1A82 and Me, the selected member.
    /// </summary>
    private void ShowCompanions(Character c)
    {
        if (c == Party.Selected) c.Companions.BlankEmptyNames();
    }

    /// <summary><c>sub0EDA</c>, mdrcharRemoveItem: the slot is emptied, whatever it was doing.</summary>
    private static void RemoveItem(Character c, InventorySlot slot) => c.RemoveFromPack(slot);
}
