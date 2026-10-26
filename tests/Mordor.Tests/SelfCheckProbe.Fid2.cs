using Mordor.Core;

/// <summary>
/// The second combat, spells and dungeon fidelity pass (2026-10-19): one section per item.
/// </summary>
public static partial class SelfCheckProbe
{
    private static void Fid2Checks(string dir)
    {
        CastGateLevelChecks(dir);
        SquareTestChecks();
        LoneMishapChecks(dir);
        BlankGoldChecks(dir);
        WeaponHandChecks(dir);
        LookOrderChecks();
        ForcedSettleChecks();
        RearMemberChecks(dir);
        RecordAtNoughtChecks(dir);
    }

    /// <summary>
    /// The group record at nought with its count above nought (CombatMonster.AtNought): a 1-hit
    /// creature loaded at 0 stands; a record nothing reloads after a kill is stood on at nought by
    /// the next member, who falls to the next blow; a breath on a group not aimed at empties it and
    /// the room is written back with that group gone; the thief's sub0D08 reads the aimed group
    /// itself (MONSMOD.BAS:946-948, 1991-2009, 3380-3386; MAINMOD2.BAS:1514-1516).
    /// </summary>
    private static void RecordAtNoughtChecks(string dir)
    {
        Console.WriteLine("\na group record at nought with members counted (MONSMOD.BAS:946-948, 1991-2009; MAINMOD2.BAS:1514-1516)");
        var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        Item? It(short id) => items.FirstOrDefault(i => i.Id == id);
        Monster? Mon(short id) => all.FirstOrDefault(m => m.Id == id);
        var mosquito = all.First(m => m.Id != 0 && m.Hits == 1 && m.InGroup > 3);

        // a 1-hit creature loaded at 0 stands, and a hit is what kills it
        var zero = new CombatMonster { Def = mosquito, Hits = 1, Group = 0, Unrolled = true };
        zero.LoadRecord(new ScriptedRandom(new[] { 0.0, 0.0 }));
        Check($"sub0D08 loads a {mosquito.Name.Trim()} at 0: it stands", $"{zero.Hits} {zero.AtNought} {zero.Alive}", "0 True True");
        zero.Hits -= 0;
        Check("and any write of its hits - a spell or breath of nought - fells it", zero.Alive, false);

        // written back and read again: a front at nought stays standing with its count
        var f2 = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"))[1];
        int area = f2.AreaIdAt(6, 5);
        var at = new DateTime(2026, 10, 19, 11, 0, 0);
        var st = new DungeonState();
        Encounter first = null!;
        for (int seed = 0; seed < 100; seed++)
        {
            st = new DungeonState();
            first = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at, new Random(seed))!;
            if (first.Count(m => m.Group == first[0].Group) >= 2) break;
        }
        var lead = first[0];
        lead.Hits = 0; lead.AtNought = true;
        int count0 = first.Count(m => m.Group == lead.Group);
        CheckAtLeast("a group of more than one, its front put at nought", count0, 2);
        var rec = EncounterGenerator.Capture(st.LoadRoom(2, area), first, null, all);
        Check("a front at nought written back: its count and nought hits", $"{rec.Foes[lead.Group].GroupCount} {rec.Foes[lead.Group].Hits}", $"{count0} 0");
        st.StoreRoom(2, area, rec);
        var again = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at.AddMinutes(5), new Random(4))!;
        var back = again.First(m => m.Group == lead.Group);
        Check("read back: it stands at nought, the rest behind it unrolled",
              $"{back.Alive} {back.AtNought} {back.Hits} {again.Where(m => m.Group == lead.Group).Skip(1).All(m => m.Unrolled)}", "True True 0 True");

        // a charm with no slot left: the creature goes, nothing loads the record, the next stands at nought
        var charm = spells.First(s => Spellbook.IsCharm(s) && s.Effect >= 75 && s.Effect <= 99
                                      && Mdata1.MonsterClasses.Count > mosquito.Type && Mdata1.MonsterClasses[mosquito.Type].Index == s.Effect - 75);
        int noRoom = 0, standing = 0;
        for (int run = 0; run < 300; run++)
        {
            var c = Character.Create("ch", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 18, 18, 15, 15, 15 });
            c.Guilds.Insert(0, new GuildMembership { Guild = "Mage", Level = 40 });
            c.MaxHits = 4000; c.CurHits = 4000;
            for (int k = 0; k < 3; k++) c.Companions.Add(new Companion { MonsterId = mosquito.Id, Hits = 1, MaxHits = 1, BindingLevel = 5 });
            var party = new Party(); party.Members.Add(c);
            var foes = new List<CombatMonster>
            {
                new() { Def = mosquito, Hits = 1, MaxHits = 1, Group = 0 },
                new() { Def = mosquito, Hits = 1, MaxHits = 1, Group = 0, Unrolled = true },
                new() { Def = mosquito, Hits = 1, MaxHits = 1, Group = 0, Unrolled = true },
            };
            var fight = new CombatEngine(party, foes, It, new Random(5200 + run));
            fight.UseMonsterTable(all);
            fight.CastSpell(c, charm, free: true, itemLevel: 1);
            if (!foes[0].Gone || foes[1].Gone || foes[1].Alive) continue;   // charmed one, felled the next with no slot
            noRoom++;
            if (foes[2].Alive && foes[2].AtNought && foes[2].Hits == 0 && !foes[2].Unrolled) standing++;
        }
        CheckAtLeast("charms that filled the last slot and then felled the next", noRoom, 5);
        Check("each left the third standing on the record at nought", standing, noRoom);

        // a companion's breath on a group not aimed at: the first kill there leaves the record at
        // nought, the rest fall to the next breaths, and the room is written back with that group gone
        var breather = all.First(m => m.Id != 0 && (m.SpecialAttackFlags & (1 << 3)) != 0 && m.GuildLevel < 40);
        var big = all.First(m => m.Id != 0 && m.Hits >= 1000 && m.SpellFlags == 0);
        int swept = 0, wrongSweep = 0, written = 0, reloaded = 0;
        for (int run = 0; run < 400; run++)
        {
            var c = Character.Create("o", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            c.MaxHits = 30000; c.CurHits = 30000;
            c.Companions.Add(new Companion { MonsterId = breather.Id, Hits = 400, MaxHits = 400, BindingLevel = 5 });
            var party = new Party(); party.Members.Add(c);
            var foes = new List<CombatMonster>
            {
                new() { Def = big, Hits = 99999, MaxHits = 99999, Group = 0 },
                new() { Def = mosquito, Hits = 1, MaxHits = 1, Group = 1 },
                new() { Def = mosquito, Hits = 1, MaxHits = 1, Group = 1, Unrolled = true },
                new() { Def = mosquito, Hits = 1, MaxHits = 1, Group = 1, Unrolled = true },
            };
            party.OrderFor(c).Kind = OrderKind.Defend;
            party.OrderFor(c).Target = 0;
            var fight = new CombatEngine(party, foes, It, new Random(6100 + run));
            fight.UseCompanionDefs(x => Mon(x.MonsterId));
            foreach (var _ in fight.RoundPaced(spells)) { }
            if (foes[1].Alive) continue;
            swept++;
            if (foes.Skip(1).Any(m => m.Alive)) wrongSweep++;
            if (foes[0].MaxHits != 99999) reloaded++;
            var room = new RoomRecord();
            room.Foes[0].Ref = all.ToList().IndexOf(big); room.Foes[0].GroupCount = 1;
            room.Foes[1].Ref = all.ToList().IndexOf(mosquito); room.Foes[1].GroupCount = 3;
            var saved = EncounterGenerator.Capture(room, foes, null, all);
            if (saved.Foes[1].GroupCount == 0) written++;
        }
        CheckAtLeast("rounds whose breath felled the first of the group not aimed at", swept, 10);
        Check("and every one behind it fell to the breaths after", wrongSweep, 0);
        Check("each of those kills loaded the aimed group's front afresh", reloaded, swept);
        Check("the room written back with that group's count at nought", written, swept);

        // the thief: sub0D08 reads M547A itself, and an aimed group that has gone draws nothing
        string atk = PortSource("src/Mordor.Core", "MonsterAttack.cs");
        Check("the thief's sub0D08 takes the aimed group itself, and an emptied thief group retargets",
              atk.Contains("if (group.Count > 0) Sub0D08(AimedRecord(c), left);") && atk.Contains("else Retarget(c);"), true);
        string combat = PortSource("src/Mordor.Core", "Combat.cs");
        Check("AimedRecord does not move off a group that has gone",
              Between(combat, "    private CombatMonster? AimedRecord(Character who)", "    /// <summary>").Contains("NextInGroup"), false);
    }

    /// <summary>
    /// The members behind a group's front have no hits until sub0D08 loads the record for them as
    /// the one in front falls (MONSMOD.BAS:1991-2008): nothing is drawn for them when the encounter
    /// is made or when the room is read back, and two draws are made at each step forward.
    /// </summary>
    private static void RearMemberChecks(string dir)
    {
        Console.WriteLine("\nthe members behind a group's front step forward with their hits (sub0D08, MONSMOD.BAS:1991-2008)");
        var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        int runs = 0, behind = 0, wrong = 0;
        for (int k = 0; k < 300; k++)
        {
            var e = EncounterGenerator.Generate(all, 1 + k % 12, new Random(k));
            foreach (var run in CombatMonster.Runs(e))
            {
                runs++;
                if (run[0].Unrolled) wrong++;
                behind += run.Count - 1;
                wrong += run.Skip(1).Count(m => !m.Unrolled);
            }
        }
        CheckAtLeast("groups made by fn0D4D", runs, 300);
        CheckAtLeast("members behind their fronts", behind, 300);
        Check("fronts carrying a roll, members behind none", wrong, 0);

        // a room read back: its fronts are the stored records, and the members behind draw nothing
        var f2 = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"))[1];
        int area = f2.AreaIdAt(6, 5);
        var st = new DungeonState();
        var at = new DateTime(2026, 10, 19, 10, 0, 0);
        Encounter? first = null;
        for (int seed = 0; seed < 50 && (first == null || first.Count < 2); seed++)
        {
            st = new DungeonState();
            first = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at, new Random(seed));
        }
        st.StoreRoom(2, area, EncounterGenerator.Capture(st.LoadRoom(2, area), first!, null, all));
        var none = new ScriptedRandom(new[] { 0.5 });
        var again = EncounterGenerator.InRoom(all, f2, 2, 6, 5, st, at.AddMinutes(5), none)!;
        CheckAtLeast("a room read back with members behind a front", again.Count, 2);
        Check("draws made reading it back", none.Draws, 0);
        Check("its members behind the fronts come back unrolled",
              CombatMonster.Runs(again).All(r => !r[0].Unrolled && r.Skip(1).All(m => m.Unrolled)), true);

        // sub0D08's two draws, the sign and then the amount
        var big = all.First(m => m.Id != 0 && m.Hits >= 40);
        var rec = new CombatMonster { Def = big, Hits = big.Hits, Unrolled = true };
        var two = new ScriptedRandom(new[] { 0.2, 0.99 });
        rec.LoadRecord(two);
        int want = big.Hits - ((int)Math.Floor(0.99 * (big.Hits * .5) / 2) + 1);
        Check("sub0D08: Int(Rnd * 2) = 0 takes Int(Rnd * Hits * .5 / 2) + 1 off", $"{rec.Hits}/{rec.MaxHits} {two.Draws} {rec.Unrolled}", $"{want}/{want} 2 False");

        // a kill brings the next one up with its hits; the ones behind it stay as they were
        Item? It(short id) => items.FirstOrDefault(i => i.Id == id);
        var blade = items.First(i => i.Id != 0 && i.Type <= 7 && i.Swings >= 1);
        var prey = all.First(m => m.Id != 0 && m.Hits > 20 && !MonsterProperty.Has(m, MonsterProperty.ImmuneToWeapons)
                                  && !MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon));
        int fights = 0, badFront = 0, badBehind = 0;
        for (int run = 0; run < 200; run++)
        {
            var party = new Party();
            var c = Character.Create("k", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 15, 15, 15, 15, 18 });
            c.MaxHits = 4000; c.CurHits = 4000;
            c.PutInPack(new InventorySlot { ItemId = blade.Id, Known = IdLevel.All, Equipped = true });
            party.Members.Add(c);
            var foes = new List<CombatMonster>
            {
                new() { Def = prey, Hits = 1, MaxHits = 1, Group = 0 },
                new() { Def = prey, Hits = prey.Hits, MaxHits = prey.Hits, Group = 0, Unrolled = true },
                new() { Def = prey, Hits = prey.Hits, MaxHits = prey.Hits, Group = 0, Unrolled = true },
            };
            var fight = new CombatEngine(party, foes, It, new Random(4100 + run));
            foreach (var _ in fight.PlayerSwings(c)) { }
            if (foes[0].Alive) continue;
            fights++;
            var standing = foes.Where(m => m.Alive).ToList();
            if (standing.Count > 0 && standing[0].Unrolled) badFront++;
            badBehind += standing.Skip(1).Count(m => !m.Unrolled);
        }
        CheckAtLeast("swings that killed the front", fights, 20);
        Check("the next one up carries its roll", badFront, 0);
        Check("the ones behind it are still unrolled", badBehind, 0);
    }

    /// <summary>
    /// gv1D6A forces mdrmazShowMaze's change-of-area block within one area (MAZEMOD.BAS:798-844):
    /// set by the teleporter square (1813), the Teleport spell (MAINMOD2.BAS:5756) and the chest's
    /// Teleporter trap (MONSMOD.BAS:3569); the room is not read again (MAZEMOD.BAS:1467).
    /// </summary>
    private static void ForcedSettleChecks()
    {
        Console.WriteLine("\na teleport within one area still runs the change-of-area block (MAZEMOD.BAS:798-844)");
        string dun = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
        string settle = Between(dun, "    private bool SettleArea()", "    private void Arrived()");
        Check("SettleArea takes the flag and clears it whatever happens",
              settle.Contains("bool forced = _forceSettle;\n        _forceSettle = false;"), true);
        Check("the same area, forced, runs the block", settle.Contains("if (forced && _loadedArea.Level >= 0) SameAreaReload();"), true);
        string reload = Between(dun, "    private void SameAreaReload()", "    /// <summary>");
        Check("the block: the room written, the leave round, the saves and sub09CC",
              reload.Contains("StoreRoom();") && reload.Contains("MonstersRound((AttackKind)2);")
              && reload.Contains("SaveChar();\n        Arrived();"), true);
        Check("and the room not read again: no sub0A91, no arrival round", reload.Contains("LoadArea(") || reload.Contains("ArrivalRound("), false);
        Check("the teleporter square sets gv1D6A",
              Between(dun, "    private string TeleporterSquare(", "    private bool SettleArea()").Contains("_forceSettle = true;"), true);
        Check("the chest's Teleporter trap sets it for a lone opener only, whose mdrmazShowMaze follows at once (MONSMOD.BAS:3591)",
              Between(dun, "    private void ChestTeleport(", "        if (!alone)").Contains("_forceSettle = alone;"), true);
        string spell = Between(dun, "            switch (Teleporting.ForSpell(_dungeon, to.Level, to.X, to.Y))", "            if (to.Level != _level) ChangeLevel(to.Level, to.X, to.Y, settle: false);");
        Check("the Teleport spell sets it only past the sky and the rock, so no exit leaves it set",
              spell.Contains("_forceSettle = true;") && spell.IndexOf("IntoRock", StringComparison.Ordinal) < spell.IndexOf("_forceSettle = true;", StringComparison.Ordinal), true);
        Check("and nothing else does", System.Text.RegularExpressions.Regex.Matches(dun, @"_forceSettle = (true|alone);").Count, 3);
        Check("a turn takes a flag still set, as its mdrmazShowMaze does (MAINMOD2.BAS:3908)",
              Between(dun, "    private void AfterTurn()", "LookAheadLines()").Contains("if (_forceSettle) SettleArea();"), true);
    }

    /// <summary>
    /// The Look page walks MorgueList, filled record by record with the blanks skipped
    /// (MAZEMOD.BAS:203-224): the saves go to the walk in record order, and the walk keeps it.
    /// </summary>
    private static void LookOrderChecks()
    {
        Console.WriteLine("\nwho is in the room, in record order (MAZEMOD.BAS:203-224, 278-300)");
        Character Make(string n) { var c = Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 }); c.DungeonLevel = 3; return c; }
        var lead = Make("Lead");
        var saved = new List<Character> { Make("Zed"), Make("Amy"), Make("Lead"), Make("Kim") };
        var roster = LookAround.Roster(new[] { lead }, saved);
        var here = LookAround.Walk(lead, roster, c => (3, 7)).Here.Select(e => e.Name);
        Check("the walk lists in the order it is given, the party in its record's place", string.Join(",", here), "Zed,Amy,Lead,Kim");

        // MorgueList's party rows are sub0956's copy (MULTIMOD.BAS:724-731); the walk refreshes
        // gv1A82's Hits alone (MAZEMOD.BAS:280), never its DeathState
        var second = Make("Second");
        var party = new List<Character> { lead, second };
        var listed = new LookAround.ListedParty();
        listed.Sync(party);
        int was = second.CurHits;
        second.CurHits = was + 3; lead.CurHits = 4;
        int HitsOf(string n, Character shown) => LookAround.Walk(lead, party, c => (3, 7), listed: c => listed.For(c, shown)).Here.First(e => e.Name == n).Hits;
        Check("a member's hits on the Look page are the last sub0956's", HitsOf("Second", lead), was);
        Check("the one the windows show has its hits refreshed by the walk", HitsOf("Second", second), was + 3);
        listed.Sync(party);
        Check("the next view redraw copies them", HitsOf("Second", lead), was + 3);
        var lone = new LookAround.ListedParty();
        lone.Sync(new[] { lead });
        Check("sub0956 copies nothing for a lone character", lone.For(lead, null) == null, true);
        var dun = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
        Check("the ailment clock redraws without sub0956", Between(dun, "    private void AilmentTick()", "finally { _inAilment = false; }").Contains("Redraw(sub0956: false);"), true);
        foreach (var (file, what) in new[] { ("DungeonWindow.Mdi.cs", "dungeon"), ("CityWindow.Mdi.cs", "town") })
            Check($"the {what} Look page walks LoadAllFrom, which is in record order",
                  PortSource("src/Mordor.App", file).Contains("saved = Character.LoadAllFrom(SavesDir);") && PortSource("src/Mordor.App", file).Contains("LookAround.Roster(_party.Members, saved);") && PortSource("src/Mordor.Core", "Character.cs").Contains("Roster.Saved(savesDir).Select(p => p.Ch).ToList();"), true);
    }

    /// <summary>
    /// The swing loop walks M5997(0) and M5997(1), the two weapon hands (MONSMOD.BAS:325-338), which
    /// sub0F74 fills only for an item of wear class 0
    /// (MAINMOD2.BAS:1728, 1842-1848). Every equip goes through the physical checks of fn11B6
    /// (MAINMOD2.BAS:1781), and wear class 0 allows one at a time: the second hand is never filled.
    /// PlayerSwings walks every equipped item of types 0-7 - which are exactly wear class 0 - so
    /// the two agree, and so does "Using no weapon!", whose sum over both hands is one weapon's.
    /// </summary>
    private static void WeaponHandChecks(string dir)
    {
        Console.WriteLine("\nthe weapon hands: one wear-class-0 item at a time (MAINMOD2.BAS:1728, 1781, 1842-1848)");
        var types = Enumerable.Range(0, Mdata1.ItemTypes.Count).Where(t => Mdata1.ItemTypes[t].Index == 0).ToList();
        Check("the item types of wear class 0 (M54D5 = 0)", string.Join(",", types), "0,1,2,3,4,5,6,7");
        Check("how many of wear class 0 may be worn (M54F1)", Character.MaxWornOfClass(0), 1);
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Check("and the chest attach goes through sub0F74 too; no weapon is such a curse (MONSMOD.BAS:3850-3851)",
              items.Count(i => i.Id != 0 && i.Type <= 7 && Loot.AttachesItself(i)), 0);
        var daggers = items.Where(i => i.Id != 0 && i.Type == 1 && i.NHands == 1).Take(2).ToList();
        Item? Lookup(short id) => items.FirstOrDefault(i => i.Id == id);
        var c = Character.Create("hands", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
        c.ClearPack();
        c.PutInPack(new InventorySlot { ItemId = daggers[0].Id, Equipped = true });
        Check("a second one-handed weapon is refused with a hand free", c.EquipProblem(daggers[1], Lookup), "Using too Many!");
    }

    /// <summary>
    /// monGetGold writes its caption outside the If (MONSMOD.BAS:2158, 2174): with nothing found
    /// or recovered the label is given a blank, wiping what it held for the half second.
    /// </summary>
    private static void BlankGoldChecks(string dir)
    {
        Console.WriteLine("\nmonGetGold with nothing to pay still writes its blank caption (MONSMOD.BAS:2158, 2174)");
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var foe = mons.First(m => m.Id != 0 && m.Hits > 5);
        var c = Character.Create("payee", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        var party = new Party(); party.Members.Add(c);
        var fight = new CombatEngine(party, new List<CombatMonster> { new() { Def = foe, Hits = 0 } }, _ => null, new Random(1));
        Check("before monGetGold runs, no caption at all", fight.GoldLine == null, true);
        fight.PayGold();
        Check("the first pays the hand-built group's gold", fight.GoldLine?.StartsWith("Your share is ") == true, true);
        fight.PayGold();   // Fight.Gold = False (2135): the second finds nothing
        Check("nothing found: the caption is written blank, not skipped", fight.GoldLine, "");
        string dun = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
        Check("the window wipes lblCharacterHit for a blank one",
              dun.Contains("if (line == \"\") { _goldLineShown++; CombatHit.Text = \"\"; }"), true);
    }

    /// <summary>
    /// A lone character killed by a companion's mishap mid-round: the monsters still go
    /// (MULTIMOD.BAS:556-557). The first group draws its target (315), sub0E85 leaves at once on
    /// the DeathState (MONSMOD.BAS:2598) and sub0930 and the Exit For follow (MULTIMOD.BAS:362-364):
    /// one draw, no blow, whatever groups stand behind.
    /// </summary>
    private static void LoneMishapChecks(string dir)
    {
        Console.WriteLine("\na lone body left by a companion's mishap: the monsters' turn draws once (MULTIMOD.BAS:556-557)");
        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var foe = mons.First(m => m.Id != 0 && m.Hits > 5 && m.SpellFlags == 0 && m.SpecialAttackFlags == 0);
        var c = Character.Create("lone", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        c.MaxHits = 10; c.CurHits = 0; c.IsDead = true;
        var party = new Party(); party.Members.Add(c);
        var group = new List<CombatMonster>
        {
            new() { Def = foe, Hits = 50, Group = 0 }, new() { Def = foe, Hits = 50, Group = 0 },
            new() { Def = foe, Hits = 50, Group = 1 },
        };
        var rng = new ScriptedRandom(new[] { 0.5 });
        var fight = new CombatEngine(party, group, _ => null, rng);
        int before = rng.Draws;
        var said = fight.MonsterAttacks(AttackKind.Round).Where(l => l.Text != "").Select(l => l.Text).ToList();
        Check("draws made by the monsters' turn at a lone body", rng.Draws - before, 1);
        Check("and nothing said: sub0E85 left before any blow", said.Count, 0);
        string combat = PortSource("src/Mordor.Core", "Combat.cs").Replace("\r\n", "\n");
        Check("RoundPaced goes on to the monsters while any stand, not on IsOver",
              combat.Contains("if (Fled || !Monsters.Any(m => m.Alive) || Party.Members.Count == 0) yield break;\n        foreach (var line in MonsterAttacks(AttackKind.Round)) yield return line;"), true);
    }

    /// <summary>
    /// The square tests a fight makes are made on the square stood on as they are asked
    /// (MAINMOD2.BAS:521, MONSMOD.BAS:1039, 2841); the window reads them before every round and
    /// every monsters' turn, not once as the fight starts.
    /// </summary>
    private static void SquareTestChecks()
    {
        Console.WriteLine("\nthe fight's square tests read the square stood on now (MAINMOD2.BAS:521)");
        string dun = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
        int Body(string head) { int i = dun.IndexOf(head, StringComparison.Ordinal); return i; }
        string Slice(string head, int len) { int i = Body(head); return i < 0 ? "" : dun.Substring(i, Math.Min(len, dun.Length - i)); }
        Check("the round reads the square before RoundPaced",
              Slice("private async void PlayRound()", 1200).Contains("SquareTests();\n            foreach (var line in _combat.RoundPaced(_spells))"), true);
        Check("the monsters' turn reads it before MonsterAttacks",
              Slice("private void MonstersRound(AttackKind kind)", 300).Contains("SquareTests();"), true);
        Check("the flags are set in SquareTests alone",
              System.Text.RegularExpressions.Regex.Matches(dun, @"_combat\.(NoSpellsHere|AntiMagicHere) =").Count, 2);
    }

    /// <summary>
    /// mdrcharCheckCast's spell-level gate: <c>l011E = SpellDefs(s).Level * l011C</c>, an Integer
    /// taking a Single product (MAINMOD2.BAS:421-422, 461). The port rounds the exact product, the
    /// reading fn1485's own <c>l0484 = l0484 * l0488</c> was settled on (Rules.Fn1485Price); a
    /// product first rounded to a Single would differ only for a rate with a long Single fraction.
    /// No shipped rate has one, so on the data the two readings never part.
    /// </summary>
    private static void CastGateLevelChecks(string dir)
    {
        Console.WriteLine("\nthe cast gate's Level * rate (mdrcharCheckCast, MAINMOD2.BAS:457-469)");
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(dir, "MDATA2.MDR"));
        int differ = 0, total = 0;
        foreach (var g in GuildData.All)
            foreach (var s in spells)
            {
                if (s.Category < 0 || s.Category >= g.SpellRate.Length) continue;
                float rate = g.SpellRate[s.Category];
                if (rate <= 0) continue;
                total++;
                int exact = (int)Math.Round(s.Level * (double)rate, MidpointRounding.ToEven);
                float single = s.Level * rate;
                if (exact != (int)Math.Round((double)single, MidpointRounding.ToEven)) differ++;
            }
        CheckAtLeast("guild rates against spells of their school", total, 100);
        Check("pairs where a Single product would round otherwise", differ, 0);
        string gate = PortSource("src/Mordor.Core", "CastGate.cs");
        Check("CastGate.Unfit rounds the exact product half to even, as fn1485's l0484",
              gate.Contains("(int)Math.Round(s.Level * (double)rate, MidpointRounding.ToEven)"), true);
    }
}
