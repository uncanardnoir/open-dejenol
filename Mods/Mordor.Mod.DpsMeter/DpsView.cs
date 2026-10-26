using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.DpsMeter;

/// <summary>The window's two modes: what the bars measure.</summary>
public enum DpsMode
{
    /// <summary>The damage dealt, split Physical | Magical | Companion, with the kills.</summary>
    Damage,
    /// <summary>The experience gained, split Damage | Chests (<see cref="ExperienceSource"/>).</summary>
    Xp,
}

/// <summary>
/// The DPS Meter window, drawn as the game's own windows are: two tab-like buttons at the top,
/// Damage | XP, for what the bars measure (remembered in the mod's settings), then one row per
/// party member, in party order - the name, a horizontal bar and, in Damage mode, the kills - under
/// a bold heading line.
///
/// Damage: each bar is split into Physical | Magical | Companion in that order and colour
/// (<see cref="Physical"/> and the rest, the port's bar colours, <see cref="BarColours"/>). XP: the
/// experience the character's total went up by, split into Damage (every award for damage dealt:
/// their own blows, spells and charms and their companions' damage, credited to them at a quarter)
/// and Chests (a chest's trap disarmed), <see cref="XpDamage"/> and <see cref="XpChests"/>. The
/// longest bar (the top figure) is the full width and the rest in proportion. The total is written
/// over the bar in plain text, as the Party window's Next column writes over its experience bar: the
/// bar at half strength over its dark track, the figure in the Party window's white, no shadow. A
/// click on a row selects it - its name in the Party window's selected green, bold, as the game
/// marks its selected member - and shows that character's shares at the bottom
/// (<see cref="Breakdown"/>, <see cref="XpBreakdown"/>), beside Reset, which clears every tally,
/// damage and experience, at once.
///
/// The text has no font of its own: the window's, which the game gives every mod window (its list
/// rows' face and size, Party Information's names, through the Options fonts), and the heading line
/// is bold, as Party Information's header is. A row is that font's line and three pixels, as every
/// list row of the port's (UiFonts.RowPad), so a larger Options font makes taller rows. The
/// window's background is the game's (the host sets it).
///
/// It follows the tallies live (<see cref="DpsTally.Changed"/>) and the party
/// (<see cref="ModGame.Changed"/>). Nothing here takes the keyboard. The game does not guard the
/// handlers of a mod's own controls, so each catches its own failure and reports it to the mod's log.
///
/// Tagged controls (Tag), for the headless check: <c>ModeDamage</c>, <c>ModeXp</c> (the two
/// buttons); each row <c>Bar:{name}</c>, inside it <c>Name</c>, the segments (<c>Physical</c>,
/// <c>Magical</c>, <c>Companion</c>, or <c>XpDamage</c>, <c>XpChests</c>), <c>Total</c> (the figure
/// over them) and <c>Kills</c> (empty in XP mode); the heading's <c>KillsHeader</c>;
/// <c>Breakdown</c>, <c>Reset</c>.
/// </summary>
public static class DpsView
{
    // the kinds' colours, from the port's bar colours (BarColours) and nowhere else
    /// <summary>Physical: the experience bar's green toward the next level (the QoL Next column's).</summary>
    public static readonly (byte R, byte G, byte B) Physical = BarColours.ToNext;
    /// <summary>Magical: the spell points bar's full blue (the QoL HP/SP column's).</summary>
    public static readonly (byte R, byte G, byte B) Magical = BarColours.SpFull;
    /// <summary>Companion: the experience bar's purple toward the pin (the QoL Next column's).</summary>
    public static readonly (byte R, byte G, byte B) Companion = BarColours.ToPin;
    /// <summary>XP from damage: the experience bar's green toward the next level.</summary>
    public static readonly (byte R, byte G, byte B) XpDamage = BarColours.ToNext;
    /// <summary>XP from chests: the hits bar's half-way yellow.</summary>
    public static readonly (byte R, byte G, byte B) XpChests = BarColours.HitsHalf;

    private static Color Rgb((byte R, byte G, byte B) c, byte alpha = 255) => Color.FromArgb(alpha, c.R, c.G, c.B);

    // the game's text colours, the Party window's (PartyRowColours): its plain white, its selected
    // member's green, and the blue of its fixed captions
    private static readonly IBrush Plain = Brushes.WhiteSmoke;
    private static readonly IBrush Selected = Brushes.LightGreen;
    private static readonly IBrush Caption = Brushes.CornflowerBlue;
    private static readonly IBrush Track = new SolidColorBrush(Rgb(BarColours.Track));
    private static readonly IBrush Over = new SolidColorBrush(Rgb(BarColours.TextOver));

    /// <summary>The space a row keeps above and below its text together: the port's list rows' (UiFonts.RowPad).</summary>
    public const double RowPad = 3;

    /// <summary>The mod setting the mode is kept under: "Damage" or "XP".</summary>
    public const string ModeKey = "Mode";

    /// <summary>A mode as it is kept, and back (anything else is Damage).</summary>
    public static string ModeText(DpsMode m) => m == DpsMode.Xp ? "XP" : "Damage";
    public static DpsMode ModeOf(string? text) => string.Equals(text?.Trim(), "XP", StringComparison.OrdinalIgnoreCase) ? DpsMode.Xp : DpsMode.Damage;

    /// <summary>A figure as the window writes it: 1,234,567.</summary>
    public static string Number(long n) => n.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>A part's share of a whole, in whole per cent, half away from zero; 0 of nothing.</summary>
    public static long Percent(long part, long whole) =>
        whole <= 0 ? 0 : (long)Math.Round(100.0 * part / whole, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The damage shares for a character's tally. Each item's own spaces are no-break spaces, so a
    /// narrow window wraps the line between items, never inside one ("Companion 352 / (1%)").
    /// </summary>
    public static string Breakdown(DpsRow r) =>
        Item($"Physical {Number(r.Physical)} ({Percent(r.Physical, r.Total)}%)") + "   "
      + Item($"Magical {Number(r.Magical)} ({Percent(r.Magical, r.Total)}%)") + "   "
      + Item($"Companion {Number(r.Companion)} ({Percent(r.Companion, r.Total)}%)");

    /// <summary>The experience shares for a character's tally, wrapped as <see cref="Breakdown"/>.</summary>
    public static string XpBreakdown(DpsRow r) =>
        Item($"Damage {Number(r.XpDamage)} ({Percent(r.XpDamage, r.XpTotal)}%)") + "   "
      + Item($"Chests {Number(r.XpChests)} ({Percent(r.XpChests, r.XpTotal)}%)");

    private static string Item(string s) => s.Replace(' ', ' ');

    public const string NoneSelected = "Click a bar for more information.";

    /// <summary>
    /// A row's height for the text <paramref name="inside"/> is drawn in: the font's line, to the
    /// pixel above, as the game measures it (UiFonts.LineHeight), and <see cref="RowPad"/>.
    /// </summary>
    public static double RowHeight(Control inside)
    {
        var family = inside.GetValue(TextElement.FontFamilyProperty);
        double size = Math.Max(1, inside.GetValue(TextElement.FontSizeProperty));
        var ft = new FormattedText("Xgjy", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   new Typeface(family), size, null);
        return Math.Ceiling(ft.Height - 0.01) + RowPad;
    }

    /// <param name="getMode">The mode kept (the mod's settings, <see cref="ModeKey"/>); null starts in Damage.</param>
    /// <param name="setMode">Keep a mode chosen; null keeps it only while the window is open.</param>
    public static Control Build(ModGame game, DpsTally tally, Action<string, Exception> report,
                                Func<string?>? getMode = null, Action<string>? setMode = null)
    {
        void Safe(string what, Action act)
        {
            try { act(); }
            catch (Exception ex) { try { report("DPS Meter window: " + what, ex); } catch { } }
        }

        var empty = new TextBlock { Foreground = Plain, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(8, 6, 8, 0) };
        var rowsPanel = new StackPanel();
        // the names' and the kills' columns, each as wide as its widest cell in every row
        const string nameGroup = "DpsNames", killsGroup = "DpsKills";

        var breakdown = new TextBlock { Tag = "Breakdown", Foreground = Plain, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var reset = new Button { Tag = "Reset", Content = "Reset", Focusable = false, VerticalAlignment = VerticalAlignment.Center };

        DpsMode mode = DpsMode.Damage;
        try { mode = ModeOf(getMode?.Invoke()); } catch (Exception ex) { try { report("DPS Meter window: reading the mode", ex); } catch { } }

        // the two modes as a pair of tab-like buttons: the one shown pressed and bold
        ToggleButton Tab(string text, string tag) => new()
        {
            Tag = tag, Content = text, Focusable = false,
            Padding = new Thickness(10, 1), MinHeight = 0,
            CornerRadius = new CornerRadius(0),
            Margin = new Thickness(0, 0, 1, 0),
        };
        var damageTab = Tab("Damage", "ModeDamage");
        var xpTab = Tab("XP", "ModeXp");
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 0), Children = { damageTab, xpTab } };

        string? selected = null;
        string drawn = "\0";

        void ShowTabs()
        {
            damageTab.IsChecked = mode == DpsMode.Damage;
            xpTab.IsChecked = mode == DpsMode.Xp;
            damageTab.FontWeight = mode == DpsMode.Damage ? FontWeight.Bold : FontWeight.Normal;
            xpTab.FontWeight = mode == DpsMode.Xp ? FontWeight.Bold : FontWeight.Normal;
        }

        void ShowBreakdown()
        {
            breakdown.Text = selected == null ? NoneSelected
                : mode == DpsMode.Xp ? XpBreakdown(tally.For(selected)) : Breakdown(tally.For(selected));
        }

        // a line of three columns: name, bar, kills
        Grid Line(Control name, Control bar, Control kills)
        {
            var line = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = nameGroup },
                    new ColumnDefinition(1, GridUnitType.Star),
                    new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = killsGroup },
                },
            };
            Grid.SetColumn(bar, 1);
            Grid.SetColumn(kills, 2);
            line.Children.Add(name);
            line.Children.Add(bar);
            line.Children.Add(kills);
            return line;
        }

        Control Heading(double height) => new Border
        {
            Height = height,
            Child = Line(
                new TextBlock { Margin = new Thickness(0, 0, 8, 0) },
                new TextBlock { Text = mode == DpsMode.Xp ? "Experience" : "Damage", Foreground = Caption, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Tag = "KillsHeader", Text = mode == DpsMode.Xp ? "" : "Kills", Foreground = Caption, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = mode == DpsMode.Xp ? new Thickness(0) : new Thickness(8, 0, 0, 0) }),
        };

        // a row: each kind's amount in its colour under the total, the top figure the full width
        Control BarRow(string name, DpsRow r, long top, bool isSelected, double height)
        {
            static ColumnDefinition Star(double v) => new(Math.Max(0, v), GridUnitType.Star);
            var parts = mode == DpsMode.Xp
                ? new[] { ("XpDamage", r.XpDamage, XpDamage), ("XpChests", r.XpChests, XpChests) }
                : new[] { ("Physical", r.Physical, Physical), ("Magical", r.Magical, Magical), ("Companion", r.Companion, Companion) };
            long sum = mode == DpsMode.Xp ? r.XpTotal : r.Total;
            double t = top <= 0 ? 1 : top;
            var segments = new Grid();
            for (int i = 0; i < parts.Length; i++)
            {
                var (id, amount, colour) = parts[i];
                segments.ColumnDefinitions.Add(Star(amount / t));
                var b = new Border { Tag = id, Background = new SolidColorBrush(Rgb(colour, BarColours.UnderTextAlpha)) };
                Grid.SetColumn(b, i);
                segments.Children.Add(b);
            }
            segments.ColumnDefinitions.Add(Star(top <= 0 ? 1 : (top - sum) / t));

            var total = new TextBlock
            {
                Tag = "Total", Text = Number(sum), Foreground = Over,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0),
            };
            var bar = new Panel
            {
                Margin = new Thickness(0, 1),
                Children = { new Border { Background = Track, CornerRadius = new CornerRadius(1) }, segments, total },
            };

            var label = new TextBlock
            {
                Tag = "Name", Text = name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
                Foreground = isSelected ? Selected : Plain, FontWeight = isSelected ? FontWeight.Bold : FontWeight.Normal,
            };
            var kills = new TextBlock
            {
                Tag = "Kills", Text = mode == DpsMode.Xp ? "" : Number(r.Kills), Foreground = Plain,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = mode == DpsMode.Xp ? new Thickness(0) : new Thickness(8, 0, 0, 0),
            };

            var row = new Border
            {
                Tag = "Bar:" + name, Child = Line(label, bar, kills), Height = height,
                Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(row, mode == DpsMode.Xp ? XpBreakdown(r) : Breakdown(r));
            row.PointerPressed += (_, e) => Safe("selecting a bar", () =>
            {
                if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed) return;
                selected = name;
                e.Handled = true;
                Look(true);
            });
            return row;
        }

        void Look(bool force)
        {
            var members = game.Party?.Members.Select(m => m.Name.Trim()).Where(n => n != "").ToList() ?? new List<string>();
            if (selected != null && !members.Contains(selected, StringComparer.OrdinalIgnoreCase)) selected = null;
            var rows = members.Select(n => (Name: n, Row: tally.For(n))).ToList();
            double height = RowHeight(rowsPanel);
            // what the window shows: redrawn only when that changes (the party is drawn again on every blow)
            string now = string.Join("|", rows.Select(x => $"{x.Name}:{x.Row}")) + "#" + selected + "#" + mode + "#" + height.ToString(CultureInfo.InvariantCulture);
            if (!force && now == drawn) return;
            drawn = now;

            ShowTabs();
            rowsPanel.Children.Clear();
            empty.IsVisible = rows.Count == 0;
            empty.Text = game.Party == null ? "No party in play. In town or in the dungeon this shows the party's damage dealt and experience gained." : "";
            if (rows.Count > 0) rowsPanel.Children.Add(Heading(height));
            long top = rows.Count == 0 ? 0 : rows.Max(x => mode == DpsMode.Xp ? x.Row.XpTotal : x.Row.Total);
            foreach (var (name, row) in rows)
                rowsPanel.Children.Add(BarRow(name, row, top, string.Equals(name, selected, StringComparison.OrdinalIgnoreCase), height));
            ShowBreakdown();
        }

        void Switch(DpsMode to)
        {
            mode = to;
            try { setMode?.Invoke(ModeText(to)); } catch (Exception ex) { try { report("DPS Meter window: keeping the mode", ex); } catch { } }
            Look(true);
        }
        damageTab.Click += (_, _) => Safe("Damage", () => Switch(DpsMode.Damage));
        xpTab.Click += (_, _) => Safe("XP", () => Switch(DpsMode.Xp));
        // Reset clears every tally at once, damage and experience
        reset.Click += (_, _) => Safe("Reset", () => { tally.Reset(); Look(true); });

        var scroll = new ScrollViewer
        {
            Content = rowsPanel, Margin = new Thickness(8, 4, 8, 4),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetIsSharedSizeScope(rowsPanel, true);

        var bottom = new DockPanel { Margin = new Thickness(8, 4, 8, 6), LastChildFill = true };
        reset.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(reset, Dock.Right);
        bottom.Children.Add(reset);
        bottom.Children.Add(breakdown);

        var root = new DockPanel();
        DockPanel.SetDock(tabs, Dock.Top);
        DockPanel.SetDock(empty, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(tabs);
        root.Children.Add(empty);
        root.Children.Add(bottom);
        root.Children.Add(scroll);

        Action changed = () => Dispatcher.UIThread.Post(() => Safe("redrawing", () => Look(false)));
        root.AttachedToVisualTree += (_, _) => Safe("opening", () =>
        {
            game.Changed -= changed; game.Changed += changed;
            tally.Changed -= changed; tally.Changed += changed;
            Look(true);
        });
        root.DetachedFromVisualTree += (_, _) => { game.Changed -= changed; tally.Changed -= changed; };
        return root;
    }
}
