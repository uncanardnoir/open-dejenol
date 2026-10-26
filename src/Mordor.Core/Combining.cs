namespace Mordor.Core;

/// <summary>
/// The store's Combine command, <c>sub2621</c> (FRMCTYS.BAS:616-697). It costs nothing, and it is
/// the only way to turn a handful of part-used potions into one that is worth carrying.
///
/// The original builds a list by typing slot numbers into a box one at a time, then works through
/// them: the first item in the list is the one everything else is poured into, and every other item
/// has to match it four ways. The port used to ask only that the item id and alignment matched and
/// that the other copy was not equipped, which let through two things it should not have (an
/// unidentified potion, and an item class that cannot be combined at all) and turned away nothing
/// with a reason.
/// </summary>
public static class Combining
{
    /// <summary>
    /// Why the whole operation is refused before anything is poured, or null if it may go ahead.
    /// Two of the original's three refusals abort the entire list rather than skipping one item:
    /// anything not completely identified, and a first item whose class cannot be combined.
    /// </summary>
    public static string? Refusal(IReadOnlyList<InventorySlot> chosen, Func<short, Item?> lookup, IReadOnlyList<int> slotNumbers)
    {
        // FRMCTYS.BAS:647-653 - checked for every item in the list, before anything else
        for (int i = 0; i < chosen.Count; i++)
            if (chosen[i].Known != IdLevel.All)
                return $"Item #{slotNumbers[i] + 1} Not Completely Identified!";

        // FRMCTYS.BAS:654-657 - one item on its own is not an error, it just does nothing
        if (chosen.Count < 2) return "";

        var first = lookup(chosen[0].ItemId);
        if (first == null) return "";

        // FRMCTYS.BAS:662-668 - the eight kinds, and the kind alone. Selling and pricing also ask
        // "Items(Ref).Charges > 0" first (FRMCTYS.BAS:241, 737), which StoreStock.SoldByCharge
        // does; Combine does not, so a kind whose item carries no charges combines all the same.
        if (!CombinableKind(first)) return "Item Class cannot be Combined!";
        return null;
    }

    /// <summary><c>Select Case Items(Ref).Kind: Case 23, 24, 25, 26, 27, 28, 30, 31</c> (FRMCTYS.BAS:663).</summary>
    public static bool CombinableKind(Item it) => it.Type is 23 or 24 or 25 or 26 or 27 or 28 or 30 or 31;

    /// <summary>
    /// One item's fate when it is poured into the first: the line to show, whether it went in, and
    /// whether the sum overflowed - see <see cref="Combine"/>.
    /// </summary>
    public readonly record struct Result(string Line, bool Merged, bool Overflowed = false);

    /// <summary>VB's text for run-time error 6, what the untrapped overflow at FRMCTYS.BAS:677 puts up.</summary>
    public const string OverflowError = "Overflow";

    /// <summary>
    /// Pour everything after the first into the first, and say what happened to each.
    /// FRMCTYS.BAS:669-694 tests four things in order, and **two of its messages are swapped**:
    /// a mismatched item *type* reports "is not compatible class", and a mismatched item *id*
    /// reports "is not of the same type". That is how the game reads, so that is how this reads.
    ///
    /// Pack.Charges is an Integer and the sum at FRMCTYS.BAS:677 is not checked: past 32,767 it is
    /// run-time error 6. sub2621 has no On Error, nor has cmdCommand_Click that calls it
    /// (FRMCTYS.BAS:112-120), so nothing traps it - VB puts up "Overflow" and the program ends,
    /// with nothing saved since the last save. The line for that item has already been said
    /// (676); its charges are not added, it is not emptied, and nothing after it is looked at.
    /// The last result then carries <c>Overflowed</c>, and the caller ends the game.
    /// </summary>
    public static List<Result> Combine(Character c, IReadOnlyList<InventorySlot> chosen,
                                       Func<short, Item?> lookup, IReadOnlyList<int> slotNumbers)
    {
        var said = new List<Result>();
        var target = chosen[0];
        var first = lookup(target.ItemId);
        if (first == null) return said;

        for (int i = 1; i < chosen.Count; i++)
        {
            var slot = chosen[i];
            int n = slotNumbers[i] + 1;
            // the original skips any repeat of the first slot number silently
            if (slot == target) continue;

            // a slot already poured in has Ref 0 (FRMCTYS.BAS:678), so naming it again reads item 0 -
            // the Hands - and is "not compatible class"
            var it = lookup(slot.ItemId);
            if (slot.Known != IdLevel.All)
                said.Add(new Result($"STORE: Item #{n} is not completely identified.", false));
            else if (it == null || it.Type != first.Type)
                said.Add(new Result($"STORE: Item #{n} is not compatible class.", false));
            else if (slot.ItemId != target.ItemId)
                said.Add(new Result($"STORE: Item #{n} is not of the same type.", false));
            else if (slot.Alignment != target.Alignment)
                said.Add(new Result($"STORE: Item #{n} is not compatible Alignment.", false));
            else
            {
                said.Add(new Result($"STORE: Item #{n} combined to Item #{slotNumbers[0] + 1}.", true));
                int sum = target.Charges + slot.Charges;
                if (sum > short.MaxValue || sum < short.MinValue)
                {
                    said.Add(new Result("", false, Overflowed: true));
                    return said;
                }
                target.Charges = (short)sum;
                // "Chars(Me).Pack(l0214(l0212)).Ref = 0" (FRMCTYS.BAS:678) and nothing more: the slot
                // keeps its charges, alignment, identification and flags, and a spell buffer set on
                // it still points at it - RemoveFromPack would have let the buffer go
                slot.ItemId = 0;
            }
        }
        return said;
    }
}
