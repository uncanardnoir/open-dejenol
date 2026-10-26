namespace Mordor.Core;

/// <summary>
/// <c>Settings.Paused</c> (<c>gv1ACC.M602F</c>), the one pause the original has. Only the main
/// frame's resize handler touches it (FRMMAIN.BAS:202-233):
/// <code>
///   If Me.WindowState = 1 Then                       ' minimised
///       mciMusic "Stop", "Close", Tag = ""           ' the music stops
///       If No Sound FX is off Then WaveMixActivate(gv20C8, False)
///       Settings.Paused = True
///   ElseIf Me.WindowState = 2 Then                   ' maximised: how the frame is restored
///       ... sub1A50                                  ' the music starts again
///       If No Sound FX is off Then WaveMixActivate(gv20C8, True)
///       Settings.Paused = False
///   End If
/// </code>
/// While it is set the play clock does not count (<c>sub16F8</c>, MAINMOD2.BAS:6012) and the music
/// timer leaves the player alone (<c>timMusic_Timer</c>, FRMMAIN.BAS:296).
///
/// The restore branch tests for 2, maximised. Nothing in the code ever maximises the frame (only
/// minimises it, FRMMAIN.BAS:271), so whether it starts maximised is a design-time property of frmMain; the binary form has a property byte of 2 straight after
/// its position (FRMMAIN.FRM, offset 0x36, tag 0x0A), which reads - tentatively, the tag is not decoded elsewhere - as the frame being designed
/// maximised - so restoring it from the task bar returns it to maximised and takes that branch.
/// The port's frame opens at its design size instead (a window-layout difference already
/// accepted), so its restore lands on Normal; the port takes any restore from minimised as that
/// branch. A frame merely un-maximised, never minimised, changes nothing in either.
/// </summary>
public static class FramePause
{
    /// <summary><c>Settings.Paused</c>.</summary>
    public static bool Paused { get; set; }

    /// <summary>What a change of the frame's state does to the pause.</summary>
    public enum Change { None, Paused, Resumed }

    /// <summary>
    /// The frame went from one state to another: minimised pauses (FRMMAIN.BAS:206-213), coming
    /// back from minimised resumes (214-226).
    /// </summary>
    public static Change Transition(bool wasMinimised, bool isMinimised)
    {
        if (isMinimised && !wasMinimised) { Paused = true; return Change.Paused; }
        if (wasMinimised && !isMinimised) { Paused = false; return Change.Resumed; }
        return Change.None;
    }
}
