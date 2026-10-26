using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The dungeon screen is an MDI frame, because that is what the original is: one parent window with
/// the dungeon, Party Information, Messages, Objects &amp; Spells and the Automap inside it, each
/// draggable, resizable and closable on its own.
///
/// The panels are written in DungeonWindow.axaml inside a hidden Shed, keeping their x:Names and
/// their Click handlers, and this lifts each one out and hands it to a child window. Nothing else in
/// the code-behind has to know: it still addresses PartyBox, MessageLog, SheetPanel and the rest by
/// name, and the two panels that the game shows and hides by setting IsVisible go on doing exactly
/// that - <see cref="Track"/> turns that flag into the child window appearing and disappearing.
///
/// Where each window sits and how big it is comes off the character, <c>M5958</c>: see
/// <see cref="WindowLayout"/> for the table it is seeded from and for Reset and Adjust to Display.
/// </summary>
public partial class DungeonWindow
{
    private MdiChild _characterChild = null!;
    private MdiChild _dungeonChild = null!;
    private MdiChild _partyChild = null!;
    private MdiChild _messagesChild = null!;
    private MdiChild _sheetChild = null!;
    private MdiChild _automapChild = null!;

    /// <summary>The seven tabs. See <see cref="CharacterPanel"/>.</summary>
    private CharacterPanel _charPanel = null!;

    /// <summary>The bookkeeping both screens share: see <see cref="MdiFrame"/>.</summary>
    private MdiFrame _frame = null!;

    /// <summary>
    /// The original keeps the window layout on the party leader alone: FRMAUTOM, FRMDUNM and the
    /// Control Panel all write slot 0 of the character array rather than the character with the
    /// focus, so switching with Alt+1..4 does not change the arrangement.
    /// </summary>
    private Character Leader => _party.Members[0];

    /// <summary>
    /// The caption on the dungeon child, FRMGROUP.BAS:244:
    /// <c>frmDungeonMain.Caption = "Dungeon (" + RTrim(name) + ")"</c>. The original re-titles it as
    /// the focus moves round the party, so a screenshot reading "Dungeon (Misty)" is naming that
    /// player's character, not carrying fixed text. <see cref="Redraw"/> calls this, so Alt+1..4 and
    /// 'n' re-caption it.
    /// </summary>
    /// <summary>
    /// Whether the last of the two routines that caption frmDungeonMain was <c>mdrmulSwitch</c>,
    /// which writes "Dungeon (Name)" for one character as for four (MULTIMOD.BAS:622-623), rather
    /// than <c>sub1536</c>, which writes it only for a party (686-690). A character loaded while
    /// below ground gets the first: sub14E0 shows frmParty before the dungeon (MAINMOD2.BAS:3163),
    /// and its Form_Load runs mdrmulSwitch (FRMGROUP.BAS:91). So does the party list's UPDATE,
    /// which ends in mdrmulSwitch (FRMGROUP.BAS:383-389), and a drag between names
    /// (FRMGROUP.BAS:247-249). Walking in from the city leaves the form's own "Dungeon".
    /// </summary>
    private bool _captionNamed;

    /// <summary>The city's sub14E0: the character was loaded below ground, see <see cref="_captionNamed"/>.</summary>
    public void LoadedBelow() => _captionNamed = true;

    /// <summary>
    /// <c>gv19C2(n).M5E4F</c>: the slot sub1536 last painted the character windows for. sub1536 alone
    /// writes it (MULTIMOD.BAS:668-683), and never mdrmulSwitch; the list's prune moves Me down
    /// past a removed slot (FRMGROUP.BAS:339-341) but not this. Kept so that activating the
    /// character window or Objects &amp; Spells can run "If gv19C2(..).M5E4F &lt;&gt; Me Then sub1536
    /// Me" (FRMCHAR1.BAS:33-37, FRMCHAR3.BAS:304-308) - which after a party is pruned to one puts
    /// the caption back to plain "Dungeon" (MULTIMOD.BAS:686-690).
    /// </summary>
    private int _shownSlot;

    /// <summary>
    /// <c>sub1536</c> itself (MULTIMOD.BAS:654-695): everything repainted for the slot, the slot
    /// remembered, and sub1536's caption the last word.
    /// </summary>
    private void Sub1536(int which)
    {
        // "frmCharStat3!cmbClass.Clear", "frmCharStat3!txtSpell.Text = """, then sub1658 on the
        // empty box (MULTIMOD.BAS:675-677): the first class, even for the character already shown
        SpellPage.Clear(SheetClassBox);
        SheetSpellText.Text = "";
        SwitchEverythingTo(which);
        SpellListChecked();   // "frmCharStat3!cmbClass.Clear ... sub1658" (MULTIMOD.BAS:675-677)
        _shownSlot = _party.SelectedIndex;
        _captionNamed = false;
        RetitleDungeon();
    }

    /// <summary>
    /// Form_Activate of frmCharStat1 and frmCharStat3 (FRMCHAR1.BAS:33-37, FRMCHAR3.BAS:304-308),
    /// raised here when the character window or Objects &amp; Spells becomes the active child: by a
    /// click, by a restore from an icon (mdrcharSetInfo's WindowState line, MAINMOD2.BAS:4598) or
    /// by an Info's picTabPicture.SetFocus. Only a change of active child counts.
    /// </summary>
    private void WireCharacterFormActivate()
    {
        Host.Activated += hit =>
        {
            if (hit != _characterChild && hit != _sheetChild) return;
            if (_shownSlot != _party.SelectedIndex) Sub1536(_party.SelectedIndex);
        };
    }

    private void RetitleDungeon()
    {
        if (_dungeonChild == null) return;
        // sub1536 names the character only when more than one is loaded, and a lone character's
        // window is plain "Dungeon" (MULTIMOD.BAS:686-690), as game_files/dmainw1.png shows it -
        // "Dungeon" being the form's own caption too (frmDungeonMain in MORDOR.EXE, RCDATA 22).
        // mdrmulSwitch names the character whatever the party's size (MULTIMOD.BAS:622-623), so
        // a lone character keeps "Dungeon (Name)" once that has run: see CaptionNamed.
        _dungeonChild.Title = _party.Size > 1 || _captionNamed ? $"Dungeon ({_ch.Name.TrimEnd()})" : "Dungeon";
        _characterChild.Title = $"\"{_ch.Name.TrimEnd()}\"";
        _charPanel.ShowPartyPosition(_party.SelectedIndex + 1, _party.Size);
        _charPanel.Show(_ch);
        Tools.Show(_ch, Lookup);
    }

    private void BuildMdi()
    {
        _options = GameOptions.Load(_gameDir);
        Tools.Invoked += OnToolCommand;
        // smoke tests only: the info page needs a click on the page button, which a test harness
        // cannot reach without hit-testing the desktop
        if (Environment.GetEnvironmentVariable("MORDOR_TEST_TOOLBAR") == "info") Tools.InfoPage = true;
        _frame = new MdiFrame(Host, Shed);
        _charPanel = new CharacterPanel(Lookup, _spells);
        _charPanel.BufferFired += slot => { FireBuffer(slot); Focus(); };
        // the character window is captioned with the character's own name in quotes - the help's
        // screenshot of the Control Panel lists it that way, as "Crashland"
        _characterChild = _frame.Adopt(GameWindow.Character, $"\"{_ch.Name.TrimEnd()}\"", _charPanel, CityWindow.CharacterDrawnAt);
        // FRMDUNM.FRM: a sizable border with no ControlBox, MinButton or MaxButton, which is how
        // dmainw1.png shows its caption - it can be dragged to a size and nothing else. Its
        // contents are the form's own design, scaled with it.
        FitDungeonDrawing();
        _dungeonChild = _frame.Adopt(GameWindow.Dungeon, $"Dungeon ({_ch.Name.TrimEnd()})", DungeonBox);
        _dungeonChild.Chrome(sizable: true, minButton: false, maxButton: false, closeBox: false);
        _dungeonChild.BodyMargin = new Thickness(0);
        var partyDrawn = CityWindow.FitPartyDrawing(PartyBox, PartyHeader);
        _partyChild = _frame.Adopt(GameWindow.Group, "Party Information", PartyBox, partyDrawn, grow: true);
        if (partyDrawn != CityWindow.PartyDrawnAt) _partyChild.AspectHeight = CityWindow.PartyAspect(partyDrawn);
        _partyChild.BodyMargin = new Thickness(0);   // the form's own offsets are in the drawing, as in town
        _messagesChild = _frame.Adopt(GameWindow.Messages, "Messages", MessagesBox);
        _messagesChild.Restored += _ => Avalonia.Threading.Dispatcher.UIThread.Post(ShowMessages, Avalonia.Threading.DispatcherPriority.Background);
        // frmCharStat3's client (MORDOR.EXE RCDATA 10), scaled down to a smaller window; a larger one gives its lists the room (ObjectsForm.Reflow)
        // rather than zoomed as Form_Resize zooms it (FRMCHAR3.BAS:408-444), as in town; the tab control's own 2-pixel inset is in the drawing
        IndexTab.Apply(SheetTabs, perRow: 3, strip: 22, DesignFonts.CharStat3, "tabItemTab");
        _sheetChild = _frame.Adopt(GameWindow.ObjectsAndSpells, "Objects & Spells", SheetPanel, ObjectsForm.Client);
        _sheetChild.BodyMargin = new Thickness(0);
        ObjectsForm.LinkScrollBar(PackList, PackScroll, ObjectsForm.PackRowPitch);
        ObjectsForm.LinkScrollBar(SheetSpellBox, SheetSpellScroll, ObjectsForm.SpellRowPitch, wholeRows: true);
        ObjectsForm.FitSpellRow(SheetSpellLabel, SheetSpellText);
        // the Companions page drawn to the form (CompanionsPage): a name clicked names it, a picture
        // double-clicked is Info and right-clicked Attack, a numbered button kicks its companion out
        _companionPage = new CompanionsPage(CompanionPage, SheetStates, () => _options.NoStretch,
            name: _ => { if (!Engaged) OnCompanionName(null, null!); }, info: _ => OnCompanionInfo(null, null!),
            kickOut: _ => OnCompanionKickOut(null, null!), attack: _ => OnCompanionAttack(null, null!));
        ObjectsForm.FitCompanionPage(CompanionPage);
        ObjectsForm.Reflow(SheetTabs, (Canvas)PackList.Parent!, PackList, PackScroll, PackModButtons, new[] { SheetEquipBtn, SheetUseBtn, SheetDropBtn, SheetInfoBtn },
                           (Canvas)SheetSpellBox.Parent!, SheetSpellBox, SheetSpellScroll, CompanionPage, _companionPage);
        // FRMAUTOM.BAS:157-161 refuses to let the window go below a quarter of its default, and
        // nothing more; see WindowLayout.Minimum
        _automapChild = _frame.Adopt(GameWindow.Automap, "Automap", null);
        _automapChild.IsVisible = false;   // OpenAutomap fills it and shows it

        // These two are shown and hidden by the game itself - Party Information "will pop up and
        // remain until the party is disbanded (or killed)" (help, Parties), and Objects & Spells
        // opens on i / Take / Spell. Their windows follow the panel rather than the other way
        // round, so none of that code had to change.
        MdiFrame.Track(PartyBox, _partyChild);
        MdiFrame.Track(SheetPanel, _sheetChild);
        // Closed with its close box, the window comes back as a fresh load: Form_Load runs sub1658
        // on an empty class box (FRMCHAR3.BAS:381-382), which lists the first class - and each
        // spell listed is a mdrcharCheckCast, so Me's Fight or Defend order goes (SpellListChecked)
        bool sheetClosed = false;
        _sheetChild.Closed += _ => sheetClosed = true;
        SheetPanel.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !SheetPanel.IsVisible || !sheetClosed) return;
            sheetClosed = false;
            SheetClassBox.DataContext = null;   // an empty box: sub1658 fills it and picks the first class
            RefreshSheetSpells();
            SpellListChecked();
        };
        ApplyGameOptions();
        _shownSlot = _party.SelectedIndex;
        WireCharacterFormActivate();
        WireAutomapMouse();
        WireDragAndDrop();
        // the mods' Objects & Spells commands, if any (docs/MODDING.md)
        ModSlots.AddObjectsCommands(PackModButtons, () => new Mordor.Modding.ObjectsCommandContext(_ch, inDungeon: true, inCombat: Engaged, Say, Lookup),
            () => { SaveChar(); RefreshPack(); RefreshBuffers(); Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch)); });
        ModSlots.OnModsChanged(this, RefreshParty);   // a mod party column switched on or off
        ModSlots.OnLiveModsChanged(this, RefreshPack);   // a mod's pack-line marks switched on or off
    }

    /// <summary>
    /// frmDungeonMain's drawing fitted to the Options fonts (a deviation, docs/FIDELITY.md). With
    /// the default fonts nothing changes.
    ///
    /// Down: picMonsters' two rows of lblMonster (195 twips tall, 210 apart: 14 pixels a row) and
    /// its two captions, lblCharacterHit (210) and lblMonsterHit (195), are each made as tall as
    /// the font's line (<see cref="UiFonts.LineHeight(DesignFonts.Form, DesignFont)"/>) where that
    /// is taller - the descenders and lblCurrentGroup's "[" had been cut off - and the box, and
    /// everything under it, that much lower.
    ///
    /// Across, by as much again in the form's proportions, so the window keeps its size and its
    /// shape - a taller window would run over whatever the player keeps under it - and the drawing
    /// is zoomed a little smaller into it: the room is given to the right-hand side, picMonsters,
    /// lblSquareInfo, picTrap and the button row (<see cref="FitButtonRow"/>), and the view and its
    /// arrows keep theirs.
    /// </summary>
    private void FitDungeonDrawing()
    {
        var form = DesignFonts.DungeonMain;
        // lblMonster(0..3) and lblCurrentGroup's mark have the form's own face, bold
        double line = UiFonts.LineHeight(form, UiFonts.Design(form, "lblMonster(0)"));
        const double RowPitch = 210.0 / 15, CharacterHitHeight = 210.0 / 15, MonsterHitHeight = 195.0 / 15;
        double pitch = Math.Max(RowPitch, line + 1);
        double rows = 2 * (pitch - RowPitch);
        // lblCharacterHit and lblMonsterHit: no font of their own, the form's bold one
        double hit = UiFonts.LabelHeight(form, CharacterHitHeight, DesignFont.Default);
        double monsterHit = UiFonts.LabelHeight(form, MonsterHitHeight, DesignFont.Default);
        double grow = rows + (hit - CharacterHitHeight) + (monsterHit - MonsterHitHeight);
        if (grow > 0)
        {
            double wider = Math.Ceiling(DungeonCanvas.Width * grow / DungeonCanvas.Height);
            // picMonsters: the rows, the captions, the portraits' centre and the chest at its far edge
            MonsterList.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() =>
                new WrapPanel { Orientation = Avalonia.Layout.Orientation.Vertical, ItemWidth = 166 + wider / 2, ItemHeight = pitch });
            MonsterList.Height += rows;
            MonsterList.Width += wider;
            Canvas.SetTop(CombatHit, Canvas.GetTop(CombatHit) + rows);
            CombatHit.Height = hit;
            CombatHit.Width += wider;
            Canvas.SetTop(CombatMonsterHit, Canvas.GetTop(CombatMonsterHit) + rows + (hit - CharacterHitHeight));
            CombatMonsterHit.Height = monsterHit;
            CombatMonsterHit.Width += wider;
            MonsterStrip.Width += wider;
            PortraitCentre += wider / 2;
            Canvas.SetLeft(ChestIcon, Canvas.GetLeft(ChestIcon) + wider);
            // what lies under picMonsters - lblSquareInfo, picTrap and the button row - that much lower
            double under = Canvas.GetTop(CombatPanel) + CombatPanel.Height;
            CombatCanvas.Height += grow;
            CombatPanel.Height += grow;
            CombatCanvas.Width += wider;
            CombatPanel.Width += wider;
            foreach (var c in DungeonCanvas.Children)
            {
                double top = Canvas.GetTop(c);
                if (!double.IsNaN(top) && top >= under) Canvas.SetTop(c, top + grow);
            }
            MsgText.Width += wider;
            Canvas.SetLeft(ChestPanel, Canvas.GetLeft(ChestPanel) + wider);
            Canvas.SetLeft(WaitBtn, Canvas.GetLeft(WaitBtn) + wider / 2);
            DungeonCanvas.Height += grow;
            DungeonCanvas.Width += wider;
        }
        FitButtonRow();
    }

    /// <summary>
    /// The button row, picPicture(1), in a larger Options font. The form's buttons are drawn where
    /// it puts them (cmdBuffer(0) 225 twips wide and nine beside it, the eight cmdAction at their
    /// own lefts, a 17-pixel gap before Map), and with the default fonts every caption fits and
    /// this does nothing. In a larger font a caption wider than its button ("Pickup", "10") had run
    /// over its edges: each command is made as wide as its caption and its frame need, in order and
    /// with the form's own gaps between them, as far to the right as the drawing's width
    /// (<see cref="FitDungeonDrawing"/>) lets them, the gap before Map taking up the difference; the
    /// buffers take what room is left, up to their captions' need.
    /// </summary>
    private void FitButtonRow()
    {
        if (FontRule.IsDefault(UiFonts.Options)) return;
        var pass = DesignFonts.DungeonMain.Pass;
        var font = UiFonts.Design(DesignFonts.DungeonMain, "cmdAction(0)");
        // the caption, the one-pixel frame at either side (Button.frame) and two pixels inside it
        double Need(object? caption) => UiFonts.TextWidth(caption?.ToString() ?? "", pass, font) + 2 + 4;
        var actions = new[] { MapBtn, PickupBtn, DropBodyBtn, FightBtn, SpellBtn, TakeBtn, OpenBtn, ExitBtn };
        // the form's own margins: the buffers 2 pixels in from the left, Exit 4 in from the right (the
        // drawing is 480 wide with the default fonts, FitDungeonDrawing's width otherwise)
        const double FormWidth = 480, LeastGap = 2;
        double left = Canvas.GetLeft(BufferButtons);
        double rightMargin = FormWidth - (Canvas.GetLeft(ExitBtn) + ExitBtn.Width);
        double[] widths = actions.Select(b => Math.Max(b.Width, Need(b.Content))).ToArray();
        double[] gaps = new double[actions.Length];
        for (int i = 0; i + 1 < actions.Length; i++)
            gaps[i] = Canvas.GetLeft(actions[i + 1]) - (Canvas.GetLeft(actions[i]) + actions[i].Width);
        double span = widths.Sum() + gaps.Sum();
        // the commands against the right, as the form has them; the buffers in what is left
        double start = DungeonCanvas.Width - rightMargin - span;
        double buffer = Math.Max(BufferButtons.Width / 10, Math.Min(Need("10"), (start - LeastGap - left) / 10));
        BufferButtons.Width = buffer * 10;
        double x = Math.Max(start, left + BufferButtons.Width + LeastGap);
        for (int i = 0; i < actions.Length; i++)
        {
            Canvas.SetLeft(actions[i], x);
            actions[i].Width = widths[i];
            x += widths[i] + gaps[i];
        }
    }

    private void ApplyWindowLayout() => _frame.Apply(Leader, DialogOwner.Screens);

    private void SaveWindowLayout() => _frame.Save(_party.Members);

    private (double W, double H) ScaledFrameSize() => _frame.ScaleArea(DialogOwner.Screens);

    /// <summary>
    /// The function keys and CTRL-letters that pick a page of the character window. The mapping is
    /// the original's, read out of <c>fn1521</c> (MODULE6.BAS:3526-3562) and the page-to-tab table
    /// in <c>sub1560</c>: F3 goes straight to tab 0 and F9 to tab 5, while F2, F4, F6 and F7 ask
    /// for pages 4, 3, 6 and 7, which that table turns into tabs 4, 1, 2 and 3.
    ///
    /// "you must use the CTRL key to access the tabs in Mordor... so the user can access a specific
    /// tab from any window in the game" (help, Commands).
    ///
    /// What each does to the window: see <see cref="SetInfoTab"/> and <see cref="SetTabOnly"/>.
    /// Left here are Ctrl+L, Ctrl+R, Ctrl+C and Ctrl+G, whose tabs (4, 1, 2, 3) are the ones
    /// tabStatTab_event0000 sends through mdrcharSetInfo (FRMCHAR1.BAS:322-335). fn15DF only sets
    /// the VideoSoft IndexTab's CurrTab (MAINMOD2.BAS:4191-4203), and that raises the control's
    /// Click - measured in the original (2026-10-25, docs/FIDELITY.md): Ctrl+L restored a minimised
    /// character window as the active, topmost child, Ctrl+G brought a maximised one back to its
    /// normal size, and Ctrl+C changed the tab of a normal one behind the city without raising it.
    /// So mdrcharSetInfo restores a minimised or maximised window (4598), and no SetFocus follows -
    /// the F-keys' <see cref="SetInfoTab"/>.
    /// </summary>
    private void ShowCharacterTab(CharTab tab) => SetInfoTab(tab);

    /// <summary>
    /// F2, F4, F6 (Shift-F6 too) and F7: fn1521 calls mdrcharSetInfo (MAINMOD2.BAS:3534, 3544,
    /// 3552-3555, 3559), whose first line brings the window back from a minimise or a maximise
    /// (4598) - and nothing raises a window already at its normal size, as no SetFocus follows.
    /// </summary>
    private void SetInfoTab(CharTab tab)
    {
        if (tab == CharTab.Look) RefreshAreaLook();
        _charPanel.Selected = tab;
        _characterChild.SetNormal();
        Focus();
    }

    /// <summary>
    /// F3 and F9 set tabStatTab and send lblCall "UPDATE" (MAINMOD2.BAS:3537-3538, 3566-3567), and
    /// Ctrl+S, Ctrl+B and Ctrl+M set the tab in fn15DF (4193-4201): the page changes, the window is
    /// neither restored nor raised. A window closed with its close box is loaded by the reference.
    /// </summary>
    private void SetTabOnly(CharTab tab)
    {
        _charPanel.Selected = tab;
        if (!_characterChild.IsVisible) _characterChild.Reopen();
        Focus();
    }

    /// <summary>
    /// F2, Area Look: "The Area List will display the characters that are in the same room as the
    /// current character. This is useful for finding out who is in a room if you've stumbled over
    /// somebody but don't know who." <c>mdrmazLookAround</c> - see <see cref="LookAround"/>. The
    /// party is where the window is; everyone else is where their save left them, in the area of
    /// that square.
    /// </summary>
    private void RefreshAreaLook() => _charPanel.ShowLook(WalkHere().Here);

    private (System.Collections.Generic.List<LookAround.Entry> Here, int Living, int Dead) WalkHere()
    {
        System.Collections.Generic.List<Character> saved;
        try { saved = Character.LoadAllFrom(SavesDir); }
        catch { saved = new(); }   // another character's save being written is not this window's problem
        // the walk goes down MorgueList, which lists the records in their order (MAZEMOD.BAS:203-224):
        // LoadAllFrom gives the saves in it (Roster.Saved)
        var roster = LookAround.Roster(_party.Members, saved);
        (int, int) PlaceOf(Character c)
        {
            // the party's own M59BB, live: SyncAreas has put the leader's area on each of them
            // (sub0956) and the walk refreshes the shown one's entry first (MAZEMOD.BAS:278-285).
            // A body in the rock keeps its nought, as the original's record does
            if (_party.Members.Contains(c)) return (_level, SavedArea(c));
            // the roster's M547A, the record's M59BB (MAZEMOD.BAS:284-287): an area, not a square
            return (c.DungeonLevel, SavedArea(c));
        }
        // the party's Hits and DeathState are MorgueList's, as sub0956 last copied them, but for
        // gv1A82's Hits, which the walk refreshes (MAZEMOD.BAS:279-285)
        var shown = _shownSlot < _party.Size ? _party.Members[_shownSlot] : null;
        return LookAround.Walk(_party.Members[0], roster, PlaceOf, listed: c => _listed.For(c, shown));
    }

    /// <summary>
    /// Drag and drop, which the help asks for in three places: "You can also swap items and move
    /// items to the Store, Bank and the Buffer Window by using Drag and Drop" (Items), "Your
    /// character has the ability to move (Drag and Drop) spells and items into the slots"
    /// (Buffers), and "This window also supports Drag and Drop to the Buffer Window" (Spells).
    ///
    /// The original starts a drag from a MouseMove with the button down (<c>lblItem_MouseMove</c>,
    /// FRMCHAR3.BAS:589-597) and decides what was dropped from the source control's Tag.
    /// </summary>
    private void WireDragAndDrop()
    {
        // the line pressed on, any slot, Hands and empty ones too (FRMCHAR3.BAS:589-597)
        Dragging.SourceLines(PackList, i => i < Character.PackSize ? DragPayload.Item(i) with { Owner = _ch } : null);
        Dragging.SourceLines(SheetSpellBox, i => i < _sheetSpells.Count ? DragPayload.Spell(SpellIndexOf(_sheetSpells[i].Spell)) with { Owner = _ch } : null);
        // the Companions page: a companion dragged onto another changes places with it
        // (picCompanion_DragDrop, FRMCHAR3.BAS:763-779), in a fight or out of one - it has no test
        CompanionDrag.Wire(_companionPage.Pictures, () => _ch.Companions, Say, () => { SaveChar(); RefreshCompanions(); });
        // txtSpell_DragDrop (FRMCHAR3.BAS:845-849): a "SPELL" label dropped on the box puts its
        // caption, the spell's name (sub2424, FRMCHAR3.BAS:692), in it; anything else is ignored
        Dragging.Target(SheetSpellText, DragKind.Spell, p =>
        {
            if (p.Index >= 0 && p.Index < _spells.Count) SheetSpellText.Text = _spells[p.Index].Name;
        });
        // lblName_MouseDown puts the clicked spell's name in txtSpell (FRMCHAR3.BAS:635). lblName holds
        // only the character's own spells (sub2424, 681-692), and so does this list. The "No Spells
        // Yet" row takes no click: sub2424 disables elaSpells, the labels' container, when the list
        // is empty (FRMCHAR3.BAS:723-724; property 0x0F of VSVBX.VBX's VSElastic list is Enabled)
        SheetSpellBox.AddHandler(PointerPressedEvent, (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            int i = SheetSpellBox.SelectedIndex;
            if (i >= 0 && i < _sheetSpells.Count) SheetSpellText.Text = _sheetSpells[i].Spell.Name;
        }), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        // cmbClass_Click (FRMCHAR3.BAS:55): another class, another list
        SheetClassBox.SelectionChanged += (_, _) => { if (!SpellPage.Filling) { RefreshSheetSpells(); SpellListChecked(); } };
        // vsbSpells_Change (FRMCHAR3.BAS:881-912) ends in sub2424, which checks each row it writes:
        // a scroll by a whole row drops the Fight or Defend order (SpellListChecked)
        // Only a scroll the player makes counts - the wheel over the list, a key in the list or on
        // the bar, the bar's arrows, track or thumb. The bar also moves when the list is refilled,
        // resized, clamped or snapped to whole rows; vsbSpells_Change has no such callers (a refill
        // the original has, cmbClass_Click or sub1658, checks the list itself).
        int spellTopRow = 0;
        bool scrollGesture = false, barHeld = false;
        void Gesture()
        {
            scrollGesture = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => scrollGesture = false,
                                                        Avalonia.Threading.DispatcherPriority.Background);
        }
        SheetSpellBox.AddHandler(PointerWheelChangedEvent, (_, _) => Gesture(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        SheetSpellBox.AddHandler(KeyDownEvent, (_, _) => Gesture(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        SheetSpellScroll.AddHandler(KeyDownEvent, (_, _) => Gesture(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        SheetSpellScroll.AddHandler(PointerWheelChangedEvent, (_, _) => Gesture(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        SheetSpellScroll.AddHandler(PointerPressedEvent, (_, _) => barHeld = true, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        SheetSpellScroll.AddHandler(PointerReleasedEvent, (_, _) => { barHeld = false; Gesture(); }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        SheetSpellScroll.AddHandler(PointerCaptureLostEvent, (_, _) => barHeld = false, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        SheetSpellScroll.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
            int row = (int)Math.Round(SheetSpellScroll.Value / ObjectsForm.SpellRowPitch);
            if (row == spellTopRow) return;
            spellTopRow = row;
            if (scrollGesture || barHeld) SpellListChecked();
        };

        // lblItem_DragDrop's second branch: an item dropped on another item changes places with it,
        // and the buffers that pointed at either follow. Both slots must be non-zero - slot 1 on
        // screen is the character's Hands and does not move.
        Dragging.Target(PackList, DragKind.Item, p =>
        {
            int onto = PackList.SelectedIndex;
            if (onto == p.Index) return;
            if (!_ch.SwapSlots(p.Index + 1, onto + 1)) return;   // SwapSlots takes the numbers you see
            SaveChar();
            RefreshPack();
            RefreshBufferButtons();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch));
            // a drag between slots is silent (FRMCHAR3 writes no line for it; the s key says "Item swap
            // completed!"); "Slot #a and Slot #b change places." was the port's
        });

        // anything let go over Equip, Use, Drop or Info presses it (cmdCommand_DragDrop, FRMCHAR3.BAS:252-254)
        PackCommands.Wire(PackList, () => _ch, SheetEquipBtn, SheetUseBtn, SheetDropBtn, SheetInfoBtn);

        _charPanel.BufferAssigned += (slot, p) =>
        {
            if (slot < 0 || slot >= SpellBuffers.Count) return;
            // lblBuffer_DragDrop (FRMCHAR1.BAS:219-233), an empty slot included
            if (p.BufferValue(_ch) is int value) _ch.Buffers[slot] = value;
            SaveChar();
            RefreshBufferButtons();
            // The Buffers page is rebuilt from scratch, and the button that just took the drop is
            // one of the ones thrown away - pulling it out of the tree while it is still handling
            // the event leaves the page half drawn. Let the event finish first.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch));
        };
    }

    /// <summary>Where a spell sits in the table, which is what a buffer stores plus forty.</summary>
    private int SpellIndexOf(Spell s) => _spells.FindIndex(x => x.Id == s.Id);


    /// <summary>
    /// <c>sub14E0</c> (MODULE6.BAS:3152-3160) is what brings a character into play, and among the
    /// windows it puts up is this one: <c>If Not gv1A66.M5F1B Then frmAutoMap.Show</c>. So the map
    /// is open from the start unless the player has ticked "No Load Automap" in the Options window,
    /// which is precisely what that option is for.
    ///
    /// The port only ever opened it on F8, so the option had nothing to turn off.
    /// </summary>
    private void OpenAutomapIfWanted()
    {
        // walking down from town is not sub14E0: the map is the town's, carried down as it was
        if (_carriedAutomap is var (map, open)) { ArriveWithAutomap(map, open); return; }
        if (_options.NoLoadAutomap) return;
        OnOpenAutomap(null, null!);
        Focus();
    }

    /// <summary>The town's frmAutoMap, handed down by <see cref="CarryAutomap"/>.</summary>
    private (AutomapWindow? Map, bool Open)? _carriedAutomap;

    /// <summary>
    /// The one frmAutoMap, from the town. Nothing between the two unloads it (only sub16E0 does,
    /// MAINMOD2.BAS:5970), so it comes down showing the level and the drawing it had.
    /// </summary>
    public void CarryAutomap(AutomapWindow? map, bool open) => _carriedAutomap = (map, open);

    /// <summary>The map as the dungeon leaves it, for the town to take back.</summary>
    public AutomapWindow? Automap => _automap;

    /// <summary>Whether that map is loaded - open on the frame, minimised or not.</summary>
    public bool AutomapOpen => _automap != null && _automapChild.IsVisible;

    /// <summary>
    /// frmDungeonMain's Form_Load with the map already loaded: it is put back to a normal window,
    /// and told "UPDATE" (FRMDUNM.BAS:297-302) - which, with Auto Change Lev., takes it to the level
    /// you are on (FRMAUTOM.BAS:306-313). It is not loaded again, so it keeps the level and
    /// drawing it had in town; a map that was shut in town stays shut.
    /// </summary>
    private void ArriveWithAutomap(AutomapWindow? map, bool open)
    {
        _automap = map;
        if (map == null || !open) return;
        map.Rebind(_map, () => _options.NoBlinkCursor);
        map.MoveInto(_automapChild);
        _automapChild.Reopen();
        _automapChild.Minimised = false;
        PushToAutomap();
        Focus();
    }

    /// <summary>
    /// The automap's two mouse commands. Both screens wire the same ones, so the rule lives on
    /// <see cref="AutomapWindow.WireMouse"/>.
    /// </summary>
    private void WireAutomapMouse() => AutomapWindow.WireMouse(_automapChild, () => _automap);

    /// <summary>
    /// The game-wide settings, MORDOR.INI's <c>[Loading Information]</c>. They belong to the game
    /// rather than to a character, so the frame holds them.
    /// </summary>
    private GameOptions _options = new();

    /// <summary>
    /// What the Options window's answer changes on this screen. FRMCONTR.BAS:69-73 does the same
    /// for the toolbar the moment the window closes.
    /// </summary>
    private void ApplyGameOptions()
    {
        Tools.IsVisible = !_options.NoToolbar;
        Audio.Apply(_options);
    }

    /// <summary>
    /// The toolbar buttons, each of which is the key it stands for. "This functions the same as
    /// pressing 'a'" and so on, all the way down the help's list.
    /// </summary>
    private void OnToolCommand(ToolCommand cmd)
    {
        switch (cmd)
        {
            case ToolCommand.Help: new HelpWindow().Show(); break;
            case ToolCommand.Experience: Press(Key.F5); break;
            case ToolCommand.Automap: Press(Key.F8); break;
            case ToolCommand.AskToJoin: Press(Key.A); break;
            case ToolCommand.GiveItem: Press(Key.G); break;
            case ToolCommand.PickupBody: Press(Key.P); break;
            case ToolCommand.DropBody: Press(Key.L); break;
            case ToolCommand.ControlPanel: Press(Key.F10); break;
        }
        Focus();
    }

    /// <summary>Opens the Control Panel over the frame.</summary>
    private async void OnControlPanel(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveWindowLayout();
        var (fw, fh) = ScaledFrameSize();
        var panel = new ControlPanelWindow(_ch, Leader, _frame.Listed(), fw, fh, _options, _gameDir);
        panel.OptionsChanged += ApplyGameOptions;
        await OriginalBox.ShowOver(panel, DialogOwner);
        ApplyWindowLayout();
        Focus();
        // Form_Unload, however the panel is shut: "If Chars(m008C).DeathState Then sub0930 m008C", m008C = 0
        // (FRMCONTR.BAS:245-247; m008C is the form's Const 0) - for the first member dead, sub0930 again: its head, and alone the
        // dead screen once more; in company the party list's UPDATE (MAINMOD2.BAS:3016-3125)
        if (!_ended && Leader.IsDead) Sub0930(Leader);
    }
}
