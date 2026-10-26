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
/// Placing a VB3 form's controls where its form data in MORDOR.EXE puts them. Positions there are
/// twips, fifteen to the pixel; font sizes are points, and a VB3 point size in MS Sans Serif is
/// drawn at the pixel size below (8.25 at 11, 9.75 at 13, 12 at 16, 24 at 32).
/// </summary>
internal static class VbForm
{
    public static readonly FontFamily Sans = new("Microsoft Sans Serif, MS Sans Serif, Arial");
    public static readonly FontFamily Times = new("Times New Roman");

    /// <summary>The system button face, which several of these controls take as their BackColor.</summary>
    public static readonly IBrush ButtonFace = Brush.Parse("#F0F0F0");
    public static readonly IBrush Grey = Brush.Parse("#C0C0C0");
    /// <summary>&amp;H00E0FFFF&amp;, the pale yellow the About, Credits and Hall panels are filled with.</summary>
    public static readonly IBrush PaleYellow = Brush.Parse("#FFFFE0");
    public static readonly IBrush Navy = Brush.Parse("#000080");
    public static readonly IBrush Maroon = Brush.Parse("#800000");

    public static T At<T>(this T c, int left, int top, int width, int height) where T : Control
    {
        Canvas.SetLeft(c, left / 15.0);
        Canvas.SetTop(c, top / 15.0);
        c.Width = width / 15.0;
        c.Height = height / 15.0;
        return c;
    }

    /// <summary>A control at its form rectangle (<see cref="ServiceForms"/>), in twips.</summary>
    public static T At<T>(this T c, FormRect r) where T : Control => c.At(r.Left, r.Top, r.Width, r.Height);

    /// <summary>
    /// A control at its form rectangle on a form grown for a larger Options font
    /// (<see cref="UiFonts.FormGrowth"/>, as <see cref="FormPage"/> grows a service's): one each way,
    /// with the default fonts, is <see cref="At{T}(T, int, int, int, int)"/>.
    /// </summary>
    public static T At<T>(this T c, int left, int top, int width, int height, (double X, double Y) grow) where T : Control
    {
        Canvas.SetLeft(c, left / 15.0 * grow.X);
        Canvas.SetTop(c, top / 15.0 * grow.Y);
        c.Width = width / 15.0 * grow.X;
        c.Height = height / 15.0 * grow.Y;
        return c;
    }

    public static T At<T>(this T c, FormRect r, (double X, double Y) grow) where T : Control => c.At(r.Left, r.Top, r.Width, r.Height, grow);

    public static TextBlock Label(string text, double px, IBrush fore, bool bold = false, bool italic = false,
                                  TextAlignment align = TextAlignment.Left, FontFamily? face = null) => new()
    {
        Text = text, FontSize = px, Foreground = fore, FontFamily = face ?? Sans,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        TextAlignment = align, TextWrapping = TextWrapping.Wrap, ClipToBounds = true,
    };

    /// <summary>A sunken 3D panel, the look of a picture box or an elastic frame on these forms.</summary>
    public static Border Sunken(IBrush fill, Control child) => new()
    {
        Background = fill, BorderThickness = new Thickness(2, 2, 1, 1),
        BorderBrush = Brush.Parse("#808080"), Child = child, ClipToBounds = true,
    };

    public static Button Button(string caption, int px = 11, bool bold = false) => new()
    {
        Content = caption, FontFamily = Sans, FontSize = px, Padding = new Thickness(0),
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(0), Foreground = Brushes.Black, Background = Grey,
        BorderBrush = Brush.Parse("#808080"), BorderThickness = new Thickness(1),
    };

    /// <summary>A picture read out of the player's MORDOR.EXE at start (<see cref="Art"/>).</summary>
    public static Bitmap Asset(string name) => Art.Bitmap(name);

    public static Window Dialog(string title, double width, double height, IBrush back, Canvas content) => new()
    {
        Title = title, Width = width, Height = height, CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = back,
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light, ShowInTaskbar = false,
        FontFamily = Sans, Content = content,
    };
}

/// <summary>
/// frmAbout (FRMABOUT.BAS), shown modally by cmdCommand(8). Its form in MORDOR.EXE: caption "About
/// MORDOR", grey, the MORDOR icon, "MORDOR (The Depths of Dejenol)" in maroon, lblVersion in navy
/// under it, a pale yellow picture box holding the copyright line in Times New Roman, and three
/// buttons of which "Ordering Info" is shown only when <c>m0032</c> is True (FRMABOUT.BAS:24): never
/// in the full build, and in the limited one, played on the shareware's data
/// (<see cref="LimitedVersion"/>), where it opens <see cref="OrderInfoWindow"/>.
/// </summary>
public sealed class AboutWindow
{
    private readonly Window _win;

    public AboutWindow(string gameDir)
    {
        var form = new Canvas { Width = 282, Height = 95 };
        form.Children.Add(new Image { Source = VbForm.Asset("about_icon.png"), Stretch = Stretch.None }.At(90, 105, 480, 480));
        form.Children.Add(VbForm.Label("MORDOR (The Depths of Dejenol)", 13, VbForm.Maroon, bold: true).At(645, 90, 3570, 240));
        // Form_Load (FRMABOUT.BAS:23) writes it: "Version " + gc0186 + " (Last Edited " + gc0196 + ")".
        // The label is 180 twips high for an 8.25 point line, so the bottom of the text is cut off,
        // as it is in the original. gc0196 is the full MORDOR.EXE's "7/20/95", and the shareware
        // one's "7/6/95" in the limited build (LimitedVersion.LastEdited).
        form.Children.Add(VbForm.Label("Version 1.1 (Last Edited " + LimitedVersion.LastEdited + ")", 11, VbForm.Navy).At(660, 375, 3195, 180));

        var info = new Canvas();
        info.Children.Add(VbForm.Label("Mordor is © Copyright 1995, MakeItSo Software", 11, Brushes.Black,
                                       align: TextAlignment.Center, face: VbForm.Times).At(15, 45, 4005, 240));
        form.Children.Add(VbForm.Sunken(VbForm.PaleYellow, info).At(90, 645, 4080, 345));

        var credits = VbForm.Button("Credits...").At(1590, 1080, 1260, 285);
        var ok = VbForm.Button("OK").At(2925, 1080, 1260, 285);
        form.Children.Add(credits);
        form.Children.Add(ok);
        // cmdInfo, "Ordering Info", 90,1080 1425x285 in the form, hidden there (FRMABOUT.FRM)
        Button? order = null;
        if (LimitedVersion.ShowsOrderingInfo)
        {
            order = VbForm.Button("Ordering Info").At(90, 1080, 1425, 285);
            form.Children.Add(order);
        }

        _win = VbForm.Dialog("About MORDOR", 282, 95, VbForm.Grey, form);
        // cmdCredits_Click (FRMABOUT.BAS:10-12) and cmdOK_Click (FRMABOUT.BAS:18-20). Neither is the
        // form's default or cancel button: Enter presses whichever has the focus, and Escape nothing.
        credits.Click += async (_, _) => await new CreditsWindow().ShowDialog(_win);
        // cmdInfo_Click: frmOrderInfo.Show 1, modal (FRMABOUT.BAS:14-16)
        if (order != null) order.Click += async (_, _) => await new OrderInfoWindow().ShowDialog(_win);
        ok.Click += (_, _) => _win.Close();
        _win.Opened += (_, _) => ok.Focus();
    }

    public System.Threading.Tasks.Task ShowDialog(Window owner) => OriginalBox.ShowOver(_win, owner);
}

/// <summary>
/// frmOrderInfo (FRMORDER.BAS), the limited build's ordering information, reached from About's
/// "Ordering Info". Its form in MORDOR.EXE (FRMORDER.FRM): caption "Mordor Ordering Information",
/// 9060 x 5040 twips, one grey elastic over it holding lblInfo (210,165 8655x1590), lblOrderInfo
/// (210,1875 8655x2580) and cmdCommand(0) "OK" (1335x330 at a top of 4605), which Form_Load
/// centres. The labels name no font, so they take VB3's default, MS Sans Serif 8.25 bold. Their
/// text is Form_Load's (<see cref="LimitedVersion.OrderInfo"/>, <see cref="LimitedVersion.OrderHow"/>).
/// </summary>
public sealed class OrderInfoWindow
{
    private readonly Window _win;

    public OrderInfoWindow()
    {
        var form = new Canvas { Width = 604, Height = 336 };
        form.Children.Add(VbForm.Label(LimitedVersion.OrderInfo, 11, Brushes.Black, bold: true).At(210, 165, 8655, 1590));
        form.Children.Add(VbForm.Label(LimitedVersion.OrderHow, 11, Brushes.Black, bold: true).At(210, 1875, 8655, 2580));
        // cmdCommand(0).Left = (Me.Width / 2) - (cmdCommand(0).Width / 2) (FRMORDER.BAS:33)
        var ok = VbForm.Button("OK").At(9060 / 2 - 1335 / 2, 4605, 1335, 330);
        form.Children.Add(ok);
        _win = VbForm.Dialog(LimitedVersion.OrderTitle, 604, 336, VbForm.Grey, form);
        // cmdCommand_Click: Unload Me (FRMORDER.BAS:9-11)
        ok.Click += (_, _) => _win.Close();
        _win.Opened += (_, _) => ok.Focus();
    }

    public System.Threading.Tasks.Task ShowDialog(Window owner) => OriginalBox.ShowOver(_win, owner);
}

/// <summary>
/// frmCredits (FRMCREDI.BAS). The layout, the headings and the picture are the form's in
/// MORDOR.EXE; the names are written by Form_Load. Every two seconds (timAnimate, Interval 2000)
/// the next label tagged "C", counting backwards through the form's controls, turns green and a
/// random one of the first seventy-one sound effects plays (timAnimate_Timer, FRMCREDI.BAS:79-96).
///
/// The label it leaves is given ForeColor 9 - the number, not QBColor(9) - which is a red of 9 out
/// of 255 and looks black. The names start black, so the slip cannot be seen; it is kept.
/// </summary>
public sealed class CreditsWindow
{
    private readonly Window _win;

    /// <summary>SoundFiles(0 To 70) (<see cref="SoundFiles"/>) - Int(Rnd * 71) never reaches CHIT, number 71.</summary>
    private static readonly string[] Sounds = SoundFiles.Names[..71];

    public CreditsWindow()
    {
        string Lines(params string[] names) => string.Join("\n", names);

        // Form_Load (FRMCREDI.BAS:16-72), each label's text as it builds it
        string testers0 = Lines("Terry Brown", "Douglas Chinn", "Dale Cutler", "Tim Dransfield", "Lloyd Duhon",
            "Leland F. Dumas", "Bob Foley", "Brent Goodpaster", "Erik Johnson", "Todd Levi", "Ariana Mandel",
            "Paul Morash", "Thomas Nehring", "Peter Perchansky", "Keith E. Petersen", "Greg Beaty",
            "Barry Schlossberg", "Rick Sylander", "Mike Vos", "Jochen Walder", "Steve Watkins", "Brent Williams",
            "Brad Williamson", "Marv Heston");
        string testers1 = Lines("Roe R. Adams", "David Cooley", "Rip Loomis", "Angelo Montesinos",
            "Jennifer Schlickbernd", "Richard Sun", "Mike Engle");
        string thanks = Lines("Marv Heston", "Larry Allen", "Mike Vos", "Erik Johnson", "Tim DransField",
            "Tom Upchurch", "Greg Beaty", "", "...and especially Paul Morash and Meg Ross for helping make the game what it is!");

        var pic = new Canvas();
        TextBlock Heading(string t, int l, int top, int w, int h) { var x = VbForm.Label(t, 13, VbForm.Maroon, bold: true).At(l, top, w, h); pic.Children.Add(x); return x; }
        TextBlock Names(string t, int l, int top, int w, int h) { var x = VbForm.Label(t, 11, Brushes.Black, bold: true).At(l, top, w, h); pic.Children.Add(x); return x; }

        // the form's controls in the order it lists them; the ones tagged "C" are the names
        var tagged = new List<TextBlock>();
        pic.Children.Add(VbForm.Label("This is David AFTER programming Mordor...", 11, VbForm.Maroon, bold: true,
                                      align: TextAlignment.Center).At(2325, 3210, 2280, 510));
        pic.Children.Add(new Image { Source = VbForm.Asset("credits_david.png"), Stretch = Stretch.None }.At(2340, 3735, 2250, 2250));
        tagged.Add(Names(testers1, 2445, 1020, 2130, 2130));
        tagged.Add(Names(Lines("David Allen", "Carsten Pedersen", "Shannon McConnell"), 5205, 2535, 1995, 660));
        Heading("Helplesson & Manual", 5040, 2280, 2250, 300);
        Heading("Special Thanks To", 5040, 3165, 2055, 300);
        Heading("BETA Testers", 165, 735, 1725, 255);
        Heading("Computer Art", 5040, 1395, 1515, 300);
        Heading("Music & Sound FX", 5040, 735, 2025, 270);
        tagged.Add(Names(thanks, 5205, 3450, 1845, 2610));
        tagged.Add(Names(testers0, 300, 1020, 2010, 4890));
        tagged.Add(Names(Lines("Meg Ross", "Tareh Kryger", "Toshie Yamaguchi"), 5205, 1665, 1995, 600));
        tagged.Add(Names(Lines("Tim Melton", "David Allen (FX)"), 5205, 975, 1995, 450));
        tagged.Add(Names("David Allen", 300, 330, 4020, 210));
        Heading("Creator, Programmer & Developer", 165, 60, 3615, 345);

        var form = new Canvas { Width = 508, Height = 440 };
        form.Children.Add(VbForm.Sunken(VbForm.PaleYellow, pic).At(45, 60, 7530, 6120));
        var ok = VbForm.Button("OK", 11, bold: true).At(5880, 6255, 1680, 315);
        ok.IsDefault = true;
        form.Children.Add(ok);

        // Me.Caption = "Mordor " + gc0186 + " Credits" (FRMCREDI.BAS:23)
        _win = VbForm.Dialog("Mordor 1.1 Credits", 508, 440, VbForm.ButtonFace, form);
        ok.Click += (_, _) => _win.Close();

        var rng = new Random();
        // m001C, a module-level Integer, starts at 0; m001A is set to -1 by Form_Load (FRMCREDI.BAS:22).
        // current stands in for m001C among the tagged labels; starting it at 0 makes the first tick
        // wrap to the last, as m001C - 1 < 0 does.
        int current = 0, last = -1;
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        timer.Tick += (_, _) =>
        {
            // backwards through the controls, wrapping from the first to the last
            current = current - 1 < 0 ? tagged.Count - 1 : current - 1;
            if (last > -1) tagged[last].Foreground = Brush.Parse("#090000");   // ForeColor = 9
            tagged[current].Foreground = Brush.Parse("#008000");   // QBColor(2)
            last = current;
            Audio.PlayOnNextChannel(Sounds[rng.Next(Sounds.Length)]);           // sub086D Int(Rnd * 71)
        };
        _win.Opened += (_, _) => { timer.Start(); ok.Focus(); };
        _win.Closed += (_, _) => timer.Stop();
    }

    public System.Threading.Tasks.Task ShowDialog(Window owner) => OriginalBox.ShowOver(_win, owner);
}
