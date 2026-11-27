using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// Everything found by playing the themes' first builds (PR #12 and the Light theme's relief), held
/// so it is not found by playing again. Each check names what went wrong. The readability of the
/// text is ContrastCheck's; the theme switch with a control for a caption and the town's captions
/// after a trip below are in Program.cs.
/// </summary>
static class ThemeRegressionCheck
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static int _failed;
    static void Check(string what, object? got, object? want)
    {
        bool ok = Equals(got, want);
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what}" + (ok ? "" : $": got {got}, want {want}"));
        if (!ok) _failed++;
    }

    static void Pump()
    {
        for (int i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
    }

    static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Any)!.GetValue(o)!;

    public static int Run(string game)
    {
        Console.WriteLine("\nthe themes, as they were found wrong in play:");
        _failed = 0;
        _failed += ThemePairsCheck.Run();   // the declared inks on their grounds, by arithmetic
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);

        var theme = typeof(App).Assembly.GetType("Mordor.App.ThemeSelection")!;
        var choice = theme.GetNestedType("Choice", Any)!;
        var choiceField = theme.GetField("_choice", Any)!;
        object themeWas = choiceField.GetValue(null)!;
        object C(string c) => Enum.Parse(choice, c);
        // taken straight, not chosen, unless the check is of choosing: choosing writes settings.ini
        void Theme(string c) { choiceField.SetValue(null, C(c)); theme.GetMethod("Apply", Any)!.Invoke(null, null); Pump(); }
        // the colour roles (Palette): one Light and one Dark value each
        var palette = typeof(App).Assembly.GetType("Mordor.App.Palette")!;
        SolidColorBrush Role(string name) => (SolidColorBrush)palette.GetProperty(name, Any)!.GetValue(null)!;
        string Ink(string role) => $"#{Role(role).Color.ToUInt32():x8}";

        string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-themes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var envWas = new[] { Saves.EnvDir, GameFolder.EnvIni }.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        var hostWas = ModHost.Active;
        string gameDirWas = TitleWindow.GameDir;
        try
        {
            Environment.SetEnvironmentVariable(Saves.EnvDir, scratch);
            Environment.SetEnvironmentVariable(GameFolder.EnvIni, Path.Combine(scratch, "launch.ini"));
            ModHost.Active = new ModHost(new ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
            TitleWindow.GameDir = game;
            Character Hero(string name) => new()
            {
                Name = name, Race = RaceData.All[0].Name, CurHits = 100, MaxHits = 100, GoldOnHand = 1000,
                Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 20, Experience = 2_000_000 } },
            };
            var first = Hero("Themeone");
            var second = Hero("Themetwo");
            foreach (var h in new[] { first, second }) h.Save(Path.Combine(scratch, Character.SafeFileName(h.Name) + ".json"));

            // ---- the choice ----
            Check("with nothing saved the theme is Dark, the port's look before the choice",
                  theme.GetField("Default", Any)!.GetValue(null)?.ToString(), "Dark");
            string root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Mordor.sln"))) root = Path.GetDirectoryName(root)!;
            Check("App.axaml asks for Dark, so a host that never reads the choice (the browser) draws Dark",
                  File.ReadAllText(Path.Combine(root, "src", "Mordor.App", "App.axaml")).Contains("RequestedThemeVariant=\"Dark\""), true);
            var menuFor = theme.GetMethod("MenuCommandFor", Any)!;
            Check("the system menu ticks each theme's own item, Same as Windows for System (it ticked Light)",
                  string.Join(" ", new[] { "Light", "Dark", "System" }.Select(c => $"{(uint)menuFor.Invoke(null, new[] { C(c) })!:x}")),
                  "1f10 1f20 1f00");

            // the Options window's Theme: taken as it is picked, and put back by Cancel
            Theme("Dark");
            var options = new OptionsWindow(new GameOptions(), game);
            options.Show(); Pump();
            var picker = options.GetVisualDescendants().OfType<ComboBox>()
                .FirstOrDefault(b => b.ItemsSource is IEnumerable<string> s && s.Contains("Same as system"));
            Check("the Options window has the Theme choice, on every system", picker != null, true);
            if (picker != null)
            {
                picker.SelectedIndex = 1; Pump();   // Light
                Check("... Light picked: the theme is Light at once", theme.GetProperty("Current", Any)!.GetValue(null)?.ToString(), "Light");
                options.Close(); Pump();            // Cancel: closed without OK
                Check("... and Cancel puts back the theme the window opened with", theme.GetProperty("Current", Any)!.GetValue(null)?.ToString(), "Dark");
            }
            else options.Close();

            // ---- colours come from the palette ----
            // The game's colours are in Palette.cs alone: the theme's roles, each with one Light and one
            // Dark value, and FixedColour's, the same in either. No other file of the game names a colour
            // - not in code (Color.Parse, Brush.Parse, a named Brushes colour, FromRgb/FromArgb of
            // figures) nor in markup (a colour attribute or a style's Value). A colour worked out from
            // data (a mod's own, the Automap's squares) is not a literal. The bundled mods build against
            // the mod API alone and keep their own colours, counted per file so none is added; a mod's
            // fallback for a role it looks up (for its checks, where no game publishes the roles) is not
            // counted.
            {
                var literal = new System.Text.RegularExpressions.Regex(@"Color\.Parse\(|Brush\.Parse\(|Brushes\.(?!Transparent\b)[A-Z][A-Za-z]+\b|FromRgb\(\s*(0x)?[0-9]|FromArgb\(\s*(0x)?[0-9]|(Foreground|Background|BorderBrush|Fill|Stroke|CaretBrush)=""(?!Transparent"")(#[0-9A-Fa-f]{3,8}|[A-Z][A-Za-z]+)""|Property=""(Foreground|Background|BorderBrush|Fill|Stroke|CaretBrush)"" Value=""(?!Transparent"")(#[0-9A-Fa-f]{3,8}|[A-Z][A-Za-z]+)""");
                var mods = new Dictionary<string, int>
                {
                    ["src/Mods/Mordor.Mod.Cartographer/RouteSettingsWindow.cs"] = 4,
                };
                var found = new List<string>();
                foreach (var path in Directory.GetFiles(Path.Combine(root, "src", "Mordor.App"), "*.*")
                             .Concat(Directory.GetDirectories(Path.Combine(root, "src", "Mods")).SelectMany(d => Directory.GetFiles(d, "*.cs"))))
                {
                    if (!(path.EndsWith(".cs") || path.EndsWith(".axaml")) || path.EndsWith("Palette.cs")) continue;
                    string rel = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
                    int count = File.ReadAllLines(path)
                        .Where(l => !(l.TrimStart().StartsWith("//") || l.TrimStart().StartsWith("<!--")))
                        .Where(l => !(l.Contains("Role(ThemeKeys.") || l.Contains("FindResource(ThemeKeys.")))
                        .Sum(l => literal.Matches(l).Count);
                    int may = mods.TryGetValue(rel, out int a) ? a : 0;
                    if (count > may) found.Add($"{rel} {count}" + (may > 0 ? $" (at most {may})" : ""));
                }
                Check("no colour named outside Palette.cs: a window takes a role or a FixedColour", string.Join("; ", found), "");
            }

            // ---- Light's colours ----
            Theme("Light");
            Check("the chest caption in Light: Not Trapped green, a sprung trap &H80, the guess black (MONSMOD.BAS:3643-3646, 4172)",
                  $"{Ink("Green")} {Ink("Maroon")} {Ink("Text")}", "#ff008000 #ff800000 #ff000000");
            Check("the Messages list in Light the window's grey, as the original's is (it was a lighter grey)",
                  Ink("Well"), Ink("Window"));
            // the Stats page's state words (lblSpecial): the form's ForeColor &H000000FF&, bright red, on
            // its &H00C0C0C0& (frmCharStat1, MORDOR.EXE RCDATA 8); they had been QBColor 4's maroon
            Check("Light: the state words the form's bright red, #FF0000 (they were the maroon #800000)", Ink("StateInk"), "#ffff0000");
            Theme("Dark");
            Check("Dark: the state words Dark's indian red, as they were", Ink("StateInk"), "#ffcd5c5c");
            Theme("Light");

            // the About box: the version line cut off at the form's 180 twips, as in the original, and not bold
            var about = (Window)typeof(AboutWindow).GetField("_win", Any)!.GetValue(new AboutWindow(game))!;
            about.Show(); Pump();
            var version = about.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text?.StartsWith("Version 1.1") == true);
            Check("the About box's version line 12 pixels high (180 twips) and plain", (version?.Height, version?.FontWeight), ((double?)12, (FontWeight?)FontWeight.Normal));
            about.Close(); Pump();

            // a text box with a ground of its own keeps it under the pointer and with the keyboard (the
            // Library's yellow page turned the theme's white when moused over)
            {
                var yellow = (IBrush)Role("List");
                var box = new TextBox { Background = yellow, Width = 200, Height = 60, Text = "The Library" };
                var holder = new Window { Width = 300, Height = 120, Content = box };
                holder.Show(); Pump();
                Color? Ground() => box.GetVisualDescendants().OfType<Border>()
                    .Select(b => (b.Background as ISolidColorBrush)?.Color).FirstOrDefault(c => c is { A: > 0 });
                var centre = box.TranslatePoint(new Point(100, 30), holder)!.Value;
                holder.MouseMove(centre); Pump();
                var hovered = Ground();
                box.Focus(); Pump();
                var focused = Ground();
                Check("a text box's own yellow kept under the pointer and with the keyboard (it turned white)",
                      (hovered, focused), ((Color?)((ISolidColorBrush)yellow).Color, (Color?)((ISolidColorBrush)yellow).Color));
                holder.Close(); Pump();
            }

            // Create a Character: no slack round its columns (it was a fixed 940 by 560, the guilds' box a
            // fixed 330 tall), and the points left a number in a light field with the words beside it,
            // as the original's form (it was yellow on a dark block)
            {
                var create = new CharacterCreationWindow(game);
                create.Show(); Pump();
                var stats = Field<Border>(create, "StatsBox");
                var guilds = Field<Border>(create, "GuildsBox");
                var pool = Field<Border>(create, "PoolBox");
                var poolText = Field<TextBlock>(create, "PoolText");
                Check("Create a Character: sized to its contents, the guilds' box as tall as the stats'",
                      (create.SizeToContent, Math.Abs(guilds.Bounds.Height - stats.Bounds.Height) < 1), (SizeToContent.WidthAndHeight, true));
                Check("Create a Character: the points left a number alone, in the field's ground",
                      (int.TryParse(poolText.Text, out _), ReferenceEquals(pool.Background, Role("Field"))), (true, true));
                // the form's own captions, &Save Character, &Tutorial, &Exit (MORDOR.EXE): Exit's letter is
                // the E (it had been E&xit, Alt+X); in Light every access letter shows, as on Windows 3.1
                string Caption(string name) => Field<Button>(create, name).Content?.ToString() ?? "";
                Check("Create a Character: the buttons' access letters the form's own",
                      $"{Caption("CreateBtn")}|{Caption("TutorialBtn")}|{Caption("ExitBtn")}", "_Save Character|_Tutorial|_Exit");
                var exitText = Field<Button>(create, "ExitBtn").GetVisualDescendants().OfType<Avalonia.Controls.Primitives.AccessText>().FirstOrDefault();
                Check("Light: an access letter underlined all the time, not only with Alt held", exitText?.ShowAccessKey, (bool?)true);
                // the figures in navy in their panels (pnlPanel), the name field the lists' pale yellow, the
                // combo boxes' choices navy, and the form's MS Sans Serif, not the port's monospace
                var statRows = Field<StackPanel>(create, "StatRows");
                var figure = statRows.GetVisualDescendants().OfType<Border>().Select(b => b.Child).OfType<TextBlock>().FirstOrDefault();
                Check("Create a Character: the figures navy, the name field the lists' yellow, the choices navy",
                      (ReferenceEquals(figure?.Foreground, Role("Navy")), ReferenceEquals(Field<TextBox>(create, "NameBox").Background, Role("List")),
                       ReferenceEquals(Field<ComboBox>(create, "RaceBox").Foreground, Role("Navy"))), (true, true, true));
                Check("Create a Character: no monospace face left in it",
                      create.GetVisualDescendants().OfType<TextBlock>().Any(t => t.FontFamily.Name.Contains("Cascadia") || t.FontFamily.Name.Contains("Consolas")), false);
                create.Close(); Pump();
            }

            // ---- the town, twice over: leaving it and coming back must not lose its tiled ground ----
            CityWindow? town = null;
            for (int round = 1; round <= 2; round++)
            {
                Exception? thrown = null;
                try { town?.Close(); Pump(); town = new CityWindow(new Party(first), game) { Width = 1400, Height = 1000 }; town.Show(); Pump(); }
                catch (Exception ex) { thrown = ex.InnerException ?? ex; }
                var ground = town == null ? null : Field<Border>(town, "Backdrop").Background as ImageBrush;
                Check($"the town opened a {(round == 1 ? "first" : "second")} time, its tiled ground whole (the PR freed the shared picture)",
                      (thrown?.Message, ground?.Source is Avalonia.Media.Imaging.Bitmap b && b.Size.Width > 0), ((string?)null, true));
            }
            town!.Close(); Pump();

            // ---- a party of two in town ----
            var party = new Party(first, second);
            town = new CityWindow(party, game) { Width = 1400, Height = 1000 };
            town.Show(); Pump();
            var rows = Field<StackPanel>(town, "PartyRows");
            var partyBox = Field<Control>(town, "PartyBox");
            Check("the party's rows a plain stack in a frame, no list of its own (a ListBox kept a selection apart from the game's)",
                  (rows.Children.Count, partyBox.GetVisualDescendants().OfType<ListBox>().Count()), (2, 0));
            // the rows' box runs down to just above the buttons, as the original's does (a wide gap was left)
            var frame = Field<Border>(town, "PartyRowsFrame");
            var switchTo = Field<Button>(town, "PartySwitchBtn");
            double gap = switchTo.Bounds.Top - frame.Bounds.Bottom;
            Check($"the party's rows box ends a few pixels above the buttons (box {frame.Bounds.Top:0.#}-{frame.Bounds.Bottom:0.#}, buttons at {switchTo.Bounds.Top:0.#})",
                  gap is >= 1 and <= 4, true);
            if (rows.Children.Count == 2)
            {
                rows.Children[1].RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, null!));
                Pump();
                Check("... the second member clicked: the game's selection is the second, and stays so", party.SelectedIndex, 1);
            }

            // Party Options: no slack round its list and frame, and its frame's caption not the form's blue
            {
                Window? opts = null;
                var sub = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { if (w != town) opts = w; });
                typeof(App).Assembly.GetType("Mordor.App.PartyOptionsDialog")!.GetMethod("Show", Any)!
                    .Invoke(null, new object[] { town, party, (Action)(() => { }) });
                for (int i = 0; i < 3; i++) Pump();
                sub.Dispose();
                if (opts == null) Check("Party Options opened", false, true);
                else
                {
                    var caption = opts.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "Defaults");
                    var list = opts.GetVisualDescendants().OfType<ListBox>().First();
                    var frameBox = caption?.GetVisualAncestors().OfType<Border>().First(b => b.Child is StackPanel);
                    double slack = opts.Bounds.Width - list.Bounds.Width - (frameBox?.Bounds.Width ?? 0);
                    Check($"Party Options: the window no wider than its list and frame and their margins ({slack:0} pixels over)", slack <= 24, true);
                    Check("Party Options: \"Defaults\" in the ink, bold, not the form's blue",
                          ((caption?.Foreground as ISolidColorBrush)?.Color == Role("Text").Color, caption?.FontWeight),
                          (true, (FontWeight?)FontWeight.Bold));
                    opts.Close(); Pump();
                }
            }

            // the Objects & Spells' spell list and Spell box the same pale yellow as the party's rows, as
            // the original's are (they were a lighter grey); the Messages list the window's grey is above
            {
                Color? Of(IBrush? b) => (b as ISolidColorBrush)?.Color;
                var rowsGround = Of(Field<Border>(town, "PartyRowsFrame").Background);
                Check("Light: the spell list and the Spell box the party rows' pale yellow",
                      (Of(Field<ListBox>(town, "SpellList").Background) == rowsGround, Of(Field<TextBox>(town, "SpellText").Background) == rowsGround,
                       rowsGround == Color.Parse("#FFFFE0")), (true, true, true));
            }

            // the toolbar in Light: the party rows' pale yellow, the Gold and experience lines black and
            // against its right edge, as the original's picToolbar (it was white, the lines after the buttons)
            {
                var tools = Field<Toolbar>(town, "Tools");
                var readouts = Field<StackPanel>(tools, "_readouts");
                var lines = new[] { Field<TextBlock>(tools, "_gold"), Field<TextBlock>(tools, "_experience") };
                double right = readouts.TranslatePoint(new Point(readouts.Bounds.Width, 0), tools)?.X ?? 0;
                Check("Light: the toolbar's ground the party rows' pale yellow",
                      (tools.Background as ISolidColorBrush)?.Color, (Color?)Color.Parse("#FFFFE0"));
                Check("Light: the Gold and experience lines black",
                      lines.All(l => (l.Foreground as ISolidColorBrush)?.Color == Colors.Black), true);
                Check($"the Gold and experience lines against the toolbar's right edge ({tools.Bounds.Width - right:0} pixels in)",
                      tools.Bounds.Width - right < 16 && lines.All(l => l.TextAlignment == TextAlignment.Right), true);
            }

            // a list's chosen row in Light: white on navy, as the original's are (the theme's pale blue under black)
            {
                var anyList = town.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.ItemCount > 0 && l.IsEffectivelyVisible);
                if (anyList == null) Check("a list with rows in the town", false, true);
                else
                {
                    anyList.SelectedIndex = 0; Pump();
                    var mark = (anyList.ContainerFromIndex(0) as Control)?.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                        .FirstOrDefault(p => p.Name == "PART_ContentPresenter");
                    Check($"Light: {anyList.Name}'s chosen row white on navy",
                          ((mark?.Background as ISolidColorBrush)?.Color, (mark?.Foreground as ISolidColorBrush)?.Color),
                          ((Color?)Color.Parse("#000080"), (Color?)Colors.White));
                }
            }

            // the town's captions: each one piece of text, its access letter the key Form_KeyDown takes
            var captions = Field<Canvas>(town, "CityPanel").Children.OfType<Button>().Where(b => b.Classes.Contains("caption"))
                .Select(b => b.Content is AccessCaption c ? c.Text.Insert(c.AccessIndex, "&") : "not one piece").ToList();
            Check("the town's captions one piece each, the letter underlined the original's (\"Ex it\" when in pieces)",
                  string.Join("|", captions), "General &Store|&Morgue|&Guilds|&Dungeon|&Confinement|See&r|&Bank|E&xit");

            // ---- the character window ----
            var panel = Field<CharacterPanel>(town, "_charPanel");
            var tabs = Field<TabControl>(panel, "_tabs");
            double Top(string header) => tabs.GetVisualDescendants().OfType<TabItem>().First(t => Equals(t.Header, header)).Bounds.Y;
            void Show(CharTab t) { typeof(CityWindow).GetMethod("SetInfoTab", Any)!.Invoke(town, new object[] { t }); Pump(); }
            Show(CharTab.Stats);
            Check("Stats chosen: its row next to the page, Look's above it (the original's tab moves the chosen row down)", Top("Look") < Top("Stats"), true);
            Show(CharTab.Look);
            Check("Look chosen: its row comes down next to the page", Top("Look") > Top("Stats"), true);

            static bool LightAboveLeft(BoxShadows s, string colour) =>
                Enumerable.Range(0, s.Count).Any(i => s[i].IsInset && s[i].OffsetX > 0 && s[i].OffsetY > 0 && s[i].Color == Color.Parse(colour));
            // a state word on the Stats page (lblSpecial): Protected, set in town, as the gate ends the
            // spells and cures the ailments (RestInCity)
            foreach (var m in party.Members) m.Protection = 5;
            panel.Show(party.Members[party.SelectedIndex]);
            Show(CharTab.Stats);
            var stateWord = Field<Canvas>(panel, "_stats").Children.OfType<TextBlock>().FirstOrDefault(t => t.Text == "Protected");
            Check("Light: the Stats page draws its state word in the state ink, #FF0000",
                  (ReferenceEquals(stateWord?.Foreground, Role("StateInk")), (stateWord?.Foreground as ISolidColorBrush)?.Color),
                  (true, (Color?)Color.Parse("#FF0000")));
            var statBoxes = Field<Canvas>(panel, "_stats").Children.OfType<Border>().ToList();
            Check("Light: the Stats page's two boxes stand out, light above and left (they were drawn set in)",
                  (statBoxes.Count, statBoxes.All(b => LightAboveLeft(b.BoxShadow, "#FFFFFF"))), (2, true));
            var tabGround = tabs.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "PART_LayoutRoot").ToList();
            Check("Light: every tab bevelled, light above and left", tabGround.Count > 0 && tabGround.All(b => LightAboveLeft(b.BoxShadow, "#FFFFFF")), true);

            // Buffers: labels, not buttons, and a click on one still fires it
            Show(CharTab.Buffers);
            var buffers = Field<StackPanel>(panel, "_buffers");
            int fired = -1;
            Action<int> onFire = i => fired = i;
            typeof(CharacterPanel).GetEvent("BufferFired")!.AddEventHandler(panel, onFire);
            buffers.Children[2].RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, null!));
            typeof(CharacterPanel).GetEvent("BufferFired")!.RemoveEventHandler(panel, onFire);
            Check("Buffers: each line at least lblBuffer's 225 twips (15 pixels), with the lists' padding (they sat tight)",
                  buffers.Children.OfType<Border>().All(b => b.Height >= 15 && b.Height > b.Child!.DesiredSize.Height + 1), true);
            Check("Buffers: ten lines, no buttons, the third clicked fires buffer 3 (they were buttons)",
                  (buffers.Children.Count, buffers.GetVisualDescendants().OfType<Button>().Count(), fired), (10, 0, 2));

            // ---- the dungeon ----
            typeof(CityWindow).GetMethod("EnterDungeon", Any)!.Invoke(town, new object?[] { null, new RoutedEventArgs() });
            for (int i = 0; i < 5; i++) Pump();
            if (Field<DungeonWindow?>(town, "_below") is DungeonWindow down)
            {
                var pos = Field<TextBlock>(down, "PosText");
                Check("the position a label, as lblRoom is: no button round it, no focus (a button cut its text and took keys)",
                      (pos.GetVisualAncestors().OfType<Button>().Any(), pos.Focusable), (false, false));
                var downPanel = Field<CharacterPanel>(down, "_charPanel");
                typeof(DungeonWindow).GetMethod("SetInfoTab", Any)!.Invoke(down, new object[] { CharTab.Stats });
                pos.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, null!));
                Pump();
                Check("... a click on it opens Look, as lblRoom_Click's F2 does (FRMDUNM.BAS:377-379)", downPanel.Selected, CharTab.Look);
                // picMonGroups only while a group lives (MONSMOD.BAS:4324-4330): an empty yellow box had
                // stayed up after the last monster fell with a chest still shut
                var monsterList = Field<ItemsControl>(down, "MonsterList");
                monsterList.ItemsSource = new List<string>(); Pump();
                bool emptyShown = monsterList.IsVisible;
                monsterList.ItemsSource = new List<string> { "1. Kobold" }; Pump();
                bool rowsShown = monsterList.IsVisible;
                monsterList.ItemsSource = null; Pump();
                Check("the monster list hidden with no living group, shown with one", (emptyShown, rowsShown, monsterList.IsVisible), (false, true, false));
                // picMonsters itself is up with no monster on the square (FRMDUNM.BAS:273), hidden only on
                // the dead screen (MAINMOD2.BAS:3056): the port had hidden the whole panel, and the
                // owner's screenshot of the original showed its empty, set-in box
                var combat = Field<Border>(down, "CombatPanel");
                Check("the encounter panel (picMonsters) up with no monster on the square, its monster list not",
                      (combat.IsVisible, monsterList.IsVisible), (true, false));
                typeof(DungeonWindow).GetMethod("HideMonsters", Any)!.Invoke(down, null); Pump();
                Check("... and still up once the monsters are gone (HideMonsters empties it, the dead screen alone hides it)",
                      combat.IsVisible, true);
                Check("Light: the chest's box (picTrap) the lists' pale yellow, as the original's (it was the form's grey)",
                      (Field<Border>(down, "ChestPanel").Background as ISolidColorBrush)?.Color, (Color?)Color.Parse("#FFFFE0"));
                Check("Light: the view and the position set in, shadow above and left",
                      (LightAboveLeft(Field<Border>(down, "ViewFrame").BoxShadow, "#808080"), LightAboveLeft(Field<Border>(down, "PosFrame").BoxShadow, "#808080")), (true, true));
                down.Close(); Pump();
            }
            else Check("the dungeon came up", false, true);
            town.Close(); Pump();
        }
        catch (Exception ex) { Check("the theme checks could be driven", (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message, ""); }
        finally
        {
            try { choiceField.SetValue(null, themeWas); theme.GetMethod("Apply", Any)!.Invoke(null, null); Pump(); } catch { }
            TitleWindow.GameDir = gameDirWas;
            ModHost.Active = hostWas;
            foreach (var (k, v) in envWas) Environment.SetEnvironmentVariable(k, v);
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new InvalidOperationException("Refusing cleanup outside the theme test folder.");
            try { Directory.Delete(scratch, true); } catch { }
        }
        return _failed;
    }
}
