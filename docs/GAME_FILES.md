# The player's game files: full version or shareware

The port is an engine. It ships none of Mordor's data and reads the files of the player's own
installed copy of *Mordor: The Depths of Dejenol*. Without that copy there is nothing to play -
that is what keeps it to players who have the game, not a check in the code. This is the first step
towards an open-source engine that players run against a copy they have.

It plays either release:

- the **full** (commercial) release, as the full MORDOR.EXE plays;
- the **shareware** ("PUBLIC Limited Version"), as the limited MORDOR.EXE plays - its three
  levels, its dead chutes, its caption and boxes (below).

The only thing that keeps the game from starting is the files it needs being missing or unreadable.

Code: `Mordor.Core/GameFiles.cs` (`GameFiles.Check`, `GameFolder`), `Mordor.Core/LimitedVersion.cs`
(the limited build's behaviour), `Mordor.Core/Saves.cs` (the saves folder),
`Mordor.App/GameFolderWindow.cs` (the first-run window), the start-up in `Mordor.App/App.axaml.cs`.
Checks: `selfcheck` ("the game files and the limited build") and `Mordor.UiCheck` ("the first-run
window").

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
| `gv0180` | "PUBLIC BETA ... only n dungeon levels" notice, writes `Limited=True` to MORDOR.INI | MODULE1.BAS:932-940 | never set |
| `m002E` | "Limited " before the version on the splash | FRMWELCO.BAS:14-16, a Static | never set |

Every one of those strings is still in the full MORDOR.EXE (checked with `strings`): the VB3
compiler kept both branches. So there is no string in the program that marks a release, and the
constants live in p-code that only the decompiler reads. The program is not a usable test.

`[Loading Information] Limited` in MORDOR.INI is not one either: only the beta branch above ever
writes it (and only `True`), the full build never clears it, so a player who once ran such a build
keeps `Limited=True` in the Windows folder's MORDOR.INI for good. (The user's own install has
`Limited=False` in its folder's MORDOR.INI, written by its installer, not the game.)

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

The full release's data, measured on the user's install (Mordor 1.1 from MordorSetup.exe, Tarma
InstallMate 9):

| file | holds | full 1.1 |
|---|---|---|
| MDATA11.MDR (339,606 bytes) | dungeon; first word = number of levels | **15** levels |
| MDATA5.MDR (64,623) | monsters; `LevelFound` per monster | deepest 16 |
| MDATA3.MDR (46,103) | items; `Floor` per item | deepest 16 |
| MDATA1/2/3/5 | open with a version string | "1.171" |

No shareware copy was to hand to measure, so the shareware side rests on the texts above: three
levels, and the monsters and items that go with them.

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
   shareware (`Limited`) - **played**, as the limited build.

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
| no "THANK YOU FOR PLAYING MORDOR!" box on the way out | `OriginalBox.ThankYou` (the menu's Exit, a session end) |
| the "Public Version" box (MsgBox 64) once a run, before the menu's own welcome | `TitleWindow` on opening. The original shows it before its frame comes up; the port has no frame apart from the menu, so it comes over the menu |
| a level past 3 asked for: "LIMITED VERSION: Request to go to Level n is Denied." in the message list, and level 3 instead. Levels past 15, or below nought, are left alone as fn0A0B leaves them | `DungeonWindow.ChangeLevel` (stairs, teleporters, Sanctuary); a chute's landing, a line for each member who goes down, as sub08F5 calls fn0A0B for each (MAZEMOD.BAS:114-115); the Teleport spell clamps before its fall and rock tests, as fn0A0B comes before them (MAINMOD2.BAS:5823). Stairs down on level 3 still go to fn0A0B, which denies them |
| TAKE on a chute on level 3: "You cannot take this Chute in the Limited Version." | `DungeonWindow`'s TAKE |
| a chute walked onto on level 3: "There is a chute here, but it doesn't do anything in the Limited Version.", and the square routine goes on | the square routine |
| a teleporter record whose Z2 is past 3: "..but it doesn't go anywhere in the limited version...", nobody moves, and the square routine goes on | the square routine |
| About shows "Ordering Info", which opens "Mordor Ordering Information" (frmOrderInfo, laid out from FRMORDER.FRM, text from FRMORDER.BAS) | `AboutWindow`, `OrderInfoWindow` |

**Values, and which are inference.** The full build's `mc015E` and `mc0624` are 15; the limited
build's are taken as **3**, the shareware's depth in MORDOR.WRI ("limited to 3 dungeon levels") -
in the full build both equal the deepest level, and the shareware's deepest is 3. `m009C` and
`m0622` are constants that are 0 in the full build and so True (-1) in the limited one. `m02A6` and
`m0032` are variables nothing assigns in the full build's decompile; they are taken as on in the
limited build, since their only effects are lines that say "limited version" and the order form.

**Not switched**, on purpose: the `gv0180` "PUBLIC BETA" notice and its `Limited=True` write - a beta
build's flag, whose text names `gc0182` = 15 levels, not the shareware's three; and frmWelcome's
"Limited " - the port has no splash screen.

## Where the port looks (`GameFolder.Resolve`)

1. `MORDOR_GAMEDIR` (test hooks, check.sh's start-up smoke, or anyone who wants to say it outright).
2. `[Loading Information] GameDir=` in the launch settings,
   `%APPDATA%\OpenDejenol\settings.ini` (`MORDOR_LAUNCH_INI` replaces the path, for tests).
   This cannot be the saves folder's `settings.ini`, where the port keeps its other settings:
   the saves folder can depend on the game folder (below).
3. Found:
   - a `game_files` folder beside the port or beside any folder above it - as
     `Desktop\mordor\game_files` sits beside `Desktop\mordor\port` - or the port's folder itself;
   - `[File Information] BasePath` in MORDOR.INI in the Windows folder, which is how the original
     finds its own data (MAINMOD.BAS:1193-1196);
   - an uninstall registry entry whose DisplayName contains "Mordor" (its InstallLocation, else
     the folder of its icon or uninstaller);
   - `Mordor`, `Mordor v1.1`, `Mordor v1.1 The Depths of Dejenol` ... under Program Files (both)
     and `%LOCALAPPDATA%\Programs`, `C:\MORDOR`, `C:\GAMES\MORDOR`. (MordorSetup.exe's default
     folder could not be read out of its compressed payload, so these are guesses; the picker
     covers the rest.)

1 and 2 are the player's word: a folder named there that cannot be played is reported on the
first-run window, not silently passed over; a shareware one is played. Found folders are tried in
order; the first full one wins, and with none the first shareware one.

## First run

With no Mordor to play, the menu is never made. A small window comes up instead, headed "Mordor
game files not found", saying that the port plays the player's own copy, that it needs an installed
Mordor with MORDOR.EXE and the MDATA files, that the shareware plays as the shareware did with its
three levels, and what was wrong with the folder found. "Locate Mordor..." opens a folder picker;
the folder picked is checked again (its `game_files` too, if the folder above was picked by
mistake). One whose files are there and read - full or shareware - is written to the launch
settings and the menu comes up; anything else keeps the window up with the reason. "Exit" quits.

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

## Known follow-ups

- The pictures and the help are no longer in the build (below), but git history still holds the
  old `Mordor.App/Assets`, `help.json`, `wallart/` and `docs/original-help/` (last in 498ef89): a
  public repository needs a fresh history. `decompiled*/` (the decompiled program) is still in the
  developer's tree, as are the game's own strings that the port's code reproduces; the public
  tree is made without the decompiled program and with a history of one commit.

## The game's pictures and help: read from the player's copy

Since October 2026 the port carries none of the game's pictures or help. On first run - and
whenever the game files change - `Mordor.Core/Extraction/GameArt.cs` reads them out of the game
folder into a per-user cache, `%LOCALAPPDATA%\OpenDejenol\cache\<key>` (`MORDOR_ART_CACHE` moves
it; the checks use scratch caches). The key is a hash of the extractor's version and of every
source file present, so a cache is only ever used for the files it came from; it is written to a
temporary folder and renamed into place when complete. `Mordor.App/Art.cs` loads every picture
from there (`{app:Art name.png}` in a form); nothing falls back to a copy inside the program.
`Mordor.DataDump <game> extract [dir]` writes the same files for a look.

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
records, jumps), `|CONTEXT` and `|TTLBTREE`. The help used to be bundled from the CHM that the 2015
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

**Shareware and other releases.** No shareware copy was to hand, so this is reasoned, not
measured. The shareware is built from the same source (its "limited" switches are constants), so
it should hold the same forms and resource names; it needs MG11.DLL as the full game does (sub10C5
loads it at start, whatever the build), and its Help opens `App.HelpFile`, MORDOR.HLP, as the
full game's does. If a picture is not where the extractor looks, it is the blank-and-note case
above, not a failed start. What a 1995 release would lack is the 2015 edition's
extras - `cityw1.png` and the help's `CITYW1.BMP` - so its city buttons are the program's own
pictures scaled down, and its help is whatever its MORDOR.HLP holds.
