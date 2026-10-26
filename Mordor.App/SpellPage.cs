using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The Spells page of Objects &amp; Spells (FRMCHAR3), shared by the dungeon and the city, which
/// both have the form on the frame. cmbClass picks a class (<c>sub1658</c>, MAINMOD2.BAS:5510-5539);
/// <c>cmbClass_Click</c> lists that class's spells (FRMCHAR3.BAS:55-140); <c>sub2424</c> writes
/// each as four labels - Name, the guild's first three letters, the level and the points, "***"
/// past 999 - bold when <c>mdrcharCheckCast</c> passes it, and "No Spells Yet" in the first row
/// when there is nothing (FRMCHAR3.BAS:681-712). Nothing is hidden for being a combat spell or a
/// non-combat one: the cast refuses those, the list does not.
/// </summary>
internal static class SpellPage
{
    /// <summary>True while <see cref="FillClasses"/> is changing the box, so its handler can ignore it.</summary>
    public static bool Filling { get; private set; }

    /// <summary>
    /// sub1658: fill cmbClass when it is empty, otherwise leave the class as chosen. sub1536 and
    /// a guild join empty it first (<see cref="Clear"/>), so the first class is listed again. The
    /// port asks every time and rebuilds only when the box was emptied, or the character or the
    /// classes have changed, keeping the one chosen.
    /// </summary>
    public static void FillClasses(ComboBox box, Character c)
    {
        var kinds = Spellbook.SpellClasses(c);
        bool sameChar = ReferenceEquals(box.DataContext, c);
        if (sameChar && box.Tag is List<int> same && same.SequenceEqual(kinds) && box.ItemCount > 0) return;
        int keep = sameChar ? Kind(box) : -1;
        Filling = true;
        try
        {
            box.DataContext = c;
            box.Tag = kinds;
            box.ItemsSource = kinds.Count == 0
                ? new List<string> { Spellbook.NoSpellClasses }                 // 5529-5532
                : kinds.Select(k => Librarian.SpellSchools[k]).ToList();        // 5522
            box.IsEnabled = kinds.Count > 0;
            int at = kinds.IndexOf(keep);
            box.SelectedIndex = at >= 0 ? at : 0;                               // 5533
        }
        finally { Filling = false; }
    }

    /// <summary>
    /// <c>frmCharStat3!cmbClass.Clear</c>: the box emptied, so the next <see cref="FillClasses"/>
    /// fills it afresh and picks the first class (MAINMOD2.BAS:5516-5533) - for the same character
    /// too. sub1536 does it on every run (MULTIMOD.BAS:675), and so does joining or re-entering a
    /// guild (FRMCTYG.BAS:163); the other sub1658 callers leave the class as chosen.
    /// </summary>
    public static void Clear(ComboBox box) => box.DataContext = null;

    /// <summary>
    /// cmbClass.ItemData of the chosen entry. "NO SPELLS" was given none, which VB leaves at nought,
    /// so class 0 is what cmbClass_Click then lists (FRMCHAR3.BAS:70).
    /// </summary>
    public static int Kind(ComboBox box) =>
        box.Tag is List<int> k && box.SelectedIndex >= 0 && box.SelectedIndex < k.Count ? k[box.SelectedIndex] : 0;

    /// <summary>
    /// The rows for the list: sub2424's four labels per spell, or its single "No Spells Yet" row
    /// (FRMCHAR3.BAS:683-687) when the class lists nothing.
    /// </summary>
    public static List<Control> Rows(IReadOnlyList<Spellbook.SpellRow> rows, Character c, Func<short, Item?> item)
    {
        if (rows.Count == 0)
            return new List<Control> { new TextBlock { Text = Spellbook.NoSpellsYet } };
        return rows.Select(r => (Control)Row(r, Spellbook.ShownBold(c, r.Spell, item))).ToList();
    }

    private static Grid Row(Spellbook.SpellRow r, bool bold)
    {
        // lblName, lblGuild, lblLevel and lblPoints, left to right as the form places them in
        // elaSpells (MORDOR.EXE RCDATA 10): lblName 30 twips in, lblGuild 1815 and 420 wide,
        // lblLevel 2265 and 420 wide, lblPoints 2775 and 525 wide, the last three right-justified,
        // all 210 tall. lblName is wider than the gap to lblGuild, which is brought in front of it
        // (FRMCHAR3.BAS:363), so a long name is cut where Guild begins. The list's panel starts
        // 30 twips in, so the columns run from lblName's left.
        var weight = bold ? FontWeight.Bold : FontWeight.Normal;
        // 210 twips tall in the form; the port's row is the Options font's line and RowPad (ObjectsForm.SpellRowPitch)
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("119,30,34,35"), Height = ObjectsForm.SpellRowPitch, ClipToBounds = true };
        void Cell(int col, string text, double right = -1)
        {
            var t = new TextBlock { Text = text, FontWeight = weight, [Grid.ColumnProperty] = col, VerticalAlignment = VerticalAlignment.Center };
            if (right >= 0) { t.HorizontalAlignment = HorizontalAlignment.Right; t.Margin = new Avalonia.Thickness(0, 0, right, 0); }
            else t.ClipToBounds = true;
            g.Children.Add(t);
        }
        Cell(0, r.Spell.Name.TrimEnd());
        Cell(1, r.GuildText, right: 2);    // lblGuild ends 1815 + 420 = 2235, 30 short of lblLevel
        Cell(2, r.Level.ToString(), right: 6);   // lblLevel ends 2685, 90 short of lblPoints
        Cell(3, r.PointsText, right: 0);
        return g;
    }
}
