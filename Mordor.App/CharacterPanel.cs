using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>The seven pages of the character window, in the order the help lists them.</summary>
/// <summary>
/// The seven pages, in the order the form declares them - which is not something the help's
/// screenshot can show, because VB's SSTab moves the selected tab's row down next to the content.
/// bm13.png has Stats selected, so its rows read Look/Buffers/Misc then Stats/Resist./Char/Guild,
/// and that picture is equally consistent with this order and with its reverse.
///
/// <c>sub1560</c> settles it (MODULE6.BAS:4588-4606). It takes a page number and sets
/// <c>tabStatTab</c> to a tab index, and <c>fn1521</c> hands it one per function key
/// (MAINMOD2.BAS:3533-3567):
///
/// <code>
/// F3  Character Stats     tabStatTab = 0 directly
/// F4  Resistance          sub1560(3) -> 1
/// F6  Character Info      sub1560(6) -> 2
/// F7  Guild/Abilities     sub1560(7) -> 3
/// F2  Area Look           sub1560(4) -> 4
/// F9  Buffer Information  tabStatTab = 5 directly
///     item, spell and companion info: sub1560(1), (8) and (9) -> 6
/// </code>
///
/// <c>tabStatTab_event0000</c> (FRMCHAR1.BAS:322-341) says the same thing from the other end -
/// clicking a tab calls <c>mdrcharSetInfo</c> with the page that tab wants: tab 1 asks for 3,
/// tab 2 for 6, tab 3 for 7 and tab 4 for 4. Two routines, one order.
///
/// F5 is the odd one out: it calls <c>mdrcharShowInfo(2)</c>, and page 2 is the one page
/// <c>sub1560</c> does not switch to a tab for - it writes the experience line into the Messages
/// window instead.
/// </summary>
public enum CharTab { Stats = 0, Resist = 1, Char = 2, Guild = 3, Look = 4, Buffers = 5, Misc = 6 }

/// <summary>
/// The Character Window, <c>FRMCHAR1</c> - "captioned with the current character's name" and the
/// place "where all of the character and statistical information can be found" (help, The Game
/// Screen). It is a tabbed window there and it is one here, with the same seven pages:
///
/// - <b>Stats</b>: "your character's Name, Guild Title and all other important statistics,
///   including special modifiers (e.g. invisible, levitating, poisoned)"
/// - <b>Resist.</b>: "all resistances and the current resistance level (either '-' for none, or
///   the resistance percentage)"
/// - <b>Char</b>: "total monsters killed, how many times the character has died (and has been
///   raised with complications), the natural and modified (by items) stats of the character"
/// - <b>Guild</b>: "all of the guilds the character is a member of, the level of each guild, and a
///   list of the abilities that the character has, along with a rating (between 1 and 100) and
///   which guild the ability comes from"
/// - <b>Look</b>: "the characters that are in the same room as the current character"
/// - <b>Buffers</b>: the ten spell and item slots
/// - <b>Misc</b>: "either Item, Spell or Companion Information"
///
/// The original paints these with <c>sub09B8</c>, one line at a time into a picture box at a row
/// and column, bold for a heading. This lays the same lines out as text, which is also what makes
/// CHARSTAT.TXT come out right - see <see cref="CharStatFile"/>.
/// </summary>
public sealed class CharacterPanel : UserControl
{
    private readonly Func<short, Item?> _item;
    private readonly IReadOnlyList<Spell> _spells;

    private readonly TabControl _tabs;
    private readonly TextBlock _misc;
    /// <summary>The Stats page: elaTabFrame(0)'s labels, each at its form rectangle (<see cref="StatsPage"/>).</summary>
    private readonly Canvas _stats = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScrollViewer _statsViewer;
    private readonly Canvas _char = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _guild = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _resist = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Canvas _look = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    /// <summary>The Misc page when it is painted piece by piece (an item so far); <see cref="_misc"/> holds the rest.</summary>
    private readonly Canvas _miscPage = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

    /// <summary>
    /// The picture box's font, picTabPicture's in frmCharStat1's form data: it stores none, so it is
    /// VB3's default MS Sans Serif 8.25 bold, through the Options fonts (UiFonts). The form's own
    /// header stores Courier New 8.25 bold, but no control without a font of its own is drawn in
    /// it: bm13.png shows lblStatInfo's and lblRaceInfo's captions, which store none either, in a
    /// bold sans serif. The Char page's columns are counted in this font's widest character.
    /// </summary>
    private static DesignFont PictureFont => UiFonts.Design(DesignFonts.CharStat1, "picTabPicture");
    private static FontFamily PictureFace => UiFonts.FamilyOf(DesignFonts.CharStat1, PictureFont);
    private static double PageFontSize => UiFonts.SizeOf(DesignFonts.CharStat1, PictureFont);
    private readonly StackPanel _buffers;

    private Character? _shown;

    /// <summary>Raised when a buffer slot is clicked, which fires it just as Shift+N does.</summary>
    public event Action<int>? BufferFired;

    /// <summary>
    /// Raised when something is dropped on a buffer slot. "Your character has the ability to move
    /// (Drag and Drop) spells and items into the slots" (help, Buffers).
    /// </summary>
    public event Action<int, DragPayload>? BufferAssigned;

    /// <summary>
    /// Where this character stands in the party, for <c>elaGroupInfo</c>. One of one until a
    /// caller says otherwise, which is what a character running alone is.
    /// </summary>
    private int _partyPosition = 1, _partySize = 1;


    /// <summary>
    /// "Character N of M" (MAINMOD2.BAS:5647-5649), where N is one-based. The frame knows the
    /// party; this control only knows the character it was handed.
    /// </summary>
    public void ShowPartyPosition(int oneBased, int size)
    {
        _partyPosition = oneBased;
        _partySize = size;
    }

    public CharacterPanel(Func<short, Item?> item, IReadOnlyList<Spell> spells)
    {
        _item = item;
        _spells = spells;

        _misc = Page();
        _buffers = new StackPanel { Spacing = 2 };
        // everything else on the window inherits picTabPicture's font, VB3's default (PictureFont)
        UiFonts.Set(this, DesignFonts.CharStat1, PictureFont);

        // elaTabFrame(0)'s own offset from the picture box is in the page: no margin of the port's
        var statsTab = Tab("Stats", _stats, new Avalonia.Thickness(0));
        _statsViewer = (ScrollViewer)statsTab.Content!;
        // a larger Options font spreads the page across only as far as the window has room for
        _statsViewer.SizeChanged += (_, e) => { if (e.WidthChanged && _shown != null) StatsPage(_shown); };

        _tabs = new TabControl
        {
            Padding = new Avalonia.Thickness(0),
            Items =
            {
                statsTab,
                Tab("Resist.", _resist),
                Tab("Char", _char),
                Tab("Guild", _guild),
                Tab("Look", _look),
                Tab("Buffers", _buffers),
                Tab("Misc", new StackPanel { Children = { _misc, _miscPage } }),
            },
        };
        // tabStatTab's form data (MORDOR.EXE RCDATA 8): four tabs to the row, so the seven wrap onto
        // two, and picTabPicture starts 510 twips down the control, 34 pixels
        IndexTab.Apply(_tabs, perRow: 4, strip: 32, DesignFonts.CharStat1, "tabStatTab");
        Content = _tabs;
    }

    private static TextBlock Page() => new()
    {
        Foreground = Brushes.Gainsboro,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TabItem Tab(string header, Control body, Avalonia.Thickness? padding = null) => new()
    {
        Header = header,
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = padding ?? new Avalonia.Thickness(8, 6),
            Content = body,
        },
    };

    /// <summary>CTRL-l / b / m / s / r / c / g, and the Control Panel's Show.</summary>
    public CharTab Selected
    {
        get => (CharTab)Math.Clamp(_tabs.SelectedIndex, 0, 6);
        set => _tabs.SelectedIndex = (int)value;
    }

    /// <summary>
    /// Redraws every page for this character. The original repaints only the page on top and marks
    /// the rest stale; this is cheap enough to do the lot.
    /// </summary>
    public void Show(Character c)
    {
        _shown = c;
        StatsPage(c);
        Draw(_resist, ResistTabPage.Paint(c, _item));
        DrawCharPage(c);
        Draw(_guild, GuildTabPage.Paint(c, _item, _spells));
        BuffersPage(c);
    }

    /// <summary>
    /// The Look page: "the characters that are in the same room as the current character... useful
    /// for finding out who is in a room if you've stumbled over somebody but don't know who." The
    /// walk is <see cref="LookAround"/>; the leader is always on it, so the page is never empty
    /// (the port's "Nobody else is here." is gone).
    /// </summary>
    public void ShowLook(IReadOnlyList<LookAround.Entry> here) => Draw(_look, LookAround.Paint(here));

    /// <summary>
    /// The Misc page, which the original fills from whatever was last clicked - an item, a spell or
    /// a companion. The describing is done by the callers, who have the lookups; this only shows it.
    /// </summary>
    public void ShowMisc(string what)
    {
        _misc.Text = what;
        _miscPage.Children.Clear();
        _miscPage.Width = _miscPage.Height = 0;
        Selected = CharTab.Misc;
    }

    /// <summary>
    /// The Misc page painted as the original paints it - an item's, from <see cref="ItemInfoPage"/>.
    /// </summary>
    public void ShowMiscPage(IEnumerable<CharStatFile.Piece> pieces, Avalonia.Media.IImage? picture = null, bool noStretch = false)
    {
        _misc.Text = "";
        var all = pieces.ToList();
        Draw(_miscPage, all, spread: ItemInfoPage.IsItemPage(all));
        if (picture != null)
        {
            // MAINMOD2.BAS:4950-4965: the creature's 60-pixel cell at the top right of the picture box,
            // 15 pixels down, drawn before the text so the text lies over it. Stretched by the window's
            // width against its 4110-twip default (274 pixels), unless No StretchBlt is set, when it
            // is 60 by 60 at the width less 75.
            double box = Math.Max(0, _tabs.Bounds.Width - 16), s = Bounds.Width / 274;
            double size = noStretch ? 60 : 60 * s;
            var img = new Avalonia.Controls.Image { Source = picture, Width = size, Height = size };
            Canvas.SetLeft(img, noStretch ? box - 75 : box - 60 * s * 1.25);
            Canvas.SetTop(img, 15);
            _miscPage.Children.Insert(0, img);
            _miscPage.Width = Math.Max(_miscPage.Width, box);
            _miscPage.Height = Math.Max(_miscPage.Height, 15 + size);
        }
        Selected = CharTab.Misc;
    }

    // ------------------------------------------------------------------ the pages

    /// <summary>
    /// The Stats page, read off the help's own screenshot of this window - game_files/bm13.png,
    /// which is 273x368 and so is the window at its table default of 274x368.
    ///
    /// <code>
    ///   "Requnix"                     &lt;- the window's caption, not part of the page
    ///   Male Giant (Neutral)
    ///   Gladiator (107)
    ///   [ You are Alone ]
    ///   Age                       52
    ///   Spells               210/210
    ///   Spell Level               54
    ///   Hits                 500/500
    ///   Att/Def              230/276
    ///   Experience         2,425,228
    ///   Gold                   1,310
    /// </code>
    ///
    /// Four things the port had wrong. It printed the name again at the top, where the original
    /// leaves that to the caption. It wrote the guild and the title together - "Nomad - Vagrant (1)"
    /// - where the screenshot shows the title and the level alone: "Gladiator" is the Warrior title
    /// for levels 90 to 119, which is what a level 107 Warrior holds, and <see cref="GuildTitles"/>
    /// has said so in its own summary all along. It had no Age line. And it had a "Next level" line
    /// that is not on the form at all - that number belongs to the toolbar readout and to F5.
    ///
    /// The order is the screenshot's, which is not the order the port had either.
    ///
    /// Each line is its own label, as on the form (<see cref="CharStatForm"/>), in its own font:
    /// the captions, the race and title lines and the states in the form's MS Sans Serif 8.25 bold,
    /// the face every other tab is in; "You are Alone" in elaGroupInfo's MS Sans Serif 8.25 plain;
    /// only lblStat's figures in Courier New 8.25, set right - all through the Options fonts, so
    /// NonPro bold, NonPro and Pro. The page had been one text in lblStat's Courier New, its
    /// columns lined up in spaces, which put every caption in the Pro font (corrected 2026-10-14,
    /// docs/FIDELITY.md).
    /// </summary>
    private void StatsPage(Character c)
    {
        _stats.Children.Clear();
        var form = DesignFonts.CharStat1;
        DesignFont caption = UiFonts.Design(form, "lblStatInfo(0)"), figure = UiFonts.Design(form, "lblStat(0)"),
                   race = UiFonts.Design(form, "lblRaceInfo"), guild = UiFonts.Design(form, "lblGuildInfo"),
                   special = UiFonts.Design(form, "lblSpecial(0)"), group = UiFonts.Design(form, "elaGroupInfo");
        // the page grows with the Options fonts as a form page does (FormPage), and down to at least
        // the tallest line a row holds, so a larger chosen font never runs one row into the next
        var (growX, growY) = UiFonts.FormGrowth(form.Pass);
        // across, no further than the page has room for (never less than the form's own size): the
        // scroll viewer scrolls down only, and the figures are set right against the frame's edge
        double room = _statsViewer.Viewport.Width > 0 ? _statsViewer.Viewport.Width : _statsViewer.Bounds.Width;
        if (room > 0)
            growX = Math.Max(1, Math.Min(growX, room / ((CharStatForm.FrameLeft + CharStatForm.elaTabFrame0.Width) / FormPage.Twips)));
        double line = new[] { caption, figure, figure with { Bold = true }, special }.Max(f => UiFonts.LineHeight(form, f));
        growY = Math.Max(growY, line / (CharStatForm.lblStat0.Height / FormPage.Twips));
        double X(double twips) => (CharStatForm.FrameLeft + twips) / FormPage.Twips * growX;
        double Y(double twips) => (CharStatForm.FrameTop + twips) / FormPage.Twips * growY;
        double W(double twips) => twips / FormPage.Twips * growX;
        double H(double twips) => twips / FormPage.Twips * growY;
        double right = X(CharStatForm.elaTabFrame0.Width), bottom = 0;

        // a label: its caption in its font, set left (0), right (1) or centred (2) in its box at
        // that top; a caption wider than its box runs out of it on the side away from the alignment
        void Label(string text, DesignFont font, double left, double width, double top, int align, IBrush? color = null)
        {
            var t = UiFonts.Set(new TextBlock
            {
                Text = text,
                Foreground = color ?? Brushes.Gainsboro,
                FontWeight = font.Bold ? FontWeight.Bold : FontWeight.Normal,
                TextWrapping = TextWrapping.NoWrap,
                ClipToBounds = false,
            }, form, font);
            FormPage.Lift(t, UiFonts.BoldLift(form.Pass, font));
            double wide = UiFonts.TextWidth(text, form.Pass, font);
            double x = align switch { 1 => X(left) + W(width) - wide, 2 => X(left) + (W(width) - wide) / 2, _ => X(left) };
            Canvas.SetLeft(t, x);
            Canvas.SetTop(t, Y(top));
            _stats.Children.Add(t);
            right = Math.Max(right, x + wide);
            bottom = Math.Max(bottom, Y(top) + Math.Max(H(CharStatForm.lblStat0.Height), UiFonts.LineHeight(form, font)));
        }
        // an Elastic: VideoSoft's bevelled box, its caption centred in it
        void Elastic(FormRect r, int top, string? text = null, DesignFont font = default)
        {
            var box = new Border
            {
                BorderBrush = FormPage.Bevel, BorderThickness = new Avalonia.Thickness(1),
                Width = W(r.Width), Height = H(r.Height),
            };
            if (text != null)
            {
                box.Height = Math.Max(box.Height, UiFonts.LineHeight(form, font) + 2);
                box.Child = UiFonts.Set(new TextBlock
                {
                    Text = text, Foreground = Brushes.Gainsboro,
                    FontWeight = font.Bold ? FontWeight.Bold : FontWeight.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                }, form, font);
            }
            Canvas.SetLeft(box, X(r.Left));
            Canvas.SetTop(box, Y(top));
            _stats.Children.Add(box);
            bottom = Math.Max(bottom, Y(top) + box.Height);
        }

        // lblRaceInfo and lblGuildInfo, centred (MAINMOD2.BAS:5586-5591). Title already carries the
        // level: "Vagrant (1)", which is the form the screenshot shows
        Label($"{c.Sex} {c.Race} ({c.Alignment})", race, CharStatForm.lblRaceInfo.Left, CharStatForm.lblRaceInfo.Width, CharStatForm.lblRaceInfo.Top, 2);
        Label(c.Title, guild, CharStatForm.lblGuildInfo.Left, CharStatForm.lblGuildInfo.Width, CharStatForm.lblGuildInfo.Top, 2);
        // elaGroupInfo (MAINMOD2.BAS:5642-5651), an Elastic of elaTabFrame(0)'s, so on this page alone
        Elastic(CharStatForm.elaGroupInfo, CharStatForm.elaGroupInfo.Top,
                _partySize <= 1 ? "You are Alone" : $"Character {_partyPosition} of {_partySize}", group);

        // lblStatInfo and lblStat 0 to 6. AgeYears, because the original shows 52 where the record
        // holds days. lblStatInfo 1 and 2 are loaded and shown for everyone (FRMCHAR1.BAS:128-172),
        // a character with no spell points included; the port had left them off for non-casters
        var (att, def) = AttackDefence(c);
        string[] figures =
        {
            $"{c.AgeYears}",
            $"{CharStatFile.FormatNumber(c.CurSpellPoints)}/{c.MaxSpellPoints}",
            $"{Rules.SpellLevel(c.Guild.Level)}",
            $"{Math.Max(0, c.CurHits)}/{c.MaxHits}",
            $"{att}/{def}",
            $"{c.Guild.Experience:N0}",
            CharStatFile.FormatNumber(c.GoldOnHand),
        };
        var info = CharStatForm.lblStatInfo0;
        for (int n = 0; n < figures.Length; n++)
        {
            var (left, width) = CharStatForm.Figure(n);
            Label(CharStatForm.Captions[n], caption, info.Left, info.Width, CharStatForm.RowTop(n), 0);
            Label(figures[n], figure, left, width, CharStatForm.RowTop(n), 1);
        }

        // elaBorder, moved to just above Strength (FRMCHAR1.BAS:159)
        Elastic(CharStatForm.elaBorder, CharStatForm.BorderTop);

        // mdrcharStats page 11 (MAINMOD2.BAS:5623-5625) paints "Stats(i) + StatMods(i)" - the
        // stat as gear leaves it, not the bare one on the record.
        // Str$(Stats + StatMods) with no floor, bold when the natural stat is at least the race
        // maximum plus five (5624-5629) - the bold 36, 24, 26, 24 of bm13.png
        var max = c.RaceDef.Max;
        for (int i = 0; i < 6; i++)
        {
            var (left, width) = CharStatForm.Figure(7 + i);
            Label(RaceData.StatNamesLong[i], caption, info.Left, info.Width, CharStatForm.RowTop(7 + i), 0);
            Label($"{c.StatWithMods(i, _item)}", figure with { Bold = c.Stats[i] >= max[i] + 5 }, left, width, CharStatForm.RowTop(7 + i), 1);
        }

        // lblSpecial, seven red labels captioned from gv07E2$, each in its own place one under the
        // other from Strength's row, shown or hidden as page 12 turns them on (FRMCHAR1.BAS:176-182,
        // MAINMOD2.BAS:5633-5641): bm13.png's Invisible, See Invisible and Protected (4, 5 and 6)
        // stand beside Charisma, Dexterity and the row under it. This used to join StateText and
        // AilmentText with commas, which put them on one line, in a different order, and in
        // different words - POISONED for Poisoned, "Protected 12" for Protected, and a STONE that
        // is not one of the seven.
        foreach (string state in CharacterStates.Showing(c))
        {
            int n = Array.IndexOf(CharacterStates.Names, state);
            Label(state, special, CharStatForm.lblSpecial0.Left, CharStatForm.lblSpecial0.Width, CharStatForm.SpecialTop(n), 1, StateRed);
        }
        // "You have lost track of where you are." used to follow here; no label of the form says it
        _stats.Width = Math.Ceiling(right);
        _stats.Height = Math.Ceiling(bottom);
    }

    /// <summary>lblSpecial's ForeColor, &amp;HFF (pure red), in the dark theme.</summary>
    private static readonly IBrush StateRed = new SolidColorBrush(Color.FromRgb(0xff, 0x40, 0x40));

    /// <summary>
    /// The Char page, painted as the original paints it: each piece of <see cref="CharStatFile.Paint"/>
    /// that has a place goes where <c>sub09B8</c> (MAINMOD2.BAS:3246-3279) puts it - the column times
    /// the font's widest character across (<c>M37C0</c> is the seventh field of the 16-bit TEXTMETRIC,
    /// tmMaxCharWidth), the row less one times its height down, bold for a
    /// heading. The metrics are the regular font's, read before the weight is set. Headings and
    /// values share rows and overlap where the text is wider than the gap, just as they may there.
    ///
    /// On screen a value's or a heading's leading space (<c>Str$</c>'s, and " Item A/D:") is left
    /// off, so Died and Comp line up under Kills and Item A/D under Stats A/D; CHARSTAT.TXT keeps
    /// them (a deviation, docs/FIDELITY.md).
    /// </summary>
    private void DrawCharPage(Character c) =>
        Draw(_char, CharStatFile.Paint(c, _item, toFile: false, DateTime.Now).Select(p => p with { Text = p.Text.TrimStart() }));

    /// <summary>
    /// The pieces that have a place, drawn where sub09B8 draws them - see <see cref="DrawCharPage"/>.
    ///
    /// With <paramref name="spread"/> (an item's Misc page) the column is widened from the widest
    /// character to as much as keeps each piece clear of the next one on its row by a letter's gap,
    /// so the original's columns are kept, scaled to the font: the stat names no longer run together
    /// and a long line ("at the casters Spell Level") stops short of the Allowed Guilds column. The
    /// Req and Mod figures are centred under their stat's name, where the original puts them a column
    /// after the name's start (a deviation, docs/FIDELITY.md).
    /// </summary>
    private static void Draw(Canvas page, IEnumerable<CharStatFile.Piece> pieces, bool spread = false)
    {
        page.Children.Clear();
        var face = new Typeface(PictureFace);
        FormattedText Measure(string s) => new(s, System.Globalization.CultureInfo.CurrentCulture,
                                               FlowDirection.LeftToRight, face, PageFontSize, Brushes.Gainsboro);
        // the widest glyph of the ANSI set the picture box's font covers, and the line height
        double maxWidth = Enumerable.Range(0x20, 0xE0).Where(ch => ch < 0x7F || ch > 0x9F)
                                    .Max(ch => Measure(((char)ch).ToString()).WidthIncludingTrailingWhitespace);
        double height = Measure("X").Height;
        var shown = pieces.Where(p => p.OnScreen).ToList();
        // a piece's own width, in its own weight and slant
        double Width(CharStatFile.Piece p) => new FormattedText(p.Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(PictureFace, p.Italic ? FontStyle.Italic : FontStyle.Normal, p.Bold ? FontWeight.Bold : FontWeight.Normal),
            PageFontSize, null).WidthIncludingTrailingWhitespace;
        double unit = maxWidth;
        // the stat figures' centres, by row and column: under the name one column to their left
        var centreOn = new Dictionary<(int Row, int Col), double>();
        if (spread)
        {
            double gap = Measure("n").WidthIncludingTrailingWhitespace;
            foreach (var row in shown.GroupBy(p => p.Row))
            {
                var inRow = row.OrderBy(p => p.Col).ToList();
                for (int i = 0; i + 1 < inRow.Count; i++)
                    if (inRow[i + 1].Col > inRow[i].Col)
                        unit = Math.Max(unit, (Width(inRow[i]) + gap) / (inRow[i + 1].Col - inRow[i].Col));
            }
            // "Req" and "Mod" clear of the first stat's name, which the figures are centred under
            var names = shown.Where(p => p.Row == ItemInfoPage.StatNameRow).ToDictionary(p => p.Col);
            foreach (var p in shown.Where(p => p.Row is ItemInfoPage.ReqRow or ItemInfoPage.ModRow))
            {
                if (names.ContainsKey(p.Col - 1)) continue;
                var first = shown.Where(q => q.Row == p.Row && q.Col > p.Col).OrderBy(q => q.Col).FirstOrDefault();
                if (first.Text != null && names.TryGetValue(first.Col - 1, out var name))
                    unit = Math.Max(unit, (Width(p) + gap + Math.Max(0, (Width(first) - Width(name)) / 2)) / (name.Col - p.Col));
            }
            foreach (var p in shown.Where(p => p.Row is ItemInfoPage.ReqRow or ItemInfoPage.ModRow))
                if (names.TryGetValue(p.Col - 1, out var name))
                    centreOn[(p.Row, p.Col)] = unit * name.Col + Width(name) / 2;
        }
        double right = 0, bottom = 0;
        foreach (var p in shown)
        {
            var t = UiFonts.Set(new TextBlock
            {
                Text = p.Text,
                Foreground = Brushes.Gainsboro,
                FontWeight = p.Bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = p.Italic ? FontStyle.Italic : FontStyle.Normal,
                ClipToBounds = false,   // an italic's last letter leans past the box
                Padding = new Avalonia.Thickness(0, 0, 3, 0),
            }, DesignFonts.CharStat1, PictureFont);
            if (p.Bold) FormPage.Lift(t, UiFonts.BoldLift(PictureFace, PageFontSize));
            double x = centreOn.TryGetValue((p.Row, p.Col), out double centre) ? centre - Width(p) / 2 : unit * p.Col, y = height * (p.Row - 1);
            Canvas.SetLeft(t, x);
            Canvas.SetTop(t, y);
            page.Children.Add(t);
            right = Math.Max(right, x + Width(p) + 3);
            bottom = Math.Max(bottom, y + height);
        }
        page.Width = right;
        page.Height = bottom;
    }

    /// <summary>
    /// The ten buffers. "clicking on the slot itself" fires one, exactly as Shift-Number does
    /// (help, Buffers), so each row here is a button.
    /// </summary>
    private static Control BufferRow(string label, string stat)
    {
        var row = new DockPanel { LastChildFill = true };
        var cost = new TextBlock { Text = stat, Margin = new Avalonia.Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(cost, Dock.Right);
        row.Children.Add(cost);
        row.Children.Add(new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis });
        return row;
    }

    private void BuffersPage(Character c)
    {
        _buffers.Children.Clear();
        for (int i = 0; i < SpellBuffers.Count; i++)
        {
            int slot = i;
            var b = new Button
            {
                // lblBuffer: LTrim$(Str$(i + 1)) + ". " + what it holds, so 1. to 10.; lblBufferStat on
                // the same line with a spell's cost (mdcharShowBuffer, MAINMOD2.BAS:4633-4665)
                Content = BufferRow($"{i + 1}. {SpellBuffers.Describe(c, i, _spells, _item)}", SpellBuffers.CostText(c, i, _spells)),
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brush.Parse("#16161f"),
            };
            b.Click += (_, _) => BufferFired?.Invoke(slot);

            // lblBuffer_DragDrop: a slot takes an item or a spell and stores it outright - there
            // is no refusal in the original, whatever you drop goes in.
            Avalonia.Input.DragDrop.SetAllowDrop(b, true);
            b.AddHandler(Avalonia.Input.DragDrop.DragOverEvent, (_, e) =>
            {
                e.DragEffects = DragPayload.From(e) == null
                    ? Avalonia.Input.DragDropEffects.None
                    : Avalonia.Input.DragDropEffects.Link;
                e.Handled = true;
            });
            b.AddHandler(Avalonia.Input.DragDrop.DropEvent, (_, e) =>
            {
                if (DragPayload.From(e) is DragPayload p) BufferAssigned?.Invoke(slot, p);
                e.Handled = true;
            });
            _buffers.Children.Add(b);
        }
    }

    /// <summary>The same inputs combat uses (MAINMOD2.BAS:4460).</summary>
    private (int att, int def) AttackDefence(Character c)
    {
        // plus whatever sub0D1D left the stored totals short by (MONSMOD.BAS:3884-3906): the
        // whole stat part while held, and for good after a city-gate clear or a level-0 paralysis
        int stats = c.StatsADNow(_item) + c.StoredADShift(_item);
        var (ga, gd) = Rules.BestGuildAD(c);
        int ia = c.Inventory.Where(s => s.Equipped).Sum(s => _item(s.ItemId)?.Att ?? 0);
        int id = c.Inventory.Where(s => s.Equipped).Sum(s => _item(s.ItemId)?.Def ?? 0);
        return (Math.Max(0, Rules.BaseAD + stats + ga + ia), Math.Max(0, Rules.BaseAD + stats + gd + id));
    }
}
