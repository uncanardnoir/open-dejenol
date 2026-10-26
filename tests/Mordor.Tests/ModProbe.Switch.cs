using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// Switching a mod on and off on the main menu's Mods page, without a restart (API version 7,
/// IModLifecycle, ModHost.Switch, called there with no game going):
/// each bundled mod off and on again - its hooks stop and resume, nothing is subscribed twice,
/// its UI slots leave and come back, the save tag and settings.ini follow - and a test mod's
/// Harmony patch removed and put back, Load called once. A mod without the lifecycle waits for the
/// next start.
/// </summary>
public static partial class ModProbe
{
    private static object? Prop(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o);

    private static void SwitchChecks(string scratch, string bundled, List<Item> items, Func<short, Item?> lookup)
    {
        Console.WriteLine("switching mods on the main menu (IModLifecycle):");
        string dir = Path.Combine(scratch, "switch");
        Directory.CreateDirectory(dir);
        string ini = Path.Combine(dir, "settings.ini");
        var st = new ModSettingsStore(ini);
        st.SetEnabled("QoL", true); st.SetEnabled("Kaizen", true); st.SetEnabled("DpsMeter", true);
        st.WriteFlag("QoL", "ColorCodedCombatMessages", true);
        var log = new List<string>();
        var h = Host(ini, log, bundled);
        var party = new Party(MakeChar("Sw"), MakeChar("Itch"));
        var tb = new ToolbarButton("Automap", "AutoMap", "F8");
        var anyItem = items.First(i => i.Id != 0);
        Check("all three loaded and running", string.Join(",", h.Live.Select(m => m.Id).OrderBy(x => x)), "DpsMeter,Kaizen,QoL");
        Check("each can be switched without a restart", h.Loaded.All(m => m.SwitchesLive), true);
        Check("status: Running.", h.StatusOf("QoL", true), "Running.");

        // ---- Quality of Life ----
        var qol = h.Loaded.Single(m => m.Id == "QoL");
        bool Coloured() => h.MessageLineColour("You were killed by a Kobold.", MessageKind.Foes) != null;
        Check("QoL on: a foe's Messages line coloured", Coloured(), true);
        Check("QoL on: its tooltip, its party columns, no window of its own", $"{h.ToolbarTooltip(tb)} {h.ShownPartyColumns().Count > 0} {h.Windows.Any(w => w.Mod.Id == "QoL")}", "AutoMap (F8) True False");
        Check("QoL on: the original's 20 Messages lines kept", h.MessagesKept(20), 20);

        Check("QoL switched off: done now", h.Switch("QoL", false), ModSwitchResult.Now);
        Check("... still loaded, not active, not faulted", $"{h.Loaded.Contains(qol)} {qol.Active} {qol.Faulted}", "True False False");
        Check("... settings.ini says off", new ModSettingsStore(ini).IsEnabled("QoL"), false);
        Check("... status: Off.", h.StatusOf("QoL", false), "Off.");
        h.MessageLogged("two");
        h.CaptionShown(CaptionKind.CharacterHit, "a hit", null);
        Check("... its colours not asked: the window's own", Coloured(), false);
        Check("... its decisions not asked: the original's answers", $"{h.MessagesKept(20)} {h.CityAltP(new CityAltPRequest(party, party.Selected)).Action} {h.StoreAllowsRowChangeWhileChoosing()}", "20 PoolOnHand False");
        Check("... no tooltip", h.ToolbarTooltip(tb), null);
        Check("... its UI slots gone: Objects, Bank, party, Store columns, options, window",
              $"{h.ObjectsCommands.Count()} {h.BankCommands.Count()} {h.ShownPartyColumns().Count} {h.StoreColumns().Count} {h.StoreOptions.Count()} {h.Windows.Any(w => w.Mod.Id == "QoL")}",
              "0 0 0 0 0 False");
        Check("... no pack-line or Bank-line mark, no Store header sort",
              $"{h.PackRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 1)} {h.BankRowDecoration(party.Selected, anyItem, new InventorySlot { ItemId = anyItem.Id, Known = IdLevel.Barely }, 0)} {h.StoreHeaderSort("Item")}", "  ");
        Check("... the save tag leaves it out", string.Join(",", h.SaveTag() ?? new List<string>()).Contains("QoL"), false);
        Check("... a save tagged with it misses it", h.MissingFrom(new[] { "QoL@1.13.0" }).Count, 1);
        Check("... a second switch off does nothing more", (h.Switch("QoL", false), qol.Active).ToString(), "(Now, False)");

        Check("QoL switched on again: done now", h.Switch("QoL", true), ModSwitchResult.Now);
        Check("... the same object, not loaded a second time", ReferenceEquals(h.Loaded.Single(m => m.Id == "QoL"), qol) && h.Loaded.Count(m => m.Id == "QoL") == 1, true);
        Check("... loaded once in the log", log.Count(l => l.Contains("mod QoL: loaded")), 1);
        h.MessageLogged("three");
        Check("... a foe's line coloured again", Coloured(), true);
        Check("... its answers back", $"{h.ToolbarTooltip(tb)} {h.MessagesKept(20)} {h.ObjectsCommands.Count()} {h.BankCommands.Count()} {h.StoreColumns().Count} {h.StoreOptions.Count()}", "AutoMap (F8) 20 1 1 2 1");
        Check("... the save tag has it again", string.Join(",", h.SaveTag() ?? new List<string>()).Contains("QoL@1.13.0"), true);
        Check("... settings.ini says on", new ModSettingsStore(ini).IsEnabled("QoL"), true);
        Check("... status: Running.", h.StatusOf("QoL", true), "Running.");

        // ---- the DPS Meter ----
        var dps = h.Loaded.Single(m => m.Id == "DpsMeter");
        dynamic tally = ((dynamic)dps.Instance!).Tally;
        var hitter = party.Members[0];
        long Total() => (long)((dynamic)tally.For(hitter.Name)).Total;
        long t0 = Total();
        h.DamageDealt(new CombatDamage(hitter, 9, DamageKind.Physical, 0, true));
        Check("DPS on: a blow counted once", Total() - t0, 9);
        Check("DPS switched off: done now", h.Switch("DpsMeter", false), ModSwitchResult.Now);
        Check("... its tally written out (OnDisable)", (bool)tally.Dirty, false);
        h.DamageDealt(new CombatDamage(hitter, 9, DamageKind.Physical, 0, true));
        Check("... a blow not counted", Total() - t0, 9);
        Check("... its window gone", h.Windows.Any(w => w.Window.Title == "DPS Meter"), false);
        h.CharacterDeleted(hitter.Name);
        Check("... a character deleted meanwhile: its tally goes even so", Total(), 0);
        t0 = 0;
        Check("DPS switched on again", h.Switch("DpsMeter", true), ModSwitchResult.Now);
        h.DamageDealt(new CombatDamage(hitter, 4, DamageKind.Physical, 0, true));
        Check("... a blow counted once, not twice", Total() - t0, 4);
        Check("... its window back", h.Windows.Any(w => w.Window.Title == "DPS Meter"), true);

        // ---- Kaizen ----
        int made = 0;
        DungeonContext Make()
        {
            made++;
            return new DungeonContext(party, Array.Empty<Spell>(), lookup, () => false,
                (_, _, _) => Task.FromResult(false), (_, _, _) => Task.FromResult(false), _ => { });
        }
        h.Settings.WriteFlag("Kaizen", "BuffOnEntry", false);   // the hook runs, and casts nothing
        h.EnteredDungeonFromTown(Make).Wait();
        Check("Kaizen on: its moment runs", made, 1);
        Check("Kaizen switched off", h.Switch("Kaizen", false), ModSwitchResult.Now);
        var hookless = h.EnteredDungeonFromTown(Make);
        Check("... its moment does not run", (made, hookless.IsCompleted).ToString(), "(1, True)");
        Check("... its window gone", h.Windows.Any(w => w.Window.Title == "Kaizen"), false);
        Check("Kaizen switched on again", h.Switch("Kaizen", true), ModSwitchResult.Now);
        h.EnteredDungeonFromTown(Make).Wait();
        Check("... its moment runs once", made, 2);
        Check("... its window back", h.Windows.Any(w => w.Window.Title == "Kaizen"), true);

        // ---- all off: the original's game ----
        foreach (var id in new[] { "QoL", "DpsMeter", "Kaizen" }) h.Switch(id, false);
        Check("all three off: nothing running, no save tag", $"{h.Live.Count()} {h.SaveTag() == null}", "0 True");
        Check("... the original's answers", $"{h.MessagesKept(20)} {h.ToolbarTooltip(tb) == null} {h.Windows.Count()} {h.ShownPartyColumns().Count}", "20 True 0 0");
        foreach (var id in new[] { "QoL", "DpsMeter", "Kaizen" }) h.Switch(id, true);
        Check("all three on again", string.Join(",", h.Live.Select(m => m.Id).OrderBy(x => x)), "DpsMeter,Kaizen,QoL");
        h.Switch("QoL", false); h.Switch("DpsMeter", false); h.Switch("Kaizen", false);

        // ---- a mod switched on that was not loaded at the start ----
        Console.WriteLine("switching: a lifecycle mod with a Harmony patch, and one without the lifecycle:");
        string mods = Path.Combine(dir, "mods");
        MakeTestMod(mods, "Toggly", nameof(TogglyMod));
        MakeTestMod(mods, "Plain", nameof(PlainMod));
        string ini2 = Path.Combine(dir, "late.ini");
        var probeItem = items.First(i => i.Id != 0);
        var identify = typeof(Loot).GetMethod(nameof(Loot.IdentifyDifficulty))!;
        double Difficulty() => (double)identify.Invoke(null, new object[] { probeItem })!;
        double plain = Difficulty();
        var late = Host(ini2, log, mods);
        Check("nothing loaded at the start", late.Loaded.Count, 0);
        Check("Toggly switched on: done now, no restart", late.Switch("Toggly", true), ModSwitchResult.Now);
        var tog = late.Loaded.SingleOrDefault(m => m.Id == "Toggly");
        int Count(string what) => tog == null ? -1 : (int)Prop(tog.Instance!, what)!;
        Check("... loaded: Load once, OnEnable once", $"{Count("Loads")} {Count("Enables")} {Count("Disables")}", "1 1 0");
        Check("... no game going on the main menu", Prop(tog!.Instance!, "PartySize"), 0);
        Check("... its Harmony patch in effect and listed", $"{Difficulty()} {OwnedPatches("Toggly")} {tog.Patches.Count}", "777 1 1");
        late.MessageLogged("x");
        Check("... its event heard once", Count("Heard"), 1);

        Check("Toggly switched off", late.Switch("Toggly", false), ModSwitchResult.Now);
        Check("... OnDisable once", Count("Disables"), 1);
        Check("... its patch removed", $"{Difficulty() == plain} {OwnedPatches("Toggly")} {tog.Patches.Count}", "True 0 0");
        late.MessageLogged("y");
        Check("... its event not heard", Count("Heard"), 1);

        Check("Toggly switched on again", late.Switch("Toggly", true), ModSwitchResult.Now);
        Check("... no second Load; OnEnable again", $"{Count("Loads")} {Count("Enables")} {late.Loaded.Count(m => m.Id == "Toggly")}", "1 2 1");
        Check("... its patch back, once", $"{Difficulty()} {OwnedPatches("Toggly")}", "777 1");
        late.MessageLogged("z");
        Check("... its event heard once more, not twice", Count("Heard"), 2);
        late.Switch("Toggly", false);
        Check("Toggly off at the end: nothing of it patched", $"{Difficulty() == plain} {OwnedPatches("Toggly")}", "True 0");

        // ---- a mod without the lifecycle ----
        Check("Plain switched on: the next start", late.Switch("Plain", true), ModSwitchResult.NextStart);
        Check("... not loaded, not asked", $"{late.Loaded.Any(m => m.Id == "Plain")} {late.StoreAllowsRowChangeWhileChoosing()}", "False False");
        Check("... settings.ini says on", new ModSettingsStore(ini2).IsEnabled("Plain"), true);
        Check("... status says when", late.StatusOf("Plain", true), "Starts the next time Mordor starts.");
        Check("... off again: done (it was not running)", late.Switch("Plain", false), ModSwitchResult.Now);
        Check("... status: Off.", late.StatusOf("Plain", false), "Off.");
        late.Switch("Plain", true);
        var next = Host(ini2, log, mods);   // the next start
        Check("at the next start it runs", next.StoreAllowsRowChangeWhileChoosing(), true);
        Check("switched off: the next start", next.Switch("Plain", false), ModSwitchResult.NextStart);
        Check("... still running and asked", $"{next.Live.Any(m => m.Id == "Plain")} {next.StoreAllowsRowChangeWhileChoosing()}", "True True");
        Check("... status says when", next.StatusOf("Plain", false), "Running now; off from the next start.");
        Check("... the save tag still has it (it is running)", string.Join(",", next.SaveTag() ?? new List<string>()), "Plain@1");
    }
}

/// <summary>
/// A lifecycle mod for the switching checks: counts its calls, listens to MessageLogged (wired in
/// Load, once) and patches Loot.IdentifyDifficulty in OnEnable, as docs/MODDING.md says to.
/// </summary>
public sealed class TogglyMod : IMod, IModLifecycle
{
    private IModContext? _ctx;
    public int Loads { get; private set; }
    public int Enables { get; private set; }
    public int Disables { get; private set; }
    public int Heard { get; private set; }
    public int PartySize => _ctx?.Game.Party?.Size ?? 0;

    public void Load(IModContext ctx)
    {
        _ctx = ctx;
        Loads++;
        ctx.Events.MessageLogged += _ => Heard++;
    }

    public void OnEnable()
    {
        Enables++;
        _ctx!.Harmony.Patch(typeof(Loot).GetMethod(nameof(Loot.IdentifyDifficulty)),
                            postfix: new HarmonyLib.HarmonyMethod(typeof(TogglyMod).GetMethod(nameof(Post))));
    }

    public void OnDisable() => Disables++;

    public static void Post(ref double __result) => __result = 777;
}

/// <summary>A mod without the lifecycle: switched only at the next start.</summary>
public sealed class PlainMod : IMod
{
    public void Load(IModContext ctx) => ctx.Decisions.StoreAllowsRowChangeWhileChoosing = () => true;
}
