using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// frmParty's "UPDATE" (FRMGROUP.BAS:141-146, modUpdateCharacterList 310-389) unloads everyone at
    /// nought while more than one is loaded. Every place the original sends it, and the port's answer
    /// at each: the prune (DungeonWindow.PruneTheDead below ground, CityWindow.PruneTheDead in town).
    /// </summary>
    private static void PartyUpdateChecks()
    {
        Console.WriteLine("\nfrmParty's UPDATE prunes the dead wherever the original sends it (FRMGROUP.BAS:321-346)");
        string N(string s) => s.Replace("\r\n", "\n");
        string m2 = N(PortSource("decompiled-named", "MAINMOD2.BAS"));
        string mz = N(PortSource("decompiled-named", "MAZEMOD.BAS"));
        string mo = N(PortSource("decompiled-named", "MONSMOD.BAS"));
        string mu = N(PortSource("decompiled-named", "MULTIMOD.BAS"));
        string grp = N(PortSource("decompiled-named", "FRMGROUP.BAS"));

        Check("FRMGROUP.BAS: UPDATE is modUpdateCharacterList, which unloads the dead while more than one is loaded",
              grp.Contains("            Case \"UPDATE\"\n                modUpdateCharacterList")
              && grp.Contains("            If  Chars(l0118).Hits <= False And UBound(Chars) > m0052 Then")
              && grp.Contains("                mdrcharUnload l0118"), true);
        // every sender: a direct write of frmParty's lblCall, or sub0941 "UPDATE", or sub1776
        Check("the original's senders: sub0930, mdrmazShowMaze twice, the chest, sub0908, sub154A, the round, sub1776",
              m2.Contains("    If  UBound(Chars) > False Then\n        frmParty!lblCall = \"UPDATE\"\n        Exit Sub")
              && mz.Contains("            sub0941 \"UPDATE\"\n") && mz.Contains("        gv1A84 = m01D0\n        frmParty!lblCall = \"UPDATE\"")
              && mz.Contains("        sub0941 \"UPDATE\"\n")
              && mo.Contains("    If  UBound(Chars) > False Then frmParty!lblCall = \"UPDATE\"")
              && mu.Contains("            frmParty!lblCall = \"UPDATE\"\n            sub1776\n            sub1536 gv1A84")
              && mu.Contains("    mdrcharGetCarry UBound(Chars)\n    frmParty.Show\n    frmParty!lblCall = \"UPDATE\"")
              && mu.Contains("        If  l00E4 Then frmParty!lblCall = \"UPDATE\"")
              && mu.Contains("        mdrcharUnload pv014E\n        sub1776"), true);
        Check("... and sub154A is a join's and each autoloaded member's, sub0908 a square's and a chest's",
              m2.Contains("                        sub154A\n") && mu.Contains("                    sub154A\n")
              && mz.Contains("    sub0908\n") && mo.Contains("                            sub0908\n"), true);

        string dw = N(PortSource("src/Mordor.App", "DungeonWindow.axaml.cs"));
        string city = N(PortSource("src/Mordor.App", "CityWindow.axaml.cs"));
        string svc = N(PortSource("src/Mordor.App", "CityWindow.Services.cs"));
        string settle = Between(dw, "private bool SettleArea()", "private void SameAreaReload()");
        Check("dungeon: an area or level change prunes before its save (MAZEMOD.BAS:819, 827)",
              settle.IndexOf("PruneTheDead();") is int p && p > 0 && p < settle.IndexOf("if (wiped) {"), true);
        string reload = Between(dw, "private void SameAreaReload()", "private bool LoadArea(");
        Check("dungeon: so does the forced reload in one area", reload.IndexOf("PruneTheDead();") is int r && r > 0 && r < reload.IndexOf("SaveChar();"), true);
        Check("dungeon: a teleporter's and a chute's sub0908, once anyone has gone",
              System.Text.RegularExpressions.Regex.Matches(dw, @"int sizeBefore = _party\.Size;").Count == 2
              && dw.Contains("if (_party.Size < sizeBefore) PruneTheDead();")
              && dw.Contains("        if (_party.Size < sizeBefore)\n        {\n            foreach (string line in lines) Log(line);\n            lines.Clear();\n            PruneTheDead();"), true);
        string chest = Between(dw, "private void ChestTeleport(Character opener, Teleported to)", "// alone: the character goes");
        Check("dungeon: the chest's Teleporter trap's sub0908 (MONSMOD.BAS:3590)",
              chest.IndexOf("PruneTheDead();") is int c && c > chest.IndexOf("_party.Remove(opener);"), true);
        string join = Between(dw, "private async void AskToJoin()", "private async System.Threading.Tasks.Task<bool> OfferPendingRaise(");
        Check("dungeon: a join's sub154A, after the carry", join.IndexOf("PruneTheDead();") > join.IndexOf("CompanionCarry.Run(who"), true);
        string leave = Between(dw, "private async void AskToLeave()", "private int MonsterGuildLevel(");
        Check("dungeon: a leave's sub1776", leave.IndexOf("PruneTheDead();") > leave.IndexOf("leaving.Save("), true);

        Check("town: the prune is the dungeon's, said through the Messages window, unheard during the autoload",
              svc.Contains("private void PruneTheDead(bool quiet = false)") && svc.Contains("var said = _party.RemoveTheDead(")
              && svc.Contains("if (!quiet) foreach (string line in said) Log(line);"), true);
        Check("town: the autoload, a join and a leave send it",
              city.Contains("            PruneTheDead(quiet: true);")
              && Between(city, "    private async void AskToJoin()", "    /// <summary>").Contains("        PruneTheDead();")
              && Between(city, "    private async void AskToLeave()", "    /// <summary>").Contains("        PruneTheDead();"), true);
        Check("town: sub0930 in company sends it (MAINMOD2.BAS:3035-3037)",
              Between(city, "private void ArriveDeadSub0930(Character? whom = null)", "private Character PartyCopyOf(").Contains("if (_party.Size > 1) PruneTheDead();"), true);

        // the prune itself
        Character Make(string n, int hits) { var ch = Character.Create(n, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 }); ch.CurHits = hits; return ch; }
        var party = new Party(Make("Lead", 9));
        var fallen = Make("Fallen", 0);
        party.Members.Add(fallen);
        party.Members.Add(Make("Third", 4));
        var saved = new List<string>();
        var lines = party.RemoveTheDead(ch => saved.Add(ch.Name), ch => ch.IsDead = true);
        Check("the prune: the one at nought set dead, saved, said and gone; the others stay",
              (string.Join(",", party.Members.Select(m => m.Name)), fallen.IsDead, string.Join(",", saved), string.Join("|", lines)).ToString(),
              ("Lead,Third", true, "Fallen", "\"Fallen\" has died!").ToString());
        var alone = new Party(Make("Solo", 0));
        Check("the prune: the last one left is never unloaded", (alone.RemoveTheDead().Count, alone.Size).ToString(), "(0, 1)");

        Section(() => ControlPanelUnloadChecks());
    }

    /// <summary>
    /// frmControlPanel's Form_Unload: "If Chars(m008C).DeathState Then sub0930 m008C", m008C = 0
    /// (FRMCONTR.BAS:27, 245-247) - however the panel is shut, a dead first member gets sub0930 again.
    /// </summary>
    private static void ControlPanelUnloadChecks()
    {
        Console.WriteLine("\nshutting the Control Panel runs sub0930 for a dead first member (FRMCONTR.BAS:245-247)");
        string f = PortSource("decompiled-named", "FRMCONTR.BAS").Replace("\r\n", "\n");
        Check("FRMCONTR.BAS: Form_Unload, and m008C is 0",
              f.Contains("Sub Form_Unload (Cancel As Integer)\n    If  Chars(m008C).DeathState Then sub0930 m008C\nEnd Sub")
              && f.Contains("\nConst m008C = 0 ' &H0%\n"), true);
        string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs").Replace("\r\n", "\n");
        string cp = Between(city, "private async void ShowControlPanel(", "    /// <summary>");
        Check("the port, town: after the panel is shut, sub0930 for the first member when dead",
              cp.IndexOf("if (Leader.IsDead) ArriveDeadSub0930(Leader);") > cp.IndexOf("await panel.ShowDialog(this);"), true);
        string arrive = Between(city, "private void ArriveDeadSub0930(Character? whom = null)", "private Character PartyCopyOf(");
        Check("the port, town: sub0930's head is the one it is called for", arrive.Contains("(whom ?? _ch).Sub0930Head(Lookup);"), true);
        string dw = PortSource("src/Mordor.App", "DungeonWindow.Mdi.cs").Replace("\r\n", "\n");
        string dcp = Between(dw, "private async void OnControlPanel(", "\n    }");
        Check("the port, dungeon: after the panel is shut, sub0930 for the first member when dead",
              dcp.IndexOf("if (!_ended && Leader.IsDead) Sub0930(Leader);") > dcp.IndexOf("await panel.ShowDialog(DialogOwner);"), true);
    }
}
