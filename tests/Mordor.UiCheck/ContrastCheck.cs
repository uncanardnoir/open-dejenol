using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// Can the text be read? Every window the game opens, built in the Light theme and again in the
/// Dark, and every piece of text in it - a TextBlock, a text box's text, a town caption - set
/// against what is drawn behind it: its own colour on the first solid ground under it, the
/// see-through grounds above that mixed in. Text whose contrast with that ground is under
/// <see cref="Least"/> (WCAG's figure for large text and the parts of a user interface) fails.
///
/// The themes' first builds left pale text on the Light grey (the Options window, the Character
/// List, the town's captions) and dark on dark in the Help: each caught only by playing. This
/// catches the kind without anyone looking. What it cannot see: text on a picture (an image
/// ground is skipped), text covered by another window, and a colour set after the window is up.
/// Disabled controls are left out: their grey is meant to read as greyed.
///
/// Each window is also put through the layout checks (<see cref="LayoutCheck"/>): a character no
/// font here can draw, text cut short by its control, a control past its window's edge. With
/// MORDOR_SNAPSHOT_DIR set, each window is saved as drawn, &lt;Theme&gt;_&lt;name&gt;.png, and checks.json
/// beside the pictures lists each one's failures for the gallery (<see cref="PictureTools"/>). The
/// pictures are the same from run to run on one machine: the clock on the Char tab and the Automap's
/// blinking arrow are held still (<see cref="Steady"/>), and the dungeon is entered on the fixture's
/// entrance room, which no monster is drawn into (FixtureGame, spawn mask 0).
/// </summary>
static class ContrastCheck
{
    /// <summary>The least contrast a piece of text may have with its ground.</summary>
    const double Least = 3.0;

    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static int Run(string game)
    {
        Console.WriteLine($"\ncan the text be read: every window, Light and Dark, each text at least {Least}:1 against its ground:");
        int failed = 0;
        void Pump()
        {
            for (int i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        }
        _checks.Clear();
        _weights.Clear();
        // the layout checks shown windows made to fail them first: a check that cannot fail proves nothing
        failed += LayoutCheck.SelfTest(Pump);
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        var items = MdrLoader.LoadItems(Path.Combine(game, "MDATA3.MDR"));

        var theme = typeof(App).Assembly.GetType("Mordor.App.ThemeSelection")!;
        var choice = theme.GetNestedType("Choice", Any)!;
        var choiceField = theme.GetField("_choice", Any)!;
        object themeWas = choiceField.GetValue(null)!;
        // taken straight, not chosen: choosing writes settings.ini
        void Theme(string c)
        {
            choiceField.SetValue(null, Enum.Parse(choice, c));
            theme.GetMethod("Apply", Any)!.Invoke(null, null);
            Pump();
        }

        string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-contrast-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var envWas = new[] { Saves.EnvDir, GameFolder.EnvIni }.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        var hostWas = ModHost.Active;
        string gameDirWas = TitleWindow.GameDir;
        try
        {
            Environment.SetEnvironmentVariable(Saves.EnvDir, scratch);
            Environment.SetEnvironmentVariable(GameFolder.EnvIni, Path.Combine(scratch, "launch.ini"));
            ModHost.Active = new ModHost(new ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
            var hero = new Character
            {
                Name = "Contrast", Race = RaceData.All[0].Name, CurHits = 100, MaxHits = 100, GoldOnHand = 1_000_000,
                Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 20, Experience = 2_000_000 } },
            };
            int slot = 1;
            foreach (var it in items.Where(i => i.Id > 0 && i.Cursed == 0 && !ItemUse.IsGuildIssued(i)).Take(6))
                hero.Pack[slot++] = new InventorySlot { ItemId = it.Id, Known = IdLevel.All };
            hero.Save(Path.Combine(scratch, Character.SafeFileName(hero.Name) + ".json"));
            TitleWindow.GameDir = game;
            ModHost? qolHost = null;   // the Quality of Life mod switched on, for the Party window's pass
            ModHost? windowsHost = null;   // Kaizen and Auto-combat switched on, for their windows' pass

            foreach (string look in new[] { "Light", "Dark" })
            {
                Theme(look);
                // the picture's file name, and the window's failures under it in checks.json
                string Picture(string what) => string.Concat($"{look}-{what}".Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')) + ".png";
                void Error(string what, string message)
                {
                    Console.WriteLine($"  FAIL  {look}: {what}: {message}");
                    failed++;
                    Record(Picture(what), "error", message, null);
                }
                void Scan(string what, Visual root)
                {
                    Pump();
                    // what changes from run to run and is not the window's to show: the clock (Steady)
                    Steady(root, Pump);
                    var lines = LowContrast(root).GroupBy(l => l).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
                        .Select(l => (Line: l, Known: LayoutCheck.KnownContrast(what, look, l))).ToList();
                    // text under 3:1 that is the original's own colours, kept (LayoutCheck.KnownFindings), is known and does not fail
                    var low = lines.Where(l => l.Known == null).Select(l => l.Line).ToList();
                    var layout = LayoutCheck.Find(root).Distinct().Select(f => (Finding: f, Known: LayoutCheck.KnownFor(what, f))).ToList();
                    var fresh = layout.Where(l => l.Known == null).ToList();
                    bool ok = low.Count == 0 && fresh.Count == 0;
                    var why = new List<string>();
                    if (low.Count > 0) why.Add($"{low.Count} hard to read");
                    if (fresh.Count > 0) why.Add($"{fresh.Count} layout finding{(fresh.Count == 1 ? "" : "s")}");
                    Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {look}: {what}" + (ok ? "" : $" - {string.Join(", ", why)}:"));
                    foreach (var l in low.Take(12)) Console.WriteLine("          " + l);
                    if (low.Count > 12) Console.WriteLine($"          ... and {low.Count - 12} more");
                    foreach (var (f, _) in fresh.Take(12)) Console.WriteLine("          " + f);
                    if (fresh.Count > 12) Console.WriteLine($"          ... and {fresh.Count - 12} more");
                    foreach (var (l, k) in lines.Where(l => l.Known != null)) Console.WriteLine($"          known  contrast: {l} - {k!.Reason}");
                    foreach (var (f, k) in layout.Where(l => l.Known != null)) Console.WriteLine($"          known  {f} - {k!.Reason}");
                    if (!ok) failed++;
                    string picture = Picture(what);
                    Record(picture, null, null, null);
                    _weights[$"{look}: {what}"] = Reach(root);
                    foreach (var (l, k) in lines) Record(picture, "contrast", l, k?.Reason);
                    foreach (var (f, k) in layout) Record(picture, f.Kind, $"{f.Control}: {f.Numbers}", k?.Reason);
                    // MORDOR_SNAPSHOT_DIR: the window as drawn, a PNG apiece, for a before-and-after
                    // comparison of a change that should alter nothing (tools/snapshot-diff, Mordor.UiCheck --compare)
                    if (Environment.GetEnvironmentVariable("MORDOR_SNAPSHOT_DIR") is { Length: > 0 } shots
                        && (root as TopLevel ?? TopLevel.GetTopLevel(root)) is { } top)
                    {
                        Directory.CreateDirectory(shots);
                        top.CaptureRenderedFrame()?.Save(Path.Combine(shots, picture), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    }
                }
                // the mods' symbols drawn as shapes (ModGlyphs): at least `least` of the symbol shown
                // under the root, and no U+2713 or U+2192 in its text
                void Symbols(string what, Visual root, ModSymbol symbol, int least)
                {
                    int shapes = root.GetVisualDescendants().OfType<ModGlyph>().Count(g => g.Symbol == symbol && g.IsEffectivelyVisible);
                    var chars = LayoutCheck.VisibleText(root).Where(s => s.Contains('✓') || s.Contains('→')).ToList();
                    bool ok = shapes >= least && chars.Count == 0;
                    Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {look}: {what}: {shapes} {symbol} shapes (at least {least}), "
                                      + (chars.Count == 0 ? "and no U+2713 or U+2192 character in its text" : $"and the characters in {chars.Count} texts: {string.Join("; ", chars.Take(3))}"));
                    if (ok) return;
                    failed++;
                    Record(Picture(what), "error", $"{shapes} {symbol} shapes, {chars.Count} texts with the character", null);
                }
                void Window(string what, Func<Window> make)
                {
                    Window? w = null;
                    try { w = make(); w.Show(); Scan(what, w); }
                    catch (Exception ex) { Error(what, $"could not be opened: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}"); }
                    finally { try { w?.Close(); Pump(); } catch { } }
                }

                Window("the main menu", () => new TitleWindow());
                Window("the Options window", () => new OptionsWindow(new GameOptions(), game));
                Window("the Mods window", () => new ModsWindow(atMainMenu: true));
                Window("the Help", () => new HelpWindow());
                Window("the About box", () => (Window)typeof(AboutWindow).GetField("_win", Any)!.GetValue(new AboutWindow(game))!);
                Window("the Credits", () => (Window)typeof(CreditsWindow).GetField("_win", Any)!.GetValue(new CreditsWindow())!);
                Window("the Ordering Info", () => (Window)typeof(OrderInfoWindow).GetField("_win", Any)!.GetValue(new OrderInfoWindow())!);
                Window("the Hall of Records", () => (Window)typeof(HallWindow).GetField("_win", Any)!.GetValue(new HallWindow(game))!);
                Window("the Exchange", () => (Window)typeof(ExchangeWindow).GetField("_win", Any)!.GetValue(
                    new ExchangeWindow(hero, hero, id => items.FirstOrDefault(i => i.Id == id), _ => { }))!);
                Window("Create a Character", () => new CharacterCreationWindow(game));
                Window("the first-run window", () => new GameFolderWindow(GameFolder.Resolve()));

                // the main menu's own boxes, opened as the player opens them: the window that comes up
                // is caught as it opens (Character List's pale heading; Load Character's selected name,
                // black on the theme's blue)
                void Popup(string what, Action<TitleWindow> open)
                {
                    TitleWindow? menu = null;
                    Window? box = null;
                    void Caught(object? s, RoutedEventArgs e) { if (s is Window w && w != menu) box = w; }
                    var sub = Avalonia.Controls.Window.WindowOpenedEvent.AddClassHandler<Window>((w, e) => Caught(w, e));
                    try
                    {
                        menu = new TitleWindow();
                        menu.Show(); Pump();
                        open(menu);
                        for (int i = 0; i < 3; i++) Pump();
                        if (box == null) Error(what, "did not open");
                        else Scan(what, box);
                    }
                    catch (Exception ex) { Error(what, $"could not be opened: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}"); }
                    finally { sub.Dispose(); try { box?.Close(); menu?.Close(); Pump(); } catch { } }
                }
                Popup("the Character List", m => typeof(TitleWindow).GetMethod("OnCharacterList", Any)!.Invoke(m, new object?[] { null, new RoutedEventArgs() }));
                Popup("the Load Character box", m => _ = NamePicker.Ask(m, "Load Character", "Load which character?", new[] { hero.Name, "Another" }, "Nobody"));

                // the town, each tab of the character window, each service, then the dungeon
                CityWindow? town = null;
                try
                {
                    town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
                    town.Show();
                    // a state word on the Stats page (lblSpecial), so its red is weighed in each theme: set
                    // once in town, as the gate ends the spells and cures the ailments (RestInCity)
                    hero.Protection = 5;
                    void Tab(Type owner, object w, CharTab t) => owner.GetMethod("SetInfoTab", Any)!.Invoke(w, new object[] { t });
                    foreach (CharTab t in Enum.GetValues<CharTab>())
                    {
                        Tab(typeof(CityWindow), town, t);
                        Scan($"the town, the character window's {t} tab", town);
                    }
                    foreach (string service in new[] { "ShowStore", "ShowGuild", "ShowBank", "ShowMorgue", "ShowSeer", "ShowConfinement" })
                    {
                        try
                        {
                            typeof(CityWindow).GetMethod(service, Any)!.Invoke(town, new object?[] { null, new RoutedEventArgs() });
                            Scan($"the town, {service[4..]}", town);
                        }
                        catch (Exception ex) { Error($"the town, {service[4..]}", $"could not be opened: {(ex.InnerException ?? ex).Message}"); }
                    }
                    typeof(CityWindow).GetMethod("EnterDungeon", Any)!.Invoke(town, new object?[] { null, new RoutedEventArgs() });
                    for (int i = 0; i < 5; i++) Pump();
                    if (typeof(CityWindow).GetField("_below", Any)!.GetValue(town) is DungeonWindow down)
                    {
                        // the Automap's arrow blinks on a 500 ms timer (timBlinker): held on show, as
                        // the window checks above hold it, so every picture has it
                        if (down.Automap is AutomapWindow map)
                        {
                            ((DispatcherTimer)typeof(AutomapWindow).GetField("_blinker", Any)!.GetValue(map)!).Interval = TimeSpan.FromDays(1);
                            typeof(AutomapWindow).GetMethod("ShowCursor", Any)!.Invoke(map, null);
                            Pump();
                        }
                        foreach (CharTab t in Enum.GetValues<CharTab>())
                        {
                            Tab(typeof(DungeonWindow), down, t);
                            Scan($"the dungeon, the character window's {t} tab", town);
                        }
                        down.Close(); Pump();
                    }
                    else Error("the dungeon", "did not come up");
                }
                catch (Exception ex) { Error("the town", $"could not be driven: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}"); }
                finally { try { town?.Close(); Pump(); } catch { } }

                // the Party window with the Quality of Life mod's columns on (the pass above runs with
                // the mods off, and its HP/SP and Next columns had never been read): four members, one
                // in each state of the experience bar - short of the level a little way and nearly
                // there, the level earned and on the way to the pin, pinned - with quest marks, so the
                // figure and the mark are weighed over the bar's fill and over its empty part
                const string qolPass = "the town, the Party window with the Quality of Life columns";
                CityWindow? qolTown = null;
                var plainHost = ModHost.Active;
                try
                {
                    ModHost.Active = qolHost ??= QolHost(scratch);
                    var monster = MdrLoader.LoadMonsters(Path.Combine(game, "MDATA5.MDR")).First(m => m.Id > 0).Id;
                    Character Member(string name, double along, bool stage2, bool pinned, int hits, int spells, int quest, bool done)
                    {
                        var c = new Character
                        {
                            Name = name, Race = RaceData.All[0].Name, CurHits = hits, MaxHits = 100, CurSpellPoints = spells, MaxSpellPoints = spells > 0 ? 100 : 0,
                            Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 20, QuestType = (short)quest, QuestTarget = quest == 1 ? monster : (short)0, QuestDone = done } },
                        };
                        long from = c.ExpForThisLevel(), next = c.ExpForNextLevel(), cap = c.ExperienceCap();
                        c.Guild.Experience = pinned ? cap : stage2 ? next + (long)((cap - next) * along) : from + (long)((next - from) * along);
                        return c;
                    }
                    var party = new Party(
                        Member("Nearly", 0.9, false, false, 100, 100, 1, false),
                        Member("Early", 0.1, false, false, 40, 0, 1, false),
                        Member("Ready", 0.5, true, false, 10, 30, 1, true),
                        Member("Topped", 0, true, true, 70, 80, 1, false));
                    qolTown = new CityWindow(party, game) { Width = 1400, Height = 1000 };
                    qolTown.Show(); Pump();
                    var header = qolTown.FindControl<Grid>("PartyHeader")!;
                    string columns = string.Join(",", header.Children.OfType<TextBlock>().Where(t => Equals(t.Tag, "mod-party-column")).Select(t => t.Text));
                    if (columns != "HP/SP,Next") Error(qolPass, $"the mod's columns are not up: [{columns}]");
                    else
                    {
                        Scan(qolPass, qolTown);
                        // the done mark a shape, not the character (ModGlyphs): one member's quest is done
                        Symbols(qolPass, qolTown, ModSymbol.Check, 1);
                    }
                    // a change of theme with the window up recolours the bar and its figures: they hold
                    // the game's bar roles, which the theme recolours in place (nothing redraws them);
                    // the tick too, its colour as drawn
                    if (look == "Light")
                    {
                        var rows = qolTown.FindControl<StackPanel>("PartyRows")!;
                        var tick = rows.GetVisualDescendants().OfType<ModGlyph>().FirstOrDefault(g => g.Symbol == ModSymbol.Check);
                        string Inks() => string.Join(" ", new[] { "Pinned", "!" }.Select(t =>
                            rows.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(b => b.Text == t)?.Foreground is ISolidColorBrush s ? Hex(s.Color) : "none")
                            .Append(tick?.Foreground is ISolidColorBrush k ? Hex(k.Color) : "none"));
                        string Fills() => string.Join(" ", rows.GetVisualDescendants().OfType<Border>()
                            .Select(b => (b.Background as ISolidColorBrush)?.Color).OfType<Color>()
                            .Where(c => c == Palette.ColourOf(ThemeKeys.XpPinned, "Light") || c == Palette.ColourOf(ThemeKeys.XpPinned, "Dark"))
                            .Select(c => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}").Distinct());
                        // the tick's strongest pixel of the theme's colour: how far the nearest is from it
                        int TickInk(string theme)
                        {
                            if (tick == null) return 999;
                            var want = Palette.ColourOf(ThemeKeys.QuestDone, theme) ?? Colors.Magenta;
                            return LayoutCheck.PixelsOver(LayoutCheck.Frame(qolTown), qolTown, tick)
                                .Select(c => Math.Max(Math.Abs(c.R - want.R), Math.Max(Math.Abs(c.G - want.G), Math.Abs(c.B - want.B)))).DefaultIfEmpty(999).Min();
                        }
                        string before = $"{Inks()} {Fills()}";
                        int lightTick = TickInk("Light");
                        Theme("Dark");
                        string after = $"{Inks()} {Fills()}";
                        int darkTick = TickInk("Dark");
                        Theme("Light");
                        bool followed = before == "#5A005A #5C4400 #155020 #FFE178D2" && after == "#DC8CD7 #F0BE32 #5AD264 #80CD46B9";
                        Console.WriteLine($"  {(followed ? "ok  " : "FAIL")}  a switch from Light to Dark with the Party window up recolours the QoL's Pinned, its mark, its tick and its bar ({before} -> {after})");
                        if (!followed) failed++;
                        bool drawn = lightTick <= TickTolerance && darkTick <= TickTolerance;
                        Console.WriteLine($"  {(drawn ? "ok  " : "FAIL")}  ... and the tick is drawn in each theme's colour (its nearest pixel {lightTick} from Light's, {darkTick} from Dark's)");
                        if (!drawn) failed++;
                    }
                }
                catch (Exception ex) { Error(qolPass, $"could not be driven: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}"); }
                finally { try { qolTown?.Close(); Pump(); } catch { } ModHost.Active = plainHost; }

                // the Kaizen and Auto-combat windows, each opened from the town's toolbar as the player
                // opens it, a child of the town's frame: Kaizen with a buff list (a row with a target, one
                // whose target is not in the party, one asking nobody), Auto-combat with two rules
                CityWindow? modTown = null;
                try
                {
                    ModHost.Active = windowsHost ??= WindowsHost(scratch, game, hero.Name);
                    modTown = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
                    modTown.Show(); Pump();
                    var icons = (Panel)typeof(Toolbar).GetField("_icons", Any)!.GetValue(modTown.FindControl<Toolbar>("Tools"))!;
                    Avalonia.Controls.Primitives.ToggleButton Tool(string title) =>
                        icons.Children.OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(b => Equals(b.Content, title));
                    void Press(string title) { Tool(title).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
                    var frameArea = modTown.FindControl<MdiHost>("Host")!;
                    foreach (var (title, what) in new[] { ("Kaizen", KaizenPass), ("Auto-combat", AutoCombatPass) })
                    {
                        try
                        {
                            // one an earlier check left open (the game keeps a mod's window open from
                            // frame to frame, and brings it into this one) is closed first, so the window
                            // pictured is built from this pass's mods
                            if (Tool(title).IsChecked == true || frameArea.Windows.Any(c => c.Title == title && c.IsVisible)) Press(title);
                            Press(title);
                            if (frameArea.Windows.FirstOrDefault(c => c.Title == title && c.IsVisible) is null) { Error(what, "did not open"); continue; }
                            Scan(what, modTown);
                            Symbols(what, modTown, ModSymbol.RightArrow, 2);
                        }
                        catch (Exception ex) { Error(what, $"could not be driven: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}"); }
                        finally { try { if (frameArea.Windows.Any(c => c.Title == title && c.IsVisible)) Press(title); } catch { } }
                    }
                }
                catch (Exception ex) { Error(KaizenPass, $"could not be driven: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}"); }
                finally { try { modTown?.Close(); Pump(); } catch { } ModHost.Active = plainHost; }
            }
            qolHost?.Switch("QoL", false);
            windowsHost?.Switch("Kaizen", false);
            windowsHost?.Switch("AutoCombat", false);
        }
        finally
        {
            try { choiceField.SetValue(null, themeWas); theme.GetMethod("Apply", Any)!.Invoke(null, null); Pump(); } catch { }
            TitleWindow.GameDir = gameDirWas;
            ModHost.Active = hostWas;
            foreach (var (k, v) in envWas) Environment.SetEnvironmentVariable(k, v);
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new InvalidOperationException("Refusing cleanup outside the contrast test folder.");
            try { Directory.Delete(scratch, true); } catch { }
        }
        // the selection test above must have had text boxes to weigh, or it proved nothing
        failed += WeightReach();
        bool weighed = _selectionsWeighed > 0;
        Console.WriteLine($"  {(weighed ? "ok  " : "FAIL")}  text boxes' selected text weighed: {_selectionsWeighed}");
        if (!weighed) failed++;
        // checks.json beside the pictures: each picture's failures, for the gallery (Mordor.UiCheck --gallery)
        if (Environment.GetEnvironmentVariable("MORDOR_SNAPSHOT_DIR") is { Length: > 0 } dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, PictureTools.ChecksFile), PictureTools.ChecksJson(_checks));
        }
        return failed;
    }

    /// <summary>
    /// A mod host with the Quality of Life mod switched on, as the build ships it beside the game
    /// (src/Mods/ShipMods.targets), its settings in the pass's scratch folder: every feature at its
    /// default, which has the HP/SP bars, the Next column, its bar and the quest marks on.
    /// </summary>
    static ModHost QolHost(string scratch)
    {
        var host = new ModHost(new ModSettingsStore(Path.Combine(scratch, "qol-settings.ini")), _ => { });
        host.Discover(new[] { ModsFolder() });
        host.LoadEnabled();   // none is enabled
        host.Switch("QoL", true);
        if (!host.Live.Any(m => m.Id == "QoL")) throw new InvalidOperationException("the Quality of Life mod did not start");
        return host;
    }

    /// <summary>How far, in any channel, the QoL's tick may be from its colour where it is drawn most strongly: anti-aliasing at a slant.</summary>
    const int TickTolerance = 24;

    /// <summary>The bundled mods beside the build of the game (src/Mods/ShipMods.targets).</summary>
    static string ModsFolder()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "Mordor.sln"))) root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        string? mods = root == null ? null : new[] { "Release", "Debug" }
            .Select(c => Path.Combine(root, "src", "Mordor.App", "bin", c, "net10.0", "mods"))
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "QoL")));
        return mods ?? throw new DirectoryNotFoundException("no build of Mordor.App with its mods folder");
    }

    /// <summary>The Kaizen window's pass, and the Auto-combat window's.</summary>
    const string KaizenPass = "the town, the Kaizen window", AutoCombatPass = "the town, the Auto-combat window";

    /// <summary>
    /// A mod host with Kaizen and Auto-combat switched on, its settings and Kaizen's buff lists in a
    /// folder of their own under the pass's scratch folder, and something in each window to read:
    /// <paramref name="who"/>'s buff list - a spell that asks "Cast on Who?" aimed at
    /// <paramref name="who"/> (the arrow and the name), the same aimed at someone not in the party (the
    /// warning after it), and one that asks nobody ("(party)") - and two rules, one with conditions, a
    /// spell and a target, one with none. The spells are the game's own (MDATA2.MDR).
    /// </summary>
    static ModHost WindowsHost(string scratch, string game, string who)
    {
        string dir = Path.Combine(scratch, "mod-windows");
        Directory.CreateDirectory(dir);
        var spells = MdrLoader.LoadSpells(Path.Combine(game, "MDATA2.MDR")).Where(s => s.Name.Trim() != "").ToList();
        string aimed = spells.First(s => SpellThrown.WhoToAsk(s) != SpellThrown.Ask.None).Name.Trim();
        string party = spells.First(s => SpellThrown.WhoToAsk(s) == SpellThrown.Ask.None).Name.Trim();
        string Row(string name, string target) => $"{{ \"Kind\": \"Spell\", \"Name\": {System.Text.Json.JsonSerializer.Serialize(name)}, \"Target\": {System.Text.Json.JsonSerializer.Serialize(target)} }}";
        File.WriteAllText(Path.Combine(dir, "kaizen.json"),
            $"{{ \"Version\": 1, \"Characters\": {{ {System.Text.Json.JsonSerializer.Serialize(who)}: [ {Row(aimed, who)}, {Row(aimed, "Elsewhere")}, {Row(party, "")} ] }} }}");
        var store = new ModSettingsStore(Path.Combine(dir, "settings.ini"));
        // Who, PartyBelow, PartyAtLeast, SelfBelow, SpAtLeast, GroupsAtLeast, GroupsAtMost, Shown, Do, What, Targets
        store.Write("AutoCombat", "Rules", ModSettings.EncodeRows(new[]
        {
            new[] { "", "50", "", "", "10", "", "", "", "Cast", aimed, "Kobold" },
            new[] { who, "", "", "", "", "", "", "", "Fight", "", "" },
        }));
        var host = new ModHost(store, _ => { });
        host.Discover(new[] { ModsFolder() });
        host.LoadEnabled();   // none is enabled
        foreach (var id in new[] { "Kaizen", "AutoCombat" })
        {
            host.Switch(id, true);
            if (!host.Live.Any(m => m.Id == id)) throw new InvalidOperationException($"the {id} mod did not start");
        }
        return host;
    }

    /// <summary>Each picture taken, in the order taken, with its failures (contrast, layout, an error): checks.json.</summary>
    static readonly List<(string Picture, List<PictureTools.Failure> Failures)> _checks = new();

    /// <summary>Note a picture (no kind: just that it was taken), or a failure under it; a known one carries its reason.</summary>
    static void Record(string picture, string? kind, string? message, string? known)
    {
        var at = _checks.FindIndex(c => c.Picture == picture);
        if (at < 0) { _checks.Add((picture, new())); at = _checks.Count - 1; }
        if (kind != null) _checks[at].Failures.Add(new PictureTools.Failure(kind, message ?? "", known != null, known));
    }

    /// <summary>
    /// Holds still what would differ between two runs of the pass and is no part of the window: the
    /// clock on the character window's Char tab. Its Time and Date are the moment the page was drawn,
    /// and its Play Time the seconds the pass has run (the town and dungeon tick it); each is set to a
    /// fixed figure in the game's own format just before the picture, and set again should a redraw
    /// put the clock back. The game draws them; only the picture's copy is fixed.
    /// </summary>
    static void Steady(Visual root, Action pump)
    {
        var fixedAt = new DateTime(2000, 1, 1, 12, 0, 0);
        var want = new Dictionary<string, string>
        {
            ["Time:"] = fixedAt.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            ["Date:"] = fixedAt.ToString("d", System.Globalization.CultureInfo.CurrentCulture),
            ["Play Time:"] = 0f.ToString("###,###.00", System.Globalization.CultureInfo.CurrentCulture) + " Hrs",
        };
        // each heading's value: the next text to its right on its row of the page
        bool Set()
        {
            bool changed = false;
            foreach (var head in root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text != null && want.ContainsKey(t.Text)).ToList())
            {
                if (head.GetVisualParent() is not Canvas page) continue;
                double top = Canvas.GetTop(head), left = Canvas.GetLeft(head);
                var value = page.Children.OfType<TextBlock>()
                    .Where(t => t != head && Math.Abs(Canvas.GetTop(t) - top) < 1 && Canvas.GetLeft(t) > left)
                    .OrderBy(t => Canvas.GetLeft(t)).FirstOrDefault();
                if (value != null && value.Text != want[head.Text!]) { value.Text = want[head.Text!]; changed = true; }
            }
            return changed;
        }
        for (int i = 0; i < 4 && Set(); i++) pump();
    }

    /// <summary>What the weight check reached in a window: runs set bold and set plain, the bold captions in buttons among them, and the MDI windows' titles.</summary>
    record WeightReached(int Bold, int Plain, int BoldButtons, int Titles);

    static readonly Dictionary<string, WeightReached> _weights = new();

    static WeightReached Reach(Visual root)
    {
        var (bold, plain, controls) = LayoutCheck.Weighed(root);
        bool IsBold(Control c) => c.GetValue(TextElement.FontWeightProperty) >= FontWeight.SemiBold;
        var captions = root.GetVisualDescendants().OfType<MdiChild>()
            .Select(m => typeof(MdiChild).GetField("_caption", Any)?.GetValue(m)).OfType<Control>().ToHashSet();
        return new WeightReached(bold, plain,
            controls.Count(c => IsBold(c) && c.GetVisualAncestors().OfType<Button>().Any()),
            controls.Count(captions.Contains));
    }

    /// <summary>
    /// The weight check (LayoutCheck.WrongWeight) must have weighed the text it is there for, in each
    /// theme: the main menu's bold buttons, Create a Character's bold labels and plain figures, the
    /// Character List, the Hall of Records, the Options window's bold headings and plain text, and the
    /// MDI windows' bold titles in the town. Returns how many did not.
    /// </summary>
    static int WeightReach()
    {
        int failed = 0;
        var wanted = new (string Window, string What, Func<WeightReached, bool> Enough)[]
        {
            ("the main menu", "the buttons' bold captions", r => r.BoldButtons > 0),
            ("Create a Character", "bold labels and plain figures", r => r.Bold > 0 && r.Plain > 0),
            ("the Character List", "its text", r => r.Bold + r.Plain > 0),
            ("the Hall of Records", "its bold heading and plain rows", r => r.Bold > 0 && r.Plain > 0),
            ("the Options window", "bold headings and plain text", r => r.Bold > 0 && r.Plain > 0),
            ("the town, the character window's Stats tab", "the windows' bold titles", r => r.Titles > 0),
        };
        foreach (string look in new[] { "Light", "Dark" })
            foreach (var (window, what, enough) in wanted)
            {
                var r = _weights.GetValueOrDefault($"{look}: {window}");
                bool ok = r != null && enough(r);
                Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {look}: {window}: the weight check reached {what} ({(r == null ? "not scanned" : $"{r.Bold} bold runs, {r.Plain} plain, {r.BoldButtons} bold button captions, {r.Titles} titles")})");
                if (!ok) failed++;
            }
        return failed;
    }

    /// <summary>How many text boxes' selections were weighed: none would mean the check saw no text box at all.</summary>
    static int _selectionsWeighed;

    /// <summary>Each piece of text under <paramref name="root"/> whose contrast with its ground is under <see cref="Least"/>.</summary>
    internal static List<string> LowContrast(Visual root)
    {
        var found = new List<string>();
        foreach (var v in root.GetVisualDescendants())
        {
            string? text = v switch
            {
                TextBlock t => t.Inlines is { Count: > 0 } i ? i.Text : t.Text,
                TextPresenter p => p.Text,
                AccessCaption c => c.Text,
                _ => null,
            };
            if (v is not Control c0) continue;
            // a text box is weighed even empty: what it will show selected depends on its colours alone
            if (string.IsNullOrWhiteSpace(text) && v is not TextPresenter) continue;
            if (!c0.IsEffectivelyVisible || !c0.IsEffectivelyEnabled || c0.Bounds.Width <= 0 || c0.Bounds.Height <= 0) continue;
            if (c0.GetValue(TextElement.ForegroundProperty) is not ISolidColorBrush ink) continue;
            // the ground at the text's left end, its middle and its right end: a bar drawn under part
            // of it (the QoL's experience bar) is behind some of the letters and not the rest, and
            // the text must read over both; the worst of them is the text's ground
            var grounds = new[] { 0.05, 0.5, 0.95 }
                .Select(x => Ground(v, new Point(c0.Bounds.Width * x, c0.Bounds.Height / 2)))
                .OfType<Color>().Distinct().ToList();
            if (grounds.Count == 0) continue;
            var ground = grounds.OrderBy(g => Contrast(Over(Fade(ink.Color, ink.Opacity), g), g)).First();
            var fg = Over(Fade(ink.Color, ink.Opacity), ground);
            string label = string.IsNullOrWhiteSpace(text) ? "(a text box)" : $"\"{Short(text)}\"";
            double ratio = Contrast(fg, ground);
            if (ratio < Least)
                found.Add($"{label} {Hex(fg)} on {Hex(ground)}, {ratio:0.0}:1 ({Where(v)})");
            // a text box's text when it is selected: the selection's ink on the selection's colour,
            // whether or not anything is selected now (Load Character's name, selected as the box
            // opens, was black on the theme's dark blue)
            if (v is TextPresenter tp && tp.SelectionBrush is ISolidColorBrush mark)
            {
                _selectionsWeighed++;
                var markGround = Over(Fade(mark.Color, mark.Opacity), ground);
                var markInk = tp.SelectionForegroundBrush is ISolidColorBrush s ? s : ink;
                var selFg = Over(Fade(markInk.Color, markInk.Opacity), markGround);
                double selRatio = Contrast(selFg, markGround);
                if (selRatio < Least)
                    found.Add($"{label} selected: {Hex(selFg)} on {Hex(markGround)}, {selRatio:0.0}:1 ({Where(v)})");
            }
        }
        return found;
    }

    /// <summary>
    /// What is drawn behind <paramref name="v"/> at <paramref name="centre"/> (in its own
    /// coordinates): the grounds of it and its ancestors, from the nearest out to the first solid
    /// one, see-through ones mixed over it, and what a panel draws under it there before it (a bar).
    /// Null where a picture or a gradient is the ground, or nothing solid is found.
    /// </summary>
    static Color? Ground(Visual v, Point centre)
    {
        var layers = new List<Color>();
        static IBrush? Paint(Visual p) => p switch
        {
            TextBlock t => t.Background,
            Panel panel => panel.Background,
            Border border => border.Background,
            ContentPresenter cp => cp.Background,
            // a text box's field is drawn beside its text in the template, not behind it: the box's own ground
            TextBox box => box.Background,
            Avalonia.Controls.Shapes.Shape s => s.Fill,
            _ => null,
        };
        // a brush met on the way out: true when it is solid and covers what is under it
        bool? Add(IBrush brush)
        {
            if (brush is not ISolidColorBrush solid) return null;
            var c = Fade(solid.Color, solid.Opacity);
            if (c.A == 0) return false;
            layers.Add(c);
            return c.A == 255;
        }
        Visual? from = null;
        for (Visual? p = v; p != null; from = p, p = p.GetVisualParent())
        {
            // what the panel draws before the text's own branch, under the text's centre - a bar the
            // text is written over (the QoL's experience bar under its figure) - lies under the text
            if (p is Panel panel && from is Control branch && panel.Children.IndexOf(branch) is int at and > 0
                && v.TranslatePoint(centre, panel) is Point here)
            {
                bool covered = false;
                for (int i = at - 1; i >= 0 && !covered; i--)
                {
                    var under = panel.Children[i];
                    if (!under.IsEffectivelyVisible || !under.Bounds.Contains(here)) continue;
                    // what is drawn there, the sibling itself and what it holds, from the topmost
                    // down to the first solid one: a see-through fill is mixed over the bar's track
                    // under it, not over the row (the experience bar's half-strength fill)
                    // anything with no opacity on the way down is not drawn (the DataGrid keeps its red
                    // invalid-cell mark under every cell at Opacity 0); a part opacity fades what it draws
                    static double Shown(Visual d, Visual top)
                    {
                        double o = 1;
                        for (Visual? x = d; x != null; x = x == top ? null : x.GetVisualParent())
                        {
                            if (x.Opacity < 0.05 || !x.IsVisible) return 0;
                            o *= x.Opacity;
                        }
                        return o;
                    }
                    var hits = under.GetSelfAndVisualDescendants().OfType<Visual>().Reverse()
                        .Where(d => Shown(d, under) > 0 && Paint(d) is IBrush b && !(b is ISolidColorBrush s && s.Color.A == 0)
                                    && d.TranslatePoint(new Point(0, 0), panel) is Point o
                                    && new Rect(o, d.Bounds.Size).Contains(here));
                    foreach (var hit in hits)
                    {
                        var paint = Paint(hit)!;
                        double faded = Shown(hit, under);
                        var r = Add(faded < 1 && paint is ISolidColorBrush sc
                            ? new Avalonia.Media.Immutable.ImmutableSolidColorBrush(sc.Color, sc.Opacity * faded) : paint);
                        if (r == null) return null;
                        covered = r == true;
                        if (covered) break;
                    }
                }
                if (covered) break;
            }
            if (Paint(p) is not IBrush brush) continue;
            var added = Add(brush);
            if (added == null) return null;
            if (added == true) break;
        }
        if (layers.Count == 0 || layers[^1].A != 255) return null;
        var ground = layers[^1];
        for (int i = layers.Count - 2; i >= 0; i--) ground = Over(layers[i], ground);
        return ground;
    }

    static Color Fade(Color c, double opacity) => Color.FromArgb((byte)Math.Round(c.A * opacity), c.R, c.G, c.B);

    static Color Over(Color top, Color under)
    {
        double a = top.A / 255.0;
        byte Mix(byte t, byte u) => (byte)Math.Round(t * a + u * (1 - a));
        return Color.FromRgb(Mix(top.R, under.R), Mix(top.G, under.G), Mix(top.B, under.B));
    }

    /// <summary>WCAG 2's contrast ratio of two colours, 1 to 21.</summary>
    static double Contrast(Color a, Color b)
    {
        static double Channel(byte c) { double s = c / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    static string Short(string s)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= 30 ? s : s[..27] + "...";
    }

    /// <summary>Where the text is: the nearest named ancestors, and the MDI child's title if it is in one.</summary>
    static string Where(Visual v)
    {
        var names = new List<string>();
        string? child = null;
        for (Visual? p = v.GetVisualParent(); p != null; p = p.GetVisualParent())
        {
            if (p is StyledElement s && !string.IsNullOrEmpty(s.Name) && !s.Name.StartsWith("PART_") && names.Count < 2) names.Add(s.Name);
            if (child == null && p is MdiChild m) child = m.Title;
        }
        return string.Join(" < ", names.DefaultIfEmpty(v.GetType().Name)) + (child != null ? $" in \"{child}\"" : "");
    }
}
