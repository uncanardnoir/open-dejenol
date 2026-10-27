# The checks and the developer's tools

Everything here reads the player's own copy of Mordor (docs/GAME_FILES.md) and never writes into
it: pass the game folder as the first argument. None of it opens a window on screen.

| Project | What it is | Run it |
|---|---|---|
| `tests/Mordor.Tests` | the test suites: the checks that assert, and whose exit code means something | `Mordor.Tests <gamedir> <suite>` |
| `tests/Mordor.UiCheck` | the window checks: real controls laid out and clicked in Avalonia's headless platform | `Mordor.UiCheck <gamedir>` |
| `tests/Mordor.Tests.Runner` | the suites and the window checks under `dotnet test` | `MORDOR_GAMEDIR=<gamedir> dotnet test` |
| `tools/Mordor.Tools` | data dumps, rule probes and simulations that print and never assert | `Mordor.Tools <gamedir> <tool> [args]` |
| `tools/check.sh` | the full gate: builds and runs all of the above but the runner, and ends in `all good` | `sh tools/check.sh <gamedir>` |
| `tools/pack-release.sh` | the release packages (installer, update package, portable zip): no game folder, nothing uploaded (docs/RELEASING.md) | `sh tools/pack-release.sh <out folder>` |

`dotnet run --project tests/Mordor.Tests -c Release -- <gamedir> selfcheck`, or
`dotnet run --project tools/Mordor.Tools -c Release -- <gamedir> lairs`, runs one from the source.
`help` as the name (or no arguments) lists a project's suites or tools.

## The layout

    Mordor.sln
    src/      Mordor.Core (the rules, no UI), Mordor.Modding (the mod API and loader),
              Mordor.App (the program, OpenDejenol.exe), Mods/ (the bundled mods, ShipMods.targets)
    tests/    Mordor.Tests, Mordor.UiCheck, Mordor.Tests.Runner
    tools/    Mordor.Tools, NeRes, NameMap, VbToCs, check.sh, gen-mazeselect.sh, pack-release.sh
    docs/     this and the rest
    .github/workflows/   CI (build and test) and the release from a tag (docs/RELEASING.md)
    .config/dotnet-tools.json   the pinned Velopack CLI (vpk) the release packs with

The checks read the port's own source from the tree they were built in: `PortTree.Root` (in
Mordor.Tests) is the nearest folder above the build that holds `Mordor.sln`.

## The gate: tools/check.sh

`sh tools/check.sh <gamedir>` builds the suites, the tools, the window checks and (unless
`MORDOR_CHECK_NO_UI=1`) the app, then runs, in order:

1. `Mordor.Tools gamefolder` - the folder as the app's start sees it; with no Mordor to play it stops there;
2. `Mordor.Tests selfcheck` - every number that has a source to be checked against;
3. `Mordor.Tests mods` - the mod system and the bundled mods;
4. `Mordor.UiCheck` - the windows, headless;
5. the sweep - every tool below not marked otherwise, and `Mordor.Tests citations`, for a crash
   (the list is the tools' own table: `Mordor.Tools --sweep-list`);
6. the start-up smoke - a Release copy of the app in town and in the dungeon, ten seconds each.

It prints `all good` as its last line only when nothing failed. Without the decompiled source
(`decompiled/`, `decompiled-named/`) or the saved dejenol.com pages, as in the public tree, the
checks that read them print `skipped: decompiled source not present` and are not counted.

## dotnet test

`Mordor.Tests.Runner` puts the suites under the standard tooling - `dotnet test`, an IDE's test
explorer, a CI runner - without rewriting a check: each of its four tests runs one suite as its own
process from the folder that suite built into, and fails on a non-zero exit or any `FAIL` line,
listing those lines under their sections' headings. The suite's whole output is the test's output.

| Test | Runs |
|---|---|
| selfcheck | `Mordor.Tests <gamedir> selfcheck` |
| mods | `Mordor.Tests <gamedir> mods` |
| citations | `Mordor.Tests <gamedir> citations` |
| windows | `Mordor.UiCheck <gamedir>` |

The game folder comes from `MORDOR_GAMEDIR`, the variable the app itself reads. Without it every
test is skipped with a line saying so:

    MORDOR_GAMEDIR=/path/to/Mordor dotnet test                       (POSIX shells)
    set MORDOR_GAMEDIR=C:\path\to\Mordor                             (cmd, then dotnet test)
    $env:MORDOR_GAMEDIR = "C:\path\to\Mordor"; dotnet test           (PowerShell)

`dotnet test -c Release` tests the Release builds. It does not replace `tools/check.sh`, which also
sweeps the tools and starts the app.

## Mordor.Tests: the suites

| Suite | What it checks |
|---|---|
| `selfcheck` | the numbers that have a source to be checked against: dejenol.com's formulas, the help, the game data, the forms in MORDOR.EXE, the decompiled source, the port's own code where a rule lives in it |
| `mods` | the mod system: the loader, the default answers, the bundled QoL, Kaizen, DPS Meter and Cartographer mods (docs/MODDING.md) |
| `citations` | every `FILE.BAS:LINE` citation in docs/FIDELITY.md and the code, against the routine it names (selfcheck asserts the same count; this lists each problem) |

Each prints one `ok` or `FAIL` line per check and exits 1 when anything failed. Some analyses the
suites assert on also have a printing tool in Mordor.Tools (`castgate`, `commands`, `fate`,
`formfonts`, `import`, `spellhelp`, `stateleak`); their code lives in Mordor.Tests, which
Mordor.Tools references.

## Mordor.Tools: the tools

`Mordor.Tools <gamedir>` alone prints the summary. Tools marked *no sweep* are left out of
check.sh's sweep: they write files, need arguments, or take long.

### The game folder and its files

| Tool | What it prints |
|---|---|
| `gamefolder` | the folder as the app's start sees it (GameFiles.Check): exit 0 for a Mordor to play, 2 for none |
| `summary` | the data files parsed: a few spells, items and monsters, every floor's size, level 1 as an ASCII map |
| `extract [outdir]` | the pictures and the help read out of the game folder as the app does (GameArt), written to `../extracted-art/cache` - *no sweep* |
| `import [datadir [charstat-folder]]` | an original DATA folder (or `MORDOR_ORIGINAL_DATA`) read through the importer: every character and note |
| `mdata1` | MDATA1 walked the way the game walks it (fn03D0): the version, the counts, every race, guild and class record |
| `formfonts` | every control's design-time font and Tag in the forms MORDOR.EXE carries |
| `audio` | every WAV and MID in the game folder, opened through the Windows multimedia API without playing anything |
| `autoload <savesdir> <leader>` | Party.LoadFor against a saves folder: what mdrmulAutoLoad says, refusals and all - *no sweep* |
| `save` | a character round-tripped through the JSON save (in the temp folder), automap and all |

### The data, against the community's tables and the help

| Tool | What it prints |
|---|---|
| `lairs` | every lair (area LairId) against the community's Laired_Monster_List |
| `rarity` | the monsters' Rarity field against the community's Rare_Monsters page |
| `racetable` | the race table as loaded (stats, alignments, size, resistances, guilds), laid out like the Races page |
| `traptable` | MDATA5's TrapFlags against the community's Monster_Trap_Table |
| `adcheck` | Rules.GuildAD against every value in the community's A-D_For_all_Guild_Levels tables |
| `rulescheck` | the ported formulas (A/D, experience, gold to level, hit chance) beside the numbers dejenol.com quotes |
| `drops` | the two fields item drops hang on (ItemChance, ItemDropLevel) and the deep-dropping monsters |
| `classdump` | the wear classes out of MDATA1 and the maximum each allows |
| `mflags` | which monsters carry each special attack, special property and spell flag bit |
| `spells` | the spell table by school, every field of every record |
| `areas` | the spawn areas of floors 1-3: kinds allowed, lair monster, what each floor can spawn |
| `teleports` | every teleporter in the dungeon and where it puts you, rock and off-map destinations marked |
| `shadow` | which monsters can be drawn at all, and from which floor (the encounter window, Stud squares) |
| `deepest` | the fifteenth floor against the two shelves that stock from the deepest level reached |
| `store` | the store's fixed-price goods (Floor 0) by the original's own test |
| `price [name]` | modItemCost's price at stock 1-3 for the items whose name contains `name` (every item without one) |
| `orphans` | the two spells no guild reaches (Fate, Life Domination): whether anything else casts them |
| `reach` | every spell effect the data uses against what the port's dispatcher handles |
| `castgate` | the COMBAT / NON-COMBAT label computed for every spell (fn149E) against the help's own label |
| `spellhelp` | MDATA2 and MDATA1 against the header the help prints above every spell |
| `helpquotes` | every passage the docs quote from the help, checked against the help itself |
| `commands` | the help's Commands page against the keys the port's handlers bind |

### The rules, exercised

| Tool | What it prints |
|---|---|
| `rooms` | how many rooms on floors 1-3 hold monsters, the group sizes, the respawn delay |
| `respawn` | when a cleared room fills again (sub0A91) and the oddities of the original's midnight timer |
| `map` | what the automap is told against what is really there (fn2EB5, medPlotMaze) |
| `maze` | the view selector walked over every open square of level 1 in all four facings |
| `lost` | being lost on a rotator square: the chance, and the steps to work out where you are |
| `perceive` | noticing the square you are on (sub087C): how often, how right, how sure |
| `join` | hostility and the offer to join: how often, and what accepting does |
| `misid` | mis-identification of a creature: how often, as what, for how long |
| `monsterattack` | every monster's whole turn (sub0E85) run against a durable character: each special attack reached |
| `acid` | who an acid attack lands on, and the two companion-slot faults the original's draw has |
| `drain` | which stats a drain attack can take (never Strength) |
| `classspells` | the spells that harm one class of creature, cast at the right class and the wrong one |
| `fate` | what Fate does, measured down both routes that reach it |
| `stateleak` | Levitate, Invisible, See Invisible and Protect cast one at a time: which arrive alone |
| `ailments` | how long poison and disease last and what they cost, at various CON |
| `traps` | how often chests appear and which traps they carry, per floor |
| `trapread` | the chest panel (monShowChest): how often the named trap is the real one, the disarm gauge |
| `chest` | what a chest gives up (monOpenChest): how often, how many items, which ones the pick reaches |
| `locks` | a locked chest opened end to end: by a known spell, and by an item with charges |
| `slotroll` | what destroying and stealing attacks pick: a slot, not one of the things carried |
| `exchangeswap` | the Exchange window's swap (pnlItem_DragDrop): pack slots by index, slot 0 refused, the item re-identified |
| `exchange` | the Exchange screen's rules: refusals, gold above and below ground, identification on hand-over |
| `handover` | an item handed to another character re-reads how well they know it (FRMEXCHA.BAS:199-201) |
| `uncurse` | the Seer's uncurse questions, and which a cursed item answers by how it came into the pack |
| `pack` | the forty fixed pack slots: slot numbers that stay put when something leaves |
| `party` | the party roster and the spell buffers: leaving, swapping, pruning |
| `equip` | whether an item goes on and what it is worth (fn11B6, sub0F74): the class and worth tables, every refusal |
| `use` | the Use and Drop commands: each special type, the refusals, an item of another alignment |
| `gearcheck` | the guild-join gear check watched: an item one guild may use and the next may not |
| `trade` | the store's trading: what buying and selling do to the shelf, Combine, the other adventurers' restock |
| `seer` | the Seer: name matching, his fee, his refusals, what he finds |
| `rescue` | getting a body out of the dungeon: the wait at each depth, the morgue's fee, how often rescuers return |
| `raise` | who can be brought back and by what (fn142D), and the chance |
| `rating` | the Guild tab's ability ratings against fn123F |
| `swings` | Multiple Swings: the guild level each guild grants another, and what a round becomes |
| `adinterval` | when a guild awards Att/Def above its cap, from the original's own code |
| `spellgate` | why a caster does or does not know a spell: the guild spell table's gates, then Spellbook.Known |
| `quests` | how often a quest is handed out per guild and level, and what it asks for |
| `guildlog [logdir]` | the guild log's rules (dating, halls, Clean); with a folder, what that log holds |
| `guildmaster` | the guildmasters: who holds each guild, when it changes hands, the crests, the demotion |
| `hall` | the Hall of Records: how a record changes hands, what is announced, the computed measures |
| `asmodeus` | the endgame: where Asmodeus is found, what his chest yields, the ending text |
| `effects` | the effect and resistance model: what each race resists, a worn item's worth, each chest trap |
| `magic` | the spell system and the loot rolls exercised without the UI |
| `x87` | where the port's Doubles and the x87's 80-bit intermediates would give a different whole number |

### The windows' text

| Tool | What it prints |
|---|---|
| `library` | the Library's prose (fn1BBE) at each of the four grades |
| `spellinfo` | the Misc tab's spell page for spells of different classes |
| `companionpage` | the Misc tab's companion page at each ID grade, and the kick-out roll |
| `password` | character passwords: the stored space, the skipped prompt, the case-sensitive compare |

### Simulations

| Tool | What it prints |
|---|---|
| `spawnsim` | 20,000 encounters over every walkable square of floors 1-3: which monsters lead them |
| `goldsim` | what the gold formula pays per floor, beside the cost of making a level |
| `dropsim` | what item generation yields per floor: chests, items, the commonest finds |
| `combat` | random encounters fought per floor through the CombatEngine: wins, deaths, flights, experience, gold, rounds |
| `play` | whole fights by a character in the best gear their level allows, floor by floor (long) - *no sweep* |

### The port's own files

| Tool | What it does |
|---|---|
| `icon <out.ico> <png>...` | packs square 8-bit RGBA PNGs into one Windows icon, each PNG whole as one of its images; the game folder is not read (pass any, e.g. `-`) - *no sweep* |

## The program's icon

`src/Mordor.App/Assets/Icon` holds the program's icon: the port's own drawing, not the game's. The
two SVGs are the source - `opendejenol.svg` (a 256 grid, for 24 pixels and up) and
`opendejenol-16.svg` (simplified by hand for 16) - and the PNGs and `opendejenol.ico` made from
them are committed, so a build needs neither a browser nor the tool. The .ico is the exe's icon
(`<ApplicationIcon>`) and every window's (the `:is(Window)` style in `App.axaml`);
`tools/pack-release.sh` gives Velopack the .ico as the installer's icon and `opendejenol-256.png`
as its splash. After a change to an SVG:

1. render each size with a transparent background - with Edge headless, for instance, on a page
   that holds only `<img src="opendejenol.svg" width="N" height="N">` (margin 0, transparent):
   `msedge --headless=new --force-device-scale-factor=1 --default-background-color=00000000 --window-size=N,N --screenshot=opendejenol-N.png page.html`,
   for N = 24, 32, 48, 64, 128 and 256, and 16 from `opendejenol-16.svg`;
2. check each PNG is N by N with a clear corner pixel;
3. from that folder, `dotnet run --project ../../../../tools/Mordor.Tools -c Release -- - icon opendejenol.ico opendejenol-16.png opendejenol-24.png opendejenol-32.png opendejenol-48.png opendejenol-64.png opendejenol-128.png opendejenol-256.png`.

## The other tools

| Tool | What it is |
|---|---|
| `tools/check.sh` | the full gate, above |
| `tools/export-public.sh` | the developer's: makes and verifies the public tree (it is not in the public tree itself) |
| `tools/gen-mazeselect.sh` | regenerates `src/Mordor.Core/MazeSelect.g.cs` and `MazeSelectPrune.g.cs` from the decompiled source with VbToCs (needs `decompiled/`) |
| `tools/VbToCs` | translates the dungeon view's selection logic (sub0980, MODULE3.BAS) into C# (docs/DUNGEON-VIEW.md) |
| `tools/NeRes` | lists and extracts the named resources of a 16-bit NE module such as MG11.DLL |
| `tools/NameMap` | recovers the original identifiers of the decompiled source into `names.tsv` (docs/DECOMPILE.md) |
