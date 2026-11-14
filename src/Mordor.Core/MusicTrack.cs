namespace Mordor.Core;

/// <summary>
/// Which MIDI plays, and when. The original keeps the tunes in a table of eight, <c>gv2164</c>,
/// filled by <c>sub10AB</c> (MUSIC.BAS:401-422):
///
/// <code>
///  0 CITY      gv2096 (a Global never assigned, so 0)
///  1 MAIN      gc2098      5 GEN2      gc20A0
///  2 DUNGEON   gc209A      6 GEN3      gc20A2
///  3 INTRO     gc209C      7 THEME     gc20A4
///  4 GENERAL   gc209E
/// </code>
///
/// With the Options window's "Use Sound Canvas MIDI" (M5F39) every name is the Sound Canvas
/// arrangement instead: CITYSC, MAINSC, INTROSC, DUNGEOSC, GENERASC, GEN2SC, GEN3SC, THEMESC.
///
/// <c>sub063F</c> (MUSIC.BAS:433-469) plays a tune by number, and does nothing if that number is
/// already <c>mciMusic.Tag</c> - whether it is still playing or has finished. The tag is cleared
/// only when the player is closed (<c>fn19DC</c>, and minimising, FRMMAIN.BAS:209).
/// <c>mciMusic_Done</c> would set it to "-1" (FRMMAIN.BAS:122-124), but sub063F turns
/// <c>Notify</c> off before every Open and Play, so Done never fires for a tune.
///
/// Who asks for which tune:
/// - the title menu's Form_Load: THEME (FRMMENU.BAS:132)
/// - the town's Form_Load: CITY (FRMCTYM.BAS:87, a Static never set, so 0)
/// - the dungeon's Form_Load: DUNGEON (FRMDUNM.BAS:239)
/// - the Options window: THEME (FRMOPTIO.BAS:86, 113, 384)
/// - quitting: INTRO (MAINMOD.BAS:935), closed again two lines later by fn19DC before anything is
///   heard
/// - when a tune has stopped, <see cref="Next"/>
/// </summary>
public static class MusicTrack
{
    public const int City = 0, Main = 1, Dungeon = 2, Intro = 3, General = 4, Gen2 = 5, Gen3 = 6, Theme = 7;

    /// <summary>gc2094: the size of the table.</summary>
    public const int Count = 8;

    private static readonly string[] Plain = { "CITY", "MAIN", "DUNGEON", "INTRO", "GENERAL", "GEN2", "GEN3", "THEME" };
    private static readonly string[] SoundCanvas = { "CITYSC", "MAINSC", "DUNGEOSC", "INTROSC", "GENERASC", "GEN2SC", "GEN3SC", "THEMESC" };

    /// <summary>gv2164(track): the file's name without ".MID".</summary>
    public static string File(int track, bool soundCanvas) => (soundCanvas ? SoundCanvas : Plain)[track];

    /// <summary>
    /// <c>sub1A50</c> (MUSIC.BAS:79-97), which <c>timMusic</c> calls every ten seconds while no tune
    /// is playing (FRMMAIN.BAS:291-303), and which starts the music again when the game comes back
    /// from minimised (FRMMAIN.BAS:221):
    ///
    /// <code>
    /// If gv1B52 = False Then
    ///     sub063F gc20A4                                  ' THEME
    /// Else
    ///     If Int(Rnd * 100) &lt;= 25 Then
    ///         sub063F Val(frmMain!mciMusic.Tag)
    ///     Else
    ///         If Chars(m0112).DungeonLevel = False Then
    ///             sub063F gv2096                          ' CITY
    ///         Else
    ///             sub063F Int(Rnd * (gc2094 - 1)) + 1     ' 1 to 7
    ///         End If
    ///     End If
    /// End If
    /// </code>
    ///
    /// <c>gv1B52</c> is set while a party is in play (MAINMOD2.BAS:6308-6317). <c>m0112</c> is a
    /// Static never assigned, so 0: <c>Chars(0)</c>, the party's leader. The first draw is 26 in a
    /// hundred, not 25. Its tune is <c>Val(Tag)</c>: the tune that has just finished, which sub063F
    /// then refuses to play again - or, after minimising emptied the tag, <c>Val("")</c> = 0, CITY.
    /// Any choice equal to the tag does nothing, and the timer asks again ten seconds later.
    ///
    /// <paramref name="tag"/> is the tag, null for an empty one. <paramref name="leaderLevel"/> is
    /// null while no party is in play.
    /// </summary>
    public static int Next(int? tag, int? leaderLevel, System.Random rng)
    {
        if (leaderLevel == null) return Theme;
        if ((int)(rng.NextDouble() * 100) <= 25) return tag ?? City;
        if (leaderLevel == 0) return City;
        return (int)(rng.NextDouble() * (Count - 1)) + 1;
    }
}
