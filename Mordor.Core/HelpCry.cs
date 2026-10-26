namespace Mordor.Core;

/// <summary>
/// "Help! My hits are ...". A character in the party who is badly hurt says so, unprompted, and
/// HELP.WAV plays with it unless the Options window's No HELP Sound is ticked.
///
/// It comes from two places, with the same threshold and the same words in both:
///
/// <code>
/// If Chars(l0144).Hits &lt; 10 Or Chars(l0144).Hits &lt;= (Chars(l0144).MaxHits * .1#) Then
///     If Not gv1A66.M5F71 Then sub09DE mc01B0
///     sub085B RTrim(Chars(l0144).Name) + " - " + Chr$(34) + "Help! My hits are " + ...
/// </code>
///
/// - <c>timAilment_Timer</c> (FRMDUNM.BAS:594-603) checks every living character on every tick of
///   the ailment clock, so it is said while you walk about, not only in a fight
/// - <c>sub0E85</c> (MONSMOD.BAS:3404-3409) checks the character a group has just
///   finished attacking
///
/// Both are inside "If UBound(Chars) &gt; False Then", so nobody cries for help when travelling
/// alone - there would be nobody to hear it.
///
/// The quote is opened and never closed. That is <c>Chr$(34)</c> appearing once in a line that
/// reads like speech, and it is reproduced. See docs/FIDELITY.md.
/// </summary>
public static class HelpCry
{
    /// <summary>Under ten hits, or under a tenth of the most this character has ever had.</summary>
    public static bool Needed(Character c) => c.CurHits < 10 || c.CurHits <= c.MaxHits * 0.1;

    public static string Line(Character c) =>
        $"{c.Name.TrimEnd()} - \"Help! My hits are {c.CurHits}/{c.MaxHits}!";

    /// <summary>
    /// The other line built the same way, <c>sub0E85</c> (MONSMOD.BAS:3285-3291). The
    /// character you are watching gets a caption in the window instead - "You have been
    /// Paralyzed!", with no quote at all - and everybody else speaks.
    /// </summary>
    public static string Paralysis(Character c) => $"{c.Name.TrimEnd()} - \"I've been Paralyzed!";

    /// <summary>What the watched character sees instead, as a caption rather than speech.</summary>
    public const string ParalysisWatched = "You have been Paralyzed!";

    /// <summary>Sound 56 in the table MODULE1.BAS:305 builds.</summary>
    public const string Sound = "HELP";
}
