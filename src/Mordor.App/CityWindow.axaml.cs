using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

public partial class CityWindow : Window
{
    private readonly Party _party;

    /// <summary>The character the city's screens are showing, the party's selected member.</summary>
    private Character _ch => _party.Selected;

    private readonly string _gameDir;
    private readonly List<Item> _items;
    private readonly Dictionary<short, Item> _itemsById;
    private readonly List<Monster> _monsters;
    private readonly List<Spell> _spells;
    private readonly Random _rng = new();

    public CityWindow(Character ch, string gameDir)
        : this(new Party(ch), gameDir, autoload: true) { }

    /// <summary>
    /// M59BB as each record keeps it (<see cref="Character.Area"/>), what mdrmulLoadParty compares
    /// (MULTIMOD.BAS:248); a save from before the port kept it is given its square's area, -1 in
    /// town. The maze is read only if such a save turns up.
    /// </summary>
    private static Func<Character, int> AreasIn(string gameDir)
    {
        List<Floor>? maze = null;
        return c => c.AreaOr(k =>
        {
            if (k.DungeonLevel <= 0) return -1;
            try { maze ??= MdrLoader.LoadDungeon(System.IO.Path.Combine(gameDir, "MDATA11.MDR")); }
            catch { return -1; }
            return k.DungeonLevel > maze.Count ? -1 : maze[k.DungeonLevel - 1].AreaIdAt(k.X, k.Y);
        });
    }

    /// <summary>The monster table for the autoload, read only if somebody joining brings a companion to look up.</summary>
    private static Func<Companion, Monster?> MonstersIn(string gameDir)
    {
        List<Monster>? table = null;
        return c => c.Def(table ??= MdrLoader.LoadMonsters(System.IO.Path.Combine(gameDir, "MDATA5.MDR")));
    }

    /// <summary>
    /// What the autoload had to say, collected while the party was being put back together and
    /// written into the Messages window once there is one to write into. The original says these
    /// with <c>sub085B</c> as it goes, which amounts to the same thing - the window is already up
    /// there.
    /// </summary>
    private static readonly List<string> AutoloadSaid = new();

    /// <summary>
    /// Help, "Parties": "the program will automatically remember and reload the party (if
    /// requested) when the party leader is loaded again", so the city opens with whoever the
    /// leader last travelled with.
    /// </summary>
    public CityWindow(Party party, string gameDir) : this(party, gameDir, autoload: false) { }

    private CityWindow(Party party, string gameDir, bool autoload)
    {
        _party = party;
        _gameDir = gameDir;
        // the Options window's fonts, read before any control is built (UiFonts)
        UiFonts.Use(GameOptions.Load(gameDir));
        InitializeComponent();
        // the frame's caption, " (Limited Version)" after it in the shareware build (FRMMAIN.BAS:148-152)
        Title = LimitedVersion.FrameCaption;
        // sub16F8 runs from the idle loop; a quarter second is as near as a timer need come
        var playClock = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        playClock.Tick += (_, _) => PlayClock.Shared.Tick(_party.Members, PlayClock.Timer(DateTime.Now));
        playClock.Start();
        Closed += (_, _) => playClock.Stop();
        FramePauseWatch.Watch(this);   // minimised, the game pauses (FRMMAIN.BAS:202-233)
        // Nothing the autoload says reaches the Messages window: sub1738 runs sub14E0 and
        // mdrmulLoadParty (MAINMOD2.BAS:6305-6306) while gv1B52 is still False, which frmMenu's
        // Form_Load set (FRMMENU.BAS:144), and sub085B returns at once while it is
        // (MAINMOD2.BAS:3198-3199). gv1B52 turns True only after both (MAINMOD2.BAS:6308).
        AutoloadSaid.Clear();

        _items = MdrLoader.LoadItems(System.IO.Path.Combine(gameDir, "MDATA3.MDR"));
        _itemsById = _items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        _monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(gameDir, "MDATA5.MDR"));
        _spells = MdrLoader.LoadSpells(System.IO.Path.Combine(gameDir, "MDATA2.MDR"));

        // saves written before InventorySlot.SlotCursed existed carry no flag, so worn gear would
        // come off freely until it was next put on again
        foreach (var m in _party.Members) m.SyncSlotCurses(Lookup);
        // and companions the port once charmed or took on without an alignment get the creature's own
        foreach (var m in _party.Members) m.Companions.Settle(_monsters);

        // the mods' view of the game in play (ctx.Game, docs/MODDING.md): this party and these
        // tables, until the city closes; with no mod loaded it is kept and read by nobody
        Mordor.Modding.ModHost.Active.SetGame(_party, _spells, Lookup);
        Closed += (_, _) => { if (Mordor.Modding.ModHost.Active.GameParty == _party) Mordor.Modding.ModHost.Active.SetGame(null); };

        BuildMdi();
        // the frame back at the size and place it was left, before it is shown (a port necessity)
        RestoreFramePlacement();
        // every game key reaches the game before the control with the focus (see PreviewGameKey)
        AddHandler(KeyDownEvent, PreviewGameKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Tools.Invoked += OnToolCommand;
        _options = GameOptions.Load(gameDir);
        Tools.IsVisible = !_options.NoToolbar;
        Audio.Apply(_options);
        Opened += (_, _) =>
        {
            // the children's places are only right in a frame of its final size: a frame put back
            // maximised has that size by now, but its contents are laid out at the next pass
            UpdateLayout();
            ApplyWindowLayout();
            OpenAutomapIfWanted();
            // one above the other in the order sub14E0 brings them up: the three loaded, the party
            // shown, the automap, then frmCityMain (MAINMOD2.BAS:3160-3172)
            foreach (var c in new[] { _characterChild, _sheetChild, _messagesChild, _partyChild, _automapChild, _cityChild })
                Host.BringToFront(c);
            // a service already up - the dead screen of a character run dead - goes over all of them
            foreach (var s in OpenServices.ToList()) InServiceWindow(s, PlaceService);
        };
        Closed += (_, _) =>
        {
            // a session ended while the party is below: the dungeon is left first, which saves
            // the trip and the layout its children are in; the town's own are not on the frame
            if (_below != null) { _below.Close(); Save(); return; }
            SaveWindowLayout(); Save();
        };

        Audio.Init(gameDir);
        Audio.Music("CITY");
        // no HELLO: the port played one on arrival in town, but FRMCTYM.BAS plays no sound at all,
        // and the original's only HELLO is the dungeon's, for strangers on the square (MAZEMOD.BAS:371-372)
        Closed += (_, _) => Audio.Music("THEME");
        // sub14E0 (MAINMOD2.BAS:3166-3178): "If Chars(Me).DungeonLevel = 0 And Chars(Me).DeathState =
        // False Then frmCityMain.Show", else frmDungeonMain is shown and frmCityMain is never loaded,
        // so its Form_Load - the rest, the ageing, the store turned over (FRMCTYM.BAS:105-134) - does
        // not run. That comes only when the party later walks out ("frmCityMain.Show",
        // MAINMOD2.BAS:3957-3958), which is EnterDungeon's RestOnArrival. The port had opened
        // everyone in the city and rested them there first.
        bool below = _ch.DungeonLevel > 0 && !_ch.IsDead;
        if (!below && !_ch.IsDead) RestOnArrival();
        // then mdrmulLoadParty (MAINMOD2.BAS:6306), after sub14E0 has shown the city or the dungeon
        // (3171): so frmCityMain's Form_Load rests the one character run and nobody the autoload
        // brings, and its tests compare them with that character's M59BB as Form_Load left it,
        // -1 in town (FRMCTYM.BAS:112). The port had loaded the party first and rested them all.
        if (autoload)
        {
            var loaded = Party.LoadFor(_ch, SavesDir, AutoloadSaid.Add, MonstersIn(gameDir), AreasIn(gameDir));
            AutoloadSaid.Clear();   // said while gv1B52 is False, so never shown (see above)
            foreach (var m in loaded.Members.Skip(1)) { m.SyncSlotCurses(Lookup); m.Companions.Settle(_monsters); _party.Members.Add(m); }
            // each one's sub154A ends in frmParty's "UPDATE" (MULTIMOD.BAS:266, 382), unheard as the rest
            PruneTheDead(quiet: true);
        }
        RefreshSummary();
        if (below) Opened += (_, _) => { _enteringFromLoad = true; EnterDungeon(null, null!); };
        // DEVIATION, kept: sub14E0 shows frmDungeonMain for the dead even at level 0, with lblDead and
        // cmdWait (MAINMOD2.BAS:3170-3182, sub0930 3016-3131). The port's dungeon window cannot stand
        // at level 0, so the same label, button and boxes are a window of their own, captioned
        // "Dungeon (Name)" and placed as the Dungeon is (ShowDead), and the town's window is hidden
        // while it is up, as sub14E0 shows the one or the other (MAINMOD2.BAS:3170-3180; corrected
        // 2026-10-19). What differs is only that it is not the dungeon window itself, so the
        // dungeon's toolbar and buttons are not about it. No rest is given (see above) until a raise.
        if (_ch.IsDead) ArriveDeadSub0930();
        // FRMCTYM's own lines (see RestOnArrival) are said while gv1B52 is still False on the way in,
        // so sub085B drops them (MAINMOD2.BAS:3198-3199, 6305-6308); the first line of the game is
        // gc018A, "Welcome to MORDOR 1.1!" (MAIN.TXT:34), said once gv1B52 is True (MAINMOD2.BAS:6309)
        Log("Welcome to MORDOR 1.1!");
        // automated smoke tests only. This waits for Opened because some of these screens put up a
        // dialog, and a dialog cannot be shown over a window that has not been displayed yet.
        Opened += (_, _) =>
        {
            // MORDOR_TEST_INFO=1: the first carried item's Info, as the pack's Info button would give it
            if (Environment.GetEnvironmentVariable("MORDOR_TEST_INFO") == "1"
                && _ch.Inventory.FirstOrDefault(s => s.ItemId > 0) is InventorySlot first && Lookup(first.ItemId) is Item firstItem)
                ShowItemInMisc(firstItem, first.Alignment, first.Known);
            // MORDOR_TEST_INFO=spell: a spell's page, Firebolt, as the Spells window's Info would give it
            if (Environment.GetEnvironmentVariable("MORDOR_TEST_INFO") == "spell"
                && _spells.FirstOrDefault(sp => sp.Name.Trim() == "Firebolt") is Spell fb)
            {
                _charPanel.ShowMiscPage(SpellInfo.Paint(fb, _ch));
                _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
                _characterChild.Activate();
            }
            switch (Environment.GetEnvironmentVariable("MORDOR_TEST_PAGE"))
            {
                case "library": ShowLibrary(null, null!); break;
                case "confinement": ShowConfinement(null, null!); break;
                case "bank": ShowBank(null, null!); break;
                // frmHall is the main menu's (FRMMENU.BAS:40); this opens it over the town to see it
                case "hall": _ = new HallWindow(_gameDir).ShowDialog(this); break;
                case "seer": ShowSeer(null, null!); break;
                case "store": ShowStore(null, null!); break;
                case "morgue": ShowMorgue(null, null!); Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_automapChild != null) _automapChild.IsVisible = false; }, Avalonia.Threading.DispatcherPriority.Background); break;
                // frmExchange is the main menu's (FRMMENU.BAS:83); this opens it over the first two
                // members, without the menu's load and refusals, to see the screen
                case "exchange":
                    if (_party.Size >= 2)
                        _ = new ExchangeWindow(_party.Members[0], _party.Members[1], Lookup, _ => Save()).ShowDialog(this);
                    break;
                case "guildlog": ShowGuildLog(_ch.Guild.Guild); break;
                case "guilds": case "levelup": ShowGuild(null, null!); break;
            }
        };
    }

    private Item? Lookup(short id) => _itemsById.TryGetValue(id, out var it) ? it : null;

    /// <summary>
    /// The party keys from the help's Commands page: 'a' to ask someone to join, ALT-l to ask the
    /// selected character to leave, ALT-1..4 to switch, ALT-p to pool the party's on-hand gold.
    /// </summary>
    /// <summary>The dungeon, while it is in this frame in place of the town (see EnterDungeon).</summary>
    private DungeonWindow? _below;

    /// <summary>A key <see cref="PreviewGameKey"/> has already given the game, on its way back up.</summary>
    private Avalonia.Input.KeyEventArgs? _previewed;

    /// <summary>
    /// KeyPreview. Every game window's Form_KeyDown takes a key before its controls do and hands it
    /// to fn1521, which in town clears it when it is a game key (the character window,
    /// FRMCHAR1.BAS:39-53; Messages, FRMCHAR4.BAS:17-32; Party, FRMGROUP.BAS:51-66; the automap,
    /// FRMAUTOM.BAS:55-72). The City's own takes its Alt letters and hands every other key to
    /// fn1521 without clearing KeyCode (FRMCTYM.BAS:35-64, 57-61). Only a text box or a combo box keeps its
    /// keys, and in Objects &amp; Spells and the automap a scroll bar too (FRMCHAR3.BAS:310-331,
    /// FRMAUTOM.BAS:57); what fn1521 leaves goes on to the control. Taken on the way down, so that
    /// a TabControl or a list with the focus - the character window's tabs, once clicked - does
    /// not take a game key first. The service windows (FRMCTYS, FRMCITYB, ...) hand the key to
    /// fn1521 without clearing it, so both see it, and are left to their own handling; below
    /// ground the dungeon previews its own (DungeonWindow's Root handler).
    /// </summary>
    private void PreviewGameKey(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Handled || _below != null) return;
        if (OriginalBox.IsCloseChord(e) || e.Key == Avalonia.Input.Key.Tab) return;
        if (ServiceAccessKey(e)) { e.Handled = true; return; }
        // a service form's Default and Cancel buttons (the Guild's), while it is the active window
        // and no box is being typed in: it is active from its opening until another window is
        // clicked
        if (ActiveService is Svc keyed && keyed.Keys != null && e.KeyModifiers == Avalonia.Input.KeyModifiers.None
            && FocusManager?.GetFocusedElement() is not TextBox)
        {
            _svc = keyed;
            if (keyed.Keys(e.Key)) { e.Handled = true; return; }
        }
        if (FocusManager?.GetFocusedElement() is not Avalonia.Visual f || ReferenceEquals(f, this)) return;
        if (ServiceFieldKey(f, e)) { e.Handled = true; return; }
        if (KeyOwner.KeepsKeys(f)) return;   // an open combo box's list too
        if (ServiceHolding(f) != null) return;
        OnKeyDown(e);
        _previewed = e;
    }

    /// <summary>
    /// A service form's own access keys, the "&amp;" letters of its buttons' captions in MORDOR.EXE
    /// (<see cref="FormPage.Access"/>): Alt and the letter press the button while that form is the
    /// active window, whatever control has the focus, and only a button that is visible and enabled
    /// - a greyed one, or the Guild Log's Clean before Form_Resize shows it, does nothing. Windows does that for a VB form before its Form_KeyDown sees the key, so a
    /// letter none of the form's buttons answers goes on as before (fn1521's ALT-s, ALT-l, ALT-p, and
    /// fn15DF's Alt with a digit). The town's own Alt pictures (FRMCTYM.BAS:35-58) are frmCityMain's
    /// and wait for the town to be the active window again (<see cref="CityIsActive"/>). Two
    /// captions the form data gives an access key lose it at run time: cmdJoin, which sub2962 always
    /// recaptions "Join" or "Re-Acquaint" before it can be pressed (FRMCTYG.BAS:647, 787, 807), and
    /// the Morgue's companion raise, recaptioned "Raise ... for ..." when it is shown (FRMMORG.BAS:264).
    /// </summary>
    private bool ServiceAccessKey(Avalonia.Input.KeyEventArgs e)
    {
        if (e.KeyModifiers != Avalonia.Input.KeyModifiers.Alt) return false;
        if (e.Key < Avalonia.Input.Key.A || e.Key > Avalonia.Input.Key.Z) return false;
        if (ActiveService is not Svc active) return false;
        _svc = active;
        char letter = (char)('A' + (e.Key - Avalonia.Input.Key.A));
        var answer = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(active.Panel).OfType<Button>()
            .Where(b => FormPage.AccessOf(b) == letter && b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        if (answer.Count == 0) return false;
        // Two live buttons sharing a letter (the Store's &Combine and Un&curse, the Confinement's &Bind
        // and &BUY): Windows moves the focus between controls whose mnemonic is not unique rather than
        // pressing one, and the port's buttons take no focus, so the key is spent and nothing is pressed.
        // Not yet seen in the original (docs/FIDELITY.md).
        if (answer.Count == 1) answer[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        return true;
    }

    /// <summary>
    /// fn15DF (MAINMOD2.BAS:4175-4241) for a text box or combo box in the General Store, the
    /// Companion store, the Bank, the Morgue or a Guild. Each of those forms' Form_KeyDown hands a
    /// key to fn15DF while its active control is a ComboBox or a TextBox, and clears it when fn15DF
    /// used it (FRMCTYS.BAS:407-425, FRMCITYC.BAS:380-398, FRMCITYB.BAS:86-103, FRMCTYG.BAS:598-615,
    /// FRMMORG.BAS:198-215); it tests Me.ActiveControl, which only means anything with the form's
    /// KeyPreview on, so the form sees the key before the box. fn15DF's keys are Ctrl with L, B, M,
    /// S, R, C or G for a character-window tab and I, P or O for an Objects &amp; Spells page, and
    /// Alt with a digit for the Party window's lblCall - in a party, for a slot that exists. Its
    /// other two, Alt+Tab and Ctrl+Escape, are Windows' own. The Seer is not among them: its
    /// Form_KeyDown hands every key to fn1521 (FRMSEEK.BAS:374-379). The port's city had given
    /// every key in such a box to the box, so none of these did anything there.
    /// </summary>
    private bool ServiceFieldKey(Avalonia.Visual f, Avalonia.Input.KeyEventArgs e)
    {
        if (ServiceHolding(f) is not Svc holder || holder.Which is not (GameWindow.GeneralStore or GameWindow.CompanionStore or GameWindow.Bank
                                  or GameWindow.Morgue or GameWindow.Guild)) return false;
        _svc = holder;
        if (!(f is TextBox or ComboBox
              || Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<TextBox>(f) != null
              || Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ComboBox>(f) != null)) return false;
        var k = e.Key;
        // "Select Case p063C / Case mc0640" - Shift = 2, Ctrl and nothing else
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            CharTab? tab = k switch
            {
                Avalonia.Input.Key.L => CharTab.Look, Avalonia.Input.Key.B => CharTab.Buffers,
                Avalonia.Input.Key.M => CharTab.Misc, Avalonia.Input.Key.S => CharTab.Stats,
                Avalonia.Input.Key.R => CharTab.Resist, Avalonia.Input.Key.C => CharTab.Char,
                Avalonia.Input.Key.G => CharTab.Guild, _ => null,
            };
            if (tab != null) { ShowCharacterTab(tab.Value, keepFocus: true); return true; }
            int? page = k switch
            {
                Avalonia.Input.Key.I => 0, Avalonia.Input.Key.P => 1, Avalonia.Input.Key.O => 2, _ => null,
            };
            if (page != null) { ShowSheetTab(page.Value, keepFocus: true); return true; }
            return false;
        }
        // "Case mc056E" - Shift = 4, Alt and nothing else; "Case 48 To 57", the digits
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Alt && k >= Avalonia.Input.Key.D0 && k <= Avalonia.Input.Key.D9)
        {
            if (_party.Size <= 1) return false;                   // "If UBound(Chars) > False"
            int slot = k - Avalonia.Input.Key.D1;                 // "p063A - 49"
            if (slot > _party.Size - 1) return false;             // "<= UBound(Chars)"
            // lblCall_Change switches only to a slot from 0 up (FRMGROUP.BAS:150-151): Alt+0 is
            // taken and does nothing
            if (slot >= 0) SwitchIfOther(slot);
            return true;
        }
        return false;
    }

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        // a key the preview already gave the game, and the control left alone, is not given twice
        if (ReferenceEquals(e, _previewed)) { _previewed = null; return; }
        // Alt+F4 is the frame's close, which the refusal box answers (FRMMAIN.BAS:171-175) - not F4
        if (OriginalBox.IsCloseChord(e)) { base.OnKeyDown(e); return; }
        // below ground every key is the dungeon's, which sorts out its own fields and lists
        if (_below != null) { _below.KeyFromFrame(e); return; }
        // never steal a keystroke someone is typing into a field
        if (FocusManager?.GetFocusedElement() is TextBox) { base.OnKeyDown(e); return; }
        // nor one meant for a combo box: Objects & Spells leaves every key but Escape to its Spell
        // Type box, and Escape sends the focus back (FRMCHAR3.BAS:313-314)
        if (FocusManager?.GetFocusedElement() is Avalonia.Visual fv
            && KeyOwner.InComboBox(fv))
        {
            if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; Focus(); return; }
            base.OnKeyDown(e); return;
        }

        var k = e.Key;
        // "you must use the CTRL key to access the tabs in Mordor... so the user can access a
        // specific tab from any window in the game" - the help's Commands page, which is why these
        // work in the city as well as the dungeon
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            CharTab? tab = k switch
            {
                Avalonia.Input.Key.L => CharTab.Look,
                Avalonia.Input.Key.B => CharTab.Buffers,
                Avalonia.Input.Key.M => CharTab.Misc,
                Avalonia.Input.Key.S => CharTab.Stats,
                Avalonia.Input.Key.R => CharTab.Resist,
                Avalonia.Input.Key.C => CharTab.Char,
                Avalonia.Input.Key.G => CharTab.Guild,
                _ => null,
            };
            if (tab is CharTab.Stats or CharTab.Buffers or CharTab.Misc) { SetTabOnly(tab.Value); e.Handled = true; return; }
            if (tab != null) { ShowCharacterTab(tab.Value); e.Handled = true; return; }
            int? page = k switch
            {
                Avalonia.Input.Key.I => 0,
                Avalonia.Input.Key.P => 1,
                Avalonia.Input.Key.O => 2,
                _ => null,
            };
            if (page != null) { ShowSheetTab(page.Value); e.Handled = true; return; }
        }
        // the same function keys fn1521 maps in the dungeon
        switch (k)
        {
            case Avalonia.Input.Key.F2: SetInfoTab(CharTab.Look); e.Handled = true; return;
            case Avalonia.Input.Key.F3: SetTabOnly(CharTab.Stats); e.Handled = true; return;
            case Avalonia.Input.Key.F4: SetInfoTab(CharTab.Resist); e.Handled = true; return;
            case Avalonia.Input.Key.F7: SetInfoTab(CharTab.Guild); e.Handled = true; return;
            case Avalonia.Input.Key.F9: SetTabOnly(CharTab.Buffers); e.Handled = true; return;
            case Avalonia.Input.Key.F6 when !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift):
                SetInfoTab(CharTab.Char); e.Handled = true; return;
        }
        // frmCityMain's own Form_KeyDown (FRMCTYM.BAS:35-58) sees an Alt letter first while the town
        // is the active window: C, S, B, M, R, G, X and D are its eight pictures, imgPicture_Click
        // 7, 0, 1, 2, 3, 4, 6 and 5 - the Confinement, the Store, the Bank, the Morgue, the Seer, the
        // Guilds, Exit and the Dungeon (183-221). Anything else goes on to fn1521, and so does every
        // Alt key while another window - the Party window, Objects & Spells - is the active one,
        // which is where ALT-s switches character instead. The active child is the one in front.
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Alt && CityIsActive())
        {
            Action? picture = k switch
            {
                Avalonia.Input.Key.C => () => ShowConfinement(this, null!),
                Avalonia.Input.Key.S => () => ShowStore(this, null!),
                Avalonia.Input.Key.B => () => ShowBank(this, null!),
                Avalonia.Input.Key.M => () => ShowMorgue(this, null!),
                Avalonia.Input.Key.R => () => ShowSeer(this, null!),
                Avalonia.Input.Key.G => () => ShowGuild(this, null!),
                Avalonia.Input.Key.X => () => LeaveTown(null, null!),
                Avalonia.Input.Key.D => () => EnterDungeon(null, null!),
                _ => null,
            };
            if (picture != null) { picture(); e.Handled = true; return; }
        }
        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt))
        {
            int slot = k switch
            {
                Avalonia.Input.Key.D1 => 0, Avalonia.Input.Key.D2 => 1,
                Avalonia.Input.Key.D3 => 2, Avalonia.Input.Key.D4 => 3, _ => -1,
            };
            if (slot >= 0)
            {
                // fn15DF (MAINMOD2.BAS:4222-4230): in a party, and only for a slot that exists,
                // the Party window switches to it - silently. "Switched to X." and "Nobody is in
                // that slot." were this port's.
                if (_party.Size > 1) SwitchIfOther(slot);
                e.Handled = true; return;
            }
            // ALT-s "Switch to Character (updates all windows)" is "If gv1A82 <> Me Then sub1536
            // Me" (MAINMOD2.BAS:3416-3418) - every window onto the character already in command,
            // not the next one. With one index kept here, that is a repaint.
            if (k == Avalonia.Input.Key.S)
            {
                RefreshSummary();
                e.Handled = true; return;
            }
            if (k == Avalonia.Input.Key.L) { AskToLeave(); e.Handled = true; return; }
            // fn1521 case 65 asks nothing of Shift (MAINMOD2.BAS:3444), and fn15DF takes no Alt letter
            // (4222-4232): Alt+A asks to join as a plain a does, when no picture or button has it
            if (k == Avalonia.Input.Key.A) { AskToJoin(); e.Handled = true; return; }
            if (k == Avalonia.Input.Key.P)
            {
                // fn1521's own order and wording. There are no monsters in town, so the combat
                // refusal cannot fire here, but the other two are the same lines.
                if (_party.Size == 1) { Log(Party.NobodyToPoolTo); e.Handled = true; return; }
                // a mod may ask for the Bank's Party Pool & Deposit instead (docs/MODDING.md); with
                // none, this is the original's on-hand pool
                var answer = Mordor.Modding.ModHost.Active.CityAltP(new Mordor.Modding.CityAltPRequest(_party, _ch));
                if (answer.Action == Mordor.Modding.CityAltPAction.PoolAndDeposit)
                {
                    Party.PoolAndDeposit(_party.Members, _ch);
                    Log(answer.Message ?? Party.PoolCompleted(_ch));
                    // the Bank's Party Pool & Deposit ends in sub2884 (FRMCITYB.BAS:62), and so the
                    // Wealthiest record is looked at whether or not the Bank is open to redraw
                    if (_bankRefresh != null) _bankRefresh(); else CheckWealthiest();
                }
                else
                {
                    _party.PoolGold();
                    Log(Party.PoolCompleted(_ch));
                }
                Save(); RefreshSummary();
                e.Handled = true; return;
            }
        }
        else if (k == Avalonia.Input.Key.A) { AskToJoin(); e.Handled = true; return; }
        // q, fn1521 case 81 (MAINMOD2.BAS:3619-3631): "Chars(0).DungeonLevel = False" always holds in
        // town, so it is "Exit to Main Menu?" (MsgBox 36, "EXIT") and Yes ends the game loop -
        // the city's own Exit (FRMCTYM.BAS:214-219) in all but the flag it sets (M6014 for M6008),
        // and both end in sub1738's sub16CC and frmMenu (MAINMOD2.BAS:6316-6331)
        else if (k == Avalonia.Input.Key.Q && !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        { LeaveTown(null, null!); e.Handled = true; return; }
        // "F1  HelpLesson", the first of the help's Game Commands
        else if (k == Avalonia.Input.Key.F1) { new HelpWindow().Show(); Focus(); e.Handled = true; return; }
        // "s  Swap Items" and "n  Select next Character" are Game Commands too - the help is
        // explicit that those "work in both the City and the Dungeon (any time)", and fn1521,
        // which handles them, is the routine every form's KeyDown goes through. They did nothing
        // at all in town.
        else if (k == Avalonia.Input.Key.S) { SwapItems(); e.Handled = true; return; }
        // 'g' Give Item/Gold. fn1521 case 71 has no dungeon test - unlike 'i', 'u' and 'c', which
        // all begin "If Chars(Me).DungeonLevel > 0" and so do nothing in town. Giving does.
        else if (k == Avalonia.Input.Key.G) { AskToGive(); e.Handled = true; return; }
        // 'p' Pickup Character and 'l' Drop Body. Neither case tests where you are either; what
        // decides is frmInteract's "Character is not here!", which compares the level and the
        // square (FRMINTER.BAS:199-206). In town both sides are the city, so anybody dead in town
        // is here. ALT-p and ALT-l are taken above - pool gold and ask to leave.
        else if (k == Avalonia.Input.Key.P) { PickUpBody(); e.Handled = true; return; }
        else if (k == Avalonia.Input.Key.L) { DropBody(); e.Handled = true; return; }
        else if (k == Avalonia.Input.Key.N)
        {
            // fn1521 case 78 (MAINMOD2.BAS:3731-3739): the next slot in order, dead or alive,
            // wrapping to the first, and nothing said
            if (_party.Size > 1 && _party.Select((_party.SelectedIndex + 1) % _party.Size)) SwitchedTo();
            e.Handled = true; return;
        }
        else if (k == Avalonia.Input.Key.F8) { ShowAutomap(null, null!); e.Handled = true; return; }
        // "<" and ">" page the automap from anywhere in the game - see AutomapKeys
        else if (AutomapKeys.Handle(k, e.KeyModifiers, _automapChild, _automap)) { e.Handled = true; return; }
        // "A character can access the Control Panel by Pressing F10 in either the City or Dungeon"
        else if (k == Avalonia.Input.Key.F10) { ShowControlPanel(null, null!); e.Handled = true; return; }
        // F5, "Guild Experience Information" - sub1572 case 2 writes it as a message and no more
        else if (k == Avalonia.Input.Key.F5) { SayProgress(); e.Handled = true; return; }
        // "F11  Toolbar Toggle"
        else if (k == Avalonia.Input.Key.F11) { Tools.IsVisible = !Tools.IsVisible; e.Handled = true; return; }
        else if (k == Avalonia.Input.Key.F6 && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            CharStatFile.Write(_ch, _gameDir, Lookup);
            // MAINMOD2.BAS:5231. Its closing "BETA: Finished writing Character Statistics..." (5332)
            // is behind "If p06E4 = True", which the ability loop leaves at the last chance, so it
            // is never said - see CharStatFile.
            Log("BETA: Writing Character Statistics to File...");
            SetInfoTab(CharTab.Char);   // fn1521 case mc0596 paints the page it wrote (3553-3555)
            e.Handled = true; return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>
    /// F5 and the toolbar button beside it: what is still owed for the next level, and the guild
    /// quest if one is set. <see cref="Progress"/> is the wording; the original puts both lines in
    /// the Messages window.
    /// </summary>
    private void SayProgress()
    {
        Log(Progress.NextLevel(_ch));
        string? quest = Progress.Quest(_ch, id => _monsters.FirstOrDefault(m => m.Id == id), Lookup);
        if (quest != null) Log(quest);
    }

    /// <summary>
    /// The toolbar buttons. The city has no monsters to pick up bodies from and no dungeon to drop
    /// them in, but the buttons are the same ones - the original leaves them all on the strip and
    /// lets each command refuse for itself.
    /// </summary>
    private void OnToolCommand(ToolCommand cmd)
    {
        switch (cmd)
        {
            case ToolCommand.Help: new HelpWindow().Show(); break;
            case ToolCommand.Experience: SayProgress(); break;
            case ToolCommand.Automap: ShowAutomap(null, null!); break;
            case ToolCommand.AskToJoin: AskToJoin(); break;
            case ToolCommand.ControlPanel: ShowControlPanel(null, null!); break;
            // "Not in the city." was this port's own line, and it was wrong as well as invented:
            // fn1521's cases for 'g' (71), 'p' (80) and 'l' (76) have no dungeon test at all, and
            // what does decide - frmInteract's "Character is not here!" - passes for anybody who
            // is in town with you. All three work here.
            case ToolCommand.GiveItem:
                AskToGive();
                break;
            case ToolCommand.PickupBody: PickUpBody(); break;
            case ToolCommand.DropBody: DropBody(); break;
        }
        Focus();
    }

    /// <summary>
    /// The city heals you the moment you walk in, and ages you for it (help, "The City"). Returns a
    /// line for each thing that happened, each its own sub085B line, and none if you arrived in
    /// good shape.
    /// </summary>
    private List<string> RestOnArrival()
    {
        // FRMCTYM.BAS:87 - reaching the city settles where you are. It clears the level and
        // position flags but leaves the facing one, which means nothing above ground anyway.
        foreach (var m in _party.Members) { m.LostLevel = false; m.LostPosition = false; }

        // FRMCTYM.BAS:105, sub0956: everyone is put on the leader's level and square; and every
        // member's M59BB is -1 in town, set before and again after the save (112, 132 - also in
        // RestInCity). The autoload compares it (MULTIMOD.BAS:248).
        _party.PlaceAt(_party.Leader.DungeonLevel, _party.Leader.X, _party.Leader.Y);
        foreach (var m in _party.Members) m.Area = -1;

        // FRMCTYM.BAS:104 checks the experience record for everyone who walked in
        {
            var hall = HallRecords.Load(SavesDir);
            foreach (var m in _party.Members)
                hall.CheckAndLog(HallRecords.Kind.MostExperience, m, HallRecords.TotalExperience(m), SavesDir, null, _party.Size == 1);
            hall.Save(SavesDir);
        }

        // FRMCTYM.BAS:103-128, for each member in turn: mdrcharAddAge's rest - which says "You rested
        // for 1 week(s) and 2 day(s)." (or "\"Ann\" rested for ..." in a party), and nothing if no
        // time passed (MAINMOD2.BAS:6392-6399) - and then, if they carry a character, the body is
        // left here: "You are no longer carrying \"Bob\"." / "\"Ann\" is no longer carrying \"Bob\"."
        // (124-126). A carried companion stays carried. The port had "Your wounds are tended (5 ->
        // 15 hits), the poison and disease purged from you. N days pass; you are now 17." and, on
        // the morgue page, "... The keepers take the body." with the companion dropped too.
        //
        // The store and the pens are replenished only by the first character's rest (6384-6389,
        // "If p088E = m00A6"); the port had taken the longest stay in the party.
        var lines = new List<string>();
        int days = 0;
        bool party = _party.Size > 1;
        foreach (var m in _party.Members)
        {
            int d = m.RestInCity();
            Effects.Recompute(m, Lookup);   // what the cleared spell effects leave standing
            if (m == _party.Members[0]) days = d;
            if (d > 0)
                lines.Add(party ? $"\"{m.Name.TrimEnd()}\" rested for {Character.DescribeDays(d)}."
                                : $"You rested for {Character.DescribeDays(d)}.");
            if (m.CarryingCharacter != "")
            {
                lines.Add(party ? $"\"{m.Name.TrimEnd()}\" is no longer carrying \"{m.CarryingCharacter.TrimEnd()}\"."
                                : $"You are no longer carrying \"{m.CarryingCharacter.TrimEnd()}\".");
                m.CarryingCharacter = "";
            }
        }
        // mdrcharAddAge calls mdrReplenishStore with the days you just aged, so other
        // adventurers have been trading with the store while you recovered
        if (days > 0)
        {
            var shelf = StoreStock.Load(SavesDir, _items);
            // sub071B reads the deepest floor anyone has reached out of the dungeon file, and
            // only ever draws from at or above it
            int deepest = DungeonState.Load(SavesDir).DeepestReached;
            // each item sold in is filed in the Library, fully known and found by no one
            // (sub0730 m0070, -l02C4, m029E, -2, True - MAINMOD.BAS:1586)
            var library = Librarian.Store.Load(SavesDir, _monsters);
            int today = Librarian.Serial(DateTime.Now);
            bool filed = false;
            shelf.Replenish(_items, days, new Random(), deepest, it => { Librarian.FileRestock(library, it.Id, today); filed = true; });
            shelf.Save(SavesDir);
            if (filed) library.Save(SavesDir);

            // mdrReplenishCStore runs off the same aging, for the Confinement pens
            var pens = Confinement.Load(SavesDir, _monsters);
            pens.Replenish(_monsters, days, new Random(), deepest);
            pens.Save(SavesDir);
        }
        Save();
        return lines;
    }

    private string SavesDir => Saves.DirFor(_gameDir);

    /// <summary>Smoke tests only: the auto-press of Make Level has already happened.</summary>
    private bool _autoLevelled;

    /// <summary>A line of the character window, laid out as the original's tabs are: label left, value right.</summary>
    private static Grid Row(string label, string value, IBrush? colour = null, bool bold = false) =>
        new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = label, Foreground = colour ?? Brushes.LightGray, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal },
                new TextBlock { Text = value, Foreground = colour ?? Brushes.White, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
                                HorizontalAlignment = HorizontalAlignment.Right, [Grid.ColumnProperty] = 1 },
            },
        };

    private static Control Gap(double h = 8) => new Border { Height = h };

    private void RefreshSummary()
    {
        RefreshCharacterWindow();
        RefreshParty();
        // a dead screen no longer owed goes, and the town comes back (SyncDeadScreen)
        SyncDeadScreen();
    }

    /// <summary>
    /// The Party Information window, frmParty (FRMGROUP.BAS), laid out as game_files/bm5.png:
    /// Name, Hits, Spells, Status and Option per member, and Switch To, Options and Leave under
    /// them - the three fixed buttons of cmdCommand_Click (FRMGROUP.BAS:32-46), in the axaml.
    /// A click on a row is mdrmulSwitch, which in town ends in sub1536 - a full switch
    /// (MULTIMOD.BAS:632-638); a double click is sub1536 itself (FRMGROUP.BAS:181-183).
    /// </summary>
    private void RefreshParty()
    {
        Mordor.Modding.ModHost.Active.GameChanged();   // a mod's own window looks again (ctx.Game)
        PartyRows.Children.Clear();
        var modColumns = ModSlots.PartyHeader(PartyHeader, 5);   // the mods' party columns, if any (docs/MODDING.md)
        for (int i = 0; i < _party.Size; i++)
        {
            var m = _party.Members[i];
            bool selected = i == _party.SelectedIndex;
            // modUpdateStats (FRMGROUP.BAS:436-452). Paralysis is read off the character; the
            // city gate zeroes it (FRMCTYM.BAS:115).
            string status = PartyRow.Status(m);
            bool well = PartyRow.StatusIsWell(m);
            // mdrmulSwitch (MULTIMOD.BAS:613-616, 640-642): every Name and Hits black, then the
            // selected one's green, and its Name bold. There is no row marker in bm5.png.
            // The header's columns, and each label where frmParty's form data puts it (the table
            // in CityWindow.axaml): a row is lblName(i)'s 195 twips, 13 pixels, tall, or a larger
            // Options font's line (PartyRowHeight). lblOption is the standing combat order, which
            // carries between town and dungeon. See PartyRowGrid.
            var row = PartyRowGrid(m, selected, status, well,
                                   _party.OrderFor(m).Describe(id => _spells.FirstOrDefault(s => s.Id == id)));
            ModSlots.PartyRow(row, m, modColumns, PartyRowColours.Fixed, id => _monsters.FirstOrDefault(x => x.Id == id), Lookup);
            int slot = i;
            // Tapped, not PointerPressed: a press that turns into a drag must not rebuild the
            // rows underneath it. See the note in the dungeon's copy.
            row.Tapped += (_, _) => SwitchIfOther(slot);   // lblName_Click: mdrmulSwitch, which says nothing
            // lblName_DblClick: "sub1536 Index" (FRMGROUP.BAS:181-183), even onto the one selected
            row.DoubleTapped += (_, e) => { _party.Select(slot); SwitchedTo(); e.Handled = true; };

            // lblName_DragDrop (FRMGROUP.BAS:185-250). The rearrange refusal is inside
            // "If Chars(0).DungeonLevel > False", so there is none in town, and there is no combat
            // here for the giving one either.
            Dragging.Source(row, () => DragPayload.Member(slot));
            Dragging.Target(row, DragKind.Character, p =>
            {
                string? leader = _party.Swap(p.Index, slot);
                Save();
                RefreshParty();
                RefreshSummary();
                if (!string.IsNullOrEmpty(leader)) Log(leader);
            });
            Dragging.Target(row, DragKind.Item, p =>
            {
                if (slot == _party.SelectedIndex) return;
                var to = _party.Members[slot];
                if (p.Index <= 0 || p.Index >= Character.PackSize) return;
                // "sub14B5 gv1A82, Index, CInt(Source.Index), False" and then, whatever it did,
                // "If fn03BD(frmCityStore) Then frmCityStore!lblCall = "UPDATE"" (FRMGROUP.BAS:205-206):
                // the Store's boxes name slots of the pack the item has left
                try
                {
                    if (_ch.Pack[p.Index] is not InventorySlot sl || sl.ItemId == 0) return;
                    var it = Lookup(sl.ItemId);
                    if (Party.GiveItem(_ch, to, sl, it, Lookup, Log, FiledFor(to)) is string no) { GiveRefused(to, no); return; }
                    Save();
                    RefreshPack();
                    RefreshParty();
                    // an empty slot sets p049C = False and says nothing (MAINMOD2.BAS:2969-2970) - the
                    // return above; a give that went across ends with the Completed line
                    // (MAINMOD2.BAS:3004-3006)
                    Log(Party.GaveItem(to));
                }
                finally { _storeUpdate?.Invoke(); }
            });
            // a line of the Bank's vault, tagged "BANK" + slot: handled first of all, before the
            // combat test and before the drop-on-yourself test (FRMGROUP.BAS:189-193), so onto
            // your own name it goes into your own pack
            Dragging.Target(row, DragKind.Vault, p =>
            {
                var to = _party.Members[slot];
                string? no = Party.GiveFromVault(_ch, p.Index, to, Lookup, Log, FiledFor(to));
                if (no == "") return;
                if (no != null)
                {
                    // the full pack is a MsgBox, style 64, titled "SORRY!" (MAINMOD2.BAS:2985)
                    if (no == Party.NoFreeSlotsFromVault(to)) _ = Tell("SORRY!", no);
                    else Log(no);
                    return;
                }
                Save();
                _bankRefresh?.Invoke();      // frmCityBank!lblCall = "UPDATE" (MAINMOD2.BAS:2998)
                RefreshPack();
                RefreshParty();
                RefreshCharacterWindow();
                Log(Party.GaveItem(to));     // MAINMOD2.BAS:3004-3006
            });
            // a companion picture ("COMP") is neither a vault line, an item nor a name, and in town
            // there is no combat test: lblName_DragDrop runs lblName_Click Index (FRMGROUP.BAS:220)
            // and nothing else, so the commands go to the member it was let go over
            Dragging.Target(row, DragKind.Companion, _ => SwitchIfOther(slot));
            PartyRows.Children.Add(row);
        }
    }

    /// <summary>
    /// mdrmulSwitch in town (MULTIMOD.BAS:631-638): the open Confinement, Morgue and Bank are told
    /// "UPDATE", then sub1536 repaints every window onto the character now selected. Every switch
    /// comes here - the name click, ALT-1..4 and n alike - so no window goes on showing the last one.
    /// </summary>
    /// <summary>
    /// lblName_Click, "If Index &lt;&gt; Me Then mdrmulSwitch Index" (FRMGROUP.BAS:175-179), and
    /// lblCall_Change, "If Me &lt;&gt; Val(lblCall.Caption) Then ... mdrmulSwitch" (150-151), which
    /// Alt+1..4 go through: the one already in command is no switch at all, so nothing is
    /// repainted and the Spell Type box keeps its class. A drop of a companion picture runs
    /// lblName_Click too (220).
    /// </summary>
    private void SwitchIfOther(int slot)
    {
        if (slot == _party.SelectedIndex || !_party.Select(slot)) return;
        SwitchedTo();
    }

    private void SwitchedTo()
    {
        // sub1536 (MULTIMOD.BAS:638): "frmCharStat3!cmbClass.Clear", txtSpell emptied, sub1658 on
        // the empty box (675-677) - the first class listed again
        SpellPage.Clear(ClassBox);
        SpellText.Text = "";
        RefreshSummary();
        // the open services told "UPDATE" in mdrmulSwitch's order (MULTIMOD.BAS:633-637); the Seer
        // (frmSeek) is not among them, and is left as it was. They are the town branch of the
        // test at 622, which asks about the leader, Chars(m0022) with m0022 = 0 - not the one
        // switched to: a leader below ground or dead takes the dungeon branch and no service
        // hears anything, while a dead member under a living leader in town gets them all
        var leader = _party.Members.Count > 0 ? _party.Members[0] : _ch;
        if (leader.DungeonLevel <= 0 && !leader.IsDead)
        {
            _storeUpdate?.Invoke();       // frmCityStore!lblCall = "UPDATE" (MULTIMOD.BAS:633)
            _confinementUpdate?.Invoke(); // frmCityCStore (634)
            _morgueUpdate?.Invoke();      // frmCityMorgue (635)
            _guildUpdate?.Invoke();       // frmCityGuild (636)
            _bankRefresh?.Invoke();      // frmCityBank!lblCall = "UPDATE" (MULTIMOD.BAS:637)
        }
        Mordor.Modding.ModHost.Active.CharacterSwitched(_ch, inDungeon: false);
    }

    /// <summary>
    /// Switch To, cmdCommand(0): "If gv1A82 &lt;&gt; Me Then sub1536 Me" (FRMGROUP.BAS:35-36) -
    /// the same as ALT-s, every window onto the character already selected.
    /// </summary>
    private void OnPartySwitchTo(object? s, Avalonia.Interactivity.RoutedEventArgs e) => RefreshSummary();

    /// <summary>
    /// Options, cmdCommand(1) (FRMGROUP.BAS:37-42): frmPartyOptions modally when fn0EEF() finds
    /// no monsters, else "Cannot set default options while in Combat!". There are none in town,
    /// so it always opens here.
    /// </summary>
    private void OnPartyOptions(object? s, Avalonia.Interactivity.RoutedEventArgs e) =>
        // fn0EEF() is False in town, so the refusal branch has nothing to test here
        PartyOptionsDialog.Show(this, _party, () => { Save(); RefreshParty(); });

    /// <summary>Leave, cmdCommand(2): sub159D (FRMGROUP.BAS:43-44), always shown.</summary>
    private void OnPartyLeave(object? s, Avalonia.Interactivity.RoutedEventArgs e) => AskToLeave();

    /// <summary>
    /// 'a', fn1521 case mc0576 (MAINMOD2.BAS:3444-3523): a full party is told so on the Messages
    /// list; otherwise "Ask to Join" / "Character's Name?" offers the characters here who are not in
    /// the party ("No other characters in the area!" when there are none), then "Enter Password" if
    /// they have one - wrong, and a SORRY! box says "Incorrect Password!" - and an oppositely aligned
    /// one is refused on the Messages list. Joining in town restores hits and spell points, silently.
    /// </summary>
    private async void AskToJoin()
    {
        // "If Chars(m00A6).DeathState = False Then" and no else (MAINMOD2.BAS:3446): m00A6 is the constant 0
        // (17), the party's first member; the city had asked nothing
        if (_party.Members[0].IsDead) return;
        if (_party.Size >= Party.MaxSize) { Log("Your party is too full!"); return; }
        var here = Character.LoadAllFrom(SavesDir)
            .Where(c => _party.Members.All(m => m.Name != c.Name) && Party.SamePlace(_party.Leader, c))   // the dead are offered too (FRMINTER.BAS:352-387)
            .ToList();
        int pick = await NamePicker.Ask(this, "Ask to Join", "Character's Name?",
                                        here.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("JOIN"));
        if (pick < 0) return;
        var who = here[pick];
        if (who.HasPassword)
        {
            string? said = await AskPassword("Ask to Join", "Enter Password");
            if (said == null) return;
            if (!who.PasswordMatches(said)) { await Tell("SORRY!", "Incorrect Password!"); return; }
        }
        if (_party.Members.Any(m => Party.Opposed(m.Alignment, who.Alignment)))
        {
            Log($"\"{who.Name.TrimEnd()}\" is oppositely aligned to one or more members of the party!");
            return;
        }
        // sub0848 True (MAINMOD2.BAS:3477): the newcomer takes Chars(0)'s three lost flags
        // (MULTIMOD.BAS:454-458), before the dead are turned away - and a refusal from here on
        // unloads them with those flags saved (mdrcharUnload, MAINMOD.BAS:419-423)
        Party.ShareLostFlags(_party.Members[0], who);
        string whoFile = System.IO.Path.Combine(SavesDir, Character.SafeFileName(who.Name) + ".json");
        // MAINMOD2.BAS:3478-3499, once loaded. There are no monsters in town, so the combat
        // refusal cannot fire here; the body in your arms can, and then a raise waiting on them
        // is offered (fn16B5) or they are simply dead.
        if (who.IsDead)
        {
            if (_ch.CarryingCharacter != "" && _ch.CarryingCharacter.TrimEnd() == who.Name.TrimEnd())
            { Log("Drop the character before you raise them!"); who.Save(whoFile); return; }
            if (!PendingRaise.IsPending(who))
            {
                Log($"\"{who.Name.TrimEnd()}\" is Dead!  Cast a Resurrect spell on the character to have them re-join!");
                who.Save(whoFile);
                return;
            }
            if (!await OfferPendingRaise(who, alone: false)) return;
        }
        // "Chars(UBound(Chars)).X = Chars(m00A6).X ..." (MAINMOD2.BAS:3507-3509): they take the
        // leader's place; fn1521 has no square check of its own, and no refusal goes unsaid
        who.DungeonLevel = _party.Leader.DungeonLevel; who.X = _party.Leader.X; who.Y = _party.Leader.Y;
        if (_party.WhyCannotJoin(who) is string refused) { Log(refused); return; }
        _party.Add(who);
        who.CurHits = who.MaxHits;
        who.CurSpellPoints = who.MaxSpellPoints;
        // sub154A (MAINMOD2.BAS:3511) runs mdrcharGetCarry for the newcomer (MULTIMOD.BAS:380)
        foreach (string line in CompanionCarry.Run(who, who == _party.Selected, c => c.Def(_monsters))) Log(line);
        // and then "frmParty!lblCall = "UPDATE"" (MULTIMOD.BAS:382): anyone at nought leaves (FRMGROUP.BAS:321-346)
        PruneTheDead();
        Save();
        RefreshSummary();
    }

    /// <summary>
    /// The game-wide settings, MORDOR.INI. The city reads them for the toolbar and the Options
    /// window writes them.
    /// </summary>
    private GameOptions _options = new();

    /// <summary>
    /// The Control Panel, <c>FRMCONTR</c>, the same window the dungeon opens on F10 - "A character
    /// can access the Control Panel by Pressing F10 in either the City or Dungeon".
    ///
    /// Its Window Control list is the city frame's own children, which is what the help's
    /// screenshot of this window shows - it was taken in the city.
    /// </summary>
    private async void ShowControlPanel(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveWindowLayout();
        var (fw, fh) = _frame.ScaleArea(Screens);
        var panel = new ControlPanelWindow(_ch, Leader, _frame.Listed(), fw, fh, _options, _gameDir);
        panel.OptionsChanged += () => { Tools.IsVisible = !_options.NoToolbar; Audio.Apply(_options); };
        await OriginalBox.ShowOver(panel, this);
        ApplyWindowLayout();
        Save();
        RefreshSummary();
        Focus();
        // Form_Unload, however the panel is shut: "If Chars(m008C).DeathState Then sub0930 m008C", m008C = 0
        // (FRMCONTR.BAS:245-247; m008C is the form's Const 0) - for the first member dead, sub0930 again: its head, and alone the
        // dead screen and its boxes once more; in company the party list's UPDATE (MAINMOD2.BAS:3016-3125)
        if (Leader.IsDead) ArriveDeadSub0930(Leader);
    }

    /// <summary>
    /// The Automap, from town. It starts on level 1, because <c>Form_Load</c> falls back to 1 when
    /// the character is not in the dungeon (<c>Chars(0).DungeonLevel &lt;&gt; 0</c> fails,
    /// FRMAUTOM.BAS:94-98). The one exception is a body lying below ground, whose dead screen
    /// stands in for frmDungeonMain here: Chars(0).DungeonLevel is the body's level, so the map
    /// opens there, and medDrawCursor puts the arrow on the body's square (306-326).
    ///
    /// A child of the frame here, not a dialog, which is what it is in the original and what lets
    /// it be open while the town is used. F8 and the toolbar's map button both land here, and so
    /// does sub14E0 on arrival. F8 is <c>frmAutoMap.Show</c> and <c>WindowState = False</c>
    /// (MAINMOD2.BAS:3560-3563): a map already open is restored and brought up, not closed - the
    /// port had made a second press close it - and only a map that is not open is loaded afresh.
    /// </summary>
    private void ShowAutomap(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_automap != null && _automapChild.IsVisible)
        {
            _automapChild.Minimised = false;
            _automapChild.Reopen();
            Focus();
            return;
        }
        var map = SharedMap.Load(SavesDir);
        // Form_Load reads the deepest level out of the maze file and then says "If m0038 = 0 Then
        // m0038 = 1" (FRMAUTOM.BAS:92) - a game nobody has been down in yet still opens the map,
        // on level 1, with nothing drawn on it. This refused instead, which was this port's own
        // idea, and now that the map opens on arrival it would be the first thing a new player saw.
        int deepest = Math.Max(DungeonState.Load(SavesDir).DeepestReached, map.DeepestVisited);
        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(_gameDir, "MDATA11.MDR"));
        // Chars(0).X and .Y are where modAdjustMap centres a small map (FRMAUTOM.BAS:465, 482),
        // and they still hold the square the leader walked out from
        // and Chars(0) below ground - only ever a body, here - is where Form_Load (FRMAUTOM.BAS:94-98)
        // and medDrawCursor (306-326) look; Facing is stored from 1 (gv06E8, MODULE1.BAS:543-546)
        var lead = Leader;
        (int, int, int, Facing)? body = lead.DungeonLevel > 0
            ? (lead.DungeonLevel, lead.X, lead.Y, (Facing)((lead.Facing + 3) & 3))
            : null;
        var win = new AutomapWindow(dungeon, map, deepest, body, lost: body != null && lead.IsLost,
                                    lostLevel: body != null && lead.LostLevel,
                                    noBlink: () => _options.NoBlinkCursor,
                                    square: (lead.X, lead.Y));
        _automap = win;
        win.PutInto(_automapChild);
        Focus();        // the keys still belong to the city
    }


    /// <summary>
    /// Help: "You can remove a party member by asking them to 'L'eave." ALT-l and the Leave button
    /// both run <c>sub159D</c> (MULTIMOD.BAS:739-758): in town the monster refusal cannot apply,
    /// the leader is refused - which is what a lone character hears too - and then it asks. The
    /// removal itself says nothing. This port had let the leader leave, reading the removal
    /// (mdrcharUnload) as the whole command, but the Leave key and button only reach it through sub159D.
    /// </summary>
    private async void AskToLeave()
    {
        var who = _ch;
        if (_party.SelectedIndex == 0) { Log("Leader cannot leave Party!"); return; }
        if (!await Confirm("Leave", $"Ask \"{who.Name.TrimEnd()}\" to Leave?")) return;
        if (!_party.Remove(who)) return;
        // sub1776's "frmParty!lblCall = "UPDATE"" (MULTIMOD.BAS:755, 701-702): anyone at nought leaves too
        PruneTheDead();
        Save();
        RefreshSummary();
    }

    /// <summary>
    /// Show a service over the city, the way the original opens its own window for each: at the
    /// place and size its own table entry keeps (<see cref="PlaceService"/>). Each service has a
    /// window of its own (CityWindow.Services.cs), since frmCityMain shows them modeless
    /// (FRMCTYM.BAS:183-203): one already up is built again in its own window, and any other stays
    /// as it is. The window opened is the active form, as a form just shown is.
    /// </summary>
    private void OpenService(string title, GameWindow? which = null)
    {
        _svc = WindowFor(which);
        // whatever this window held before writes its placement first
        SaveServicePlacement();
        ShowPortPage();   // the page about to be built says whether it is drawn to its form (ShowForm)
        _serviceExtraWidth = 0;   // the page about to be built says again if it wants more (the Store's mod columns)
        // a service of the town's own is a window of its own, which nothing returns to
        if (which != null) _serviceReturn = null;
        _serviceWhich = which;
        ServiceTitle.Text = title;
        // sub0930's screen has only its own controls: nothing closes it but a raise or a rescue
        ServiceExit.IsVisible = which != GameWindow.Dungeon;
        // a new page starts at its top, not where the last one was scrolled to
        if (ServicePage.Parent is ScrollViewer pages) pages.Offset = default;
        ServicePanel.IsVisible = true;   // Track shows the window
        Host.Active = _serviceChild;
        PlaceService();                  // and Opened again, for one put up before the frame had a size
        // and once the page the caller is about to build is in, sized to it
        var mine = _svc;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (mine.Open) InServiceWindow(mine, PlaceService); },
                                                    Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// The window at hand unloaded (its Exit, or its Control-menu Close), or its page given back to
    /// the Guild it was opened over. The other services that are up stay up.
    /// </summary>
    private void CloseService(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var back = _serviceReturn;
        _serviceReturn = null;
        if (back != null) { back(); return; }
        var closing = _svc;
        SaveServicePlacement();
        ShowPortPage();
        // frmDungeonMain unloaded at level 0 is frmCityMain shown again (sub14CC, MAINMOD2.BAS:3150-3152)
        if (closing.Key == GameWindow.Dungeon) { _deadScreenAt = null; _cityChild.IsVisible = true; }
        _serviceExtraWidth = 0;
        _serviceWhich = null;
        _serviceKeys = null;
        closing.Key = null;
        ServicePanel.IsVisible = false;
        ServicePage.Content = null;
        // a child window unloaded hands the activation to the one beneath it: another service
        // still up, the topmost, or the town
        var under = OpenServices.OrderByDescending(o => o.Child.ZIndex).FirstOrDefault();
        if (under != null) _svc = under;
        if (Host.Active == closing.Child) Host.Active = under?.Child ?? _cityChild;
        RefreshSummary();
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180): whatever they closed,
        // the dead screen is what is left
        if (DeadScreenOwed) ShowDead();
    }

    private void Save()
    {
        try
        {
            _party.SaveAll(Saves.DirFor(_gameDir));
        }
        catch { /* non-fatal */ }
    }

    /// <summary>
    /// charPay (MAINMOD2.BAS:693-709) for every town service: gold on hand first, then the bank
    /// (<see cref="Character.Pay"/>), false and nothing taken when the purse is short. Every town
    /// charge comes here. Before it, a mod may have the rest of the party make up the shortfall
    /// (docs/MODDING.md, <c>CoverShortfall</c>); with none, or when the party cannot cover it,
    /// this is charPay alone and the caller's refusal is the original's.
    /// </summary>
    private bool CharPay(long cost)
    {
        CoverShortfall(cost);
        bool paid = _ch.Pay(cost);
        // a charge paid ends in mdrcharStats False, 10 (MAINMOD2.BAS:708; a refusal leaves at 697), which
        // in town tells an open Bank "GOLD" (5616-5620): its status line, the bank gold drawn on and all
        if (paid) _bankGold?.Invoke();
        return paid;
    }

    /// <summary>
    /// Whether <see cref="CharPay"/> would succeed: the purse test the town makes ahead of a charge
    /// (Make Level's button and refusal, the Seer's Locate Character). With no mod it is the
    /// original's <c>Gold + BankGold &gt;= cost</c>; a mod covering shortfalls counts what the
    /// party could make up. Moves nothing.
    /// </summary>
    private bool CanPay(long cost) =>
        _ch.Purse >= cost
        || Mordor.Modding.ModHost.Active.WouldCoverShortfall(_party, _ch, cost);

    /// <summary>
    /// A mod's party cover for a short purse: the gold moves (Party.CoverShortfall), each giver's
    /// line goes to Messages, the party is saved and the toolbar, party and an open Bank redrawn.
    /// Only balances going down, so the Wealthiest record has nothing new to see.
    /// </summary>
    private void CoverShortfall(long cost)
    {
        if (_ch.Purse >= cost) return;
        if (Mordor.Modding.ModHost.Active.CoverShortfall(_party, _ch, cost) is not { } lines) return;
        foreach (string line in lines) Log(line);
        Save();
        RefreshSummary();
        _bankRefresh?.Invoke();
    }

    // ---------------- Guild Hall ----------------

    /// <summary>
    /// <c>cmdInfo_Click</c> (FRMCTYG.BAS:45-81): what the guild wants from you and what you have,
    /// and the quest underneath when there is one. See <see cref="GuildHall.Info"/>.
    /// </summary>
    private string GuildInfo()
    {
        string target = _ch.Guild.QuestType == 1
            ? _monsters.FirstOrDefault(m => m.Id == _ch.Guild.QuestTarget)?.Name ?? "monster"
            : Lookup(_ch.Guild.QuestTarget)?.Name ?? "item";
        return GuildHall.Info(_ch.ExpForNextLevel(), _ch.Guild.Experience,
                              _ch.GoldForNextLevel(), _ch.Purse,
                              _ch.Guild.QuestType, target, _ch.Guild.QuestDone);
    }
    /// <summary>The guild being looked at, <c>m001A</c>: the current one on opening (FRMCTYG.BAS:630).</summary>
    private string _guildVisited = "";

    /// <summary><c>frmCityGuild!lblCall = "UPDATE"</c> for the open Guild; null when it is shut.</summary>
    private Action? _guildUpdate;

    /// <summary>Whether the last redraw of the guild tested Make Level against experience and gold.</summary>
    private bool _makeTested = true;

    /// <summary>
    /// frmCityGuild (FRMCTYG.BAS). The guild list (<c>lstGuilds</c>, sub2974) holds every guild the
    /// character's race and alignment allow, "Name (level)" for the ones joined; Visit shows the
    /// selected one - its welcome, its greeting (sub2962), its GuildMaster, the stats it asks for.
    /// The seven buttons are Join (captioned Re-Acquaint for a guild already joined, and the only
    /// way to change the active guild), Visit, Make Level, Exp. Info, Library, Guild Log and Exit.
    /// Join is live only for a guild that is not the current one and does not refuse you; Make Level
    /// and Exp. Info only for the current guild, Make Level also needing the experience and the gold.
    /// </summary>
    private void ShowGuild(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180): back to the dead screen
        if (DeadScreenOwed) { ShowDead(); return; }
        // a picture clicked for one already up brings it forward and builds nothing again (imgPicture_Click, FRMCTYM.BAS:192-203)
        if (s != null && BringUpOpenService(GameWindow.Guild)) return;
        OpenService("Guilds", GameWindow.Guild);
        // Form_Load starts on the current guild every time (FRMCTYG.BAS:630), and tests Make Level
        // against experience and gold (648-653); a redraw after an action keeps what was visited
        if (s != null || _guildVisited == "" || GuildData.All.All(g => g.Name != _guildVisited))
        {
            _guildVisited = _ch.Guild.Guild;
            _makeTested = true;
        }
        // in the Guild's own window, whichever is at hand: lblCall is the Guild form's own label
        InServiceWindow(GameWindow.Guild, DrawGuild);
    }

    /// <summary>
    /// frmCityGuild's lblCall "UPDATE" (FRMCTYG.BAS:671-686): the form redraws its controls where it
    /// stands - no Form_Load, so the window is not placed again. The port's page is rebuilt onto
    /// the same service window at the form's own size, so the window still fits it; sub2974 clears
    /// lstGuilds and fills it again, so the list starts from its top as the original's does.
    /// </summary>
    private void RedrawGuildInPlace()
    {
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180), as ShowGuild has it
        if (DeadScreenOwed) { ShowDead(); return; }
        // in the Guild's own window, whichever is at hand: lblCall is the Guild form's own label
        InServiceWindow(GameWindow.Guild, DrawGuild);
    }

    /// <summary>The Guild's page for <see cref="_guildVisited"/>, put into the open service window.</summary>
    private void DrawGuild()
    {
        var def = GuildData.ByName(_guildVisited);
        int index = GuildData.IndexOf(def.Name);
        var member = _ch.Guilds.FirstOrDefault(m => m.Guild == def.Name);
        bool isCurrent = def.Name == _ch.Guild.Guild;
        var (greeting, refused) = GuildHall.Greeting(def, _ch, alreadyAMember: member != null && member.Level > 0);

        // frmCityGuild's form data (MORDOR.EXE RCDATA 20): a 5220x3420-twip client, and every
        // control in elaElastic(0) at -15,-15 (ServiceForms.Guild has the rectangles)
        var form = new FormPage(ServiceForms.Guild.ClientW, ServiceForms.Guild.ClientH, ServiceForms.Guild.Elastic);
        var root = form.Root;
        // lblWelcome, 12 points bold (Tag ":12"), blue, centred (sub294F, FRMCTYG.BAS:831)
        FormPage.Label(root, $"Welcome to the {def.Name}'s guild!", ServiceForms.Guild.lblWelcome,
                       DesignFonts.Welcome, FormPage.Blue, align: 2);
        FormPage.Label(root, "GuildMaster", ServiceForms.Guild.GuildMasterCaption, FormPage.Maroon);     // Label2(1)
        // lblGuildMaster, BorderStyle 1, 8.25 (sub294F, 835-845)
        FormPage.Label(root, Guildmasters.LoadSeeded(SavesDir).Describe(index, _ch), ServiceForms.Guild.lblGuildMaster,
                       bold: false, boxed: true);
        FormPage.Label(root, "Stats Required", ServiceForms.Guild.StatsCaption, FormPage.Maroon);  // Label2(0)
        // elaElastic(1) holds lblStatName and lblStat, Courier New 8.25; Form_Load loads 1..5 each
        // at the one before's Left + Width (FRMCTYG.BAS:631-640), and sub294F fills the numbers
        var stats = FormPage.Elastic(root, ServiceForms.Guild.StatsBox, new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x10)));
        for (int i = 0; i < 6; i++)
        {
            FormPage.Label(stats, RaceData.StatNamesLong[i][..3], FormPage.Across(ServiceForms.Guild.lblStatName0, i), DesignFonts.GuildStatName);
            FormPage.Label(stats, def.ReqStats[i].ToString(), FormPage.Across(ServiceForms.Guild.lblStat0, i), DesignFonts.GuildStat, align: 2);
        }
        // lblWelcomeInfo, centred (sub2962)
        FormPage.Label(root, greeting, ServiceForms.Guild.lblWelcomeInfo, bold: false, align: 2);
        FormPage.Label(root, "Guilds", ServiceForms.Guild.GuildsCaption, FormPage.Maroon);         // Label2(2)

        // sub2974: the guilds fn111E lets this race and alignment into, in table order
        var list = FormPage.List(root, ServiceForms.Guild.lstGuilds);
        var listed = GuildData.All.Where(g => g.AllowsRace(_ch.Race) && g.AllowsAlignment(_ch.Alignment)).ToList();
        foreach (var g in listed)
            list.Items.Add(GuildHall.ListRow(g.Name, _ch.Guilds.FirstOrDefault(m => m.Guild == g.Name)?.Level ?? 0));
        // sub2974 clears it, so nothing is selected until you pick

        // sub2962 always enables Make Level for the current guild (823); only Form_Load, Visit and the
        // end of a level then switch it off for want of experience or gold. After a Join or a
        // demotion it stays on however short you are, and the routine refuses you itself.
        bool canMake = isCurrent && (!_makeTested || (_ch.Guild.Experience >= _ch.ExpForNextLevel()
                       && CanPay(_ch.GoldForNextLevel())));
        // the buttons with a font of their own are 8.25 plain; Visit the Library and EXIT have the form's bold
        var make = FormPage.Access(FormPage.Button(root, "Make Level", ServiceForms.Guild.cmdMake, bold: false), 'M');   // "&Make Level"
        make.IsEnabled = canMake;
        var info = FormPage.Access(FormPage.Button(root, "Exp. Info", ServiceForms.Guild.cmdInfo, bold: false), 'I');   // "Exp. &Info"
        info.IsEnabled = isCurrent;
        var visit = FormPage.Access(FormPage.Button(root, "Visit", ServiceForms.Guild.cmdVisit, bold: false), 'V');   // "&Visit"
        var join = FormPage.Button(root, GuildHall.JoinCaption(member != null && member.Level > 0), ServiceForms.Guild.cmdJoin, bold: false);
        join.IsEnabled = !isCurrent && !refused;
        var log = FormPage.Access(FormPage.Button(root, "Read Guild Log", ServiceForms.Guild.cmdLog, bold: false), 'R');   // "&Read Guild Log"
        var library = FormPage.Access(FormPage.Button(root, "Visit the Library", ServiceForms.Guild.cmdLibrary), 'L');   // "Visit the &Library"
        var exit = FormPage.Button(root, "EXIT", ServiceForms.Guild.cmdExit);
        FormPage.Access(exit, 'X');   // "E&XIT": Alt+X while this form is the active one (ServiceAccessKey)

        visit.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;   // cmdVisit_Click: "If lstGuilds.ListIndex <> -1"
            _guildVisited = listed[list.SelectedIndex].Name;
            _makeTested = true;   // cmdVisit tests experience and gold (586-591)
            ShowGuild(null, null!);
        };
        join.Click += async (_, _) => await JoinVisitedGuild(def);
        make.Click += async (_, _) => await MakeLevel();
        info.Click += async (_, _) => await Tell(GuildHall.InfoTitle, GuildInfo());
        library.Click += (_, _) => ShowLibrary(null, null!);
        log.Click += (_, _) => ShowGuildLog(def.Name);   // the visited guild's log (FRMCTYG.BAS:174-177)
        exit.Click += (_, _) => { _guildVisited = ""; CloseService(null, null!); };
        // The form data makes cmdMake the Default button and cmdExit the Cancel button: while the
        // Guild is the active window, Enter presses Make Level (when it is enabled) and Escape
        // presses EXIT, wherever the focus is (PreviewGameKey hands them here first)
        Func<Avalonia.Input.Key, bool> keys = k =>
        {
            var pressed = k == Avalonia.Input.Key.Escape ? exit
                        : k is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return && make.IsEnabled ? make : null;
            pressed?.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            return pressed != null;
        };
        _serviceKeys = keys;
        var keysIn = _svc;
        form.Page.DetachedFromVisualTree += (_, _) => { if (keysIn.Keys == keys) keysIn.Keys = null; };

        var page = form.Page;
        // frmCityGuild!lblCall = "UPDATE", which mdrmulSwitch sets when the party switches character
        // with the Guild open (MULTIMOD.BAS:636): lblCall_Change (FRMCTYG.BAS:671-686) puts m001A
        // back on the new character's current guild, redraws through sub294F, sub2974 and sub2962,
        // and tests Make Level against experience (fn12A1) and gold (fn12B2) - as Form_Load does.
        // It redraws in place: the window is not opened or placed again
        Action update = () =>
        {
            _guildVisited = _ch.Guild.Guild;
            _makeTested = true;
            RedrawGuildInPlace();
        };
        _guildUpdate = update;
        page.DetachedFromVisualTree += (_, _) => { if (_guildUpdate == update) _guildUpdate = null; };
        ShowForm(form);

        // smoke tests only: press Make Level for us once, since the button is not keyboard-reachable
        if (Environment.GetEnvironmentVariable("MORDOR_TEST_PAGE") == "levelup" && make.IsEnabled && !_autoLevelled)
        {
            _autoLevelled = true;
            make.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        }
    }

    /// <summary>
    /// cmdJoin_Click (FRMCTYG.BAS:84-168). For a guild not yet joined: the warning when your best
    /// guild level is under 10, then the question - priced when you already belong to a guild other
    /// than the current one, plain otherwise - in a Yes/No box, JOIN GUILD. Then, joining or
    /// re-acquainting alike, worn cursed gear refuses it; the fee is taken; and the visited guild
    /// becomes the current one - at level 1, with a line in its log, if it is new.
    /// </summary>
    private async System.Threading.Tasks.Task JoinVisitedGuild(GuildDef def)
    {
        bool joined = _ch.Guilds.Any(m => m.Guild == def.Name && m.Level > 0);
        bool others = _ch.Guilds.Any(m => m.Guild != _ch.Guild.Guild && m.Level > 0);
        long cost = others ? Rules.GuildJoinCost(def) : 0;
        if (!joined)
        {
            string ask = (_ch.TopGuildLevel < 10 ? GuildHall.Warning : "") + GuildHall.JoinQuestion(def.Name, others, cost);
            if (!await Confirm(GuildHall.JoinTitle, ask)) return;
        }
        if (_ch.Inventory.Any(sl => sl.Equipped && sl.SlotCursed))
        {
            await Tell("Join Guild", "Your Aura is Tainted with one or more cursed items!");
            return;
        }
        if (others && !joined && !CharPay(cost))
        {
            await Tell("Join Guild", "Not enough gold!");
            return;
        }
        if (joined) _ch.SetActiveGuild(def.Name);
        else
        {
            _ch.Guilds.Insert(0, new GuildMembership { Guild = def.Name, Level = 1, Experience = 0 });
            _ch.RecalcSpellPoints(refill: true);
            GuildLog.Append(SavesDir, GuildData.IndexOf(def.Name), GuildLog.Wrote.JoinedGuild(_ch.Name));
        }
        if (GearCheck.Any(_ch, Lookup)) Log(GearCheck.UnequippedOnJoining);
        _makeTested = false;   // cmdJoin redraws through sub2962 alone
        // "frmCharStat3!cmbClass.Clear" then sub1658 (FRMCTYG.BAS:163-164): the first class again
        SpellPage.Clear(ClassBox);
        Save(); RefreshSummary(); ShowGuild(null, null!);
    }

    /// <summary>
    /// cmdMake_Click (FRMCTYG.BAS:179-578), in its order: the cap on the message line; experience and
    /// gold refused in a "Make Level" box with the Exp. Info box behind; the quest - done, handed
    /// over, or refused in a "MAKE LEVEL" box (with the forfeit offered to the pinned); then the level.
    /// After it the Messages list gets the new title and the spells learned; the box says
    /// "Congratulations! You are now Level N!"; the pack is read again; the guild may change hands;
    /// and the Questmaster goes through his books. Nothing is said about hits, which change silently,
    /// and no stat rises.
    /// </summary>
    /// <summary>
    /// The gate FRMCTYG.BAS:309-350 runs over every spell record, in record order, before and after
    /// the level: some guild joined whose rate for the spell's class is above nought, whose ceiling
    /// reaches the spell's level, and at which <c>Level * rate &lt;= guildLevel / 2 + .5</c>.
    /// </summary>
    private bool[] SpellsWithinReach()
    {
        var reach = new bool[_spells.Count];
        for (int i = 0; i < _spells.Count; i++)
        {
            var sp = _spells[i];
            foreach (var m in _ch.Guilds)
            {
                if (m.Level <= 0) continue;
                var g = GuildData.ByName(m.Guild);
                if (sp.Category < 0 || sp.Category >= g.SpellRate.Length) continue;
                double rate = g.SpellRate[sp.Category];
                if (rate <= 0 || sp.Level > g.SpellMax[sp.Category]) continue;
                if (sp.Level * rate <= m.Level / 2.0 + 0.5) { reach[i] = true; break; }
            }
        }
        return reach;
    }

    private async System.Threading.Tasks.Task MakeLevel()
    {
        if (_ch.Guild.Level >= _ch.ActiveGuildDef.MaxLevel) { Log(GuildHall.NothingLeftToTeach); return; }
        if (!_ch.CanLevelUp())
        {
            await Tell(GuildHall.MakeLevelTitle, GuildHall.NotEnoughExperience);
            await Tell(GuildHall.InfoTitle, GuildInfo());
            return;
        }
        // an item quest's item is handed over here, before the gold is asked for, so a character
        // turned away for want of gold has still given it up (FRMCTYG.BAS:257-266, 298-303)
        if (!_ch.QuestSatisfied(consume: true))
        {
            bool monster = _ch.Guild.QuestType == 1;
            string? named = monster ? _monsters.FirstOrDefault(m => m.Id == _ch.Guild.QuestTarget)?.Name
                                    : Lookup(_ch.Guild.QuestTarget)?.Name;
            // a failed lookup adds no line at all (236-241, 268-274)
            string what = named == null ? "" : (monster ? "You must have defeated one " : "You must return with one ") + named + ".";
            if (_ch.IsPinned) OfferForfeit(what, monster);
            else await Tell("MAKE LEVEL", "Quest has not been completed!" + (what != "" ? "\n" + what : ""));
            return;
        }
        long fee = _ch.GoldForNextLevel();
        if (!CanPay(fee))
        {
            await Tell(GuildHall.MakeLevelTitle, GuildHall.NotEnoughGold);
            await Tell(GuildHall.InfoTitle, GuildInfo());
            return;
        }
        CharPay(fee);
        string titleBefore = _ch.GuildTitle;
        var knewBefore = SpellsWithinReach();
        _ch.LevelUp(_rng);
        int guildIndex = GuildData.IndexOf(_ch.Guild.Guild);
        if (_ch.GuildTitle != titleBefore)
        {
            // sub085B at FRMCTYG.BAS:326 (fn13C2 says "You were" alone), and the log line
            string who = _party.Size == 1 ? "You were" : $"\"{_ch.Name.TrimEnd()}\" was";
            Log($"{_ch.Guild.Guild.ToUpperInvariant()}'S GUILD: {who} appointed the title of \"{_ch.GuildTitle}\" for reaching Level {_ch.Guild.Level}.");
            GuildLog.Append(SavesDir, guildIndex, GuildLog.Wrote.Titled(_ch.Name, _ch.GuildTitle, _ch.Guild.Level));
        }
        var nowKnown = SpellsWithinReach();
        var learned = Enumerable.Range(0, _spells.Count).Where(i => nowKnown[i] && !knewBefore[i]).ToList();
        if (learned.Count > 0) Log("Your spell knowledge has increased!");
        foreach (int i in learned)
            Log($"Of the {Librarian.SpellSchools.ElementAtOrDefault(_spells[i].Category) ?? ""} class, you learned \"{_spells[i].Name.TrimEnd()}\"");
        Audio.PlayOnNextChannel("MAKELEV");
        await Tell("MAKE LEVEL", GuildHall.MadeLevel(_ch.Guild.Level));
        foreach (string line in RereadPack()) Log(line);
        await ClaimMastership(guildIndex);
        foreach (string line in OfferSkillRecords()) Log(line);   // FRMCTYG.BAS:454-456
        foreach (string line in _ch.RollQuestForLevel(_monsters, _items, _rng)) Log(line);
        if (_ch.Guild.QuestType > 0)
            GuildLog.Append(SavesDir, guildIndex, _ch.Guild.QuestType == 1
                ? GuildLog.Wrote.QuestForMonster(_ch.Name, _monsters.FirstOrDefault(m => m.Id == _ch.Guild.QuestTarget)?.Name ?? "monster")
                : GuildLog.Wrote.QuestForItem(_ch.Name, Lookup(_ch.Guild.QuestTarget)?.Name ?? "item"));
        _makeTested = true;   // the end of a level tests them again (418-423)
        RefreshSummary(); Save(); ShowGuild(null, null!);
    }

    /// <summary>
    /// The original's <c>fn170D(..., "PASSWORD")</c> - a one-line box that shows asterisks. It
    /// returns null when the player backs out, which the original signals by returning "-1".
    /// </summary>
    /// <summary>A one-line question, the original's <c>fn170D</c>. Null when it is cancelled.</summary>
    /// <summary>
    /// <c>s</c> - Swap Items. The rule is <see cref="SwapCommand"/>'s, so the city refuses exactly
    /// where the dungeon refuses: silently, on anything without a comma, anything over five
    /// characters, slot 1, and anything past the fortieth.
    /// </summary>
    private async void SwapItems()
    {
        string? typed = await AskFor("Swap Items", "Enter the Item Slots to Swap\n(i.e '12,17')");
        if (typed == null) return;
        var slots = SwapCommand.Parse(typed);
        if (slots == null) return;
        if (!_ch.SwapSlots(slots.Value.A, slots.Value.B, buffersFollow: false)) return;   // no M59B0 remap (MAINMOD2.BAS:3432-3442)
        Save();
        RefreshPack();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch));
        Log(SwapCommand.Done);
    }

    /// <summary>
    /// 'g' Give Item/Gold, <c>fn1521</c> case 71 (MAINMOD2.BAS:3705-3730): "Character's Name?",
    /// "How much Gold?" and "Which Item #?", then sub14B5 - gold and an item may go in the one
    /// command. See <see cref="GiveFlow"/>; the dungeon asks the same.
    /// </summary>
    private async void AskToGive()
    {
        bool gave = await GiveFlow.Run(this, _party.Members, _ch, Lookup, FiledFor, Log, Tell);
        if (!gave) return;
        Save(); RefreshPack(); RefreshSummary();
    }

    /// <summary>
    /// sub14B5's refusals: the guild-issued and equipped ones are sub085B lines, the full pack is
    /// <c>MsgBox(..., mc02EC, "SORRY!")</c> (MAINMOD2.BAS:2953, mc02EC = 64).
    /// </summary>
    private void GiveRefused(Character to, string no)
    {
        if (no == Party.NoFreeSlots(to)) _ = Tell("SORRY!", no);
        else Log(no);
    }

    /// <summary>
    /// sub14B5's <c>sub0730 p049A, Ref, IdLevel, -1, False</c> (MAINMOD2.BAS:2962, 2994): the
    /// Library files the given item under the receiver, graded as sub0730 grades it.
    /// </summary>
    private Action<int, IdLevel> FiledFor(Character to) =>
        (id, level) => Librarian.FileItem(SavesDir, to, id, level);

    /// <summary>
    /// <c>p</c> Pickup Character, fn1521 case 80 without the shift (MAINMOD2.BAS:3654-3683). The
    /// same command as the dungeon's, and in town "here" means the city: frmInteract compares the
    /// level and the area, and everybody above ground has the same pair (0 and -1).
    ///
    /// Picking somebody up while already carrying somebody puts the first one down first and says
    /// so - it does not refuse.
    /// </summary>
    private async void PickUpBody()
    {
        // fn1521 guards this with "DeathState = False And Ailments(mc01EC) = 0" (MAINMOD2.BAS:3655),
        // silently. The city gate has already zeroed the paralysis (FRMCTYM.BAS:115), but this is
        // the same routine, so the test is kept here as in the dungeon.
        if (_ch.IsDead || _ch.Paralysed) return;
        var here = BodiesInTheCity();
        int pick = await NamePicker.Ask(this, "Pickup Character", "Character's Name?",
                                        here.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("PICKUP"));
        if (pick < 0) return;
        var who = here[pick];

        // the Hide test, before anything already carried is put down (MAINMOD2.BAS:3663-3668)
        if (who.Options.Hide) { Log(Interact.Hidden(who.Name)); return; }

        string? dropped = PutDownWhatIsCarried();
        if (dropped != null) Log(dropped);                 // sub085B, the Messages window, both lines
        _ch.CarryingCharacter = who.Name;
        _ch.CarryingCompanion = -1;
        Save();
        Log(Interact.NowCarrying(_ch, who.Name));
        RefreshCompanions();
    }

    /// <summary>
    /// frmInteract's PICKUP list above ground (FRMINTER.BAS:339-387): the dead at level 0, not
    /// yourself and not anyone a party member already carries. Hidden characters are listed.
    /// </summary>
    private List<Character> BodiesInTheCity() =>
        Character.LoadAllFrom(SavesDir)
            .Where(c => c.IsDead && c.Name != _ch.Name && c.DungeonLevel == 0
                        && _party.Members.All(m => m.CarryingCharacter != c.Name))
            .ToList();

    /// <summary>
    /// The morgue's Grab Body (FRMMORG.BAS:58-64, bm10.png): the same question, and then only
    /// <c>M5943 = idx + 4</c>. No Hide test, nothing already carried put down, and nothing said.
    /// </summary>
    private async System.Threading.Tasks.Task GrabBody()
    {
        var here = BodiesInTheCity();
        int pick = await NamePicker.Ask(this, "Pickup Character", "Character's Name?",
                                        here.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("PICKUP"));
        if (pick < 0) return;
        _ch.CarryingCharacter = here[pick].Name;
        _ch.CarryingCompanion = -1;
        Save();
        RefreshCompanions();
    }

    /// <summary>
    /// <c>l</c> Drop Body, fn1521 case 76 (MAINMOD2.BAS:3684-3704). Carrying nothing says nothing:
    /// the whole body of the case is inside <c>If M5943 &lt;&gt; -1</c>.
    /// </summary>
    private void DropBody()
    {
        if (_ch.Paralysed) return;   // "If Chars(Me).Ailments(mc01EC) = 0" in fn1521 (MAINMOD2.BAS:3690)
        if (PutDownWhatIsCarried() is not string said) return;
        Save();
        if (said != "") Log(said);
        RefreshCompanions();
    }

    /// <summary>Puts down whoever is carried and returns the line, or null when there was nobody.</summary>
    private string? PutDownWhatIsCarried()
    {
        if (_ch.CarryingCharacter != "")
        {
            string said = Interact.NoLongerCarrying(_ch, _ch.CarryingCharacter);
            _ch.CarryingCharacter = "";
            return said;
        }
        if (_ch.CarryingCompanion >= 0 && _ch.CarryingCompanion < CompanionSlots.Slots)
        {
            // "Companions(M5943).Ref = -1" (MAINMOD2.BAS:3695, and 3670 before a pickup): the
            // companion put down is let go. The line reads the name before the slot is emptied
            string said = Interact.NoLongerCarryingCompanion(_ch, _ch.CompanionName(_ch.CarryingCompanion));
            _ch.Companions.Release(_ch.CarryingCompanion);
            _ch.CarryingCompanion = -1;
            return said;
        }
        _ch.CarryingCompanion = -1;
        return null;
    }

    /// <summary>
    /// The list the port shows where the original shows a box you type a name into. Same shape as
    /// the dungeon's.
    /// </summary>
    private async System.Threading.Tasks.Task<int> AskToPick(string title, string prompt, IReadOnlyList<string> options)
    {
        int chosen = -1;
        var list = new ListBox { Height = 180, ItemsSource = options.ToList() };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // in frmInteract's font, the form it stands for, through the Options fonts (UiFonts)
        var win = UiFonts.Set(new Window
        {
            Title = title,
            Width = 420, Height = 300,
            Background = Avalonia.Media.Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = prompt, Foreground = Brushes.LightGray },
                        list,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
                    },
                },
            },
        }, DesignFonts.Interact, "lblQuestion");
        ok.Click += (_, _) => { chosen = list.SelectedIndex; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => { if (options.Count > 0) list.SelectedIndex = 0; list.Focus(); };

        await OriginalBox.ShowOver(win, this);
        return chosen;
    }

    private System.Threading.Tasks.Task<string?> AskFor(string title, string prompt) =>
        AskLine(title, prompt, secret: false);

    private System.Threading.Tasks.Task<string?> AskPassword(string title, string prompt) =>
        AskLine(title, prompt, secret: true);

    private async System.Threading.Tasks.Task<string?> AskLine(string title, string prompt, bool secret)
    {
        string? answer = null;
        var box = new TextBox { PasswordChar = secret ? '*' : ' ', Width = 240 };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        // fn170D's frmInteract, in its font through the Options fonts (UiFonts)
        var win = UiFonts.Set(new Window
        {
            Title = title,
            Width = 380, SizeToContent = SizeToContent.Height,
            Background = Avalonia.Media.Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = prompt, Foreground = Brushes.LightGray },
                        box,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
                    },
                },
            },
        }, DesignFonts.Interact, "lblQuestion");
        ok.Click += (_, _) => { answer = box.Text ?? ""; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => box.Focus();
        await OriginalBox.ShowOver(win, this);
        return answer;
    }


    /// <summary>A Yes/No box, the original's <c>MsgBox ..., vbYesNo</c>. True means Yes.</summary>
    private async System.Threading.Tasks.Task<bool> Confirm(string title, string prompt)
    {
        bool yes = false;
        var yesBtn = new Button { Content = "Yes", IsDefault = true };
        var noBtn = new Button { Content = "No", IsCancel = true };
        var win = new Window
        {
            Title = title,
            Width = 460, SizeToContent = SizeToContent.Height,
            Background = Avalonia.Media.Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = prompt, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { yesBtn, noBtn } },
                    },
                },
            },
        };
        yesBtn.Click += (_, _) => { yes = true; win.Close(); };
        noBtn.Click += (_, _) => win.Close();
        win.Opened += (_, _) => yesBtn.Focus();   // so Enter and Escape answer it without a mouse
        UiFonts.MessageBox(win);
        await OriginalBox.ShowOver(win, this);
        return yes;
    }

    /// <summary>A box with one OK button, the original's plain <c>MsgBox text, 64, title</c>.</summary>
    private async System.Threading.Tasks.Task Tell(string title, string text)
    {
        var okBtn = new Button { Content = "OK", IsDefault = true, IsCancel = true };
        var win = new Window
        {
            Title = title,
            Width = 420, SizeToContent = SizeToContent.Height,
            Background = Avalonia.Media.Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = text, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
                        okBtn,
                    },
                },
            },
        };
        okBtn.Click += (_, _) => win.Close();
        win.Opened += (_, _) => okBtn.Focus();
        UiFonts.MessageBox(win);
        await OriginalBox.ShowOver(win, this);
    }

    // ---------------- Hall of Records ----------------

    /// <summary>
    /// Offer a batch of values to the Hall and collect whatever it announces. The original checks
    /// different records at different moments - six stats at creation (and, in the original, when an item raises one), the
    /// three skills after a level, gold at the bank, experience on reaching the city - so each
    /// caller passes only what it has just changed.
    /// </summary>
    private string OfferRecords(params (HallRecords.Kind kind, VbCurrency value)[] offers)
    {
        var said = OfferRecordLines(offers);
        return said.Count == 0 ? "" : "  " + string.Join("  ", said);
    }

    /// <summary>The records taken, one line each - each is its own sub085B (HALL.BAS:511-513).</summary>
    private List<string> OfferRecordLines(params (HallRecords.Kind kind, VbCurrency value)[] offers)
    {
        var hall = HallRecords.Load(SavesDir);
        var said = new List<string>();
        foreach (var (kind, value) in offers)
            if (hall.CheckAndLog(kind, _ch, value, SavesDir, null, _party.Size == 1) is string line) said.Add(line);
        if (offers.Length > 0) hall.Save(SavesDir);
        return said;
    }

    /// <summary>The three skill records FRMCTYG.BAS:454-456 checks the moment you make a level.</summary>
    private List<string> OfferSkillRecords() => OfferRecordLines(
        (HallRecords.Kind.Fighting, HallRecords.FightingSkill(_ch, Lookup)),
        (HallRecords.Kind.Magic, HallRecords.MagicSkill(_ch, _spells)),
        (HallRecords.Kind.Thieving, HallRecords.ThievingSkill(_ch, Lookup)));

    // ---------------- Guildmasters ----------------

    /// <summary>
    /// FRMCTYG.BAS:434, run straight after a level: if this character now out-ranks whoever the
    /// guild had on record, the guild is theirs, and the crest comes with it.
    /// </summary>
    private async System.Threading.Tasks.Task ClaimMastership(int guildIndex)
    {
        var table = Guildmasters.LoadSeeded(SavesDir);
        var (qualified, changedHands) = Guildmasters.Claim(table, guildIndex, _ch);
        if (!qualified) return;
        table.Save(SavesDir);

        if (changedHands)
        {
            // FRMCTYG.BAS:440-443: a beep and a box headed GuildMaster
            await Tell("GuildMaster", "Congratulations!\nYou are now the new Guildmaster!");
            GuildLog.Append(SavesDir, guildIndex, GuildLog.Wrote.BecameGuildmaster(_ch.Name));
        }
        // modAwardCrest (FRMCTYG.BAS:715-752): the crest in a box headed "Congratulations!", or
        // "Sorry!" when there is no room for it
        var (award, crestMsg) = Guildmasters.AwardCrest(_ch, guildIndex, _items);
        if (award is Guildmasters.Award.Given or Guildmasters.Award.NoRoom && Guildmasters.CrestFor(_items, guildIndex) is Item crest)
            GuildLog.Append(SavesDir, guildIndex, GuildLog.Wrote.GuildmasterAward(_ch.Name, crest.Name));
        if (crestMsg != null) await Tell(award == Guildmasters.Award.NoRoom ? "Sorry!" : "Congratulations!", crestMsg);
    }

    /// <summary>
    /// FRMCTYG.BAS:422-432 - a level makes you sharper, so the whole pack is read again and anything
    /// you now make more of is announced by slot number, a Messages line each.
    /// </summary>
    private List<string> RereadPack()
    {
        var said = new List<string>();
        // "Item #N" is the pack slot, as it is everywhere else the game names one: FRMCTYG.BAS:428
        // counts the pack array, not the things in it
        for (int i = 1; i < Character.PackSize; i++)
        {
            if (_ch.Pack[i] is not InventorySlot slot || slot.ItemId == 0) continue;
            if (Lookup(slot.ItemId) is not Item it) continue;
            var before = slot.Known;
            Loot.OnReceived(_ch, slot, it, Lookup);
            if (slot.Known != before)
            {
                said.Add($"With your increased Perception, you have identified Item #{i + 1} as: "
                       + Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges));
                // sub0730 Me, Ref, IdLevel, -1, False (FRMCTYG.BAS:434): filed at the level now read
                Librarian.FileItem(SavesDir, _ch, it.Id, slot.Known);
            }
        }
        return said;
    }

    /// <summary>
    /// FRMCTYG.BAS:271-296. A character who is pinned - all the experience this level can hold,
    /// and a quest they cannot finish - is offered the way out, twice: once to file for forfeit,
    /// and again on the exact level it would drop them to. It costs Int(Log(level + 1)) levels
    /// and cannot be undone.
    /// </summary>
    private async void OfferForfeit(string what, bool monsterQuest)
    {
        int cost = _ch.ForfeitCost();
        int to = Math.Max(1, _ch.Guild.Level - cost);

        // two line breaks before the offer on a monster quest (FRMCTYG.BAS:243), one on an item quest (276)
        if (!await Confirm("Make Level",
                "Quest has not been completed!" + (what != "" ? "\n" + what : "")
                + (monsterQuest ? "\n\n" : "\n") + "Since you are pinned and have not yet completed your quest, you are given the "
                + "opportunity of filing for forfeit.  Do you wish to do so?"))
            return;

        if (!await Confirm("forfeit Quest",
                $"If you forfeit this quest, you will be reduced to level {to}.  This means that you will "
                + "lose any attack, defense, abilities and other guild bonuses given to you within the levels "
                + $"you have made since you were level {to}.\n\nOnce you forfeit, it is done.\n\n"
                + "Do you wish to forfeit your quest?"))
            return;

        int guildIndex = GuildData.IndexOf(_ch.Guild.Guild);
        // sub29BD checks for cursed gear only after both confirmations
        if (_ch.ForfeitRefusal(Lookup) is string tainted) { await Tell("forfeit Quest", tainted); return; }
        var off = _ch.ForfeitQuest(Lookup, _rng);
        GuildLog.Append(SavesDir, guildIndex, GuildLog.Wrote.Demoted(_ch.Name, cost, _ch.Sex));
        _makeTested = false;   // sub29BD redraws through sub2962 alone (956)
        RefreshSummary(); Save(); ShowGuild(null, null!);
        // sub29BD ends on a box headed "Demotion Complete" (FRMCTYG.BAS:963)
        await Tell("Demotion Complete", $"You have been demoted to level {_ch.Guild.Level}."
                   + (off.Count > 0 ? "\n\nOne or more items were unequipped when you were demoted..." : ""));
    }

    // ---------------- Guild log ----------------

    /// <summary>
    /// frmGuildLog (FRMGLDL.BAS): the guild's own record of what its members have done, newest
    /// first, dated in days ago. The Clean button only appears once the whole log has grown past
    /// fifty entries a guild, and then it keeps the newest fifty of all of them.
    /// </summary>
    private void ShowGuildLog(string guildName)
    {
        // over the Guild, modally (FRMCTYG.BAS:174-177): closing the log returns to it
        if (_serviceWhich == GameWindow.Guild) _serviceReturn = () => ShowGuild(null, null!);
        // Form_Resize captions it "Log of the " + the guild's name (FRMGLDL.BAS:40)
        OpenService("Log of the " + guildName);
        DrawGuildLog(guildName);
    }

    /// <summary>
    /// frmGuildLog's form data (MORDOR.EXE RCDATA 48): a 7755x2490-twip client, every control in
    /// its one Elastic at -15,-15 (ServiceForms.GuildLog has the rectangles). lstLog is MS Sans
    /// Serif 8.25 plain; lblEntries, Clean and Done have the form's 8.25 bold. Drawn again in place
    /// after a clean, as sub316B clears lstLog and fills it again (FRMGLDL.BAS:116-117).
    /// </summary>
    private void DrawGuildLog(string guildName)
    {
        int guild = GuildData.IndexOf(guildName);
        long today = GuildLog.Today();
        var entries = GuildLog.For(SavesDir, guild);

        // sized, never restyled, by the Options fonts: its Form_Load runs sub0802 alone (FRMGLDL.BAS:30)
        var form = new FormPage(ServiceForms.GuildLog.ClientW, ServiceForms.GuildLog.ClientH, ServiceForms.GuildLog.Elastic, FontPass.SizeOnly);
        var root = form.Root;
        // sub318B (FRMGLDL.BAS:131-187): each of this guild's entries put at the top, "No Entries" for none
        var list = FormPage.List(root, ServiceForms.GuildLog.lstLog);
        list.ItemsSource = entries.Count == 0 ? new List<string> { "No Entries" }
                                              : entries.Select(e => GuildLog.Line(e, today)).ToList();
        FormPage.Label(root, $"{entries.Count} Entries", ServiceForms.GuildLog.lblEntries);
        // "&Clean Guild Log" is Visible False in the form; Form_Resize shows it once the whole log
        // has grown past fifty entries a guild (FRMGLDL.BAS:44). Its Alt+C is the log's own while the
        // log is the active window (ServiceAccessKey); the town's Confinement waits for the town
        var clean = FormPage.Access(FormPage.Button(root, "Clean Guild Log", ServiceForms.GuildLog.cmdClean), 'C');   // "&Clean Guild Log"
        clean.IsVisible = GuildLog.NeedsCleaning(SavesDir, GuildData.All.Count);
        // "&Done": Alt+D. It is neither Default nor Cancel in the form data, so Enter and Escape do nothing
        var done = FormPage.Button(root, "Done", ServiceForms.GuildLog.cmdDone);
        FormPage.Access(done, 'D');   // "&Done": Alt+D while the log is the active form (ServiceAccessKey)

        clean.Click += async (_, _) =>
        {
            if (!await Confirm("Clean Log File",
                    $"Cleaning the Guild Log will remove all but {GuildLog.KeepOnClean} entries from the entire guild log "
                    + "with the option of writing old entries to a text file.  Do you wish to Proceed?"))
                return;
            bool toText = await Confirm("Clean Guild Log",
                    $"Do you want the erased log entries to be written to {GuildLog.TextName}?");
            // the clean says nothing beyond its two boxes (FRMGLDL.BAS:19-126)
            GuildLog.Clean(SavesDir, _ch.Name, i => GuildData.All[Math.Clamp(i, 0, GuildData.All.Count - 1)].Name, toText);
            DrawGuildLog(guildName);
        };
        // cmdDone_Click: Unload Me (FRMGLDL.BAS:24-26) - back to the Guild it was opened over
        done.Click += (_, _) => CloseService(null, null!);
        ShowForm(form);
    }

    // ---------------- Store ----------------

    private static Control Titled(string title, Control body, Control footer, int col)
    {
        var sp = new DockPanel();
        var head = new TextBlock { Text = title, FontWeight = FontWeight.Bold, Margin = new Avalonia.Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(head, Dock.Top); sp.Children.Add(head);
        DockPanel.SetDock(footer, Dock.Bottom); footer.Margin = new Avalonia.Thickness(0, 6, 0, 0); sp.Children.Add(footer);
        sp.Children.Add(body);
        Grid.SetColumn(sp, col);
        return sp;
    }

    // ---------------- Bank ----------------

    /// <summary>Redraws the open Bank, <c>frmCityBank!lblCall = "UPDATE"</c>; null when it is shut.</summary>
    private Action? _bankRefresh;

    /// <summary>The open Bank's sub2884, <c>frmCityBank!lblCall = "GOLD"</c>; null when it is shut.</summary>
    private Action? _bankGold;

    // DEVIATION: the port's own Bank controls (twips, as ServiceForms.Bank's), in space the form
    // leaves empty. Withdraw, in elaElastic(3): right of cmdInfo (45,1920,1530,300) by the same 45
    // twips Info stands in from the box, as tall, out to 60 twips short of the box's right side as
    // lstItems is. Deposit, in elaElastic(0): under elaElastic(3), its left lined up with Info's (2850+45), at
    // cmdExit's top and height, and 1485 wide - 45 narrower than Info's 1530, so it ends 45 twips short
    // of Exit (4425) as Info stands 45 in from its box. The mods' Bank commands, right-
    // aligned on lblLabel(5)'s line, from the end of its box to elaElastic(3)'s right side.
    private static readonly FormRect BankWithdrawButton = new(1620, 1920, 1395, 300);
    private static readonly FormRect BankDepositButton = new(2895, 3585, 1485, 330);
    private static readonly FormRect BankModCommands = new(4950, 975, 975, 240);

    /// <summary>
    /// <c>mdrhalCheckRecord mc00D4, gv1A84, Chars(Me).BankGold</c>, the Wealthiest record that
    /// sub2884 looks at every time the Bank's status line is drawn (FRMCITYB.BAS:284).
    /// </summary>
    private void CheckWealthiest()
    {
        foreach (string line in OfferRecordLines((HallRecords.Kind.Wealthiest, _ch.GoldInBank))) Log(line);
    }

    /// <summary>
    /// The Bank, <c>FRMCITYB.BAS</c>, drawn to its form data as game_files/bm7.png shows it: "Welcome to the Bank!", the
    /// Account Status line, a Deposit box and a Withdraw box each with an "All" button, the three
    /// Party Options, and the "Deposit/Withdraw Items" list with Info and Exit.
    ///
    /// Items go in by dragging a pack line from the character's Items window onto the list, and come
    /// out by dragging a line of the list onto a pack slot there (FRMCHAR3.BAS:530-561) - the
    /// original has no button for either, and no pack list on this form.
    ///
    /// DEVIATION (asked for by the user): a Withdraw button beside Info and a Deposit button left of
    /// Exit, where the form has empty space; a double-click on a line withdraws it; the list takes
    /// Shift/Ctrl multi-selection for Withdraw. Deposit takes the line selected on Objects &amp;
    /// Spells' Items page. Both go through the drag paths' own code (<see cref="TakeFromVault"/>
    /// and the list's drop), so every rule is the original's; a withdrawal goes to the first free
    /// pack slot, and a full pack is the vault branch's "has no free slots!" box (MAINMOD2.BAS:2985).
    /// </summary>
    private void ShowBank(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180): back to the dead screen
        if (DeadScreenOwed) { ShowDead(); return; }
        // a picture clicked for one already up brings it forward and builds nothing again (imgPicture_Click, FRMCTYM.BAS:192-203)
        if (s != null && BringUpOpenService(GameWindow.Bank)) return;
        OpenService("Bank", GameWindow.Bank);
        // frmCityBank's form data (MORDOR.EXE RCDATA 18): a 6195x3945-twip client, every control
        // in elaElastic(0) at 0,-15, three more Elastics inside it (ServiceForms.Bank has the rectangles)
        var form = new FormPage(ServiceForms.Bank.ClientW, ServiceForms.Bank.ClientH, ServiceForms.Bank.Elastic);
        var root = form.Root;
        // lblLabel(0..5): the welcome, 12 points bold and blue, and five navy captions in the form's bold
        FormPage.Label(root, "Welcome to the Bank!", ServiceForms.Bank.Welcome, DesignFonts.Welcome, FormPage.Blue);
        FormPage.Label(root, "Account Status", ServiceForms.Bank.AccountCaption, FormPage.Navy);
        FormPage.Label(root, "Deposit", ServiceForms.Bank.DepositCaption, FormPage.Navy);
        FormPage.Label(root, "Withdraw", ServiceForms.Bank.WithdrawCaption, FormPage.Navy);
        FormPage.Label(root, "Party Options", ServiceForms.Bank.PartyCaption, FormPage.Navy);
        FormPage.Label(root, "Deposit/Withdraw Items", ServiceForms.Bank.ItemsCaption, FormPage.Navy);
        // lblStatus, one line in elaElastic(1), maroon in the form's bold; sub2884 sets its caption
        // (FRMCITYB.BAS:278-294), and VB cuts what runs past the label's box
        var statusBox = FormPage.Elastic(root, ServiceForms.Bank.StatusBox);
        var status = FormPage.Label(statusBox, "", ServiceForms.Bank.lblStatus, FormPage.Maroon);
        var depositBox = FormPage.Text(root, ServiceForms.Bank.DepositAmount);
        var withdrawBox = FormPage.Text(root, ServiceForms.Bank.WithdrawAmount);
        // elaElastic(3): lstItems, in the form's bold, and Info under it
        var itemsBox = FormPage.Elastic(root, ServiceForms.Bank.ItemsBox);
        var vault = FormPage.List(itemsBox, ServiceForms.Bank.lstItems, bold: true);
        // DEVIATION: Shift/Ctrl pick several lines for Withdraw (lstItems is a single-select list)
        vault.SelectionMode = SelectionMode.Multiple;
        var rowSlots = new List<int>();   // ItemData: the vault slot behind each row
        var info = FormPage.Access(FormPage.Button(itemsBox, "Info", ServiceForms.Bank.cmdInfo, bold: false), 'I');   // "&Info"
        info.IsEnabled = false;
        // DEVIATION: the port's two buttons, in the form's empty space - Withdraw beside Info in
        // elaElastic(3), as tall as Info and out to the list's right edge; Deposit under the list's
        // box, left of Exit, its left lined up with Info's, as tall as Exit and ending 45 twips short of it
        var withdraw = FormPage.Button(itemsBox, "Withdraw", BankWithdrawButton, bold: false);
        withdraw.IsEnabled = false;
        ToolTip.SetTip(withdraw, "Take the selected items out of the Bank, each into the first free pack slot (Shift or Ctrl selects several; a double-click takes one)");
        var deposit = FormPage.Button(root, "Deposit", BankDepositButton, bold: false);
        ToolTip.SetTip(deposit, "Put the item selected on the Items page of Objects & Spells into the Bank");
        // elaElastic(2), the Party Options: cmdCommand(2..4)
        var partyBox = FormPage.Elastic(root, ServiceForms.Bank.PartyBox);
        var pool = FormPage.Access(FormPage.Button(partyBox, "Pool & Deposit", ServiceForms.Bank.PoolDeposit, bold: false), 'o');   // "P&ool && Deposit"
        var partyDep = FormPage.Access(FormPage.Button(partyBox, "Party Deposit", ServiceForms.Bank.PartyDeposit, bold: false), 'P');   // "&Party Deposit"
        var poolAll = FormPage.Access(FormPage.Button(partyBox, "Party Pool & Deposit", ServiceForms.Bank.PartyPoolDeposit, bold: false), 'D');   // "Party Pool && &Deposit"

        void DrawVault()
        {
            // sub28A7: the slots that hold something, in slot order, by name as you know them -
            // no numbers, and no line for an empty vault
            int keep = vault.SelectedIndex;
            rowSlots.Clear();
            var rows = new List<object>();
            for (int i = 0; i < _ch.Bank.Count; i++)
            {
                var sl = _ch.Bank[i];
                if (sl.ItemId <= 0 || Lookup(sl.ItemId) is not Item it) continue;
                rowSlots.Add(i);
                // with a mod, something after the name (ctx.UI.BankRowDecoration); with none, the text alone
                rows.Add(ModSlots.BankLine(Loot.NameFor(it, sl.Known, sl.Alignment, sl.Charges), _ch, it, sl, i));
            }
            vault.ItemsSource = rows;
            // sub28A7 (239-251): the remembered row, pulled back to the last one if the list got shorter;
            // Info is greyed only for an empty list
            if (rows.Count > 0 && keep >= 0) vault.SelectedIndex = Math.Min(keep, rows.Count - 1);
            info.IsEnabled = withdraw.IsEnabled = rows.Count > 0;
        }

        // sub2884: the status line, the Wealthiest record looked at every time, and the party
        // buttons greyed for a character alone
        void Status()
        {
            status.Text = $"You have {CharStatFile.FormatNumber(_ch.GoldInBank)} gold and "
                        + $"{Character.BankSlots - _ch.Bank.Count(b => b.ItemId > 0)} free Bank slots available.";   // Ref > 0 (231)
            CheckWealthiest();
            pool.IsEnabled = partyDep.IsEnabled = poolAll.IsEnabled = _party.Size > 1;
        }
        void Refresh()
        {
            DrawVault();
            Status();
        }
        _bankRefresh = Refresh;
        // lblCall "GOLD" (FRMCITYB.BAS:154-155): sub2884 alone, which mdrcharStats sends whenever the gold
        // shown changes in town (MAINMOD2.BAS:5616-5620) - charPay's, from whichever service charged
        Action gold = Status;
        _bankGold = gold;
        vault.DetachedFromVisualTree += (_, _) => { if (_bankGold == gold) _bankGold = null; };
        Refresh();

        // lstItems_DragDrop (FRMCITYB.BAS:172-206): a pack line other than Hands. The drop and the
        // Deposit button both come here.
        async System.Threading.Tasks.Task Deposit(int slot)
        {
            // "If fn03BD(frmCityStore) Then frmCityStore!lblCall = "UPDATE"" (FRMCITYB.BAS:180), the
            // first thing done for every pack line dropped - the Hands, an empty slot and a refused
            // one alike - and before anything moves. The Store's lblCall_Change (FRMCTYS.BAS:479-502)
            // empties its typed boxes and captions and redraws its shelf; in the original the Store
            // and the Bank can be open together (frmCityMain shows each modeless, FRMCTYM.BAS:183-197).
            // So they can here: an open Store hears it in its own window, and with none open
            // nothing does - as fn03BD finds when the Store is not loaded
            _storeUpdate?.Invoke();
            if (slot <= 0 || slot >= Character.PackSize) return;   // "If Source.Index Then" (FRMCITYB.BAS:181)
            // an empty slot is not equipped and runs on to the room test: silent with a hole free,
            // "Out of room!" with all forty full (FRMCITYB.BAS:186-196)
            if (_ch.Pack[slot] is InventorySlot sl && sl.ItemId != 0 && sl.Equipped)
            { Log("Cannot put equipped items in the bank!"); return; }
            if (!_ch.DepositToVault(slot)) { await Tell("Out of room!", "There is no more room left for another item!"); return; }
            // silent, and the buffers are left pointing where they were (201-203)
            Save(); Refresh(); RefreshCharacterWindow();
        }
        Dragging.Target(vault, DragKind.Item, async p => await Deposit(p.Index));
        // the line pressed on, not SelectedIndex: with Shift/Ctrl selection that is the anchor
        Dragging.SourceLines(vault, row => row < rowSlots.Count ? DragPayload.Vault(rowSlots[row]) : null);

        // DEVIATION: the port's Deposit, Withdraw and double-click (see the summary)
        deposit.Click += async (_, _) => await Deposit(PackList.SelectedIndex);
        async System.Threading.Tasks.Task Withdraw(IEnumerable<int> rows)
        {
            // the vault slots behind the rows, taken before the first one redraws the list
            var slots = rows.Where(r => r >= 0 && r < rowSlots.Count).Distinct().OrderBy(r => r).Select(r => rowSlots[r]).ToList();
            foreach (int v in slots)
            {
                int free = _ch.FirstFreeSlot();
                if (free < 0) { await Tell("SORRY!", Party.NoFreeSlotsFromVault(_ch)); return; }
                TakeFromVault(v, free);
            }
        }
        withdraw.Click += async (_, _) => await Withdraw(vault.Selection.SelectedIndexes.ToList());
        vault.DoubleTapped += async (_, e) =>
        {
            if (e.Source is not Avalonia.Visual hit
                || Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ListBoxItem>(hit, includeSelf: true) is not ListBoxItem line) return;
            int row = vault.IndexFromContainer(line);
            if (row >= 0) await Withdraw(new[] { row });
        };

        // a mod's Bank commands (ctx.UI.AddBankCommand), right-aligned on the "Deposit/Withdraw
        // Items" caption's line; with none, nothing is added. A mod setting changing redraws the list.
        if (ModSlots.BankCommands(() => new Mordor.Modding.BankCommandContext(_ch, Log, Lookup),
                                  () => { Save(); Refresh(); RefreshCharacterWindow(); _storeUpdate?.Invoke(); }, form.Fonts) is { } modRow)
            FormPage.Place(root, new Panel { Children = { modRow } }, BankModCommands);
        if (Mordor.Modding.ModHost.Active.Live.Any())
        {
            var host = Mordor.Modding.ModHost.Active;
            Action again = () => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_bankRefresh != null) DrawVault(); });
            vault.AttachedToVisualTree += (_, _) => { host.Changed -= again; host.Changed += again; };
            vault.DetachedFromVisualTree += (_, _) => host.Changed -= again;
        }

        // txtAmount_KeyPress (303-331): Enter moves the gold and says nothing. Too much, or a deposit
        // of nought or less, gets a Beep; either way the box is emptied. A withdrawal is only
        // refused for being more than the bank holds, so a negative one goes through and pays in.
        void Submit(TextBox box, bool deposit)
        {
            // CCur of the text, fraction and all (Party.BankTyped). Text that is not a number, or
            // comes to nought, stops the transfer and leaves the box as it was (On Error, If l00E6),
            // but the status still redraws (330).
            var said = Party.BankTyped(_ch, box.Text ?? "", deposit);
            if (said == Party.BankAnswer.Beep) Audio.Beep();
            if (said != Party.BankAnswer.Ignored) box.Text = "";
            RefreshSummary(); Save(); Refresh();
        }
        depositBox.KeyDown += (_, k) => { if (k.Key == Avalonia.Input.Key.Enter) { Submit(depositBox, true); k.Handled = true; } };
        withdrawBox.KeyDown += (_, k) => { if (k.Key == Avalonia.Input.Key.Enter) { Submit(withdrawBox, false); k.Handled = true; } };
        // cmdCommand 0 and 1 (28-35): fill the box with everything, LTrim$(Str$(balance)), and press Enter
        var depAll = FormPage.Button(root, "All", ServiceForms.Bank.DepositAll, bold: false);
        depAll.Click += (_, _) => { depositBox.Text = Party.BankAllText(_ch.GoldOnHand); Submit(depositBox, true); };
        var wdAll = FormPage.Button(root, "All", ServiceForms.Bank.WithdrawAll, bold: false);
        wdAll.Click += (_, _) => { withdrawBox.Text = Party.BankAllText(_ch.GoldInBank); Submit(withdrawBox, false); };

        // the Party Options (38-59), each silent
        pool.Click += (_, _) =>
        {
            Party.PoolToBank(_party.Members, _ch);   // all or nothing on an Overflow: FRMCITYB has no handler here
            RefreshSummary(); Save(); Refresh();
            _guildUpdate?.Invoke();   // "If fn03BD(frmCityGuild) Then frmCityGuild!lblCall.Caption = "UPDATE"" (FRMCITYB.BAS:64)
        };
        partyDep.Click += (_, _) =>
        {
            Party.PartyDeposit(_party.Members);
            RefreshSummary(); Save(); Refresh();
            _guildUpdate?.Invoke();   // "If fn03BD(frmCityGuild) Then frmCityGuild!lblCall.Caption = "UPDATE"" (FRMCITYB.BAS:64)
        };
        poolAll.Click += (_, _) =>
        {
            Party.PoolAndDeposit(_party.Members, _ch);
            RefreshSummary(); Save(); Refresh();
            _guildUpdate?.Invoke();   // "If fn03BD(frmCityGuild) Then frmCityGuild!lblCall.Caption = "UPDATE"" (FRMCITYB.BAS:64)
        };

        // cmdInfo_Click (74-84): the selected vault item on the character window's Misc page
        info.Click += (_, _) =>
        {
            if (vault.SelectedIndex < 0 || vault.SelectedIndex >= rowSlots.Count) return;
            var sl = _ch.Bank[rowSlots[vault.SelectedIndex]];
            if (Lookup(sl.ItemId) is Item it) ShowItemInMisc(it, sl.Alignment, sl.Known);
        };
        var exit = FormPage.Button(root, "Exit", ServiceForms.Bank.cmdExit);
        FormPage.Access(exit, 'X');   // "E&xit": Alt+X while this form is the active one (ServiceAccessKey)
        exit.Click += (_, _) => { _bankRefresh = null; CloseService(null, null!); };
        ShowForm(form);
    }

    /// <summary>
    /// A line of the Bank's list dropped on a pack slot of the Items window (FRMCHAR3.BAS:530-561):
    /// Hands refuses in silence, an equipped slot with the bank's line; an item already there changes
    /// places with the one coming out, an empty slot just takes it. What the character can make of
    /// it now may be more than when it went in, and that is said. Buffers naming the slot let go.
    /// </summary>
    private void TakeFromVault(int vaultSlot, int onto)
    {
        // lblItem_DragDrop's last line, "If fn03BD(frmCityStore) Then frmCityStore!lblCall = "UPDATE""
        // (FRMCHAR3.BAS:583), is reached by a drop on the Hands too; the equipped refusal's Exit Sub skips it
        if (onto <= 0 || onto >= Character.PackSize) { _storeUpdate?.Invoke(); return; }
        if (vaultSlot < 0 || vaultSlot >= _ch.Bank.Count || _ch.Bank[vaultSlot].ItemId <= 0) return;
        if (_ch.Pack[onto] is InventorySlot there && there.ItemId != 0 && there.Equipped)
        {
            Log("Cannot put equipped items in the bank!");
            return;
        }
        var coming = _ch.Bank[vaultSlot];
        _ch.Bank[vaultSlot] = _ch.Pack[onto] is InventorySlot going && going.ItemId != 0 ? going : new InventorySlot();
        _ch.PutInSlot(onto, coming);
        if (Lookup(coming.ItemId) is Item it)
        {
            var now = Loot.ReadableLevel(_ch, it, Lookup);
            if (now > coming.Known)
            {
                coming.Known = now;
                Log("You have re-identified the retrieved item as: " + Loot.NameFor(it, coming.Known, coming.Alignment, coming.Charges));
                // sub0730 files it at the level re-read (FRMCHAR3.BAS:550), graded as that level
                // grades it rather than as known outright
                Librarian.FileItem(SavesDir, _ch, it.Id, coming.Known);
            }
        }
        SpellBuffers.PackSlotEmptied(_ch, onto);
        Save();
        RefreshCharacterWindow();
        _bankRefresh?.Invoke();
        _storeUpdate?.Invoke();
    }

    // ---------------- Morgue ----------------
    /// <summary>
    /// The dead screen's "&amp;Wait for Rescue", <c>cmdWait_Click</c> (FRMDUNM.BAS:144-196), for a
    /// body below ground on the city-hosted dead screen: the button disabled while the days are
    /// drawn (162), the days rolled as the dungeon's own wait rolls them (164-186), a Beep and the
    /// "Wait for Rescue" box (182-183), the body carried to town and aged (187-189), the stats
    /// shown and saved (190), and then <c>sub0930 Me</c> again (194) - now at level 0, so
    /// "&amp;Visit the Morgue" and the morgue's own box (MAINMOD2.BAS:3048, 3068-3087). The
    /// rules are Rescue's, shared with the dungeon's dead screen.
    /// </summary>
    private async System.Threading.Tasks.Task WaitForRescue(Button btn)
    {
        // "If Chars(Me).DeathState > False Then If Chars(Me).DungeonLevel > False Then" (155-156);
        // otherwise only sub0930 Me
        if (!_ch.IsDead) return;
        if (_ch.DungeonLevel <= 0) { if (DeadScreenOwed) ShowDead(); return; }
        Floor? floor = null;
        try
        {
            _areaDungeon ??= MdrLoader.LoadDungeon(System.IO.Path.Combine(_gameDir, "MDATA11.MDR"));
            if (_ch.DungeonLevel <= _areaDungeon.Count) floor = _areaDungeon[_ch.DungeonLevel - 1];
        }
        catch (Exception ex) { CrashLog.Write("wait for rescue: the dungeon", ex); }
        bool easy = floor != null && Rescue.EasyToFind(floor, _ch.X, _ch.Y);          // 158-161
        int days = Rescue.DaysUntilFound(_rng, _ch.DungeonLevel, SavedArea(_ch), easy);  // M59BB (170)
        btn.IsEnabled = false;                                                         // 162
        Audio.Beep();                                                                  // 182
        await Tell("Wait for Rescue", Rescue.Found(days));                             // 183
        Rescue.CarryHome(_ch, days);                                                   // 187-189
        try
        {
            if (_party.Members.IndexOf(_ch) == 0)
            {
                // as the dungeon reads it: the saved deepest, or the map's when none is saved
                int deepest = DungeonState.Load(SavesDir).DeepestReached;
                if (deepest == 0) deepest = SharedMap.Load(SavesDir).DeepestVisited;
                Rescue.TurnOverTown(SavesDir, _items, _monsters, _rng, deepest);
            }
        }
        catch (Exception ex) { CrashLog.Write("rescue replenish", ex); }
        btn.IsEnabled = true;                                                          // 192
        // a mod hears of the arrival, as it does for the dungeon's wait (docs/MODDING.md)
        var (_, deadLine) = Mordor.Modding.ModHost.Active.ArrivedInTown(_party, _ch, Mordor.Modding.TownArrivalKind.Rescued);
        RefreshSummary(); Save();                                                      // mdrcharStats True, 50 (190)
        // sub0930 Me (194): its head, its screen and its boxes again, now for a body at level 0
        ArriveDeadSub0930();
        if (deadLine != null) Log(deadLine);
    }

    /// <summary>
    /// <c>sub0930 Me</c> for a character arriving dead - loaded (sub14E0, MAINMOD2.BAS:3181-3182),
    /// or found by Wait for Rescue (FRMDUNM.BAS:194). Its head runs first, whatever follows: the
    /// companions let go, the carry put down and sub048C (3026-3033) - so a body that
    /// mdrcharSetDead alone left poisoned, diseased or held (a companion's mishap, the chest trap,
    /// the list's UPDATE) is cleaned before any raise can reach it - then the save (fn04ED, 3034),
    /// then its screen and, once the window is up, its boxes (ArriveDead). The port had shown the
    /// screen without the head, and a pending raise then brought the body back still poisoned.
    /// </summary>
    private void ArriveDeadSub0930(Character? whom = null)
    {
        (whom ?? _ch).Sub0930Head(Lookup);   // sub0930 pv04B4: Me arriving, Chars(0) from the Control Panel
        RefreshSummary();
        Save();
        // "If UBound(Chars) > False Then frmParty!lblCall = "UPDATE": Exit Sub" (MAINMOD2.BAS:3035-3038):
        // the screen and its boxes are for a character alone
        // "frmParty!lblCall = "UPDATE"" (3036): in company anyone at nought leaves - this one too, if
        // anyone else is left standing (FRMGROUP.BAS:321-346)
        if (_party.Size > 1) PruneTheDead();
        if (!DeadScreenOwed) return;
        _arrivedDead = true;
        ShowDead();
    }

    /// <summary>
    /// The party's own copy of a character picked from the files on disk, or the disk copy when
    /// they are not in the party. The morgue's lists are read off disk (the original's MorgueList),
    /// and the morgue writes its change to the file (FRMMORG.BAS:161, 172; sub0A7C's Put,
    /// MAINMOD2.BAS:5994). In the original nobody it can pick is also in Chars(): a party member at
    /// nought is unloaded by the party list's UPDATE (FRMGROUP.BAS:321-346), and a living member
    /// is in town with the one asking, so never at the level typed. In the port a party member
    /// can still be picked, and the party's save (<see cref="Save"/>) would then write the stale
    /// party copy over the file - the gold spent and the change lost - so the change is made on
    /// the party's copy, which that save then writes.
    /// </summary>
    private Character PartyCopyOf(Character onDisk) =>
        _party.Members.FirstOrDefault(m => m.Name.Trim() == onDisk.Name.Trim()) ?? onDisk;

    /// <summary>
    /// Hiring rescuers, <c>FRMMORG.BAS:104-176</c> - the morgue counter's, for a living character
    /// in town; the dead screen never offers it. You name a dead character and the coordinates
    /// you think they are at; the morgue charges by depth and sends a party. They have to be told
    /// roughly the right place - within eight squares each way and the exact level - and past the
    /// twenty-fifth floor they sometimes simply do not come back.
    /// </summary>
    private Button RescuersButton()
    {
        // the button's caption, from the form in MORDOR.EXE
        var go = new Button { Content = "Hire Rescuers" };
        go.Click += async (_, _) =>
        {
            // FRMMORG.BAS:110-113: "FIND CHARACTER" / "Character's Name?" over everyone but you
            var everyone = Character.LoadAllFrom(SavesDir).Where(c => c.Name.Trim() != _ch.Name.Trim()).ToList();
            int pick = await NamePicker.Ask(this, "FIND CHARACTER", "Character's Name?",
                                            everyone.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor(""));
            if (pick < 0) return;
            var target = PartyCopyOf(everyone[pick]);
            // 114-116: the coordinates, and nothing said for "", "0" or a cancel
            string? typed = await AskFor("Location?", "Last known coords?\n(I.E. \"15,5,3\" for 15,5 Level 3)");
            if (typed == null || typed == "" || typed == "0" || typed == "-1") return;
            // the comma loop and Val, as FRMMORG.BAS:118-135 has them
            var parts = Rescue.SplitCoords(typed);
            if (parts.Count != 3) { await Tell("Bad coordinates", Rescue.BadCoordinates); return; }
            int gx = parts[0], gy = parts[1], gl = parts[2];
            if (gx < 1 || gy < 1 || gl < 1 || gl > 15) { await Tell("Sorry!", Rescue.InvalidCoordinates); return; }

            long cost = Rescue.SearchCost(gl);
            if (!await Confirm("Start Search?", Rescue.Offer(cost, target.Name))) return;
            if (!CharPay(cost)) { await Tell("HIRE RESCUERS", Rescue.NotEnoughGold); return; }
            RefreshSummary();

            // the outcome goes to the Messages window (sub085B, 157-178)
            if (!Rescue.RescuersReturn(_rng, gl)) { Log(Rescue.NeverReturned); Save(); return; }

            bool near = Rescue.CloseEnough(gx, gy, gl, target.X + 1, target.Y + 1, target.DungeonLevel);   // stored from 0, typed from 1
            var (message, back) = Rescue.Report(target, near);
            if (message != "") Log(message);
            if (back)
            {
                target.DungeonLevel = 0;
                target.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(target.Name) + ".json"));
            }
            Save();
            ShowMorgue(null, null!);
        };
        return go;
    }


    /// <summary>Set on arrival dead: sub14E0 calls sub0930 (MAINMOD2.BAS:3181-3182), which puts its boxes up at once.</summary>
    private bool _arrivedDead;

    /// <summary>
    /// What a dead character is shown - sub0930's screen (MAINMOD2.BAS:3016-3131). In the original
    /// that is the dungeon window, lblDead and cmdWait, even at level 0: sub14E0 loads frmDungeonMain
    /// for anyone dead (MAINMOD2.BAS:3170-3182), and a dead character never reaches the city or the
    /// morgue counter. The port opens every character in the city, so the screen is a service
    /// window of the city frame's, with the town's own window hidden while it is up (since
    /// 2026-10-19); it used to be a branch of the morgue counter's page, which it is not.
    /// </summary>
    private void ShowDead()
    {
        // frmDungeonMain's caption, as mdrmulSwitch (MULTIMOD.BAS:623) sets it
        OpenService("Dungeon (" + _ch.Name.TrimEnd() + ")", GameWindow.Dungeon);
        // frmDungeonMain in frmCityMain's place: sub14E0 shows the one or the other, never both
        // (MAINMOD2.BAS:3170-3180), so while the dead screen is up the town is not there to click
        _cityChild.IsVisible = false;
        var sp = new StackPanel { Spacing = 8 };
        Func<System.Threading.Tasks.Task>? visit = null;

        // sub0930 (MAINMOD2.BAS:3050-3055): pulled into the rock, at any depth, the label says so and
        // cmdWait stays hidden - nothing to wait for and no morgue to visit. The port had shown the
        // ordinary label and Hire Rescuers below ground, and the morgue's rock box as panel text at
        // level 0; that box is ArriveDead's now.
        if (_ch.CauseOfDeath == Rules.DeathRock)
        {
            sp.Children.Add(new TextBlock
            {
                Text = Rescue.DeadInRock,
                Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap,
            });
        }
        // MAINMOD2.BAS:3068 - the morgue is only on offer once the body is out of the dungeon.
        // Below ground cmdWait is captioned "&Wait for Rescue" (3045-3046) and its click is
        // cmdWait_Click's wait (FRMDUNM.BAS:155-194). The port had offered the morgue counter's
        // Hire Rescuers here (FRMMORG.BAS:104-176), a service for a living character in town
        // that the dead screen never has.
        else if (_ch.DungeonLevel > 0)
        {
            sp.Children.Add(new TextBlock
            {
                // sub0930's own label (MAINMOD2.BAS:3052); the port had a paragraph of its own here
                Text = Rescue.DeadInTheDungeon,
                Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap,
            });
            var wait = new Button { Content = "_Wait for Rescue" };
            wait.Click += async (_, _) => await WaitForRescue(wait);
            sp.Children.Add(wait);
        }
        else
        {
            // Dying with your own body in the morgue is its own path in MAINMOD2.BAS, separate from a
            // living character raising someone at the FRMMORG counter (MAINMOD2.BAS:3073-3086):
            //   l04CC = Int(2912.32# * (MaxHits / 1.5#))
            //   If highest guild level <= 5 Then
            //       If Gold + BankGold < l04CC Then the guild offers to pay
            //   Else "Do you wish to pay?"
            //   Chance of success is morgueRaiseChance(-1, -1)%
            // A character at guild level 5 or under who can afford it is asked nothing between the
            // fee and the chance - the Else belongs to the level test, not the purse test.
            var deadOne = _ch;   // the character this screen and its price are for
            long cost = (long)(2912.32 * (_ch.MaxHits / 1.5));
            int chance = Rules.MorgueRaiseChance(_ch.Stats[(int)Stat.Con]);
            bool lowLevel = _ch.TopGuildLevel <= 5;
            bool guildPays = lowLevel && _ch.Purse < cost;

            string offer = $"A small fee of {cost:N0} is required for your Resurrection.";
            if (guildPays)
                offer += "\nHowever, since you are just starting out, the guild offers to pay for your resurrection.\nIs this acceptable?";
            else if (!lowLevel)
                offer += "\nDo you wish to pay?";
            offer += $"\nChance of success is {chance}%.";
            // What a dead character sees is sub0930's screen, not the offer: the lblDead label and
            // cmdWait captioned "&Visit the Morgue" (MAINMOD2.BAS:3044-3051). The offer is the box that
            // button - or arriving dead (sub14E0 calls sub0930, 3180) - puts up. The port had shown the
            // offer as panel text beside a "Pay N gold" / "Accept the guild's offer" button of its own.
            sp.Children.Add(new TextBlock
            {
                Text = Rescue.DeadInTheDungeon,
                Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap,
            });

            var msg = new TextBlock { Foreground = Brushes.Khaki, TextWrapping = TextWrapping.Wrap };
            var btn = new Button
            {
                Content = "_Visit the Morgue",
            };
            async System.Threading.Tasks.Task Visit()
            {
                // MAINMOD2.BAS:3087-3118. The offer is a Yes/No box titled "Welcome to the morgue!";
                // a short purse is "Not Enough Gold!" in a SORRY! box, and nothing is taken. The port
                // had shown the offer as text and answered a short purse with "Not Enough Gold! (you
                // have N between purse and bank)" and a raise the guild paid for with "The Xs settle
                // the bill.", neither of them the original's.
                // cmdWait_Click acts only for a dead one in command (FRMDUNM.BAS:155) and ends in
                // "sub0930 Me" (FRMDUNM.BAS:194); the morgue box is offered to a character alone
                // (MAINMOD2.BAS:3035-3038): the dead one's, priced for them, and nobody else's
                if (!ReferenceEquals(_ch, deadOne) || !DeadScreenOwed) return;
                Audio.Beep();                                   // the Beep before the box (MAINMOD2.BAS:3074)
                if (!await Confirm("Welcome to the morgue!", offer)) return;
                if (!guildPays && !CharPay(cost)) { await Tell("SORRY!", "Not Enough Gold!"); return; }
                btn.IsEnabled = false;
                // raiseComplications (1642-1685) is shared with the raise spells through Raising; the
                // own-body routine's differences - its Constitution line and the floor at the race
                // minimum - are ownBody.
                var outcome = Raising.Resolve(_rng, _ch, RaceData.ByName(_ch.Race), Raising.ByMorgue, chance, Lookup, ownBody: true, inAParty: _party.Size > 1);
                // "Raise Failed!" and "Success!" are MsgBoxes titled "Raise Info" (3095, 3101); what
                // follows them is sub085B lines, but for raiseComplications' own two boxes (1653, 1675)
                if (outcome.Lines.Count > 0) await Tell("Raise Info", outcome.Lines[0]);
                var said = new List<string>();
                foreach (string line in outcome.Lines.Skip(1))
                {
                    // sound 38 is POISON.WAV in the SoundFiles table (MODULE1.BAS:325); complications play it (MAINMOD2.BAS:1652)
                    if (line == "There are complications...") Audio.PlayOnNextChannel("POISON");
                    if (PendingRaise.BoxTitleFor(line) is string title) await Tell(title, line);
                    else said.Add(line);
                }
                msg.Text = string.Join("  ", said);
                if (!outcome.Raised) { RefreshSummary(); Save(); return; }

                // no cure here: the own-body raise (MAINMOD2.BAS:3087-3118) touches no ailment. sub048C
                // ran as the body arrived (ArriveDeadSub0930) and the city arrival that follows rests
                // it (FRMCTYM.BAS:113-130); the port's CureAll here had left PoisonLevel behind
                GuildLog.Append(SavesDir, GuildData.IndexOf(_ch.Guild.Guild), GuildLog.Wrote.ResurrectedInMorgue(_ch.Name));
                Audio.PlayOnNextChannel("RAISE");
                CityArrivalAfterRaise();
            }
            btn.Click += async (_, _) => await Visit();
            sp.Children.Add(btn); sp.Children.Add(msg);
            visit = Visit;
        }
        ServicePage.Content = sp;

        if (_arrivedDead)
        {
            _arrivedDead = false;
            // a box needs its owner on screen; arriving, the window is not open yet
            if (IsVisible) _ = ArriveDead(visit);
            else Opened += async (_, _) => await ArriveDead(visit);
        }
    }

    /// <summary>
    /// sub14CC after a raise (MAINMOD2.BAS:3134-3155): below ground the dungeon is set going again
    /// where the body lay; at level 0 frmDungeonMain is unloaded and frmCityMain shown, so its
    /// Form_Load runs - the city arrival, rest and ageing and all (FRMCTYM.BAS:103-134). The port
    /// had only cleared the ailments and stayed on the dead screen's panel.
    /// </summary>
    private void CityArrivalAfterRaise()
    {
        // frmDungeonMain unloaded: the dead screen's own window, whichever is at hand
        if (OpenServiceFor(GameWindow.Dungeon) is Svc deadScreen) InServiceWindow(deadScreen, () => CloseService(null, null!));
        Save();
        if (_ch.DungeonLevel > 0) { EnterDungeon(null, null!); return; }
        Host.Active = _cityChild;
        // and that Form_Load tells the map "UPDATE" and minimises it (FRMCTYM.BAS:149-156): the dead
        // screen had left it open, still showing the body's level and square
        CityFormLoadTellsAutomap();
        var rested = RestOnArrival();
        RefreshSummary(); Save();
        foreach (var line in rested) Log(line);
    }

    /// <summary>
    /// sub0930's boxes on arrival, in its order (MAINMOD2.BAS:3035-3087). With anyone else loaded
    /// it stops before any of them (UBound(Chars) &gt; False, 3035-3038). Otherwise a raise somebody
    /// else cast is offered first (fn16B5); if it is taken the character is on their feet and
    /// sub14CC puts them back where they stand. Refused, or failed without complications, it is
    /// forgotten and saved; then a body at level 0 gets the morgue's rock box, or its own offer.
    /// </summary>
    private async System.Threading.Tasks.Task ArriveDead(Func<System.Threading.Tasks.Task>? visit)
    {
        if (_party.Size > 1) return;
        if (_ch.IsDead && PendingRaise.IsPending(_ch))
        {
            // UBound(Chars) is 0 by now, so the wording for a character run alone
            if (await OfferPendingRaise(_ch, alone: true))
            {
                // sub14CC (MAINMOD2.BAS:3134-3155) shows the dungeon for a body below ground and the
                // city otherwise. The port's city is already up; below ground, the dungeon button
                // resumes where the body lies.
                CityArrivalAfterRaise();
                return;
            }
            PendingRaise.Forget(_ch);
            Save();
        }
        if (!_ch.IsDead || _ch.DungeonLevel != 0) return;
        if (_ch.CauseOfDeath == Rules.DeathRock)
        {
            // MsgBox ..., 64, "Morgue" (MAINMOD2.BAS:3070) - a box, and then sub0930 exits
            await Tell("Morgue", "You are in the morgue.  However, since you were pulled from Rock, they can't help you.");
            return;
        }
        if (visit != null) await visit();
    }

    /// <summary>
    /// fn16B5 (MAINMOD2.BAS:314-380): the raise another character cast on <paramref name="who"/>,
    /// offered in a RAISE box and rolled only on Yes. True when they are on their feet. Its boxes
    /// are MsgBoxes, its other lines sub085B's, and the guild hears who did it. On a No the file is
    /// saved as it stands, as mdrcharUnload does for the join case (MAINMOD.BAS:419-423): a rocked
    /// body's chance, zeroed before the box, is wiped.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> OfferPendingRaise(Character who, bool alone)
    {
        PendingRaise.Prepare(who);
        string file = System.IO.Path.Combine(SavesDir, Character.SafeFileName(who.Name) + ".json");
        Audio.Beep();                                       // fn16B5's Beep (MAINMOD2.BAS:336)
        if (!await Confirm(PendingRaise.OfferTitle, PendingRaise.Offer(who, alone))) { who.Save(file); return false; }
        var outcome = PendingRaise.Accept(_rng, who, RaceData.ByName(who.Race), Lookup, alone, out string by);
        foreach (string line in outcome.Lines)
        {
            if (line == "Success!") Audio.PlayOnNextChannel("RAISE");     // sub086D mc00BE, just before the box
            // sound 38 is POISON.WAV in the SoundFiles table (MODULE1.BAS:325); raiseComplications plays it before the box (MAINMOD2.BAS:1652)
            if (line == "There are complications...") Audio.PlayOnNextChannel("POISON");
            if (PendingRaise.BoxTitleFor(line) is string title) await Tell(title, line);
            else Log(line);
        }
        if (!outcome.Raised) { who.Save(file); RefreshSummary(); return false; }
        // sub0578 - the guild's line (MAINMOD2.BAS:373)
        GuildLog.Append(SavesDir, GuildData.IndexOf(who.Guild.Guild), GuildLog.Wrote.Resurrected(who.Name, by));
        who.Save(file);
        RefreshSummary();
        return true;
    }

    /// <summary>sub2BDB's pick (FRMMORG.BAS:262-263): the companion slot m0026 and its price m0022.</summary>
    private int _morgueCompSlot;
    private long _morgueCompCost;

    /// <summary>lblCall_Change (FRMMORG.BAS:250-256): the party switched character while the counter is up.</summary>
    private Action? _morgueUpdate;

    /// <summary>
    /// The morgue counter, frmCityMorgue (FRMMORG.BAS), drawn to its form data as game_files/bm10.png shows it:
    /// "Welcome to the Morgue!", [Raise Character] [Hire Rescuers], [Grab Body], the Raise
    /// Companion button across the width, and Exit at the bottom right. Of the access keys only
    /// Exit's Alt+X is kept.
    ///
    /// Raise Character never raises anybody on the spot: it takes the fee and writes the raise on
    /// the body (sub0A7C), and the dead character is asked when next run - see
    /// <see cref="PendingRaise"/>.
    /// </summary>
    private void ShowMorgue(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // port-only: a dead character never reaches this form (sub14E0, MAINMOD2.BAS:3170-3182)
        if (DeadScreenOwed) { ShowDead(); return; }
        // a picture clicked for one already up brings it forward and builds nothing again (imgPicture_Click, FRMCTYM.BAS:192-203)
        if (s != null && BringUpOpenService(GameWindow.Morgue)) return;
        OpenService("Morgue", GameWindow.Morgue);   // bm10.png's title bar

        // frmCityMorgue's form data (MORDOR.EXE RCDATA 28): a 3480x1905-twip client, every control in
        // elaElastic at 0,-15 (ServiceForms.Morgue has the rectangles). Label1 is 12 points bold, blue
        // and centred; bm10.png shows it underlined. The five buttons have MS Sans Serif 8.25 plain.
        var form = new FormPage(ServiceForms.Morgue.ClientW, ServiceForms.Morgue.ClientH, ServiceForms.Morgue.Elastic);
        var root = form.Root;
        FormPage.Label(root, Morgue.Welcome, ServiceForms.Morgue.Welcome, DesignFonts.Welcome, FormPage.Blue, align: 2)
            .TextDecorations = TextDecorations.Underline;
        var raise = FormPage.Access(FormPage.Button(root, "Raise Character", ServiceForms.Morgue.RaiseCharacter, bold: false), 'R');   // "&Raise Character"
        var rescuers = FormPage.Access(FormPage.PlaceButton(root, RescuersButton(), ServiceForms.Morgue.HireRescuers, bold: false), 'H');   // "&Hire Rescuers"
        var grab = FormPage.Access(FormPage.Button(root, "Grab Body", ServiceForms.Morgue.GrabBody, bold: false), 'G');   // "&Grab Body"
        var raiseComp = FormPage.Button(root, "", ServiceForms.Morgue.RaiseCompanion, bold: false);
        raiseComp.IsVisible = false;                           // Visible False in the form; shown only by sub2BDB
        var exit = FormPage.Button(root, "Exit", ServiceForms.Morgue.cmdExit, bold: false);
        FormPage.Access(exit, 'X');   // "E&xit": Alt+X while this form is the active one (ServiceAccessKey)

        // cmdCommand m001A, Raise Character (FRMMORG.BAS:65-88)
        raise.Click += async (_, _) =>
        {
            var here = BodiesInTheCity();
            int pick = await NamePicker.Ask(this, "RAISE CHARACTER", "Character's Name?",
                                            here.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("PICKUP"));
            if (pick < 0) return;
            var body = PartyCopyOf(here[pick]);
            // MsgBox ..., 48, "Sorry!" (FRMMORG.BAS:71)
            if (body.CauseOfDeath == Rules.DeathRock) { await Tell("Sorry!", Morgue.Rocked); return; }
            // FRMMORG.BAS:74-76 leaves if morgueRaiseChance answers 0. Called with -1 it only does
            // that for rock, which has just been turned away, so nothing reaches it.
            // morgueRaiseCost answers -1 when a raise is waiting on the body (FRMMORG.BAS:77-80)
            if (PendingRaise.IsPending(body)) { await Tell("SORRY!", PendingRaise.AlreadyCast); return; }
            long cost = Morgue.RaiseCost(body.MaxHits);
            if (!await Confirm("RAISE CHARACTER", Morgue.CostOffer(body.Name, cost))) return;
            // MsgBox("Not enough gold!", l0056, "SORRY!"): l0056 is a Variant nothing sets, so a
            // plain OK box with no icon (FRMMORG.BAS:83)
            if (!CharPay(cost)) { await Tell("SORRY!", Morgue.NotEnoughGold); return; }
            // sub0A7C l002C, morgueRaiseChance(l002C, -1), 23 (FRMMORG.BAS:86)
            PendingRaise.Initiate(body, _ch, Morgue.RaiseChance(body.Stats[(int)Stat.Con], body.CauseOfDeath), Raising.ByMorgue);
            body.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(body.Name) + ".json"));
            Save();
            RefreshSummary();
            await Tell("DONE!", Morgue.TakenCareOf);
        };

        // cmdCommand 1, Grab Body (FRMMORG.BAS:58-64)
        grab.Click += async (_, _) => await GrabBody();

        // sub2BDB (FRMMORG.BAS:258-269): the first companion slot in use at nought hits puts its
        // price on the button and shows it. Nothing here ever hides it again but a raise - a
        // switch to a character with no dead companion leaves the last caption and slot up.
        void Scan()
        {
            for (int i = 0; i < CompanionSlots.Slots; i++)
            {
                if (_ch.Companions[i] is not Companion c || c.Hits != 0) continue;
                var def = c.Def(_monsters);
                _morgueCompCost = Morgue.CompanionCost(c.MaxHits, def?.LevelFound ?? 0);
                _morgueCompSlot = i;
                raiseComp.Content = new TextBlock { Text = Morgue.CompanionCaption(_ch.CompanionName(i), _morgueCompCost) };
                raiseComp.IsVisible = true;
                break;
            }
        }
        Scan();
        _morgueUpdate = Scan;

        // cmdCommand 2, Raise Companion (FRMMORG.BAS:89-108)
        raiseComp.Click += async (_, _) =>
        {
            if (!CharPay(_morgueCompCost)) { await Tell("RAISE COMPANION", Morgue.NotEnoughGold); return; }
            RefreshSummary();
            if (_ch.Companions[_morgueCompSlot] is Companion comp)
            {
                int con = comp.Def(_monsters)?.Stats.ElementAtOrDefault(3) ?? 0;
                if (_rng.Next(100) > Morgue.CompanionChance(con))
                {
                    Audio.PlayOnNextChannel("POISON");                           // sound 38 (FRMMORG.BAS:96), POISON.WAV in the SoundFiles table (MODULE1.BAS:325)
                    Log(Morgue.RaiseFailed);
                    _ch.Companions.Release(_morgueCompSlot);       // Ref = -1: the slot is left empty
                }
                else
                {
                    Audio.PlayOnNextChannel("RAISE");                            // sub086D mc006C
                    Log(Morgue.RaiseWorked);
                    comp.Hits = comp.MaxHits;
                }
            }
            // M5943 = -1: whatever was carried is put down, and nothing is said of it
            _ch.CarryingCompanion = -1;
            _ch.CarryingCharacter = "";
            RefreshCompanions();
            raiseComp.IsVisible = false;
            Save();
        };

        exit.Click += (_, _) => { _morgueUpdate = null; CloseService(null, null!); };

        var update = _morgueUpdate;
        form.Page.DetachedFromVisualTree += (_, _) => { if (_morgueUpdate == update) _morgueUpdate = null; };
        ShowForm(form);
    }

    /// <summary>
    /// The Seer, <c>FRMSEEK.BAS</c>. He locates monsters and items; he does not identify anything
    /// and he does not lift curses, whatever the port used to offer here. LOCATE CHARACTER is his
    /// third command, over every character but you (FRMSEEK.BAS:322-356; see LocatePerson below).
    /// </summary>
    private void ShowSeer(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180): back to the dead screen
        if (DeadScreenOwed) { ShowDead(); return; }
        // a picture clicked for one already up brings it forward and builds nothing again (imgPicture_Click, FRMCTYM.BAS:192-203)
        if (s != null && BringUpOpenService(GameWindow.Seer)) return;
        OpenService("The Seer", GameWindow.Seer);

        async System.Threading.Tasks.Task Locate(bool monster)
        {
            // fn170D "LOCATE MONSTER" / "Name of the Monster?" (or ITEM), a plain line; cancelled or
            // empty, the Seer says nothing (FRMSEEK.BAS:61-64, 219-222). Every answer is a box.
            string? typed = await AskFor(monster ? "LOCATE MONSTER" : "LOCATE ITEM",
                                         monster ? "Name of the Monster?" : "Name of the Item?");
            if (string.IsNullOrEmpty(typed)) return;
            string title = monster ? "Locate Monster" : "Locate Item";

            // he matches on the start of the name, and refuses to guess between two: monsters from
            // record 1 (For l0036 = 1 To gv1BF6 - 1), each also by its plural, monsterNameFor(Me, 2, ..)
            // with IdLevel nought (FRMSEEK.BAS:66-86); items from record 0, Hands included
            // (For l0036 = False To gv1BEC - 1, 224-233)
            var pool = monster ? _monsters.Skip(1).ToList() : null;
            var names = monster ? pool!.Select(m => m.Name).ToList() : _items.Select(i => i.Name).ToList();
            var plurals = monster ? pool!.Select(m => EncounterGenerator.NameFor(m, _ch, 2, 0)).ToList() : null;
            var (found, idx) = Seer.Match(typed, names, plurals);
            if (found == Seer.Found.None) { await Tell("Sorry!", monster ? Seer.NoSuchMonster(typed) : Seer.NoSuchItem(typed)); return; }
            if (found == Seer.Found.Several) { await Tell("Sorry!", monster ? Seer.SeveralMonsters : Seer.SeveralItems); return; }

            var mon = monster ? pool![idx] : null;
            var item = monster ? null : _items[idx];
            int level = monster ? mon!.LevelFound : item!.Floor;
            string what = monster ? mon!.Name : item!.Name;

            if (level > Seer.DeepestHeCanReach) { await Tell("Sorry!", monster ? Seer.MonsterTooDeep : Seer.ItemTooDeep); return; }

            long fee = Seer.Fee(_rng, level);
            string offer = monster ? Seer.OfferMonster(what, $"{fee:N0} gold") : Seer.OfferItem(what, $"{fee:N0} gold");
            if (!await Confirm(title, offer)) return;
            // charPay (MAINMOD2.BAS:693-709): the bank makes up what the purse lacks
            if (!CharPay(fee)) { await Tell(title, Seer.NotEnoughGold); return; }
            RefreshSummary(); Save();

            // the search can come to nothing, and the deeper the quarry the likelier that is
            if (Seer.SearchFailsEarly(_rng, level))
            {
                await Tell(title, Seer.FailureNamesTheLevel(_rng, level)
                    ? (monster ? Seer.MonsterLevelOnly(level) : Seer.ItemLevelOnly(level))
                    : (monster ? Seer.NoPlaceForMonster : Seer.NoCarrier));
                return;
            }

            if (!monster)
            {
                await Tell(title, Seer.Carriers(Seer.Carriers(_monsters, item!, _rng), _rng));
                return;
            }

            var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(_gameDir, "MDATA11.MDR"));
            if (level < 1 || level > dungeon.Count) { await Tell(title, Seer.CannotDetect); return; }
            var spot = Seer.Locate(dungeon[level - 1], mon!, _rng);
            if (spot == null) { await Tell(title, Seer.CannotDetect); return; }

            var (x, y, classes, lair) = spot.Value;
            // sub321C (FRMSEEK.BAS:413-428, called with 5 at 176): he is only roughly right - up to four
            // squares off each way, kept on the level
            (x, y) = Seer.Blur(_rng, x, y, 5, dungeon[level - 1].Width, dungeon[level - 1].Height);
            string text = Seer.Sighting(mon!.Name, x, y, level) + "\n"
                        + Seer.TypeIsCommon(lair ? Seer.AlwaysSeen : Seer.Commonality(classes));
            // three times in four he throws in how often it turns up among its own kind
            if (_rng.NextDouble() * 100 < 75) text += "\n" + Seer.SeenAmongItsType(Seer.Frequency(mon.Chance));

            await Tell(title, text);
        }

        /// <summary>
        /// LOCATE CHARACTER, FRMSEEK.BAS:322-356. The last of the Seer's three services, and the
        /// one the port had never built: it needs to read other characters' saves, which is what
        /// the original's random-access character file gives it for free.
        ///
        /// The fee has no random element, unlike the other two - it is the depth they are at and
        /// nothing else. The roll is against three times that depth, so the deeper somebody is the
        /// likelier he loses them, and a near miss still gets you the level.
        /// </summary>
        async System.Threading.Tasks.Task LocatePerson()
        {
            // "LOCATE CHARACTER" / "Character's Name?" over every character but yourself (FRMSEEK.BAS:326,
            // FRMINTER.BAS:339-386); a cancel ends it without a word
            var everyone = Character.LoadAllFrom(SavesDir).Where(p => p.Name.Trim() != _ch.Name.Trim()).ToList();
            int pick = await NamePicker.Ask(this, "LOCATE CHARACTER", "Character's Name?",
                                            everyone.Select(p => p.Name.TrimEnd()).ToList(), Interact.NobodyFor(""));
            if (pick < 0) return;
            var who = everyone[pick];
            if (who.DungeonLevel <= 0) { await Tell("Seer", Seer.InTheCity); return; }

            long fee = Seer.LocateCharacterFee(who.DungeonLevel);
            if (!await Confirm("Try to Locate", Seer.OfferCharacter(who.Name.Trim(), $"{fee:N0} gold"))) return;
            if (!CanPay(fee)) { await Tell("Locate Character", "Not enough gold!"); return; }
            CharPay(fee);

            RefreshSummary();
            Save();
            await Tell("Seer", Seer.LocateCharacter(new Random(), who.Name.Trim(), who.DungeonLevel,
                                                    who.X + 1, who.Y + 1, who.IsDead));
        }

        // frmCitySeer's form data (MORDOR.EXE RCDATA 52): a 5025x1755-twip client, every control in
        // elaElastic(0) at -15,-15 (ServiceForms.Seer has the rectangles). Label1 is 12 points bold,
        // blue and centred; lblLabel is captioned "Seer Options" in the form data (bm11.png shows
        // "Locate Options", from another build) over elaElastic(1), which holds cmdCommand 0, 2
        // and 3 - Character, Monster, Item. Exit is cmdCommand(1) (FRMSEEK.BAS:362-363). None of
        // the buttons has a font of its own: they are the form's 8.25 bold.
        var form = new FormPage(ServiceForms.Seer.ClientW, ServiceForms.Seer.ClientH, ServiceForms.Seer.Elastic);
        var root = form.Root;
        FormPage.Label(root, "Welcome to the City Seer!", ServiceForms.Seer.Welcome, DesignFonts.Welcome, FormPage.Blue, align: 2);
        FormPage.Label(root, "Seer Options", ServiceForms.Seer.OptionsCaption, FormPage.Navy);
        var options = FormPage.Elastic(root, ServiceForms.Seer.OptionsBox);
        var charBtn = FormPage.Access(FormPage.Button(options, "Character", ServiceForms.Seer.Character), 'C');   // "&Character"
        charBtn.Click += async (_, _) => await LocatePerson();
        var monBtn = FormPage.Access(FormPage.Button(options, "Monster", ServiceForms.Seer.Monster), 'M');   // "&Monster"
        monBtn.Click += async (_, _) => await Locate(true);
        var itemBtn = FormPage.Access(FormPage.Button(options, "Item", ServiceForms.Seer.Item), 'I');   // "&Item"
        itemBtn.Click += async (_, _) => await Locate(false);
        var exit = FormPage.Button(root, "Exit", ServiceForms.Seer.Exit);
        FormPage.Access(exit, 'X');   // "E&xit": Alt+X while this form is the active one (ServiceAccessKey)
        exit.Click += (_, _) => CloseService(null, null!);
        ShowForm(form);
    }

    // ---------------- Dungeon ----------------
    /// <summary>The character was loaded below ground and is being taken straight there (sub14E0).</summary>
    private bool _enteringFromLoad;

    private async void EnterDungeon(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the original never shows a dead character the town (sub14E0, MAINMOD2.BAS:3166-3182); the
        // nearest this port has is the morgue screen - "You are dead. Visit the Morgue to be revived
        // first." was the port's
        if (DeadScreenOwed) { ShowDead(); return; }
        // Unload frmCityMain unloads every service with it (FRMCTYM.BAS:169-179): a store or a bank
        // left open - a click on the Dungeon picture reaches past it - does not go down too
        CloseAllServices();
        if (_ch.CurHits <= 0) _ch.CurHits = 1;

        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(_gameDir, "MDATA11.MDR"));
        // a walk down from town, not a character loaded below ground nor a raise below (both of
        // which come here with the character's own level above nought)
        bool fromTown = !_enteringFromLoad && _ch.DungeonLevel < 1;
        var (level, x, y) = FindStart(dungeon);
        var win = new DungeonWindow(_party, dungeon, _gameDir, level, x, y);
        // and a mod may act once the dungeon is up (docs/MODDING.md, ModActions.EnteredDungeonFromTown)
        if (fromTown) win.EnteredFromTown();
        // sub14E0: frmParty's Form_Load runs mdrmulSwitch before the dungeon is shown, which
        // captions it "Dungeon (Name)" (MAINMOD2.BAS:3163, FRMGROUP.BAS:91, MULTIMOD.BAS:622-623)
        if (_enteringFromLoad) win.LoadedBelow();
        _enteringFromLoad = false;
        // one Messages window serves the whole game in the original (sub085B, MAINMOD2.BAS:3196-3205):
        // what the city has said - the autoload's lines when a party is run straight into the
        // dungeon (sub1738, MAINMOD2.BAS:6305-6306) - is still there below ground
        win.CarryMessages(_messages, _messageLooks);
        // and one frmAutoMap: open or shut, it goes down as it is (see TakeAutomapBack)
        if (_automapLoadedInTown) win.CarryAutomap(_automap, _automap != null && _automapChild.IsVisible);
        // The original has one of each of these forms - Party, the character window, Objects &
        // Spells, Messages, the automap - in the one frame for town and dungeon alike, and each writes
        // its own placement as it unloads (FRMGROUP.BAS:131, FRMAUTOM.BAS:172-176; sub162C,
        // MAINMOD2.BAS:4407-4450). The port has a copy of each per frame, so the city hands its
        // placements to the dungeon going down and takes the dungeon's back coming up; otherwise
        // the city's stale copies were written over the dungeon's as the city closed.
        SaveWindowLayout();
        // One frame, as the original has: "Unload frmCityMain" and "frmDungeonMain.Show" inside
        // frmMain (FRMCTYM.BAS:211-212). The town's toolbar and children leave the frame and the
        // dungeon's take their place; walking out puts the town back (MAINMOD2.BAS:3957-3958).
        var town = (Control?)GetValue(ContentControl.ContentProperty);
        _below = win;
        try
        {
            await win.RunInside(this,
                root => SetValue(ContentControl.ContentProperty, root),
                () =>
                {
                    SetValue(ContentControl.ContentProperty, town);
                    // the town's toolbar and windows are shown again from here on, so they say what
                    // the dungeon left - the experience still needed, the gold - straight away, not
                    // after the options, the layout and the music below (the rest's own refresh
                    // follows it). Unload frmDungeonMain / frmCityMain.Show is one step in the
                    // original (MAINMOD2.BAS:3957-3958), with nothing stale in between.
                    RefreshSummary();
                });
        }
        finally { _below = null; }
        // the game options are the frame's, and the Options window may have changed them below
        // (FRMCONTR.BAS:69-73 applies them the moment it closes): read them again
        _options = GameOptions.Load(_gameDir);
        Tools.IsVisible = !_options.NoToolbar;
        Audio.Apply(_options);
        // a TabControl taken out of the frame and put back shows an empty page until a tab is
        // picked again (an Avalonia behaviour): pick each one's own tab again
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var tab = _charPanel.Selected;
            _charPanel.Selected = tab == CharTab.Stats ? CharTab.Resist : CharTab.Stats;
            _charPanel.Selected = tab;
            int page = SheetTabs.SelectedIndex;
            SheetTabs.SelectedIndex = page == 0 ? 1 : 0;
            SheetTabs.SelectedIndex = page;
        }, Avalonia.Threading.DispatcherPriority.Loaded);
        Focus();
        ApplyWindowLayout();
        // q loads no frmCityMain (MAINMOD2.BAS:6316-6331), so the map is neither told "UPDATE" nor
        // minimised - and it is saved as it stood, not as a minimised window
        TakeAutomapBack(win, cityLoads: AutomapLayout.CityLoadsOnReturn(_ch.IsDead, win.ExitedToMainMenu));
        _messages.Clear(); _messageLooks.Clear(); _messages.AddRange(win.Messages); _messageLooks.AddRange(win.MessageLooks); ShowMessages();
        Host.Active = _cityChild;   // "frmCityMain.Show" is what brings you back (MAINMOD2.BAS:3958)
        // q's "Exit to Main Menu?" ends the game loop and shows frmMenu, the title, with nothing
        // in the city in between (MAINMOD2.BAS:6316-6331): no rest, the character left where they stood
        if (win.ExitedToMainMenu) { Save(); Close(); return; }
        // a session end answered Yes from the dungeon has already unloaded the party
        if (OriginalBox.Ending) return;
        Audio.Music("CITY");
        // Waiting out a rescue leaves the body at level 0 still in frmDungeonMain: cmdWait_Click ends
        // in sub0930 Me (FRMDUNM.BAS:194), its boxes, and the city is not loaded - no Form_Load, no
        // rest. The rest comes after a raise, when sub14CC shows frmCityMain (MAINMOD2.BAS:3150-3152).
        if (_ch.IsDead)
        {
            // a mod hears of the arrival (docs/MODDING.md); with none, nothing is asked
            var (_, deadLine) = Mordor.Modding.ModHost.Active.ArrivedInTown(_party, _ch, Mordor.Modding.TownArrivalKind.Rescued);
            ArriveDeadSub0930();
            if (deadLine != null) Log(deadLine);
            return;
        }
        var rested = RestOnArrival();
        // a mod may have the town do the Bank's Party Deposit or Party Pool as the party walks in (docs/MODDING.md);
        // with none, nothing is asked and nothing moves
        var (deposited, depositLine) = Mordor.Modding.ModHost.Active.ArrivedInTown(_party, _ch, Mordor.Modding.TownArrivalKind.Walked);
        RefreshSummary(); Save();
        if (_ch.CanLevelUp()) ShowGuild(null, null!);
        foreach (var line in rested) Log(line);
        if (depositLine != null) Log(depositLine);
        // the Bank's status line looks at the Wealthiest record after every party option (sub2884,
        // FRMCITYB.BAS:62): so does a deposit made on the Bank's behalf
        if (deposited != 0) CheckWealthiest();
    }

    /// <summary>Resume a saved dungeon position, else start at the level-1 up-staircase (the town entrance).</summary>
    private (int level, int x, int y) FindStart(List<Floor> dungeon)
    {
        if (_ch.DungeonLevel >= 1 && _ch.DungeonLevel <= dungeon.Count) return (_ch.DungeonLevel, _ch.X, _ch.Y);
        var f = dungeon[0];
        for (int y = 0; y < f.Height; y++)
            for (int x = 0; x < f.Width; x++)
                if (f.Has(x, y, Floor.STAIRS_UP)) return (1, x, y);
        return (1, 0, 0);
    }

    /// <summary>
    /// The city's Exit, imgPicture_Click case mc0026 (FRMCTYM.BAS:214-219): "Exit to Main Menu?"
    /// (MsgBox 36, "EXIT"). Yes sets M6023 and M6008, which end the game loop; sub1738 then runs
    /// sub16CC, the party's unload, and shows frmMenu (MAINMOD2.BAS:6316-6328). No does nothing.
    /// The port had left at once, playing BYE.WAV, which nothing on this path plays.
    /// </summary>
    private async void LeaveTown(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the Exit picture is the town's, which the dead are never shown (sub14E0, MAINMOD2.BAS:3166-3180);
        // q, which comes here with no sender, is fn1521's and works for the dead
        if (s != null && DeadScreenOwed) { ShowDead(); return; }
        if (!await Confirm("EXIT", "Exit to Main Menu?")) return;
        Save();
        await PartyUnload.Run(this, _party, SavesDir, Log);
        Close();
    }


    // ---------------- Confinement ----------------

    /// <summary>
    /// <c>gv1EBC.M57D4</c> as the Confinement leaves it: the companion slot the Misc page was last
    /// painted for from this window, -1 for a creature in the pens, -2 for none yet.
    /// </summary>
    private int _confinementInfoSlot = -2;

    /// <summary>
    /// <c>frmCityCStore!lblCall = "UPDATE"</c>, which mdrmulSwitch (MULTIMOD.BAS:634) sets when the
    /// party switches character in the city with the Confinement open. Null when it is not open.
    /// </summary>
    private Action? _confinementUpdate;

    /// <summary>
    /// Confinement &amp; Holding, frmCityCStore (FRMCITYC.BAS), drawn to its form data as the original's window is
    /// in the help's screenshot (bm9.png): Bind Companions and Identify, Realign &amp; Sell down the
    /// left, Buy Companions on the right with its G N E pen counts, a cost line and a search box.
    ///
    /// There is no list of your companions here. Both left-hand panels take a companion number typed
    /// into their Comp. box and Enter, read with fn33DB (FRMCITYC.BAS:654-672): <c>Val(text) - 1</c>,
    /// and anything outside the four slots or on an empty one is -1. The help's other way in, dragging
    /// a companion's picture from the Companions tab, needs a drag source there that this port does
    /// not have; a drop of anything else gets the original's refusal line.
    ///
    /// Everything it says is a MsgBox except the Buy line and the drop refusal, which go to the
    /// Messages window (<c>sub085B</c>), the city's message line here.
    /// </summary>
    private void ShowConfinement(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180): back to the dead screen
        if (DeadScreenOwed) { ShowDead(); return; }
        // a picture clicked for one already up brings it forward and builds nothing again (imgPicture_Click, FRMCTYM.BAS:192-203)
        if (s != null && BringUpOpenService(GameWindow.CompanionStore)) return;
        OpenService("Confinement & Holding", GameWindow.CompanionStore);   // bm9.png's title bar
        var pen = Confinement.Load(SavesDir, _monsters);   // pens the port filed Unaligned put right, too
        Monster? Def(Companion? c) => c?.Def(_monsters);   // Monsters(Companions(i).Ref)
        // a companion of somebody who joined since the city opened: its Ref settled, as on load
        _ch.Companions.Settle(_monsters);
        var gneTags = ItemInfoPage.AlignmentTags;             // gv077E$: (u) (g) (n) (e)

        // frmCityCStore's form data (MORDOR.EXE RCDATA 60): a 6510x4080-twip client, elaElastic(0) at
        // -30,-15 holding the three panels - elaElastic(3) Bind, (2) Identify, (1) Buy - with their
        // captions over them and Exit (ServiceForms.Confinement has the rectangles). The captions
        // are the form's 8.25 bold; the fields' labels are right-justified.
        var form = new FormPage(ServiceForms.Confinement.ClientW, ServiceForms.Confinement.ClientH, ServiceForms.Confinement.Elastic);
        var root = form.Root;
        FormPage.Label(root, "Buy Companions", ServiceForms.Confinement.BuyCaption);
        FormPage.Label(root, "Bind Companions", ServiceForms.Confinement.BindCaption);
        // two lines' height: "Companions", the third line, is cut off, as bm9.png shows
        FormPage.Label(root, "Identify, Realign & Sell Companions", ServiceForms.Confinement.IdCaption);
        var bindBox = FormPage.Elastic(root, ServiceForms.Confinement.BindBox);
        FormPage.Label(bindBox, "Comp.", ServiceForms.Confinement.BindComp, align: 1);
        FormPage.Label(bindBox, "Cost", ServiceForms.Confinement.BindCost, align: 1);
        var idBox = FormPage.Elastic(root, ServiceForms.Confinement.IdBox);
        FormPage.Label(idBox, "Comp.", ServiceForms.Confinement.IdComp, align: 1);
        FormPage.Label(idBox, "ID Cost", ServiceForms.Confinement.IdCostCaption, align: 1);
        var buyBox = FormPage.Elastic(root, ServiceForms.Confinement.BuyBox);
        FormPage.Label(buyBox, "Creature", ServiceForms.Confinement.CreatureHead, DesignFonts.CompCounts, FormPage.Maroon);
        // The counts' column (elaCompInfo, and lblLabel(4)'s "G N E" over it, both Courier New and set
        // right): the form's box, or a full line of counts in the chosen Pro font with CountsPad at
        // each side when that is wider, its right edge kept. The counts and "G N E" keep CountsPad in
        // from its right, so each letter is over its number and neither touches the box's edges: the
        // counts had been set left, against the box's left edge with the defaults, and a larger
        // Consolas cut the last one off (a deviation, docs/FIDELITY.md).
        const double CountsPad = 2;
        double countsRight = FormPage.X(ServiceForms.Confinement.elaCompInfo0.Left + ServiceForms.Confinement.elaCompInfo0.Width);
        double countsWidth = Math.Max(FormPage.X(ServiceForms.Confinement.elaCompInfo0.Width),
                                      UiFonts.TextWidth("9 9 9", form.Fonts, DesignFonts.CompCounts) + 2 * CountsPad);
        var gneHead = FormPage.Label(buyBox, "G N E", ServiceForms.Confinement.GneHead, DesignFonts.CompCounts, FormPage.Maroon, align: 1);
        gneHead.Padding = new Avalonia.Thickness(0, 0, CountsPad, 0);
        if (gneHead.Width < countsWidth) { Canvas.SetLeft(gneHead, countsRight - countsWidth); gneHead.Width = countsWidth; }
        FormPage.Label(buyBox, "Companion", ServiceForms.Confinement.CompanionCaption, align: 1);
        FormPage.Label(buyBox, "Search for What?", ServiceForms.Confinement.SearchCaption);

        // formatNumber (MODULE1.BAS:194-213): "###,###,###,###,###", and "0" for nought
        static string Fmt(decimal v)
        {
            string t = v.ToString("###,###,###,###,###", System.Globalization.CultureInfo.CurrentCulture);
            return t == "" ? "0" : t;
        }

        // fn33DB (FRMCITYC.BAS:654-672). l0224 is an Integer, so Val(text) - 1 is rounded half to
        // even, and a number too big for one falls into the error trap, which also answers -1.
        int SlotOf(string? text)
        {
            double v = VbVal(text ?? "") - 1;
            if (v > short.MaxValue || v < short.MinValue) return -1;
            int n = (int)Math.Round(v, MidpointRounding.ToEven);
            if (n < 0 || n > Character.MaxCompanionSlots - 1) return -1;
            return _ch.Companions[n] != null ? n : -1;   // Companions(l0224).Ref = -1: an empty slot
        }

        // frmCharStat3!lblCompName, as mdrcharShowCompanions writes it (MAINMOD2.BAS:4734-4738):
        // "1. (Kobold)" for one with no name of its own, "1. "Fido"" for one with
        string CompName(int i)
        {
            var c = _ch.Companions[i]!;
            string name = c.Name.TrimEnd();
            return name == "" ? $"{i + 1}. ({Def(c)?.Name ?? ""})" : $"{i + 1}. \"{name}\"";
        }

        // idLevelName (MAINMOD2.BAS:2553-2564), on a companion's ID level: 0, 1, 2, anything else
        static string IdLevelName(int level) => level switch
        {
            0 => "Completely Identified",
            1 => "Mostly identified",
            2 => "Partially identified",
            _ => "Barely identified",
        };

        // mdrcharSetInfo mc009C: the companion page on the Misc tab. The Info button on the
        // Companions tab also sharpens what you know first (CityWindow.Mdi.cs); these do not.
        void ShowPage(Companion? c, int slot, Monster def, Alignment storeAlign = Alignment.Good, bool reopen = true)
        {
            _charPanel.ShowMiscPage(CompanionInfo.Paint(c, slot, def, _ch, Lookup, storeAlign), MonsterPortrait.Of(def), _options.NoStretch);
            _confinementInfoSlot = slot;
            if (!reopen) return;
            _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
            _characterChild.Activate();
            Focus();   // picTabPicture.SetFocus: the keyboard leaves the Confinement's box (FRMCITYC.BAS:202, 218, 238)
        }

        // cmdID_Click (FRMCITYC.BAS:194-198) files the companion's new ID level in the Library with
        // sub06FE (MONSMOD.BAS:3938-4006) - Librarian.FileCompanion. No place: that needs a
        // dungeon level above 0 (MONSMOD.BAS:3988).
        void FileLibraryRecord(Monster def, int idLevel) => Librarian.FileCompanion(SavesDir, _monsters, _ch, def, idLevel);

        // modItemCost (FRMCITYC.BAS:620-641). With none left in the pen its Log(0) - or Log of less -
        // falls into the trap, which answers 0 and passes the message to sub04FC: the error file,
        // and nothing on screen unless the game runs with /DEBUG (MAINMOD2.BAS:1689-1700).
        long ModItemCost(Monster def, ItemAlignment align)
        {
            if (pen.Count(def, align) <= 0)
            {
                CrashLog.Note("Error 'Illegal function call' occurred in FRMCTYS:modItemCost.");
                return 0;
            }
            return pen.PriceToBuy(def, align);
        }

        void OnEnter(TextBox box, Action act) => box.KeyDown += (_, k) =>
        {
            if (k.Key != Avalonia.Input.Key.Enter && k.Key != Avalonia.Input.Key.Return) return;
            k.Handled = true;
            act();
        };

        // txtBind_DragDrop and txtID_DragDrop (FRMCITYC.BAS:744-796): a companion picture ("COMP")
        // from the Companions page puts its number in the box - "txtBind.Text = Source.Index + 1" -
        // and presses Enter on it; anything else is refused with a line. The help: "To Sell or
        // Identify a companion, Drag and Drop the selected companion picture from your Companions
        // tab (Objects & Spells Window) into the Comp. field". The port had nothing to drag as a
        // companion, so only the refusal could ever happen.
        void TakeDrops(TextBox box, Action enter) => Dragging.Target(box, DragKind.Companion, p =>
        {
            box.Text = (p.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            enter();
        }, () => Log("You may only drag and drop companions to the companion store."));   // sub085B, :750, :794

        // ---------------- Bind Companions ----------------
        var txtBind = FormPage.Text(bindBox, ServiceForms.Confinement.txtBind);
        var lblBindCost = FormPage.Label(bindBox, "", ServiceForms.Confinement.lblBindCost, bold: false, boxed: true);
        var cmdBind = FormPage.Access(FormPage.Button(bindBox, "Bind", ServiceForms.Confinement.cmdBind, bold: false), 'B');   // "&Bind"
        cmdBind.IsEnabled = false;

        // txtBind_KeyPress (FRMCITYC.BAS:760-786)
        async void BindEnter()
        {
            int i = SlotOf(txtBind.Text);
            if (i > -1 && Def(_ch.Companions[i]) is Monster def)
            {
                var c = _ch.Companions[i]!;
                if (c.BindingLevel < 0) { await Tell("Bind Companion", Confinement.FreeWillCannotBind); return; }
                if (c.BindingLevel >= Binding.Max) { await Tell("Bind Companion", Confinement.AlreadyBound); return; }
                lblBindCost.Text = Fmt(Binding.Cost(def.GuildLevel, c.BindingLevel));
                txtBind.Text = CompName(i);
                cmdBind.IsEnabled = true;
            }
            else
            {
                lblBindCost.Text = "";
                cmdBind.IsEnabled = false;
            }
        }
        // txtBind_Change (FRMCITYC.BAS:737-742): touching the box takes the offer away again
        txtBind.TextChanged += (_, _) =>
        {
            if (cmdBind.IsEnabled) { cmdBind.IsEnabled = false; lblBindCost.Text = ""; }
        };

        // cmdBind_Click (FRMCITYC.BAS:54-98). It tests the ceiling again but not free will, and it
        // reads the box as it is now.
        cmdBind.Click += async (_, _) =>
        {
            int i = SlotOf(txtBind.Text);
            if (i < 0 || Def(_ch.Companions[i]) is not Monster def) return;
            var c = _ch.Companions[i]!;
            int level = c.BindingLevel;
            if (level >= Binding.Max) { await Tell("Bind Companion", Confinement.AlreadyBound); return; }
            int to = level + Binding.Step > Binding.Max ? Binding.Max : level + Binding.Step;
            decimal cost = Binding.Cost(def.GuildLevel, level);
            if (!await Confirm("Bind Companion", $"Cost to bind companion to a binding level of {to} is {Fmt(cost)}.\nDo you wish to pay?")) return;
            if (!CharPay(Binding.Charge(cost))) { await Tell("Bind Companion", Confinement.NotEnoughGold); return; }
            c.BindingLevel = to;
            // :84-90 only copies the new level into gv1EBC; nothing is repainted, and the port's
            // Misc page reads the companion itself the next time it is painted
            RefreshSummary(); Save();
            txtBind.Text = "";
            lblBindCost.Text = "";
            cmdBind.IsEnabled = false;
        };

        // ---------------- Identify, Realign & Sell ----------------
        var txtID = FormPage.Text(idBox, ServiceForms.Confinement.txtID);
        var lblValueNum = FormPage.Label(idBox, "Value", ServiceForms.Confinement.lblValueNum, align: 1);
        var lblValue = FormPage.Label(idBox, "", ServiceForms.Confinement.lblValue, bold: false, boxed: true);
        var lblIDCost = FormPage.Label(idBox, "", ServiceForms.Confinement.lblIDCost, bold: false, boxed: true);
        var lblIDLevel = FormPage.Label(idBox, "", ServiceForms.Confinement.lblIDLevel, bold: false, align: 1);
        var cmdIDInfo = FormPage.Access(FormPage.Button(idBox, "Info", ServiceForms.Confinement.cmdIDInfo, bold: false), 'n');   // "I&nfo"
        var cmdSell = FormPage.Access(FormPage.Button(idBox, "Sell", ServiceForms.Confinement.cmdSell, bold: false), 'S');   // "&Sell"
        var cmdID = FormPage.Access(FormPage.Button(idBox, "ID", ServiceForms.Confinement.cmdID, bold: false), 'I');   // "&ID"
        var cmdRealign = new[]
        {
            FormPage.Button(idBox, "G", ServiceForms.Confinement.cmdRealign0, bold: false),
            FormPage.Button(idBox, "N", ServiceForms.Confinement.cmdRealign1, bold: false),
            FormPage.Button(idBox, "E", ServiceForms.Confinement.cmdRealign2, bold: false),
        };
        foreach (var b in new[] { cmdIDInfo, cmdSell, cmdID }.Concat(cmdRealign)) b.IsEnabled = false;

        // sub268D (FRMCITYC.BAS:643-652): the panel emptied. The realign buttons are not touched.
        void ClearId()
        {
            txtID.Text = "";
            cmdID.IsEnabled = false;
            cmdIDInfo.IsEnabled = false;
            cmdSell.IsEnabled = false;
            lblValue.Text = "";
            lblIDCost.Text = "";
            lblIDLevel.Text = "";
            lblValueNum.Text = "Value";
        }

        // txtID_KeyPress (FRMCITYC.BAS:804-846)
        async void IdEnter()
        {
            int i = SlotOf(txtID.Text);
            foreach (var b in cmdRealign) b.IsEnabled = false;
            if (i > -1 && Def(_ch.Companions[i]) is Monster def)
            {
                var c = _ch.Companions[i]!;
                if (c.Hits == 0)
                {
                    await Tell("Sorry!", Confinement.NoDeadOnes);
                    ClearId();
                    return;
                }
                lblValue.Text = CharStatFile.FormatNumber(pen.PriceToSell(def, c.Alignment, c.IdLevel));
                lblIDLevel.Text = IdLevelName(c.IdLevel);
                txtID.Text = CompName(i);
                cmdIDInfo.IsEnabled = true;
                cmdSell.IsEnabled = true;
                if (c.IdLevel > 0)
                {
                    cmdID.IsEnabled = true;
                    lblIDCost.Text = CharStatFile.FormatNumber(pen.PriceToIdentify(def, c.Alignment, c.IdLevel));
                    lblValueNum.Text = "? Value";
                }
                else
                {
                    // every alignment but its own; the pen index k is the companion's k (0 G 1 N 2 E)
                    for (int k = 0; k < cmdRealign.Length; k++)
                        if ((int)c.Alignment - 1 != k) cmdRealign[k].IsEnabled = true;
                    // :837 - its tag and its own pen's count, not capped: "(n)2 Value"
                    lblValueNum.Text = gneTags[(int)c.Alignment] + pen.Count(def, c.Alignment) + " Value";
                    cmdID.IsEnabled = false;
                    lblIDCost.Text = "";
                }
                lblIDLevel.Text = IdLevelName(c.IdLevel);
            }
            else ClearId();
        }

        // cmdIDInfo_Click (FRMCITYC.BAS:208-221): the companion in the box, on the Misc page
        cmdIDInfo.Click += (_, _) =>
        {
            int i = SlotOf(txtID.Text);
            if (i > -1 && Def(_ch.Companions[i]) is Monster def) ShowPage(_ch.Companions[i], i, def);
        };

        // cmdID_Click (FRMCITYC.BAS:181-206): pay, one step better known, the page, and Enter again
        cmdID.Click += async (_, _) =>
        {
            int i = SlotOf(txtID.Text);
            if (i < 0 || Def(_ch.Companions[i]) is not Monster def) return;
            var c = _ch.Companions[i]!;
            // fn33E9 divides by the ID level; at nought the original stops with a division by zero
            if (c.IdLevel <= 0) return;
            if (!CharPay(pen.PriceToIdentify(def, c.Alignment, c.IdLevel))) { await Tell("Identify Companion", Confinement.NotEnoughGold); return; }
            c.IdLevel -= 1;
            FileLibraryRecord(def, c.IdLevel);
            ShowPage(c, i, def);
            RefreshSummary(); Save();
            IdEnter();
        };

        // cmdRealign_Click (FRMCITYC.BAS:242-274)
        for (int k = 0; k < cmdRealign.Length; k++)
        {
            var want = (ItemAlignment)(k + 1);
            cmdRealign[k].Click += async (_, _) =>
            {
                int i = SlotOf(txtID.Text);
                if (i < 0 || Def(_ch.Companions[i]) is not Monster def) return;
                var c = _ch.Companions[i]!;
                long cost = Confinement.PriceToRealign(def, c.Alignment, want);
                if (!await Confirm("REALIGN ITEM", Confinement.RealignOffer(cost))) return;
                if (!CharPay(cost)) { await Tell("SORRY!", Confinement.NotEnoughGold); return; }
                c.Alignment = want;
                // :262-269: the Misc page is repainted when it is showing this companion
                if (_confinementInfoSlot == i && _charPanel.Selected == CharTab.Misc) ShowPage(c, i, def, reopen: false);
                RefreshSummary(); Save();
                IdEnter();
            };
        }

        // ---------------- Buy Companions ----------------
        // mc006A, nine rows at the form's 225 twips. The port's rows are the font's line and
        // UiFonts.RowPad, as every list's are (a deviation, docs/FIDELITY.md): as many of them as
        // fit where the form's nine are, beside vsbComps, and fewer for a larger Options font
        double rowPitch = UiFonts.RowHeight(form.Fonts, FormPage.Y(ServiceForms.Confinement.elaComp0.Height),
                                            DesignFonts.PlainSans, DesignFonts.CompCounts);
        int Rows = Math.Clamp((int)(FormPage.Y(ServiceForms.Confinement.vsbComps.Height) / rowPitch), 1, 9);
        var rowName = new TextBlock[Rows];
        var rowInfo = new TextBlock[Rows];
        var rowTag = new string[Rows];
        // elaComp(0..8) and elaCompInfo(0..8): Form_Load loads each row at the one above's Top +
        // Height (FRMCITYC.BAS:498-503), in black on the rows' own light ground as bm9.png has
        // them; the names MS Sans Serif 8.25, the counts Courier New 8.25 bold
        FormRect RowAt(FormRect first, int r) =>
            first with { Top = first.Top + (int)Math.Round(r * rowPitch * FormPage.Twips / form.GrowY), Height = (int)Math.Round(rowPitch * FormPage.Twips / form.GrowY) };
        for (int r = 0; r < Rows; r++)
        {
            rowTag[r] = "";
            rowName[r] = FormPage.Label(buyBox, "", RowAt(ServiceForms.Confinement.elaComp0, r), DesignFonts.PlainSans, Brushes.Black);
            rowInfo[r] = FormPage.Label(buyBox, "", RowAt(ServiceForms.Confinement.elaCompInfo0, r), DesignFonts.CompCounts, Brushes.Black);
            rowName[r].VerticalAlignment = rowInfo[r].VerticalAlignment = VerticalAlignment.Top;
            rowName[r].Padding = rowInfo[r].Padding = new Avalonia.Thickness(0, (UiFonts.RowPad - 1) / 2, 0, 0);
            rowName[r].Background = rowInfo[r].Background = Brushes.White;
            rowName[r].TextWrapping = rowInfo[r].TextWrapping = TextWrapping.NoWrap;
            // the counts set right in their column, CountsPad in, under "G N E" (see countsWidth)
            rowInfo[r].TextAlignment = TextAlignment.Right;
            rowInfo[r].Padding = new Avalonia.Thickness(0, (UiFonts.RowPad - 1) / 2, CountsPad, 0);
            if (rowInfo[r].Width < countsWidth)
            {
                // widened to the left; the name keeps the form's gap before it
                double left = countsRight - countsWidth;
                Canvas.SetLeft(rowInfo[r], left);
                rowInfo[r].Width = countsWidth;
                double nameRoom = left - FormPage.X(ServiceForms.Confinement.elaCompInfo0.Left - ServiceForms.Confinement.elaComp0.Left - ServiceForms.Confinement.elaComp0.Width)
                                  - Canvas.GetLeft(rowName[r]);
                if (rowName[r].Width > nameRoom) rowName[r].Width = Math.Max(0, nameRoom);
            }
        }
        var vsbComps = FormPage.Place(buyBox, new Avalonia.Controls.Primitives.ScrollBar
        {
            Orientation = Orientation.Vertical, AllowAutoHide = false, Minimum = 1, Maximum = 1, Value = 1,
            SmallChange = 1, LargeChange = Rows, ViewportSize = 0,
        }, ServiceForms.Confinement.vsbComps);

        var lblSelectedComp = FormPage.Label(buyBox, "", ServiceForms.Confinement.lblSelectedComp, bold: false, boxed: true);
        string selectedTag = "";                              // lblSelectedComp.Tag
        var lblCost = FormPage.Label(buyBox, "Cost", ServiceForms.Confinement.lblCost, align: 1);
        var lblSelectedCompCost = FormPage.Label(buyBox, "", ServiceForms.Confinement.lblSelectedCompCost, bold: false, boxed: true);
        var cmdBuy = FormPage.Access(FormPage.Button(buyBox, "BUY", ServiceForms.Confinement.cmdBuy, bold: false), 'B');   // "&BUY"
        var cmdInfo = FormPage.Access(FormPage.Button(buyBox, "INFO", ServiceForms.Confinement.cmdInfo, bold: false), 'F');   // "IN&FO"
        cmdBuy.IsEnabled = cmdInfo.IsEnabled = false;
        var txtSearch = FormPage.Text(buyBox, ServiceForms.Confinement.txtSearch);

        // elaAlignments, "Select Alignment" over its three cmdAlign buttons, drawn over the list. Its
        // P?C008 is the Elastic's property 08, Visible: the form data stores it as 0 on this frame
        // (and the Store's), on no other Elastic, and bm9.png has no frame. elaComp_Click shows it
        // only while it waits for an alignment and hides it again after (FRMCITYC.BAS:332, 347,
        // 351); the code reads it as its busy flag too (312, 902). See ShowFrame, below.
        var alignInside = FormPage.Elastic(buyBox, ServiceForms.Confinement.elaAlignments, FormPage.Face);
        var elaAlignments = (Control)alignInside.Parent!;
        elaAlignments.ZIndex = 1;   // over the rows, as the form has it
        elaAlignments.IsVisible = false;
        FormPage.Label(alignInside, "Select Alignment", ServiceForms.Confinement.SelectAlignment, FormPage.Maroon, align: 2);
        var cmdAlign = new[]
        {
            FormPage.Button(alignInside, "G", ServiceForms.Confinement.cmdAlign0, bold: false),
            FormPage.Button(alignInside, "N", ServiceForms.Confinement.cmdAlign1, bold: false),
            FormPage.Button(alignInside, "E", ServiceForms.Confinement.cmdAlign2, bold: false),
        };
        var exitBtn = FormPage.Button(root, "Exit", ServiceForms.Confinement.cmdExit);
        FormPage.Access(exitBtn, 'X');   // "E&xit": Alt+X while this form is the active one (ServiceAccessKey)
        exitBtn.Click += (_, _) => CloseService(null, null!);

        var order = new List<int>();          // m0032: monster indices; grows, never shrinks, while open
        int listed = 0;                       // m0026
        int top = 1;                          // m0030
        int sbValue = 1, sbTag = 2, sbMax = 1;   // vsbComps.Value, .Tag and .Max
        bool sbEnabled = false;
        bool syncing = false;
        int chosenMon = 0;                    // m0028, a monster index
        int chosenAlign = 0;                  // m002C: 0 G, 1 N, 2 E; -1 while one is being picked
        bool choosing = false;                // elaAlignments.P?C008, its Visible
        int lit = -1;                         // m0066
        bool gone = false;
        System.Threading.Tasks.TaskCompletionSource<bool>? picked = null;

        // elaAlignments.P?C008 = shown: the frame up or down, and the flag that beeps at a row
        // click (FRMCITYC.BAS:312) and stops the list redrawing (902) while it is up
        void ShowFrame(bool shown)
        {
            choosing = shown;
            elaAlignments.IsVisible = shown;
        }

        ItemAlignment AlignOf(int a) => (ItemAlignment)(a + 1);
        // gv162E(i).M5766: the pen's monster index while anything is in it, 0 once it has sold out
        int PenMonster(int idx) => idx > 0 && idx < _monsters.Count && pen.AnyOf(_monsters[idx]) ? idx : 0;

        void ShowScrollBar()
        {
            syncing = true;
            vsbComps.Maximum = Math.Max(1, sbMax);
            vsbComps.Value = Math.Clamp(sbValue, 1, Math.Max(1, sbMax));
            vsbComps.IsEnabled = sbEnabled;
            syncing = false;
        }

        // sub336F (FRMCITYC.BAS:725-735): the clicked row goes blue (QBColor 9), the last one black
        IBrush blue = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF));
        void Light(int r)
        {
            if (lit > -1 && lit < Rows) { rowName[lit].Foreground = Brushes.Black; rowInfo[lit].Foreground = Brushes.Black; }
            lit = r;
            rowName[r].Foreground = blue;
            rowInfo[r].Foreground = blue;
        }

        // vsbComps_Change (FRMCITYC.BAS:896-945). It moves m0030 by how far the bar moved since
        // the last time, then fills the nine rows. Rows are only filled up to UBound(m0032), which
        // keeps the entries a shorter list left behind, and an index out of range lands in its
        // error trap part-way down, with the Tag not brought up to date.
        void VsbChange()
        {
            if (choosing) return;
            top += sbValue - sbTag;
            for (int r = 0; r < Rows; r++)
            {
                rowName[r].Foreground = Brushes.Black;
                rowInfo[r].Foreground = Brushes.Black;
                if (r > order.Count - 1) continue;
                int k = top + r;
                if (k < 0 || k >= order.Count || order[k] < 0 || order[k] >= _monsters.Count) return;
                int shown = PenMonster(order[k]);
                var def = _monsters[order[k]];
                if (_monsters[shown].LevelFound == 0) rowInfo[r].Text = "PRICE";
                else rowInfo[r].Text = string.Join(" ", Enumerable.Range(0, 3).Select(a => Math.Min(pen.Count(def, AlignOf(a)), Confinement.ShelfCap)));
                rowName[r].Text = " " + _monsters[shown].Name;
                rowTag[r] = shown.ToString();
                if (shown == chosenMon) Light(r);
            }
            sbTag = sbValue;
        }

        // A move of vsbComps.Value, which runs vsbComps_Change when it is a change
        void SetValue(int v)
        {
            if (v == sbValue) return;
            sbValue = v;
            ShowScrollBar();
            VsbChange();
        }

        // Max can fall below Min, and VB then keeps Value between the two the other way round
        void SetMax(int max)
        {
            sbMax = max;
            int v = Math.Clamp(sbValue, Math.Min(1, max), Math.Max(1, max));
            if (v != sbValue) { sbValue = v; VsbChange(); }
            ShowScrollBar();
        }

        // sub3347 (FRMCITYC.BAS:476-526): every pen with anything in it, in monster order. 0 is a
        // fresh start (opening the window), 1 a rebuild that keeps the list where it was.
        void Rebuild(int p)
        {
            listed = 0;
            if (p == 0) { order.Clear(); order.Add(-1); }
            for (int idx = 0; idx < _monsters.Count; idx++)
            {
                if (PenMonster(idx) <= 0) continue;
                listed++;
                if (listed - 1 > order.Count - 1) order.Add(idx); else order[listed - 1] = idx;
            }
            if (p == 0) top = 1;
            // :506-509, as written: monster 0 is never penned (M5766 > 0 is the test), and an empty
            // list leaves -1 here, so this cannot come true
            if (order.Count == 1 && order[0] == 0) { rowName[0].Text = "NO COMPANIONS!"; rowInfo[0].Text = ""; }
            sbEnabled = order.Count - 1 > Rows - 1;
            if (p == 0)
            {
                sbTag = 2;
                SetMax(listed - (Rows - 1));
            }
            else
            {
                sbTag = sbValue;
                SetMax(listed - (Rows - 1));
                VsbChange();
            }
        }

        vsbComps.PropertyChanged += (_, ev) =>
        {
            if (syncing || ev.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
            int v = (int)Math.Round(vsbComps.Value, MidpointRounding.ToEven);
            if (v == sbValue) return;
            sbValue = v;
            VsbChange();
        };

        void PriceLine(int monIdx, int a)
        {
            var def = _monsters[monIdx];
            int n = Math.Min(pen.Count(def, AlignOf(a)), Confinement.ShelfCap);
            lblCost.Text = n + gneTags[a + 1] + " Cost";
            lblSelectedCompCost.Text = " " + CharStatFile.FormatNumber(ModItemCost(def, AlignOf(a)));
        }

        // elaComp_event0000 (FRMCITYC.BAS:302-370): a click on a row, name or counts alike
        async void ClickRow(int p)
        {
            if (p < 0 || p > listed) p = 0;
            if (choosing || rowName[p].Text is null or "") { Audio.Beep(); return; }
            Light(p);
            chosenMon = (int)VbVal(rowTag[p]);
            int idx = chosenMon;
            if (idx < 0 || idx >= _monsters.Count) return;
            // :319-330: G, N and E (the loop's 0 to 2) enabled where the pen has stock, m002C left at
            // the last of them. :321 makes the buttons themselves visible, inside the frame, which
            // stays hidden unless there is a choice to make: one alignment in stock is taken at
            // once, with none the one chosen before stands (AlignmentChoice)
            var offer = AlignmentChoice.For(Enumerable.Range(0, 3).Select(a => pen.Count(_monsters[idx], AlignOf(a))).ToList());
            for (int a = 0; a < 3; a++) cmdAlign[a].IsEnabled = offer.Enabled[a];
            chosenAlign = offer.Chosen(chosenAlign);
            if (offer.Asks)
            {
                // :331-337: the frame comes up, and nothing else in the window answers until an
                // alignment is picked
                ShowFrame(true);
                if (exitBtn != null) exitBtn.IsEnabled = false;
                cmdBuy.IsEnabled = false;
                cmdInfo.IsEnabled = false;
                vsbComps.IsEnabled = false;
                picked = new System.Threading.Tasks.TaskCompletionSource<bool>();
                await picked.Task;
                if (gone) return;
                // :343 enables the bar whether or not it was enabled before
                sbEnabled = true;
                vsbComps.IsEnabled = true;
                if (exitBtn != null) exitBtn.IsEnabled = true;
                cmdBuy.IsEnabled = true;
                cmdInfo.IsEnabled = true;
                ShowFrame(false);   // :347
            }
            else
            {
                cmdBuy.IsEnabled = true;
                cmdInfo.IsEnabled = true;
                ShowFrame(false);   // :351
            }
            lblSelectedComp.Text = rowName[p].Text;
            selectedTag = rowTag[p];
            if (chosenAlign < 0 || pen.Count(_monsters[chosenMon], AlignOf(chosenAlign)) == 0) return;
            PriceLine(chosenMon, chosenAlign);
        }

        // cmdAlign_Click (FRMCITYC.BAS:50-52)
        for (int a = 0; a < cmdAlign.Length; a++)
        {
            int which = a;
            cmdAlign[a].Click += (_, _) => { chosenAlign = which; picked?.TrySetResult(true); };
        }

        for (int r = 0; r < Rows; r++)
        {
            int row = r;
            // elaCompInfo_event0000 (:372-374) is the same click
            foreach (var cell in new Control[] { rowName[r], rowInfo[r] })
                cell.PointerPressed += (_, ev) =>
                {
                    if (ev.GetCurrentPoint(cell).Properties.IsLeftButtonPressed) ClickRow(row);
                };
        }

        // cmdBuy_Click (FRMCITYC.BAS:100-173)
        cmdBuy.Click += async (_, _) =>
        {
            // the first slot whose Ref is -1 (FRMCITYC.BAS:111-116): a dead companion keeps its slot
            if (_ch.Companions.FirstFree < 0) { await Tell("Buy Companion", Confinement.NoRoom); return; }
            int idx = (int)VbVal(selectedTag);
            if (idx < 0 || idx >= _monsters.Count || chosenAlign < 0) return;
            var def = _monsters[idx];
            var align = AlignOf(chosenAlign);
            if (!CharPay(ModItemCost(def, align))) { await Tell("Buy Companion", Confinement.NotEnoughGold); return; }
            // into the first hole, l00B2 (FRMCITYC.BAS:111-116)
            int slot = _ch.Companions.Add(new Companion
            {
                Ref = idx, MonsterId = def.Id,   // Ref = l00B4, M57D4 = Monsters(l00B4).Id (FRMCITYC.BAS:128-129)
                Name = "", Hits = def.Hits, MaxHits = def.Hits,
                Alignment = align, IdLevel = 0, BindingLevel = Confinement.BoughtBinding(def),
            });
            // :140 - a creature from floor 0 never runs out
            if (def.LevelFound > 0) pen.Take(def, align);
            pen.Save(SavesDir);
            Rebuild(1);
            if (pen.Count(def, align) < 1)
            {
                lblSelectedComp.Text = "";
                selectedTag = "";
                lblCost.Text = "Cost";
                lblSelectedCompCost.Text = "";
                cmdBuy.IsEnabled = false;
                cmdInfo.IsEnabled = false;
            }
            else
            {
                // :163 counts m0028's pen, the row clicked, and prices l00B4, the one bought
                int n = Math.Min(pen.Count(_monsters[chosenMon], align), Confinement.ShelfCap);
                lblCost.Text = n + gneTags[chosenAlign + 1] + " Cost";
                lblSelectedCompCost.Text = " " + CharStatFile.FormatNumber(ModItemCost(def, align));
            }
            RefreshSummary(); Save();
            Log($"Companion #{slot + 1} is now your {def.Name}.");   // sub085B, :172
        };

        // cmdInfo_Click (FRMCITYC.BAS:223-240): the creature as the pen has it, fully known, unbound,
        // at the alignment picked
        cmdInfo.Click += (_, _) =>
        {
            int idx = (int)VbVal(selectedTag);
            if (idx < 0 || idx >= _monsters.Count) return;
            ShowPage(null, -1, _monsters[idx], (Alignment)Math.Max(0, chosenAlign));
        };

        // cmdSell_Click (FRMCITYC.BAS:276-300). No box, and no dead test of its own: it sells what the
        // Comp. box names now, whether or not Enter was pressed on it.
        cmdSell.Click += (_, _) =>
        {
            int i = SlotOf(txtID.Text);
            if (i > -1 && Def(_ch.Companions[i]) is Monster def)
            {
                var c = _ch.Companions[i]!;
                // FRMCITYC.BAS:284 has no handler: an Overflow would end the original program unsaved, so
                // the port sells nothing
                // a mod may have the town put the gold in a bank account, the seller's or another
                // member's (ModDecisions.TownIncomeBank)
                long sale = pen.PriceToSell(def, c.Alignment, c.IdLevel);
                var bank = Mordor.Modding.ModHost.Active.TownIncomeBank(_party, _ch, sale, Mordor.Modding.TownIncomeKind.CompanionSale);
                if (!(bank ?? _ch).Receive(sale, bank != null)) return;
                // :285-292 - not a floor-0 creature, and not onto a pen already holding nine
                pen.Add(def, c.Alignment);
                // Ref = -1 (FRMCITYC.BAS:293): the slot is emptied and the others stay where they
                // are. M5943 is not touched, so selling the companion you carry leaves the carry
                // pointing at the hole
                _ch.Companions.Release(i);
                _ch.Companions.BlankEmptyNames();   // then mdrcharShowCompanions Me (FRMCITYC.BAS:294)
                RefreshSummary(); Save();
                ClearId();
            }
            pen.Save(SavesDir);
            Rebuild(1);
        };

        OnEnter(txtBind, BindEnter);
        OnEnter(txtID, IdEnter);
        TakeDrops(txtBind, BindEnter);
        TakeDrops(txtID, IdEnter);

        // txtSearch_KeyPress (FRMCITYC.BAS:854-894): a prefix of a creature's name, any case; an
        // exact name wins outright. It only scrolls the one it finds to the top.
        OnEnter(txtSearch, async () =>
        {
            string typed = txtSearch.Text ?? "";
            int found = 0, at = 0;
            for (int i = 0; i < listed && i < order.Count; i++)
            {
                if (order[i] < 0) break;
                string name = _monsters[PenMonster(order[i])].Name;
                if (name.Length < typed.Length || !name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, typed, StringComparison.OrdinalIgnoreCase)) { at = i; found = 1; break; }
                found++;
                at = i;
            }
            if (found != 1)
            {
                // sub07E2 .5#: the answer sits in the box for half a second, then your text comes back
                txtSearch.Text = found == 0 ? "Companion Not Found!" : "Name is ambiguous!";
                await System.Threading.Tasks.Task.Delay(500);
                txtSearch.Text = typed;
                return;
            }
            SetValue(at + 1 > sbMax ? sbMax : at + 1);
            txtSearch.Text = "";
        });

        Rebuild(0);
        // Form_Load itself never fills the rows. sub3347 0 sets the Tag to "2" and m0030 to 1, which
        // only lands on the first creature if a Change arrives with Value 1 - as setting Min and
        // Max on the bar would make it - so the port runs that one Change here.
        VsbChange();

        // lblCall_Change (FRMCITYC.BAS:453-474): the party switched character while the window is up
        _confinementUpdate = () =>
        {
            txtID.Text = "";
            lblValue.Text = "";
            lblValueNum.Text = "Value";
            lblIDCost.Text = "";
            lblIDLevel.Text = "";
            cmdIDInfo.IsEnabled = false;
            cmdSell.IsEnabled = false;
            cmdID.IsEnabled = false;
            cmdBind.IsEnabled = false;
            txtBind.Text = "";
            lblBindCost.Text = "";
            VsbChange();
            // :468-470, as written: with the bar disabled (nine creatures or fewer) the frame is
            // shown, and with no wait running nothing hides it again: its buttons still set m002C,
            // but every row click beeps until the window is shut
            if (!vsbComps.IsEnabled) ShowFrame(true);
        };

        // leaving any other way while an alignment is being picked must not leave EXIT dead, and
        // ends the wait the way the form's unloading does (fn03BD)
        var update = _confinementUpdate;
        form.Page.DetachedFromVisualTree += (_, _) =>
        {
            gone = true;
            if (_confinementUpdate == update) _confinementUpdate = null;
            picked?.TrySetResult(false);
            if (exitBtn != null) exitBtn.IsEnabled = true;
        };
        ShowForm(form);
    }

    // ---------------- Library ----------------

    /// <summary>
    /// The Library, <c>fn1BBE</c> (MODULE11.BAS:78-406). Not a table of numbers - the original
    /// writes a page of prose about each creature and item, and it tells you steadily more as the
    /// world learns more. Four grades for a creature, two for an item; see <see cref="Librarian"/>.
    ///
    /// The records are shared between every character, which is what a library is for. The port had
    /// shown the current character's own tally instead.
    /// </summary>
    private void ShowLibrary(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // over the Guild, modally (FRMCTYG.BAS:170-172): closing the Library returns to it
        if (_serviceWhich == GameWindow.Guild) _serviceReturn = () => ShowGuild(null, null!);
        OpenService("The Library");
        // the whole monster table, one row per index as sub327E loops it (FRMLIBRA.BAS:64-69): the
        // Morgul and the Shadow Ninja share Id 307 and have a record each
        var monsters = _monsters;
        var spells = MdrLoader.LoadSpells(System.IO.Path.Combine(_gameDir, "MDATA2.MDR"));
        Librarian.SpellNameOf = i => i >= 0 && i < spells.Count ? spells[i].Name.Trim() : "a spell";

        int today = Librarian.Serial(DateTime.Now);
        // The Library is read as it stands: frmLibrary writes nothing (FRMLIBRA.BAS). Its records
        // are written where the original writes them - sub06FE in the dungeon (Librarian.FileSighting)
        // and for a companion, sub0730 for an item. The port had folded each member's own tally in
        // here instead, which stamped every creature "level 1" with the date of the first visit.
        var store = Librarian.Store.Load(SavesDir, _monsters);

        // sub327E (FRMLIBRA.BAS:57-88) fills one list, not two: the radio pair decides whether it
        // holds creatures or items, and the picture box is only on show for creatures. The names
        // go in with nothing added to them - the port had written "(level 7)" after each, and had
        // put both lists on screen at once. lstList is a Sorted ListBox in the form data, so they
        // are shown alphabetically whatever order sub327E adds them in.
        var monRows = LibrarySearch.Sorted(monsters.Where(m => store.KnowsMonster(m.Ref)), m => m.Name.Trim());
        var itemRows = LibrarySearch.Sorted(_items.Where(i => i.Id != 0 && store.KnowsItem(i.Id))
                                                  .GroupBy(i => i.Id).Select(g => g.First()), i => i.Name.Trim());

        // frmLibrary's form data (MORDOR.EXE RCDATA 56): a 7710x4410-twip client, every control in
        // elaElastic(0) at -15,-15 (ServiceForms.Library has the rectangles). No control has a font
        // of its own, so all are the form's MS Sans Serif 8.25 bold, sized (never restyled) by the
        // Options fonts: its Form_Load runs sub0802 alone (FRMLIBRA.BAS:31)
        var form = new FormPage(ServiceForms.Library.ClientW, ServiceForms.Library.ClientH, ServiceForms.Library.Elastic, FontPass.SizeOnly);
        var root = form.Root;
        FormPage.Label(root, "Browse", ServiceForms.Library.BrowseCaption, FormPage.Navy);          // Label1

        // elaElastic(1) holds the radio pair: optOption(1) "Monsters", Value True, over optOption(0) "Items"
        var options = FormPage.Elastic(root, ServiceForms.Library.OptionsBox);
        var monOpt = FormPage.Option(options, "Monsters", ServiceForms.Library.optMonsters, "library", true);
        var itemOpt = FormPage.Option(options, "Items", ServiceForms.Library.optItems, "library", false);

        // picMonster, 900 twips square - the sheet's 60-pixel cell - with no border of its own. It is
        // drawn stretched to the box, or dropped in the middle at its own size when "No StretchBlt"
        // is on - the same pair of calls everything else in the game gets (FRMLIBRA.BAS:106-118).
        var pic = new Image
        {
            Stretch = _options.NoStretch ? Stretch.None : Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        // every picture in this game is a 1990s bitmap; smoothing it is not kindness
        Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(pic, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        var picBox = FormPage.Place(root, new Border { Background = FormPage.Face, ClipToBounds = true, Child = pic },
                                    ServiceForms.Library.picMonster);

        var list = FormPage.List(root, ServiceForms.Library.lstList, bold: true);
        // txtInfo: a MultiLine TextBox with a vertical scroll bar whose keys are all thrown away
        // (FRMLIBRA.BAS:122-128), so it is read, not typed into
        var info = UiFonts.Set(new TextBox
        {
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
            FontWeight = FontWeight.Bold, Padding = new Avalonia.Thickness(3, 1),
            MinHeight = 0, MinWidth = 0,
            Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x10)), Foreground = Brushes.Gainsboro,
        }, form.Fonts, DesignFont.Default);
        info.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.ScrollBar>(x)) { Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.ScrollBar.AllowAutoHideProperty, false) } });
        ScrollViewer.SetVerticalScrollBarVisibility(info, Avalonia.Controls.Primitives.ScrollBarVisibility.Visible);
        // the theme lays the scroll bar over the text; the text is kept clear of it, as VB's is
        info.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Presenters.TextPresenter>(x))
        {
            Setters = { new Avalonia.Styling.Setter(Layoutable.MarginProperty, new Avalonia.Thickness(0, 0, 17, 0)) },
        });
        FormPage.Place(root, info, ServiceForms.Library.txtInfo);
        FormPage.Label(root, "Search For:", ServiceForms.Library.SearchCaption);                    // lblLabel
        var search = FormPage.Text(root, ServiceForms.Library.txtSearch, bold: true);
        // lblEntries: "There are N Monster Entries." / "There are N Item Entries." - fn1BD5 reads
        // the running total the library file keeps in records 4 and 5 (HALL.BAS:412-425), which is
        // the number of things anybody has ever had written up, and so the length of this list.
        // Navy, set right, ending just short of Exit.
        var entries = FormPage.Label(root, "", ServiceForms.Library.lblEntries, FormPage.Navy, align: 1);
        // cmdCommand(0), "Exit" with no access key; its Cancel is True, so Escape presses it
        var exit = FormPage.Button(root, "Exit", ServiceForms.Library.cmdExit);

        bool showingMonsters = true;     // the branch sub327E tests first, and the only one with a picture
        List<string> shown = new();

        void Fill()
        {
            picBox.IsVisible = showingMonsters;
            pic.Source = null;
            info.Text = "";
            int n = showingMonsters ? monRows.Count : itemRows.Count;
            entries.Text = $"There are {n} {(showingMonsters ? "Monster" : "Item")} Entries.";
            shown = showingMonsters ? monRows.Select(m => m.Name.Trim()).ToList()
                                    : itemRows.Select(i => i.Name.Trim()).ToList();
            if (n == 0)
            {
                // "<NO ENTRIES>", and the list goes dead (FRMLIBRA.BAS:81-83)
                shown = new List<string> { "<NO ENTRIES>" };
                list.ItemsSource = shown;
                list.IsEnabled = false;
                return;
            }
            list.ItemsSource = shown;
            list.IsEnabled = true;
            list.SelectedIndex = 0;      // lstList.ListIndex = False, which fires lstList_Click
        }

        list.SelectionChanged += (_, _) =>
        {
            int i = list.SelectedIndex;
            if (i < 0 || !list.IsEnabled) return;
            if (showingMonsters)
            {
                if (i >= monRows.Count) return;
                var m = monRows[i];
                info.Text = Librarian.DescribeMonster(m, store.ForMonster(m.Ref), today, Plural(m.Name));
                var (picture, size) = MonsterPortrait.ForLibrary(m);
                pic.Source = picture;
                double box = Math.Min(form.PixelsX(ServiceForms.Library.picMonster.Width), form.PixelsY(ServiceForms.Library.picMonster.Height));
                pic.Width = _options.NoStretch ? size : box;
                pic.Height = _options.NoStretch ? size : box;
            }
            else
            {
                if (i >= itemRows.Count) return;
                var it = itemRows[i];
                info.Text = Librarian.DescribeItem(it, store.ForItem(it.Id), today,
                    id => Librarian.FinderName(monsters, id));
            }
            info.CaretIndex = 0;
            list.ScrollIntoView(i);
        };

        // optOption_Click: sub327E, and the focus to the list if it has anything (FRMLIBRA.BAS:90-95)
        void Chose(bool monstersNow)
        {
            showingMonsters = monstersNow;
            Fill();
            if (list.IsEnabled) list.Focus();
        }
        monOpt.IsCheckedChanged += (_, _) => { if (monOpt.IsChecked == true && !showingMonsters) Chose(true); };
        itemOpt.IsCheckedChanged += (_, _) => { if (itemOpt.IsChecked == true && showingMonsters) Chose(false); };

        // txtSearch_KeyDown and _KeyPress (FRMLIBRA.BAS:130-162): Enter looks the typed start up in
        // the list now shown, and the Enter itself goes no further
        search.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, async (_, k) =>
        {
            if (k.Key != Avalonia.Input.Key.Enter && k.Key != Avalonia.Input.Key.Return) return;
            k.Handled = true;
            var (outcome, index) = LibrarySearch.Find(shown, search.Text ?? "");
            if (outcome == LibrarySearch.Outcome.Selected)
            {
                list.SelectedIndex = index;
                search.Text = "";
                return;
            }
            await Tell(LibrarySearch.Title, outcome == LibrarySearch.Outcome.NoMatch ? LibrarySearch.NoMatch : LibrarySearch.Ambiguous);
            search.Focus();
            search.SelectAll();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // cmdCommand_Click: Unload Me (FRMLIBRA.BAS:17-24) - back to the Guild it was opened over
        exit.Click += (_, _) => CloseService(null, null!);
        // Exit is the form's Cancel button (14 FF): Escape presses it while the Library is the
        // active window, wherever the focus is - the window's key preview hands it here first, as
        // it does the Guild's (PreviewGameKey, _serviceKeys) - and from the Search box too, which
        // the preview leaves to itself and which does not take an Escape, so it comes up to the page
        Func<Avalonia.Input.Key, bool> keys = k =>
        {
            if (k != Avalonia.Input.Key.Escape) return false;
            exit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            return true;
        };
        _serviceKeys = keys;
        var keysIn = _svc;
        form.Page.DetachedFromVisualTree += (_, _) => { if (keysIn.Keys == keys) keysIn.Keys = null; };
        form.Page.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, k) =>
        {
            if (k.Key != Avalonia.Input.Key.Escape || k.KeyModifiers != Avalonia.Input.KeyModifiers.None) return;
            k.Handled = true;
            keys(k.Key);
        });

        Fill();
        ShowForm(form);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (list.IsEnabled) list.Focus(); else search.Focus(); },
                                                    Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// The original speaks of creatures in the plural throughout the Library - "Dwarven Lords are
    /// Small sized Humanoids". It builds that with <c>fn0E23</c>, which knows the irregular cases;
    /// this does the ordinary ones and leaves anything already plural alone.
    /// </summary>
    private static string Plural(string name)
    {
        string n = name.TrimEnd();
        if (n.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return n;
        if (n.EndsWith("y", StringComparison.OrdinalIgnoreCase) && n.Length > 1
            && !"aeiou".Contains(char.ToLower(n[^2]))) return n[..^1] + "ies";
        if (n.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("x", StringComparison.OrdinalIgnoreCase)) return n + "es";
        return n + "s";
    }
}
