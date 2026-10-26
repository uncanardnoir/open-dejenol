using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The Options window, <c>FRMOPTIO</c>. "This will display the Game Options screen, allowing the
/// user to set volume, etc." (help, Control Panel), and the help's screenshot of it is
/// game_files/bm3.png, which is where the layout and every caption come from: Game Settings in two
/// columns at the top left, Music Settings beneath, Font Settings and File Backups down the right,
/// OK and Cancel along the bottom.
///
/// Which caption goes with which setting is not read off the picture - it is read off the INI key
/// the OK button writes beside each field (FRMOPTIO.BAS:152-247). See <see cref="GameOptions"/>.
///
/// Cancel is not a no-op there: the form applies music, sound, the toolbar and the automap cursor
/// as you tick them, and Cancel puts the saved values back (FRMOPTIO.BAS:135-146). It does not put
/// back No 3-D, No StretchBlt or No Party Sound, because those three are never applied live. The
/// port does this for No Sound FX and the FX volume; the other live boxes still wait for OK.
/// </summary>
public sealed class OptionsWindow : Window
{
    private readonly GameOptions _o;
    private readonly string _gameDir;
    private readonly Dictionary<string, CheckBox> _boxes = new();
    private readonly Slider _music, _fx;
    /// <summary>gv219A as the slider has it; the options keep theirs until OK.</summary>
    private int _fxRaw;
    /// <summary>lblLabel(2), the "Pro" font M41D4, and lblLabel(3), the "NonPro" font M41DB.</summary>
    private readonly TextBlock _label2, _label3;
    private readonly bool _inGame;
    private TextBlock? _lastBackup;

    /// <summary>True when OK was used, so the caller knows to apply what changed.</summary>
    public bool Accepted { get; private set; }

    /// <param name="inGame">
    /// <c>gv1B52</c>, a character in play: Form_Load then disables No 3-D, both font "..." buttons,
    /// Default, Backup and Restore (FRMOPTIO.BAS:330-338), so all of these work from the main menu only.
    /// </param>
    public OptionsWindow(GameOptions options, string gameDir, bool inGame = false)
    {
        _o = options;
        _gameDir = gameDir;
        _inGame = inGame;

        Title = "Options";
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brush.Parse("#101018");
        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        CheckBox Box(string key, string caption, bool value)
        {
            var b = new CheckBox
            {
                Content = caption,
                IsChecked = value,
                Foreground = Brushes.Gainsboro,
                FontSize = 12,
                Margin = new Thickness(0, 1),
            };
            _boxes[key] = b;
            return b;
        }

        // the two columns of Game Settings, in the picture's own order
        var left = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Box(nameof(GameOptions.NoPartyFX), "No Party Sound", _o.NoPartyFX),
                Box(nameof(GameOptions.NoDing), "No Msg. Ding", _o.NoDing),
                Box(nameof(GameOptions.NoHelpSound), "No HELP Sound", _o.NoHelpSound),
                Box(nameof(GameOptions.NoToolbar), "No Toolbar", _o.NoToolbar),
                Box(nameof(GameOptions.NoBlinkCursor), "No Automap Cursor Blink", _o.NoBlinkCursor),
            },
        };
        var right = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Box(nameof(GameOptions.No3d), "No 3-D", _o.No3d),
                Box(nameof(GameOptions.NoStretch), "No StretchBlt", _o.NoStretch),
                Box(nameof(GameOptions.NoLoadAutomap), "No Load Automap", _o.NoLoadAutomap),
                Box(nameof(GameOptions.NoAutoSave), "No Autosave", _o.NoAutoSave),
            },
        };

        _music = Volume(_o.MusicVolume);
        // FX Vol. is gv219A, waveOutSetVolume's own Long (Mordor.Core WaveVolume). Form_Load puts it
        // on the slider (FRMOPTIO.BAS:354-356) with m0070 set, so that does not count as a scroll.
        _fxRaw = _o.FXVolume;
        _fx = new Slider
        {
            Minimum = 0,
            Maximum = WaveVolume.SliderMax,
            Value = WaveVolume.ToSlider(_fxRaw),
            Width = 170,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // hsbVolume_Scroll (FRMOPTIO.BAS:396-412): the setting is taken at once and sub09DE plays
        // sound 2, HIT, which sets the device to it - you hear the volume you chose
        _fx.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty) return;
            _fxRaw = WaveVolume.FromSlider((int)Math.Round(_fx.Value));
            Audio.SetFxVolume(_fxRaw);
            Audio.Play("HIT");
        };
        // Cancel: "gv219A = m006C" (FRMOPTIO.BAS:148), the value it had when the form was loaded
        // and "gv1A66.M5EFC = m0056" (140): the No Sound FX flag as loaded, the session left as it is
        Closed += (_, _) => { if (!Accepted) { Audio.SetFxVolume(_o.FXVolume); Audio.RestoreNoFx(_o.NoFX); } };
        var noMusic = Box(nameof(GameOptions.NoMusic), "No Music", _o.NoMusic);
        var noFx = Box(nameof(GameOptions.NoFX), "No Sound FX", _o.NoFX);
        // the form greys the slider out while its "No" box is ticked
        noMusic.IsCheckedChanged += (_, _) => _music.IsEnabled = noMusic.IsChecked != true;
        // No Sound FX is live (chkCheck_Click, Case 2, FRMOPTIO.BAS:88-99): ticked, the slider greys
        // and mdrDeInitializeFX closes the effects down; cleared, the slider comes back, fn1A08 opens
        // them again and sub09DE plays RAISE - or, if the device will not open, the form's box and
        // the slider greyed again. Attached after the box was set, as m0070 keeps Form_Load out.
        noFx.IsCheckedChanged += async (_, _) =>
        {
            if (noFx.IsChecked == true)
            {
                _fx.IsEnabled = false;
                Audio.NoFxTicked();
                return;
            }
            _fx.IsEnabled = true;
            if (!Audio.NoFxCleared())
            {
                await OriginalBox.Tell(this, "Sound FX", "Error initializing Sound FX!\n\nDo you have a WAVE compatible Sound Card Installed?  Are the drivers current?");
                _fx.IsEnabled = false;
            }
        };
        _music.IsEnabled = !_o.NoMusic;
        _fx.IsEnabled = !_o.NoFX;

        _label2 = new TextBlock { Foreground = Brushes.Gainsboro, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _label3 = new TextBlock { Foreground = Brushes.Gainsboro, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        ShowFonts();

        var useDefaults = new Button
        {
            Content = "_Default",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = !inGame,
        };
        useDefaults.Click += (_, _) => { _o.DefaultFonts(); ShowFonts(); };
        if (inGame && _boxes.TryGetValue(nameof(GameOptions.No3d), out var no3d)) no3d.IsEnabled = false;

        var ok = new Button { Content = "_OK", IsDefault = true, Width = 120, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => { Accept(); Close(); };
        var cancel = new Button { Content = "_Cancel", IsCancel = true, Width = 120, HorizontalContentAlignment = HorizontalAlignment.Center };
        cancel.Click += (_, _) => Close();
        // the Mods page is reached from the main menu's own Mods button, not from here: the form
        // is bm3.png's, OK and Cancel alone

        Content = new Border
        {
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,14,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                {
                                    Group("Game Settings", new Grid
                                    {
                                        ColumnDefinitions = new ColumnDefinitions("Auto,16,Auto"),
                                        Children = { left, Place(right, 2) },
                                    }),
                                    Group("Music Settings", new StackPanel
                                    {
                                        Spacing = 6,
                                        Children =
                                        {
                                            Labelled("Music Vol.", _music, noMusic),
                                            Labelled("FX Vol.", _fx, noFx),
                                            Box(nameof(GameOptions.SoundCanvas), "Use Sound Canvas MIDI", _o.SoundCanvas),
                                        },
                                    }),
                                },
                            },
                            Place(new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                {
                                    Group("Font Settings", new StackPanel
                                    {
                                        Spacing = 6,
                                        Children =
                                        {
                                            // bm3.png: "Courier New (9)", M41D4's default, under this caption,
                                            // and its "..." is cmdCommand(4), "Set Proportional Font"
                                            Caption("Non-Proportional Font"),
                                            WithBrowse(_label2, proportional: true),
                                            Caption("Proportional Font"),
                                            WithBrowse(_label3, proportional: false),
                                            useDefaults,
                                        },
                                    }),
                                    Group("File Backups", Backups()),
                                },
                            }, 2),
                        },
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Children = { ok, cancel },
                    },
                },
            },
        };
    }

    private static Control Place(Control c, int column)
    {
        c[Grid.ColumnProperty] = column;
        return c;
    }

    private static Slider Volume(int value) => new()
    {
        Minimum = 0,
        Maximum = 100,
        Value = Math.Clamp(value, 0, 100),
        Width = 170,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Control Labelled(string caption, Slider bar, CheckBox beside) => new StackPanel
    {
        Spacing = 2,
        Children =
        {
            new TextBlock { Text = caption, Foreground = Brush.Parse("#8fb0ff"), FontSize = 11 },
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = { bar, beside },
            },
        },
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Foreground = Brush.Parse("#8fb0ff"),
        FontWeight = FontWeight.Bold,
        FontSize = 11,
    };

    private static Border Group(string caption, Control body) => new()
    {
        BorderBrush = Brush.Parse("#3a3a4e"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 8),
        Child = new StackPanel
        {
            Spacing = 6,
            Children = { Caption(caption), body },
        },
    };

    /// <summary>
    /// File Backups: lblBackup, "Last Backup was on" (sub2C76, FRMOPTIO.BAS:416-420), above
    /// cmdCommand(1) Backup and cmdCommand(2) Restore (cmdCommand_Click, FRMOPTIO.BAS:259-281).
    /// </summary>
    private Control Backups()
    {
        Button Make(string text, bool backup)
        {
            var b = new Button
            {
                Content = text,
                IsEnabled = !_inGame,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            b.Click += async (_, _) => await BackupOrRestore(backup);
            return b;
        }
        _lastBackup = new TextBlock
        {
            Text = _o.LastBackupLine,
            Foreground = Brushes.Gainsboro,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        return new StackPanel
        {
            Spacing = 6,
            Children = { _lastBackup, Make("Backup", true), Make("Restore", false) },
        };
    }

    private string SavesDir => Saves.DirFor(_gameDir);

    /// <summary>
    /// cmdCommand_Click's Backup and Restore case (FRMOPTIO.BAS:259-281).
    ///
    /// The first box's title is <c>frmDirectory.Tag + " files"</c>, read before the Tag is set:
    /// touching the property loads frmDirectory with its design-time Tag, and every earlier use
    /// ended with Unload. The design-time Tag is not in the decompiled source; the port takes it as
    /// empty, so the title is " files". Its style, <c>mc00D0 &amp; mc00D2</c>, is "324": No is the
    /// default button.
    ///
    /// On success the original says "Mordor will now be shut down." and ends (mdrEndProgram, End);
    /// the port shuts the application down the same way, without saving anything on the way.
    /// </summary>
    private async System.Threading.Tasks.Task BackupOrRestore(bool backup)
    {
        if (!await OriginalBox.YesNoDefaultNo(this, " files",
                "You must restart Mordor after Backing up or Restoring.  Do you still wish to proceed?")) return;
        string? dir = await DirectoryDialog.Ask(this, backup ? "Backup" : "Restore", SavesDir);
        if (string.IsNullOrEmpty(dir)) return;
        if (!await Fn0662(backup, dir)) return;
        await OriginalBox.Tell(this, "Shutting Down...",
            "Mordor will now be shut down.  " + (backup ? "" : "Restart Mordor to have restored data take affect!"));
        Audio.StopMusic();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life)
            life.Shutdown();
        else
            Environment.Exit(0);
    }

    /// <summary>
    /// fn0662 (MAINMOD.BAS:954-1046) with its boxes; <see cref="Mordor.Core.Backups"/> copies. The
    /// saves directory is the original's DATA directory; the game directory stands for BasePath.
    /// A box with no title of its own carries App.Title, "Mordor 1.1" (MORDOR.MAK).
    /// </summary>
    private async System.Threading.Tasks.Task<bool> Fn0662(bool backup, string dir)
    {
        string saves = SavesDir;
        // UCase(Left(l01FC, Len(l01FC) - 1)): the directory upper-cased, without its last backslash
        string shown = System.IO.Path.GetFullPath(dir).TrimEnd('\\', '/').ToUpperInvariant();
        try
        {
            if (Mordor.Core.Backups.SameDir(dir, _gameDir) || Mordor.Core.Backups.SameDir(dir, saves))
            {
                await OriginalBox.Tell(this, "Mordor 1.1",
                    "You cannot backup or restore from the Base Mordor directory or the Mordor Data directory!");
                return false;
            }
            if (backup)
            {
                long need = Mordor.Core.Backups.Size(saves);
                long free = Mordor.Core.Backups.FreeSpace(dir);
                if (free >= 0 && need > free)
                {
                    string drive = shown.Length >= 2 ? shown[..2] : shown;
                    await OriginalBox.Tell(this, "Mordor 1.1",
                        "There is not enough free space on " + drive + " to backup the current data files.\n"
                        + "Select another drive or make room for more files.");
                    return false;
                }
                bool asked = Mordor.Core.Backups.Found(dir)
                    ? await OriginalBox.YesNoDefaultNo(this, "Overwrite Files?", "Backup files already exist in " + shown + ".\nOverwrite?")
                    : await OriginalBox.YesNoDefaultNo(this, "Backup Files?", "Backup files to " + shown + "?");
                if (!asked) return false;
                Mordor.Core.Backups.Backup(saves, dir);
            }
            else
            {
                if (!Mordor.Core.Backups.Found(dir))
                {
                    await OriginalBox.Tell(this, "Restore Files", "Backup files not found in " + shown + "!");
                    return false;
                }
                if (!await OriginalBox.YesNoDefaultNo(this, "Overwrite Files?",
                        "WARNING!\n\nRestoring these backup files from '" + shown
                        + "' will overwrite your current game files.  Are you sure you wish to proceed?")) return false;
                Mordor.Core.Backups.Restore(dir, saves);
            }
            // a restore dates the "Last Backup" too (MAINMOD.BAS:1032), kept
            string today = DateTime.Today.ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
            PortSettings.Write(saves, "LastBackup", today);
            _o.LastBackup = today;
            if (_lastBackup != null) _lastBackup.Text = _o.LastBackupLine;
            string what = backup ? "Backup" : "Restore";
            await OriginalBox.Tell(this, what + " Game Files", what + " of Game files successful!");
            return true;
        }
        catch (Exception ex)
        {
            await OriginalBox.Tell(this, "Error!", "Error '" + ex.Message + "' occurred while attempting to Backup/Restore Game Files.");
            return false;
        }
    }

    /// <summary>
    /// Each font name on the form has a "..." button beside it (game_files/bm3.png):
    /// cmdCommand(4) and (5) (FRMOPTIO.BAS:282-305). Each opens the common Font dialog
    /// (dlgDialog.Action = 4, Flags 1 = screen fonts, CancelError on) on the font it changes, and
    /// a font chosen goes into the label and its size (sub2C63). The code sets DialogTitle to "Set
    /// Proportional Font" or "Set Non-Proportional Font", but the Common Dialog control ignores
    /// DialogTitle for the Font dialog, so the box the player saw was the system's own "Font"; the
    /// port opens that same Windows dialog.
    /// </summary>
    private Control WithBrowse(Control name, bool proportional)
    {
        var dots = new Button
        {
            Content = "...",
            IsEnabled = !_inGame,
            [Grid.ColumnProperty] = 2,
        };
        dots.Click += (_, _) =>
        {
            string font = proportional ? _o.ProportionalFont : _o.NonProportionalFont;
            int size = proportional ? _o.ProportionalSize : _o.NonProportionalSize;
            if (!FontDialog.Choose(this, ref font, ref size)) return;   // Cancel is an error, and the handler just leaves
            if (font == "") return;
            if (proportional) { _o.ProportionalFont = font; _o.ProportionalSize = size; }
            else { _o.NonProportionalFont = font; _o.NonProportionalSize = size; }
            ShowFonts();
        };
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,6,Auto"),
            Children = { name, dots },
        };
    }

    /// <summary>
    /// sub2C63 (FRMOPTIO.BAS:422-429): each label is written in its own font, "Name (size)".
    /// VB's size is in points; the port's is in 96-per-inch pixels.
    /// </summary>
    private void ShowFonts()
    {
        void Show(TextBlock label, string font, int size)
        {
            label.Text = $"{font} ({size})";
            try { label.FontFamily = new FontFamily(font); } catch { }
            if (size > 0) label.FontSize = size * 96.0 / 72.0;
        }
        Show(_label2, _o.ProportionalFont, _o.ProportionalSize);
        Show(_label3, _o.NonProportionalFont, _o.NonProportionalSize);
    }

    private bool Ticked(string key) => _boxes.TryGetValue(key, out var b) && b.IsChecked == true;

    private void Accept()
    {
        _o.No3d = Ticked(nameof(GameOptions.No3d));
        _o.NoMusic = Ticked(nameof(GameOptions.NoMusic));
        _o.NoFX = Ticked(nameof(GameOptions.NoFX));
        _o.NoStretch = Ticked(nameof(GameOptions.NoStretch));
        _o.SoundCanvas = Ticked(nameof(GameOptions.SoundCanvas));
        _o.NoDing = Ticked(nameof(GameOptions.NoDing));
        _o.NoLoadAutomap = Ticked(nameof(GameOptions.NoLoadAutomap));
        _o.NoToolbar = Ticked(nameof(GameOptions.NoToolbar));
        _o.NoPartyFX = Ticked(nameof(GameOptions.NoPartyFX));
        _o.NoHelpSound = Ticked(nameof(GameOptions.NoHelpSound));
        _o.NoAutoSave = Ticked(nameof(GameOptions.NoAutoSave));
        _o.NoBlinkCursor = Ticked(nameof(GameOptions.NoBlinkCursor));
        _o.MusicVolume = (int)_music.Value;
        _o.FXVolume = _fxRaw;
        // written to the saves directory's settings.ini, never a MORDOR.INI. The smoke tests drive this
        // window, and none of them is going to rewrite the player's settings.
        if (Environment.GetEnvironmentVariable("MORDOR_TEST") == null) _o.Save(_gameDir);
        Accepted = true;
    }
}
