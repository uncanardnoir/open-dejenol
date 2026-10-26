namespace Mordor.Core;

/// <summary>
/// <c>mdrcharCheckItems</c> (MAINMOD2.BAS:667-689). Walks everything a character has equipped,
/// asks <c>fn11B6</c> whether they can still use it, and takes off whatever they cannot.
///
/// It is called at three moments, and only one of them is quiet. Joining a guild calls it with
/// the announcement suppressed and then says one line for the lot -
/// <see cref="UnequippedOnJoining"/> - because a new guild can disallow a great deal at once
/// (FRMCTYG.BAS:159-161). A stat drain in a fight calls it with the announcement on, so each item
/// names itself as it comes off (MONSMOD.BAS:3190).
///
/// The port had nothing here at all: gear stayed equipped after a guild change that forbade it.
/// </summary>
public static class GearCheck
{
    /// <summary>What the Guild Hall says when joining cost you some of your gear.</summary>
    public const string UnequippedOnJoining = "GUILD: One or more items were unequipped when you joined...";

    /// <summary>
    /// One item coming off, named. The two wordings are the original's own, chosen on whether
    /// there is anybody else in the party to distinguish from (MAINMOD2.BAS:676-679).
    /// </summary>
    public static string NoLongerUsable(Character who, string itemName, bool inAParty) =>
        inAParty ? $"\"{who.Name.TrimEnd()}\" can no longer use their {itemName}!"
                 : $"You can no longer use your {itemName}!";

    /// <summary>
    /// Take off whatever can no longer be used, and return one line per item. The test is
    /// <c>fn11B6(char, ref, False, 1)</c> (MAINMOD2.BAS:671): its fourth argument skips free hands
    /// and "using too many", so what is left is the guild, the guild level and the stats -
    /// <see cref="Character.EquipProblem"/> without the item lookup. The stats are what a drain
    /// or a reversed potion trips; the guild is what joining trips.
    /// </summary>
    public static List<string> Run(Character c, Func<short, Item?> lookup, bool inAParty, bool announce)
    {
        var said = new List<string>();
        for (int i = 1; i < Character.PackSize; i++)
        {
            if (c.Pack[i] is not InventorySlot slot || !slot.Equipped || slot.ItemId == 0) continue;
            var it = lookup(slot.ItemId);
            if (it == null || c.EquipProblem(it) == null) continue;

            // fn11E2 calls sub0F74 with a *positive* slot index, so the unequip is not forced and
            // a cursed item refuses to come off (MODULE6.BAS:1728). The announcement is printed
            // before the refusal can be seen and l018E is set before the call, so the game says
            // you can no longer use the thing and then leaves it on you. Reproduced.
            if (!slot.SlotCursed) slot.Equipped = false;
            if (announce) said.Add(NoLongerUsable(c, Loot.NameFor(it, slot.Known), inAParty));
        }
        return said;
    }

    /// <summary>Whether anything came off, for the callers that only want the one line.</summary>
    public static bool Any(Character c, Func<short, Item?> lookup)
    {
        bool any = false;
        for (int i = 1; i < Character.PackSize; i++)
        {
            if (c.Pack[i] is not InventorySlot slot || !slot.Equipped || slot.ItemId == 0) continue;
            var it = lookup(slot.ItemId);
            if (it == null || c.EquipProblem(it) == null) continue;
            if (!slot.SlotCursed) slot.Equipped = false;
            any = true;   // l018E is set whether or not the item actually came off
        }
        return any;
    }
}
