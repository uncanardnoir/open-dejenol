using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mordor.App;

/// <summary>
/// The original's password box, <c>fn170D(..., "PASSWORD")</c>: one line, shown as asterisks,
/// with a way to back out. It is asked for in four places - creating a character, loading one,
/// changing the password, and asking somebody to join your party.
/// </summary>
public static class PasswordBox
{
    /// <summary>Returns what was typed, or null if the player backed out.</summary>
    public static async System.Threading.Tasks.Task<string?> Ask(Window owner, string title, string prompt)
    {
        string? answer = null;
        var box = new TextBox { PasswordChar = '*', Width = 240 };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        // fn170D shows frmInteract: its font through the Options fonts (UiFonts)
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
                        new TextBlock { Text = prompt, Foreground = Brushes.LightGray },
                        box,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { ok, cancel } },
                    },
                },
            },
        }, Mordor.Core.DesignFonts.Interact, "lblQuestion");
        ok.Click += (_, _) => { answer = box.Text ?? ""; win.Close(); };
        cancel.Click += (_, _) => win.Close();
        win.Opened += (_, _) => box.Focus();
        await win.ShowDialog(owner);
        return answer;
    }
}
