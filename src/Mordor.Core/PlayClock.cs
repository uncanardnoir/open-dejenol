namespace Mordor.Core;

/// <summary>
/// <c>sub16F8</c> (MAINMOD2.BAS:6004-6023), the play clock behind the Char page's "Play Time". The
/// original calls it from its idle loop (MAINMOD2.BAS:6314) and the music timer, as fast as they
/// come round, and each call adds the <c>Timer</c> seconds since the last one to <c>M5906(5)</c>
/// of every character in the party - a Currency, so the fractions are kept to four places.
///
/// Its faults are kept:
/// <list type="bullet">
/// <item>Past midnight <c>Timer</c> starts again from nought, and the gap is taken as
/// <c>(86340 - last) + now</c>: 86340 where a day has 86400, so every midnight crossed while
/// playing loses a minute.</item>
/// <item>A party member whose clock has not started yet (its <c>gv1B1E</c> entry still 0, or its
/// count -1) is started and the routine leaves there with <c>Exit Sub</c> - the members after it
/// are neither started nor counted on that call.</item>
/// </list>
/// The start times are kept by party position, as <c>gv1B1E</c> is. <c>Settings.Paused</c>
/// (<see cref="FramePause.Paused"/>, set while the game is minimised) stops the counting but not
/// the restarting: each member's start is still moved on to now (MAINMOD2.BAS:6012-6019), so the
/// time spent minimised is never counted, not even when play resumes.
/// </summary>
public sealed class PlayClock
{
    /// <summary>The one the windows share, so two of them ticking the same party count it once.</summary>
    public static PlayClock Shared { get; } = new();

    private readonly List<float> _started = new();   // gv1B1E

    /// <summary>VB's <c>Timer</c>: seconds since midnight, as a Single.</summary>
    public static float Timer(DateTime now) => (float)now.TimeOfDay.TotalSeconds;

    /// <summary>One call of sub16F8 at <paramref name="timer"/>.</summary>
    public void Tick(IReadOnlyList<Character> party, float timer)
    {
        while (_started.Count < party.Count) _started.Add(0);   // ReDim Preserve as the party grows
        for (int i = 0; i < party.Count; i++)
        {
            var c = party[i];
            if (_started[i] == 0 || c.PlaySeconds == -1)
            {
                _started[i] = timer;
                return;
            }
            if (!FramePause.Paused)                                       // MAINMOD2.BAS:6012
            {
                float gap = timer < _started[i] ? (86340f - _started[i]) + timer : timer - _started[i];
                c.PlaySeconds = Math.Round(c.PlaySeconds + gap, 4, MidpointRounding.ToEven);
            }
            _started[i] = timer;                                          // 6019, paused or not
        }
    }
}
