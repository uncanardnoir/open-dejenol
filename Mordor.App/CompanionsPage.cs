using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The Companions page of Objects &amp; Spells as frmCharStat3's form data lays it out
/// (<see cref="ServiceForms.Companions"/>, MORDOR.EXE RCDATA 10), the same in town and below: a
/// picture per slot, picCompanion(Index), with its name (lblCompName) and hits (lblCompHits)
/// beside it, and the four kick-out buttons cmdCompanion(0..3), "1" to "4", along the top right.
/// Every rectangle is the form's, in pixels at 15 twips each, on elaTabFrame(2) - the page's Canvas.
///
/// <c>mdrcharShowCompanions</c> (MAINMOD2.BAS:4709-4760) fills it: for each filled slot the
/// picture is repainted and shown, the name is "n. (Creature)" or "n. \"Own Name\"", the hits
/// "h\m" and the button enabled; an empty slot's picture and labels are hidden and its button
/// disabled; no companion at all puts "No Companions" in lblCompName(0).
///
/// <c>picCompanion_Paint</c> (FRMCHAR3.BAS:801-820) draws the creature's 60-pixel cell of the
/// monster sheet, column <c>(PicId - 1) Mod 10</c> and row <c>Int((PicId - 1) / 10)</c>: StretchBlt
/// to fill the 50-pixel box, or, with No StretchBlt (gv1A66.M5F04), BitBlt at its own size centred
/// - (50 / 2) - (60 / 2), five pixels up and left, so the cell's middle 50 pixels show. Both
/// SRCCOPY; the stretch is drawn with no smoothing (the DC's default stretch mode drops pixels).
///
/// A picture is dragged with a button down over it (picCompanion_MouseMove, 795-799) and takes a
/// companion dropped on it (picCompanion_DragDrop, 763-779, <see cref="CompanionDrag"/>); a
/// double-click is Info (picCompanion_DblClick, 744-759); a right press is Attack in the dungeon
/// (picCompanion_MouseDown, 783-793). A click on the name is Name (lblCompName_Click, 478-488).
/// </summary>
public sealed class CompanionsPage
{
    /// <summary>frmCharStat3's client, which this page's rectangles were read against.</summary>
    public static readonly Size Client = new(ServiceForms.Companions.ClientW / 15.0, ServiceForms.Companions.ClientH / 15.0);

    private static readonly FormRect[] PictureRects =
        { ServiceForms.Companions.Picture0, ServiceForms.Companions.Picture1, ServiceForms.Companions.Picture2, ServiceForms.Companions.Picture3 };
    private static readonly FormRect[] NameRects =
        { ServiceForms.Companions.Name0, ServiceForms.Companions.Name1, ServiceForms.Companions.Name2, ServiceForms.Companions.Name3 };
    private static readonly FormRect[] HitsRects =
        { ServiceForms.Companions.Hits0, ServiceForms.Companions.Hits1, ServiceForms.Companions.Hits2, ServiceForms.Companions.Hits3 };

    /// <summary>The sheet's cell, mc02E6 (FRMCHAR3.BAS:807).</summary>
    public const int Cell = MonsterPortrait.Cell;

    /// <summary>The slots: mc00E0 in mdrcharShowCompanions, four pictures on the form.</summary>
    public const int Slots = 4;

    private readonly TextBlock _carry;
    private readonly Func<bool> _noStretch;
    private readonly Border[] _pictures = new Border[Slots];
    private readonly Image[] _images = new Image[Slots];
    private readonly TextBlock[] _names = new TextBlock[Slots];
    private readonly TextBlock[] _hits = new TextBlock[Slots];
    private readonly Button[] _kick = new Button[Slots];
    private readonly bool[] _filled = new bool[Slots];

    /// <summary>The pictures, picCompanion(0..3).</summary>
    public IReadOnlyList<Border> Pictures => _pictures;
    /// <summary>The kick-out buttons, cmdCompanion(0..3).</summary>
    public IReadOnlyList<Button> KickButtons => _kick;
    /// <summary>The names, lblCompName(0..3).</summary>
    public IReadOnlyList<TextBlock> Names => _names;
    /// <summary>The hits, lblCompHits(0..3).</summary>
    public IReadOnlyList<TextBlock> Hits => _hits;
    /// <summary>The picture each box shows, or null.</summary>
    public IImage? PictureOf(int slot) => _images[slot].Source;

    /// <summary>The slot the last command on the page was given on (the control's Index), or -1.</summary>
    public int Slot { get; private set; } = -1;

    /// <param name="page">elaTabFrame(2), on which every control is placed</param>
    /// <param name="carry">lblCarryInfo, already on the page</param>
    /// <param name="noStretch">the Options window's No StretchBlt, read at each paint</param>
    /// <param name="name">lblCompName_Click</param>
    /// <param name="info">picCompanion_DblClick</param>
    /// <param name="kickOut">cmdCompanion_Click</param>
    /// <param name="attack">picCompanion_MouseDown's right button, in the dungeon; null in town, where it does nothing</param>
    public CompanionsPage(Canvas page, TextBlock carry, Func<bool> noStretch,
                          Action<int> name, Action<int> info, Action<int> kickOut, Action<int>? attack)
    {
        _carry = carry;
        _noStretch = noStretch;
        var ci = ServiceForms.Companions.CarryInfo;
        Canvas.SetLeft(carry, ci.Left / 15.0);
        Canvas.SetTop(carry, ci.Top / 15.0);
        carry.Width = ci.Width / 15.0;
        carry.Height = ci.Height / 15.0;
        var ela = ServiceForms.Companions.Elastic;
        double ex = ela.Left / 15.0, ey = ela.Top / 15.0;
        for (int i = 0; i < Slots; i++)
        {
            int slot = i;
            var img = new Image { Stretch = Stretch.Fill };
            // every picture in this game is a 1990s bitmap; smoothing it is not kindness
            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
            _images[i] = img;
            var inner = new Canvas { Children = { img } };
            var r = PictureRects[i];
            var box = new Border
            {
                Background = FormPage.Face, ClipToBounds = true, Child = inner, IsVisible = false,
                Width = r.Width / 15.0, Height = r.Height / 15.0, Tag = slot,
            };
            Canvas.SetLeft(box, ex + r.Left / 15.0);
            Canvas.SetTop(box, ey + r.Top / 15.0);
            box.SizeChanged += (_, _) => Lay(slot);
            box.DoubleTapped += (_, e) =>
            {
                if (!_filled[slot]) return;
                Slot = slot;
                e.Handled = true;
                info(slot);
            };
            if (attack != null)
                box.PointerPressed += (_, e) =>
                {
                    if (!_filled[slot] || !e.GetCurrentPoint(box).Properties.IsRightButtonPressed) return;
                    Slot = slot;
                    e.Handled = true;
                    attack(slot);
                };
            _pictures[i] = box;
            page.Children.Add(box);

            bool right = ServiceForms.Companions.RightHand(slot);
            var n = NameRects[i];
            var label = new TextBlock
            {
                Width = n.Width / 15.0, Height = n.Height / 15.0, IsVisible = false,
                TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
                TextTrimming = TextTrimming.None, TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.Khaki, Background = Brushes.Transparent,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            UiFonts.SetFont(label, "frmCharStat3.lblCompName(0)");
            Canvas.SetLeft(label, ex + n.Left / 15.0);
            Canvas.SetTop(label, ey + n.Top / 15.0);
            // Tapped: VB's Click comes when the button goes back up on the label
            label.Tapped += (_, e) =>
            {
                if (!_filled[slot]) return;   // "Chars(gv1A82).Companions(Index).Ref > -1" - not the "No Companions" caption
                Slot = slot;
                e.Handled = true;
                name(slot);
            };
            _names[i] = label;
            page.Children.Add(label);

            var h = HitsRects[i];
            var hits = new TextBlock
            {
                Width = h.Width / 15.0, Height = h.Height / 15.0, IsVisible = false, FontWeight = FontWeight.Bold,
                TextAlignment = right ? TextAlignment.Right : TextAlignment.Left, TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.LightGray,
            };
            Canvas.SetLeft(hits, ex + h.Left / 15.0);
            Canvas.SetTop(hits, ey + h.Top / 15.0);
            _hits[i] = hits;
            page.Children.Add(hits);
        }

        // cmdCompanion(3) where (0) was designed, and each of 2, 1, 0 a button's width left of the
        // next (Form_Load, FRMCHAR3.BAS:384-394), captioned "1" to "4" (395-397)
        var k = ServiceForms.Companions.Kick0;
        for (int i = 0; i < Slots; i++)
        {
            int slot = i;
            var b = new Button
            {
                Content = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Width = k.Width / 15.0, Height = k.Height / 15.0, Focusable = false, IsEnabled = false,
                // an 18x17 button: the theme's padding would leave no room for its digit
                Padding = new Thickness(0), MinWidth = 0, MinHeight = 0,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            };
            Canvas.SetLeft(b, (k.Left - (Slots - 1 - i) * k.Width) / 15.0);
            Canvas.SetTop(b, k.Top / 15.0);
            b.Click += (_, _) => { Slot = slot; kickOut(slot); };
            _kick[i] = b;
            page.Children.Add(b);   // after lblCarryInfo: a button stands over a label
        }
    }

    /// <summary>
    /// <c>mdrcharShowCompanions</c> for the character on the page (MAINMOD2.BAS:4709-4760). The
    /// caller blanks the empty slots' names and writes the carry line first, as before.
    /// </summary>
    public void Show(CompanionSlots slots, Func<Companion, Monster?> def)
    {
        int shown = 0;
        for (int i = 0; i < Slots; i++)
        {
            var c = slots[i];
            _filled[i] = c != null;
            if (c == null)
            {
                _pictures[i].IsVisible = _names[i].IsVisible = _hits[i].IsVisible = false;
                _kick[i].IsEnabled = false;
                _images[i].Source = null;
                continue;
            }
            shown++;
            var m = def(c);
            _names[i].Text = c.Name.Trim() == ""
                ? $"{i + 1}. ({m?.Name.Trim() ?? "?"})"
                : $"{i + 1}. \"{c.Name.Trim()}\"";
            // the hits separated by a backslash, which is the original's, not a slip (MAINMOD2.BAS:4735)
            _hits[i].Text = $"{c.Hits}\\{c.MaxHits}";
            _images[i].Source = m == null ? null : MonsterPortrait.Of(m);
            _pictures[i].IsVisible = _names[i].IsVisible = _hits[i].IsVisible = true;
            _kick[i].IsEnabled = true;
            Lay(i);
        }
        if (shown == 0)
        {
            _names[0].IsVisible = true;
            _names[0].Text = "No Companions";
        }
        if (Slot >= 0 && !_filled[Slot]) Slot = -1;
    }

    /// <summary>
    /// The picture in its box: filling it, or at the cell's own size centred with No StretchBlt
    /// (FRMCHAR3.BAS:815-818). In a larger Options font the page is laid out larger and zoomed back
    /// (<see cref="ObjectsForm.FitCompanionPage"/>), so the cell's own size is in that scale.
    /// </summary>
    private void Lay(int slot)
    {
        var box = _pictures[slot];
        var img = _images[slot];
        double w = double.IsNaN(box.Width) ? 0 : box.Width, h = double.IsNaN(box.Height) ? 0 : box.Height;
        if (_noStretch())
        {
            double scale = w / (PictureRects[slot].Width / 15.0);
            double size = Cell * scale;
            img.Width = img.Height = size;
            Canvas.SetLeft(img, w / 2 - size / 2);
            Canvas.SetTop(img, h / 2 - size / 2);
        }
        else
        {
            img.Width = w;
            img.Height = h;
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
        }
    }

    private Dictionary<Control, Point>? _base;
    private double _carryWidth;

    /// <summary>
    /// The window made larger than the form's client (<see cref="ObjectsForm.Reflow"/>): the original
    /// zooms the whole form; the port keeps every control at its size, in the Options fonts, and
    /// gives the room to the page: the right-hand slots (1 and 3) and the buttons move right by
    /// <paramref name="dw"/>, the lower slots (2 and 3) down by <paramref name="dh"/>, and the carry
    /// line widens. At the form's own size nothing moves.
    /// </summary>
    public void Grow(double dw, double dh)
    {
        if (_base == null)
        {
            _base = new Dictionary<Control, Point>();
            void Keep(Control c) => _base[c] = new Point(Canvas.GetLeft(c), Canvas.GetTop(c));
            for (int i = 0; i < Slots; i++) { Keep(_pictures[i]); Keep(_names[i]); Keep(_hits[i]); Keep(_kick[i]); }
            _carryWidth = _carry.Width;
        }
        void Move(Control c, double x, double y)
        {
            var p = _base[c];
            Canvas.SetLeft(c, p.X + x);
            Canvas.SetTop(c, p.Y + y);
        }
        for (int i = 0; i < Slots; i++)
        {
            double x = ServiceForms.Companions.RightHand(i) ? dw : 0, y = i >= 2 ? dh : 0;
            Move(_pictures[i], x, y);
            Move(_names[i], x, y);
            Move(_hits[i], x, y);
            Move(_kick[i], dw, 0);
        }
        if (!double.IsNaN(_carryWidth)) _carry.Width = _carryWidth + dw;
    }
}
