using System.Reflection;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The mod system (docs/MODDING.md), asserted headless: the loader, the hooks' default answers, the
/// Quality of Life mod's answers, its settings round trip, its Item sort and Next column, and a mod
/// that throws. Exit code non-zero on any failure. Everything it writes goes to a scratch folder in
/// the temp directory, never the saves.
/// </summary>
public static partial class ModProbe
{
    private static int _failed;

    private static void Check(string what, object? got, object? want)
    {
        bool ok = Equals(got?.ToString(), want?.ToString());
        if (!ok) _failed++;
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what,-60} got {got ?? "null"}, want {want ?? "null"}");
    }

    private static ModHost Host(string settings, List<string> log, params string[] roots)
    {
        var h = new ModHost(new ModSettingsStore(settings), log.Add);
        h.Discover(roots);
        h.LoadEnabled();
        return h;
    }

    public static int Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;
        _failed = 0;

        string scratch = Path.Combine(Path.GetTempPath(), "mordor-modprobe-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(scratch);
        string bundled = Path.Combine(AppContext.BaseDirectory, "mods");
        try
        {
            // ---- discovery ----
            Console.WriteLine("discovery (the build's mods folder):");
            var log = new List<string>();
            string offIni = Path.Combine(scratch, "off.ini");
            var off = Host(offIni, log, bundled);
            var qol = off.Discovered.FirstOrDefault(d => d.Id == "QoL");
            Check("QoL manifest found", qol != null, true);
            Check("QoL manifest has no problem", qol?.Problem, null);
            Check("QoL name", qol?.Manifest.Name, "Quality of Life");
            Check("QoL declares a toggle per feature again (1.11.0; General's two again, 1.12.0), in its settings window's order", string.Join(",", qol?.Manifest.Settings.Select(t => t.Key) ?? Array.Empty<string>()),
                  "UnlockedStoreAlignment,ToolbarTooltips,XpToNextColumn,HpSpBars,XpBar,MarkActiveQuests,MarkUnidentified,StoreSlotColumns,StoreSortable,SayStoreShelved,ColorCodedCombatMessages");
            Check("QoL version", qol?.Manifest.Version, "1.13.0");
            var qolWin = qol?.Manifest.SettingsWindow;
            Check("QoL has a settings window of its own: its title and five groups, General first, Town gold one choice",
                  $"{qolWin?.Title}: {string.Join(" | ", qolWin?.Groups.Select(g => g.Label + " (" + string.Join(",", g.Keys) + ")") ?? Array.Empty<string>())}",
                  "Quality of Life settings: General (UnlockedStoreAlignment,ToolbarTooltips) | Town gold (TownGold) | Enhanced character UI (XpToNextColumn,HpSpBars,XpBar,MarkActiveQuests,MarkUnidentified) | Enhanced store UI (StoreSlotColumns,StoreSortable,SayStoreShelved) | Color-coded combat messages (ColorCodedCombatMessages)");
            Check("every group and setting has a short description (the tooltips)",
                  (qolWin?.Groups.All(g => g.Description != "") ?? false) && (qol?.Manifest.Settings.All(t => t.Description != "" && t.Description.Length <= 110) ?? false), true);
            Check("every key a group names is declared, and every toggle is in a group",
                  (qolWin?.Groups.SelectMany(g => g.Keys).All(k => qol!.Manifest.Settings.Any(t => t.Key == k) || qol.Manifest.Choices.Any(c => c.Key == k)) ?? false)
                  && (qol?.Manifest.Settings.All(t => qolWin!.Groups.Any(g => g.Keys.Contains(t.Key))) ?? false), true);
            Check("the group lines", string.Join(" | ", qolWin?.Groups.Select(g => g.Description) ?? Array.Empty<string>()),
                  "The Store's unlocked alignment choice and the toolbar's tooltips. | In town, gold kept in the bank for you and charges covered from it. The dungeon's gold is always split evenly. | Adds a Next column, HP/SP and XP bars, quest marks and unidentified-item marks. | Adds Slot and Worn columns, sortable headings, and a note of what a sale stocks to the Store. | Colours combat lines in the Messages window by kind.");
            Check("QoL's one choice: Town gold, Everyone's own bank by default (1.13.0; Party gold and its All to the leader gone)",
                  string.Join(";", qol?.Manifest.Choices.Select(c => $"{c.Key} {c.Label}: {string.Join("|", c.Options)} ({c.Default}) {c.Description}") ?? Array.Empty<string>()),
                  "TownGold Town gold: Off|Everyone's own bank|Leader's bank (Everyone's own bank) Where the party's gold is kept in town, and who covers a charge the payer is short of.");
            Check("... each option with its own tooltip line",
                  string.Join(" | ", qol?.Manifest.Choices.Single().Options.Select(o => qol.Manifest.Choices.Single().TooltipFor(o)) ?? Array.Empty<string>()),
                  "As the game: nothing automatic. Sales pay the seller's hand; a charge takes the payer's hand, then bank. | Arriving and selling put gold in each one's own account; a shortfall comes from the others' accounts, in party order. | Arriving and selling put the party's gold in the leader's account; it covers a shortfall after the payer's own bank.");
            Check("an option without a line of its own: the choice's description", new ModChoiceSpec { Description = "d", Options = { "a", "b" }, OptionDescriptions = { "A" } }.TooltipFor("b"), "d");

            Check("every toggle defaults on", string.Join(",", qol?.Manifest.Settings.Where(t => !t.Default).Select(t => t.Key) ?? Array.Empty<string>()), "");
            Check("the dropped and merged keys are not declared", string.Join(",", qol?.Manifest.Settings.Select(t => t.Key).Where(k => k is "ItemSort" or "BankSort" or "HideUnusableInStore"
                or "ExtendedCombatLog" or "AltPPoolAndDeposit" or "EnhancedCharacterUI" or "EnhancedStoreUI" or "BankManagedTownGold"
                or "PartyGold" or "AutoDepositInTown" or "SaleGoldToBank" or "PartyCoversShortfall" or "DepositOnArrival") ?? Array.Empty<string>()), "");
            Check("the old gold keys are not choices either", string.Join(",", qol?.Manifest.Choices.Select(c => c.Key).Where(k => k != "TownGold") ?? Array.Empty<string>()), "");
            Check("the feature labels", string.Join(" | ", qol?.Manifest.Settings.Select(t => t.Label) ?? Array.Empty<string>()),
                  "Unlocked Store alignment choice | Toolbar tooltips | Next column (XP to next level) | HP/SP bars | XP bar | Quest marks | Unidentified-item marks | Slot and Worn columns | Sortable column headings | Note what a sale stocks | Color-coded combat messages");

            // ---- mods off: the original's answers ----
            Console.WriteLine("mods off (nothing enabled - the default):");
            var party = new Party(MakeChar("Anna"), MakeChar("Bert"));
            Check("QoL not enabled by default", off.IsEnabled("QoL"), false);
            Check("nothing loaded", off.Loaded.Count, 0);
            Check("Store: row change while choosing", off.StoreAllowsRowChangeWhileChoosing(), false);
            Check("town ALT-p", off.CityAltP(new CityAltPRequest(party, party.Selected)).Action, CityAltPAction.PoolOnHand);
            Check("toolbar tooltip", off.ToolbarTooltip(new ToolbarButton("Automap", "AutoMap", "F8")), null);
            Check("Objects & Spells commands", off.ObjectsCommands.Count(), 0);
            Check("Bank commands: none, the Bank is the original's (and the port's two buttons)", off.BankCommands.Count(), 0);
            Check("party columns", off.ShownPartyColumns().Count, 0);
            Check("mod windows", off.Windows.Count(), 0);
            Check("Store check boxes: none, the Store's page is the original's", off.StoreOptions.Count(), 0);
            Check("Store columns: none, the Buy list is the original's", $"{off.StoreColumns().Count} {off.ShownStoreColumns().Count}", "0 0");
            Check("Messages keeps the original's 20", off.MessagesKept(MessageList.Kept), 20);
            Check("Messages lines in the window's own colour", $"{off.MessageLineColour("You were killed by a Kobold.", MessageKind.Foes)} {ModHost.Empty().MessageLineColour("x", MessageKind.Party)}", " ");
            off.CaptionShown(CaptionKind.CharacterHit, "You hit the Kobold for 3 damage!", party.Selected);   // nobody listening: nothing happens
            Check("a caption with no mod: nothing written", File.Exists(offIni), false);
            var twenty = new List<string>();
            for (int i = 1; i <= 25; i++) MessageList.Add(twenty, "line " + i);
            Check("Messages list: the original's twenty kept", $"{twenty.Count} {twenty[0]}", "20 line 6");
            Check("save tag", off.SaveTag(), null);
            Check("nothing was written to settings.ini", File.Exists(offIni), false);
            var anyItem = items.First(i => i.Id != 0);
            Check("Store rows all shown", off.StoreRowVisible(party.Selected, anyItem, usable: false), true);
            Check("no pack-line mark", off.PackRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 1), null);
            Check("no Bank-line mark", off.BankRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 0), null);
            party.Members[0].GoldOnHand = 40; party.Members[0].GoldInBank = 5;
            Check("arriving in town: nothing moves", off.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked), (0L, (string?)null));
            Check("... the gold still on hand", $"{party.Members[0].GoldOnHand}/{party.Members[0].GoldInBank}", "40/5");
            party.Members[0].GoldOnHand = 0; party.Members[0].GoldInBank = 0;
            var empty = ModHost.Empty();
            Check("empty host: Store", empty.StoreAllowsRowChangeWhileChoosing(), false);
            Check("empty host: no Store check boxes", empty.StoreOptions.Count(), 0);
            Check("empty host: no Store columns", empty.StoreColumns().Count, 0);
            Check("empty host: no Bank commands", empty.BankCommands.Count(), 0);
            Check("empty host: no Bank-line mark", empty.BankRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 0), null);
            Check("empty host: ALT-p", empty.CityAltP(new CityAltPRequest(party, party.Selected)), CityAltPAnswer.Original);

            // ---- QoL on ----
            Console.WriteLine("QoL enabled, toggles at their defaults:");
            string ini = Path.Combine(scratch, "settings.ini");
            File.WriteAllLines(ini, new[] { "[Loading Information]", "Character=Anna", "", "[Game Options]", "NoDing=True" });
            new ModSettingsStore(ini).SetEnabled("QoL", true);
            log.Clear();
            var on = Host(ini, log, bundled);
            var lm = on.Loaded.FirstOrDefault(l => l.Id == "QoL");
            Check("QoL loaded", lm != null, true);
            Check("QoL not faulted", lm?.Faulted, false);
            Check("load written to the log", log.Any(l => l.Contains("mod QoL: loaded Quality of Life")), true);
            Check("Store: row change while choosing", on.StoreAllowsRowChangeWhileChoosing(), true);
            Check("town ALT-p: the game's own on-hand pool (Alt+P pools and deposits everything gone, 1.10.0)", on.CityAltP(new CityAltPRequest(party, party.Selected)), CityAltPAnswer.Original);
            Check("tooltip AutoMap", on.ToolbarTooltip(new ToolbarButton("Automap", "AutoMap", "F8")), "AutoMap (F8)");
            Check("tooltip Control Panel", on.ToolbarTooltip(new ToolbarButton("ControlPanel", "Control Panel", "F10")), "Control Panel (F10)");
            Check("tooltip Page (no key)", on.ToolbarTooltip(new ToolbarButton("Page", "Page", null)), "Page");
            Check("no mod window (the Combat Log is gone, 1.9.0)", on.Windows.Count(), 0);
            Check("Messages keeps the original's 20 (the Combat Log's 200 gone with it)", on.MessagesKept(MessageList.Kept), 20);
            Check("combat colours: the party's side, the foes', a chest, a join, the rest",
                  string.Join(" ", new[] { MessageKind.Party, MessageKind.Foes, MessageKind.Chest, MessageKind.Join, MessageKind.None }.Select(k => on.MessageLineColour("a line", k)?.ToString() ?? "-")),
                  "(144, 238, 144) (205, 92, 92) (255, 165, 0) (221, 160, 221) -");
            var sort = on.ObjectsCommands.FirstOrDefault(p => p.Command.Label == "Sort");
            Check("Sort command added", sort.Command != null, true);
            Check("Sort shown", sort.Command != null && on.IsShown(sort.Mod, sort.Command), true);
            Check("party columns shown (the XP bar rides in Next)", string.Join(",", on.ShownPartyColumns().Select(c => c.Column.Header)), "HP/SP,Next");
            Check("save tag", string.Join(",", on.SaveTag() ?? new List<string>()), "QoL@1.13.0");
            Check("a save's missing mods", string.Join(",", on.MissingFrom(new[] { "QoL@1.0.0", "Other@2" })), "Other@2");
            Check("an untagged save misses none", on.MissingFrom(null).Count, 0);

            // the pool itself: the Bank's arithmetic, now in one place
            party.Members[0].GoldOnHand = 10; party.Members[0].GoldInBank = 100;
            party.Members[1].GoldOnHand = 20; party.Members[1].GoldInBank = 200;
            Party.PoolAndDeposit(party.Members, party.Members[0]);
            Check("pool & deposit: to the one in command", $"{party.Members[0].GoldOnHand}/{party.Members[0].GoldInBank}", "0/330");
            Check("pool & deposit: the other left with nothing", $"{party.Members[1].GoldOnHand}/{party.Members[1].GoldInBank}", "0/0");

            ArrivalChecks(on, party);
            StoreAndMarkChecks(on, party.Selected, items);
            StoreSortChecks(on, off, party.Selected, items, Lookup);
            ShelvedChecks(on, party.Selected, items);
            ShortfallChecks(on, off, party);
            GoldChecks(on, off);
            BankChecks(on, items, Lookup);

            // ---- toggles live, and persisted ----
            Console.WriteLine("toggles off, read live and kept in settings.ini:");
            foreach (var t in new[] { "UnlockedStoreAlignment", "ToolbarTooltips" })
            {
                on.Settings.WriteFlag("QoL", t, false);
                Check($"General: {t} off alone: the rest untouched", $"{on.StoreAllowsRowChangeWhileChoosing()} {on.ToolbarTooltip(new ToolbarButton("Automap", "AutoMap", "F8")) ?? "none"} {on.ShownStoreColumns().Count}",
                      t == "UnlockedStoreAlignment" ? "False AutoMap (F8) 2" : "True none 2");
                on.Settings.WriteFlag("QoL", t, true);
            }
            foreach (var t in new[] { "UnlockedStoreAlignment", "ToolbarTooltips", "XpToNextColumn", "HpSpBars", "XpBar", "MarkActiveQuests", "MarkUnidentified", "StoreSlotColumns", "StoreSortable", "SayStoreShelved", "ColorCodedCombatMessages" })
                on.Settings.WriteFlag("QoL", t, false);
            Check("Store columns hidden", on.ShownStoreColumns().Count, 0);
            Check("Store: the row change back to the original's lock (Unlocked Store alignment choice off, a toggle again 1.12.0)", on.StoreAllowsRowChangeWhileChoosing(), false);
            Check("ALT-p back to the original", on.CityAltP(new CityAltPRequest(party, party.Selected)).Action, CityAltPAction.PoolOnHand);
            Check("no toolbar tooltip (Toolbar tooltips off, a toggle again 1.12.0)", on.ToolbarTooltip(new ToolbarButton("Automap", "AutoMap", "F8")), null);
            Check("combat colours off: the window's own", on.MessageLineColour("You were killed by a Kobold.", MessageKind.Foes), null);
            Check("no pack-line mark (Unidentified-item marks off)", on.PackRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 1), null);
            Check("Sort still there: no toggle since 1.8.0", on.IsShown(sort.Mod, sort.Command!), true);
            Check("Bank Sort still there too", on.BankCommands.Any() && on.BankCommands.All(p => on.IsShown(p.Mod, p.Command)), true);
            Check("no Bank-line mark", on.BankRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 0), null);
            Check("party columns hidden", on.ShownPartyColumns().Count, 0);
            var reread = new ModSettingsStore(ini);
            Check("re-read: enabled", reread.IsEnabled("QoL"), true);
            Check("re-read: XP bar", reread.ReadFlag("QoL", "XpBar", true), false);
            reread.WriteFlag("QoL", "StoreSortable", true);
            Check("re-read again: StoreSortable", new ModSettingsStore(ini).ReadFlag("QoL", "StoreSortable", false), true);
            foreach (var t in new[] { "UnlockedStoreAlignment", "ToolbarTooltips", "XpToNextColumn", "HpSpBars", "XpBar", "MarkActiveQuests", "MarkUnidentified", "StoreSlotColumns", "StoreSortable", "SayStoreShelved", "ColorCodedCombatMessages" })
                on.Settings.WriteFlag("QoL", t, true);
            Check("General back on: the row change and the tooltip", $"{on.StoreAllowsRowChangeWhileChoosing()} {on.ToolbarTooltip(new ToolbarButton("Automap", "AutoMap", "F8"))}", "True AutoMap (F8)");
            string text = File.ReadAllText(ini);
            Check("the file has a [Mod:QoL] section", text.Contains("[Mod:QoL]"), true);
            Check("other sections left alone", text.Contains("Character=Anna") && text.Contains("NoDing=True"), true);
            var s2 = new ModSettingsStore(ini);
            s2.Write("QoL", "Note", "two\nlines");
            Check("text setting kept on one line", new ModSettingsStore(ini).Read("QoL", "Note"), "two lines");

            // ---- every earlier settings.ini brought up to 1.13.0 (QolMod.Migrate): the town-gold keys made the
            // Town gold choice, the merged toggles of 1.8.0 to 1.10.0 handed to their features (each its own key
            // again), the dropped keys and the combat log's gone ----
            Console.WriteLine("settings.ini from earlier versions (QolMod.Migrate):");
            {
                var migrate = lm!.Instance!.GetType().GetMethod("Migrate")!;
                string[] keys = { "TownGold", "XpToNextColumn", "HpSpBars", "XpBar", "MarkActiveQuests", "MarkUnidentified",
                                  "StoreSlotColumns", "StoreSortable", "SayStoreShelved", "ColorCodedCombatMessages",
                                  "EnhancedCharacterUI", "EnhancedStoreUI", "ItemSort", "BankSort", "UnlockedStoreAlignment", "ToolbarTooltips",
                                  "AltPPoolAndDeposit", "ExtendedCombatLog", "Window.CombatLog.Place", "Window.CombatLog.Open",
                                  "PartyGold", "AutoDepositInTown", "SaleGoldToBank", "PartyCoversShortfall", "DepositOnArrival", "BankManagedTownGold" };
                var mst = on.Settings;   // the store the loaded mod reads
                void Clear() { foreach (var k in keys) mst.Remove("QoL", k); }
                // what is set after the migration, as the next start reads it: key=value, the rest unset (their defaults, on)
                string After(params (string Key, string Value)[] set)
                {
                    Clear();
                    foreach (var (k, v) in set) mst.Write("QoL", k, v);
                    migrate.Invoke(null, new object[] { lm.Settings });
                    var fresh = new ModSettingsStore(ini);
                    return string.Join(" ", keys.Where(k => fresh.Read("QoL", k) != null).Select(k => k + "=" + fresh.Read("QoL", k)));
                }
                Check("nothing ever set: nothing written, the defaults (on) hold", After(), "");
                Check("1.8.0 to 1.10.0's Enhanced character UI off: all five features off, the merged key gone", After(("EnhancedCharacterUI", "False")),
                      "XpToNextColumn=False HpSpBars=False XpBar=False MarkActiveQuests=False MarkUnidentified=False");
                Check("... on: all five on", After(("EnhancedCharacterUI", "True")),
                      "XpToNextColumn=True HpSpBars=True XpBar=True MarkActiveQuests=True MarkUnidentified=True");
                Check("Enhanced store UI off: its three off", After(("EnhancedStoreUI", "False")), "StoreSlotColumns=False StoreSortable=False SayStoreShelved=False");
                // Town gold (1.13.0) from 1.10.0 to 1.12.0's keys: All to the leader is Leader's bank; else any bank
                // feature on or unset is Everyone's own bank; all off is Off; every old key taken out
                Check("Town gold: Party gold All to the leader -> Leader's bank", After(("PartyGold", "All to the leader")), "TownGold=Leader's bank");
                Check("... the stored option in another case still counts", After(("PartyGold", "all TO the leader")), "TownGold=Leader's bank");
                Check("... even with every bank feature off", After(("PartyGold", "All to the leader"), ("AutoDepositInTown", "False"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "False")),
                      "TownGold=Leader's bank");
                Check("... and with the merged key off", After(("PartyGold", "All to the leader"), ("BankManagedTownGold", "False")), "TownGold=Leader's bank");
                Check("Split evenly, the bank features never set (on by default) -> Everyone's own bank", After(("PartyGold", "Split evenly")), "TownGold=Everyone's own bank");
                Check("Split evenly, all three off -> Off", After(("PartyGold", "Split evenly"), ("AutoDepositInTown", "False"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "False")),
                      "TownGold=Off");
                Check("all three off, no Party gold -> Off", After(("AutoDepositInTown", "False"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "False")), "TownGold=Off");
                Check("two off, the third never set (on) -> Everyone's own bank", After(("AutoDepositInTown", "False"), ("SaleGoldToBank", "False")), "TownGold=Everyone's own bank");
                Check("one on, two off -> Everyone's own bank", After(("AutoDepositInTown", "False"), ("SaleGoldToBank", "True"), ("PartyCoversShortfall", "False")), "TownGold=Everyone's own bank");
                Check("each of the three on alone -> Everyone's own bank",
                      string.Join(" | ", new[] { "AutoDepositInTown", "SaleGoldToBank", "PartyCoversShortfall" }.Select(on1 =>
                          After(new[] { "AutoDepositInTown", "SaleGoldToBank", "PartyCoversShortfall" }.Select(k => (k, k == on1 ? "True" : "False")).ToArray()))),
                      "TownGold=Everyone's own bank | TownGold=Everyone's own bank | TownGold=Everyone's own bank");
                Check("1.10.0's merged Bank-managed town gold off -> Off", After(("BankManagedTownGold", "False")), "TownGold=Off");
                Check("... on -> Everyone's own bank", After(("BankManagedTownGold", "True")), "TownGold=Everyone's own bank");
                Check("... off, one feature on on its own -> Everyone's own bank", After(("BankManagedTownGold", "False"), ("PartyCoversShortfall", "True")), "TownGold=Everyone's own bank");
                Check("... on, all three off on their own -> Off (a feature's own key wins)",
                      After(("BankManagedTownGold", "True"), ("AutoDepositInTown", "False"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "False")), "TownGold=Off");
                Check("a DepositOnArrival key on (never shipped), the three off -> Everyone's own bank, and taken out",
                      After(("DepositOnArrival", "True"), ("AutoDepositInTown", "False"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "False")), "TownGold=Everyone's own bank");
                Check("... off with the three off -> Off", After(("DepositOnArrival", "False"), ("AutoDepositInTown", "False"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "False")), "TownGold=Off");
                Check("Town gold set already: kept, the old keys still taken out", After(("TownGold", "Off"), ("PartyGold", "All to the leader"), ("SaleGoldToBank", "True")), "TownGold=Off");
                Check("... and read as the mod reads it", lm.Settings.GetChoice("TownGold"), "Off");
                Check("migrated: read as the mod reads it", (After(("PartyGold", "All to the leader")), lm.Settings.GetChoice("TownGold")).Item2, "Leader's bank");
                Check("a settings.ini from before the merges: each feature keeps its own value", After(("XpBar", "False"), ("StoreSortable", "False"), ("PartyCoversShortfall", "False")),
                      "TownGold=Everyone's own bank XpBar=False StoreSortable=False");
                Check("a feature set on its own wins over the merged key", After(("EnhancedCharacterUI", "False"), ("XpBar", "True")),
                      "XpToNextColumn=False HpSpBars=False XpBar=True MarkActiveQuests=False MarkUnidentified=False");
                Check("the Extended combat log off: colours off, its window's keys gone",
                      After(("ExtendedCombatLog", "False"), ("Window.CombatLog.Place", "10,10,400,300"), ("Window.CombatLog.Open", "True")), "ColorCodedCombatMessages=False");
                Check("the Extended combat log on: colours on", After(("ExtendedCombatLog", "True")), "ColorCodedCombatMessages=True");
                Check("colours set already: left as they are", After(("ColorCodedCombatMessages", "False"), ("ExtendedCombatLog", "True")), "ColorCodedCombatMessages=False");
                Check("the dropped toggles, either way: their keys gone, nothing else",
                      After(("ItemSort", "False"), ("BankSort", "True"), ("AltPPoolAndDeposit", "True")), "");
                Check("General's two (toggles again, 1.12.0): kept as set, never taken out now",
                      After(("UnlockedStoreAlignment", "False"), ("ToolbarTooltips", "True")), "UnlockedStoreAlignment=False ToolbarTooltips=True");
                Check("... and read as the mod reads them", $"{lm.Settings.Get("UnlockedStoreAlignment")} {lm.Settings.Get("ToolbarTooltips")} {on.StoreAllowsRowChangeWhileChoosing()}", "False True False");
                Check("taken out by 1.9.0 to 1.11.0 (absent): nothing written, both read on, the row change and the tooltip there",
                      (After(("ItemSort", "False")), $"{lm.Settings.Get("UnlockedStoreAlignment")} {lm.Settings.Get("ToolbarTooltips")} {on.StoreAllowsRowChangeWhileChoosing()} {on.ToolbarTooltip(new ToolbarButton("Automap", "AutoMap", "F8"))}"),
                      ("", "True True True AutoMap (F8)"));
                Check("a 1.10.0 settings.ini, everything at once",
                      After(("EnhancedCharacterUI", "True"), ("EnhancedStoreUI", "False"), ("BankManagedTownGold", "True"), ("PartyGold", "Split evenly"), ("ColorCodedCombatMessages", "False")),
                      "TownGold=Everyone's own bank XpToNextColumn=True HpSpBars=True XpBar=True MarkActiveQuests=True MarkUnidentified=True StoreSlotColumns=False StoreSortable=False SayStoreShelved=False ColorCodedCombatMessages=False");
                Check("a 1.12.0 settings.ini, everything at once",
                      After(("PartyGold", "All to the leader"), ("AutoDepositInTown", "True"), ("SaleGoldToBank", "False"), ("PartyCoversShortfall", "True"), ("XpBar", "False"), ("UnlockedStoreAlignment", "True")),
                      "TownGold=Leader's bank XpBar=False UnlockedStoreAlignment=True");
                Check("a second start finds nothing to do: Town gold as left", (After(("PartyGold", "All to the leader")), (string)(migrate.Invoke(null, new object[] { lm.Settings }) ?? ""), new ModSettingsStore(ini).Read("QoL", "TownGold")).Item3, "Leader's bank");
                Check("a second start finds nothing to do: the same", (After(("EnhancedStoreUI", "False")), (string)(migrate.Invoke(null, new object[] { lm.Settings }) ?? "")).Item1, "StoreSlotColumns=False StoreSortable=False SayStoreShelved=False");
                Check("... still read as the mod reads them", $"{lm.Settings.Get("StoreSortable")} {lm.Settings.Get("XpBar")}", "False True");
                mst.WriteFlag("QoL", "HideUnusableInStore", true);
                After(("ItemSort", "False"));
                Check("the Store box's state kept under its key", new ModSettingsStore(ini).ReadFlag("QoL", "HideUnusableInStore", false), true);
                mst.WriteFlag("QoL", "HideUnusableInStore", false);
                Clear();
            }

            // ---- the Next column ----
            Console.WriteLine("Next column (the toolbar's figure, two significant figures):");
            var xp = lm!.Instance!.GetType().Assembly.GetType("Mordor.Mod.QoL.XpColumn")!;
            string Short(long n) => (string)xp.GetMethod("Short")!.Invoke(null, new object[] { n })!;
            string Cell(Character c) => (string)xp.GetMethod("Cell")!.Invoke(null, new object[] { c })!;
            foreach (var (n, want) in new (long, string)[]
            {
                (0, "0"), (950, "950"), (999, "999"), (1000, "1.0K"), (3512, "3.5K"), (9950, "10K"),
                (12345, "12K"), (99960, "100K"), (999_600, "1.0M"), (1_000_000, "1.0M"), (2_480_000, "2.5M"),
                (2_450_000, "2.5M"), (2_449_999, "2.4M"), (5_000_000_000, "5.0B"),
            })
                Check($"format {n:N0}", Short(n), want);
            var who = MakeChar("Cara");
            who.Guild.Experience = 0;
            long need = who.ExpForNextLevel();
            Check("short of the level: the toolbar's amount", Cell(who), Short(need));
            Check("the toolbar says the same", Progress.ToolbarNeed(who).State, Progress.NeedState.Short);
            who.Guild.Experience = need;
            Check("the pin lies past the level", who.ExperienceCap() > need, true);
            Check("enough for the level: the amount to the pin", Cell(who), Short(who.ExperienceCap() - need));
            Check("... and the toolbar agrees", Progress.ToolbarNeed(who), (Progress.NeedState.Ready, who.ExperienceCap() - need));
            // the first level where the pin lies more than 2,300 past the next
            for (who.Guild.Level = 1; who.Guild.Level < 200 && who.ExperienceCap() - who.ExpForNextLevel() <= 2300; who.Guild.Level++) { }
            Check("a level with the pin more than 2,300 past the next", who.ExperienceCap() - who.ExpForNextLevel() > 2300, true);
            who.Guild.Experience = who.ExperienceCap() - 2300;
            Check("2,300 short of the pin", Cell(who), "2.3K");
            who.Guild.Level = 1;
            who.Guild.Experience = who.ExperienceCap();
            Check("pinned", Cell(who), "Pinned");
            who.Guild.Experience = who.ExperienceCap() + 500;
            Check("past the pin (a lowered pin): Pinned, not a negative amount", Cell(who), "Pinned");
            on.Settings.WriteFlag("QoL", "XpToNextColumn", true); on.Settings.WriteFlag("QoL", "HpSpBars", true);
            Check("the Next column and the HP/SP bars on: the HP/SP and Next columns", string.Join(",", on.ShownPartyColumns().Select(c => c.Column.Header)), "HP/SP,Next");

            BarsAndLogChecks(on, lm, party.Selected);
            QuestChecks(on, off, lm, MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR")), items, Lookup);

            // ---- Item sort ----
            Console.WriteLine("Item sort:");
            SortChecks(on, sort.Mod, sort.Command!, items, Lookup);

            // ---- the Kaizen mod and its groundwork (ModProbe.Kaizen.cs) ----
            KaizenChecks(dir, scratch, bundled, items, Lookup);

            // ---- the DPS Meter mod and DamageDealt (ModProbe.DpsMeter.cs) ----
            DpsChecks(dir, scratch, bundled, items, Lookup);

            // ---- switching mods on and off on the main menu (ModProbe.Switch.cs) ----
            SwitchChecks(scratch, bundled, items, Lookup);

            // ---- mods that throw ----
            Console.WriteLine("mods that throw:");
            string bad = Path.Combine(scratch, "mods");
            MakeTestMod(bad, "Boom", nameof(BoomMod));
            MakeTestMod(bad, "Flaky", nameof(FlakyMod));
            MakeTestMod(bad, "Patchy", nameof(PatchyMod));
            MakeTestMod(bad, "Shuffly", nameof(ShufflyMod));
            string badIni = Path.Combine(scratch, "bad.ini");
            var st = new ModSettingsStore(badIni);
            st.SetEnabled("Boom", true); st.SetEnabled("Flaky", true); st.SetEnabled("Patchy", true); st.SetEnabled("Shuffly", true);
            var probeItem = items.First(i => i.Id != 0);
            var identify = typeof(Loot).GetMethod(nameof(Loot.IdentifyDifficulty))!;
            double Difficulty() => (double)identify.Invoke(null, new object[] { probeItem })!;
            double before = Difficulty();
            log.Clear();
            ModHost? thrown = null;
            try { thrown = Host(badIni, log, bad); } catch (Exception ex) { Console.WriteLine("  loader threw: " + ex.Message); }
            Check("the loader carried on", thrown != null, true);
            var boom = thrown?.Loaded.FirstOrDefault(l => l.Id == "Boom");
            Check("Boom disabled for the session", boom?.Faulted, true);
            Check("Boom's reason", boom?.FaultReason, "Load: boom");
            Check("Boom logged", log.Any(l => l.Contains("mod Boom: disabled for this session")), true);
            Check("Boom's Harmony patch removed", OwnedPatches("Boom"), 0);
            var patchy = thrown?.Loaded.FirstOrDefault(l => l.Id == "Patchy");
            Check("Patchy loaded", patchy?.Faulted, false);
            Check("Patchy's patch logged", log.Any(l => l.Contains("mod Patchy: Harmony patch Mordor.Core.Loot.IdentifyDifficulty")), true);
            Check("Patchy's patch in effect", Difficulty(), 4242);
            new HarmonyLib.Harmony("Patchy").UnpatchAll("Patchy");
            Check("unpatched again", Difficulty(), before);
            var flaky = thrown?.Loaded.FirstOrDefault(l => l.Id == "Flaky");
            Check("Flaky loaded", flaky?.Faulted, false);
            Check("a throwing decision answers the original", thrown?.StoreAllowsRowChangeWhileChoosing(), false);
            Check("Flaky disabled after its hook threw", flaky?.Faulted, true);
            Check("Flaky logged", log.Any(l => l.Contains("mod Flaky: disabled for this session - it threw in StoreAllowsRowChangeWhileChoosing")), true);
            Check("and it is not asked again", thrown?.StoreAllowsRowChangeWhileChoosing(), false);
            var shuffly = thrown?.Loaded.FirstOrDefault(l => l.Id == "Shuffly");
            var three = items.Where(i => i.Id != 0).Take(3).ToList();
            Check("Shuffly loaded", shuffly?.Faulted, false);
            Check("a Store order that drops a row: the shelf's order", string.Join(",", thrown?.StoreRowOrder(party.Selected, three, Lookup).Select(i => i.Id) ?? Array.Empty<short>()), string.Join(",", three.Select(i => i.Id)));
            Check("... and its mod disabled", shuffly?.FaultReason, "StoreRowOrder: its answer is not the rows it was given, reordered");
            Check("the save tag leaves the disabled out", string.Join(",", thrown?.SaveTag() ?? new List<string>()), "Patchy@1");
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch { }
        }

        Console.WriteLine(_failed == 0 ? "mods: all checks passed" : $"mods: {_failed} FAILED");
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>Set QoL's Town gold choice (1.13.0) as the settings window would.</summary>
    private static void TownGold(ModHost on, string mode) => on.Settings.Write("QoL", "TownGold", mode);

    /// <summary>
    /// Town gold on arriving in town (1.13.0): Everyone's own bank is the Bank's Party Deposit
    /// (Party.PartyDeposit) for every member as the party walks in, Leader's bank its Party Pool
    /// (Party.PoolToBank) into the leader's account - the next living member when the leader is dead -
    /// each with one line for the total; Off is the game's: nothing. Nothing when nobody carries gold,
    /// nothing after a rescue, and nothing at all on an Overflow (all or nothing, as the Bank's).
    /// </summary>
    private static void ArrivalChecks(ModHost on, Party party)
    {
        Console.WriteLine("Town gold: arriving in town:");
        var (a, b) = (party.Members[0], party.Members[1]);
        // the Bank's own rule, on its own
        a.GoldOnHand = 7; a.GoldInBank = 3; b.GoldOnHand = 0; b.GoldInBank = 9;
        Check("Party Deposit: the total moved", Party.PartyDeposit(party.Members), 7);
        Check("Party Deposit: each into their own account", $"{a.GoldOnHand}/{a.GoldInBank} {b.GoldOnHand}/{b.GoldInBank}", "0/10 0/9");

        Check("default: Everyone's own bank", on.Loaded.Single(l => l.Id == "QoL").Settings.GetChoice("TownGold"), "Everyone's own bank");
        a.GoldOnHand = 1200; a.GoldInBank = 300; b.GoldOnHand = 2312; b.GoldInBank = 0;
        var walked = on.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked);
        Check("Everyone's own bank, walked in: the total", walked.Deposited, 3512);
        Check("... the line", walked.Line, $"Deposited the party's gold: {3512:N0}");
        Check("... each member's own account", $"{a.GoldOnHand}/{a.GoldInBank} {b.GoldOnHand}/{b.GoldInBank}", "0/1500 0/2312");
        var again = on.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked);
        Check("nothing on hand: nothing moved, no line", again, (0L, (string?)null));
        Check("... the accounts untouched", $"{a.GoldInBank} {b.GoldInBank}", "1500 2312");

        a.GoldOnHand = 50;
        Check("carried in dead (a rescue): nothing", on.ArrivedInTown(party, party.Selected, TownArrivalKind.Rescued), (0L, (string?)null));
        Check("... the gold still on hand", a.GoldOnHand, 50);
        TownGold(on, "Off");
        Check("Off: nothing, as the game", on.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked), (0L, (string?)null));
        Check("... the gold still on hand", $"{a.GoldOnHand}/{a.GoldInBank}", "50/1500");
        TownGold(on, "Everyone's own bank");
        Check("Everyone's own bank again, read live", on.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked).Deposited, 50);
        // gold with a fraction (a Currency, kept since an amount like 10.5 was typed at the Bank):
        // the total is exact and the line shows it as formatNumber does, rounded half away from zero
        a.GoldOnHand = VbCurrency.FromDecimal(10.5m); b.GoldOnHand = VbCurrency.FromDecimal(0.25m); a.GoldInBank = 0; b.GoldInBank = 0;
        var frac = on.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked);
        Check("a fraction: the exact total", frac.Deposited, "10.75");
        Check("a fraction: the line, rounded as formatNumber", frac.Line, "Deposited the party's gold: 11");
        Check("a fraction: each account exact", $"{a.GoldOnHand}/{a.GoldInBank} {b.GoldOnHand}/{b.GoldInBank}", "0/10.5 0/0.25");
        // an account that cannot hold its member's gold: the Bank's all or nothing - nobody's moves
        a.GoldOnHand = 5; a.GoldInBank = VbCurrency.MaxValue - 2; b.GoldOnHand = 40; b.GoldInBank = 0;
        Check("Everyone's own bank, an account that would overflow: nothing moved, no line", on.ArrivedInTown(party, party.Selected, TownArrivalKind.Walked), (0L, (string?)null));
        Check("... every hand and account as it was", $"{a.GoldOnHand} {a.GoldInBank == VbCurrency.MaxValue - 2} {b.GoldOnHand}/{b.GoldInBank}", "5 True 40/0");
        a.GoldOnHand = 0; a.GoldInBank = 0; b.GoldOnHand = 0; b.GoldInBank = 0;

        // Leader's bank: everyone's gold on hand pooled into the first member's account
        var lead = MakeChar("Lead"); var mid = MakeChar("Mid"); var last = MakeChar("Last");
        var three = new Party(lead, mid, last);
        three.Select(2);
        void Hands(long l, long m, long s) { lead.GoldOnHand = l; mid.GoldOnHand = m; last.GoldOnHand = s; }
        string Gold() => $"{lead.GoldOnHand}/{lead.GoldInBank} {mid.GoldOnHand}/{mid.GoldInBank} {last.GoldOnHand}/{last.GoldInBank}";
        TownGold(on, "Leader's bank");
        Hands(100, 200, 300); lead.GoldInBank = 1; mid.GoldInBank = 2; last.GoldInBank = 3;
        var pooled = on.ArrivedInTown(three, three.Selected, TownArrivalKind.Walked);
        Check("Leader's bank, walked in (the last in command): the total and the line naming the leader", pooled, (600L, (string?)"Deposited the party's gold with \"Lead\": 600"));
        Check("... all of it in the leader's account, the others' accounts untouched", Gold(), "0/601 0/2 0/3");
        Check("... nothing on hand: nothing moved, no line", on.ArrivedInTown(three, three.Selected, TownArrivalKind.Walked), (0L, (string?)null));
        Hands(10, 0, 0);
        Check("... carried in dead (a rescue): nothing", (on.ArrivedInTown(three, three.Selected, TownArrivalKind.Rescued), Gold()), ((0L, (string?)null), "10/601 0/2 0/3"));
        lead.IsDead = true; lead.CurHits = 0;
        Hands(10, 20, 30);
        Check("... the leader dead: the next living member's account, the dead leader's own hand pooled too",
              (on.ArrivedInTown(three, three.Selected, TownArrivalKind.Walked), Gold()), ((60L, (string?)"Deposited the party's gold with \"Mid\": 60"), "0/601 0/62 0/3"));
        lead.IsDead = false; lead.CurHits = lead.MaxHits;
        Hands(10, 20, 30); lead.GoldInBank = VbCurrency.MaxValue - 50;
        Check("... the leader's account would overflow: nothing moved, no line", on.ArrivedInTown(three, three.Selected, TownArrivalKind.Walked), (0L, (string?)null));
        Check("... every hand as it was, the account untouched", $"{lead.GoldOnHand} {mid.GoldOnHand} {last.GoldOnHand} {lead.GoldInBank == VbCurrency.MaxValue - 50}", "10 20 30 True");
        lead.GoldInBank = 0;
        TownGold(on, "Off");
        Check("Off again: nothing", (on.ArrivedInTown(three, three.Selected, TownArrivalKind.Walked), Gold()), ((0L, (string?)null), "10/0 20/62 30/3"));
        on.Settings.Write("QoL", "TownGold", "leader's BANK");
        Check("a stored option of another case still counts", on.Loaded.Single(l => l.Id == "QoL").Settings.GetChoice("TownGold"), "Leader's bank");
        on.Settings.Write("QoL", "TownGold", "All to the leader");
        Check("an unknown option (the old dungeon choice's) reads as the default", on.Loaded.Single(l => l.Id == "QoL").Settings.GetChoice("TownGold"), "Everyone's own bank");
        on.Settings.Remove("QoL", "TownGold");
    }

    /// <summary>
    /// The dungeon's gold is always the game's even share (1.13.0: Party gold's All to the leader and
    /// its PartyGoldTo decision gone), whatever Town gold is; and Town gold's sale gold
    /// (TownIncomeBank, Character.Receive): Off the seller's hand, Everyone's own bank the seller's
    /// account, Leader's bank the leader's - the next living member when the leader is dead - with
    /// the game's overflow refusal for wherever it goes.
    /// </summary>
    private static void GoldChecks(ModHost on, ModHost off)
    {
        Console.WriteLine("the dungeon's gold: always the game's even share (PayGold):");
        // a fight paying a pot of 101: nothing from the monsters, 101 recovered from a thief by the second member
        (string Gold, string? Line) Pay(bool leaderDead = false, int watch = 0)
        {
            var party = new Party(MakeChar("Lead"), MakeChar("Mid"), MakeChar("Last"));
            foreach (var m in party.Members) { m.GoldOnHand = 5; m.GoldInBank = 0; }
            if (leaderDead) { party.Members[0].IsDead = true; party.Members[0].CurHits = 0; }
            party.Select(watch);
            var fight = new CombatEngine(party, new List<CombatMonster>(), _ => null, new Random(1));
            fight.GoldRecovered[party.Members[1]] = 101;
            fight.PayGold();
            return (string.Join(" ", party.Members.Select(m => $"{m.GoldOnHand}/{m.GoldInBank}")), fight.GoldLine);
        }
        var original = Pay();
        Check("the game: 101 three ways, CLng each (34), the line the share", original, ("39/0 39/0 39/0", (string?)"Your share is 34 Gold!"));
        Check("... watching the one robbed: the share and the recovery", Pay(watch: 1), ("39/0 39/0 39/0", (string?)"Your share is 34 and you recovered 101 Gold!"));
        Check("... the leader dead: the dead paid too, as the game", Pay(leaderDead: true), ("39/0 39/0 39/0", (string?)"Your share is 34 Gold!"));
        Check("no way for a mod to take the dungeon's gold any more: no PartyGoldTo decision, no CombatEngine.GoldTo",
              $"{typeof(ModDecisions).GetProperty("PartyGoldTo") == null} {typeof(CombatEngine).GetProperty("GoldTo") == null} {typeof(ModHost).GetMethod("PartyGoldTo") == null}", "True True True");
        foreach (var mode in new[] { "Off", "Everyone's own bank", "Leader's bank" })
        {
            TownGold(on, mode);
            Check($"QoL on, Town gold {mode}: the dungeon's split the same as the game's", $"{Pay()} {Pay(watch: 1)} {Pay(leaderDead: true, watch: 2)}",
                  $"{original} {("39/0 39/0 39/0", (string?)"Your share is 34 and you recovered 101 Gold!")} {("39/0 39/0 39/0", (string?)"Your share is 34 Gold!")}");
        }
        on.Settings.Remove("QoL", "TownGold");

        Console.WriteLine("Town gold: sale gold (TownIncomeBank):");
        var lead = MakeChar("Lead"); var seller = MakeChar("Seller"); var last = MakeChar("Last");
        var party3 = new Party(lead, seller, last);
        party3.Select(1);
        string Gold3() => $"{lead.GoldOnHand}/{lead.GoldInBank} {seller.GoldOnHand}/{seller.GoldInBank} {last.GoldOnHand}/{last.GoldInBank}";
        void Zero() { foreach (var m in party3.Members) { m.GoldOnHand = 0; m.GoldInBank = 0; } }
        // what the Store's Sell does with the answer: the account named, else the seller's hand
        string Sell(ModHost h, Character who, long price, TownIncomeKind kind)
        {
            var bank = h.TownIncomeBank(party3, who, price, kind);
            var payee = bank ?? who;
            bool can = payee.CanReceive(price, bank != null);
            if (can) payee.Receive(price, bank != null);
            return $"{bank?.Name ?? "hand"} {can} {Gold3()}";
        }
        Zero(); seller.GoldOnHand = 10; seller.GoldInBank = 20;
        Check("default (Everyone's own bank): a Store sale into the seller's own account", Sell(on, seller, 300, TownIncomeKind.StoreSale), "Seller True 0/0 10/320 0/0");
        Check("... a Confinement sale too", Sell(on, seller, 50, TownIncomeKind.CompanionSale), "Seller True 0/0 10/370 0/0");
        TownGold(on, "Off");
        Check("Off: the seller's hand, as the game", Sell(on, seller, 300, TownIncomeKind.StoreSale), "hand True 0/0 310/370 0/0");
        Check("... a Confinement sale too", Sell(on, seller, 5, TownIncomeKind.CompanionSale), "hand True 0/0 315/370 0/0");
        TownGold(on, "Leader's bank");
        Zero();
        Check("Leader's bank: a Store sale into the leader's account", Sell(on, seller, 300, TownIncomeKind.StoreSale), "Lead True 0/300 0/0 0/0");
        Check("... a Confinement sale too", Sell(on, seller, 50, TownIncomeKind.CompanionSale), "Lead True 0/350 0/0 0/0");
        Check("... the leader selling: the leader's own account", Sell(on, lead, 7, TownIncomeKind.StoreSale), "Lead True 0/357 0/0 0/0");
        lead.IsDead = true; lead.CurHits = 0;
        Check("... the leader dead: the next living member's account (the seller's here)", Sell(on, seller, 40, TownIncomeKind.StoreSale), "Seller True 0/357 0/40 0/0");
        Check("... and when the last sells, still that member's", Sell(on, last, 3, TownIncomeKind.StoreSale), "Seller True 0/357 0/43 0/0");
        lead.IsDead = false; lead.CurHits = lead.MaxHits;
        Zero(); lead.GoldInBank = VbCurrency.MaxValue;
        Check("... a sale that would overflow the leader's account: refused, nothing moved (the seller's hand untouched too)",
              (Sell(on, seller, 1, TownIncomeKind.StoreSale), lead.GoldInBank == VbCurrency.MaxValue), ("Lead False " + Gold3(), true));
        TownGold(on, "Everyone's own bank");
        Zero(); seller.GoldInBank = VbCurrency.MaxValue;
        Check("Everyone's own bank, a sale that would overflow the seller's account: refused, nothing moved",
              (seller.CanReceive(1, true), seller.Receive(1, true), seller.GoldInBank == VbCurrency.MaxValue, seller.GoldOnHand), (false, false, true, VbCurrency.Zero));
        Check("... the hand would still take it (the game's own place)", seller.CanReceive(1, false), true);
        Zero();
        Check("mods off, or none: the hand", $"{off.TownIncomeBank(party3, seller, 1, TownIncomeKind.StoreSale)?.Name ?? "hand"} {ModHost.Empty().TownIncomeBank(party3, seller, 1, TownIncomeKind.CompanionSale)?.Name ?? "hand"}", "hand hand");
        on.Settings.Remove("QoL", "TownGold");

        Console.WriteLine("a mod's settings brought up to date while it is off (ModHost.MigrateSettings, for its settings window):");
        string offIni = Path.Combine(Path.GetDirectoryName(on.Settings.Path)!, "migrate-off.ini");
        var store = new ModSettingsStore(offIni);
        store.Write("QoL", "EnhancedStoreUI", "False");
        store.Write("QoL", "ItemSort", "False");
        store.Write("QoL", "ToolbarTooltips", "False");
        store.Write("QoL", "PartyGold", "All to the leader");
        store.Write("QoL", "SaleGoldToBank", "False");
        var offHost = new ModHost(store, _ => { });
        offHost.Discover(on.Discovered.Select(d => Path.GetDirectoryName(d.Directory)!).Distinct());
        offHost.LoadEnabled();
        Check("QoL off there: not loaded", offHost.Loaded.Any(l => l.Id == "QoL"), false);
        offHost.MigrateSettings(offHost.Discovered.Single(d => d.Id == "QoL"));
        var after = new ModSettingsStore(offIni);
        Check("its Migrate ran all the same: the merged key handed to the three, the old keys gone, General's kept",
              $"{after.Read("QoL", "StoreSlotColumns")} {after.Read("QoL", "StoreSortable")} {after.Read("QoL", "SayStoreShelved")} {after.Read("QoL", "EnhancedStoreUI") ?? "gone"} {after.Read("QoL", "ItemSort") ?? "gone"} {after.Read("QoL", "ToolbarTooltips") ?? "gone"}",
              "False False False gone gone False");
        Check("... and Town gold made from the old gold keys, which are gone",
              $"{after.Read("QoL", "TownGold")} {after.Read("QoL", "PartyGold") ?? "gone"} {after.Read("QoL", "SaleGoldToBank") ?? "gone"}", "Leader's bank gone gone");
        Check("... and still not loaded", offHost.Loaded.Any(l => l.Id == "QoL"), false);
        offHost.MigrateSettings(offHost.Discovered.Single(d => d.Id == "Kaizen"));   // no Migrate: nothing
        Check("a mod with no Migrate: nothing written", new ModSettingsStore(offIni).Read("Kaizen", "Enabled"), null);
    }

    /// <summary>
    /// Town gold's shortfall (1.13.0): the payer pays by the game's charPay (their hand, then their
    /// own bank); what is still short comes, exactly, from the others' bank accounts in party order
    /// (Everyone's own bank) or from the leader's account alone (Leader's bank; the next living
    /// member when the leader is dead; nothing when the payer is the leader). Others' gold on hand is
    /// never taken. Nothing when it cannot be covered (the game's refusal follows), in a party of one,
    /// below ground, with Off, with no mod, or when the payer's hand would overflow; one line per giver.
    /// </summary>
    private static void ShortfallChecks(ModHost on, ModHost off, Party _)
    {
        Console.WriteLine("Town gold: the shortfall, Everyone's own bank (the default):");
        var fifi = MakeChar("fifi"); var me = MakeChar("uncanard"); var lou = MakeChar("loulou"); var riri = MakeChar("riri");
        var party = new Party(fifi, me, lou, riri);
        void Set(long a, long b, long c, long d, long e, long f, long g, long h)
        {
            fifi.GoldOnHand = a; fifi.GoldInBank = b; me.GoldOnHand = c; me.GoldInBank = d;
            lou.GoldOnHand = e; lou.GoldInBank = f; riri.GoldOnHand = g; riri.GoldInBank = h;
        }
        string All() => $"{fifi.GoldOnHand}/{fifi.GoldInBank} {me.GoldOnHand}/{me.GoldInBank} {lou.GoldOnHand}/{lou.GoldInBank} {riri.GoldOnHand}/{riri.GoldInBank}";
        string Lines(List<string>? l) => l == null ? "null" : string.Join(" | ", l);
        string Gives(string who, long n) => $"\"{who}\" gives \"uncanard\" {n:N0} Gold.";

        // the first other account in party order has enough: the rest untouched, and nobody's hand taken
        Set(200, 1000, 100, 50, 300, 0, 0, 500);
        Check("a plan moves nothing", $"{Party.PlanShortfall(party.Members, me, 500)?.Count} {All()}", "1 200/1000 100/50 300/0 0/500");
        Check("the host's test moves nothing", $"{on.WouldCoverShortfall(party, me, 500)} {All()}", "True 200/1000 100/50 300/0 0/500");
        var said = on.CoverShortfall(party, me, 500);
        Check("exactly the shortfall (350) from the first account in party order", Lines(said), Gives("fifi", 350));
        Check("... onto the payer's gold on hand; others' hands untouched", All(), "200/650 450/50 300/0 0/500");
        Check("... and charPay then takes the hand, then the payer's own bank", $"{me.Pay(500)} {me.GoldOnHand}/{me.GoldInBank}", "True 0/0");

        // the accounts in party order, member 1 first, the payer skipped
        Set(0, 100, 0, 0, 0, 50, 0, 500);
        Check("party order across the accounts", Lines(on.CoverShortfall(party, me, 400)), $"{Gives("fifi", 100)} | {Gives("loulou", 50)} | {Gives("riri", 250)}");
        Check("... balances", All(), "0/0 400/0 0/0 0/250");
        Check("... charPay takes it", $"{me.Pay(400)} {me.Purse}", "True 0");
        // the leader paying: the next members' accounts in order
        Set(0, 0, 0, 30, 0, 30, 0, 100);
        var lines1 = on.CoverShortfall(party, fifi, 100);
        Check("member 1 paying: members 2, 3, 4 in order", Lines(lines1),
              "\"uncanard\" gives \"fifi\" 30 Gold. | \"loulou\" gives \"fifi\" 30 Gold. | \"riri\" gives \"fifi\" 40 Gold.");
        Check("... balances", All(), "100/0 0/0 0/0 0/60");

        // gold on hand among the others does not count: only their accounts
        Set(0, 10, 0, 0, 5000, 20, 5000, 30);
        Check("others' gold on hand never taken: can't cover", $"{on.WouldCoverShortfall(party, me, 100)} {Lines(on.CoverShortfall(party, me, 100))}", "False null");
        Check("... nothing moved", All(), "0/10 0/0 5000/20 5000/30");
        Check("... charPay refuses, as the game", me.Pay(100), false);
        Check("exactly enough: every account emptied", Lines(on.CoverShortfall(party, me, 60)), $"{Gives("fifi", 10)} | {Gives("loulou", 20)} | {Gives("riri", 30)}");
        Check("... onto the payer's hand", All(), "0/0 60/0 5000/0 5000/0");

        // the purse is enough: nothing asked, nothing moved
        Set(100, 100, 60, 40, 0, 0, 0, 0);
        Check("purse enough: nothing", $"{Lines(on.CoverShortfall(party, me, 100))} {All()}", "null 100/100 60/40 0/0 0/0");

        // a purse with a fraction: the shortfall is 0.5, handed over exactly, and the line shows it
        // rounded as formatNumber does (half away from zero); charPay then takes every last bit
        Set(0, 0, 0, 0, 0, 0, 0, 0);
        fifi.GoldInBank = VbCurrency.FromDecimal(10.25m); me.GoldOnHand = VbCurrency.FromDecimal(99.5m);
        Check("a fraction short: the line", Lines(on.CoverShortfall(party, me, 100)), Gives("fifi", 1));
        Check("a fraction short: exactly the half moved", All(), "0/9.75 100/0 0/0 0/0");
        Check("... charPay takes it all", $"{me.Pay(100)} {me.GoldOnHand}/{me.GoldInBank}", "True 0/0");

        // a party of one
        var alone = new Party(me);
        Set(0, 100, 0, 0, 0, 0, 0, 0);
        Check("a party of one: nothing", $"{Lines(on.CoverShortfall(alone, me, 50))} {on.WouldCoverShortfall(alone, me, 50)} {me.Purse}", "null False 0");

        // below ground: not the town; and the dead give nothing
        me.DungeonLevel = 1;
        Check("payer below ground: nothing", $"{Lines(on.CoverShortfall(party, me, 50))} {All()}", "null 0/100 0/0 0/0 0/0");
        Check("payer below ground: not even asked", on.WouldCoverShortfall(party, me, 50), false);
        me.DungeonLevel = 0; fifi.DungeonLevel = 2; lou.GoldInBank = 20;
        Check("a member below ground does not give", $"{Lines(on.CoverShortfall(party, me, 20))} {All()}", $"{Gives("loulou", 20)} 0/100 20/0 0/0 0/0");
        fifi.DungeonLevel = 0;
        Set(0, 100, 0, 0, 0, 0, 0, 100);
        fifi.IsDead = true; fifi.CurHits = 0;
        Check("a dead member does not give", $"{Lines(on.CoverShortfall(party, me, 30))} {All()}", $"{Gives("riri", 30)} 0/100 30/0 0/0 0/70");
        fifi.IsDead = false; fifi.CurHits = fifi.MaxHits;

        // the payer's hand cannot hold the gift: counts as not covered, nothing moved
        Set(0, 100, 0, 0, 0, 0, 0, 0);
        me.GoldOnHand = VbCurrency.MaxValue - 5; me.GoldInBank = -100;
        Check("the payer's hand would overflow: no lines, nothing moved",
              $"{Lines(on.CoverShortfall(party, me, 922337203685400L))} {fifi.GoldInBank} {me.GoldOnHand == VbCurrency.MaxValue - 5} {me.GoldInBank}", "null 100 True -100");

        Console.WriteLine("Town gold: the shortfall, Leader's bank:");
        TownGold(on, "Leader's bank");
        // the leader's account alone, after the payer's own hand and bank
        Set(0, 1000, 100, 50, 0, 9999, 0, 9999);
        Check("the leader's account makes up exactly the shortfall; the others' untouched", Lines(on.CoverShortfall(party, me, 500)), Gives("fifi", 350));
        Check("... balances", All(), "0/650 450/50 0/9999 0/9999");
        Check("... charPay takes the payer's hand, then own bank", $"{me.Pay(500)} {me.GoldOnHand}/{me.GoldInBank}", "True 0/0");
        Set(0, 1000, 0, 400, 0, 0, 0, 0);
        Check("own bank before the leader's: only the rest (100) from the leader", Lines(on.CoverShortfall(party, me, 500)), Gives("fifi", 100));
        Check("... charPay empties the payer's own account", $"{me.Pay(500)} {All()}", "True 0/900 0/0 0/0 0/0");
        Set(0, 50, 0, 0, 1000, 1000, 0, 1000);
        Check("the leader's account short: nobody else asked, nothing moved, the game's refusal",
              $"{on.WouldCoverShortfall(party, me, 100)} {Lines(on.CoverShortfall(party, me, 100))} {All()} {me.Pay(100)}", "False null 0/50 0/0 1000/1000 0/1000 False");
        Set(0, 0, 0, 1000, 0, 1000, 0, 1000);
        Check("the leader paying: no cover (the leader's account is the payer's own)",
              $"{on.WouldCoverShortfall(party, fifi, 100)} {Lines(on.CoverShortfall(party, fifi, 100))} {All()}", "False null 0/0 0/1000 0/1000 0/1000");
        Set(0, 5000, 0, 1000, 0, 0, 0, 0);
        fifi.IsDead = true; fifi.CurHits = 0;
        Check("the leader dead: the next living member's account (uncanard's) covers loulou",
              $"{Lines(on.CoverShortfall(party, lou, 300))} {All()}", $"\"uncanard\" gives \"loulou\" {300:N0} Gold. 0/5000 0/700 300/0 0/0");
        Check("... and uncanard, now the leader, paying gets no cover", $"{on.WouldCoverShortfall(party, me, 2000)} {Lines(on.CoverShortfall(party, me, 2000))}", "False null");
        fifi.IsDead = false; fifi.CurHits = fifi.MaxHits;
        Set(0, 100, 0, 0, 0, 0, 0, 0);
        me.GoldOnHand = VbCurrency.MaxValue - 5; me.GoldInBank = -100;
        Check("the payer's hand would overflow: no lines, nothing moved",
              $"{Lines(on.CoverShortfall(party, me, 922337203685400L))} {fifi.GoldInBank} {me.GoldOnHand == VbCurrency.MaxValue - 5}", "null 100 True");

        // Off, read live; and no mod at all
        Set(0, 100, 0, 0, 0, 0, 0, 0);
        TownGold(on, "Off");
        Check("Off: nothing, as the game", $"{Lines(on.CoverShortfall(party, me, 50))} {on.WouldCoverShortfall(party, me, 50)} {All()}", "null False 0/100 0/0 0/0 0/0");
        TownGold(on, "Everyone's own bank");
        Check("Everyone's own bank again, read live", on.WouldCoverShortfall(party, me, 50), true);
        Check("mods off: nothing", $"{Lines(off.CoverShortfall(party, me, 50))} {off.WouldCoverShortfall(party, me, 50)} {All()}", "null False 0/100 0/0 0/0 0/0");
        var empty = ModHost.Empty();
        Check("empty host: nothing", $"{Lines(empty.CoverShortfall(party, me, 50))} {empty.WouldCoverShortfall(party, me, 50)}", "null False");
        Set(0, 2000, 0, 0, 0, 0, 0, 0);
        Check("the line, thousands grouped", Lines(on.CoverShortfall(party, me, 1350)), Gives("fifi", 1350));
        Set(0, 0, 0, 0, 0, 0, 0, 0);
        on.Settings.Remove("QoL", "TownGold");
    }

    /// <summary>The Store's hiding of rows the character cannot use, and the pack's marks for items not fully known.</summary>
    private static void StoreAndMarkChecks(ModHost on, Character ch, List<Item> items)
    {
        Console.WriteLine("Store: hide items you can't use:");
        var usable = items.First(i => i.Id != 0 && Character.IsWearable(i) && ch.EquipProblem(i) == null);
        var unusable = items.First(i => i.Id != 0 && ch.EquipProblem(i) != null);
        Check("off by default: an unusable row is listed", on.StoreRowVisible(ch, unusable, usable: false), true);
        on.Settings.WriteFlag("QoL", "HideUnusableInStore", true);
        Check("on: an unusable row is hidden", on.StoreRowVisible(ch, unusable, usable: false), false);
        Check("on: a usable row is listed", on.StoreRowVisible(ch, usable, usable: true), true);
        on.Settings.WriteFlag("QoL", "HideUnusableInStore", false);
        Check("off again, read live", on.StoreRowVisible(ch, unusable, usable: false), true);

        Console.WriteLine("Store: the Hide unusable box (StoreOptions), the only place the setting is set (1.8.0):");
        var boxes = on.StoreOptions.ToList();
        Check("one box, QoL's", string.Join(",", boxes.Select(b => b.Mod.Id + " " + b.Option.Label)), "QoL Hide unusable");
        var (bm, box) = boxes.Single();
        Check("shown whenever the mod runs", on.IsShown(bm, box), true);
        Check("unticked: the setting is off", on.StoreOptionValue(bm, box), false);
        int changes = 0;
        Action counted = () => changes++;
        on.Changed += counted;
        on.SetStoreOption(bm, box, true);
        Check("ticked in the Store: the setting is on, kept under its old key", on.Settings.ReadFlag("QoL", "HideUnusableInStore", false), true);
        Check("... the box reads it back", on.StoreOptionValue(bm, box), true);
        Check("... the list hides at once", on.StoreRowVisible(ch, unusable, usable: false), false);
        Check("... and the host says a setting changed (the Store redraws)", changes > 0, true);
        on.Settings.WriteFlag("QoL", "HideUnusableInStore", false);
        Check("the setting cleared: the box follows", on.StoreOptionValue(bm, box), false);
        Check("... the list shows the row again", on.StoreRowVisible(ch, unusable, usable: false), true);
        on.Changed -= counted;
        Check("no toggle on the Mods page for it", bm.Settings.Toggles.Any(t => t.Key == "HideUnusableInStore"), false);

        Console.WriteLine("Mark items not fully identified:");
        string Mark(IdLevel known)
        {
            var d = on.PackRowDecoration(ch, usable, new InventorySlot { ItemId = usable.Id, Known = known }, 1);
            var parts = new List<string>();
            for (; d != null; d = d.Then) parts.Add($"'{d.Suffix}' {d.Colour} {d.Tooltip}");
            return parts.Count == 0 ? "none" : string.Join(" | ", parts);
        }
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", false);   // the marks alone first
        Check("Barely", Mark(IdLevel.Barely), "' ?' (230, 90, 80) Barely identified");
        Check("Partial", Mark(IdLevel.Partial), "' ?' (235, 160, 50) Partly identified");
        Check("Mostly", Mark(IdLevel.Mostly), "' ?' (225, 215, 100) Mostly identified");
        Check("All: no mark", Mark(IdLevel.All), "none");
        on.Settings.WriteFlag("QoL", "MarkUnidentified", false);
        Check("toggle off: no mark", Mark(IdLevel.Barely), "none");
        on.Settings.WriteFlag("QoL", "MarkUnidentified", true);

        StoreSlotChecks(on, ch, items, usable, Mark);
    }

    /// <summary>
    /// "Store: slot and worn columns": the short label of each of MDATA1's 24 wear classes, the
    /// Store's Slot and Worn columns, and the label after a pack line known at least Partially.
    /// </summary>
    private static void StoreSlotChecks(ModHost on, Character ch, List<Item> items, Item usable, Func<IdLevel, string> mark)
    {
        Console.WriteLine("Store: slot and worn columns:");
        var asm = on.Loaded.Single(l => l.Id == "QoL").Instance!.GetType().Assembly;
        var ws = asm.GetType("Mordor.Mod.QoL.WearSlot")!;
        string Label(int cls) => (string)ws.GetMethod("Label")!.Invoke(null, new object[] { cls })!;
        Check("MDATA1 has 24 wear classes", Mdata1.ItemDescriptions.Count, 24);
        string[] want =
        {
            "Weapon", "Armor", "Shield", "Head", "Hands", "Body", "Feet", "Wrists", "Garment", "Ornament",
            "Liquid", "Particles", "Parchment", "Book", "Stick", "Neck", "Round", "Square", "Waist", "Old Object",
            "Element", "Glittering", "Crest", "Misc",
        };
        for (int k = 0; k < want.Length; k++)
            Check($"class {k} {Mdata1.ItemDescriptions[k].Name.Trim()} ({Character.MaxWornOfClass(k)} worn)", Label(k), want[k]);
        Check("out of range: nothing", $"'{Label(-1)}' '{Label(24)}'", "'' ''");

        on.Settings.WriteFlag("QoL", "StoreSlotColumns", true);
        var cols = on.ShownStoreColumns();
        Check("on: Slot and Worn", string.Join(",", cols.Select(c => $"{c.Column.Header} {c.Column.Width}")), "Slot 90,Worn 150");
        var slotCol = cols[0]; var wornCol = cols[1];
        Func<short, Item?> lookup = id => items.FirstOrDefault(i => i.Id == id);
        string Cell((LoadedMod Mod, StoreColumn Column) c, Item it)
        {
            var v = on.StoreCell(c.Mod, c.Column, new StoreCellQuery(ch, it, lookup));
            return v is ModCell m ? $"{m.Text}|{m.Tooltip}" : v?.ToString() ?? "null";
        }
        Item OfClass(int cls, Item? not = null) => items.First(i => i.Id != 0 && Character.WearClassOf(i) == cls && i != not);
        var belt = OfClass(18);
        var otherBelt = OfClass(18, belt);
        Check("Slot: the label, class and type in the tooltip", Cell(slotCol, belt), $"Waist|Waist Protection - {Mdata1.ItemTypeName(belt.Type).Trim()}");
        var potion = OfClass(10);
        Check("Slot of a potion", Cell(slotCol, potion), "Liquid|Liquid - Potion");
        Check("Worn: nothing on in that class", Cell(wornCol, otherBelt), "|");
        var on1 = new InventorySlot { ItemId = belt.Id, Known = IdLevel.All, Equipped = true };
        ch.PutInPack(on1);
        Check("Worn: the belt worn, as the pack names it", Cell(wornCol, otherBelt), $"{Loot.NameFor(belt, IdLevel.All)}|Worn now: {Loot.NameFor(belt, IdLevel.All)}");
        on1.Known = IdLevel.Partial;
        Check("... at the slot's own level of knowledge", Cell(wornCol, otherBelt).Split('|')[0], Loot.NameFor(belt, IdLevel.Partial));
        Check("Worn for another class: nothing", Cell(wornCol, OfClass(3)), "|");
        Check("Worn for a consumable: nothing", Cell(wornCol, potion), "|");
        on1.Equipped = false;
        Check("carried, not worn: nothing", Cell(wornCol, otherBelt), "|");
        ch.RemoveFromPack(on1);

        Console.WriteLine("pack label after an item known at least Partially:");
        string l = Label(Character.WearClassOf(usable));
        string tip = $"{Mdata1.ItemDescriptions[Character.WearClassOf(usable)].Name.Trim()} - {Mdata1.ItemTypeName(usable.Type).Trim()}";
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", true);
        Check("Barely: the mark alone, no label", mark(IdLevel.Barely), "' ?' (230, 90, 80) Barely identified");
        Check("Partial: the mark, then the label", mark(IdLevel.Partial), $"' ?' (235, 160, 50) Partly identified | ' [{l}]' (140, 140, 155) {tip}");
        Check("All: the label alone", mark(IdLevel.All), $"' [{l}]' (140, 140, 155) {tip}");
        on.Settings.WriteFlag("QoL", "MarkUnidentified", false);
        Check("marks off: Mostly has the label alone", mark(IdLevel.Mostly), $"' [{l}]' (140, 140, 155) {tip}");
        Check("marks off: Barely has nothing", mark(IdLevel.Barely), "none");
        on.Settings.WriteFlag("QoL", "MarkUnidentified", true);
        // pack slot 0 is the Hands, MDATA3's item 0 (sub0F74, MAINMOD2.BAS:1723): no "[Weapon]" after them
        var hands = items.Single(i => i.Id == 0);
        var handsMark = on.PackRowDecoration(ch, hands, new InventorySlot { ItemId = 0, Known = IdLevel.All }, 0);
        Check("the Hands (slot 1, item 0): no label", handsMark == null ? "none" : $"'{handsMark.Suffix}'", "none");
        Check("... a Weapon's class all the same, so it is the Hands that are left out", Label(Character.WearClassOf(hands)), "Weapon");
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", false);
        Check("toggle off: no columns", on.ShownStoreColumns().Count, 0);
        Check("toggle off: no label", mark(IdLevel.All), "none");
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", true);
    }

    /// <summary>
    /// "Store: sortable columns": the Item and Slot headers' clicks (A-Z, Z-A, the shelf's order),
    /// their marks, the order itself, the sort kept in settings.ini, the hidden rows left out before
    /// sorting, and nothing at all with the toggle off or no mod.
    /// </summary>
    private static void StoreSortChecks(ModHost on, ModHost off, Character ch, List<Item> items, Func<short, Item?> lookup)
    {
        Console.WriteLine("Store: sortable columns:");
        // a shelf as the Store lists it: item order, some of every class, usable and not
        var shelf = items.Where(i => i.Id != 0 && i.Name.Trim() != "").GroupBy(i => Character.WearClassOf(i))
            .SelectMany(g => g.Take(3)).OrderBy(i => i.Id).ToList();
        // the Store's own drawing: what a mod hides left out, then the order asked for
        List<Item> Drawn() => on.StoreRowOrder(ch, shelf.Where(i => on.StoreRowVisible(ch, i, ch.EquipProblem(i) == null)).ToList(), lookup);
        string Ids(IEnumerable<Item> l) => string.Join(",", l.Select(i => i.Id));
        string Name(Item i) => i.Name.Trim();
        var asm = on.Loaded.Single(l => l.Id == "QoL").Instance!.GetType().Assembly;
        var ws = asm.GetType("Mordor.Mod.QoL.WearSlot")!;
        string Label(Item i) => (string)ws.GetMethod("LabelOf")!.Invoke(null, new object[] { i })!;
        string Arrow(string h) => on.StoreHeaderSort(h) is { } a ? a.Arrow.ToString() : "not sortable";
        void Click(string h) { if (on.StoreHeaderSort(h) is { } a) on.StoreHeaderClicked(a.Mod, h); }
        var cmp = StringComparer.OrdinalIgnoreCase;

        on.Settings.WriteFlag("QoL", "StoreSlotColumns", true);
        Check("on by default", on.Loaded.Single(l => l.Id == "QoL").Settings.Get("StoreSortable"), true);
        Check("no sort yet: the shelf's order", Ids(Drawn()), Ids(shelf));
        Check("Item and Slot sortable, unmarked; Worn not", $"{Arrow("Item")} {Arrow("Slot")} {Arrow("Worn")}", "None None not sortable");
        Check("... owned by QoL", on.StoreHeaderSort("Item")?.Mod.Id, "QoL");

        Click("Item");
        Check("Item once: A to Z", Ids(Drawn()), Ids(shelf.OrderBy(Name, cmp)));
        Check("... names in order", Drawn().Select(Name).Zip(Drawn().Select(Name).Skip(1)).All(p => cmp.Compare(p.First, p.Second) <= 0), true);
        Check("... Item marked ascending, Slot not", $"{Arrow("Item")} {Arrow("Slot")}", "Ascending None");
        Click("Item");
        Check("Item twice: Z to A", Ids(Drawn()), Ids(shelf.OrderByDescending(Name, cmp)));
        Check("... marked descending", Arrow("Item"), "Descending");
        Click("Item");
        Check("Item three times: the shelf's order again", Ids(Drawn()), Ids(shelf));
        Check("... unmarked", Arrow("Item"), "None");

        Click("Slot");
        Check("Slot once: by slot, then name", Ids(Drawn()), Ids(shelf.OrderBy(Label, cmp).ThenBy(Name, cmp)));
        Check("... slot labels in order", Drawn().Select(Label).Zip(Drawn().Select(Label).Skip(1)).All(p => cmp.Compare(p.First, p.Second) <= 0), true);
        Check("... only Slot marked", $"{Arrow("Item")} {Arrow("Slot")}", "None Ascending");
        Click("Slot");
        Check("Slot twice: reversed", Ids(Drawn()), Ids(shelf.OrderByDescending(Label, cmp).ThenByDescending(Name, cmp)));
        Click("Item");
        Check("Item after Slot: one sort at a time, Item A to Z", $"{Arrow("Item")} {Arrow("Slot")} {Ids(Drawn()) == Ids(shelf.OrderBy(Name, cmp))}", "Ascending None True");

        Console.WriteLine("... kept in settings.ini:");
        Click("Item");   // Z to A
        Check("the setting", new ModSettingsStore(on.Settings.Path).Read("QoL", "StoreSortOrder"), "-Item");
        var again = new ModHost(new ModSettingsStore(on.Settings.Path), _ => { });
        again.Discover(on.Discovered.Select(d => Path.GetDirectoryName(d.Directory)!).Distinct());
        again.LoadEnabled();
        Check("a new start (the Store opened again): still Z to A", $"{again.StoreHeaderSort("Item")?.Arrow} {Ids(again.StoreRowOrder(ch, shelf, lookup)) == Ids(shelf.OrderByDescending(Name, cmp))}", "Descending True");

        Console.WriteLine("... hidden rows left out before sorting:");
        on.Settings.WriteFlag("QoL", "HideUnusableInStore", true);
        var listed = shelf.Where(i => ch.EquipProblem(i) == null).ToList();
        Check("some rows hidden, some listed", listed.Count > 0 && listed.Count < shelf.Count, true);
        Check("the listed rows only, Z to A", Ids(Drawn()), Ids(listed.OrderByDescending(Name, cmp)));
        on.Settings.WriteFlag("QoL", "HideUnusableInStore", false);

        Console.WriteLine("... Slot with the slot columns off (each its own toggle again, 1.11.0):");
        Click("Slot");
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", false);
        Check("Slot not sortable, Item still, the shelf's order", $"{Arrow("Slot")} {Arrow("Item")} {Ids(Drawn()) == Ids(shelf)}", "not sortable None True");
        Check("... the sort kept for when they come back", on.Settings.Read("QoL", "StoreSortOrder"), "Slot");
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", true);
        Check("columns back: sorted by slot again", Arrow("Slot"), "Ascending");

        Console.WriteLine("... toggle off, and no mod:");
        on.Settings.WriteFlag("QoL", "StoreSortable", false);
        Check("toggle off: no header sortable", $"{Arrow("Item")} {Arrow("Slot")}", "not sortable not sortable");
        Check("toggle off: the shelf's order", Ids(Drawn()), Ids(shelf));
        Check("toggle off: the Slot and Worn columns stay (their own toggle)", on.ShownStoreColumns().Count, 2);
        on.Settings.WriteFlag("QoL", "StoreSortable", true);
        Check("toggle on again, read live: sorted again", Arrow("Slot"), "Ascending");
        Check("mods off: the shelf's order", Ids(off.StoreRowOrder(ch, shelf, lookup)), Ids(shelf));
        Check("mods off: no header sortable", off.StoreHeaderSort("Item") == null && off.StoreHeaderSort("Slot") == null, true);
        Check("empty host: the shelf's order, no header", $"{Ids(ModHost.Empty().StoreRowOrder(ch, shelf, lookup)) == Ids(shelf)} {ModHost.Empty().StoreHeaderSort("Item") == null}", "True True");
        on.Settings.Write("QoL", "StoreSortOrder", "");
    }

    /// <summary>
    /// "Say what a sale adds to the Store": the Store's own shelving test (cmdSell_Click, FRMCTYS.BAS:264-267),
    /// and the line the mod writes when it holds - a/an by the first letter, the tag only when aligned.
    /// </summary>
    private static void ShelvedChecks(ModHost on, Character ch, List<Item> items)
    {
        Console.WriteLine("Say what a sale adds to the Store:");
        var said = new List<string>();
        var vowel = items.First(i => i.Id != 0 && i.Cursed == 0 && i.Floor > 0 && "AEIOU".IndexOf(char.ToUpperInvariant(i.Name.Trim()[0])) >= 0);
        var consonant = items.First(i => i.Id != 0 && i.Cursed == 0 && i.Floor > 0 && "AEIOU".IndexOf(char.ToUpperInvariant(i.Name.Trim()[0])) < 0);
        on.StoreShelved(ch, vowel, ItemAlignment.Good, said.Add);
        Check("a vowel: an, and the Good tag", said.LastOrDefault(), $"The store is now stocking an {vowel.Name.Trim()} (g).");
        on.StoreShelved(ch, consonant, ItemAlignment.Unaligned, said.Add);
        Check("a consonant: a, unaligned: no tag", said.LastOrDefault(), $"The store is now stocking a {consonant.Name.Trim()}.");
        on.StoreShelved(ch, consonant, ItemAlignment.Neutral, said.Add);
        Check("Neutral's tag", said.LastOrDefault()?.EndsWith(" (n)."), true);
        on.StoreShelved(ch, consonant, ItemAlignment.Evil, said.Add);
        Check("Evil's tag", said.LastOrDefault()?.EndsWith(" (e)."), true);
        int n = said.Count;
        on.Settings.WriteFlag("QoL", "SayStoreShelved", false);
        on.StoreShelved(ch, vowel, ItemAlignment.Good, said.Add);
        Check("toggle off: nothing said", said.Count, n);
        on.Settings.WriteFlag("QoL", "SayStoreShelved", true);
        ModHost.Empty().StoreShelved(ch, vowel, ItemAlignment.Good, said.Add);
        Check("no mod: nothing said", said.Count, n);

        // the event is raised only where the Store shelves: its own test
        var shelf = new StoreStock();
        var own = items.FirstOrDefault(i => i.Id != 0 && i.Floor == 0 && i.Cursed == 0);
        var cursed = items.FirstOrDefault(i => i.Id != 0 && i.Cursed != 0);
        Check("an ordinary sale shelves", shelf.ShelvesSale(vowel, ItemAlignment.Good), true);
        if (cursed != null) Check("a cursed item is never shelved", shelf.ShelvesSale(cursed, ItemAlignment.Neutral), false);
        if (own != null)
        {
            Check("the Store's own floor-0 goods, unaligned: not shelved", shelf.ShelvesSale(own, ItemAlignment.Unaligned), false);
            Check("... aligned: shelved", shelf.ShelvesSale(own, ItemAlignment.Good), true);
        }
        for (int i = 0; i < StoreStock.ShelfCap; i++) shelf.Add(vowel.Id, ItemAlignment.Good);
        Check("a full row: not shelved", shelf.ShelvesSale(vowel, ItemAlignment.Good), false);
        Check("... another alignment's row still is", shelf.ShelvesSale(vowel, ItemAlignment.Evil), true);
        string store = PortSource("src/Mordor.App", "CityWindow.Store.cs");
        Check("the Store's sell path asks that test and tells the mods there",
              System.Text.RegularExpressions.Regex.IsMatch(store, @"if \(shelve && stock\.ShelvesSale\(it, slot\.Alignment\)\)\s*\{\s*stock\.Add\(it\.Id, slot\.Alignment\);[^}]*ModHost\.Active\.StoreShelved\(_ch, it, slot\.Alignment, Log\);"), true);
    }

    private static string PortSource(params string[] parts)
    {
        string p = PortTree.At(parts);
        if (File.Exists(p)) return File.ReadAllText(p);
        Console.WriteLine($"  FAIL  port source not found: {p}");
        _failed++;
        return "";
    }

    private static Character MakeChar(string name)
    {
        var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 12, 12, 12, 12, 12, 12 });
        c.ClearPack();
        return c;
    }

    /// <summary>
    /// 1.7.0's "Mark active quests": the game's own quest state (Progress.QuestStateOf), the mark and
    /// the tooltip's line (the F5 line), the toggle read live, and the host's Next cell without it.
    /// The marked cells are controls, which this probe cannot build (no Avalonia), so the marked
    /// text is checked through QuestMark.Text and the host only where it hands back a string.
    /// </summary>
    private static void QuestChecks(ModHost on, ModHost off, LoadedMod lm, List<Monster> monsters, List<Item> items, Func<short, Item?> lookup)
    {
        Console.WriteLine("Mark active quests:");
        var asm = lm.Instance!.GetType().Assembly;
        var qm = asm.GetType("Mordor.Mod.QoL.QuestMark")!;
        var xp = asm.GetType("Mordor.Mod.QoL.XpColumn")!;
        var qol = asm.GetType("Mordor.Mod.QoL.QolMod")!;
        Monster? MonsterOf(short id) => monsters.FirstOrDefault(m => m.Id == id);
        Func<short, Monster?> mon = MonsterOf;
        object? Of(Character c) => qm.GetMethod("Of")!.Invoke(null, new object[] { c, mon, lookup });
        string? Glyph(Character c) => Of(c) is { } m ? (string)m.GetType().GetProperty("Glyph")!.GetValue(m)! : null;
        string? Tip(Character c) => Of(c) is { } m ? (string)m.GetType().GetProperty("Tip")!.GetValue(m)! : null;
        string Text(Character c) => (string)qm.GetMethod("Text")!.Invoke(null, new object[] { c, mon, lookup })!;
        string Cell(Character c) => (string)xp.GetMethod("Cell")!.Invoke(null, new object[] { c })!;
        object? MarkFor(Character c) => qol.GetMethod("QuestMarkFor")!.Invoke(null, new object[] { new PartyCellQuery(c, mon, lookup) });

        var monster = monsters.First(m => m.Id != 0 && !string.IsNullOrWhiteSpace(m.Name));
        var item = items.First(i => i.Id != 0 && !string.IsNullOrWhiteSpace(i.Name));
        var who = MakeChar("Quinn");
        who.Guild.Experience = who.ExpForNextLevel() - 4321;
        Check("the figure without a quest", Cell(who), "4.3K");

        // no quest
        Check("no quest: state", Progress.QuestStateOf(who), Progress.QuestState.None);
        Check("no quest: no mark", Of(who), null);
        Check("no quest: the figure alone", Text(who), "4.3K");

        // a monster quest, set as the Guild sets it (FRMCTYG.BAS:484-561)
        who.Guild.QuestType = 1; who.Guild.QuestTarget = monster.Id; who.Guild.QuestDone = false;
        Check("monster quest: Active", Progress.QuestStateOf(who), Progress.QuestState.Active);
        Check("monster quest: a gold !", Glyph(who), "!");
        Check("monster quest: marked text", Text(who), "!4.3K");
        Check("monster quest: the tooltip's line is F5's", Tip(who), "Guild quest: You must defeat one " + monster.Name.Trim() + ".");
        Check("... F5 says the same", Progress.Quest(who, mon, lookup), "You must defeat one " + monster.Name.Trim() + ".");

        // the creature slain: M57B2 (sub0CF3)
        who.Guild.QuestDone = true;
        Check("monster quest done: Done", Progress.QuestStateOf(who), Progress.QuestState.Done);
        Check("done: a green tick", Glyph(who), "✓");
        Check("done: marked text", Text(who), "✓4.3K");
        Check("done: the tooltip's line is F5's", Tip(who), "Guild quest: Your quest has been completed.");
        who.Guild.Experience = who.ExperienceCap();
        Check("done and pinned: the tick before Pinned", Text(who), "✓Pinned");
        who.Guild.Experience = who.ExpForNextLevel() - 4321;

        // an item quest: done only when the guild takes it, so carrying it leaves it Active
        who.Guild.QuestType = 2; who.Guild.QuestTarget = item.Id; who.Guild.QuestDone = false;
        Check("item quest: Active", Progress.QuestStateOf(who), Progress.QuestState.Active);
        Check("item quest: marked text", Text(who), "!4.3K");
        Check("item quest: the tooltip's line is F5's", Tip(who), "Guild quest: You must return with one " + item.Name.Trim() + ".");
        who.PutInPack(new InventorySlot { ItemId = item.Id });
        Check("item quest, the item carried: still Active (the game says nothing more)", Progress.QuestStateOf(who), Progress.QuestState.Active);
        Check("... the guild would take it", who.QuestSatisfied(consume: false), true);

        // a kind the game does not know: no readout says anything, so no mark
        who.Guild.QuestType = 3;
        Check("an unknown quest kind: None, as F5 says nothing", $"{Progress.QuestStateOf(who)} {Progress.Quest(who, mon, lookup) ?? "null"}", "None null");
        who.Guild.QuestType = 1; who.Guild.QuestTarget = monster.Id;

        // only the current guild's quest counts: another guild's is not marked
        var other = MakeChar("Rhea");
        other.Guilds.Add(new GuildMembership { Guild = "Other", Level = 3, QuestType = 1, QuestTarget = monster.Id });
        Check("a quest in a guild not current: no mark", Of(other), null);

        // the Next column, read live: the mark rides in it, so it goes with it
        string Cols() => string.Join(",", on.ShownPartyColumns().Select(c => c.Column.Header));
        on.Settings.WriteFlag("QoL", "XpToNextColumn", false);
        Check("the Next column off: no Next column, so no mark", Cols(), "HP/SP");
        on.Settings.WriteFlag("QoL", "XpToNextColumn", true);
        Check("on: the columns back", Cols(), "HP/SP,Next");
        Check("on: the mark", MarkFor(who) is { } mk ? mk.GetType().GetProperty("Glyph")!.GetValue(mk) : null, "!");
        Check("no quest: no mark", MarkFor(MakeChar("Sol")), null);
        Check("QoL not faulted by the quest mark", lm.Faulted, false);

        // mods off: no column at all, and F5's readout is the game's own
        Check("mods off: no party columns", off.ShownPartyColumns().Count, 0);
        Check("mods off: F5's quest line unchanged", Progress.Quest(who, mon, lookup), "You must defeat one " + monster.Name.Trim() + ".");
    }

    /// <summary>
    /// The HP/SP and experience bars' arithmetic, the Combat Log's store, the caption event and the
    /// longer Messages list. The bars' controls need Avalonia, which this probe has not got, so
    /// only their numbers are checked here; the columns are checked by header and visibility.
    /// </summary>
    private static void BarsAndLogChecks(ModHost on, LoadedMod lm, Character member)
    {
        var asm = lm.Instance!.GetType().Assembly;
        var bars = asm.GetType("Mordor.Mod.QoL.Bars")!;
        object? Call(string name, params object[] args) =>
            bars.GetMethods().Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        string Frac(long cur, long max) => Call("Fraction", cur, max) is double d ? d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "none";

        Console.WriteLine("HP and SP bars (fraction, percentage, colour):");
        foreach (var (cur, max, want) in new (long, long, string)[]
        {
            (15, 15, "1"), (12, 15, "0.8"), (0, 15, "0"), (-3, 15, "0"), (20, 15, "1"), (1, 3, "0.333"), (5, 0, "none"),
        })
            Check($"fraction {cur}/{max}", Frac(cur, max), want);
        foreach (var (f, want) in new (double, int)[] { (1.0, 100), (0.999, 99), (0.8, 80), (0.5, 50), (0.004, 1), (0.0, 0), (0.12345, 12) })
            Check($"percent {f}", Call("Percent", f), want);
        foreach (var (f, want) in new (double, string)[]
        {
            (1.0, "(60, 200, 70)"),      // green
            (0.75, "(145, 205, 60)"),    // half-way green to yellow
            (0.5, "(230, 210, 50)"),     // yellow
            (0.25, "(225, 130, 45)"),    // half-way yellow to red
            (0.0, "(220, 50, 40)"),      // red
            (-1.0, "(220, 50, 40)"), (2.0, "(60, 200, 70)"),
        })
            Check($"colour at {f}", Call("Colour", f)?.ToString(), want);

        Console.WriteLine("SP bar (blue, through violet, to a dim purple):");
        foreach (var (f, want) in new (double, string)[]
        {
            (1.0, "(70, 150, 255)"),     // bright blue
            (0.75, "(110, 123, 245)"),   // half-way blue to violet
            (0.5, "(150, 95, 235)"),     // violet
            (0.25, "(135, 78, 193)"),    // half-way violet to dim purple
            (0.0, "(120, 60, 150)"),     // dim purple
            (-1.0, "(120, 60, 150)"), (2.0, "(70, 150, 255)"),
        })
            Check($"SP colour at {f}", Call("SpColour", f)?.ToString(), want);

        Console.WriteLine("XP bar, two stages (green: this level's fn12A1 to the next's; purple: the next's to the pin, fn13ED):");
        var c = MakeChar("Dora");
        string F(double d) => d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        // stage one's fill, stage two's fill, stage two shown, pinned, toolbar state
        string Xp() { var s = (System.Runtime.CompilerServices.ITuple)Call("Experience", c)!; return $"{F((double)s[0]!)} {F((double)s[1]!)} {s[2]} {s[3]} {s[4]}"; }
        string Along(long v, long a, long b) => F((double)(v - a) / (b - a));
        var xpCol = asm.GetType("Mordor.Mod.QoL.XpColumn")!;
        string Tip() => (string)xpCol.GetMethod("Tip")!.Invoke(null, new object[] { c })!;
        string Text() => (string)xpCol.GetMethod("Cell")!.Invoke(null, new object[] { c })!;
        string Sh(long n) => (string)xpCol.GetMethod("Short")!.Invoke(null, new object[] { n })!;
        Check("level 1 floor is nought", c.ExpForThisLevel(), 0);
        long next = c.ExpForNextLevel(), cap = c.ExperienceCap();
        Check("the pin lies past the next level (level 1)", cap > next, true);
        c.Guild.Experience = 0;
        Check("level 1, no experience: stage one empty", Xp(), "0 0 False False Short");
        Check("... text: the whole amount to the next level", Text(), Sh(next));
        c.Guild.Experience = next / 2;
        Check("level 1, half way to the next", Xp(), $"{Along(next / 2, 0, next)} 0 False False Short");
        Check("... text: what is left to the next level", Text(), Sh(next - next / 2));
        Check("tooltip short: both numbers", Tip(), $"Level 2: {(int)Math.Floor(100.0 * (next / 2) / next)}% of the way\n{next - next / 2:N0} to next level, {cap - next / 2:N0} to pin");
        c.Guild.Experience = next - 1;
        var oneShort = (System.Runtime.CompilerServices.ITuple)Call("Experience", c)!;
        Check("one short: stage one never whole", $"{(double)oneShort[0]! < 1} {oneShort[1]} {oneShort[2]}", "True 0 False");
        c.Guild.Experience = next;
        Check("Ready at the threshold: stage one 100%, stage two from 0", Xp(), "1 0 True False Ready");
        Check("... text: the amount to the pin", Text(), Sh(cap - next));
        long mid = next + (cap - next) / 2;
        c.Guild.Experience = mid;
        Check("Ready, half way to the pin", Xp(), $"1 {Along(mid, next, cap)} True False Ready");
        Check("... text: what is left to the pin", Text(), Sh(cap - mid));
        Check("tooltip Ready", Tip(), $"Ready to level 2 - {cap - mid:N0} more to the pin");
        c.Guild.Experience = cap - 1;
        var nearPin = (System.Runtime.CompilerServices.ITuple)Call("Experience", c)!;
        Check("one short of the pin: stage two never whole", $"{nearPin[0]} {(double)nearPin[1]! < 1} {nearPin[2]} {nearPin[3]}", "1 True True False");
        c.Guild.Experience = cap;
        Check("Pinned: stage two 100%", Xp(), "1 1 True True Pinned");
        Check("... text Pinned", Text(), "Pinned");
        Check("tooltip Pinned", Tip(), "Pinned: make the level, or experience is lost");
        c.Guild.Experience = cap + 500;   // left above the pin (a level drained away beneath it)
        Check("past the pin: pinned look, the toolbar's Ready", Xp(), "1 1 True True Ready");
        Check("... text Pinned, not a negative amount", Text(), "Pinned");
        Check("tooltip past the pin: no negative amount", Tip(), $"Ready to level 2 - {500:N0} past the pin: make the level, or the next experience earned takes it back to the pin");
        c.Guild.Level = 5;
        long from = c.ExpForThisLevel(), to = c.ExpForNextLevel(), pin = c.ExperienceCap();
        Check("level 5 floor is fn12A1 at level 4", from, Rules.Fn12A1(4, c.ActiveGuildDef.ExpFactor, c.RaceDef.ExpFactor));
        Check("level 5's pin is fn13ED", pin, Rules.Fn13ED(5, c.ActiveGuildDef.ExpFactor, c.RaceDef.ExpFactor));
        Check("level 5's floor is above nought", from > 0, true);
        long quarter = from + (to - from) / 4;
        c.Guild.Experience = quarter;
        Check("level 5, a quarter of the way: from this level's floor", Xp(), $"{Along(quarter, from, to)} 0 False False Short");
        Check("... text", Text(), Sh(to - quarter));
        long mid5 = to + (pin - to) / 2;
        c.Guild.Experience = mid5;
        Check("level 5, half way to the pin: from the next level's threshold", Xp(), $"1 {Along(mid5, to, pin)} True False Ready");
        // the first level where the pin lies more than 2,300 past the next
        for (c.Guild.Level = 1; c.Guild.Level < 200 && c.ExperienceCap() - c.ExpForNextLevel() <= 2300; c.Guild.Level++) { }
        Check("a level with the pin more than 2,300 past the next", c.ExperienceCap() - c.ExpForNextLevel() > 2300, true);
        c.Guild.Experience = c.ExperienceCap() - 2300;
        Check("2,300 short of the pin: text", Text(), "2.3K");
        Check("... tooltip", Tip(), $"Ready to level {c.Guild.Level + 1} - 2,300 more to the pin");
        c.Guild.Level = 5;
        c.Guild.Experience = from - 10;   // drained below the level's floor
        Check("drained below the floor: empty, not negative", Xp(), "0 0 False False Short");
        Check("... text: the whole amount still short", Text(), Sh(to - (from - 10)));
        Check("colours: to next green, to pin magenta purple",
              $"{bars.GetField("ToNextColour")!.GetValue(null)} {bars.GetField("ToPinColour")!.GetValue(null)}", "(50, 175, 95) (205, 70, 185)");
        var pinCol = ((byte R, byte G, byte B))bars.GetField("ToPinColour")!.GetValue(null)!;
        bool bluish((byte R, byte G, byte B) x) => x.B > x.R;
        Check("the XP purple is redder than blue; every SP stop bluer than red",
              $"{bluish(pinCol)} {bluish(((byte, byte, byte))Call("SpColour", 1.0)!)} {bluish(((byte, byte, byte))Call("SpColour", 0.5)!)} {bluish(((byte, byte, byte))Call("SpColour", 0.0)!)}",
              "False True True True");

        Console.WriteLine("party columns with the bars:");
        var s = on.Settings;
        string Cols() => string.Join(",", on.ShownPartyColumns().Select(p => p.Column.Header));
        Check("both on: HP/SP and Next (the XP bar rides in Next)", Cols(), "HP/SP,Next");
        s.WriteFlag("QoL", "HpSpBars", false); Check("the HP/SP bars off: Next alone", Cols(), "Next");
        s.WriteFlag("QoL", "XpToNextColumn", false); Check("and the Next column off: neither", Cols(), "");
        s.WriteFlag("QoL", "HpSpBars", true); Check("the HP/SP bars back alone", Cols(), "HP/SP");
        s.WriteFlag("QoL", "XpToNextColumn", true);
        Check("widths: HP/SP 40, Next 50 (44 before 1.7.0's quest mark); no XP column since 1.8.0",
              string.Join(",", lm.UI.PartyColumns.Select(p => p.Header + " " + p.Width)), "HP/SP 40,Next 50");

        Console.WriteLine("Color-coded combat messages:");
        string Colour(MessageKind k) => on.MessageLineColour("You were killed by a Kobold.", k)?.ToString() ?? "-";
        s.WriteFlag("QoL", "ColorCodedCombatMessages", true);
        Check("toggle on: the foes' lines indian red", Colour(MessageKind.Foes), "(205, 92, 92)");
        Check("toggle on: a line of no kind keeps the window's colour", Colour(MessageKind.None), "-");
        s.WriteFlag("QoL", "ColorCodedCombatMessages", false);
        Check("toggle off, read live: the window's own", Colour(MessageKind.Foes), "-");
        Check("toggle off: Messages keeps 20", on.MessagesKept(MessageList.Kept), 20);
        Check("no window offered (the Combat Log is gone)", on.Windows.Count(), 0);
        s.WriteFlag("QoL", "ColorCodedCombatMessages", true);
        on.CaptionShown(CaptionKind.CharacterHit, "You hit the Kobold for 3 damage!", member);   // nobody listening any more: nothing happens
        on.MessageLogged("A line for Messages.");
        Check("the mod no longer keeps a log", lm.Instance!.GetType().GetProperty("Log") == null && asm.GetType("Mordor.Mod.QoL.CombatLog") == null, true);

        // put the toggles back as the rest of the probe expects them
        foreach (var k in new[] { "XpToNextColumn", "HpSpBars", "XpBar", "MarkActiveQuests", "MarkUnidentified" }) s.WriteFlag("QoL", k, true);
    }

    private static int OwnedPatches(string owner) =>
        HarmonyLib.Harmony.GetAllPatchedMethods().Select(HarmonyLib.Harmony.GetPatchInfo)
            .Where(i => i != null).Sum(i => i!.Owners.Count(o => o == owner));

    /// <summary>A mod folder holding this probe's own assembly, with <paramref name="type"/> as its entry.</summary>
    private static void MakeTestMod(string root, string id, string type)
    {
        string d = Path.Combine(root, id);
        Directory.CreateDirectory(d);
        File.Copy(typeof(ModProbe).Assembly.Location, Path.Combine(d, id + ".dll"), true);
        File.WriteAllText(Path.Combine(d, "mod.json"),
            "{ \"id\": \"" + id + "\", \"name\": \"" + id + "\", \"version\": \"1\", \"gameVersion\": \"1.1\", "
            + "\"assembly\": \"" + id + ".dll\", \"entryType\": \"" + type + "\" }");
    }

    /// <summary>
    /// 1.6.0's Bank additions: the identification mark after a line of the Bank's list (the pack's
    /// mark, without the slot label) and the Bank's Sort, which puts the vault in the pack sort's order.
    /// </summary>
    private static void BankChecks(ModHost on, List<Item> items, Func<short, Item?> lookup)
    {
        Console.WriteLine("Bank: the identification mark and the Sort button:");
        var ch = MakeChar("Banker");
        var one = items.First(i => i.Id != 0);
        string Mark(IdLevel known)
        {
            var d = on.BankRowDecoration(ch, one, new InventorySlot { ItemId = one.Id, Known = known }, 3);
            return d == null ? "none" : $"'{d.Suffix}' {d.Colour} {d.Tooltip}{(d.Then == null ? "" : " and more")}";
        }
        on.Settings.WriteFlag("QoL", "StoreSlotColumns", true);   // the pack would add its slot label; the Bank does not
        Check("Bank line, Barely", Mark(IdLevel.Barely), "' ?' (230, 90, 80) Barely identified");
        Check("Bank line, Partial", Mark(IdLevel.Partial), "' ?' (235, 160, 50) Partly identified");
        Check("Bank line, Mostly", Mark(IdLevel.Mostly), "' ?' (225, 215, 100) Mostly identified");
        Check("Bank line, All: no mark", Mark(IdLevel.All), "none");
        on.Settings.WriteFlag("QoL", "MarkUnidentified", false);
        Check("Bank line, mark toggle off: none", Mark(IdLevel.Barely), "none");
        on.Settings.WriteFlag("QoL", "MarkUnidentified", true);

        var sort = on.BankCommands.FirstOrDefault(p => p.Command.Label == "Sort");
        Check("Bank Sort added", sort.Command != null, true);
        if (sort.Command == null) return;
        Check("Bank Sort shown", on.IsShown(sort.Mod, sort.Command), true);
        on.Settings.WriteFlag("QoL", "BankSort", false);
        Check("an old BankSort=False is not read: still shown", on.IsShown(sort.Mod, sort.Command), true);
        Check("... and the Objects & Spells Sort shown", on.ObjectsCommands.Where(p => p.Command.Label == "Sort").All(p => on.IsShown(p.Mod, p.Command)), true);

        // a vault with holes (withdrawals) and the items in reverse, worn kinds and consumables both
        var picks = items.Where(i => i.Id != 0).GroupBy(i => i.Type).Select(g => g.First()).Take(8)
            .Concat(items.Where(i => i.Id != 0 && Character.MaxWornOfClass(Character.WearClassOf(i)) == 0).GroupBy(i => i.Type).Select(g => g.First()).Take(2))
            .Reverse().ToList();
        var placed = new List<InventorySlot>();
        ch.Bank = new List<InventorySlot>();
        for (int k = 0; k < picks.Count; k++)
        {
            if (k % 3 == 1) ch.Bank.Add(new InventorySlot());   // a hole
            var s = new InventorySlot
            {
                ItemId = picks[k].Id, Charges = (short)(k + 1), Known = k % 2 == 0 ? IdLevel.All : IdLevel.Partial,
                Alignment = (ItemAlignment)(k % 4), SlotCursed = k == 2,
            };
            ch.Bank.Add(s);
            placed.Add(s);
        }
        int count = ch.Bank.Count;
        var state = placed.ToDictionary(s => s, s => (s.ItemId, s.Charges, s.Alignment, s.Known, s.SlotCursed));
        // the same items in the same order in a pack, for the pack's Sort to put in its order
        var twin = MakeChar("Twin");
        for (int k = 0; k < placed.Count; k++)
            twin.PutInSlot(k + 1, new InventorySlot { ItemId = placed[k].ItemId, Charges = placed[k].Charges, Known = placed[k].Known, Alignment = placed[k].Alignment });
        var packSort = on.ObjectsCommands.First(p => p.Command.Label == "Sort");
        on.Run(packSort.Mod, packSort.Command, new ObjectsCommandContext(twin, inDungeon: false, inCombat: false, _ => { }, lookup));

        var said = new List<string>();
        var ctx = new BankCommandContext(ch, said.Add, lookup);
        Check("Bank sort ran and changed the vault", on.Run(sort.Mod, sort.Command, ctx), true);
        Check("its line", string.Join("|", said), "Bank sorted.");
        Check("as many vault slots as before", ch.Bank.Count, count);
        Check("the items fill the vault from its first slot, the holes last",
              ch.Bank.Take(placed.Count).All(s => s.ItemId > 0) && ch.Bank.Skip(placed.Count).All(s => s.ItemId <= 0), true);
        Check("every item still in the vault, once", placed.All(s => ch.Bank.Count(b => ReferenceEquals(b, s)) == 1), true);
        Check("each record moved whole", placed.All(s => state[s] == (s.ItemId, s.Charges, s.Alignment, s.Known, s.SlotCursed)), true);
        Check("the pack sort's order", string.Join(",", ch.Bank.Take(placed.Count).Select(s => s.ItemId)),
              string.Join(",", Enumerable.Range(1, placed.Count).Select(i => twin.Pack[i]?.ItemId ?? 0)));
        said.Clear();
        Check("sorted again: nothing moves, nothing to save", on.Run(sort.Mod, sort.Command, ctx), false);
        Check("... and it still says so", string.Join("|", said), "Bank sorted.");
        var none = MakeChar("Empty");
        Check("an empty vault: nothing to do", on.Run(sort.Mod, sort.Command, new BankCommandContext(none, _ => { }, lookup)), false);
    }

    private static void SortChecks(ModHost host, LoadedMod mod, ObjectsCommand sort, List<Item> items, Func<short, Item?> lookup)
    {
        var c = MakeChar("Sorter");
        var hands = c.Pack[0];
        // a scattering of items, with gaps, and every kind of per-copy state
        // worn and consumable kinds both, so the group order is tested (a Potion - class 10 - sits
        // between the worn classes by number)
        var picks = items.Where(i => i.Id != 0).GroupBy(i => i.Type).Select(g => g.First()).Take(6)
            .Concat(items.Where(i => i.Id != 0 && Character.MaxWornOfClass(Character.WearClassOf(i)) == 0).GroupBy(i => i.Type).Select(g => g.First()).Take(2)).ToList();
        int[] at = { 3, 7, 8, 12, 20, 25, 33, 39 };
        var placed = new List<InventorySlot>();
        for (int k = 0; k < picks.Count && k < at.Length; k++)
        {
            var s = new InventorySlot
            {
                ItemId = picks[picks.Count - 1 - k].Id,     // reversed, so the sort has work to do
                Charges = (short)(k + 1),
                Known = k % 2 == 0 ? IdLevel.All : IdLevel.Partial,
                Alignment = (ItemAlignment)(k % 4),
                Equipped = k == 1,
                SlotCursed = k == 2,
            };
            c.PutInSlot(at[k], s);
            placed.Add(s);
        }
        // buffers: two items, a spell, and an empty one
        c.Buffers[0] = SpellBuffers.ForItem(at[0]);
        c.Buffers[1] = SpellBuffers.ForItem(at[5]);
        c.Buffers[2] = SpellBuffers.ForSpell(4);
        c.Buffers[3] = SpellBuffers.Empty;
        var b0 = c.Pack[at[0]]; var b1 = c.Pack[at[5]];
        var state = placed.ToDictionary(s => s, s => (s.Equipped, s.Charges, s.Alignment, s.Known, s.SlotCursed));

        var said = new List<string>();
        var ctx = new ObjectsCommandContext(c, inDungeon: true, inCombat: true, said.Add, lookup);
        bool changed = host.Run(mod, sort, ctx);
        Check("sort ran (in combat too: Swap Items has no combat test)", changed, true);
        Check("its line", string.Join("|", said), "Pack sorted.");
        Check("Hands still slot 1", ReferenceEquals(c.Pack[0], hands), true);
        int n = placed.Count;
        Check("items packed from slot 2", Enumerable.Range(1, n).All(i => c.Pack[i] is { ItemId: > 0 }), true);
        Check("empty slots last", Enumerable.Range(n + 1, Character.PackSize - n - 1).All(i => c.Pack[i] == null || c.Pack[i]!.ItemId == 0), true);
        Check("every item still carried, once", placed.All(s => Enumerable.Range(1, Character.PackSize - 1).Count(i => ReferenceEquals(c.Pack[i], s)) == 1), true);
        bool ordered = true;
        for (int i = 1; i < n; i++)
        {
            var a = lookup(c.Pack[i]!.ItemId)!; var b = lookup(c.Pack[i + 1]!.ItemId)!;
            static int G(Item x) { int k = Character.WearClassOf(x), m = Character.MaxWornOfClass(k); return (m > 0 ? 0 : m == 0 ? 1 : 2) * 1000 + k; }
            int ca = G(a), cb = G(b);
            var ka = c.Pack[i]!.Known; var kb = c.Pack[i + 1]!.Known;
            // how well each is known comes first: fully identified at the top, the less known below
            if (ka < kb) ordered = false;
            if (ka != kb) continue;
            if (ca > cb) ordered = false;
            if (ca == cb && ka >= IdLevel.Partial && a.Type > b.Type) ordered = false;
        }
        Check("fully identified first, then by ID level; within one: worn, consumables, Misc; by class, then type", ordered, true);
        Check("the not fully identified sit below every fully identified item",
              Enumerable.Range(1, n).SkipWhile(i => c.Pack[i]!.Known == IdLevel.All).All(i => c.Pack[i]!.Known != IdLevel.All), true);
        Check("state moved with each item", placed.All(s => state[s] == (s.Equipped, s.Charges, s.Alignment, s.Known, s.SlotCursed)), true);
        Check("buffer 1 follows its item", ReferenceEquals(c.Pack[c.Buffers[0]], b0), true);
        Check("buffer 2 follows its item", ReferenceEquals(c.Pack[c.Buffers[1]], b1), true);
        Check("spell buffer untouched", c.Buffers[2], SpellBuffers.ForSpell(4));
        Check("empty buffer untouched", c.Buffers[3], SpellBuffers.Empty);

        // paralysed: refused in silence, as Equip, Use and Drop are
        var before = c.Pack.ToArray();
        c.ParalysisLevel = 1;
        said.Clear();
        Check("paralysed: refused", host.Run(mod, sort, ctx), false);
        Check("paralysed: nothing said", said.Count, 0);
        Check("paralysed: pack as it was", Enumerable.Range(0, Character.PackSize).All(i => ReferenceEquals(before[i], c.Pack[i])), true);
    }
}

// ---- test mods, loaded from copies of this assembly by the probe above ----

/// <summary>Patches something, then throws in Load: it must be disabled and its patch removed.</summary>
public sealed class BoomMod : IMod
{
    public void Load(IModContext ctx)
    {
        ctx.Harmony.Patch(typeof(Loot).GetMethod(nameof(Loot.IdentifyDifficulty)),
                          postfix: new HarmonyLib.HarmonyMethod(typeof(BoomMod).GetMethod(nameof(Post))));
        throw new InvalidOperationException("boom");
    }
    public static void Post(ref double __result) => __result = -1;
}

/// <summary>Loads fine; its decision throws when asked.</summary>
public sealed class FlakyMod : IMod
{
    public void Load(IModContext ctx) =>
        ctx.Decisions.StoreAllowsRowChangeWhileChoosing = () => throw new InvalidOperationException("flaky");
}

/// <summary>Answers the Store's order with a row left out, which the host must refuse.</summary>
public sealed class ShufflyMod : IMod
{
    public void Load(IModContext ctx) =>
        ctx.Decisions.StoreRowOrder = q => q.Rows.Skip(1).Reverse().ToList();
}

/// <summary>Loads fine and patches a method, which the loader must log.</summary>
public sealed class PatchyMod : IMod
{
    public void Load(IModContext ctx) =>
        ctx.Harmony.Patch(typeof(Loot).GetMethod(nameof(Loot.IdentifyDifficulty)),
                          postfix: new HarmonyLib.HarmonyMethod(typeof(PatchyMod).GetMethod(nameof(Post))));
    public static void Post(ref double __result) => __result = 4242;
}
