using System.Text.Json;

namespace Mordor.Core;

/// <summary>
/// A party of up to four characters who explore and fight together (the original's Parties page).
/// One member is "selected" at a time and receives your commands; every member fights, takes hits
/// and earns experience. A party of one is just a lone adventurer, so all the game code can assume
/// a party exists and nothing needs a single-character special case.
/// </summary>
public sealed class Party
{
    public const int MaxSize = 4;

    public List<Character> Members { get; } = new();

    /// <summary>Index of the character whose commands you are entering. Clamped to a living member where possible.</summary>
    public int SelectedIndex { get; private set; }

    public Party(params Character[] members)
    {
        foreach (var m in members) Add(m);
    }

    public Character Selected => Members[Math.Clamp(SelectedIndex, 0, Members.Count - 1)];
    public Character Leader => Members[0];
    public int Size => Members.Count;

    /// <summary>Members who can still act: alive, not petrified.</summary>
    public IEnumerable<Character> Able => Members.Where(c => !c.IsDead && !c.Stoned && c.CurHits > 0);
    public bool AnyAble => Able.Any();

    /// <summary>Everything the monsters can attack: each living member and each of their companions.</summary>
    public IEnumerable<(Character owner, Companion? companion)> Targets()
    {
        foreach (var c in Able)
        {
            yield return (c, null);
            foreach (var comp in c.Companions.All) yield return (c, comp);
        }
    }

    /// <summary>
    /// Can this character join? From the help, "Parties": at most four, "characters that are
    /// oppositely aligned cannot be in the same party together", and "in order for other
    /// characters to join you, they must be in the same room, or in the city with you, and alive".
    /// </summary>
    public string? WhyCannotJoin(Character c)
    {
        if (Members.Count == 0) return null;      // the first member is the leader, and always joins

        // Two routines refuse a join, and the one the player meets first is frmInteract - the box
        // that asks "Character's Name?" - which checks the name before the character is loaded at
        // all (FRMINTER.BAS:192-245, tag "JOIN"). Its wording is what is used here. fn1521's own
        // refusals come later and are quoted where they are still reachable.
        //
        // "Your party is too full!" is fn1521's (MAINMOD2.BAS:3447); the box makes no such check.
        if (Members.Count >= MaxSize) return "Your party is too full!";
        if (Members.Any(m => m.Name == c.Name)) return Interact.AlreadyInParty;

        // The box refuses a dead character outright. fn1521's longer line - "Name" is Dead! Cast a
        // Resurrect spell on the character to have them re-join! - is for the case the help
        // describes, "a character who is dead ... must have a raise spell cast on them already",
        // which this port does not model yet. Using it here would put the wrong one of the two in
        // front of the player.
        if (c.IsDead) return Interact.IsDead;

        // "In order for other characters to join you, they must be in the same room, or in the
        // city with you, and alive" (help, Parties), and the box says so in one line for every
        // command that names somebody, not just this one.
        if (!SamePlace(Leader, c)) return Interact.NotHere;

        foreach (var m in Members)
            if (Opposed(m.Alignment, c.Alignment))
                return $"\"{c.Name.TrimEnd()}\" is oppositely aligned to one or more members of the party!";
        return null;
    }

    /// <summary>
    /// frmInteract's JOIN list in the dungeon (FRMINTER.BAS:352-387): every character on file who
    /// is not in the party (369-374) and whose record is on this floor and in this area,
    /// <c>DungeonLevel = Chars(Me).DungeonLevel And M547A = Chars(Me).M59BB</c> (377-380) - the
    /// dead included. <paramref name="savedArea"/> reads a record's M59BB (or works it out for a
    /// save from before the port kept it).
    /// </summary>
    public List<Character> HereToJoin(IEnumerable<Character> everyone, int level, int area, int floors,
                                      Func<Character, int> savedArea) =>
        everyone.Where(c => Members.All(m => m.Name != c.Name) && c.DungeonLevel == level
                            && c.DungeonLevel >= 1 && c.DungeonLevel <= floors
                            && savedArea(c) == area)
                .ToList();

    /// <summary>In the city together, or standing on the same square of the same dungeon level.</summary>
    public static bool SamePlace(Character a, Character b) =>
        a.DungeonLevel == 0 && b.DungeonLevel == 0
        || a.DungeonLevel == b.DungeonLevel && a.X == b.X && a.Y == b.Y;

    /// <summary>
    /// <c>sub0848 True</c> (MULTIMOD.BAS:446-458), as the join case calls it once the newcomer is
    /// loaded (MAINMOD2.BAS:3477): <c>M59F4</c> 0, 1 and 2 - lost on the floor, in position, in
    /// facing - copied from <c>Chars(m0022)</c>, the first member, to the newcomer.
    /// </summary>
    public static void ShareLostFlags(Character first, Character newcomer)
    {
        newcomer.LostLevel = first.LostLevel;
        newcomer.LostPosition = first.LostPosition;
        newcomer.LostFacing = first.LostFacing;
    }

    public static bool Opposed(Alignment a, Alignment b) =>
        (a == Alignment.Good && b == Alignment.Evil) || (a == Alignment.Evil && b == Alignment.Good);

    // ---- passing things around (help, "Parties": Giving Items & Gold, and ALT-p to pool) ----

    /// <summary>
    /// ALT-p: "Pool party's on-hand gold to current character". Only what everyone is carrying
    /// moves; bank accounts are each character's own. Returns how much was gathered.
    /// </summary>
    /// <summary>
    /// ALT-p, <c>fn1521</c> case 80 with the shift state set (MAINMOD2.BAS:3635-3654). Three lines,
    /// and the port had invented all three:
    ///
    /// <code>
    /// If fn0EEF() > 0 And Foes(0).Engaged Then sub085B "Cannot pool gold while in combat!"
    /// Else If UBound(Chars) > m00A6 Then ... sub085B "Pool of party's on-hand gold to " + Chr(34) + RTrim(Name) + Chr(34) + " completed!"
    ///      Else sub085B "Nobody else in party to Pool to!"
    /// </code>
    ///
    /// The refusal is on the size of the party, not on whether any gold moved. A party of two
    /// where the other has nothing to give still says it completed - which is right, because the
    /// command did what it was asked.
    /// </summary>
    public const string NobodyToPoolTo = "Nobody else in party to Pool to!";

    /// <summary>The same case, when a fight is on (MAINMOD2.BAS:3638).</summary>
    public const string CannotPoolInCombat = "Cannot pool gold while in combat!";

    public static string PoolCompleted(Character to) =>
        $"Pool of party's on-hand gold to \"{to.Name.TrimEnd()}\" completed!";

    /// <summary>
    /// 'g' Give, <c>fn1521</c> case 71 (MAINMOD2.BAS:3706-3712). Its one refusal before it starts
    /// asking questions, when there is nobody to give to.
    /// </summary>
    public const string NobodyToGiveTo = "Nobody in party to give to!";

    /// <remarks>
    /// An Overflow (a purse at the edge of Currency's range) is caught by fn1521's trap, which
    /// writes to the error file only and leaves the command where it got to: members already
    /// pooled stay pooled, the one that overflowed keeps their gold, and the rest are not asked.
    /// </remarks>
    public VbCurrency PoolGold()
    {
        var to = Selected;
        VbCurrency moved = 0;
        try
        {
            foreach (var m in Members)
            {
                if (m == to) continue;
                var took = m.GoldOnHand;
                to.GoldOnHand += took;      // MAINMOD2.BAS:3644
                m.GoldOnHand = 0;           // 3645
                moved = VbCurrency.AddSaturating(moved, took);
            }
        }
        catch (OverflowException) { }   // fn1521's trap (4169-4171)
        return moved;
    }

    /// <summary>
    /// The Bank's party options (FRMCITYB.BAS:38-59) have no error handler, so an Overflow in one
    /// ends the original program, and the gold already moved in memory is never saved. The port
    /// does not die: it puts back every balance the option touched, so nothing moves at all.
    /// Returns false when that happened.
    /// </summary>
    private static bool AllOrNothing(IEnumerable<Character> who, Action move)
    {
        var before = who.Distinct().Select(c => (c, c.GoldOnHand, c.GoldInBank)).ToList();
        try { move(); return true; }
        catch (OverflowException)
        {
            foreach (var (c, hand, bank) in before) { c.GoldOnHand = hand; c.GoldInBank = bank; }
            return false;
        }
    }

    /// <summary>
    /// The Bank's "Party Pool", <c>cmdCommand_Click</c>'s first party option (FRMCITYB.BAS:38-42):
    /// every member's gold on hand, this character's included, into this character's account.
    /// Silent there. All or nothing on an Overflow (<see cref="AllOrNothing"/>).
    /// </summary>
    public static bool PoolToBank(IEnumerable<Character> members, Character to)
    {
        var all = members.ToList();
        return AllOrNothing(all.Append(to), () =>
        {
            foreach (var m in all) { to.GoldInBank += m.GoldOnHand; m.GoldOnHand = 0; }
        });
    }

    /// <summary>
    /// <see cref="PoolToBank"/>, the Bank's Party Pool, for a mod's pool on arriving in town
    /// (the Quality of Life mod's Town gold, "Leader's bank"): the same move, by the same rule,
    /// returning the total moved - nought when nothing moved, or on an Overflow (all or nothing).
    /// </summary>
    public static VbCurrency PoolOnHandToBank(IEnumerable<Character> members, Character to)
    {
        var all = members.ToList();
        VbCurrency moved = 0;
        foreach (var m in all) moved = VbCurrency.AddSaturating(moved, m.GoldOnHand);   // the port's own total, for the mod's line
        return PoolToBank(all, to) ? moved : VbCurrency.Zero;
    }

    /// <summary>
    /// The Bank's "Party Pool &amp; Deposit", <c>cmdCommand_Click</c>'s third party option
    /// (FRMCITYB.BAS:48-59): every other member's banked and on-hand gold goes into this
    /// character's account and theirs are left at nothing, and this character's own on-hand gold is
    /// deposited too. Silent there. The Bank's button and the Quality of Life mod's ALT-p both come
    /// here, so the money moves by one rule. All or nothing on an Overflow (<see cref="AllOrNothing"/>).
    /// </summary>
    public static bool PoolAndDeposit(IEnumerable<Character> members, Character to)
    {
        var all = members.ToList();
        return AllOrNothing(all.Append(to), () =>
        {
            foreach (var m in all)
            {
                if (m == to) continue;
                // two statements there (54-55), so the two balances are never summed first
                to.GoldInBank += m.GoldInBank;
                to.GoldInBank += m.GoldOnHand;
                m.GoldInBank = 0; m.GoldOnHand = 0;
            }
            to.GoldInBank += to.GoldOnHand; to.GoldOnHand = 0;
        });
    }

    /// <summary>
    /// The Bank's "Party Deposit", <c>cmdCommand_Click</c>'s second party option
    /// (FRMCITYB.BAS:43-47): every member's gold on hand goes into their own account. Silent
    /// there. Returns the total moved. The Bank's button and the Quality of Life mod's deposit on
    /// arrival both come here, so the money moves by one rule. All or nothing on an Overflow
    /// (<see cref="AllOrNothing"/>), and then the total is nought.
    /// </summary>
    public static VbCurrency PartyDeposit(IEnumerable<Character> members)
    {
        var all = members.ToList();
        VbCurrency moved = 0;
        bool done = AllOrNothing(all, () =>
        {
            foreach (var m in all)
            {
                moved = VbCurrency.AddSaturating(moved, m.GoldOnHand);   // the port's own total, for the mod's line
                m.GoldInBank += m.GoldOnHand;
                m.GoldOnHand = 0;
            }
        });
        return done ? moved : VbCurrency.Zero;
    }

    /// <summary>What the Bank's text box does with Enter: nothing at all, a Beep, or the gold moved.</summary>
    public enum BankAnswer { Ignored, Beep, Moved }

    /// <summary>
    /// <c>txtAmount_KeyPress</c> (FRMCITYB.BAS:303-331). The amount is <c>CCur</c> of what was
    /// typed, a Currency, so a fraction is kept to four places - this is the one place in the
    /// game where gold gets one. Not a number (On Error Resume Next) or nought (<c>If l00E6</c>) is
    /// <see cref="BankAnswer.Ignored"/>, and the box keeps its text. A deposit of more than is on
    /// hand, or of nought or less, Beeps; a withdrawal Beeps only for more than the account holds
    /// or an account already below nought, so a negative withdrawal goes through and pays in with
    /// no check on the gold on hand. Either way the box is then emptied.
    /// <para>
    /// The routine runs under On Error Resume Next, so a statement that overflows is skipped and
    /// the next one runs: a negative withdrawal that would carry the account past the top of
    /// Currency's range leaves the account as it was and still takes the amount off the gold on
    /// hand. That is the original's, and kept; the port does not die of it.
    /// </para>
    /// </summary>
    public static BankAnswer BankTyped(Character ch, string text, bool deposit)
    {
        if (!VbCurrency.TryParse(text, out var amount) || amount == 0) return BankAnswer.Ignored;
        if (deposit)
        {
            if (amount > ch.GoldOnHand || amount <= 0) return BankAnswer.Beep;
            if (VbCurrency.TryAdd(ch.GoldInBank, amount, out var bank)) ch.GoldInBank = bank;          // 315
            if (VbCurrency.TrySubtract(ch.GoldOnHand, amount, out var hand)) ch.GoldOnHand = hand;     // 316
        }
        else
        {
            if (amount > ch.GoldInBank || ch.GoldInBank < 0) return BankAnswer.Beep;
            if (VbCurrency.TryAdd(ch.GoldOnHand, amount, out var hand)) ch.GoldOnHand = hand;          // 323
            if (VbCurrency.TrySubtract(ch.GoldInBank, amount, out var bank)) ch.GoldInBank = bank;     // 324
        }
        return BankAnswer.Moved;
    }

    /// <summary>The "All" buttons' text, <c>LTrim$(Str$(balance))</c> (FRMCITYB.BAS:29-31): "10.5", ".25".</summary>
    public static string BankAllText(VbCurrency balance) => balance.Str().TrimStart(' ');

    /// <summary>One member's part in covering another's shortfall: how much they handed over.</summary>
    public sealed record ShortfallGift(Character Giver, VbCurrency Amount);

    /// <summary>
    /// What other members would hand <paramref name="payer"/> so that their purse - gold on hand
    /// and bank together, what charPay takes (<see cref="Character.Pay"/>) - reaches
    /// <paramref name="cost"/>. Not the original's: the Quality of Life mod's Town gold
    /// (docs/MODDING.md). The givers are <paramref name="from"/> in its order - every other member
    /// in party order when null - leaving out the payer, anyone not in the party, below ground or
    /// dead (the payer must be in the city too); each gives from their bank account only, in turn,
    /// until exactly the shortfall is found. One gift per giver who gives, in the givers' order.
    /// Empty when the purse is already enough; null when the givers together cannot cover it, or
    /// there is nobody to ask. Moves nothing.
    /// </summary>
    public static List<ShortfallGift>? PlanShortfall(IReadOnlyList<Character> members, Character payer, long cost,
                                                     IReadOnlyList<Character>? from = null)
    {
        VbCurrency need = VbCurrency.SubtractSaturating(cost, payer.Purse);
        if (need <= 0) return new List<ShortfallGift>();
        if (payer.DungeonLevel != 0 || !members.Contains(payer)) return null;
        var givers = (from ?? members).Distinct()
            .Where(m => m != payer && members.Contains(m) && m.DungeonLevel == 0 && !m.IsDead).ToList();
        if (givers.Count == 0) return null;
        var gifts = new List<ShortfallGift>();
        foreach (var g in givers)
        {
            if (need <= 0) break;
            VbCurrency part = VbCurrency.Min(VbCurrency.Max(0, g.GoldInBank), need);
            if (part <= 0) continue;
            gifts.Add(new ShortfallGift(g, part));
            need -= part;
        }
        return need > 0 ? null : gifts;
    }

    /// <summary>
    /// <see cref="PlanShortfall"/>, done: each giver's part comes out of their bank account and
    /// goes onto the payer's gold on hand, so the payer's purse is then exactly
    /// <paramref name="cost"/> and <see cref="Character.Pay"/> takes it all (the hand, then the
    /// payer's own bank, as charPay). Null, and nothing moved, when the givers cannot cover it - or
    /// when handing it over would carry the payer's gold on hand past Currency's range, which
    /// counts as not covered.
    /// </summary>
    public static List<ShortfallGift>? CoverShortfall(IReadOnlyList<Character> members, Character payer, long cost,
                                                      IReadOnlyList<Character>? from = null)
    {
        var gifts = PlanShortfall(members, payer, cost, from);
        if (gifts == null) return null;
        bool done = AllOrNothing(gifts.Select(g => g.Giver).Append(payer), () =>
        {
            foreach (var g in gifts)
            {
                g.Giver.GoldInBank -= g.Amount;
                payer.GoldOnHand += g.Amount;
            }
        });
        return done ? gifts : null;
    }

    /// <summary>Hand gold to another member. Returns what actually moved: nothing, if it would overflow.</summary>
    public static VbCurrency GiveGold(Character from, Character to, VbCurrency amount)
    {
        VbCurrency moved = VbCurrency.Min(VbCurrency.Max(amount, 0), from.GoldOnHand);
        if (!VbCurrency.TryAdd(to.GoldOnHand, moved, out var got) || !VbCurrency.TrySubtract(from.GoldOnHand, moved, out var left))
            return VbCurrency.Zero;
        from.GoldOnHand = left;
        to.GoldOnHand = got;
        return moved;
    }

    /// <summary>
    /// What <c>sub14B5</c> says when gold changes hands (MAINMOD2.BAS:2934). Its partner for an
    /// item is <see cref="GaveItem"/>.
    /// </summary>
    public static string GaveGold(long amount, Character to) =>
        $"Give of {amount:N0} gold to \"{to.Name.TrimEnd()}\" Completed!";

    /// <summary>
    /// The Exchange screen's gold transfer (FRMEXCHA.BAS:92-128). Below ground you can only hand
    /// over what you are carrying; in the city the bank makes up the difference, exactly as it
    /// does when you buy something. Returns null on success, or why not.
    ///
    /// Only zero is turned away. A negative amount passes the "Too Much!" test and runs the
    /// transfer backwards - the one dropped on pays, with no check that they have it and no bank
    /// to make up the difference, so their gold can go below zero. That is pnlGold_DragDrop as
    /// written (FRMEXCHA.BAS:112-124), and it is kept.
    ///
    /// pnlGold_DragDrop's error trap (97, 128-130) leaves the routine silently, so an Overflow
    /// stops the transfer where it got to. The "Too Much!" test forms <c>Gold + BankGold</c> on
    /// either level - VB's <c>Or</c> evaluates both sides - so a purse whose two halves overflow
    /// together stops it before anything moves.
    /// </summary>
    public static string? TransferGold(Character from, Character to, long amount)
    {
        if (amount == 0) return null;
        if (!VbCurrency.TryAdd(from.GoldOnHand, from.GoldInBank, out var purse)) return null;   // 115, the trap
        VbCurrency available = from.DungeonLevel != 0 ? from.GoldOnHand : purse;
        if (amount > available) return "Too Much!";

        try
        {
            to.GoldOnHand += amount;            // 119
            from.GoldOnHand -= amount;          // 120
            if (from.GoldOnHand < 0)            // the shortfall comes out of the bank
            {
                from.GoldInBank += from.GoldOnHand;
                from.GoldOnHand = 0;
            }
        }
        catch (OverflowException) { }        // the trap: Resume to the end, silently
        return null;
    }

    /// <summary>
    /// The Exchange screen swaps two pack slots, which may belong to different characters
    /// (FRMEXCHA.BAS:156-214). You drag an item from a source panel onto a destination panel and
    /// the two trade places. It refuses a guild-issued item on either side, and anything worn -
    /// but only when the two slots belong to different people, since rearranging your own pack is
    /// nobody else's business.
    ///
    /// Only the <paramref name="to"/> character then re-reads what they have been handed: the
    /// original's handler runs on the panel that was dropped on, and looks at that slot alone.
    /// Dragging the other way is a separate drop, and re-reads the other side.
    ///
    /// Returns null on success, or why not.
    /// </summary>
    public static string? SwapItems(Character from, int slotFrom, Character to, int slotTo, Func<short, Item?> item)
    {
        // pnlItem_DragDrop reads M5857 at the panel's own index - a pack slot, not a place in a
        // list - and refuses when either end is slot 0, which is the character's Hands
        // (FRMEXCHA.BAS:177).
        if (slotFrom <= 0 || slotFrom >= Character.PackSize) return "";   // refused in silence; "Nothing there." was the port's
        if (slotTo <= 0 || slotTo >= Character.PackSize) return "";
        if (from == to && slotFrom == slotTo) return null;

        // Both ends may be empty, and an empty end is the whole point: dropping onto a slot with
        // nothing in it is how the original hands an item over, because swapping with nothing is
        // giving. That is also where this used to lose the item. It wrote the two slots back by
        // asking SlotOf for the index of the object it was holding, and a stand-in built for an
        // empty slot belongs to nobody, so SlotOf answered -1 and PutInSlot(-1, ...) did nothing:
        // the source slot was emptied and the item never arrived. The indices were in hand all
        // along.
        var source = from.Pack[slotFrom];
        var destination = to.Pack[slotTo];
        var itSource = source == null ? null : item(source.ItemId);
        var itDestination = destination == null ? null : item(destination.ItemId);

        if (itDestination != null && !ChestTraps.Destructible(itDestination)) return "Destination Item is a Guild-Issued Item!";
        if (itSource != null && !ChestTraps.Destructible(itSource)) return "Source Item is a Guild-Issued Item!";
        if (from != to)
        {
            if (destination?.Equipped == true) return "Destination Item is either Equipped or in the Bank!";
            if (source?.Equipped == true) return "Source Item is either Equipped or in the Bank!";
        }

        from.PutInSlot(slotFrom, destination);
        to.PutInSlot(slotTo, source);

        // a buffer pointing at the slot that has just been emptied has nothing left to point at
        if (destination == null) SpellBuffers.PackSlotEmptied(from, slotFrom);
        if (source == null) SpellBuffers.PackSlotEmptied(to, slotTo);

        if (source != null && itSource != null) Loot.OnReceived(to, source, itSource, item);
        return null;
    }

    /// <summary>
    /// The full-pack refusal of <c>sub14B5</c>. Unlike the other two it is not a <c>sub085B</c>
    /// line but <c>MsgBox(..., mc02EC, "SORRY!")</c> (MAINMOD2.BAS:2953, mc02EC = 64), so the
    /// callers need to tell it apart.
    /// </summary>
    public static string NoFreeSlots(Character to) => "\"" + to.Name.TrimEnd() + "\" has no free slots!";

    /// <summary>
    /// Hand an item to another member. Cursed gear that is equipped will not come off, and the
    /// receiver's pack holds the same thirty-nine as everyone else's.
    /// </summary>
    /// <summary>
    /// <c>sub14B5</c> (MAINMOD2.BAS:2936-2968). Three refusals, and none of them is the port's
    /// old ones: a guild-issued item cannot change hands at all, an equipped one is refused flatly
    /// rather than taken off first, and a full pack is somebody's name and an exclamation mark.
    ///
    /// <paramref name="identified"/> is handed the line to say when the person receiving it can
    /// read it better than its owner could - <c>"Brannoc" identified the given item as: ...</c>
    /// (see <see cref="ReadGiven"/>). A handover that works then ends with
    /// <see cref="GaveItem"/>, which the callers write after it (MAINMOD2.BAS:3004-3006).
    /// </summary>
    public static string? GiveItem(Character from, Character to, InventorySlot slot, Item? it,
                                   Func<short, Item?>? lookup = null, Action<string>? identified = null,
                                   Action<int, IdLevel>? filed = null)
    {
        if (it != null && !ChestTraps.Destructible(it)) return "Cannot give Guild-Issued Items!";
        if (slot.Equipped) return "Cannot give equipped items!";
        if (to.FirstFreeSlot() < 0) return NoFreeSlots(to);

        from.RemoveFromPack(slot);
        to.PutInPack(slot);
        if (it != null) ReadGiven(to, slot, it, lookup, identified, filed);
        return null;
    }

    /// <summary>
    /// The last line of <c>sub14B5</c> when an item went across, from the pack or from the vault
    /// alike: <c>sub085B "Give of Item to " + Chr$(34) + RTrim(Name) + Chr$(34) + " Completed!"</c>
    /// (MAINMOD2.BAS:3004-3006). <c>sub085B</c> is live all game (gv1B52 is set True at
    /// MAINMOD2.BAS:6308), so this line is always written.
    /// </summary>
    public static string GaveItem(Character to) => "Give of Item to \"" + to.Name.TrimEnd() + "\" Completed!";

    /// <summary>
    /// The vault branch's full-pack box (MAINMOD2.BAS:2985): <c>MsgBox(Chars(p049A).Name + " has no
    /// free slots!", mc02EC, "SORRY!")</c>. Unlike the pack branch's (MAINMOD2.BAS:2953) the name is
    /// neither quoted nor RTrimmed - kept as written.
    /// </summary>
    public static string NoFreeSlotsFromVault(Character to) => to.Name + " has no free slots!";

    /// <summary>
    /// <c>sub14B5</c>'s vault branch (MAINMOD2.BAS:2972-3002), reached from <c>lblName_DragDrop</c>
    /// when the dragged control's Tag starts "BANK" (FRMGROUP.BAS:189-193) - before the combat test
    /// and before the "onto yourself" test, so neither applies and a character can hand a vault
    /// item into their own pack.
    ///
    /// Only the guild-issued refusal is made; there is no equipped test on this branch. The whole
    /// record moves into the receiver's first free slot (charges, alignment, the equipped and
    /// cursed bits and what is known of it), then the vault slot is emptied
    /// (<c>M5860(p049C).Ref = False</c>).
    ///
    /// Returns null when it went across, "" when there was nothing in that vault slot, and
    /// otherwise the refusal; the full pack is <see cref="NoFreeSlotsFromVault"/>, a MsgBox.
    /// </summary>
    public static string? GiveFromVault(Character from, int bankSlot, Character to,
                                        Func<short, Item?>? lookup = null, Action<string>? identified = null,
                                        Action<int, IdLevel>? filed = null)
    {
        if (bankSlot < 0 || bankSlot >= from.Bank.Count || from.Bank[bankSlot].ItemId <= 0) return "";
        var slot = from.Bank[bankSlot];
        var it = lookup?.Invoke(slot.ItemId);
        // isDestructible(M5860(p049C).Ref) = False (MAINMOD2.BAS:2974)
        if (it != null && !ChestTraps.Destructible(it)) return "Cannot give Guild-Issued Items!";
        // For l04A4 = 1 To mc0190 - 1 (MAINMOD2.BAS:2978)
        int free = to.FirstFreeSlot();
        if (free < 0) return NoFreeSlotsFromVault(to);
        to.PutInSlot(free, slot);                    // Pack(l04A0) = M5860(p049C) (MAINMOD2.BAS:2988)
        if (it != null) ReadGiven(to, slot, it, lookup, identified, filed);
        from.Bank[bankSlot] = new InventorySlot();   // M5860(p049C).Ref = False (MAINMOD2.BAS:2997)
        return null;
    }

    /// <summary>
    /// What the receiver makes of a given item, the same five lines in both branches of
    /// <c>sub14B5</c> (MAINMOD2.BAS:2957-2964, 2989-2996):
    /// <code>
    /// If Pack(l04A0).IdLevel &lt;&gt; m0372 Then
    ///     If fn12F0(p049A, Pack(l04A0).Ref) &lt; Pack(l04A0).IdLevel Then
    ///         Pack(l04A0).IdLevel = fn12F0(p049A, Pack(l04A0).Ref)
    ///         sub085B """Name"" identified the given item as: " + itemNameFor(...)
    ///         sub0730 p049A, Ref, IdLevel, -1, False
    /// </code>
    /// The original counts IdLevel downwards (0, m0372, is fully known), the port's
    /// <see cref="IdLevel"/> upwards, so "less than" becomes "more than". Unlike the Exchange's
    /// re-reading (<see cref="Loot.OnReceived"/>, FRMEXCHA.BAS:201-205) both calls pass the item's
    /// own reference, so there is no stray LevelScale read here. <c>sub0730</c> is the Library
    /// filing the item at its new level; <paramref name="filed"/> is handed (item id, level) for
    /// <see cref="Librarian.FileItem(string, Character, int, IdLevel)"/>, which grades it as sub0730 does.
    /// </summary>
    private static void ReadGiven(Character to, InventorySlot slot, Item it, Func<short, Item?>? lookup,
                                  Action<string>? identified, Action<int, IdLevel>? filed)
    {
        if (slot.Known == IdLevel.All) return;
        var can = Loot.ReadableLevel(to, it, lookup);
        if (can <= slot.Known) return;
        slot.Known = can;
        identified?.Invoke("\"" + to.Name.TrimEnd() + "\" identified the given item as: "
                         + Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges));
        filed?.Invoke(it.Id, slot.Known);
    }

    public bool Add(Character c)
    {
        if (WhyCannotJoin(c) != null) return false;
        Members.Add(c);
        return true;
    }

    /// <summary>
    /// Take someone out of the party - <c>sub04DC</c> (mdrcharUnload, MAINMOD.BAS:414-450). It
    /// removes whichever index it is given and shifts everyone after them down, and does no
    /// checking at all.
    ///
    /// The checking lives one level up, in <c>sub159D</c> (MULTIMOD.BAS:739-751), which is what
    /// the Leave button calls: it refuses while monsters are on the square, and it refuses to
    /// take out whoever stands at position one - "Leader cannot leave Party!" This comment used
    /// to say the leader rule was the port's invention, which was a conclusion drawn from the
    /// wrong routine.
    /// </summary>
    public bool Remove(Character c)
    {
        int i = Members.IndexOf(c);
        if (i < 0 || Members.Count == 1) return false;
        Members.RemoveAt(i);
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Members.Count - 1);
        return true;
    }

    /// <summary>
    /// Whether taking this person out would hand the party to somebody else, and who that is. Used
    /// to print the original's "X is now party leader!" after the fact.
    /// </summary>
    public Character? LeaderAfterRemoving(Character c) =>
        Members.Count > 1 && Members.IndexOf(c) == 0 ? Members[1] : null;

    /// <summary>The original's line when position one changes hands (FRMGROUP.BAS:239).</summary>
    public static string NowLeader(Character c) => $"\"{c.Name}\" is now party leader!";

    /// <summary>
    /// Everyone who has died leaves the party where they fell - the first half of <c>sub2F40</c>
    /// (modUpdateGroup, FRMGROUP.BAS:321-348). The port never did this, so corpses stayed on the
    /// roster for the rest of the session.
    ///
    /// Three details are the original's, not conveniences:
    ///
    /// - it only prunes while <c>UBound(gv14C2) &gt; m0052</c>, that is while more than one person
    ///   is in the party. A character adventuring alone who dies stays exactly where they are,
    ///   which is what lets the Wait for Rescue screen work
    /// - the death is only announced to you when they died on the **leader's** level
    ///   (<c>If gv14C2(l0118).M5470 = gv14C2(m0052).M5470</c>) - somebody who fell two floors up
    ///   simply vanishes from the list
    /// - <c>sub04DC</c> saves the character to file before dropping them, because their body has to
    ///   still be in the dungeon for anyone to go and fetch
    ///
    /// Returns the lines to show, in the order the original prints them.
    /// </summary>
    public List<string> RemoveTheDead(Action<Character>? save = null, Action<Character>? setDead = null)
    {
        var said = new List<string>();
        if (Members.Count <= 1) return said;

        int leaderLevel = Members[0].DungeonLevel;
        for (int i = 0; i < Members.Count && Members.Count > 1; )
        {
            var c = Members[i];
            if (c.CurHits > 0 && !c.IsDead && !c.Stoned) { i++; continue; }

            c.CurHits = 0;
            if (c.DungeonLevel == leaderLevel) said.Add($"\"{c.Name.TrimEnd()}\" has died!");
            // "If DeathState = False Then mdrcharSetDead" (FRMGROUP.BAS:333-335): hits run out without a death yet
            if (!c.IsDead) setDead?.Invoke(c);
            save?.Invoke(c);
            Members.RemoveAt(i);
            if (i < SelectedIndex) SelectedIndex--;
            // sub0956 (FRMGROUP.BAS:342) puts everyone left on the leader's floor and square after each
            // removal, so anyone found dead after the first counts as on the leader's floor
            foreach (var m in Members) { m.DungeonLevel = Members[0].DungeonLevel; m.X = Members[0].X; m.Y = Members[0].Y; }
            // the loop index does not advance: whoever shifted into this slot is examined next,
            // which is what the original's l0118 = l0118 - 1 achieves
        }
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Members.Count - 1);
        return said;
    }

    /// <summary>Select a member by position (the original uses Alt+1..4).</summary>
    public bool Select(int index)
    {
        if (index < 0 || index >= Members.Count) return false;
        SelectedIndex = index;
        return true;
    }

    /// <summary>Move the selection to the next member who can act; false if nobody can.</summary>
    public bool SelectNextAble()
    {
        for (int step = 1; step <= Members.Count; step++)
        {
            int i = (SelectedIndex + step) % Members.Count;
            var c = Members[i];
            if (!c.IsDead && !c.Stoned && c.CurHits > 0) { SelectedIndex = i; return true; }
        }
        return false;
    }

    /// <summary>
    /// Drag one member onto another in the party window - <c>lblName_DragDrop</c>
    /// (FRMGROUP.BAS:218-228). It **swaps** the two, taking each one's standing order with them;
    /// the port lifted one out and inserted it, which rotates everybody in between instead.
    /// With four members that is a visible difference: swapping 1 and 3 leaves 2 alone, rotating
    /// does not.
    ///
    /// Returns the line to print when position one changed hands, or null.
    /// </summary>
    public string? Swap(int a, int b)
    {
        if (a < 0 || a >= Members.Count || b < 0 || b >= Members.Count || a == b) return null;
        (Members[a], Members[b]) = (Members[b], Members[a]);
        if (SelectedIndex == a) SelectedIndex = b;
        else if (SelectedIndex == b) SelectedIndex = a;
        // FRMGROUP.BAS:238 - announced only when one end of the swap was the leader's place
        return a == 0 || b == 0 ? NowLeader(Members[0]) : null;
    }

    /// <summary>The old name, kept so existing callers still compile. It swaps, as the original does.</summary>
    public void MoveTo(int from, int to) => Swap(from, to);

    /// <summary>
    /// Each member's standing order in combat (the original's gv1E66 array). Combat runs on a
    /// one-second timer, so these are what actually happens each round unless you step in.
    /// </summary>
    public Dictionary<Character, CombatOrder> Orders { get; } = new();

    public CombatOrder OrderFor(Character c)
    {
        if (!Orders.TryGetValue(c, out var o)) Orders[c] = o = new CombatOrder();
        return o;
    }

    /// <summary>A fight ended, so nobody is holding an order any more (FRMGROUP.BAS:419).</summary>
    public void ClearOrders() { foreach (var o in Orders.Values) o.Clear(); }

    /// <summary>
    /// A fight has started: each character with no order yet takes up their Combat Setting from
    /// Party Options. sub0BDE -1 (MULTIMOD.BAS:158-165) touches only "If Att = False And M56C4 = -1",
    /// and keeps the group they aimed at while the monsters waited (M547A is only reset when it is
    /// below 0 or its group is empty).
    /// </summary>
    /// <returns>Those who took their setting up, in party order - the caller runs sub0BDE's item
    /// branch (MULTIMOD.BAS:174-175) for any whose setting names a buffer holding a pack slot.</returns>
    public List<Character> ApplyDefaultOrders(IReadOnlyList<Spell>? spells = null)
    {
        var took = new List<Character>();
        foreach (var c in Members)
        {
            var o = OrderFor(c);
            if (o.Kind != OrderKind.None) continue;
            int aimed = o.Target;
            o.ApplyDefault(c, spells);
            o.Target = aimed;
            took.Add(c);
        }
        return took;
    }

    /// <summary>Everyone rests on reaching the city.</summary>
    public void RestInCity() { foreach (var c in Members) c.RestInCity(); }

    /// <summary>Move the whole party to a dungeon position.</summary>
    public void PlaceAt(int level, int x, int y)
    {
        foreach (var c in Members) { c.DungeonLevel = level; c.X = x; c.Y = y; }
    }

    public void SaveAll(string savesDir)
    {
        Directory.CreateDirectory(savesDir);
        foreach (var c in Members)
            try { c.Save(Path.Combine(savesDir, Character.SafeFileName(c.Name) + ".json")); } catch { }
    }

    // ---- the party file: MDATA12 (gc1C38, MAIN.TXT:590) in the original, party-<leader>.json here ----
    //
    // A record is the leader's name and three more, keyed by the leader (MULTIMOD.BAS:400-433). It
    // is written in one place only, sub16CC's unload of the party on the way to the main menu
    // (MAINMOD2.BAS:5926-5938): joining, leaving and dying never touch it. So what comes back
    // with the leader is the party as it stood when they last left, and only if the answer then was Yes.

    private sealed class Roster { public string Leader { get; set; } = ""; public List<string> Members { get; set; } = new(); }

    private static string RosterPath(string savesDir, string leader) =>
        Path.Combine(savesDir, "party-" + Character.SafeFileName(leader) + ".json");

    /// <summary>sub16CC's box title (MAINMOD2.BAS:5928).</summary>
    public const string ReloadTitle = "Save Party?";

    /// <summary>What sub16CC says as it starts, before its box (MAINMOD2.BAS:5927).</summary>
    public const string Disbanding = "Disbanding Party...";

    /// <summary>sub16CC's question, naming the leader, <c>Chars(m00A6)</c> (MAINMOD2.BAS:5928).</summary>
    public string ReloadQuestion =>
        "Do you wish to Reload this Party when \"" + Leader.Name.TrimEnd() + "\" is loaded again?";

    /// <summary>
    /// <c>fn179B</c> (MULTIMOD.BAS:387-444). With <paramref name="remember"/> the
    /// leader's record holds the party's names (426-433). Without it, a record the leader already
    /// has is blanked, which is the same as having none, and no record is made for a leader who had
    /// none (420). True, or False when the file could not be written (its error handler,
    /// 439-443), whose line "Error '...' in MULTIMOD:mdrmulSaveParty" goes to sub04FC (440) - to
    /// <paramref name="errorLog"/> here.
    /// </summary>
    public bool SaveParty(string savesDir, bool remember, Action<string>? errorLog = null)
    {
        try
        {
            string p = RosterPath(savesDir, Leader.Name);
            if (!remember)
            {
                if (File.Exists(p)) File.Delete(p);
                return true;
            }
            Directory.CreateDirectory(savesDir);
            var r = new Roster { Leader = Leader.Name, Members = Members.Select(m => m.Name).ToList() };
            File.WriteAllText(p, JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            errorLog?.Invoke("Error '" + ex.Message + "' in MULTIMOD:mdrmulSaveParty");
            return false;
        }
    }

    /// <summary>
    /// Everything the autoload says is prefixed with this - it is a report on something that
    /// happened before you touched anything, not an answer to a command (MULTIMOD.BAS:249-267).
    /// </summary>
    public const string AutoloadPrefix = "AUTOLOAD PARTY: ";

    /// <summary>
    /// Load the leader's remembered party, the way <c>mdrmulLoadParty</c> does it
    /// (MULTIMOD.BAS:240-270). It does not simply take everybody back: each remembered member is
    /// loaded and then tested three times, and each failure is reported by name.
    ///
    /// - not in the leader's area (M59BB) and on the leader's level any more:
    ///   <c>"Sela" is not here anymore!</c> (MULTIMOD.BAS:248). The area is the character's
    ///   <see cref="Character.Area"/>, through <paramref name="areaOf"/> when the caller has the maze
    ///   to work out a save from before the port kept it; the port had compared the squares.
    /// - dead: <c>"Sela" is dead!  Can't join!</c>
    /// - <c>Abs(alignment difference) &gt; 1</c> against **any** member already in: oppositely
    ///   aligned, so Good and Evil will not travel together and Neutral will travel with either
    ///
    /// Anyone who passes is announced too - <c>"Sela" is now a member of your party.</c> - and a
    /// character whose file will not load at all gets a question rather than a refusal.
    ///
    /// This port had taken everybody back in silence, skipping only the ones whose file had gone.
    /// </summary>
    public static Party LoadFor(Character leader, string savesDir, Action<string>? say = null,
                                Func<Companion, Monster?>? monsters = null, Func<Character, int>? areaOf = null)
    {
        var party = new Party(leader);
        bool SameArea(Character a, Character b)
        {
            // every record saved in town holds M59BB = -1 (FRMCTYM.BAS:112, 132); a save written before
            // the port kept that may still carry a dungeon area at level 0, so level 0 reads as -1
            if (a.DungeonLevel <= 0 && b.DungeonLevel <= 0) return true;
            if (areaOf != null) return areaOf(a) == areaOf(b);
            if (a.Area is int x && b.Area is int y) return x == y;
            return a.X == b.X && a.Y == b.Y;   // a save from before M59BB was kept, and no maze to hand
        }
        void Said(string line) => say?.Invoke(AutoloadPrefix + line);
        // a dead leader brings nobody back (MULTIMOD.BAS:219)
        if (leader.IsDead) return party;
        try
        {
            string p = RosterPath(savesDir, leader.Name);
            if (!File.Exists(p)) return party;
            var r = JsonSerializer.Deserialize<Roster>(File.ReadAllText(p));
            if (r == null) return party;
            foreach (var name in r.Members)
            {
                if (name == leader.Name) continue;
                string f = Path.Combine(savesDir, Character.SafeFileName(name) + ".json");
                Character them;
                try { them = Character.Load(f); }
                catch
                {
                    say?.Invoke($"An error occurred auto-loading \"{name.TrimEnd()}\".  Has the character been deleted?");
                    continue;
                }

                string q = "\"" + them.Name.TrimEnd() + "\"";
                if (!SameArea(them, leader) || them.DungeonLevel != leader.DungeonLevel)
                { Said($"{q} is not here anymore!"); continue; }
                if (them.IsDead)
                { Said($"{q} is dead!  Can't join!"); continue; }
                if (party.Members.Any(m => Math.Abs((int)m.Alignment - (int)them.Alignment) > 1))
                { Said($"{q} is oppositely aligned to one or more members of the party!"); continue; }

                if (party.Add(them))
                {
                    Said($"{q} is now a member of your party.");
                    // then sub154A (MULTIMOD.BAS:266), whose mdrcharGetCarry (MULTIMOD.BAS:380) settles
                    // the newcomer's fallen companions; its own lines carry no AUTOLOAD prefix
                    if (monsters != null)
                        foreach (string line in CompanionCarry.Run(them, false, monsters)) say?.Invoke(line);
                }
            }
        }
        catch { }
        return party;
    }
}
