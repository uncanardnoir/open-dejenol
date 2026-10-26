using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Styling;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The General Store, FRMCTYS, drawn to its form data (FormPage), as bm12.png shows it: three service
/// panels down the left (Uncurse Items, Combine Items, and Identify, Realign &amp; Sell Items) and
/// the Buy Items list on the right with its U G N E stock counts, a cost line, and a search box.
///
/// There is no pack list in this window. Every left-hand panel takes a slot number typed into its
/// Item box and Enter, or an item dropped on the box from the Items window - exactly as the
/// original's txtUncurse, txtCombine and txtID do (FRMCTYS.BAS:771-887, 944-974), each of which
/// reads the slot with <c>fn131A</c> (MAINMOD2.BAS:2523).
///
/// Everything the store says goes to the Messages window (<c>sub085B</c>), which in this port is
/// the city's message line; its refusals that are MsgBoxes in the original are boxes here.
/// </summary>
public partial class CityWindow
{
    /// <summary>
    /// A line of the Buy Items list: <c>elaItem</c> and <c>elaItemInfo</c> as vsbItems_Change fills
    /// them (FRMCTYS.BAS:988-1024).
    /// </summary>
    private sealed class ShelfLine
    {
        public Item Item { get; init; } = null!;
        /// <summary><c>" " + Name</c>, and <c>" [charges]"</c> when the item has any (1005-1009).</summary>
        public string Caption { get; init; } = "";
        /// <summary>The fixed price, or the four counts each capped at nine (991-1003).</summary>
        public string Info { get; init; } = "";
        /// <summary>fn11B6: bold when your character can use it (1017-1023).</summary>
        public bool Usable { get; init; }
    }

    /// <summary>
    /// VB's <c>Val</c> as fn131A uses it: leading blanks skipped, blanks between digits ignored,
    /// and it stops at the first thing that is not part of a number - so "7. Potion" is 7.
    /// </summary>
    private static double VbVal(string s)
    {
        int i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        bool neg = false;
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) { neg = s[i] == '-'; i++; }
        double v = 0, scale = 0;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c == ' ' || c == '\t') continue;
            if (c == '.' && scale == 0) { scale = 1; continue; }
            if (c < '0' || c > '9') break;
            if (scale == 0) v = v * 10 + (c - '0');
            else { scale /= 10; v += (c - '0') * scale; }
        }
        return neg ? -v : v;
    }

    private void ShowStore(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // the dead are never shown the town (sub14E0, MAINMOD2.BAS:3166-3180): back to the dead screen
        if (DeadScreenOwed) { ShowDead(); return; }
        // a picture clicked for one already up brings it forward and builds nothing again (imgPicture_Click, FRMCTYM.BAS:192-203)
        if (s != null && BringUpOpenService(GameWindow.GeneralStore)) return;
        OpenService("General Store", GameWindow.GeneralStore);
        var stock = StoreStock.Load(SavesDir, _items);

        // frmCityStore's form data (MORDOR.EXE RCDATA 16): a 7170x4380-twip client, elaElastic(0) at
        // -15,-15 holding the four panels - elaElastic(1) Uncurse, (2) Combine, (3) Identify, (4)
        // Buy - under their navy captions, and EXIT (ServiceForms.Store has the rectangles). The
        // fields' labels are right-justified in the form's 8.25 bold.
        var form = new FormPage(ServiceForms.Store.ClientW, ServiceForms.Store.ClientH, ServiceForms.Store.Elastic);
        var root = form.Root;
        // this page's font pass, kept for what is drawn after the build: the counts cells the
        // grid makes as rows come into view, and the headings redrawn when a mod's sort changes
        var pass = form.Fonts;
        FormPage.Label(root, "Uncurse Items", ServiceForms.Store.UncurseCaption, FormPage.Navy);
        FormPage.Label(root, "Combine Items", ServiceForms.Store.CombineCaption, FormPage.Navy);
        FormPage.Label(root, "Identify, Realign & Sell Items", ServiceForms.Store.IdCaption, FormPage.Navy);
        FormPage.Label(root, "Buy Items", ServiceForms.Store.BuyCaption, FormPage.Navy);
        var uncurseBox = FormPage.Elastic(root, ServiceForms.Store.UncurseBox);
        FormPage.Label(uncurseBox, "Item", ServiceForms.Store.UncurseItem, align: 1);
        FormPage.Label(uncurseBox, "Cost", ServiceForms.Store.UncurseCost, align: 1);
        var combineBox = FormPage.Elastic(root, ServiceForms.Store.CombineBox);
        FormPage.Label(combineBox, "Item", ServiceForms.Store.CombineItem, align: 1);
        FormPage.Label(combineBox, "Items", ServiceForms.Store.CombineItems, align: 1);
        var idBox = FormPage.Elastic(root, ServiceForms.Store.IdBox);
        FormPage.Label(idBox, "Item", ServiceForms.Store.IdItem, align: 1);
        FormPage.Label(idBox, "ID Cost", ServiceForms.Store.IdCostCaption, align: 1);
        var buyBox = FormPage.Elastic(root, ServiceForms.Store.BuyBox);
        FormPage.Label(buyBox, "Item", ServiceForms.Store.SelectedItemCaption, align: 1);
        FormPage.Label(buyBox, "Search for What?", ServiceForms.Store.SearchCaption);
        var exitBtn = FormPage.Button(root, "EXIT", ServiceForms.Store.cmdExit, bold: false);
        FormPage.Access(exitBtn, 'X');   // "E&XIT": Alt+X while this form is the active one (ServiceAccessKey)
        exitBtn.Click += (_, _) => CloseService(null, null!);

        // fn131A (MAINMOD2.BAS:2523-2551): an empty box is 0, which is the Hands; a number from 1
        // to 40 whose slot holds something (or is slot 1) is that slot, less one; anything else -1.
        int TypedSlot(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            double v = VbVal(text);
            if (v > short.MaxValue || v < short.MinValue) return -1;     // the overflow lands in its error trap
            int n = (int)Math.Round(v, MidpointRounding.ToEven);           // l03D8 As Integer
            if (n < 1 || n > Character.PackSize) return -1;
            var sl = _ch.Pack[n - 1];
            if ((sl != null && sl.ItemId != 0) || n == 1)
                if (Lookup(sl?.ItemId ?? Character.HandsItemId) is Item named && named.Name != "") return n - 1;
            return -1;
        }

        (InventorySlot? slot, Item? it) At(int i)
        {
            if (i < 0 || i >= Character.PackSize || _ch.Pack[i] is not InventorySlot sl) return (null, null);
            return (sl, Lookup(sl.ItemId));
        }

        // frmCharStat3!lblItem(n).Caption, as mdrcharShowItem writes it (MAINMOD2.BAS:5428-5436):
        // the slot number, ". ", and the item's name at what you know of it - only for a slot that
        // holds something, or for slot 0, the Hands (MAINMOD2.BAS:5435 "Or pv079C = 0")
        string Caption(int i)
        {
            var (sl, it) = At(i);
            bool named = (i > 0 && sl is { ItemId: > 0 }) || i == 0;
            return $"{i + 1}. " + (!named || sl == null || it == null ? "" : Loot.NameFor(it, sl.Known, sl.Alignment, sl.Charges));
        }

        // "You may only drag and drop items on your character to the store." - sub085B, so a
        // refusal goes to the Messages window like every other line the game says
        void NotAnItem() => Log("You may only drag and drop items on your character to the store.");

        // ---------------- Uncurse Items ----------------
        var txtUncurse = FormPage.Text(uncurseBox, ServiceForms.Store.txtUncurse);
        var uncurseCost = FormPage.Label(uncurseBox, "", ServiceForms.Store.lblUncurse, bold: false, boxed: true);
        var uncurseBtn = FormPage.Access(FormPage.Button(uncurseBox, "Uncurse", ServiceForms.Store.cmdUncurse, bold: false), 'c');   // "Un&curse"
        uncurseBtn.IsEnabled = false;

        // txtUncurse_KeyPress (FRMCTYS.BAS:960-974): any item at all gets a fee - whether it is
        // cursed is not asked until you press Uncurse. uncurseFee (767-769) is (.8# * Price) / .5#
        // returned As Long, so VB rounds it half to even rather than cutting it off.
        void UncurseEnter()
        {
            int i = TypedSlot(txtUncurse.Text);
            if (i > 0 && At(i).it is Item it)
            {
                uncurseCost.Text = $"{(long)Math.Round(0.8 * it.Price / 0.5, MidpointRounding.ToEven):N0}";
                txtUncurse.Text = Caption(i);
                uncurseBtn.IsEnabled = true;
            }
            else { uncurseCost.Text = ""; uncurseBtn.IsEnabled = false; }
        }
        // txtUncurse_Change (937-942): touching the box takes the offer away again
        txtUncurse.TextChanged += (_, _) =>
        {
            if (uncurseBtn.IsEnabled) { uncurseBtn.IsEnabled = false; uncurseCost.Text = ""; }
        };

        // ---------------- Combine Items ----------------
        //
        // txtCombine_KeyPress (FRMCTYS.BAS:788-809) is an accumulator, not a picker. Each item you
        // name - by typing it and pressing Enter, or by dropping it on the box - has its slot
        // number appended to lblCombine, comma separated, and the box clears ready for the next.
        // The first one named is the one everything else is poured into; Combine lights up as soon
        // as there is one.
        var txtCombine = FormPage.Text(combineBox, ServiceForms.Store.txtCombine);
        var combineCount = FormPage.Label(combineBox, "", ServiceForms.Store.lblCombine, bold: false, boxed: true);
        var combineBtn = FormPage.Access(FormPage.Button(combineBox, "Combine", ServiceForms.Store.cmdCombine, bold: false), 'C');   // "&Combine"
        combineBtn.IsEnabled = false;
        var combining = new List<int>();          // slot numbers, in the order they were named

        void ShowCombining()
        {
            combineCount.Text = string.Join(",", combining.Select(n => n + 1));
            combineBtn.IsEnabled = combining.Count > 0;      // lblCombine_Change (504-510)
        }

        void CombineEnter()
        {
            int i = TypedSlot(txtCombine.Text);
            if (i <= 0) return;                  // not a slot: the box keeps what you typed
            combining.Add(i);
            txtCombine.Text = "";
            ShowCombining();
        }

        void ForgetCombining() { combining.Clear(); ShowCombining(); }

        // ---------------- Identify, Realign & Sell Items ----------------
        var txtID = FormPage.Text(idBox, ServiceForms.Store.txtID);
        var valueNum = FormPage.Label(idBox, "Value", ServiceForms.Store.lblValueNum, align: 1);
        var idValue = FormPage.Label(idBox, "", ServiceForms.Store.lblValue, bold: false, boxed: true);
        var idCost = FormPage.Label(idBox, "", ServiceForms.Store.lblIDCost, bold: false, boxed: true);
        var idLevel = FormPage.Label(idBox, "", ServiceForms.Store.lblIDLevel, bold: false, align: 1);
        var infoBtn = FormPage.Access(FormPage.Button(idBox, "Info", ServiceForms.Store.cmdIDInfo, bold: false), 'n');   // "I&nfo"
        var sellBtn = FormPage.Access(FormPage.Button(idBox, "Sell", ServiceForms.Store.cmdSell, bold: false), 'S');   // "&Sell"
        var idBtn = FormPage.Access(FormPage.Button(idBox, "ID", ServiceForms.Store.cmdID, bold: false), 'I');   // "&ID"
        var realignBtns = new[]
        {
            FormPage.Button(idBox, "G", ServiceForms.Store.cmdRealign0, bold: false),
            FormPage.Button(idBox, "N", ServiceForms.Store.cmdRealign1, bold: false),
            FormPage.Button(idBox, "E", ServiceForms.Store.cmdRealign2, bold: false),
        };
        foreach (var b in new[] { infoBtn, sellBtn, idBtn }.Concat(realignBtns)) b.IsEnabled = false;

        // ---------------- Buy Items ----------------
        var buyInfoBtn = FormPage.Access(FormPage.Button(buyBox, "INFO", ServiceForms.Store.cmdInfo, bold: false), 'F');   // "IN&FO"
        var buyBtn = FormPage.Access(FormPage.Button(buyBox, "BUY", ServiceForms.Store.cmdBuy, bold: false), 'B');   // "&BUY"
        buyInfoBtn.IsEnabled = buyBtn.IsEnabled = false;

        // sub268D (557-566): the Identify panel emptied
        void ClearId()
        {
            txtID.Text = "";
            idValue.Text = "";
            valueNum.Text = "Value";
            idCost.Text = "";
            idLevel.Text = "";
            infoBtn.IsEnabled = false;
            sellBtn.IsEnabled = false;
            idBtn.IsEnabled = false;
        }

        // txtID_KeyPress (827-887). Every button on the panel runs this first, so the panel is
        // always showing what the box names.
        void IdEnter()
        {
            int i = TypedSlot(txtID.Text);
            foreach (var b in realignBtns) b.IsEnabled = false;
            if (i > 0 && At(i) is (InventorySlot slot, Item it))
            {
                if (ItemUse.IsGuildIssued(it))
                {
                    Log("Cannot sell Guild-Issued Items!");
                    ClearId();
                    return;
                }
                string value = $"{stock.SellPrice(it, slot):N0}";
                // a potion with more than a whole one's charges is bought a whole one at a time:
                // "(23->20)" (845-854)
                if (StoreStock.SoldByCharge(it) && slot.Charges > it.Charges && slot.Known == IdLevel.All)
                    value += $" ({slot.Charges}->{slot.Charges - it.Charges})";
                idValue.Text = value;
                txtID.Text = Caption(i);
                infoBtn.IsEnabled = true;
                sellBtn.IsEnabled = true;
                if (slot.Known != IdLevel.All)
                {
                    idBtn.IsEnabled = true;
                    idCost.Text = $"{stock.IdentifyFee(it, slot):N0}";
                    valueNum.Text = "? Value";
                }
                else
                {
                    // 863-871: Items.Align > 1 is "it comes in alignments"; the one it already has
                    // stays off. Which alignments its flags actually allow is not asked.
                    for (int k = 0; k < realignBtns.Length; k++)
                        realignBtns[k].IsEnabled = it.AlignmentFlags > 1 && (int)slot.Alignment != k + 1;
                    // 872: its alignment tag and the shelf count, uncapped - "(g)3 Value"
                    valueNum.Text = ItemInfoPage.AlignmentTags[(int)slot.Alignment] + stock.Count(it.Id, slot.Alignment) + " Value";
                    idBtn.IsEnabled = false;
                    idCost.Text = "";
                }
                idLevel.Text = Loot.IdLevelName(slot.Known);
            }
            else
            {
                // 878-884, word for word: it empties the *Uncurse* cost and disables the Buy panel's
                // INFO (cmdInfo, not cmdIDInfo), and leaves this panel's Info and the value caption
                // as they were. Kept.
                uncurseCost.Text = "";
                idBtn.IsEnabled = false;
                buyInfoBtn.IsEnabled = false;
                sellBtn.IsEnabled = false;
                idValue.Text = "";
                idCost.Text = "";
                idLevel.Text = "";
            }
        }

        // The list is one grid over the form's own: Label9's headers at the top, the nine elaItem and
        // elaItemInfo rows under them, each 210 twips (14 pixels), and vsbItems down the right - the
        // grid's own scroll bar (ServiceForms.Store.ListArea). A mod's columns widen it (StoreWidth).
        var listArea = ServiceForms.Store.ListArea;
        // elaItem is the form's MS Sans Serif 8.25 bold, elaItemInfo and Label9's headings Courier New
        // 8.25 bold (MORDOR.EXE, frmCityStore), each through the Options fonts (UiFonts)
        var nameFont = UiFonts.Design(DesignFonts.CityStore, "elaItem(0)");
        var countFont = DesignFonts.StoreCounts;
        var buyList = FormPage.Place(buyBox, UiFonts.Set(new DataGrid
        {
            IsReadOnly = true, AutoGenerateColumns = false, GridLinesVisibility = DataGridGridLinesVisibility.None,
            CanUserSortColumns = false, CanUserReorderColumns = false, CanUserResizeColumns = false,
            // mc0066: nine rows show at a time, with vsbItems beside them; the headers are Label9's
            // line, from its top to the first row's.
            // The form's rows are elaItem0's 14 pixels; the port's are the fonts' line and RowPad, 17
            // pixels with the default fonts, so the lines are not crowded and a larger Options font
            // makes them taller (deviation at the user's request, FIDELITY.md)
            RowHeight = UiFonts.RowHeight(pass, FormPage.Y(ServiceForms.Store.elaItem0.Height), nameFont, countFont),
            ColumnHeaderHeight = UiFonts.LabelHeight(pass, FormPage.Y(ServiceForms.Store.elaItem0.Top - ServiceForms.Store.ItemHead.Top), countFont),
            // elaItem is a row of fixed labels, and a label too narrow for its caption clips it: there
            // is no sideways scroll bar to cover the last row, and no ellipsis
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        }, pass, nameFont), listArea);
        // the default header padding is wider than the U G N E column; Label9 is 30 twips in from the rows.
        // A heading is a TextBlock in its own font (Head): the form's two in Label9's Courier New, a mod's
        // in the list's own face.
        buyList.Styles.Add(new Style(x => x.OfType<DataGridColumnHeader>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Avalonia.Thickness(2, 0)),
                new Setter(Layoutable.MinHeightProperty, 0d),
                new Setter(TemplatedControl.ForegroundProperty, Brushes.IndianRed),
                new Setter(TemplatedControl.FontWeightProperty, FontWeight.Bold),
                // so a header can be right-justified, as the U G N E label is
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            },
        });
        // the theme's rows and cells are at least 32 pixels high; elaItem is 14
        buyList.Styles.Add(new Style(x => x.OfType<DataGridCell>())
        {
            Setters = { new Setter(Layoutable.MinHeightProperty, 0d), new Setter(TemplatedControl.PaddingProperty, new Avalonia.Thickness(0)) },
        });
        buyList.Styles.Add(new Style(x => x.OfType<DataGridRow>()) { Setters = { new Setter(Layoutable.MinHeightProperty, 0d) } });
        // vsbItems is a plain scroll bar, always drawn, beside the counts. The theme's hides itself
        // to a hairline and, on a hover, widens back over the E column's numbers.
        buyList.Styles.Add(new Style(x => x.OfType<ScrollBar>()) { Setters = { new Setter(ScrollBar.AllowAutoHideProperty, false) } });
        // "Items that are displayed in bold are items that your character can currently use." The
        // others are simply not bold (CurrentY = False, 1017-1023). Deviation at the user's request
        // (FIDELITY.md): they are also greyed, since bold alone is hard to tell apart in this font.
        FontWeight Weight(ShelfLine? r) => r is { Usable: true } ? FontWeight.Bold : FontWeight.Normal;
        IBrush Ink(ShelfLine? r) => r is { Usable: true } ? Brushes.WhiteSmoke : Brushes.Gray;
        // A heading in its own font: Label9's Courier New for the form's two, the list's face for a mod's
        TextBlock Head(string text, DesignFont font) => UiFonts.Set(new TextBlock { Text = text, FontWeight = FontWeight.Bold }, pass, font);
        // a mod column's heading: in Label9's face too, set in as far as the column's cells (the
        // header's own padding is 2)
        TextBlock ModHead(string text)
        {
            var t = Head(text, countFont);
            t.Margin = new Avalonia.Thickness(ModSlots.StoreCellInset - 2, 0, 0, 0);
            return t;
        }
        // the theme's header keeps a 32-pixel column at its right for a sort arrow this list never shows
        // (CanUserSortColumns is off); the heading reaches into it so its right edge is the counts'
        const double SortArrowColumn = 32;
        // elaItemInfo's right edge is 75 twips short of vsbItems
        double countsRight = FormPage.X(ServiceForms.Store.vsbItems.Left - ServiceForms.Store.elaItemInfo0.Left - ServiceForms.Store.elaItemInfo0.Width);
        DataGridColumn itemCol;
        buyList.Columns.Add(itemCol = new DataGridTemplateColumn
        {
            Header = Head("Item", countFont),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<ShelfLine>((r, _) => new TextBlock
            {
                Text = r?.Caption ?? "", FontWeight = Weight(r), Foreground = Ink(r),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.None, ClipToBounds = true,
            }, supportsRecycling: false),
        });
        var ugne = Head("U G N E", countFont);
        ugne.TextAlignment = TextAlignment.Right;
        ugne.HorizontalAlignment = HorizontalAlignment.Stretch;
        ugne.Margin = new Avalonia.Thickness(0, 0, -SortArrowColumn - 2 + FormPage.X(ServiceForms.Store.vsbItems.Left - ServiceForms.Store.UgneHead.Left - ServiceForms.Store.UgneHead.Width), 0);
        buyList.Columns.Add(new DataGridTemplateColumn
        {
            // The form's "U G N E" label is Courier New, right-justified over the counts (MORDOR.EXE,
            // frmCityStore), and each count is joined on with a single space (FRMCTYS.BAS:1000), so a
            // letter sits over its number only when both are one fixed-pitch font at one size and share
            // a right edge. The header had been the theme's smaller header font, left-justified.
            Header = ugne,
            // from elaItem's right edge to vsbItems' left: elaItemInfo and the gaps round it, or as
            // wide as the fullest line of counts in a larger Options font
            Width = new DataGridLength(Math.Max(FormPage.X(ServiceForms.Store.vsbItems.Left - ServiceForms.Store.elaItem0.Left - ServiceForms.Store.elaItem0.Width),
                                                UiFonts.TextWidth("9 9 9 9", pass, countFont) + countsRight + 4)),
            CellTemplate = new FuncDataTemplate<ShelfLine>((r, _) => UiFonts.Set(new TextBlock
            {
                Text = r?.Info ?? "", FontWeight = Weight(r), Foreground = Ink(r),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, countsRight, 0),
            }, pass, countFont), supportsRecycling: false),
        });
        // the mods' columns (docs/MODDING.md, StoreColumns), between Item and the counts; with none,
        // the list is the original's two. Read with the row's own bold and grey, for the one in command.
        // Their cells are in the list's own face, elaItem's (the "NonPro" role); their headings are
        // on Label9's heading line, so in its Courier New (the "Pro" role) as "Item" and "U G N E"
        // are, and one line of headings is one face. A heading is set in as far as its cells' text.
        var modCols = ModSlots.AddStoreColumns(buyList, 1,
            o => o is ShelfLine sl ? (sl.Item, Ink(sl), Weight(sl)) : null,
            () => _ch, Lookup, ModHead);
        // vsbItems' own strip at the right (ListArea runs to its right edge). The theme's grid lays
        // its rows under the whole width and its scroll bar over their right end, so without this
        // the bar sat on the E column's numbers; the star Item column now stops where it should.
        double barWidth = FormPage.X(ServiceForms.Store.vsbItems.Width);
        buyList.Columns.Add(new DataGridTemplateColumn
        {
            Header = null, Width = new DataGridLength(barWidth),
            CellTemplate = new FuncDataTemplate<ShelfLine>((_, _) => new Panel(), supportsRecycling: true),
        });
        buyList.Styles.Add(new Style(x => x.OfType<ScrollBar>().PropertyEquals(ScrollBar.OrientationProperty, Orientation.Vertical)) { Setters = { new Setter(Layoutable.WidthProperty, barWidth) } });

        var search = FormPage.Text(buyBox, ServiceForms.Store.txtSearch);
        // elaSelectedItem and elaSelectedItemCost: Elastics used as fields, Arial 8.25
        var buyName = FormPage.Label(buyBox, "", ServiceForms.Store.elaSelectedItem, DesignFonts.StoreSelected, boxed: true);
        buyName.TextWrapping = TextWrapping.NoWrap;
        var costLabel = FormPage.Label(buyBox, "Cost", ServiceForms.Store.lblCost, align: 1);
        var buyCost = FormPage.Label(buyBox, "", ServiceForms.Store.elaSelectedItemCost, DesignFonts.StoreSelected, boxed: true);

        // elaAlignments, the hidden "Select Alignment" frame (Label11, maroon and centred) over the
        // list, and its four cmdAlign buttons
        var alignInside = FormPage.Elastic(buyBox, ServiceForms.Store.elaAlignments, FormPage.Face);
        var alignFrame = (Control)alignInside.Parent!;
        FormPage.Label(alignInside, "Select Alignment", ServiceForms.Store.SelectAlignment, FormPage.Maroon, align: 2);
        var alignBtns = new[]
        {
            FormPage.Button(alignInside, "U", ServiceForms.Store.cmdAlign0, bold: false),
            FormPage.Button(alignInside, "G", ServiceForms.Store.cmdAlign1, bold: false),
            FormPage.Button(alignInside, "N", ServiceForms.Store.cmdAlign2, bold: false),
            FormPage.Button(alignInside, "E", ServiceForms.Store.cmdAlign3, bold: false),
        };
        // the frame is drawn over the list, as the form has it, once it is shown
        alignFrame.ZIndex = 1;
        void ShowAlignFrame(bool shown)
        {
            alignFrame.IsVisible = shown;
            alignFrame.IsEnabled = shown;   // nor can a hidden button take the focus
        }
        ShowAlignFrame(false);

        List<ShelfLine> rows = new();
        HashSet<short> hidden = new();         // the shelved items a mod hid at the last drawing
        ShelfLine? lit = null;                // m0062, the highlighted row
        Item? selected = null;                 // elaSelectedItem.Tag
        int chosenAlign = 0;                   // m0028%
        ShelfLine? pending = null;
        bool choosing = false;                 // elaAlignments showing
        // A mod may make a header sortable (docs/MODDING.md, StoreHeaderSort and StoreRowOrder);
        // with none the headers are the original's plain captions and the list the shelf's order.
        // The headers that sort now, by the header control a click lands on, and the marks they
        // showed at the last drawing (null before the first), so a change of sort is told from a
        // buy or a sale
        var sortable = new Dictionary<object, (Mordor.Modding.LoadedMod Mod, string Name)>(ReferenceEqualityComparer.Instance);
        string? sortMarks = null;
        int modChanges = 0;                    // mod-setting changes heard, to tell whether a header click redrew the list

        // vsbItems.Value: the index of the list's top row, the first one at least half in view
        int TopRow()
        {
            var shown = buyList.GetVisualDescendants().OfType<DataGridRowsPresenter>().FirstOrDefault()?
                .Children.OfType<DataGridRow>().Where(r => r.IsVisible && r.Bounds.Bottom > r.Bounds.Height / 2)
                .Select(r => r.Index).Where(i => i >= 0).ToList();
            return shown is { Count: > 0 } ? shown.Min() : 0;
        }

        // The headers a mod sorts by: "Item", and each mod column's own header while it is shown. A
        // sortable one gets its mark (▲ first to last, ▼ last to first) and takes clicks; any other is
        // left the plain caption it was built with. Returns the marks, to tell a change of sort.
        string SortHeaders()
        {
            sortable.Clear();
            var host = Mordor.Modding.ModHost.Active;
            if (!host.Live.Any()) return "";
            var heads = new List<(DataGridColumn Col, string Name)> { (itemCol, "Item") };
            foreach (var (g, m, c) in modCols)
                if (host.IsShown(m, c)) heads.Add((g, c.Header));
            var marks = new System.Text.StringBuilder();
            foreach (var (col, name) in heads)
            {
                // Label9's Courier New over Item and over a mod's column alike, set in as its cells are
                TextBlock HeadOf(string text) => col == itemCol ? Head(text, countFont) : ModHead(text);
                if (host.StoreHeaderSort(name) is not { } s) { col.Header = HeadOf(name); continue; }
                string mark = s.Arrow switch
                {
                    Mordor.Modding.StoreSortArrow.Ascending => " ▲",
                    Mordor.Modding.StoreSortArrow.Descending => " ▼",
                    _ => "",
                };
                var head = HeadOf(name + mark);
                head.Cursor = new Cursor(StandardCursorType.Hand);
                col.Header = head;
                sortable[head] = (s.Mod, name);
                marks.Append(name).Append(':').Append(s.Arrow).Append(';');
            }
            return marks.ToString();
        }

        // sub260A (512-555) and vsbItems_Change (976-1026): every shelf entry with anything on it,
        // in item order - the shelf is indexed by item, and nothing sorts it.
        void RefreshShelf()
        {
            // sub260A 1, as cmdBuy_Click (FRMCTYS.BAS:91) and cmdSell_Click (285) call it:
            // vsbItems.Tag keeps the scroll bar's value and vsbItems_Change redraws the nine rows from
            // there (548-552, 976-1026), so the list stays where it was and the highlight stays on
            // m0024's row (1011-1016). A new ItemsSource puts a DataGrid back at its top, so the top
            // row is taken first and scrolled back to after - the last row first, so it lands at the
            // top, or the list goes as far down as it can (vsbItems.Max, 552) when rows have gone.
            int top = rows.Count > 0 ? TopRow() : 0;
            int topItem = top < rows.Count ? rows[top].Item.Id : -1;
            var shelved = _items.Where(i => i.Id != 0 && stock.AnyInStock(i.Id)).Select(i =>
            {
                var counts = stock.Counts(i.Id);
                return new ShelfLine
                {
                    Item = i,
                    Caption = " " + i.Name + (i.Charges > 0 ? $" [{i.Charges}]" : ""),
                    // floor 0 with unaligned stock is the store's own goods: the list price
                    Info = i.Floor == 0 && counts[0] > 0
                        ? $"{i.Price:N0}"
                        : string.Join(" ", counts.Select(n => Math.Min(n, StoreStock.ShelfCap))),
                    Usable = _ch.EquipProblem(i) == null,
                };
            }).ToList();
            // a mod may hide rows (docs/MODDING.md, StoreRowVisible); with none, every row is listed.
            // The search below looks through the rows listed, so a hidden item is "Item Not Found!"
            rows = shelved.Where(r => Mordor.Modding.ModHost.Active.StoreRowVisible(_ch, r.Item, r.Usable)).ToList();
            // then the order a mod asks for (StoreRowOrder), of the rows listed; with none, the
            // shelf's. The search below goes through the rows in this, the drawn, order
            string marks = SortHeaders();
            bool resorted = sortMarks != null && marks != sortMarks;
            sortMarks = marks;
            var order = Mordor.Modding.ModHost.Active.StoreRowOrder(_ch, rows.Select(r => r.Item).ToList(), Lookup);
            var lineOf = new Dictionary<Item, ShelfLine>(ReferenceEqualityComparer.Instance);
            foreach (var r in rows) lineOf[r.Item] = r;
            rows = order.Select(i => lineOf[i]).ToList();
            var listed = rows.Select(r => r.Item.Id).ToHashSet();
            var hiddenNow = shelved.Select(r => r.Item.Id).Where(id => !listed.Contains(id)).ToHashSet();
            // which rows a mod hides changed (a switch, or its setting): the list changed shape by
            // the mod, not by the shelf. With no mod nothing is ever hidden, and this is never so
            bool refiltered = !hiddenNow.SetEquals(hidden);
            hidden = hiddenNow;
            buyList.ItemsSource = rows;
            var wasLit = lit;
            lit = lit == null ? null : rows.FirstOrDefault(r => r.Item.Id == lit.Item.Id);
            buyList.SelectedItem = lit;
            if (wasLit != null && lit == null && hiddenNow.Contains(wasLit.Item.Id))
            {
                // the highlighted row is still on the shelf but a mod now hides it: its name, price
                // and BUY go as cmdBuy_Click's do when a row's stock runs out (FRMCTYS.BAS:92-98),
                // and a choice of its alignment still up goes with it
                if (choosing) CancelChoice();
                buyName.Text = "";
                selected = null;
                costLabel.Text = "Cost";
                buyCost.Text = "";
                buyBtn.IsEnabled = false;
                buyInfoBtn.IsEnabled = false;
            }
            // the top row is kept by its place in the list, as vsbItems.Tag keeps it (548-552) -
            // unless a mod's hiding changed the list's shape, when the place would now be some
            // other item: then by the item that was at the top, if it is still listed
            // A change of sort (a header clicked, or its toggle switched) reorders the whole list, so
            // neither the place nor the top item means anything: the highlighted row is brought to
            // the top, as the search brings what it finds, or the list starts at its top
            int keep = top;
            if (resorted) keep = lit == null ? 0 : rows.IndexOf(lit);
            else if (refiltered && topItem >= 0 && rows.FindIndex(r => r.Item.Id == topItem) is int at && at >= 0) keep = at;
            if (keep > 0 && rows.Count > 0)
            {
                buyList.UpdateLayout();
                buyList.ScrollIntoView(rows[^1], null);
                buyList.ScrollIntoView(rows[Math.Min(keep, rows.Count - 1)], null);
            }
        }

        // 375-385: the row's caption, then "N(x) Cost" with the count capped at nine, and the price
        // at that alignment after a space
        void ShowChosen(ShelfLine row, int a)
        {
            var align = ItemAlignments.All[a];
            buyName.Text = row.Caption;
            selected = row.Item;
            chosenAlign = a;
            int n = stock.Count(row.Item.Id, align);
            if (n == 0) return;
            costLabel.Text = $"{Math.Min(n, StoreStock.ShelfCap)}{ItemInfoPage.AlignmentTags[a]} Cost";
            buyCost.Text = $" {stock.PriceOf(row.Item, align):N0}";
        }

        // While Select Alignment is up the original beeps at any other row and greys the list's
        // scroll bar and EXIT until an alignment button is pressed (FRMCTYS.BAS:332-335, 358, 981).
        // That is the game's behaviour. A mod may answer StoreAllowsRowChangeWhileChoosing with
        // true (docs/MODDING.md; the Quality of Life mod does), and then a click on another row
        // withdraws the pending choice and takes the new row, and the list and EXIT stay usable.
        // The answer is taken when the frame comes up and holds until the choice is over.
        bool unlocked = false;

        // vsbItems.Enabled = False while Select Alignment is up, True again after (FRMCTYS.BAS:358, 365):
        // the DataGrid's own vertical bar is vsbItems here, so it is greyed with the rest
        void EnableScroll(bool on)
        {
            foreach (var bar in buyList.GetVisualDescendants().OfType<ScrollBar>())
                if (bar.Orientation == Orientation.Vertical) bar.IsEnabled = on;
        }
        // the form's EXIT, which elaItem_Click disables while you choose
        void Lock(bool on)
        {
            EnableScroll(!on);
            exitBtn.IsEnabled = !on;
        }

        void CancelChoice()
        {
            choosing = false;
            pending = null;
            ShowAlignFrame(false);
        }

        // elaItem_event0000 (326-392)
        void ClickRow(ShelfLine row)
        {
            bool wasChoosing = choosing;
            if (choosing)
            {
                if (!unlocked)
                {
                    // 332-335: Beep, and the highlight stays where it was
                    Audio.Beep();
                    buyList.SelectedItem = lit;
                    Mordor.Modding.ModHost.Active.StoreRowClicked(_ch, row.Item, whileChoosing: true, taken: false);
                    return;
                }
                CancelChoice();
            }
            Mordor.Modding.ModHost.Active.StoreRowClicked(_ch, row.Item, wasChoosing, taken: true);
            lit = row;
            buyList.SelectedItem = row;
            // 344-352: U, G, N and E enabled where the shelf has stock, m0028 left at the last of
            // them; the frame only comes up with more than one (AlignmentChoice). One in stock is
            // taken at once, and none (not listed, so not clicked) keeps the one before
            var offer = AlignmentChoice.For(ItemAlignments.All.Select(a => stock.Count(row.Item.Id, a)).ToList());
            for (int a = 0; a < 4; a++) alignBtns[a].IsEnabled = offer.Enabled[a];
            if (offer.Asks)
            {
                // the frame comes up and nothing else in the store answers until a button is
                // pressed - unless a mod unlocks the choice (above), when BUY and INFO still wait
                // for an alignment but the list and EXIT do not
                choosing = true;
                pending = row;
                unlocked = Mordor.Modding.ModHost.Active.StoreAllowsRowChangeWhileChoosing();
                ShowAlignFrame(true);
                if (unlocked)
                {
                    // a row taken earlier must not go on showing its name and price over this choice
                    buyName.Text = ""; buyCost.Text = ""; costLabel.Text = "Cost";
                }
                else Lock(true);
                buyBtn.IsEnabled = false;
                buyInfoBtn.IsEnabled = false;
                return;
            }
            buyBtn.IsEnabled = true;
            buyInfoBtn.IsEnabled = true;
            ShowChosen(row, offer.Chosen(chosenAlign));
        }

        // A click on a sortable header. While Select Alignment holds the list (the original's lock:
        // vsbItems_Change returns at once, FRMCTYS.BAS:981) it does nothing at all. Otherwise the
        // mod is told, and the list is drawn again - by the mod-setting change the click made, when
        // it made one (heard below), or here
        void SortClick(Mordor.Modding.LoadedMod mod, string name)
        {
            if (choosing && !unlocked) return;
            int before = modChanges;
            Mordor.Modding.ModHost.Active.StoreHeaderClicked(mod, name);
            if (modChanges == before) RefreshShelf();
            // a header click focuses the grid, and the new rows make row 0 its current cell, outlined:
            // the keys belong to the city window, as the original's labels never take focus
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus(), Avalonia.Threading.DispatcherPriority.Input);
        }

        // cmdAlign_Click (37-39), and the rest of elaItem_Click after its wait (365-385)
        for (int a = 0; a < alignBtns.Length; a++)
        {
            int which = a;
            alignBtns[a].Click += (_, _) =>
            {
                if (!choosing || pending == null) return;
                choosing = false;
                ShowAlignFrame(false);
                if (!unlocked) Lock(false);
                buyBtn.IsEnabled = true;
                buyInfoBtn.IsEnabled = true;
                ShowChosen(pending, which);
            };
        }

        // a click anywhere on a row - name or counts - is elaItem_Click (elaItemInfo_event0000, 394-396),
        // once per click. The DataGrid moves its selection on the press, before the release: the press
        // is marked here so that SelectionChanged leaves it to the release, or a click would run
        // ClickRow twice (the second time into the frame the first had just raised, and beep). The
        // original's labels answer only the left button; any other press is swallowed, so it cannot
        // move the highlight on its own.
        // Only the left button sets or clears the mark: another button let go in the middle of a
        // left press is not the end of that press. And a press that ends without a release reaching
        // the list - the pointer taken by something outside it, the window losing it - clears it
        // too, or every later change of row would be taken for a click in progress.
        bool pressing = false;
        int presses = 0;
        buyList.AddHandler(InputElement.PointerPressedEvent, (_, ev) =>
        {
            var props = ev.GetCurrentPoint(buyList).Properties;
            if (props.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) { ev.Handled = true; return; }
            presses++;
            pressing = true;
        }, RoutingStrategies.Tunnel);
        buyList.AddHandler(InputElement.PointerReleasedEvent, (_, ev) =>
        {
            if (ev.InitialPressMouseButton != MouseButton.Left) return;
            bool wasPressing = pressing;
            pressing = false;
            if (!wasPressing) return;
            // a sortable header (a mod's, see SortHeaders): to its mod, and the list drawn again
            if (sortable.Count > 0
                && (ev.Source as Avalonia.Visual)?.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is { } hdr)
            {
                if (hdr.Content is { } content && sortable.TryGetValue(content, out var owner)) SortClick(owner.Mod, owner.Name);
                return;
            }
            if ((ev.Source as Control)?.DataContext is ShelfLine row) ClickRow(row);
            else buyList.SelectedItem = lit;   // let go off the rows: the highlight stays where it was
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        // Raised on the list when the capture leaves it and whatever inside it held it. It is let
        // go of once the input in hand is done with, so that a release the capture is dropped for
        // still finds its press; a press that has begun since is left alone.
        buyList.PointerCaptureLost += (_, _) =>
        {
            int lost = presses;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (presses == lost) pressing = false; },
                                                         Avalonia.Threading.DispatcherPriority.Background);
        };
        // vsbItems.Enabled = False while the frame is up, so the wheel moves nothing either
        buyList.AddHandler(InputElement.PointerWheelChangedEvent, (_, ev) => { if (choosing && !unlocked) ev.Handled = true; }, RoutingStrategies.Tunnel);
        // The original's list is elaItem labels, which take no focus: every key goes to Form_KeyDown
        // and on to fn1521/fn15DF (FRMCTYS.BAS:407-424), so the arrows never touch the list. So it is
        // here, unless the mod decision that unlocks the alignment choice says otherwise (Quality of
        // Life, "Unlocked Store alignment choice"): then the list can be moved with the keys, and a
        // row reached that way is taken as if clicked, so the highlight and the BUY price never part.
        // The mod setting is live (it can be turned off with the Store open), so it is asked again
        // whenever a mod setting changes and at every change of row.
        bool KeysTakeRows() => Mordor.Modding.ModHost.Active.StoreAllowsRowChangeWhileChoosing();
        void ApplyKeysTakeRows()
        {
            bool on = KeysTakeRows();
            buyList.Focusable = on;
            buyList.IsTabStop = on;
            // turned off while the list had the keyboard: the keys go back to the form, as the
            // original's labels never had them
            if (!on && buyList.IsKeyboardFocusWithin) Focus();
        }
        ApplyKeysTakeRows();
        Action keysSettingChanged = () => Avalonia.Threading.Dispatcher.UIThread.Post(ApplyKeysTakeRows);
        Mordor.Modding.ModHost.Active.Changed += keysSettingChanged;
        buyList.DetachedFromVisualTree += (_, _) => Mordor.Modding.ModHost.Active.Changed -= keysSettingChanged;
        buyList.SelectionChanged += (_, _) =>
        {
            if (pressing) return;   // a click: the release takes it
            if (buyList.SelectedItem is ShelfLine picked && !ReferenceEquals(picked, lit))
            {
                if (KeysTakeRows()) ClickRow(picked);
                else buyList.SelectedItem = lit;
            }
        };

        void RefreshAll() { RefreshShelf(); RefreshSummary(); Save(); stock.Save(SavesDir); }

        // "You can also swap items and move items to the Store, Bank and the Buffer Window by
        // using Drag and Drop" (help, Items). The three DragDrop handlers (txtCombine at
        // FRMCTYS.BAS:771, txtID at 811, txtUncurse at 944) are all the same three lines: put the
        // dragged label's caption in the box and run what pressing Enter there would run.
        Dragging.Target(txtUncurse, DragKind.Item, p => { txtUncurse.Text = Caption(p.Index); UncurseEnter(); }, NotAnItem);
        Dragging.Target(txtCombine, DragKind.Item, p => { txtCombine.Text = Caption(p.Index); CombineEnter(); }, NotAnItem);
        Dragging.Target(txtID, DragKind.Item, p => { txtID.Text = Caption(p.Index); IdEnter(); }, NotAnItem);

        void OnEnter(TextBox box, Action act) => box.KeyDown += (_, k) =>
        {
            if (k.Key != Key.Enter && k.Key != Key.Return) return;
            k.Handled = true;
            act();
        };
        OnEnter(txtUncurse, UncurseEnter);
        OnEnter(txtCombine, CombineEnter);
        OnEnter(txtID, IdEnter);

        // txtSearch_KeyPress (895-935): a prefix of an item's name, any case; an exact name wins
        // outright. It never narrows the list - it only scrolls to the one it finds. It walks the
        // rows as listed (m002E, the list's own index of the shelf), so with a mod's sort it walks
        // them in the order drawn and the place it scrolls to is the drawn one. Which item it finds
        // does not depend on the order (one exact name, or a single prefix match), but for two
        // listed under the very same name, where the first drawn wins
        OnEnter(search, async () =>
        {
            string typed = search.Text ?? "";
            int found = 0, at = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                string name = rows[i].Item.Name;
                if (name.Length < typed.Length || !name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(name, typed, StringComparison.OrdinalIgnoreCase)) { at = i; found = 1; break; }
                found++;
                at = i;
            }
            if (found != 1)
            {
                // sub07E2 .5#: the answer sits in the box for half a second, then your text comes back
                search.Text = found == 0 ? "Item Not Found!" : "Item is ambiguous!";
                await System.Threading.Tasks.Task.Delay(500);
                search.Text = typed;
                return;
            }
            // vsbItems.Value = its position: it becomes the top row, or the list goes as far as it can.
            // Going to the end first and then back up to it leaves it at the top.
            // vsbItems_Change returns at once while Select Alignment is up (981): the value moves, the
            // list does not (unless a mod has unlocked the choice - see ClickRow)
            if (!choosing || unlocked)
            {
                buyList.ScrollIntoView(rows[^1], null);
                buyList.ScrollIntoView(rows[at], null);
            }
            search.Text = "";
        });

        // Charge for a service, falling back to the bank as the help says every city service does.
        // charPay, and on failure the original's box: MsgBox "Not enough gold!", titled by the
        // service (FRMCTYS.BAS:141, 211, 307). "X costs N gold; you have M ..." was this port's.
        bool Charge(long cost, string title)
        {
            if (CharPay(cost)) return true;
            _ = Tell(title, "Not enough gold!");
            return false;
        }

        // ---------------- actions ----------------
        uncurseBtn.Click += (_, _) =>
        {
            var (slot, it) = At(TypedSlot(txtUncurse.Text));
            if (slot == null || it == null) return;
            // cmdUncurse_Click:297-305 asks three things in this order, and the first two read
            // different fields: the *slot's* curse flag, then the slot's equipped bit. This is the
            // only place in the game that tests the slot flag without also testing equipped, and
            // that is what makes both odd answers below possible.
            //
            //   bought and never worn -> flag clear            -> "Item is not cursed!", a lie
            //   found in a chest and refused by the equip
            //     guards (too weak, wrong guild, wrong
            //     alignment) -> flag set, not equipped         -> "Item isn't equipped!"
            //
            // The second was recorded here as unreachable; it is not. sub0F01 copies the item's
            // curse onto the slot before it tries to put the thing on (MONSMOD.BAS:3830), and the
            // attempt goes through sub0F74, which can refuse.
            if (Uncursing.Refusal(slot) is string no)
            {
                if (no == Uncursing.NotEquipped) Audio.Beep();      // FRMCTYS.BAS:302
                Log(no);
                return;
            }
            long fee = (long)Math.Round(0.8 * it.Price / 0.5, MidpointRounding.ToEven);
            if (!Charge(fee, "UNCURSE ITEM")) return;

            // it comes off, and the clerk names it exactly while he is about it
            slot.Equipped = false;
            slot.Known = IdLevel.All;
            // FRMCTYS.BAS:310 forces the item off with sub0F74 and a negative slot index, and that
            // routine clears the slot curse on its way out (MAINMOD2.BAS:1749-1751) whatever the item is.
            // The test at FRMCTYS.BAS:314 then clears it again if the item definition is not
            // cursed, which can no longer be true of anything - it is dead but harmless. So an
            // alignment curse and an inherent one both come off here and both come back the moment
            // you put the item on again.
            slot.SlotCursed = false;
            Effects.Recompute(_ch, Lookup);
            // sub0730 files it in the Library at the level just set, fully known (FRMCTYS.BAS:312)
            Librarian.FileItem(SavesDir, _ch, it.Id, slot.Known);
            Log("Item has been separated from you.");   // FRMCTYS.BAS:319, word for word
            txtUncurse.Text = "";
            uncurseCost.Text = "";
            uncurseBtn.IsEnabled = false;
            RefreshAll();
        };

        combineBtn.Click += async (_, _) =>
        {
            // sub2621 pours everything in the list into the first item on it
            var chosen = combining.Select(n => _ch.Pack[n]).OfType<InventorySlot>().ToList();
            var numbers = new List<int>(combining);
            if (chosen.Count != numbers.Count) { ForgetCombining(); return; }
            if (chosen.Count == 0) return;

            if (Combining.Refusal(chosen, Lookup, numbers) is string no)
            {
                // one item on its own is not an error in the original - it simply does nothing;
                // the other two are MsgBoxes titled "SORRY!" (649, 665)
                if (no != "") _ = Tell("SORRY!", no);
                ForgetCombining();
                return;
            }
            // the STORE: lines are sub2621's own (FRMCTYS.BAS:676-691), each its own sub085B line
            var results = Combining.Combine(_ch, chosen, Lookup, numbers);
            foreach (var r in results) if (r.Line != "") Log(r.Line);
            if (results.Count > 0 && results[^1].Overflowed)
            {
                // run-time error 6 with no handler anywhere up the chain (FRMCTYS.BAS:677, 115): VB's
                // box, under the project's Title="Mordor 1.1" (MORDOR.MAK), then the program ends there
                // and then - no unload, so no save
                await Tell("Mordor 1.1", Combining.OverflowError);
                Environment.Exit(6);
            }
            ForgetCombining();                   // cmdCommand_Click (115-117)
            RefreshAll();
        };

        idBtn.Click += (_, _) =>
        {
            // cmdID_Click (128-150)
            IdEnter();
            if (!idBtn.IsEnabled) return;
            int i = TypedSlot(txtID.Text);
            var (slot, it) = At(i);
            if (i <= 0 || slot == null || it == null || slot.Known == IdLevel.All) return;
            if (!Charge(stock.IdentifyFee(it, slot), "Identify Item")) return;
            slot.Known = Rules.NextIdLevel(slot.Known);
            // sub0730 files it at the level it has now (FRMCTYS.BAS:145): graded 4 when that is
            // complete, 3 at Mostly, and 0 - a record with a finder but no grade - below that
            Librarian.FileItem(SavesDir, _ch, it.Id, slot.Known);
            // it says nothing: the item's name in the list is what changes. "It is a ... completely
            // identified." and "You learn a little more: ..." were this port's.
            RefreshAll();
            IdEnter();
        };

        // cmdRealign_Click (FRMCTYS.BAS:190-219): equipped is refused in a box; otherwise the cost is
        // put in a Yes/No box, and a Yes pays and changes it without a word. The button for the
        // alignment it already has is off (txtID_KeyPress:865), but the click itself does not
        // check - it reads whatever the box says now, as the original does.
        async void Realign(ItemAlignment want)
        {
            var (slot, it) = At(TypedSlot(txtID.Text));
            if (slot == null || it == null) return;
            if (slot.Equipped) { await Tell("SORRY!", "Cannot realign Equipped item!"); return; }
            long fee = StoreStock.RealignCost(it, slot.Alignment, want);
            if (!await Confirm("REALIGN ITEM", $"Cost to realign is {fee:N0}.\nDo you wish to pay?")) return;
            if (!CharPay(fee)) { await Tell("SORRY!", "Not enough gold!"); return; }
            slot.Alignment = want;
            RefreshAll();
            IdEnter();
        }
        realignBtns[0].Click += (_, _) => Realign(ItemAlignment.Good);
        realignBtns[1].Click += (_, _) => Realign(ItemAlignment.Neutral);
        realignBtns[2].Click += (_, _) => Realign(ItemAlignment.Evil);

        infoBtn.Click += (_, _) =>
        {
            // cmdIDInfo_Click (152-168): the copy in the pack, on the Misc page. It reads the box
            // without pressing Enter, and an empty box is fn131A's 0 - the Hands.
            var (slot, it) = At(TypedSlot(txtID.Text));
            if (slot == null || it == null) return;
            ShowItemInMisc(it, slot.Alignment, slot.Known);
        };

        buyInfoBtn.Click += (_, _) =>
        {
            // cmdInfo_Click (170-188): the alignment is read back out of the Cost caption - the
            // first of (u) (g) (n) (e) it contains - and the shelf's item is shown fully known
            int a = Array.FindIndex(ItemInfoPage.AlignmentTags, t => costLabel.Text?.Contains(t) == true);
            if (a < 0 || selected == null) return;
            ShowItemInMisc(selected, ItemAlignments.All[a], IdLevel.All);
        };

        sellBtn.Click += (_, _) =>
        {
            // cmdSell_Click (221-286)
            IdEnter();
            if (!sellBtn.IsEnabled) return;
            int i = TypedSlot(txtID.Text);
            var (slot, it) = At(i);
            if (slot == null || it == null) return;
            // 233 - equipped *and* cursed is the refusal; equipped and merely unwanted is taken off
            // for you and sold
            // Gold = Gold + sellPrice (240) has no handler: an Overflow ends the original program
            // before anything is saved, so the port refuses the sale and changes nothing
            // a mod may have the town put sale gold in a bank account, the seller's or another
            // member's (ModDecisions.TownIncomeBank); the original's hand otherwise, and the same
            // overflow refusal for whichever it goes to
            var bank = Mordor.Modding.ModHost.Active.TownIncomeBank(_party, _ch, stock.SellPrice(it, slot), Mordor.Modding.TownIncomeKind.StoreSale);
            var payee = bank ?? _ch;
            if (!payee.CanReceive(stock.SellPrice(it, slot), bank != null)) return;
            if (slot.Equipped && !_ch.CanUnequip(slot, it)) { _ = Tell("SELL ITEM", "Item is Equipped and Cursed!"); return; }
            if (slot.Equipped) { slot.Equipped = false; Effects.Recompute(_ch, Lookup); }
            long price = stock.SellPrice(it, slot);
            payee.Receive(price, bank != null);

            // potions, scrolls and the like are bought a whole item's worth of charges at a time.
            // Sell a Potion of Health [23] where a full one is 3 and you are left holding [20]; sell
            // a part-used one and the store cannot shelve it at all.
            bool shelve = true, takeAway = true;
            if (StoreStock.SoldByCharge(it))
            {
                if (slot.Charges > it.Charges)
                {
                    slot.Charges = (short)(slot.Charges - it.Charges);
                    takeAway = false;
                }
                else if (slot.Charges < it.Charges)
                {
                    shelve = false;
                }
            }
            if (takeAway) _ch.RemoveFromPack(slot);

            // "When you sell an item to the store, it will be added to the inventory list and
            // available for purchase by others." The permanent goods are always available anyway,
            // and a cursed item is never shelved (267 tests item.Cursed).
            if (shelve && stock.ShelvesSale(it, slot.Alignment))
            {
                stock.Add(it.Id, slot.Alignment);
                // a mod may say so (docs/MODDING.md, StoreShelved); with none the sale stays silent
                Mordor.Modding.ModHost.Active.StoreShelved(_ch, it, slot.Alignment, Log);
            }
            // nothing is filed in the Library: cmdSell_Click (221-286) has no sub0730

            // a sale is silent: "Sold the X for N gold.", "The shopkeeper squints at it ...", "You
            // still hold X [n]." and "Too little of it was left to put back on the shelf." were
            // this port's. 277-282: the panel empties, or keeps the slot when some is left.
            if (takeAway) ClearId(); else txtID.Text = $"{i + 1}";
            RefreshAll();
            IdEnter();
        };

        buyBtn.Click += (_, _) =>
        {
            // cmdBuy_Click (41-110)
            if (selected is not Item it) return;
            int a = chosenAlign;
            var align = ItemAlignments.All[a];
            // 53-63: a free slot is looked for before your purse is
            int free = _ch.FirstFreeSlot();
            if (free < 0) { _ = Tell("BUY ITEM", "No free slots!"); return; }
            long price = stock.PriceOf(it, align);
            if (!CharPay(price)) { _ = Tell("BUY ITEM", "Not enough gold!"); return; }
            // 77: the shelf only goes down for a dungeon item, or for an aligned copy of a floor-0
            // one - the permanent unaligned stock is bottomless
            if (StoreStock.DepletedByBuying(it, align)) stock.Take(it.Id, align);
            // nor on buying: cmdBuy_Click (41-110) has no sub0730 either
            // 70-76: fully identified, with the item's own charges
            var bought = new InventorySlot { ItemId = it.Id, Known = IdLevel.All, Alignment = align, Charges = it.Charges > 0 ? (short)it.Charges : (short)0 };
            _ch.PutInSlot(free, bought);
            RefreshAll();
            if (stock.Count(it.Id, align) < 1)
            {
                // 92-98
                buyName.Text = "";
                selected = null;
                costLabel.Text = "Cost";
                buyCost.Text = "";
                buyBtn.IsEnabled = false;
                buyInfoBtn.IsEnabled = false;
            }
            else
            {
                costLabel.Text = $"{Math.Min(stock.Count(it.Id, align), StoreStock.ShelfCap)}{ItemInfoPage.AlignmentTags[a]} Cost";
                buyCost.Text = $" {stock.PriceOf(it, align):N0}";
            }
            // 109: the pack index, Hands being 0, plus one - the number the Items window shows
            Log($"Slot #{free + 1} now contains your {it.Name}.");
        };

        RefreshShelf();

        // ---------------- layout ----------------
        // The controls are at their form rectangles already. On the heading's line, right of "Buy
        // Items", go the mods' check boxes (docs/MODDING.md, StoreOptions); with none, the heading
        // alone, as the original's.
        var page = form.Page;
        Border? optionsHost = null;
        var buyCaption = ServiceForms.Store.BuyCaption;
        if (ModSlots.StoreOptions(Brushes.LightGray, pass) is { } modOptions)
            optionsHost = FormPage.Place(root, new Border { Child = modOptions },
                new FormRect(buyCaption.Left + buyCaption.Width, buyCaption.Top - 30,
                             ServiceForms.Store.ClientW - 60 - buyCaption.Left - buyCaption.Width, buyCaption.Height));
        // A mod's columns widen the list by their own width, and with it the Buy Items panel, the
        // page and the window (PlaceService); EXIT and the check boxes keep their place from the
        // right. With none, nought: the form's own size.
        void StoreWidth(double extra)
        {
            page.Width = form.Client.Width + extra;
            if (buyBox.Parent is Control panel) panel.Width = form.PixelsX(ServiceForms.Store.BuyBox.Width) + extra;
            buyList.Width = form.PixelsX(listArea.Width) + extra;
            Canvas.SetLeft(exitBtn, form.PixelsX(ServiceForms.Store.cmdExit.Left) + extra);
            if (optionsHost != null)
                optionsHost.Width = form.PixelsX(ServiceForms.Store.ClientW - 60 - buyCaption.Left - buyCaption.Width) + extra;
        }
        // frmCityStore!lblCall = "UPDATE", lblCall_Change (FRMCTYS.BAS:479-502): the Identify panel,
        // the combine list and the Uncurse panel emptied and their buttons greyed, and the rows
        // redrawn (vsbItems_Change, which returns at once while the choice holds the list, 981).
        // The boxes hold slot numbers, so anything that moves the pack under them must send it.
        void Update()
        {
            ClearId();
            ForgetCombining();
            txtUncurse.Text = "";
            uncurseCost.Text = "";
            uncurseBtn.IsEnabled = false;
            if (!choosing || unlocked) RefreshShelf();
        }
        Action update = Update;
        _storeUpdate = update;
        // the mods' columns shown as their mods say, and the window as much wider as they are
        // (PlaceService, posted by OpenService, runs after this); with none, nought
        _serviceExtraWidth = ModSlots.ShowStoreColumns(modCols);
        StoreWidth(_serviceExtraWidth);
        // a mod setting changing may show or hide rows (StoreRowVisible): the list is drawn again,
        // unless the alignment choice holds it; and mod columns come and go, the window with them.
        // Only with a mod running; without, nothing changes
        if (Mordor.Modding.ModHost.Active.Live.Any())
        {
            Action again = () => { modChanges++; Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_storeUpdate != update) return;
                if (modCols.Count > 0)
                {
                    double extra = ModSlots.ShowStoreColumns(modCols);
                    StoreWidth(extra);
                    InServiceWindow(GameWindow.GeneralStore, () => ServiceExtraWidthChanged(extra));
                }
                if (!choosing || unlocked) RefreshShelf();
            }); };
            Mordor.Modding.ModHost.Active.Changed += again;
            page.DetachedFromVisualTree += (_, _) => Mordor.Modding.ModHost.Active.Changed -= again;
        }
        // an equip in town changes what a mod column may show (what is worn): the rows are drawn
        // again - not the Store's UPDATE, which the original does not send for an equip
        Action? modRedraw = modCols.Count > 0 ? () => { if (_storeUpdate == update && (!choosing || unlocked)) RefreshShelf(); } : null;
        _storeModRedraw = modRedraw;
        // leaving the store any other way while the frame is up must not leave EXIT dead
        page.DetachedFromVisualTree += (_, _) =>
        {
            exitBtn.IsEnabled = true;
            if (_storeUpdate == update) _storeUpdate = null;
            if (_storeModRedraw == modRedraw) _storeModRedraw = null;
        };
        ShowForm(form);
    }

    /// <summary><c>frmCityStore!lblCall = "UPDATE"</c> for the open General Store; null when it is shut.</summary>
    private Action? _storeUpdate;

    /// <summary>The open Store's rows drawn again for its mod columns (after an equip); null when shut or with none.</summary>
    private Action? _storeModRedraw;
}
