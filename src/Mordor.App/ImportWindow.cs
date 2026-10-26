using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Mordor.Core;
using Mordor.Core.Import;

namespace Mordor.App;

/// <summary>
/// The port's own: "Import Original Saves..." on the Options window, beside Backup and Restore and,
/// like them, from the main menu only. It takes an original game's DATA folder (or the game's
/// folder with DATA inside it), reads it with <see cref="OriginalSaves"/> - reading only - and shows
/// what it found: the characters by name, the shared files, and anything the import has to say
/// about them. Nothing is written until the player presses Import, and then:
/// <list type="bullet">
/// <item>the saves folder is copied first into <c>BACKUP\before-import-&lt;time&gt;</c>, which the
/// Options window's Restore can bring back;</item>
/// <item>a character whose name the port already has is unticked to begin with, and ticking it is
/// asked about again, by name, before anything is replaced;</item>
/// <item>the shared world (automap, rooms, store, Library, Hall of Records, guildmasters, pens,
/// guild log) is ticked only when the saves folder has none of it yet - importing it replaces the
/// port's.</item>
/// </list>
/// docs/GAME_FILES.md, "Importing the original's saves".
/// </summary>
public static class ImportWindow
{
    private static readonly IBrush Text = Brushes.Gainsboro;
    private static readonly IBrush Heading = Brush.Parse("#8fb0ff");
    private static readonly IBrush Warn = Brush.Parse("#e0b040");

    public static async Task Run(Window owner, string gameDir, string savesDir)
    {
        var picked = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Import - choose the original Mordor's DATA folder (or the folder with MORDOR.EXE)",
            AllowMultiple = false,
        });
        string? path = picked.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;
        string? data = OriginalSaves.FindDataDir(path);
        if (data == null)
        {
            await OriginalBox.Tell(owner, "Import Original Saves", "There is no MDATA4.MDR, the original's characters file, in\n" + path + "\nor in a DATA folder inside it.");
            return;
        }
        if (Backups.SameDir(data, savesDir))
        {
            await OriginalBox.Tell(owner, "Import Original Saves", "That is the port's own saves folder.");
            return;
        }

        OriginalSaves src;
        SaveImport.ImportPlan plan;
        try
        {
            var items = MdrLoader.LoadItems(Path.Combine(gameDir, "MDATA3.MDR"));
            var monsters = MdrLoader.LoadMonsters(Path.Combine(gameDir, "MDATA5.MDR"));
            src = OriginalSaves.Read(data, items, monsters);
            plan = SaveImport.Plan(src, savesDir);
        }
        catch (Exception ex)
        {
            await OriginalBox.Tell(owner, "Import Original Saves", "The original's files could not be read: " + ex.Message);
            return;
        }

        var form = BuildPreview(owner.FontFamily, src, plan, data, savesDir);
        await form.Window.ShowDialog(owner);
        if (form.Result is not { } choice) return;
        var (names, world) = choice;
        if (names.Count == 0 && !world)
        {
            await OriginalBox.Tell(owner, "Import Original Saves", "Nothing was ticked, so nothing was imported.");
            return;
        }
        // every character that would replace one of the port's is asked about again, by name
        foreach (var c in plan.Characters.Where(c => c.Exists && names.Contains(c.Imported.Name)).ToList())
            if (!await OriginalBox.YesNoDefaultNo(owner, "Replace Character?",
                    $"The saves folder already has a character named \"{c.Imported.Name}\".\n\nReplace it with the original's \"{c.Imported.Name}\"?"))
                names.Remove(c.Imported.Name);
        if (names.Count == 0 && !world) return;

        try
        {
            var res = SaveImport.Apply(src, savesDir, names, world, DateTime.Now);
            CrashLog.Note($"imported from {data}: {string.Join(" ", res.Written)}" + (res.BackupDir != null ? $"; the saves folder was copied to {res.BackupDir} first" : ""));
            string chars = names.Count == 0 ? "no characters" : string.Join(", ", names.Select(n => "\"" + n + "\""));
            await OriginalBox.Tell(owner, "Import Original Saves",
                $"Imported {chars}" + (world ? " and the shared world" : "") + "."
                + (res.BackupDir != null ? $"\n\nThe saves folder as it was is in\n{res.BackupDir}\n(Restore on this window brings it back)." : ""));
        }
        catch (Exception ex)
        {
            await OriginalBox.Tell(owner, "Error!", "Error '" + ex.Message + "' occurred while importing. Nothing was written if the backup could not be made"
                                                  + "; otherwise the saves folder as it was is in its BACKUP folder.");
        }
    }

    /// <summary>The preview, built and not shown: Mordor.UiCheck lays it out headless and presses its buttons.</summary>
    public sealed class PreviewForm
    {
        public required Window Window { get; init; }
        public required List<(CheckBox Box, string Name)> Characters { get; init; }
        public CheckBox? World { get; init; }
        public required Button Import { get; init; }
        public required Button Cancel { get; init; }
        public bool Go { get; set; }
        /// <summary>The names ticked and whether the world is, once Import was pressed; null when cancelled.</summary>
        public (List<string> Names, bool World)? Result =>
            Go ? (Characters.Where(b => b.Box.IsChecked == true).Select(b => b.Name).ToList(), World?.IsChecked == true) : null;
    }

    /// <summary>The preview: what will be imported, ticked or not.</summary>
    public static PreviewForm BuildPreview(FontFamily font, OriginalSaves src, SaveImport.ImportPlan plan, string data, string savesDir)
    {
        var win = new Window
        {
            Title = "Import Original Saves",
            Width = 640,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 720,
            CanResize = false,
            Background = Brush.Parse("#101018"),
            FontFamily = font,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        TextBlock Line(string s, IBrush? brush = null, bool bold = false) => new()
        {
            Text = s,
            Foreground = brush ?? Text,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
        };

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(Line("From the original's DATA folder:", Heading, true));
        body.Children.Add(Line(data));
        body.Children.Add(Line("Into this saves folder:", Heading, true));
        body.Children.Add(Line(savesDir));

        body.Children.Add(Line($"Characters ({plan.Characters.Count} of {src.Characters.Count} can be imported)", Heading, true));
        var boxes = new List<(CheckBox Box, string Name)>();
        foreach (var c in plan.Characters)
        {
            var ch = c.Imported.Character!;
            string where = ch.DungeonLevel > 0 ? $"dungeon level {ch.DungeonLevel}" : "in town";
            string dead = ch.IsDead ? ", " + DeathStates.Name(ch.CauseOfDeath) : "";
            var box = new CheckBox
            {
                IsChecked = !c.Exists,
                Foreground = Text,
                Content = new StackPanel
                {
                    Children =
                    {
                        Line($"{ch.Name} - {ch.Race} {ch.Guild.Guild} ({ch.Guild.Level}), {ch.CurHits}/{ch.MaxHits} hits, "
                             + $"{CharStatFile.FormatNumber(ch.GoldOnHand)} gold, {where}{dead}"),
                        c.Exists ? Line("The saves folder already has a character of this name: ticked, it is replaced (you are asked again).", Warn) : new Control(),
                    },
                },
            };
            boxes.Add((box, c.Imported.Name));
            body.Children.Add(box);
        }
        foreach (var c in src.Characters.Where(c => c.Character == null))
            body.Children.Add(Line($"{c.Name} - cannot be imported: {c.Refusal}", Warn));
        if (src.Characters.Count == 0) body.Children.Add(Line("(none)"));

        CheckBox? worldBox = null;
        if (plan.World.Count > 0)
        {
            body.Children.Add(Line("The shared world", Heading, true));
            bool anyThere = plan.World.Any(w => w.Exists);
            worldBox = new CheckBox
            {
                IsChecked = !anyThere,
                Foreground = Text,
                Content = Line("Import it too - " + string.Join(", ", plan.World.Select(w => w.Holds))),
            };
            body.Children.Add(worldBox);
            if (anyThere)
                body.Children.Add(Line("The saves folder has its own " + string.Join(", ", plan.World.Where(w => w.Exists).Select(w => w.File))
                                       + ": importing the world replaces them.", Warn));
            if (src.Parties.Count > 0)
                body.Children.Add(Line("Remembered parties come with their leader: " + string.Join("; ", src.Parties.Select(p => string.Join(", ", p.Members)))));
        }

        var notes = src.Notes.Where(n => n.Kind != NoteKind.Info).ToList();
        var infos = src.Notes.Where(n => n.Kind == NoteKind.Info).ToList();
        if (notes.Count + infos.Count > 0)
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var n in notes) list.Children.Add(Line($"{n.Where}: {n.Text}", Warn));
            foreach (var n in infos) list.Children.Add(Line($"{n.Where}: {n.Text}"));
            body.Children.Add(new Expander
            {
                Header = Line($"What the import has to say ({notes.Count} to look at, {infos.Count} for information)", Heading),
                IsExpanded = notes.Count > 0,
                Content = list,
            });
        }
        body.Children.Add(Line("Before anything is written the saves folder is copied into BACKUP\\before-import-<date and time> inside it. "
                               + "The original's files are only read."));

        var ok = new Button { Content = "_Import", IsDefault = true, Width = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = "_Cancel", IsCancel = true, Width = 110, HorizontalContentAlignment = HorizontalAlignment.Center };
        win.Content = new DockPanel
        {
            Margin = new Thickness(14),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0),
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Children = { ok, cancel },
                },
                new ScrollViewer { Content = body, MaxHeight = 600 },
            },
        };
        var form = new PreviewForm { Window = win, Characters = boxes, World = worldBox, Import = ok, Cancel = cancel };
        ok.Click += (_, _) => { form.Go = true; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        return form;
    }
}
