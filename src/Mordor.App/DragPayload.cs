using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace Mordor.App;

/// <summary>
/// What is being dragged. The original decides by the dragged control's Tag: <c>"ITEM"</c>,
/// <c>"SPELL"</c>, <c>"CHAR"</c>, <c>"COMP"</c>, or <c>"BANK"</c> followed by a slot number - see
/// <c>lblBuffer_DragDrop</c> (FRMCHAR1.BAS:215) and <c>lblItem_DragDrop</c> (FRMCHAR3.BAS:518).
/// This carries the same two things Avalonia's way.
/// </summary>
public sealed record DragPayload(DragKind Kind, int Index)
{
    /// <summary>The name the payload travels under in a <see cref="DataObject"/>.</summary>
    public const string Format = "mordor/dragged";

    /// <summary>A pack slot, which is what a buffer stores directly.</summary>
    public static DragPayload Item(int packSlot) => new(DragKind.Item, packSlot);

    /// <summary>An index into the spell table; a buffer stores it plus forty.</summary>
    public static DragPayload Spell(int spellIndex) => new(DragKind.Spell, spellIndex);

    /// <summary>
    /// A line of the bank's vault. The original tags these <c>"BANK"</c> followed by the slot
    /// number and reads the number back off the Tag (FRMCHAR3.BAS:527-529).
    /// </summary>
    public static DragPayload Vault(int bankSlot) => new(DragKind.Vault, bankSlot);

    /// <summary>
    /// A place in the party, tagged <c>"CHAR"</c> in the original. Dragging one name onto another
    /// swaps the two (<c>lblName_DragDrop</c>, FRMGROUP.BAS:218-228) - "A player can change the
    /// order of the party (and swap character positions) by Dragging and Dropping characters names
    /// within the Name field of the Party Information Window" (help, Parties).
    /// </summary>
    public static DragPayload Member(int partySlot) => new(DragKind.Character, partySlot);

    /// <summary>
    /// A companion's picture on the Companions page, <c>picCompanion(Index)</c>, tagged
    /// <c>"COMP"</c>; the drop reads <c>Source.Index</c>, the slot (FRMCHAR3.BAS:765,
    /// FRMCITYC.BAS:746-747). It is dragged whenever a button is down over it
    /// (<c>picCompanion_MouseMove</c>, FRMCHAR3.BAS:795-799).
    /// </summary>
    public static DragPayload Companion(int slot) => new(DragKind.Companion, slot);

    /// <summary>
    /// What a spell buffer holds after this lands on it, <c>lblBuffer_DragDrop</c>
    /// (FRMCHAR1.BAS:219-233): "ITEM" stores the slot and "SPELL" the spell plus forty. Any other
    /// Tag - a vault line, a name - matches no Case and the buffer keeps what it had (null here).
    /// A slot that holds nothing is stored too, and then <c>mdcharShowBuffer</c> finds
    /// <c>Pack(l06C4).Ref &lt;= False</c> and writes -1 back, "(Nothing)" (MAINMOD2.BAS:4646-4649),
    /// so dropping an empty slot on a buffer empties it. The Hands are slot 0 with Ref 0, so they
    /// empty it the same way.
    /// </summary>
    public int? BufferValue(Mordor.Core.Character ch) => Kind switch
    {
        DragKind.Item => Index < 0 || Index >= Mordor.Core.Character.PackSize || ch.Pack[Index] is not { ItemId: > 0 }
                             ? Mordor.Core.SpellBuffers.Empty : Mordor.Core.SpellBuffers.ForItem(Index),
        DragKind.Spell => Mordor.Core.SpellBuffers.ForSpell(Index),
        _ => null,
    };

    /// <summary>
    /// The character whose list a spell or pack line was dragged from (the one in command), or null.
    /// Set by the Spells and pack lists of Objects &amp; Spells, so a mod's window can take the same
    /// drag the buffers do (<see cref="Mordor.Modding.ModDragged"/>); the game's own targets do not read it.
    /// </summary>
    public Mordor.Core.Character? Owner { get; init; }

    public static DragPayload? From(DragEventArgs e) => e.Data.Get(Format) as DragPayload;

    public DataObject Wrapped()
    {
        var d = new DataObject();
        d.Set(Format, this);
        // and for a mod's window, the same line as a ModDragged (docs/MODDING.md, ctx.Game)
        if (Owner != null && Kind is DragKind.Item or DragKind.Spell)
            d.Set(Mordor.Modding.ModDragged.Format, new Mordor.Modding.ModDragged(
                Kind == DragKind.Spell ? Mordor.Modding.ModDragKind.Spell : Mordor.Modding.ModDragKind.PackItem, Owner, Index));
        return d;
    }
}

public enum DragKind { Item, Spell, Vault, Character, Companion }

/// <summary>
/// Objects &amp; Spells' four commands as drop targets, the same in town and below. The original's
/// only command button with a DragDrop handler in any form is this array:
/// <c>cmdCommand_DragDrop (Index%, Source As Control, ...)</c> is the one line
/// <c>cmdCommand_Click Index</c> (FRMCHAR3.BAS:252-254). It reads nothing of the Source, so
/// whatever is let go over Equip, Use, Drop or Info presses it - on the item selected, which for a
/// dragged pack line is that line, since <c>lblItem_MouseMove</c> runs <c>lblItem_Click Index</c>
/// before every <c>Drag</c> (FRMCHAR3.BAS:589-597). A spell, a companion, a vault line, a party
/// name: the press is the same, on whatever item was selected.
/// </summary>
public static class PackCommands
{
    /// <param name="pack">the Items page's list, whose selection is <c>m001A</c></param>
    /// <param name="owner">the character on the page: only a line of their own pack moves the selection</param>
    /// <param name="commands">cmdCommand(0 To 3): Equip, Use, Drop, Info</param>
    public static void Wire(ListBox pack, Func<Mordor.Core.Character> owner, params Button[] commands)
    {
        foreach (var b in commands)
            Dragging.PressedBy(b, p =>
            {
                // lblItem_Click Index, from lblItem_MouseMove (FRMCHAR3.BAS:592-593): a line of this
                // pack is the item selected when it lands; anything else leaves the selection be
                if (p.Kind == DragKind.Item && p.Owner != null && ReferenceEquals(p.Owner, owner())
                    && p.Index >= 0 && p.Index < pack.ItemCount)
                    pack.SelectedIndex = p.Index;
            });
    }
}

/// <summary>
/// The Companions page of Objects &amp; Spells as a drag source and a drop target, the same in town
/// and below. The original has a picture per companion: dragging one drags <c>"COMP"</c> with its
/// slot (FRMCHAR3.BAS:795-799), and dropping one on another rearranges them
/// (<c>picCompanion_DragDrop</c>, FRMCHAR3.BAS:763-779, <see cref="Mordor.Core.CompanionSlots.Rearrange"/>).
/// Each picture (<see cref="CompanionsPage.Pictures"/>, its slot as its Tag) is both; an empty
/// slot's picture is hidden, so it neither drags nor takes a drop.
/// </summary>
public static class CompanionDrag
{
    /// <param name="pictures">picCompanion(0..3), each tagged with its slot</param>
    /// <param name="slots">the companions of the character on the page, read when the drag happens</param>
    /// <param name="say">sub085B, for "No use re-arranging a dead companion!"</param>
    /// <param name="rearranged">mdrcharShowCompanions Me (FRMCHAR3.BAS:778): save and repaint</param>
    public static void Wire(System.Collections.Generic.IEnumerable<Control> pictures, Func<Mordor.Core.CompanionSlots> slots, Action<string> say, Action rearranged)
    {
        foreach (var picture in pictures)
        {
            if (picture.Tag is not int onto) continue;
            Dragging.Source(picture, () => slots()[onto] != null ? DragPayload.Companion(onto) : null);
            Dragging.Target(picture, DragKind.Companion, p =>
            {
                if (onto == p.Index) return;   // "If Index <> Source.Index"
                if (slots().Rearrange(onto, p.Index) is string no) { say(no); return; }
                rearranged();
            });
        }
    }
}

/// <summary>
/// Turning a control into a drag source or a drop target. The original starts a drag from a
/// MouseMove with the button down (<c>lblItem_MouseMove</c>, FRMCHAR3.BAS:589-597) and decides what
/// was dropped from the source control's Tag; these two do the same with Avalonia's machinery, and
/// both screens use them.
/// </summary>
public static class Dragging
{
    /// <summary>
    /// How far the pointer has to go from where the left button went down before a drag starts, in
    /// the window's pixels either way: the system's own drag rectangle (SM_CXDRAG, SM_CYDRAG, four
    /// pixels unless changed), or four when the platform does not say.
    /// </summary>
    private static Avalonia.Size DragDistance(Control from) =>
        TopLevel.GetTopLevel(from)?.PlatformSettings?.GetTapSize(PointerType.Mouse) is { Width: > 0, Height: > 0 } s
            ? s : new Avalonia.Size(4, 4);

    /// <remarks>
    /// DEVIATION: a drag starts once the pointer has left the system's drag rectangle round the
    /// point the left button went down on, with that button held all the way. lblItem_MouseMove
    /// drags on any MouseMove with the button down (FRMCHAR3.BAS:589-597), only returning early
    /// when the pointer has not moved since the last MouseMove
    /// (<c>If X = m0022 And Y = m0026 Then Exit Sub</c>, 590), and so did this. A
    /// pack row is 235 twips, under sixteen pixels, so a click with a pixel of hand-shake in it
    /// became a drag: the click was lost to it, and when the shake crossed into the next row the
    /// drop swapped two slots nobody meant to move. The same threshold serves every drag source -
    /// the pack, the spells, the party's names, the Exchange's lines.
    ///
    /// The second press of a double-click never starts a drag either. A double-click acts while
    /// the button is still down (the Bank's withdraw redraws the list and selects the next line),
    /// so a few pixels of movement after it would drag whatever is under the pointer now, which
    /// the player never pressed on. VB3 has no double-click on these labels to collide with.
    /// </remarks>
    public static void Source(Control from, Func<DragPayload?> what) => SourceCore(from, _ => true, what);

    /// <summary>
    /// A list whose lines are dragged: the payload is the line the left button went down on
    /// (<c>lblItem_MouseMove</c> drags <c>lblItem(Index)</c>, the label under the pointer,
    /// FRMCHAR3.BAS:589-597), not <c>SelectedIndex</c>. With Shift or
    /// Ctrl multi-selection the press extends or toggles the selection and SelectedIndex stays the
    /// anchor, so a drag of the third line of a Shift-selection would have taken the first. A press
    /// on no line (the list's empty space) starts nothing. <paramref name="line"/> is handed the
    /// pressed line's index and says what it carries, or null for nothing.
    /// </summary>
    public static void SourceLines(ListBox list, Func<int, DragPayload?> line)
    {
        int pressed = -1;
        SourceCore(list, e => (pressed = LineUnder(list, e)) >= 0, () => pressed >= 0 ? line(pressed) : null);
    }

    /// <summary>The index of <paramref name="list"/>'s own line under a press, or -1.</summary>
    internal static int LineUnder(ListBox list, PointerEventArgs e)
    {
        if (e.Source is not Avalonia.Visual v) return -1;
        for (var row = Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ListBoxItem>(v, true); row != null;
             row = Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ListBoxItem>(row))
            if (ItemsControl.ItemsControlFromItemContainer(row) == list) return list.IndexFromContainer(row);
        return -1;
    }

    private static void SourceCore(Control from, Func<PointerPressedEventArgs, bool> pressedOn, Func<DragPayload?> what)
    {
        Avalonia.Point? pressedAt = null;   // where the left button went down, in the window's pixels
        bool dragging = false;
        Avalonia.Point Where(PointerEventArgs e) =>
            TopLevel.GetTopLevel(from) is TopLevel top ? e.GetPosition(top) : e.GetPosition(from);
        from.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            // a left press of its own, not a right press made while the left was already held, and
            // not the second press of a double-click (see Source's remarks)
            pressedAt = e.GetCurrentPoint(from).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed
                        && e.ClickCount < 2 && pressedOn(e)
                        ? Where(e) : null;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        from.AddHandler(InputElement.PointerReleasedEvent, (_, _) => pressedAt = null,
                        Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        // and whatever a double-tap handler does, the press it came from is over as a drag
        from.AddHandler(InputElement.DoubleTappedEvent, (_, _) => pressedAt = null,
                        Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble,
                        handledEventsToo: true);
        from.PointerMoved += async (_, e) =>
        {
            if (dragging || pressedAt is not Avalonia.Point start) return;
            // a release that never came here (let go outside, or taken by a box) ends the press too
            if (!e.GetCurrentPoint(from).Properties.IsLeftButtonPressed) { pressedAt = null; return; }
            var now = Where(e);
            var room = DragDistance(from);
            if (Math.Abs(now.X - start.X) < room.Width && Math.Abs(now.Y - start.Y) < room.Height) return;
            if (what() is not DragPayload p) return;
            pressedAt = null;
            dragging = true;
            try
            {
                await DragDrop.DoDragDrop(e, p.Wrapped(), DragDropEffects.Move | DragDropEffects.Link);
            }
            catch (Exception ex)
            {
                // OLE refuses a drag for reasons that are none of the game's business - another
                // drag already running, a window losing focus mid-gesture. This is an async void
                // handler, so an exception escaping it takes the process down with it.
                CrashLog.Note("drag refused: " + ex.Message);
            }
            finally { dragging = false; }
        };
    }

    /// <summary>
    /// A button that anything dropped on presses, as a VB <c>cmd_DragDrop</c> that only calls its
    /// own <c>_Click</c> (FRMCHAR3.BAS:252-254). Every kind is taken - VB hands any dragged control
    /// to DragDrop - and <paramref name="first"/> runs before the press, for what the drag itself
    /// did in the original before it was let go. A disabled button takes nothing, as a disabled
    /// VB control gets no events.
    /// </summary>
    public static void PressedBy(Button onto, Action<DragPayload>? first = null)
    {
        DragDrop.SetAllowDrop(onto, true);
        onto.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (DragPayload.From(e) == null || !onto.IsEffectivelyEnabled) return;
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        });
        onto.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (DragPayload.From(e) is not DragPayload p || !onto.IsEffectivelyEnabled) return;
            e.Handled = true;
            first?.Invoke(p);
            onto.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, onto));
        });
    }

    /// <summary>
    /// <paramref name="refused"/> is for the targets that say something when the wrong thing lands
    /// on them - the store's three fields answer "You may only drag and drop items on your
    /// character to the store." (FRMCTYS.BAS:773).
    /// </summary>
    /// <remarks>
    /// One control can carry several of these - a party row takes a name, a pack slot and a vault
    /// line (FRMGROUP.BAS:189-200), the pack list a slot and a vault line. Avalonia runs every
    /// handler on the element in turn but skips the ones added without handledEventsToo once
    /// <c>e.Handled</c> is set, so a target that marks the event handled for a kind it does not
    /// take swallows the drop meant for the next one. Only a target that deals with what arrived
    /// - it accepts it, or it has a refusal to say - marks it handled; the others leave the
    /// effect at None for a later target to raise.
    /// </remarks>
    public static void Target(Control onto, DragKind accepts, Action<DragPayload> took, Action? refused = null)
    {
        DragDrop.SetAllowDrop(onto, true);
        onto.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            // a target that has something to say still has to accept the drop to be told about it
            var p = DragPayload.From(e);
            bool mine = p?.Kind == accepts;
            bool answers = p != null && refused != null;
            e.DragEffects = mine ? DragDropEffects.Move
                          : answers ? DragDropEffects.Link
                          : DragDropEffects.None;
            if (mine || answers) e.Handled = true;
        });
        onto.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (DragPayload.From(e) is not DragPayload p) { e.Handled = true; return; }
            if (p.Kind != accepts && refused == null) return;   // someone else's: leave it unhandled
            e.Handled = true;
            // the drop lands on the line under the pointer, as a VB DragDrop lands on the control it was
            // let go over - the list's selection is made to follow, so the handler reads that line
            if (onto is ListBox lb && e.Source is Avalonia.Visual v
                && Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ListBoxItem>(v, true) is ListBoxItem row)
                lb.SelectedIndex = lb.IndexFromContainer(row);
            if (p.Kind == accepts) took(p);
            else refused?.Invoke();
        });
    }
}
