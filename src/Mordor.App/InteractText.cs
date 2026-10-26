using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// <c>frmInteract</c> asking for text (FRMINTER.BAS): a caption, <c>lblQuestion</c> and
/// <c>txtQuestion</c>, with OK and Cancel. OK hands the text to the question's own test
/// (<c>cmdOK_Click</c>, 116-151 and 282-290): it answers with what fn170D returns, or with a
/// refusal, which is put in a <c>MsgBox(..., 48, "SORRY!")</c> before the box is emptied and the
/// question stays open. Cancel returns "-1" (36-47), as the window's close box does here.
/// </summary>
public static class InteractText
{
    public static async System.Threading.Tasks.Task<string> Ask(Window owner, string title, string label, int maxLength,
                                                                 Func<string, (string? Tag, string? Refusal)> ok)
    {
        string answer = "-1";
        var box = new TextBox { Width = 260, MaxLength = maxLength, HorizontalAlignment = HorizontalAlignment.Left };
        var okButton = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // fn170D's frmInteract, in its font through the Options fonts (UiFonts)
        var win = UiFonts.Set(new Window
        {
            Title = title,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            Background = Brush.Parse("#101018"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = label, Foreground = Brushes.LightGray },
                        box,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { okButton, cancel } },
                    },
                },
            },
        }, Mordor.Core.DesignFonts.Interact, "lblQuestion");
        bool busy = false;
        okButton.Click += async (_, _) =>
        {
            if (busy) return;
            var (tag, refusal) = ok(box.Text ?? "");
            if (refusal != null)
            {
                busy = true;
                await OriginalBox.Tell(win, Mordor.Core.GiveCommand.Sorry, refusal);
                box.Text = "";                     // txtQuestion.Text = "" : Exit Sub
                busy = false;
                box.Focus();
                return;
            }
            answer = tag ?? "-1";
            win.Close();
        };
        cancel.Click += (_, _) => { answer = "-1"; win.Close(); };
        win.Opened += (_, _) => box.Focus();       // txtQuestion.SetFocus (FRMINTER.BAS:483-485)
        await OriginalBox.ShowOver(win, owner);
        return answer;
    }
}
