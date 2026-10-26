using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The town batch (2026-10-18): the guild's quest targets drawn the original's way
    /// (FRMCTYG.BAS:489-561), and the rest of the batch below it.
    /// </summary>
    private static void TownBatchChecks(string dir)
    {
        Section(() => QuestTargetChecks(dir));
        Section(() => RestockChecks(dir));
        Section(() => PauseChecks());
        Section(() => GiveChecks(dir));
        Section(() => DepositUpdateChecks());
    }

    /// <summary>
    /// FRMCITYB.BAS:180: a pack line dropped on the Bank tells an open Store "UPDATE" first, before
    /// the Hands test, the equipped refusal and the move. The port sent it after.
    /// </summary>
    private static void DepositUpdateChecks()
    {
        Console.WriteLine("\nthe deposit tells an open Store before it moves anything (FRMCITYB.BAS:180)");
        string b = PortSource("decompiled-named", "FRMCITYB.BAS").Replace("\r\n", "\n");
        Check("FRMCITYB.BAS: UPDATE is the first thing an ITEM drop does",
              b.Contains("    If  Source.Tag = \"ITEM\" Then\n        If  fn03BD(frmCityStore) Then frmCityStore!lblCall = \"UPDATE\"\n        If  Source.Index Then"), true);
        string m = PortSource("decompiled-named", "FRMCTYM.BAS").Replace("\r\n", "\n");
        Check("FRMCTYM.BAS: the service forms are shown modeless, so the Store and the Bank can be up together",
              m.Contains("Static m00B0 As Integer") && m.Contains("            frmCityStore.Show m00B0") && m.Contains("            frmCityBank.Show m00B0"), true);
        string src = PortSource("src/Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        int at = src.IndexOf("async System.Threading.Tasks.Task Deposit(int slot)");
        int upd = at < 0 ? -1 : src.IndexOf("_storeUpdate?.Invoke();", at);
        int hands = at < 0 ? -1 : src.IndexOf("if (slot <= 0 || slot >= Character.PackSize) return;", at);
        int move = at < 0 ? -1 : src.IndexOf("_ch.DepositToVault(slot)", at);
        Check("the port: Deposit tells the Store before the Hands test and the move",
              at >= 0 && upd > at && upd < hands && hands < move, true);
    }

    /// <summary>
    /// 'g' Give, fn1521 case 71 (MAINMOD2.BAS:3705-3730): three frmInteract questions - name,
    /// gold, item number - and sub14B5. The port handed the selected item, or all the gold, to the
    /// next member round.
    /// </summary>
    private static void GiveChecks(string dir)
    {
        Console.WriteLine("\n'g' Give: name, gold, item # (MAINMOD2.BAS:3705-3730, FRMINTER.BAS:116-151)");
        string Norm(string s) => s.Replace("\r\n", "\n");
        string main2 = Norm(PortSource("decompiled-named", "MAINMOD2.BAS"));
        string inter = Norm(PortSource("decompiled-named", "FRMINTER.BAS"));
        Check("MAINMOD2.BAS: the three questions, in order",
              main2.Contains("l0540 = Val(fn170D(\"Give to Character\", \"Character's Name?\", \"\", \"GIVE\"))")
              && main2.Contains("l05DE = CLng(fn170D(\"Give to \" + Chr$(34) + RTrim(Chars(l0540).Name) + Chr$(34), \"How much Gold?\", \"\", \"GIVEGOLD\"))")
              && main2.Contains("l05E0 = Val(fn170D(\"Give to \" + Chr$(34) + RTrim(Chars(l0540).Name) + Chr$(34), \"Which Item #?\", \"\", \"GIVEITEM\"))")
              && main2.Contains("                sub14B5 Me, l0540, l05E0, l05DE"), true);
        Check("FRMINTER.BAS: the gold answer's checks",
              inter.Contains("            Case \"GIVEGOLD\"\n                If  Val(l0036) < 1 Then\n                    frmInteract.Tag = \"0\"")
              && inter.Contains("l003A = MsgBox(\"Not enough gold!\", 48, \"SORRY!\")"), true);
        Check("FRMINTER.BAS: the item answer's checks",
              inter.Contains("l003A = MsgBox(\"No Item to give!\", 48, \"SORRY!\")")
              && inter.Contains("l003A = MsgBox(\"Item is Equipped!\", 48, \"SORRY!\")"), true);
        Check("FRMINTER.BAS: the name list leaves out the one giving",
              inter.Contains("                If  Me.Tag = \"GIVE\" And l007A = gv1A84 Then"), true);

        // the gold question
        Check("gold: empty is 0", GiveCommand.GoldAnswer("", 100).Tag, "0");
        Check("gold: under 1 is 0", GiveCommand.GoldAnswer("0.4", 100).Tag, "0");
        Check("gold: not a number is 0", GiveCommand.GoldAnswer("abc", 100).Tag, "0");
        Check("gold: more than on hand is refused", GiveCommand.GoldAnswer("150", 100).Refusal, "Not enough gold!");
        Check("gold: what was typed goes back as typed", GiveCommand.GoldAnswer("50", 100).Tag, "50");
        Check("gold: CLng of it", GiveCommand.GoldFromTag("50"), (long?)50);
        Check("gold: CLng rounds half to even", GiveCommand.GoldFromTag("2.5"), (long?)2);
        Check("gold: \"12 gold\" passes Val but is a Type mismatch to CLng (the error trap ends the command)",
              $"{GiveCommand.GoldAnswer("12 gold", 100).Tag}|{GiveCommand.GoldFromTag("12 gold")?.ToString() ?? "error"}", "12 gold|error");

        // Val as VB reads it, and the CLng the transfer uses (review, 2026-10-18): "1e3" was 1 to the
        // port's Val, passed the 500-gold test, and then moved 1,000
        Check("Val: E and D exponents, &H and &O, blanks anywhere, stop at the first misfit",
              $"{GiveCommand.Val("1e3")} {GiveCommand.Val("1D2")} {GiveCommand.Val("&H10")} {GiveCommand.Val("&O17")} {GiveCommand.Val(" 1 2")} {GiveCommand.Val("7. Potion")} {GiveCommand.Val("1,000")} {GiveCommand.Val("&HFFFF")} {GiveCommand.Val("1e")} {GiveCommand.Val("abc")}",
              "1000 100 16 15 12 7 1 -1 1 0");
        string Gold(string typed)
        {
            var (tag, no) = GiveCommand.GoldAnswer(typed, 500);
            if (no != null) return no;
            return GiveCommand.GoldFromTag(tag!) is long g ? g.ToString() : "type mismatch";
        }
        Check("gold, 500 on hand: \"1e3\" is a thousand to Val, so refused", Gold("1e3"), "Not enough gold!");
        Check("gold, 500 on hand: \"1e9\" refused", Gold("1e9"), "Not enough gold!");
        Check("gold: \"1D2\" is 100 to both", Gold("1D2"), "100");
        Check("gold: \"&H10\" is 16 to both", Gold("&H10"), "16");
        Check("gold: \" 12\" is 12", Gold(" 12"), "12");
        Check("gold: \"12 gold\" passes Val and is a Type mismatch to CLng, which ends the command", Gold("12 gold"), "type mismatch");
        Check("gold: a negative is 0", Gold("-5"), "0");
        Check("gold: empty is 0", Gold(""), "0");
        // the original's own hole: Val stops at the comma, CLng reads the locale's thousands separator
        bool commaGroups = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator == ",";
        Check("gold, 500 on hand: \"1,000\" is 1 to the test and a thousand to the transfer (the original's hole)",
              Gold("1,000"), commaGroups ? "1000" : Gold("1,000"));

        // the item question, against a real pack
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        Item? Lookup(short id) => items.FirstOrDefault(i => i.Id == id);
        var plain = items.First(i => i.Id != 0 && i.Floor >= 1 && i.Cursed == 0 && ChestTraps.Destructible(i));
        var crest = items.FirstOrDefault(i => i.Id != 0 && !ChestTraps.Destructible(i));
        var me = Character.Create("Giver", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        var you = Character.Create("Taker", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        me.PutInSlot(1, new InventorySlot { ItemId = plain.Id, Known = IdLevel.All });
        me.PutInSlot(2, new InventorySlot { ItemId = plain.Id, Known = IdLevel.All, Equipped = true });
        Check("item: empty or 0 is no item", $"{GiveCommand.ItemAnswer("", me, Lookup).Tag} {GiveCommand.ItemAnswer("0", me, Lookup).Tag}", "0 0");
        Check("item: #1, the Hands, is refused (fn131A answers slot 0)", GiveCommand.ItemAnswer("1", me, Lookup).Refusal, "No Item to give!");
        Check("item: #2 is pack slot 1", $"{GiveCommand.ItemAnswer("2", me, Lookup).Tag} {GiveCommand.SlotFromTag("2")}", "2 1");
        Check("item: a worn one is refused", GiveCommand.ItemAnswer("3", me, Lookup).Refusal, "Item is Equipped!");
        Check("item: an empty slot is refused", GiveCommand.ItemAnswer("5", me, Lookup).Refusal, "No Item to give!");
        Check("item: past 40 is refused", GiveCommand.ItemAnswer("41", me, Lookup).Refusal, "No Item to give!");
        Check("item: the answer's Val into an Integer, less one past 1", $"{GiveCommand.SlotFromTag("0")} {GiveCommand.SlotFromTag("1")} {GiveCommand.SlotFromTag("2.5")} {GiveCommand.SlotFromTag("1.6")}", "0 0 1 1");

        // sub14B5: the gold, then the item, in the one command
        me.GoldOnHand = 300; you.GoldOnHand = 5;
        var said = GiveCommand.Give(me, you, 1, 120, Lookup);
        Check("give: the gold goes first and says so, then the item",
              string.Join("|", said.Select(s => s.Text)),
              "Give of 120 gold to \"Taker\" Completed!|Give of Item to \"Taker\" Completed!");
        Check("give: both arrived", $"{me.GoldOnHand} {you.GoldOnHand} {me.Pack[1]?.ItemId ?? 0} {you.Pack.Count(p => p != null && p.ItemId == plain.Id)}",
              $"180 125 0 1");
        Check("give: an empty slot gives nothing and says nothing", GiveCommand.Give(me, you, 7, 0, Lookup).Count, 0);
        if (crest != null)
        {
            me.PutInSlot(4, new InventorySlot { ItemId = crest.Id, Known = IdLevel.All });
            said = GiveCommand.Give(me, you, 4, 10, Lookup);
            Check("give: a guild-issued item is refused after the gold has gone",
                  string.Join("|", said.Select(s => s.Text)), "Give of 10 gold to \"Taker\" Completed!|Cannot give Guild-Issued Items!");
        }
        var party = new List<Character> { me, you };
        Check("give: the name list is everyone but the giver", string.Join(",", GiveCommand.Offered(party, me).Select(c => c.Name.TrimEnd())), "Taker");

        string flow = Norm(PortSource("src/Mordor.App", "GiveFlow.cs"));
        Check("the port: both windows ask the three questions",
              Norm(PortSource("src/Mordor.App", "CityWindow.axaml.cs")).Contains("await GiveFlow.Run(this, _party.Members, _ch, Lookup, FiledFor, Log, Tell)")
              && Norm(PortSource("src/Mordor.App", "DungeonWindow.axaml.cs")).Contains("await GiveFlow.Run(DialogOwner, _party.Members, _ch, Lookup, FiledFor, Say, Tell)")
              && flow.Contains("GiveCommand.GoldLabel") && flow.Contains("GiveCommand.ItemLabel"), true);
    }

    /// <summary>
    /// Settings.Paused (FRMMAIN.BAS:202-233): minimised, the play clock stops counting
    /// (MAINMOD2.BAS:6012), the music is stopped and closed and its timer idles (FRMMAIN.BAS:207-209,
    /// 296), and the wave device is given up (210-212). The port counted on and played on.
    /// </summary>
    private static void PauseChecks()
    {
        Console.WriteLine("\nminimised, the game pauses (FRMMAIN.BAS:202-233, MAINMOD2.BAS:6012)");
        string Norm(string s) => s.Replace("\r\n", "\n");
        string main = Norm(PortSource("decompiled-named", "FRMMAIN.BAS"));
        string main2 = Norm(PortSource("decompiled-named", "MAINMOD2.BAS"));
        Check("FRMMAIN.BAS: minimised, the music is stopped and closed and Paused set",
              main.Contains("    If  Me.WindowState = mc00D6 Then\n        frmMain!mciMusic.Command = \"Stop\"\n        frmMain!mciMusic.Command = \"Close\"")
              && main.Contains("        Settings.Paused = True\n    ElseIf Me.WindowState = mc00E4 Then"), true);
        Check("FRMMAIN.BAS: back, sub1A50 starts the music and Paused is cleared",
              main.Contains("        sub1A50\n        If  gv1A66.M5EFC = False Then\n            l00D4 = extfn1DCC(gv20C8, True)\n        End If\n        Settings.Paused = False"), true);
        Check("FRMMAIN.BAS: the music timer idles while paused",
              main.Contains("    sub16F8\n    If  gv1A66.M5EF1 = False Then\n        If  Settings.Paused = False Then"), true);
        Check("MAINMOD2.BAS: sub16F8 counts only unpaused, and restarts every member either way",
              main2.Contains("            If  Settings.Paused = False Then\n                If  Timer < gv1B1E(l0834) Then")
              && main2.Contains("                Chars(l0834).M5906(mc0786) = Chars(l0834).M5906(mc0786) + l0832\n            End If\n            gv1B1E(l0834) = Timer"), true);

        Check("minimising pauses", FramePause.Transition(false, true), FramePause.Change.Paused);
        Check("and it holds", FramePause.Paused, true);
        Check("a minimised frame minimised again changes nothing", FramePause.Transition(true, true), FramePause.Change.None);
        Check("restoring resumes", FramePause.Transition(true, false), FramePause.Change.Resumed);
        Check("un-maximising changes nothing (neither branch of MDIForm_Resize)", FramePause.Transition(false, false), FramePause.Change.None);

        var a = Character.Create("a", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        var clock = new PlayClock();
        var one = new List<Character> { a };
        clock.Tick(one, 100f);
        clock.Tick(one, 110f);
        FramePause.Transition(false, true);
        clock.Tick(one, 500f);
        clock.Tick(one, 900f);
        Check("minimised, the play clock does not count", a.PlaySeconds, 10.0);
        FramePause.Transition(true, false);
        clock.Tick(one, 905f);
        Check("back, it counts from the last tick, and the time away is never counted", a.PlaySeconds, 15.0);
        FramePause.Paused = false;

        string audio = Norm(PortSource("src/Mordor.App", "Audio.cs"));
        string watch = Norm(PortSource("src/Mordor.App", "FramePauseWatch.cs"));
        Check("the port: effects, the music timer and new tunes all wait while paused",
              audio.Contains("if (FramePause.Paused) return;   // WaveMixActivate")
              && audio.Contains("if (FramePause.Paused) return;   // timMusic_Timer")
              && audio.Contains("if (FramePause.Paused) { _resumeMusic = path; return; }"), true);
        Check("the port: the menu and the town frame are watched",
              Norm(PortSource("src/Mordor.App", "TitleWindow.axaml.cs")).Contains("FramePauseWatch.Watch(this);")
              && Norm(PortSource("src/Mordor.App", "CityWindow.axaml.cs")).Contains("FramePauseWatch.Watch(this);")
              && watch.Contains("case FramePause.Change.Paused: Audio.Pause(); break;"), true);
    }

    /// <summary>
    /// mdrReplenishStore, MAINMOD.BAS:1484-1683. Three differences the audit found: a full shelf
    /// spends the pass (the port drew again), the weights are whole percents in an Integer array
    /// (the port kept exact doubles), and only the buying-out loop gives up (the port capped both
    /// loops and the passes).
    /// </summary>
    private static void RestockChecks(string dir)
    {
        Console.WriteLine("\nthe Store restock (mdrReplenishStore, MAINMOD.BAS:1484-1683)");
        string m = PortSource("decompiled-named", "MAINMOD.BAS").Replace("\r\n", "\n");
        Check("MAINMOD.BAS: the weights are an Integer array",
              m.Contains("Dim l02C8() As Integer\n    ReDim l02C8(gv1BEC)             As Integer"), true);
        Check("MAINMOD.BAS: a shelf at nine adds nothing, and the item still ends the loop",
              m.Contains("                            If  Shelf(l02C4).Align(l02C2) < mc0298 Then\n                                Shelf(l02C4).Align(l02C2) = Shelf(l02C4).Align(l02C2) + 1")
              && m.Contains("            Loop While l02C4 = False\n        Else"), true);
        Check("MAINMOD.BAS: only the buying-out loop gives up, at 100",
              m.Contains("            Loop While l02C6 < 100 And l02C4 = False"), true);
        Check("MAINMOD.BAS: the Library is told of each item sold in (not ported here)",
              m.Contains("                                sub0730 m0070, -l02C4, m029E, -2, True"), true);

        // whole percents, half to even
        Check("weight: 1 of 300 is 0.33%, which an Integer holds as 0", StoreStock.Weight(1, 300f), 0);
        Check("weight: 1 of 8 is 12.5%, held as 12 (half to even)", StoreStock.Weight(1, 8f), 12);
        Check("weight: 3 of 8 is 37.5%, held as 38", StoreStock.Weight(3, 8f), 38);
        Check("weight: no rarity is 0", StoreStock.Weight(0, 8f), 0);

        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        int kinds = Mdata1.ItemTypes.Count;
        Check("no item has an Align above 1 with none of the four alignments (the add would spin)",
              items.Count(i => i.AlignmentFlags > 1 && (i.AlignmentFlags & 0xF) == 0), 0);
        var weightsShort = 0;
        for (int k = 0; k < kinds; k++)
        {
            var run = items.Skip(1).Where(i => i.Type == k && i.Floor <= 3).ToList();
            float total = run.Sum(i => (float)i.Rarity);
            if (run.Count > 0 && total > 0 && run.Sum(i => StoreStock.Weight(i.Rarity, total)) < 100) weightsShort++;
        }
        CheckAtLeast("kinds whose floor-3 weights sum short of 100 (a draw there picks nothing and goes round)", weightsShort, 1);

        // a kind whose first item within floor 1 can be sold in: the draws below name it exactly
        int kind = -1, idx = -1;
        for (int k = 1; k < kinds && kind < 0; k++)
        {
            int f = 0;
            for (int i = 0; i < items.Count; i++) if (items[i].Type == k && items[i].Floor <= 1) { f = i; break; }
            if (f > 0 && items[f].Floor == 1 && items[f].Cursed == 0 && items[f].Rarity > 0) { kind = k; idx = f; }
        }
        CheckAtLeast("a kind that can be sold in at floor 1 (found in MDATA3)", kind, 0);
        if (kind < 0) return;
        var it = items[idx];
        double kindRoll = (kind + 0.1) / kinds;
        var align = ItemAlignment.Unaligned;
        var rolls = new List<double>();
        if (it.AlignmentFlags > 1)
            for (int a = 0; a < 4; a++) if ((it.AlignmentFlags & (1 << a)) != 0) { align = (ItemAlignment)a; break; }
        double alignRoll = ((int)align + 0.5) / 4;

        // a full shelf: pass, sell in, floor, kind, pick, (alignment), then the next pass ends it
        rolls.AddRange(new[] { 0.1, 0.1, 0.5, kindRoll, 0.0 });
        if (it.AlignmentFlags > 1) rolls.Add(alignRoll);
        rolls.Add(0.99);
        var shelf = new StoreStock();
        for (int i = 0; i < StoreStock.ShelfCap; i++) shelf.Add(it.Id, align);
        var s = new ScriptedRandom(rolls.ToArray());
        shelf.Replenish(items, 50, s, deepestReached: 1);
        Check($"a full shelf of {it.Name.Trim()}: still nine, nothing else stocked",
              $"{shelf.Count(it.Id, align)} {shelf.Shelf.Count}", $"{StoreStock.ShelfCap} 1");
        Check("and the pass took no draw beyond the one item (no redraw)", s.Draws, rolls.Count);

        // the selling-in loop has no cap: 150 kinds that name nothing, then the item
        rolls = new List<double> { 0.1, 0.1, 0.5 };
        for (int i = 0; i < 150; i++) rolls.Add(0.999999);          // rounds to NumItemTypes: no such kind
        rolls.AddRange(new[] { kindRoll, 0.0 });
        if (it.AlignmentFlags > 1) rolls.Add(alignRoll);
        rolls.Add(0.99);
        shelf = new StoreStock();
        s = new ScriptedRandom(rolls.ToArray());
        shelf.Replenish(items, 50, s, deepestReached: 1);
        Check("selling in: past 150 empty draws it still lands, one on the shelf", shelf.Count(it.Id, align), 1);
        Check("selling in: every draw taken", s.Draws, rolls.Count);

        // the buying-out loop gives up after 100: pass, buy out, floor, 100 empty kinds, pass ends
        s = new ScriptedRandom(new[] { 0.1, 0.9, 0.5, 0.999999 });
        new StoreStock().Replenish(items, 50, s, deepestReached: 1);
        Check("buying out: 100 empty draws and the pass is given up (3 + 100 + the next pass)", s.Draws, 104);
    }

    /// <summary>
    /// FRMCTYG.BAS:489-561. The port drew a kind uniformly, then any monster or item of that kind at
    /// or above the tier, and gave up after 60 tries. The original's runs lean to the quest's own
    /// level, skip an item run's first entry, can land one past its end, and loop until valid -
    /// with the rare test's Rnd drawn on every pass, since VB's And does not short-circuit.
    /// </summary>
    private static void QuestTargetChecks(string dir)
    {
        Console.WriteLine("\nguild quest targets (FRMCTYG.BAS:489-561)");
        string g = PortSource("decompiled-named", "FRMCTYG.BAS").Replace("\r\n", "\n");
        Check("FRMCTYG.BAS: two times in five the monster run starts at the class head",
              g.Contains("                If  Int(Rnd * 100) < 40 Then\n                    l0112 = gv1DD2(l0116, 0)"), true);
        Check("FRMCTYG.BAS: the monster pick and its rare test, Rnd in the And",
              g.Contains("                l0118 = CInt(Rnd * (l0114 - l0112))\n                l0118 = l0118 + l0112\n                If  Monsters(l0118).Rarity < 10 And ((Rnd * 100) > 25) Then"), true);
        Check("FRMCTYG.BAS: the item pick, + 1 inside the Int",
              g.Contains("                l0128 = Int(Rnd * (l0124 - l0122) + 1)\n                l0128 = l0128 + l0122"), true);
        Check("FRMCTYG.BAS: no cap on either loop",
              g.Contains("            Loop While l0114 = -1 Or Monsters(l0114).Floor > l0110 Or l0118 = -1")
              && g.Contains("            Loop While l0128 = -1"), true);

        var mons = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        int nc = EncounterGenerator.ClassCount(mons);
        var r = EncounterGenerator.ClassRanges(mons);
        double ClassRoll(int c) => (c + 0.5) / nc;

        // ---- monsters: a class whose run is long enough to show the shape ----
        // tier 2, and the first class with a monster deeper than 2 that is not its head
        int tier = 2, cls = -1;
        for (int c = 0; c < nc && cls < 0; c++)
        {
            if (r[c, 0] < 0) continue;
            int firstAt = -1, firstDeep = -1;
            for (int i = r[c, 0]; i <= r[c, 1]; i++)
            {
                if (firstAt < 0 && mons[i].LevelFound >= tier) firstAt = i;
                if (mons[i].LevelFound > tier) { firstDeep = i; break; }
            }
            if (firstAt > r[c, 0] + 1 && firstDeep > firstAt + 1 && mons[r[c, 0]].Chance >= 10 && mons[firstAt].Chance >= 10)
                cls = c;
        }
        CheckAtLeast("a class that shows the run (found in MDATA5)", cls, 0);
        if (cls >= 0)
        {
            int head = r[cls, 0];
            int at = head; while (mons[at].LevelFound < tier) at++;
            int deep = at; while (mons[deep].LevelFound <= tier) deep++;

            // the class, 0.1 (< 40: from the head), span 0, rare test 0.1 (not turned down)
            var s = new ScriptedRandom(new[] { ClassRoll(cls), 0.1, 0.0, 0.1 });
            var m = Rules.QuestMonster(mons, tier, s);
            Check("monster: from the head with a span of 0, the class's first monster", m?.Name, mons[head].Name);
            Check("monster: four Rnd for a pass that is taken", s.Draws, 4);

            // 0.9: the run starts at the first monster at the tier, not the head
            s = new ScriptedRandom(new[] { ClassRoll(cls), 0.9, 0.0, 0.1 });
            Check("monster: otherwise the first monster at the tier, so the draw leans to the quest's level",
                  Rules.QuestMonster(mons, tier, s)?.Name, mons[at].Name);

            // the span's top: one before the first monster deeper than the tier
            s = new ScriptedRandom(new[] { ClassRoll(cls), 0.9, 0.9999, 0.1 });
            Check("monster: the run ends one before the first deeper monster", Rules.QuestMonster(mons, tier, s)?.Name, mons[deep - 1].Name);

            // and the rare test's Rnd is drawn for a common monster too: the fourth roll is spent
            // whatever the Rarity, so the pass after a turned-down rare one starts on the fifth
            s = new ScriptedRandom(new[] { ClassRoll(cls), 0.1, 0.0, 0.99 });
            Rules.QuestMonster(mons, tier, s);
            Check("monster: a common monster still spends the rare test's Rnd (VB's And)", s.Draws, 4);
        }

        // ---- items ----
        int kinds = Mdata1.ItemTypes.Count;
        int band = Rules.QuestItemBand(3);          // tier 3: Int(3 / 1.5) + 1 = 3
        int kind = -1, first = -1;
        for (int k = 1; k < kinds && kind < 0; k++)
        {
            int f = -1;
            for (int i = 0; i < items.Count; i++) if (items[i].Type == k) { f = i; break; }
            if (f < 0 || items[f].Floor > band || f + 2 >= items.Count) continue;
            var nx = items[f + 1];
            if (nx.Type == k && nx.Floor <= band && nx.Floor >= 1 && nx.Price >= 100 && nx.Cursed == 0 && nx.Rarity >= 5) { kind = k; first = f; }
        }
        CheckAtLeast("an item kind that shows the run (found in MDATA3)", kind, 0);
        if (kind >= 0)
        {
            var s = new ScriptedRandom(new[] { (kind + 0.5) / kinds, 0.0, 0.1 });
            var it = Rules.QuestItem(items, 3, s);
            Check("item: a span of 0 names the run's second item, never its first (+ 1 inside the Int)", it?.Name, items[first + 1].Name);
            Check("item: three Rnd for a pass that is taken (kind, pick, rare test)", s.Draws, 3);

            int last = first;
            while (last + 1 < items.Count - 1 && items[last + 1].Type == kind && items[last + 1].Floor <= band) last++;
            // Int(Rnd * span + 1) tops out at span: the run's last item
            s = new ScriptedRandom(new[] { (kind + 0.5) / kinds, 0.99999, 0.1 });
            Check("item: the span's top is the run's last item", Rules.QuestItem(items, 3, s)?.Name, items[last].Name);
        }

        // a run of one: the span is 0 and the pick is one past it - another kind, or deeper than the band
        int single = -1, sFirst = -1;
        for (int k = 1; k < kinds && single < 0; k++)
        {
            int f = -1;
            for (int i = 0; i < items.Count; i++) if (items[i].Type == k) { f = i; break; }
            if (f < 0 || items[f].Floor > band || f + 2 >= items.Count) continue;
            var nx = items[f + 1];
            if ((nx.Type != k || nx.Floor > band) && nx.Price >= 100 && nx.Cursed == 0 && nx.Floor >= 1) { single = k; sFirst = f; }
        }
        CheckAtLeast("a one-item run whose next record would pass (found in MDATA3)", single, 0);
        if (single >= 0)
        {
            var s = new ScriptedRandom(new[] { (single + 0.5) / kinds, 0.5, 0.1 });
            var it = Rules.QuestItem(items, 3, s);
            var nx = items[sFirst + 1];
            Check($"item: a one-item run of {items[sFirst].Name.Trim()} names the record past it",
                  it?.Name, nx.Name);
            Check("item: and that record is outside the run (another kind, or deeper than the band)",
                  nx.Type != single || nx.Floor > band, true);
        }

        // ---- no cap: every draw comes back with something, at every tier ----
        var rng = new Random(20261018);
        int none = 0, atTier = 0, drawn = 0;
        for (int t = 1; t <= 10; t++)
            for (int i = 0; i < 400; i++)
            {
                var m = Rules.QuestMonster(mons, t, rng);
                if (m == null) none++; else { drawn++; if (m.LevelFound == t) atTier++; }
                if (Rules.QuestItem(items, t, rng) == null) none++;
            }
        Check("quest targets that failed to appear in 8,000 draws (the port gave up after 60 tries)", none, 0);
        Check("the item loop's guard: every band 1-7 has a pick that can pass (by the run's own reach)",
              Enumerable.Range(1, 7).All(b => Rules.QuestItemReachable(items, b, Mdata1.ItemTypes.Count)), true);
        Check("and the guard is the run's reach, not any item: nothing reachable with no kinds",
              Rules.QuestItemReachable(items, 7, 0), false);
        CheckAtLeast("of 4,000 monster targets, those at the tier's own level", atTier, 1);
    }
}
