using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// frmMenu (FRMMENU.BAS), the main menu. A character is first loaded, which only makes it the
/// menu's current character; <c>Run "Name"</c> is what takes it into the game. The game data is
/// the player's own full Mordor, found or chosen at start (GameFolder, docs/GAME_FILES.md).
///
/// The loaded character is held in memory, as <c>Chars(0)</c> is. Change Password and Exchange
/// Items change that copy; Change Password and Save Exchange write it at once, Unload writes it
/// (mdrcharUnload saves before it clears), and Run takes it into the game as it stands.
/// </summary>
public partial class TitleWindow : Window
{
    /// <summary>The player's Mordor folder, set at start from <see cref="GameFolder"/> before this window is made.</summary>
    public static string GameDir { get; set; } = "";
    private string SavesDir => Saves.DirFor(GameDir);

    /// <summary>Sub Main runs once a run, and with it the limited build's "Public Version" box.</summary>
    private static bool _publicBoxShown;

    /// <summary>Preloading happens once, not every time the window is shown again.</summary>
    private bool _preloadTried;

    /// <summary><c>Chars(Me)</c>: the loaded character, or null when its name is blank.</summary>
    private Character? _loaded;

    public TitleWindow()
    {
        InitializeComponent();
        // frmMain's caption, which MDIForm_Load sets before the menu is shown (FRMMAIN.BAS:148-152):
        // the shareware's frame reads "Mordor 1.1 (Limited Version)" at the main menu already
        // (observed in the original shareware 2026-10-26). The port's menu stands in for the frame
        // there; with the full game's data it keeps frmMenu's own empty caption, as it always has.
        if (LimitedVersion.On) Title = LimitedVersion.FrameCaption;
        // The guild table has no stand-in: without MDATA1 there are no guilds, no spell tables and
        // no game. Main's startup says so and stops - MsgBox "Error reading " + path + "!" and
        // "Is BasePath in MORDOR.INI correct?", captioned "ERROR!", then End (MAINMOD.BAS:1193-1196).
        string? loadFailed = null;
        try
        {
            Mdata1.LoadFrom(GameDir); RaceData.LoadFrom(GameDir);
            GuildData.LoadFrom(GameDir);
        }
        catch (Exception ex)
        {
            loadFailed = "Error reading " + System.IO.Path.Combine(GameDir, "MDATA1.MDR") + "!\r\n"
                       + "Is BasePath in MORDOR.INI correct?";
            CrashLog.Write("loading MDATA1", ex);
        }
        // Image1's picture, read out of frmMenu's form in the player's MORDOR.EXE (Art)
        Backdrop.Source = Art.Bitmap("menu_background.png");

        Audio.Init(GameDir);
        Audio.Apply(GameOptions.Load(GameDir));
        Audio.Music("INTRO");
        Activated += (_, _) => Audio.Music("INTRO");
        FramePauseWatch.Watch(this);   // minimised, the game pauses (FRMMAIN.BAS:202-233)
        ShowMenuState();

        // at the menu gv1B52 is False (FRMMENU.BAS:144), so a session end only asks "Exit Mordor?"
        // (FRMMAIN.BAS:178-181) - there is no party to unload
        // The shareware's close box asks that same question at the menu instead of refusing, and a Yes
        // quits (observed in the original shareware 2026-10-26; LimitedVersion.CloseBoxAsksToExit)
        OriginalBox.RefuseCloseBox(this, () => OriginalBox.YesNo(this, "EXIT", "Exit Mordor?"),
                                   closeBoxAsks: () => LimitedVersion.CloseBoxAsksToExit);

        // Form_Load (FRMMENU.BAS:142): the focus starts on cmdCommand(0)
        Opened += async (_, _) =>
        {
            // Sub Main's sub03E3 (MAINMOD2.BAS:6437), before the data is read and so before any box
            // of the menu's: the limited build's "Limitation Notice" (MsgBox 48), and then
            // Limited=True, so that it is not shown again (MODULE1.BAS:932-940; LimitedVersion). The
            // original keeps the key in MORDOR.INI; the port in the saves directory's PortSettings,
            // as it keeps NotFirstTime. A box closed by the program ending leaves the key unwritten.
            if (LimitedVersion.ShowsLimitationNotice && !PortSettings.Load(SavesDir).LimitationNoticeSeen)
            {
                await OriginalBox.Tell(this, LimitedVersion.LimitationNoticeTitle, LimitedVersion.LimitationNoticeText);
                if (!OriginalBox.CanOwn(this)) return;
                PortSettings.Write(SavesDir, "Limited", "True");
            }
            if (loadFailed != null)
            {
                await OriginalBox.Tell(this, "ERROR!", loadFailed);
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life)
                    life.Shutdown(1);
                return;
            }
            CreateBtn.Focus();
            // Sub Main, once the data is read and before the frame comes up: "If m0622 Then sub10F8"
            // (MAINMOD2.BAS:6455) - the limited build's "Public Version" box, once a run. The port has
            // no frame apart from the menu, so it comes over the menu, before the menu's own welcome.
            if (LimitedVersion.ShowsPublicBox && !_publicBoxShown)
            {
                _publicBoxShown = true;
                await OriginalBox.Tell(this, LimitedVersion.PublicBoxTitle, LimitedVersion.PublicBoxText);   // MsgBox 64
            }
            // the program may have shut down under the box, closing the menu: nothing more is shown
            if (!OriginalBox.CanOwn(this)) return;
            await Welcome();
            // Form_Load's last line, after the welcome: "If m00A8 = True Then cmdCommand(mc002A).Caption =
            // "Ordering/&About"" (FRMMENU.BAS:162), m00A8 a Static the full build never sets. The
            // shareware's About button reads "Ordering/&About" (observed in the original shareware
            // 2026-10-26). It opens frmAbout as ever (117), whose Ordering Info the limited build shows.
            if (LimitedVersion.AboutReadsOrdering) AboutBtn.Content = "Ordering/_About";
            if (!OriginalBox.CanOwn(this)) return;
            // a character marked "Preload Character" goes straight in, password and all
            if (_preloadTried) { MenuReady(); return; }
            _preloadTried = true;
            if (Preloaded() is Character ready) { _loaded = ready; ShowMenuState(); await Play(ready); }
            MenuReady();
        };
        // the port's own: a newer version found by the check at start (Updates) is offered here,
        // once the menu's own boxes are done and while nothing else is up over it
        Activated += (_, _) => OfferUpdate();
        Updates.Found += OfferUpdate;
        Closed += (_, _) => Updates.Found -= OfferUpdate;
    }

    /// <summary>The menu's start-up boxes are over: an update found can be offered from now on.</summary>
    private bool _menuReady;

    private void MenuReady()
    {
        _menuReady = true;
        OfferUpdate();
    }

    /// <summary>
    /// <see cref="Updates.Offer"/> over the menu, when there is something to offer and the menu is
    /// up, in front and has no window of its own open over it (a game being played hides the menu,
    /// so the offer waits until the party is back at the menu).
    /// </summary>
    private async void OfferUpdate()
    {
        if (!Updates.Pending || !_menuReady || !IsVisible || !IsActive || OwnedWindows.Count > 0 || !OriginalBox.CanOwn(this)) return;
        try { await Updates.Offer(this); }
        catch (Exception ex) { CrashLog.Write("the update offer", ex); }
    }

    /// <summary>
    /// Form_Load's first-run box (FRMMENU.BAS:146-160): shown after the menu is up while
    /// NotFirstTime holds no "T", then NotFirstTime=True is written. The original keeps the flag in
    /// MORDOR.INI; the port keeps it in the saves directory's <see cref="PortSettings"/>, so a fresh
    /// saves directory is a first run.
    /// </summary>
    private async System.Threading.Tasks.Task Welcome()
    {
        // not shown over a menu that has closed, and so not marked as seen
        if (PortSettings.Load(SavesDir).Welcomed || !OriginalBox.CanOwn(this)) return;
        await OriginalBox.Tell(this, "Welcome!",   // MsgBox 64
            "Welcome to Mordor 1.1!\n\n"   // gc0186 = "1.1" (MAIN.TXT:33)
            + "We just wanted to remind you that Mordor is a very large game, and can sometimes be complex to the beginner.  "
            + "It is for this reason that we have put a great deal of effort into the on-line Mordor HelpLesson.\n\n"
            + "Even if you've read the manual, it's recommended that a player becomes familiar with the HelpLesson, which contains "
            + "valuable game information, game statistics, hints & tips on playing, and more!\n\n"
            + "If you ever have a question about game play, troubleshooting or even an individual spell, simply press "
            + "F1 anytime in the game and do a search for whatever you're looking for!\n\n"
            + "Enjoy the Game & Happy Adventuring!");
        // a box closed by the program shutting down, not by its OK, leaves the flag unwritten, as
        // the original ending there would
        if (OriginalBox.CanOwn(this)) PortSettings.Write(SavesDir, "NotFirstTime", "True");
    }

    private bool IsSaveLoaded => _loaded != null && _loaded.Name.Trim() != "";

    /// <summary>A name as a button caption: an underscore in it is a letter, not an access key.</summary>
    private static string Plain(string s) => s.Replace("_", "__");

    /// <summary>
    /// sub2D2E (FRMMENU.BAS:247-258): with a character loaded, cmdCommand(0) runs it and Change
    /// Password, Delete and Exchange appear; without one it creates, and Load loads. cmdCommand_Click
    /// runs it after every button (FRMMENU.BAS:120-121).
    /// </summary>
    private void ShowMenuState()
    {
        bool loaded = IsSaveLoaded;
        CreateBtn.Content = loaded ? "_Run \"" + Plain(_loaded!.Name.TrimEnd()) + "\"" : "_Create a Character";
        PasswordBtn.IsVisible = loaded;
        DeleteBtn.IsVisible = loaded;
        ExchangeBtn.IsVisible = loaded;
        LoadBtn.Content = loaded ? "_Unload Character" : "_Load Character";
    }

    /// <summary>The saved characters in record order - see <see cref="Roster"/>.</summary>
    private List<(string file, Character ch)> SavedCharacters() =>
        Roster.Saved(SavesDir).Select(p => (p.File, p.Ch)).ToList();

    private string FileFor(Character c) => System.IO.Path.Combine(SavesDir, Character.SafeFileName(c.Name) + ".json");

    /// <summary>
    /// <c>fn04ED</c>, the save: True, or False from its error handler. The menu's own callers
    /// throw the answer away (FRMMENU.BAS:66, and Save Exchange at FRMEXCHA.BAS:27-28).
    /// </summary>
    private bool SaveCharacter(Character c)
    {
        if (c.Name.Trim() == "") return false;
        try { Directory.CreateDirectory(SavesDir); c.Save(FileFor(c)); return true; }
        catch { return false; }
    }

    /// <summary>
    /// <c>mdrcharUnload</c>'s save (MAINMOD.BAS:422-427). When fn04ED fails it passes
    /// "Error Saving Character in MAINMOD:mdrcharUnload!" to sub04FC and leaves without clearing,
    /// so the character stays loaded. sub04FC shows its line only under /DEBUG and otherwise writes
    /// it to the error file (MAINMOD2.BAS:1689-1700), which is the port's crash log. Returns
    /// whether the character may be let go.
    /// </summary>
    private static bool UnloadSave(Func<Character, bool> save, Character c)
    {
        if (c.Name.Trim() == "" || save(c)) return true;
        CrashLog.Note("Error Saving Character in MAINMOD:mdrcharUnload!");
        return false;
    }

    /// <summary>
    /// fn170D's password box. Cancel comes back as "-1", so the menu cannot tell a typed -1 from a
    /// Cancel, and treats both as Cancel (FRMMENU.BAS:43, 51 and 227).
    /// </summary>
    private async System.Threading.Tasks.Task<string?> AskPassword(string title, string prompt)
    {
        string? typed = await PasswordBox.Ask(this, title, prompt);
        return typed == "-1" ? null : typed;
    }

    /// <summary>
    /// "If you select this option, the character will be automatically loaded (without verification
    /// of the password) the next time Mordor is started" - the help's Control Panel page. Note what
    /// it says about the password: preloading walks straight past it, which is worth knowing before
    /// you turn it on.
    ///
    /// Who that is comes from the one slot, [Loading Information] Character, read at start and
    /// loaded by name (MAINMOD.BAS:1266-1272); it is written when a party is unloaded with its
    /// leader marked, and blanked when that leader is unloaded unmarked (MAINMOD2.BAS:5948-5953).
    /// So the tick takes effect at the next Exit to the menu, as it does in the original. The
    /// port had looked for any saved character with the tick, first found winning; it keeps the
    /// slot in settings.ini in the saves directory (see PortSettings), not in MORDOR.INI.
    /// </summary>
    private Character? Preloaded()
    {
        var settings = PortSettings.Load(SavesDir);
        // saves from before the slot was kept: no Character entry at all yet, so the tick is found
        // the old way until a leader is next unloaded and sub16CC writes the entry
        if (!settings.HasCharacter)
            return SavedCharacters().Select(p => p.ch).FirstOrDefault(c => c.Options.PreloadCharacter);
        string name = settings.Character;
        if (name == "") return null;
        return SavedCharacters().Select(p => p.ch).FirstOrDefault(c => c.Name.TrimEnd() == name);
    }

    // ---- the game data two of the buttons need, read when first wanted ----

    private Dictionary<short, Item>? _items;
    private Item? Lookup(short id)
    {
        if (_items == null)
        {
            try { _items = MdrLoader.LoadItems(System.IO.Path.Combine(GameDir, "MDATA3.MDR")).GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First()); }
            catch { _items = new(); }
        }
        return _items.TryGetValue(id, out var it) ? it : null;
    }

    private List<Floor>? _dungeon;

    /// <summary>
    /// <c>M59BB</c> as each record keeps it (<see cref="Character.Area"/>), which is what
    /// FRMMENU.BAS:80 compares: -1 in town, the area of the square below, and nought for a death
    /// in rock (MAINMOD2.BAS:1185) whatever the square says. A save from before the port kept it
    /// falls back to the square's area id, -1 in town.
    /// </summary>
    private int AreaOf(Character c) => c.AreaOr(k =>
    {
        if (k.DungeonLevel <= 0) return -1;
        try { _dungeon ??= MdrLoader.LoadDungeon(System.IO.Path.Combine(GameDir, "MDATA11.MDR")); }
        catch { return -1; }
        if (k.DungeonLevel > _dungeon.Count) return -1;
        return _dungeon[k.DungeonLevel - 1].AreaIdAt(k.X, k.Y);
    });

    // ---- cmdCommand_Click, button by button ----

    /// <summary>
    /// cmdCommand(0) (FRMMENU.BAS:93-101): frmCreateChar when nobody is loaded, which leaves the new
    /// character loaded (CHARCREA.BAS writes it into <c>Chars(gv1A84)</c>); otherwise into the game.
    /// </summary>
    private async void OnCreateOrRun(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (IsSaveLoaded) { await Play(_loaded!); return; }
        var create = new CharacterCreationWindow(GameDir);
        await OriginalBox.ShowOver(create, this);
        if (create.Result is { } ch) _loaded = ch;
        ShowMenuState();
    }

    /// <summary>
    /// cmdCommand(1), Change Password (cmdCommand_Click, FRMMENU.BAS:42-67). Every prompt is fn170D's
    /// password box, which answers "-1" to Cancel.
    ///
    /// Two faults of the original are kept. The re-entry's Cancel test looks at the first entry, not
    /// the second (FRMMENU.BAS:55), so cancelling the re-entry is simply a mismatch: "Passwords
    /// don't match!" and round again. And the line meant to turn an empty password into a space
    /// (FRMMENU.BAS:62-63) sets the old one instead, so the new one is stored exactly as typed -
    /// empty included, which is no password at all.
    /// </summary>
    private async void OnChangePassword(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!IsSaveLoaded) return;
        var ch = _loaded!;
        string? old = await AskPassword("Change Password", "Enter Old Password");
        if (old == null) { ShowMenuState(); return; }
        if (!ch.PasswordMatches(old))
        {
            await OriginalBox.Tell(this, "Change Password", "Incorrect Password!");   // MsgBox 48
            ShowMenuState();
            return;
        }
        string? first, again;
        do
        {
            first = await AskPassword("ENTER NEW PASSWORD", "Please enter Password");
            if (first == null) { ShowMenuState(); return; }
            again = await AskPassword("ENTER NEW PASSWORD", "Please re-enter Password");
            // a Cancel here is "-1", which only matches a first entry of -1
            again ??= "-1";
            if (first != again) await OriginalBox.Tell(this, "ERROR", "Passwords don't match!");   // MsgBox 48
        } while (first != again);
        ch.Password = first;
        SaveCharacter(ch);   // fn04ED(Chars(Me))
        await OriginalBox.Tell(this, "Change Password", "Password has been Changed.");   // MsgBox 64
        ShowMenuState();
    }

    /// <summary>cmdCommand(2), Exit: <c>Unload frmMain</c> (FRMMENU.BAS:115).</summary>
    private async void OnQuit(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // MDIForm_QueryUnload (FRMMAIN.BAS:174-184): at the menu there are no monsters, so it asks
        if (!await OriginalBox.YesNo(this, "EXIT", "Exit Mordor?")) return;
        // MDIForm_Unload (FRMMAIN.BAS:241-248), whenever the build is not the limited one (m009C,
        // LimitedVersion: the shareware's data says nothing here).
        // Read literally, mdrEndProgram's End (MAINMOD.BAS:947) should stop the program before this
        // box. But the real game shows it: run under OTVDM (docs/RUNNING-THE-ORIGINAL.md), Exit, then
        // Yes to "Exit Mordor?", put up this box, captured on 2026-09-26, with this text and the
        // title "Mordor 1.1". That is gc0192 + " " + gc0186, "Mordor" and "1.1" (MAIN.TXT:33-35).
        // Nothing saves the loaded character on the way out - Unload is what writes it.
        await OriginalBox.ThankYou(this);
        Close();
    }

    /// <summary>cmdCommand(3): the HelpLesson (FRMMENU.BAS:105).</summary>
    private void OnHelp(object? s, Avalonia.Interactivity.RoutedEventArgs e) => new HelpWindow().Show();

    /// <summary>
    /// cmdCommand(4), Exchange Items (cmdCommand_Click, FRMMENU.BAS:68-87). sub2D04 1 loads a second
    /// character beside the first; either owing the bank ends it, and so does their standing on
    /// different levels or in different areas; otherwise frmExchange opens modally.
    ///
    /// Whatever happens after the load, the second character is saved on the way out
    /// (mdrcharUnload saves before it clears, MAINMOD.BAS:422-427) - after Cancel as much as after
    /// Save Exchange, so Cancel does not undo a swap for them. The loaded character keeps its
    /// changes in memory; they reach the disk when it is unloaded or played, and are lost if the
    /// game is left from the menu first.
    /// </summary>
    private async void OnExchange(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!IsSaveLoaded) return;
        var me = _loaded!;
        var other = await LoadCharacter(exchange: true);
        if (other == null) { ShowMenuState(); return; }   // UBound(Chars) = 0: nothing was loaded

        if (me.GoldInBank < 0 || other.GoldInBank < 0)
        {
            await OriginalBox.Tell(this, "Sorry!",   // MsgBox 48
                "One or both characters still owe money to the Bank and cannot Exchange Items or Gold until it is paid!");
            UnloadSave(SaveCharacter, other);   // mdrcharUnload mc0058
            ShowMenuState();
            return;
        }
        if (me.DungeonLevel != other.DungeonLevel || AreaOf(me) != AreaOf(other))
            await OriginalBox.Tell(this, "Exchange Items", "Characters are not at the same location!");   // MsgBox 48
        else
            await new ExchangeWindow(me, other, Lookup, c => SaveCharacter(c)).ShowDialog(this);
        UnloadSave(SaveCharacter, other);   // mdrcharUnload mc0058
        ShowMenuState();
    }

    /// <summary>
    /// cmdCommand(5) - sub2D18 (FRMMENU.BAS:174-199): the loaded character only. Any guild it was
    /// master of loses its name, and sub0565 True reinstates that guild's original master.
    /// </summary>
    private async void OnDelete(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loaded is not { } ch || ch.Name.Trim() == "") return;
        if (!await OriginalBox.YesNo(this, "DELETE CHARACTER", "Delete " + ch.Name.TrimEnd() + "?")) return;

        // the guildmasters first (178-185): every guild this character held loses the name, and sub0565
        // True refills every empty seat, not only the ones just vacated
        var masters = Guildmasters.Load(SavesDir);
        foreach (var m in ch.Guilds.Where(m => m.Level > 0))
        {
            int g = GuildData.IndexOf(m.Guild);
            if (g >= 0 && masters.Held.TryGetValue(g, out var h) && h.Name == ch.Name.TrimEnd()) h.Name = "";
        }
        masters.Seed(SavesDir);

        // then the record; the original has no box for a failed delete, so the system's own message is shown
        var before = SavedCharacters().Select(p => p.ch.Name).ToList();
        try { File.Delete(FileFor(ch)); }
        catch (Exception ex) { StatusText.Text = ex.Message; return; }
        // the record is blanked, not removed (FRMMENU.BAS:191-192), and the next new character takes it
        Roster.Removed(SavesDir, ch.Name, before);
        // a mod that keeps something per character lets it go (docs/MODDING.md, CharacterDeleted)
        Mordor.Modding.ModHost.Active.CharacterDeleted(ch.Name.TrimEnd());

        await OriginalBox.Tell(this, "CHARACTER DELETED", "Character has been deleted!");
        _loaded = null;
        ShowMenuState();
    }

    /// <summary>cmdCommand(6) (FRMMENU.BAS:90-92): frmOptions, modal.</summary>
    private async void OnOptions(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new OptionsWindow(GameOptions.Load(GameDir), GameDir);
        await OriginalBox.ShowOver(win, this);
        // the fonts too: the Hall of Records and the next game's windows are drawn in them (UiFonts)
        if (win.Accepted) { Audio.Apply(GameOptions.Load(GameDir)); UiFonts.Use(GameOptions.Load(GameDir)); Audio.Music("INTRO"); }
        ShowMenuState();
    }

    /// <summary>
    /// The port's own: the Mods page (docs/MODDING.md), modal over the menu as Options is. Not on
    /// the original's form; it had been reached from inside Options.
    /// </summary>
    private async void OnMods(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try { await new ModsWindow(atMainMenu: true).ShowDialog(this); }
        catch (Exception ex) { CrashLog.Write("the Mods page", ex); }
        ShowMenuState();
    }

    /// <summary>
    /// cmdCommand(7) (FRMMENU.BAS:106-113): load when nobody is loaded; otherwise mdrcharUnload,
    /// which saves the character before it lets go of it. The focus goes back to cmdCommand(0).
    /// </summary>
    private async void OnLoad(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (IsSaveLoaded) { if (UnloadSave(SaveCharacter, _loaded!)) _loaded = null; }
        else if (await LoadCharacter(exchange: false) is Character c) _loaded = c;
        ShowMenuState();
        CreateBtn.Focus();
    }

    /// <summary>
    /// sub2D04 (FRMMENU.BAS:204-243): "Load which character?" for a load, "Exchange Items with
    /// who?" for an exchange, both over everybody but the loaded character (FRMINTER.BAS:382-384).
    /// "Enter Password" is asked only when the character has one (FRMMENU.BAS:225).
    ///
    /// fn04CD answers True, False or -2 (MAINMOD.BAS:284-350). False - the record could not be
    /// read - ends it without a word; -2 is "Incorrect Password!". The "Unknown error" box after
    /// them (FRMMENU.BAS:240) can never be reached, so it is not here.
    /// </summary>
    private async System.Threading.Tasks.Task<Character?> LoadCharacter(bool exchange)
    {
        string me = IsSaveLoaded ? _loaded!.Name.TrimEnd() : "";
        var saved = SavedCharacters().Where(p => p.ch.Name.TrimEnd() != me).ToList();
        int i = exchange
            ? await NamePicker.Ask(this, "Exchange Items", "Exchange Items with who?",
                                   saved.Select(p => p.ch.Name.TrimEnd()).ToList(), Interact.NobodyFor("LOADCHAR"))
            : await NamePicker.Ask(this, "Load Character", "Load which character?",
                                   saved.Select(p => p.ch.Name.TrimEnd()).ToList(), Interact.NobodyFor("LOADCHAR"));
        if (i < 0) return null;

        Character candidate;
        try { candidate = Character.Load(saved[i].file); }
        catch { return null; }   // fn04CD's error handler returns False, and False says nothing

        string said = "";
        if (candidate.HasPassword)
        {
            string? typed = await AskPassword("Load Character", "Enter Password");
            if (typed == null) return null;
            said = typed;
        }
        // fn04CD compares whatever was typed - "" when nobody asked - with the trimmed password
        if (!candidate.PasswordMatches(said))
        {
            await OriginalBox.Tell(this, "SORRY!", "Incorrect Password!");   // MsgBox 64
            return null;
        }
        return candidate;
    }

    /// <summary>cmdCommand(8): frmAbout, modal (FRMMENU.BAS:117).</summary>
    private async void OnAbout(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await new AboutWindow(GameDir).ShowDialog(this);
        ShowMenuState();
    }

    /// <summary>
    /// cmdCommand(9), frmCharList (FRMCHARL.BAS): "Name (Level N Guild)" for each character, " [STATE]" after the
    /// dead, and the count. With nobody, the list holds &lt;NONE&gt; and the count is 0. It closes on
    /// Exit or as soon as it loses focus (Form_Deactivate / Form_LostFocus).
    /// </summary>
    private async void OnCharacterList(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var saved = SavedCharacters().Select(p => p.ch).ToList();
        var rows = saved.Select(c =>
        {
            string row = c.Name.TrimEnd() + " (Level " + c.Guild.Level + " " + c.Guild.Guild + ")";
            if (c.IsDead) row += " [" + DeathStates.Name(c.CauseOfDeath).ToUpperInvariant() + "]";
            return row;
        }).ToList();
        if (rows.Count == 0) rows.Add("<NONE>");

        var list = new ListBox { ItemsSource = rows, Height = 260, SelectedIndex = saved.Count > 0 ? 0 : -1 };
        var exit = new Button { Content = "Exit", IsCancel = true };
        var win = new Window
        {
            Title = "Character List", Width = 420, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush.Parse("#101018"),
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Content = new Border
            {
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal, Spacing = 8,
                            Children =
                            {
                                new TextBlock { Text = "Characters", Foreground = Brushes.LightGray },
                                new TextBlock { Text = saved.Count.ToString(), Foreground = Brushes.White },
                            },
                        },
                        list,
                        exit,
                    },
                },
            },
        };
        exit.Click += (_, _) => win.Close();
        win.Deactivated += (_, _) => win.Close();
        await OriginalBox.ShowOver(win, this);
        ShowMenuState();
    }

    /// <summary>cmdCommand(10): frmHall, modal (FRMMENU.BAS:40).</summary>
    private async void OnHall(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await new HallWindow(GameDir).ShowDialog(this);
        ShowMenuState();
    }

    private async System.Threading.Tasks.Task Play(Character ch)
    {
        // the port's own (docs/MODDING.md): a save made with mods that are not running now is
        // pointed out once, and then plays as it is
        await ModStartup.WarnIfModsMissing(this, ch);
        Hide();
        var city = new CityWindow(ch, GameDir);
        city.Closed += (_, _) => { Show(); Audio.Music("INTRO"); };
        city.Show();
        await System.Threading.Tasks.Task.CompletedTask;
    }
}
