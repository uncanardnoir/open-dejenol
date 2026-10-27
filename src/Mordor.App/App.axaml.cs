using System.Linq;
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Mordor.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // a Library file that cannot be read or written says so in the crash log
        Mordor.Core.Librarian.Store.Note = CrashLog.Note;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // music tracks loop: poll the MIDI player and restart a finished track. Ten seconds
            // is timMusic.Interval from FRMMAIN.FRM, the original's own polling rate.
            var loop = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            loop.Tick += (_, _) => Audio.TickMusic();
            loop.Start();
            desktop.Exit += (_, _) => Audio.StopMusic();
            string test = Environment.GetEnvironmentVariable("MORDOR_TEST") ?? "";
            if (test == "mdi") { desktop.MainWindow = new MdiDemoWindow(); base.OnFrameworkInitializationCompleted(); return; }
            // The smoke hooks plant characters, log lines, records and even a whole automap. They
            // must never do that in the real saves folder, so a test run without a redirect gets
            // a throwaway one - MORDOR_TEST_LOST=mapped would otherwise overwrite automap.json.
            if (test != "" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MORDOR_SAVES")))
            {
                string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-smoke-saves");
                Environment.SetEnvironmentVariable("MORDOR_SAVES", scratch);
            }
            // The player's own Mordor (GameFolder, docs/GAME_FILES.md): configured, or found. The
            // game proper starts only on a folder whose files are there and read - the full game,
            // or the shareware played as the limited build (LimitedVersion); the test hooks below
            // take whatever was found (MORDOR_GAMEDIR names it outright) and go straight in.
            var found = Mordor.Core.GameFolder.Resolve();
            bool hook = test is "create" or "help" or "options" or "city" or "dungeon";
            TitleWindow.GameDir = found.IsPlayable || hook ? found.Found?.Dir ?? "" : "";
            if (hook) Mordor.Core.LimitedVersion.Apply(found.Found);
            // the enabled mods, if any (docs/MODDING.md) - after the saves redirect, since
            // settings.ini in the saves directory is what says which are enabled
            if (hook) ModStartup.Start(TitleWindow.GameDir);
            // the pictures and the help come out of the player's own copy (Art; a failure is in the crash log)
            if (hook) Art.Prepare(TitleWindow.GameDir);
            if (test == "create")
            {
                // test hook: open character creation directly (with real race/guild rules loaded)
                string dir = TitleWindow.GameDir;
                Mordor.Core.Mdata1.LoadFrom(dir); Mordor.Core.RaceData.LoadFrom(dir); Mordor.Core.GuildData.LoadFrom(dir);
                desktop.MainWindow = new CharacterCreationWindow(dir);
            }
            else if (test == "help") desktop.MainWindow = new HelpWindow();
            else if (test == "options")
            {
                // test hook: the Options window on its own. It is two clicks deep in the real game
                // - F10, then Game Options - which a keyboard-driven harness cannot reach.
                string dir = TitleWindow.GameDir;
                // the tables Backup's "found" test reads a character against
                Mordor.Core.Mdata1.LoadFrom(dir); Mordor.Core.RaceData.LoadFrom(dir); Mordor.Core.GuildData.LoadFrom(dir);
                desktop.MainWindow = new OptionsWindow(Mordor.Core.GameOptions.Load(dir), dir);
            }
            else if (test == "city")
            {
                // test hook: open the town with a throwaway character that has some history
                string dir = TitleWindow.GameDir;
                Mordor.Core.Mdata1.LoadFrom(dir); Mordor.Core.RaceData.LoadFrom(dir); Mordor.Core.GuildData.LoadFrom(dir);
                // MORDOR_TEST_KEEP: the "Tester" already in the (scratch) saves, as the dungeon's hook
                // has it, so a saved window layout can be looked at
                var ch = Environment.GetEnvironmentVariable("MORDOR_TEST_KEEP") == "1"
                    ? Mordor.Core.Character.LoadAllFrom(Mordor.Core.Saves.DirFor(dir))
                                           .FirstOrDefault(c => c.Name.Trim() == "Tester")
                    : null;
                ch ??= Mordor.Core.Character.Create("Tester", Mordor.Core.RaceData.All[0], Mordor.Core.Alignment.Neutral, Mordor.Core.Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
                var monsters = Mordor.Core.MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
                foreach (var m in monsters.Where(m => m.Id != 0 && m.LevelFound <= 2).Take(6)) ch.NoteMonster(m.Id, killed: m.Id % 2 == 0);
                var items = Mordor.Core.MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
                foreach (var it in items.Where(i => i.Id != 0 && i.Floor == 1).Take(5)) ch.ItemsSeen.Add(it.Id);
                ch.Poisoned = true;
                // MORDOR_TEST_DEAD: arrive dead, to see the morgue's box and the dead screen
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_DEAD") is "1" or "raise") { ch.CurHits = 0; ch.IsDead = true; }
                // MORDOR_TEST_DEAD=raise: and somebody has cast a raise on the body, to see fn16B5's offer
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_DEAD") == "raise") { ch.RaiseChance = 35; ch.RaisedBy = "Healer"; ch.RaiseKind = 23; }
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_GM") == "1")
                {
                    // one level short of Crashland, with the experience and gold to take it
                    ch.Guilds[0].Level = Mordor.Core.Guildmasters.Seeded[0].Level;
                    ch.Guilds[0].Experience = ch.ExperienceCap();
                    ch.GoldInBank = 50_000_000_000L;
                    ch.Poisoned = false;
                }
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_FORFEIT") == "1")
                {
                    // pinned, with a fetch quest whose item is nowhere in the pack
                    ch.Guilds[0].Level = 20;
                    ch.Guilds[0].Experience = ch.ExperienceCap();
                    ch.Guilds[0].QuestType = 2;
                    ch.Guilds[0].QuestTarget = items.First(i => i.Id != 0 && i.Floor > 10).Id;
                    ch.GoldInBank = 50_000_000_000L;
                    ch.Poisoned = false;
                    var nomad = Mordor.Core.GuildData.ByName(ch.Guild.Guild);
                    // go through the real equip rules, or the fixture hands out two swords at once
                    Mordor.Core.Item? Look(short id) => items.FirstOrDefault(i => i.Id == id);
                    foreach (var it in items.Where(i => i.Id != 0 && Mordor.Core.Character.IsWearable(i)
                                                        && ch.EquipProblem(i) == null
                                                        && Mordor.Core.Character.UseLevel(i, nomad) is > 17 and <= 20))
                    {
                        var slot = new Mordor.Core.InventorySlot { ItemId = it.Id, Known = Mordor.Core.IdLevel.All };
                        if (ch.WearRefusal(slot, it, Look) != null) continue;
                        slot.Equipped = true;
                        ch.PutInPack(slot);
                        if (ch.Inventory.Count >= 2) break;
                    }
                    // one shallow item carried but not worn, barely known: the bank can take it,
                    // and taking it back is what re-identifies it
                    var shallow = items.FirstOrDefault(i => i.Id != 0 && i.Floor == 1 && !Mordor.Core.Character.IsWearable(i))
                               ?? items.First(i => i.Id != 0 && i.Floor == 1);
                    ch.PutInPack(new Mordor.Core.InventorySlot
                    {
                        ItemId = shallow.Id, Charges = (short)shallow.Charges,
                        Known = Mordor.Core.IdLevel.Barely,
                    });
                    // two of one charged item and one of another, all read right through, so the
                    // store's Combine has something to pour and something to turn away
                    var charged = items.Where(i => i.Id != 0 && Mordor.Core.StoreStock.SoldByCharge(i)
                                                   && i.Charges > 1).Take(2).ToList();
                    if (charged.Count == 2)
                    {
                        foreach (var (it, n) in new[] { (charged[0], 3), (charged[0], 5), (charged[1], 4) })
                            ch.PutInPack(new Mordor.Core.InventorySlot
                            {
                                ItemId = it.Id, Charges = (short)n, Known = Mordor.Core.IdLevel.All,
                            });
                    }
                }
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_RECORDS") == "1")
                {
                    // a few records in other people's names, so the Hall has something to show
                    string sd = Mordor.Core.Saves.DirFor(dir);
                    var hall = Mordor.Core.HallRecords.Load(sd);
                    var holder = Mordor.Core.Character.Create("Crashland", Mordor.Core.RaceData.All[0],
                        Mordor.Core.Alignment.Neutral, Mordor.Core.Sex.Male, new[] { 21, 12, 12, 12, 12, 12 });
                    hall.Check(Mordor.Core.HallRecords.Kind.Strength, holder, 21);
                    hall.Check(Mordor.Core.HallRecords.Kind.Constitution, holder, 19);
                    hall.Check(Mordor.Core.HallRecords.Kind.MostExperience, holder, 1_482_905);
                    hall.Check(Mordor.Core.HallRecords.Kind.Wealthiest, holder, 9_250_000);
                    hall.Check(Mordor.Core.HallRecords.Kind.Fighting, holder, 41);
                    var deep = monsters.Where(m => m.Id != 0).OrderByDescending(Mordor.Core.HallRecords.MonsterPower).Skip(4).First();
                    hall.Check(Mordor.Core.HallRecords.Kind.DeadliestCreature, holder, deep.Id,
                               id => monsters.FirstOrDefault(x => x.Id == id) is Mordor.Core.Monster d ? Mordor.Core.HallRecords.MonsterPower(d) : 0);
                    hall.Save(sd);
                }
                // MORDOR_TEST_PETS=dead: slot 1 empty, slot 2 fallen and carried, slot 3 alive - to see
                // the kept slot numbers, the Carry line and the morgue's Raise Companion button
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_PETS") == "dead")
                {
                    var weak = monsters.Where(m => m.Id != 0 && m.Hits > 0).OrderBy(m => m.GuildLevel).First();
                    ch.Companions[1] = new Mordor.Core.Companion { Ref = weak.Ref, MonsterId = weak.Id, Name = "Fido", Hits = 0, MaxHits = 40, BindingLevel = 5 };
                    ch.Companions[2] = new Mordor.Core.Companion { Ref = weak.Ref, MonsterId = weak.Id, Hits = 12, MaxHits = 12, BindingLevel = 5 };
                    ch.CarryingCompanion = 1;
                    ch.GoldOnHand = 10_000_000;
                }
                // MORDOR_TEST_BANK=some: a few shallow items in the vault, known to every degree, out of
                // order and with a hole a withdrawal left, for the Bank's buttons, marks and Sort;
                // =full: all forty vault slots taken, to see the Bank's "Out of room!"
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_BANK") is "some" or "full")
                {
                    bool full = Environment.GetEnvironmentVariable("MORDOR_TEST_BANK") == "full";
                    var stock = items.Where(i => i.Id != 0 && i.Floor is >= 1 and <= 6).ToList();
                    int count = full ? Mordor.Core.Character.BankSlots : 7;
                    for (int k = 0; k < count; k++)
                    {
                        var it = stock[(k * 7 + 3) % stock.Count];
                        ch.Bank.Add(new Mordor.Core.InventorySlot { ItemId = it.Id, Charges = (short)it.Charges, Known = (Mordor.Core.IdLevel)(k % 4) });
                    }
                    if (!full) ch.Bank[2] = new Mordor.Core.InventorySlot();
                }
                var cityParty = new Mordor.Core.Party(ch);
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_PARTY") == "1")
                {
                    // two companions with packs of their own, to exercise the Exchange screen
                    var shallow = items.Where(i => i.Id != 0 && i.Floor is >= 1 and <= 4).Take(8).ToList();
                    foreach (var it in shallow.Take(3))
                        ch.PutInPack(new Mordor.Core.InventorySlot { ItemId = it.Id, Known = Mordor.Core.IdLevel.Barely });
                    foreach (var (name, guild, level) in new[] { ("Brannoc", "Warrior", 4), ("Sela", "Healer", 30) })
                    {
                        var m = Mordor.Core.Character.Create(name, Mordor.Core.RaceData.All[0], Mordor.Core.Alignment.Neutral, Mordor.Core.Sex.Female, new[] { 12, 21, 21, 12, 12, 12 });
                        m.Guilds.Insert(0, new Mordor.Core.GuildMembership { Guild = guild, Level = level });
                        m.RecalcSpellPoints(refill: true);
                        foreach (var it in shallow.Skip(3).Take(3))
                            m.PutInPack(new Mordor.Core.InventorySlot { ItemId = it.Id, Known = Mordor.Core.IdLevel.Barely });
                        // MORDOR_TEST_QUESTS=1: a monster quest for each, Brannoc's done (the creature
                        // slain, M57B2), to see the Quality of Life mod's quest marks
                        if (Environment.GetEnvironmentVariable("MORDOR_TEST_QUESTS") == "1")
                        {
                            m.Guild.QuestType = 1;
                            m.Guild.QuestTarget = monsters.First(x => x.Id != 0 && x.LevelFound <= 2).Id;
                            m.Guild.QuestDone = name == "Brannoc";
                        }
                        cityParty.Add(m);
                    }
                    // a few log entries of varying age, so the guild log screen has something to show
                    string savesDir = Mordor.Core.Saves.DirFor(dir);
                    if (Mordor.Core.GuildLog.All(savesDir).Count == 0)
                    {
                        int g = Mordor.Core.GuildData.IndexOf(ch.Guild.Guild);
                        long now = Mordor.Core.GuildLog.Today();
                        foreach (var (ago, text) in new (int, string)[]
                        {
                            (9, Mordor.Core.GuildLog.Wrote.JoinedGuild("Brannoc")),
                            (4, Mordor.Core.GuildLog.Wrote.Titled("Brannoc", "Veteran", 4)),
                            (1, Mordor.Core.GuildLog.Wrote.DiedOnLevel("Sela", 3)),
                            (0, Mordor.Core.GuildLog.Wrote.FirstToLevel("Tester", 5)),
                        })
                        {
                            Mordor.Core.GuildLog.Append(savesDir, g, text);
                            var all = Mordor.Core.GuildLog.All(savesDir);
                            all[^1] = all[^1] with { Day = now - ago };
                            Mordor.Core.GuildLog.Rewrite(savesDir, all);
                        }
                    }
                }
                TestNames(cityParty);
                desktop.MainWindow = new CityWindow(cityParty, dir);
            }
            else if (test == "dungeon")
            {
                // test hook: open the dungeon directly at the level-1 entrance with a throwaway character
                string dir = TitleWindow.GameDir;
                Mordor.Core.Mdata1.LoadFrom(dir); Mordor.Core.RaceData.LoadFrom(dir); Mordor.Core.GuildData.LoadFrom(dir);
                var dungeon = Mordor.Core.MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
                var f = dungeon[0]; int sx = 0, sy = 0;
                for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) if (f.Has(x, y, Mordor.Core.Floor.STAIRS_UP)) { sx = x; sy = y; }
                // MORDOR_TEST_CHUTE=1: stand on the first chute square instead, to press t on it
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_CHUTE") == "1")
                    for (int y = 0; y < f.Height; y++) for (int x = 0; x < f.Width; x++) if (f.Has(x, y, Mordor.Core.Floor.CHUTE) && sx >= 0) { sx = x; sy = y; y = f.Height; break; }
                // MORDOR_TEST_KEEP: come back as the character who was here last time instead of a
                // fresh one. Anything that is meant to persist between visits - the window layout
                // above all - cannot be tested at all against a character made new on every run.
                var ch = Environment.GetEnvironmentVariable("MORDOR_TEST_KEEP") == "1"
                    ? Mordor.Core.Character.LoadAllFrom(Mordor.Core.Saves.DirFor(dir))
                                           .FirstOrDefault(c => c.Name.Trim() == "Tester")
                    : null;
                ch ??= Mordor.Core.Character.Create("Tester", Mordor.Core.RaceData.All[0], Mordor.Core.Alignment.Neutral, Mordor.Core.Sex.Male, new[] { 10, 10, 10, 10, 10, 10 });
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_MAGE") == "1")
                {
                    // a Wizard deep enough to have the movement spells: Teleport and Resurrect are
                    // both level 12, and Teleport wants INT 17 WIS 14 CON 11 DEX 17
                    ch.Guilds.Insert(0, new Mordor.Core.GuildMembership { Guild = "Wizard", Level = 26 });
                    ch.Stats = new[] { 14, 21, 21, 14, 12, 18 };
                    ch.RecalcSpellPoints(refill: true); ch.CurHits = 5;
                    var items = Mordor.Core.MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
                    foreach (var (name, known) in new[] { ("Elven Ring", true), ("Holy Sword", true), ("Bronze Sword", false) })
                        if (items.FirstOrDefault(i => i.Name == name) is { } it)
                            ch.PutInPack(new Mordor.Core.InventorySlot { ItemId = it.Id, Charges = (short)it.Charges, Identified = known });
                }
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_ASMODEUS") == "1")
                {
                    // a character who can actually survive him, standing on the deepest floor
                    ch.Guilds.Insert(0, new Mordor.Core.GuildMembership { Guild = "Ninja", Level = 300 });
                    ch.Stats = new[] { 30, 30, 30, 30, 30, 30 };
                    ch.RecalcSpellPoints(refill: true);
                    ch.MaxHits = 30000; ch.CurHits = 30000;
                }
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_FIGHT") == "1" || Environment.GetEnvironmentVariable("MORDOR_TEST_ASMODEUS") == "1")
                {
                    // something to swing, or the fight is one "Using no weapon!" after another;
                    // Asmodeus wants a weapon from at least half his floor, so he gets the deepest
                    var items = Mordor.Core.MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
                    var weapon = Environment.GetEnvironmentVariable("MORDOR_TEST_ASMODEUS") == "1"
                        ? items.Where(i => i.Type <= 7 && i.Att > 0).OrderByDescending(i => i.Floor).First()
                        : items.First(i => i.Name == "Bronze Sword");
                    ch.PutInPack(new Mordor.Core.InventorySlot { ItemId = weapon.Id, Equipped = true, Identified = true });
                    ch.RecalcSwings();
                }
                string lostMode = Environment.GetEnvironmentVariable("MORDOR_TEST_LOST") ?? "";
                if (lostMode != "") ch.BecomeLost();
                if (lostMode == "mapped")
                {
                    // pretend an earlier trip mapped the whole floor, so the first step back onto
                    // known ground should end the lost state straight away
                    var seeded = new Mordor.Core.SharedMap();
                    seeded.Merge(1, Enumerable.Range(0, f.Width * f.Height).Select(i => (i % f.Width, i / f.Width)), f.Width);
                    seeded.Save(Mordor.Core.Saves.DirFor(dir));
                }
                var party = new Mordor.Core.Party(ch);
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_PARTY") == "1")
                {
                    // three companions of mixed states, to exercise the party panel. The third
                    // field is the hits to leave them on, 0 for untouched; Brannoc is left under
                    // ten so the ailment clock's "Help!" can be seen without waiting for a fight.
                    foreach (var (name, guild, hits) in new[] { ("Brannoc", "Warrior", 5), ("Sela", "Healer", 0), ("Kord", "Ninja", 0) })
                    {
                        var m = Mordor.Core.Character.Create(name, Mordor.Core.RaceData.All[0], Mordor.Core.Alignment.Neutral, Mordor.Core.Sex.Female, new[] { 12, 12, 12, 12, 12, 12 });
                        m.Guilds.Insert(0, new Mordor.Core.GuildMembership { Guild = guild, Level = 4 });
                        m.RecalcSpellPoints(refill: true);
                        if (hits > 0) m.CurHits = Math.Clamp(hits, 1, m.MaxHits);
                        if (name == "Kord") m.Poisoned = true;   // watch the 4s ailment clock bite
                        m.DungeonLevel = ch.DungeonLevel; m.X = ch.X; m.Y = ch.Y;
                        party.Add(m);
                    }
                }
                // every member holds the weakest creature there is, so a companion's accidents -
                // at their 20% ceiling for a level 1 creature - can be watched in a fight
                // MORDOR_TEST_PETS=caster: the weakest creature that casts, to watch a companion's spells
                if (Environment.GetEnvironmentVariable("MORDOR_TEST_PETS") is "1" or "caster")
                {
                    bool casters = Environment.GetEnvironmentVariable("MORDOR_TEST_PETS") == "caster";
                    var weak = Mordor.Core.MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"))
                        .Where(m => m.Id != 0 && m.Hits > 0 && (!casters || m.SpellFlags > 0)).OrderBy(m => m.GuildLevel).First();
                    foreach (var member in party.Members)
                        member.Companions.Add(new Mordor.Core.Companion
                        { Ref = weak.Ref, MonsterId = weak.Id, Hits = 500, MaxHits = 500, BindingLevel = 5 });
                }
                TestNames(party);
                desktop.MainWindow = new DungeonWindow(party, dungeon, dir, 1, sx, sy);
            }
            else
            {
                // no menu until there are Mordor's files to play: the first-run window asks for them if need be
                Avalonia.Controls.Window Start(string dir)
                {
                    // The game's pictures and help are read out of the player's copy (Art,
                    // Mordor.Core.Extraction.GameArt); the port carries none. By now they have been
                    // got ready, saying so (below, and the first-run window's Prepare), and this
                    // only finds them. A copy they cannot be read from goes back to the first-run
                    // window with the reason.
                    if (Art.Prepare(dir) is string artError) return ArtFailed(dir, artError);
                    TitleWindow.GameDir = dir;
                    ModStartup.Start(dir);
                    Avalonia.Controls.Window w = test == "browser" ? new MainWindow() : new TitleWindow();
                    w.Closed += (_, _) => HelpWindow.CloseAll();
                    return w;
                }
                Avalonia.Controls.Window ArtFailed(string dir, string artError) =>
                    GameFolderWindow.FirstWindow(
                        new Mordor.Core.GameFolderResolution(
                            new Mordor.Core.GameFilesCheck(dir, Mordor.Core.GameFilesVerdict.Damaged, artError),
                            "art", false, Array.Empty<Mordor.Core.GameFilesCheck>()),
                        Start, GameFolderWindow.MakeMain, prepare: Art.PrepareAsync);
                // Reading the pictures and help out of a copy for the first time takes a while (10 to
                // 20 seconds): a small window says so meanwhile, the work off the window's thread. A
                // start that finds them in the cache goes straight on, with nothing in between.
                if (found.IsPlayable && !Art.IsReady(found.Found!.Dir))
                {
                    string dir = found.Found.Dir;
                    desktop.MainWindow = GameFolderWindow.WhilePreparing(dir, Art.PrepareAsync,
                        error => error is string artError ? ArtFailed(dir, artError)
                                 : GameFolderWindow.FirstWindow(found, Start, GameFolderWindow.MakeMain, prepare: Art.PrepareAsync),
                        GameFolderWindow.MakeMain);
                }
                else desktop.MainWindow = GameFolderWindow.FirstWindow(found, Start, GameFolderWindow.MakeMain, prepare: Art.PrepareAsync);
                // an installed copy asks for a newer version in the background; the main menu
                // offers what it finds (Updates, docs/RELEASING.md). Nothing for a test run.
                Updates.Begin();
            }
            // mdrEndProgram's WinHelp HELP_QUIT (MAINMOD.BAS:938): the help goes when the game
            // does. The main window closing is the menu's Exit, and a help window left up would
            // otherwise keep the process alive. A session end closes it only once the game's own
            // question has been answered Yes (OriginalBox.EndSession), not as the request arrives:
            // a No, or monsters in the area, keeps the game and its help.
            if (hook && desktop.MainWindow != null) desktop.MainWindow.Closed += (_, _) => HelpWindow.CloseAll();
            // A test session has no title to come back to, so Exit to Main Menu (or closing the
            // frame) closes the last window and the process ends. Say so in crash.log, so an end
            // like that is not taken for a crash. ShutdownRequested is raised when the last window
            // has closed (and by the OS ending the session, with windows still up); a Shutdown
            // called by the game raises only Exit.
            if (test is "city" or "dungeon")
            {
                bool lastClosed = false, noted = false;
                desktop.ShutdownRequested += (_, _) => { if (desktop.Windows.Count == 0) lastClosed = true; };
                desktop.Exit += (_, x) => { if (noted) return; noted = true; CrashLog.Note(lastClosed
                    ? $"test session ended ({test}): last window closed"
                    : $"test session ended ({test}): the game shut down, exit code {x.ApplicationExitCode}"); };   // Exit can come twice
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// MORDOR_TEST_NAMES: comma-separated names for the planted party's members in order, to see a
    /// long name (fifteen characters, "Thorgrimbaldson") in the Party Information window. A blank
    /// entry keeps that member's name.
    /// </summary>
    private static void TestNames(Mordor.Core.Party party)
    {
        string names = Environment.GetEnvironmentVariable("MORDOR_TEST_NAMES") ?? "";
        if (names == "") return;
        var each = names.Split(',');
        for (int i = 0; i < each.Length && i < party.Size; i++)
            if (each[i].Trim() != "") party.Members[i].Name = each[i].Trim();
    }
}