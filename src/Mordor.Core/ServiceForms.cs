namespace Mordor.Core;

/// <summary>
/// A control's rectangle in its form's data: Left, Top, Width and Height in twips (15 to the
/// pixel), measured from the corner of the container it sits in.
/// </summary>
public readonly record struct FormRect(int Left, int Top, int Width, int Height)
{
    public override string ToString() => $"{Left},{Top} {Width}x{Height}";
}

/// <summary>
/// The town's service forms (and the Library, the Guild Log and the Hall of Records, which have no
/// window-table entry) as their form data in MORDOR.EXE lays them out: each form's client
/// (its ScaleWidth and ScaleHeight, 120 twips narrower and 405 shorter than the window table's
/// default for it, <see cref="WindowLayout"/>, which is the form's outer size with its Fixed Double
/// border and caption) and the rectangle of every control the port draws. The resources are
/// RCDATA pairs, the form's data and then the table of its control names; <c>tools/check.sh</c>
/// reads each rectangle back out of MORDOR.EXE and holds these to it.
///
/// Every control but the root sits in a VideoSoft Elastic, <c>elaElastic(0)</c>, which covers the
/// client and a pixel beyond; its rectangle is relative to the client and the rest to their own
/// Elastic. See docs/FIDELITY.md, "The town's service windows are drawn to their forms' data".
/// </summary>
public static class ServiceForms
{
    /// <summary>One control as the form data names it: its Name, its Index in a control array (-1 for none), its rectangle.</summary>
    public readonly record struct Control(string Name, int Index, FormRect Rect);

    /// <summary>frmCityGuild, FRMCTYG.BAS: RCDATA 20, its names in 21.</summary>
    public static class Guild
    {
        public const int Resource = 20;
        public const int ClientW = 5220, ClientH = 3420;
        public static readonly FormRect Elastic = new(-15, -15, 5250, 3450);
        public static readonly FormRect lblWelcome = new(60, 45, 5100, 285);
        public static readonly FormRect GuildMasterCaption = new(165, 420, 1710, 195);   // Label2(1)
        public static readonly FormRect lblGuildMaster = new(165, 630, 3000, 240);
        public static readonly FormRect StatsCaption = new(165, 900, 1710, 210);         // Label2(0)
        public static readonly FormRect StatsBox = new(165, 1125, 3000, 585);            // elaElastic(1)
        public static readonly FormRect lblStatName0 = new(75, 75, 390, 210);            // in elaElastic(1)
        public static readonly FormRect lblStat0 = new(45, 270, 375, 210);               // in elaElastic(1)
        public static readonly FormRect lblWelcomeInfo = new(165, 1740, 3000, 600);
        public static readonly FormRect GuildsCaption = new(3375, 420, 1710, 195);       // Label2(2)
        public static readonly FormRect lstGuilds = new(3360, 630, 1755, 1980);
        public static readonly FormRect cmdMake = new(180, 2415, 1450, 300);
        public static readonly FormRect cmdInfo = new(1725, 2415, 1450, 300);
        public static readonly FormRect cmdVisit = new(3375, 2715, 1755, 285);
        public static readonly FormRect cmdJoin = new(180, 2730, 1455, 300);
        public static readonly FormRect cmdLog = new(1725, 2730, 1455, 300);
        public static readonly FormRect cmdLibrary = new(180, 3060, 3000, 300);
        public static readonly FormRect cmdExit = new(4050, 3045, 1080, 315);

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("elaElastic", 1, StatsBox),
            new("lblWelcome", -1, lblWelcome), new("Label2", 1, GuildMasterCaption),
            new("lblGuildMaster", -1, lblGuildMaster), new("Label2", 0, StatsCaption),
            new("lblStatName", 0, lblStatName0), new("lblStat", 0, lblStat0),
            new("lblWelcomeInfo", -1, lblWelcomeInfo), new("Label2", 2, GuildsCaption),
            new("lstGuilds", -1, lstGuilds), new("cmdMake", -1, cmdMake), new("cmdInfo", -1, cmdInfo),
            new("cmdVisit", -1, cmdVisit), new("cmdJoin", -1, cmdJoin), new("cmdLog", -1, cmdLog),
            new("cmdLibrary", -1, cmdLibrary), new("cmdExit", -1, cmdExit),
        };
    }

    /// <summary>frmCityBank, FRMCITYB.BAS: RCDATA 18, its names in 19.</summary>
    public static class Bank
    {
        public const int Resource = 18;
        public const int ClientW = 6195, ClientH = 3945;
        public static readonly FormRect Elastic = new(0, -15, 6210, 3960);
        public static readonly FormRect Welcome = new(1650, 30, 2940, 300);              // lblLabel(0)
        public static readonly FormRect AccountCaption = new(105, 330, 1485, 210);       // lblLabel(1)
        public static readonly FormRect StatusBox = new(120, 585, 5970, 300);            // elaElastic(1)
        public static readonly FormRect lblStatus = new(75, 45, 5850, 225);              // in elaElastic(1)
        public static readonly FormRect DepositCaption = new(195, 990, 1485, 210);       // lblLabel(2)
        public static readonly FormRect DepositAmount = new(210, 1245, 1845, 300);       // txtAmount(0)
        public static readonly FormRect DepositAll = new(2130, 1245, 555, 300);          // cmdCommand(0)
        public static readonly FormRect WithdrawCaption = new(210, 1755, 1485, 210);     // lblLabel(3)
        public static readonly FormRect WithdrawAmount = new(210, 2010, 1845, 300);      // txtAmount(1)
        public static readonly FormRect WithdrawAll = new(2130, 2010, 555, 300);         // cmdCommand(1)
        public static readonly FormRect PartyCaption = new(105, 2475, 1485, 210);        // lblLabel(4)
        public static readonly FormRect PartyBox = new(120, 2730, 2625, 780);            // elaElastic(2)
        public static readonly FormRect PoolDeposit = new(60, 60, 1215, 300);            // cmdCommand(2), in elaElastic(2)
        public static readonly FormRect PartyDeposit = new(1350, 60, 1215, 300);         // cmdCommand(3), in elaElastic(2)
        public static readonly FormRect PartyPoolDeposit = new(60, 405, 2505, 300);      // cmdCommand(4), in elaElastic(2)
        public static readonly FormRect ItemsCaption = new(2835, 990, 2115, 210);        // lblLabel(5)
        public static readonly FormRect ItemsBox = new(2850, 1230, 3075, 2280);          // elaElastic(3)
        public static readonly FormRect lstItems = new(60, 60, 2940, 1785);              // in elaElastic(3)
        public static readonly FormRect cmdInfo = new(45, 1920, 1530, 300);              // in elaElastic(3)
        public static readonly FormRect cmdExit = new(4425, 3585, 1530, 330);

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("lblLabel", 0, Welcome), new("lblLabel", 1, AccountCaption),
            new("elaElastic", 1, StatusBox), new("lblStatus", -1, lblStatus),
            new("lblLabel", 2, DepositCaption), new("txtAmount", 0, DepositAmount), new("cmdCommand", 0, DepositAll),
            new("lblLabel", 3, WithdrawCaption), new("txtAmount", 1, WithdrawAmount), new("cmdCommand", 1, WithdrawAll),
            new("lblLabel", 4, PartyCaption), new("elaElastic", 2, PartyBox), new("cmdCommand", 2, PoolDeposit),
            new("cmdCommand", 3, PartyDeposit), new("cmdCommand", 4, PartyPoolDeposit),
            new("lblLabel", 5, ItemsCaption), new("elaElastic", 3, ItemsBox), new("lstItems", -1, lstItems),
            new("cmdInfo", -1, cmdInfo), new("cmdExit", -1, cmdExit),
        };
    }

    /// <summary>frmCityMorgue, FRMMORG.BAS: RCDATA 28, its names in 29.</summary>
    public static class Morgue
    {
        public const int Resource = 28;
        public const int ClientW = 3480, ClientH = 1905;
        public static readonly FormRect Elastic = new(0, -15, 3495, 1935);
        public static readonly FormRect Welcome = new(45, 60, 3195, 330);                // Label1
        public static readonly FormRect RaiseCharacter = new(135, 465, 1590, 300);       // cmdCommand(0)
        public static readonly FormRect HireRescuers = new(1770, 465, 1590, 300);        // cmdCommand(3)
        public static readonly FormRect GrabBody = new(135, 810, 1590, 300);             // cmdCommand(1)
        public static readonly FormRect RaiseCompanion = new(135, 1170, 3225, 300);      // cmdCommand(2)
        public static readonly FormRect cmdExit = new(2265, 1560, 1095, 300);

        public static readonly Control[] Controls =
        {
            new("elaElastic", -1, Elastic), new("Label1", -1, Welcome), new("cmdCommand", 0, RaiseCharacter),
            new("cmdCommand", 3, HireRescuers), new("cmdCommand", 1, GrabBody), new("cmdCommand", 2, RaiseCompanion),
            new("cmdExit", -1, cmdExit),
        };
    }

    /// <summary>frmCityCStore, Confinement &amp; Holding, FRMCITYC.BAS: RCDATA 60, its names in 61.</summary>
    public static class Confinement
    {
        public const int Resource = 60;
        public const int ClientW = 6510, ClientH = 4080;
        public static readonly FormRect Elastic = new(-30, -15, 6540, 4395);
        public static readonly FormRect BuyCaption = new(3015, 75, 1575, 225);           // lblLabel(0)
        public static readonly FormRect BindCaption = new(105, 75, 1575, 225);           // lblLabel(1)
        public static readonly FormRect IdCaption = new(120, 1440, 1575, 405);           // lblLabel(2)
        public static readonly FormRect cmdExit = new(5130, 3720, 1305, 330);
        // elaElastic(3), Bind Companions
        public static readonly FormRect BindBox = new(120, 330, 2805, 780);
        public static readonly FormRect BindComp = new(30, 120, 570, 210);               // lblInfo(6)
        public static readonly FormRect txtBind = new(660, 75, 2055, 285);
        public static readonly FormRect BindCost = new(90, 450, 450, 210);               // lblInfo(0)
        public static readonly FormRect lblBindCost = new(645, 435, 1170, 255);
        public static readonly FormRect cmdBind = new(1920, 420, 795, 300);
        // elaElastic(2), Identify, Realign & Sell
        public static readonly FormRect IdBox = new(120, 1890, 2760, 1785);
        public static readonly FormRect IdComp = new(60, 120, 540, 210);                 // lblLabel(8)
        public static readonly FormRect txtID = new(630, 75, 2070, 285);
        public static readonly FormRect lblValueNum = new(15, 450, 1065, 195);
        public static readonly FormRect lblValue = new(1140, 420, 1545, 255);
        public static readonly FormRect cmdRealign0 = new(90, 720, 255, 255);            // G; N and E are 240 twips on
        public static readonly FormRect cmdRealign1 = new(330, 720, 255, 255);
        public static readonly FormRect cmdRealign2 = new(570, 720, 255, 255);
        public static readonly FormRect cmdIDInfo = new(1140, 720, 750, 300);
        public static readonly FormRect cmdSell = new(1935, 720, 750, 300);
        public static readonly FormRect lblIDLevel = new(45, 1140, 1845, 210);
        public static readonly FormRect cmdID = new(1950, 1065, 720, 300);
        public static readonly FormRect IdCostCaption = new(285, 1440, 780, 195);       // lblLabel(9)
        public static readonly FormRect lblIDCost = new(1125, 1410, 1545, 255);
        // elaElastic(1), Buy Companions
        public static readonly FormRect BuyBox = new(3030, 315, 3390, 3360);
        public static readonly FormRect CreatureHead = new(120, 30, 915, 195);           // lblLabel(3)
        public static readonly FormRect GneHead = new(2370, 30, 585, 195);               // lblLabel(4)
        public static readonly FormRect elaComp0 = new(90, 270, 2205, 225);              // the rows, each 225 below (FRMCITYC.BAS:498-503)
        public static readonly FormRect elaCompInfo0 = new(2370, 270, 585, 225);
        public static readonly FormRect vsbComps = new(3030, 255, 270, 2100);
        public static readonly FormRect elaAlignments = new(420, 780, 1905, 690);
        public static readonly FormRect SelectAlignment = new(165, 60, 1590, 195);      // Label11, in elaAlignments
        public static readonly FormRect cmdAlign0 = new(300, 285, 375, 300);             // in elaAlignments
        public static readonly FormRect cmdAlign1 = new(750, 285, 375, 300);
        public static readonly FormRect cmdAlign2 = new(1200, 285, 375, 300);
        public static readonly FormRect CompanionCaption = new(60, 2430, 930, 195);      // lblLabel(5)
        public static readonly FormRect lblSelectedComp = new(1050, 2400, 2265, 255);
        public static readonly FormRect lblCost = new(120, 2730, 870, 195);
        public static readonly FormRect lblSelectedCompCost = new(1050, 2700, 990, 255);
        public static readonly FormRect cmdBuy = new(2085, 2700, 600, 255);
        public static readonly FormRect cmdInfo = new(2715, 2700, 600, 255);
        public static readonly FormRect SearchCaption = new(135, 3045, 1605, 210);       // lblLabel(7)
        public static readonly FormRect txtSearch = new(1710, 3000, 1605, 285);

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("lblLabel", 0, BuyCaption), new("lblLabel", 1, BindCaption),
            new("lblLabel", 2, IdCaption), new("cmdExit", -1, cmdExit),
            new("elaElastic", 3, BindBox), new("lblInfo", 6, BindComp), new("txtBind", -1, txtBind),
            new("lblInfo", 0, BindCost), new("lblBindCost", -1, lblBindCost), new("cmdBind", -1, cmdBind),
            new("elaElastic", 2, IdBox), new("lblLabel", 8, IdComp), new("txtID", -1, txtID),
            new("lblValueNum", -1, lblValueNum), new("lblValue", -1, lblValue),
            new("cmdRealign", 0, cmdRealign0), new("cmdRealign", 1, cmdRealign1), new("cmdRealign", 2, cmdRealign2),
            new("cmdIDInfo", -1, cmdIDInfo), new("cmdSell", -1, cmdSell), new("lblIDLevel", -1, lblIDLevel),
            new("cmdID", -1, cmdID), new("lblLabel", 9, IdCostCaption), new("lblIDCost", -1, lblIDCost),
            new("elaElastic", 1, BuyBox), new("lblLabel", 3, CreatureHead), new("lblLabel", 4, GneHead),
            new("elaComp", 0, elaComp0), new("elaCompInfo", 0, elaCompInfo0), new("vsbComps", -1, vsbComps),
            new("elaAlignments", -1, elaAlignments), new("Label11", -1, SelectAlignment),
            new("cmdAlign", 0, cmdAlign0), new("cmdAlign", 1, cmdAlign1), new("cmdAlign", 2, cmdAlign2),
            new("lblLabel", 5, CompanionCaption), new("lblSelectedComp", -1, lblSelectedComp),
            new("lblCost", -1, lblCost), new("lblSelectedCompCost", -1, lblSelectedCompCost),
            new("cmdBuy", -1, cmdBuy), new("cmdInfo", -1, cmdInfo), new("lblLabel", 7, SearchCaption),
            new("txtSearch", -1, txtSearch),
        };
    }

    /// <summary>frmCityStore, the General Store, FRMCTYS.BAS: RCDATA 16, its names in 17.</summary>
    public static class Store
    {
        public const int Resource = 16;
        public const int ClientW = 7170, ClientH = 4380;
        public static readonly FormRect Elastic = new(-15, -15, 7200, 4410);
        public static readonly FormRect BuyCaption = new(3060, 90, 1215, 240);           // Label1(0)
        public static readonly FormRect IdCaption = new(135, 2280, 2550, 240);           // Label1(1)
        public static readonly FormRect UncurseCaption = new(135, 90, 1365, 210);        // Label1(2)
        public static readonly FormRect CombineCaption = new(135, 1155, 1410, 210);      // Label1(3)
        public static readonly FormRect cmdExit = new(6015, 3930, 1050, 300);
        // elaElastic(1), Uncurse Items
        public static readonly FormRect UncurseBox = new(120, 315, 2805, 810);
        public static readonly FormRect UncurseItem = new(60, 150, 450, 180);            // Label2(0)
        public static readonly FormRect txtUncurse = new(570, 105, 2160, 285);
        public static readonly FormRect UncurseCost = new(60, 495, 450, 165);            // Label3
        public static readonly FormRect lblUncurse = new(570, 465, 1170, 255);
        public static readonly FormRect cmdUncurse = new(1800, 465, 930, 255);
        // elaElastic(2), Combine Items
        public static readonly FormRect CombineBox = new(120, 1365, 2805, 840);
        public static readonly FormRect CombineItem = new(75, 135, 465, 180);            // Label2(1)
        public static readonly FormRect txtCombine = new(570, 120, 2160, 285);
        public static readonly FormRect CombineItems = new(75, 510, 465, 165);           // Label6
        public static readonly FormRect lblCombine = new(585, 480, 1290, 255);
        public static readonly FormRect cmdCombine = new(1950, 480, 780, 255);           // cmdCommand(0)
        // elaElastic(3), Identify, Realign & Sell Items
        public static readonly FormRect IdBox = new(120, 2520, 2805, 1800);
        public static readonly FormRect IdItem = new(30, 165, 510, 180);                 // Label2(2)
        public static readonly FormRect txtID = new(600, 120, 2130, 285);
        public static readonly FormRect lblValueNum = new(45, 495, 1065, 195);
        public static readonly FormRect lblValue = new(1170, 465, 1545, 255);
        public static readonly FormRect cmdRealign0 = new(90, 765, 255, 255);
        public static readonly FormRect cmdRealign1 = new(330, 765, 255, 255);
        public static readonly FormRect cmdRealign2 = new(570, 765, 255, 255);
        public static readonly FormRect cmdIDInfo = new(1170, 765, 750, 255);
        public static readonly FormRect cmdSell = new(1965, 765, 750, 255);
        public static readonly FormRect lblIDLevel = new(75, 1185, 1845, 210);
        public static readonly FormRect cmdID = new(1980, 1155, 720, 255);
        public static readonly FormRect IdCostCaption = new(315, 1485, 780, 195);       // Label5
        public static readonly FormRect lblIDCost = new(1155, 1455, 1545, 255);
        // elaElastic(4), Buy Items
        public static readonly FormRect BuyBox = new(3045, 315, 4020, 3555);
        public static readonly FormRect ItemHead = new(135, 75, 570, 180);               // Label9(0)
        public static readonly FormRect UgneHead = new(2790, 75, 810, 195);              // Label9(1)
        public static readonly FormRect elaItem0 = new(105, 300, 2655, 210);             // nine rows, each 210 below
        public static readonly FormRect elaItemInfo0 = new(2835, 300, 780, 210);
        public static readonly FormRect vsbItems = new(3690, 285, 270, 2115);
        public static readonly FormRect elaAlignments = new(930, 1005, 1905, 675);
        public static readonly FormRect SelectAlignment = new(90, 60, 1755, 195);       // Label11, in elaAlignments
        public static readonly FormRect cmdAlign0 = new(90, 285, 375, 300);              // U, in elaAlignments
        public static readonly FormRect cmdAlign1 = new(525, 285, 375, 300);             // G
        public static readonly FormRect cmdAlign2 = new(960, 285, 375, 300);             // N
        public static readonly FormRect cmdAlign3 = new(1425, 285, 375, 300);            // E
        public static readonly FormRect SelectedItemCaption = new(75, 2475, 510, 195);  // Label12(0)
        public static readonly FormRect elaSelectedItem = new(675, 2460, 3000, 225);
        public static readonly FormRect lblCost = new(90, 2775, 870, 195);
        public static readonly FormRect elaSelectedItemCost = new(1080, 2760, 1275, 225);
        public static readonly FormRect cmdBuy = new(2445, 2745, 600, 255);
        public static readonly FormRect cmdInfo = new(3090, 2745, 600, 255);
        public static readonly FormRect SearchCaption = new(150, 3210, 1605, 210);       // Label10
        public static readonly FormRect txtSearch = new(1725, 3165, 1995, 285);

        /// <summary>The Buy Items list drawn as one grid: the headers' top to the ninth row's bottom, the rows' left to vsbItems' right.</summary>
        public static FormRect ListArea => new(elaItem0.Left, ItemHead.Top,
                                                vsbItems.Left + vsbItems.Width - elaItem0.Left, elaItem0.Top + 9 * elaItem0.Height - ItemHead.Top);

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("Label1", 0, BuyCaption), new("Label1", 1, IdCaption),
            new("Label1", 2, UncurseCaption), new("Label1", 3, CombineCaption), new("cmdExit", -1, cmdExit),
            new("elaElastic", 1, UncurseBox), new("Label2", 0, UncurseItem), new("txtUncurse", -1, txtUncurse),
            new("Label3", -1, UncurseCost), new("lblUncurse", -1, lblUncurse), new("cmdUncurse", -1, cmdUncurse),
            new("elaElastic", 2, CombineBox), new("Label2", 1, CombineItem), new("txtCombine", -1, txtCombine),
            new("Label6", -1, CombineItems), new("lblCombine", -1, lblCombine), new("cmdCommand", 0, cmdCombine),
            new("elaElastic", 3, IdBox), new("Label2", 2, IdItem), new("txtID", -1, txtID),
            new("lblValueNum", -1, lblValueNum), new("lblValue", -1, lblValue),
            new("cmdRealign", 0, cmdRealign0), new("cmdRealign", 1, cmdRealign1), new("cmdRealign", 2, cmdRealign2),
            new("cmdIDInfo", -1, cmdIDInfo), new("cmdSell", -1, cmdSell), new("lblIDLevel", -1, lblIDLevel),
            new("cmdID", -1, cmdID), new("Label5", -1, IdCostCaption), new("lblIDCost", -1, lblIDCost),
            new("elaElastic", 4, BuyBox), new("Label9", 0, ItemHead), new("Label9", 1, UgneHead),
            new("elaItem", 0, elaItem0), new("elaItemInfo", 0, elaItemInfo0), new("vsbItems", -1, vsbItems),
            new("elaAlignments", -1, elaAlignments), new("Label11", -1, SelectAlignment),
            new("cmdAlign", 0, cmdAlign0), new("cmdAlign", 1, cmdAlign1), new("cmdAlign", 2, cmdAlign2), new("cmdAlign", 3, cmdAlign3),
            new("Label12", 0, SelectedItemCaption), new("elaSelectedItem", -1, elaSelectedItem), new("lblCost", -1, lblCost),
            new("elaSelectedItemCost", -1, elaSelectedItemCost), new("cmdBuy", -1, cmdBuy), new("cmdInfo", -1, cmdInfo),
            new("Label10", -1, SearchCaption), new("txtSearch", -1, txtSearch),
        };
    }

    /// <summary>frmCitySeer, FRMSEEK.BAS: RCDATA 52, its names in 53.</summary>
    public static class Seer
    {
        public const int Resource = 52;
        public const int ClientW = 5025, ClientH = 1755;
        public static readonly FormRect Elastic = new(-15, -15, 5055, 1785);
        public static readonly FormRect Welcome = new(90, 60, 4905, 300);                // Label1
        public static readonly FormRect OptionsCaption = new(150, 405, 1725, 210);       // lblLabel
        public static readonly FormRect OptionsBox = new(150, 645, 4800, 510);           // elaElastic(1)
        public static readonly FormRect Character = new(60, 75, 1500, 360);              // cmdCommand(0), in elaElastic(1)
        public static readonly FormRect Monster = new(1650, 75, 1500, 360);              // cmdCommand(2), in elaElastic(1)
        public static readonly FormRect Item = new(3240, 75, 1500, 360);                 // cmdCommand(3), in elaElastic(1)
        public static readonly FormRect Exit = new(3765, 1335, 1185, 360);               // cmdCommand(1)

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("Label1", -1, Welcome), new("lblLabel", -1, OptionsCaption),
            new("elaElastic", 1, OptionsBox), new("cmdCommand", 0, Character), new("cmdCommand", 2, Monster),
            new("cmdCommand", 3, Item), new("cmdCommand", 1, Exit),
        };
    }

    /// <summary>
    /// frmLibrary, FRMLIBRA.BAS: RCDATA 56, its names in 57. Shown modally by the Guild's Visit the
    /// Library (FRMCTYG.BAS:170-172); it has no entry in the window table.
    /// </summary>
    public static class Library
    {
        public const int Resource = 56;
        public const int ClientW = 7710, ClientH = 4410;
        public static readonly FormRect Elastic = new(-15, -15, 7740, 4440);
        public static readonly FormRect BrowseCaption = new(90, 90, 1245, 225);          // Label1, navy
        public static readonly FormRect OptionsBox = new(105, 345, 1215, 645);           // elaElastic(1)
        public static readonly FormRect optMonsters = new(45, 45, 1100, 270);            // optOption(1), Value True; in elaElastic(1)
        public static readonly FormRect optItems = new(45, 330, 1100, 270);              // optOption(0), in elaElastic(1)
        public static readonly FormRect picMonster = new(1560, 105, 900, 900);
        public static readonly FormRect lstList = new(90, 1095, 2370, 2175);             // Sorted
        public static readonly FormRect txtInfo = new(2565, 90, 5070, 3795);             // MultiLine, ScrollBars 2
        public static readonly FormRect SearchCaption = new(90, 3345, 1305, 210);        // lblLabel, "Search For:"
        public static readonly FormRect txtSearch = new(105, 3600, 2385, 285);
        public static readonly FormRect lblEntries = new(75, 4050, 6015, 210);           // navy, Alignment 1
        public static readonly FormRect cmdExit = new(6165, 3990, 1485, 315);            // cmdCommand(0), "Exit", Cancel

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("elaElastic", 1, OptionsBox),
            new("optOption", 1, optMonsters), new("optOption", 0, optItems),
            new("cmdCommand", 0, cmdExit), new("lstList", -1, lstList), new("txtInfo", -1, txtInfo),
            new("txtSearch", -1, txtSearch), new("picMonster", -1, picMonster), new("Label1", -1, BrowseCaption),
            new("lblEntries", -1, lblEntries), new("lblLabel", -1, SearchCaption),
        };
    }

    /// <summary>
    /// frmGuildLog, FRMGLDL.BAS: RCDATA 48, its names in 49. Shown modally by the Guild's Read Guild
    /// Log (FRMCTYG.BAS:174-177); it has no entry in the window table. Its one Elastic is no array.
    /// </summary>
    public static class GuildLog
    {
        public const int Resource = 48;
        public const int ClientW = 7755, ClientH = 2490;
        public static readonly FormRect Elastic = new(-15, -15, 7785, 2520);
        public static readonly FormRect lstLog = new(90, 75, 7620, 1980);                // MS Sans Serif 8.25 plain
        public static readonly FormRect lblEntries = new(90, 2115, 1560, 225);
        public static readonly FormRect cmdClean = new(4305, 2125, 1755, 315);           // "&Clean Guild Log", Visible False
        public static readonly FormRect cmdDone = new(6150, 2125, 1560, 315);            // "&Done"

        public static readonly Control[] Controls =
        {
            new("elaElastic", -1, Elastic), new("cmdClean", -1, cmdClean), new("lstLog", -1, lstLog),
            new("cmdDone", -1, cmdDone), new("lblEntries", -1, lblEntries),
        };
    }

    /// <summary>
    /// frmHall, the Hall of Records, FRMHALL.BAS: RCDATA 58, its names in 59. The main menu's, shown
    /// modally by its cmdCommand(10); it has no entry in the window table.
    /// </summary>
    public static class Hall
    {
        public const int Resource = 58;
        public const int ClientW = 7665, ClientH = 5055;
        public static readonly FormRect Elastic = new(-15, -15, 7695, 5085);
        public static readonly FormRect Heading = new(195, 45, 7305, 330);               // lblLabel(1), 12 bold underlined, centred
        public static readonly FormRect RecordsBox = new(195, 390, 3510, 4200);          // elaElastic(1), pale yellow
        public static readonly FormRect lblRecordStat0 = new(60, 15, 3450, 240);         // in elaElastic(1)
        public static readonly FormRect lblRecordChar0 = new(30, 255, 3435, 435);        // in elaElastic(1), navy, 8.25 plain, right
        public static readonly FormRect HistoryBox = new(3930, 390, 3555, 4200);         // elaElastic(2), pale yellow
        public static readonly FormRect lblHistoryStat0 = new(60, 15, 3465, 240);        // in elaElastic(2)
        public static readonly FormRect lblHistoryChar0 = new(60, 255, 3435, 435);       // in elaElastic(2), navy, 8.25 plain, right
        public static readonly FormRect cmdExit = new(5415, 4665, 2085, 345);            // cmdCommand(0), "E&xit", Default and Cancel

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("elaElastic", 2, HistoryBox),
            new("lblHistoryChar", 0, lblHistoryChar0), new("lblHistoryStat", 0, lblHistoryStat0),
            new("elaElastic", 1, RecordsBox), new("lblRecordStat", 0, lblRecordStat0), new("lblRecordChar", 0, lblRecordChar0),
            new("cmdCommand", 0, cmdExit), new("lblLabel", 1, Heading),
        };
    }

    /// <summary>
    /// The Companions page of Objects &amp; Spells, frmCharStat3 (FRMCHAR3.BAS): RCDATA 10, its names
    /// in 11. The page is elaTabFrame(2); lblCarryInfo, elaElastic and cmdCompanion(0) sit on it, and
    /// the four slots in elaElastic, each a 750-twip picCompanion (the sheet's 60-pixel cell drawn
    /// in 50 pixels) with its lblCompName and lblCompHits beside it - to the right of the left-hand
    /// pictures (0 and 2), left-aligned, and to the left of the right-hand ones (1 and 3),
    /// right-justified. Form_Load loads cmdCompanion(1..3) on (0) and puts each of 2, 1 and 0 a
    /// button's width left of the next (FRMCHAR3.BAS:384-394), so (3) stands where (0) was designed.
    /// </summary>
    public static class Companions
    {
        public const int Resource = 10;
        public const int ClientW = 4065, ClientH = 3105;
        public static readonly FormRect Elastic = new(60, 270, 3705, 2205);              // elaElastic(0), on the page
        public static readonly FormRect CarryInfo = new(90, 30, 3330, 210);              // lblCarryInfo, on the page
        public static readonly FormRect Kick0 = new(3495, 30, 270, 255);                 // cmdCompanion(0) "1", on the page
        public static readonly FormRect Picture0 = new(60, 45, 750, 750);                // picCompanion(0..3), in elaElastic
        public static readonly FormRect Picture1 = new(2910, 300, 750, 750);
        public static readonly FormRect Picture2 = new(60, 1125, 750, 750);
        public static readonly FormRect Picture3 = new(2910, 1395, 750, 750);
        public static readonly FormRect Name0 = new(885, 60, 1995, 225);                 // lblCompName(0..3), 8.25 plain
        public static readonly FormRect Name1 = new(840, 600, 1995, 225);                // right-justified
        public static readonly FormRect Name2 = new(885, 1140, 1995, 225);
        public static readonly FormRect Name3 = new(840, 1680, 1995, 225);               // right-justified
        public static readonly FormRect Hits0 = new(900, 315, 795, 225);                 // lblCompHits(0..3), the form's bold
        public static readonly FormRect Hits1 = new(2025, 855, 810, 225);                // right-justified
        public static readonly FormRect Hits2 = new(900, 1395, 810, 225);
        public static readonly FormRect Hits3 = new(2055, 1935, 780, 225);               // right-justified

        /// <summary>The right-hand slots' labels are right-justified (their Alignment, 1).</summary>
        public static bool RightHand(int slot) => (slot & 1) == 1;

        public static readonly Control[] Controls =
        {
            new("elaElastic", 0, Elastic), new("lblCarryInfo", -1, CarryInfo), new("cmdCompanion", 0, Kick0),
            new("picCompanion", 0, Picture0), new("picCompanion", 1, Picture1), new("picCompanion", 2, Picture2), new("picCompanion", 3, Picture3),
            new("lblCompName", 0, Name0), new("lblCompName", 1, Name1), new("lblCompName", 2, Name2), new("lblCompName", 3, Name3),
            new("lblCompHits", 0, Hits0), new("lblCompHits", 1, Hits1), new("lblCompHits", 2, Hits2), new("lblCompHits", 3, Hits3),
        };
    }
}
