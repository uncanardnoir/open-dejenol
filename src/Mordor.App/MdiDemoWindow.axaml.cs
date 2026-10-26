using Avalonia.Controls;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// A bench for <see cref="MdiHost"/>, reachable with <c>MORDOR_TEST=mdi</c>. It puts six windows
/// named after the real ones on a frame so dragging, resizing, rolling up, maximising, closing and
/// the stacking order can all be tried without disturbing the game.
/// </summary>
public sealed class MdiDemoWindow : Window
{
    public MdiDemoWindow()
    {
        Title = "OpenDejenol — MDI bench";
        Width = 1100;
        Height = 760;
        Background = Brush.Parse("#0a0a10");
        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");

        var host = new MdiHost();

        TextBlock Filler(string what) => new()
        {
            Text = what,
            Foreground = Brushes.Gainsboro,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        };

        // Two of these titles are not fixed text. The character window is captioned with the
        // character's own name in quotes, and the dungeon is captioned with whoever currently has
        // the focus - "Dungeon (<selected character>)" - so it re-titles when you switch with
        // Alt+1..4 or 'n'. The bench stands in for that with obvious placeholders.
        host.Add("\"<character>\"", Filler("The character window: seven tabs across the top, stats beneath."), 8, 8, 300, 330);
        host.Add("Dungeon (<selected character>)", Filler("The view, the arrow pad, the monsters and the command bar."), 320, 8, 520, 300);
        host.Add("Automap (Level 1)", Filler("The map, drawn from what has been recorded."), 320, 320, 330, 260);
        host.Add("Objects & Spells", Filler("1. Hands\n*  2. Bronze Dagger\n   3.\n   4."), 660, 320, 330, 260);
        host.Add("Party Information", Filler("Name   Hits   Spells  Status  Option"), 8, 350, 300, 150);
        host.Add("Messages", Filler("Everything the game has said this trip."), 8, 510, 640, 180);

        var reset = new Button { Content = "Adjust to Display", Focusable = false, Margin = new Avalonia.Thickness(4) };
        reset.Click += (_, _) => host.AdjustToDisplay();
        var show = new Button { Content = "Show Messages", Focusable = false, Margin = new Avalonia.Thickness(4) };
        show.Click += (_, _) => host.Show("Messages");

        Content = new DockPanel
        {
            Children =
            {
                new Border
                {
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Background = Brush.Parse("#14141c"),
                    Child = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Children = { reset, show },
                    },
                },
                host,
            },
        };
    }
}
