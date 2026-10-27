using System.Reflection;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The Cartographer mod and the Automap hooks under it (API version 13, docs/MODDING.md "The Automap"):
/// nothing hooked with mods off; the menu (the mod's items after the game's own first item), colouring,
/// waypoints picked across levels, the marks (tints, frames, the hover's arrow on the same level only),
/// a click going to the destination and highlighting it, cartographer.json's round trip and a garbled
/// file moved aside; the square under a point (AutomapLayout.SquareAt); and the hooks guarded - a mod
/// throwing in its marks or in a pick's answer is disabled. The window itself is Mordor.UiCheck's.
/// </summary>
public static partial class ModProbe
{
    /// <summary>A stand-in for the open Automap: the level shown, a pick waiting, the refreshes asked for.</summary>
    private sealed class FakeAutomap
    {
        public int Shown = 1, Deepest = 5, Refreshes;
        public (string Prompt, Action<AutomapSquare?> Done)? Pick;
        public readonly AutomapView View;

        public FakeAutomap()
        {
            View = new AutomapView(() => Shown, () => Deepest, lv => lv >= 1 && lv <= Deepest ? (30, 30) : (0, 0),
                                   lv => Shown = Math.Clamp(lv, 1, Deepest), () => Refreshes++,
                                   (p, d) => { Answer(null); Pick = (p, d); }, () => Answer(null), () => Pick != null);
        }

        /// <summary>The player answers a waiting pick: a square clicked, or null (Escape, the right button).</summary>
        public void Answer(AutomapSquare? s)
        {
            var p = Pick;
            Pick = null;
            p?.Done(s);
        }
    }

    private static string Hex((byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static void CartographerChecks(string scratch, string bundled)
    {
        Console.WriteLine("Cartographer: the manifest, and mods off:");
        string dir = Path.Combine(scratch, "carto");
        Directory.CreateDirectory(dir);
        var log = new List<string>();
        var off = Host(Path.Combine(dir, "off.ini"), log, bundled);
        var cm = off.Discovered.FirstOrDefault(d => d.Id == "Cartographer");
        Check("Cartographer manifest found, no problem", cm != null && cm.Problem == null, true);
        Check("its name, version and one-line description", $"{cm?.Manifest.Name} {cm?.Manifest.Version}: {cm?.Manifest.Description}",
              "Cartographer 1.0.0: Mark squares on the automap with colours and waypoints");
        Check("no toggles, no choices, no settings window", $"{cm?.Manifest.Settings.Count} {cm?.Manifest.Choices.Count} {cm?.Manifest.SettingsWindow == null}", "0 0 True");
        Check("not enabled by default (off until ticked, as the others)", off.IsEnabled("Cartographer"), false);
        var fake = new FakeAutomap();
        var sq = new AutomapSquare(1, 2, 3);
        Check("mods off: no Automap hook, no menu (the right button re-plots at once), no marks, nothing told",
              $"{off.AutomapHooked} {off.AutomapMenu(fake.View, sq).Count} {off.AutomapMarks(fake.View).Count} {off.AutomapClick(fake.View, sq)}", "False 0 0 False");
        var none = ModHost.Empty();
        none.AutomapHover(fake.View, sq);
        Check("no host at all: the same", $"{none.AutomapHooked} {none.AutomapMenu(fake.View, sq).Count} {none.AutomapMarks(fake.View).Count} {none.AutomapClick(fake.View, sq)}", "False 0 0 False");
        Check("mods off: no cartographer.json written", File.Exists(Path.Combine(dir, "cartographer.json")), false);
        Check("the Automap asks the host before anything of its own: right button, left, hover",
              PortSource("src/Mordor.App", "AutomapWindow.axaml.cs") is var aw
              && aw.Contains("if (!map.OfferMenu(e)) map.RedrawLevel();")
              && aw.Contains("if (_pick == null && !ModHost.Active.AutomapHooked) return;")
              && aw.Contains("if (!host.AutomapHooked || SquareAt(e) is not { } square) return false;"), true);

        // ---- the square under a point (the Automap's own geometry) ----
        Console.WriteLine("Cartographer: the square under a point on picMaze (AutomapLayout.SquareAt):");
        Check("the top left pixel is the level's top row: x 0, y the last", AutomapLayout.SquareAt(0, 0, 30, 30), (0, 29));
        Check("pixel 13 still the first square, 14 (the shared edge) the next east and south", $"{AutomapLayout.SquareAt(13.9, 13.9, 30, 30)} {AutomapLayout.SquareAt(14, 14, 30, 30)}", "(0, 29) (1, 28)");
        Check("the bottom right pixel (picMaze is squares * 14 + 1) held to the level", AutomapLayout.SquareAt(30 * 14, 30 * 14, 30, 30), (29, 0));
        Check("a square's middle", AutomapLayout.SquareAt(5 * 14 + 7, (29 - 8) * 14 + 7, 30, 30), (5, 8));

        // ---- the mod loaded ----
        Console.WriteLine("Cartographer: the mod loaded - the menu, colours, waypoints and marks:");
        string onIni = Path.Combine(dir, "settings.ini");
        new ModSettingsStore(onIni).SetEnabled("Cartographer", true);
        var on = Host(onIni, log, bundled);
        var lm = on.Loaded.FirstOrDefault(l => l.Id == "Cartographer");
        Check("loaded, not faulted, lifecycle", lm != null && !lm.Faulted && lm.SwitchesLive, true);
        if (lm == null) return;
        Check("its Automap hooks are on", on.AutomapHooked, true);
        Check("no window, no toolbar button, no other slot", $"{on.Windows.Count()} {on.ShownPartyColumns().Count} {on.ObjectsCommands.Count()}", "0 0 0");

        string Menu(AutomapSquare s) => string.Join(" | ", on.AutomapMenu(fake.View, s).Select(p =>
            p.Item.IsSeparator ? "-" : p.Item.Label + (p.Item.Swatch is { } c ? " " + Hex(c) : "") + (p.Item.IsEnabled ? "" : " (greyed)")));
        void Choose(AutomapSquare s, string label)
        {
            var p = on.AutomapMenu(fake.View, s).FirstOrDefault(x => x.Item.Label == label);
            if (p.Item == null) { Check($"the menu has {label}", false, true); return; }
            on.RunAutomapItem(p.Mod, p.Item);
        }
        string Marks() => string.Join(" ", on.AutomapMarks(fake.View).Select(k =>
            $"{k.Kind}({k.X},{k.Y}{(k.Kind == AutomapMarkKind.Arrow ? $"->{k.ToX},{k.ToY}" : "")}){Hex(k.Colour)}{(k.Bold ? "!" : "")}"));

        Check("a plain square: the four colours with their swatches, No colour greyed, then Add waypoint...",
              Menu(sq), "Blue #4E79A7 | Orange #F28E2B | Red #E15759 | Purple #BC13FE | No colour (greyed) | - | Add waypoint... #EDC948");
        Check("nothing marked: no marks", Marks(), "");
        Choose(sq, "Blue");
        string file = Path.Combine(dir, "cartographer.json");
        Check("Blue: the square tinted Tableau's blue", Marks(), "Tint(2,3)#4E79A7");
        Check("... written at once to cartographer.json beside settings.ini (the saves folder, DataDirectory)", File.Exists(file), true);
        Check("... No colour no longer greyed", Menu(sq).Contains("No colour (greyed)"), false);
        Choose(sq, "Red");
        Check("Red over it: one tint, red", Marks(), "Tint(2,3)#E15759");
        fake.Shown = 2;
        Check("another level shows none of level 1's", Marks(), "");
        fake.Shown = 1;
        Choose(sq, "No colour");
        Check("No colour: gone", Marks(), "");
        Choose(new AutomapSquare(1, 4, 4), "Purple");
        Choose(new AutomapSquare(1, 6, 4), "Orange");
        Check("two more squares, each its own colour", Marks(), "Tint(4,4)#BC13FE Tint(6,4)#F28E2B");

        // a waypoint picked on another level: the level bar pages the map, a click takes the square
        var src = new AutomapSquare(1, 2, 3);
        Choose(src, "Add waypoint...");
        Check("Add waypoint...: a pick waits, its prompt for the status line", fake.Pick?.Prompt, "Click the destination square (any level). Esc cancels.");
        fake.View.ShowLevel(3);
        var dest = new AutomapSquare(3, 10, 11);
        fake.Answer(dest);
        Check("paged to level 3 and the destination clicked: the waypoint kept", fake.Pick == null && File.ReadAllText(file).Contains("\"ToLevel\": 3"), true);
        Check("level 3 (the destination's) shows nothing of it: the destination is not marked", Marks(), "");
        fake.Shown = 1;
        Check("level 1: the source tinted and framed yellow, apart from the four colours",
              Marks(), "Tint(4,4)#BC13FE Tint(6,4)#F28E2B Tint(2,3)#EDC948 Frame(2,3)#EDC948");
        Check("its menu: Change waypoint... and Remove waypoint in place of Add",
              Menu(src), "Blue #4E79A7 | Orange #F28E2B | Red #E15759 | Purple #BC13FE | No colour (greyed) | - | Change waypoint... #EDC948 | Remove waypoint");
        Choose(src, "Blue");
        Check("a colour on a waypoint's source: the colour's tint, the yellow frame kept", Marks(),
              "Tint(2,3)#4E79A7 Tint(4,4)#BC13FE Tint(6,4)#F28E2B Frame(2,3)#EDC948");

        // hovering: the source highlighted; an arrow only to a destination on the same level
        on.AutomapHover(fake.View, src);
        Check("hovering a source whose destination is on another level: highlighted, no arrow",
              Marks().EndsWith("Frame(2,3)#EDC948 Frame(2,3)#FFFFFF!", StringComparison.Ordinal) && !Marks().Contains("Arrow"), true);
        var near = new AutomapSquare(1, 5, 5);
        Choose(near, "Add waypoint...");
        fake.Answer(new AutomapSquare(1, 8, 9));
        on.AutomapHover(fake.View, near);
        Check("hovering one whose destination is on the same level: highlighted, and the arrow to it",
              Marks().EndsWith("Frame(5,5)#FFFFFF! Arrow(5,5->8,9)#EDC948", StringComparison.Ordinal), true);
        on.AutomapHover(fake.View, new AutomapSquare(1, 8, 9));
        Check("hovering the destination itself: nothing more (it is not marked)", Marks().Contains("#FFFFFF") || Marks().Contains("Arrow"), false);
        on.AutomapHover(fake.View, new AutomapSquare(1, 6, 4));
        Check("hovering a plain coloured square: no highlight", Marks().Contains("#FFFFFF"), false);
        on.AutomapHover(fake.View, null);
        Check("the pointer off the map: no highlight, no arrow", Marks().Contains("#FFFFFF") || Marks().Contains("Arrow"), false);
        on.AutomapHover(fake.View, src);
        fake.Shown = 2;
        Check("a hover on level 1 draws nothing on level 2", Marks(), "");
        fake.Shown = 1;
        on.AutomapHover(fake.View, null);

        // clicking a source: to the destination's level, the destination highlighted until the next click
        Check("a click on a plain square: nothing done", on.AutomapClick(fake.View, new AutomapSquare(1, 6, 4)), false);
        Check("a click on a source: done", on.AutomapClick(fake.View, src), true);
        Check("... the map on the destination's level", fake.Shown, 3);
        Check("... the destination highlighted there", Marks(), "Frame(10,11)#FFFFFF!");
        Check("... the next click lets the highlight go", $"{on.AutomapClick(fake.View, new AutomapSquare(3, 1, 1))} [{Marks()}]", "True []");
        Check("... and a click with nothing to let go does nothing", on.AutomapClick(fake.View, new AutomapSquare(3, 1, 1)), false);
        fake.Shown = 1;

        // a pick given up, or answered with the source itself, changes nothing
        Choose(src, "Change waypoint...");
        fake.Answer(null);
        Check("Change waypoint... given up (Escape, the right button): the old destination kept", File.ReadAllText(file).Contains("\"ToX\": 11"), true);
        Choose(src, "Change waypoint...");
        fake.Answer(src);
        Check("... answered with the source square itself: kept too", File.ReadAllText(file).Contains("\"ToX\": 11"), true);
        Choose(src, "Change waypoint...");
        Choose(near, "Change waypoint...");
        Check("a second pick answers the first with nothing", fake.Pick != null && File.ReadAllText(file).Contains("\"ToX\": 11"), true);
        fake.Answer(new AutomapSquare(2, 1, 2));
        Check("... and the second takes the square", File.ReadAllText(file).Contains("\"ToLevel\": 2"), true);
        Choose(near, "Remove waypoint");
        Check("Remove waypoint: its frame gone, the source's menu back to Add", $"{Marks().Contains("Frame(5,5)")} {Menu(near).EndsWith("Add waypoint... #EDC948", StringComparison.Ordinal)}", "False True");

        // ---- cartographer.json ----
        Console.WriteLine("Cartographer: cartographer.json:");
        string json = File.ReadAllText(file).Replace("\r", "");
        Check("the file counts squares from one, as the Automap's readout does",
              json.Contains("\"Level\": 1,\n      \"X\": 3,\n      \"Y\": 4,\n      \"Colour\": \"Blue\"")
              && json.Contains("\"ToLevel\": 3,\n      \"ToX\": 11,\n      \"ToY\": 12"), true);
        var asm = Assembly.LoadFrom(Path.Combine(bundled, "Cartographer", "Mordor.Mod.Cartographer.dll"));
        var notesType = asm.GetType("Mordor.Mod.Cartographer.MapNotes", throwOnError: true)!;
        var colourType = asm.GetType("Mordor.Mod.Cartographer.MarkColour", throwOnError: true)!;
        var reported = new List<string>();
        dynamic Notes(string? path) => Activator.CreateInstance(notesType, path, (Action<string, Exception>)((w, _) => reported.Add(w)))!;
        dynamic back = Notes(file);
        Check("read back: the colours and waypoints as they were",
              $"{back.ColourCount} {back.WaypointCount} {back.ColourAt(src)} {back.ColourAt(new AutomapSquare(1, 4, 4))} {back.WaypointFrom(src)}",
              $"3 1 Blue Purple {new AutomapSquare(3, 10, 11)}");
        Check("... nothing on squares never marked", $"{back.ColourAt(new AutomapSquare(1, 9, 9)) == null} {back.WaypointFrom(near) == null}", "True True");
        // a file a person edited: an unknown colour, a level below one, a square off the map, a waypoint to itself
        string edited = Path.Combine(dir, "edited", "cartographer.json");
        Directory.CreateDirectory(Path.GetDirectoryName(edited)!);
        File.WriteAllText(edited, "{ \"Version\": 1, // a note\n \"Colours\": [ { \"Level\": 2, \"X\": 1, \"Y\": 1, \"Colour\": \"teal\" }, { \"Level\": 3, \"X\": 1, \"Y\": 1, \"Colour\": \"purple\" }, { \"Level\": 1, \"X\": 1, \"Y\": 1, \"Colour\": \"Green\" },"
                                  + " { \"Level\": 0, \"X\": 1, \"Y\": 1, \"Colour\": \"Red\" }, { \"Level\": 1, \"X\": 0, \"Y\": 1, \"Colour\": \"Red\" }, { \"Level\": 1, \"X\": 2, \"Y\": 2, \"Colour\": \"1\" } ],"
                                  + " \"Waypoints\": [ { \"Level\": 1, \"X\": 2, \"Y\": 2, \"ToLevel\": 1, \"ToX\": 2, \"ToY\": 2 }, { \"Level\": 1, \"X\": 3, \"Y\": 3, \"ToLevel\": 4, \"ToX\": 1, \"ToY\": 30 }, ], }");
        dynamic ed = Notes(edited);
        Check("a hand-edited file: the entries that make sense kept (case ignored, the old \"Teal\" read as Purple), the rest left out, the file kept",
              $"{ed.ColourCount} {ed.ColourAt(new AutomapSquare(2, 0, 0))} {ed.ColourAt(new AutomapSquare(3, 0, 0))} {ed.WaypointCount} {ed.WaypointFrom(new AutomapSquare(1, 2, 2))} {ed.MovedAside == null}",
              $"2 Purple Purple 1 {new AutomapSquare(4, 0, 29)} True");
        ed.SetColour(new AutomapSquare(1, 5, 5), (dynamic)Enum.Parse(colourType, "Red"));
        string rewritten = File.ReadAllText(edited);
        Check("... a change writes it back with the new name: Purple, no teal left",
              $"{rewritten.Contains("\"Colour\": \"Purple\"")} {rewritten.Contains("teal", StringComparison.OrdinalIgnoreCase)}", "True False");
        string garbled = Path.Combine(dir, "garbled", "cartographer.json");
        Directory.CreateDirectory(Path.GetDirectoryName(garbled)!);
        File.WriteAllText(garbled, "{ not json");
        reported.Clear();
        dynamic bad = Notes(garbled);
        string? aside = bad.MovedAside;
        Check("a garbled file: moved aside to cartographer.json.bad-<time>, the notes start empty, the log told",
              $"{bad.ColourCount} {aside != null && Path.GetFileName(aside).StartsWith("cartographer.json.bad-", StringComparison.Ordinal)} {File.Exists(garbled)} {reported.Count}",
              "0 True False 1");
        bad.SetColour(new AutomapSquare(1, 0, 0), (dynamic)Enum.Parse(colourType, "Red"));
        Check("... a change then writes a new file, the bad one kept", $"{File.Exists(garbled)} {aside != null && File.ReadAllText(aside) == "{ not json"}", "True True");
        dynamic mem = Notes(null);
        mem.SetColour(src, (dynamic)Enum.Parse(colourType, "Purple"));
        Check("with no file: kept in memory, nothing written", $"{mem.ColourAt(src)} {Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories).Length}", "Purple 0");

        // ---- switched off and on ----
        Console.WriteLine("Cartographer: switched off and on, and the hooks guarded:");
        Check("switched off on the main menu: done now", on.Switch("Cartographer", false), ModSwitchResult.Now);
        Check("... no Automap hook: the right button is the original's again, no marks", $"{on.AutomapHooked} {on.AutomapMenu(fake.View, src).Count} {on.AutomapMarks(fake.View).Count}", "False 0 0");
        Check("switched on again", on.Switch("Cartographer", true), ModSwitchResult.Now);
        Check("... its marks back, the notes kept", on.AutomapMarks(fake.View).Count > 0, true);

        // ---- guarded: a mod that throws in its marks, or in a pick's answer, is disabled ----
        string tm = Path.Combine(dir, "testmods");
        MakeTestMod(tm, "Smudge", nameof(SmudgeMod));
        MakeTestMod(tm, "Picky", nameof(PickyMod));
        string tIni = Path.Combine(dir, "test.ini");
        var ts = new ModSettingsStore(tIni);
        ts.SetEnabled("Smudge", true); ts.SetEnabled("Picky", true);
        log.Clear();
        var th = Host(tIni, log, tm);
        var smudge = th.Loaded.Single(l => l.Id == "Smudge");
        var picky = th.Loaded.Single(l => l.Id == "Picky");
        var f2 = new FakeAutomap();
        Check("a mod throwing in its marks: none drawn, the mod disabled, the reason logged",
              $"{th.AutomapMarks(f2.View).Count} {smudge.Faulted} {log.Any(l => l.Contains("mod Smudge: disabled for this session - it threw in AutomapMarks"))}", "0 True True");
        var item = th.AutomapMenu(f2.View, sq).Single();
        Check("the other's menu item is offered", item.Item.Label, "Pick");
        th.RunAutomapItem(item.Mod, item.Item);
        Check("... and starts a pick", f2.Pick?.Prompt, "Pick a square");
        f2.Answer(sq);
        Check("a pick's answer that throws: the mod disabled", $"{picky.Faulted} {picky.FaultReason}", "True Automap pick: picky");
        Check("... and with no mod left, nothing hooked", th.AutomapHooked, false);
    }
}

/// <summary>Draws on the Automap, and throws doing so: the host must disable it and draw nothing of it.</summary>
public sealed class SmudgeMod : IMod
{
    public void Load(IModContext ctx) => ctx.UI.AutomapMarks = _ => throw new InvalidOperationException("smudge");
}

/// <summary>Offers one item that asks for a square, and throws when it gets one.</summary>
public sealed class PickyMod : IMod
{
    public void Load(IModContext ctx) =>
        ctx.UI.AutomapMenu = (view, _) => new[]
        {
            new AutomapMenuItem("Pick", () => view.PickSquare("Pick a square", _ => throw new InvalidOperationException("picky"))),
        };
}
