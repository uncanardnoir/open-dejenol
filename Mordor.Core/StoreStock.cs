using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// The General Store's shelves. The original tracks stock per item *and per alignment*, and shows it
/// as four columns: U, G, N, E. Items it stocks permanently (the floor-0 bronze and leather gear) are
/// unaligned and always available at a fixed price; everything else appears only after an adventurer
/// sells it in, and the more of one kind sit on the shelf the cheaper they get.
/// Persisted next to the character saves as store.json.
/// </summary>
public sealed class StoreStock
{
    /// <summary>"itemId:alignment" → how many are on the shelf.</summary>
    public Dictionary<string, int> Shelf { get; set; } = new();

    private static string Key(short id, ItemAlignment a) => $"{id}:{(int)a}";

    public int Count(short id, ItemAlignment a) => Shelf.TryGetValue(Key(id, a), out int n) ? n : 0;

    /// <summary>The four counts the original prints, in U G N E order.</summary>
    public int[] Counts(short id) => ItemAlignments.All.Select(a => Count(id, a)).ToArray();

    public bool AnyInStock(short id) => ItemAlignments.All.Any(a => Count(id, a) > 0);

    public void Add(short id, ItemAlignment a) => Shelf[Key(id, a)] = Count(id, a) + 1;

    /// <summary>Take one off the shelf; false if none were there.</summary>
    public bool Take(short id, ItemAlignment a)
    {
        int n = Count(id, a);
        if (n <= 0) return false;
        if (n == 1) Shelf.Remove(Key(id, a)); else Shelf[Key(id, a)] = n - 1;
        return true;
    }

    /// <summary>
    /// What the store asks for one of these. This is the game's own <c>modItemCost</c>, from
    /// FRMCTYS.BAS in the decompiled source:
    /// <code>
    ///   If gv1426(p0200).M5693 = 0 Then fn272C = gv1426(p0200).M5686 : Exit Function
    ///   l0208 = gv15FA(p0200).M5557(p01FE) - 1
    ///   If l0208 > mc00B6 Then l0208 = mc00B6            ' the shelf caps at 9
    ///   l020E = Log(Abs(l020C - gv1426(l0202).M56DB) + 1) ' l0202 is never assigned, so item 0,
    ///                                                     ' whose charges are 0 - this reduces to
    ///                                                     ' Log(this item's charges + 1)
    ///   fn272C = (((1.5 * l0206) / (Log(l0208 + 1) + 1.15)) / 1) - l020E * (0.05 * l0206)
    /// </code>
    /// M5693 is the item's floor (field 6 of the record), so floor-0 goods sell at the list price,
    /// which is the help's "items that can only be bought in the store ... are displayed with a
    /// fixed price". Verified against the real game running under OTVDM: a Good Iron Sword with one
    /// in stock (price 56,598) is priced 73,823, and this returns 73,823.
    /// </summary>
    public long PriceOf(Item it, ItemAlignment a)
    {
        // FRMCTYS.BAS:594-597 tests the floor alone
        if (it.Floor == 0) return it.Price;
        // Shelf().Align - 1 with no floor: an empty shelf takes Log(0) and the error path leaves the price 0
        // (FRMCTYS.BAS:600-613)
        if (Count(it.Id, a) <= 0) return 0;
        int shelf = Math.Min(Count(it.Id, a) - 1, ShelfCap);
        double cost = (1.5 * it.Price) / (Math.Log(shelf + 1) + 1.15)
                    - Math.Log(it.Charges + 1) * (0.05 * it.Price);
        return (long)Math.Round(cost, MidpointRounding.ToEven);   // modItemCost As Long (FRMCTYS.BAS:587): rounded, no floor
    }

    /// <summary>The shelf holds at most nine of one item per alignment (mc00B6 in FRMCTYS.BAS).</summary>
    public const int ShelfCap = 9;

    /// <summary>
    /// The item types the store buys a charge-chunk at a time rather than whole: potions, scrolls
    /// and the rest. FRMCTYS.BAS switches on these in three separate places.
    /// </summary>
    public static bool SoldByCharge(Item it) =>
        it.Charges > 0 && it.Type is 23 or 24 or 25 or 26 or 27 or 28 or 30 or 31;

    /// <summary>
    /// The original counts identification down - 0 is completely identified and 3 barely - while
    /// <see cref="IdLevel"/> runs the other way so that existing saves keep their meaning. This is
    /// the conversion the formulas need.
    /// </summary>
    public static int IdCountdown(IdLevel known) => 3 - (int)known;

    /// <summary>
    /// What the store pays you, from <c>fn273B</c> in FRMCTYS.BAS:
    /// <code>
    ///   If item.Cursed >= 1 And slot is equipped Then price = 0
    ///   If stock = 0 And any alignment of it is in stock Then stock = 1
    ///   stock capped at 9
    ///   l0238 = 1 : If sold by charge Then l0238 = 5 : charges = min(charges, a full item's)
    ///   l0238 = l0238 * Log(Abs(charges - item.Charges) + 1)
    ///   If completely identified And item is cursed Then 0
    ///   Else l023A = ((.9# * price) / (stock + 1.15#)) / (idLevel + 1)
    ///        fn273B = l023A - (l0238 * (.05# * l023A))
    /// </code>
    /// So a part-used item is docked, a cursed one you have identified is worthless, and the price
    /// climbs as you learn what the thing actually is.
    /// </summary>
    public long SellPrice(Item it, InventorySlot slot)
    {
        double price = it.Cursed >= 1 && slot.Equipped ? 0 : it.Price;

        int stock = Count(it.Id, slot.Alignment);
        if (stock == 0 && AnyInStock(it.Id)) stock = 1;
        stock = Math.Min(stock, ShelfCap);

        int charges = slot.Charges;
        double chargeFactor = 1;
        if (SoldByCharge(it))
        {
            chargeFactor = 5;
            if (slot.Charges > it.Charges) charges = it.Charges;
        }
        chargeFactor *= Math.Log(Math.Abs(charges - it.Charges) + 1);

        int id = IdCountdown(slot.Known);
        if (id == 0 && it.Cursed != 0) return 0;
        // l023A and sellPrice are both Longs (FRMCTYS.BAS:699, 708): each step rounds, and nothing floors it
        long based = (long)Math.Round(((0.9 * price) / (stock + 1.15)) / (id + 1), MidpointRounding.ToEven);
        return (long)Math.Round(based - chargeFactor * (0.05 * based), MidpointRounding.ToEven);
    }

    /// <summary>
    /// What one step of identification costs, <c>fn271B</c> in FRMCTYS.BAS. It divides by how much
    /// is still unknown, so the last step - the one that finishes the job - is the dearest. The
    /// store only offers it while something is still unknown, so the divisor is never zero.
    /// </summary>
    public long IdentifyFee(Item it, InventorySlot slot)
    {
        int stock = Math.Min(Count(it.Id, slot.Alignment), ShelfCap);
        int id = IdCountdown(slot.Known);
        if (id <= 0) return 0;
        return (long)Math.Round((0.23 * it.Price) / ((stock / 2.0 + 1) * 0.9) / id + 1, MidpointRounding.ToEven);   // identifyFee As Long (568)
    }

    /// <summary>
    /// The store's fee to change an item's alignment, <c>cmdRealign_Click</c> in FRMCTYS.BAS:
    /// <code>
    ///   l011C = 1
    ///   If Abs(slot.Align - (Index + 1)) > 1 Then l011C = 2
    ///   l011A = (((.72# * item.Price))) * l011C
    /// </code>
    /// Double to cross the spectrum - Good straight to Evil - and single for a neighbouring step.
    /// The original also refuses outright while the item is equipped.
    /// </summary>
    public static long RealignCost(Item it, ItemAlignment from, ItemAlignment to) =>
        // l011A is a Long: the Double is rounded into it, half to even, not truncated
        (long)Math.Round(0.72 * it.Price * (Math.Abs((int)from - (int)to) > 1 ? 2 : 1), MidpointRounding.ToEven);

    /// <summary>
    /// How many things a character can carry. The original's pack is a fixed array of forty whose
    /// slot zero is reserved, and Buy scans <c>For l006E = 1 To mc0078 - 1</c> for a blank one
    /// (FRMCTYS.BAS:54-60), so thirty-nine is the real answer.
    /// </summary>
    public const int PackSlots = 39;

    /// <summary>
    /// Whether buying one takes it off the shelf - <c>If gv1426(l0072).M5693 &gt; False Or m0028% &gt; 0</c>
    /// (FRMCTYS.BAS:77). A floor-0 item bought unaligned is the store's own bottomless stock and the
    /// count never moves; buy an *aligned* copy of that same item and it does.
    /// </summary>
    public static bool DepletedByBuying(Item it, ItemAlignment bought) =>
        it.Floor > 0 || bought != ItemAlignment.Unaligned;

    /// <summary>
    /// Whether selling one puts it on the shelf. The original answers no for an unaligned floor-0
    /// item, because the store already has all of those it needs
    /// (<c>If item.M5693 = False And slot.M4EBF = 0 Then l0122 = False</c>, FRMCTYS.BAS:264).
    ///
    /// The test is on the **floor alone** - not on the price, and not on the item type. The port
    /// once used a test of its own here, which also demanded a price above zero and excluded guild
    /// crests, and a guild crest is floor 0 with no price: it failed the port's test and so went
    /// onto the shelf for sale. It fails the original's too, but the other way round, so it does
    /// not. (Selling a crest is refused outright anyway - see <see cref="ItemUse.IsGuildIssued"/>.)
    /// </summary>
    public static bool ShelvedBySelling(Item it, ItemAlignment a) =>
        !(it.Floor == 0 && a == ItemAlignment.Unaligned);

    /// <summary>
    /// Whether a sale puts the item on the shelf, cmdSell_Click's test (FRMCTYS.BAS:264-267): not
    /// the Store's own unaligned floor-0 goods (<see cref="ShelvedBySelling"/>), not cursed, and that
    /// alignment's row below the cap. The Store's sell path asks this, and so what a mod is told
    /// (StoreShelved) follows the same rule.
    /// </summary>
    public bool ShelvesSale(Item it, ItemAlignment a) =>
        it.Cursed == 0 && ShelvedBySelling(it, a) && Count(it.Id, a) < ShelfCap;

    public static string PathFor(string savesDir) => Path.Combine(savesDir, "store.json");

    /// <summary>
    /// Load the shelf, or build the one a new game starts with. The original ships a stocked
    /// MDATA7.MDR rather than an empty one, so a fresh store is never bare.
    /// </summary>
    public static StoreStock Load(string savesDir, IEnumerable<Item>? items)
    {
        string p = PathFor(savesDir);
        if (!File.Exists(p) && items != null)
        {
            var fresh = CreateInitial(items);
            fresh.Save(savesDir);
            return fresh;
        }
        return Load(savesDir);
    }

    public static StoreStock Load(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p)) return JsonSerializer.Deserialize<StoreStock>(File.ReadAllText(p)) ?? new StoreStock();
        }
        catch { }
        return new StoreStock();
    }

    public void Save(string savesDir)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            File.WriteAllText(PathFor(savesDir), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>
    /// The shelf a brand-new game starts with, from MAINMOD.BAS. This is why the original's store
    /// already has second-hand goods on it before anyone has sold anything:
    /// <code>
    ///   Case Id 356 To 361                  ' the six artifacts, one copy each
    ///       358 -> Good, 359 -> Evil, the rest -> Neutral
    ///   Case Else
    ///       If Floor > 1 Or Price > 200000 Or Price = 0 Or Cursed Then nothing
    ///       Else If Items.Align > 1 Then 0 / 1 / 1 / 1
    ///            Else If Floor = 0 Then U = 99 Else U = 5
    /// </code>
    /// The floor-0 goods sitting at 99 is how the original represents its permanent stock.
    /// </summary>
    public static StoreStock CreateInitial(IEnumerable<Item> items)
    {
        var s = new StoreStock();
        foreach (var it in items)
        {
            if (it.Id == 0) continue;
            if (it.Id >= 356 && it.Id <= 361)
            {
                var a = it.Id switch { 358 => ItemAlignment.Good, 359 => ItemAlignment.Evil, _ => ItemAlignment.Neutral };
                s.Shelf[Key(it.Id, a)] = 1;
                continue;
            }
            if (it.Floor > 1 || it.Price > 200000 || it.Price == 0 || it.Cursed != 0) continue;
            // "If Items(l01CA).Align > 1" (MAINMOD.BAS:894) - the flag word as a number, not a count of
            // its bits: an item that can only be Good (2), Neutral (4) or Evil (8) is stocked one of each
            // alignment all the same. The port had counted bits.
            if (it.AlignmentFlags > 1)
            {
                s.Shelf[Key(it.Id, ItemAlignment.Good)] = 1;
                s.Shelf[Key(it.Id, ItemAlignment.Neutral)] = 1;
                s.Shelf[Key(it.Id, ItemAlignment.Evil)] = 1;
            }
            else s.Shelf[Key(it.Id, ItemAlignment.Unaligned)] = it.Floor == 0 ? 99 : 5;
        }
        return s;
    }

    /// <summary>
    /// The deepest floor either replenisher will ever draw from. Both read the deepest level
    /// anyone has reached and then cap it the same way - <c>If l02BC >= mc0118 - 1 Then l02BC =
    /// mc0118 - 1</c> in mdrReplenishStore (MAINMOD.BAS:1506) and the identical two lines in
    /// mdrReplenishCStore (MAINMOD.BAS:1308) - and <c>mc0118</c> is 15.
    ///
    /// The same file uses that constant three other times, each as a loop bound running 1 to 15
    /// (MAINMOD.BAS:513, 825, 832), so 15 is the number of floors rather than the last index and
    /// the two caps are one short. Reproduced in both places.
    /// </summary>
    public const int DeepestStocked = 14;

    /// <summary>
    /// Other adventurers trading with the store while you were away - <c>mdrReplenishStore</c>
    /// (<c>sub071B</c>, MAINMOD.BAS:1484-1683), called from mdrcharAddAge with the days you just
    /// aged. Each pass has a <c>Clamp(days, 1, 50)</c> percent chance of running again, and each
    /// pass is 60% someone selling something in and **40% someone buying something out**. The port
    /// only ever added, so a shelf could fill and never empty.
    ///
    /// The draw has three stages, and the middle one matters more than it looks:
    ///
    /// 1. a floor, biased shallow - roll 1..100 and walk upwards while
    ///    <c>roll &lt; 100 - (floor / (deepest + 1)) * 100</c>
    /// 2. a random item **type** (Dagger, Sword, Potion...), and then only the contiguous run of
    ///    items of that type at or above the floor. Picking the type first gives every kind of item
    ///    the same chance, so a type with three members is as likely as one with eighty - pooling
    ///    all the items together instead, as the port did, is a different distribution entirely
    /// 3. one item out of that run, weighted by its Rarity
    ///
    /// Cursed items and the floor-0 permanent stock are never touched either way.
    ///
    /// <paramref name="deepestReached"/> is the original's <c>l02BC</c>, read from the dungeon file
    /// and then capped at 14. The cap is one short of the fifteen floors the game has, so nothing
    /// that only appears on floor 15 is ever stocked by anyone but a player selling it in. That
    /// looks like an off-by-one - <c>mc0118</c> is 15 and the code writes <c>mc0118 - 1</c> - and it
    /// is reproduced.
    ///
    /// The routine is ported line for line, draw for draw (MAINMOD.BAS:1484-1683), and what that
    /// keeps is worth listing, because each of these was once smoothed over:
    /// <list type="bullet">
    /// <item><b>A full shelf wastes the pass.</b> Selling in, the item is drawn, its alignment rolled,
    /// and then <c>If Shelf(l02C4).Align(l02C2) &lt; mc0298</c> (1582) adds one only below nine. Either
    /// way <c>l02C4</c> is the item, so <c>Loop While l02C4 = False</c> (1593) is done: nothing else is
    /// drawn in its place. The port drew again, up to 100 times.</item>
    /// <item><b>Rarity weights are whole percents.</b> <c>l02C8()</c> is an Integer array (1501), so
    /// each <c>(Rarity / total) * 100</c> rounds half to even on the way in (1558-1564): an item under
    /// half a percent of its run weighs nothing, and the weights can sum short of the
    /// <c>Rnd * 100</c> they are tested against, when nothing is picked and the loop goes round.</item>
    /// <item><b>The run's end.</b> The run starts at the first item of the kind within the floor - an
    /// index of 0 counts as none found (1544) - and ends one before the first deeper item or other
    /// kind. A run that reaches the end of the file finds no end, and <c>l02B6</c> keeps whatever
    /// it last held, in this pass or an earlier one, before <c>If l02B6 &lt; l02B4</c> (1552) clamps
    /// it.</item>
    /// <item><b>The caps.</b> The passes and the selling-in loop have none; only the buying-out loop
    /// gives up, after 100 draws (1671).</item>
    /// <item>The alignment of an item sold in is <c>Int(Rnd * 4)</c> drawn until its Align bit is set
    /// (1578-1580), one draw per try.</item>
    /// </list>
    /// Not ported here: each item sold in is also filed in the Library, <c>sub0730 m0070, -l02C4,
    /// m029E, -2, True</c> (1586). The Library is being reworked separately; see docs/FIDELITY.md.
    /// </summary>
    /// <param name="shelved">Told of each item a pass actually puts on a shelf - the one place the
    /// original calls sub0730 to file it in the Library (MAINMOD.BAS:1582-1586,
    /// <see cref="Librarian.FileRestock"/>). It draws nothing.</param>
    public void Replenish(IReadOnlyList<Item> items, int days, Random rng, int deepestReached = DeepestStocked,
                          Action<Item>? shelved = null)
    {
        // `Get #gv1B58, 2, l02BC` and the cap at 14 (1505-1508). The map file starts that record at
        // 1 (MAINMOD.BAS:510-512), so it is never below 1; the port's DungeonState starts at 0
        int deepest = Math.Clamp(deepestReached, 1, DeepestStocked);
        int n = items.Count;
        int kinds = Mdata1.ItemTypes.Count > 0 ? Mdata1.ItemTypes.Count : n == 0 ? 0 : items.Max(i => (int)i.Type) + 1;
        if (kinds == 0) return;

        // `l02BE = (p02A6 / 100) * 100`, a Single, clamped 1..50 (1510-1512)
        float chance = Math.Clamp(days / 100f * 100f, 1f, 50f);
        int last = 0;                                   // l02B6: Dim'd once, so it outlives a pass

        while (true)                                                                              // 1509
        {
            if (rng.NextDouble() * 100 > chance) break;                                           // 1513-1515
            bool adding = rng.NextDouble() * 100 < 60;                                            // 1517-1521

            // a floor, biased towards the shallow end (1522-1531)
            int roll = (int)Math.Floor(rng.NextDouble() * 100) + 1, floor = 0;
            for (int f = 1; f <= deepest; f++)
            {
                if (roll < 100 - (f / (double)(deepest + 1)) * 100) floor = f; else break;
            }
            floor = Math.Clamp(floor, 1, deepest);

            // Selling in loops until something is drawn (1593). Only a table with nothing at all in
            // reach of the floor could keep it there for ever - the original would hang; the shipped
            // data always has something, and the selfcheck says so
            if (adding && !Reachable(items, kinds, floor)) continue;

            int tries = 0;                                                                        // 1595
            while (true)
            {
                tries++;                                                                          // 1597
                int chosen = DrawOne(items, kinds, floor, rng, ref last);
                if (chosen > 0)
                {
                    var it = items[chosen];
                    if (it.Cursed == 0 && it.Floor > 0)                                           // 1574, 1638
                    {
                        // sub0730 files what was shelved, inside the If that adds it (1582-1586)
                        if (adding) { if (AddOne(it, rng)) shelved?.Invoke(it); }
                        else if (!RemoveOne(it, rng)) chosen = 0;
                    }
                    else chosen = 0;
                }
                if (adding ? chosen != 0 : tries >= 100 || chosen != 0) break;                    // 1593, 1671
            }
        }
    }

    /// <summary>
    /// The draw both halves share (1535-1572 and 1598-1636): a kind <c>(Rnd * NumItemTypes)</c>
    /// rounded into an Integer, the first item of it within the floor, the run from there, the
    /// whole-percent weights and the <c>Rnd * 100</c> against them. Returns the table index picked,
    /// or 0 for none - index 0 is the null record, and the original takes 0 as "not found" both
    /// for the run's start and for the pick.
    /// </summary>
    private static int DrawOne(IReadOnlyList<Item> items, int kinds, int floor, Random rng, ref int last)
    {
        // MAINMOD.BAS:1535 and 1598 are `l02B8 = (Rnd * NumItemTypes)` with `l02B8 As Integer`, so the draw
        // rounds rather than truncating. Two things follow and both are the original's:
        //
        //   Type 0 comes up half as often as the rest, because only Rnd * N below 0.5 reaches it
        //   while every other type owns a full unit of the range.
        //   The draw can land on N itself, one past the last type, which matches no item at all -
        //   the original's search then finds nothing and its Do loop goes round again.
        int kind = Rules.RoundedIndex(rng, kinds);
        int n = items.Count;
        int first = 0;                                                                            // 1537-1543
        for (int i = 0; i < n; i++)
            if (items[i].Type == kind && items[i].Floor <= floor) { first = i; break; }
        if (first == 0) return 0;                                                                 // 1544

        for (int i = first; i < n; i++)                                                           // 1545-1550: no
            if (items[i].Floor > floor || items[i].Type != kind) { last = i - 1; break; }         // end, l02B6 stale
        if (last < first) last = first;                                                           // 1551-1553

        float total = 0;                                                                          // 1554-1557, Single
        for (int i = first; i <= last; i++) total += items[i].Rarity;

        float pick = (float)(rng.NextDouble() * 100), acc = 0;                                    // 1565-1566
        for (int i = first; i <= last; i++)
        {
            acc += Weight(items[i].Rarity, total);                                                // 1558-1564, 1569
            if (pick <= acc) return i;                                                            // 1570-1573
        }
        return 0;
    }

    /// <summary>
    /// One item's share of its run, as <c>l02C8()</c> holds it: <c>(Rarity / total) * 100</c> in
    /// Single, stored in an Integer, so rounded half to even (MAINMOD.BAS:1501, 1558-1564).
    /// </summary>
    public static int Weight(int rarity, float total) =>
        rarity > 0 ? (int)Math.Round(rarity / total * 100f, MidpointRounding.ToEven) : 0;

    /// <summary>
    /// Whether the selling-in loop can land at this floor, read the way <see cref="DrawOne"/> draws:
    /// for some kind the draw can name, the run from its first item within the floor (an index of 0
    /// is not found) holds an item with a whole-percent weight above nought, before the weights
    /// already reach 100, that is neither cursed nor permanent stock and has an alignment to take.
    /// A run that reaches the end of the file takes its end from a stale variable, so only its first
    /// item is counted as certain. Only for the guard in <see cref="Replenish"/>.
    /// </summary>
    private static bool Reachable(IReadOnlyList<Item> items, int kinds, int floor)
    {
        int n = items.Count;
        for (int k = 0; k < kinds; k++)
        {
            int first = 0;
            for (int i = 0; i < n; i++) if (items[i].Type == k && items[i].Floor <= floor) { first = i; break; }
            if (first == 0) continue;
            int last = first;
            for (int i = first; i < n; i++) if (items[i].Floor > floor || items[i].Type != k) { last = Math.Max(first, i - 1); break; }
            float total = 0;
            for (int i = first; i <= last; i++) total += items[i].Rarity;
            int acc = 0;
            for (int i = first; i <= last && acc < 100; i++)
            {
                int w = Weight(items[i].Rarity, total);
                var it = items[i];
                if (w > 0 && it.Cursed == 0 && it.Floor > 0 && (it.AlignmentFlags <= 1 || (it.AlignmentFlags & 0xF) != 0)) return true;
                acc += w;
            }
        }
        return false;
    }

    /// <summary>
    /// Somebody sold one in. MAINMOD.BAS:1575-1587: the alignment is nought unless the item's Align
    /// is above 1, when <c>Int(Rnd * (mc02A0 + 1))</c> is drawn until <c>testBit</c> says the item
    /// comes in it; then one is added if fewer than nine are there. A full shelf adds nothing, and
    /// the pass is still spent.
    /// </summary>
    private bool AddOne(Item it, Random rng)
    {
        var a = ItemAlignment.Unaligned;
        if (it.AlignmentFlags > 1)                                                                // 1577
        {
            // an Align with none of bits 0-3 set would spin here for ever in the original; no
            // shipped item has one (selfcheck)
            if ((it.AlignmentFlags & 0xF) == 0) return false;
            do a = (ItemAlignment)(int)Math.Floor(rng.NextDouble() * 4);                          // 1578-1580
            while ((it.AlignmentFlags & (1 << (int)a)) == 0);
        }
        if (Count(it.Id, a) >= ShelfCap) return false;                                            // 1582
        Add(it.Id, a);                                                                            // 1583-1585
        return true;
    }

    /// <summary>
    /// Somebody bought one. MAINMOD.BAS:1637-1662: it means to look for any alignment with stock,
    /// then pick a random alignment that has some and take one away.
    ///
    /// The look is broken, and kept broken: the loop at 1641-1646 runs its counter l02AC four
    /// times but reads <c>Shelf(l02C4).Align(l02C2)</c>, and l02C2 was set to 0 just before
    /// (1639). Shelf alignment 0 is Unaligned - the index is the one a bought item takes as its
    /// <c>Pack.Alignment</c> (FRMCTYS.BAS:73), and 0 there is no alignment. So an item with no
    /// unaligned copy on the shelf is never sold off; one with any is, and then from whichever
    /// stocked alignment the retry loop at 1648-1650 lands on, one <c>Int(Rnd * 4)</c> per try.
    /// </summary>
    private bool RemoveOne(Item it, Random rng)
    {
        if (Count(it.Id, ItemAlignment.Unaligned) <= 0) return false;
        ItemAlignment a;
        do a = (ItemAlignment)(int)Math.Floor(rng.NextDouble() * 4);                              // 1648-1650
        while (Count(it.Id, a) <= 0);
        Take(it.Id, a);
        return true;
    }
}
