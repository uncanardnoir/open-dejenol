using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// A short game played from the main menu to the dungeon and back, through the game's own windows
/// and keys, on the invented fixture game only (FixtureGame): two characters are made in Create a
/// Character; the first joins the Chanter's guild; the two make a party, buy a sword and armour at
/// the General Store and put them on; they walk into the Sparring Room and beat the two Sparring
/// Posts there, each aimed at a group of its own; back in town both make level 2, and the first
/// learns Fixture Spark; the spell goes in a buffer by drag and drop and that buffer becomes the
/// first one's Combat Setting in Party Options; in the Target Room the fight opens on the spell
/// with no key pressed; and the party walks back up to town and out to the main menu.
///
/// Nothing in the game is seeded or hooked. The dice roll as they do in play; what makes the
/// outcome certain is the fixture's data (FixtureGame's remarks, and SelfCheckProbe's
/// PlaythroughData, which holds those numbers to it). The one thing the data cannot settle is
/// the Sparring Room's group count - two groups seven times in eight, one otherwise - and with one
/// group both characters are aimed at it.
///
/// The combat clock is not left to run: its tick handler, CombatTick, is called once a round, and
/// a round takes only as long as the game's own quarter-second pauses on the blows of the character
/// in command. Every wait is bounded, so a hang is a FAIL and not a stuck CI job. Saves go to a
/// scratch folder of the run's own, removed after. With MORDOR_SNAPSHOT_DIR set, a picture of the
/// game after each step goes to its playthrough folder, NN-step.png.
/// </summary>
static class PlaythroughCheck
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static int _failed;
    static string? _shots;
    static readonly List<Window> Opened = new();
    static RecordingDragSource? _drag;

    /// <summary>The longest any one wait may take before it is a FAIL.</summary>
    const double WaitSeconds = 15;

    /// <summary>The most rounds a fight may take before it is a FAIL.</summary>
    const int MaxRounds = 50;

    const string FirstName = "Aster", SecondName = "Briar";

    public static int Run(string game)
    {
        Console.WriteLine("\na scripted playthrough of the fixture game, through its own windows and keys:");
        _failed = 0;
        if (!GameFiles.IsFixture(game))
        {
            Console.WriteLine($"  FAIL  {game} is not the test fixture (no {FixtureGame.MarkerFile}): the playthrough only runs on the fixture");
            return 1;
        }
        string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-play-" + Guid.NewGuid().ToString("N"));
        string saves = Path.Combine(scratch, "saves");
        Directory.CreateDirectory(saves);
        var envWas = new[] { Saves.EnvDir, GameFolder.EnvIni, "MORDOR_MUTE" }.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        var hostWas = ModHost.Active;
        string gameDirWas = TitleWindow.GameDir;
        var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => Opened.Add(w));
        var play = new Play(game, saves);
        try
        {
            Environment.SetEnvironmentVariable(Saves.EnvDir, saves);
            Environment.SetEnvironmentVariable(GameFolder.EnvIni, Path.Combine(scratch, "launch.ini"));
            Environment.SetEnvironmentVariable("MORDOR_MUTE", "1");
            ModHost.Active = new ModHost(new ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
            TitleWindow.GameDir = game;
            // the save journal opened as the app opens it once the game folder is known (App.OpenJournal)
            App.OpenJournal(game);
            if (Environment.GetEnvironmentVariable("MORDOR_SNAPSHOT_DIR") is { Length: > 0 } shots)
                Directory.CreateDirectory(_shots = Path.Combine(shots, "playthrough"));
            BindDragSource();
            play.Through();
        }
        catch (Exception ex) { Fail("the playthrough could not be driven: " + (ex.InnerException ?? ex)); }
        finally
        {
            opened.Dispose();
            SaveJournal.Detach();
            foreach (var w in Opened.ToArray().Reverse()) { try { if (w.IsVisible) w.Close(); } catch { } }
            Pump();
            Opened.Clear();
            TitleWindow.GameDir = gameDirWas;
            ModHost.Active = hostWas;
            foreach (var (k, v) in envWas) Environment.SetEnvironmentVariable(k, v);
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                || !Path.GetFileName(scratch).StartsWith("mordor-uicheck-play-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the playthrough's temporary folder.");
            try { Directory.Delete(scratch, true); } catch { }
        }
        Console.WriteLine(_failed == 0 ? "  the playthrough went through" : $"  {_failed} playthrough check(s) FAILED");
        return _failed;
    }

    // ---------------- the steps ----------------

    sealed class Play
    {
        readonly string _saves;
        readonly List<Spell> _spells;
        readonly List<Item> _items;
        TitleWindow? _menu;
        CityWindow? _town;
        TitleWindow Menu => _menu ?? throw new InvalidOperationException("the main menu is not up");
        CityWindow Town => _town ?? throw new InvalidOperationException("the town is not up");
        Spell Spark => _spells.First(s => s.Name == FixtureGame.PlaythroughSpell);
        Item ItemNamed(string name) => _items.First(i => i.Name == name);

        public Play(string game, string saves)
        {
            _saves = saves;
            _spells = MdrLoader.LoadSpells(GameFiles.DataFile(game, "MDATA2.MDR"));
            _items = MdrLoader.LoadItems(GameFiles.DataFile(game, "MDATA3.MDR"));
        }

        Party Party => Field<Party>(_town!, "_party");
        Character First => Party.Members.First(m => m.Name == FirstName);
        Character Second => Party.Members.First(m => m.Name == SecondName);
        DungeonWindow? Below => Field<DungeonWindow?>(_town!, "_below");

        public void Through()
        {
            var steps = new (string Name, Func<bool> Run)[]
            {
                ("1, two characters made in Create a Character", Create),
                ("2, the first joins the Chanter", JoinChanter),
                ("3, the second joins the first's party", FormParty),
                ("4, both buy a sword and armour and put them on", BuyGear),
                ("5, into the dungeon and the Sparring Room", IntoSparringRoom),
                ("6, the fight, each aimed at a group of its own", Spar),
                ("7, back to town, and both make level 2", LevelUp),
                ("8, the spell in a buffer, and that buffer the Combat Setting", SetBuffer),
                ("9, the Target Room: the spell cast with no key pressed", CastOnArrival),
                ("10, back to town and out to the main menu, the saves written", Home),
                ("11, Exit at the main menu, and the save journal it leaves", ExitAndJournal),
            };
            // A step returns false when the game is not where the next step needs it (no fight began,
            // a window did not open): the run stops there. A check that fails without that - a
            // figure that is not the one wanted - is counted, and the run goes on.
            foreach (var (name, run) in steps)
            {
                Console.WriteLine($"  step {name}:");
                bool ok;
                try { ok = run(); }
                catch (Exception ex) { Fail($"step {name} threw: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}\n{(ex.InnerException ?? ex).StackTrace}"); ok = false; }
                OtherBoxes($"after step {name}");
                if (!ok) { Fail($"the playthrough stopped at step {name}"); return; }
            }
        }

        // ---- 1 ----

        bool Create()
        {
            _menu = new TitleWindow();
            _menu.Show(); Pump();
            // a fresh saves folder is a first run: the welcome box
            if (!Answer("Welcome!", "OK")) return false;
            Pump();
            // Plainfolk start at 10 in everything with 6 points to spend: Int and Wis to 12 make the
            // first eligible for the Chanter (Int 12, Wis 12), and the last two go on Str
            if (!MakeCharacter(FirstName, new[] { (1, 2), (2, 2), (0, 2) }, chanter: true)) return false;
            if (!Unload()) return false;
            if (!MakeCharacter(SecondName, new[] { (0, 3), (3, 3) }, chanter: false)) return false;
            if (!Unload()) return false;
            // Load Character, the first
            Click(Menu.FindControl<Button>("LoadBtn")!);
            if (!PickName("Load Character", FirstName)) return false;
            Check("the first is loaded: the menu offers to run it",
                  ButtonText(Menu.FindControl<Button>("CreateBtn")!), $"Run \"{FirstName}\"");
            foreach (string name in new[] { FirstName, SecondName })
                Check($"{name}'s save is written", File.Exists(SaveOf(name)), true);
            Shot("01-created", _menu);
            return true;
        }

        string SaveOf(string name) => Path.Combine(_saves, Character.SafeFileName(name) + ".json");

        bool MakeCharacter(string name, (int Stat, int Ups)[] ups, bool chanter)
        {
            Click(Menu.FindControl<Button>("CreateBtn")!);
            var make = WaitForWindow<CharacterCreationWindow>();
            if (make == null) return Fail("Create a Character did not open");
            var nameBox = make.FindControl<TextBox>("NameBox")!;
            nameBox.Focus(); Pump();
            make.KeyTextInput(name); Pump();
            Check($"{name}: the name typed into the Name box", nameBox.Text, name);
            make.FindControl<ComboBox>("RaceBox")!.SelectedIndex = 0;   // Plainfolk
            make.FindControl<ComboBox>("SexBox")!.SelectedIndex = 0;
            make.FindControl<ComboBox>("AlignBox")!.SelectedIndex = 0;
            Pump();
            var rows = make.FindControl<StackPanel>("StatRows")!;
            foreach (var (stat, n) in ups)
                for (int i = 0; i < n; i++)
                {
                    // the rows are built again after every press: the ▲ of this row as it is now
                    var spin = ((Grid)rows.Children[stat]).Children.OfType<StackPanel>().First();
                    Click((Button)spin.Children[0]);
                }
            Check($"{name}: every point spent", make.FindControl<TextBlock>("PoolText")!.Text, "0");
            var chanterRow = make.FindControl<StackPanel>("GuildRows")!.Children.OfType<TextBlock>()
                                 .FirstOrDefault(t => t.Text == FixtureGame.PlaythroughGuild);
            if (chanter)
                Check($"{name}: the Chanter is listed in bold, the stats it asks met",
                      chanterRow?.FontWeight == Avalonia.Media.FontWeight.Bold, true);
            if (chanter) Shot("01-create-a-character", make);
            Click(make.FindControl<Button>("CreateBtn")!);
            // no password: asked twice, then confirmed
            if (!Answer("ENTER PASSWORD", "OK") || !Answer("ENTER PASSWORD", "OK") || !Answer("NO PASSWORD", "Yes")) return false;
            if (!WaitFor(() => !make.IsVisible)) return Fail($"{name}: Create a Character did not close after saving");
            Check($"{name}: saved", File.Exists(SaveOf(name)), true);
            return true;
        }

        bool Unload()
        {
            var load = Menu.FindControl<Button>("LoadBtn")!;
            if (ButtonText(load) != "Unload Character") return Fail($"the menu's Load button reads \"{ButtonText(load)}\", not Unload Character");
            Click(load);
            Check("unloaded: the menu offers Create a Character again", ButtonText(Menu.FindControl<Button>("CreateBtn")!), "Create a Character");
            return true;
        }

        // ---- 2 ----

        bool JoinChanter()
        {
            Click(_menu!.FindControl<Button>("CreateBtn")!);   // Run "Aster"
            _town = WaitForWindow<CityWindow>();
            if (_town == null) return Fail("the town did not come up");
            _town.Width = 1400; _town.Height = 1000;
            Pump(); Pump();
            Check("in town: a party of one, the first", string.Join(",", Party.Members.Select(m => m.Name)), FirstName);
            Click(Caption("Guilds"));
            var list = ServiceList(s => s.StartsWith(FixtureGame.PlaythroughGuild, StringComparison.Ordinal));
            if (list == null) return Fail("the Guilds' list has no Chanter");
            list.SelectedIndex = IndexWhere(list, s => s.StartsWith(FixtureGame.PlaythroughGuild, StringComparison.Ordinal));
            Pump();
            Click(ServiceButton("Visit"));
            Check("visiting the Chanter's guild", ServiceText(t => t.StartsWith("Welcome to the", StringComparison.Ordinal)), $"Welcome to the {FixtureGame.PlaythroughGuild}'s guild!");
            Click(ServiceButton("Join"));
            if (!Answer(GuildHall.JoinTitle, "Yes")) return false;
            Pump();
            Check("the first belongs to the Chanter and the Nomad, the Chanter the active guild, at level 1",
                  $"{First.Guild.Guild} {First.Guild.Level} {string.Join("+", First.Guilds.Select(g => g.Guild).OrderBy(g => g))}",
                  $"{FixtureGame.PlaythroughGuild} 1 {FixtureGame.PlaythroughGuild}+{GuildData.StartingGuild}");
            Shot("02-chanter", Town);
            Click(ServiceButton("EXIT"));
            return true;
        }

        // ---- 3 ----

        bool FormParty()
        {
            // a, Ask to Join, from the town's own keys
            Key(Town, Avalonia.Input.Key.A, symbol: "a");
            if (!PickName("Ask to Join", SecondName)) return false;
            Check("the party is two: the first, then the second", string.Join(",", Party.Members.Select(m => m.Name)), $"{FirstName},{SecondName}");
            // the Party window: the second's row clicked, and Switch To
            if (!SwitchTo(1)) return false;
            Shot("03-party", Town);
            return true;
        }

        /// <summary>The Party window's row for this member clicked, then Switch To.</summary>
        bool SwitchTo(int slot)
        {
            var rows = Town.FindControl<StackPanel>("PartyRows")!;
            var row = (Control)rows.Children[slot];
            var at = Centre(row, Town);
            if (Town.InputHitTest(at) is Visual hit && (hit == row || row.IsVisualAncestorOf(hit)))
            {
                Apart();
                MouseClick(Town, at);
                Apart();
            }
            else
            {
                // something is drawn over the Party window there: Alt and the member's number, the same switch
                Note($"the Party window's row {slot + 1} is covered here; Alt+{slot + 1} instead");
                Key(Town, Avalonia.Input.Key.D1 + slot, RawInputModifiers.Alt);
            }
            Click(Town.FindControl<Button>("PartySwitchBtn")!);
            return Check($"switched to {Party.Members[slot].Name}", Party.Selected.Name, Party.Members[slot].Name);
        }

        // ---- 4 ----

        bool BuyGear()
        {
            // the second is in command after step 3
            foreach (int slot in new[] { 1, 0 })
            {
                if (Party.SelectedIndex != slot && !SwitchTo(slot)) return false;
                var who = Party.Selected;
                Click(Caption("General Store"));
                long before = who.GoldOnHand.Whole;
                long cost = 0;
                var bought = new List<int>();
                foreach (string name in new[] { FixtureGame.PlaythroughWeapon, FixtureGame.PlaythroughArmour })
                {
                    var it = ItemNamed(name);
                    int at = Buy(who, it);
                    if (at <= 0) return false;
                    bought.Add(at);
                    cost += it.Price;
                }
                Check($"{who.Name}: paid {cost} gold for the two", before - who.GoldOnHand.Whole, cost);
                Click(ServiceButton("EXIT"));
                // Objects & Spells, its Items page: each slot picked and Equip pressed
                Key(Town, Avalonia.Input.Key.I, RawInputModifiers.Control);
                var pack = Town.FindControl<ListBox>("PackList")!;
                foreach (int at in bought)
                {
                    pack.SelectedIndex = at; Pump();
                    Click(Town.FindControl<Button>("PackEquipBtn")!);
                    Check($"{who.Name}: {_items.First(i => i.Id == who.Pack[at]!.ItemId).Name} equipped", who.Pack[at]?.Equipped == true, true);
                }
            }
            foreach (var m in Party.Members)
                Check($"{m.Name}: wearing the sword and the armour",
                      string.Join(",", m.Inventory.Where(s => s.Equipped).Select(s => _items.First(i => i.Id == s.ItemId).Name).OrderBy(n => n)),
                      string.Join(",", new[] { FixtureGame.PlaythroughWeapon, FixtureGame.PlaythroughArmour }.OrderBy(n => n)));
            Shot("04-gear", Town);
            return true;
        }

        /// <summary>The Store's row for the item clicked and BUY pressed: the pack slot it went to, or 0.</summary>
        int Buy(Character who, Item it)
        {
            var grid = Town.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.IsEffectivelyVisible);
            if (grid == null) { Fail("the Store's list is not up"); return 0; }
            var line = grid.ItemsSource?.Cast<object>().FirstOrDefault(o => (o.GetType().GetProperty("Item")?.GetValue(o) as Item)?.Id == it.Id);
            if (line == null) { Fail($"the Store does not list {it.Name}"); return 0; }
            grid.ScrollIntoView(line, null); Pump();
            var row = grid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.DataContext == line && r.IsEffectivelyVisible);
            if (row == null) { Fail($"{it.Name}'s row is not drawn"); return 0; }
            var at = Centre(row, Town);
            if (!(Town.InputHitTest(at) is Visual hit && row.IsVisualAncestorOf(hit))) { Fail($"{it.Name}'s row is covered by another window"); return 0; }
            Apart();
            MouseClick(Town, at);
            var buy = ServiceButton("BUY");
            if (!buy.IsEffectivelyEnabled) { Fail($"BUY is not offered for {it.Name}"); return 0; }
            var had = Enumerable.Range(1, Character.PackSize - 1).Where(i => who.Pack[i]?.ItemId == it.Id).ToList();
            Click(buy);
            int slot = Enumerable.Range(1, Character.PackSize - 1).FirstOrDefault(i => who.Pack[i]?.ItemId == it.Id && !had.Contains(i));
            if (slot <= 0) { Fail($"{who.Name}: no {it.Name} in the pack after BUY"); return 0; }
            Check($"{who.Name}: bought {it.Name}, and the Messages say where it went",
                  CityMessages().Any(l => l == $"Slot #{slot + 1} now contains your {it.Name}."), true);
            return slot;
        }

        // ---- 5 ----

        bool IntoSparringRoom()
        {
            if (Party.SelectedIndex != 0 && !SwitchTo(0)) return false;
            if (!Down()) return false;
            if (!Walk(FixtureGame.SparringRoom)) return false;
            var d = Below!;
            var fight = Field<CombatEngine?>(d, "_combat");
            if (fight == null) return Fail("no fight began on walking into the Sparring Room");
            var groups = CombatMonster.Runs(fight.Monsters);
            Check("the Sparring Room holds the Sparring Post, one to a group, hostile",
                  fight.Monsters.All(m => m.Def.Name == FixtureGame.SparringMonster && m.Hostile) && groups.All(g => g.Count == 1), true);
            Check("one or two groups of it", groups.Count is 1 or 2, true);
            Shot("05-sparring-room", Town);
            return true;
        }

        // ---- 6 ----

        bool Spar()
        {
            var d = Below!;
            var fight = Field<CombatEngine>(d, "_combat");
            var groups = CombatMonster.Runs(fight.Monsters);
            if (groups.Count == 1)
                Note("the Sparring Post came in one group this time (one time in eight): both are aimed at it");
            // the number keys aim the one in command; Alt+2 puts the second in command
            Key(Town, Avalonia.Input.Key.D1, symbol: "1");
            Key(Town, Avalonia.Input.Key.D2, RawInputModifiers.Alt);
            Check("Alt+2: the second in command", Party.Selected.Name, SecondName);
            Key(Town, groups.Count > 1 ? Avalonia.Input.Key.D2 : Avalonia.Input.Key.D1, symbol: groups.Count > 1 ? "2" : "1");
            Key(Town, Avalonia.Input.Key.D1, RawInputModifiers.Alt);
            int Group(Character c) => groups.FindIndex(g => g.Contains(fight.Monsters.ElementAtOrDefault(Party.OrderFor(c).Target)!));
            Check("the first aimed at group 1, the second at group " + (groups.Count > 1 ? "2" : "1"),
                  $"{Group(First)} {Group(Second)}", groups.Count > 1 ? "0 1" : "0 0");
            long xp1 = First.Guild.Experience, xp2 = Second.Guild.Experience;
            if (!Fight(d, fight, null)) return false;
            Check("every Sparring Post dead, the fight won", fight.Won && fight.Monsters.All(m => !m.Alive), true);
            foreach (var c in new[] { First, Second })
            {
                long before = c == First ? xp1 : xp2;
                // the fight's own record of what each blow paid (CombatEngine.Credit): nought for one who never landed one
                Check($"{c.Name} landed blows, paid {fight.Experience.GetValueOrDefault(c)} experience for them ({before} to {c.Guild.Experience})",
                      fight.Experience.GetValueOrDefault(c) > 0 && c.Guild.Experience > before, true);
                Check($"{c.Name} has the experience for level 2 in the {c.Guild.Guild}'s guild ({c.Guild.Experience} of {c.ExpForNextLevel()})",
                      c.CanLevelUp(), true);
            }
            Check("nobody was hurt", Party.Members.All(m => m.CurHits == m.MaxHits), true);
            Shot("06-fight-won", Town);
            return true;
        }

        // ---- 7 ----

        bool LevelUp()
        {
            if (!Walk(FixtureGame.Entrance) || !Up()) return false;
            foreach (int slot in new[] { 0, 1 })
            {
                if (Party.SelectedIndex != slot && !SwitchTo(slot)) return false;
                var who = Party.Selected;
                // the town opens the Guilds by itself for one who can make a level; otherwise its picture
                if (ServiceButtonOrNull("Make Level") == null) Click(Caption("Guilds"));
                var make = ServiceButton("Make Level");
                if (!make.IsEffectivelyEnabled) return Fail($"{who.Name}: Make Level is not offered");
                long gold = who.GoldOnHand.Whole, fee = who.GoldForNextLevel();
                int messages = CityMessages().Count;
                Click(make);
                if (!Answer("MAKE LEVEL", "OK")) return false;
                // the boxes that may follow it - a new Guildmaster, a crest - are the game's to show
                OtherBoxes($"after {who.Name}'s level");
                Check($"{who.Name}: level 2 in the {who.Guild.Guild}'s guild, for {fee} gold",
                      $"{who.Guild.Level} {gold - who.GoldOnHand.Whole}", $"2 {fee}");
                if (who == First)
                {
                    Check($"{who.Name} knows {Spark.Name} now", Spellbook.Known(who, _spells).Contains(Spark), true);
                    Check("and the Messages say it was learned",
                          CityMessages().Skip(messages).Any(l => l.Contains($"you learned \"{Spark.Name}\"")), true);
                }
            }
            Shot("07-level-2", Town);
            Click(ServiceButton("EXIT"));
            return true;
        }

        // ---- 8 ----

        bool SetBuffer()
        {
            if (Party.SelectedIndex != 0 && !SwitchTo(0)) return false;
            var town = Town;
            // Objects & Spells on its Spells page (Ctrl+P), the character window on Buffers (F9), side by side
            Key(town, Avalonia.Input.Key.P, RawInputModifiers.Control);
            Key(town, Avalonia.Input.Key.F9);
            var sheet = Field<MdiChild>(town, "_sheetChild");
            var charWin = Field<MdiChild>(town, "_characterChild");
            sheet.IsVisible = charWin.IsVisible = true;
            sheet.PlaceAt(new Rect(10, 10, 420, 360));
            charWin.PlaceAt(new Rect(450, 10, 520, 420));
            // each brought to the front, as a click on its title bar would
            sheet.Activate(); charWin.Activate();
            Pump(); Pump();
            var classes = town.FindControl<ComboBox>("ClassBox")!;
            int fire = IndexWhere(classes, s => s == Librarian.SpellSchools[Spark.Category]);
            if (fire < 0) return Fail("the Spell Type box has no " + Librarian.SpellSchools[Spark.Category]);
            classes.SelectedIndex = fire; Pump();
            var spellList = town.FindControl<ListBox>("SpellList")!;
            var known = Field<List<Spellbook.SpellRow>>(town, "_known");
            int at = known.FindIndex(r => r.Spell == Spark || r.Spell.Id == Spark.Id);
            if (at < 0) return Fail($"{Spark.Name} is not on the Spells page");
            spellList.ScrollIntoView(at); Pump();
            if (spellList.ContainerFromIndex(at) is not Control spellRow) return Fail($"{Spark.Name}'s line is not drawn");
            var payload = Drag(town, Centre(spellRow, town));
            if (payload?.Kind != DragKind.Spell) return Fail($"dragging {Spark.Name}'s line carried {payload?.Kind.ToString() ?? "nothing"}");
            var buffers = Field<Panel>(Field<CharacterPanel>(town, "_charPanel"), "_buffers");
            var first = (Control)buffers.Children[0];
            var onto = Centre(first, town);
            if (!(town.InputHitTest(onto) is Visual over && (over == first || first.IsVisualAncestorOf(over))))
                return Fail("buffer 1 on the character window's Buffers page is covered or not drawn");
            Drop(town, onto, payload);
            int spellIndex = _spells.IndexOf(Spark);
            Check($"buffer 1 holds {Spark.Name}",
                  SpellBuffers.IsSpell(First.Buffers[0]) && SpellBuffers.SpellIndexOf(First.Buffers[0]) == spellIndex, true);
            // Party Options: the first, Buffer, buffer 1, OK
            Click(town.FindControl<Button>("PartyOptionsBtn")!);
            var options = WaitForBox("Party Options");
            if (options == null) return Fail("Party Options did not open");
            var who = options.GetVisualDescendants().OfType<ListBox>().First();
            who.SelectedIndex = 0; Pump();
            var buffer = options.GetVisualDescendants().OfType<RadioButton>().First(r => Equals(r.Content, "Buffer"));
            buffer.IsChecked = true; Pump();
            var which = options.GetVisualDescendants().OfType<ComboBox>().First();
            which.SelectedIndex = 0; Pump();
            Shot("08-party-options", options);
            Click(FindButton(options, "OK")!);
            WaitFor(() => !options.IsVisible);
            Check($"{FirstName}'s Combat Setting is buffer 1", First.CombatSetting, 3);
            Check($"{SecondName}'s is still Fight", Second.CombatSetting, 1);
            Shot("08-buffer", town);
            return true;
        }

        // ---- 9 ----

        bool CastOnArrival()
        {
            if (!Down()) return false;
            if (!Walk((FixtureGame.TargetRoom.X, FixtureGame.Entrance.Y)) || !Walk(FixtureGame.TargetRoom)) return false;
            var d = Below!;
            var fight = Field<CombatEngine?>(d, "_combat");
            if (fight == null) return Fail("no fight began on walking into the Target Room");
            Check("the Target Room holds one Straw Target, hostile",
                  fight.Monsters.Count == 1 && fight.Monsters[0].Def.Name == FixtureGame.TargetMonster && fight.Monsters[0].Hostile, true);
            // no key pressed: the order is the Combat Setting's, taken up as the fight began (sub0BDE)
            var order = Party.OrderFor(First);
            Check($"{FirstName}'s order is the spell, from the Combat Setting", $"{order.Kind} {order.SpellId}", $"{OrderKind.Spell} {Spark.Id}");
            Check($"{SecondName}'s is Fight", Party.OrderFor(Second).Kind, OrderKind.Fight);
            string quoted = $"\"{Spark.Name}\"";
            Check($"the Party window's Option column reads {quoted}",
                  Town.FindControl<StackPanel>("PartyRows") is { } rows && rows.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == quoted)
                  || Field<StackPanel>(d, "PartyRows").GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == quoted), true);
            int points = First.CurSpellPoints, cost = Spellbook.Cost(Spark, First);
            int messages = d.Messages.Count;
            var captions = new List<string>();
            bool shot = false;
            void Seen(string caption)
            {
                if (caption == "" || captions.LastOrDefault() == caption) return;
                captions.Add(caption);
                if (!shot && caption.StartsWith("You ", StringComparison.Ordinal)) { shot = true; Shot("09-spell-cast", Town); }
            }
            if (!Fight(d, fight, Seen)) return false;
            Check($"{FirstName} paid {cost} spell points for the cast", points - First.CurSpellPoints >= cost && cost > 0, true);
            // a damage spell's result is lblCharacterHit's (Combat.CastDamage, CaptionLines): "You Fried
            // One!" or "You damaged one for N!" for the one in command
            Check("the combat caption shows the spell's result (" + string.Join(" | ", captions) + ")",
                  captions.Any(c => c is "You Fried One!" || c.StartsWith("You damaged one for", StringComparison.Ordinal)), true);
            var said = d.Messages.Skip(messages).ToList();
            Note("the Messages window during the fight: " + (said.Count == 0 ? "(nothing)" : string.Join(" | ", said)));
            Check("the Straw Target dead, the fight won", fight.Won, true);
            return true;
        }

        // ---- 10 ----

        bool Home()
        {
            if (!Walk((FixtureGame.TargetRoom.X, FixtureGame.Entrance.Y)) || !Walk(FixtureGame.Entrance) || !Up()) return false;
            Check("back in town: no dungeon, everybody at level 0",
                  Below == null && Party.Members.All(m => m.DungeonLevel == 0), true);
            Shot("10-town", Town);
            // the Exit picture: "Exit to Main Menu?", and the party is not kept for next time
            Click(Caption("Exit"));
            if (!Answer("EXIT", "Yes")) return false;
            if (!Answer(Mordor.Core.Party.ReloadTitle, "No")) return false;
            if (!WaitFor(() => !Town.IsVisible && Menu.IsVisible)) return Fail("the town did not give way to the main menu");
            foreach (string name in new[] { FirstName, SecondName })
            {
                var c = Character.Load(SaveOf(name));
                bool first = name == FirstName;
                Check($"{name}'s save: level 2 in the {(first ? FixtureGame.PlaythroughGuild : GuildData.StartingGuild)}'s guild, in town, the gear worn",
                      $"{c.Guild.Guild} {c.Guild.Level} {c.DungeonLevel} {c.Inventory.Count(s => s.Equipped)}",
                      $"{(first ? FixtureGame.PlaythroughGuild : GuildData.StartingGuild)} 2 0 2");
                if (first)
                    Check($"{name}'s save: buffer 1 the spell, and the Combat Setting on it",
                          $"{SpellBuffers.IsSpell(c.Buffers[0]) && SpellBuffers.SpellIndexOf(c.Buffers[0]) == _spells.IndexOf(Spark)} {c.CombatSetting}", "True 3");
            }
            Shot("10-main-menu", Menu);
            return true;
        }

        // ---- 11 ----

        /// <summary>
        /// The menu's Exit, Yes, and the goodbye box; then the save journal the game kept on the way
        /// (SaveJournal, docs/GAME_FILES.md): a few of its lines, and none with a path in it.
        /// </summary>
        bool ExitAndJournal()
        {
            Click(Menu.FindControl<Button>("ExitBtn")!);
            if (!Answer("EXIT", "Yes")) return false;
            if (LimitedVersion.SaysGoodbye && !Answer("Mordor 1.1", "OK")) return false;
            if (!WaitFor(() => !Menu.IsVisible)) return Fail("the main menu did not close on Exit");
            string journal = Path.Combine(_saves, SaveJournal.FileName);
            if (!File.Exists(journal)) return Fail("no save journal in the saves folder");
            var lines = File.ReadAllLines(journal);
            bool Has(string evt, params string[] parts) => lines.Any(l => l.Contains("] " + evt + " ") && parts.All(l.Contains));
            Check("journal: opened first, on the MORDOR_SAVES folder", lines.Length > 0 && lines[0].Contains("] OPEN OpenDejenol ")
                  && lines[0].Contains("saves folder: MORDOR_SAVES"), true);
            Check($"journal: {FirstName}'s file written by Create a Character, with the character",
                  Has("WRITE", $"\"{FirstName}.json\"", "Create a Character", ", town"), true);
            Check($"journal: {FirstName} loaded by Load Character, the file the same as written",
                  Has("LOAD", $"\"{FirstName}\" how: Load Character", $"file \"{FirstName}.json\"", "same as written"), true);
            Check($"journal: {FirstName} run from the menu", Has("LOAD", $"\"{FirstName}\" how: Run", "same as written"), true);
            Check($"journal: {SecondName} joined in town, the file the same as written",
                  Has("LOAD", $"\"{SecondName}\" how: Ask to Join", "same as written"), true);
            Check("journal: the dungeon's autosave and the arrival in town write the party",
                  Has("WRITE", $"\"{FirstName}.json\"", "autosave") && Has("WRITE", $"\"{SecondName}.json\"", "arrival in town"), true);
            Check("journal: the world files written (automap.json, dungeon.json)",
                  Has("WRITE", "\"automap.json\"") && Has("WRITE", "\"dungeon.json\""), true);
            Check($"journal: {SecondName} unloaded as the party broke up at exit",
                  Has("UNLOAD", $"\"{SecondName}\" how: the party breaking up at exit", "last saved by this run"), true);
            Check("journal: the last save of each is level 2, in town",
                  lines.Last(l => l.Contains($"] WRITE \"{FirstName}.json\"")).Contains($"{FixtureGame.PlaythroughGuild} 2,")
                  && lines.Last(l => l.Contains($"] WRITE \"{FirstName}.json\"")).EndsWith(", town"), true);
            Check("journal: nothing CHANGED, OLDER or failed in a game played alone",
                  lines.Any(l => l.Contains("CHANGED since") || l.Contains("OLDER than") || l.Contains("] WRITE-FAILED ")), false);
            Check("journal: the last line is the Exit at the main menu", lines[^1].Contains("] CLOSE Exit at the main menu"), true);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Check("journal: no line holds the home folder or the saves folder's path",
                  lines.Any(l => l.Contains(home, StringComparison.OrdinalIgnoreCase) || l.Contains(_saves, StringComparison.OrdinalIgnoreCase)), false);
            // a sample, for the eye: the opening, the first loads, and the end
            foreach (string l in lines.Take(4).Concat(lines.Where(l => l.Contains("] LOAD ")).Take(3)).Concat(lines.TakeLast(5)))
                Note("journal: " + l);
            return true;
        }

        // ---------------- the dungeon ----------------

        /// <summary>The town's Dungeon picture: down the stairs, the clocks held.</summary>
        bool Down()
        {
            Click(Caption("Dungeon"));
            if (!WaitFor(() => Below != null)) return Fail("the dungeon did not come up");
            Pump(); Hold();
            var d = Below!;
            Check("in the dungeon at the town stairs, facing north",
                  $"{Field<int>(d, "_level")} {Nav(d).X},{Nav(d).Y} {Nav(d).Facing}", $"1 {FixtureGame.Entrance.X},{FixtureGame.Entrance.Y} {Facing.North}");
            return true;
        }

        /// <summary>t on the town stairs, and the town back.</summary>
        bool Up()
        {
            var d = Below!;
            if ((Nav(d).X, Nav(d).Y) != FixtureGame.Entrance) return Fail("not on the town stairs");
            Key(Town, Avalonia.Input.Key.T, symbol: "t");
            if (!WaitFor(() => Below == null)) return Fail("t on the stairs did not take the party up to town");
            Pump(); Pump();
            return true;
        }

        DungeonNav Nav(DungeonWindow d) => Field<DungeonNav>(d, "_nav");

        /// <summary>
        /// The cursor keys, in a straight line to the square: turned to face it, then Up a step at a
        /// time, each step waited for.
        /// </summary>
        bool Walk((int X, int Y) to)
        {
            var d = Below!;
            Hold();
            var nav = Nav(d);
            int dx = Math.Sign(to.X - nav.X), dy = Math.Sign(to.Y - nav.Y);
            if (dx != 0 && dy != 0) return Fail($"no straight line from ({nav.X},{nav.Y}) to ({to.X},{to.Y})");
            if (dx == 0 && dy == 0) return true;
            var face = Enum.GetValues<Facing>().First(f => DungeonNav.Delta(f) == (dx, dy));
            int turn = ((int)face - (int)nav.Facing + 4) % 4;
            if (turn != 0)
            {
                var k = turn switch { 1 => Avalonia.Input.Key.Right, 3 => Avalonia.Input.Key.Left, _ => Avalonia.Input.Key.Down };
                Ready(d);
                Key(Town, k);
                if (!WaitFor(() => Nav(d).Facing == face)) return Fail($"the party did not turn to face {face}");
            }
            for (int steps = 0; (Nav(d).X, Nav(d).Y) != to; steps++)
            {
                if (steps > 30) return Fail($"walking to ({to.X},{to.Y}) did not get there");
                var from = (Nav(d).X, Nav(d).Y);
                Ready(d);
                Key(Town, Avalonia.Input.Key.Up);
                if (!WaitFor(() => (Nav(d).X, Nav(d).Y) != from)) return Fail($"a step forward from ({from.Item1},{from.Item2}) went nowhere");
                Hold();
            }
            return true;
        }

        /// <summary>Wait out a sub07E2 pause that holds the keys (a won fight's gold line).</summary>
        void Ready(DungeonWindow d)
        {
            WaitFor(() => !Field<bool>(d, "_holdingKeys") && !Field<bool>(d, "_replaying") && !Field<bool>(d, "_inRound"));
            Hold();
        }

        /// <summary>The dungeon's two clocks stopped: the playthrough plays the rounds itself.</summary>
        void Hold()
        {
            if (Below is not DungeonWindow d) return;
            foreach (string name in new[] { "_clock", "_ailmentClock" })
                Field<DispatcherTimer?>(d, name)?.Stop();
        }

        /// <summary>
        /// The fight played to its end, a round at a time through the combat clock's own handler,
        /// each round waited out (the game's pauses on the blows of the one in command). The
        /// combat caption is read as the round draws it.
        /// </summary>
        bool Fight(DungeonWindow d, CombatEngine fight, Action<string>? caption)
        {
            var hit = Field<TextBlock>(d, "CombatHit");
            for (int round = 1; !fight.IsOver; round++)
            {
                if (round > MaxRounds) return Fail($"the fight was not over in {MaxRounds} rounds");
                Ready(d);
                Call(d, "CombatTick");
                var clock = Stopwatch.StartNew();
                do
                {
                    Pump(1); Hold();
                    caption?.Invoke(hit.Text ?? "");
                    if (clock.Elapsed.TotalSeconds > WaitSeconds) return Fail($"round {round} did not end in {WaitSeconds} seconds");
                    Thread.Sleep(2);
                } while (Field<bool>(d, "_inRound"));
                caption?.Invoke(hit.Text ?? "");
            }
            Ready(d);
            Pump();
            return true;
        }

        // ---------------- the town ----------------

        /// <summary>A town picture's caption button, which does what the picture does.</summary>
        Button Caption(string text) =>
            Town.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("caption") && ButtonText(b) == text)
            ?? throw new InvalidOperationException($"the town has no {text} caption");

        Button? ServiceButtonOrNull(string text) =>
            Town.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && ButtonText(b) == text);

        Button ServiceButton(string text) =>
            ServiceButtonOrNull(text) ?? throw new InvalidOperationException($"no {text} button is up in town");

        string? ServiceText(Func<string, bool> which) =>
            Town.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "").FirstOrDefault(which);

        ListBox? ServiceList(Func<string, bool> holding) =>
            Town.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.IsEffectivelyVisible && IndexWhere(l, holding) >= 0);

        List<string> CityMessages() => Field<List<string>>(Town, "_messages");

        /// <summary>A name picker (Load Character, Ask to Join): Down until it shows the name, then OK.</summary>
        bool PickName(string title, string name)
        {
            var box = WaitForBox(title);
            if (box == null) return Fail($"{title} did not come up");
            var text = box.GetVisualDescendants().OfType<TextBox>().First();
            for (int i = 0; i < 10 && text.Text != name; i++) Key(box, Avalonia.Input.Key.Down);
            if (text.Text != name) return Fail($"{title} does not offer {name}");
            Click(FindButton(box, "OK")!);
            return WaitFor(() => !box.IsVisible) || Fail($"{title} did not close");
        }
    }

    // ---------------- driving the windows ----------------

    static void Pump(int times = 6)
    {
        for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
    }

    /// <summary>Two gestures on one spot must not read as a double click (Avalonia's 500 ms).</summary>
    static void Apart() => Thread.Sleep(600);

    /// <summary>Pump until <paramref name="done"/>, or <see cref="WaitSeconds"/> - the game's own short waits (a box after an await) included.</summary>
    static bool WaitFor(Func<bool> done, double seconds = WaitSeconds)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            Pump(2);
            if (done()) return true;
            if (clock.Elapsed.TotalSeconds > seconds) return false;
            Thread.Sleep(5);
        }
    }

    static bool Check(string what, object? got, object? want)
    {
        bool ok = Equals(got, want);
        Console.WriteLine($"    {(ok ? "ok  " : "FAIL")}  {what}{(ok ? "" : $": got {got}, want {want}")}");
        if (!ok) _failed++;
        return ok;
    }

    static bool Fail(string what)
    {
        Console.WriteLine($"    FAIL  {what}");
        _failed++;
        return false;
    }

    static void Note(string what) => Console.WriteLine($"    note  {what}");

    static T WaitForWindow<T>() where T : Window
    {
        T? found = null;
        WaitFor(() => (found = Opened.OfType<T>().LastOrDefault(w => w.IsVisible)) != null);
        return found!;
    }

    /// <summary>The box with this title, once it is up.</summary>
    static Window? WaitForBox(string title)
    {
        Window? found = null;
        WaitFor(() => (found = Opened.LastOrDefault(w => w.IsVisible && w.Title == title)) != null);
        return found;
    }

    /// <summary>The box with this title answered with this button.</summary>
    static bool Answer(string title, string button)
    {
        var box = WaitForBox(title);
        if (box == null) return Fail($"no \"{title}\" box came up");
        var b = FindButton(box, button);
        if (b == null) return Fail($"the \"{title}\" box has no {button}");
        Click(b);
        return WaitFor(() => !box.IsVisible) || Fail($"the \"{title}\" box did not close on {button}");
    }

    /// <summary>
    /// Any box still up that the step did not expect: said, and closed with its OK, No or Cancel.
    /// The game's main windows are left alone.
    /// </summary>
    static void OtherBoxes(string when)
    {
        // one box's answer may put up the next: a few rounds of it, each given time to come up
        for (int round = 0; round < 5; round++)
        {
            WaitFor(() => false, 0.2);
            var up = Opened.Where(w => w.IsVisible && w is not (TitleWindow or CityWindow or CharacterCreationWindow or DungeonWindow)).Reverse().ToList();
            if (up.Count == 0) return;
            foreach (var w in up)
            {
                string text = w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
                Note($"a box was up {when}: \"{w.Title}\": {text.Replace('\n', ' ')}");
                var b = FindButton(w, "OK") ?? FindButton(w, "No") ?? FindButton(w, "Cancel");
                if (b != null) Click(b); else w.Close();
                Pump();
            }
        }
    }

    static string ButtonText(Button b)
    {
        string? s = b.Content switch
        {
            string t => t,
            AccessText t => t.Text,
            TextBlock t => t.Text,
            AccessCaption c => c.Text,
            _ => null,
        };
        return (s ?? "").Replace("_", "").Replace("&", "");
    }

    static Button? FindButton(Visual root, string text) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible && ButtonText(b) == text);

    /// <summary>A button pressed: its Click, as a mouse click or its access key raises it. A greyed button is not pressed.</summary>
    static void Click(Button b)
    {
        if (!b.IsEffectivelyEnabled) throw new InvalidOperationException($"the {ButtonText(b)} button is greyed");
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
    }

    static void Key(TopLevel w, Key key, RawInputModifiers mods = RawInputModifiers.None, string? symbol = null)
    {
        w.KeyPress(key, mods, PhysicalKey.None, symbol);
        w.KeyRelease(key, mods, PhysicalKey.None, symbol);
        Pump();
    }

    static void MouseClick(TopLevel top, Point at)
    {
        top.MouseDown(at, MouseButton.Left); Pump();
        top.MouseUp(at, MouseButton.Left); Pump();
    }

    static Point Centre(Visual v, Visual top) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), top)!.Value;

    static int IndexWhere(ItemsControl list, Func<string, bool> which)
    {
        int i = 0;
        foreach (var o in list.Items)
        {
            if (o is string s && which(s)) return i;
            i++;
        }
        return -1;
    }

    /// <summary>A press, a move past the drag distance and a release: what the stand-in drag source was handed.</summary>
    static DragPayload? Drag(TopLevel top, Point from)
    {
        _drag!.Last = null;
        Apart();
        top.MouseDown(from, MouseButton.Left); Pump();
        top.MouseMove(from + new Point(3, 3), RawInputModifiers.LeftMouseButton); Pump();
        top.MouseMove(from + new Point(12, 12), RawInputModifiers.LeftMouseButton); Pump();
        top.MouseUp(from + new Point(12, 12), MouseButton.Left); Pump();
        Apart();
        return _drag.Last;
    }

    /// <summary>The drag let go over this point, as the platform's drop arrives.</summary>
    static void Drop(TopLevel top, Point onto, DragPayload payload)
    {
        var data = payload.Wrapped();
        top.DragDrop(onto, Avalonia.Input.Raw.RawDragEventType.DragEnter, data, DragDropEffects.Link, RawInputModifiers.None);
        top.DragDrop(onto, Avalonia.Input.Raw.RawDragEventType.DragOver, data, DragDropEffects.Link, RawInputModifiers.None);
        top.DragDrop(onto, Avalonia.Input.Raw.RawDragEventType.Drop, data, DragDropEffects.Link, RawInputModifiers.None);
        Pump();
    }

    /// <summary>The headless platform has no drag source: a stand-in that notes what a drag carries (as Program.cs binds it).</summary>
    static void BindDragSource()
    {
        _drag = RecordingDragSource.Bound ?? RecordingDragSource.New();
        var locator = typeof(AvaloniaLocator).GetProperty("CurrentMutable", Any)!.GetValue(null)!;
        var helper = typeof(AvaloniaLocator).GetMethods(Any).First(m => m.Name == "Bind" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
            .MakeGenericMethod(typeof(Avalonia.Input.Platform.IPlatformDragSource)).Invoke(locator, null)!;
        helper.GetType().GetMethods(Any).First(m => m.Name == "ToConstant").MakeGenericMethod(_drag.GetType()).Invoke(helper, new object[] { _drag });
    }

    static void Shot(string name, TopLevel top)
    {
        if (_shots == null) return;
        Pump();
        top.CaptureRenderedFrame()?.Save(Path.Combine(_shots, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    static T Field<T>(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
            if (t.GetField(name, Any) is FieldInfo f) return (T)f.GetValue(o)!;
        throw new MissingFieldException(o.GetType().Name, name);
    }

    static object? Call(object o, string name, params object?[] args) =>
        o.GetType().GetMethod(name, Any)!.Invoke(o, args);
}
