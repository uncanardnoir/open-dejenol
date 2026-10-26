using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Layout;
using Mordor.Core;

namespace Mordor.App;

public partial class MainWindow : Window
{
    private List<Floor> _dungeon = new();
    private const int Cell = 20;

    public MainWindow()
    {
        InitializeComponent();
        PathBox.Text = TitleWindow.GameDir;
        LoadAll();
    }

    private void OnReload(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LoadAll();

    private async void OnLoadCharacter(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string dir = PathBox.Text ?? "";
        string saveDir = Saves.DirFor(dir);
        var files = System.IO.Directory.Exists(saveDir)
            ? System.IO.Directory.GetFiles(saveDir, "*.json").OrderBy(f => f).ToList()
            : new List<string>();
        if (files.Count == 0) { StatusText.Text = "No saved characters yet."; return; }

        // frmCharList names a character the way the whole game does: the name, then the guild
        // they are currently in and their level in it, then the state of the body in capitals if
        // there is one (FRMCHARL.BAS:51-53). Above the list it counts them. This picker had been
        // showing bare file names.
        var loaded = new List<Character>();
        var rows = new List<string>();
        foreach (string f in files)
        {
            Character c;
            try { c = Character.Load(f); } catch { continue; }
            loaded.Add(c);
            string row = $"{c.Name.TrimEnd()} (Level {c.Guild.Level} {c.Guild.Guild})";
            if (c.IsDead) row += " [" + DeathStates.Name(c.CauseOfDeath).ToUpperInvariant() + "]";
            rows.Add(row);
        }
        if (loaded.Count == 0) { StatusText.Text = "No saved characters yet."; return; }

        var list = new ListBox { ItemsSource = rows, Height = 240 };
        var open = new Button { Content = "Open", Background = new SolidColorBrush(Color.FromRgb(0x2d, 0x6a, 0x2d)) };
        var picker = new Window
        {
            Title = "Load Character", Width = 420, Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(12), Spacing = 10,
                Children =
                {
                    // lblNumber, which the original writes as a bare count beside the list
                    new TextBlock { Text = loaded.Count.ToString() },
                    list, open,
                },
            },
        };
        Character? picked = null;
        open.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= loaded.Count) return;
            picked = loaded[list.SelectedIndex];
            picker.Close();
        };
        await OriginalBox.ShowOver(picker, this);
        if (picked != null) new CityWindow(picked, dir).Show();
    }

    private async void OnNewCharacter(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string dir = PathBox.Text ?? "";
        var create = new CharacterCreationWindow(dir);
        await OriginalBox.ShowOver(create, this);
        if (create.Result is { } ch)
            new CityWindow(ch, dir).Show();
    }

    private void LoadAll()
    {
        string dir = PathBox.Text ?? "";
        try
        {
            string P(string f) => System.IO.Path.Combine(dir, f);
            Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir);   // real tables from MDATA1
            GuildData.LoadFrom(dir);  // real guild requirements from MDATA1
            Audio.Init(dir);
            Audio.Music("THEME");
            SoundBox.IsCheckedChanged += (_, _) => Audio.Enabled = SoundBox.IsChecked == true;
            MusicBox.IsCheckedChanged += (_, _) => { Audio.MusicEnabled = MusicBox.IsChecked == true; if (Audio.MusicEnabled) Audio.Music("THEME"); };
            MonsterGrid.ItemsSource = MdrLoader.LoadMonsters(P("MDATA5.MDR"));
            ItemGrid.ItemsSource = MdrLoader.LoadItems(P("MDATA3.MDR"));
            SpellGrid.ItemsSource = MdrLoader.LoadSpells(P("MDATA2.MDR"));
            _dungeon = MdrLoader.LoadDungeon(P("MDATA11.MDR"));

            LevelBox.ItemsSource = _dungeon.Select(f => $"Level {f.LevelNum}").ToList();
            if (_dungeon.Count > 0) LevelBox.SelectedIndex = 0;

            StatusText.Text = $"Loaded {((List<Monster>)MonsterGrid.ItemsSource).Count} monsters, " +
                              $"{((List<Item>)ItemGrid.ItemsSource).Count} items, " +
                              $"{((List<Spell>)SpellGrid.ItemsSource).Count} spells, {_dungeon.Count} levels.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            StatusText.Foreground = Brushes.IndianRed;
        }
    }

    private void OnLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LevelBox.SelectedIndex < 0 || LevelBox.SelectedIndex >= _dungeon.Count) return;
        RenderFloor(_dungeon[LevelBox.SelectedIndex]);
    }

    private void RenderFloor(Floor f)
    {
        MapCanvas.Children.Clear();
        MapCanvas.Width = f.Width * Cell + 2;
        MapCanvas.Height = f.Height * Cell + 2;
        FloorInfo.Text = $"{f.Width}x{f.Height}   teleporters: {f.Teleporters.Count}   chutes: {f.Chutes.Count}";

        var wall = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE8));
        var door = new SolidColorBrush(Color.FromRgb(0xC8, 0x90, 0x40));
        var secret = new SolidColorBrush(Color.FromRgb(0x80, 0x50, 0xC0));

        for (int y = 0; y < f.Height; y++)
        for (int x = 0; x < f.Width; x++)
        {
            double px = x * Cell + 1, py = (f.Height - 1 - y) * Cell + 1; // row 0 = south = bottom

            // feature fill
            var fill = FeatureFill(f, x, y);
            if (fill != null)
                MapCanvas.Children.Add(new Rectangle { Width = Cell, Height = Cell, Fill = fill,
                    [Canvas.LeftProperty] = px, [Canvas.TopProperty] = py });

            // north edge
            if (f.Has(x, y, Floor.WALL_NORTH)) AddLine(px, py, px + Cell, py, wall);
            else if (f.Has(x, y, Floor.DOOR_NORTH)) AddLine(px, py, px + Cell, py, door);
            else if (f.Has(x, y, Floor.SECRET_DOOR_NORTH)) AddLine(px, py, px + Cell, py, secret);

            // east edge
            if (f.Has(x, y, Floor.WALL_EAST)) AddLine(px + Cell, py, px + Cell, py + Cell, wall);
            else if (f.Has(x, y, Floor.DOOR_EAST)) AddLine(px + Cell, py, px + Cell, py + Cell, door);
            else if (f.Has(x, y, Floor.SECRET_DOOR_EAST)) AddLine(px + Cell, py, px + Cell, py + Cell, secret);

            // marker
            string? mark = Marker(f, x, y);
            if (mark != null)
                MapCanvas.Children.Add(new TextBlock { Text = mark, FontSize = 11,
                    Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
                    [Canvas.LeftProperty] = px + 3, [Canvas.TopProperty] = py + 3 });
        }

        // outer border (west of col0, north handled per-cell; draw full frame)
        AddLine(1, 1, 1, f.Height * Cell + 1, Brushes.Gray);                                   // west
        AddLine(1, f.Height * Cell + 1, f.Width * Cell + 1, f.Height * Cell + 1, Brushes.Gray); // south
    }

    private IBrush? FeatureFill(Floor f, int x, int y)
    {
        if (f.Has(x, y, Floor.ROCK)) return new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3b));
        if (f.Has(x, y, Floor.WATER)) return new SolidColorBrush(Color.FromRgb(0x1e, 0x3a, 0x6a));
        if (f.Has(x, y, Floor.QUICKSAND)) return new SolidColorBrush(Color.FromRgb(0x5a, 0x4a, 0x20));
        if (f.Has(x, y, Floor.FOG)) return new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x48));
        if (f.Has(x, y, Floor.ANTIMAGIC)) return new SolidColorBrush(Color.FromRgb(0x50, 0x20, 0x40));
        return null;
    }

    private static string? Marker(Floor f, int x, int y)
    {
        if (f.Has(x, y, Floor.STAIRS_UP)) return "▲";
        if (f.Has(x, y, Floor.STAIRS_DOWN)) return "▼";
        if (f.Has(x, y, Floor.CHUTE)) return "C";
        if (f.Has(x, y, Floor.TELEPORTER)) return "T";
        if (f.Has(x, y, Floor.PIT)) return "O";
        if (f.Has(x, y, Floor.ROTATOR)) return "@";
        if (f.Has(x, y, Floor.EXTINGUISHER)) return "x";
        if (f.Has(x, y, Floor.STUD)) return "S";
        return null;
    }

    private void AddLine(double x1, double y1, double x2, double y2, IBrush brush) =>
        MapCanvas.Children.Add(new Line
        {
            StartPoint = new Avalonia.Point(x1, y1),
            EndPoint = new Avalonia.Point(x2, y2),
            Stroke = brush, StrokeThickness = 1.5
        });
}

