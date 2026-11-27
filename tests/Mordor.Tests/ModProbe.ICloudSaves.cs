using System.IO.Compression;
using System.Reflection;
using System.Text;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The iCloud Saves mod and the two hooks added for it (API version 17, docs/MODDING.md): with mods off
/// nothing is hooked and nothing is written; the game raises <c>Saved</c> after its saves and asks
/// <c>MainMenuOpened</c> only when hooked; the mod hooks nothing off macOS. Then the sync itself, on
/// temporary folders standing in for two Macs' saves folders and one iCloud Drive - never the real
/// iCloud Drive (<c>MORDOR_ICLOUD</c> points the mod at a temporary one) and never the real saves:
/// a snapshot's manifest round trip, the hashes proven before a restore (a tampered, a cut-short and
/// a path-climbing snapshot refused), conflict detection, placeholders and dataless files never
/// restored from, the backup made and checked before anything is replaced, this Mac's settings.ini
/// and mods.log never synced, pruning only this Mac's own, and the start-up questions.
/// </summary>
public static partial class ModProbe
{
    private static Assembly? _icloudAsm;

    private static Type IcType(string name) =>
        _icloudAsm!.GetType("Mordor.Mod.ICloudSaves." + name, throwOnError: true)!;

    /// <summary>A static of the mod's, its own exception unwrapped.</summary>
    private static object? IcStatic(string type, string method, params object?[] args)
    {
        try { return IcType(type).GetMethod(method)!.Invoke(null, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { throw ex.InnerException; }
    }

    /// <summary>A stand-in Mac: its own saves folder, name and clock, on the shared stand-in iCloud Drive.</summary>
    private sealed class FakeMac
    {
        public readonly string Saves, Name;
        public DateTime Now;
        public readonly HashSet<string> Dataless = new(StringComparer.OrdinalIgnoreCase);
        public readonly dynamic Sync;

        public FakeMac(string root, string name, string cloud, DateTime now)
        {
            Name = name; Now = now;
            Saves = Path.Combine(root, name, "saves");
            Directory.CreateDirectory(Saves);
            Func<DateTime> clock = () => Now;
            Func<string, bool> dataless = p => Dataless.Contains(Path.GetFileName(p));
            Sync = Activator.CreateInstance(IcType("CloudSync"), Saves, cloud, name, clock, dataless)!;
        }

        public void Write(string file, string text) => File.WriteAllText(Path.Combine(Saves, file), text);
        public string Read(string file) => File.ReadAllText(Path.Combine(Saves, file));
        public string Kind() => ((object)Sync.Status().Kind).ToString()!;
        public string WorldHash() => (string)Sync.ReadLocal().WorldHash;
        public string[] Backups() => Directory.GetDirectories(Path.GetDirectoryName(Saves)!, "saves-backup-*");
    }

    /// <summary>A stand-in main menu: answers each question with the next answer given, and records what it was asked.</summary>
    private sealed class FakeMenu
    {
        public readonly List<string> Asked = new();
        public readonly Queue<int> Answers = new();
        public int Quits;
        public MainMenuContext Context(string saves) => new(saves, (title, text, buttons) =>
        {
            Asked.Add($"{text} [{string.Join("|", buttons)}]");
            return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : buttons.Count - 1);
        }, () => Quits++);
    }

    private static string[] SnapshotsIn(string cloud) =>
        Directory.Exists(Path.Combine(cloud, "Snapshots"))
            ? Directory.GetFiles(Path.Combine(cloud, "Snapshots")).Select(p => Path.GetFileName(p)).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    private static void ICloudSavesChecks(string scratch, string bundled)
    {
        string root = Path.Combine(scratch, "icloud");
        string cloudDocs = Path.Combine(root, "CloudDocs");
        Directory.CreateDirectory(cloudDocs);
        string cloud = Path.Combine(cloudDocs, "OpenDejenol");
        string? oldEnv = Environment.GetEnvironmentVariable("MORDOR_ICLOUD");
        try
        {
            // every iCloud Drive path the mod works out from here on is the temporary one
            Environment.SetEnvironmentVariable("MORDOR_ICLOUD", cloudDocs);
            HookChecks(root, bundled, cloudDocs, cloud);
            _icloudAsm = Assembly.LoadFrom(Path.Combine(bundled, "ICloudSaves", "Mordor.Mod.ICloudSaves.dll"));
            EngineChecks(root, cloud);
            ChainChecks(root);
            StartChecks(root);
        }
        finally { Environment.SetEnvironmentVariable("MORDOR_ICLOUD", oldEnv); }
    }

    // ---------------------------------------------------------------- the hooks and the mod loaded

    private static void HookChecks(string root, string bundled, string cloudDocs, string cloud)
    {
        Console.WriteLine("iCloud Saves: the manifest, and the two hooks with mods off (API version 17):");
        string dir = Path.Combine(root, "host");
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        var off = Host(Path.Combine(dir, "off.ini"), log, bundled);
        var im = off.Discovered.FirstOrDefault(d => d.Id == "ICloudSaves");
        Check("iCloud Saves manifest found, no problem", im != null && im.Problem == null, true);
        Check("its name and version", im?.Manifest.Title, "iCloud Saves 1.0.0");
        Check("its description says macOS only", im?.Manifest.Description.StartsWith("macOS only:"), true);
        Check("two toggles, both on once the mod is", string.Join(",", im?.Manifest.Settings.Select(t => $"{t.Key}={t.Default}") ?? Array.Empty<string>()),
              "UploadAtSavePoints=True,CheckAtStart=True");
        Check("each toggle's description short, one sentence (the Mods page's rule)",
              im?.Manifest.Settings.All(t => t.Description.Length <= 110 && t.Description.Count(c => c == '.') == 1), true);
        Check("not enabled by default", off.IsEnabled("ICloudSaves"), false);
        bool made = false;
        var t = off.MainMenuOpened(() => { made = true; return new MainMenuContext(dir, (_, _, _) => Task.FromResult(0), () => { }); });
        Check("mods off: the main menu's moment hooked by nobody, done at once, no context made",
              $"{off.MainMenuHooked} {t.IsCompleted} {made}", "False True False");
        off.SavePointReached(SavePointKind.GameEnded);
        off.SavePointReached(SavePointKind.ArrivedInTown);
        ModHost.Empty().SavePointReached(SavePointKind.GameEnded);
        Check("mods off: a save point tells nobody, and no iCloud folder is made", Directory.Exists(cloud), false);
        Check("the empty host: nothing hooked", ModHost.Empty().MainMenuHooked, false);
        string tw = PortSource("src/Mordor.App", "TitleWindow.axaml.cs").Replace("\r\n", "\n");
        Check("the menu asks the host first, and awaits the moment only when a mod hooks it, before the preload",
              System.Text.RegularExpressions.Regex.IsMatch(tw, @"_preloadTried = true;[^}]*if \(Mordor\.Modding\.ModHost\.Active\.MainMenuHooked\)\s*\{\s*await MenuModsOpened\(\);[^}]*\}\s*if \(Preloaded\(\) is Character ready\)"), true);
        // the menu shut while the hooks run, and opened again however they end (the windows suite
        // drives it with a hook that waits)
        Check("... with the menu (but Exit) shut while it is awaited, and opened again in a finally",
              System.Text.RegularExpressions.Regex.IsMatch(tw, @"Task MenuModsOpened\(\)\s*\{\s*var shut = Form\.Children\.OfType<Button>\(\)\.Where\(b => b != ExitBtn[^;]*;\s*foreach \(var b in shut\) b\.IsEnabled = false;[^}]*try \{ await ModStartup\.MainMenuOpened\(this\); \}\s*finally\s*\{[^}]*foreach \(var b in shut\) b\.IsEnabled = true;"), true);
        string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        Check("the city raises Saved after its arrival save, and after its last save as it closes - each only when that save worked",
              System.Text.RegularExpressions.Regex.IsMatch(city, @"RefreshSummary\(\); bool saved = Save\(\);\s*(//[^\n]*\n\s*)*if \(saved\) Mordor\.Modding\.ModHost\.Active\.SavePointReached\(Mordor\.Modding\.SavePointKind\.ArrivedInTown\);")
              && System.Text.RegularExpressions.Regex.IsMatch(city, @"if \(_below != null\) \{ _below\.Close\(\); saved = Save\(\); \}\s*else \{ SaveWindowLayout\(\); saved = Save\(\); \}\s*(//[^\n]*\n\s*)*if \(saved\) Mordor\.Modding\.ModHost\.Active\.SavePointReached\(Mordor\.Modding\.SavePointKind\.GameEnded\);")
              && System.Text.RegularExpressions.Regex.IsMatch(city, @"private bool Save\(\)\s*\{\s*try\s*\{\s*return _party\.TrySaveAll\(Saves\.DirFor\(_gameDir\)\);\s*\}\s*catch \{ return false;"), true);
        {
            // the city's Save says whether it worked (Party.TrySaveAll): a folder that cannot be made, or one member's file not written
            var two = new Party(new Character { Name = "Ann", Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } } },
                                new Character { Name = "Ben", Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } } });
            string good = Path.Combine(dir, "saved-ok"), blocked = Path.Combine(dir, "saved-file"), partly = Path.Combine(dir, "saved-partly");
            File.WriteAllText(blocked, "a file where the folder would be");
            Directory.CreateDirectory(Path.Combine(partly, "Ben.json"));   // a folder in the way of Ben's save
            Check("Party.TrySaveAll: true when all are written; false when the folder cannot be made; false when one member's save fails, the other still written",
                  $"{two.TrySaveAll(good)} {two.TrySaveAll(blocked)} {two.TrySaveAll(partly)} {File.Exists(Path.Combine(partly, "Ann.json"))}",
                  "True False False True");
        }
        {
            // the game's context failing to be made (RunActions' make) ends no program: logged, no hook asked, the task done
            string menuMods = Path.Combine(dir, "menu-mods");
            MakeTestMod(menuMods, "MenuHook", nameof(MenuHookMod));
            var mst = new ModSettingsStore(Path.Combine(dir, "menuhook.ini"));
            mst.SetEnabled("MenuHook", true);
            var mlog = new List<string>();
            var mh = Host(mst.Path, mlog, menuMods);
            int asked = 0;
            Task? done = null;
            Exception? thrown = null;
            try { done = mh.MainMenuOpened(() => throw new InvalidOperationException("no menu")); done.GetAwaiter().GetResult(); }
            catch (Exception ex) { thrown = ex; }
            Check("a context that throws as it is made: no throw out of the host, the task done, the hook not asked, the mod not faulted, mods.log told",
                  $"{thrown?.GetType().Name ?? "none"} {done?.IsCompletedSuccessfully} {asked} {mh.Loaded.Single(l => l.Id == "MenuHook").Faulted} "
                  + $"{mlog.Any(l => l.Contains("MainMenuOpened could not be made") && l.Contains("no menu"))}",
                  "none True 0 False True");
            mh.MainMenuOpened(() => new MainMenuContext(dir, (_, _, _) => { asked++; return Task.FromResult(0); }, () => { })).GetAwaiter().GetResult();
            Check("... and the next time, made, the hook is asked (nothing left running)", asked, 1);
        }

        // a context is good only while its hook runs
        var probe = new MainMenuContext(dir, (_, _, b) => Task.FromResult(b.Count), () => { });
        int before = probe.Ask("t", "x", "A", "B").Result;
        probe.Close();
        Check("a closed main-menu context asks nothing (-1) and quits nothing", $"{before} {probe.Ask("t", "x", "A").Result} {probe.IsOpen}", "2 -1 False");

        Console.WriteLine("iCloud Saves: the mod loaded (MORDOR_ICLOUD at a temporary folder):");
        string ini = Path.Combine(dir, "settings.ini");
        new ModSettingsStore(ini).SetEnabled("ICloudSaves", true);
        var on = Host(ini, log, bundled);
        var lm = on.Loaded.FirstOrDefault(l => l.Id == "ICloudSaves");
        Check("loaded, not faulted, switches live", lm != null && !lm.Faulted && lm.SwitchesLive, true);
        if (!OperatingSystem.IsMacOS())
        {
            Check("off macOS: nothing hooked - no main-menu moment, no window, no save-point listener", $"{on.MainMenuHooked} {on.Windows.Count()}", "False 0");
            Check("... and it says why in mods.log", log.Any(l => l.Contains("ICloudSaves") && l.Contains("macOS only")), true);
            on.SavePointReached(SavePointKind.GameEnded);
            Check("... a save point uploads nothing", Directory.Exists(cloud), false);
            return;
        }
        Check("on macOS: the main-menu moment hooked, one window titled iCloud Saves",
              $"{on.MainMenuHooked} {string.Join(",", on.Windows.Select(w => w.Window.Title))}", "True iCloud Saves");
        // a world beside settings.ini (the mod's DataDirectory), then the game's save point
        File.WriteAllText(Path.Combine(dir, "dungeon.json"), "{\"rooms\":1}");
        File.WriteAllText(Path.Combine(dir, "Anna.json"), "{\"name\":\"Anna\"}");
        File.WriteAllText(Path.Combine(dir, "mods.log"), "this run's log");
        on.SavePointReached(SavePointKind.GameEnded);
        var up = SnapshotsIn(cloud);
        Check("the game ending: one snapshot in iCloud Drive's OpenDejenol/Snapshots, the mod still running",
              $"{up.Length} {up.FirstOrDefault()?.EndsWith(".odsave")} {lm!.Running}", "1 True True");
        on.SavePointReached(SavePointKind.ArrivedInTown);
        Check("back in town with nothing changed: no second snapshot", SnapshotsIn(cloud).Length, 1);
        File.WriteAllText(Path.Combine(dir, "dungeon.json"), "{\"rooms\":2}");
        on.SavePointReached(SavePointKind.ArrivedInTown);
        Check("back in town with the world changed: a second one", SnapshotsIn(cloud).Length, 2);
        // the start: in sync, so nothing is asked and nothing quits
        var menu = new FakeMenu();
        on.MainMenuOpened(() => menu.Context(dir)).GetAwaiter().GetResult();
        Check("the main menu at start, in sync: nothing asked, no quit", $"{menu.Asked.Count} {menu.Quits} {lm.Running}", "0 0 True");
        // tidy: these snapshots are the host test's own
        Directory.Delete(cloud, recursive: true);
    }

    // ---------------------------------------------------------------- the sync, on stand-in Macs

    private static void EngineChecks(string root, string cloud)
    {
        Console.WriteLine("iCloud Saves: what is synced:");
        bool Synced(string n) => (bool)IcStatic("CloudSync", "IsSynced", n)!;
        Check("the world's files are synced; this Mac's settings.ini, mods.log, the mod's record, crash.log, hidden, half-written and stray files are not",
              string.Join(",", new[] { "dungeon.json", "Anna.json", "party-Anna.json", "MDATA9.MDR", "settings.ini", "mods.log", "icloudsaves.json", "crash.log", ".DS_Store", "dungeon.json.tmp", "x.partial.nosync", "dpsmeter.json.bad-20261030-120000", "Anna.json.good", "MDATA9B.MDR", "stray.zip" }
                  .Select(n => $"{n}={(Synced(n) ? 1 : 0)}")),
              "dungeon.json=1,Anna.json=1,party-Anna.json=1,MDATA9.MDR=1,settings.ini=0,mods.log=0,icloudsaves.json=0,crash.log=0,.DS_Store=0,dungeon.json.tmp=0,x.partial.nosync=0,dpsmeter.json.bad-20261030-120000=0,Anna.json.good=0,MDATA9B.MDR=0,stray.zip=0");
        Check("the dungeon's levels, each in a file of its own (DungeonState), are synced with it; the old single file kept aside as dungeon.json.old is not",
              string.Join(",", new[] { "dungeon-01.json", "dungeon-12.json", "dungeon.json", "dungeon.json.old", "dungeon-05.json.tmp" }
                  .Select(n => $"{n}={(Synced(n) ? 1 : 0)}")),
              "dungeon-01.json=1,dungeon-12.json=1,dungeon.json=1,dungeon.json.old=0,dungeon-05.json.tmp=0");
        Check("a snapshot's file name: UTC time and the Mac's name, tidied", IcStatic("Snapshot", "FileName", new DateTime(2026, 10, 30, 3, 4, 5, DateTimeKind.Utc), "Dave's Mac_mini", 1),
              "20261030T030405Z_Dave-s-Mac-mini.odsave");

        var t0 = new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc);
        var a = new FakeMac(root, "Mac-A", cloud, t0);
        var b = new FakeMac(root, "Mac-B", cloud, t0);
        a.Write("dungeon.json", "{\"rooms\":\"A1\"}");
        a.Write("automap.json", "{\"map\":1}");
        a.Write("Anna.json", "{\"name\":\"Anna\",\"level\":3}");
        a.Write("party-Anna.json", "[\"Anna\"]");
        a.Write("settings.ini", "[Mod:ICloudSaves]\nEnabled=True\nA's own settings");
        a.Write("mods.log", "A's log");

        Console.WriteLine("iCloud Saves: a first upload, and its snapshot read back:");
        Check("Mac A, never synced, iCloud empty: an upload is due", a.Kind(), "LocalNewer");
        dynamic r = a.Sync.AutoUpload();
        Check("the upload at a save point goes up", (bool)r.Done, true);
        var names = SnapshotsIn(cloud);
        Check("one snapshot, named for its time and Mac, nothing half-written left (.nosync)", string.Join(",", names), "20261030T010000Z_Mac-A.odsave");
        string snapA1 = Path.Combine(cloud, "Snapshots", names[0]);
        dynamic v = IcStatic("Snapshot", "Verify", snapA1)!;
        Check("the manifest: format 1, OpenDejenol, Mac A, the upload's time",
              $"{v.Manifest.Format} {v.Manifest.App} {v.Manifest.Device} {((DateTime)v.Manifest.CreatedUtc):O}", $"1 OpenDejenol Mac-A {t0:O}");
        var listed = ((IEnumerable<dynamic>)v.Manifest.Files).Select(f => (string)f.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Check("the world's four files, never settings.ini or mods.log", string.Join(",", listed), "Anna.json,automap.json,dungeon.json,party-Anna.json");
        var files = (IReadOnlyDictionary<string, byte[]>)v.Files;
        Check("each file's bytes as written", Encoding.UTF8.GetString(files["Anna.json"]), "{\"name\":\"Anna\",\"level\":3}");
        Check("its world hash is the world here", (string)v.Manifest.WorldHash, a.WorldHash());
        Check("each file's SHA-256 recorded, lower-case hex",
              ((IEnumerable<dynamic>)v.Manifest.Files).All(f => ((string)f.Sha256).Length == 64 && (string)f.Sha256 == ((string)f.Sha256).ToLowerInvariant()), true);
        Check("Mac A now in sync; a save point with nothing changed uploads nothing", $"{a.Kind()} {(bool)a.Sync.AutoUpload().Done} {SnapshotsIn(cloud).Length}", "InSync False 1");

        Console.WriteLine("iCloud Saves: a second Mac with nothing saved restores it, backup first:");
        b.Write("settings.ini", "B's own settings");
        b.Write("mods.log", "B's log");
        Check("Mac B, no world yet: iCloud's is offered", b.Kind(), "CloudNewer");
        r = b.Sync.Restore(names[0]);
        Check("restored", (bool)r.Done, true);
        Check("B's world is A's, byte for byte", $"{b.Read("Anna.json") == a.Read("Anna.json")} {b.WorldHash() == a.WorldHash()}", "True True");
        Check("B's own settings.ini and mods.log untouched", $"{b.Read("settings.ini")} | {b.Read("mods.log")}", "B's own settings | B's log");
        Check("a saves-backup folder beside B's saves, made first, holding what B had", b.Backups().Length == 1
              && File.ReadAllText(Path.Combine(b.Backups()[0], "settings.ini")) == "B's own settings", true);
        Check("B in sync now", b.Kind(), "InSync");

        Console.WriteLine("iCloud Saves: both Macs change the world - a conflict, nobody wins on their own:");
        a.Now = t0.AddHours(1);
        a.Write("dungeon.json", "{\"rooms\":\"A2\"}");
        Check("A changed, iCloud not: the save point uploads", (bool)a.Sync.AutoUpload().Done, true);
        string snapA2 = SnapshotsIn(cloud).Last();
        b.Now = t0.AddHours(2);
        b.Write("store.json", "{\"stock\":\"B's\"}");
        b.Write("party-Bob.json", "[\"Bob\"]");
        string bHash = b.WorldHash();
        dynamic st = b.Sync.Status();
        Check("B changed too: a conflict, iCloud's newest the other side", $"{st.Kind} {st.Candidate.FileName}", $"Conflict {snapA2}");
        r = b.Sync.AutoUpload();
        Check("the save point holds back in a conflict: nothing uploaded, it says why",
              $"{(bool)r.Done} {SnapshotsIn(cloud).Length} {((string)r.Message).Contains("both changed")}", "False 2 True");
        Check("... and B's saves are as they were", b.WorldHash(), bHash);

        Console.WriteLine("iCloud Saves: a tampered, a cut-short and a path-climbing snapshot are never restored:");
        // a copy of A2 with dungeon.json changed inside and the manifest left as it was
        string tampered = Path.Combine(cloud, "Snapshots", "20261030T050000Z_Mac-C.odsave");
        File.Copy(Path.Combine(cloud, "Snapshots", snapA2), tampered);
        using (var z = ZipFile.Open(tampered, ZipArchiveMode.Update))
        {
            z.GetEntry("world/dungeon.json")!.Delete();
            using var s = z.CreateEntry("world/dungeon.json").Open();
            s.Write(Encoding.UTF8.GetBytes("{\"rooms\":\"ZZ\"}"));   // the same length as A2's, so only the hash can tell
        }
        string Refusal(string path)
        {
            try { IcStatic("Snapshot", "Verify", path); return "accepted"; }
            catch (Exception ex) { return ex.GetType().Name + ": " + ex.Message; }
        }
        Check("a file changed inside: refused, its hash does not match", Refusal(tampered).StartsWith("SnapshotInvalidException") && Refusal(tampered).Contains("does not match its hash"), true);
        int backupsBefore = b.Backups().Length;
        r = b.Sync.Restore(Path.GetFileName(tampered));
        Check("restoring it: refused, nothing changed, no backup made",
              $"{(bool)r.Done} {b.WorldHash() == bHash} {b.Backups().Length == backupsBefore}", "False True True");
        File.Delete(tampered);
        // cut short, as a half-arrived download would be
        string cut = Path.Combine(cloud, "Snapshots", "20261030T060000Z_Mac-C.odsave");
        var whole = File.ReadAllBytes(Path.Combine(cloud, "Snapshots", snapA2));
        File.WriteAllBytes(cut, whole[..(whole.Length / 2)]);
        var cutEntry = ((IEnumerable<dynamic>)b.Sync.ListCloud()).First(e => (string)e.FileName == Path.GetFileName(cut));
        Check("cut short: listed as unreadable", ((object)cutEntry.State).ToString(), "Unreadable");
        Check("... not the candidate (A2 still is)", (string)b.Sync.Status().Candidate.FileName, snapA2);
        r = b.Sync.Restore(Path.GetFileName(cut));
        Check("... and restoring it refused, nothing changed", $"{(bool)r.Done} {b.WorldHash() == bHash} {b.Backups().Length == backupsBefore}", "False True True");
        File.Delete(cut);
        // a manifest naming a file outside the saves folder, or this Mac's settings.ini, with honest hashes
        string Crafted(string name, params string[] unlisted)
        {
            string p = Path.Combine(root, "crafted-" + Guid.NewGuid().ToString("N")[..6] + ".odsave");
            byte[] data = Encoding.UTF8.GetBytes("x");
            string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
            var fileType = IcType("SnapshotFile");
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(fileType))!;
            list.GetType().GetMethod("Add")!.Invoke(list, new[] { Activator.CreateInstance(fileType, name, 1L, sha) });
            dynamic m = Activator.CreateInstance(IcType("SnapshotManifest"))!;
            m.Device = "Mac-C"; m.CreatedUtc = t0; m.Files = (dynamic)list;
            m.WorldHash = (string)IcStatic("Snapshot", "WorldHash", list)!;
            using var fs = File.Create(p);
            using var z = new ZipArchive(fs, ZipArchiveMode.Create);
            using (var s = z.CreateEntry("manifest.json").Open())
                System.Text.Json.JsonSerializer.Serialize(s, (object)m, IcType("SnapshotManifest"), (System.Text.Json.JsonSerializerOptions)IcType("Snapshot").GetField("Json")!.GetValue(null)!);
            using (var s = z.CreateEntry("world/" + name).Open()) s.Write(data);
            foreach (string extra in unlisted) using (var s = z.CreateEntry(extra).Open()) s.Write(data);
            return p;
        }
        Check("a manifest naming ../evil.json: refused", Refusal(Crafted("../evil.json")).Contains("may not be restored"), true);
        Check("a manifest carrying settings.ini: refused (this Mac's own)", Refusal(Crafted("settings.ini")).Contains("may not be restored"), true);
        Check("a manifest naming /store.json (rooted): refused", Refusal(Crafted("/store.json")).Contains("may not be restored"), true);
        Check("... or C:\\store.json, or \\\\server\\store.json: refused",
              Refusal(Crafted("C:\\store.json")).Contains("may not be restored") && Refusal(Crafted("\\\\server\\store.json")).Contains("may not be restored"), true);
        Check("a zip holding a world file its manifest does not list: refused",
              Refusal(Crafted("store.json", "world/extra.json")).Contains("which its manifest does not list"), true);
        Check("... or an entry outside world/: refused", Refusal(Crafted("store.json", "elsewhere.json")).Contains("which its manifest does not list"), true);
        // APFS opens "ſettings.ini" (U+017F, long s) as settings.ini, which .NET's ordinal compare calls another file
        Check("a manifest naming \u017Fettings.ini or icloud\u017Faves.json (APFS opens this Mac's own): refused",
              Refusal(Crafted("\u017Fettings.ini")).Contains("may not be restored") && Refusal(Crafted("icloud\u017Faves.json")).Contains("may not be restored"), true);
        Check("... a Kelvin sign, a ligature, a full-width letter, a control character: refused",
              new[] { "\u212Aaizen.json", "\uFB00.json", "\uFF41.json", "a\u0007.json" }.All(n => Refusal(Crafted(n)).Contains("may not be restored")), true);
        Check("a Mac's name from a manifest, shown: line breaks gone, cut to 64", (string)IcStatic("CloudSync", "Shown", "Evil\n\nRestore it? Yes!" + new string('x', 80))!,
              "Evil  Restore it? Yes!" + new string('x', 42) + "...");
        Check("a manifest naming an honest file: accepted (the crafting is right)", Refusal(Crafted("store.json")), "accepted");

        Console.WriteLine("iCloud Saves: choosing iCloud's in the conflict - the backup holds B's world:");
        var earlier = b.Backups();
        b.Now = t0.AddHours(3);
        r = b.Sync.Restore(snapA2);
        Check("restored", (bool)r.Done, true);
        var made = b.Backups().Except(earlier).ToList();
        string newBackup = made.FirstOrDefault() ?? "";
        Check("one more backup folder, named saves-backup-yyyyMMdd-HHmmss", $"{made.Count} {System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(newBackup), @"^saves-backup-\d{8}-\d{6}(-\d+)?$")}", "1 True");
        Check("the backup holds B's changed store.json and B-only party file, byte for byte",
              $"{File.ReadAllText(Path.Combine(newBackup, "store.json"))} {File.ReadAllText(Path.Combine(newBackup, "party-Bob.json"))}", "{\"stock\":\"B's\"} [\"Bob\"]");
        Check("B's world is now A2's: the B-only files gone from the saves (they are in the backup)",
              $"{b.WorldHash() == a.WorldHash()} {File.Exists(Path.Combine(b.Saves, "party-Bob.json"))} {File.Exists(Path.Combine(b.Saves, "store.json"))}", "True False False");
        Check("B's settings.ini still B's", b.Read("settings.ini"), "B's own settings");
        Check("nothing half-written left in the saves", Directory.GetFiles(b.Saves).Any(f => f.EndsWith(".tmp")), false);

        Console.WriteLine("iCloud Saves: a snapshot not downloaded to this Mac is never restored from:");
        string stub = Path.Combine(cloud, "Snapshots", ".20991231T000000Z_Mac-C.odsave.icloud");
        File.WriteAllText(stub, "placeholder");
        var stubEntry = ((IEnumerable<dynamic>)b.Sync.ListCloud()).First(e => ((string)e.FileName).Contains("Mac-C"));
        Check("a .name.icloud placeholder: listed under its real name, not downloaded, its time and Mac from the name",
              $"{stubEntry.FileName} {stubEntry.State} {stubEntry.Device} {((DateTime)stubEntry.CreatedUtc):yyyy}", "20991231T000000Z_Mac-C.odsave NotDownloaded Mac-C 2099");
        Check("B unchanged since its sync, a newer one on its way: told it is not downloaded, nothing offered", b.Kind(), "CloudNotDownloaded");
        string bNow = b.WorldHash();
        backupsBefore = b.Backups().Length;
        r = b.Sync.Restore("20991231T000000Z_Mac-C.odsave");
        Check("restoring it: refused, nothing changed, no backup", $"{(bool)r.Done} {b.WorldHash() == bNow} {b.Backups().Length == backupsBefore} {((string)r.Message).Contains("not downloaded")}", "False True True True");
        b.Write("Bert.json", "{\"name\":\"Bert\"}");
        Check("B changed while it waits: a conflict, and the save point uploads nothing", $"{b.Kind()} {(bool)b.Sync.AutoUpload().Done}", "Conflict False");
        File.Delete(stub);
        File.Delete(Path.Combine(b.Saves, "Bert.json"));
        // a dataless file (Optimise Mac Storage): its name is there, its contents in iCloud. Garbage
        // here, so opening it would show as Unreadable; NotDownloaded proves it was never opened.
        string dl = Path.Combine(cloud, "Snapshots", "20991231T000000Z_Mac-D.odsave");
        File.WriteAllText(dl, "not a zip");
        b.Dataless.Add(Path.GetFileName(dl));
        var dlEntry = ((IEnumerable<dynamic>)b.Sync.ListCloud()).First(e => (string)e.FileName == Path.GetFileName(dl));
        Check("a dataless snapshot: not downloaded, never opened", ((object)dlEntry.State).ToString(), "NotDownloaded");
        Check("... restoring it refused", (bool)b.Sync.Restore(Path.GetFileName(dl)).Done, false);
        File.Delete(dl);
        b.Dataless.Clear();
        // a snapshot still being written by another Mac (its hidden .nosync name) is not listed at all
        File.WriteAllText(Path.Combine(cloud, "Snapshots", ".20991231T000000Z_Mac-E.odsave.partial.nosync"), "half");
        Check("a snapshot still being written (.nosync): not listed", ((IEnumerable<dynamic>)b.Sync.ListCloud()).Any(e => ((string)e.FileName).Contains("Mac-E")), false);
        File.Delete(Path.Combine(cloud, "Snapshots", ".20991231T000000Z_Mac-E.odsave.partial.nosync"));

        Console.WriteLine("iCloud Saves: a manifest with fields missing, or one dated ahead, is unreadable and never breaks the listing:");
        string nulled = Path.Combine(cloud, "Snapshots", "20261030T070000Z_Mac-F.odsave");
        using (var fs = File.Create(nulled))
        using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
        using (var w = new StreamWriter(z.CreateEntry("manifest.json").Open()))
            w.Write("{\"format\":1,\"device\":\"Mac-F\",\"worldHash\":null,\"files\":[]}");
        var listing = ((IEnumerable<dynamic>)b.Sync.ListCloud()).ToList();
        Check("a null world hash: listed as unreadable, the rest still listed",
              $"{listing.First(x => (string)x.FileName == Path.GetFileName(nulled)).State} {listing.Count(x => x.State.ToString() == "Ready") > 1}", "Unreadable True");
        b.Write("Anna.json", "{\"name\":\"Anna\",\"level\":5}");
        r = b.Sync.AutoUpload();
        Check("an unreadable snapshot not accounted for: the save point holds back and says why", $"{(bool)r.Done} {((string)r.Message).Contains("cannot read")}", "False True");
        Check("... it is not counted as seen by that", ((IEnumerable<object>)b.Sync.LoadState().Seen).Any(n => (string)n == Path.GetFileName(nulled)), false);
        File.Delete(nulled);
        string ahead = Path.Combine(cloud, "Snapshots", "20991231T000000Z_Mac-G.odsave");
        File.Copy(Path.Combine(cloud, "Snapshots", snapA2), ahead);
        using (var z = ZipFile.Open(ahead, ZipArchiveMode.Update))
        {
            var m = z.GetEntry("manifest.json")!;
            string json;
            using (var rd = new StreamReader(m.Open())) json = rd.ReadToEnd();
            m.Delete();
            using var w = new StreamWriter(z.CreateEntry("manifest.json").Open());
            w.Write(System.Text.RegularExpressions.Regex.Replace(json, "\"createdUtc\": \"[^\"]+\"", "\"createdUtc\": \"2099-12-31T00:00:00Z\""));
        }
        Check("a snapshot dated years ahead of this Mac's clock: unreadable, never the newest for ever",
              ((object)((IEnumerable<dynamic>)b.Sync.ListCloud()).First(x => (string)x.FileName == Path.GetFileName(ahead)).State).ToString(), "Unreadable");
        File.Delete(ahead);
        b.Write("Anna.json", "{\"name\":\"Anna\",\"level\":3}");
        Check("with those gone and B's change undone, B is in sync again", b.Kind(), "InSync");

        Console.WriteLine("iCloud Saves: keeping both - Upload now in a conflict leaves the other Mac's copy in iCloud:");
        a.Now = t0.AddHours(4);
        a.Write("Anna.json", "{\"name\":\"Anna\",\"level\":4}");
        a.Sync.AutoUpload();
        b.Now = t0.AddHours(5);
        b.Write("Anna.json", "{\"name\":\"Anna\",\"level\":9}");
        Check("both changed again: a conflict", b.Kind(), "Conflict");
        int count = SnapshotsIn(cloud).Length;
        r = b.Sync.UploadNow();
        Check("B keeps its own: uploaded, and every snapshot already there is still there", $"{(bool)r.Done} {SnapshotsIn(cloud).Length - count}", "True 1");
        Check("B in sync; A, unchanged since its own upload, sees B's as a fork (it grew from A2, not A's A3): a conflict, not \"newer\"",
              $"{b.Kind()} {a.Kind()}", "InSync Conflict");
        var listedNow = ((IEnumerable<dynamic>)a.Sync.ListCloud()).ToList();
        Check("... B's snapshot records the world it grew from: A2's",
              (string?)listedNow.First(x => ((string)x.FileName).Contains("_Mac-B")).ParentWorldHash == (string?)listedNow.First(x => (string)x.FileName == snapA2).WorldHash, true);

        Console.WriteLine("iCloud Saves: a newer world from a Mac that grew from this one's is newer; Upload now does not override it:");
        var e = new FakeMac(root, "Mac-E", cloud, t0.AddHours(6));
        string newestB = SnapshotsIn(cloud).Last(n => n.Contains("_Mac-B"));
        Check("Mac E restores B's world", (bool)e.Sync.Restore(newestB).Done, true);
        b.Now = t0.AddHours(7);
        b.Write("dungeon.json", "{\"rooms\":\"B2\"}");
        b.Sync.AutoUpload();
        Check("B uploads from that world: E, unchanged, is offered it as newer", e.Kind(), "CloudNewer");
        int cnt = SnapshotsIn(cloud).Length;
        r = e.Sync.UploadNow();
        Check("E's Upload now is refused (it would bury the newer world), nothing uploaded",
              $"{(bool)r.Done} {SnapshotsIn(cloud).Length - cnt} {((string)r.Message).Contains("newer world")} {e.Kind()}", "False 0 True CloudNewer");

        Console.WriteLine("iCloud Saves: a restore that fails part-way puts the backup back:");
        string eBefore = e.WorldHash();
        int eBackups = e.Backups().Length;
        int writes = 0;
        e.Sync.BeforeRestoreWrite = (Action<string>)(_ => { if (++writes == 2) throw new IOException("disk full (a test's)"); });
        r = e.Sync.Restore(SnapshotsIn(cloud).Last(n => n.Contains("_Mac-B")));
        e.Sync.BeforeRestoreWrite = null;
        Check("failed, said so, the saves as they were, the backup kept, no restore left marked",
              $"{(bool)r.Done} {((string)r.Message).Contains("put back as they were")} {e.WorldHash() == eBefore} {e.Backups().Length - eBackups} {(string?)e.Sync.LoadState().RestoreInProgress ?? "none"}",
              "False True True 1 none");
        Check("... nothing half-written left", Directory.GetFiles(e.Saves).Any(f => f.EndsWith(".tmp")), false);

        Console.WriteLine("iCloud Saves: a restore that fails in its last step, removing files, puts every file back:");
        var x = new FakeMac(root, "Mac-X", cloud, t0.AddHours(8));
        var y = new FakeMac(root, "Mac-Y", cloud, t0.AddHours(8));
        y.Write("common.json", "{\"y\":1}");
        y.Write("y-only.json", "{\"y\":2}");
        y.Sync.UploadNow();
        string ySnap = SnapshotsIn(cloud).Last(n => n.Contains("_Mac-Y"));
        x.Write("common.json", "{\"x\":1}");
        x.Write("x-only.json", "{\"x\":2}");
        string xBefore = x.WorldHash();
        int deletes = 0;
        x.Sync.BeforeRestoreDelete = (Action<string>)(_ => { deletes++; throw new IOException("disk gone (a test's)"); });
        r = x.Sync.Restore(ySnap);
        x.Sync.BeforeRestoreDelete = null;
        Check("failed at the removal, after every write: the snapshot's own file gone again, the one it lacks still here, all as it was",
              $"{deletes} {(bool)r.Done} {x.WorldHash() == xBefore} {File.Exists(Path.Combine(x.Saves, "y-only.json"))} {x.Read("x-only.json")} {x.Read("common.json")}",
              "1 False True False {\"x\":2} {\"x\":1}");
        Check("... no restore left marked, nothing half-written", $"{(string?)x.Sync.LoadState()?.RestoreInProgress ?? "none"} {Directory.GetFiles(x.Saves).Any(f => f.EndsWith(".tmp"))}", "none False");
        x.Sync.BeforeRestoreWrite = (Action<string>)(n => { if (n == "y-only.json") throw new IOException("disk full (a test's)"); });
        r = x.Sync.Restore(ySnap);
        x.Sync.BeforeRestoreWrite = null;
        Check("failed writing the file only the snapshot has, after the shared one: the shared one put back",
              $"{(bool)r.Done} {x.WorldHash() == xBefore} {x.Read("common.json")}", "False True {\"x\":1}");

        Console.WriteLine("iCloud Saves: pruning keeps a Mac's last ten of its own, and never another's:");
        var d = new FakeMac(root, "Mac-D", cloud, t0.AddDays(1));
        for (int i = 0; i < 12; i++)
        {
            d.Now = t0.AddDays(1).AddMinutes(i);
            d.Write("dungeon.json", "{\"rooms\":" + i + "}");
            d.Sync.UploadNow();
        }
        var all = SnapshotsIn(cloud);
        Check("Mac D: ten of its twelve left, the newest ten", $"{all.Count(n => n.Contains("_Mac-D"))} {all.Contains("20261031T010000Z_Mac-D.odsave")} {all.Contains("20261031T011100Z_Mac-D.odsave")}", "10 False True");
        Check("A's and B's untouched", $"{all.Count(n => n.Contains("_Mac-A"))} {all.Count(n => n.Contains("_Mac-B"))}", "3 2");
        // a planted snapshot claiming Mac D's name is not D's to delete: pruning goes by what D uploaded
        string planted = Path.Combine(cloud, "Snapshots", "20261030T000000Z_Mac-D.odsave");
        File.Copy(Path.Combine(cloud, "Snapshots", SnapshotsIn(cloud).First(n => n.Contains("_Mac-D"))), planted);
        d.Now = t0.AddDays(2);
        d.Write("dungeon.json", "{\"rooms\":99}");
        d.Sync.UploadNow();
        Check("a copy planted under Mac D's name and older than all of D's: left alone by D's pruning", File.Exists(planted), true);
        File.Delete(planted);
        Check("no half-written snapshot left anywhere", all.Any(n => n.EndsWith(".nosync")), false);
        // one of D's own evicted by iCloud to a placeholder is still D's by its record, and pruned
        string oldestD = SnapshotsIn(cloud).First(n => n.Contains("_Mac-D") && !n.StartsWith('.'));
        File.Delete(Path.Combine(cloud, "Snapshots", oldestD));
        string evicted = Path.Combine(cloud, "Snapshots", "." + oldestD + ".icloud");
        File.WriteAllText(evicted, "placeholder");
        d.Now = t0.AddDays(3);
        d.Write("dungeon.json", "{\"rooms\":100}");
        d.Sync.UploadNow();
        Check("one of D's own evicted to a placeholder: pruned as its oldest, ten left", $"{File.Exists(evicted)} {SnapshotsIn(cloud).Count(n => n.Contains("_Mac-D"))}", "False 10");

        Console.WriteLine("iCloud Saves: time-based retention keeps the last ten plus one a day for seven days:");
        string dailyCloud = Path.Combine(root, "cloud-daily");
        var eMac = new FakeMac(root, "Mac-E", dailyCloud, t0);
        // 2 uploads on each of 9 earlier days (days 0..8), then 11 uploads on day 9
        for (int day = 0; day < 9; day++)
            for (int k = 0; k < 2; k++)
            {
                eMac.Now = t0.AddDays(day).AddHours(k + 1);
                eMac.Write("dungeon.json", $"{{\"day\":{day},\"k\":{k}}}");
                eMac.Sync.UploadNow();
            }
        for (int k = 0; k < 11; k++)
        {
            eMac.Now = t0.AddDays(9).AddMinutes(k);
            eMac.Write("dungeon.json", $"{{\"day\":9,\"k\":{k}}}");
            eMac.Sync.UploadNow();
        }
        var keptE = SnapshotsIn(dailyCloud);
        // Day 9: newest 10 kept (k=1..10; k=0 pruned). Days 2..8 (7 prior days): newest 1 each (k=1) kept; k=0 pruned. Days 0..1 (>7 days old): all pruned.
        Check("last 10 on day 9 plus 1 per day for days 2..8 = 17 kept; days 0..1 pruned",
              $"{keptE.Length} {keptE.Any(n => n.StartsWith("20261030"))} {keptE.Any(n => n.StartsWith("20261031"))} {keptE.Count(n => n.StartsWith("20261101"))} {keptE.Count(n => n.StartsWith("20261107"))} {keptE.Count(n => n.StartsWith("20261108"))}",
              "17 False False 1 1 10");

        Console.WriteLine("iCloud Saves: oversized snapshot gets a clear message:");
        string bigCloud = Path.Combine(root, "cloud-big");
        var bigMac = new FakeMac(root, "Mac-Big", bigCloud, t0);
        byte[] junk = new byte[17 * 1024 * 1024];
        new Random(42).NextBytes(junk);
        File.WriteAllBytes(Path.Combine(bigMac.Saves, "huge.json"), junk);
        dynamic bigRes = bigMac.Sync.UploadNow();
        Check("oversized archive refused with clear message and no snapshot written",
              $"{(bool)bigRes.Done} {((string)bigRes.Message).Contains("16 MB limit")} {SnapshotsIn(bigCloud).Length}",
              "False True 0");

        Console.WriteLine("iCloud Saves: the seen list, grown long, loses only names no longer in iCloud Drive:");
        dynamic longList = d.Sync.LoadState();
        for (int i = 0; i < 600; i++) longList.Seen.Add($"20200101T{i:000000}Z_Gone.odsave");
        d.Sync.SaveState(longList);
        Check("missing once (a folder iCloud may still be filling): nothing dropped yet",
              ((IEnumerable<object>)d.Sync.LoadState().Seen).Count(o => ((string)o).Contains("_Gone")), 600);
        d.Sync.SaveState(d.Sync.LoadState());
        var seenNow = ((IEnumerable<object>)d.Sync.LoadState().Seen).Select(o => (string)o).ToList();
        Check("missing at the next trim too: the gone names dropped, every snapshot still there kept",
              $"{seenNow.Any(n => n.Contains("_Gone"))} {SnapshotsIn(cloud).Where(n => !n.StartsWith('.')).All(n => seenNow.Contains(n, StringComparer.OrdinalIgnoreCase))}", "False True");
        var offline = new FakeMac(root, "Mac-Off", Path.Combine(root, "no-cloud-here"), t0);
        dynamic offList = Activator.CreateInstance(IcType("SyncState"))!;
        for (int i = 0; i < 600; i++) offList.Seen.Add($"20200101T{i:000000}Z_Gone.odsave");
        offline.Sync.SaveState(offList);
        Check("... and with iCloud Drive unreadable, nothing dropped", ((IEnumerable<object>)offline.Sync.LoadState().Seen).Count(), 600);

        Console.WriteLine("iCloud Saves: where iCloud Drive is, and isn't:");
        string missing = Path.Combine(root, "no-icloud-here");
        Environment.SetEnvironmentVariable("MORDOR_ICLOUD", missing);
        var (folder, problem) = ((string?, string?))IcStatic("ICloudDrive", "Locate")!;
        Check("iCloud Drive turned off (its folder missing): no folder, and the reason names iCloud Drive",
              $"{folder == null} {problem?.Contains("iCloud Drive is not turned on")}", "True True");
        Check("... and nothing is made in its place", Directory.Exists(missing), false);
        Environment.SetEnvironmentVariable("MORDOR_ICLOUD", Path.GetDirectoryName(cloud));
        Check("iCloud Drive there: its OpenDejenol folder", ((ValueTuple<string?, string?>)IcStatic("ICloudDrive", "Locate")!).Item1, cloud);
        if (OperatingSystem.IsMacOS())
        {
            string anyFile = Path.Combine(a.Saves, "Anna.json");
            Check("macOS: stat(2) read at the offsets the dataless test uses (st_size at 96 is the file's length)",
                  (long)IcStatic("ICloudDrive", "StatSize", anyFile)!, new FileInfo(anyFile).Length);
            Check("macOS: an ordinary file is not dataless", (bool)IcStatic("ICloudDrive", "IsDataless", anyFile)!, false);
        }
        else Check("off macOS: stat(2) is not called", (long)IcStatic("ICloudDrive", "StatSize", Path.Combine(a.Saves, "Anna.json"))!, -1L);
    }

    // ---------------------------------------------------------------- several uploads in a row from the other Mac

    /// <summary>
    /// Two Macs on one iCloud Drive folder of their own. The other Mac uploads at every return to town,
    /// so by the time this one looks there are often several of its snapshots in a row: the newest's
    /// parent is the other Mac's previous upload, not this Mac's world. The chain is followed back
    /// through them; only a chain that does not reach this Mac's world, or cannot be followed, is a conflict.
    /// </summary>
    private static void ChainChecks(string root)
    {
        Console.WriteLine("iCloud Saves: the other Mac uploaded twice since this one synced - newer, not a conflict:");
        string home = Path.Combine(root, "chain");
        string cloud = Path.Combine(home, "cloud");
        Directory.CreateDirectory(cloud);
        var t0 = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
        var a = new FakeMac(home, "Mac-A", cloud, t0);
        var b = new FakeMac(home, "Mac-B", cloud, t0);
        a.Write("dungeon.json", "{\"rooms\":\"A1\"}");
        a.Write("Anna.json", "{\"level\":3}");
        a.Sync.AutoUpload();
        string a1 = SnapshotsIn(cloud).Single();
        b.Now = t0.AddMinutes(10);
        b.Sync.Restore(a1);
        b.Now = t0.AddHours(1); b.Write("Anna.json", "{\"level\":4}");
        Check("B back in town: uploaded", (bool)b.Sync.AutoUpload().Done, true);
        b.Now = t0.AddHours(2); b.Write("Anna.json", "{\"level\":5}");
        Check("B back in town again: uploaded again", (bool)b.Sync.AutoUpload().Done, true);
        var bSnaps = SnapshotsIn(cloud).Where(n => n.Contains("_Mac-B")).ToArray();
        var listed = ((IEnumerable<dynamic>)a.Sync.ListCloud()).ToList();
        dynamic Entry(string name) => listed.First(x => (string)x.FileName == name);
        Check("B's newest grew from B's first, not from A's world (the case the newest's parent alone got wrong)",
              $"{(string?)Entry(bSnaps[1]).ParentWorldHash == (string?)Entry(bSnaps[0]).WorldHash} {(string?)Entry(bSnaps[1]).ParentWorldHash == a.WorldHash()}",
              "True False");
        dynamic st = a.Sync.Status();
        Check("A, unchanged: iCloud's is newer, B's newest offered", $"{st.Kind} {st.Candidate.FileName}", $"CloudNewer {bSnaps[1]}");
        Check("... and the save point uploads nothing over it", (bool)a.Sync.AutoUpload().Done, false);

        Console.WriteLine("iCloud Saves: the same, with A changed too - a conflict:");
        a.Now = t0.AddHours(3);
        a.Write("dungeon.json", "{\"rooms\":\"A2\"}");
        Check("A changed as well: a conflict", a.Kind(), "Conflict");
        a.Write("dungeon.json", "{\"rooms\":\"A1\"}");
        Check("A's change undone: newer again", a.Kind(), "CloudNewer");

        Console.WriteLine("iCloud Saves: a chain that cannot be followed is a conflict, never newer:");
        string b1 = Path.Combine(cloud, "Snapshots", bSnaps[0]);
        a.Dataless.Add(bSnaps[0]);
        Check("B's first not downloaded to A yet (its world unknown): a conflict", a.Kind(), "Conflict");
        a.Dataless.Clear();
        byte[] kept = File.ReadAllBytes(b1);
        File.WriteAllBytes(b1, Encoding.UTF8.GetBytes("not a snapshot"));
        Check("B's first unreadable: a conflict", a.Kind(), "Conflict");
        File.WriteAllBytes(b1, kept);
        Check("B's first whole again: newer", a.Kind(), "CloudNewer");
        File.Move(b1, b1 + ".aside");
        Check("B's first gone from iCloud Drive (the chain broken): a conflict", a.Kind(), "Conflict");
        File.Move(b1 + ".aside", b1);

        Console.WriteLine("iCloud Saves: a real fork, several uploads deep, is a conflict:");
        // C restores A's first world; A then moves on and uploads; C plays on from the older world and
        // uploads twice. C's chain leads back to A1, which is not the world A last synced (A3)
        var c = new FakeMac(home, "Mac-C", cloud, t0.AddHours(4));
        c.Sync.Restore(a1);
        a.Sync.Restore(bSnaps[1]);
        a.Now = t0.AddHours(5); a.Write("dungeon.json", "{\"rooms\":\"A3\"}");
        Check("A takes B's world, changes it and uploads", (bool)a.Sync.AutoUpload().Done, true);
        c.Now = t0.AddHours(6); c.Write("Anna.json", "{\"level\":7}"); c.Sync.UploadNow();
        c.Now = t0.AddHours(7); c.Write("Anna.json", "{\"level\":8}");
        Check("C uploads twice from A's first world", (bool)c.Sync.AutoUpload().Done, true);
        st = a.Sync.Status();
        Check("A, unchanged since its upload: C's newest is a fork - a conflict", $"{st.Kind} {((string)st.Candidate.FileName).Contains("_Mac-C")}", "Conflict True");
    }

    // ---------------------------------------------------------------- the questions at the main menu

    private static void StartChecks(string root)
    {
        Console.WriteLine("iCloud Saves: the start-up check at the main menu:");
        string cloud = Path.Combine(root, "start-cloud");
        Directory.CreateDirectory(cloud);
        var t0 = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var src = new FakeMac(root, "Start-A", cloud, t0);
        src.Write("dungeon.json", "{\"rooms\":\"S1\"}");
        src.Write("Anna.json", "{\"name\":\"Anna\"}");
        src.Sync.UploadNow();

        var log = new List<string>();
        // each Mac's "told at start" setting, as its settings.ini would keep it
        var told = new Dictionary<string, string>();
        dynamic Saves(FakeMac mac, (string?, string?) where, bool check = true, bool upload = true) =>
            Activator.CreateInstance(IcType("CloudSaves"), mac.Saves, (Func<(string?, string?)>)(() => where), mac.Name,
                                     (Func<bool>)(() => upload), (Func<bool>)(() => check),
                                     (Action<string>)(l => log.Add(l)), (Action<string, Exception?>)((w, e) => log.Add("ERROR " + w + " " + e)),
                                     (Func<DateTime>)(() => mac.Now), (Func<string, bool>)(p => mac.Dataless.Contains(Path.GetFileName(p))),
                                     (Func<string>)(() => told.TryGetValue(mac.Name, out var t) ? t : ""),
                                     (Action<string>)(v => told[mac.Name] = v))!;
        void Start(dynamic saves, FakeMenu menu, string savesDir) => ((Task)saves.MainMenu(menu.Context(savesDir))).GetAwaiter().GetResult();

        // a newer world, declined
        var fresh = new FakeMac(root, "Start-B", cloud, t0.AddHours(1));
        fresh.Write("settings.ini", "B");
        var menu = new FakeMenu();
        menu.Answers.Enqueue(1);
        Start(Saves(fresh, (cloud, null)), menu, fresh.Saves);
        Check("a newer world in iCloud: asked Restore or Not now; Not now changes nothing, makes no backup, does not quit",
              $"{menu.Asked.Count} {menu.Asked[0].EndsWith("[Restore|Not now]")} {File.Exists(Path.Combine(fresh.Saves, "dungeon.json"))} {fresh.Backups().Length} {menu.Quits}",
              "1 True False 0 0");
        Check("... the question names the other Mac and says the saves are backed up first", menu.Asked[0].Contains("Start-A") && menu.Asked[0].Contains("saves-backup"), true);
        // accepted
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(0);
        Start(Saves(fresh, (cloud, null)), menu, fresh.Saves);
        Check("Restore: restored, backed up first, told, then the program closes so every mod reads the restored world",
              $"{fresh.Read("dungeon.json")} {fresh.Backups().Length} {menu.Asked.Count} {menu.Asked[1].Contains("will now close")} {menu.Quits}",
              "{\"rooms\":\"S1\"} 1 2 True 1");

        // a conflict: Keep this Mac's uploads; Decide later does nothing
        src.Now = t0.AddHours(2); src.Write("dungeon.json", "{\"rooms\":\"S2\"}"); src.Sync.UploadNow();
        fresh.Now = t0.AddHours(3); fresh.Write("Anna.json", "{\"name\":\"Anna\",\"b\":1}");
        int before = SnapshotsIn(cloud).Length;
        menu = new FakeMenu();
        menu.Answers.Enqueue(2);
        Start(Saves(fresh, (cloud, null)), menu, fresh.Saves);
        Check("a conflict: asked Use iCloud's, Keep this Mac's or Decide later; Decide later uploads nothing and quits nothing",
              $"{menu.Asked[0].EndsWith("[Use iCloud's|Keep this Mac's|Decide later]")} {SnapshotsIn(cloud).Length - before} {menu.Quits}", "True 0 0");
        menu = new FakeMenu();
        menu.Answers.Enqueue(1);
        Start(Saves(fresh, (cloud, null)), menu, fresh.Saves);
        Check("Keep this Mac's: uploaded as a new snapshot, iCloud's kept", $"{SnapshotsIn(cloud).Length - before} {fresh.Kind()} {menu.Quits}", "1 InSync 0");

        // the check switched off: nothing asked, even with a newer world there
        src.Now = t0.AddHours(4); src.Write("dungeon.json", "{\"rooms\":\"S3\"}"); src.Sync.UploadNow();
        menu = new FakeMenu();
        Start(Saves(fresh, (cloud, null), check: false), menu, fresh.Saves);
        Check("Check iCloud at start off: nothing asked", menu.Asked.Count, 0);

        // a restore asked for during a game: refused uploads meanwhile, offered at the start, cancellable
        dynamic s = Saves(fresh, (cloud, null), check: false);
        string newest = (string)fresh.Sync.Status().Candidate.FileName;
        s.RestoreAtNextStart(newest);
        fresh.Write("Anna.json", "{\"name\":\"Anna\",\"b\":2}");
        before = SnapshotsIn(cloud).Length;
        s.SavePoint(SavePointKind.GameEnded);
        Check("a restore waiting: the save point uploads nothing", SnapshotsIn(cloud).Length - before, 0);
        menu = new FakeMenu();
        menu.Answers.Enqueue(1);
        Start(s, menu, fresh.Saves);
        Check("the next start offers it (Restore or Cancel the restore), even with the check off; cancelled, it is gone",
              $"{menu.Asked[0].EndsWith("[Restore|Cancel the restore]")} {menu.Asked[0].Contains("have changed since")} {(string?)fresh.Sync.LoadState().PendingRestore ?? "none"} {menu.Quits}",
              "True True none 0");

        // a restore waiting that cannot be done at the start: never left to hold every upload unseen
        var waits = new FakeMac(root, "Start-F", cloud, t0.AddHours(5));
        string newestS = SnapshotsIn(cloud).Last(n => n.Contains("_Start-A"));
        waits.Sync.Restore(newestS);
        string olderS = SnapshotsIn(cloud).First(n => n.Contains("_Start-A"));
        dynamic w = Saves(waits, (cloud, null), check: false);
        w.RestoreAtNextStart(olderS);
        waits.Now = t0.AddHours(6); waits.Write("Anna.json", "{\"name\":\"Anna\",\"f\":1}");
        string playedF = waits.Read("Anna.json");
        int backupsF = waits.Backups().Length;
        waits.Dataless.Add(olderS);
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(0);
        Start(w, menu, waits.Saves);
        Check("a restore waiting, not downloaded at the start: told why, then asked Keep it waiting or Cancel the restore, each said plainly",
              $"{menu.Asked.Count} {menu.Asked[1].Contains("not downloaded")} {menu.Asked[1].EndsWith("[Keep it waiting|Cancel the restore]")} "
              + $"{menu.Asked[1].Contains("nothing is uploaded from this Mac until then")} {menu.Asked[1].Contains("uploaded as usual")}",
              "2 True True True True");
        Check("... kept waiting: still waiting, the saves untouched, no backup, no quit",
              $"{(string?)waits.Sync.LoadState().PendingRestore == olderS} {waits.Read("Anna.json") == playedF} {waits.Backups().Length - backupsF} {menu.Quits}", "True True 0 0");
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(-1);
        Start(w, menu, waits.Saves);
        Check("... the box closed with neither pressed: kept waiting (it changes nothing)", (string?)waits.Sync.LoadState().PendingRestore == olderS, true);
        before = SnapshotsIn(cloud).Length;
        w.SavePoint(SavePointKind.ArrivedInTown);
        Check("... and while it waits the save point says how to upload again", $"{SnapshotsIn(cloud).Length - before} {((string)w.LastMessage).Contains("Cancel the restore")}", "0 True");
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(1);
        Start(w, menu, waits.Saves);
        Check("... cancelled: no longer waiting, the saves untouched", $"{(string?)waits.Sync.LoadState().PendingRestore ?? "none"} {waits.Read("Anna.json") == playedF} {menu.Quits}", "none True 0");
        w.SavePoint(SavePointKind.ArrivedInTown);
        Check("... and the next save point uploads this Mac's game, the snapshot asked for still in iCloud",
              $"{SnapshotsIn(cloud).Length - before} {SnapshotsIn(cloud).Contains(olderS)}", "1 True");
        waits.Dataless.Clear();
        w.RestoreAtNextStart("20261101T230000Z_Gone.odsave");
        waits.Now = t0.AddHours(7); waits.Write("Anna.json", "{\"name\":\"Anna\",\"f\":2}");
        menu = new FakeMenu();
        menu.Answers.Enqueue(0);
        before = SnapshotsIn(cloud).Length;
        Start(w, menu, waits.Saves);
        Check("a restore waiting of a snapshot gone from iCloud Drive: cancelled, and told so with an OK",
              $"{menu.Asked.Count} {menu.Asked[1].Contains("not in iCloud Drive any more")} {menu.Asked[1].Contains("is cancelled")} {menu.Asked[1].EndsWith("[OK]")} "
              + $"{(string?)waits.Sync.LoadState().PendingRestore ?? "none"} {menu.Quits}",
              "2 True True True none 0");
        w.SavePoint(SavePointKind.ArrivedInTown);
        Check("... and uploads go on as usual", SnapshotsIn(cloud).Length - before, 1);

        // no iCloud Drive: said once, nothing written
        var none = new FakeMac(root, "Start-C", cloud, t0);
        menu = new FakeMenu();
        Start(Saves(none, (null, "iCloud Drive is not turned on on this Mac.")), menu, none.Saves);
        Check("no iCloud Drive: the reason told at start, nothing written to the saves",
              $"{menu.Asked.Count} {menu.Asked[0].Contains("not turned on")} {Directory.GetFiles(none.Saves).Length} {menu.Quits}", "1 True 0 0");
        menu = new FakeMenu();
        dynamic offAgain = Saves(none, (null, "iCloud Drive is not turned on on this Mac."));
        Start(offAgain, menu, none.Saves);
        Check("... and not again at the next start, though the window still says it", $"{menu.Asked.Count} {((string)offAgain.LastMessage).Contains("not turned on")}", "0 True");
        Start(Saves(none, (cloud, null)), new FakeMenu(), none.Saves);
        menu = new FakeMenu();
        Start(Saves(none, (null, "iCloud Drive is not turned on on this Mac.")), menu, none.Saves);
        Check("... turned on and off again: told again", menu.Asked.Count, 1);

        // a restore the program did not live to finish: caught at the next start, the backup offered back
        var cut = new FakeMac(root, "Start-D", cloud, t0.AddHours(8));
        cut.Write("Anna.json", "{\"name\":\"Anna\",\"mine\":1}");
        string cutHash = cut.WorldHash();
        string backup = (string)cut.Sync.Backup();
        cut.Write("Anna.json", "{\"name\":\"Anna\",\"half\":1}");
        cut.Write("store.json", "{\"half\":1}");
        dynamic marked = Activator.CreateInstance(IcType("SyncState"))!;
        marked.RestoreInProgress = backup;
        cut.Sync.SaveState(marked);
        Check("an interrupted restore: the save point uploads nothing", $"{(bool)cut.Sync.AutoUpload().Done}", "False");
        var cutBackups = cut.Backups();
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(0);
        Start(Saves(cut, (cloud, null)), menu, cut.Saves);
        Check("the next start asks Put them back or Keep; put back: the saves from before, the mark gone, the program closes",
              $"{menu.Asked[0].EndsWith("[Put them back|Keep the saves as they are]")} {cut.WorldHash() == cutHash} {(string?)cut.Sync.LoadState().RestoreInProgress ?? "none"} {menu.Quits}",
              "True True none 1");
        var putAside = cut.Backups().Except(cutBackups).ToList();
        Check("... the saves as they were before putting back copied to a backup first (a game may have been played on them)",
              $"{putAside.Count} {(putAside.Count == 1 ? File.ReadAllText(Path.Combine(putAside[0], "store.json")) : "")}", "1 {\"half\":1}");

        // the same, kept as they are: the half-restored saves differ from the world recorded, so they
        // would look like this Mac's newer world - nothing goes up on its own until Upload now
        var kept = new FakeMac(root, "Start-E", cloud, t0.AddHours(9));
        kept.Write("Anna.json", "{\"name\":\"Anna\",\"mine\":1}");
        kept.Sync.UploadNow();
        string keptBackup = (string)kept.Sync.Backup();
        kept.Write("Anna.json", "{\"name\":\"Anna\",\"half\":1}");
        dynamic keptMark = kept.Sync.LoadState();
        keptMark.RestoreInProgress = keptBackup;
        kept.Sync.SaveState(keptMark);
        menu = new FakeMenu();
        menu.Answers.Enqueue(-1);
        Start(Saves(kept, (cloud, null)), menu, kept.Saves);
        Check("the box closed with neither pressed: taken as Keep - the mark gone, uploads held, no file changed",
              $"{menu.Asked.Count} {(string?)kept.Sync.LoadState().RestoreInProgress ?? "none"} {(bool)kept.Sync.LoadState().UploadsHeld} {kept.Read("Anna.json")} {menu.Quits}",
              "2 none True {\"name\":\"Anna\",\"half\":1} 0");
        keptMark = kept.Sync.LoadState();
        keptMark.RestoreInProgress = keptBackup;
        keptMark.UploadsHeld = false;
        kept.Sync.SaveState(keptMark);
        menu = new FakeMenu();
        menu.Answers.Enqueue(1); menu.Answers.Enqueue(0);
        dynamic keptSaves = Saves(kept, (cloud, null));
        before = SnapshotsIn(cloud).Length;
        Start(keptSaves, menu, kept.Saves);
        Check("Keep: the mark gone, uploads held, told so, nothing uploaded at that start, no quit",
              $"{(string?)kept.Sync.LoadState().RestoreInProgress ?? "none"} {(bool)kept.Sync.LoadState().UploadsHeld} {menu.Asked.Count} {menu.Asked[1].Contains("Upload now")} {SnapshotsIn(cloud).Length - before} {menu.Quits}",
              "none True 2 True 0 0");
        dynamic held = kept.Sync.AutoUpload();
        Check("... a save point then uploads nothing and says why", $"{(bool)held.Done} {((string)held.Message).Contains("interrupted restore")} {SnapshotsIn(cloud).Length - before}", "False True 0");
        Check("... and the window says so", ((string)keptSaves.Describe().Head).Contains("until you Upload now"), true);
        dynamic sent = kept.Sync.UploadNow();
        Check("Upload now sends them and lifts the hold; the next save point is as ever",
              $"{(bool)sent.Done} {(bool)kept.Sync.LoadState().UploadsHeld} {SnapshotsIn(cloud).Length - before} {kept.Kind()}", "True False 1 InSync");

        // a restore asked for, then a newer world from another Mac before the next start: the question
        // says so, and the restore does not bury it
        var asker = new FakeMac(root, "Start-F", cloud, t0.AddHours(10));
        var other = new FakeMac(root, "Start-G", cloud, t0.AddHours(10));
        other.Write("dungeon.json", "{\"rooms\":\"G1\"}");
        other.Sync.UploadNow();
        string g1 = SnapshotsIn(cloud).Last(n => n.Contains("_Start-G"));
        asker.Sync.Restore(g1);
        asker.Now = t0.AddHours(10.5);
        asker.Write("dungeon.json", "{\"rooms\":\"F1\"}");
        asker.Sync.UploadNow();
        string f1 = SnapshotsIn(cloud).Last(n => n.Contains("_Start-F"));
        asker.Sync.RequestRestore(g1);
        other.Now = t0.AddHours(11);
        other.Write("dungeon.json", "{\"rooms\":\"G2\"}");
        other.Sync.UploadNow();
        string g2 = SnapshotsIn(cloud).Last(n => n.Contains("_Start-G"));
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(0);
        Start(Saves(asker, (cloud, null)), menu, asker.Saves);
        Check("the restore asked for is offered, and the question names the newer world saved since",
              $"{menu.Asked[0].Contains("iCloud also has a newer world")} {menu.Asked[0].Contains("Start-G")} {asker.Read("dungeon.json")}", "True True {\"rooms\":\"G1\"}");
        Check("... the newer one is not marked seen by the restore: it is news at the next start",
              $"{((IEnumerable<object>)asker.Sync.LoadState().Seen).Any(n => (string)n == g2)} {asker.Kind()} {(string)asker.Sync.Status().Candidate.FileName == g2}", "False CloudNewer True");
        Check("... while this Mac's own upload and the one restored are", ((IEnumerable<object>)asker.Sync.LoadState().Seen).Count(n => (string)n == g1 || (string)n == f1), 2);
        Check("nothing logged as an error in all that (every instance's log)", string.Join(" | ", log.Where(l => l.StartsWith("ERROR"))), "");
    }
}

/// <summary>
/// A test mod hooking the main menu's moment (API version 17): asked, it asks the menu's box once, which
/// the probe counts (the mod is loaded from a copy of this assembly, so a static here would be another's).
/// </summary>
public sealed class MenuHookMod : IMod
{
    public void Load(IModContext ctx) => ctx.Actions.MainMenuOpened = m => m.Ask("MenuHook", "asked", "OK");
}
