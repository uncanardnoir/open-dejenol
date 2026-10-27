# The player's game files: full version or shareware

The port is an engine. It ships none of Mordor's data and reads the files of the player's own
copy of *Mordor: The Depths of Dejenol* - installed, or unpacked by the port from the installer the
player bought (below, "The installers"). Without that copy there is nothing to play -
that is what keeps it to players who have the game, not a check in the code. This is the first step
towards an open-source engine that players run against a copy they have.

It plays either release:

- the **full** (commercial) release, as the full MORDOR.EXE plays;
- the **shareware** ("PUBLIC Limited Version"), as the limited MORDOR.EXE plays - its three
  levels, its dead chutes, its caption and boxes (below).

The only thing that keeps the game from starting is the files it needs being missing or unreadable.

Code: `src/Mordor.Core/GameFiles.cs` (`GameFiles.Check`, `GameFolder`), `src/Mordor.Core/LimitedVersion.cs`
(the limited build's behaviour), `src/Mordor.Core/Saves.cs` (the saves folder),
`src/Mordor.App/GameFolderWindow.cs` (the first-run window), the start-up in `src/Mordor.App/App.axaml.cs`,
`src/Mordor.Core/InstallerUnpack.cs` and `src/Mordor.Core/Extraction/Lzma.cs` (the installers).
Checks: `selfcheck` ("the game files and the limited build", "the installers, unpacked") and
`Mordor.UiCheck` ("the first-run window", "the first-run window, given an installer").

## How the original tells its two releases apart

It does not, at run time. The shareware is the same source compiled with its "limited" constants
switched on. In the full `MORDOR.EXE` each is a constant False, or 15, or a variable nothing sets:

| constant | what it does | where | in the full build |
|---|---|---|---|
| `m009C` | title bar " (Limited Version)" | FRMMAIN.BAS:148-152, `If m009C = True` | `Const m009C = 0` (FRMMAIN.BAS:34) |
| `m009C` | goodbye box asking players to pass on the shareware | FRMMAIN.BAS:240-248, `If m009C = False` | shown |
| `mc015E` | "LIMITED VERSION: Request to go to Level n is Denied.", and the level clamped to it | MAZEMOD.BAS:409-422 (fn0A0B, which every change of level goes through) | `mc015E = 15`, the same as the deepest level `mc015C = 15`: never fires |
| `m0622`, `mc0624` | TAKE on a chute on the last level: "You cannot take this Chute in the Limited Version." | MAINMOD2.BAS:3997-3998, `If m0622 = True And DungeonLevel = mc0624` | `Const m0622 = 0`, `Const mc0624 = 15` (MAINMOD2.BAS:224-225) |
| `m0622` | "This is the PUBLIC version of Mordor" box, captioned "Public Version", at start | MAINMOD2.BAS:6339-6345 (sub10F8), called from Sub Main at 6455 `If m0622 Then`, before the frame is shown | never called |
| `m02A6` | a teleporter to past the last level: "..but it doesn't go anywhere in the limited version..." | MAZEMOD.BAS:1794-1796, `If m02A6 = True And Z2 > mc015E` | a Static (1535) nothing sets |
| `m02A6` | a chute walked onto on the last level: "There is a chute here, but it doesn't do anything in the Limited Version." | MAZEMOD.BAS:1840-1841, `If m02A6 = True And DungeonLevel = mc015E` | the same |
| `m0032` | About's "Ordering Info" button (FRMABOUT.FRM: at 90,1080, 1425x285, hidden), which opens frmOrderInfo (FRMORDER.BAS) | FRMABOUT.BAS:24, `If m0032 = True Then cmdInfo.Visible = True` | a Dim nothing sets |
| `m00A8` | the main menu's About button captioned "Ordering/&About" | FRMMENU.BAS:162, at the end of Form_Load | a Static never set |
| `gv0180` | "Limitation Notice": "PUBLIC BETA ... only n dungeon levels", then `Limited=True` written to MORDOR.INI, shown until that key holds a "T" (the shareware sets it, with n = 3: observed in the original shareware 2026-10-26) | MODULE1.BAS:932-940, sub03E3, called from Sub Main at 6437 before the data is read | never set (and `gc0182` = 15) |
| `m002E` | "Limited " before the version on the splash | FRMWELCO.BAS:14-16, a Static | never set |

Every one of those strings is still in the full MORDOR.EXE (checked with `strings`): the VB3
compiler kept both branches. So there is no string in the program that marks a release, and the
constants live in p-code that only the decompiler reads. The program is not a usable test.

`[Loading Information] Limited` in MORDOR.INI is not one either: only the `gv0180` branch above -
the shareware's Limitation Notice - ever writes it (and only `True`), the full build never clears it,
so a player who once ran the shareware keeps `Limited=True` in the Windows folder's MORDOR.INI for good. (The user's own copy has
`Limited=False` in its folder's MORDOR.INI - not a file MordorSetup.exe holds.)

## What the shareware lacks

The developers say it in three places:

- MORDOR.WRI (in the install, "Mordor 1.1 Information"): "For those of you who have the PUBLIC
  Limited Version (or Shareware) version of MORDOR, the only difference between the version you are
  playing and the FULL version of MORDOR is that the Shareware version is limited to 3 dungeon
  levels. Of course because of that limitation, you're missing out on over 90% of the game - more
  levels, more monsters, more items". And: "All characters created with the Shareware version of
  Mordor are 100% compatible with the commercial version!"
- FRMORDER.BAS:17: "The full commercial version of MORDOR has so many more Monsters, Items and
  Dungeon Levels than the PUBLIC version".
- MAINMOD2.BAS:6342: "hundreds of additional monsters, items and over a dozen more levels".

The full release's data, measured on the user's copy (Mordor 1.1, unpacked from MordorSetup.exe,
the Tarma InstallMate 9 installer Decklin's Domain sells - not installed by it, which Windows 10 and
11 refuse; below, "The installers"):

| file | holds | full 1.1 |
|---|---|---|
| MDATA11.MDR (339,606 bytes) | dungeon; first word = number of levels | **15** levels |
| MDATA5.MDR (64,623) | monsters; `LevelFound` per monster | deepest 16 |
| MDATA3.MDR (46,103) | items; `Floor` per item | deepest 16 |
| MDATA1/2/3/5 | open with a version string | "1.171" |

### The shareware, measured (2026-10-25)

The real shareware: `MORDOR11.ZIP` from the Internet Archive,
<https://archive.org/details/MordorTheDepthsofDejenol_1020>, 106 files dated 10 June to 6 July
1995 (MORDOR.EXE 6 July 1995, 14:40). Against the full 1.1 above, by SHA-256:

| file | shareware | full 1.1 |
|---|---|---|
| MDATA1.MDR, MDATA2.MDR, MDATA3.MDR, MDATA5.MDR, MG11.DLL, CMDIALOG/MCI/MSGBLAST/THREED/VSVBX.VBX, WAVMIX16.DLL, all 71 WAVs and 16 MIDs | **identical** - e.g. MDATA5 `c26ebd92...5b1c8739`, MDATA3 `babf9ede...7988d7c7`, MDATA1 `b8086bf8...bc2d032f`, MDATA2 `cd811db2...001908dd`, MG11.DLL `b0a9dbd5...6c661680` | the same files |
| MDATA11.MDR | 68,166 bytes, **3** levels, `f2498923...f8373c14` | 339,606 bytes, 15 levels, `866551e7...6dfadc16` |
| MORDOR.EXE | 1,199,121 bytes, `bde2daac...d9356d14` | 1,199,377 bytes, `5284a6a7...0ba7d197` |
| MORDOR.HLP | 861,890 bytes, `00db3697...df1889e0`, titled "Mordor HelpLesson" | 842,686 bytes, `631126a5...b988b6d8`, "M1: Depths of Dejenol Help Lesson" (the 2015 edition's, "1994 to 2015 Decklin's Domain") |
| MORDOR.WRI | 8,320 bytes, "Mordor 1.1 Information (7/5/95)", `0f23e4d2...a107c58c` | 9,216 bytes, `d41ef39f...9dbefd66` |
| only in one | SETUP.EXE, FH_BMP.DLL, MMSYSTEM.DLL, WAVEMIX.INI | VBRUN300.DLL, VBCTL3D.VBX, MORDOR.INI, SUPPORT.WRI (and the user's install has the 2015 edition's extras: cityw1.png, dmainw1.png, MORDOR.chm ...) |

So **the shareware does not lack monsters or items in its data**: its bestiary (MDATA5) and its
treasure (MDATA3) are the full game's byte for byte - the deepest monster found on level 16, the
deepest item on 16 - and so are its races, guilds and spells (MDATA1, MDATA2), its pictures
(MG11.DLL) and its sounds and music. Only the dungeon stops: MDATA11 holds three levels. The "more
monsters, more items" of MORDOR.WRI and the order form are what a shareware player never meets
below level 3, not what the files leave out. Its three levels have the same squares, secret doors
(1, 12, 18) and teleporters (4, 4, 6) as the full game's first three, but MDATA11 is not cut from
the full file byte for byte (no 64-byte run of it is in the full one). The level-3 teleporters with a fixed destination below (three) and its two chutes that
drop past level 3 are all on level 3, where the limited build makes them do nothing.

**The shareware's MORDOR.EXE is an earlier build**, not the full one with its switches flipped. It
is dated 6 July 1995; About's date `gc0196` in it is "7/6/95", where the full build has "7/20/95";
its order form says "$39.95", where the full build's (never shown) says "$39.99" - as its
MORDOR.WRI does ("You can now order the full version of MORDOR for only $39.95 + S&H!"). Its other
strings differ too: it has no "Please use the Mordor EXIT buttons to leave the game." or "Exit
Mordor" (the full build's refusal of the close box), no "THANK YOU FOR PLAYING MORDOR!" box text but
a "Thank you for playing MORDOR!", "If you would like to distribute the Shareware version of Mordor,
you can find it on most game FTP sites, ...", "See MORDOR.WRI for more Information!", "PLEASE DO NOT
DISTRIBUTE!" and "This is the Commercial version of Mordor, NOT the Shareware version.", and an
older piracy line. Its p-code has not been decompiled, so what those do in it is not known; the
port keeps to the full build's decompiled branches (below) and takes from the shareware's program
only the two strings the limited build shows that differ: the price and the date. Every other line
the limited build shows - the caption, the Public Version box, the LIMITED VERSION line, the dead
chute and teleporter lines, the rest of the order form - is in the shareware's MORDOR.EXE word for
word.

`tools/check.sh` passes against this folder: the checks that hold figures of the full game's
dungeon or help print "full game only (this folder is the shareware)" and are skipped, and what
can be held to the shareware is checked instead.

## The check (`GameFiles.Check`)

In order; the first that fails is the answer, and the player is told which:

1. The folder exists.
2. **Required files**: `MORDOR.EXE`, `MDATA1.MDR`, `MDATA2.MDR`, `MDATA3.MDR`, `MDATA5.MDR`,
   `MDATA11.MDR` - what the port reads (sounds and music are optional, as they are now).
   Missing: refused.
3. `MORDOR.EXE` is a Windows 3.1 program (MZ, then NE), as Mordor 1.x is. Not: refused.
4. MDATA1, 2, 3 and 5 open with a data version string (`digit.digit...`); the number itself is not
   checked, so a 1.0 or another 1.1 pressing reads too. Not: refused.
5. Every data file parses with the port's own loaders. Not: refused as damaged.
6. **Which release**: MDATA11 holds at least **15** levels (fn0A0B's own limit), some monster is
   found on level 15 or below, and some item likewise: the full game (`Full`). Less is the
   shareware (`Limited`) - **played**, as the limited build. The real shareware is told by the
   first of the three alone: its monsters and items reach level 16 as the full game's do (they
   are the same files); the other two are for a copy cut down some other way.

No whole-file hash is required: other full releases (1.0, CD against download) need not match
byte for byte, but they will hold fifteen levels. The SHA-256 of the 1.1 MORDOR.EXE is kept
only to *name* a recognised copy ("Mordor 1.1 (full release)") in the log.

Step 6 only chooses behaviour, as the original's two builds differ; it never refuses. A copy that
has been edited to look full is simply played as the full game.

## The shareware, played as the limited build (`LimitedVersion`)

With the shareware's data, `LimitedVersion.On` is set at start (and when the first-run window
accepts a folder), and the port takes the limited build's branches:

| behaviour | port |
|---|---|
| the city and dungeon frames are captioned "Mordor 1.1 (Limited Version)" | `CityWindow`, `DungeonWindow` constructors |
| the main menu too is captioned "Mordor 1.1 (Limited Version)": frmMain's caption, set by MDIForm_Load before the menu comes up (FRMMAIN.BAS:148-152), observed over the shareware's menu 2026-10-26. The port's menu stands in for the frame there, and with the full game's data takes the full build's "Mordor 1.1" (the Else branch, FRMMAIN.BAS:150-151) | `TitleWindow` constructor |
| the help window is captioned "Mordor HelpLesson", the shareware help's own title (the TITLE in the help file's internal SYSTEM file, as WinHelp captioned it; observed in the original shareware 2026-10-26). Not a switch: the help window takes the title of whichever MORDOR.HLP it reads - the full 1.1's is "M1: Depths of Dejenol Help Lesson" | `HelpWindow`, `GameArt.HelpTitle` |
| no "THANK YOU FOR PLAYING MORDOR!" box on the way out | `OriginalBox.ThankYou` (the menu's Exit, a session end) |
| the main menu's close box asks "Exit Mordor?" (Yes and No, captioned "EXIT") and a Yes quits, with no goodbye box - the full build refuses it with "Please use the Mordor EXIT buttons to leave the game." (FRMMAIN.BAS:171-175). Observed in the original shareware 2026-10-26, at the menu only: the city's and the dungeon's close box were not tried there, so they keep the full build's refusal | `TitleWindow`, `OriginalBox.RefuseCloseBox`, `LimitedVersion.CloseBoxAsksToExit` |
| the "Limitation Notice" (MsgBox 48): "NOTICE!", "Because this is a PUBLIC BETA version of MORDOR, only 3 dungeon levels are currently available.", "This message will not appear again." - first of all, before the Public Version box, and then never again | `TitleWindow` on opening. The original writes `Limited=True` to the Windows folder's MORDOR.INI after it; the port writes the same key to the saves folder's `settings.ini`, as it does `NotFirstTime`. Seen in the original shareware 2026-10-26 first thing on every run, because under OTVDM the INI write did not last
| the "Public Version" box (MsgBox 64) once a run, before the menu's own welcome | `TitleWindow` on opening. The original shows it before its frame comes up; the port has no frame apart from the menu, so it comes over the menu |
| a level past 3 asked for: "LIMITED VERSION: Request to go to Level n is Denied." in the message list, and level 3 instead. Levels past 15, or below nought, are left alone as fn0A0B leaves them | `DungeonWindow.ChangeLevel` (stairs, teleporters, Sanctuary); a chute's landing, a line for each member who goes down, as sub08F5 calls fn0A0B for each (MAZEMOD.BAS:114-115); the Teleport spell clamps before its fall and rock tests, as fn0A0B comes before them (MAINMOD2.BAS:5823). Stairs down on level 3 still go to fn0A0B, which denies them |
| TAKE on a chute on level 3: "You cannot take this Chute in the Limited Version." | `DungeonWindow`'s TAKE |
| a chute walked onto on level 3: "There is a chute here, but it doesn't do anything in the Limited Version.", and the square routine goes on | the square routine |
| a teleporter record whose Z2 is past 3: "..but it doesn't go anywhere in the limited version...", nobody moves, and the square routine goes on | the square routine |
| the main menu's About button reads "Ordering/&About" (frmMenu's Form_Load, FRMMENU.BAS:162, `If m00A8 = True`, a Static the full build never sets; observed in the original shareware 2026-10-26), and opens frmAbout as ever (117) | `TitleWindow`, `LimitedVersion.AboutReadsOrdering` |
| About shows "Ordering Info", which opens "Mordor Ordering Information" (frmOrderInfo, laid out from FRMORDER.FRM, text from FRMORDER.BAS), at the price the player's MORDOR.EXE says: "$39.95" in the 1995 shareware's, "$39.99" in the demo's, as in the full build's FRMORDER.BAS:18 (Known follow-ups, below) | `AboutWindow`, `OrderInfoWindow`, `LimitedVersion.OrderInfo`, `LimitedVersion.ReadProgram` |
| About's "Version 1.1 (Last Edited ...)", the player's MORDOR.EXE's `gc0196`: 7/6/95 in the 1995 shareware's, 7/20/95 in the demo's, as in the full build's (MAIN.TXT:36) | `AboutWindow`, `LimitedVersion.LastEdited`, `LimitedVersion.ReadProgram` |

**Values, and which are inference.** The full build's `mc015E` and `mc0624` are 15; the limited
build's are taken as **3**, the shareware's depth in MORDOR.WRI ("limited to 3 dungeon levels") and
in its MDATA11 (measured, above) - in the full build both equal the deepest level, and the
shareware's deepest is 3. The shareware's own program has not been decompiled, so its constants
themselves are not read; the price and the About date are, from its strings. `m009C` and
`m0622` are constants that are 0 in the full build and so True (-1) in the limited one. `m02A6` and
`m0032` are variables nothing assigns in the full build's decompile; they are taken as on in the
limited build, since their only effects are lines that say "limited version" and the order form.

**Not switched**, on purpose: frmWelcome's "Limited " - the port has no splash screen. The `gv0180`
"PUBLIC BETA" notice was left out too until 2026-10-26, as a beta build's flag whose text named
`gc0182` = 15 levels; the real shareware shows it, saying 3 (observed in the original shareware
2026-10-26), so it is switched with the rest.

## Where the port looks (`GameFolder.Resolve`)

1. `MORDOR_GAMEDIR` (test hooks, check.sh's start-up smoke, or anyone who wants to say it outright).
2. `[Loading Information] GameDir=` in the launch settings,
   `%APPDATA%\OpenDejenol\settings.ini` (`MORDOR_LAUNCH_INI` replaces the path, for tests).
   This cannot be the saves folder's `settings.ini`, where the port keeps its other settings:
   the saves folder can depend on the game folder (below).
3. Found:
   - a `game_files` folder beside the port or beside any folder above it - as
     `Desktop\mordor\game_files` sits beside `Desktop\mordor\port` - or the port's folder itself;
   - the games the port unpacked from the installers, `%LOCALAPPDATA%\OpenDejenol\game\full` and
     then `\shareware` (`MORDOR_UNPACK_DIR` replaces `...\game`; below, "The installers");
   - `[File Information] BasePath` in MORDOR.INI in the Windows folder, which is how the original
     finds its own data (MAINMOD.BAS:1193-1196);
   - an uninstall registry entry whose DisplayName contains "Mordor" (its InstallLocation, else
     the folder of its icon or uninstaller);
   - `Mordor`, `Mordor v1.1`, `Mordor v1.1 The Depths of Dejenol` ... under Program Files (both)
     and `%LOCALAPPDATA%\Programs`, `C:\MORDOR`, `C:\GAMES\MORDOR`. (MordorSetup.exe's own
     default, read out of its project, is `INSTALLDIR` = "Mordor v1.1 The Depths of Dejenol" in
     Program Files (32-bit); the others are guesses, and the pickers cover the rest.)

1 and 2 are the player's word: a folder named there that cannot be played is reported on the
first-run window, not silently passed over; a shareware one is played. Found folders are tried in
order; the first full one wins, and with none the first shareware one.

## First run

With no Mordor to play, the menu is never made. A small window comes up instead, headed "Mordor
game files not found", saying that the port plays the player's own copy, that it needs Mordor's
own files (MORDOR.EXE and the MDATA files), that the shareware plays as the shareware did with its
three levels, that Decklin's Domain's installers (MordorSetup.exe, and the free MordorDemoSetup.exe)
do not run on Windows 10 or 11 and the port unpacks them instead, and what was wrong with the
folder found.

- When MordorSetup.exe or MordorDemoSetup.exe is in the player's Downloads folder (the Downloads
  known folder, wherever it has been moved; `MORDOR_DOWNLOADS` replaces it for the checks), the
  window says so - "Found MordorSetup.exe in Downloads." - with an "Unpack it" button. It is
  offered, never unpacked unasked.
- "Use the installer..." opens a file picker (`*.exe`, starting in Downloads) for the installer.
- Either unpacks it (`InstallerUnpack.Unpack`, below) off the window's thread, the buttons off and
  "Unpacking MordorSetup.exe ..." shown meanwhile. A file that is not one of the installers keeps
  the window up with "This file isn't a Mordor installer OpenDejenol can read. Choose
  MordorSetup.exe or MordorDemoSetup.exe, as downloaded from Decklin's Domain."; what was wrong
  with the file goes to the crash log.
- "Locate Mordor..." opens a folder picker, for a Mordor installed some other way; the folder
  picked is checked again (its `game_files` too, if the folder above was picked by mistake).

A folder whose files are there and read - full or shareware, picked or unpacked - has its pictures
and help read out (below; off the window's thread, "Preparing the game files ..." shown and the
buttons off until the menu is up), is written to the launch settings and the menu comes up;
anything else keeps the window up with the reason. "Exit" quits. A start with a Mordor already
known whose pictures and help are not yet in the cache (the first, or after the game files change)
shows a small window saying "Preparing the game files ..." meanwhile; one that finds them shows
nothing before the menu.

With the developer's layout (`Desktop\mordor\port` beside `Desktop\mordor\game_files`) nothing of
this is seen: the folder is found beside the port and nothing is written.

## Saves (`Saves.DirFor`)

In order:

1. `MORDOR_SAVES`, for the smoke tests' throwaway characters.
2. `<game folder>\..\saves` - where the port has always kept them, as `Desktop\mordor\saves` sits
   beside `Desktop\mordor\game_files` - **when that folder exists and has anything in it**. An
   existing setup goes on exactly as before.
3. Otherwise `%APPDATA%\OpenDejenol\saves`, the player's own: a Mordor installed under Program
   Files has no writable folder beside it. It is named, not made, until the first save writes
   into it (every save makes its folder if need be).

Nothing is ever moved or copied from one to the other.

## Files the port writes (`PortFiles`)

The port never writes into the game folder except CHARSTAT.TXT, as the original does, and only
when it can. Every file it writes lands somewhere writable, and each one's first place is where it
went before, so an existing setup (`Desktop\mordor\game_files` with `Desktop\mordor\saves`) sees
nothing move. A file with a list is appended to the first place that takes it (`PortFiles.Append`:
the folder is made if need be, a place that refuses is passed over, and nothing throws).

| file | where | when that cannot be written |
|---|---|---|
| characters, party, automap, dungeon state, shelves, Library, Hall, guild logs, `saves\BACKUP` | the saves folder (above) | - (the per-user saves when nothing is beside the game folder) |
| `settings.ini` (the Options, window places, mods switched on) | the saves folder | as above |
| `mods.log`, a mod's data (`dpsmeter.json`, `kaizen.json`) | the saves folder, beside settings.ini | as above |
| `crash.log` | beside the saves folder in use - for `<game>\..\saves`, beside the game folder (`Desktop\mordor\crash.log`), where it always was; for the per-user saves, `%APPDATA%\OpenDejenol` | `%APPDATA%\OpenDejenol`, then `%TEMP%\mordor-crash.log` |
| `CHARSTAT.TXT` (Shift-F6) | beside the saves folder when the saves are not the per-user ones - `Desktop\mordor\CHARSTAT.TXT`, where the port has always put it - else the game folder, the original's "Root Mordor directory" | the game folder, then `%APPDATA%\OpenDejenol` |
| the pictures and the help | `%LOCALAPPDATA%\OpenDejenol\cache` (below) | - |
| the chosen game folder | `%APPDATA%\OpenDejenol\settings.ini` | - |
| the update check's choices (`[Updates] Check=`, `Prereleases=`; docs/RELEASING.md) | `%APPDATA%\OpenDejenol\settings.ini`, only read | - |

**An installed copy.** The installer (docs/RELEASING.md) puts the program in
`%LOCALAPPDATA%\OpenDejenol.Game` (the program itself in its `current\`), a folder of its own,
which an update replaces and an uninstall removes. Nothing above is in it: the saves, the
settings, the crash log, the cache and the mods beside the game folder stay where the table puts
them through updates and an uninstall alike.

**The per-user folders' old name.** Before the program was named OpenDejenol, the per-user
folders were `%APPDATA%\Mordor Port` and `%LOCALAPPDATA%\Mordor Port`. At start (not on a
`MORDOR_TEST` run) `UserFolders.MigrateAll` renames each old folder to `OpenDejenol` when the old
one is there and the new one is not, with everything in it - the launch settings, the per-user
saves, the crash log, the cache. Until that has happened, or when it cannot (a file in it held
open by another copy of the program), the old folder is the one read and written, so the chosen
game folder and the per-user saves are never lost. When both are there the new one is used and
the old one is left alone. The saves beside the game folder are in neither and do not move.
Checked on scratch folders in `selfcheck` (the game files section).

`MORDOR.INI` in the game folder is only read, never written (the original's own was in the WINDOWS
directory). DEVIATION, a port necessity: the original wrote CHARSTAT.TXT only into its own
directory, which under Program Files a player cannot write to; the port then writes it to the
per-user folder rather than lose it. `MORDOR_CRASHLOG` (the smoke test) names the crash log
outright. Checked with read-only scratch folders in `selfcheck` (the game files section).

## The installers (`InstallerUnpack`)

Mordor is sold today by Decklin's Domain as `MordorSetup.exe` (6,041,600 bytes; "Installer for
Mordor v1.1 The Depths of Dejenol"), and the demo is `MordorDemoSetup.exe` (2,761,728 bytes, from
<https://www.decklinsdomain.info/DsD/mordor1/MordorDemoSetup.exe>). Both are Tarma InstallMate 9
setups of 2016, and both refuse to run on Windows 10 and 11 ("Windows 10 (x64) ... not supported";
compatibility modes and version spoofing do not help - tried on a fresh Windows VM). So the port
reads the game out of the installer itself and never runs it. The format, as far as the port
needs it (worked out from MordorSetup.exe on 2026-09-13, and the same in the demo):

1. **The setup program** is a 32-bit Windows PE file. Its section `.tsuarch` (raw data at 0x25E00
   in MordorSetup.exe, to the end of the file) holds the archive: 16 bytes, then a `tiz3` header -
   its length (0x3C, a 16-bit word) at +4, the archive's whole length counted from the section's
   start at +0x10, and at +0x38 the five property bytes of an **LZMA** stream (`5D`, a 16 MB
   dictionary) whose data follows at +0x3D. The stream has no stated size; it ends with LZMA's end
   marker, exactly at the archive's length (5,886,082 bytes of LZMA in MordorSetup.exe, unpacking
   to 14,459,355).
2. **The unpacked archive** is a run of `tzf3` records, back to back to its last byte: a 0x40-byte
   header - the record's 8-byte key at +8, its length (64-bit) at +0x10, two FILETIMEs - then the
   record's bytes, stored whole. Records keyed `FFFF...0002`, `...0003`, `...0006`, `...0011`,
   `...0001` are the setup's own (the `tin9` project and its runtime); every other record is one
   installed file, keyed by an 8-byte hash.
3. **The project** (`tin9`, the first record) names them. A `file` entry: the record's key at +8,
   its folder's key at +0x2C, its size at +0x3C, and at +0x54 its name - a 32-bit length, the
   characters, a 32-bit zero - then the same way the name it was stored under (the name, "_",
   sixteen hex digits, some characters of the name as "_": `DI_ANIM.WAV_25C94EF79CE94451`). A
   `fldr` entry: its own key at +8, its parent's key and then its two names (an internal one -
   `INSTALLDIR`, `System16Folder`, `MUSIC_E1568F666122A428` - and the folder's own) after a run of
   fields that is 0x2C bytes long for the install's folders and shorter for Windows' and
   InstallMate's own. The port finds entries by their tags and takes one only when every field
   reads; the sizes must match the records'.

What MordorSetup.exe installs, read so: 305 files - into `INSTALLDIR` ("Mordor v1.1 The Depths of
Dejenol" in Program Files (32-bit)) MORDOR.EXE, the help (MORDOR.HLP, MORDOR.chm ...), MORDOR.WRI
and SUPPORT.WRI, readme.htm, UNINSTAL.TXT; into `DATA\` the five MDATA files and MG11.DLL; into
`MUSIC\` the 71 WAVs and 16 MIDs; `SSAVER\` the screen saver's 84 pictures; `html\` and `images\`
the 2015 edition's help pages and pictures (cityw1.png among them); the Visual Basic 3 runtime
(VBRUN300.DLL and the VBX controls, WAVMIX16.DLL) into Windows' 16-bit System folder; and
WININIT.INI into the Windows folder. `DATA\` and `MUSIC\` are where the original reads its data and
sounds (`BasePath + gc1C74` = "DATA\", `gc1C78` = "MUSIC\", MAIN.TXT). An empty `BACKUP\` folder is
declared too.

**What the port unpacks, and where.** Everything but WININIT.INI (the installer's instructions to
Windows), into `%LOCALAPPDATA%\OpenDejenol\game\full` for the full game or `...\game\shareware`
for the shareware - one folder per edition, told apart by `GameFiles.Check` on what was unpacked,
not by the installer's name, so the demo never replaces the full game (or the other way round) and
unpacking the same installer again replaces its own folder with the same files. The port reads all
its files from one folder, so `DATA\`, `MUSIC\`, `images\` and the VB3 runtime go into the top
folder - the layout of the developer's `game_files`, which MordorSetup.exe's files match byte for
byte (143 files by SHA-256; `selfcheck`); `html\` and `SSAVER\`, which the port does not read,
keep their folders. The folder is the port's own and holds no saves: they go to
`%APPDATA%\OpenDejenol\saves` as for any game folder with no `saves` beside it (below).

**Read as untrusted.** The installer is a file the player picked. The PE headers and section table,
the archive's length and header, the LZMA stream (the decoder stops at the end of its input and at
128 MB of output, and refuses a distance before the start) and every record are checked before use;
the installer is capped at 64 MB; every file and folder name must be a plain name (no `..`, no
separator, no drive, no character Windows refuses, no device name); the files are written into a
new `.unpacking-...` folder beside the edition folders, which is checked with `GameFiles.Check` and
only then moved into place (an earlier unpacking of that edition moved aside first and put back if
the move fails). Anything wrong is a message - "This file isn't a Mordor installer OpenDejenol can
read." and what to choose, the reason kept apart (`UnpackResult.Detail`) for the crash log - and
nothing is left behind.

**The LZMA decoder** (`Extraction/Lzma.cs`) is the decoding part of Igor Pavlov's LZMA SDK in C#,
public domain, vendored rather than taken as a package: the bounds and the size limit above are
the only changes.

**The demo, measured (2026-10-27).** MordorDemoSetup.exe is the same format and unpacks to 220
files: no `SSAVER\`, otherwise the full installer's files byte for byte but four. Its MDATA11.MDR is
68,166 bytes with **3** levels, as the 1995 shareware's, but not the same bytes (`65ff0d07...b84108d7`
against the 1995 file's `f2498923...f8373c14`); its MORDOR.EXE is 1,199,377 bytes like the full
1.1's but differs from it in 1,200 bytes (`15cd6021...d4ddb1e7`), and is not the 1995 shareware's
either. `GameFiles.Check` reads it as the shareware ("its dungeon has 3 levels"), played as the
limited build. readme.htm and UNINSTAL.TXT are the demo's own.

## Known follow-ups

- The pictures and the help are no longer in the build (below), but git history still holds the
  old `Mordor.App/Assets`, `help.json`, `wallart/` and `docs/original-help/` (last in 498ef89): a
  public repository needs a fresh history. `decompiled*/` (the decompiled program) is still in the
  developer's tree, as are the game's own strings that the port's code reproduces; the public
  tree is made without the decompiled program and with a history of one commit.
- The limited build's About date and order-form price are read from the player's own MORDOR.EXE
  (`LimitedVersion.ReadProgram`, since 0.1.1): "7/20/95" and $39.99 from the 2016 demo's
  (MordorDemoSetup.exe), whose help is the 2015 edition's too ("M1: Depths of Dejenol Help
  Lesson"); "7/6/95" and $39.95 from the 1995 shareware's. The date is found as the length-counted
  string after "Welcome to MORDOR 1.1!" and "Mordor" in the program's string constants, the price in
  the order form's sentence "...at any time for only $NN.NN + S&&H!"; where either is not found,
  the 1995 shareware's is shown. Measured on the demo's and the full 1.1's programs; the 1995
  shareware's MORDOR.EXE was not at hand for 0.1.1 (it is inside MORDOR11.ZIP's 16-bit setup
  archives), so for it the reading rests on the layout measured on 2026-10-25 (above: "7/6/95" at
  byte 623888 after the same two strings, the price at 568050 in the same sentence), and the
  fallback is that file's own pair. `selfcheck` expects each copy's own: its date and price by its
  MORDOR.EXE, its help's title by its MORDOR.HLP.

## The game's pictures and help: read from the player's copy

Since October 2026 the port carries none of the game's pictures or help. On first run - and
whenever the game files change - `src/Mordor.Core/Extraction/GameArt.cs` reads them out of the game
folder into a per-user cache, `%LOCALAPPDATA%\OpenDejenol\cache\<key>` (`MORDOR_ART_CACHE` moves
it; the checks use scratch caches). The key is a hash of the extractor's version and of every
source file present, so a cache is only ever used for the files it came from; it is written to a
temporary folder and renamed into place when complete. `src/Mordor.App/Art.cs` loads every picture
from there (`{app:Art name.png}` in a form); nothing falls back to a copy inside the program.
`Mordor.Tools <game> extract [dir]` writes the same files for a look.

| file in the cache | from | how |
|---|---|---|
| maze_corridor, maze_overlays, maze_backdrop, maze_dark, maze_water, maze_quicksand | MG11.DLL resources MORDOR/MAZE, DOOR, BLNK, FOG, WATR, QSND | NE resource table, the .BMP inside |
| automap-pics, combat-misc, asmodeus, monsters | MG11.DLL MORDOR/PICS, MISC, ASMO, MONS | MONS is RLE8-compressed |
| menu_background, credits_david, town_exit | MORDOR.EXE: the picture in VB3 form frmMenu, frmCredits, frmCityMain (first) | a form is an RCDATA resource followed by its names table; its pictures are whole .BMP files |
| tool_page ... tool_help (9) | MORDOR.EXE: frmMain's nine 20x20 pictures, in form order | page, join, automap, control panel, drop body, pick up, give, experience, help |
| about_icon | MORDOR.EXE RT_ICON 32x32 | the AND mask is the transparency |
| town_store, morgue, guild, dungeon, confinement, seer, bank | 103x75 crops of the City window's screenshot: `cityw1.png` (the 1.1 install), else the identical `CITYW1.BMP` inside MORDOR.HLP | else frmCityMain's own 120x87 pictures, scaled nearest-neighbour |
| help.json | MORDOR.HLP | `WinHelpFile` + `HelpBundle` |

Every picture is pixel for pixel the one the port used to carry (selfcheck holds their hashes,
and compares the files of 498ef89 when git can show them). The WinHelp reader handles the 3.1
file system, B+ trees, `|SYSTEM`, `|TOPIC` (LZ77 and phrase compression, display and table
records, jumps), `|CONTEXT` and `|TTLBTREE`. `|SYSTEM`'s CONTENTS record (type 3, a topic offset)
names the topic WinHelp opens the file at for HELP_CONTENTS, which is what the original's Help
asked for (FRMMAIN.BAS:75-76); help.json marks that topic (`"Contents":true`) and the help window
opens there when no topic is asked for, the first topic in a file without the record. It is
Welcome in both releases measured: offset 0, the first topic, in the full game's MORDOR.HLP, and
0x26896D, the last of its 73 topics, in the shareware's (whose first is AutoMap). The art cache's
key carries the extractor's version (`GameArt.Version`, 2 since the mark), so a cache made before
it is made again. The help used to be bundled from the CHM that the 2015
edition's help tool generated beside MORDOR.HLP; made from the .HLP it has the same 73 topics,
titles, text, line layout, tab stops and 103 jumps, except:

- **Spells**: the .HLP lists the sixteen spell classes in two tabbed columns (as WinHelp showed
  them); the CHM's export had one list. Same words, same jumps.
- **U+FFFD**: the old bundle had 12 (bytes the CHM held that did not decode). The .HLP has a
  copyright sign for two of them, and nothing at all for the other ten (trap-gauge symbols in
  Encounters: Treasure and the "Nothing Chosen" symbol in Encounters: Combat), so those quotes are
  now empty, as they were in WinHelp.
- Measures: the .HLP stores indents and stops in tens of twips. The export's points are recovered
  with one rule (v -> 10v - 3 twips, truncated) and table column edges with another (half the
  running width-plus-gap, the row's start subtracted from each span as the export did); both
  reproduce every stop of MORDOR.HLP 1.1, but they are fitted to it, and another release's help
  may come out a point off here and there.

**What must be there.** MORDOR.EXE (already required) and MG11.DLL: the original itself stops
without MG11.DLL ("Error! Cannot locate graphics file ... Halting Program", MODULE12.BAS:71-75),
and so does the port - the first-run window comes back with the reason, as it does for an
MG11.DLL that holds none of the pictures. Anything else missing or unreadable becomes a blank
picture of the right size with a note in the crash log, and the game goes on; no MORDOR.HLP means
the help window says the help is unavailable.

**Shareware and other releases.** Measured on the real shareware (above): its MG11.DLL is the full
game's, and every one of the 30 pictures comes out of it and its MORDOR.EXE pixel for pixel the
one the port used to carry (selfcheck), with nothing missing. It has no `cityw1.png`, but its
MORDOR.HLP carries `CITYW1.BMP` as the full game's does, so its city buttons are the same pictures.
Its help is its own MORDOR.HLP: another file from the full game's, titled "Mordor HelpLesson",
with the same 73 topic titles and 103 jumps but its own text, layout and topic order (AutoMap first,
Welcome last, and Welcome its Contents topic, where the help window opens). Another release whose
picture is not where the extractor looks gets the blank-and-note case above, not a failed start;
one with neither `cityw1.png` nor `CITYW1.BMP` gets the program's own city pictures scaled down.

## Importing the original's saves

A player who has played the original can bring the game across: **Options, File Backups, "Import
Original Saves..."** (from the main menu only, as Backup and Restore are). It reads the original's
`DATA` folder - or the game's folder with `DATA` inside it, as `BasePath + "DATA\"` (gc1C74) - and
writes the port's JSON into the saves folder in use.

Code: `src/Mordor.Core/Import/VbLayout.cs` (the record types and each file's `Len`),
`src/Mordor.Core/Import/OriginalSaves.cs` (reading, field by field into the port's model),
`src/Mordor.Core/Import/SaveImport.cs` (the plan, the backup, the writing), `src/Mordor.App/ImportWindow.cs`
(the folder picker, the preview, the questions). Checks: `selfcheck`'s "importing the original's
saves" sections, `Mordor.UiCheck`'s "the Import Original Saves preview", and the probe
`Mordor.Tools <game> import [datadir]`, which prints every character and note.

### The safe way round

- The original's files are only read (opened read-only, never written; the checks compare their
  sizes and times before and after).
- Nothing is written until the preview's **Import**. The preview lists every character by name
  with race, guild, hits, gold and where they are, the world files, the remembered parties, and
  what the import has to say (below).
- Before anything is written, every file at the top of the saves folder is copied into
  `BACKUP\before-import-yyyyMMdd-HHmmss` inside it - a folder the Options window's **Restore** can
  bring back (it holds character files, which is what Restore looks for). A copy that fails stops
  the import before anything is written.
- **Characters** are written one file each. A character whose name the saves folder already has
  is unticked in the preview, and ticking it is asked about again, by name ("Replace
  Character?", No the default), before that one file is replaced. The rest are left alone. New
  characters go into `roster.json` in the original's record order.
- **The shared world** - automap, rooms, store, Library, Hall of Records, guildmasters, pens, guild
  log - is one tick box, ticked only when the saves folder has none of those files yet: importing
  it replaces the port's (the backup keeps them). A fresh saves folder is therefore the simplest
  case: everything is new and ticked.
- A remembered party (MDATA12) is written as `party-<leader>.json` when its leader is imported.

### Which file holds what

The `Open` lines of the decompiled modules (gc1C10 to gc1C44, MAIN.TXT:580-593), checked by
selfcheck against every `Open ... For Random ... Len =`:

| file | Len | holds | the port's file |
|---|---|---|---|
| MDATA4 | 2900 | record 1 the version "1.1", record 2 the count, then a T5810 per character; a deleted one is blanked in place (mdrmazGetChars, fn04ED) | `<name>.json` each, `roster.json` |
| MDATA6 | 16 | version, MDATA3's item-data stamp, count, then a T5755 shelf per item table index: Align(0-3) = U G N E counts | `store.json` (`"itemId:align"`) |
| MDATA7 | 128 | version, then per guild a T564C: a variable-length name and a level | `guildmasters.json` |
| MDATA8 | 10 | version, the deepest level anyone reached, then per level 900 Longs, one flag word per square, `(L-1)*900 + x + 30*(y-1) + 2` (fn0A34) | `automap.json`; the deepest level into `dungeon.json` |
| MDATA9 | Binary | Integer count, then entries: Integer guild, Long day, Integer length, text (sub0578, HALL.BAS:25-50) | `MDATA9.MDR` in the port's own layout |
| MDATA10 | 28 | record 1 = 15 levels, records 2-16 each level's respawn tally (fn0DDC), then per level 200 areas of four T4E8B Foes and a T4F0B Fight (fn0E38) | `dungeon.json` (`Rooms`, `Tally`) |
| MDATA12 | 35 | an Integer count, then four variable-length names a party, the leader's first (fn179B) | `party-<leader>.json` |
| MDATA13 | 100 | version, monster count, item count, monsters found, items found, then a T5BF1 per monster table index and per item table index (sub0F9A) | `library.json` (`"mr:index"`, `"i:itemId"`) |
| MDATA14 | 45 | a T73E9 per record, record k+1 for record k (fn1C07); sub0595 writes record 1 over the version stamp | `records.json` |
| MDATA15 | 16 | version, MDATA5's stamp, count, then a T5755 pen per monster table index: Align(0-2) = Good, Neutral, Evil | `confinement.json` (`"Ref:align"`, align one more) |

MDATA1, 2, 3, 5 and 11 are the game's tables; the port reads its own game folder's, and the saves
are read against them (item and monster table indexes). A DATA folder whose MDATA3 or MDATA5 count
differs from the game folder's is warned about; the store, pens and Library are then matched by
Id, as the original re-maps them (fn0425, mdrUpdateLibraryFile). `MDATA10.MDR<n>`, which fn0F88
can open for another level, is a path no build of the game takes (gv1AEC is never above nought
there) and is not looked for. Any file stamped with another version than "1.1" is refused, as
fn0326 refuses it.

Two things the port had been said to do and does not: `Roster.cs` does not walk MDATA4 - it
stands in for MDATA4's record order, so the import writes it; and the port's `MDATA9.MDR` is not
the original's layout - same fields, same order, but Int32 counts and lengths, an Int64 day and
UTF-8 text (`GuildLog`), so the import converts the original's log rather than copying it.

### How a record is read

VB3 random files pack a type's fields with nothing between them: Integer 2 bytes, Long and Single
4, Currency 8 (ten-thousandths), `String * n` its length padded with spaces, little endian, arrays
from 0 so `Pack(40)` is 41 slots. Record n is at `(n - 1) * Len` and only as long as its type, so
the last record of a file is short: MDATA4's last is 2887 bytes, the size of T5810, and every
file's last record is checked to be exactly its type's size. Every field name in `VbLayout` is
MAIN.TXT's own, and selfcheck compares each type field for field with MAIN.TXT. Offsets counted
by hand are checked too, among them GuildsJoined at 1678 in a character record, the offset seen
when a copy of MDATA4 was edited.

### A character, field by field (T5810)

| record | port | notes |
|---|---|---|
| Name (String*30) | `Name` | RTrim |
| RaceIndex, CurrentGuild, GuildsJoined(0-15) | `Race`, `Guilds` | by name; Level > 0 is joined; the current guild first, the rest by index. A race or current guild not in MDATA1 refuses the character |
| GuildsJoined(g).M579B, M55CB, M57A9, M57B2 | `Experience`, `QuestType`, `QuestTarget`, `QuestDone` | the current guild's experience is the character's own M579B, copied into its slot only when the guild changes (FRMCTYG.BAS:148-150) |
| GuildsJoined(g).Att, Def | - | the port works guild A/D out from the level; a record that differs is warned about |
| Alignment, M5829 | `Alignment`, `Sex` | 0 Good 1 Neutral 2 Evil; 0 Male 1 Female |
| Age (Single) | `Age` | whole days |
| Stats(0-5), StatMods | `Stats` | StatMods is worked out from the gear; a difference is warned about |
| Hits, MaxHits, SpellPoints | `CurHits`, `MaxHits`, `CurSpellPoints` | `MaxSpellPoints` is (INT + WIS) * 5 |
| DungeonLevel, X, Y, Facing, M59BB | `DungeonLevel`, `X - 1`, `Y - 1`, `Facing`, `Area` | X and Y are 1-based in the record, 0-based in the port |
| Sanctuary, M59F4(0-2) | `SanctuaryX/Y/Level`, `LostLevel`, `LostPosition`, `LostFacing` | |
| Gold, BankGold (Currency) | `GoldOnHand`, `GoldInBank` | to the ten-thousandth (`VbCurrency`) |
| Pack(1-39) | `Pack` | Ref < 1 is empty; the item is the table's `Items(Ref).Id` (a disagreeing Id is warned about); Equipped, Charges, Alignment (U G N E), Cursed (the slot's own), IdLevel counted down: 0 = All, 3 = Barely |
| M5860(0-39) | `Bank` | holes kept, trailing empties dropped |
| Companions(0-3) | `Companions` | Ref > -1 is a companion; Ref, M57D4 (Id), Name, Hits, MaxHits, Alignment + 1, IdLevel, BindingLevel; an empty slot keeps what its last companion left |
| M5943 | `CarryingCompanion` | 0-3; a carried body (4 and up) is put down, as fn04CD does when the record is next loaded |
| Ailments(0-6) | `PoisonLevel`, `DiseaseLevel`, `ParalysisLevel`, `Levitating`, `InvisibleNow`, `SeeingInvisible`, `Protection` | |
| SpellEffects, SpellResistances(0-10) | `SpellEffects`, `SpellResist` | |
| DeathState | `IsDead`, `CauseOfDeath`, `Stoned` | the port writes 0 for an ordinary death (DeathStates) |
| M58DC, M58E9, M58F9 | `RaiseKind`, `RaiseChance`, `RaisedBy` | a raise waiting for an answer |
| M5906(0-6) | `Kills`, `Deaths`, `Complications`, `MonsterQuestsDone`, `ItemQuestsDone`, `PlaySeconds`, `CreatedOn` | |
| M5914(0, 2-4), M5914(5) | `Options`, `CombatSetting` | |
| M594C | `Password` | RTrim; none is the lone space |
| M5958(0-19) | `Windows` | twips / 15 = pixels |
| M59B0(0-9), M56AF | `Buffers`, `Swings` | |
| Att, Def | `ADShift` | the running totals less base, best guild, stats and gear; kept when Att and Def agree, warned about when not |

Worked out by the port and not imported (a stored figure that differs from the port's is a note):
M5700 and M586D (hands and wear counts), M5997 (weapon slots), M5970 (the experience cap),
M59A1 (the A/D interval cache), M5962 (the record number while loaded), ItemEffects,
ItemResistances and Resistances. Fields nothing reads (M56F7, M587C, M588A, M58A6, M59CB, M59D9,
Stats(6), Ailments(7), M5906(7-8), M59B0(10), M5958(20), Pack(40), M5860(40), Companions(4)) are
noted when not empty. Per companion, the record's Att and Def: the port uses the monster's.

### The world, and what has no place in the port

- **Rooms**: every area with any record written is imported whole (the four Foes and the Fight:
  groups, hits, the Single time stamp, the chest's gold to the ten-thousandth); the respawn
  tallies too.
- **Automap**: a flag word per square. The been-here bit (gc1D50, 23) becomes the port's explored
  squares; the rest of the word its recorded flags, and the same bits its "already worked out"
  set (the original asks one word both questions, mdrmazPerceiveSquare). The row stride is 30,
  every level's width.
- **Library**: each record with anything in it, by monster table index or item Id. Records 4 and
  5, the counts of monsters and items found, have no place in `library.json` (the port does not
  keep them) - a note.
- **Hall of Records**: the twelve records; the thirteenth, "Nothing", is not one.
- **Guildmasters**: name and level per guild; a guild with no name is left to the port's own
  appointment.
- Not in the DATA folder, and not imported: MORDOR.INI (the original's options, in the WINDOWS
  folder) and BACKUP.

### What the import says

Every value it cannot keep as it is, or that does not agree with the game data, is a note:
**Info** (expected - a field the port works out, a stale copy the original also ignores),
**Warning** (kept as said, worth a look) and **Refused** (not imported: a file of another version,
a character whose race or current guild is not in MDATA1). The preview shows them all.

### Tests

- Always: the layouts against hand-counted offsets; a DATA folder built byte by byte at those
  offsets (two characters, a deleted record, one past the count, every world file) read back and
  written into a scratch saves folder, field by field, then loaded by the port's own code;
  importing over it again (the backup, a character not confirmed left alone).
- With the decompiled source: every type against MAIN.TXT, every file's `Len` against every
  `Open`.
- With `MORDOR_ORIGINAL_DATA` naming a copy of an original DATA folder (never committed): sizes,
  an import end to end, nothing refused and no warning, value plausibility for every character, a
  second walk of MDATA4 by hand, the level at offset 1678, each character loaded and saved again
  unchanged, every world file read back. Without it these print "skipped" and are not counted.
- On screen against the original under OTVDM: a written procedure (private:
  `docs/IMPORT-GROUND-TRUTH.md`).

Imported from the developer's own original DATA folder (a copy): one character, Probe (Human Nomad
1, 15/15 hits, 71 gold, in town, a Leather Cap worn), 54 store shelves holding 1,654 items, 10
pens holding 21 creatures, the twelve guildmasters, 13 squares of level 1 walked, 4 rooms with
records, 10 creature and 16 item Library records, the twelve Hall records and 15 guild log
entries - with one note (the Library's found counts) and no warning.
