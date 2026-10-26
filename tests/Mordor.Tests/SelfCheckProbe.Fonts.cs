using System.Text.RegularExpressions;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The Options window's fonts, applied as the original applies them (docs/FIDELITY.md, "The
    /// Options fonts are applied"): the rule itself (<see cref="FontRule"/>: sub0784, sub07C5,
    /// sub0802), the port's table of design fonts (<see cref="DesignFonts"/>) against each control's
    /// font in MORDOR.EXE's form data, each form's pass against its own Form_Load and Form_Resize,
    /// every control of <see cref="ServiceForms"/> covered, and the port's UI source free of fonts
    /// of its own outside Mordor.App's UiFonts but for the windows listed as the port's own.
    /// </summary>
    private static void FontChecks(string dir)
    {
        Console.WriteLine("\nthe Options fonts are applied as sub0784, sub07C5 and sub0802 apply them (MAINMOD.BAS:1750-2100)");

        // ---- the rule
        var def = new GameOptions();
        var mine = new GameOptions { ProportionalFont = "Consolas", ProportionalSize = 10, NonProportionalFont = "Segoe UI", NonProportionalSize = 10 };
        string R(GameOptions o, DesignFont d, FontPass p) { var r = FontRule.Resolve(o, d, p); return $"{r.Face} {r.Points} = {r.Pixels}px"; }
        Check("the defaults are Courier New 9 and MS Sans Serif 8 (MAINMOD.BAS:1256-1265)", FontRule.IsDefault(def), true);
        Check("defaults, a zooming form's own font: NonProSize 8, 11 pixels", R(def, DesignFont.Default, FontPass.RestyleAndZoom), "MS Sans Serif 8 = 11px");
        Check("defaults, a zooming form's Courier New: ProSize 9, 12 pixels", R(def, DesignFonts.StoreCounts, FontPass.RestyleAndZoom), "Courier New 9 = 12px");
        Check("defaults, a form sub0784 alone styles keeps its design (the early exit)", R(def, DesignFonts.StoreCounts, FontPass.Restyle), "Courier New 8.25 = 11px");
        Check("defaults, a Tag of \":12\" keeps its 12 points through the zoom", R(def, DesignFonts.Welcome, FontPass.RestyleAndZoom), "MS Sans Serif 12 = 16px");
        Check("defaults, 12 points with no Tag is NonProSize after sub0802 (frmHall's heading)", R(def, DesignFonts.HallHeading, FontPass.SizeOnly), "MS Sans Serif 8 = 11px");
        Check("defaults, Arial keeps its face at NonProSize", R(def, DesignFonts.StoreSelected, FontPass.RestyleAndZoom), "Arial 8 = 11px");
        Check("chosen fonts: Courier New becomes Pro", R(mine, DesignFonts.StoreCounts, FontPass.RestyleAndZoom), "Consolas 10 = 13px");
        Check("chosen fonts: MS Sans Serif becomes NonPro", R(mine, DesignFont.Default, FontPass.RestyleAndZoom), "Segoe UI 10 = 13px");
        Check("chosen fonts: a form sub0784 alone styles takes them too", R(mine, DesignFont.Default, FontPass.Restyle), "Segoe UI 10 = 13px");
        Check("chosen fonts: Arial is never renamed, but sized", R(mine, DesignFonts.StoreSelected, FontPass.RestyleAndZoom), "Arial 10 = 13px");
        Check("chosen fonts: sub0802 sizes a form and never renames it", R(mine, DesignFont.Default, FontPass.SizeOnly), "MS Sans Serif 10 = 13px");
        var odd = new GameOptions { ProportionalFont = "MS Sans Serif", ProportionalSize = 9, NonProportionalFont = "Arial", NonProportionalSize = 8 };
        Check("sub0784's second test reads the name its first just set", R(odd, DesignFonts.StoreCounts, FontPass.Restyle), "Arial 8 = 11px");
        // a size of nothing or less (a hand-edited MORDOR.INI) is an error VB's On Error Resume Next
        // steps over: the control keeps the size it had, the face is still set, and nothing is drawn
        // at no size (FormattedText throws on one)
        var zero = new GameOptions { ProportionalFont = "Consolas", ProportionalSize = 0, NonProportionalFont = "Segoe UI", NonProportionalSize = -3 };
        Check("ProSize=0: sub0784 sets the face and the size stays", R(zero, DesignFonts.StoreCounts, FontPass.Restyle), "Consolas 8.25 = 11px");
        Check("NonProSize=-3: sub0784 sets the face and the size stays", R(zero, DesignFont.Default, FontPass.Restyle), "Segoe UI 8.25 = 11px");
        Check("ProSize=0 through the sizing pass too: the design size stays", R(zero, DesignFonts.StoreCounts, FontPass.RestyleAndZoom), "Consolas 8.25 = 11px");
        Check("NonProSize=-3 through sub0802: the design size stays", R(zero, DesignFonts.HallHeading, FontPass.SizeOnly), "MS Sans Serif 12 = 16px");
        Check("a size under half a pixel is still one pixel", FontRule.Pixels(0.1) + "|" + FontRule.Pixels(0) + "|" + FontRule.Pixels(-5), "1|1|1");
        string ui0 = PortSource("src/Mordor.App", "UiFonts.cs");
        Check("every FormattedText of UiFonts is given at least one pixel",
              ui0.Contains("size = Math.Max(1, size);") && ui0.Contains("Math.Max(1, SizeOf(pass, font)), null);")
              && ui0.Contains("Math.Max(1, r.Pixels), null);")
              && Regex.Matches(ui0, @"new FormattedText\(").Count == 4, true);

        // [Fonts] as MAINMOD.BAS:1252-1265 reads it: an empty face is the default one; the size as read
        GameOptions Fonts(params string[] lines)
        {
            var g = new GameOptions();
            g.ReadFonts(new IniLayers(Ini.Parse(lines), Ini.Parse(Array.Empty<string>())));
            return g;
        }
        string Four(GameOptions g) => $"{g.ProportionalFont} {g.ProportionalSize}|{g.NonProportionalFont} {g.NonProportionalSize}";
        Check("[Fonts] missing: the defaults", Four(Fonts()), "Courier New 9|MS Sans Serif 8");
        Check("an empty Pro= and NonPro= are the default faces (MAINMOD.BAS:1256-1265)",
              Four(Fonts("[Fonts]", "Pro=", "ProSize=9", "NonPro=  ", "NonProSize=8")), "Courier New 9|MS Sans Serif 8");
        Check("... and the sizes are not read again: an empty face keeps the size that was read",
              Four(Fonts("[Fonts]", "Pro=", "ProSize=12", "NonPro=", "NonProSize=10")), "Courier New 12|MS Sans Serif 10");
        Check("a face that is there is kept", Four(Fonts("[Fonts]", "Pro=Consolas", "ProSize=10", "NonPro=Arial", "NonProSize=9")), "Consolas 10|Arial 9");

        Check("points to pixels as Windows rounds them",
              string.Join(" ", new[] { 7.5, 8, 8.25, 9, 9.75, 10, 12 }.Select(p => FontRule.Pixels(p))), "10 11 11 12 13 13 16");

        // ---- the table against the form data
        var forms = FormFonts.ReadAll(dir).ToDictionary(f => f.Resource);
        var tableForms = typeof(DesignFonts).GetFields().Select(f => f.GetValue(null)).OfType<DesignFonts.Form>().ToList();
        foreach (var f in tableForms)
            Check($"{f.Name} is RCDATA {f.Resource}", forms.TryGetValue(f.Resource, out var ff) ? ff.Name : "missing", f.Name);
        static string Seen(FormFonts.Ctl c) =>
            new DesignFont(c.Face, c.Size, c.Font == null || (c.Style & 1) != 0, c.Tag).ToString();
        foreach (var e in DesignFonts.All)
        {
            var c = forms.TryGetValue(e.Form.Resource, out var ff) ? ff.Controls.FirstOrDefault(x => x.Key == e.Control) : null;
            Check($"{e.Form.Name}.{e.Control}", c == null ? "missing" : Seen(c), e.Font.ToString());
        }

        // each form's pass, from its own module: sub0784 restyles; sub07C5, sub0760 (by way of
        // sub07ED) and sub0802 size
        var modules = new Dictionary<string, string>
        {
            ["frmCharStat1"] = "FRMCHAR1", ["frmCharStat3"] = "FRMCHAR3", ["frmCharStat4"] = "FRMCHAR4", ["frmMain"] = "FRMMAIN",
            ["frmCityMain"] = "FRMCTYM", ["frmCityStore"] = "FRMCTYS", ["frmCityBank"] = "FRMCITYB", ["frmCityGuild"] = "FRMCTYG",
            ["frmDungeonMain"] = "FRMDUNM", ["frmInteract"] = "FRMINTER", ["frmCityMorgue"] = "FRMMORG", ["frmExchange"] = "FRMEXCHA",
            ["frmAutoMap"] = "FRMAUTOM", ["frmParty"] = "FRMGROUP", ["frmPartyOptions"] = "FRMPINFO", ["frmCitySeer"] = "FRMSEEK",
            ["frmCityCStore"] = "FRMCITYC", ["frmGuildLog"] = "FRMGLDL", ["frmLibrary"] = "FRMLIBRA", ["frmHall"] = "FRMHALL",
        };
        foreach (var f in tableForms)
        {
            if (!modules.TryGetValue(f.Name, out var bas)) { Check($"{f.Name}: its module is known", false, true); continue; }
            string src = PortSource("decompiled-named", bas + ".BAS");
            bool restyles = Regex.IsMatch(src, @"^\s*sub0784 Me\b", RegexOptions.Multiline);
            bool sizes = Regex.IsMatch(src, @"^\s*sub0(7C5|760|7ED|802) (Me|p)", RegexOptions.Multiline);
            // frmMain is restyled by no routine at all: the port gives its toolbar readouts the
            // Options fonts as sub0784 would (a deviation, docs/FIDELITY.md)
            string want = f.Name == "frmMain" ? "neither" : f.Pass switch
            {
                FontPass.Restyle => "sub0784",
                FontPass.RestyleAndZoom => "sub0784 and a sizing pass",
                _ => "a sizing pass",
            };
            string got = restyles && sizes ? "sub0784 and a sizing pass" : restyles ? "sub0784" : sizes ? "a sizing pass" : "neither";
            Check($"{f.Name}: {bas}.BAS runs", got, want);
        }

        // every control the service forms draw is its form's default font or in the table
        foreach (var t in typeof(ServiceForms).GetNestedTypes())
        {
            if (t.GetField("Resource")?.GetValue(null) is not int res || t.GetField("Controls")?.GetValue(null) is not ServiceForms.Control[] ctls) continue;
            if (!forms.TryGetValue(res, out var ff)) { Check($"RCDATA {res} for {t.Name}", "missing", "there"); continue; }
            var table = tableForms.FirstOrDefault(x => x.Resource == res);
            var loose = new List<string>();
            foreach (var c in ctls)
            {
                string key = c.Index >= 0 ? $"{c.Name}({c.Index})" : c.Name;
                var seen = ff.Controls.FirstOrDefault(x => x.Key == key);
                if (seen == null || (seen.Face == DesignFont.NonProFace && seen.Size == 8.25 && seen.Tag == null)) continue;
                if (table == null || !DesignFonts.All.Any(e => e.Form == table && e.Control == key)) loose.Add($"{key} ({Seen(seen)})");
            }
            Check($"{ff.Name}: each control with a font of its own is in DesignFonts", loose.Count == 0 ? "all" : string.Join(", ", loose), "all");
        }

        // the pages take a control's own font from the table, never from a face or a size of their own
        var literal = new Regex(@"FormPage\.Label\([^;]*(font: ""|points: )");
        Check("the town's form pages name no face or size of their own (DesignFonts instead)",
              string.Join(" ", new[] { "CityWindow.axaml.cs", "CityWindow.Store.cs" }.Where(f => literal.IsMatch(PortSource("src/Mordor.App", f)))), "");

        // ---- the port's source
        string app = PortTree.At("src/Mordor.App");
        var files = System.IO.Directory.GetFiles(app, "*.cs").Concat(System.IO.Directory.GetFiles(app, "*.axaml")).ToList();
        Check("the port's UI source is there to read", files.Count > 20, true);
        // the port's own windows, not a Mordor form the Options fonts reach: each keeps its own font
        var ownWindows = new Dictionary<string, string>
        {
            ["UiFonts.cs"] = "the mechanism itself",
            ["SansStandIn.cs"] = "the mechanism itself: the faces MS Sans Serif is drawn in",
            ["AboutWindow.cs"] = "frmAbout and VbForm, its helpers: the callers on a restyled form (Exchange, Hall) put UiFonts over what they build",
            ["CharacterCreationWindow.axaml"] = "the port's own creation layout, not frmCreateChar's",
            ["CharacterCreationWindow.axaml.cs"] = "the port's own creation layout, not frmCreateChar's",
            ["ControlPanelWindow.cs"] = "the port's own Control Panel layout, not frmControlPanel's",
            ["HelpWindow.axaml"] = "the help viewer, WinHelp's and not a form",
            ["HelpWindow.axaml.cs"] = "the help viewer, WinHelp's and not a form",
            ["MainWindow.axaml"] = "the map browser, a developer's tool",
            ["MainWindow.axaml.cs"] = "the map browser, a developer's tool",
            ["MdiDemoWindow.axaml.cs"] = "a developer's demonstration",
            ["ModsWindow.cs"] = "the port's own Mods page",
            ["ModSettingsWindow.cs"] = "a mod's settings window, opened from the Mods page and in its style",
            ["GameFolderWindow.cs"] = "the port's own first-run window, up before there is a game folder whose Options UiFonts could read",
            ["OptionsWindow.cs"] = "the port's own Options layout; its two font labels show the chosen fonts themselves",
            ["OptionsDialogs.cs"] = "frmDirectory, the port's own dialog",
            ["ImportWindow.cs"] = "the port's own import dialog, opened from Options and in the style of its dialogs",
            ["OriginalBox.cs"] = "MsgBox, Windows' own box",
            ["TitleWindow.axaml"] = "the main menu, drawn to its own screenshot",
            ["TitleWindow.axaml.cs"] = "the main menu, drawn to its own screenshot",
            ["Mdi.cs"] = "the child windows' title bars and their buttons, Windows' caption font and not a form's",
        };
        // lines elsewhere that are allowed their font, and why
        var ownLines = new (string File, string Text, string Why)[]
        {
            ("CityWindow.axaml", "FontFamily=\"Cascadia Mono, Consolas, monospace\"", "the frame: its title bars and the port's own boxes"),
            ("DungeonWindow.axaml", "FontFamily=\"Cascadia Mono, Consolas, monospace\"", "the frame: its title bars and the port's own boxes"),
            ("AutomapWindow.axaml", "FontFamily=\"Cascadia Mono, Consolas, monospace\"", "the window the map is lifted out of; the map itself is frmAutoMap's font"),
            ("ExchangeWindow.cs", "FontFamily = VbForm.Sans,", "the gold amount's InputBox, Windows' own"),
        };
        var fontLine = new Regex(@"FontFamily\s*=|FontSize\s*=|FontFamily=""|FontSize=""|FontSizeProperty|FontFamilyProperty|new FontFamily\(|SetFontSize|SetFontFamily|HeaderFontSize""\]|Property=""FontSize""|Property=""FontFamily""");
        var strays = new List<string>();
        foreach (var path in files)
        {
            string name = System.IO.Path.GetFileName(path);
            if (ownWindows.ContainsKey(name)) continue;
            var lines = System.IO.File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (l.StartsWith("//") || l.StartsWith("///") || l.StartsWith("<!--") || !fontLine.IsMatch(l)) continue;
                if (l.Contains("UiFonts.")) continue;
                if (ownLines.Any(o => o.File == name && l.Contains(o.Text))) continue;
                strays.Add($"{name}:{i + 1}");
            }
        }
        Check("no game window sets a font of its own outside UiFonts", strays.Count == 0 ? "none" : string.Join(" ", strays), "none");
        foreach (var o in ownLines)
            Check($"{o.File} still has its allowed font ({o.Why})",
                  files.Any(p => System.IO.Path.GetFileName(p) == o.File && System.IO.File.ReadAllText(p).Contains(o.Text)), true);
        // the MsgBox stand-ins are in Windows' message font, not their frame's Cascadia Mono: the
        // town's and the dungeon's Tell and Confirm, and OriginalBox
        foreach (var (file, boxes) in new[] { ("CityWindow.axaml.cs", 2), ("DungeonWindow.axaml.cs", 2), ("OriginalBox.cs", 1) })
            Check($"{file}: its {boxes} MsgBox stand-in(s) take UiFonts.MessageBox",
                  Regex.Matches(PortSource("src/Mordor.App", file), @"UiFonts\.MessageBox\(win\)").Count, boxes);

        // every form and control the source names is in the table: a misspelt one would silently
        // be the form's default
        var named = new List<string>();
        foreach (var path in files)
        {
            string text = System.IO.File.ReadAllText(path);
            foreach (Match m in Regex.Matches(text, @"UiFonts\.Font=""([^"".]+)(?:\.([^""]+))?"""))
                named.Add(m.Groups[1].Value + (m.Groups[2].Success ? "." + m.Groups[2].Value : ""));
            foreach (Match m in Regex.Matches(text, @"DesignFonts\.(\w+), ""([^""]+)"""))
                named.Add((typeof(DesignFonts).GetField(m.Groups[1].Value)?.GetValue(null) is DesignFonts.Form f ? f.Name : m.Groups[1].Value)
                          + "." + m.Groups[2].Value);
        }
        var unknown = named.Distinct().Where(n =>
        {
            int dot = n.IndexOf('.');
            string form = dot < 0 ? n : n[..dot];
            var f = tableForms.FirstOrDefault(x => x.Name == form);
            return f == null || dot >= 0 && !DesignFonts.All.Any(e => e.Form == f && e.Control == n[(dot + 1)..]);
        }).ToList();
        Check($"the {named.Distinct().Count()} forms and controls the windows name are in DesignFonts", unknown.Count == 0 ? "all" : string.Join(", ", unknown), "all");

        // ---- the rows: the font's line and RowPad, in every list the port draws
        string Src(string f) => PortSource("src/Mordor.App", f);
        string ui = Src("UiFonts.cs"), page = Src("FormPage.cs"), store = Src("CityWindow.Store.cs"), objects = Src("ObjectsForm.cs");
        Check("a row is the tallest line and RowPad", ui.Contains("RowHeight(FontPass pass, double atLeast, params DesignFont[] fonts) => Math.Max(atLeast, Line(pass, fonts) + RowPad);")
              && ui.Contains("public const double RowPad = 3;"), true);
        Check("the Guild's, the Bank's, the Library's and the Guild Log's lists (FormPage.List)", page.Contains("TightRows(l, UiFonts.RowHeight(Pass, 0, Sans(bold)));"), true);
        Check("the Store's list", store.Contains("RowHeight = UiFonts.RowHeight(pass,"), true);
        Check("Confinement's rows", Regex.Replace(PortSource("src/Mordor.App", "CityWindow.axaml.cs"), @"\s+", " ")
                  .Contains("double rowPitch = UiFonts.RowHeight(form.Fonts, FormPage.Y(ServiceForms.Confinement.elaComp0.Height), DesignFonts.PlainSans, DesignFonts.CompCounts);"), true);
        Check("Objects & Spells' pack and spell rows", objects.Contains("PackRowPitch => UiFonts.Cached(nameof(PackRowPitch), () => UiFonts.RowHeight(") && objects.Contains("SpellRowPitch => UiFonts.Cached(nameof(SpellRowPitch), () => UiFonts.RowHeight("), true);
        Check("Party Information's rows, and the drawing grown with them",
              Src("CityWindow.Mdi.cs").Contains("PartyRowHeight => 195 / 15 + PartyLineGrowth;") && Src("CityWindow.Mdi.cs").Contains("box.DrawnHeight = PartyDrawnAt.Height + 5 * g;"), true);
        Check("the Exchange's rows", Src("ExchangeWindow.cs").Contains("Height = UiFonts.RowHeight(DesignFonts.Exchange, 16)"), true);
        Check("a label a larger font no longer fits is let out to it (FormPage.LetOut)", page.Contains("LetOut(t, t, font, align, 0);") && page.Contains("LetOut(t, field, font, align, 6);"), true);

        // ---- the mods' text in the list's own face, and the Store's check box inside the page
        string slots = Src("ModSlots.cs");
        Check("a mod's Store cells are in the list's own face, its headings in Label9's Courier New as Item's are",
              store.Contains("() => _ch, Lookup, ModHead);") && store.Contains("var t = Head(text, countFont);")
              && store.Contains("TextBlock HeadOf(string text) => col == itemCol ? Head(text, countFont) : ModHead(text);")
              && !store.Contains("Head(name, font)") && !slots.Contains("fontSize"), true);

        // ---- 2026-09-29: spacing and the bold face
        string cityFlat = Regex.Replace(PortSource("src/Mordor.App", "CityWindow.axaml.cs"), @"\s+", " ");
        Check("Confinement's counts: set right, CountsPad in, in a column as wide as \"9 9 9\" in the Pro font, under \"G N E\" set the same",
              cityFlat.Contains("UiFonts.TextWidth(\"9 9 9\", form.Fonts, DesignFonts.CompCounts) + 2 * CountsPad")
              && cityFlat.Contains("gneHead.Padding = new Avalonia.Thickness(0, 0, CountsPad, 0);")
              && cityFlat.Contains("rowInfo[r].TextAlignment = TextAlignment.Right;")
              && cityFlat.Contains("rowInfo[r].Padding = new Avalonia.Thickness(0, (UiFonts.RowPad - 1) / 2, CountsPad, 0);"), true);
        Check("the Hall's right-set lines keep the headings' inset from the panel's edge",
              Src("HallWindow.cs").Contains("line.Padding = new Avalonia.Thickness(0, 0, stat.Left / 15.0, 0);"), true);
        Check("MS Sans Serif is the stand-in family, whose bold is Tahoma Bold and whose bold keeps the plain line",
              ui.Contains("? StandIn()") && Src("SansStandIn.cs").Contains("new FontFamily(\"Tahoma\")")
              && ui.Contains("if (weight > FontWeight.Normal && SansStandIn.IsFamily(family)) weight = FontWeight.Normal;"), true);
        Check("a bold caption is lifted to the plain face's line (labels, buttons, the character window's pages)",
              page.Contains("Lift(t, UiFonts.BoldLift(Pass, font));") && page.Contains("Lift(caption, UiFonts.BoldLift(Pass, Sans(bold)));")
              && Src("CharacterPanel.cs").Contains("if (p.Bold) FormPage.Lift(t, UiFonts.BoldLift(PictureFace, PageFontSize));"), true);
        Check("a list's text is set in from its line", page.Contains("new Setter(TemplatedControl.PaddingProperty, new Thickness(ListInset, 0)),")
              && page.Contains("public const double ListInset = 3;"), true);
        Check("the item page spreads its columns to its text; the Char page drops leading spaces on screen",
              Src("CharacterPanel.cs").Contains("Draw(_miscPage, all, spread: ItemInfoPage.IsItemPage(all));")
              && Src("CharacterPanel.cs").Contains(".Select(p => p with { Text = p.Text.TrimStart() })"), true);
        Check("the Exchange's client keeps the form's 60-twip margin right and below",
              Src("ExchangeWindow.cs").Contains("double clientW = (Math.Max(3195 + 3090, 4530 + 1740) + Margin) / 15.0 * grow.X, clientH = (5070 + 345 + Margin) / 15.0 * grow.Y;"), true);
        Check("the automap's row numbers have room for the widest in the chosen font",
              Src("AutomapWindow.axaml.cs").Contains("GutterLeft = GutterFor(h);"), true);
        Check("the Quality of Life mod's own views set no font: they take the game's where they are put",
              fontLine.IsMatch(PortSource("src/Mods", "Mordor.Mod.QoL", "Views.cs")), false);
        Check("the Store's mod check box is VB's compact box in the Store's face",
              slots.Contains("var box = FormPage.Compact(UiFonts.Set(new CheckBox") && page.Contains("public static CheckBox Compact(CheckBox box)"), true);
        Check("... on the heading's line, inside the page's right edge",
              store.Contains("ServiceForms.Store.ClientW - 60 - buyCaption.Left - buyCaption.Width"), true);
        Check("the Store's counts column is wide enough for its fullest line in the chosen font",
              store.Contains("UiFonts.TextWidth(\"9 9 9 9\", pass, countFont)"), true);
        Check("the Store draws in its own page's pass, kept at build time, not the pass of whichever page was built last",
              store.Contains("var pass = form.Fonts;") && !store.Contains("FormPage.Pass")
              && store.Contains("}, pass, countFont), supportsRecycling: false),")
              && !Src("ModSlots.cs").Contains("FormPage.Pass"), true);

        // frmCityGuild's form data makes cmdMake the Default button and cmdExit the Cancel button
        string city = Regex.Replace(PortSource("src/Mordor.App", "CityWindow.axaml.cs"), @"\s+", " ");
        Check("the Guild: Enter presses Make Level (when enabled) and Escape presses EXIT while it is the active window",
              city.Contains("var pressed = k == Avalonia.Input.Key.Escape ? exit : k is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return && make.IsEnabled ? make : null;")
              && city.Contains("_serviceKeys = keys;") && city.Contains("if (ActiveService is Svc keyed && keyed.Keys != null") && city.Contains("if (keyed.Keys(e.Key)) { e.Handled = true; return; }"), true);
    }
}
