using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// Character creation, laid out like the original: name, race, sex and alignment on the left, the six
/// statistics with their race ranges in the middle, and the guilds you would be allowed to join on the
/// right. There are no dice. Your race gives you a fixed number of stat points; the game pre-spends
/// five on every stat and leaves you the rest, which is exactly the race's bonus value from MDATA1
/// (a Human is offered 9/9/9/11/10/11 with 5 points left, which is the minimums plus five each).
/// </summary>
public partial class CharacterCreationWindow : Window
{
    private readonly string _gameDir;
    private Race _race = RaceData.All[0];
    private int[] _stats = new int[6];
    private int[] _base = new int[6];
    private int _pool;

    /// <summary>Points the original spends for you on every stat before you touch anything.</summary>
    private const int FreePerStat = 5;

    public Character? Result { get; private set; }

    public CharacterCreationWindow(string gameDir)
    {
        _gameDir = gameDir;
        InitializeComponent();
        RaceBox.ItemsSource = RaceData.All.Select(r => r.Name).ToList();
        SexBox.ItemsSource = new[] { "Male", "Female" };
        SexBox.SelectedIndex = 0;
        RaceBox.SelectedIndex = 0; // triggers OnRaceChanged -> Roll
    }

    private void OnRaceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RaceBox.SelectedIndex < 0) return;
        _race = RaceData.All[RaceBox.SelectedIndex];
        AlignBox.ItemsSource = _race.Alignments.Select(a => a.ToString()).ToList();
        AlignBox.SelectedIndex = 0;
        Roll();
    }

    private void OnAlignChanged(object? sender, SelectionChangedEventArgs e) => RefreshGuilds();


    /// <summary>Start again: every stat at its race minimum plus five, the rest of the points in hand.</summary>
    private void Roll()
    {
        _stats = new int[6];
        _base = new int[6];
        for (int i = 0; i < 6; i++)
        {
            _base[i] = Math.Min(_race.Max[i], _race.Min[i] + FreePerStat);
            _stats[i] = _base[i];
        }
        int spent = Enumerable.Range(0, 6).Sum(i => _base[i] - _race.Min[i]);
        _pool = 30 + _race.BonusPoints - spent;
        RefreshStats();
    }

    private void RefreshStats()
    {
        StatRows.Children.Clear();
        string[] full = { "Strength", "Intelligence", "Wisdom", "Constitution", "Charisma", "Dexterity" };
        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto,26,Auto,10,Auto") };

            g.Children.Add(new TextBlock { Text = full[i], FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });

            var val = new TextBlock { Text = _stats[i].ToString(), Foreground = Brushes.DodgerBlue, FontWeight = FontWeight.Bold,
                                      MinWidth = 22, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 2); g.Children.Add(val);

            // the original's little up/down spinner
            var spin = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            var up = new Button { Content = "▲", FontSize = 8, Padding = new Avalonia.Thickness(4, 0), MinHeight = 13,
                                  IsEnabled = _pool > 0 && _stats[i] < _race.Max[i] };
            up.Click += (_, _) => { if (_pool > 0 && _stats[idx] < _race.Max[idx]) { _stats[idx]++; _pool--; RefreshStats(); } };
            var down = new Button { Content = "▼", FontSize = 8, Padding = new Avalonia.Thickness(4, 0), MinHeight = 13,
                                    IsEnabled = _stats[i] > _race.Min[i] };
            down.Click += (_, _) => { if (_stats[idx] > _race.Min[idx]) { _stats[idx]--; _pool++; RefreshStats(); } };
            spin.Children.Add(up); spin.Children.Add(down);
            Grid.SetColumn(spin, 3); g.Children.Add(spin);

            var range = new TextBlock { Text = $"({_race.Min[i]}-{_race.Max[i]})", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(range, 4); g.Children.Add(range);

            StatRows.Children.Add(g);
        }
        PoolText.Text = $"{_pool}   Stat Points Left";
        RefreshGuilds();
    }

    /// <summary>
    /// modUpdateInfo's three states (CHARCREA.BAS:346-379), which its list shows as bold, plain and italic: you qualify now;
    /// your race and alignment are allowed but your stats are too low; or your alignment or race rules
    /// the guild out entirely.
    /// </summary>
    private void RefreshGuilds()
    {
        GuildRows.Children.Clear();
        if (AlignBox.SelectedIndex < 0 || AlignBox.SelectedIndex >= _race.Alignments.Length) return;
        var alignment = _race.Alignments[AlignBox.SelectedIndex];
        var probe = new Character { Race = _race.Name, Alignment = alignment, Stats = _stats };

        foreach (var g in GuildData.All)
        {
            bool raceOk = g.AllowsRace(_race.Name);
            bool alignOk = g.AllowsAlignment(alignment);
            bool statsOk = g.MeetsStats(_stats);

            // modUpdateInfo (CHARCREA.BAS:346-379): a guild the race cannot join is not listed at all; italic
            // for one the alignment rules out, bold where the stats already qualify, plain otherwise - and
            // nothing else: no colour, no tooltip (bm1.png). The list is drawn in the form's own colours,
            // black MS Sans Serif on silver (MORDOR.EXE, frmCreateChar's lblGuild and its picture box,
            // BackColor &HC0C0C0, ForeColor 0), so the bold and the italic read as they do in bm1.png;
            // white monospace on near-black had flattened the three states into one.
            if (!raceOk) continue;
            var row = new TextBlock { Text = g.Name, FontSize = 14, Foreground = Brushes.Black,
                                      FontFamily = new FontFamily("Microsoft Sans Serif, Segoe UI, sans-serif") };
            if (!alignOk) row.FontStyle = FontStyle.Italic;
            else if (statsOk) row.FontWeight = FontWeight.Bold;
            GuildRows.Children.Add(row);
        }
    }

    // ---- the original's two reference buttons ----

    // cmdHelp_Click (CHARCREA.BAS:105-119) opens the help file at contexts 14 and 15 - the "Races:
    // Statistics" and "Guilds: Statistics" pages; the tables the port drew in its own popups were its own
    private void OnRaceStats(object? s, Avalonia.Interactivity.RoutedEventArgs e) => new HelpWindow("Races: Statistics").Show();

    private void OnGuildStats(object? s, Avalonia.Interactivity.RoutedEventArgs e) => new HelpWindow("Guilds: Statistics").Show();

    private static TextBlock Mono(string text, bool bold = false, IBrush? colour = null) =>
        new() { Text = text, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, Foreground = colour ?? Brushes.LightGray };

    private void Popup(string title, Control body) =>
        new Window
        {
            Title = title, Width = 820, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1a)),
            Content = new ScrollViewer { Content = body },
        }.ShowDialog(this);

    /// <summary>
    /// cmdTutorial_Click (CHARCREA.BAS:198-205) opens the help file at its Tutorial topic. The port
    /// had a popup of five sentences of its own here, paraphrasing that lesson.
    /// </summary>
    private void OnTutorial(object? s, Avalonia.Interactivity.RoutedEventArgs e) => new HelpWindow("Tutorial").Show();

    private void OnExit(object? s, Avalonia.Interactivity.RoutedEventArgs e) { Result = null; Close(); }

    private async void OnCreate(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // cmdSave_Click (CHARCREA.BAS:128-176): each refusal is a box, in this order, and the port's
        // "Enter a name.", "Choose an alignment." and "A character called X already exists. Only one
        // may hold a name." are gone
        ErrorText.Text = "";
        string name = (NameBox.Text ?? "").Trim();
        if (name.Length == 0) { await OriginalBox.Tell(this, "NO NAME!", "No Name Defined!"); return; }
        if (name.Contains('|')) { await OriginalBox.Tell(this, "Sorry!", "The character '|' is not allowed in a Character name."); return; }
        if (SexBox.SelectedIndex < 0) { await OriginalBox.Tell(this, "NO SEX!", "No Sex Defined!"); return; }
        if (AlignBox.SelectedIndex < 0) { await OriginalBox.Tell(this, "NO ALIGNMENT!", "No Alignment Defined!"); return; }

        string saveDir = Saves.DirFor(_gameDir);
        if (System.IO.File.Exists(System.IO.Path.Combine(saveDir, Character.SafeFileName(name) + ".json")))
        {
            await OriginalBox.Tell(this, "NAME ALREADY EXISTS!", $"Name \"{NameBox.Text}\" already exists!\nTry another.");
            return;
        }
        // points not spent are asked about, not refused (152-156)
        if (_pool > 0 && !await OriginalBox.YesNo(this, "POINTS", "You still have points left.  Save Anyway?")) return;

        var alignment = _race.Alignments[AlignBox.SelectedIndex];
        var sex = SexBox.SelectedIndex == 1 ? Sex.Female : Sex.Male;

        var ch = Character.Create(name, _race, alignment, sex, _stats);
        // CHARCREA.BAS:157-175 - asked twice, and asked again until the two match; an empty one is
        // confirmed in a box and then stored as a single space so every later check skips the
        // prompt. The port had given up on a mismatch and never asked about an empty one.
        string? first, again;
        do
        {
            first = await PasswordBox.Ask(this, "ENTER PASSWORD", "Please enter Password");
            if (first == null) return;
            again = await PasswordBox.Ask(this, "ENTER PASSWORD", "Please re-enter Password");
            if (again == null) return;
            if (first != again) await OriginalBox.Tell(this, "ERROR", "Passwords don't match!");
        } while (first != again);
        if (first == "" && !await OriginalBox.YesNo(this, "NO PASSWORD", "You have chosen no password.\nIs this okay?")) return;
        ch.Password = first == "" ? Character.NoPassword : first;

        try
        {
            System.IO.Directory.CreateDirectory(saveDir);
            ch.Save(System.IO.Path.Combine(saveDir, Character.SafeFileName(name) + ".json"));
            // fn04ED (MAINMOD.BAS:384-402): the new record goes into the first blank one, or on the end
            Roster.Added(saveDir, ch.Name, Roster.Saved(saveDir).Select(p => p.Ch.Name));
            // CHARCREA.BAS:258 - the guild you are born into records your arrival
            GuildLog.Append(saveDir, GuildData.IndexOf(ch.Guild.Guild), GuildLog.Wrote.JoinedGuild(ch.Name));
            // and CHARCREA.BAS:178 offers all six of their natural stats to the Hall of Records
            var hall = HallRecords.Load(saveDir);
            for (int i = 0; i < 6; i++) hall.CheckAndLog((HallRecords.Kind)i, ch, ch.Stats[i], saveDir, null, alone: true);
            hall.Save(saveDir);
        }
        catch { await OriginalBox.Tell(this, "ERROR!", $"Error saving character to {saveDir}!"); return; }   // 179

        Result = ch;
        Close();
    }
}
