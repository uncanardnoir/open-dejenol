using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The character window's Stats page against frmCharStat1's form data (MORDOR.EXE RCDATA 8):
    /// each label's rectangle (<see cref="CharStatForm"/>) read back out of the exe, Form_Resize's
    /// stacking of the loaded copies, and each label's font - the captions, the race and title lines
    /// and the states in the form's MS Sans Serif 8.25 bold (the NonPro font, bold, as every other
    /// tab's text), only lblStat's figures in Courier New (the Pro font). The page had been one text
    /// in lblStat's font, which put every caption in the Pro font.
    /// </summary>
    private static void StatsPageChecks(string dir)
    {
        Console.WriteLine("\nthe character window's Stats page is frmCharStat1's labels, each in its own font (MORDOR.EXE RCDATA 8)");
        var res = FormFonts.Resources(System.IO.Path.Combine(dir, "MORDOR.EXE"));
        if (!res.TryGetValue(CharStatForm.Resource, out var data) || !res.TryGetValue(CharStatForm.Resource + 1, out var namesRes))
        { Console.WriteLine("  FAIL  RCDATA 8 or 9 missing"); _failed++; return; }
        var names = FormFonts.Names(namesRes);
        Check("RCDATA 8 is frmCharStat1", names.FirstOrDefault() ?? "", "frmCharStat1");
        int U16(int p) => data[p] | (data[p + 1] << 8);

        // a control's rectangle: on a Label (01) under 05, after its Caption (00), Index (02),
        // BackColor (03) and ForeColor (04); on an Elastic (a custom control) under 04
        string RectOf(string name, int index)
        {
            for (int q = 0; q + 12 < data.Length; q++)
            {
                if (data[q] != 1 && data[q] != 3) continue;
                if (data[q + 3] != 0 || (data[q + 4] != 0 && data[q + 4] != 0x80)) continue;
                int n = data[q + 5];
                if (n <= 0 || n >= names.Count || names[n] != name) continue;
                int k = q + 6, idx = -1;
                if (data[q + 4] == 0x80) { idx = U16(k); k += 2; }
                if (idx != index || data[k] != 0) continue;
                int type = data[k + 1];
                k += 2;
                if (type == 0xFF) k += 1 + data[k];
                if (data[k] != 0xFF) continue;
                k++;
                int rectId = type == 1 ? 5 : 4;
                while (k + 8 < data.Length && data[k] != rectId)
                {
                    // an Elastic's BackColor (01) and ForeColor (02) and its Align (03) may come first
                    int size = type == 1
                        ? data[k] switch { 0 => 2 + data[k + 1], 2 => 3, 3 or 4 => 5, _ => -1 }
                        : data[k] switch { 1 or 2 => 5, 3 => 3, _ => -1 };
                    if (size < 0) return $"property {data[k]:X2} before the rectangle";
                    k += size;
                }
                short F(int o) => (short)U16(o);
                return $"{F(k + 1)},{F(k + 3)} {F(k + 5)}x{F(k + 7)}";
            }
            return "missing";
        }
        foreach (var c in CharStatForm.Controls)
            Check($"frmCharStat1: {c.Name}{(c.Index >= 0 ? $"({c.Index})" : "")}", RectOf(c.Name, c.Index), c.Rect.ToString());

        // Form_Resize's stacking (FRMCHAR1.BAS:124-183), from its own lines
        string resize = PortSource("decompiled-named", "FRMCHAR1.BAS");
        Check("Form_Resize: half a row is lblStat's Height / 2 in an Integer, half a row more before Spells and Hits, a whole one before Strength",
              resize.Contains("l0074 = (lblStat(l0072).Height / 2)")
              && resize.Contains("lblStat(l0072).Top = lblStat(l0072 - 1).Top + lblStat(l0072 - 1).Height + l0074")
              && resize.Contains("lblStat(l0072).Top = lblStat(l0072 - 1).Top + lblStat(l0072 - 1).Height + (l0074 * 2)"), true);
        Check("Form_Resize: the stats' figures are 400 wide, a twip past their caption; elaBorder above Strength; lblSpecial(0) on Strength's row",
              resize.Contains("lblStat(l0072).Width = 400")
              && resize.Contains("lblStat(l0072).Left = lblStatInfo(l0072).Left + lblStatInfo(l0072).Width + 1")
              && resize.Contains("elaBorder.P?C005 = lblStat(l0072).Top - (lblStat(l0072).Height / 1.2#)")
              && resize.Contains("lblSpecial(l0072).Top = lblStat(7).Top"), true);
        Check("Form_Resize: the captions", string.Join("|", CharStatForm.Captions.Skip(1).Where(s => !resize.Contains($"lblStatInfo(l0072).Caption = \"{s}\""))), "");
        Check("the rows' tops, Age to Dexterity", string.Join(" ", Enumerable.Range(0, 13).Select(CharStatForm.RowTop)),
              "885 1208 1423 1746 1961 2176 2391 2822 3037 3252 3467 3682 3897");
        Check("elaBorder's top, lblSpecial(6)'s, and the stats' figures' place",
              $"{CharStatForm.BorderTop} {CharStatForm.SpecialTop(6)} {CharStatForm.Figure(7)} {CharStatForm.Figure(6)}", "2643 4112 (1801, 400) (1965, 1455)");

        // the fonts: the form data's, then through the Options fonts
        var forms = FormFonts.ReadAll(dir).ToDictionary(f => f.Resource);
        var form = forms.TryGetValue(CharStatForm.Resource, out var ff) ? ff : null;
        string Stored(string key) => form?.Controls.FirstOrDefault(x => x.Key == key) is { } c
            ? (c.Font == null ? "none" : $"{c.Font} {c.Points} {((c.Style & 1) != 0 ? "bold" : "plain")}") : "missing";
        Check("the form data: lblStatInfo, lblRaceInfo, lblGuildInfo and lblSpecial store no font",
              string.Join("|", new[] { "lblStatInfo(0)", "lblRaceInfo", "lblGuildInfo", "lblSpecial(0)" }.Select(Stored)), "none|none|none|none");
        Check("the form data: lblStat(0) and (6) store Courier New 8.25, elaGroupInfo MS Sans Serif 8.25 plain",
              string.Join("|", new[] { "lblStat(0)", "lblStat(6)", "elaGroupInfo" }.Select(Stored)), "Courier New 8.25 plain|Courier New 8.25 plain|MS Sans Serif 8.25 plain");
        // the form's own header holds Courier New 8.25 bold; bm13.png shows the unfonted captions in a
        // bold sans serif all the same, so a control with none is VB3's default, not the form's
        {
            string head = System.Text.Encoding.Latin1.GetString(data, 0, Math.Min(200, data.Length));
            int at = head.IndexOf("\u000BCourier New", StringComparison.Ordinal);
            string got = at < 0 ? "none" : $"Courier New {BitConverter.ToSingle(data, at + 12)} {((data[at + 16] & 1) != 0 ? "bold" : "plain")}";
            Check("the form's own header stores Courier New 8.25 bold (not a control's font)", got, "Courier New 8.25 bold");
        }
        DesignFont Table(string key) => DesignFonts.All.FirstOrDefault(e => e.Form == DesignFonts.CharStat1 && e.Control == key).Font;
        var def = new GameOptions();
        var mine = new GameOptions { ProportionalFont = "Consolas", ProportionalSize = 10, NonProportionalFont = "Segoe UI", NonProportionalSize = 10 };
        string Drawn(GameOptions o, string key)
        {
            var d = Table(key);
            if (d.Face == null) return "not in DesignFonts";
            var r = FontRule.Resolve(o, d, DesignFonts.CharStat1.Pass);
            return $"{r.Face} {r.Points}{(d.Bold ? " bold" : "")}";
        }
        Check("the Stats page's captions are the sans bold face, as the other tabs: MS Sans Serif 8 bold by default",
              string.Join("|", new[] { "lblStatInfo(0)", "lblRaceInfo", "lblGuildInfo", "lblSpecial(0)", "picTabPicture" }.Select(k => Drawn(def, k))),
              string.Join("|", Enumerable.Repeat("MS Sans Serif 8 bold", 5)));
        Check("... the NonPro font, bold, when one is chosen", Drawn(mine, "lblStatInfo(0)") + "|" + Drawn(mine, "lblSpecial(0)"), "Segoe UI 10 bold|Segoe UI 10 bold");
        Check("the Stats page's figures are the Pro font, plain: Courier New 9 by default, the chosen Pro font otherwise",
              Drawn(def, "lblStat(0)") + "|" + Drawn(mine, "lblStat(0)"), "Courier New 9|Consolas 10");
        Check("\"You are Alone\" is the sans face, plain", Drawn(def, "elaGroupInfo") + "|" + Drawn(mine, "elaGroupInfo"), "MS Sans Serif 8|Segoe UI 10");

        // the port draws each label in its own font, not the page in lblStat's
        string panel = PortSource("src/Mordor.App", "CharacterPanel.cs");
        Check("the port: the page is no longer one text in lblStat's font",
              panel.Contains("UiFonts.Set(Page(), DesignFonts.CharStat1, \"lblStat(0)\")"), false);
        Check("the port: each label's font is its own control's",
              panel.Contains("DesignFont caption = UiFonts.Design(form, \"lblStatInfo(0)\"), figure = UiFonts.Design(form, \"lblStat(0)\"),")
              && panel.Contains("race = UiFonts.Design(form, \"lblRaceInfo\"), guild = UiFonts.Design(form, \"lblGuildInfo\"),")
              && panel.Contains("special = UiFonts.Design(form, \"lblSpecial(0)\"), group = UiFonts.Design(form, \"elaGroupInfo\");"), true);
        Check("the port: captions in the caption font set left, figures in lblStat's set right, the states set right",
              panel.Contains("Label(CharStatForm.Captions[n], caption, info.Left, info.Width, CharStatForm.RowTop(n), 0);")
              && panel.Contains("Label(figures[n], figure, left, width, CharStatForm.RowTop(n), 1);")
              && panel.Contains("Label(RaceData.StatNamesLong[i], caption, info.Left, info.Width, CharStatForm.RowTop(7 + i), 0);")
              && panel.Contains("Label($\"{c.StatWithMods(i, _item)}\", figure with { Bold = c.Stats[i] >= max[i] + 5 }, left, width, CharStatForm.RowTop(7 + i), 1);")
              && panel.Contains("Label(state, special, CharStatForm.lblSpecial0.Left, CharStatForm.lblSpecial0.Width, CharStatForm.SpecialTop(n), 1, StateRed);"), true);
        Check("the port: a label's weight is its font's, its face and size through UiFonts, a bold one lifted",
              panel.Contains("FontWeight = font.Bold ? FontWeight.Bold : FontWeight.Normal,")
              && panel.Contains("}, form, font);")
              && panel.Contains("FormPage.Lift(t, UiFonts.BoldLift(form.Pass, font));"), true);
    }
}
