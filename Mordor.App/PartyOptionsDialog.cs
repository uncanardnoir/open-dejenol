using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// <c>frmPartyOptions</c>, from the help's screenshot of it (game_files/bm6.png, 230x151):
/// the party down the left, a "Defaults" frame down the right holding four radio buttons -
/// Nothing, Fight, Defend, Buffer - a buffer number under them, and one OK button.
///
/// The Party Information window's Options button shows it modally, "frmPartyOptions.Show
/// mc003C", whenever there are no monsters (FRMGROUP.BAS:37-42) - which is in the dungeon and in
/// the city alike, so both windows open this one copy.
///
/// The captions are the screenshot's. <c>gv058A$</c> (MODULE1.BAS:419-423) holds "None",
/// "Fight", "Defend", "Spell" and this port cited it for these buttons, but that table is not
/// what the form is captioned with: the picture plainly reads Nothing and Buffer.
///
/// It used to be a combo box per member with a paragraph of this port's own explaining what
/// combat settings are. The help says only "This is the default setting for the selected
/// character when combat starts. None, Fight, Defend, or a spell buffer can be selected."
/// </summary>
internal static class PartyOptionsDialog
{
    public static void Show(Window owner, Party party, Action closed)
    {
        var who = new ListBox
        {
            Width = 110,
            Background = Brush.Parse("#0a0a10"),
            ItemsSource = party.Members.Select(m => m.Name.TrimEnd()).ToList(),
            SelectedIndex = party.SelectedIndex,
        };

        var nothing = new RadioButton { Content = "Nothing", GroupName = "defaults" };
        var fight = new RadioButton { Content = "Fight", GroupName = "defaults" };
        var defend = new RadioButton { Content = "Defend", GroupName = "defaults" };
        var buffer = new RadioButton { Content = "Buffer", GroupName = "defaults" };

        // cmbBuffer, filled "For l003C = 0 To 9: cmbBuffer.AddItem CStr(l003C + 1)"
        // (FRMPINFO.BAS:35-37) - so it reads 1 to 10. The port had written 1..9 then 0 on the
        // grounds that Shift+0 fires the tenth, which is true of the key and not of this list.
        var whichBuffer = new ComboBox
        {
            Width = 70,
            ItemsSource = Enumerable.Range(1, SpellBuffers.Count).Select(i => i.ToString()).ToList(),
        };

        bool loading = false;
        Character Current() => party.Members[Math.Clamp(who.SelectedIndex, 0, party.Size - 1)];

        void Load()
        {
            loading = true;
            int s = Current().CombatSetting;
            nothing.IsChecked = s == 0;
            fight.IsChecked = s == 1;
            defend.IsChecked = s == 2;
            buffer.IsChecked = s >= 3;
            whichBuffer.SelectedIndex = s >= 3 ? Math.Clamp(s - 3, 0, SpellBuffers.Count - 1) : 0;
            whichBuffer.IsEnabled = s >= 3;
            loading = false;
        }

        void Store()
        {
            if (loading) return;
            var c = Current();
            c.CombatSetting = fight.IsChecked == true ? 1
                            : defend.IsChecked == true ? 2
                            : buffer.IsChecked == true ? 3 + Math.Max(0, whichBuffer.SelectedIndex)
                            : 0;
            whichBuffer.IsEnabled = buffer.IsChecked == true;
        }

        who.SelectionChanged += (_, _) => Load();
        foreach (var r in new[] { nothing, fight, defend, buffer }) r.IsCheckedChanged += (_, _) => Store();
        whichBuffer.SelectionChanged += (_, _) => Store();
        Load();

        var ok = new Button
        {
            Content = "OK",
            IsDefault = true,
            Width = 96,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        var defaults = new Border
        {
            BorderBrush = Brush.Parse("#444"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "Defaults", Foreground = Brushes.CornflowerBlue },
                    nothing, fight, defend, buffer, whichBuffer,
                },
            },
        };

        // frmPartyOptions' own font, MS Sans Serif 8.25 (lstCharacters), which its Form_Load puts
        // through sub0784 (FRMPINFO.BAS): the Options fonts, UiFonts. It had been the frame's Cascadia Mono
        var win = UiFonts.Set(new Window
        {
            Title = "Party Options",
            Width = 400, SizeToContent = SizeToContent.Height,
            Background = Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,12,Auto"),
                            Children = { who, new ContentControl { Content = defaults, [Grid.ColumnProperty] = 2 } },
                        },
                        ok,
                    },
                },
            },
        }, DesignFonts.PartyOptions, "lstCharacters");
        ok.Click += (_, _) => win.Close();
        win.KeyDown += (_, ke) => { if (ke.Key == Key.Escape) win.Close(); };
        win.Closed += (_, _) => closed();
        win.ShowDialog(owner);
    }
}

/// <summary>
/// The Party Information rows' colours, as frmParty paints them, carried onto the port's dark
/// background the way the rebuilt Store does it - the original's black list text reads as
/// WhiteSmoke, its red as IndianRed, its blue as CornflowerBlue.
/// </summary>
internal static class PartyRowColours
{
    /// <summary>QBColor(0): every Name and Hits label before the switch colours one (MULTIMOD.BAS:613-616).</summary>
    public static readonly IBrush Plain = Brushes.WhiteSmoke;
    /// <summary>QBColor(mc0116 = 2): the selected member's Name and Hits (MULTIMOD.BAS:640-641).</summary>
    public static readonly IBrush Selected = Brushes.LightGreen;
    /// <summary>QBColor(mc014C = 1): lblStatus reading "OK" (FRMGROUP.BAS:446-448).</summary>
    public static readonly IBrush Well = Brushes.CornflowerBlue;
    /// <summary>QBColor(mc014E = 4): lblStatus with Po/Di/Pa in it (FRMGROUP.BAS:449-450).</summary>
    public static readonly IBrush Ailing = Brushes.IndianRed;
    /// <summary>lblSpells and lblOption are never recoloured in code; they are blue in game_files/bm5.png.</summary>
    public static readonly IBrush Fixed = Brushes.CornflowerBlue;

    public static IBrush NameAndHits(bool selected) => selected ? Selected : Plain;
    /// <summary>Only lblName is made bold, "frmParty!lblName(p00FA).FontBold = True" (MULTIMOD.BAS:642).</summary>
    public static FontWeight NameWeight(bool selected) => selected ? FontWeight.Bold : FontWeight.Normal;
    public static IBrush Status(bool well) => well ? Well : Ailing;
}
