namespace Mordor.Core;

/// <summary>
/// The shareware ("PUBLIC Limited Version") build of Mordor, played when the player's data is the
/// shareware's (<see cref="GameFiles.Check"/>: <see cref="GameFilesVerdict.Limited"/>).
///
/// <para>The original has no run-time switch for it: the limited MORDOR.EXE is the same source
/// compiled with a handful of constants changed, and the full MORDOR.EXE still carries every
/// string of the limited branches (docs/GAME_FILES.md). The port cannot tell the builds apart by
/// their program, so it goes by the data the player has, and with shareware data behaves as the
/// limited build does. It only changes behaviour, as the original's two builds differ; it never
/// refuses to play.</para>
///
/// <para>What is switched, each where the full build has a constant False or 15:</para>
/// <list type="bullet">
/// <item><c>m009C</c> (FRMMAIN.BAS:34): the frame caption gets " (Limited Version)" (148-152), and
///   the goodbye box ("THANK YOU FOR PLAYING MORDOR!", 240-248) is not shown.</item>
/// <item><c>m0622</c> with <c>mc0624</c> (MAINMOD2.BAS:224-225): TAKE on a chute on the last level
///   says "You cannot take this Chute in the Limited Version." (3997-3998); and the "Public
///   Version" box at start (sub10F8, 6339-6345, called from Sub Main at 6455).</item>
/// <item><c>mc015E</c>, fn0A0B's clamp (MAZEMOD.BAS:409-422): a level past it is refused, "LIMITED
///   VERSION: Request to go to Level n is Denied.", and the party stays on the last level.</item>
/// <item><c>m02A6</c> (MAZEMOD.BAS:1535, a Static never set in the full build): a teleporter to a
///   level past the clamp "..but it doesn't go anywhere in the limited version..." (1794-1796),
///   and a chute walked onto on the last level "doesn't do anything in the Limited Version"
///   (1840-1841).</item>
/// <item><c>m0032</c> (FRMABOUT.BAS:8, 24, never set in the full build): About's "Ordering Info"
///   button, which opens frmOrderInfo (FRMORDER.BAS).</item>
/// <item><c>gv0180</c> (MAIN.TXT:27, never set in the full build): sub03E3's "Limitation Notice"
///   (MODULE1.BAS:932-940), until MORDOR.INI's Limited key holds a "T". The shareware build has it
///   set: observed in the original shareware 2026-10-26.</item>
/// </list>
/// <para>The limited value of <c>mc015E</c> and <c>mc0624</c> is the shareware's depth, 3:
/// "the Shareware version is limited to 3 dungeon levels" (MORDOR.WRI). The full build's are 15.</para>
/// </summary>
public static class LimitedVersion
{
    /// <summary>The player's data is the shareware's: the limited build's behaviour is on. Set at start.</summary>
    public static bool On { get; set; }

    /// <summary>Switch on or off from the check of the folder being played.</summary>
    public static void Apply(GameFilesCheck? check) => On = check?.Verdict == GameFilesVerdict.Limited;

    /// <summary>mc015E and mc0624 in the limited build: "limited to 3 dungeon levels" (MORDOR.WRI).</summary>
    public const int LastLevel = GameFiles.SharewareLevels;

    /// <summary>fn0A0B's mc015C: no level past it is loaded at all, limited or not (MAZEMOD.BAS:415-417).</summary>
    public const int DeepestLevel = GameFiles.FullLevels;

    /// <summary>
    /// The frame's caption, gc0192 + " " + gc0186, with " (Limited Version)" when m009C is True
    /// (FRMMAIN.BAS:148-152).
    /// </summary>
    public static string FrameCaption => On ? "Mordor 1.1 (Limited Version)" : "Mordor 1.1";

    /// <summary>MDIForm_Unload shows its goodbye box only when m009C = False (FRMMAIN.BAS:240).</summary>
    public static bool SaysGoodbye => !On;

    /// <summary>About's cmdInfo is made visible when m0032 = True (FRMABOUT.BAS:24).</summary>
    public static bool ShowsOrderingInfo => On;

    /// <summary>Sub Main: "If m0622 Then sub10F8" (MAINMOD2.BAS:6455).</summary>
    public static bool ShowsPublicBox => On;

    /// <summary>sub10F8's MsgBox, 64, captioned "Public Version" (MAINMOD2.BAS:6339-6345).</summary>
    public const string PublicBoxTitle = "Public Version";
    public const string PublicBoxText =
        "This is the PUBLIC version of Mordor 1.1, so please feel free to give it to your friends and distribute it as you wish!\n\n"
        + "Order the full commercial release now for hundreds of additional monsters, items and over a dozen more levels to explore!\n\n"
        + "For information on Ordering, see the About/Ordering Information section from the Main Menu!";

    /// <summary>
    /// sub03E3 "If gv0180 Then" (MODULE1.BAS:932-940), called from Sub Main before the data is read
    /// (MAINMOD2.BAS:6437) and so before the "Public Version" box (6455): unless MORDOR.INI's
    /// [Loading Information] Limited holds a "T", MsgBox 48 captioned "Limitation Notice", then
    /// Limited=True is written and the box is not shown again. The full build never sets gv0180;
    /// the shareware does - observed in the original shareware 2026-10-26: this box, first thing on
    /// every run there (under OTVDM its INI write did not last), its count gc0182 being 3, where
    /// the full build's MAIN.TXT:28 has 15.
    /// </summary>
    public static bool ShowsLimitationNotice => On;

    public const string LimitationNoticeTitle = "Limitation Notice";

    /// <summary>MODULE1.BAS:934-937, with gc0182 the shareware's 3 (observed in the original shareware 2026-10-26).</summary>
    public static string LimitationNoticeText =>
        "NOTICE!\n"
        + "Because this is a PUBLIC BETA version of MORDOR, only " + LastLevel + " dungeon levels are currently available.\n"
        + "\n"
        + "This message will not appear again.";

    /// <summary>
    /// The close box at the main menu. The full build's MDIForm_QueryUnload refuses an UnloadMode of
    /// 0 - the close box, the system menu, Alt+F4 - with "Please use the Mordor EXIT buttons to leave
    /// the game." (FRMMAIN.BAS:171-175). The shareware does not: at the main menu its close box asks
    /// "Exit Mordor?" (Yes and No, captioned "EXIT") and a Yes quits, with no goodbye box (observed
    /// in the original shareware 2026-10-26) - the rest of that QueryUnload (176-192) with the
    /// limited build's silent MDIForm_Unload, as its earlier program has no refusal. Its program is
    /// not decompiled, and the evidence is the menu's alone, so the city and the dungeon keep the
    /// full build's refusal.
    /// </summary>
    public static bool CloseBoxAsksToExit => On;

    /// <summary>
    /// frmMenu's Form_Load ends "If m00A8 = True Then cmdCommand(mc002A).Caption = "Ordering/&amp;About""
    /// (FRMMENU.BAS:162), m00A8 a Static nothing sets in the full build. The shareware's main menu
    /// reads "Ordering/&amp;About" (observed in the original shareware 2026-10-26). The button still
    /// opens frmAbout (FRMMENU.BAS:117), where the limited build shows Ordering Info.
    /// </summary>
    public static bool AboutReadsOrdering => On;

    /// <summary>MAINMOD2.BAS:3998.</summary>
    public const string ChuteCannotBeTaken = "You cannot take this Chute in the Limited Version.";

    /// <summary>MAZEMOD.BAS:1841.</summary>
    public const string ChuteDoesNothing = "There is a chute here, but it doesn't do anything in the Limited Version.";

    /// <summary>MAZEMOD.BAS:1795.</summary>
    public const string TeleporterGoesNowhere = "..but it doesn't go anywhere in the limited version...";

    /// <summary>MAZEMOD.BAS:419.</summary>
    public static string LevelDenied(int level) => "LIMITED VERSION: Request to go to Level " + level + " is Denied.";

    /// <summary>
    /// fn0A0B's clamp (MAZEMOD.BAS:415-422) on a level asked for: past mc015C nothing is loaded and
    /// the level is left as it is; past mc015E (the last level in the limited build) the request is
    /// denied, with its line, and the level becomes mc015E. Returns the level to go to, and the
    /// line or null.
    /// </summary>
    public static (int Level, string? Said) Clamp(int level)
    {
        if (!On || level < 0 || level > DeepestLevel || level <= LastLevel) return (level, null);
        return (LastLevel, LevelDenied(level));
    }

    /// <summary>TAKE on a chute (MAINMOD2.BAS:3997): "If m0622 = True And DungeonLevel = mc0624".</summary>
    public static bool ChuteIsDeadToTake(int level) => On && level == LastLevel;

    /// <summary>A chute walked onto (MAZEMOD.BAS:1840): "If m02A6 = True And DungeonLevel = mc015E".</summary>
    public static bool ChuteIsDeadUnderfoot(int level) => On && level == LastLevel;

    /// <summary>A teleporter record (MAZEMOD.BAS:1794): "If m02A6 = True And l02A4.M5D98 > mc015E", M5D98 being its Z2.</summary>
    public static bool TeleporterIsDead(int z2) => On && z2 > LastLevel;

    /// <summary>
    /// About's date, gc0196 in lblVersion's "Version 1.1 (Last Edited ...)" (FRMABOUT.BAS:23). The
    /// full 1.1 MORDOR.EXE holds "7/20/95" (MAIN.TXT:36; at byte 624144 of that file). The shareware's
    /// MORDOR.EXE (MORDOR11.ZIP), an earlier build, holds "7/6/95" in the same place (byte 623888,
    /// after the same "Welcome to MORDOR 1.1!" and "Mordor" strings) and no "7/20/95" at all, so
    /// the limited build shows that.
    /// </summary>
    public static string LastEdited => On ? SharewareLastEdited : FullLastEdited;

    public const string FullLastEdited = "7/20/95";
    public const string SharewareLastEdited = "7/6/95";

    /// <summary>
    /// frmOrderInfo's caption and its two labels as Form_Load writes them (FRMORDER.BAS:13-36), "&amp;&amp;" as the "&amp;" it shows.
    /// The form is only ever shown by the limited build, and the price is the shareware MORDOR.EXE's:
    /// "...at any time for only $39.95 + S&amp;&amp;H!" (byte 568050 of the MORDOR.EXE in MORDOR11.ZIP),
    /// as its MORDOR.WRI has it too ("You can now order the full version of MORDOR for only $39.95 +
    /// S&amp;H!"). The full 1.1 MORDOR.EXE, which never shows the form, says $39.99 there
    /// (FRMORDER.BAS:18); every other word of the form is the same in both.
    /// </summary>
    public const string OrderTitle = "Mordor Ordering Information";
    public const string OrderInfo =
        "Whether you’re ordering MORDOR for yourself or as a gift, this first of a kind Windows FRP game designed to be played for months and even longer is guaranteed to put a smile on most any avid FRPer’s face!!  "
        + "The full commercial version of MORDOR has so many more Monsters, Items and Dungeon Levels than the PUBLIC version that we can’t even begin to get into detail.  Find out for yourself!\n\n"
        + "  You can now pre-order the full version of MORDOR at any time for only $39.95 + S&H!  Feel free to use your VISA, MASTERCARD, Check, or Money Order to have MORDOR: the Depths of Dejenol, sent directly to your doorstep!\n\n";
    public const string OrderHow =
        "To order, call TDA! at (800) 624-2101 between 9AM and 4PM PST Monday through Friday and ask to order MORDOR!  Be sure to specify either 3.5\" disks or CD-ROM.\n\n"
        + "You can also contact the company at the following EMail addresses:\n"
        + "Internet: 102033.242@compuserve.com\n"
        + "CompuServe: 102033,242\n"
        + "(Remember to include the proper Address & Credit Card information if you order via EMail!!)\n\n"
        + "Street address:\n"
        + "TDA!\n"
        + "720 132nd St SW #202\n"
        + "Everett, WA 98204\n"
        + "(206) 742-4145\n\n"
        + "Order the full version of MORDOR now & see just how far you can take adventure!";
}
