namespace Mordor.Core;

/// <summary>
/// The character window's Stats page as frmCharStat1's form data in MORDOR.EXE (RCDATA 8, its names
/// in 9) lays it out: separate labels in an Elastic, <c>elaTabFrame(0)</c>, each in a font of its
/// own. The rectangles are twips from the Elastic's corner; <c>selfcheck</c> reads each back out of
/// MORDOR.EXE and holds these to it.
///
/// <code>
///   lblRaceInfo       Male Giant (Neutral)       centred, the form's MS Sans Serif 8.25 bold
///   lblGuildInfo      Gladiator (107)            centred, the same
///   elaGroupInfo      [   You are Alone   ]      an Elastic, MS Sans Serif 8.25 plain
///   lblStatInfo(n)    Age            lblStat(n)  52   captions MS Sans Serif 8.25 bold, the
///                                                     numbers Courier New 8.25, set right
///   elaBorder         ----------------------     the line above the stats
///   lblStatInfo(7..)  Strength  lblStat(7..) 36  lblSpecial(n)  Invisible   red, set right
/// </code>
///
/// Form_Resize places the loaded copies when the form is new (FRMCHAR1.BAS:124-183): each
/// <c>lblStat(n)</c> one height below the one before, half a height more before Spells and Hits
/// and a whole one more before Strength; a <c>lblStatInfo(n)</c> on its <c>lblStat</c>'s row;
/// the six stats' figures 400 twips wide, starting a twip after their caption ends; elaBorder's top
/// <c>lblStat(7).Top - lblStat(7).Height / 1.2</c>; and <c>lblSpecial(n)</c> one height under the
/// one before from <c>lblStat(7)</c>'s row, each in its own place whether the ones above show or not.
/// </summary>
public static class CharStatForm
{
    public const int Resource = 8;

    /// <summary>
    /// <c>elaTabFrame(0)</c>'s corner from picTabPicture's, the page the other tabs paint: the
    /// Elastic is at 195,645 on the form, the picture at 180,510 in tabStatTab at 30,30.
    /// </summary>
    public const int FrameLeft = 195 - (30 + 180), FrameTop = 645 - (30 + 510);

    public static readonly FormRect elaTabFrame0 = new(195, 645, 3540, 4350);   // on the form
    public static readonly FormRect lblRaceInfo = new(45, 60, 3450, 215);
    public static readonly FormRect lblGuildInfo = new(45, 300, 3450, 215);
    public static readonly FormRect elaGroupInfo = new(60, 570, 3450, 225);
    public static readonly FormRect elaBorder = new(75, 1770, 3405, 120);
    public static readonly FormRect lblStatInfo0 = new(270, 885, 1530, 215);
    public static readonly FormRect lblStat0 = new(1965, 885, 1455, 215);
    public static readonly FormRect lblSpecial0 = new(2205, 2260, 1260, 215);

    /// <summary>The captions of lblStatInfo 0 to 6 (the form's "Age" and FRMCHAR1.BAS:135-156).</summary>
    public static readonly string[] Captions = { "Age", "Spells", "Spell Level", "Hits", "Att/Def", "Experience", "Gold" };

    /// <summary>The six stats' figures' width, set by Form_Resize (FRMCHAR1.BAS:161-165).</summary>
    public const int StatFigureWidth = 400;

    /// <summary>
    /// The top of row <paramref name="n"/> (0 Age to 12 Dexterity), in twips in the Elastic, as
    /// Form_Resize stacks them: <c>l0074</c> is half lblStat's height put in an Integer, which VB
    /// rounds to the even 108 (FRMCHAR1.BAS:107).
    /// </summary>
    public static int RowTop(int n)
    {
        int half = (int)Math.Round(lblStat0.Height / 2.0, MidpointRounding.ToEven);
        int top = lblStat0.Top;
        for (int i = 1; i <= n; i++)
            top += lblStat0.Height + (i is 1 or 3 ? half : i == 7 ? half * 2 : 0);
        return top;
    }

    /// <summary>
    /// The left of row <paramref name="n"/>'s figure and its width: lblStat(0)'s for the first seven
    /// (lblStat(6) is moved to lblStat(5)'s Left), 400 wide a twip past the caption for the stats.
    /// </summary>
    public static (int Left, int Width) Figure(int n) => n < 7
        ? (lblStat0.Left, lblStat0.Width)
        : (lblStatInfo0.Left + lblStatInfo0.Width + 1, StatFigureWidth);

    /// <summary>elaBorder's top once Form_Resize has moved it (its <c>P?C005</c>), rounded to a twip as VB stores a Single in an Integer.</summary>
    public static int BorderTop => (int)Math.Round(RowTop(7) - lblStat0.Height / 1.2, MidpointRounding.ToEven);

    /// <summary>lblSpecial(<paramref name="n"/>)'s top: from lblStat(7)'s row down, one height each.</summary>
    public static int SpecialTop(int n) => RowTop(7) + n * lblSpecial0.Height;

    /// <summary>The form's controls, as its data names them, for the check against MORDOR.EXE.</summary>
    public static readonly ServiceForms.Control[] Controls =
    {
        new("elaTabFrame", 0, elaTabFrame0), new("lblRaceInfo", -1, lblRaceInfo), new("lblGuildInfo", -1, lblGuildInfo),
        new("elaGroupInfo", -1, elaGroupInfo), new("elaBorder", -1, elaBorder), new("lblStatInfo", 0, lblStatInfo0),
        new("lblStat", 0, lblStat0), new("lblSpecial", 0, lblSpecial0),
    };
}
