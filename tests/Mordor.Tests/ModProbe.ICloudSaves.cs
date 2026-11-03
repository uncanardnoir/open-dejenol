using System.IO.Compression;
using System.Reflection;
using System.Text;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The iCloud Saves mod and the two hooks added for it (API version 15, docs/MODDING.md): with mods off
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
            StartChecks(root);
        }
        finally { Environment.SetEnvironmentVariable("MORDOR_ICLOUD", oldEnv); }
    }

    // ---------------------------------------------------------------- the hooks and the mod loaded

    private static void HookChecks(string root, string bundled, string cloudDocs, string cloud)
    {
        Console.WriteLine("iCloud Saves: the manifest, and the two hooks with mods off (API version 15):");
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
        Check("the menu asks the host first, and awaits the moment only when a mod hooks it, before the preload",
              PortSource("src/Mordor.App", "TitleWindow.axaml.cs").Replace("\r\n", "\n") is var tw
              && System.Text.RegularExpressions.Regex.IsMatch(tw, @"_preloadTried = true;[^}]*if \(Mordor\.Modding\.ModHost\.Active\.MainMenuHooked\)\s*\{\s*await ModStartup\.MainMenuOpened\(this\);[^}]*\}\s*if \(Preloaded\(\) is Character ready\)"), true);
        string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        Check("the city raises Saved after its arrival save, and after its last save as it closes",
              city.Contains("RefreshSummary(); Save();\n        // and, the arrival saved, that the saves are on disk (docs/MODDING.md, Saved; API version 15)\n        Mordor.Modding.ModHost.Active.SavePointReached(Mordor.Modding.SavePointKind.ArrivedInTown);")
              && System.Text.RegularExpressions.Regex.IsMatch(city, @"else \{ SaveWindowLayout\(\); Save\(\); \}\s*(//[^\n]*\n\s*)*Mordor\.Modding\.ModHost\.Active\.SavePointReached\(Mordor\.Modding\.SavePointKind\.GameEnded\);"), true);

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
        Check("the world's files are synced; this Mac's settings.ini, mods.log, the mod's record, crash.log, hidden and half-written files are not",
              string.Join(",", new[] { "dungeon.json", "Anna.json", "party-Anna.json", "MDATA9.MDR", "settings.ini", "mods.log", "icloudsaves.json", "crash.log", ".DS_Store", "dungeon.json.tmp", "x.partial.nosync" }
                  .Select(n => $"{n}={(Synced(n) ? 1 : 0)}")),
              "dungeon.json=1,Anna.json=1,party-Anna.json=1,MDATA9.MDR=1,settings.ini=0,mods.log=0,icloudsaves.json=0,crash.log=0,.DS_Store=0,dungeon.json.tmp=0,x.partial.nosync=0");
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
        string Crafted(string name)
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
            return p;
        }
        Check("a manifest naming ../evil.json: refused", Refusal(Crafted("../evil.json")).Contains("may not be restored"), true);
        Check("a manifest carrying settings.ini: refused (this Mac's own)", Refusal(Crafted("settings.ini")).Contains("may not be restored"), true);
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
        dynamic Saves(FakeMac mac, (string?, string?) where, bool check = true, bool upload = true) =>
            Activator.CreateInstance(IcType("CloudSaves"), mac.Saves, (Func<(string?, string?)>)(() => where), mac.Name,
                                     (Func<bool>)(() => upload), (Func<bool>)(() => check),
                                     (Action<string>)(l => log.Add(l)), (Action<string, Exception?>)((w, e) => log.Add("ERROR " + w + " " + e)),
                                     (Func<DateTime>)(() => mac.Now), (Func<string, bool>)(_ => false))!;
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

        // no iCloud Drive: said once, nothing written
        var none = new FakeMac(root, "Start-C", cloud, t0);
        menu = new FakeMenu();
        Start(Saves(none, (null, "iCloud Drive is not turned on on this Mac.")), menu, none.Saves);
        Check("no iCloud Drive: the reason told at start, nothing written to the saves",
              $"{menu.Asked.Count} {menu.Asked[0].Contains("not turned on")} {Directory.GetFiles(none.Saves).Length} {menu.Quits}", "1 True 0 0");

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
        menu = new FakeMenu();
        menu.Answers.Enqueue(0); menu.Answers.Enqueue(0);
        Start(Saves(cut, (cloud, null)), menu, cut.Saves);
        Check("the next start asks Put them back or Keep; put back: the saves from before, the mark gone, the program closes",
              $"{menu.Asked[0].EndsWith("[Put them back|Keep the saves as they are]")} {cut.WorldHash() == cutHash} {(string?)cut.Sync.LoadState().RestoreInProgress ?? "none"} {menu.Quits}",
              "True True none 1");
        Check("nothing logged as an error in all that (every instance's log)", string.Join(" | ", log.Where(l => l.StartsWith("ERROR"))), "");
    }
}
