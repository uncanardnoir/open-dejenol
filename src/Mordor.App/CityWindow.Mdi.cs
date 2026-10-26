using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The city is a frame, the same as the dungeon. In the original there is one frame and both of
/// them are children of it, with the character window, Objects &amp; Spells, Party Information and
/// Messages beside them either way - which is what the help's screenshot of the Control Panel
/// shows, because that screenshot was taken in the city.
///
/// The character window here is the same <see cref="CharacterPanel"/> the dungeon uses, with the
/// same seven pages. It used to be a five-page panel of this port's own invention, and two of those
/// pages were wrong: the resistances page listed ten of the eleven under names that were not
/// <c>gv0620$</c>'s, and the items page numbered the compacted inventory rather than the forty
/// slots the pack actually has.
/// </summary>
public partial class CityWindow
{
    private MdiFrame _frame = null!;
    private MdiChild _characterChild = null!;
    private MdiChild _partyChild = null!;
    private MdiChild _messagesChild = null!;
    private MdiChild _sheetChild = null!;
    private MdiChild _cityChild = null!;
    private MdiChild _automapChild = null!;

    /// <summary>
    /// The size the port lays the character window's contents out at. Below it the contents are
    /// scaled down rather than cut off (<see cref="ShrinkToFit"/>); it is the port's, not the
    /// original's, whose controls scale with the form at any size. Objects &amp; Spells is drawn to
    /// its own form data instead (<see cref="ObjectsForm.Client"/>), in town and in the dungeon.
    /// </summary>
    internal static readonly Avalonia.Size CharacterDrawnAt = new(300, 360);

    /// <summary>
    /// Party Information, in the town and the dungeon alike, is drawn to frmParty's own form data
    /// instead: its client, 4365x1485 twips (MORDOR.EXE, RCDATA 44), and it zooms with the window
    /// both ways, as Form_Resize zooms the form (FRMGROUP.BAS:117-124). The layout is in
    /// CityWindow.axaml, and DungeonWindow.axaml has the same.
    /// </summary>
    internal static readonly Avalonia.Size PartyDrawnAt = new(4365 / 15, 1485 / 15);

    /// <summary>
    /// How much taller than the form's 13 pixels (lblName's 195 twips) a line of Party Information
    /// is in the Options window's font: nothing with the defaults, whose 11-pixel face has a
    /// 13-pixel line. A larger font makes the header and every row that much taller rather than
    /// cutting their text, and the drawing and the window with them (<see cref="FitPartyDrawing"/>;
    /// a deviation, docs/FIDELITY.md).
    /// </summary>
    internal static double PartyLineGrowth => UiFonts.Cached(nameof(PartyLineGrowth), () =>
        Math.Max(0, UiFonts.LabelHeight(DesignFonts.Party, 0, DesignFonts.PlainSans, DesignFont.Default) - 195 / 15));

    /// <summary>A member's row: lblName's 13 pixels, or the Options font's line.</summary>
    internal static double PartyRowHeight => 195 / 15 + PartyLineGrowth;

    /// <summary>
    /// How much wider than the form's own the Party Information drawing is laid out in the Options
    /// window's font: <see cref="UiFonts.FormGrowth"/> across, exactly one with the defaults. Every
    /// column, margin and button is that much wider, so a larger font keeps the room the form gave
    /// each caption - the headings had run together ("Hits Spells Status", "Option HP/SP") - as a
    /// service page drawn to its form's data does (<see cref="FormPage"/>; a deviation,
    /// docs/FIDELITY.md). The window keeps its size and the drawing is zoomed to it.
    /// </summary>
    internal static double PartyGrowthX => UiFonts.FormGrowth(DesignFonts.Party.Pass).X;

    /// <summary>frmParty's columns under the header, from the header's own left: the edges of the table in CityWindow.axaml.</summary>
    internal static readonly double[] PartyColumns = { 72, 49, 42, 53, 64 };

    /// <summary>
    /// The room a long name leaves before a member's hits, in the form's pixels. lblHits is brought
    /// in front of lblName and cuts it where its label begins (FRMGROUP.BAS:371); the port cuts it
    /// this much sooner, or this much before the hits' own text when a five-digit figure reaches
    /// further left than its label (<see cref="PartyRowGrid"/>), so a cut name never runs into them.
    /// </summary>
    internal const double PartyNameGap = 3;

    /// <summary>
    /// frmParty's drawing grown for a larger Options font: the header and the four rows each
    /// <see cref="PartyLineGrowth"/> taller and the buttons that much lower, and every column,
    /// margin, left and button <see cref="PartyGrowthX"/> wider. Returns the drawn size. With the
    /// default fonts, nothing changes.
    /// </summary>
    internal static Avalonia.Size FitPartyDrawing(ElasticCanvas box, Grid header)
    {
        double g = PartyLineGrowth, gx = PartyGrowthX;
        if (g <= 0 && gx <= 1) return PartyDrawnAt;
        header.Height += g;
        // the rows start below the header, the buttons below four rows
        foreach (var c in box.Children)
        {
            double top = Canvas.GetTop(c);
            if (c == header || double.IsNaN(top)) continue;
            Canvas.SetTop(c, top + (c is Button ? 5 * g : g));
        }
        if (gx > 1)
        {
            for (int i = 0; i < header.ColumnDefinitions.Count && i < PartyColumns.Length; i++)
                header.ColumnDefinitions[i].Width = new GridLength(PartyColumns[i] * gx);
            foreach (var t in header.Children.OfType<Avalonia.Controls.Control>())
                t.Margin = new Avalonia.Thickness(t.Margin.Left * gx, t.Margin.Top, t.Margin.Right * gx, t.Margin.Bottom);
            foreach (var c in box.Children)
            {
                double left = Canvas.GetLeft(c), right = Canvas.GetRight(c);
                if (!double.IsNaN(left)) Canvas.SetLeft(c, left * gx);
                if (!double.IsNaN(right)) Canvas.SetRight(c, right * gx);
                // cmdCommand's 945 twips, the drawing's style's Width, as wide again
                if (c is Button b) b.Width = 945 / 15 * gx;
            }
        }
        box.DrawnHeight = PartyDrawnAt.Height + 5 * g;
        return new Avalonia.Size(PartyDrawnAt.Width * gx, PartyDrawnAt.Height + 5 * g);
    }

    /// <summary>
    /// The window's height for a width, frmParty's own proportion (Form_Resize) with the grown
    /// drawing's: taller by the rows' growth, and less tall again by the width's, since the wider
    /// drawing is zoomed down to the same window width.
    /// </summary>
    internal static Func<double, double> PartyAspect(Avalonia.Size drawn) =>
        w => WindowLayout.AspectHeight(GameWindow.Group, w) * (drawn.Height / PartyDrawnAt.Height) / (drawn.Width / PartyDrawnAt.Width);

    /// <summary>
    /// One member's row of Party Information, the same in town and below: frmParty's five labels
    /// where its form data puts them (the table in CityWindow.axaml), in the header's columns.
    /// Every cell has a width of its own and sets its text in it (left, centred or right as the
    /// label's Alignment has it), so what a row holds - a bold name, a five-digit figure - never
    /// moves the cells after it: a row whose content outgrew a column had been laid out a few
    /// pixels off the others.
    ///
    /// lblHits (1095,45 660x195, right-justified) is 44 pixels wide, and "30000/30000" is wider:
    /// VB draws a caption wider than a label that is not AutoSize cut at the label's edges. The
    /// port lets the figure reach left, over the name's room, as far as it needs and keeps it
    /// whole, since lblHits is in front of lblName and the name is what gives way (a deviation,
    /// docs/FIDELITY.md). The name is cut <see cref="PartyNameGap"/> short of lblHits or of the
    /// figure, whichever begins first.
    /// </summary>
    internal static Grid PartyRowGrid(Character m, bool selected, string status, bool well, string option)
    {
        double gx = PartyGrowthX;
        var colour = PartyRowColours.NameAndHits(selected);
        string hits = $"{m.CurHits}/{m.MaxHits}";
        // lblHits ends 5 pixels short of its column's right; its text begins that figure's width before it
        double hitsRight = (PartyColumns[0] + PartyColumns[1] - 5) * gx;
        double hitsLeft = hitsRight - UiFonts.TextWidth(hits, DesignFonts.Party.Pass, DesignFonts.PlainSans);
        // lblName is 15 twips right of the header's Name, and was cut at the column's edge
        double nameLeft = 1 * gx;
        double nameRoom = Math.Max(0, Math.Min(PartyColumns[0] * gx, hitsLeft) - PartyNameGap * gx - nameLeft);
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", PartyColumns.Select(c => (c * gx).ToString(System.Globalization.CultureInfo.InvariantCulture)))),
            Height = PartyRowHeight,
            Background = Avalonia.Media.Brushes.Transparent,      // so the whole row takes clicks
            Children =
            {
                new TextBlock { Text = m.Name, Foreground = colour, FontWeight = PartyRowColours.NameWeight(selected),
                                Margin = new Avalonia.Thickness(nameLeft, 0, 0, 0), Width = nameRoom,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, ClipToBounds = true },
                // lblHits, right-justified, ending under the header's Hits
                new TextBlock { Text = hits, Foreground = colour, Width = hitsRight,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, TextAlignment = Avalonia.Media.TextAlignment.Right,
                                [Grid.ColumnSpanProperty] = 2 },
                // lblSpells, centred in its 615 twips
                new TextBlock { Text = m.CurSpellPoints.ToString(), Foreground = PartyRowColours.Fixed, Width = 41 * gx,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, TextAlignment = Avalonia.Media.TextAlignment.Center,
                                ClipToBounds = true, [Grid.ColumnProperty] = 2 },
                // lblStatus, right-justified, 12 pixels short of its column's right. QBColor 1 for
                // "OK", QBColor 4 otherwise (FRMGROUP.BAS:446-451)
                new TextBlock { Text = status, Foreground = PartyRowColours.Status(well), Width = (PartyColumns[3] - 12) * gx,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, TextAlignment = Avalonia.Media.TextAlignment.Right,
                                [Grid.ColumnProperty] = 3 },
                // lblOption (FRMGROUP.BAS:453-467): the standing order. A VB label that is not
                // AutoSize keeps its size: a caption wider than the label word-wraps inside it and
                // whatever falls below its one line's height is cut off, so a long spell shows the
                // words that fit and never runs into its neighbour. Right-justified in its 900
                // twips, as game_files/bm5.png shows "None".
                new TextBlock { Text = option, Foreground = PartyRowColours.Fixed, Width = 60 * gx,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, TextAlignment = Avalonia.Media.TextAlignment.Right,
                                TextWrapping = Avalonia.Media.TextWrapping.WrapWithOverflow, MaxLines = 1,
                                ClipToBounds = true, [Grid.ColumnProperty] = 4 },
            },
        };
        return row;
    }

    /// <summary>
    /// The port's size for a window it draws with no table entry of its own - the Guild Log, the
    /// Library and the Hall of Records, which the original opens as windows of their own outside
    /// the table - laid out for this much room.
    /// </summary>
    private static readonly Avalonia.Size ServiceSize = new(660, 560);

    /// <summary>The map behind the child window: the buttons and the slider are still its.</summary>
    private AutomapWindow? _automap;

    private CharacterPanel _charPanel = null!;

    /// <summary>
    /// The layout lives on the party leader, as it does in the dungeon: the Control Panel writes
    /// slot 0 of the character array whichever character has the focus.
    /// </summary>
    private Character Leader => _party.Members[0];

    /// <summary>
    /// Whether frmCityMain would be the active form: the town is the child last clicked, and it
    /// starts as the one, since sub14E0 shows frmCityMain after frmParty and frmAutoMap
    /// (MAINMOD2.BAS:3163-3171). A click on a child activates it, as it does a form.
    /// </summary>
    private bool CityIsActive() =>
        // a service up over the town is a form of its own (frmCityStore and the rest, FRMCTYM.BAS:192-203),
        // the active one from its opening until another window is clicked, and the town is again once it is
        _cityChild.IsVisible && !_cityChild.Minimised
        && (Host.Active ?? _cityChild) == _cityChild;

    private void BuildMdi()
    {
        _frame = new MdiFrame(Host, Shed);
        _charPanel = new CharacterPanel(Lookup, _spells);

        // the sizes these contents are laid out at, below which they are scaled rather than cut
        _characterChild = _frame.Adopt(GameWindow.Character, Captioned(), _charPanel, CharacterDrawnAt);
        // frmCharStat3's client (MORDOR.EXE RCDATA 10), scaled down to a smaller window; a larger one gives its lists the room (ObjectsForm.Reflow)
        // rather than zoomed as Form_Resize zooms it (FRMCHAR3.BAS:408-444); the tab control's own 2-pixel inset is in the drawing
        IndexTab.Apply(SheetTabs, perRow: 3, strip: 22, DesignFonts.CharStat3, "tabItemTab");
        _sheetChild = _frame.Adopt(GameWindow.ObjectsAndSpells, "Objects & Spells", SheetTabs, ObjectsForm.Client);
        _sheetChild.BodyMargin = new Avalonia.Thickness(0);
        ObjectsForm.LinkScrollBar(PackList, PackScroll, ObjectsForm.PackRowPitch);
        ObjectsForm.LinkScrollBar(SpellList, SpellScroll, ObjectsForm.SpellRowPitch, wholeRows: true);
        ObjectsForm.FitSpellRow(SpellLabel, SpellText);
        // the Companions page drawn to the form (CompanionsPage): a name clicked names it, a picture
        // double-clicked is Info, a numbered button kicks its companion out; no right-click attack in town
        _companionPage = new CompanionsPage(CompanionPage, CarryLine, () => _options.NoStretch,
            name: _ => OnCompanionName(null, null!), info: _ => OnCompanionInfo(null, null!),
            kickOut: _ => OnCompanionKickOut(null, null!), attack: null);
        ObjectsForm.FitCompanionPage(CompanionPage);
        ObjectsForm.Reflow(SheetTabs, (Canvas)PackList.Parent!, PackList, PackScroll, PackModButtons, new[] { PackEquipBtn, PackUseBtn, PackDropBtn, PackInfoBtn },
                           (Canvas)SpellList.Parent!, SpellList, SpellScroll, CompanionPage, _companionPage);
        var partyDrawn = FitPartyDrawing(PartyBox, PartyHeader);
        _partyChild = _frame.Adopt(GameWindow.Group, "Party Information", PartyBox, partyDrawn, grow: true);
        if (partyDrawn != PartyDrawnAt) _partyChild.AspectHeight = PartyAspect(partyDrawn);
        // the form's own offsets are in the drawing; the port's 4-pixel body margin would take
        // eight pixels off a client the drawing is meant to fill
        _partyChild.BodyMargin = new Avalonia.Thickness(0);
        _messagesChild = _frame.Adopt(GameWindow.Messages, "Messages", MessagesBox);
        _messagesChild.Restored += _ => Avalonia.Threading.Dispatcher.UIThread.Post(ShowMessages, Avalonia.Threading.DispatcherPriority.Background);
        // frmCityMain is gv19C2 index 10 (FRMCTYM.BAS:95-104, 171), drawn to its form's own design
        // and scaled to the size the character keeps for it. FRMCTYM.FRM gives it a Fixed Double
        // border and no ControlBox, MinButton or MaxButton: it cannot be resized, rolled up or
        // closed, and cityw1.png shows its caption bare.
        _cityChild = _frame.Adopt(GameWindow.City, "The City", CityBox);
        _cityChild.Chrome(sizable: false, minButton: false, maxButton: false, closeBox: false);
        _cityChild.BodyMargin = new Avalonia.Thickness(0);
        // a service over the town: a window of its own, as frmCityStore and the rest are forms of
        // their own, and more are made as more are opened together (CityWindow.Services.cs)
        _svc = NewServiceWindow();

        // FRMAUTOM is a child of the one frame in the original, and the city is a child of that
        // same frame, so the map sits beside the town exactly as it sits beside the dungeon. It
        // was a dialog here, which meant the town was blocked for as long as you were reading it.
        _automapChild = _frame.Adopt(GameWindow.Automap, "Automap", null);
        _automapChild.IsVisible = false;   // ShowAutomap fills it and shows it
        AutomapWindow.WireMouse(_automapChild, () => _automap);

        WireDragAndDrop();
        WireSpellText();
        // the mods' Objects & Spells commands, if any (docs/MODDING.md)
        ModSlots.AddObjectsCommands(PackModButtons, () => new Mordor.Modding.ObjectsCommandContext(_ch, inDungeon: false, inCombat: false, Log, Lookup),
            // a command that moved the pack (Quality of Life's Sort) is a run of dragged swaps, each of
            // which ends in the Store's "UPDATE" (FRMCHAR3.BAS:583)
            () => { Save(); RefreshPack(); Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch)); _storeUpdate?.Invoke(); });
        ModSlots.OnModsChanged(this, RefreshParty);   // a mod party column switched on or off
        ModSlots.OnLiveModsChanged(this, RefreshPack);   // a mod's pack-line marks switched on or off
        // the city is a child of frmMain, whose close box says to use the EXIT buttons (FRMMAIN.BAS:171-175)
        // and any other close - a session ending - is FRMMAIN.BAS:176-192: in town there are no
        // monsters, so it asks "Exit Mordor?", and Yes unloads the party with sub16CC (gv1B52 holds)
        OriginalBox.RefuseCloseBox(this, async () =>
        {
            if (!await Confirm("EXIT", "Exit Mordor?")) return false;
            Save();
            await PartyUnload.Run(this, _party, SavesDir, Log);
            return true;
        });
    }

    /// <summary>
    /// The same three drags the dungeon has. They are the frame's, not the dungeon's: the
    /// character window with its buffers is here too, and so is Objects &amp; Spells, so "move
    /// (Drag and Drop) spells and items into the slots" and "swap items" work in town as well.
    /// </summary>
    private void WireDragAndDrop()
    {
        // the line pressed on (lblItem(Index).Drag), any slot, Hands and empty ones too:
        // lblItem_MouseMove drags whatever it is on (FRMCHAR3.BAS:589-597)
        Dragging.SourceLines(PackList, i => i < Character.PackSize ? DragPayload.Item(i) with { Owner = _ch } : null);
        Dragging.SourceLines(SpellList, i => i < _known.Count ? DragPayload.Spell(_known[i].SpellIndex) with { Owner = _ch } : null);
        // the Companions page: a companion dragged onto another changes places with it, or to the
        // Confinement's Comp. boxes (picCompanion_DragDrop, FRMCHAR3.BAS:763-779)
        CompanionDrag.Wire(_companionPage.Pictures, () => _ch.Companions, Log, () => { Save(); RefreshCompanions(); });

        // an item dropped on another item changes places with it, and the buffers follow
        Dragging.Target(PackList, DragKind.Item, p =>
        {
            int onto = PackList.SelectedIndex;
            // a drag between slots is silent; "Slot #a and Slot #b change places." was the port's.
            // lblItem_DragDrop ends "If fn03BD(frmCityStore) Then frmCityStore!lblCall = "UPDATE""
            // (FRMCHAR3.BAS:583), after its ITEM branch whether or not that swapped anything - onto
            // the same slot or onto the Hands (562-563) it swaps nothing and still sends it
            if (onto != p.Index && _ch.SwapSlots(p.Index + 1, onto + 1))
            {
                Save();
                RefreshPack();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch));
            }
            _storeUpdate?.Invoke();
        });

        // a line of the Bank's list dropped on a pack slot takes it out (FRMCHAR3.BAS:530-561)
        Dragging.Target(PackList, DragKind.Vault, p => TakeFromVault(p.Index, PackList.SelectedIndex));

        // anything let go over Equip, Use, Drop or Info presses it (cmdCommand_DragDrop, FRMCHAR3.BAS:252-254)
        PackCommands.Wire(PackList, () => _ch, PackEquipBtn, PackUseBtn, PackDropBtn, PackInfoBtn);

        _charPanel.BufferAssigned += (slot, p) =>
        {
            if (slot < 0 || slot >= SpellBuffers.Count) return;
            // lblBuffer_DragDrop (FRMCHAR1.BAS:219-233), an empty slot included
            if (p.BufferValue(_ch) is int value) _ch.Buffers[slot] = value;
            Save();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _charPanel.Show(_ch));
        };
    }

    /// <summary>
    /// <c>sub14E0</c> (MODULE6.BAS:3152-3160) is what brings a character into play, and among the
    /// windows it puts up is this one: <c>If Not gv1A66.M5F1B Then frmAutoMap.Show</c>. So the map
    /// is open from the start unless the player has ticked "No Load Automap" in the Options window,
    /// which is precisely what that option is for.
    ///
    /// A character comes into play in town, before there is any dungeon to be in, so this is where
    /// sub14E0 really runs - the dungeon does the same when you walk down into it.
    /// </summary>
    private void OpenAutomapIfWanted()
    {
        // a character run below ground: sub14E0 shows the map with the character on their level,
        // so Form_Load opens it there (FRMAUTOM.BAS:94-95), and frmCityMain is never loaded. The
        // dungeon does that one, as it arrives with no map handed down to it.
        if (_ch.DungeonLevel > 0 && !_ch.IsDead) return;
        _automapLoadedInTown = true;
        if (_options.NoLoadAutomap) return;
        // a body - at any depth - opens the map on its level (see ShowAutomap)
        ShowAutomap(null, null!);
        // sub14E0 shows the map first and frmCityMain after it, and the city's Form_Load
        // minimises a map that is loaded (FRMCTYM.BAS:154-156). The dead are shown frmDungeonMain
        // instead (MAINMOD2.BAS:3170-3180), whose Form_Load puts the map back to a normal window
        // (FRMDUNM.BAS:297-299): the port had minimised theirs as though the city had loaded.
        if (AutomapLayout.CityShownAtStart(_ch.DungeonLevel, _ch.IsDead)) _automapChild.Minimised = true;
        Focus();
    }

    /// <summary>
    /// What frmCityMain's Form_Load does to a loaded map: "UPDATE" - "You are in the city.", the
    /// arrow off (FRMCTYM.BAS:149-151) - and then minimised (154-156). An unloaded map is left
    /// alone: gv1D64 is 0 once the map has unloaded (FRMAUTOM.BAS:189), so no "UPDATE" is sent.
    /// </summary>
    private void CityFormLoadTellsAutomap(SharedMap? map = null)
    {
        if (_automap == null || !_automapChild.IsVisible) return;
        map ??= SharedMap.Load(SavesDir);
        _automapChild.Minimised = true;
        _automap.InTown(Math.Max(DungeonState.Load(SavesDir).DeepestReached, map.DeepestVisited));
    }

    /// <summary>
    /// Whether sub14E0's <c>frmAutoMap.Show</c> has been decided here, in town - and so whether the
    /// map, open or shut, is this frame's to hand to the dungeon. False only while a character run
    /// below ground has not yet walked out.
    /// </summary>
    private bool _automapLoadedInTown;

    /// <summary>
    /// The one frmAutoMap coming back up from the dungeon's frame. Nothing on the way unloads it
    /// (only sub16E0 does, MAINMOD2.BAS:5970), so the level it shows and what is drawn on it come
    /// with it - the town's map used to be its own copy, read once, which knew nothing of the trip.
    /// Then frmCityMain's Form_Load: "UPDATE", which says "You are in the city." and takes the arrow
    /// off, and the map minimised (FRMCTYM.BAS:149-156). A map that was shut below stays shut.
    /// </summary>
    private void TakeAutomapBack(DungeonWindow below, bool cityLoads)
    {
        _automapLoadedInTown = true;
        _automap = below.Automap;
        if (_automap == null || !below.AutomapOpen) { _automapChild.IsVisible = false; return; }
        var map = SharedMap.Load(SavesDir);
        _automap.Rebind(map, () => _options.NoBlinkCursor);
        _automap.MoveInto(_automapChild);
        _automapChild.IsVisible = true;
        if (cityLoads) CityFormLoadTellsAutomap(map);
    }

    /// <summary>
    /// Where a service's window goes, and at what size. Each service's own Form_Load takes its size
    /// from <c>M5958(index)</c>, and its place too unless it has none - Left and Top both nought -
    /// when <c>sub02FE</c> centres the form on the screen (MODULE1.BAS:118-121), here the frame;
    /// then <c>sub0524</c> pulls back a window left beyond the edge (FRMCTYS.BAS:436-448 and the
    /// same lines in the other five). Every one of the six has a Fixed Double border (BorderStyle 3
    /// in each .FRM), so none can be dragged to a size and none has a MinButton or MaxButton; the
    /// Seer alone keeps its ControlBox (FRMSEEK.FRM).
    ///
    /// A service drawn to its form's data (<see cref="ShowForm"/>: all six - the Guilds, the Bank, the Morgue,
    /// the Seer, Confinement and the Store) is the form's client exactly, with the window's frame round it
    /// - grown with the page for a larger Options font (<see cref="FormPage.GrowX"/>, a deviation).
    /// DEVIATION: Form_Resize would zoom the form to the size kept for it (sub07B3, sub07C5); the
    /// port draws it at the form's own size, since the sizes kept on the characters were written
    /// from the port's earlier, larger pages. Its place is the character's. It is never cut down to
    /// a frame smaller than the form: it has no scrolling of its own, so the frame's scroll bars
    /// (MdiScroll) reach what lies past the frame's right or bottom edge, as an MDI client's do.
    ///
    /// DEVIATION: the other pages are the port's own layout and bigger than the original's forms, so a
    /// window is made as wide as its page asks when the table's width is smaller, exactly as tall as
    /// the page (a Fixed Double form is its design's size, with nothing below it), and no bigger than
    /// the frame; only a frame too small for the page leaves it to scroll. The window is kept
    /// inside the frame, and centred on the size it really has.
    ///
    /// The dead screen stands in for frmDungeonMain and goes where the Dungeon goes; the Library and
    /// the Guild Log, which have no window-table entry, go in the middle, each its form's client.
    /// </summary>
    /// <summary>Where the dead screen was dragged to while it has been up; null once it closes.</summary>
    private Avalonia.Point? _deadScreenAt;

    private void PlaceService()
    {
        double hw = Host.Bounds.Width, hh = Host.Bounds.Height;
        var (pw, ph) = ServicePageSize();
        double w = pw, h = ph;
        bool placed = false;
        double x = 0, y = 0;
        if (_serviceWhich is GameWindow which)
        {
            var r = Leader.WindowFor(which);
            var (dw, dh) = WindowLayout.Default(which);
            // a mod's Store columns widen the window by their own width (_serviceExtraWidth), on top
            // of the width kept for it, which is written back without it: nought with no mod column
            w = Math.Max(pw, (r.HasSize ? r.Width : dw) + _serviceExtraWidth);
            if (hw > 1) w = Math.Min(w, hw);   // no wider than the frame, before the page is measured at it
            // a Fixed Double form is exactly as tall as its design (frmCityStore and the other five,
            // BorderStyle 3), with nothing below its last control; the port's page is the design, so
            // the window is its height, measured at the width it gets. The table's taller height had
            // left an empty band under the page. The width is capped to the frame first, since a
            // narrower page wraps taller. The dead screen stands in for frmDungeonMain and keeps the
            // Dungeon's size.
            h = which == GameWindow.Dungeon ? Math.Max(ph, r.HasSize ? r.Height : dh) : ServicePageHeight(w);
            // a service drawn to its form's data (ShowForm) is exactly the form's client inside the
            // window's frame, and wider only by a mod's columns
            if (_serviceForm is Avalonia.Size client)
            {
                w = client.Width + FormChromeW + _serviceExtraWidth;
                h = client.Height + FormChromeH;
            }
            placed = !(r.Left == 0 && r.Top == 0);
            x = r.Left; y = r.Top;
            _serviceChild.Chrome(sizable: false, minButton: false, maxButton: false,
                                 closeBox: which == GameWindow.Seer);
        }
        else if (_serviceForm is Avalonia.Size own)
        {
            // the Library and the Guild Log, drawn to their forms' data, have no window-table entry:
            // the form's client in the window's frame, in the middle, and no dragging to a size
            w = own.Width + FormChromeW;
            h = own.Height + FormChromeH;
            _serviceChild.Chrome(sizable: false, minButton: false, maxButton: false, closeBox: false);
        }
        else _serviceChild.Chrome(sizable: true, minButton: false, maxButton: false, closeBox: false);
        // A page of the port's own layout scrolls inside its window (ServiceScroll), so it is capped
        // to the frame. A form drawn to its own data (ShowForm) has no scrolling of its own - its
        // client is the form's client, fixed - so capping it clipped its bottom and right controls
        // (the Store is 480x316 pixels) with no way to reach them. It keeps the form's size, as a
        // Fixed Double MDI child does in a smaller MDI client, and the frame's scroll bars
        // (MdiHost, MdiScroll.Bars) reach what lies past the frame's edges; it is placed no further
        // left or up than the frame's corner, so what is off the frame is always right or below.
        bool formSized = _serviceForm != null;
        if (hw > 1 && !formSized) w = Math.Min(w, hw);
        if (hh > 1 && !formSized) h = Math.Min(h, hh);
        if (!placed) { x = (hw - w) / 2; y = (hh - h) / 2; }
        if (hw > 1) x = Math.Min(x, hw - w);
        if (hh > 1) y = Math.Min(y, hh - h);
        // Every one of these windows has a title bar that moves it: frmDungeonMain and the six
        // Fixed Double services are ordinary MDI children, and a Fixed Double border fixes the size,
        // not the place. The port had held the dead screen still, reasoning that dragging it would
        // uncover a town the dead are never shown (sub14E0, MAINMOD2.BAS:3166-3180) - but the
        // original's frmDungeonMain can be dragged off whatever is behind it, and every service a
        // dead character clicks is this screen (ShowDead), so none of them could be moved.
        _serviceChild.Movable = true;
        // the dead screen put up again while it is up - after a wait, a refused raise - stays
        // where it was dragged, as frmDungeonMain does across sub0930's calls
        if (_serviceWhich == GameWindow.Dungeon && _deadScreenAt is Avalonia.Point at)
        {
            x = at.X; y = at.Y;
            if (hw > 1) x = Math.Min(x, hw - w);
            if (hh > 1) y = Math.Min(y, hh - h);
        }
        _serviceChild.PlaceAt(new Avalonia.Rect(Math.Max(0, x), Math.Max(0, y), w, h));
        Host.BringToFront(_serviceChild);
    }

    /// <summary>
    /// The window a service's page asks for: the page measured at its own width, up to the width the
    /// port lays its pages out for, and as tall as it then is, with the child window's frame round it.
    /// </summary>
    private (double W, double H) ServicePageSize()
    {
        const double chromeW = 2 + 8, chromeH = 22 + 2 + 8;   // MdiChild: border, title bar, margin
        ServicePanel.Measure(new Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
        double pw = Math.Min(ServicePanel.DesiredSize.Width, ServiceSize.Width - chromeW + _serviceExtraWidth);
        ServicePanel.Measure(new Avalonia.Size(pw, double.PositiveInfinity));
        double ph = ServicePanel.DesiredSize.Height;
        return (Math.Max(200, pw + chromeW), Math.Max(120, ph + chromeH));
    }

    /// <summary>The service window's frame round a form's client: MdiChild's one-pixel border and 22-pixel title bar.</summary>
    private const double FormChromeW = 2, FormChromeH = 2 + 22;

    /// <summary>
    /// Puts a service drawn to its form's data (<see cref="FormPage"/>) into the service window. The
    /// form has its own Exit and its own caption on the window's title bar, so the port's heading
    /// strip with its EXIT goes, and so do the margin and the scrolling: the window's client is the
    /// form's client, which holds every control. A redraw in place (the Guild's lblCall "UPDATE",
    /// the Bank's) builds the same size again, so the window still fits it.
    /// </summary>
    private void ShowForm(FormPage page)
    {
        bool was = _serviceForm == page.Client;
        _serviceForm = page.Client;
        ServiceHeader.IsVisible = false;
        ServiceBody.Padding = new Avalonia.Thickness(0);
        ServicePanel.BorderThickness = new Avalonia.Thickness(0);
        ServiceScroll.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        ServiceScroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        _serviceChild.BodyMargin = new Avalonia.Thickness(0);
        ServicePage.Content = page.Page;
        if (!was && ServicePanel.IsVisible) PlaceService();
    }

    /// <summary>The service window back to the port's own page: heading strip, margin and scrolling.</summary>
    private void ShowPortPage()
    {
        _serviceForm = null;
        ServiceHeader.IsVisible = true;
        ServiceBody.Padding = new Avalonia.Thickness(12);
        ServicePanel.BorderThickness = new Avalonia.Thickness(2);
        ServiceScroll.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        ServiceScroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        _serviceChild.BodyMargin = new Avalonia.Thickness(4);
    }

    /// <summary>The window's height for the page laid out across a window <paramref name="w"/> wide.</summary>
    private double ServicePageHeight(double w)
    {
        const double chromeW = 2 + 8, chromeH = 22 + 2 + 8;   // as ServicePageSize
        ServicePanel.Measure(new Avalonia.Size(Math.Max(0, w - chromeW), double.PositiveInfinity));
        return Math.Max(120, ServicePanel.DesiredSize.Height + chromeH);
    }

    /// <summary>
    /// A service's Form_Unload: <c>mdrcharSaveWindowPos -1, index, Me, True</c>, its placement
    /// onto the whole party (FRMCTYS.BAS:473, FRMCITYC.BAS:447, FRMCITYB.BAS:145, FRMCTYG.BAS:666,
    /// FRMMORG.BAS:245, FRMSEEK.BAS:408 - see <see cref="MdiFrame.Save"/>). The dead screen and
    /// the port's own windows have no entry of their own to write.
    /// </summary>
    private void SaveServicePlacement()
    {
        if (!ServicePanel.IsVisible || _serviceWhich is not GameWindow which) return;
        var p = _serviceChild.Placement();
        // the dead screen writes no entry, but keeps its place while it stays up (PlaceService)
        if (which == GameWindow.Dungeon) { _deadScreenAt = p.Position; return; }
        // the width a mod's Store columns added is not the window's own: it is kept as it would be with none
        double width = _serviceExtraWidth > 0 ? Math.Max(1, p.Width - _serviceExtraWidth) : p.Width;
        foreach (var c in _party.Members)
        {
            var r = c.WindowFor(which);
            if (_serviceForm != null)
            {
                // a form drawn at its own size: Me.Width and Me.Height are what Form_Load gave it,
                // the character's entry or, with none, the table's default (the form's own size)
                var (dw, dh) = WindowLayout.Default(which);
                if (!r.HasSize) { r.Width = dw; r.Height = dh; }
                r.Left = p.X; r.Top = p.Y; r.State = 0;
                continue;
            }
            r.Left = p.X; r.Top = p.Y; r.Width = width; r.Height = p.Height; r.State = 0;
        }

    }
    /// <summary>
    /// The mods' columns in the open service changed width (switched on or off): the window's place
    /// is kept as it is now, without the old extra, and it is placed again with the new.
    /// </summary>
    private void ServiceExtraWidthChanged(double extra)
    {
        if (extra == _serviceExtraWidth) return;
        SaveServicePlacement();
        _serviceExtraWidth = extra;
        if (ServicePanel.IsVisible) PlaceService();
    }

    /// <summary>
    /// Every service unloaded, as frmCityMain's Form_Unload does on the way down
    /// (FRMCTYM.BAS:169-179), with what each one's own Exit clears.
    /// </summary>
    private void CloseAllServices()
    {
        if (!AnyServiceOpen) return;
        _guildVisited = "";
        _bankRefresh = null;
        _morgueUpdate = null;
        _guildUpdate = null;
        // each one unloaded in turn, and nothing given back to (a Library over the Guild goes too)
        foreach (var s in OpenServices.ToList())
        {
            s.Return = null;
            InServiceWindow(s, () => CloseService(null, null!));
        }
    }

    /// <summary>The character window's caption: the character's own name, in quotes.</summary>
    private string Captioned() => $"\"{_ch.Name.TrimEnd()}\"";

    private void ApplyWindowLayout() => _frame.Apply(Leader, Screens);
    private void SaveWindowLayout()
    {
        _frame.Save(_party.Members);
        foreach (var s in OpenServices.ToList()) InServiceWindow(s, SaveServicePlacement);
    }

    // the frame's size and place while it was last normal, and whether it was last maximised
    private Avalonia.PixelPoint _frameAt;
    private Avalonia.Size _frameSize;
    private bool _frameMaximised, _frameKnown;

    /// <summary>
    /// The frame's own placement, a port necessity (see <see cref="FramePlacement"/>): put back
    /// from settings.ini before the frame is first shown, so that the children's saved places -
    /// which are only right inside a frame of the size they were saved in - are applied to a frame
    /// that is already that size; and written back as it closes. A saved place whose title strip
    /// no display shows is let go, and the frame opens centred at its design size as before -
    /// still maximised, on the primary display, if it was left maximised.
    /// </summary>
    private void RestoreFramePlacement()
    {
        var saved = PortSettings.Load(SavesDir).Frame;
        if (saved != null && OnSomeScreen(saved))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new Avalonia.PixelPoint(saved.Left, saved.Top);
            Width = saved.Width;
            Height = saved.Height;
            if (saved.Maximised) WindowState = WindowState.Maximized;
            (_frameAt, _frameSize, _frameMaximised, _frameKnown) =
                (Position, new Avalonia.Size(saved.Width, saved.Height), saved.Maximised, true);
        }
        else if (saved is { Maximised: true })
        {
            // only the place is let go: a frame left maximised on a display that has gone is still
            // maximised, on the primary display, at its design size when it is restored
            if (Screens?.Primary is { } primary)
            {
                var wa = primary.WorkingArea;
                double scale = primary.Scaling > 0 ? primary.Scaling : 1;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new Avalonia.PixelPoint(wa.X + Math.Max(0, (int)((wa.Width - Width * scale) / 2)),
                                                   wa.Y + Math.Max(0, (int)((wa.Height - Height * scale) / 2)));
            }
            WindowState = WindowState.Maximized;
        }
        // read once the move or resize is over: Windows moves a window it is maximising before it
        // says it is maximised, so the state is only settled after the event
        void Note() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible) return;
            if (WindowState == WindowState.Normal)
            {
                (_frameAt, _frameSize, _frameMaximised, _frameKnown) = (Position, ClientSize, false, true);
            }
            else if (WindowState == WindowState.Maximized) _frameMaximised = true;
        }, Avalonia.Threading.DispatcherPriority.Background);
        PositionChanged += (_, _) => Note();
        Resized += (_, _) => Note();
        PropertyChanged += (_, e) => { if (e.Property == WindowStateProperty) Note(); };
        Opened += (_, _) => Note();
        Closing += (_, _) => SaveFramePlacement();
    }

    private void SaveFramePlacement()
    {
        if (!_frameKnown) return;
        try
        {
            var p = new FramePlacement(_frameAt.X, _frameAt.Y, Math.Round(_frameSize.Width), Math.Round(_frameSize.Height), _frameMaximised);
            if (!PortSettings.Write(SavesDir, "Frame", p.ToString()))
                CrashLog.Note("could not write Frame to " + PortSettings.PathFor(SavesDir));
        }
        catch (Exception ex) { CrashLog.Note("could not write Frame to settings.ini: " + ex.Message); }
    }

    /// <summary>
    /// Whether any display's working area shows some of a saved frame's title strip, so that it
    /// can be taken hold of and moved (<see cref="FramePlacement.TitleShownOn"/>).
    /// </summary>
    private bool OnSomeScreen(FramePlacement p)
    {
        var all = Screens?.All;
        if (all == null || all.Count == 0) return true;
        foreach (var s in all)
        {
            var wa = s.WorkingArea;
            if (p.TitleShownOn(wa.X, wa.Y, wa.Width, wa.Height, s.Scaling)) return true;
        }
        return false;
    }

    /// <summary>Everything the frame shows for whoever has the focus now.</summary>
    private void RefreshCharacterWindow()
    {
        _characterChild.Title = Captioned();
        _charPanel.ShowPartyPosition(_party.SelectedIndex + 1, _party.Size);
        _charPanel.Show(_ch);
        _charPanel.ShowLook(AreaLook());
        RefreshPack();
        RefreshSpells();
        RefreshCompanions();
        Tools.Show(_ch, Lookup);
    }

    /// <summary>
    /// F2 in the city. "The Area List will display the characters that are in the same room as the
    /// current character" - <c>mdrmazLookAround</c>, with every character in town on floor 0 and in
    /// area -1 (<c>M59BB</c> is set to -1 on reaching town, FRMDUNM.BAS:279), so everyone in town.
    /// </summary>
    private IReadOnlyList<LookAround.Entry> AreaLook()
    {
        List<Character> saved;
        try { saved = Character.LoadAllFrom(SavesDir); }
        catch { saved = new(); }   // another character's save being written is not this window's problem
        // the walk goes down MorgueList, which lists the records in their order (MAZEMOD.BAS:203-224):
        // LoadAllFrom gives the saves in it (Roster.Saved)
        var roster = LookAround.Roster(_party.Members, saved);
        // "MorgueList(l010E).M547A = Chars(m00C0).M59BB" (MAZEMOD.BAS:287): each record's M59BB against
        // the leader's, which the city gate made -1 (FRMCTYM.BAS:112). A record on floor 0 whose M59BB
        // is something else is not in the list; the area is not taken as -1 from being in town.
        (int, int) PlaceOf(Character c) =>
            _party.Members.Contains(c) ? (0, SavedArea(c)) : (c.DungeonLevel, SavedArea(c));
        return LookAround.Walk(_party.Members[0], roster, PlaceOf).Here;
    }

    private List<Floor>? _areaDungeon;

    /// <summary>
    /// A character's <c>M59BB</c> (<see cref="Character.Area"/>), through the same
    /// <see cref="Character.AreaOr"/> fallback the dungeon's SavedArea uses: a save from before the
    /// port kept it takes the area of its square, -1 in town.
    /// </summary>
    private int SavedArea(Character c) => c.AreaOr(k =>
    {
        if (k.DungeonLevel <= 0) return -1;
        try { _areaDungeon ??= MdrLoader.LoadDungeon(System.IO.Path.Combine(_gameDir, "MDATA11.MDR")); }
        catch { return -1; }
        return k.DungeonLevel <= _areaDungeon.Count ? _areaDungeon[k.DungeonLevel - 1].AreaIdAt(k.X, k.Y) : -1;
    });

    /// <summary>
    /// The Items page. The pack is forty fixed slots with the character's Hands in the first, and
    /// the numbers on this list are the ones Swap Items takes and the game's own messages name, so
    /// an empty slot still shows its number.
    /// </summary>
    private void RefreshPack()
    {
        int sel = PackList.SelectedIndex;
        var rows = new List<Control>();
        for (int i = 0; i < Character.PackSize; i++)
        {
            var slot = _ch.Pack[i];
            // the equipped mark is its own label, lblItemStatus (MAINMOD2.BAS:5429-5433), and lblItem
            // stands beside it, so every row's number starts at the same place, an empty row's too
            if (slot == null || (i > 0 && slot.ItemId == 0)) { rows.Add(ObjectsForm.PackRow("", PackLine($"{i + 1}.", false, false))); continue; }
            var it = Lookup(slot.ItemId);
            string mark = slot.Equipped ? "*" : "";
            string text = $"{i + 1}. {(it == null ? "#" + slot.ItemId : Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges))}";
            // with a mod, something after the name (ctx.UI.PackRowDecoration); with none, the line alone
            rows.Add(ObjectsForm.PackRow(mark, ModSlots.PackLine(PackLine(text, PackRow.Bold(it, slot), PackRow.Italic(it, slot)), _ch, it, slot, i)));
        }
        PackList.ItemsSource = rows;
        PackList.SelectedIndex = Math.Clamp(sel, 0, Character.PackSize - 1);
    }

    /// <summary>
    /// One line of the Items page. <see cref="PackRow"/> holds the rule; this only draws it.
    /// </summary>
    private static Control PackLine(string text, bool bold, bool italic) => new TextBlock
    {
        Text = text,
        FontWeight = bold ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal,
        FontStyle = italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
    };

    /// <summary>The slot the list is pointing at, or null when it is empty or the Hands.</summary>
    private InventorySlot? SelectedPackSlot()
    {
        int i = PackList.SelectedIndex;
        if (i <= 0 || i >= Character.PackSize) return null;
        var slot = _ch.Pack[i];
        return slot == null || slot.ItemId == 0 ? null : slot;
    }

    private void OnPackEquip(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // all of cmdCommand_Click is inside "If Chars(Me).Ailments(mc0116) = False" (FRMCHAR3.BAS:203,
        // 249), mc0116 = 2 being paralysis: nothing is equipped and the selection does not move.
        // frmCityMain's Form_Load clears it for everyone there (FRMCTYM.BAS:115), so it rarely bites
        if (_ch.Paralysed) return;
        int idx = PackList.SelectedIndex;
        EquipSelected();
        // "sub0F74 Me, m001A / If m001A < mc0122 - 1 Then lblItem_Click m001A + 1" (FRMCHAR3.BAS:
        // 212-216): the selection moves on to the next row after every Equip, whether sub0F74 did
        // anything or not - only the last slot stays put
        if (idx < Character.PackSize - 1) PackList.SelectedIndex = idx + 1;
    }

    /// <summary><c>sub0F74</c> on the selected slot.</summary>
    private void EquipSelected()
    {
        var slot = SelectedPackSlot();
        if (slot == null) return;   // sub0F74 leaves on an empty slot without a word
        var it = Lookup(slot.ItemId);
        if (it == null) return;
        // and asks nothing about how well you know the item - "You don't know what that is. ..." was
        // this port's
        if (!slot.Equipped)
        {
            // sub0F74 will not even discuss a potion or a scroll - it checks the item's class
            // maximum first and returns without a word (MODULE6.BAS:1724)
            if (!Character.IsWearable(it)) return;
            if (_ch.WearRefusal(slot, it, Lookup) is string why) { Log(why); return; }
        }
        else if (!_ch.CanUnequip(slot, it)) { Log("Cannot unequip!  Item is cursed!"); return; }

        slot.Equipped = !slot.Equipped;
        // MODULE6.BAS:1745 clears the slot curse on the way off, 1786 and 1821 set it on the way on
        if (slot.Equipped) _ch.ApplyEquipCurse(slot, it); else slot.SlotCursed = false;
        Effects.Recompute(_ch, Lookup);
        // silent: sub0F74 speaks only to refuse
        Save();
        RefreshSummary();
        _storeModRedraw?.Invoke();   // a mod's Store column showing what is worn; null with none
    }

    /// <summary>"Must be in Dungeon to Use items." - FRMCHAR3.BAS:225, word for word.</summary>
    private void OnPackUse(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ch.Paralysed) return;   // cmdCommand_Click's "If Ailments(mc0116) = False" (FRMCHAR3.BAS:203)
        Log("Must be in Dungeon to Use items.");
    }

    /// <summary>
    /// Info. FRMCHAR3.BAS:205-208 sends it to <c>sub1560</c> page 1, which that table turns into the
    /// character window's Misc page - so it answers over there, not here.
    /// </summary>
    private void OnPackInfo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ch.Paralysed) return;   // cmdCommand_Click's "If Ailments(mc0116) = False" (FRMCHAR3.BAS:203)
        var slot = SelectedPackSlot();
        if (slot == null) return;   // "If m001A > -1" (FRMCHAR3.BAS:206), silently
        if (Lookup(slot.ItemId) is not Item it) return;   // "If Pack(m001A).Ref > 0" (207); the port's "You cannot make it out at all." is gone
        ShowItemInMisc(it, slot.Alignment, slot.Known);
        Focus();   // frmCharStat1!picTabPicture.SetFocus (FRMCHAR3.BAS:209), as the dungeon's Info
    }

    /// <summary>
    /// Drop, cmdCommand(2) (FRMCHAR3.BAS:227-246). frmCharStat3 is one form in the city and below
    /// ground, with Equip, Use, Drop and Info in its form data (MORDOR.EXE), and Drop's case asks
    /// nothing about DungeonLevel - so the city has it too, and it does what the dungeon's does:
    /// the guild's own items refused (230-231), "Drop the X?" (234), a cursed item worn refused
    /// with a Beep after that (235-237), then the slot on screen emptied (240) and the selection
    /// moved on (241-243). Silent when it goes through.
    /// </summary>
    private async void OnPackDrop(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_ch.Paralysed) return;   // cmdCommand_Click's "If Ailments(mc0116) = False" (FRMCHAR3.BAS:203)
        var slot = SelectedPackSlot();
        if (slot == null) return;   // "If m001A > 0 And m001A < mc0122" / "If Pack(m001A).Ref > 0" (228-229)
        int idx = PackList.SelectedIndex;
        if (Lookup(slot.ItemId) is not Item it) return;
        if (ItemUse.DropProblem(slot, it, idx) is string why)
        {
            if (why != "") Log(why);   // sub085B (FRMCHAR3.BAS:231)
            return;
        }
        string shown = Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges);   // itemNameFor (234)
        if (!await Confirm("DROP ITEM", $"Drop the {shown}?")) return;
        if (ItemUse.CursedRefusal(slot, it, idx) is string cursed)
        {
            Audio.Beep();   // Beep (FRMCHAR3.BAS:236)
            Log(cursed);    // sub085B (237)
            return;
        }
        _ch.RemoveFromPack(slot);   // "mdrcharRemoveItem Me, (m001A)" (240)
        Effects.Recompute(_ch, Lookup);
        RefreshPack();
        if (idx < Character.PackSize - 1) PackList.SelectedIndex = idx + 1;   // 241-243
        Save();
        RefreshSummary();
    }

    /// <summary>
    /// <c>mdrcharSetInfo</c> page 1: the item on the character window's Misc page, painted as
    /// <see cref="ItemInfoPage"/> has it. The pack, the store and the bank all come here.
    /// </summary>
    private void ShowItemInMisc(Item it, ItemAlignment alignment, IdLevel known)
    {
        _charPanel.ShowMiscPage(ItemInfoPage.Paint(it, alignment, known, _spells));
        _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
        _characterChild.Activate();
        Focus();   // picTabPicture.SetFocus: the keyboard leaves a service's box (FRMCTYS.BAS:166, 186; FRMCITYB.BAS:81)
    }

    /// <summary>
    /// The Messages window, <c>sub085B</c>. It keeps twenty lines and no more, and a double-click
    /// empties it (FRMCHAR4.BAS:84-88).
    /// </summary>
    private readonly List<string> _messages = new();

    /// <summary>Each line's look (kind and colour, kept with it), one for one with <see cref="_messages"/>, carried with them to and from the dungeon.</summary>
    private readonly List<MessageLook> _messageLooks = new();

    private void Log(string line)
    {
        var look = new MessageLook(MessageKind.None, Mordor.Modding.ModHost.Active.MessageLineColour(line, MessageKind.None));
        if (!MessageList.Add(_messages, _messageLooks, line, look, Mordor.Modding.ModHost.Active.MessagesKept(MessageList.Kept))) return;
        ShowMessages();
        Audio.Ding();       // sub085B plays MESSAGE.WAV for every line it adds
        Mordor.Modding.ModHost.Active.MessageLogged(line);
    }

    /// <summary>
    /// <c>sub085B</c>'s last two lines (MAINMOD2.BAS:3200-3202): the newest line is the selected
    /// one, which is also what brings it into view.
    /// </summary>
    private void ShowMessages()
    {
        MessageView.Show(MessageLog, _messages, _messageLooks);
    }

    private void OnClearMessages(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _messages.Clear();
        _messageLooks.Clear();
        ShowMessages();
    }

    // ---------------- the Spells page ----------------

    /// <summary><c>m0038</c>: what the Spells page is listing, in the order it lists them.</summary>
    private List<Spellbook.SpellRow> _known = new();

    /// <summary>
    /// As in the dungeon: sub1658 fills the class box (MAINMOD2.BAS:5510-5539), cmbClass_Click lists
    /// the class (FRMCHAR3.BAS:55-140) and sub2424 writes the rows (FRMCHAR3.BAS:681-712).
    /// </summary>
    private void RefreshSpells()
    {
        SpellPage.FillClasses(ClassBox, _ch);
        _known = Spellbook.ClassSpells(_ch, _spells, SpellPage.Kind(ClassBox));
        SpellList.ItemsSource = SpellPage.Rows(_known, _ch, Lookup);
    }

    /// <summary>"Cannot cast spells while in the city!" - FRMCHAR3.BAS:157, word for word.</summary>
    private void OnSpellCast(object? s, Avalonia.Interactivity.RoutedEventArgs e) =>
        Log("Cannot cast spells while in the city!");

    /// <summary>
    /// lblName_DblClick (FRMCHAR3.BAS:617-620) is cmdCast_Click after the name, so the refusal -
    /// but not from the "No Spells Yet" row, whose elaSpells is disabled (FRMCHAR3.BAS:723-724).
    /// </summary>
    private void OnSpellListDoubleTapped(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_known.Count == 0) return;
        OnSpellCast(s, e);
    }

    /// <summary>
    /// cmdInfo_Click (FRMCHAR3.BAS:282-302): whatever is in txtSpell, looked up among every spell
    /// by fn1725 - a spell this character cannot cast has a page too. An empty box does nothing;
    /// a miss has fn1725's line said and the focus sent back to the main window (sub0775); a hit
    /// writes the spell's whole name into the box and puts its page on the character window.
    /// </summary>
    private void OnSpellInfo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string typed = SpellText.Text ?? "";
        if (typed == "") return;
        int i = Spellbook.FindByName(_spells, typed, Log);
        if (i == -1) { Focus(); return; }   // sub0775: gv1BFC.SetFocus (MAINMOD.BAS:1852-1854)
        SpellText.Text = _spells[i].Name;
        // mdrcharSetInfo mc0156 (9): the window comes back from a minimise first (MAINMOD2.BAS:4598)
        _charPanel.ShowMiscPage(SpellInfo.Paint(_spells[i], _ch));
        _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
        _characterChild.Activate();
        // "If frmCharStat1!picTabPicture.Visible Then frmCharStat1!picTabPicture.SetFocus"
        // (FRMCHAR3.BAS:299-301): the keyboard leaves txtSpell for the character window, whose
        // Form_KeyDown hands every key to fn1521 - so Alt+1..4 and the rest work again. The port
        // had left the focus in the box, where Objects & Spells keeps every key but Escape.
        Focus();
    }

    /// <summary>
    /// lblName_MouseDown puts the clicked spell's name in txtSpell (FRMCHAR3.BAS:635), and in
    /// txtSpell Enter is Cast and Escape hands the focus back to the main window
    /// (txtSpell_KeyDown, 851-861).
    /// </summary>
    private void WireSpellText()
    {
        SpellList.AddHandler(PointerPressedEvent, (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            int i = SpellList.SelectedIndex;
            // the "No Spells Yet" row takes no click: elaSpells is disabled while the list is
            // empty (FRMCHAR3.BAS:723-724)
            if (i >= 0 && i < _known.Count) SpellText.Text = _known[i].Spell.Name;
        }), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        // cmbClass_Click (FRMCHAR3.BAS:55): another class, another list
        ClassBox.SelectionChanged += (_, _) => { if (!SpellPage.Filling) RefreshSpells(); };
        // txtSpell_DragDrop (FRMCHAR3.BAS:845-849): a "SPELL" label dropped here puts its caption,
        // the spell's name (sub2424, FRMCHAR3.BAS:692), in the box; anything else is ignored
        Dragging.Target(SpellText, DragKind.Spell, p =>
        {
            if (p.Index >= 0 && p.Index < _spells.Count) SpellText.Text = _spells[p.Index].Name;
        });
        // txtSpell_KeyDown (FRMCHAR3.BAS:851-861): Enter casts, Escape hands the focus back - taken
        // on the way down, on the box itself, so nothing on the way can swallow it
        SpellText.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, k) =>
        {
            if (k.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return) { k.Handled = true; OnSpellCast(null, null!); }
            else if (k.Key == Avalonia.Input.Key.Escape) { k.Handled = true; Focus(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    // ---------------- the Companions page ----------------

    /// <summary>The Companions page, drawn to frmCharStat3's form data.</summary>
    private CompanionsPage _companionPage = null!;

    private void RefreshCompanions()
    {
        // mdrcharShowCompanions for the one on screen: " " into each empty slot's name (MAINMOD2.BAS:4745)
        _ch.Companions.BlankEmptyNames();
        CarryLine.Text = _ch.CarryText;
        // each slot's picture, name and hits, an empty one hidden and the numbers kept (MAINMOD2.BAS:4717-4749)
        _companionPage.Show(_ch.Companions, c => c.Def(_monsters));
    }

    /// <summary>The slot whose picture, name or button was used last (the control's Index), or -1.</summary>
    private int SelectedCompanion() =>
        _companionPage.Slot is int slot and >= 0 && _ch.Companions[slot] != null ? slot : -1;

    /// <summary>"You cannot kick out companions in the city!" - FRMCHAR3.BAS:260.</summary>
    private void OnCompanionKickOut(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        if (CompanionTurn.KickOutProblem(_ch, i, inDungeon: false) is string no && no != "") Log(no);
    }

    /// <summary>
    /// "Companion Name Label - Name a Companion", <c>lblCompName_Click</c> (FRMCHAR3.BAS:478-488):
    /// the name clicked on the page. There is no fight in town, so fn0EEF has nothing to refuse.
    /// </summary>
    private async void OnCompanionName(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        if (i < 0) return;   // nothing selected: silently, as the original
        // fn170D("Name " + fn128B(...), "New Name?", ...) - FRMCHAR3.BAS:477
        string? typed = await AskFor($"Name {_ch.CompanionName(i)}", "New Name?");
        if (typed == null) return;
        if (_ch.Companions[i] is Companion named) named.Name = typed.Trim();
        Save();
        RefreshCompanions();
    }

    private void OnCompanionInfo(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int i = SelectedCompanion();
        if (i < 0 || _ch.Companions[i] is not Companion c) return;   // nothing selected: silently, as the original
        var def = c.Def(_monsters);
        if (def == null) return;   // the page's own lookup finds nothing and it leaves (MAINMOD2.BAS:4934-4940)
        CompanionInfo.Sharpen(_ch, c, def, Lookup);
        // sub06FE l0292, False, True, False, Me (FRMCHAR3.BAS:757): the creature goes in the
        // Library at the ID level just sharpened. In town there is no place to file with it.
        Librarian.FileCompanion(SavesDir, _monsters, _ch, def, c.IdLevel);
        _charPanel.ShowMiscPage(CompanionInfo.Paint(c, i, def, _ch, Lookup), MonsterPortrait.Of(def), _options.NoStretch);
        _characterChild.SetNormal();   // mdrcharSetInfo (MAINMOD2.BAS:4598), then picTabPicture.SetFocus activates it
        _characterChild.Activate();
        Focus();   // frmCharStat1!picTabPicture.SetFocus (FRMCHAR3.BAS:759)
    }

    /// <summary>
    /// A page of the character window, from F2/F3/F4/F6/F7/F9 and the CTRL-letters. The mapping is
    /// the original's: see <see cref="CharacterPanel"/>.
    /// </summary>
    private void ShowCharacterTab(CharTab tab, bool keepFocus = false)
    {
        if (tab == CharTab.Look) _charPanel.ShowLook(AreaLook());
        _charPanel.Selected = tab;
        // fn15DF only sets CurrTab (MAINMOD2.BAS:4187-4222), and that raises the IndexTab's Click
        // (measured in the original, 2026-10-25), whose tabStatTab_event0000 sends tabs 4, 1, 2 and
        // 3 - Ctrl+L, R, C and G - through mdrcharSetInfo (FRMCHAR1.BAS:322-335); Ctrl+S, B and M
        // (only from a service's box, ServiceFieldKey, here) go no further
        if (tab is not (CharTab.Look or CharTab.Resist or CharTab.Char or CharTab.Guild)) return;
        // restored if minimised - then the active, topmost child - or maximised; a window at its
        // normal size is not raised, as nothing sets the focus after
        var wasActive = Host.Active;
        _characterChild.SetNormal();
        // a service's box keeps the focus unless the restore made the character window the active one
        if (keepFocus && ReferenceEquals(Host.Active, wasActive)) return;
        Focus();
    }

    /// <summary>
    /// F2, F4, F6 (Shift-F6 too) and F7: mdrcharSetInfo (MAINMOD2.BAS:3534, 3544, 3552-3555, 3559),
    /// whose first line brings the window back from a minimise or a maximise (4598); nothing raises
    /// one already at its normal size. As the dungeon's.
    /// </summary>
    private void SetInfoTab(CharTab tab)
    {
        if (tab == CharTab.Look) _charPanel.ShowLook(AreaLook());
        _charPanel.Selected = tab;
        _characterChild.SetNormal();
        Focus();
    }

    /// <summary>
    /// F3 and F9 (MAINMOD2.BAS:3537-3538, 3566-3567) and Ctrl+S, Ctrl+B and Ctrl+M (fn15DF,
    /// 4193-4201) set the tab and nothing more: the window is neither restored nor raised. As the
    /// dungeon's.
    /// </summary>
    private void SetTabOnly(CharTab tab)
    {
        _charPanel.Selected = tab;
        if (!_characterChild.IsVisible) _characterChild.Reopen();
        Focus();
    }

    /// <summary>CTRL-i, CTRL-p and CTRL-o: the three pages, reachable anywhere in the game.</summary>
    private void ShowSheetTab(int tab, bool keepFocus = false)
    {
        SheetTabs.SelectedIndex = tab;
        if (keepFocus) return;   // fn15DF only sets the tab, as above
        _sheetChild.Minimised = false;
        _sheetChild.Reopen();
        Focus();
    }
}
