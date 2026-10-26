using System.Text.Json;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The second town and party batch (2026-10-19): sections 3, 4, 6 and 7 of the fidelity sweep.
    /// </summary>
    private static void Town2Checks(string dir)
    {
        Section(() => CompanionRemainsChecks());
        Section(() => SeerPluralChecks(dir));
        Section(() => JoinInFightChecks());
        Section(() => JoinLostFlagChecks());
        Section(() => MorgueOrderChecks());
        Section(() => ServiceAccessKeyChecks(dir));
        Section(() => ServiceWindowsChecks());
        Section(() => JoinLeaderDeadChecks());
        Section(() => DeadScreenHostChecks());
        Section(() => InteractRefusalsChecks());
        Section(() => JoinReviewChecks());
    }

    /// <summary>
    /// Review of the mid-fight join: the boxes are awaited with the fight going on, so the window
    /// and the party are looked at again before the party is touched; a refusal at Party.Add's own
    /// test saves the unloaded joiner as the others do; and a modifier does not keep 'a' from
    /// fn1521 case 65, which asks nothing of Shift (MAINMOD2.BAS:3444), with fn15DF taking no
    /// Ctrl or Alt A (4187-4232).
    /// </summary>
    private static void JoinReviewChecks()
    {
        Console.WriteLine("\n'a' Ask to Join after its boxes, and with a modifier (MAINMOD2.BAS:3444, 4187-4232)");
        string m2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        string tab = Between(m2, "Function fn15DF (p063A As Integer, p063C As Integer) As Integer", "End Function");
        Check("MAINMOD2.BAS: fn15DF's Ctrl letters are L B M S R C G I P O, its Alt keys the digits - no A",
              tab.Contains("Case mc05DA") && !tab.Contains("Case mc0576") && tab.Contains("                Case 48 To 57"), true);
        string dw = PortSource("Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
        string join = Between(dw, "private async void AskToJoin()", "private async System.Threading.Tasks.Task<bool> OfferPendingRaise(");
        int guard = join.IndexOf("if (_ended || _party.Members.Count == 0 || _party.Members.Any(m => m.Name.TrimEnd() == who.Name.TrimEnd())) return;");
        Check("the port: after the boxes the window and the party are looked at again, before the party is touched",
              join.Contains("if (pick < 0 || _ended) return;") && join.Contains("if (said == null || _ended) return;")
              && guard > 0 && guard < join.IndexOf("SavePos();") && join.IndexOf("SavePos();") < join.IndexOf("_party.Add(who);"), true);
        Check("the port: Party.Add's own refusal saves the unloaded joiner too",
              join.Contains("if (_party.WhyCannotJoin(who) is string refused) { Say(refused); who.Save(whoFile); return; }"), true);
        Check("the port: in the dungeon any modifier still asks", dw.Contains("            case Key.A:\n                AskToJoin(); e.Handled = true; return;"), true);
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        Check("the port: in town Alt+A asks as well as a and Ctrl+A",
              city.Contains("            if (k == Avalonia.Input.Key.A) { AskToJoin(); e.Handled = true; return; }")
              && city.Contains("        else if (k == Avalonia.Input.Key.A) { AskToJoin(); e.Handled = true; return; }"), true);
    }

    /// <summary>
    /// frmInteract's per-tag refusals ("Yourself?  HA!", "Character is not here!", ...) sit in
    /// cmdOK_Click's txtQuestion branch, reached only with cmbCharacter hidden, and Form_Resize shows
    /// cmbCharacter for every tag that has them (FRMINTER.BAS:154-250, 365-455): the original never
    /// says them either. The audit had filed them as lines the port does not reach.
    /// </summary>
    private static void InteractRefusalsChecks()
    {
        Console.WriteLine("\nfrmInteract's per-tag refusals are not reached in the original either (FRMINTER.BAS:66-110, 365-455)");
        string f = PortSource("decompiled-named", "FRMINTER.BAS").Replace("\r\n", "\n");
        string ok = Between(f, "Sub cmdOK_Click ()", "End Sub");
        int comboExit = ok.IndexOf("    If  cmbCharacter.Visible = True Then\n        m001C = cmbCharacter.ListIndex\n        frmInteract.Hide\n        m001E = True\n        Exit Sub\n    End If");
        int refusals = ok.IndexOf("txtQuestion.Text = \"Yourself?  HA!\"");
        Check("cmdOK_Click: with cmbCharacter shown it leaves before the refusals", comboExit > 0 && refusals > comboExit, true);
        string resize = Between(f, "Sub Form_Resize ()", "End Sub");
        Check("Form_Resize: the list shown when anyone is on it",
              resize.Contains("    If  cmbCharacter.ListCount > False Then\n        cmbCharacter.ListIndex = False\n        cmbCharacter.Visible = True"), true);
        Check("Form_Resize: and shown greyed when nobody is, for every tag that has refusals",
              resize.Contains("        Case \"PICKUP\", \"SPELLRAISE\", \"LOCATE\", \"FETCH\", \"JOIN\", \"LOADCHAR\", \"SPELL\", \"SPELLHEAL\", \"GIVE\"\n            If  cmbCharacter.ListCount = False Then")
              && resize.Contains("                cmbCharacter.Visible = True\n                cmbCharacter.Enabled = False\n                cmdOK.Enabled = False"), true);
        Check("the txtQuestion branch's name tags are all among them",
              ok.Contains("            Case \"PICKUP\", \"SPELL\", \"SPELLHEAL\", \"SPELLRAISE\", \"LOCATE\", \"GIVE\", \"FETCH\", \"JOIN\"")
              && ok.Contains("            Case \"LOADCHAR\""), true);
    }

    /// <summary>
    /// sub14E0 shows frmDungeonMain for the dead and frmCityMain for the living in town, never both
    /// (MAINMOD2.BAS:3170-3180); sub14CC turns the one into the other after a raise (3150-3152).
    /// </summary>
    private static void DeadScreenHostChecks()
    {
        Console.WriteLine("\nthe dead screen stands where frmDungeonMain does, without the town (MAINMOD2.BAS:3170-3180)");
        string m2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        Check("MAINMOD2.BAS: the town only for one alive at level 0, else the dungeon form",
              m2.Contains("    If  Chars(Me).DungeonLevel = 0 And Chars(Me).DeathState = False Then\n        frmCityMain.Show\n    Else\n        Load frmDungeonMain"), true);
        Check("MAINMOD2.BAS: after a raise at level 0, the dungeon form unloaded and the town shown",
              m2.Contains("        Unload frmDungeonMain\n        frmCityMain.Show\n    End If"), true);
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        int dead = city.IndexOf("private void ShowDead()");
        int open = dead < 0 ? -1 : city.IndexOf("OpenService(\"Dungeon (\" + _ch.Name.TrimEnd() + \")\", GameWindow.Dungeon);", dead);
        int hide = dead < 0 ? -1 : city.IndexOf("_cityChild.IsVisible = false;", dead);
        Check("the port: the dead screen's opening hides the town", dead > 0 && open > dead && hide > open && hide - open < 400, true);
        Check("the port: the dead screen's closing shows it again",
              city.Contains("if (closing.Key == GameWindow.Dungeon) { _deadScreenAt = null; _cityChild.IsVisible = true; }"), true);
        // sub0930's screen is a lone character's: with anyone else loaded it leaves after frmParty's
        // UPDATE (3035-3038), which unloads a member at nought from a party (FRMGROUP.BAS:321-346)
        Check("MAINMOD2.BAS: sub0930 stops for a party before its screen",
              m2.Contains("    If  UBound(Chars) > False Then\n        frmParty!lblCall = \"UPDATE\"\n        Exit Sub\n    End If\n    mdrcharStats False, 50\n    frmDungeonMain!timCombat.Enabled = False"), true);
        string grp = PortSource("decompiled-named", "FRMGROUP.BAS").Replace("\r\n", "\n");
        Check("FRMGROUP.BAS: the party list's UPDATE unloads a member at nought",
              grp.Contains("            If  Chars(l0118).Hits <= False And UBound(Chars) > m0052 Then") && grp.Contains("                mdrcharUnload l0118"), true);
        string dun = PortSource("decompiled-named", "FRMDUNM.BAS").Replace("\r\n", "\n");
        Check("FRMDUNM.BAS: cmdWait does anything only for a dead character in command",
              dun.Contains("Sub cmdWait_Click ()") && Between(dun, "Sub cmdWait_Click ()", "End Sub").Contains("    If  Chars(Me).DeathState > False Then"), true);
        string svc = PortSource("Mordor.App", "CityWindow.Services.cs").Replace("\r\n", "\n");
        Check("the port: the dead screen is owed only to a character dead and alone",
              svc.Contains("private bool DeadScreenOwed => _ch.IsDead && _party.Size == 1;"), true);
        Check("the port: every refresh takes down a dead screen no longer owed",
              Between(city, "private void RefreshSummary()", "\n    }").Contains("SyncDeadScreen();")
              && svc.Contains("if (DeadScreenOwed || OpenServiceFor(GameWindow.Dungeon) is not Svc dead) return;"), true);
        Check("the port: the pictures and a closing ask for the dead screen only when it is owed",
              !city.Contains("if (_ch.IsDead) { ShowDead(); return; }") && !city.Contains("if (_ch.IsDead) ShowDead();")
              && !PortSource("Mordor.App", "CityWindow.Store.cs").Contains("if (_ch.IsDead) { ShowDead(); return; }"), true);
        Check("the port: Visit the Morgue is the dead one's, and nobody's in company",
              city.Contains("var deadOne = _ch;") && city.Contains("if (!ReferenceEquals(_ch, deadOne) || !DeadScreenOwed) return;"), true);
    }

    /// <summary>
    /// The join case asks first whether Chars(m00A6) is alive, and m00A6 is the constant 0
    /// (MAINMOD2.BAS:17, 3446): the party's first member. The dungeon had asked about the one in
    /// command, the city nothing.
    /// </summary>
    private static void JoinLeaderDeadChecks()
    {
        Console.WriteLine("\n'a' Ask to Join asks whether the first member is alive (MAINMOD2.BAS:17, 3446)");
        string m2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        Check("MAINMOD2.BAS: m00A6 is 0, and the join case tests it first",
              m2.Contains("\nConst m00A6 = 0 ' &H0%\n")
              && m2.Contains("        Case mc0576\n            fn1521 = True\n            If  Chars(m00A6).DeathState = False Then"), true);
        foreach (string file in new[] { "DungeonWindow.axaml.cs", "CityWindow.axaml.cs" })
        {
            string src = PortSource("Mordor.App", file).Replace("\r\n", "\n");
            int at = src.IndexOf("private async void AskToJoin()");
            int gate = at < 0 ? -1 : src.IndexOf("if (_party.Members[0].IsDead) return;", at);
            int full = at < 0 ? -1 : src.IndexOf("Your party is too full!", at);
            Check($"the port, {file}: the first member's death is asked about before anything else", at > 0 && gate > at && gate < full, true);
        }
    }

    /// <summary>
    /// frmCityMain shows each service modeless (FRMCTYM.BAS:183-203), so they can be up together and
    /// hear each other: the deposit's Store UPDATE (FRMCITYB.BAS:180), the Party Options' Guild UPDATE
    /// (64), charPay's Bank GOLD (MAINMOD2.BAS:708, 5616-5620). The port drew them all in one window.
    /// Mordor.UiCheck drives the windows themselves.
    /// </summary>
    private static void ServiceWindowsChecks()
    {
        Console.WriteLine("\nthe town's services are windows of their own, up together (FRMCTYM.BAS:183-203)");
        string N(string s) => s.Replace("\r\n", "\n");
        string b = N(PortSource("decompiled-named", "FRMCITYB.BAS"));
        string m2 = N(PortSource("decompiled-named", "MAINMOD2.BAS"));
        Check("FRMCITYB.BAS: the Party Options end by telling an open Guild",
              b.Contains("            sub2884\n            mdrcharStats True, 10\n            If  fn03BD(frmCityGuild) Then frmCityGuild!lblCall.Caption = \"UPDATE\""), true);
        Check("FRMCITYB.BAS: \"GOLD\" is sub2884 alone",
              b.Contains("            Case \"GOLD\"\n                sub2884\n            Case Else"), true);
        Check("MAINMOD2.BAS: charPay ends in mdrcharStats 10, which in town tells the Bank \"GOLD\"",
              m2.Contains("        charPay = True\n    End If\n    mdrcharStats False, 10\nEnd Function")
              && m2.Contains("            If  fn03BD(frmCityBank) Then\n                frmCityBank!lblCall = \"GOLD\""), true);
        string city = N(PortSource("Mordor.App", "CityWindow.axaml.cs"));
        string store = N(PortSource("Mordor.App", "CityWindow.Store.cs"));
        string svc = N(PortSource("Mordor.App", "CityWindow.Services.cs"));
        Check("the port: a service opens in its own window, or the one it already has",
              city.Contains("        _svc = WindowFor(which);") && svc.Contains("if (OpenServiceFor(which.Value) is Svc open) return open;")
              && svc.Contains("?? NewServiceWindow();"), true);
        int bringUps = new[] { "GeneralStore" }.Count(k => store.Contains($"if (s != null && BringUpOpenService(GameWindow.{k})) return;"))
                     + new[] { "Guild", "Bank", "Morgue", "Seer", "CompanionStore" }.Count(k => city.Contains($"if (s != null && BringUpOpenService(GameWindow.{k})) return;"));
        Check("the port: each of the six pictures brings an open service forward instead of building it again", bringUps, 6);
        Check("the port: the Bank's three Party Options tell an open Guild",
              System.Text.RegularExpressions.Regex.Matches(city, "_guildUpdate\\?\\.Invoke\\(\\);   // \"If fn03BD\\(frmCityGuild\\)").Count, 3);
        Check("the port: a charge paid tells an open Bank GOLD", city.Contains("if (paid) _bankGold?.Invoke();"), true);
        Check("the port: the deposit's Store UPDATE has no single window standing in its way",
              !city.Contains("Here they are pages of the one service window"), true);
    }

    /// <summary>
    /// The service forms' access keys: each button caption's "&amp;" letter in MORDOR.EXE's form data,
    /// pressed with Alt while its form is the active window. The port had wired Exit's alone.
    /// </summary>
    private static void ServiceAccessKeyChecks(string dir)
    {
        Console.WriteLine("\nthe service forms' access keys are their captions' own (MORDOR.EXE form data)");
        string exePath = Path.Combine(dir, "MORDOR.EXE");
        if (!File.Exists(exePath)) { Console.WriteLine("  FAIL  MORDOR.EXE missing"); _failed++; return; }
        string exe = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(exePath));
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        string store = PortSource("Mordor.App", "CityWindow.Store.cs").Replace("\r\n", "\n");
        var keys = new (string Src, string Caption)[]
        {
            ("store", "&BUY"), ("store", "IN&FO"), ("store", "&Sell"), ("store", "&ID"), ("store", "I&nfo"),
            ("store", "&Combine"), ("store", "Un&curse"),
            ("city", "&Make Level"), ("city", "Exp. &Info"), ("city", "&Visit"), ("city", "&Read Guild Log"),
            ("city", "Visit the &Library"), ("city", "&Clean Guild Log"),
            ("city", "&Info"), ("city", "P&ool && Deposit"), ("city", "&Party Deposit"), ("city", "Party Pool && &Deposit"),
            ("city", "&Raise Character"), ("city", "&Grab Body"), ("city", "&Hire Rescuers"),
            ("city", "&Character"), ("city", "&Monster"), ("city", "&Item"),
            ("city", "&Bind"), ("city", "&BUY"), ("city", "IN&FO"), ("city", "&Sell"), ("city", "&ID"), ("city", "I&nfo"),
        };
        foreach (var (src, caption) in keys)
        {
            int amp = caption.Replace("&&", "\u0001").IndexOf('&');
            char letter = char.ToUpperInvariant(caption.Replace("&&", "\u0001")[amp + 1]);
            string text = src == "store" ? store : city;
            string tail = $"), '{letter}');   // \"{caption}\"";
            bool wired = text.Split('\n').Any(l => l.Contains("FormPage.Access(") &&
                l.EndsWith(tail, StringComparison.OrdinalIgnoreCase) && char.ToUpperInvariant(l[l.LastIndexOf(tail, StringComparison.OrdinalIgnoreCase) + 4]) == letter);
            Check($"\"{caption}\" is in the form data, and Alt+{letter} presses it", exe.Contains(caption) && wired, true);
        }
        string g = PortSource("decompiled-named", "FRMCTYG.BAS");
        string mo = PortSource("decompiled-named", "FRMMORG.BAS");
        Check("FRMCTYG.BAS / FRMMORG.BAS: Join and the companion raise are recaptioned without an access key",
              g.Contains("cmdJoin.Caption = \"Re-Acquaint\"") && g.Contains("cmdJoin.Caption = \"Join\"")
              && mo.Contains("cmdCommand(mc001E).Caption = \"Raise \" + fn128B(Me, l00CC)"), true);
        Check("the port: the keys are answered from the open service's page, Alt and a letter alone",
              city.Contains("if (ServiceAccessKey(e)) { e.Handled = true; return; }")
              && city.Contains("GetVisualDescendants(active.Panel).OfType<Button>()")
              && city.Contains("b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();")
              && city.Contains("if (answer.Count == 1) answer[0].RaiseEvent("), true);
    }

    /// <summary>
    /// MorgueList is filled by walking the character file's records (mdrmazGetChars, MAZEMOD.BAS:203-224),
    /// and who is in the room walks it in that order (278). The port's stand-in read the save folder in
    /// whatever order it gave.
    /// </summary>
    private static void MorgueOrderChecks()
    {
        Console.WriteLine("\nthe port's MorgueList is in record order (MAZEMOD.BAS:203-224, 278)");
        string mz = PortSource("decompiled-named", "MAZEMOD.BAS").Replace("\r\n", "\n");
        Check("MAZEMOD.BAS: the room walk goes through MorgueList from the first record",
              mz.Contains("    For l010E = 0 To UBound(MorgueList)\n        If  MorgueList(l010E).Id = Chars(gv1A82).M5962 Then"), true);

        string dir = Path.Combine(Path.GetTempPath(), "mordor-selfcheck-morgue-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            foreach (string n in new[] { "Zed", "Abe", "Moe" })
            {
                new Character { Name = n, Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } } }
                    .Save(Path.Combine(dir, n + ".json"));
                Roster.Added(dir, n, Array.Empty<string>());
            }
            string order = string.Join(",", Character.LoadAllFrom(dir).Select(c => c.Name));
            Check("LoadAllFrom gives the characters in the order they were made, not by file name", order, "Zed,Abe,Moe");
            var party = new List<Character> { new() { Name = "Moe" } };
            string walk = string.Join(",", LookAround.Roster(party, Character.LoadAllFrom(dir)).Select(c => c.Name));
            Check("... and so does who-is-here's roster, the party's live record in its own place", walk, "Zed,Abe,Moe");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>
    /// The join case calls <c>sub0848 True</c> once the newcomer is loaded and found not opposed
    /// (MAINMOD2.BAS:3470-3477): the newcomer takes Chars(0)'s three lost flags (MULTIMOD.BAS:454-458).
    /// The dead refusals after it unload them, and mdrcharUnload saves the record so.
    /// </summary>
    private static void JoinLostFlagChecks()
    {
        Console.WriteLine("\na newcomer takes the first member's lost flags (sub0848 True, MAINMOD2.BAS:3477)");
        string m2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        Check("MAINMOD2.BAS: sub0848 True after the alignment loop, before the dead test",
              m2.Contains("                        Next l0542\n                        sub0848 True\n                        If  Chars(UBound(Chars)).DeathState <> False Then"), true);
        string mm = PortSource("decompiled-named", "MULTIMOD.BAS").Replace("\r\n", "\n");
        Check("MULTIMOD.BAS: sub0848 True copies M59F4 0-2 from Chars(m0022) to the newcomer alone",
              mm.Contains("    If  p00D0 Then\n        Chars(UBound(Chars)).M59F4(m00D4) = Chars(m0022).M59F4(m00D4)\n        Chars(UBound(Chars)).M59F4(mc00D6) = Chars(m0022).M59F4(mc00D6)\n        Chars(UBound(Chars)).M59F4(mc00D8) = Chars(m0022).M59F4(mc00D8)\n        Exit Sub"), true);

        var lead = new Character { Name = "Lead", LostLevel = true, LostPosition = false, LostFacing = true };
        var joiner = new Character { Name = "New", LostLevel = false, LostPosition = true, LostFacing = false };
        Party.ShareLostFlags(lead, joiner);
        Check("the newcomer's flags are the first member's", (joiner.LostLevel, joiner.LostPosition, joiner.LostFacing), (true, false, true));

        foreach (var (file, say) in new[] { ("DungeonWindow.axaml.cs", "Say"), ("CityWindow.axaml.cs", "Log") })
        {
            string src = PortSource("Mordor.App", file).Replace("\r\n", "\n");
            int at = src.IndexOf("private async void AskToJoin()");
            int opposed = at < 0 ? -1 : src.IndexOf("is oppositely aligned to one or more members of the party!", at);
            int share = at < 0 ? -1 : src.IndexOf("Party.ShareLostFlags(_party.Members[0], who);", at);
            int dead = at < 0 ? -1 : src.IndexOf("if (who.IsDead)", at);
            int deadLine = at < 0 ? -1 : src.IndexOf(say + "($\"\\\"{who.Name.TrimEnd()}\\\" is Dead!", at);
            Check($"the port, {file}: the flags pass after the alignment refusal and before the dead test",
                  at > 0 && opposed > at && share > opposed && dead > share, true);
            Check($"the port, {file}: \"is Dead!\" saves the unloaded record",
                  deadLine > 0 && src.IndexOf("who.Save(whoFile);", deadLine) is int sv && sv > deadLine && sv - deadLine < 200, true);
        }
    }

    /// <summary>
    /// 'a' Ask to Join is fn1521 case mc0576, which no combat test stands before, and which itself
    /// handles a fight: the dead refused while monsters are here, the living set up by sub0BDE
    /// (MAINMOD2.BAS:3444-3512). The port kept the key from doing anything during a fight.
    /// </summary>
    private static void JoinInFightChecks()
    {
        Console.WriteLine("\n'a' Ask to Join works during a fight (MAINMOD2.BAS:3444-3512)");
        string m2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        Check("MAINMOD2.BAS: the join case refuses the dead in a fight and sets the living up for it",
              m2.Contains("                            If  fn0EEF() > False Then\n                                sub085B \"Cannot raise character in Combat!\"")
              && m2.Contains("                        If  fn0EEF() > False Then sub0BDE UBound(Chars)"), true);
        string dw = PortSource("Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
        Check("the port: 'a' is not held back while a fight is on",
              dw.Contains("            case Key.A:\n                AskToJoin(); e.Handled = true; return;")
              && !dw.Contains("case Key.A when"), true);
        int at = dw.IndexOf("private async void AskToJoin()");
        Check("the port: the join itself still refuses a dead joiner with monsters here, and calls sub0BDE",
              at > 0 && dw.IndexOf("if (MonstersHere) { Say(\"Cannot raise character in Combat!\");", at) > at
              && dw.IndexOf("Sub0BDE(who);", at) > at, true);
    }

    /// <summary>
    /// LOCATE MONSTER tries a monster's plural as its second name (FRMSEEK.BAS:66-86): a name the
    /// typing does not begin is tried as <c>monsterNameFor(Me, 2, l0040)</c>, Ref and KnownAs the
    /// record and IdLevel nought. The port matched the plain names only.
    /// </summary>
    private static void SeerPluralChecks(string dir)
    {
        Console.WriteLine("\nthe Seer's LOCATE MONSTER also reads the plural (FRMSEEK.BAS:66-86)");
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        string seek = PortSource("decompiled-named", "FRMSEEK.BAS").Replace("\r\n", "\n");
        Check("FRMSEEK.BAS: monsters from record 1, items from record 0",
              seek.Contains("            For l0036 = 1 To gv1BF6 - 1\n") && seek.Contains("            For l0036 = False To gv1BEC - 1\n"), true);
        Check("FRMSEEK.BAS: the second try is the plural, IdLevel left at nought",
              seek.Contains("                Else\n                    l0040.Ref = l0036\n                    l0040.KnownAs = l0036\n                    If  UCase(l002E) = Left(UCase(monsterNameFor(Me, 2, l0040)), Len(l002E)) Then")
              && !seek.Contains("l0040.IdLevel"), true);

        var pool = monsters.Skip(1).ToList();
        var names = pool.Select(m => m.Name).ToList();
        var plurals = pool.Select(m => EncounterGenerator.NameFor(m, null, 2, 0)).ToList();
        int kobold = names.FindIndex(n => n == "Kobold");
        Check("the plural of Kobold, as fn0E23 makes it", kobold >= 0 ? plurals[kobold] : "", "Kobolds");
        Check("\"Kobolds\" finds the Kobold through its plural", Seer.Match("Kobolds", names, plurals), (Seer.Found.One, kobold));
        Check("... which the plain names alone did not", Seer.Match("Kobolds", names).found, Seer.Found.None);
        Check("\"Kobold\" still finds it by its own name", Seer.Match("Kobold", names, plurals).found, Seer.Found.One);
        var itemNames = items.Select(i => i.Name).ToList();
        Check("LOCATE ITEM reads record 0 too: \"Hands\" is found", Seer.Match("Hands", itemNames), (Seer.Found.One, 0));

        string city = PortSource("Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        Check("the port: the Seer's monster search passes the plurals, the item search every record",
              city.Contains("var (found, idx) = Seer.Match(typed, names, plurals);")
              && city.Contains("var pool = monster ? _monsters.Skip(1).ToList() : null;")
              && city.Contains(": _items.Select(i => i.Name).ToList();"), true);
    }

    /// <summary>
    /// An empty companion slot keeps its last occupant's record, only the monster index set to -1
    /// (fn1163 writes no name, MAINMOD2.BAS:383-400; only mdrcharShowCompanions blanks it, 4745), and
    /// the character file keeps all four records (MAIN.TXT:339). The port forgot it on a save.
    /// </summary>
    private static void CompanionRemainsChecks()
    {
        Console.WriteLine("\nan empty companion slot keeps what was left in it, through a save (MAINMOD2.BAS:383-400)");
        string m2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        Check("MAINMOD2.BAS: fn1163 fills a slot without writing its name",
              m2.Contains("            Chars(pv00D2).Companions(l00DC).Hits = p00D8\n")
              && !m2.Contains("Chars(pv00D2).Companions(l00DC).Name ="), true);

        var slots = new CompanionSlots();
        slots[0] = new Companion { Name = "Rex", Hits = 5, MaxHits = 9, MonsterId = 3, Ref = 3 };
        slots[1] = new Companion { Name = "Fluffy", Hits = 7, MaxHits = 12, MonsterId = 4, Ref = 4, BindingLevel = 10 };
        slots.Release(1);
        string json = JsonSerializer.Serialize(slots);
        var back = JsonSerializer.Deserialize<CompanionSlots>(json)!;
        Check("after a save and load, the emptied slot is still empty", back[1] == null, true);
        Check("... and its record keeps the name it was left", back.Left(1).Name, "Fluffy");
        Check("... and the hits", back.Left(1).Hits, 7);
        Check("... the untouched slots are written as null, as before", json.Contains("null,null"), true);
        Check("... the companion in play is not marked", back[0]!.Vacant, false);
        int at = back.Add(new Companion { Name = "", Hits = 3, MaxHits = 3, MonsterId = 8, Ref = 8 }, keepSlotName: true);
        Check("the next creature charmed in takes the slot and its old name, as fn1163 leaves it", (at, back[1]?.Name), (1, "Fluffy"));
        var old = JsonSerializer.Deserialize<CompanionSlots>("[{\"Name\":\"Rex\",\"Hits\":5,\"MonsterId\":3,\"Ref\":3},null,null,null]")!;
        Check("a save from before reads its holes back as the creator's blank", old.Left(1).Name, " ");
    }
}
