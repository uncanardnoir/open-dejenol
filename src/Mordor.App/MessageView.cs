using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The Messages window's list, kept in step with the game's message list by change rather than
/// rebuilt. The window used to be handed a fresh copy of every line on each new message, so the
/// list threw away and redrew all its rows and its scroll bar went to the top before being sent
/// back to the bottom. Only the lines that went (from the top, as MessageList trims) and the lines
/// that came (at the bottom) are touched now; what is shown is the same list, line for line, and
/// the newest line is selected and scrolled to as before.
///
/// Each row is drawn as its text alone, in the window's own colour unless the line's
/// <see cref="MessageLook"/> carries a colour (a mod's MessageLineColour, asked once as the line was
/// added and kept with it); the text is never changed.
/// </summary>
internal static class MessageView
{
    /// <summary>A shown line: its text and its look (kind and colour), as kept with the line.</summary>
    internal sealed record Row(string Text, MessageLook Look)
    {
        public override string ToString() => Text;
    }

    private static readonly FuncDataTemplate<Row> RowTemplate = new((r, _) =>
    {
        var text = new TextBlock { Text = r?.Text ?? "" };
        if (r?.Look.Colour is { } c)
            text.Foreground = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
        return text;
    });

    public static void Show(ListBox box, IReadOnlyList<string> messages, IReadOnlyList<MessageLook>? looks = null)
    {
        // the looks run beside the lines one for one (MessageList.Add keeps them so); any other
        // count draws every line plain rather than give a line another's look
        bool paired = looks != null && looks.Count == messages.Count;
        var rows = new List<Row>(messages.Count);
        for (int i = 0; i < messages.Count; i++) rows.Add(new Row(messages[i], paired ? looks![i] : MessageLook.Plain));
        if (box.ItemsSource is not ObservableCollection<Row> shown)
        {
            shown = new ObservableCollection<Row>(rows);
            box.ItemTemplate = RowTemplate;
            box.ItemsSource = shown;
        }
        else Sync(shown, rows);

        box.SelectedIndex = shown.Count - 1;
        if (shown.Count > 0)
            Dispatcher.UIThread.Post(() =>
            {
                if (box.ItemsSource == shown && shown.Count > 0) box.ScrollIntoView(shown.Count - 1);
            }, DispatcherPriority.Loaded);   // once the new row is laid out, ahead of queued input
    }

    /// <summary>
    /// Make <paramref name="shown"/> equal <paramref name="messages"/>: drop the fewest lines from
    /// the top so that what is left is where the new list starts, then add what is new. Whatever
    /// that finds, the result is the new list exactly.
    /// </summary>
    internal static void Sync<T>(IList<T> shown, IReadOnlyList<T> messages)
    {
        var eq = EqualityComparer<T>.Default;
        if (messages.Count == 0) { shown.Clear(); return; }   // a clear: one change, not one per line
        int drop = shown.Count;
        for (int k = 0; k < shown.Count; k++)
        {
            int n = shown.Count - k;
            if (n > messages.Count) continue;
            bool same = true;
            for (int i = 0; i < n && same; i++) same = eq.Equals(shown[k + i], messages[i]);
            if (same) { drop = k; break; }
        }
        for (int i = 0; i < drop; i++) shown.RemoveAt(0);
        for (int i = shown.Count; i < messages.Count; i++) shown.Add(messages[i]);
    }
}
