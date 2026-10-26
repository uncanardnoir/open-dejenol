using Mordor.Core;

/// <summary>
/// The help's Commands page, listed against what the port's key handlers actually bind. The page
/// is a flat list of about fifty keys, which makes it the one place the interface can be checked
/// against a source rather than against a screenshot.
/// </summary>
public static class CommandProbe
{
    /// <summary>Every command the help's Commands page lists, in its own order and wording.</summary>
    public static readonly (string Key, string Does, string Where)[] Listed =
    {
        ("F1", "HelpLesson", "game"), ("F2", "Area Look", "game"), ("F3", "Character Stats", "game"),
        ("F4", "Resistance Information", "game"), ("F5", "Experience Information", "game"),
        ("F6", "Character Information", "game"), ("F7", "Guild/Abilities Information", "game"),
        ("F9", "Buffer Information", "game"), ("F10", "Game Options", "game"),
        ("F11", "Toolbar Toggle", "game"),
        ("a", "Ask character to Join", "game"), ("e", "Equip Item", "game"), ("d", "Drop Item", "game"),
        ("i", "Item Info", "game"), ("g", "Give Item/Gold", "game"), ("p", "Pickup Body", "game"),
        ("l", "Drop Body", "game"), ("s", "Swap Items", "game"),
        ("Ctrl+L", "Look in Room tab", "tab"), ("Ctrl+B", "Buffers tab", "tab"),
        ("Ctrl+M", "Miscellaneous tab", "tab"), ("Ctrl+S", "Character Stats tab", "tab"),
        ("Ctrl+R", "Resistances tab", "tab"), ("Ctrl+C", "Character Info tab", "tab"),
        ("Ctrl+G", "Guild Info tab", "tab"), ("Ctrl+I", "Items tab", "tab"),
        ("Ctrl+P", "Spells tab", "tab"), ("Ctrl+O", "Companions tab", "tab"),
        ("n", "Select next Character", "party"), ("Alt+S", "Switch to Character", "party"),
        ("Alt+O", "Party Options", "party"), ("Alt+L", "Ask character to leave", "party"),
        ("Alt+1..4", "Switch to character", "party"), ("Alt+P", "Pool party's gold", "party"),
        ("F8", "View AutoMap", "automap"), ("<", "Previous Automap Level", "automap"),
        (">", "Next Automap Level", "automap"),
        ("Esc", "Backout", "general"), ("Shift+F6", "Write CHARSTAT.TXT", "misc"),
        ("f", "Fight", "dungeon"), ("d", "Drop / Defend", "dungeon"), ("c", "Cast Spell", "dungeon"),
        ("o", "Open Chest", "dungeon"), ("1-4", "Select Monster Group", "dungeon"),
        ("u", "Use Item", "dungeon"), ("t", "Take Stairs, Chute", "dungeon"),
        ("j", "Allow monsters to join", "dungeon"), ("space", "Stop fighting", "dungeon"),
        ("Shift+0-9", "Cast/Use from Buffer", "dungeon"),
    };

    public static void Run(string dir)
    {
        Console.WriteLine($"the help's Commands page lists {Listed.Length} keys:\n");
        foreach (var g in Listed.GroupBy(c => c.Where))
        {
            Console.WriteLine($"  {g.Key}:");
            foreach (var c in g) Console.WriteLine($"    {c.Key,-10} {c.Does}");
        }
        Console.WriteLine("\nClick commands the page lists: monster label (= 1-4), companion name label (name it)");
        Console.WriteLine("Double click: automap (reset/resize), party character (switch), companion picture (stats),");
        Console.WriteLine("              item in Items tab (use), spell in book (cast), buffer entry (cast/use)");
        Console.WriteLine("Right click:  companion picture (attack it), automap (redraw)");

        var (found, total, missing) = Scan(PortTree.Root);
        Console.WriteLine($"\nof the keys with a token to look for, the source still binds {found} of {total}");
        foreach (string m in missing) Console.WriteLine($"  MISSING  {m}");
    }
    /// <summary>
    /// Whether the port still binds each of those keys. This is a scan of the key handler's source
    /// rather than a test of behaviour: the bindings live in switch statements inside an Avalonia
    /// window, and driving them needs the window. It is a weak check and it is meant to be - it
    /// catches a binding being deleted, not one being wired to the wrong thing.
    ///
    /// Every key on the help's page was traced by hand to its handler once; this keeps that from
    /// rotting silently.
    /// </summary>
    public static (int Found, int Total, List<string> Missing) Scan(string portDir)
    {
        string[] files =
        {
            System.IO.Path.Combine(portDir, "src/Mordor.App", "DungeonWindow.axaml.cs"),
            System.IO.Path.Combine(portDir, "src/Mordor.App", "CityWindow.axaml.cs"),
        };
        string src = string.Concat(files.Where(System.IO.File.Exists).Select(System.IO.File.ReadAllText));

        // what to look for in the source, per listed key
        var tokens = new Dictionary<string, string>
        {
            ["F1"] = "Key.F1", ["F2"] = "Key.F2", ["F3"] = "Key.F3", ["F4"] = "Key.F4",
            ["F5"] = "Key.F5", ["F6"] = "Key.F6", ["F7"] = "Key.F7", ["F8"] = "Key.F8",
            ["F9"] = "Key.F9", ["F10"] = "Key.F10", ["F11"] = "Key.F11",
            ["a"] = "Key.A", ["e"] = "Key.E", ["d"] = "Key.D", ["i"] = "Key.I", ["g"] = "Key.G",
            ["p"] = "Key.P", ["l"] = "Key.L", ["s"] = "Key.S", ["n"] = "Key.N",
            ["f"] = "Key.F", ["c"] = "Key.C", ["o"] = "Key.O", ["u"] = "Key.U", ["t"] = "Key.T",
            ["j"] = "Key.J", ["space"] = "Key.Space", ["Esc"] = "Key.Escape",
            ["<"] = "Key.OemComma", [">"] = "Key.OemPeriod",
            ["1-4"] = "Key.D1", ["Shift+0-9"] = "Key.D0", ["Alt+1..4"] = "KeyModifiers.Alt",
            ["Shift+F6"] = "KeyModifiers.Shift",
        };
        foreach (string c in new[] { "L", "B", "M", "S", "R", "C", "G", "I", "P", "O" })
            tokens[$"Ctrl+{c}"] = $"Key.{c} when e.KeyModifiers.HasFlag(KeyModifiers.Control)";

        var missing = new List<string>();
        int found = 0;
        foreach (var (key, token) in tokens)
            if (src.Contains(token)) found++; else missing.Add($"{key} ({token})");
        return (found, tokens.Count, missing);
    }

}
