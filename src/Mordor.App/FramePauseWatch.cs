using Avalonia.Controls;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// frmMain's <c>MDIForm_Resize</c> (FRMMAIN.BAS:202-233) for the port's frames: the main menu and
/// the town's frame, which every other window of a session sits in. Minimised, the game pauses -
/// the play clock, the music and the effects; back, they go on. See <see cref="FramePause"/>.
/// </summary>
public static class FramePauseWatch
{
    public static void Watch(Window frame)
    {
        frame.PropertyChanged += (_, e) =>
        {
            if (e.Property != Window.WindowStateProperty) return;
            bool was = e.OldValue is WindowState.Minimized, now = e.NewValue is WindowState.Minimized;
            switch (FramePause.Transition(was, now))
            {
                case FramePause.Change.Paused: Audio.Pause(); break;
                case FramePause.Change.Resumed: Audio.Resume(); break;
            }
        };
        // a frame closed from the task bar while minimised hands the game back to the menu, which
        // is not minimised: the port's frames are separate windows where the original's is one
        frame.Closed += (_, _) =>
        {
            if (frame.WindowState == WindowState.Minimized && FramePause.Transition(true, false) == FramePause.Change.Resumed)
                Audio.Resume();
        };
    }
}
