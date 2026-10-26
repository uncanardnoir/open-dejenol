using System.Text.RegularExpressions;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Command buttons that take a drop (2026-10-18). In the original, dropping anything on one of
    /// Objects &amp; Spells' four commands presses it: <c>cmdCommand_DragDrop</c> is
    /// <c>cmdCommand_Click Index</c> (FRMCHAR3.BAS:252-254). These pin that it is the only command
    /// button with a DragDrop handler in any form, and that both windows wire all four. The
    /// behaviour itself is driven headless in Mordor.UiCheck.
    /// </summary>
    private static void CommandDropChecks(string dir)
    {
        Console.WriteLine("\ncommand buttons that take a drop (FRMCHAR3.BAS:252-254)");
        string root = DecompiledDir("decompiled-named");
        var handlers = new List<string>();
        if (System.IO.Directory.Exists(root))
            foreach (var f in System.IO.Directory.GetFiles(root, "*.BAS").OrderBy(f => f, StringComparer.Ordinal))
                foreach (Match m in Regex.Matches(System.IO.File.ReadAllText(f), @"^Sub (cmd\w*)_DragDrop \(", RegexOptions.Multiline))
                    handlers.Add(System.IO.Path.GetFileName(f) + ":" + m.Groups[1].Value);
        Check("every form's command buttons with a DragDrop handler: only FRMCHAR3's cmdCommand array", string.Join(", ", handlers), "FRMCHAR3.BAS:cmdCommand");
        string char3 = PortSource("decompiled-named", "FRMCHAR3.BAS").Replace("\r\n", "\n");
        Check("FRMCHAR3.BAS: it presses the button, whatever was dropped",
              char3.Contains("Sub cmdCommand_DragDrop (Index%, Source As Control, X As Single, Y As Single)\n    cmdCommand_Click Index\nEnd Sub"), true);
        Check("FRMCHAR3.BAS: a dragged pack line is selected before it is dragged",
              char3.Contains("    If  Button > False Then\n        lblItem_Click Index\n        lblItem(Index).Drag"), true);
        string city = PortSource("Mordor.App", "CityWindow.Mdi.cs"), dun = PortSource("Mordor.App", "DungeonWindow.Mdi.cs");
        string cityX = PortSource("Mordor.App", "CityWindow.axaml"), dunX = PortSource("Mordor.App", "DungeonWindow.axaml");
        Check("town and dungeon: Equip, Use, Drop and Info take a drop and press themselves",
              city.Contains("PackCommands.Wire(PackList, () => _ch, PackEquipBtn, PackUseBtn, PackDropBtn, PackInfoBtn);")
              && dun.Contains("PackCommands.Wire(PackList, () => _ch, SheetEquipBtn, SheetUseBtn, SheetDropBtn, SheetInfoBtn);")
              && new[] { "Equip", "Use", "Drop", "Info" }.All(n => cityX.Contains($"x:Name=\"Pack{n}Btn\"") && cityX.Contains($"Click=\"OnPack{n}\"")
                                                              && dunX.Contains($"x:Name=\"Sheet{n}Btn\"") && dunX.Contains($"Click=\"OnSheet{n}\"")), true);
    }
}
