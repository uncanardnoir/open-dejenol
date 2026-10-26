using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The town's service windows drawn to their forms' data: each form's client and the rectangle
    /// of every control the port draws, read out of MORDOR.EXE and compared with
    /// <see cref="ServiceForms"/>, which the pages are built from; then the port's own wiring - each
    /// page built on its form's client and rectangles, the window sized to that client.
    ///
    /// A form resource (RCDATA) is the form's properties and then one record per control:
    /// 01 (the first inside a container) or 03 (the next), a length, a byte of nought, 80 for a
    /// control array, the control's number in the names resource that follows it, its Index when
    /// in an array, a nought, and its type (1 Label, 2 TextBox, 4 CommandButton, 8 ListBox, FF a
    /// custom control, whose class name follows). Its properties follow an FF; the rectangle is
    /// four twip values under 05 on a Label and 04 on the rest, after the properties below it.
    /// </summary>
    private static void ServiceFormChecks(string dir)
    {
        Console.WriteLine("\nthe town's service windows are drawn to their forms' data (MORDOR.EXE)");
        string exe = System.IO.Path.Combine(dir, "MORDOR.EXE");
        if (!System.IO.File.Exists(exe)) { Console.WriteLine("  FAIL  MORDOR.EXE missing"); _failed++; return; }
        byte[] b = System.IO.File.ReadAllBytes(exe);
        int U16(byte[] a, int p) => a[p] | (a[p + 1] << 8);
        var res = new Dictionary<int, byte[]>();
        {
            int ne = U16(b, 0x3C), table = ne + U16(b, ne + 0x24), shift = U16(b, table), p = table + 2;
            while (U16(b, p) != 0)
            {
                int type = U16(b, p), count = U16(b, p + 2);
                p += 8;
                for (int i = 0; i < count; i++, p += 12)
                    if (type == 0x800A)
                    {
                        int at = U16(b, p) << shift;
                        res[U16(b, p + 6) & 0x7FFF] = b.AsSpan(at, Math.Min(U16(b, p + 2) << shift, b.Length - at)).ToArray();
                    }
            }
        }

        string cityPage = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
        // each control's properties before its FF, the ones fixed when its window is made
        var prelude = new Dictionary<string, string>();
        var record = new Dictionary<string, string>();

        void Form(string title, int id, int clientW, int clientH, GameWindow? which, Type cls, ServiceForms.Control[] controls,
                  string? source = null, string? drawnOn = null)
        {
            if (!res.TryGetValue(id, out var data) || !res.TryGetValue(id + 1, out var namesRes))
            { Console.WriteLine($"  FAIL  RCDATA {id} missing"); _failed++; return; }
            var names = new List<string>();
            for (int q = 0; q < namesRes.Length && namesRes[q] != 0; q += namesRes[q] + 1)
                names.Add(System.Text.Encoding.ASCII.GetString(namesRes, q + 1, namesRes[q]));
            Check($"{title}: RCDATA {id} is {names.FirstOrDefault()}", names.FirstOrDefault() ?? "", title);

            // the form's own ScaleLeft, ScaleTop, ScaleWidth and ScaleHeight, 35 to 38
            string client = "missing";
            for (int q = 0; q + 20 < data.Length; q++)
                if (data[q] == 0x35 && data[q + 5] == 0x36 && data[q + 10] == 0x37 && data[q + 15] == 0x38)
                { client = $"{BitConverter.ToInt32(data, q + 11)}x{BitConverter.ToInt32(data, q + 16)}"; break; }
            Check($"{title}: the client, in twips", client, $"{clientW}x{clientH}");
            // the window table's default is the form's outer size: the client and its Fixed Double
            // border, 120 twips across and 405 down with the caption
            if (which is GameWindow w)
            {
                var (dw, dh) = WindowLayout.Default(w);
                Check($"{title}: the window table's default is that client's window",
                      $"{Math.Round(dw * 15) - 120}x{Math.Round(dh * 15) - 405}", $"{clientW}x{clientH}");
            }

            foreach (var c in controls)
            {
                string got = "missing";
                for (int q = 0; q + 12 < data.Length && got == "missing"; q++)
                {
                    if (data[q] != 1 && data[q] != 3) continue;
                    if (data[q + 3] != 0 || (data[q + 4] != 0 && data[q + 4] != 0x80)) continue;
                    int name = data[q + 5];
                    if (name <= 0 || name >= names.Count || names[name] != c.Name) continue;
                    int k = q + 6, index = -1;
                    if (data[q + 4] == 0x80) { index = U16(data, k); k += 2; }
                    if (index != c.Index || data[k] != 0) continue;
                    int type = data[k + 1];
                    k += 2;
                    if (type == 0xFF) { k += 1 + data[k]; if (data[k] != 0xFF) continue; k++; }
                    else
                    {
                        // the properties fixed when the window is made come before the FF, one byte
                        // each: a TextBox's MultiLine (17) and ScrollBars (18), a ListBox's Sorted (15)
                        int pre = k;
                        while (pre + 1 < data.Length && data[pre] != 0xFF && pre - k < 8
                               && (data[pre] is 0x15 or 0x17 or 0x18)) pre += 2;
                        if (data[pre] != 0xFF) continue;
                        prelude[$"{title}.{c.Name}"] = string.Join(" ", data.Skip(k).Take(pre - k).Select(x => x.ToString("X2")));
                        k = pre + 1;
                    }
                    // the properties that come before the rectangle, and their sizes
                    var sizes = type switch
                    {
                        1 => new Dictionary<int, int> { [0] = -1, [2] = 2, [3] = 4, [4] = 4 },
                        2 => new Dictionary<int, int> { [0] = -1, [1] = 2, [2] = 4, [3] = 4 },
                        4 => new Dictionary<int, int> { [0] = -1, [2] = 2, [3] = 4 },
                        8 => new Dictionary<int, int> { [1] = 2, [2] = 4, [3] = 4 },
                        // a PictureBox (00): BackColor, then an Index; an OptionButton (06): Caption, Index
                        0 => new Dictionary<int, int> { [1] = 4, [2] = 2 },
                        6 => new Dictionary<int, int> { [0] = -1, [2] = 2, [3] = 4, [4] = 4 },
                        _ => new Dictionary<int, int> { [1] = 4, [2] = 4, [3] = 2 },
                    };
                    // a VScrollBar (type 0A) has its rectangle first, under 02
                    if (type == 0x0A) sizes.Clear();
                    int rectId = type is 1 or 0 or 6 ? 5 : type == 0x0A ? 2 : 4;
                    while (k + 8 < data.Length && data[k] != rectId)
                    {
                        if (!sizes.TryGetValue(data[k], out int size)) { got = $"property {data[k]:X2} before the rectangle"; break; }
                        k += size < 0 ? 2 + data[k + 1] : 1 + size;
                    }
                    if (got != "missing") break;
                    int F(int o) => (short)U16(data, o);
                    got = $"{F(k + 1)},{F(k + 3)} {F(k + 5)}x{F(k + 7)}";
                    // the whole record, its length after the 01 or 03, as hex
                    record[$"{title}.{c.Name}{(c.Index >= 0 ? $"({c.Index})" : "")}"] = string.Join(" ", data.Skip(q).Take(U16(data, q + 1)).Select(x => x.ToString("X2")));
                }
                Check($"{title}: {c.Name}{(c.Index >= 0 ? $"({c.Index})" : "")}", got, c.Rect.ToString());
            }

            // the port: the page on this client, and every rectangle placed
            string cn = cls.Name;
            string pageSource = source ?? cityPage;
            Check($"{title}: the page is drawn on that client",
                  pageSource.Contains(drawnOn ?? $"new FormPage(ServiceForms.{cn}.ClientW, ServiceForms.{cn}.ClientH, ServiceForms.{cn}.Elastic)"), true);
            var unplaced = cls.GetFields().Where(f => f.FieldType == typeof(FormRect) && (f.Name != "Elastic" || drawnOn != null)
                                                      && !(cn == "Guild" && f.Name.StartsWith("lblStat"))   // the Guild's stats go through FormPage.Across
                                                      && !pageSource.Contains($"ServiceForms.{cn}.{f.Name}"))
                                  .Select(f => f.Name).ToList();
            Check($"{title}: every rectangle is placed", unplaced.Count == 0 ? "all" : string.Join(" ", unplaced), "all");
        }

        Form("frmCityGuild", ServiceForms.Guild.Resource, ServiceForms.Guild.ClientW, ServiceForms.Guild.ClientH,
             GameWindow.Guild, typeof(ServiceForms.Guild), ServiceForms.Guild.Controls);
        Check("frmCityGuild: lblStatName and lblStat loaded across from their first (FRMCTYG.BAS:637-638)",
              cityPage.Contains("FormPage.Across(ServiceForms.Guild.lblStatName0, i)") && cityPage.Contains("FormPage.Across(ServiceForms.Guild.lblStat0, i)"), true);
        Form("frmCityBank", ServiceForms.Bank.Resource, ServiceForms.Bank.ClientW, ServiceForms.Bank.ClientH,
             GameWindow.Bank, typeof(ServiceForms.Bank), ServiceForms.Bank.Controls);
        Form("frmCityMorgue", ServiceForms.Morgue.Resource, ServiceForms.Morgue.ClientW, ServiceForms.Morgue.ClientH,
             GameWindow.Morgue, typeof(ServiceForms.Morgue), ServiceForms.Morgue.Controls);
        Form("frmCityCStore", ServiceForms.Confinement.Resource, ServiceForms.Confinement.ClientW, ServiceForms.Confinement.ClientH,
             GameWindow.CompanionStore, typeof(ServiceForms.Confinement), ServiceForms.Confinement.Controls);
        Form("frmCityStore", ServiceForms.Store.Resource, ServiceForms.Store.ClientW, ServiceForms.Store.ClientH,
             GameWindow.GeneralStore, typeof(ServiceForms.Store), ServiceForms.Store.Controls, PortSource("src/Mordor.App", "CityWindow.Store.cs"));
        Form("frmCitySeer", ServiceForms.Seer.Resource, ServiceForms.Seer.ClientW, ServiceForms.Seer.ClientH,
             GameWindow.Seer, typeof(ServiceForms.Seer), ServiceForms.Seer.Controls);

        // the Library, the Guild Log and the Hall of Records: no window-table entry, so no default to hold them to
        Form("frmLibrary", ServiceForms.Library.Resource, ServiceForms.Library.ClientW, ServiceForms.Library.ClientH,
             null, typeof(ServiceForms.Library), ServiceForms.Library.Controls,
             drawnOn: "new FormPage(ServiceForms.Library.ClientW, ServiceForms.Library.ClientH, ServiceForms.Library.Elastic, FontPass.SizeOnly)");
        Form("frmGuildLog", ServiceForms.GuildLog.Resource, ServiceForms.GuildLog.ClientW, ServiceForms.GuildLog.ClientH,
             null, typeof(ServiceForms.GuildLog), ServiceForms.GuildLog.Controls,
             drawnOn: "new FormPage(ServiceForms.GuildLog.ClientW, ServiceForms.GuildLog.ClientH, ServiceForms.GuildLog.Elastic, FontPass.SizeOnly)");
        string hallPage = PortSource("src/Mordor.App", "HallWindow.cs");
        Form("frmHall", ServiceForms.Hall.Resource, ServiceForms.Hall.ClientW, ServiceForms.Hall.ClientH,
             null, typeof(ServiceForms.Hall), ServiceForms.Hall.Controls, hallPage,
             "double clientW = ServiceForms.Hall.ClientW / 15.0 * grow.X, clientH = ServiceForms.Hall.ClientH / 15.0 * grow.Y;");
        // Objects & Spells' Companions page: the four pictures, their names and hits, the kick-out buttons
        Form("frmCharStat3", ServiceForms.Companions.Resource, ServiceForms.Companions.ClientW, ServiceForms.Companions.ClientH,
             null, typeof(ServiceForms.Companions), ServiceForms.Companions.Controls, PortSource("src/Mordor.App", "CompanionsPage.cs"),
             "Client = new(ServiceForms.Companions.ClientW / 15.0, ServiceForms.Companions.ClientH / 15.0);");

        string Rec(string key) => record.TryGetValue(key, out var r) ? r : "missing";

        // elaAlignments.P?C008 (FRMCITYC.BAS:312-351, FRMCTYS.BAS:334-371) is the Elastic's property
        // 08, Visible: the two Select Alignment frames store it, as 0, straight after their
        // rectangle, and the other Elastics - shown at design, as bm9.png has them - do not store it
        string AfterRect(string key, FormRect r)
        {
            string H(int v) => $"{v & 0xFF:X2} {(v >> 8) & 0xFF:X2}";
            string rect = $"04 {H(r.Left)} {H(r.Top)} {H(r.Width)} {H(r.Height)} ";
            string rec = Rec(key);
            int at = rec.IndexOf(rect, StringComparison.Ordinal);
            return at < 0 ? "no rectangle" : rec.Substring(at + rect.Length, 2);
        }
        Check("frmCityCStore: elaAlignments stores property 08 (P?C008) straight after its rectangle",
              AfterRect("frmCityCStore.elaAlignments", ServiceForms.Confinement.elaAlignments), "08");
        Check("frmCityCStore: ... as 0, Visible False", Rec("frmCityCStore.elaAlignments").Contains("B2 02 08 00 0D"), true);
        Check("frmCityStore: elaAlignments the same", AfterRect("frmCityStore.elaAlignments", ServiceForms.Store.elaAlignments), "08");
        Check("frmCityStore: ... as 0", Rec("frmCityStore.elaAlignments").Contains("A3 02 08 00 0D"), true);
        Check("frmCityCStore: the shown Elastics do not store it (elaElastic(1), (3))",
              AfterRect("frmCityCStore.elaElastic(1)", ServiceForms.Confinement.BuyBox) + " " + AfterRect("frmCityCStore.elaElastic(3)", ServiceForms.Confinement.BindBox), "0D 0D");
        Check("frmCityStore: nor its elaElastic(1)", AfterRect("frmCityStore.elaElastic(1)", ServiceForms.Store.UncurseBox), "0D");
        Check("frmLibrary: lstList is Sorted (15 FF before its FF), so the names are shown alphabetically",
              prelude.TryGetValue("frmLibrary.lstList", out var sorted) ? sorted : "missing", "15 FF");
        Check("frmLibrary: txtInfo is MultiLine with a vertical scroll bar (17 FF, 18 02)",
              prelude.TryGetValue("frmLibrary.txtInfo", out var info) ? info : "missing", "17 FF 18 02");
        Check("frmLibrary: the page sorts the list and shows txtInfo's scroll bar",
              cityPage.Contains("var monRows = LibrarySearch.Sorted(") && cityPage.Contains("var itemRows = LibrarySearch.Sorted(")
              && cityPage.Contains("ScrollViewer.SetVerticalScrollBarVisibility(info, Avalonia.Controls.Primitives.ScrollBarVisibility.Visible);"), true);
        Check("frmLibrary: Exit is \"Exit\", no access key, and Cancel (14 FF): Escape presses it",
              Rec("frmLibrary.cmdCommand(0)").Contains("45 78 69 74 02") && Rec("frmLibrary.cmdCommand(0)").Contains(" 14 FF FF"), true);
        Check("frmLibrary: the page's Escape is the Exit's",
              cityPage.Contains("var exit = FormPage.Button(root, \"Exit\", ServiceForms.Library.cmdExit);")
              && cityPage.Contains("if (k.Key != Avalonia.Input.Key.Escape || k.KeyModifiers != Avalonia.Input.KeyModifiers.None) return;"), true);
        Check("frmLibrary: Escape is the active form's Cancel wherever the focus is: the window's key preview hands it to the Library, as to the Guild",
              cityPage.Contains("if (k != Avalonia.Input.Key.Escape) return false;")
              && System.Text.RegularExpressions.Regex.Matches(cityPage, @"_serviceKeys = keys;").Count == 2
              && cityPage.Contains("            keys(k.Key);"), true);
        Check("frmLibrary: Monsters is optOption(1), Value True (13 FF); Items optOption(0)",
              Rec("frmLibrary.optOption(1)").Contains("08 4D 6F 6E 73 74 65 72 73") && Rec("frmLibrary.optOption(1)").Contains(" 13 FF FF")
              && Rec("frmLibrary.optOption(0)").Contains("05 49 74 65 6D 73") && !Rec("frmLibrary.optOption(0)").Contains(" 13 FF"), true);
        Check("frmLibrary: the page starts on Monsters", cityPage.Contains("var monOpt = FormPage.Option(options, \"Monsters\", ServiceForms.Library.optMonsters, \"library\", true);"), true);
        Check("frmGuildLog: Clean is \"&Clean Guild Log\" and Visible False (09 00)",
              Rec("frmGuildLog.cmdClean").Contains("26 43 6C 65 61 6E") && Rec("frmGuildLog.cmdClean").Contains(" 09 00 "), true);
        Check("frmGuildLog: Done is \"&Done\" and neither Default nor Cancel",
              Rec("frmGuildLog.cmdDone").Contains("26 44 6F 6E 65") && !Rec("frmGuildLog.cmdDone").Contains(" 13 FF") && !Rec("frmGuildLog.cmdDone").Contains(" 14 FF"), true);
        Check("frmGuildLog: the page's Done is Alt+D", cityPage.Contains("FormPage.Access(done, 'D');"), true);
        Check("frmHall: Exit is \"E&xit\", Default and Cancel (13 FF 14 FF)",
              Rec("frmHall.cmdCommand(0)").Contains("45 26 78 69 74") && Rec("frmHall.cmdCommand(0)").Contains("13 FF 14 FF FF"), true);
        Check("frmHall: the window's Exit is Default and Cancel", hallPage.Contains("exit.IsDefault = true;") && hallPage.Contains("exit.IsCancel = true;"), true);
        Check("frmHall: each record pair is loaded under the one before (FRMHALL.BAS:115-119)",
              hallPage.Contains("top += stat.Height;") && hallPage.Contains("top += chr.Height;"), true);

        // txtSearch_KeyDown (FRMLIBRA.BAS:130-156), from Core
        var names = new[] { "Orc", "Orc Lord", "Kobold", "Giant Rat" };
        Check("Library search: a unique start is selected", LibrarySearch.Find(names, "kob").ToString(), "(Selected, 2)");
        Check("Library search: upper and lower case alike", LibrarySearch.Find(names, "GIANT r").ToString(), "(Selected, 3)");
        Check("Library search: no entry starts that way", LibrarySearch.Find(names, "Dragon").Outcome.ToString(), "NoMatch");
        Check("Library search: a start two entries share is ambiguous, a whole name included", LibrarySearch.Find(names, "orc").Outcome.ToString(), "Ambiguous");
        Check("Library search: longer than the entry is no match", LibrarySearch.Find(names, "Kobolds").Outcome.ToString(), "NoMatch");
        Check("Library search: an empty box matches all (ambiguous)", LibrarySearch.Find(names, "").Outcome.ToString(), "Ambiguous");
        Check("Library search: an empty box on a one-line list selects it", LibrarySearch.Find(new[] { "<NO ENTRIES>" }, "").ToString(), "(Selected, 0)");
        Check("Library search: the boxes' words", $"{LibrarySearch.Title}|{LibrarySearch.NoMatch}|{LibrarySearch.Ambiguous}", "Search|No match was found.|ambiguous!");
        Check("Library list: sorted without regard to case",
              string.Join(",", LibrarySearch.Sorted(new[] { "orc", "Giant Rat", "Ant", "bat" }, s => s)), "Ant,bat,Giant Rat,orc");
        // lstList is a Sorted ListBox, whose order is lstrcmpi's: a word sort, in which a hyphen and
        // an apostrophe count for nothing at first (lstrcmpiW gives Archer < Arch-Fiend < Archmage,
        // Dragonfly < Dragons < Dragon's Blood, Tarantula < T'Kovant). .NET's culture compare (ICU)
        // put the apostrophe ahead of the letters, and every T' creature before Tarantula.
        string Order(params string[] n) => string.Join(",", LibrarySearch.Sorted(n.Reverse(), s => s));
        string Managed(params string[] n) => string.Join(",", n.Reverse().OrderBy(s => s, Comparer<string>.Create(LibrarySearch.WordSort)));
        Check("Library list: a hyphen counts for nothing at first (Archer, Arch-Fiend, Archmage)",
              Order("Archer", "Arch-Fiend", "Archmage"), "Archer,Arch-Fiend,Archmage");
        Check("Library list: an apostrophe counts for nothing at first (Dragonfly, Dragons, Dragon's Blood)",
              Order("Dragonfly", "Dragons", "Dragon's Blood"), "Dragonfly,Dragons,Dragon's Blood");
        Check("Library list: Tarantula before T'Kovant", Order("Tarantula", "T'Kovant"), "Tarantula,T'Kovant");
        Check("Library list: the managed stand-in for lstrcmpi agrees on all three",
              $"{Managed("Archer", "Arch-Fiend", "Archmage")}|{Managed("Dragonfly", "Dragons", "Dragon's Blood")}|{Managed("Tarantula", "T'Kovant")}",
              "Archer,Arch-Fiend,Archmage|Dragonfly,Dragons,Dragon's Blood|Tarantula,T'Kovant");
        Check("Library list: names equal but for case keep the order they were added in",
              string.Join(",", LibrarySearch.Sorted(new[] { "b", "Orc", "ORC", "orc", "a" }, s => s)), "a,b,Orc,ORC,orc");
        {
            // the creatures and items the Library lists (MDATA5, MDATA3), in the list's order
            var monNames = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).Select(m => m.Name.Trim()).Distinct().ToList();
            var itemNames = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).Select(i => i.Name.Trim()).Distinct().ToList();
            string Around(List<string> all, string name, int before, int after)
            {
                var s = LibrarySearch.Sorted(all, x => x);
                int i = s.IndexOf(name);
                return i < 0 ? $"{name} missing" : string.Join(",", s.Skip(i - before).Take(before + 1 + after));
            }
            Check("Library list, MDATA5: Arch-Fiend between Apparitionist and Arthropod", Around(monNames, "Arch-Fiend", 1, 1), "Apparitionist,Arch-Fiend,Arthropod");
            Check("Library list, MDATA5: T'Butu after Tarantula and Tazen, T'Kovant after Tiger Shark",
                  $"{Around(monNames, "T'Butu", 2, 1)}|{Around(monNames, "T'Kovant", 1, 2)}", "Tarantula,Tazen,T'Butu,Tengu|Tiger Shark,T'Kovant,T'Lanea,T'Mana");
            Check("Library list, MDATA5: I'ssus after Iron Bear, Ye'ssus after Yellow Gremlin",
                  $"{Around(monNames, "I'ssus", 1, 1)}|{Around(monNames, "Ye'ssus", 1, 1)}", "Iron Bear,I'ssus,Jestor|Yellow Gremlin,Ye'ssus,Yundrun");
            Check("Library list, MDATA3: Dragon's Blood after Dorlan's Sash, then Dragon's Water", Around(itemNames, "Dragon's Blood", 1, 1), "Dorlan's Sash,Dragon's Blood,Dragon's Water");
            string WholeManaged(List<string> all) => string.Join(",", all.OrderBy(s => s, Comparer<string>.Create(LibrarySearch.WordSort)));
            Check("Library list: the managed stand-in orders every MDATA5 and MDATA3 name as lstrcmpi does",
                  WholeManaged(monNames) == string.Join(",", LibrarySearch.Sorted(monNames, x => x))
                  && WholeManaged(itemNames) == string.Join(",", LibrarySearch.Sorted(itemNames, x => x)), true);
        }
        Check("Library search: the page selects a single match and clears the box, else the box and the text selected",
              cityPage.Contains("var (outcome, index) = LibrarySearch.Find(shown, search.Text ?? \"\");")
              && cityPage.Contains("list.SelectedIndex = index;") && cityPage.Contains("search.Text = \"\";")
              && cityPage.Contains("search.SelectAll();"), true);

        string storePage = PortSource("src/Mordor.App", "CityWindow.Store.cs");
        Check("frmCityStore: the Buy list is Label9's line and nine elaItem rows, to vsbItems' right",
              ServiceForms.Store.ListArea.ToString(), "105,75 3855x2115");
        Check("frmCityStore: its rows are the fonts' line and RowPad, never under elaItem's 210 twips (user request), its headers Label9's line",
              storePage.Contains("RowHeight = UiFonts.RowHeight(pass, FormPage.Y(ServiceForms.Store.elaItem0.Height), nameFont, countFont),")
              && storePage.Contains("ColumnHeaderHeight = UiFonts.LabelHeight(pass, FormPage.Y(ServiceForms.Store.elaItem0.Top - ServiceForms.Store.ItemHead.Top), countFont),"), true);
        Check("frmCityStore: a mod's columns widen the page, the panel and the list alike (none: the form's own)",
              storePage.Contains("page.Width = form.Client.Width + extra;") && storePage.Contains("buyList.Width = form.PixelsX(listArea.Width) + extra;")
              && storePage.Contains("StoreWidth(_serviceExtraWidth);"), true);

        string mdi = PortSource("src/Mordor.App", "CityWindow.Mdi.cs");
        Check("the window is the form's client in its frame, wider only by mod columns",
              mdi.Contains("w = client.Width + FormChromeW + _serviceExtraWidth;") && mdi.Contains("h = client.Height + FormChromeH;"), true);
        Check("the port's heading strip, margin and scrolling go for a form's page",
              mdi.Contains("ServiceHeader.IsVisible = false;") && mdi.Contains("ServiceBody.Padding = new Avalonia.Thickness(0);")
              && mdi.Contains("ServicePanel.BorderThickness = new Avalonia.Thickness(0);")
              && mdi.Contains("_serviceChild.BodyMargin = new Avalonia.Thickness(0);"), true);
        string page = PortSource("src/Mordor.App", "FormPage.cs");
        Check("a control is placed at its twips over 15, grown with the page for a larger Options font",
              page.Contains("Canvas.SetLeft(c, X(r.Left));") && page.Contains("c.Height = Y(r.Height);") && page.Contains("public const double Twips = 15;")
              && page.Contains("public static double X(double twips) => twips / Twips * _zoomX;")
              && page.Contains("public static double Y(double twips) => twips / Twips * _zoomY;")
              && page.Contains("(GrowX, GrowY) = (_zoomX, _zoomY) = UiFonts.FormGrowth(fonts);")
              && page.Contains("Client = new Size(X(clientW), Y(clientH));"), true);
        // with the default fonts the growth is exactly one each way, so every page is the form's own size
        string uiFonts = PortSource("src/Mordor.App", "UiFonts.cs");
        Check("a page grows only for fonts other than the defaults, and never shrinks",
              uiFonts.Contains("FormGrowth(FontPass pass) => FontRule.IsDefault(Options) ? (1, 1) : (")
              && uiFonts.Contains("double most = 1;") && uiFonts.Contains("most = Math.Max(most, chosen / design);"), true);
        // no service page, nor the Hall or the Exchange, turns twips into pixels but through the growth
        foreach (var (file, what) in new[] { ("CityWindow.axaml.cs", "the town's pages"), ("CityWindow.Store.cs", "the Store") })
            Check($"{what}: no twips turned to pixels outside FormPage's growth", PortSource("src/Mordor.App", file).Contains("/ FormPage.Twips"), false);
        string exchangePage = PortSource("src/Mordor.App", "ExchangeWindow.cs");
        Check("frmExchange and frmHall grow as the services do",
              exchangePage.Contains("var grow = UiFonts.FormGrowth(DesignFonts.Exchange.Pass);") && hallPage.Contains("var grow = UiFonts.FormGrowth(DesignFonts.Hall.Pass);")
              && !System.Text.RegularExpressions.Regex.IsMatch(exchangePage + hallPage, @"\.At\([^()]*\d\)[;)]"), true);
        Check("list rows are the Options font's line and RowPad, not the theme's",
              page.Contains("TightRows(l, UiFonts.RowHeight(Pass, 0, Sans(bold)));") && page.Contains("new Setter(Layoutable.HeightProperty, rowHeight)"), true);
    }
}
