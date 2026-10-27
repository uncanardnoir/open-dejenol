using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The game files (src/Mordor.Core/GameFiles.cs, LimitedVersion.cs, Saves.cs; docs/GAME_FILES.md):
    /// the game folder given here is the full game; scratch copies with a file gone, or with a
    /// damaged or foreign file, are refused; copies cut to the shareware's size are played, as the
    /// limited build; the limited build's behaviours are on for those and off for the full game;
    /// the launch settings keep the chosen folder; and the saves folder is found as it should be.
    /// Only scratch copies are written - never the game folder or the saves.
    /// </summary>
    /// <summary>The order form's sentence with the price in it, from "  You can now" to "doorstep!".</summary>
    private static string OrderSentence(string orderInfo)
    {
        int s = orderInfo.IndexOf("  You can now", StringComparison.Ordinal);
        int e = orderInfo.IndexOf("doorstep!", StringComparison.Ordinal);
        return s < 0 || e < s ? "" : orderInfo[s..(e + "doorstep!".Length)];
    }

    private static void GameFilesChecks(string dir)
    {
        Console.WriteLine("\nthe game files and the limited build (GameFiles, GameFolder, LimitedVersion, Saves; docs/GAME_FILES.md)");

        // ---- the evidence, still where the docs say it is ----
        Section("the evidence in the decompiled source (MAZEMOD, FRMMAIN, MAINMOD2, FRMABOUT, FRMORDER)", () =>
        {
            string maze = PortSource("decompiled-named", "MAZEMOD.BAS");
            string fn0A0B = Between(maze, "Function fn0A0B (", "End Function");
            Check("fn0A0B refuses a level above mc015C, which is 15 (MAZEMOD.BAS)", fn0A0B.Contains("' Const mc015C = 15 ") && fn0A0B.Contains("DungeonLevel > mc015C Then"), true);
            Check("and its LIMITED VERSION clamp mc015E is 15 too in this build", fn0A0B.Contains("' Const mc015E = 15 ") && fn0A0B.Contains("DungeonLevel > mc015E Then"), true);
            Check("GameFiles.FullLevels is that 15", GameFiles.FullLevels, 15);
            string frmMain = PortSource("decompiled-named", "FRMMAIN.BAS");
            Check("the (Limited Version) caption hangs on m009C, a constant 0 in this build (FRMMAIN.BAS)",
                  frmMain.Contains("Const m009C = 0 ") && frmMain.Contains("If  m009C = True Then") && frmMain.Contains("\" (Limited Version)\""), true);
            Check("and the goodbye box on m009C = False (FRMMAIN.BAS:240)", frmMain.Contains("If  m009C = False Then") && frmMain.Contains("THANK YOU FOR PLAYING MORDOR!"), true);
            string main2 = PortSource("decompiled-named", "MAINMOD2.BAS");
            Check("TAKE on a chute hangs on m0622 and mc0624, 0 and 15 in this build (MAINMOD2.BAS:224-225, 3997)",
                  main2.Contains("Const m0622 = 0 ") && main2.Contains("Const mc0624 = 15 ")
                  && main2.Contains("If  m0622 = True And Chars(m00A6).DungeonLevel = mc0624 Then"), true);
            Check("and Sub Main's Public Version box on m0622 (MAINMOD2.BAS:6455)", main2.Contains("If  m0622 Then sub10F8"), true);
            Check("the chute and teleporter lines underfoot hang on m02A6 and mc015E (MAZEMOD.BAS:1794, 1840)",
                  maze.Contains("If  m02A6 = True And l02A4.M5D98 > mc015E Then") && maze.Contains("If  m02A6 = True And Chars(m00C0).DungeonLevel = mc015E Then"), true);
            string about = PortSource("decompiled-named", "FRMABOUT.BAS");
            Check("About's Ordering Info hangs on m0032 and opens frmOrderInfo (FRMABOUT.BAS)",
                  about.Contains("If  m0032 = True Then cmdInfo.Visible = True") && about.Contains("frmOrderInfo.Show"), true);
            // the limited build's lines, word for word as the BAS has them
            Check("LIMITED VERSION line as fn0A0B has it", fn0A0B.Contains("\"LIMITED VERSION: Request to go to Level \"") && LimitedVersion.LevelDenied(4) == "LIMITED VERSION: Request to go to Level 4 is Denied.", true);
            Check("the dead TAKE line as MAINMOD2 has it", main2.Contains("\"" + LimitedVersion.ChuteCannotBeTaken + "\""), true);
            Check("the dead chute line as MAZEMOD has it", maze.Contains("\"" + LimitedVersion.ChuteDoesNothing + "\""), true);
            Check("the dead teleporter line as MAZEMOD has it", maze.Contains("\"" + LimitedVersion.TeleporterGoesNowhere + "\""), true);
            Check("the Public Version box as sub10F8 has it, gc0186 = \"1.1\"",
                  main2.Contains("\"Public Version\"") && main2.Contains("\"Order the full commercial release now for hundreds of additional monsters, items and over a dozen more levels to explore!\"")
                  && LimitedVersion.PublicBoxText.StartsWith("This is the PUBLIC version of Mordor 1.1, so please feel free to give it to your friends", StringComparison.Ordinal), true);
            // sub03E3's notice: on gv0180, never set in this build, its count gc0182 = 15 here; the
            // shareware's says 3 (observed in the original shareware 2026-10-26). Before the data is
            // read, and so before the Public Version box (MAINMOD2.BAS:6437, 6455)
            string mod1 = PortSource("decompiled-named", "MODULE1.BAS");
            string mainTxt = PortSource("decompiled-named", "MAIN.TXT");
            Check("the Limitation Notice as sub03E3 has it, on gv0180, with the INI's Limited key (MODULE1.BAS:932-940)",
                  mod1.Contains("If  gv0180 Then") && mod1.Contains("\"Loading Information\", \"Limited\"), \"T\") = False Then")
                  && mod1.Contains("l06E2 = \"NOTICE!\" + Chr(13)")
                  && mod1.Contains("\"Because this is a PUBLIC BETA version of MORDOR, only \" + CStr(gc0182) + \" dungeon levels are currently available.\" + Chr(13)")
                  && mod1.Contains("l06E2 = l06E2 + \"This message will not appear again.\"")
                  && mod1.Contains("MsgBox l06E2, mc06E4, \"Limitation Notice\"")
                  && mod1.Contains("extfn0184(\"Loading Information\", \"Limited\", \"True\""), true);
            Check("gv0180 is a Global nothing sets, gc0182 15 in the full build (MAIN.TXT:27-28)",
                  mainTxt.Contains("Global gv0180 As Integer") && mainTxt.Contains("Global Const gc0182 = 15 ")
                  && !System.Text.RegularExpressions.Regex.IsMatch(string.Join("\n", Directory.GetFiles(PortTree.At("decompiled-named"), "*.BAS").Select(File.ReadAllText)), @"gv0180\s*="), true);
            Check("Sub Main: sub03E3 before the data and the Public Version box (MAINMOD2.BAS:6437, 6455)",
                  main2.IndexOf("sub03E3 False", StringComparison.Ordinal) is int n03 && n03 > 0
                  && n03 < main2.IndexOf("mdrReadAllData", n03, StringComparison.Ordinal) && n03 < main2.IndexOf("If  m0622 Then sub10F8", StringComparison.Ordinal), true);
            string order = PortSource("decompiled-named", "FRMORDER.BAS");
            Check("the order form's last line as FRMORDER has it (&& shows as &)",
                  order.Contains("\"Order the full version of MORDOR now && see just how far you can take adventure!\"")
                  && LimitedVersion.OrderHow.EndsWith("Order the full version of MORDOR now & see just how far you can take adventure!", StringComparison.Ordinal), true);
            // the full build's own copy of the form, with its price; the limited build's differs only in that
            Check("the order form as FRMORDER has it, with the full build's $39.99 (FRMORDER.BAS:18)",
                  order.Contains("\"" + OrderSentence(LimitedVersion.OrderInfoAt(LimitedVersion.FullPrice)).Replace("&", "&&") + "\"")
                  && LimitedVersion.FullPrice == "$39.99", true);
            Check("About's date in the full build is gc0196 (MAIN.TXT:36)",
                  PortSource("decompiled-named", "MAIN.TXT").Contains("Global Const gc0196 = \"" + LimitedVersion.FullLastEdited + "\""), true);
        });
        Check("the limited build's last level is the shareware's 3 (MORDOR.WRI)", LimitedVersion.LastLevel, 3);

        // ---- the limited build's behaviour, on and off ----
        bool wasOn = LimitedVersion.On;
        try
        {
            LimitedVersion.On = false;
            Check("full: the frame is captioned Mordor 1.1", LimitedVersion.FrameCaption, "Mordor 1.1");
            Check("full: goodbye box, no Public Version box, no Ordering Info, no Limitation Notice",
                  (LimitedVersion.SaysGoodbye, LimitedVersion.ShowsPublicBox, LimitedVersion.ShowsOrderingInfo, LimitedVersion.ShowsLimitationNotice), (true, false, false, false));
            Check("full: level 4 and level 15 are not denied", (LimitedVersion.Clamp(4), LimitedVersion.Clamp(15)), ((4, (string?)null), (15, (string?)null)));
            Check("full: chutes and teleporters on and to level 3 and below work",
                  (LimitedVersion.ChuteIsDeadToTake(3), LimitedVersion.ChuteIsDeadUnderfoot(3), LimitedVersion.TeleporterIsDead(9)), (false, false, false));

            LimitedVersion.On = true;
            Check("limited: the frame is captioned Mordor 1.1 (Limited Version)", LimitedVersion.FrameCaption, "Mordor 1.1 (Limited Version)");
            Check("limited: no goodbye box, the Public Version box, Ordering Info, the Limitation Notice",
                  (LimitedVersion.SaysGoodbye, LimitedVersion.ShowsPublicBox, LimitedVersion.ShowsOrderingInfo, LimitedVersion.ShowsLimitationNotice), (false, true, true, true));
            Check("limited: levels 1 to 3 are not denied", (LimitedVersion.Clamp(1).Said, LimitedVersion.Clamp(3)), ((string?)null, (3, (string?)null)));
            Check("limited: level 4 is denied, and the party stays on 3", LimitedVersion.Clamp(4), (3, "LIMITED VERSION: Request to go to Level 4 is Denied."));
            Check("limited: level 15 is denied too", LimitedVersion.Clamp(15).Level, 3);
            Check("limited: past mc015C (16) and below nought fn0A0B leaves before the clamp", (LimitedVersion.Clamp(16), LimitedVersion.Clamp(-1)), ((16, (string?)null), (-1, (string?)null)));
            Check("limited: a chute on level 3 is dead to TAKE and underfoot, on level 2 it is not",
                  (LimitedVersion.ChuteIsDeadToTake(3), LimitedVersion.ChuteIsDeadUnderfoot(3), LimitedVersion.ChuteIsDeadToTake(2), LimitedVersion.ChuteIsDeadUnderfoot(2)),
                  (true, true, false, false));
            Check("limited: a teleporter to level 4 is dead, to 3 or its own level (0) it is not",
                  (LimitedVersion.TeleporterIsDead(4), LimitedVersion.TeleporterIsDead(3), LimitedVersion.TeleporterIsDead(0)), (true, false, false));

            LimitedVersion.Apply(new GameFilesCheck("x", GameFilesVerdict.Limited, ""));
            Check("Apply: the shareware's check switches it on", LimitedVersion.On, true);
            LimitedVersion.Apply(new GameFilesCheck("x", GameFilesVerdict.Full, ""));
            Check("Apply: the full game's switches it off", LimitedVersion.On, false);
            LimitedVersion.Apply(null);
            Check("Apply: nothing found, off", LimitedVersion.On, false);
        }
        finally { LimitedVersion.On = wasOn; }

        // ---- the game folder given to the check ----
        var mine = GameFiles.Check(dir);
        Console.WriteLine($"  {dir}: {mine.Verdict} - {mine.Reason} [{mine.Recognised}]");
        FullCheck("the game folder given here is a full Mordor", mine.Verdict, GameFilesVerdict.Full);
        FullCheck("and is played as the full build", (mine.IsPlayable, mine.IsLimited), (true, false));
        int levels = MdrLoader.LoadDungeon(Path.Combine(dir, "MDATA11.MDR")).Count;
        int deepMonster = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR")).Max(m => (int)m.LevelFound);
        int deepItem = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Max(i => (int)i.Floor);
        Console.WriteLine($"  its dungeon has {levels} levels, its deepest monster is found on {deepMonster}, its deepest item on {deepItem}");
        FullCheck("with fifteen levels in MDATA11", levels, GameFiles.FullLevels);
        if (_shareware)
        {
            // the shareware itself: "limited to 3 dungeon levels" (MORDOR.WRI), played as the limited
            // build; its bestiary and treasure are the full game's, reaching past its dungeon
            Check("shareware: the game folder given is played as the limited build", (mine.Verdict, mine.IsPlayable, mine.IsLimited), (GameFilesVerdict.Limited, true, true));
            Check("shareware: three levels in MDATA11", levels, GameFiles.SharewareLevels);
            Check("shareware: and monsters and items found below them all the same", (deepMonster > levels, deepItem > levels), (true, true));
        }

        // ---- About's date and the order form's price, read from MORDOR.EXE (LimitedVersion.ReadProgram) ----
        // Each build's program has its own: the full 1.1's gc0196 is "7/20/95" and its (never shown)
        // order form says $39.99; the 1995 shareware's, an earlier build, "7/6/95" and $39.95; the
        // limited build in Decklin's Domain's demo, 7/20/95 and $39.99 (measured 2026-10-27)
        {
            // a constant pool laid out as the 1995 shareware's (docs/GAME_FILES.md), its date shorter
            byte[] Pool(string date, int len) => System.Text.Encoding.Latin1.GetBytes(
                "\0\0\x06\0" + "1.1" + "\0\x18\0\x16\0" + "Welcome to MORDOR 1.1!" + "\x08\0\x06\0" + "Mordor"
                + "\x08\0" + (char)len + "\0" + date + "\0\x04\0\x01\0,\0"
                + "xx  You can now pre-order the full version of MORDOR at any time for only $39.95 + S&&H!  Feel free");
            Check("found by the text around them: a 1995-style program's 7/6/95 and $39.95", LimitedVersion.FromProgram(Pool("7/6/95", 6)), ((string?)"7/6/95", (string?)"$39.95"));
            Check("a date whose length word does not fit it is not taken", LimitedVersion.FromProgram(Pool("7/6/95", 7)).Date, null);
            Check("a program without either: nothing", LimitedVersion.FromProgram(System.Text.Encoding.Latin1.GetBytes("Mordor 7/6/95 $39.95 + S&&H!")), ((string?)null, (string?)null));
            bool was = LimitedVersion.On;
            try
            {
                LimitedVersion.Apply(new GameFilesCheck(Path.Combine(Path.GetTempPath(), "mordor-no-such-folder"), GameFilesVerdict.Limited, ""));
                Check("limited, with no program to read: the 1995 shareware's 7/6/95 and $39.95",
                      (LimitedVersion.LastEdited, LimitedVersion.Price, OrderSentence(LimitedVersion.OrderInfo).Contains("only $39.95 + S&H!")), ("7/6/95", "$39.95", true));
            }
            finally { LimitedVersion.On = was; }

            string exePath = Path.Combine(dir, "MORDOR.EXE");
            byte[] exe = File.ReadAllBytes(exePath);
            bool Holds(string s) => IndexOf(exe, System.Text.Encoding.Latin1.GetBytes(s)) >= 0;
            // the 1995 shareware's MORDOR.EXE by its SHA-256 (docs/GAME_FILES.md, "The shareware, measured")
            string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(exe));
            bool of1995 = sha.StartsWith("BDE2DAAC", StringComparison.Ordinal) && sha.EndsWith("D9356D14", StringComparison.Ordinal);
            var (date, price) = LimitedVersion.ReadProgram(dir);
            Console.WriteLine($"  this MORDOR.EXE holds About's date {date ?? "(not found)"} and the order price {price ?? "(not found)"}");
            Check(of1995 ? "this MORDOR.EXE, the 1995 shareware's: 7/6/95 and $39.95" : "this MORDOR.EXE, not the 1995 shareware's: 7/20/95 and $39.99",
                  (date, price), of1995 ? ((string?)"7/6/95", (string?)"$39.95") : ((string?)"7/20/95", (string?)"$39.99"));
            bool before = LimitedVersion.On;
            try
            {
                LimitedVersion.Apply(mine);
                // the form's sentence as VB keeps it, "&" written "&&"
                string sentence = OrderSentence(LimitedVersion.OrderInfo).Replace("&", "&&");
                if (LimitedVersion.On)
                    Check("limited: About's date and the order form's price are this MORDOR.EXE's, the sentence word for word",
                          (LimitedVersion.LastEdited, LimitedVersion.Price, Holds("\0" + LimitedVersion.LastEdited + "\0"), Holds(sentence)), (date ?? "", price ?? "", true, true));
                else
                    Check("full: About's date is gc0196, and the form (never shown) is this MORDOR.EXE's, word for word",
                          (LimitedVersion.LastEdited, LimitedVersion.Price, Holds(sentence)), (LimitedVersion.FullLastEdited, LimitedVersion.FullPrice, true));
            }
            finally { LimitedVersion.On = before; }
        }

        // ---- scratch copies ----
        string scratch = Path.Combine(Path.GetTempPath(), "mordor-gamefiles-" + Guid.NewGuid().ToString("N")[..8]);
        string? oldEnv = Environment.GetEnvironmentVariable(GameFolder.EnvDir);
        try
        {
            Environment.SetEnvironmentVariable(GameFolder.EnvDir, null);
            string Copy(string name)
            {
                string to = Path.Combine(scratch, name);
                Directory.CreateDirectory(to);
                foreach (string f in GameFiles.Required) File.Copy(Path.Combine(dir, f), Path.Combine(to, f));
                return to;
            }

            string good = Copy(Path.Combine("root", "game_files"));
            FullCheck("a copy of just the required files is the full game", GameFiles.Check(good).Verdict, GameFilesVerdict.Full);
            FullCheck("and is named as the 1.1 release by its MORDOR.EXE", GameFiles.Check(good).Recognised, "Mordor 1.1 (full release)");

            foreach (string gone in GameFiles.Required)
            {
                string d = Copy("no-" + gone);
                File.Delete(Path.Combine(d, gone));
                var c = GameFiles.Check(d);
                Check($"without {gone}: refused as missing, naming it", (c.Verdict, c.Reason.Contains(gone), c.IsPlayable), (GameFilesVerdict.Missing, true, false));
            }
            Check("an empty folder is refused", GameFiles.Check(Directory.CreateDirectory(Path.Combine(scratch, "empty")).FullName).Verdict, GameFilesVerdict.Missing);
            Check("a folder that is not there is refused", GameFiles.Check(Path.Combine(scratch, "nowhere")).Verdict, GameFilesVerdict.NotFound);

            // the shareware's dungeon: "limited to 3 dungeon levels" (MORDOR.WRI) - the level count
            // is MDATA11's first word, and the loader reads only as many levels as it says
            string share = Copy("shareware");
            CutDungeon(Path.Combine(share, "MDATA11.MDR"));
            var limited = GameFiles.Check(share);
            Console.WriteLine($"  shareware-sized dungeon: {limited.Reason}");
            Check("a three-level dungeon is the shareware: accepted, as the limited build", (limited.Verdict, limited.IsPlayable), (GameFilesVerdict.Limited, true));
            FullCheck("though its MORDOR.EXE is still recognised (the release is told by its data)", limited.Recognised, "Mordor 1.1 (full release)");
            bool before = LimitedVersion.On;
            LimitedVersion.Apply(limited);
            bool onForShare = LimitedVersion.On;
            LimitedVersion.Apply(GameFiles.Check(good));
            bool onForFull = LimitedVersion.On;
            LimitedVersion.On = before;
            FullCheck("the shareware's data switches the limited build on, the full game's off", (onForShare, onForFull), (true, false));

            // fewer monsters and items than the full game: none below level 3
            string fewer = Copy("shallow-monsters");
            ShallowRecords(Path.Combine(fewer, "MDATA5.MDR"), monsters: true);
            var shallowM = GameFiles.Check(fewer);
            FullCheck("a bestiary that stops at level 3 is the shareware, accepted", (shallowM.Verdict, shallowM.Reason.Contains("monster"), shallowM.IsPlayable), (GameFilesVerdict.Limited, true, true));
            string fewerItems = Copy("shallow-items");
            ShallowRecords(Path.Combine(fewerItems, "MDATA3.MDR"), monsters: false);
            var shallowI = GameFiles.Check(fewerItems);
            FullCheck("treasure that stops at level 3 is the shareware, accepted", (shallowI.Verdict, shallowI.Reason.Contains("item"), shallowI.IsPlayable), (GameFilesVerdict.Limited, true, true));

            string broken = Copy("damaged");
            File.WriteAllBytes(Path.Combine(broken, "MDATA11.MDR"), new byte[] { 0x0F, 0, 1, 2, 3 });
            Check("a dungeon file that does not read is refused as damaged", (GameFiles.Check(broken).Verdict, GameFiles.Check(broken).IsPlayable), (GameFilesVerdict.Damaged, false));
            string foreign = Copy("foreign");
            File.WriteAllText(Path.Combine(foreign, "MORDOR.EXE"), "not a program");
            Check("a MORDOR.EXE that is no Windows 3.1 program is refused", GameFiles.Check(foreign).Verdict, GameFilesVerdict.NotMordor);
            string foreignData = Copy("foreign-data");
            File.WriteAllBytes(Path.Combine(foreignData, "MDATA2.MDR"), new byte[64]);
            Check("a data file without Mordor's version string is refused", GameFiles.Check(foreignData).Verdict, GameFilesVerdict.NotMordor);

            // ---- the launch settings ----
            string ini = Path.Combine(scratch, "launch", "settings.ini");
            Check("no launch settings: no folder configured", GameFolder.Configured(ini), null);
            Check("a folder is remembered", GameFolder.Remember(good, ini), true);
            Check("and read back", GameFolder.Configured(ini), good);
            Check("under [Loading Information] GameDir=", File.ReadAllText(ini).Contains("[Loading Information]") && File.ReadAllText(ini).Contains("GameDir=" + good), true);
            File.AppendAllText(ini, "[Other]\nKeep=1\n");
            GameFolder.Remember(share, ini);
            Check("a new choice replaces it and leaves the rest of the file", (GameFolder.Configured(ini), Ini.Read(ini).Text("Other", "Keep")), (share, "1"));
            var chosen = GameFolder.Resolve(ini, Path.Combine(scratch, "root", "port", "bin"));
            Check("a configured shareware folder is played, as the limited build",
                  (chosen.Explicit, chosen.IsPlayable, chosen.IsFull, chosen.Found?.Verdict), (true, true, false, GameFilesVerdict.Limited));
            GameFolder.Remember(broken, ini);
            var chosenBroken = GameFolder.Resolve(ini, Path.Combine(scratch, "root", "port", "bin"));
            Check("a configured folder is the player's word: a damaged one is reported, not passed over",
                  (chosenBroken.Explicit, chosenBroken.IsPlayable, chosenBroken.Nearest?.Dir), (true, false, broken));
            GameFolder.Remember(good, ini);
            FullCheck("and a full one there is taken", GameFolder.Resolve(ini, scratch).IsFull, true);

            // ---- found without being told ----
            string none = Path.Combine(scratch, "launch", "none.ini");
            var beside = GameFolder.Resolve(none, Path.Combine(scratch, "root", "port", "src/Mordor.App", "bin"));
            FullCheck("with nothing configured, game_files beside a folder above the port is found",
                  (beside.IsFull, beside.Found?.Dir, beside.How, beside.Explicit), (true, good, "beside the port", false));
            // a shareware copy nearer the port than a full one: the full one is still taken
            string nearShare = Copy(Path.Combine("pref", "game_files"));
            CutDungeon(Path.Combine(nearShare, "MDATA11.MDR"));
            string farFull = Copy("game_files");
            var preferred = GameFolder.Resolve(none, Path.Combine(scratch, "pref", "port"));
            FullCheck("a full Mordor found further on is taken before a shareware one found first", (preferred.IsFull, preferred.Found?.Dir), (true, farFull));
            Directory.Delete(farFull, true);
            var lonely = GameFolder.Resolve(none, Path.Combine(scratch, "pref", "port"));
            // the machine's own installs are searched after these folders, and a full one there
            // would rightly be taken first
            Check("with no full one about, the shareware found is played", lonely.Found?.Dir == nearShare || lonely.IsFull, true);
            Check("the order of the search starts beside the port",
                  GameFolder.Candidates(Path.Combine(scratch, "a", "b")).First().Dir, Path.Combine(scratch, "a", "b", "game_files"));
            // the walk up ends at the drive's root, which keeps its separator: "C:" without it is the
            // current directory on C:, and was checked under the root's name in its place
            string driveRoot = Path.GetPathRoot(Path.GetFullPath(scratch))!;
            var upward = GameFolder.Candidates(Path.Combine(scratch, "a", "b")).Select(c => c.Dir).ToList();
            Check("the drive's root is searched as the root, its separator kept",
                  (upward.Contains(driveRoot), upward.Any(d => d.Length > 0 && d == driveRoot.TrimEnd('\\', '/'))),
                  (true, false));
            string? cwdWas = null;
            try
            {
                // from a working directory that is not the root, a root candidate still names the root
                cwdWas = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(scratch);
                var rootCheck = GameFiles.Check(upward.FirstOrDefault(d => d == driveRoot) ?? driveRoot.TrimEnd('\\', '/'));
                Check("and checked as the root, not the working directory", rootCheck.Dir, driveRoot);
            }
            finally { if (cwdWas != null) Directory.SetCurrentDirectory(cwdWas); }
            Check("any other folder loses its trailing separator",
                  GameFolder.Candidates(Path.Combine(scratch, "a") + Path.DirectorySeparatorChar).ElementAt(1).Dir, Path.Combine(scratch, "a"));
            Environment.SetEnvironmentVariable(GameFolder.EnvDir, broken);
            var told = GameFolder.Resolve(ini, scratch);
            Check("MORDOR_GAMEDIR comes first, and is the player's word too", (told.Explicit, told.Found?.Verdict), (true, GameFilesVerdict.Damaged));
            Environment.SetEnvironmentVariable(GameFolder.EnvDir, share);
            Check("MORDOR_GAMEDIR naming the shareware: played", GameFolder.Resolve(ini, scratch).IsPlayable, true);
            Environment.SetEnvironmentVariable(GameFolder.EnvDir, null);

            // ---- the saves folder (Saves.Resolve) ----
            string gameA = Directory.CreateDirectory(Path.Combine(scratch, "savesA", "game_files")).FullName;
            string perUser = Path.Combine(scratch, "appdata", "OpenDejenol", "saves");
            string legacyA = Path.Combine(gameA, "..", "saves");
            Check("no saves beside the game folder: the per-user folder", Saves.Resolve(gameA, null, perUser), perUser);
            Check("and it is only named, not made", Directory.Exists(perUser), false);
            Directory.CreateDirectory(legacyA);
            Check("an empty saves folder beside it: still the per-user folder", Saves.Resolve(gameA, null, perUser), perUser);
            File.WriteAllText(Path.Combine(legacyA, "Someone.json"), "{}");
            Check("saves beside the game folder (the developer's layout): kept there", Saves.Resolve(gameA, null, perUser), legacyA);
            Check("as the port always had it, <game>\\..\\saves", Saves.LegacyDirFor(gameA), Path.Combine(gameA, "..", "saves"));
            string over = Path.Combine(scratch, "override");
            Check("MORDOR_SAVES before either", Saves.Resolve(gameA, over, perUser), over);
            Check("and it is made", Directory.Exists(over), true);
            Check("nothing moved out of the legacy folder", File.Exists(Path.Combine(legacyA, "Someone.json")), true);
            Check("the per-user folder is under %APPDATA%\\OpenDejenol (or the old Mordor Port, until it is renamed)",
                  Saves.PerUserDir, Path.Combine(UserFolders.In(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), "saves"));

            // ---- the per-user folders' old name (UserFolders): renamed, never lost ----
            {
                string parentN = Directory.CreateDirectory(Path.Combine(scratch, "rename-new")).FullName;
                Check("nothing there yet: the new name, only named", (UserFolders.In(parentN), Directory.Exists(Path.Combine(parentN, "OpenDejenol"))),
                      (Path.Combine(parentN, "OpenDejenol"), false));
                string parentO = Directory.CreateDirectory(Path.Combine(scratch, "rename-old")).FullName;
                string oldDir = Directory.CreateDirectory(Path.Combine(parentO, "Mordor Port", "saves")).FullName;
                File.WriteAllText(Path.Combine(parentO, "Mordor Port", "settings.ini"), "[Loading Information]\r\nGameDir=C:\\Somewhere\\Mordor\r\n");
                File.WriteAllText(Path.Combine(oldDir, "Someone.json"), "{}");
                Check("only the old folder there: it is the one read, until renamed", UserFolders.In(parentO), Path.Combine(parentO, "Mordor Port"));
                Check("and its launch settings are read from it",
                      GameFolder.Configured(Path.Combine(UserFolders.In(parentO), "settings.ini")), "C:\\Somewhere\\Mordor");
                Check("renamed: the new folder in use", UserFolders.Migrate(parentO), Path.Combine(parentO, "OpenDejenol"));
                Check("with the launch settings and the saves in it, and no old folder left",
                      (GameFolder.Configured(Path.Combine(parentO, "OpenDejenol", "settings.ini")),
                       File.Exists(Path.Combine(parentO, "OpenDejenol", "saves", "Someone.json")), Directory.Exists(Path.Combine(parentO, "Mordor Port"))),
                      ("C:\\Somewhere\\Mordor", true, false));
                Check("renaming again does nothing", UserFolders.Migrate(parentO), Path.Combine(parentO, "OpenDejenol"));
                string parentB = Directory.CreateDirectory(Path.Combine(scratch, "rename-both")).FullName;
                Directory.CreateDirectory(Path.Combine(parentB, "Mordor Port"));
                File.WriteAllText(Path.Combine(parentB, "Mordor Port", "settings.ini"), "old");
                Directory.CreateDirectory(Path.Combine(parentB, "OpenDejenol"));
                Check("both there: the new one, and the old left alone",
                      (UserFolders.Migrate(parentB), File.Exists(Path.Combine(parentB, "Mordor Port", "settings.ini"))), (Path.Combine(parentB, "OpenDejenol"), true));
                string parentL = Directory.CreateDirectory(Path.Combine(scratch, "rename-locked")).FullName;
                Directory.CreateDirectory(Path.Combine(parentL, "Mordor Port"));
                string locked = Path.Combine(parentL, "Mordor Port", "settings.ini");
                File.WriteAllText(locked, "x");
                using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                    Check("a file in the old folder held open: no rename, the old folder still read, no exception",
                          UserFolders.Migrate(parentL), OperatingSystem.IsWindows() ? Path.Combine(parentL, "Mordor Port") : Path.Combine(parentL, "OpenDejenol"));
            }
            string? savesEnv = Environment.GetEnvironmentVariable(Saves.EnvDir);
            try
            {
                Environment.SetEnvironmentVariable(Saves.EnvDir, null);
                Check("DirFor without MORDOR_SAVES: the legacy folder with saves in it", Saves.DirFor(gameA), legacyA);
                Environment.SetEnvironmentVariable(Saves.EnvDir, over);
                Check("DirFor with MORDOR_SAVES: that", Saves.DirFor(gameA), over);
            }
            finally { Environment.SetEnvironmentVariable(Saves.EnvDir, savesEnv); }
            // a save into a per-user folder not made yet makes it
            var someone = new Character { Name = "Scratch", Guilds = { new GuildMembership { Guild = "Nomad", Level = 1 } } };
            string fresh = Path.Combine(scratch, "appdata2", "OpenDejenol", "saves");
            someone.Save(Path.Combine(fresh, "Scratch.json"));
            Check("the first save into a per-user folder makes the folder", File.Exists(Path.Combine(fresh, "Scratch.json")), true);

            // ---- the port's loose files, crash.log and CHARSTAT.TXT (PortFiles) ----
            string perRoot = Path.Combine(scratch, "appdata", "OpenDejenol");
            Check("the per-user folder for them is %APPDATA%\\OpenDejenol, beside the per-user saves",
                  PortFiles.PerUserRoot, UserFolders.Roaming);
            string Places(List<string> l) => string.Join(" | ", l);
            // the developer's layout: saves\ with saves in it beside game_files - nothing moves
            string rootU = Directory.CreateDirectory(Path.Combine(scratch, "userlayout")).FullName;
            string gameU = Directory.CreateDirectory(Path.Combine(rootU, "game_files")).FullName;
            Directory.CreateDirectory(Path.Combine(rootU, "saves"));
            File.WriteAllText(Path.Combine(rootU, "saves", "Someone.json"), "{}");
            string savesU = Saves.Resolve(gameU, null, perUser);
            string besideGame = Directory.GetParent(gameU)!.FullName;   // where both always went
            Check("saves beside the game folder: crash.log first where it always was, beside the game folder",
                  PortFiles.CrashLogPlaces(savesU, perRoot)[0], Path.Combine(besideGame, "crash.log"));
            Check("and CHARSTAT.TXT first where the port always put it, beside the game folder too",
                  PortFiles.CharStatPlaces(gameU, savesU, perUser, perRoot)[0], Path.Combine(besideGame, "CHARSTAT.TXT"));
            Check("CHARSTAT.TXT then the game folder, then the per-user folder",
                  Places(PortFiles.CharStatPlaces(gameU, savesU, perUser, perRoot)),
                  Places(new() { Path.Combine(besideGame, "CHARSTAT.TXT"), Path.Combine(gameU, "CHARSTAT.TXT"), Path.Combine(perRoot, "CHARSTAT.TXT") }));
            Check("crash.log then the per-user folder, then the temporary folder",
                  Places(PortFiles.CrashLogPlaces(savesU, perRoot)),
                  Places(new() { Path.Combine(besideGame, "crash.log"), Path.Combine(perRoot, "crash.log"), Path.Combine(Path.GetTempPath(), "mordor-crash.log") }));
            Check("before the game folder is known, crash.log starts at the per-user folder", PortFiles.CrashLogPlaces(null, perRoot)[0], Path.Combine(perRoot, "crash.log"));
            Check("a CHARSTAT.TXT written there lands beside the game folder",
                  CharStatFile.Write(someone, PortFiles.CharStatPlaces(gameU, savesU, perUser, perRoot), _ => null), Path.Combine(besideGame, "CHARSTAT.TXT"));
            CharStatFile.Write(someone, PortFiles.CharStatPlaces(gameU, savesU, perUser, perRoot), _ => null);
            Check("and is appended to, as the original's", File.ReadAllText(Path.Combine(besideGame, "CHARSTAT.TXT")).Split("CHARACTER: ").Length - 1, 2);

            // a fresh install that can be written to, with no saves beside it
            string gameF = Directory.CreateDirectory(Path.Combine(scratch, "fresh", "Mordor")).FullName;
            string savesF = Saves.Resolve(gameF, null, perUser);
            Check("a fresh install: crash.log in the per-user folder, beside its saves",
                  Places(PortFiles.CrashLogPlaces(savesF, perRoot)), Places(new() { Path.Combine(perRoot, "crash.log"), Path.Combine(Path.GetTempPath(), "mordor-crash.log") }));
            Check("and CHARSTAT.TXT in the game folder, as the original, else the per-user folder",
                  Places(PortFiles.CharStatPlaces(gameF, savesF, perUser, perRoot)), Places(new() { Path.Combine(gameF, "CHARSTAT.TXT"), Path.Combine(perRoot, "CHARSTAT.TXT") }));

            // a game folder that cannot be written to, as under Program Files: writes denied by its ACL
            string gameRO = Directory.CreateDirectory(Path.Combine(scratch, "programfiles", "Mordor")).FullName;
            if (SetWritesDenied(gameRO, true))
            {
                try
                {
                    Check("the read-only scratch game folder refuses a file (the check's own premise)", PortFiles.Append(new[] { Path.Combine(gameRO, "probe.txt") }, "x"), null);
                    string savesRO = Saves.Resolve(gameRO, null, perUser);
                    Check("a read-only game folder: CHARSTAT.TXT goes to the per-user folder",
                          CharStatFile.Write(someone, PortFiles.CharStatPlaces(gameRO, savesRO, perUser, perRoot), _ => null), Path.Combine(perRoot, "CHARSTAT.TXT"));
                    Check("and the crash log too", PortFiles.Append(PortFiles.CrashLogPlaces(savesRO, perRoot), "x\r\n"), Path.Combine(perRoot, "crash.log"));
                    Check("and nothing was left in the game folder", Directory.EnumerateFileSystemEntries(gameRO).Any(), false);
                }
                finally { SetWritesDenied(gameRO, false); }
                // saves beside the game folder, but the folder holding them read-only
                string rootRO = Directory.CreateDirectory(Path.Combine(scratch, "ro-root")).FullName;
                string gameR = Directory.CreateDirectory(Path.Combine(rootRO, "game_files")).FullName;
                File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(rootRO, "saves")).FullName, "Someone.json"), "{}");
                string savesR = Saves.Resolve(gameR, null, perUser);
                SetWritesDenied(rootRO, true);
                try
                {
                    Check("a read-only folder beside the saves: crash.log goes to the per-user folder",
                          PortFiles.Append(PortFiles.CrashLogPlaces(savesR, perRoot), "x\r\n"), Path.Combine(perRoot, "crash.log"));
                    Check("and CHARSTAT.TXT to the game folder, the original's place",
                          PortFiles.Append(PortFiles.CharStatPlaces(gameR, savesR, perUser, perRoot), "x\r\n"), Path.Combine(gameR, "CHARSTAT.TXT"));
                    Check("nothing was left beside the saves", Directory.EnumerateFiles(rootRO).Any(), false);
                }
                finally { SetWritesDenied(rootRO, false); }
            }
            else Check("a read-only scratch folder could be made (Windows ACLs)", OperatingSystem.IsWindows(), false);
            // and, on any system, a game folder that cannot exist: its parent is a file
            string blocker = Path.Combine(scratch, "blocker.txt");
            File.WriteAllText(blocker, "");
            string gameX = Path.Combine(blocker, "Mordor");
            string perRootX = Path.Combine(scratch, "appdataX", "OpenDejenol");
            Check("an unwritable game folder: CHARSTAT.TXT to the per-user folder, which is made",
                  PortFiles.Append(PortFiles.CharStatPlaces(gameX, Path.Combine(perRootX, "saves"), Path.Combine(perRootX, "saves"), perRootX), "x\r\n"), Path.Combine(perRootX, "CHARSTAT.TXT"));
            Check("nowhere writable: null, and no exception", PortFiles.Append(new[] { Path.Combine(gameX, "a.txt"), Path.Combine(blocker, "b", "c.txt") }, "x"), null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GameFolder.EnvDir, oldEnv);
            try { Directory.Delete(scratch, true); } catch { }
        }

        // no personal path left in what a player runs: no one's user folder named outright
        foreach (string f in new[] { "App.axaml.cs", "TitleWindow.axaml.cs", "MainWindow.axaml.cs", "Program.cs", "GameFolderWindow.cs" })
            Check($"Mordor.App/{f} names no user's folder", System.Text.RegularExpressions.Regex.IsMatch(PortSource("src/Mordor.App", f), @"[A-Za-z]:[\\/]+Users[\\/]+[A-Za-z]", System.Text.RegularExpressions.RegexOptions.IgnoreCase), false);
        Check("the per-user folders are named for the program", (UserFolders.Name, UserFolders.OldName), ("OpenDejenol", "Mordor Port"));

        // the installed copy's update check, as the launch settings set it (UpdateSettings, docs/RELEASING.md)
        var noLines = Ini.Parse(Array.Empty<string>());
        Check("updates: checked unless the launch settings say [Updates] Check=False",
              (UpdateSettings.CheckEnabled(noLines), UpdateSettings.CheckEnabled(Ini.Parse(new[] { "[Updates]", "Check=False" })),
               UpdateSettings.CheckEnabled(Ini.Parse(new[] { "[Updates]", "Check=True" }))), (true, false, true));
        Check("updates: with no Prereleases line, test releases are offered only to a test release",
              (UpdateSettings.Prereleases("0.1.0", noLines), UpdateSettings.Prereleases("0.2.0-beta.1", noLines),
               UpdateSettings.Prereleases("0.2.0+build-7", noLines), UpdateSettings.Prereleases(null, noLines)), (false, true, false, false));
        Check("updates: Prereleases=True or False is the player's word either way",
              (UpdateSettings.Prereleases("0.1.0", Ini.Parse(new[] { "[Updates]", "Prereleases=True" })),
               UpdateSettings.Prereleases("0.2.0-beta.1", Ini.Parse(new[] { "[updates]", "prereleases=false" }))), (true, false));
    }

    /// <summary>
    /// Deny (or allow again) the current user the making of files and folders in this folder - not
    /// inherited, so what is inside stays writable - as a folder under Program Files is denied to a
    /// player. True when it was done and, when denying, a file can no longer be made there.
    /// </summary>
    private static bool SetWritesDenied(string dir, bool deny)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var info = new DirectoryInfo(dir);
            var acl = info.GetAccessControl();
            var me = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
            var rule = new System.Security.AccessControl.FileSystemAccessRule(me,
                System.Security.AccessControl.FileSystemRights.CreateFiles | System.Security.AccessControl.FileSystemRights.CreateDirectories,
                System.Security.AccessControl.InheritanceFlags.None, System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Deny);
            if (deny) acl.AddAccessRule(rule); else acl.RemoveAccessRule(rule);
            info.SetAccessControl(acl);
            if (!deny) return true;
            string probe = Path.Combine(dir, "probe-" + Guid.NewGuid().ToString("N")[..8]);
            try { File.WriteAllText(probe, ""); File.Delete(probe); }
            catch { return true; }
            // the denial did not take (an account that passes over it): undo it and say so
            acl.RemoveAccessRule(rule);
            info.SetAccessControl(acl);
            return false;
        }
        catch { return false; }
    }

    /// <summary>MDATA11's level count, its first word, cut to the shareware's three.</summary>
    private static void CutDungeon(string path)
    {
        using var f = new FileStream(path, FileMode.Open, FileAccess.Write);
        f.Write(BitConverter.GetBytes((ushort)GameFiles.SharewareLevels));
    }

    /// <summary>
    /// Every record of MDATA5 (monsters) or MDATA3 (items) moved to level 3 at most, at the place
    /// MdrLoader reads the field from: record 3 + i (of 160 or 125 bytes), after the name and its
    /// length, LevelFound after seven words, Floor after three words and a dword. The copy is the
    /// full one cut to the shareware's depth.
    /// </summary>
    private static void ShallowRecords(string path, bool monsters)
    {
        var bytes = File.ReadAllBytes(path);
        int size = monsters ? 160 : 125, skip = monsters ? 14 : 10;
        int count = BitConverter.ToUInt16(bytes, 2 * size);
        for (int i = 0; i < count; i++)
        {
            int rec = (3 + i) * size;
            int at = rec + 2 + BitConverter.ToUInt16(bytes, rec) + skip;
            if (BitConverter.ToInt16(bytes, at) > GameFiles.SharewareLevels)
                BitConverter.GetBytes((short)GameFiles.SharewareLevels).CopyTo(bytes, at);
        }
        File.WriteAllBytes(path, bytes);
    }
}
