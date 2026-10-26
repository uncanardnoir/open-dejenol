using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// Holding and Confinement, <c>FRMCITYC.BAS</c>. The help: "On the right is the list of creatures
/// that are currently available ... To select a future companion, simply click on it ... To buy
/// them, click BUY." It also buys them back - "Selling a companion to the store is the only way to
/// get rid of hostile (or bound) companions without taking the chance of them turning on you and
/// attacking" - identifies them, and realigns them, on top of the Bind service the port already had.
///
/// Like the General Store it keeps shelves: how many of each creature, of each alignment, are
/// penned up at the moment. Selling one adds it to the pen and buying one takes it away, and the
/// price moves with what is in stock.
/// </summary>
public sealed class Confinement
{
    /// <summary>mc00EC: the shelves never hold more than this of one creature and alignment.</summary>
    public const int ShelfCap = 9;

    /// <summary>
    /// "tableIndex:alignment" → how many are penned. The original's pens are <c>gv162E(Ref)</c>,
    /// one record per monster table index with <c>Align(0 To 3)</c> counts (FRMCITYC.BAS:288-291,
    /// MAINMOD.BAS:542-557), so two creatures that share an Id - the Morgul and the Shadow Ninja,
    /// both 307 - have pens of their own. The alignment is the item scheme, 1 G 2 N 3 E, the
    /// original's Align index plus one.
    /// </summary>
    public Dictionary<string, int> Pens { get; set; } = new();

    /// <summary>
    /// What <see cref="Pens"/> is keyed by: <see cref="ByRef"/>, the table index, for every file
    /// this port writes now. A file without it was written while the pens were keyed by monster
    /// Id, and is moved across on load (<see cref="FromIdKeys"/>).
    /// </summary>
    public string? KeyedBy { get; set; }

    /// <summary>The <see cref="KeyedBy"/> of a file keyed as the original's pens are.</summary>
    public const string ByRef = "Ref";

    private static string PathFor(string savesDir) => Path.Combine(savesDir, "confinement.json");
    private static string Key(int tableIndex, ItemAlignment align) => $"{tableIndex}:{(int)align}";

    /// <summary>
    /// The pen's table index: the creature's own Ref (<see cref="Monster.Ref"/>), or null for a
    /// creature built by hand, which has no pen - counted as none, and nothing taken or added.
    /// </summary>
    private static int? RefOf(Monster def) => def.Ref >= 0 ? def.Ref : null;

    /// <summary>
    /// Read from a file still keyed by monster Id and not yet moved across (<see cref="FromIdKeys"/>):
    /// such a store must not be written back stamped as keyed by Ref.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    private bool _idKeyed;

    /// <summary>Every pen as (table index, alignment, count), for the probes.</summary>
    public IEnumerable<(int Ref, ItemAlignment Align, int Count)> Entries =>
        Pens.Select(kv => (Parse(kv.Key), kv.Value))
            .Where(p => p.Item1 is not null)
            .Select(p => (p.Item1!.Value.Ref, p.Item1!.Value.Align, p.Value));

    private static (int Ref, ItemAlignment Align)? Parse(string key)
    {
        int colon = key.IndexOf(':');
        if (colon < 0 || !int.TryParse(key[..colon], out int r) || !int.TryParse(key[(colon + 1)..], out int a)) return null;
        return (r, (ItemAlignment)a);
    }

    /// <summary>
    /// Load the pens, or build the ones a new game starts with. Pass the creature list and a fresh
    /// game gets sub054E's ten creatures rather than an empty Confinement. A file keyed by Id is
    /// moved to table indices (<see cref="FromIdKeys"/>), and then pens filed Unaligned go to
    /// their creature's own alignment (<see cref="RepairUnaligned"/>). The list must be the
    /// whole table, as the loader gives it.
    /// </summary>
    public static Confinement Load(string savesDir, IReadOnlyList<Monster> monsters)
    {
        string p = PathFor(savesDir);
        if (!File.Exists(p))
        {
            var fresh = Fresh(monsters);
            fresh.Save(savesDir);
            return fresh;
        }
        var pens = Load(savesDir);
        if (pens._idKeyed) pens.FromIdKeys(monsters);
        pens.RepairUnaligned(monsters);
        return pens;
    }

    /// <summary>
    /// A file from before the pens were keyed by table index: "monsterId:alignment". Each pen
    /// moves to the table index of its creature.
    ///
    /// An Id that is one creature moves exactly. An Id two creatures share (only 307 in the
    /// shipped data: the Morgul, Neutral, and the Shadow Ninja, Evil) is settled by the pen's
    /// alignment where exactly one of them has it as its own - a Neutral 307 pen is the Morgul's,
    /// an Evil one the Shadow Ninja's - since sub054E and mdrReplenishCStore pen a creature at its
    /// own alignment (MAINMOD.BAS:551, 1379) and fn1163 gives a charmed or joined companion its
    /// own (MAINMOD2.BAS:392). A pen that does not settle that way (Good, which only a realigned
    /// sale fills, or Unaligned) goes the way the original moves a companion store file whose
    /// creature table has changed: <c>gv162E(i) = old record</c> for every table index i with the
    /// old record's Id (MODULE1.BAS:836-842) would give each creature the whole count, inventing
    /// stock; instead it goes to the first table index with the Id - the Morgul - by the same
    /// rule the COMPANION SORTER uses for a companion it cannot place (MAINMOD2.BAS:4722-4727,
    /// <c>Exit For</c>), so no count is made up. A pen whose Id no creature has is dropped, as
    /// MODULE1's loop drops it. Answers how many pens
    /// were moved.
    /// </summary>
    public int FromIdKeys(IReadOnlyList<Monster> monsters)
    {
        var old = Pens;
        Pens = new Dictionary<string, int>();
        int moved = 0;
        foreach (var (key, n) in old)
        {
            if (Parse(key) is not { } parsed) continue;
            var (idNum, align) = parsed;
            short id = (short)idNum;
            var withId = monsters.Where(m => m.Id == id).ToList();
            var same = withId.Where(m => AlignmentOf(m) == align).ToList();
            // one creature; else the one whose own alignment the pen has; else the first with the Id
            var to = withId.Count == 1 ? withId : same.Count == 1 ? same : withId.Take(1).ToList();
            foreach (var m in to)
            {
                string k = Key(CompanionRef.IndexOf(monsters, m), align);
                Pens[k] = Pens.GetValueOrDefault(k) + n;
            }
            if (to.Count > 0) moved++;
        }
        KeyedBy = ByRef;
        _idKeyed = false;
        return moved;
    }

    /// <summary>
    /// Pens filed as Unaligned, which the original cannot have: its pens are <c>Align(0 To 3)</c>
    /// indexed by the companion's alignment, 0 G 1 N 2 E (FRMCITYC.BAS:288-289), and the list
    /// shows those three (FRMCITYC.BAS:917-921). They came from companions the port's charm and
    /// join had left Unaligned (see <see cref="CombatMonster.CompanionAlignment"/>), which sold
    /// into a pen no column reads - "0 0 0" on the list. Each such pen goes to the creature's own
    /// alignment, the one fn1163 would have given the companion (MAINMOD2.BAS:392), capped at
    /// <see cref="ShelfCap"/> as a sale is (FRMCITYC.BAS:288). Answers how many creatures moved.
    /// The pen is a table index, one creature, so its own alignment is known exactly.
    /// </summary>
    public int RepairUnaligned(IReadOnlyList<Monster> monsters)
    {
        int moved = 0;
        foreach (var key in Pens.Keys.Where(k => k.EndsWith(":0")).ToList())
        {
            int n = Pens[key];
            if (Parse(key) is not { } p) continue;
            if (n <= 0) { Pens.Remove(key); continue; }
            if (CompanionRef.At(monsters, p.Ref) is not Monster def) continue;
            Pens.Remove(key);
            string to = Key(p.Ref, AlignmentOf(def));
            int there = Pens.GetValueOrDefault(to);
            Pens[to] = Math.Max(there, Math.Min(ShelfCap, there + n));
            moved += n;
        }
        return moved;
    }

    private static Confinement Load(string savesDir)
    {
        try
        {
            string p = PathFor(savesDir);
            if (File.Exists(p) && JsonSerializer.Deserialize<Confinement>(File.ReadAllText(p)) is Confinement read)
            {
                read._idKeyed = read.KeyedBy != ByRef;
                return read;
            }
        }
        catch { }
        return new Confinement();
    }

    public void Save(string savesDir)
    {
        try
        {
            if (_idKeyed) return;   // Id keys never go back out labelled as table indices
            Directory.CreateDirectory(savesDir);
            KeyedBy = ByRef;   // every pen this port makes is keyed by table index
            File.WriteAllText(PathFor(savesDir), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary><c>gv162E(Ref).Align(a)</c>: how many of this creature, at this alignment, are penned.</summary>
    public int Count(Monster def, ItemAlignment align) => RefOf(def) is int r ? Count(r, align) : 0;

    /// <summary>The pen at table index <paramref name="tableIndex"/>.</summary>
    private int Count(int tableIndex, ItemAlignment align) => Pens.GetValueOrDefault(Key(tableIndex, align));

    public bool AnyOf(Monster def) =>
        ItemAlignments.All.Any(a => Count(def, a) > 0);

    /// <summary>
    /// One bought out of the pen, <c>gv162E(l00B4)</c> with l00B4 the row's table index
    /// (FRMCITYC.BAS:141). The caller decides whether it comes off at all: cmdBuy_Click only
    /// decrements for a creature with a floor above 0 (FRMCITYC.BAS:140-142).
    /// </summary>
    public void Take(Monster def, ItemAlignment align)
    {
        // no floor: :141 subtracts one whatever is there, so a pen can go to -1
        if (RefOf(def) is not int r) return;
        string k = Key(r, align);
        Pens[k] = Pens.GetValueOrDefault(k) - 1;
    }

    /// <summary>
    /// A sold companion joins the pen of its Ref, <c>gv162E(Companions(i).Ref)</c>
    /// (FRMCITYC.BAS:288-291), unless it came from the top floor (level 0 monsters do not stock).
    /// </summary>
    public void Add(Monster def, ItemAlignment align)
    {
        if (def.LevelFound == 0 || RefOf(def) is not int r) return;
        string k = Key(r, align);
        int n = Pens.GetValueOrDefault(k);
        if (n < ShelfCap) Pens[k] = n + 1;
    }

    // ---------------------------------------------------------------- prices

    /// <summary>
    /// VB's Currency: every assignment to one keeps four decimal places, rounded half to even.
    /// </summary>
    private static double Cur(double v) => Math.Round(v, 4, MidpointRounding.ToEven);

    /// <summary>An assignment to a Long, which VB rounds half to even rather than cutting off.</summary>
    private static long Lng(double v) => (long)Math.Round(v, MidpointRounding.ToEven);

    /// <summary>
    /// What a creature is fundamentally worth, fn33A6 (FRMCITYC.BAS:528-604), a Currency. Attack
    /// and defence at 500 each and hits at 250, scaled by the log of its strength plus dexterity
    /// and the log of its guild level, and then a long tariff of surcharges for everything nasty it
    /// can do - the worst of them, a spell school it casts, at 5,250 a guild level.
    ///
    /// Each surcharge is written <c>3250@ * GuildLevel + 1</c>, which multiplies before it adds:
    /// the <c>+ 1</c> is a single gold piece, not a bump to the level. That reads like a slip but
    /// it is what the game charges, so it is what the port charges.
    ///
    /// The spell surcharges are the seven bits 0 to 6 and only when SpellFlags is above zero (:574);
    /// the resistances are the first eleven and only the positive ones count (:597-601); and a
    /// creature of one hit is worth exactly 1 whatever came before (:602).
    /// </summary>
    public static double BaseValue(Monster def)
    {
        double v = 500.0 * def.Att;
        v += 500.0 * def.Def;
        v += 250.0 * def.Hits;
        // Stats(m01DC) with m01DC a Static never assigned, so Stats(0), plus Stats(5). The Max(1, ..)
        // is the port's: the original's Log(0) would raise an error here.
        int str = def.Stats.Length > 0 ? def.Stats[0] : 0;
        int dex = def.Stats.Length > 5 ? def.Stats[5] : 0;
        v = Cur(v * Math.Log(Math.Max(1, str + dex)));
        v = Cur(v * Math.Log(def.GuildLevel + 1));

        int gl = def.GuildLevel;
        void Special(int bit, int rate) { if ((def.SpecialAttackFlags & (1 << bit)) != 0) v += rate * gl + 1; }
        Special(3, 3250); Special(4, 3250); Special(5, 2050); Special(6, 2050);
        Special(8, 1500); Special(10, 1500); Special(11, 500);

        if (def.SpellFlags > 0)
        {
            void Spell(int bit, int rate) { if ((def.SpellFlags & (1 << bit)) != 0) v += rate * gl + 1; }
            Spell(0, 5250); Spell(1, 3250); Spell(2, 4250); Spell(3, 3250);
            Spell(4, 5250); Spell(5, 5250); Spell(6, 4250);
        }

        for (int r = 0; r < 11 && r < def.Resistances.Length; r++)
            if (def.Resistances[r] > 0) v += 500.0 * def.Resistances[r];
        if (def.Hits == 1) v = 1;
        return v;
    }

    /// <summary>
    /// What they charge for one, modItemCost (FRMCITYC.BAS:620-641): half again its worth (never
    /// under 10), divided down by the log of how many of that kind are already penned - so the last
    /// of something is dear. Returned As Long, so rounded half to even.
    /// </summary>
    public long PriceToBuy(Monster def, ItemAlignment align)
    {
        double value = BaseValue(def);
        if (value < 10) value = 10;
        int stock = Math.Min(Count(def, align) - 1, ShelfCap);
        double price = (1.5 * value) / (Math.Log(stock + 1) + 1.15);
        // with none penned the original's Log(0) falls into its error trap and the price is 0
        return double.IsFinite(price) ? Lng(price) : 0;
    }

    /// <summary>
    /// What they pay for one, fn33FA (FRMCITYC.BAS:694-723): nine tenths of its worth over the
    /// stock, and then divided again by how well you know it - so an unidentified companion is
    /// worth a fraction of one you have had looked at. l024A is a Long, so half to even.
    /// </summary>
    public long PriceToSell(Monster def, ItemAlignment align, int idLevel)
    {
        int stock = Count(def, align);
        if (stock == 0 && AnyOf(def)) stock = 1;
        if (stock > ShelfCap) stock = ShelfCap;
        double value = BaseValue(def);
        long v = Lng(((0.9 * value) / (stock + 1.15)) / (idLevel + 1));
        return v < 0 ? 0 : v;
    }

    /// <summary>
    /// One step of identification, fn33E9 (FRMCITYC.BAS:674-692) - the same shape as the General
    /// Store's fee for an item, and likewise dearest for the step that finishes the job. Only ever
    /// asked for with the ID level above nought; the original would divide by zero.
    /// </summary>
    public long PriceToIdentify(Monster def, ItemAlignment align, int idLevel)
    {
        if (idLevel <= 0) return 0;
        int stock = Math.Min(Count(def, align), ShelfCap);
        double value = BaseValue(def);
        long v = Lng((0.13 * value) / ((stock / 2.0 + 1) * 0.9) / idLevel + 1);
        return v < 0 ? 0 : v;
    }

    /// <summary>
    /// Changing a companion's alignment, cmdRealign_Click (FRMCITYC.BAS:242-274): 0.72 of its
    /// worth, doubled if you are asking for the far side rather than a neighbour. l0122 is a Long.
    /// </summary>
    public static long PriceToRealign(Monster def, ItemAlignment from, ItemAlignment to) =>
        Lng(0.72 * BaseValue(def) * (Math.Abs((int)from - (int)to) > 1 ? 2 : 1));

    /// <summary>The binding a bought companion comes with: <c>5 + its floor</c>, capped at 10.</summary>
    public static int BoughtBinding(Monster def) => Math.Min(10, 5 + def.LevelFound);

    // ---------------------------------------------------------------- what they say

    public const string NoRoom = "No more room for companions!";
    public const string NotEnoughGold = "Not enough gold!";
    public const string NoDeadOnes = "We don't have any interest in dead companions!";
    public const string FreeWillCannotBind = "You cannot bind a companion that has joined you free of will.";
    public const string AlreadyBound = "Companion is already bound to or beyond our abilities!";

    public static string RealignOffer(long cost) =>
        $"Cost to realign is {cost:N0}.\nDo you wish to pay?";

    /// <summary>
    /// The pens a brand-new game starts with - <c>sub054E</c>, MAINMOD.BAS:522-559, the routine
    /// behind the "Creating Companion Store File..." line on the loading screen. The port used to
    /// start with the pens empty, which meant a new character had nothing to buy.
    ///
    /// Ten creatures, two of each, at the creature's own alignment. They are table indices, named
    /// by kind: <c>gv1DD2(kind, 0)</c> is the first table index of that kind (sub06D7,
    /// <see cref="EncounterGenerator.ClassRanges"/>) and <c>+ 1</c> the next index in the table - the
    /// first two Humanoids, Animals, Insects and Thieves, plus the first Cleanup and the first
    /// Undead on their own. <paramref name="monsters"/> is the whole table, as loaded.
    /// </summary>
    public static Confinement Fresh(IReadOnlyList<Monster> monsters)
    {
        var pens = new Confinement { KeyedBy = ByRef };
        var first = EncounterGenerator.ClassRanges(monsters);
        int kinds = first.GetLength(0);
        int Start(int kind) => kind < kinds ? first[kind, 0] : -1;
        // MAINMOD.BAS:544 - kinds 0, 7, 8 and 14 and the index after each, kinds 1 and 9 alone
        var picked = new[] { Start(0), Start(0) + 1, Start(7), Start(7) + 1, Start(8), Start(8) + 1,
                             Start(14), Start(14) + 1, Start(1), Start(9) };
        for (int i = 0; i < monsters.Count; i++)
            if (picked.Contains(i))
                pens.Pens[Key(i, AlignmentOf(monsters[i]))] = 2;   // Align(Monsters(i).Alignment) = 2 (:551)
        return pens;
    }

    /// <summary>
    /// A creature's own alignment, <c>M4EBF</c> - what it is penned as.
    ///
    /// A monster's alignment is the **character** scheme, 0 Good, 1 Neutral, 2 Evil, and not the
    /// item scheme where 0 means unaligned and the three alignments start at 1. The rest of the
    /// port already reads it that way (<c>(Alignment)m.AlignmentValue</c> in Combat and the
    /// dungeon window); this converts it, rather than casting the number straight across, which
    /// made every creature one alignment too holy - the help's own screenshot of the Confinement
    /// has Kobolds under N and Margoyles under E.
    ///
    /// Note the pen columns are three, G N E, not the store's four: a creature is never unaligned.
    /// </summary>
    public static ItemAlignment AlignmentOf(Monster m) => m.AlignmentValue switch
    {
        0 => ItemAlignment.Good,
        1 => ItemAlignment.Neutral,
        2 => ItemAlignment.Evil,
        _ => ItemAlignment.Neutral,
    };

    /// <summary>
    /// Other people trading creatures while you rest - <c>mdrReplenishCStore</c> (<c>sub0709</c>,
    /// MAINMOD.BAS:1284-1477). It is <c>mdrReplenishStore</c> written again for creatures, with the
    /// same shape: a <c>Clamp(days, 1, 50)</c> percent chance of another pass, 60% of passes adding
    /// and 40% taking one away, a floor biased shallow, then a random creature **kind**, then one
    /// creature of that kind weighted by its Chance. The port never restocked the pens at all.
    ///
    /// One difference from the store's version, and it is a mistake in the original: the cap test
    /// reads <c>gv162E(l028A).Align(l0288)</c>, and <c>l0288</c> is a procedure-level Dim that the
    /// adding path never assigns. It starts at 0 on each call and only the take-away path moves it
    /// (MAINMOD.BAS:1438 sets 0, :1447 a random 0 to 3), so the cap test reads whichever pen the
    /// last take-away touched in this call - Good (index 0) until then. Pen index i is alignment
    /// i: 0 Good 1 Neutral 2 Evil, and a fourth, 3, that nothing fills (sub054E fills
    /// <c>Align(Monsters().Alignment)</c>, and FRMCITYC captions index i as <c>gv077E$(i + 1)</c>).
    /// It then increments the creature's **own** alignment, so a creature can be penned past nine
    /// whenever the pen tested is not its own. When the test fails the pass is spent - l028A is
    /// still set, so the Do loop ends without penning anything. The store's copy of the same loop
    /// assigns its index first and does not have this. Reproduced, variable and all.
    /// </summary>
    public void Replenish(IReadOnlyList<Monster> monsters, int days, Random rng, int deepestReached = StoreStock.DeepestStocked)
    {
        int deepest = Math.Clamp(deepestReached, 1, StoreStock.DeepestStocked);
        double chance = Math.Clamp(days, 1, 50);

        var byKind = monsters.Where(m => m.Id != 0).GroupBy(m => (int)m.Type)
                             .ToDictionary(g => g.Key, g => g.OrderBy(m => m.LevelFound).ToList());
        int kinds = byKind.Count == 0 ? 0 : byKind.Keys.Max() + 1;
        if (kinds == 0) return;

        int passes = 0;
        int l0288 = 0;          // the one pen index both paths share, 0 at the start of each call
        while (rng.NextDouble() * 100 <= chance && passes++ < 500)
        {
            bool adding = rng.NextDouble() * 100 < 60;

            int roll = rng.Next(100) + 1, floor = 0;
            for (int f = 1; f <= deepest; f++)
            {
                if (roll < 100 - (f / (double)(deepest + 1)) * 100) floor = f; else break;
            }
            floor = Math.Clamp(floor, 1, deepest);

            bool done = false;
            for (int tries = 0; !done && tries < 100; tries++)
            {
                var chosen = DrawOne(byKind, kinds, floor, rng);
                if (chosen == null) continue;
                done = adding ? PenOne(chosen, l0288) : ReleaseOne(chosen, rng, ref l0288);
            }
        }
    }

    private static Monster? DrawOne(Dictionary<int, List<Monster>> byKind, int kinds, int floor, Random rng)
    {
        // MAINMOD.BAS:1337 is `l027E = (Rnd * NumMonsterClasses)` with `l027E As Integer`, the same
        // rounded draw the item store makes at :1598 - so kind 0 comes up half as often as the
        // rest, and the draw can land one past the last kind and match nothing, which sends the
        // original's Do loop round again. See StoreStock.DrawOne.
        int kind = Rules.RoundedIndex(rng, kinds);
        if (!byKind.TryGetValue(kind, out var all)) return null;
        var run = all.Where(m => m.LevelFound <= floor).ToList();
        if (run.Count == 0) return null;

        int total = run.Sum(m => (int)m.Chance);
        if (total <= 0) return null;
        double pick = rng.NextDouble() * 100, acc = 0;
        Monster? chosen = null;
        foreach (var m in run)
        {
            acc += m.Chance / (double)total * 100;
            if (pick <= acc) { chosen = m; break; }
        }
        // MAINMOD.BAS:1371 - a creature from the town level is never penned by anyone else
        return chosen != null && chosen.LevelFound > 0 ? chosen : null;
    }

    /// <summary>Pen index i of gv162E().Align, 0 Good 1 Neutral 2 Evil 3 the unused fourth.</summary>
    private static ItemAlignment PenIndex(int i) => (ItemAlignment)(i + 1);

    /// <summary>
    /// Somebody sold one in. The cap is tested against pen <paramref name="l0288"/> and the
    /// increment lands on the creature's own - see the note on <see cref="Replenish"/>. A failed
    /// test still ends the pass (MAINMOD.BAS:1372-1391), so it answers true with nothing penned.
    /// </summary>
    private bool PenOne(Monster m, int l0288)
    {
        if (Count(m, PenIndex(l0288)) >= ShelfCap) return true;
        var a = AlignmentOf(m);
        if (RefOf(m) is int r) Pens[Key(r, a)] = Count(m, a) + 1;
        return true;
    }

    /// <summary>
    /// Somebody bought one, MAINMOD.BAS:1436-1465. l0288 is set to 0 and the "is any left" loop
    /// tests <c>Align(l0288)</c> four times over - the Good pen only - so a creature with none
    /// Good is never bought out and the Do loop draws again. Otherwise a pen is drawn with
    /// <c>Int(Rnd * 4)</c> until one is not zero, and it loses one; l0288 keeps that index.
    /// </summary>
    private bool ReleaseOne(Monster m, Random rng, ref int l0288)
    {
        l0288 = 0;
        if (Count(m, PenIndex(l0288)) <= 0) return false;
        do l0288 = rng.Next(4); while (Count(m, PenIndex(l0288)) == 0);
        Take(m, PenIndex(l0288));
        return true;
    }
}
