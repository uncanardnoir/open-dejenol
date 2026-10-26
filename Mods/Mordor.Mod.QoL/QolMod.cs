using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.QoL;

/// <summary>
/// Quality of Life: the conveniences the user asked for, most a toggle of their own (mod.json
/// declares them, all on by default once the mod is enabled; the Sort buttons are always there and
/// the Store's hiding is set by its own box). Every hook reads its toggle as it
/// runs, so ticking one on the Mods page takes effect at once.
///
/// The controls (the bars) are in <see cref="Views"/> and are reached only from
/// inside the hooks, so loading the mod needs no Avalonia: the game has it, the headless probe
/// does not.
/// </summary>
public sealed class QolMod : IMod, IModLifecycle
{
    /// <summary>
    /// Switched on again on the main menu (API version 7): nothing to put back - every hook is wired once in
    /// <see cref="Load"/> and the host calls them only while the mod is on. No Harmony patches.
    /// </summary>
    public void OnEnable() { }

    /// <summary>
    /// Switched off on the main menu: the host stops calling the hooks and takes the columns, buttons,
    /// marks and colours away.
    /// </summary>
    public void OnDisable() { }

    /// <summary>
    /// Town gold (1.13.0), one choice: <see cref="TownGoldOff"/> (the original's: nothing
    /// automatic), <see cref="OwnBank"/> (each member's gold kept in their own account, the others'
    /// accounts covering a shortfall in party order) or <see cref="LeaderBank"/> (the party's gold
    /// kept in the leader's account, which covers a shortfall after the payer's own). The dungeon's
    /// gold is always the game's even share.
    /// </summary>
    public const string TownGold = "TownGold";
    public const string TownGoldOff = "Off";
    public const string OwnBank = "Everyone's own bank";
    public const string LeaderBank = "Leader's bank";

    /// <summary>
    /// The town-gold keys of 1.10.0 to 1.12.0, read once by <see cref="MigrateTownGold"/> and taken
    /// out: Party gold (the choice, <c>All to the leader</c> the dungeon's whole pot to the leader),
    /// the three Bank-managed town gold toggles and their merged key of 1.10.0 (DepositOnArrival
    /// never shipped; it is read and taken out the same, in case).
    /// </summary>
    public const string OldPartyGold = "PartyGold";
    public const string OldAllToLeader = "All to the leader";
    public static readonly string[] OldBankFeatures = { "AutoDepositInTown", "SaleGoldToBank", "PartyCoversShortfall", "DepositOnArrival" };
    public const string OldBankMerged = "BankManagedTownGold";

    // Enhanced character UI: one toggle 1.8.0 to 1.10.0, five since 1.11.0
    public const string XpToNextColumn = "XpToNextColumn";
    public const string HpSpBars = "HpSpBars";
    /// <summary>The experience bar, drawn behind the Next column's figure (so shown with that column only).</summary>
    public const string XpBar = "XpBar";
    /// <summary>The quest mark, drawn before the Next column's figure (so shown with that column only).</summary>
    public const string MarkActiveQuests = "MarkActiveQuests";
    public const string MarkUnidentified = "MarkUnidentified";

    // Enhanced store UI: one toggle 1.9.0 and 1.10.0, three since 1.11.0
    public const string StoreSlotColumns = "StoreSlotColumns";
    public const string StoreSortable = "StoreSortable";
    public const string SayStoreShelved = "SayStoreShelved";

    /// <summary>The Messages window's combat lines coloured by kind (1.9.0, in place of the Extended combat log).</summary>
    public const string ColorCodedCombatMessages = "ColorCodedCombatMessages";

    public const string HideUnusableInStore = "HideUnusableInStore";
    /// <summary>The Store's sort now (a text setting, not a toggle): see <see cref="StoreSort"/>.</summary>
    public const string StoreSortOrder = "StoreSortOrder";

    /// <summary>
    /// The Store's own box for <see cref="HideUnusableInStore"/>: since 1.8.0 the only place it is set
    /// (no Mods-page toggle); its state is kept under that key as before, off until ticked.
    /// </summary>
    public const string HideUnusableLabel = "Hide unusable";

    /// <summary>
    /// The merged toggles of 1.8.0 to 1.10.0, each with the features it stood for: since 1.11.0 each
    /// feature has its own key again (the old ones), and the settings window's group box switches
    /// them together.
    /// </summary>
    public static readonly (string Merged, string[] Features)[] Merged =
    {
        ("EnhancedCharacterUI", new[] { XpToNextColumn, HpSpBars, XpBar, MarkActiveQuests, MarkUnidentified }),
        ("EnhancedStoreUI", new[] { StoreSlotColumns, StoreSortable, SayStoreShelved }),
    };

    // General (1.12.0): toggles again, under their old keys. 1.9.0 to 1.11.0 had them always on and
    // took the keys out, so a key absent reads as its default, on; nothing takes them out now.
    /// <summary>Another Store row can be picked while Select Alignment is up (commit 164befa's store).</summary>
    public const string UnlockedStoreAlignment = "UnlockedStoreAlignment";
    /// <summary>The toolbar buttons' tooltips: the help's name and the key.</summary>
    public const string ToolbarTooltips = "ToolbarTooltips";

    /// <summary>
    /// Keys taken out and not carried anywhere: the Sort toggles (1.8.0: the buttons are always
    /// there), Alt+P pools and deposits everything (1.10.0: ALT-p is the game's own again).
    /// (UnlockedStoreAlignment and ToolbarTooltips were taken out by 1.9.0 to 1.11.0; since 1.12.0
    /// they are toggles again and kept.)
    /// </summary>
    public static readonly string[] Dropped = { "ItemSort", "BankSort", "AltPPoolAndDeposit" };

    /// <summary>
    /// The Extended combat log, gone in 1.9.0: its toggle (what <see cref="ColorCodedCombatMessages"/>
    /// starts from) and its window's place and open state.
    /// </summary>
    public static readonly string[] CombatLogKeys = { "ExtendedCombatLog", "Window.CombatLog.Place", "Window.CombatLog.Open" };

    /// <summary>
    /// settings.ini from any earlier version brought up to 1.13.0, once. The town-gold keys become
    /// the Town gold choice (<see cref="MigrateTownGold"/>). A merged toggle of 1.8.0 to 1.10.0
    /// (Enhanced character UI, Enhanced store UI) gives its value to each of its features not set on
    /// its own - so it on means all of them on, off all of them off - and is taken out; a feature
    /// set on its own (a settings.ini from before the merge kept the old keys) keeps its value; one
    /// never set keeps its default, on. Color-coded combat messages takes the Extended combat log's
    /// value unless set already. Then the dropped toggles and the Combat Log window's keys are taken
    /// out. Nothing to do in a settings.ini that never had them. General's two keys are left alone:
    /// absent (1.9.0 to 1.11.0 took them out) they read as their default, on.
    /// </summary>
    public static void Migrate(ModSettings s)
    {
        MigrateTownGold(s);
        foreach (var (merged, features) in Merged)
        {
            if (!s.Has(merged)) continue;
            bool on = string.Equals(s.GetText(merged).Trim(), "True", StringComparison.OrdinalIgnoreCase);
            foreach (var f in features)
                if (!s.Has(f)) s.Set(f, on);
            s.Remove(merged);
        }
        if (s.Has("ExtendedCombatLog") && !s.Has(ColorCodedCombatMessages))
            s.Set(ColorCodedCombatMessages, string.Equals(s.GetText("ExtendedCombatLog").Trim(), "True", StringComparison.OrdinalIgnoreCase));
        foreach (var k in Dropped.Concat(CombatLogKeys)) s.Remove(k);
    }

    /// <summary>
    /// 1.10.0 to 1.12.0's town gold as the one Town gold choice, unless it is set already: Party gold
    /// at All to the leader is <see cref="LeaderBank"/>; otherwise any Bank-managed town gold feature
    /// on, or never set (each read on its own key, else from the merged key of 1.10.0, else its
    /// default, on), is <see cref="OwnBank"/>; all of them off is <see cref="TownGoldOff"/>. Then every
    /// old key is taken out. A settings.ini without any of them is left alone (the choice's default,
    /// Everyone's own bank, is what the old defaults made).
    /// </summary>
    public static void MigrateTownGold(ModSettings s)
    {
        var old = OldBankFeatures.Append(OldBankMerged).Append(OldPartyGold).ToArray();
        if (!old.Any(s.Has)) return;
        if (!s.Has(TownGold))
        {
            static bool On(string text) => string.Equals(text.Trim(), "True", StringComparison.OrdinalIgnoreCase);
            bool? merged = s.Has(OldBankMerged) ? On(s.GetText(OldBankMerged)) : null;
            // DepositOnArrival never shipped: read only when set, never as an unset feature
            bool anyOn = OldBankFeatures.Any(f => s.Has(f) ? On(s.GetText(f)) : f != "DepositOnArrival" && (merged ?? true));
            string pick = string.Equals(s.GetText(OldPartyGold).Trim(), OldAllToLeader, StringComparison.OrdinalIgnoreCase) ? LeaderBank
                : anyOn ? OwnBank : TownGoldOff;
            s.SetChoice(TownGold, pick);
        }
        foreach (var k in old) s.Remove(k);
    }

    /// <summary>
    /// The leader whose account holds the party's gold in <see cref="LeaderBank"/>: the first member,
    /// or, when the first is dead (or not in the city), the next living member in the city in party
    /// order; null when there is none. Asked as each move is made, so a leader raised again is the
    /// leader again (what an earlier stand-in was given stays in that member's own account).
    /// </summary>
    public static Character? TownLeader(IReadOnlyList<Character> members) =>
        members.FirstOrDefault(m => !m.IsDead && m.DungeonLevel == 0);

    /// <summary>
    /// The colour of a Messages line of each kind with Color-coded combat messages on, or null for
    /// the window's own: the old Combat Log's (1.0.0 to 1.8.0) - the party's side light green, the
    /// foes' indian red, chests orange, joining plum.
    /// </summary>
    public static (byte R, byte G, byte B)? CombatColour(MessageKind kind) => kind switch
    {
        MessageKind.Party => (144, 238, 144),
        MessageKind.Foes => (205, 92, 92),
        MessageKind.Chest => (255, 165, 0),
        MessageKind.Join => (221, 160, 221),
        _ => null,
    };

    /// <summary>
    /// Where the handlers of the mod's own controls report a failure they caught: the mod's log and
    /// crash.log. The host does not guard those handlers.
    /// </summary>
    internal static Action<string, Exception>? Report { get; private set; }

    public void Load(IModContext ctx)
    {
        var s = ctx.Settings;
        Report = (what, ex) => { try { ctx.Log.Error(what, ex); } catch { } };
        Migrate(s);

        // a. while Select Alignment is up, another row withdraws the choice and is taken (commit 164befa's store);
        // a toggle again since 1.12.0 (always on 1.9.0 to 1.11.0)
        ctx.Decisions.StoreAllowsRowChangeWhileChoosing = () => s.Get(UnlockedStoreAlignment);

        // c. "AutoMap (F8)": the help's name for the button and the key it says does the same; a
        // toggle again since 1.12.0 (always on 1.9.0 to 1.11.0)
        ctx.UI.ToolbarTooltip = b => s.Get(ToolbarTooltips) ? Tooltip(b) : null;

        // d. Sort, on the Items page of Objects & Spells: always there with the mod on
        ctx.UI.AddObjectsCommand(new ObjectsCommand("Sort", PackSort.Run, () => true));

        // Enhanced character UI, in the Party Information window: f. the HP and SP bars, and e. the
        // Next column, with g. the experience bar behind its text and q. a guild quest's mark before
        // the figure (the tables name the quest's monster or item)
        ctx.UI.AddPartyColumn(new PartyColumn("HP/SP", c => Draw.HpSp(c), () => s.Get(HpSpBars), width: 40));
        ctx.UI.AddPartyColumn(PartyColumn.WithTables("Next",
            q => Draw.Next(q.Character, XpColumn.Cell(q.Character), s.Get(MarkActiveQuests) ? QuestMarkFor(q) : null, s.Get(XpBar)),
            () => s.Get(XpToNextColumn), width: NextWidth));

        // h. the Messages window's combat lines coloured by the kind the game gave them (the text untouched)
        ctx.UI.MessageLineColour = l => s.Get(ColorCodedCombatMessages) ? CombatColour(l.Kind) : null;

        // i. Town gold (the choice, read as each question comes). Walking back into town: the Bank's
        // Party Deposit for everybody (Everyone's own bank) or its Party Pool into the leader's
        // account (Leader's bank), and a line saying how much; ...
        ctx.Decisions.TownArrival = a => a.How != TownArrivalKind.Walked ? null : s.GetChoice(TownGold) switch
        {
            OwnBank => new TownArrivalAnswer(TownArrivalAction.PartyDeposit, DepositedLine),
            LeaderBank when (TownLeader(a.Party.Members) ?? a.Selected) is { } leader
                => new TownArrivalAnswer(TownArrivalAction.PartyPool, t => PooledLine(leader, t), leader),
            _ => null,
        };
        // ... gold from a sale (the Store, the Confinement) into the seller's account, or the
        // leader's, not the hand
        ctx.Decisions.TownIncomeBank = q => s.GetChoice(TownGold) switch
        {
            OwnBank => q.Character,
            LeaderBank => TownLeader(q.Party.Members) ?? q.Character,
            _ => null,
        };

        // j. the Store's Buy list without what the character in command cannot use (off by default)
        ctx.Decisions.StoreRowVisible = q => s.Get(HideUnusableInStore) && !q.Usable ? false : null;
        // ... set from inside the Store only: a box on the Buy Items line (the list is redrawn)
        ctx.UI.AddStoreOption(new StoreOption(HideUnusableLabel,
            () => s.Get(HideUnusableInStore), v => s.Set(HideUnusableInStore, v),
            tooltip: "Leave out of the Buy list what the character in command cannot use"));

        // k. with Enhanced character UI on, a mark after an item not fully identified, on the Items page
        // ... and, with Enhanced store UI on (n.), the slot label after it
        ctx.UI.PackRowDecoration = r => PackDecoration(r.Item, r.Slot.Known, s.Get(MarkUnidentified), s.Get(StoreSlotColumns));
        // ... and the same mark after a line of the Bank's list (no slot label: the list is narrow)
        ctx.UI.BankRowDecoration = r => PackDecoration(r.Item, r.Slot.Known, s.Get(MarkUnidentified), slot: false);

        // p. Sort, in the Bank: the vault in the pack sort's order; always there with the mod on
        ctx.UI.AddBankCommand(new BankCommand("Sort", BankSort.Run, () => true));

        // n. the Store's Buy list: what each item is worn as, and what the one in command wears there now
        ctx.UI.AddStoreColumn(new StoreColumn("Slot", q => WearSlot.SlotCell(q.Item), () => s.Get(StoreSlotColumns), width: 90));
        ctx.UI.AddStoreColumn(new StoreColumn("Worn", q => WearSlot.WornCell(q.Character, q.Item, q.Lookup), () => s.Get(StoreSlotColumns), width: 150));

        // o. the Store's Buy list sorted by a click on Item, or on Slot while that column is shown;
        // the sort is kept in settings.ini, so the Store opens sorted as it was left
        ctx.UI.StoreHeaderSort = h => s.Get(StoreSortable) && (h == StoreSort.ItemHeader || (h == StoreSort.SlotHeader && s.Get(StoreSlotColumns)))
            ? (StoreSortArrow?)StoreSort.Arrow(s.GetText(StoreSortOrder), h)
            : null;
        ctx.UI.StoreHeaderClicked = h => s.SetText(StoreSortOrder, StoreSort.Next(s.GetText(StoreSortOrder), h));
        ctx.Decisions.StoreRowOrder = q => s.Get(StoreSortable) ? StoreSort.Order(q.Rows, s.GetText(StoreSortOrder), s.Get(StoreSlotColumns)) : null;

        // l. with Enhanced store UI on, a sale that puts the item on the shelf says so
        ctx.Events.StoreShelved += sh => { if (s.Get(SayStoreShelved)) sh.Say(ShelvedLine(sh.Item.Name, sh.Alignment)); };

        // m. Town gold again: a town service the one in command cannot afford from their hand and
        // their own bank (the game's charPay, untouched): the others' accounts in party order
        // (Everyone's own bank) or the leader's account (Leader's bank) make up exactly the
        // shortfall (Party.CoverShortfall), one line per giver, and the charge goes through
        ctx.Decisions.CoverShortfall = q => s.GetChoice(TownGold) switch
        {
            OwnBank => new ShortfallAnswer(Gave, q.Party.Members.Where(m => m != q.Payer).ToList()),
            LeaderBank when TownLeader(q.Party.Members) is { } leader && leader != q.Payer => new ShortfallAnswer(Gave, new[] { leader }),
            _ => null,
        };

        ctx.Log.Info("ready");
    }

    /// <summary>
    /// "The store is now stocking a Long Sword (g)." The item's own name; "an" before a vowel
    /// (a, e, i, o, u), "a" otherwise; the alignment tag (gv077E$, as itemNameFor adds it) only for
    /// an aligned item.
    /// </summary>
    public static string ShelvedLine(string name, ItemAlignment alignment)
    {
        string n = name.Trim();
        string article = n.Length > 0 && "aeiouAEIOU".IndexOf(n[0]) >= 0 ? "an" : "a";
        string tag = alignment > ItemAlignment.Unaligned ? " " + ItemInfoPage.AlignmentTags[Math.Clamp((int)alignment, 0, 3)] : "";
        return $"The store is now stocking {article} {n}{tag}.";
    }

    /// <summary>
    /// The mark after a pack line whose item is not fully known: a "?" tinted by how much is known,
    /// with the level named in its tooltip. Nothing at All. It tells no more than the item's own
    /// Info does (which shows how well it is identified); the name drawn is the game's, untouched.
    /// </summary>
    public static PackRowDecoration? PackDecoration(Item item, IdLevel known, bool mark, bool slot)
    {
        var m = mark ? IdMark(known) : null;
        var l = slot ? WearSlot.PackLabel(item, known) : null;
        return m == null ? l : l == null ? m : m with { Then = l };
    }

    public static PackRowDecoration? IdMark(IdLevel known) => known switch
    {
        IdLevel.Barely => new PackRowDecoration(" ?", (230, 90, 80), "Barely identified"),
        IdLevel.Partial => new PackRowDecoration(" ?", (235, 160, 50), "Partly identified"),
        IdLevel.Mostly => new PackRowDecoration(" ?", (225, 215, 100), "Mostly identified"),
        _ => null,
    };

    /// <summary>
    /// One giver's part of a covered shortfall: <c>"fifi" gives "uncanard" 350 Gold.</c> The mod's
    /// own wording; the names quoted as the game's own pool and give lines quote them.
    /// </summary>
    public static string GaveLine(Character from, Character to, VbCurrency amount) =>
        $"\"{from.Name.TrimEnd()}\" gives \"{to.Name.TrimEnd()}\" {CharStatFile.FormatNumber(amount)} Gold.";

    /// <summary>One giver's line for a covered shortfall (<see cref="GaveLine"/>).</summary>
    private static string? Gave(Party.ShortfallGift g, Character to) => GaveLine(g.Giver, to, g.Amount);

    /// <summary>The arrival deposit's line (Everyone's own bank): <c>Deposited the party's gold: 3,512</c>.</summary>
    public static string DepositedLine(VbCurrency total) => $"Deposited the party's gold: {CharStatFile.FormatNumber(total)}";

    /// <summary>
    /// The arrival pool's line (Leader's bank): the deposit's, naming whose account, the name quoted
    /// as the game's pool lines quote it: <c>Deposited the party's gold with "Lead": 3,512</c>.
    /// </summary>
    public static string PooledLine(Character leader, VbCurrency total) =>
        $"Deposited the party's gold with \"{leader.Name.TrimEnd()}\": {CharStatFile.FormatNumber(total)}";

    /// <summary>
    /// The Next column's width: 44 until 1.7.0, 6 more for the quest mark before the figure, so
    /// "!Pinned" in bold and "✓100K" keep clear of the column before.
    /// </summary>
    public const double NextWidth = 50;

    /// <summary>A member's quest mark (Enhanced character UI); null with no quest.</summary>
    public static Mark? QuestMarkFor(PartyCellQuery q) => QuestMark.Of(q.Character, q.Monster, q.Item);

    /// <summary>
    /// The Next column's cell: the figure (XpColumn.Cell) over the experience bar, with the quest mark
    /// before it when there is one.
    /// </summary>
    public static object NextCell(PartyCellQuery q) => Draw.Next(q.Character, XpColumn.Cell(q.Character), QuestMarkFor(q), bar: true);

    public static string Tooltip(ToolbarButton b) => b.Key == null ? b.HelpName : $"{b.HelpName} ({b.Key})";

    /// <summary>
    /// The way to <see cref="Views"/>, typed as object and never inlined: a method that names an
    /// Avalonia type is resolved when the method calling it is compiled, so the hooks go through
    /// these and a hook that returns text never needs Avalonia (the headless probe has none).
    /// </summary>
    private static class Draw
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object HpSp(Character c) => Views.HpSpCell(c);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object Next(Character c, string text, Mark? mark, bool bar) => Views.NextCell(c, text, mark, bar);
    }
}
