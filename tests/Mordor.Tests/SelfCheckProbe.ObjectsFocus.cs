using System.Text.RegularExpressions;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Where Objects &amp; Spells leaves the keyboard. Its Form_KeyDown keeps every key but Escape in a
    /// ComboBox, a TextBox or a VScrollBar and hands the rest to fn1521 (FRMCHAR3.BAS:310-331), and
    /// fn1521 is where Alt+1..4 are (fn15DF, MAINMOD2.BAS:3385, 4222-4230). So what matters is where
    /// the focus goes:
    /// <list type="bullet">
    /// <item>A tab click sets nothing (tabItemTab_event000D, FRMCHAR3.BAS:830-843), and only
    /// lblCall "SPELL" - the Spell button and the c key - puts it in txtSpell (454-460). Avalonia
    /// gives a clicked TabItem the focus, which the previews pass over, so nothing in the port may
    /// move it into the Spell Type box or the spell box but that one path.</item>
    /// <item>cmdCast_Click ends in sub0775, gv1BFC.SetFocus (183, 187); cmdInfo_Click, the items'
    /// Info and a companion's Info put it on frmCharStat1!picTabPicture (299-301, 209, 759); Kick
    /// Out ends in sub0775 once asked (271-280). The port had left it in txtSpell after a cast or
    /// an Info, where Alt+1..4 did nothing until Escape.</item>
    /// </list>
    /// </summary>
    private static void ObjectsFocusChecks()
    {
        Console.WriteLine("\nObjects & Spells hands the keyboard back as FRMCHAR3 does");
        string dungeon = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
        string cityMdi = PortSource("src/Mordor.App", "CityWindow.Mdi.cs");

        // the body of a method, braces matched from its first "{"
        string Body(string src, string signature)
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
        // a Focus() of the frame's after the last Activate() of the character window (picTabPicture.SetFocus)
        bool FocusAfterActivate(string body) =>
            body.Contains("_characterChild.Activate();") &&
            body.IndexOf("Focus();", body.LastIndexOf("_characterChild.Activate();", StringComparison.Ordinal), StringComparison.Ordinal) > 0;

        string castTyped = Body(dungeon, "private void CastTyped()");
        string after = Body(dungeon, "Task CastThenSub0775(Spell spell)");
        Check("dungeon: cmdCast_Click's cast is followed by sub0775 (FRMCHAR3.BAS:183, 187)",
              castTyped.Contains("_ = CastThenSub0775(_spells[i]);") && after.Contains("await CastKnownSpell(spell);")
              && Regex.IsMatch(after, @"finally\s*\{\s*Focus\(\);\s*\}"), true);
        Check("dungeon: an unknown name is sub0775 (FRMCHAR3.BAS:169-171)", castTyped.Contains("if (i == -1) { Focus(); return; }"), true);
        Check("dungeon: spell Info puts the focus on the character window (FRMCHAR3.BAS:299-301)",
              FocusAfterActivate(Body(dungeon, "private void OnSheetSpellInfo(")), true);
        Check("dungeon: item Info puts the focus on the character window (FRMCHAR3.BAS:209)",
              FocusAfterActivate(Body(dungeon, "private void OnSheetInfo(")), true);
        Check("dungeon: companion Info puts the focus on the character window (FRMCHAR3.BAS:759)",
              FocusAfterActivate(Body(dungeon, "private void OnCompanionInfo(")), true);
        string kick = Body(dungeon, "private async void OnCompanionKickOut(");
        Check("dungeon: Kick Out ends in sub0775 once asked, yes or no (FRMCHAR3.BAS:271-280)",
              Regex.IsMatch(kick, @"if \(await Confirm\([^\n]*\)\) LetCompanionGo\(i, forced: false\);\s*Focus\(\);\s*\}$"), true);

        Check("city: spell Info puts the focus on the character window (FRMCHAR3.BAS:299-301)",
              FocusAfterActivate(Body(cityMdi, "private void OnSpellInfo(")), true);
        Check("city: an unknown name is sub0775 (FRMCHAR3.BAS:294)", Body(cityMdi, "private void OnSpellInfo(").Contains("if (i == -1) { Focus(); return; }"), true);
        string packInfo = Body(cityMdi, "private void OnPackInfo(");
        Check("city: item Info puts the focus on the character window (FRMCHAR3.BAS:209)",
              packInfo.IndexOf("Focus();", packInfo.IndexOf("ShowItemInMisc(", StringComparison.Ordinal) + 1, StringComparison.Ordinal) > 0, true);
        Check("city: companion Info puts the focus on the character window (FRMCHAR3.BAS:759)",
              FocusAfterActivate(Body(cityMdi, "private void OnCompanionInfo(")), true);
        // "Cannot cast spells while in the city!" is an Exit Sub before any sub0775 (FRMCHAR3.BAS:156-159)
        Check("city: the town's cast refusal leaves the focus where it is",
              Body(cityMdi, "private void OnSpellCast(").Contains("Focus"), false);

        // only lblCall "SPELL" puts the focus in txtSpell, and nothing puts it in cmbClass
        var app = PortTree.At("src/Mordor.App");
        var into = new List<string>();
        foreach (var f in System.IO.Directory.GetFiles(app, "*.cs"))
        {
            string s = System.IO.File.ReadAllText(f);
            foreach (Match m in Regex.Matches(s, @"\b(SheetSpellText|SpellText|SheetClassBox|ClassBox)\.Focus\("))
                into.Add($"{System.IO.Path.GetFileName(f)}:{m.Groups[1].Value}");
        }
        Check("the spell box and the Spell Type box take the focus only from lblCall \"SPELL\" (FRMCHAR3.BAS:454-460)",
              string.Join(" ", into), "DungeonWindow.axaml.cs:SheetSpellText");
        Check("  ... and that one is the Spell button's (OnShowSheetSpells)",
              Body(dungeon, "private void OnShowSheetSpells(").Contains("SheetSpellText.Focus();"), true);
        // a tab click sets nothing (tabItemTab_event000D, FRMCHAR3.BAS:830-843): no handler of the
        // port's on the tab control's selection
        Check("a tab of Objects & Spells being picked moves no focus (FRMCHAR3.BAS:830-843)",
              Regex.IsMatch(dungeon + PortSource("src/Mordor.App", "CityWindow.axaml.cs") + cityMdi
                            + PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs") + PortSource("src/Mordor.App", "IndexTab.cs"),
                            @"SheetTabs\.(SelectionChanged|GotFocus|AddHandler)|TabItem[^\n]*Focusable\s*=\s*false"), false);
        // what the previews let the controls keep: a clicked TabItem is none of them
        string owner = PortSource("src/Mordor.App", "KeyOwner.cs");
        Check("KeyOwner leaves a tab's keys to the game", Regex.IsMatch(owner, @"\bTab(Item|Control)\b"), false);

        Section(() => SpellTypeFocusChecks(dungeon, cityMdi, Body));
    }

    /// <summary>
    /// The Spell Type box after a pick. Reported 2026-10-12: on the Spells page, after choosing a spell
    /// type, Alt+1..4 do not switch characters. That is the original's: cmbClass_Click lists the class
    /// and moves no focus - no sub0775, no SetFocus (FRMCHAR3.BAS:55-149) - so the box, which the click
    /// gave the keyboard, keeps it, and Form_KeyDown leaves a ComboBox every key but Escape, which is
    /// gv1BFC.SetFocus (313-314). Alt+digit reaches fn1521 and fn15DF only in its last two branches
    /// (319-330). The service forms do hand Alt+digit to fn15DF from their boxes (FRMCTYS.BAS:407-425
    /// and the rest, the city's ServiceFieldKey); Objects &amp; Spells does not. So the port keeps the
    /// box's keys too, and Escape is the way back, as it was.
    /// </summary>
    private static void SpellTypeFocusChecks(string dungeon, string cityMdi, Func<string, string, string> Body)
    {
        string frm3 = PortSource("decompiled-named", "FRMCHAR3.BAS").Replace("\r\n", "\n");
        int from = frm3.IndexOf("Sub cmbClass_Click ()", StringComparison.Ordinal);
        int to = from < 0 ? -1 : frm3.IndexOf("\nEnd Sub", from, StringComparison.Ordinal);
        string cmbClick = from < 0 || to < 0 ? "" : frm3.Substring(from, to - from);
        Check("FRMCHAR3: cmbClass_Click moves no focus (55-149)",
              cmbClick.Contains("vsbSpells_Change") && !cmbClick.Contains("SetFocus") && !cmbClick.Contains("sub0775"), true);
        Check("FRMCHAR3: a ComboBox keeps every key but Escape, which is gv1BFC.SetFocus (313-314)",
              Regex.IsMatch(frm3, @"If  TypeOf Me\.ActiveControl Is ComboBox Then\n\s*If  KeyCode = mc0166 Then gv1BFC\.SetFocus\n\s*ElseIf"), true);

        string cityKeys = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
        string dungeonMdi = PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs");
        Check("city: a Spell Type pick lists the class and nothing more (cmbClass_Click)",
              cityMdi.Contains("ClassBox.SelectionChanged += (_, _) => { if (!SpellPage.Filling) RefreshSpells(); };")
              && !Body(cityMdi, "private void RefreshSpells()").Contains("Focus"), true);
        Check("dungeon: a Spell Type pick lists the class and nothing more (cmbClass_Click)",
              // and SpellListChecked, the Att = False of each mdrcharCheckCast it makes (MAINMOD2.BAS:433)
              dungeonMdi.Contains("SheetClassBox.SelectionChanged += (_, _) => { if (!SpellPage.Filling) { RefreshSheetSpells(); SpellListChecked(); } };")
              && !Body(dungeon, "private void RefreshSheetSpells()").Contains("Focus")
              && !Body(dungeon, "private void SpellListChecked()").Contains("Focus"), true);
        // vsbSpells_Change is a scroll the player makes; the port's bar also moves on a refill,
        // a resize, a clamp or the whole-row snap, and those must not drop the order
        Check("dungeon: only a scroll the player makes checks the list (vsbSpells_Change, FRMCHAR3.BAS:881-912)",
              dungeonMdi.Contains("if (scrollGesture || barHeld) SpellListChecked();")
              && dungeonMdi.Contains("SheetSpellBox.AddHandler(PointerWheelChangedEvent, (_, _) => Gesture()"), true);
        // Form_Load runs sub1658 (FRMCHAR3.BAS:381-382): the window reopened after its close box
        Check("dungeon: Objects & Spells reopened lists afresh and checks the list (FRMCHAR3.BAS:382)",
              dungeonMdi.Contains("_sheetChild.Closed += _ => sheetClosed = true;")
              && Regex.IsMatch(dungeonMdi, @"SheetClassBox\.DataContext = null;[^\n]*\n\s*RefreshSheetSpells\(\);\s*SpellListChecked\(\);"), true);
        // l01D0 = Int(Rnd * 100) (MAINMOD2.BAS:840), after mdrcharCheckCast and before anything the
        // cast does, on the casts outside a fight too (FRMCHAR3.BAS:186, MAINMOD2.BAS:4159, 6140)
        string castHere = Body(dungeon, "CastSpellHere(Spell spell, int itemLevel,");
        int checkAt = castHere.IndexOf("CheckCast(spell", StringComparison.Ordinal);
        int drawAt = castHere.IndexOf("_ = _rng.NextDouble();", StringComparison.Ordinal);
        int castAt = castHere.IndexOf("SpellThrown.Handles(spell) ? await", StringComparison.Ordinal);
        Check("dungeon: CastSpellHere draws l01D0 after the check and before the cast",
              checkAt >= 0 && drawAt > checkAt && castAt > drawAt, true);
        Check("city: in the Spell Type box Escape hands the keyboard back and the rest is the box's (FRMCHAR3.BAS:313-314)",
              Regex.IsMatch(cityKeys, @"KeyOwner\.InComboBox\(fv\)\)\s*\{\s*if \(e\.Key == Avalonia\.Input\.Key\.Escape\) \{ e\.Handled = true; Focus\(\); return; \}\s*base\.OnKeyDown\(e\); return;"), true);
        Check("dungeon: in the Spell Type box Escape hands the keyboard back and the rest is the box's (FRMCHAR3.BAS:313-314)",
              Regex.IsMatch(dungeon, @"KeyOwner\.KeepsKeys\(focused\)[^\n]*\n[^\n]*PopupRoot\)\)\s*\{\s*if \(e\.Key == Key\.Escape\) \{ e\.Handled = true; Focus\(\); \}\s*return;"), true);
        Check("city: Alt+digit from a box is fn15DF's only on a service form, not on Objects & Spells",
              Body(cityKeys, "private bool ServiceFieldKey(").Contains("if (ServiceHolding(f) is not Svc holder || holder.Which is not (GameWindow.GeneralStore"), true);
    }
}
