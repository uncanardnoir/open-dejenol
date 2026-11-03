using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.Mod.AutoCombat;

/// <summary>
/// The Auto-combat window: the ordered rules, one line each, and an editor for the one picked.
/// Everything is chosen with the mouse - drop-downs and buttons, none focusable - because a mod
/// window takes no keyboard: the game's frame sees every key first, and a letter typed into a box
/// here would be a dungeon command (f is Fight). The drop-downs offer only what the game shows:
/// the party's names, each member's known combat spells, pack items known at All that hold one,
/// the names the monster list has shown in a fight while the mod ran (<see cref="AutoCombatMod.SeenKey"/>)
/// and the monster classes the list names an unidentified group by.
/// Every change is written at once (<see cref="AutoCombatMod.Write"/>). The game does not guard the
/// handlers of a mod's own controls, so each catches its own failure and reports it to the mod's log.
/// </summary>
internal static class AutoCombatView
{
    private static readonly IBrush Heading = Brush.Parse("#8fb0ff");
    private static readonly IBrush Faint = Brush.Parse("#9a9aae");
    private static readonly IBrush Warn = Brush.Parse("#e0a050");
    private static readonly IBrush Picked = Brush.Parse("#7fdc7f");

    /// <summary>The window's line under the heading.</summary>
    public const string Help = "Each round of a fight, every member takes the order of the first rule that fits them. "
                             + "Orders go through the game's own keys and Cast; heals and cures cannot be cast in a fight.";

    private const string Unset = "(not asked)";
    private const string Choose = "(choose)";
    private const string Anyone = "Anyone";
    private static readonly string[] Percents = { "10", "20", "25", "30", "40", "50", "60", "70", "75", "80", "90", "100" };
    private static readonly string[] Points = { "1", "2", "3", "5", "10", "15", "20", "25", "30", "40", "50", "75", "100", "150", "200", "300", "500", "1000" };
    private static readonly string[] GroupCounts = { "1", "2", "3", "4" };

    public static Control Build(ModGame game, ModSettings settings, Action<string, Exception> report)
    {
        void Safe(string what, Action act)
        {
            try { act(); }
            catch (Exception ex) { try { report("Auto-combat window: " + what, ex); } catch { } }
        }

        var title = new TextBlock { Foreground = Heading, FontWeight = FontWeight.Bold, Text = "Auto-combat rules" };
        var help = new TextBlock { Foreground = Faint, TextWrapping = TextWrapping.Wrap, Text = Help };
        var off = new TextBlock
        {
            Foreground = Warn, TextWrapping = TextWrapping.Wrap, IsVisible = false,
            Text = "\"Fight by the rules\" is off: the rules are kept, and used once it is ticked on the Mods page.",
        };
        var rowsPanel = new StackPanel();
        var editor = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        int picked = -1;
        bool building = false;

        List<Rule> rules = AutoCombatMod.Read(settings);

        void Save() => AutoCombatMod.Write(settings, rules);

        Button Btn(string text, Action act)
        {
            var b = new Button { Content = text, Focusable = false, Padding = new Thickness(6, 1), Margin = new Thickness(0, 0, 4, 0) };
            b.Click += (_, _) => Safe(text, act);
            return b;
        }

        // the names the game shows: the party in order
        IEnumerable<string> PartyNames() => game.Party?.Members.Select(m => m.Name.Trim()) ?? Enumerable.Empty<string>();

        IEnumerable<Character> WhoFor(Rule r) => game.Party?.Members.Where(m => r.Who == "" || string.Equals(m.Name.Trim(), r.Who.Trim(), StringComparison.OrdinalIgnoreCase))
                                                 ?? Enumerable.Empty<Character>();

        // the combat spells the rule's member knows (anyone's, for Anyone): the spell page's list,
        // less the heals and cures the game refuses in a fight
        IEnumerable<string> SpellNames(Rule r) => WhoFor(r)
            .SelectMany(m => game.KnownSpells(m))
            .Where(s => CastGate.UseOf(s) != SpellUse.NonCombat)
            .Select(s => s.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);

        // pack items known at All that hold such a spell: below All the game shows nothing of an item's spell
        IEnumerable<string> ItemNames(Rule r) => WhoFor(r)
            .SelectMany(m => Enumerable.Range(1, Character.PackSize - 1).Where(i => !m.SlotEmpty(i)).Select(i => m.Pack[i]!))
            .Where(s => s.Known == IdLevel.All && game.Item(s.ItemId) is Item it && ItemUse.SpellOf(it, game.Spells) is Spell sp
                        && CastGate.UseOf(sp) != SpellUse.NonCombat)
            .Select(s => game.Item(s.ItemId)!.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);

        // the names a monster list can show: those it has shown in a fight while the mod ran, the
        // monster classes (an unidentified group's name, fn0E23) and "Unseen Monster" - never a list
        // of monsters the party has not met
        IEnumerable<string> ShownNames() =>
            settings.GetRows(AutoCombatMod.SeenKey).Select(r => r.Length > 0 ? r[0].Trim() : "")
                .Concat(Mdata1.MonsterClasses.Select(c => c.Name.Trim())).Append("Unseen Monster")
                .Where(n => n != "").Distinct(StringComparer.OrdinalIgnoreCase);

        ComboBox Combo(IEnumerable<string> options, string current, Action<string> set)
        {
            var list = options.ToList();
            if (current != "" && !list.Contains(current, StringComparer.OrdinalIgnoreCase)) list.Insert(Math.Min(1, list.Count), current);
            var c = new ComboBox { ItemsSource = list, Focusable = false, MinWidth = 120, Margin = new Thickness(0, 0, 4, 0) };
            c.SelectedItem = list.FirstOrDefault(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
            c.SelectionChanged += (_, _) => Safe("a choice", () =>
            {
                if (building || c.SelectedItem is not string v) return;
                set(v);
                Save();
                // drawn again once the drop-down has closed: Draw takes this box out of the window
                Dispatcher.UIThread.Post(() => Safe("redrawing", Draw));
            });
            return c;
        }

        ComboBox Number(int? now, string[] steps, string suffix, Action<int?> set) =>
            Combo(new[] { Unset }.Concat(steps.Select(s => s + suffix)), now is int n ? n + suffix : Unset,
                  v => set(v == Unset ? null : int.Parse(v.TrimEnd('%'))));

        Control Line(string label, params Control[] controls)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            p.Children.Add(new TextBlock { Text = label, Width = 170, VerticalAlignment = VerticalAlignment.Center });
            foreach (var c in controls) p.Children.Add(c);
            return p;
        }

        void DrawEditor()
        {
            editor.Children.Clear();
            if (picked < 0 || picked >= rules.Count)
            {
                editor.Children.Add(new TextBlock { Foreground = Faint, Text = rules.Count == 0 ? "Add a rule to begin." : "Click a rule to change it." });
                return;
            }
            var r = rules[picked];
            editor.Children.Add(new TextBlock { Foreground = Heading, FontWeight = FontWeight.Bold, Text = $"Rule {picked + 1}" });
            editor.Children.Add(Line("For", Combo(new[] { Anyone }.Concat(PartyNames()), r.Who == "" ? Anyone : r.Who, v => r.Who = v == Anyone ? "" : v)));
            editor.Children.Add(Line("If someone's hits below", Number(r.PartyBelow, Percents, "%", v => r.PartyBelow = v)));
            editor.Children.Add(Line("If everyone's hits at least", Number(r.PartyAtLeast, Percents, "%", v => r.PartyAtLeast = v)));
            editor.Children.Add(Line("If own hits below", Number(r.SelfBelow, Percents, "%", v => r.SelfBelow = v)));
            editor.Children.Add(Line("If spell points at least", Number(r.SpAtLeast, Points, "", v => r.SpAtLeast = v)));
            editor.Children.Add(Line("If groups at least", Number(r.GroupsAtLeast, GroupCounts, "", v => r.GroupsAtLeast = v)));
            editor.Children.Add(Line("If groups at most", Number(r.GroupsAtMost, GroupCounts, "", v => r.GroupsAtMost = v)));
            editor.Children.Add(Line("If a group is shown as", Combo(new[] { Unset }.Concat(ShownNames()), r.Shown == "" ? Unset : r.Shown,
                                                                      v => r.Shown = v == Unset ? "" : v)));
            editor.Children.Add(Line("Do", Combo(Enum.GetNames<RuleAction>(), r.Do.ToString(), v => { r.Do = Enum.Parse<RuleAction>(v); r.What = ""; })));
            if (r.Do is RuleAction.Cast or RuleAction.Use)
            {
                var names = (r.Do == RuleAction.Cast ? SpellNames(r) : ItemNames(r)).ToList();
                editor.Children.Add(Line(r.Do == RuleAction.Cast ? "Spell" : "Item",
                    names.Count == 0 && r.What == ""
                        ? new TextBlock { Foreground = Warn, Text = r.Do == RuleAction.Cast ? "No combat spell known." : "No item at All with a combat spell." }
                        : Combo(r.What == "" ? names.Prepend(Choose) : names, r.What == "" ? Choose : r.What,
                                v => { if (v != Choose) r.What = v; })));
            }
            if (r.Do is RuleAction.Fight or RuleAction.Cast or RuleAction.Use)
            {
                var aims = new[] { "(add a target)", Rule.First, Rule.Last, Rule.Most, Rule.Fewest }.Concat(ShownNames());
                editor.Children.Add(Line("Aim at, in order",
                    new TextBlock { Text = r.Targets.Count == 0 ? "(the aim as it is)" : string.Join(", ", r.Targets), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }));
                editor.Children.Add(Line("", Combo(aims, "(add a target)", v => { if (!v.StartsWith('(') && !r.Targets.Contains(v)) r.Targets.Add(v); }),
                                         Btn("Clear", () => { r.Targets.Clear(); Save(); Draw(); })));
                editor.Children.Add(new TextBlock { Foreground = Faint, TextWrapping = TextWrapping.Wrap,
                    Text = "*most and *fewest go by the count the monster list shows; a name matches the list's own, so an unidentified group answers only to its class." });
            }
        }

        void Draw()
        {
            building = true;
            try
            {
                off.IsVisible = !settings.Get(AutoCombatMod.FightByRules);
                rowsPanel.Children.Clear();
                if (rules.Count == 0) rowsPanel.Children.Add(new TextBlock { Foreground = Faint, Text = "No rules yet." });
                for (int i = 0; i < rules.Count; i++)
                {
                    int at = i;
                    var t = new TextBlock
                    {
                        Text = $"{i + 1}. {rules[i].Describe()}", TextWrapping = TextWrapping.Wrap,
                        Foreground = i == picked ? Picked : null, FontWeight = i == picked ? FontWeight.Bold : FontWeight.Normal,
                    };
                    var row = new Border { Background = Brushes.Transparent, Child = t, Padding = new Thickness(0, 1) };
                    row.PointerPressed += (_, e) => Safe("picking a rule", () => { picked = at; Draw(); e.Handled = true; });
                    rowsPanel.Children.Add(row);
                }
                DrawEditor();
            }
            finally { building = false; }
        }

        void Move(int by)
        {
            if (picked < 0 || picked + by < 0 || picked + by >= rules.Count) return;
            (rules[picked], rules[picked + by]) = (rules[picked + by], rules[picked]);
            picked += by;
            Save(); Draw();
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                Btn("Add", () => { rules.Add(new Rule()); picked = rules.Count - 1; Save(); Draw(); }),
                Btn("Copy", () => { if (picked >= 0) { rules.Insert(picked + 1, rules[picked].Clone()); picked++; Save(); Draw(); } }),
                Btn("Up", () => Move(-1)),
                Btn("Down", () => Move(1)),
                Btn("Remove", () => { if (picked >= 0) { rules.RemoveAt(picked); picked = Math.Min(picked, rules.Count - 1); Save(); Draw(); } }),
            },
        };

        var body = new StackPanel { Spacing = 2, Margin = new Thickness(8, 6, 8, 6), Children = { title, help, off, rowsPanel, buttons, editor } };
        var root = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        // the editor's lists follow the party (a member joining, the one in command); the rules
        // themselves are re-read when a setting changes - the Mods page's toggle, or the rules
        // written by another window
        string partyKey = "";
        Action changed = () => Dispatcher.UIThread.Post(() => Safe("redrawing", () =>
        {
            string key = game.Party == null ? "" : string.Join("\n", game.Party.Members.Select(m => m.Name.Trim()));
            if (key == partyKey) return;
            partyKey = key;
            Draw();
        }));
        Action<string> settingChanged = k => Dispatcher.UIThread.Post(() => Safe("redrawing", () =>
        {
            if (k == AutoCombatMod.RulesKey)
            {
                var now = AutoCombatMod.Read(settings);
                if (now.Select(x => string.Join("|", x.ToCells())).SequenceEqual(rules.Select(x => string.Join("|", x.ToCells())))) return;
                rules = now;
                picked = Math.Min(picked, rules.Count - 1);
            }
            Draw();
        }));
        root.AttachedToVisualTree += (_, _) => Safe("opening", () =>
        {
            game.Changed -= changed; game.Changed += changed;
            settings.Changed -= settingChanged; settings.Changed += settingChanged;
            rules = AutoCombatMod.Read(settings);
            Draw();
        });
        root.DetachedFromVisualTree += (_, _) => { game.Changed -= changed; settings.Changed -= settingChanged; };
        return root;
    }
}
