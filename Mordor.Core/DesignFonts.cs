using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// Which of the original's font passes a form goes through. Each reads the font a control was
/// designed in (<see cref="DesignFont"/>) and the Options window's two fonts (<see cref="GameOptions"/>):
/// <list type="bullet">
/// <item><see cref="Restyle"/>: <c>sub0784</c> (MAINMOD.BAS:1862-1898) alone, which the form's load runs -
/// frmCharStat4 (Messages), frmAutoMap, frmInteract, frmPartyOptions' FRMPINFO.</item>
/// <item><see cref="RestyleAndZoom"/>: <c>sub0784</c> at load and then, as the form is shown and
/// every time it is resized, <c>sub07C5</c> (MAINMOD.BAS:1925-1975) or <c>sub0760</c>
/// (1750-1848) - the six services, frmParty, frmCharStat1, frmCharStat3, frmCityMain,
/// frmDungeonMain and frmExchange (whose Form_Load runs sub0784 and then sub07ED).</item>
/// <item><see cref="SizeOnly"/>: <c>sub0802</c> or <c>sub07ED</c> alone (2001-2100): sizes, never faces -
/// frmOptions, frmControlPanel, frmCharList, frmGuildLog, frmLibrary, frmHall, frmOrderInfo,
/// frmCreateChar, frmAbout, frmDirectory.</item>
/// </list>
/// </summary>
public enum FontPass { Restyle, RestyleAndZoom, SizeOnly }

/// <summary>
/// A control's font as its form data in MORDOR.EXE has it: the face, the size in points, bold, and
/// the Tag, whose leading ":" gives the control a size of its own that the zoom passes keep
/// (lblWelcome's ":12"). A control with no font of its own has VB3's default, MS Sans Serif 8.25
/// bold (<see cref="Default"/>), whatever its form's own font: frmCharStat1's header stores Courier
/// New 8.25 bold, and bm13.png shows its unfonted captions in a bold sans serif all the same.
/// </summary>
public readonly record struct DesignFont(string Face, double Points, bool Bold = true, string? Tag = null)
{
    /// <summary>gc0562, the face every "Pro" control is designed in (MAIN.TXT:124).</summary>
    public const string ProFace = "Courier New";
    /// <summary>gc0566, the face every "NonPro" control is designed in, and VB3's default (MAIN.TXT:125).</summary>
    public const string NonProFace = "MS Sans Serif";

    /// <summary>VB3's default font, a form's and a control's with none of its own: MS Sans Serif 8.25 bold.</summary>
    public static readonly DesignFont Default = new(NonProFace, 8.25);

    /// <summary>The same face and size, not bold (a control whose form data clears FontBold).</summary>
    public DesignFont Plain => this with { Bold = false };

    /// <summary>The size a Tag of ":n" holds, or null for none.</summary>
    public double? FixedPoints =>
        Tag is { Length: > 1 } t && t[0] == ':' && double.TryParse(t[1..], NumberStyles.Float, CultureInfo.InvariantCulture, out double p)
            ? p : null;

    public override string ToString() => $"{Face} {Points.ToString(CultureInfo.InvariantCulture)}{(Bold ? " bold" : "")}{(Tag != null ? " " + Tag : "")}";
}

/// <summary>A face and a size in points, what a control is drawn in once the passes have run.</summary>
public readonly record struct ResolvedFont(string Face, double Points)
{
    /// <summary>The size in 96-per-inch pixels, as Windows rounds a point size to a cell height.</summary>
    public double Pixels => FontRule.Pixels(Points);
}

/// <summary>
/// The original's rule for the Options window's fonts. MORDOR.INI [Fonts] holds "Pro" and ProSize
/// (<c>gv1ADC.M41D4</c>, <c>M41E5</c>, Courier New 9 by default) and "NonPro" and NonProSize
/// (<c>M41DB</c>, <c>M41F0</c>, MS Sans Serif 8) (MAINMOD.BAS:1252-1265). Three routines apply them:
/// <code>
/// sub0784 (MAINMOD.BAS:1862-1898), at Form_Load - nothing at all while all four are the defaults;
///   otherwise, for the form and each control but lines and scroll bars:
///     If FontName = "Courier New"   Then FontName = Pro:    FontSize = ProSize
///     If FontName = "MS Sans Serif" Then FontName = NonPro: FontSize = NonProSize
///   (in that order, the second test reading the name the first may have just set)
/// sub07C5 / sub0760 (1925-1975, 1750-1848), as a form is shown and on every resize,
/// sub0802 / sub07ED (2001-2100), once at load:
///     If Tag starts ":"         Then FontSize = Val(Mid(Tag, 2)) * width ratio
///     ElseIf FontName = Pro     Then FontSize = ProSize * width ratio
///     Else                           FontSize = NonProSize * width ratio
/// </code>
/// The width ratio is the port's zoom (a ScaleTransform over the whole drawing), so it is not in
/// the points returned here. So with the defaults a zooming form's Courier New controls are 9
/// points, 12 pixels, and every other control is 8 points, 11 pixels - MS Sans Serif 8 is the same
/// 11-pixel face as 8.25, and Arial keeps its face at NonProSize - and a form that only restyles
/// keeps the sizes it was designed in.
/// </summary>
public static class FontRule
{
    /// <summary>sub0784's early exit: all four settings are the defaults (MAINMOD.BAS:1866-1870).</summary>
    public static bool IsDefault(GameOptions o) =>
        o.ProportionalFont == DesignFont.ProFace && o.NonProportionalFont == DesignFont.NonProFace
        && o.ProportionalSize == GameOptions.DefaultProportionalSize && o.NonProportionalSize == GameOptions.DefaultNonProportionalSize;

    public static ResolvedFont Resolve(GameOptions o, DesignFont d, FontPass pass)
    {
        string face = d.Face;
        double points = d.Points;
        if (pass != FontPass.SizeOnly && !IsDefault(o))
        {
            // sub0784, test by test: VB's "=" on strings is Option Compare Binary, exact
            if (face == DesignFont.ProFace) { face = o.ProportionalFont; points = Sized(points, o.ProportionalSize); }
            if (face == DesignFont.NonProFace) { face = o.NonProportionalFont; points = Sized(points, o.NonProportionalSize); }
        }
        if (pass != FontPass.Restyle)
            points = Sized(points, d.FixedPoints ?? (face == o.ProportionalFont ? o.ProportionalSize : o.NonProportionalSize));
        return new ResolvedFont(face, points);
    }

    /// <summary>
    /// A FontSize assignment: a size of nothing or less (a hand-edited MORDOR.INI's ProSize=0, a
    /// Tag of ":0") is an error in VB, which sub0784 steps over with its On Error Resume Next
    /// (MAINMOD.BAS:1865), and sub07C5 with its <c>LDD52: Resume Next</c> (MAINMOD.BAS:1931,
    /// 1980-1981), so the control keeps the size it had and the face is still set.
    /// </summary>
    private static double Sized(double had, double wanted) => wanted > 0 ? wanted : had;

    /// <summary>
    /// Points to 96-per-inch pixels, rounded as Windows rounds a cell height: 8 and 8.25 are 11, 9 is
    /// 12, 10 is 13. Never below one pixel: a size that rounds to nothing would not be drawn at all,
    /// and Avalonia will not measure text of no size.
    /// </summary>
    public static double Pixels(double points) => Math.Max(1, Math.Round(points * 96 / 72, MidpointRounding.AwayFromZero));
}

/// <summary>
/// The design font of every control whose text the port draws from a form, as the form data in
/// MORDOR.EXE has it, and which pass its form goes through. <c>tools/check.sh</c> (selfcheck) reads
/// each back out of MORDOR.EXE and holds this table to it, and checks that every control of
/// <see cref="ServiceForms"/> not listed here is the default MS Sans Serif 8.25. The port's windows
/// take their fonts from here through Mordor.App's UiFonts.
/// </summary>
public static class DesignFonts
{
    /// <summary>A form: its name in MORDOR.EXE, its RCDATA and its pass.</summary>
    public sealed record Form(string Name, int Resource, FontPass Pass)
    {
        /// <summary>A control of the form with its design font, for <see cref="All"/>.</summary>
        public Entry Control(string key, DesignFont font) => new(this, key, font);
    }

    /// <summary>One control's design font: its key is Name or Name(Index), as the form data names it.</summary>
    public readonly record struct Entry(Form Form, string Control, DesignFont Font);

    public static readonly Form CharStat1 = new("frmCharStat1", 8, FontPass.RestyleAndZoom);
    public static readonly Form CharStat3 = new("frmCharStat3", 10, FontPass.RestyleAndZoom);
    public static readonly Form CharStat4 = new("frmCharStat4", 12, FontPass.Restyle);
    public static readonly Form Main = new("frmMain", 6, FontPass.Restyle);
    public static readonly Form CityMain = new("frmCityMain", 14, FontPass.RestyleAndZoom);
    public static readonly Form CityStore = new("frmCityStore", 16, FontPass.RestyleAndZoom);
    public static readonly Form CityBank = new("frmCityBank", 18, FontPass.RestyleAndZoom);
    public static readonly Form CityGuild = new("frmCityGuild", 20, FontPass.RestyleAndZoom);
    public static readonly Form DungeonMain = new("frmDungeonMain", 22, FontPass.RestyleAndZoom);
    public static readonly Form Interact = new("frmInteract", 26, FontPass.Restyle);
    public static readonly Form CityMorgue = new("frmCityMorgue", 28, FontPass.RestyleAndZoom);
    public static readonly Form Exchange = new("frmExchange", 38, FontPass.RestyleAndZoom);
    public static readonly Form AutoMap = new("frmAutoMap", 40, FontPass.Restyle);
    public static readonly Form Party = new("frmParty", 44, FontPass.RestyleAndZoom);
    public static readonly Form PartyOptions = new("frmPartyOptions", 46, FontPass.Restyle);
    public static readonly Form CitySeer = new("frmCitySeer", 52, FontPass.RestyleAndZoom);
    public static readonly Form CityCStore = new("frmCityCStore", 60, FontPass.RestyleAndZoom);
    public static readonly Form GuildLog = new("frmGuildLog", 48, FontPass.SizeOnly);
    public static readonly Form Library = new("frmLibrary", 56, FontPass.SizeOnly);
    public static readonly Form Hall = new("frmHall", 58, FontPass.SizeOnly);

    // ---- the controls with a font of their own, and the default-font ones the port's text stands for

    /// <summary>frmCityStore's counts, elaItemInfo, and Label9's two headings: Courier New 8.25 bold.</summary>
    public static readonly DesignFont StoreCounts = new(DesignFont.ProFace, 8.25);
    /// <summary>frmCityStore's elaSelectedItem and elaSelectedItemCost, Arial 8.25.</summary>
    public static readonly DesignFont StoreSelected = new("Arial", 8.25, Bold: false);
    /// <summary>The Guild's lblStatName (bold) and lblStat (not), Courier New 8.25.</summary>
    public static readonly DesignFont GuildStatName = new(DesignFont.ProFace, 8.25);
    public static readonly DesignFont GuildStat = new(DesignFont.ProFace, 8.25, Bold: false);
    /// <summary>Each service's welcome, MS Sans Serif 12 bold, Tag ":12".</summary>
    public static readonly DesignFont Welcome = new(DesignFont.NonProFace, 12, Tag: ":12");
    /// <summary>Confinement's elaCompInfo counts and its lblLabel(3) and (4) headings, Courier New 8.25 bold.</summary>
    public static readonly DesignFont CompCounts = new(DesignFont.ProFace, 8.25);
    /// <summary>frmCharStat1's lblStat, the Stats page's numbers: Courier New 8.25.</summary>
    public static readonly DesignFont CharStat = new(DesignFont.ProFace, 8.25, Bold: false);
    /// <summary>frmCharStat3's lblCarryInfo, Arial 8.25 bold.</summary>
    public static readonly DesignFont CarryInfo = new("Arial", 8.25);
    /// <summary>frmDungeonMain's lblDisarm, Courier New 7.5 bold.</summary>
    public static readonly DesignFont Disarm = new(DesignFont.ProFace, 7.5);
    /// <summary>frmMain's picToolbar, which sub08B9 paints the resistances on: Courier New 8.25 bold.</summary>
    public static readonly DesignFont Toolbar = new(DesignFont.ProFace, 8.25);
    /// <summary>frmExchange's pnlName, MS Sans Serif 9.75 bold.</summary>
    public static readonly DesignFont ExchangeName = new(DesignFont.NonProFace, 9.75);
    /// <summary>
    /// frmHall's heading, lblLabel(1): MS Sans Serif 12 bold underlined, with no Tag, so sub0802
    /// sizes it at NonProSize like every other control of the form (FRMHALL.BAS:48).
    /// </summary>
    public static readonly DesignFont HallHeading = new(DesignFont.NonProFace, 12);
    /// <summary>MS Sans Serif 8.25 not bold, a control's own font that only clears FontBold.</summary>
    public static readonly DesignFont PlainSans = DesignFont.Default.Plain;

    /// <summary>Every entry above against its control, and the default-font controls the port's text stands for.</summary>
    public static readonly Entry[] All =
    {
        CityStore.Control("elaItemInfo(0)", StoreCounts),
        CityStore.Control("Label9(0)", StoreCounts),
        CityStore.Control("Label9(1)", StoreCounts),
        CityStore.Control("elaSelectedItem", StoreSelected),
        CityStore.Control("elaSelectedItemCost", StoreSelected),
        CityStore.Control("elaItem(0)", DesignFont.Default),
        CityStore.Control("Label10", DesignFont.Default),
        CityGuild.Control("lblStatName(0)", GuildStatName),
        CityGuild.Control("lblStat(0)", GuildStat),
        CityGuild.Control("lblWelcome", Welcome),
        CityGuild.Control("lstGuilds", PlainSans),
        CityBank.Control("lblLabel(0)", Welcome),
        CityBank.Control("lstItems", DesignFont.Default),
        CityMorgue.Control("Label1", Welcome),
        CitySeer.Control("Label1", Welcome),
        CityCStore.Control("elaCompInfo(0)", CompCounts),
        CityCStore.Control("elaComp(0)", PlainSans),
        CityCStore.Control("lblLabel(3)", CompCounts),
        CityCStore.Control("lblLabel(4)", CompCounts),
        CharStat1.Control("lblStat(0)", CharStat),
        CharStat1.Control("lblStatInfo(0)", DesignFont.Default),
        CharStat1.Control("lblRaceInfo", DesignFont.Default),
        CharStat1.Control("lblGuildInfo", DesignFont.Default),
        CharStat1.Control("lblSpecial(0)", DesignFont.Default),
        CharStat1.Control("elaGroupInfo", PlainSans),
        CharStat1.Control("picTabPicture", DesignFont.Default),
        CharStat1.Control("lblBuffer(0)", PlainSans),
        CharStat1.Control("tabStatTab", PlainSans),
        CharStat3.Control("lblItem(0)", PlainSans),
        CharStat3.Control("lblItemStatus(0)", DesignFont.Default),
        CharStat3.Control("lblName(0)", PlainSans),
        CharStat3.Control("lblLabel(0)", DesignFont.Default),
        CharStat3.Control("lblCarryInfo", CarryInfo),
        CharStat3.Control("lblCompName(0)", PlainSans),
        CharStat3.Control("tabItemTab", PlainSans),
        CharStat3.Control("cmdCommand(0)", PlainSans),
        CharStat4.Control("lstMessages", DesignFont.Default),
        Main.Control("picToolbar", Toolbar),
        Main.Control("lblExperience", PlainSans),
        Main.Control("lblGold", PlainSans),
        CityMain.Control("lblSelection(0)", DesignFont.Default),
        DungeonMain.Control("lblDisarm", Disarm),
        DungeonMain.Control("lblTrap", DesignFont.Default),
        DungeonMain.Control("lblRoom", PlainSans),
        DungeonMain.Control("lblSquareInfo", DesignFont.Default),
        DungeonMain.Control("lblMonster(0)", DesignFont.Default),
        DungeonMain.Control("cmdAction(0)", PlainSans),
        Party.Control("lblName(0)", PlainSans),
        Party.Control("lblLabel(0)", DesignFont.Default),
        Party.Control("cmdCommand(0)", PlainSans),
        AutoMap.Control("lblInfo", DesignFont.Default),
        AutoMap.Control("lblLevel", PlainSans),
        Interact.Control("cmbCharacter", DesignFont.Default),
        Interact.Control("lblQuestion", DesignFont.Default),
        PartyOptions.Control("lstCharacters", PlainSans),
        Exchange.Control("pnlName(0)", ExchangeName),
        Exchange.Control("pnlItem(0)", DesignFont.Default),
        GuildLog.Control("lstLog", PlainSans),
        Library.Control("lstList", DesignFont.Default),
        Hall.Control("lblLabel(1)", HallHeading),
        Hall.Control("lblRecordStat(0)", DesignFont.Default),
        Hall.Control("lblRecordChar(0)", PlainSans),
        Hall.Control("lblHistoryChar(0)", PlainSans),
    };
}
