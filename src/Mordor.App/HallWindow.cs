using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// frmHall (FRMHALL.BAS), shown modally by cmdCommand(10). Drawn to its form data in MORDOR.EXE
/// (RCDATA 58, <see cref="ServiceForms.Hall"/>): a 7665x5055-twip client, caption "Hall of
/// Records", every control in elaElastic(0) at -15,-15. The heading "Records &amp; Masters of
/// Abilities" is lblLabel(1), 12 point bold underlined (drawn at the Options NonPro size, as
/// sub0802 draws it, <see cref="UiFonts"/>), navy, centred on the button face; the two
/// pale yellow Elastics stand side by side; "E&amp;xit" is at the bottom right, and it is both the
/// Default and the Cancel button, so Enter and Escape press it too. sub32F6 (FRMHALL.BAS:114-278)
/// fills them: the six stat records on the left, the six others on the right, each a black
/// heading over two navy lines set right.
/// </summary>
public sealed class HallWindow
{
    private readonly Window _win;

    public HallWindow(string gameDir)
    {
        var hall = HallRecords.Load(Saves.DirFor(gameDir));
        // laid out larger for a larger Options font, as a service's form is (FormPage): the heading's
        // and the records' captions keep the room they have with the defaults, where this is one each way
        var grow = UiFonts.FormGrowth(DesignFonts.Hall.Pass);
        List<Monster> monsters;
        try { monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(gameDir, "MDATA5.MDR")); }
        catch { monsters = new(); }

        // gv05EE$, MODULE1.BAS:485-490
        string[] statNames = { "Strength", "Intelligence", "Wisdom", "Constitution", "Charisma", "Dexterity" };
        var left = new List<(HallRecords.Kind, Func<VbCurrency, string>)>();
        for (int i = 0; i < 6; i++)
        {
            string name = statNames[i];
            left.Add(((HallRecords.Kind)i, v => "With a " + v.CStr() + " " + name));
        }
        // the monster whose Id is the stored value, or "(Unknown)" (FRMHALL.BAS:203-209)
        string Creature(VbCurrency id) => "Defeated a \"" + (monsters.FirstOrDefault(m => id == m.Id)?.Name ?? "(Unknown)") + "\"";
        var right = new List<(HallRecords.Kind, Func<VbCurrency, string>)>
        {
            (HallRecords.Kind.DeadliestCreature, Creature),
            // formatNumber(CCur(...)) for these two, CStr for the rest
            (HallRecords.Kind.MostExperience, v => "Having " + CharStatFile.FormatNumber(v) + " total experience"),
            (HallRecords.Kind.Wealthiest, v => "Having a total of " + CharStatFile.FormatNumber(v) + " gold in the Bank"),
            (HallRecords.Kind.Fighting, v => "Having a combined fighting skill of " + v.CStr()),
            // "spell knowledge && power" in the source: a label shows && as one &
            (HallRecords.Kind.Magic, v => "Having a spell knowledge & power of " + v.CStr()),
            (HallRecords.Kind.Thieving, v => "Having a total thieving skill of " + v.CStr()),
        };

        // lblRecordStat/lblRecordChar and their History twins: the first pair where the form puts
        // it, each later one loaded directly under the one before (FRMHALL.BAS:115-119)
        Canvas Column(IEnumerable<(HallRecords.Kind k, Func<VbCurrency, string> text)> rows, FormRect stat, FormRect chr)
        {
            var c = new Canvas();
            int top = stat.Top;
            foreach (var (k, text) in rows)
            {
                c.Children.Add(UiFonts.Set(VbForm.Label(HallRecords.Heading(k), 11, Brushes.Black, bold: true), DesignFonts.Hall, "lblRecordStat(0)")
                                     .At(stat with { Top = top }, grow));
                top += stat.Height;
                var line = UiFonts.Set(VbForm.Label(hall.Describe(k, text), 11, VbForm.Navy, align: TextAlignment.Right), DesignFonts.Hall, "lblRecordChar(0)")
                                  .At(chr with { Top = top }, grow);
                // set in from the panel's right edge as far as the headings are from its left
                // (lblRecordStat's 60 twips): the lines reach the panel's edge in the form data, and
                // drawn inside its sunken border they had touched it (a deviation, docs/FIDELITY.md)
                line.Padding = new Avalonia.Thickness(0, 0, stat.Left / 15.0, 0);
                c.Children.Add(line);
                top += chr.Height;
            }
            return c;
        }

        double clientW = ServiceForms.Hall.ClientW / 15.0 * grow.X, clientH = ServiceForms.Hall.ClientH / 15.0 * grow.Y;
        var form = new Canvas { Width = clientW, Height = clientH, Background = VbForm.Grey, ClipToBounds = true };
        // elaElastic(0), which every control is placed in
        var root = new Canvas().At(ServiceForms.Hall.Elastic, grow);
        form.Children.Add(root);
        var heading = new Border
        {
            Background = VbForm.ButtonFace,
            // lblLabel(1) is designed 12 points, but with no ":" Tag: Form_Load's sub0802 sizes it
            // at NonProSize, 8 points with the defaults, as it does every control (UiFonts)
            Child = UiFonts.Set(new TextBlock
            {
                Text = "Records & Masters of Abilities", FontWeight = FontWeight.Bold,
                Foreground = VbForm.Navy, TextDecorations = TextDecorations.Underline,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            }, DesignFonts.Hall, "lblLabel(1)"),
        }.At(ServiceForms.Hall.Heading, grow);
        root.Children.Add(heading);
        root.Children.Add(VbForm.Sunken(VbForm.PaleYellow, Column(left, ServiceForms.Hall.lblRecordStat0, ServiceForms.Hall.lblRecordChar0))
                                .At(ServiceForms.Hall.RecordsBox, grow));
        root.Children.Add(VbForm.Sunken(VbForm.PaleYellow, Column(right, ServiceForms.Hall.lblHistoryStat0, ServiceForms.Hall.lblHistoryChar0))
                                .At(ServiceForms.Hall.HistoryBox, grow));
        var exit = UiFonts.Set(VbForm.Button("E_xit", 11, bold: true), DesignFonts.Hall).At(ServiceForms.Hall.cmdExit, grow);
        // Default and Cancel both True in the form data: Enter and Escape press it as well as Alt+X
        exit.IsDefault = true;
        exit.IsCancel = true;
        root.Children.Add(exit);

        _win = VbForm.Dialog("Hall of Records", clientW, clientH, VbForm.Grey, form);
        // cmdCommand_Click (FRMHALL.BAS:35-42): Unload Me
        exit.Click += (_, _) => _win.Close();
        _win.Opened += (_, _) => exit.Focus();
    }

    public System.Threading.Tasks.Task ShowDialog(Window owner) => OriginalBox.ShowOver(_win, owner);
}
