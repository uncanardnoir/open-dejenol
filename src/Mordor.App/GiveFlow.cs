using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// 'g' Give, the same in town and below (fn1521 case 71 has no dungeon test): the three questions
/// of <see cref="GiveCommand"/> and then sub14B5. Returns whether anything was handed over, so the
/// window can save and repaint.
/// </summary>
public static class GiveFlow
{
    public static async System.Threading.Tasks.Task<bool> Run(Window owner, IReadOnlyList<Character> party, Character me,
        Func<short, Item?> lookup, Func<Character, Action<int, IdLevel>>? filedFor, Action<string> say,
        Func<string, string, System.Threading.Tasks.Task> tell)
    {
        // the whole case is inside "If Ailments(mc01EC) = False" (MAINMOD2.BAS:3707): held fast, nothing
        if (me.Paralysed) return false;
        if (party.Count <= 1) { say(Party.NobodyToGiveTo); return false; }          // 3708-3711

        var offered = GiveCommand.Offered(party, me);
        int pick = await NamePicker.Ask(owner, GiveCommand.NameTitle, GiveCommand.NameLabel,
                                        offered.Select(c => c.Name.TrimEnd()).ToList(), Interact.NobodyFor("GIVE"));
        if (pick < 0) return false;                                                    // 3713-3715
        var to = offered[pick];

        string goldTag = await InteractText.Ask(owner, GiveCommand.ToTitle(to), GiveCommand.GoldLabel,
                                                GiveCommand.GoldMaxLength, t => GiveCommand.GoldAnswer(t, me.GoldOnHand));
        // CLng of what was typed: not a number throughout is fn1521's error trap, which ends the
        // command in silence (GiveCommand.GoldFromTag)
        if (GiveCommand.GoldFromTag(goldTag) is not long gold) return false;
        if (gold == -1) return false;                                                  // 3717-3719

        string itemTag = await InteractText.Ask(owner, GiveCommand.ToTitle(to), GiveCommand.ItemLabel,
                                                GiveCommand.ItemMaxLength, t => GiveCommand.ItemAnswer(t, me, lookup));
        if ((int)Math.Round(GiveCommand.Val(itemTag), MidpointRounding.ToEven) == -1) return false;   // 3721-3723
        int slot = GiveCommand.SlotFromTag(itemTag);                                   // 3724-3728

        var said = GiveCommand.Give(me, to, slot, gold, lookup, filedFor?.Invoke(to));               // 3729
        foreach (var s in said)
            if (s.Box) await tell(GiveCommand.Sorry, s.Text); else say(s.Text);
        return said.Count > 0;
    }
}
