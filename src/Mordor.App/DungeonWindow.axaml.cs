using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Mordor.Core;

namespace Mordor.App;

public partial class DungeonWindow : Window
{
    private readonly Party _party;

    /// <summary>
    /// The character your commands are for. From the help, "Parties": "Once the character is
    /// selected, the interaction with the game is the exact same as if the character was running
    /// by themselves" - so everything below reads this and needs no other special case.
    /// </summary>
    private Character _ch => _party.Selected;

    private readonly List<Floor> _dungeon;
    private readonly List<Monster> _monsters;
    private readonly List<Spell> _spells;
    private readonly List<Item> _items;
    private readonly Dictionary<short, Item> _itemsById;
    private readonly Dictionary<int, DungeonNav> _navs = new(); // per-level navigators (keep explored maps)
    private readonly Random _rng = new();
    private DungeonNav _nav;
    private readonly SharedMap _map;           // the automap is shared by every character, as in the original
    private readonly DungeonState _dstate;     // which rooms still hold monsters, also shared
    private int _fightingArea;                 // the room whose monsters we are fighting
    /// <summary>gv1D68: the room whose records are loaded, and the records as they now stand.</summary>
    private (int Level, int Area, Encounter Records)? _room;
    /// <summary>gv1D66 and gv1D6C: the area and level sub0A91 last ran for.</summary>
    private (int Level, int Area) _loadedArea = (-1, -1);

    /// <summary>
    /// Monsters standing on this square that have not attacked yet. The original keeps them in
    /// Foes() with M4ECC unset and rolls every second whether they engage; until then you can walk
    /// away and nothing happens.
    /// </summary>
    private List<CombatMonster>? _waiting;
    private bool _waitingIsLair;
    /// <summary>Set while the Continue button is standing in for the original's "Wait for Rescue".</summary>
    private bool _awaitingRescue;

    /// <summary>Where the monsters are, so walking off the square leaves them behind.</summary>
    private (int Level, int X, int Y) _encounterAt = (-1, -1, -1);

    /// <summary>
    /// The original's timCombat: one tick a second, enabled the whole time you are below ground
    /// (MAZEMOD.BAS:739 sets Interval = 1000, FRMDUNM.BAS:616 is the handler). Combat is not
    /// turn-based; this is what makes it happen while you walk.
    /// </summary>
    private Avalonia.Threading.DispatcherTimer? _clock;

    /// <summary>The original's m000C / m0024 re-entrancy guards on the timer.</summary>
    private bool _inRound;
    /// <summary>Set while a paced round is still drawing; PlayRound releases the guard when it ends.</summary>
    private bool _keepRoundGuard;

    /// <summary>
    /// timAilment, the dungeon form's other timer. Four seconds, from FRMDUNM.FRM - the interval
    /// is a design-time property, so it is in the form binary rather than the code.
    /// </summary>
    private Avalonia.Threading.DispatcherTimer? _ailmentClock;

    /// <summary>The original's m0026 guard on that timer.</summary>
    private bool _inAilment;
    private string _cured = "";        // cure messages from the ailment tick, shown with the step
    private int _level; // 1-based
    private CombatEngine? _combat;
    private readonly string _gameDir;

    /// <summary>mc01B0: the original's combat timer interval, in milliseconds.</summary>
    private const int CombatTickMs = 1000;

    /// <summary>timAilment.Interval from FRMDUNM.FRM, in milliseconds.</summary>
    private const int AilmentTickMs = 4000;

    /// <summary>
    /// picMaze2, the view: 1515x1515 twips in FRMDUNM.FRM, 101 pixels square. The picture is drawn
    /// with <see cref="ViewDensity"/> pixels to each of those, so it stays sharp as the Dungeon
    /// window scales it up.
    /// </summary>
    private const double W = 101, H = 101;
    private const int ViewDensity = 4;
    private const int MaxDepth = 5;
    private static readonly double[] K = { 0.0, 0.20, 0.32, 0.40, 0.45, 0.48 };

    public DungeonWindow(Character ch, List<Floor> dungeon, string gameDir, int level, int x, int y)
        : this(new Party(ch), dungeon, gameDir, level, x, y) { }

    public DungeonWindow(Party party, List<Floor> dungeon, string gameDir, int level, int x, int y)
    {
        _party = party; _dungeon = dungeon; _gameDir = gameDir; _level = Math.Clamp(level, 1, dungeon.Count);
        // the Options window's fonts, read before any control is built (UiFonts)
        UiFonts.Use(GameOptions.Load(gameDir));
        InitializeComponent();
        // the frame's caption, " (Limited Version)" after it in the shareware build (FRMMAIN.BAS:148-152)
        Title = LimitedVersion.FrameCaption;
        // MDIForm_QueryUnload (FRMMAIN.BAS:167-175): a close the user asks for - the X, Alt+F4, the
        // system menu - is refused with "Please use the Mordor EXIT buttons to leave the game.";
        // the game's own Close() calls (Exit, the stairs out, death) are programmatic and go through
        // Any other close - Windows ending the session - is the rest of MDIForm_QueryUnload
        // (FRMMAIN.BAS:177-191): "Exit Mordor?" with no monsters in the area, dead, or at level 0,
        // and Yes runs sub16CC; otherwise a Beep and "Cannot exit while monsters are in the area!"
        _queryEnd = async () =>
        {
            if (MonstersHere && !_ch.IsDead && _level > 0)
            {
                Audio.Beep();
                Say("Cannot exit while monsters are in the area!");
                return false;
            }
            if (!await Confirm("Exit Mordor?", "EXIT")) return false;
            FinishWonFight();
            SaveChar();
            await PartyUnload.Run(DialogOwner, _party, SavesDir, Say);
            return true;
        };
        OriginalBox.RefuseCloseBox(this, _queryEnd);
        // shown on its own (the smoke test), the window's own Opened and Closed start and end it;
        // inside the city's frame, RunInside and Close do
        Opened += (_, _) => Begun?.Invoke();
        Closed += (_, _) => EndOnce();
        // sub16F8 runs from the idle loop; a quarter second is as near as a timer need come
        var playClock = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        playClock.Tick += (_, _) => PlayClock.Shared.Tick(_party.Members, PlayClock.Timer(DateTime.Now));
        playClock.Start();
        Ended += () => playClock.Stop();
        _monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(gameDir, "MDATA5.MDR"));
        _spells = MdrLoader.LoadSpells(System.IO.Path.Combine(gameDir, "MDATA2.MDR"));
        foreach (var m in _party.Members) m.RecalcSpellPoints();
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(gameDir, "MDATA3.MDR"));
        _items = items;
        _itemsById = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        // the mods' view of the game (ctx.Game): the city has set it, unless the dungeon was started
        // on its own (MORDOR_TEST=dungeon)
        if (Mordor.Modding.ModHost.Active.GameParty == null) Mordor.Modding.ModHost.Active.SetGame(_party, _spells, Lookup);

        // After the tables are loaded, never before: the character window is handed the spell
        // list at construction and keeps it, so building the frame first gave it a null one - a
        // buffer holding a spell then threw the moment the Buffers page drew it.
        // and the same moment is where pre-SlotCursed saves get their worn gear reflagged
        foreach (var m in _party.Members) m.SyncSlotCurses(Lookup);
        // and companions the port once charmed or took on without an alignment get the creature's own
        foreach (var m in _party.Members) m.Companions.Settle(_monsters);
        BuildMdi();
        // txtSpell_KeyDown (FRMCHAR3.BAS:851-861): Enter casts, Escape hands the focus back. Taken
        // on the way down, on the box itself, so nothing between it and the window can swallow it.
        SheetSpellText.AddHandler(KeyDownEvent, (_, k) =>
        {
            if (k.Key is Key.Enter or Key.Return) { k.Handled = true; CastTyped(); }
            else if (k.Key == Key.Escape) { k.Handled = true; Focus(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // KeyPreview. Every form's Form_KeyDown takes the key before its controls do and hands it
        // to fn1521 or the key queue (FRMCHAR3.BAS:310-331, FRMCHAR4.BAS:17-32, FRMGROUP.BAS:51-66,
        // FRMCHAR1.BAS:39-53, FRMAUTOM.BAS:55-72, FRMDUNM.BAS:201-220); only a text box, a combo
        // box and - in Objects & Spells and the automap - a scroll bar keep their keys. So every
        // other control - the character window's tabs once clicked, a button, a list - is passed
        // over on the way down and the game has the key first: Left and Right turn the party
        // rather than change a tab. The pack, the spell rows and the companions are labels and
        // buttons there, and the Messages list is previewed like the rest; here those are
        // ListBoxes, which would move their selection instead, so a key meant for one is the
        // game's alone. A key the game does not use goes on to the control, as sub0456's queue
        // hands back what it does not take.
        // On Root rather than the window, so that it still sees the keys once Root is in the
        // city's frame.
        Root.AddHandler(KeyDownEvent, (_, k) =>
        {
            if (k.Handled || FocusedElement() is not Visual f) return;
            if (OriginalBox.IsCloseChord(k)) return;
            if (k.Key == Key.Tab) return;   // Tab and Shift+Tab still move the focus on, out of the list
            if (KeyOwner.KeepsKeys(f)) return;   // an open combo box's list too
            if (ReferenceEquals(f, this) || ReferenceEquals(f, _outer)) return;   // the frame's own OnKeyDown has it
            OnKeyDown(k);
            if (f is ListBox || f.FindAncestorOfType<ListBox>() != null) k.Handled = true;
            _previewed = k;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _map = SharedMap.Load(SavesDir);
        _dstate = DungeonState.Load(SavesDir);
        // a dungeon.json from before the record existed: seed it from the shared map, or every
        // level an established game next visits would log "was the first to go to level N"
        if (_dstate.DeepestReached == 0) _dstate.DeepestReached = _map.DeepestVisited;
        Audio.Init(gameDir);
        Audio.Music("DUNGEON");
        _nav = NavFor(_level);
        _nav.SetPosition(x, y);
        // the way the record faces (Chars(Me).Facing, MAIN.TXT:329); the walk down from town sets
        // North first (FRMCTYM.BAS:211), and so does a record at level 0 here
        int facing = _ch.DungeonLevel >= 1 && _ch.Facing is >= 1 and <= 4 ? _ch.Facing : 1;
        while ((int)_nav.Facing != facing - 1) _nav.TurnRight();
        Focusable = true;
        BuildBufferButtons();
        _clock = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CombatTickMs) };
        _clock.Tick += (_, _) => CombatTick();
        _ailmentClock = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(AilmentTickMs) };
        _ailmentClock.Tick += (_, _) => AilmentTick();

        Begun += () =>
        {
            Focus();
            MsgText.Text = _nav.Describe();   // the square's own label; "You enter the dungeon." was the port's
            // Form_Load: M59BB for the square, then sub09CC (FRMDUNM.BAS:278-283), before sub0835
            SyncAreas();
            WhoElseIsHere();
            DeliverCarried(_ch);   // then sub0A7C for the body Me carries (FRMDUNM.BAS:284-285)
            Redraw();
            // FRMDUNM.BAS:303-304: sub0835, then sub0A91 for the room you start in - and no round
            LoadArea(arrivalRound: false);
            // MORDOR_TEST_INFO=companion: the first companion's page, as its Info would give it
            if (Environment.GetEnvironmentVariable("MORDOR_TEST_INFO") == "companion" && _ch.Companions[0] is Companion pet
                && MonsterOf(pet) is Monster petDef)
            {
                CompanionInfo.Sharpen(_ch, pet, petDef, Lookup);
                _charPanel.ShowMiscPage(CompanionInfo.Paint(pet, 0, petDef, _ch, Lookup), MonsterPortrait.Of(petDef), _options.NoStretch);
                _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
                _characterChild.Activate();
            }
            // test hook: put a group on the square we start on, so the monster strip can be seen
            // without wandering until the dungeon obliges
            if (Environment.GetEnvironmentVariable("MORDOR_TEST_FIGHT") == "1")
            {
                // (square-less: every class admitted, no lair - a test hook, not a game path)
                List<CombatMonster> g = EncounterGenerator.Generate(_monsters, _level, _rng, null, -1, -1, _ch, _party.Leader, Lookup);
                // MORDOR_TEST_CHEST: the creature on this level likeliest to leave a chest, by
                // its own BoxChance, so the chest can be watched without waiting on the dice
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_CHEST") == "1")
                {
                    var boxer = _monsters
                        .Where(m => m.Id != 0 && m.LevelFound <= _level && m.BoxChance.Length > 2)
                        .OrderByDescending(m => Math.Max(m.BoxChance[1], m.BoxChance[2]))
                        .ThenBy(m => m.GuildLevel).FirstOrDefault();
                    // entered at L1F688 with the boxer as lead, the way a lair monster is
                    if (boxer != null && EncounterGenerator.Generate(_monsters, _level, _rng, null, -1, -1, _ch, _party.Leader, Lookup,
                                                                     forceLead: _monsters.IndexOf(boxer)) is { Count: > 0 } chesty)
                        g = chesty;
                    // and a tester who lives long enough to open it, and who can see a chest across a
                    // fight at all - monShowChest hides it from low DEX and a poor disarm chance
                    _ch.MaxHits = _ch.CurHits = 900;
                    _ch.Stats[(int)Stat.Dex] = 18;
                    if (_ch.Guilds.All(g => g.Guild != "Thief")) _ch.Guilds.Insert(0, new GuildMembership { Guild = "Thief", Level = 20 });
                }
                if (g.Count > 0)
                {
                    _waiting = g;
                    _encounterAt = (_level, _nav.X, _nav.Y);
                    // a friendly group with an offer standing, to exercise the j key
                    if (Environment.GetEnvironmentVariable("MORDOR_TEST_JOIN") == "1")
                    {
                        foreach (var m in g) m.Hostile = false;
                        g[0].JoinOffer = Math.Min(g.Count, 2);
                    }
                    ShowWaiting(g);
                }
            }
            // test hook: MORDOR_TEST_GROUPS=N (2 to 4) puts N groups of two, each a different
            // creature, on the starting square, to see monShowMonsters' portrait strip; "3i" makes
            // the last of them an invisible creature, which the strip leaves blank. Anything else
            // - a count out of range, a bad suffix - is ignored, as is a table with too few creatures.
            if (Environment.GetEnvironmentVariable("MORDOR_TEST_GROUPS") is string groupsWanted && groupsWanted.Length > 0)
            {
                bool lastInvisible = groupsWanted.EndsWith('i');
                string digits = lastInvisible ? groupsWanted[..^1] : groupsWanted;
                if (int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int groupCount)
                    && groupCount is >= 2 and <= 4)
                {
                    var kinds = _monsters.Where(m => m.Id != 0 && m.Hits > 0 && m.PicId > 0 && !Asmodeus.Is(m)
                                                     && !MonsterProperty.Has(m, MonsterProperty.Invisible))
                                         .OrderBy(m => m.GuildLevel).Take(groupCount).ToList();
                    if (lastInvisible && kinds.Count > 0
                        && _monsters.FirstOrDefault(m => m.Id != 0 && m.Hits > 0 && MonsterProperty.Has(m, MonsterProperty.Invisible)) is Monster ghost)
                        kinds[^1] = ghost;
                    if (kinds.Count == groupCount)
                    {
                        var g = new List<CombatMonster>();
                        for (int n = 0; n < kinds.Count; n++)
                            for (int k = 0; k < 2; k++) g.Add(new CombatMonster { Def = kinds[n], Hits = kinds[n].Hits, MaxHits = kinds[n].Hits, Group = n });
                        _waiting = g;
                        _encounterAt = (_level, _nav.X, _nav.Y);
                        // a tester who outlives the look and can kill a group to watch the strip
                        // close up - but only a throwaway one: a Tester kept between runs
                        // (MORDOR_TEST_KEEP) is saved as it stands, so it is left as it is rather
                        // than having 30000 hits and a level 300 guild written into it, and so is any
                        // character outside the MORDOR_TEST=dungeon run that makes one afresh
                        if (Environment.GetEnvironmentVariable("MORDOR_TEST") == "dungeon" && Environment.GetEnvironmentVariable("MORDOR_TEST_KEEP") != "1")
                        {
                            _ch.MaxHits = _ch.CurHits = 30000;
                            if (_ch.Guilds.All(m => m.Guild != "Ninja"))
                                _ch.Guilds.Insert(0, new GuildMembership { Guild = "Ninja", Level = 300 });
                            _ch.Stats = new[] { 30, 30, 30, 30, 30, 30 };
                            _ch.RecalcSwings();
                        }
                        ShowWaiting(g);
                    }
                }
            }
            // sub14E0 loads frmCharStat3 with frmCharStat1 and frmCharStat4 (MAINMOD2.BAS:3160-3162),
            // and an MDI child is shown as it is loaded, in the state its Form_Load reads from
            // M5958 (FRMCHAR3.BAS:401). So Objects & Spells is up from the start, as it is in town;
            // the port had kept it shut until i, Take or Spell.
            ShowSheet();
            ApplyWindowLayout();
            OpenAutomapIfWanted();
            // and one above the other in the order they come up: the three loaded, the party
            // shown, the automap, then frmDungeonMain itself (MAINMOD2.BAS:3160-3175)
            foreach (var c in new[] { _characterChild, _sheetChild, _messagesChild, _partyChild, _automapChild, _dungeonChild })
                Host.BringToFront(c);
            // sub14E0 for a character loaded dead below ground: frmDungeonMain shown as for anyone,
            // and then "If Chars(Me).DeathState <> False Then sub0930 Me" (MAINMOD2.BAS:3181-3182) -
            // the dead screen in the dungeon window, its square readout ("(You are Dead)",
            // MAZEMOD.BAS:310) and buttons kept, as when one dies there. The port had given them the
            // city's dead screen instead (observed against the original 2026-10-26)
            if (_ch.IsDead) Sub0930(_ch);
            // a mod's entry hook on a walk down from town comes here, before the clocks
            // (DungeonWindow.Mods.cs); with none hooked they start at once, as they always have
            AfterBegun(() => { _clock.Start(); _ailmentClock.Start(); });
        };
        // mdrcharUnload (MAINMOD.BAS:419-426) writes the character as they leave play, with no
        // test of M5F80 - so walking out of the dungeon saves your trip even with autosave off.
        // Without this the flag would not be "write less often", it would be "lose everything".
        // smoke test only: something on the way out that throws, to see the town still comes back
        if (Environment.GetEnvironmentVariable("MORDOR_TEST_LEAVE_THROWS") == "1")
            Ended += () => throw new InvalidOperationException("test: a leaving step that throws");
        Ended += () =>
        {
            // every step on its own guard: one that throws is written to the crash log and the
            // rest still run, above all the saves - a leaving that stopped half way would lose the
            // trip and leave the town's frame waiting on a dungeon that never finished
            Guarded("stopping the clocks", () => { _clock.Stop(); _ailmentClock.Stop(); });
            // however the window goes - q, the stairs up out of level 1 - a won round still being
            // drawn is finished first: its spoils, the room stamped, the Hall
            Guarded("finishing a won fight", FinishWonFight);
            Guarded("delivering what was carried", () => DeliverCarried());
            if (!_noRoomWriteOnClose)
            {
                Guarded("storing the room", () =>
                {
                    StoreRoom();             // leaving the level is leaving the room (MAZEMOD.BAS:811)
                    if (_combat?.Source != _room?.Records) StoreRoom(_combat);
                });
            }
            Guarded("saving dungeon.json", () => _dstate.Save(SavesDir));
            // and every form writes its placement as it unloads (FRMDUNM.BAS:360-361,
            // FRMGROUP.BAS:130-131), whichever way the dungeon is left - a rescue included
            Guarded("saving the window layout", SaveWindowLayout);
            Guarded("saving the party", () => _party.SaveAll(Saves.DirFor(_gameDir)));
        };

        // smoke tests only: he turns up about twelve times in twenty thousand encounters on the
        // deepest floor's stud squares, which is no way to check the ending works
        if (Environment.GetEnvironmentVariable("MORDOR_TEST_ASMODEUS") == "1")
            Begun += () =>
            {
                if (_monsters.FirstOrDefault(Asmodeus.Is) is not Monster def) return;
                _waiting = new List<CombatMonster> { new() { Def = def, Hits = def.Hits } };
                _encounterAt = (_level, _nav.X, _nav.Y);
                ShowWaiting(_waiting);
                // (test hook only - and no line: the original announces no encounter, his included)
            };
    }

    private string SavesDir => Saves.DirFor(_gameDir);

    /// <summary>Set by the stairs out of level 1, which leave without writing the room back (MAINMOD2.BAS:3955-3959).</summary>
    private bool _noRoomWriteOnClose;

    private DungeonNav NavFor(int level)
    {
        if (!_navs.TryGetValue(level, out var nav))
        {
            nav = new DungeonNav(_dungeon[level - 1], 0, 0, Facing.North);
            nav.Explored.Clear();
            // take in anything an older single-character save mapped, then use the shared map
            if (_ch.Explored.Count > 0) { _map.AbsorbLegacy(_ch, nav.Floor.Width); _ch.Explored.Clear(); }
            foreach (var cell in _map.CellsFor(level, nav.Floor.Width)) nav.Explored.Add(cell);
            // a map saved before the automap kept its own record has explored squares but nothing
            // written down about them; give it what it was entitled to show, secrets excepted
            if (nav.Explored.Count > 0 && !_map.HasRecordFor(level))
                MapRecord.BackfillExplored(_map, nav.Floor, nav.Explored);
            nav.Mapping = !_ch.IsLost;
            _navs[level] = nav;
        }
        return nav;
    }


    /// <summary>
    /// Write down why the dungeon shut, and where the party was. A window that closes for a reason
    /// and a window that vanishes because the process died look identical from the player's chair,
    /// and this is what tells the two apart afterwards.
    /// </summary>
    protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs e)
    {
        if (!_ended) CrashLog.Note($"dungeon closing - {e.CloseReason}, last input: {CrashLog.Context}");
        base.OnClosing(e);
    }

    // ------------------------------------------------------------------ the one frame

    /// <summary>
    /// The frame this dungeon is drawn in when it is not a window of its own: the city's. The
    /// original has one frame, frmMain, and going down unloads frmCityMain and shows
    /// frmDungeonMain inside the same frame (FRMCTYM.BAS:211-212; sub14E0, MAINMOD2.BAS:3170-3180);
    /// coming up is "Unload frmDungeonMain", "frmCityMain.Show" (MAINMOD2.BAS:3957-3958). The port did it with a second window
    /// laid over the first, with a toolbar and a set of child windows of its own, and the town's
    /// frame still showing behind.
    /// </summary>
    private Window? _outer;

    /// <summary>What <see cref="RunInside"/> hands back, finished when the dungeon is left.</summary>
    private System.Threading.Tasks.TaskCompletionSource? _hosted;

    /// <summary>Puts the town's contents back into the frame.</summary>
    private Action? _unmount;

    /// <summary>The frame's <c>MDIForm_QueryUnload</c> for a session end while the party is below.</summary>
    private readonly Func<System.Threading.Tasks.Task<bool>> _queryEnd;

    /// <summary>
    /// What used to hang on the window's own Opened and Closed. Inside the city's frame the window
    /// is never shown, so <see cref="RunInside"/> and <see cref="Close"/> raise them instead.
    /// </summary>
    private event Action? Begun, Ended;

    private bool _ended;

    private void EndOnce()
    {
        if (_ended) return;
        _ended = true;
        // each handler on its own, so that one throwing cannot stop the next
        foreach (var h in Ended?.GetInvocationList() ?? Array.Empty<Delegate>())
            Guarded("leaving the dungeon", (Action)h);
    }

    /// <summary>One step of leaving: a failure is noted in the crash log and the leaving goes on.</summary>
    private static void Guarded(string what, Action step)
    {
        try { step(); }
        catch (Exception ex) { CrashLog.Note($"dungeon: {what} failed - {ex.GetType().Name}: {ex.Message}"); }
    }

    /// <summary>The window a box or a dialog is shown over: the frame the dungeon is in.</summary>
    private Window DialogOwner => _outer ?? this;

    /// <summary>The element with the keyboard focus, in whichever frame this is in.</summary>
    private IInputElement? FocusedElement() => TopLevel.GetTopLevel(Root)?.FocusManager?.GetFocusedElement();

    /// <summary>
    /// Takes the dungeon's toolbar and its child windows out of this window and puts them in the
    /// city's frame through <paramref name="mount"/>; <paramref name="unmount"/> puts the town back.
    /// The task finishes once the dungeon has been left and everything its closing saved is saved.
    /// </summary>
    public System.Threading.Tasks.Task RunInside(Window frame, Action<Control> mount, Action unmount)
    {
        _outer = frame;
        _unmount = unmount;
        _hosted = new System.Threading.Tasks.TaskCompletionSource(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        var root = Root;
        Content = null;
        mount(root);
        // a session end while the party is below is asked the dungeon's question, not the town's
        OriginalBox.HostFrame(frame, _queryEnd);
        // once the frame has laid the children out, which is when the window's own Opened comes
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_ended) return;
            frame.UpdateLayout();
            Begun?.Invoke();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
        return _hosted.Task;
    }

    /// <summary>
    /// Every way out of the dungeon - Exit, the stairs up out of level 1, q, a rescue, a session
    /// ending - comes through here. Inside the city's frame it does what the window's Closed did,
    /// puts the town back and lets the city carry on; on its own it closes the window.
    /// </summary>
    public new void Close()
    {
        if (_outer == null) { base.Close(); return; }
        if (_ended) return;
        CrashLog.Note($"dungeon left - in the city's frame, last input: {CrashLog.Context}");
        try { EndOnce(); }
        finally
        {
            // whatever the leaving did, the town gets its frame back and the city carries on
            Guarded("unhosting", () => OriginalBox.UnhostFrame(_queryEnd));
            Guarded("putting the town back", () => _unmount?.Invoke());
            // the window was never shown; closing it lets its native handle go
            Guarded("closing the unseen window", () => base.Close());
            _hosted?.TrySetResult();
        }
    }

    /// <summary>The keyboard goes to the frame the dungeon is in.</summary>
    public bool Focus() => _outer != null ? _outer.Focus() : base.Focus();

    /// <summary>
    /// A key the city's frame received while the dungeon is in it: the frame's Form_KeyDown hands
    /// it on, as every form's does to fn1521.
    /// </summary>
    public void KeyFromFrame(KeyEventArgs e) => OnKeyDown(e);

    /// <summary>The frame's own state, for the pauses the original takes while it is minimised.</summary>
    private WindowState FrameState => DialogOwner.WindowState;


    // ------------------------------------------------------------------ input
    /// <summary>A key Root's preview has already given the game, on its way back up.</summary>
    private KeyEventArgs? _previewed;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // a key the preview gave the game, which the control then left alone, is not given twice
        if (ReferenceEquals(e, _previewed)) { _previewed = null; return; }
        // Alt+F4 is the frame's close, not F4: left alone it reaches MDIForm_QueryUnload's refusal
        // (FRMMAIN.BAS:171-175) instead of turning the character window to a page
        if (OriginalBox.IsCloseChord(e)) { base.OnKeyDown(e); return; }
        // Escape gives up a mod's pick waiting on the Automap (API version 13); never so with no mod
        if (AutomapKeys.CancelPick(e.Key, _automap)) { e.Handled = true; return; }
        // typing in txtSpell is typing, not commands; Enter casts and Escape hands the focus back
        // (txtSpell_KeyDown, FRMCHAR3.BAS:851-861)
        if (SheetSpellText.IsFocused)
        {
            if (e.Key is Key.Enter or Key.Return) { e.Handled = true; CastTyped(); }   // cmdCast_Click
            else if (e.Key == Key.Escape) { e.Handled = true; Focus(); }
            return;
        }
        // Objects & Spells' Form_KeyDown (FRMCHAR3.BAS:313-318): in a combo box, a text box or a
        // scroll bar - the Spell Type box, the spell box - every key but Escape is the control's
        // own, and Escape sends the focus back (gv1BFC.SetFocus)
        if (FocusedElement() is Visual focused && KeyOwner.KeepsKeys(focused)
            && (SheetPanel.IsVisualAncestorOf(focused) || TopLevel.GetTopLevel(focused) is Avalonia.Controls.Primitives.PopupRoot))
        {
            if (e.Key == Key.Escape) { e.Handled = true; Focus(); }
            return;
        }
        // a sub07E2 wait holds the key until it is over (KeyMustWait)
        try { if (KeyMustWait(e)) return; }
        catch (Exception ex) { CrashLog.Write("dungeon keystroke, finishing a won round", ex); e.Handled = true; return; }
        // An exception on the UI thread takes the process with it, and the window just vanishes.
        // Keystrokes are where the game does most of its work, so they are worth catching: the
        // player gets a line they can report instead of a window that disappeared.
        CrashLog.Context = $"key {e.Key}" + (e.KeyModifiers == KeyModifiers.None ? "" : $" +{e.KeyModifiers}")
                         + $" at {_level}:({_nav.X + 1},{_nav.Y + 1})"
                         + (_combat != null ? " in combat" : _waiting != null ? " with monsters waiting" : "");
        try { HandleKey(e); }
        catch (Exception ex)
        {
            // fn1521's handler (MAINMOD2.BAS:4169-4171) goes through sub04FC, which writes to the error
            // file and shows nothing unless the game was started with /DEBUG (MAINMOD2.BAS:1689-1700)
            CrashLog.Write("dungeon keystroke", ex);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The keys that stop a chest count, each with <c>If gv1E7C(Me) &gt; 0 Then gv1E7C(Me) = 0</c>
    /// and the caption cleared: Defend (MAINMOD2.BAS:3594-3600, only for a character who can
    /// act), the cursor keys (3759-3763), t (3946-3949), Fight (4011-4016), space (4040-4044) and
    /// Shift and a number, firing a buffer (4137-4145). The help's Commands page calls space
    /// "Stop fighting, opening, etc."
    /// </summary>
    private void CallOffChestCount(KeyEventArgs e)
    {
        if (_combat == null || _combat.ChestRoundsOf(_ch) <= 0) return;
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool stops = e.Key switch
        {
            Key.D when !shift => !_ch.Paralysed,
            Key.Up or Key.Down or Key.Left or Key.Right or Key.T or Key.F or Key.Space => true,
            Key.D0 => shift && HoldsSomething(9),
            Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9
                => shift && HoldsSomething(e.Key - Key.D1),
            _ => false,
        };
        if (!stops) return;
        _combat.ChestRounds[_ch] = 0;
        CombatHit.Text = "";
    }

    /// <summary>
    /// lblOption (FRMGROUP.BAS:453-466): Fight, Defend, the spell in quotes - and when there is
    /// no order but a chest count running, "Open".
    /// </summary>
    private string OptionOf(Character m)
    {
        string d = _party.OrderFor(m).Describe(id => _spells.FirstOrDefault(s => s.Id == id));
        return d == "None" && _combat != null && _combat.ChestRoundsOf(m) > 0 ? "Open" : d;
    }

    /// <summary>"If Chars(Me).M59B0(n) &gt; -1": a buffer with something in it.</summary>
    private bool HoldsSomething(int slot) =>
        slot >= 0 && slot < _ch.Buffers.Length && !SpellBuffers.IsEmpty(_ch.Buffers[slot]);

    private void HandleKey(KeyEventArgs e)
    {
        // the party keys work at any time, including mid-fight, which is the point of them
        // fn1521's second Select runs only "If Chars(Me).DungeonLevel > 0 And Chars(Me).DeathState
        // = False" (MAINMOD2.BAS:3757-4166). Its keys - the four cursor keys (3759), t (3946),
        // f (4011), space (4040), o and Alt-o, Party Options (4055-4062), j (4095) and the digits,
        // plain or shifted (4127) - do nothing at all for the dead, not even call off a chest
        // count. The port had tested only paralysis, so a body could walk. Ctrl-o is a page of the
        // first Select, and Alt with a digit is fn15DF's (HandlePartyKey), so both still work.
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (_ch.IsDead
            && ((!alt && e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.T or Key.F or Key.Space or Key.J
                                or Key.D0 or Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5 or Key.D6 or Key.D7 or Key.D8 or Key.D9)
                || (e.Key == Key.O && !e.KeyModifiers.HasFlag(KeyModifiers.Control))))
        {
            e.Handled = true;
            return;
        }
        if (HandlePartyKey(e)) { e.Handled = true; if (_combat != null) RefreshCombat(); return; }
        CallOffChestCount(e);

        // Shift and a number fires a spell buffer; a plain number picks which monster group you
        // are aiming at (MAINMOD2.BAS:4131-4158). Both work whether or not a fight has started.
        int digit = e.Key switch
        {
            Key.D1 => 1, Key.D2 => 2, Key.D3 => 3, Key.D4 => 4, Key.D5 => 5,
            Key.D6 => 6, Key.D7 => 7, Key.D8 => 8, Key.D9 => 9, Key.D0 => 0, _ => -1,
        };
        if (digit >= 0 && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            e.Handled = true;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) FireBuffer(digit == 0 ? 9 : digit - 1);
            else TargetGroup(digit - 1);
            return;
        }

        // Combat does not block you: the original's timer keeps running while you walk, so the
        // movement keys stay live and these only set what you will do each round.
        if (_combat != null && !_combat.IsOver)
        {
            switch (e.Key)
            {
                // the Commands page: f Fight, d Defend, c Cast Spell, <space> "Stop fighting,
                // opening, etc." - which is what the port has been calling the None order
                case Key.F: SetOrder(OrderKind.Fight); e.Handled = true; return;
                // held fast, fn1521 case 68 (MAINMOD2.BAS:3596) goes to its Else and calls
                // cmdCommand_Click 2 (FRMCHAR3.BAS:203), which does nothing for the paralysed
                case Key.D when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    if (!_ch.Paralysed) SetOrder(OrderKind.Defend);
                    e.Handled = true; return;
                case Key.Space: SetOrder(OrderKind.None); e.Handled = true; return;
                // no c here: it opens Objects & Spells on its Spells page, fight or no fight
                // (MAINMOD2.BAS:3582-3590), and a spell order is given with that window's Cast
                // (FRMCHAR3.BAS:174-184) - so it falls through to the window's keys and the
                // plain c below. It used to cast from a spell box of this panel's own.
                case Key.O: OpenTheChest(); e.Handled = true; return;
            }

        }
        else if (_combat != null && _combat.IsOver && e.Key is Key.Enter)
        {
            if (WaitBtn.IsVisible) OnWait(null, null!);
            e.Handled = true; return;
        }

        // Objects & Spells has no letters of its own. Its Form_KeyDown (FRMCHAR3.BAS:310-331)
        // leaves a key alone in a text box, combo box or scroll bar, save Escape, and hands every
        // other key to the game's fn1521 or queues it for the dungeon (sub0456) - the same keys
        // as anywhere else. The port had e, u, d, c, i and Escape mean Equip, Use, Drop, Cast and
        // Close while the window was open; c is the Spells page in fn1521, not Cast, and i is
        // the Info button. e, u, d and i reach the window's buttons through fn1521 below.

        string msg = "";
        switch (e.Key)
        {
            // CTRL-letter picks a page of the character window and must be tested before the plain
            // letters it shares - l is Drop Body, g is Give, s is Swap, c is Cast
            case Key.L when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowCharacterTab(CharTab.Look); e.Handled = true; return;
            case Key.B when e.KeyModifiers.HasFlag(KeyModifiers.Control): SetTabOnly(CharTab.Buffers); e.Handled = true; return;
            case Key.M when e.KeyModifiers.HasFlag(KeyModifiers.Control): SetTabOnly(CharTab.Misc); e.Handled = true; return;
            case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Control): SetTabOnly(CharTab.Stats); e.Handled = true; return;
            case Key.R when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowCharacterTab(CharTab.Resist); e.Handled = true; return;
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowCharacterTab(CharTab.Char); e.Handled = true; return;
            case Key.G when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowCharacterTab(CharTab.Guild); e.Handled = true; return;
            case Key.I when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowSheetTab(0); e.Handled = true; return;
            case Key.P when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowSheetTab(1); e.Handled = true; return;
            case Key.O when e.KeyModifiers.HasFlag(KeyModifiers.Control): ShowSheetTab(2); e.Handled = true; return;

            // Game Commands, from the help's Commands page: i Item Info, g Give Item/Gold,
            // p Pickup Body, l Drop Body, n Select next Character
            // fn1521 case 73 sends Objects & Spells lblCall "4" (MAINMOD2.BAS:3609-3613), its Info
            // button on the selected slot (FRMCHAR3.BAS:205-208) - not a key that opens the window
            case Key.I: OnSheetInfo(null, null!); e.Handled = true; return;
            // e: lblCall "1" (MAINMOD2.BAS:3591-3593; m05BC is a Static never set), the Equip button
            case Key.E: OnSheetEquip(null, null!); e.Handled = true; return;
            // u: lblCall "2" (MAINMOD2.BAS:3614-3618), the Use button
            case Key.U: OnSheetUse(null, null!); e.Handled = true; return;
            // d with nobody in the area, or held fast: fn1521 case 68's Else sends lblCall "3"
            // (MAINMOD2.BAS:3594-3607), the Drop button. With monsters about it is Defend, which starts the fight
            case Key.D when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                if (MonstersHere && !_ch.Paralysed) SetOrder(OrderKind.Defend);
                else OnSheetDrop(null, null!);
                e.Handled = true; return;
            // c: Objects & Spells on its Spells page, nothing if you are dead (MAINMOD2.BAS:3582-3590)
            case Key.C:
                if (!_ch.IsDead) OnShowSheetSpells(null, null!);
                e.Handled = true; return;
            case Key.G: AskToGive(); e.Handled = true; return;
            // the help's Game Commands: "s  Swap Items"
            case Key.S when !e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                SwapItems(); e.Handled = true; return;
            case Key.P: PickUpBody(); e.Handled = true; return;
            case Key.L: DropBody(); e.Handled = true; return;
            case Key.N:
                // fn1521 case 78 (MAINMOD2.BAS:3731-3739): Me + 1, back to 0 past the end, posted
                // to the Party window's lblCall, which switches if that is not Me already
                // (FRMGROUP.BAS:151). Dead or alive, and nothing is said - the port had three
                // sentences of its own here.
                if (_party.Members.Count > 1) SwitchCommandsTo((_party.SelectedIndex + 1) % _party.Members.Count);
                e.Handled = true; return;
            // 'a' Ask character to Join. fn1521 case 65 has no dungeon test, and the help's
            // Parties page describes it as the way a party is made anywhere: "In order for other
            // characters to join you, they must be in the same room, or in the city with you".
            // The dungeon had no case for this at all, so the toolbar's own Ask Character to Join
            // button - which does Press(Key.A) - has been pressing a key nothing listened for.
            // Mid-fight as well: fn1521 has no combat test before case mc0576 (MAINMOD2.BAS:3380-3444),
            // and the case itself refuses a dead joiner with monsters here (3479-3483) and puts a
            // living one into the fight with sub0BDE (3512).
            case Key.A:
                AskToJoin(); e.Handled = true; return;
            // monsters are standing there and have not moved: go for them
            case Key.F when _waiting != null: SetOrder(OrderKind.Fight); e.Handled = true; return;
            // `j` Allow monster(s) to join - only ever offered by a friendly group
            case Key.J when _waiting != null && _waiting.Count > 0 && _waiting[0].JoinOffer > 0:
            {
                string joined = EncounterGenerator.AcceptJoin(_ch, _waiting, Lookup, _rng, _party.OrderFor(_ch).Target);
                if (_waiting.Count == 0)
                {
                    var emptied = _waiting;
                    _waiting = null; _encounterAt = (-1, -1, -1); HideMonsters();
                    // MAINMOD2.BAS:4110-4113: no group left, so sub0C53 - as though it were won
                    ClearedByJoining(emptied);
                }
                else ShowWaiting(_waiting);
                RefreshParty(); SaveChar();
                if (joined != "") Say(joined); else CombatMonsterHit.Text = "";   // lblMonsterHit hidden
                e.Handled = true;
                return;
            }
            // the help's Miscellaneous Keys: "SHIFT-F6  Outputs current character stats,
            // statistics and information to CHARSTAT.TXT (in the Root Mordor directory)"
            case Key.F6 when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
            {
                CharStatFile.Write(_ch, _gameDir, Lookup);
                SetInfoTab(CharTab.Char);   // fn1521 case mc0596 paints the page it wrote (3553-3555)
                // MAINMOD2.BAS:5231 writes this before the report; its closing "BETA: Finished writing
                // Character Statistics..." is never reached - see CharStatFile. "Written to X." and
                // "Could not write CHARSTAT.TXT." were this port's.
                msg = "BETA: Writing Character Statistics to File...";
                break;
            }
            // the character window's pages, from the help's Commands list and fn1521
            case Key.F2: SetInfoTab(CharTab.Look); e.Handled = true; return;
            case Key.F3: SetTabOnly(CharTab.Stats); e.Handled = true; return;
            case Key.F4: SetInfoTab(CharTab.Resist); e.Handled = true; return;
            case Key.F6: SetInfoTab(CharTab.Char); e.Handled = true; return;
            case Key.F7: SetInfoTab(CharTab.Guild); e.Handled = true; return;
            case Key.F9: SetTabOnly(CharTab.Buffers); e.Handled = true; return;
            // F5, "Guild Experience Information". sub1572 case 2 is the odd page out: it paints
            // nothing and writes its lines into the Messages window instead (MODULE6.BAS:5164).
            case Key.F5:
            {
                Say(Progress.NextLevel(_ch));
                string? quest = Progress.Quest(_ch, id => _monsters.FirstOrDefault(m => m.Id == id),
                                                    id => _itemsById.TryGetValue(id, out var qi) ? qi : null);
                if (quest != null) Say(quest);
                e.Handled = true;
                return;
            }
            // "F1  HelpLesson", the first of the help's Game Commands. The window was only
            // reachable from the toolbar's book icon and the title screen.
            case Key.F1: new HelpWindow().Show(); Focus(); e.Handled = true; return;
            // "F11  Toolbar Toggle" - the help's Game Commands
            case Key.F11: Tools.IsVisible = !Tools.IsVisible; e.Handled = true; return;
            // the help's Commands page, Automap Keys: "F8  View AutoMap"
            case Key.F8: OnOpenAutomap(null, null!); e.Handled = true; return;

            // "<" and ">" page the automap from anywhere in the game - see AutomapKeys
            case Key.OemComma:
            case Key.OemPeriod:
                if (AutomapKeys.Handle(e.Key, e.KeyModifiers, _automapChild, _automap)) { e.Handled = true; return; }
                break;
            // "A character can access the Control Panel by Pressing F10 in either the City or
            // Dungeon" - the help's Control Panel page
            case Key.F10: OnControlPanel(null, null!); e.Handled = true; return;
            // Dungeon Commands: o Open Chest, t Take Stairs, Chute, etc.
            case Key.O: OpenTheChest(); e.Handled = true; return;
            // what the stairs give back is the new square's label, lblSquareInfo, and not a line
            case Key.T:
            {
                string label = UseStairs();
                if (label != "") MsgText.Text = label;
                break;
            }
            // <space> is "Stop fighting, opening, etc." (the help's Commands page). Out of a
            // fight there is nothing to stop, but it is still this window's key: letting it
            // bubble would hand it to whatever button last had focus, which is how a stray
            // space could press Exit and drop you out of the dungeon.
            case Key.Space: e.Handled = true; return;
            // the cursor keys are the movement keys - the letters they used to share are all
            // commands of their own in the original, so they no longer walk you anywhere.
            // Held fast, none of the four does anything - not even a turn - and says nothing:
            // "If Chars(Me).Ailments(mc01EC) = False And gv1EB2 = False Then" in
            // fn1521 (MAINMOD2.BAS:3766), after the chest count has been called off
            case Key.Up or Key.Down or Key.Left or Key.Right when _ch.Paralysed:
                e.Handled = true; return;
            case Key.Up: StepForward(); msg = ""; break;
            // Shift with a turn: "If p0534 = mc055A Then l0538 = fn1521(mc05EC, False)" after
            // each of the three (MAINMOD2.BAS:3903-3942) - the turn and then a whole step forward,
            // Up as though pressed. p0534 is VB's Shift argument, 1 being Shift alone. The step
            // stands in for the turn's own automap UPDATE, which that Else posts only without it
            case Key.Down: _nav.TurnAround(); AfterTurn(); if (e.KeyModifiers == KeyModifiers.Shift) StepForward(); msg = ""; break;
            case Key.Left: _nav.TurnLeft(); AfterTurn(); if (e.KeyModifiers == KeyModifiers.Shift) StepForward(); msg = ""; break;
            case Key.Right: _nav.TurnRight(); AfterTurn(); if (e.KeyModifiers == KeyModifiers.Shift) StepForward(); msg = ""; break;
            // Escape is "gv1BFC.SetFocus" and nothing more (MAINMOD2.BAS:3386-3390): the focus goes
            // back to the view. The port had taken it as leaving the dungeon; leaving is q
            case Key.Escape: Focus(); e.Handled = true; return;
            // q, "Exit to Main Menu?" (MAINMOD2.BAS:3619-3631)
            case Key.Q: ExitToMainMenu(); e.Handled = true; return;
            default: return;
        }

        e.Handled = true;
        // msg is a sub085B line (the give's, SHIFT-F6's) and goes to Messages alone: lblSquareInfo
        // is written only by the square routine and the death code (MAZEMOD.BAS:1545-1863,
        // FRMDUNM.BAS:507-510, MAINMOD2.BAS:3052-3054). It had been echoed there as well
        if (msg != "") Log(msg);
        Redraw();
    }

    /// <summary>fn1521's Up arrow (mc05EC, MAINMOD2.BAS:3770-3902): one step forward, or a bump.</summary>
    private void StepForward()
    {
                // the original works out where you are first, then applies whatever the square does
                // remember which side we are about to cross, so a door we open gets written down
                var (wasX, wasY, wasFacing) = (_nav.X, _nav.Y, _nav.Facing);
                if (_nav.MoveForward())
                {
                    if (!_ch.LostPosition && !_ch.LostLevel)
                        MapRecord.OnPassingThrough(_map, _nav.Floor, wasX, wasY, wasFacing);
                    // every sub085B line is logged as it is said, in the source's order; the label -
                    // lblSquareInfo, blanked at MAZEMOD.BAS:1545 and written only by the square's
                    // captions - is never put on the list
                    WorkOutWhereYouAre();
                    // sub09CC is not asked here: mdrmazShowMaze asks it when the area changes
                    // (see SettleArea, which AfterMove ends in)
                    string label = AfterMove();
                    foreach (string line in LookAheadLines()) Log(line);
                    MsgText.Text = label;
                }
                else
                {
                    // a wall: OUCH.WAV (sub09DE mc0606, MAINMOD2.BAS:3897) and not a word. "You walk
                    // into a wall." was this port's.
                    // In fog the wall you walked into goes on the map (MAINMOD2.BAS:3864-3891);
                    // the Redraw below is the "DRAW" that follows it. Only a wall bit takes that
                    // blocked branch (MAINMOD2.BAS:3772, 3863): a step off the edge with no wall bit
                    // goes through the move branch, is clamped back with OUCH and records nothing
                    // (MAINMOD2.BAS:3786-3790, 3848-3851)
                    if (_nav.EdgeAt(_nav.X, _nav.Y, _nav.Facing) == Edge.Wall)
                        MapRecord.OnBumpInFog(_map, _nav.Floor, _nav.X, _nav.Y, _nav.Facing);
                    Audio.Play("OUCH");
                }
    }

    /// <summary>
    /// <c>s</c> - Swap Items, from the help's Commands page. The original asks for both slot
    /// numbers in one box, comma separated: "Enter the Item Slots to Swap (i.e '12,17')". It
    /// refuses anything without a comma, anything longer than five characters, slot 1 - your hands
    /// - and anything past the fortieth (MAINMOD2.BAS:3415-3437). Empty slots swap perfectly well;
    /// that is how you tidy a pack.
    /// </summary>
    private async void SwapItems()
    {
        string? typed = await AskFor("Swap Items", "Enter the Item Slots to Swap\n(i.e '12,17')");
        if (typed == null) return;
        // the guards are the original's and live on SwapCommand, because "s" is a Game Command and
        // the city has to refuse in exactly the same places
        var slots = SwapCommand.Parse(typed);
        if (slots == null) return;
        if (!_ch.SwapSlots(slots.Value.A, slots.Value.B, buffersFollow: false)) return;   // no M59B0 remap (MAINMOD2.BAS:3432-3442)
        SaveChar();
        RefreshPack();
        RefreshBuffers();
        Log(SwapCommand.Done);   // sub085B (MAINMOD2.BAS:3439), the Messages window alone
    }

    // ---------------- the command bar ----------------
    //
    // The original's Dungeon window carries its commands on buttons as well as keys: ten spell
    // buffers along the bottom left, then Map, Pickup, Drop, Fight, Spell, Take, Open and Exit
    // (help screenshot game_files/dmainw1.png). Each of these stands for the key it shares with,
    // so they go through the same handler rather than growing a second copy of the logic.

    private void Press(Key k, KeyModifiers mods = KeyModifiers.None)
    {
        OnKeyDown(new KeyEventArgs { Key = k, KeyModifiers = mods, RoutedEvent = KeyDownEvent });
        Focus();   // the keyboard belongs to the window, not to the button that was clicked
    }

    private void OnForward(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.Up);
    private void OnTurnLeft(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.Left);
    private void OnTurnRight(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.Right);
    private void OnTurnAround(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.Down);

    // cmdAction_Click (FRMDUNM.BAS:76-109) hands each button's key to sub0456, as typed: index 0
    // Fight f, 1 Spell c, 2 Take t, 3 Open o, 4 Pickup p, 5 Drop l, 6 Map F8, 7 Exit q (the
    // indexes are the form's, MORDOR.EXE RCDATA 22). Map and Spell have handlers of their own that
    // the F8 and c keys call too (OnOpenAutomap, OnShowSheetSpells); the rest press their key.
    /// <summary>
    /// Take, cmdAction(2): the t key, "Take Stairs, Chute, etc." (FRMDUNM.BAS:94-95). The port had
    /// it open Objects &amp; Spells, as though it were Take an item.
    /// </summary>
    private void OnTakeStairs(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.T);
    private void OnPickupBody(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.P);
    private void OnDropBody(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.L);
    private void OnOpenChest(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.O);
    /// <summary>
    /// Form_QueryUnload (FRMMAIN.BAS:186-190) beeps and refuses while there is anything on your
    /// square - you cannot walk out of a fight by closing the window.
    /// </summary>
    private void OnExitToTown(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.Q);

    /// <summary>
    /// True once the player has answered Yes to "Exit to Main Menu?": the city that opened this
    /// window closes too, and the title comes back.
    /// </summary>
    public bool ExitedToMainMenu { get; private set; }

    /// <summary>
    /// <c>q</c>, and the Exit button, which is cmdAction 7 queuing it (FRMDUNM.BAS:85-86). fn1521
    /// case 81 (MAINMOD2.BAS:3619-3631): allowed with no monster in the area, or dead, and then
    /// only after "Exit to Main Menu?" (MsgBox 36, "EXIT"); otherwise a Beep and "Cannot exit while
    /// monsters are in the area!". Yes sets M6023 and M6014, which end the game loop; the
    /// character is written and unloaded and frmMenu - the title, not the city - is shown
    /// (MAINMOD2.BAS:6316-6331). The character stays where they stood, and nothing rests.
    /// </summary>
    private async void ExitToMainMenu()
    {
        if (!MonstersHere || _ch.IsDead)
        {
            if (!await Confirm("Exit to Main Menu?", "EXIT")) return;
            // a round still being drawn when the box went up finishes first
            FinishWonFight();
            ExitedToMainMenu = true;
            CrashLog.Note("closing: q, Exit to Main Menu");
            SaveChar();
            // M6023 is set, so sub1738 runs sub16CC before it shows frmMenu (MAINMOD2.BAS:6322-6325)
            await PartyUnload.Run(DialogOwner, _party, SavesDir, Say);
            Close();
            return;
        }
        Audio.Beep();
        Say("Cannot exit while monsters are in the area!");
    }

    /// <summary>
    /// A fight whose last monster has fallen while PlayRound is still drawing the round: sub0C53
    /// belongs to that round (MONSMOD.BAS:758-792), so whatever takes the party away - a step out
    /// of the area, the stairs, the Exit - finishes it first, with its spoils and its stamp.
    /// </summary>
    private void FinishWonFight()
    {
        if (_combat is { Won: true } && _finished != _combat) EndCombat();
        // and a chest's treasure still waiting on its second (AfterChestOpened): whatever takes the
        // party away, it has been gone through first, as it had in the original before any key
        // could be taken
        RunPendingTreasure(_pendingTreasure);
    }

    // ---------------- sub07E2 and the key queue ----------------
    //
    // Every form's Form_KeyDown hands a key to sub0456, which only queues it (MAINMOD.BAS:89-101;
    // FRMDUNM.BAS:201-215); the queue is run by sub0695 from the main loop (MAINMOD2.BAS:6310-6316,
    // MAINMOD.BAS:1063-1168), never from inside a routine that is waiting in sub07E2
    // (MAINMOD.BAS:1989-1999, DoEvents while the time runs). So a key pressed while monGetGold holds
    // its caption (MONSMOD.BAS:2175) or while monOpenChest waits (3650, then 2175) is done after the
    // wait, not in the middle of it. The port takes keys as they come, so these waits hold them
    // here and hand them on, in order, when the time is up. The window is not frozen: it draws,
    // and the mouse works, as DoEvents lets it.

    private bool _holdingKeys;
    private int _holdId;
    /// <summary>Set while held commands are being done, so newer ones queue behind them.</summary>
    private bool _replaying;
    /// <summary>Set around the replay's own call into OnKeyDown, so that key is not queued again.</summary>
    private bool _inReplayCall;
    private readonly Queue<Action> _heldKeys = new();

    /// <summary>Hold the keys for <paramref name="seconds"/> (a sub07E2), then do those pressed meanwhile.</summary>
    private void HoldKeys(double seconds) =>
        HoldUntil(System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(seconds)));

    /// <summary>Hold the keys until <paramref name="done"/> finishes, then do those pressed meanwhile.</summary>
    private async void HoldUntil(System.Threading.Tasks.Task done)
    {
        int mine = ++_holdId;
        _holdingKeys = true;
        // a wait that fails must not leave the keys held for good
        try { await done; }
        catch { if (mine == _holdId) _holdingKeys = false; throw; }
        if (mine != _holdId) return;   // a later wait has taken over, and ends later
        _holdingKeys = false;
        if (_ended) { _heldKeys.Clear(); return; }
        if (_replaying) return;        // the replay already running takes the rest in order
        _replaying = true;
        try
        {
            // one at a time, as sub0695 takes them; a command that starts another wait leaves the
            // rest to that wait's end, and one that puts up a box waits for its answer - the
            // original's MsgBox blocks the queue until it is answered
            while (!_holdingKeys && !_ended && _heldKeys.Count > 0)
            {
                var next = _heldKeys.Dequeue();
                _inReplayCall = true;
                try { next(); } finally { _inReplayCall = false; }
                // a mod's window is a child inside the frame, not a window it owns: only the boxes are waited on
                while (!_ended && DialogOwner.OwnedWindows.Any(w => w.IsVisible))
                    await System.Threading.Tasks.Task.Delay(50);
            }
        }
        finally { _replaying = false; }
        if (_ended) _heldKeys.Clear();
    }

    /// <summary>
    /// A key that must wait: one pressed during a hold or while held ones are still being done, or
    /// one pressed while a won round is still being drawn - that round's sub0C53 (MONSMOD.BAS:758-792)
    /// is finished first, as the round would have run to its end before the queue was looked at,
    /// and if it waits in monGetGold the key waits with it.
    /// </summary>
    private bool KeyMustWait(KeyEventArgs e)
    {
        if (_inReplayCall) return false;
        if (!_holdingKeys && !_replaying && _combat is { Won: true } && _finished != _combat) EndCombat();
        if (!_holdingKeys && !_replaying) return false;
        var (k, mods) = (e.Key, e.KeyModifiers);
        _heldKeys.Enqueue(() => OnKeyDown(new KeyEventArgs { Key = k, KeyModifiers = mods, RoutedEvent = KeyDownEvent }));
        e.Handled = true;
        return true;
    }

    /// <summary>
    /// A button that stands for a key goes through the same queue: cmdBuffer_Click and the
    /// cmdAction buttons only hand their key to sub0456 (FRMDUNM.BAS:76-123). True when it was held.
    /// </summary>
    private bool ButtonMustWait(Action act)
    {
        if (_inReplayCall) return false;
        // as KeyMustWait: a won round still being drawn is finished first, and its wait holds this too
        if (!_holdingKeys && !_replaying && _combat is { Won: true } && _finished != _combat) EndCombat();
        if (!_holdingKeys && !_replaying) return false;
        _heldKeys.Enqueue(act);
        return true;
    }

    private void OnShowSheetSpells(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ButtonMustWait(() => OnShowSheetSpells(s, e))) return;   // the c key waits in sub0456 too
        // the Spell button is cmdAction index 1, which queues the c key (FRMDUNM.BAS:90-91), so
        // it goes through fn1521 case 67 as the key does: nothing for the dead (MAINMOD2.BAS:3584)
        if (_ch.IsDead) return;
        // "If frmCharStat3.WindowState = minimised Then ... normal" (3587-3589), then lblCall =
        // "SPELL" (3590): the Spells page, tabItemTab = 1, and the spell box selected whole and
        // given the focus (FRMCHAR3.BAS:454-460). The port had opened the window on whatever page
        // it was on and put the focus in the list.
        _sheetChild.Minimised = false;
        ShowSheet();
        SheetTabs.SelectedIndex = 1;
        SheetSpellText.SelectAll();
        // after the window and the page have been laid out: a box on a page that is only now being
        // shown cannot take the focus yet, and it stayed with the view - so Enter never reached it
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SheetSpellText.SelectAll();
            SheetSpellText.Focus();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>The Info button beside the pack, which describes whatever is selected.</summary>
    private void OnSheetInfo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var slot = SelectedPackSlot();
        if (slot == null) return;   // nothing selected: the original's routines just leave
        // FRMCHAR3.BAS:204 sends item info to sub1560 page 1, which that table turns into the
        // character window Misc tab - "The Miscellaneous Tab is used to display either Item, Spell
        // or Companion Information" (help).
        // "If Chars(Me).Ailments(2) = False" (FRMCHAR3.BAS:203): held fast, the button does nothing
        if (_ch.Paralysed) return;
        // "If Pack(m001A).Ref > 0" (FRMCHAR3.BAS:208); the port's "You cannot make it out at all." is gone
        if (!_itemsById.TryGetValue(slot.ItemId, out var it)) return;
        _charPanel.ShowMiscPage(ItemInfoPage.Paint(it, slot.Alignment, slot.Known, _spells));
        _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
        _characterChild.Activate();
        Focus();
    }

    /// <summary>
    /// The Companions page: "the type of creature for a companion along with their hits", and
    /// above the box, whatever body is being carried - "When you pick up a dead companion or
    /// character, their number (for companions) or name (for characters) will be displayed above
    /// the box containing the companion information" (help, Companions).
    ///
    /// Each slot is the form's picture, name and hits (<see cref="CompanionsPage"/>). How bound or
    /// content a companion is (<see cref="Binding.Describe"/>, fn0D92) is not on this page in the
    /// original - lblCompHits holds the hits alone (MAINMOD2.BAS:4735) - but on the companion's Info
    /// page (<see cref="CompanionInfo"/>), a double-click away; the port's list had carried it too.
    /// </summary>
    private void RefreshCompanions()
    {
        // mdrcharShowCompanions for the one on screen: " " into each empty slot's name (MAINMOD2.BAS:4745)
        _ch.Companions.BlankEmptyNames();
        // Only the body being carried goes above the box. The states used to be here as well, as
        // "Under: ... Protected 12"; the original shows them nowhere on this page, only as the bare
        // gv07E2$ captions of lblSpecial on the character window (FRMCHAR1.BAS:174-182,
        // MODULE1.BAS:442-448), which CharacterPanel already draws.
        SheetStates.Text = _ch.CarryText;

        // mdrcharShowCompanions (MAINMOD2.BAS:4709-4760) writes each slot: the slot number, then the
        // creature's name in brackets when the companion has none of its own or its own name in
        // quotes, the hits separated by a backslash, and the picture; an empty slot is hidden and
        // the others keep their numbers
        _companionPage.Show(_ch.Companions, MonsterOf);
    }

    /// <summary>The Companions page, drawn to frmCharStat3's form data.</summary>
    private CompanionsPage _companionPage = null!;

    /// <summary>
    /// CTRL-i, CTRL-p and CTRL-o: the three pages of Objects and Spells, reachable "anytime in the
    /// game" (help, Commands).
    /// </summary>
    private void ShowSheetTab(int tab)
    {
        ShowSheet();
        SheetTabs.SelectedIndex = tab;
        Focus();
    }

    /// <summary>
    /// The Spells page Info button. <c>cmdInfo_Click</c> (FRMCHAR3.BAS:279-298) looks the spell up
    /// and calls <c>sub1560</c> with page 9, which is the character window Misc tab, then puts the
    /// focus on it. So the description does not appear here - it appears over there.
    /// </summary>
    private void OnSheetSpellInfo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // "If txtSpell.Text = "" Then Exit Sub", then fn1725 on what is typed - any spell at all, with
        // its two refusals as sub085B lines - and the box takes the full name (FRMCHAR3.BAS:287-296)
        string typed = SheetSpellText.Text ?? "";
        if (typed == "") return;
        int i = Spellbook.FindByName(_spells, typed, Log);
        if (i == -1) { Focus(); return; }   // sub0775
        SheetSpellText.Text = _spells[i].Name;
        _charPanel.ShowMiscPage(SpellInfo.Paint(_spells[i], _ch));
        _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
        _characterChild.Activate();
        // "If frmCharStat1!picTabPicture.Visible Then frmCharStat1!picTabPicture.SetFocus"
        // (FRMCHAR3.BAS:299-301): the keyboard leaves txtSpell, where Objects & Spells keeps every
        // key but Escape, for the character window, which hands them all to fn1521
        Focus();
    }

    /// <summary>Puts a description on the character window Misc page and brings it forward.</summary>
    private void ShowInMisc(string what)
    {
        _charPanel.ShowMisc(what);
        _characterChild.Minimised = false;
        _characterChild.Reopen();
        Focus();
    }

    // ---------------- the Companions page ----------------

    /// <summary>The slot whose picture, name or button was used (the control's Index), or -1.</summary>
    private int SelectedCompanion() =>
        _companionPage.Slot is int slot and >= 0 && _ch.Companions[slot] != null ? slot : -1;

    /// <summary>
    /// Kick Out, <c>cmdCompanion_Click</c> (FRMCHAR3.BAS:252-277). Two refusals first, then a
    /// question - and a bound companion is warned about, because letting one go runs
    /// <see cref="CompanionTurn.Check"/> and it may round on you instead of leaving.
    /// </summary>
    private async void OnCompanionKickOut(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        string? no = CompanionTurn.KickOutProblem(_ch, i, inDungeon: true);
        if (no != null) { if (no != "") Say(no); return; }
        // sub0775 once the question is answered, yes or no (FRMCHAR3.BAS:271-280): the keyboard back
        // to the main window. The refusals above leave it where it was (Exit Sub, 261 and 265).
        if (await Confirm(CompanionTurn.KickOutQuestion(_ch, i), "Kick Out Companion")) LetCompanionGo(i, forced: false);
        Focus();
    }

    /// <summary>picCompanion_MouseDown's refusal (FRMCHAR3.BAS:784).</summary>
    internal const string NoAttackingGoodCompanions = "It is against your ways to attack a Good companion.";

    /// <summary>
    /// "Companion Picture - Attack Companion", the help's Right Click Commands: a right press on
    /// the picture (picCompanion_MouseDown, FRMCHAR3.BAS:783-793). This is the same routine with its forced flag up, so the roll is
    /// skipped and the companion always rounds on you - unless the monsters already here are too
    /// far from it in alignment to join.
    /// </summary>
    private void OnCompanionAttack(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        if (i < 0) return;   // nothing selected: silently, as the original
        // picCompanion_MouseDown (FRMCHAR3.BAS:786) asks Hits > 0: a fallen companion cannot be attacked, silently
        if (_ch.Companions[i] is not Companion { Hits: > 0 }) return;
        // m02BE is a Static never assigned, so 0 - Good, as the line says (FRMCHAR3.BAS:781-784): a Good
        // character and a Good companion (the companion carries the foe's 0-2, ItemAlignment.Good here)
        if (_ch.Alignment == Alignment.Good && _ch.Companions[i]!.Alignment == ItemAlignment.Good)
        {
            Say(NoAttackingGoodCompanions);
            return;
        }
        LetCompanionGo(i, forced: true);
    }

    /// <summary>
    /// The shared half of the two. <c>fn0B6C</c> decides whether the companion becomes a monster;
    /// on Kick Out, a companion that does not turn simply leaves, which is what the caller does
    /// when the function comes back False.
    /// </summary>
    private void LetCompanionGo(int i, bool forced)
    {
        var live = _combat?.Monsters ?? _waiting;
        int groups = live == null ? 0 : live.Where(m => m.Alive).Select(m => m.Def.Id).Distinct().Count();

        var outcome = CompanionTurn.Check(_ch, i, forced, _rng, MonsterOf, live,
                                          isLeader: _party.SelectedIndex == 0, groupsPresent: groups);
        if (outcome.Message != null) Say(outcome.Message);
        if (outcome.Refused) return;

        if (outcome.Turned && outcome.Became != null)
        {
            DropCompanion(_ch, i);
            JoinTheFight(outcome.Became);
        }
        else if (!forced)
        {
            // Kick Out with no turn: the slot is simply cleared. The original says nothing at all
            // here - cmdCompanion_Click clears M4ED4 and repaints, and there is no message.
            DropCompanion(_ch, i);
        }
        SaveChar();
        RefreshCompanions();
        Redraw(sub0956: false);
    }

    /// <summary>
    /// Puts a turned companion among the monsters. If a fight is already running it joins it; if
    /// not, it becomes the group standing on this square and the fight starts, which is what
    /// <c>fn0B6C</c> does by calling sub0BCD and then re-arming the combat timer.
    /// </summary>
    private void JoinTheFight(CombatMonster became)
    {
        if (_combat != null)
        {
            _combat.Monsters.Add(became);
            Sub0BDE();   // monCheckCompAttack ends in sub0BDE -1 (MONSMOD.BAS:597)
            RefreshCombat();
            return;
        }
        var group = _waiting ?? new List<CombatMonster>();
        group.Add(became);
        _waiting = null;
        _encounterAt = (_level, _nav.X, _nav.Y);
        StartCombat(group, isLair: false, monstersMoveNow: false);
    }

    /// <summary>
    /// "Companion Name Label - Name a Companion", the help's Click Commands: <c>lblCompName_Click</c>
    /// (FRMCHAR3.BAS:478-488), the name clicked on the page. <c>If fn0EEF() = False</c>: not during a
    /// fight, which the page's callback checks (Engaged).
    /// </summary>
    private async void OnCompanionName(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        if (i < 0) return;   // nothing selected: silently, as the original
        // fn170D("Name " + fn128B(...), "New Name?", ...) - FRMCHAR3.BAS:477
        string? typed = await AskFor($"Name {_ch.CompanionName(i)}", "New Name?");
        if (typed == null) return;
        if (_ch.Companions[i] is Companion named) named.Name = typed.Trim();
        SaveChar();
        RefreshCompanions();
    }

    /// <summary>
    /// Info, and "Companion Picture - Get Companion Stats" on a double-click. It answers on the
    /// character window's Misc page, where sub1572 case 8 paints it.
    /// </summary>
    private void OnCompanionInfo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        if (i < 0 || _ch.Companions[i] is not Companion c) return;   // nothing selected: silently, as the original
        var def = MonsterOf(c);
        if (def == null) return;   // the page's own lookup finds nothing and it leaves (MAINMOD2.BAS:4934-4940)
        CompanionInfo.Sharpen(_ch, c, def, Lookup);
        // sub06FE l0292, False, True, False, Me (FRMCHAR3.BAS:757): into the Library at the ID level
        // just sharpened, with Chars(0)'s "X,Y,level" as the place when it knows where it is
        // (MONSMOD.BAS:3987-3991) - the port keeps X and Y from 0, the original from 1
        Librarian.FileCompanion(SavesDir, _monsters, _ch, def, c.IdLevel, LivePlace());
        _charPanel.ShowMiscPage(CompanionInfo.Paint(c, i, def, _ch, Lookup), MonsterPortrait.Of(def), _options.NoStretch);
        _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
        _characterChild.Activate();
        Focus();   // frmCharStat1!picTabPicture.SetFocus (FRMCHAR3.BAS:759)
    }

    /// <summary>
    /// <see cref="Librarian.FileSighting"/> - sub06FE with the level rolled - for each group given
    /// as its Foes record's Ref, KnownAs and GroupCount and whether it is filed as seen, read by
    /// <paramref name="reader"/>, placed where the party stands now (<see cref="LivePlace"/>), and
    /// dated today. The Library file is read and written once for the lot.
    /// </summary>
    private void FileSightings(IEnumerable<(int Ref, int KnownAs, int Count, bool Sighting)> groups, bool killed, Character reader)
    {
        var list = groups.ToList();
        if (list.Count == 0) return;
        // filed from the middle of a room's reading, a fight and a death: a Library that cannot be
        // filed to is logged and passed over rather than stopping any of them
        try
        {
            var store = Librarian.Store.Load(SavesDir, _monsters);
            int today = Librarian.Serial(DateTime.Now);
            string? place = LivePlace();
            foreach (var (r, k, n, seen) in list)
                Librarian.FileSighting(store, _monsters, r, k, n, killed, seen, reader, place, _level, today, Lookup);
            store.Save(SavesDir);
        }
        catch (Exception ex) { CrashLog.Note("the Library could not file a sighting: " + ex.Message); }
    }

    /// <summary>
    /// Chars(0)'s "X,Y,level" for the Library (MONSMOD.BAS:3987-3991), from the square the party
    /// stands on now. The original's Chars(0).X and .Y move with every step (MAINMOD2.BAS:3786-3790);
    /// the port's record of them is written only at a change of area (<see cref="SavePos"/>), so it
    /// would name the square the party came into the room on.
    /// </summary>
    private string? LivePlace() =>
        Librarian.PlaceOf(Leader.LostLevel, Leader.LostPosition, _level, _nav.X, _nav.Y);

    /// <summary>A combat member as its group's Foes record would read: table indices of what it is and what it is taken for.</summary>
    private (int Ref, int KnownAs, int Count, bool Sighting) FoeOf(CombatMonster m, int count, bool sighting)
    {
        int r = -1, k = -1;
        for (int i = 0; i < _monsters.Count; i++)
        {
            if (r < 0 && ReferenceEquals(_monsters[i], m.Def)) r = i;
            if (k < 0 && ReferenceEquals(_monsters[i], m.Seen)) k = i;
        }
        return (r < 0 ? int.MaxValue : r, k, count, sighting);   // not in the table: filed nowhere
    }

    /// <summary>The creature a companion is, <c>Monsters(Companions(i).Ref)</c>.</summary>
    private Monster? MonsterOf(Companion c) => c.Def(_monsters);

    /// <summary>
    /// <c>sub0C00</c>: every companion that is not bound for life walks towards leaving, says so as
    /// it goes, and at zero goes - unless it turns on you on the way out, which is the same
    /// <c>fn0B6C</c> Kick Out runs. See <see cref="CompanionMood"/> for the arithmetic.
    /// </summary>
    private void CompanionsDrift(Character who)
    {
        bool leader = _party.Members.Count > 0 && _party.Members[0] == who;
        // slots 0 to 3 in order (sub0C00, MONSMOD.BAS:616); one that goes leaves its slot empty
        for (int i = 0; i < CompanionSlots.Slots; i++)
        {
            if (who.Companions[i] is not Companion c) continue;
            var def = MonsterOf(c);
            if (def == null) continue;

            var step = CompanionMood.Round(who, i, def, _rng, leader);
            if (step.Drifted != null) Say(step.Drifted);
            if (!step.Leaving) continue;

            var live = _combat?.Monsters ?? _waiting;
            int groups = live == null ? 0 : live.Where(m => m.Alive).Select(m => m.Def.Id).Distinct().Count();
            var outcome = CompanionTurn.Check(who, i, forced: false, _rng, MonsterOf, live,
                                              isLeader: leader, groupsPresent: groups);
            if (outcome.Refused) continue;
            if (outcome.Turned && outcome.Became != null)
            {
                if (outcome.Message != null) Say(outcome.Message);
                DropCompanion(who, i);
                JoinTheFight(outcome.Became);
                continue;
            }
            // fn0B6C came back False, so it simply goes and the game says so
            if (step.Left != null) Say(step.Left);
            DropCompanion(who, i);
        }
        RefreshCompanions();
    }

    /// <summary>
    /// Lets a companion go: <c>Ref = -1</c>, and the carry cleared if it was that slot
    /// (cmdCompanion_Click, FRMCHAR3.BAS:273-276). The slot stays empty; nobody moves up.
    /// </summary>
    private void DropCompanion(Character who, int i)
    {
        who.Companions.Release(i);
        if (who.CarryingCompanion == i) who.CarryingCompanion = -1;
    }

    // ---------------- the Party Information buttons ----------------

    /// <summary>
    /// A party row is both ends of a drag. <c>lblName_DragDrop</c> (FRMGROUP.BAS:185-250) reads the
    /// dropped control's Tag and does one of several things; two of them matter here.
    ///
    /// <c>"CHAR"</c> - another name - swaps the two places, and each member's standing order goes
    /// with them. In the dungeon it refuses while a group is engaged: "You cannot rearrange the
    /// party during combat!" (FRMGROUP.BAS:213-217), and that test is made only when
    /// <c>Chars(0).DungeonLevel &gt; False</c>, so the city never refuses.
    ///
    /// <c>"ITEM"</c> - a pack slot - hands it over, which is what the help means by "give an item
    /// or gold to another character by Dragging and Dropping the selected item from the items
    /// window ... to the name of the receiving character". It refuses while engaged too, in its own
    /// words, and a drop on yourself does nothing at all (FRMGROUP.BAS:192-200).
    /// </summary>
    private void WirePartyRow(Control row, int which)
    {
        Dragging.Source(row, () => DragPayload.Member(which));
        Dragging.Target(row, DragKind.Character, p =>
        {
            if (Engaged) { Say("You cannot rearrange the party during combat!"); return; }
            string? leader = _party.Swap(p.Index, which);
            _captionNamed = true;   // mdrmulSwitch, then the caption set outright (FRMGROUP.BAS:240, 247-249)
            SaveChar();
            RefreshParty();
            Redraw(sub0956: false);
            if (leader != null) Log(leader);   // a sub085B (FRMGROUP.BAS:243); it went to the label, and only there
        });
        // a companion picture ("COMP") is neither a vault line, an item nor a name: lblName_DragDrop
        // makes the same combat refusal and then runs lblName_Click Index (FRMGROUP.BAS:214-220), so
        // the commands go to the member it was let go over, and nothing else happens
        Dragging.Target(row, DragKind.Companion, _ =>
        {
            if (Engaged) { Say("You cannot rearrange the party during combat!"); return; }
            SwitchCommandsTo(which);
            Focus();
        });
        Dragging.Target(row, DragKind.Item, p =>
        {
            if (Engaged) { Say("You cannot give items or gold during combat!"); return; }
            if (which == _party.SelectedIndex) return;      // "If gv1A82 = Index Then Exit Sub"
            var to = _party.Members[which];
            if (p.Index <= 0 || p.Index >= Character.PackSize) return;
            if (_ch.Pack[p.Index] is not InventorySlot slot || slot.ItemId == 0) return;
            var it = Lookup(slot.ItemId);
            // A handover that works says "Give of Item to "X" Completed!" (MAINMOD2.BAS:3004-3006),
            // after the re-reading line when the person receiving it can read the item better than
            // its owner could. "Tester hands the Elven Ring to Brannoc." was the port's own.
            string said = "";
            if (Party.GiveItem(_ch, to, slot, it, Lookup, line => said = line, FiledFor(to)) is string no)
            { GiveRefused(to, no); return; }
            SaveChar();
            RefreshPack();
            RefreshParty();
            if (said != "") Say(said);
            Say(Party.GaveItem(to));
        });
    }

    /// <summary>
    /// sub14B5's refusals: "Cannot give Guild-Issued Items!" and "Cannot give equipped items!" are
    /// sub085B lines (MAINMOD2.BAS:2939, 2943), the full pack a <c>MsgBox(..., mc02EC, "SORRY!")</c>
    /// (MAINMOD2.BAS:2953) - the same split as the city's.
    /// </summary>
    private void GiveRefused(Character to, string no)
    {
        if (no == Party.NoFreeSlots(to)) _ = Tell("SORRY!", no);
        else Say(no);
    }

    /// <summary>
    /// sub14B5's <c>sub0730 p049A, Ref, IdLevel, -1, False</c> (MAINMOD2.BAS:2962): the Library
    /// files the given item under the receiver, graded as sub0730 grades it.
    /// </summary>
    private Action<int, IdLevel> FiledFor(Character to) =>
        (id, level) => Librarian.FileItem(SavesDir, to, id, level);

    /// <summary>
    /// <c>fn0EEF() &gt; False And Foes(0).Engaged</c> - a fight that has actually started, which
    /// is what the party window's two refusals test.
    /// </summary>
    private bool Engaged => _combat != null && !_combat.IsOver;

    /// <summary>
    /// <c>fn0EEF() &gt; 0</c>: there are monsters on this square, whether or not they have come
    /// for you. Several rules test this rather than <see cref="Engaged"/>, and the difference
    /// matters - a group standing quietly in front of you still stops you leaving.
    /// </summary>
    private bool MonstersHere => Engaged || _waiting is { Count: > 0 };

    /// <summary>
    /// <c>mdrmulSwitch</c> (MULTIMOD.BAS:605-630): who the commands are for. It sets
    /// <c>gv1A84</c> and <c>Me</c>, redraws the ten buffers, re-captions the Dungeon window
    /// "Dungeon (Name)" and moves the combat cursor. It does not touch <c>gv1A82</c>, which is
    /// what the information windows are showing.
    /// </summary>
    private void SwitchCommandsTo(int which)
    {
        if (which == _party.SelectedIndex) return;       // "If Index <> Me Then"
        if (!_party.Select(which)) return;
        _captionNamed = true;                            // "Dungeon (Name)", any party size (MULTIMOD.BAS:622-623)
        RetitleDungeon();
        RefreshBuffers();
        RefreshParty();
        Redraw(sub0956: false);
        // monShowCursor for the new character (MULTIMOD.BAS:624)
        ShowCursor();
        // "If Fight.ChestTrap > -1 Then monShowChest" (MULTIMOD.BAS:625-627): the chest is drawn
        // again for whoever is now in command, since what it shows is their own reading of it.
        // mdrmulSwitch writes no message; "X is watching now." was this port's.
        // monShowChest reads the Fight record, which keeps the chest after the fight is won; here
        // that is _chest, the fight itself being gone by then, and ShowChest reads _combat - which
        // with no fight on it took for "no chest" and painted the chest out. For a character with
        // no hits it leaves at once (MONSMOD.BAS:4090) and nothing is erased: picMonsters is a
        // painted surface and the chest last drawn stays on it, picture and trap panel alike.
        if (_chest != null)
        {
            if (_combat != null) ShowChest();
            else { _combat = _chest; try { ShowChest(); } finally { _combat = null; } }
        }
        // The original leaves frmCharStat3 on gv1A82 here, but its buttons act on Chars(Me)
        // (FRMCHAR3.BAS:203-240) and it never shows one while acting on the other: activating the
        // form - which any click on it does first - runs "If gv19C2(mc0158).M5E4F <> Me Then
        // sub1536 Me" (FRMCHAR3.BAS:304-307), the full repaint. The port keeps one index, so the
        // sheet is repainted now; it had kept the last character's pack under the new one's buttons.
        RefreshPack();
        RefreshCompanions();
        RefreshSheetSpells();
        Mordor.Modding.ModHost.Active.CharacterSwitched(_ch, inDungeon: true);
    }

    /// <summary>
    /// <c>sub1536</c> (MULTIMOD.BAS:654-680), the Switch To button and a double click on a name:
    /// the same, and then every information window repainted for the new character - the stats
    /// pages, the companions, all forty pack slots.
    ///
    /// The original keeps two indices, <c>gv1A82</c> for what the windows show and <c>gv1A84</c>
    /// for whose commands are running, so that switching in a fight need not repaint anything.
    /// This port keeps one: see docs/FIDELITY.md.
    /// </summary>
    private void SwitchEverythingTo(int which)
    {
        SwitchCommandsTo(which);
        // RetitleDungeon, called above, already repaints the character window - see the note in
        // docs/FIDELITY.md about this port keeping one index where the original keeps two.
        RefreshPack();
        RefreshCompanions();
        RefreshSheetSpells();
    }

    /// <summary>
    /// The Switch To button. <c>cmdCommand_Click</c> case 0 is
    /// <c>If gv1A82 &lt;&gt; Me Then sub1536 Me</c> (FRMGROUP.BAS:35-36) - it switches to whoever
    /// is selected now, and does nothing if the windows already show them.
    ///
    /// This button used to press 'n', which is Select next Character - a different command from
    /// the help's list entirely, and one that walks past the character you had picked.
    /// </summary>
    private void OnSwitchTo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // _shownSlot is gv1A82; when it already matches, the repaint does no harm
        if (_shownSlot != _party.SelectedIndex) Sub1536(_party.SelectedIndex); else SwitchEverythingTo(_party.SelectedIndex);
        Focus();
    }

    /// <summary>
    /// The Options button. <c>cmdCommand_Click</c> case 1 refuses during a fight, in its own
    /// words (FRMGROUP.BAS:37-42).
    /// </summary>
    private void OnPartyOptions(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the test is fn0EEF(), monsters present - a peaced group blocks it too
        if (MonstersHere)
        {
            Say("Cannot set default options while in Combat!");
            return;
        }
        ShowPartyOptions();
    }
    private void OnPartyLeave(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.L, KeyModifiers.Alt);

    // ---------------- the Automap ----------------

    /// <summary>The Automap window behind the child: never shown, but still its controller.</summary>
    private AutomapWindow? _automap;

    /// <summary>
    /// The Map button and F8. The original Automap is a level bar and nothing drawn that has not
    /// been recorded (FRMAUTOM.BAS:74-158), and it is an MDI child rather than a dialog.
    /// </summary>
    private void OnOpenAutomap(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ButtonMustWait(() => OnOpenAutomap(s, e))) return;   // cmdAction's F8 (FRMDUNM.BAS:100-101) waits too
        foreach (var (lvl, nav) in _navs) _map.Merge(lvl, nav.Explored, nav.Floor.Width);
        _map.Save(SavesDir);
        int deepest = Math.Max(_dstate.DeepestReached, _map.DeepestVisited);

        // FRMAUTOM is an MDI child in the original, not a dialog: the map sits on the frame beside
        // the dungeon and you can walk with it open. F8 is frmAutoMap.Show and WindowState = False
        // (MAINMOD2.BAS:3560-3563): a map already loaded is restored and brought up, not closed,
        // and only one that is not is loaded - Form_Load, on the level you are on.
        if (_automap != null && _automapChild.IsVisible)
        {
            _automapChild.Minimised = false;
            _automapChild.Reopen();
            Focus();
            return;
        }
        // FRMAUTOM reads Chars(0) throughout (m0078 = 0, FRMAUTOM.BAS:24) - the leader, not the
        // character whose commands are being entered, whom mdrmulSwitch only points Me at
        // (MULTIMOD.BAS:618)
        var win = new AutomapWindow(_dungeon, _map, deepest,
                                    (_level, _nav.X, _nav.Y, _nav.Facing), Leader.IsLost,
                                    Leader.Options.AutoChangeLevel, Leader.LostLevel, () => _options.NoBlinkCursor);
        _automap = win;                     // the buttons and the slider are still its handlers
        win.PutInto(_automapChild);
        Focus();        // the keys still belong to the dungeon
    }

    // ---------------- character sheet / pack ----------------

    private void ShowSheet()
    {
        RefreshCompanions();
        RefreshPack();
        RefreshSheetSpells();
        RefreshBuffers();
        SheetPanel.IsVisible = true;
    }

    /// <summary>
    /// The Items tab of Objects &amp; Spells. It lists the pack's forty **slots**, numbered, with the
    /// empty ones still showing their number - which is how the original's own list reads, and why
    /// it has to: Swap Items takes those numbers, the buffers point at them, and the game's
    /// messages name them ("BOX: Slot #7, Potion"). Slot 1 is the character's Hands.
    ///
    /// An equipped item is marked with an asterisk, as it is in the game, in the lblItemStatus
    /// column before the line (<see cref="ObjectsForm.PackRow"/>).
    /// </summary>
    private void RefreshPack()
    {
        int sel = PackList.SelectedIndex;
        var rows = new List<Control>(Character.PackSize);
        for (int i = 0; i < Character.PackSize; i++)
        {
            var slot = _ch.Pack[i];
            // the mark is lblItemStatus, a label of its own beside lblItem (MAINMOD2.BAS:5429-5433)
            if (slot == null || (i > 0 && slot.ItemId == 0)) { rows.Add(ObjectsForm.PackRow("", PackLine($"{i + 1}.", false, false))); continue; }

            _itemsById.TryGetValue(slot.ItemId, out var it);
            string mark = slot.Equipped ? "*" : "";
            if (it == null) { rows.Add(ObjectsForm.PackRow(mark, PackLine($"{i + 1}. #{slot.ItemId}", false, false))); continue; }

            // Loot.NameFor is fn1345 (MAINMOD2.BAS:2566) and the city has always used it. This
            // screen built its own string instead, and it differed in four ways: the charges and
            // the alignment were the other way round, the charges only showed for an item with a
            // spell on it where the original shows them for anything that has any, "(cursed)" was
            // "[CURSED]" and only when equipped, and there was a "(not for your guild)" that the
            // original does not have anywhere. Two screens formatting one list differently is a
            // bug on its own, never mind which of the two was right.
            string text = $"{i + 1}. {Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges)}";
            // with a mod, something after the name (ctx.UI.PackRowDecoration); with none, the line alone
            rows.Add(ObjectsForm.PackRow(mark, ModSlots.PackLine(PackLine(text, PackRow.Bold(it, slot), PackRow.Italic(it, slot)), _ch, it, slot, i)));
        }
        PackList.ItemsSource = rows;
        PackList.SelectedIndex = Math.Clamp(sel, 0, Character.PackSize - 1);
        (_packShownFor, _packShown) = (_ch, PackStamp());
    }

    /// <summary>
    /// The original paints a slot the moment anything changes it - <c>mdrcharShowItem</c> after a
    /// chest's item lands (MONSMOD.BAS:3849), sub0F74 (MAINMOD2.BAS:1864), mdrcharRemoveItem - and
    /// the list is simply right. The port rebuilds the list instead, and each place that changed
    /// the pack had to remember to: a chest's item went in unseen until something else redrew
    /// it. What the list last showed is kept here, and <see cref="RefreshParty"/>, which follows
    /// nearly everything, catches the list up when the pack has moved under it.
    /// </summary>
    private string _packShown = "";
    private Character? _packShownFor;

    private string PackStamp() => string.Join(",", _ch.Pack.Select(s => s == null ? "-"
                                  : $"{s.ItemId}.{s.Equipped}.{s.Charges}.{s.Known}.{s.Alignment}"));

    private void CatchUpPack()
    {
        if (_packShownFor == _ch && _packShown == PackStamp()) return;
        RefreshPack();
        RefreshBuffers();
    }

    /// <summary>
    /// One line of the Items page, bold and italic as <c>mdrcharShowItem</c> sets them. See
    /// <see cref="PackRow"/>.
    /// </summary>
    private static Control PackLine(string text, bool bold, bool italic) => new TextBlock
    {
        Text = text,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle = italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
    };

    /// <summary>Whatever is selected in the pack list, or null - the list is slot-indexed.</summary>
    private InventorySlot? SelectedPackSlot()
    {
        int i = PackList.SelectedIndex;
        if (i <= 0 || i >= Character.PackSize) return null;      // 0 is your hands
        var slot = _ch.Pack[i];
        return slot == null || slot.ItemId == 0 ? null : slot;
    }

    private void OnSheetEquip(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ch.Paralysed) return;   // cmdCommand_Click (FRMCHAR3.BAS:203): "If Ailments(mc0116) = False"
        int idx = PackList.SelectedIndex;
        EquipSelected();
        // "sub0F74 Me, m001A / If m001A < mc0122 - 1 Then lblItem_Click m001A + 1" (FRMCHAR3.BAS:
        // 212-216): the selection moves on to the next row after every Equip, whether sub0F74 did
        // anything or not - only the last slot stays put
        if (idx < Character.PackSize - 1) PackList.SelectedIndex = idx + 1;
    }

    /// <summary><c>sub0F74</c> on the selected slot.</summary>
    private void EquipSelected()
    {
        var slot = SelectedPackSlot();
        if (slot == null) return;   // nothing selected: the original's routines just leave
        int idx = PackList.SelectedIndex;
        // no identification test: neither sub0F74 (equip) nor sub15CD (use) asks how well you know
        // the item - "You don't know what that is. ..." was this port's
        _itemsById.TryGetValue(slot.ItemId, out var it);
        if (it != null && !slot.Equipped)
        {
            // sub0F74 will not even discuss a potion or a scroll - it checks the item's class
            // maximum first and returns without a word (MODULE6.BAS:1724)
            if (!Character.IsWearable(it)) return;
            // the refusals are sub085B lines (MAINMOD2.BAS:1734, 1786, 1816, 1818): the Messages window
            if (_ch.WearRefusal(slot, it, Lookup) is string why) { Log(why); return; }
        }
        if (slot.Equipped && !_ch.CanUnequip(slot, it)) { Log("Cannot unequip!  Item is cursed!"); return; }

        slot.Equipped = !slot.Equipped;
        // MODULE6.BAS:1745 clears the slot curse on the way off, 1786 and 1821 set it on the way on
        if (slot.Equipped) _ch.ApplyEquipCurse(slot, it); else slot.SlotCursed = false;
        Effects.Recompute(_ch, id => _itemsById.TryGetValue(id, out var e) ? e : null);
        // and nothing is said either way: sub0F74's only lines are its four refusals (MAINMOD2.BAS:
        // 1734, 1786, 1816, 1818). "You equip the X.", "You unequip the X." and "... feel a chill.
        // It is cursed and will not come off." were this port's.
        RefreshPack(); SaveChar();
    }

    private Item? Lookup(short id) => _itemsById.TryGetValue(id, out var it) ? it : null;

    /// <summary>
    /// The Party Information window, which the help says "will pop up and remain until the party
    /// is disbanded (or killed)".
    ///
    /// It is shown even for one character: "Even when only one character is loaded, the party
    /// window is still displayed. The reason for this is so that the player can see the
    /// character's current hits, status, etc, if the Stats Window is tabbed to a page other than
    /// the character stats page" (help, The Game Screen). The port used to hide it below two,
    /// which was exactly wrong now that the character window has seven pages to be lost on.
    /// </summary>
    private void RefreshParty()
    {
        Mordor.Modding.ModHost.Active.GameChanged();   // a mod's own window looks again (ctx.Game)
        PartyBox.IsVisible = true;
        CatchUpPack();

        // modUpdateStats: "If fn0EEF() = False Then" - no monster in the area - every member's
        // order is cleared as the window is painted (FRMGROUP.BAS:418-422): Att and the spell,
        // M56C4, and the item order M5766 with them - not the group aimed at, M547A
        if (!MonstersHere)
        {
            foreach (var m in _party.Members)
            {
                var o = _party.OrderFor(m);
                o.Kind = OrderKind.None;
                o.SpellId = 0;
            }
            _combat?.ItemOrders.Clear();
        }

        PartyRows.Children.Clear();
        var modColumns = ModSlots.PartyHeader(PartyHeader, 5);   // the mods' party columns, if any (docs/MODDING.md)
        for (int i = 0; i < _party.Size; i++)
        {
            var m = _party.Members[i];
            bool selected = i == _party.SelectedIndex;
            // modUpdateStats (FRMGROUP.BAS:436-452): "Po", "Di", "Pa" run together, or "OK".
            // Nothing else goes in this column - the dead are off the roster before it paints, all
            // but the last one left, whom the UPDATE never prunes (FRMGROUP.BAS:327). That one
            // shows its hits, "0/15", and "OK" beside them: modUpdateStats has no word for dead.
            string state = PartyRow.Status(m);
            bool well = PartyRow.StatusIsWell(m);
            // mdrmulSwitch (MULTIMOD.BAS:613-616, 640-642): every Name and Hits black, then the
            // selected one's green, and its Name bold. There is no row marker in game_files/bm5.png.
            // frmParty's form data, as the town's copy has it (CityWindow.axaml): the header's
            // columns, each label where its rectangle puts it, a row 195 twips, 13 pixels, tall,
            // or a larger Options font's line (PartyRowHeight); lblOption (FRMGROUP.BAS:453-467)
            // is the standing order. See CityWindow.PartyRowGrid.
            // "Party Character - Switch all windows to Character", the help's Double Click Commands
            int which = i;
            var row = CityWindow.PartyRowGrid(m, selected, state, well, OptionOf(m));
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            ModSlots.PartyRow(row, m, modColumns, PartyRowColours.Fixed, id => _monsters.FirstOrDefault(x => x.Id == id), Lookup);
            // Two different commands on one row, and the original keeps them apart:
            //
            //   lblName_Click   -> mdrmulSwitch  (MULTIMOD.BAS:605) - who the commands are for
            //   lblName_DblClick -> sub1536      (MULTIMOD.BAS:654) - that, and repaint everything
            //
            // The help draws the same line: ALT-<n> "only changes who the current commands are
            // for", while Switch To "updates all of the informational windows". lblHits, lblSpells,
            // lblStatus and lblOption all forward to lblName, so the whole row answers.
            // Tapped, not PointerPressed. VB raises Click when the button goes down and comes back
            // up on the same control, and starts a drag from MouseMove instead - so a drag never
            // fires Click. Switching on the press ran RefreshParty in the middle of the drag,
            // which rebuilds every row and takes the dragged control out from under the mouse;
            // the swap then landed about one time in two.
            row.Tapped += (_, e) =>
            {
                SwitchCommandsTo(which);
                Focus();
                e.Handled = true;
            };
            row.DoubleTapped += (_, e) =>
            {
                // lblName_DblClick is sub1536 Index outright (FRMGROUP.BAS:181-183), whose caption
                // names the character only in a party (MULTIMOD.BAS:686-690). The Switch To button
                // and Alt-S run it only "If gv1A82 <> Me", which is _shownSlot against the selection
                Sub1536(which);
                Focus();
                e.Handled = true;
            };
            WirePartyRow(row, which);
            PartyRows.Children.Add(row);
        }
    }

    /// <summary>
    /// 'g' Give Item/Gold, <c>fn1521</c> case 71 (MAINMOD2.BAS:3705-3730): "Character's Name?",
    /// "How much Gold?" and "Which Item #?", then sub14B5 - gold and an item may go in the one
    /// command. See <see cref="GiveFlow"/>; the town asks the same.
    /// </summary>
    private async void AskToGive()
    {
        bool gave = await GiveFlow.Run(DialogOwner, _party.Members, _ch, Lookup, FiledFor, Say, Tell);
        if (!gave) return;
        SaveChar(); RefreshPack(); RefreshParty();
    }

    /// <summary>Alt+1..4 and the party keys, which only mean anything with more than one member.</summary>
    private bool HandlePartyKey(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return false;

        int slot = e.Key switch { Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, _ => -1 };
        if (slot < 0 && e.Key == Key.S)
        {
            // ALT-s "Switch to Character (updates all windows)" is
            // "If gv1A82 <> Me Then sub1536 Me" (MAINMOD2.BAS:3416-3418): it does not move to
            // anybody, it brings every window onto the character already in command. _shownSlot is
            // gv1A82; when it already matches, the repaint does no harm. It cycled to the next member, and said
            // "You are alone." or "X steps up.", none of which is the original's.
            if (_shownSlot != _party.SelectedIndex) Sub1536(_party.SelectedIndex); else SwitchEverythingTo(_party.SelectedIndex);
            RefreshSheetSpells();
            return true;
        }
        if (slot >= 0)
        {
            // fn15DF (MAINMOD2.BAS:4222-4230): in a party, and only for a slot that exists, the
            // number goes to lblCall and the Party window switches to it. Silently; an empty slot
            // is ignored rather than refused with "Nobody is in that slot."
            if (_party.Members.Count > 1 && slot < _party.Members.Count) SwitchCommandsTo(slot);
            RefreshSheetSpells();
            return true;
        }

        switch (e.Key)
        {
            // The same rule as the Options button, and a different sentence for it: the button
            // says "while in Combat!" (FRMGROUP.BAS:41) and the key says "while monsters are in
            // the area!" (MAINMOD2.BAS:4059). Both are the original's.
            case Key.O:
                if (MonstersHere) { Say("Cannot set default options while monsters are in the area!"); return true; }
                ShowPartyOptions(); return true;
            case Key.P:
            {
                // the order and the wording are fn1521's: combat first, then the size of the
                // party, and only then the pooling. Whether any gold actually moved is not asked.
                // all three are sub085B lines (MAINMOD2.BAS:3638, 3649, 3651) - the Messages window,
                // not lblSquareInfo, where the port had put them and nowhere else
                if (_combat != null && !_combat.IsOver)
                {
                    Log(Party.CannotPoolInCombat);
                    return true;
                }
                if (_party.Size == 1) { Log(Party.NobodyToPoolTo); return true; }
                _party.PoolGold();
                Log(Party.PoolCompleted(_ch));
                SaveChar(); RefreshParty(); Redraw(sub0956: false);
                return true;
            }
            case Key.L:
                AskToLeave();
                return true;
        }
        return false;
    }

    /// <summary>
    /// ALT-l and the Leave button: <c>sub159D</c> (MULTIMOD.BAS:739-758). Two refusals, then a
    /// question, and then the removal says nothing at all - neither mdrcharUnload nor sub1776
    /// writes a line. The leader is refused, which is also what a lone character hears.
    /// </summary>
    private async void AskToLeave()
    {
        var leaving = _ch;
        if (_level > 0 && MonstersHere)
        {
            Say($"\"{leaving.Name.TrimEnd()}\" cannot leave while monsters are in the area!");
            return;
        }
        if (_party.SelectedIndex == 0) { Say("Leader cannot leave Party!"); return; }
        if (!await Confirm($"Ask \"{leaving.Name.TrimEnd()}\" to Leave?", "Leave")) return;
        if (!_party.Remove(leaving)) return;
        // they stay where they are, as a character left behind in the dungeon does
        leaving.DungeonLevel = _level; leaving.X = _nav.X; leaving.Y = _nav.Y;
        leaving.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(leaving.Name) + ".json"));
        // sub1776's "frmParty!lblCall = "UPDATE"" (MULTIMOD.BAS:755, 701-702): anyone at nought leaves too
        // (FRMGROUP.BAS:321-346), before mdrmulSwitch and sub1536 (756-757)
        PruneTheDead();
        _captionNamed = false;   // "mdrmulSwitch Me : sub1536 Me" (MULTIMOD.BAS:756-757) - sub1536's caption last
        _shownSlot = _party.SelectedIndex;
        RefreshParty(); RefreshSheetSpells(); Redraw(sub0956: false);
    }

    private int MonsterGuildLevel(Companion c) =>
        MonsterOf(c)?.GuildLevel ?? 1;

    /// <summary><c>m0038</c>: the spells of the chosen class, as the list shows them.</summary>
    private List<Spellbook.SpellRow> _sheetSpells = new();

    /// <summary>
    /// What listing the spells does to the shown character's order. cmbClass_Click asks
    /// <c>mdrcharCheckCast(Me, spell, False, True)</c> of every spell it lists (FRMCHAR3.BAS:112)
    /// and sub2424 of every row it writes (FRMCHAR3.BAS:699, from vsbSpells_Change :912 and the end
    /// of cmbClass_Click :142), and mdrcharCheckCast opens with <c>CombatOrders(pv00FC).Att = False</c>
    /// (MAINMOD2.BAS:433) - so a Fight or Defend order is dropped each time the list is written
    /// with a spell in it, in a fight or out of one. The callers are the original's: a class picked
    /// (cmbClass_Click), the list scrolled (vsbSpells_Change), and sub1658, which re-clicks the
    /// class box, run by sub1536 (MULTIMOD.BAS:675-677) and by sub15CD after an item's stat
    /// change (MAINMOD2.BAS:6230). The port's other repaints of the list are its own and leave the
    /// order alone. The list must be current for <see cref="_ch"/>, the original's Me.
    /// </summary>
    private void SpellListChecked()
    {
        if (_sheetSpells.Count == 0) return;   // "No Spells Yet": no spell is listed, so nothing is checked
        if (_party.OrderFor(_ch).CheckCastClearsAtt()) RefreshParty();
    }

    private void RefreshSheetSpells()
    {
        // sub1658 fills the class box (MAINMOD2.BAS:5510-5539), cmbClass_Click lists the class
        // (FRMCHAR3.BAS:55-140) and sub2424 writes the rows (FRMCHAR3.BAS:681-712). Only the
        // character's own spells, from the guilds' spell tables: an item's
        // spell is reached through the item - the pack's Use, a double-click on it
        // (lblItem_DblClick, FRMCHAR3.BAS:517-519, into sub15CD) and a buffer holding its slot.
        // Nothing is left out for being a combat or a non-combat spell; the list used to hide the
        // combat ones outside a fight, which the original does not do - it lists them and the cast
        // refuses what cannot be thrown ("X is a Combat Spell!").
        SpellPage.FillClasses(SheetClassBox, _ch);
        _sheetSpells = Spellbook.ClassSpells(_ch, _spells, SpellPage.Kind(SheetClassBox));
        SheetSpellBox.ItemsSource = SpellPage.Rows(_sheetSpells, _ch, Lookup);

        // test hook: preselect a spell by name, so a prompt path can be driven without clicking
        string want = Environment.GetEnvironmentVariable("MORDOR_TEST_SPELL") ?? "";
        if (want != "")
        {
            SheetTabs.SelectedIndex = 1;   // and show the page it is on
            int i = _sheetSpells.FindIndex(c => c.Spell.Name.Trim() == want);
            if (i >= 0) { SheetSpellBox.SelectedIndex = i; SheetSpellText.Text = _sheetSpells[i].Spell.Name; }
            // in another class: choose that class, whose SelectionChanged lists it and comes back here
            else if (_spells.FirstOrDefault(sp => sp.Name.Trim() == want) is { } wanted
                     && SheetClassBox.Tag is List<int> kinds && kinds.IndexOf(wanted.Category) is int k and >= 0
                     && SheetClassBox.SelectedIndex != k)
                SheetClassBox.SelectedIndex = k;
        }
    }

    /// <summary>
    /// The sheet's Cast (and the c key): <c>cmdCast_Click</c> (FRMCHAR3.BAS:150-186). It reads
    /// txtSpell, not the list: nothing typed or a dead caster and it leaves; fn1725 looks the name
    /// up among every spell and says why on a miss (then sub0775, the focus back); the box takes the
    /// full name, and the spell goes as it sends it (174-186), whose gate refuses one you do not know.
    /// </summary>
    private void OnSheetCast(object? s, Avalonia.Interactivity.RoutedEventArgs e) => CastTyped();

    private void CastTyped()
    {
        string typed = SheetSpellText.Text ?? "";
        if (typed == "" || _ch.IsDead) return;               // 164-166
        int i = Spellbook.FindByName(_spells, typed, Log);  // fn1725, 167-168
        if (i == -1) { Focus(); return; }                    // sub0775, 169-172
        SheetSpellText.Text = _spells[i].Name;               // 173
        _ = CastThenSub0775(_spells[i]);
    }

    /// <summary>
    /// cmdCast_Click's cast and then <c>sub0775</c>, gv1BFC.SetFocus - after a spell that became the
    /// order (FRMCHAR3.BAS:183) and after one cast at once (187) alike: the keyboard goes back to
    /// the main window. The port had left it in txtSpell after Enter there, and Objects &amp; Spells
    /// keeps every key but Escape in a text box (310-318), so Alt+1..4 and the movement keys did
    /// nothing until Escape or a click elsewhere.
    /// </summary>
    private async System.Threading.Tasks.Task CastThenSub0775(Spell spell)
    {
        try { await CastKnownSpell(spell); }
        finally { Focus(); }
    }

    /// <summary>
    /// A double-click in the list, <c>lblName_DblClick</c> (FRMCHAR3.BAS:617-620): the name into
    /// txtSpell, then cmdCast_Click. The list holds only the character's own spells (sub2424).
    /// </summary>
    private void OnSheetSpellDoubleTapped(object? s, Avalonia.Input.TappedEventArgs e)
    {
        int i = SheetSpellBox.SelectedIndex;
        // an empty list's "No Spells Yet" row sits in a disabled elaSpells and takes no
        // double-click (FRMCHAR3.BAS:723-724)
        if (i < 0 || i >= _sheetSpells.Count) return;
        SheetSpellText.Text = _sheetSpells[i].Spell.Name;
        CastTyped();
    }

    /// <summary>
    /// A spell of your own, from the sheet's Cast or a buffer key - the two send it the same way.
    /// With monsters engaged, or waiting and the spell a combat or an either one, it becomes the
    /// order and mdrcharCheckCast only speaks (FRMCHAR3.BAS:174-184, MAINMOD2.BAS:4151-4157);
    /// otherwise it is <c>mdrcharCastSpell(Me, spell, 0, -1)</c> (FRMCHAR3.BAS:186,
    /// MAINMOD2.BAS:4159) - the gate and then the effect.
    ///
    /// The buffer had skipped the gate - no points test, no anti-magic refusal, no level test - and
    /// sent every map spell to the heal box. Both routes are this one now.
    /// </summary>
    private async System.Threading.Tasks.Task CastKnownSpell(Spell spell, bool fromBuffer = false)
    {
        if (BecomesOrder(spell)) { await CastInCombat(new Spellbook.Castable(spell, null, null, 0), fromBuffer); return; }
        var before = Spot;   // a spell that moves the party redraws the maze (MAINMOD2.BAS:1209-1210, 1347-1349, 5900)
        var (cast, msg) = await CastSpellHere(spell, itemLevel: 0);
        if (cast && SpellEffect.Of(spell) != SpellEffect.Passwall) Audio.PlaySpell(spell);   // sub1005, after the Select (1396-1403)
        SaveChar();
        ShowCastLine(msg);
        Redraw(sub0956: Spot != before);
    }

    /// <summary>
    /// What a cast said: a sub085B line, so the Messages window (Log) and nowhere else. It used to
    /// go to a message line of this port's under Objects &amp; Spells as well, or into the square's
    /// label - lblSquareInfo, which no sub085B line reaches.
    /// </summary>
    private void ShowCastLine(string msg)
    {
        RefreshCompanions(); RefreshPack(); RefreshSheetSpells(); RefreshBuffers();
        if (msg != "") Log(msg);
    }

    /// <summary>
    /// The test cmdCast_Click, fn1521's buffer keys and sub15CD all make before anything is thrown
    /// (FRMCHAR3.BAS:174, MAINMOD2.BAS:4151, 6129): <c>fn0EEF() &gt; 0 And (Engaged = 1 Or
    /// (Engaged = 0 And (fn149E = combat Or fn149E = either)))</c>.
    /// </summary>
    private bool BecomesOrder(Spell spell) =>
        MonstersHere && (Engaged || CastGate.UseOf(spell) != SpellUse.NonCombat);

    /// <summary>
    /// <c>mdrcharCastSpell</c>'s return value (MAINMOD2.BAS:1610): set when the cast ran to the end
    /// of the Select, left false by every <c>Exit Function</c> on the way - a cancelled box, a
    /// refusal, a death by Teleport, Displacement or Ethereal Portal. sub15CD spends an item's
    /// charge only when it is true (6140).
    /// </summary>
    private bool _spellCast;

    /// <summary>
    /// The non-combat branch of <c>mdrcharCastSpell</c> (MAINMOD2.BAS:826-1403) for the character on
    /// screen: <c>mdrcharCheckCast</c> first (830-835), then the effect. <paramref name="itemLevel"/>
    /// is <c>p01AA</c>, an item's own spell level: 0 for a spell of your own, and 0 for an item of
    /// level 0, which is then gated and charged like one (439-502, 1355).
    /// </summary>
    /// <remarks>
    /// <paramref name="caster"/> and <paramref name="preset"/> are for a mod's cast
    /// (<see cref="ModCast"/>): a party member other than the one in command, and the target the
    /// "Cast on Who?" box would have been answered with. Left null - every cast by hand - the caster
    /// is the one in command and the box is asked, as before. Only <see cref="SpellThrown"/>'s cases
    /// take another caster or a preset target; the rest (map spells, raises, binds, locks) ask
    /// things of their own and stay the one in command's.
    /// </remarks>
    private async System.Threading.Tasks.Task<(bool Cast, string Said)> CastSpellHere(Spell spell, int itemLevel,
        Character? caster = null, SpellThrown.Target? preset = null)
    {
        var who = caster ?? _ch;
        if ((who != _ch || preset != null) && !SpellThrown.Handles(spell)) return (false, "");
        // the caster is the one in command here (a cast by hand, or a mod's standing for one)
        if (CheckCast(spell, itemLevel, who, asCommander: true) is { } refused)
        {
            _lastRefusal = refused.Kind;   // for a mod's DungeonContext.LastRefusal; nothing else reads it
            return (false, refused.Text);
        }
        _spellCast = false;
        // "l01D0 = Int(Rnd * 100)" (MAINMOD2.BAS:840): mdrcharCastSpell draws it once its check has
        // passed, before the item's charge (841) and before any box the cast asks, and never reads it -
        // for the Cast button (FRMCHAR3.BAS:186), a buffer key (MAINMOD2.BAS:4159), an item's Use
        // (6140) and a mod's cast standing for one of those alike
        _ = _rng.NextDouble();
        string said = SpellThrown.Handles(spell) ? await CastAndThrow(spell, itemLevel, who, preset)
                                                 : await CastHere(spell, itemLevel);
        return (_spellCast, said);
    }

    /// <summary>
    /// The spell effects that need the dungeon around you, from the same Select Case in
    /// MAINMOD2.BAS: Ethereal Portal (9), Detect Rock (11), Depth Perception (12), Find Direction
    /// (13) and Sanctuary (17).
    /// </summary>
    /// <summary>
    /// <c>mdrcharCheckCast</c> (MAINMOD2.BAS:502-545) - everything the game refuses before a
    /// spell leaves your hands. See <see cref="CastGate"/> for the five refusals; this supplies
    /// the three things it needs to know about where you are standing.
    ///
    /// The port had one rule of its own here instead: an anti-magic square let the cast start,
    /// **took the spell points**, and said "You begin X, but the anti-magic field swallows it."
    /// The original says "Anti-Magic area!", spends nothing, and does it on an extinguish square
    /// too. It also forces the anti-magic bit onto your automap on the way out, which is how you
    /// find out a square is one.
    /// </summary>
    /// <remarks>
    /// <paramref name="asCommander"/>: the caster speaks as the one in command whoever they are -
    /// a mod's cast (<see cref="ModCast"/>) stands for switching to the caster and casting by hand.
    /// </remarks>
    private Refusal? CheckCast(Spell spell, int itemLevel, Character? caster = null, bool asCommander = false)
    {
        var who = caster ?? _ch;
        bool isYou = asCommander || who == _ch;   // "If pv00FC <> gv1A82 Then" picks the wording (MAINMOD2.BAS:447)
        bool antiMagic = _nav.Floor.Has(_nav.X, _nav.Y, Floor.ANTIMAGIC)
                      || _nav.Floor.Has(_nav.X, _nav.Y, Floor.EXTINGUISHER);
        bool monsters = _combat is { IsOver: false } || _waiting is { Count: > 0 };
        bool engaged = _combat is { IsOver: false };
        // p0100, an item's own level: not 0, and the points are not asked (MAINMOD2.BAS:502)
        int cost = itemLevel != 0 ? 0 : Spellbook.Cost(spell, who);

        // does not know it, spell level too low, a stat too low (437-497)
        if (CastGate.Unfit(who, spell, itemLevel, isYou, Lookup) is { } unfit) return unfit;
        var said = CastGate.Refuse(who, spell, cost, isYou, monsters, engaged, antiMagic);
        if (said?.Kind == RefusalKind.AntiMagic && !who.IsLost)
        {
            // mdrmazPerceiveSquare 18, True - forced, so it lands on the map whoever you are
            _map.Record(_level, _nav.X, _nav.Y, _nav.Floor.Width, Floor.ANTIMAGIC);
            _map.Note(_level, _nav.X, _nav.Y, _nav.Floor.Width, Floor.ANTIMAGIC);
            PushToAutomap();
        }
        return said;
    }

    /// <summary>
    /// The effects of the non-combat Select Case that are not <see cref="SpellThrown"/>'s: the
    /// map spells, the raises, the locks, the binds. <paramref name="itemLevel"/> is p01AA - the
    /// points are taken only when it is 0 (MAINMOD2.BAS:1355-1357), and only by a case that runs
    /// to the end of the Select: see <see cref="_spellCast"/>.
    /// </summary>
    private async System.Threading.Tasks.Task<string> CastHere(Spell spell, int itemLevel)
    {
        int effect = SpellEffect.Of(spell);
        int cost = itemLevel != 0 ? 0 : Spellbook.Cost(spell, _ch);
        // the charge after the Select, and mdrcharCastSpell = True (1355-1357, 1610)
        void Spend() { _ch.CurSpellPoints -= cost; _spellCast = true; }

        if (effect is SpellEffect.SoulSearch or SpellEffect.RetrieveSoul)
        {
            bool fetch = effect == SpellEffect.RetrieveSoul;
            // frmInteract's lists (FRMINTER.BAS:339-389), read from the character files as the
            // morgue list is. LOCATE offers everybody but the caster (382) - the rest of the party
            // included. FETCH offers only a body in the rock (DeathState 8) on the leader's level
            // whose area is nought (343-351); every rock death writes that area (MAINMOD2.BAS:1185,
            // 5885), and a body fetched out takes the caster's (1323), which drops it off the list.
            // The port had offered anyone outside the party to either spell. The typed refusals
            // ("Character is not on this level!", "Character is not in ROCK!", FRMINTER.BAS:178-190)
            // belong to the text box, which is shown only when the list is empty - and then greyed (435-455).
            var files = Character.LoadAllFrom(SavesDir).Where(c => c.Name != _ch.Name);
            var others = fetch
                ? files.Where(c => c.IsDead && c.CauseOfDeath == Rules.DeathRock
                                   && c.DungeonLevel == _party.Leader.DungeonLevel
                                   && SavedArea(c) == 0).ToList()
                : files.ToList();

            int pick = await NamePicker.Ask(DialogOwner, $"Cast \"{spell.Name.TrimEnd()}\"", fetch ? "Fetch Who?" : "Locate Who?",
                                            others.Select(c => c.Name.TrimEnd()).ToList(),
                                            Interact.NobodyFor(fetch ? "FETCH" : "LOCATE"));
            if (pick < 0) return "";                             // cancelled: nothing spent
            var them = others[pick];
            Spend();

            if (fetch)
            {
                // MAINMOD2.BAS:1319 also lets you fetch anyone "deeper than level 15", which no
                // character can be, so in practice it is your own level or nothing.
                // MAINMOD2.BAS:1319-1328, and nothing after: the case sets l01BA. Every map spell here
                // had "You cast X. " in front of the original's line, which the original never writes.
                if (them.DungeonLevel != _level && them.DungeonLevel <= SpellEffect.MaxDungeonLevel)
                    return $"\"{them.Name}\" is not within reach from here!";
                them.X = _nav.X; them.Y = _nav.Y; them.DungeonLevel = _level;
                // "l01B0.M59BB = Chars(m00A6).M59BB" (MAINMOD2.BAS:1323), m00A6 = 0: slot 0, the leader,
                // whoever cast it - X and Y come from the same slot (1321-1322)
                them.Area = _party.Members[0].Area ?? _nav.Floor.AreaIdAt(_nav.X, _nav.Y);
                them.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(them.Name) + ".json"));
                return $"Fetch of \"{them.Name}\" was successful!";
            }

            if (Math.Abs(them.DungeonLevel - _level) > SpellEffect.LocateLevelRange)
                return $"\"{them.Name}\" is too far away to locate.";
            if (them.DungeonLevel == 0)
                return $"\"{them.Name}\" is in the city.";

            var d = SpellEffect.RelativeDirection(_nav.X, _nav.Y, _level, _nav.Facing,
                                                  them.X, them.Y, them.DungeonLevel);
            // facing North the original's string has two spaces before "and down" (1296); the
            // other three facings have one
            string gap = _nav.Facing == Facing.North ? ",  and down " : ", and down ";
            return $"\"{them.Name}\" is right {d.Right}, forward {d.Forward}{gap}{d.Down}.";
        }

        if (SpellEffect.IsRaise(effect))
        {
            // The raise case (MAINMOD2.BAS:1061-1095) has no refusal for monsters on the square.
            // "Cannot raise character in Combat!", which the port put here, is the Ask to Join case's
            // (MAINMOD2.BAS:3479-3482), for a dead character being loaded into the party.

            // The spell's own refusal, before anything is asked (MAINMOD2.BAS:1062-1066): carrying any
            // character at all. ("Drop the character before you raise them!", which the port used
            // here, belongs to asking a dead character to join, 3484-3489.)
            if (_ch.CarryingCharacter != "")
                return $"\"{_ch.Name.TrimEnd()}\", drop who you're carrying to raise the dead.";

            // "Cast on Who?" under the tag SPELLRAISE (MAINMOD2.BAS:1067). frmInteract lists the bodies
            // on this floor and in this area, not yourself, not anyone a party member carries, and
            // not one a raise is already waiting on - morgueRaiseCost answers -1 for those
            // (FRMINTER.BAS:359-387) - and shows a button for each of the character's own
            // companions at nought hits (FRMINTER.BAS:397-405). The port had raised the first dead
            // party member on the spot, and the first dead companion of anybody in the party.
            int area = _nav.Floor.AreaIdAt(_nav.X, _nav.Y);
            var bodies = Character.LoadAllFrom(SavesDir)
                .Where(c => c.IsDead && c.Name != _ch.Name && !PendingRaise.IsPending(c)
                            && _party.Members.All(m => m.CarryingCharacter != c.Name)
                            && c.DungeonLevel == _level && c.DungeonLevel >= 1 && c.DungeonLevel <= _dungeon.Count
                            && SavedArea(c) == area)
                .ToList();
            // Ref > -1 And Hits = False (FRMINTER.BAS:400-402): a button per fallen companion, by slot,
            // captioned with the slot number alone, "&1".."&4" (FRMINTER.BAS:399)
            var deadSlots = _ch.Companions.Occupied.Where(o => o.Companion.Hits == 0).Select(o => o.Slot).ToList();
            var options = bodies.Select(c => c.Name.TrimEnd()).Concat(deadSlots.Select(i => (i + 1).ToString())).ToList();
            int pick = await NamePicker.Ask(DialogOwner, $"Cast \"{spell.Name.TrimEnd()}\"", Spellbook.CastOnWho,
                                            options, Interact.NobodyFor("SPELLRAISE"));
            if (pick < 0) return "";                         // m0078 = False: nothing cast, nothing spent

            if (pick >= bodies.Count)
            {
                int slot = deadSlots[pick - bodies.Count];
                var comp = _ch.Companions[slot]!;
                Spend();
                int con = MonsterOf(comp)?.Stats.ElementAtOrDefault(3) ?? 10;
                // MAINMOD2.BAS:1072-1087: "Raise Failed!" or "Raise Was Successful!" as a line, the
                // carried companion put down either way (M5943 = -1), and then the closing line on
                // the companion - l01BA is not set on this path. It is silent: no RAISE, which the
                // morgue's companion raise plays (FRMMORG.BAS:100) and this case does not.
                string said;
                if (_rng.Next(100) > SpellEffect.CompanionRaiseChance(con))
                {
                    _ch.Companions.Release(slot);           // Ref = -1: the slot is left empty
                    said = "Raise Failed!";
                }
                else
                {
                    comp.Hits = SpellEffect.RaisesToFull(effect) ? comp.MaxHits : 1;
                    said = "Raise Was Successful!";
                }
                _ch.CarryingCompanion = -1;
                Log(said);
                return SpellThrown.Line(_party, spell, new SpellThrown.Target.Companion(slot));
            }

            var body = bodies[pick];
            // morgueRaiseChance refuses outright before anything is written (MAINMOD2.BAS:2752-2764):
            // stone yields only to Restore Flesh, and someone pulled into rock only to Resurrect. The
            // case then sets l01BA, so no closing line - but the points are still spent, since
            // they come off after the Select (MAINMOD2.BAS:1355-1357). The port had refunded them.
            Spend();
            if (Rules.RaiseRefusal(body.CauseOfDeath, effect) is string no) return no;

            // Nothing is rolled here. sub0A7C (MAINMOD2.BAS:1091) writes the chance, the caster and
            // the spell's effect on the body and moves it to the caster's square; the roll is made
            // by fn16B5 when that character is next run and accepts. The port had rolled it on the
            // spot. The chance is off the body's own file - natural CON.
            int chance = Rules.SpellRaiseChance(body.Stats[(int)Stat.Con], effect, body.CauseOfDeath);
            PendingRaise.Initiate(body, _ch, chance, effect);
            body.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(body.Name) + ".json"));
            RefreshParty();
            // the closing line names the body from the morgue list, whatever the party's size
            // (MAINMOD2.BAS:1374-1375)
            return $"\"{spell.Name.TrimEnd()}\" thrown on \"{body.Name.TrimEnd()}\".";
        }

        if (Spellbook.OpensLockUpTo(spell) is var lockLevel && lockLevel > 0)
        {
            // MAINMOD2.BAS:1050-1051 reads Fight.ChestLock - the loaded area's own record, which
            // outlives the monsters. Once a fight is won the port lets go of _combat and keeps the
            // chest in _chest (as OpenChestBy reads it), so a lock cast at after the last monster
            // fell - which is when anyone casts one - had been told there was no chest. Corrected
            // 2026-10-02. A chest left in another area is not here (FightIsHere), as monOpenChest has it.
            var locked = _combat ?? (_chest != null && FightIsHere(_chest) ? _chest : null);
            if (locked == null || locked.ChestLocked <= 0) return "There is no locked chest here!";
            Spend();
            // MAINMOD2.BAS:1050-1060: opened or not, nothing but the closing line - l01B6 = -1,
            // so "\"Charm of Opening\" thrown." The port had "The lock holds." / "The lock springs open."
            if (locked.ChestLocked <= lockLevel)
            {
                locked.UnlockChest();
                // "monShowChest Chars(pv01A6).M59BB" (1058): the caption loses "Magically Locked".
                // ShowChest draws _combat, so a chest held only in _chest is shown through it, as
                // the arrival path does
                if (_combat != null) ShowChest();
                else { _combat = locked; try { ShowChest(); } finally { _combat = null; } }
            }
            return SpellThrown.Line(_party, spell, new SpellThrown.Target.Nobody());
        }

        if (Spellbook.IsBinding(spell))
        {
            // "Cast on Which Companion?" (MAINMOD2.BAS:888), frmInteract's COMP list: one button per
            // slot, numbered, for each living companion whose binding is not 0 (FRMINTER.BAS:413-427);
            // none, and the box says "No bound companions to select!". The port had picked the
            // least bound itself and refused with "You have no companion to bind.".
            var slots = _ch.Companions.Occupied
                .Where(o => o.Companion.Hits > 0 && o.Companion.BindingLevel != 0).Select(o => o.Slot).ToList();
            if (slots.Count == 0) return Interact.NoBoundCompanions;
            int pick = await AskToPick($"Cast \"{spell.Name.TrimEnd()}\"", "Cast on Which Companion?",
                                       slots.Select(i => (i + 1).ToString()).ToList());
            if (pick < 0) return "";
            Spend();
            return SpellEffect.BindCompanion(_ch.Companions[slots[pick]]!, slots[pick], spell, Spellbook.SpellLevelFor(_ch, spell));
        }

        if (effect is SpellEffect.Teleport or SpellEffect.Displacement)
        {
            bool withDepth = effect == SpellEffect.Teleport;
            string prompt = withDepth
                ? "Enter coords as follows (using +/-):\nRight,Fwds,Down(max 3)(ie -3,+2,-1)"   // MAINMOD2.BAS:1138
                : "Enter coords as follows (using +/-):\nRight, Forwards (ie +3,-2)";          // 1120
            string? typed = await AskFor($"Cast \"{spell.Name}\"", prompt);
            if (typed == null) return "";                       // cancelled: nothing spent
            var n = typed.Split(',').Select(p => int.TryParse(p.Trim(), out int v) ? v : 0).ToArray();
            if (n.Length < 2) return "";   // mdrcharTeleport leaves (5770) and nothing is cast or said

            var to = SpellEffect.TeleportTo(_nav.X, _nav.Y, _nav.Facing, _level,
                                            n[0], n[1], withDepth && n.Length > 2 ? n[2] : 0,
                                            _nav.Floor.Width, _nav.Floor.Height);
            Audio.PlayOnNextChannel("TELEPORT");

            // Both spells go through mdrcharTeleport (MAINMOD2.BAS:1125 and 1143), so both can end
            // the two ways it ends. Asking to go up from level one puts you above the city and you
            // fall - the original tests `DungeonLevel <= False` and does not clamp its way out of
            // it, which the port had been doing. Either way it answers mc0232 and the case leaves
            // with m0078 = False before the charge (1126-1128, 1144-1146): a death costs no points.
            // The tests are on the destination, before anything of the arrival runs: the rock
            // branch never reaches sub0835 or mdrmazShowMaze, which only the Else runs
            // (MAINMOD2.BAS:5860, 5896-5900) - so no area settled and no "first to go to level N",
            // which is sub0835's (MAZEMOD.BAS:1632-1637). The bodies stay on the rock square.
            // fn0A0B comes between the new level and these tests (MAINMOD2.BAS:5823): in the
            // limited build a level past its last is denied and the last one tested instead
            var (spellLevel, spellDenied) = LimitedVersion.Clamp(to.Level);
            if (spellDenied != null) { Log(spellDenied); to = (to.X, to.Y, spellLevel); }
            switch (Teleporting.ForSpell(_dungeon, to.Level, to.X, to.Y))
            {
                case Teleporting.Outcome.FallsFromTheSky: FellOutOfTheSky(); return "";
                case Teleporting.Outcome.IntoRock: TeleportedIntoRock(to.Level, to.X, to.Y); return "";
            }
            // gv1D6A = Chars(m00A6).M59BB (MAINMOD2.BAS:5756), taken by the mdrmazShowMaze at the end
            // of the arrival (5900). The original sets it before the short answer's exit and the two
            // deaths too, where no mdrmazShowMaze follows (a dead one's leaves before clearing it,
            // MAZEMOD.BAS:772-775, and entering the dungeon clears it, FRMDUNM.BAS:255); the port
            // sets it only here, so it cannot fire on some later step.
            _forceSettle = true;

            if (to.Level != _level) ChangeLevel(to.Level, to.X, to.Y, settle: false);
            else _nav.SetPosition(to.X, to.Y);

            // The arrival is "sub0835 gv1D6C : mdrmazShowMaze" (MAINMOD2.BAS:5899-5900): the whole
            // square routine on the landing square - the lost rolls, the automap, and whatever the
            // square does, a pit or a chute or another teleporter - and then the area, which
            // ArriveBySpell does in that order. The port had repainted the label and
            // settled the area, and nothing on the square acted. sub0835 repaints lblSquareInfo -
            // the port's MsgText - and the case then sets l01BA, so no closing line (1130-1135,
            // 1148-1153).
            Spend();
            MsgText.Text = ArriveBySpell();
            PushToAutomap();                       // frmAutoMap!lblCall = "UPDATE" (5902-5904)
            return "";
        }

        switch (effect)
        {
            case SpellEffect.DepthPerception:
            case SpellEffect.FindDirection:
            {
                Spend();
                // 1262 and 1271: the line, then the leader's lost flag for the level (M59F4(0)) or
                // for the facing (M59F4(2)) goes, sub0848 hands the leader's three flags to everyone,
                // and mdrmazLookAround redraws (1263-1269, 1272-1278). The port had only answered.
                string said = effect == SpellEffect.DepthPerception
                    ? $"You are on level {_level}."             // Chars(pv01A6).DungeonLevel
                    : $"You are facing {_nav.Facing}.";        // gv06E8$ North..West
                var leader = _party.Leader;
                if (effect == SpellEffect.DepthPerception) leader.LostLevel = false;
                else leader.LostFacing = false;
                foreach (var m in _party.Members)
                {
                    m.LostLevel = leader.LostLevel; m.LostPosition = leader.LostPosition; m.LostFacing = leader.LostFacing;
                }
                ApplyLost();
                PushToAutomap();
                return said;
            }

            case SpellEffect.DetectRock:
            {
                Spend();
                // the square ahead, wrapped at the edges as 1232-1235 wrap it
                var (dx, dy) = DungeonNav.Delta(_nav.Facing);
                int ax = Wrap(_nav.X + dx, _nav.Floor.Width), ay = Wrap(_nav.Y + dy, _nav.Floor.Height);
                bool rock = _nav.Floor.Has(ax, ay, Floor.ROCK);
                // and it goes on the automap (1238-1256): rock, but only when the caster knows where
                // they are (M59F4(1) and M59F4(0) clear); not rock, bit 23 - gc1D50, the "been here"
                // bit, the black ground - whether lost or not. That square then counts as mapped,
                // so stepping onto it later finds you. Reproduced.
                if (rock)
                {
                    if (!_ch.LostPosition && !_ch.LostLevel)
                        _map.Record(_level, ax, ay, _nav.Floor.Width, Floor.ROCK);
                }
                else _nav.Explored.Add((ax, ay));
                PushToAutomap();
                // MAINMOD2.BAS:1237 and 1257. The two halves of this answer do not agree with
                // each other - "The square ahead is Rock." against "The ahead square is not
                // Rock." - and both are written out in full twenty lines apart, so neither is a
                // slip for the other in any way this port can settle. Both are kept.
                return (rock ? "The square ahead is Rock." : "The ahead square is not Rock.");
            }

            case SpellEffect.Passwall:
            {
                // Step one square in the facing direction whatever stands in the way, wrapping at
                // the map edge. If that square is solid rock it kills the whole party, exactly as
                // MAINMOD2.BAS:1180-1206 does - it is not a safe way through a wall.
                // Its sound comes first, before the case, rock or no rock (864-866); the end-of-cast
                // sound skips it (1398)
                Audio.PlaySpell(spell);
                // the caster is moved first and the rock tested where they now stand (1170-1184),
                // so a death leaves every body on the rock square
                bool rock = _nav.StepThrough();
                int nx = _nav.X, ny = _nav.Y;
                if (rock)
                {
                    WalkedIntoRock(nx, ny);   // and Exit Function, before the charge (1205-1206)
                    return "";
                }
                Spend();
                // "sub0835 Chars(m00A6).DungeonLevel : mdrmazShowMaze" (1209-1210): the whole square
                // routine where the caster comes out, then the area - ArriveBySpell
                MsgText.Text = ArriveBySpell();
                PushToAutomap();                   // "DRAW" and "UPDATE" (1211-1214)
                return SpellThrown.Line(_party, spell, new SpellThrown.Target.Nobody());   // l01B6 = -1 (1208)
            }

            case SpellEffect.Sanctuary:
            {
                if (!_ch.HasSanctuary) return "\"Set Sanctuary\" has not been cast!";
                Spend();
                int sx = _ch.SanctuaryX - 1, sy = _ch.SanctuaryY - 1, lvl = Math.Clamp(_ch.SanctuaryLevel, 1, _dungeon.Count);
                if (lvl != _level) ChangeLevel(lvl, sx, sy, settle: false);
                else _nav.SetPosition(sx, sy);
                // "sub0835 gv1D6C : mdrmazShowMaze" (1348-1349): the square routine where you land,
                // then the area - ArriveBySpell. The port had settled the area
                // and left the square's own effects and its label out.
                MsgText.Text = ArriveBySpell();
                // "sub086D mc0280" (1350): sound 52, SoundFiles(mc068C) = "WITHER.WAV" (MODULE1.BAS:360)
                Audio.PlayOnNextChannel("WITHER");
                PushToAutomap();                   // frmAutoMap!lblCall = "UPDATE" (1351)
                // mdrcharAddAge True, pv01A6, mc0282, False (1352): a year, and it says so - before
                // the closing line, which follows the Select
                string older = _ch.AgeBy(SpellEffect.SanctuaryDays, _party.Size > 1);
                if (older != "") Log(older);
                return SpellThrown.Line(_party, spell, new SpellThrown.Target.Nobody());   // l01B6 = -1 (1353)
            }
        }
        return "";
    }

    /// <summary>
    /// Ethereal Portal into rock (MAINMOD2.BAS:1184-1206). The loop there is
    ///
    ///     Do : mdrcharSetDead pv01A6, mc00B6 : sub0930 pv01A6 : Loop While UBound(Chars) > m00A6
    ///
    /// - always the caster's <b>slot</b>. Each sub0930 sends the party list UPDATE, which unloads
    /// the body and closes the gap (FRMGROUP.BAS:321-346), so the next member moves into that slot
    /// and dies in turn, with the prune's "\"X\" has died!" for each one on the leader's floor. So:
    ///
    /// - caster in slot 0 or 1: everybody from the caster on dies in the loop, and whoever is left
    ///   in slot 0 at line 1191; then "You walked into solid rock!" and the square goes on the
    ///   automap as rock whether you knew where you were or not (1193-1202).
    /// - caster in slot 2 or more: the caster and everyone after them die, and then the loop asks
    ///   mdrcharSetDead for a slot that is gone. Its handler is a bare <c>Resume</c>
    ///   (MAINMOD2.BAS:4586-4587), which retries the failing line for ever: the game hangs. <b>The one
    ///   deviation:</b> a hang cannot be ported, so here the deaths stop there, with the members
    ///   before the caster alive, and nothing after.
    ///
    /// Each mdrcharSetDead with the rock cause also writes rock on the automap at that body's
    /// square when the body knew where it was (4561-4568). The deaths are the rock death, the one
    /// only Resurrect undoes; every one runs sub0930, so sub048C with it (Kill). No points are
    /// charged: the case leaves by Exit Function (1205-1206).
    /// </summary>
    private void WalkedIntoRock(int x, int y)
    {
        var members = _party.Members.ToList();
        int k = Math.Max(0, members.IndexOf(_ch));   // pv01A6
        var victims = members.Skip(k).ToList();
        bool completes = k <= 1;
        if (completes) victims.AddRange(members.Take(k));   // slot 0, at line 1191

        foreach (var m in victims)
        {
            m.CauseOfDeath = Rules.DeathRock;
            m.AreaForRockDeath();   // "M59BB = False" (MAINMOD2.BAS:1185), and mdrcharSetDead again (4569-4574)
            // mdrcharSetDead's own rock write (4561-4568), for a body not lost
            if (!m.LostPosition && !m.LostLevel)
            {
                _map.Record(_level, x, y, _nav.Floor.Width, Floor.ROCK);
                PushToAutomap();
            }
            Kill(m, "");
        }
        if (!completes) return;   // the hang, see above

        const string said = "You walked into solid rock!";   // 1193
        Log(said);   // a sub085B line; the square's label keeps the dead screen's text
        _map.Record(_level, x, y, _nav.Floor.Width, Floor.ROCK);   // 1194-1202, lost or not
        PushToAutomap();
    }

    /// <summary>The original wraps a step off the edge of the 30x30 grid round to the other side.</summary>
    private static int Wrap(int v, int size) => v < 0 ? size - 1 : v >= size ? 0 : v;

    /// <summary>
    /// A plain message box with an OK button - the original's MsgBox with no choice in it.
    /// </summary>
    private async System.Threading.Tasks.Task Tell(string title, string body)
    {
        var ok = new Button { Content = "OK", IsDefault = true };
        var win = new Window
        {
            Title = title,
            Width = 560, SizeToContent = SizeToContent.Height,
            Background = Avalonia.Media.Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock { Text = body, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
                        ok,
                    },
                },
            },
        };
        ok.Click += (_, _) => win.Close();
        win.Opened += (_, _) => ok.Focus();
        UiFonts.MessageBox(win);
        await OriginalBox.ShowOver(win, DialogOwner);
    }

    /// <summary>
    /// The original's fn170D: a little modal that asks for a line of text. Combat keeps ticking
    /// behind it, which is the point - nothing in this game pauses for a prompt.
    /// Returns null if the player cancels.
    /// </summary>
    /// <summary>
    /// <c>a</c> Ask character to Join, fn1521 case mc0576 (MAINMOD2.BAS:3444-3523). The original asks
    /// for a name and a password; this port lists the characters it can see and puts each one's
    /// reason for refusing beside it, which is the same reading the city uses. The refusals
    /// themselves are the original's where it has them - see <see cref="Party.WhyCannotJoin"/>.
    ///
    /// In a fight too, as <c>fn1521</c> is reachable mid-combat: a dead joiner gets "Cannot raise
    /// character in Combat!" while monsters are here (<c>fn0EEF() &gt; False</c>, 3479-3483), and a
    /// living one is given the Combat Setting by <c>sub0BDE</c> (3512). The fight keeps going behind
    /// the boxes, as the fight's timer does through the DoEvents of frmInteract's wait (FRMINTER.BAS:488-492).
    /// </summary>
    private async void AskToJoin()
    {
        // "If Chars(m00A6).DeathState = False Then" and no else (MAINMOD2.BAS:3446): m00A6 is the constant 0
        // (17), the party's first member - not the one in command, whom the port had asked about
        if (_party.Members[0].IsDead) return;
        if (_party.Size >= Party.MaxSize) { Say("Your party is too full!"); return; }   // 3447-3449

        // frmInteract's JOIN list (FRMINTER.BAS:352-387): everyone not in the party on this floor and in
        // this area, the dead included
        int area = _nav.Floor.AreaIdAt(_nav.X, _nav.Y);
        var here = _party.HereToJoin(Character.LoadAllFrom(SavesDir), _level, area, _dungeon.Count, SavedArea);
        int pick = await NamePicker.Ask(DialogOwner, "Ask to Join", "Character's Name?",
                                        here.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("JOIN"));
        // the fight goes on behind the box (FRMINTER.BAS:488-492): the dungeon may be gone when it answers
        if (pick < 0 || _ended) return;
        var who = here[pick];
        if (who.HasPassword)
        {
            string? said = await PasswordBox.Ask(DialogOwner, "Ask to Join", "Enter Password");
            if (said == null || _ended) return;
            // MsgBox "Incorrect Password!", 64, "SORRY!" (MAINMOD2.BAS:3517)
            if (!who.PasswordMatches(said)) { await Tell("SORRY!", "Incorrect Password!"); return; }
        }
        if (_party.Members.Any(m => Party.Opposed(m.Alignment, who.Alignment)))
        {
            Say($"\"{who.Name.TrimEnd()}\" is oppositely aligned to one or more members of the party!");
            return;
        }
        // sub0848 True (MAINMOD2.BAS:3477): the newcomer takes Chars(0)'s three lost flags
        // (MULTIMOD.BAS:454-458), before the dead are turned away - and a refusal from here on
        // unloads them with those flags saved (mdrcharUnload, MAINMOD.BAS:419-423)
        Party.ShareLostFlags(_party.Members[0], who);
        string whoFile = System.IO.Path.Combine(SavesDir, Character.SafeFileName(who.Name) + ".json");
        if (who.IsDead)
        {
            // MAINMOD2.BAS:3478-3499, in this order: monsters here, the body in your arms, and then
            // a raise waiting on them is offered (fn16B5) or they are simply dead
            if (MonstersHere) { Say("Cannot raise character in Combat!"); who.Save(whoFile); return; }
            if (_ch.CarryingCharacter != "" && _ch.CarryingCharacter.TrimEnd() == who.Name.TrimEnd())
            { Say("Drop the character before you raise them!"); who.Save(whoFile); return; }
            if (!PendingRaise.IsPending(who))
            {
                Say($"\"{who.Name.TrimEnd()}\" is Dead!  Cast a Resurrect spell on the character to have them re-join!");
                who.Save(whoFile);
                return;
            }
            // they are being loaded into a party, so the offer names them (UBound(Chars) > m00A6)
            if (!await OfferPendingRaise(who, alone: false)) return;
        }
        // "Chars(UBound(Chars)).X = Chars(m00A6).X ..." (3507-3509) - they step onto your square; the
        // list was by area (FRMINTER.BAS:201), and fn1521 makes no square check of its own. The
        // party's stored X/Y only follow _nav on an area change, so they are brought up to date
        // first, or Party.Add's same-square test would refuse the join silently.
        // A port necessity, not a rule: the fight went on behind the boxes, so before the party is touched
        // the window must still be up, with somebody in it, and the newcomer not already among them
        if (_ended || _party.Members.Count == 0 || _party.Members.Any(m => m.Name.TrimEnd() == who.Name.TrimEnd())) return;
        SavePos();
        who.DungeonLevel = _party.Leader.DungeonLevel; who.X = _party.Leader.X; who.Y = _party.Leader.Y;
        // a refusal here unloads them too, saved as they stand (mdrcharUnload, MAINMOD.BAS:419-423)
        if (_party.WhyCannotJoin(who) is string refused) { Say(refused); who.Save(whoFile); return; }
        _party.Add(who);
        // sub154A (MAINMOD2.BAS:3511) runs mdrcharGetCarry for the newcomer (MULTIMOD.BAS:380): a
        // fallen companion they brought is picked up, and any others let go
        foreach (string line in CompanionCarry.Run(who, who == _party.Selected, MonsterOf)) Say(line);
        // and then "frmParty!lblCall = "UPDATE"" (MULTIMOD.BAS:382): anyone at nought leaves (FRMGROUP.BAS:321-346)
        PruneTheDead();
        Sub0BDE(who);   // "If fn0EEF() > False Then sub0BDE UBound(Chars)" (MAINMOD2.BAS:3512)
        SaveChar();
        // and nothing is said: the join case writes no line on success (3496-3511)
        RefreshParty(); RefreshSheetSpells(); Redraw(sub0956: false);
    }

    /// <summary>
    /// fn16B5 (MAINMOD2.BAS:314-380) for somebody being asked into the party: the raise another
    /// character cast on them, offered in a RAISE box, rolled only on Yes. True when they are on
    /// their feet. On a No the join case unloads the character, and mdrcharUnload saves it as it
    /// stands (MAINMOD.BAS:419-423): an ordinary raise is still waiting next time, a rocked
    /// body's chance - zeroed before the box - is wiped. A failure clears it through raiseComplications.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> OfferPendingRaise(Character who, bool alone)
    {
        PendingRaise.Prepare(who);
        string file = System.IO.Path.Combine(SavesDir, Character.SafeFileName(who.Name) + ".json");
        Audio.Beep();                                       // fn16B5's Beep (MAINMOD2.BAS:336)
        if (!await Confirm(PendingRaise.Offer(who, alone), PendingRaise.OfferTitle)) { who.Save(file); return false; }
        var outcome = PendingRaise.Accept(_rng, who, RaceData.ByName(who.Race), Lookup, alone, out string by);
        foreach (string line in outcome.Lines)
        {
            if (line == "Success!") Audio.PlayOnNextChannel("RAISE");     // sub086D mc00BE, just before the box
            // sound 38 is POISON.WAV in the SoundFiles table (MODULE1.BAS:325); raiseComplications plays it before the box (MAINMOD2.BAS:1652)
            if (line == "There are complications...") Audio.PlayOnNextChannel("POISON");
            if (PendingRaise.BoxTitleFor(line) is string title) await Tell(title, line);
            else Say(line);
        }
        if (!outcome.Raised) { who.Save(file); return false; }
        // sub0578 - the guild's line (MAINMOD2.BAS:373)
        GuildLog.Append(SavesDir, GuildData.IndexOf(who.Guild.Guild), GuildLog.Wrote.Resurrected(who.Name, by));
        who.Save(file);
        return true;
    }

    private async System.Threading.Tasks.Task<string?> AskFor(string title, string prompt)
    {
        string? answer = null;
        var box = new TextBox { Width = 260 };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // fn170D's frmInteract, in its font through the Options fonts (UiFonts)
        var win = UiFonts.Set(new Window
        {
            Title = title,
            Width = 380, Height = 210,
            Background = Avalonia.Media.Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = prompt, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap },
                        box,
                        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
                    },
                },
            },
        }, DesignFonts.Interact, "lblQuestion");
        ok.Click += (_, _) => { answer = box.Text ?? ""; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => box.Focus();

        await OriginalBox.ShowOver(win, DialogOwner);
        return answer;
    }

    /// <summary>
    /// The original's "Locate Who?" / "Fetch Who?" list (<see cref="PickList"/>, shared with the mods' "Cast on Who?"). Returns the chosen index, or -1.
    /// </summary>
    private System.Threading.Tasks.Task<int> AskToPick(string title, string prompt, IReadOnlyList<string> options) =>
        PickList.Ask(DialogOwner, title, prompt, options);

    /// <summary>Everyone with a save who is not already in this party.</summary>
    /// <summary>
    /// "Cast on Who?" - <c>fn170D(... "Cast on Who?", "", tag)</c> (MAINMOD2.BAS:933). The box
    /// lists the party, and for a heal it also shows a button per companion the caster has,
    /// alive or dead: the death is only noticed after you pick one, and that is what "Companion
    /// is Dead!" is for (FRMINTER.BAS:397-411).
    ///
    /// Returns the party member or the companion, or (null, null) when it was cancelled. It is
    /// asked even of a lone character with no companions: fn170D always opens the box, and the
    /// party list in it then holds one name (FRMINTER.BAS:331-338). The port had skipped it.
    /// </summary>
    private async System.Threading.Tasks.Task<(Character? person, Companion? companion, bool cancelled)>
        AskCastOnWho(Spell spell, bool withCompanions)
    {
        var people = _party.Members.ToList();
        // a button per occupied slot, numbered by the slot, dead or alive (FRMINTER.BAS:398-410)
        var comps = withCompanions ? _ch.Companions.Occupied.ToList() : new List<(int Slot, Companion Companion)>();

        var options = people.Select(m => m.Name.TrimEnd()).ToList();
        foreach (var (slot, _) in comps) options.Add((slot + 1).ToString());   // cmdCompanion captions "&1".."&4" (FRMINTER.BAS:399)

        int pick = await AskToPick($"Cast \"{spell.Name.TrimEnd()}\"", Spellbook.CastOnWho, options);
        if (pick < 0) return (null, null, true);            // fn170D returns -1 and nothing is cast
        return pick < people.Count ? (people[pick], null, false) : (null, comps[pick - people.Count].Companion, false);
    }

    /// <summary>
    /// The non-combat spells that end with <see cref="SpellThrown"/>'s line: ask "Cast on Who?" if
    /// the case does (a cancelled box casts nothing and costs nothing), do it, spend the points -
    /// which the original takes after the effect (MAINMOD2.BAS:1355-1357) - and say where it went.
    /// A heal aimed at a dead companion is refused with "Companion is Dead!" (940).
    /// </summary>
    private async System.Threading.Tasks.Task<string> CastAndThrow(Spell spell, int itemLevel,
        Character? caster = null, SpellThrown.Target? preset = null)
    {
        // the caster: the one in command for every cast by hand; a mod's cast names another
        // member and hands the box's answer in as preset (see CastSpellHere)
        var who = caster ?? _ch;
        var ask = SpellThrown.WhoToAsk(spell);
        SpellThrown.Target target;
        if (ask == SpellThrown.Ask.None)
            target = SpellEffect.IsParty(spell) ? new SpellThrown.Target.Party() : new SpellThrown.Target.Nobody();
        else if (preset != null)
            target = preset;
        else
        {
            // the box lists the one in command's companions (AskCastOnWho): only a cast by hand asks
            if (who != _ch) return "";
            var (person, companion, cancelled) = await AskCastOnWho(spell, ask == SpellThrown.Ask.PeopleAndCompanions);
            if (cancelled) return "";
            if (companion != null)
            {
                if (SpellEffect.Of(spell) is SpellEffect.Damage or SpellEffect.Cure && companion.Hits < 1)
                    return Spellbook.CompanionIsDead;
                target = new SpellThrown.Target.Companion(who.Companions.SlotOf(companion));
            }
            else target = new SpellThrown.Target.Person(person ?? who);
        }
        // MAINMOD2.BAS:952-957 - a heal is thrown at p01AA, the item's level, and only when that is
        // nought at the current guild's spell level (not the best guild's), never below 1. The port
        // had healed from every item at level 1.
        int healLevel = itemLevel > 0 ? itemLevel : Rules.SpellLevel(who.Guild.Level);
        // the line names the caster as the one in command (for a cast by hand they are the same)
        string said = SpellThrown.Apply(_party, who, spell, target, healLevel, _rng, Lookup, _combat, commander: who);
        if (itemLevel == 0) who.CurSpellPoints -= Spellbook.Cost(spell, who);   // 1355-1357
        _spellCast = true;
        RefreshParty();
        return said;
    }

    /// <summary>
    /// <c>sub09CC</c> (MAZEMOD.BAS:348-380): who else is in the area and who is lying in it. See
    /// <see cref="RoomRoster"/>. It runs as the window opens (FRMDUNM.BAS:282) and from
    /// <see cref="SettleArea"/> when the area or the level has changed (MAZEMOD.BAS:798, 844) - an
    /// area, not a square: the port had matched other characters by their exact square and asked
    /// on every step, so a body lying elsewhere in the room went unmentioned.
    ///
    /// The counts are <see cref="WalkHere"/>'s, the same <c>mdrmazLookAround</c> walk the readout
    /// and the Look page use. Nothing gates it on being lost; the port's own guard is gone.
    /// Sound 63 is SoundFiles(mc06A2) = "YEECH.WAV" and 62 is mc06A0 = "HELLO.WAV"
    /// (MODULE1.BAS:106-107, 370-371).
    /// </summary>
    private void WhoElseIsHere()
    {
        var (_, living, dead) = WalkHere();
        int carrying = _party.Members.Count(m => m.CarryingCharacter != "");
        if (RoomRoster.DeadBodies(dead - carrying) is string bodies)
        {
            Log(bodies);
            Audio.Play("YEECH");                     // sub09DE mc0142 (MAZEMOD.BAS:367)
        }
        if (RoomRoster.Strangers(living - _party.Size) is string strangers)
        {
            Audio.Play("HELLO");                     // sub09DE mc0144 (MAZEMOD.BAS:371-372)
            Log(strangers);
        }
    }

    /// <summary>Use a charged item outside combat (healing potions, etc.).</summary>
    /// <summary>
    /// The Use command, <c>sub15CD</c> (MAINMOD2.BAS:6076-6215), in the original's own order: the
    /// guards first, then the spell the item holds, then the charge, then whatever else it does.
    /// The port used to accept only healing potions here.
    /// </summary>
    private void OnSheetUse(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the Use button, and a double click, which is lblItem_DblClick calling it (FRMCHAR3.BAS:519):
        // held fast, nothing (cmdCommand_Click (FRMCHAR3.BAS:203)). A buffered item goes to sub15CD
        // direct and has no such test - see UseSelectedItem.
        if (_ch.Paralysed) return;
        UseSelectedItem();
    }

    /// <summary><c>sub15CD</c> on the selected slot, which asks nothing about paralysis.</summary>
    private void UseSelectedItem()
    {
        var slot = SelectedPackSlot();
        if (slot == null) return;   // nothing selected: the original's routines just leave
        int idx = PackList.SelectedIndex;
        // no identification test: neither sub0F74 (equip) nor sub15CD (use) asks how well you know
        // the item - "You don't know what that is. ..." was this port's
        _itemsById.TryGetValue(slot.ItemId, out var it);
        if (it == null) return;

        if (ItemUse.Problem(_ch, slot, it, Lookup) is string why)
        {
            // an empty string is one of the original's silent refusals; the rest are sub085B lines
            // (MAINMOD2.BAS:6098-6123), so the Messages window has them as well as the sheet
            if (why != "") Say(why);
            return;
        }

        // an item that holds a spell casts it, and goes through the same machinery as casting one
        // you know - in combat that means picking a target, out of combat it happens here
        var spell = ItemUse.SpellOf(it, _spells);
        if (spell != null)
        {
            _ = UseItemSpell(slot, it, spell);
            return;
        }

        // sub1149 spends the charge before anything below is decided, which is why a misaligned
        // item can cost you one and do nothing
        var lines = new List<string>();
        Spellbook.SpendCharge(_ch, slot, it);
        var hall = HallRecords.Load(SavesDir);
        lines.AddRange(ItemUse.ApplySpecial(_ch, slot, it, hall, SavesDir, _party.Size > 1, Lookup));
        hall.Save(SavesDir);

        foreach (string line in lines)
            if (ItemUse.SoundIn(line) is string sound) Audio.PlayOnNextChannel(sound);
        // an item that does nothing visible says nothing: sub15CD has no "You use the X." line

        RefreshPack(); RefreshSheetSpells(); RefreshBuffers();
        // what the item does is said with sub085B, each its own line (MAINMOD2.BAS:6149-6225),
        // and only there - FRMCHAR3 has no message label of its own
        foreach (string line in lines.Where(l => ItemUse.SoundIn(l) == null && l != "")) Log(line);
        if (ItemUse.ChangesStats(_ch, slot, it)) SpellListChecked();   // sub1658 (MAINMOD2.BAS:6230)
        SaveChar();
        Redraw(sub0956: false);
    }

    /// <summary>
    /// The spell half of <c>sub15CD</c> (MAINMOD2.BAS:6127-6145), for the Use button, a buffered
    /// item and the sheet's Cast on an item's spell alike. With monsters engaged, or waiting and
    /// the spell a combat or an either one, it is the fight's business (6129-6138); otherwise
    /// <c>mdrcharCastSpell(ch, spell, M5734, -1)</c> - the gate and the effect at the item's own
    /// level, whatever the effect is, map spells included (6140). Only a cast that ran to the end
    /// spends the charge; then the rest of sub15CD runs (6144-6225).
    ///
    /// The port had said "Nothing Happens." and spent a charge for an item holding a map spell,
    /// healed from every item at level 1, and never charged spell points for an item of level 0.
    /// </summary>
    /// <remarks>
    /// <paramref name="caster"/> and <paramref name="preset"/>: a mod's Use (<see cref="ModUseItem"/>),
    /// as <see cref="CastSpellHere"/> has them; null for every Use by hand. Returns whether the cast
    /// went through. A mod's Use never reaches the combat branch: it is refused before, while
    /// monsters are present.
    /// </remarks>
    private async System.Threading.Tasks.Task<bool> UseItemSpell(InventorySlot slot, Item it, Spell spell,
        Character? caster = null, SpellThrown.Target? preset = null)
    {
        var who = caster ?? _ch;
        if (BecomesOrder(spell))
        {
            if (caster != null || preset != null) return false;
            await CastInCombat(new Spellbook.Castable(spell, slot, it, 0));
            return false;
        }

        var before = Spot;
        var (cast, msg) = await CastSpellHere(spell, itemLevel: it.SpellLvl, caster, preset);
        var lines = new List<string>();
        if (cast)
        {
            if (SpellEffect.Of(spell) != SpellEffect.Passwall) Audio.PlaySpell(spell);   // Passwall had its own at the start (MAINMOD2.BAS:864-866)
            Spellbook.SpendCharge(who, slot, it);   // mdrcharSubtractItemUse (6145), silently
            var hall = HallRecords.Load(SavesDir);
            lines.AddRange(ItemUse.ApplySpecial(who, slot, it, hall, SavesDir, _party.Size > 1, Lookup));
            hall.Save(SavesDir);
            foreach (string line in lines)
                if (ItemUse.SoundIn(line) is string sound) Audio.PlayOnNextChannel(sound);
        }
        // the cast's line, then sub15CD's own, each a sub085B of its own (MAINMOD2.BAS:6140-6225) -
        // they had been run together into one line with two spaces between
        ShowCastLine(msg);
        foreach (string line in lines.Where(l => ItemUse.SoundIn(l) == null && l != "")) Log(line);
        if (cast && ItemUse.ChangesStats(who, slot, it)) SpellListChecked();   // sub1658 (MAINMOD2.BAS:6230)
        SaveChar();
        Redraw(sub0956: Spot != before);
        return cast;
    }

    /// <summary>
    /// Drop, FRMCHAR3.BAS:227-246. It destroys the item - <c>sub0EDA</c> blanks the slot and nothing
    /// is left on the floor. Two things cannot be dropped: a guild crest, and anything cursed you
    /// are wearing. The original asks for confirmation between those two checks, so it will ask
    /// whether you are sure and only then tell you the thing is cursed; this does the same.
    /// </summary>
    private async void OnSheetDrop(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ch.Paralysed) return;   // cmdCommand_Click (FRMCHAR3.BAS:203): "If Ailments(mc0116) = False"
        var slot = SelectedPackSlot();
        if (slot == null) return;   // nothing selected: the original's routines just leave
        int idx = PackList.SelectedIndex;
        _itemsById.TryGetValue(slot.ItemId, out var it);
        if (it == null) return;

        if (ItemUse.DropProblem(slot, it, idx) is string why)
        {
            if (why != "") Log(why);   // sub085B (FRMCHAR3.BAS:231)
            return;
        }

        string shown = Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges);   // itemNameFor with all four (FRMCHAR3.BAS:234)
        if (!await Confirm($"Drop the {shown}?", "DROP ITEM")) return;

        if (ItemUse.CursedRefusal(slot, it, idx) is string cursed)
        {
            Audio.Beep();   // Beep (FRMCHAR3.BAS:236); OUCH.WAV was the port's
            Log(cursed);   // sub085B (FRMCHAR3.BAS:237)
            return;
        }

        // "mdrcharRemoveItem Me, (m001A)" (FRMCHAR3.BAS:240): the slot on screen. RemoveFromPackAt
        // counts carried items, hands and blanks left out, so slot n was item n+1 - it threw away
        // the thing below the one you had asked about
        _ch.RemoveFromPack(slot);
        Effects.Recompute(_ch, Lookup);
        RefreshPack(); RefreshSheetSpells(); RefreshBuffers();
        // "If m001A < mc0122 - 1 Then lblItem_Click m001A + 1" (FRMCHAR3.BAS:241-243): a drop
        // that went through moves the selection on to the next row
        if (idx < Character.PackSize - 1) PackList.SelectedIndex = idx + 1;
        // the drop itself is silent (FRMCHAR3.BAS:227-246); "It is gone." was the port's
        SaveChar();
        Redraw(sub0956: false);
    }

    /// <summary>A Yes/No box, the original's <c>MsgBox ..., vbYesNo</c>. True means Yes.</summary>
    private async System.Threading.Tasks.Task<bool> Confirm(string prompt, string title)
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
                        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { yesBtn, noBtn } },
                    },
                },
            },
        };
        yesBtn.Click += (_, _) => { yes = true; win.Close(); };
        noBtn.Click += (_, _) => win.Close();
        win.Opened += (_, _) => yesBtn.Focus();
        UiFonts.MessageBox(win);
        await OriginalBox.ShowOver(win, DialogOwner);
        return yes;
    }

    private void OnSheetClose(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SheetPanel.IsVisible = false;
        Focus();
    }

    /// <summary>
    /// fn0A34 on a square you are about to land on. This has to be asked *before* the move,
    /// because moving marks the square, and a marked square is by definition on the map.
    /// </summary>
    private bool Unmapped(int level, int x, int y) =>
        !NavFor(Math.Clamp(level, 1, _dungeon.Count)).HasMapped(x, y);

    /// <summary>The automap only records while you know where you are (MODULE3.BAS:1574).</summary>
    private void ApplyLost()
    {
        // MODULE3.BAS:1494-1498 - the write to the map file itself wants all THREE flags clear,
        // facing included, even though the "you are lost" tests elsewhere look only at two
        bool mapping = !(_ch.LostPosition || _ch.LostLevel || _ch.LostFacing);
        foreach (var (_, nav) in _navs) nav.Mapping = mapping;
    }

    /// <summary>
    /// Every step, if you are lost, three chances to work out where you are; and walking onto
    /// ground you have already mapped settles it outright. MAZEMOD.BAS:1541-1583, all of it on
    /// Chars(m00C0), the leader - not the character on screen.
    /// </summary>
    private void WorkOutWhereYouAre()
    {
        // the three rolls are gated on being lost (position or level); the found-yourself clear
        // at MAZEMOD.BAS:1569-1578 is not - it runs on any mapped square and also wipes a facing
        // flag left over on its own, which is otherwise the one state nothing else clears
        var leader = Leader;
        var said = Lost.TryToWorkItOut(leader, _rng, Lookup);
        if (_nav.HasMapped(_nav.X, _nav.Y) && Lost.OnKnownGround(leader) is string found)
            said.Add(found);

        // sub0848 False after each change hands the leader's three flags to everyone
        foreach (var m in _party.Members)
        {
            m.LostLevel = leader.LostLevel; m.LostPosition = leader.LostPosition; m.LostFacing = leader.LostFacing;
        }
        ApplyLost();
        _nav.MapHere();   // if they have just worked it out, this square is the first one recorded
        // sub085B lines, said before anything else on the square (1541-1583): to the list at once
        foreach (string line in said) Log(line);
    }

    /// <summary>Called after a successful step: handle chutes/teleporters, then roll for an encounter.</summary>
    private string AfterMove()
    {
        var fl = _nav.Floor;
        int x = _nav.X, y = _nav.Y;

        // Sickness and old age do not wait for you to move: they are on timAilment, a second
        // timer on the dungeon form. See AilmentTick.
        _cured = "";

        // The square's effects in sub0835's order, in one pass (MAZEMOD.BAS:1703-1893): water and
        // quicksand, the anti-magic and stud perceptions, the extinguisher, the facing bits, the
        // rotator - each perceived just before its effect - then for each member the extinguisher,
        // the pit and the stairs captions; only then the teleporter (1788) and the chute (1839),
        // and the end of the routine (1878-1896).
        //
        // Two things are kept apart, as the original keeps them: lblSquareInfo, which holds one
        // caption and is overwritten (the pit's, then the stairs' over it, 1766-1783) and is what
        // this returns, for the square's label only; and the Messages list's sub085B lines, which
        // are logged the moment they are said, so they land in the source's order.
        string caption = _cured;
        void Perceived(int bit) { string s = PerceiveBit(bit); if (s != "") Log(s); }
        string Compose() => caption.Trim();

        if (fl.Has(x, y, Floor.WATER) || fl.Has(x, y, Floor.QUICKSAND))
        {
            // The step into water or quicksand is a sound and nothing else: WADE (sound 45), unless
            // the leader is levitating (MAZEMOD.BAS:1703-1707). It says nothing - the words are the
            // ailment clock's "You are Drowning!" / "You are Sinking!" (FRMDUNM.BAS:500-528), which
            // WaterAndQuicksand already does. The port's two lines here had no source.
            if (!Leader.Levitating) Audio.PlayOnNextChannel("WADE");
        }

        // Fog does nothing on arrival. It costs you the square's **edges**, not the square.
        // MAZEMOD.BAS:1630 sets the mapped bit whether you are in fog or not, and it is only the
        // wall and door bits that sit behind `If testBit(BitDark, ...) = False` (1590-1596, 1646,
        // 1671) - which is why the help can say that "other map items (i.e. fog, water, etc)"
        // sometimes "look strange" under the grey explore squares. They are on the map to look strange.
        //
        // This port took the square back off the map altogether and said so in a sentence of
        // its own writing. The original says nothing at all: what fog does, it does to the
        // Dungeon window, which draws the FOG resource over the view (MAZEMOD.BAS:1359-1363).
        // Nor does it sound here: the port played FOG on stepping in, but the original's only
        // FOG is the 25% one on the ailment clock (FRMDUNM.BAS:589-591), in AilmentTick.

        // anti-magic and stud do nothing on arrival but may be noticed (1708-1709)
        if (fl.Has(x, y, Floor.ANTIMAGIC)) Perceived(Floor.ANTIMAGIC);
        if (fl.Has(x, y, Floor.STUD)) Perceived(Floor.STUD);

        // Neither the extinguisher nor the facing squares announce themselves - the original says
        // nothing at all, and noticing them is what perception is for. The extinguisher is
        // perceived and sounds here (1710-1713); its effect is in the member loop below.
        bool extinguish = fl.Has(x, y, Floor.EXTINGUISHER);
        if (extinguish) { Perceived(Floor.EXTINGUISHER); Audio.PlayOnNextChannel("EXTING"); }

        foreach (var (bit, face) in new[] { (Floor.FACE_NORTH, Facing.North), (Floor.FACE_EAST, Facing.East), (Floor.FACE_SOUTH, Facing.South), (Floor.FACE_WEST, Facing.West) })
            if (fl.Has(x, y, bit))
            {
                // perceived, then turned, silently (MAZEMOD.BAS:1714-1729). "An unseen force turns
                // you to face X." was this port's.
                Perceived(bit);
                while (_nav.Facing != face) _nav.TurnRight();
            }

        if (fl.Has(x, y, Floor.ROTATOR))
        {
            // after the facing bits (1730-1741), so on a square with both the spin has the last
            // word; perceived first, so its automap write sees you before the roll below can lose you.
            Perceived(Floor.ROTATOR);
            // MODULE3.BAS:1727 - an outright random facing, Int(Rnd * 4) + 1, not a count of turns
            _nav.FaceTowards((Facing)_rng.Next(4));
            // the original says nothing about the spin itself - only the line below, when your
            // perception fails to keep track (MAZEMOD.BAS:1725-1736)
            // and, unless your perception saves you, you lose track of where you are as well
            if (Lost.LoseDirection(Leader, _rng, Lookup))
            {
                foreach (var m in _party.Members) { m.LostPosition = true; m.LostFacing = true; }
                ApplyLost();
                Log(Lost.DirectionLostText(_party.Size > 1));   // the leader's roll (1732)
            }
        }

        // MAZEMOD.BAS:1742-1786, for each member: the extinguisher clears every magical protection
        // mask and all eleven protection values (1743-1751), and then the pit
        if (extinguish)
            foreach (var m in _party.Members) Effects.ClearSpellEffects(m, Lookup);

        bool anyFell = false;
        if (fl.Has(x, y, Floor.PIT))
        {
            // MAZEMOD.BAS:1743-1774, every member leader first: a Levitating one may float (the roll is
            // drawn only for them), anyone else falls for Int(Rnd * ((level + 1) / 2 * 15.5)) *
            // Log(size + 2) + 1, rounded into an Integer, and hits may go below nought. The one on
            // screen gets the square's caption and FALL then HIT; the others a line on the Messages
            // list each, and no sound. Nobody dies on the spot - see AfterAFall.
            foreach (var m in _party.Members.ToList())
            {
                if (m.Levitating && Rules.HoversOverPit(_rng, _level))
                {
                    if (m == _ch) caption = SquareInfo.HoveringOverPit;
                    continue;
                }
                if (m == _ch) { Audio.PlayOnNextChannel("FALL"); Audio.PlayAfterLast("HIT"); }
                int dmg = Rules.PitDamage(_rng, _level, (int)RaceData.ByName(m.Race).Size);
                if (m == _ch) caption = SquareInfo.FellIntoPit(dmg);
                else Log(SquareInfo.TheyFellIntoPit(m.Name, dmg));   // sub085B: the list only (1768)
                m.CurHits -= dmg;
                anyFell = true;
            }
        }
        // the stairs captions come after the pit in the same loop and write the same label
        // (1778-1784), so on a pit with stairs you are told about the stairs
        if (fl.Has(x, y, Floor.STAIRS_UP) || fl.Has(x, y, Floor.STAIRS_DOWN)) caption = _nav.Describe();

        if (fl.Has(x, y, Floor.TELEPORTER))
        {
            Audio.PlayOnNextChannel("TELEPORT");   // sub086D mc02A0 (1789), record or no record
            // and mdrmazPerceiveSquare BitTeleporter (1790): always noticed, always named, put on
            // the automap if you are not lost, and "You just hit a teleporter!" (MAZEMOD.BAS:628,
            // 636-651, 664-670) - unless perception messages are turned off (620). "You are
            // teleported!" was the port's own.
            Perceived(Floor.TELEPORTER);
            // No record for the square: the loop finds nothing, nobody moves, and the routine
            // carries on to the chute and its end as for any other square (MAZEMOD.BAS:1791-1838).
            if (fl.Teleporters.Any(t => t.X == x + 1 && t.Y == y + 1))
            {
                var tp = fl.Teleporters.First(t => t.X == x + 1 && t.Y == y + 1);
                // the limited build: a record leading past its last level does nothing but say so,
                // and the loop is left (MAZEMOD.BAS:1794-1796) - the routine goes on as for no record
                if (LimitedVersion.TeleporterIsDead(tp.Z2)) Log(LimitedVersion.TeleporterGoesNowhere);
                else return TeleporterSquare(tp, anyFell);
            }
        }

        // the limited build: a chute on its last level says so and nothing else (MAZEMOD.BAS:1840-1841)
        if (fl.Has(x, y, Floor.CHUTE) && LimitedVersion.ChuteIsDeadUnderfoot(_level))
            Log(LimitedVersion.ChuteDoesNothing);
        else if (fl.Has(x, y, Floor.CHUTE))
        {
            // after this step's own message, since the leader may be asked whether to follow. The
            // chute's own end of the routine (AfterAFall in ChuteFall) takes in a pit on the same
            // square too; the shipped maps have no chute square carrying anything else (selfcheck).
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ChuteFall(taken: false, x, y));
            return Compose();
        }

        if (fl.Has(x, y, Floor.PIT))
        {
            AfterAFall(anyFell);
            // a lone faller who died there goes no further: nothing after the check (1879-1884) spawns
            // anything. And the label is no longer the pit's: sub0930 writes lblSquareInfo over it -
            // "You are Dead.  You may be resurrected..." (MAINMOD2.BAS:3052), which OfferRescue
            // has just put up - before frmDungeonMain.Refresh (3057), so the fall's own caption is
            // never drawn. The port had put the pit's caption back over the dead one.
            if (_ch.IsDead) return _party.Size == 1 ? MsgText.Text ?? "" : Compose();
        }

        // monsters live in rooms, not in the spaces between steps; the encounter itself is not
        // announced (see ShowWaiting)
        SettleArea();
        return Compose();
    }

    /// <summary>
    /// A teleporter square with a record, after everything else on the square has been done
    /// (MAZEMOD.BAS:1788-1830), and the end of the square routine after it: sub0908 (1878), the
    /// lone check and the party list's UPDATE if anyone fell into a pit on the way (1879-1889),
    /// then sub0835 again on the new square (1891-1893) and mdrmazShowMaze (1894-1896).
    /// </summary>
    private string TeleporterSquare(Floor.Teleporter tp, bool anyFell)
    {
        _forceSettle = true;   // gv1D6A = Chars(gv1A84).M59BB for each member moved (MAZEMOD.BAS:1813)
        {
            // The square routine's teleporter (MAZEMOD.BAS:1788-1830), not mdrcharTeleport: it writes
            // X, Y and DungeonLevel for each member and tests neither rock nor level 0, so the party
            // is put on the destination as it is. Each member's square is decided first, then
            // fn0A34 asks of each whether it is mapped (1819-1825) - before the move, which marks it.
            var members = _party.Members.ToList();
            var dest = Teleporting.ForSquare(_dungeon, _level, tp, members.Count, _rng);
            for (int i = 0; i < members.Count; i++)
            {
                var (dl, dx, dy) = dest[i];
                if (!Unmapped(dl, dx, dy)) continue;
                members[i].BecomeLost();
                if (members[i] == _ch) Log(Lost.Realisation(members[i]));   // sub08E1 speaks for Me alone
            }

            // sub0908 (MAZEMOD.BAS:1878; MULTIMOD.BAS:52-71): anyone not on the leader's square leaves
            // the party where they landed - which a random teleporter, drawing for each member
            // separately, all but always does to a party
            var lead = dest[0];
            int sizeBefore = _party.Size;
            for (int i = 1; i < members.Count; i++)
            {
                var m = members[i];
                if (dest[i] == lead) continue;
                // sub0908 ends, once anyone has gone, in sub1536 gv1A84 (MULTIMOD.BAS:93-95) after
                // an UPDATE that leaves at once for a list of one (FRMGROUP.BAS:319-321): sub1536's
                // caption is the last word, plain "Dungeon" alone (MULTIMOD.BAS:686-690)
                _captionNamed = false;
                Log(Chute.Disappeared(m));
                bool wasShown = m == _ch;
                (m.DungeonLevel, m.X, m.Y) = dest[i];
                // M59BB of their own square (MAZEMOD.BAS:1818)
                if (dest[i].Level >= 1 && dest[i].Level <= _dungeon.Count)
                    m.Area = _dungeon[dest[i].Level - 1].AreaIdAt(dest[i].X, dest[i].Y);
                if (m.CurHits <= 0) { m.CurHits = 0; if (!m.IsDead) SetDead(m); }
                _party.Remove(m);
                if (wasShown) _party.Select(0);           // sub0908 hands the screen to the leader (MULTIMOD.BAS:78-84)
                _shownSlot = _party.SelectedIndex;   // M5E4F: sub1536 gv1A84 closes sub0908 (MULTIMOD.BAS:95)
                m.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(m.Name) + ".json"));
            }
            // "If l0012 = True Then ... frmParty!lblCall = "UPDATE": sub1776" (MULTIMOD.BAS:88-95): once
            // anyone has gone, the party list's UPDATE - anyone at nought left with the leader goes too
            // (FRMGROUP.BAS:321-346)
            if (_party.Size < sizeBefore) PruneTheDead();
            ApplyLost();

            if (lead.Level != _level) ChangeLevel(lead.Level, lead.X, lead.Y);
            else _nav.SetPosition(lead.X, lead.Y);
            RefreshParty(); RefreshSheetSpells();

            // the end of the routine (1879-1889), after sub0908: the lone check, which is not
            // conditional, and the party list's UPDATE when a pit on this same square set l0280
            AfterAFall(anyFell);

            // The teleporter sets l0282 and l0286 (MAZEMOD.BAS:1829-1830), so once sub0908 is done
            // the whole square routine runs again on the leader's new square (1891-1893) - a
            // chute, pit, water, rotator or another teleporter there acts in its turn - and only
            // after that returns does mdrmazShowMaze run (1894-1896). There is no guard: a fixed
            // teleporter landing on a teleporter fires that one too, and so on. A random one never
            // lands on a teleporter (1798-1808), and the shipped maps hold no loop (selfcheck).
            string square = RerunSquare();
            SettleArea();
            return square;   // the new square's label: the re-run blanks it first (1545)
        }
    }

    /// <summary>
    /// mdrmazShowMaze's two halves for wherever the party now stands, however it got there - a
    /// step, the stairs, a chute, a pit, a teleporter or a spell all end in it (MAZEMOD.BAS:1892-1895,
    /// MAINMOD2.BAS:3968-3969, 3988-3989, 5899-5900). If the area or the level differs from the one
    /// loaded: the old room is written back and an engaged group gets its leave round (795-818),
    /// then sub0A91 reads the new room (MAZEMOD.BAS:1468) and an engaged group there gets its
    /// arrival round (1469-1471). True if a group now stands waiting in front of you.
    /// </summary>
    private bool SettleArea()
    {
        var here = (_level, _nav.Floor.AreaIdAt(_nav.X, _nav.Y));
        // gv1D6A, set by a teleporter square, the Teleport spell and the chest's Teleporter trap,
        // forces the first half even within one area (MAZEMOD.BAS:798); mdrmazShowMaze clears it
        // (831) on whatever call comes next
        bool forced = _forceSettle;
        _forceSettle = false;
        if (here == _loadedArea)
        {
            if (forced && _loadedArea.Level >= 0) SameAreaReload();
            return false;
        }
        bool wiped = _loadedArea.Level >= 0 && LeaveArea();
        // "frmParty!lblCall = "UPDATE"" (MAZEMOD.BAS:827), whatever the leave round did (its own is 819):
        // anyone at nought leaves the party (FRMGROUP.BAS:321-346) before the save and sub0956 (832-842)
        PruneTheDead();
        if (wiped) { _loadedArea = here; SaveChar(); Arrived(); return false; }
        // A changed area or level is also when mdrmazShowMaze writes the party: m01C8 is set False
        // only then (MAZEMOD.BAS:797-800), and with it "If Not gv1A66.M5F80 Then m01C2 =
        // fn04ED(Chars(gv1A84))" for everyone (MAZEMOD.BAS:834-842) - after the old room is
        // written back and given its leave round (812-818), before the new one is read (1468).
        // The port wrote nothing on a walk from one area to the next, so a game that died took
        // you back to wherever the last stairs, fight or trade had left the record.
        SaveChar();
        Arrived();
        return LoadArea(arrivalRound: true);
    }

    /// <summary>
    /// The rest of that same block, before the new room is read (MAZEMOD.BAS:832-844): sub0956
    /// puts the leader's M59BB on everyone (832), each carrier's body is written to the carrier's
    /// square and area (sub0A7C, 839-841), and then sub09CC says who is in the area (844). It is
    /// not conditional on the leave round: a party that did not survive it still gets it.
    /// </summary>
    private void Arrived()
    {
        SyncAreas();
        DeliverCarried();
        WhoElseIsHere();
    }

    /// <summary>
    /// <c>gv1D6A</c>: a move that set it - the teleporter square (MAZEMOD.BAS:1813), the Teleport
    /// spell's mdrcharTeleport (MAINMOD2.BAS:5756) and the chest's Teleporter trap (MONSMOD.BAS:3569) -
    /// makes the next mdrmazShowMaze go through its change-of-area block even when the party has
    /// come down in the area it left (MAZEMOD.BAS:798). Ethereal Portal, Sanctuary and a walk do
    /// not set it; a level square does (MAZEMOD.BAS:113), but always changes the level.
    /// </summary>
    private bool _forceSettle;

    /// <summary>
    /// The change-of-area block run for the area the party is still in, because gv1D6A forced it
    /// (MAZEMOD.BAS:798-844): the room is written back (811), an engaged group gets its leave
    /// round (816-819), everyone is saved, a carried body is put down and sub09CC says who is here
    /// (832-844). The room is not read again: gv1D66 was not moved on (789-791), so the area test
    /// in front of sub0A91 finds the area unchanged (1467) - the fight goes on, with no arrival
    /// round. The port had skipped the whole block, so a teleport within one area cost no free
    /// round and said nothing of the bodies or the others in the room. Not modelled: the block
    /// also turns timCombat off (817, 828), and with sub0A91 skipped nothing in the routine turns
    /// it back on (1470-1472), so the original's fight waits for the next monShowMonsters to
    /// restart its clock (MONSMOD.BAS:4420); the port's clock goes on.
    /// </summary>
    private void SameAreaReload()
    {
        FinishWonFight();
        StoreRoom();
        if (_combat != null && _combat.Source != _room?.Records) StoreRoom(_combat);
        // "If Foes(0).Ref <> -1 And Foes(0).Engaged = 1" (816): a hostile group not yet fighting is
        // engaged too, as LeaveArea has it
        if (_combat == null && _waiting is { Count: > 0 } w && w[0].Hostile && w.Any(m => m.Alive))
        {
            _waiting = null;
            StartCombat(w, _waitingIsLair, monstersMoveNow: false);
        }
        if (_combat != null && !_combat.IsOver)
        {
            MonstersRound((AttackKind)2);
            RefreshCombat(); RefreshParty();
            if (_combat.IsOver) EndCombat();
        }
        // the block's UPDATE (MAZEMOD.BAS:827): anyone at nought leaves the party (FRMGROUP.BAS:321-346)
        PruneTheDead();
        // fn04ED for everyone (834-842) and the rest of the block, whether or not the party lived
        SaveChar();
        Arrived();
    }

    /// <summary>
    /// sub0A91 for the square the party stands on (MONSMOD.BAS:667-745). Timer is the machine's
    /// local clock; fn0D4D reads Chars(Me) and, for the identify skill, Chars(0) - the party's
    /// first member. The room's records are read back as they were left (fn0E38), or made afresh
    /// when they are due. The arrival round is mdrmazShowMaze's, and entering the dungeon has none
    /// (FRMDUNM.BAS:304 calls sub0A91 alone).
    /// </summary>
    private bool LoadArea(bool arrivalRound)
    {
        var fl = _nav.Floor;
        int x = _nav.X, y = _nav.Y;
        _loadedArea = (_level, fl.AreaIdAt(x, y));
        var group = EncounterGenerator.InRoom(_monsters, fl, _level, x, y, _dstate, DateTime.Now, _rng, out bool written, _ch, _party.Leader, Lookup);
        // fn0D4D Puts the room to the level's file as it makes it (sub0C79 at MONSMOD.BAS:1425,
        // fn0F88 at 1979), so dungeon.json is written now: the port had kept a new room in memory
        // until the next change of area, and a game killed before then lost it, chest and all
        if (written) _dstate.Save(SavesDir);
        _room = group != null ? (_level, fl.AreaIdAt(x, y), group) : null;
        // an area with nothing in it: whatever the last one showed - its group, its chest - stays
        // behind with it
        if (group == null && _combat == null) { _waiting = null; _encounterAt = (-1, -1, -1); HideMonsters(); }
        if (group != null)
        {
            // what fn0D4D put in the Messages window, and sub0A91's line when it returned False
            // (MONSMOD.BAS:711-712)
            foreach (var line in group.Messages) Log(line);
            if (group.Failed) Log("Error generating monsters!");
            // sub06FE for each group standing: as seen when just made (MONSMOD.BAS:717-721), and
            // again on every visit (730-734) - the Library's "last seen" date and place
            var foes = group.Foes.Where(f => f.GroupCount > 0).ToList();
            // the as-seen pass, when the groups were just made, then the every-visit pass - in
            // the original's order, through one read and write of the Library file
            var passes = (group.Fresh ? foes.Select(f => (f.Ref, f.KnownAs, f.GroupCount, true)) : Enumerable.Empty<(int, int, int, bool)>())
                         .Concat(foes.Select(f => (f.Ref, f.KnownAs, f.GroupCount, false)));
            FileSightings(passes, killed: false, _ch);
        }
        if (group != null && group.Count == 0 && _combat == null)
        {
            // nobody standing, but the Fight record still holds a chest nobody opened:
            // monShowChest p01AA (MONSMOD.BAS:728) puts it back up
            _fightingArea = fl.AreaIdAt(x, y);
            // picMonsters, where the chest is drawn, is part of the dungeon form; here it is the
            // combat panel, with nobody in it
            MonsterList.ItemsSource = null; MonsterStrip.Children.Clear(); StateIcon.Source = null;
            CombatTitle.Text = ""; CombatHit.Text = ""; CombatMonsterHit.Text = ""; WaitBtn.IsVisible = false;
            ShowWaitingChest(group);
            CombatPanel.IsVisible = ChestIcon.Source != null && !_combatClockOff;
            return false;
        }
        if (group == null || group.Count == 0) return false;
        _fightingArea = fl.AreaIdAt(x, y);
        // a lair monster carries a richer chest (MONSMOD.BAS adds 25 to its class-3 chance)
        _waitingIsLair = fl.AreaAt(x, y) is Floor.Area ar && ar.LairId > 0 && group[0].Def.Id == ar.LairId;
        _waiting = group;
        _encounterAt = (_level, x, y);
        foreach (var id in group.Select(m => m.Def.Id).Distinct())
            foreach (var m in _party.Members) m.NoteMonster(id, killed: false);
        // sub0A91 (MONSMOD.BAS:667-756) and what it calls - fn0E38, fn0D4D, sub06FE, sub0BDE,
        // sub0B9B, monShowChest, monShowMonsters, monShowCursor - and the arrival in mdrmazShowMaze
        // (MAZEMOD.BAS:1466-1487) play no encounter sound. The port's roar by monster kind here had
        // no source and is gone. The one sound there is: fn0D4D plays sound 68 as its class pick
        // draws monster 277 as the lead (MONSMOD.BAS:1649) - not when he comes as a lair monster,
        // and whether or not the pick then survives to be the lead of what you meet.
        if (group.AsmodeusCues > 0) Audio.Play(Asmodeus.Sound);
        // sub0A91: "CombatOrders(Me).M547A = False" (MONSMOD.BAS:683), then fn0DCD(-1) - the first
        // group standing (747)
        _party.OrderFor(_ch).Target = -1;
        ShowWaiting(group);
        ShowWaitingChest(group);
        // sub0A91 ends in sub0BDE -1 (MONSMOD.BAS:726), so the dungeon's Form_Load, which runs sub0A91
        // for the room a character is loaded into (FRMDUNM.BAS:303-304), hands a hostile group's room
        // the Combat Settings at once: with Foes(0).Engaged = 1 (fn0D4D, MONSMOD.BAS:1713-1721) each
        // member with no order - Att = False And M56C4 = -1 - takes theirs up, dead or alive, sub0BDE
        // asking nothing of DeathState (MULTIMOD.BAS:158-170). A walk in has its arrival round, whose
        // engage runs Sub0BDE; a load has none, and the dead never engage at all with the fight clock
        // off - so the Party window's Option said "None" where the original's says "Fight" (observed
        // 2026-10-26). Fight and Defend here; a setting on a buffer needs the fight's own checks and
        // waits for the engage, as before.
        if (!arrivalRound && group.Any(m => m.Hostile))
        {
            foreach (var m in _party.Members)
            {
                var o = _party.OrderFor(m);
                if (o.Kind != OrderKind.None || m.CombatSetting is not (1 or 2)) continue;
                int aimed = o.Target;
                o.ApplyDefault(m);
                o.Target = aimed;
            }
            RefreshParty();
        }
        if (arrivalRound) ArrivalRound(group);
        return true;
    }

    /// <summary>
    /// Death with no fight in progress (poison, a trap, old age). In a party this is not the end
    /// of the trip: MONSMOD.BAS's sub0930 updates the party window and returns, and only a lone
    /// character goes straight to the death screen. The body stays where it fell, for a raise.
    /// </summary>
    private void DieOutsideCombat(string why) => Kill(_ch, why);

    /// <summary>
    /// Teleported to level zero: the line goes up in a box and the whole party dies an ordinary
    /// death (MAINMOD2.BAS:5839-5857).
    /// </summary>
    private async void FellOutOfTheSky()
    {
        string said = Interact.FellFromTeleport(_party.Size > 1);
        foreach (var m in _party.Members.ToList())
        {
            m.CauseOfDeath = Rules.DeathOrdinary;
            Kill(m, said);
        }
        await Tell(Interact.FellFromTeleportTitle, said);
    }

    /// <summary>
    /// Teleported into rock. The square is written onto the automap as rock first - it is the
    /// one thing you learn from it - and then everybody dies the death only Resurrect undoes
    /// (MAINMOD2.BAS:5859-5893).
    /// </summary>
    private void TeleportedIntoRock(int level, int x, int y)
    {
        string said = Interact.TeleportedIntoRock(_ch, _party.Size > 1);
        if (level >= 1 && level <= _dungeon.Count)
        {
            var fl = NavFor(level).Floor;
            if (x >= 0 && y >= 0 && x < fl.Width && y < fl.Height)
            {
                _map.Record(level, x, y, fl.Width, Floor.ROCK);
                PushToAutomap();
                // mdrcharTeleport has already written the new X, Y and DungeonLevel when it tests
                // the square (5786-5833, 5860), so the bodies lie in the rock and are saved there.
                // Only the place changes: none of the arrival (sub0835, mdrmazShowMaze) runs.
                var facing = _nav.Facing;
                _level = level;
                _nav = NavFor(level);
                _nav.SetPosition(x, y);
                while (_nav.Facing != facing) _nav.TurnRight();
            }
        }
        else if (level > 0)
        {
            // Past the deepest level the original still writes X, Y and that DungeonLevel
            // (MAINMOD2.BAS:5786-5833) - fn0A0B loads nothing for it (MAZEMOD.BAS:415-417) and the
            // automap write is skipped (MAINMOD2.BAS:5873) - so the bodies lie on a level with no
            // map. The window cannot stand there, so SavePos puts them there instead.
            _bodiesOffTheMap = (level, x, y);
        }
        // everyone is there before the first death, so each guild log line names that level
        if (level > 0) SavePos();
        foreach (var m in _party.Members.ToList())
        {
            m.CauseOfDeath = Rules.DeathRock;
            m.Area = level <= 0 ? -1 : 0;   // the level it landed on: -1 at 0, else False (MAINMOD2.BAS:5881-5885)
            Kill(m, said);
        }
    }

    /// <summary>
    /// The endgame, <c>sub0C61</c> (MODULE4.BAS:4234). Every survivor gets a line in their guild's
    /// log, and the ending itself goes up in a box - which is where the original leaves it too.
    /// Nothing else changes: the game carries on.
    /// </summary>
    private async void BeatAsmodeus()
    {
        bool inAParty = _party.Size > 1;
        foreach (var c in _party.Members)
            GuildLog.Append(SavesDir, GuildData.IndexOf(c.Guild.Guild), Asmodeus.LogLine(c, inAParty));
        await Tell(Asmodeus.EndingTitle, Asmodeus.Ending(inAParty));
    }

    /// <summary>
    /// MODULE6.BAS:4574 - a death goes into the guild log, naming the level you died on, unless
    /// you did not know which level that was.
    /// </summary>
    private void LogDeath(Character who)
    {
        _countedDead.Add(who);
        GuildLog.Append(SavesDir, GuildData.IndexOf(who.Guild.Guild),
                        who.LostLevel ? GuildLog.Wrote.DiedSomewhere(who.Name)
                                      : GuildLog.Wrote.DiedOnLevel(who.Name, who.DungeonLevel));
    }

    /// <summary>Deaths already logged and counted this fight, so a Kill() mid-fight and EndCombat do not both count one.</summary>
    private readonly HashSet<Character> _countedDead = new();

    /// <summary>The engine EndCombat has already run for, so a second call (flee during a paced round) is a no-op.</summary>
    private CombatEngine? _combatEnded;




    /// <param name="sub0930">
    /// False for a death that is <c>mdrcharSetDead</c> alone, with no sub0930 after it - the chest
    /// trap's, MONSMOD.BAS:3654: the party list's UPDATE unloads that body as it lies, so its
    /// companions, its paralysis, poison, disease and spell resistances all stay with it.
    /// </param>
    /// <summary>
    /// <c>sub0930</c> on its own (MAINMOD2.BAS:3016-3125), for the one caller that reaches it
    /// without a death of its own in front: the `o` key's tail. It does not ask whether the
    /// character is dead. The companions are let go and the carried body put down (3026-3030),
    /// sub048C (3033), the save; then in a party the list's UPDATE (3035-3037) and nothing more.
    /// Alone, a character not yet dead is set dead, an ordinary death (3042), and the dead screen
    /// follows (3043-3057). Then a raise somebody else cast on the body is offered (3058-3066):
    /// see <see cref="Sub0930RaiseOffer"/>.
    /// </summary>
    private void Sub0930(Character who)
    {
        who.Sub0930Head(Lookup);   // 3026-3033: companions, carry, sub048C
        who.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(who.Name) + ".json"));
        if (_party.Size > 1) { PruneTheDead(); return; }
        if (!who.IsDead) SetDead(who);
        SaveChar();
        OfferRescue();
        if (who.IsDead && PendingRaise.IsPending(who)) _ = Sub0930RaiseOffer(who);
    }

    /// <summary>
    /// sub0930's pending raise (MAINMOD2.BAS:3058-3066), after the dead screen is painted:
    /// <c>If DeathState &gt; False And M58E9 &gt; 0 Then If fn16B5(pv) = True Then sub14CC pv: Exit Sub
    /// Else M58E9 = 0: M58F9 = " ": fn04ED</c>. Taken, the character is on their feet and sub14CC
    /// (3134-3155) puts the dungeon back - or, at level 0, closes it for the city. Refused, or
    /// failed without complications, the raise is forgotten and the file saved. The city's
    /// arrival does the same for a body loaded there (CityWindow.ArriveDead).
    /// </summary>
    private async System.Threading.Tasks.Task Sub0930RaiseOffer(Character who)
    {
        try
        {
            string file = System.IO.Path.Combine(SavesDir, Character.SafeFileName(who.Name) + ".json");
            // UBound(Chars) is 0 here - sub0930 left already for anybody in company - so the
            // offer is the wording for a character run alone
            if (!await OfferPendingRaise(who, alone: true))
            {
                if (_ended) return;
                PendingRaise.Forget(who);                    // M58E9 = 0, M58F9 = " " (3063-3064)
                who.Save(file);                              // fn04ED (3065)
                return;
            }
            if (_ended) return;
            Sub14CC();
        }
        catch (Exception ex) { CrashLog.Write("sub0930 raise offer", ex); }
    }

    /// <summary>
    /// sub14CC (MAINMOD2.BAS:3134-3155), the dead screen taken down after fn16B5 raised the
    /// character: saved, the stats repainted, and below ground picMonsters and lblRoom shown, the
    /// combat and ailment clocks running again, lblDead and cmdWait hidden, the room read and the
    /// maze drawn. lblSquareInfo is not touched, so the dead screen's line stays until the square
    /// routine next writes it. At level 0 the dungeon form is unloaded and the city shown.
    /// </summary>
    private void Sub14CC()
    {
        SaveChar();
        RefreshParty();
        if (_level <= 0)
        {
            try
            {
                foreach (var (lvl, nav) in _navs) _map.Merge(lvl, nav.Explored, nav.Floor.Width);
                _map.Save(SavesDir);
                _dstate.Save(SavesDir);
                _party.SaveAll(Saves.DirFor(_gameDir));
            }
            catch { /* non-fatal, as WaitForRescue */ }
            Close();
            return;
        }
        _combatClockOff = false;                                 // timCombat.Enabled = True (3141)
        _awaitingRescue = false;
        CombatTitle.Text = "";                                   // lblDead.Visible = False (3143)
        WaitBtn.IsVisible = false;                               // cmdWait.Visible = False (3144)
        CombatPanel.IsVisible = _chest != null || _waiting is { Count: > 0 };   // picMonsters.Visible = True (3138)
        Redraw();                                                // mdrmazLookAround, sub0835, mdrmazShowMaze (3145-3147)
        Focus();                                                 // frmDungeonMain.SetFocus (3148)
    }

    private void Kill(Character who, string why, bool sub0930 = true)
    {
        who.CurHits = 0; who.IsDead = true; who.Deaths++;
        LogDeath(who);
        // sub0930: the companions go, M5943 = -1 (MAINMOD2.BAS:3026-3030), and sub048C (3033) -
        // paralysis undone, poison, disease, states 0-6 and spell resistances ended, recomputed
        if (sub0930) who.Sub0930Head(Lookup);
        Audio.PlayOnNextChannel("DIE");
        // Every one of these lines is a sub085B in the original, which is the Messages
        // window - a list that keeps what it is told. The port had been putting them on the
        // message line alone, where the very next thing to happen wiped them: a death
        // outside combat is followed immediately by the rescue offer, so "You have died of
        // natural causes." was overwritten before anybody could read it and the body was
        // left on screen with no account of how it got there.
        // mdrcharSetDead itself writes nothing to the list, so a death with no line of its own
        // (a chest trap, MONSMOD.BAS:3652) leaves the list alone
        // - and the list alone: lblSquareInfo gets the dead screen's line (MAINMOD2.BAS:3052-3054)
        if (why != "") Log(why);

        // "If UBound(Chars) > False Then frmParty!lblCall = "UPDATE" : Exit Sub" (MAINMOD2.BAS:3035-3037):
        // in a party it is the list's UPDATE and nothing more, whether or not anybody is left able.
        // The UPDATE prunes everyone at nought while more than one is left (FRMGROUP.BAS:326), so
        // the last member stays, and when that one is at nought too no dead screen follows here -
        // the original's own gap, kept. The port had tested AnyAble and gone to the dead screen
        // with the bodies still on the list.
        if (_party.Size > 1)
        {
            // No SelectNextAble here. sub2F40 never picks a "next able" character: it adjusts Me
            // only for a removal below it - "If l0118 < Me Then Me = Me - 1" (FRMGROUP.BAS:340) -
            // clamps it if the list got shorter, and finishes with mdrmulSwitch Me, falling back
            // to the leader only when Me has run off the end. So somebody else dying leaves you
            // looking at the same character.
            //
            // This port called SelectNextAble whenever anyone died, so a party member dying two
            // slots away moved the selection off the character you were running.
            SaveChar();
            PruneTheDead();
            RefreshParty();
            Redraw();
            return;
        }

        // nobody left standing. The body stays exactly where it fell - MAINMOD2.BAS:3040 only
        // offers the morgue when DungeonLevel is 0, and "Wait for Rescue" otherwise - so the port
        // no longer spirits it home.
        SaveChar();
        OfferRescue();
    }

    /// <summary>
    /// What the dungeon shows a corpse (MAINMOD2.BAS:3036-3050). A character pulled into the rock
    /// gets no wait button at all: they have to be summoned out before anything can reach them.
    /// </summary>
    /// <summary>
    /// Drop the dead from the roster - the first half of <c>sub2F40</c> (FRMGROUP.BAS:321-348).
    /// Their body stays on the square it fell on, saved to file, so a rescuer can still find it.
    ///
    /// The original runs this from its party-window update, which fires on far more occasions than
    /// this port has hooks for; here it runs when a fight ends and when somebody dies outside one,
    /// which are the two moments a party's membership can actually change by death. A character
    /// adventuring alone is never pruned, which is what leaves the Wait for Rescue screen a body
    /// to talk about.
    /// </summary>
    /// <summary>
    /// The end of the square routine after a pit or a chute (MAZEMOD.BAS:1879-1889): a lone character
    /// at nought dies there, silently; and if anyone fell, the party list's UPDATE
    /// (modUpdateCharacterList, FRMGROUP.BAS:310-352) takes out everyone at nought, "\"X\" has died!"
    /// for those on the leader's floor.
    /// </summary>
    private void AfterAFall(bool runUpdate)
    {
        if (_party.Size == 1 && _ch.CurHits <= 0 && !_ch.IsDead) { Kill(_ch, ""); return; }
        if (runUpdate) PruneTheDead();
    }

    /// <summary>
    /// <c>mdrcharSetDead</c> for a death that is not the one on screen dying in front of you: hits to
    /// nought, an ordinary death, counted, logged at the floor they are on, DIE.WAV
    /// (MAINMOD2.BAS:4558).
    /// </summary>
    private void SetDead(Character m)
    {
        m.CurHits = 0; m.IsDead = true; m.Deaths++;
        m.CauseOfDeath = Rules.DeathOrdinary;   // DeathState 1, as both callers pass
        LogDeath(m);
        Audio.PlayOnNextChannel("DIE");   // the companions are sub0930's to release, not this routine's
    }

    /// <summary>
    /// The party list's UPDATE after the characters' turns (MULTIMOD.BAS:542-553), for bodies
    /// already marked dead: each death not yet counted is counted, logged and heard as
    /// mdrcharSetDead does (MAINMOD2.BAS:4558-4578), then the dead are unloaded
    /// (FRMGROUP.BAS:321-346), all but the last one left.
    /// </summary>
    private void CountThenPrune()
    {
        if (_party.Size <= 1) return;
        foreach (var fallen in (_combat?.NewlyDead ?? Enumerable.Empty<Character>()).Where(c => !_countedDead.Contains(c)).ToList())
        {
            fallen.Deaths++; LogDeath(fallen); Audio.PlayOnNextChannel("DIE");
        }
        PruneTheDead();
    }

    private void PruneTheDead()
    {
        if (_party.Size <= 1) return;
        // modUpdateCharacterList ends in mdrmulSwitch Me (FRMGROUP.BAS:383-389): "Dungeon (Name)",
        // and it stays so if the list is left with one
        _captionNamed = true;
        int before = _party.Size;
        var shown = _shownSlot < _party.Size ? _party.Members[_shownSlot] : null;
        var said = _party.RemoveTheDead(c =>
            c.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(c.Name) + ".json")), SetDead);
        // "If l0118 = gv1A82 Then l011A = True" (FRMGROUP.BAS:336-338) ... "If l011A = True Then sub1536 Me" (360-361):
        // the one the windows showed has gone, so sub1536 paints them for Me - its caption
        // overwritten by mdrmulSwitch after (383-389). Otherwise M5E4F keeps its slot while Me moves
        // down past the removed (339-341). gv1A82 is read as M5E4F here, which differ only after a drag
        if (shown != null && !_party.Members.Contains(shown)) _shownSlot = _party.SelectedIndex;
        if (_party.Size == before) return;   // somebody may go unannounced (off the leader's floor), and still go

        foreach (string line in said) Say(line);
        RefreshParty();
        RefreshSheetSpells();
        Redraw();
    }


    /// <summary>
    /// "frmDungeonMain!timCombat.Enabled = False" (MAINMOD2.BAS:3040): the dead screen stops the
    /// fight clock, and a round already under way stops at its next check of the timer
    /// (MONSMOD.BAS:294, 374, 506). Only sub14CC, the character coming back, starts it again
    /// (MAINMOD2.BAS:3142) - and in this port that is a new dungeon window.
    /// </summary>
    private bool _combatClockOff;

    /// <summary>
    /// The dead screen, the lone character's tail of <c>sub0930</c> (MAINMOD2.BAS:3039-3057): the
    /// fight clock off, picTrap hidden, lblDead captioned with the death state and shown, cmdWait
    /// captioned and shown (not in the rock), the line in lblSquareInfo, and picMonsters hidden.
    /// The port had shown picMonsters instead, as the place for the death state, and left the
    /// clock running - so the fight went on being fought and drawn over a corpse, the chest and its
    /// trap panel stayed up and the button sat on top of the monster picture.
    /// </summary>
    private void OfferRescue()
    {
        bool inRock = _ch.CauseOfDeath == Rules.DeathRock;
        _combatClockOff = true;                                  // timCombat.Enabled = False (3040)
        ChestPanel.IsVisible = false;                            // picTrap.Visible = False (3041)
        // lblDead, inside picMaze2 where the form puts it (0,615 1470x225): the death-state word
        // (3043-3044). The view behind it is empty - see RenderView
        CombatTitle.Text = DeathStates.Name(_ch.CauseOfDeath);
        // MAINMOD2.BAS:3045-3049 - "&Wait for Rescue" below ground, "&Visit the Morgue" at level 0,
        // and no button at all when you are in the rock: somebody has to summon you out first
        WaitBtn.Content = _level > 0 ? "_Wait for Rescue" : "_Visit the Morgue";
        WaitBtn.IsVisible = !inRock;
        MsgText.Text = inRock ? Rescue.DeadInRock : Rescue.DeadInTheDungeon;   // lblSquareInfo (3052, 3054)
        CombatPanel.IsVisible = false;                           // picMonsters.Visible = False (3056)
        _awaitingRescue = !inRock;
        RenderView();                                            // picMaze2.Cls (3024)
    }

    /// <summary>
    /// Lying there until somebody finds you, <c>cmdWait_Click</c> (FRMDUNM.BAS:140-190). However
    /// many days it takes are added to your age, and then you wake up in town.
    /// </summary>
    private async void WaitForRescue()
    {
        bool easy = Rescue.EasyToFind(_nav.Floor, _nav.X, _nav.Y);
        int days = Rescue.DaysUntilFound(_rng, _level, _ch.Area ?? _nav.Floor.AreaIdAt(_nav.X, _nav.Y), easy);   // Chars(Me).M59BB (FRMDUNM.BAS:170)
        // FRMDUNM.BAS:181-182: a Beep and a "Wait for Rescue" MsgBox, and nothing more is said -
        // mdrcharAddAge is called with p088C = False (189), so no "older" line. The port had
        // written the find and an "N week(s)... older." into the message line, with FOUND.WAV.
        Audio.Beep();
        await Tell("Wait for Rescue", Rescue.Found(days));
        // FRMDUNM.BAS:187-189 - carried to town, M59BB = -1, the days added to the age - and the
        // store and pens turned over for character 0; both shared with the city-hosted dead screen
        Rescue.CarryHome(_ch, days);
        // after the await this runs outside OnWait's try, so a failure is logged here rather than
        // taking the process down
        try
        {
            if (_party.Members.IndexOf(_ch) == 0)
                Rescue.TurnOverTown(SavesDir, _items, _monsters, _rng, _dstate.DeepestReached);
        }
        catch (Exception ex) { CrashLog.Write("rescue replenish", ex); }
        // deliberately not SaveChar(): that calls SavePos, which would put the body straight back
        // on the square it has just been carried off
        try
        {
            foreach (var (lvl, nav) in _navs) _map.Merge(lvl, nav.Explored, nav.Floor.Width);
            _map.Save(SavesDir);
            _dstate.Save(SavesDir);
            _party.SaveAll(Saves.DirFor(_gameDir));
        }
        catch { /* non-fatal */ }
        Close();
    }


    /// <summary>
    /// A chute, walked onto (<paramref name="taken"/> false, MAZEMOD.BAS:1839-1893) or taken with the
    /// TAKE key (true, MAINMOD2.BAS:3996-4009). See <see cref="Chute"/> for the rules; this carries them
    /// out member by member, leader first, then breaks the party up as sub0908 does.
    /// </summary>
    private async void ChuteFall(bool taken, int x, int y)
    {
        var fl = _nav.Floor;
        int from = _level;
        // no record, or a depth of 0, and nobody changes floor (sub08F5, 111-118)
        // the first record for the square with a depth above nought; the data never runs past the bottom
        // (selfcheck), so the clamp only guards a damaged file
        var rec = fl.Chutes.FirstOrDefault(c => c.X == x + 1 && c.Y == y + 1 && c.Depth > 0);
        // sub08F5 adds the depth and calls fn0A0B for each member who goes down (MAZEMOD.BAS:114-115):
        // in the limited build a floor past its last is denied, a line for each, and the last taken
        var (asked, denied) = LimitedVersion.Clamp(from + Math.Max(0, (int)rec.Depth));
        int to = Math.Clamp(asked, 1, _dungeon.Count);
        bool partyAtStart = _party.Size > 1;
        var members = _party.Members.ToList();
        var landed = new Dictionary<Character, int>();
        var lines = new List<string>();
        string hint = "";
        bool follow = false;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            bool floats = !taken && Chute.Floats(m, _rng, from);   // rolled even when following
            if (floats && !follow) { if (m == _ch) hint = Chute.TakeHint; continue; }
            if (!taken && i == 0 && members.Count > 1)
                follow = await Confirm(Chute.FollowQuestion, Chute.FollowTitle);
            if (denied != null) lines.Add(denied);
            landed[m] = to;
            if (Unmapped(to, x, y))
            {
                m.LostLevel = m.LostPosition = m.LostFacing = true;
                if (m == _ch) lines.Add(Lost.Realisation(m));   // sub08E1 speaks only for the one on screen
            }
            int leaderFloor = landed.TryGetValue(members[0], out int lf) ? lf : from;
            if (Chute.Land(m, m == _ch, to == leaderFloor, to, _rng) is string said) lines.Add(said);
        }
        if (landed.Count > 0 && !taken) { Audio.PlayOnNextChannel("FALL"); Audio.PlayAfterLast("HIT"); }

        // sub0908: anyone not on the leader's floor leaves the party where they are
        int leaderLevel = landed.TryGetValue(members[0], out int ll) ? ll : from;
        int sizeBefore = _party.Size;
        foreach (var m in members.Skip(1))
        {
            int level = landed.TryGetValue(m, out int ml) ? ml : from;
            if (level == leaderLevel) continue;
            _captionNamed = false;   // sub0908's closing sub1536 gv1A84 (MULTIMOD.BAS:93-95, 686-690)
            lines.Add(Chute.Disappeared(m));
            bool wasShown = m == _ch;
            // sub08F5 has moved them before sub0908 runs - level, square and the M59BB of that square on
            // the floor they landed on (MAZEMOD.BAS:114-116); one who floated was never moved
            if (landed.ContainsKey(m) && level != from) Chute.MoveDown(m, level, x, y, _dungeon[level - 1]);
            else { m.DungeonLevel = level; m.X = x; m.Y = y; }
            if (m.CurHits <= 0)
            {
                m.CurHits = 0;
                // mdrcharSetDead: counted, logged at the floor they are on, DIE.WAV (MAINMOD2.BAS:4558)
                if (!m.IsDead) SetDead(m);
            }
            _party.Remove(m);
            if (wasShown) _party.Select(0);   // sub0908 hands the screen to the leader (MULTIMOD.BAS:78-84)
            _shownSlot = _party.SelectedIndex;   // M5E4F: sub1536 gv1A84 closes sub0908 (MULTIMOD.BAS:95)
            m.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(m.Name) + ".json"));
        }
        // sub0908's UPDATE once anyone has gone (MULTIMOD.BAS:88-95): anyone at nought left with the
        // leader goes too (FRMGROUP.BAS:321-346) - its lines after the chute's and the disappearances
        if (_party.Size < sizeBefore)
        {
            foreach (string line in lines) Log(line);
            lines.Clear();
            PruneTheDead();
        }
        ApplyLost();
        if (leaderLevel != from) ChangeLevel(leaderLevel, x, y);
        else
        {
            SaveChar();
            // the leader stayed: mdrmazShowMaze still follows the square's effects (MAZEMOD.BAS:1892-1895),
            // so an area entered by stepping onto this chute is settled now, not on the next tick
            SettleArea();
        }

        // TAKE always, and a walk only when the leader went down with a party behind them, runs the
        // square routine again where they land (MAINMOD2.BAS:4003; MAZEMOD.BAS:1857, 1891-1893): the
        // lost rolls, the square's own effects - a chute there fires again - and its label. A lone
        // walker is not re-run, and the label is left as it was.
        bool rerun = taken || (landed.ContainsKey(members[0]) && partyAtStart);
        // the chute's own lines were said before the re-run's (1839-1870, then 1891-1893)
        foreach (string line in lines) Log(line);
        string square = rerun ? RerunSquare() : hint;
        // the label is lblSquareInfo alone: the landing square's caption after a re-run, or the TAKE
        // hint (MAZEMOD.BAS:1863). The chute's lines are sub085B (MAZEMOD.BAS:131, 134, and
        // sub08E1, sub0908) and went to the list above, not here.
        MsgText.Text = square.Trim();
        RefreshParty(); RefreshSheetSpells(); Redraw();

        // a walked chute sets l0280 whether or not anyone went down (MAZEMOD.BAS:1871); TAKE never runs the
        // UPDATE, only the lone check of the square it re-runs
        AfterAFall(runUpdate: !taken);
    }

    private string UseStairs()
    {
        var fl = _nav.Floor;
        if (fl.Has(_nav.X, _nav.Y, Floor.STAIRS_DOWN))
        {
            // the limited build's last level: down still goes to fn0A0B, which denies it (ChangeLevel)
            if (_level >= _dungeon.Count && LimitedVersion.Clamp(_level + 1).Said == null) return "";
            ChangeLevel(_level + 1, _nav.X, _nav.Y);
            // the new square's own label, "There are Stairs Up here." (MAZEMOD.BAS:1780), is all that
            // is said; "You descend to level N." and "You ascend to level N." were the port's
            return _nav.Describe();
        }
        // only the way up asks: "If Chars(Me).DeathState = False And Chars(Me).Ailments(mc01EC) =
        // False Then" wraps the stairs-up case alone in fn1521 (MAINMOD2.BAS:3953). The stairs
        // down and the chute after it are outside that test, so a paralysed character still goes
        // down and still takes a chute - kept
        if (fl.Has(_nav.X, _nav.Y, Floor.STAIRS_UP) && !_ch.Paralysed)
        {
            // level 1: "DungeonLevel = 0 : Unload frmDungeonMain : frmCityMain.Show : Exit Function"
            // (MAINMOD2.BAS:3955-3959). No mdrmazShowMaze, so no leave round for an engaged group
            // (MAZEMOD.BAS:815-818) and no fn0F88 write-back of the room (811) - mid-fight too; and
            // Form_Unload writes nothing either (FRMDUNM.BAS:360-367). The room keeps what was last
            // written to it (at generation, a chest, a win, an earlier leaving).
            if (_level == 1)
            {
                // a round still being drawn is finished first, while the party still stands on level 1, so its
                // save cannot put them back there after the exit sets the level to 0 (MAINMOD2.BAS:3955-3959)
                FinishWonFight();
                // a mod may act here first, while the dungeon can still cast (docs/MODDING.md,
                // ModActions.LeavingByStairs); with none hooked the climb follows at once
                BeforeStairsToTown(() =>
                {
                    // it is the leader's level that goes to 0, "Chars(m00A6).DungeonLevel = 0" with m00A6 = 0
                    // (MAINMOD2.BAS:17, 3956), whoever is selected; then frmCityMain's Form_Load lines
                    // everyone up on the leader with sub0956 (FRMCTYM.BAS:105, MULTIMOD.BAS:715-721). The
                    // port had set the selected character's alone, so the rest were saved on level 1
                    _party.PlaceAt(0, _nav.X, _nav.Y);
                    _noRoomWriteOnClose = true; Close();
                });
                return "";
            }
            ChangeLevel(_level - 1, _nav.X, _nav.Y);
            return _nav.Describe();
        }
        if (fl.Has(_nav.X, _nav.Y, Floor.CHUTE))
        {
            // the limited build: "If m0622 = True And DungeonLevel = mc0624" (MAINMOD2.BAS:3997-3998)
            if (LimitedVersion.ChuteIsDeadToTake(_level)) { Log(LimitedVersion.ChuteCannotBeTaken); return ""; }
            int tx = _nav.X, ty = _nav.Y;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ChuteFall(taken: true, tx, ty));
            return "";
        }
        return "";   // nothing to take: t does nothing (MAINMOD2.BAS:3946-4010)
    }

    private void ChangeLevel(int newLevel, int x, int y, bool settle = true)
    {
        // fn0A0B, which every change of level goes through: the limited build refuses a level past
        // its last, says so in the list, and stays on the last (MAZEMOD.BAS:418-422)
        var (allowed, denied) = LimitedVersion.Clamp(newLevel);
        if (denied != null) { Log(denied); newLevel = allowed; }
        newLevel = Math.Clamp(newLevel, 1, _dungeon.Count);
        var facing = _nav.Facing;
        _level = newLevel;
        _nav = NavFor(newLevel);
        _nav.SetPosition(x, y);
        // keep facing
        while (_nav.Facing != facing) _nav.TurnRight();
        // a new level is a new area: mdrmazShowMaze runs after every change of level
        // (MAINMOD2.BAS:3968-3969, 3988-3989). A caller that runs the square routine first leaves
        // it to that routine, and the area after it (ArriveBySpell), as sub0835 comes before
        // mdrmazShowMaze in mdrcharTeleport (MAINMOD2.BAS:5899-5900)
        if (settle) SettleArea();
        // MAZEMOD.BAS:1632-1637 - going deeper than anyone ever has is worth a line in the guild
        // log, but only if you know what level you are on
        if (!_ch.LostLevel && _dstate.NoteDepth(newLevel))
        {
            GuildLog.Append(SavesDir, GuildData.IndexOf(_ch.Guild.Guild), GuildLog.Wrote.FirstToLevel(_ch.Name, newLevel));
            _dstate.Save(SavesDir);
        }
        SaveChar();
    }

    /// <summary>
    /// The party moves as one. MAZEMOD.BAS copies the leader's square onto every member after a
    /// step, so a save picks them all up in the same place.
    ///
    /// Anyone being carried comes too: sub0A7C (MAINMOD2.BAS:5974) writes the carrier's X, Y and
    /// DungeonLevel straight onto the body and saves it, which is what makes walking out of the
    /// dungeon deliver them to the city.
    /// </summary>
    private void SavePos()
    {
        if (_bodiesOffTheMap is var (bl, bx, by)) _party.PlaceAt(bl, bx, by);
        else _party.PlaceAt(_level, _nav.X, _nav.Y);
        // and the facing, which the record keeps too: "Chars(l013A).Facing = Chars(m0022).Facing"
        // (sub0956, MULTIMOD.BAS:723). The port had not kept it, so every return faced North.
        foreach (var c in _party.Members) c.Facing = (int)_nav.Facing + 1;
        SyncAreas();
    }

    /// <summary>
    /// Set by a Teleport into a level past the deepest (see <see cref="TeleportedIntoRock"/>): the
    /// party is dead, and its bodies are saved there rather than where the window last stood.
    /// </summary>
    private (int Level, int X, int Y)? _bodiesOffTheMap;

    /// <summary>
    /// M59BB for the party: the area of the leader's square, -1 at level 0 (MAZEMOD.BAS:822-824),
    /// copied onto every member by sub0956, the dead included (MULTIMOD.BAS:715-722, from
    /// MAZEMOD.BAS:832). One body is not: a death in rock writes nought (MAINMOD2.BAS:4571-4573,
    /// 5883-5885) after sub0956 has lined the party up, and sub0930 takes that body out of the party
    /// straight after, so no later sub0956 reaches it. The port may still hold it for a moment.
    /// </summary>
    private void SyncAreas()
    {
        int area = _level <= 0 ? -1 : _nav.Floor.AreaIdAt(_nav.X, _nav.Y);
        foreach (var c in _party.Members)
            if (!(c.IsDead && c.CauseOfDeath == Rules.DeathRock)) c.Area = area;
    }

    /// <summary>
    /// A saved character's M59BB, or for a save from before the port kept it the area of its square.
    /// </summary>
    private int SavedArea(Character c) => c.AreaOr(k =>
        k.DungeonLevel >= 1 && k.DungeonLevel <= _dungeon.Count ? _dungeon[k.DungeonLevel - 1].AreaIdAt(k.X, k.Y) : -1);

    /// <summary>
    /// Put whoever is being carried where the carrier ended up: sub0A7C (MAINMOD2.BAS:5974-5995).
    /// mdrmazShowMaze does it for every carrier each time the area or the level changes
    /// (MAZEMOD.BAS:839-841, see <see cref="Arrived"/>) - the port had done it only as the window
    /// closes - and the dungeon form's load and the city do it too (FRMDUNM.BAS:284-285,
    /// FRMCTYM.BAS:122). Not on every step: a body stays where it was last put until the carrier
    /// leaves the area.
    /// </summary>
    private void DeliverCarried(Character? only = null)
    {
        foreach (var m in _party.Members.Where(m => m.CarryingCharacter != "" && (only == null || m == only)))
        {
            string path = System.IO.Path.Combine(SavesDir, Character.SafeFileName(m.CarryingCharacter) + ".json");
            if (!System.IO.File.Exists(path)) { m.CarryingCharacter = ""; continue; }
            try
            {
                var body = Character.Load(path);
                body.X = m.X; body.Y = m.Y; body.DungeonLevel = m.DungeonLevel;
                // "l0830.M59BB = Chars(Me).M59BB" (MAINMOD2.BAS:5992); walking out to town, the
                // city form has set the carrier's to -1 first (FRMCTYM.BAS:112, then 118)
                body.Area = m.DungeonLevel <= 0 ? -1 : m.Area;
                body.Save(path);
            }
            catch { }
        }
    }

    /// <summary>Autosave after anything that changes progress (combat, level changes) so a crash can't lose it.</summary>
    /// <summary>
    /// Everything that is written as you play. The character records are the part "No Autosave"
    /// switches off, and only that part.
    ///
    /// <c>mdrmazShowMaze</c> - the routine that draws the maze view - walks the party and writes
    /// each one when the area or the level has changed (MAZEMOD.BAS:797-800, 834-842; see
    /// <see cref="SettleArea"/>):
    ///
    /// <code>
    /// For gv1A84 = 0 To UBound(Chars)
    ///     If Not gv1A66.M5F80 Then
    ///         m01C2 = fn04ED(Chars(gv1A84))
    /// </code>
    ///
    /// That is the only guarded <c>fn04ED</c> in the game. <c>mdrcharUnload</c> (MAINMOD.BAS:419)
    /// writes the character when they leave play whatever the flag says, which is why turning
    /// autosave off costs you a trip if the game dies and costs you nothing if it does not. The
    /// map and the dungeon state are not character records and are not part of that trade.
    ///
    /// Position is not skipped either: <see cref="SavePos"/> only moves the in-memory character.
    /// </summary>
    private void SaveChar()
    {
        try
        {
            // each form writes its own placement back in Form_Unload; the port has one save
            SaveWindowLayout();
            SavePos();
            foreach (var (lvl, nav) in _navs) _map.Merge(lvl, nav.Explored, nav.Floor.Width);
            _map.Save(SavesDir);
            _dstate.Save(SavesDir);
            if (!_options.NoAutoSave) _party.SaveAll(Saves.DirFor(_gameDir));
        }
        catch { /* non-fatal */ }
    }

    // ------------------------------------------------------------------ combat
    /// <summary>
    /// One tick of the original's combat timer, <c>mdrmulInitiateCombatRound</c>.
    ///
    /// Two things happen here. If a fight is on, a round runs - every character acts on their
    /// standing order, then companions, then the monsters. If monsters are merely standing on your
    /// square, this is where they decide, once a second, whether to start on you at all.
    /// </summary>
    private void CombatTick()
    {
        if (_inRound) return;                          // the original's m000C guard
        if (_combatClockOff) return;                   // timCombat.Enabled = False on the dead screen (MAINMOD2.BAS:3040)
        // gv1ACC.M602F: the only thing that stops the clock is minimising the game
        // (FRMMAIN.BAS:213 sets it on WindowState = Minimized, 225 clears it on restore).
        // Opening a window does not pause anything - the help warns you to be quick.
        if (FrameState == WindowState.Minimized) return;
        _inRound = true;
        try
        {
            // the monsters are the area's (Foes() are loaded per area): leave the area and they
            // stay behind; a step within it keeps them with you
            bool sameSquare = _encounterAt == (_level, _nav.X, _nav.Y);
            bool here = sameSquare || (_encounterAt.Level == _level && _nav.InBounds(_encounterAt.X, _encounterAt.Y)
                                       && _nav.Floor.AreaIdAt(_encounterAt.X, _encounterAt.Y) == _nav.Floor.AreaIdAt(_nav.X, _nav.Y));

            if (_combat != null && !_combat.IsOver)
            {
                // the free rounds for walking are given at the step itself (LeaveArea, and the
                // arrival in AfterMove); a step inside the area gives none
                if (!here) { LeaveFight(); return; }
                if (!sameSquare) _encounterAt = (_level, _nav.X, _nav.Y);
                if (Environment.GetEnvironmentVariable("MORDOR_TRACE_ROUNDS") == "1")
                    Console.WriteLine($"ROUND {DateTime.UtcNow:HH:mm:ss.fff}");
                // PlayRound draws the blows a quarter-second apart, so it outlives this call.
                // It clears _inRound itself; the guard has to hold until the round really ends.
                _keepRoundGuard = true;
                // the mods' orders for this round (ModActions.CombatRound), given as keys pressed
                // between ticks would be; with no mod hooked nothing happens here
                BeforeCombatRound();
                PlayRound();
                return;
            }

            if (_waiting == null) return;
            if (!here) { _waiting = null; _encounterAt = (-1, -1, -1); HideMonsters(); return; }
            if (!sameSquare) _encounterAt = (_level, _nav.X, _nav.Y);

            // MULTIMOD.BAS:551-557. A group that already means you harm attacks on every tick and
            // rolls for nothing. Only a friendly one rolls to turn hostile - and not even that
            // while it has an offer to join standing.
            var lead = _waiting[0].Def;
            if (_waiting[0].Hostile)
            {
                // they were already coming for you
            }
            else
            {
                if (_waiting[0].JoinOffer > 0) return;
                if (!Rules.MonstersEngage(_rng, (Alignment)Math.Clamp((int)lead.AlignmentValue, 0, 2), _ch.Alignment,
                                          _ch.EffectiveStat((int)Stat.Cha, Lookup)))
                    return;
                foreach (var m in _waiting) m.Hostile = true;
            }


            var group = _waiting;
            bool lair = _waitingIsLair;
            _waiting = null;
            // MODULE7.BAS:577-582 - the tick on which they engage is also a tick on which they act
            StartCombat(group, lair, monstersMoveNow: true);
        }
        finally { if (!_keepRoundGuard) _inRound = false; }
    }

    /// <summary>
    /// One tick of timAilment, the dungeon form's second timer, every four seconds
    /// (FRMDUNM.BAS:395; the interval is a design-time property of the form, recovered from
    /// FRMDUNM.FRM - see docs/DECOMPILE.md). Sickness and old age are on this clock, not on your
    /// steps, so standing still in a poisoned state is no safer than walking.
    /// </summary>
    private void AilmentTick()
    {
        if (FrameState == WindowState.Minimized) return;   // the same gv1ACC.M602F gate
        if (_inAilment) return;                             // the original's m0026 guard
        _inAilment = true;
        try
        {
            // FRMDUNM.BAS:445-449, before anyone's turn
            AmbientSound();

            // FRMDUNM.BAS:451-584, member by member, in the original's order, by index. Whether a member
            // is alive is asked once, at the top. A death takes the member out of the party at once
            // (sub0930's lblCall "UPDATE", MAINMOD2.BAS:3035, handled at FRMGROUP.BAS:141-146), so after a drowning the rest of the
            // turn falls on whoever has moved into that place - or, when the drowned one was last, on
            // the one before them a second time (the clamp at 532). Alone, the body stays and takes it
            string said = "";
            for (int i = 0; i < _party.Size; i++)
            {
                var m = _party.Members[i];
                if (m.IsDead) continue;

                IdentifyMonsters(m);                                    // 455-497
                string square = WaterAndQuicksand(m);                   // 500-531
                if (square != "") said = square;
                if (i > _party.Size - 1) i = _party.Size - 1;
                m = _party.Members[i];

                // poison, then disease (533-551): a cure on the clock is a sound and nothing more
                if (m.Poisoned || m.Diseased)
                {
                    int con = m.EffectiveStat((int)Stat.Con, Lookup);
                    var (curedP, curedD, lost) = Rules.AilmentStep(_rng, con,
                        m.Poisoned ? Math.Max(1, m.PoisonLevel) : 0,
                        m.Diseased ? Math.Max(1, m.DiseaseLevel) : 0);
                    if (curedP) { m.Poisoned = false; m.PoisonLevel = 0; Audio.Play("BETTER"); }
                    if (curedD) { m.Diseased = false; m.DiseaseLevel = 0; Audio.Play("BETTER"); }
                    m.CurHits -= lost;
                }

                // paralysis wears off on this clock, not on the combat round (554-565), fight or
                // no fight: "If (Int(Rnd * 100) + 1) > l0196 Or fn0EEF() = False Then" - with
                // nothing on the square, waiting or engaged, it always goes. BETTER, then sub0D1D
                // mode 1 (FRMDUNM.BAS:562-563), and not a word
                if (m.ParalysisLevel > 0
                    && Rules.ParalysisPasses(_rng, m.ParalysisLevel, m.Guild.Level,
                                             m.EffectiveStat((int)Stat.Con, Lookup),
                                             Effects.ResistancePercent(m, 8, Lookup), MonstersHere))
                {
                    Audio.Play("BETTER");
                    m.Unparalyse(Lookup);
                }

                CompanionsDrift(m);                                     // sub0C00, 563

                // old age (564-574): mdrcharSetDead puts the hits to 0, so the check below is what
                // takes the body out. "You" is the one on screen, as everywhere sub085B names someone
                if (Rules.DiesOfOldAge(_rng, m.Age, RaceData.ByName(m.Race).MaxAge))
                {
                    string line = m == _ch ? "You have died of natural causes."
                                           : $"\"{m.Name.TrimEnd()}\" has died of natural causes.";
                    if (m.IsDead) Log(line); else Kill(m, line);
                }

                // the death check (576-583): mdrcharSetDead + sub0930, no line
                // and the index steps back so the one who moves up is not skipped
                if (m.CurHits < 1)
                {
                    if (!m.IsDead) Kill(m, "");
                    i--;
                }
            }

            // the leader's square (586-591): fog is heard a quarter of the time, water and quicksand always
            if (_nav.Floor.Has(_nav.X, _nav.Y, Floor.FOG) && _rng.NextDouble() * 100 < 25) Audio.PlayOnNextChannel("FOG");
            if (_nav.Floor.Has(_nav.X, _nav.Y, Floor.WATER) || _nav.Floor.Has(_nav.X, _nav.Y, Floor.QUICKSAND))
                Audio.PlayOnNextChannel(_rng.Next(2) == 0 ? "WATER" : "WATER1");
            CallForHelp();

            if (said != "") MsgText.Text = said;
            RefreshParty();
            Redraw(sub0956: false);
        }
        finally { _inAilment = false; }
    }

    /// <summary>
    /// Noticing what sort of square you have walked onto, <c>sub087C</c> (MAZEMOD.BAS:605). The
    /// party's best perception decides whether anything registers at all, and then a second roll
    /// against half of it decides whether you work out what it really is - get that right and it
    /// goes on your automap; get it wrong and you are told, with just as much confidence, that it
    /// is something else entirely.
    ///
    /// A square you have already mapped says nothing: you knew that already.
    /// </summary>
    /// <summary>
    /// <c>mdrmazPerceiveAhead</c>, which mdrmazShowMaze runs at the end of every redraw - so
    /// after every step and every turn, which is where this is called from. See
    /// <see cref="PerceiveAhead"/> for what it does and what it will not do.
    ///
    /// The same No Perception Messages option turns it off: both routines open with
    /// <c>If Chars(Me).M5914(4) Then Exit Sub</c> (MAZEMOD.BAS:463 and 459).
    /// </summary>
    private List<string> LookAheadLines()
    {
        if (_ch.Options.NoPerceptionMessages) return new();
        if (_ch.IsLost) return new();
        var (best, who) = PerceiveAhead.Best(_party, _level, Lookup);
        var said = PerceiveAhead.Look(_map, _nav.Floor, _nav.X, _nav.Y, _nav.Facing,
                                      _rng, best, who.Name, _party.Size > 1);
        if (said.Count > 0) PushToAutomap();      // the square it wrote is redrawn, as lblCall = "x,y" does
        return said;
    }

    /// <summary>
    /// A turn: the facing changes and mdrmazShowMaze runs (MAINMOD2.BAS:3902-3942). In the same area
    /// that is the view and mdrmazPerceiveAhead (MAZEMOD.BAS:1488), whose finds are sub085B lines;
    /// lblSquareInfo is only written by sub0835 and the square routine on arrival (MAZEMOD.BAS:1545,
    /// 1780-1783), so the square's label is left as it was. The port had written _nav.Describe()
    /// to the Messages window on every turn.
    /// </summary>
    private void AfterTurn()
    {
        // a turn ends in mdrmazShowMaze (MAINMOD2.BAS:3908, 3922, 3936), which runs the forced
        // block for a gv1D6A still set and clears it; nothing the port does leaves it set, so this
        // is a guard and normally does nothing
        if (_forceSettle) SettleArea();
        foreach (string line in LookAheadLines()) Say(line);
    }

    /// <summary>
    /// mdrmazPerceiveSquare for one bit of the square the leader stands on, called where sub0835
    /// calls it - each bit just before its own effect (MAZEMOD.BAS:1708-1741, 1790) - so the lost
    /// test that decides the automap write (638) sees the state at that moment, before a rotator's
    /// roll. Returns the line, or "".
    /// </summary>
    private string PerceiveBit(int bit)
    {
        // The best perception in the party and whose it is. The loop runs over every
        // member - "For l0194 = m00C0 To UBound(Chars)" (MAZEMOD.BAS:622) - and asks
        // nothing about whether they are alive, so a dead character's eyes still count.
        // The port had been skipping them.
        double best = 0;
        Character who = _ch;
        foreach (var m in _party.Members)
        {
            double p = Rules.BestAbilityChance(m, 5, _level, 0, Lookup);   // WIS + StatMods / 4 (MAINMOD2.BAS:1962)
            if (p > best) { best = p; who = m; }
        }

        var o = Perception.PerceiveSquare(_rng, bit, best, who.Name, _party.Size > 1,
                                          Leader.Options.NoPerceptionMessages,
                                          _map.Knows(_level, _nav.X, _nav.Y, _nav.Floor.Width, bit),
                                          Leader.IsLost);
        // MAZEMOD.BAS:638-651 - working out what a square is writes the bit into the automap file
        // itself, which is both how the game knows not to tell you twice and how the icon gets
        // onto the map. None of these is written down by walking onto the square.
        if (o.MapIt)
        {
            _map.Note(_level, _nav.X, _nav.Y, _nav.Floor.Width, bit);
            _map.Record(_level, _nav.X, _nav.Y, _nav.Floor.Width, bit);
            PushToAutomap();
        }
        return o.Line ?? "";
    }


    /// <summary>
    /// Standing in water or quicksand, on the ailment clock (FRMDUNM.BAS:500-528). Both cost you
    /// hits equal to the dungeon level every tick; quicksand also eats your pack. Levitating keeps
    /// you clear of both.
    /// </summary>
    private string WaterAndQuicksand(Character m)
    {
        var fl = _nav.Floor;
        bool water = fl.Has(_nav.X, _nav.Y, Floor.WATER);
        bool quicksand = fl.Has(_nav.X, _nav.Y, Floor.QUICKSAND);
        if (!water && !quicksand) return "";
        if (m.Levitating) return "";                  // Ailments(3) in the original

        // FRMDUNM.BAS:500-531, for one member: the square's label says "You are Drowning!" or "You
        // are Sinking!" (sub1642, whoever it is happening to), the water's sound is for the one on
        // screen only, a quicksand loss is a Messages line, and a death says nothing of its own
        string square = "";
        if (water)
        {
            if (m == _ch) Audio.PlayOnNextChannel("DROWN");
            square = SquareInfo.Drowning;
        }
        if (quicksand)
        {
            square = SquareInfo.Sinking;
            if (Rules.QuicksandTakesSomething(_rng, _level))
            {
                // a slot, not one of the items you have - the same roll the Slime and a thieving
                // monster make
                int slot = _rng.Next(ChestTraps.PackSlots - 1) + 1;
                if (m.Pack[slot] is InventorySlot held && held.ItemId > 0
                    && Lookup(held.ItemId) is Item it && ChestTraps.Destructible(it))
                {
                    Log(m == _ch ? $"QUICKSAND: {it.Name} Destroyed!"
                                 : $"\"{m.Name.TrimEnd()}\": QUICKSAND: {it.Name} Destroyed!");
                    Audio.Play("SLIMETR");
                    m.RemoveFromPack(held);
                }
            }
        }

        m.CurHits -= _level;
        // mdrcharSetDead + sub0930 (528-530), no line; the port's "drowns." was its own
        if (m.CurHits < 1) Kill(m, "");
        return square;
    }

    /// <summary>
    /// Working out what you are looking at, over time (FRMDUNM.BAS:455-497). Every tick, each
    /// character has a go at the groups in front of them; darkness makes it harder, and an
    /// invisible monster cannot be read at all without See Invisible.
    /// </summary>
    private void IdentifyMonsters(Character m)
    {
        var groups = _combat?.Monsters ?? _waiting;
        if (groups == null || groups.Count == 0) return;

        bool dark = _nav.Floor.Has(_nav.X, _nav.Y, Floor.FOG);
        {
            double skill = Rules.IdentifySkill(m.TopGuildLevel,
                m.EffectiveStat((int)Stat.Int, Lookup), m.EffectiveStat((int)Stat.Wis, Lookup));

            // the original holds one identification level per group slot, not per creature kind,
            // so two separate groups of the same monster are read separately (MDATA5 Foes(0..3))
            var slots = new List<List<CombatMonster>>();
            foreach (var g in groups)
            {
                if (slots.Count > 0 && CombatMonster.SameGroup(slots[^1][0], g)) slots[^1].Add(g);
                else slots.Add(new List<CombatMonster> { g });
            }
            for (int slot = 0; slot < slots.Count; slot++)
            {
                var mon = slots[slot][0];
                // an invisible monster stays unreadable unless you can see invisible
                // the monster record's M55BE is the port's SpecialPropertyFlags; bit 1 is Invisible
                bool unseen = MonsterProperty.Has(mon.Def, MonsterProperty.Invisible) && !m.SeeingInvisible;
                int groupNo = slot + 1;
                int alive = slots[slot].Count(g => g.Alive);
                // FRMDUNM.BAS:466-470: "You" whenever the identifier is the character you are
                // playing, even in a party
                string who = m == _ch ? "You" : $"\"{m.Name}\"";

                void Announce(CombatMonster mm)
                {
                    string line = $"{who} properly identified the monster(s) in group {groupNo} as "
                                + EncounterGenerator.NameFor(mm.Seen, m, Math.Max(1, alive), 0) + ".";
                    // a sub085B, fight or no fight (FRMDUNM.BAS:471-494); out of one it had gone to
                    // lblSquareInfo alone
                    Log(line);
                    if (_combat != null) RefreshCombat();
                    else if (_waiting != null) ShowWaiting(_waiting);
                }

                // the first pass: lifting the fog, so the group is named at its own level rather
                // than by its class (FRMDUNM.BAS:456-464). The lit square gets x1.25, band 5..80
                if (!mon.Identified && !unseen && _rng.NextDouble() * 100 < Rules.RecogniseChance(skill, dark))
                {
                    foreach (var g in slots[slot]) g.Identified = true;
                    Announce(mon);
                }

                // the second, quite separate pass: realising you had the wrong creature entirely
                // (FRMDUNM.BAS:475-489). Its own roll, its own band of 10..90, and in fog it is
                // your skill cut to three quarters rather than raised
                if (!unseen && mon.Believed != null && mon.Believed != mon.Def
                    && _rng.Next(100) + 1 < Rules.RenameChance(skill, dark))
                {
                    foreach (var g in slots[slot]) g.Believed = g.Def;
                    Announce(mon);
                    // sub06FE Foes(l0140), False, False, True, l0144 (FRMDUNM.BAS:497): filed as
                    // seen, by the one who recognised it
                    FileSightings(new[] { FoeOf(mon, alive, sighting: true) }, killed: false, m);
                }
            }

        }
    }

    /// <summary>
    /// The dungeon is not silent. FRMDUNM.BAS:449-453 rolls a flat 10% each tick, but only when
    /// nothing is in front of you, and plays one of three: something scurrying, a moan, or a door
    /// slamming a long way off.
    /// </summary>
    private void AmbientSound()
    {
        if (_party.Members[0].IsDead) return;                  // Chars(m00A4), the leader
        if (_combat != null || _waiting != null) return;
        if (_rng.NextDouble() * 100 >= 10) return;
        Audio.PlayOnNextChannel(new[] { "SCURRY", "MOAN", "DSTSLAM" }[_rng.Next(3)]);
    }

    /// <summary>
    /// mdrcharSetDead's DIE (sub086D 1, MAINMOD2.BAS:4558) for a death in a timed round that has
    /// not been heard yet: a lone character's, which sends no party-list UPDATE to play it
    /// (MULTIMOD.BAS:362-364). A death already counted - a party member's through PartyUpdate or
    /// CountThenPrune, a chest trap's through SetDead or Kill - made its own DIE.
    ///
    /// There was a STONE here as well, whenever anybody in the party was a statue. Stoning's
    /// STONE is the blow's (sub09DE mc04B6, MONSMOD.BAS:3213-3215), on the line that turns them,
    /// and the original has none after the round: that one was a second, invented, and it came
    /// back every round a statue stood in the party.
    /// </summary>
    private void AfterRoundSounds()
    {
        if (_combat == null) return;
        if (_party.Members.Any(m => m.CurHits <= 0 && !_countedDead.Contains(m))) Audio.PlayOnNextChannel("DIE");
    }

    /// <summary>
    /// You walked out of the fight. The monsters stay on their square; the original simply stops
    /// running rounds, because the round is keyed to the square you are standing on.
    /// </summary>
    private void LeaveFight()
    {
        if (_combat == null) return;
        _combat = null;
        _party.ClearOrders();
        HideMonsters();
        _encounterAt = (-1, -1, -1);
        // a group already waiting in the area you have walked into comes up now
        if (_waiting != null && _waiting.Count > 0)
        {
            _encounterAt = (_level, _nav.X, _nav.Y);
            ShowWaiting(_waiting);
            ShowWaitingChest(_waiting);
        }
        // walking off is silent - "You leave them behind." was the port's
        Redraw();
    }

    /// <summary>
    /// Monsters are on your square but have not moved. The original shows them in the dungeon
    /// window just the same - picMonsters and lblMonster are always there - so the panel comes up
    /// and simply says they are waiting.
    /// </summary>
    /// <summary>
    /// "1 Goblin offers to 'j'oin." - MONSMOD.BAS:4336-4341, which picks between "offers" and
    /// "offer" on the count and names them through the same function everything else uses.
    /// </summary>
    private string JoinLine(List<CombatMonster> group)
    {
        if (group.Count == 0 || group[0].JoinOffer <= 0) return "";
        int n = group[0].JoinOffer;
        return $"  {n} {EncounterGenerator.NameFor(group[0].Seen, _ch, n, group[0].Identified ? 0 : 1)} "
             + (n == 1 ? "offers" : "offer") + " to 'j'oin.";
    }


    private void ShowWaiting(List<CombatMonster> group)
    {
        // No sentence: the original draws the skull or the peace sign, the portraits and the numbered
        // groups (the help's dmainw1.png) and writes nothing about the encounter anywhere -
        // MONSMOD has no such line. "X - not moving yet" and "You see X. They have not come for you
        // yet." were this port's.
        CombatTitle.Text = "";
        ShowPortraits(group);
        ShowState(group);
        // lblMonster_Click (FRMDUNM.BAS:369-375) tests only "If Foes(Index).GroupCount > 0" - it
        // does not ask whether the fight has started, and neither does the 1-4 key. These rows
        // were plain TextBlocks while the in-combat ones were labels you could click, so a group
        // standing in front of you could be picked with the number keys and not with the mouse.
        // one row per Foes() slot: two slots of one monster are two rows (MONSMOD.BAS:1750-1752)
        // monShowCursor asks only whether monsters are present (fn0EEF() > 0) and whether the
        // character is in an area, not whether they are engaged (MONSMOD.BAS:4202-4226): the
        // order's mark stands beside the aimed group while they wait too - with no order yet,
        // the bullet. A target that is gone goes back to the first group standing (4203-4208).
        var runs = CombatMonster.Runs(group).ToList();
        var order = _party.OrderFor(_ch);
        int aimed = order.Target >= 0 && order.Target < group.Count
            ? runs.FindIndex(g => g.Contains(group[order.Target]))
            : -1;
        if (aimed < 0 || !runs[aimed].Any(m => m.Alive))
        {
            aimed = runs.FindIndex(g => g.Any(m => m.Alive));
            if (aimed >= 0) order.Target = group.IndexOf(runs[aimed].First(m => m.Alive));
        }
        // the rows are monShowMonsters' own, engaged or not - "n. count name" for a monster of groups
        // of two to twelve, else "n. name", by what the group is known as (MONSMOD.BAS:4400-4404)
        MonsterList.ItemsSource = runs
            .Select((g, n) => (g, n))
            .Where(t => t.g.Any(m => m.Alive))
            .Select(t => MonsterLabel(t.n + 1,
                                      MonsterRow(t.n + 1, t.g[0].Seen, t.g.Count(m => m.Alive), t.g[0].Identified),
                                      t.n == aimed ? OrderGlyph(order) : ""))
            .ToList();
        // the one line there is: an offer to join, on lblMonsterHit (MONSMOD.BAS:4343-4346).
        // "They watch you, and do not come." and "They have not come for you. Attack, or walk
        // away." were this port's.
        string offer = JoinLine(group).Trim();
        // told to the mods when it changes: this is repainted with the group, the offer standing
        if (offer != CombatMonsterHit.Text) Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.JoinOffer, offer, null);
        CombatMonsterHit.Text = offer;
        WaitBtn.IsVisible = false;
        CombatPanel.IsVisible = !_combatClockOff;   // picMonsters stays hidden on the dead screen (MAINMOD2.BAS:3056)
    }

    /// <summary>Put the combat panel away. The inverse of what ShowWaiting and StartFight set up.</summary>
    /// <summary>
    /// sub0C53's blanking of picMonsters when a fight is won (MONSMOD.BAS:762-768, 780-781): the
    /// groups' captions and lblCharacterHit emptied, then monShowMonsters -1 paints the portraits
    /// out (MONSMOD.BAS:4363-4364) and sub0B9B, with nobody standing, the state picture and lblMonsterHit (MONSMOD.BAS:4468-4470). The
    /// chest, if one was left, stays (783-787). With no chest picMonsters is empty, which in this
    /// port is the panel hidden, as in LoadArea and after a chest is opened.
    /// </summary>
    private void BlankPicMonsters()
    {
        MonsterList.ItemsSource = null;
        MonsterStrip.Children.Clear();
        StateIcon.Source = null;
        CombatTitle.Text = "";
        CombatHit.Text = "";
        CombatMonsterHit.Text = "";
        if (_chest == null) CombatPanel.IsVisible = false;
    }

    private void HideMonsters()
    {
        _chest = null;
        ChestPanel.IsVisible = false;
        CombatPanel.IsVisible = false;
        MonsterList.ItemsSource = null;
        MonsterStrip.Children.Clear();
        StateIcon.Source = null;
        ChestIcon.Source = null;
        CombatHit.Text = "";
        CombatMonsterHit.Text = "";
    }

    /// <summary>
    /// You go for them first. MAINMOD2.BAS:4025 - taking a Fight action while the monsters are
    /// still unengaged (<c>Foes(0).M4ECC = 0</c>) sets it to 1 and the fight is on. This is how
    /// most fights actually start; the per-second roll is the monsters getting impatient.
    /// </summary>
    private bool EngageWaiting()
    {
        if (_waiting == null) return false;
        var group = _waiting;
        bool lair = _waitingIsLair;
        _waiting = null;
        // you struck first: they answer on the next tick, as MAINMOD2 leaves it to sub15B0
        StartCombat(group, lair, monstersMoveNow: false);
        return true;
    }

    /// <summary>
    /// ALT-o and the Options button: <c>frmPartyOptions</c>, shared with the city's Party
    /// Information window in <see cref="PartyOptionsDialog"/>.
    /// </summary>
    private void ShowPartyOptions() =>
        PartyOptionsDialog.Show(DialogOwner, _party, () => { SaveChar(); RefreshParty(); RefreshCombat(); Focus(); });

    /// <summary>
    /// Pick up a body, or put down the one you have. You can carry one thing: a fallen character
    /// lying on this square, or one of your own dead companions. Whatever you carry travels with
    /// you, so walking out of the dungeon delivers them to the city and the morgue.
    /// </summary>
    /// <summary>`l`, Drop Body - its own command in the original, not the other half of `p`.</summary>
    /// <summary>
    /// <c>l</c> Drop Body, fn1521 case 76 (MAINMOD2.BAS:3684-3704). Carrying nothing is not an
    /// error and says nothing - the whole body of the case is inside "If M5943 &lt;&gt; -1".
    /// </summary>
    private void DropBody()
    {
        // fn1521 case 76 (MAINMOD2.BAS:3690): "If Chars(Me).Ailments(mc01EC) = 0", silently
        if (_ch.Paralysed) return;
        if (PutDownWhatIsCarried() is not string said) return;
        SaveChar();
        Log(said);   // a sub085B (MAINMOD2.BAS:3694, 3698); it went to lblSquareInfo, and only there
        Redraw(sub0956: false);
    }

    /// <summary>
    /// Puts down whoever is being carried and returns the line for it, or null when there was
    /// nobody. Shared because 'p' does this before it picks the next one up rather than refusing.
    /// </summary>
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
            // "Companions(M5943).Ref = -1" first (MAINMOD2.BAS:3695, and 3670 before a pickup): a
            // companion put down is let go, not left lying in its slot. The name is still in the
            // record when the line is written, so it is read before the slot is emptied here
            string said = Interact.NoLongerCarryingCompanion(_ch, _ch.CompanionName(_ch.CarryingCompanion));
            _ch.Companions.Release(_ch.CarryingCompanion);
            _ch.CarryingCompanion = -1;
            RefreshCompanions();
            return said;
        }
        _ch.CarryingCompanion = -1;
        return null;
    }

    /// <summary>`p`, Pickup Body (MAINMOD2.BAS:3654-3683).</summary>
    private async void PickUpBody()
    {
        // fn1521 (MAINMOD2.BAS:3655): "If DeathState = False And Ailments(mc01EC) = 0", before the
        // name is asked for - dead or held fast, p does nothing and says nothing
        if (_ch.IsDead || _ch.Paralysed) return;

        // frmInteract's PICKUP list (FRMINTER.BAS:339-387): the dead on this floor and in this area,
        // not yourself and not anyone a party member already carries. Hidden characters are listed;
        // the Hide test comes after the pick
        int area = _nav.Floor.AreaIdAt(_nav.X, _nav.Y);
        var here = Character.LoadAllFrom(SavesDir)
            .Where(c => c.IsDead && c.Name != _ch.Name
                        && _party.Members.All(m => m.CarryingCharacter != c.Name)
                        && c.DungeonLevel == _level && c.DungeonLevel >= 1 && c.DungeonLevel <= _dungeon.Count
                        && SavedArea(c) == area)
            .ToList();
        int pick = await NamePicker.Ask(DialogOwner, "Pickup Character", "Character's Name?",
                                        here.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("PICKUP"));
        if (pick < 0) return;
        var who = here[pick];

        // the Hide test, before anything already carried is put down
        if (who.Options.Hide) { Say(Interact.Hidden(who.Name)); return; }

        // "If Chars(Me).M5943 <> -1 Then ... Chars(Me).M5943 = -1" before the new one is taken
        // (MAINMOD2.BAS:3670-3683): picking somebody up while carrying somebody else puts the
        // first one down, and says so. It does not refuse.
        string? dropped = PutDownWhatIsCarried();
        if (dropped != null) Say(dropped);

        _ch.CarryingCharacter = who.Name;
        _ch.CarryingCompanion = -1;
        SaveChar();
        Say(Interact.NowCarrying(_ch, who.Name));
        Redraw(sub0956: false);
    }

    /// <summary>The ten buffers and what each currently holds.</summary>
    private void RefreshBuffers()
    {
        RefreshBufferButtons();
    }

    /// <summary>
    /// A plain number key picks which monster group this character goes for, if that group still
    /// has anything in it (MAINMOD2.BAS:4131-4136): key n is <c>Foes(n - 1)</c>, a slot. The rows
    /// are the same runs, the emptied ones included, so the keys and the rows keep one numbering
    /// even when two slots hold the same monster.
    /// </summary>
    private void TargetGroup(int group)
    {
        var all = _combat?.Monsters ?? _waiting;
        if (all == null || all.Count == 0) return;
        var runs = CombatMonster.Runs(all);
        if (group < 0 || group >= runs.Count) return;
        // "If Foes(n).GroupCount > 0"
        var first = runs[group].FirstOrDefault(m => m.Alive);
        if (first == null) return;
        int index = all.IndexOf(first);
        if (index < 0) return;
        _party.OrderFor(_ch).Target = index;
        // and nothing is said: "CombatOrders(Me).M547A = n : monShowCursor" and no more. "X takes
        // aim at Y." was this port's.
        ShowCursor();
    }

    /// <summary>
    /// <c>monShowCursor</c> (MONSMOD.BAS:4198-4229): the order's mark beside the aimed group,
    /// engaged or waiting alike - it tests fn0EEF() &gt; 0, not Engaged.
    /// </summary>
    private void ShowCursor()
    {
        if (_combat != null) RefreshCombat();
        else if (_waiting is { Count: > 0 } w) ShowWaiting(w);
    }

    /// <summary>
    /// Shift and a number fires one of the ten buffers. An item buffer uses that pack slot; a
    /// spell buffer becomes your standing order while a fight is on, and is simply cast otherwise
    /// (MAINMOD2.BAS:4141-4158).
    /// </summary>
    private async void FireBuffer(int slot)
    {
        if (slot < 0 || slot >= SpellBuffers.Count) return;
        int b = _ch.Buffers[slot];
        if (SpellBuffers.IsEmpty(b)) return;   // "If Chars(Me).M59B0(n) > -1" - an empty buffer does nothing and says nothing

        if (SpellBuffers.IsItem(b))
        {
            int packSlot = SpellBuffers.PackSlotOf(b);
            // sub15CD: "If Pack(p0852).Ref < 1 And p0852 > 0 Then Exit Sub" (MAINMOD2.BAS:6096), silently,
            // buffer kept. This used to test the compacted inventory's length, not the fixed slot.
            if (packSlot > 0 && (packSlot >= Character.PackSize || _ch.Pack[packSlot] is not { ItemId: > 0 })) return;
            PackList.SelectedIndex = packSlot;
            UseSelectedItem();
            return;
        }

        int spellIndex = SpellBuffers.SpellIndexOf(b);
        // past the end of the table the original errors out of fn1521 and keeps the buffer; the port
        // had cleared it
        if (spellIndex >= _spells.Count) return;
        var spell = _spells[spellIndex];

        // with monsters in front of you it becomes the standing order, and otherwise it is
        // mdrcharCastSpell(Me, spell, 0, -1) (MAINMOD2.BAS:4151-4159) - the sheet's Cast, except that
        // the buffer sets M5766 = -1 with the order (4155)
        await CastKnownSpell(spell, fromBuffer: true);
    }

    /// <summary>
    /// The ten buffer buttons along the bottom of the original's Dungeon window, captioned
    /// <c>l00BA + 1</c> - 1 to 10, as dmainw1.png shows (FRMDUNM.BAS:248-252) - though the tenth
    /// fires on the 0 key.
    /// </summary>
    private void BuildBufferButtons()
    {
        BufferButtons.Children.Clear();
        for (int i = 0; i < SpellBuffers.Count; i++)
        {
            int which = i;
            // cmdBuffer(n), 225x285 twips each, one beside the other (FRMDUNM.BAS:245-252)
            var b = new Button
            {
                Content = (i + 1).ToString(),
                Focusable = false,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                Classes = { "frame" },   // the black frame that parts them (DungeonWindow.axaml)
            };
            b.Click += (_, _) => { if (!ButtonMustWait(() => FireBuffer(which))) FireBuffer(which); Focus(); };
            BufferButtons.Children.Add(b);
        }
    }

    /// <summary>
    /// Nothing to refresh on the buttons themselves: a VB3 command button has no tooltip, and
    /// the original neither greys nor recaptions them. The port had done both.
    /// </summary>
    private void RefreshBufferButtons() { }

    /// <summary>
    /// The Messages window the original keeps beside the dungeon (help screenshot bm15.png):
    /// frmCharStat4!lstMessages, written by <c>sub085B</c> (MODULE6.BAS:3190-3203).
    ///
    /// It holds twenty lines and no more - the routine adds one and then drops the oldest as soon
    /// as the count passes <c>mc04F2 = 20</c> - and it always scrolls to the newest. Double-clicking
    /// it empties it (FRMCHAR4.BAS:84-88), which is the only way to clear it.
    /// </summary>
    private readonly List<string> _messages = new();

    /// <summary>
    /// Each line's <see cref="MessageLook"/> (kind, and the colour a mod gave it as it was added),
    /// one for one with <see cref="_messages"/>, carried with them to and from the town.
    /// </summary>
    private readonly List<MessageLook> _messageLooks = new();

    private void Log(string line) => Log(line, MessageKind.None);

    private void Log(string line, MessageKind kind)
    {
        // MessageList carries mc04F2 and sub085B's trim, and adds a repeated line as sub085B does
        var look = new MessageLook(kind, Mordor.Modding.ModHost.Active.MessageLineColour(line, kind));
        if (!MessageList.Add(_messages, _messageLooks, line, look, Mordor.Modding.ModHost.Active.MessagesKept(MessageList.Kept))) return;
        ShowMessages();
        Audio.Ding();       // sub085B plays MESSAGE.WAV for every line it adds
        Mordor.Modding.ModHost.Active.MessageLogged(line);
    }

    /// <summary>
    /// <c>timAilment_Timer</c>, FRMDUNM.BAS:594-603. After the wall noises and before the redraw,
    /// every living character who is badly hurt says so - which is how you find out that somebody
    /// behind you is nearly dead without opening the party window.
    ///
    /// It sits on this clock rather than on a move, so a party standing still still hears it.
    /// </summary>
    private void CallForHelp()
    {
        if (_party.Size <= 1) return;            // "If UBound(Chars) > False Then"
        foreach (var m in _party.Members.Where(m => !m.IsDead).ToList())
        {
            if (!HelpCry.Needed(m)) continue;
            Audio.Play(HelpCry.Sound);
            Log(HelpCry.Line(m));
        }
    }

    /// <summary>
    /// <c>sub085B</c>'s last two lines: the list holds what it holds, and the newest line is the
    /// selected one (MAINMOD2.BAS:3204-3205). Selecting it is also what scrolls it into view.
    /// </summary>
    private void ShowMessages()
    {
        MessageView.Show(MessageLog, _messages, _messageLooks);
    }

    /// <summary>
    /// The lines already in frmCharStat4's list as the dungeon opens: the original has the one
    /// Messages window for the whole game (sub085B, MAINMOD2.BAS:3194-3205), so nothing said in
    /// town is lost below. Carried silently: they were said, and dinged, already.
    /// </summary>
    public void CarryMessages(IReadOnlyList<string> lines, IReadOnlyList<MessageLook>? looks = null)
    {
        int kept = Mordor.Modding.ModHost.Active.MessagesKept(MessageList.Kept);
        // each line with its own look (one for one, or all plain)
        bool paired = looks != null && looks.Count == lines.Count;
        for (int i = 0; i < lines.Count; i++) MessageList.Add(_messages, _messageLooks, lines[i], paired ? looks![i] : MessageLook.Plain, kept);
        ShowMessages();
    }

    /// <summary>What the Messages window holds, for the city to carry on with.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>Each of <see cref="Messages"/>'s looks, one for one.</summary>
    public IReadOnlyList<MessageLook> MessageLooks => _messageLooks;

    /// <summary>lstMessages_DblClick: empties the window.</summary>
    private void OnClearMessages(object? s, Avalonia.Interactivity.RoutedEventArgs e) => ClearMessages();

    private void ClearMessages()
    {
        _messages.Clear();
        _messageLooks.Clear();
        ShowMessages();
    }

    /// <summary>
    /// <c>sub085B</c>: it appends to the Messages window and plays MESSAGE.WAV, and that list
    /// is the game's record of what has happened - and the only place the line goes. It used to be
    /// echoed into lblSquareInfo under the view (or a message line of the port's own under
    /// Objects &amp; Spells), where "You detect a Pit ahead of you." overwrote the square's own
    /// caption; lblSquareInfo is written only by the square routine and the death code
    /// (MAZEMOD.BAS:1545-1863, FRMDUNM.BAS:507-510, MAINMOD2.BAS:3052-3054).
    /// </summary>
    private void Say(string line)
    {
        Log(line);
        if (_combat != null) RefreshCombat();
    }

    /// <summary>Set the selected character's standing order for the rounds to come.</summary>
    private void SetOrder(OrderKind kind, short spellId = 0, bool keepItem = false)
    {
        // an order given to monsters who have not moved yet is what starts the fight
        if (kind != OrderKind.None) EngageWaiting();
        if (_combat == null) return;
        var o = _party.OrderFor(_ch);
        o.Kind = kind;
        o.SpellId = spellId;
        // the order keys set M5766 = -1 with it (MAINMOD2.BAS:3602-3604, 4030-4032, 4155). cmdCast_Click
        // does not (FRMCHAR3.BAS:180): M56C4 is now a spell of your own, and the old item slot goes
        // on losing a charge per cast of it (MAINMOD2.BAS:841-851) - kept
        KeepItemSlot(keepItem);
        // Fight, Defend and space set the order and say nothing (MAINMOD2.BAS:3594-3602,
        // 4011-4054); "X: Fight." in the combat list was this port's
        RefreshCombat();
        RefreshParty();
    }

    /// <summary>
    /// The square bits the fight reads, as they are where the party stands now. Every test is made
    /// live on the square at the moment it is asked: the round's cast on the caster's square
    /// (MAINMOD2.BAS:521), a companion's cast (MONSMOD.BAS:1039) and a monster's
    /// (2841) on the character's. A fight belongs to an area, not a square, so the party can walk
    /// onto an anti-magic square of the same area with the fight going on; the port had read the
    /// square once, as the fight started.
    /// </summary>
    private void SquareTests()
    {
        if (_combat == null) return;
        _combat.NoSpellsHere = _nav.Floor.Has(_nav.X, _nav.Y, Floor.ANTIMAGIC) || _nav.Floor.Has(_nav.X, _nav.Y, Floor.EXTINGUISHER);
        _combat.AntiMagicHere = _nav.Floor.Has(_nav.X, _nav.Y, Floor.ANTIMAGIC);
    }

    private void StartCombat(List<CombatMonster> group, bool isLair, bool monstersMoveNow)
    {
        // the fight is on: Foes(0).Engaged = 1 (MAINMOD2.BAS:4025-4026), and a room left mid-fight
        // is written back that way
        foreach (var m in group) m.Hostile = true;
        _combat = new CombatEngine(_party, group, Lookup, _rng, isLair);
        _combat.UseMonsterTable(_monsters);
        _combat.UseSpellTable(_spells);
        // a monster's kill runs sub0930 (MULTIMOD.BAS:362-363), which in a party is the list's
        // UPDATE there and then (MAINMOD2.BAS:3035-3037), after the companions are let go and
        // sub048C runs (3026-3033) - all Sub0930's; the UPDATE prunes (FRMGROUP.BAS:321-346).
        // mdrcharSetDead's sound, count and log (MAINMOD2.BAS:4558-4578) come first
        _combat.PartyUpdate = who =>
        {
            if (who.IsDead && !_countedDead.Contains(who)) { who.Deaths++; LogDeath(who); Audio.PlayOnNextChannel("DIE"); }
            Sub0930(who);
        };
        // after the characters' turns, anyone dead sends the list its UPDATE (MULTIMOD.BAS:542-553):
        // the prune alone, with no sub0930 in front, as a companion's mishap has none
        // (MONSMOD.BAS:901-1305). Each death is counted and logged first (MAINMOD2.BAS:4558-4578),
        // since a body pruned here is no longer on the list EndCombat counts from
        _combat.ListUpdate = CountThenPrune;
        // MONSMOD.BAS:2836 - no monster casts on an anti-magic or an extinguisher square
        // sub06FE Foes(l0456), True, False, False, pv0442 before each death in a group's turn
        // (MONSMOD.BAS:2756, 2826, 3042, 3095, 3227): the killer goes in the Library, read by the dead
        _combat.CharacterKilled = (victim, killer) => FileSightings(new[] { FoeOf(killer, 0, sighting: false) }, killed: true, victim);
        SquareTests();
        // a round's cast refused as "Anti-Magic area!": mdrmazPerceiveSquare 18, True (MAINMOD2.BAS:523),
        // as CheckCast does for a cast by hand
        _combat.AntiMagicRefused = who =>
        {
            if (who.IsLost) return;
            _map.Record(_level, _nav.X, _nav.Y, _nav.Floor.Width, Floor.ANTIMAGIC);
            _map.Note(_level, _nav.X, _nav.Y, _nav.Floor.Width, Floor.ANTIMAGIC);
            PushToAutomap();
        };
        // a character's blow or damage spell, for the mods (ModEvents.DamageDealt); nothing with none loaded
        _combat.DamageDealt = d => Mordor.Modding.ModHost.Active.DamageDealt(d);
        // and every award of experience (ModEvents.ExperienceGained)
        _combat.ExperienceGained = x => Mordor.Modding.ModHost.Active.ExperienceGained(x);
        _combat.SoundFxOff = _options.NoFX;
        _combat.NoPartySound = _options.NoPartyFX;
        _combat.NoteWhoIsAlive();
        _countedDead.Clear();
        Sub0BDE();   // sub0BDE -1 at the engage: the Combat Setting is what each character starts on
        // the fight starting is not announced: no "X attacks!" in the title, the message bar or
        // the list - MONSMOD writes no such line
        CombatTitle.Text = "";
        CombatHit.Text = "";
        CombatMonsterHit.Text = "";
        foreach (var id in group.Select(m => m.Def.Id).Distinct()) _ch.NoteMonster(id, killed: false);
        ShowPortraits(group);
        ShowState(group);
        // and no sound: the engage paths - the tick (MULTIMOD.BAS:583-586), F (MAINMOD2.BAS:4026-4036),
        // o (4069-4075), a spell or item (544-546, 6131-6136), a companion turning (MONSMOD.BAS:568-596) -
        // play none, nor do sub0BDE, monShowMonsters or monShowCursor. The port's roar by monster
        // kind had no source.
        WaitBtn.IsVisible = false;
        RefreshSheetSpells();   // the combat spells join the Objects & Spells list while engaged
        CombatPanel.IsVisible = true;
        // sub0A91 (MONSMOD.BAS:728) draws the chest as the monsters appear, before a blow is struck.
        // When it is already up for this group, the fight starting does not draw it again - nothing
        // calls monShowChest there, so the trap guess shown stays the one first shown.
        if (_chest != null && _chest.Source != null && _chest.Source == _combat.Source) _chest = _combat;
        else ShowChest();
        // No initiative roll here. When the monsters decide to attack, sub0A64 runs in that same
        // tick (MODULE7.BAS:581); when you attacked first, they simply take their turn at the
        // next tick. Nothing in the original decides who goes first.
        //
        // The DEX roll the community calls initiative is real, but it belongs elsewhere and does
        // something else: sub0A64 takes an argument naming its caller, and on the two paths that
        // are not this tick - you moved, you changed level - it rolls per monster group to decide
        // whether that group gets a free attack in (MULTIMOD.BAS:337 and 355). AttackKind carries
        // that argument and CombatEngine.AttackChance is the roll. Round is this call, and Round
        // is the one that never asks.
        if (monstersMoveNow) MonstersRound(AttackKind.Round);
        RefreshCombat();
        if (_combat.IsOver) EndCombat();
    }

    /// <summary>
    /// cmdCast_Click with monsters engaged (FRMCHAR3.BAS:174-184): the spell is not thrown there
    /// and then but becomes <c>CombatOrders(Me).M56C4</c>, the standing order, and monShowCursor
    /// puts "&gt;" beside the group. This was the combat panel's own Cast button; that panel is
    /// gone, and the Objects &amp; Spells window's Cast is the one the original has.
    ///
    /// Reached with monsters engaged, or waiting and the spell a combat or an either one
    /// (<see cref="BecomesOrder"/>). The item path is the port's, kept from that button: an item
    /// has charges rather than a standing order.
    /// </summary>
    /// <summary>
    /// What a refusal from <c>mdrcharCheckCast</c> leaves as the order. The routine opens with
    /// <c>CombatOrders.Att = False</c> (MAINMOD2.BAS:433) and every refusal sets <c>M56C4 = -1</c>
    /// (453, 479, 493, 508, 517-518, 532-533, 539-540): no order at all. The anti-magic refusal goes
    /// through <c>sub102F pv, -1</c> instead (525), which with a spell still in M56C4 clears it and
    /// sets Att to Fight (MULTIMOD.BAS:467-479). Engaged or waiting alike. The line is a sub085B.
    ///
    /// An item's cast (sub15CD, MAINMOD2.BAS:6130) puts nothing in M56C4 first, so on an anti-magic
    /// square it depends on the order already standing: a spell there is cleared to Fight as above;
    /// none (M56C4 = -1 with Att just zeroed) takes sub102F's first branch, Defend (470-474).
    /// </summary>
    private void RefusedOrder(Refusal said, Character? caster = null)
    {
        var who = caster ?? _ch;
        var o = _party.OrderFor(who);
        o.CheckCastClearsAtt();                       // Att = False (MAINMOD2.BAS:433)
        if (said.Kind == RefusalKind.AntiMagic) o.Sub102FMinusOne();   // sub102F pv, -1 (525)
        else o.Kind = OrderKind.None;                 // M56C4 = -1
        o.SpellId = 0;
        KeepItemSlot(!said.ClearsItemSlot, who);
        Log(said.Text);
        ShowCursor();   // cmdCast_Click's monShowCursor after the check, passed or not (FRMCHAR3.BAS:182)
        RefreshParty();
    }

    /// <summary>
    /// M5766 after an order change that leaves it (<paramref name="keep"/>): M56C4 no longer holds
    /// the item's own spell, so the slot is only charged. Otherwise it is set to -1.
    /// </summary>
    private void KeepItemSlot(bool keep, Character? caster = null)
    {
        if (_combat == null) return;
        var who = caster ?? _ch;
        if (keep && _combat.ItemOrders.TryGetValue(who, out var io)) _combat.ItemOrders[who] = io with { ItemCast = false };
        else _combat.ItemOrders.Remove(who);
    }

    /// <summary>
    /// <c>sub0BDE</c> (MULTIMOD.BAS:132-199) for the whole party, or for one member
    /// (<paramref name="only"/>, as the join passes <c>UBound(Chars)</c>). Nothing unless monsters
    /// are here and engaged (148, 167). Each member with no order - "Att = False And M56C4 = -1" -
    /// has any chest count dropped (gv1E7C = False, 166) and takes up their Combat Setting, keeping
    /// the group aimed at unless it is gone (159-164); a setting on an item buffer runs sub15CD
    /// (174-175, <see cref="CombatSettingItem"/>).
    /// </summary>
    private void Sub0BDE(Character? only = null)
    {
        if (_combat == null || _combat.IsOver) return;
        foreach (var m in only != null ? new[] { only } : _party.Members.ToArray())
        {
            var o = _party.OrderFor(m);
            if (o.Kind != OrderKind.None) continue;
            _combat.ChestRounds[m] = 0;
            int aimed = o.Target;
            o.ApplyDefault(m, _spells);
            o.Target = aimed;
            CombatSettingItem(m);
            CombatSettingSpell(m);
        }
    }

    /// <summary>
    /// sub0BDE's spell branch (MULTIMOD.BAS:176-183): a Combat Setting on a buffer holding a spell
    /// puts it in M56C4 and asks <c>mdrcharCheckCast(l003A, M56C4, False, False)</c> - no item level,
    /// so the points are asked. The wording goes by gv1A82, not the gv1A84 set around it
    /// (MAINMOD2.BAS:447), which is <see cref="CheckCast"/>'s own test. A refusal leaves what the
    /// check leaves (<see cref="RefusedOrder"/>: M56C4 = -1, or sub102F pv, -1 on an anti-magic
    /// square, MAINMOD2.BAS:525) and then "sub102F l003A, mc0068" (MULTIMOD.BAS:181) overwrites it
    /// whatever the refusal was: M56C4 = -1, M5766 = -1, Att = 1 (MULTIMOD.BAS:480-483) - the member
    /// fights instead. A pass keeps the spell as the order; the check has nothing to engage, since
    /// sub0BDE only gets here with Foes(0).Engaged = 1 (MULTIMOD.BAS:167).
    /// </summary>
    private void CombatSettingSpell(Character m)
    {
        if (_combat == null || _combat.IsOver) return;
        var o = _party.OrderFor(m);
        if (o.Kind != OrderKind.Spell) return;
        if (m.CombatSetting < 3) return;
        if (!SpellBuffers.IsSpell(m.Buffers.ElementAtOrDefault(m.CombatSetting - 3))) return;
        var spell = _spells.FirstOrDefault(s => s.Id == o.SpellId);
        if (spell == null) return;
        if (CheckCast(spell, 0, m) is not { } refused) return;
        RefusedOrder(refused, m);
        // sub102F l003A, mc0068 (MULTIMOD.BAS:181, 480-483)
        o.Kind = OrderKind.Fight;
        o.SpellId = 0;
        KeepItemSlot(false, m);
        RefreshParty();
    }


    /// <summary>
    /// sub0BDE's item branch (MULTIMOD.BAS:171-175): a Combat Setting on a buffer that holds a pack
    /// slot - anything below 40, an empty buffer's -1 included - is <c>sub15CD member, slot</c>,
    /// the Use command, run for that member as the setting is taken up. sub15CD's guards come
    /// first (MAINMOD2.BAS:6093-6127), each line a sub085B. An item with a spell goes the combat
    /// way, since sub0BDE runs only with Foes(0).Engaged = 1: mdrcharCheckCast at the item's level,
    /// and a pass makes it the order with nothing thrown and no charge spent (6129-6137); its own
    /// "sub0BDE -1" at 6132 is turned back by gv1EE6, already set. An item with no spell falls
    /// through to the charge and whatever else it does (6145 on) - used up as the fight begins.
    /// </summary>
    private void CombatSettingItem(Character m)
    {
        if (_combat == null || _combat.IsOver) return;
        int buffer = m.CombatSetting - 3;
        if (buffer < 0 || buffer >= m.Buffers.Length) return;
        int b = m.Buffers[buffer];
        if (SpellBuffers.IsSpell(b)) return;                        // the spell branch (176-185)
        if (m.IsDead) return;                                        // MAINMOD2.BAS:6093
        if (b < 0 || b > SpellBuffers.PackSlots - 1) return;         // 6094-6095: an empty buffer ends here
        // "If Pack(p0852).Ref < 1 And p0852 > 0 Then Exit Sub" (6096): slot 0, the Hands, goes on to
        // Items(0), whose Charges of 0 get "the Item doesn't appear to do much of anything." (6098-6100)
        if (b > 0 && m.Pack[b] is not { ItemId: > 0 }) return;
        if (m.Pack[b] is not InventorySlot slot) return;
        _itemsById.TryGetValue(slot.ItemId, out var it);
        if (it == null) return;
        if (ItemUse.Problem(m, slot, it, Lookup) is string why)
        {
            if (why != "") Log(why);
            return;
        }
        if (ItemUse.SpellOf(it, _spells) is Spell spell)
        {
            if (CheckCast(spell, it.SpellLvl, m) is { } refused) { RefusedOrder(refused, m); return; }
            var o = _party.OrderFor(m);
            o.Kind = OrderKind.Spell;                                // Att = False : M56C4 = spell + 1000
            o.SpellId = spell.Id;
            _combat.ItemOrders[m] = new CombatEngine.ItemOrder(slot, it, ItemCast: true);   // M5766 = slot
            return;
        }
        // mdrcharSubtractItemUse and the rest of sub15CD (MAINMOD2.BAS:6145-6225)
        Spellbook.SpendCharge(m, slot, it);
        var hall = HallRecords.Load(SavesDir);
        var lines = ItemUse.ApplySpecial(m, slot, it, hall, SavesDir, _party.Size > 1, Lookup).ToList();
        hall.Save(SavesDir);
        foreach (string line in lines)
        {
            if (ItemUse.SoundIn(line) is string sound) Audio.PlayOnNextChannel(sound);
            else Log(line);
        }
        if (m == _ch) { RefreshPack(); RefreshSheetSpells(); RefreshBuffers(); }
        // sub1658 lists Chars(Me) - the window's character, whoever used the item (MAINMOD2.BAS:6230, 5518)
        if (ItemUse.ChangesStats(m, slot, it)) SpellListChecked();
    }

    private async System.Threading.Tasks.Task CastInCombat(Spellbook.Castable c, bool fromBuffer = false)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        if (!MonstersHere) return;
        // cmdCast_Click (FRMCHAR3.BAS:174-184) has no paralysis test. A known spell becomes the order
        // first (M56C4 = l00E0) and only then does mdrcharCheckCast run - and a refusal takes the
        // order away again (see RefusedOrder)
        //
        // Every spell of your own goes this way, the utility ones included. The port had thrown a
        // utility spell on the spot instead, and since mdrcharCheckCast turns away anything
        // non-combat here, what that reached was Teleport - moved mid-fight where the original makes
        // it the order and, with monsters present, fires it at them (MAINMOD2.BAS:856).
        if (!c.FromItem)
        {
            if (!Engaged)
            {
                // monsters waiting: the gate first, and only a spell it lets through starts the
                // fight (MAINMOD2.BAS:537-546). The spell is the order before the gate asks, as
                // at FRMCHAR3.BAS:180, which is what an anti-magic refusal turns into Fight.
                var pending = _party.OrderFor(_ch);
                pending.Kind = OrderKind.Spell; pending.SpellId = c.Spell.Id;
                if (CheckCast(c.Spell, 0) is { } no) { RefusedOrder(no); return; }
                SetOrder(OrderKind.Spell, c.Spell.Id, keepItem: !fromBuffer);
                return;
            }
            SetOrder(OrderKind.Spell, c.Spell.Id, keepItem: !fromBuffer);
            if (CheckCast(c.Spell, 0) is { } said) RefusedOrder(said);
            return;
        }
        // an item: mdrcharCheckCast at its own level, and a pass engages (6130-6132)
        if (CheckCast(c.Spell, c.Item!.SpellLvl) is { } refused)
        {
            RefusedOrder(refused);
            return;
        }
        // "Foes(0).Engaged = mc0138 : sub0BDE -1" (MAINMOD2.BAS:6131-6132)
        EngageWaiting();
        if (_combat == null || _combat.IsOver) return;
        Sub0BDE();   // already engaged, sub0BDE runs all the same (6132)

        // then the item becomes the order, not a cast: "Att = False : M56C4 = l0862 + 1000 :
        // M5766 = p0852 : sub0B9B : monShowCursor : Exit Sub" (MAINMOD2.BAS:6133-6137). Nothing is
        // thrown and no charge is spent here - the Exit Sub is before mdrcharSubtractItemUse (6143).
        // Each round casts it at the item's level and takes a charge (MONSMOD.BAS:312-313,
        // MAINMOD2.BAS:841-852; CombatEngine.ItemOrderActs). The port had cast it once, at once.
        var o = _party.OrderFor(_ch);
        o.Kind = OrderKind.Spell;
        o.SpellId = c.Spell.Id;
        _combat.ItemOrders[_ch] = new CombatEngine.ItemOrder(c.Slot!, c.Item, ItemCast: true);
        RefreshCombat();
        RefreshParty();
    }

    /// <summary>
    /// The monster's own picture off the sheet. The cutting is <see cref="MonsterPortrait"/>, which
    /// the Library needs as well. Asmodeus gets no portrait in the strip at all (fn0C3B, see
    /// <see cref="ShowPortraits"/>); the dungeon view and the Library draw his own picture.
    /// </summary>
    private Bitmap? LoadPortrait(Monster m) => MonsterPortrait.Of(m);

    /// <summary>Centre of picMonsters' portrait band, ScaleWidth / 2, in the panel canvas.</summary>
    private double PortraitCentre = 182;   // and half the width a larger Options font adds (FitDungeonDrawing)

    /// <summary>
    /// <c>monShowMonsters</c>' portrait strip (MONSMOD.BAS:4351-4396), at the port's scale of one.
    /// Every Foes() slot with GroupCount &gt; 0 counts (4351-4355); when the lead slot is Asmodeus
    /// - <c>fn0C3B</c> (MONSMOD.BAS:747-756) is True for monster id 277 with a count above nought -
    /// the count drops by one, not below one (4356-4359). Each counted slot that is not Asmodeus is
    /// then drawn 60x60 (mc0674) at x = centre - 30 * count + (n - 1) * 60, n its place among the
    /// drawn, and y 7 (4383, 4385) - 6 on this canvas, which, like the rest of picMonsters here, takes
    /// the original's co-ordinates less one: sub0B9B's 3,8 is StateIcon's 2,7, monShowChest's y 8 is
    /// ChestIcon's 7, ScaleWidth / 2 is PortraitCentre's 182, and in game_files/dmainw1.png the
    /// portraits' top edge is one row above the skull's, as here - off the sheet cell M5A1B - 1
    /// (4371-4372). A slot whose monster
    /// is invisible (bit mc0154 of M55BE) is painted over in the back colour instead, unless the
    /// character in command has See Invisible, Ailments(mc0156) (4381-4393).
    ///
    /// The original wipes the band only when the count changed (4363-4365) and otherwise paints
    /// over the same places; rebuilding it each time shows the same result. Called wherever the
    /// port redraws the groups: an encounter shown or engaged, every RefreshCombat (a group dying,
    /// a round, a target), and the character switch (mdrmulSwitch, MULTIMOD.BAS:628-630, via
    /// ShowCursor).
    /// </summary>
    private void ShowPortraits(IReadOnlyList<CombatMonster> monsters)
    {
        MonsterStrip.Children.Clear();
        var slots = CombatMonster.Runs(monsters);
        bool Counted(List<CombatMonster> s) => s.Any(m => m.Alive);          // Foes(n).GroupCount > 0
        bool Fn0C3B(List<CombatMonster> s) => Counted(s) && Asmodeus.Is(s[0].Def);
        int count = slots.Count(Counted);
        if (slots.Count > 0 && Fn0C3B(slots[0])) count = Math.Max(1, count - 1);
        int drawn = 0;
        foreach (var slot in slots)
        {
            if (!Counted(slot) || Fn0C3B(slot)) continue;
            drawn++;
            var def = slot[0].Def;
            if (MonsterProperty.Has(def, MonsterProperty.Invisible) && !_ch.SeeingInvisible) continue;   // a blank
            var img = new Image
            {
                Source = LoadPortrait(def), Width = MonsterPortrait.Cell, Height = MonsterPortrait.Cell,
                Stretch = Stretch.Fill,
            };
            RenderOptions.SetBitmapInterpolationMode(img, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            Canvas.SetLeft(img, PortraitCentre - MonsterPortrait.Cell / 2.0 * count + (drawn - 1) * MonsterPortrait.Cell);
            Canvas.SetTop(img, 6);   // y 7 (MONSMOD.BAS:4383), less one: see above
            MonsterStrip.Children.Add(img);
        }
    }

    /// <summary>
    /// The toolbar's Fight. Like every button on that bar it only queues its key - Fight is "F"
    /// (FRMDUNM.BAS:76-109) - so it does what F does: engages monsters that have not moved yet and
    /// sets the Fight order (MAINMOD2.BAS:4011-4036). It used to return unless a fight had already
    /// started, so the button did nothing to a group standing in front of you.
    /// </summary>
    private void OnAttack(object? s, Avalonia.Interactivity.RoutedEventArgs e) => Press(Key.F);

    /// <summary>
    /// <c>sub07E2 .25#</c> in MODULE4.BAS:366 and 499 - the original waits a quarter of a second
    /// before and after each swing of the character you are watching, pumping messages while it
    /// does. That is why a fight reads as a rhythm of separate blows rather than a burst, and it
    /// is the pacing the port was missing: a round resolved instantly and then the clock sat idle
    /// for the rest of the second.

    /// </summary>
    private const int SwingPauseMs = 250;

    /// <summary>
    /// Play out one round, drawing the watched character's blows a quarter-second apart. The
    /// re-entry guard means a long round simply delays the next tick, which is what the original
    /// does too - a seven-swing Vorpal Blade genuinely takes longer than a second to resolve.
    /// </summary>
    private async void PlayRound()
    {
        try
        {
            if (_combat == null) return;
            // the fight this round is drawing. A step out of the area can end it or put another in its
            // place while a blow is on screen; the round then stops rather than carry on with that one
            var fight = _combat;
            // an item order spends a charge per cast (MAINMOD2.BAS:841-843)
            bool itemOrders = _combat.ItemOrders.Count > 0;
            SquareTests();
            foreach (var line in _combat.RoundPaced(_spells))
            {
                if (_combat != fight) return;
                // the dead screen turned the clock off: "If frmDungeonMain!timCombat.Enabled = False
                // Then Exit Sub" (MONSMOD.BAS:294, 374, 506) - nothing more of the round is fought or
                // drawn. What had come after a death was "You Killed One!" and a monster's spell
                if (_combatClockOff) break;
                // No "lone and wiped" stop here: the engine takes the hits off before it yields the
                // blow's caption (MonsterAttack.GroupAttacks), so a test on this line lost the
                // killing caption and its sounds, sub085B's "You were killed by a X."
                // (MONSMOD.BAS:3036-3040) and the Dies() behind it. The engine itself ends the
                // groups once a lone character is down ("If UBound(Chars) = False Then Exit For",
                // MULTIMOD.BAS:364) and RoundPaced stops on IsOver; EndCombat then runs sub0930
                // a count that ran out: monOpenChest for that character, in the middle of the round
                if (line.OpensChest && line.Actor != null) { OpenChestBy(line.Actor); continue; }
                if (line.Channel == LineChannel.Log)
                {
                    // nothing to show, but not nothing to hear: a line on the log alone is how the
                    // engine carries a sound with no caption - a monster's spell (fn0FF5's sub1005,
                    // sub0D35, sub0E99, MONSMOD.BAS:245, 2847-2852), a companion's death cry
                    // (sub0CB7, 2723-2725, 2799-2801, 2861-2863, 2966-2968, 3103-3105), DRAIN,
                    // STONE, STEAL and BYE for a member not on screen (3183, 3213, 3369, 3377), a
                    // companion's breath or spit (931-933, 1011-1013). This branch had played the
                    // picks alone, so in a timed round every one of those was silent; MonstersRound
                    // played them. Now both play every line through CombatSounds.
                    LineSounds(line);
                    continue;
                }
                if (line.Channel == LineChannel.CharacterHit && line.Actor != _ch)
                {
                    // another member's blow: the original plays its sounds (NoPartyFX off) and
                    // shows nothing, and it does not wait
                    LineSounds(line);
                    continue;
                }
                if (line.Channel == LineChannel.CharacterHit)
                {
                    // MONSMOD.BAS:344-372: the swing sound, the caption cleared, and a quarter
                    // second before the blow is even rolled; then (MONSMOD.BAS:444-504) the hit
                    // sounds and the result for another quarter. Lines that swing nothing do not
                    // wait at all. The swing sound goes before the wait (MONSMOD.BAS:344-350) and
                    // the blow's own after it (MONSMOD.BAS:444-452 HIT, 472-473 the death cry) -
                    // one each. The port had played
                    // the whole line at both points, so every blow was heard twice.
                    LineSounds(line, swing: true, blow: false);
                    if (line.Paced)
                    {
                        CombatHit.Text = "";
                        await System.Threading.Tasks.Task.Delay(SwingPauseMs);
                        if (_combat != fight) return;
                    }
                    LineSounds(line, swing: false, blow: true);
                    CombatHit.Text = line.Text;
                    // a damage spell's caption carries the group it was written for: the order's
                    // target goes there as the caption shows, so the mark walks along with the spell
                    // (MAINMOD2.BAS:1456-1460), and the empty line after the last caption carries
                    // where the loop put the target back (1582-1586), for monShowCursor's redraw (1590)
                    if (line.AimAt is int aimAt) _party.OrderFor(_ch).Target = aimAt;
                    if (line.Text != "")
                        Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.CharacterHit, line.Text, line.Actor);
                    RefreshCombat();
                    RefreshParty();
                    if (line.Paced)
                    {
                        await System.Threading.Tasks.Task.Delay(SwingPauseMs);
                        if (_combat != fight) return;
                    }
                    continue;
                }
                // the monsters' side: sounds for everyone, the caption (lblMonsterHit) with its
                // quarter-second wait for the character being watched, messages on the bar
                LineSounds(line);
                if (line.Channel == LineChannel.MonsterHit && line.Actor == _ch)
                {
                    CombatMonsterHit.Text = line.Text;
                    Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.MonsterHit, line.Text, line.Actor);
                    RefreshCombat();
                    RefreshParty();
                    if (line.Paced)
                    {
                        await System.Threading.Tasks.Task.Delay(SwingPauseMs);
                        if (_combat != fight) return;
                    }
                }
                else if (line.Channel == LineChannel.Message)
                {
                    // a sub085B line, into the Messages window as it is said - and not also into
                    // lblSquareInfo, which is the square's
                    Log(line.Text, line.Kind);
                    RefreshCombat();
                    RefreshParty();
                }
            }



            if (_combatClockOff) { RefreshParty(); return; }   // the dead screen is up: see above
            AfterRoundSounds();
            RefreshCombat();
            RefreshParty();
            if (itemOrders) { RefreshSheetSpells(); RefreshPack(); RefreshBuffers(); }
            if (_combat == fight && fight.IsOver) EndCombat();
        }
        finally { _keepRoundGuard = false; _inRound = false; }
    }

    /// <summary>
    /// The sounds one line of a fight makes, and whose they are.
    ///
    /// "No Party Sound" (M5F48) is tested at every one of them, always in the same shape:
    ///
    /// <code>
    /// If (gv1A66.M5F48 = True And pv00F4 = Me) Or gv1A66.M5F48 = False Then
    /// </code>
    ///
    /// <c>Me</c> is the index of the character being played, so with the box ticked you hear only
    /// your own blows and the blows landed on you - "all background combat sounds (from other
    /// characters and companions in the party)" go quiet, which is the help's own description of
    /// it. MONSMOD.BAS:344, 444, 471, 890, 913 and MAINMOD2.BAS:1452, 1531 are all that line.
    ///
    /// A line with no character to it - the cry for help, a monster spell's sub0E99 - is not
    /// guarded in the original either. The rule itself is <see cref="CombatSounds.Of"/>, which the
    /// self-check runs over scripted rounds.
    /// </summary>
    private void LineSounds(CombatEngine.CombatLine line, bool swing = true, bool blow = true) =>
        Audio.PlayAll(CombatSounds.Of(line, _ch, _options.NoPartyFX, swing, blow));

    /// <summary>
    /// The monsters' turn outside the timer - the engage tick and a square change. The original
    /// waits a quarter second on each caption here too, but the callers need it finished before
    /// they go on, so the port plays it straight through and leaves the last caption showing.
    /// </summary>
    private void MonstersRound(AttackKind kind)
    {
        if (_combat == null || _combat.IsOver) return;
        SquareTests();
        var aliveBefore = _party.Members.Where(m => m.CurHits > 0).ToList();
        foreach (var line in _combat.MonsterAttacks(kind))
        {
            LineSounds(line);
            if (line.Channel == LineChannel.MonsterHit && line.Actor == _ch)
            {
                CombatMonsterHit.Text = line.Text;
                Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.MonsterHit, line.Text, line.Actor);
            }
            else if (line.Channel == LineChannel.Message) Log(line.Text, line.Kind);   // sub085B, the list alone
        }
        // mdrcharSetDead's sound (MAINMOD2.BAS:4558) for a death here. One pruned mid-round had it
        // in PartyUpdate; the selection may have moved off them, so _ch is not the one to ask
        if (aliveBefore.Any(m => m.CurHits <= 0 && _party.Members.Contains(m))) Audio.PlayOnNextChannel("DIE");
    }



    /// <summary>
    /// <c>sub0B9B</c> (MONSMOD.BAS:4434-4471), the picture at the left-hand end of picMonsters.
    /// A skull and crossbones while the group is hostile, a peace sign while it is not, and a
    /// demon's head in flames for Asmodeus - the original tests his monster id here exactly as it
    /// does for his chest. It is the only thing on screen that tells a group standing on your
    /// square apart from a group fighting you, and the port had neither picture.
    /// </summary>
    private void ShowState(List<CombatMonster> group)
    {
        if (group.Count == 0) { StateIcon.Source = null; return; }
        bool asmodeus = Asmodeus.Is(group[0].Def);
        StateIcon.Source = MiscPics.Get(
            !group[0].Hostile ? MiscPics.AtPeace
                              : asmodeus ? MiscPics.AsmodeusHostile : MiscPics.Hostile);
    }

    /// <summary>The chest an encounter left, waiting to be opened; null when there is none.</summary>
    private CombatEngine? _chest;
    /// <summary>lblTrap.Visible: whether the chest is on screen for the character in command.</summary>
    private bool _chestSeen;

    /// <summary>l0510: what opening it turned out to be, for lblTrap to say afterwards.</summary>
    private string _trapCaption = "";
    /// <summary>The last trap caption told to the mods (CaptionShown), so a repaint of the same readout is not told again.</summary>
    private string _trapReadoutTold = "";
    /// <summary>Which chest <see cref="_trapReadoutTold"/> was about: its encounter (Fight record), or the fight when it has none.</summary>
    private object? _trapReadoutChest;

    /// <summary>
    /// sub0A91's monShowChest (MONSMOD.BAS:728) for a group that has not moved: fn0D4D rolled the
    /// chest with the monsters, so it is there to be seen before any blow is struck.
    /// </summary>
    private void ShowWaitingChest(List<CombatMonster> group)
    {
        if (_combat != null) return;
        _combat = new CombatEngine(_party, group, Lookup, _rng);
        ShowChest();
        _combat = null;
    }

    /// <summary>
    /// The chest panel, <c>monShowChest</c> (MONSMOD.BAS:4124-4176). A locked chest says so and
    /// nothing else. Otherwise you get a trap name - which may well be the wrong one - the
    /// diamonds saying how sure of it you are, and the gauge saying how likely you are to disarm
    /// whatever is really there.
    /// </summary>
    private void ShowChest()
    {
        // Fight is what sub0A91 read for the loaded area (MONSMOD.BAS:687): no other area has a chest here
        if (_combat != null && !FightIsHere(_combat)) return;
        if (_combat == null || _combat.ChestClass <= 0)
        {
            // "Else picMonsters.Line ... picMonsters.BackColor, BF" (MONSMOD.BAS:4114) - a fight
            // that left nothing paints that corner back to the window's own colour
            _chest = null; ChestIcon.Source = null; ChestPanel.IsVisible = false; _chestSeen = false; return;
        }
        _chest = _combat;
        // "If Chars(Me).Hits <= False Then Exit Sub" (MONSMOD.BAS:4090): the chest is still there
        // for the others, but nothing is drawn for a character with no hits
        if (_ch.CurHits <= 0) return;
        // the chest belongs to Monsters(Fight.M4F24), the encounter's lead, which the Fight record
        // keeps after every group has gone
        var lead = _combat.ChestLead;
        if (lead == null) return;

        // MONSMOD.BAS:4097-4113: the picture is drawn whenever there is a chest, monsters or no -
        // cell (2 + (TreasureClass - 2)), which is the class itself, unless the lead is Asmodeus,
        // whose own chest it is then whatever the class rolled
        ChestIcon.Source = MiscPics.Get(Asmodeus.Is(lead) ? MiscPics.AsmodeusChest : MiscPics.Chest(_combat.ChestClass));

        // MONSMOD.BAS:4118-4128. With a group standing the trap panel may not be shown:
        //     Abs(Log(DEX / 4) + Log(DEX / 4) * (disarm / 100 - .5)) - Log(Foes(0)'s DEX / 8) < .5
        // hides picTrap, lblTrap and lblDisarm - not the picture - and a chest whose trap panel is
        // hidden cannot be opened in the fight, because the Open key asks whether lblTrap is
        // showing. It is the character's own DEX, without gear, against group 0's monster. A DEX
        // of nought on either side is a Log error, and monShowChest's handler leaves the panel as
        // it was.
        _chestSeen = true;
        var standing = _combat.Monsters.FirstOrDefault(m => m.Alive);
        if (standing != null)
        {
            var front = _combat.Monsters.FirstOrDefault(m => m.Group == 0) ?? _combat.Monsters[0];
            double dex = _ch.Stats[(int)Stat.Dex];
            var frontDef = front.Def;   // Monsters(Foes(0).Ref): the creature itself, not the first of its Id
            double frontDex = frontDef.Stats.Length > 5 ? frontDef.Stats[5] : 0;
            if (dex <= 0 || frontDex <= 0) return;
            double disarm = Rules.BestAbilityChance(_ch, 0, _level, _combat.ChestGuard, Lookup);   // Stats + StatMods (MAINMOD2.BAS:1945-1946)
            double seen = Math.Log(dex / 4);
            if (Math.Abs(seen + seen * (disarm / 100 - .5)) - Math.Log(frontDex / 8) < .5)
            {
                _chestSeen = false;
                ChestPanel.IsVisible = false;
                return;
            }
        }

        if (Rules.MagicallyLocked(_combat.ChestLocked))
        {
            ChestText.Text = Rules.LockedCaption;
            ChestGauge.Text = "";
        }
        else
        {
            // the readout reads Monsters(Fight.M4F24) too (MONSMOD.BAS:4135)
            var def = lead;   // the creature itself, not the first of its Id
            int monDex = def.Stats.Length > 5 ? def.Stats[5] : 10;
            // fn122A leaves the winning guild's level behind in gv141C.M5B9F, and the confidence
            // term reads it back (MONSMOD.BAS:4139)
            var (_, guildLevel) = CombatEngine.WinningGuild(_ch, 0);
            var (gauge, diamonds, shown) = Rules.TrapReadout(
                _rng,
                _ch.Stats[(int)Stat.Wis],
                _ch.EffectiveStat((int)Stat.Wis, Lookup) - _ch.Stats[(int)Stat.Wis],
                _combat.ChestGuard, monDex,
                Rules.BestAbilityChance(_ch, 0, _level, _combat.ChestGuard, Lookup),
                guildLevel, _combat.ChestTrap);
            ChestText.Text = ChestTraps.Names[shown];
            ChestGauge.Text = gauge + " " + new string('♦', diamonds);
        }
        // told to the mods when the readout changes, not at every repaint of the same one; a chest
        // from another encounter is news even when its readout reads the same as the last
        object chestIs = (object?)_combat.Source ?? _combat;
        if (!ReferenceEquals(chestIs, _trapReadoutChest)) { _trapReadoutChest = chestIs; _trapReadoutTold = ""; }
        string readout = (ChestText.Text + " " + ChestGauge.Text).Trim();
        if (readout != _trapReadoutTold)
        {
            _trapReadoutTold = readout;
            Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.Trap, readout, _ch);
        }
        ChestPanel.IsVisible = true;
    }

    /// <summary>
    /// Clicking the chest picture, which the help lists beside the `o` key: "To open a chest
    /// during peacetime or combat, either press 'o' or click on the chest." It is the same
    /// command, so it is the same routine - and a magically locked chest ignores both.
    /// </summary>
    private void OnChestClicked(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        OpenTheChest();
        e.Handled = true;
    }

    /// <summary>
    /// `o` and a click on the chest: <c>fn1521</c> case 79 (MAINMOD2.BAS:4063-4090). Nothing for a
    /// dead or paralysed character, or a magically locked chest. With nothing left standing it is
    /// opened there and then. With monsters about it becomes a fight - the orders are reloaded
    /// (sub0BDE), yours are cleared - and if the chest is on screen for you, the count starts, or
    /// stops if it was running: "it usually takes a number of rounds and makes your character much
    /// easier to hit."
    /// </summary>
    private void OpenTheChest()
    {
        // "If Chars(Me).DeathState = False And ..." (MAINMOD2.BAS:4064) - DeathState only; a
        // character standing on 0 hits who has not been set dead yet may still open it
        if (_ch.IsDead) return;
        if (_ch.Paralysed) return;                                        // Ailments(2) = 0 (MAINMOD2.BAS:4064)
        // Fight.ChestTrap is the loaded area's record (MONSMOD.BAS:687): a chest left in another
        // area is not there to be opened
        if (_chest == null || !FightIsHere(_chest)) return;
        if (Rules.MagicallyLocked(_chest.ChestLocked)) return;   // "nothing will happen"

        // with a group standing that has not moved, this is what starts the fight:
        // "Foes(0).Engaged = mc0138" (MAINMOD2.BAS:4068-4069)
        // - it sets Engaged and redraws the monsters and the cursor (4068-4075), with no sound and
        // no fresh look at the chest
        if (_combat == null && _waiting != null && _waiting.Any(m => m.Alive)) EngageWaiting();
        bool monstersStand = _combat != null && !_combat.IsOver && _combat.Monsters.Any(m => m.Alive);
        if (!monstersStand)
        {
            if (_chest.GoldEarned > 0) OpenChestBy(_ch);           // "And Fight.Gold > 0"
            // and then fn1521's own tail (MAINMOD2.BAS:4089-4092), which the count-down open
            // (MULTIMOD.BAS:527) does not have: "If Chars(Me).Hits < 1 Then M5906(1) += 1 : sub0930
            // l0540". l0540 is a plain Dim of fn1521 and nothing on this path assigns it, so it is
            // slot 0 - the leader. By now monOpenChest's UPDATE has pruned everyone at nought from a
            // party, so Me is below 1 only when one character is left, and slot 0 is Me: a second
            // death counted, and sub0930 on them.
            if (_ch.CurHits < 1)
            {
                _ch.Deaths++;
                Sub0930(_party.Members[0]);
            }
            return;
        }

        // sub0BDE -1: everybody without an order drops any count and takes up their Combat
        // Setting again (MAINMOD2.BAS:4070); then this character's own orders are cleared
        Sub0BDE();
        _party.OrderFor(_ch).Clear();
        _combat?.ItemOrders.Remove(_ch);   // M5766 = -1 with them (MAINMOD2.BAS:4073)

        if (_chestSeen)
        {
            if (_combat!.ChestRoundsOf(_ch) > 0) _combat.ChestRounds[_ch] = 0;
            else if (CombatEngine.OpeningRounds(Rules.BestAbilityChance(_ch, 0, _level, _combat.ChestGuard, Lookup)) is int n)
                _combat.ChestRounds[_ch] = n;
            CombatHit.Text = _combat.ChestCaption().Text;
            Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.Chest, CombatHit.Text, _ch);
        }
        RefreshCombat();
        RefreshParty();
    }

    /// <summary>
    /// <c>monOpenChest</c> for <paramref name="opener"/> - the character in command when the fight
    /// is over, or whoever's count ran out in the middle of one.
    /// </summary>
    private void OpenChestBy(Character opener)
    {
        if (_chest == null || !FightIsHere(_chest)) return;
        _combat ??= _chest;
        _trapCaption = "";
        _trapReadoutTold = "";
        void Out(IEnumerable<string> said)
        {
            // straight to the Messages window as each is said; they used to wait in the fight's
            // log for a batch at the end of it
            foreach (string line in said) Log(line, MessageKind.Chest);
        }
        // pv04F2 is a slot, taken before anything below can move the list
        int openerSlot = _party.Members.IndexOf(opener);
        // monOpenChest springs the trap, then pays the gold, then goes through the treasure
        Out(OpenChest(_chest, opener));
        if (_chestTeleport is { } thrown)
        {
            _chestTeleport = null;
            ChestTeleport(opener, thrown);
            return;
        }

        // "If UBound(Chars) > False Then frmParty!lblCall = "UPDATE"" (MONSMOD.BAS:3653): in a party
        // the list is pruned first - everyone the trap left on nought is set dead (an ordinary
        // death), "\"X\" has died!" for those on the leader's floor, and unloaded (FRMGROUP.BAS:321-346).
        // The last one standing is never pruned. The port had skipped this.
        PruneTheDead();

        // MONSMOD.BAS:3654-3658. Then Chars(m01BC).Hits with m01BC = 0 - the leader's hits, after
        // the prune - so it can only be below 1 when one character is left. The one it kills is
        // slot pv04F2, the opener's slot from before the prune. mdrcharSetDead says nothing - the
        // port had "The trap kills you." and "You have died." here, which were its own. And it is
        // an Exit Sub before monGetGold and before Fight is emptied, so the chest stays where it
        // was, untrapped now, with its gold still in it for somebody else to open.
        if (_party.Members[0].CurHits < 1)
        {
            // mdrcharSetDead pv04F2 (MONSMOD.BAS:3654) and nothing after it. When the opener was
            // not in slot 0 and the prune has left one character, that slot is gone: the routine's
            // bare Resume (MAINMOD2.BAS:4586-4587) retries the failing line for ever and the game
            // hangs. A hang cannot be ported, so nobody is set dead here - the deviation. On the 'o'
            // key the caller's fn1521 ending (MAINMOD2.BAS:4089-4091) then finds the survivor below
            // 1, counts a death and runs sub0930 on them, and sub0930 (3042) sets them dead, which
            // counts the second (4578): two, as the opener in slot 0 gets from the set-dead above and
            // that same ending - the original's own double count, not the port's. The count-down
            // open (MULTIMOD.BAS:527) has no such ending: it leaves them at nought for the
            // four-second tick's death check, which counts one, as the set-dead alone does there.
            if (openerSlot == 0 && !_party.Members[0].IsDead)
            {
                _party.Members[0].CauseOfDeath = Rules.DeathOrdinary;   // mc022C = 1
                Kill(_party.Members[0], "", sub0930: false);
            }
            _trapCaption = "";
            _trapReadoutTold = "";
            ShowChest();
            RefreshCombat();
            RefreshParty();
            SaveChar();
            return;
        }

        var chest = _chest;
        // "l0522 = monGetGold()" (MONSMOD.BAS:3660): its share line - "Your share is N Gold!" - is
        // lblCharacterHit's for half a second (2158, 2174-2176), not a sub085B line
        chest.PayGold();
        GoldPaid();
        string? goldLine = chest.GoldLine;   // shown once picTrap is down, below
        // The treasure loop (MONSMOD.BAS:3661-3866) comes after both waits - picTrap's half second
        // (3650) and monGetGold's (2175) - so its lines and its items come a second after the
        // opening. What it reads of the Fight record is taken now, before the record is emptied:
        // Monsters(Fight.M4F24), Fight.TreasureClass, Fight.ChestLock, and the leader's level.
        Action? treasure = null;
        if (chest.ChestLead is Monster owner)
        {
            int chestClass = chest.ChestClass, chestLocked = chest.ChestLocked, level = _level;
            // and for sub0730 (MONSMOD.BAS:3840-3848): Fight.M4F24 against Foes(0).KnownAs as the
            // room's records stand, and Chars(0)'s level and lost flags, as the chest is opened
            var src = chest.Source;
            int leadIndex = src?.Lead ?? _monsters.IndexOf(owner);
            int knownAs0 = src != null && src.Room.Area > 0 ? _dstate.LoadRoom(src.Room.Level, src.Room.Area).Foes[0].KnownAs : -1;   // no records: not known, so "unknown monster"
            var finding = (leadIndex, knownAs0, Leader.LostLevel, Leader.LostPosition, level);
            treasure = () =>
            {
                Out(TreasureFrom(owner, chestClass, chestLocked, level, finding));
                RefreshParty();
                RefreshPack();
                SaveChar();
            };
        }
        chest.ChestEmptied();
        StoreRoom(chest);            // Fight = l05AC, then fn0F88 (MONSMOD.BAS:3868-3869)
        _chest = null;
        _chestSeen = false;
        // keys wait, as sub0456's queue holds them through sub07E2 (see HoldKeys), until the treasure
        // has been gone through (the queue is only looked at after monOpenChest returns); the treasure
        // goes at the end of the waits, or at once if the dungeon is left first
        _pendingTreasure = treasure;

        // MONSMOD.BAS:3640-3651: the caption goes into lblTrap, lblDisarm is cleared, and then
        // sub07E2 .5# - half a second of doing nothing - before picTrap is taken down. Without the
        // wait the answer would be on screen for no time at all, which is the point of the call.
        // "Going through Treasure..." (3677) is written to lblCharacterHit for each item and the
        // caption is cleared as the routine ends (3870), with no wait between - so it is never
        // seen, and what is left is an empty caption. The port had put it on the message line.
        ChestText.Text = _trapCaption;
        _trapReadoutTold = _trapCaption;
        Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.Trap, _trapCaption, null);
        ChestGauge.Text = "";
        ChestIcon.Source = null;
        CombatHit.Text = "";
        // With nothing standing, picMonsters is blank from then on: sub0C53 had already painted over
        // the portraits and the state picture and cleared the captions (MONSMOD.BAS:762-781), and
        // monShowChest now paints over the chest (4114-4117). An empty picMonsters is the panel
        // hidden in this port, as for an area with nothing in it (LoadArea); it used to stay up,
        // empty, until the next change of area.
        RefreshCombat();
        RefreshParty();
        SaveChar();
        HoldUntil(AfterChestOpened(goldLine, treasure));
    }

    /// <summary>
    /// A chest's gold line and treasure still to come, <see cref="AfterChestOpened"/>'s: gone
    /// through at the end of its wait, or by <see cref="FinishWonFight"/> if the dungeon is left
    /// before that, so that leaving cannot lose it.
    /// </summary>
    private Action? _pendingTreasure;

    /// <summary>
    /// The rest of <c>monOpenChest</c> after the trap: picTrap's half second (MONSMOD.BAS:3650-3651),
    /// then monGetGold's caption and its half second (3660, 2158-2176), then the treasure loop
    /// (3661-3866).
    /// </summary>
    private async System.Threading.Tasks.Task AfterChestOpened(string? goldLine, Action? treasure)
    {
        await System.Threading.Tasks.Task.Delay(500);
        if (_ended) return;
        ChestPanel.IsVisible = false;
        bool standing = (_combat is { IsOver: false } && _combat.Monsters.Any(m => m.Alive))
                     || (_waiting?.Any(m => m.Alive) ?? false);
        if (!standing && _chest == null && !_awaitingRescue && !_ch.IsDead && !WaitBtn.IsVisible) HideMonsters();
        // then monGetGold (MONSMOD.BAS:3660), after picTrap's half second: its caption has a half
        // second of its own, whether or not it says anything (sub07E2 .5 at 2175 is unconditional)
        await ShowGoldLine(goldLine);
        RunPendingTreasure(treasure);
    }

    /// <summary>The treasure loop, once - unless something has already run it.</summary>
    private void RunPendingTreasure(Action? which)
    {
        if (which == null || !ReferenceEquals(_pendingTreasure, which)) return;
        _pendingTreasure = null;
        which();
    }


    /// <summary>
    /// What the chest actually gives up, <c>monOpenChest</c>'s Do loop (MONSMOD.BAS:3669-3861).
    /// It does not hand over one item and stop: having given you something it comes back round at
    /// <c>Int(ItemChance / 10)</c> and may give you another, and another. Each one goes to a party
    /// member picked at random, retried up to ten times to find one with a free slot, and an item
    /// cursed with a 2 puts itself on as it arrives (<see cref="Loot.AttachesItself"/>). Each find
    /// is filed in the Library by sub0730 (<see cref="Librarian.FileFind"/>), under the lead or an
    /// unknown monster as <paramref name="finding"/> says.
    /// </summary>
    private IEnumerable<string> TreasureFrom(Monster lead, int chestClass, int chestLocked, int level,
                                             (int LeadIndex, int KnownAs0, bool LostLevel, bool LostPosition, int Level) finding)
    {
        if (chestClass < 1 || chestClass > 3) yield break;   // gv0878$(1..3), the container's name
        bool first = true;
        for (int guard = 0; guard < 40; guard++)
        {
            bool another = first
                ? Loot.DropsItem(lead, chestClass, _rng, chestLocked, level)
                : Loot.DropsAnother(lead, _rng);
            first = false;
            if (!another) yield break;

            if (Loot.RollDrop(_items, lead, chestClass, _rng) is not Item drop) yield break;

            // MONSMOD.BAS:3778-3797 - a random member, retried up to ten times for a free slot
            Character? taker = null;
            for (int tries = 0; tries < 10; tries++)
            {
                // CInt(Rnd * UBound(Chars)) (MONSMOD.BAS:3788): rounded half to even, so the first and last
                // members are half as likely as those between
                var who = _party.Members[(int)Math.Round(_rng.NextDouble() * (_party.Size - 1), MidpointRounding.ToEven)];
                if (who.Inventory.Count < 39) { taker = who; break; }
            }
            if (taker == null) { yield return "An item was found, but nobody had room to carry it."; yield break; }

            var slot = Loot.NewSlot(drop, _rng);
            slot.Charges = Loot.RollCharges(drop, _rng);
            // fn12F0 (MONSMOD.BAS:3828): skill and difficulty both rounded into Integers before the test
            slot.Known = Loot.ReadableLevel(taker, drop, Lookup);
            // MONSMOD.BAS:3830 - the slot takes the item's curse the moment it lands in the pack,
            // before anything tries to put it on. This is the one route by which a slot can be
            // flagged cursed without being worn, and the Seer can tell the difference.
            slot.SlotCursed = drop.Cursed != 0;
            taker.PutInPack(slot);
            Audio.Play("FOUND");
            // MONSMOD.BAS:3832-3838: the container's name (gv0878$, MODULE1.BAS:425-427), the slot as
            // the pack numbers it, and the item as the taker reads it. The quoted name is for a party,
            // whoever took it
            string box = new[] { "Container", "Box", "Chest" }[chestClass - 1].ToUpperInvariant();
            string got = $"{box}: Slot #{taker.SlotOf(slot) + 1}, "
                       + Loot.NameFor(drop, slot.Known, slot.Alignment, slot.Charges);
            yield return _party.Size == 1 ? got : $"\"{taker.Name.TrimEnd()}\" - {got}";

            // sub0730 l0526, l04FA, Pack(l051A).IdLevel, l04FC or -2, True (MONSMOD.BAS:3840-3848):
            // the find goes in the Library under the taker, at the level the taker reads it. A
            // Library that cannot be filed to is logged and passed over: it must never cost the
            // party the rest of the chest
            try
            {
                var store = Librarian.Store.Load(SavesDir, _monsters);
                Librarian.FileFind(store, taker, drop.Id, slot.Known,
                                   Librarian.ChestFindFrom(lead, finding.LeadIndex, finding.KnownAs0, taker),
                                   Librarian.Serial(DateTime.Now), finding.LostLevel, finding.LostPosition, finding.Level);
                store.Save(SavesDir);
            }
            catch (Exception ex) { CrashLog.Note("the Library could not file a chest's find: " + ex.Message); }

            // mdrcharShowItem l0526, m01BC, l051A (MONSMOD.BAS:3849): the slot is painted into the
            // pack list as it lands, whoever got it - the list shows the character in command. Alone,
            // the buffers are repainted too (mdcharShowBuffer, 3834-3836)
            RefreshPack();
            if (_party.Size == 1) RefreshBuffers();
            // MONSMOD.BAS:3850 - an item whose Cursed is 2 (gc1DD0) does not wait to be put on; one
            // whose Cursed is 1 stays in the pack, flagged cursed all the same (3830). It attaches
            // itself through sub0F74 (3851), the same routine the Equip button uses, so it is subject
            // to the same refusals: a thing you are too weak for, or of the wrong guild, or not
            // wearable at all, stays in the pack. It stays flagged cursed either way, which is how a
            // chest can leave you carrying something the Store's uncurse will answer "Item isn't equipped!"
            // about (FRMCTYS.BAS:303).
            if (Loot.AttachesItself(drop))
            {
                // sub0F74 says its own refusal (MAINMOD2.BAS:1786, 1816, 1818) and leaves; a thing
                // that is not wearable at all it leaves without a word (1729-1731)
                if (Character.IsWearable(drop))
                {
                    if (taker.WearRefusal(slot, drop, Lookup) is string why) yield return why;
                    else
                    {
                        slot.Equipped = true;
                        taker.ApplyEquipCurse(slot, drop);
                        Effects.Recompute(taker, Lookup);
                        RefreshPack();   // sub0F74 ends with mdrcharShowItem on the slot (MAINMOD2.BAS:1864)
                    }
                }
                // and whatever sub0F74 did, the line and both sounds follow it (MONSMOD.BAS:3852-3858):
                // sub086D 61 and sub09DE 63, which SoundFiles names EXTING.WAV and YEECH.WAV (MODULE1.BAS:369, 371). The port
                // had played BLACKOUT, which is SoundFiles(7) (315), and only when the item went on
                Audio.PlayOnNextChannel("EXTING");
                Audio.Play("YEECH");
                yield return taker == _ch
                    ? "An item you found has attached itself to you!!"
                    : $"A found item has attached itself to \"{taker.Name.TrimEnd()}\"!!";
            }
        }
    }


    /// <summary>
    /// Opening the chest an encounter left behind, from <c>monOpenChest</c> in MONSMOD.BAS. One
    /// roll against the party's best thieving ability either disarms it or sets it off; what each
    /// trap then does is <see cref="ChestTraps"/>. Returns the lines to show.
    /// </summary>
    private List<string> OpenChest(CombatEngine fight, Character opener)
    {
        var lines = new List<string>();
        if (fight.ChestClass <= 0) return lines;

        // l0510, the caption monOpenChest leaves in lblTrap (MONSMOD.BAS:3503, 3507, 3510). It is
        // one of exactly three things and each of them stands alone: "Not Trapped", "Trap
        // Disarmed", or the trap's own name out of gv08AA$. The port wrapped all three in a
        // sentence of its own - "A chest lies among the remains. Not Trapped." - and put it in the
        // combat log, where the original puts nothing at all.
        if (fight.ChestTrap == 0)
        {
            _trapCaption = "Not Trapped";
            return lines;
        }

        double thieving = Rules.BestAbilityChance(opener, 0, _level, fight.ChestGuard, Lookup);   // Stats + StatMods (MAINMOD2.BAS:1945-1946)
        string what = ChestTraps.Names[fight.ChestTrap];
        if (_rng.Next(100) + 1 <= thieving)
        {
            _trapCaption = "Trap Disarmed";
            // MONSMOD.BAS:3508-3509 pays for the disarm, and only for a disarm - an untrapped chest
            // earns nothing because the award sits inside the ChestTrap > 0 branch. The guild level
            // that discounts it is Chars(0)'s, not the opener's; see Rules.ChestExp.
            long xp = Rules.ChestExp(thieving, fight.ChestGuard,
                                     _party.Members[0].Guild.Level, _party.Size);
            // for the mods (ExperienceGained), on a chest seen before its fight was wired too
            fight.ExperienceGained ??= x => Mordor.Modding.ModHost.Active.ExperienceGained(x);
            if (xp > 0) fight.GiveExperience(opener, xp, ExperienceSource.Chest);
            foreach (var said in fight.TakeSaid()) Log(said.Text);   // pinned / enough to make a level
            return lines;
        }

        _trapCaption = what;
        var result = ChestTraps.Spring(
            // MONSMOD.BAS:3514, 3521, 3528 and 3534 each loop the whole party for the gas, the
            // spores, the explosion and the fire - only the opener's own bad luck is personal
            fight.ChestTrap, _party.Members, opener, _level, _rng,
            id => _itemsById.TryGetValue(id, out var it) ? it : null,
            (x, y) => _nav.InBounds(x - 1, y - 1) ? (int)_nav.Floor.At(x - 1, y - 1).Flags : 1 << 19);
        lines.AddRange(result.Lines);
        fight.TrapSprung();
        StoreRoom(fight);            // Fight.ChestTrap = False, then fn0F88 (MONSMOD.BAS:3514-3515)

        _chestTeleport = result.TeleportTo;   // OpenChestBy carries it out and leaves (MONSMOD.BAS:3602)
        return lines;
    }

    /// <summary>Where the Teleporter trap just sent the opener, for <see cref="OpenChestBy"/>.</summary>
    private Teleported? _chestTeleport;

    /// <summary>
    /// The Teleporter trap's case of monOpenChest (MONSMOD.BAS:3559-3602), after the record has been
    /// written with the trap gone (3567-3568). Only the opener moves (3572-3577), and is lost if
    /// the square was never mapped (3579-3583, the "you realize" line only when alone). In a party the opener
    /// leaves it: a leader first changes places with Chars(1) (3585-3589) and sub0908 (3590) drops whoever
    /// is off the leader's square (MULTIMOD.BAS:52-71), so the rest stay in the area with the chest
    /// still standing there, untrapped and full. Alone, mdrmazShowMaze (MONSMOD.BAS:3591) runs at once and the
    /// new area is settled now. Then the case leaves by Exit Sub (3602): no gold, no treasure, no
    /// UPDATE of the chest's own (3652; sub0908's, in a party, is the only one), and lblTrap keeps the trap name (3564).
    /// </summary>
    private void ChestTeleport(Character opener, Teleported to)
    {
        int x = to.X - 1, y = to.Y - 1;
        bool alone = _party.Size == 1;
        // gv1D6A = Chars(m01BC).M59BB (MONSMOD.BAS:3569). Alone, mdrmazShowMaze follows at once (3591)
        // and takes it; in a party nothing here runs mdrmazShowMaze, and the original carries gv1D6A
        // to whatever next does - a step, a turn, a repaint of the maze. The port does not carry it
        // that far: it is set for the lone opener only, so it cannot fire late on a later step.
        _forceSettle = alone;
        ChestText.Text = _trapCaption;
        _trapReadoutTold = _trapCaption;
        Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.Trap, _trapCaption, null);
        ChestGauge.Text = "";
        if (Unmapped(_level, x, y))
        {
            opener.LostLevel = opener.LostPosition = opener.LostFacing = true;
            if (alone && opener == _ch) Log(Lost.Realisation(opener));   // sub08E1 (MAZEMOD.BAS:54-63)
        }

        if (!alone)
        {
            opener.X = x; opener.Y = y; opener.DungeonLevel = _level;
            opener.Area = _nav.Floor.AreaIdAt(x, y);   // M59BB of the new square (MONSMOD.BAS:3577)
            Log(Chute.Disappeared(opener));
            if (opener.CurHits <= 0) { opener.CurHits = 0; if (!opener.IsDead) SetDead(opener); }
            bool wasShown = opener == _ch;
            _captionNamed = false;                    // sub0908's closing sub1536 gv1A84 (MULTIMOD.BAS:93-95, 686-690)
            _party.Remove(opener);                    // the leader's swap and unload end the same way
            if (wasShown) _party.Select(0);           // sub0908 hands the screen to the leader (MULTIMOD.BAS:78-84)
            _shownSlot = _party.SelectedIndex;   // M5E4F: sub1536 gv1A84 closes sub0908 (MULTIMOD.BAS:95)
            opener.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(opener.Name) + ".json"));
            // sub0908 (MONSMOD.BAS:3590) ends, with the opener gone, in frmParty's UPDATE (MULTIMOD.BAS:88-95):
            // anyone the trap left at nought with the leader goes too (FRMGROUP.BAS:321-346)
            PruneTheDead();
            ApplyLost();
            // gv1EB2 = False : monShowChest Chars(m01BC).M59BB (MONSMOD.BAS:3592-3593) - the chest
            // the party is still standing beside
            ShowChest();
            RefreshCombat(); RefreshParty(); RefreshSheetSpells();
            SaveChar();
            Redraw();
            return;
        }

        // alone: the character goes, and mdrmazShowMaze (MONSMOD.BAS:3591) writes the old room
        // back and reads the new one (MAZEMOD.BAS:798-826, 1467-1471) before anything else happens.
        // The chest record went to the file at 3568, trap gone and gold still in it; the Fight
        // record the new room brings replaces it here, as sub0A91 replaces it there.
        _nav.SetPosition(x, y);
        ApplyLost();
        if (_combat == _chest && _combat is { IsOver: true }) _combat = null;
        SavePos();
        Redraw();
        SettleArea();
        RefreshCombat(); RefreshParty();
        SaveChar();
    }

    /// <summary>
    /// sub0C53 (MONSMOD.BAS:758-792) when the last group has gone off with you rather than died
    /// (MAINMOD2.BAS:4110-4113): the room is stamped and the level's tally loses one, exactly as
    /// for a won fight, and the gold is paid now if there is no chest - otherwise the chest is
    /// put up and holds it. The port used to hide the monsters and nothing else, so the room's
    /// seed brought the same group, and the same free companions, straight back.
    /// </summary>
    /// <summary>
    /// fn0F88 (MONSMOD.BAS:3908-3936): the loaded room's records written back as they now stand -
    /// the groups from the fight made of them, or from the group still waiting, and the chest from
    /// whichever fight record holds it. The original writes at MAZEMOD.BAS:811 (leaving the room),
    /// MAINMOD2.BAS:4560 (a death), and after a trap, a chest and monGetGold.
    /// </summary>
    private void StoreRoom(CombatEngine? chest = null)
    {
        // whose records: the fight's own, when a fight record is named, else the loaded room's
        var records = chest?.Source ?? _room?.Records;
        if (records == null || records.Room.Area <= 0) return;
        var fight = chest?.Source == records ? chest
                  : _combat?.Source == records ? _combat
                  : _chest?.Source == records ? _chest : null;
        // the members are the room's own list: a fight holds the same creatures, but a join
        // takes them off this list only
        var (level, area) = records.Room;
        _dstate.StoreRoom(level, area, EncounterGenerator.Capture(_dstate.LoadRoom(level, area), records, fight, _monsters));
        // fn0F88 is a Put to the level's file, so the file is written each time
        _dstate.Save(SavesDir);
    }

    /// <summary>
    /// Walking out of an area, mdrmazShowMaze (MAZEMOD.BAS:811-820): the room's records are written
    /// back first, and then, if its group is engaged - <c>Foes(0).Engaged = 1</c> - the combat
    /// timer is stopped and it gets <c>sub0A64 2</c>, a round of its own, before you are gone.
    /// A group standing hostile is engaged in the original's sense even before the port's
    /// timer has turned it into a fight. True if the party did not survive it.
    /// </summary>
    private bool LeaveArea()
    {
        // PlayRound draws a round a blow at a time, so a step can come after the last monster has
        // fallen and before the round has been drawn to its end. sub0C53 - the room stamped, the
        // gold or the chest put up - belongs to that round and to this area (MONSMOD.BAS:758-792),
        // so it is finished here, before the room is written back and left.
        FinishWonFight();
        StoreRoom();
        if (_combat != null && _combat.Source != _room?.Records) StoreRoom(_combat);
        // The chest is the Fight record's, and the Fight record is the area's: leaving writes it
        // back to the room it came from (fn0F88, MAZEMOD.BAS:811) and sub0A91 reads the next
        // area's in its place (fn0E38, MONSMOD.BAS:687). An unopened chest stays behind with its
        // gold, to be found again there - HideMonsters below lets go of it.
        if (_chest != null && _chest != _combat && _chest.Source != _room?.Records) StoreRoom(_chest);
        // a won fight has been written with its room and stays there with it
        if (_combat is { Won: true }) _combat = null;
        if (_combat == null && _waiting is { Count: > 0 } w && w[0].Hostile && w.Any(m => m.Alive))
            StartCombat(w, _waitingIsLair, monstersMoveNow: false);
        _waiting = null;
        if (_combat != null && !_combat.IsOver)
        {
            // AttackKind 2 is the argument mc021A passes; the enum names it for its other caller
            MonstersRound((AttackKind)2);
            RefreshCombat(); RefreshParty();
            if (_combat.IsOver) { bool wiped = _combat.PartyWiped; EndCombat(); return wiped; }
            LeaveFight();
            return false;
        }
        _encounterAt = (-1, -1, -1);
        HideMonsters();
        return false;
    }

    /// <summary>
    /// Walking into an area whose group is engaged, MAZEMOD.BAS:1468-1471: straight after
    /// sub0A91 reads the records, <c>sub0A64 1</c> - the group gets a round as you arrive.
    /// </summary>
    private void ArrivalRound(List<CombatMonster> group)
    {
        if (group.Count == 0 || !group[0].Hostile || !group.Any(m => m.Alive)) return;
        _waiting = null;
        StartCombat(group, _waitingIsLair, monstersMoveNow: false);
        if (_combat == null || _combat.IsOver) return;
        MonstersRound(AttackKind.Moved);
        RefreshCombat(); RefreshParty();
        if (_combat.IsOver) EndCombat();
    }

    /// <summary>A level's header area count (M5CF4), for the tally.</summary>
    private int HeaderAreasOf(int level) => level >= 1 && level <= _dungeon.Count ? _dungeon[level - 1].HeaderAreaCount : 0;

    private void ClearedByJoining(List<CombatMonster> emptied)
    {
        // the question sub0C53 asks first (MONSMOD.BAS:769-774): was Foes(0) Asmodeus?
        if (emptied is Encounter en && en.Foes[0].Ref >= 0 && en.Foes[0].Ref < _monsters.Count
            && Asmodeus.Is(_monsters[en.Foes[0].Ref])) BeatAsmodeus();
        var (roomLevel, roomArea) = emptied is Encounter joinedRoom ? joinedRoom.Room : (-1, -1);
        var fight = EncounterGenerator.ClearedByJoining(_party, emptied, _dstate, roomLevel, roomArea,
                                                        HeaderAreasOf(roomLevel), DateTime.Now, Lookup, _rng, _monsters);
        foreach (var line in fight.Log) Log(line, MessageKind.Join);
        _combat = fight;
        ShowChest();                                         // monShowChest otherwise
        BlankPicMonsters();
        // monGetGold's caption (784), not a Messages line - when there was no chest (783) - and
        // its half second, through which the keys wait
        if (fight.ChestClass == 0) HoldKeys(0.5);
        _ = ShowGoldLine(fight.GoldLine);
        _combat = null;
        _dstate.Save(SavesDir);
    }

    private void EndCombat()
    {
        if (_combat == null || _combatEnded == _combat) return;   // a flee mid-round must not end it twice
        _combatEnded = _combat;
        _finished = _combat;
        // the area the fight belongs to; LeaveArea finishes a fight before it leaves, so this is
        // only a guard against drawing its chest anywhere else
        bool here = FightIsHere(_combat);
        // FRMGROUP.BAS:419 clears every order once no monsters are left
        _party.ClearOrders();
        _encounterAt = (-1, -1, -1);
        if (_combat.Won)
        {
            // sub0C53 asks one question before anything else (MONSMOD.BAS:769-774): was the *lead*
            // group Asmodeus?
            // fn0C3B(1, Foes(0).Ref): slot 0's monster, as the join path asks it
            var slot0 = _combat.Source is Encounter src && src.Foes[0].Ref >= 0 && src.Foes[0].Ref < _monsters.Count
                ? _monsters[src.Foes[0].Ref] : _combat.Monsters.Count > 0 ? _combat.Monsters[0].Def : null;
            if (slot0 != null && Asmodeus.Is(slot0)) BeatAsmodeus();
            // then the room is stamped emptied and written, and the tally loses one (775-779) -
            // VB Timer is the local clock
            // the fight's own room, which need not be where you now stand
            StoreRoom(_combat);
            if (_combat.Source is Encounter won && won.Room.Area > 0)
                _dstate.MarkCleared(won.Room.Level, won.Room.Area, DateTime.Now, HeaderAreasOf(won.Room.Level));
            _combat.AwardSpoils();   // the gold split across the party; experience was paid blow by blow
            GoldPaid();
            // the share line is lblCharacterHit's, after the portraits are painted out (sub0C53
            // calls monShowMonsters -1 at 780 and monGetGold at 784) - shown below
            StoreRoom(_combat);      // monGetGold writes the emptied gold back (2178)
            foreach (var m in _combat.Monsters.Where(m => m.Hits <= 0 && !m.Alive))
                foreach (var c in _combat.Party.Members) c.NoteMonster(m.Def.Id, killed: true);
            // quests were settled at each kill, by sub0CF3 - see CombatEngine.QuestKill
            // MODULE4.BAS:471-479 offers a kill to the Hall from inside the killer's own swing, and
            // only when the group was known for what it was - the engine collected exactly those
            var hall = HallRecords.Load(SavesDir);
            int PowerOf(int id) => _monsters.FirstOrDefault(x => x.Id == id) is Monster d ? HallRecords.MonsterPower(d) : 0;
            foreach (var (killer, def) in _combat.RecordableKills)
                hall.Check(HallRecords.Kind.DeadliestCreature, killer, def.Id, PowerOf);
            hall.Save(SavesDir);
            // No "Victory!" line: the fight ending is not announced. The gold share is lblCharacterHit's
            // for half a second, and the experience is said only when it takes somebody past a
            // level (MONSMOD.BAS:305-307).
            // each destroyed item already had its own line as it went (MONSMOD.BAS:3288-3315); the
            // end-of-fight "Lost in the fight: ..." summary was this port's
            // MONSMOD.BAS:4124 puts the chest on screen and waits: the help says "To open a chest
            // during peacetime or combat, either press 'o' or click on the chest."
            if (here)
            {
                ShowChest();
                BlankPicMonsters();
            }
            // monGetGold (784), when there was no chest to hold the gold (783): its caption, and
            // its half second, through which the keys wait - a step taken in it comes after it,
            // so leaving the area cannot wipe the caption the moment it is up
            if (_combat.ChestClass == 0) HoldKeys(0.5);
            _ = ShowGoldLine(_combat.GoldLine);
        }
        else if (_combat.PartyWiped)
        {
            StoreRoom(_combat);      // mdrcharSetDead writes the room back (MAINMOD2.BAS:4560)
            foreach (var c in _combat.Party.Members.Where(c => c.CurHits <= 0 || c.Stoned)) c.IsDead = true;
            _combat.Log.Add(_ch.CauseOfDeath == Rules.DeathRock ? Rescue.DeadInRock : Rescue.DeadInTheDungeon);
        }
        // sub091E runs once per death, wherever it happened; anyone who fell in this fight and
        // has not already been counted by Kill() is counted here, won or lost
        foreach (var fallen in _combat.NewlyDead.Where(c => _countedDead.Add(c)).ToList()) { fallen.Deaths++; LogDeath(fallen); }
        // A fight you walked away from needs no acknowledgement. The original turns the combat
        // timer off and hides picMonsters (MAINMOD2.BAS:3035-3052) and you carry straight on
        // walking; the Continue button this port used to put here was invented, and pressing it
        // on a lost fight was what carried you out of the dungeon unasked.
        bool wiped = _combat.PartyWiped;
        WaitBtn.IsVisible = wiped;
        if (wiped)
        {
            // MAINMOD2.BAS:3040-3048 - the caption says what the button really does. In the rock
            // there is no button at all, which OfferRescue handles.
            WaitBtn.Content = _level > 0 ? "_Wait for Rescue" : "_Visit the Morgue";
            RefreshCombat();
            RefreshParty();
            SaveChar();
            // alone, it is sub0930's dead screen (MAINMOD2.BAS:3039-3057): the port had left the
            // fight panel, the chest and its trap panel up, with the button laid over them.
            // All of sub0930, not its dead screen alone: sub0A64 calls it for the character at nought
            // (MULTIMOD.BAS:362-363), so the companions go (MAINMOD2.BAS:3026-3030) and sub048C runs
            // (3033) - which a companion's mishap, mdrcharSetDead alone in monCompanionAttack, had
            // not done yet, and which a monster's blow had (Dies) and does again harmlessly.
            // A party that went down with more than one body still on the list gets the list's
            // UPDATE first, as each of those deaths sent it in the original (sub0930's,
            // MAINMOD2.BAS:3035-3037, or the round's, MULTIMOD.BAS:542-553): everyone at nought is
            // unloaded but the last (FRMGROUP.BAS:321-346), and that one gets sub0930 and the dead
            // screen, as the next blow at them does in the original (MULTIMOD.BAS:362-363). The
            // deaths were counted just above, before the prune takes them off the list.
            if (_party.Size > 1) PruneTheDead();
            if (_party.Size == 1) Sub0930(_ch);
            return;
        }

        // MAINMOD2.BAS:3035 turns the combat timer off and hides picMonsters; there is nothing to
        // press. Everything the fight said is already in the Messages panel - each sub085B line went
        // there as it was said - and the panel itself stays up, with the chest if one was left, until
        // the next step.
        // "The fight is over." was the port's; sub0C53 announces nothing, and it does not touch
        // lblSquareInfo either - the square's caption from before the fight is still there, now
        // that no fight line is echoed into it
        RefreshCombat();
        _combat = null;
        _combatEnded = null;
        // sub0C53 ends on sub0BF1 (MONSMOD.BAS:790), the party window's "STATS" (MULTIMOD.BAS:602,
        // FRMGROUP.BAS:147-148): modUpdateStats, which with no monster left in the area clears every
        // order (FRMGROUP.BAS:418-422) and repaints the Option column. The port had repainted the
        // window only during the round, so "Fight" or the spell stayed up after it.
        RefreshParty();
        SaveChar();
        Focus();
    }

    /// <summary>
    /// <c>mdrcharStats False, 10</c> after the gold is shared out (monGetGold, MONSMOD.BAS:2154):
    /// <c>sub166D Me, 10</c>, the toolbar's gold line (MAINMOD2.BAS:5578), and lblStat(6), the gold on
    /// the character window (MAINMOD2.BAS:5615-5616). The port had left both to the next keystroke.
    /// </summary>
    private void GoldPaid()
    {
        Tools.ShowGold(_ch);
        _charPanel.Show(_ch);
    }

    private int _goldLineShown;

    /// <summary>
    /// monGetGold's caption: lblCharacterHit made visible and given the share line (MONSMOD.BAS:2158, 2174),
    /// then sub07E2 .5 (2175) and hidden again (2176). It is not a sub085B line, so it never reaches
    /// the Messages list. lblCharacterHit sits in picMonsters, which the port hides when it is empty,
    /// so the panel is put up for the half second when it was down, and taken down after if
    /// nothing has come into it meanwhile. An empty caption (no gold found or recovered) is still
    /// written: "lblCharacterHit.Caption = l03A2" is outside the If (MONSMOD.BAS:2158-2174), so
    /// whatever the label held - the killing blow's "You Killed One!" - is wiped for the half second,
    /// which is waited all the same (2175), so what comes after monGetGold in the chest (the
    /// treasure) still comes after it. The port had left the last caption standing. An empty label
    /// shows nothing, so the panel is not put up for it. Null is monGetGold not run at all.
    /// </summary>
    private async System.Threading.Tasks.Task ShowGoldLine(string? line)
    {
        if (line == "") { _goldLineShown++; CombatHit.Text = ""; }
        if (string.IsNullOrEmpty(line)) { await System.Threading.Tasks.Task.Delay(500); return; }
        int mine = ++_goldLineShown;
        bool raised = !CombatPanel.IsVisible && !_combatClockOff;
        CombatHit.Text = line;
        Mordor.Modding.ModHost.Active.CaptionShown(Mordor.Modding.CaptionKind.GoldShare, line, null);
        if (raised) CombatPanel.IsVisible = true;
        await System.Threading.Tasks.Task.Delay(500);
        if (mine != _goldLineShown || _ended) return;
        if (CombatHit.Text == line) CombatHit.Text = "";
        bool standing = (_combat is { IsOver: false } && _combat.Monsters.Any(m => m.Alive))
                     || (_waiting?.Any(m => m.Alive) ?? false);
        if (raised && !standing && _chest == null && !WaitBtn.IsVisible) CombatPanel.IsVisible = false;
    }

    /// <summary>
    /// Whether a fight record is the loaded area's. The Foes and the Fight record - the chest with
    /// them - are read for the area you stand in (sub0A91's fn0E38, MONSMOD.BAS:687) and written
    /// back to it as you leave (fn0F88, MAZEMOD.BAS:811), so a chest can only be drawn or opened in
    /// its own area. A fight made by a test hook belongs to no room and to wherever it is.
    /// </summary>
    private bool FightIsHere(CombatEngine? fight) =>
        fight?.Source is not Encounter e || e.Room.Area <= 0 || e.Room == _loadedArea;

    /// <summary>The last fight <see cref="EndCombat"/> ran for, so that it is never run twice.</summary>
    private CombatEngine? _finished;

    /// <summary>
    /// <c>cmdWait_Click</c> (FRMDUNM.BAS:140). The original's dungeon screen has exactly one button
    /// in this place, it is shown only when you are dead (MAINMOD2.BAS:3046), and the first thing
    /// it does is check that you are: <c>If gv14C2(gv1A86).M5483 &gt; False</c>. Below ground it
    /// waits for somebody to find you; at level 0 it walks you into the Morgue.
    ///
    /// The port used to caption this "Continue" and show it after *every* fight, which the original
    /// never does - a fight you walk away from needs no acknowledgement. Worse, pressing it after a
    /// fight you lost carried you out of the dungeon without asking, which is what made it look
    /// like the game had closed on you.
    /// </summary>
    private void OnWait(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CrashLog.Context = "wait button";
        try
        {
            // cmdWait_Click:151 - it does nothing at all unless you are dead
            if (!_ch.IsDead && !_awaitingRescue) return;

            if (_awaitingRescue) { _awaitingRescue = false; _combat = null; HideMonsters(); WaitForRescue(); return; }
            _combat = null;
            HideMonsters();
            SaveChar();
            WaitForRescue();
        }
        catch (Exception ex)
        {
            // Logged, not said: cmdWait_Click has no error handler and no words of its own for one
            // (FRMDUNM.BAS:144-196). The sentence the port put on the screen here had no source.
            CrashLog.Write("wait button", ex);
        }
    }

    /// <summary>
    /// One line of the monster list, as MODULE4.BAS:4395 builds it: a number, then - only for a
    /// monster that comes in groups of two to twelve - how many are still standing, then the name
    /// at whatever level you have identified it to. Never hit points: you cannot see how hurt
    /// anything is, which is most of what makes a fight in this game frightening.
    /// </summary>
    private string MonsterRow(int n, Monster def, int alive, bool identified)
    {
        string name = EncounterGenerator.NameFor(def, _ch, alive, identified);
        return def.InGroup > 1 && def.InGroup <= 12 ? $"{n}. {alive} {name}" : $"{n}. {name}";
    }

    /// <summary>
    /// "Monster Label - (Same as '1-4')", the help's Click Commands. Clicking a group picks it the
    /// way the number key does.
    ///
    /// <paramref name="glyph"/> is lblCurrentGroup, which monShowCursor sets to the order's mark and
    /// moves to "lblMonster(n).Left - lblCurrentGroup.Width" - just left of the group being aimed
    /// at (MONSMOD.BAS:4224-4226). Every row keeps that column, so the names do not shift as the
    /// mark moves.
    /// </summary>
    private Control MonsterLabel(int group, string text, string glyph = "")
    {
        var t = new TextBlock
        {
            Text = text,
            Foreground = Brushes.Khaki,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        t.PointerPressed += (_, e) => { TargetGroup(group - 1); Focus(); e.Handled = true; };
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        // lblCurrentGroup is 180 twips wide - 12 pixels - centred (Alignment 2 in frmDungeonMain's
        // form data, MORDOR.EXE RCDATA 22). Byte 149 is not a character in the Windows 3.1 ANSI set
        // the game was built on, and MS Sans Serif draws its block for it: game_files/dmainw1.png
        // shows a narrow upright bar beside "1. 4 Blue Pelagons", not a bullet. The port had drawn
        // a bullet, which read as list styling.
        if (glyph == NoOrderGlyph)
            row.Children.Add(new Border
            {
                Width = 12, Child = new Border
                {
                    Width = 2, Height = 9, Background = Brushes.Khaki,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                },
            });
        else
            row.Children.Add(new TextBlock { Text = glyph, Width = 12, Foreground = Brushes.Khaki, FontWeight = FontWeight.Bold,
                                             TextAlignment = TextAlignment.Center });
        row.Children.Add(t);
        return row;
    }

    /// <summary>lblCurrentGroup's caption for no order, Chr$(149) (MONSMOD.BAS:4217).</summary>
    private const string NoOrderGlyph = "•";

    /// <summary>
    /// monShowCursor's caption for lblCurrentGroup (MONSMOD.BAS:4210-4218): "+" for Fight, "[" for
    /// Defend, "&gt;" when a spell is the order, and otherwise byte 149 - the Windows-1252 bullet,
    /// which the decompiler's text shows as a replacement character - for Nothing.
    /// </summary>
    private static string OrderGlyph(CombatOrder o) => o.Kind switch
    {
        OrderKind.Fight => "+",
        OrderKind.Defend => "[",
        _ when o.SpellId > 0 => ">",
        _ => NoOrderGlyph,
    };

    private void RefreshCombat()
    {
        if (_combat == null) return;
        // MODULE4.BAS:4404-4412 - four fixed slots, each holding one group. A group with nothing
        // left in it is simply hidden, and the slots that remain keep their own numbers.
        var groups = new List<List<CombatMonster>>();
        foreach (var m in _combat.Monsters)
        {
            if (groups.Count > 0 && CombatMonster.SameGroup(groups[^1][0], m)) groups[^1].Add(m);
            else groups.Add(new List<CombatMonster> { m });
        }

        // lblCurrentGroup: the order's mark beside the group being aimed at. A target whose group
        // is gone goes back to the first one standing - "M547A = fn0DCD(-1)" (MONSMOD.BAS:4203,
        // 4207), and the target is written back as that group. Line 4205 asks only whether the character
        // is in an area (M59BB > -1), which below ground they always are.
        var order = _party.OrderFor(_ch);
        int aimed = order.Target >= 0 && order.Target < _combat.Monsters.Count
            ? groups.FindIndex(g => g.Contains(_combat.Monsters[order.Target]))
            : -1;
        if (aimed < 0 || !groups[aimed].Any(m => m.Alive))
        {
            aimed = groups.FindIndex(g => g.Any(m => m.Alive));
            if (aimed >= 0) order.Target = _combat.Monsters.IndexOf(groups[aimed].First(m => m.Alive));
        }
        var rows = groups
            .Select((g, n) => (g, n))
            .Where(t => t.g.Any(m => m.Alive))
            .Select(t => MonsterLabel(t.n + 1, MonsterRow(t.n + 1, t.g[0].Seen, t.g.Count(m => m.Alive), t.g[0].Identified),
                                      t.n == aimed ? OrderGlyph(order) : ""))
            .ToList();
        MonsterList.ItemsSource = rows;
        ShowPortraits(_combat.Monsters);   // monShowMonsters draws the strip with the rows
    }

    // ------------------------------------------------------------------ drawing
    /// <summary>Where the automap was last written for, so an arrival is only recorded once.</summary>
    private (int level, int x, int y)? _lastRecorded;

    /// <summary>
    /// Write down what can be seen from wherever the party is now - MAZEMOD.BAS:1579-1687. This is
    /// the only thing the automap is ever drawn from, and it is where a secret door gets its one
    /// chance of being noticed instead of being recorded as the plain wall it looks like.
    ///
    /// It hangs off the position rather than off the walking code because walking is not the only
    /// way onto a square: a chute, a teleporter, a flight of stairs and a Teleport spell all arrive
    /// somewhere too, and the original records on all of them.
    /// </summary>
    /// <summary>
    /// sub0835 run again on the square the party has just been put on - by a teleporter or a chute
    /// (l0286, MAZEMOD.BAS:1891-1893) or by TAKE (MAINMOD2.BAS:4004). The whole routine, in its
    /// order: the lost rolls and the found-yourself test (MAZEMOD.BAS:1541-1583), the automap
    /// write for the square and its neighbours (1584-1696), then the square's own effects, captions and perception.
    /// The write is made here and not left to the next Redraw, because the effects may carry the
    /// party on before it comes (a teleporter onto a teleporter), and the original writes each
    /// square it passes through.
    /// </summary>
    private string ArriveBySpell()
    {
        // "sub0835 ... : mdrmazShowMaze" (MAINMOD2.BAS:5899-5900, 1209-1210, 1348-1349): the square
        // routine, and then the area, whatever the routine did - AfterMove returns before settling
        // when the caster dies in a pit, and the old area has still to be written back. A chute
        // under the caster is the exception: the port runs its fall after this (ChuteFall), and
        // the fall settles the area it ends in.
        string label = RerunSquare();
        if (!_nav.Floor.Has(_nav.X, _nav.Y, Floor.CHUTE)) SettleArea();
        return label;
    }

    private string RerunSquare()
    {
        WorkOutWhereYouAre();
        RecordArrival();
        return AfterMove();
    }

    /// <summary>The party's MorgueList rows as sub0956 last wrote them; see <see cref="Redraw"/>.</summary>
    private readonly LookAround.ListedParty _listed = new();

    /// <summary>Where the party stands, to tell whether a spell or an item moved it (and so redrew the maze).</summary>
    private (int, int, int) Spot => (_level, _nav.X, _nav.Y);

    private void RecordArrival()
    {
        var here = (_level, _nav.X, _nav.Y);
        if (_lastRecorded == here) return;
        _lastRecorded = here;
        MapRecord.OnEntry(_map, _nav.Floor, _ch, _nav.X, _nav.Y, _rng, Lookup);
    }


    /// <param name="sub0956">
    /// Whether the original would have run <c>mdrmazShowMaze</c> here, and with it <c>sub0956</c>'s
    /// copy of the party into MorgueList (MAZEMOD.BAS:832): a step, a turn, a level or area
    /// change, a teleport, the party list's prune. False where it does not - the ailment clock, a
    /// spell or item that leaves the party where it stands, a switch of character, a give - so
    /// the Look page keeps the other members' hits as the last view redraw left them
    /// (<see cref="LookAround.ListedParty"/>).
    /// </param>
    private void Redraw(bool sub0956 = true)
    {
        RecordArrival();
        SyncAreas();   // M59BB follows the leader on every step (MAZEMOD.BAS:822-824)
        if (sub0956) _listed.Sync(_party.Members);
        // lblRoom, top left of the Dungeon window: "26,6,6 West [ 1 ]" (help screenshot
        // game_files/dmainw1.png) - x, y, level, facing and how many are in the area, the dead after
        // a plus (MAZEMOD.BAS:309-331). The port had read the number as the focused member's
        // place and put the name after it; see LookAround.Readout for the lost flags.
        var (_, living, dead) = WalkHere();
        PosText.Text = LookAround.Readout(_ch, _party.Members[0], _nav.X + 1, _nav.Y + 1, _level, _nav.Facing, living, dead);
        RetitleDungeon();
        RenderView();
        RefreshParty();
        PushToAutomap();
    }

    /// <summary>
    /// <c>frmAutoMap!lblCall = "DRAW"</c>, the line the original runs at the end of every step it
    /// records (MAZEMOD.BAS:1695) and after every teleport, chute and staircase. The map is an MDI
    /// child sitting beside the dungeon, not a dialog you close to get back, so it has to keep up:
    /// without this the arrow stayed where it was when you opened the map and nothing you walked
    /// appeared until you closed it and opened it again.
    /// </summary>
    private void PushToAutomap()
    {
        if (_automap == null || !_automapChild.IsVisible) return;
        // Not from the ailment clock. timAilment_Timer (FRMDUNM.BAS:395-614) never posts to
        // frmAutoMap - its only call out is sub0BF1, frmParty's "STATS" (MULTIMOD.BAS:601-603) -
        // but AilmentTick ends in Redraw, which came here every four seconds. The "UPDATE" this
        // stands for runs medDrawCursor, and with "Auto Change Lev." ticked that turns hsbLevel back
        // to the level you stand on (FRMAUTOM.BAS:306-313): a level paged to on the map jumped back
        // to your own within four seconds, whatever you were doing with the window - resizing it,
        // most noticeably. The map now follows you only on what posts to it in the original.
        if (_inAilment) return;
        _map.Merge(_level, _nav.Explored, _nav.Floor.Width);
        // the deepest level as the map was opened with it (OnOpenAutomap): the record may have moved
        // on since, and "UPDATE" reads it again (FRMAUTOM.BAS:543-544)
        // Chars(0)'s flags and its M5914, as FRMAUTOM reads them (FRMAUTOM.BAS:24, 307, 310)
        _automap.Walked((_level, _nav.X, _nav.Y, _nav.Facing), Leader.IsLost, Leader.LostLevel,
                        Math.Max(_dstate.DeepestReached, _map.DeepestVisited), Leader.Options.AutoChangeLevel);
    }

    private Rect Frame(int d)
    {
        double k = K[Math.Min(d, K.Length - 1)];
        return new Rect(W * k, H * k, W * (1 - 2 * k), H * (1 - 2 * k));
    }

    /// <summary>
    /// The dungeon view. Which background columns and which slabs to light is
    /// <see cref="MazeSelect"/>, translated from sub0980 in MODULE3.BAS; where the pieces land is
    /// <see cref="MazeView"/>, from the same procedure's tail. Both read the same square words
    /// this port already loads from MDATA11, with the original's bit numbering, so there is
    /// nothing to convert and nothing here is invented.
    /// </summary>
    /// <summary>
    /// The lead group on this square, <c>Foes(0)</c> - whether it has engaged you or is only
    /// standing there. <c>fn0C3B</c> is given <c>Foes(0).GroupCount</c> as well as the reference
    /// and answers False when it is zero, so a group you have killed is not a group: null once
    /// nothing in it is still standing.
    /// </summary>
    private Monster? LeadGroup
    {
        get
        {
            if (_combat is { Monsters.Count: > 0 } c)
                return c.Monsters.Any(m => m.Alive) ? c.Monsters[0].Def : null;
            return _waiting is { Count: > 0 } w ? w[0].Def : null;
        }
    }

    private void RenderView()
    {
        ViewCanvas.Children.Clear();
        // "If Chars(m00C0).DeathState > 0 ... Exit Sub" (MAZEMOD.BAS:773-776): with the leader dead
        // mdrmazShowMaze draws nothing, and sub0930 has already cleared picMaze2 (MAINMOD2.BAS:3024)
        // - the view stays empty behind lblDead
        if (_party.Members[0].IsDead) return;

        // The last thing mdrmazShowMaze does is ask fn0C3B whether the lead group is Asmodeus
        // (MAZEMOD.BAS:1478-1480), and if it is, picMaze2 gets his 100x100 picture out of
        // MG11.DLL's "ASMO" instead of the maze - the whole view, not a portrait beside it. That
        // resource had never been extracted and the moment had no picture at all here.
        //
        // No StretchBlt is the option that decides how: stretched to fill the box, or dropped in
        // the middle at its own size (the same pair of calls the maze itself gets, 1481-1485).
        if (LeadGroup is { } lead && Asmodeus.Is(lead))
        {
            var him = AsmodeusArt;
            var where = _options.NoStretch
                ? new Avalonia.Rect((W - 100) / 2, (H - 100) / 2, 100, 100)
                : new Avalonia.Rect(0, 0, W, H);
            ViewCanvas.Children.Add(new Avalonia.Controls.Image
            {
                Source = him,
                Width = where.Width, Height = where.Height,
                Stretch = Avalonia.Media.Stretch.Fill,
                [Canvas.LeftProperty] = where.X,
                [Canvas.TopProperty] = where.Y,
            });
            return;
        }

        // The original addresses the map as one flat run of records, x + 30 * (y - 1), and simply
        // reads whatever record that lands on - so do the same, and give it 0 off the ends.
        var fields = _nav.Floor.Fields;
        int Walls(int square) =>
            square >= 1 && square <= fields.Length ? (int)fields[square - 1].Flags : 0;

        var view = MazeSelect.For(_nav.X + 1, _nav.Y + 1, (int)_nav.Facing + 1, _level, Walls);

        // drawn ViewDensity times over and shown at picMaze2's size
        var img = new Avalonia.Controls.Image { Width = W, Height = H, Stretch = Avalonia.Media.Stretch.Fill };
        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)W * ViewDensity, (int)H * ViewDensity));
        using (var ctx = rtb.CreateDrawingContext())
            MazeView.Render(ctx, new Avalonia.Rect(0, 0, W * ViewDensity, H * ViewDensity), view, Walls(view.Square));
        img.Source = rtb;
        ViewCanvas.Children.Add(img);
    }

    /// <summary>Asmodeus's 100x100 picture, MG11.DLL's "ASMO", read from the player's copy at start (Art).</summary>
    private static Bitmap AsmodeusArt => Art.Bitmap("asmodeus.png");
}
