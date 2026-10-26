namespace Mordor.Core;

/// <summary>
/// The General Store's uncurse, <c>cmdUncurse_Click</c> (FRMCTYS.BAS:296-305). Not the Seer -
/// that is FRMSEEK, who finds monsters; this is one of the three service panels down the left of
/// the store window.
///
/// It asks two questions before it will take your gold, and they read different fields: the
/// slot's curse flag, then the slot's equipped bit. This is the only place in the game that tests
/// the slot flag without also testing equipped, which is why both of its refusals can be seen in
/// play and why both of them are odd.
///
/// The flag is set by three routines and only one of them equips (see
/// <see cref="InventorySlot.SlotCursed"/>), so a slot can be flagged cursed and not worn:
///
///   bought and never worn    flag clear, not equipped  -> "Item is not cursed!", which is a lie
///   worn                     flag set, equipped        -> uncursed properly
///   found in a chest, then
///   refused by the equip
///   guards                   flag set, not equipped    -> "Item isn't equipped!"
///
/// The second refusal was recorded in FIDELITY as unreachable. It is not: <c>sub0F01</c> copies
/// the item's curse onto the slot (MONSMOD.BAS:3830) *before* asking <c>sub0F74</c> to put it on,
/// and that request is an ordinary equip which can be turned down for stats, guild or wearability.
/// </summary>
public static class Uncursing
{
    public const string NotCursed = "Item is not cursed!";
    public const string NotEquipped = "Item isn't equipped!";

    /// <summary>What the clerk says, or null when the uncurse goes ahead.</summary>
    public static string? Refusal(InventorySlot slot) =>
        !slot.SlotCursed ? NotCursed : !slot.Equipped ? NotEquipped : null;
}
