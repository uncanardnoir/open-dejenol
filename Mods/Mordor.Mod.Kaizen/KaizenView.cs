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

namespace Mordor.Mod.Kaizen;

/// <summary>
/// The Kaizen window: the buff list of the character in command, edited by drag and drop as the
/// spell buffers are set. A heading naming whose list it is, a grey line with the gestures
/// (<see cref="Gestures"/>; the orange note while "Buff on entering the dungeon" is off), then one line per row - <c>1. Fire Shield → Oto</c>,
/// <c>2. Mass Protection (party)</c> - with the stale ones marked. Another character taking command
/// shows theirs. Every change is written at once (<see cref="BuffLists"/>).
///
/// - Add: a line of that character's Spells list or pack (Objects &amp; Spells, in town or in the
///   dungeon) dragged onto the window - the game's own drag (<see cref="ModDragged"/>) - lands at
///   the gap under the pointer, or at the end. Only what <see cref="Buffs.Judge"/> takes; a spell
///   (or an item's spell) that asks "Cast on Who?" asks it at once, in the game's own box
///   (<see cref="ModGame.AskCastOnWho"/>), and Cancel cancels the drop.
/// - Move: a row dragged up or down within the window. Remove: a row dragged out of the window and
///   let go there, or Remove on its right-click menu. Target: a double-click on a row (or Change
///   target on its menu) asks "Cast on Who?" again.
///
/// Nothing here takes the keyboard. Drawn in the child window the game gives a mod (its list rows'
/// face and size, Objects &amp; Spells' items, through the Options fonts; the Mods page's colours).
/// The game does not guard the handlers of a mod's own controls, so each catches its own failure and reports it to the mod's log.
/// </summary>
internal static class KaizenView
{
    private static readonly IBrush Heading = Brush.Parse("#8fb0ff");
    private static readonly IBrush Faint = Brush.Parse("#9a9aae");
    private static readonly IBrush Warn = Brush.Parse("#e0a050");

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>A row being dragged within the window: which, where the press was, and whether it has moved yet.</summary>
    private sealed class RowDrag
    {
        public required int From { get; init; }
        public required Point Start { get; init; }
        public bool Moving { get; set; }
    }

    /// <summary>The window's one line under the heading: how its rows are made, moved, retargeted and removed.</summary>
    public const string Gestures = "Drag a spell from the Spells list or an item from their pack to add a row. Drag a row up or down to move it. "
                                 + "Double-click a row to change its target. Drag a row out of the window or right-click to remove it.";

    public static Control Build(ModGame game, BuffLists lists, ModSettings settings, Action<string, Exception> report)
    {
        void Safe(string what, Action act)
        {
            try { act(); }
            catch (Exception ex) { try { report("Kaizen window: " + what, ex); } catch { } }
        }
        async void SafeAsync(string what, Func<Task> act)
        {
            try { await act(); }
            catch (Exception ex) { try { report("Kaizen window: " + what, ex); } catch { } }
        }

        var title = new TextBlock { Foreground = Heading, FontWeight = FontWeight.Bold };
        var note = new TextBlock { Foreground = Faint, TextWrapping = TextWrapping.Wrap };
        var help = new TextBlock { Foreground = Faint, TextWrapping = TextWrapping.Wrap };
        var off = new TextBlock
        {
            Foreground = Warn, TextWrapping = TextWrapping.Wrap, IsVisible = false,
            Text = "\"Buff on entering the dungeon\" is off: the lists are kept, and cast once it is ticked on the Mods page.",
        };
        // a refused drop's one line, or what letting go of a dragged row now would do
        var said = new TextBlock { Foreground = Warn, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var rowsPanel = new StackPanel();
        // the gap below the last row, marked like a row's top edge when a drop would land there
        var endGap = new Border { MinHeight = 8, BorderThickness = new Thickness(0, 2, 0, 0), BorderBrush = Brushes.Transparent };

        Character? shown = null;
        string partyKey = "";
        var rows = new List<BuffRow>();
        var rowViews = new List<Border>();
        RowDrag? drag = null;
        Control? rootRef = null;
        ScrollViewer? scrollRef = null;
        // a "Cast on Who?" box of this window's is up: one at a time - a double-click, the menu or a
        // drop meanwhile does nothing
        bool asking = false;

        void Say(string? text)
        {
            said.Text = text ?? "";
            said.IsVisible = !string.IsNullOrEmpty(text);
        }

        void Save(List<BuffRow> now)
        {
            if (shown == null) return;
            rows = now;
            lists.Set(shown.Name, rows);
        }

        // the gap a drop at this point lands in: above the row whose upper half it is over, and
        // below the last row, or anywhere off the rows' visible area (the heading over a list scrolled
        // down, where a hidden row lies under the pointer), the end. Given the event's GetPosition.
        int GapAt(Func<Visual, Point> at)
        {
            if (scrollRef == null || !new Rect(scrollRef.Bounds.Size).Contains(at(scrollRef))) return rowViews.Count;
            var p = at(rowsPanel);
            if (p.Y < 0 || p.Y > rowsPanel.Bounds.Height) return rowViews.Count;
            for (int i = 0; i < rowViews.Count; i++)
            {
                var b = rowViews[i].Bounds;
                if (p.Y < b.Y + b.Height / 2) return i;
            }
            return rowViews.Count;
        }

        // the gap marked with a line, or none (-1)
        void Mark(int gap)
        {
            for (int i = 0; i < rowViews.Count; i++) rowViews[i].BorderBrush = i == gap ? Heading : Brushes.Transparent;
            endGap.BorderBrush = gap == rowViews.Count && gap >= 0 ? Heading : Brushes.Transparent;
        }

        void Draw()
        {
            drag = null;
            rowsPanel.Children.Clear();
            rowViews.Clear();
            if (shown == null)
            {
                title.Text = "Kaizen";
                note.Text = "No party in play. In town or in the dungeon this shows the buff list of the character in command.";
                note.IsVisible = true;
                help.IsVisible = false;
                return;
            }
            string who = shown.Name.TrimEnd();
            title.Text = $"Buff list of \"{who}\"";
            // the heading and one line of the gestures; the note is only for an empty window
            note.IsVisible = false;
            help.Text = Gestures;
            help.IsVisible = true;
            rows = lists.For(shown.Name).ToList();
            if (rows.Count == 0)
                rowsPanel.Children.Add(new TextBlock { Text = "No rows yet.", Foreground = Faint, Margin = new Thickness(2, 4) });
            for (int r = 0; r < rows.Count; r++)
            {
                var v = RowView(r);
                rowViews.Add(v);
                rowsPanel.Children.Add(v);
            }
            rowsPanel.Children.Add(endGap);
            Mark(-1);
        }

        // "1. Fire Shield → Oto": the number, the spell or item, and its target - "(party)" for one
        // that asks nobody - with what no longer holds marked as the drop-downs had it
        Border RowView(int at)
        {
            var row = rows[at];
            var c = shown!;
            var text = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
            var runs = new InlineCollection();
            text.Inlines = runs;
            runs.Add(new Run($"{at + 1}. {row.Name.Trim()}"));
            if (row.Kind == BuffKind.Spell)
            {
                if (!game.KnownSpells(c).Any(s => Same(s.Name, row.Name))) runs.Add(new Run(" (not known now)") { Foreground = Warn });
            }
            else if (Buffs.ItemNamed(c, row.Name, game.Item) is { Known: IdLevel.All }) runs.Add(new Run(" (item)"));
            else runs.Add(new Run(" (item, not in the pack now)") { Foreground = Warn });

            bool? asks = Buffs.AsksTarget(row, c, game.Spells, game.Item);
            if (asks == false) runs.Add(new Run(" (party)"));
            else if (asks == true && row.Target.Trim() == "") { runs.Add(new Run(" → ")); runs.Add(new Run("(no target)") { Foreground = Warn }); }
            else if (row.Target.Trim() != "")
            {
                runs.Add(new Run(" → " + row.Target.Trim()));
                if (game.Party == null || Buffs.Member(game.Party.Members, row.Target) == null)
                    runs.Add(new Run(" (not in the party)") { Foreground = Warn });
            }

            var b = new Border
            {
                Child = text, Background = Brushes.Transparent, Padding = new Thickness(2, 1),
                BorderThickness = new Thickness(0, 2, 0, 0), BorderBrush = Brushes.Transparent,
            };

            var change = new MenuItem { Header = "Change target", IsEnabled = asks != false };
            change.Click += (_, _) => SafeAsync("changing a target", () => Retarget(at));
            var remove = new MenuItem { Header = "Remove" };
            remove.Click += (_, _) => Safe("removing a row", () => { Save(BuffEdits.Remove(rows, at)); Dispatcher.UIThread.Post(() => Safe("redrawing", Draw)); });
            var menu = new ContextMenu();
            menu.Items.Add(change);
            menu.Items.Add(remove);
            b.ContextMenu = menu;

            b.DoubleTapped += (_, _) =>
            {
                drag = null;
                Dispatcher.UIThread.Post(() => SafeAsync("changing a target", () => Retarget(at)));
            };

            // a row's own drag: a left press, then the pointer held and moved past the drag distance;
            // the pointer is captured, so a let-go outside the window is seen too
            b.PointerPressed += (_, e) => Safe("pressing a row", () =>
            {
                var pt = e.GetCurrentPoint(b);
                drag = pt.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed && e.ClickCount < 2 && rootRef != null
                    ? new RowDrag { From = at, Start = e.GetPosition(rootRef) } : null;
            });
            b.PointerMoved += (_, e) => Safe("dragging a row", () =>
            {
                if (drag is not { } d || d.From != at || rootRef == null) return;
                if (!e.GetCurrentPoint(b).Properties.IsLeftButtonPressed) { drag = null; return; }
                var p = e.GetPosition(rootRef);
                if (!d.Moving)
                {
                    // the game's own drag distance (Dragging.DragDistance): the system's drag rectangle,
                    // four pixels either way when the platform does not say
                    var room = TopLevel.GetTopLevel(b)?.PlatformSettings?.GetTapSize(PointerType.Mouse) is { Width: > 0, Height: > 0 } s
                        ? s : new Size(4, 4);
                    if (Math.Abs(p.X - d.Start.X) < room.Width && Math.Abs(p.Y - d.Start.Y) < room.Height) return;
                    d.Moving = true;
                    e.Pointer.Capture(b);
                }
                bool inside = new Rect(rootRef.Bounds.Size).Contains(p);
                b.Opacity = inside ? 1 : 0.4;
                Mark(inside ? GapAt(v => e.GetPosition(v)) : -1);
                Say(inside ? null : $"Let go here to remove row {at + 1}.");
            });
            b.PointerReleased += (_, e) => Safe("letting go of a row", () =>
            {
                if (drag is not { Moving: true } d || d.From != at || rootRef == null) { drag = null; return; }
                drag = null;
                var p = e.GetPosition(rootRef);
                bool inside = new Rect(rootRef.Bounds.Size).Contains(p);
                int gap = GapAt(v => e.GetPosition(v));
                e.Pointer.Capture(null);
                e.Handled = true;
                Say(null);
                Save(inside ? BuffEdits.Move(rows, at, gap) : BuffEdits.Remove(rows, at));
                // the row taking the event is one of those drawn again: let the event finish first
                Dispatcher.UIThread.Post(() => Safe("redrawing", Draw));
            });
            b.PointerCaptureLost += (_, _) => Safe("dropping a row's drag", () =>
            {
                if (drag is not { Moving: true } d || d.From != at) return;
                drag = null;
                b.Opacity = 1;
                Mark(-1);
                Say(null);
            });
            return b;
        }

        // "Cast on Who?" again for a row, in the game's own box; Cancel keeps the target
        async Task Retarget(int at)
        {
            if (shown is not { } owner || asking || at >= rows.Count) return;
            var row = rows[at];
            Say(null);
            var spell = Buffs.RowSpell(row, owner, game.Spells, game.Item);
            if (spell == null)
            {
                Say(row.Kind == BuffKind.Item ? $"\"{row.Name.Trim()}\" is not in the pack fully identified now: its target cannot be asked."
                                              : $"There is no spell called \"{row.Name.Trim()}\".");
                return;
            }
            if (SpellThrown.WhoToAsk(spell) == SpellThrown.Ask.None) { Say($"\"{row.Name.Trim()}\" asks nobody: it is cast on the whole party."); return; }
            Character? who;
            asking = true;
            try { who = await game.AskCastOnWho(spell, rootRef, row.Target); }   // the box starts on the row's target
            finally { asking = false; }
            // the one in command or the list changed while the box was up: the answer is for nothing now
            if (who == null || !ReferenceEquals(shown, owner)) return;
            var now = lists.For(owner.Name).ToList();
            if (at >= now.Count || now[at] != row) return;
            Save(BuffEdits.Retarget(now, at, who.Name));
            Draw();
        }

        // a line of the game's own lists let go over the window: judged, its target asked if its
        // spell asks one, and put in at the gap it was let go at
        async Task TakeDrop(ModDragged d, int gap)
        {
            if (shown is not { } owner || asking) return;
            Say(null);
            // the list as the gap was measured on: if it changes while the box is up, the gap means nothing
            var before = lists.For(owner.Name);
            BuffRow? row;
            string? refusal;
            asking = true;
            try
            {
                (row, refusal) = await Buffs.Take(d, owner, game.KnownSpells(owner), game.Spells, game.Item,
                                                  s => game.AskCastOnWho(s, rootRef));
            }
            finally { asking = false; }
            if (refusal != null) { Say(refusal); return; }
            if (row == null || !ReferenceEquals(shown, owner)) return;
            var now = lists.For(owner.Name);
            Save(BuffEdits.Insert(now, BuffEdits.GapAfter(before, now, gap), row));
            Draw();
        }

        // drawn again only when the one in command or the party's line-up has changed: the game
        // says "look again" on every repaint of the party, a blow included
        void Look(bool force)
        {
            off.IsVisible = !settings.Get(KaizenMod.BuffOnEntry);
            var c = game.Selected;
            string key = game.Party == null ? "" : string.Join("\n", game.Party.Members.Select(m => m.Name.Trim()));
            if (!force && ReferenceEquals(c, shown) && key == partyKey) return;
            if (!ReferenceEquals(c, shown)) Say(null);
            shown = c;
            partyKey = key;
            Draw();
        }

        var top = new StackPanel { Spacing = 2, Margin = new Thickness(8, 6, 8, 6), Children = { title, help, note, off, said } };
        var scroll = new ScrollViewer
        {
            Content = rowsPanel, Margin = new Thickness(8, 0, 4, 6),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        scrollRef = scroll;
        // a background, so a drop anywhere in the window is seen (empty space included)
        var root = new DockPanel { Background = Brushes.Transparent };
        rootRef = root;
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(scroll);

        DragDrop.SetAllowDrop(root, true);
        root.AddHandler(DragDrop.DragOverEvent, (_, e) => Safe("a drag over the window", () =>
        {
            bool takes = shown != null && !asking && e.Data.Get(ModDragged.Format) is ModDragged;
            e.DragEffects = takes ? DragDropEffects.Link : DragDropEffects.None;
            Mark(takes ? GapAt(v => e.GetPosition(v)) : -1);
            e.Handled = true;
        }));
        root.AddHandler(DragDrop.DragLeaveEvent, (_, _) => Safe("a drag leaving the window", () => Mark(-1)));
        root.AddHandler(DragDrop.DropEvent, (_, e) => Safe("a drop on the window", () =>
        {
            Mark(-1);
            e.Handled = true;
            if (shown == null || asking || e.Data.Get(ModDragged.Format) is not ModDragged d) return;
            int gap = GapAt(v => e.GetPosition(v));
            // the box is not opened inside the drop: the drag's own loop is still running
            Dispatcher.UIThread.Post(() => SafeAsync("taking a drop", () => TakeDrop(d, gap)));
        }));

        Action changed = () => Dispatcher.UIThread.Post(() => Safe("redrawing", () => Look(false)));
        Action<string> settingChanged = _ => changed();
        root.AttachedToVisualTree += (_, _) => Safe("opening", () =>
        {
            game.Changed -= changed;
            game.Changed += changed;
            // "Buff on entering the dungeon" ticked or cleared on the Mods page: the note follows
            settings.Changed -= settingChanged;
            settings.Changed += settingChanged;
            Look(true);
        });
        root.DetachedFromVisualTree += (_, _) => { game.Changed -= changed; settings.Changed -= settingChanged; };
        return root;
    }
}
