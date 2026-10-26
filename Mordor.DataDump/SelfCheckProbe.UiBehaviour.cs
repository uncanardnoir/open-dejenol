using System.Text.RegularExpressions;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// UI behaviour held to the source (the 2026-10-18 fidelity sweep, section 8): what the windows
    /// do on their own when the game switches character, shows a page, and the like. Each check
    /// reads the BAS for the rule and the port's source for the place it is kept.
    /// </summary>
    private static void UiBehaviourChecks()
    {
        Console.WriteLine("\nUI behaviour: the Spell Type box is emptied where the original empties it");
        string multi = PortSource("decompiled-named", "MULTIMOD.BAS").Replace("\r\n", "\n");
        string guild = PortSource("decompiled-named", "FRMCTYG.BAS").Replace("\r\n", "\n");
        string group = PortSource("decompiled-named", "FRMGROUP.BAS").Replace("\r\n", "\n");
        string dungeonMdi = PortSource("Mordor.App", "DungeonWindow.Mdi.cs").Replace("\r\n", "\n");
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        string spellPage = PortSource("Mordor.App", "SpellPage.cs").Replace("\r\n", "\n");

        Check("MULTIMOD: sub1536 empties cmbClass and txtSpell, then sub1658 (675-677)",
              Regex.IsMatch(multi, @"frmCharStat3!cmbClass\.Clear\n\s*frmCharStat3!txtSpell\.Text = """"\n\s*sub1658\n"), true);
        Check("FRMCTYG: cmdJoin empties cmbClass, then sub1658 (163-164)",
              Regex.IsMatch(guild, @"mdrcharStats False, 50\n\s*frmCharStat3!cmbClass\.Clear\n\s*sub1658\n"), true);
        Check("FRMGROUP: lblName_Click switches only to another member (175-179)",
              Regex.IsMatch(group, @"Sub lblName_Click \(Index%\)\n\s*If  Index <> Me Then\n\s*mdrmulSwitch Index"), true);

        Check("SpellPage.Clear empties the box, which FillClasses then fills from the first class",
              spellPage.Contains("public static void Clear(ComboBox box) => box.DataContext = null;")
              && spellPage.Contains("bool sameChar = ReferenceEquals(box.DataContext, c);")
              && spellPage.Contains("int keep = sameChar ? Kind(box) : -1;"), true);
        string sub1536 = Body(dungeonMdi, "private void Sub1536(int which)");
        int clear = sub1536.IndexOf("SpellPage.Clear(SheetClassBox);", StringComparison.Ordinal);
        int text = sub1536.IndexOf("SheetSpellText.Text = \"\";", StringComparison.Ordinal);
        int fill = sub1536.IndexOf("SwitchEverythingTo(which);", StringComparison.Ordinal);
        Check("dungeon: sub1536 empties the box and txtSpell before the repaint fills it",
              clear >= 0 && text > clear && fill > text, true);
        string switched = Body(city, "private void SwitchedTo()");
        clear = switched.IndexOf("SpellPage.Clear(ClassBox);", StringComparison.Ordinal);
        text = switched.IndexOf("SpellText.Text = \"\";", StringComparison.Ordinal);
        fill = switched.IndexOf("RefreshSummary();", StringComparison.Ordinal);
        Check("city: a switch (mdrmulSwitch's sub1536) empties the box and txtSpell before the repaint",
              clear >= 0 && text > clear && fill > text, true);
        Check("city: a click, Alt+1..4 or a companion dropped on the one in command switches nothing",
              Body(city, "private void SwitchIfOther(int slot)").Contains("if (slot == _party.SelectedIndex || !_party.Select(slot)) return;")
              && !city.Contains("_party.Select(slot)) SwitchedTo();"), true);
        Check("city: joining a guild empties the box before the repaint (FRMCTYG.BAS:163)",
              Regex.IsMatch(city, @"SpellPage\.Clear\(ClassBox\);\n\s*Save\(\); RefreshSummary\(\); ShowGuild\(null, null!\);"), true);

        Console.WriteLine("\nUI behaviour: the character window and its page keys, as mdrcharSetInfo and fn1521 have them");
        string main2 = PortSource("decompiled-named", "MAINMOD2.BAS").Replace("\r\n", "\n");
        string mdi = PortSource("Mordor.App", "Mdi.cs").Replace("\r\n", "\n");
        string cityMdi = PortSource("Mordor.App", "CityWindow.Mdi.cs").Replace("\r\n", "\n");
        string dungeon = PortSource("Mordor.App", "DungeonWindow.axaml.cs").Replace("\r\n", "\n");
        Check("MAINMOD2: mdrcharSetInfo puts any WindowState back to normal first (4598)",
              Regex.IsMatch(main2, @"Sub mdrcharSetInfo [^\n]*\n(?:[^\n]*\n){4}\s*If  frmCharStat1\.WindowState <> m05B8 Then frmCharStat1\.WindowState = m05B8\n"), true);
        Check("MAINMOD2: F3 and F9 set the tab and send UPDATE, no mdrcharSetInfo (3535-3538, 3564-3567)",
              Regex.IsMatch(main2, @"Case mc058A\n\s*fn1521 = True\n\s*frmCharStat1!tabStatTab\.P\?C01E = m058C\n\s*frmCharStat1!lblCall = ""UPDATE""\n")
              && Regex.IsMatch(main2, @"Case mc05A0\n\s*fn1521 = True\n\s*frmCharStat1!tabStatTab\.P\?C01E = mc05A2\n\s*frmCharStat1!lblCall = ""UPDATE""\n"), true);
        // measured in the original (2026-10-25): Ctrl+L on a minimised character window restored it as
        // the active, topmost child; Ctrl+G on a maximised one brought it back to its normal size
        Check("MdiChild.SetNormal: minimised comes back as the active child at the front, maximised back and to the front; normal is left in place",
              Body(mdi, "public void SetNormal()").Replace(" ", "").Replace("\n", "")
              == "{if(!IsVisible)Reopen();if(!Minimised&&!Maximised)return;boolfromIcon=Minimised;Minimised=false;Maximised=false;if(fromIcon)Activate();elseReopen();}", true);
        Check("MdiChild.Activate: to the front and the active child, unless a click would not make it so",
              Body(mdi, "public void Activate()").Replace(" ", "").Replace("\n", "")
              == "{Reopen();if(Activates&&HostisMdiHosth)h.Active=this;}", true);
        Check("MdiHost.Active: a change of active child is its form's Form_Activate (Activated)",
              Regex.IsMatch(mdi, @"if \(ReferenceEquals\(_active, value\)\) return;\n\s*_active = value;\n\s*if \(value != null\) Activated\?\.Invoke\(value\);"), true);
        Check("dungeon: Form_Activate of the character windows hangs on any change of active child, not only a click",
              Body(dungeonMdi, "private void WireCharacterFormActivate()").Contains("Host.Activated += hit =>")
              && !Body(dungeonMdi, "private void WireCharacterFormActivate()").Contains("PointerPressedEvent"), true);
        int Count(string s, string what) => Regex.Matches(s, Regex.Escape(what)).Count;
        // every Info: mdrcharSetInfo and then picTabPicture.SetFocus, which activates the window
        Check("the Info pages (item, spell, companion; pack, Store, Bank, Confinement) restore a maximised window too",
              (Count(cityMdi, "_characterChild.SetNormal();   // mdrcharSetInfo"), Count(city, "_characterChild.SetNormal();   // mdrcharSetInfo"),
               Count(dungeon, "_characterChild.SetNormal();   // mdrcharSetInfo")), (3, 2, 4));
        Check("and picTabPicture.SetFocus makes the character window the active child, at its normal size too",
              (Count(cityMdi, "activates it\n        _characterChild.Activate();"), Count(city, "activates it\n                _characterChild.Activate();") + Count(city, "activates it\n            _characterChild.Activate();"),
               Count(dungeon, "activates it\n        _characterChild.Activate();") + Count(dungeon, "activates it\n                _characterChild.Activate();")), (3, 2, 4));
        Check("city: the Store's, the Bank's and the Confinement's Info take the keyboard out of the service's box (FRMCTYS.BAS:166, FRMCITYB.BAS:81, FRMCITYC.BAS:202)",
              Body(cityMdi, "private void ShowItemInMisc(").Contains("_characterChild.Activate();\n        Focus();")
              && city.Contains("_characterChild.Activate();\n            Focus();   // picTabPicture.SetFocus: the keyboard leaves the Confinement's box"), true);
        foreach (var (src, where, pre) in new[] { (dungeon, "dungeon", "case Key."), (city, "city", "case Avalonia.Input.Key.") })
        {
            Check($"{where}: F2, F4, F7 go through mdrcharSetInfo; F3 and F9 only set the tab",
                  src.Contains(pre + "F2: SetInfoTab(CharTab.Look);") && src.Contains(pre + "F4: SetInfoTab(CharTab.Resist);")
                  && src.Contains(pre + "F7: SetInfoTab(CharTab.Guild);") && src.Contains(pre + "F3: SetTabOnly(CharTab.Stats);")
                  && src.Contains(pre + "F9: SetTabOnly(CharTab.Buffers);") && src.Contains("SetInfoTab(CharTab.Char);   // fn1521 case mc0596"), true);
        }
        Check("dungeon: Ctrl+B, Ctrl+M and Ctrl+S only set the tab (fn15DF, MAINMOD2.BAS:4193-4201)",
              dungeon.Contains("case Key.B when e.KeyModifiers.HasFlag(KeyModifiers.Control): SetTabOnly(CharTab.Buffers);")
              && dungeon.Contains("case Key.M when e.KeyModifiers.HasFlag(KeyModifiers.Control): SetTabOnly(CharTab.Misc);")
              && dungeon.Contains("case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Control): SetTabOnly(CharTab.Stats);"), true);
        Check("city: Ctrl+B, Ctrl+M and Ctrl+S only set the tab (fn15DF, MAINMOD2.BAS:4193-4201)",
              city.Contains("if (tab is CharTab.Stats or CharTab.Buffers or CharTab.Misc) { SetTabOnly(tab.Value); e.Handled = true; return; }"), true);
        foreach (var (src, where) in new[] { (dungeonMdi, "dungeon"), (cityMdi, "city") })
        {
            Check($"{where}: SetInfoTab restores without raising; SetTabOnly neither",
                  Body(src, "private void SetInfoTab(CharTab tab)").Contains("_characterChild.SetNormal();")
                  && !Body(src, "private void SetInfoTab(CharTab tab)").Contains("Reopen")
                  && Body(src, "private void SetTabOnly(CharTab tab)").Contains("if (!_characterChild.IsVisible) _characterChild.Reopen();")
                  && !Body(src, "private void SetTabOnly(CharTab tab)").Contains("Minimised"), true);
        }
        // Ctrl+L/R/C/G: setting CurrTab raises the IndexTab's Click (measured in the original,
        // 2026-10-25), so they go through mdrcharSetInfo; no SetFocus follows, so a window at its
        // normal size is not raised (Ctrl+C on one behind the city left the city on top)
        Check("dungeon: Ctrl+L, Ctrl+R, Ctrl+C and Ctrl+G restore without raising",
              dungeonMdi.Contains("private void ShowCharacterTab(CharTab tab) => SetInfoTab(tab);"), true);
        string showTab = Body(cityMdi, "private void ShowCharacterTab(CharTab tab, bool keepFocus = false)");
        Check("city: Ctrl+L, Ctrl+R, Ctrl+C and Ctrl+G restore without raising, from a service's box too",
              showTab.Contains("_characterChild.SetNormal();") && !showTab.Contains("Reopen")
              && showTab.Contains("if (tab is not (CharTab.Look or CharTab.Resist or CharTab.Char or CharTab.Guild)) return;")
              && showTab.IndexOf("if (keepFocus", StringComparison.Ordinal) > showTab.IndexOf("_characterChild.SetNormal();", StringComparison.Ordinal), true);
        Check("city: a service's box keeps the focus unless the restore made the character window the active one",
              showTab.Contains("if (keepFocus && ReferenceEquals(Host.Active, wasActive)) return;"), true);
    }

    /// <summary>The body of a method, braces matched from its first "{"; empty when it is not found.</summary>
    private static string Body(string src, string signature)
    {
        int at = src.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0) return "";
        int open = src.IndexOf('{', at);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0) return src.Substring(open, i - open + 1);
        }
        return "";
    }
}
