namespace Mordor.Core;

/// <summary>
/// The sounds one line of a fight makes, in the order the original makes them, and which of them
/// "No Party Sound" (gv1A66.M5F48) silences. Both of the window's ways through a monsters' turn -
/// the timed round (RoundPaced) and the engage/move tick (MonsterAttacks) - ask this, line by
/// line, so a line sounds the same whichever of them yields it.
///
/// No Party Sound is tested at every guarded call in the same shape,
///
/// <code>
/// If (gv1A66.M5F48 = True And pv0442 = Me) Or gv1A66.M5F48 = False Then
/// </code>
///
/// with <c>Me</c> the character being played, so with the box ticked a line whose
/// <see cref="CombatEngine.CombatLine.Actor"/> is somebody else is silent. A line with no actor
/// is one whose sounds the original plays with no such test: the cry for help (MONSMOD.BAS:3406,
/// which has only its own No HELP Sound box) and a monster spell's sub0E99 (MONSMOD.BAS:2850-2852).
/// <see cref="CombatEngine.CombatLine.PickSounds"/> - fn0FF5's sub1005 (MONSMOD.BAS:245) - have no
/// test either, and come before anything else on the line.
///
/// No Sound FX is not asked here: every one of these goes through sub09DE (MUSIC.BAS:143) or
/// sub086D (MUSIC.BAS:204), which leave at once with it ticked, and so does the port's Audio.Play.
/// </summary>
public static class CombatSounds
{
    /// <summary>
    /// The sounds of <paramref name="line"/>. <paramref name="swing"/> is the part before a
    /// character's blow is rolled (the picks, the cast and the swing, MONSMOD.BAS:344-350) and
    /// <paramref name="blow"/> the part after it lands (444-473); a line that is not a blow is
    /// asked for both at once.
    /// </summary>
    public static IEnumerable<string> Of(CombatEngine.CombatLine line, Character? watched, bool noPartySound,
                                         bool swing = true, bool blow = true)
    {
        if (swing && line.PickSounds != null) foreach (string s in line.PickSounds) yield return s;
        if (noPartySound && line.Actor != null && line.Actor != watched) yield break;
        if (swing && line.CastSounds != null) foreach (string s in line.CastSounds) yield return s;
        if (swing && line.SwingSound != null) yield return line.SwingSound;
        if (blow && line.HitSounds != null) foreach (string s in line.HitSounds) yield return s;
    }

    /// <summary>
    /// Every sound a run of lines makes, as the window plays them: each line once, whatever
    /// channel it is on - a line with nothing to show is as loud as one with a caption. A line
    /// that opens the chest is handed to monOpenChest instead and makes none of its own.
    /// </summary>
    public static List<string> OfLines(IEnumerable<CombatEngine.CombatLine> lines, Character? watched, bool noPartySound)
    {
        var all = new List<string>();
        foreach (var line in lines)
        {
            if (line.OpensChest) continue;
            all.AddRange(Of(line, watched, noPartySound));
        }
        return all;
    }
}
