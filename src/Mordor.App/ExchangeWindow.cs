using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// frmExchange (FRMEXCHA.BAS), which the main menu's Exchange Items opens modally over the loaded
/// character, <c>Chars(0)</c>, and the one sub2D04 1 has just loaded beside it, <c>Chars(1)</c>.
///
/// Laid out from its form in MORDOR.EXE: each character's name in quotes on a panel at the top,
/// under it a sunken panel holding "Gold: N" and the forty pack slots, the instructions bottom left
/// and Save Exchange over Cancel bottom right. The item panels are numbered 0-39 for the first
/// character and 40-79 for the second, and that numbering is what travels in a drag here too.
///
/// An item dropped on an item swaps the two slots (pnlItem_DragDrop, FRMEXCHA.BAS:156-214, which
/// is <see cref="Party.SwapItems"/>); its refusals are boxes headed "Sorry!". Gold is dragged from
/// one gold panel onto the other and asked for with an InputBox. Save Exchange writes both
/// characters and closes; Cancel only closes, leaving whatever was swapped in memory
/// (cmdCommand_Click, FRMEXCHA.BAS:21-34).
/// </summary>
public sealed class ExchangeWindow
{
    private readonly Window _win;
    private readonly Character[] _chars;
    private readonly Func<short, Item?> _lookup;
    private readonly TextBlock[] _gold = new TextBlock[2];
    private readonly TextBlock[] _rows = new TextBlock[Character.PackSize * 2];

    /// <summary>m0038: the panel last clicked on each side, whose text is drawn in QBColor(9).</summary>
    // Both start at 0, not -1, so the first pick on the right side repaints the left side's
    // first panel black - which it already is.
    private readonly int[] _picked = { 0, 0 };
    private readonly bool[] _blue = new bool[Character.PackSize * 2];

    public ExchangeWindow(Character first, Character second, Func<short, Item?> lookup, Action<Character> save)
    {
        _chars = new[] { first, second };
        _lookup = lookup;
        // laid out larger for a larger Options font, as a service's form is (FormPage); one each
        // way with the defaults
        var grow = UiFonts.FormGrowth(DesignFonts.Exchange.Pass);

        // The client: the form's controls and, past the right-hand panel and below Cancel, the
        // 60-twip margin the form keeps at their left (pnlName(0) and pnlPanel at 60): 6345x5475
        // twips. The form's window is 6360x5475 twips outside, frame and caption included, so its
        // client is smaller, and pnlName(1) and pnlPanel(1) (3195 + 3090 = 6285) and Cancel (5070 +
        // 345 = 5415) reach its edges or past them; the port's 416x361 (6240x5415) had cut the
        // panel's right edge and put Cancel against the window's bottom (a deviation, docs/FIDELITY.md).
        const int Margin = 60;
        double clientW = (Math.Max(3195 + 3090, 4530 + 1740) + Margin) / 15.0 * grow.X, clientH = (5070 + 345 + Margin) / 15.0 * grow.Y;
        var form = new Canvas { Width = clientW, Height = clientH };
        for (int side = 0; side < 2; side++)
        {
            int dx = side == 0 ? 0 : 3135;
            // pnlName: Chr$(34) + RTrim(Name) + Chr$(34) (FRMEXCHA.BAS:53-54), 9.75 bold. Every font
            // here is the form's through the Options fonts (UiFonts): Form_Load runs sub0784
            // (FRMEXCHA.BAS:44) and then sub07ED (FRMEXCHA.BAS:74), which sizes even pnlName at NonProSize
            form.Children.Add(Raised(UiFonts.Set(VbForm.Label("\"" + _chars[side].Name.TrimEnd() + "\"", 13, Brushes.Black, bold: true,
                                                  align: TextAlignment.Center), DesignFonts.Exchange, "pnlName(0)")).At(60 + dx, 75, 3090, 360, grow));

            var inner = new Canvas();
            _gold[side] = UiFonts.Set(VbForm.Label("", 11, Brushes.Black, bold: true, align: TextAlignment.Center), DesignFonts.Exchange);
            var goldPanel = Raised(_gold[side]).At(105, 105, 2865, 360, grow);
            inner.Children.Add(goldPanel);

            var list = new StackPanel();
            for (int i = 0; i < Character.PackSize; i++)
            {
                int index = side * Character.PackSize + i;
                // pnlItem: 16 pixels, or the Options font's line and RowPad when that is taller
                var row = UiFonts.Set(new TextBlock { Height = UiFonts.RowHeight(DesignFonts.Exchange, 16), Background = Brushes.Transparent },
                                      DesignFonts.Exchange, "pnlItem(0)");
                _rows[index] = row;
                row.PointerPressed += (_, _) => Pick(index);
                Dragging.Source(row, () => { Pick(index); return DragPayload.Item(index); });
                Dragging.Target(row, DragKind.Item, dragged => ItemDropped(index, dragged.Index));
                list.Children.Add(row);
            }
            inner.Children.Add(new ScrollViewer
            {
                Content = list, Background = VbForm.Grey,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible,
            }.At(105, 495, 2865, 3090, grow));
            form.Children.Add(VbForm.Sunken(VbForm.Grey, inner).At(60 + dx, 420, 3090, 3705, grow));

            int me = side;
            Dragging.Source(goldPanel, () => DragPayload.Vault(me));
            GoldTarget(goldPanel, me);
        }

        // pnlPanel(2), written in three pieces by Form_Load (FRMEXCHA.BAS:47-50)
        form.Children.Add(Raised(UiFonts.Set(VbForm.Label(
            "To exchange Items, Drag and Drop an item label from one character to another."
            + " To exchange gold, Drag and Drop from the gold label to the other character's gold label and enter the amount."
            + " When exchange is complete, click on Save Exchange to save.", 11, Brushes.Black), DesignFonts.Exchange, DesignFonts.PlainSans)).At(60, 4170, 4400, 1250, grow));

        var saveExchange = UiFonts.Set(VbForm.Button("Save Exchange", 11, bold: true), DesignFonts.Exchange).At(4530, 4695, 1740, 345, grow);
        var cancel = UiFonts.Set(VbForm.Button("Cancel", 11, bold: true), DesignFonts.Exchange).At(4530, 5070, 1740, 345, grow);
        form.Children.Add(saveExchange);
        form.Children.Add(cancel);

        _win = VbForm.Dialog("Exchange Items", clientW, clientH, VbForm.Grey, form);
        // cmdCommand(0) saves both with fn04ED and then does what cmdCommand(1) does, Unload Me
        saveExchange.Click += (_, _) => { save(_chars[0]); save(_chars[1]); _win.Close(); };
        cancel.Click += (_, _) => _win.Close();

        for (int i = 0; i < _rows.Length; i++) ShowItem(i);
        ShowGold();
    }

    public System.Threading.Tasks.Task ShowDialog(Window owner) => OriginalBox.ShowOver(_win, owner);

    /// <summary>An SSPanel's raised 3D look.</summary>
    private static Border Raised(Control child) => new()
    {
        Background = VbForm.Grey, BorderThickness = new Thickness(1, 1, 2, 2),
        BorderBrush = Brush.Parse("#808080"), Padding = new Thickness(3, 1), Child = child, ClipToBounds = true,
    };

    /// <summary>
    /// mdrcharShowItem False, 1 or 2, slot (MAINMOD2.BAS:5370-5480): the equipped mark, the slot
    /// number and the item's name as the character knows it, bold for a charged item and italic for
    /// a cursed one once fully identified - the same line the Items page draws.
    /// </summary>
    private void ShowItem(int index)
    {
        var who = _chars[index / Character.PackSize];
        int i = index % Character.PackSize;
        var row = _rows[index];
        var slot = who.Pack[i];
        if (slot == null || (i > 0 && slot.ItemId == 0))
        {
            row.Text = $"  {i + 1}.";
            row.FontWeight = FontWeight.Normal; row.FontStyle = FontStyle.Normal;
        }
        else
        {
            var it = _lookup(slot.ItemId);
            string mark = slot.Equipped ? "*" : " ";
            row.Text = $"{mark} {i + 1}. {(it == null ? "#" + slot.ItemId : Loot.NameFor(it, slot.Known, slot.Alignment, slot.Charges))}";
            row.FontWeight = PackRow.Bold(it, slot) ? FontWeight.Bold : FontWeight.Normal;
            row.FontStyle = PackRow.Italic(it, slot) ? FontStyle.Italic : FontStyle.Normal;
        }
        row.Foreground = _blue[index] ? Brush.Parse("#0000FF") : Brushes.Black;
    }

    /// <summary>
    /// pnlItem_Click (FRMEXCHA.BAS:139-154): the panel last picked on that side goes back to
    /// QBColor(0) and this one turns QBColor(9).
    /// </summary>
    private void Pick(int index)
    {
        int side = index < Character.PackSize ? 0 : 1;
        int was = _picked[side];
        _picked[side] = index;
        if (was > -1) { _blue[was] = false; ShowItem(was); }
        _blue[index] = true;
        ShowItem(index);
    }

    /// <summary>sub2D61 (FRMEXCHA.BAS:78-90): on hand, plus the bank when they are in town.</summary>
    private void ShowGold()
    {
        for (int side = 0; side < 2; side++)
        {
            var c = _chars[side];
            VbCurrency shown = c.DungeonLevel == 0 ? c.Purse : c.GoldOnHand;
            _gold[side].Text = "Gold: " + CharStatFile.FormatNumber(shown);
        }
    }

    /// <summary>pnlItem_DragDrop (FRMEXCHA.BAS:156-214), a panel dropped on another.</summary>
    private async void ItemDropped(int onto, int from)
    {
        if (onto == from) return;   // "Index <> Source.Index"
        var to = _chars[onto < Character.PackSize ? 0 : 1];
        var source = _chars[from < Character.PackSize ? 0 : 1];
        string? no = Party.SwapItems(source, from % Character.PackSize, to, onto % Character.PackSize, _lookup);
        if (!string.IsNullOrEmpty(no)) { await OriginalBox.Tell(_win, "Sorry!", no); return; }   // MsgBox 48
        if (no == "") return;   // either end is slot 1, the Hands: nothing happens and nothing is said
        ShowItem(from);
        ShowItem(onto);
        // the original redraws both slot numbers on both sides, which comes to the same four rows
        ShowItem((from + Character.PackSize) % (Character.PackSize * 2));
        ShowItem((onto + Character.PackSize) % (Character.PackSize * 2));
        Pick(onto);
    }

    /// <summary>
    /// pnlGold_DragDrop (FRMEXCHA.BAS:92-131). Anything dropped on a gold panel whose Index is not
    /// the panel's own starts a transfer from the other side - an item panel included, since the
    /// handler never looks at what was dropped. Dragging item 3 of the left pack onto the left gold
    /// asks how much gold to take from the right.
    /// </summary>
    private void GoldTarget(Control panel, int index)
    {
        DragDrop.SetAllowDrop(panel, true);
        panel.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragPayload.From(e) is { Kind: DragKind.Item or DragKind.Vault } ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        });
        panel.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            e.Handled = true;
            if (DragPayload.From(e) is not { Kind: DragKind.Item or DragKind.Vault } p) return;
            if (p.Index == index) return;   // "If Source.Index = Index Then Exit Sub"
            var to = _chars[index];
            var from = _chars[1 - index];
            string typed = await InputBox("How Much Gold?", "Transfer Gold", "0");
            if (typed.Length == 0) return;
            // CLng: a number that does not convert, or does not fit a Long, is an error, and the
            // handler's On Error ends it without a word
            if (!VbConvert.TryCLng(typed, out int clng)) return;
            long amount = clng;
            if (amount == 0) return;
            if (Party.TransferGold(from, to, amount) is string no) { await OriginalBox.Tell(_win, "Transfer Gold", no); return; }   // MsgBox 48
            ShowGold();
        });
    }

    /// <summary>VB's InputBox: a prompt, a line holding the default, OK and Cancel; Cancel is "".</summary>
    private async System.Threading.Tasks.Task<string> InputBox(string prompt, string title, string start)
    {
        string answer = "";
        var box = new TextBox { Text = start, Width = 300 };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
        var win = new Window
        {
            Title = title, SizeToContent = SizeToContent.WidthAndHeight, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false,
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light, FontFamily = VbForm.Sans,
            Content = new Grid
            {
                Margin = new Thickness(10), ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Children =
                {
                    new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 10, 20) },
                    new StackPanel { Spacing = 6, [Grid.ColumnProperty] = 1, Children = { ok, cancel } },
                    new Border { [Grid.RowProperty] = 1, [Grid.ColumnSpanProperty] = 2, Margin = new Thickness(0, 8, 0, 0), Child = box },
                },
            },
        };
        ok.Click += (_, _) => { answer = box.Text ?? ""; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
        await OriginalBox.ShowOver(win, _win);
        return answer;
    }
}
